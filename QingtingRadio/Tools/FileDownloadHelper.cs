using QingtingRadio.DataModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Networking.BackgroundTransfer;
using Windows.Storage;
using Windows.Storage.FileProperties;
using QingtingRadio.ViewModels;

namespace QingtingRadio.Tools
{
    public class FileDownloadHelper
    {
        private CancellationTokenSource cts;
        private List<DownloadOperation> activeDownloads;
        // 每个后台传输单独的取消令牌：DownloadOperation 本身没有 Cancel()，
        // 取消要通过 StartAsync()/AttachAsync() 返回的异步操作来触发（见 HandleDownloadAsync）。
        private readonly Dictionary<Guid, CancellationTokenSource> downloadTokens = new Dictionary<Guid, CancellationTokenSource>();
        private ObservableCollection<DownloadItem> downloadFiles;
        // 上次落盘时间戳：进度回调频率极高，写盘按节流 + 关键节点强制落盘
        private long _lastPersistTicks;

        public event DataLoadingEventHandler DataLoading;
        public event DataLoadedEventHandler DataLoaded;

        public ObservableCollection<DownloadItem> DownloadFiles
        {
            get
            {
                return downloadFiles;
            }
        }

        private FileDownloadHelper()
        {
            cts = new CancellationTokenSource();
            activeDownloads = new List<DownloadOperation>();
            downloadFiles = new ObservableCollection<DownloadItem>();

            _ = LoadDownloadCache();
        }

        private async Task LoadDownloadCache()
        {
            List<DownloadItem> downloadLists = await FileHelper.Current.ReadObjectAsync<List<DownloadItem>>("DownloadFiles.json");
            if (downloadLists == null) 
            {
                downloadLists = new List<DownloadItem>();
            }
            var files = await FileHelper.GetAudioCacheFolderAsync();
            if (files == null) return;
            if (DataLoading != null) DataLoading();
            IReadOnlyList<IStorageItem> items = await files.GetItemsAsync();
            foreach (var item in items)
            {
                if (!item.IsOfType(StorageItemTypes.File)) continue;

                int index = -1;
                if (downloadLists != null)
                {
                    index = downloadLists.FindIndex(v1 => v1.FileName == item.Name);
                }

                BasicProperties property = await item.GetBasicPropertiesAsync();
                if (index >= 0)
                {
                    downloadLists.ElementAt(index).FileSize = property.Size;
                }
                else
                {
                    DownloadItem tmp = new DownloadItem()
                    {
                        FileName = item.Name,
                        FilePath = item.Path,
                        IsSuccess = true,
                        Type = MusicItem.AudioType.Replay,
                    };

                   
                    tmp.FileSize = property.Size;
                    tmp.Progress = tmp.FileSize > 0 ? 100 : 0;

                    try
                    {
                        string[] name = item.Name.Split('_');
                        tmp.Title = name.ElementAt(1).Split('.').ElementAt(0);
                        tmp.AlbumName = name.ElementAt(0);
                        if (name.Length > 2)
                        {
                            tmp.AudioTime = name.ElementAt(2);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogManager.LogAsync("load download cache  name split error", ex);
                    }

                    downloadLists.Add(tmp);
                }
            }

            if (downloadLists != null)
            {
                downloadLists.ForEach(c =>
                {
                    downloadFiles.Add(c);
                });
            }

            await SaveCacheToDownloadFile(true);
            if (DataLoaded != null) DataLoaded();
        }

        public async Task StartDownload(DownloadItem downloadItem, bool isNewDownload = true)
        {
            if (downloadItem == null) return;
            if (!Uri.TryCreate(downloadItem.FileUri, UriKind.Absolute, out Uri source))
            {
                return;
            }

            // 兜底清洗文件名：调用方可能带入 Windows 不允许的字符，CreateFileAsync 会直接抛异常，
            // 下载表面上看是"点了没反应"。这里就地改写，保证落盘名与列表记录、后续查找用的是同一个名字。
            downloadItem.FileName = FileHelper.SanitizeFileName(downloadItem.FileName);
            if (string.IsNullOrEmpty(downloadItem.FileName))
            {
                return;
            }

            try
            {
                StorageFile destinationFile;
                try
                {
                    var folder = await Windows.Storage.KnownFolders.MusicLibrary.CreateFolderAsync("QingtingRadio", CreationCollisionOption.OpenIfExists);
                    destinationFile = await folder.CreateFileAsync(downloadItem.FileName, CreationCollisionOption.OpenIfExists);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.ToString());

                    return;
                }

                BackgroundDownloader downloader = new BackgroundDownloader();
                DownloadOperation download = downloader.CreateDownload(source, destinationFile);
                download.Priority = BackgroundTransferPriority.High;

                if (isNewDownload)
                {
                    DeleteExistedFileName(downloadItem.FileName);
                }

                if (downloadItem.ID == Guid.Empty)
                {
                    int index = -1;
                    List<DownloadItem> files = await FileHelper.Current.ReadObjectAsync<List<DownloadItem>>("DownloadFiles.json");
                    if (files == null)
                    {
                        files = new List<DownloadItem>();
                    }
                    else
                    {
                        index = files.FindIndex(v1 => v1.ID == download.Guid);
                    }
                    if (index < 0)
                    {
                        downloadItem.FilePath = download.ResultFile.Path;
                     //   double ratio = RadioSettingsViewModel.Instance.IsHighBitrate ? 7908.026546 : 3004.381389;
                        downloadItem.ID = download.Guid;
                        downloadFiles.Insert(0, downloadItem);
                        files.Add(downloadItem);
                        await FileHelper.Current.WriteObjectAsync<List<DownloadItem>>(files, "DownloadFiles.json");
                    }
                }
                else
                {
                    await UpdateExistCacheFileGuid(downloadItem.ID, download.Guid);
                }

                AppHelper.Current.ShowOperationMessage("【" + downloadItem.Title + "】" + "开始下载");
                await HandleDownloadAsync(download, true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
                LogManager.LogAsync("start download " + downloadItem.FileName, ex);
            }
        }

        private async Task HandleDownloadAsync(DownloadOperation download, bool start)
        {
            CancellationTokenSource downloadCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token);
            try
            {
                activeDownloads.Add(download);
                lock (downloadTokens)
                {
                    downloadTokens[download.Guid] = downloadCts;
                }

                Progress<DownloadOperation> progressCallback = new Progress<DownloadOperation>(DownloadProgress);
                if (start)
                {
                    await download.StartAsync().AsTask(downloadCts.Token, progressCallback);
                }
                else
                {
                    await download.AttachAsync().AsTask(downloadCts.Token, progressCallback);
                }

                ResponseInformation response = download.GetResponseInformation();

                string statusCode = response != null ? response.StatusCode.ToString() : String.Empty;

            }
            catch (TaskCanceledException)
            {

            }
            catch (OperationCanceledException)
            {

            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
                await SyncDownloadMemoryCache(download, false);
                await DeletePartialFileAsync(download);
            }
            finally
            {
                activeDownloads.Remove(download);
                lock (downloadTokens)
                {
                    downloadTokens.Remove(download.Guid);
                }
                downloadCts.Dispose();
            }
        }

        /// <summary>
        /// 下载失败后清掉半成品文件：否则它会一直占空间，还会让"文件已存在"的判断失真。
        /// </summary>
        private async Task DeletePartialFileAsync(DownloadOperation download)
        {
            try
            {
                IStorageFile file = download?.ResultFile;
                if (file != null)
                {
                    await file.DeleteAsync(StorageDeleteOption.PermanentDelete);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
                LogManager.LogAsync("delete partial download file", ex);
            }
        }

        private async void DownloadProgress(DownloadOperation download)
        {
            // Progress<T> 回调要求 Action 签名，只能保持 async void，必须自带异常保护
            try
            {
                BackgroundDownloadProgress currentProgress = download.Progress;

                await SyncDownloadMemoryCache(download, true);

                if (currentProgress.HasRestarted)
                { }

                if (currentProgress.HasResponseChanged)
                {
                    ResponseInformation response = download.GetResponseInformation();
                    int headersCount = response != null ? response.Headers.Count : 0;

                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
                LogManager.LogAsync("download progress", ex);
            }
        }

        public async Task<bool> DeleteDownloadFile(DownloadItem file)
        {
            try
            {
                //从内存缓存中删除
                string filename = null;
                {
                    int index = activeDownloads.FindIndex(v1 => v1.Guid == file.ID);
                    if(index >= 0)
                    {
                        DownloadOperation currentOperation = activeDownloads.ElementAt(index);
                        try
                        {
                            // 必须真正取消而不是 Pause：Pause 只是挂起，后台传输仍活着并占着目标文件，
                            // 紧接着删文件会因文件被占用而失败（表现为"删了还在"）。
                            // DownloadOperation 没有 Cancel()，要取消承载它的异步操作。
                            CancellationTokenSource downloadCts;
                            lock (downloadTokens)
                            {
                                downloadTokens.TryGetValue(currentOperation.Guid, out downloadCts);
                            }
                            downloadCts?.Cancel();
                        }
                        catch (Exception ex)
                        {
                            LogManager.LogAsync("cancel download operation", ex);
                        }
                        activeDownloads.RemoveAt(index);
                    }                 
                }

                for (int i = 0; i < downloadFiles.Count; i++)
                {
                    if(file.ID == Guid.Empty)
                    {
                        if(!downloadFiles.ElementAt(i).FileName.Equals(file.FileName))
                        {
                            continue;
                        }
                    }
                    else
                    {
                        if(downloadFiles.ElementAt(i).ID != file.ID)
                        {
                            continue;
                        }
                    }

                    filename = downloadFiles.ElementAt(i).FileName;          
                    downloadFiles.RemoveAt(i);
                    await SaveCacheToDownloadFile(true);
                    break;
                    
                }

                //删除文件
                StorageFolder radioFolder = await FileHelper.GetAudioCacheFolderAsync();
                if (radioFolder == null)
                {
                    return false;
                }

                // 没在列表里匹配到条目时（例如顺手清理残留文件），退回用传入的文件名
                if (string.IsNullOrEmpty(filename))
                {
                    filename = file.FileName;
                }

                if (string.IsNullOrEmpty(filename))
                {
                    return true;
                }

                StorageFile radioFile = await radioFolder.TryGetItemAsync(filename) as StorageFile;
                if (radioFile == null)
                {
                    // 文件本来就不存在：内存与持久化列表都已移除，视为删除成功
                    return true;
                }

                // 取消是异步生效的：后台传输可能还短暂占用文件，删不掉就稍等后重试
                Exception lastError = null;
                for (int attempt = 0; attempt < 5; attempt++)
                {
                    try
                    {
                        await radioFile.DeleteAsync();
                        return true;
                    }
                    catch (Exception ex)
                    {
                        lastError = ex;
                        await Task.Delay(200);
                    }
                }

                LogManager.LogAsync("delete download file failed " + filename, lastError);
                return false;
            }
            catch(Exception ex)
            {
                Debug.WriteLine(ex.ToString());
                LogManager.LogAsync("delete download file", ex);

                return false;
            }

            return true;
        }

        private async Task SyncDownloadMemoryCache(DownloadOperation download, bool isSuccess)
        {
            BackgroundDownloadProgress currentProgress = download.Progress;

            int index = -1;
            for (int i = 0; i < downloadFiles.Count; i++)
            {
                if (downloadFiles.ElementAt(i).ID == download.Guid)
                {
                    index = i;
                    break;
                }
            }

            if(index < 0)
            {
                return;
            }

            bool forceSave = false;
            if (isSuccess)
            {
                double percent = 100;
                if (currentProgress.TotalBytesToReceive > 0)
                {
                    percent = currentProgress.BytesReceived * 100 / currentProgress.TotalBytesToReceive;
                }  
                
                if (percent >= 100)
                {

                    //    downloadFiles[index].Progress = Math.Round(Convert.ToDouble(currentProgress.TotalBytesToReceive) / 1024 /1024, 2, MidpointRounding.ToEven).ToString() + "M";
                    downloadFiles[index].Progress = 100;
                    downloadFiles[index].IsSuccess = true;
                    downloadFiles[index].FileSize = currentProgress.TotalBytesToReceive;
                    forceSave = true;
                    AppHelper.Current.ShowOperationMessage("【" + downloadFiles[index].Title + "】" + "下载成功");
                }
                else
                {
                    downloadFiles[index].Progress = Convert.ToInt32(percent);
                    downloadFiles[index].FileSize = currentProgress.BytesReceived;
                    if (currentProgress.Status == BackgroundTransferStatus.PausedByApplication)
                    {
                        //   downloadFiles[index].FileSize = "暂停";
                        downloadFiles[index].FileSize = -1;
                        forceSave = true;
                    }
                }
            }
            else
            {
                downloadFiles[index].FileSize = -3;
                forceSave = true;
            }

            await SaveCacheToDownloadFile(forceSave);
        }

        /// <summary>
        /// 把下载列表持久化到 JSON。进度回调按 500ms 节流；完成/暂停/失败等关键节点传入 force 立即落盘。
        /// </summary>
        private async Task SaveCacheToDownloadFile(bool force = false)
        {
            long now = DateTime.Now.Ticks;
            if (!force && now - _lastPersistTicks < TimeSpan.TicksPerSecond / 2)
            {
                return;
            }
            _lastPersistTicks = now;

            await FileHelper.Current.WriteObjectAsync<List<DownloadItem>>(downloadFiles.ToList<DownloadItem>(), "DownloadFiles.json");
        }

        public async Task CheckDownloadState(Guid guid)
        {
            int index = activeDownloads.FindIndex((c) => c.Guid == guid);
            if(index < 0)
            {
                List<DownloadItem> tmp = await FileHelper.Current.ReadObjectAsync<List<DownloadItem>>("DownloadFiles.json");
                if (tmp == null)
                {
                    return;
                }

                int fileIndex = tmp.FindIndex(v1 => v1.ID == guid);
                if (fileIndex < 0)
                {
                    return;
                }

                DownloadItem file = tmp.ElementAt(fileIndex);
                await StartDownload(file, false);

                return;
            }

            DownloadOperation currentOperation = activeDownloads.ElementAt(index);
            switch(currentOperation.Progress.Status)
            {
                case BackgroundTransferStatus.Running:
                    currentOperation.Pause();                 
                    break;
                case BackgroundTransferStatus.PausedByApplication:
                    currentOperation.Resume();
                    break;
                default:
                    break;

            }
        }

        private async Task  UpdateExistCacheFileGuid(Guid oldGuid, Guid newGuid)
        {
            int cacheIndex = -1;
            for (int i = 0; i < downloadFiles.Count; i++)
            {
                if (downloadFiles.ElementAt(i).ID == oldGuid)
                {
                    cacheIndex = i;
                    break;
                }
            }
            if(cacheIndex >= 0)
            {
                downloadFiles.ElementAt(cacheIndex).ID = newGuid;

                await SaveCacheToDownloadFile(true);
            }         
        }

        private void DeleteExistedFileName(string fileName)
        {
            for (int i = 0; i < downloadFiles.Count; i++)
            {
                if (downloadFiles.ElementAt(i).FileName.Equals(fileName))
                {
                    downloadFiles.RemoveAt(i);
                    break;
                }
            }
        }

        public static readonly FileDownloadHelper Current = new FileDownloadHelper();
    }
}
