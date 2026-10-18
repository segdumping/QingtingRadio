using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using QingtingRadio.Https;
using QingtingRadio.DataModels;
using System.Diagnostics;
using QingtingRadio.Tools;

namespace QingtingRadio.ViewModels
{
    public class RadioCategoryViewModel : ObservableCollection<Categories>
    {
        APIService _api = new APIService();
        string _request_uri;
        public event DataLoadingEventHandler DataLoading;
        public event DataLoadedEventHandler DataLoaded;

        private async void LoadRadioCategories()
        {
            if (DataLoading != null) DataLoading();
            List<Categories> list = null;
            try
            {
                list = await _api.GetRadioCategories(_request_uri);
            }
            catch(Exception ex)
            {
                Debug.WriteLine(ex.ToString());
                LogManager.LogAsync("load radio category", ex);
            }

            if (list != null && list.Any())
            {
                list.ForEach((c) => { this.Add(c); });
                if(_request_uri.Equals(ServiceURLs.ChannelRadios))
                {
                    Categories tmp = new Categories()
                    {
                        ID = "-3",
                        Title = "自定义",
                    };                   
                    this.Insert(0, tmp);
                }
            }
            if (DataLoaded != null) DataLoaded();
        }

        public void DoRefresh()
        {
            this.Clear();
            LoadRadioCategories();
        }

        public RadioCategoryViewModel(string Uri)
        {
            _request_uri = Uri;
            LoadRadioCategories();
        }
    }

}
