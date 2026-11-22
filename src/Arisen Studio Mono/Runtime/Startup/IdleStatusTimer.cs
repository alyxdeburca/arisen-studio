using System;
using System.Threading;

namespace ArisenStudio.Runtime.Startup
{
    /// <summary>
    /// The 15 second idle-status timer. Raises <see cref="Idle" /> every <see cref="DefaultInterval" /> while running,
    /// so the host can set the status text to the "STATUS_IDLING" string.
    /// Ported from MainWindow (old source: Forms/Windows/MainWindow.cs:150 declaration, 472-474 setup with
    /// Interval = 15000 and Enabled = true, 10475-10478 restart, 10510-10513 tick handler).
    /// Toolkit-free: no WinForms or Eto timer. Nothing is created until <see cref="Start" /> is called.
    /// Testable: pass a clock and useBackgroundTimer = false, then drive it with <see cref="Poll" />.
    /// </summary>
    public sealed class IdleStatusTimer : IDisposable
    {
        /// <summary>
        /// Interval of the old timer (StatusIdleTimer.Interval = 15000).
        /// </summary>
        public static readonly TimeSpan DefaultInterval = TimeSpan.FromMilliseconds(15000);

        private readonly object _gate = new object();
        private readonly Func<DateTime> _clock;
        private readonly bool _useBackgroundTimer;
        private System.Threading.Timer _timer;
        private bool _running;
        private bool _disposed;
        private int _generation;
        private DateTime _nextDue;

        /// <summary>
        /// Creates a stopped timer. Opens nothing.
        /// </summary>
        /// <param name="interval">Tick interval. Null uses <see cref="DefaultInterval" />. Must be greater than zero.</param>
        /// <param name="clock">Clock used by <see cref="Poll" />. Null uses DateTime.UtcNow.</param>
        /// <param name="useBackgroundTimer">
        /// True creates a System.Threading.Timer in <see cref="Start" /> that raises <see cref="Idle" /> on a thread-pool thread.
        /// False creates no timer, and the caller drives the timer with <see cref="Poll" /> (tests, or a UI timer).
        /// </param>
        public IdleStatusTimer(TimeSpan? interval = null, Func<DateTime> clock = null, bool useBackgroundTimer = true)
        {
            TimeSpan value = interval ?? DefaultInterval;

            if (value <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException("interval", "The interval must be greater than zero.");
            }

            Interval = value;
            _clock = clock ?? (() => DateTime.UtcNow);
            _useBackgroundTimer = useBackgroundTimer;
        }

        /// <summary>
        /// Raised on every tick while the timer runs. With the background timer it is raised on a thread-pool thread,
        /// not the UI thread, so marshal to the UI thread in the handler. With <see cref="Poll" /> it is raised on the caller's thread.
        /// </summary>
        public event EventHandler Idle;

        /// <summary>
        /// Tick interval.
        /// </summary>
        public TimeSpan Interval { get; private set; }

        /// <summary>
        /// True between <see cref="Start" /> and <see cref="Stop" />.
        /// </summary>
        public bool IsRunning
        {
            get
            {
                lock (_gate)
                {
                    return _running;
                }
            }
        }

        /// <summary>
        /// The old restart rule (MainWindow.SetStatus, lines 10475-10478): the timer is restarted only when an exception
        /// accompanies the status and the status is not the idling text. A plain status change without an exception
        /// does NOT reset the timer, which matches the old app (quirk recorded, not fixed).
        /// </summary>
        /// <param name="status">The status text being set.</param>
        /// <param name="idlingText">The localised "STATUS_IDLING" text.</param>
        /// <param name="exception">The exception passed to SetStatus, or null.</param>
        public static bool ShouldRestart(string status, string idlingText, Exception exception)
        {
            return exception != null && status != idlingText;
        }

        /// <summary>
        /// Restarts the timer when <see cref="ShouldRestart" /> says so. Returns true when it restarted.
        /// </summary>
        public bool RestartIfNeeded(string status, string idlingText, Exception exception)
        {
            if (!ShouldRestart(status, idlingText, exception))
            {
                return false;
            }

            Restart();
            return true;
        }

        /// <summary>
        /// Starts the timer. Does nothing when it is already running (setting Timer.Enabled = true twice did nothing either).
        /// The first tick comes one interval after the call, as with the old timer.
        /// </summary>
        public void Start()
        {
            lock (_gate)
            {
                ThrowIfDisposed();

                if (_running)
                {
                    return;
                }

                _running = true;
                _generation++;
                _nextDue = _clock() + Interval;

                if (_useBackgroundTimer)
                {
                    int generation = _generation;
                    _timer = new System.Threading.Timer(_ => OnBackgroundTick(generation), null, Interval, Interval);
                }
            }
        }

        /// <summary>
        /// Stops the timer. A tick already in flight is dropped. Safe to call when stopped.
        /// </summary>
        public void Stop()
        {
            lock (_gate)
            {
                StopLocked();
            }
        }

        /// <summary>
        /// Stop followed by Start, so the next tick is one full interval away (the old Stop(); Start(); pair).
        /// </summary>
        public void Restart()
        {
            lock (_gate)
            {
                ThrowIfDisposed();
                StopLocked();
            }

            Start();
        }

        /// <summary>
        /// Clock-driven tick for tests or a host UI timer. Raises <see cref="Idle" /> and returns true when the timer is running
        /// and a full interval has passed since the last tick (or the start). Missed ticks are not replayed: at most one
        /// tick is raised per call, and the next one is one interval after now (a WinForms timer coalesced ticks as well).
        /// </summary>
        public bool Poll()
        {
            lock (_gate)
            {
                if (_disposed || !_running)
                {
                    return false;
                }

                DateTime now = _clock();

                // Clock moved backwards: do not wait longer than one interval.
                if (_nextDue - now > Interval)
                {
                    _nextDue = now + Interval;
                }

                if (now < _nextDue)
                {
                    return false;
                }

                _nextDue = now + Interval;
            }

            RaiseIdle();
            return true;
        }

        /// <summary>
        /// Time left until the next tick, or null when stopped. Never negative.
        /// </summary>
        public TimeSpan? TimeUntilNextTick()
        {
            lock (_gate)
            {
                if (!_running)
                {
                    return null;
                }

                TimeSpan left = _nextDue - _clock();
                return left < TimeSpan.Zero ? TimeSpan.Zero : left;
            }
        }

        /// <summary>
        /// Stops the timer and releases the background timer. The instance cannot be started again.
        /// </summary>
        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                StopLocked();
                _disposed = true;
            }
        }

        private void OnBackgroundTick(int generation)
        {
            lock (_gate)
            {
                // Drop ticks from a timer that was stopped or replaced.
                if (_disposed || !_running || generation != _generation)
                {
                    return;
                }

                _nextDue = _clock() + Interval;
            }

            try
            {
                RaiseIdle();
            }
            catch (Exception)
            {
                // An exception in a handler must not kill the process from a thread-pool thread.
                // The old UI-thread timer routed it to Application.ThreadException; the host should catch inside its handler.
            }
        }

        private void RaiseIdle()
        {
            EventHandler handler = Idle;

            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        private void StopLocked()
        {
            _running = false;
            _generation++;

            if (_timer != null)
            {
                _timer.Dispose();
                _timer = null;
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException("IdleStatusTimer");
            }
        }
    }
}
