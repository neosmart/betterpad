using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Media;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace betterpad
{
    public partial class EditorWindow : Form
    {
        delegate Task AsyncAction();
        private Dictionary<Keys, AsyncAction> _shortcuts;
        private const int MmapSize = 32;
        /// <summary>
        /// This memory map is used to access data shared between all instances
        /// </summary>
        private readonly MemoryMappedFile _mmap = MemoryMappedFile.CreateOrOpen("{6472DD80-A7A5-4F44-BAD4-69BB7F9580DE}", MmapSize);
        private int _documentNumber;
        public string FilePath { get; private set; }
        private bool _ignoreChanges = false;
        private string _processPath;
        private FindDialog _finder;
        private FindStatus _findStatus = new FindStatus();
        private object _statusTimerLock = new object();
        private System.Windows.Forms.Timer _statusTimer;
        private bool _ignoreSettings = false;
        /// <summary>
        /// Used by the WindowManager to make a "dialog box" blocking its parent
        /// </summary>
        public EditorWindow ParentWindow { get; private set; } = null;
        public BetterRichTextBox BetterBox => text;
        public CancellationToken Cancel { get; set; }
        /// <summary>
        /// Whether or not this form will be recovered by the Window Manager in case of an unsafe shutdown.
        /// </summary>
        public bool RecoveryEnabled { get; set; } = true;

        private bool DocumentChanged
        {
            get => DocumentHash != _lastHash;
        }

        private ulong DocumentHash
        {
            get
            {
                unsafe
                {
                    fixed (char* ptr = text.Text)
                    {
                        MetroHash.MetroHash.Hash64_1((byte*)ptr, 0, sizeof(char) * (uint)text.Text.Length, 0, out var hash);
                        return hash;
                    }
                }
            }
        }

        public Func<EditorWindow, Task> StartAction { get; internal set; }

        private ulong _lastHash;

        public EditorWindow()
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
                // Only open URLs if the ctrl button is held down
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
            // The mmap is purposely not disposed
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

        private Task UiExecute(Action action)
        {
            if (InvokeRequired)
            {
                return Task.Factory.FromAsync(BeginInvoke((Action)(() => action())), EndInvoke);
            }

            action();
            return Task.CompletedTask;
        }

        private Task UiExecute(AsyncAction action)
        {
            if (InvokeRequired)
            {
                return Task.Factory.FromAsync(BeginInvoke((Action)(() => action())), EndInvoke);
            }

            return action();
        }

        private void InitializeShortcuts()
        {
            _shortcuts = new Dictionary<Keys, AsyncAction>
            {
                // File menu
                { Keys.Control | Keys.N, () => UiExecute(NewWindow) },
                { Keys.Control | Keys.O, OpenAsync },
                { Keys.Control | Keys.S, SaveAsync },
                { Keys.Control | Keys.Shift | Keys.S, SaveAsAsync },
                { Keys.F12, SaveAsAsync },
                { Keys.Control | Keys.P, () => UiExecute(Print) },
                { Keys.Control | Keys.W, ExitAsync },
                // Edit menu
                { Keys.Control | Keys.X, () => UiExecute(Cut) },
                { Keys.Control | Keys.C, () => UiExecute(Copy) },
                { Keys.Control | Keys.V, () => UiExecute(Paste) },
                { Keys.Control | Keys.Y, () => UiExecute(text.Redo) },
                { Keys.Control | Keys.F, () => UiExecute(() => Find()) },
                { Keys.F3, () => UiExecute(() => FindNext()) },
                { Keys.Shift | Keys.F3, () => UiExecute(FindPrevious) },
                { Keys.Control | Keys.H, () => UiExecute(Replace) },
                { Keys.Control | Keys.G, () => UiExecute(GoTo) },
                { Keys.F5, () => UiExecute(TimeDate) },
                // Help menu
                { Keys.F1, () => UiExecute(BetterpadHelp) },
            };
        }

        private void InitializeMenuHandlers()
        {
            var handlers = new Dictionary<MenuItem, AsyncAction>
            {
                // File menu
                { newToolStripMenuItem, () => UiExecute(NewWindow) },
                { openToolStripMenuItem, () => UiExecute(() => OpenAsync()) },
                { saveToolStripMenuItem, () => UiExecute(() => SaveAsync()) },
                { saveAsToolStripMenuItem, () => UiExecute(() => SaveAsAsync()) },
                { pageSetupToolStripMenuItem, () => UiExecute(PageSetup) },
                { printToolStripMenuItem, () => UiExecute(Print) },
                { exitToolStripMenuItem, () => UiExecute(Close) },
                // Edit menu
                { undoToolStripMenuItem, () => UiExecute(text.Undo) },
                { redoToolStripMenuItem, () => UiExecute(text.Redo) },
                { cutToolStripMenuItem, () => UiExecute(Cut) },
                { copyToolStripMenuItem, () => UiExecute(Copy) },
                { pasteToolStripMenuItem, () => UiExecute(Paste) },
                { deleteToolStripMenuItem, () => UiExecute(Delete) },
                { findToolStripMenuItem, () => UiExecute(() => Find()) },
                { findNextToolStripMenuItem, () => UiExecute(() => FindNext()) },
                { replaceToolStripMenuItem, () => UiExecute(Replace) },
                { goToToolStripMenuItem, () => UiExecute(GoTo) },
                { selectAllToolStripMenuItem, () => UiExecute(text.SelectAll) },
                { timeDateToolStripMenuItem, () => UiExecute(TimeDate) },
                // Format menu
                { wordWrapToolStripMenuItem, () => UiExecute(ToggleWordWrap) },
                { fontToolStripMenuItem, () => UiExecute(ConfigureFont) },
                // View menu
                { statusBarToolStripMenuItem, () => UiExecute(StatusBar) },
                // Help menu
                { viewHelpToolStripMenuItem, () => UiExecute(BetterpadHelp) },
                { checkForUpdateMenuItem, CheckForUpdatesAsync },
                { aboutBetterpadToolStripMenuItem, () => UiExecute(About) },
            };

            foreach (var menuItem in handlers.Keys)
            {
                menuItem.Click += async (sender, args) =>
                {
                    await handlers[menuItem]();
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
            // These both return differing (and very wrong) answers on hi-dpi displays
            // TextRender.MeasureText() is way too big while Graphics.MeasureString() is way too small
            //
            // Width = (int) (1.05 * TextRenderer.MeasureText(" ", text.Font).Width * 80);
            // Width = (int)CreateGraphics().MeasureString(" ", text.Font).Width * 80;

            // testbox is a technically hidden but officially a visible part of the form
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
        private void NewWindow()
        {
            WindowManager.CreateNewWindow();
        }

        private async Task<bool> OpenAsync()
        {
            var inNewWindow = DocumentChanged || text.Text.Length != 0;

            DocumentChangeStatus changeStatus = DocumentChangeStatus.NoChanges;
            if (!inNewWindow)
            {
                changeStatus = await MaybeSaveAndGetStatusAsync();
                if (changeStatus == DocumentChangeStatus.ChangedNotSaved)
                {
                    return false;
                }
            }

            var documentChanged = changeStatus == DocumentChangeStatus.ChangedAndSaved
                || !string.IsNullOrEmpty(FilePath) || text.Text != "";

            bool result = true;
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

                    // first document
                    if (inNewWindow)
                    {
                        OpenNew(first, sameLocation: false);
                    }
                    else
                    {
                        await OpenAsync(first);

                        // Decrement the document number IF no changes had been made AND no new document was created in the meantime
                        if (!documentChanged)
                        {
                            DecrementDocumentNumber();
                        }
                    }

                    //subsequent documents, if any
                    foreach (var doc in dialog.FileNames.Skip(1))
                    {
                        OpenNew(doc, sameLocation: false);
                    }
                }
                else
                {
                    result = false;
                }
            }
            GC.Collect();
            return result;
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
                    form.Visible = false;
                },
                AfterShow = async (form) =>
                {
                    form.Size = Size;
                    if (string.IsNullOrEmpty(path))
                    {
                        if (!await form.OpenAsync())
                        {
                            form.Close();
                            DecrementDocumentNumber();
                            return;
                        }
                    }
                    else
                    {
                        await form.OpenAsync(path);
                        DecrementDocumentNumber();
                    }

                    form.Focus();
                    form.BringToFront();
                }
            };
            WindowManager.CreateNewWindow(actions);
        }

        public async Task OpenAsync(string path)
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

            //File.ReadAllText causes an access violation when the file is being written to
            //var data = File.ReadAllText(path, Encoding.UTF8);
            using (var file = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Write | FileShare.Delete))
            using (var reader = new StreamReader(file, Encoding.UTF8))
            {
                text.Text = await reader.ReadToEndAsync();
            }
            var justCreated = (DateTime.UtcNow - File.GetCreationTimeUtc(path)) < TimeSpan.FromMilliseconds(1000);
            SetStatus(string.Format("Document {0}", justCreated ? "created" : "loaded"));
            _lastHash = DocumentHash;

            GC.Collect();
        }

        private Task<bool> SaveAsAsync()
        {
            return SaveAsAsync(null);
        }

        private async Task<bool> SaveAsAsync(string filePath)
        {
            while (true)
            {
                if (string.IsNullOrEmpty(filePath))
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
                        if (dialog.ShowDialog(this) != DialogResult.OK)
                        {
                            return false;
                        }

                        filePath = dialog.FileName;
                    }
                }

                switch (await SaveAsync(filePath))
                {
                    case SaveResult.Ok:
                        FilePath = filePath;
                        return true;
                    case SaveResult.Retry:
                        continue;
                    case SaveResult.Cancel:
                        return false;
                }
            }
        }

        private Task<bool> SaveAsync()
        {
            return SaveAsAsync(FilePath);
        }

        enum SaveResult
        {
            Ok,
            Retry,
            Cancel
        }

        private async Task<SaveResult> SaveAsync(string path)
        {
            bool alreadyThere = File.Exists(path);

            try
            {
                using (var file = File.Open(path, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read))
                using (var writer = new StreamWriter(file, new UTF8Encoding(false)))
                {
                    await writer.WriteAsync(text.Text);
                }
                SetTitle(Path.GetFileName(path));
                SetStatus($"{(alreadyThere ? "Changes" : "Document")} saved");
            }
            catch (UnauthorizedAccessException)
            {
                var result = MessageBox.Show(this,
                    $"Unable to save document to path {path}. " +
                    "Please select a different path and try again.",
                    "Access denied!", MessageBoxButtons.OKCancel, MessageBoxIcon.Error,
                    MessageBoxDefaultButton.Button1);

                return result == DialogResult.Cancel ? SaveResult.Cancel : SaveResult.Retry;
            }
            catch (Exception ex)
            {
                var result = MessageBox.Show(this, ex.Message, "Error saving file!",
                    MessageBoxButtons.RetryCancel, MessageBoxIcon.Error);

                return result == DialogResult.Cancel ? SaveResult.Cancel : SaveResult.Retry;
            }

            _lastHash = DocumentHash;
            return SaveResult.Ok;
        }

        private void PageSetup()
        {
        }

        private void Print()
        {
        }

        private async Task ExitAsync()
        {
            if (await MaybeSaveAndGetStatusAsync() == DocumentChangeStatus.ChangedNotSaved)
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

        // Used to store the font as returned by the font selector, as we don't apply it directly
        private static Regex NonStandardFontWeightRegex = new Regex(@"Thin|Light|Medium|Bold|Heavy|Black",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private void ConfigureFont()
        {
            using (var fontDialog = new FontDialog()
            {
                Font = text.Font,
                FontMustExist = true,
                AllowVectorFonts = true,
                AllowVerticalFonts = false,
            })
            {
                if (fontDialog.ShowDialog(this) == DialogResult.OK)
                {
                    // Hack in support for non-standard font weights, broken in System.Windows.Forms
                    // It is expecting a font family with different (limited) styles. Modern fonts
                    // are seen as having a family per style, with the FontStyle set to Regular.
                    var font = fontDialog.Font;
                    if (NonStandardFontWeightRegex.IsMatch(font.FontFamily.Name))
                    {
                        font = new Font(font, font.Style & ~FontStyle.Bold);
                    }
                    text.Font = font;
                }
            }
        }

        //Help menu handlers
        private void BetterpadHelp()
        {
            //NotImplementedException();
        }

        private async Task CheckForUpdatesAsync()
        {
            using var statusTimer = new System.Windows.Forms.Timer();
            statusTimer.Interval = 500;

            uint dotCount = 0;
            void ReportUpdateCheckProgress()
            {
                var message = "Checking for updates.";
                SetStatus(message.PadRight((int)(message.Length + (++dotCount % 4)), '.'));
            };

            ReportUpdateCheckProgress();
            statusTimer.Tick += (s, e) => ReportUpdateCheckProgress();
            statusTimer.Start();

            // The changes to the UI can be jarring if the response is immediately received. Slow it down a tad.
            await Task.Delay(800);

            var updateManager = new UpdateManager();
            var version = await updateManager.GetLatestVersionAsync(false);
            statusTimer.Stop();

            if (version == null)
            {
                SystemSounds.Exclamation.Play();
                SetStatus("Error checking for updates! Please try check your connection or try again later!");
            }
            else if (updateManager.UpdateAvailable(version))
            {
                SetStatus("Update available! Launching download in new window.", TimeSpan.FromSeconds(10));
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
        }

        private void About()
        {
            var actions = new WindowManager.NewFormActions()
            {
                BeforeShow = (form) =>
                {
                    form.ParentWindow = this;

                    form.Height = 500;
                    form.Width = 800;
                    form.StartPosition = FormStartPosition.CenterParent;

                    form.mainMenu1.MenuItems.Clear();
                    form.FormBorderStyle = FormBorderStyle.FixedSingle;
                    form.SizeGripStyle = SizeGripStyle.Hide;
                    form.ShowInTaskbar = false;
                    form.MaximizeBox = false;
                    form.MinimizeBox = false;
                    form.RecoveryEnabled = false;
                },
                AfterShow = async (form) =>
                {
                    var sb = new StringBuilder();
                    sb.AppendLine($"betterpad by NeoSmart Technologies");
                    sb.AppendLine($"https://neosmart.net/betterpad/\r\n");
                    sb.AppendLine($"Version {ShortVersion} - build {await CalculateBuildHashAsync()}\r\n");
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

                    form._shortcuts.Clear();
                    form._shortcuts.Add(Keys.Escape, () => { form.Close(); return Task.CompletedTask; });
                    form._shortcuts.Add(Keys.Control | Keys.W, () => { form.Close(); return Task.CompletedTask; });
                }
            };

            var about = new EditorWindow();
            actions.BeforeShow(about);
            about.Shown += async (s, e) => await actions.AfterShow(about);
            about.ShowDialog(this);
        }

        private string ShortVersion
        {
            get
            {
                var version = Assembly.GetExecutingAssembly().GetName().Version;
                return $"{version.Major}.{version.Minor}.{version.Build}";
            }
        }

        private static string _buildHash;
        public static async Task<string> CalculateBuildHashAsync()
        {
            if (_buildHash == null)
            {
                var buffer = new byte[4096];
                using (var file = new FileStream(Application.ExecutablePath, FileMode.Open, FileAccess.Read, FileShare.Read,
                    buffer.Length, FileOptions.Asynchronous | FileOptions.SequentialScan))
                using (var sha = SHA1.Create())
                {
                    byte[] hash;

                    int offset = 0;
                    int bytesRead = 0;
                    while (true)
                    {
                        // There's a really weird bug with .NET 4.7 on Windows 10, causing ReadAsync
                        // to incorrectly return 0 bytes once after each correct full-buffer read :S
                        bytesRead = await file.ReadAsync(buffer, offset, buffer.Length - bytesRead);
                        if (bytesRead == 0 && file.Position != file.Length)
                        {
                            continue;
                        }

                        offset += bytesRead;

                        // Results must be deterministic, so we must guarantee a complete buffer each time
                        if (bytesRead != 0 && bytesRead != buffer.Length)
                        {
                            continue;
                        }

                        if (bytesRead == 0)
                        {
                            sha.TransformFinalBlock(buffer, 0, offset);
                            hash = sha.Hash;
                            break;
                        }
                        else
                        {
                            sha.TransformBlock(buffer, 0, offset, buffer, 0);
                            // Reset offset for next round
                            offset = 0;
                        }
                    }
                    _buildHash = BitConverter.ToString(hash, 0).Replace("-", "").ToLower().Substring(0, 20);
                }
            }

            return _buildHash;
        }

        private void text_SelectionChanged(Object sender, EventArgs e)
        {
            deleteToolStripMenuItem.Enabled = text.TextSelected;
            cutToolStripMenuItem.Enabled = text.TextSelected;
            copyToolStripMenuItem.Enabled = text.TextSelected;
        }

        private async Task<bool> MaybeSaveAsync()
        {
            return await MaybeSaveAndGetStatusAsync() != DocumentChangeStatus.ChangedNotSaved;
        }

        enum DocumentChangeStatus
        {
            NoChanges,
            ChangedAndSaved,
            ChangedNotSaved,
        }

        private async Task<DocumentChangeStatus> MaybeSaveAndGetStatusAsync()
        {
            if (_ignoreChanges)
            {
                return DocumentChangeStatus.NoChanges;
            }

            if (RecoveryManager.ShutdownInProgress)
            {
                RecoveryManager.ShutdownDumpComplete.Wait();
                return DocumentChangeStatus.NoChanges;
            }

            if (DocumentChanged)
            {
                var result = MessageBox.Show(this,
                    "The document has unsaved changes. Do you want to save changes before closing?", "Save changes?",
                    MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);

                if (result == DialogResult.Cancel)
                {
                    return DocumentChangeStatus.ChangedNotSaved;
                }
                if (result == DialogResult.Yes)
                {
                    if (!await SaveAsync())
                    {
                        return DocumentChangeStatus.ChangedNotSaved;
                    }
                }
            }

            return DocumentChangeStatus.ChangedAndSaved;
        }

        private async void Form1_FormClosing(Object sender, FormClosingEventArgs e)
        {
            if (!await MaybeSaveAsync())
            {
                e.Cancel = true;
            }

            SavePreferences();
        }

        private async void Form1_Shown(object sender, EventArgs e)
        {
        }

        private async void OnCmdKey(Keys keyData)
        {
            if (_shortcuts.TryGetValue(keyData, out var action))
            {
                await action();
            }
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

        private void ResetStatus(object sender, EventArgs e)
        {
            Invoke((Action)(() =>
            {
                lock (_statusTimerLock)
                {
                    lblStatus1.Text = " ";
                    (sender as System.Windows.Forms.Timer).Stop();
                    (sender as System.Windows.Forms.Timer).Dispose();
                }
            }));
        }

        private void SetStatus(string message, TimeSpan timeout)
        {
            lock (_statusTimerLock)
            {
                Invoke((Action)(() =>
                {
                    lblStatus1.Text = message;

                    //Make sure to cancel old timers
                    _statusTimer?.Stop();
                    _statusTimer?.Dispose();

                    _statusTimer = new System.Windows.Forms.Timer();

                    _statusTimer.Interval = (int)timeout.TotalMilliseconds;
                    _statusTimer.Tick += ResetStatus;
                    _statusTimer.Start();
                }));
            }
        }

        private async void DragDropHandler(object sender, DragEventArgs e)
        {
            var inNewWindow = DocumentChanged || text.Text.Length != 0;
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
                    await OpenAsync(path);
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
