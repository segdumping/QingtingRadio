using QingtingRadio.DataModels;
using QingtingRadio.Https;
using QingtingRadio.Tools;
using QingtingRadio.ViewModels;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.UI;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Navigation;

namespace QingtingRadio
{
    /// <summary>
    /// 回听/节目列表页面，双模式：
    /// - 电台回听（默认）：左侧节目名+时间，右侧 回听/下载/收藏，顶部 前天/昨天/今天 页签；
    /// - 专辑节目（导航参数 "album"）：左侧节目名+时长，右侧 播放/下载/收藏，顶部显示专辑名。
    /// 由 PlayPage / PlayerPanel 的"回听列表"按钮导航进入，替代原先的弹出浮层。
    /// </summary>
    public sealed partial class ReplayPage : Page
    {
        private RadioReplayViewModel m_Replay;
        private RadioAlbumViewModel Album = new RadioAlbumViewModel();
        private MediaListViewModel MediaList = MediaListViewModel.Instance;

        public ReplayPage()
        {
            this.InitializeComponent();

            m_Replay = new RadioReplayViewModel();
            ReplayListView.ItemsSource = m_Replay;
            m_Replay.DataLoading += DataLoading;
            m_Replay.DataLoaded += DataLoaded;

            Album.DataLoading += DataLoading;
            Album.DataLoaded += DataLoaded;
            MediaList.PropertyChanged += MediaList_PropertyChanged;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if ((e.Parameter as string) == "album")
            {
                // 专辑节目模式
                TitleName.Text = "节目列表";
                DayTabs.Visibility = Visibility.Collapsed;
                AlbumTitle.Visibility = Visibility.Visible;
                ReplayListView.Visibility = Visibility.Collapsed;
                AlbumListView.Visibility = Visibility.Visible;

                LoadAlbumList();
            }
            else
            {
                // 电台回听模式（默认）
                TitleName.Text = "回听节目";
                LoadReplayList();
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);

            m_Replay.DataLoading -= DataLoading;
            m_Replay.DataLoaded -= DataLoaded;
            // MediaList 是单例，不退订会让本页被单例长期持有（每次进入回听页都泄漏一个实例，
            // 且在已离开的页面上继续触发滚动逻辑）。
            MediaList.PropertyChanged -= MediaList_PropertyChanged;
        }

        #region 专辑节目模式

        private void LoadAlbumList()
        {
            AlbumInfo tmp = MediaListViewModel.Instance.LastestAlbumInfo;
            if (tmp == null) return;
            AlbumTitle.Text = tmp.Title;
            Album.Init(tmp.Id, RadioAlbumViewModel.LoadType.control);
            AlbumListView.ItemsSource = Album;
            ScrollAlbumIntoView();
        }

        private async void AlbumListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            await PlayAlbumItemCoreAsync(e.ClickedItem as AlbumItem);
        }

        private async void AlbumPlay_Click(object sender, RoutedEventArgs e)
        {
            await PlayAlbumItemCoreAsync(((FrameworkElement)e.OriginalSource).DataContext as AlbumItem);
        }

        private async Task PlayAlbumItemCoreAsync(AlbumItem tmp)
        {
            if (tmp == null) return;
            AlbumInfo info = await Album.LoadAlbumInfo();
            if (info == null) return;
            if (!info.SaleType.Equals("0"))
            {
                NotifyUser notifyPopup = new NotifyUser("本专辑为收费类型， 请移步蜻蜓FM官方网站 或 官方app收听");
                notifyPopup.Show();
                return;
            }

            tmp.ImageUri = info.ImageUri;
            tmp.AlbumName = info.Title;

            await RadioPlayViewModel.Instance.PlayAlbumItem(tmp, info.Id);
        }

        private async void AlbumDownload_Click(object sender, RoutedEventArgs e)
        {
            AlbumItem item = ((FrameworkElement)e.OriginalSource).DataContext as AlbumItem;

            if (NetworkManager.Current.NetworkTitle.Contains("G") && !RadioSettingsViewModel.Instance.MoNetDownload)
            {
                NotifyUser notifyPopup = new NotifyUser("已禁用2G/3G/4G网络下载");
                notifyPopup.Show();
                return;
            }

            AlbumInfo info = await Album.LoadAlbumInfo();
            if (item == null || info == null) return;

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

        private async void AlbumFavorite_Click(object sender, RoutedEventArgs e)
        {
            AlbumItem item = ((FrameworkElement)e.OriginalSource).DataContext as AlbumItem;
            if (item == null) return;
            AlbumInfo info = await Album.LoadAlbumInfo();
            if (info == null) return;
            item.ImageUri = info.ImageUri;
            item.AlbumName = info.Title;
            item.AlbumId = info.Id;

            await AppHelper.Current.SaveFavoriteProgram(item);
        }

        private void ScrollAlbumIntoView()
        {
            try
            {
                if (MediaList.SelectedIndex < 0) return;
                if (AlbumListView.Items.Count <= MediaList.SelectedIndex) return;

                AlbumListView.ScrollIntoView(AlbumListView.Items[MediaList.SelectedIndex], ScrollIntoViewAlignment.Leading);
            }
            catch (Exception ex)
            {
                LogManager.LogAsync("scroll album into view", ex);
            }
        }

        private void MediaList_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName.Equals("SelectedIndex"))
            {
                try
                {
                    int index = (sender as MediaListViewModel).SelectedIndex;
                    AlbumListView.ScrollIntoView(AlbumListView.Items[index], ScrollIntoViewAlignment.Leading);
                }
                catch (Exception ex)
                {
                    LogManager.LogAsync("medialist property changed", ex);
                }
            }
        }

        #endregion

        #region 电台回听模式

        private void LoadReplayList()
        {
            if (NetworkManager.Current.Network == 5)
            {
                NotifyUser notifyPopup = new NotifyUser("当前无网络链接");
                notifyPopup.Show();
                return;
            }

            m_Replay.LoadReplayInfo(0);
            ReplayListView.Tag = "0";
            ButtonNewBackground("today");

            ScrollIntoView();
        }

        private async void ReplayListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            await PlayReplayAsync(e.ClickedItem as ReplayRadio, GetSelectedDateYmd());
        }

        private async void PlayReplay_Click(object sender, RoutedEventArgs e)
        {
            await PlayReplayAsync(((FrameworkElement)e.OriginalSource).DataContext as ReplayRadio, GetSelectedDateYmd());
        }

        /// <summary>
        /// 当前页签对应的日期（yyyyMMdd），用于回听地址与缓存文件名。
        /// </summary>
        private string GetSelectedDateYmd()
        {
            int dayFlag = 0;
            int.TryParse(ReplayListView.Tag?.ToString(), out dayFlag);
            return DateTime.Now.AddDays(dayFlag * (-1)).ToString("yyyyMMdd");
        }

        /// <summary>
        /// 回听播放流程（节目单与收藏列表共用）。dateYmd 为该回听节目所属日期。
        /// </summary>
        public static async Task PlayReplayAsync(ReplayRadio replay, string dateYmd)
        {
            if (replay == null) return;
            LastestPlay item = await LastestPlay.LoadCurrentAsync() ?? new LastestPlay();

            // 收藏的回听节目可能属于另一台电台，优先用收藏时记录的电台名
            string stationTitle = string.IsNullOrEmpty(replay.RadioTitle) ? item.Title : replay.RadioTitle;

            item.Title = stationTitle;
            item.FileName = stationTitle + "_" + replay.Title + "_" + dateYmd + "_" + replay.StartTime.Replace(":", "") + "_" + replay.EndTime.Replace(":", "") + ".aac";

            string time = replay.DateTime.Substring(0, 10).Replace("-", "") + "/" + replay.Id + "/" + replay.Id + "_" + replay.DateTime.Substring(0, 10).Replace("-", "") + "_" + replay.StartTime.Replace(":", "") + "_" + replay.EndTime.Replace(":", "");
            item.PlayUri = string.Format(ServiceURLs.NewReply, time, RadioSettingsViewModel.Instance.DeviceId);

            item.Id = replay.Id;
            item.Playing = replay.Title;
            item.StartTime = replay.StartTime;
            item.EndTime = replay.EndTime;
            item.Type = replay.Type;
            item.Date = dateYmd;

            await RadioPlayViewModel.Instance.PlayRadioReplay(item);
        }

        private async void Download_Click(object sender, RoutedEventArgs e)
        {
            await DownloadRadioItemAsync(((FrameworkElement)e.OriginalSource).DataContext as ReplayRadio);
        }

        private async void Favorite_Click(object sender, RoutedEventArgs e)
        {
            ReplayRadio replay = ((FrameworkElement)e.OriginalSource).DataContext as ReplayRadio;
            if (replay == null) return;

            try
            {
                List<ReplayRadio> favorites = await ReplayRadio.LoadFavoritesAsync();
                if (favorites == null) favorites = new List<ReplayRadio>();

                if (favorites.Any(f => f.Id == replay.Id && f.DateTime == replay.DateTime))
                {
                    AppHelper.Current.ShowOperationMessage("【" + replay.Title + "】已在收藏中");
                    return;
                }

                // 记录所属电台名，供收藏列表回听时正确显示与命名
                LastestPlay current = await LastestPlay.LoadCurrentAsync();
                replay.RadioTitle = current?.Title;

                favorites.Add(replay);
                await ReplayRadio.SaveFavoritesAsync(favorites);
                AppHelper.Current.ShowOperationMessage("【" + replay.Title + "】收藏成功");
            }
            catch (Exception ex)
            {
                LogManager.LogAsync("favorite replay", ex);
            }
        }

        private async Task DownloadRadioItemAsync(ReplayRadio replay)
        {
            if (replay == null) return;

            if (NetworkManager.Current.NetworkTitle.Contains("G") && !RadioSettingsViewModel.Instance.MoNetDownload)
            {
                NotifyUser notifyPopup = new NotifyUser("已禁用2G/3G/4G网络下载");
                notifyPopup.Show();
                return;
            }

            LastestPlay item = await LastestPlay.LoadCurrentAsync();
            if (item == null) return;

            string tmpTime = replay.DateTime.Substring(0, 10).Replace("-", "") + "/" + replay.Id + "/" + replay.Id + "_" + replay.DateTime.Substring(0, 10).Replace("-", "") + "_" + replay.StartTime.Replace(":", "") + "_" + replay.EndTime.Replace(":", "");
            string downloadUri = string.Format(ServiceURLs.NewDownLoad, tmpTime, RadioSettingsViewModel.Instance.DeviceId);
            string filename = Regex.Replace(CreateDownlodFileName(replay, item), "[\\,/,:,*,?,\",<,>,&,#,!,@,$,%,^]", "", RegexOptions.IgnoreCase);

            bool isexist = await FileHelper.Current.CacheExist(filename, FileHelper.CacheType.AudioCache);
            if (isexist)
            {
                string message = "文件已存在，是否重新下载？";
                bool result = await AppHelper.ShowMessageDialog(message, "是", "否");
                if (!result) return;
            }

            DownloadItem tmp = new DownloadItem()
            {
                FileUri = downloadUri,
                FileName = filename,
                Duration = GetPlayDuration(replay).ToString(),
                ImageUri = item.ImageUri,
                Progress = 0,
                IsSuccess = false,
                AudioId = item.Id,
                Type = item.Type,
                AudioTime = replay.DateTime,
                AlbumName = Regex.Replace(item.Title, "[\\,/,:,*,?,\",<,>,&,#,!,@,$,%,^]", "", RegexOptions.IgnoreCase),
                Title = Regex.Replace(replay.Title, "[\\,/,:,*,?,\",<,>,&,#,!,@,$,%,^]", "", RegexOptions.IgnoreCase),
            };

            await FileDownloadHelper.Current.StartDownload(tmp);
        }

        private string CreateDownlodFileName(ReplayRadio replay, LastestPlay item)
        {
            // 用节目单自身的日期命名，而不是共享的"当前页签"静态标记：
            // 收藏列表等入口进来时页签状态可能和节目实际日期不一致
            string date = !string.IsNullOrEmpty(replay.DateTime) && replay.DateTime.Length >= 10
                ? replay.DateTime.Substring(0, 10).Replace("-", "")
                : DateTime.Now.ToString("yyyyMMdd");

            return item.Title + "_" + replay.Title + "_" + date + "_" + replay.StartTime.Replace(":", "") + "_" + replay.EndTime.Replace(":", "") + ".aac";
        }

        private static double GetPlayDuration(ReplayRadio replay)
        {
            DateTime sdt = DateTime.Parse("2000-01-01" + " " + replay.StartTime);
            DateTime edt = DateTime.Parse("2000-01-01" + " " + replay.EndTime);
            return (double.Parse(edt.TimeOfDay.TotalSeconds.ToString()) - double.Parse(sdt.TimeOfDay.TotalSeconds.ToString()));
        }

        private void ScrollIntoView()
        {
            try
            {
                if (ReplayListView.Items.Count == 0) return;
                ItemCollection items = ReplayListView.Items;
                ReplayRadio tmp = items[items.Count - 1] as ReplayRadio;

                DateTime startDate = DateTime.Parse(DateTime.Now.ToString("yyyy-MM-dd") + " " + tmp.StartTime);
                DateTime endDate = DateTime.Parse(DateTime.Now.ToString("yyyy-MM-dd") + " " + tmp.EndTime);

                if (DateTime.Compare(startDate, DateTime.Now) < 0 && DateTime.Compare(endDate, DateTime.Now) > 0)
                {
                    ReplayListView.ScrollIntoView(ReplayListView.Items[items.Count - 1], ScrollIntoViewAlignment.Leading);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
            }
        }

        private void DayBefore_Click(object sender, RoutedEventArgs e)
        {
            m_Replay.LoadReplayInfo(2);
            ReplayListView.Tag = "2";
            ButtonNewBackground("daybefore");
            ScrollIntoView();
        }

        private void YesToday_Click(object sender, RoutedEventArgs e)
        {
            m_Replay.LoadReplayInfo(1);
            ReplayListView.Tag = "1";
            ButtonNewBackground("yestoday");
            ScrollIntoView();
        }

        private void Today_Click(object sender, RoutedEventArgs e)
        {
            m_Replay.LoadReplayInfo(0);
            ReplayListView.Tag = "0";
            ButtonNewBackground("today");
            ScrollIntoView();
        }

        private void ButtonNewBackground(string flag)
        {
            UISettings uiSettings = new UISettings();

            Today.Background = new SolidColorBrush();
            DayBefore.Background = new SolidColorBrush();
            YesToday.Background = new SolidColorBrush();

            if (flag.Equals("today"))
            {
                Today.Background = new SolidColorBrush(uiSettings.GetColorValue(UIColorType.Accent));
            }
            else if (flag.Equals("yestoday"))
            {
                YesToday.Background = new SolidColorBrush(uiSettings.GetColorValue(UIColorType.Accent));
            }
            else
            {
                DayBefore.Background = new SolidColorBrush(uiSettings.GetColorValue(UIColorType.Accent));
            }
        }

        private void DataLoading()
        {
            RingCircle.IsActive = true;
        }

        private void DataLoaded()
        {
            RingCircle.IsActive = false;
        }

        #endregion
    }

    /// <summary>
    /// 专辑节目时长显示：只输出 mm:ss（不带"时长："前缀），与回听行的时间风格一致。
    /// </summary>
    public class AlbumItemDurationConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null) return value;

            TimeSpan tmp = TimeSpan.FromSeconds(double.Parse(value.ToString()));
            return tmp.Hours > 0 ? tmp.ToString(@"hh\:mm\:ss") : tmp.ToString(@"mm\:ss");
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// 节目名显示：去掉首尾的中英文括号与空白（蜻蜓接口会用括号标注重播/经典重现类节目）。
    /// </summary>
    public class ReplayTitleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            string title = value as string;
            if (string.IsNullOrEmpty(title)) return title;

            return title.Trim('（', '）', '(', ')', ' ');
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
