using System;
using System.Reflection;

namespace TwoTo1Screen.Services
{
    /// <summary>Static app metadata (version, etc.).</summary>
    internal static class AppInfo
    {
        /// <summary>Semantic version like "1.1.0" derived from the assembly version.</summary>
        public static string Version
        {
            get
            {
                try
                {
                    var v = Assembly.GetExecutingAssembly().GetName().Version;
                    return v == null ? "1.2.4" : $"{v.Major}.{v.Minor}.{v.Build}";
                }
                catch { return "1.2.4"; }
            }
        }

        /// <summary>Compare dotted numeric versions. Returns &lt;0,0,&gt;0.</summary>
        public static int Compare(string a, string b)
        {
            try
            {
                var pa = a.Split('.'); var pb = b.Split('.');
                int n = Math.Max(pa.Length, pb.Length);
                for (int i = 0; i < n; i++)
                {
                    int ai = i < pa.Length && int.TryParse(pa[i], out var x) ? x : 0;
                    int bi = i < pb.Length && int.TryParse(pb[i], out var y) ? y : 0;
                    if (ai != bi) return ai.CompareTo(bi);
                }
                return 0;
            }
            catch { return 0; }
        }
    }
}
