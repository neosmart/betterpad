using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace betterpad
{
    class Win32
    {
        public struct COPYDATASTRUCT
        {
            public IntPtr dwData;
            public int cbData;
            [MarshalAs(UnmanagedType.LPStr)]
            public string message;
        }

        public static int WM_COPYDATA = 0x004A;

        public static int SendWindowsStringMessage(IntPtr hWnd, int wParam, string msg, IntPtr messageCode)
        {
            int result = 0;

            if (hWnd.ToInt32() > 0)
            {
                //only to get the actual length
                var msgBytes = Encoding.Default.GetBytes(msg);
                COPYDATASTRUCT cds = new COPYDATASTRUCT()
                {
                    dwData = messageCode,
                    message = msg,
                    cbData = msgBytes.Length + 1
                };
                result = SendMessage(hWnd, WM_COPYDATA, wParam, ref cds);
            }

            return result;
        }

        [DllImport("user32.dll", EntryPoint = "SendMessage", CharSet = CharSet.Auto)]
        public static extern int SendMessage(IntPtr hWnd, int wMsg, int wParam, ref COPYDATASTRUCT lParam);

        public static string ReceiveWindowMessage(Message msg)
        {
            if (msg.Msg != WM_COPYDATA)
            {
                return null;
            }

            var cds = (COPYDATASTRUCT)msg.GetLParam(typeof(COPYDATASTRUCT));
            return cds.message;
        }

        [DllImport("user32.dll")]
        public static extern bool SetProcessDPIAware();

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);
    }
}
