using ArisenStudio.Extensions;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ArisenStudio.Runtime.Startup
{
    /// <summary>
    /// Carries one current-game reading from <see cref="CurrentGamePoller" />.
    /// </summary>
    public sealed class CurrentGameEventArgs : EventArgs
    {
        public CurrentGameEventArgs(string caption, string rawGame, bool changed)
        {
            Caption = caption;
            RawGame = rawGame;
            Changed = changed;
        }

        /// <summary>
        /// The text for the "current game" status label ("XMB Menu", "webMAN Required" or the game title).
        /// </summary>
        public string Caption { get; private set; }

        /// <summary>
        /// What webMAN returned before the fallbacks (may be null or empty).
        /// </summary>
        public string RawGame { get; private set; }

        /// <summary>
        /// True when <see cref="Caption" /> differs from the previous reading. The old handler set the caption on every tick.
        /// </summary>
        public bool Changed { get; private set; }
    }

    /// <summary>
    /// The 1.5 second current-game poll (PS3 via webMAN).
    /// Ported from MainWindow (old source: Forms/Windows/MainWindow.Designer.cs:13745 Interval = 1500,
    /// Forms/Windows/MainWindow.cs:10421-10440 TimerCurrentGame_Tick PS3 branch, 5242-5248 and 10402-10415 enable/disable rules).
    /// Constructing it opens nothing and starts nothing: the timer exists only after <see cref="Start" />,
    /// and the webMAN request is made only when a tick runs.
    /// </summary>
    public sealed class CurrentGamePoller : IDisposable
    {
        /// <summary>
        /// Interval of the old timer (TimerCurrentGame.Interval = 1500).
        /// </summary>
        public static readonly TimeSpan DefaultInterval = TimeSpan.FromMilliseconds(1500);

        /// <summary>
        /// Caption when webMAN is installed but returns no game (old literal).
        /// </summary>
        public const string XmbMenuCaption = "XMB Menu";

        /// <summary>
        /// Caption when webMAN is not installed (old literal).
        /// </summary>
        public const string WebManRequiredCaption = "webMAN Required";

        private readonly object _gate = new object();
        private readonly Func<string> _getAddress;
        private readonly Func<bool> _isWebManInstalled;
        private readonly Func<string, Task<string>> _fetchGame;
        private System.Threading.Timer _timer;
        private SynchronizationContext _context;
        private bool _running;
        private bool _disposed;
        private int _generation;
        private int _inFlight;
        private string _lastCaption;

        /// <summary>
        /// Creates a stopped poller. Stores the delegates only: no timer, no socket, no webMAN call.
        /// </summary>
        /// <param name="getAddress">Returns the console address, read on every tick (old: ConsoleProfile.Address).</param>
        /// <param name="isWebManInstalled">
        /// Returns the "webMAN installed" flag, read AFTER the fetch on every tick (old: MainWindow.IsWebManInstalled).
        /// It is a delegate on purpose: WebManExtensions.IsWebManInstalled(ip) switches the console LED and is not read-only.
        /// </param>
        /// <param name="fetchGame">
        /// Fetches the game for an address. Null uses WebManExtensions.GetGameAsync (looked up on the first tick, not here).
        /// </param>
        /// <param name="interval">Tick interval. Null uses <see cref="DefaultInterval" />. Must be greater than zero.</param>
        public CurrentGamePoller(Func<string> getAddress, Func<bool> isWebManInstalled, Func<string, Task<string>> fetchGame = null, TimeSpan? interval = null)
        {
            if (getAddress == null)
            {
                throw new ArgumentNullException("getAddress");
            }

            if (isWebManInstalled == null)
            {
                throw new ArgumentNullException("isWebManInstalled");
            }

            TimeSpan value = interval ?? DefaultInterval;

            if (value <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException("interval", "The interval must be greater than zero.");
            }

            _getAddress = getAddress;
            _isWebManInstalled = isWebManInstalled;
            _fetchGame = fetchGame;
            Interval = value;
        }

        /// <summary>
        /// Raised after every completed tick (see <see cref="CurrentGameEventArgs.Changed" />).
        /// Thread: if <see cref="Start" /> was called on a thread with a SynchronizationContext (a UI thread), the event is posted to it.
        /// Otherwise it is raised on the thread that finished the fetch (a thread-pool thread).
        /// </summary>
        public event EventHandler<CurrentGameEventArgs> GameRead;

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
        /// The old enable rule (MainWindow.cs:5242-5248 and 10402-10415): the poll runs only when a console is connected
        /// and Settings.AlwaysShowCurrentGamePlaying is on. It is off in every other case.
        /// </summary>
        public static bool ShouldRun(bool isConsoleConnected, bool alwaysShowCurrentGamePlaying)
        {
            return isConsoleConnected && alwaysShowCurrentGamePlaying;
        }

        /// <summary>
        /// Maps a webMAN reading to the caption, as the old PS3 branch did (MainWindow.cs:10425-10437):
        /// installed and blank gives "XMB Menu", installed with a title gives the title, not installed gives "webMAN Required".
        /// </summary>
        public static string ResolveCaption(string game, bool webManInstalled)
        {
            if (!webManInstalled)
            {
                return WebManRequiredCaption;
            }

            return string.IsNullOrWhiteSpace(game) ? XmbMenuCaption : game;
        }

        /// <summary>
        /// Starts or stops the poller from the two settings, using <see cref="ShouldRun" />.
        /// </summary>
        public void Apply(bool isConsoleConnected, bool alwaysShowCurrentGamePlaying)
        {
            if (ShouldRun(isConsoleConnected, alwaysShowCurrentGamePlaying))
            {
                Start();
            }
            else
            {
                Stop();
            }
        }

        /// <summary>
        /// Starts polling. The first tick comes one interval later (as with the old timer). Does nothing when already running.
        /// Captures SynchronizationContext.Current for the <see cref="GameRead" /> event.
        /// </summary>
        public void Start()
        {
            lock (_gate)
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException("CurrentGamePoller");
                }

                if (_running)
                {
                    return;
                }

                _running = true;
                _generation++;
                _context = SynchronizationContext.Current;
                int generation = _generation;
                _timer = new System.Threading.Timer(_ => OnTimer(generation), null, Interval, Interval);
            }
        }

        /// <summary>
        /// Stops polling. A fetch already in flight is dropped: its result raises no event (the old code let a late result
        /// overwrite the caption after the timer was disabled; this is a deviation, recorded in the build request).
        /// </summary>
        public void Stop()
        {
            lock (_gate)
            {
                StopLocked();
            }
        }

        /// <summary>
        /// Runs one tick now: reads the address, fetches the game, resolves the caption and raises <see cref="GameRead" />.
        /// Returns false when the tick was skipped (a previous tick is still running) or the result was dropped
        /// (stopped meanwhile when running; always raised when called while stopped, so tests and one-off reads work).
        /// </summary>
        public async Task<bool> TickAsync()
        {
            int generation;
            bool wasRunning;

            lock (_gate)
            {
                if (_disposed)
                {
                    return false;
                }

                generation = _generation;
                wasRunning = _running;
            }

            // No overlapping ticks: the old async-void handler could overlap on a slow console (HTTP timeout 100 s).
            if (Interlocked.CompareExchange(ref _inFlight, 1, 0) != 0)
            {
                return false;
            }

            try
            {
                string game;

                try
                {
                    Func<string, Task<string>> fetch = _fetchGame ?? (ip => WebManExtensions.GetGameAsync(ip));
                    game = await fetch(_getAddress()).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // WebManExtensions.GetGameAsync already swallows errors and returns an empty string. Do the same for an injected fetcher.
                    game = string.Empty;
                }

                // Read after the fetch, as the old handler did.
                string caption = ResolveCaption(game, _isWebManInstalled());
                bool changed;
                SynchronizationContext context;

                lock (_gate)
                {
                    if (_disposed || (wasRunning && (!_running || generation != _generation)))
                    {
                        return false;
                    }

                    changed = !string.Equals(caption, _lastCaption, StringComparison.Ordinal);
                    _lastCaption = caption;
                    context = _context;
                }

                Raise(context, new CurrentGameEventArgs(caption, game, changed), generation, wasRunning);
                return true;
            }
            finally
            {
                Interlocked.Exchange(ref _inFlight, 0);
            }
        }

        /// <summary>
        /// Stops the poller and releases the timer. The instance cannot be started again.
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

        private void OnTimer(int generation)
        {
            lock (_gate)
            {
                if (_disposed || !_running || generation != _generation)
                {
                    return;
                }
            }

            // Fire and forget, as the old async void handler. TickAsync does not throw for fetch errors;
            // a throwing GameRead handler is swallowed here so it cannot kill the process from a thread-pool thread.
            TickAsync().ContinueWith(t => { Exception ignored = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
        }

        private bool IsCurrent(int generation, bool wasRunning)
        {
            lock (_gate)
            {
                return !_disposed && (!wasRunning || (_running && generation == _generation));
            }
        }

        private void Raise(SynchronizationContext context, CurrentGameEventArgs args, int generation, bool wasRunning)
        {
            EventHandler<CurrentGameEventArgs> handler = GameRead;

            if (handler == null)
            {
                return;
            }

            if (context != null)
            {
                // Re-check on the target thread: Stop() may have run there between the post and this delegate.
                context.Post(_ =>
                {
                    if (IsCurrent(generation, wasRunning))
                    {
                        handler(this, args);
                    }
                }, null);
            }
            else
            {
                handler(this, args);
            }
        }

        private void StopLocked()
        {
            _running = false;
            _generation++;

            // A restart must report the first reading as changed again (the host resets the label to "None" on stop).
            _lastCaption = null;

            if (_timer != null)
            {
                _timer.Dispose();
                _timer = null;
            }
        }
    }
}
