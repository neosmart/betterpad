using System;
using System.Threading;

namespace betterpad
{
    public class ScopedMutex : IDisposable
    {
        private readonly Mutex _mutex;
        private bool _locked;
        public bool SafeWait { get; set; }

        public ScopedMutex(string name)
        {
            _mutex = new Mutex(false, name);
            _locked = false;
            SafeWait = true;
        }

        public ScopedMutex(bool initiallyOwned, string name)
            : this(name)
        {
            if (initiallyOwned)
            {
                WaitOne();
            }
        }

        public bool WaitOne()
        {
            try
            {
                _locked = true; //Regardless of AbandonedMutexException
                _mutex.WaitOne();
            }
            catch (AbandonedMutexException)
            {
                if (!SafeWait)
                {
                    throw;
                }
            }

            return true;
        }

        public void ReleaseMutex()
        {
            _mutex.ReleaseMutex();
            _locked = false;
        }

        public void Dispose()
        {
            if (_locked)
            {
                ReleaseMutex();
            }
            _mutex.Dispose();
        }
    }
}