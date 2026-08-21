using System;
using System.Collections.Generic;
using AmongUs.GameOptions;
using BepInEx.Configuration;
using Il2CppInterop.Runtime.Attributes;
using InnerNet;
using UnityEngine;

namespace Nocturne;

public sealed class NocturneMenu : MonoBehaviour
{
    private const int WindowId = 770731;
    private const float FullH = 610f;
    private const float MinW = 620f;
    private const float HeaderH = 64f;
    private const float SidebarW = 200f;
    private const float TabH = 44f;
    private const float RowH = 34f;
    private const float FooterH = 40f;

    private struct TabDef
    {
        public NocturneIcon Icon;
        public string Ru;
        public string En;
        public TabDef(NocturneIcon icon, string ru, string en) { Icon = icon; Ru = ru; En = en; }
        public string Name => NocturneText.T(Ru, En);
    }

    private static readonly TabDef[] Tabs =
    {
        new TabDef(NocturneIcon.Home, "Главная", "Home"),
        new TabDef(NocturneIcon.Star, "Удобства", "QoL"),
        new TabDef(NocturneIcon.Door, "Лобби", "Lobby"),
        new TabDef(NocturneIcon.Eye, "Визуальные", "Visual"),
        new TabDef(NocturneIcon.Crew, "Игроки", "Players"),
        new TabDef(NocturneIcon.Bolt, "Читы", "Cheats"),
        new TabDef(NocturneIcon.Shield, "Защита", "Guard"),
        new TabDef(NocturneIcon.Wifi, "Спуф", "Spoof"),
        new TabDef(NocturneIcon.Tune, "Хост", "Host"),
    };

    internal static bool Opened;
    internal static bool Typing;
    internal static bool ToggleRequest;

    private bool _open;
    private bool _collapsed;
    private int _tab;
    private int _slider;
    private readonly HashSet<int> _roleOpen = new HashSet<int>();
    private string _presetName = "";
    private float _scroll;
    private float _scrollPending;
    private float _maxScroll;
    private float _viewH;
    private bool _scrollGrab;
    private float _scrollLastY;
    private bool _scrollMoved;
    private float _h = FullH;
    private int _resize;
    private Rect _window = new Rect(300f, 110f, 720f, FullH);
    private bool _mobileCentered;

    private bool _built;
    private int _themeBuilt = -1;
    private Texture2D _gradient;
    private Texture2D _gloss;
    private GUIStyle _brand;
    private GUIStyle _verPill;
    private GUIStyle _status;
    private GUIStyle _statVal;
    private GUIStyle _secTitle;
    private GUIStyle _favStar;
    private float _statAt = -99f;
    private string _statCode = "-", _statPing = "-", _statPlayers = "-";
    private Color _statPingCol = Color.gray;
    private string _footerStr;
    private int _footerSig = int.MinValue;
    private GUIStyle _tabStyle;
    private GUIStyle _cardTitle;
    private GUIStyle _rowLabel;
    private GUIStyle _value;
    private GUIStyle _valueC;
    private GUIStyle _muted;
    private GUIStyle _footer;
    private GUIStyle _btnLabel;
    private GUIStyle _centerMuted;
    private GUIStyle _arrow;
    private GUIStyle _smallBtn;
    private GUIStyle _rowName;
    private GUIStyle _rowInfo;
    private GUIStyle _invisible;
    private GUIStyle _keyBadge;
    private GUIStyle _star;

    private readonly List<ClientData> _guardClients = new List<ClientData>();
    private readonly List<PlayerControl> _roleClients = new List<PlayerControl>();
    private readonly List<PlayerControl> _immPlayers = new List<PlayerControl>();
    private readonly List<QuickItem> _searchHits = new List<QuickItem>();
    private string _cloneText = "";
    private string _netText = "";
    private byte _netSrc = 255;
    private readonly List<PlayerControl> _netPick = new List<PlayerControl>();
    private string _nickText = "";
    private string _platformText = "";
    private string _fcText;
    private string _lobbySearch;
    private int _mapSel;
    private string _chatSend = "";
    private string _qcChain = "";
    private bool _qcWordListOpen;
    private string _qcTemplate = "";
    private string _qcTemplateB = "";
    private bool _qcDupSelf = true;
    private int _frameSystemIdx;
    private int _frameValue;

    private static PlayerControl PlayerByIdText(string idText)
    {
        if (!byte.TryParse((idText ?? "").Trim(), out byte pid)) return null;
        foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
        {
            if (pc != null && pc.PlayerId == pid) return pc;
        }
        return null;
    }
    private string _search = "";
    private string _searchDone;
    private string _textFocus;
    private bool _wasTyping;

    private bool _rebinding;
    private ConfigEntry<KeyCode> _rebindTarget;
    internal static bool Rebinding;

    private float _openAt = -1f;
    private float _closeAt = -1f;
    private float _fade = 1f;
    private int _press;
    private Texture2D _crystalTex;
    private string _crystalAccent;
    private GUIStyle _crystalStyle;
    private float _pressAt;

    private int _tabDir = 1;
    private float _tabAnimAt = -1f;
    private readonly int[] _subTab = new int[12];
    private int _cardGrp = -1;
    private readonly Dictionary<object, float> _pillAnim = new Dictionary<object, float>();
    private readonly Dictionary<object, float> _pillVel = new Dictionary<object, float>();

    private static Vector2 M => Event.current != null ? Event.current.mousePosition : new Vector2(-1f, -1f);

    private static int _mobile = -1;
    private static bool Mobile
    {
        get
        {
            if (_mobile < 0) { try { _mobile = Application.isMobilePlatform ? 1 : 0; } catch { _mobile = 0; } }
            return _mobile == 1;
        }
    }

    public void Update()
    {
        bool menuKey = NocturneConfig.MenuKey.Value != KeyCode.None && Input.GetKeyDown(NocturneConfig.MenuKey.Value);
        bool fallbackKey = Input.GetKeyDown(KeyCode.Delete) && !Typing && !NocturneChatWindow.Typing && !Patches.NocturneTextFocus.Any;
        if (!_rebinding && (menuKey || fallbackKey))
        {
            if (_open && _closeAt < 0f) RequestClose();
            else Open();
        }
        if (ToggleRequest)
        {
            ToggleRequest = false;
            if (!_rebinding) { if (_open && _closeAt < 0f) RequestClose(); else Open(); }
        }
        if (!_rebinding && _open && _closeAt < 0f && Input.GetKeyDown(KeyCode.Escape))
        {
            if (!string.IsNullOrEmpty(_search)) { _search = string.Empty; _scroll = 0f; _scrollPending = 0f; }
            else RequestClose();
        }
        Opened = _open;

        bool typing = _open && _closeAt < 0f && _textFocus != null;
        Typing = typing;
        if (typing) SetMoveable(false);
        else if (_wasTyping) SetMoveable(true);
        _wasTyping = typing;
    }

    private void Open()
    {
        _open = true;
        _openAt = Time.unscaledTime;
        _closeAt = -1f;
        _mobileCentered = false;
        try { Input.simulateMouseWithTouches = true; } catch { }
    }

    private void RequestClose()
    {
        if (_open && _closeAt < 0f) _closeAt = Time.unscaledTime;
    }

    private void SetTab(int nt)
    {
        if (nt == _tab) return;
        _tabDir = nt > _tab ? 1 : -1;
        _tab = nt;
        _tabAnimAt = Time.unscaledTime;
        _scroll = 0f;
        _scrollPending = 0f;
    }

    private static float SmoothSat(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }

    private static int RectId(Rect r) => (Mathf.RoundToInt(r.x) * 73856093) ^ (Mathf.RoundToInt(r.y) * 19349663) ^ (Mathf.RoundToInt(r.width) * 83492791);

    private float PressK(int id)
    {
        if (_press != id) return 1f;
        float t = (Time.unscaledTime - _pressAt) / 0.13f;
        if (t >= 1f) { _press = 0; return 1f; }
        return Mathf.Lerp(0.97f, 1f, SmoothSat(t));
    }

    private void Pressed(Rect r, int id)
    {
        Event e = Event.current;
        if (e != null && e.type == EventType.MouseDown && r.Contains(e.mousePosition))
        {
            _press = id;
            _pressAt = Time.unscaledTime;
        }
    }

    private static void SetMoveable(bool value)
    {
        try { if (PlayerControl.LocalPlayer != null) PlayerControl.LocalPlayer.moveable = value; }
        catch { }
    }

    internal void DrawGui()
    {
        if (!_open) return;
        Build();
        HandleRebindCapture();

        float baseS = Mobile
            ? Mathf.Clamp(Screen.height / 850f, 0.8f, 2.4f)
            : Mathf.Clamp(Screen.height / 1080f, 0.72f, 2.1f);
        float userS = NocturneConfig.MenuScale.Value;
        float s = Mathf.Clamp(baseS * userS, 0.5f, 3.2f);
        Matrix4x4 prevMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
        float vw = Screen.width / s;
        float vh = Screen.height / s;

        float now = Time.unscaledTime;
        float fade = 1f;
        if (_closeAt >= 0f)
        {
            float t = Mathf.Clamp01((now - _closeAt) / 0.2f);
            fade = 1f - SmoothSat(t);
            if (t >= 1f) { _open = false; _closeAt = -1f; _openAt = -1f; _collapsed = false; GUI.matrix = prevMatrix; return; }
        }
        else if (_openAt >= 0f)
        {
            float t = Mathf.Clamp01((now - _openAt) / 0.26f);
            fade = SmoothSat(t);
            if (t >= 1f) _openAt = -1f;
        }
        _fade = fade;

        if (!(NocturneConfig.LiteMenu != null && NocturneConfig.LiteMenu.Value))
            NocturneStyle.Fill(new Rect(0f, 0f, vw, vh), new Color(0.02f, 0.02f, 0.022f, 0.34f * fade));

        if (Mobile) CenterWindow(vw, vh);
        else
        {
            ClampWindow(vw, vh);
            if (fade >= 1f) HandleResize(vw, vh);
        }

        Color prevColor = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, fade);

        GUI.BeginGroup(_window);
        DrawWindow(WindowId);
        GUI.EndGroup();
        HandleWindowDrag(vw, vh);

        GUI.color = prevColor;
        GUI.matrix = prevMatrix;
    }

    private int _winDrag;
    private Vector2 _winDragOff;

    private void HandleWindowDrag(float vw, float vh)
    {
        Event e = Event.current;
        if (e == null || _resize != 0) return;
        var header = new Rect(_window.x, _window.y, _window.width, HeaderH);
        if (e.type == EventType.MouseDown && e.button == 0 && header.Contains(e.mousePosition))
        {
            _winDrag = 1;
            _winDragOff = new Vector2(e.mousePosition.x - _window.x, e.mousePosition.y - _window.y);
            e.Use();
        }
        else if (_winDrag == 1 && e.type == EventType.MouseDrag)
        {
            _window.x = Mathf.Clamp(e.mousePosition.x - _winDragOff.x, 0f, Mathf.Max(0f, vw - _window.width));
            _window.y = Mathf.Clamp(e.mousePosition.y - _winDragOff.y, 0f, Mathf.Max(0f, vh - _window.height));
            e.Use();
        }
        else if (_winDrag == 1 && e.type == EventType.MouseUp)
        {
            _winDrag = 0;
            e.Use();
        }
    }

    private const int RzL = 1, RzR = 2, RzT = 4, RzB = 8;

    private void HandleResize(float vw, float vh)
    {
        Event e = Event.current;
        if (_collapsed || e == null) return;

        if (e.type == EventType.MouseDown && e.button == 0)
        {
            Vector2 m = e.mousePosition;
            bool inX = m.x >= _window.x - 8f && m.x <= _window.xMax + 8f;
            bool inY = m.y >= _window.y - 8f && m.y <= _window.yMax + 8f;
            int dir = 0;
            if (inY && Mathf.Abs(m.x - _window.x) <= 8f) dir |= RzL;
            if (inY && Mathf.Abs(m.x - _window.xMax) <= 8f) dir |= RzR;
            if (inX && Mathf.Abs(m.y - _window.y) <= 8f) dir |= RzT;
            if (inX && Mathf.Abs(m.y - _window.yMax) <= 8f) dir |= RzB;
            if (dir != 0) { _resize = dir; e.Use(); }
        }
        else if (_resize != 0 && e.type == EventType.MouseDrag)
        {
            Vector2 m = e.mousePosition;
            if ((_resize & RzR) != 0) _window.width = Mathf.Clamp(m.x - _window.x, MinW, Mathf.Max(MinW, vw - _window.x));
            if ((_resize & RzB) != 0) _h = Mathf.Clamp(m.y - _window.y, FullH, Mathf.Max(FullH, vh - _window.y));
            if ((_resize & RzL) != 0)
            {
                float right = _window.x + _window.width;
                float nx = Mathf.Clamp(m.x, 0f, right - MinW);
                _window.x = nx;
                _window.width = right - nx;
            }
            if ((_resize & RzT) != 0)
            {
                float bottom = _window.y + _h;
                float ny = Mathf.Clamp(m.y, 0f, bottom - FullH);
                _window.y = ny;
                _h = bottom - ny;
            }
            e.Use();
        }
        else if (_resize != 0 && e.type == EventType.MouseUp)
        {
            _resize = 0;
            e.Use();
        }
    }

    private void CenterWindow(float vw, float vh)
    {
        float w = Mathf.Clamp(720f, MinW, vw);
        float bodyH = Mathf.Clamp(vh * 0.9f, Mathf.Min(320f, vh), FullH);
        _window.width = w;
        _h = bodyH;
        _window.height = _collapsed ? HeaderH : bodyH;
        if (!_mobileCentered)
        {
            _mobileCentered = true;
            _window.x = Mathf.Max(0f, (vw - w) * 0.5f);
            _window.y = Mathf.Max(0f, (vh - _window.height) * 0.5f);
        }
        _window.x = Mathf.Clamp(_window.x, 0f, Mathf.Max(0f, vw - _window.width));
        _window.y = Mathf.Clamp(_window.y, 0f, Mathf.Max(0f, vh - _window.height));
    }

    private void ClampWindow(float vw, float vh)
    {
        _window.width = Mathf.Clamp(_window.width, MinW, Mathf.Max(MinW, vw));
        _h = Mathf.Clamp(_h, FullH, Mathf.Max(FullH, vh));
        _window.height = _collapsed ? HeaderH : _h;
        _window.x = Mathf.Clamp(_window.x, 0f, Mathf.Max(0f, vw - _window.width));
        _window.y = Mathf.Clamp(_window.y, 0f, Mathf.Max(0f, vh - _window.height));
    }

    private void DrawWindow(int id)
    {
        NocturnePalette p = NocturneStyle.Current;
        NocturneStyle.Lite = NocturneConfig.LiteMenu != null && NocturneConfig.LiteMenu.Value;
        bool lite = NocturneStyle.Lite;
        if (lite) { GUI.skin.button.fontSize = 12; GUI.skin.toggle.fontSize = GUI.skin.label.fontSize = 13; }
        float w = _window.width;
        float h = _window.height;
        GUI.color = new Color(1f, 1f, 1f, _fade);

        if (lite || _collapsed) NocturneStyle.FillRounded(new Rect(0f, 0f, w, h), lite ? Rgb(20, 21, 25) : p.Window, 16);
        else NocturneStyle.DrawTex(new Rect(0f, 0f, w, h), _gradient);
        if (!lite) NocturneStyle.FillRounded(new Rect(0f, 0f, w, h), A(p.Accent, 0.04f), 16);
        NocturneStyle.StrokeRounded(new Rect(0f, 0f, w, h), lite ? A(Color.white, 0.20f) : A(p.Accent, 0.22f), 16, 1);
        if (!_collapsed && !lite)
            NocturneStyle.DrawTex(new Rect(2f, 2f, w - 4f, HeaderH - 3f), _gloss);

        DrawHeader(w, p);
        if (_collapsed) return;

        Rect area;
        if (lite)
        {
            const float sideW = 150f;
            float colTop = HeaderH + 8f;
            DrawSearchBar(new Rect(sideW + 8f, colTop, w - sideW - 8f - 16f, 26f));
            NocturneStyle.Fill(new Rect(sideW, colTop, 1f, h - colTop - FooterH), A(Color.white, 0.12f));

            float ty = colTop;
            int nn = Tabs.Length + 2;
            for (int i = 0; i < nn; i++)
            {
                string nm = i < Tabs.Length ? Tabs[i].Name : (i == Tabs.Length ? NocturneText.T("Настройки", "Settings") : NocturneText.T("Избранное", "Favorites"));
                Color bg = GUI.backgroundColor;
                if (_tab == i) GUI.backgroundColor = new Color(0.5f, 0.5f, 0.5f);
                if (GUI.Button(new Rect(8f, ty, sideW - 12f, 28f), nm) && _tab != i)
                { _tab = i; _scroll = 0f; _scrollPending = 0f; }
                GUI.backgroundColor = bg;
                ty += 30f;
            }
            area = new Rect(sideW + 8f, colTop + 32f, w - sideW - 8f - 16f, h - (colTop + 32f) - FooterH);
        }
        else
        {
            const float railW = 54f;
            float colTop = HeaderH + 8f;
            float railBottom = h - FooterH;
            DrawRail(new Rect(0f, colTop, railW, railBottom - colTop));
            NocturneStyle.Fill(new Rect(railW, colTop, 1f, railBottom - colTop), A(Color.white, 0.08f));

            float cxL = railW + 12f;
            string secName = _tab == FavTab ? NocturneText.T("Избранное", "Favorites") : _tab < Tabs.Length ? Tabs[_tab].Name : NocturneText.T("Настройки", "Settings");
            _secTitle.normal.textColor = p.Text;
            GUI.Label(new Rect(cxL, colTop - 1f, 168f, 28f), Up(secName), _secTitle);
            DrawCollapseAll(new Rect(w - 16f - 30f, colTop, 30f, 26f));
            DrawSearchBar(new Rect(cxL + 172f, colTop, w - (cxL + 172f) - 16f - 34f, 26f));
            DrawStatusStrip(new Rect(cxL, colTop + 34f, w - cxL - 16f, 32f));
            area = new Rect(cxL, colTop + 72f, w - cxL - 16f, h - colTop - 72f - FooterH);
        }
        if (Event.current != null && Event.current.type == EventType.Repaint)
        {
            _scroll = Mathf.Clamp(_scroll + _scrollPending, 0f, _maxScroll);
            _scrollPending = 0f;
        }
        if (Event.current != null && Event.current.type == EventType.ScrollWheel && area.Contains(Event.current.mousePosition))
        {
            _scrollPending += Event.current.delta.y * 20f;
            Event.current.Use();
        }

        Event se = Event.current;
        if (se != null)
        {
            if (se.type == EventType.MouseDown)
            {
                _scrollGrab = area.Contains(se.mousePosition);
                _scrollLastY = se.mousePosition.y;
                _scrollMoved = false;
            }
            else if (se.type == EventType.MouseDrag && _scrollGrab && _slider == 0)
            {
                float dy = se.mousePosition.y - _scrollLastY;
                _scrollLastY = se.mousePosition.y;
                if (!_scrollMoved && Mathf.Abs(dy) > 6f) _scrollMoved = true;
                if (_scrollMoved) { _scrollPending -= dy; se.Use(); }
            }
            else if (se.type == EventType.MouseUp)
            {
                if (_scrollGrab && _scrollMoved) se.Use();
                _scrollGrab = false;
                _scrollMoved = false;
            }
        }

        float slide = 0f;
        float cAlpha = 1f;
        if (_tabAnimAt >= 0f)
        {
            float ct = Mathf.Clamp01((Time.unscaledTime - _tabAnimAt) / 0.17f);
            float ce = SmoothSat(ct);
            slide = (1f - ce) * 34f * _tabDir;
            cAlpha = Mathf.Clamp01(ce * 1.2f);
            if (ct >= 1f) _tabAnimAt = -1f;
        }

        Color cPrev = GUI.color;
        GUI.color = new Color(cPrev.r, cPrev.g, cPrev.b, cPrev.a * cAlpha);
        GUI.BeginGroup(area);
        _inScroll = true;
        if (!lite) DrawStars(area.width, area.height);
        float x = 4f;
        float cx = x + slide;
        float cw = area.width - 20f;
        float startY = 6f - _scroll;
        float cy = startY;
        _viewH = area.height;
        var localArea = new Rect(slide, 0f, area.width, area.height);
        _cardGrp = -1;
        if (!string.IsNullOrEmpty(_search))
            DrawSearch(cx, ref cy, cw);
        else if (_tab == FavTab)
            DrawFavorites(cx, ref cy, cw);
        else switch (_tab)
        {
            case 0: DrawHome(cx, ref cy, cw); break;
            case 1: DrawQoL(cx, ref cy, cw); break;
            case 2: DrawLobbyTab(cx, ref cy, cw); break;
            case 3: DrawVisual(cx, ref cy, cw); break;
            case 4: DrawPlayers(cx, ref cy, cw); break;
            case 5: try { DrawCheats(cx, ref cy, cw); } catch { } break;
            case 6: DrawGuard(cx, ref cy, cw); break;
            case 7: DrawNet(cx, ref cy, cw); break;
            case 8: DrawHostTab(cx, ref cy, cw); break;
            case 9: DrawSettings(cx, ref cy, cw); break;
            default: DrawEmpty(localArea, NocturneIcon.Star, NocturneText.T("Раздел в разработке", "Section in progress")); break;
        }
        _inScroll = false;
        GUI.EndGroup();
        GUI.color = cPrev;

        float contentH = cy - startY;
        float maxScroll = Mathf.Max(0f, contentH - (area.height - 12f));
        _maxScroll = maxScroll;
        if (maxScroll > 1f)
        {
            float trackH = area.height - 8f;
            float thumbH = Mathf.Max(28f, trackH * (area.height / Mathf.Max(contentH, 1f)));
            float thumbY = area.y + 4f + (trackH - thumbH) * (_scroll / maxScroll);
            NocturneStyle.FillRounded(new Rect(area.xMax - 5f, area.y + 4f, 3f, trackH), A(Color.white, 0.05f), 1);
            NocturneStyle.FillRounded(new Rect(area.xMax - 5f, thumbY, 3f, thumbH), A(p.Accent, 0.55f), 1);
        }

        float fy = h - FooterH + 4f;
        int fsig = (int)NocturneConfig.MenuKey.Value * 397 + (int)NocturneConfig.CopyCodeKey.Value * 17 + (NocturneText.IsRussian ? 3 : 0) + NocturneConfig.ThemeIndex.Value * 100003;
        if (_footerStr == null || fsig != _footerSig)
        {
            _footerSig = fsig;
            string acc = Hex(p.Accent);
            _footerStr = $"{NocturneText.T("Меню", "Menu")} <color=#{acc}><b>{KeyName(NocturneConfig.MenuKey)}</b></color>     {NocturneText.T("Код лобби", "Lobby code")} <color=#{acc}><b>{KeyName(NocturneConfig.CopyCodeKey)}</b></color>     {NocturneText.T("Закрыть", "Close")} <color=#{acc}><b>Esc</b></color>";
        }
        GUI.Label(new Rect(x, fy, cw, FooterH - 8f), _footerStr, _footer);

        Color grip = A(p.Muted, 0.5f);
        for (int gi = 0; gi < 3; gi++)
            for (int gj = 0; gj <= gi; gj++)
                NocturneStyle.Fill(new Rect(w - 8f - gj * 4f, h - 8f - gi * 4f, 2f, 2f), grip);
    }

    [HideFromIl2Cpp]
    private void DrawHeader(float w, NocturnePalette p)
    {
        if (NocturneStyle.Lite)
        {
            _brand.normal.textColor = p.Text;
            GUI.Label(new Rect(16f, 15f, w - 100f, 34f), "Nocturne  v" + NocturnePlugin.PluginVersion, _brand);
            var lminB = new Rect(w - 68f, 20f, 26f, 24f);
            var lcloseB = new Rect(w - 38f, 20f, 26f, 24f);
            if (WindowButton(lminB, NocturneIcon.Minimize, p)) _collapsed = !_collapsed;
            if (WindowButton(lcloseB, NocturneIcon.Close, p)) RequestClose();
            if (!_collapsed)
                NocturneStyle.Fill(new Rect(0f, HeaderH - 1f, w, 1f), A(Color.white, 0.12f));
            return;
        }

        DrawLogo(new Rect(20f, 15f, 34f, 34f), p);

        _brand.wordWrap = false;
        var brandR = new Rect(67f, 15f, 180f, 34f);
        GUI.Label(brandR, "Nocturne", _brand);
        float sh = Mathf.Repeat(Time.unscaledTime * 0.6f, 3.4f) / 3.4f;
        float bw = brandR.width * 0.24f;
        float bx = brandR.x - bw + sh * (brandR.width + bw * 2f);
        Color hc = GUI.color;
        GUI.BeginGroup(new Rect(bx, brandR.y, bw, brandR.height));
        Color oc = _brand.normal.textColor;
        _brand.normal.textColor = Color.white;
        GUI.color = new Color(1f, 1f, 1f, hc.a * 0.55f);
        GUI.Label(new Rect(brandR.x - bx, 0f, brandR.width, brandR.height), "Nocturne", _brand);
        _brand.normal.textColor = oc;
        GUI.color = hc;
        GUI.EndGroup();

        bool inRoom = LobbyBehaviour.Instance != null || ShipStatus.Instance != null;
        Color dot = inRoom ? new Color(0.36f, 0.92f, 0.52f) : p.Muted;
        var chip = new Rect(w - 258f, 20f, 82f, 24f);
        NocturneStyle.FillRounded(chip, A(dot, 0.12f), 8);
        NocturneStyle.StrokeRounded(chip, A(dot, 0.30f), 8, 1);
        float pl = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 2.4f);
        NocturneStyle.FillRounded(new Rect(chip.x + 11f, chip.y + chip.height / 2f - 3f, 6f, 6f), new Color(dot.r, dot.g, dot.b, inRoom ? 0.55f + 0.45f * pl : 0.8f), 3);
        _status.normal.textColor = Color.Lerp(dot, Color.white, 0.25f);
        GUI.Label(new Rect(chip.x + 24f, chip.y, chip.width - 26f, chip.height), inRoom ? "online" : NocturneText.T("меню", "menu"), _status);

        var ver = new Rect(w - 168f, 20f, 62f, 24f);
        NocturneStyle.FillRounded(ver, A(p.Accent, 0.14f), 8);
        GUI.Label(ver, "v" + NocturnePlugin.PluginVersion, _verPill);

        var minB = new Rect(w - 96f, 20f, 26f, 24f);
        var closeB = new Rect(w - 62f, 20f, 26f, 24f);
        if (WindowButton(minB, NocturneIcon.Minimize, p)) _collapsed = !_collapsed;
        if (WindowButton(closeB, NocturneIcon.Close, p)) RequestClose();

        if (!_collapsed)
            NocturneStyle.Fill(new Rect(0f, HeaderH - 1f, w, 1f), A(p.Accent, 0.30f));
    }

    [HideFromIl2Cpp]
    private bool WindowButton(Rect r, NocturneIcon icon, NocturnePalette p)
    {
        bool hover = r.Contains(M);
        int id = RectId(r);
        Pressed(r, id);
        float k = PressK(id);

        Matrix4x4 m = GUI.matrix;
        if (k < 1f) GUIUtility.ScaleAroundPivot(new Vector2(k, k), r.center);
        if (hover) NocturneStyle.FillRounded(r, A(Color.white, 0.07f), 7);
        if (NocturneStyle.Lite)
        {
            _centerMuted.normal.textColor = hover ? p.Text : p.Muted;
            GUI.Label(r, icon == NocturneIcon.Close ? "✕" : "—", _centerMuted);
        }
        else NocturneIcons.Draw(icon, new Rect(r.x + r.width / 2f - 7f, r.y + r.height / 2f - 7f, 14f, 14f), hover ? p.Text : p.Muted);
        GUI.matrix = m;

        return GUI.Button(r, GUIContent.none, _invisible);
    }

    [HideFromIl2Cpp]
    private void DrawLogo(Rect box, NocturnePalette p)
    {
        Vector2 c = box.center;
        if (!NocturneStyle.Lite)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2.2f);
            NocturneStyle.FillRounded(new Rect(c.x - 22f, c.y - 22f, 44f, 44f), A(p.Accent, 0.09f + 0.13f * pulse), 22);
        }

        if (_crystalTex == null || _crystalAccent != p.Id)
        {
            _crystalTex = BuildMoon(p.Accent);
            _crystalAccent = p.Id;
        }
        if (_crystalStyle == null) _crystalStyle = new GUIStyle();
        _crystalStyle.normal.background = _crystalTex;
        GUI.Box(box, GUIContent.none, _crystalStyle);

        if (!NocturneStyle.Lite)
            NocturneStyle.FillRounded(new Rect(box.x + 23f, box.y + 7f, 4f, 4f), A(Color.white, 0.9f), 2);
    }

    private static Texture2D BuildMoon(Color accent)
    {
        const int n = 64;
        float cx = n * 0.5f, cy = n * 0.5f;
        float R = n * 0.33f;
        float r = n * 0.30f;
        float ox = n * 0.15f;
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                float dOut = Mathf.Sqrt(dx * dx + dy * dy) - R;
                float ix = dx - ox;
                float dIn = Mathf.Sqrt(ix * ix + dy * dy) - r;
                float d = Mathf.Max(dOut, -dIn);
                float a = Mathf.Clamp01(0.5f - d);
                if (a <= 0f) { px[y * n + x] = new Color32(0, 0, 0, 0); continue; }
                Color col = Color.Lerp(accent, Color.white, 0.20f * Mathf.Clamp01(-dOut * 0.4f));
                px[y * n + x] = new Color32((byte)(col.r * 255f), (byte)(col.g * 255f), (byte)(col.b * 255f), (byte)(a * 255f));
            }
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    private void DrawTabHighlight(Rect r)
    {
        NocturnePalette p = NocturneStyle.Current;
        NocturneStyle.FillRounded(r, A(p.Accent, 0.12f), 6);
        NocturneStyle.FillRounded(new Rect(r.x, r.y + 4f, 3f, r.height - 8f), p.Accent, 2);
    }

    private void DrawTab(Rect r, NocturneIcon icon, string label, int index)
    {
        NocturnePalette p = NocturneStyle.Current;
        bool active = index == _tab;
        bool hover = r.Contains(M);

        if (!active && hover)
            NocturneStyle.FillRounded(r, A(Color.white, 0.045f), 6);

        Color ink = active ? Color.Lerp(p.Accent, Color.white, 0.35f) : (hover ? p.Text : p.Muted);
        NocturneIcons.Draw(icon, new Rect(r.x + 12f, r.y + r.height / 2f - 8f, 16f, 16f), ink);
        _tabStyle.normal.textColor = ink;
        GUI.Label(new Rect(r.x + 36f, r.y, r.width - 40f, r.height), label, _tabStyle);
        if (GUI.Button(r, GUIContent.none, _invisible)) SetTab(index);
    }

    private bool _inScroll;

    private bool Cull(float y, float h) => _inScroll && (y + h < -4f || y > _viewH + 4f);

    private bool RowCull(ref float y, float h, float step)
    {
        if (!Cull(y, h)) return false;
        y += step;
        return true;
    }

    private readonly Dictionary<string, bool> _cardCollapsed = new Dictionary<string, bool>();
    private readonly HashSet<string> _knownCards = new HashSet<string>();
    private readonly Dictionary<string, string> _upperCache = new Dictionary<string, string>();

    private string Up(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        if (!_upperCache.TryGetValue(s, out string u)) { u = s.ToUpperInvariant(); _upperCache[s] = u; }
        return u;
    }

    private void Grp(int g) => _cardGrp = g;

    private void RefreshStatus()
    {
        if (Time.unscaledTime - _statAt < 0.3f) return;
        _statAt = Time.unscaledTime;
        try
        {
            var net = (InnerNetClient)AmongUsClient.Instance;
            if (net != null && net.GameId != 0 && (LobbyBehaviour.Instance != null || ShipStatus.Instance != null))
            {
                _statCode = GameCode.IntToGameName(net.GameId);
                int ping = net.Ping;
                _statPing = ping + " ms";
                _statPingCol = ping < 90 ? new Color(0.4f, 0.9f, 0.5f) : (ping < 180 ? new Color(0.95f, 0.8f, 0.35f) : new Color(0.95f, 0.45f, 0.45f));
                int alive = 0;
                if (GameData.Instance != null) alive = GameData.Instance.PlayerCount;
                _statPlayers = alive.ToString();
            }
            else { _statCode = "-"; _statPing = "-"; _statPlayers = "-"; _statPingCol = Color.gray; }
        }
        catch { }
    }

    private void DrawStatusStrip(Rect r)
    {
        NocturnePalette p = NocturneStyle.Current;
        RefreshStatus();
        if (NocturneStyle.Lite)
        {
            _muted.normal.textColor = p.Muted;
            GUI.Label(new Rect(r.x + 4f, r.y, r.width - 8f, r.height),
                NocturneText.T("Код: ", "Code: ") + _statCode + "    " + NocturneText.T("Пинг: ", "Ping: ") + _statPing + "    " + NocturneText.T("Игроки: ", "Players: ") + _statPlayers, _muted);
            return;
        }
        NocturneStyle.FillRounded(r, A(Color.white, 0.03f), 7);
        NocturneStyle.StrokeRounded(r, A(Color.white, 0.06f), 7, 1);

        float seg = r.width / 3f;
        StatCell(new Rect(r.x, r.y, seg, r.height), NocturneText.T("КОД", "CODE"), _statCode, p.Accent);
        NocturneStyle.Fill(new Rect(r.x + seg, r.y + 5f, 1f, r.height - 10f), A(Color.white, 0.06f));
        StatCell(new Rect(r.x + seg, r.y, seg, r.height), NocturneText.T("ПИНГ", "PING"), _statPing, _statPingCol);
        NocturneStyle.Fill(new Rect(r.x + seg * 2f, r.y + 5f, 1f, r.height - 10f), A(Color.white, 0.06f));
        StatCell(new Rect(r.x + seg * 2f, r.y, seg, r.height), NocturneText.T("ИГРОКИ", "PLAYERS"), _statPlayers, p.Text);
    }

    private void StatCell(Rect r, string label, string value, Color valCol)
    {
        GUI.Label(new Rect(r.x + 12f, r.y + 3f, r.width - 14f, 12f), label, _footer);
        _statVal.normal.textColor = valCol;
        GUI.Label(new Rect(r.x + 12f, r.y + 13f, r.width - 14f, 16f), value, _statVal);
    }

    private const int FavTab = 10;

    private enum FavKind { Bool, Float, Int, Cycle, Key }

    private sealed class FavItem
    {
        public FavKind Kind;
        public string Label;
        public readonly HashSet<string> Cards = new HashSet<string>();
        public ConfigEntry<bool> B;
        public ConfigEntry<float> F;
        public ConfigEntry<int> I;
        public ConfigEntry<string> S;
        public ConfigEntry<KeyCode> K;
        public float Min, Max;
        public string Fmt;
        public string[] Vals, Disp;
    }

    private readonly Dictionary<string, FavItem> _favReg = new Dictionary<string, FavItem>();
    private readonly List<string> _favOrder = new List<string>();
    private readonly HashSet<string> _favSet = new HashSet<string>();
    private bool _favLoaded;
    private string _curCard;

    private void FavLoad()
    {
        if (_favLoaded) return;
        _favLoaded = true;
        string csv = NocturneConfig.FavFeatures != null ? NocturneConfig.FavFeatures.Value : "";
        if (string.IsNullOrEmpty(csv)) return;
        foreach (string raw in csv.Split(';'))
        {
            string k = raw.Trim();
            if (k.Length > 0 && _favSet.Add(k)) _favOrder.Add(k);
        }
    }

    private void FavSave()
    {
        if (NocturneConfig.FavFeatures != null)
            NocturneConfig.FavFeatures.Value = string.Join(";", _favOrder.ToArray());
    }

    private static string FavKey(ConfigEntryBase e)
    {
        try { return e == null ? null : e.Definition.Section + "/" + e.Definition.Key; }
        catch { return null; }
    }

    private FavItem FavReg(string key, FavKind kind)
    {
        if (key == null) return null;
        if (!_favReg.TryGetValue(key, out FavItem it)) { it = new FavItem(); _favReg[key] = it; }
        it.Kind = kind;
        if (_curCard != null) it.Cards.Add(_curCard);
        return it;
    }

    private void FavCard(string cardKey)
    {
        if (string.IsNullOrEmpty(cardKey)) return;
        FavLoad();
        var keys = new List<string>();
        foreach (var kv in _favReg) if (kv.Value.Cards.Contains(cardKey)) keys.Add(kv.Key);
        if (keys.Count == 0) return;

        bool allFav = true;
        for (int i = 0; i < keys.Count; i++) if (!_favSet.Contains(keys[i])) { allFav = false; break; }
        if (allFav)
        {
            for (int i = 0; i < keys.Count; i++) if (_favSet.Remove(keys[i])) _favOrder.Remove(keys[i]);
            NocturneToast.Push(NocturneText.T("Избранное", "Favorites"), NocturneText.T("Карточка убрана", "Card removed"), 1.8f, NocturneNotifyKind.Info);
        }
        else
        {
            for (int i = 0; i < keys.Count; i++) if (_favSet.Add(keys[i])) _favOrder.Add(keys[i]);
            NocturneToast.Push(NocturneText.T("Избранное", "Favorites"), NocturneText.T("Карточка в избранном", "Card added"), 1.8f, NocturneNotifyKind.Success);
        }
        FavSave();
    }

    private void FavRow(Rect r, string key)
    {
        if (key == null) return;
        FavLoad();
        Event e = Event.current;
        if (e != null && e.type == EventType.MouseDown && e.button == 1 && r.Contains(e.mousePosition))
        {
            e.Use();
            if (_favSet.Remove(key)) { _favOrder.Remove(key); NocturneToast.Push(NocturneText.T("Избранное", "Favorites"), NocturneText.T("Убрано из избранного", "Removed from favorites"), 1.6f, NocturneNotifyKind.Info); }
            else { _favSet.Add(key); _favOrder.Add(key); NocturneToast.Push(NocturneText.T("Избранное", "Favorites"), NocturneText.T("Добавлено в избранное", "Added to favorites"), 1.6f, NocturneNotifyKind.Success); }
            FavSave();
        }
        if (!NocturneStyle.Lite && _favSet.Contains(key))
        {
            _favStar.normal.textColor = NocturneStyle.Current.Accent;
            GUI.Label(new Rect(r.x - 4f, r.y, 10f, r.height), "★", _favStar);
        }
    }

    private void DrawFavorites(float x, ref float y, float w)
    {
        FavLoad();
        GUI.Label(new Rect(x + 2f, y, w - 4f, 22f),
            NocturneText.T("Правый клик по строке или заголовку карточки — добавить/убрать.", "Right-click a row or card header to add/remove."), _muted);
        y += 28f;

        if (_favOrder.Count == 0)
        {
            GUI.Label(new Rect(x + 2f, y, w - 4f, 24f), NocturneText.T("Пусто.", "Empty."), _muted);
            y += 26f;
            return;
        }

        int missing = 0;
        for (int i = 0; i < _favOrder.Count; i++)
        {
            if (!_favReg.TryGetValue(_favOrder[i], out FavItem it)) { missing++; continue; }
            switch (it.Kind)
            {
                case FavKind.Bool: Toggle(x, ref y, w, it.Label, it.B); break;
                case FavKind.Float: Slider(x, ref y, w, it.Label, it.F, it.Min, it.Max, it.Fmt); break;
                case FavKind.Int: SliderInt(x, ref y, w, it.Label, it.I, (int)it.Min, (int)it.Max); break;
                case FavKind.Cycle: ActionCycle(x, ref y, w, it.Label, it.S, it.Vals, it.Disp); break;
                case FavKind.Key: KeyRow(x, ref y, w, it.Label, it.K); break;
            }
        }
        if (missing > 0)
        {
            GUI.Label(new Rect(x + 2f, y, w - 4f, 22f),
                NocturneText.T($"Ещё {missing} — откройте их разделы, чтобы подгрузить.", $"{missing} more — open their tabs to load."), _muted);
            y += 24f;
        }
    }

    private void DrawRail(Rect area)
    {
        NocturnePalette p = NocturneStyle.Current;
        int n = Tabs.Length + 2;
        float itemH = Mathf.Min(48f, area.height / n);
        float iconSz = Mathf.Clamp(itemH * 0.42f, 18f, 24f);

        Event e = Event.current;
        if (e != null && e.type == EventType.ScrollWheel && area.Contains(e.mousePosition))
        {
            int nt = Mathf.Clamp(_tab + (e.delta.y > 0f ? 1 : -1), 0, n - 1);
            if (nt != _tab) { _tabDir = nt > _tab ? 1 : -1; _tab = nt; _scroll = 0f; _scrollPending = 0f; _tabAnimAt = Time.unscaledTime; }
            e.Use();
        }

        for (int i = 0; i < n; i++)
        {
            var r = new Rect(area.x + 5f, area.y + i * itemH + 2f, area.width - 8f, itemH - 4f);
            bool active = _tab == i;
            bool hover = r.Contains(M);

            if (active)
            {
                NocturneStyle.FillRounded(r, A(p.Accent, 0.16f), 9);
                NocturneStyle.FillRounded(new Rect(area.x, r.y + 4f, 3f, r.height - 8f), p.Accent, 1);
            }
            else if (hover)
                NocturneStyle.FillRounded(r, A(Color.white, 0.05f), 9);

            NocturneIcon icon = i < Tabs.Length ? Tabs[i].Icon : (i == Tabs.Length ? NocturneIcon.Gear : NocturneIcon.Bookmark);
            Color ic = active ? Color.Lerp(p.Accent, Color.white, 0.5f) : (hover ? A(p.Text, 0.95f) : A(p.Text, 0.55f));
            NocturneIcons.Draw(icon, new Rect(r.center.x - iconSz / 2f, r.center.y - iconSz / 2f, iconSz, iconSz), ic);

            if (GUI.Button(r, GUIContent.none, _invisible) && _tab != i)
            {
                _tabDir = i > _tab ? 1 : -1;
                _tab = i;
                _scroll = 0f;
                _scrollPending = 0f;
                _tabAnimAt = Time.unscaledTime;
            }
        }
    }

    private Rect Card(float x, ref float y, float w, string title, float bodyH)
    {
        NocturnePalette p = NocturneStyle.Current;
        const float head = 28f;
        _curCard = _tab + "/" + title;

        if (_cardGrp >= 0 && _cardGrp != _subTab[_tab])
            return new Rect(-20000f, -20000f, w - 20f, bodyH);

        _knownCards.Add(title);
        _cardCollapsed.TryGetValue(title, out bool collapsed);
        float shownBody = collapsed ? 0f : bodyH;
        var card = new Rect(x, y, w, head + shownBody + 6f);

        Event ce = Event.current;
        var headRect = new Rect(card.x, card.y, card.width, head);
        if (ce != null && ce.type == EventType.MouseDown && ce.button == 0 && headRect.Contains(ce.mousePosition))
        {
            collapsed = !collapsed;
            _cardCollapsed[title] = collapsed;
            ce.Use();
        }
        else if (ce != null && ce.type == EventType.MouseDown && ce.button == 1 && headRect.Contains(ce.mousePosition))
        {
            FavCard(_tab + "/" + title);
            ce.Use();
        }

        if (!Cull(y, card.height))
        {
            bool hover = headRect.Contains(M);
            if (hover) NocturneStyle.Fill(new Rect(card.x, card.y, card.width, head), A(Color.white, 0.03f));
            if (!NocturneStyle.Lite)
                NocturneStyle.FillRounded(new Rect(card.x + 4f, card.y + head / 2f - 6f, 3f, 12f), p.Accent, 2);
            GUI.Label(new Rect(card.x + 16f, card.y, card.width - 44f, head), Up(title), _cardTitle);
            GUI.Label(new Rect(card.xMax - 22f, card.y, 18f, head), collapsed ? "▸" : "▾", _centerMuted);
            NocturneStyle.Fill(new Rect(card.x + 4f, card.y + head - 1f, card.width - 8f, 1f), A(Color.white, 0.07f));
        }

        y = card.yMax + 8f;
        return collapsed
            ? new Rect(-20000f, -20000f, card.width - 20f, bodyH)
            : new Rect(card.x + 10f, card.y + head + 2f, card.width - 20f, bodyH);
    }

    private void Sub(float x, ref float y, float w, string title)
    {
        if (Cull(y, 26f)) { y += 26f; return; }
        NocturnePalette p = NocturneStyle.Current;
        NocturneStyle.Fill(new Rect(x, y + 21f, w, 1f), A(Color.white, 0.06f));
        if (!NocturneStyle.Lite)
            NocturneStyle.FillRounded(new Rect(x, y + 6f, 2f, 12f), A(p.Accent, 0.75f), 1);
        GUI.Label(new Rect(x + 10f, y - 1f, w - 12f, 22f), Up(title), _cardTitle);
        y += 26f;
    }

    private static readonly string[] KickBanVals = { "Kick", "Ban" };
    private static readonly string[] VoteVals = { "Null", "Warn", "Kick", "Ban" };
    private static string[] _kickBanDisp, _voteDisp;
    private static bool _dispRu;

    private static void EnsureDisp()
    {
        bool ru = NocturneText.IsRussian;
        if (_kickBanDisp != null && ru == _dispRu) return;
        _dispRu = ru;
        _kickBanDisp = new[] { NocturneText.T("Кик", "Kick"), NocturneText.T("Бан", "Ban") };
        _voteDisp = new[] { NocturneText.T("Нулл", "Null"), NocturneText.T("Варн", "Warn"), NocturneText.T("Кик", "Kick"), NocturneText.T("Бан", "Ban") };
    }

    private static string[] KickBanDisp { get { EnsureDisp(); return _kickBanDisp; } }
    private static string[] VoteDisp { get { EnsureDisp(); return _voteDisp; } }

    private static readonly string[] GuardSubsRu = { "Списки", "Фильтры", "Защита" };
    private static readonly string[] GuardSubsEn = { "Lists", "Filters", "Shield" };

    private void DrawGuard(float x, ref float y, float w)
    {
        SubBar(x, ref y, w, GuardSubsRu, GuardSubsEn);

        Grp(0);
        Rect b = Card(x, ref y, w, NocturneText.T("Списки доступа", "Access lists"), 2f * RowH + 62f);
        float by = b.y;
        Toggle(b.x, ref by, b.width, NocturneText.T("Бан-лист (кик по заходу)", "Ban list (kick on join)"), NocturneConfig.AccessBanEnabled);
        Toggle(b.x, ref by, b.width, NocturneText.T("Только вайтлист", "Whitelist only"), NocturneConfig.AccessWhitelistOnly);
        GUI.Label(new Rect(b.x + 2f, by, b.width * 0.5f, 26f), $"{NocturneText.T("Бан", "Ban")}: <b>{NocturneAccess.BanCount}</b>   {NocturneText.T("Вайт", "White")}: <b>{NocturneAccess.WhiteCount}</b>", _muted);
        if (SmallButton(new Rect(b.x + b.width - 174f, by + 1f, 82f, 24f), NocturneText.T("ОЧ. БАН", "CLR BAN"), new Color(0.9f, 0.36f, 0.36f))) NocturneAccess.ClearBans();
        if (SmallButton(new Rect(b.x + b.width - 86f, by + 1f, 82f, 24f), NocturneText.T("ОЧ. ВАЙТ", "CLR WHITE"), NocturneStyle.Current.Accent)) NocturneAccess.ClearWhites();
        by += 32f;
        if (SmallButton(new Rect(b.x + 2f, by, 112f, 24f), NocturneText.T("ИМПОРТ TXT", "IMPORT TXT"), NocturneStyle.Current.Accent)) NocturneAccess.ImportTxt();
        GUI.Label(new Rect(b.x + 122f, by, b.width - 122f, 24f), "Among Us/Nocturne/BanList.txt · WhiteList.txt", _muted);

        Grp(0);
        b = Card(x, ref y, w, NocturneText.T("Ник-бан и история", "Nick ban & history"), 4f * RowH + 62f);
        by = b.y;
        Toggle(b.x, ref by, b.width, NocturneText.T("Ник-бан (кик по нику)", "Nick ban (kick by name)"), NocturneConfig.AccessNickBanEnabled);
        Toggle(b.x, ref by, b.width, NocturneText.T("История ников по FriendCode", "Nick history by FriendCode"), NocturneConfig.NameHistory);
        Toggle(b.x, ref by, b.width, NocturneText.T("Уведомлять о знакомых", "Notify about known players"), NocturneConfig.NotifyKnownPlayer);
        Toggle(b.x, ref by, b.width, NocturneText.T("Показывать заход ботов", "Show bot joins"), NocturneConfig.ShowBotJoins);
        _nickText = CustomText(new Rect(b.x + 2f, by, b.width - 168f, 26f), _nickText ?? "", "nickBan");
        if (SmallButton(new Rect(b.x + b.width - 160f, by + 1f, 158f, 24f), NocturneText.T("＋ НИК В БАН", "＋ NICK TO BAN"), NocturneStyle.Current.Accent) && !string.IsNullOrWhiteSpace(_nickText))
        {
            NocturneAccess.AddNickBan(_nickText);
            _nickText = "";
        }
        by += 32f;
        GUI.Label(new Rect(b.x + 2f, by, b.width * 0.5f, 26f), $"{NocturneText.T("Ников", "Nicks")}: <b>{NocturneAccess.NickBanCount}</b>", _muted);
        if (SmallButton(new Rect(b.x + b.width - 96f, by + 1f, 92f, 24f), NocturneText.T("ОЧИСТИТЬ", "CLEAR"), new Color(0.9f, 0.36f, 0.36f))) NocturneAccess.ClearNickBans();

        Grp(0);
        b = Card(x, ref y, w, NocturneText.T("Платформ-бан", "Platform ban"), 2f * RowH + 32f);
        by = b.y;
        Toggle(b.x, ref by, b.width, NocturneText.T("Бан по имени платформы", "Ban by platform name"), NocturneConfig.AccessPlatformBanEnabled);
        _platformText = CustomText(new Rect(b.x + 2f, by, b.width - 168f, 26f), _platformText ?? "", "platformBan");
        if (SmallButton(new Rect(b.x + b.width - 160f, by + 1f, 158f, 24f), NocturneText.T("＋ В БАН", "＋ TO BAN"), NocturneStyle.Current.Accent) && !string.IsNullOrWhiteSpace(_platformText))
        {
            NocturneAccess.AddPlatformBan(_platformText);
            _platformText = "";
        }
        by += 32f;
        GUI.Label(new Rect(b.x + 2f, by, b.width * 0.5f, 26f), $"{NocturneText.T("Платформ", "Platforms")}: <b>{NocturneAccess.PlatformBanCount}</b>", _muted);
        if (SmallButton(new Rect(b.x + b.width - 96f, by + 1f, 92f, 24f), NocturneText.T("ОЧИСТИТЬ", "CLEAR"), new Color(0.9f, 0.36f, 0.36f))) NocturneAccess.ClearPlatformBans();

        Grp(0);
        ListCard(x, ref y, w, NocturneText.T("Бан-лист", "Ban list"), NocturneAccess.BanEntries, NocturneAccess.RemoveBan);
        Grp(0);
        ListCard(x, ref y, w, NocturneText.T("Вайтлист", "Whitelist"), NocturneAccess.WhiteEntries, NocturneAccess.RemoveWhite);
        Grp(0);
        RecentPlayersCard(x, ref y, w);
        Grp(0);
        NickListCard(x, ref y, w);
        Grp(0);
        PlatformListCard(x, ref y, w);
        Grp(0);
        RecentCard(x, ref y, w);

        float lvlBody = 2f * RowH
            + (NocturneConfig.MinLevelEnabled.Value ? 50f + RowH : 0f)
            + (NocturneConfig.MaxLevelEnabled.Value ? 50f + RowH : 0f);
        Grp(1);
        b = Card(x, ref y, w, NocturneText.T("Уровень", "Level"), lvlBody);
        by = b.y;
        Toggle(b.x, ref by, b.width, NocturneText.T("Реакция на низкий уровень", "React to low level"), NocturneConfig.MinLevelEnabled);
        if (NocturneConfig.MinLevelEnabled.Value)
        {
            SliderInt(b.x, ref by, b.width, NocturneText.T("Мин. уровень", "Min level"), NocturneConfig.MinLevel, 1, 500);
            ActionCycle(b.x, ref by, b.width, NocturneText.T("Действие", "Action"), NocturneConfig.MinLevelAction, KickBanVals, KickBanDisp);
        }
        Toggle(b.x, ref by, b.width, NocturneText.T("Реакция на высокий уровень", "React to high level"), NocturneConfig.MaxLevelEnabled);
        if (NocturneConfig.MaxLevelEnabled.Value)
        {
            SliderInt(b.x, ref by, b.width, NocturneText.T("Макс. уровень", "Max level"), NocturneConfig.MaxLevel, 1, 999);
            ActionCycle(b.x, ref by, b.width, NocturneText.T("Действие", "Action"), NocturneConfig.MaxLevelAction, KickBanVals, KickBanDisp);
        }

        Grp(2);
        b = Card(x, ref y, w, NocturneText.T("Соединение", "Connection"), RowH);
        by = b.y;
        Toggle(b.x, ref by, b.width, NocturneText.T("Форс DTLS (шифрование)", "Force DTLS (encryption)"), NocturneConfig.ForceDtls);

        Grp(2);
        b = Card(x, ref y, w, NocturneText.T("Войт-кик", "Vote-kick"), 2f * RowH);
        by = b.y;
        Toggle(b.x, ref by, b.width, NocturneText.T("Блокировать войт-кики", "Block vote-kicks"), NocturneConfig.VoteKickProtect);
        ActionCycle(b.x, ref by, b.width, NocturneText.T("Реакция на голосующего", "React to voter"), NocturneConfig.VoteKickAction, VoteVals, VoteDisp);

        Grp(2);
        b = Card(x, ref y, w, NocturneText.T("Античит RPC (хост)", "RPC anticheat (host)"), 2f * RowH + 42f);
        by = b.y;
        Toggle(b.x, ref by, b.width, NocturneText.T("Ловить невозможные RPC", "Catch impossible RPCs"), NocturneConfig.RpcGuard);
        ActionCycle(b.x, ref by, b.width, NocturneText.T("Реакция на читера", "React to cheater"), NocturneConfig.RpcGuardAction, VoteVals, VoteDisp);
        GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 36f), NocturneText.T("Саботаж мирным, вент без права и через систему, скан/анимация импостером, репорт/двери в H&S.", "Crew sabotage, illegal vent (both paths), impostor scan/anim, report/doors in H&S."), _muted);

        Grp(2);
        b = Card(x, ref y, w, NocturneText.T("Анти-бан", "Anti-ban"), 2f * RowH + 42f);
        by = b.y;
        Toggle(b.x, ref by, b.width, NocturneText.T("На хосте: банить отправителя", "As host: punish sender"), NocturneConfig.AntiBanHost);
        ActionCycle(b.x, ref by, b.width, NocturneText.T("Реакция", "Action"), NocturneConfig.AntiBanHostAction, VoteVals, VoteDisp);
        GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 36f), NocturneText.T("Гасит краш-бан (vent-kick) пакет. Вне хоста работает всегда.", "Kills the crash-ban (vent-kick) packet. Off-host it is always on."), _muted);

        Grp(1);
        b = Card(x, ref y, w, NocturneText.T("Цвета", "Colors"), RowH);
        by = b.y;
        Toggle(b.x, ref by, b.width, NocturneText.T("Кик Fortegreen", "Kick Fortegreen"), NocturneConfig.KickFortegreen);

        Grp(2);
        b = Card(x, ref y, w, NocturneText.T("Анти-флуд", "Anti-flood"), RowH);
        by = b.y;
        Toggle(b.x, ref by, b.width, NocturneText.T("Блок фейк-собраний и спавн-флуда", "Block fake meetings & spawn floods"), NocturneConfig.BlockFakeMeetings);

        Grp(2);
        b = Card(x, ref y, w, NocturneText.T("Анти-форс", "Anti-force"), 2f * RowH + 26f);
        by = b.y;
        Toggle(b.x, ref by, b.width, NocturneText.T("Блок форс-вент-ТП", "Block forced vent TP"), NocturneConfig.VentTpProtect);
        Toggle(b.x, ref by, b.width, NocturneText.T("Блок форс-зиплайна", "Block forced zipline"), NocturneConfig.ZiplineProtect);
        GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Режет чужие попытки катать/выбивать тебя.", "Drops others' attempts to ride/boot you."), _muted);

        Grp(1);
        b = Card(x, ref y, w, NocturneText.T("Модерация", "Moderation"), RowH);
        by = b.y;
        Toggle(b.x, ref by, b.width, NocturneText.T("Кик/бан в матче (хост)", "Kick/ban in match (host)"), NocturneConfig.UnlockMatchKickBan);

        Grp(1);
        b = Card(x, ref y, w, NocturneText.T("Детект входящих", "Join detect"), 2f * RowH + 26f);
        by = b.y;
        Toggle(b.x, ref by, b.width, NocturneText.T("Показывать платформу/ур./raw", "Show platform/lvl/raw"), NocturneConfig.JoinDetect);
        Toggle(b.x, ref by, b.width, NocturneText.T("Видеть своих (Nocturne)", "See other Nocturne users"), NocturneConfig.ModHandshake);
        GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Тост при заходе; ⚠ на подозрит. raw-имя.", "Toast on join; ⚠ on suspicious raw name."), _muted);
    }

    private void ReserveSelectedColor()
    {
        PlayerControl sel = NocturneMouseTools.Selected;
        if (sel == null || sel.Data == null || sel.Data.DefaultOutfit == null)
        {
            NocturneToast.Push(NocturneText.T("Резерв цвета", "Color reserve"), NocturneText.T("Выбери игрока (ЛКМ).", "Select a player (LMB)."), 2.5f, NocturneNotifyKind.Warning);
            return;
        }
        string fc = NocturneColorReservations.Fc(sel);
        if (string.IsNullOrWhiteSpace(fc))
        {
            NocturneToast.Push(NocturneText.T("Резерв цвета", "Color reserve"), NocturneText.T("Нет FriendCode.", "No FriendCode."), 2.5f, NocturneNotifyKind.Warning);
            return;
        }
        NocturneColorReservations.AddOrUpdate(fc, sel.Data.DefaultOutfit.ColorId, sel.Data.PlayerName);
        NocturneToast.Push(NocturneText.T("Резерв цвета", "Color reserve"), sel.Data.PlayerName, 2.5f, NocturneNotifyKind.Success);
    }

    private void DrawSnipe(float x, ref float y, float w)
    {
        bool on = NocturneConfig.SnipeColor.Value;
        int max = NocturneColorSnipe.Max();
        int cols = Mathf.Max(1, Mathf.FloorToInt((w - 36f + 6f) / 32f));
        int rows = Mathf.CeilToInt((max + 1f) / cols);

        Rect b = Card(x, ref y, w, NocturneText.T("Перехват цвета", "Color snipe"), RowH + (on ? rows * 32f + 24f : 0f));
        float by = b.y;
        Toggle(b.x, ref by, b.width, NocturneText.T("Ловить цвет в лобби", "Snipe color in lobby"), NocturneConfig.SnipeColor);
        if (!on) return;

        PlayerControl me = PlayerControl.LocalPlayer;
        int want = Mathf.Clamp(NocturneConfig.SnipeColorId.Value, 0, max);
        for (int i = 0; i <= max; i++)
        {
            var r = new Rect(b.x + (i % cols) * 32f, by + (i / cols) * 32f, 26f, 26f);
            NocturneStyle.FillRounded(r, A(Color.black, 0.25f), 6);
            DrawColorDot(new Rect(r.x + 5f, r.y + 5f, 16f, 16f), i);
            bool busy = me != null && NocturneColorSnipe.Taken(i, me);
            if (busy) GUI.Label(r, "×", _star);
            NocturneStyle.StrokeRounded(r, i == want ? NocturneStyle.Current.Accent : A(Color.white, 0.10f), 6, i == want ? 2 : 1);
            if (GUI.Button(r, GUIContent.none, _invisible)) NocturneConfig.SnipeColorId.Value = i;
        }
        by += rows * 32f;

        bool free = me != null && !NocturneColorSnipe.Taken(want, me);
        GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 22f),
            free ? NocturneText.T("Цвет свободен — беру.", "Color is free — taking it.") : NocturneText.T("Занят — жду, пока освободится.", "Taken — waiting for it to free up."), _muted);
    }

    private void DrawColorAll(float x, ref float y, float w)
    {
        bool on = NocturneConfig.ColorAll.Value;
        int max = NocturneColorSnipe.Max();
        int cols = Mathf.Max(1, Mathf.FloorToInt((w - 36f + 6f) / 32f));
        int rows = Mathf.CeilToInt((max + 1f) / cols);

        Rect b = Card(x, ref y, w, NocturneText.T("Цвет всем (хост)", "Color all (host)"), RowH + (on ? rows * 32f + 22f : 22f));
        float by = b.y;
        Toggle(b.x, ref by, b.width, NocturneText.T("Красить всех в один цвет", "Force one color on all"), NocturneConfig.ColorAll);
        if (!on)
        {
            GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 22f), NocturneText.T("Хостом. Красит и новозашедших.", "Host only. Colors new joiners too."), _muted);
            return;
        }

        int pick = Mathf.Clamp(NocturneConfig.ColorAllId.Value, 0, max);
        for (int i = 0; i <= max; i++)
        {
            var r = new Rect(b.x + (i % cols) * 32f, by + (i / cols) * 32f, 26f, 26f);
            NocturneStyle.FillRounded(r, A(Color.black, 0.25f), 6);
            DrawColorDot(new Rect(r.x + 5f, r.y + 5f, 16f, 16f), i);
            NocturneStyle.StrokeRounded(r, i == pick ? NocturneStyle.Current.Accent : A(Color.white, 0.10f), 6, i == pick ? 2 : 1);
            if (GUI.Button(r, GUIContent.none, _invisible)) NocturneConfig.ColorAllId.Value = i;
        }
        by += rows * 32f;
        GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 22f), NocturneText.T("Красит и новозашедших. Выкл — вернут свои цвета.", "Colors new joiners. Off — they repick colors."), _muted);
    }

    private void DrawColorDot(Rect r, int colorId)
    {
        Color c = new Color(0.5f, 0.5f, 0.5f, 1f);
        try
        {
            if (Palette.PlayerColors != null && colorId >= 0 && colorId < Palette.PlayerColors.Length)
            {
                Color32 c32 = Palette.PlayerColors[colorId];
                c = new Color(c32.r / 255f, c32.g / 255f, c32.b / 255f, 1f);
            }
        }
        catch { }
        NocturneStyle.FillRounded(r, c, 6);
        NocturneStyle.StrokeRounded(r, A(Color.white, 0.3f), 6, 1);
    }

    private void PlayerRow(float x, ref float y, float w, InnerNetClient net, ClientData c)
    {
        var r = new Rect(x, y, w, RowH + 2f);
        HoverFill(r);
        int nk = NocturneNameHistory.KnownNickCount(c.Character);
        string note = nk > 1 ? "  " + NocturneText.T($"·{nk} ников", $"·{nk} nicks") : string.Empty;
        GUI.Label(new Rect(r.x + 10f, r.y, r.width - 200f, r.height),
            $"<b>{NocturneAccess.SafeName(c)}</b>   <color=#8A94AC><size=11>{ClientInfo(c)}{note}</size></color>", _rowName);
        float cy = r.y + (r.height - 24f) / 2f;
        string mfc = NocturneColorReservations.Fc(c.Character);
        bool muted = NocturneMuteList.IsMuted(mfc);
        if (SmallButton(new Rect(r.xMax - 188f, cy, 44f, 24f), NocturneText.T("МУТ", "MUTE"), muted ? new Color(0.9f, 0.4f, 0.4f) : new Color(0.5f, 0.55f, 0.62f))) NocturneMuteList.Toggle(mfc);
        if (SmallButton(new Rect(r.xMax - 142f, cy, 44f, 24f), NocturneText.T("НИК", "NICK"), new Color(0.86f, 0.5f, 0.28f))) NocturneAccess.NickBanClient(net, c);
        if (SmallButton(new Rect(r.xMax - 96f, cy, 44f, 24f), NocturneText.T("БАН", "BAN"), new Color(0.9f, 0.36f, 0.36f))) NocturneAccess.BanClient(net, c);
        if (SmallButton(new Rect(r.xMax - 50f, cy, 44f, 24f), NocturneText.T("ВАЙТ", "WHITE"), NocturneStyle.Current.Accent)) NocturneAccess.WhiteClient(c);
        y += RowH + 6f;
    }

    private void NickListCard(float x, ref float y, float w)
    {
        IReadOnlyList<string> nicks = NocturneAccess.NickBanEntries;
        float body = nicks.Count > 0 ? nicks.Count * 30f : 26f;
        Rect b = Card(x, ref y, w, $"{NocturneText.T("Ник-бан список", "Nick ban list")} ({nicks.Count})", body);
        float by = b.y;
        if (nicks.Count == 0)
        {
            GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Пусто.", "Empty."), _muted);
            return;
        }

        for (int i = 0; i < nicks.Count; i++)
        {
            var r = new Rect(b.x, by, b.width, 28f);
            HoverFill(r);
            GUI.Label(new Rect(r.x + 8f, r.y, r.width - 46f, r.height), $"<b>{nicks[i]}</b>", _rowName);
            if (SmallButton(new Rect(r.xMax - 38f, r.y + 3f, 34f, 22f), "✕", new Color(0.9f, 0.4f, 0.4f)))
            {
                NocturneAccess.RemoveNickBan(nicks[i]);
                break;
            }
            by += 30f;
        }
    }

    private void RecentCard(float x, ref float y, float w)
    {
        IReadOnlyList<RecentEntry> recent = NocturneRecent.Entries;
        float body = 30f + (recent.Count > 0 ? recent.Count * 46f : 26f);
        Rect b = Card(x, ref y, w, $"{NocturneText.T("Кто уходил", "Who left")} ({recent.Count})", body);
        float by = b.y;

        if (SmallButton(new Rect(b.x + b.width - 96f, by, 92f, 24f), NocturneText.T("ОЧИСТИТЬ", "CLEAR"), new Color(0.9f, 0.36f, 0.36f)))
            NocturneRecent.Clear();
        by += 30f;

        if (recent.Count == 0)
        {
            GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Пусто.", "Empty."), _muted);
            return;
        }

        for (int i = 0; i < recent.Count; i++)
        {
            RecentEntry e = recent[i];
            var r = new Rect(b.x, by, b.width, 44f);
            HoverFill(r);
            GUI.Label(new Rect(r.x + 8f, r.y + 1f, r.width - 152f, 22f), e.Title, _rowName);
            GUI.Label(new Rect(r.x + 8f, r.y + 21f, r.width - 152f, 20f), e.Info, _rowInfo);

            if (SmallButton(new Rect(r.xMax - 144f, r.y + 10f, 66f, 24f), NocturneText.T("БАН", "BAN"), new Color(0.9f, 0.4f, 0.4f)))
                NocturneAccess.AddBan(e.Name, e.Fc, e.Puid);
            if (SmallButton(new Rect(r.xMax - 74f, r.y + 10f, 70f, 24f), NocturneText.T("НИК-БАН", "NICK"), NocturneStyle.Current.Accent))
                NocturneAccess.AddNickBan(e.Name);
            by += 46f;
        }
    }

    private void PlatformListCard(float x, ref float y, float w)
    {
        IReadOnlyList<string> plats = NocturneAccess.PlatformBanEntries;
        float body = plats.Count > 0 ? plats.Count * 30f : 26f;
        Rect b = Card(x, ref y, w, $"{NocturneText.T("Платформ-бан список", "Platform ban list")} ({plats.Count})", body);
        float by = b.y;
        if (plats.Count == 0)
        {
            GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Пусто.", "Empty."), _muted);
            return;
        }

        for (int i = 0; i < plats.Count; i++)
        {
            var r = new Rect(b.x, by, b.width, 28f);
            HoverFill(r);
            GUI.Label(new Rect(r.x + 8f, r.y, r.width - 46f, r.height), $"<b>{plats[i]}</b>", _rowName);
            if (SmallButton(new Rect(r.xMax - 38f, r.y + 3f, 34f, 22f), "✕", new Color(0.9f, 0.4f, 0.4f)))
            {
                NocturneAccess.RemovePlatformBan(plats[i]);
                break;
            }
            by += 30f;
        }
    }

    [HideFromIl2Cpp]
    private void ListCard(float x, ref float y, float w, string title, IReadOnlyList<AccessEntry> entries, Action<string> onRemove)
    {
        float body = entries.Count > 0 ? entries.Count * 30f : 26f;
        Rect b = Card(x, ref y, w, $"{title} ({entries.Count})", body);
        float by = b.y;
        if (entries.Count == 0)
        {
            GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Пусто.", "Empty."), _muted);
            return;
        }

        for (int i = 0; i < entries.Count; i++)
        {
            AccessEntry e = entries[i];
            var r = new Rect(b.x, by, b.width, 28f);
            HoverFill(r);
            string name = string.IsNullOrEmpty(e.Name) ? (string.IsNullOrEmpty(e.Code) ? NocturneText.T("гость", "guest") : e.Code) : e.Name;
            GUI.Label(new Rect(r.x + 8f, r.y, r.width - 46f, r.height),
                $"<b>{name}</b>   <color=#8A94AC><size=11>{EntrySub(e)}</size></color>", _rowName);
            if (SmallButton(new Rect(r.xMax - 38f, r.y + 3f, 34f, 22f), "✕", new Color(0.9f, 0.4f, 0.4f)))
            {
                onRemove(string.IsNullOrEmpty(e.Code) ? e.Puid : e.Code);
                break;
            }
            by += 30f;
        }
    }

    private void RecentPlayersCard(float x, ref float y, float w)
    {
        List<RecentRow> rows = NocturneRecentPlayers.Rows;
        float body = 26f + (rows.Count > 0 ? rows.Count * 30f : 26f);
        Rect b = Card(x, ref y, w, $"{NocturneText.T("Недавние игроки", "Recent players")} ({rows.Count})", body);
        float by = b.y;

        GUI.Label(new Rect(b.x + 2f, by, b.width - 100f, 24f),
            NocturneText.T("Кого игра запомнила по прошлым лобби — с ФК и PUID.", "Whom the game remembers from past lobbies — with FC and PUID."), _muted);
        if (SmallButton(new Rect(b.x + b.width - 96f, by + 1f, 92f, 24f), NocturneText.T("ОЧИСТИТЬ", "CLEAR"), new Color(0.9f, 0.36f, 0.36f)))
            NocturneRecentPlayers.Clear();
        by += 26f;

        if (rows.Count == 0)
        {
            GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Пусто — сыграй хотя бы одно лобби.", "Empty — play at least one lobby."), _muted);
            return;
        }

        for (int i = 0; i < rows.Count; i++)
        {
            RecentRow e = rows[i];
            if (RowCull(ref by, 28f, 30f)) continue;
            var r = new Rect(b.x, by, b.width, 28f);
            HoverFill(r);
            GUI.Label(new Rect(r.x + 8f, r.y, r.width - 190f, r.height),
                $"<b>{e.Name}</b>   <color=#8A94AC><size=11>{e.Code}</size></color>", _rowName);

            bool banned = NocturneAccess.IsBanned(e.Code, e.Puid);
            bool white = NocturneAccess.IsWhite(e.Code, e.Puid);
            if (SmallButton(new Rect(r.xMax - 182f, r.y + 3f, 88f, 22f), banned ? NocturneText.T("В БАНЕ ✓", "BANNED ✓") : NocturneText.T("В БАН", "BAN"), banned ? new Color(0.5f, 0.5f, 0.58f) : new Color(0.9f, 0.4f, 0.4f)) && !banned)
                NocturneRecentPlayers.Ban(e);
            if (SmallButton(new Rect(r.xMax - 90f, r.y + 3f, 88f, 22f), white ? NocturneText.T("В ВАЙТЕ ✓", "WHITE ✓") : NocturneText.T("В ВАЙТ", "WHITE"), white ? new Color(0.5f, 0.5f, 0.58f) : NocturneStyle.Current.Accent) && !white)
                NocturneRecentPlayers.White(e);
            by += 30f;
        }
    }

    private static string EntrySub(AccessEntry e)
    {
        string s = string.Empty;
        if (!string.IsNullOrEmpty(e.Code)) s = e.Code;
        if (!string.IsNullOrEmpty(e.Puid))
        {
            string pu = e.Puid.Length > 14 ? e.Puid.Substring(0, 14) + "…" : e.Puid;
            s = s.Length > 0 ? s + " · PUID " + pu : "PUID " + pu;
        }
        return s;
    }

    private bool SmallButton(Rect r, string label, Color col)
    {
        if (Cull(r.y, r.height)) return false;
        if (NocturneStyle.Lite)
        {
            Color bg = GUI.backgroundColor;
            GUI.backgroundColor = Color.Lerp(Color.white, col, 0.6f);
            bool cl = GUI.Button(r, label);
            GUI.backgroundColor = bg;
            return cl;
        }
        bool hover = r.Contains(M);
        int id = RectId(r);
        Pressed(r, id);
        float k = PressK(id);

        Matrix4x4 m = GUI.matrix;
        if (k < 1f) GUIUtility.ScaleAroundPivot(new Vector2(k, k), r.center);
        NocturneStyle.FillRounded(r, hover ? A(col, 0.4f) : A(col, 0.2f), 7);
        NocturneStyle.Fill(new Rect(r.x + 4f, r.y + 2f, r.width - 8f, 1f), A(Color.white, 0.14f));
        _smallBtn.normal.textColor = Color.Lerp(col, Color.white, 0.55f);
        GUI.Label(r, label, _smallBtn);
        GUI.matrix = m;

        return GUI.Button(r, GUIContent.none, _invisible);
    }

    [HideFromIl2Cpp]
    private void ActionCycle(float x, ref float y, float w, string label, ConfigEntry<string> entry, string[] vals, string[] disp)
    {
        string fk = FavKey(entry);
        var cit = FavReg(fk, FavKind.Cycle); if (cit != null) { cit.Label = label; cit.S = entry; cit.Vals = vals; cit.Disp = disp; }
        FavRow(new Rect(x, y, w, RowH - 4f), fk);
        int i = Mathf.Max(0, Array.IndexOf(vals, entry.Value));
        CycleRow(x, ref y, w, label, disp[i], () => { entry.Value = vals[(i + 1) % vals.Length]; });
    }

    [HideFromIl2Cpp]
    private void CollectClients(List<ClientData> into)
    {
        try
        {
            InnerNetClient net = GuardNet();
            if (net == null || net.allClients == null) return;
            var e = net.allClients.GetEnumerator();
            while (e.MoveNext())
            {
                ClientData c = e.Current;
                if (c != null && c.Id >= 0 && c.Id != net.ClientId) into.Add(c);
            }
        }
        catch { }
    }

    private PlayerControl NetSrc()
    {
        if (_netSrc == 255) return PlayerControl.LocalPlayer;
        _netPick.Clear();
        CollectPlayers(_netPick);
        for (int i = 0; i < _netPick.Count; i++)
            if (_netPick[i].PlayerId == _netSrc) return _netPick[i];
        _netSrc = 255;
        return PlayerControl.LocalPlayer;
    }

    private void NextNetSrc()
    {
        _netPick.Clear();
        CollectPlayers(_netPick);
        _netPick.RemoveAll((Predicate<PlayerControl>)(p => p == PlayerControl.LocalPlayer));
        if (_netPick.Count == 0) { _netSrc = 255; return; }
        if (_netSrc == 255) { _netSrc = _netPick[0].PlayerId; return; }
        int i = _netPick.FindIndex((Predicate<PlayerControl>)(p => p.PlayerId == _netSrc));
        _netSrc = i < 0 || i + 1 >= _netPick.Count ? (byte)255 : _netPick[i + 1].PlayerId;
    }

    [HideFromIl2Cpp]
    private void CollectPlayers(List<PlayerControl> into)
    {
        try
        {
            if (PlayerControl.AllPlayerControls == null) return;
            foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
                if (pc != null && pc.Data != null && !pc.Data.Disconnected) into.Add(pc);
        }
        catch { }
    }

    private void WhisperRow(float x, ref float y, float w, PlayerControl pc)
    {
        if (RowCull(ref y, 30f, 32f)) return;
        var r = new Rect(x, y, w, 30f);
        HoverFill(r);
        int colorId = pc.Data != null && pc.Data.DefaultOutfit != null ? pc.Data.DefaultOutfit.ColorId : 0;
        DrawColorDot(new Rect(r.x + 6f, r.y + 9f, 12f, 12f), colorId);
        GUI.Label(new Rect(r.x + 26f, r.y, r.width - 120f, r.height), pc.Data != null ? pc.Data.PlayerName : "?", _rowName);
        if (SmallButton(new Rect(r.xMax - 100f, r.y + 3f, 96f, 24f), NocturneText.T("ШЕПНУТЬ", "WHISPER"), new Color(0.55f, 0.7f, 1f)))
            NocturneWhisper.Prefill(pc.PlayerId.ToString());
        y += 32f;
    }

    private void TpRow(float x, ref float y, float w, PlayerControl pc)
    {
        if (RowCull(ref y, 30f, 32f)) return;
        var r = new Rect(x, y, w, 30f);
        HoverFill(r);
        int colorId = pc.Data != null && pc.Data.DefaultOutfit != null ? pc.Data.DefaultOutfit.ColorId : 0;
        DrawColorDot(new Rect(r.x + 6f, r.y + 9f, 12f, 12f), colorId);
        GUI.Label(new Rect(r.x + 26f, r.y, r.width - 200f, r.height), pc.Data != null ? pc.Data.PlayerName : "?", _rowName);
        if (SmallButton(new Rect(r.xMax - 194f, r.y + 3f, 92f, 24f), NocturneText.T("К НЕМУ", "GO"), new Color(0.5f, 0.78f, 0.92f)))
            TpTo(pc);
        bool follow = NocturneFollow.IsTarget(pc.PlayerId);
        if (SmallButton(new Rect(r.xMax - 98f, r.y + 3f, 94f, 24f), follow ? NocturneText.T("СТОП", "STOP") : NocturneText.T("ИДТИ ЗА", "FOLLOW"), follow ? new Color(0.9f, 0.4f, 0.4f) : new Color(0.55f, 0.7f, 1f)))
            NocturneToast.Push(NocturneText.T("Слежка", "Follow"), NocturneFollow.Toggle(pc), 2f, NocturneNotifyKind.Info);
        y += 32f;
    }

    private static void TpTo(PlayerControl pc)
    {
        try
        {
            PlayerControl me = PlayerControl.LocalPlayer;
            if (pc == null || me == null || me.NetTransform == null) return;
            me.NetTransform.RpcSnapTo(pc.GetTruePosition());
        }
        catch { }
    }

    private void VotekickRow(float x, ref float y, float w, PlayerControl pc)
    {
        if (RowCull(ref y, 30f, 32f)) return;
        var r = new Rect(x, y, w, 30f);
        HoverFill(r);
        int colorId = pc.Data != null && pc.Data.DefaultOutfit != null ? pc.Data.DefaultOutfit.ColorId : 0;
        DrawColorDot(new Rect(r.x + 6f, r.y + 9f, 12f, 12f), colorId);
        string nm = pc.Data != null ? pc.Data.PlayerName : "?";
        if (IsHost(pc)) nm += "  [H]";
        GUI.Label(new Rect(r.x + 26f, r.y, r.width - 200f, r.height), nm, _rowName);
        bool sel = NocturneVotekick.IsTarget(pc.PlayerId);
        if (SmallButton(new Rect(r.xMax - 190f, r.y + 3f, 86f, 24f), sel ? NocturneText.T("АВТО ✓", "AUTO ✓") : NocturneText.T("АВТО", "AUTO"), sel ? NocturneStyle.Current.Accent : new Color(0.36f, 0.39f, 0.47f)))
            NocturneVotekick.ToggleTarget(pc.PlayerId);
        if (SmallButton(new Rect(r.xMax - 100f, r.y + 3f, 96f, 24f), NocturneText.T("ЗАЯВИТЬ", "VOTE"), new Color(0.78f, 0.42f, 0.95f)))
            NocturneVotekick.VoteOne(pc);
        y += 32f;
    }

    private void LoopRow(float x, ref float y, float w, PlayerControl pc)
    {
        if (RowCull(ref y, 30f, 32f)) return;
        var r = new Rect(x, y, w, 30f);
        HoverFill(r);
        int colorId = pc.Data != null && pc.Data.DefaultOutfit != null ? pc.Data.DefaultOutfit.ColorId : 0;
        DrawColorDot(new Rect(r.x + 6f, r.y + 9f, 12f, 12f), colorId);
        string nm = pc.Data != null ? pc.Data.PlayerName : "?";
        if (pc.Data != null && pc.Data.IsDead) nm += "  <size=80%>†</size>";
        GUI.Label(new Rect(r.x + 26f, r.y, r.width - 130f, r.height), nm, _rowName);

        bool run = NocturneLobbyPranks.LoopTarget == pc.PlayerId && NocturneLobbyPranks.LoopLeft > 0;
        string lbl = run ? NocturneText.T("СТОП ", "STOP ") + NocturneLobbyPranks.LoopLeft : NocturneText.T("УБИТЬ ×20", "KILL ×20");
        if (SmallButton(new Rect(r.xMax - 110f, r.y + 3f, 106f, 24f), lbl, run ? new Color(0.9f, 0.4f, 0.4f) : new Color(0.78f, 0.42f, 0.95f)))
            NocturneToast.Push(NocturneText.T("Рофл", "Prank"), NocturneLobbyPranks.MurderLoop(pc, 20), 2f, NocturneNotifyKind.Info);
        y += 32f;
    }

    private void KillRow(float x, ref float y, float w, PlayerControl pc)
    {
        if (RowCull(ref y, 30f, 32f)) return;
        var r = new Rect(x, y, w, 30f);
        HoverFill(r);
        int colorId = pc.Data != null && pc.Data.DefaultOutfit != null ? pc.Data.DefaultOutfit.ColorId : 0;
        DrawColorDot(new Rect(r.x + 6f, r.y + 9f, 12f, 12f), colorId);
        string nm = pc.Data != null ? pc.Data.PlayerName : "?";
        if (pc.Data != null && pc.Data.IsDead) nm += "  <size=80%>†</size>";
        GUI.Label(new Rect(r.x + 26f, r.y, r.width - 322f, r.height), nm, _rowName);
        if (SmallButton(new Rect(r.xMax - 316f, r.y + 3f, 62f, 24f), NocturneText.T("КИЛЛ", "KILL"), new Color(0.9f, 0.4f, 0.4f)))
            NocturneToast.Push(NocturneText.T("Килл", "Kill"), NocturneKillTools.KillOne(pc), 2f, NocturneNotifyKind.Info);
        if (SmallButton(new Rect(r.xMax - 250f, r.y + 3f, 62f, 24f), NocturneText.T("ТЕЛЕ", "TELE"), new Color(0.78f, 0.42f, 0.95f)))
            NocturneToast.Push(NocturneText.T("Телекилл", "Telekill"), NocturneKillTools.Telekill(pc), 2f, NocturneNotifyKind.Info);
        if (SmallButton(new Rect(r.xMax - 184f, r.y + 3f, 78f, 24f), NocturneText.T("ЭНДЕР", "ENDER"), new Color(0.35f, 0.75f, 0.55f)))
            NocturneToast.Push(NocturneText.T("Эндермен", "Enderman"), NocturneEnderman.Kill(pc), 2.5f, NocturneNotifyKind.Info);
        if (SmallButton(new Rect(r.xMax - 102f, r.y + 3f, 98f, 24f), NocturneText.T("ВЫГНАТЬ", "EJECT"), new Color(0.5f, 0.78f, 0.92f)))
            NocturneToast.Push(NocturneText.T("Эжект", "Eject"), NocturneMeetingTools.Eject(pc), 2f, NocturneNotifyKind.Info);
        y += 32f;
    }

    private void FrameSabotageRow(float x, ref float y, float w, PlayerControl pc, SystemTypes system)
    {
        if (RowCull(ref y, 30f, 32f)) return;
        var r = new Rect(x, y, w, 30f);
        HoverFill(r);
        int colorId = pc.Data != null && pc.Data.DefaultOutfit != null ? pc.Data.DefaultOutfit.ColorId : 0;
        DrawColorDot(new Rect(r.x + 6f, r.y + 9f, 12f, 12f), colorId);
        string nm = pc.Data != null ? pc.Data.PlayerName : "?";
        GUI.Label(new Rect(r.x + 26f, r.y, r.width - 120f, r.height), nm, _rowName);
        if (SmallButton(new Rect(r.xMax - 90f, r.y + 3f, 86f, 24f), NocturneText.T("ПОДСТАВИТЬ", "FRAME"), new Color(0.85f, 0.45f, 0.2f)))
            NocturneToast.Push(NocturneText.T("Подстава", "Frame"), NocturneFrameSabotage.Send(pc, system, (byte)_frameValue), 2.4f, NocturneNotifyKind.Info);
        y += 32f;
    }

    private void VentKickRow(float x, ref float y, float w, PlayerControl pc)
    {
        if (RowCull(ref y, 30f, 32f)) return;
        var r = new Rect(x, y, w, 30f);
        HoverFill(r);
        int colorId = pc.Data != null && pc.Data.DefaultOutfit != null ? pc.Data.DefaultOutfit.ColorId : 0;
        DrawColorDot(new Rect(r.x + 6f, r.y + 9f, 12f, 12f), colorId);

        bool sel = NocturneVentKick.IsSelected(pc.PlayerId);
        var box = new Rect(r.x + 24f, r.y + 5f, 20f, 20f);
        NocturneStyle.FillRounded(box, sel ? NocturneStyle.Current.Accent : A(Color.black, 0.25f), 5);
        if (sel) GUI.Label(box, "✔", _star);
        NocturneStyle.StrokeRounded(box, A(Color.white, 0.14f), 5, 1);
        if (GUI.Button(box, GUIContent.none, _invisible)) NocturneVentKick.ToggleSelect(pc.PlayerId);

        string nm = pc.Data != null ? pc.Data.PlayerName : "?";
        if (IsHost(pc)) nm += "  [H]";
        GUI.Label(new Rect(r.x + 50f, r.y, r.width - 140f, r.height), nm, _rowName);
        if (SmallButton(new Rect(r.xMax - 90f, r.y + 3f, 86f, 24f), NocturneText.T("КИК", "KICK"), new Color(0.95f, 0.5f, 0.25f)))
            NocturneToast.Push(NocturneText.T("Вент кик", "Vent kick"), NocturneVentKick.Kick(pc), 2.4f, NocturneNotifyKind.Info);
        y += 32f;
    }

    private void JailRow(float x, ref float y, float w, PlayerControl pc)
    {
        if (RowCull(ref y, 30f, 32f)) return;
        var r = new Rect(x, y, w, 30f);
        HoverFill(r);
        int colorId = pc.Data != null && pc.Data.DefaultOutfit != null ? pc.Data.DefaultOutfit.ColorId : 0;
        DrawColorDot(new Rect(r.x + 6f, r.y + 9f, 12f, 12f), colorId);

        bool sel = NocturneJail.IsTarget(pc.PlayerId);
        var box = new Rect(r.x + 24f, r.y + 5f, 20f, 20f);
        NocturneStyle.FillRounded(box, sel ? NocturneStyle.Current.Accent : A(Color.black, 0.25f), 5);
        if (sel) GUI.Label(box, "✔", _star);
        NocturneStyle.StrokeRounded(box, A(Color.white, 0.14f), 5, 1);
        if (GUI.Button(box, GUIContent.none, _invisible)) NocturneJail.ToggleTarget(pc.PlayerId);

        string nm = pc.Data != null ? pc.Data.PlayerName : "?";
        if (IsHost(pc)) nm += "  [H]";
        GUI.Label(new Rect(r.x + 50f, r.y, r.width - 50f, r.height), nm, _rowName);
        y += 32f;
    }

    private void BlindRow(float x, ref float y, float w, PlayerControl pc)
    {
        if (RowCull(ref y, 30f, 32f)) return;
        var r = new Rect(x, y, w, 30f);
        HoverFill(r);
        int colorId = pc.Data != null && pc.Data.DefaultOutfit != null ? pc.Data.DefaultOutfit.ColorId : 0;
        DrawColorDot(new Rect(r.x + 6f, r.y + 9f, 12f, 12f), colorId);

        bool sel = NocturneBlind.IsSelected(pc.PlayerId);
        var box = new Rect(r.x + 24f, r.y + 5f, 20f, 20f);
        NocturneStyle.FillRounded(box, sel ? NocturneStyle.Current.Accent : A(Color.black, 0.25f), 5);
        if (sel) GUI.Label(box, "✔", _star);
        NocturneStyle.StrokeRounded(box, A(Color.white, 0.14f), 5, 1);
        if (GUI.Button(box, GUIContent.none, _invisible)) NocturneBlind.ToggleSelect(pc.PlayerId);

        string nm = pc.Data != null ? pc.Data.PlayerName : "?";
        if (IsHost(pc)) nm += "  [H]";
        GUI.Label(new Rect(r.x + 50f, r.y, r.width - 140f, r.height), nm, _rowName);

        Color sc = NocturneBlind.IsDark(pc.PlayerId) ? new Color(0.4f, 0.42f, 0.48f) : NocturneBlind.IsBright(pc.PlayerId) ? new Color(0.95f, 0.85f, 0.35f) : new Color(0.5f, 0.55f, 0.62f);
        if (SmallButton(new Rect(r.xMax - 90f, r.y + 3f, 86f, 24f), NocturneBlind.StateName(pc.PlayerId), sc))
            NocturneToast.Push(NocturneText.T("Свет", "Vision"), NocturneBlind.Cycle(pc), 2.2f, NocturneNotifyKind.Info);
        y += 32f;
    }

    private static bool IsHostSelf()
    {
        try { return AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost; }
        catch { return false; }
    }

    private void PetRow(float x, ref float y, float w, PlayerControl pc)
    {
        if (RowCull(ref y, 30f, 32f)) return;
        var r = new Rect(x, y, w, 30f);
        HoverFill(r);
        int colorId = pc.Data != null && pc.Data.DefaultOutfit != null ? pc.Data.DefaultOutfit.ColorId : 0;
        DrawColorDot(new Rect(r.x + 6f, r.y + 9f, 12f, 12f), colorId);

        string nm = pc.Data != null ? pc.Data.PlayerName : "?";
        if (IsHost(pc)) nm += "  [H]";
        GUI.Label(new Rect(r.x + 26f, r.y, r.width - 140f, r.height), nm, _rowName);

        bool on = NocturnePet.IsTarget(pc.PlayerId);
        Color c = on ? new Color(0.4f, 0.7f, 0.95f) : NocturneStyle.Current.Accent;
        if (SmallButton(new Rect(r.xMax - 108f, r.y + 3f, 104f, 24f), on ? NocturneText.T("ГЛАЖУ ✓", "PETTING ✓") : NocturneText.T("ГЛАДИТЬ", "PET"), c))
            NocturneToast.Push(NocturneText.T("Пет", "Pet"), on ? NocturnePet.Stop2() : NocturnePet.Grab(pc), 2f, NocturneNotifyKind.Info);
        y += 32f;
    }

    private void TaskRow(float x, ref float y, float w, PlayerControl pc)
    {
        if (RowCull(ref y, 30f, 32f)) return;
        var r = new Rect(x, y, w, 30f);
        HoverFill(r);
        int colorId = pc.Data != null && pc.Data.DefaultOutfit != null ? pc.Data.DefaultOutfit.ColorId : 0;
        DrawColorDot(new Rect(r.x + 6f, r.y + 9f, 12f, 12f), colorId);

        string nm = pc.Data != null ? pc.Data.PlayerName : "?";
        if (IsHost(pc)) nm += "  [H]";
        GUI.Label(new Rect(r.x + 26f, r.y, r.width - 260f, r.height), nm, _rowName);

        float bw = 78f;
        if (SmallButton(new Rect(r.xMax - 3 * bw - 8f, r.y + 3f, bw, 24f), NocturneText.T("ОБНУЛИТЬ", "CLEAR"), new Color(0.9f, 0.4f, 0.4f)))
            NocturneToast.Push(NocturneText.T("Задания", "Tasks"), TaskTools.Clear(pc), 2.5f, NocturneNotifyKind.Info);
        if (SmallButton(new Rect(r.xMax - 2 * bw - 4f, r.y + 3f, bw, 24f), NocturneText.T("НОРМА", "NORMAL"), NocturneStyle.Current.Accent))
            NocturneToast.Push(NocturneText.T("Задания", "Tasks"), TaskTools.Normal(pc), 2.5f, NocturneNotifyKind.Info);
        if (SmallButton(new Rect(r.xMax - bw, r.y + 3f, bw, 24f), NocturneText.T("ЗАВАЛИТЬ", "FLOOD"), new Color(0.95f, 0.5f, 0.25f)))
            NocturneToast.Push(NocturneText.T("Задания", "Tasks"), TaskTools.Flood(pc), 2.5f, NocturneNotifyKind.Info);
        y += 32f;
    }

    private void RideRow(float x, ref float y, float w, PlayerControl pc)
    {
        if (RowCull(ref y, 30f, 32f)) return;
        var r = new Rect(x, y, w, 30f);
        HoverFill(r);
        int colorId = pc.Data != null && pc.Data.DefaultOutfit != null ? pc.Data.DefaultOutfit.ColorId : 0;
        DrawColorDot(new Rect(r.x + 6f, r.y + 9f, 12f, 12f), colorId);

        bool sel = RideTargets.Has(pc.PlayerId);
        var box = new Rect(r.x + 24f, r.y + 5f, 20f, 20f);
        NocturneStyle.FillRounded(box, sel ? NocturneStyle.Current.Accent : A(Color.black, 0.25f), 5);
        if (sel) GUI.Label(box, "✔", _star);
        NocturneStyle.StrokeRounded(box, A(Color.white, 0.14f), 5, 1);
        if (GUI.Button(box, GUIContent.none, _invisible)) RideTargets.Toggle(pc.PlayerId);

        string nm = pc.Data != null ? pc.Data.PlayerName : "?";
        if (IsHost(pc)) nm += "  [H]";
        GUI.Label(new Rect(r.x + 50f, r.y, r.width - 184f, r.height), nm, _rowName);

        Color ac = NocturneStyle.Current.Accent;
        string tag = NocturneText.T("Зиплайн", "Zipline");
        if (SmallButton(new Rect(r.xMax - 132f, r.y + 3f, 62f, 24f), NocturneText.T("ВНИЗ ↓", "DOWN ↓"), ac))
            NocturneToast.Push(tag, Zipline.Ride(pc, true), 2.5f, NocturneNotifyKind.Info);
        if (SmallButton(new Rect(r.xMax - 66f, r.y + 3f, 62f, 24f), NocturneText.T("ВВЕРХ ↑", "UP ↑"), ac))
            NocturneToast.Push(tag, Zipline.Ride(pc, false), 2.5f, NocturneNotifyKind.Info);
        y += 32f;
    }

    private static PlayerControl _hostPc;
    private static int _hostFrame = -1;

    private static bool IsHost(PlayerControl pc)
    {
        if (_hostFrame != Time.frameCount)
        {
            _hostFrame = Time.frameCount;
            _hostPc = null;
            try
            {
                ClientData h = AmongUsClient.Instance != null ? AmongUsClient.Instance.GetHost() : null;
                if (h != null) _hostPc = h.Character;
            }
            catch { }
        }
        return _hostPc != null && _hostPc == pc;
    }

    private void RoleRow(float x, ref float y, float w, PlayerControl pc)
    {
        if (RowCull(ref y, 30f, 32f)) return;
        var r = new Rect(x, y, w, 30f);
        HoverFill(r);
        int colorId = pc.Data != null && pc.Data.DefaultOutfit != null ? pc.Data.DefaultOutfit.ColorId : 0;
        DrawColorDot(new Rect(r.x + 6f, r.y + 9f, 12f, 12f), colorId);
        GUI.Label(new Rect(r.x + 26f, r.y, r.width - 240f, r.height), pc.Data != null ? pc.Data.PlayerName : "?", _rowName);
        int idx = NocturneForceRoles.IndexOf(pc.PlayerId);
        Color col = idx == 0 ? new Color(0.5f, 0.55f, 0.62f) : NocturneStyle.Current.Accent;
        if (SmallButton(new Rect(r.xMax - 178f, r.y + 3f, 122f, 24f), NocturneForceRoles.Name(idx) + "  ▸", col))
            NocturneForceRoles.Cycle(pc.PlayerId);
        if (SmallButton(new Rect(r.xMax - 52f, r.y + 3f, 48f, 24f), NocturneText.T("ФОРС", "SET"), new Color(0.4f, 0.8f, 0.5f)))
            NocturneForceRoles.ForceNow(pc.PlayerId);
        y += 32f;
    }

    private void ImmortalRow(float x, ref float y, float w, PlayerControl pc, int ventId)
    {
        if (RowCull(ref y, 30f, 32f)) return;
        var r = new Rect(x, y, w, 30f);
        HoverFill(r);
        int colorId = pc.Data != null && pc.Data.DefaultOutfit != null ? pc.Data.DefaultOutfit.ColorId : 0;
        DrawColorDot(new Rect(r.x + 6f, r.y + 9f, 12f, 12f), colorId);

        bool me = pc == PlayerControl.LocalPlayer;
        bool on = NocturneGodMode.IsGranted(pc.PlayerId);

        if (!me)
        {
            bool mk = NocturneVentTp.IsMarked(pc.PlayerId);
            var box = new Rect(r.x + 24f, r.y + 5f, 20f, 20f);
            NocturneStyle.FillRounded(box, mk ? NocturneStyle.Current.Accent : A(Color.black, 0.25f), 5);
            if (mk) GUI.Label(box, "✔", _star);
            NocturneStyle.StrokeRounded(box, A(Color.white, 0.14f), 5, 1);
            if (GUI.Button(box, GUIContent.none, _invisible)) NocturneVentTp.ToggleMark(pc.PlayerId);
        }

        GUI.Label(new Rect(r.x + 50f, r.y, r.width - 212f, r.height), pc.Data != null ? pc.Data.PlayerName : "?", _rowName);

        if (me)
        {
            GUI.Label(new Rect(r.xMax - 158f, r.y, 154f, r.height), NocturneText.T("это ты", "you"), _muted);
        }
        else
        {
            if (SmallButton(new Rect(r.xMax - 158f, r.y + 3f, 96f, 24f), on ? NocturneText.T("СНЯТЬ", "REMOVE") : NocturneText.T("БЕСС.", "GOD"), on ? new Color(0.95f, 0.78f, 0.3f) : new Color(0.5f, 0.55f, 0.62f)))
                NocturneToast.Push(NocturneText.T("Бессмертие", "Immortality"), NocturneGodMode.Toggle(pc), 2.2f, NocturneNotifyKind.Info);
            if (SmallButton(new Rect(r.xMax - 58f, r.y + 3f, 54f, 24f), NocturneText.T("ВЕНТ", "VENT"), new Color(0.55f, 0.7f, 1f)))
                NocturneToast.Push(NocturneText.T("Вент-ТП", "Vent TP"), NocturneVentTp.Send(pc, ventId), 2.2f, NocturneNotifyKind.Info);
        }

        y += 32f;
    }

    private static InnerNetClient GuardNet()
    {
        try { return AmongUsClient.Instance == null ? null : (InnerNetClient)AmongUsClient.Instance; }
        catch { return null; }
    }

    private static string ClientInfo(ClientData c)
    {
        string s = string.Empty;
        try
        {
            string lvl = Patches.NocturneJoinLevels.Display(c);
            if (lvl != "?") s = NocturneText.T("ур.", "lvl") + lvl;
        }
        catch { }
        try
        {
            if (c.PlatformData != null)
            {
                string pl = Plat(c.PlatformData.Platform);
                if (pl.Length > 0) s = s.Length > 0 ? s + " · " + pl : pl;
            }
        }
        catch { }
        return s;
    }

    private static string Plat(Platforms p)
    {
        return (int)p switch
        {
            1 => "Epic",
            2 => "Steam",
            3 => "Mac",
            4 => "MS",
            5 => "Itch",
            6 => "iOS",
            7 => "Android",
            8 => "Switch",
            9 => "Xbox",
            10 => "PS",
            _ => string.Empty,
        };
    }

    private string _homeAbout, _homeImp;
    private float _homeAboutH, _homeImpH, _homeTextW = -1f;

    private void DrawHome(float x, ref float y, float w)
    {
        SubBar(x, ref y, w, HomeSubsRu, HomeSubsEn);

        string about = NocturneText.T(
            "<b>Nocturne</b> — клиент-сайд мод-меню для Among Us.",
            "<b>Nocturne</b> — a client-side mod menu for Among Us.");
        MeasureHome(about, w);
        float ah = _homeAboutH;
        Grp(0);
        Rect b = Card(x, ref y, w, NocturneText.T("О моде", "About"), ah + 4f * 30f + 8f);
        GUI.Label(new Rect(b.x, b.y, b.width, ah), about, _rowLabel);
        float by = b.y + ah + 8f;
        InfoRow(b.x, ref by, b.width, NocturneText.T("Версия", "Version"), "v" + NocturnePlugin.PluginVersion);
        InfoRow(b.x, ref by, b.width, NocturneText.T("Автор", "Author"), "Kawasaki");
        InfoRow(b.x, ref by, b.width, NocturneText.T("Контрибьютор", "Contributor"), "YosefUME");
        InfoRow(b.x, ref by, b.width, NocturneText.T("Тестер", "Tester"), "LITAFOM");

        Grp(1);
        b = Card(x, ref y, w, NocturneText.T("Горячие клавиши", "Hotkeys"), 12f * 30f);
        by = b.y;
        InfoRow(b.x, ref by, b.width, NocturneText.T("Меню", "Menu"), KeyDisp(NocturneConfig.MenuKey));
        InfoRow(b.x, ref by, b.width, NocturneText.T("Код лобби", "Lobby code"), KeyDisp(NocturneConfig.CopyCodeKey));
        InfoRow(b.x, ref by, b.width, NocturneText.T("Завершить матч", "End match"), KeyDisp(NocturneConfig.EndMatchKey));
        InfoRow(b.x, ref by, b.width, NocturneText.T("Досчитать голоса", "Tally votes"), KeyDisp(NocturneConfig.CloseVotingKey));
        InfoRow(b.x, ref by, b.width, NocturneText.T("Закрыть собрание", "Close meeting"), KeyDisp(NocturneConfig.CloseMeetingKey));
        InfoRow(b.x, ref by, b.width, NocturneText.T("Открыть плеер", "Open player"), KeyDisp(NocturneConfig.MusicToggleKey));
        InfoRow(b.x, ref by, b.width, NocturneText.T("Предыдущий трек", "Previous track"), KeyDisp(NocturneConfig.MusicPrevKey));
        InfoRow(b.x, ref by, b.width, NocturneText.T("Следующий трек", "Next track"), KeyDisp(NocturneConfig.MusicNextKey));
        InfoRow(b.x, ref by, b.width, NocturneText.T("Играть / Пауза", "Play / Pause"), KeyDisp(NocturneConfig.MusicPlayPauseKey));
        InfoRow(b.x, ref by, b.width, NocturneText.T("Стоп", "Stop"), KeyDisp(NocturneConfig.MusicStopKey));
        InfoRow(b.x, ref by, b.width, NocturneText.T("Громкость +", "Volume up"), KeyDisp(NocturneConfig.MusicVolumeUpKey));
        InfoRow(b.x, ref by, b.width, NocturneText.T("Громкость -", "Volume down"), KeyDisp(NocturneConfig.MusicVolumeDownKey));

        string imp = NocturneText.T(
            "<color=#FFD166>Мод может конфликтовать с другими модами. Перед запуском отключи или удали остальные — меньше вылетов и багов.</color>",
            "<color=#FFD166>The mod may conflict with other mods. Disable or remove other mods before launching to avoid crashes and bugs.</color>");
        float ih = _homeImpH;
        Grp(0);
        b = Card(x, ref y, w, NocturneText.T("Важно", "Important"), ih);
        GUI.Label(b, imp, _rowLabel);

        Grp(0);
        DrawUpdate(x, ref y, w);

        Grp(0);
        b = Card(x, ref y, w, NocturneText.T("Ссылки", "Links"), 3f * RowH);
        by = b.y;
        LinkRow(b.x, ref by, b.width, NocturneText.T("Сайт", "Website"), "https://onyxmenu.kawas-set.workers.dev");
        LinkRow(b.x, ref by, b.width, "Discord", "https://discord.gg/cP4MrVUfM7");
        LinkRow(b.x, ref by, b.width, "GitHub", "https://github.com/Veltrix-s/OnyxMenu");

        Grp(2);
        b = Card(x, ref y, w, NocturneText.T("Быстрые действия", "Quick actions"), 48f);
        ActionRow(b, NocturneIcon.Bell, NocturneText.T("Тест уведомления", "Test notification"), NocturneText.T("Проверить", "Test"), () => NocturneToast.Push(NocturneText.T("Nocturne на связи ✓", "Nocturne is live ✓")));
    }

    private void MeasureHome(string about, float w)
    {
        string imp = NocturneText.T(
            "<color=#FFD166>Мод может конфликтовать с другими модами. Перед запуском отключи или удали остальные — меньше вылетов и багов.</color>",
            "<color=#FFD166>The mod may conflict with other mods. Disable or remove other mods before launching to avoid crashes and bugs.</color>");
        if (Mathf.Abs(w - _homeTextW) < 0.5f && ReferenceEquals(about, _homeAbout) && ReferenceEquals(imp, _homeImp)) return;
        _homeTextW = w; _homeAbout = about; _homeImp = imp;
        float tw = w - 36f;
        _homeAboutH = _rowLabel.CalcHeight(new GUIContent(about), tw);
        _homeImpH = _rowLabel.CalcHeight(new GUIContent(imp), tw);
    }

    private void DrawUpdate(float x, ref float y, float w)
    {
        UpState st = NocturneUpdateCheck.State;
        Rect b = Card(x, ref y, w, NocturneText.T("Обновление", "Update"), 30f + 30f);
        float by = b.y;

        string info;
        switch (st)
        {
            case UpState.Checking: info = NocturneText.T("Проверяю…", "Checking…"); break;
            case UpState.Found: info = NocturneText.T("Доступна v", "Version v") + NocturneUpdateCheck.Latest; break;
            case UpState.Loading: info = NocturneText.T("Качаю…", "Downloading…"); break;
            case UpState.Done: info = NocturneText.T("Готово — перезапусти игру", "Done — restart the game"); break;
            case UpState.Fail: info = NocturneText.T("Ошибка: ", "Error: ") + NocturneUpdateCheck.Err; break;
            default: info = NocturneText.T("Установлена последняя: v", "Up to date: v") + NocturnePlugin.PluginVersion; break;
        }
        GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 24f), info, _muted);
        by += 30f;

        float cw = (b.width - 10f) / 2f;
        bool busy = st == UpState.Checking || st == UpState.Loading;
        if (SmallButton(new Rect(b.x, by, cw, 26f), NocturneText.T("ПРОВЕРИТЬ", "CHECK"), busy ? new Color(0.5f, 0.5f, 0.58f) : NocturneStyle.Current.Accent) && !busy)
            NocturneUpdateCheck.Recheck();

        if (st == UpState.Done)
        {
            if (SmallButton(new Rect(b.x + cw + 10f, by, cw, 26f), NocturneText.T("ВЫЙТИ ИЗ ИГРЫ", "QUIT GAME"), new Color(0.9f, 0.4f, 0.4f)))
                NocturneUpdateCheck.Restart();
        }
        else if (SmallButton(new Rect(b.x + cw + 10f, by, cw, 26f), NocturneText.T("СКАЧАТЬ", "DOWNLOAD"), st == UpState.Found ? new Color(0.30f, 0.72f, 0.40f) : new Color(0.5f, 0.5f, 0.58f)) && st == UpState.Found)
            NocturneUpdateCheck.Download();
    }

    private void LinkRow(float x, ref float y, float w, string label, string url)
    {
        NocturnePalette p = NocturneStyle.Current;
        var r = new Rect(x, y, w, RowH - 4f);
        bool hover = r.Contains(M);
        NocturneStyle.FillRounded(r, hover ? A(p.Accent, 0.18f) : A(Color.white, 0.04f), 8);
        NocturneStyle.StrokeRounded(r, hover ? A(p.Accent, 0.5f) : A(Color.white, 0.08f), 8, 1);
        GUI.Label(new Rect(r.x + 12f, r.y, r.width - 48f, r.height), label, _rowLabel);
        GUI.Label(new Rect(r.x + w - 40f, r.y, 34f, r.height), "↗", _value);
        if (!string.IsNullOrEmpty(url) && GUI.Button(r, GUIContent.none, _invisible))
            OpenLink(url);
        y += RowH;
    }

    private void DrawQoL(float x, ref float y, float w)
    {
        SubBar(x, ref y, w, QolSubsRu, QolSubsEn);

        Grp(0);
        Rect b = Card(x, ref y, w, NocturneText.T("Отображение", "Display"), 5f * RowH + 108f);
        float by = b.y;
        Toggle(b.x, ref by, b.width, NocturneText.T("Счётчик FPS", "FPS counter"), NocturneConfig.ShowFps);
        Toggle(b.x, ref by, b.width, NocturneText.T("Хост под FPS", "Host under FPS"), NocturneConfig.ShowHostLine);
        Toggle(b.x, ref by, b.width, NocturneText.T("Таймер лобби", "Lobby timer"), NocturneConfig.ShowLobbyTimer);
        Toggle(b.x, ref by, b.width, NocturneText.T("Уведомления", "Notifications"), NocturneConfig.Toasts);
        Toggle(b.x, ref by, b.width, NocturneText.T("Лок FPS на 30", "Lock FPS to 30"), NocturneConfig.FpsLock30);
        FpsSlider(b.x, ref by, b.width, NocturneText.T("Лимит FPS (при анлоке)", "FPS limit (unlocked)"), NocturneConfig.FpsCap);
        Slider(b.x, ref by, b.width, NocturneText.T("Масштаб HUD", "HUD scale"), NocturneConfig.HudScale, 0.6f, 2f, "0.00");

        Grp(1);
        b = Card(x, ref y, w, NocturneText.T("Чат", "Chat"), 9f * RowH);
        by = b.y;
        Toggle(b.x, ref by, b.width, NocturneText.T("Улучшенный чат", "Better chat"), NocturneConfig.BetterChat);
        Toggle(b.x, ref by, b.width, NocturneText.T("Инфо над пузырями (ур./платформа)", "Info above bubbles (lvl/platform)"), NocturneConfig.ChatBubbleSenderInfo);
        Toggle(b.x, ref by, b.width, NocturneText.T("Время под сообщением", "Timestamp under message"), NocturneConfig.ChatTimestamps);
        Toggle(b.x, ref by, b.width, NocturneText.T("Тёмный чат под тему", "Dark chat theme"), NocturneConfig.DarkChatTheme);
        Toggle(b.x, ref by, b.width, NocturneText.T("Чат всегда виден", "Chat always visible"), NocturneConfig.VisualAlwaysShowChat);
        Toggle(b.x, ref by, b.width, NocturneText.T("Видеть чат мёртвых", "See dead chat"), NocturneConfig.GhostChat);
        Toggle(b.x, ref by, b.width, NocturneText.T("Без лимита длины", "No length limit"), NocturneConfig.UnlimitedChatLength);
        Toggle(b.x, ref by, b.width, NocturneText.T("Без задержки чата", "No chat cooldown"), NocturneConfig.SkipChatCooldown);
        Toggle(b.x, ref by, b.width, NocturneText.T("Окно чата (перетаскиваемое)", "Chat window (draggable)"), NocturneConfig.ChatWindow);

        Grp(1);
        b = Card(x, ref y, w, NocturneText.T("Лог и фильтр чата", "Chat log & filter"), 6f * RowH + 46f);
        by = b.y;
        Toggle(b.x, ref by, b.width, NocturneText.T("Лог чата в файл", "Log chat to file"), NocturneConfig.ChatLog);
        Toggle(b.x, ref by, b.width, NocturneText.T("Цензура бан-слов", "Censor banned words"), NocturneConfig.BanWords);
        Toggle(b.x, ref by, b.width, NocturneText.T("Команда /xmas (хост)", "/xmas command (host)"), NocturneConfig.ChatCmdXmas);
        Toggle(b.x, ref by, b.width, NocturneText.T("Колор-команды /c /color", "Color commands /c /color"), NocturneConfig.ColorCmd);
        Toggle(b.x, ref by, b.width, NocturneText.T("Показывать кто вписал", "Notify who used it"), NocturneConfig.ColorCmdNotify);
        GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 20f), NocturneText.T("Хост красит любого, не-хост — себя. Пример: /c red", "Host colors anyone, non-host colors self. e.g. /c red"), _muted);
        by += 22f;
        Toggle(b.x, ref by, b.width, NocturneText.T("Команды хоста (/kick /role /start…)", "Host commands (/kick /role /start…)"), NocturneConfig.ChatCmds);
        GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 20f), NocturneText.T("Пиши /help в чат — список команд.", "Type /help in chat for the list."), _muted);

        bool ev = NocturneConfig.EventNotify.Value;
        Grp(0);
        b = Card(x, ref y, w, NocturneText.T("Уведомления о событиях", "Event notifications"), (ev ? 9f : 2f) * RowH);
        by = b.y;
        Toggle(b.x, ref by, b.width, NocturneText.T("Показывать уведомления", "Show notifications"), NocturneConfig.EventNotify);
        Toggle(b.x, ref by, b.width, NocturneText.T("Консоль событий (окно)", "Event console (window)"), NocturneConfig.EventConsole);
        if (ev)
        {
            Toggle(b.x, ref by, b.width, NocturneText.T("Дублировать в чат", "Also in chat"), NocturneConfig.EventNotifyChat);
            Toggle(b.x, ref by, b.width, NocturneText.T("Войткики", "Votekicks"), NocturneConfig.NotifyVotekick);
            Toggle(b.x, ref by, b.width, NocturneText.T("Саботаж", "Sabotage"), NocturneConfig.NotifySabotage);
            Toggle(b.x, ref by, b.width, NocturneText.T("Убийства", "Kills"), NocturneConfig.NotifyKill);
            Toggle(b.x, ref by, b.width, NocturneText.T("Собрания", "Meetings"), NocturneConfig.NotifyMeeting);
            Toggle(b.x, ref by, b.width, NocturneText.T("Изгнания", "Ejections"), NocturneConfig.NotifyEject);
            Toggle(b.x, ref by, b.width, NocturneText.T("Защита (блоки)", "Guard (blocks)"), NocturneConfig.SecurityNotify);
        }

        _roleClients.Clear();
        CollectPlayers(_roleClients);
        int wsCount = 0;
        for (int i = 0; i < _roleClients.Count; i++) if (_roleClients[i] != PlayerControl.LocalPlayer) wsCount++;
        Grp(1);
        b = Card(x, ref y, w, NocturneText.T("Шепот", "Whisper"), 26f + (wsCount > 0 ? wsCount * 32f : 26f));
        by = b.y;
        GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 22f), NocturneText.T("Или в чате: /w [ник или ID] сообщение", "Or in chat: /w [name or ID] message"), _muted);
        by += 26f;
        if (wsCount == 0)
            GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Нет других игроков.", "No other players."), _muted);
        else
            for (int i = 0; i < _roleClients.Count; i++)
                if (_roleClients[i] != PlayerControl.LocalPlayer) WhisperRow(b.x, ref by, b.width, _roleClients[i]);
    }

    private struct RoleDef
    {
        public string Ru, En;
        public RoleTypes Role;
        public bool Imp;
        public float DetailH;
        public RoleDef(string ru, string en, RoleTypes role, bool imp, float dh) { Ru = ru; En = en; Role = role; Imp = imp; DetailH = dh; }
    }

    private static readonly RoleDef[] HostRoles =
    {
        new RoleDef("Учёный", "Scientist", RoleTypes.Scientist, false, 100f),
        new RoleDef("Инженер", "Engineer", RoleTypes.Engineer, false, 100f),
        new RoleDef("Ангел", "Guardian Angel", RoleTypes.GuardianAngel, false, 134f),
        new RoleDef("Следопыт", "Tracker", RoleTypes.Tracker, false, 150f),
        new RoleDef("Паникёр", "Noisemaker", RoleTypes.Noisemaker, false, 84f),
        new RoleDef("Детектив", "Detective", RoleTypes.Detective, false, 50f),
        new RoleDef("Судья", "Judge", RoleTypes.Judge, false, 50f),
        new RoleDef("Оборотень", "Shapeshifter", RoleTypes.Shapeshifter, true, 134f),
        new RoleDef("Фантом", "Phantom", RoleTypes.Phantom, true, 100f),
        new RoleDef("Гадюка", "Viper", RoleTypes.Viper, true, 50f),
    };

    private void DrawHostTab(float x, ref float y, float w)
    {
        bool hostReady = NocturneLobbySettings.Ready();
        if (hostReady) SubBar(x, ref y, w, HostSubsRu, HostSubsEn);
        else _subTab[_tab] = 0;

        Grp(0);
        Rect rb = Card(x, ref y, w, NocturneText.T("Власть хоста", "Host powers"), 38f + RowH * 2f);
        float rby = rb.y;
        GUI.Label(new Rect(rb.x + 2f, rby, rb.width - 2f, 34f),
            NocturneText.T("Только хост, для своей катки.", "Host only, for your own game."), _muted);
        rby += 38f;
        Toggle(rb.x, ref rby, rb.width, NocturneText.T("Меня нельзя выгнать", "Immune to voting"), NocturneConfig.VoteImmune);
        Toggle(rb.x, ref rby, rb.width, NocturneText.T("Без репортов и собраний", "No reports & meetings"), NocturneConfig.NoReports);

        if (!hostReady)
        {
            Rect hb = Card(x, ref y, w, NocturneText.T("Правила лобби", "Lobby rules"), RowH + 24f);
            GUI.Label(new Rect(hb.x + 4f, hb.y, hb.width - 8f, 44f),
                NocturneText.T("Доступно только хосту в лобби. Создай комнату и стань хостом.",
                           "Host in lobby only. Create a room and become host."), _muted);
            return;
        }

        Grp(0);
        DrawPresets(x, ref y, w);

        Grp(1);
        Rect b = Card(x, ref y, w, NocturneText.T("Основное", "Basics"), 2f * RowH + 302f);
        float by = b.y;

        string[] maps = { "Skeld", "Mira HQ", "Polus", "Dleks", "Airship", "Fungle" };
        int mp = Mathf.Clamp(NocturneLobbySettings.Map(), 0, 5);
        CycleRow(b.x, ref by, b.width, NocturneText.T("Карта", "Map"), maps[mp], () => NocturneLobbySettings.SetMap((mp + 1) % 6));

        int pl = NocturneLobbySettings.Players();
        int nPl = SliderIntVal(b.x, ref by, b.width, NocturneText.T("Игроки", "Players"), pl, 4, 15, "");
        if (nPl != pl) NocturneLobbySettings.SetPlayers(nPl);

        int im = NocturneLobbySettings.Imps();
        int nIm = SliderIntVal(b.x, ref by, b.width, NocturneText.T("Импостеры", "Impostors"), im, 1, 3, "");
        if (nIm != im) NocturneLobbySettings.SetImps(nIm);

        float kc = NocturneLobbySettings.KillCd();
        float nKc = SliderFloatVal(b.x, ref by, b.width, NocturneText.T("Кулдаун убийства", "Kill cooldown"), kc, 0f, 60f, 0.1f, "0.0", NocturneText.T("с", "s"));
        if (Mathf.Abs(nKc - kc) > 0.001f) NocturneLobbySettings.SetKillCd(nKc);

        string[] dists = { NocturneText.T("Короткая", "Short"), NocturneText.T("Средняя", "Medium"), NocturneText.T("Длинная", "Long") };
        int kd = Mathf.Clamp(NocturneLobbySettings.KillDist(), 0, 2);
        CycleRow(b.x, ref by, b.width, NocturneText.T("Дистанция убийств", "Kill distance"), dists[kd], () => NocturneLobbySettings.SetKillDist((kd + 1) % 3));

        int sp = Mathf.RoundToInt(NocturneLobbySettings.Speed() * 100f);
        int nSp = SliderIntVal(b.x, ref by, b.width, NocturneText.T("Скорость", "Speed"), sp, 25, 300, "%");
        if (nSp != sp) NocturneLobbySettings.SetSpeed(nSp / 100f);

        int cv = Mathf.RoundToInt(NocturneLobbySettings.CrewVis() * 100f);
        int nCv = SliderIntVal(b.x, ref by, b.width, NocturneText.T("Обзор мирных", "Crew vision"), cv, 25, 500, "%");
        if (nCv != cv) NocturneLobbySettings.SetCrewVis(nCv / 100f);

        int iv = Mathf.RoundToInt(NocturneLobbySettings.ImpVis() * 100f);
        int nIv = SliderIntVal(b.x, ref by, b.width, NocturneText.T("Обзор предов", "Impostor vision"), iv, 25, 500, "%");
        if (nIv != iv) NocturneLobbySettings.SetImpVis(nIv / 100f);

        Grp(1);
        b = Card(x, ref y, w, NocturneText.T("Собрания и голосование", "Meetings & voting"), 2f * RowH + 210f);
        by = b.y;

        int me = NocturneLobbySettings.Meetings();
        int nMe = SliderIntVal(b.x, ref by, b.width, NocturneText.T("Экстренных собраний", "Emergency meetings"), me, 0, 15, "");
        if (nMe != me) NocturneLobbySettings.SetMeetings(nMe);

        int mc = NocturneLobbySettings.MeetingCd();
        int nMc = SliderIntVal(b.x, ref by, b.width, NocturneText.T("КД собрания", "Meeting cd"), mc, 0, 60, NocturneText.T("с", "s"));
        if (nMc != mc) NocturneLobbySettings.SetMeetingCd(nMc);

        int di = NocturneLobbySettings.Discuss();
        int nDi = SliderIntVal(b.x, ref by, b.width, NocturneText.T("Обсуждение", "Discussion"), di, 0, 120, NocturneText.T("с", "s"));
        if (nDi != di) NocturneLobbySettings.SetDiscuss(nDi);

        int vo = NocturneLobbySettings.Voting();
        int nVo = SliderIntVal(b.x, ref by, b.width, NocturneText.T("Голосование", "Voting"), vo, 0, 300, NocturneText.T("с", "s"));
        if (nVo != vo) NocturneLobbySettings.SetVoting(nVo);

        bool an = NocturneLobbySettings.Anon();
        bool nAn = ToggleVal(b.x, ref by, b.width, NocturneText.T("Анонимные голоса", "Anonymous votes"), an);
        if (nAn != an) NocturneLobbySettings.SetAnon(nAn);

        bool cf = NocturneLobbySettings.Confirm();
        bool nCf = ToggleVal(b.x, ref by, b.width, NocturneText.T("Подтверждать выброс", "Confirm ejects"), cf);
        if (nCf != cf) NocturneLobbySettings.SetConfirm(nCf);

        Grp(1);
        b = Card(x, ref y, w, NocturneText.T("Задания", "Tasks"), RowH + 194f);
        by = b.y;

        string[] taskBars = { NocturneText.T("Постоянно", "Always"), NocturneText.T("Только собрания", "Meetings only"), NocturneText.T("Скрыто", "Hidden") };
        int tb = Mathf.Clamp(NocturneLobbySettings.TaskBar(), 0, 2);
        CycleRow(b.x, ref by, b.width, NocturneText.T("Шкала заданий", "Task bar"), taskBars[tb], () => NocturneLobbySettings.SetTaskBar((tb + 1) % 3));

        int co = NocturneLobbySettings.Common();
        int nCo = SliderIntVal(b.x, ref by, b.width, NocturneText.T("Общие задания", "Common tasks"), co, 0, 8, "");
        if (nCo != co) NocturneLobbySettings.SetCommon(nCo);

        int lo = NocturneLobbySettings.Long();
        int nLo = SliderIntVal(b.x, ref by, b.width, NocturneText.T("Длинные задания", "Long tasks"), lo, 0, 8, "");
        if (nLo != lo) NocturneLobbySettings.SetLong(nLo);

        int sh = NocturneLobbySettings.Short();
        int nSh = SliderIntVal(b.x, ref by, b.width, NocturneText.T("Короткие задания", "Short tasks"), sh, 0, 12, "");
        if (nSh != sh) NocturneLobbySettings.SetShort(nSh);

        bool vi = NocturneLobbySettings.Visual();
        bool nVi = ToggleVal(b.x, ref by, b.width, NocturneText.T("Визуальные задания", "Visual tasks"), vi);
        if (nVi != vi) NocturneLobbySettings.SetVisual(nVi);

        float rolesBody = 24f + HostRoles.Length * RowH;
        foreach (RoleDef d in HostRoles) if (_roleOpen.Contains((int)d.Role)) rolesBody += d.DetailH;
        Grp(2);
        b = Card(x, ref y, w, NocturneText.T("Роли (кол-во/шанс)", "Roles (count/chance)"), rolesBody);
        by = b.y;
        GUI.Label(new Rect(b.x + b.width - 190f, by, 76f, 20f), NocturneText.T("Кол-во", "Count"), _centerMuted);
        GUI.Label(new Rect(b.x + b.width - 100f, by, 96f, 20f), NocturneText.T("Шанс", "Chance"), _centerMuted);
        by += 24f;
        Color crew = new Color(0.42f, 0.80f, 0.72f);
        Color imp = new Color(0.92f, 0.38f, 0.38f);
        foreach (RoleDef d in HostRoles)
        {
            bool open = RoleRow(b.x, ref by, b.width, d, d.Imp ? imp : crew);
            if (open) DrawRoleDetails(b.x, ref by, b.width, d.Role);
        }
    }

    private void DrawPresets(float x, ref float y, float w)
    {
        var names = NocturneLobbyPresets.Names();
        int pn = names.Count;
        Rect b = Card(x, ref y, w, NocturneText.T("Пресеты", "Presets"), 34f + (pn > 0 ? pn * RowH : RowH) + 6f);
        float by = b.y;

        _presetName = CustomText(new Rect(b.x + 2f, by, b.width - 128f, 26f), _presetName ?? "", "nocturnePreset");
        if (string.IsNullOrEmpty(_presetName) && _textFocus != "nocturnePreset")
            GUI.Label(new Rect(b.x + 10f, by, b.width - 140f, 26f), NocturneText.T("Имя пресета…", "Preset name…"), _muted);
        if (SmallButton(new Rect(b.x + b.width - 120f, by + 1f, 118f, 24f), NocturneText.T("СОХРАНИТЬ", "SAVE"), NocturneStyle.Current.Accent))
        {
            NocturneToast.Push(NocturneText.T("Пресет", "Preset"), NocturneLobbyPresets.Save(_presetName), 2.5f, NocturneNotifyKind.Info);
            _presetName = "";
            _textFocus = null;
        }
        by += 34f;

        if (pn == 0)
        {
            GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Пусто. Задай имя и сохрани текущие настройки.", "Empty. Name it and save current settings."), _muted);
            return;
        }

        for (int i = 0; i < pn; i++)
        {
            string name = names[i];
            var r = new Rect(b.x, by, b.width, RowH - 4f);
            HoverFill(r);
            if (GUI.Button(new Rect(r.x, r.y, r.width - 34f, r.height), GUIContent.none, _invisible))
                NocturneToast.Push(NocturneText.T("Пресет", "Preset"), NocturneLobbyPresets.Apply(name), 2f, NocturneNotifyKind.Success);
            NocturneStyle.FillRounded(new Rect(r.x + 10f, r.y + (r.height - 8f) / 2f, 8f, 8f), NocturneStyle.Current.Accent, 4);
            GUI.Label(new Rect(r.x + 26f, r.y, r.width - 70f, r.height), name, _rowLabel);
            GUI.Label(new Rect(r.x + r.width - 80f, r.y, 44f, r.height), "▸", _value);
            var del = new Rect(r.xMax - 28f, r.y + (r.height - 22f) / 2f, 24f, 22f);
            if (ArrowButton(del, "✕")) NocturneLobbyPresets.Delete(name);
            by += RowH;
        }
    }

    private bool RoleRow(float x, ref float y, float w, RoleDef d, Color team)
    {
        var r = new Rect(x, y, w, RowH - 4f);
        HoverFill(r);
        int key = (int)d.Role;
        bool open = _roleOpen.Contains(key);
        if (GUI.Button(new Rect(r.x, r.y, r.width - 200f, r.height), GUIContent.none, _invisible))
        {
            if (open) _roleOpen.Remove(key); else _roleOpen.Add(key);
            open = !open;
        }
        GUI.Label(new Rect(r.x + 6f, r.y, 16f, r.height), open ? "▾" : "▸", _centerMuted);
        NocturneStyle.FillRounded(new Rect(r.x + 24f, r.y + (r.height - 8f) / 2f, 8f, 8f), team, 4);
        GUI.Label(new Rect(r.x + 38f, r.y, r.width - 238f, r.height), NocturneText.T(d.Ru, d.En), _rowLabel);

        int cnt = NocturneLobbySettings.RoleNum(d.Role);
        int chc = NocturneLobbySettings.RoleChance(d.Role);
        int nCnt = Stepper(new Rect(r.xMax - 190f, r.y, 76f, r.height), cnt, 0, 15, 1, "");
        int nChc = Stepper(new Rect(r.xMax - 100f, r.y, 96f, r.height), chc, 0, 100, 10, "%");
        if (nCnt != cnt || nChc != chc) NocturneLobbySettings.SetRole(d.Role, nCnt, nChc);
        y += RowH;
        return open;
    }

    private void DrawRoleDetails(float x, ref float y, float w, RoleTypes role)
    {
        switch (role)
        {
            case RoleTypes.Scientist:
                RoleFloat(role, x, ref y, w, "Перезарядка сканера", "Scan cooldown", NocturneLobbySettings.SciCd, NocturneLobbySettings.SetSciCd, 0, 60, NocturneText.T("с", "s"));
                RoleFloat(role, x, ref y, w, "Время батареи", "Battery time", NocturneLobbySettings.SciBat, NocturneLobbySettings.SetSciBat, 0, 30, NocturneText.T("с", "s"));
                break;
            case RoleTypes.Engineer:
                RoleFloat(role, x, ref y, w, "Перезарядка", "Cooldown", NocturneLobbySettings.EngCd, NocturneLobbySettings.SetEngCd, 0, 60, NocturneText.T("с", "s"));
                RoleFloat(role, x, ref y, w, "Время в венте", "Vent time", NocturneLobbySettings.EngVent, NocturneLobbySettings.SetEngVent, 0, 60, NocturneText.T("с", "s"));
                break;
            case RoleTypes.GuardianAngel:
                RoleFloat(role, x, ref y, w, "Перезарядка", "Cooldown", NocturneLobbySettings.GaCd, NocturneLobbySettings.SetGaCd, 0, 60, NocturneText.T("с", "s"));
                RoleFloat(role, x, ref y, w, "Время щита", "Shield time", NocturneLobbySettings.GaDur, NocturneLobbySettings.SetGaDur, 0, 30, NocturneText.T("с", "s"));
                RoleBool(x, ref y, w, "Преды видят щит", "Impostors see shield", NocturneLobbySettings.GaImpSee, NocturneLobbySettings.SetGaImpSee);
                break;
            case RoleTypes.Tracker:
                RoleFloat(role, x, ref y, w, "Перезарядка", "Cooldown", NocturneLobbySettings.TrCd, NocturneLobbySettings.SetTrCd, 0, 60, NocturneText.T("с", "s"));
                RoleFloat(role, x, ref y, w, "Длительность", "Duration", NocturneLobbySettings.TrDur, NocturneLobbySettings.SetTrDur, 0, 60, NocturneText.T("с", "s"));
                RoleFloat(role, x, ref y, w, "Задержка метки", "Ping delay", NocturneLobbySettings.TrDelay, NocturneLobbySettings.SetTrDelay, 0, 30, NocturneText.T("с", "s"));
                break;
            case RoleTypes.Noisemaker:
                RoleFloat(role, x, ref y, w, "Длит. сигнала", "Alert duration", NocturneLobbySettings.NmDur, NocturneLobbySettings.SetNmDur, 0, 30, NocturneText.T("с", "s"));
                RoleBool(x, ref y, w, "Преды видят сигнал", "Impostors see alert", NocturneLobbySettings.NmImpAlert, NocturneLobbySettings.SetNmImpAlert);
                break;
            case RoleTypes.Detective:
                RoleFloat(role, x, ref y, w, "Лимит улик", "Suspect limit", NocturneLobbySettings.DetLimit, NocturneLobbySettings.SetDetLimit, 0, 10, "");
                break;
            case RoleTypes.Shapeshifter:
                RoleFloat(role, x, ref y, w, "Перезарядка", "Cooldown", NocturneLobbySettings.SsCd, NocturneLobbySettings.SetSsCd, 0, 60, NocturneText.T("с", "s"));
                RoleFloat(role, x, ref y, w, "Длительность", "Duration", NocturneLobbySettings.SsDur, NocturneLobbySettings.SetSsDur, 0, 60, NocturneText.T("с", "s"));
                RoleBool(x, ref y, w, "Оставлять облик", "Leave skin", NocturneLobbySettings.SsSkin, NocturneLobbySettings.SetSsSkin);
                break;
            case RoleTypes.Phantom:
                RoleFloat(role, x, ref y, w, "Перезарядка", "Cooldown", NocturneLobbySettings.PhCd, NocturneLobbySettings.SetPhCd, 0, 60, NocturneText.T("с", "s"));
                RoleFloat(role, x, ref y, w, "Длительность", "Duration", NocturneLobbySettings.PhDur, NocturneLobbySettings.SetPhDur, 0, 60, NocturneText.T("с", "s"));
                break;
            case RoleTypes.Viper:
                RoleFloat(role, x, ref y, w, "Время растворения", "Dissolve time", NocturneLobbySettings.VpDis, NocturneLobbySettings.SetVpDis, 0, 60, NocturneText.T("с", "s"));
                break;
            case RoleTypes.Judge:
                RoleFloat(role, x, ref y, w, "% открытых заданий", "Tasks opened %", NocturneLobbySettings.JudgeTaskPct, NocturneLobbySettings.SetJudgeTaskPct, 0, 100, "%");
                break;
        }
    }

    [HideFromIl2Cpp]
    private void RoleFloat(RoleTypes role, float x, ref float y, float w, string ru, string en, Func<float> get, Action<float> set, int min, int max, string suffix)
    {
        int v = Mathf.RoundToInt(get());
        int nv = SliderIntVal(x + 14f, ref y, w - 14f, NocturneText.T(ru, en), v, min, max, suffix, ru.GetHashCode() ^ (((int)role + 1) * 397));
        if (nv != v) set(nv);
    }

    [HideFromIl2Cpp]
    private void RoleBool(float x, ref float y, float w, string ru, string en, Func<bool> get, Action<bool> set)
    {
        bool v = get();
        bool nv = ToggleVal(x + 14f, ref y, w - 14f, NocturneText.T(ru, en), v);
        if (nv != v) set(nv);
    }

    private int Stepper(Rect r, int value, int min, int max, int step, string suffix)
    {
        value = Mathf.Clamp(value, min, max);
        var lb = new Rect(r.x, r.y + (r.height - 20f) / 2f, 20f, 20f);
        var rb = new Rect(r.xMax - 20f, r.y + (r.height - 20f) / 2f, 20f, 20f);
        if (ArrowButton(lb, "◂")) value = Mathf.Clamp(value - step, min, max);
        if (ArrowButton(rb, "▸")) value = Mathf.Clamp(value + step, min, max);
        GUI.Label(new Rect(lb.xMax, r.y, rb.x - lb.xMax, r.height), value + suffix, _valueC);
        return Mathf.Clamp(value, min, max);
    }

    private void DrawLobbyTab(float x, ref float y, float w)
    {
        SubBar(x, ref y, w, LobbySubsRu, LobbySubsEn);

        Grp(0);
        Rect b = Card(x, ref y, w, NocturneText.T("Косметика", "Cosmetics"), 3f * RowH);
        float by = b.y;
        Toggle(b.x, ref by, b.width, NocturneText.T("Водяной знак Nocturne", "Nocturne watermark"), NocturneConfig.LobbyBrand);
        Toggle(b.x, ref by, b.width, NocturneText.T("Лобби-бар (СТАРТ, чипы)", "Lobby bar (START, chips)"), NocturneConfig.LobbyBar);
        Toggle(b.x, ref by, b.width, NocturneText.T("Сенсорная кнопка меню", "Touch menu button"), NocturneConfig.MenuButton);

        Grp(0);
        b = Card(x, ref y, w, NocturneText.T("Старт", "Start"), 4f * RowH);
        by = b.y;
        Toggle(b.x, ref by, b.width, NocturneText.T("Разблок. кнопку Старт", "Unlock Start button"), NocturneConfig.AlwaysUnlockStartButton);
        Toggle(b.x, ref by, b.width, NocturneText.T("Старт по Enter", "Start on Enter"), NocturneConfig.QuickStartOnEnter);
        Toggle(b.x, ref by, b.width, NocturneText.T("Мгновенный старт (Enter)", "Instant start (Enter)"), NocturneConfig.InstantStartOnEnter);
        Toggle(b.x, ref by, b.width, NocturneText.T("Авто-возврат в лобби", "Auto-return to lobby"), NocturneConfig.AutoReturnLobbyAfterMatch);

        Grp(0);
        b = Card(x, ref y, w, NocturneText.T("Прочее", "Misc"), 2f * RowH + 56f);
        by = b.y;
        Toggle(b.x, ref by, b.width, NocturneText.T("Отключить музыку лобби", "Mute lobby music"), NocturneConfig.MuteLobbyMusic);
        Toggle(b.x, ref by, b.width, NocturneText.T("Расширенный браузер лобби", "Rich lobby browser"), NocturneConfig.RichLobbyRows);
        _lobbySearch = CustomText(new Rect(b.x + 2f, by, b.width - 92f, 26f), _lobbySearch ?? NocturneConfig.LobbySearchHost.Value ?? "", "lobbySearch");
        if (SmallButton(new Rect(b.x + b.width - 88f, by + 1f, 86f, 24f), NocturneText.T("ПОИСК", "SEARCH"), NocturneStyle.Current.Accent))
            NocturneConfig.LobbySearchHost.Value = (_lobbySearch ?? "").Trim();
        by += 30f;
        GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 22f), NocturneText.T("Фильтр браузера по нику хоста. Пусто — все лобби.", "Browser filter by host name. Empty = all lobbies."), _muted);

        Grp(1);
        b = Card(x, ref y, w, NocturneText.T("Автохост", "Auto-host"), 7f * RowH + 100f);
        by = b.y;
        Toggle(b.x, ref by, b.width, NocturneText.T("Включить автохост", "Enable auto-host"), NocturneConfig.AutoHostEnabled);
        Toggle(b.x, ref by, b.width, NocturneText.T("Мгновенный старт", "Instant start"), NocturneConfig.AutoHostInstantStart);
        Toggle(b.x, ref by, b.width, NocturneText.T("Возврат в лобби после матча", "Return to lobby after match"), NocturneConfig.AutoHostReturnAfterMatch);
        Toggle(b.x, ref by, b.width, NocturneText.T("Ждать загрузку игроков", "Wait for players to load"), NocturneConfig.AutoHostWaitLoadedPlayers);
        Toggle(b.x, ref by, b.width, NocturneText.T("Форс в последнюю минуту", "Force in last minute"), NocturneConfig.AutoHostForceLastMinute);
        Toggle(b.x, ref by, b.width, NocturneText.T("Уведомления автохоста", "Auto-host notifications"), NocturneConfig.AutoHostNotifications);
        bool gmBefore = NocturneConfig.GameMaster.Value;
        Toggle(b.x, ref by, b.width, NocturneText.T("Мастер игры", "Game master"), NocturneConfig.GameMaster);
        if (NocturneConfig.GameMaster.Value && !gmBefore) NocturneConfig.GhostAfterStart.Value = false;
        SliderInt(b.x, ref by, b.width, NocturneText.T("Минимум игроков", "Min players"), NocturneConfig.AutoHostMinPlayers, 1, 15);
        SliderInt(b.x, ref by, b.width, NocturneText.T("Задержка старта, с", "Start delay, s"), NocturneConfig.AutoHostStartDelaySeconds, 0, 180);

        Grp(1);
        b = Card(x, ref y, w, NocturneText.T("Манекены (хост)", "Dummies (host)"), 4f * RowH + 40f);
        by = b.y;
        Toggle(b.x, ref by, b.width, NocturneText.T("Включить манекенов", "Enable dummies"), NocturneConfig.DummyEnabled);
        Toggle(b.x, ref by, b.width, NocturneText.T("Делают таски", "Do tasks"), NocturneConfig.DummyDoTasks);
        Toggle(b.x, ref by, b.width, NocturneText.T("Чинят саботаж", "Fix sabotage"), NocturneConfig.DummyFixSabotage);
        Toggle(b.x, ref by, b.width, NocturneText.T("Репорт тел + чат + голос", "Report bodies + chat + vote"), NocturneConfig.DummyReportBodies);
        GUI.Label(new Rect(b.x + 2f, by, b.width - 160f, 24f), $"{NocturneText.T("Клавиша спавна", "Spawn key")}: <b>{KeyName(NocturneConfig.DummyKey)}</b>", _muted);
        if (SmallButton(new Rect(b.x + b.width - 150f, by, 148f, 24f), NocturneText.T("СПАВН МАНЕКЕНА", "SPAWN DUMMY"), NocturneStyle.Current.Accent))
            NocturneToast.Push(NocturneText.T("Манекен", "Dummy"), NocturneDummies.SpawnNow(), 2.5f, NocturneNotifyKind.Info);

        bool seekersOn = NocturneConfig.HideAndSeekTwoSeekers.Value;
        Grp(0);
        b = Card(x, ref y, w, NocturneText.T("Геймплей лобби", "Lobby gameplay"), 7f * RowH + 6f + (seekersOn ? RowH : 0f));
        by = b.y;
        Toggle(b.x, ref by, b.width, NocturneText.T("Без условий победы", "No win conditions"), NocturneConfig.NoWinConditions);
        Toggle(b.x, ref by, b.width, NocturneText.T("Кастом сикеры (прятки)", "Custom seekers (hide & seek)"), NocturneConfig.HideAndSeekTwoSeekers);
        if (seekersOn)
        {
            int sc = NocturneConfig.SeekerCount.Value;
            int nsc = SliderIntVal(b.x, ref by, b.width, NocturneText.T("Сикеров", "Seekers"), sc, 1, 15, "");
            if (nsc != sc) NocturneConfig.SeekerCount.Value = nsc;
        }
        Toggle(b.x, ref by, b.width, NocturneText.T("Пред без форы (прятки)", "Seeker: skip head start"), NocturneConfig.SeekerInstantStart);
        Toggle(b.x, ref by, b.width, NocturneText.T("4 импостера (≥9 игроков)", "4 impostors (≥9 players)"), NocturneConfig.FourImpostors);
        Toggle(b.x, ref by, b.width, NocturneText.T("Снять лимиты настроек", "Unlock option limits"), NocturneConfig.LooseHostOptions);
        Toggle(b.x, ref by, b.width, NocturneText.T("Шаг настроек 0.1", "Option step 0.1"), NocturneConfig.ForceMinValues);
        Toggle(b.x, ref by, b.width, NocturneText.T("Копировать код при дисконнекте", "Copy code on disconnect"), NocturneConfig.CopyCodeOnDisconnect);

        Grp(1);
        b = Card(x, ref y, w, NocturneText.T("Фейк-карта (хост)", "Fake map (host)"), RowH + 30f);
        by = b.y;
        string[] fmNames = { "The Skeld", "MIRA HQ", "Polus", "dlekS", "Airship", "Fungle" };
        int fmi = Mathf.Clamp(NocturneConfig.FakeMapId.Value, 0, fmNames.Length - 1);
        CycleRow(b.x, ref by, b.width, NocturneText.T("Карта", "Map"), fmNames[fmi], () => { NocturneConfig.FakeMapId.Value = (fmi + 1) % fmNames.Length; });
        bool fmOn = NocturneFakeMap.Active;
        if (SmallButton(new Rect(b.x + 2f, by, 176f, 24f), fmOn ? NocturneText.T("ВЫКЛ ФЕЙК-КАРТУ", "FAKE MAP OFF") : NocturneText.T("ВКЛ ФЕЙК-КАРТУ", "FAKE MAP ON"), fmOn ? new Color(0.9f, 0.4f, 0.4f) : NocturneStyle.Current.Accent))
        {
            if (fmOn) NocturneFakeMap.DisableAndRestoreLobby();
            else NocturneFakeMap.Enable(NocturneConfig.FakeMapId.Value);
        }

        int wt = Mathf.Clamp(NocturneConfig.LobbyWeather.Value, 0, 4);
        Grp(0);
        b = Card(x, ref y, w, NocturneText.T("Оформление лобби", "Lobby appearance"), 4f * RowH + 24f + (wt > 0 ? 50f : 0f));
        by = b.y;
        Toggle(b.x, ref by, b.width, NocturneText.T("Тёмная тема лобби", "Dark lobby theme"), NocturneConfig.LobbyTheme);
        Toggle(b.x, ref by, b.width, NocturneText.T("Своя картинка в главном меню", "Own main menu picture"), NocturneConfig.MainMenuArt);
        Toggle(b.x, ref by, b.width, NocturneText.T("Анимации старта и панели", "Start & panel animations"), NocturneConfig.LobbyAnims);
        string[] wNames =
        {
            NocturneText.T("Выкл", "Off"), NocturneText.T("Снег", "Snow"), NocturneText.T("Дождь", "Rain"),
            NocturneText.T("Листья", "Leaves"), NocturneText.T("Конфетти", "Confetti")
        };
        CycleRow(b.x, ref by, b.width, NocturneText.T("Погода", "Weather"), wNames[wt], () => { NocturneConfig.LobbyWeather.Value = (wt + 1) % 5; });
        if (wt > 0) SliderInt(b.x, ref by, b.width, NocturneText.T("Частиц", "Particles"), NocturneConfig.LobbySnowAmount, 10, 400);
        GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 22f), NocturneText.T("Погоду видишь только ты.", "Only you see the weather."), _muted);

        Grp(2);
        b = Card(x, ref y, w, NocturneText.T("Клоны лобби", "Lobby clones"), 6f * RowH + 482f + (NocturneConfig.CloneFormationAnim.Value ? 50f : 0f));
        by = b.y;
        Toggle(b.x, ref by, b.width, NocturneText.T("Режим клонов (ЛКМ=спавн, ПКМ=удал.)", "Clone mode (LMB=spawn, RMB=remove)"), NocturneConfig.LobbyCloneMode);
        Toggle(b.x, ref by, b.width, NocturneText.T("Клон-тень", "Shadow clone"), NocturneConfig.LobbyCloneShadow);
        Toggle(b.x, ref by, b.width, NocturneText.T("Клоны-охрана (кружат)", "Guard clones (orbit)"), NocturneConfig.LobbyCloneGuard);
        Toggle(b.x, ref by, b.width, NocturneText.T("Клоны бродят", "Clones wander"), NocturneConfig.LobbyCloneDrift);
        SliderInt(b.x, ref by, b.width, NocturneText.T("Макс. клонов", "Max clones"), NocturneConfig.LobbyCloneMax, 1, 2000);
        SliderInt(b.x, ref by, b.width, NocturneText.T("Клонов за клик", "Clones per click"), NocturneConfig.LobbyCloneSpawnCount, 1, 20);
        Slider(b.x, ref by, b.width, NocturneText.T("Радиус охраны", "Guard radius"), NocturneConfig.LobbyCloneGuardRadius, 1f, 8f, "0.0");
        Slider(b.x, ref by, b.width, NocturneText.T("Масштаб клонов", "Clone scale"), NocturneConfig.LobbyCloneScale, 0.4f, 2f, "0.00");
        SliderInt(b.x, ref by, b.width, NocturneText.T("Цвет (-1 = свой)", "Color (-1 = own)"), NocturneConfig.LobbyCloneColorId, -1, 17);

        string[] formNames = { NocturneText.T("Линия", "Line"), NocturneText.T("Круг", "Circle"), NocturneText.T("Треугольник", "Triangle"), NocturneText.T("Звезда", "Star"), NocturneText.T("Сердце", "Heart"), NocturneText.T("Ромб", "Diamond"), NocturneText.T("Спираль", "Spiral"), NocturneText.T("Крест", "Cross"), NocturneText.T("Волна", "Wave"), NocturneText.T("Дракон", "Dragon"), NocturneText.T("Персонаж", "Character"), NocturneText.T("Бесконечность", "Infinity"), NocturneText.T("Стрела", "Arrow"), NocturneText.T("Корона", "Crown"), NocturneText.T("Молния", "Lightning"), NocturneText.T("Цветок", "Flower"), NocturneText.T("Полумесяц", "Crescent"), NocturneText.T("Клевер", "Clover"), NocturneText.T("Ёлка", "Tree"), NocturneText.T("Квадрат", "Square"), NocturneText.T("Смайл", "Smiley"), NocturneText.T("Бабочка", "Butterfly"), NocturneText.T("Солнце", "Sun"), NocturneText.T("Пятиугольник", "Pentagon"), NocturneText.T("Икс", "X"), NocturneText.T("Сетка", "Grid"), NocturneText.T("Пакман", "Pacman"), NocturneText.T("Атом", "Atom"), NocturneText.T("Нота", "Note"), NocturneText.T("Знак вопроса", "Question"), NocturneText.T("Пульс", "Heartbeat") };
        int cfi = Mathf.Clamp(NocturneConfig.CloneFormation.Value, 0, formNames.Length - 1);
        CycleRow(b.x, ref by, b.width, NocturneText.T("Формация", "Formation"), formNames[cfi], () => { NocturneConfig.CloneFormation.Value = (cfi + 1) % formNames.Length; });
        Slider(b.x, ref by, b.width, NocturneText.T("Размер формации", "Formation size"), NocturneConfig.CloneFormationScale, 0.3f, 3f, "0.00");
        SliderInt(b.x, ref by, b.width, NocturneText.T("Копии формации", "Formation copies"), NocturneConfig.LobbyCloneFormationCopies, 1, 5);
        Toggle(b.x, ref by, b.width, NocturneText.T("Живые формации", "Living formations"), NocturneConfig.CloneFormationAnim);
        if (NocturneConfig.CloneFormationAnim.Value)
            Slider(b.x, ref by, b.width, NocturneText.T("Скорость движения", "Motion speed"), NocturneConfig.CloneFormationAnimSpeed, 0.2f, 3f, "0.0");
        if (SmallButton(new Rect(b.x + 2f, by, 132f, 24f), NocturneText.T("ПОСТРОИТЬ", "BUILD"), NocturneStyle.Current.Accent))
            NocturneLobbyClones.Instance?.BuildFormation(cfi);
        if (SmallButton(new Rect(b.x + 142f, by, 120f, 24f), NocturneText.T("ОЧИСТИТЬ", "CLEAR"), new Color(0.9f, 0.4f, 0.4f)))
            NocturneLobbyClones.Instance?.ClearAll();
        by += 32f;

        Toggle(b.x, ref by, b.width, NocturneText.T("Голые клоны (портрет красит по зонам)", "Bare clones (portrait tints by zone)"), NocturneConfig.CloneNaked);
        if (SmallButton(new Rect(b.x + 2f, by, b.width - 4f, 24f), NocturneText.T("Я ИЗ КЛОНОВ", "ME FROM CLONES"), new Color(0.78f, 0.42f, 0.95f)))
            NocturneLobbyClones.Instance?.BuildSelf();
        by += 32f;

        _cloneText = CustomText(new Rect(b.x + 2f, by, b.width - 168f, 26f), _cloneText ?? "", "cloneText");
        if (SmallButton(new Rect(b.x + b.width - 160f, by + 1f, 158f, 24f), NocturneText.T("ТЕКСТ ИЗ КЛОНОВ", "TEXT FROM CLONES"), NocturneStyle.Current.Accent))
            NocturneLobbyClones.Instance?.BuildText(_cloneText);

        bool ncReady = NocturneTwins.Ready();
        Grp(2);
        b = Card(x, ref y, w, NocturneText.T("Сетевые клоны", "Networked clones"), (ncReady ? 4f * RowH + 208f : 26f) + 32f);
        by = b.y;
        if (!ncReady)
        {
            GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Только хост в лобби.", "Host in lobby only."), _muted);
            by += 26f;
        }
        else
        {
            GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 22f), NocturneText.T("Копии тебя, видят все. Убираются на старте.", "Copies of you, everyone sees. Cleared on start."), _muted);
            by += 26f;
            Toggle(b.x, ref by, b.width, NocturneText.T("Режим клика (ЛКМ=спавн, ПКМ=удал.)", "Click mode (LMB=spawn, RMB=remove)"), NocturneConfig.NetCloneMode);

            PlayerControl nsrc = NetSrc();
            bool mine = nsrc == null || nsrc == PlayerControl.LocalPlayer;
            string sname = mine ? NocturneText.T("Я", "Me") : (nsrc.Data != null ? nsrc.Data.PlayerName : "?");
            CycleRow(b.x, ref by, b.width, NocturneText.T("Внешность", "Look like"), sname, NextNetSrc);
            if (!mine) DrawColorDot(new Rect(b.x + b.width - 132f, by - RowH + 11f, 12f, 12f), nsrc.Data != null && nsrc.Data.DefaultOutfit != null ? nsrc.Data.DefaultOutfit.ColorId : 0);

            int ncf = Mathf.Clamp(NocturneConfig.CloneFormation.Value, 0, formNames.Length - 1);
            CycleRow(b.x, ref by, b.width, NocturneText.T("Формация", "Formation"), formNames[ncf], () => { NocturneConfig.CloneFormation.Value = (ncf + 1) % formNames.Length; });
            Slider(b.x, ref by, b.width, NocturneText.T("Размер формации", "Formation size"), NocturneConfig.CloneFormationScale, 0.3f, 3f, "0.00");
            SliderInt(b.x, ref by, b.width, NocturneText.T("Клонов", "Clones"), NocturneConfig.NetCloneCount, 1, 2000);
            by += 4f;
            float ncw = (b.width - 10f) / 2f;
            if (SmallButton(new Rect(b.x, by, ncw, 26f), mine ? NocturneText.T("КЛОН СЕБЯ", "CLONE SELF") : NocturneText.T("КЛОН ЕГО", "CLONE THEM"), NocturneStyle.Current.Accent))
                NocturneToast.Push(NocturneText.T("Клоны", "Clones"), NocturneTwins.CloneOf(nsrc), 2.5f, NocturneNotifyKind.Info);
            if (SmallButton(new Rect(b.x + ncw + 10f, by, ncw, 26f), NocturneText.T("ФОРМАЦИЯ", "FORMATION"), new Color(0.4f, 0.75f, 1f)))
                NocturneToast.Push(NocturneText.T("Клоны", "Clones"), NocturneTwins.Formation(ncf, NocturneConfig.NetCloneCount.Value, nsrc), 2.5f, NocturneNotifyKind.Info);
            by += 32f;
            _netText = CustomText(new Rect(b.x, by, b.width - 132f, 26f), _netText ?? "", "netCloneText");
            if (SmallButton(new Rect(b.x + b.width - 128f, by + 1f, 128f, 24f), NocturneText.T("ТЕКСТ ИЗ КЛОНОВ", "TEXT FROM CLONES"), new Color(0.4f, 0.75f, 1f)))
                NocturneToast.Push(NocturneText.T("Клоны", "Clones"), NocturneTwins.Text(_netText, nsrc), 2.5f, NocturneNotifyKind.Info);
            by += 32f;
            string ncInfo = $"{NocturneText.T("Клонов", "Clones")}: <b>{NocturneTwins.Count}</b>";
            if (NocturneTwins.Queued > 0) ncInfo += $"   {NocturneText.T("в очереди", "queued")}: <b>{NocturneTwins.Queued}</b>";
            if (NocturneTwins.Figures > 0) ncInfo += $"   {NocturneText.T("фигур", "figures")}: <b>{NocturneTwins.Figures}</b>";
            GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 24f), ncInfo, _muted);
            by += 26f;
            float ncw2 = (b.width - 10f) / 2f;
            if (SmallButton(new Rect(b.x, by, ncw2, 26f), NocturneText.T("УБРАТЬ ПОСЛЕДНЮЮ", "REMOVE LAST"), new Color(0.92f, 0.62f, 0.30f)))
                NocturneToast.Push(NocturneText.T("Клоны", "Clones"), NocturneTwins.DropLast(), 2f, NocturneNotifyKind.Info);
            if (SmallButton(new Rect(b.x + ncw2 + 10f, by, ncw2, 26f), NocturneText.T("УБРАТЬ ВСЕХ", "CLEAR ALL"), new Color(0.9f, 0.4f, 0.4f)))
                NocturneTwins.ClearAll();
            by += 32f;
        }

        float lbw = (b.width - 10f) / 2f;
        if (SmallButton(new Rect(b.x, by, lbw, 26f), NocturneText.T("РАЗРУШИТЬ ЛОББИ", "DESTROY LOBBY"), new Color(0.9f, 0.4f, 0.4f)))
            NocturneToast.Push(NocturneText.T("Лобби", "Lobby"), NocturneLobbyTools.DestroyLobby(), 2.5f, NocturneNotifyKind.Warning);
        if (SmallButton(new Rect(b.x + lbw + 10f, by, lbw, 26f), NocturneText.T("СОЗДАТЬ ЛОББИ", "CREATE LOBBY"), NocturneStyle.Current.Accent))
            NocturneToast.Push(NocturneText.T("Лобби", "Lobby"), NocturneLobbyTools.CreateLobby(), 2.5f, NocturneNotifyKind.Success);

        Grp(1);
        b = Card(x, ref y, w, NocturneText.T("Спавнер карты (хост)", "Map spawner (host)"), 2f * RowH + 6f);
        by = b.y;
        string[] mapNames = { "The Skeld", "Mira HQ", "Polus", "dlekS", "Airship", "The Fungle" };
        int ms = Mathf.Clamp(_mapSel, 0, 5);
        CycleRow(b.x, ref by, b.width, NocturneText.T("Карта", "Map"), mapNames[ms], () => { _mapSel = (ms + 1) % 6; });
        float mcw = (b.width - 10f) / 2f;
        if (SmallButton(new Rect(b.x, by, mcw, 26f), NocturneText.T("УБРАТЬ КАРТУ", "DESPAWN MAP"), new Color(0.9f, 0.4f, 0.4f)))
            NocturneToast.Push(NocturneText.T("Карта", "Map"), NocturneLobbyTools.DespawnMap(), 2.5f, NocturneNotifyKind.Warning);
        if (SmallButton(new Rect(b.x + mcw + 10f, by, mcw, 26f), NocturneText.T("СПАВН КАРТЫ", "SPAWN MAP"), NocturneStyle.Current.Accent))
            NocturneToast.Push(NocturneText.T("Карта", "Map"), NocturneLobbyTools.SpawnMap(ms), 2.5f, NocturneNotifyKind.Success);

        IReadOnlyList<NocturneColorReservations.Entry> res = NocturneColorReservations.All();
        Grp(1);
        b = Card(x, ref y, w, NocturneText.T("Резерв цветов", "Color reservations"), 2f * RowH + (res.Count > 0 ? res.Count * 30f : 26f));
        by = b.y;
        Toggle(b.x, ref by, b.width, NocturneText.T("Закреплять цвет (хост)", "Reserve color (host)"), NocturneConfig.ColorReservationsEnabled);
        if (SmallButton(new Rect(b.x + 2f, by, 260f, 24f), NocturneText.T("ЗАРЕЗЕРВИРОВАТЬ ВЫБРАННОГО", "RESERVE SELECTED"), NocturneStyle.Current.Accent))
            ReserveSelectedColor();
        by += 30f;
        if (res.Count == 0)
            GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Пусто. Выбор игрока — ЛКМ (нужен «Выбор мышью»).", "Empty. Select a player with LMB (needs Mouse select)."), _muted);
        else
            for (int i = 0; i < res.Count; i++)
            {
                NocturneColorReservations.Entry en = res[i];
                var rr = new Rect(b.x, by, b.width, 28f);
                HoverFill(rr);
                DrawColorDot(new Rect(rr.x + 6f, rr.y + 8f, 12f, 12f), en.ColorId);
                GUI.Label(new Rect(rr.x + 26f, rr.y, rr.width - 66f, rr.height), $"<b>{en.Name}</b>   <color=#8A94AC><size=11>{en.Fc}</size></color>", _rowName);
                if (SmallButton(new Rect(rr.xMax - 38f, rr.y + 3f, 34f, 22f), "✕", new Color(0.9f, 0.4f, 0.4f))) { NocturneColorReservations.Remove(en.Fc); break; }
                by += 30f;
            }
    }

    private static readonly string[] CheatSubsRu = { "Собрания", "Игроки", "Себе", "Карта", "Задания", "Чат" };
    private static readonly string[] CheatSubsEn = { "Meetings", "Players", "Self", "Map", "Tasks", "Chat" };

    private static readonly string[] HomeSubsRu = { "О моде", "Клавиши", "Действия" };
    private static readonly string[] HomeSubsEn = { "About", "Keys", "Actions" };

    private static readonly string[] QolSubsRu = { "Интерфейс", "Чат" };
    private static readonly string[] QolSubsEn = { "Interface", "Chat" };

    private static readonly string[] LobbySubsRu = { "Основное", "Хост", "Клоны" };
    private static readonly string[] LobbySubsEn = { "Basics", "Host", "Clones" };

    private static readonly string[] PlayersSubsRu = { "Инфо", "Действия" };
    private static readonly string[] PlayersSubsEn = { "Info", "Actions" };

    private static readonly string[] VisualSubsRu = { "Камера", "Облик", "Эффекты" };
    private static readonly string[] VisualSubsEn = { "Camera", "Look", "Effects" };

    private static readonly string[] SettingsSubsRu = { "Интерфейс", "Клавиши", "Прочее" };
    private static readonly string[] SettingsSubsEn = { "Interface", "Keys", "Other" };

    private static readonly string[] HostSubsRu = { "Власть", "Настройки", "Роли" };
    private static readonly string[] HostSubsEn = { "Powers", "Options", "Roles" };

    private int SubBar(float x, ref float y, float w, string[] ru, string[] en)
    {
        string[] names = NocturneText.IsRussian ? ru : en;
        int cur = Mathf.Clamp(_subTab[_tab], 0, names.Length - 1);
        _subTab[_tab] = cur;

        if (NocturneStyle.Lite)
        {
            float segW = w / names.Length;
            for (int i = 0; i < names.Length; i++)
            {
                Color bg = GUI.backgroundColor;
                if (cur == i) GUI.backgroundColor = new Color(0.5f, 0.5f, 0.5f);
                if (GUI.Button(new Rect(x + i * segW, y, segW - 2f, 26f), names[i]) && cur != i)
                { _subTab[_tab] = i; _scroll = 0f; _scrollPending = 0f; }
                GUI.backgroundColor = bg;
            }
            y += 32f;
            return cur;
        }

        NocturnePalette p = NocturneStyle.Current;
        int cols = names.Length <= 5 ? names.Length : 4;
        int rows = Mathf.CeilToInt(names.Length / (float)cols);
        float rowH = 26f;

        for (int rr = 0; rr < rows; rr++)
        {
            int from = rr * cols;
            int cnt = Mathf.Min(cols, names.Length - from);
            var bar = new Rect(x, y + rr * (rowH + 4f), w, rowH);
            if (Cull(bar.y, bar.height)) continue;
            float segW = bar.width / cnt;

            NocturneStyle.FillRounded(bar, A(Color.white, 0.04f), 7);
            NocturneStyle.StrokeRounded(bar, A(Color.white, 0.07f), 7, 1);

            for (int c = 0; c < cnt; c++)
            {
                int i = from + c;
                var seg = new Rect(bar.x + c * segW, bar.y, segW, bar.height);
                bool on = cur == i;
                if (on)
                    NocturneStyle.FillRounded(new Rect(seg.x + 2f, seg.y + 2f, seg.width - 4f, seg.height - 4f), A(p.Accent, 0.22f), 6);
                else if (c > 0)
                    NocturneStyle.Fill(new Rect(seg.x, seg.y + 5f, 1f, seg.height - 10f), A(Color.white, 0.06f));
                _smallBtn.normal.textColor = on ? Color.Lerp(p.Accent, Color.white, 0.55f) : p.Muted;
                GUI.Label(seg, names[i], _smallBtn);
                if (GUI.Button(seg, GUIContent.none, _invisible) && !on)
                {
                    _subTab[_tab] = i;
                    _scroll = 0f;
                    _scrollPending = 0f;
                }
            }
        }
        y += rows * (rowH + 4f) + 6f;
        return cur;
    }

    private void DrawCheats(float x, ref float y, float w)
    {
        switch (SubBar(x, ref y, w, CheatSubsRu, CheatSubsEn))
        {
            case 1: CheatsPlayers(x, ref y, w); break;
            case 2: CheatsSelf(x, ref y, w); break;
            case 3: CheatsMap(x, ref y, w); break;
            case 4: CheatsTasks(x, ref y, w); break;
            case 5: CheatsChat(x, ref y, w); break;
            default: CheatsMeetings(x, ref y, w); break;
        }
    }

    private void CheatsMeetings(float x, ref float y, float w)
    {
        bool inMatch = ShipStatus.Instance != null;

        Rect mb = Card(x, ref y, w, NocturneText.T("Собрания", "Meetings"), RowH + 2f * 30f + 6f);
        float mby = mb.y;
        Toggle(mb.x, ref mby, mb.width, NocturneText.T("Спам-собрания", "Spam meetings"), NocturneConfig.NukeGame);
        mby += 4f;
        if (SmallButton(new Rect(mb.x, mby, mb.width, 26f), NocturneText.T("НАБРАТЬ СОБРАНИЕ", "CALL MEETING"), NocturneStyle.Current.Accent))
            NocturneToast.Push(NocturneText.T("Собрание", "Meeting"), NocturneMeetingTools.CallMeeting(), 2f, NocturneNotifyKind.Info);
        mby += 30f;
        float mbHalf = (mb.width - 8f) / 2f;
        if (SmallButton(new Rect(mb.x, mby, mbHalf, 26f), NocturneText.T("ЗАКРЫТЬ ГОЛОСОВАНИЕ", "CLOSE VOTING"), new Color(0.5f, 0.55f, 0.62f)))
            NocturneToast.Push(NocturneText.T("Голосование", "Voting"), NocturneMeetingTools.CloseVoting(), 2f, NocturneNotifyKind.Info);
        if (SmallButton(new Rect(mb.x + mbHalf + 8f, mby, mbHalf, 26f), NocturneText.T("ЗАКРЫТЬ СОБРАНИЕ", "CLOSE MEETING"), new Color(0.9f, 0.4f, 0.4f)))
            NocturneToast.Push(NocturneText.T("Собрание", "Meeting"), NocturneMeetingTools.CloseMeeting(), 2f, NocturneNotifyKind.Info);

        _roleClients.Clear();
        CollectPlayers(_roleClients);
        int vkCount = 0;
        for (int i = 0; i < _roleClients.Count; i++) if (_roleClients[i] != PlayerControl.LocalPlayer) vkCount++;
        float vkBody = 40f + 30f * 5f + RowH * 3f + (vkCount > 0 ? vkCount * 32f + 22f : 26f);
        Rect b = Card(x, ref y, w, NocturneText.T("Войткик", "Votekick"), vkBody);
        float by = b.y;
        GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 36f), NocturneText.T("Кик нужен от 3 разных клиентов. Авто: всем → выход → перезаход добирает.", "Kick needs 3 unique clients. Auto: vote all → leave → rejoin stacks."), _muted);
        by += 40f;
        if (SmallButton(new Rect(b.x, by, b.width, 26f), NocturneVotekick.Armed ? NocturneText.T("АВТО: ВКЛ — СТОП", "AUTO: ON — STOP") : NocturneText.T("АВТО-ВОЙТКИК", "AUTO VOTEKICK"), NocturneVotekick.Armed ? new Color(0.9f, 0.4f, 0.4f) : NocturneStyle.Current.Accent))
            NocturneVotekick.ToggleAuto();
        by += 30f;
        if (SmallButton(new Rect(b.x, by, b.width, 26f), NocturneText.T("ГОЛОСА ВСЕМ + ОСТАТЬСЯ", "VOTE ALL + STAY"), NocturneStyle.Current.Accent))
            NocturneVotekick.VoteAllStay();
        by += 30f;
        if (SmallButton(new Rect(b.x, by, b.width, 26f), NocturneText.T("ЗАЯВИТЬ ВСЕМ ПО ОЧЕРЕДИ", "VOTE EACH IN TURN"), new Color(0.78f, 0.42f, 0.95f)))
            NocturneVotekick.RapidAll();
        by += 30f;
        string autoLbl = NocturneVotekick.AutoTargeting
            ? NocturneText.T("АВТО ПО ЦЕЛЯМ: СТОП", "AUTO TARGETS: STOP")
            : NocturneText.T("АВТО ПО ЦЕЛЯМ", "AUTO TARGETS") + " (" + NocturneVotekick.TargetCount + ")";
        if (SmallButton(new Rect(b.x, by, b.width - 130f, 26f), autoLbl, NocturneVotekick.AutoTargeting ? new Color(0.9f, 0.4f, 0.4f) : NocturneStyle.Current.Accent))
            NocturneVotekick.ToggleTargetAuto();
        if (SmallButton(new Rect(b.x + b.width - 124f, by, 124f, 26f), NocturneText.T("СБРОС ЦЕЛЕЙ", "CLEAR TARGETS"), new Color(0.5f, 0.5f, 0.58f)))
            NocturneVotekick.ClearTargets();
        by += 30f;
        bool hostSel = NocturneVotekick.HostIsTarget();
        if (SmallButton(new Rect(b.x, by, b.width, 26f), hostSel ? NocturneText.T("ПРЕСЕТ ХОСТ ✓ — СНЯТЬ", "PRESET HOST ✓ — REMOVE") : NocturneText.T("ПРЕСЕТ: ОТМЕТИТЬ ХОСТА", "PRESET: MARK HOST"), hostSel ? NocturneStyle.Current.Accent : new Color(0.92f, 0.62f, 0.30f)))
            NocturneVotekick.ToggleHostTarget();
        by += 30f;
        Toggle(b.x, ref by, b.width, NocturneText.T("Копировать код лобби", "Copy lobby code"), NocturneConfig.VkCopyCode);
        Toggle(b.x, ref by, b.width, NocturneText.T("Авто-перезаход по коду", "Auto rejoin by code"), NocturneConfig.VkRejoin);
        Toggle(b.x, ref by, b.width, NocturneText.T("Перезаход если войткикают (2 голоса)", "Rejoin if votekicked (2 votes)"), NocturneConfig.VkAutoRejoin);
        if (vkCount == 0)
            GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Нет других игроков.", "No other players."), _muted);
        else
        {
            GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 20f), NocturneText.T("Выборочно — отметь «АВТО» у нужных:", "Selective — mark AUTO on players:"), _muted);
            by += 22f;
            for (int i = 0; i < _roleClients.Count; i++)
                if (_roleClients[i] != PlayerControl.LocalPlayer) VotekickRow(b.x, ref by, b.width, _roleClients[i]);
        }

        List<string> pend = NocturneJudgeOverrule.Lines;
        int jTotal = NocturneJudgeOverrule.Total;
        float jBody = RowH + 24f + (pend.Count > 0 ? pend.Count * 20f + 4f : 22f) + 30f + (jTotal > 0 ? 22f : 0f);
        b = Card(x, ref y, w, NocturneText.T("Судья: оверрул", "Judge: overrule"), jBody);
        by = b.y;
        Toggle(b.x, ref by, b.width, NocturneText.T("Следить за оверрулами", "Watch overrules"), NocturneConfig.JudgeWatch);
        GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 22f),
            NocturneText.T("Очередь видит хост — цель судьи до удара молотка.", "The host sees the queue — the judge's target before the gavel."), _muted);
        by += 24f;
        if (pend.Count == 0)
        {
            GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 20f), NocturneText.T("Оверрулов нет.", "No overrules."), _muted);
            by += 22f;
        }
        else
        {
            for (int i = 0; i < pend.Count; i++)
            {
                GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 20f), pend[i], _rowName);
                by += 20f;
            }
            by += 4f;
        }
        if (SmallButton(new Rect(b.x, by, b.width, 26f), NocturneText.T("СБРОСИТЬ ОЧЕРЕДЬ (ХОСТ)", "CLEAR QUEUE (HOST)"), new Color(0.5f, 0.55f, 0.62f)))
            NocturneToast.Push(NocturneText.T("Судья", "Judge"), NocturneJudgeOverrule.ClearAll(), 2f, NocturneNotifyKind.Info);
        by += 30f;
        if (jTotal > 0)
            GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 20f), NocturneText.T("Оверрулов за матч: ", "Overrules this match: ") + jTotal, _muted);

        b = Card(x, ref y, w, NocturneText.T("Собрание: свободный ход", "Meeting: free roam"), inMatch ? 28f + 26f + 30f : 26f);
        by = b.y;
        if (!inMatch)
            GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Только в матче.", "In match only."), _muted);
        else
        {
            GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 24f),
                NocturneText.T("Закрыть собрание у себя — ходишь, пока у других идёт.", "Close the meeting locally — roam while others are stuck."), _muted);
            by += 26f;
            if (SmallButton(new Rect(b.x, by, b.width, 26f), NocturneText.T("ВЫЙТИ И ХОДИТЬ", "EXIT & ROAM"), NocturneStyle.Current.Accent))
                NocturneToast.Push(NocturneText.T("Собрание", "Meeting"), NocturneMeetingRoam.Roam(), 2.5f, NocturneNotifyKind.Info);
        }
    }

    private void CheatsPlayers(float x, ref float y, float w)
    {
        bool inMatch = ShipStatus.Instance != null;
        Color red = new Color(0.9f, 0.4f, 0.4f);

        _roleClients.Clear();
        CollectPlayers(_roleClients);
        int loopN = 0;
        for (int i = 0; i < _roleClients.Count; i++) if (_roleClients[i] != PlayerControl.LocalPlayer) loopN++;

        Rect b = Card(x, ref y, w, NocturneText.T("Рофл: цикл смерти", "Prank: murder loop"), inMatch ? 38f + (loopN > 0 ? loopN * 32f : 26f) : 26f);
        float by = b.y;
        if (!inMatch)
            GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Только в матче.", "In match only."), _muted);
        else
        {
            GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 34f),
                NocturneText.T("Только хост, только для своих — в пабликах забанят.", "Host only, friends only — publics will ban you."), _muted);
            by += 38f;
            if (loopN == 0)
                GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Нет других игроков.", "No other players."), _muted);
            else
                for (int i = 0; i < _roleClients.Count; i++)
                    if (_roleClients[i] != PlayerControl.LocalPlayer) LoopRow(b.x, ref by, b.width, _roleClients[i]);
        }

        _roleClients.Clear();
        CollectPlayers(_roleClients);
        int killN = 0;
        for (int i = 0; i < _roleClients.Count; i++) if (_roleClients[i] != PlayerControl.LocalPlayer) killN++;

        b = Card(x, ref y, w, NocturneText.T("Мгновенный килл", "Instant kill"), inMatch ? 68f + (killN > 0 ? killN * 32f : 26f) : 26f);
        by = b.y;
        if (!inMatch)
            GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Только в матче.", "In match only."), _muted);
        else
        {
            GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 34f),
                NocturneText.T("Только хост, для своих. В пабликах — бан.", "Host only, friends only. Publics = ban."), _muted);
            by += 38f;
            float third = (b.width - 8f) / 3f;
            if (SmallButton(new Rect(b.x, by, third, 26f), NocturneText.T("ВСЕХ", "ALL"), red))
                NocturneToast.Push(NocturneText.T("Килл", "Kill"), NocturneKillTools.KillAll(0), 2f, NocturneNotifyKind.Info);
            if (SmallButton(new Rect(b.x + third + 4f, by, third, 26f), NocturneText.T("МИРНЫХ", "CREW"), new Color(0.92f, 0.62f, 0.30f)))
                NocturneToast.Push(NocturneText.T("Килл", "Kill"), NocturneKillTools.KillAll(1), 2f, NocturneNotifyKind.Info);
            if (SmallButton(new Rect(b.x + third * 2f + 8f, by, b.width - third * 2f - 8f, 26f), NocturneText.T("ПРЕДОВ", "IMPS"), new Color(0.78f, 0.42f, 0.95f)))
                NocturneToast.Push(NocturneText.T("Килл", "Kill"), NocturneKillTools.KillAll(2), 2f, NocturneNotifyKind.Info);
            by += 30f;
            if (killN == 0)
                GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Нет других игроков.", "No other players."), _muted);
            else
                for (int i = 0; i < _roleClients.Count; i++)
                    if (_roleClients[i] != PlayerControl.LocalPlayer) KillRow(b.x, ref by, b.width, _roleClients[i]);
        }

        _roleClients.Clear();
        CollectPlayers(_roleClients);
        int vkN = 0;
        for (int i = 0; i < _roleClients.Count; i++) if (_roleClients[i] != PlayerControl.LocalPlayer) vkN++;

        float vkkBody = vkN > 0 ? 88f + vkN * 32f : 54f;
        b = Card(x, ref y, w, NocturneText.T("Вент кик", "Vent kick"), inMatch ? vkkBody : 26f);
        by = b.y;
        if (!inMatch)
            GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Только в матче.", "In match only."), _muted);
        else
        {
            GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 24f),
                NocturneText.T("Вент-кик.", "Vent kick."), _muted);
            by += 28f;
            if (vkN == 0)
                GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Нет других игроков.", "No other players."), _muted);
            else
            {
                float vkSelw = (b.width - 8f) / 2f;
                if (SmallButton(new Rect(b.x, by, vkSelw, 24f), NocturneText.T("ВЫБРАТЬ ВСЕХ", "SELECT ALL"), NocturneStyle.Current.Accent))
                    NocturneVentKick.SelectAll();
                if (SmallButton(new Rect(b.x + vkSelw + 8f, by, vkSelw, 24f), NocturneText.T("СНЯТЬ ВСЕ", "CLEAR ALL"), new Color(0.9f, 0.4f, 0.4f)))
                    NocturneVentKick.ClearSelection();
                by += 30f;
                string vkLbl = NocturneText.T("КИК ПО ВЫБРАННЫМ", "KICK SELECTED") + (NocturneVentKick.SelectedCount > 0 ? $" ({NocturneVentKick.SelectedCount})" : "");
                if (SmallButton(new Rect(b.x, by, b.width, 24f), vkLbl, new Color(0.95f, 0.5f, 0.25f)))
                    NocturneToast.Push(NocturneText.T("Вент кик", "Vent kick"), NocturneVentKick.KickSelected(), 2.4f, NocturneNotifyKind.Info);
                by += 30f;
                for (int i = 0; i < _roleClients.Count; i++)
                    if (_roleClients[i] != PlayerControl.LocalPlayer) VentKickRow(b.x, ref by, b.width, _roleClients[i]);
            }
        }

        _roleClients.Clear();
        CollectPlayers(_roleClients);
        int jailN = 0;
        for (int i = 0; i < _roleClients.Count; i++) if (_roleClients[i] != PlayerControl.LocalPlayer) jailN++;

        float jailBody = jailN > 0 ? 88f + jailN * 32f : 82f;
        b = Card(x, ref y, w, NocturneText.T("Тюрьма", "Jail"), inMatch ? jailBody : 26f);
        by = b.y;
        if (!inMatch)
            GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Только в матче.", "In match only."), _muted);
        else
        {
            GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 24f),
                NocturneText.T("Держит отмеченных в комнате — выйдут, закинет обратно в вент.", "Keeps marked players in the room — leaving forces them back into a vent."), _muted);
            by += 28f;

            float jrNameW = b.width - 60f;
            if (SmallButton(new Rect(b.x, by, 26f, 24f), "◄", new Color(0.5f, 0.55f, 0.62f))) NocturneJail.CellStep(-1);
            GUI.Label(new Rect(b.x + 30f, by, jrNameW, 24f), NocturneJail.CellName(), _centerMuted);
            if (SmallButton(new Rect(b.x + b.width - 26f, by, 26f, 24f), "►", new Color(0.5f, 0.55f, 0.62f))) NocturneJail.CellStep(1);
            by += 30f;

            if (jailN == 0)
                GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Нет других игроков.", "No other players."), _muted);
            else
            {
                float jSelw = (b.width - 8f) / 2f;
                if (SmallButton(new Rect(b.x, by, jSelw, 24f), NocturneText.T("ВЫБРАТЬ ВСЕХ", "SELECT ALL"), NocturneStyle.Current.Accent))
                    NocturneJail.SelectAll();
                if (SmallButton(new Rect(b.x + jSelw + 8f, by, jSelw, 24f), NocturneText.T("СНЯТЬ ВСЕ", "CLEAR ALL"), new Color(0.9f, 0.4f, 0.4f)))
                    NocturneJail.ClearTargets();
                by += 30f;
                for (int i = 0; i < _roleClients.Count; i++)
                    if (_roleClients[i] != PlayerControl.LocalPlayer) JailRow(b.x, ref by, b.width, _roleClients[i]);
            }
        }

        _roleClients.Clear();
        CollectPlayers(_roleClients);
        int blN = 0;
        for (int i = 0; i < _roleClients.Count; i++) if (_roleClients[i] != PlayerControl.LocalPlayer) blN++;

        float blBody = blN > 0 ? 88f + blN * 32f : 54f;
        b = Card(x, ref y, w, NocturneText.T("Слепота / фулбрайт", "Blind / fullbright"), inMatch ? blBody : 26f);
        by = b.y;
        if (!inMatch)
            GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Только в матче.", "In match only."), _muted);
        else
        {
            GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 24f),
                NocturneText.T("Тьма или слепящий свет.", "Darkness or blinding light."), _muted);
            by += 28f;
            if (blN == 0)
                GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Нет других игроков.", "No other players."), _muted);
            else
            {
                float blSelw = (b.width - 8f) / 2f;
                if (SmallButton(new Rect(b.x, by, blSelw, 24f), NocturneText.T("ВЫБРАТЬ ВСЕХ", "SELECT ALL"), NocturneStyle.Current.Accent))
                    NocturneBlind.SelectAll();
                if (SmallButton(new Rect(b.x + blSelw + 8f, by, blSelw, 24f), NocturneText.T("СНЯТЬ ВСЕ", "CLEAR ALL"), new Color(0.9f, 0.4f, 0.4f)))
                    NocturneBlind.ClearSelection();
                by += 30f;
                string blLbl = NocturneText.T("ОСЛЕПИТЬ ВЫБРАННЫХ", "BLIND SELECTED") + (NocturneBlind.SelectedCount > 0 ? $" ({NocturneBlind.SelectedCount})" : "");
                float blActw = (b.width - 8f) / 2f;
                if (SmallButton(new Rect(b.x, by, blActw, 24f), blLbl, new Color(0.4f, 0.42f, 0.48f)))
                    NocturneToast.Push(NocturneText.T("Свет", "Vision"), NocturneBlind.BlindSelected(), 2.2f, NocturneNotifyKind.Info);
                if (SmallButton(new Rect(b.x + blActw + 8f, by, blActw, 24f), NocturneText.T("ВЕРНУТЬ ВЫБРАННЫМ", "RESTORE SELECTED"), NocturneStyle.Current.Accent))
                    NocturneToast.Push(NocturneText.T("Свет", "Vision"), NocturneBlind.RestoreSelected(), 2.2f, NocturneNotifyKind.Info);
                by += 30f;
                for (int i = 0; i < _roleClients.Count; i++)
                    if (_roleClients[i] != PlayerControl.LocalPlayer) BlindRow(b.x, ref by, b.width, _roleClients[i]);
            }
        }

        int petN = 0;
        for (int i = 0; i < _roleClients.Count; i++) if (_roleClients[i] != PlayerControl.LocalPlayer) petN++;
        b = Card(x, ref y, w, NocturneText.T("Пет-рука", "Pet hand"), 28f + 26f + 30f + 30f + 30f + (petN > 0 ? petN * 32f : 0f));
        by = b.y;
        GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Джойстик слева-снизу, роспись мышью или ГЛАДИТЬ игрока.", "Joystick bottom-left, paint with mouse, or PET a player."), _muted);
        by += 26f;
        float ptw2 = (b.width - 8f) / 2f;
        bool petMan = NocturnePet.On && NocturnePet.Manual;
        if (SmallButton(new Rect(b.x, by, ptw2, 24f), petMan ? NocturneText.T("РУЧНОЙ: ВКЛ", "MANUAL: ON") : NocturneText.T("РУЧНОЙ ДЖОЙСТИК", "MANUAL JOYSTICK"), petMan ? NocturneStyle.Current.Accent : new Color(0.5f, 0.55f, 0.62f)))
            NocturneToast.Push(NocturneText.T("Пет", "Pet"), NocturnePet.ToggleManual(), 2f, NocturneNotifyKind.Info);
        if (SmallButton(new Rect(b.x + ptw2 + 8f, by, ptw2, 24f), NocturneText.T("СТОП", "STOP"), new Color(0.9f, 0.4f, 0.4f)))
            NocturnePet.Stop();
        by += 30f;
        bool petPaint = NocturnePet.On && NocturnePet.Paint;
        if (SmallButton(new Rect(b.x, by, ptw2, 24f), petPaint ? NocturneText.T("РОСПИСЬ: ВКЛ", "PAINT: ON") : NocturneText.T("РОСПИСЬ", "PAINT"), petPaint ? NocturneStyle.Current.Accent : new Color(0.5f, 0.55f, 0.62f)))
            NocturneToast.Push(NocturneText.T("Пет", "Pet"), NocturnePet.TogglePaint(), 2.5f, NocturneNotifyKind.Info);
        if (SmallButton(new Rect(b.x + ptw2 + 8f, by, ptw2, 24f), NocturneText.T("ОЧИСТИТЬ", "CLEAR") + $" ({NocturnePet.PaintCount})", new Color(0.5f, 0.55f, 0.62f)))
            NocturneToast.Push(NocturneText.T("Пет", "Pet"), NocturnePet.ClearPaint(), 2f, NocturneNotifyKind.Info);
        by += 30f;
        float rNameW = b.width - 60f;
        if (SmallButton(new Rect(b.x, by, 26f, 24f), "◄", new Color(0.5f, 0.55f, 0.62f))) NocturnePet.RoomStep(-1);
        GUI.Label(new Rect(b.x + 30f, by, rNameW, 24f), NocturnePet.RoomName(), _centerMuted);
        if (SmallButton(new Rect(b.x + b.width - 26f, by, 26f, 24f), "►", new Color(0.5f, 0.55f, 0.62f))) NocturnePet.RoomStep(1);
        by += 28f;
        if (SmallButton(new Rect(b.x, by, b.width, 24f), NocturneText.T("ЗАЛИТЬ КОМНАТУ ПЕТОМ", "FILL ROOM WITH PET"), NocturneStyle.Current.Accent))
            NocturneToast.Push(NocturneText.T("Пет", "Pet"), NocturnePet.FillRoom(), 2.5f, NocturneNotifyKind.Info);
        by += 30f;
        for (int i = 0; i < _roleClients.Count; i++)
            if (_roleClients[i] != PlayerControl.LocalPlayer) PetRow(b.x, ref by, b.width, _roleClients[i]);

        string[] frameSystems = NocturneText.IsRussian
            ? new[] { "Sabotage (надёжно)", "Reactor (16 или 128)", "Электрика (>5)" }
            : new[] { "Sabotage (reliable)", "Reactor (16 or 128)", "Electrical (>5)" };
        SystemTypes[] frameSystemValues = { SystemTypes.Sabotage, SystemTypes.Reactor, SystemTypes.Electrical };
        _roleClients.Clear();
        CollectPlayers(_roleClients);
        int frameCount = 0;
        for (int i = 0; i < _roleClients.Count; i++) if (_roleClients[i] != PlayerControl.LocalPlayer) frameCount++;

        b = Card(x, ref y, w, NocturneText.T("Подстава саботажа", "Frame sabotage"), inMatch ? 132f + (frameCount > 0 ? frameCount * 32f : RowH) : 26f);
        by = b.y;
        if (!inMatch)
            GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Только в матче.", "In match only."), _muted);
        else
        {
            GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 34f),
                NocturneText.T("Подделывает actor в системном RPC — ловит только чужой guard, не проверено. Sabotage флагает не-импостора при любом значении.", "Fakes the actor in a system RPC — only catches a foreign guard, untested. Sabotage flags a non-impostor at any value."), _muted);
            by += 38f;
            CycleRow(b.x, ref by, b.width, NocturneText.T("Система", "System"), frameSystems[_frameSystemIdx], () => _frameSystemIdx = (_frameSystemIdx + 1) % frameSystems.Length);
            _frameValue = SliderIntVal(b.x, ref by, b.width, NocturneText.T("Значение", "Value"), _frameValue, 0, 200, "");
            if (frameCount == 0)
                GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Нет других игроков.", "No other players."), _muted);
            else
                for (int i = 0; i < _roleClients.Count; i++)
                    if (_roleClients[i] != PlayerControl.LocalPlayer) FrameSabotageRow(b.x, ref by, b.width, _roleClients[i], frameSystemValues[_frameSystemIdx]);
        }

        _roleClients.Clear();
        CollectPlayers(_roleClients);
        int tpCount = 0;
        for (int i = 0; i < _roleClients.Count; i++) if (_roleClients[i] != PlayerControl.LocalPlayer) tpCount++;
        b = Card(x, ref y, w, NocturneText.T("Телепорт к игроку", "Teleport to player"), 24f + (tpCount > 0 ? tpCount * 32f : 26f));
        by = b.y;
        GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 22f), NocturneText.T("Прыжок к выбранному или идти за ним.", "Snap to the selected player or follow them."), _muted);
        by += 24f;
        if (tpCount == 0)
            GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Нет других игроков.", "No other players."), _muted);
        else
            for (int i = 0; i < _roleClients.Count; i++)
                if (_roleClients[i] != PlayerControl.LocalPlayer) TpRow(b.x, ref by, b.width, _roleClients[i]);

        b = Card(x, ref y, w, NocturneText.T("Фан (хост)", "Fun (host)"), 5f * 30f + 24f);
        by = b.y;
        GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 22f), NocturneText.T("В игре, не в лобби. На офиц. сервере может кикнуть.", "In-game only. May kick on official servers."), _muted);
        by += 24f;
        float cw2 = (b.width - 10f) / 2f;
        if (SmallButton(new Rect(b.x, by, cw2, 26f), NocturneText.T("ВСЕ В ЯЙЦА", "ALL TO EGGS"), NocturneStyle.Current.Accent)) FunToast(NocturneLobbyPranks.MassMorphToEgg());
        if (SmallButton(new Rect(b.x + cw2 + 10f, by, cw2, 26f), NocturneText.T("МОРФ В ВЫБРАННОГО", "MORPH TO TARGET"), NocturneStyle.Current.Accent)) FunToast(NocturneLobbyPranks.MorphAllIntoSelected());
        by += 30f;
        if (SmallButton(new Rect(b.x, by, cw2, 26f), NocturneText.T("РАДУГА", "RAINBOW") + St(NocturneLobbyPranks.RainbowActive), FunCol(NocturneLobbyPranks.RainbowActive))) FunToast(NocturneLobbyPranks.ToggleRainbow());
        if (SmallButton(new Rect(b.x + cw2 + 10f, by, cw2, 26f), NocturneText.T("ЦИКЛ КОСМЕТИКИ", "COSMETIC CYCLE") + St(NocturneLobbyPranks.SkinCycleActive), FunCol(NocturneLobbyPranks.SkinCycleActive))) FunToast(NocturneLobbyPranks.ToggleSkinCycle());
        by += 30f;
        if (SmallButton(new Rect(b.x, by, cw2, 26f), NocturneText.T("ТАКТ: ", "BEAT: ") + NocturneLobbyPranks.SyncName(), NocturneStyle.Current.Accent)) FunToast(NocturneLobbyPranks.ToggleSync());
        if (SmallButton(new Rect(b.x + cw2 + 10f, by, cw2, 26f), NocturneText.T("РАЗМЕР: ", "SIZE: ") + NocturneLobbyPranks.ScaleName(), NocturneStyle.Current.Accent)) FunToast(NocturneLobbyPranks.CycleScale());
        by += 30f;
        if (SmallButton(new Rect(b.x, by, cw2, 26f), NocturneText.T("ДВИЖЕНИЕ: ", "MOTION: ") + NocturneLobbyPranks.SpinName(), NocturneStyle.Current.Accent)) FunToast(NocturneLobbyPranks.CycleSpin());
        if (SmallButton(new Rect(b.x + cw2 + 10f, by, cw2, 26f), NocturneText.T("АНИМАЦИЯ: ", "ANIM: ") + NocturneLobbyPranks.AnimName(), NocturneStyle.Current.Accent)) FunToast(NocturneLobbyPranks.CycleAnim());
        by += 30f;
        if (SmallButton(new Rect(b.x, by, b.width, 26f), NocturneText.T("СБРОС ОБЛИКА", "RESET LOOK"), new Color(0.9f, 0.4f, 0.4f))) FunToast(NocturneLobbyPranks.ResetAppearance());
    }

    private void CheatsSelf(float x, ref float y, float w)
    {
        Rect b = Card(x, ref y, w, NocturneText.T("Фантом: тихий уход", "Phantom: silent vanish"), 28f + 26f + RowH);
        float by = b.y;
        GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 24f),
            NocturneText.T("Дым исчезновения уходит за карту — не видно, где ты пропал.", "Vanish smoke goes off-map — nobody sees where you vanished."), _muted);
        by += 26f;
        Toggle(b.x, ref by, b.width, NocturneText.T("Скрывать точку исчезновения", "Hide vanish point"), NocturneConfig.PhantomNoVanish);

        bool reach = NocturneConfig.BuffKillReach.Value;
        b = Card(x, ref y, w, NocturneText.T("Бафы ролей", "Role buffs"), 38f + RowH * 29f + 234f + (reach ? 54f : 4f));
        by = b.y;
        GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 34f), NocturneText.T("Применяются в матче. Работают и вне хоста (ванильный хост), мод-хост/античит режет сетевые.", "Apply in match. Work off-host too (vanilla host), modded host / anti-cheat blocks networked ones."), _muted);
        by += 38f;

        Sub(b.x, ref by, b.width, NocturneText.T("Общее", "General"));
        Toggle(b.x, ref by, b.width, NocturneText.T("Без кулдаунов", "No cooldowns"), NocturneConfig.BuffNoCd);
        Toggle(b.x, ref by, b.width, NocturneText.T("Ходить внутри вента", "Move inside vent"), NocturneConfig.BuffVentWalk);
        Toggle(b.x, ref by, b.width, NocturneText.T("Лестницы/зиплайн без кд", "No ladder/zipline cd"), NocturneConfig.BuffMapCd);

        Sub(b.x, ref by, b.width, NocturneText.T("Импостер", "Impostor"));
        Toggle(b.x, ref by, b.width, NocturneText.T("Дальность убийства", "Kill reach"), NocturneConfig.BuffKillReach);
        if (reach) Slider(b.x, ref by, b.width, NocturneText.T("Радиус", "Radius"), NocturneConfig.BuffKillDist, 1f, 12f, "0.0");
        Toggle(b.x, ref by, b.width, NocturneText.T("Убивать кого угодно", "Kill anyone"), NocturneConfig.BuffKillAny);
        Toggle(b.x, ref by, b.width, NocturneText.T("Аура убийства", "Kill aura"), NocturneConfig.BuffKillAura);
        Toggle(b.x, ref by, b.width, NocturneText.T("Авто-вент после килла", "Auto-vent after kill"), NocturneConfig.AutoVentKill);
        Toggle(b.x, ref by, b.width, NocturneText.T("Тело в вент (хост)", "Body to vent (host)"), NocturneConfig.BodyToVent);
        Toggle(b.x, ref by, b.width, NocturneText.T("Кулдаун убийства 0 (хост)", "Kill cooldown 0 (host)"), NocturneConfig.BuffNoKillCd);
        Toggle(b.x, ref by, b.width, NocturneText.T("Венты любой ролью", "Vents with any role"), NocturneConfig.BuffVentAny);
        Toggle(b.x, ref by, b.width, NocturneText.T("Вент-сеть (прыжок в любой вент)", "Vent network (hop any vent)"), NocturneConfig.VentNetwork);
        Toggle(b.x, ref by, b.width, NocturneText.T("Таски предом", "Tasks as impostor"), NocturneConfig.BuffImpTasks);
        Toggle(b.x, ref by, b.width, NocturneText.T("Авто-репорт своих киллов", "Auto-report own kills"), NocturneConfig.BuffAutoReport);
        Toggle(b.x, ref by, b.width, NocturneText.T("Саботаж из вента", "Sabotage from vent"), NocturneConfig.BuffVentSab);

        Sub(b.x, ref by, b.width, NocturneText.T("Оборотень", "Shapeshifter"));
        Toggle(b.x, ref by, b.width, NocturneText.T("Вечная маскировка", "Endless shapeshift"), NocturneConfig.BuffSsForever);
        Toggle(b.x, ref by, b.width, NocturneText.T("Без анимации", "No animation"), NocturneConfig.BuffSsQuiet);

        Sub(b.x, ref by, b.width, NocturneText.T("Фантом", "Phantom"));
        Toggle(b.x, ref by, b.width, NocturneText.T("Убийство в невидимости", "Kill while vanished"), NocturneConfig.BuffVanishKill);
        Toggle(b.x, ref by, b.width, NocturneText.T("Вечная невидимость", "Endless invisibility"), NocturneConfig.BuffPhVanish);
        Toggle(b.x, ref by, b.width, NocturneText.T("Видеть невидимого", "See vanished"), NocturneConfig.SeePhantoms);

        Sub(b.x, ref by, b.width, NocturneText.T("Инженер", "Engineer"));
        Toggle(b.x, ref by, b.width, NocturneText.T("Вечный вент", "Endless vent"), NocturneConfig.BuffEngVent);
        Toggle(b.x, ref by, b.width, NocturneText.T("Без кулдауна", "No cooldown"), NocturneConfig.BuffEngCd);

        Sub(b.x, ref by, b.width, NocturneText.T("Учёный", "Scientist"));
        Toggle(b.x, ref by, b.width, NocturneText.T("Вечная батарея", "Endless battery"), NocturneConfig.BuffSciBat);
        Toggle(b.x, ref by, b.width, NocturneText.T("Без кулдауна", "No cooldown"), NocturneConfig.BuffSciCd);

        Sub(b.x, ref by, b.width, NocturneText.T("Детектив", "Detective"));
        Toggle(b.x, ref by, b.width, NocturneText.T("Любая дистанция", "Any range"), NocturneConfig.BuffDetReach);
        Toggle(b.x, ref by, b.width, NocturneText.T("Допрос без кулдауна", "Interrogate without cooldown"), NocturneConfig.BuffDetCd);

        Sub(b.x, ref by, b.width, NocturneText.T("Судья", "Judge"));
        Toggle(b.x, ref by, b.width, NocturneText.T("Переголосование без заданий", "Overrule without tasks"), NocturneConfig.BuffJudgeNoTasks);

        Sub(b.x, ref by, b.width, NocturneText.T("Ангел", "Guardian Angel"));
        Toggle(b.x, ref by, b.width, NocturneText.T("Видеть щит", "See shield"), NocturneConfig.SeeProtections);

        bool creach = NocturneConfig.ConsoleReach.Value;
        bool air = NocturneConfig.AirshipSpawn.Value;
        b = Card(x, ref y, w, NocturneText.T("Мирный: помощь", "Crew: assist"), 5f * RowH + (creach ? 50f : 0f) + (air ? 50f : 0f) + 8f);
        by = b.y;
        Toggle(b.x, ref by, b.width, NocturneText.T("Чинить саботаж сразу", "Auto-fix sabotage"), NocturneConfig.SabAutoFix);
        Toggle(b.x, ref by, b.width, NocturneText.T("Открытые шлюзы (Полюс, Мира)", "Skip decontamination"), NocturneConfig.SkipDecon);
        Toggle(b.x, ref by, b.width, NocturneText.T("Дальность консолей", "Console reach"), NocturneConfig.ConsoleReach);
        if (creach) Slider(b.x, ref by, b.width, NocturneText.T("Дистанция", "Distance"), NocturneConfig.ConsoleDist, 1f, 15f, "0.0");
        Toggle(b.x, ref by, b.width, NocturneText.T("Свой спавн на Airship", "Pick Airship spawn"), NocturneConfig.AirshipSpawn);
        if (air) SliderInt(b.x, ref by, b.width, NocturneText.T("Точка спавна", "Spawn point"), NocturneConfig.AirshipSpawnId, 0, 6);

        b = Card(x, ref y, w, NocturneText.T("Выживание", "Survival"), 2f * RowH + 46f);
        by = b.y;
        Toggle(b.x, ref by, b.width, NocturneText.T("Бессмертие (God Mode)", "God Mode"), NocturneConfig.GodMode);
        GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 22f), NocturneText.T("Не убить (фейк-вент). От голосования не спасает.", "Can't be killed (fake vent). Not vs vote-out."), _muted);
        by += 24f;
        Toggle(b.x, ref by, b.width, NocturneText.T("Невидимость", "Invisibility"), NocturneConfig.Invisible);
        GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 22f), NocturneText.T("Для других уезжаешь за карту. Взаимодействий/килла нет. ⚠ античит.", "You warp off-map for others. No interaction/kill. ⚠ anti-cheat."), _muted);

        bool lc = NocturneConfig.LagComp.Value;
        bool lcj = lc && NocturneConfig.LagCompJitter.Value;
        float lch = RowH + 40f + (lc ? 2f * RowH + (lcj ? 100f : 0f) : 0f);
        b = Card(x, ref y, w, NocturneText.T("Мираж", "Mirage"), lch);
        by = b.y;
        Toggle(b.x, ref by, b.width, NocturneText.T("Включить", "Enable"), NocturneConfig.LagComp);
        if (lc)
        {
            Toggle(b.x, ref by, b.width, NocturneText.T("Стоп-кадр (замер для других)", "Freeze frame (frozen to others)"), NocturneConfig.LagCompFreeze);
            Toggle(b.x, ref by, b.width, NocturneText.T("Мерцание (рваный сигнал)", "Flicker (broken signal)"), NocturneConfig.LagCompJitter);
            if (lcj)
            {
                SliderInt(b.x, ref by, b.width, NocturneText.T("Мерцание мин (кадры)", "Flicker min (frames)"), NocturneConfig.LagCompJitterMin, 1, 30);
                SliderInt(b.x, ref by, b.width, NocturneText.T("Мерцание макс (кадры)", "Flicker max (frames)"), NocturneConfig.LagCompJitterMax, 1, 30);
            }
        }
        GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 40f), NocturneText.T("Только для других — у себя двигаешься нормально. ⚠ палит античит.", "Others only — you move normally. ⚠ anti-cheat risk."), _muted);

        bool spd = NocturneConfig.SpeedMod.Value;
        b = Card(x, ref y, w, NocturneText.T("Движение", "Movement"), 2f * RowH + 22f + (spd ? 50f : 0f));
        by = b.y;
        Toggle(b.x, ref by, b.width, NocturneText.T("Своя скорость", "Custom speed"), NocturneConfig.SpeedMod);
        if (spd)
            Slider(b.x, ref by, b.width, NocturneText.T("Множитель", "Multiplier"), NocturneConfig.SpeedMult, 0f, 3f, "0.0");
        Toggle(b.x, ref by, b.width, NocturneText.T("Инверт управления", "Invert controls"), NocturneConfig.InvertControls);
        GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 22f), NocturneText.T("Клиентское. Большие значения палит античит.", "Client-side. High values trip anti-cheat."), _muted);

        bool sd = NocturneConfig.SelfDrag.Value;
        bool sds = sd && NocturneConfig.SelfDragSmooth.Value;
        b = Card(x, ref y, w, NocturneText.T("Мышь и призрак", "Mouse & ghost"), 4f * RowH + 52f + (sd ? RowH : 0f) + (sds ? RowH : 0f));
        by = b.y;
        Toggle(b.x, ref by, b.width, NocturneText.T("Телепорт по ПКМ", "Teleport on RMB"), NocturneConfig.MouseTeleport);
        Toggle(b.x, ref by, b.width, NocturneText.T("Выбор мышью + ресайз колёсиком", "Mouse select + wheel resize"), NocturneConfig.MouseSelect);
        Toggle(b.x, ref by, b.width, NocturneText.T("Тащить себя (ЛКМ)", "Drag self (LMB)"), NocturneConfig.SelfDrag);
        if (sd) Toggle(b.x, ref by, b.width, NocturneText.T("— плавно (скольжение)", "— smooth glide"), NocturneConfig.SelfDragSmooth);
        if (sds) Slider(b.x, ref by, b.width, NocturneText.T("Скорость", "Speed"), NocturneConfig.SelfDragSpeed, 2f, 14f, "0.0");
        bool gaBefore = NocturneConfig.GhostAfterStart.Value;
        Toggle(b.x, ref by, b.width, NocturneText.T("Призрак после старта", "Ghost after start"), NocturneConfig.GhostAfterStart);
        if (NocturneConfig.GhostAfterStart.Value && !gaBefore) NocturneConfig.GameMaster.Value = false;
        if (SmallButton(new Rect(b.x + 2f, by, b.width - 4f, 26f), NocturneText.T("СУИЦИД (ПРЕД)", "SUICIDE (IMPOSTOR)"), new Color(0.9f, 0.4f, 0.4f)))
            NocturneToast.Push(NocturneText.T("Суицид", "Suicide"), Patches.GhostStart.Now(), 2.5f, NocturneNotifyKind.Warning);
        by += 30f;
        GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 22f), NocturneText.T("Настоящая смерть, только за преда и при готовом кд.", "Real death, impostor only, needs kill cooldown ready."), _muted);
    }

    private void CheatsMap(float x, ref float y, float w)
    {
        bool inMatch = ShipStatus.Instance != null;
        int mapId = NocturneNav.CurrentMapId();
        bool fungle = mapId == 5;
        bool hasO2 = mapId == 0 || mapId == 1 || mapId == 3;
        Color acc = NocturneStyle.Current.Accent;
        Color red = new Color(0.9f, 0.4f, 0.4f);
        float cw;

        _roleClients.Clear();
        CollectPlayers(_roleClients);
        int zpN = 0;
        for (int i = 0; i < _roleClients.Count; i++) if (_roleClients[i] != PlayerControl.LocalPlayer) zpN++;
        bool zpOn = inMatch && Zipline.OnMap;

        Rect b = Card(x, ref y, w, NocturneText.T("Зиплайн (Fungle)", "Zipline (Fungle)"), zpOn && zpN > 0 ? 88f + zpN * 32f : 26f);
        float by = b.y;
        if (!inMatch)
            GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Только в матче.", "In match only."), _muted);
        else if (!Zipline.OnMap)
            GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Зиплайна на этой карте нет.", "No zipline on this map."), _muted);
        else if (zpN == 0)
            GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Нет других игроков.", "No other players."), _muted);
        else
        {
            GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Отправить кататься.", "Send for a ride."), _muted);
            by += 28f;
            float zpSelw = (b.width - 8f) / 2f;
            if (SmallButton(new Rect(b.x, by, zpSelw, 24f), NocturneText.T("ВЫБРАТЬ ВСЕХ", "SELECT ALL"), NocturneStyle.Current.Accent))
                RideTargets.All();
            if (SmallButton(new Rect(b.x + zpSelw + 8f, by, zpSelw, 24f), NocturneText.T("СНЯТЬ ВСЕ", "CLEAR ALL"), new Color(0.9f, 0.4f, 0.4f)))
                RideTargets.Clear();
            by += 30f;
            string zpTag = RideTargets.Count > 0 ? $" ({RideTargets.Count})" : "";
            if (SmallButton(new Rect(b.x, by, zpSelw, 24f), NocturneText.T("ВНИЗ ↓", "DOWN ↓") + zpTag, NocturneStyle.Current.Accent))
                NocturneToast.Push(NocturneText.T("Зиплайн", "Zipline"), Zipline.RideSelected(true), 2.5f, NocturneNotifyKind.Info);
            if (SmallButton(new Rect(b.x + zpSelw + 8f, by, zpSelw, 24f), NocturneText.T("ВВЕРХ ↑", "UP ↑") + zpTag, NocturneStyle.Current.Accent))
                NocturneToast.Push(NocturneText.T("Зиплайн", "Zipline"), Zipline.RideSelected(false), 2.5f, NocturneNotifyKind.Info);
            by += 30f;
            for (int i = 0; i < _roleClients.Count; i++)
                if (_roleClients[i] != PlayerControl.LocalPlayer) RideRow(b.x, ref by, b.width, _roleClients[i]);
        }

        bool ptOn = inMatch && Platform.OnMap;
        b = Card(x, ref y, w, NocturneText.T("Платформа (Airship)", "Platform (Airship)"), ptOn ? 58f + RowH : 26f);
        by = b.y;
        if (!inMatch)
            GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Только в матче.", "In match only."), _muted);
        else if (!Platform.OnMap)
            GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Платформы на этой карте нет.", "No platform on this map."), _muted);
        else
        {
            Toggle(b.x, ref by, b.width, NocturneText.T("Разблокировать (прятки)", "Unlock (hide & seek)"), NocturneConfig.PlatformUnlock);
            string state = Platform.IsLeft ? NocturneText.T("слева", "left") : NocturneText.T("справа", "right");
            if (Platform.Locked) state += NocturneText.T(" · заблокирована", " · locked");
            GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Сейчас: ", "Now: ") + state, _muted);
            by += 28f;
            float ptw = (b.width - 16f) / 3f;
            if (SmallButton(new Rect(b.x, by, ptw, 24f), NocturneText.T("◀ ВЛЕВО", "◀ LEFT"), NocturneStyle.Current.Accent))
                NocturneToast.Push(NocturneText.T("Платформа", "Platform"), Platform.Move(true), 2f, NocturneNotifyKind.Info);
            if (SmallButton(new Rect(b.x + ptw + 8f, by, ptw, 24f), NocturneText.T("ПЕРЕКЛЮЧИТЬ", "TOGGLE"), NocturneStyle.Current.Accent))
                NocturneToast.Push(NocturneText.T("Платформа", "Platform"), Platform.Toggle(), 2f, NocturneNotifyKind.Info);
            if (SmallButton(new Rect(b.x + 2f * ptw + 16f, by, ptw, 24f), NocturneText.T("ВПРАВО ▶", "RIGHT ▶"), NocturneStyle.Current.Accent))
                NocturneToast.Push(NocturneText.T("Платформа", "Platform"), Platform.Move(false), 2f, NocturneNotifyKind.Info);
        }

        b = Card(x, ref y, w, NocturneText.T("Двери", "Doors"), inMatch ? 30f * 2f + RowH + 6f : 26f);
        by = b.y;
        if (!inMatch)
            GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Только в матче.", "In match only."), _muted);
        else
        {
            cw = (b.width - 10f) / 2f;
            if (SmallButton(new Rect(b.x, by, cw, 26f), NocturneText.T("ЗАКРЫТЬ ВСЕ", "CLOSE ALL"), acc)) NocturneDoors.CloseAll();
            if (SmallButton(new Rect(b.x + cw + 10f, by, cw, 26f), NocturneText.T("ОТКРЫТЬ ВСЕ", "OPEN ALL"), acc)) NocturneDoors.OpenAll();
            by += 30f;
            if (SmallButton(new Rect(b.x, by, cw, 26f), NocturneText.T("ЗАПИНИТЬ ВСЕ", "PIN ALL"), acc)) NocturneDoors.PinAll();
            if (SmallButton(new Rect(b.x + cw + 10f, by, cw, 26f), NocturneText.T("СНЯТЬ ПИНЫ", "UNPIN"), red)) NocturneDoors.UnpinAll();
            by += 30f;
            Toggle(b.x, ref by, b.width, NocturneText.T("Авто-открытие (без миниигры)", "Auto-open (no minigame)"), NocturneConfig.DoorKeepOpen);
        }

        float sabBody = 30f * 4f + RowH * 3f + 4f;
        b = Card(x, ref y, w, NocturneText.T("Саботаж", "Sabotage"), (inMatch ? sabBody : 26f) + RowH);
        by = b.y;
        Toggle(b.x, ref by, b.width, NocturneText.T("Обход саботажа связи", "Bypass comms sabotage"), NocturneConfig.CommsBypass);
        if (!inMatch)
            GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Только в матче.", "In match only."), _muted);
        else
        {
            cw = (b.width - 10f) / 2f;
            string mainLbl = mapId == 2 ? NocturneText.T("СЕЙСМИКА", "SEISMIC") : mapId == 4 ? NocturneText.T("КРУШЕНИЕ", "CRASH") : NocturneText.T("РЕАКТОР", "REACTOR");
            if (SmallButton(new Rect(b.x, by, b.width, 26f), NocturneText.T("ПОЧИНИТЬ ВСЁ", "FIX ALL"), new Color(0.30f, 0.72f, 0.40f))) NocturneSabotage.Fix();
            by += 30f;
            if (SmallButton(new Rect(b.x, by, cw, 26f), NocturneText.T("САБОТАЖ ВСЕГО", "SABOTAGE ALL"), red)) NocturneSabotage.All();
            if (SmallButton(new Rect(b.x + cw + 10f, by, cw, 26f), NocturneText.T("СЛУЧАЙНЫЙ", "RANDOM"), acc)) NocturneSabotage.Random();
            by += 30f;
            if (SmallButton(new Rect(b.x, by, cw, 26f), mainLbl, acc)) NocturneSabotage.Main();
            if (SmallButton(new Rect(b.x + cw + 10f, by, cw, 26f), NocturneText.T("СВЯЗЬ", "COMMS"), acc)) NocturneSabotage.Comms();
            by += 30f;
            if (!fungle && SmallButton(new Rect(b.x, by, hasO2 ? cw : b.width, 26f), NocturneText.T("СВЕТ", "LIGHTS"), acc)) NocturneSabotage.Lights();
            if (hasO2 && SmallButton(new Rect(b.x + cw + 10f, by, cw, 26f), NocturneText.T("КИСЛОРОД", "OXYGEN"), acc)) NocturneSabotage.Oxygen();
            if (!fungle || hasO2) by += 30f;
            if (fungle && SmallButton(new Rect(b.x, by, b.width, 26f), NocturneText.T("ГРИБЫ (MIXUP)", "MUSHROOM MIXUP"), acc)) { NocturneSabotage.Mush(); }
            if (fungle) by += 30f;
            Toggle(b.x, ref by, b.width, NocturneText.T("Спам главного саботажа", "Spam main sabotage"), NocturneConfig.SabSpamReactor);
            Toggle(b.x, ref by, b.width, NocturneText.T("Мульти-саботаж (пред, не хост)", "Multi sabotage (impostor, non-host)"), NocturneConfig.MultiSabotage);
            if (!fungle) Toggle(b.x, ref by, b.width, NocturneText.T("Держать свет выключенным", "Keep lights off"), NocturneConfig.SabAutoLights);
            if (fungle) Toggle(b.x, ref by, b.width, NocturneText.T("Бесконечные грибы", "Infinite mushroom"), NocturneConfig.SabInfMushroom);
        }
    }

    private void CheatsTasks(float x, ref float y, float w)
    {
        bool inMatch = ShipStatus.Instance != null;
        Color acc = NocturneStyle.Current.Accent;
        float cw;

        Rect b = Card(x, ref y, w, NocturneText.T("Прятки: слив таймера", "Hide & seek: drain timer"), 28f + RowH + 50f);
        float by = b.y;
        GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 24f),
            TaskDrain.Running
                ? NocturneText.T("Идёт слив — таймер мирных утекает.", "Draining — the crew timer is running out.")
                : NocturneText.T("Только в прятках. Работает и призраком.", "Hide and seek only. Keeps working as a ghost."), _muted);
        by += 26f;
        Toggle(b.x, ref by, b.width, NocturneText.T("Сливать таймер", "Drain the timer"), NocturneConfig.HnsDrain);
        Slider(b.x, ref by, b.width, NocturneText.T("Шаг отправки, сек", "Send step, sec"), NocturneConfig.HnsDrainStep, 0.15f, 1.5f, "0.00");

        _roleClients.Clear();
        CollectPlayers(_roleClients);
        int tkN = 0;
        for (int i = 0; i < _roleClients.Count; i++) if (_roleClients[i] != PlayerControl.LocalPlayer) tkN++;
        bool tkOn = inMatch && IsHostSelf();
        b = Card(x, ref y, w, NocturneText.T("Задания (хост)", "Tasks (host)"), 26f + (tkOn && tkN > 0 ? 24f + tkN * 32f : 24f));
        by = b.y;
        if (!inMatch)
            GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Только в матче.", "In match only."), _muted);
        else if (!IsHostSelf())
            GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Только для хоста.", "Host only."), _muted);
        else if (tkN == 0)
            GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Нет других игроков.", "No other players."), _muted);
        else
        {
            GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Обнулить — палит, завалить — гора тасков.", "Clear exposes them, flood buries in tasks."), _muted);
            by += 26f;
            for (int i = 0; i < _roleClients.Count; i++)
                if (_roleClients[i] != PlayerControl.LocalPlayer) TaskRow(b.x, ref by, b.width, _roleClients[i]);
        }

        float fakeBody = inMatch ? 40f + 30f + RowH * 2f : 26f;
        b = Card(x, ref y, w, NocturneText.T("Фейк-задания", "Fake tasks"), fakeBody);
        by = b.y;
        if (!inMatch)
            GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Только в матче.", "In match only."), _muted);
        else
        {
            GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 36f), NocturneText.T("Проиграть анимацию задания для окружающих (казаться занятым).", "Play a task animation for others (look busy)."), _muted);
            by += 40f;
            cw = (b.width - 20f) / 3f;
            if (SmallButton(new Rect(b.x, by, cw, 26f), NocturneText.T("ЩИТЫ", "SHIELDS"), acc)) NocturneFakeTasks.Shields();
            if (SmallButton(new Rect(b.x + cw + 10f, by, cw, 26f), NocturneText.T("АСТЕРОИДЫ", "ASTEROIDS"), acc)) NocturneFakeTasks.Asteroids();
            if (SmallButton(new Rect(b.x + (cw + 10f) * 2f, by, cw, 26f), NocturneText.T("МУСОР", "GARBAGE"), acc)) NocturneFakeTasks.Garbage();
            by += 30f;
            Toggle(b.x, ref by, b.width, NocturneText.T("Скан в медбэе (постоянно)", "Medbay scan (held)"), NocturneConfig.FakeScan);
            if (NocturneFakeTasks.HasCams())
                Toggle(b.x, ref by, b.width, NocturneText.T("Камеры «заняты»", "Cameras in use"), NocturneConfig.FakeCams);
            else
                GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 22f), NocturneText.T("Камеры — не на этой карте.", "Cameras — not on this map."), _muted);
        }

        bool autoOn = NocturneConfig.AutoTasks.Value;
        b = Card(x, ref y, w, NocturneText.T("Авто-задания", "Auto tasks"), RowH + (autoOn ? 76f : 24f));
        by = b.y;
        Toggle(b.x, ref by, b.width, NocturneText.T("Делать задания сами", "Finish tasks by itself"), NocturneConfig.AutoTasks);
        if (autoOn)
        {
            Slider(b.x, ref by, b.width, NocturneText.T("Пауза между заданиями", "Gap between tasks"), NocturneConfig.AutoTasksDelay, 0.8f, 6f, "0.0");
            int left = NocturneAutoTasks.Left();
            GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 22f),
                inMatch ? NocturneText.T("Осталось: ", "Left: ") + $"<b>{left}</b>" : NocturneText.T("Только в матче, мирным.", "In match, crewmate only."), _muted);
        }
        else
            GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 22f), NocturneText.T("По одному, не в собрании. Не для предов.", "One by one, never in a meeting. Not for impostors."), _muted);
    }

    private void CheatsChat(float x, ref float y, float w)
    {
        Rect b = Card(x, ref y, w, NocturneText.T("Отправить в чат", "Chat sender"), 4f * RowH + 50f);
        float by = b.y;
        _chatSend = CustomText(new Rect(b.x + 2f, by, b.width - 4f, 26f), _chatSend ?? "", "chatSend");
        NocturneChatSender.Message = _chatSend ?? "";
        by += 32f;
        float chHalf = (b.width - 10f) / 2f;
        if (SmallButton(new Rect(b.x, by, chHalf, 26f), NocturneText.T("ОТПРАВИТЬ", "SEND"), NocturneStyle.Current.Accent)) NocturneChatSender.SendNow();
        if (SmallButton(new Rect(b.x + chHalf + 10f, by, chHalf, 26f), NocturneChatSender.Spamming ? NocturneText.T("СПАМ: ВКЛ", "SPAM: ON") : NocturneText.T("СПАМ: ВЫКЛ", "SPAM: OFF"), NocturneChatSender.Spamming ? new Color(0.9f, 0.4f, 0.4f) : NocturneStyle.Current.Accent)) NocturneChatSender.Spamming = !NocturneChatSender.Spamming;
        by += 30f;
        bool floodReady = NocturneChatSender.FloodReady;
        string floodLabel = floodReady ? NocturneText.T("ЗАТОПИТЬ ЧАТ", "FLOOD CHAT") : NocturneText.T("ОСТЫВАЕТ ", "COOLDOWN ") + Mathf.CeilToInt(NocturneChatSender.FloodCooldownLeft) + "с";
        if (SmallButton(new Rect(b.x, by, b.width - 4f, 26f), floodLabel, floodReady ? new Color(0.9f, 0.4f, 0.4f) : NocturneStyle.Current.Button) && floodReady)
            NocturneToast.Push(NocturneText.T("Чат", "Chat"), NocturneChatSender.Flood(), 2f, NocturneNotifyKind.Warning);
        by += 30f;
        Slider(b.x, ref by, b.width, NocturneText.T("Задержка (с)", "Delay (s)"), NocturneConfig.ChatSpamDelay, 1.5f, 10f, "0.0");
        by += 22f;
        GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 22f), NocturneText.T("Чат в лобби/митинге. Малая задержка — кик за флуд.", "Chat in lobby/meeting. Low delay — flood kick."), _muted);

        const int qcCols = 5;
        int qcRows = Mathf.CeilToInt(NocturneQuickChatChain.KnownSubs.Length / (float)qcCols);
        float qcListHeight = _qcWordListOpen ? qcRows * 30f + 4f : 0f;
        b = Card(x, ref y, w, NocturneText.T("Цепочка Quick Chat", "Quick Chat chain"), 5f * RowH + 34f + qcListHeight);
        by = b.y;
        GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 22f),
            NocturneText.T("Через запятую.", "Comma-separated."), _muted);
        by += 24f;
        _qcChain = CustomText(new Rect(b.x + 2f, by, b.width - 4f, 26f), _qcChain ?? "", "qcChain");
        by += 32f;
        float qcHalf = (b.width - 8f) / 2f;
        if (SmallButton(new Rect(b.x, by, qcHalf, 26f), _qcWordListOpen ? NocturneText.T("СПИСОК СЛОВ ▴", "WORD LIST ▴") : NocturneText.T("СПИСОК СЛОВ ▾", "WORD LIST ▾"), NocturneStyle.Current.Accent))
            _qcWordListOpen = !_qcWordListOpen;
        if (SmallButton(new Rect(b.x + qcHalf + 8f, by, qcHalf, 26f), NocturneText.T("ОЧИСТИТЬ", "CLEAR"), new Color(0.9f, 0.4f, 0.4f)))
            _qcChain = "";
        by += 30f;
        if (_qcWordListOpen)
        {
            float qcCellW = (b.width - (qcCols - 1) * 4f) / qcCols;
            for (int i = 0; i < NocturneQuickChatChain.KnownSubs.Length; i++)
            {
                int col = i % qcCols;
                int row = i / qcCols;
                int wordId = NocturneQuickChatChain.KnownSubs[i];
                if (SmallButton(new Rect(b.x + col * (qcCellW + 4f), by + row * 30f, qcCellW, 26f), wordId.ToString(), new Color(0.36f, 0.39f, 0.47f)))
                {
                    _qcChain = string.IsNullOrWhiteSpace(_qcChain) ? $"{NocturneQuickChatChain.KnownRoot},{wordId}" : $"{_qcChain},{wordId}";
                    _qcWordListOpen = false;
                }
            }
            by += qcRows * 30f + 4f;
        }
        if (SmallButton(new Rect(b.x, by, b.width, 26f), _qcDupSelf ? NocturneText.T("СЕБЕ КОПИЮ: ВКЛ", "COPY TO SELF: ON") : NocturneText.T("СЕБЕ КОПИЮ: ВЫКЛ", "COPY TO SELF: OFF"), _qcDupSelf ? NocturneStyle.Current.Accent : new Color(0.36f, 0.39f, 0.47f)))
            _qcDupSelf = !_qcDupSelf;
        by += 30f;
        if (SmallButton(new Rect(b.x, by, b.width, 26f), NocturneText.T("ОТПРАВИТЬ ЦЕПОЧКОЙ", "SEND CHAIN"), NocturneStyle.Current.Accent))
            NocturneToast.Push(NocturneText.T("Quick Chat", "Quick Chat"), NocturneQuickChatChain.SendFromText(_qcChain, _qcDupSelf), 2.2f, NocturneNotifyKind.Info);

        b = Card(x, ref y, w, NocturneText.T("Шаблон Quick Chat (с игроком)", "Quick Chat template (with player)"), 6f * RowH + 66f);
        by = b.y;
        GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 38f),
            NocturneText.T("ID корневой фразы-шаблона (например, обвинение). Игрок A берётся по ЛКМ-выбору.",
                "Template root phrase id (e.g. an accusation). Player A is whoever is LMB-selected."), _muted);
        by += 40f;
        _qcTemplate = CustomText(new Rect(b.x + 2f, by, b.width - 4f, 26f), _qcTemplate ?? "", "qcTemplate");
        by += 32f;
        GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 38f),
            NocturneText.T("ID игрока B (необязательно, для шаблонов на двоих — например, «A видел B в венте»).",
                "Player B id (optional, for two-player templates — e.g. \"A saw B vent\")."), _muted);
        by += 40f;
        _qcTemplateB = CustomText(new Rect(b.x + 2f, by, b.width - 4f, 26f), _qcTemplateB ?? "", "qcTemplateB");
        by += 32f;
        if (SmallButton(new Rect(b.x, by, b.width, 26f), _qcDupSelf ? NocturneText.T("СЕБЕ КОПИЮ: ВКЛ", "COPY TO SELF: ON") : NocturneText.T("СЕБЕ КОПИЮ: ВЫКЛ", "COPY TO SELF: OFF"), _qcDupSelf ? NocturneStyle.Current.Accent : new Color(0.36f, 0.39f, 0.47f)))
            _qcDupSelf = !_qcDupSelf;
        by += 30f;
        if (SmallButton(new Rect(b.x, by, b.width, 26f), NocturneText.T("ОТПРАВИТЬ НА ВЫБРАННОГО", "SEND ON SELECTED"), NocturneStyle.Current.Accent))
        {
            PlayerControl playerB = PlayerByIdText(_qcTemplateB);
            string result = playerB != null
                ? NocturneQuickChatChain.SendTemplateFromText(_qcTemplate, _qcDupSelf, NocturneMouseTools.Selected, playerB)
                : NocturneQuickChatChain.SendTemplateFromText(_qcTemplate, _qcDupSelf, NocturneMouseTools.Selected);
            NocturneToast.Push(NocturneText.T("Quick Chat", "Quick Chat"), result, 2.2f, NocturneNotifyKind.Info);
        }
    }

    private void DrawPlayers(float x, ref float y, float w)
    {
        SubBar(x, ref y, w, PlayersSubsRu, PlayersSubsEn);

        Grp(0);
        Rect b = Card(x, ref y, w, NocturneText.T("Роли и инфо", "Roles & info"), (NocturneConfig.RevealVotes.Value ? 9f : 8f) * RowH);
        float by = b.y;
        Toggle(b.x, ref by, b.width, NocturneText.T("Показывать роли всех", "Reveal all roles"), NocturneConfig.RevealRoles);
        Toggle(b.x, ref by, b.width, NocturneText.T("Инфо над игроками (лобби)", "Info above players (lobby)"), NocturneConfig.VisualPlayerInfoNames);
        Toggle(b.x, ref by, b.width, NocturneText.T("ID над игроками", "Player id above names"), NocturneConfig.ShowPlayerIds);
        Toggle(b.x, ref by, b.width, NocturneText.T("Френдкод над игроками", "Friend code above names"), NocturneConfig.ShowPlayerFc);
        Toggle(b.x, ref by, b.width, NocturneText.T("Счётчик войткиков", "Votekick counter"), NocturneConfig.ShowVotekickCount);
        Toggle(b.x, ref by, b.width, NocturneText.T("Ники в прятках", "Names in Hide and Seek"), NocturneConfig.ForceNamesInHns);
        Toggle(b.x, ref by, b.width, NocturneText.T("Раскрыть Оборотня", "Unmask shapeshifter"), NocturneConfig.UnmaskShapeshifter);
        Toggle(b.x, ref by, b.width, NocturneText.T("Голоса на собрании", "Votes in meeting"), NocturneConfig.RevealVotes);
        if (NocturneConfig.RevealVotes.Value)
            Toggle(b.x, ref by, b.width, NocturneText.T("Раскрывать анонимные", "De-anonymize voters"), NocturneConfig.RevealAnonVotes);

        _roleClients.Clear();
        CollectPlayers(_roleClients);
        float rbBody = 30f + (_roleClients.Count > 0 ? _roleClients.Count * 32f : 26f);
        Grp(1);
        b = Card(x, ref y, w, NocturneText.T("Форс ролей (хост)", "Force roles (host)"), rbBody);
        by = b.y;
        if (!NocturneForceRoles.Host())
            GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Только хост. Роли выдаются на старте матча.", "Host only. Roles apply on match start."), _muted);
        else if (_roleClients.Count == 0)
            GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Нет игроков в лобби.", "No players in lobby."), _muted);
        else
        {
            GUI.Label(new Rect(b.x + 2f, by, b.width - 110f, 24f), $"{NocturneText.T("Назначено", "Assigned")}: <b>{NocturneForceRoles.Count}</b>   {NocturneText.T("выдача на старте", "applied on start")}", _muted);
            if (SmallButton(new Rect(b.x + b.width - 96f, by, 92f, 24f), NocturneText.T("СБРОС", "CLEAR"), new Color(0.9f, 0.4f, 0.4f))) NocturneForceRoles.Clear();
            by += 30f;
            for (int i = 0; i < _roleClients.Count; i++) RoleRow(b.x, ref by, b.width, _roleClients[i]);
        }

        _immPlayers.Clear();
        CollectPlayers(_immPlayers);
        bool immReady = ShipStatus.Instance != null && LobbyBehaviour.Instance == null;
        int ventCount = NocturneVentTp.VentCount();
        float autoH = immReady ? RowH + (NocturneConfig.VentTpAuto.Value ? 50f : 0f) : 0f;
        float immBody = 30f + (immReady ? 30f : 0f) + (immReady && _immPlayers.Count > 0 ? 60f : 0f) + autoH + (immReady && _immPlayers.Count > 0 ? _immPlayers.Count * 32f : 26f);
        Grp(1);
        b = Card(x, ref y, w, NocturneText.T("Действия на игроках", "Player actions"), immBody);
        by = b.y;
        GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Работает вне хоста. Нужен ванильный хост.", "Works off-host. Needs a vanilla host."), _muted);
        by += 28f;
        if (!immReady)
            GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Только в матче.", "In-match only."), _muted);
        else if (_immPlayers.Count == 0)
            GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Нет игроков.", "No players."), _muted);
        else
        {
            int vt = ventCount > 0 ? ((NocturneVentTp.Vent % ventCount) + ventCount) % ventCount : 0;
            NocturneVentTp.Vent = vt;
            GUI.Label(new Rect(b.x + 2f, by, b.width - 200f, 24f), NocturneText.T("Вент для ТП: ", "TP vent: ") + (ventCount > 0 ? vt.ToString() : "-") + $"   {NocturneText.T("отмечено", "marked")}: {NocturneVentTp.MarkedCount}", _muted);
            if (SmallButton(new Rect(b.x + b.width - 116f, by, 34f, 24f), "◂", NocturneStyle.Current.Accent) && ventCount > 0) NocturneVentTp.Vent = (vt - 1 + ventCount) % ventCount;
            if (SmallButton(new Rect(b.x + b.width - 40f, by, 34f, 24f), "▸", NocturneStyle.Current.Accent) && ventCount > 0) NocturneVentTp.Vent = (vt + 1) % ventCount;
            by += 30f;
            float selw = (b.width - 8f) / 2f;
            if (SmallButton(new Rect(b.x, by, selw, 24f), NocturneText.T("ВЫБРАТЬ ВСЕХ", "SELECT ALL"), NocturneStyle.Current.Accent)) NocturneVentTp.MarkAll();
            if (SmallButton(new Rect(b.x + selw + 8f, by, selw, 24f), NocturneText.T("СНЯТЬ ВСЕ", "CLEAR ALL"), new Color(0.9f, 0.4f, 0.4f))) NocturneVentTp.ClearMarks();
            by += 30f;
            if (SmallButton(new Rect(b.x, by, b.width, 24f), NocturneText.T("ВЫКИНУТЬ ВСЕХ ИЗ ВЕНТОВ", "KICK EVERYONE FROM VENTS"), new Color(0.78f, 0.42f, 0.95f)))
                NocturneToast.Push(NocturneText.T("Венты", "Vents"), NocturneVentTp.KickAllFromVents(), 2.2f, NocturneNotifyKind.Info);
            by += 30f;
            Toggle(b.x, ref by, b.width, NocturneText.T("Авто-раскидывание отмеченных", "Auto-scatter marked"), NocturneConfig.VentTpAuto);
            if (NocturneConfig.VentTpAuto.Value)
                Slider(b.x, ref by, b.width, NocturneText.T("Интервал", "Interval"), NocturneConfig.VentTpAutoDelay, 0.3f, 10f, "0.0");
            for (int i = 0; i < _immPlayers.Count; i++) ImmortalRow(b.x, ref by, b.width, _immPlayers[i], vt);
        }

        Grp(1);
        b = Card(x, ref y, w, NocturneText.T("Морф (хост)", "Morph (host)"), 2f * RowH + 22f);
        by = b.y;
        float mw2 = (b.width - 8f) / 2f;
        if (SmallButton(new Rect(b.x, by, mw2, 24f), NocturneText.T("ВЫБР. → В МЕНЯ", "SELECTED → ME"), NocturneStyle.Current.Accent))
            NocturneToast.Push(NocturneText.T("Морф", "Morph"), NocturneMorph.Into(NocturneMouseTools.Selected, PlayerControl.LocalPlayer), 2f, NocturneNotifyKind.Info);
        if (SmallButton(new Rect(b.x + mw2 + 8f, by, mw2, 24f), NocturneText.T("ВСЕХ → В ВЫБР.", "ALL → SELECTED"), NocturneStyle.Current.Accent))
            NocturneToast.Push(NocturneText.T("Морф", "Morph"), NocturneMorph.IntoAll(NocturneMouseTools.Selected), 2f, NocturneNotifyKind.Info);
        by += 30f;
        if (SmallButton(new Rect(b.x, by, mw2, 24f), NocturneText.T("ВСЕХ → В МЕНЯ", "ALL → ME"), NocturneStyle.Current.Accent))
            NocturneToast.Push(NocturneText.T("Морф", "Morph"), NocturneMorph.IntoAll(PlayerControl.LocalPlayer), 2f, NocturneNotifyKind.Info);
        if (SmallButton(new Rect(b.x + mw2 + 8f, by, mw2, 24f), NocturneText.T("СБРОС МОРФА", "REVERT ALL"), new Color(0.9f, 0.4f, 0.4f)))
            NocturneToast.Push(NocturneText.T("Морф", "Morph"), NocturneMorph.RevertAll(), 2f, NocturneNotifyKind.Info);

        InnerNetClient net = GuardNet();
        _guardClients.Clear();
        CollectClients(_guardClients);
        float listBody = _guardClients.Count > 0 ? _guardClients.Count * (RowH + 6f) : 28f;
        Grp(0);
        b = Card(x, ref y, w, NocturneText.T("Игроки в лобби", "Players in lobby"), listBody);
        by = b.y;
        if (_guardClients.Count == 0)
            GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 26f), NocturneText.T("Зайди в лобби как хост.", "Join a lobby as host."), _muted);
        else
            for (int i = 0; i < _guardClients.Count; i++)
                PlayerRow(b.x, ref by, b.width, net, _guardClients[i]);
    }

    private static void FunToast(string msg) => NocturneToast.Push(NocturneText.T("Фан", "Fun"), msg, 2.5f, NocturneNotifyKind.Info);
    private static string St(bool on) => on ? NocturneText.T(": вкл", ": on") : NocturneText.T(": выкл", ": off");
    private static Color FunCol(bool on) => on ? NocturneStyle.Current.Accent : new Color(0.5f, 0.55f, 0.62f);

    private static readonly string[] PlatNames = { "Epic", "Steam", "Mac", "MS Store", "Itch", "iOS", "Android", "Switch", "Xbox", "PS", "Starlight", "Unknown" };

    private void DrawNet(float x, ref float y, float w)
    {
        int ping = -1;
        string server = "—";
        try
        {
            if (AmongUsClient.Instance != null)
            {
                var net = (InnerNetClient)AmongUsClient.Instance;
                ping = net.Ping;
                if (!string.IsNullOrEmpty(net.networkAddress)) server = net.networkAddress + ":" + net.networkPort;
            }
        }
        catch { ping = -1; }

        Rect b = Card(x, ref y, w, NocturneText.T("Соединение", "Connection"), 4f * 30f);
        float by = b.y;
        InfoRow(b.x, ref by, b.width, NocturneText.T("Пинг", "Ping"), ping >= 0 ? ping + " ms" : "—");
        InfoRow(b.x, ref by, b.width, "FPS", NocturneHud.CurrentFps.ToString());
        InfoRow(b.x, ref by, b.width, NocturneText.T("Сервер", "Server"), server);
        InfoRow(b.x, ref by, b.width, NocturneText.T("Лобби", "Lobby"), LobbyBehaviour.Instance != null ? NocturneText.T("в лобби", "in lobby") : NocturneText.T("нет", "no"));

        float spoofBody = 6f * RowH + 30f + (NocturneConfig.SpoofLevelEnabled.Value ? 50f : 0f) + (NocturneConfig.SpoofFriendCodeEnabled.Value ? 58f : 0f);
        b = Card(x, ref y, w, NocturneText.T("Спуф", "Spoof"), spoofBody);
        by = b.y;
        Toggle(b.x, ref by, b.width, NocturneText.T("Спуф платформы", "Spoof platform"), NocturneConfig.SpoofPlatformEnabled);
        int pi = Mathf.Clamp(NocturneConfig.SpoofPlatformIndex.Value, 0, PlatNames.Length - 1);
        CycleRow(b.x, ref by, b.width, NocturneText.T("Платформа", "Platform"), PlatNames[pi], () => { NocturneConfig.SpoofPlatformIndex.Value = (pi + 1) % PlatNames.Length; });
        Toggle(b.x, ref by, b.width, NocturneText.T("Спуф уровня", "Spoof level"), NocturneConfig.SpoofLevelEnabled);
        if (NocturneConfig.SpoofLevelEnabled.Value)
            SliderInt(b.x, ref by, b.width, NocturneText.T("Уровень", "Level"), NocturneConfig.SpoofLevelValue, 1, 9999);
        Toggle(b.x, ref by, b.width, NocturneText.T("Спуф Device ID", "Spoof Device ID"), NocturneConfig.SpoofDeviceId);
        Toggle(b.x, ref by, b.width, NocturneText.T("Спуф ФК", "Spoof friend code"), NocturneConfig.SpoofFriendCodeEnabled);
        if (NocturneConfig.SpoofFriendCodeEnabled.Value)
        {
            _fcText = CustomText(new Rect(b.x + 2f, by, b.width - 190f, 26f), _fcText ?? NocturneConfig.SpoofFriendCodeValue.Value ?? "", "fcSpoof");
            if (SmallButton(new Rect(b.x + b.width - 184f, by + 1f, 86f, 24f), NocturneText.T("ЗАДАТЬ", "SET"), NocturneStyle.Current.Accent))
                NocturneConfig.SpoofFriendCodeValue.Value = (_fcText ?? "").Trim();
            if (SmallButton(new Rect(b.x + b.width - 92f, by + 1f, 88f, 24f), NocturneText.T("РАНДОМ", "RANDOM"), new Color(0.5f, 0.55f, 0.62f)))
            { _fcText = Patches.NocturneFcSpoof.Random(); NocturneConfig.SpoofFriendCodeValue.Value = _fcText; }
            by += 30f;
            GUI.Label(new Rect(b.x + 4f, by, b.width - 6f, 20f), NocturneText.T("Спуф ФК срабатывает при регистрации акка.", "Spoofs the FC set at account registration."), _muted);
            by += 24f;
        }
        if (SmallButton(new Rect(b.x, by, b.width, 24f), NocturneText.T("РАНДОМ ОБРАЗА", "RANDOM OUTFIT"), NocturneStyle.Current.Accent))
            NocturneToast.Push(NocturneText.T("Образ", "Outfit"), NocturneOutfits.Randomize(PlayerControl.LocalPlayer), 2f, NocturneNotifyKind.Info);
        by += 30f;
        Toggle(b.x, ref by, b.width, NocturneText.T("Рандом → сохранять в профиль", "Random → save to profile"), NocturneConfig.RandomOutfitSave);

        bool brOn = NocturneConfig.BugRoomCycle.Value;
        b = Card(x, ref y, w, NocturneText.T("Баг-комнаты", "Bug rooms"), 3f * RowH + 34f + (brOn ? 76f : 26f));
        by = b.y;
        Toggle(b.x, ref by, b.width, NocturneText.T("Крутить цикл", "Run cycle"), NocturneConfig.BugRoomCycle);
        Toggle(b.x, ref by, b.width, NocturneText.T("Поиск: новая комната каждый круг", "Hunt: new room each cycle"), NocturneConfig.BugRoomHunt);
        Toggle(b.x, ref by, b.width, NocturneText.T("Писать найденные в файл", "Log found rooms"), NocturneConfig.BugRoomLog);

        GUI.Label(new Rect(b.x + 2f, by, 118f, 26f), NocturneText.T("Хвосты кодов", "Code endings"), _rowLabel);
        NocturneConfig.BugRoomTargets.Value = CustomText(new Rect(b.x + 122f, by, b.width - 124f, 26f), NocturneConfig.BugRoomTargets.Value ?? "", "bugTails");
        by += 32f;

        if (brOn)
        {
            Slider(b.x, ref by, b.width, NocturneText.T("Пауза", "Delay"), NocturneConfig.BugRoomDelay, 1f, 10f, "0.0");
            GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 22f),
                $"{NocturneBugRooms.Stage}   {NocturneText.T("кругов", "runs")}: <b>{NocturneBugRooms.Runs}</b>   {NocturneText.T("ур.", "lvl")}: <b>{NocturneBugRooms.Was}→{NocturneBugRooms.Lvl}</b>   {NocturneText.T("найдено", "found")}: <b>{NocturneBugRooms.Hits}</b>", _muted);
        }
        else
            GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Пусто = судить по уровню. Nocturne/BugRooms.txt", "Empty = judge by level. Nocturne/BugRooms.txt"), _muted);
    }

    private void DrawVisual(float x, ref float y, float w)
    {
        SubBar(x, ref y, w, VisualSubsRu, VisualSubsEn);

        Grp(1);
        Rect b = Card(x, ref y, w, NocturneText.T("Косметика", "Cosmetics"), 7f * RowH + 6f + (NocturneConfig.SeasonDecor.Value ? 28f : 0f));
        float by = b.y;
        Toggle(b.x, ref by, b.width, NocturneText.T("Разблокировать косметику", "Unlock cosmetics"), NocturneConfig.FreeCosmetics);
        Toggle(b.x, ref by, b.width, NocturneText.T("Прятать косметику в матче", "Hide cosmetics in match"), NocturneConfig.HideCosmeticsInMatch);
        Toggle(b.x, ref by, b.width, NocturneText.T("Одинаковые цвета (хост)", "Duplicate colors (host)"), NocturneConfig.AllowDuplicateColors);
        Toggle(b.x, ref by, b.width, NocturneText.T("Классический вид", "Classic look"), NocturneConfig.ClassicBody);
        Toggle(b.x, ref by, b.width, NocturneText.T("Классика 2018 полностью", "Full 2018 classic"), NocturneConfig.ClassicMode);
        Toggle(b.x, ref by, b.width, NocturneText.T("Классическое главное меню", "Classic main menu"), NocturneConfig.ClassicMenu);
        Toggle(b.x, ref by, b.width, NocturneText.T("Сезонный декор круглый год", "Seasonal decor year-round"), NocturneConfig.SeasonDecor);
        if (NocturneConfig.SeasonDecor.Value)
        {
            int mask = NocturneConfig.SeasonDecorMask.Value;
            float cw = (b.width - 4f) / 2f;
            for (int i = 0; i < 2; i++)
            {
                bool on = (mask & (1 << i)) != 0;
                int seen = Patches.NocturneSeasonDecor.Seen(i);
                string lbl = NocturneText.T(Patches.NocturneSeasonDecor.Ru[i], Patches.NocturneSeasonDecor.En[i]);
                if (seen > 0) lbl += " " + seen;
                if (SmallButton(new Rect(b.x + i * (cw + 4f), by, cw, 24f), lbl, on ? NocturneStyle.Current.Accent : new Color(0.36f, 0.39f, 0.47f)))
                    NocturneConfig.SeasonDecorMask.Value = mask ^ (1 << i);
            }
            by += 28f;
        }

        Grp(1);
        DrawSnipe(x, ref y, w);
        Grp(1);
        DrawColorAll(x, ref y, w);

        float camBody = 3f * RowH
            + (NocturneConfig.VisualFreeCamera.Value ? 50f : 0f)
            + (NocturneConfig.VisualCameraZoom.Value ? RowH : 0f);
        Grp(0);
        b = Card(x, ref y, w, NocturneText.T("Камера", "Camera"), camBody);
        by = b.y;
        Toggle(b.x, ref by, b.width, NocturneText.T("Свободная камера (WASD)", "Free camera (WASD)"), NocturneConfig.VisualFreeCamera);
        if (NocturneConfig.VisualFreeCamera.Value)
            SliderInt(b.x, ref by, b.width, NocturneText.T("Скорость камеры", "Camera speed"), NocturneConfig.VisualFreeCameraSpeed, 4, 30);
        Toggle(b.x, ref by, b.width, NocturneText.T("Зум колёсиком (безлимит)", "Wheel zoom (unlimited)"), NocturneConfig.VisualCameraZoom);
        if (NocturneConfig.VisualCameraZoom.Value)
            Toggle(b.x, ref by, b.width, NocturneText.T("Держать зум на тасках", "Keep zoom during tasks"), NocturneConfig.ZoomDuringTasks);
        Toggle(b.x, ref by, b.width, NocturneText.T("Ноклип", "No-clip"), NocturneConfig.VisualNoClip);

        float espH = 8f * RowH
            + (NocturneConfig.EspBoxes.Value ? 5f * RowH + 24f : 0f)
            + (NocturneConfig.EspSideColors.Value ? 3f * RowH : 0f)
            + (NocturneConfig.NeonOutline.Value ? RowH : 0f)
            + (NocturneConfig.OverheadChat.Value ? RowH + 52f : 0f);
        Grp(0);
        b = Card(x, ref y, w, "ESP", espH);
        by = b.y;
        Toggle(b.x, ref by, b.width, NocturneText.T("Боксы сквозь стены", "Boxes through walls"), NocturneConfig.EspBoxes);
        if (NocturneConfig.EspBoxes.Value)
        {
            Toggle(b.x, ref by, b.width, NocturneText.T("Ник и дистанция", "Name and distance"), NocturneConfig.EspNames);
            Toggle(b.x, ref by, b.width, NocturneText.T("Показывать мёртвых", "Show dead players"), NocturneConfig.EspDead);
            GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 22f), NocturneText.T("Кого показывать:", "Who to show:"), _muted);
            by += 24f;
            Toggle(b.x, ref by, b.width, NocturneText.T("Предатели (пред, оборотень, фантом, гадюка)", "Impostors (imp, shifter, phantom, viper)"), NocturneConfig.EspSeeImps);
            Toggle(b.x, ref by, b.width, NocturneText.T("Ангел-хранитель", "Guardian angel"), NocturneConfig.EspSeeAngels);
            Toggle(b.x, ref by, b.width, NocturneText.T("Мирные (и все роли мирных)", "Crewmates (and all crew roles)"), NocturneConfig.EspSeeCrew);
        }
        Toggle(b.x, ref by, b.width, NocturneText.T("Трейсеры до игроков", "Tracers to players"), NocturneConfig.Tracers);
        Toggle(b.x, ref by, b.width, NocturneText.T("Трейсеры к телам", "Tracers to bodies"), NocturneConfig.TracerBodies);
        Toggle(b.x, ref by, b.width, NocturneText.T("Стрелки к таскам", "Task arrows"), NocturneConfig.TaskArrows);
        Toggle(b.x, ref by, b.width, NocturneText.T("Цвет ESP по сторонам", "ESP color by side"), NocturneConfig.EspSideColors);
        if (NocturneConfig.EspSideColors.Value)
        {
            CycleRow(b.x, ref by, b.width, NocturneText.T("Мирные", "Crewmates"), NocturneTracers.SideName(NocturneConfig.EspCrewColor.Value), () => NocturneConfig.EspCrewColor.Value = (NocturneConfig.EspCrewColor.Value + 1) % NocturneTracers.SideCount);
            CycleRow(b.x, ref by, b.width, NocturneText.T("Предатели", "Impostors"), NocturneTracers.SideName(NocturneConfig.EspImpColor.Value), () => NocturneConfig.EspImpColor.Value = (NocturneConfig.EspImpColor.Value + 1) % NocturneTracers.SideCount);
            CycleRow(b.x, ref by, b.width, NocturneText.T("Мёртвое тело", "Dead body"), NocturneTracers.SideName(NocturneConfig.EspDeadColor.Value), () => NocturneConfig.EspDeadColor.Value = (NocturneConfig.EspDeadColor.Value + 1) % NocturneTracers.SideCount);
        }
        Toggle(b.x, ref by, b.width, NocturneText.T("КД килла над убийцами", "Kill CD over killers"), NocturneConfig.KillTimers);
        Toggle(b.x, ref by, b.width, NocturneText.T("Сообщения над игроками", "Chat above players"), NocturneConfig.OverheadChat);
        if (NocturneConfig.OverheadChat.Value)
        {
            string[] ocWhere = { NocturneText.T("Лобби и матч", "Lobby and match"), NocturneText.T("Только матч", "Match only"), NocturneText.T("Только лобби", "Lobby only") };
            int ocw = Mathf.Clamp(NocturneConfig.OverheadChatWhere.Value, 0, 2);
            CycleRow(b.x, ref by, b.width, NocturneText.T("Показывать", "Show in"), ocWhere[ocw], () => { NocturneConfig.OverheadChatWhere.Value = (ocw + 1) % 3; });
            SliderInt(b.x, ref by, b.width, NocturneText.T("Держать сообщение", "Message time"), NocturneConfig.OverheadChatTime, 2, 15);
        }
        Toggle(b.x, ref by, b.width, NocturneText.T("Неон-обводка игроков", "Neon outline"), NocturneConfig.NeonOutline);
        if (NocturneConfig.NeonOutline.Value)
        {
            string[] noModes = { NocturneText.T("Радуга", "Rainbow"), NocturneText.T("По роли", "By role"), NocturneText.T("Цвет игрока", "Player color") };
            int nm = Mathf.Clamp(NocturneConfig.NeonOutlineMode.Value, 0, 2);
            CycleRow(b.x, ref by, b.width, NocturneText.T("Цвет обводки", "Outline color"), noModes[nm], () => { NocturneConfig.NeonOutlineMode.Value = (nm + 1) % 3; });
        }

        Grp(0);
        b = Card(x, ref y, w, NocturneText.T("Обзор", "Vision"), 3f * RowH + 22f);
        by = b.y;
        Toggle(b.x, ref by, b.width, NocturneText.T("Видеть игроков в вентах", "See players in vents"), NocturneConfig.SeeVents);
        Toggle(b.x, ref by, b.width, NocturneText.T("Видеть призраков", "See ghosts"), NocturneConfig.SeeGhosts);
        Toggle(b.x, ref by, b.width, NocturneText.T("Wallhack (без тьмы)", "Wallhack (no darkness)"), NocturneConfig.Wallhack);
        GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 20f), NocturneText.T("Убирает завесу обзора — видно всю карту и всех.", "Removes the vision veil — see the whole map and everyone."), _muted);

        Grp(0);
        b = Card(x, ref y, w, NocturneText.T("Радар (миникарта)", "Radar (minimap)"), 4f * RowH + 130f);
        by = b.y;
        Toggle(b.x, ref by, b.width, NocturneText.T("Показывать радар", "Show radar"), NocturneConfig.Radar);
        Toggle(b.x, ref by, b.width, NocturneText.T("Тела на радаре", "Bodies on radar"), NocturneConfig.RadarBodies);
        Toggle(b.x, ref by, b.width, NocturneText.T("Телепорт по ПКМ на радаре", "Right-click radar to teleport"), NocturneConfig.RadarTeleport);
        Toggle(b.x, ref by, b.width, NocturneText.T("Кнопки дверей (пред)", "Door buttons (impostor)"), NocturneConfig.RadarDoors);
        SliderInt(b.x, ref by, b.width, NocturneText.T("Размер, %", "Size, %"), NocturneConfig.RadarSize, 60, 180);
        SliderInt(b.x, ref by, b.width, NocturneText.T("Прозрачность, %", "Opacity, %"), NocturneConfig.RadarOpacity, 30, 100);
        by += 4f;
        GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 20f), NocturneText.T("Двигать — ЛКМ по радару. Скелет карты + точки игроков.", "Drag with LMB. Map skeleton + player dots."), _muted);

        Grp(0);
        b = Card(x, ref y, w, NocturneText.T("Разбор матча (карта)", "Match review (map)"), RowH * 3f + 24f);
        by = b.y;
        Toggle(b.x, ref by, b.width, NocturneText.T("Окно разбора", "Review window"), NocturneConfig.ReplayView);
        Toggle(b.x, ref by, b.width, NocturneText.T("Чистить после собрания", "Clear after meeting"), NocturneConfig.ReplayClearAfterMeeting);
        KeyRow(b.x, ref by, b.width, NocturneText.T("Открыть разбор", "Open review"), NocturneConfig.ReplayKey);
        GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 20f), NocturneText.T("Пути + события на карте. Виден и в лобби после раунда.", "Paths + events on a map. Visible in the lobby after a round."), _muted);

        Grp(2);
        b = Card(x, ref y, w, NocturneText.T("Анимации (на себе)", "Animations (self)"), 6f * 28f + 24f + RowH);
        by = b.y;
        Toggle(b.x, ref by, b.width, NocturneText.T("Лунная походка", "Moonwalk"), NocturneConfig.MoonWalk);
        Color anac = NocturneStyle.Current.Accent;
        Color anon = new Color(0.32f, 0.78f, 0.45f);
        float anbw = (b.width - 6f) / 2f;
        if (SmallButton(new Rect(b.x, by, anbw, 24f), NocturneText.T("Лезть ↑", "Climb ↑"), NocturneAnimations.Active(NocturneAnim.ClimbUp) ? anon : anac)) NocturneAnimations.Toggle(NocturneAnim.ClimbUp);
        if (SmallButton(new Rect(b.x + anbw + 6f, by, anbw, 24f), NocturneText.T("Лезть ↓", "Climb ↓"), NocturneAnimations.Active(NocturneAnim.ClimbDown) ? anon : anac)) NocturneAnimations.Toggle(NocturneAnim.ClimbDown);
        by += 28f;
        if (SmallButton(new Rect(b.x, by, anbw, 24f), NocturneText.T("В люк", "Enter vent"), NocturneAnimations.Active(NocturneAnim.EnterVent) ? anon : anac)) NocturneAnimations.Toggle(NocturneAnim.EnterVent);
        if (SmallButton(new Rect(b.x + anbw + 6f, by, anbw, 24f), NocturneText.T("Из люка", "Exit vent"), NocturneAnimations.Active(NocturneAnim.ExitVent) ? anon : anac)) NocturneAnimations.Toggle(NocturneAnim.ExitVent);
        by += 28f;
        if (SmallButton(new Rect(b.x, by, anbw, 24f), NocturneText.T("Прыжок", "Jump"), NocturneAnimations.Active(NocturneAnim.Jump) ? anon : anac)) NocturneAnimations.Toggle(NocturneAnim.Jump);
        if (SmallButton(new Rect(b.x + anbw + 6f, by, anbw, 24f), NocturneText.T("Спавн", "Spawn"), NocturneAnimations.Active(NocturneAnim.Spawn) ? anon : anac)) NocturneAnimations.Toggle(NocturneAnim.Spawn);
        by += 28f;
        if (SmallButton(new Rect(b.x, by, anbw, 24f), NocturneText.T("Туман вкл", "Fog on"), anac)) NocturneAnimations.MushroomIn();
        if (SmallButton(new Rect(b.x + anbw + 6f, by, anbw, 24f), NocturneText.T("Туман выкл", "Fog off"), anac)) NocturneAnimations.MushroomOut();
        by += 28f;
        if (SmallButton(new Rect(b.x, by, anbw, 24f), NocturneText.T("Вспышка", "Alert flash"), anac)) NocturneAnimations.AlertFlash();
        if (SmallButton(new Rect(b.x + anbw + 6f, by, anbw, 24f), NocturneText.T("Звук митинга", "Meeting sting"), anac)) NocturneAnimations.MeetingSting();
        by += 28f;
        if (SmallButton(new Rect(b.x, by, anbw, 24f), NocturneText.T("Звук выброса", "Eject sfx"), anac)) NocturneAnimations.EjectSfx();
        if (SmallButton(new Rect(b.x + anbw + 6f, by, anbw, 24f), NocturneText.T("Сброс", "Reset"), new Color(0.85f, 0.32f, 0.32f))) NocturneAnimations.ResetAll();
        by += 28f;
        GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 20f), NocturneText.T("Тумблеры держат позу, «Сброс» вернёт норму. Для роликов.", "Toggles hold the pose, 'Reset' restores. For clips."), _muted);

        Grp(2);
        b = Card(x, ref y, w, NocturneText.T("Стелс", "Stealth"), RowH);
        by = b.y;
        Toggle(b.x, ref by, b.width, NocturneText.T("Скрыть MOD-штамп", "Hide MOD stamp"), NocturneConfig.HideModStamp);

        Grp(2);
        b = Card(x, ref y, w, NocturneText.T("Пропуск анимаций", "Skip animations"), RowH);
        by = b.y;
        Toggle(b.x, ref by, b.width, NocturneText.T("Интро «Shhh»", "'Shhh' intro"), NocturneConfig.SkipShhh);

        Grp(1);
        b = Card(x, ref y, w, NocturneText.T("Избранные образы", "Favorite outfits"), 4f * 30f + 60f);
        by = b.y;
        for (int i = 0; i < 4; i++) FavoriteRow(b.x, ref by, b.width, i);
        float cwHalf = (b.width - 8f) / 2f;
        if (SmallButton(new Rect(b.x, by, cwHalf, 24f), NocturneText.T("ОБРАЗ ВЫБР. → МНЕ", "OUTFIT → ME"), NocturneStyle.Current.Accent))
        {
            string cap = NocturneOutfits.Capture(NocturneMouseTools.Selected);
            NocturneToast.Push(NocturneText.T("Образ", "Outfit"), cap.Length > 0 && NocturneOutfits.Apply(PlayerControl.LocalPlayer, cap) ? NocturneText.T("скопирован", "copied") : NocturneText.T("нет цели", "no target"), 2f, NocturneNotifyKind.Info);
        }
        if (SmallButton(new Rect(b.x + cwHalf + 8f, by, cwHalf, 24f), NocturneText.T("МОРФ В ВЫБР. (хост)", "MORPH INTO (host)"), NocturneStyle.Current.Accent))
            NocturneToast.Push(NocturneText.T("Морф", "Morph"), NocturneMorph.Into(PlayerControl.LocalPlayer, NocturneMouseTools.Selected), 2.2f, NocturneNotifyKind.Info);
        by += 28f;
        GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 22f), NocturneText.T("Цель — выбранный мышью. Морф копирует ник+образ намертво.", "Target = mouse-selected. Morph copies nick+look for good."), _muted);

        Grp(1);
        b = Card(x, ref y, w, NocturneText.T("Режим тела", "Body mode"), RowH);
        by = b.y;
        string[] bmVals = { "Disabled", "Horse", "Seeker", "Long", "LongHorse" };
        string[] bmDisp = { NocturneText.T("Выкл", "Off"), NocturneText.T("Лошадь", "Horse"), NocturneText.T("Сикер", "Seeker"), NocturneText.T("Длинный", "Long"), NocturneText.T("Длинная лошадь", "Long horse") };
        int bi = Mathf.Max(0, Array.IndexOf(bmVals, NocturneConfig.BodyMode.Value));
        CycleRow(b.x, ref by, b.width, NocturneText.T("Стиль тела", "Body style"), bmDisp[bi], () => { NocturneConfig.BodyMode.Value = bmVals[(bi + 1) % bmVals.Length]; });

        Grp(1);
        b = Card(x, ref y, w, NocturneText.T("Цветной ник", "Colored name"), 3f * RowH);
        by = b.y;
        Toggle(b.x, ref by, b.width, NocturneText.T("Включить цветной ник", "Enable colored name"), NocturneConfig.NameColor);
        int nci = NocturneNameColor.Clamp(NocturneConfig.NameColorStyle.Value);
        CycleRow(b.x, ref by, b.width, NocturneText.T("Стиль", "Style"), NocturneNameColor.StyleName(nci), () => { NocturneConfig.NameColorStyle.Value = NocturneNameColor.Next(nci); });
        Toggle(b.x, ref by, b.width, NocturneText.T("Анимация", "Animation"), NocturneConfig.NameColorAnimated);
    }

    private void FavoriteRow(float x, ref float y, float w, int i)
    {
        ConfigEntry<string> slot = i < NocturneConfig.FavoriteOutfits.Length ? NocturneConfig.FavoriteOutfits[i] : null;
        string data = slot != null ? slot.Value : "";
        bool has = !string.IsNullOrWhiteSpace(data);
        var r = new Rect(x, y, w, 26f);
        HoverFill(r);
        GUI.Label(new Rect(r.x + 4f, r.y, 52f, 26f), NocturneText.T($"Слот {i + 1}", $"Slot {i + 1}"), _rowLabel);
        GUI.Label(new Rect(r.x + 58f, r.y, 88f, 26f), has ? NocturneOutfits.Summary(data) : NocturneText.T("пусто", "empty"), _muted);

        var apply = new Rect(r.xMax - 208f, r.y + 1f, 66f, 24f);
        var mine = new Rect(r.xMax - 138f, r.y + 1f, 48f, 24f);
        var sel = new Rect(r.xMax - 86f, r.y + 1f, 52f, 24f);
        var clr = new Rect(r.xMax - 30f, r.y + 1f, 26f, 24f);
        if (SmallButton(apply, NocturneText.T("НАДЕТЬ", "APPLY"), has ? NocturneStyle.Current.Accent : new Color(0.5f, 0.55f, 0.62f)) && has)
            NocturneToast.Push(NocturneText.T("Образ", "Outfit"), NocturneOutfits.Apply(PlayerControl.LocalPlayer, data) ? NocturneText.T("надет", "applied") : NocturneText.T("не готов", "not ready"), 2f, NocturneNotifyKind.Info);
        if (SmallButton(mine, NocturneText.T("МОЙ", "MINE"), new Color(0.5f, 0.55f, 0.62f)) && slot != null)
        { string cap = NocturneOutfits.Capture(PlayerControl.LocalPlayer); if (cap.Length > 0) slot.Value = cap; }
        if (SmallButton(sel, NocturneText.T("ВЫБР.", "SEL."), new Color(0.5f, 0.55f, 0.62f)) && slot != null)
        { string cap = NocturneOutfits.Capture(NocturneMouseTools.Selected); if (cap.Length > 0) slot.Value = cap; }
        if (SmallButton(clr, "✕", new Color(0.9f, 0.4f, 0.4f)) && slot != null) slot.Value = "";
        y += 30f;
    }

    private void DrawSettings(float x, ref float y, float w)
    {
        SubBar(x, ref y, w, SettingsSubsRu, SettingsSubsEn);

        float gw = w - 56f;
        Grp(0);
        Rect b = Card(x, ref y, w, NocturneText.T("Интерфейс", "Interface"), 2f * RowH + 50f + 30f + ThemeGridHeight(gw));
        float by = b.y;
        CycleRow(b.x, ref by, b.width, NocturneText.T("Язык", "Language"), NocturneText.LangName, () => NocturneText.Toggle());
        Toggle(b.x, ref by, b.width, NocturneText.T("Лёгкий режим (для слабых ПК)", "Lite mode (weak PCs)"), NocturneConfig.LiteMenu);
        Slider(b.x, ref by, b.width, NocturneText.T("Масштаб меню", "Menu scale"), NocturneConfig.MenuScale, 0.7f, 1.8f, "0.00");
        GUI.Label(new Rect(b.x + 12f, by, b.width - 24f, 20f), NocturneText.T("Тема (акцент)", "Theme (accent)"), _muted);
        by += 24f;
        ThemeSwatches(b.x + 10f, by, b.width - 20f);

        Grp(1);
        b = Card(x, ref y, w, NocturneText.T("Горячие клавиши", "Hotkeys"), 10f * RowH + 24f);
        by = b.y;
        KeyRow(b.x, ref by, b.width, NocturneText.T("Меню", "Menu"), NocturneConfig.MenuKey);
        KeyRow(b.x, ref by, b.width, NocturneText.T("Код лобби", "Lobby code"), NocturneConfig.CopyCodeKey);
        KeyRow(b.x, ref by, b.width, NocturneText.T("Завершить матч", "End match"), NocturneConfig.EndMatchKey);
        KeyRow(b.x, ref by, b.width, NocturneText.T("Досчитать голоса", "Tally votes"), NocturneConfig.CloseVotingKey);
        KeyRow(b.x, ref by, b.width, NocturneText.T("Закрыть собрание", "Close meeting"), NocturneConfig.CloseMeetingKey);
        KeyRow(b.x, ref by, b.width, NocturneText.T("Радиал (удержание)", "Radial (hold)"), NocturneConfig.RadialKey);
        KeyRow(b.x, ref by, b.width, NocturneText.T("Консоль событий", "Event console"), NocturneConfig.EventConsoleKey);
        KeyRow(b.x, ref by, b.width, NocturneText.T("Окно чата", "Chat window"), NocturneConfig.ChatWindowKey);
        KeyRow(b.x, ref by, b.width, NocturneText.T("Обычный чат", "Game chat"), NocturneConfig.OpenChatKey);
        KeyRow(b.x, ref by, b.width, NocturneText.T("Спам чата", "Chat spam"), NocturneConfig.ChatSpamKey);
        GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 22f), NocturneText.T("Клик — назначить, корзина/ПКМ — сброс, Esc — отмена.", "Click to set, trash/RMB to clear, Esc to cancel."), _muted);

        Grp(1);
        b = Card(x, ref y, w, NocturneText.T("Клавиши читов", "Cheat keys"), 16f * RowH);
        by = b.y;
        KeyRow(b.x, ref by, b.width, NocturneText.T("God Mode", "God Mode"), NocturneConfig.GodModeKey);
        KeyRow(b.x, ref by, b.width, NocturneText.T("Мираж", "Mirage"), NocturneConfig.MirageKey);
        KeyRow(b.x, ref by, b.width, NocturneText.T("Невидимость", "Invisibility"), NocturneConfig.InvisibleKey);
        KeyRow(b.x, ref by, b.width, NocturneText.T("Ноклип", "No-clip"), NocturneConfig.NoClipKey);
        KeyRow(b.x, ref by, b.width, NocturneText.T("Зум", "Zoom"), NocturneConfig.ZoomKey);
        KeyRow(b.x, ref by, b.width, NocturneText.T("Авто-войткик", "Auto votekick"), NocturneConfig.VotekickKey);
        KeyRow(b.x, ref by, b.width, NocturneText.T("Войткик всех", "Votekick everyone"), NocturneConfig.VotekickAllKey);
        KeyRow(b.x, ref by, b.width, NocturneText.T("Войткик хоста", "Votekick host"), NocturneConfig.VotekickHostKey);
        KeyRow(b.x, ref by, b.width, NocturneText.T("Перезайти в игру", "Rejoin last game"), NocturneConfig.RejoinLastKey);
        KeyRow(b.x, ref by, b.width, NocturneText.T("Выбрать всех (вент кик)", "Select all (vent kick)"), NocturneConfig.VentKickSelectKey);
        KeyRow(b.x, ref by, b.width, NocturneText.T("Вент кик", "Vent kick"), NocturneConfig.VentKickKey);
        KeyRow(b.x, ref by, b.width, NocturneText.T("Выбрать всех (катать)", "Select all (rides)"), NocturneConfig.ZiplineSelectKey);
        KeyRow(b.x, ref by, b.width, NocturneText.T("Зиплайн вниз", "Zipline down"), NocturneConfig.ZiplineDownKey);
        KeyRow(b.x, ref by, b.width, NocturneText.T("Зиплайн вверх", "Zipline up"), NocturneConfig.ZiplineUpKey);
        KeyRow(b.x, ref by, b.width, NocturneText.T("Саботаж всего", "Sabotage all"), NocturneConfig.SabotageKey);
        KeyRow(b.x, ref by, b.width, NocturneText.T("Закрыть двери", "Close doors"), NocturneConfig.DoorsKey);

        Grp(2);
        b = Card(x, ref y, w, NocturneText.T("Призраки и лобби", "Ghosts & lobby"), 8f * RowH + 22f);
        by = b.y;
        KeyRow(b.x, ref by, b.width, NocturneText.T("Призрак после старта", "Ghost after start"), NocturneConfig.GhostKey);
        KeyRow(b.x, ref by, b.width, NocturneText.T("Суицид (пред)", "Suicide (impostor)"), NocturneConfig.GhostNowKey);
        KeyRow(b.x, ref by, b.width, NocturneText.T("ТП отмеченных в люк", "TP marked to vent"), NocturneConfig.VentTpKey);
        KeyRow(b.x, ref by, b.width, NocturneText.T("Сменить люк", "Cycle vent"), NocturneConfig.VentCycleKey);
        KeyRow(b.x, ref by, b.width, NocturneText.T("Видеть призраков", "See ghosts"), NocturneConfig.SeeGhostsKey);
        KeyRow(b.x, ref by, b.width, NocturneText.T("Спавн лобби (хост)", "Spawn lobby (host)"), NocturneConfig.SpawnLobbyKey);
        KeyRow(b.x, ref by, b.width, NocturneText.T("Деспавн лобби (хост)", "Despawn lobby (host)"), NocturneConfig.DespawnLobbyKey);
        KeyRow(b.x, ref by, b.width, NocturneText.T("Выйти из лобби", "Leave lobby"), NocturneConfig.LeaveLobbyKey);
        GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 20f), NocturneText.T("Выход — двойное нажатие в течение 3 с.", "Leave — press twice within 3s."), _muted);

        Grp(2);
        b = Card(x, ref y, w, NocturneText.T("Фан (хост)", "Fun (host)"), 9f * RowH);
        by = b.y;
        KeyRow(b.x, ref by, b.width, NocturneText.T("Все в яйца", "All to eggs"), NocturneConfig.FunEggKey);
        KeyRow(b.x, ref by, b.width, NocturneText.T("Морф в выбранного", "Morph to target"), NocturneConfig.FunMorphKey);
        KeyRow(b.x, ref by, b.width, NocturneText.T("Радуга", "Rainbow"), NocturneConfig.FunRainbowKey);
        KeyRow(b.x, ref by, b.width, NocturneText.T("Цикл косметики", "Cosmetic cycle"), NocturneConfig.FunSkinCycleKey);
        KeyRow(b.x, ref by, b.width, NocturneText.T("Такт", "Beat"), NocturneConfig.FunBeatKey);
        KeyRow(b.x, ref by, b.width, NocturneText.T("Размер", "Size"), NocturneConfig.FunSizeKey);
        KeyRow(b.x, ref by, b.width, NocturneText.T("Движение", "Motion"), NocturneConfig.FunMotionKey);
        KeyRow(b.x, ref by, b.width, NocturneText.T("Анимация", "Animation"), NocturneConfig.FunAnimKey);
        KeyRow(b.x, ref by, b.width, NocturneText.T("Сброс облика", "Reset look"), NocturneConfig.FunResetKey);

        Grp(1);
        b = Card(x, ref y, w, NocturneText.T("Клавиши плеера", "Player keys"), 7f * RowH);
        by = b.y;
        KeyRow(b.x, ref by, b.width, NocturneText.T("Открыть плеер", "Open player"), NocturneConfig.MusicToggleKey);
        KeyRow(b.x, ref by, b.width, NocturneText.T("Предыдущий трек", "Previous track"), NocturneConfig.MusicPrevKey);
        KeyRow(b.x, ref by, b.width, NocturneText.T("Следующий трек", "Next track"), NocturneConfig.MusicNextKey);
        KeyRow(b.x, ref by, b.width, NocturneText.T("Играть / Пауза", "Play / Pause"), NocturneConfig.MusicPlayPauseKey);
        KeyRow(b.x, ref by, b.width, NocturneText.T("Стоп", "Stop"), NocturneConfig.MusicStopKey);
        KeyRow(b.x, ref by, b.width, NocturneText.T("Громкость +", "Volume up"), NocturneConfig.MusicVolumeUpKey);
        KeyRow(b.x, ref by, b.width, NocturneText.T("Громкость -", "Volume down"), NocturneConfig.MusicVolumeDownKey);

        Grp(2);
        b = Card(x, ref y, w, NocturneText.T("Аккаунт / штрафы", "Account / penalties"), 3f * RowH + 6f);
        by = b.y;
        Toggle(b.x, ref by, b.width, NocturneText.T("Сбросить штраф за выход", "Clear disconnect penalty"), NocturneConfig.ClearDisconnectPenalty);
        Toggle(b.x, ref by, b.width, NocturneText.T("Гостю доп. функции", "Guest extra features"), NocturneConfig.RemoveGuestLimits);
        Toggle(b.x, ref by, b.width, NocturneText.T("Игнор. ограничения", "Ignore restrictions"), NocturneConfig.RemoveMinorLimits);

        Grp(0);
        b = Card(x, ref y, w, NocturneText.T("Приватность", "Privacy"), RowH + 26f);
        by = b.y;
        Toggle(b.x, ref by, b.width, NocturneText.T("Блок телеметрии / данных", "Block telemetry / data"), NocturneConfig.BlockTelemetry);
        GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Применяется при запуске игры.", "Applied on game start."), _muted);
    }

    private void DrawEmpty(Rect area, NocturneIcon icon, string msg)
    {
        NocturnePalette p = NocturneStyle.Current;
        float s = 66f;
        var ic = new Rect(area.center.x - s / 2f, area.y + area.height * 0.30f, s, s);
        NocturneStyle.FillRounded(new Rect(ic.x - 12f, ic.y - 12f, s + 24f, s + 24f), A(p.Accent, 0.06f), 22);
        NocturneIcons.Draw(icon, ic, A(p.Muted, 0.55f));
        GUI.Label(new Rect(area.x, ic.yMax + 16f, area.width, 24f), msg, _centerMuted);
    }

    [HideFromIl2Cpp]
    private void ActionRow(Rect body, NocturneIcon icon, string label, string btn, Action onClick)
    {
        NocturnePalette p = NocturneStyle.Current;
        var row = new Rect(body.x, body.y, body.width, 44f);
        var sq = new Rect(row.x, row.y + 2f, 40f, 40f);
        NocturneStyle.FillRounded(sq, A(p.Accent, 0.14f), 10);
        NocturneIcons.Draw(icon, new Rect(sq.x + 10f, sq.y + 10f, 20f, 20f), p.Accent);
        GUI.Label(new Rect(sq.xMax + 14f, row.y, row.width - 170f, 44f), label, _rowLabel);

        var bt = new Rect(row.xMax - 112f, row.y + 5f, 112f, 34f);
        bool hover = bt.Contains(M);
        NocturneStyle.FillRounded(bt, p.Accent, 9);
        NocturneStyle.FillRounded(new Rect(bt.x + 1.5f, bt.y + 1.5f, bt.width - 3f, bt.height - 3f), hover ? A(p.Accent, 0.30f) : p.Panel, 8);
        NocturneStyle.Fill(new Rect(bt.x + 8f, bt.y + 4f, bt.width - 16f, 1f), A(Color.white, 0.10f));
        GUI.Label(bt, btn, _btnLabel);
        if (GUI.Button(bt, GUIContent.none, _invisible)) onClick();
    }

    private void InfoRow(float x, ref float y, float w, string label, string value)
    {
        GUI.Label(new Rect(x + 2f, y, w * 0.5f, 28f), label, _muted);
        GUI.Label(new Rect(x + w * 0.5f, y, w * 0.5f - 2f, 28f), value, _value);
        y += 30f;
    }

    [HideFromIl2Cpp]
    private void Toggle(float x, ref float y, float w, string label, ConfigEntry<bool> entry)
    {
        if (entry == null) { y += RowH; return; }
        string fk = FavKey(entry);
        var it = FavReg(fk, FavKind.Bool); if (it != null) { it.Label = label; it.B = entry; }
        if (Cull(y, RowH)) { y += RowH; return; }
        FavRow(new Rect(x, y, w, RowH - 4f), fk);
        if (NocturneStyle.Lite)
        {
            bool nv = GUI.Toggle(new Rect(x + 4f, y, w - 8f, RowH - 4f), entry.Value, label);
            if (nv != entry.Value) entry.Value = nv;
            y += RowH;
            return;
        }
        var r = new Rect(x, y, w, RowH - 4f);
        HoverFill(r);
        if (GUI.Button(r, GUIContent.none, _invisible))
            entry.Value = !entry.Value;

        GUI.Label(new Rect(r.x + 10f, r.y, r.width - 74f, r.height), label, _rowLabel);
        DrawPill(new Rect(r.xMax - 50f, r.y + (r.height - 24f) / 2f, 46f, 24f), entry.Value, entry);
        y += RowH;
    }

    [HideFromIl2Cpp]
    private void Slider(float x, ref float y, float w, string label, ConfigEntry<float> entry, float min, float max, string fmt)
    {
        if (entry == null) { y += RowH; return; }
        string fk = FavKey(entry);
        var fit = FavReg(fk, FavKind.Float); if (fit != null) { fit.Label = label; fit.F = entry; fit.Min = min; fit.Max = max; fit.Fmt = fmt; }
        if (Cull(y, 50f)) { y += 50f; return; }
        FavRow(new Rect(x, y, w, 46f), fk);
        if (NocturneStyle.Lite)
        {
            GUI.Label(new Rect(x + 6f, y, w - 70f, 20f), label, _rowLabel);
            GUI.Label(new Rect(x + w - 60f, y, 56f, 20f), entry.Value.ToString(fmt), _value);
            y += 22f;
            entry.Value = GUI.HorizontalSlider(new Rect(x + 6f, y + 3f, w - 12f, 16f), entry.Value, min, max);
            y += 24f;
            return;
        }
        NocturnePalette p = NocturneStyle.Current;
        GUI.Label(new Rect(x + 10f, y, w - 74f, 22f), label, _rowLabel);
        GUI.Label(new Rect(x + w - 58f, y, 54f, 22f), entry.Value.ToString(fmt), _value);
        y += 26f;

        var track = new Rect(x + 10f, y + 2f, w - 20f, 8f);
        int id = label.GetHashCode();
        Event e = Event.current;
        var hit = new Rect(track.x - 8f, track.y - 10f, track.width + 16f, 28f);
        if (e != null && e.type == EventType.MouseDown && hit.Contains(e.mousePosition)) { _slider = id; e.Use(); }
        if (_slider == id && e != null)
        {
            if (e.type == EventType.MouseDrag || e.type == EventType.MouseDown)
            {
                entry.Value = Mathf.Lerp(min, max, Mathf.Clamp01((e.mousePosition.x - track.x) / track.width));
                e.Use();
            }
            else if (e.type == EventType.MouseUp) { _slider = 0; e.Use(); }
        }

        float t = Mathf.InverseLerp(min, max, entry.Value);
        float kx = track.x + track.width * t;
        NocturneStyle.FillRounded(track, p.Button, 4);
        NocturneStyle.FillRounded(new Rect(track.x, track.y, Mathf.Max(8f, track.width * t), track.height), p.Accent, 4);
        NocturneStyle.FillRounded(new Rect(kx - 9f, track.y + track.height / 2f - 9f, 18f, 18f), A(p.Accent, 0.35f), 9);
        NocturneStyle.FillRounded(new Rect(kx - 6f, track.y + track.height / 2f - 6f, 12f, 12f), Color.white, 6);
        y += 24f;
    }

    [HideFromIl2Cpp]
    private void SliderInt(float x, ref float y, float w, string label, ConfigEntry<int> entry, int min, int max)
    {
        if (entry == null) { y += RowH; return; }
        string fk = FavKey(entry);
        var iit = FavReg(fk, FavKind.Int); if (iit != null) { iit.Label = label; iit.I = entry; iit.Min = min; iit.Max = max; }
        FavRow(new Rect(x, y, w, 46f), fk);
        if (NocturneStyle.Lite)
        {
            GUI.Label(new Rect(x + 6f, y, w - 70f, 20f), label, _rowLabel);
            GUI.Label(new Rect(x + w - 60f, y, 56f, 20f), entry.Value.ToString(), _value);
            y += 22f;
            entry.Value = Mathf.RoundToInt(GUI.HorizontalSlider(new Rect(x + 6f, y + 3f, w - 12f, 16f), entry.Value, min, max));
            y += 24f;
            return;
        }
        NocturnePalette p = NocturneStyle.Current;
        GUI.Label(new Rect(x + 10f, y, w - 74f, 22f), label, _rowLabel);
        GUI.Label(new Rect(x + w - 58f, y, 54f, 22f), entry.Value.ToString(), _value);
        y += 26f;

        const float arrowW = 24f;
        var leftBtn = new Rect(x + 8f, y - 3f, arrowW, 20f);
        var rightBtn = new Rect(x + w - arrowW - 8f, y - 3f, arrowW, 20f);
        if (ArrowButton(leftBtn, "◂")) entry.Value = Mathf.Clamp(entry.Value - 1, min, max);
        if (ArrowButton(rightBtn, "▸")) entry.Value = Mathf.Clamp(entry.Value + 1, min, max);

        var track = new Rect(leftBtn.xMax + 8f, y + 2f, rightBtn.x - leftBtn.xMax - 16f, 8f);
        int id = label.GetHashCode();
        Event e = Event.current;
        var hit = new Rect(track.x, track.y - 10f, track.width, 28f);
        if (e != null && e.type == EventType.MouseDown && hit.Contains(e.mousePosition)) { _slider = id; e.Use(); }
        if (_slider == id && e != null)
        {
            if (e.type == EventType.MouseDrag || e.type == EventType.MouseDown)
            {
                entry.Value = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(min, max, Mathf.Clamp01((e.mousePosition.x - track.x) / track.width))), min, max);
                e.Use();
            }
            else if (e.type == EventType.MouseUp) { _slider = 0; e.Use(); }
        }

        float t = Mathf.InverseLerp(min, max, entry.Value);
        float kx = track.x + track.width * t;
        NocturneStyle.FillRounded(track, p.Button, 4);
        NocturneStyle.FillRounded(new Rect(track.x, track.y, Mathf.Max(8f, track.width * t), track.height), p.Accent, 4);
        NocturneStyle.FillRounded(new Rect(kx - 9f, track.y + track.height / 2f - 9f, 18f, 18f), A(p.Accent, 0.35f), 9);
        NocturneStyle.FillRounded(new Rect(kx - 6f, track.y + track.height / 2f - 6f, 12f, 12f), Color.white, 6);
        y += 24f;
    }

    private int SliderIntVal(float x, ref float y, float w, string label, int value, int min, int max, string suffix, int key = 0)
    {
        NocturnePalette p = NocturneStyle.Current;
        value = Mathf.Clamp(value, min, max);
        GUI.Label(new Rect(x + 10f, y, w - 74f, 22f), label, _rowLabel);
        GUI.Label(new Rect(x + w - 58f, y, 54f, 22f), value + suffix, _value);
        y += 26f;

        const float arrowW = 24f;
        var leftBtn = new Rect(x + 8f, y - 3f, arrowW, 20f);
        var rightBtn = new Rect(x + w - arrowW - 8f, y - 3f, arrowW, 20f);
        if (ArrowButton(leftBtn, "◂")) value = Mathf.Clamp(value - 1, min, max);
        if (ArrowButton(rightBtn, "▸")) value = Mathf.Clamp(value + 1, min, max);

        var track = new Rect(leftBtn.xMax + 8f, y + 2f, rightBtn.x - leftBtn.xMax - 16f, 8f);
        int id = key != 0 ? key : label.GetHashCode();
        Event e = Event.current;
        var hit = new Rect(track.x, track.y - 10f, track.width, 28f);
        if (e != null && e.type == EventType.MouseDown && hit.Contains(e.mousePosition)) { _slider = id; e.Use(); }
        if (_slider == id && e != null)
        {
            if (e.type == EventType.MouseDrag || e.type == EventType.MouseDown)
            {
                value = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(min, max, Mathf.Clamp01((e.mousePosition.x - track.x) / track.width))), min, max);
                e.Use();
            }
            else if (e.type == EventType.MouseUp) { _slider = 0; e.Use(); }
        }

        float t = Mathf.InverseLerp(min, max, value);
        float kx = track.x + track.width * t;
        NocturneStyle.FillRounded(track, p.Button, 4);
        NocturneStyle.FillRounded(new Rect(track.x, track.y, Mathf.Max(8f, track.width * t), track.height), p.Accent, 4);
        NocturneStyle.FillRounded(new Rect(kx - 9f, track.y + track.height / 2f - 9f, 18f, 18f), A(p.Accent, 0.35f), 9);
        NocturneStyle.FillRounded(new Rect(kx - 6f, track.y + track.height / 2f - 6f, 12f, 12f), Color.white, 6);
        y += 24f;
        return value;
    }

    private float SliderFloatVal(float x, ref float y, float w, string label, float value, float min, float max, float step, string fmt, string suffix)
    {
        NocturnePalette p = NocturneStyle.Current;
        value = Mathf.Clamp(value, min, max);
        GUI.Label(new Rect(x + 10f, y, w - 74f, 22f), label, _rowLabel);
        GUI.Label(new Rect(x + w - 58f, y, 54f, 22f), value.ToString(fmt) + suffix, _value);
        y += 26f;

        const float arrowW = 24f;
        var leftBtn = new Rect(x + 8f, y - 3f, arrowW, 20f);
        var rightBtn = new Rect(x + w - arrowW - 8f, y - 3f, arrowW, 20f);
        if (ArrowButton(leftBtn, "◂")) value = Mathf.Clamp(value - step, min, max);
        if (ArrowButton(rightBtn, "▸")) value = Mathf.Clamp(value + step, min, max);

        var track = new Rect(leftBtn.xMax + 8f, y + 2f, rightBtn.x - leftBtn.xMax - 16f, 8f);
        int id = label.GetHashCode();
        Event e = Event.current;
        var hit = new Rect(track.x, track.y - 10f, track.width, 28f);
        if (e != null && e.type == EventType.MouseDown && hit.Contains(e.mousePosition)) { _slider = id; e.Use(); }
        if (_slider == id && e != null)
        {
            if (e.type == EventType.MouseDrag || e.type == EventType.MouseDown)
            {
                float raw = Mathf.Lerp(min, max, Mathf.Clamp01((e.mousePosition.x - track.x) / track.width));
                value = Mathf.Clamp(Mathf.Round(raw / step) * step, min, max);
                e.Use();
            }
            else if (e.type == EventType.MouseUp) { _slider = 0; e.Use(); }
        }

        float t = Mathf.InverseLerp(min, max, value);
        float kx = track.x + track.width * t;
        NocturneStyle.FillRounded(track, p.Button, 4);
        NocturneStyle.FillRounded(new Rect(track.x, track.y, Mathf.Max(8f, track.width * t), track.height), p.Accent, 4);
        NocturneStyle.FillRounded(new Rect(kx - 9f, track.y + track.height / 2f - 9f, 18f, 18f), A(p.Accent, 0.35f), 9);
        NocturneStyle.FillRounded(new Rect(kx - 6f, track.y + track.height / 2f - 6f, 12f, 12f), Color.white, 6);
        y += 24f;
        return value;
    }

    private bool ToggleVal(float x, ref float y, float w, string label, bool value)
    {
        NocturnePalette p = NocturneStyle.Current;
        var r = new Rect(x, y, w, RowH - 4f);
        HoverFill(r);
        bool clicked = GUI.Button(r, GUIContent.none, _invisible);
        GUI.Label(new Rect(r.x + 10f, r.y, r.width - 74f, r.height), label, _rowLabel);
        var pill = new Rect(r.xMax - 50f, r.y + (r.height - 24f) / 2f, 46f, 24f);
        NocturneStyle.FillRounded(pill, value ? A(p.Accent, 0.9f) : A(Color.white, 0.10f), 12);
        float kx = value ? pill.xMax - 22f : pill.x + 2f;
        NocturneStyle.FillRounded(new Rect(kx, pill.y + 2f, 20f, 20f), Color.white, 10);
        y += RowH;
        return clicked ? !value : value;
    }

    [HideFromIl2Cpp]
    private void FpsSlider(float x, ref float y, float w, string label, ConfigEntry<int> entry)
    {
        if (entry == null) { y += RowH; return; }
        const int min = 30, max = 300, step = 5;
        NocturnePalette p = NocturneStyle.Current;
        GUI.Label(new Rect(x + 10f, y, w - 74f, 22f), label, _rowLabel);
        GUI.Label(new Rect(x + w - 58f, y, 54f, 22f), entry.Value >= max ? "∞" : entry.Value.ToString(), _value);
        y += 26f;

        const float arrowW = 24f;
        var leftBtn = new Rect(x + 8f, y - 3f, arrowW, 20f);
        var rightBtn = new Rect(x + w - arrowW - 8f, y - 3f, arrowW, 20f);
        if (ArrowButton(leftBtn, "◂")) entry.Value = Mathf.Clamp(entry.Value - step, min, max);
        if (ArrowButton(rightBtn, "▸")) entry.Value = Mathf.Clamp(entry.Value + step, min, max);

        var track = new Rect(leftBtn.xMax + 8f, y + 2f, rightBtn.x - leftBtn.xMax - 16f, 8f);
        int id = label.GetHashCode();
        Event e = Event.current;
        var hit = new Rect(track.x, track.y - 10f, track.width, 28f);
        if (e != null && e.type == EventType.MouseDown && hit.Contains(e.mousePosition)) { _slider = id; e.Use(); }
        if (_slider == id && e != null)
        {
            if (e.type == EventType.MouseDrag || e.type == EventType.MouseDown)
            {
                int raw = Mathf.RoundToInt(Mathf.Lerp(min, max, Mathf.Clamp01((e.mousePosition.x - track.x) / track.width)));
                entry.Value = Mathf.Clamp(Mathf.RoundToInt(raw / (float)step) * step, min, max);
                e.Use();
            }
            else if (e.type == EventType.MouseUp) { _slider = 0; e.Use(); }
        }

        float t = Mathf.InverseLerp(min, max, entry.Value);
        float kx = track.x + track.width * t;
        NocturneStyle.FillRounded(track, p.Button, 4);
        NocturneStyle.FillRounded(new Rect(track.x, track.y, Mathf.Max(8f, track.width * t), track.height), p.Accent, 4);
        NocturneStyle.FillRounded(new Rect(kx - 9f, track.y + track.height / 2f - 9f, 18f, 18f), A(p.Accent, 0.35f), 9);
        NocturneStyle.FillRounded(new Rect(kx - 6f, track.y + track.height / 2f - 6f, 12f, 12f), Color.white, 6);
        y += 24f;
    }

    private bool ArrowButton(Rect r, string glyph)
    {
        NocturnePalette p = NocturneStyle.Current;
        bool hover = r.Contains(M);
        NocturneStyle.FillRounded(r, hover ? A(p.Accent, 0.22f) : A(Color.white, 0.05f), 6);
        GUI.Label(r, glyph, _arrow);
        return GUI.Button(r, GUIContent.none, _invisible);
    }

    private const float SwW = 28f, SwH = 22f, SwGap = 6f;

    private static int ThemeCols(float w) => Mathf.Max(1, Mathf.FloorToInt((w + SwGap) / (SwW + SwGap)));

    private static float ThemeGridHeight(float w) =>
        Mathf.CeilToInt((float)NocturneStyle.ThemeCount / ThemeCols(w)) * (SwH + SwGap) - SwGap;

    private void ThemeSwatches(float x, float y, float w)
    {
        int n = NocturneStyle.ThemeCount;
        int cols = ThemeCols(w);
        int cur = NocturneConfig.ThemeIndex.Value;
        for (int i = 0; i < n; i++)
        {
            var r = new Rect(x + (i % cols) * (SwW + SwGap), y + (i / cols) * (SwH + SwGap), SwW, SwH);
            NocturneStyle.FillRounded(r, NocturneStyle.ThemeAt(i).Accent, 6);
            NocturneStyle.StrokeRounded(r, i == cur ? Color.white : A(Color.black, 0.30f), 6, i == cur ? 2 : 1);
            if (GUI.Button(r, GUIContent.none, _invisible)) NocturneConfig.ThemeIndex.Value = i;
        }
    }

    private void DrawSearchBar(Rect r)
    {
        string prev = _search;
        _search = CustomText(r, _search ?? "", "nocturneSearch");
        if (_search != prev) { _scroll = 0f; _scrollPending = 0f; }
        if (string.IsNullOrEmpty(_search) && _textFocus != "nocturneSearch")
            GUI.Label(new Rect(r.x + 8f, r.y, r.width - 16f, r.height), NocturneText.T("Поиск фич…", "Search features…"), _muted);
        if (!string.IsNullOrEmpty(_search))
        {
            var clr = new Rect(r.xMax - 24f, r.y, 22f, r.height);
            if (GUI.Button(clr, GUIContent.none, _invisible)) { _search = ""; _textFocus = null; }
            GUI.Label(clr, "×", _star);
        }
    }

    private void DrawCollapseAll(Rect r)
    {
        if (_knownCards.Count == 0) return;
        int col = 0;
        foreach (string t in _knownCards) if (_cardCollapsed.TryGetValue(t, out bool c) && c) col++;
        bool mostCollapsed = col * 2 >= _knownCards.Count;

        bool clicked;
        if (NocturneStyle.Lite)
            clicked = GUI.Button(r, mostCollapsed ? "+" : "−");
        else
        {
            NocturnePalette p = NocturneStyle.Current;
            bool hover = r.Contains(M);
            NocturneStyle.FillRounded(r, hover ? A(p.Accent, 0.18f) : A(Color.white, 0.05f), 8);
            NocturneStyle.StrokeRounded(r, hover ? A(p.Accent, 0.30f) : A(Color.white, 0.10f), 8, 1);
            GUI.Label(r, mostCollapsed ? "⊞" : "⊟", _centerMuted);
            clicked = GUI.Button(r, GUIContent.none, _invisible);
        }
        if (clicked)
        {
            bool target = !mostCollapsed;
            foreach (string t in _knownCards) _cardCollapsed[t] = target;
        }
    }

    private void DrawSearch(float x, ref float y, float w)
    {
        if (_search != _searchDone)
        {
            _searchDone = _search;
            string q = _search.Trim().ToLowerInvariant();
            _searchHits.Clear();
            foreach (QuickItem it in NocturneQuick.Items)
                if (it.Ru.ToLowerInvariant().Contains(q) || it.En.ToLowerInvariant().Contains(q))
                    _searchHits.Add(it);
        }

        int n = _searchHits.Count;
        Rect b = Card(x, ref y, w, NocturneText.T("Поиск", "Search") + "  (" + n + ")", n > 0 ? n * RowH + 6f : 30f);
        float by = b.y;
        if (n == 0)
        {
            GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Ничего не найдено.", "Nothing found."), _muted);
            return;
        }
        for (int i = 0; i < n; i++) QuickRow(b.x, ref by, b.width, _searchHits[i]);
    }

    [HideFromIl2Cpp]
    private void QuickRow(float x, ref float y, float w, QuickItem it)
    {
        if (Cull(y, RowH)) { y += RowH; return; }
        var r = new Rect(x, y, w, RowH - 2f);
        HoverFill(r);
        bool fav = NocturneQuick.IsFav(it.Id);
        var star = new Rect(r.x + 6f, r.y, 26f, r.height);
        if (GUI.Button(star, GUIContent.none, _invisible)) NocturneQuick.ToggleFav(it.Id);
        _star.normal.textColor = fav ? NocturneStyle.Current.Accent : A(NocturneStyle.Current.Muted, 0.7f);
        GUI.Label(star, fav ? "★" : "☆", _star);
        GUI.Label(new Rect(r.x + 36f, r.y, r.width - 92f, r.height), it.Label, _rowLabel);
        var pill = new Rect(r.xMax - 50f, r.y + (r.height - 24f) / 2f, 46f, 24f);
        if (GUI.Button(pill, GUIContent.none, _invisible)) it.Cfg.Value = !it.Cfg.Value;
        DrawPill(pill, it.Cfg.Value, it.Cfg);
        y += RowH;
    }

    [HideFromIl2Cpp]
    private void CycleRow(float x, ref float y, float w, string label, string valueText, Action onClick)
    {
        if (Cull(y, RowH)) { y += RowH; return; }
        if (NocturneStyle.Lite)
        {
            if (GUI.Button(new Rect(x + 4f, y, w - 8f, RowH - 4f), label + ":  " + valueText)) onClick();
            y += RowH;
            return;
        }
        var r = new Rect(x, y, w, RowH - 4f);
        HoverFill(r);
        if (GUI.Button(r, GUIContent.none, _invisible)) onClick();
        GUI.Label(new Rect(r.x + 12f, r.y, r.width * 0.45f, r.height), label, _rowLabel);
        GUI.Label(new Rect(r.x + r.width - 168f, r.y, 164f, r.height), valueText + "   ▸", _value);
        y += RowH;
    }

    [HideFromIl2Cpp]
    private void KeyRow(float x, ref float y, float w, string label, ConfigEntry<KeyCode> entry)
    {
        if (entry == null) { y += RowH; return; }
        string fk = FavKey(entry);
        var kit = FavReg(fk, FavKind.Key); if (kit != null) { kit.Label = label; kit.K = entry; }
        if (Cull(y, RowH)) { y += RowH; return; }
        FavRow(new Rect(x, y, w, RowH - 4f), fk);
        NocturnePalette p = NocturneStyle.Current;
        var r = new Rect(x, y, w, RowH - 4f);
        HoverFill(r);
        GUI.Label(new Rect(r.x + 12f, r.y, r.width * 0.5f, r.height), label, _rowLabel);

        bool listening = _rebinding && ReferenceEquals(_rebindTarget, entry);
        float bw = listening ? 132f : 96f;
        var badge = new Rect(r.xMax - bw - 4f, r.y + (r.height - 24f) / 2f, bw, 24f);
        NocturneStyle.FillRounded(badge, listening ? A(p.Accent, 0.85f) : A(Color.white, 0.06f), 7);
        NocturneStyle.StrokeRounded(badge, listening ? A(p.Accent, 0.9f) : A(Color.white, 0.10f), 7, 1);
        _keyBadge.normal.textColor = listening ? Color.white : p.Text;
        string txt = listening ? NocturneText.T("Нажми клавишу…", "Press a key…") : (entry.Value == KeyCode.None ? "—" : KeyStr(entry.Value));
        GUI.Label(badge, txt, _keyBadge);

        Event e = Event.current;

        if (!listening && entry.Value != KeyCode.None && entry != NocturneConfig.MenuKey)
        {
            var del = new Rect(badge.x - 26f, badge.y, 22f, 24f);
            bool dh = e != null && del.Contains(e.mousePosition);
            NocturneStyle.FillRounded(del, dh ? A(new Color(0.9f, 0.3f, 0.3f), 0.30f) : A(Color.white, 0.06f), 6);
            NocturneIcons.Draw(NocturneIcon.Trash, new Rect(del.x + 5f, del.y + 5f, 14f, 14f), dh ? new Color(0.95f, 0.5f, 0.5f) : A(p.Muted, 0.9f));
            if (GUI.Button(del, GUIContent.none, _invisible))
            {
                entry.Value = KeyCode.None;
                _rebinding = false; _rebindTarget = null; Rebinding = false;
            }
        }

        bool rightClear = e != null && e.type == EventType.MouseDown && e.button == 1 && badge.Contains(e.mousePosition) && entry != NocturneConfig.MenuKey;
        bool clicked = GUI.Button(badge, GUIContent.none, _invisible);
        if (rightClear)
        {
            entry.Value = KeyCode.None;
            _rebinding = false; _rebindTarget = null; Rebinding = false;
            e.Use();
        }
        else if (clicked)
        {
            if (listening) { _rebinding = false; _rebindTarget = null; }
            else { _rebinding = true; _rebindTarget = entry; }
            Rebinding = _rebinding;
        }
        y += RowH;
    }

    private void HandleRebindCapture()
    {
        Rebinding = _rebinding;
        if (!_rebinding || _rebindTarget == null) return;
        Event e = Event.current;
        if (e == null || e.type != EventType.KeyDown) return;
        KeyCode kc = e.keyCode;
        if (kc == KeyCode.Escape) { _rebinding = false; _rebindTarget = null; }
        else if (kc != KeyCode.None) { _rebindTarget.Value = kc; _rebinding = false; _rebindTarget = null; }
        Rebinding = _rebinding;
        e.Use();
    }

    [HideFromIl2Cpp]
    private void DrawPill(Rect track, bool on, object key)
    {
        NocturnePalette p = NocturneStyle.Current;
        float target = on ? 1f : 0f;
        float a = target, v = 0f;
        if (key != null)
        {
            if (!_pillAnim.TryGetValue(key, out a)) a = target;
            _pillVel.TryGetValue(key, out v);
            if (Event.current != null && Event.current.type == EventType.Repaint)
            {
                float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
                v += (-300f * (a - target) - 24f * v) * dt;
                a += v * dt;
                _pillAnim[key] = a;
                _pillVel[key] = v;
            }
        }
        float e = Mathf.Clamp01(a);
        float kpos = Mathf.Clamp(a, -0.12f, 1.12f);
        int r = Mathf.RoundToInt(track.height / 2f);

        if (e > 0.01f)
            NocturneStyle.FillRounded(new Rect(track.x - 2f, track.y - 2f, track.width + 4f, track.height + 4f), A(p.Accent, 0.30f * e), r + 2);
        NocturneStyle.FillRounded(track, Color.Lerp(p.Button, p.Accent, e), r);
        if (e < 0.99f)
            NocturneStyle.StrokeRounded(track, A(Color.white, 0.06f * (1f - e)), r, 1);
        if (e > 0.01f)
        {
            NocturneStyle.Fill(new Rect(track.x + 5f, track.y + 2f, track.width - 10f, 1f), A(Color.white, 0.30f * e));
            NocturneStyle.Fill(new Rect(track.x + 5f, track.yMax - 3f, track.width - 10f, 1f), A(Color.black, 0.16f * e));
        }

        float knob = track.height - 6f;
        float kx = Mathf.Lerp(track.x + 3f, track.xMax - knob - 3f, kpos);
        int kr = Mathf.RoundToInt(knob / 2f);
        NocturneStyle.FillRounded(new Rect(kx, track.y + 5f, knob, knob), A(Color.black, 0.34f), kr);
        NocturneStyle.FillRounded(new Rect(kx, track.y + 3f, knob, knob), Color.Lerp(p.Muted, Color.white, e), kr);
    }

    private static float Frac(float v) => v - Mathf.Floor(v);

    private void DrawStars(float w, float h)
    {
        if (Event.current == null || Event.current.type != EventType.Repaint) return;
        float t = Time.unscaledTime;
        for (int i = 0; i < 26; i++)
        {
            float fx = Frac(i * 0.61803399f + 0.11f);
            float fy = Frac(i * 0.75487767f + 0.29f);
            float drift = Frac(fy + 0.02f * t * (0.4f + fx));
            float sz = 1.3f + 1.7f * fy;
            float tw = 0.5f + 0.5f * Mathf.Sin(t * (0.7f + fx) + i);
            Color col = (i % 5 == 0) ? NocturneStyle.Current.Accent : Color.white;
            NocturneStyle.FillRounded(new Rect(fx * w, drift * h, sz, sz), A(col, 0.05f + 0.06f * tw), 1);
        }
    }

    private void HoverFill(Rect r)
    {
        if (Event.current == null || Event.current.type != EventType.Repaint) return;
        if (!r.Contains(Event.current.mousePosition)) return;
        NocturnePalette p = NocturneStyle.Current;
        NocturneStyle.FillRounded(r, A(p.Accent, 0.06f), 8);
        NocturneStyle.FillRounded(new Rect(r.x, r.y, r.width * 0.42f, r.height), A(p.Accent, 0.05f), 8);
        NocturneStyle.FillRounded(new Rect(r.x + 1f, r.y + 4f, 3f, r.height - 8f), p.Accent, 2);
    }

    private string CustomText(Rect r, string text, string key)
    {
        NocturnePalette p = NocturneStyle.Current;
        bool focused = _textFocus == key;
        NocturneStyle.FillRounded(r, p.Button, 7);
        NocturneStyle.StrokeRounded(r, focused ? A(p.Accent, 0.8f) : A(Color.white, 0.08f), 7, 1);

        Event e = Event.current;
        if (e != null && e.type == EventType.MouseDown)
            _textFocus = r.Contains(e.mousePosition) ? key : (_textFocus == key ? null : _textFocus);

        if (focused && e != null && e.type == EventType.KeyDown)
        {
            if (e.keyCode == KeyCode.Backspace) { if (text.Length > 0) text = text.Substring(0, text.Length - 1); e.Use(); }
            else if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) { _textFocus = null; e.Use(); }
            else if (e.character != '\0' && !char.IsControl(e.character) && text.Length < 24) { text += e.character; e.Use(); }
        }

        bool caret = focused && Time.unscaledTime % 1f < 0.5f;
        GUI.Label(new Rect(r.x + 8f, r.y, r.width - 16f, r.height), text + (caret ? "|" : ""), _rowLabel);
        return text;
    }

    private static void OpenLink(string url)
    {
        try { GUIUtility.systemCopyBuffer = url; } catch { }
        try { Application.OpenURL(url); } catch { }
        NocturneToast.Push(NocturneText.T("Ссылка", "Link"), NocturneText.T("Открыта в браузере · скопирована", "Opened in browser · copied"), 2.5f, NocturneNotifyKind.Info);
    }

    private static readonly Dictionary<KeyCode, string> _keyStr = new Dictionary<KeyCode, string>();
    private static string KeyStr(KeyCode k)
    {
        if (!_keyStr.TryGetValue(k, out string s)) { s = k.ToString(); _keyStr[k] = s; }
        return s;
    }
    private static string KeyName(ConfigEntry<KeyCode> key) => key != null ? KeyStr(key.Value) : "?";
    private static string KeyDisp(ConfigEntry<KeyCode> key) => key == null || key.Value == KeyCode.None ? "—" : KeyStr(key.Value);
    private static Color A(Color c, float a) => new Color(c.r, c.g, c.b, a);

    private static Rect LerpRect(Rect a, Rect b, float t) =>
        new Rect(Mathf.Lerp(a.x, b.x, t), Mathf.Lerp(a.y, b.y, t), Mathf.Lerp(a.width, b.width, t), Mathf.Lerp(a.height, b.height, t));

    private static string Hex(Color c)
    {
        Color32 c32 = c;
        return c32.r.ToString("X2") + c32.g.ToString("X2") + c32.b.ToString("X2");
    }

    private void Build()
    {
        int ti = NocturneConfig.ThemeIndex.Value;
        if (_built && ti == _themeBuilt) return;
        _built = true;
        _themeBuilt = ti;
        NocturnePalette p = NocturneStyle.Current;

        _invisible = new GUIStyle();
        _gradient = NocturneStyle.BuildGradient((int)_window.width, (int)FullH, Rgb(30, 30, 32), Rgb(14, 14, 15), 16);
        _gloss = NocturneStyle.BuildVFade(96, Color.white, 0.05f, 0f);

        _brand = Label(p.Text, 25, FontStyle.Bold, TextAnchor.MiddleLeft);
        _verPill = Label(p.Accent, 12, FontStyle.Bold, TextAnchor.MiddleCenter);
        _status = Label(p.Text, 11, FontStyle.Bold, TextAnchor.MiddleLeft);
        _statVal = Label(p.Text, 12, FontStyle.Bold, TextAnchor.MiddleLeft);
        _secTitle = Label(p.Text, 15, FontStyle.Bold, TextAnchor.MiddleLeft);
        _favStar = Label(p.Accent, 11, FontStyle.Bold, TextAnchor.MiddleCenter);
        _cardTitle = Label(A(p.Text, 0.82f), 11, FontStyle.Bold, TextAnchor.MiddleLeft);
        _rowLabel = Label(p.Text, 15, FontStyle.Normal, TextAnchor.MiddleLeft);
        _rowLabel.wordWrap = true;
        _value = Label(p.Accent, 13, FontStyle.Bold, TextAnchor.MiddleRight);
        _valueC = Label(p.Accent, 13, FontStyle.Bold, TextAnchor.MiddleCenter);
        _muted = Label(p.Muted, 13, FontStyle.Normal, TextAnchor.MiddleLeft);
        _footer = Label(p.Muted, 12, FontStyle.Normal, TextAnchor.MiddleLeft);
        _btnLabel = Label(p.Accent, 13, FontStyle.Bold, TextAnchor.MiddleCenter);
        _centerMuted = Label(A(p.Muted, 0.8f), 14, FontStyle.Normal, TextAnchor.MiddleCenter);
        _arrow = Label(p.Accent, 15, FontStyle.Bold, TextAnchor.MiddleCenter);
        _smallBtn = Label(Color.white, 11, FontStyle.Bold, TextAnchor.MiddleCenter);
        _rowName = Label(p.Text, 14, FontStyle.Normal, TextAnchor.MiddleLeft);
        _rowName.wordWrap = false;
        _rowName.clipping = TextClipping.Clip;
        _rowInfo = Label(p.Muted, 13, FontStyle.Normal, TextAnchor.MiddleLeft);
        _rowInfo.wordWrap = false;
        _rowInfo.clipping = TextClipping.Clip;
        _tabStyle = Label(p.Muted, 14, FontStyle.Bold, TextAnchor.MiddleLeft);
        _keyBadge = Label(p.Text, 12, FontStyle.Bold, TextAnchor.MiddleCenter);
        _star = Label(p.Accent, 17, FontStyle.Normal, TextAnchor.MiddleCenter);
    }

    private static GUIStyle Label(Color color, int size, FontStyle style, TextAnchor anchor)
    {
        var s = new GUIStyle(GUI.skin.label)
        {
            fontSize = size,
            fontStyle = style,
            alignment = anchor,
            richText = true
        };
        s.normal.textColor = color;
        s.padding = NocturneStyle.Offset(0, 0, 0, 0);
        return s;
    }

    private static Color Rgb(byte r, byte g, byte b) => new Color(r / 255f, g / 255f, b / 255f, 1f);
}
