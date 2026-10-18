using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.FileProperties;

namespace QingtingRadio.Tools
{
    public class FileHelper
    {      
        public enum CacheType
        { 
            ImageCache = 0,
            AudioCache = 1,
        }

        private Windows.Storage.StorageFolder _local_folder;
        // 串行化 DatasCache 下 JSON 的读写：
        // 1) 下载进度等高频写入并发执行时避免互相覆盖；
        // 2) 防止"写替换(MoveAndReplace)期间正好有读打开文件"导致读失败/写失败
        private static readonly SemaphoreSlim _ioLock = new SemaphoreSlim(1, 1);

        private FileHelper()
        {
            _local_folder = Windows.Storage.ApplicationData.Current.LocalFolder;
        }

        public async Task WriteObjectAsync<T>(T obj, string filename)
        {
            await _ioLock.WaitAsync();
            try
            {
                var folder = await _local_folder.CreateFolderAsync("DatasCache", CreationCollisionOption.OpenIfExists);
                string tmpName = filename + ".tmp";

                using (var data = await folder.OpenStreamForWriteAsync(tmpName, CreationCollisionOption.ReplaceExisting))
                {
                    DataContractJsonSerializer serizlizer = new DataContractJsonSerializer(typeof(T));
                    serizlizer.WriteObject(data, obj);
                    await data.FlushAsync();
                }

                // 临时文件 + 替换：中途崩溃/断电不会留下半截 JSON 把原文件写坏
                StorageFile tmp = await folder.GetFileAsync(tmpName);
                StorageFile target = await folder.CreateFileAsync(filename, CreationCollisionOption.OpenIfExists);
                await tmp.MoveAndReplaceAsync(target);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
                LogManager.LogAsync("write object " + filename, ex);
            }
            finally
            {
                _ioLock.Release();
            }
        }
        public async Task<T> ReadObjectAsync<T>(string filename) where T : class
        {
            await _ioLock.WaitAsync();
            try
            {
                var folder = await _local_folder.CreateFolderAsync("DatasCache", CreationCollisionOption.OpenIfExists);

                // 文件不存在是正常情况（首次运行/尚未产生过数据）：直接返回 null。
                // 之前靠 FileNotFoundException 来表达"没有数据"，调试器会在每次无文件读取时中断，
                // 日志里也全是异常噪音。
                StorageFile file = await folder.TryGetItemAsync(filename) as StorageFile;
                if (file == null)
                {
                    return null;
                }

                using (var data = await file.OpenStreamForReadAsync())
                {
                    DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(T));

                    return serializer.ReadObject(data) as T;
                }
            }
            catch(Exception ex)
            {
                Debug.WriteLine(ex.ToString());
                LogManager.LogAsync("read object " + filename, ex);

                return null;
            }
            finally
            {
                _ioLock.Release();
            }
        }

        /// <summary>
        /// 保存图片的原始压缩字节。相比"解码成 WriteableBitmap 再重编码"，免去画质损失与 CPU 开销。
        /// </summary>
        public async Task SaveImageBytesAsync(byte[] bytes, string filename)
        {
            if (bytes == null || bytes.Length == 0)
            {
                return;
            }

            try
            {
                var folder = await _local_folder.CreateFolderAsync("ImagesCache", CreationCollisionOption.OpenIfExists);
                var file = await folder.CreateFileAsync(filename, CreationCollisionOption.ReplaceExisting);
                await FileIO.WriteBytesAsync(file, bytes);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
                LogManager.LogAsync("save image " + filename, ex);
            }
        }

        public async Task<bool> DeleteCacheFile()
        {
            try
            {
                StorageFolder folder = await _local_folder.GetFolderAsync("ImagesCache");
                if (folder != null)
                {
                    IReadOnlyCollection<StorageFile> files = await folder.GetFilesAsync(Windows.Storage.Search.CommonFileQuery.DefaultQuery);
                    //files.ToList().ForEach(async (f) => await f.DeleteAsync(StorageDeleteOption.PermanentDelete));
                    List<IAsyncAction> list = new List<IAsyncAction>();
                    foreach (var f in files)
                    {
                        list.Add(f.DeleteAsync(StorageDeleteOption.PermanentDelete));
                    }
                    List<Task> list2 = new List<Task>();
                    list.ForEach((t) => list2.Add(t.AsTask()));

                    await Task.Run(() => { Task.WaitAll(list2.ToArray()); });                  
                }
                return true;
            }
            catch(Exception ex)
            {
                Debug.WriteLine(ex.ToString());
                LogManager.LogAsync("delete cache file", ex);

                return false;
            }
        }

        /// <summary>
        /// 把用户/接口给的字符串清洗成可用的文件名片段：
        /// 去掉 Windows 文件名非法字符与控制字符，并去掉首尾空白与结尾的点。
        /// 下载落盘、本地缓存查找、播放时拼文件名必须共用这一个方法，否则会两处拼出不同名字、缓存查不中。
        /// </summary>
        public static string SanitizeFileName(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;

            const string forbidden = "\\/:*?\"<>|&#!@$%^,";
            StringBuilder builder = new StringBuilder(name.Length);
            foreach (char c in name)
            {
                if (char.IsControl(c)) continue;
                if (forbidden.IndexOf(c) >= 0) continue;
                builder.Append(c);
            }

            string result = builder.ToString().Trim().TrimEnd('.');
            return string.IsNullOrEmpty(result) ? "未命名" : result;
        }

        public async Task<double> GetCacheSize()
        {
            try
            {
                StorageFolder folder = await _local_folder.GetFolderAsync("ImagesCache");
                var files = await folder.GetFilesAsync(Windows.Storage.Search.CommonFileQuery.DefaultQuery);
                double size = 0; BasicProperties p;
                foreach (var f in files)
                {
                    p = await f.GetBasicPropertiesAsync();
                    size += p.Size;
                }
                return size;
            }
            catch(Exception ex)
            {
                Debug.WriteLine(ex.ToString());
                LogManager.LogAsync("get cache size", ex);

                return 0;
            }
        }

        /// <summary>
        /// 解析音乐库下的 QingtingRadio 音频缓存目录；无音乐库权限或目录不存在时返回 null。
        /// 全项目统一从这里取，避免各处自行 TryGetItemAsync 再 as 的重复模式。
        /// </summary>
        public static async Task<StorageFolder> GetAudioCacheFolderAsync()
        {
            try
            {
                var item = await Windows.Storage.KnownFolders.MusicLibrary.TryGetItemAsync("QingtingRadio");
                return item as StorageFolder;
            }
            catch (Exception ex)
            {
                LogManager.LogAsync("get audio cache folder", ex);
                return null;
            }
        }

        public async Task<bool> CacheExist(string filename, CacheType cachetype)
        {
            try
            {
                StorageFolder f = null;
                switch(cachetype)
                {
                    case CacheType.AudioCache:
                        f = await GetAudioCacheFolderAsync();
                        break;
                    case CacheType.ImageCache:
                        var ic = await _local_folder.TryGetItemAsync("ImagesCache");
                        f = ic as StorageFolder;
                        break;
                    default:
                        break;
                }

                if (f != null)
                {
                    var f2 = await f.TryGetItemAsync(filename);
                    if (f2 == null) return false;
                    else
                    {
                        return true;
                    }
                }
                else
                {
                    return false;
                }
            }
            catch(Exception ex)
            {
                Debug.WriteLine(ex.ToString());

                return false;
            }
        }

        public static readonly FileHelper Current = new FileHelper();
    }
}
