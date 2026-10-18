using QingtingRadio.ViewModels;
using System;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Media;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace QingtingRadio
{
    public sealed partial class NotifyUser : UserControl
    {
        private Popup m_Popup;

        private string m_TextBlockContent;
        private TimeSpan m_ShowTime;

        private NotifyUser()
        {
            this.InitializeComponent();
            m_Popup = new Popup();
            this.Width = ApplicationView.GetForCurrentView().VisibleBounds.Width;
            m_Popup.VerticalOffset = 0;
            m_Popup.Child = this;

            this.Loaded += NotifyUser_Loaded; ;
            this.Unloaded += NotifyUser_Unloaded; ;
        }

        public NotifyUser(string content, TimeSpan showTime) : this()
        {
            this.m_TextBlockContent = content;
            this.m_ShowTime = showTime;
        }

        public NotifyUser(string content) : this(content, TimeSpan.FromSeconds(2))
        {
        }

        public void Show()
        {
            this.m_Popup.IsOpen = true;
        }

        private void NotifyUser_Loaded(object sender, RoutedEventArgs e)
        {
            this.tbNotify.Text = m_TextBlockContent;
            this.sbOut.BeginTime = this.m_ShowTime;
            this.sbOut.Begin();
            this.sbOut.Completed += SbOut_Completed;
            Window.Current.SizeChanged += Current_SizeChanged; 
        }

        private void SbOut_Completed(object sender, object e)
        {
            this.m_Popup.IsOpen = false;
        }

        private void Current_SizeChanged(object sender, Windows.UI.Core.WindowSizeChangedEventArgs e)
        {
            this.Width = e.Size.Width;
            this.Height = e.Size.Height;
        }

        private void NotifyUser_Unloaded(object sender, RoutedEventArgs e)
        {
            Window.Current.SizeChanged -= Current_SizeChanged;
        }
    }
}
