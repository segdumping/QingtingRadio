using System;

namespace QingtingRadio.Tools
{
    /// <summary>
    /// 版本号比较：统一按 Major.Minor.Build 三段解析后比较，
    /// 避免 string.CompareTo 把 "1.1.9" 排在 "1.1.10" 之后、或 "1.1.25" 与 "1.1.25.0" 判不相等。
    /// 解析失败时退回字符串比较。
    /// </summary>
    public static class VersionHelper
    {
        public static int Compare(string versionA, string versionB)
        {
            Version a, b;
            if (Version.TryParse(versionA, out a) && Version.TryParse(versionB, out b))
            {
                return new Version(a.Major, a.Minor, Math.Max(a.Build, 0))
                    .CompareTo(new Version(b.Major, b.Minor, Math.Max(b.Build, 0)));
            }

            return string.Compare(versionA, versionB, StringComparison.Ordinal);
        }
    }
}
