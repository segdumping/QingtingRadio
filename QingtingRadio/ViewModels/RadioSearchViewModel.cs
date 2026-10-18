using QingtingRadio.DataModels;
using QingtingRadio.Https;
using QingtingRadio.Tools;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI.Xaml.Data;

namespace QingtingRadio.ViewModels
{
    class RadioSearchViewModel : ObservableCollection<MusicItem>, ISupportIncrementalLoading, IDisposable
    {
        APIService _api = new APIService();
        public event DataLoadingEventHandler DataLoading;
        public event DataLoadedEventHandler DataLoaded;
        private string _local_path = Windows.Storage.ApplicationData.Current.LocalFolder.Path;
        private string _key_word;
        private string _type;
        private bool _disposed;
        private bool _busy = false;
        private bool _has_more_items = false;
        private int _current_page = 1;
        public int TotalCount { get; set; }

        public enum _search_type { remix, channel_live, program_ondemand, channel_ondemand };

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

        public void NewSearch(int typeIndex, string keyWords)
        {
            this.Init();
            if (typeIndex == 0) {
                _type = "channel_ondemand,channel_live,program";
            } else
            {
                _type = ((_search_type)Enum.ToObject(typeof(_search_type), typeIndex)).ToString();
            }
           
            _key_word = keyWords;
        }

        public RadioSearchViewModel()
        {
            Init();
        }

        private void RadioSearchViewModel_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (this.Count == 0) return;

            MusicItem tmp = this.ElementAt(this.Count - 1);
            if (tmp.Type == MusicItem.AudioType.Program) return;
            if (string.IsNullOrEmpty(tmp.ImageUri))
            {
                tmp.ImageUri = "ms-appx:///Assets/recommend_defaultbg.png";
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
            List<MusicItem> list = null;

            if (DataLoading != null) DataLoading();
            try
            {
                string uri = string.Format(ServiceURLs.NewSearch, _key_word, _current_page, _type);
                list = await _api.GetSearchResult(Uri.EscapeUriString(uri));
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

        public void Dispose()
        {
            if (_disposed) return;

            this.Clear();
            CollectionChanged -= RadioSearchViewModel_CollectionChanged;

            _disposed = true;
        }
    }
}
