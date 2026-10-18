using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace QingtingRadio
{
    /// <summary>
    /// 页面头部控件：标题内容 + 与汉堡按钮对齐的装饰线。
    /// </summary>
    public sealed partial class PageHeader : UserControl
    {
        public PageHeader()
        {
            this.InitializeComponent();

            this.Loaded += PageHeader_Loaded;
            this.Unloaded += PageHeader_Unloaded;
        }

        private void PageHeader_Loaded(object sender, RoutedEventArgs e)
        {
            // Loaded 可能随可视化树进出多次触发：先退订再订阅，避免监听叠加
            MainPage.Current.TogglePaneButtonRectChanged -= Current_TogglePaneButtonSizeChanged;
            MainPage.Current.TogglePaneButtonRectChanged += Current_TogglePaneButtonSizeChanged;
            this.headerLine.Margin = new Thickness(MainPage.Current.TogglePaneButtonRect.Right, 0, 0, 0);
        }

        private void PageHeader_Unloaded(object sender, RoutedEventArgs e)
        {
            MainPage.Current.TogglePaneButtonRectChanged -= Current_TogglePaneButtonSizeChanged;
        }

        private void Current_TogglePaneButtonSizeChanged(MainPage sender, Rect e)
        {
            this.headerLine.Margin = new Thickness(e.Right, 0, 0, 0);
        }

        public UIElement HeaderContent
        {
            get { return (UIElement)GetValue(HeaderContentProperty); }
            set { SetValue(HeaderContentProperty, value); }
        }

        public static readonly DependencyProperty HeaderContentProperty =
            DependencyProperty.Register("HeaderContent", typeof(UIElement), typeof(PageHeader), new PropertyMetadata(DependencyProperty.UnsetValue));
    }
}
