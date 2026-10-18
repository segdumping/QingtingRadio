using QingtingRadio.Tools;
using System;
using System.Diagnostics;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;


namespace QingtingRadio
{
    /// <summary>
    /// 提供特定于应用程序的行为，以补充默认的应用程序类。
    /// </summary>
    sealed partial class App : Application
    {
        public App()
        {
            this.InitializeComponent();
            this.Suspending += OnSuspending;

            // 未处理异常只记录日志、保住进程；否则任何一处异常都会直接杀掉应用，
            // 用户重新打开就回到首页，且无从排查
            this.UnhandledException += App_UnhandledException;

            this.EnteredBackground += App_EnteredBackground;
            this.LeavingBackground += App_LeavingBackground;
        }

        private void App_UnhandledException(object sender, Windows.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            Debug.WriteLine("unhandled exception: " + e.Message);
            LogManager.LogAsync("unhandled exception", e.Exception);
            e.Handled = true;
        }

        /// <summary>
        /// 在应用程序由最终用户正常启动时进行调用。
        /// 将在启动应用程序以打开特定文件等情况下使用。
        /// </summary>
        /// <param name="e">有关启动请求和过程的详细信息。</param>
        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {

            Frame rootFrame = Window.Current.Content as Frame;

            // 不要在窗口已包含内容时重复应用程序初始化，
            // 只需确保窗口处于活动状态
            if (rootFrame == null)
            {
                // 创建要充当导航上下文的框架，并导航到第一页
                rootFrame = new Frame();

                rootFrame.NavigationFailed += OnNavigationFailed;

                string lastPage = null;
                if (e.PreviousExecutionState == ApplicationExecutionState.Terminated)
                {
                    // 应用被系统终止后重启：恢复上次停留的页面，而不是回到首页
                    lastPage = ApplicationData.Current.LocalSettings.Values["LastPage"] as string;
                }

                _ = AppHelper.RegisterToast();

                // 将框架放在当前窗口中
                Window.Current.Content = rootFrame;

                if (string.IsNullOrEmpty(lastPage))
                {
                    rootFrame.Navigate(typeof(MainPage), e.Arguments);
                }
                else
                {
                    // 参数带上要恢复的页面类型名，由 MainPage 完成实际导航
                    rootFrame.Navigate(typeof(MainPage), lastPage);
                }
                //    确保当前窗口处于活动状态
                Window.Current.Activate();
            }
            else if (e.PrelaunchActivated == false)
            {
                if (rootFrame.Content == null)
                {
                    // 当导航堆栈尚未还原时，导航到第一页，
                    // 并通过将所需信息作为导航参数传入来配置
                    // 参数
                    rootFrame.Navigate(typeof(MainPage), e.Arguments);
                }
                // 确保当前窗口处于活动状态
                Window.Current.Activate();
            }

        }

        /// <summary>
        /// 导航到特定页失败时调用
        /// </summary>
        ///<param name="sender">导航失败的框架</param>
        ///<param name="e">导航失败的详细信息</param>
        void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            // 记录后保持运行，不再抛异常杀掉进程
            LogManager.LogAsync("navigation failed to " + e.SourcePageType, e.Exception);
        }

        /// <summary>
        /// 在将要挂起应用程序执行时调用。  在不知道应用程序
        /// 无需知道应用程序会被终止还是会恢复，
        /// 并让内存内容保持不变。
        /// </summary>
        /// <param name="sender">挂起的请求的源。</param>
        /// <param name="e">有关挂起的详细信息。</param>
        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            Debug.WriteLine("app onsuspending............");
            SuspendingDeferral deferral = e.SuspendingOperation.GetDeferral();
            try
            {
                // 记住当前停留的页面，供应用被终止后重启时恢复
                Frame rootFrame = Window.Current?.Content as Frame;
                Frame contentFrame = (rootFrame?.Content as MainPage)?.ContentFrame;
                if (contentFrame?.Content != null)
                {
                    ApplicationData.Current.LocalSettings.Values["LastPage"] =
                        contentFrame.Content.GetType().FullName + ", QingtingRadio";
                }
            }
            catch (Exception ex)
            {
                LogManager.LogAsync("onsuspending save last page", ex);
            }
            finally
            {
                deferral.Complete();
            }
        }

        private void App_LeavingBackground(object sender, LeavingBackgroundEventArgs e)
        {
            _ = AppHelper.Current.AppLeavingHandle();
        }

        private void App_EnteredBackground(object sender, EnteredBackgroundEventArgs e)
        {
            AppHelper.Current.AppEnteredHandle();
        }

        protected override void OnActivated(IActivatedEventArgs args)
        {
            Frame rootFrame = Window.Current.Content as Frame;
            if (rootFrame == null)
            {
                rootFrame = new Frame();
                rootFrame.NavigationFailed += OnNavigationFailed;
                Window.Current.Content = rootFrame;
            }

            // 应用运行中通过协议/Toast 再次激活时，MainPage 已缓存复用，
            // 不能再 Navigate 一次（会在返回堆栈叠加条目），只需激活窗口
            if (!(rootFrame.Content is MainPage))
            {
                object parameter = null;
                if (args is ProtocolActivatedEventArgs protocolArgs)
                {
                    parameter = protocolArgs;
                }
                else if (args is ToastNotificationActivatedEventArgs toastArgs)
                {
                    parameter = toastArgs;
                }

                rootFrame.Navigate(typeof(MainPage), parameter);
            }

            Window.Current.Activate();
        }
    }
}
