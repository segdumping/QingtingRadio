using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QingtingRadio.DataModels
{
    /// <summary>
    /// IP 定位结果：用户所在地区名与对应电台 ID。
    /// </summary>
    public class LocalRadio
    {
        public string RegionName { get; set; }
        public string RadioID { get; set; }
    }
}