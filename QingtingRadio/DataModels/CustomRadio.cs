using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Data.Json;

namespace QingtingRadio.DataModels
{
    public class CustomRadio : MusicItem
    {
        public string PlayUri { get; set; }

        public CustomRadio() { }

        public CustomRadio(JsonObject json) : base(json, AudioType.None)
        {

        }
    }
}
