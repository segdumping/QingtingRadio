using QingtingRadio.DataModels;
using QingtingRadio.Https;
using QingtingRadio.Tools;
using QingtingRadio.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.RegularExpressions;
using System.Windows.Input;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Foundation.Metadata;
using Windows.System.Profile;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace QingtingRadio
{
    public sealed partial class SearchPage : Page
    {
        RadioSettingsViewModel _settings = RadioSettingsViewModel.Instance;
        private ObservableCollection<string> _searchHistory;
        private RadioSearchViewModel _searchHandle;
        public SearchPage()
        {
            this.InitializeComponent();
            this.Loaded += SearchPage_Loaded;
            this.Unloaded += SearchPage_Unloaded;

            _searchHistory = new ObservableCollection<string>();
            _searchHandle = new RadioSearchViewModel();
            SearchResults.ItemsSource = _searchHandle;
            NavigationCacheMode = NavigationCacheMode.Required;
        }

        private void SearchPage_Unloaded(object sender, RoutedEventArgs e)
        {
            if (_searchHandle != null)
            {
                _searchHandle.DataLoaded -= DataLoaded;
                _searchHandle.DataLoading -= DataLoading;
            }
            if (_settings != null)
            {
                _settings.PropertyChanged -= _settings_PropertyChanged;
            }
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            base.OnNavigatingFrom(e);

            if (e.NavigationMode == NavigationMode.Back)
            {
                NavigationCacheMode = NavigationCacheMode.Disabled;
                SearchResults.ItemsSource = null;
                HistoryWords.ItemsSource = null;
                _searchHandle.Dispose();
                _searchHandle = null;
            }
        }

        private void SearchPage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadSearchKeyWords();
            _settings_PropertyChanged(null, new PropertyChangedEventArgs("AppTheme"));
            _settings.PropertyChanged += _settings_PropertyChanged;
            _searchHandle.DataLoaded += DataLoaded;
            _searchHandle.DataLoading += DataLoading;
        }

        private void _settings_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName.Equals("AppTheme"))
            {
                if(_settings.AppTheme)
                {
                    width.Opacity = 0.5;
                }
                else
                {
                    width.Opacity = 1;
                }
            }
        }

        private async void LoadSearchKeyWords()
        {            
            List<string> words = await FileHelper.Current.ReadObjectAsync<List<string>>("SearchHistory.json");
            if (words == null)
            {
                words = new List<string>();
            }

            _searchHistory.Clear();
            if (words.Count == 0)
            {
                APIService _api = new APIService();
                List<string> tmp = await _api.GetHotKeyWords();
                if (tmp != null)
                {
                    tmp.ForEach(c => _searchHistory.Add(c));
                }
            }
            else
            {
                words.ForEach(c => _searchHistory.Add(c));
            }

            HistoryWords.ItemsSource = _searchHistory;

            await FileHelper.Current.WriteObjectAsync<List<string>>(_searchHistory.ToList<string>(), "SearchHistory.json");
        }

        private async void SearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            if (string.IsNullOrEmpty(SearchBox.Text)) return;
            SearchHistory.Visibility = Visibility.Collapsed;
            SearchResults.Visibility = Visibility.Visible;
            int index = SearchType.SelectedIndex < 0 ? 0 : SearchType.SelectedIndex;

            AddWordToSearchHistory(SearchBox.Text);
            _searchHandle.NewSearch(index, SearchBox.Text);
            await SearchResults.LoadMoreItemsAsync();           
        }

        private async void SearchResults_ItemClick(object sender, ItemClickEventArgs e)
        {
            MusicItem tmp = e.ClickedItem as MusicItem;
            if (tmp == null) return;

            if (tmp is SearchItemRadio)
            {
                RadioItem item = new RadioItem()
                {
                    Id = tmp.Id,
                    Title = tmp.Title,
                    ImageUri = tmp.ImageUri,
                    PlayCount = tmp.PlayCount
                };

                await RadioPlayViewModel.Instance.PlayRadioLive(item);
            }
            else if (tmp is SearchItemProgram)
            {
                SearchItemProgram program = tmp as SearchItemProgram;
                this.Frame.Navigate(typeof(AlbumPage), new object[] { program.AlbumId });

                await RadioPlayViewModel.Instance.PlaySearchItem(program);
            }
            else if (tmp is SearchItemAlbum)
            {
                this.Frame.Navigate(typeof(AlbumPage), new object[] { tmp.Id });
            }
            else
            { }
        }

        private async void SearchType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(SearchBox.Text)) return;

            _searchHandle.NewSearch(SearchType.SelectedIndex, SearchBox.Text);
            await SearchResults.LoadMoreItemsAsync();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
        }

        private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (string.IsNullOrEmpty(SearchBox.Text))
            {
                SearchHistory.Visibility = Visibility.Visible;
                SearchResults.Visibility = Visibility.Collapsed;

                _searchHandle.Init();
            }
        }

        private async void DeleteWord_Click(object sender, RoutedEventArgs e)
        {
            string word = (e.OriginalSource as Button).DataContext.ToString();

            _searchHistory.Remove(word);
            await FileHelper.Current.WriteObjectAsync<List<string>>(_searchHistory.ToList<string>(), "SearchHistory.json");
        }

        private async void HistoryWords_ItemClick(object sender, ItemClickEventArgs e)
        {
            string word = e.ClickedItem.ToString();
            if (string.IsNullOrEmpty(word)) return;

            SearchBox.Text = word;
            SearchHistory.Visibility = Visibility.Collapsed;
            SearchResults.Visibility = Visibility.Visible;
            int index = SearchType.SelectedIndex < 0 ? 0 : SearchType.SelectedIndex;

            AddWordToSearchHistory(word);
            _searchHandle.NewSearch(index, SearchBox.Text);
            await SearchResults.LoadMoreItemsAsync();            
        }

        private async void AddWordToSearchHistory(string word)
        {
            if (string.IsNullOrEmpty(word)) return;

            string newWord = word.Trim();
            if (_searchHistory.Contains(newWord))
            {
                _searchHistory.Remove(newWord);
            }

            _searchHistory.Insert(0, newWord);
            // 历史上限 20 条：既避免文件无限增长，也让下拉列表保持可用
            while (_searchHistory.Count > 20)
            {
                _searchHistory.RemoveAt(_searchHistory.Count - 1);
            }

            await FileHelper.Current.WriteObjectAsync<List<string>>(_searchHistory.ToList<string>(), "SearchHistory.json");
        }

        private async void ClearSearchHistory_Click(object sender, RoutedEventArgs e)
        {
            _searchHistory.Clear();
            await FileHelper.Current.WriteObjectAsync<List<string>>(_searchHistory.ToList<string>(), "SearchHistory.json");
        }

        private void Grid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // 预留滚动条 24px，再均分列数并扣除每张卡片的 Margin 8 + 边框 2 + 内边距 24
            double listWidth = e.NewSize.Width - 24;
            int column = (int)(listWidth / 330);
            if (column < 1) column = 1;
            width.Width = listWidth / column - 34;
        }

        private async void PullToRefreshBox_RefreshInvoked(DependencyObject sender, object args)
        {
            RadioSearchViewModel search = null;
            if (SearchResults.ItemsSource != null)
            {
               search = SearchResults.ItemsSource as RadioSearchViewModel;
            }

            if(search != null)
            {
                search.Init();
                await SearchResults.LoadMoreItemsAsync();
            }                   
        }

        private void DownloadAlbum_Click(object sender, RoutedEventArgs e)
        {
            SearchItemProgram tmp = ((FrameworkElement)e.OriginalSource).DataContext as SearchItemProgram;
            if (tmp == null) return;

            AlbumItem item = new AlbumItem()
            {
                Id = tmp.Id,
                ImageUri = tmp.ImageUri,
                Title = tmp.Title,
                FilePath = tmp.FilePath,
                AlbumName = tmp.AlbumName,
                AlbumId = tmp.AlbumId,
                Type = MusicItem.AudioType.Album,
            };

            AlbumPage.DownloadProgramItem(item);
        }

        private void FavoriteAlbum_Click(object sender, RoutedEventArgs e)
        {
            MusicItem tmp = ((FrameworkElement)e.OriginalSource).DataContext as MusicItem;
            if (tmp == null) return;

            FavoriteSearchItem(tmp);
        }

        public async static void FavoriteSearchItem(MusicItem tmp)
        {
            if (tmp == null) return;

            if (tmp is SearchItemRadio)
            {
                RadioItem item = new RadioItem()
                {
                    Id = tmp.Id,
                    ImageUri = tmp.ImageUri,
                    Title = tmp.Title,
                    Type = MusicItem.AudioType.Live
                };

                await AppHelper.Current.SaveFavoriteRadio(item);
            }
            else if (tmp is SearchItemProgram program)
            {
                AlbumItem item = new AlbumItem()
                {
                    Id = program.Id,
                    ImageUri = program.ImageUri,
                    Title = program.Title,
                    FilePath = program.FilePath,
                    AlbumName = program.AlbumName,
                    AlbumId = program.AlbumId,
                    Type = MusicItem.AudioType.Album,
                };

                await AppHelper.Current.SaveFavoriteProgram(item);
            }
            else if (tmp is SearchItemAlbum)
            {
                await AppHelper.Current.SaveFavoriteAlbum(tmp.Id);
            }
            else
            { }
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

        private void Radio_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (e.Pointer.PointerDeviceType != Windows.Devices.Input.PointerDeviceType.Mouse) return;
            ((((sender as Grid).Children[2] as StackPanel).Children[0] as Grid).Children[1] as Button).Visibility = Visibility.Visible;
        }

        private void Radio_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (e.Pointer.PointerDeviceType != Windows.Devices.Input.PointerDeviceType.Mouse) return;
            ((((sender as Grid).Children[2] as StackPanel).Children[0] as Grid).Children[1] as Button).Visibility = Visibility.Collapsed;
        }

        private void Program_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (e.Pointer.PointerDeviceType != Windows.Devices.Input.PointerDeviceType.Mouse) return;
            ((((sender as Grid).Children[2] as Grid).Children[0] as Grid).Children[1] as StackPanel).Visibility = Visibility.Visible;
        }

        private void Program_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (e.Pointer.PointerDeviceType != Windows.Devices.Input.PointerDeviceType.Mouse) return;
            ((((sender as Grid).Children[2] as Grid).Children[0] as Grid).Children[1] as StackPanel).Visibility = Visibility.Collapsed;
        }

        private void Album_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (e.Pointer.PointerDeviceType != Windows.Devices.Input.PointerDeviceType.Mouse) return;
            ((((sender as Grid).Children[1] as Grid).Children[0] as Grid).Children[1] as Button).Visibility = Visibility.Visible;
        }

        private void Album_PointerExited(object sender, PointerRoutedEventArgs e)
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

    public class ConvertSearchCommonData : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null) return value;

            string key = parameter.ToString();
            if (key.Equals("duration"))
            {
                // 搜索结果里的节目常常没有 duration 字段，此时是空串；
                // 原来的 double.Parse 会抛 FormatException，导致搜索结果列表渲染失败。
                double seconds;
                if (!double.TryParse(value.ToString(), out seconds) || seconds <= 0)
                {
                    return string.Empty;
                }

                TimeSpan tmp = TimeSpan.FromSeconds(seconds);
                return "时长：" + (tmp.Hours > 0 ? tmp.ToString(@"hh\:mm\:ss") : tmp.ToString(@"mm\:ss"));
            }
            else if (key.Equals("updatetime"))
            {
                double seconds;
                DateTime startTime = new DateTime(1970, 1, 1);
                double.TryParse(value.ToString(), out seconds);
                DateTime dt = startTime.AddSeconds(seconds);
                return dt.ToString("yyyy-MM-dd");
            }
            else if (key.Equals("description"))
            {
                string desc = value.ToString();

                if (desc.Length <= 18)
                {
                    return desc;
                }
                else
                {
                    return desc.Substring(0, 18) + "...";
                }
            }
            else
            {
                return null;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    internal class SearchFavoriteCommand : ICommand
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
            MusicItem tmp = parameter as MusicItem;
            if (tmp == null) return;

            SearchPage.FavoriteSearchItem(tmp);
        }
    }

    internal class SearchItemDownloadCommand : ICommand
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
            SearchItemProgram tmp = parameter as SearchItemProgram;
            if (tmp == null) return;
            AlbumItem item = new AlbumItem()
            {
                Id = tmp.Id,
                ImageUri = tmp.ImageUri,
                Title = tmp.Title,
                FilePath = tmp.FilePath,
                AlbumName = tmp.AlbumName,
                AlbumId = tmp.AlbumId,
                Type = MusicItem.AudioType.Album,
            };

            AlbumPage.DownloadProgramItem(item);
        }
    }
}
