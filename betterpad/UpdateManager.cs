using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Serialization.Json;
using System.Threading.Tasks;
using System.Reflection;

namespace betterpad
{
    class UpdateManager
    {
        public async Task<VersionInfo> GetLatestVersionAsync(bool includeBeta = false)
        {
            try
            {
                var request = WebRequest.Create("https://api.neosmart.net/GetVersionInfo/25");
                using (var response = await request.GetResponseAsync())
                using (var stream = response.GetResponseStream())
                // DataContractJsonSerializer does not have a ReadObjectAsync, so we'll stream it to a new MemoryStream
                using (var memStream = new MemoryStream())
                using (var reader = new StreamReader(memStream))
                {
                    await stream.CopyToAsync(memStream);
                    memStream.Seek(0, SeekOrigin.Begin);

                    var serializer = new DataContractJsonSerializer(typeof(VersionInfo[]));
                    var versions = (VersionInfo[])serializer.ReadObject(memStream);

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
