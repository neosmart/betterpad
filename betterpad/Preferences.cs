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
        public string FontFamily = "Consolas";
        public float FontSize = 11;
        public bool WordWrap = true;
        public int Width = 1024;
        public int Height = 800;

        private static string _path => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NeoSmart Technologies", "betterpad", "preferences.js");

        public static Preferences Load()
        {
            if (!File.Exists(_path))
            {
                return new Preferences();
            }

            try
            {
                var serializer = new DataContractJsonSerializer(typeof(Preferences));
                using (var fstream = new FileStream(_path, FileMode.Open))
                {
                    return serializer.ReadObject(fstream) as Preferences;
                }
            }
            catch
            {
                return new Preferences();
            }
        }

        public void Save()
        {
            if (File.Exists(_path))
            {
                File.Delete(_path);
            }
            if (!Directory.Exists(Path.GetDirectoryName(_path)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path));
            }

            var serializer = new DataContractJsonSerializer(typeof(Preferences));
            using (var mutex = new ScopedMutex("{F995FD2B-04EC-4410-AB57-9B4E6FF4B2B2}"))
            using (var fstream = new FileStream(_path, FileMode.Create))
            {
                mutex.WaitOne();
                serializer.WriteObject(fstream, this);
            }
        }
    }
}
