using Windows.UI.Xaml.Controls;
using QingtingRadio.ViewModels;
using Windows.UI.Core;
using Windows.UI.Xaml.Navigation;
using System;
using QingtingRadio.DataModels;
using Windows.System.Profile;
using QingtingRadio.Tools;
using Windows.Foundation.Metadata;

namespace QingtingRadio
{
    public sealed partial class RadioList : Page
    {
        private RadioListViewModel _list_ratios;
        public RadioList()
        {
            this.InitializeComponent();

            NavigationCacheMode = NavigationCacheMode.Required;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            object[] parameters = e.Parameter as object[];
            if (parameters == null || parameters.Length < 2)
            {
                // 导航参数不完整时直接返回，避免索引越界崩溃
                if (this.Frame.CanGoBack) this.Frame.GoBack();
                return;
            }

            if(e.NavigationMode == NavigationMode.New)
            {
                AppHelper.Current.CheckNetwork("当前无网络链接，请开启网络后收听！");
                RadioLists.ItemSource = _list_ratios = new RadioListViewModel(RadioListViewModel._load_type._load_page, parameters[0].ToString());
                _list_ratios.DataLoading += DataLoading;
                _list_ratios.DataLoaded += DataLoaded;
            }

            TitleName.Text = parameters[1].ToString();
            NavigationCacheMode = NavigationCacheMode.Enabled;
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            base.OnNavigatingFrom(e);

            if (e.NavigationMode == NavigationMode.Back)
            {
                NavigationCacheMode = NavigationCacheMode.Disabled;
                _list_ratios.DataLoading -= DataLoading;
                _list_ratios.DataLoaded -= DataLoaded;
                RadioLists.ItemSource = null;
                _list_ratios.Dispose();
                _list_ratios = null;
            }
        }

        private void Search_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            MainPage.Current.ContentFrame.Navigate(typeof(SearchPage));
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
