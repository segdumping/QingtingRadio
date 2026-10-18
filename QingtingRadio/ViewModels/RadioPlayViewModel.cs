using QingtingRadio.DataModels;
using System;
using System.Text;
using QingtingRadio.Https;
using System.Runtime.Serialization.Json;
using System.IO;
using Windows.Foundation.Collections;
using Windows.Media.Playback;
using System.Diagnostics;
using QingtingRadio.Tools;
using System.Collections.Generic;
using Windows.System.Profile;
using Windows.Storage.Streams;
using Windows.Security.ExchangeActiveSyncProvisioning;
using Windows.ApplicationModel.Background;
using System.Linq;
using Windows.Media.Core;
using QingtingRadio.Services;
using Windows.Media;
using Windows.Storage;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI.Popups;
using Windows.Storage.FileProperties;

namespace QingtingRadio.ViewModels
{
    class RadioPlayViewModel
    {
        private APIService _api = new APIService();
        private MediaPlayer player = PlaybackService.Instance.Player;
        private MediaListViewModel _mediaList = MediaListViewModel.Instance;
        private RadioPlayPanelViewModel PlayPanel = RadioPlayPanelViewModel.Instance;
        // 串行化切台路径：NewPlayItem 内部有多个 await，快速连点电台时必须防止两次调用交错操作 PlaybackList
        private readonly SemaphoreSlim _playLock = new SemaphoreSlim(1, 1);
        public async Task PlayRadioLive(RadioItem item, bool isCustom = false)
        {
            if (item == null) return;
            LastestPlay tmp = new LastestPlay();
            {
                tmp.Id = item.Id;
                tmp.ImageUri = item.ImageUri;
                tmp.Playing = item.NowPlaying;
                tmp.Title = item.Title;
                tmp.Type = item.Type;
            }

            if (isCustom)
            {
                tmp.PlayUri = item.PlayCount;
                tmp.Id = item.Id;
            }
            else
            {
                string mediaId = await PlayInfoHelper.Current.GetMediaId(item.Id);

                if (string.IsNullOrEmpty(mediaId))
                {
                    tmp.PlayUri = string.Format(ServiceURLs.PlayRadio, item.Id, RadioSettingsViewModel.Instance.Bitrate, RadioSettingsViewModel.Instance.DeviceId);
                }
                else
                {
                    tmp.PlayUri = string.Format(ServiceURLs.PlayRadio, mediaId, RadioSettingsViewModel.Instance.Bitrate, RadioSettingsViewModel.Instance.DeviceId);
                }

                try
                {
                    if (item.TimeInterval != null)
                    {

                        string[] tmpTime = item.TimeInterval.Split('-');
                        tmp.StartTime = tmpTime.ElementAt(0);
                        tmp.EndTime = tmpTime.ElementAt(1);
                    }
                }
                catch (Exception ex)
                {
                    LogManager.LogAsync("create radio item info set time", ex);
                }

                tmp.Date = DateTime.Now.ToString("yyyyMMdd");
            }

            if (!await UpdatePlayPanel(tmp, false)) return;
            await NewPlayItem(tmp);
        }

        public async Task PlayRadioReplay(LastestPlay item)
        {
            if (!await UpdatePlayPanel(item, true)) return;

            await NewPlayItem(item);
        }

        private async Task<bool> UpdatePlayPanel(LastestPlay item, bool isReplay)
        {
            if (!AppHelper.Current.NotifyNetState()) return false;

            // 先记住上一档节目：PlayID 马上会被新条目覆盖，而播放会话里还是旧节目的进度
            string previousId = PlayPanel.PlayID;

            PlayPanel.IsBuffering = true;
            PlayPanel.IsReplay = isReplay;
            PlayPanel.ImageUri = string.IsNullOrEmpty(item.ImageUri) ? "ms-appx:///Assets/recommend_defaultbg.png" : item.ImageUri;
            PlayPanel.Title = item.Title;
            PlayPanel.PlayID = item.Id;
            PlayPanel.NowPlaying = string.IsNullOrEmpty(item.Playing) ? "未知" : item.Playing;

            // “正在播出”只是展示信息，绝不能挡在 player.Play() 前面：
            // 自定义电台的 Id 是流地址，拼出的节目单接口在服务端要 6 秒以上才返回 404，
            // 之前 await 在这里会让每次点播都干等数秒（表现为转圈很久）。
            // 改为后台获取，拿到后再回填，且只在还停留在同一档节目时回填。
            if (!isReplay)
            {
                _ = RefreshNowPlayingTitleAsync(item.Id);
            }

            // 收听历史一次读改写完成：旧条目进度落库 + 新条目去重追加（原先分两次全量读写同一文件）
            await SaveToPlayHistory(item, previousId);
            await LastestPlay.SaveCurrentAsync(item);

            return true;
        }

        /// <summary>
        /// 后台刷新“正在播出”标题。非数字频道 Id（例如自定义电台的流地址）直接跳过，
        /// 避免发起必然失败且极慢的节目单请求。
        /// </summary>
        private async Task RefreshNowPlayingTitleAsync(string id)
        {
            try
            {
                if (string.IsNullOrEmpty(id)) return;
                foreach (char c in id)
                {
                    if (!char.IsDigit(c)) return;
                }

                string playingTitle = await PlayInfoHelper.Current.CurrentPlayingTitle(id);
                if (!string.IsNullOrEmpty(playingTitle) && PlayPanel.PlayID == id)
                {
                    PlayPanel.NowPlaying = playingTitle;
                }
            }
            catch (Exception ex)
            {
                LogManager.LogAsync("refresh now playing title", ex);
            }
        }

        public async Task PlayHistoryItem(LastestPlay item)
        {
            // 直播不需要回放面板，其余（节目/专辑/回听）都按回放处理
            if (!await UpdatePlayPanel(item, item.Type != MusicItem.AudioType.Live)) return;

            await NewPlayItem(item);
        }

        public async Task PlayDownloadItem(LastestPlay item)
        {
            if (!await UpdatePlayPanel(item, true)) return;
            await NewPlayItem(item, true);
        }

        public async Task PlaySearchItem(SearchItemProgram item)
        {
            await PlayProgramCoreAsync(item.Id, item.AlbumId, item.AlbumName, item.Title,
                item.ImageUri, item.FilePath, MusicItem.AudioType.Program);
        }

        public async Task PlayAlbumItem(AlbumItem item, string albumId)
        {
            await PlayProgramCoreAsync(item.Id, albumId, item.AlbumName, item.Title,
                item.ImageUri, item.FilePath, MusicItem.AudioType.Album);
        }

        /// <summary>
        /// 节目/专辑点播的共同入口：搜索结果与专辑列表只差来源类型，构造与播放流程完全一致。
        /// </summary>
        private async Task PlayProgramCoreAsync(string id, string albumId, string albumName,
            string programTitle, string imageUri, string filePath, MusicItem.AudioType type)
        {
            LastestPlay tmp = new LastestPlay()
            {
                Id = id,
                Type = type,
                AlbumId = albumId,
                Title = albumName,
                ImageUri = imageUri,
                Playing = programTitle,
                FileName = FileHelper.SanitizeFileName(albumName) + "_" + FileHelper.SanitizeFileName(programTitle) + ".aac",
                PlayUri = string.Format(ServiceURLs.PlayProgram, filePath),
            };

            if (!await UpdatePlayPanel(tmp, true)) return;

            await NewPlayItem(tmp);
        }

        /// <summary>
        /// 播放/暂停按钮统一入口（主面板与迷你播放条共用）。
        /// 失败/无源状态按"重播上次节目"处理，避免失败后按钮点了没反应。
        /// </summary>
        public async Task TogglePlayPauseAsync()
        {
            switch (player.PlaybackSession.PlaybackState)
            {
                case MediaPlaybackState.Playing:
                    player.Pause();
                    break;
                case MediaPlaybackState.Paused:
                    if (!AppHelper.Current.NotifyNetState()) return;
                    player.Play();
                    break;
                case MediaPlaybackState.Buffering:
                case MediaPlaybackState.Opening:
                case MediaPlaybackState.None:
                    // 注：UWP 的 MediaPlaybackState 没有 Failed 状态，播放失败走
                    // MediaPlaybackList.ItemFailed 事件，由各面板把状态复位为可点击重播
                    try
                    {
                        LastestPlay item = await LastestPlay.LoadCurrentAsync();
                        if (item != null)
                        {
                            await PlayHistoryItem(item);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogManager.LogAsync("toggle play pause replay last", ex);
                    }
                    break;
            }
        }

        private async Task NewPlayItem(LastestPlay item, bool isShowToast = false)
        {
            await _playLock.WaitAsync();
            try
            {
                MediaSource source;

                player.Pause();
                bool isAlbumItem = await _mediaList.IsPlayListItem(item.Id, item.AlbumId);
                if (isAlbumItem)
                {
                    // 条目已在列表里，IsPlayListItem 内部已切曲开始播放：必须复位缓冲指示，
                    // 否则 UpdatePlayPanel 刚置上的 IsBuffering=true 会一直留着（界面持续转圈）。
                    PlayPanel.IsBuffering = false;
                    return;
                }
                else
                {
                    if (_mediaList.IsClearPlayList(item.AlbumId))
                    {
                        _mediaList.ClearPlaybackList();
                    }
                }

                bool isFileExist = await FileHelper.Current.CacheExist(item.FileName, FileHelper.CacheType.AudioCache);
                if (!isFileExist)
                {
                    if (isShowToast)
                    {
                        string message = "文件不存在，是否联网播放？";
                        bool result = await AppHelper.ShowMessageDialog(message, "是", "否");
                        if (!result)
                        {
                            // 用户放弃联网播放：之后不会有任何播放状态变化来复位缓冲指示
                            PlayPanel.IsBuffering = false;
                            return;
                        }
                    }

                    source = MediaSource.CreateFromUri(new Uri(item.PlayUri));
                    source.CustomProperties["filepath"] = item.FileName;
                }
                else
                {
                    try
                    {
                        // 缓存目录统一由 FileHelper 解析；无音乐库权限或目录不存在时返回 null
                        StorageFolder radioFolder = await FileHelper.GetAudioCacheFolderAsync();
                        if (radioFolder == null)
                        {
                            throw new FileNotFoundException("QingtingRadio 本地缓存目录不存在或无音乐库权限");
                        }

                        StorageFile radioFile = await radioFolder.GetFileAsync(item.FileName);
                        // CreateFromStream 不复制流，流必须保持打开直到播放完成，不能放进 using
                        IRandomAccessStream openfile = await radioFile.OpenAsync(FileAccessMode.Read);
                        _mediaList.RegisterStream(openfile);
                        source = MediaSource.CreateFromStream(openfile, "audio/x-aac");

                        source.CustomProperties["filepath"] = item.PlayUri;
                    }
                    catch (Exception ex)
                    {
                        source = MediaSource.CreateFromUri(new Uri(item.PlayUri));
                        source.CustomProperties["filepath"] = item.FileName;
                        LogManager.LogAsync("new play item create from stream", ex);
                    }
                }

                // SMTC 统一使用 MediaPlayer 自带实例（与 PlaybackService 里的 ButtonPressed 接管同源），
                // 不再混用 GetForCurrentView 的视图实例
                SystemMediaTransportControls smtc = player.SystemMediaTransportControls;
                if (item.Type ==  MusicItem.AudioType.Program || item.Type == MusicItem.AudioType.Album)
                {
                    smtc.IsNextEnabled = true;
                    smtc.IsPreviousEnabled = true;
                }
                else
                {
                    smtc.IsNextEnabled = false;
                    smtc.IsPreviousEnabled = false;
                }

                // 记录本地缓存文件名：本地流播放没有 Uri，快照需要它来保存"上次播放"的文件名
                source.CustomProperties["localFileName"] = item.FileName ?? string.Empty;
                // PropertySet 不接受 null：Id 为空（例如扫描缓存目录补出来的条目）时用空串占位，避免这里直接抛异常
                source.CustomProperties["mediaId"] = item.Id ?? string.Empty;
                var playbackItem = item.ToPlaybackItem(source);

                if (item.Position > 0 && item.Type !=  MusicItem.AudioType.Live)
                {
                    source.CustomProperties["position"] = item.Position;
                }
                _mediaList.PlaybackList.Items.Add(playbackItem);

                int itemCount = _mediaList.PlaybackList.Items.Count;
                if (itemCount == 1)
                {
                    _mediaList.PlaybackList.StartingItem = playbackItem;
                }
                else if (itemCount > 1)
                {
                    _mediaList.PlaybackList.MoveTo((uint)(itemCount - 1));
                }
                else
                { }

                player.Play();

                _ = ResetBufferingFlagAfterTimeoutAsync();
                await _mediaList.CheckMediaPlaybackList(item.Id, item.Type, item.AlbumId);
            }
            catch (Exception ex)
            {
                LogManager.LogAsync("new play item", ex);
                PlayPanel.IsBuffering = false;
            }
            finally
            {
                _playLock.Release();
            }
        }

        /// <summary>
        /// 缓冲指示兜底：请求失败或超时导致 BufferingEnded 不触发时，20 秒后复位 IsBuffering，
        /// 避免界面停留在无限缓冲状态。
        /// </summary>
        private async Task ResetBufferingFlagAfterTimeoutAsync()
        {
            await Task.Delay(TimeSpan.FromSeconds(20));
            MediaPlaybackState state = player.PlaybackSession.PlaybackState;
            if (state != MediaPlaybackState.Playing && state != MediaPlaybackState.Buffering)
            {
                PlayPanel.IsBuffering = false;
            }
        }

        public async Task UpdatePlayPosition(string itemId)
        {
            try
            {
                double position = player.PlaybackSession.Position.TotalSeconds;
                double total = player.PlaybackSession.NaturalDuration.TotalSeconds;

                List<LastestPlay> history = await LastestPlay.LoadHistoryAsync();
                if (history == null)
                {
                    history = new List<LastestPlay>();
                }

                int index = history.FindIndex(v1 => v1.Id == itemId);
                if (index >= 0)
                {
                    history[index].Position = position;
                    history[index].TotalSeconds = total;
                }

                await LastestPlay.SaveHistoryAsync(history);
            }
            catch (Exception ex)
            {
                LogManager.LogAsync("update play position", ex);
            }
        }

        private async Task SaveToPlayHistory(LastestPlay item, string previousId)
        {
            try
            {
                List<LastestPlay> history = await LastestPlay.LoadHistoryAsync();
                if (history == null)
                {
                    history = new List<LastestPlay>();
                }

                bool changed = false;

                // 上一档节目的收听进度落库（必须在切换播放源之前，此时播放会话还是旧内容）
                if (!string.IsNullOrEmpty(previousId))
                {
                    int prevIndex = history.FindIndex(v => v.Id == previousId);
                    if (prevIndex >= 0)
                    {
                        history[prevIndex].Position = player.PlaybackSession.Position.TotalSeconds;
                        history[prevIndex].TotalSeconds = player.PlaybackSession.NaturalDuration.TotalSeconds;
                        changed = true;
                    }
                }

                // 新条目去重后追加，历史上限 60 条
                if (history.Count > 60)
                {
                    history.RemoveAt(0);
                    changed = true;
                }

                int index = history.FindIndex(v1 => v1.Id == item.Id);
                if (index >= 0)
                {
                    history.RemoveAt(index);
                }
                item.StartTime = DateTime.Now.ToString();
                history.Add(item);
                changed = true;

                if (changed)
                {
                    await LastestPlay.SaveHistoryAsync(history);
                }
            }
            catch (Exception ex)
            {
                LogManager.LogAsync("save play history", ex);
            }
        }

        private RadioPlayViewModel() { }
        public static readonly RadioPlayViewModel Instance = new RadioPlayViewModel();
    }
}
