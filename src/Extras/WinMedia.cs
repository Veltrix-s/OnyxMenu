using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NAudio.CoreAudioApi;
using Windows.Media;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace Nocturne;

internal sealed class WinMedia
{
    internal sealed class Info
    {
        internal string App = string.Empty;
        internal string Title = string.Empty;
        internal string Artist = string.Empty;
        internal bool Playing;
        internal bool CanToggle;
        internal bool CanPrev;
        internal bool CanNext;
        internal bool CanSeek;
        internal bool CanShuffle;
        internal bool CanRepeat;
        internal bool Shuffle;
        internal bool Repeat;
        internal float Position;
        internal float Length;
        internal float Rate;
        internal long Tick;
        internal int Count;
        internal int Track;
        internal int Cover;
        internal byte[] Image;
        internal MediaService Service;
        internal bool Web;
        internal string Exe;
        internal bool Mixer;
        internal float Level;
        internal bool Muted;
        internal bool Ad;
        internal bool Stub;

        internal float Clock()
        {
            float t = Position;
            if (Playing)
                t += (float)((Stopwatch.GetTimestamp() - Tick) * (double)Rate / Stopwatch.Frequency);
            return Length > 0f ? Math.Min(t, Length) : t;
        }
    }

    private static readonly (string Key, string Name, string Proc, bool Web)[] Known =
    {
        ("opera", "Opera", "opera", true), ("spotify", "Spotify", "spotify", false), ("chrome", "Chrome", "chrome", true), ("msedge", "Edge", "msedge", true),
        ("firefox", "Firefox", "firefox", true), ("brave", "Brave", "brave", true), ("vivaldi", "Vivaldi", "vivaldi", true),
        ("yandex.music", "Yandex Music", null, false), ("yandexmusic", "Yandex Music", null, false), ("yandex", "Yandex", "browser", true),
        ("zunemusic", "Media Player", null, false), ("mediaplayer", "Media Player", null, false), ("applemusic", "Apple Music", null, false),
        ("itunes", "iTunes", "itunes", false), ("vlc", "VLC", "vlc", false), ("telegram", "Telegram", "telegram", false),
        ("tidal", "Tidal", "tidal", false), ("deezer", "Deezer", "deezer", false), ("foobar", "foobar2000", "foobar2000", false),
        ("aimp", "AIMP", "aimp", false), ("musicbee", "MusicBee", "musicbee", false), ("winamp", "Winamp", "winamp", false),
    };

    private const long LogoKey = -2819190696966706824L;

    private readonly ConcurrentQueue<Action<GlobalSystemMediaTransportControlsSession>> _jobs = new ConcurrentQueue<Action<GlobalSystemMediaTransportControlsSession>>();
    private readonly AutoResetEvent _wake = new AutoResetEvent(false);
    private Thread _thread;
    private volatile bool _stop;
    private volatile bool _fast = true;
    private volatile bool _props;
    private volatile bool _cycle;
    private volatile bool _flip;
    private volatile string _pick;
    private float _volTo = -1f;

    internal volatile Info Now;
    internal volatile bool Failed;

    internal bool Fast
    {
        set => _fast = value;
    }

    internal void Start()
    {
        if (_thread != null && _thread.IsAlive)
            return;
        _stop = false;
        Failed = false;
        _thread = new Thread(Guard) { IsBackground = true, Name = "Nocturne media" };
        _thread.Start();
    }

    private void Guard()
    {
        try
        {
            Run();
        }
        catch (Exception e)
        {
            NocturnePlugin.Logger?.LogWarning($"[Media] {e.GetType().Name}: {e.Message}");
            Now = null;
            Failed = true;
        }
    }

    internal void Stop()
    {
        _stop = true;
        _wake.Set();
    }

    internal void Cycle()
    {
        _cycle = true;
        _wake.Set();
    }

    internal void Volume(float level)
    {
        Interlocked.Exchange(ref _volTo, Math.Clamp(level, 0f, 1f));
        _wake.Set();
    }

    internal void Mute()
    {
        _flip = true;
        _wake.Set();
    }

    internal void Toggle() => Queue(s =>
    {
        var info = s.GetPlaybackInfo();
        var c = info.Controls;
        bool playing = info.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
        if (playing && c.IsPauseEnabled)
            _ = s.TryPauseAsync();
        else if (!playing && c.IsPlayEnabled)
            _ = s.TryPlayAsync();
        else if (c.IsPlayPauseToggleEnabled)
            _ = s.TryTogglePlayPauseAsync();
    });

    internal void Next() => Queue(s => _ = s.TrySkipNextAsync());

    internal void Prev() => Queue(s => _ = s.TrySkipPreviousAsync());

    internal void Seek(float seconds) => Queue(s =>
    {
        double at = s.GetTimelineProperties().StartTime.TotalSeconds + seconds;
        _ = s.TryChangePlaybackPositionAsync((long)(at * 10000000.0));
    });

    internal void Shuffle() => Queue(s => _ = s.TryChangeShuffleActiveAsync(s.GetPlaybackInfo().IsShuffleActive != true));

    internal void Repeat() => Queue(s =>
    {
        var mode = s.GetPlaybackInfo().AutoRepeatMode;
        var next = !mode.HasValue || mode.Value == MediaPlaybackAutoRepeatMode.None ? MediaPlaybackAutoRepeatMode.List
            : mode.Value == MediaPlaybackAutoRepeatMode.List ? MediaPlaybackAutoRepeatMode.Track
            : MediaPlaybackAutoRepeatMode.None;
        _ = s.TryChangeAutoRepeatModeAsync(next);
    });

    private void Queue(Action<GlobalSystemMediaTransportControlsSession> job)
    {
        _jobs.Enqueue(job);
        _wake.Set();
    }

    private void OnProps(GlobalSystemMediaTransportControlsSession s, MediaPropertiesChangedEventArgs a)
    {
        _props = true;
        _wake.Set();
    }

    private void OnPlayback(GlobalSystemMediaTransportControlsSession s, PlaybackInfoChangedEventArgs a) => _wake.Set();

    private void OnSessions(GlobalSystemMediaTransportControlsSessionManager m, SessionsChangedEventArgs a) => _wake.Set();

    private void OnCurrent(GlobalSystemMediaTransportControlsSessionManager m, CurrentSessionChangedEventArgs a) => _wake.Set();

    private void Run()
    {
        GlobalSystemMediaTransportControlsSessionManager mgr = null;
        GlobalSystemMediaTransportControlsSession cur = null;
        string curId = null;
        string app = string.Empty;
        string audio = null;
        bool web = false;
        MediaService service = null;
        string exe = null;
        var scan = new Scan();
        long scanAt = 0;
        bool scanOff = false;
        var levels = new AppVolume();
        long mixAt = 0;
        bool mixOff = false;
        string key = null;
        string title = string.Empty;
        string artist = string.Empty;
        byte[] image = null;
        long imageKey = 0;
        int track = 0;
        int cover = 0;
        int fails = 0;
        long nextProps = 0;
        long changedAt = 0;
        var adKeys = new HashSet<long>();
        Task<Props> metaTask = null;
        Task<Pic> picTask = null;
        IRandomAccessStreamReference waiting = null;
        bool coverDue = false;
        while (!_stop)
        {
            try
            {
                if (mgr == null)
                {
                    mgr = GlobalSystemMediaTransportControlsSessionManager.RequestAsync().GetAwaiter().GetResult();
                    mgr.SessionsChanged += OnSessions;
                    mgr.CurrentSessionChanged += OnCurrent;
                }

                var list = mgr.GetSessions();
                if (_cycle)
                {
                    _cycle = false;
                    for (int i = 0; i < list.Count; i++)
                    {
                        if (list[i].SourceAppUserModelId == curId)
                        {
                            _pick = list[(i + 1) % list.Count].SourceAppUserModelId;
                            break;
                        }
                    }
                }

                GlobalSystemMediaTransportControlsSession next = null;
                string want = _pick;
                if (want != null)
                {
                    for (int i = 0; i < list.Count && next == null; i++)
                    {
                        if (list[i].SourceAppUserModelId == want)
                            next = list[i];
                    }
                    if (next == null)
                        _pick = null;
                }
                next ??= mgr.GetCurrentSession();
                if (next == null && list.Count > 0)
                    next = list[0];

                string id = next?.SourceAppUserModelId;
                if (id != curId)
                {
                    if (cur != null)
                    {
                        cur.MediaPropertiesChanged -= OnProps;
                        cur.PlaybackInfoChanged -= OnPlayback;
                    }
                    cur = next;
                    curId = id;
                    app = Identify(id, out audio, out web);
                    service = web ? null : MediaServices.FromApp(id);
                    exe = null;
                    scanAt = 0;
                    mixAt = 0;
                    levels.Reset();
                    key = null;
                    waiting = null;
                    coverDue = false;
                    _props = true;
                    if (cur != null)
                    {
                        cur.MediaPropertiesChanged += OnProps;
                        cur.PlaybackInfoChanged += OnPlayback;
                        NocturnePlugin.Logger?.LogInfo($"[Media] session: {id}");
                    }
                }

                while (_jobs.TryDequeue(out var job))
                {
                    if (cur != null)
                        Perform(job, cur);
                }

                long now = Stopwatch.GetTimestamp();
                if (metaTask != null && metaTask.IsCompleted)
                {
                    Props got = metaTask.IsFaulted ? null : metaTask.Result;
                    if (metaTask.IsFaulted)
                        _ = metaTask.Exception;
                    metaTask = null;
                    if (got != null && got.Id == curId)
                    {
                        string k = got.Artist + "\n" + got.Title + "\n" + got.Album;
                        if (k != key)
                        {
                            key = k;
                            title = got.Title;
                            artist = got.Artist;
                            track++;
                            coverDue = true;
                            scanAt = 0;
                            changedAt = now;
                        }
                        if (got.Thumb != null)
                            waiting = got.Thumb;
                        else if (coverDue || imageKey != 0)
                        {
                            image = null;
                            imageKey = 0;
                            cover++;
                            coverDue = false;
                        }
                    }
                }

                if (picTask != null && picTask.IsCompleted)
                {
                    Pic pic = picTask.IsFaulted ? null : picTask.Result;
                    if (picTask.IsFaulted)
                        _ = picTask.Exception;
                    picTask = null;
                    if (pic != null && pic.Id == curId)
                    {
                        long h = Hash(pic.Bytes);
                        if (coverDue || h != imageKey)
                        {
                            image = pic.Bytes;
                            imageKey = h;
                            cover++;
                            coverDue = false;
                        }
                    }
                }

                if (waiting != null && picTask == null)
                {
                    IRandomAccessStreamReference reference = waiting;
                    string forId = curId;
                    waiting = null;
                    picTask = Task.Run(() =>
                    {
                        try
                        {
                            return new Pic { Id = forId, Bytes = Picture(reference) };
                        }
                        finally
                        {
                            _wake.Set();
                        }
                    });
                }

                if (cur != null && metaTask == null && (_props || now >= nextProps))
                {
                    _props = false;
                    GlobalSystemMediaTransportControlsSession owner = cur;
                    string ownerId = curId;
                    metaTask = Task.Run(() => ReadProps(owner, ownerId));
                    long age = now - changedAt;
                    nextProps = now + Stopwatch.Frequency * (age < Stopwatch.Frequency * 3 ? 1 : age < Stopwatch.Frequency * 20 ? 4 : 12);
                }

                if (cur != null && web && audio != null && !scanOff && Stopwatch.GetTimestamp() - scanAt >= Stopwatch.Frequency * 2)
                {
                    scanAt = Stopwatch.GetTimestamp();
                    try
                    {
                        MediaService hit = scan.Find(audio, title);
                        if (hit != null)
                        {
                            service = hit;
                            exe = scan.Exe ?? exe;
                        }
                    }
                    catch (Exception e)
                    {
                        scanOff = true;
                        NocturnePlugin.Logger?.LogWarning($"[Media] windows: {e.GetType().Name}: {e.Message}");
                    }
                }

                float to = Interlocked.Exchange(ref _volTo, -1f);
                bool flip = _flip;
                _flip = false;
                if (cur != null && audio != null && !mixOff && (to >= 0f || flip || Stopwatch.GetTimestamp() - mixAt >= (_fast ? Stopwatch.Frequency / 2 : Stopwatch.Frequency * 2)))
                {
                    mixAt = Stopwatch.GetTimestamp();
                    try
                    {
                        levels.Sync(audio, to, flip);
                    }
                    catch (Exception e)
                    {
                        mixOff = true;
                        levels.Reset();
                        NocturnePlugin.Logger?.LogWarning($"[Media] mixer: {e.GetType().Name}: {e.Message}");
                    }
                }

                if (cur == null)
                    Now = new Info { Count = list.Count };
                else
                {
                    var info = cur.GetPlaybackInfo();
                    var tl = cur.GetTimelineProperties();
                    var c = info.Controls;
                    bool playing = info.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
                    double rate = playing ? (info.PlaybackRate ?? 1.0) : 0.0;
                    double start = tl.StartTime.TotalSeconds;
                    double length = tl.EndTime.TotalSeconds - start;
                    double pos = tl.Position.TotalSeconds - start;
                    if (playing && tl.LastUpdatedTime.Year > 2000)
                        pos += Math.Max(0.0, (DateTimeOffset.UtcNow - tl.LastUpdatedTime).TotalSeconds) * rate;
                    if (length > 0.0)
                        pos = Math.Min(pos, length);
                    var mode = info.AutoRepeatMode;
                    bool locked = !c.IsNextEnabled && !c.IsPreviousEnabled;
                    bool ad = service == MediaServices.Spotify && (locked && length > 0.0 && length <= 75.0 || IsAdTitle(title) || locked && title.Length > 0 && title == artist);
                    if (ad && imageKey != 0)
                        adKeys.Add(imageKey);
                    Now = new Info
                    {
                        App = app,
                        Title = title,
                        Artist = artist,
                        Playing = playing,
                        CanToggle = c.IsPlayEnabled || c.IsPauseEnabled || c.IsPlayPauseToggleEnabled,
                        CanPrev = c.IsPreviousEnabled,
                        CanNext = c.IsNextEnabled,
                        CanSeek = c.IsPlaybackPositionEnabled && length > 0.0,
                        CanShuffle = c.IsShuffleEnabled,
                        CanRepeat = c.IsRepeatEnabled,
                        Shuffle = info.IsShuffleActive == true,
                        Repeat = mode.HasValue && mode.Value != MediaPlaybackAutoRepeatMode.None,
                        Position = (float)Math.Max(pos, 0.0),
                        Length = (float)Math.Max(length, 0.0),
                        Rate = (float)rate,
                        Tick = Stopwatch.GetTimestamp(),
                        Count = list.Count,
                        Track = track,
                        Cover = cover,
                        Image = image,
                        Service = service,
                        Web = web,
                        Exe = exe,
                        Mixer = levels.Found,
                        Level = levels.Level,
                        Muted = levels.Muted,
                        Ad = ad,
                        Stub = image != null && (imageKey == LogoKey || adKeys.Contains(imageKey)),
                    };
                }
                fails = 0;
            }
            catch (Exception e)
            {
                if (fails == 0)
                    NocturnePlugin.Logger?.LogWarning($"[Media] {e.GetType().Name}: {e.Message}");
                mgr = null;
                cur = null;
                curId = null;
                Now = null;
                if (++fails >= 4)
                {
                    Failed = true;
                    break;
                }
                _wake.WaitOne(2500);
                continue;
            }
            _wake.WaitOne(_fast ? 250 : 800);
        }
        Detach(mgr, cur);
        Now = null;
    }

    private sealed class Props
    {
        internal string Id;
        internal string Title;
        internal string Artist;
        internal string Album;
        internal IRandomAccessStreamReference Thumb;
    }

    private sealed class Pic
    {
        internal string Id;
        internal byte[] Bytes;
    }

    private Props ReadProps(GlobalSystemMediaTransportControlsSession s, string id)
    {
        try
        {
            var mp = s.TryGetMediaPropertiesAsync().GetAwaiter().GetResult();
            return new Props
            {
                Id = id,
                Title = mp.Title ?? string.Empty,
                Artist = mp.Artist ?? string.Empty,
                Album = mp.AlbumTitle ?? string.Empty,
                Thumb = mp.Thumbnail,
            };
        }
        finally
        {
            _wake.Set();
        }
    }

    private void Detach(GlobalSystemMediaTransportControlsSessionManager mgr, GlobalSystemMediaTransportControlsSession cur)
    {
        try
        {
            if (cur != null)
            {
                cur.MediaPropertiesChanged -= OnProps;
                cur.PlaybackInfoChanged -= OnPlayback;
            }
            if (mgr != null)
            {
                mgr.SessionsChanged -= OnSessions;
                mgr.CurrentSessionChanged -= OnCurrent;
            }
        }
        catch (Exception e)
        {
            NocturnePlugin.Logger?.LogWarning($"[Media] detach: {e.GetType().Name}: {e.Message}");
        }
    }

    private static void Perform(Action<GlobalSystemMediaTransportControlsSession> job, GlobalSystemMediaTransportControlsSession s)
    {
        try
        {
            job(s);
        }
        catch (Exception e)
        {
            NocturnePlugin.Logger?.LogWarning($"[Media] command: {e.GetType().Name}: {e.Message}");
        }
    }

    private static byte[] Picture(IRandomAccessStreamReference reference)
    {
        if (reference == null)
            return null;
        try
        {
            using var stream = reference.OpenReadAsync().GetAwaiter().GetResult();
            ulong size = stream.Size;
            if (size == 0 || size > 4194304)
                return null;
            using var reader = new DataReader(stream);
            reader.LoadAsync((uint)size).GetAwaiter().GetResult();
            var bytes = new byte[size];
            reader.ReadBytes(bytes);
            return bytes;
        }
        catch (Exception e)
        {
            NocturnePlugin.Logger?.LogWarning($"[Media] cover: {e.GetType().Name}: {e.Message}");
            return null;
        }
    }

    private static bool IsAdTitle(string title) =>
        string.Equals(title, "Advertisement", StringComparison.OrdinalIgnoreCase) || string.Equals(title, "Реклама", StringComparison.OrdinalIgnoreCase);

    private static long Hash(byte[] bytes)
    {
        if (bytes == null)
            return 0;
        long h = bytes.Length;
        int step = Math.Max(1, bytes.Length / 96);
        for (int i = 0; i < bytes.Length; i += step)
            h = h * 31 + bytes[i];
        return h;
    }

    private static string Identify(string id, out string audio, out bool web)
    {
        audio = null;
        web = false;
        if (string.IsNullOrEmpty(id))
            return "Windows";
        string low = id.ToLowerInvariant();
        for (int i = 0; i < Known.Length; i++)
        {
            if (low.Contains(Known[i].Key))
            {
                audio = Known[i].Proc;
                web = Known[i].Web;
                return Known[i].Name;
            }
        }
        int bang = id.IndexOf('!');
        string s = bang >= 0 ? id.Substring(bang + 1) : id;
        if (s.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            s = s.Substring(0, s.Length - 4);
            audio = s;
        }
        int dot = s.LastIndexOf('.');
        if (dot >= 0 && dot < s.Length - 1)
            s = s.Substring(dot + 1);
        return s.Length == 0 ? "Windows" : char.ToUpperInvariant(s[0]) + s.Substring(1);
    }

    private sealed class AppVolume
    {
        private readonly Dictionary<uint, string> _names = new Dictionary<uint, string>();

        internal bool Found;
        internal float Level;
        internal bool Muted;

        internal void Reset()
        {
            Found = false;
            Level = 0f;
            Muted = false;
            _names.Clear();
        }

        internal void Sync(string proc, float to, bool flip)
        {
            using var en = new MMDeviceEnumerator();
            using var dev = en.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            var list = dev.AudioSessionManager.Sessions;
            int n = 0;
            float level = 0f;
            bool muted = true;
            for (int i = 0; i < list.Count; i++)
            {
                using var s = list[i];
                if (s.IsSystemSoundsSession || !Owned(s.GetProcessID, proc))
                    continue;
                var v = s.SimpleAudioVolume;
                if (to >= 0f)
                {
                    v.Volume = to;
                    v.Mute = false;
                }
                else if (flip)
                    v.Mute = !v.Mute;
                level = Math.Max(level, v.Volume);
                muted &= v.Mute;
                n++;
            }
            Found = n > 0;
            Level = level;
            Muted = n > 0 && muted;
        }

        private bool Owned(uint pid, string proc)
        {
            if (!_names.TryGetValue(pid, out string name))
            {
                if (_names.Count > 64)
                    _names.Clear();
                name = NameOf(pid);
                _names[pid] = name;
            }
            return string.Equals(name, proc, StringComparison.OrdinalIgnoreCase);
        }

        private static string NameOf(uint pid)
        {
            try
            {
                using var p = Process.GetProcessById((int)pid);
                return p.ProcessName;
            }
            catch
            {
                return string.Empty;
            }
        }
    }

    private sealed class Scan
    {
        private delegate bool Visit(IntPtr hwnd, IntPtr arg);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(Visit proc, IntPtr arg);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hwnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int max);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);

        private readonly StringBuilder _text = new StringBuilder(512);
        private readonly Visit _visit;
        private string _proc;
        private string _track;
        private MediaService _hit;

        internal string Exe;

        internal Scan()
        {
            _visit = OnWindow;
        }

        internal MediaService Find(string proc, string track)
        {
            _proc = proc;
            _track = track;
            _hit = null;
            Exe = null;
            EnumWindows(_visit, IntPtr.Zero);
            return _hit;
        }

        private bool OnWindow(IntPtr hwnd, IntPtr arg)
        {
            if (!IsWindowVisible(hwnd))
                return true;
            _text.Clear();
            if (GetWindowText(hwnd, _text, _text.Capacity) == 0)
                return true;
            string title = _text.ToString();
            bool ours = _track.Length > 0 && title.IndexOf(_track, StringComparison.OrdinalIgnoreCase) >= 0;
            MediaService svc = MediaServices.FromTitle(title, ours);
            if (svc == null || (_hit != null && !ours))
                return true;
            GetWindowThreadProcessId(hwnd, out uint pid);
            string path = Owner(pid, out string name);
            if (!string.Equals(name, _proc, StringComparison.OrdinalIgnoreCase))
                return true;
            _hit = svc;
            Exe = path;
            return !ours;
        }

        private static string Owner(uint pid, out string name)
        {
            name = null;
            try
            {
                using var p = Process.GetProcessById((int)pid);
                name = p.ProcessName;
                return p.MainModule?.FileName;
            }
            catch
            {
                return null;
            }
        }
    }
}
