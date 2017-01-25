using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Serialization.Json;
using System.IO;

namespace betterpad
{
    [Serializable]
    internal class Preferences
    {
        public string FontFamily;
        public double FontSize;
        public bool WordWrap;
        public int Width;
        public int Height;

        public static Preferences Load()
        {
            var prefPath = Environment.ExpandEnvironmentVariables("%localappdata%\\NeoSmart Technologies\\betterpad\\preferences.js");
            if (!File.Exists(prefPath))
            {
                return null;
            }

            var serializer = new DataContractJsonSerializer(typeof(Preferences));
            using (var fstream = new FileStream(prefPath, System.IO.FileMode.Open))
            {
                return serializer.ReadObject(fstream) as Preferences;
            }
        }

        public void Save()
        {
            var prefPath = Environment.ExpandEnvironmentVariables("%localappdata%\\NeoSmart Technologies\\betterpad\\preferences.js");
            if (File.Exists(prefPath))
            {
                File.Delete(prefPath);
            }
            if (!Directory.Exists(Path.GetDirectoryName(prefPath)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(prefPath));
            }

            var serializer = new DataContractJsonSerializer(typeof(Preferences));
            using (var mutex = new ScopedMutex("{F995FD2B-04EC-4410-AB57-9B4E6FF4B2B2}"))
            using (var fstream = new FileStream(prefPath, System.IO.FileMode.Create))
            {
                mutex.WaitOne();
                serializer.WriteObject(fstream, this);
            }
        }
    }
}
