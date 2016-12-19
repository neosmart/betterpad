using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace betterpad
{
    static class Program
    {
        public static bool Restart;
        public static Size WindowSize { get; set; }
        public static Point WindowLocation { get; set; }

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            if (Environment.OSVersion.Version.Major >= 6)
            {
                SetProcessDPIAware();
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            do
            {
                var newWindow = new Form1();
                if (Restart)
                {
                    newWindow.StartPosition = FormStartPosition.Manual;
                    newWindow.Location = WindowLocation;
                    newWindow.Size = WindowSize;
                }
                Restart = false;
                Application.Run(newWindow);
            } while (Restart);
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();
    }
}
