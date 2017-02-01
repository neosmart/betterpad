using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace betterpad
{
    internal enum ReleaseLevel
    {
        Alpha,
        Beta,
        RC,
        Stable
    };

    [Serializable]
    internal class VersionInfo
    {
        public DateTime ReleaseDate;
        public ReleaseLevel Level;
        public BasicVersion Version;
        public string InfoUrl;
        public string DownloadUrl;
    }

    [Serializable]
    internal class BasicVersion
    {
        public int Major;
        public int Minor;
        public int Build;
        public int Revision;
    }
}
