using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using QingtingRadio.ViewModels;
using Windows.System.Profile;
using Windows.UI;
using Windows.UI.ViewManagement;
using Windows.Foundation.Metadata;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Core;
using Windows.Foundation;
using Windows.ApplicationModel.Core;
using System.ComponentModel;
using Windows.UI.Popups;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Controls.Primitives;
using QingtingRadio.Tools;
using System.Threading.Tasks;
using Windows.Graphics.Display;
using System.Diagnostics;
using System.Linq;

//“空白页”项模板在 http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409 上有介绍

namespace QingtingRadio
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private DispatcherTimer themeTimer;
        private bool isExit = false;
        // SplitView.DisplayMode 的回调登记令牌：Loaded 登记、Unloaded 注销，避免回调用悬挂的 this 引用
        private long rootSplitViewDisplayModeToken;

        // 无导航参数也可安全恢复的页面白名单（见 OnNavigatedTo）
        private static readonly Type[] RestorablePageTypes =
        {
            typeof(SearchPage),
            typeof(HistoryPage),
            typeof(DownloadLists),
            typeof(FavoriteLists),
            typeof(SettingsPage),
        };

        public MainPage()
        {
            this.InitializeComponent();
            this.Loaded += MainPage_Loaded;
            this.Unloaded += MainPage_Unloaded; 

            NavigationCacheMode = NavigationCacheMode.Required;         
        }

        private void MainPage_Unloaded(object sender, RoutedEventArgs e)
        {
            RadioSingleProperty.Current.PropertyChanged -= SinglePropertyChanged;
            RadioSettingsViewModel.Instance.PropertyChanged -= SinglePropertyChanged;
            // Loaded 里登记的回调必须在这里对称注销，否则重复进入 MainPage 会叠加回调
            SystemNavigationManager.GetForCurrentView().BackRequested -= MainPage_BackRequested;
            if (rootSplitViewDisplayModeToken != 0)
            {
                RootSplitView.UnregisterPropertyChangedCallback(SplitView.DisplayModeProperty, rootSplitViewDisplayModeToken);
                rootSplitViewDisplayModeToken = 0;
            }
        }

        private void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            Current = this;
            this.CheckTogglePaneButtonSizeChanged();
            RadioSettingsViewModel.Instance.PropertyChanged += SinglePropertyChanged;
            RadioSingleProperty.Current.PropertyChanged += SinglePropertyChanged;
            SystemNavigationManager.GetForCurrentView().BackRequested += MainPage_BackRequested;
            SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Collapsed;
            rootSplitViewDisplayModeToken = RootSplitView.RegisterPropertyChangedCallback(
                SplitView.DisplayModeProperty,
                (s, a) =>
                {
                   CheckTogglePaneButtonSizeChanged();
                });

            // OnNavigatedTo 可能已经恢复上次停留的页面；此时不能再用首页覆盖它。
            // 只有内容帧还是空的（首次启动且无可恢复页面）时才默认进首页，
            // 顺带避免每次回到 MainPage 都往 SubFrame 压一层重复的首页。
            if (SubFrame.Content == null)
            {
                SubFrame.Navigate(typeof(HomePage));
            }
        }

        private void MainPage_BackRequested(object sender, BackRequestedEventArgs e)
        {
            if (SubFrame.CanGoBack)
            {
                e.Handled = true;
                SubFrame.GoBack();
            }
            else
            {
                if (isExit) App.Current.Exit();
                else
                {                   
                    NotifyUser notifyPopup = new NotifyUser("再按一次退出");
                    notifyPopup.Show();

                    isExit = true;

                    Task.Run(async () =>
                    {                       
                        await Task.Delay(1500);

                        isExit = false;
                    });

                    e.Handled = true;
                }             
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            AutoSwitchThemeTimer();
            if (!RadioSettingsViewModel.Instance.AutoSwitchTheme)
            {
                ChangeAppTheme();
            }

            // 应用被系统终止后重启：e.Parameter 携带要恢复的页面类型名，恢复上次停留的页面。
            // 只恢复不需要导航参数的页面：AlbumPage/RadioList 等依赖参数的页面若直接恢复，
            // 会因参数缺失抛异常，统一回退到首页。
            string lastPage = e.Parameter as string;
            if (!string.IsNullOrEmpty(lastPage))
            {
                try
                {
                    Type pageType = Type.GetType(lastPage);
                    if (pageType != null && RestorablePageTypes.Contains(pageType))
                    {
                        ContentFrame.Navigate(pageType);
                    }
                }
                catch (Exception ex)
                {
                    LogManager.LogAsync("restore last page " + lastPage, ex);
                }
            }
        }


        public static MainPage Current = null;
        public Frame ContentFrame { get { return SubFrame; } }

        public Rect TogglePaneButtonRect
        {
            get;
            private set;
        }

        public event TypedEventHandler<MainPage, Rect> TogglePaneButtonRectChanged;

        public void OpenNavePane()
        {
            TogglePaneButton.IsChecked = true;
        }


        private void RootSplitView_PaneClosed(SplitView sender, object args)
        {
        }

        private void TogglePaneButton_Unchecked(object sender, RoutedEventArgs e)
        {          
            this.CheckTogglePaneButtonSizeChanged();

            SplitViewDisplayMode mode = RootSplitView.DisplayMode;
        }

        private void TogglePaneButton_Checked(object sender, RoutedEventArgs e)
        {          
            CheckTogglePaneButtonSizeChanged();

            SplitViewDisplayMode mode = RootSplitView.DisplayMode;
        }

        private void CheckTogglePaneButtonSizeChanged()
        {
            if (RootSplitView.DisplayMode == SplitViewDisplayMode.Inline ||
                RootSplitView.DisplayMode == SplitViewDisplayMode.Overlay)
            {
                var transform = TogglePaneButton.TransformToVisual(this);
                var rect = transform.TransformBounds(new Rect(0, 0, TogglePaneButton.ActualWidth, TogglePaneButton.ActualHeight));
                TogglePaneButtonRect = rect;
            }
            else
            {
                TogglePaneButtonRect = new Rect();
            }

            var handler = TogglePaneButtonRectChanged;
            if (handler != null)
            {
                // 这里原来同时对同一个事件调用了两次（普通调用 + DynamicInvoke），
                // 等于每个订阅者都被通知两遍，容易造成重复布局/重复副作用。
                handler(this, this.TogglePaneButtonRect);
            }
        }



        private async void CustomTitleBar()
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (ApiInformation.IsTypePresent("Windows.UI.ViewManagement.StatusBar"))
                {
                 //   ApplicationView.GetForCurrentView().SetDesiredBoundsMode( ApplicationViewBoundsMode.UseCoreWindow);
                    StatusBar statusBar = StatusBar.GetForCurrentView();
                    statusBar.BackgroundOpacity = 1;
                    if (this.Frame.RequestedTheme == ElementTheme.Light)
                    {
                        statusBar.BackgroundColor = Colors.White;
                        statusBar.ForegroundColor = Color.FromArgb(255, 77, 77, 77);
                    }
                    else
                    {
                        statusBar.BackgroundColor = Color.FromArgb(255, 29, 29, 29);
                        statusBar.ForegroundColor = Color.FromArgb(255, 177, 177, 177);
                    }
                }
                else
                {
                    CoreApplication.GetCurrentView().TitleBar.ExtendViewIntoTitleBar = false;
                    var titleBar = Windows.UI.ViewManagement.ApplicationView.GetForCurrentView().TitleBar;
                    if (this.Frame.RequestedTheme == ElementTheme.Light)
                    {
                        titleBar.BackgroundColor = Colors.White;
                        titleBar.ForegroundColor = Color.FromArgb(255, 77, 77, 77);
                        titleBar.InactiveBackgroundColor = Color.FromArgb(255, 240, 240, 240);
                        titleBar.InactiveForegroundColor = Color.FromArgb(255, 77, 77, 77);
                        titleBar.ButtonBackgroundColor = Colors.White;
                        titleBar.ButtonInactiveBackgroundColor = Color.FromArgb(255, 240, 240, 240);
                        titleBar.ButtonInactiveForegroundColor = Color.FromArgb(255, 77, 77, 77);

                    }
                    else
                    {
                        titleBar.BackgroundColor = Color.FromArgb(255, 29, 29, 29);
                        titleBar.ForegroundColor = Color.FromArgb(255, 177, 177, 177);
                        titleBar.InactiveBackgroundColor = Color.FromArgb(255, 36, 36, 36);
                        titleBar.InactiveForegroundColor = Color.FromArgb(255, 177, 177, 177);
                        titleBar.ButtonBackgroundColor = Color.FromArgb(255, 29, 29, 29);
                        titleBar.ButtonInactiveBackgroundColor = Color.FromArgb(255, 36, 36, 36);
                        titleBar.ButtonInactiveForegroundColor = Color.FromArgb(255, 177, 177, 177);
                    }
                }
            });               
        }

        private async void CheckAppTheme()
        {
            CheckSplitViewPaneOpenStatus();
            if (RadioSettingsViewModel.Instance.AutoSwitchTheme)
            {
                string message = "是否关闭自动切换夜间模式？";
                bool result = await AppHelper.ShowMessageDialog(message, "是", "否");
                if (result)
                {
                    RadioSettingsViewModel.Instance.AutoSwitchTheme = false;
                }
                else
                {
                    return;
                }
            }
                     
            string ThemeTag = ThemeIcon.Tag.ToString();
            RadioSettingsViewModel.Instance.AppTheme = ThemeTag.Equals("0") ? true : false;
        }

        public void ChangeAppTheme()
        {
            if (RadioSettingsViewModel.Instance.AppTheme)
            {
                this.Frame.RequestedTheme = ElementTheme.Dark;
                ThemeIcon.Text = "\xE708";
                ThemeIcon.Tag = "1";
            }
            else
            {              
                this.Frame.RequestedTheme = ElementTheme.Light;
                ThemeIcon.Text = "\xE706";
                ThemeIcon.Tag = "0";
            }
            CustomTitleBar();
        }        

        private void CheckSplitViewPaneOpenStatus()
        {
            if (RootSplitView.IsPaneOpen && (
                 RootSplitView.DisplayMode == SplitViewDisplayMode.CompactOverlay ||
                 RootSplitView.DisplayMode == SplitViewDisplayMode.Overlay))
            {
                RootSplitView.IsPaneOpen = false;
            }
        }

        public void SinglePropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if(e.PropertyName.Equals("IsLoading"))
            {
                //ring.IsActive = (sender as RadioSingleProperty).IsLoading;
            }
            else if (e.PropertyName.Equals("PanelVisibility"))
            {
                PlayerPanelControl.Visibility = (sender as RadioSingleProperty).PanelVisibility;
            }
            else if (e.PropertyName.Equals("AutoSwitchTheme"))
            {
                AutoSwitchThemeTimer();
            }
            else if (e.PropertyName.Equals("AutoSwitchThemeStartTime") || e.PropertyName.Equals("AutoSwitchThemeEndTime"))
            {
                if (RadioSettingsViewModel.Instance.AutoSwitchTheme)
                {
                    UpdateTimerInterval();
                }
            }
            else if (e.PropertyName.Equals("AppTheme"))
            {
                ChangeAppTheme();
            }
            else
            {

            }
        }


        private void AutoSwitchThemeTimer()
        {
            EnsureThemeTimer();

            if (RadioSettingsViewModel.Instance.AutoSwitchTheme)
            {
                UpdateTimerInterval();
                themeTimer.Start();
            }
            else
            {
                themeTimer.Stop();
            }
        }

        private void EnsureThemeTimer()
        {
            if (themeTimer == null)
            {
                themeTimer = new DispatcherTimer();
                // Tick 订阅只做一次，跟随 timer 生命周期，避免重复订阅叠加
                themeTimer.Tick += (s, e) =>
                {
                    UpdateTimerInterval();
                };
            }
        }

        /// <summary>
        /// 计算当前应处于的主题，并把定时器对齐到下一个切换时刻。
        /// 每个分支都必须重设 Interval：此前 start==end（默认值 0）时什么都不做，
        /// 定时器以 1ms 间隔永久空转，等于忙轮询 UI 线程。
        /// </summary>
        private void UpdateTimerInterval()
        {
            EnsureThemeTimer();

            if (!RadioSettingsViewModel.Instance.AutoSwitchTheme)
            {
                themeTimer.Stop();
                return;
            }

            double nowSeconds = DateTime.Now.TimeOfDay.TotalSeconds;
            double startSeconds = RadioSettingsViewModel.Instance.AutoSwitchThemeStartTime;
            double endSeconds = RadioSettingsViewModel.Instance.AutoSwitchThemeEndTime;

            if (startSeconds == endSeconds)
            {
                // 起止时间相同视为无效配置：保留设置，改为每 24 小时重新检查
                themeTimer.Interval = TimeSpan.FromHours(24);
                return;
            }

            // 支持跨午夜时段（end < start），例如 22:00 - 6:00
            bool isDark = startSeconds < endSeconds
                ? nowSeconds >= startSeconds && nowSeconds < endSeconds
                : nowSeconds >= startSeconds || nowSeconds < endSeconds;

            RadioSettingsViewModel.Instance.AppTheme = isDark;

            // 夜间中 → 等结束时刻；白天中 → 等开始时刻；已过则顺延一天
            double nextBoundary = isDark ? endSeconds : startSeconds;
            double delta = nextBoundary - nowSeconds;
            if (delta <= 0)
            {
                delta += 86400;
            }

            themeTimer.Interval = TimeSpan.FromSeconds(Math.Max(delta, 1));
        }

        private void FirstMenu_Tapped(object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e)
        {            
            int selectedIndex = (sender as ListBox).SelectedIndex;
            switch (selectedIndex)
            {
                case 0:
                    CheckSplitViewPaneOpenStatus();
                    SubFrame.Navigate(typeof(HomePage));
                    break;
                case 1:
                    CheckSplitViewPaneOpenStatus();
                    SubFrame.Navigate(typeof(SearchPage));
                    break;                
                case 2:
                    CheckSplitViewPaneOpenStatus();
                    SubFrame.Navigate(typeof(HistoryPage));
                    break;
                case 3:
                    CheckSplitViewPaneOpenStatus();
                    SubFrame.Navigate(typeof(DownloadLists));
                    break;
                case 4:
                    CheckSplitViewPaneOpenStatus();
                    SubFrame.Navigate(typeof(FavoriteLists));
                    break;
                default:
                    break;
            }
            FirstMenu.SelectedIndex = -1;
        }

        private void SecondMenu_Tapped(object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e)
        {           
            int selectedIndex = (sender as ListBox).SelectedIndex;
            switch(selectedIndex)
            {
                case 0:
                    CheckAppTheme();
                    break;
                case 1:
                    CheckSplitViewPaneOpenStatus();
                    SubFrame.Navigate(typeof(SettingsPage));
                    break;         
                default:
                    break;
            }
            SecondMenu.SelectedIndex = -1;
        }

        private void SubFrame_Navigated(object sender, NavigationEventArgs e)
        {
            if (SubFrame.CanGoBack)
            {
                SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility =
                    AppViewBackButtonVisibility.Visible;
            }
            else
            {
                SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility =
                    AppViewBackButtonVisibility.Collapsed;
            }
        }

        private void Search_Click(object sender, RoutedEventArgs e)
        {
            this.SubFrame.Navigate(typeof(SearchPage));
        }

        public static Size GetScreen()
        {
            var applicationView = ApplicationView.GetForCurrentView();
            var displayInformation = DisplayInformation.GetForCurrentView();
            var bounds = applicationView.VisibleBounds;
            var scale = displayInformation.RawPixelsPerViewPixel;
            var size = new Size(bounds.Width * scale, bounds.Height * scale);
            return size;
        }
    }   
}
