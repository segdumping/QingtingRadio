using System;

namespace QingtingRadio.Tools
{
    /// <summary>
    /// 版本号比较：统一按 Major.Minor.Build 三段解析后比较，
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
