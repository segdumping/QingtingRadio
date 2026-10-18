using QingtingRadio.DataModels;
using QingtingRadio.Https;
using QingtingRadio.Tools;
using QingtingRadio.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.RegularExpressions;
using System.Windows.Input;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Foundation.Metadata;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

namespace QingtingRadio
{
    public sealed partial class AlbumPage : Page
    {
        RadioAlbumViewModel _album = new RadioAlbumViewModel();
        public AlbumPage()
        {
            this.InitializeComponent();
            this.Loaded += AlbumPage_Loaded;
            this.Unloaded += AlbumPage_Unloaded;

            NavigationCacheMode = NavigationCacheMode.Required;
        }

        private void AlbumPage_Loaded(object sender, RoutedEventArgs e)
        {
            _album.DataLoaded += DataLoaded;
            _album.DataLoading += DataLoading;
        }

        private void AlbumPage_Unloaded(object sender, RoutedEventArgs e)
        {
            if (_album != null)
            {
                _album.DataLoaded -= DataLoaded;
                _album.DataLoading -= DataLoading;
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            object[] parameters = e.Parameter as object[];
            if (e.NavigationMode == NavigationMode.New)
            {
                string albumId = parameters[0].ToString();
                _album.Init(albumId, RadioAlbumViewModel.LoadType.page);
                AlbumList.ItemsSource = _album;
                SetAlbumInfo(albumId);
            }
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            base.OnNavigatingFrom(e);

            if (e.NavigationMode == NavigationMode.Back)
            {
                NavigationCacheMode = NavigationCacheMode.Disabled;
                AlbumList.ItemsSource = null;
            }
        }

        private async void SetAlbumInfo(string albumId)
        {                   
            try
            {
                AlbumInfo tmp = await _album.LoadAlbumInfo();
                if (tmp == null) return;
                UpdateTime.Text = string.IsNullOrEmpty(tmp.UpdateTime) ? string.Empty : "更新：" + tmp.UpdateTime.Substring(0,10);
                PlayCount.Text = string.IsNullOrEmpty(tmp.PlayCount) ? string.Empty : "播放：" + tmp.PlayCount;
                PragramCount.Text = string.IsNullOrEmpty(tmp.PragromCount) ? string.Empty : "节目：" + tmp.PragromCount;
                if (!tmp.SaleType.Equals("0"))
                {
                    AlbumDescription.Text = "本专辑为收费类型， 请移步蜻蜓FM官方网站 或 官方app收听。";
                }
                else
                {
                    AlbumDescription.Text = tmp.Description;
                }

                BitmapImage bmp = new BitmapImage()
                {
                    UriSource = new Uri(tmp.ImageUri),
                };

                AlbumThumb.Source = bmp;

                TitleName.Text = tmp.Title;
            }
            catch(Exception ex)
            {
                LogManager.LogAsync("set album info", ex);
            }
        }

        private async void AlbumList_ItemClick(object sender, ItemClickEventArgs e)
        {
            AlbumItem tmp = e.ClickedItem as AlbumItem;
            if (tmp == null) return;
            AlbumInfo info = await _album.LoadAlbumInfo();
            if (info == null) return;
            if(!info.SaleType.Equals("0"))
            {
                NotifyUser notifyPopup = new NotifyUser("本专辑为收费类型， 请移步蜻蜓FM官方网站 或 官方app收听");
                notifyPopup.Show();
            }
            else
            {
                tmp.ImageUri = info.ImageUri;
                tmp.AlbumName = info.Title;

                await RadioPlayViewModel.Instance.PlayAlbumItem(tmp, info.Id);              
            }          
        }

        private void Grid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // 预留滚动条 24px，再均分列数并扣除每张卡片的 Margin 8 + 边框 2 + 内边距 24
            double listWidth = e.NewSize.Width - 24;
            int column = (int)(listWidth / 330);
            if (column < 1) column = 1;
            width.Width = listWidth / column - 34;
        }

        private void DownloadRadioItem_Click(object sender, RoutedEventArgs e)
        {
            AlbumItem item = ((FrameworkElement)e.OriginalSource).DataContext as AlbumItem;
            DownloadProgramItem(item);
        }

        public async static void DownloadProgramItem(AlbumItem albumItem)
        {           
            if (albumItem == null) return;
            AlbumItem item = albumItem as AlbumItem;

            if (NetworkManager.Current.NetworkTitle.Contains("G") && !RadioSettingsViewModel.Instance.MoNetDownload)
            {
                NotifyUser notifyPopup = new NotifyUser("已禁用2G/3G/4G网络下载");
                notifyPopup.Show();
                return;
            }

            AlbumInfo info = await PlayInfoHelper.Current.GetAlbumInfo(item.AlbumId);
            if (item != null && info !=null)
            {
                DownloadItem tmp = new DownloadItem();
                string albumName = FileHelper.SanitizeFileName(info.Title);
                string title = FileHelper.SanitizeFileName(item.Title);

                string fileName = albumName + "_" + title + ".aac";
                bool isexist = await FileHelper.Current.CacheExist(fileName, FileHelper.CacheType.AudioCache);
                if (isexist)
                {
                    string message = "文件已存在，是否重新下载？";
                    bool result = await AppHelper.ShowMessageDialog(message, "是", "否");
                    if (!result) return;
                }

                string downloadUri = string.Format(ServiceURLs.PlayProgram, item.FilePath);
                tmp.FileName = fileName;
                tmp.AlbumName = albumName;
                tmp.Title = title;
                tmp.ImageUri = info.ImageUri;
                tmp.Progress = 0;
                tmp.IsSuccess = false;
                tmp.AudioId = item.Id;
                tmp.FileUri = downloadUri;
                tmp.AlubmId = info.Id;

                tmp.AudioTime = item.Duration;
                if (item.Duration != null)
                {
                    TimeSpan duration = TimeSpan.FromSeconds(double.Parse(item.Duration.ToString()));
                    tmp.AudioTime = "时长：" + (duration.Hours > 0 ? duration.ToString(@"hh\:mm\:ss") : duration.ToString(@"mm\:ss"));
                }

                tmp.Type = item.Type;

                await FileDownloadHelper.Current.StartDownload(tmp);
            }           
        }

        private void SearchResults_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            FrameworkElement senderElement = sender as FrameworkElement;
            FlyoutBase flyoutBase = FlyoutBase.GetAttachedFlyout(senderElement);

            flyoutBase.ShowAt(senderElement);
        }

        private void SearchResults_Holding(object sender, HoldingRoutedEventArgs e)
        {
            FrameworkElement senderElement = sender as FrameworkElement;
            FlyoutBase flyoutBase = FlyoutBase.GetAttachedFlyout(senderElement);

            flyoutBase.ShowAt(senderElement);
        }

        private async void PullToRefreshBox_RefreshInvoked(DependencyObject sender, object args)
        {
            _album.Init();
            await AlbumList.LoadMoreItemsAsync();
        }

        private async void FavoriteAlbumItem_Click(object sender, RoutedEventArgs e)
        {
            AlbumItem item = ((FrameworkElement)e.OriginalSource).DataContext as AlbumItem;
            if (item == null) return;
            AlbumInfo info = await _album.LoadAlbumInfo();
            if (info == null) return;
            item.ImageUri = info.ImageUri;
            item.AlbumName = info.Title;
            item.AlbumId = info.Id;

            await AppHelper.Current.SaveFavoriteProgram(item);
        }

        private void Grid_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (e.Pointer.PointerDeviceType != Windows.Devices.Input.PointerDeviceType.Mouse) return;
            (((sender as Grid).Children[0] as Grid).Children[1] as StackPanel).Visibility = Visibility.Visible;
        }

        private void Grid_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (e.Pointer.PointerDeviceType != Windows.Devices.Input.PointerDeviceType.Mouse) return;
            (((sender as Grid).Children[0] as Grid).Children[1] as StackPanel).Visibility = Visibility.Collapsed;
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
    internal class FavoriteAlbumItemCommand : ICommand
    {
        event EventHandler ICommand.CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object parameter)
        {
            return true;
        }

        public async void Execute(object parameter)
        {
            AlbumItem item = parameter as AlbumItem;
            if (item == null) return;
            AlbumInfo info = await PlayInfoHelper.Current.GetAlbumInfo(item.AlbumId);
            if (info == null) return;
            item.ImageUri = info.ImageUri;
            item.AlbumName = info.Title;
            item.AlbumId = info.Id;

            await AppHelper.Current.SaveFavoriteProgram(item);
        }
    }
    internal class DowanloadAlbumItemCommand : ICommand
    {
        event EventHandler ICommand.CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object parameter)
        {
            return true;
        }

        public void Execute(object parameter)
        {
            AlbumItem item = parameter as AlbumItem;
            if (item == null) return;

            AlbumPage.DownloadProgramItem(item);
            
        }
    }
}
