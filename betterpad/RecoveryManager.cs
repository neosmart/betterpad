using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace betterpad
{
    class RecoveryManager
    {
        private string _lastDumpDirectory = null;

        [DllImport("kernel32.dll")]
        static extern uint RegisterApplicationRecoveryCallback(IntPtr pRecoveryCallback, IntPtr pvParameter, int dwPingInterval, int dwFlags);

        [DllImport("kernel32.dll")]
        static extern uint ApplicationRecoveryInProgress(out bool pbCancelled);

        [DllImport("kernel32.dll")]
        static extern uint ApplicationRecoveryFinished(bool bSuccess);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern int RegisterApplicationRestart([MarshalAs(UnmanagedType.LPWStr)] string commandLineArgs, int Flags);

        delegate int ApplicationRecoveryCallback(IntPtr pvParameter);

        public void RegisterRecovery()
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode‌​.ThrowException);
            var callback = new ApplicationRecoveryCallback(CreateRecoveryData);
            IntPtr del = Marshal.GetFunctionPointerForDelegate(callback);
            RegisterApplicationRecoveryCallback(del, IntPtr.Zero, 5000, 0);
        }

        public int CreateRecoveryData()
        {
            return CreateRecoveryData(IntPtr.Zero);
        }

        public int CreateRecoveryData(IntPtr param)
        {
            try
            {
                var tempDir = Path.Combine(Path.GetTempPath(), "betterpad-" + Path.GetRandomFileName());
                if (!Directory.Exists(tempDir))
                {
                    Directory.CreateDirectory(tempDir);
                }

                foreach (var form in WindowManager.ActiveDocuments)
                {
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

                //If we already backed up before, delete the old backup
                if (!string.IsNullOrEmpty(_lastDumpDirectory))
                {
                    try
                    {
                        Directory.Delete(_lastDumpDirectory, true);
                    }
                    catch { }
                }
                _lastDumpDirectory = tempDir;
            }
            catch { }


            if (!string.IsNullOrEmpty(_lastDumpDirectory))
            {
                //Now ask Windows to restart us at next run
                RegisterApplicationRestart($"/recover {_lastDumpDirectory}", 0);
                ApplicationRecoveryFinished(true);
                return 0;
            }
            else
            {
                ApplicationRecoveryFinished(false);
                return -1;
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
                            AfterShow = (form) =>
                            {
                                if (!string.IsNullOrEmpty(recoveryInfo.FilePath))
                                {
                                    form.Open(recoveryInfo.FilePath);
                                }
                                form.BetterBox.Text = recoveryInfo.Text;
                                form.BetterBox.SelectionStart = recoveryInfo.Position;
                            }
                        });
                    }
                }
                catch { }
            }

            try
            {
                Directory.Delete(recoveryPath, true);
            }
            catch { }

            return true;
        }

        const int WM_QUERYENDSESSION = 0x11;
        const int ENDSESSION_CLOSEAPP = 1;
        const int WM_ENDSESSION = 22;

        public static bool MessageHandler(ref Message m)
        {
            if (m.Msg == WM_QUERYENDSESSION || m.Msg == WM_ENDSESSION)
            {
                m.Result = new IntPtr(ENDSESSION_CLOSEAPP);
                return true;
            }

            return false;
        }
    }
}
