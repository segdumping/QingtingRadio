using System;
using System.Collections.Generic;
using System.Linq;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using QingtingRadio.ViewModels;
using QingtingRadio.DataModels;
using QingtingRadio.Https;
using Windows.UI.Core;
using QingtingRadio.Tools;
using System.ComponentModel;
using System.Threading.Tasks;
using Windows.System.Profile;
using Windows.UI.Popups;
using Windows.Foundation.Metadata;
using Microsoft.Toolkit.Uwp.UI.Extensions;
using Windows.UI;
using Windows.UI.Xaml.Media;
using Windows.UI.ViewManagement;

namespace QingtingRadio
{
    public sealed partial class HomePage : Page
    {
        private RadioListViewModel _list_ratios;
        private RadioListViewModel _local_radios;
        private RadioListViewModel _country_radios;
        private RadioCategoryViewModel _region_radios;
        private RadioListViewModel _channel_radios;
        private RadioCategoryViewModel _category_radios;

        RadioSettingsViewModel _settings = RadioSettingsViewModel.Instance;

        /// <summary>
        /// 央广九套直达电台：国家台页签顶部的名字按钮，点击直接播放对应卫星直播流。
        /// </summary>
        public List<Categories> CnrStations { get; } = Https.ServiceURLs.CnrStations
            .Select(s => new Categories { Title = s.Title, ID = s.Url })
            .ToList();

        public HomePage()
        {
            this.InitializeComponent();
            this.Loaded += HomePage_Loaded; ;
            this.Unloaded += HomePage_Unloaded;

            NavigationCacheMode = NavigationCacheMode.Required;
            IntitDataSource();
        }

        private void HomePage_Loaded(object sender, RoutedEventArgs e)
        {
            LocalPivot.Header = _settings.UserRegionName;
            if (!RadioSettingsViewModel.Instance.HasCustomRegion)
            {
                CheckUserLocation();
            }

            ShowToast();

            _list_ratios.DataLoaded += DataLoaded;
            _list_ratios.DataLoading += DataLoading;
            _region_radios.DataLoaded += DataLoaded;
            _region_radios.DataLoading += DataLoading;
        }

        private void HomePage_Unloaded(object sender, RoutedEventArgs e)
        {
            if (_list_ratios != null)
            {
                _list_ratios.DataLoaded -= DataLoaded;
                _list_ratios.DataLoading -= DataLoading;
            }
            if (_region_radios != null)
            {
                _region_radios.DataLoaded -= DataLoaded;
                _region_radios.DataLoading -= DataLoading;
            }
        }

        private async void CnrStation_Click(object sender, RoutedEventArgs e)
        {
            Button station = sender as Button;
            if (station == null || station.Tag == null) return;

            // 直连流复用自定义电台的播放路径:PlayRadioLive 的 isCustom 分支从 PlayCount 取播放地址
            RadioItem tmp = new RadioItem
            {
                Id = station.Tag.ToString(),
                Title = station.Content.ToString(),
                ImageUri = "ms-appx:///Assets/recommend_defaultbg.png",
                NowPlaying = "直播中",
                PlayCount = station.Tag.ToString(),
            };

            await RadioPlayViewModel.Instance.PlayRadioLive(tmp, true);
        }

        private async void CheckUserLocation()
        {
            GetUserLocation UserLocation = new GetUserLocation();
            LocalRadio tmp = await UserLocation.GetUserLocationByIp();
            if (tmp != null && tmp.RadioID != RadioSettingsViewModel.Instance.UserRegionID)
            {
                LocalPivot.Header = tmp.RegionName + "台";
                RadioSettingsViewModel.Instance.UserRegionID = tmp.RadioID;
                RadioSettingsViewModel.Instance.UserRegionName = tmp.RegionName + "台";

                LocalLists.ItemSource = null;
                _local_radios.Init(_settings.UserRegionID);
                LocalLists.ItemSource = _local_radios;
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if(_local_radios.CurrentRadioId != _settings.UserRegionID)
            {
                LocalLists.ItemSource = null;
                _local_radios.Init(_settings.UserRegionID);
                LocalLists.ItemSource = _local_radios;
            }
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            base.OnNavigatingFrom(e);

            if (e.NavigationMode == NavigationMode.Back)
            {
                NavigationCacheMode = NavigationCacheMode.Disabled;
                _list_ratios.Dispose();
                _list_ratios = null;

                _local_radios.Dispose();
                _local_radios = null;

                _country_radios.Dispose();
                _country_radios = null;

                _channel_radios.Dispose();
                _channel_radios = null;

                _region_radios = null;
                _category_radios = null;
            }
        }

        private async void CustomListonRadio()
        {
            ContentDialog content_dialog = new ContentDialog()
            {
                Title = "自定义电台",
                Content = new CustomRadios(),
                SecondaryButtonText = "退出",
                IsPrimaryButtonEnabled = false,
                FullSizeDesired = false,
            };

            content_dialog.SecondaryButtonClick += (_s, _e) =>
            {
            };

            content_dialog.MaxHeight = Windows.UI.ViewManagement.ApplicationView.GetForCurrentView().VisibleBounds.Height - 56;

            await content_dialog.ShowAsync();
        }

        private async void ShowToast()
        {
            try
            {
                if (VersionHelper.Compare(_settings.CurrentVersion, _settings.ToastedKey) <= 0)
                {
                    return;
                }

                string toast = await VersionUpdateHelper.Current.VersionLog(false);
                if (string.IsNullOrEmpty(toast)) return;

                TextBlock text = new TextBlock()
                {
                    Text = toast,
                    TextWrapping = TextWrapping.Wrap,
                    LineHeight = 24,
                    FontSize = 15,
                    Margin = new Thickness(12, 8, 0, 4),
                };
            
                ContentDialog content_dialog = new ContentDialog()
                {
                    Title = "版本更新提示",
                    Content = text,
                    SecondaryButtonText = "确认",
                    IsPrimaryButtonEnabled = false,
                    FullSizeDesired = true,
                };

                content_dialog.Style = (Style)Application.Current.Resources["CustomContent"];
                content_dialog.RequestedTheme = RadioSettingsViewModel.Instance.AppTheme ? ElementTheme.Dark : ElementTheme.Light;

                await content_dialog.ShowAsync();
            }
            catch
            { }
        }

        private void Search_Click(object sender, RoutedEventArgs e)
        {
            MainPage.Current.ContentFrame.Navigate(typeof(SearchPage));
        }

        private void Pivot_PivotItemUnloaded(Pivot sender, PivotItemEventArgs args)
        {
            // 原实现在这里强制 GC.Collect：会阻塞 UI 线程、制造切换卡顿，且掩盖真正的引用泄漏。
            // 内存交给 GC 自行回收，如需排查泄漏请检查事件订阅是否成对退订。
        }

        private void IntitDataSource()
        {
            _list_ratios = new RadioListViewModel(RadioListViewModel._load_type._load_all);
            LocalLists.ItemSource = _local_radios = new RadioListViewModel(RadioListViewModel._load_type._load_page, _settings.UserRegionID);
            _country_radios = new RadioListViewModel(RadioListViewModel._load_type._load_page, "409");
            _channel_radios = new RadioListViewModel(RadioListViewModel._load_type._load_page, "407");
            _region_radios = new RadioCategoryViewModel(ServiceURLs.RegionRadios);
            _category_radios = new RadioCategoryViewModel(ServiceURLs.ChannelRadios);
        }

        private void DataLoading()
        {
            RingCircle.IsActive = true;
        }

        private void DataLoaded()
        {
            RingCircle.IsActive = false;
        }
    }
}
