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
using System.IO;

namespace betterpad
{
    public partial class PaddedRichTextBox : TextBox
    {
        private Padding _padding = Padding.Empty;
        private byte[] _lastBytesCompressed;
        private Encoding _encoding = new UTF8Encoding(false);
        private byte[] _utfBytes => _encoding.GetBytes(Text);
        private Stack<byte[]> _undoBuffer = new Stack<byte[]>();
        private Stack<byte[]> _discardedUndos = new Stack<byte[]>(); //stores undos popped from undo buffer before a redo is made
        private Stack<byte[]> _redoBuffer = new Stack<byte[]>();
        private string _lastText => _lastBytesCompressed != null ? _encoding.GetString(Lz4Net.Lz4.DecompressBytes(_lastBytesCompressed)) : string.Empty;

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
            MaxLength = int.MaxValue;
            Multiline = true;

            //to-do: handle delete/backspace
            //store undos prior to paste and word/line breaks
            KeyDown += (sender, args) =>
            {
                if (args.KeyData == Keys.Space || args.KeyData == Keys.Enter || args.KeyData == Keys.Tab)
                {
                    CreateUndo();
                }
            };

            //clear redo chain on text changed
            TextChanged += ClearRedoHandler;
            CreateUndo();
        }

        private void ClearRedoHandler(object sender, EventArgs e)
        {
            ClearRedoTree();
        }

        public new void Paste()
        {
            CreateUndo();
            base.Paste();
        }

        private void ClearRedoTree()
        {
            if (_discardedUndos.Any())
            _discardedUndos.Clear();
            if (_redoBuffer.Any())
            _redoBuffer.Clear();
        }

        //Generates a bsdiff to transform _current_ text to _previous_ text
        private void CreateUndo()
        {
            var lastBytes = _lastBytesCompressed != null ? Lz4Net.Lz4.DecompressBytes(_lastBytesCompressed) : new byte[0];
            var utfBytes = _utfBytes;
            using (var diffStream = new MemoryStream())
            {
                BsDiff.BinaryPatchUtility.Create(_utfBytes, lastBytes, diffStream);
                diffStream.Seek(0, SeekOrigin.Begin);
                var diffBytes = diffStream.ToArray();
                _undoBuffer.Push(diffBytes);
            }
            _lastBytesCompressed = utfBytes.Any() ? Lz4Net.Lz4.CompressBytes(utfBytes) : null;
        }

        //Generates a bsdiff to transform _previous_ text to _current_ text
        private void CreateRedo()
        {
            var lastBytes = _lastBytesCompressed != null ? Lz4Net.Lz4.DecompressBytes(_lastBytesCompressed) : new byte[0];
            var utfBytes = _utfBytes;
            using (var diffStream = new MemoryStream())
            {
                BsDiff.BinaryPatchUtility.Create(lastBytes, utfBytes, diffStream);
                diffStream.Seek(0, SeekOrigin.Begin);
                var diffBytes = diffStream.ToArray();
                _redoBuffer.Push(diffBytes);
            }
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

        public void SetPadding(Control textBox, Padding padding)
        {
            var rect = new Rectangle(padding.Left, padding.Top, textBox.ClientSize.Width - padding.Left - padding.Right,
                textBox.ClientSize.Height - padding.Top - padding.Bottom);
            RECT rc = new RECT(rect);
            SendMessageRefRect(Handle, EM_SETRECT, 0, ref rc);
        }

        public new bool CanUndo
        {
            get => _undoBuffer.Any();
        }

        public bool CanRedo
        {
            get => _redoBuffer.Any();
        }

        public new void Undo()
        {
            if (!_undoBuffer.Any())
            {
                return;
            }

            //temporarily unsubscribe from the redo clear handler
            TextChanged -= ClearRedoHandler;

            //We already have the result as a (compressed) string, but we need to
            //figure out what the new last result will become
            CreateRedo();
            var result = _lastBytesCompressed != null ? Lz4Net.Lz4.DecompressBytes(_lastBytesCompressed) : new byte[0];
            var patch = _undoBuffer.Pop();
            _discardedUndos.Push(patch);
            using (var currentLastStream = new MemoryStream(result))
            using (var newLastStream = new MemoryStream())
            {
                BsDiff.BinaryPatchUtility.Apply(currentLastStream, () => new MemoryStream(patch), newLastStream);
                var newLastBytes = newLastStream.ToArray();
                _lastBytesCompressed = newLastBytes.Any() ? Lz4Net.Lz4.CompressBytes(newLastBytes) : null;
            }
            Text = _encoding.GetString(result);
            SelectionLength = 0;
            SelectionStart = Text.Length;

            //re-subscribe to clear redo tree handler
            TextChanged += ClearRedoHandler;
        }

        public void Redo()
        {
            if (!_redoBuffer.Any())
            {
                return;
            }

            //temporarily unsubscribe from the redo clear handler
            TextChanged -= ClearRedoHandler;

            var utfBytes = _utfBytes;
            var patch = _redoBuffer.Pop();
            using (var utfStream = new MemoryStream(utfBytes))
            using (var newStream = new MemoryStream())
            {
                BsDiff.BinaryPatchUtility.Apply(utfStream, () => new MemoryStream(patch), newStream);
                var newBytes = newStream.ToArray();
                _lastBytesCompressed = Lz4Net.Lz4.CompressBytes(utfBytes);
                Text = _encoding.GetString(newBytes);
            }

            //Now we need to create an undo point, unless a saved one already exists
            if (_discardedUndos.Any())
            {
                _undoBuffer.Push(_discardedUndos.Pop());
            }
            else
            {
                CreateUndo();
            }

            //re-subscribe to clear redo tree handler
            TextChanged += ClearRedoHandler;
        }
    }
}
