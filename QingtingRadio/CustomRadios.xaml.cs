using Microsoft.Toolkit.Uwp.UI.Extensions;
using QingtingRadio.DataModels;
using QingtingRadio.Tools;
using QingtingRadio.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using Windows.Foundation.Metadata;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Navigation;

namespace QingtingRadio
{
    /// <summary>
    /// 自定义电台列表页：展示/播放/删除用户添加的电台。
    /// </summary>
    public sealed partial class CustomRadios : Page
    {
        ObservableCollection<CustomRadio> items;

        public CustomRadios()
        {
            this.InitializeComponent();
            // 单例事件在构造/析构成对订阅退订，避免 Loaded 多次触发时重复挂接或页面被单例持有。
            RadioSingleProperty.Current.PropertyChanged += Current_PropertyChanged;
            this.Unloaded += CustomRadios_Unloaded;
            CustomRadiosView.ItemsSource = items = new ObservableCollection<CustomRadio>();
        }

        private void CustomRadios_Unloaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            RadioSingleProperty.Current.PropertyChanged -= Current_PropertyChanged;
        }

        private void Current_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName.Equals("HasCustomRadio"))
            {
                LoadCustomRadios();
            }
        }

        private async void LoadCustomRadios()
        {
            RingCircle.IsActive = true;
            bool isSaveRequired = false;
            List<CustomRadio> tmp = await FileHelper.Current.ReadObjectAsync<List<CustomRadio>>("CustosRadios.json");
            if (tmp != null)
            {
                items.Clear();
                tmp.ForEach(c =>
                {
                    if (string.IsNullOrEmpty(c.Id))
                    {
                        c.Id = Math.Abs((c.Title + c.PlayUri).GetHashCode()).ToString();
                        isSaveRequired = true;
                    }
                    items.Add(c);
                });

                if (isSaveRequired)
                {
                    await FileHelper.Current.WriteObjectAsync<List<CustomRadio>>(tmp, "CustosRadios.json");
                }
            }
            RingCircle.IsActive = false;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            object[] parameters = e.Parameter as object[];
            LoadCustomRadios();

            TitleName.Text = parameters[0].ToString();
            
        }

        private async void CustomRadiosView_ItemClick(object sender, ItemClickEventArgs e)
        {
            CustomRadio item = e.ClickedItem as CustomRadio;
            RadioItem tmp = new RadioItem();
            if (tmp != null)
            {
                tmp.Id = item.Id;        
                tmp.Title = item.Title;
                tmp.ImageUri = "ms-appx:///Assets/recommend_defaultbg.png";
                tmp.NowPlaying = "未知";
                tmp.PlayCount = item.PlayUri;

                await RadioPlayViewModel.Instance.PlayRadioLive(tmp, true);
            }
        }

        private async void DeleteCustomRadio_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            CustomRadio item = ((FrameworkElement)e.OriginalSource).DataContext as CustomRadio;
            List<CustomRadio>  tmp = await FileHelper.Current.ReadObjectAsync<List<CustomRadio>>("CustosRadios.json");
            if (tmp != null)
            {
                int index = tmp.FindIndex(v => v.Title.Equals(item.Title));
                if (index >= 0) tmp.RemoveAt(index);
                CustomRadiosView.ItemsSource = tmp;

                await FileHelper.Current.WriteObjectAsync(tmp, "CustosRadios.json");
            }
        }

        private void ContextMenu_RightTapped(object sender, Windows.UI.Xaml.Input.RightTappedRoutedEventArgs e)
        {
            FrameworkElement senderElement = sender as FrameworkElement;
            FlyoutBase flyoutBase = FlyoutBase.GetAttachedFlyout(senderElement);

            flyoutBase.ShowAt(senderElement);
        }

        private void ContextMenu_Holding(object sender, Windows.UI.Xaml.Input.HoldingRoutedEventArgs e)
        {
            FrameworkElement senderElement = sender as FrameworkElement;
            FlyoutBase flyoutBase = FlyoutBase.GetAttachedFlyout(senderElement);

            flyoutBase.ShowAt(senderElement);
        }

        private async void AddRadio_Click(object sender, RoutedEventArgs e)
        {
            ContentDialog content_dialog = new ContentDialog()
            {
                Title = "自定义电台",
                Content = new CustomControl(),
                PrimaryButtonText = "保存",
                SecondaryButtonText = "取消",
                MaxHeight = 360,
                FullSizeDesired = false,
            };

            content_dialog.Style = (Style)Application.Current.Resources["CustomContent"];
            content_dialog.RequestedTheme = RadioSettingsViewModel.Instance.AppTheme ? ElementTheme.Dark : ElementTheme.Light;
            content_dialog.PrimaryButtonClick += (_s, _e) =>
            {
                CustomControl tmp = (_s.Content as CustomControl);

                tmp.SaveCustomRadio(tmp);
            };

            content_dialog.Loaded += (_s, _e) =>
            {
                if (ApiInformation.IsTypePresent("Windows.UI.ViewManagement.StatusBar"))
                {
                    StatusBar.SetForegroundColor(this, Colors.SlateGray);
                }
            };

            content_dialog.Unloaded += (_s, _e) =>
            {
                if (ApiInformation.IsTypePresent("Windows.UI.ViewManagement.StatusBar"))
                {
                    Color ground = RadioSettingsViewModel.Instance.AppTheme ? Color.FromArgb(255, 177, 177, 177) : Color.FromArgb(255, 77, 77, 77);
                    StatusBar.SetForegroundColor(this, ground);
                }
            };

            await content_dialog.ShowAsync();
        }

        private async void ImportRadio_Click(object sender, RoutedEventArgs e)
        {
            string msg = string.Empty;

            FileOpenPicker openPicker = new FileOpenPicker()
            {
                ViewMode = PickerViewMode.Thumbnail,
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            };
            openPicker.FileTypeFilter.Add(".json");
            StorageFile file = await openPicker.PickSingleFileAsync();
            if (file == null) return;

            try
            {
                StorageFolder folder = await ApplicationData.Current.LocalFolder.CreateFolderAsync("DatasCache", CreationCollisionOption.OpenIfExists);
                if (folder == null) return;
                await file.CopyAsync(folder, "CustomRadiosTemp.json");

                List<CustomRadio> originalCustom = await FileHelper.Current.ReadObjectAsync<List<CustomRadio>>("CustosRadios.json");
                if (originalCustom != null)
                {
                    List<CustomRadio> tmpCustom = await FileHelper.Current.ReadObjectAsync<List<CustomRadio>>("CustomRadiosTemp.json");
                    if (tmpCustom != null)
                    {
                        originalCustom.AddRange(tmpCustom);
                        HashSet<CustomRadio> set = new HashSet<CustomRadio>(originalCustom, new CustomCompare());
                        originalCustom.Clear();
                        originalCustom.AddRange(set);

                        await FileHelper.Current.WriteObjectAsync<List<CustomRadio>>(originalCustom, "CustosRadios.json");
                        await (await folder.GetFileAsync("CustomRadiosTemp.json")).DeleteAsync();
                    }
                }
                else
                {
                    StorageFile tmpFile = await folder.GetFileAsync("CustomRadiosTemp.json");
                    if (tmpFile != null)
                    {
                        await tmpFile.RenameAsync("CustosRadios.json");                       
                    }
                }

                LoadCustomRadios();
                msg = "导入成功";
            }
            catch(Exception ex)
            {
                Debug.WriteLine(ex.ToString());
                LogManager.LogAsync("import radio click", ex);
                msg = "导入失败";
            }
            finally
            {
                NotifyUser notifyPopup = new NotifyUser(msg);
                notifyPopup.Show();
            }
        }

        private async void ExportRadio_Click(object sender, RoutedEventArgs e)
        {
            string msg = string.Empty;

            FileSavePicker savePicker = new FileSavePicker()
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                SuggestedFileName = "自定义电台",
            };
            savePicker.FileTypeChoices.Add("JSON文件", new List<string>() { ".json" });
            StorageFile file = await savePicker.PickSaveFileAsync();
            if (file == null) return;
           
            try
            {
                StorageFolder folder = await ApplicationData.Current.LocalFolder.CreateFolderAsync("DatasCache", CreationCollisionOption.OpenIfExists);
                if (folder == null) return;
                StorageFile tmpFile = await folder.GetFileAsync("CustosRadios.json");
                if (tmpFile == null) return;

                await tmpFile.CopyAndReplaceAsync(file);

                msg = "导出成功";
            } 
            catch(Exception ex)
            {
                LogManager.LogAsync("export radio click", ex);
                msg = "导出失败";
            }
            finally
            {
                NotifyUser notifyPopup = new NotifyUser(msg);
                notifyPopup.Show();
            }
        }
    }

    /// <summary>
    /// 去重比较器：按标题判断自定义电台是否相同。
    /// </summary>
    class CustomCompare : IEqualityComparer<CustomRadio>
    {
        public bool Equals(CustomRadio x, CustomRadio y)
        {
            if (x.Title == y.Title)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public int GetHashCode(CustomRadio item)
        {
            if(item == null) return 0;
            else
            {
                return (item.Title + item.PlayUri).GetHashCode();
            } 
        }
    }
}