using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace betterpad
{
    class RecoveryManager
    {
        private string _lastDumpDirectory = null;
        public static bool ShutdownInitiated { get; private set; }
        public static ManualResetEventSlim ShutdownDumpComplete = new ManualResetEventSlim(false);
        private static string _appName;
        private static string _appCompany;

        [DllImport("kernel32.dll")]
        static extern uint RegisterApplicationRecoveryCallback(IntPtr pRecoveryCallback, IntPtr pvParameter, int dwPingInterval, int dwFlags);

        [DllImport("kernel32.dll")]
        static extern uint ApplicationRecoveryInProgress(out bool pbCancelled);

        [DllImport("kernel32.dll")]
        static extern uint ApplicationRecoveryFinished(bool bSuccess);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern int RegisterApplicationRestart([MarshalAs(UnmanagedType.LPWStr)] string commandLineArgs, int Flags);

        static int SM_SHUTTINGDOWN = 0x2000;

        [DllImport("user32.dll")]
        static extern bool GetSystemMetrics(int metric);

        public static bool ShutdownInProgress => GetSystemMetrics(SM_SHUTTINGDOWN) || ShutdownInitiated;

        delegate int ApplicationRecoveryCallback(IntPtr pvParameter);

        public void RegisterRecovery()
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode‌​.ThrowException);
            var callback = new ApplicationRecoveryCallback(CreateRecoveryData);
            IntPtr del = Marshal.GetFunctionPointerForDelegate(callback);
            RegisterApplicationRecoveryCallback(del, IntPtr.Zero, 5000, 0);

            if (_appName == null)
            {
                using (var process = Process.GetCurrentProcess())
                {
                    var path = process.MainModule.FileName;
                    _appName = Path.GetFileNameWithoutExtension(path);
                    _appCompany = FileVersionInfo.GetVersionInfo(path).CompanyName;
                    if (string.IsNullOrWhiteSpace(_appName))
                    {
                        _appCompany = "{EC8B5FBC-2565-4CBD-9289-0000F25C1DFB}";
                    }
                }
            }
        }

        public int CreateRecoveryData(IntPtr param)
        {
            return CreateRecoveryData(null);
        }

        public void CleanUp()
        {
            if (Directory.Exists(UnsafeShutdownPath))
            {
                try
                {
                    Directory.Delete(UnsafeShutdownPath, true);
                }
                catch { }
            }
        }

        public int CreateRecoveryData(string path)
        {
            lock (this)
            {
                //Protect against automated/other dumps when a shutdown dump has been triggered
                if (ShutdownInitiated && string.IsNullOrWhiteSpace(path))
                {
                    return 0;
                }

                string recoveryPath = null;
                try
                {
                    IEnumerable<string> oldFiles = null;
                    var tempDir = path ?? Path.Combine(Path.GetTempPath(), _appName + "-" + Path.GetRandomFileName());
                    if (!Directory.Exists(tempDir))
                    {
                        Directory.CreateDirectory(tempDir);
                    }
                    else
                    {
                        oldFiles = Directory.GetFiles(tempDir);
                    }

                    foreach (var form in WindowManager.ActiveDocuments.ToArray())
                    {
                        if (!form.RecoveryEnabled)
                        {
                            continue;
                        }

                        //try...catch here so we can attempt to salvage at least one document
                        try
                        {
                            var recoveryInfo = new RecoveryInfo();

                            //We have to use special methods to prevent .NET blocking us from accessing another thread
                            recoveryInfo.FilePath = form.FilePath;
                            recoveryInfo.Position = form.BetterBox.Win32SelectionStart;
                            recoveryInfo.Text = form.BetterBox.Win32Text;

                            var serializationPath = Path.Combine(tempDir, Path.GetRandomFileName());
                            using (var stream = new FileStream(serializationPath, FileMode.CreateNew))
                            {
                                var bf = new BinaryFormatter();
                                bf.Serialize(stream, recoveryInfo);
                            }
                        }
                        catch { }

                        if (Heartbeat())
                        {
                            //recovery aborted
                            try
                            {
                                Directory.Delete(tempDir, true);
                            }
                            catch { }
                            return 1;
                        }
                    }

                    //clear old backups from the same directory
                    if (oldFiles != null)
                    {
                        foreach (var f in oldFiles)
                        {
                            File.Delete(f);
                        }
                    }

                    recoveryPath = tempDir;
                    if (path == null)
                    {
                        //recovery path was automatically generated
                        if (!string.IsNullOrEmpty(_lastDumpDirectory))
                        {
                            try
                            {
                                Directory.Delete(_lastDumpDirectory, true);
                            }
                            catch { }
                        }
                        _lastDumpDirectory = recoveryPath;
                    }
                }
                catch
                {
                    recoveryPath = null;
                }

                GC.Collect();

                if (!string.IsNullOrEmpty(recoveryPath))
                {
                    //Now ask Windows to restart us at next run
                    RegisterApplicationRestart($"/recover {recoveryPath}", 0);
                    ApplicationRecoveryFinished(true);
                    return 0;
                }
                else
                {
                    ApplicationRecoveryFinished(false);
                    return -1;
                }
            }
        }

        private bool Heartbeat()
        {
            bool cancel;
            ApplicationRecoveryInProgress(out cancel);
            if (cancel)
            {
                //abort recovery
            }
            return cancel;
        }

        public bool Recover(string recoveryPath)
        {
            if (!Directory.Exists(recoveryPath))
            {
                return false;
            }

            foreach (var file in Directory.EnumerateFiles(recoveryPath))
            {
                try
                {
                    var bf = new BinaryFormatter();
                    using (var stream = new FileStream(file, FileMode.Open))
                    {
                        var recoveryInfo = (RecoveryInfo)bf.Deserialize(stream);

                        WindowManager.CreateNewWindow(new WindowManager.NewFormActions()
                        {
                            AfterShow = async (form) =>
                            {
                                if (!string.IsNullOrEmpty(recoveryInfo.FilePath))
                                {
                                    await form.OpenAsync(recoveryInfo.FilePath);
                                }

                                //only restore text if backup is newer than destination (or destination does not exist)
                                var lastWrite = File.Exists(recoveryInfo.FilePath) ? File.GetLastWriteTimeUtc(recoveryInfo.FilePath) : (DateTime?)null;
                                var backupWrite = File.GetLastWriteTimeUtc(file);

                                if (lastWrite == null || lastWrite < backupWrite)
                                {
                                    form.BetterBox.Text = recoveryInfo.Text;
                                    form.BetterBox.SelectionStart = recoveryInfo.Position;
                                }
                            }
                        });
                    }
                }
                catch { }
            }

            try
            {
                GC.Collect();
                //Don't delete recovery data until new recovery data is available
                //Directory.Delete(recoveryPath, true);
            }
            catch { }

            return true;
        }

        public string UnsafeShutdownPath
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), _appCompany, _appName, "unsafe-recovery");
            }
        }

        public bool UnsafeShutdown => Directory.Exists(UnsafeShutdownPath);

        const int WM_QUERYENDSESSION = 0x11;
        const int ENDSESSION_CLOSEAPP = 1;
        const int WM_ENDSESSION = 22;

        public static bool MessageHandler(ref Message m)
        {
            if (m.Msg == WM_ENDSESSION || (m.Msg == WM_QUERYENDSESSION && ShutdownInitiated))
            {
                m.Result = new IntPtr(ENDSESSION_CLOSEAPP);
                return true;
            }
            else if (m.Msg == WM_QUERYENDSESSION)
            {
                //system getting ready to shut down
                //individual *forms* receive this message, so we must protect against a race condition
                using (var shutdownMutex = new ScopedMutex(_appName + "{EA433526-9724-43FD-B175-6EA7BA7517A4}"))
                {
                    if (ShutdownInitiated)
                    {
                        m.Result = new IntPtr(ENDSESSION_CLOSEAPP);
                        return true;
                    }

                    ShutdownInitiated = true;
                    var recoveryMan = new RecoveryManager();
                    recoveryMan.CreateRecoveryData(recoveryMan.UnsafeShutdownPath);
                    ShutdownDumpComplete.Set();
                    m.Result = new IntPtr(ENDSESSION_CLOSEAPP);
                    return true;
                }
            }

            return false;
        }
    }
}
