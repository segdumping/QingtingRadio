using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;
using Windows.Networking.BackgroundTransfer;
using Windows.Storage;
using QingtingRadio.DataModels;
using QingtingRadio.Tools;
using QingtingRadio.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Navigation;
using System.Windows.Input;

namespace QingtingRadio
{
    /// <summary>
    /// 回听收藏列表页。
    /// </summary>
    public sealed partial class FavoriteLists : Page
    {
        static ObservableCollection<RadioItem> _FavoriteRadios;
        static ObservableCollection<AlbumInfo> _FavoriteAlbums;
        static ObservableCollection<AlbumItem> _FavoritePrograms;
        static ObservableCollection<ReplayRadio> _FavoriteReplays;

        RadioSettingsViewModel _settings = RadioSettingsViewModel.Instance;
        RadioSingleProperty _singleProperty => RadioSingleProperty.Current;

        public FavoriteLists()
        {
            this.InitializeComponent();
            this.Loaded += FavoriteLists_Loaded;
            this.Unloaded += FavoriteLists_Unloaded;

            NavigationCacheMode = NavigationCacheMode.Required;
        }

        private void FavoriteLists_Unloaded(object sender, RoutedEventArgs e)
        {
            _settings.PropertyChanged -= _singleProperty_PropertyChanged;
            _singleProperty.PropertyChanged -= _singleProperty_PropertyChanged;
        }

        private void FavoriteLists_Loaded(object sender, RoutedEventArgs e)
        {
            _singleProperty_PropertyChanged(null, new PropertyChangedEventArgs("AppTheme"));
            _settings.PropertyChanged += _singleProperty_PropertyChanged;
            _singleProperty.PropertyChanged += _singleProperty_PropertyChanged;
        }

        private void _singleProperty_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName.Equals("AppTheme"))
            {
                if (_settings.AppTheme)
                {
                    border.Opacity = 0.5;
                }
                else
                {
                    border.Opacity = 1;
                }
            }
            
        }

        private async void LoadFavoriteRadios()
        {
            try
            {
                List<RadioItem> tmpItem = await FileHelper.Current.ReadObjectAsync<List<RadioItem>>("FavoriteRadios.json") ?? new List<RadioItem>();
                if (_FavoriteRadios == null)
                {
                    _FavoriteRadios = new ObservableCollection<RadioItem>();
                }

                // 每次全量刷新：只比较数量会在"删一条再加一条"时漏掉内容变化
                _FavoriteRadios.Clear();
                tmpItem.ForEach(c => _FavoriteRadios.Add(c));
                if (RadiosView.ItemsSource != _FavoriteRadios)
                {
                    RadiosView.ItemsSource = _FavoriteRadios;
                }
            }
            catch (Exception ex)
            {
                LogManager.LogAsync("load favorite radios", ex);
            }
        }

        private async void LoadFavoriteAlbums()
        {
            try
            {
                List<AlbumInfo> tmpItem = await FileHelper.Current.ReadObjectAsync<List<AlbumInfo>>("FavoriteAlbums.json") ?? new List<AlbumInfo>();
                if (_FavoriteAlbums == null)
                {
                    _FavoriteAlbums = new ObservableCollection<AlbumInfo>();
                }

                _FavoriteAlbums.Clear();
                tmpItem.ForEach(c => _FavoriteAlbums.Add(c));
                if (AlbumsView.ItemsSource != _FavoriteAlbums)
                {
                    AlbumsView.ItemsSource = _FavoriteAlbums;
                }
            }
            catch (Exception ex)
            {
                LogManager.LogAsync("load favorite albums", ex);
            }
        }

        private async void LoadFavoritePragroms()
        {
            try
            {
                List<AlbumItem> tmpItem = await FileHelper.Current.ReadObjectAsync<List<AlbumItem>>("FavoritePrograms.json") ?? new List<AlbumItem>();
                if (_FavoritePrograms == null)
                {
                    _FavoritePrograms = new ObservableCollection<AlbumItem>();
                }

                _FavoritePrograms.Clear();
                tmpItem.ForEach(c => _FavoritePrograms.Add(c));
                if (ProgramsView.ItemsSource != _FavoritePrograms)
                {
                    ProgramsView.ItemsSource = _FavoritePrograms;
                }
            }
            catch (Exception ex)
            {
                LogManager.LogAsync("load favorite programs", ex);
            }
        }

        private async void RadiosView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is RadioItem tmp)
            {
                await RadioPlayViewModel.Instance.PlayRadioLive(tmp);
            }
        }

        private void AlbumsView_ItemClick(object sender, ItemClickEventArgs e)
        {
            AlbumInfo tmp = e.ClickedItem as AlbumInfo;
            if (tmp == null) return;

            this.Frame.Navigate(typeof(AlbumPage), new object[] { tmp.Id });
        }

        private async void ProgramsView_ItemClick(object sender, ItemClickEventArgs e)
        {
            AlbumItem tmp = e.ClickedItem as AlbumItem;
            if (tmp == null) return;
            AlbumInfo info = await PlayInfoHelper.Current.GetAlbumInfo(tmp.AlbumId);
            if (info != null)
            {
                if (!info.SaleType.Equals("0"))
                {
                    NotifyUser notifyPopup = new NotifyUser("本专辑为收费类型， 请移步蜻蜓FM官方网站 或 官方app收听");
                    notifyPopup.Show();
                    return;
                }
            }

            string albumId = info?.Id;
            await RadioPlayViewModel.Instance.PlayAlbumItem(tmp, albumId);
        }

        public static async void DeleteFavorite(MusicItem item)
        {
            if (item == null) return;
            try
            {
                if (item is RadioItem)
                {
                    if (_FavoriteRadios == null) return;
                    int index = _FavoriteRadios.ToList().FindIndex(v => v.Id == item.Id);
                    if (index < 0) return;
                    _FavoriteRadios.RemoveAt(index);
                    await FileHelper.Current.WriteObjectAsync<List<RadioItem>>(_FavoriteRadios.ToList<RadioItem>(), "FavoriteRadios.json");
                }
                else if (item is AlbumInfo)
                {
                    if (_FavoriteAlbums == null) return;
                    int index = _FavoriteAlbums.ToList().FindIndex(v => v.Id == item.Id);
                    if (index < 0) return;
                    _FavoriteAlbums.RemoveAt(index);
                    await FileHelper.Current.WriteObjectAsync<List<AlbumInfo>>(_FavoriteAlbums.ToList<AlbumInfo>(), "FavoriteAlbums.json");
                }
                else if (item is ReplayRadio replay)
                {
                    if (_FavoriteReplays == null) return;
                    int index = _FavoriteReplays.ToList().FindIndex(v => v.Id == replay.Id && v.DateTime == replay.DateTime);
                    if (index < 0) return;
                    _FavoriteReplays.RemoveAt(index);
                    await ReplayRadio.SaveFavoritesAsync(_FavoriteReplays.ToList<ReplayRadio>());
                }
                else
                {
                    if (_FavoritePrograms == null) return;
                    int index = _FavoritePrograms.ToList().FindIndex(v => v.Id == item.Id);
                    if (index < 0) return;
                    _FavoritePrograms.RemoveAt(index);
                    await FileHelper.Current.WriteObjectAsync<List<AlbumItem>>(_FavoritePrograms.ToList<AlbumItem>(), "FavoritePrograms.json");
                }
            }
            catch (Exception ex)
            {
                LogManager.LogAsync("delete favorite", ex);
            }
        }

        private void Radios_Loaded(object sender, RoutedEventArgs e)
        {
            RingCircle.IsActive = true;
            LoadFavoriteRadios();
            RingCircle.IsActive = false;
        }

        private void Albums_Loaded(object sender, RoutedEventArgs e)
        {
            RingCircle.IsActive = true;
            LoadFavoriteAlbums();
            RingCircle.IsActive = false;
        }

        private void Programs_Loaded(object sender, RoutedEventArgs e)
        {
            RingCircle.IsActive = true;
            LoadFavoritePragroms();
            RingCircle.IsActive = false;
        }

        private async void Replays_Loaded(object sender, RoutedEventArgs e)
        {
            RingCircle.IsActive = true;
            await LoadFavoriteReplays();
            RingCircle.IsActive = false;
        }

        private async Task LoadFavoriteReplays()
        {
            try
            {
                List<ReplayRadio> tmpItem = await ReplayRadio.LoadFavoritesAsync() ?? new List<ReplayRadio>();
                if (_FavoriteReplays == null)
                {
                    _FavoriteReplays = new ObservableCollection<ReplayRadio>();
                }

                _FavoriteReplays.Clear();
                tmpItem.ForEach(c => _FavoriteReplays.Add(c));
                if (ReplaysView.ItemsSource != _FavoriteReplays)
                {
                    ReplaysView.ItemsSource = _FavoriteReplays;
                }
            }
            catch (Exception ex)
            {
                LogManager.LogAsync("load favorite replays", ex);
            }
        }

        private async void ReplaysView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is ReplayRadio tmp)
            {
                if (string.IsNullOrEmpty(tmp.DateTime) || tmp.DateTime.Length < 10) return;
                string dateYmd = tmp.DateTime.Substring(0, 10).Replace("-", "");
                await ReplayPage.PlayReplayAsync(tmp, dateYmd);
            }
        }

        private void Grid_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (e.Pointer.PointerDeviceType != Windows.Devices.Input.PointerDeviceType.Mouse) return;
            (((sender as Grid).Children[1] as Grid).Children[1] as Button).Visibility = Visibility.Visible;
        }

        private void Grid_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (e.Pointer.PointerDeviceType != Windows.Devices.Input.PointerDeviceType.Mouse) return;
            (((sender as Grid).Children[1] as Grid).Children[1] as Button).Visibility = Visibility.Collapsed;
        }

        private void DeleteFavorite_Click(object sender, RoutedEventArgs e)
        {
            DeleteFavorite(((FrameworkElement)e.OriginalSource).DataContext as MusicItem);
        }
    }

    /// <summary>
    /// 命令：删除收藏条目。
    /// </summary>
    internal class DeleteFavoriteCommand : ICommand
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
            FavoriteLists.DeleteFavorite(parameter as MusicItem);
        }
    }
}