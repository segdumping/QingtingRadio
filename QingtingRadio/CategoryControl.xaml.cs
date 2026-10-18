using QingtingRadio.ViewModels;
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

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace QingtingRadio
{
    public sealed partial class CategoryControl : UserControl
    {
        public CategoryControl()
        {
            this.InitializeComponent();
        }

        public RadioCategoryViewModel ItemSource
        {
            set
            {
                ItemControl.ItemsSource = value;
            }
        }

        private void CategoryRadio_Click(object sender, RoutedEventArgs e)
        {
            String Channel = (sender as Button).Tag.ToString();
            String Name = (sender as Button).Content.ToString();
            if (Channel.Equals("-3"))
            {
                MainPage.Current.ContentFrame.Navigate(typeof(CustomRadios), new object[] { Name });
            }
            else
            {
                MainPage.Current.ContentFrame.Navigate(typeof(RadioList), new object[] { Channel, Name });
            }
        }

        private void PullToRefreshBox_RefreshInvoked(DependencyObject sender, object args)
        {
            RadioCategoryViewModel category = null;
            if (ItemControl.ItemsSource != null)
            {
                category = ItemControl.ItemsSource as RadioCategoryViewModel;
            }

            if (category != null)
            {
                category.DoRefresh();
            }
        }
    }
}
