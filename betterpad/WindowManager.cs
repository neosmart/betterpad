using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace betterpad
{
    class WindowManager : ApplicationContext
    {
        public struct NewFormActions
        {
            public Action<EditorWindow> BeforeShow;
            public Func<EditorWindow, Task> AfterShow;
        }

        private static List<EditorWindow> _activeWindows = new List<EditorWindow>();
        public static IEnumerable<EditorWindow> ActiveDocuments => _activeWindows;
        private static Queue<NewFormActions> WindowQueue = new Queue<NewFormActions>();
        private static Semaphore CreateWindowEvent = new Semaphore(0, 256);
        private static ManualResetEvent AllWindowsClosed = new ManualResetEvent(false);
        private static ManualResetEvent CloseAll = new ManualResetEvent(false);
        private static RecoveryManager _recoveryManager = new RecoveryManager();
        private int ThreadCounter = 0;

        public WindowManager()
        {
        }

        public async Task StartAsync()
        {
            var args = Environment.GetCommandLineArgs().Skip(1).ToArray();

            //Create recovery manager
            _recoveryManager.RegisterRecovery();

            //Check if recovery needed
            if (args.Length >= 2 && args[0] == "/recover")
            {
                var recoveryPath = args[1];
                _recoveryManager.Recover(recoveryPath);
                try
                {
                    Directory.Delete(recoveryPath, true);
                }
                catch { }
            }
            else
            {
                if (_recoveryManager.UnsafeShutdown)
                {
                    _recoveryManager.Recover(_recoveryManager.UnsafeShutdownPath);
                }
                //save unsafe shutdown data until a new backup exists or we exit cleanly
                //_recoveryManager.CleanUp();
                foreach (var path in args)
                {
                    OpenInNewWindow(path);
                }
            }

            //Create a backup every x seconds for recovery purposes, but only after any recovery finishes
            var backupTimer = new System.Threading.Timer((o) =>
            {
                _recoveryManager.CreateRecoveryData(_recoveryManager.UnsafeShutdownPath);
            }, null, 10 * 1000, 60 * 1000);

            //new window handler for default entity
            if (!WindowQueue.Any())
            {
                CreateNewWindow();
            }

            var waitHandles = new WaitHandle[] { CreateWindowEvent, AllWindowsClosed, CloseAll };
            bool done = false;
            AppDomain.CurrentDomain.ProcessExit += (sender, e) =>
            {
                while (!done)
                {
                    Thread.Yield();
                };
            };

            CancellationTokenSource cancelAll = new CancellationTokenSource();
            List<Task> activeWindows = new List<Task>();
            while (!done)
            {
                switch (WaitHandle.WaitAny(waitHandles))
                {
                    case 0:
                        // Threads registering drag-and-drop handlers must be STA
                        var thread = new Thread(() =>
                        {
                        Interlocked.Increment(ref ThreadCounter);
                        try
                        {
                            if (WindowQueue.Any())
                            {
                                using (var form = new EditorWindow()
                                {
                                    Cancel = cancelAll.Token
                                })
                                {
                                    _activeWindows.Add(form);
                                    var handler = WindowQueue.Dequeue();
                                    handler.BeforeShow?.Invoke(form);
                                        if (handler.AfterShow != null)
                                        {
                                            {
                                                form.Shown += (s, e) =>
                                                {
                                                    form.Invoke((Action)(async () =>
                                                    {
                                                        await handler.AfterShow(form);
                                                    }));
                                                };
                                            }
                                        }

                                        form.ShowDialog();
                                        _activeWindows.Remove(form);
                                    }
                                }
                            }
                            finally
                            {
                                if (Interlocked.Decrement(ref ThreadCounter) == 0)
                                {
                                    AllWindowsClosed.Set();
                                }
                            }
                        });
                        thread.SetApartmentState(ApartmentState.STA);
                        thread.Start();
                        break;
                    case 1:
                        done = true;
                        break;
                    case 2:
                        done = true;
                        break;
                }
            }

            cancelAll.Cancel();
            await Task.WhenAll(activeWindows);

            // Delete the unsafe shutdown folder if this is a clean shutdown
            if (!RecoveryManager.ShutdownInitiated && Directory.Exists(_recoveryManager.UnsafeShutdownPath))
            {
                try
                {
                    Directory.Delete(_recoveryManager.UnsafeShutdownPath, true);
                }
                catch { }
                {

                }
            }
        }

        private void OpenInNewWindow(string path)
        {
            var openAction = new NewFormActions()
            {
                AfterShow = async (f) =>
                {
                    await f.OpenAsync(path);
                    f.Focus();
                }
            };
            CreateNewWindow(openAction);
        }

        public static void CreateNewWindow(NewFormActions actions = new NewFormActions())
        {
            WindowQueue.Enqueue(actions);
            CreateWindowEvent.Release();
        }
    }
}
