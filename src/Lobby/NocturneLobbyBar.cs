using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Nocturne;

public sealed class NocturneLobbyBar : MonoBehaviour
{
    private static Texture2D _panelTex;
    private static Texture2D _btnTex;
    private static Texture2D _chipTex;
    private static Texture2D _glowTex;
    private static Texture2D _lineTex;
    private static Texture2D _glossTex;
    private static Texture2D _moteTex;
    private static Texture2D _panelBakeTex;
    private static int _panelBakeW = -1, _panelBakeH = -1;
    private static readonly Texture2D[] _chipIconTex = new Texture2D[3];

    private GUIStyle _panelStyle;
    private GUIStyle _btnStyle;
    private GUIStyle _chipStyle;
    private GUIStyle _glowStyle;
    private GUIStyle _hostStyle;
    private GUIStyle _startStyle;
    private GUIStyle _chevronStyle;
    private GUIStyle _chipLabelStyle;
    private GUIStyle _chipValueStyle;
    private GUIStyle _iconStyle;
    private GUIStyle _glossStyle;
    private GUIStyle _lineStyle;
    private GUIStyle _moteStyle;
    private GUIStyle _panelBakeStyle;
    private readonly GUIStyle[] _chipIconStyle = new GUIStyle[3];
    private GUIStyle _monogramStyle;
    private bool _stylesReady;

    private float _visAlpha;

    private struct Mote { public float X, Y, Speed, Size, Phase, Bright; }
    private const int MoteCount = 14;
    private Mote[] _motes;
    private uint _moteSeed = 0x6D2B79F5u;

    private string _timeShown = string.Empty;
    private string _timePrev = string.Empty;
    private float _timeRoll = 1f;

    private float _btnHover;
    private float _shake;

    private GameStartManager _gsm;
    private float _nextGsmLookup;
    private readonly HashSet<SpriteRenderer> _suppressed = new HashSet<SpriteRenderer>();
    private readonly HashSet<Collider2D> _suppressedColliders = new HashSet<Collider2D>();
    private readonly HashSet<Behaviour> _suppressedTexts = new HashSet<Behaviour>();
    private PassiveButton _disabledButton;
    private int _suppressFrame = -1;
    private float _nextSuppressScan;
    private int _avatarFrame = -1;

    private float _fpsAccum;
    private int _fpsFrames;
    private float _fps = 30f;
    private float _lobbyEnteredAt = -1f;
    private float _chevronPhase;

    private bool _countingDown;
    private float _countdownEndAt;
    private int _lastCountSecond = -1;
    private const float CountdownSeconds = 5f;

    private GameObject _avatarHolder;
    private PoolablePlayer _avatarPlayer;
    private Camera _avatarCam;
    private RenderTexture _avatarRT;
    private Texture2D _avatarTex;
    private GUIStyle _avatarBoxStyle;
    private bool _avatarReady;
    private bool _avatarSetUp;
    private float _nextAvatarRender;
    private float _nextAvatarCheck;
    private float _avatarSettleUntil = -1f;
    private string _avatarSig = string.Empty;
    private const int AvatarLayer = 29;
    private static readonly Vector3 AvatarPos = new Vector3(0f, 500f, 0f);

    private OptionsMenuBehaviour _optionsMenu;
    private GameSettingMenu _gameSettingMenu;
    private PlayerCustomizationMenu _customizeMenu;
    private FriendsListUI _friendsMenu;
    private float _nextOptionsLookup;
    private float _nextSettingsLookup;
    private float _nextCustomizeLookup;
    private float _nextFriendsLookup;
    private int _settingsFrame = -1;
    private bool _settingsResult;

    private static bool Enabled => NocturneConfig.LobbyBar.Value;
    private static Color Accent => Patches.NocturneLobbyTheme.LobbyAccent(NocturneStyle.Current.Accent);

    private void Update()
    {
        _fpsAccum += Time.unscaledDeltaTime;
        _fpsFrames++;
        if (_fpsAccum >= 0.4f)
        {
            _fps = _fpsFrames / _fpsAccum;
            _fpsAccum = 0f;
            _fpsFrames = 0;
        }

        _chevronPhase += Time.unscaledDeltaTime * 2.2f;
        if (_chevronPhase > 1000f) _chevronPhase = 0f;

        if (LobbyBehaviour.Instance == null) { _lobbyEnteredAt = -1f; _visAlpha = 0f; return; }
        if (_lobbyEnteredAt < 0f) _lobbyEnteredAt = Time.unscaledTime;

        bool show = Enabled && !IsChatOpen() && !IsSettingsOpen();
        _visAlpha = Mathf.MoveTowards(_visAlpha, show ? 1f : 0f, Time.unscaledDeltaTime * 6f);
    }

    internal void DrawGui()
    {
        try
        {
            if (!Enabled || LobbyBehaviour.Instance == null) { RestoreVanilla(); return; }

            GameStartManager gsm = ResolveGsm();
            if (gsm == null) { RestoreVanilla(); return; }

            EnsureStyles();
            if (!_stylesReady) return;

            if (Time.frameCount != _suppressFrame)
            {
                _suppressFrame = Time.frameCount;
                SuppressVanilla(gsm);
            }

            if (_visAlpha <= 0.001f) return;

            Event ev = Event.current;
            if (ev != null && ev.type != EventType.Repaint && ev.type != EventType.MouseDown) return;

            DrawBar(gsm, _visAlpha);
        }
        catch { }
    }

    private void OnDisable() => RestoreVanilla();

    private static bool IsChatOpen()
    {
        if (LobbyBehaviour.Instance == null && ShipStatus.Instance == null) return false;
        try { return HudManager.Instance != null && HudManager.Instance.Chat != null && HudManager.Instance.Chat.IsOpenOrOpening; }
        catch { return false; }
    }

    private bool IsSettingsOpen()
    {
        if (Time.frameCount == _settingsFrame) return _settingsResult;
        _settingsFrame = Time.frameCount;
        _settingsResult = IsSettingsOpenUncached();
        return _settingsResult;
    }

    private bool IsSettingsOpenUncached()
    {
        try
        {
            if (_optionsMenu == null && Time.unscaledTime >= _nextOptionsLookup)
            {
                _nextOptionsLookup = Time.unscaledTime + 0.06f;
                _optionsMenu = Object.FindObjectOfType<OptionsMenuBehaviour>();
            }
            if (_optionsMenu != null && ((Component)_optionsMenu).gameObject.activeInHierarchy) return true;
        }
        catch { }

        try
        {
            if (_gameSettingMenu == null && Time.unscaledTime >= _nextSettingsLookup)
            {
                _nextSettingsLookup = Time.unscaledTime + 0.06f;
                _gameSettingMenu = Object.FindObjectOfType<GameSettingMenu>();
            }
            if (_gameSettingMenu != null && ((Component)_gameSettingMenu).gameObject.activeInHierarchy) return true;
        }
        catch { }

        try
        {
            if (_customizeMenu == null && Time.unscaledTime >= _nextCustomizeLookup)
            {
                _nextCustomizeLookup = Time.unscaledTime + 0.06f;
                _customizeMenu = Object.FindObjectOfType<PlayerCustomizationMenu>();
            }
            if (_customizeMenu != null && ((Component)_customizeMenu).gameObject.activeInHierarchy) return true;
        }
        catch { }

        try
        {
            if (_friendsMenu == null && Time.unscaledTime >= _nextFriendsLookup)
            {
                _nextFriendsLookup = Time.unscaledTime + 0.06f;
                _friendsMenu = Object.FindObjectOfType<FriendsListUI>();
            }
            if (_friendsMenu != null && ((Component)_friendsMenu).gameObject.activeInHierarchy) return true;
        }
        catch { }

        return false;
    }

    private GameStartManager ResolveGsm()
    {
        if (_gsm != null) return _gsm;
        if (Time.unscaledTime < _nextGsmLookup) return null;
        _nextGsmLookup = Time.unscaledTime + 0.5f;
        try
        {
            _gsm = DestroyableSingleton<GameStartManager>.InstanceExists
                ? DestroyableSingleton<GameStartManager>.Instance
                : Object.FindObjectOfType<GameStartManager>();
        }
        catch { _gsm = null; }
        return _gsm;
    }

    private void SuppressVanilla(GameStartManager gsm)
    {
        try
        {
            PassiveButton btn = gsm.StartButton;
            if (btn == null) return;

            ReassertSuppressed();
            if (Time.unscaledTime < _nextSuppressScan) return;
            _nextSuppressScan = Time.unscaledTime + 0.25f;

            DisableUnder(((Component)gsm).gameObject);

            try { if (((Behaviour)btn).enabled) { ((Behaviour)btn).enabled = false; _disabledButton = btn; } } catch { }
            try
            {
                Collider2D col = ((Component)btn).GetComponent<Collider2D>();
                if (col != null && col.enabled) { col.enabled = false; _suppressedColliders.Add(col); }
            }
            catch { }

            try
            {
                foreach (TMP_Text t in ((Component)gsm).GetComponentsInChildren<TMP_Text>(true))
                {
                    if (t != null && ((Behaviour)t).enabled) { ((Behaviour)t).enabled = false; _suppressedTexts.Add(t); }
                }
            }
            catch { }
        }
        catch { }
    }

    private void ReassertSuppressed()
    {
        foreach (SpriteRenderer r in _suppressed) { try { if (r != null && r.enabled) r.enabled = false; } catch { } }
        foreach (Behaviour b in _suppressedTexts) { try { if (b != null && b.enabled) b.enabled = false; } catch { } }
        foreach (Collider2D c in _suppressedColliders) { try { if (c != null && c.enabled) c.enabled = false; } catch { } }
        if (_disabledButton != null) { try { if (((Behaviour)_disabledButton).enabled) ((Behaviour)_disabledButton).enabled = false; } catch { } }
    }

    private void DisableUnder(GameObject root)
    {
        if (root == null) return;
        foreach (SpriteRenderer r in root.GetComponentsInChildren<SpriteRenderer>(true))
            if (r != null && r.enabled) { r.enabled = false; _suppressed.Add(r); }
    }

    private void RestoreVanilla()
    {
        foreach (SpriteRenderer r in _suppressed) { try { if (r != null) r.enabled = true; } catch { } }
        _suppressed.Clear();
        foreach (Collider2D c in _suppressedColliders) { try { if (c != null) c.enabled = true; } catch { } }
        _suppressedColliders.Clear();
        foreach (Behaviour b in _suppressedTexts) { try { if (b != null) b.enabled = true; } catch { } }
        _suppressedTexts.Clear();
        if (_disabledButton != null) { try { ((Behaviour)_disabledButton).enabled = true; } catch { } _disabledButton = null; }
        _countingDown = false;
        _nextSuppressScan = 0f;
    }

    private string _hostTextC;
    private float _hostWC;
    private int _hostFontC = -1;

    private int _chipFrame = -1;
    private string _pingText = "-- ms", _fpsText = "-- FPS";
    private Color _pingCol = Color.white, _fpsCol = Color.white, _timeCol = Color.white;

    private void DrawBar(GameStartManager gsm, float a)
    {
        if (Time.frameCount != _avatarFrame)
        {
            _avatarFrame = Time.frameCount;
            UpdateAvatar();
        }
        float s = Mathf.Clamp(Screen.height / 1080f, 0.6f, 1.6f);
        float barW = 560f * s;
        float barH = 196f * s;
        float x = (Screen.width - barW) * 0.5f;
        float y = Screen.height - barH - 46f * s + (1f - a) * 14f * s;

        _shake = Mathf.MoveTowards(_shake, 0f, Time.unscaledDeltaTime * 6f);
        if (_shake > 0.001f) x += Mathf.Sin(Time.unscaledTime * 60f) * _shake * 10f * s;

        bool amHost = false;
        try { amHost = AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost; } catch { }

        Color prev = GUI.color;
        Color accent = Accent;

        float glowPulseBar = 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 1.8f);
        GUI.color = new Color(accent.r, accent.g, accent.b, 0.16f * glowPulseBar * a);
        GUI.Box(new Rect(x - 9f * s, y - 9f * s, barW + 18f * s, barH + 18f * s), GUIContent.none, _glowStyle);
        GUI.color = new Color(0f, 0f, 0f, 0.45f * a);
        GUI.Box(new Rect(x + 4f * s, y + 6f * s, barW, barH), GUIContent.none, _panelStyle);
        GUI.color = new Color(accent.r, accent.g, accent.b, 0.85f * a);
        GUI.Box(new Rect(x - 2.5f * s, y - 2.5f * s, barW + 5f * s, barH + 5f * s), GUIContent.none, _glowStyle);
        GUI.color = new Color(1f, 1f, 1f, a);
        GUI.Box(new Rect(x, y, barW, barH), GUIContent.none, _panelBakeStyle);

        DrawMotes(new Rect(x + 6f * s, y + 6f * s, barW - 12f * s, barH - 12f * s), s, a);

        GUI.color = new Color(accent.r, accent.g, accent.b, 0.06f * a);
        GUI.Label(new Rect(x + barW - 70f * s, y + barH - 64f * s, 60f * s, 60f * s), "◆", _monogramStyle);
        GUI.color = new Color(1f, 1f, 1f, a);

        string hostText = NocturneText.T("ХОСТ: ", "HOST: ") + ResolveHostName();
        float medH = 52f * s;
        float gapIcon = 10f * s;
        if (hostText != _hostTextC || _hostFontC != _hostStyle.fontSize)
        {
            _hostFontC = _hostStyle.fontSize;
            try { _hostWC = _hostStyle.CalcSize(new GUIContent(hostText)).x; _hostTextC = hostText; }
            catch { _hostWC = barW * 0.5f; }
        }
        float textW = _hostWC;
        float groupW = medH + gapIcon + textW;
        float gx = x + (barW - groupW) * 0.5f;
        float hostCenterY = y + 25f * s;
        DrawMedallion(new Rect(gx, hostCenterY - medH * 0.5f, medH, medH), LocalPlayerColor(), a);
        GUI.color = new Color(1f, 1f, 1f, a);
        GUI.Label(new Rect(gx + medH + gapIcon, hostCenterY - 16f * s, textW + 14f * s, 32f * s), hostText, _hostStyle);

        DrawDivider(new Rect(x + 24f * s, y + 46f * s, barW - 48f * s, 1f * s), a);

        float btnH = 70f * s;
        float btnY = y + 52f * s;
        float chevW = 64f * s;
        float btnX = x + chevW + 10f * s;
        float btnW = barW - (chevW + 10f * s) * 2f;
        Rect startRect = new Rect(btnX, btnY, btnW, btnH);

        bool over = startRect.Contains(Event.current != null ? Event.current.mousePosition : new Vector2(-1f, -1f));
        _btnHover = Mathf.MoveTowards(_btnHover, over ? 1f : 0f, Time.unscaledDeltaTime * 8f);
        float grow = _btnHover * 4f * s;
        startRect = new Rect(startRect.x - grow, startRect.y - grow * 0.5f, startRect.width + grow * 2f, startRect.height + grow);

        float glowPulse = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 2.4f);
        float glowAmt = (0.35f + 0.30f * _btnHover) * glowPulse;
        GUI.color = new Color(accent.r, accent.g, accent.b, glowAmt * a);
        GUI.Box(new Rect(startRect.x - 10f * s, startRect.y - 8f * s, startRect.width + 20f * s, startRect.height + 16f * s), GUIContent.none, _glowStyle);
        GUI.color = new Color(1f, 1f, 1f, a);

        Event bev = Event.current;
        bool startClicked = bev != null && bev.type == EventType.MouseDown && bev.button == 0 && startRect.Contains(bev.mousePosition);

        Color prevBg = GUI.backgroundColor;
        GUI.backgroundColor = new Color(accent.r, accent.g, accent.b, 1f);

        if (amHost)
        {
            float now = Time.unscaledTime;
            if (_countingDown && now >= _countdownEndAt) { _countingDown = false; DoStart(gsm); }

            string startLabel;
            if (_countingDown)
            {
                int rem = Mathf.CeilToInt(Mathf.Max(0f, _countdownEndAt - now));
                if (rem != _lastCountSecond) { _lastCountSecond = rem; if (rem > 0) PlayCountdownSound(gsm); }
                startLabel = NocturneText.T("ОТМЕНА  (", "CANCEL  (") + rem + ")";
            }
            else
            {
                _lastCountSecond = -1;
                startLabel = NocturneText.T("СТАРТ", "START");
            }

            GUI.Box(startRect, startLabel, _startStyle);
            if (startClicked)
            {
                bev.Use();
                PlayClickSound(gsm);
                if (_countingDown)
                {
                    _countingDown = false;
                    _shake = 1f;
                    NocturneToast.Push(NocturneText.T("Старт", "Start"), NocturneText.T("Отсчёт отменён.", "Countdown cancelled."), 2.2f, NocturneNotifyKind.Info);
                }
                else
                {
                    _countingDown = true;
                    _countdownEndAt = now + CountdownSeconds;
                }
            }
        }
        else
        {
            string waitLabel = NocturneText.T("ОЖИДАНИЕ", "WAITING");
            int rem = -1;
            if (Patches.NocturneStartCounter.TryGet(out int rpcSec)) rem = rpcSec;
            else { try { if (gsm != null && (int)gsm.startState == 1) rem = Mathf.CeilToInt(Mathf.Max(0f, gsm.countDownTimer)); } catch { } }
            if (rem >= 0) waitLabel = NocturneText.T("СТАРТ  (", "START  (") + rem + ")";
            GUI.Box(startRect, waitLabel, _startStyle);
        }

        GUI.backgroundColor = prevBg;

        DrawButtonShine(startRect, s, a);
        DrawChevrons(new Rect(x + 6f * s, btnY, chevW, btnH), true, s, a);
        DrawChevrons(new Rect(x + barW - chevW - 6f * s, btnY, chevW, btnH), false, s, a);

        DrawDivider(new Rect(x + 24f * s, y + 128f * s, barW - 48f * s, 1f * s), a);

        float chipsY = y + 134f * s;
        float chipH = 50f * s;
        float gap = 10f * s;
        float chipW = (barW - 16f * s - gap * 2f) / 3f;
        float cx = x + 8f * s;

        if (Time.frameCount != _chipFrame)
        {
            _chipFrame = Time.frameCount;
            int ping = ReadPing();
            _pingText = ping >= 0 ? ping + " ms" : "-- ms";
            _pingCol = PingColor(ping);
            int fps = Mathf.RoundToInt(_fps);
            _fpsText = fps + " FPS";
            _fpsCol = FpsColor(fps);
            float remain = LobbyRemainingSeconds();
            AdvanceTimeRoll(LobbyTimeText());
            if (remain <= 60f)
            {
                float blink = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f);
                _timeCol = Color.Lerp(new Color(0.98f, 0.32f, 0.30f, 1f), new Color(1f, 0.78f, 0.40f, 1f), blink);
            }
            else _timeCol = Color.Lerp(accent, Color.white, 0.18f);
        }

        DrawTimeChip(new Rect(cx, chipsY, chipW, chipH), 0, NocturneText.T("ВРЕМЯ", "LOBBY TIME"), s, _timeCol, a);
        DrawChip(new Rect(cx + (chipW + gap), chipsY, chipW, chipH), 1, "PING", _pingText, s, _pingCol, a);
        DrawChip(new Rect(cx + (chipW + gap) * 2f, chipsY, chipW, chipH), 2, "FPS", _fpsText, s, _fpsCol, a);

        GUI.color = prev;
    }

    private void DrawButtonShine(Rect btn, float s, float a)
    {
        float inset = 4f * s;
        Rect inner = new Rect(btn.x + inset, btn.y, btn.width - inset * 2f, btn.height);
        GUI.BeginGroup(inner);
        GUI.color = new Color(1f, 1f, 1f, 0.10f * a);
        GUI.Box(new Rect(0f, 4f * s, inner.width, inner.height * 0.5f), GUIContent.none, _glossStyle);

        if (NocturneConfig.LobbyAnims.Value)
        {
            float t = Mathf.Repeat(Time.unscaledTime * 0.55f, 1.8f) / 1.8f;
            float bandW = inner.width * 0.16f;
            float bandX = t * (inner.width + bandW * 2f) - bandW;
            GUI.color = new Color(1f, 1f, 1f, 0.12f * a);
            GUI.Box(new Rect(bandX, 5f * s, bandW, inner.height - 10f * s), GUIContent.none, _glowStyle);
        }
        GUI.EndGroup();
        GUI.color = new Color(1f, 1f, 1f, a);
    }

    private void DrawDivider(Rect r, float a)
    {
        GUI.color = new Color(1f, 1f, 1f, 0.10f * a);
        GUI.Box(r, GUIContent.none, _lineStyle);
        GUI.color = new Color(1f, 1f, 1f, a);
    }

    private void DrawMotes(Rect area, float s, float a)
    {
        if (_moteStyle == null || !NocturneConfig.LobbyAnims.Value) return;
        if (Event.current != null && Event.current.type != EventType.Repaint) return;
        if (_motes == null)
        {
            _motes = new Mote[MoteCount];
            uint seed = 0x9E3779B9u;
            for (int i = 0; i < MoteCount; i++)
                _motes[i] = new Mote
                {
                    X = Rand(ref seed),
                    Y = Rand(ref seed),
                    Speed = 0.02f + Rand(ref seed) * 0.045f,
                    Size = 3.2f + Rand(ref seed) * 4.2f,
                    Phase = Rand(ref seed) * 6.28f,
                    Bright = 0.45f + Rand(ref seed) * 0.55f,
                };
        }

        float dt = Time.unscaledDeltaTime;
        GUI.BeginGroup(area);
        for (int i = 0; i < _motes.Length; i++)
        {
            Mote m = _motes[i];
            m.Y -= m.Speed * dt;
            if (m.Y < -0.06f)
            {
                m.Y += 1.12f;
                m.X = Rand(ref _moteSeed);
                m.Size = 3.2f + Rand(ref _moteSeed) * 4.2f;
                m.Speed = 0.02f + Rand(ref _moteSeed) * 0.045f;
                m.Phase = Rand(ref _moteSeed) * 6.28f;
            }
            _motes[i] = m;

            float twinkle = 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 1.4f + m.Phase);
            float alpha = 0.32f * m.Bright * twinkle * a;
            float px = m.X * area.width;
            float py = m.Y * area.height;
            float sz = m.Size * s;
            GUI.color = new Color(1f, 0.88f, 0.66f, alpha);
            GUI.Box(new Rect(px - sz * 0.7f, py - sz * 0.7f, sz * 1.4f, sz * 1.4f), GUIContent.none, _moteStyle);
        }
        GUI.EndGroup();
        GUI.color = new Color(1f, 1f, 1f, a);
    }

    private static float Rand(ref uint state)
    {
        state ^= state << 13;
        state ^= state >> 17;
        state ^= state << 5;
        return (state & 0xFFFFFF) / (float)0x1000000;
    }

    private void ClearAvatarName()
    {
        try
        {
            if (_avatarPlayer != null && _avatarPlayer.cosmetics != null && _avatarPlayer.cosmetics.nameText != null)
                ((TMP_Text)_avatarPlayer.cosmetics.nameText).text = string.Empty;
        }
        catch { }
    }

    private static void SetLayerRecursive(GameObject go, int layer)
    {
        if (go == null) return;
        go.layer = layer;
        for (int i = 0; i < go.transform.childCount; i++)
            SetLayerRecursive(go.transform.GetChild(i).gameObject, layer);
    }

    private void DrawMedallion(Rect r, Color body, float a)
    {
        if (_avatarReady && _avatarTex != null && _avatarBoxStyle != null)
        {
            GUI.color = new Color(1f, 1f, 1f, a);
            GUI.Box(new Rect(r.x - r.width * 0.06f, r.y - r.height * 0.42f, r.width * 1.12f, r.height * 1.26f), GUIContent.none, _avatarBoxStyle);
        }
        else DrawCrewIcon(new Rect(r.x + r.width * 0.14f, r.y + r.height * 0.12f, r.width * 0.72f, r.height * 0.80f), body);

        GUI.color = new Color(1f, 1f, 1f, a);
    }

    private static int ReadPing()
    {
        if (AmongUsClient.Instance == null) return -1;
        return ((InnerNet.InnerNetClient)AmongUsClient.Instance).Ping;
    }

    private static Color PingColor(int ping)
    {
        if (ping < 0) return new Color(0.70f, 0.72f, 0.78f, 1f);
        if (ping <= 90) return new Color(0.40f, 0.92f, 0.50f, 1f);
        if (ping <= 180) return new Color(0.98f, 0.82f, 0.32f, 1f);
        return new Color(0.98f, 0.36f, 0.36f, 1f);
    }

    private static Color FpsColor(int fps)
    {
        if (fps >= 50) return new Color(0.40f, 0.92f, 0.50f, 1f);
        if (fps >= 28) return new Color(0.98f, 0.82f, 0.32f, 1f);
        return new Color(0.98f, 0.36f, 0.36f, 1f);
    }

    private void DrawChevrons(Rect area, bool left, float s, float a)
    {
        Color baseCol = Accent;
        for (int i = 0; i < 3; i++)
        {
            float t = Mathf.Repeat(_chevronPhase - i * 0.18f, 1.2f);
            float ca = 0.35f + 0.55f * Mathf.Clamp01(1f - Mathf.Abs(t - 0.3f) * 2.2f);
            GUI.color = new Color(baseCol.r, baseCol.g, baseCol.b, ca * a);
            float w = area.width / 3f;
            float cxp = left ? area.x + (2 - i) * w : area.x + i * w;
            GUI.Label(new Rect(cxp, area.y, w, area.height), left ? "◄" : "►", _chevronStyle);
        }
        GUI.color = new Color(1f, 1f, 1f, a);
    }

    private bool EnsureAvatar()
    {
        if (_avatarReady && _avatarPlayer != null && _avatarCam != null && _avatarRT != null && _avatarTex != null) return true;

        try
        {
            HudManager hud = DestroyableSingleton<HudManager>.Instance;
            if (hud == null || hud.IntroPrefab == null || hud.IntroPrefab.PlayerPrefab == null) return false;

            PlayerControl lp = PlayerControl.LocalPlayer;
            if (lp == null || lp.Data == null) return false;

            if (_avatarHolder == null)
            {
                _avatarHolder = new GameObject("NocturneAvatarHolder");
                Object.DontDestroyOnLoad(_avatarHolder);
                _avatarHolder.transform.position = AvatarPos;
            }

            if (_avatarPlayer == null)
            {
                _avatarPlayer = Object.Instantiate<PoolablePlayer>(hud.IntroPrefab.PlayerPrefab, _avatarHolder.transform);
                _avatarPlayer.transform.localPosition = Vector3.zero;
                _avatarPlayer.transform.localScale = Vector3.one;
                SetLayerRecursive(_avatarPlayer.gameObject, AvatarLayer);
                ClearAvatarName();
                _avatarSetUp = false;
            }

            if (_avatarRT == null) { _avatarRT = new RenderTexture(192, 224, 16, RenderTextureFormat.ARGB32); _avatarRT.Create(); }
            if (_avatarTex == null) { _avatarTex = new Texture2D(192, 224, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear }; Object.DontDestroyOnLoad(_avatarTex); }

            if (_avatarCam == null)
            {
                GameObject camGo = new GameObject("NocturneAvatarCam");
                Object.DontDestroyOnLoad(camGo);
                _avatarCam = camGo.AddComponent<Camera>();
                _avatarCam.orthographic = true;
                _avatarCam.orthographicSize = 0.95f;
                _avatarCam.clearFlags = CameraClearFlags.SolidColor;
                _avatarCam.backgroundColor = new Color(0f, 0f, 0f, 0f);
                _avatarCam.cullingMask = 1 << AvatarLayer;
                _avatarCam.nearClipPlane = 0.05f;
                _avatarCam.farClipPlane = 60f;
                _avatarCam.depth = -90;
                _avatarCam.allowHDR = false;
                _avatarCam.allowMSAA = false;
                _avatarCam.enabled = false;
                _avatarCam.targetTexture = _avatarRT;
                camGo.transform.position = AvatarPos + new Vector3(0f, 0.45f, -10f);
                camGo.transform.rotation = Quaternion.identity;
            }

            _avatarBoxStyle ??= new GUIStyle(GUIStyle.none);
            _avatarBoxStyle.normal.background = _avatarTex;

            _avatarReady = true;
            return true;
        }
        catch { _avatarReady = false; return false; }
    }

    private void UpdateAvatar()
    {
        try
        {
            if (!EnsureAvatar()) return;

            float now = Time.unscaledTime;

            if (now >= _nextAvatarCheck)
            {
                _nextAvatarCheck = now + 0.3f;
                PlayerControl host = ResolveHostPlayer();
                if (host != null && host.Data != null)
                {
                    string sig = OutfitSignature(host);
                    if (!_avatarSetUp || sig != _avatarSig)
                    {
                        _avatarSig = sig;
                        try
                        {
                            _avatarPlayer.UpdateFromEitherPlayerDataOrCache(host.Data, (PlayerOutfitType)0, 0, false, (System.Action)null);
                            ClearAvatarName();
                            _avatarSetUp = true;
                        }
                        catch { }
                        _avatarSettleUntil = now + 1.2f;
                    }
                }
            }

            if (now >= _avatarSettleUntil) return;
            if (now < _nextAvatarRender) return;
            _nextAvatarRender = now + 0.25f;
            _avatarHolder.transform.position = AvatarPos;

            RenderTexture prevRT = RenderTexture.active;
            _avatarCam.Render();
            RenderTexture.active = _avatarRT;
            _avatarTex.ReadPixels(new Rect(0f, 0f, _avatarRT.width, _avatarRT.height), 0, 0, false);
            _avatarTex.Apply(false);
            RenderTexture.active = prevRT;
        }
        catch { _avatarReady = false; }
    }

    private void DrawCrewIcon(Rect r, Color body)
    {
        if (_avatarReady && _avatarTex != null && _avatarBoxStyle != null)
        {
            GUI.Box(new Rect(r.x - r.width * 0.5f, r.y - r.height * 0.70f, r.width * 2f, r.height * 2f), GUIContent.none, _avatarBoxStyle);
            return;
        }

        Color prev = GUI.color;
        GUI.color = body;
        GUI.Box(r, GUIContent.none, _iconStyle);
        GUI.color = new Color(body.r * 0.62f, body.g * 0.62f, body.b * 0.62f, 1f);
        GUI.Box(new Rect(r.x, r.y + r.height * 0.6f, r.width, r.height * 0.4f), GUIContent.none, _iconStyle);
        GUI.color = new Color(0.70f, 0.88f, 0.98f, 1f);
        GUI.Box(new Rect(r.x + r.width * 0.28f, r.y + r.height * 0.16f, r.width * 0.64f, r.height * 0.30f), GUIContent.none, _iconStyle);
        GUI.color = prev;
    }

    private static Color _lpColorC = new Color(0.30f, 0.62f, 1f, 1f);
    private static float _lpColorAt = -99f;

    private static Color LocalPlayerColor()
    {
        float now = Time.unscaledTime;
        if (now - _lpColorAt < 0.5f) return _lpColorC;
        _lpColorAt = now;
        try
        {
            PlayerControl lp = ResolveHostPlayer();
            if (lp != null && lp.Data != null)
            {
                int cid = lp.Data.DefaultOutfit.ColorId;
                if (Palette.PlayerColors != null && cid >= 0 && cid < Palette.PlayerColors.Length)
                {
                    Color32 c = Palette.PlayerColors[cid];
                    return _lpColorC = new Color(c.r / 255f, c.g / 255f, c.b / 255f, 1f);
                }
            }
        }
        catch { }
        return _lpColorC;
    }

    private void DrawChip(Rect rect, int kind, string label, string value, float s, Color valueColor, float a)
    {
        GUI.color = new Color(1f, 1f, 1f, a);
        GUI.Box(rect, GUIContent.none, _chipStyle);

        Color iconCol = valueColor; iconCol.a = a;
        ChipIcon(new Rect(rect.x + 8f * s, rect.y + (rect.height - 22f * s) * 0.5f, 22f * s, 22f * s), kind, iconCol);

        GUI.color = new Color(0.60f, 0.65f, 0.72f, a);
        GUI.Label(new Rect(rect.x, rect.y + 8f * s, rect.width, 13f * s), label, _chipLabelStyle);

        Color valCol = valueColor; valCol.a = a;
        GUI.color = valCol;
        GUI.Label(new Rect(rect.x, rect.y + 20f * s, rect.width, 22f * s), value, _chipValueStyle);
        GUI.color = new Color(1f, 1f, 1f, a);
    }

    private void ChipIcon(Rect box, int kind, Color col)
    {
        if (kind < 0 || kind > 2) return;
        GUIStyle st = _chipIconStyle[kind];
        if (st == null) return;
        GUI.color = col;
        GUI.Box(box, GUIContent.none, st);
        GUI.color = Color.white;
    }

    private static void PlayClickSound(GameStartManager gsm)
    {
        try
        {
            AudioClip clip = gsm != null && gsm.StartButton != null ? gsm.StartButton.ClickSound : null;
            if (clip != null && SoundManager.Instance != null)
                SoundManager.Instance.PlaySound(clip, false, 1f, (UnityEngine.Audio.AudioMixerGroup)null);
        }
        catch { }
    }

    private static void PlayCountdownSound(GameStartManager gsm)
    {
        try
        {
            AudioClip clip = gsm != null ? gsm.gameStartSound : null;
            if (clip != null && SoundManager.Instance != null)
                SoundManager.Instance.PlaySound(clip, false, 1f, (UnityEngine.Audio.AudioMixerGroup)null);
        }
        catch { }
    }

    private void DoStart(GameStartManager gsm)
    {
        try
        {
            if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost || LobbyBehaviour.Instance == null) return;

            PlayClickSound(gsm);
            try { gsm.MinPlayers = 1; } catch { }
            try { Patches.StartControl.UnlockStartButton(gsm); } catch { }

            bool ok = false;
            try { ok = Patches.StartControl.TryInstantStart(gsm); } catch { }
            if (!ok) gsm.BeginGame();
        }
        catch { }
    }

    private static PlayerControl ResolveHostPlayer()
    {
        try
        {
            var client = AmongUsClient.Instance;
            if (client == null) return PlayerControl.LocalPlayer;

            int hostId = client.HostId;
            var all = PlayerControl.AllPlayerControls;
            if (all != null)
                for (int i = 0; i < all.Count; i++)
                {
                    PlayerControl pc = all[i];
                    if (pc != null && pc.OwnerId == hostId) return pc;
                }
        }
        catch { }
        return PlayerControl.LocalPlayer;
    }

    private static bool LocalIsHost() => AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost;

    private static string OutfitSignature(PlayerControl pc)
    {
        try
        {
            if (pc == null || pc.Data == null) return "none";
            var o = pc.Data.DefaultOutfit;
            return $"{pc.OwnerId}|{o.ColorId}|{o.HatId}|{o.SkinId}|{o.VisorId}|{o.PetId}";
        }
        catch { return "err"; }
    }

    private static string _hostNameC;
    private static float _hostNameAt = -99f;

    private static string ResolveHostName()
    {
        float now = Time.unscaledTime;
        if (_hostNameC != null && now - _hostNameAt < 0.5f) return _hostNameC;
        _hostNameAt = now;
        try
        {
            PlayerControl host = ResolveHostPlayer();
            if (host != null && host.Data != null && !string.IsNullOrWhiteSpace(host.Data.PlayerName))
            {
                string raw = NocturneNameColor.Strip(host.Data.PlayerName);
                return _hostNameC = LocalIsHost() ? raw + NocturneText.T(" (Вы!)", " (You!)") : raw;
            }
        }
        catch { }
        return _hostNameC = NocturneText.T("Хост", "Host");
    }

    private const float LobbyMaxSeconds = 600f;

    private float LobbyRemainingSeconds()
    {
        float elapsed = _lobbyEnteredAt < 0f ? 0f : Mathf.Max(0f, Time.unscaledTime - _lobbyEnteredAt);
        return Mathf.Clamp(LobbyMaxSeconds - elapsed, 0f, LobbyMaxSeconds);
    }

    private string LobbyTimeText()
    {
        int total = Mathf.CeilToInt(LobbyRemainingSeconds());
        return $"{total / 60:00}:{total % 60:00}";
    }

    private void AdvanceTimeRoll(string value)
    {
        if (_timeShown.Length == 0) { _timeShown = value; _timePrev = value; _timeRoll = 1f; return; }
        if (value != _timeShown) { _timePrev = _timeShown; _timeShown = value; _timeRoll = 0f; }
        if (_timeRoll < 1f) _timeRoll = Mathf.Min(1f, _timeRoll + Time.unscaledDeltaTime * 2.4f);
    }

    private void DrawTimeChip(Rect rect, int kind, string label, float s, Color valueColor, float a)
    {
        GUI.color = new Color(1f, 1f, 1f, a);
        GUI.Box(rect, GUIContent.none, _chipStyle);

        Color iconCol = valueColor; iconCol.a = a;
        ChipIcon(new Rect(rect.x + 8f * s, rect.y + (rect.height - 22f * s) * 0.5f, 22f * s, 22f * s), kind, iconCol);

        GUI.color = new Color(0.60f, 0.65f, 0.72f, a);
        GUI.Label(new Rect(rect.x, rect.y + 8f * s, rect.width, 13f * s), label, _chipLabelStyle);

        float vy = rect.y + 20f * s;
        float vh = 22f * s;
        float chW = 11f * s;
        string cur = _timeShown;
        string prv = _timePrev;
        float vx = rect.x + rect.width * 0.5f - cur.Length * chW * 0.5f;
        bool rolling = NocturneConfig.LobbyAnims.Value && _timeRoll < 1f && prv.Length == cur.Length;
        float p = Mathf.Clamp01(_timeRoll);
        float ease = p * p * p * (p * (p * 6f - 15f) + 10f);

        Color valCol = valueColor; valCol.a = a;
        for (int i = 0; i < cur.Length; i++)
        {
            float cxp = vx + i * chW;
            char nc = cur[i];
            bool ch = rolling && i < prv.Length && prv[i] != nc;
            if (ch)
            {
                GUI.color = new Color(valCol.r, valCol.g, valCol.b, a * (1f - ease));
                GUI.Label(new Rect(cxp, vy - vh * ease, chW, vh), prv[i].ToString(), _chipValueStyle);
                GUI.color = new Color(valCol.r, valCol.g, valCol.b, a * ease);
                GUI.Label(new Rect(cxp, vy + vh * (1f - ease), chW, vh), nc.ToString(), _chipValueStyle);
            }
            else
            {
                GUI.color = valCol;
                GUI.Label(new Rect(cxp, vy, chW, vh), nc.ToString(), _chipValueStyle);
            }
        }
        GUI.color = new Color(1f, 1f, 1f, a);
    }


    private void EnsureStyles()
    {
        if (_stylesReady && _panelTex != null && _btnTex != null && _chipTex != null && _glowTex != null
            && _glossTex != null && _lineTex != null && _moteTex != null && _panelBakeTex != null && _chipIconTex[0] != null)
            return;

        _stylesReady = false;
        try
        {
            float sc = Mathf.Clamp(Screen.height / 1080f, 0.6f, 1.6f);

            if (_panelTex == null) _panelTex = RoundedTex(new Color(0.035f, 0.035f, 0.037f, 0.98f), 20);
            if (_btnTex == null) _btnTex = RoundedTex(new Color(1f, 1f, 1f, 1f), 16);
            if (_chipTex == null) _chipTex = RoundedTex(new Color(0.09f, 0.09f, 0.095f, 0.96f), 14);
            if (_glowTex == null) _glowTex = RoundedTex(new Color(1f, 1f, 1f, 1f), 16);
            if (_lineTex == null) _lineTex = Solid(new Color(1f, 1f, 1f, 1f));
            if (_glossTex == null) _glossTex = RoundedVFadeTex(new Color(1f, 1f, 1f, 1f), true, 18);
            if (_moteTex == null) _moteTex = SoftDotTex(16);

            int pw = Mathf.RoundToInt(560f * sc), ph = Mathf.RoundToInt(196f * sc);
            if (_panelBakeTex == null || _panelBakeW != pw || _panelBakeH != ph)
            {
                _panelBakeW = pw; _panelBakeH = ph;
                _panelBakeTex = BakePanel(pw, ph, sc);
            }
            for (int k = 0; k < 3; k++)
                if (_chipIconTex[k] == null) _chipIconTex[k] = BakeChipIcon(k, 48);

            _panelStyle = NewBoxStyle(_panelTex, 22);
            _panelBakeStyle = new GUIStyle(GUIStyle.none) { normal = { background = _panelBakeTex } };
            _glowStyle = NewBoxStyle(_glowTex, 18);
            _chipStyle = NewBoxStyle(_chipTex, 16);
            _glossStyle = NewBoxStyle(_glossTex, 18);
            _lineStyle = NewBoxStyle(_lineTex, 0);
            _moteStyle = new GUIStyle(GUIStyle.none) { normal = { background = _moteTex } };
            for (int k = 0; k < 3; k++)
                _chipIconStyle[k] = new GUIStyle(GUIStyle.none) { normal = { background = _chipIconTex[k] } };

            _monogramStyle = new GUIStyle { alignment = TextAnchor.MiddleCenter, fontSize = 40, fontStyle = FontStyle.Bold };
            _monogramStyle.normal.textColor = Color.white;

            _iconStyle = new GUIStyle(GUIStyle.none) { normal = { background = _glowTex } };
            _btnStyle = NewBoxStyle(_btnTex, 18);
            _startStyle = new GUIStyle(_btnStyle) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(34f * sc), fontStyle = FontStyle.Bold };
            _startStyle.normal.textColor = Color.white;
            _startStyle.hover.textColor = Color.white;
            _startStyle.active.textColor = new Color(0.92f, 0.96f, 1f, 1f);
            _startStyle.hover.background = _btnTex;
            _startStyle.active.background = _btnTex;

            _hostStyle = new GUIStyle { alignment = TextAnchor.MiddleLeft, fontSize = Mathf.RoundToInt(21f * sc), fontStyle = FontStyle.Bold };
            _hostStyle.normal.textColor = new Color(0.95f, 0.96f, 0.98f, 1f);

            _chevronStyle = new GUIStyle { alignment = TextAnchor.MiddleCenter, fontSize = 26, fontStyle = FontStyle.Bold };
            _chevronStyle.normal.textColor = Color.white;

            _chipLabelStyle = new GUIStyle { alignment = TextAnchor.MiddleCenter, fontSize = 11, fontStyle = FontStyle.Bold };
            _chipLabelStyle.normal.textColor = Color.white;

            _chipValueStyle = new GUIStyle { alignment = TextAnchor.MiddleCenter, fontSize = 16, fontStyle = FontStyle.Bold };
            _chipValueStyle.normal.textColor = Color.white;

            _stylesReady = true;
        }
        catch { _stylesReady = false; }
    }

    private static GUIStyle NewBoxStyle(Texture2D tex, int border)
    {
        GUIStyle st = new GUIStyle(GUIStyle.none);
        st.normal.background = tex;
        st.border.left = border;
        st.border.right = border;
        st.border.top = border;
        st.border.bottom = border;
        return st;
    }

    private static Texture2D Solid(Color c)
    {
        Texture2D t = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        t.SetPixel(0, 0, c);
        t.Apply();
        Object.DontDestroyOnLoad(t);
        return t;
    }

    private static Texture2D RoundedVFadeTex(Color c, bool opaqueAtTop, int radius)
    {
        int size = radius * 2 + 12;
        Texture2D t = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < size; y++)
        {
            float f = (float)y / (size - 1);
            float fade = opaqueAtTop ? f : (1f - f);
            fade *= fade;
            for (int x = 0; x < size; x++)
            {
                float dx = 0f, dy = 0f;
                if (x < radius) dx = radius - x;
                else if (x > size - 1 - radius) dx = x - (size - 1 - radius);
                if (y < radius) dy = radius - y;
                else if (y > size - 1 - radius) dy = y - (size - 1 - radius);
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float mask = Mathf.Clamp01(radius + 0.5f - dist);
                Color cc = c;
                cc.a *= fade * mask;
                t.SetPixel(x, y, cc);
            }
        }
        t.Apply();
        Object.DontDestroyOnLoad(t);
        return t;
    }

    private static Texture2D SoftDotTex(int size)
    {
        float r = size * 0.5f;
        Texture2D t = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dist = Mathf.Sqrt((x - r + 0.5f) * (x - r + 0.5f) + (y - r + 0.5f) * (y - r + 0.5f)) / r;
                float aa = Mathf.Clamp01(1f - dist);
                aa *= aa;
                t.SetPixel(x, y, new Color(1f, 1f, 1f, aa));
            }
        t.Apply();
        Object.DontDestroyOnLoad(t);
        return t;
    }

    private static Texture2D BakePanel(int w, int h, float s)
    {
        Texture2D t = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        Color[] px = new Color[w * h];

        const float r = 20f;
        Color dark = new Color(0.035f, 0.035f, 0.037f, 0.98f);
        Color warm = new Color(0.15f, 0.14f, 0.13f, 1f);

        float gi = 6f * s;
        float gradTop = gi, gradBot = h - gi;
        float gInset = 5f * s;
        float glX0 = gInset, glX1 = w - gInset;
        float glY0 = 4f * s, glY1 = 4f * s + h * 0.40f;
        float shY0 = h * 0.60f, shY1 = h * 0.60f + h * 0.36f;

        for (int yimg = 0; yimg < h; yimg++)
            for (int xp = 0; xp < w; xp++)
            {
                float fx = xp + 0.5f, fy = yimg + 0.5f;

                float dx = 0f, dy = 0f;
                if (fx < r) dx = r - fx; else if (fx > w - r) dx = fx - (w - r);
                if (fy < r) dy = r - fy; else if (fy > h - r) dy = fy - (h - r);
                float mask = Mathf.Clamp01(r + 0.5f - Mathf.Sqrt(dx * dx + dy * dy));
                if (mask <= 0f) { px[(h - 1 - yimg) * w + xp] = new Color(0f, 0f, 0f, 0f); continue; }

                float oR = dark.r, oG = dark.g, oB = dark.b, oA = dark.a * mask;

                if (fx >= gi && fx <= w - gi && fy >= gradTop && fy <= gradBot)
                {
                    float gf = 1f - (fy - gradTop) / Mathf.Max(1f, gradBot - gradTop - 1f);
                    float sa = Mathf.Clamp01(gf) * 0.5f * mask;
                    if (sa > 0f) { oR = warm.r * sa + oR * (1f - sa); oG = warm.g * sa + oG * (1f - sa); oB = warm.b * sa + oB * (1f - sa); oA = sa + oA * (1f - sa); }
                }

                if (fx >= glX0 && fx <= glX1 && fy >= glY0 && fy <= glY1)
                {
                    float f = 1f - (fy - glY0) / Mathf.Max(1f, glY1 - glY0 - 1f);
                    float sa = f * f * 0.07f * mask;
                    if (sa > 0f) { oR = sa + oR * (1f - sa); oG = sa + oG * (1f - sa); oB = sa + oB * (1f - sa); oA = sa + oA * (1f - sa); }
                }

                if (fx >= glX0 && fx <= glX1 && fy >= shY0 && fy <= shY1)
                {
                    float f = (fy - shY0) / Mathf.Max(1f, shY1 - shY0 - 1f);
                    float sa = f * f * 0.30f * mask;
                    if (sa > 0f) { oR *= 1f - sa; oG *= 1f - sa; oB *= 1f - sa; oA = sa + oA * (1f - sa); }
                }

                px[(h - 1 - yimg) * w + xp] = new Color(oR, oG, oB, oA);
            }

        t.SetPixels(px);
        t.Apply();
        Object.DontDestroyOnLoad(t);
        return t;
    }

    private static Texture2D BakeChipIcon(int kind, int size)
    {
        Texture2D t = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        Color[] px = new Color[size * size];
        for (int i = 0; i < px.Length; i++) px[i] = new Color(1f, 1f, 1f, 0f);
        float u = size, cx = size * 0.5f, cy = size * 0.5f;

        if (kind == 0)
        {
            float outer = u * 0.43f;
            float inner = outer - Mathf.Max(1.5f, u * 0.11f);
            float st = Mathf.Max(1.4f, u * 0.09f);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float fx = x + 0.5f, fy = y + 0.5f;
                    float dist = Mathf.Sqrt((fx - cx) * (fx - cx) + (fy - cy) * (fy - cy));
                    float aa = Mathf.Clamp01(outer - dist + 0.75f) * Mathf.Clamp01(dist - inner + 0.75f);
                    if (Mathf.Abs(fx - cx) <= st * 0.5f && fy >= cy && fy <= cy + u * 0.26f) aa = 1f;
                    if (Mathf.Abs(fy - cy) <= st * 0.5f && fx >= cx && fx <= cx + u * 0.22f) aa = 1f;
                    if (aa > 0f) px[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(aa));
                }
        }
        else if (kind == 1)
        {
            float bw = u * 0.15f, gp = u * 0.11f, baseY = cy - u * 0.30f;
            float x0 = cx - (bw * 1.5f + gp);
            for (int i = 0; i < 3; i++)
                FillRectPx(px, size, x0 + i * (bw + gp), baseY, bw, u * (0.22f + i * 0.16f));
        }
        else
        {
            float bw = u * 0.13f, gp = u * 0.09f, baseY = cy - u * 0.30f;
            float[] hs = { 0.24f, 0.44f, 0.16f, 0.36f };
            float x0 = cx - (bw * 2f + gp * 1.5f);
            for (int i = 0; i < 4; i++)
                FillRectPx(px, size, x0 + i * (bw + gp), baseY, bw, u * hs[i]);
        }

        t.SetPixels(px);
        t.Apply();
        Object.DontDestroyOnLoad(t);
        return t;
    }

    private static void FillRectPx(Color[] px, int size, float rx, float ry, float rw, float rh)
    {
        int x0 = Mathf.Clamp(Mathf.RoundToInt(rx), 0, size);
        int x1 = Mathf.Clamp(Mathf.RoundToInt(rx + rw), 0, size);
        int y0 = Mathf.Clamp(Mathf.RoundToInt(ry), 0, size);
        int y1 = Mathf.Clamp(Mathf.RoundToInt(ry + rh), 0, size);
        for (int y = y0; y < y1; y++)
            for (int x = x0; x < x1; x++)
                px[y * size + x] = new Color(1f, 1f, 1f, 1f);
    }

    private static Texture2D RoundedTex(Color fill, int radius)
    {
        int size = radius * 2 + 6;
        Texture2D t = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = 0f, dy = 0f;
                if (x < radius) dx = radius - x;
                else if (x > size - 1 - radius) dx = x - (size - 1 - radius);
                if (y < radius) dy = radius - y;
                else if (y > size - 1 - radius) dy = y - (size - 1 - radius);
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                Color c = fill;
                c.a *= Mathf.Clamp01(radius + 0.5f - dist);
                t.SetPixel(x, y, c);
            }
        t.Apply();
        Object.DontDestroyOnLoad(t);
        return t;
    }
}
