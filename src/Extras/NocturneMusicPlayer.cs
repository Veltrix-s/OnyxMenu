using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using BepInEx.Unity.IL2CPP.Utils;
using Il2CppInterop.Runtime.Attributes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using NVorbis;
using UnityEngine;

namespace Nocturne;

public sealed class NocturneMusicPlayer : MonoBehaviour
{
    private static NocturneMusicPlayer _instance;
    internal static NocturneMusicPlayer Instance => _instance;

    private AudioSource _source;
    private GUI.WindowFunction _winFn, _listFn, _eqFn;
    private readonly List<string> _tracks       = new List<string>();
    private readonly List<string> _trackNames  = new List<string>();
    private readonly List<float>  _trackDurations = new List<float>();

    private sealed class RawTrack { public float[] Samples; public int Channels; public int Frequency; }
    private readonly Dictionary<int, RawTrack> _loadedRaw = new Dictionary<int, RawTrack>();

    private float[] _rawSamples;
    private int     _rawChannels  = 2;
    private int     _rawFrequency = 44100;
    private int     _rawTotal;
    private int     _rawReadPos;

    private double[] _eqX1, _eqX2, _eqY1, _eqY2;

    private volatile bool _eqStateResetPending;

    private float[] _bassEnhState;
    private float[] _lcBassState;
    private float   _sideBassState;
    private float[] _deessLpfLow;
    private float[] _deessLpfHigh;
    private float[] _deessEnv;
    private float[] _deessGain;
    private float[] _cfLpf;
    private float[] _dcX1;
    private float[] _dcY1;
    private float[] _limBuf;
    private int     _limWritePos;
    private float   _limEnvPeak;
    private float   _limCurrGain = 1f;
    private const int   kLookaheadFramesC = 240;
    private const float kStereoWidth      = 0.15f;
    private const float kBassEnhAmount    = 0.14f;
    private const float kBassEnhCutoffHz  = 140f;
    private const float kLcBassCutoffHz   = 200f;
    private const float kSideBassCutoffHz = 250f;
    private const float kSideBassNarrow   = 0.88f;

    private const float kDeessLowCutoffHz  = 2800f;
    private const float kDeessHighCutoffHz = 8500f;
    private const float kDeessThreshold    = 0.13f;
    private const float kDeessRatio        = 3.0f;
    private const float kDeessMinGain      = 0.45f;
    private const float kPostLimiterCeil   = 0.94f;
    private const float kLcTrebleAmount    = 0.05f;

    private const float kCrossfeedCutoffHz = 720f;
    private const float kCrossfeedMix      = 0.20f;
    private const float kCrossfeedComp     = 0.90f;

    private const float kSaturationAmount  = 0.07f;

    private const float kDcBlockerR        = 0.9995f;

    private volatile bool _crossfading;
    private int     _crossfadeFrames;
    private int     _crossfadeFramesDone;
    private float[] _rawSamplesB;
    private int     _rawChannelsB;
    private int     _rawTotalB;
    private int     _rawReadPosB;
    private double[] _eqX1B, _eqX2B, _eqY1B, _eqY2B;
    private float[] _bassEnhStateB;
    private float[] _lcBassStateB;
    private float   _sideBassStateB;
    private float[] _deessLpfLowB;
    private float[] _deessLpfHighB;
    private float[] _deessEnvB;
    private float[] _deessGainB;
    private const float kCrossfadeSec = 2.5f;

    private sealed class EqCoeffs
    {
        public readonly double[] B0, B1, B2, A1, A2;
        public readonly double   PreAtten;
        public EqCoeffs(double[] b0, double[] b1, double[] b2, double[] a1, double[] a2, double pre)
        { B0=b0; B1=b1; B2=b2; A1=a1; A2=a2; PreAtten=pre; }
    }
    private volatile EqCoeffs _eqCoeffs;

    private int   _currentIndex = -1;
    private int   _loadGen;
    private bool  _isLoading;
    private float _volume  = 0.7f;
    private bool  _loop;
    private bool  _shuffle;
    private bool  _autoPlayed;
    internal string StatusText = string.Empty;

    internal bool  IsPlaying    => _source != null && _source.isPlaying;
    internal bool  IsPaused     => _source != null && !_source.isPlaying && _source.clip != null && _source.time > 0f;
    internal int   CurrentIndex => _currentIndex;
    internal int   TrackCount   => _tracks.Count;
    internal bool  IsLoading    => _isLoading;
    internal bool  Loop         { get => _loop;    set { _loop    = value; if (_source != null) _source.loop = value; } }
    internal bool  Shuffle      { get => _shuffle; set => _shuffle = value; }
    internal float CurrentTime  => _source != null ? _source.time : 0f;
    internal float TotalTime    => _source != null && _source.clip != null ? _source.clip.length : 0f;

    [HideFromIl2Cpp]
    internal IReadOnlyList<string> TrackNames => _trackNames;

    internal float Volume
    {
        get => _volume;
        set { _volume = Mathf.Clamp01(value); if (_source != null) _source.volume = _volume; }
    }

    private bool   _visible;
    private Rect   _winRect = new Rect(80f, 80f, 460f, 160f);
    private Vector2 _trackScroll;
    private bool   _stylesDirty = true;
    private float  _openedAt  = -1f;
    private float  _closingAt = -1f;
    private float  _winAlpha  = 1f;
    private float  _artRotation;
    private float  _artSpinSpeed;
    private float  _beatLevel;

    private float  _artSlideStart = -1f;
    private int    _artSlideDir;
    private Color  _artPrevColor = new Color(0.15f, 0.08f, 0.10f, 1f);
    private string _artPrevLetter = "♪";

    private static Texture2D _artOverlayTex;
    private GUIStyle _artOverlayStyle;

    private GUIStyle _winStyle;
    private GUIStyle _titleStyle;
    private GUIStyle _btnStyle;

    private static Texture2D _bgTex;
    private static Texture2D _headerTex;
    private static Texture2D _accentTex;
    private static Texture2D _darkTex;
    private static Texture2D _darkerTex;
    private static Texture2D _trackActiveTex;
    private static Texture2D _roundedBgTex;
    private static Texture2D _cornerTL, _cornerTR, _cornerBL, _cornerBR;
    private GUIStyle _cornerStyleTL, _cornerStyleTR, _cornerStyleBL, _cornerStyleBR;

    private bool _showPlaylist;
    private Rect _listRect    = new Rect(80f, 260f, 460f, 200f);
    private float _listOpenedAt  = -1f;
    private float _listClosingAt = -1f;

    private bool   _showEqPanel;
    private Rect   _eqPanelRect  = new Rect(546f, 80f, 230f, 420f);
    private float  _eqOpenedAt  = -1f;
    private float  _eqClosingAt = -1f;
    private int    _activePresetIdx;
    private float[] _eqGains     = new float[10];

    private readonly float[] _eqHeights = new float[14];
    private readonly float[] _eqTargets = new float[14];
    private readonly float[] _eqTimers  = new float[14];
    private readonly float[][] _eqTrail = { new float[14], new float[14], new float[14] };
    private float _eqTrailTimer;
    private float[] _waveformHeights;
    private int     _waveformSeed = -1;
    private bool  _sweepActive;
    private float _sweepTimer;

    private float _marqueeStart;

    private GUIContent _marqueeContent;
    private Texture2D _artTex;

    private Color _clrToastAccent  = new Color(0.85f, 0.10f, 0.15f, 1f);
    private Color _clrScrubFill    = new Color(0.94f, 0.20f, 0.24f, 1f);
    private Color _clrScrubFillHi  = new Color(1.00f, 0.55f, 0.55f, 1f);
    private Color _clrScrubDim     = new Color(0.32f, 0.10f, 0.13f, 1f);
    private Color _clrScrubDimHi   = new Color(0.85f, 0.30f, 0.30f, 1f);
    private Color _clrThumbGlow    = new Color(0.80f, 0.10f, 0.15f, 1f);
    private Color _clrPlayheadGlow = new Color(1.00f, 0.85f, 0.85f, 1f);
    private Color _clrEqBot        = new Color(0.38f, 0.04f, 0.07f, 1f);
    private Color _clrEqTop        = new Color(1.00f, 0.52f, 0.62f, 1f);
    private Color _clrEqBase       = new Color(0.80f, 0.10f, 0.15f, 1f);
    private Color _clrArtStripBg   = new Color(0.20f, 0.07f, 0.09f, 1f);
    private Color _clrArtStripFill = new Color(0.95f, 0.18f, 0.22f, 1f);

    private Color     _artColor        = new Color(0.52f, 0.08f, 0.15f, 1f);
    private Color     _artColorCurrent = new Color(0.52f, 0.08f, 0.15f, 1f);

    private Color     _artColorLastUploaded = new Color(-1f, -1f, -1f, -1f);
    private int       _artIndex = -2;
    private GUIStyle  _artStyle;
    private GUIStyle  _artBgStyle;
    private GUIStyle  _artLetterStyle;
    private static Texture2D _artRoundedTex;
    private GUIStyle  _seekTimeStyle;
    private GUIStyle  _seekRemainStyle;

    private readonly System.Collections.Generic.Dictionary<int, float> _hoverIntensity =
        new System.Collections.Generic.Dictionary<int, float>();
    private GUIStyle  _trackBadgeStyle;
    private GUIStyle  _progRemain;
    private float     _toastTimer;
    private string    _toastText = string.Empty;
    private GUIStyle  _toastStyle;
    private GUIStyle  _toastLabelStyle;
    private GUIStyle  _eqBar;
    private GUIStyle  _eqBarGrad;
    private GUIStyle  _eqPeakBar;
    private GUIStyle  _eqSeg;
    private static Texture2D _eqGradTex;
    private static Texture2D _eqPeakTex;
    private readonly float[] _eqPeaks = new float[14];
    private GUIStyle  _bigTimeStyle;
    private GUIStyle  _totalTimeStyle;
    private GUIStyle  _displayBgStyle;
    private GUIStyle  _controlsBarStyle;
    private GUIStyle  _brandLabelStyle;

    private static readonly string[] kPresetNames =
    {
        "(Default)",   "Acoustic",       "Bass Boost",  "Classical",
        "Club",        "Dance",          "Electronic",  "Full Bass",
        "Full Bass+Treble",              "Full Treble", "Hip-Hop",
        "Jazz",        "Laptop",         "Large Hall",  "Live",
        "Loudness",    "Metal",          "Party",       "Piano",
        "Pop",         "R&B",            "Reggae",      "Rock",
        "Ska",         "Soft",           "Soft Rock",   "Techno",
        "Vocal Boost",
    };
    private static readonly float[] kEqBandFreqs =
        { 32f, 64f, 125f, 250f, 500f, 1000f, 2000f, 4000f, 8000f, 16000f };
    private static readonly float[][] kPresetGains =
    {
        new float[] {  0,  0,  0,  0,  0,  0,  0,  0,  0,  0 },
        new float[] {  5,  4,  3,  1,  0, -1, -1,  0,  2,  4 },
        new float[] { 10,  8,  6,  3,  1,  0,  0,  0,  0,  0 },
        new float[] {  0,  0,  0,  0,  0,  0,-10,-10,-10,-12 },
        new float[] {  0,  0,  4,  7,  7,  7,  4,  0,  0,  0 },
        new float[] { 12,  9,  2, -1, -1, -9,-10,-10, -1, -1 },
        new float[] {  8,  7,  2, -3, -3,  0,  2,  5,  7,  8 },
        new float[] { 12, 12, 12,  7,  2, -6,-12,-12,-12,-12 },
        new float[] {  9,  7,  0,-10, -7,  2, 10, 12, 12, 12 },
        new float[] {-12,-12,-12, -6,  3, 12, 12, 12, 12, 12 },
        new float[] {  8,  7,  4,  3, -1, -1,  0,  2,  3,  4 },
        new float[] {  4,  3,  1,  2, -2, -2,  0,  2,  4,  5 },
        new float[] {  5, 12,  6, -5, -4,  2,  5, 12, 12, 12 },
        new float[] { 12, 12,  7,  7,  0, -7, -7, -7,  0,  0 },
        new float[] { -7,  0,  5,  6,  7,  7,  5,  3,  3,  2 },
        new float[] {  6,  5,  0, -5, -6,  0,  0,  0,  6, 10 },
        new float[] { 10,  6,  1, -4, -3,  1,  3,  1,  6,  8 },
        new float[] {  9,  9,  0,  0,  0,  0,  0,  0,  9,  9 },
        new float[] {  0,  2,  4,  6,  5,  4,  4,  3,  2,  2 },
        new float[] { -3,  5,  9,  9,  6, -2, -4, -4, -3, -3 },
        new float[] {  7,  6,  2, -2, -3,  2,  4,  5,  6,  5 },
        new float[] {  0,  0, -2, -9,  0,  8,  8,  0,  0,  0 },
        new float[] {  9,  5, -8,-11, -5,  5, 11, 12, 12, 12 },
        new float[] { -4, -7, -6, -2,  5,  7, 11, 12, 12, 12 },
        new float[] {  5,  2, -2, -4, -2,  5, 10, 12, 12, 12 },
        new float[] {  5,  5,  2, -2, -6, -8, -5, -2,  3, 11 },
        new float[] {  9,  7,  0, -8, -7,  0,  9, 12, 12, 11 },
        new float[] { -3, -3, -2,  2,  6,  7,  5,  3,  1,  0 },
    };

    private static readonly Color[] kArtPalette =
    {
        new Color(0.72f, 0.10f, 0.16f, 1f),
        new Color(0.12f, 0.35f, 0.72f, 1f),
        new Color(0.12f, 0.58f, 0.35f, 1f),
        new Color(0.62f, 0.32f, 0.05f, 1f),
        new Color(0.52f, 0.10f, 0.62f, 1f),
        new Color(0.05f, 0.52f, 0.60f, 1f),
        new Color(0.65f, 0.50f, 0.08f, 1f),
        new Color(0.10f, 0.50f, 0.50f, 1f),
    };

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
        try
        {
            string name = NocturneConfig.MusicAccent.Value;
            foreach (var t in AccentThemes)
                if (string.Equals(t.En, name, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(t.Ru, name, StringComparison.Ordinal))
                    return t.Hue;
        }
        catch { }
        return 0.986f;
    }

    private static Color Rehue(Color c, float hue)
    {
        Color.RGBToHSV(c, out _, out float s, out float v);
        Color o = Color.HSVToRGB(hue, s, v);
        o.a = c.a;
        return o;
    }

    private static Color WithA(Color c, float a) => new Color(c.r, c.g, c.b, a);

    private void Awake()
    {
        _instance = this;
        _source = gameObject.AddComponent<AudioSource>();
        _source.volume               = _volume;
        _source.playOnAwake          = false;
        _source.loop                 = _loop;
        _source.priority             = 0;

        _source.spatialBlend         = 0f;
        _source.dopplerLevel         = 0f;
        _source.bypassEffects        = true;
        _source.bypassListenerEffects= true;
        _source.bypassReverbZones    = true;
        _source.panStereo            = 0f;
        _source.pitch                = 1f;
        ScanTracks();
        if (NocturneConfig.MusicEqPreset.Value > 0
            && NocturneConfig.MusicEqPreset.Value < kPresetNames.Length)
            SelectPreset(NocturneConfig.MusicEqPreset.Value);
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    private void BeginPlayerOpen()
    {
        _visible   = true;
        _openedAt  = Time.unscaledTime;
        _closingAt = -1f;
    }

    private void BeginPlayerClose()
    {
        _closingAt = Time.unscaledTime;
    }

    private static bool IsChatOpen()
    {

        if (LobbyBehaviour.Instance == null && ShipStatus.Instance == null) return false;
        try
        {
            return HudManager.Instance != null
                && HudManager.Instance.Chat != null
                && HudManager.Instance.Chat.IsOpenOrOpening;
        }
        catch { return false; }
    }

    private static float PlayerEaseOutBack(float t, float overshoot = 1.1f)
    {
        t = Mathf.Clamp01(t);
        float v = t - 1f;
        return 1f + (overshoot + 1f) * v * v * v + overshoot * v * v;
    }

    private static float PlayerSmoothSaturate(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    private void Update()
    {
        if (_closingAt >= 0f && Time.unscaledTime - _closingAt >= 0.42f)
        {
            _visible   = false;
            _closingAt = -1f;
        }

        bool chatOpen = IsChatOpen();
        bool noTextFieldFocused = GUIUtility.keyboardControl == 0 && !NocturneMenu.Typing && !NocturneChatWindow.Typing && !Patches.NocturneTextFocus.Any;

        if (!NocturneMenu.Rebinding && NocturneConfig.MusicToggleKey.Value != KeyCode.None && UnityEngine.Input.GetKeyDown(NocturneConfig.MusicToggleKey.Value) && noTextFieldFocused && !chatOpen)
        {
            if (!_visible && _closingAt < 0f) BeginPlayerOpen();
            else if (_visible && _closingAt < 0f) BeginPlayerClose();
        }

        if (noTextFieldFocused && !chatOpen && !NocturneMenu.Rebinding)
        {
            float dt0 = Time.deltaTime;
            if (NocturneConfig.MusicPrevKey.Value      != KeyCode.None && UnityEngine.Input.GetKeyDown(NocturneConfig.MusicPrevKey.Value))      Prev();
            if (NocturneConfig.MusicNextKey.Value      != KeyCode.None && UnityEngine.Input.GetKeyDown(NocturneConfig.MusicNextKey.Value))      Next();
            if (NocturneConfig.MusicPlayPauseKey.Value != KeyCode.None && UnityEngine.Input.GetKeyDown(NocturneConfig.MusicPlayPauseKey.Value)) PlayPause();
            if (NocturneConfig.MusicStopKey.Value      != KeyCode.None && UnityEngine.Input.GetKeyDown(NocturneConfig.MusicStopKey.Value))      Stop();
            float volDelta = 0f;
            if (NocturneConfig.MusicVolumeUpKey.Value   != KeyCode.None && UnityEngine.Input.GetKey(NocturneConfig.MusicVolumeUpKey.Value))   volDelta += dt0 * 0.6f;
            if (NocturneConfig.MusicVolumeDownKey.Value != KeyCode.None && UnityEngine.Input.GetKey(NocturneConfig.MusicVolumeDownKey.Value)) volDelta -= dt0 * 0.6f;
            if (volDelta != 0f) Volume = Mathf.Clamp01(_volume + volDelta);
        }

        float dt = Time.deltaTime;
        const int kBarsUpd = 14;
        if (IsPlaying)
        {
            for (int i = 0; i < kBarsUpd; i++)
            {
                _eqTimers[i] -= dt;
                if (_eqTimers[i] <= 0f)
                {
                    _eqTargets[i] = UnityEngine.Random.Range(0.15f, 1.0f);
                    _eqTimers[i]  = UnityEngine.Random.Range(0.08f, 0.28f);
                }
                _eqHeights[i] = Mathf.Lerp(_eqHeights[i], _eqTargets[i], dt * 10f);
            }
        }
        else
        {
            for (int i = 0; i < kBarsUpd; i++)
                _eqHeights[i] = Mathf.Lerp(_eqHeights[i], 0f, dt * 5f);
        }
        for (int i = 0; i < _eqPeaks.Length; i++)
        {
            if (_eqHeights[i] > _eqPeaks[i])
                _eqPeaks[i] = _eqHeights[i];
            else
                _eqPeaks[i] = Mathf.Max(0f, _eqPeaks[i] - dt * 0.55f);
        }

        _eqTrailTimer -= dt;
        if (_eqTrailTimer <= 0f)
        {
            _eqTrailTimer = 0.08f;
            Array.Copy(_eqTrail[1], _eqTrail[2], 14);
            Array.Copy(_eqTrail[0], _eqTrail[1], 14);
            Array.Copy(_eqHeights,  _eqTrail[0], 14);
        }

        if (_sweepActive)
        {
            _sweepTimer += dt;
            if (_sweepTimer >= 0.4f) _sweepActive = false;
        }

        float peakEQ = 0f;
        for (int i = 0; i < _eqHeights.Length; i++)
            if (_eqHeights[i] > peakEQ) peakEQ = _eqHeights[i];
        float beatTarget = IsPlaying ? Mathf.Clamp01((peakEQ - 0.55f) / 0.45f) : 0f;
        _beatLevel = Mathf.Lerp(_beatLevel, beatTarget, dt * 14f);

        float targetSpin = IsPlaying ? 60f : 0f;
        _artSpinSpeed = Mathf.Lerp(_artSpinSpeed, targetSpin, dt * 1.4f);
        _artRotation += _artSpinSpeed * dt;
        if (_artRotation >= 360f) _artRotation -= 360f;

        _artColorCurrent = Color.Lerp(_artColorCurrent, _artColor, dt * 5f);

        bool playerRendered = _visible || _closingAt >= 0f || _openedAt >= 0f;
        if (playerRendered && _artStyle != null && _artTex != null)
        {
            float dr = _artColorCurrent.r - _artColorLastUploaded.r;
            float dg = _artColorCurrent.g - _artColorLastUploaded.g;
            float db = _artColorCurrent.b - _artColorLastUploaded.b;
            float da = _artColorCurrent.a - _artColorLastUploaded.a;
            if (dr < 0) dr = -dr; if (dg < 0) dg = -dg; if (db < 0) db = -db; if (da < 0) da = -da;
            if (dr + dg + db + da > 0.008f)
            {
                _artTex.SetPixel(0, 0, _artColorCurrent);
                _artTex.Apply();
                _artStyle.normal.background = _artTex;
                _artColorLastUploaded = _artColorCurrent;
            }
        }

        if (_toastTimer > 0f) _toastTimer -= dt;

        if (_source == null || _isLoading || _source.isPlaying || _source.clip == null) return;
        if (!_loop && _autoPlayed && _source.time == 0f)
        {
            _autoPlayed = false;
            Next();
        }
    }

    internal void DrawGui()
    {

        if (_toastTimer > 0f && _toastText.Length > 0)
        {
            EnsureStyles();
            GUI.skin = null;
            if (_toastStyle != null && _toastLabelStyle != null && _eqSeg != null)
            {
                float ta = Mathf.Clamp01(_toastTimer * 1.4f);
                const float tw = 320f, th = 38f;
                float tx = 12f;
                float ty = Screen.height - th - 12f;
                var   ocT = GUI.color;
                GUI.color = new Color(0f, 0f, 0f, ta * 0.4f);
                GUI.Box(new Rect(tx + 3f, ty + 3f, tw, th), GUIContent.none, _toastStyle);
                GUI.color = new Color(1f, 1f, 1f, ta);
                GUI.Box(new Rect(tx, ty, tw, th), GUIContent.none, _toastStyle);
                GUI.color = WithA(_clrToastAccent, ta);
                GUI.Box(new Rect(tx, ty, 3f, th), GUIContent.none, _eqSeg);
                GUI.color = new Color(1f, 1f, 1f, ta);
                GUI.Label(new Rect(tx + 10f, ty, tw - 14f, th), "▶  " + _toastText, _toastLabelStyle);
                GUI.color = ocT;
            }
        }

        if (!_visible && _closingAt < 0f) return;
        EnsureStyles();
        GUI.skin = null;

        float alpha   = 1f;
        float offsetY = 0f;
        bool  closing = _closingAt >= 0f;
        bool  opening = !closing && _openedAt >= 0f;
        if (closing)
        {
            float t  = Mathf.Clamp01((Time.unscaledTime - _closingAt) / 0.34f);
            alpha    = 1f - PlayerSmoothSaturate(t);
            offsetY  = t * t * t * t * 18f;
        }
        else if (opening)
        {
            float t = Mathf.Clamp01((Time.unscaledTime - _openedAt) / 0.36f);
            if (t < 0.995f)
            {
                alpha   = PlayerSmoothSaturate(t);
                offsetY = (1f - PlayerEaseOutBack(t)) * -24f;
            }
            else
            {
                _openedAt = -1f;
                opening   = false;
            }
        }
        bool animating = closing || opening;
        _winAlpha = alpha;

        Color prevColor = GUI.color;
        GUI.color = new Color(prevColor.r, prevColor.g, prevColor.b, prevColor.a * alpha);

        Rect drawRect = new Rect(_winRect.x, _winRect.y + offsetY, 460f, _winRect.height);
        Rect nextRect = drawRect;
        try
        {
            if (_winFn == null) _winFn = (GUI.WindowFunction)DrawWindow;
            nextRect = GUILayout.Window(772100, drawRect, _winFn,
                string.Empty, _winStyle);
        }
        catch { }
        finally { GUI.color = prevColor; }

        if (!animating)
        {
            _winRect       = nextRect;
            _winRect.width = 460f;
            _winRect.x     = Mathf.Clamp(_winRect.x, 0f, Screen.width  - _winRect.width);
            _winRect.y     = Mathf.Clamp(_winRect.y, 0f, Screen.height - 60f);
        }

        Rect dr = animating ? drawRect : _winRect;
        if (_eqSeg != null)
        {
            var ocB = GUI.color;

            float beatMul = 1f + _beatLevel * 0.55f;
            float[] sAlpha = { 0.28f, 0.15f, 0.06f };
            for (int s = 0; s < 3; s++)
            {
                float o = (s + 1) * 2f;
                GUI.color = new Color(0f, 0f, 0f, sAlpha[s] * alpha * beatMul);
                GUI.Box(new Rect(dr.x + o,  dr.yMax + o, dr.width, 3f),      GUIContent.none, _eqSeg);
                GUI.Box(new Rect(dr.xMax + o, dr.y + o,  3f, dr.height + o), GUIContent.none, _eqSeg);
            }

            if (_beatLevel > 0.02f)
            {
                Color rim = _artColorCurrent;
                GUI.color = new Color(rim.r, rim.g, rim.b, 0.32f * _beatLevel * alpha);
                GUI.Box(new Rect(dr.x - 1f, dr.y - 2f, dr.width + 2f, 2f), GUIContent.none, _eqSeg);
            }

            GUI.color = new Color(1f, 1f, 1f, 0.18f * alpha);
            GUI.Box(new Rect(dr.x,      dr.y - 1f, dr.width, 1f), GUIContent.none, _eqSeg);
            GUI.color = new Color(1f, 1f, 1f, 0.07f * alpha);
            GUI.Box(new Rect(dr.x,      dr.yMax,   dr.width, 1f), GUIContent.none, _eqSeg);
            GUI.Box(new Rect(dr.x - 1f, dr.y,      1f, dr.height), GUIContent.none, _eqSeg);
            GUI.Box(new Rect(dr.xMax,   dr.y,      1f, dr.height), GUIContent.none, _eqSeg);
            GUI.color = ocB;
        }

        if (_showPlaylist || _listClosingAt >= 0f)
        {
            float listAlpha = 1f, listOff = 0f;
            bool listClosing = _listClosingAt >= 0f;
            bool listOpening = !listClosing && _listOpenedAt >= 0f;
            if (listClosing)
            {
                float t = Mathf.Clamp01((Time.unscaledTime - _listClosingAt) / 0.34f);
                listAlpha = 1f - PlayerSmoothSaturate(t);
                listOff   = t * t * t * t * 18f;
                if (t >= 0.999f)
                {
                    _showPlaylist = false;
                    _listClosingAt = -1f;
                }
            }
            else if (listOpening)
            {
                float t = Mathf.Clamp01((Time.unscaledTime - _listOpenedAt) / 0.36f);
                if (t < 0.995f)
                {
                    listAlpha = PlayerSmoothSaturate(t);
                    listOff   = (1f - PlayerEaseOutBack(t)) * -24f;
                }
                else _listOpenedAt = -1f;
            }
            bool listAnim = listClosing || listOpening;

            _listRect.width = 460f;
            Rect listDraw = new Rect(_listRect.x, _listRect.y + listOff, 460f, _listRect.height);
            Color prevC = GUI.color;
            GUI.color = new Color(prevC.r, prevC.g, prevC.b, prevC.a * listAlpha);
            Rect nextList = listDraw;
            try
            {
                if (_listFn == null) _listFn = (GUI.WindowFunction)DrawPlaylistWindow;
                nextList = GUILayout.Window(772101, listDraw, _listFn,
                    string.Empty, _winStyle);
            }
            catch { }
            finally { GUI.color = prevC; }
            if (!listAnim)
            {
                _listRect       = nextList;
                _listRect.width = 460f;
                _listRect.x = Mathf.Clamp(_listRect.x, 0f, Screen.width  - _listRect.width);
                _listRect.y = Mathf.Clamp(_listRect.y, 0f, Screen.height - 60f);
            }
        }

        if (_showEqPanel || _eqClosingAt >= 0f)
        {
            float eqAlpha = 1f, eqOff = 0f;
            bool eqClosing = _eqClosingAt >= 0f;
            bool eqOpening = !eqClosing && _eqOpenedAt >= 0f;
            if (eqClosing)
            {
                float t = Mathf.Clamp01((Time.unscaledTime - _eqClosingAt) / 0.34f);
                eqAlpha = 1f - PlayerSmoothSaturate(t);
                eqOff   = t * t * t * t * 18f;
                if (t >= 0.999f)
                {
                    _showEqPanel = false;
                    _eqClosingAt = -1f;
                }
            }
            else if (eqOpening)
            {
                float t = Mathf.Clamp01((Time.unscaledTime - _eqOpenedAt) / 0.36f);
                if (t < 0.995f)
                {
                    eqAlpha = PlayerSmoothSaturate(t);
                    eqOff   = (1f - PlayerEaseOutBack(t)) * -24f;
                }
                else _eqOpenedAt = -1f;
            }
            bool eqAnim = eqClosing || eqOpening;

            _eqPanelRect.width  = 230f;
            _eqPanelRect.height = 545f;
            Rect eqDraw = new Rect(_eqPanelRect.x, _eqPanelRect.y + eqOff, 230f, 545f);
            Color prevC = GUI.color;
            GUI.color = new Color(prevC.r, prevC.g, prevC.b, prevC.a * eqAlpha);
            Rect nextEq = eqDraw;
            try
            {
                if (_eqFn == null) _eqFn = (GUI.WindowFunction)DrawEqPanel;
                nextEq = GUI.Window(772102, eqDraw, _eqFn,
                    string.Empty, _winStyle);
            }
            catch { }
            finally { GUI.color = prevC; }
            if (!eqAnim)
            {
                _eqPanelRect        = nextEq;
                _eqPanelRect.width  = 230f;
                _eqPanelRect.height = 465f;
                _eqPanelRect.x = Mathf.Clamp(_eqPanelRect.x, 0f, Screen.width  - _eqPanelRect.width);
                _eqPanelRect.y = Mathf.Clamp(_eqPanelRect.y, 0f, Screen.height - 60f);
            }
        }
    }

    private void TogglePlaylist()
    {
        if (_showPlaylist && _listClosingAt < 0f)
        {
            _listClosingAt = Time.unscaledTime;
            _listOpenedAt  = -1f;
        }
        else if (!_showPlaylist)
        {
            _showPlaylist  = true;
            _listOpenedAt  = Time.unscaledTime;
            _listClosingAt = -1f;
            _listRect = new Rect(_winRect.x, _winRect.y + _winRect.height + 2f, 460f, _listRect.height);
        }
    }

    private void ToggleEqPanel()
    {
        if (_showEqPanel && _eqClosingAt < 0f)
        {
            _eqClosingAt = Time.unscaledTime;
            _eqOpenedAt  = -1f;
        }
        else if (!_showEqPanel)
        {
            _showEqPanel  = true;
            _eqOpenedAt   = Time.unscaledTime;
            _eqClosingAt  = -1f;
            _eqPanelRect = new Rect(_winRect.x + _winRect.width + 4f, _winRect.y, 230f, _eqPanelRect.height);
        }
    }

    private void DrawFillBarRect(Rect bar, float value, out float newValue, bool waveform = false)
    {
        float v = Mathf.Clamp01(value);
        if (waveform && _waveformHeights != null && _eqSeg != null && Event.current.type == EventType.Repaint)
        {

            GUI.Box(bar, GUIContent.none, _progBg);
            int   n        = _waveformHeights.Length;
            float slotW    = bar.width / n;
            float colW     = Mathf.Max(1f, slotW - 1f);
            float playheadX = bar.x + bar.width * v;
            var   ocW      = GUI.color;
            for (int i = 0; i < n; i++)
            {
                float frac = (i + 0.5f) / n;
                float h    = _waveformHeights[i] * bar.height;
                float cy   = bar.y + (bar.height - h) * 0.5f;
                float bx   = bar.x + i * slotW;

                float distSlots = Mathf.Abs(bx + slotW * 0.5f - playheadX) / Mathf.Max(1f, slotW);
                Color c;
                if (frac <= v)
                {
                    c = WithA(_clrScrubFill, _winAlpha);
                    if (distSlots < 1.5f) c = Color.Lerp(c, WithA(_clrScrubFillHi, _winAlpha), 1f - distSlots / 1.5f);
                }
                else
                {
                    c = WithA(_clrScrubDim, 0.72f * _winAlpha);
                    if (distSlots < 1.5f) c = Color.Lerp(c, WithA(_clrScrubDimHi, 0.85f * _winAlpha), 1f - distSlots / 1.5f);
                }
                GUI.color = c;
                GUI.Box(new Rect(bx, cy, colW, h), GUIContent.none, _eqSeg);
            }

            GUI.color = WithA(_clrPlayheadGlow, 0.32f * _winAlpha);
            GUI.Box(new Rect(playheadX - 1f, bar.y, 2f, bar.height), GUIContent.none, _eqSeg);
            GUI.color = new Color(1f, 1f, 1f, 0.85f * _winAlpha);
            GUI.Box(new Rect(playheadX - 0.5f, bar.y, 1f, bar.height), GUIContent.none, _eqSeg);
            GUI.color = ocW;
            var oc2 = GUI.color; GUI.color = Color.clear;
            newValue = GUI.HorizontalSlider(bar, v, 0f, 1f, _invisTrack, _invisThumb);
            GUI.color = oc2;
            return;
        }

        GUI.Box(bar, GUIContent.none, _progBg);
        if (_progRemain != null && v < 0.999f)
            GUI.Box(new Rect(bar.x + bar.width * v, bar.y, bar.width * (1f - v), bar.height),
                GUIContent.none, _progRemain);
        if (v > 0.001f)
        {
            if (_progGlow != null)
                GUI.Box(new Rect(bar.x - 1f, bar.y - 2f, bar.width * v + 6f, bar.height + 4f),
                    GUIContent.none, _progGlow);
            GUI.Box(new Rect(bar.x, bar.y, bar.width * v, bar.height), GUIContent.none, _progFill);
        }
        float tx = Mathf.Clamp(bar.x + bar.width * v - 3f, bar.x, bar.xMax - 6f);
        if (_eqSeg != null)
        {
            var ocTh = GUI.color;
            GUI.color = WithA(_clrThumbGlow, 0.28f * _winAlpha);
            GUI.Box(new Rect(tx - 2f, bar.y - 5f, 10f, bar.height + 10f), GUIContent.none, _eqSeg);
            GUI.color = ocTh;
        }
        GUI.Box(new Rect(tx, bar.y - 3f, 6f, bar.height + 6f), GUIContent.none, _progThumb);
        var oc = GUI.color; GUI.color = Color.clear;
        newValue = GUI.HorizontalSlider(bar, v, 0f, 1f, _invisTrack, _invisThumb);
        GUI.color = oc;
    }

    private bool HoverButton(Rect rect, string label, GUIStyle style)
    {
        int key = unchecked((int)(rect.x * 1000.123f) ^ ((int)(rect.y * 1009.456f) << 16));
        _hoverIntensity.TryGetValue(key, out float intensity);
        bool hover = rect.Contains(Event.current.mousePosition);
        float target = hover ? 1f : 0f;
        intensity = Mathf.MoveTowards(intensity, target, Time.unscaledDeltaTime * 8f);
        if (intensity < 0f) intensity = 0f;
        else if (intensity > 1f) intensity = 1f;
        _hoverIntensity[key] = intensity;

        Matrix4x4 prev = GUI.matrix;
        if (intensity > 0.01f)
        {
            float scale = 1f + 0.045f * intensity;
            Vector2 pivot = new Vector2(rect.x + rect.width * 0.5f, rect.y + rect.height * 0.5f);
            GUIUtility.ScaleAroundPivot(new Vector2(scale, scale), pivot);
        }
        bool clicked = GUI.Button(rect, label, style);
        GUI.matrix = prev;
        return clicked;
    }

    [HideFromIl2Cpp]
    private void DrawWindow(int _)
    {
        GUI.color = new Color(1f, 1f, 1f, _winAlpha);

        if (_artOverlayStyle != null)
        {
            float ambA = (0.05f + _beatLevel * 0.04f) * _winAlpha;
            var   ocAmb = GUI.color;
            GUI.color = new Color(_artColorCurrent.r, _artColorCurrent.g, _artColorCurrent.b, ambA);
            GUI.Box(new Rect(0f, 0f, 460f, _winRect.height), GUIContent.none, _artOverlayStyle);
            GUI.color = ocAmb;
        }
        string trackLabel = _currentIndex >= 0 && _currentIndex < _trackNames.Count
            ? _trackNames[_currentIndex]
            : NocturneText.T("нет трека", "no track");
        float total    = TotalTime;
        float current  = CurrentTime;
        float progress = total > 0f ? current / total : 0f;
        string stateLabel = _isLoading ? NocturneText.T("⟳ ...", "⟳ ...")
            : IsPlaying ? NocturneText.T("Играет", "Playing")
            : IsPaused  ? NocturneText.T("❙❙ Пауза", "❙❙ Paused")
            : NocturneText.T("■ Стоп", "■ Stopped");

        const float kWinW = 460f;

        Rect titleRow = GUILayoutUtility.GetRect(kWinW, kWinW, 26f, 26f);
        GUI.Box(titleRow, GUIContent.none, _headerStyle);

        if (_eqSeg != null)
        {
            var ocG = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.07f * _winAlpha);
            GUI.Box(new Rect(titleRow.x, titleRow.y, titleRow.width, 11f), GUIContent.none, _eqSeg);
            GUI.color = ocG;
        }
        GUI.Label(new Rect(titleRow.x + 4f, titleRow.y, 68f, 26f), " ♪ NOCTURNE", _brandLabelStyle);
        string badge  = TrackCount > 0 && _currentIndex >= 0 ? $"{_currentIndex + 1}/{TrackCount}" : string.Empty;
        float  badgeW = badge.Length > 0 ? 34f : 0f;
        if (badge.Length > 0)
            GUI.Label(new Rect(titleRow.x + 68f, titleRow.y, badgeW, 26f), badge, _trackBadgeStyle);
        float marqX = titleRow.x + 68f + badgeW + 2f;
        float marqW = titleRow.width - 68f - badgeW - 2f - 30f - 4f;
        DrawMarqueeLabel(trackLabel, marqX, titleRow.y, marqW, 26f);
        if (HoverButton(new Rect(titleRow.xMax - 30f, titleRow.y, 26f, 26f), "✕", _closeBtnStyle))
            BeginPlayerClose();

        Rect accentRow = GUILayoutUtility.GetRect(kWinW, kWinW, 2f, 2f);
        float pulse = 0.55f + 0.45f * Mathf.Sin(Time.realtimeSinceStartup * 2.8f);
        var prevCol = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, pulse * _winAlpha);
        GUI.Box(accentRow, GUIContent.none, _accentLineStyle);
        GUI.color = prevCol;

        if (_waveformHeights == null) ComputeWaveform(-1);
        Rect seekRow = GUILayoutUtility.GetRect(kWinW, kWinW, 34f, 34f);
        string remainStr = total > 0f ? "-" + FormatTime(total - current) : "--:--";
        GUI.Label(new Rect(seekRow.x + 2f,       seekRow.y + 8f, 30f, 18f), FormatTime(current), _seekTimeStyle);
        GUI.Label(new Rect(seekRow.xMax - 32f,   seekRow.y + 8f, 30f, 18f), remainStr,           _seekRemainStyle);
        Rect seekBar = new Rect(seekRow.x + 32f, seekRow.y + 4f, seekRow.width - 64f, 26f);
        DrawFillBarRect(seekBar, progress, out float newProgress, waveform: true);
        if (_source != null && _source.clip != null && total > 0f && Mathf.Abs(newProgress - progress) > 0.002f)
            _source.time = newProgress * total;

        if (IsPlaying && progress > 0.04f && _eqSeg != null && Event.current.type == EventType.Repaint)
        {
            float fillEnd = seekBar.x + seekBar.width * progress;
            float shimW   = 18f;
            float phase   = (Time.realtimeSinceStartup % 2.5f) / 2.5f;
            float shimX   = seekBar.x + (seekBar.width * progress - shimW) * phase;
            if (shimX >= seekBar.x && shimX + shimW <= fillEnd)
            {
                var ocSh = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, 0.22f * _winAlpha);
                GUI.Box(new Rect(shimX, seekBar.y, shimW, seekBar.height), GUIContent.none, _eqSeg);
                GUI.color = ocSh;
            }
        }

        if (_isLoading && _eqSeg != null && Event.current.type == EventType.Repaint)
        {
            float bandW   = 70f;
            float cycle   = 1.3f;
            float phase   = (Time.realtimeSinceStartup % cycle) / cycle;
            float bandX   = seekBar.x - bandW + (seekBar.width + bandW * 2f) * phase;

            var   ocLd = GUI.color;
            for (int i = 0; i < 3; i++)
            {
                float fade = 1f - i * 0.30f;
                float w    = bandW + i * 14f;
                float x    = bandX - i * 7f;
                if (x + w > seekBar.x && x < seekBar.xMax)
                {
                    float cx0 = Mathf.Max(x, seekBar.x);
                    float cx1 = Mathf.Min(x + w, seekBar.xMax);
                    if (cx1 > cx0)
                    {
                        GUI.color = new Color(1f, 0.85f, 0.78f, 0.085f * fade * _winAlpha);
                        GUI.Box(new Rect(cx0, seekBar.y, cx1 - cx0, seekBar.height), GUIContent.none, _eqSeg);
                    }
                }
            }
            GUI.color = ocLd;
        }

        if (_eqSeg != null) { var ocZ = GUI.color; GUI.color = new Color(1f,1f,1f,0.07f * _winAlpha); GUI.Box(new Rect(seekRow.x, seekRow.yMax, seekRow.width, 1f), GUIContent.none, _eqSeg); GUI.color = ocZ; }

        UpdateArtTex();
        Rect dispRow = GUILayoutUtility.GetRect(kWinW, kWinW, 80f, 80f);
        GUI.Box(dispRow, GUIContent.none, _displayBgStyle);

        if (_eqSeg != null && Event.current.type == EventType.Repaint)
        {
            var ocGr = GUI.color;
            float gh = dispRow.height * 0.28f;
            GUI.color = new Color(1f, 1f, 1f, 0.055f * _winAlpha);
            GUI.Box(new Rect(dispRow.x, dispRow.y, dispRow.width, gh), GUIContent.none, _eqSeg);
            GUI.color = new Color(0f, 0f, 0f, 0.07f * _winAlpha);
            GUI.Box(new Rect(dispRow.x, dispRow.yMax - gh, dispRow.width, gh), GUIContent.none, _eqSeg);
            GUI.color = ocGr;
        }

        if (_eqSeg != null && Event.current.type == EventType.Repaint)
        {
            var ocD = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.045f * _winAlpha);
            for (float dy = dispRow.y + 6f; dy < dispRow.yMax - 2f; dy += 8f)
                for (float dx = dispRow.x + 5f; dx < dispRow.xMax - 2f; dx += 9f)
                    GUI.Box(new Rect(dx, dy, 1f, 1f), GUIContent.none, _eqSeg);
            GUI.color = ocD;
        }

        if (_artBgStyle != null)
        {
            float ax = dispRow.x + 6f, ay = dispRow.y + 14f;
            float aw = 52f;

            float slideT = -1f;
            if (_artSlideStart >= 0f)
            {
                slideT = (Time.unscaledTime - _artSlideStart) / 0.55f;
                if (slideT >= 1f) { slideT = -1f; _artSlideStart = -1f; }
            }

            if (slideT >= 0f)
            {

                float tIn  = slideT;
                float tOut = slideT * slideT * (3f - 2f * slideT);
                float oldOffset = -tOut * 90f * _artSlideDir;
                float oldAlpha  = 1f - tIn;
                DrawArtDisc(ax + oldOffset, ay, aw, oldAlpha, _artPrevColor,
                            _artPrevLetter, _artRotation, drawHalo: false);

                float ease = PlayerEaseOutBack(slideT);
                float newOffset = (1f - ease) * 90f * _artSlideDir;
                float newAlpha  = Mathf.Min(1f, slideT * 1.6f);
                DrawArtDisc(ax + newOffset, ay, aw, newAlpha, _artColorCurrent,
                            GetArtLetter(), _artRotation, drawHalo: true);
            }
            else
            {
                DrawArtDisc(ax, ay, aw, 1f, _artColorCurrent, GetArtLetter(),
                            _artRotation, drawHalo: true);
            }

            if (_eqSeg != null)
            {
                var ocMs = GUI.color;
                float msY = ay + aw + 9f;
                GUI.color = WithA(_clrArtStripBg, _winAlpha);
                GUI.Box(new Rect(ax, msY, aw, 3f), GUIContent.none, _eqSeg);
                if (progress > 0.001f)
                {
                    GUI.color = WithA(_clrArtStripFill, _winAlpha);
                    GUI.Box(new Rect(ax, msY, aw * progress, 3f), GUIContent.none, _eqSeg);
                }
                GUI.color = ocMs;
            }
        }
        GUI.Label(new Rect(dispRow.x + 72f, dispRow.y + 10f, 54f, 52f), FormatTime(current), _bigTimeStyle);
        Rect eqArea = new Rect(dispRow.x + 130f, dispRow.y + 4f, dispRow.width - 130f - 58f, 56f);
        DrawEqBars(eqArea);
        float rightX = dispRow.xMax - 56f;
        GUI.Label(new Rect(rightX, dispRow.y + 26f, 54f, 28f), total > 0f ? FormatTime(total) : "--:--", _totalTimeStyle);

        if (_eqSeg != null) { var ocZ2 = GUI.color; GUI.color = new Color(1f,1f,1f,0.07f * _winAlpha); GUI.Box(new Rect(dispRow.x, dispRow.yMax, dispRow.width, 1f), GUIContent.none, _eqSeg); GUI.color = ocZ2; }

        Rect statusRow = GUILayoutUtility.GetRect(kWinW, kWinW, 18f, 18f);
        GUI.Box(statusRow, GUIContent.none, _listHeaderStyle);

        float statusTxtX = statusRow.x + 8f;
        if (IsPlaying && _eqSeg != null)
        {
            float t0   = Time.realtimeSinceStartup;
            float bx   = statusRow.x + 8f;
            float maxH = 12f;
            var ocNP   = GUI.color;
            for (int nb = 0; nb < 3; nb++)
            {
                float bh = 2f + (maxH - 2f) * (0.5f + 0.5f * Mathf.Sin(t0 * (3.5f + nb * 0.8f) + nb * 1.1f));
                GUI.color = new Color(1.00f, 0.52f, 0.62f, 0.90f * _winAlpha);
                GUI.Box(new Rect(bx + nb * 5f, statusRow.y + (maxH - bh) * 0.5f + 3f, 3f, bh),
                    GUIContent.none, _eqSeg);
            }
            GUI.color = ocNP;
            statusTxtX = statusRow.x + 25f;
        }
        GUI.Label(new Rect(statusTxtX, statusRow.y, statusRow.width - 50f, 18f), stateLabel, _stateStyle);

        if (IsPlaying && _eqSeg != null)
        {
            float vuL = 0f, vuM = 0f, vuH = 0f;
            for (int i = 0; i < 5;  i++) vuL += _eqHeights[i];
            for (int i = 5; i < 9;  i++) vuM += _eqHeights[i];
            for (int i = 9; i < 14; i++) vuH += _eqHeights[i];
            vuL /= 5f; vuM /= 4f; vuH /= 5f;
            float vuX   = statusRow.xMax - 36f;
            float vuMaxH = 13f;
            var   oc4   = GUI.color;
            float[] vus = { vuL, vuM, vuH };
            for (int v = 0; v < 3; v++)
            {
                float h = Mathf.Max(2f, vus[v] * vuMaxH);
                GUI.color = Color.Lerp(
                    new Color(0.38f, 0.04f, 0.07f, _winAlpha),
                    new Color(1.00f, 0.52f, 0.62f, _winAlpha),
                    vus[v]);
                GUI.Box(new Rect(vuX + v * 9f, statusRow.y + vuMaxH - h + 2f, 7f, h),
                    GUIContent.none, _eqSeg);
            }
            GUI.color = oc4;
        }

        if (_eqSeg != null) { var ocZ3 = GUI.color; GUI.color = new Color(1f,1f,1f,0.07f * _winAlpha); GUI.Box(new Rect(statusRow.x, statusRow.yMax, statusRow.width, 1f), GUIContent.none, _eqSeg); GUI.color = ocZ3; }

        Rect ctrlRow = GUILayoutUtility.GetRect(kWinW, kWinW, 38f, 38f);
        GUI.Box(ctrlRow, GUIContent.none, _controlsBarStyle);
        float cx = ctrlRow.x + 6f;
        float cy = ctrlRow.y + 6f;
        if (HoverButton(new Rect(cx,        cy, 24f, 26f), "|<",  _btnStyle)) Prev();
        if (HoverButton(new Rect(cx + 26f,  cy, 28f, 26f), "-10", _btnStyle))
            { if (_source != null) _source.time = Mathf.Max(0f, _source.time - 10f); }
        string playLabel = IsPlaying
            ? NocturneText.T("❚❚ ПАУЗА", "❚❚ PAUSE")
            : NocturneText.T("▶ ИГРАТЬ", "▶ PLAY");
        float playPulse = IsPlaying ? 0.78f + 0.22f * Mathf.Sin(Time.realtimeSinceStartup * 2.4f) : 1f;
        var ocP = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, playPulse * _winAlpha);
        if (HoverButton(new Rect(cx + 56f,  cy, 76f, 26f), playLabel, _btnPlay)) PlayPause();
        GUI.color = ocP;
        if (HoverButton(new Rect(cx + 134f, cy, 28f, 26f), "+10", _btnStyle))
            { if (_source != null) _source.time = Mathf.Min(Mathf.Max(0f, TotalTime - 0.5f), _source.time + 10f); }
        if (HoverButton(new Rect(cx + 164f, cy, 24f, 26f), ">|",  _btnStyle)) Next();
        if (HoverButton(new Rect(cx + 190f, cy, 22f, 26f), "■",   _btnStop))  Stop();
        Rect volBar = new Rect(cx + 214f, cy + 8f, 54f, 10f);
        DrawFillBarRect(volBar, _volume, out float newVol);
        if (Mathf.Abs(newVol - _volume) > 0.001f) Volume = newVol;

        Rect volWheelZone = new Rect(cx + 210f, ctrlRow.y, 90f, ctrlRow.height);
        Event evW = Event.current;
        if (evW.type == EventType.ScrollWheel && volWheelZone.Contains(evW.mousePosition))
        {
            Volume = Mathf.Clamp01(_volume - evW.delta.y * 0.05f);
            evW.Use();
        }
        GUI.Label(new Rect(cx + 270f, cy + 4f, 28f, 18f), $"{Mathf.RoundToInt(_volume * 100f)}%", _volPctStyle);

        float rx = ctrlRow.xMax - 6f;
        if (HoverButton(new Rect(rx - 28f,  cy + 1f, 26f, 24f), "≡",
                _showPlaylist ? _btnToggleOn : _btnToggleOff))
        {
            TogglePlaylist();
        }
        if (HoverButton(new Rect(rx - 66f,  cy + 1f, 34f, 24f), "MIX",
                _shuffle ? _btnToggleOn : _btnToggleOff)) Shuffle = !_shuffle;
        if (HoverButton(new Rect(rx - 104f, cy + 1f, 34f, 24f), "RPT",
                _loop    ? _btnToggleOn : _btnToggleOff)) Loop    = !_loop;
        if (HoverButton(new Rect(rx - 142f, cy + 1f, 34f, 24f), "EQ",
                _activePresetIdx != 0 ? _btnToggleOn : _btnToggleOff))
        {
            ToggleEqPanel();
        }

        GUI.DragWindow(new Rect(0f, 0f, kWinW, 26f));
    }

    [HideFromIl2Cpp]
    private void DrawPlaylistWindow(int _)
    {

        const float kPW = 460f;

        Rect hdr = GUILayoutUtility.GetRect(kPW, kPW, 26f, 26f);
        GUI.Box(hdr, GUIContent.none, _listHeaderStyle);
        GUI.Label(new Rect(hdr.x + 4f, hdr.y, hdr.width - 80f, 26f),
            NocturneText.T($"≡  ПЛЕЙЛИСТ  ({TrackCount})", $"≡  PLAYLIST  ({TrackCount})"),
            _sectionStyle);
        if (GUI.Button(new Rect(hdr.xMax - 74f, hdr.y + 3f, 68f, 20f),
            NocturneText.T("ОБНОВИТЬ", "REFRESH"), _btnSmall))
            ScanTracks();

        Rect div = GUILayoutUtility.GetRect(kPW, kPW, 1f, 1f);
        if (_dividerStyle != null) GUI.Box(div, GUIContent.none, _dividerStyle);

        if (TrackCount == 0)
        {
            GUILayoutUtility.GetRect(kPW, kPW, 8f, 8f);
            Rect emptyR = GUILayoutUtility.GetRect(kPW, kPW, 44f, 44f);
            if (_emptyStyle != null)
                GUI.Label(emptyR,
                    NocturneText.T("Нет треков. Положи .wav/.ogg/.mp3 в:\nBepInEx/plugins/Nocturne/Music/",
                                "No tracks. Put .wav/.ogg/.mp3 into:\nBepInEx/plugins/Nocturne/Music/"),
                    _emptyStyle);
            GUILayoutUtility.GetRect(kPW, kPW, 8f, 8f);
        }
        else
        {
            float listH = Mathf.Min(TrackCount * 30f + 4f, 220f);
            Rect scrollArea = GUILayoutUtility.GetRect(kPW, kPW, listH, listH);
            float contentH   = TrackCount * 30f + 4f;
            float contentW   = scrollArea.width - (contentH > listH ? 16f : 0f);
            Rect contentRect = new Rect(0f, 0f, contentW, contentH);
            _trackScroll = GUI.BeginScrollView(scrollArea, _trackScroll, contentRect);
            for (int i = 0; i < _trackNames.Count; i++)
            {
                bool   active  = i == _currentIndex;
                string durStr  = _trackDurations.Count > i && _trackDurations[i] > 0f
                    ? $"  [{FormatTime(_trackDurations[i])}]" : string.Empty;
                string row     = $"{(active ? ">  " : "   ")}{i + 1:D2}  {_trackNames[i]}{durStr}";
                if (GUI.Button(new Rect(0f, i * 30f + 2f, contentW, 28f), row,
                    active ? _trackActive : _trackNormal))
                    Play(i);
            }
            GUI.EndScrollView();
        }

        GUILayoutUtility.GetRect(kPW, kPW, 4f, 4f);
        GUI.DragWindow(new Rect(0f, 0f, 9999f, 9999f));
    }

    [HideFromIl2Cpp]
    private void DrawEqPanel(int _)
    {
        const float kPW  = 230f;
        const float kBH  = 24f;
        const float kGap = 2f;

        Rect title = new Rect(0f, 0f, kPW, 26f);
        GUI.Box(title, GUIContent.none, _headerStyle);
        GUI.Label(new Rect(6f, 2f, 160f, 22f), "♩ EQUALIZER", _brandLabelStyle);
        if (GUI.Button(new Rect(kPW - 28f, 1f, 24f, 22f), "✕", _closeBtnStyle))
            ToggleEqPanel();

        float pulse = 0.55f + 0.45f * Mathf.Sin(Time.realtimeSinceStartup * 2.8f);
        var prevCol = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, pulse);
        GUI.Box(new Rect(0f, 26f, kPW, 2f), GUIContent.none, _accentLineStyle);
        GUI.color = prevCol;

        float y = 28f;

        if (_eqSeg != null && _eqGains != null)
        {
            const float kCH = 42f;
            GUI.Box(new Rect(2f, y, kPW - 4f, kCH), GUIContent.none, _displayBgStyle);
            float midY  = y + kCH * 0.5f;
            float maxBH = kCH * 0.40f;
            float bW    = (kPW - 10f) / 10f;
            var   oc5   = GUI.color;
            float eqHue = AccentHue();
            GUI.color = Rehue(new Color(0.45f, 0.10f, 0.15f, 0.40f), eqHue);
            GUI.Box(new Rect(4f, midY, kPW - 8f, 1f), GUIContent.none, _eqSeg);
            for (int b = 0; b < 10 && b < _eqGains.Length; b++)
            {
                float gain = _eqGains[b];
                float t    = Mathf.Abs(gain) / 12f;
                float barH = t * maxBH;
                float barX = 4f + b * bW + 1f;
                float barY = gain >= 0f ? midY - barH : midY + 1f;
                GUI.color  = gain >= 0f
                    ? Rehue(new Color(0.85f + t * 0.12f, 0.18f * t, 0.24f * t, 0.90f), eqHue)
                    : Rehue(new Color(0.35f, 0.04f, 0.06f, 0.75f), eqHue);
                GUI.Box(new Rect(barX, barY, bW - 2f, Mathf.Max(1f, barH)), GUIContent.none, _eqSeg);
            }
            GUI.color = oc5;
            y += kCH + 4f;
        }

        float colW = (kPW - 6f) * 0.5f;
        for (int i = 0; i < kPresetNames.Length; i++)
        {
            bool active = _activePresetIdx == i;
            GUIStyle st = active ? _btnToggleOn : _btnStyle;
            if (i == 0)
            {

                if (GUI.Button(new Rect(2f, y, kPW - 4f, kBH), kPresetNames[i], st))
                    SelectPreset(i);
                y += kBH + kGap;
            }
            else if (i % 2 == 1)
            {

                if (GUI.Button(new Rect(2f, y, colW, kBH), kPresetNames[i], st))
                    SelectPreset(i);

                int j = i + 1;
                if (j < kPresetNames.Length)
                {
                    bool activeR = _activePresetIdx == j;
                    if (GUI.Button(new Rect(4f + colW, y, colW, kBH), kPresetNames[j], activeR ? _btnToggleOn : _btnStyle))
                        SelectPreset(j);
                }
                y += kBH + kGap;
            }
        }

        y += 6f;
        var tpc = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, 0.5f);
        GUI.Box(new Rect(0f, y, kPW, 2f), GUIContent.none, _accentLineStyle);
        GUI.color = tpc;
        y += 6f;
        GUI.Label(new Rect(6f, y, 160f, 18f), "♪ " + NocturneText.T("ТЕМА", "THEME"), _brandLabelStyle);
        y += 20f;

        string curAcc = NocturneConfig.MusicAccent.Value;
        const int perRow = 9;
        float sw = (kPW - 6f - (perRow - 1) * 2f) / perRow;
        for (int i = 0; i < AccentThemes.Length; i++)
        {
            int cx = i % perRow, cy = i / perRow;
            Rect r = new Rect(3f + cx * (sw + 2f), y + cy * (sw + 2f), sw, sw);
            bool sel = string.Equals(AccentThemes[i].En, curAcc, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(AccentThemes[i].Ru, curAcc, StringComparison.Ordinal);
            var oc = GUI.color;
            if (sel)
            {
                GUI.color = Color.white;
                GUI.Box(new Rect(r.x - 2f, r.y - 2f, r.width + 4f, r.height + 4f), GUIContent.none, _eqSeg);
            }
            GUI.color = Color.HSVToRGB(AccentThemes[i].Hue, 0.80f, 0.95f);
            GUI.Box(r, GUIContent.none, _eqSeg);
            GUI.color = oc;
            if (GUI.Button(r, GUIContent.none, GUIStyle.none))
            {
                NocturneConfig.MusicAccent.Value = AccentThemes[i].En;
                _stylesDirty = true;
            }
        }

        GUI.DragWindow(new Rect(0f, 0f, kPW, 26f));
    }

    private void DrawEqBars(Rect area)
    {
        if (Event.current.type != EventType.Repaint || _eqSeg == null) return;
        const int   kBars    = 14;
        const float kBarW    = 11f;
        const float kGap     = 3f;
        const int   kSegs    = 7;
        const float kMaxH    = 50f;
        const float kPeakH   = 3f;
        const float kSegGap  = 1.5f;
        float segSlotH = kMaxH / kSegs;
        float segH     = segSlotH - kSegGap;
        float totalW   = kBars * (kBarW + kGap) - kGap;
        float startX   = area.x + (area.width - totalW) * 0.5f;
        float baseY    = area.y + area.height - 2f;

        var cBot  = WithA(_clrEqBot, _winAlpha);
        var cTop  = WithA(_clrEqTop, _winAlpha);
        var saved = GUI.color;

        GUI.color = WithA(_clrEqBase, 0.22f * _winAlpha);
        GUI.Box(new Rect(startX, baseY, totalW, 1f), GUIContent.none, _eqSeg);

        float[] trailAlphas = { 0.13f, 0.07f, 0.035f };
        for (int t = 2; t >= 0; t--)
        {
            float ta = trailAlphas[t];
            for (int i = 0; i < kBars; i++)
            {
                float barX    = startX + i * (kBarW + kGap);
                int   litSegs = Mathf.RoundToInt(_eqTrail[t][i] * kSegs);
                for (int s = 0; s < litSegs && s < kSegs; s++)
                {
                    float frac = kSegs <= 1 ? 1f : (float)s / (kSegs - 1);
                    var   col  = Color.Lerp(cBot, cTop, frac);
                    float segY = baseY - (s + 1) * segSlotH + kSegGap * 0.5f;
                    GUI.color  = new Color(col.r, col.g, col.b, ta * _winAlpha);
                    GUI.Box(new Rect(barX, segY, kBarW, segH), GUIContent.none, _eqSeg);
                }
            }
        }
        GUI.color = saved;

        float sweepT = _sweepActive ? Mathf.Clamp01(_sweepTimer / 0.4f) : 1f;
        for (int i = 0; i < kBars; i++)
        {
            float barX    = startX + i * (kBarW + kGap);
            float reveal  = Mathf.Clamp01((sweepT - (float)i / kBars) * kBars);
            int   litSegs = Mathf.RoundToInt(_eqHeights[i] * kSegs * reveal);
            if (litSegs < 1 && reveal > 0.02f) litSegs = 1;

            for (int s = 0; s < litSegs && s < kSegs; s++)
            {
                float t    = kSegs <= 1 ? 1f : (float)s / (kSegs - 1);
                float segY = baseY - (s + 1) * segSlotH + kSegGap * 0.5f;
                GUI.color  = Color.Lerp(cBot, cTop, t);
                GUI.Box(new Rect(barX, segY, kBarW, segH), GUIContent.none, _eqSeg);
            }
            GUI.color = saved;

            if (_eqPeakBar != null && _eqPeaks[i] > 0.06f && reveal >= 1f)
            {
                float ph = Mathf.Max(3f, _eqPeaks[i] * kMaxH);
                GUI.Box(new Rect(barX, baseY - ph - kPeakH - 1f, kBarW, kPeakH),
                    GUIContent.none, _eqPeakBar);
            }

            int reflSegs = Mathf.Min(litSegs, 3);
            for (int s = 0; s < reflSegs; s++)
            {
                float alpha = (1f - (float)s / 3f) * 0.20f * _winAlpha;
                float t     = kSegs <= 1 ? 1f : (float)(litSegs - 1 - s) / (kSegs - 1);
                var   col   = Color.Lerp(cBot, cTop, t);
                float segY  = baseY + 2f + s * segSlotH;
                GUI.color   = new Color(col.r, col.g, col.b, alpha);
                GUI.Box(new Rect(barX, segY, kBarW, segH), GUIContent.none, _eqSeg);
            }
            GUI.color = saved;
        }
        GUI.color = saved;
    }

    private void DrawMarqueeLabel(string text, float x, float y, float w, float h)
    {
        if (_trackLabelStyle == null || w < 4f) return;
        Rect clip = new Rect(x, y, w, h);

        if (_marqueeContent == null) _marqueeContent = new GUIContent(text);
        else if (_marqueeContent.text != text) _marqueeContent.text = text;
        Vector2 sz = _trackLabelStyle.CalcSize(_marqueeContent);
        if (sz.x <= w || !IsPlaying)
        {
            _marqueeStart = Time.realtimeSinceStartup;
            GUI.Label(clip, text, _trackLabelStyle);
            return;
        }
        const float kSpeed = 38f;
        const float kPause = 1.8f;
        float overflow = sz.x - w;
        float cycleLen = overflow / kSpeed + kPause * 2f;
        float t       = (Time.realtimeSinceStartup - _marqueeStart) % cycleLen;
        float scrollX = 0f;
        if (t > kPause && t < kPause + overflow / kSpeed)
            scrollX = (t - kPause) * kSpeed;
        else if (t >= kPause + overflow / kSpeed)
            scrollX = overflow;
        GUI.BeginGroup(clip);
        GUI.Label(new Rect(-scrollX, 0f, sz.x + 4f, h + 2f), text, _trackLabelStyle);
        GUI.EndGroup();
    }

    private void UpdateArtTex()
    {
        int idx = _currentIndex;
        if (idx == _artIndex) return;

        if (_artIndex >= 0 && idx >= 0 && _trackNames.Count > 0)
        {
            _artPrevColor  = _artColorCurrent;
            _artPrevLetter = GetLetterForIdx(_artIndex);

            bool forward = idx > _artIndex
                           || (_artIndex == _trackNames.Count - 1 && idx == 0);
            _artSlideDir = forward ? -1 : 1;
            _artSlideStart = Time.unscaledTime;
        }
        _artIndex = idx;
        Color c = idx >= 0 && idx < _trackNames.Count
            ? kArtPalette[Math.Abs(_trackNames[idx].GetHashCode()) % kArtPalette.Length]
            : new Color(0.15f, 0.08f, 0.10f, 1f);
        _artColor = c;
        ComputeWaveform(idx);
        if (_artTex == null)
        {
            _artTex = MakeTex(c);
        }
        else
        {
            _artTex.SetPixel(0, 0, c);
            _artTex.Apply();
        }
        if (_artStyle != null) _artStyle.normal.background = _artTex;
    }

    private string GetLetterForIdx(int idx)
    {
        if (idx < 0 || idx >= _trackNames.Count) return "♪";
        string name = _trackNames[idx];
        int i = 0;
        while (i < name.Length && (char.IsDigit(name[i]) || name[i] == ' ')) i++;
        return i < name.Length ? name[i].ToString().ToUpper() : "♪";
    }

    private void DrawArtDisc(float ax, float ay, float aw, float alphaMul,
                              Color discColor, string letter, float rotationDeg, bool drawHalo)
    {
        if (_artBgStyle == null) return;
        float a = _winAlpha * alphaMul;
        float cxC = ax + aw * 0.5f;
        float cyC = ay + aw * 0.5f;
        var ocArt = GUI.color;

        if (drawHalo)
        {
            float haloA = (0.14f + _beatLevel * 0.36f) * a;
            float haloS = 1.10f + _beatLevel * 0.14f;
            float haloW = aw * haloS;
            float haloO = (haloW - aw) * 0.5f;
            GUI.color = new Color(discColor.r, discColor.g, discColor.b, haloA);
            GUI.Box(new Rect(ax - haloO, ay - haloO, haloW, haloW), GUIContent.none, _artBgStyle);
        }

        Matrix4x4 mPrev = GUI.matrix;
        GUIUtility.RotateAroundPivot(rotationDeg, new Vector2(cxC, cyC));

        GUI.color = new Color(discColor.r, discColor.g, discColor.b, a);
        GUI.Box(new Rect(ax, ay, aw, aw), GUIContent.none, _artBgStyle);

        Color labelC = Color.Lerp(discColor, Color.black, 0.58f);
        float labelW = 26f;
        float labelO = (aw - labelW) * 0.5f;
        GUI.color = new Color(labelC.r, labelC.g, labelC.b, a);
        GUI.Box(new Rect(ax + labelO, ay + labelO, labelW, labelW), GUIContent.none, _artBgStyle);

        if (_artLetterStyle != null)
        {
            var savedTc = _artLetterStyle.normal.textColor;
            var newTc = savedTc; newTc.a = savedTc.a * alphaMul;
            _artLetterStyle.normal.textColor = newTc;
            GUI.Label(new Rect(ax, ay, aw, aw), letter, _artLetterStyle);
            _artLetterStyle.normal.textColor = savedTc;
        }

        GUI.matrix = mPrev;

        if (drawHalo && _eqHeights != null && _artBgStyle != null)
        {
            const int nBars     = 28;
            float innerR        = aw * 0.54f;
            float baseBarH      = 2f;
            float maxBarH       = 14f;
            float spectrumA     = 0.80f * a;
            for (int i = 0; i < nBars; i++)
            {
                float angleDeg = (i / (float)nBars) * 360f;
                int specIdx = (i * _eqHeights.Length) / nBars;
                if (specIdx >= _eqHeights.Length) specIdx = _eqHeights.Length - 1;
                float spec = _eqHeights[specIdx];
                if (spec < 0f) spec = 0f;
                float barH = baseBarH + spec * (maxBarH - baseBarH);
                Matrix4x4 rPrev = GUI.matrix;
                GUIUtility.RotateAroundPivot(angleDeg, new Vector2(cxC, cyC));

                Rect barRect = new Rect(cxC - 1f, cyC - innerR - barH, 2f, barH);
                Color barCol = Color.Lerp(discColor, Color.white, 0.55f + spec * 0.35f);
                GUI.color = new Color(barCol.r, barCol.g, barCol.b, spectrumA);
                GUI.Box(barRect, GUIContent.none, _artBgStyle);
                GUI.matrix = rPrev;
            }
        }

        GUI.color = new Color(0.96f, 0.92f, 0.86f, 0.90f * a);
        GUI.Box(new Rect(cxC - 2.5f, cyC - 2.5f, 5f, 5f), GUIContent.none, _artBgStyle);

        GUI.color = ocArt;
    }

    private void ComputeWaveform(int trackIdx, int columns = 90)
    {
        if (trackIdx == _waveformSeed) return;
        _waveformSeed   = trackIdx;
        _waveformHeights = new float[columns];
        uint rng = trackIdx >= 0 && trackIdx < _trackNames.Count
            ? (uint)Math.Abs(_trackNames[trackIdx].GetHashCode()) | 1u
            : 12345u;
        float[] raw = new float[columns];
        for (int i = 0; i < columns; i++)
        {
            rng = rng * 1664525u + 1013904223u;
            raw[i] = 0.25f + 0.75f * ((rng >> 16 & 0xFFFFu) / 65535f);
        }

        for (int pass = 0; pass < 2; pass++)
            for (int i = 1; i < columns - 1; i++)
                raw[i] = (raw[i - 1] + raw[i] * 2f + raw[i + 1]) / 4f;
        for (int i = 0; i < columns; i++) _waveformHeights[i] = raw[i];
    }

    private string GetArtLetter() => GetLetterForIdx(_currentIndex);

    private GUIStyle _headerStyle;
    private GUIStyle _accentLineStyle;
    private GUIStyle _panelStyle;
    private GUIStyle _listHeaderStyle;
    private GUIStyle _trackLabelStyle;
    private GUIStyle _stateStyle;
    private GUIStyle _timeRightStyle;
    private GUIStyle _sectionStyle;
    private GUIStyle _volLabelStyle;
    private GUIStyle _volPctStyle;
    private GUIStyle _emptyStyle;
    private GUIStyle _dividerStyle;
    private GUIStyle _progBg;
    private GUIStyle _progFill;
    private GUIStyle _progThumb;
    private GUIStyle _progGlow;
    private GUIStyle _invisTrack;
    private GUIStyle _invisThumb;
    private GUIStyle _btnPlay;
    private GUIStyle _btnStop;
    private GUIStyle _btnToggleOn;
    private GUIStyle _btnToggleOff;
    private GUIStyle _btnSmall;
    private GUIStyle _closeBtnStyle;
    private GUIStyle _trackNormal;
    private GUIStyle _trackActive;

    private string _appliedAccent;

    private void EnsureStyles()
    {

        string acc = NocturneConfig.MusicAccent.Value;
        if (acc != _appliedAccent) { _appliedAccent = acc; _stylesDirty = true; }
        if (!_stylesDirty && _bgTex != null) return;
        _stylesDirty = false;
        try { BuildStyles(); } catch { _stylesDirty = true; }
    }

    private void BuildStyles()
    {

        var cBg         = new Color(0.052f, 0.038f, 0.052f, 0.82f);
        var cHeader     = new Color(0.110f, 0.032f, 0.048f, 1f);
        var cPanel      = new Color(0.072f, 0.044f, 0.056f, 1f);
        var cListHdr    = new Color(0.088f, 0.048f, 0.060f, 1f);
        var cAccent     = new Color(0.800f, 0.095f, 0.155f, 1f);
        var cAccentHov  = new Color(0.920f, 0.140f, 0.200f, 1f);
        var cAccentDim  = new Color(0.480f, 0.055f, 0.090f, 1f);
        var cBtn        = new Color(0.155f, 0.072f, 0.092f, 1f);
        var cBtnHov     = new Color(0.220f, 0.105f, 0.130f, 1f);
        var cBtnStop    = new Color(0.130f, 0.060f, 0.075f, 1f);
        var cTrackRow   = new Color(0.082f, 0.048f, 0.058f, 1f);
        var cTrackHov   = new Color(0.210f, 0.105f, 0.132f, 1f);
        var cTrackAct   = new Color(0.520f, 0.048f, 0.085f, 1f);
        var cProgBg     = new Color(0.130f, 0.065f, 0.080f, 1f);
        var cThumb      = new Color(1.000f, 0.320f, 0.380f, 1f);

        float hue = AccentHue();
        cBg = Rehue(cBg, hue);             cHeader = Rehue(cHeader, hue);       cPanel = Rehue(cPanel, hue);
        cListHdr = Rehue(cListHdr, hue);   cAccent = Rehue(cAccent, hue);       cAccentHov = Rehue(cAccentHov, hue);
        cAccentDim = Rehue(cAccentDim, hue); cBtn = Rehue(cBtn, hue);           cBtnHov = Rehue(cBtnHov, hue);
        cBtnStop = Rehue(cBtnStop, hue);   cTrackRow = Rehue(cTrackRow, hue);   cTrackHov = Rehue(cTrackHov, hue);
        cTrackAct = Rehue(cTrackAct, hue); cProgBg = Rehue(cProgBg, hue);       cThumb = Rehue(cThumb, hue);

        _clrToastAccent  = Rehue(new Color(0.85f, 0.10f, 0.15f, 1f), hue);
        _clrScrubFill    = Rehue(new Color(0.94f, 0.20f, 0.24f, 1f), hue);
        _clrScrubFillHi  = Rehue(new Color(1.00f, 0.55f, 0.55f, 1f), hue);
        _clrScrubDim     = Rehue(new Color(0.32f, 0.10f, 0.13f, 1f), hue);
        _clrScrubDimHi   = Rehue(new Color(0.85f, 0.30f, 0.30f, 1f), hue);
        _clrThumbGlow    = Rehue(new Color(0.80f, 0.10f, 0.15f, 1f), hue);
        _clrPlayheadGlow = Rehue(new Color(1.00f, 0.85f, 0.85f, 1f), hue);
        _clrEqBot        = Rehue(new Color(0.38f, 0.04f, 0.07f, 1f), hue);
        _clrEqTop        = Rehue(new Color(1.00f, 0.52f, 0.62f, 1f), hue);
        _clrEqBase       = Rehue(new Color(0.80f, 0.10f, 0.15f, 1f), hue);
        _clrArtStripBg   = Rehue(new Color(0.20f, 0.07f, 0.09f, 1f), hue);
        _clrArtStripFill = Rehue(new Color(0.95f, 0.18f, 0.22f, 1f), hue);

        _bgTex          = MakeTex(cBg);
        _headerTex      = MakeTex(cHeader);
        _accentTex      = MakeTex(cAccent);
        _darkTex        = MakeTex(cBtn);
        _darkerTex      = MakeTex(cTrackRow);
        _trackActiveTex = MakeTex(cTrackAct);
        var panelTex    = MakeTex(cPanel);
        var accentHovTx = MakeTex(cAccentHov);
        var accentDimTx = MakeTex(cAccentDim);
        var listHdrTex  = MakeTex(cListHdr);
        var btnHovTex   = MakeTex(cBtnHov);
        var btnStopTex  = MakeTex(cBtnStop);

        var btnRoundDark    = MakeRoundedButtonTex(cBtn);
        var btnRoundDarkHov = MakeRoundedButtonTex(cBtnHov);
        var btnRoundAcc     = MakeRoundedButtonTex(cAccent);
        var btnRoundAccHov  = MakeRoundedButtonTex(cAccentHov);
        var btnRoundAccDim  = MakeRoundedButtonTex(cAccentDim);
        var btnRoundStop    = MakeRoundedButtonTex(cBtnStop);
        var btnRoundHeader  = MakeRoundedButtonTex(cHeader);
        var trackHovTex = MakeTex(cTrackHov);
        var progBgTex   = MakeTex(cProgBg);
        var thumbTex    = MakeTex(cThumb);

        _roundedBgTex = MakeRoundedRectTex(cBg, 10);
        _winStyle = new GUIStyle(GUI.skin.window);
        _winStyle.normal.background   = _roundedBgTex;
        _winStyle.onNormal.background = _roundedBgTex;
        _winStyle.padding.left = _winStyle.padding.right = 0;
        _winStyle.padding.top  = _winStyle.padding.bottom = 0;
        _winStyle.border.left = _winStyle.border.right = 10;
        _winStyle.border.top  = _winStyle.border.bottom = 10;

        _headerStyle = new GUIStyle(GUI.skin.box);
        _headerStyle.normal.background = _headerTex;
        _headerStyle.padding.left   = 12; _headerStyle.padding.right  = 4;
        _headerStyle.padding.top    = 2;  _headerStyle.padding.bottom = 2;
        _headerStyle.margin.left = _headerStyle.margin.right = 0;
        _headerStyle.margin.top  = _headerStyle.margin.bottom = 0;
        _headerStyle.border.left = _headerStyle.border.right = 0;
        _headerStyle.border.top  = _headerStyle.border.bottom = 0;

        _accentLineStyle = new GUIStyle(GUI.skin.box);
        _accentLineStyle.normal.background = _accentTex;
        _accentLineStyle.margin.left = _accentLineStyle.margin.right = 0;
        _accentLineStyle.margin.top  = _accentLineStyle.margin.bottom = 0;
        _accentLineStyle.border.left = _accentLineStyle.border.right = 0;
        _accentLineStyle.border.top  = _accentLineStyle.border.bottom = 0;

        _panelStyle = new GUIStyle(GUI.skin.box);
        _panelStyle.normal.background = panelTex;
        _panelStyle.padding.left = _panelStyle.padding.right  = 14;
        _panelStyle.padding.top  = _panelStyle.padding.bottom = 0;
        _panelStyle.margin.left = _panelStyle.margin.right = 0;
        _panelStyle.margin.top  = _panelStyle.margin.bottom = 0;
        _panelStyle.border.left = _panelStyle.border.right = 0;
        _panelStyle.border.top  = _panelStyle.border.bottom = 0;

        _listHeaderStyle = new GUIStyle(GUI.skin.box);
        _listHeaderStyle.normal.background = listHdrTex;
        _listHeaderStyle.padding.left   = 12; _listHeaderStyle.padding.right  = 4;
        _listHeaderStyle.padding.top    = 4;  _listHeaderStyle.padding.bottom = 4;
        _listHeaderStyle.margin.left = _listHeaderStyle.margin.right = 0;
        _listHeaderStyle.margin.top  = _listHeaderStyle.margin.bottom = 0;
        _listHeaderStyle.border.left = _listHeaderStyle.border.right = 0;
        _listHeaderStyle.border.top  = _listHeaderStyle.border.bottom = 0;

        _dividerStyle = new GUIStyle(GUI.skin.box);
        _dividerStyle.normal.background = MakeTex(Rehue(new Color(0.18f, 0.08f, 0.11f, 1f), hue));
        _dividerStyle.margin.left = _dividerStyle.margin.right = 0;
        _dividerStyle.margin.top  = _dividerStyle.margin.bottom = 0;
        _dividerStyle.border.left = _dividerStyle.border.right = 0;
        _dividerStyle.border.top  = _dividerStyle.border.bottom = 0;

        _titleStyle = new GUIStyle(GUI.skin.label);
        _titleStyle.fontStyle        = FontStyle.Bold;
        _titleStyle.fontSize         = 14;
        _titleStyle.normal.textColor = Rehue(new Color(1f, 0.65f, 0.68f, 1f), hue);
        _titleStyle.alignment        = TextAnchor.MiddleLeft;

        _trackLabelStyle = new GUIStyle(GUI.skin.label);
        _trackLabelStyle.fontStyle        = FontStyle.Bold;
        _trackLabelStyle.fontSize         = 14;
        _trackLabelStyle.normal.textColor = Color.white;
        _trackLabelStyle.wordWrap         = true;

        _stateStyle = new GUIStyle(GUI.skin.label);
        _stateStyle.fontSize         = 11;
        _stateStyle.normal.textColor = Rehue(new Color(0.80f, 0.40f, 0.45f, 1f), hue);

        _timeRightStyle = new GUIStyle(GUI.skin.label);
        _timeRightStyle.fontSize         = 10;
        _timeRightStyle.normal.textColor = Rehue(new Color(0.58f, 0.42f, 0.48f, 1f), hue);
        _timeRightStyle.alignment        = TextAnchor.MiddleRight;

        _sectionStyle = new GUIStyle(GUI.skin.label);
        _sectionStyle.fontStyle        = FontStyle.Bold;
        _sectionStyle.fontSize         = 10;
        _sectionStyle.normal.textColor = Rehue(new Color(0.68f, 0.46f, 0.52f, 1f), hue);
        _sectionStyle.alignment        = TextAnchor.MiddleLeft;

        _volLabelStyle = new GUIStyle(GUI.skin.label);
        _volLabelStyle.fontStyle        = FontStyle.Bold;
        _volLabelStyle.fontSize         = 10;
        _volLabelStyle.normal.textColor = Rehue(new Color(0.72f, 0.50f, 0.55f, 1f), hue);
        _volLabelStyle.alignment        = TextAnchor.MiddleLeft;

        _volPctStyle = new GUIStyle(GUI.skin.label);
        _volPctStyle.fontSize         = 10;
        _volPctStyle.normal.textColor = Rehue(new Color(0.58f, 0.42f, 0.48f, 1f), hue);
        _volPctStyle.alignment        = TextAnchor.MiddleRight;

        _seekTimeStyle = new GUIStyle(GUI.skin.label);
        _seekTimeStyle.fontSize         = 10;
        _seekTimeStyle.fontStyle        = FontStyle.Bold;
        _seekTimeStyle.normal.textColor = new Color(0.85f, 0.65f, 0.40f, 1f);
        _seekTimeStyle.alignment        = TextAnchor.MiddleLeft;

        _seekRemainStyle = new GUIStyle(GUI.skin.label);
        _seekRemainStyle.fontSize         = 10;
        _seekRemainStyle.fontStyle        = FontStyle.Bold;
        _seekRemainStyle.normal.textColor = new Color(0.65f, 0.45f, 0.30f, 0.85f);
        _seekRemainStyle.alignment        = TextAnchor.MiddleRight;

        _trackBadgeStyle = new GUIStyle(GUI.skin.label);
        _trackBadgeStyle.fontSize         = 10;
        _trackBadgeStyle.fontStyle        = FontStyle.Bold;
        _trackBadgeStyle.normal.textColor = Rehue(new Color(0.70f, 0.40f, 0.46f, 0.85f), hue);
        _trackBadgeStyle.alignment        = TextAnchor.MiddleLeft;

        _toastStyle = new GUIStyle(GUI.skin.box);
        _toastStyle.normal.background = MakeTex(Rehue(new Color(0.07f, 0.04f, 0.05f, 0.94f), hue));
        _toastStyle.border.left = _toastStyle.border.right = 0;
        _toastStyle.border.top  = _toastStyle.border.bottom = 0;

        _toastLabelStyle = new GUIStyle(GUI.skin.label);
        _toastLabelStyle.fontSize         = 11;
        _toastLabelStyle.fontStyle        = FontStyle.Bold;
        _toastLabelStyle.normal.textColor = Color.white;
        _toastLabelStyle.alignment        = TextAnchor.MiddleLeft;

        _emptyStyle = new GUIStyle(GUI.skin.label);
        _emptyStyle.fontSize         = 11;
        _emptyStyle.normal.textColor = Rehue(new Color(0.50f, 0.38f, 0.44f, 1f), hue);
        _emptyStyle.padding.left     = 14;
        _emptyStyle.wordWrap         = true;

        _bigTimeStyle = new GUIStyle(GUI.skin.label);
        _bigTimeStyle.fontSize         = 26;
        _bigTimeStyle.fontStyle        = FontStyle.Bold;
        _bigTimeStyle.normal.textColor = new Color(0.96f, 0.88f, 0.52f, 1f);
        _bigTimeStyle.alignment        = TextAnchor.MiddleCenter;

        _totalTimeStyle = new GUIStyle(GUI.skin.label);
        _totalTimeStyle.fontSize         = 17;
        _totalTimeStyle.fontStyle        = FontStyle.Bold;
        _totalTimeStyle.normal.textColor = new Color(0.96f, 0.88f, 0.52f, 0.55f);
        _totalTimeStyle.alignment        = TextAnchor.MiddleRight;

        _displayBgStyle = new GUIStyle(GUI.skin.box);
        _displayBgStyle.normal.background = MakeTex(new Color(0.040f, 0.028f, 0.040f, 1f));
        _displayBgStyle.padding.left = _displayBgStyle.padding.right = 0;
        _displayBgStyle.padding.top  = _displayBgStyle.padding.bottom = 0;
        _displayBgStyle.margin.left = _displayBgStyle.margin.right = 0;
        _displayBgStyle.margin.top  = _displayBgStyle.margin.bottom = 0;
        _displayBgStyle.border.left = _displayBgStyle.border.right = 0;
        _displayBgStyle.border.top  = _displayBgStyle.border.bottom = 0;

        _controlsBarStyle = new GUIStyle(GUI.skin.box);
        _controlsBarStyle.normal.background = MakeTex(Rehue(new Color(0.075f, 0.050f, 0.062f, 1f), hue));
        _controlsBarStyle.padding.left = _controlsBarStyle.padding.right = 0;
        _controlsBarStyle.padding.top  = _controlsBarStyle.padding.bottom = 5;
        _controlsBarStyle.margin.left = _controlsBarStyle.margin.right = 0;
        _controlsBarStyle.margin.top  = _controlsBarStyle.margin.bottom = 0;
        _controlsBarStyle.border.left = _controlsBarStyle.border.right = 0;
        _controlsBarStyle.border.top  = _controlsBarStyle.border.bottom = 0;

        _brandLabelStyle = new GUIStyle(GUI.skin.label);
        _brandLabelStyle.fontSize         = 13;
        _brandLabelStyle.fontStyle        = FontStyle.Bold;
        _brandLabelStyle.normal.textColor = new Color(0.96f, 0.38f, 0.44f, 1f);
        _brandLabelStyle.alignment        = TextAnchor.MiddleLeft;

        _progBg = new GUIStyle(GUI.skin.box);
        _progBg.normal.background = progBgTex;
        _progBg.border.left = _progBg.border.right = 0;
        _progBg.border.top  = _progBg.border.bottom = 0;

        _progFill = new GUIStyle(GUI.skin.box);
        _progFill.normal.background = _accentTex;
        _progFill.border.left = _progFill.border.right = 0;
        _progFill.border.top  = _progFill.border.bottom = 0;

        _progRemain = new GUIStyle(GUI.skin.box);
        _progRemain.normal.background = MakeTex(Rehue(new Color(0.38f, 0.05f, 0.08f, 1f), hue));
        _progRemain.border.left = _progRemain.border.right = 0;
        _progRemain.border.top  = _progRemain.border.bottom = 0;

        _progThumb = new GUIStyle(GUI.skin.box);
        _progThumb.normal.background = thumbTex;
        _progThumb.border.left = _progThumb.border.right = 0;
        _progThumb.border.top  = _progThumb.border.bottom = 0;

        _progGlow = new GUIStyle(GUI.skin.box);
        _progGlow.normal.background = MakeTex(Rehue(new Color(0.800f, 0.095f, 0.155f, 0.30f), hue));
        _progGlow.border.left = _progGlow.border.right = 0;
        _progGlow.border.top  = _progGlow.border.bottom = 0;

        _artStyle = new GUIStyle(GUI.skin.box);
        _artStyle.normal.background = MakeTex(new Color(0.15f, 0.08f, 0.10f, 1f));
        _artStyle.normal.textColor  = new Color(1f, 0.68f, 0.72f, 0.85f);
        _artStyle.fontSize           = 26;
        _artStyle.fontStyle          = FontStyle.Bold;
        _artStyle.alignment          = TextAnchor.MiddleCenter;
        _artStyle.border.left = _artStyle.border.right = 0;
        _artStyle.border.top  = _artStyle.border.bottom = 0;

        _artRoundedTex = MakeCircleTex(52);
        _artBgStyle = new GUIStyle(GUIStyle.none);
        _artBgStyle.normal.background = _artRoundedTex;
        _artBgStyle.border.left = _artBgStyle.border.right = 0;
        _artBgStyle.border.top  = _artBgStyle.border.bottom = 0;

        _artLetterStyle = new GUIStyle(GUIStyle.none);
        _artLetterStyle.normal.textColor = new Color(1f, 0.68f, 0.72f, 0.85f);
        _artLetterStyle.fontSize         = 26;
        _artLetterStyle.fontStyle        = FontStyle.Bold;
        _artLetterStyle.alignment        = TextAnchor.MiddleCenter;

        _artOverlayTex = MakeRoundedRectTex(Color.white, 10);
        _artOverlayStyle = new GUIStyle(GUIStyle.none);
        _artOverlayStyle.normal.background = _artOverlayTex;
        _artOverlayStyle.border.left = _artOverlayStyle.border.right = 10;
        _artOverlayStyle.border.top  = _artOverlayStyle.border.bottom = 10;

        _eqBar = new GUIStyle(GUI.skin.box);
        _eqBar.normal.background = _accentTex;
        _eqBar.border.left = _eqBar.border.right = 0;
        _eqBar.border.top  = _eqBar.border.bottom = 0;

        _eqGradTex = new Texture2D(1, 4, TextureFormat.RGBA32, false);
        _eqGradTex.filterMode = FilterMode.Bilinear;
        _eqGradTex.wrapMode   = TextureWrapMode.Clamp;
        _eqGradTex.SetPixel(0, 3, Rehue(new Color(1.00f, 0.45f, 0.55f, 1f), hue));
        _eqGradTex.SetPixel(0, 2, Rehue(new Color(0.90f, 0.15f, 0.22f, 1f), hue));
        _eqGradTex.SetPixel(0, 1, Rehue(new Color(0.65f, 0.07f, 0.12f, 1f), hue));
        _eqGradTex.SetPixel(0, 0, Rehue(new Color(0.28f, 0.02f, 0.03f, 1f), hue));
        _eqGradTex.Apply();
        UnityEngine.Object.DontDestroyOnLoad(_eqGradTex);
        _eqBarGrad = new GUIStyle(GUI.skin.box);
        _eqBarGrad.normal.background = _eqGradTex;
        _eqBarGrad.border.left = _eqBarGrad.border.right = 0;
        _eqBarGrad.border.top  = _eqBarGrad.border.bottom = 0;

        _eqPeakTex = MakeTex(Rehue(new Color(1.00f, 0.70f, 0.75f, 1f), hue));
        UnityEngine.Object.DontDestroyOnLoad(_eqPeakTex);
        _eqPeakBar = new GUIStyle(GUI.skin.box);
        _eqPeakBar.normal.background = _eqPeakTex;
        _eqPeakBar.border.left = _eqPeakBar.border.right = 0;
        _eqPeakBar.border.top  = _eqPeakBar.border.bottom = 0;

        var eqSegTex = MakeTex(Color.white);
        _eqSeg = new GUIStyle(GUI.skin.box);
        _eqSeg.normal.background = eqSegTex;
        _eqSeg.border.left = _eqSeg.border.right = 0;
        _eqSeg.border.top  = _eqSeg.border.bottom = 0;

        _invisTrack = new GUIStyle(GUI.skin.horizontalSlider);
        _invisTrack.normal.background = null;
        _invisTrack.border.left = _invisTrack.border.right = 0;
        _invisTrack.border.top  = _invisTrack.border.bottom = 0;
        _invisTrack.fixedHeight = 0;

        _invisThumb = new GUIStyle(GUI.skin.horizontalSliderThumb);
        _invisThumb.normal.background = null;
        _invisThumb.fixedWidth  = 0;
        _invisThumb.fixedHeight = 0;

        var btnBorder = new RectOffset();
        btnBorder.left = btnBorder.right = btnBorder.top = btnBorder.bottom = 6;

        _btnStyle = new GUIStyle(GUI.skin.button);
        _btnStyle.fontSize          = 12;
        _btnStyle.fontStyle         = FontStyle.Bold;
        _btnStyle.alignment         = TextAnchor.MiddleCenter;
        _btnStyle.border            = btnBorder;
        _btnStyle.normal.background = btnRoundDark;
        _btnStyle.normal.textColor  = new Color(0.92f, 0.80f, 0.83f, 1f);
        _btnStyle.hover.background  = btnRoundDarkHov;
        _btnStyle.hover.textColor   = Color.white;
        _btnStyle.active.background = btnRoundAcc;
        _btnStyle.active.textColor  = Color.white;
        _btnStyle.padding.left  = _btnStyle.padding.right  = 6;
        _btnStyle.padding.top   = _btnStyle.padding.bottom = 4;

        _closeBtnStyle = new GUIStyle(_btnStyle);
        _closeBtnStyle.fontSize              = 12;
        _closeBtnStyle.border                = btnBorder;
        _closeBtnStyle.normal.background     = btnRoundHeader;
        _closeBtnStyle.normal.textColor      = new Color(0.75f, 0.48f, 0.53f, 1f);
        _closeBtnStyle.hover.background      = btnRoundAcc;
        _closeBtnStyle.hover.textColor       = Color.white;
        _closeBtnStyle.active.background     = btnRoundAccHov;

        _btnPlay = new GUIStyle(_btnStyle);
        _btnPlay.fontSize              = 12;
        _btnPlay.border                = btnBorder;
        _btnPlay.normal.background     = btnRoundAcc;
        _btnPlay.normal.textColor      = Color.white;
        _btnPlay.hover.background      = btnRoundAccHov;
        _btnPlay.hover.textColor       = Color.white;
        _btnPlay.active.background     = btnRoundAccHov;

        _btnStop = new GUIStyle(_btnStyle);
        _btnStop.fontSize              = 11;
        _btnStop.border                = btnBorder;
        _btnStop.normal.background     = btnRoundStop;
        _btnStop.normal.textColor      = new Color(0.80f, 0.55f, 0.60f, 1f);
        _btnStop.hover.background      = btnRoundDarkHov;
        _btnStop.hover.textColor       = Color.white;

        _btnToggleOn = new GUIStyle(_btnStyle);
        _btnToggleOn.fontSize              = 11;
        _btnToggleOn.border                = btnBorder;
        _btnToggleOn.normal.background     = btnRoundAcc;
        _btnToggleOn.normal.textColor      = Color.white;
        _btnToggleOn.hover.background      = btnRoundAccHov;
        _btnToggleOn.hover.textColor       = Color.white;

        _btnToggleOff = new GUIStyle(_btnStyle);
        _btnToggleOff.fontSize              = 11;
        _btnToggleOff.border                = btnBorder;
        _btnToggleOff.normal.background     = btnRoundAccDim;
        _btnToggleOff.normal.textColor      = new Color(0.72f, 0.48f, 0.54f, 1f);
        _btnToggleOff.hover.background      = btnRoundDarkHov;
        _btnToggleOff.hover.textColor       = Color.white;

        _btnSmall = new GUIStyle(_btnStyle);
        _btnSmall.fontSize             = 10;
        _btnSmall.padding.left  = _btnSmall.padding.right  = 4;
        _btnSmall.padding.top   = _btnSmall.padding.bottom = 2;

        _trackNormal = new GUIStyle(GUI.skin.button);
        _trackNormal.fontSize          = 11;
        _trackNormal.alignment         = TextAnchor.MiddleLeft;
        _trackNormal.normal.background = _darkerTex;
        _trackNormal.normal.textColor  = new Color(0.82f, 0.68f, 0.72f, 1f);
        _trackNormal.hover.background  = trackHovTex;
        _trackNormal.hover.textColor   = Color.white;
        _trackNormal.active.background = _accentTex;
        _trackNormal.active.textColor  = Color.white;
        _trackNormal.padding.left      = 14;
        _trackNormal.padding.right     = 6;
        _trackNormal.padding.top       = _trackNormal.padding.bottom = 3;
        _trackNormal.margin.left = _trackNormal.margin.right = 0;
        _trackNormal.margin.top  = 1; _trackNormal.margin.bottom = 1;
        _trackNormal.border.left = _trackNormal.border.right = 0;
        _trackNormal.border.top  = _trackNormal.border.bottom = 0;

        _trackActive = new GUIStyle(_trackNormal);
        _trackActive.fontStyle         = FontStyle.Bold;
        _trackActive.normal.background = _trackActiveTex;
        _trackActive.normal.textColor  = Color.white;
        _trackActive.hover.background  = _trackActiveTex;
        _trackActive.hover.textColor   = Color.white;

        var cDisp = new Color(0.040f, 0.028f, 0.040f, 1f);
        _cornerTL = MakeCornerTex(cDisp, 0);
        _cornerTR = MakeCornerTex(cDisp, 1);
        _cornerBL = MakeCornerTex(cDisp, 2);
        _cornerBR = MakeCornerTex(cDisp, 3);

        _cornerStyleTL = new GUIStyle(GUIStyle.none); _cornerStyleTL.normal.background = _cornerTL;
        _cornerStyleTR = new GUIStyle(GUIStyle.none); _cornerStyleTR.normal.background = _cornerTR;
        _cornerStyleBL = new GUIStyle(GUIStyle.none); _cornerStyleBL.normal.background = _cornerBL;
        _cornerStyleBR = new GUIStyle(GUIStyle.none); _cornerStyleBR.normal.background = _cornerBR;
    }

    private static Texture2D MakeTex(Color c)
    {
        var t = new Texture2D(1, 1);
        t.SetPixel(0, 0, c);
        t.Apply();
        UnityEngine.Object.DontDestroyOnLoad(t);
        return t;
    }

    private static Texture2D MakeRoundedButtonTex(Color fill, int size = 16, int radius = 5)
    {
        var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
        t.filterMode = FilterMode.Bilinear;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = 0f, dy = 0f;
            if (x < radius)             dx = radius - x;
            else if (x > size - 1 - radius) dx = x - (size - 1 - radius);
            if (y < radius)             dy = radius - y;
            else if (y > size - 1 - radius) dy = y - (size - 1 - radius);
            float dist = Mathf.Sqrt(dx * dx + dy * dy);
            float alpha = Mathf.Clamp01(radius + 0.5f - dist);

            Color c = fill;

            if (y == size - 1) c = Color.Lerp(c, Color.white, 0.22f);
            else if (y == size - 2) c = Color.Lerp(c, Color.white, 0.10f);

            else if (y == 0) c = Color.Lerp(c, Color.black, 0.28f);
            else if (y == 1) c = Color.Lerp(c, Color.black, 0.14f);

            c.a *= alpha;
            t.SetPixel(x, y, c);
        }
        t.Apply();
        UnityEngine.Object.DontDestroyOnLoad(t);
        return t;
    }

    private static Texture2D MakeCornerTex(Color fill, int corner)
    {
        const int cs = 10;
        var t = new Texture2D(cs, cs, TextureFormat.RGBA32, false);
        t.filterMode = FilterMode.Bilinear;
        float ccx, ccy;
        switch (corner)
        {
            case 0: ccx =  cs; ccy = -1; break;
            case 1: ccx = -1;  ccy = -1; break;
            case 2: ccx =  cs; ccy =  cs; break;
            case 3: ccx = -1;  ccy =  cs; break;
            default: ccx = ccy = cs * 0.5f; break;
        }
        for (int y = 0; y < cs; y++)
            for (int x = 0; x < cs; x++)
            {
                float dist  = Mathf.Sqrt((x + 0.5f - ccx) * (x + 0.5f - ccx) + (y + 0.5f - ccy) * (y + 0.5f - ccy));
                float alpha = Mathf.Clamp01(dist - cs + 1f);
                var   c     = fill;
                c.a         = fill.a * alpha;
                t.SetPixel(x, y, c);
            }
        t.Apply();
        UnityEngine.Object.DontDestroyOnLoad(t);
        return t;
    }

    private static Texture2D MakeCircleTex(int diameter)
    {
        var t = new Texture2D(diameter, diameter, TextureFormat.RGBA32, false);
        t.filterMode = FilterMode.Bilinear;
        float r = diameter * 0.5f;
        for (int y = 0; y < diameter; y++)
            for (int x = 0; x < diameter; x++)
            {
                float dx   = x + 0.5f - r;
                float dy   = y + 0.5f - r;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float a    = Mathf.Clamp01(r - dist + 0.5f);
                t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        t.Apply();
        UnityEngine.Object.DontDestroyOnLoad(t);
        return t;
    }

    private static Texture2D MakeRoundedRectTex(Color fill, int radius)
    {
        int sz = radius * 2 + 4;
        var t  = new Texture2D(sz, sz, TextureFormat.RGBA32, false);
        t.filterMode = FilterMode.Bilinear;
        for (int y = 0; y < sz; y++)
            for (int x = 0; x < sz; x++)
            {
                bool inCorner = (x < radius || x >= sz - radius) && (y < radius || y >= sz - radius);
                if (!inCorner) { t.SetPixel(x, y, fill); continue; }
                float cx    = x < radius ? radius : sz - 1 - radius;
                float cy    = y < radius ? radius : sz - 1 - radius;
                float dist  = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));
                float alpha = Mathf.Clamp01(radius - dist + 0.5f);
                var   c     = fill;
                c.a         = fill.a * alpha;
                t.SetPixel(x, y, c);
            }
        t.Apply();
        UnityEngine.Object.DontDestroyOnLoad(t);
        return t;
    }

    internal void ScanTracks()
    {
        Stop();
        _tracks.Clear();
        _trackNames.Clear();
        _trackDurations.Clear();
        _loadedRaw.Clear();
        _rawSamples = null;
        _rawTotal   = 0;
        _currentIndex = -1;

        string folder = Path.Combine(BepInEx.Paths.PluginPath, "Nocturne", "Music");
        if (!Directory.Exists(folder))
        {
            try { Directory.CreateDirectory(folder); } catch { }
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
        while (!done) yield return null;
        for (int i = 0; i < paths.Length && i < _trackDurations.Count; i++)
            _trackDurations[i] = durations[i];
    }

    private static float GetDurationFast(string path)
    {
        try
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".mp3")
                using (var r = new Mp3FileReader(path)) return (float)r.TotalTime.TotalSeconds;
            if (ext == ".wav")
                using (var r = new WaveFileReader(path)) return (float)r.TotalTime.TotalSeconds;
            if (ext == ".ogg")
                using (var vr = new VorbisReader(path)) return (float)vr.TotalTime.TotalSeconds;
        }
        catch { }
        return 0f;
    }

    private void SelectPreset(int idx)
    {
        _activePresetIdx = idx;
        _eqGains = kPresetGains[idx];
        RecalculateEqCoefficients();
        _eqStateResetPending = true;
        if (NocturneConfig.MusicEqPreset != null)
            NocturneConfig.MusicEqPreset.Value = idx;
    }

    internal void Play(int index)
    {
        if (index < 0 || index >= _tracks.Count) return;
        _currentIndex = index;
        _loadGen++;
        TrimCache(index);
        if (_loadedRaw.TryGetValue(index, out RawTrack cached) && cached != null && cached.Samples != null)
        {
            PlayRawTrack(cached, _tracks[index]);
            PrefetchNeighbors(index);
            return;
        }
        this.StartCoroutine(LoadAndPlay(index, _loadGen));
    }

    private void TrimCache(int keepIndex)
    {

        var keep = new HashSet<int> { keepIndex };
        if (TrackCount > 1)
        {
            keep.Add((keepIndex + 1) % TrackCount);
            keep.Add(keepIndex <= 0 ? TrackCount - 1 : keepIndex - 1);
        }

        var toRemove = new List<int>();
        foreach (int k in _loadedRaw.Keys)
            if (!keep.Contains(k))
                toRemove.Add(k);
        foreach (int k in toRemove) _loadedRaw.Remove(k);
    }

    private void PrefetchNeighbors(int center)
    {
        if (_tracks.Count <= 1) return;
        int next = (center + 1) % _tracks.Count;
        int prev = center <= 0 ? _tracks.Count - 1 : center - 1;
        if (!_loadedRaw.ContainsKey(next)) this.StartCoroutine(PrefetchTrack(next));
        if (prev != next && !_loadedRaw.ContainsKey(prev)) this.StartCoroutine(PrefetchTrack(prev));
    }

    private bool IsNeighborOfCurrent(int index)
    {
        if (index == _currentIndex) return true;
        if (_tracks.Count <= 1) return false;
        int next = (_currentIndex + 1) % _tracks.Count;
        int prev = _currentIndex <= 0 ? _tracks.Count - 1 : _currentIndex - 1;
        return index == next || index == prev;
    }

    [HideFromIl2Cpp]
    private IEnumerator PrefetchTrack(int index)
    {
        if (index < 0 || index >= _tracks.Count || _loadedRaw.ContainsKey(index)) yield break;

        float[] samples = null; int channels = 0, frequency = 0; bool done = false;
        string path = _tracks[index];
        string ext  = Path.GetExtension(path).ToLowerInvariant();
        int targetRate = AudioSettings.outputSampleRate;

        ThreadPool.QueueUserWorkItem(_ =>
        {
            try
            {
                lock (_decodeGate)
                {
                    if (ext == ".ogg") DecodeOgg(path, targetRate, out samples, out channels, out frequency);
                    else DecodeNAudio(path, ext, targetRate, out samples, out channels, out frequency);
                }
            }
            catch { samples = null; }
            finally { done = true; }
        });

        while (!done) yield return null;

        if (samples != null && !_loadedRaw.ContainsKey(index) && IsNeighborOfCurrent(index))
            _loadedRaw[index] = new RawTrack { Samples = samples, Channels = channels, Frequency = frequency };
    }

    internal void PlayPause()
    {
        if (_source == null) return;
        if (_source.isPlaying) { _source.Pause(); return; }
        if (_source.clip != null) { _source.UnPause(); return; }
        Play(_currentIndex >= 0 ? _currentIndex : 0);
    }

    internal void Stop()
    {
        if (_source == null) return;
        _source.Stop();
        if (_source.clip != null)
        {
            try { UnityEngine.Object.Destroy(_source.clip); } catch { }
            _source.clip = null;
        }
        _autoPlayed = false;
    }

    internal void Next()
    {
        if (TrackCount == 0) return;
        Play(_shuffle ? UnityEngine.Random.Range(0, TrackCount) : (_currentIndex + 1) % TrackCount);
    }

    internal void Prev()
    {
        if (TrackCount == 0) return;
        Play(_currentIndex <= 0 ? TrackCount - 1 : _currentIndex - 1);
    }

    [HideFromIl2Cpp]
    private void PlayRawTrack(RawTrack raw, string sourcePath)
    {
        if (_source == null || raw == null || raw.Samples == null || raw.Channels < 1) return;

        bool startCrossfade = _source.isPlaying
                              && _rawSamples != null
                              && _rawChannels == raw.Channels
                              && _rawTotal > 0
                              && _rawReadPos < _rawTotal;
        if (startCrossfade)
        {
            _rawSamplesB  = _rawSamples;
            _rawChannelsB = _rawChannels;
            _rawTotalB    = _rawTotal;
            _rawReadPosB  = _rawReadPos;
            if (_eqX1 != null)
            {
                _eqX1B = (double[])_eqX1.Clone();
                _eqX2B = (double[])_eqX2.Clone();
                _eqY1B = (double[])_eqY1.Clone();
                _eqY2B = (double[])_eqY2.Clone();
            }
            else { _eqX1B = _eqX2B = _eqY1B = _eqY2B = null; }
            if (_bassEnhState  != null) _bassEnhStateB  = (float[])_bassEnhState.Clone();
            if (_lcBassState   != null) _lcBassStateB   = (float[])_lcBassState.Clone();
            if (_deessLpfLow   != null) _deessLpfLowB   = (float[])_deessLpfLow.Clone();
            if (_deessLpfHigh  != null) _deessLpfHighB  = (float[])_deessLpfHigh.Clone();
            if (_deessEnv      != null) _deessEnvB      = (float[])_deessEnv.Clone();
            if (_deessGain     != null) _deessGainB     = (float[])_deessGain.Clone();
            _sideBassStateB      = _sideBassState;
            _crossfadeFrames     = (int)(raw.Frequency * kCrossfadeSec);
            _crossfadeFramesDone = 0;
            _crossfading         = true;
        }
        else
        {
            _crossfading  = false;
            _rawSamplesB  = null;
        }

        _source.Stop();
        if (_source.clip != null)
        {
            try { UnityEngine.Object.Destroy(_source.clip); } catch { }
            _source.clip = null;
        }

        _rawSamples   = raw.Samples;
        _rawChannels  = raw.Channels;
        _rawFrequency = raw.Frequency;
        _rawTotal     = raw.Samples.Length / raw.Channels;
        _rawReadPos   = 0;
        EnsureEqState();
        RecalculateEqCoefficients();

        string name = Path.GetFileNameWithoutExtension(sourcePath);
        AudioClip clip = AudioClip.Create(name, _rawTotal, _rawChannels, _rawFrequency, true,
            (Action<Il2CppStructArray<float>>)PcmReaderCallback,
            (Action<int>)PcmSetPositionCallback);

        _source.clip   = clip;
        _source.loop   = _loop;
        _source.volume = _volume;
        _source.time   = 0f;
        _source.Play();
        _autoPlayed  = true;
        _sweepActive = true;
        _sweepTimer  = 0f;
        StatusText   = _currentIndex >= 0 && _currentIndex < _trackNames.Count ? _trackNames[_currentIndex] : string.Empty;
        if (_currentIndex >= 0 && _currentIndex < _trackNames.Count)
        {
            _toastText  = _trackNames[_currentIndex];
            _toastTimer = 2.5f;
        }
    }

    private void EnsureEqState()
    {
        int needed = _rawChannels * 10;
        if (_eqX1 == null || _eqX1.Length != needed)
        {
            _eqX1 = new double[needed];
            _eqX2 = new double[needed];
            _eqY1 = new double[needed];
            _eqY2 = new double[needed];
        }
        else
        {
            Array.Clear(_eqX1, 0, needed); Array.Clear(_eqX2, 0, needed);
            Array.Clear(_eqY1, 0, needed); Array.Clear(_eqY2, 0, needed);
        }

        if (_bassEnhState == null || _bassEnhState.Length != _rawChannels)
            _bassEnhState = new float[_rawChannels];
        else Array.Clear(_bassEnhState, 0, _rawChannels);
        if (_lcBassState == null || _lcBassState.Length != _rawChannels)
            _lcBassState = new float[_rawChannels];
        else Array.Clear(_lcBassState, 0, _rawChannels);

        if (_deessLpfLow == null || _deessLpfLow.Length != _rawChannels)
        {
            _deessLpfLow   = new float[_rawChannels];
            _deessLpfHigh  = new float[_rawChannels];
            _deessEnv      = new float[_rawChannels];
            _deessGain     = new float[_rawChannels];
        }
        else
        {
            Array.Clear(_deessLpfLow,   0, _rawChannels);
            Array.Clear(_deessLpfHigh,  0, _rawChannels);
            Array.Clear(_deessEnv,      0, _rawChannels);
        }
        for (int i = 0; i < _rawChannels; i++) _deessGain[i] = 1f;

        if (_cfLpf == null || _cfLpf.Length != _rawChannels)
            _cfLpf = new float[_rawChannels];
        else Array.Clear(_cfLpf, 0, _rawChannels);

        if (_dcX1 == null || _dcX1.Length != _rawChannels)
        {
            _dcX1 = new float[_rawChannels];
            _dcY1 = new float[_rawChannels];
        }
        else
        {
            Array.Clear(_dcX1, 0, _rawChannels);
            Array.Clear(_dcY1, 0, _rawChannels);
        }

        _sideBassState = 0f;

        int limSize = kLookaheadFramesC * _rawChannels;
        if (_limBuf == null || _limBuf.Length != limSize)
            _limBuf = new float[limSize];
        else Array.Clear(_limBuf, 0, limSize);
        _limWritePos = 0;
        _limEnvPeak  = 0f;
        _limCurrGain = 1f;
    }

    private static float SoftKneeEq(float gainDb, int bandIndex)
    {
        if (gainDb <= 0f) return gainDb;
        float knee;
        if      (bandIndex == 6 || bandIndex == 7) knee = 3f;
        else if (bandIndex >= 8)                    knee = 4f;
        else if (bandIndex == 5)                    knee = 6f;
        else                                        knee = 8f;
        return gainDb <= knee ? gainDb : knee + (gainDb - knee) * 0.4f;
    }

    private void RecalculateEqCoefficients()
    {
        if (_rawFrequency <= 0) { _eqCoeffs = null; return; }

        bool flat = true;
        for (int b = 0; b < _eqGains.Length; b++) if (_eqGains[b] != 0f) { flat = false; break; }
        if (flat) { _eqCoeffs = null; return; }

        const int kB = 10;

        double energySum = 0.0;
        for (int b = 0; b < kB; b++)
        {
            float gEff = SoftKneeEq(_eqGains[b], b);
            double lin = Math.Pow(10.0, gEff / 20.0);
            energySum += lin * lin;
        }
        double avgEnergy = energySum / kB;
        double preAtten  = avgEnergy > 1.0 ? 1.0 / Math.Sqrt(avgEnergy) : 1.0;

        var b0c = new double[kB]; var b1c = new double[kB]; var b2c = new double[kB];
        var a1c = new double[kB]; var a2c = new double[kB];
        for (int b = 0; b < kB; b++)
        {
            float gEff   = SoftKneeEq(_eqGains[b], b);
            double A     = Math.Pow(10.0, gEff / 40.0);
            double w0    = 2.0 * Math.PI * kEqBandFreqs[b] / _rawFrequency;
            double sinW  = Math.Sin(w0), cosW = Math.Cos(w0);
            double alpha = sinW / 4.0;
            double a0    = 1.0 + alpha / A;
            b0c[b] = (1.0 + alpha * A) / a0;
            b1c[b] = (-2.0 * cosW)     / a0;
            b2c[b] = (1.0 - alpha * A) / a0;
            a1c[b] = (-2.0 * cosW)     / a0;
            a2c[b] = (1.0 - alpha / A) / a0;
        }
        _eqCoeffs = new EqCoeffs(b0c, b1c, b2c, a1c, a2c, preAtten);
    }

    [HideFromIl2Cpp]
    private void PcmReaderCallback(Il2CppStructArray<float> data)
    {
        int len   = data.Length;
        var raw   = _rawSamples;
        int ch    = _rawChannels;
        int total = _rawTotal;
        int pos   = _rawReadPos;
        if (raw == null || ch < 1 || total <= 0 || _limBuf == null)
        {
            for (int i = 0; i < len; i++) data[i] = 0f;
            return;
        }

        int framesReq  = len / ch;
        int framesLeft = total - pos;
        if (framesLeft < 0) framesLeft = 0;
        int framesNow  = framesReq < framesLeft ? framesReq : framesLeft;

        var coeffs = _eqCoeffs;
        var b0 = coeffs?.B0; var b1 = coeffs?.B1; var b2 = coeffs?.B2;
        var a1 = coeffs?.A1; var a2 = coeffs?.A2;
        double pre = coeffs != null ? coeffs.PreAtten : 1.0;
        var x1 = _eqX1; var x2 = _eqX2; var y1 = _eqY1; var y2 = _eqY2;

        var gainsSnapshot = _eqGains;

        if (_eqStateResetPending)
        {
            if (x1 != null) { Array.Clear(x1, 0, x1.Length); Array.Clear(x2, 0, x2.Length); }
            if (y1 != null) { Array.Clear(y1, 0, y1.Length); Array.Clear(y2, 0, y2.Length); }
            _eqStateResetPending = false;
        }

        int sr = _rawFrequency > 0 ? _rawFrequency : 48000;
        float bassAlpha     = 1f - Mathf.Exp(-6.2831853f * kBassEnhCutoffHz / sr);
        float lcAlpha       = 1f - Mathf.Exp(-6.2831853f * kLcBassCutoffHz  / sr);
        float sideBassAlpha = 1f - Mathf.Exp(-6.2831853f * kSideBassCutoffHz / sr);
        float deessLowAlpha  = 1f - Mathf.Exp(-6.2831853f * kDeessLowCutoffHz  / sr);
        float deessHighAlpha = 1f - Mathf.Exp(-6.2831853f * kDeessHighCutoffHz / sr);
        float cfAlpha        = 1f - Mathf.Exp(-6.2831853f * kCrossfeedCutoffHz / sr);

        float lcGain        = Mathf.Max(0f, (1f - _volume) * 0.22f);
        float sideMul       = 1f + kStereoWidth * 2f;

        float eqHotAtten  = 1f;
        if (coeffs != null)
        {
            float w0  = 0.5f, w1 = 0.6f, w2 = 0.8f, w3 = 0.95f, w4 = 1.0f;
            float w5  = 1.15f, w6 = 1.75f, w7 = 1.95f, w8 = 1.65f, w9 = 1.15f;

            float pain1 = 0f, pain2 = 0f;
            for (int b = 0; b < gainsSnapshot.Length; b++)
            {
                float g = SoftKneeEq(gainsSnapshot[b], b);
                if (g <= 0f) continue;
                float lin = (float)Math.Pow(10.0, g / 20.0);
                float bw;
                switch (b)
                {
                    case 0: bw = w0; break; case 1: bw = w1; break; case 2: bw = w2; break;
                    case 3: bw = w3; break; case 4: bw = w4; break; case 5: bw = w5; break;
                    case 6: bw = w6; break; case 7: bw = w7; break; case 8: bw = w8; break;
                    default: bw = w9; break;
                }
                float weighted = (lin - 1f) * bw;
                if (weighted > pain1) { pain2 = pain1; pain1 = weighted; }
                else if (weighted > pain2) { pain2 = weighted; }
            }
            float pain = pain1 + pain2 * 0.5f;

            eqHotAtten = 1f / (1f + pain * 0.12f);
        }

        float bassEnhScale = 1f;
        {
            float bassBoostMax = gainsSnapshot[0];
            if (gainsSnapshot[1] > bassBoostMax) bassBoostMax = gainsSnapshot[1];
            if (gainsSnapshot[2] > bassBoostMax) bassBoostMax = gainsSnapshot[2];
            if (bassBoostMax > 3f)
            {
                bassEnhScale = 1f - (bassBoostMax - 3f) / 9f;
                if (bassEnhScale < 0f) bassEnhScale = 0f;
            }
        }
        float bassEnhAmtEff = kBassEnhAmount * bassEnhScale;

        bool  stereo      = ch == 2;
        var   bsA = _bassEnhState;
        var   lcA = _lcBassState;

        bool  xfd      = _crossfading;
        int   xfdFr    = _crossfadeFrames;
        int   xfdDone  = _crossfadeFramesDone;
        var   rawB     = _rawSamplesB;
        int   chB      = _rawChannelsB;
        int   totB     = _rawTotalB;
        int   posB     = _rawReadPosB;
        var   x1B = _eqX1B; var x2B = _eqX2B; var y1B = _eqY1B; var y2B = _eqY2B;
        var   bsB = _bassEnhStateB; var lcB = _lcBassStateB;

        int srcA = pos * ch;
        int o    = 0;
        int limFrames = kLookaheadFramesC;
        int limWP = _limWritePos;
        float limEnv = _limEnvPeak;
        float limGain = _limCurrGain;

        for (int f = 0; f < framesNow; f++)
        {

            double dL = 0.0, dR = 0.0;
            if (stereo)
            {
                dL = raw[srcA] * pre;
                dR = raw[srcA + 1] * pre;
                srcA += 2;
                if (coeffs != null)
                {
                    for (int b = 0; b < 10; b++)
                    {
                        int si = b;
                        double y = b0[b]*dL + b1[b]*x1[si] + b2[b]*x2[si] - a1[b]*y1[si] - a2[b]*y2[si];
                        x2[si] = x1[si]; x1[si] = dL;
                        y2[si] = y1[si]; y1[si] = y;
                        dL = y;
                    }
                    for (int b = 0; b < 10; b++)
                    {
                        int si = 10 + b;
                        double y = b0[b]*dR + b1[b]*x1[si] + b2[b]*x2[si] - a1[b]*y1[si] - a2[b]*y2[si];
                        x2[si] = x1[si]; x1[si] = dR;
                        y2[si] = y1[si]; y1[si] = y;
                        dR = y;
                    }
                }
                float sL = (float)dL, sR = (float)dR;

                float mid    = (sL + sR) * 0.5f;
                float sideRw = (sL - sR) * 0.5f;
                _sideBassState += sideBassAlpha * (sideRw - _sideBassState);
                float sideBass = _sideBassState;
                float sideHigh = sideRw - sideBass;
                float side = sideBass * kSideBassNarrow + sideHigh * sideMul;
                sL = (mid + side) * eqHotAtten;
                sR = (mid - side) * eqHotAtten;

                bsA[0] += bassAlpha * (sL - bsA[0]);
                bsA[1] += bassAlpha * (sR - bsA[1]);
                sL += bsA[0] * Mathf.Abs(bsA[0]) * bassEnhAmtEff;
                sR += bsA[1] * Mathf.Abs(bsA[1]) * bassEnhAmtEff;

                if (lcGain > 0.005f)
                {
                    lcA[0] += lcAlpha * (sL - lcA[0]);
                    lcA[1] += lcAlpha * (sR - lcA[1]);
                    float lcTrebL = sL - lcA[0];
                    float lcTrebR = sR - lcA[1];
                    sL += lcA[0] * lcGain + lcTrebL * lcGain * kLcTrebleAmount;
                    sR += lcA[1] * lcGain + lcTrebR * lcGain * kLcTrebleAmount;
                }

                _deessLpfLow[0]  += deessLowAlpha  * (sL - _deessLpfLow[0]);
                _deessLpfHigh[0] += deessHighAlpha * (sL - _deessLpfHigh[0]);
                float bandL = _deessLpfHigh[0] - _deessLpfLow[0];
                float aBandL = bandL < 0f ? -bandL : bandL;
                if (aBandL > _deessEnv[0]) _deessEnv[0] = aBandL;
                else                       _deessEnv[0] *= 0.99955f;
                float tgL;
                if (_deessEnv[0] > kDeessThreshold)
                {

                    float over = _deessEnv[0] - kDeessThreshold;
                    float deessTarget = kDeessThreshold + over / kDeessRatio;
                    tgL = deessTarget / _deessEnv[0];
                    if (tgL < kDeessMinGain) tgL = kDeessMinGain;
                }
                else tgL = 1f;
                if (tgL < _deessGain[0]) _deessGain[0] += (tgL - _deessGain[0]) * 0.55f;
                else                     _deessGain[0] += (tgL - _deessGain[0]) * 0.018f;
                float hpL = sL - _deessLpfLow[0];
                sL = _deessLpfLow[0] + hpL * _deessGain[0];

                _deessLpfLow[1]  += deessLowAlpha  * (sR - _deessLpfLow[1]);
                _deessLpfHigh[1] += deessHighAlpha * (sR - _deessLpfHigh[1]);
                float bandR = _deessLpfHigh[1] - _deessLpfLow[1];
                float aBandR = bandR < 0f ? -bandR : bandR;
                if (aBandR > _deessEnv[1]) _deessEnv[1] = aBandR;
                else                       _deessEnv[1] *= 0.99955f;
                float tgR;
                if (_deessEnv[1] > kDeessThreshold)
                {
                    float over = _deessEnv[1] - kDeessThreshold;
                    float deessTarget = kDeessThreshold + over / kDeessRatio;
                    tgR = deessTarget / _deessEnv[1];
                    if (tgR < kDeessMinGain) tgR = kDeessMinGain;
                }
                else tgR = 1f;
                if (tgR < _deessGain[1]) _deessGain[1] += (tgR - _deessGain[1]) * 0.55f;
                else                     _deessGain[1] += (tgR - _deessGain[1]) * 0.018f;
                float hpR = sR - _deessLpfLow[1];
                sR = _deessLpfLow[1] + hpR * _deessGain[1];

                float fadeIn = 1f, fadeOut = 0f;
                if (xfd)
                {
                    float t = (float)xfdDone / xfdFr;
                    if (t > 1f) t = 1f;
                    fadeIn  = t * t * (3f - 2f * t);
                    fadeOut = 1f - fadeIn;
                    sL *= fadeIn;
                    sR *= fadeIn;
                    if (rawB != null && posB < totB && chB == 2)
                    {
                        int bi = posB * 2;
                        double bL = rawB[bi] * pre;
                        double bR = rawB[bi + 1] * pre;
                        if (coeffs != null && x1B != null)
                        {
                            for (int b = 0; b < 10; b++)
                            {
                                int si = b;
                                double y = b0[b]*bL + b1[b]*x1B[si] + b2[b]*x2B[si] - a1[b]*y1B[si] - a2[b]*y2B[si];
                                x2B[si] = x1B[si]; x1B[si] = bL;
                                y2B[si] = y1B[si]; y1B[si] = y;
                                bL = y;
                            }
                            for (int b = 0; b < 10; b++)
                            {
                                int si = 10 + b;
                                double y = b0[b]*bR + b1[b]*x1B[si] + b2[b]*x2B[si] - a1[b]*y1B[si] - a2[b]*y2B[si];
                                x2B[si] = x1B[si]; x1B[si] = bR;
                                y2B[si] = y1B[si]; y1B[si] = y;
                                bR = y;
                            }
                        }
                        float sBL = (float)bL, sBR = (float)bR;
                        float mB = (sBL + sBR) * 0.5f;
                        float sBRaw = (sBL - sBR) * 0.5f;
                        _sideBassStateB += sideBassAlpha * (sBRaw - _sideBassStateB);
                        float sBBass = _sideBassStateB;
                        float sBHigh = sBRaw - sBBass;
                        float dB = sBBass * kSideBassNarrow + sBHigh * sideMul;
                        sBL = (mB + dB) * eqHotAtten;
                        sBR = (mB - dB) * eqHotAtten;
                        if (bsB != null)
                        {
                            bsB[0] += bassAlpha * (sBL - bsB[0]);
                            bsB[1] += bassAlpha * (sBR - bsB[1]);
                            sBL += bsB[0] * Mathf.Abs(bsB[0]) * bassEnhAmtEff;
                            sBR += bsB[1] * Mathf.Abs(bsB[1]) * bassEnhAmtEff;
                        }
                        if (lcGain > 0.005f && lcB != null)
                        {
                            lcB[0] += lcAlpha * (sBL - lcB[0]);
                            lcB[1] += lcAlpha * (sBR - lcB[1]);
                            sBL += lcB[0] * lcGain + (sBL - lcB[0]) * lcGain * kLcTrebleAmount;
                            sBR += lcB[1] * lcGain + (sBR - lcB[1]) * lcGain * kLcTrebleAmount;
                        }
                        if (_deessLpfLowB != null && _deessLpfHighB != null)
                        {
                            _deessLpfLowB[0]  += deessLowAlpha  * (sBL - _deessLpfLowB[0]);
                            _deessLpfHighB[0] += deessHighAlpha * (sBL - _deessLpfHighB[0]);
                            float bBL = _deessLpfHighB[0] - _deessLpfLowB[0];
                            float aBBL = bBL < 0f ? -bBL : bBL;
                            if (aBBL > _deessEnvB[0]) _deessEnvB[0] = aBBL;
                            else                      _deessEnvB[0] *= 0.99955f;
                            float tBL;
                            if (_deessEnvB[0] > kDeessThreshold)
                            {
                                float over = _deessEnvB[0] - kDeessThreshold;
                                float deessTarget = kDeessThreshold + over / kDeessRatio;
                                tBL = deessTarget / _deessEnvB[0];
                                if (tBL < kDeessMinGain) tBL = kDeessMinGain;
                            }
                            else tBL = 1f;
                            if (tBL < _deessGainB[0]) _deessGainB[0] += (tBL - _deessGainB[0]) * 0.55f;
                            else                      _deessGainB[0] += (tBL - _deessGainB[0]) * 0.018f;
                            float hBL = sBL - _deessLpfLowB[0];
                            sBL = _deessLpfLowB[0] + hBL * _deessGainB[0];

                            _deessLpfLowB[1]  += deessLowAlpha  * (sBR - _deessLpfLowB[1]);
                            _deessLpfHighB[1] += deessHighAlpha * (sBR - _deessLpfHighB[1]);
                            float bBR = _deessLpfHighB[1] - _deessLpfLowB[1];
                            float aBBR = bBR < 0f ? -bBR : bBR;
                            if (aBBR > _deessEnvB[1]) _deessEnvB[1] = aBBR;
                            else                      _deessEnvB[1] *= 0.99955f;
                            float tBR;
                            if (_deessEnvB[1] > kDeessThreshold)
                            {
                                float over = _deessEnvB[1] - kDeessThreshold;
                                float deessTarget = kDeessThreshold + over / kDeessRatio;
                                tBR = deessTarget / _deessEnvB[1];
                                if (tBR < kDeessMinGain) tBR = kDeessMinGain;
                            }
                            else tBR = 1f;
                            if (tBR < _deessGainB[1]) _deessGainB[1] += (tBR - _deessGainB[1]) * 0.55f;
                            else                      _deessGainB[1] += (tBR - _deessGainB[1]) * 0.018f;
                            float hBR = sBR - _deessLpfLowB[1];
                            sBR = _deessLpfLowB[1] + hBR * _deessGainB[1];
                        }
                        sL += sBL * fadeOut;
                        sR += sBR * fadeOut;
                        posB++;
                    }
                    xfdDone++;
                    if (xfdDone >= xfdFr || (rawB != null && posB >= totB))
                    {
                        xfd = false;
                    }
                }

                if (_cfLpf != null && _cfLpf.Length >= 2)
                {
                    _cfLpf[0] += cfAlpha * (sL - _cfLpf[0]);
                    _cfLpf[1] += cfAlpha * (sR - _cfLpf[1]);
                    float cfL = (sL + _cfLpf[1] * kCrossfeedMix) * kCrossfeedComp;
                    float cfR = (sR + _cfLpf[0] * kCrossfeedMix) * kCrossfeedComp;
                    sL = cfL; sR = cfR;
                }

                float sLc = sL > 1.5f ? 1.5f : (sL < -1.5f ? -1.5f : sL);
                float sRc = sR > 1.5f ? 1.5f : (sR < -1.5f ? -1.5f : sR);
                sL = sLc - kSaturationAmount * sLc * sLc * sLc;
                sR = sRc - kSaturationAmount * sRc * sRc * sRc;

                if (_dcX1 != null && _dcY1 != null && _dcX1.Length >= 2)
                {
                    float yDcL = sL - _dcX1[0] + kDcBlockerR * _dcY1[0];
                    _dcX1[0] = sL; _dcY1[0] = yDcL; sL = yDcL;
                    float yDcR = sR - _dcX1[1] + kDcBlockerR * _dcY1[1];
                    _dcX1[1] = sR; _dcY1[1] = yDcR; sR = yDcR;
                }

                float aL = sL < 0f ? -sL : sL;
                float aR = sR < 0f ? -sR : sR;
                float fp = aL > aR ? aL : aR;
                if (fp > limEnv) limEnv = fp;
                else             limEnv *= 0.99975f;
                float target = limEnv > kPostLimiterCeil ? kPostLimiterCeil / limEnv : 1f;
                if (target < limGain) limGain += (target - limGain) * 0.55f;
                else                  limGain += (target - limGain) * 0.012f;
                int wIdx = limWP * 2;
                data[o]     = _limBuf[wIdx]     * limGain;
                data[o + 1] = _limBuf[wIdx + 1] * limGain;
                _limBuf[wIdx]     = sL;
                _limBuf[wIdx + 1] = sR;
                limWP = (limWP + 1) % limFrames;
                o += 2;
            }
            else
            {

                for (int c = 0; c < ch; c++)
                {
                    double s = raw[srcA++] * pre;
                    if (coeffs != null)
                    {
                        int baseSi = c * 10;
                        for (int b = 0; b < 10; b++)
                        {
                            int si = baseSi + b;
                            double y = b0[b]*s + b1[b]*x1[si] + b2[b]*x2[si] - a1[b]*y1[si] - a2[b]*y2[si];
                            x2[si] = x1[si]; x1[si] = s;
                            y2[si] = y1[si]; y1[si] = y;
                            s = y;
                        }
                    }
                    float sm = (float)s;
                    float am = sm < 0f ? -sm : sm;
                    if (am > limEnv) limEnv = am;
                    else             limEnv *= 0.99975f;
                    float target = limEnv > kPostLimiterCeil ? kPostLimiterCeil / limEnv : 1f;
                    if (target < limGain) limGain += (target - limGain) * 0.55f;
                    else                  limGain += (target - limGain) * 0.012f;
                    int wIdx = limWP * ch + c;
                    data[o++] = _limBuf[wIdx] * limGain;
                    _limBuf[wIdx] = sm;
                }
                limWP = (limWP + 1) % limFrames;
            }
        }

        for (; o < len; o++) data[o] = 0f;
        _rawReadPos = pos + framesNow;
        _limWritePos = limWP;
        _limEnvPeak  = limEnv;
        _limCurrGain = limGain;
        if (xfd)
        {
            _crossfadeFramesDone = xfdDone;
            _rawReadPosB = posB;
        }
        else if (_crossfading)
        {
            _crossfading = false;
            _rawSamplesB = null;
        }
    }

    [HideFromIl2Cpp]
    private void PcmSetPositionCallback(int newFrame)
    {
        if (newFrame < 0) newFrame = 0;
        _rawReadPos = newFrame;

        if (_eqX1 != null)
        {
            Array.Clear(_eqX1, 0, _eqX1.Length); Array.Clear(_eqX2, 0, _eqX2.Length);
            Array.Clear(_eqY1, 0, _eqY1.Length); Array.Clear(_eqY2, 0, _eqY2.Length);
        }
        if (_bassEnhState  != null) Array.Clear(_bassEnhState,  0, _bassEnhState.Length);
        if (_lcBassState   != null) Array.Clear(_lcBassState,   0, _lcBassState.Length);
        if (_deessLpfLow   != null) Array.Clear(_deessLpfLow,   0, _deessLpfLow.Length);
        if (_deessLpfHigh  != null) Array.Clear(_deessLpfHigh,  0, _deessLpfHigh.Length);
        if (_deessEnv      != null) Array.Clear(_deessEnv,      0, _deessEnv.Length);
        if (_deessGain     != null) for (int i = 0; i < _deessGain.Length; i++) _deessGain[i] = 1f;
        if (_cfLpf         != null) Array.Clear(_cfLpf,         0, _cfLpf.Length);
        if (_dcX1          != null) Array.Clear(_dcX1,          0, _dcX1.Length);
        if (_dcY1          != null) Array.Clear(_dcY1,          0, _dcY1.Length);
        _sideBassState = 0f;
        if (_limBuf        != null) Array.Clear(_limBuf,        0, _limBuf.Length);
        _limEnvPeak = 0f;
        _limCurrGain = 1f;
    }

    [HideFromIl2Cpp]
    private IEnumerator LoadAndPlay(int index, int gen)
    {
        _isLoading = true;
        StatusText = NocturneText.T("Загрузка...", "Loading...");

        float[]  samples   = null;
        int      channels  = 0;
        int      frequency = 0;
        string   error     = null;
        bool     done      = false;

        string path = _tracks[index];
        string ext  = Path.GetExtension(path).ToLowerInvariant();
        int    targetRate = AudioSettings.outputSampleRate;

        ThreadPool.QueueUserWorkItem(_ =>
        {
            try
            {
                if (_loadGen != gen) { done = true; return; }

                lock (_decodeGate)
                {
                    if (ext == ".ogg")
                        DecodeOgg(path, targetRate, out samples, out channels, out frequency);
                    else
                        DecodeNAudio(path, ext, targetRate, out samples, out channels, out frequency);
                }

                if (_loadGen != gen) { samples = null; done = true; return; }

            }
            catch (Exception e) { error = e.Message; }
            finally { done = true; }
        });

        while (!done) yield return null;

        if (_loadGen != gen) yield break;
        _isLoading = false;

        if (error != null || samples == null)
        {
            StatusText = NocturneText.T("Ошибка загрузки", "Load error");
            NocturnePlugin.Logger?.LogWarning((object)$"[Music] {Path.GetFileName(path)}: {error}");
            yield break;
        }

        var raw = new RawTrack { Samples = samples, Channels = channels, Frequency = frequency };
        _loadedRaw[index] = raw;

        if (_currentIndex == index)
        {
            PlayRawTrack(raw, path);
            PrefetchNeighbors(index);
        }

        this.StartCoroutine(DeferredLohCompact(gen));
    }

    [HideFromIl2Cpp]
    private IEnumerator DeferredLohCompact(int gen)
    {
        float until = Time.realtimeSinceStartup + 3.5f;
        while (Time.realtimeSinceStartup < until)
        {
            if (_loadGen != gen) yield break;
            yield return null;
        }
        if (_loadGen != gen || _isLoading) yield break;
        try
        {
            System.Runtime.GCSettings.LargeObjectHeapCompactionMode =
                System.Runtime.GCLargeObjectHeapCompactionMode.CompactOnce;
            GC.Collect();
        }
        catch { }
    }

    private static readonly object _decodeGate = new object();

    private static float[] DrainSamples(ISampleProvider provider)
    {
        const int Chunk = 1 << 20;
        var chunks = new List<float[]>();
        float[] cur = new float[Chunk];
        int curPos = 0;
        long total = 0;
        while (true)
        {
            if (curPos == cur.Length)
            {
                chunks.Add(cur);
                cur = new float[Chunk];
                curPos = 0;
            }
            int read = provider.Read(cur, curPos, cur.Length - curPos);
            if (read <= 0) break;
            curPos += read;
            total += read;
        }
        if (curPos > 0) chunks.Add(cur);

        float[] samples = new float[total];
        int dst = 0;
        for (int c = 0; c < chunks.Count; c++)
        {
            int len = (c == chunks.Count - 1) ? (samples.Length - dst) : chunks[c].Length;
            Array.Copy(chunks[c], 0, samples, dst, len);
            dst += len;
            chunks[c] = null;
        }
        return samples;
    }

    private static void DecodeNAudio(string path, string ext, int targetRate,
        out float[] samples, out int channels, out int frequency)
    {
        using WaveStream reader = ext == ".mp3"
            ? (WaveStream)new Mp3FileReader(path)
            : new WaveFileReader(path);

        channels  = reader.WaveFormat.Channels;
        frequency = targetRate;

        if (reader.TotalTime.TotalSeconds > 720)
            throw new InvalidOperationException(
                $"Track too long ({reader.TotalTime:mm\\:ss}), max 12 min");

        ISampleProvider provider = reader.ToSampleProvider();

        if (reader.WaveFormat.SampleRate != targetRate)
            provider = new WdlResamplingSampleProvider(provider, targetRate);

        samples = NormalizePeak(ApplySubsonicHpf(DrainSamples(provider), channels, targetRate));
    }

    private static void DecodeOgg(string path, int targetRate,
        out float[] samples, out int channels, out int frequency)
    {
        using var vorbis = new VorbisReader(path);
        channels  = vorbis.Channels;
        frequency = targetRate;

        if (vorbis.TotalTime.TotalSeconds > 720)
            throw new InvalidOperationException(
                $"Track too long ({vorbis.TotalTime:mm\\:ss}), max 12 min");

        ISampleProvider provider = new VorbisSampleProvider(vorbis);
        if (vorbis.SampleRate != targetRate)
            provider = new WdlResamplingSampleProvider(provider, targetRate);

        samples = NormalizePeak(ApplySubsonicHpf(DrainSamples(provider), channels, targetRate));
    }

    private static float[] ApplySubsonicHpf(float[] s, int channels, int sampleRate)
    {
        if (s.Length == 0 || channels < 1) return s;

        const double kCutoffHz = 25.0;
        const double kQ        = 0.70710678;

        double w0    = 2.0 * Math.PI * kCutoffHz / sampleRate;
        double cosw  = Math.Cos(w0);
        double sinw  = Math.Sin(w0);
        double alpha = sinw / (2.0 * kQ);

        double a0 = 1.0 + alpha;
        double b0 =  (1.0 + cosw) * 0.5 / a0;
        double b1 = -(1.0 + cosw)       / a0;
        double b2 =  (1.0 + cosw) * 0.5 / a0;
        double a1 = -2.0 * cosw         / a0;
        double a2 =  (1.0 - alpha)      / a0;

        var x1 = new double[channels];
        var x2 = new double[channels];
        var y1 = new double[channels];
        var y2 = new double[channels];

        for (int i = 0; i < s.Length; i++)
        {
            int ch = i % channels;
            double x = s[i];
            double y = b0 * x + b1 * x1[ch] + b2 * x2[ch] - a1 * y1[ch] - a2 * y2[ch];
            x2[ch] = x1[ch]; x1[ch] = x;
            y2[ch] = y1[ch]; y1[ch] = y;
            s[i] = (float)y;
        }
        return s;
    }

    private static float[] NormalizePeak(float[] s)
    {
        if (s.Length == 0) return s;
        double sum2 = 0.0;
        float peak = 0f;
        for (int i = 0; i < s.Length; i++)
        {
            sum2 += (double)s[i] * s[i];
            float a = s[i] < 0f ? -s[i] : s[i];
            if (a > peak) peak = a;
        }
        if (peak < 0.01f) return s;
        float rms  = (float)Math.Sqrt(sum2 / s.Length);
        const float kTargetRms = 0.18f;
        float gain = rms > 0.0001f ? kTargetRms / rms : 1f;
        if (gain < 1.0f) gain = 1.0f;
        float maxGain = 0.92f / peak;
        if (gain > maxGain) gain = maxGain;
        if (gain < 1.001f) return s;
        for (int i = 0; i < s.Length; i++) s[i] *= gain;
        return s;
    }

    private sealed class VorbisSampleProvider : ISampleProvider
    {
        private readonly VorbisReader _reader;
        public WaveFormat WaveFormat { get; }
        public VorbisSampleProvider(VorbisReader reader)
        {
            _reader = reader;
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(reader.SampleRate, reader.Channels);
        }
        public int Read(float[] buffer, int offset, int count)
            => _reader.ReadSamples(buffer, offset, count);
    }

    internal static string FormatTime(float s)
    {
        if (float.IsNaN(s) || s < 0f) return "0:00";
        int t = (int)s;
        return $"{t / 60}:{t % 60:D2}";
    }
}
