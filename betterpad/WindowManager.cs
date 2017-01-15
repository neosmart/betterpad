using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace betterpad
{
    class WindowManager : ApplicationContext
    {
        public struct NewFormActions
        {
            public Action<Form1> BeforeShow;
            public Action<Form1> AfterShow;
        }

        private static List<Form1> _activeWindows = new List<Form1>();
        public static IEnumerable<Form1> ActiveDocuments => _activeWindows;
        private static Queue<NewFormActions> WindowQueue = new Queue<NewFormActions>();
        private static Semaphore CreateWindowEvent = new Semaphore(0, 256);
        private static ManualResetEvent AllWindowsClosed = new ManualResetEvent(false);
        private static ManualResetEvent CloseAll = new ManualResetEvent(false);
        private int ThreadCounter = 0;
        private static RecoveryManager _recoveryManager = new RecoveryManager();

        private static void ThreadRunner()
        {

        }

        static public void CreateDump()
        {
            _recoveryManager.CreateRecoveryData();
        }

        public WindowManager()
        {
            var args = Environment.GetCommandLineArgs().Skip(1).ToArray();

            //Create recovery manager
            _recoveryManager.RegisterRecovery();

            //Create a backup every x seconds for recovery purposes
            var backupTimer = new System.Threading.Timer((o) =>
            {
                _recoveryManager.CreateRecoveryData();
            }, null, 10 * 1000, 60 * 1000);

            //Check if recovery needed
            if (args.Length >= 2 && args[0] == "/recover")
            {
                var recoveryPath = args[1];
                _recoveryManager.Recover(recoveryPath);
            }
            else
            {
                foreach (var path in args)
                {
                    if (!File.Exists(path))
                    {
                        try
                        {
                            File.Create(path);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Error creating file at path {path}\r\n\r\n{ex.Message}", "Error opening file!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            continue;
                        }
                    }

                    var openAction = new NewFormActions()
                    {
                        AfterShow = (f) =>
                        {
                            f.Open(path);
                            f.Focus();
                        }
                    };
                    CreateNewWindow(openAction);
                }
            }

            //new window handler for default entity
            if (!WindowQueue.Any())
            {
                CreateNewWindow();
            }

            var waitHandles = new WaitHandle [] { CreateWindowEvent, AllWindowsClosed, CloseAll };
            bool done = false;
            AppDomain.CurrentDomain.ProcessExit += (sender, e) =>
            {
                while (!done) ;
            };

            while (!done)
            {
                switch (WaitHandle.WaitAny(waitHandles))
                {
                    case 0:
                        var thread = new Thread(() =>
                        {
                            Interlocked.Increment(ref ThreadCounter);
                            if (WindowQueue.Any())
                            {
                                using (var form = new Form1())
                                {
                                    _activeWindows.Add(form);
                                    var handler = WindowQueue.Dequeue();
                                    handler.BeforeShow?.Invoke(form);
                                    form.StartAction = handler.AfterShow;
                                    form.ShowDialog();
                                    _activeWindows.Remove(form);
                                }
                            }
                            if (Interlocked.Decrement(ref ThreadCounter) == 0)
                            {
                                AllWindowsClosed.Set();
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
        }

        public static void CreateNewWindow(NewFormActions actions = new NewFormActions())
        {
            WindowQueue.Enqueue(actions);
            CreateWindowEvent.Release();
        }
    }
}
