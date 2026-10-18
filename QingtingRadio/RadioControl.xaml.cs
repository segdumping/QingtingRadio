using System;
using QingtingRadio.DataModels;
using QingtingRadio.Tools;
using QingtingRadio.ViewModels;
using System.ComponentModel;
using Windows.UI.Xaml.Controls;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Media;
using System.Collections.Generic;
using System.Windows.Input;

namespace QingtingRadio
{
    public sealed partial class RadioControl : UserControl
    {
        private RadioListViewModel _radio_lists;
        private RadioSettingsViewModel _settings = RadioSettingsViewModel.Instance;
        public RadioControl()
        {
            this.InitializeComponent();
            this.Loaded += RadioControl_Loaded;
            this.Unloaded += RadioControl_Unloaded;
        }

        private void RadioControl_Loaded(object sender, RoutedEventArgs e)
        {
            _settings_PropertyChanged(null, new PropertyChangedEventArgs("AppTheme"));
            _settings.PropertyChanged += _settings_PropertyChanged;
        }

        private void RadioControl_Unloaded(object sender, RoutedEventArgs e)
        {
            _settings.PropertyChanged -= _settings_PropertyChanged;
        }

        public RadioListViewModel ItemSource
        {
            set
            {
                RadiosListView.ItemsSource = value;
                _radio_lists = value;
            }
        }

        private void _settings_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName.Equals("AppTheme"))
            {
                if (_settings.AppTheme)
                {
                    width.Opacity = 0.5;
                }
                else
                {
                    width.Opacity = 1;
                }
            }
        }

        private async void RadiosListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is RadioItem tmp)
            {
                await RadioPlayViewModel.Instance.PlayRadioLive(tmp);
            }
        }

        private void Grid_SizeChanged(object sender, Windows.UI.Xaml.SizeChangedEventArgs e)
        {
            // 按列表实际宽度均分列数，余量分摊到每张卡片，行末不再留大块空白。
            // 先预留垂直滚动条约 24px，再扣除每张卡片的 Margin 8 + 边框 2 + Border 内边距 24
            double listWidth = e.NewSize.Width - 24;
            int column = (int)(listWidth / 330);
            if (column < 1) column = 1;
            width.Width = listWidth / column - 34;
        }

        private async void PullToRefreshBox_RefreshInvoked(Windows.UI.Xaml.DependencyObject sender, object args)
        {
            if (_radio_lists == null) return;

            _radio_lists.Init();
            await RadiosListView.LoadMoreItemsAsync();
        }

        private async void Favorite_Click(object sender, RoutedEventArgs e)
        {
            RadioItem item = ((FrameworkElement)e.OriginalSource).DataContext as RadioItem;
            if (item == null) return;

            await AppHelper.Current.SaveFavoriteRadio(item);
        }

        private void RadioList_PointerEntered(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (e.Pointer.PointerDeviceType != Windows.Devices.Input.PointerDeviceType.Mouse) return;
            Border card = sender as Border;
            if (card != null)
            {
                card.Background = (Brush)this.Resources["CardHoverBackground"];
                card.BorderBrush = (Brush)this.Resources["CardHoverBorderBrush"];
            }
            ((((card.Child as Grid).Children[1] as Grid).Children[0] as Grid).Children[1] as Button).Visibility = Visibility.Visible;
        }

        private void RadioList_PointerExited(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (e.Pointer.PointerDeviceType != Windows.Devices.Input.PointerDeviceType.Mouse) return;
            Border card = sender as Border;
            if (card != null)
            {
                card.Background = (Brush)this.Resources["CardNormalBackground"];
                card.BorderBrush = (Brush)this.Resources["CardNormalBorderBrush"];
            }
            ((((card.Child as Grid).Children[1] as Grid).Children[0] as Grid).Children[1] as Button).Visibility = Visibility.Collapsed;
        }
    }

    internal class FavoriteItemCommand : ICommand
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
            RadioItem item = parameter as RadioItem;
            if (item == null) return;

            await AppHelper.Current.SaveFavoriteRadio(item);
        }
    }
}
