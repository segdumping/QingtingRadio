using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Data.Json;

namespace QingtingRadio.DataModels
{
    /// <summary>
    /// 用户添加的自定义电台：播放地址即流 URL。
    /// </summary>
    public class CustomRadio : MusicItem
    {
        public string PlayUri { get; set; }

        public CustomRadio() { }

        public CustomRadio(JsonObject json) : base(json, AudioType.None)
        {

        }
    }
}