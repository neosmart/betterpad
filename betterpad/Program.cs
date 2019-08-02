using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace betterpad
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static async Task Main()
        {
            using (var mutex = new Mutex(false, "{99EC16DC-9097-4931-BFF2-869E15A17AB4}"))
            {
                if (!mutex.WaitOne(0))
                {
                    // Another session exists
                    BringToForeground(Environment.GetCommandLineArgs().Skip(1));
                    return;
                }

                // Register application path in HKCU registry
                var setup = new Setup();
                setup.RegisterAppPath();

                if (Environment.OSVersion.Version.Major >= 6)
                {
                    Win32.SetProcessDPIAware();
                }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                var wm = new WindowManager();
                await wm.StartAsync();
            }
        }

        private static void BringToForeground(IEnumerable<string> paths = null)
        {
            int pid = 0;
            using (var thisProcess = Process.GetCurrentProcess())
            {
                pid = thisProcess.Id;
            }

            var processes = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(Application.ExecutablePath));
            try
            {
                foreach (var process in processes)
                {
                    if (process.Id == pid)
                    {
                        // Just us, keep going
                        continue;
                    }

                    Win32.SetForegroundWindow(process.MainWindowHandle);
                    if (paths != null)
                    {
                        foreach (var path in paths)
                        {
                            var fullPath = Path.GetFullPath(path);
                            Win32.SendWindowsStringMessage(process.MainWindowHandle, 0, fullPath, IntPtr.Zero);
                        }
                    }
                }
            }
            finally
            {
                foreach (var p in processes)
                {
                    p.Dispose();
                }
            }
        }
    }
}
