using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
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
        private Dictionary<Keys, Action> _shortcuts;
        private const int MmapSize = 32;
        private readonly MemoryMappedFile _mmap = MemoryMappedFile.CreateOrOpen("{6472DD80-A7A5-4F44-BAD4-69BB7F9580DE}", MmapSize);
        private int _documentNumber;
        private string _path;
        private bool _ignoreChanges = false;
        private string _processPath;
        private Stack<byte[]> UndoStack = new Stack<byte[]>();

        private unsafe byte[] DocumentHash
        {
            get
            {
                var bytes = new byte[sizeof(char) * text.Text.Length];
                fixed (void* ptr = text.Text)
                {
                    System.Runtime.InteropServices.Marshal.Copy(new IntPtr(ptr), bytes, 0, bytes.Length);
                }

                MetroHash.MetroHash.Hash64_1(bytes, 0, (uint) bytes.Length, 0, out var hash);
                return hash;
            }
        }

        public Action<Form1> StartAction { get; internal set; }

        private byte[] _lastHash;

        public Form1()
        {
            InitializeComponent();
            InitializeShortcuts();
            InitializeMenuHandlers();
            InitializeLayout();
            HookLocationDetection();
            GetDocumentNumber();
            SetTitle($"Untitled {_documentNumber}");
            _lastHash = DocumentHash;
        }

        private void GetDocumentNumber()
        {
            //purposely not disposing the mmap
            using (new ScopedMutex(true, "{8BED64DE-A2F9-408F-A223-92EDAD8D90E8}"))
            using (var view = _mmap.CreateViewAccessor(0, MmapSize))
            {
                _documentNumber = view.ReadInt32(0) + 1;
                view.Write(0, _documentNumber);
            }
        }

        private void DecrementDocumentNumber()
        {
            //purposely not disposing the mmap
            using (new ScopedMutex(true, "{8BED64DE-A2F9-408F-A223-92EDAD8D90E8}"))
            using (var view = _mmap.CreateViewAccessor(0, MmapSize))
            {
                _documentNumber = view.ReadInt32(0) - 1;
                view.Write(0, _documentNumber);
            }
        }

        private void InitializeShortcuts()
        {
            _shortcuts = new Dictionary<Keys, Action>
            {
                //File menu
                { Keys.Control | Keys.N, New },
                { Keys.Control | Keys.Shift | Keys.N, NewWindow },
                { Keys.Control | Keys.O, Open },
                { Keys.Control | Keys.Shift | Keys.O, OpenNew },
                { Keys.Control | Keys.S, () => { Save(); } },
                { Keys.F12, SaveAs },
                { Keys.Control | Keys.P, Print },
                { Keys.Control | Keys.W, Exit },
                //Edit menu
                { Keys.Control | Keys.X, Cut },
                { Keys.Control | Keys.C, Copy },
                { Keys.Control | Keys.V, Paste },
                { Keys.Control | Keys.Z, text.Undo },
                { Keys.Control | Keys.Y, text.Redo },
                { Keys.Control | Keys.F, Find },
                { Keys.F3, FindNext },
                { Keys.Control | Keys.H, Replace },
                { Keys.Control | Keys.G, GoTo },
                { Keys.F5, TimeDate },
                //Help menu
                { Keys.F1, BetterpadHelp },
            };
        }

        private void InitializeMenuHandlers()
        {
            var handlers = new Dictionary<ToolStripMenuItem, Action>
            {
                //File menu
                { newToolStripMenuItem, New },
                { newWindowToolStripMenuItem, NewWindow },
                { openToolStripMenuItem, Open },
                { saveToolStripMenuItem, () => { Save(); } },
                { saveAsToolStripMenuItem, SaveAs },
                { pageSetupToolStripMenuItem, PageSetup },
                { printToolStripMenuItem, Print },
                { exitToolStripMenuItem, Close },
                //Edit menu
                { undoToolStripMenuItem, text.Undo },
                { redoToolStripMenuItem, text.Redo },
                { cutToolStripMenuItem, Cut },
                { copyToolStripMenuItem, Copy },
                { pasteToolStripMenuItem, Paste },
                { deleteToolStripMenuItem, Delete },
                { findToolStripMenuItem, Find },
                { findNextToolStripMenuItem, FindNext },
                { replaceToolStripMenuItem, Replace },
                { goToToolStripMenuItem, GoTo },
                { selectAllToolStripMenuItem, text.SelectAll },
                { timeDateToolStripMenuItem, TimeDate },
                //Format menu
                { wordWrapToolStripMenuItem, WordWrap },
                { fontToolStripMenuItem, ConfigureFont },
                //View menu
                { statusBarToolStripMenuItem, () => statusStrip1.Visible = statusBarToolStripMenuItem.Checked },
                //Help menu
                { viewHelpToolStripMenuItem, BetterpadHelp },
                { aboutBetterpadToolStripMenuItem, About },
            };

            foreach (var menuItem in handlers.Keys)
            {
                menuItem.Click += (sender, args) =>
                {
                    handlers[menuItem]();
                };
            }
        }

        private void InitializeLayout()
        {
            SetDefaultWidth();
            using (var process = Process.GetCurrentProcess())
            {
                _processPath = process.MainModule.FileName;
            }
            Icon = Icon.ExtractAssociatedIcon(_processPath);
            text.Padding = new Padding(14, 10, 12, 10);
            text_SelectionChanged(null, null);
            text_TextChanged(null, null);
            statusBarToolStripMenuItem.PerformClick();
            wordWrapToolStripMenuItem.PerformClick();
        }

        private void SetDefaultWidth()
        {
            //These both return differing (and very wrong) answers on hi-dpi displays
            //TextRender.MeasureText() is way too big while Graphics.MeasureString() is way too small
            //Width = (int) (1.05 * TextRenderer.MeasureText(" ", text.Font).Width * 80);
            //Width = (int)CreateGraphics().MeasureString(" ", text.Font).Width * 80;

            //testbox is a technically hidden but officially a visible part of the form
            testbox.Font = text.Font;
            testbox.Text = "                                                                                  ";
            Width = testbox.Width;
        }

        private void SetTitle(string document)
        {
            Text = $"{document} - Betterpad by NeoSmart Technologies";
        }

        private void text_TextChanged(Object sender, EventArgs e)
        {
            redoToolStripMenuItem.Enabled = text.CanRedo;
            undoToolStripMenuItem.Enabled = text.CanUndo;
        }

        private void HookLocationDetection()
        {
            Action updateLocation = () =>
            {
                var line = text.GetLineFromCharIndex(text.SelectionStart);
                var offset = text.SelectionStart - text.GetFirstCharIndexFromLine(line);
                locationLabel.Text = $"Ln {line + 1}, Col {offset + 1}"; //not zero-based :'(
            };

            text.MouseUp += (sender, args) => updateLocation();
            text.KeyUp += (sender, args) => updateLocation();

            updateLocation();
        }

        //File menu handlers
        private void New()
        {
            if (!UnsavedChanges())
            {
                return;
            }

            _ignoreChanges = true; //So Close() doesn't trigger another warning

            //set properties for new window
            WindowManager.CreateNewWindow((form) =>
            {
                form.Size = Size;
                form.StartPosition = FormStartPosition.Manual;
                form.Location = Location;
            });

            Close();
        }

        private void NewWindow()
        {
            WindowManager.CreateNewWindow();
        }

        private void Open()
        {
            bool documentChanged = false;
            if (!UnsavedChanges(ref documentChanged))
            {
                return;
            }
            documentChanged = documentChanged || !string.IsNullOrEmpty(_path) || text.Text != "";

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
                //Decrement the document number IF no changes had been made AND no new document was created in the meantime
                if (!documentChanged)
                {
                    DecrementDocumentNumber();
                }
            }
        }

        private void OpenNew()
        {
            WindowManager.CreateNewWindow((form) =>
            {
                form.Size = Size;
                form.StartPosition = FormStartPosition.Manual;
                form.Location = Location;
            }, (form) =>
            {
                form.Focus();
                form.Open();
            });
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

            var data = File.ReadAllText(path, Encoding.UTF8);
            text.Text = data;
            _lastHash = DocumentHash;
        }

        private bool Save()
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
                    return true;
                }
            }

            return false;
        }

        private void Save(string path)
        {
            File.WriteAllText(path, text.Text, new UTF8Encoding(false));
            _lastHash = DocumentHash;
        }

        private void SaveAs()
        {
            var oldPath = _path;
            _path = null;
            if (!Save())
            {
                _path = oldPath;
            }
        }

        private void PageSetup()
        {
            throw new NotImplementedException();
        }

        private void Print()
        {
            throw new NotImplementedException();
        }

        private void Exit()
        {
            if (!UnsavedChanges())
            {
                return;
            }

            Close();
        }

        //Edit menu handlers
        private void Cut()
        {
            Copy();
            Delete();
        }

        private void Copy()
        {
            if (!string.IsNullOrEmpty(text.SelectedText))
            {
                Clipboard.SetText(text.SelectedText, TextDataFormat.UnicodeText);
            }
        }

        private void Paste()
        {
            text.Paste();
            //text.Paste(DataFormats.GetFormat(DataFormats.UnicodeText));
        }

        private void Delete()
        {
            if (!string.IsNullOrEmpty(text.SelectedText))
            {
                var sb = new StringBuilder(text.TextLength - text.SelectionLength);
                sb.Append(text.Text.Substring(0, text.SelectionStart));
                sb.Append(text.Text.Substring(text.SelectionStart + text.SelectionLength));

                text.Text = sb.ToString();
            }
        }

        private void Find()
        {
            var finder = new FindDialog();
            finder.ShowDialog(this);
        }

        private void FindNext()
        {
            throw new NotImplementedException();
        }

        private void Replace()
        {
            throw new NotImplementedException();
        }

        private void GoTo()
        {
            throw new NotImplementedException();
        }

        private void TimeDate()
        {
            throw new NotImplementedException();
        }

        //Format menu handlers
        private void WordWrap()
        {
            text.WordWrap = wordWrapToolStripMenuItem.Checked;
        }

        private void ConfigureFont()
        {
            using (var fontDialog = new FontDialog())
            {
                fontDialog.Font = text.Font;
                fontDialog.FontMustExist = true;
                fontDialog.AllowVectorFonts = true;
                fontDialog.AllowVerticalFonts = false;
                if (fontDialog.ShowDialog() == DialogResult.OK)
                {
                    text.Font = fontDialog.Font;
                }
            }
        }

        //Help menu handlers
        private void BetterpadHelp()
        {
            throw new NotImplementedException();
        }

        private void About()
        {
            throw new NotImplementedException();
        }

        private void text_SelectionChanged(Object sender, EventArgs e)
        {
            deleteToolStripMenuItem.Enabled = text.TextSelected;
            cutToolStripMenuItem.Enabled = text.TextSelected;
            copyToolStripMenuItem.Enabled = text.TextSelected;
        }

        private bool UnsavedChanges()
        {
            bool documentChanged = false;
            return UnsavedChanges(ref documentChanged);
        }

        private bool UnsavedChanges(ref bool documentChanged)
        {
            if (_ignoreChanges)
            {
                return true;
            }

            if (!Interop.ByteArrayCompare(DocumentHash, _lastHash))
            {
                documentChanged = true;
                var result = MessageBox.Show(this,
                    "The document has unsaved changes. Do you want to save changes before closing?", "Save changes?",
                    MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (result == DialogResult.Cancel)
                {
                    return false;
                }
                if (result == DialogResult.Yes)
                {
                    if (!Save())
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private void Form1_FormClosing(Object sender, FormClosingEventArgs e)
        {
            if (!UnsavedChanges())
            {
                e.Cancel = true;
            }
        }

        private void Form1_Shown(object sender, EventArgs e)
        {
            //Run any actions queued by window manager
            StartAction?.Invoke(this);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (_shortcuts.TryGetValue(keyData, out var action))
            {
                action();
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }
    }
}
