using QingtingRadio.DataModels;
using QingtingRadio.Https;
using QingtingRadio.Tools;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using Windows.UI.Xaml.Data;

namespace QingtingRadio.ViewModels
{
    public class RadioReplayViewModel : ObservableCollection<ReplayRadio>
    {
        APIService _api = new APIService();
        public event DataLoadingEventHandler DataLoading;
        public event DataLoadedEventHandler DataLoaded;

        public async void LoadReplayInfo(int ValueType)
        {
            List<ReplayRadio> list = null;
            if (DataLoading != null) DataLoading();
            try
            {
                this.Clear();

                var currentDay = PlayInfoHelper.Current.GetCurrentDay(ValueType);
                //    LastestPlay item = await FileHelper.Current.ReadObjectAsync<LastestPlay>("LastestPlayRadio.json");

                string itemId = MediaListViewModel.Instance.CurrentMediaId;
                if (!string.IsNullOrEmpty(itemId))
                {
                    string Uri = string.Format(ServiceURLs.ReplayList, itemId, currentDay.Item1);
                    // 直接使用 AddDays 计算的目标日期，避免跨月/跨年时用当前年月拼出错误日期
                    DateTime dt = PlayInfoHelper.Current.GetTargetDate(ValueType);
                    list = await _api.GetReplayList(Uri, dt.ToString("yyyy-MM-dd"));
                }             
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
            }

            if (list != null && list.Any())
            {
                list.ForEach((c) => { this.Add(c); });
            }
            if (DataLoaded != null) DataLoaded();
        }
    }

    public class CheckReplayItemState : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null) return null;

            // 只显示时间段（HH:mm-HH:mm），不再带"回听：/直播："前缀
            string[] datetime = value.ToString().Split(' ');
            if (datetime.Length < 2) return value.ToString();
            return datetime.ElementAt(1);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
