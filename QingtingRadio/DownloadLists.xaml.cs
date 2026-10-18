using QingtingRadio.DataModels;
using QingtingRadio.Tools;
using QingtingRadio.ViewModels;
using System;
using System.Windows.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace QingtingRadio
{
    public sealed partial class DownloadLists : Page
    {
        public DownloadLists()
        {
            this.InitializeComponent();
            this.Loaded += DownloadLists_Loaded;
            this.Unloaded += DownloadLists_Unloaded;

            NavigationCacheMode = NavigationCacheMode.Required;
        }

        private void DownloadLists_Loaded(object sender, RoutedEventArgs e)
        {
            FileDownloadHelper.Current.DataLoaded += DataLoaded;
            FileDownloadHelper.Current.DataLoading += DataLoading;
        }

        private void DownloadLists_Unloaded(object sender, RoutedEventArgs e)
        {
            FileDownloadHelper.Current.DataLoaded -= DataLoaded;
            FileDownloadHelper.Current.DataLoading -= DataLoading;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.NavigationMode == NavigationMode.New)
            {
                DowanloadListsView.ItemsSource = FileDownloadHelper.Current.DownloadFiles;
            }
        }

        private async void DowanloadListsView_ItemClick(object sender, ItemClickEventArgs e)
        {
            DownloadItem file = (e.ClickedItem as DownloadItem);
            LastestPlay item = new LastestPlay();

            if (file == null) return;
            if(file.IsSuccess)
            {
                item.AlbumId = file.AlubmId;
                item.Type = file.Type;
                
                item.ImageUri = file.ImageUri;
                // 扫描缓存目录补出来的条目没有 AudioId，留空会导致历史去重、SMTC 元数据都不正确，
                // 这里退回用文件名兜底，保证 Id 始终可用。
                item.Id = string.IsNullOrEmpty(file.AudioId) ? file.FileName : file.AudioId;
                item.Title = file.AlbumName;
                item.Playing = file.Title;
                item.Date = file.AudioTime;
                item.PlayUri = file.FileUri;
                item.FileName = file.FileName;

                try
                {
                    //item.Date = file.d
                    //item.StartTime = tmp.ElementAt(3).ToString();
                    //item.EndTime = tmp.ElementAt(4).ToString();
                }
                catch(Exception ex)
                {
                    LogManager.LogAsync("downloadlists view item click", ex);
                }
                              
                await RadioPlayViewModel.Instance.PlayDownloadItem(item);                         
            }
            else
            {
                await FileDownloadHelper.Current.CheckDownloadState(file.ID);
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

        private async void DeleteDowanloadFile_Click(object sender, RoutedEventArgs e)
        {
            if(((FrameworkElement)e.OriginalSource).DataContext is DownloadItem item)
            {
                await FileDownloadHelper.Current.DeleteDownloadFile(item);
            }          
        }

        private void DowanloadListsView_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            FrameworkElement senderElement = sender as FrameworkElement;
            FlyoutBase flyoutBase = FlyoutBase.GetAttachedFlyout(senderElement);

            flyoutBase.ShowAt(senderElement);
        }

        private void DowanloadListsView_Holding(object sender, HoldingRoutedEventArgs e)
        {
            FrameworkElement senderElement = sender as FrameworkElement;
            FlyoutBase flyoutBase = FlyoutBase.GetAttachedFlyout(senderElement);

            flyoutBase.ShowAt(senderElement);
        }

        private void Grid_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (e.Pointer.PointerDeviceType != Windows.Devices.Input.PointerDeviceType.Mouse) return;
            ((((sender as Grid).Children[1] as Grid).Children[0] as Grid).Children[1] as Button).Visibility = Visibility.Visible;
        }

        private void Grid_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (e.Pointer.PointerDeviceType != Windows.Devices.Input.PointerDeviceType.Mouse) return;
            ((((sender as Grid).Children[1] as Grid).Children[0] as Grid).Children[1] as Button).Visibility = Visibility.Collapsed;
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

    public class DownloadSizeValueConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            double fileSize = System.Convert.ToDouble(value);
            if (fileSize < 0 && fileSize > -2)
            {
                return "暂停";
            }
            else if(fileSize < -2)
            {
                return "失败";
            }
            else
            {
                return Math.Round(fileSize / 1024 / 1024, 2).ToString() + "M";
            }
            
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    internal class DeleteDownloadItemCommand : ICommand
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
            if (parameter is DownloadItem item)
            {
                await FileDownloadHelper.Current.DeleteDownloadFile(item);
            }
        }
    }
}
