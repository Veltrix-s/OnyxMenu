using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using BepInEx.Unity.IL2CPP.Utils;
using Il2CppInterop.Runtime.Attributes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using NAudio.Wave;
using NVorbis;
using UnityEngine;

namespace Nocturne;

public sealed class NocturneMusicPlayer : MonoBehaviour
{
    private static NocturneMusicPlayer _instance;
    internal static NocturneMusicPlayer Instance => _instance;

    private AudioSource _source;
    private GUI.WindowFunction _winFn, _listFn, _eqFn;
    private readonly List<string> _tracks = new List<string>();
    private readonly List<string> _trackNames = new List<string>();
    private readonly List<float> _trackDurations = new List<float>();

    private PlayerTrack _playing;
    private PlayerTrack _pending;
    private readonly List<PlayerTrack> _limbo = new List<PlayerTrack>();
    private int _rawReadPos;

    private PlayerChain _chain;
    private PlayerLimiter _lim;
    private int _limCh;
    private int _limRate;
    private float[] _mixA;
    private readonly PlayerMixer _mixer = new PlayerMixer();
    private volatile bool _measuring;
    private volatile bool _measureAgain;
    private float _remeasureAt;
    private float _endCheckAt;
    private volatile string _loudInfo = string.Empty;

    private int _currentIndex = -1;
    private int _loadGen;
    private float _playAt = -10f;
    private int _coverIdx = -1;
    private float _coverAt;
    private float _artAt = -10f;
    private WinMedia _media;
    private WinMedia.Info _winInfo;
    private bool _win;
    private int _winTrack = -1;
    private int _winCover = -1;
    private int _winGen;
    private bool _winAd;
    private float _winSeekTo;
    private float _winSeekAt = -10f;
    private float _winSeekSent = -10f;
    private bool _winSeekDirty;
    private float _winVolTo;
    private float _winVolAt = -10f;
    private float _winVolSent = -10f;
    private bool _winVolDirty;
    private float _cVol;
    private PlayerCover _winOwned;
    private readonly PlayerSearch _find = new PlayerSearch();
    private readonly List<string> _recent = new List<string>();
    private bool _recentLoaded;
    private bool _findMove;
    private bool _findRu;
    private MediaService _findFor;
    private bool _isLoading;
    private float _volume = 1f;
    private bool _loop;
    private bool _shuffle;
    private bool _autoPlayed;
    internal string StatusText = string.Empty;

    internal bool IsPlaying => _source != null && _source.isPlaying;
    internal bool IsPaused => _source != null && !_source.isPlaying && _source.clip != null && _source.time > 0f;
    internal int TrackCount => _tracks.Count;
    internal bool Loop
    {
        get => _loop; set
        {
            _loop = value;
            if (_source != null)
                _source.loop = value;
        }
    }
    internal bool Shuffle
    {
        get => _shuffle; set => _shuffle = value;
    }
    internal float CurrentTime => _source != null ? _source.time : 0f;
    internal float TotalTime => _source != null && _source.clip != null ? _source.clip.length : 0f;

    internal float Volume
    {
        get => _volume;
        set
        {
            _volume = Ui.Clamp01(value);
            if (_source != null)
                _source.volume = _volume;
            _chain?.SetVolume(_volume);
            _mixer.SetVolume(_volume);
        }
    }

    private bool _visible;
    private Rect _winRect = Ui.R(80f, 80f, PlayerPoster.W, PlayerPoster.Tall);
    private Vector2 _trackScroll;
    private Vector2 _eqScroll;
    private float _openedAt = -1f;
    private float _closingAt = -1f;
    private float _winAlpha = 1f;
    private float _listAlpha = 1f;
    private float _eqAlpha = 1f;
    private float _beatLevel;
    private float _bassEnv;
    private float _bassAvg;
    private float _glow;
    private float _phase;
    private float _playMix;
    private float _foldT;
    private bool _fold;
    private float _lastVol = 1f;
    private readonly PlayerMeter _meter = new PlayerMeter();
    private readonly PlayerCovers _covers = new PlayerCovers();
    private PlayerCover _cover;
    private PlayerCover _prevCover;

    private int _stateFrame = -1;
    private float _cTime;
    private float _cTotal;
    private bool _cPlaying;
    private bool _cPaused;

    private float _artSlideStart = -1f;
    private int _artSlideDir;
    private Color _artColor = new Color(0.52f, 0.08f, 0.15f, 1f);
    private Color _artColorCurrent = new Color(0.52f, 0.08f, 0.15f, 1f);
    private int _artIndex = -2;

    private readonly PlayerPoster _poster = new PlayerPoster();
    private readonly PlayerList _list = new PlayerList();
    private readonly PlayerEq _eq = new PlayerEq();
    private Color _cardBg = new Color(0.075f, 0.050f, 0.070f, 0.97f);
    private Color _accent = new Color(0.94f, 0.20f, 0.24f, 1f);
    private bool _stylesDirty = true;
    private string _appliedAccent;
    private string _listTitle = string.Empty;
    private int _listCount = -1;

    private bool _showPlaylist;
    private Rect _listRect = Ui.R(80f, 260f, PlayerList.W, 200f);
    private float _listOpenedAt = -1f;
    private float _listClosingAt = -1f;

    private bool _showEqPanel;
    private Rect _eqPanelRect = Ui.R(390f, 80f, PlayerEq.W, PlayerEq.Tall);
    private float _eqOpenedAt = -1f;
    private float _eqClosingAt = -1f;
    private int _activePresetIdx;
    private float[] _eqGains = new float[10];

    private float _toastTimer;
    private string _toastText = string.Empty;

    private static readonly string[] kPresetNames =
    {
        "(Default)", "Acoustic", "Bass Boost", "Clarity", "Classical", "Club", "Dance", "Electronic",
        "Full Bass", "Full Bass+Treble", "Full Treble", "Harman", "Hip-Hop", "Jazz", "Laptop", "Large Hall",
        "Live", "Loudness", "Metal", "Nocturne", "Party", "Piano", "Pop", "R&B",
        "Reggae", "Rock", "Ska", "Soft", "Soft Rock", "Techno", "Vocal Boost", "Warm", "Custom",
    };
    private static readonly string[] kOldPresetNames =
    {
        "(Default)", "Acoustic", "Bass Boost", "Classical", "Club", "Dance", "Electronic", "Full Bass",
        "Full Bass+Treble", "Full Treble", "Hip-Hop", "Jazz", "Laptop", "Large Hall", "Live", "Loudness",
        "Metal", "Party", "Piano", "Pop", "R&B", "Reggae", "Rock", "Ska", "Soft", "Soft Rock", "Techno", "Vocal Boost",
    };
    private static readonly float[][] kPresetGains =
    {
        new float[] {  0,  0,  0,  0,  0,  0,  0,  0,  0,  0 },
        new float[] {  2,  3,  2,  1,  0,  0,  1,  2,  3,  2 },
        new float[] {  6,  6,  4,  2,  0,  0,  0,  0,  0,  0 },
        new float[] {  0, -1, -1, -1,  0,  1,  2,  3,  3,  2 },
        new float[] {  2,  1,  0,  0,  0,  0,  0,  0,  1,  2 },
        new float[] {  5,  5,  3,  0,  0,  1,  2,  2,  3,  2 },
        new float[] {  6,  6,  3,  0, -2, -1,  1,  3,  4,  3 },
        new float[] {  6,  5,  2, -1, -2,  0,  2,  4,  5,  4 },
        new float[] {  7,  7,  5,  2,  0, -1, -2, -3, -3, -3 },
        new float[] {  6,  5,  1, -2, -2,  0,  2,  4,  6,  6 },
        new float[] { -3, -2, -1,  0,  1,  3,  5,  6,  7,  7 },
        new float[] {  5,  5,  3,  0,  0,  0,  1,  2,  1, -1 },
        new float[] {  7,  6,  3,  1, -1,  0,  1,  1,  2,  2 },
        new float[] {  3,  2,  1,  2,  0, -1,  0,  1,  2,  3 },
        new float[] { -3, -2,  3,  4,  2,  0, -1, -1,  1,  2 },
        new float[] {  5,  5,  3,  3,  0, -2, -2, -2,  0,  1 },
        new float[] { -3,  0,  2,  3,  3,  3,  2,  2,  2,  1 },
        new float[] {  6,  5,  2,  0, -1,  0,  0,  2,  4,  5 },
        new float[] {  6,  4,  1, -3, -2,  1,  3,  3,  4,  5 },
        new float[] {  2,  3,  2,  0, -1,  0,  1.5f,  1,  1.5f,  1.5f },
        new float[] {  6,  5,  1,  0,  0,  0,  1,  2,  4,  5 },
        new float[] {  1,  2,  2,  2,  1,  0,  1,  2,  3,  3 },
        new float[] {  3,  3,  2,  0,  0,  1,  2,  2,  3,  3 },
        new float[] {  6,  5,  3,  1, -1,  0,  1,  2,  3,  3 },
        new float[] {  5,  4,  2,  0,  0,  1,  2,  1,  0,  0 },
        new float[] {  5,  4,  3,  1, -1, -1,  1,  3,  4,  4 },
        new float[] { -2, -1,  0,  1,  2,  3,  4,  4,  4,  3 },
        new float[] {  2,  2,  1,  0,  0, -1, -2, -3, -3, -3 },
        new float[] {  3,  3,  2,  1,  0, -1,  0,  2,  3,  3 },
        new float[] {  7,  6,  3,  0, -1,  1,  2,  3,  3,  2 },
        new float[] { -2, -2, -1,  1,  3,  4,  4,  2,  1,  0 },
        new float[] {  3,  3,  2,  1,  0, -1, -1, -2, -2, -2 },
        new float[] {  0,  0,  0,  0,  0,  0,  0,  0,  0,  0 },
    };
    private const int kCustom = 32;
    private const int kDefaultPreset = 19;
    private readonly float[] _custom = new float[PlayerChain.Bands];
    internal static readonly (string Ru, string En, float Hue)[] AccentThemes =
    {
        ("Красный",   "Red",     0.986f),
        ("Алый",      "Crimson", 0.972f),
        ("Оранжевый", "Orange",  0.060f),
        ("Янтарный",  "Amber",   0.095f),
        ("Жёлтый",    "Yellow",  0.140f),
        ("Лайм",      "Lime",    0.230f),
        ("Зелёный",   "Green",   0.330f),
        ("Изумруд",   "Emerald", 0.420f),
        ("Бирюза",    "Teal",    0.480f),
        ("Циан",      "Cyan",    0.520f),
        ("Небо",      "Sky",     0.560f),
        ("Синий",     "Blue",    0.620f),
        ("Индиго",    "Indigo",  0.690f),
        ("Фиолет",    "Violet",  0.760f),
        ("Пурпур",    "Purple",  0.810f),
        ("Маджента",  "Magenta", 0.870f),
        ("Розовый",   "Pink",    0.920f),
        ("Роза",      "Rose",    0.955f),
    };

    private static float AccentHue()
    {
        string name = NocturneConfig.MusicAccent.Value;
        foreach (var t in AccentThemes)
            if (string.Equals(t.En, name, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(t.Ru, name, StringComparison.Ordinal))
                return t.Hue;
        return 0.986f;
    }

    private static Color Rehue(Color c, float hue)
    {
        Color.RGBToHSV(c, out _, out float s, out float v);
        Color o = Color.HSVToRGB(hue, s, v);
        o.a = c.a;
        return o;
    }

    private void Awake()
    {
        _instance = this;
        _source = gameObject.AddComponent<AudioSource>();
        _source.volume = _volume;
        _source.playOnAwake = false;
        _source.loop = _loop;
        _source.priority = 0;

        _source.spatialBlend = 0f;
        _source.dopplerLevel = 0f;
        _source.bypassEffects = true;
        _source.bypassListenerEffects = true;
        _source.bypassReverbZones = true;
        _source.panStereo = 0f;
        _source.pitch = 1f;
        ScanTracks();
        InitEq();
    }

    private void OnDestroy()
    {
        _media?.Stop();
        if (_instance == this) _instance = null;
    }

    private void BeginPlayerOpen()
    {
        _visible = true;
        _openedAt = Time.unscaledTime;
        _closingAt = -1f;
    }

    private void BeginPlayerClose()
    {
        _closingAt = Time.unscaledTime;
        _find.Focus = false;
    }

    private static bool IsChatOpen()
    {

        if (LobbyBehaviour.Instance == null && ShipStatus.Instance == null) return false;
        return HudManager.Instance != null
            && HudManager.Instance.Chat != null
            && HudManager.Instance.Chat.IsOpenOrOpening;
    }

    private static float PlayerEaseOutBack(float t, float overshoot = 1.1f)
    {
        t = Ui.Clamp01(t);
        float v = t - 1f;
        return 1f + (overshoot + 1f) * v * v * v + overshoot * v * v;
    }

    private static float PlayerSmoothSaturate(float t)
    {
        t = Ui.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    private void Update()
    {
        if (_closingAt >= 0f && Time.unscaledTime - _closingAt >= 0.42f)
        {
            _visible = false;
            _closingAt = -1f;
        }

        if (NocturneConfig.MusicWindows.Value != _win)
            SetSource(NocturneConfig.MusicWindows.Value);

        bool finding = _win && _find.Focus && _showPlaylist && _listClosingAt < 0f && _visible && _closingAt < 0f;
        if (finding)
            Patches.NocturneTextFocus.Mark();
        if (finding != _findMove)
            HoldMove(finding);

        bool chatOpen = IsChatOpen();
        bool noTextFieldFocused = GUIUtility.keyboardControl == 0 && !NocturneMenu.Typing && !NocturneChatWindow.Typing && !Patches.NocturneTextFocus.Any;

        if (!NocturneMenu.Rebinding && NocturneKeys.Down(NocturneConfig.MusicToggleKey) && noTextFieldFocused && !chatOpen)
        {
            if (!_visible && _closingAt < 0f)
                BeginPlayerOpen();
            else if (_visible && _closingAt < 0f)
                BeginPlayerClose();
        }

        if (noTextFieldFocused && !chatOpen && !NocturneMenu.Rebinding)
        {
            float dt0 = Time.deltaTime;
            if (NocturneKeys.Down(NocturneConfig.MusicPrevKey))
                Prev();
            if (NocturneKeys.Down(NocturneConfig.MusicNextKey))
                Next();
            if (NocturneKeys.Down(NocturneConfig.MusicPlayPauseKey))
                PlayPause();
            if (NocturneKeys.Down(NocturneConfig.MusicStopKey))
            {
                if (!_win)
                    Stop();
                else if (_winInfo != null && _winInfo.Playing)
                    _media.Toggle();
            }
            float volDelta = 0f;
            if (NocturneKeys.Held(NocturneConfig.MusicVolumeUpKey))
                volDelta += dt0 * 0.6f;
            if (NocturneKeys.Held(NocturneConfig.MusicVolumeDownKey))
                volDelta -= dt0 * 0.6f;
            if (volDelta != 0f)
            {
                if (_win)
                    NudgeWin(volDelta);
                else
                    Volume = Ui.Clamp01(_volume + volDelta);
            }
        }

        float dt = Time.deltaTime;
        if (_measureAgain && !_measuring)
        {
            _measureAgain = false;
            Remeasure();
        }
        if (_remeasureAt > 0f && Time.unscaledTime >= _remeasureAt)
        {
            _remeasureAt = 0f;
            Remeasure();
        }
        if (_limbo.Count > 0)
            ReleaseLimbo();
        if (_win)
            UpdateWin();
        else
            UpdateArt();
        if (_toastTimer > 0f) _toastTimer -= dt;

        bool shown = _visible || _closingAt >= 0f;
        if (shown || _toastTimer > 0f)
        {
            bool playing = _win ? _winInfo != null && _winInfo.Playing : IsPlaying;
            if (shown)
            {
                PlayerTrack cur = _playing;
                _meter.Step(cur?.Samples, cur != null ? cur.Channels : 2, cur != null ? cur.Frequency : 44100, cur != null && playing ? (int)(_source.time * cur.Frequency) : 0, dt, playing);
            }
            float bass = _meter.Bass;
            _bassEnv = Ui.Lerp(_bassEnv, bass, dt * (bass > _bassEnv ? 40f : 7f));
            _bassAvg = Ui.Lerp(_bassAvg, bass, dt * 0.7f);
            float punch = Ui.Clamp01((_bassEnv - _bassAvg * 1.1f) / Ui.Max(0.03f, _bassAvg));
            _beatLevel = Ui.Lerp(_beatLevel, playing ? punch : 0f, dt * 16f);
            _glow = Ui.Lerp(_glow, _meter.Mean, dt * 5f);
            _playMix = Ui.MoveTowards(_playMix, playing ? 1f : 0f, dt * 1.2f);
            _phase += dt * (0.25f + 0.75f * _playMix);
            _foldT = Ui.MoveTowards(_foldT, _fold ? 1f : 0f, dt * 4.5f);
            _artColorCurrent = Ui.Mix(_artColorCurrent, _artColor, dt * 5f);
        }

        if (_source == null || _isLoading || _source.clip == null) return;
        if (_source.isPlaying)
        {
            if (!_loop && _autoPlayed && Time.unscaledTime >= _endCheckAt)
            {
                _endCheckAt = Time.unscaledTime + 0.1f;
                if (_source.clip.length - _source.time < 0.2f)
                {
                    _autoPlayed = false;
                    Next();
                }
            }
            return;
        }
        if (!_loop && _autoPlayed && _source.time == 0f)
        {
            _autoPlayed = false;
            Next();
        }
    }

    public void OnGUI()
    {
        try
        {
            DrawGui();
        }
        catch { }
    }

    internal void DrawGui()
    {
        if (_toastTimer > 0f && _toastText.Length > 0 && Event.current.type == EventType.Repaint)
        {
            EnsureStyles();
            GUI.skin = null;
            Toast();
        }

        if (!_visible && _closingAt < 0f) return;
        EnsureStyles();
        GUI.skin = null;
        float sw = Screen.width;
        float sh = Screen.height;
        bool paint = Event.current.type == EventType.Repaint;

        if (_win && _showPlaylist)
        {
            Event fe = Event.current;
            if (fe.type == EventType.MouseDown && !Ui.In(_listRect, fe.mousePosition))
                _find.Focus = false;
            else if (_find.Focus && fe.type == EventType.KeyDown && _find.Type(fe))
                Search(_find.Text);
        }
        Color keep = GUI.color;

        float alpha = 1f;
        float offsetY = 0f;
        bool closing = _closingAt >= 0f;
        bool opening = !closing && _openedAt >= 0f;
        if (closing)
        {
            float t = Ui.Clamp01((Time.unscaledTime - _closingAt) / 0.34f);
            alpha = 1f - PlayerSmoothSaturate(t);
            offsetY = t * t * t * t * 18f;
        }
        else if (opening)
        {
            float t = Ui.Clamp01((Time.unscaledTime - _openedAt) / 0.36f);
            if (t < 0.995f)
            {
                alpha = PlayerSmoothSaturate(t);
                offsetY = (1f - PlayerEaseOutBack(t)) * -24f;
            }
            else
            {
                _openedAt = -1f;
                opening = false;
            }
        }
        bool animating = closing || opening;
        _winAlpha = alpha;

        float cardH = PlayerPoster.HeightFor(PlayerSmoothSaturate(_foldT));
        if (_winFn == null)
            _winFn = (GUI.WindowFunction)DrawWindow;
        Rect nextRect = Window(772100, Ui.R(_winRect.m_XMin, _winRect.m_YMin + offsetY, PlayerPoster.W, cardH), _winFn, alpha, paint);

        if (!animating)
        {
            _winRect = nextRect;
            _winRect.m_Width = PlayerPoster.W;
            _winRect.m_Height = cardH;
            _winRect.m_XMin = Ui.Clamp(_winRect.m_XMin, 0f, sw - PlayerPoster.W);
            _winRect.m_YMin = Ui.Clamp(_winRect.m_YMin, 0f, sh - 60f);
        }

        if (_showPlaylist || _listClosingAt >= 0f)
        {
            float listAlpha = 1f, listOff = 0f;
            bool listClosing = _listClosingAt >= 0f;
            bool listOpening = !listClosing && _listOpenedAt >= 0f;
            if (listClosing)
            {
                float t = Ui.Clamp01((Time.unscaledTime - _listClosingAt) / 0.34f);
                listAlpha = 1f - PlayerSmoothSaturate(t);
                listOff = t * t * t * t * 18f;
                if (t >= 0.999f)
                {
                    _showPlaylist = false;
                    _listClosingAt = -1f;
                }
            }
            else if (listOpening)
            {
                float t = Ui.Clamp01((Time.unscaledTime - _listOpenedAt) / 0.36f);
                if (t < 0.995f)
                {
                    listAlpha = PlayerSmoothSaturate(t);
                    listOff = (1f - PlayerEaseOutBack(t)) * -24f;
                }
                else
                    _listOpenedAt = -1f;
            }
            bool listAnim = listClosing || listOpening;
            _listAlpha = listAlpha;

            float listH = _win ? PlayerSearch.HeightFor(_recent.Count) : PlayerList.HeightFor(TrackCount);
            if (_listFn == null)
                _listFn = (GUI.WindowFunction)DrawPlaylistWindow;
            Rect nextList = Window(772101, Ui.R(_listRect.m_XMin, _listRect.m_YMin + listOff, PlayerList.W, listH), _listFn, listAlpha, paint);
            if (!listAnim)
            {
                _listRect = nextList;
                _listRect.m_Width = PlayerList.W;
                _listRect.m_Height = listH;
                _listRect.m_XMin = Ui.Clamp(_listRect.m_XMin, 0f, sw - PlayerList.W);
                _listRect.m_YMin = Ui.Clamp(_listRect.m_YMin, 0f, sh - 60f);
            }
        }

        if (_showEqPanel || _eqClosingAt >= 0f)
        {
            float eqAlpha = 1f, eqOff = 0f;
            bool eqClosing = _eqClosingAt >= 0f;
            bool eqOpening = !eqClosing && _eqOpenedAt >= 0f;
            if (eqClosing)
            {
                float t = Ui.Clamp01((Time.unscaledTime - _eqClosingAt) / 0.34f);
                eqAlpha = 1f - PlayerSmoothSaturate(t);
                eqOff = t * t * t * t * 18f;
                if (t >= 0.999f)
                {
                    _showEqPanel = false;
                    _eqClosingAt = -1f;
                }
            }
            else if (eqOpening)
            {
                float t = Ui.Clamp01((Time.unscaledTime - _eqOpenedAt) / 0.36f);
                if (t < 0.995f)
                {
                    eqAlpha = PlayerSmoothSaturate(t);
                    eqOff = (1f - PlayerEaseOutBack(t)) * -24f;
                }
                else
                    _eqOpenedAt = -1f;
            }
            bool eqAnim = eqClosing || eqOpening;
            _eqAlpha = eqAlpha;

            if (_eqFn == null)
                _eqFn = (GUI.WindowFunction)DrawEqPanel;
            Rect nextEq = Window(772102, Ui.R(_eqPanelRect.m_XMin, _eqPanelRect.m_YMin + eqOff, PlayerEq.W, PlayerEq.Tall), _eqFn, eqAlpha, paint);
            if (!eqAnim)
            {
                _eqPanelRect = nextEq;
                _eqPanelRect.m_Width = PlayerEq.W;
                _eqPanelRect.m_Height = PlayerEq.Tall;
                _eqPanelRect.m_XMin = Ui.Clamp(_eqPanelRect.m_XMin, 0f, sw - PlayerEq.W);
                _eqPanelRect.m_YMin = Ui.Clamp(_eqPanelRect.m_YMin, 0f, sh - 60f);
            }
        }
        GUI.color = keep;
    }

    private Rect Window(int id, Rect r, GUI.WindowFunction fn, float alpha, bool paint)
    {
        Patches.MouseShield.Cover(r);
        if (paint)
            PlayerGfx.Cast(r, (0.50f + 0.20f * _beatLevel) * alpha);
        return GUI.Window(id, r, fn, string.Empty, PlayerGfx.None);
    }

    private void Card(float w, float h, float alpha)
    {
        GUI.color = Ui.C(_cardBg.r, _cardBg.g, _cardBg.b, _cardBg.a * alpha);
        GUI.Box(Ui.R(0f, 0f, w, h), PlayerGfx.Blank, PlayerGfx.Round14);
    }

    private void Rim(float w, float h, float alpha, float beat)
    {
        GUI.color = Ui.C(Ui.Lerp(1f, _artColorCurrent.r, beat), Ui.Lerp(1f, _artColorCurrent.g, beat), Ui.Lerp(1f, _artColorCurrent.b, beat), (0.10f + 0.40f * beat) * alpha);
        GUI.Box(Ui.R(0f, 0f, w, h), PlayerGfx.Blank, PlayerGfx.Ring);
    }

    private void Toast()
    {
        Color keep = GUI.color;
        float ta = Ui.Clamp01(_toastTimer * 1.4f);
        float slide = 1f - PlayerSmoothSaturate(Ui.Clamp01((2.5f - _toastTimer) * 4f));
        float x = 14f - slide * 24f;
        float y = Screen.height - 62f;
        Rect r = Ui.R(x, y, 320f, 48f);
        GUI.color = Ui.C(0f, 0f, 0f, 0.5f * ta);
        GUI.Box(Ui.R(x - 24f, y - 12f, 368f, 104f), PlayerGfx.Blank, PlayerGfx.Shadow);
        GUI.color = Ui.C(_cardBg.r, _cardBg.g, _cardBg.b, _cardBg.a * ta);
        GUI.Box(r, PlayerGfx.Blank, PlayerGfx.Round14);
        if (_cover != null)
        {
            GUI.color = Ui.C(1f, 1f, 1f, ta);
            GUI.Box(Ui.R(x + 7f, y + 7f, 34f, 34f), PlayerGfx.Blank, _cover.TileStyle);
        }
        else
        {
            GUI.color = Ui.C(_artColorCurrent.r, _artColorCurrent.g, _artColorCurrent.b, ta);
            GUI.Box(Ui.R(x + 10f, y + 10f, 28f, 28f), PlayerGfx.Blank, PlayerGfx.Dot);
            GUI.color = Ui.C(1f, 1f, 1f, ta);
            GUI.Box(Ui.R(x + 16f, y + 16f, 16f, 16f), PlayerGfx.Blank, PlayerGfx.Icon(NocturneIcon.Play));
        }
        GUI.color = Ui.C(1f, 1f, 1f, 0.5f * ta);
        GUI.Label(Ui.R(x + 50f, y + 7f, 260f, 14f), NocturneText.T("Сейчас играет", "Now playing"), PlayerGfx.Tiny);
        GUI.color = Ui.C(1f, 1f, 1f, ta);
        GUI.Label(Ui.R(x + 50f, y + 20f, 260f, 24f), _toastText, PlayerGfx.RowOn);
        GUI.color = keep;
    }

    private void TogglePlaylist()
    {
        if (_showPlaylist && _listClosingAt < 0f)
        {
            _find.Focus = false;
            _listClosingAt = Time.unscaledTime;
            _listOpenedAt = -1f;
        }
        else if (!_showPlaylist)
        {
            if (_win)
                LoadRecent();
            _showPlaylist = true;
            _listOpenedAt = Time.unscaledTime;
            _listClosingAt = -1f;
            _listRect = Ui.R(_winRect.m_XMin, _winRect.m_YMin + _winRect.m_Height + 10f, PlayerList.W, _win ? PlayerSearch.HeightFor(_recent.Count) : PlayerList.HeightFor(TrackCount));
            _trackScroll = Ui.V(0f, Ui.Max(0f, _currentIndex * 38f - 114f));
            _find.Focus = _win;
        }
    }

    private void ToggleEqPanel()
    {
        if (_showEqPanel && _eqClosingAt < 0f)
        {
            _eqClosingAt = Time.unscaledTime;
            _eqOpenedAt = -1f;
        }
        else if (!_showEqPanel)
        {
            _showEqPanel = true;
            _eqOpenedAt = Time.unscaledTime;
            _eqClosingAt = -1f;
            _eqPanelRect = Ui.R(_winRect.m_XMin + _winRect.m_Width + 10f, _winRect.m_YMin, PlayerEq.W, PlayerEq.Tall);
            _eqScroll = Ui.V(0f, Ui.Max(0f, _activePresetIdx / 2 * 34f - 68f));
        }
    }

    [HideFromIl2Cpp]
    private void DrawWindow(int _)
    {
        if (_stateFrame != Time.frameCount)
        {
            _stateFrame = Time.frameCount;
            if (_win)
                WinClock();
            else
            {
                _cTime = CurrentTime;
                _cTotal = TotalTime;
                _cPlaying = IsPlaying;
                _cPaused = !_cPlaying && IsPaused;
            }
        }

        Color keep = GUI.color;
        float fold = PlayerSmoothSaturate(_foldT);
        float cardH = PlayerPoster.HeightFor(fold);
        Card(PlayerPoster.W, cardH, _winAlpha);

        var s = _poster.S;
        s.Now = Time.realtimeSinceStartup;
        s.Dt = Time.unscaledDeltaTime;
        s.Alpha = _winAlpha;
        s.Fold = fold;
        s.Beat = _beatLevel;
        s.Glow = _glow;
        s.Phase = _phase;
        s.Time = _cTime;
        s.Total = _cTotal;
        s.Volume = _volume;
        s.Index = _currentIndex;
        s.Count = TrackCount;
        s.Playing = _cPlaying;
        s.Loading = _isLoading;
        s.Shuffle = _shuffle;
        s.Loop = _loop;
        s.EqOn = _activePresetIdx != 0;
        s.ListOn = _showPlaylist;
        s.Art = _artColorCurrent;
        s.Accent = _accent;
        s.Bg = _cardBg;
        s.Cover = _cover;
        s.PrevCover = _prevCover;
        s.SlideDir = _artSlideDir;
        s.Hint = "BepInEx/plugins/Nocturne/Music";
        s.Remote = false;
        s.Idle = false;
        s.Artist = null;
        s.Chip = null;
        s.Service = null;
        s.Pick = false;
        s.CanPrev = s.CanNext = s.CanPlay = s.CanSeek = s.CanShuffle = s.CanLoop = true;

        s.Slide = -1f;
        if (_artSlideStart >= 0f)
        {
            s.Slide = (Time.unscaledTime - _artSlideStart) / 0.55f;
            if (s.Slide >= 1f)
            {
                s.Slide = -1f;
                _artSlideStart = -1f;
            }
        }

        bool has = _currentIndex >= 0 && _currentIndex < _trackNames.Count;
        s.Name = has ? _trackNames[_currentIndex] : TrackCount == 0 ? NocturneText.T("Нет треков", "No tracks") : NocturneText.T("Нет трека", "No track");
        s.Sub = _isLoading ? NocturneText.T("Загрузка…", "Loading…")
            : _cPlaying ? NocturneText.T("Играет", "Playing")
            : _cPaused ? NocturneText.T("Пауза", "Paused")
            : NocturneText.T("Стоп", "Stopped");
        if (_win)
            FillWin(s);

        _poster.Draw();
        Rim(PlayerPoster.W, cardH, _winAlpha, _beatLevel);
        GUI.color = keep;

        var r = _poster.R;
        if (r.Source) NocturneConfig.MusicWindows.Value = !_win;
        if (r.Cycle) _media?.Cycle();
        if (r.Search) TogglePlaylist();
        if (r.Library) OpenLibrary();
        if (r.Prev) Prev();
        if (r.Next) Next();
        if (r.Play) PlayPause();
        if (r.Seek >= 0f)
        {
            if (_win)
            {
                if (_winInfo != null && _winInfo.Length > 0f)
                {
                    _winSeekTo = r.Seek * _winInfo.Length;
                    _winSeekAt = Time.unscaledTime;
                    _winSeekDirty = true;
                }
            }
            else if (_source != null && _source.clip != null && _cTotal > 0f)
                _source.time = r.Seek * _cTotal;
        }
        if (r.Volume >= 0f)
        {
            if (_win)
            {
                _winVolTo = r.Volume;
                _winVolAt = Time.unscaledTime;
                _winVolDirty = true;
            }
            else
                Volume = r.Volume;
        }
        if (r.Mute && _win)
            _media.Mute();
        else if (r.Mute)
        {
            if (_volume > 0.001f)
            {
                _lastVol = _volume;
                Volume = 0f;
            }
            else
                Volume = _lastVol > 0.001f ? _lastVol : 0.7f;
        }
        if (r.Shuffle)
        {
            if (_win)
                _media.Shuffle();
            else
                Shuffle = !_shuffle;
        }
        if (r.Loop)
        {
            if (_win)
                _media.Repeat();
            else
                Loop = !_loop;
        }
        if (r.Eq) ToggleEqPanel();
        if (r.List) TogglePlaylist();
        if (r.Fold) _fold = !_fold;
        if (r.Close) BeginPlayerClose();
    }

    [HideFromIl2Cpp]
    private void DrawPlaylistWindow(int _)
    {
        if (_win)
        {
            DrawSearchWindow();
            return;
        }

        if (_listCount != TrackCount)
        {
            _listCount = TrackCount;
            _listTitle = NocturneText.T($"ПЛЕЙЛИСТ · {TrackCount}", $"PLAYLIST · {TrackCount}");
        }

        Color keep = GUI.color;
        float listH = PlayerList.HeightFor(TrackCount);
        Card(PlayerList.W, listH, _listAlpha);

        var v = _list;
        v.Now = Time.realtimeSinceStartup;
        v.Dt = Time.unscaledDeltaTime;
        v.Alpha = _listAlpha;
        v.Current = _currentIndex;
        v.Playing = _cPlaying;
        v.Accent = _accent;
        v.Bg = _cardBg;
        v.Names = _trackNames;
        v.Times = _trackDurations;
        v.Scroll = _trackScroll;
        v.Title = _listTitle;
        v.Empty = NocturneText.T("Нет треков. Положи .wav/.ogg/.mp3 в BepInEx/plugins/Nocturne/Music", "No tracks. Put .wav/.ogg/.mp3 into BepInEx/plugins/Nocturne/Music");
        v.Draw();
        Rim(PlayerList.W, listH, _listAlpha, 0f);
        GUI.color = keep;
        _trackScroll = v.Scroll;
        if (v.Pick >= 0) Play(v.Pick);
        if (v.Refresh) ScanTracks();
        if (v.Close) TogglePlaylist();
    }

    [HideFromIl2Cpp]
    private void DrawEqPanel(int _)
    {
        Color keep = GUI.color;
        Card(PlayerEq.W, PlayerEq.Tall, _eqAlpha);

        var v = _eq;
        v.Dt = Time.unscaledDeltaTime;
        v.Alpha = _eqAlpha;
        v.Gains = _eqGains;
        v.Preset = _activePresetIdx;
        v.Presets = kPresetNames;
        v.Accent = _accent;
        v.Themes = AccentThemes;
        v.ThemeEn = NocturneConfig.MusicAccent.Value;
        v.Scroll = _eqScroll;
        v.Title = NocturneText.T("ЭКВАЛАЙЗЕР", "EQUALIZER");
        v.PresetsText = NocturneText.T("ПРЕСЕТЫ", "PRESETS");
        v.ThemeText = NocturneText.T("ТЕМА", "THEME");
        v.CustomText = NocturneText.T("Свой", "Custom");
        v.LoudText = NocturneText.T("ГРОМКОСТЬ", "LOUDNESS");
        v.LoudA = NocturneText.T("Тихо", "Quiet");
        v.LoudB = NocturneText.T("Норма", "Normal");
        v.LoudC = NocturneText.T("Громко", "Loud");
        v.Loud = Level;
        v.LoudInfo = _loudInfo;
        v.FxText = NocturneText.T("ЗВУК", "SOUND");
        v.FxA = NocturneText.T("Голос", "Voice");
        v.FxB = NocturneText.T("Наушники", "Headphones");
        v.Fx = (int)Fx;
        v.Draw();
        Rim(PlayerEq.W, PlayerEq.Tall, _eqAlpha, 0f);
        GUI.color = keep;
        _eqScroll = v.Scroll;
        if (v.PickPreset >= 0) SelectPreset(v.PickPreset);
        if (v.PickLoud >= 0) SetLoudness(v.PickLoud);
        if (v.PickFx >= 0) ToggleFx(v.PickFx);
        if (v.Band >= 0) EditBand(v.Band, v.BandGain);
        if (v.Commit) SaveCustom();
        if (v.PickTheme >= 0)
        {
            NocturneConfig.MusicAccent.Value = AccentThemes[v.PickTheme].En;
            _stylesDirty = true;
        }
        if (v.Close) ToggleEqPanel();
    }

    private void UpdateArt()
    {
        if (_coverIdx >= 0 && Time.unscaledTime >= _coverAt)
        {
            int due = _coverIdx;
            _coverIdx = -1;
            if (due == _artIndex && due < _tracks.Count)
                this.StartCoroutine(MakeCover(due, _tracks[due], _trackNames[due]));
        }

        int idx = _currentIndex;
        if (idx == _artIndex) return;

        _coverIdx = -1;
        bool have = idx >= 0 && idx < _tracks.Count;
        if (_artIndex >= 0 && have)
        {
            bool forward = idx > _artIndex
                           || (_artIndex == _tracks.Count - 1 && idx == 0);
            _artSlideDir = forward ? -1 : 1;
            _artSlideStart = Time.unscaledTime;
        }
        _artIndex = idx;
        if (!have)
        {
            _cover = null;
            _prevCover = null;
            _artColor = new Color(0.15f, 0.08f, 0.10f, 1f);
            return;
        }

        if (_cover != null)
            _prevCover = _cover;
        _cover = null;
        PlayerCover c = _covers.Cached(_tracks[idx]);
        float now = Time.unscaledTime;
        if (c != null)
            Arrive(c);
        else if (now - _artAt < 0.8f)
        {
            _coverIdx = idx;
            _coverAt = now + 0.25f;
        }
        else
            this.StartCoroutine(MakeCover(idx, _tracks[idx], _trackNames[idx]));
        _artAt = now;
    }

    private void Arrive(PlayerCover c)
    {
        _cover = c;
        _artColor = c.Color;
    }

    private static PlayerArt ArtFrom(Color32[] px, int w, int h, string name)
    {
        try
        {
            if (px != null)
                return PlayerCovers.Shrink(px, w, h);
            return name != null ? PlayerCovers.Draw(name) : null;
        }
        catch (Exception e)
        {
            NocturnePlugin.Logger?.LogWarning($"[Cover] {e.GetType().Name} {e.Message}");
            return null;
        }
    }

    [HideFromIl2Cpp]
    private IEnumerator MakeCover(int idx, string path, string name)
    {
        PlayerCovers.Hit hit = null;
        bool online = NocturneConfig.MusicOnlineCovers.Value;
        string clean = PlayerGfx.Clean(name);
        bool done = false;
        ThreadPool.QueueUserWorkItem(_ =>
        {
            hit = PlayerCovers.Find(path, clean, online);
            done = true;
        });
        while (!done)
            yield return null;

        Color32[] px = null;
        int w = 0;
        int h = 0;
        if (hit.Bytes != null)
        {
            Texture2D raw = _covers.Decode(hit.Bytes);
            yield return null;
            if (raw != null)
                px = PlayerCovers.Read(raw, out w, out h);
        }
        PlayerArt art = null;
        done = false;
        ThreadPool.QueueUserWorkItem(_ =>
        {
            art = ArtFrom(px, w, h, null) ?? ArtFrom(null, 0, 0, name);
            done = true;
        });
        while (!done)
            yield return null;
        if (art == null)
            yield break;

        bool real = art.Real;
        PlayerCover c = _covers.Store(path, _covers.Bake(art));
        if (idx == _artIndex)
            Arrive(c);
        if (!real)
            NocturnePlugin.Logger?.LogInfo($"[Cover] {clean}: online={online}, search={hit.Ask}, as '{hit.Artist}' / '{hit.Title}'");
        if (!real && hit.Ask)
            this.StartCoroutine(LookupCover(idx, path, hit));
    }

    [HideFromIl2Cpp]
    private IEnumerator LookupCover(int idx, string path, PlayerCovers.Hit hit)
    {
        byte[] bytes = null;
        bool done = false;
        ThreadPool.QueueUserWorkItem(_ =>
        {
            bytes = PlayerCovers.Fetch(hit);
            done = true;
        });
        while (!done)
            yield return null;
        if (bytes == null)
        {
            NocturnePlugin.Logger?.LogInfo($"[Cover] not found for {hit.Artist} - {hit.Title}: {hit.Why}");
            yield break;
        }

        Texture2D raw = _covers.Decode(bytes);
        yield return null;
        int w = 0;
        int h = 0;
        Color32[] px = raw != null ? PlayerCovers.Read(raw, out w, out h) : null;
        PlayerArt art = null;
        if (px != null)
        {
            done = false;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                art = ArtFrom(px, w, h, null);
                done = true;
            });
            while (!done)
                yield return null;
        }
        if (art == null)
        {
            NocturnePlugin.Logger?.LogWarning($"[Cover] could not read the image for {hit.Artist} - {hit.Title} ({bytes.Length} bytes)");
            yield break;
        }
        NocturnePlugin.Logger?.LogInfo($"[Cover] found for {hit.Artist} - {hit.Title}: {hit.Why}");
        PlayerCover c = _covers.Bake(art);

        PlayerCover old = _covers.Swap(path, c);
        if (idx == _artIndex)
        {
            _prevCover = _cover;
            Arrive(c);
        }
        if (old == null)
            yield break;
        float until = Time.unscaledTime + 1.2f;
        while (Time.unscaledTime < until)
            yield return null;
        if (ReferenceEquals(_prevCover, old))
            _prevCover = null;
        if (!ReferenceEquals(_cover, old))
            PlayerCovers.Release(old);
    }

    private void SetSource(bool on)
    {
        _win = on;
        _artIndex = -2;
        _coverIdx = -1;
        _artSlideStart = -1f;
        _winTrack = -1;
        _winCover = -1;
        _winAd = false;
        _winInfo = null;
        _winSeekDirty = false;
        _winSeekAt = -10f;
        _winVolDirty = false;
        _winVolAt = -10f;
        DropWin();
        if (_showPlaylist && _listClosingAt < 0f)
            TogglePlaylist();
        if (!on)
        {
            _media?.Stop();
            _media = null;
            return;
        }

        Stop();
        if (_showEqPanel && _eqClosingAt < 0f)
            ToggleEqPanel();
        try
        {
            _media = new WinMedia();
            _media.Start();
        }
        catch (Exception e)
        {
            NocturnePlugin.Logger?.LogWarning($"[Media] {e.GetType().Name}: {e.Message}");
            Unavailable();
        }
    }

    private void Unavailable()
    {
        NocturneConfig.MusicWindows.Value = false;
        SetSource(false);
        NocturneToast.Push(NocturneText.T("Плеер", "Player"), NocturneText.T("Windows media недоступны", "Windows media is unavailable"), 3.5f, NocturneNotifyKind.Warning);
    }

    private void DropWin()
    {
        _winGen++;
        _cover = null;
        _prevCover = null;
        _artColor = Ui.C(0.15f, 0.08f, 0.10f, 1f);
        if (_winOwned == null) return;
        PlayerCovers.Release(_winOwned);
        _winOwned = null;
    }

    private void UpdateWin()
    {
        if (_media.Failed)
        {
            Unavailable();
            return;
        }

        _media.Fast = _visible || _closingAt >= 0f || _toastTimer > 0f;
        if (_winSeekDirty && Time.unscaledTime - _winSeekSent >= 0.15f)
        {
            _winSeekDirty = false;
            _winSeekSent = Time.unscaledTime;
            _media.Seek(_winSeekTo);
        }
        if (_winVolDirty && Time.unscaledTime - _winVolSent >= 0.06f)
        {
            _winVolDirty = false;
            _winVolSent = Time.unscaledTime;
            _media.Volume(_winVolTo);
        }
        var w = _media.Now;
        _winInfo = w;
        if (w == null) return;

        if (w.Track != _winTrack)
        {
            bool change = _winTrack > 0 && w.Track > 0;
            _winTrack = w.Track;
            if (w.Track == 0)
                DropWin();
            else if (change && !w.Ad)
            {
                _artSlideDir = -1;
                _artSlideStart = Time.unscaledTime;
                if (_cover != null)
                    _prevCover = _cover;
                _cover = null;
                if (w.Title.Length > 0)
                {
                    _toastText = w.Artist.Length > 0 ? w.Artist + " - " + w.Title : w.Title;
                    _toastTimer = 2.5f;
                }
            }
        }

        if (w.Cover != _winCover || w.Ad != _winAd)
        {
            bool keep = w.Ad && _winAd;
            _winCover = w.Cover;
            _winAd = w.Ad;
            if (!keep && w.Cover > 0)
            {
                bool fetch = !w.Ad && (w.Stub || w.Image == null);
                string name = w.Ad ? "Spotify" : w.Artist.Length > 0 ? w.Artist + " - " + w.Title : w.Title;
                this.StartCoroutine(MakeWinCover(++_winGen, w.Ad || fetch ? null : w.Image, name, w.Artist, w.Title, fetch));
            }
        }
    }

    [HideFromIl2Cpp]
    private IEnumerator MakeWinCover(int gen, byte[] bytes, string name, string artist, string title, bool fetch)
    {
        if (fetch && NocturneConfig.MusicOnlineCovers.Value)
        {
            bool found = false;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                bytes = PlayerCovers.Lookup(artist, title);
                found = true;
            });
            while (!found)
                yield return null;
        }

        Color32[] px = null;
        int w = 0;
        int h = 0;
        if (bytes != null)
        {
            Texture2D raw = _covers.Decode(bytes);
            yield return null;
            if (raw != null)
                px = PlayerCovers.Read(raw, out w, out h);
        }
        PlayerArt art = null;
        bool done = false;
        ThreadPool.QueueUserWorkItem(_ =>
        {
            art = ArtFrom(px, w, h, null) ?? ArtFrom(null, 0, 0, name);
            done = true;
        });
        while (!done)
            yield return null;
        if (art == null || gen != _winGen)
            yield break;

        PlayerCover c = _covers.Bake(art);
        PlayerCover old = _winOwned;
        _winOwned = c;
        if (_cover != null)
            _prevCover = _cover;
        Arrive(c);
        if (old == null)
            yield break;
        float until = Time.unscaledTime + 1.2f;
        while (Time.unscaledTime < until)
            yield return null;
        if (ReferenceEquals(_prevCover, old))
            _prevCover = null;
        PlayerCovers.Release(old);
    }

    [HideFromIl2Cpp]
    private void DrawSearchWindow()
    {
        MediaService sv = _winInfo?.Service;
        bool ru = NocturneText.IsRussian;
        if (!ReferenceEquals(sv, _findFor) || ru != _findRu)
        {
            _findFor = sv;
            _findRu = ru;
            _find.Title = sv != null ? NocturneText.T("ПОИСК · ", "SEARCH · ") + sv.Label : NocturneText.T("ПОИСК", "SEARCH");
            _find.Go = sv != null ? NocturneText.T("Найти в ", "Search in ") + sv.Label : NocturneText.T("Сервис не определён", "Service not detected");
            _find.Placeholder = NocturneText.T("Название песни или исполнитель", "Song or artist");
            _find.Recents = NocturneText.T("НЕДАВНИЕ", "RECENT");
        }

        Color keep = GUI.color;
        float h = PlayerSearch.HeightFor(_recent.Count);
        Card(PlayerSearch.W, h, _listAlpha);
        var v = _find;
        v.Now = Time.realtimeSinceStartup;
        v.Dt = Time.unscaledDeltaTime;
        v.Alpha = _listAlpha;
        v.Accent = _accent;
        v.Ready = sv != null;
        v.Recent = _recent;
        v.Draw();
        Rim(PlayerSearch.W, h, _listAlpha, 0f);
        GUI.color = keep;
        if (v.Submit)
            Search(v.Text);
        else if (v.Pick >= 0 && v.Pick < _recent.Count)
            Search(_recent[v.Pick]);
        if (v.Close)
            TogglePlaylist();
    }

    private void Search(string query)
    {
        query = query.Trim();
        var w = _winInfo;
        if (query.Length == 0 || w == null || w.Service == null)
            return;
        _find.Text = query;
        Remember(query);
        MediaService sv = w.Service;
        string web = MediaServices.SearchUrl(sv, query);
        if (sv.App != null && InApp(sv, w.Web))
            OpenLink(MediaServices.AppUrl(sv, query), null, sv, web);
        else
            OpenLink(web, w.Web ? w.Exe : null, sv, null);
    }

    private void OpenLibrary()
    {
        var w = _winInfo;
        if (w == null || w.Service == null)
            return;
        MediaService sv = w.Service;
        if (sv.AppLibrary != null && InApp(sv, w.Web))
            OpenLink(sv.AppLibrary, null, sv, sv.Library);
        else if (sv.Library != null)
            OpenLink(sv.Library, w.Web ? w.Exe : null, sv, null);
    }

    private static bool InApp(MediaService sv, bool web)
    {
        if (!web)
            return true;
        if (sv.AppProc == null)
            return false;
        try
        {
            var list = System.Diagnostics.Process.GetProcessesByName(sv.AppProc);
            bool any = list.Length > 0;
            for (int i = 0; i < list.Length; i++)
                list[i].Dispose();
            return any;
        }
        catch
        {
            return false;
        }
    }

    private static void OpenLink(string url, string exe, MediaService sv, string fallback)
    {
        try
        {
            if (exe != null)
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe, "\"" + url + "\""));
            else if (url.StartsWith("http", StringComparison.Ordinal))
                Application.OpenURL(url);
            else
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception e)
        {
            NocturnePlugin.Logger?.LogWarning($"[Media] open: {e.GetType().Name}: {e.Message}");
            Application.OpenURL(fallback ?? url);
        }
        NocturneToast.Push(NocturneText.T("Плеер", "Player"), NocturneText.T("Открыто: ", "Opened: ") + sv.Label, 2.5f, NocturneNotifyKind.Info);
    }

    private void LoadRecent()
    {
        if (_recentLoaded)
            return;
        _recentLoaded = true;
        string raw = NocturneConfig.MusicSearches.Value;
        if (string.IsNullOrEmpty(raw))
            return;
        string[] parts = raw.Split('|');
        for (int i = 0; i < parts.Length && _recent.Count < 5; i++)
        {
            if (parts[i].Length > 0)
                _recent.Add(parts[i]);
        }
    }

    private void Remember(string query)
    {
        query = query.Replace('|', ' ');
        for (int i = _recent.Count - 1; i >= 0; i--)
        {
            if (string.Equals(_recent[i], query, StringComparison.OrdinalIgnoreCase))
                _recent.RemoveAt(i);
        }
        _recent.Insert(0, query);
        if (_recent.Count > 5)
            _recent.RemoveAt(5);
        NocturneConfig.MusicSearches.Value = string.Join("|", _recent);
    }

    private void HoldMove(bool hold)
    {
        var pc = PlayerControl.LocalPlayer;
        if (hold)
        {
            if (pc == null || !pc.moveable)
                return;
            pc.moveable = false;
            _findMove = true;
            return;
        }
        _findMove = false;
        if (pc != null)
            pc.moveable = true;
    }

    private void WinClock()
    {
        var w = _winInfo;
        float now = Time.unscaledTime;
        float held = now - _winSeekAt;
        _cTime = w == null ? 0f : held < 1f ? Ui.Min(_winSeekTo + (w.Playing ? held * w.Rate : 0f), w.Length) : w.Clock();
        _cTotal = w != null ? w.Length : 0f;
        _cVol = now - _winVolAt < 0.7f ? _winVolTo : w != null && w.Mixer && !w.Muted ? w.Level : 0f;
        _cPlaying = w != null && w.Playing;
        _cPaused = w != null && !w.Playing && w.Track > 0;
    }

    private void NudgeWin(float delta)
    {
        var w = _winInfo;
        if (w == null || !w.Mixer)
            return;
        float now = Time.unscaledTime;
        float cur = now - _winVolAt < 0.7f ? _winVolTo : w.Muted ? 0f : w.Level;
        _winVolTo = Ui.Clamp01(cur + delta);
        _winVolAt = now;
        _winVolDirty = true;
    }

    [HideFromIl2Cpp]
    private void FillWin(PlayerPoster.State s)
    {
        var w = _winInfo;
        s.Remote = true;
        s.Loading = false;
        s.EqOn = false;
        s.ListOn = false;
        s.SearchOn = _showPlaylist;
        s.Volume = _cVol;
        if (w == null || w.Track == 0)
        {
            s.Name = NocturneText.T("Нет активного плеера", "Nothing is playing");
            s.Artist = string.Empty;
            s.Idle = true;
            s.Chip = null;
            s.Service = null;
            s.Pick = false;
            s.Hint = NocturneText.T("Включи музыку в Spotify или браузере", "Start music in Spotify or a browser");
            s.CanPrev = s.CanNext = s.CanPlay = s.CanSeek = s.CanShuffle = s.CanLoop = s.CanSearch = s.CanLibrary = s.CanVolume = false;
            return;
        }

        s.Name = w.Ad ? NocturneText.T("Реклама", "Advertisement") : w.Title.Length > 0 ? w.Title : w.App;
        s.Artist = w.Ad ? "Spotify" : w.Artist;
        s.Idle = false;
        s.Service = w.Service;
        s.Chip = w.Service != null ? w.Service.Label : !w.Web ? w.App : w.Count > 1 ? w.App : null;
        s.CanSearch = w.Service != null;
        s.CanLibrary = w.Service != null && w.Service.Library != null;
        s.CanVolume = w.Mixer;
        s.Pick = w.Count > 1;
        s.CanPrev = w.CanPrev;
        s.CanNext = w.CanNext;
        s.CanPlay = w.CanToggle;
        s.CanSeek = w.CanSeek;
        s.CanShuffle = w.CanShuffle;
        s.CanLoop = w.CanRepeat;
        s.Shuffle = w.Shuffle;
        s.Loop = w.Repeat;
    }

    private void EnsureStyles()
    {
        string acc = NocturneConfig.MusicAccent.Value;
        if (acc != _appliedAccent)
        {
            _appliedAccent = acc;
            _stylesDirty = true;
        }
        if (!_stylesDirty) return;
        _stylesDirty = false;
        PlayerGfx.Ensure();
        float hue = AccentHue();
        _cardBg = Rehue(new Color(0.075f, 0.050f, 0.070f, 0.97f), hue);
        _accent = Rehue(new Color(0.94f, 0.20f, 0.24f, 1f), hue);
    }

    internal void ScanTracks()
    {
        Stop();
        _tracks.Clear();
        _trackNames.Clear();
        _trackDurations.Clear();
        _currentIndex = -1;

        string folder = Path.Combine(BepInEx.Paths.PluginPath, "Nocturne", "Music");
        if (!Directory.Exists(folder))
        {
            try
            {
                Directory.CreateDirectory(folder);
            }
            catch { }
            StatusText = NocturneText.T("Папка Music пуста", "Music folder is empty");
            return;
        }

        foreach (string ext in new[] { "*.wav", "*.ogg", "*.mp3" })
        {
            try
            {
                foreach (string f in Directory.GetFiles(folder, ext))
                {
                    _tracks.Add(f);
                    _trackNames.Add(Path.GetFileNameWithoutExtension(f));
                    _trackDurations.Add(0f);
                }
            }
            catch { }
        }

        StatusText = TrackCount == 0
            ? NocturneText.T("Нет треков", "No tracks found")
            : NocturneText.T($"Найдено: {TrackCount}", $"Found: {TrackCount} tracks");

        if (TrackCount > 0)
            this.StartCoroutine(ScanDurationsAsync());
    }

    [HideFromIl2Cpp]
    private IEnumerator ScanDurationsAsync()
    {
        var paths = _tracks.ToArray();
        var durations = new float[paths.Length];
        bool done = false;
        ThreadPool.QueueUserWorkItem(_ =>
        {
            for (int i = 0; i < paths.Length; i++)
                durations[i] = GetDurationFast(paths[i]);
            done = true;
        });
        while (!done)
            yield return null;
        for (int i = 0; i < paths.Length && i < _trackDurations.Count; i++)
            _trackDurations[i] = durations[i];
    }

    private static float GetDurationFast(string path)
    {
        try
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".mp3")
                using (var r = new Mp3FileReader(path))
                    return (float)r.TotalTime.TotalSeconds;
            if (ext == ".wav")
                using (var r = new WaveFileReader(path))
                    return (float)r.TotalTime.TotalSeconds;
            if (ext == ".ogg")
                using (var vr = new VorbisReader(path))
                    return (float)vr.TotalTime.TotalSeconds;
        }
        catch { }
        return 0f;
    }

    private static int Level => Ui.Clamp(NocturneConfig.MusicLoudness.Value, 0, 2);

    private static PlayerFx Fx
    {
        get
        {
            PlayerFx fx = PlayerFx.None;
            if (NocturneConfig.MusicVoice.Value) fx |= PlayerFx.Voice;
            if (NocturneConfig.MusicHeadphones.Value) fx |= PlayerFx.Phones;
            return fx;
        }
    }

    private void InitEq()
    {
        string saved = NocturneConfig.MusicEqCustom.Value;
        if (!string.IsNullOrEmpty(saved))
        {
            string[] parts = saved.Split(';');
            for (int i = 0; i < parts.Length && i < _custom.Length; i++)
                if (float.TryParse(parts[i], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float g))
                    _custom[i] = Ui.Clamp(g, -12f, 12f);
        }

        int idx = NocturneConfig.MusicEqPreset.Value;
        if (NocturneConfig.MusicEqVer.Value < 2)
        {
            idx = idx >= 0 && idx < kOldPresetNames.Length && idx != 16 ? Array.IndexOf(kPresetNames, kOldPresetNames[idx]) : kDefaultPreset;
            if (idx < 0)
                idx = kDefaultPreset;
            NocturneConfig.MusicEqVer.Value = 2;
        }
        if (NocturneConfig.MusicEqVer.Value < 3)
        {
            NocturneConfig.MusicLoudness.Value = 2;
            NocturneConfig.MusicEqVer.Value = 3;
        }
        SelectPreset(idx);
    }

    private void SelectPreset(int idx)
    {
        idx = Ui.Clamp(idx, 0, kPresetNames.Length - 1);
        _activePresetIdx = idx;
        _eqGains = idx == kCustom ? _custom : kPresetGains[idx];
        NocturneConfig.MusicEqPreset.Value = idx;
        ApplyEq();
    }

    private void ApplyEq(bool drag = false)
    {
        _chain?.SetEq(_eqGains, false);
        _mixer.SetEq(_eqGains);
        if (drag)
            _remeasureAt = Time.unscaledTime + 0.35f;
        else
            Remeasure();
    }

    private void EditBand(int band, float db)
    {
        if (_activePresetIdx != kCustom)
        {
            Array.Copy(_eqGains, _custom, PlayerChain.Bands);
            _activePresetIdx = kCustom;
            _eqGains = _custom;
            NocturneConfig.MusicEqPreset.Value = kCustom;
        }
        _custom[band] = db;
        ApplyEq(true);
    }

    private void SaveCustom()
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < _custom.Length; i++)
        {
            if (i > 0)
                sb.Append(';');
            sb.Append(_custom[i].ToString("0.#", System.Globalization.CultureInfo.InvariantCulture));
        }
        NocturneConfig.MusicEqCustom.Value = sb.ToString();
    }

    private void SetLoudness(int level)
    {
        NocturneConfig.MusicLoudness.Value = Ui.Clamp(level, 0, 2);
        Remeasure();
    }

    private void ToggleFx(int i)
    {
        if (i == 0)
            NocturneConfig.MusicVoice.Value = !NocturneConfig.MusicVoice.Value;
        else
            NocturneConfig.MusicHeadphones.Value = !NocturneConfig.MusicHeadphones.Value;
        _chain?.SetFx(Fx, false);
        _mixer.SetFx(Fx);
        Remeasure();
    }

    private void Remeasure()
    {
        PlayerTrack cur = _playing;
        if (cur == null || cur.Loud == null)
            return;
        if (_measuring)
        {
            _measureAgain = true;
            return;
        }

        _measuring = true;
        float[] eq = (float[])_eqGains.Clone();
        int level = Level;
        PlayerFx fx = Fx;
        PlayerChain chain = _chain;
        ThreadPool.QueueUserWorkItem(_ =>
        {
            try
            {
                PlayerLoud.Result res = cur.Loud.Measure(eq, fx);
                float g = PlayerLoud.Gain(res, level);
                cur.Gain = g;
                cur.Key = PlayerLoud.Key(eq, level, fx);
                _loudInfo = LoudText(res.Lufs, g);
                if (ReferenceEquals(_playing, cur) && chain != null)
                    chain.GainTo = g;
            }
            finally
            {
                _measuring = false;
            }
        });
    }
    internal void Play(int index)
    {
        if (_win || index < 0 || index >= _tracks.Count) return;
        _currentIndex = index;
        _loadGen++;
        if (_pending != null)
        {
            Retire(_pending, 0.1f);
            _pending = null;
        }
        float now = Time.unscaledTime;
        float start = now - _playAt < 0.8f ? now + 0.22f : now;
        _playAt = now;
        this.StartCoroutine(LoadAndPlay(index, _loadGen, start));
    }

    private void Retire(PlayerTrack t, float hold)
    {
        if (t == null || t.Retired)
            return;
        t.Retired = true;
        t.Cancelled = true;
        t.ReleaseAt = Time.unscaledTime + hold;
        _limbo.Add(t);
    }

    private void ReleaseLimbo()
    {
        float now = Time.unscaledTime;
        for (int i = _limbo.Count - 1; i >= 0; i--)
        {
            PlayerTrack t = _limbo[i];
            if (now < t.ReleaseAt || !t.Finished || t.Pinned)
                continue;
            t.Give();
            _limbo.RemoveAt(i);
        }
    }
    internal void PlayPause()
    {
        if (_win)
        {
            _media.Toggle();
            return;
        }
        if (_source == null) return;
        if (_source.isPlaying)
        {
            _source.Pause();
            return;
        }
        if (_source.clip != null)
        {
            _source.UnPause();
            return;
        }
        Play(_currentIndex >= 0 ? _currentIndex : 0);
    }

    internal void Stop()
    {
        if (_source == null)
            return;
        _loadGen++;
        if (_pending != null)
        {
            Retire(_pending, 0.1f);
            _pending = null;
        }
        _isLoading = false;
        _source.Stop();
        if (_source.clip != null)
        {
            try
            {
                UnityEngine.Object.Destroy(_source.clip);
            }
            catch { }
            _source.clip = null;
        }
        _autoPlayed = false;
        _mixer.Clear();
        Retire(_playing, 0.5f);
        _playing = null;
    }

    internal void Next()
    {
        if (_win)
        {
            _media.Next();
            return;
        }
        if (TrackCount == 0) return;
        Play(_shuffle ? UnityEngine.Random.Range(0, TrackCount) : (_currentIndex + 1) % TrackCount);
    }

    internal void Prev()
    {
        if (_win)
        {
            _media.Prev();
            return;
        }
        if (TrackCount == 0) return;
        Play(_currentIndex <= 0 ? TrackCount - 1 : _currentIndex - 1);
    }

    [HideFromIl2Cpp]
    private void PlayRawTrack(PlayerTrack raw, string sourcePath)
    {
        if (_source == null || raw == null || raw.Samples == null || raw.Channels < 1) return;

        PlayerTrack old = _playing;
        int at = 0;
        int span = 0;
        if (old != null && _chain != null && _source.isPlaying && old.Channels == raw.Channels && old.Frequency == raw.Frequency)
        {
            at = Math.Max(0, Math.Min(_rawReadPos, (int)(_source.time * old.Frequency)));
            span = PlayerMixer.Plan(old, at);
        }
        _source.Stop();
        if (_source.clip != null)
        {
            try
            {
                UnityEngine.Object.Destroy(_source.clip);
            }
            catch { }
            _source.clip = null;
        }
        if (span > 0)
            _mixer.Enter(old, _chain, at, span);
        else
            _mixer.Begin();
        Retire(old, 0.5f);

        var chain = new PlayerChain(raw.Channels, raw.Frequency);
        chain.SetEq(_eqGains, true);
        chain.SetFx(Fx, true);
        chain.SetVolume(_volume);
        chain.SetGain(raw.Gain, true);
        if (_lim == null || _limCh != raw.Channels || _limRate != raw.Frequency)
        {
            _lim = new PlayerLimiter(raw.Channels, raw.Frequency);
            _limCh = raw.Channels;
            _limRate = raw.Frequency;
        }
        else if (span == 0 && !_mixer.Busy)
            _lim.Reset();
        _chain = chain;
        _rawReadPos = 0;
        _playing = raw;
        _loudInfo = raw.Info;

        string name = Path.GetFileNameWithoutExtension(sourcePath);
        AudioClip clip = AudioClip.Create(name, raw.Total, raw.Channels, raw.Frequency, true,
            (Action<Il2CppStructArray<float>>)PcmReaderCallback,
            (Action<int>)PcmSetPositionCallback);

        _source.clip = clip;
        _source.loop = _loop;
        _source.volume = _volume;
        _source.time = 0f;
        _source.Play();
        _autoPlayed = true;
        StatusText = _currentIndex >= 0 && _currentIndex < _trackNames.Count ? _trackNames[_currentIndex] : string.Empty;
        if (_currentIndex >= 0 && _currentIndex < _trackNames.Count)
        {
            _toastText = PlayerGfx.Clean(_trackNames[_currentIndex]);
            _toastTimer = 2.5f;
        }
    }

    [HideFromIl2Cpp]
    private void MeasureTrack(PlayerTrack t, bool final)
    {
        float[] eq = (float[])_eqGains.Clone();
        int level = Level;
        PlayerFx fx = Fx;
        var loud = new PlayerLoud(t.Samples, t.Ready, t.Channels, t.Frequency);
        PlayerLoud.Result res = loud.Measure(eq, fx);
        float g = PlayerLoud.Gain(res, level);
        t.Gain = g;
        t.Key = PlayerLoud.Key(eq, level, fx);
        t.Info = LoudText(res.Lufs, g);
        if (!final)
            return;

        t.Loud = loud;
        if (!ReferenceEquals(_playing, t))
            return;
        _chain?.Settle(g);
        _loudInfo = t.Info;
        if (t.Key != PlayerLoud.Key(_eqGains, Level, Fx))
            _measureAgain = true;
    }

    private static string LoudText(float lufs, float gain)
    {
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        return lufs.ToString("0.0", inv) + " LUFS  " + (20f * MathF.Log10(gain)).ToString("+0.0;-0.0;0.0", inv) + " dB";
    }

    [HideFromIl2Cpp]
    private unsafe void PcmReaderCallback(Il2CppStructArray<float> data)
    {
        int len = data.Length;
        float* dst = (float*)((byte*)data.Pointer + IntPtr.Size * 4);
        PlayerTrack raw = _playing;
        var chain = _chain;
        var lim = _lim;
        float[] src = raw?.Samples;
        if (src == null || chain == null || lim == null || raw.Channels < 1)
        {
            new Span<float>(dst, len).Clear();
            return;
        }

        int ch = raw.Channels;
        int frames = len / ch;
        int pos = _rawReadPos;
        int n = Math.Max(0, Math.Min(frames, raw.Ready - pos));
        if (_mixA == null || _mixA.Length < frames * ch)
            _mixA = new float[frames * ch];
        var mix = _mixA;
        chain.Process(src, pos, n, mix);
        Array.Clear(mix, n * ch, (frames - n) * ch);
        _mixer.Mix(mix, n, frames, ch);

        lim.Process(mix, frames);
        int used = frames * ch;
        mix.AsSpan(0, used).CopyTo(new Span<float>(dst, used));
        if (used < len)
            new Span<float>(dst + used, len - used).Clear();
        _rawReadPos = pos + frames;
    }

    [HideFromIl2Cpp]
    private void PcmSetPositionCallback(int newFrame)
    {
        _rawReadPos = Math.Max(0, newFrame);
        _chain?.Reset();
        _lim?.Reset();
    }
    [HideFromIl2Cpp]
    private IEnumerator LoadAndPlay(int index, int gen, float start)
    {
        _isLoading = true;
        StatusText = NocturneText.T("Загрузка...", "Loading...");

        while (Time.unscaledTime < start)
            yield return null;
        if (_loadGen != gen)
            yield break;

        string path = _tracks[index];
        PlayerTrack track = PlayerTrack.Begin(path, AudioSettings.outputSampleRate, MeasureTrack);
        _pending = track;
        while (!track.Started)
            yield return null;
        if (ReferenceEquals(_pending, track))
            _pending = null;

        if (_loadGen != gen)
        {
            Retire(track, 0.1f);
            yield break;
        }
        _isLoading = false;

        if (track.Failed)
        {
            StatusText = NocturneText.T("Ошибка загрузки", "Load error");
            NocturnePlugin.Logger?.LogWarning($"[Music] {Path.GetFileName(path)}: {track.Error}");
            Retire(track, 0.1f);
            yield break;
        }
        PlayRawTrack(track, path);
    }
}