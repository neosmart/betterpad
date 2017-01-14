using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
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
        static void Main()
        {
            using (var mutex = new Mutex(false, "{99EC16DC-9097-4931-BFF2-869E15A17AB4}"))
            {
                if (!mutex.WaitOne(0))
                {
                    //Another session exists
                    BringToForeground();
                    return;
                }
                if (Environment.OSVersion.Version.Major >= 6)
                {
                    SetProcessDPIAware();
                }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                new WindowManager();
            }
        }

        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        private static void BringToForeground()
        {
            int pid = 0;
            using (var thisProcess = Process.GetCurrentProcess())
            {
                pid = thisProcess.Id;
            }

            var processes = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(Application.ExecutablePath));
            foreach (var process in processes)
            {
                if (process.Id == pid)
                {
                    //just us, keep going
                    continue;
                }

                SetForegroundWindow(process.MainWindowHandle);
            }

            foreach (var p in processes)
            {
                p.Dispose();
            }
        }
    }
}
