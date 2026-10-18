using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI.Xaml.Data;
using QingtingRadio.Https;
using QingtingRadio.DataModels;
using QingtingRadio.Tools;
using Windows.Storage;
using Windows.UI.Xaml.Media.Imaging;
using System.Diagnostics;

namespace QingtingRadio.ViewModels
{
    public class RadioListViewModel : ObservableCollection<RadioItem>, ISupportIncrementalLoading, IDisposable
    {
        APIService _api = new APIService();
        public event DataLoadingEventHandler DataLoading;
        public event DataLoadedEventHandler DataLoaded;
        private string _local_path = Windows.Storage.ApplicationData.Current.LocalFolder.Path;
        private string _current_radio_id = null;
        private bool _busy = false;
        private bool _has_more_items = false;
        private int _current_page = 1;
        private bool _disposed;
        public int TotalCount { get; set; }

        public enum _load_type{ _load_all, _load_page };
        public _load_type LoadType { get; set; }
        
        public string CurrentRadioId
        {
            get
            {
                return _current_radio_id;
            }
            set
            {
                _current_radio_id = value;
            }
        }

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

        public RadioListViewModel(_load_type LoadType, string RadioID = null)
        {
            this.LoadType = LoadType;
            CurrentRadioId = RadioID;
            HasMoreItems = true;
            CollectionChanged += RadioListViewModel_CollectionChanged;
        }

        public void Init(string RadioID = null)
        {
            this.Clear();
            TotalCount = 0;
            HasMoreItems = true;
            _current_page = 1;      
            if (!string.IsNullOrEmpty(RadioID))
            {
                CurrentRadioId = RadioID;
            }       
        }

        private async void RadioListViewModel_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            try
            {
                if (this.Count == 0) return;

                RadioItem tmp = this.ElementAt(this.Count - 1);
                if (string.IsNullOrEmpty(tmp.ImageUri))
                {
                    tmp.ImageUri = "ms-appx:///Assets/recommend_defaultbg.png";
                }
                else
                {
                    // 缓存键必须与 DownloadImage 的落盘名一致：去掉 CDN 的 "!n" 尺寸后缀，
                    // 否则带后缀的地址永远查不到缓存、每次进页面都重新下载
                    string[] sArray = tmp.ImageUri.Split('/');
                    string lastSegment = sArray[sArray.Length - 1];
                    string ImageId = lastSegment.Split('!')[0];

                    if (await FileHelper.Current.CacheExist(ImageId, FileHelper.CacheType.ImageCache))
                    {
                        tmp.ImageUri = _local_path + "\\ImagesCache\\" + ImageId;
                    }
                    else
                    {
                        // 已带尺寸后缀的不再拼接，避免出现 "...!200!200"
                        if (!lastSegment.Contains("!"))
                        {
                            tmp.ImageUri = tmp.ImageUri + "!200";
                        }
                        await _api.DownloadImage(tmp.ImageUri);
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.LogAsync("radio list image cache", ex);
            }
        }

        public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
        {
            return InnerLoadMoreItemsAsync(count).AsAsyncOperation();
        }
        private async Task<LoadMoreItemsResult> InnerLoadMoreItemsAsync(uint expectedCount)
        {
            _busy = true;
            var actualCount = 0;
            List<RadioItem> list = null;

            if (DataLoading != null) DataLoading();
            try
            {                
                string _uri;
                if(LoadType == _load_type._load_page)
                {                 
                    _uri = string.Format(ServiceURLs.CategoryRadios, _current_radio_id, _current_page);
                }
                else
                {
                    _uri = ServiceURLs.HotRadios;
                }
                list = await _api.GetRadiosList(_uri, _current_radio_id);
                HasMoreItems = false;
            }
            catch (Exception ex)
            {
                LogManager.LogAsync("load more radios", ex);
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
            if(LoadType == _load_type._load_all)
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

        public void Dispose()
        {
            if (_disposed) return;

            this.Clear();
            CollectionChanged -= RadioListViewModel_CollectionChanged;

            _disposed = true;
        }
    }

    public class ConvertAudienceCount : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null) return "听众：0000";

            double.TryParse(value.ToString(), out double count);

            if(count > 10000)
            {
                return "听众：" + Math.Round((count / 10000), 1, MidpointRounding.ToEven).ToString() + "万";
            }
            else
            {
                return "听众：" + value.ToString(); ;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public class ConvertNowPlaying : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null) return null;
            return "正在直播：" + value.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public class ConverPlayDuration : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null) return "直播时间：00:00-00:00";
            return "直播时间：" + value.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}

