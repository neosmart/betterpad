using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace betterpad
{
    class Setup
    {
        /// <summary>
        /// Registers the executable path in the registry, allowing it to be run via its filename only in the run dialog
        /// </summary>
        /// <returns></returns>
        public bool RegisterAppPath()
        {
            var path = System.Reflection.Assembly.GetExecutingAssembly().Location;
            var app = Path.GetFileName(path);
            using (var hkcu = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64))
            using (var appPaths = hkcu.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\App Paths", true))
            {
                if (appPaths == null)
                {
                    return false;
                }

                using (var reg = appPaths.GetSubKeyNames().Contains(app) ? appPaths.OpenSubKey(app, true) : appPaths.CreateSubKey(app))
                {
                    reg.SetValue("", path);
                    reg.SetValue("Path", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
                }
            }

            return true;
        }
    }
}
