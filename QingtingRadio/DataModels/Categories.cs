using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.UI.Core;

namespace QingtingRadio.DataModels
{
    /// <summary>
    /// 分类条目（ID + 标题）：电台分类、地区列表、央广直达电台共用。
    /// </summary>
    public class Categories
    {
        public string ID { get; set; }
        public string Title { get; set; }
    }
}