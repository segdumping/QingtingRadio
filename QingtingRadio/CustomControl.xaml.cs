using QingtingRadio.DataModels;
using QingtingRadio.Tools;
using QingtingRadio.ViewModels;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Windows.UI.Core;
using Windows.UI.Xaml.Controls;

namespace QingtingRadio
{
    /// <summary>
    /// 添加自定义电台的输入控件：校验标题与流地址后写入本地列表。
    /// </summary>
    public sealed partial class CustomControl : UserControl
    {
        public CustomControl()
        {
            this.InitializeComponent();
        }

        public async void SaveCustomRadio(CustomControl c)
        {
            // 同时接受 http:// 与 https://（流媒体几乎都是 https，旧代码只放行 http 导致加不进来）
            string reg = @"^https?://(.*?)";
            if (Regex.IsMatch(c.PlayUri.Text, reg))
            {
                CustomRadio item = new CustomRadio()
                {
                    Title = c.Title.Text,
                    PlayUri = c.PlayUri.Text,
                    Id = Math.Abs((c.Title.Text + c.PlayUri.Text).GetHashCode()).ToString(),
                };

                List<CustomRadio> tmp = null;
                tmp = await FileHelper.Current.ReadObjectAsync<List<CustomRadio>>("CustosRadios.json");
                if (tmp == null)
                {
                    tmp = new List<CustomRadio>();
                }
                tmp.Add(item);
                await FileHelper.Current.WriteObjectAsync<List<CustomRadio>>(tmp, "CustosRadios.json");
                RadioSingleProperty.Current.HasCustomRadio = true;
            }
            else
            {
                NotifyUser notifyPopup = new NotifyUser("输入的网址格式为：http:// 或 https://");
                notifyPopup.Show();
            }
        }

        private async void Title_TextChanged(object sender, TextChangedEventArgs e)
        {
            List<CustomRadio> tmp = await FileHelper.Current.ReadObjectAsync<List<CustomRadio>>("CustosRadios.json");
            if (tmp != null)
            {
                TextBox testBox = sender as TextBox;

                int index = tmp.FindIndex(v => v.Title.Equals(testBox.Text));
                if (index >= 0)
                {
                    NotifyUser notifyPopup = new NotifyUser(testBox.Text + "已存在");
                    notifyPopup.Show();
                }
            }
        }
    }
}