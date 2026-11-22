using System;
using System.Threading;

namespace ArisenStudio.Runtime.Startup
{
    /// <summary>
    /// Single-instance guard: a named Mutex, the same name the old app uses.
    /// Ported from Program (old source: Program.cs:16 "public static Mutex Mutex = new(true, "Arisen Studio")",
    /// 25 "if (Mutex.WaitOne(TimeSpan.Zero, true))", 42-43 "Mutex.ReleaseMutex(); Mutex.Dispose();").
    /// Constructing it creates no OS object: the mutex is created in <see cref="TryAcquire" />.
    /// Thread rule: a Mutex is owned by the thread that acquired it, so call <see cref="TryAcquire" /> and
    /// <see cref="Dispose" /> on the same thread (the Main thread, as the old Program did).
    /// There is no finalizer: the OS frees the mutex when the process ends, and <see cref="Dispose" /> releases it earlier.
    /// </summary>
    public sealed class SingleInstanceGuard : IDisposable
    {
        /// <summary>
        /// The mutex name used by the old app. No "Global\" prefix, as in the old code.
        /// </summary>
        public const string DefaultName = "Arisen Studio";

        private readonly object _gate = new object();
        private Mutex _mutex;
        private int _ownerThreadId;
        private bool _acquired;
        private bool _disposed;

        /// <summary>
        /// Creates a guard. Opens nothing.
        /// </summary>
        /// <param name="name">Mutex name. Null or empty uses <see cref="DefaultName" />. Only tests should pass another name.</param>
        public SingleInstanceGuard(string name = null)
        {
            Name = string.IsNullOrEmpty(name) ? DefaultName : name;
        }

        /// <summary>
        /// The mutex name.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// True when this instance owns the mutex, so it is the only running copy.
        /// </summary>
        public bool IsAcquired
        {
            get
            {
                lock (_gate)
                {
                    return _acquired;
                }
            }
        }

        /// <summary>
        /// Tries to become the single instance. Returns true when no other instance holds the mutex.
        /// Returns true again on a repeat call once acquired. After a false result the handle is closed and the
        /// next call tries again. An abandoned mutex (the previous instance crashed) counts as acquired.
        /// </summary>
        public bool TryAcquire()
        {
            lock (_gate)
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException("SingleInstanceGuard");
                }

                if (_acquired)
                {
                    return true;
                }

                // initiallyOwned: false, then a zero-wait acquire. The old code created it owned and then called WaitOne(0),
                // which for the first instance acquired it twice on the same thread; the result (first instance wins) is the same.
                Mutex mutex = new Mutex(false, Name);
                bool acquired;

                try
                {
                    acquired = mutex.WaitOne(TimeSpan.Zero, false);
                }
                catch (AbandonedMutexException)
                {
                    // The previous owner ended without releasing. This thread now owns the mutex.
                    acquired = true;
                }

                if (!acquired)
                {
                    mutex.Dispose();
                    return false;
                }

                _mutex = mutex;
                _ownerThreadId = Thread.CurrentThread.ManagedThreadId;
                _acquired = true;
                return true;
            }
        }

        /// <summary>
        /// Releases the mutex (when owned) and closes the handle. Safe to call more than once and when never acquired.
        /// When called from a thread other than the one that acquired, the release is skipped (a Mutex can only be
        /// released by its owner) and the handle is closed; the OS then frees the mutex when the owner thread or process ends.
        /// </summary>
        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;

                if (_mutex == null)
                {
                    return;
                }

                if (_acquired && _ownerThreadId == Thread.CurrentThread.ManagedThreadId)
                {
                    try
                    {
                        _mutex.ReleaseMutex();
                    }
                    catch (ApplicationException)
                    {
                        // Not owned any more. Nothing to release.
                    }
                }

                _mutex.Dispose();
                _mutex = null;
                _acquired = false;
            }
        }
    }
}
