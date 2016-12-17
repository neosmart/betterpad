using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace betterpad
{
    public partial class Form1 : Form
    {
        private static Dictionary<Keys, Action> _shortcuts;
        private readonly MemoryMappedFile _mmap = MemoryMappedFile.CreateOrOpen("{6472DD80-A7A5-4F44-BAD4-69BB7F9580DE}", 32);
        private int _documentNumber;
        private string _path;

        public Form1()
        {
            InitializeComponent();
            InitializeShortcuts();
            InitializeLayout();
            GetDocumentNumber();
            SetTitle($"Untitled {_documentNumber}");
        }

        private void GetDocumentNumber()
        {
            //purposely not disposing the mmap
            using (new ScopedMutex(true, "{8BED64DE-A2F9-408F-A223-92EDAD8D90E8}"))
            using (var view = _mmap.CreateViewAccessor(0, 32))
            {
                _documentNumber = view.ReadInt32(0) + 1;
                view.Write(0, _documentNumber);
            }
        }

        private void InitializeShortcuts()
        {
            _shortcuts = new Dictionary<Keys, Action>
            {
                { Keys.Control | Keys.C, Copy },
                { Keys.Control | Keys.V, Paste },
                { Keys.Control | Keys.X, Cut },
                { Keys.Control | Keys.F, Find },
                { Keys.Control | Keys.O, Open },
                { Keys.Control | Keys.S, Save },
            };
        }

        private void InitializeLayout()
        {
            text.Padding = new Padding(12, 10, 12, 10);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (_shortcuts.TryGetValue(keyData, out var action))
            {
                action();
                return true;
            }

            // Call the base class
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void Find()
        {
            var finder = new FindDialog();
            finder.ShowDialog(this);
        }

        private void Paste()
        {
            //var data = Clipboard.GetText(TextDataFormat.UnicodeText);
            text.Paste(DataFormats.GetFormat(DataFormats.UnicodeText));
        }

        private void Copy()
        {
            text.Copy();
        }

        private void Cut()
        {
            text.Cut();
        }

        private void Open()
        {
            var dialog = new OpenFileDialog()
            {
                AutoUpgradeEnabled = true,
                CheckFileExists = true,
                CheckPathExists = true,
                DefaultExt = "txt",
                Filter = "Text Files (*.txt)|*.txt|Log Files (*.log)|*.log",
                Multiselect = false,
                RestoreDirectory = true,
                ShowReadOnly = true,
                ValidateNames = true,
                Title = "Open file"
            };

            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                Open(dialog.FileName);
            }
        }

        private void Open(string path)
        {
            var dir = Path.GetDirectoryName(path);
            if (!Directory.Exists(dir))
            {
                MessageBox.Show(this, $"Could not find a part of the path {dir}!", "Directory not found!",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            _path = path;
            SetTitle(Path.GetFileName(path));

            if (!File.Exists(path))
            {
                //Don't load it, but we will save this reference because we'll write to it
                return;
            }

            var data = File.ReadAllText(path);
            text.Text = data;
        }

        private void Save()
        {
            if (string.IsNullOrEmpty(_path))
            {
                var dialog = new SaveFileDialog()
                {
                    AddExtension = true,
                    AutoUpgradeEnabled = true,
                    CheckFileExists = false,
                    CheckPathExists = true,
                    DefaultExt = "txt",
                    RestoreDirectory = true,
                    Title = "Save file",
                    Filter = "Text Files (*.txt)|*.txt|Log Files (*.log)|*.log"
                };

                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    _path = dialog.FileName;
                    Save(_path);
                }
            }
        }

        private void Save(string path)
        {
            File.WriteAllText(path, text.Text, Encoding.UTF8);
        }

        private void SetTitle(string document)
        {
            Text = $"{document} - Betterpad by NeoSmart Technologies";
        }
    }
}
