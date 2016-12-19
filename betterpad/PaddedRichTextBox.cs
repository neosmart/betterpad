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
    public partial class PaddedRichTextBox : RichTextBox
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

        public PaddedRichTextBox()
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
    }
}
