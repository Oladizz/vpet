using System.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace VPet.SmartPet.Core
{
    /// <summary>
    /// Context-aware app usage tracker.
    ///
    /// Polls the active (foreground) window every second using Windows APIs,
    /// and records:
    ///   - how often each app is opened (launch count)
    ///   - how long you stay focused on each app (total and per session)
    ///   - your current app right now (live context for the pet)
    ///
    /// Data is persisted to usage.json and exposed to the pet brain so the
    /// pet can react ("You have been in VS Code for 2 hours, take a break!").
    /// This module is fully offline — it never calls any external API.
    /// </summary>
    public class AppUsageTracker
    {
        // ---------------- Win32 imports ----------------

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);

        // ---------------- State ----------------

        private readonly System.Timers.Timer _pollTimer;
        private uint _lastProcessId;
        private DateTime _sessionStart;
        private string _currentApp = "Desktop";
        private readonly object _lock = new();

        public UsageReport Report { get; } = new();

        /// <summary>The app that is currently in focus (updated every second).</summary>
        public string CurrentApp
        {
            get { lock (_lock) return _currentApp; }
            private set { lock (_lock) _currentApp = value; }
        }

        /// <summary>How long (seconds) the current app has been in focus in this session.</summary>
        public int CurrentSessionSeconds
        {
            get
            {
                lock (_lock)
                {
                    if (_lastProcessId == 0) return 0;
                    return (int)(DateTime.UtcNow - _sessionStart).TotalSeconds;
                }
            }
        }

        public AppUsageTracker()
        {
            _pollTimer = new System.Timers.Timer(1000) { AutoReset = true };
            _pollTimer.Elapsed += OnPoll;
            Load();
        }

        public void Start() => _pollTimer.Start();
        public void Stop()
        {
            _pollTimer.Stop();
            Persist();
        }

        // ---------------- Polling ----------------

        private void OnPoll(object? sender, System.Timers.ElapsedEventArgs e)
        {
            try
            {
                var hwnd = GetForegroundWindow();
                GetWindowThreadProcessId(hwnd, out uint pid);

                string appName = ResolveAppName(pid);

                lock (_lock)
                {
                    if (pid != _lastProcessId && pid != 0)
                    {
                        // The user switched to a different app -> count an "open".
                        Report.OnAppOpened(appName);
                        _sessionStart = DateTime.UtcNow;
                    }
                    else if (pid != 0)
                    {
                        // Same app, accumulate focus time.
                        Report.OnFocusTick(appName, seconds: 1);
                    }
                    else
                    {
                        // Nothing focused (e.g. desktop) — count as idle, not app time.
                        appName = "Desktop";
                    }

                    _lastProcessId = pid;
                    _currentApp = appName;
                }
            }
            catch
            {
                // Never let the tracker crash the plugin.
            }
        }

        private static readonly HashSet<string> _desktopProcessNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "explorer", "shellexperiencehost", "startmenuexperiencehost", "searchhost",
            "textinputhost", "lockapp", "applicationframehost"
        };

        /// <summary>
        /// Friendly app name from a process id. Skips shell/desktop processes
        /// so the pet reports real apps ("Chrome", "VS Code") instead of
        /// "explorer.exe".
        /// </summary>
        private static string ResolveAppName(uint pid)
        {
            if (pid == 0) return "Desktop";
            try
            {
                using var process = Process.GetProcessById((int)pid);
                var name = process.ProcessName;
                if (_desktopProcessNames.Contains(name))
                    return "Desktop";
                // Use MainModule file name when available for a friendlier label.
                try
                {
                    var path = process.MainModule?.FileName;
                    if (!string.IsNullOrEmpty(path))
                        return Path.GetFileNameWithoutExtension(path);
                }
                catch
                {
                    // Access denied on some protected processes — fall back.
                }
                return name;
            }
            catch
            {
                return "Unknown";
            }
        }

        // ---------------- Persistence ----------------

        public void Persist()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(PluginSettings.UsagePath)!);
                File.WriteAllText(PluginSettings.UsagePath,
                    JsonSerializer.Serialize(Report, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SmartPet] Usage persist failed: {ex.Message}");
            }
        }

        public void Load()
        {
            try
            {
                if (File.Exists(PluginSettings.UsagePath))
                {
                    var loaded = JsonSerializer.Deserialize<UsageReport>(File.ReadAllText(PluginSettings.UsagePath));
                    if (loaded != null)
                        loaded.CopyTo(Report);
                }
            }
            catch
            {
                // Corrupt file -> start fresh.
            }
        }
    }

    /// <summary>
    /// Serializable usage statistics: per-app open counts and focus durations.
    /// </summary>
    public class UsageReport
    {
        public Dictionary<string, AppStats> Apps { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public DateTime DayKey { get; set; } = DateTime.UtcNow.Date;

        public void OnAppOpened(string appName)
        {
            Get(appName).OpenCount++;
            Get(appName).LastOpened = DateTime.UtcNow;
        }

        public void OnFocusTick(string appName, int seconds)
        {
            var stats = Get(appName);
            stats.TotalFocusSeconds += seconds;
            stats.CurrentSessionSeconds += seconds;
            stats.LastSeen = DateTime.UtcNow;
        }

        /// <summary>Gets (or lazily creates) the stats entry for an app.</summary>
        private AppStats Get(string appName)
        {
            if (!Apps.TryGetValue(appName, out AppStats stats))
            {
                stats = new AppStats();
                Apps[appName] = stats;
            }
            return stats;
        }

        /// <summary>Daily rollover: keep rolling stats, start a fresh day marker.</summary>
        public void RolloverDayIfNeeded()
        {
            var today = DateTime.UtcNow.Date;
            if (DayKey != today)
            {
                DayKey = today;
                // Daily counters reset; lifetime counters are kept.
                foreach (var s in Apps.Values)
                {
                    s.DailyOpenCount = 0;
                    s.DailyFocusSeconds = 0;
                    s.CurrentSessionSeconds = 0;
                }
            }
        }

        public void CopyTo(UsageReport target)
        {
            target.Apps.Clear();
            foreach (var kv in Apps)
                target.Apps[kv.Key] = kv.Value;
            target.DayKey = DayKey;
        }

        /// <summary>One-line human-readable usage summary for pet speech bubbles.</summary>
        public string SummaryText()
        {
            if (Apps.Count == 0)
                return "No apps tracked yet — start using things and I'll keep score!";

            var top = MostUsedApp()!.Value;
            var topKey = Apps.First(kv => ReferenceEquals(kv.Value, top.Value)).Key;
            var totalApps = Apps.Count;
            var longest = FormatMinutes(top.Value.TotalFocusSeconds);
            return $"{totalApps} app{(totalApps == 1 ? "" : "s")} tracked. Your favorite is " +
                   $"{topKey} with {longest} of focus time across " +
                   $"{top.Value.OpenCount} sessions.";
        }

        private static string FormatMinutes(long seconds)
        {
            var hours = seconds / 3600;
            var minutes = (seconds % 3600) / 60;
            return hours > 0 ? $"{hours}h {minutes}m" : $"{minutes}m";
        }

        /// <summary>Top app by total focus time, for quick pet commentary.</summary>
        public KeyValuePair<string, AppStats>? MostUsedApp()
        {
            KeyValuePair<string, AppStats>? best = null;
            foreach (var kv in Apps)
            {
                if (best == null || kv.Value.TotalFocusSeconds > best.Value.Value.TotalFocusSeconds)
                    best = kv;
            }
            return best;
        }
    }

    public class AppStats
    {
        /// <summary>Total times this app was brought to focus (all time).</summary>
        public int OpenCount { get; set; }
        public int DailyOpenCount { get; set; }
        /// <summary>Total seconds this app had focus (all time).</summary>
        public long TotalFocusSeconds { get; set; }
        public long DailyFocusSeconds { get; set; }
        /// <summary>Seconds of the current uninterrupted focus session.</summary>
        public long CurrentSessionSeconds { get; set; }
        public DateTime LastOpened { get; set; }
        public DateTime LastSeen { get; set; }
    }
}
