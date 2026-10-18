using QingtingRadio.DataModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Media.Playback;
using QingtingRadio.Https;
using QingtingRadio.Services;
using Windows.Media.Core;
using System.ComponentModel;
using QingtingRadio.Tools;
using Windows.Storage;
using Windows.Storage.Streams;
using System.Diagnostics;
using System.Threading;

namespace QingtingRadio.ViewModels
{
    public class MediaListViewModel : INotifyPropertyChanged
    {
        APIService _api = new APIService();
        MediaPlayer player = PlaybackService.Instance.Player;

        private MediaPlaybackList _playbackList;
        private int _currentPage = 1;
        // 保护 Items 增删/MoveTo 的互斥锁，替代原来"忙等自旋"的 _isBusy 标志
        private readonly SemaphoreSlim _listLock = new SemaphoreSlim(1, 1);
        // 播放列表里为本地缓存文件打开的流，必须在列表整体清空时统一释放，否则句柄越积越多
        private readonly List<IRandomAccessStream> _openedStreams = new List<IRandomAccessStream>();
        // 列表"代次"：ClearPlaybackList 每清空一次就自增。
        // 分页加载（AppendLoad/CurrentPlayAlbumChanged）会跨 await，换台清空后如果继续按旧代次插入，
        // 就会把上一档节目的条目混进新列表，这里用代次比对来作废过期的插入。
        private int _listGeneration;
        bool _canSkipNext;
        bool _canSkipPrevious;

        private AlbumInfo _albumInfo;
        private string _currentMediaId;
        private MusicItem.AudioType _mediaType =  MusicItem.AudioType.None;

        public MediaPlaybackList PlaybackList { get { return _playbackList; } }

        public MusicItem.AudioType MediaType
        {
            get { return _mediaType; }
        }

        public string CurrentMediaId
        {
            get { return _currentMediaId; }
        }

        public AlbumInfo LastestAlbumInfo
        {
            get { return _albumInfo; }
        }

        public int SelectedIndex
        {
            get
            {
                return (int)_playbackList.CurrentItemIndex;
            }
            set
            {
                OnPropertyChanged("SelectedIndex");
            }
        }

        public bool CanSkipNext
        {
            get { return _canSkipNext; }
            set
            {
                if (_canSkipNext != value)
                {
                    _canSkipNext = value;
                    OnPropertyChanged("CanSkipNext");
                }
            }
        }

        public bool CanSkipPrevious
        {
            get { return _canSkipPrevious; }
            set
            {
                if (_canSkipPrevious != value)
                {
                    _canSkipPrevious = value;
                    OnPropertyChanged("CanSkipPrevious");
                }
            }
        }


        private async Task<List<AlbumItem>> LoadMoreAlbumitem(int pageSize = 60)
        {
            if (_albumInfo == null || string.IsNullOrEmpty(_albumInfo.Id)) return null;
            string _uri = string.Format(ServiceURLs.AlbumList, _albumInfo.Id, _albumInfo.Version, _currentPage, pageSize);
            List<AlbumItem> list = await _api.GetAlbumList(_uri, _albumInfo.Id);

            // 只有真正取到数据才推进页码，网络失败时下次重试同一页
            if (list != null)
            {
                _currentPage++;
            }
            return list;
        }

        public bool IsClearPlayList(string albumId)
        {
            return _albumInfo == null || albumId != _albumInfo.Id;
        }

        /// <summary>
        /// 判断给定节目是否已在播放列表内；已在则直接切到该条目并开始播放。
        /// 返回 false 表示"不在列表里"，由调用方决定重新入列播放。
        /// </summary>
        public async Task<bool> IsPlayListItem(string mediaId, string albumId)
        {
            if (mediaId == null) return false;
            if (_playbackList.Items.Count == 0) return false;
            if (_albumInfo == null) return false;
            if (albumId != _albumInfo.Id) return false;

            _currentMediaId = mediaId;

            bool result = await CurrentIndexChanged();

            return result;
        }

        public async Task<bool> AppendLoad()
        {
            if (_albumInfo == null) return false;
            int programCount = 0;
            int.TryParse(_albumInfo.PragromCount, out programCount);
            int playbackCount = _playbackList.Items.Count;
            if (programCount > 0 && playbackCount >= programCount) return false;

            int generation = _listGeneration;
            List<AlbumItem> list = await LoadMoreAlbumitem();
            // 空列表说明分页已取尽：必须视为终止，否则调用方会无限翻页、无限打接口
            if (list == null || list.Count == 0) return false;

            await _listLock.WaitAsync();
            try
            {
                // 取数据期间换台把列表清空了：这批数据已过期，不能追加
                if (generation != _listGeneration) return false;

                StorageFolder files = await FileHelper.GetAudioCacheFolderAsync();

                foreach (var c in list)
                {
                    var playbackItem = await CreatePlaybackItem(c, files);
                    if (playbackItem == null) continue;

                    _playbackList.Items.Add(playbackItem);
                }
            }
            finally
            {
                _listLock.Release();
            }

            return true;
        }

        /// <summary>
        /// 登记为播放列表打开的本地文件流，供清空列表时统一释放。
        /// </summary>
        public void RegisterStream(IRandomAccessStream stream)
        {
            if (stream == null) return;
            _openedStreams.Add(stream);
        }

        /// <summary>
        /// 清空播放列表并释放其关联的本地文件流。
        /// </summary>
        public void ClearPlaybackList()
        {
            _listGeneration++;

            _playbackList.Items.Clear();

            IRandomAccessStream[] dead = _openedStreams.ToArray();
            _openedStreams.Clear();
            if (dead.Length == 0) return;

            _ = DisposeStreamsWhenSafeAsync(dead);
        }

        /// <summary>
        /// 释放旧播放列表关联的本地文件流。
        /// 必须等媒体引擎"确实开始播放新内容"之后再释放：释放一个仍被 MediaPlayer 引用的流，
        /// 会让媒体管线访问已关闭的文件，属于进程级崩溃风险。
        /// 限定时间内没有确认新内容开播，就直接放弃释放（宁可句柄延迟回收，也不再冒险）。
        /// </summary>
        private async Task DisposeStreamsWhenSafeAsync(IRandomAccessStream[] streams)
        {
            // 最多轮询 30 秒，等待新内容进入播放状态
            for (int i = 0; i < 15; i++)
            {
                await Task.Delay(TimeSpan.FromSeconds(2));
                if (player.PlaybackSession.PlaybackState == MediaPlaybackState.Playing)
                {
                    break;
                }
            }

            if (player.PlaybackSession.PlaybackState != MediaPlaybackState.Playing)
            {
                LogManager.LogAsync("skip disposing old audio streams: player not playing new content");
                return;
            }

            // 已确认在新内容上播放，再留几秒缓冲时间，消除媒体引擎可能残留的引用
            await Task.Delay(TimeSpan.FromSeconds(3));

            foreach (var stream in streams)
            {
                try
                {
                    stream.Dispose();
                }
                catch (Exception ex)
                {
                    LogManager.LogAsync("dispose audio stream", ex);
                }
            }
        }

        public async Task CheckMediaPlaybackList(string mediaId, MusicItem.AudioType playType, string albumId = null)
        {
            if (mediaId == null) return;

            CanSkipNext = false;
            CanSkipPrevious = false;
            _currentMediaId = mediaId;
            _mediaType = playType;
            switch (playType)
            {
                case MusicItem.AudioType.Live:
                case MusicItem.AudioType.Replay:
                    {
                        _currentPage = 1;
                        _albumInfo = null;

                    }
                    break;
                case MusicItem.AudioType.Program:
                case MusicItem.AudioType.Album:
                    {
                        // 没有专辑上下文时不能直接 return：那会把 CanSkip 永久留在 false，
                        // 导致明明有播放列表，上一曲/下一曲按钮却一直是禁用的。这里改为去按实际列表长度决定。
                        if (albumId == null) break;
                        // 节目总数解析失败时视为"列表已完整"，不做整表重建
                        int programCount = 0;
                        int.TryParse(_albumInfo?.PragromCount, out programCount);
                        if (_albumInfo == null || (_albumInfo != null && albumId != _albumInfo.Id) ||
                            (programCount > 0 && _playbackList.Items.Count < programCount))
                        {
                            _currentPage = 1;
                            _albumInfo = await LoadAlbumInfo(albumId);
                            await CurrentPlayAlbumChanged();
                        }
                    }
                    break;
                default:
                    break;
            }

            // 统一按实际播放列表恢复上一曲/下一曲可用状态：
            // 只有节目/专辑这种有列表的点播才可切曲，直播/回听固定禁用。
            bool canSkip = (playType == MusicItem.AudioType.Program || playType == MusicItem.AudioType.Album)
                && _playbackList.Items.Count > 1;
            CanSkipNext = canSkip;
            CanSkipPrevious = canSkip;
        }


        private async Task CurrentPlayAlbumChanged()
        {
            int insertIndex = 0;
            bool hasCurrentPlay = false;

            await _listLock.WaitAsync();
            try
            {
                int generation = _listGeneration;

                // 已经在列表里的条目（按 mediaId）。同一专辑分页没取满时会再次走到这里，
                // 若不去重，就会把第一页的节目重复插入到列表头部。
                HashSet<string> existingIds = new HashSet<string>();
                foreach (MediaPlaybackItem existed in _playbackList.Items)
                {
                    string existedId = existed?.Source?.CustomProperties != null
                        ? existed.Source.CustomProperties["mediaId"] as string
                        : null;
                    if (!string.IsNullOrEmpty(existedId))
                    {
                        existingIds.Add(existedId);
                    }
                }

                StorageFolder files = await FileHelper.GetAudioCacheFolderAsync();


                for (;;)
                {
                    // 取数据期间换台清空了列表：本轮的插入目标已失效，直接放弃
                    if (generation != _listGeneration)
                    {
                        break;
                    }

                    if (hasCurrentPlay && _playbackList.Items.Count - insertIndex > 3)
                    {
                        break;
                    }
                    List<AlbumItem> list = await LoadMoreAlbumitem();
                    // 空列表视为分页取尽，避免目标条目不存在时无限翻页
                    if (list == null || list.Count == 0) break;


                    foreach (var c in list)
                    {
                        if (generation != _listGeneration)
                        {
                            hasCurrentPlay = true;
                            break;
                        }

                        bool isCurrent = c.Id == _currentMediaId;

                        // 已在列表中的条目不再重复插入，只推进游标 / 记录目标位置
                        if (existingIds.Contains(c.Id))
                        {
                            if (isCurrent)
                            {
                                hasCurrentPlay = true;
                            }
                            else if (!hasCurrentPlay)
                            {
                                insertIndex++;
                            }
                            continue;
                        }

                        var playbackItem = await CreatePlaybackItem(c, files);
                        if (playbackItem == null) continue;

                        existingIds.Add(c.Id);

                        if (isCurrent)
                        {
                            // 目标节目固定插在"它前面那些节目"之后
                            hasCurrentPlay = true;
                            _playbackList.Items.Insert(insertIndex, playbackItem);
                            insertIndex++;
                        }
                        else if (hasCurrentPlay)
                        {
                            _playbackList.Items.Add(playbackItem);
                        }
                        else
                        {
                            _playbackList.Items.Insert(insertIndex, playbackItem);
                            insertIndex++;
                        }
                    }
                }

                if (hasCurrentPlay)
                {
                    int index = FindPlaybackItemIndex(_currentMediaId);
                    if (index >= 0)
                    {
                        _playbackList.MoveTo((uint)index);
                    }
                }
            }
            finally
            {
                _listLock.Release();
            }
        }

        /// <summary>
        /// 在当前播放列表中按 mediaId 定位条目下标，找不到返回 -1。
        /// </summary>
        private int FindPlaybackItemIndex(string mediaId)
        {
            if (string.IsNullOrEmpty(mediaId)) return -1;

            for (int i = 0; i < _playbackList.Items.Count; i++)
            {
                var properties = _playbackList.Items[i]?.Source?.CustomProperties;
                if (properties != null && (properties["mediaId"] as string) == mediaId)
                {
                    return i;
                }
            }

            return -1;
        }

        private async Task<MediaPlaybackItem> CreatePlaybackItem(AlbumItem c, StorageFolder files)
        {
            MediaSource source;
            bool isExist = false;
            string fileName = _albumInfo.Title + "_" + c.Title + ".aac";

            if (files == null)
            {
                isExist = false;
            }
            else
            {
                var f2 = await files.TryGetItemAsync(fileName);
                if (f2 != null)
                {
                    isExist = true;
                }
            }


            if (isExist && files != null)
            {
                try
                {
                    StorageFile radioFile = await files.GetFileAsync(fileName);
                    // CreateFromStream 不复制流，流必须保持打开直到播放完成，不能放进 using
                    IRandomAccessStream openfile = await radioFile.OpenAsync(FileAccessMode.Read);
                    RegisterStream(openfile);
                    source = MediaSource.CreateFromStream(openfile, "audio/x-aac");
                    source.CustomProperties["filepath"] = string.Format(ServiceURLs.PlayProgram, c.FilePath);
                }
                catch (Exception ex)
                {
                    source = MediaSource.CreateFromUri(new Uri(string.Format(ServiceURLs.PlayProgram, c.FilePath)));
                    source.CustomProperties["filepath"] = fileName;
                    LogManager.LogAsync("current play album  create from stream", ex);
                }
            }
            else
            {
                source = MediaSource.CreateFromUri(new Uri(string.Format(ServiceURLs.PlayProgram, c.FilePath)));
                source.CustomProperties["filepath"] = fileName;
            }

            source.CustomProperties["localFileName"] = fileName;
            source.CustomProperties["mediaId"] = c.Id;
            c.AlbumId = _albumInfo.Id;
            c.AlbumName = _albumInfo.Title;

            return c.ToPlaybackItem(source);
        }

        private async Task<bool> CurrentIndexChanged()
        {
            for (;;)
            {
                MediaPlaybackItem item = null;
                await _listLock.WaitAsync();
                try
                {
                    item = _playbackList.Items.FirstOrDefault(r =>
                        r.Source.CustomProperties["mediaId"]?.ToString() == _currentMediaId);
                    if (item != null)
                    {
                        _playbackList.MoveTo((uint)_playbackList.Items.IndexOf(item));
                        player.Play();
                    }
                }
                finally
                {
                    _listLock.Release();
                }

                if (item != null)
                {
                    // 找到了并已切换播放
                    return true;
                }

                // 没找到就继续翻页；AppendLoad 返回 false（取尽/网络失败）即停止，交由调用方决定是否另行播放
                bool hasMore = await AppendLoad();
                if (!hasMore)
                {
                    return false;
                }
            }
        }

        public async Task<AlbumInfo> LoadAlbumInfo(string albumId)
        {
            if (albumId == null) return null;
            string _uri = string.Format(ServiceURLs.AlbumInfo, albumId);

            return await _api.GetAlbumInfo(_uri);
        }

        public async void SkipNext()
        {
            if (!CanSkipNext)
                return;

            if (_playbackList == null)
                return;

            try
            {
                if (_playbackList.CurrentItemIndex + 3 > _playbackList.Items.Count)
                {
                    await AppendLoad();
                }
                _playbackList.MoveNext();
            }
            catch (Exception ex)
            {
                LogManager.LogAsync("skip next", ex);
            }
        }

        public void SkipPrevious()
        {
            if (!CanSkipPrevious)
                return;

            if (_playbackList == null)
                return;

            if (_playbackList.CurrentItemIndex == 0)
                return;

            _playbackList.MovePrevious();
        }

        private MediaListViewModel()
        {
            _playbackList = new MediaPlaybackList();
            PlaybackService.Instance.Player.Source = _playbackList;

            LoadLastestPlayItem();
        }

        private async void LoadLastestPlayItem()
        {
            try
            {
                LastestPlay item = await LastestPlay.LoadCurrentAsync();
                if (item != null)
                {
                    CanSkipNext = false;
                    CanSkipPrevious = false;
                    _currentMediaId = item.Id;
                    _currentPage = 1;
                    _mediaType = item.Type;
                    _albumInfo = await LoadAlbumInfo(item.AlbumId);
                }
            }
            catch (Exception ex)
            {
                LogManager.LogAsync("load lastest play item", ex);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public static readonly MediaListViewModel Instance = new MediaListViewModel();    
    }
}
