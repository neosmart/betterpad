using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace betterpad
{
    class Win32
    {
        public struct COPYDATASTRUCT
        {
            public IntPtr Data;
            public int Length;
            [MarshalAs(UnmanagedType.LPStr)]
            public string Messag;
        }

        public static int WM_COPYDATA = 0x004A;

        public static int SendWindowsStringMessage(IntPtr hWnd, int wParam, string msg, IntPtr messageCode)
        {
            int result = 0;

            if (hWnd != IntPtr.Zero)
            {
                var msgByteCount = Encoding.Default.GetByteCount(msg);
                COPYDATASTRUCT cds = new COPYDATASTRUCT()
                {
                    Data = messageCode,
                    Messag = msg,
                    Length = msgByteCount + 1
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
            return cds.Messag;
        }

        [DllImport("user32.dll")]
        public static extern bool SetProcessDPIAware();

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);
    }
}
