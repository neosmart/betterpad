using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Media;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace betterpad
{
    public partial class Form1 : Form
    {
        private Dictionary<Keys, Action> _shortcuts;
        private const int MmapSize = 32;
        private readonly MemoryMappedFile _mmap = MemoryMappedFile.CreateOrOpen("{6472DD80-A7A5-4F44-BAD4-69BB7F9580DE}", MmapSize);
        private int _documentNumber;
        public string FilePath { get; private set; }
        private bool _ignoreChanges = false;
        private string _processPath;
        private FindDialog _finder;
        private FindStatus _findStatus = new FindStatus();
        private object _statusTimerLock = new object();
        private Timer _statusTimer = null;
        private bool _ignoreSettings = false;
        public BetterRichTextBox BetterBox => text;

        private bool DocumentChanged
        {
            get
            {
                return Interop.ByteArrayCompare(DocumentHash, _lastHash) == false;
            }
        }

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
            InitializeHandlers();
            HookLocationDetection();
            GetDocumentNumber();
            LoadPreferences();
            SetTitle($"Untitled {_documentNumber}");
            _lastHash = DocumentHash;
            _finder = new FindDialog(text);
        }

        private void InitializeHandlers()
        {
            Load += (s, e) => {
                text.Padding = new Padding(10, 10, 12, 10);
            };

            text.LinkClicked += (s, e) =>
            {
                //Only open URLs if the ctrl button is held down
                if (!((ModifierKeys & Keys.Control) == Keys.Control))
                {
                    SetStatus("Hold down CTRL and click to open URL");
                    return;
                }

                var link = e.LinkText;
                if (Uri.TryCreate(link, UriKind.Absolute, out var uri) && uri.Host.Contains("."))
                {
                    using (var p = new Process())
                    {
                        p.StartInfo = new ProcessStartInfo(uri.AbsoluteUri);
                        p.Start();
                    }
                }
                else
                {
                    SetStatus("Invalid URL!");
                }
            };
        }

        private void SelectionChangedFindHandler(object sender, EventArgs e)
        {
            _findStatus.FindCount = 0;
            _findStatus.StartPosition = -1;
            _findStatus.FirstResult = -1;
            text.SelectionChanged -= SelectionChangedFindHandler;
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
                { Keys.Control | Keys.S, () => Save() },
                { Keys.F12, SaveAs },
                { Keys.Control | Keys.P, Print },
                { Keys.Control | Keys.W, Exit },
                //Edit menu
                { Keys.Control | Keys.X, Cut },
                { Keys.Control | Keys.C, Copy },
                { Keys.Control | Keys.V, Paste },
                { Keys.Control | Keys.Y, text.Redo },
                { Keys.Control | Keys.F, () => Find() },
                { Keys.F3, () => FindNext() },
                { Keys.Shift | Keys.F3, FindPrevious },
                { Keys.Control | Keys.H, Replace },
                { Keys.Control | Keys.G, GoTo },
                { Keys.F5, TimeDate },
                //Help menu
                { Keys.F1, BetterpadHelp },
            };
        }

        private void InitializeMenuHandlers()
        {
            var handlers = new Dictionary<MenuItem, Action>
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
                { findToolStripMenuItem, () => Find() },
                { findNextToolStripMenuItem, () => FindNext() },
                { replaceToolStripMenuItem, Replace },
                { goToToolStripMenuItem, GoTo },
                { selectAllToolStripMenuItem, text.SelectAll },
                { timeDateToolStripMenuItem, TimeDate },
                //Format menu
                { wordWrapToolStripMenuItem, ToggleWordWrap },
                { fontToolStripMenuItem, ConfigureFont },
                //View menu
                { statusBarToolStripMenuItem, StatusBar },
                //Help menu
                { viewHelpToolStripMenuItem, BetterpadHelp },
                { checkForUpdateMenuItem, CheckForUpdates },
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
            text_SelectionChanged(null, null);
            text_TextChanged(null, null);
            statusBarToolStripMenuItem.PerformClick();
            wordWrapToolStripMenuItem.PerformClick();
            wordWrapToolStripMenuItem.PerformClick();
            lblStatus1.Text = string.Empty;
            lblStatus2.Text = string.Empty;
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
            Text = $"{document} - betterpad by NeoSmart Technologies";
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
            var actions = new WindowManager.NewFormActions()
            {
                BeforeShow = (form) =>
                {
                    form.Size = Size;
                    form.StartPosition = FormStartPosition.Manual;
                    form.Location = Location;
                }
            };
            WindowManager.CreateNewWindow(actions);

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
            documentChanged = documentChanged || !string.IsNullOrEmpty(FilePath) || text.Text != "";

            using (var dialog = new OpenFileDialog()
            {
                AutoUpgradeEnabled = true,
                CheckFileExists = true,
                CheckPathExists = true,
                DefaultExt = "txt",
                Filter = "Plain Text Files|" + FileExtensions.AsFilter + "|Text Files (*.txt)|*.txt|Log Files (*.log)|*.log",
                Multiselect = true,
                RestoreDirectory = true,
                InitialDirectory = string.IsNullOrEmpty(FilePath) ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) : Path.GetDirectoryName(FilePath),
                ShowReadOnly = true,
                ValidateNames = true,
                Title = "Open file"
            })
            {
                if (dialog.ShowDialog(this) == DialogResult.OK && dialog.FileNames.Length > 0)
                {
                    var first = dialog.FileNames.First();

                    //first document
                    Open(first);
                    //Decrement the document number IF no changes had been made AND no new document was created in the meantime
                    if (!documentChanged)
                    {
                        DecrementDocumentNumber();
                    }

                    //subsequent documents, if any
                    foreach (var doc in dialog.FileNames.Skip(1))
                    {
                        OpenNew(doc, false);
                    }
                }
            }
            GC.Collect();
        }

        private void OpenNew()
        {
            OpenNew(null, true);
        }

        private void OpenNew(string path, bool sameLocation)
        {
            var actions = new WindowManager.NewFormActions()
            {
                BeforeShow = (form) =>
                {
                    form.Size = Size;
                    if (sameLocation)
                    {
                        form.StartPosition = FormStartPosition.Manual;
                        form.Location = Location;
                    }
                },
                AfterShow = (form) =>
                {
                    form.Focus();
                    form.BringToFront();
                    if (string.IsNullOrEmpty(path))
                    {
                        form.Open();
                    }
                    else
                    {
                        form.Open(path);
                        DecrementDocumentNumber();
                    }
                }
            };
            WindowManager.CreateNewWindow(actions);
        }

        public void Open(string path)
        {
            var dir = Path.GetDirectoryName(path);
            if (!Directory.Exists(dir))
            {
                MessageBox.Show(this, $"Could not find a part of the path {dir}!", "Directory not found!",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            FilePath = path;
            SetTitle(Path.GetFileName(path));

            if (!File.Exists(path))
            {
                //Don't load it, but we will save this reference because we'll write to it
                return;
            }

            var data = File.ReadAllText(path, Encoding.UTF8);
            text.Text = data;
            var justCreated = (DateTime.UtcNow - File.GetCreationTimeUtc(path)) < TimeSpan.FromMilliseconds(1000);
            SetStatus(string.Format("Document {0}", justCreated ? "created" : "loaded"));
            _lastHash = DocumentHash;

            GC.Collect();
        }

        private bool Save()
        {
            while (true)
            {
                if (string.IsNullOrEmpty(FilePath))
                {
                    using (var dialog = new SaveFileDialog()
                    {
                        AddExtension = true,
                        AutoUpgradeEnabled = true,
                        CheckFileExists = false,
                        CheckPathExists = true,
                        DefaultExt = "txt",
                        RestoreDirectory = true,
                        InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                        Title = "Save file",
                        Filter = "Text Files (*.txt)|*.txt|Log Files (*.log)|*.log"
                    })
                    {
                        var result = dialog.ShowDialog(this);
                        GC.Collect();
                        if (result == DialogResult.OK)
                        {
                            FilePath = dialog.FileName;
                        }
                        else
                        {
                            return false;
                        }
                    }
                }

                try
                {
                    Save(FilePath);
                }
                catch (UnauthorizedAccessException)
                {
                    var result = MessageBox.Show(this, $"Unable to save document to path {FilePath}. Please select a different path and try again.",
                        "Access denied!", MessageBoxButtons.OKCancel, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1);
                    if (result == DialogResult.OK)
                    {
                        FilePath = null;
                        continue;
                    }
                    return false;
                }
                return true;
            }
        }

        private void Save(string path)
        {
            File.WriteAllText(path, text.Text, new UTF8Encoding(false));
            SetTitle(Path.GetFileName(path));
            SetStatus("Changes saved");
            _lastHash = DocumentHash;

            GC.Collect();
        }

        private void SaveAs()
        {
            var oldPath = FilePath;
            FilePath = null;
            if (!Save())
            {
                FilePath = oldPath;
            }
        }

        private void PageSetup()
        {
        }

        private void Print()
        {
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
            text.Paste(DataFormats.GetFormat(DataFormats.UnicodeText));
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

        private bool Find()
        {
            _finder.SearchBox.SelectAll();
            _finder.SearchBox.Focus();

            var result = _finder.ShowDialog(this);
            if (result == DialogResult.OK)
            {
                _findStatus.FindCount = 0;
                _findStatus.SearchTerm = _finder.SearchTerm;
                _findStatus.Options = _finder.Options;
                _findStatus.StartPosition = text.SelectionStart + text.SelectionLength;
                _findStatus.EndPosition = -1;
                _findStatus.FirstResult = -1;
                _findStatus.Direction = FindStatus.SearchDirection.Forward;
                return FindNext();
            }

            return false;
        }

        private bool FindNext(bool noDialog = false)
        {
            if (_findStatus.Direction != FindStatus.SearchDirection.Forward ||
                _findStatus.StartPosition == -1)
            {
                _findStatus.Direction = FindStatus.SearchDirection.Forward;
                _findStatus.FindCount = 0;
                _findStatus.FirstResult = -1;
                _findStatus.StartPosition = text.SelectionStart + text.SelectionLength;
                _findStatus.OriginalStartPosition = text.SelectionStart + text.SelectionLength;
                _findStatus.EndPosition = -1;
            }

            return Find(-1, false, noDialog);
        }

        private bool Find(int end, bool reverse, bool noDialog = false)
        {
            var notFound = false;

            if (string.IsNullOrEmpty(_findStatus.SearchTerm))
            {
                if (!noDialog)
                {
                    return Find();
                }
                else
                {
                    return false;
                }
            }

            text.SelectionChanged -= SelectionChangedFindHandler;

            var index = text.Find(_findStatus.SearchTerm, _findStatus.StartPosition, _findStatus.EndPosition, _findStatus.Options | (_findStatus.Direction == FindStatus.SearchDirection.Reverse ? RichTextBoxFinds.Reverse : RichTextBoxFinds.None));

            if (index == -1 && _findStatus.Direction == FindStatus.SearchDirection.Forward && _findStatus.StartPosition != 0)
            {
                _findStatus.StartPosition = 0;
                _findStatus.EndPosition = -1;
                return Find(end, reverse);
            }
            else if (index == -1 && _findStatus.Direction == FindStatus.SearchDirection.Reverse && _findStatus.EndPosition <= _findStatus.OriginalStartPosition && _findStatus.EndPosition != -1)
            {
                _findStatus.StartPosition = _findStatus.OriginalStartPosition;
                _findStatus.EndPosition = -1;
                return Find(end, reverse);
            }

            if (index == -1)
            {
                SystemSounds.Beep.Play();
                notFound = true;
                SetStatus("No results found!", TimeSpan.FromMilliseconds(3000));
            }
            else if (index == _findStatus.FirstResult)
            {
                //Looped around
                _findStatus.FindCount = 0;
                _findStatus.StartPosition = _findStatus.Direction == FindStatus.SearchDirection.Forward ? _findStatus.OriginalStartPosition : 0;
                _findStatus.EndPosition = _findStatus.Direction == FindStatus.SearchDirection.Forward ? -1 : _findStatus.OriginalStartPosition;
                SystemSounds.Beep.Play();
                notFound = true;
                SetStatus("No more results! Search restarted.", TimeSpan.FromMilliseconds(3000));
            }
            else if (_findStatus.FirstResult == -1)
            {
                _findStatus.FirstResult = index;
            }

            if (index != -1 && _findStatus.Direction == FindStatus.SearchDirection.Forward)
            {
                _findStatus.StartPosition = text.SelectionStart + text.SelectionLength;
            }
            else if (index != -1 && _findStatus.Direction == FindStatus.SearchDirection.Reverse)
            {
                _findStatus.EndPosition = text.SelectionStart - 1;
                if (_findStatus.EndPosition < _findStatus.StartPosition)
                {
                    _findStatus.StartPosition = 0;
                }
            }

            //Hook the selection changed event to allow restarting search from current position
            text.SelectionChanged += SelectionChangedFindHandler;

            return !notFound;
        }

        private void FindPrevious()
        {
            if (_findStatus.Direction != FindStatus.SearchDirection.Reverse ||
                _findStatus.StartPosition == -1)
            {
                _findStatus.Direction = FindStatus.SearchDirection.Reverse;
                _findStatus.FindCount = 0;
                _findStatus.StartPosition = 0;
                _findStatus.FirstResult = -1;
                _findStatus.EndPosition = text.SelectionStart - 1;
                _findStatus.OriginalStartPosition = text.SelectionStart;
            }
            Find(-1, true);
        }

        private void Replace()
        {
            using (var replaceDialog = new ReplaceDialog(text))
            {
                replaceDialog.Status = SetStatus;
                string lastSearch = null;
                string lastReplace = null;
                RichTextBoxFinds lastOptions = RichTextBoxFinds.None;
                replaceDialog.FindCallback = (term) =>
                {
                    if (term != lastSearch || lastOptions != _findStatus.Options)
                    {
                        lastSearch = term;
                        lastOptions = _findStatus.Options;

                        _findStatus.FindCount = 0;
                        _findStatus.SearchTerm = term;
                        _findStatus.Options = replaceDialog.Options;
                        _findStatus.StartPosition = text.SelectionStart + text.SelectionLength;
                        _findStatus.EndPosition = -1;
                        _findStatus.FirstResult = -1;
                        _findStatus.Direction = FindStatus.SearchDirection.Forward;
                    }
                    var result = FindNext(true);
                    return result;
                };
                replaceDialog.ReplaceCallback = (term, replacement) =>
                {
                    if (term != lastSearch || lastOptions != _findStatus.Options || replacement != lastReplace)
                    {
                        lastSearch = term;
                        lastOptions = _findStatus.Options;
                        lastReplace = replacement;

                        _findStatus.FindCount = 0;
                        _findStatus.SearchTerm = term;
                        _findStatus.Options = replaceDialog.Options;
                        _findStatus.StartPosition = text.SelectionStart;
                        _findStatus.EndPosition = -1;
                        _findStatus.FirstResult = -1;
                        _findStatus.Direction = FindStatus.SearchDirection.Forward;
                    }
                    if (FindNext(true))
                    {
                        text.SelectionChanged -= SelectionChangedFindHandler;
                        text.Insert(replacement);
                        text.SelectionChanged += SelectionChangedFindHandler;
                        return true;
                    }
                    return false;
                };
                replaceDialog.ShowDialog(this);
            }
        }

        private void GoTo()
        {
            using (var gotoDialog = new GoToDialog())
            {
                do
                {
                    gotoDialog.StartPosition = FormStartPosition.CenterParent;
                    var result = gotoDialog.ShowDialog(this);
                    if (result == DialogResult.OK)
                    {
                        int line = text.GetFirstCharIndexFromLine(gotoDialog.LineNumber);
                        if (line == -1)
                        {
                            SystemSounds.Beep.Play();
                            gotoDialog.SelectAll();
                            continue;
                        }
                        else
                        {
                            text.SelectionStart = line;
                        }
                    }
                    break;
                } while (true);
            }
        }

        private void TimeDate()
        {
            var snippet = (DateTime.Now).ToString("h:mm tt M/d/yyyy");
            text.Insert(snippet);
        }

        //Format menu handlers
        private void WordWrap(bool wrap)
        {
            wordWrapToolStripMenuItem.Checked = wrap;
            text.WordWrap = wrap;
        }

        private void ToggleWordWrap()
        {
            wordWrapToolStripMenuItem.Checked = !text.WordWrap;
            text.WordWrap = !text.WordWrap;
        }

        private void StatusBar()
        {
            statusBarToolStripMenuItem.Checked = !statusBarToolStripMenuItem.Checked;
            statusStrip1.Visible = statusBarToolStripMenuItem.Checked;
        }

        private void ConfigureFont()
        {
            using (var fontDialog = new FontDialog())
            {
                fontDialog.Font = text.Font;
                fontDialog.FontMustExist = true;
                fontDialog.AllowVectorFonts = true;
                fontDialog.AllowVerticalFonts = false;
                if (fontDialog.ShowDialog(this) == DialogResult.OK)
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

        private void CheckForUpdates()
        {
            var statusTimer = new Timer();
            statusTimer.Interval = 500;
            uint dotCount = 0;
            statusTimer.Tick += (s, e) =>
            {
                var message = "Checking for updates.";
                SetStatus(message.PadRight((int) (message.Length + (++dotCount % 4)), '.'));
            };
            statusTimer.Start();
            var thread = new System.Threading.Thread(() =>
            {
                var updateManager = new UpdateManager();
                var version = updateManager.GetLatestVersion(false);
                statusTimer.Stop();
                statusTimer.Dispose();

                if (version == null)
                {
                    SystemSounds.Exclamation.Play();
                    SetStatus("Error checking for updates! Please try check your connection or try again later!");
                }
                else if (updateManager.UpdateAvailable(version))
                {
                    SetStatus("Update available! Lauching download in new window.", TimeSpan.FromSeconds(10));
                    using (var process = new Process())
                    {
                        process.StartInfo = new ProcessStartInfo(version.DownloadUrl ?? version.InfoUrl);
                        process.Start();
                    }
                }
                else
                {
                    SystemSounds.Asterisk.Play();
                    SetStatus("No update is available.");
                }

            });
            thread.Start();
        }

        private void About()
        {
            var actions = new WindowManager.NewFormActions()
            {
                BeforeShow = (form) =>
                {
                    form.Height = 500;
                    form.Width = 800;
                    form.StartPosition = FormStartPosition.CenterParent;
                },
                AfterShow = (form) =>
                {
                    var sb = new StringBuilder();
                    sb.AppendLine($"betterpad by NeoSmart Technologies");
                    sb.AppendLine($"https://neosmart.net/betterpad/\r\n");
                    sb.AppendLine($"Version {ShortVersion} - build {BuildHash}\r\n");
                    sb.AppendLine($"Copyright © NeoSmart Technologies 2016-{DateTime.UtcNow.Year}");
                    sb.AppendLine("All rights reserved.\r\n\r\n");

                    form.text.Text = sb.ToString();
                    form.text.ReadOnly = true;
                    form._ignoreChanges = true;
                    form.Text = "betterpad by NeoSmart Technologies";
                    form._ignoreSettings = true;

                    form.text.SelectionStart = sb.Length;
                    form.text.SelectionFont = new Font(form.text.Font.FontFamily, form.text.Font.Size, FontStyle.Italic);
                    form.text.SelectedText = "> A better notepad. Still simple. Still fast.";
                    form.text.SelectionStart = 0;

                    form._shortcuts.Add(Keys.Escape, () => form.Close());
                }
            };
            using (var about = new Form1())
            {
                actions.BeforeShow(about);
                about.fileToolStripMenuItem.Visible = false;
                about.editToolStripMenuItem.Visible = false;
                about.formatToolStripMenuItem.Visible = false;
                about.viewToolStripMenuItem.Visible = false;
                about.helpToolStripMenuItem.Visible = false;
                about.StartAction = actions.AfterShow;
                about.FormBorderStyle = FormBorderStyle.FixedSingle;
                about.SizeGripStyle = SizeGripStyle.Hide;
                about.ShowInTaskbar = false;
                about.MaximizeBox = false;
                about.MinimizeBox = false;
                about.ShowDialog(this);
            }
        }

        private string ShortVersion
        {
            get
            {
                var version = Assembly.GetExecutingAssembly().GetName().Version;
                return $"{version.Major}.{version.Minor}.{version.Build}";
            }
        }

        private string BuildHash
        {
            get
            {
                var bytes = System.IO.File.ReadAllBytes(Application.ExecutablePath);
                MetroHash.MetroHash.Hash64_1(bytes, 0, (uint)bytes.Length, 0, out var hash);
                return BitConverter.ToString(hash, 0).Replace("-", "").ToLower();
            }
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

            if (RecoveryManager.ShutdownInProgress)
            {
                RecoveryManager.ShutdownDumpComplete.Wait();
                return true;
            }

            if (DocumentChanged)
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

            SavePreferences();
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

        private void SetStatus(string message)
        {
            if (InvokeRequired)
            {
                Invoke((Action)(() => { SetStatus(message); }));
                return;
            }
            SetStatus(message, TimeSpan.FromSeconds(4));
        }

        private void SetStatus(string message, TimeSpan timeout)
        {
            lblStatus1.Text = message;

            lock (_statusTimerLock)
            {
                //Make sure to cancel old timers
                _statusTimer?.Stop();
                _statusTimer?.Dispose();

                _statusTimer = new Timer();
                _statusTimer.Interval = (int) timeout.TotalMilliseconds;
                _statusTimer.Tick += (s, e) =>
                {
                    Invoke((Action)(() =>
                    {
                        lblStatus1.Text = string.Empty;
                        _statusTimer?.Dispose();
                    }));
                };
                _statusTimer.Start();
            }
        }

        private void DragDropHandler(object sender, DragEventArgs e)
        {
            var inNewWindow = DocumentChanged && text.Text.Length == 0;
            var paths = (string[])e.Data.GetData(DataFormats.FileDrop);
            foreach (var path in paths)
            {
                if (inNewWindow)
                {
                    OpenNew(path, false);
                }
                else
                {
                    inNewWindow = true; //we can only replace one document
                    Open(path);
                }
            }
        }

        private void DragDropBegin(object sender, DragEventArgs e)
        {
            e.Effect = DragDropEffects.Move;
        }

        protected override void WndProc(ref Message m)
        {
            if (RecoveryManager.MessageHandler(ref m))
            {
                return;
            }

            if (m.Msg == Win32.WM_COPYDATA)
            {
                var path = Win32.ReceiveWindowMessage(m);
                OpenNew(path, false);
            }

            base.WndProc(ref m);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (components != null)
                {
                    components.Dispose();
                }

                //Own dispose code
                _finder.Dispose();
                text.Dispose();
            }

            base.Dispose(disposing);
        }

        private void LoadPreferences()
        {
            var preferences = Preferences.Load();
            if (preferences == null)
            {
                return;
            }

            Width = preferences.Width;
            Height = preferences.Height;
            WordWrap(preferences.WordWrap);
            text.Font = new Font(preferences.FontFamily, preferences.FontSize);
        }

        private void SavePreferences()
        {
            if (_ignoreSettings)
            {
                return;
            }

            var preferences = new Preferences()
            {
                Width = Width,
                Height = Height - 34,  //I don't know why, but the height creeps up by 34 each time
                FontFamily = text.Font.Name,
                FontSize = text.Font.Size,
                WordWrap = text.WordWrap
            };
            preferences.Save();
        }
    }
}
