using QingtingRadio.DataModels;
using QingtingRadio.Tools;
using QingtingRadio.ViewModels;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace QingtingRadio
{
    public sealed partial class HistoryPage : Page
    {
        private RadioSettingsViewModel _settings = RadioSettingsViewModel.Instance;

        public HistoryPage()
        {
            this.InitializeComponent();
            this.Loaded += HistoryPage_Loaded;
            this.Unloaded += HistoryRadios_Unloaded;

            NavigationCacheMode = NavigationCacheMode.Required;
        }

        private void HistoryPage_Loaded(object sender, RoutedEventArgs e)
        {
            _settings_PropertyChanged(null, new PropertyChangedEventArgs("AppTheme"));
            _settings.PropertyChanged += _settings_PropertyChanged;
        }
        

        private void HistoryRadios_Unloaded(object sender, RoutedEventArgs e)
        {
            _settings.PropertyChanged -= _settings_PropertyChanged;
            //   GC.Collect();
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

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.NavigationMode == NavigationMode.New)
            {
                LoadHistoryRadios();
            }
        }

        private async void LoadHistoryRadios()
        {
            RingCircle.IsActive = true;
            List<LastestPlay> items = await LastestPlay.LoadHistoryAsync();
            if(items != null)
            {
                try
                {
                    items.Sort((v1, v2) => (DateTime.Parse(v2.StartTime).CompareTo(DateTime.Parse(v1.StartTime))));
                    HistoryRadiosView.ItemsSource = items;
                }
                catch (Exception ex)
                {
                    LogManager.LogAsync("sort history radios", ex);
                }
            }
            RingCircle.IsActive = false;
        }


        private async void HistoryRadiosView_ItemClick(object sender, ItemClickEventArgs e)
        {                                  
            if(e.ClickedItem is LastestPlay tmp)
            {
                List<LastestPlay> history = await LastestPlay.LoadHistoryAsync();
                if (history != null)
                {
                    int index = history.FindIndex(v1 => v1.Id == tmp.Id);
                    if (index >= 0)
                    {
                        tmp = history[index];
                    }
                }               

                await RadioPlayViewModel.Instance.PlayHistoryItem(tmp);
            }               
        }

        private void Grid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // 预留滚动条 24px，再均分列数并扣除每张卡片的 Margin 8 + 边框 2 + 内边距 24
            double listWidth = e.NewSize.Width - 24;
            int column = (int)(listWidth / 330);
            if (column < 1) column = 1;
            width.Width = listWidth / column - 34;
        }

        private async void DeleteAll_Click(object sender, RoutedEventArgs e)
        {
            await LastestPlay.SaveHistoryAsync(new List<LastestPlay>());
            LoadHistoryRadios();
        }
    }
}
