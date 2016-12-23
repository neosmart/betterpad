using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace betterpad
{
    class WindowManager : ApplicationContext
    {
        private static List<Form1> _activeWindows = new List<Form1>();
        private static Queue<Tuple<Action<Form1>, Action<Form1>>> WindowQueue = new Queue<Tuple<Action<Form1>, Action<Form1>>>();
        private static Semaphore CreateWindowEvent = new Semaphore(1, 256);
        private static ManualResetEvent AllWindowsClosed = new ManualResetEvent(false);
        private static ManualResetEvent CloseAll = new ManualResetEvent(false);
        private int ThreadCounter = 0;

        private static void ThreadRunner()
        {

        }

        public WindowManager()
        {
            //new window handler for default entity
            WindowQueue.Enqueue(new Tuple<Action<Form1>, Action<Form1>>(null, null));

            var waitHandles = new WaitHandle [] { CreateWindowEvent, AllWindowsClosed, CloseAll };
            bool done = false;
            while (!done)
            {
                switch (WaitHandle.WaitAny(waitHandles))
                {
                    case 0:
                        var thread = new Thread(() =>
                        {
                            Interlocked.Increment(ref ThreadCounter);
                            var form = new Form1();
                            var handler = WindowQueue.Dequeue();
                            handler.Item1?.Invoke(form);
                            form.StartAction = handler.Item2;
                            form.ShowDialog();
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

        public static void CreateNewWindow(Action<Form1> beforeShow = null, Action<Form1> afterShow = null)
        {
            WindowQueue.Enqueue(new Tuple<Action<Form1>, Action<Form1>>(beforeShow, afterShow));
            CreateWindowEvent.Release();
        }
    }
}
