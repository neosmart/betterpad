using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Reflection;

namespace betterpad
{
    class UpdateManager
    {
        public VersionInfo GetLatestVersion(bool includeBeta = false)
        {
            try
            {
                var request = WebRequest.Create("https://api.neosmart.net/GetVersionInfo/1");
                using (var response = request.GetResponse())
                using (var stream = response.GetResponseStream())
                using (var reader = new StreamReader(stream))
                {
                    /*var serializer = new JavaScriptSerializer();
                    var versions = serializer.Deserialize<VersionInfo[]>(reader.ReadToEnd());*/

                    /*var serializer = new JsonParser();
                    var versions = serializer.Parse<VersionInfo[]>(reader.ReadToEnd());*/

                    var serializer = new DataContractJsonSerializer(typeof(VersionInfo[]));
                    var versions = (VersionInfo[])serializer.ReadObject(stream);

                    return versions.Where(v => includeBeta || v.Level == ReleaseLevel.Stable).Max();
                }
            }
            catch
            {
                return null;
            }
        }

        public bool UpdateAvailable(VersionInfo latest)
        {
            var thisInfo = Assembly.GetExecutingAssembly().GetName().Version;

            return latest.Version.Major > thisInfo.Major ||
                (latest.Version.Major == thisInfo.Major && latest.Version.Minor > thisInfo.Minor) ||
                (latest.Version.Major == thisInfo.Major && latest.Version.Minor == thisInfo.Minor && latest.Version.Build > thisInfo.Build);
        }
    }
}
