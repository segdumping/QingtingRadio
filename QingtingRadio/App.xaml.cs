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

        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {

            Frame rootFrame = Window.Current.Content as Frame;

            if (rootFrame == null)
            {
                rootFrame = new Frame();

                rootFrame.NavigationFailed += OnNavigationFailed;

                string lastPage = null;
                if (e.PreviousExecutionState == ApplicationExecutionState.Terminated)
                {
                    // 应用被系统终止后重启：恢复上次停留的页面，而不是回到首页
                    lastPage = ApplicationData.Current.LocalSettings.Values["LastPage"] as string;
                }

                _ = AppHelper.RegisterToast();

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
                Window.Current.Activate();
            }
            else if (e.PrelaunchActivated == false)
            {
                if (rootFrame.Content == null)
                {
                    rootFrame.Navigate(typeof(MainPage), e.Arguments);
                }
                Window.Current.Activate();
            }

        }

        void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            // 记录后保持运行，不再抛异常杀掉进程
            LogManager.LogAsync("navigation failed to " + e.SourcePageType, e.Exception);
        }

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
