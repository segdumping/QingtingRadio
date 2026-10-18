using QingtingRadio.DataModels;
using QingtingRadio.Https;
using QingtingRadio.Tools;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI.Xaml.Data;

namespace QingtingRadio.ViewModels
{
    public class RadioAlbumViewModel : ObservableCollection<AlbumItem>, ISupportIncrementalLoading
    {
        APIService _api = new APIService();
        public event DataLoadingEventHandler DataLoading;
        public event DataLoadedEventHandler DataLoaded;
        private string _album_id = null;
        private AlbumInfo _album_info = null;
        private bool _busy = false;
        private bool _has_more_items = false;
        private int _current_page = 1;
        public int TotalCount { get; set; }      

        public enum LoadType { page, control};
        private LoadType _loadType;

        public bool HasMoreItems
        {
            get
            {
                if (_busy)
                    return false;
                else
                    return _has_more_items;
            }
            private set
            {
                _has_more_items = value;
            }
        }

        public void Init()
        {
            this.Clear();
            TotalCount = 0;
            _current_page = 1;
            _has_more_items = true;
        }

        public void Init(string albumId, LoadType type)
        {
            if(_album_id == albumId)
            {
                return;
            }

            this.Clear();
            _album_id = albumId;
            HasMoreItems = true;
            _current_page = 1;
            _album_info = null;
            _loadType = type;
        }

        public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
        {
            return InnerLoadMoreItemsAsync(count).AsAsyncOperation();
        }

        private async Task<LoadMoreItemsResult> InnerLoadMoreItemsAsync(uint expectedCount)
        {
            _busy = true;
            if (DataLoading != null) DataLoading();
            
            var actualCount = 0;
            List<AlbumItem> list = null;
           
            try
            {
                if (_album_info == null)
                {
                    _album_info = await LoadAlbumInfo();
                }
                string _uri = string.Format(ServiceURLs.AlbumList, _album_id, _album_info.Version, _current_page, 30);
                list = await _api.GetAlbumList(_uri, _album_id);
                HasMoreItems = false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
                HasMoreItems = false;
            }

            if (list != null && list.Any())
            {
                actualCount = list.Count;
                TotalCount += actualCount;
                _current_page++;
                HasMoreItems = true;
                list.ForEach((c) => { this.Add(c); });

            }
            else
            {
                HasMoreItems = false;
            }
            _busy = false;
            if (DataLoaded != null) DataLoaded();
            return new LoadMoreItemsResult
            {
                Count = (uint)actualCount
            };
        }

        public async Task<AlbumInfo> LoadAlbumInfo()
        {
            if (_album_info == null)
            {
                if (_album_id == null) return null;
                string _uri = string.Format(ServiceURLs.AlbumInfo, _album_id);
                _album_info = await _api.GetAlbumInfo(_uri);
            }

            return _album_info;        
        }
    }

    public class ConvertPlayDuration : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null) return value;

            TimeSpan tmp = TimeSpan.FromSeconds(double.Parse(value.ToString()));           

            return "时长：" + (tmp.Hours >0 ? tmp.ToString(@"hh\:mm\:ss") : tmp.ToString(@"mm\:ss"));
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public class ConvertAlbumUpdateTime : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null) return value;

            return value.ToString().Substring(0,10);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public class ConvertPlayCount : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null) return value;

            return "播放：" + value.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
