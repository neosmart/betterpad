using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace betterpad
{
    public partial class BetterRichTextBox : RichTextBox
    {
        private Padding _padding = Padding.Empty;

        public new Padding Padding
        {
            get { return _padding; }
            set
            {
                _padding = value;
                SetPadding(this, _padding);
            }
        }

        public BetterRichTextBox()
        {
            InitializeComponent();
            Multiline = true;
        }

        public sealed override bool Multiline
        {
            get { return base.Multiline; }
            set { base.Multiline = true; }
        }

        public bool TextSelected => SelectionLength > 0;

        [DllImport(@"User32.dll", EntryPoint = @"SendMessage", CharSet = CharSet.Auto)]
        private static extern int SendMessageRefRect(IntPtr hWnd, uint msg, int wParam, ref RECT rect);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public readonly int Left;
            public readonly int Top;
            public readonly int Right;
            public readonly int Bottom;

            private RECT(int left, int top, int right, int bottom)
            {
                Left = left;
                Top = top;
                Right = right;
                Bottom = bottom;
            }

            public RECT(Rectangle r) : this(r.Left, r.Top, r.Right, r.Bottom)
            {
            }
        }

        private const int EM_SETRECT = 0xB3;

        public void SetPadding(RichTextBox textBox, Padding padding)
        {
            var rect = new Rectangle(padding.Left, padding.Top, textBox.ClientSize.Width - padding.Left - padding.Right,
                textBox.ClientSize.Height - padding.Top - padding.Bottom);
            RECT rc = new RECT(rect);
            SendMessageRefRect(Handle, EM_SETRECT, 0, ref rc);
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        static extern IntPtr LoadLibrary(string lpFileName);

        protected override CreateParams CreateParams
        {
            get
            {
                LoadLibrary("MsftEdit.dll");

                //Use newer versions of the RTF control
                //Fixes a lot of bugs, such as http://stackoverflow.com/q/41233421/17027
                //A list of versions and their DLL paths can be found at https://github.com/dpradov/keynote-nf/issues/530
                CreateParams createParams = base.CreateParams;
                createParams.ClassName = "RichEdit50W";

                return createParams;
            }
        }

        //Uncomment to replace smooth scrolling with fixed 3-line scroll
#if false
        [DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int wMsg, int wParam, int lParam);

        const int NULL = 0x00;
        //Scroll and wheel messages
        const int WM_HSCROLL = 0x0114;
        const int WM_VSCROLL = 0x0115;
        const int WM_MOUSEWHEEL = 0x020A;

        //Scrollbar commands
        const int SB_LINEUP = 0;
        const int SB_LINELEFT = 0;
        const int SB_LINEDOWN = 1;
        const int SB_LINERIGHT = 1;
        const int SB_PAGEUP = 2;
        const int SB_PAGELEFT = 2;
        const int SB_PAGEDOWN = 3;
        const int SB_PAGERIGHT = 3;
        const int SB_THUMBPOSITION = 4;
        const int SB_THUMBTRACK = 5;
        const int SB_TOP = 6;
        const int SB_LEFT = 6;
        const int SB_BOTTOM = 7;
        const int SB_RIGHT = 7;
        const int SB_ENDSCROLL = 8;
        static short GET_WHEEL_DELTA_WPARAM(IntPtr wParam)
        {
            return (short)((int)wParam >> 16);
        }
        protected override void WndProc(ref Message m)
        {
            switch(m.Msg)
            {
                case WM_MOUSEWHEEL:
                {
                        if (GET_WHEEL_DELTA_WPARAM(m.WParam) > 0) // A positive value indicates that the wheel was rotated forward, away from the user;
                        {
                            SendMessage(Handle, WM_VSCROLL, SB_LINEUP, NULL);
                            SendMessage(Handle, WM_VSCROLL, SB_LINEUP, NULL);
                            SendMessage(Handle, WM_VSCROLL, SB_LINEUP, NULL);
                        }
                        else if (GET_WHEEL_DELTA_WPARAM(m.WParam) < 0) //A negative value indicates that the wheel was rotated backward, toward the user.
                        {
                            SendMessage(Handle, WM_VSCROLL, SB_LINEDOWN, NULL);
                            SendMessage(Handle, WM_VSCROLL, SB_LINEDOWN, NULL);
                            SendMessage(Handle, WM_VSCROLL, SB_LINEDOWN, NULL);
                        }
                        //return TRUE;  //block the message
                        return;
                }
            }

            base.WndProc(ref m);
        }
#endif
    }
}
