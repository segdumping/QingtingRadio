using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QingtingRadio
{
    /// <summary>数据开始加载（一般用于显示加载圈）。</summary>
    public delegate void DataLoadingEventHandler();
    /// <summary>数据加载完成（一般用于隐藏加载圈）。</summary>
    public delegate void DataLoadedEventHandler();
}
