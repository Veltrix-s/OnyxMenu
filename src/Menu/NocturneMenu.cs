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
    private const float MinW = 720f;
    private const float HeaderH = 44f;
    private const float SidebarW = 200f;
    private const float TabH = 44f;
    private const float RowH = 34f;
    private const float FooterH = 12f;

    private struct TabDef
    {
        public NocturneIcon Icon;
        public string Ru;
        public string En;
        public TabDef(NocturneIcon icon, string ru, string en)
        {
            Icon = icon;
            Ru = ru;
            En = en;
        }
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

    private static readonly string[] MapsShort = { "Skeld", "Mira HQ", "Polus", "Dleks", "Airship", "Fungle" };
    private static readonly string[] MapsFull = { "The Skeld", "MIRA HQ", "Polus", "dlekS", "Airship", "Fungle" };
    private static readonly string[] MapsAlt = { "The Skeld", "Mira HQ", "Polus", "dlekS", "Airship", "The Fungle" };
    private static readonly string[] BmVals = { "Disabled", "Horse", "Seeker", "Long", "LongHorse" };

    private static readonly string[] VentModesRu = { "Все", "Только мирные", "Только преды", "Никто" };
    private static readonly string[] VentModesEn = { "Everyone", "Crew only", "Impostors only", "Nobody" };

    private static readonly string[] DistsRu = { "Короткая", "Средняя", "Длинная" };
    private static readonly string[] DistsEn = { "Short", "Medium", "Long" };

    private static readonly string[] TaskBarsRu = { "Постоянно", "Только собрания", "Скрыто" };
    private static readonly string[] TaskBarsEn = { "Always", "Meetings only", "Hidden" };

    private static readonly string[] WeatherRu = { "Выкл", "Снег", "Дождь", "Листья", "Конфетти" };
    private static readonly string[] WeatherEn = { "Off", "Snow", "Rain", "Leaves", "Confetti" };

    private static readonly string[] OcWhereRu = { "Лобби и матч", "Только матч", "Только лобби" };
    private static readonly string[] OcWhereEn = { "Lobby and match", "Match only", "Lobby only" };

    private static readonly string[] NoModesRu = { "Радуга", "По роли", "Цвет игрока" };
    private static readonly string[] NoModesEn = { "Rainbow", "By role", "Player color" };

    private static readonly string[] BmDispRu = { "Выкл", "Лошадь", "Сикер", "Длинный", "Длинная лошадь" };
    private static readonly string[] BmDispEn = { "Off", "Horse", "Seeker", "Long", "Long horse" };

    private static readonly string[] FrameSysRu = { "Sabotage (надёжно)", "Reactor (16 или 128)", "Электрика (>5)" };
    private static readonly string[] FrameSysEn = { "Sabotage (reliable)", "Reactor (16 or 128)", "Electrical (>5)" };
    private static readonly SystemTypes[] FrameSysVals = { SystemTypes.Sabotage, SystemTypes.Reactor, SystemTypes.Electrical };

    private static readonly string[] FormNamesRu =
    {
        "Линия", "Круг", "Треугольник", "Звезда", "Сердце", "Ромб", "Спираль", "Крест", "Волна", "Дракон",
        "Персонаж", "Бесконечность", "Стрела", "Корона", "Молния", "Цветок", "Полумесяц", "Клевер", "Ёлка",
        "Квадрат", "Смайл", "Бабочка", "Солнце", "Пятиугольник", "Икс", "Сетка", "Пакман", "Атом", "Нота",
        "Знак вопроса", "Пульс"
    };

    private static readonly string[] FormNamesEn =
    {
        "Line", "Circle", "Triangle", "Star", "Heart", "Diamond", "Spiral", "Cross", "Wave", "Dragon",
        "Character", "Infinity", "Arrow", "Crown", "Lightning", "Flower", "Crescent", "Clover", "Tree",
        "Square", "Smiley", "Butterfly", "Sun", "Pentagon", "X", "Grid", "Pacman", "Atom", "Note",
        "Question", "Heartbeat"
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
    private bool _subPinned;
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
    private GUIStyle _gradient;
    private Texture2D _gloss;
    private GUIStyle _brand;
    private GUIStyle _verPill;
    private GUIStyle _status;
    private GUIStyle _secTitle;
    private GUIStyle _railGroup;
    private GUIStyle _railLabel;
    private GUIStyle _favStar;
    private GUIStyle _tabStyle;
    private GUIStyle _cardTitle;
    private GUIStyle _rowLabel;
    private GUIStyle _wrapLabel;
    private GUIStyle _value;
    private GUIStyle _valueC;
    private GUIStyle _valueClip;
    private GUIStyle _muted;
    private GUIStyle _mutedClip;
    private GUIStyle _searchHint;
    private GUIStyle _searchText;
    private GUIStyle _footer;
    private GUIStyle _btnLabel;
    private GUIStyle _centerMuted;
    private GUIStyle _arrow;
    private GUIStyle _smallBtn;
    private GUIStyle _cardVal;
    private GUIStyle _tabOn;
    private GUIStyle _tabOff;
    private GUIStyle _rowName;
    private GUIStyle _rowSel;
    private GUIStyle _rowInfo;
    private GUIStyle _invisible;
    private GUIStyle _keyBadge;
    private GUIStyle _star;

    private readonly List<ClientData> _guardClients = new List<ClientData>();
    private readonly List<PlayerControl> _roleClients = new List<PlayerControl>();
    private readonly List<PlayerControl> _immPlayers = new List<PlayerControl>();
    private readonly List<PlayerControl> _shieldPlayers = new List<PlayerControl>();
    private readonly List<QuickItem> _searchHits = new List<QuickItem>();
    private readonly List<SearchCard> _searchCards = new List<SearchCard>();
    private readonly List<FavItem> _searchFeat = new List<FavItem>();
    private int _pendJumpTab = -1;
    private string _pendJumpTitle;
    private string _flashKey;
    private float _flashUntil;
    private float _nextIndexFlush;
    private int _outfitPickSlot = -1;
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
        => byte.TryParse((idText ?? "").Trim(), out byte pid) ? Utils.ById(pid) : null;

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
    private int _crystalAccent = int.MinValue;
    private GUIStyle _crystalStyle;
    private float _pressAt;

    private int _tabDir = 1;
    private float _tabAnimAt = -1f;
    private float _railSelOff = -1f;
    private int _railSelFrame = -1;
    private readonly int[] _subTab = new int[12];
    private int _cardGrp = -1;
    private readonly Dictionary<object, float> _pillAnim = new Dictionary<object, float>();
    private readonly Dictionary<object, float> _pillVel = new Dictionary<object, float>();

    private static Vector2 M => NocturneStyle.Mouse;

    private static int _mobile = -1;
    private static bool Mobile
    {
        get
        {
            if (_mobile < 0)
            {
                _mobile = Application.isMobilePlatform ? 1 : 0;
            }
            return _mobile == 1;
        }
    }

    public void Update()
    {
        float t = Time.unscaledTime;
        if (t > _nextIndexFlush)
        {
            _nextIndexFlush = t + 2f;
            NocturneSearchIndex.Flush();
        }

        bool menuKey = NocturneKeys.Down(NocturneConfig.MenuKey);
        bool fallbackKey = Input.GetKeyDown(KeyCode.Delete) && !Typing && !NocturneChatWindow.Typing && !Patches.NocturneTextFocus.Any;
        if (!_rebinding && (menuKey || fallbackKey))
        {
            if (_open && _closeAt < 0f)
                RequestClose();
            else
                Open();
        }
        if (ToggleRequest)
        {
            ToggleRequest = false;
            if (!_rebinding)
            {
                if (_open && _closeAt < 0f)
                    RequestClose();
                else
                    Open();
            }
        }
        if (!_rebinding && _open && _closeAt < 0f && Input.GetKeyDown(KeyCode.Escape))
        {
            if (!string.IsNullOrEmpty(_search))
            {
                _search = string.Empty;
                _scroll = 0f;
                _scrollPending = 0f;
            }
            else
                RequestClose();
        }
        Opened = _open;

        bool typing = _open && _closeAt < 0f && _textFocus != null;
        Typing = typing;
        if (typing)
            SetMoveable(false);
        else if (_wasTyping)
            SetMoveable(true);
        _wasTyping = typing;
    }

    private void Open()
    {
        _open = true;
        _openAt = Time.unscaledTime;
        _closeAt = -1f;
        _mobileCentered = false;
        try
        {
            Input.simulateMouseWithTouches = true;
        }
        catch { }
    }

    private void RequestClose()
    {
        if (_open && _closeAt < 0f)
            _closeAt = Time.unscaledTime;
    }

    private void SetTab(int nt)
    {
        if (nt == _tab)
            return;
        _tabDir = nt > _tab ? 1 : -1;
        _tab = nt;
        _tabAnimAt = Time.unscaledTime;
        _scroll = 0f;
        _scrollPending = 0f;
    }

    private static float SmoothSat(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    private static int RectId(Rect r) => (Mathf.RoundToInt(r.x) * 73856093) ^ (Mathf.RoundToInt(r.y) * 19349663) ^ (Mathf.RoundToInt(r.width) * 83492791);

    private float PressK(int id)
    {
        if (_press != id)
            return 1f;
        float t = (NocturneStyle.Now - _pressAt) / 0.13f;
        if (t >= 1f)
        {
            _press = 0;
            return 1f;
        }
        return Mathf.Lerp(0.97f, 1f, SmoothSat(t));
    }

    private void Pressed(Rect r, int id)
    {
        Event e = NocturneStyle.Ev;
        if (e != null && e.type == EventType.MouseDown && r.Contains(e.mousePosition))
        {
            _press = id;
            _pressAt = Time.unscaledTime;
        }
    }

    private static void SetMoveable(bool value)
    {
        if (PlayerControl.LocalPlayer != null)
            PlayerControl.LocalPlayer.moveable = value;
    }

    internal void DrawGui()
    {
        if (!_open) return;

        Build();
        RefreshAccentStyles(NocturneStyle.Current.Accent);
        HandleRebindCapture();

        float baseS = Mobile
            ? Mathf.Clamp(Screen.height / 850f, 0.8f, 2.4f)
            : Mathf.Clamp(Screen.height / 1080f, 0.72f, 2.1f);
        float s = Mathf.Clamp(baseS * NocturneConfig.MenuScale.Value, 0.5f, 3.2f);
        if (!Mobile)
            s = Mathf.Min(s, Mathf.Min(Screen.width / (MinW + 24f), Screen.height / (FullH + 24f)));
        Matrix4x4 prevMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
        float vw = Screen.width / s;
        float vh = Screen.height / s;

        float now = NocturneStyle.Now;
        float fade = 1f;
        if (_closeAt >= 0f)
        {
            float t = Mathf.Clamp01((now - _closeAt) / 0.2f);
            fade = 1f - SmoothSat(t);
            if (t >= 1f)
            {
                _open = false;
                _closeAt = -1f;
                _openAt = -1f;
                _collapsed = false;
                GUI.matrix = prevMatrix;
                return;
            }
        }
        else if (_openAt >= 0f)
        {
            float t = Mathf.Clamp01((now - _openAt) / 0.26f);
            fade = SmoothSat(t);
            if (t >= 1f)
                _openAt = -1f;
        }
        _fade = fade;

        if (!(NocturneConfig.LiteMenu != null && NocturneConfig.LiteMenu.Value))
            NocturneStyle.Fill(new Rect(0f, 0f, vw, vh), new Color(0.02f, 0.02f, 0.022f, 0.34f * fade));

        if (Mobile)
            CenterWindow(vw, vh);
        else
        {
            ClampWindow(vw, vh);
            if (fade >= 1f)
                HandleResize(vw, vh);
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
        Event e = NocturneStyle.Ev;
        if (e == null || _resize != 0)
            return;
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
        Event e = NocturneStyle.Ev;
        if (_collapsed || e == null)
            return;

        if (e.type == EventType.MouseDown && e.button == 0)
        {
            Vector2 m = e.mousePosition;
            bool inX = m.x >= _window.x - 8f && m.x <= _window.xMax + 8f;
            bool inY = m.y >= _window.y - 8f && m.y <= _window.yMax + 8f;
            int dir = 0;
            if (inY && Mathf.Abs(m.x - _window.x) <= 8f)
                dir |= RzL;
            if (inY && Mathf.Abs(m.x - _window.xMax) <= 8f)
                dir |= RzR;
            if (inX && Mathf.Abs(m.y - _window.y) <= 8f)
                dir |= RzT;
            if (inX && Mathf.Abs(m.y - _window.yMax) <= 8f)
                dir |= RzB;
            if (dir != 0)
            {
                _resize = dir;
                e.Use();
            }
        }
        else if (_resize != 0 && e.type == EventType.MouseDrag)
        {
            Vector2 m = e.mousePosition;
            if ((_resize & RzR) != 0)
                _window.width = Mathf.Clamp(m.x - _window.x, MinW, Mathf.Max(MinW, vw - _window.x));
            if ((_resize & RzB) != 0)
                _h = Mathf.Clamp(m.y - _window.y, FullH, Mathf.Max(FullH, vh - _window.y));
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
        float w = Mathf.Clamp(840f, MinW, vw);
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
        if (lite)
        {
            GUI.skin.button.fontSize = 12;
            GUI.skin.toggle.fontSize = GUI.skin.label.fontSize = 13;
        }
        float w = _window.width;
        float h = _window.height;
        float op = NocturneConfig.MenuOpacity != null ? Mathf.Clamp(NocturneConfig.MenuOpacity.Value, 0.45f, 1f) : 1f;
        GUI.color = new Color(1f, 1f, 1f, _fade * op);

        if (lite || _collapsed)
            NocturneStyle.FillRounded(new Rect(0f, 0f, w, h), lite ? Rgb(20, 21, 25) : p.Window, 16);
        else
            GUI.Box(new Rect(0f, 0f, w, h), GUIContent.none, _gradient);
        NocturneMenuBg.Draw(new Rect(0f, 0f, w, h));
        if (!lite && !NocturneMenuBg.On)
            NocturneStyle.FillRounded(new Rect(0f, 0f, w, h), A(p.Accent, 0.04f), 16);
        NocturneStyle.StrokeRounded(new Rect(0f, 0f, w, h), lite ? A(Color.white, 0.20f) : A(p.Accent, 0.22f), 16, 1);
        if (!_collapsed && !lite)
            NocturneStyle.DrawTex(new Rect(2f, 2f, w - 4f, HeaderH - 3f), _gloss);

        DrawHeader(w, p);
        if (_collapsed)
            return;

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
                if (_tab == i)
                    GUI.backgroundColor = new Color(0.5f, 0.5f, 0.5f);
                if (GUI.Button(new Rect(8f, ty, sideW - 12f, 28f), nm) && _tab != i)
                {
                    _tab = i;
                    _scroll = 0f;
                    _scrollPending = 0f;
                }
                GUI.backgroundColor = bg;
                ty += 30f;
            }
            area = new Rect(sideW + 8f, colTop + 32f, w - sideW - 8f - 16f, h - (colTop + 32f) - FooterH);
        }
        else
        {
            const float railW = 150f;
            float colTop = HeaderH + 8f;
            float railBottom = h - FooterH;

            DrawSearchBar(new Rect(8f, colTop, railW - 16f, 26f));
            DrawRail(new Rect(0f, colTop + 34f, railW, railBottom - colTop - 34f));
            NocturneStyle.Fill(new Rect(railW, HeaderH, 1f, h - HeaderH - 3f), A(Color.white, 0.08f));

            float cxL = railW + 12f;
            if (NocturneStyle.Painting)
            {
                string secName = _tab == FavTab ? NocturneText.T("Избранное", "Favorites") : _tab < Tabs.Length ? Tabs[_tab].Name : NocturneText.T("Настройки", "Settings");
                _secTitle.normal.textColor = p.Text;
                Lab(new Rect(cxL, colTop - 1f, w - cxL - 52f, 28f), Up(secName), _secTitle);
            }
            DrawCollapseAll(new Rect(w - 16f - 30f, colTop, 30f, 26f));
            area = new Rect(cxL, colTop + 28f, w - cxL - 16f, h - colTop - 28f - FooterH);
        }
        if (NocturneStyle.Painting)
        {
            _scroll = Mathf.Clamp(_scroll + _scrollPending, 0f, _maxScroll);
            _scrollPending = 0f;
        }
        Event se = NocturneStyle.Ev;
        if (se != null && se.type == EventType.ScrollWheel && area.Contains(se.mousePosition))
        {
            _scrollPending += se.delta.y * 20f;
            se.Use();
        }

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
                if (!_scrollMoved && Mathf.Abs(dy) > 6f)
                    _scrollMoved = true;
                if (_scrollMoved)
                {
                    _scrollPending -= dy;
                    se.Use();
                }
            }
            else if (se.type == EventType.MouseUp)
            {
                if (_scrollGrab && _scrollMoved)
                    se.Use();
                _scrollGrab = false;
                _scrollMoved = false;
            }
        }

        _subPinned = false;
        string[] pinned = PinnedSubs();
        if (pinned != null)
        {
            float py = area.y + 2f;
            SubBar(area.x + 4f, ref py, area.width - 20f, pinned);
            float barH = py - area.y;
            area = new Rect(area.x, area.y + barH, area.width, area.height - barH);
            _subPinned = true;
        }

        float slide = 0f;
        float cAlpha = 1f;
        if (_tabAnimAt >= 0f)
        {
            float ct = Mathf.Clamp01((NocturneStyle.Now - _tabAnimAt) / 0.17f);
            float ce = SmoothSat(ct);
            slide = (1f - ce) * 34f * _tabDir;
            cAlpha = Mathf.Clamp01(ce * 1.2f);
            if (ct >= 1f)
                _tabAnimAt = -1f;
        }

        Color cPrev = GUI.color;
        GUI.color = new Color(cPrev.r, cPrev.g, cPrev.b, cPrev.a * cAlpha);
        GUI.BeginGroup(area);
        _inScroll = true;
        float x = 4f;
        float cx = x + slide;
        float cw = area.width - 20f;
        float startY = 6f - _scroll;
        float cy = startY;
        _viewH = area.height;
        var localArea = new Rect(slide, 0f, area.width, area.height);
        _cardGrp = -1;
        LayBegin(cx, cy, cw, string.IsNullOrEmpty(_search) && _tab != FavTab ? ColsFor(cw) : 1);
        if (!string.IsNullOrEmpty(_search))
            DrawSearch(cx, ref cy, cw);
        else if (_tab == FavTab)
            DrawFavorites(cx, ref cy, cw);
        else
            switch (_tab)
            {
                case 0:
                    DrawHome(cx, ref cy, cw);
                    break;
                case 1:
                    DrawQoL(cx, ref cy, cw);
                    break;
                case 2:
                    DrawLobbyTab(cx, ref cy, cw);
                    break;
                case 3:
                    DrawVisual(cx, ref cy, cw);
                    break;
                case 4:
                    DrawPlayers(cx, ref cy, cw);
                    break;
                case 5:
                    try
                    {
                        DrawCheats(cx, ref cy, cw);
                    }
                    catch { }
                    break;
                case 6:
                    DrawGuard(cx, ref cy, cw);
                    break;
                case 7:
                    DrawNet(cx, ref cy, cw);
                    break;
                case 8:
                    DrawHostTab(cx, ref cy, cw);
                    break;
                case 9:
                    DrawSettings(cx, ref cy, cw);
                    break;
                default:
                    DrawEmpty(localArea, NocturneIcon.Star, NocturneText.T("Раздел в разработке", "Section in progress"));
                    break;
            }
        if (_layCols > 1)
            cy = LayBottom();
        _layCols = 1;

        _inScroll = false;
        GUI.EndGroup();
        GUI.color = cPrev;

        if (_slider != 0 && !Input.GetMouseButton(0))
            _slider = 0;

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

        Color grip = A(p.Muted, 0.5f);
        for (int gi = 0; gi < 3; gi++)
            for (int gj = 0; gj <= gi; gj++)
                NocturneStyle.Fill(new Rect(w - 8f - gj * 4f, h - 8f - gi * 4f, 2f, 2f), grip);
    }

    private bool _inRoom;
    private int _inRoomFrame = -1;

    private bool InRoom()
    {
        int f = Time.frameCount;
        if (f == _inRoomFrame)
            return _inRoom;

        _inRoomFrame = f;
        _inRoom = LobbyBehaviour.Instance != null || ShipStatus.Instance != null;
        return _inRoom;
    }

    [HideFromIl2Cpp]
    private void DrawHeader(float w, NocturnePalette p)
    {
        if (NocturneStyle.Lite)
        {
            _brand.normal.textColor = p.Text;
            Lab(new Rect(14f, 10f, w - 90f, 24f), "Nocturne  v" + NocturnePlugin.PluginVersion, _brand);
            var lminB = new Rect(w - 64f, 11f, 26f, 22f);
            var lcloseB = new Rect(w - 34f, 11f, 26f, 22f);
            if (WindowButton(lminB, NocturneIcon.Minimize, p))
                _collapsed = !_collapsed;
            if (WindowButton(lcloseB, NocturneIcon.Close, p))
                RequestClose();
            if (!_collapsed)
                NocturneStyle.Fill(new Rect(0f, HeaderH - 1f, w, 1f), A(Color.white, 0.12f));
            return;
        }

        DrawLogo(new Rect(14f, 10f, 24f, 24f), p);

        _brand.wordWrap = false;
        var brandR = new Rect(46f, 10f, 160f, 24f);
        Lab(brandR, "Nocturne", _brand);
        if (NocturneStyle.Painting)
        {
            float sh = Mathf.Repeat(NocturneStyle.Now * 0.6f, 3.4f) / 3.4f;
            float bw = brandR.width * 0.24f;
            float bx = brandR.x - bw + sh * (brandR.width + bw * 2f);
            Color hc = GUI.color;
            GUI.BeginGroup(new Rect(bx, brandR.y, bw, brandR.height));
            Color oc = _brand.normal.textColor;
            _brand.normal.textColor = Color.white;
            GUI.color = new Color(1f, 1f, 1f, hc.a * 0.55f);
            Lab(new Rect(brandR.x - bx, 0f, brandR.width, brandR.height), "Nocturne", _brand);
            _brand.normal.textColor = oc;
            GUI.color = hc;
            GUI.EndGroup();
        }

        bool inRoom = InRoom();
        Color dot = inRoom ? new Color(0.36f, 0.92f, 0.52f) : p.Muted;
        var chip = new Rect(w - 236f, 11f, 74f, 22f);
        NocturneStyle.FillRounded(chip, A(dot, 0.12f), 8);
        NocturneStyle.StrokeRounded(chip, A(dot, 0.30f), 8, 1);
        float pl = 0.55f + 0.45f * Mathf.Sin(NocturneStyle.Now * 2.4f);
        NocturneStyle.FillRounded(new Rect(chip.x + 11f, chip.y + chip.height / 2f - 3f, 6f, 6f), new Color(dot.r, dot.g, dot.b, inRoom ? 0.55f + 0.45f * pl : 0.8f), 3);
        _status.normal.textColor = Color.Lerp(dot, Color.white, 0.25f);
        Lab(new Rect(chip.x + 24f, chip.y, chip.width - 26f, chip.height), inRoom ? "online" : NocturneText.T("меню", "menu"), _status);

        var ver = new Rect(w - 154f, 11f, 58f, 22f);
        NocturneStyle.FillRounded(ver, A(p.Accent, 0.14f), 8);
        _verPill.normal.textColor = p.Accent;
        Lab(ver, "v" + NocturnePlugin.PluginVersion, _verPill);

        var minB = new Rect(w - 88f, 11f, 26f, 22f);
        var closeB = new Rect(w - 58f, 11f, 26f, 22f);
        if (WindowButton(minB, NocturneIcon.Minimize, p))
            _collapsed = !_collapsed;
        if (WindowButton(closeB, NocturneIcon.Close, p))
            RequestClose();

        if (!_collapsed)
            NocturneStyle.Fill(new Rect(0f, HeaderH - 1f, w, 1f), A(p.Accent, 0.30f));
    }

    [HideFromIl2Cpp]
    private bool WindowButton(Rect r, NocturneIcon icon, NocturnePalette p)
    {
        int id = RectId(r);
        Pressed(r, id);

        if (NocturneStyle.Painting)
        {
            bool hover = r.Contains(M);
            float k = PressK(id);
            bool scaled = k < 1f;
            Matrix4x4 m = default;
            if (scaled)
            {
                m = GUI.matrix;
                GUIUtility.ScaleAroundPivot(new Vector2(k, k), r.center);
            }
            if (hover)
                NocturneStyle.FillRounded(r, A(Color.white, 0.07f), 7);
            if (NocturneStyle.Lite)
            {
                _centerMuted.normal.textColor = hover ? p.Text : p.Muted;
                Lab(r, icon == NocturneIcon.Close ? "✕" : "—", _centerMuted);
            }
            else
                NocturneIcons.Draw(icon, new Rect(r.x + r.width / 2f - 7f, r.y + r.height / 2f - 7f, 14f, 14f), hover ? p.Text : p.Muted);
            if (scaled)
                GUI.matrix = m;
        }

        return GUI.Button(r, GUIContent.none, _invisible);
    }

    [HideFromIl2Cpp]
    private void DrawLogo(Rect box, NocturnePalette p)
    {
        Vector2 c = box.center;
        if (!NocturneStyle.Lite)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(NocturneStyle.Now * 2.2f);
            float g = box.width * 1.28f;
            NocturneStyle.FillRounded(new Rect(c.x - g * 0.5f, c.y - g * 0.5f, g, g), A(p.Accent, 0.09f + 0.13f * pulse), Mathf.RoundToInt(g * 0.5f));
        }

        int accKey = ((int)(p.Accent.r * 255f) << 16) | ((int)(p.Accent.g * 255f) << 8) | (int)(p.Accent.b * 255f);
        if (_crystalTex == null || _crystalAccent != accKey)
        {
            if (_crystalTex != null)
                UnityEngine.Object.Destroy(_crystalTex);
            _crystalTex = BuildLogo(p.Accent);
            _crystalAccent = accKey;
        }
        if (_crystalStyle == null)
            _crystalStyle = new GUIStyle();
        _crystalStyle.normal.background = _crystalTex;
        GUI.Box(box, GUIContent.none, _crystalStyle);
    }

    private static readonly float[][] LogoPolys =
    {
        new[] { 0.13f, 0.05f, 0.595f, 0.497f, 0.87f, 0.95f, 0.405f, 0.503f },
        new[] { 0.13f, 0.30f, 0.28f, 0.44f, 0.28f, 0.70f, 0.13f, 0.86f },
        new[] { 0.87f, 0.70f, 0.72f, 0.56f, 0.72f, 0.30f, 0.87f, 0.14f },
        new[] { 0.318f, 0.134f, 0.648f, 0.368f, 0.663f, 0.473f },
        new[] { 0.682f, 0.866f, 0.352f, 0.632f, 0.337f, 0.527f },
    };

    private static readonly float[][] LogoShades =
    {
        new[] { 0.6f, 0.3f, 0.45f, 0.9f },
        new[] { 0.95f, 0.35f, 0.2f, 0.7f },
        new[] { 0.2f, 0.7f, 0.95f, 0.35f },
        new[] { 0.8f },
        new[] { 0.4f },
    };

    private static Texture2D BuildLogo(Color accent)
    {
        const int n = 64;
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n, v = 1f - (y + 0.5f) / n;
                float best = 0f, edge = 0f;
                int hit = -1;
                for (int i = 0; i < LogoPolys.Length; i++)
                {
                    float d = PolyDist(LogoPolys[i], u, v) * n;
                    float cov = Mathf.Clamp01(0.5f - d);
                    if (cov > best)
                    {
                        best = cov;
                        edge = d;
                        hit = i;
                    }
                }
                if (hit < 0)
                    continue;

                float[] sh = LogoShades[hit];
                float s = sh.Length == 1 ? sh[0] : sh[Facet(LogoPolys[hit], u, v)];
                Color col = s >= 0.5f ? Color.Lerp(accent, Color.white, (s - 0.5f) * 1.1f) : Color.Lerp(accent * 0.45f, accent, s * 2f);
                col = Color.Lerp(col, Color.white, 0.35f * Mathf.Clamp01(1f + edge / 1.6f));
                px[y * n + x] = new Color32((byte)(col.r * 255f), (byte)(col.g * 255f), (byte)(col.b * 255f), (byte)(best * 255f));
            }
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, true)
        {
            hideFlags = HideFlags.HideAndDontSave,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Trilinear
        };
        tex.SetPixels32(px);
        tex.Apply(true);
        return tex;
    }

    private static float PolyDist(float[] p, float x, float y)
    {
        int cnt = p.Length / 2;
        float best = float.MaxValue;
        bool inside = false;
        for (int i = 0, j = cnt - 1; i < cnt; j = i, i++)
        {
            float ax = p[j * 2], ay = p[j * 2 + 1];
            float ex = p[i * 2] - ax, ey = p[i * 2 + 1] - ay;
            float wx = x - ax, wy = y - ay;
            float t = Mathf.Clamp01((wx * ex + wy * ey) / (ex * ex + ey * ey));
            float dx = wx - ex * t, dy = wy - ey * t;
            best = Mathf.Min(best, dx * dx + dy * dy);
            if ((ay > y) != (ay + ey > y) && x < ax + (y - ay) * ex / ey)
                inside = !inside;
        }
        return inside ? -Mathf.Sqrt(best) : Mathf.Sqrt(best);
    }

    private static int Facet(float[] p, float x, float y)
    {
        bool a = Side(p[0], p[1], p[4], p[5], x, y) == Side(p[0], p[1], p[4], p[5], p[2], p[3]);
        bool b = Side(p[2], p[3], p[6], p[7], x, y) == Side(p[2], p[3], p[6], p[7], p[0], p[1]);
        if (a)
            return b ? 0 : 1;
        return b ? 3 : 2;
    }

    private static bool Side(float ax, float ay, float bx, float by, float x, float y) =>
        (bx - ax) * (y - ay) - (by - ay) * (x - ax) > 0f;

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

        if (NocturneStyle.Painting)
        {
            Color ink = active ? Color.Lerp(p.Accent, Color.white, 0.35f) : (hover ? p.Text : p.Muted);
            NocturneIcons.Draw(icon, new Rect(r.x + 12f, r.y + r.height / 2f - 8f, 16f, 16f), ink);
            _tabStyle.normal.textColor = ink;
            Lab(new Rect(r.x + 36f, r.y, r.width - 40f, r.height), label, _tabStyle);
        }
        if (GUI.Button(r, GUIContent.none, _invisible))
            SetTab(index);
    }

    private bool _inScroll;

    private bool Cull(float y, float h) => _inScroll && (y + h < -4f || y > _viewH + 4f);

    private bool RowCull(ref float y, float h, float step)
    {
        if (!Cull(y, h))
            return false;
        y += step;
        return true;
    }

    private readonly Dictionary<string, bool> _cardCollapsed = new Dictionary<string, bool>();
    private readonly HashSet<string> _knownCards = new HashSet<string>();
    private readonly Dictionary<string, string> _upperCache = new Dictionary<string, string>();

    private string Up(string s)
    {
        if (string.IsNullOrEmpty(s))
            return s;
        if (!_upperCache.TryGetValue(s, out string u))
        {
            u = s.ToUpperInvariant();
            _upperCache[s] = u;
        }
        return u;
    }

    private void Grp(int g) => _cardGrp = g;

    private const int FavTab = 10;

    private enum FavKind
    {
        Bool, Float, Int, Cycle, Key
    }

    private sealed class FavItem
    {
        public FavKind Kind;
        public string Label;
        public readonly HashSet<string> Cards = new HashSet<string>();
        public string LastCard;
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

    private const int RecentMax = 8;
    private readonly List<string> _recent = new List<string>();
    private bool _recentLoaded;

    private void RecentLoad()
    {
        if (_recentLoaded)
            return;
        _recentLoaded = true;

        string csv = NocturneConfig.RecentFeatures != null ? NocturneConfig.RecentFeatures.Value : "";
        if (string.IsNullOrEmpty(csv))
            return;

        foreach (string raw in csv.Split(';'))
        {
            string k = raw.Trim();
            if (k.Length > 0 && !_recent.Contains(k))
                _recent.Add(k);
        }
    }

    private void RecentTouch(string key)
    {
        if (string.IsNullOrEmpty(key))
            return;

        RecentLoad();
        _recent.Remove(key);
        _recent.Insert(0, key);
        while (_recent.Count > RecentMax)
            _recent.RemoveAt(_recent.Count - 1);

        if (NocturneConfig.RecentFeatures != null)
            NocturneConfig.RecentFeatures.Value = string.Join(";", _recent.ToArray());
    }

    private void FavLoad()
    {
        if (_favLoaded) return;
        _favLoaded = true;
        string csv = NocturneConfig.FavFeatures != null ? NocturneConfig.FavFeatures.Value : "";
        if (string.IsNullOrEmpty(csv))
            return;
        foreach (string raw in csv.Split(';'))
        {
            string k = raw.Trim();
            if (k.Length > 0 && _favSet.Add(k))
                _favOrder.Add(k);
        }
    }

    private void FavSave()
    {
        if (NocturneConfig.FavFeatures != null)
            NocturneConfig.FavFeatures.Value = string.Join(";", _favOrder.ToArray());
    }

    private static readonly Dictionary<ConfigEntryBase, string> _favKeys = new Dictionary<ConfigEntryBase, string>();

    private static string FavKey(ConfigEntryBase e)
    {
        if (e == null)
            return null;
        if (_favKeys.TryGetValue(e, out string k))
            return k;

        try
        {
            k = e.Definition.Section + "/" + e.Definition.Key;
        }
        catch
        {
            k = null;
        }
        _favKeys[e] = k;
        return k;
    }

    private FavItem FavReg(string key, FavKind kind)
    {
        if (key == null)
            return null;
        if (!_favReg.TryGetValue(key, out FavItem it))
        {
            it = new FavItem();
            _favReg[key] = it;
        }
        it.Kind = kind;
        if (_curCard != null && !ReferenceEquals(it.LastCard, _curCard))
        {
            it.LastCard = _curCard;
            it.Cards.Add(_curCard);
        }
        return it;
    }

    private void FavCard(string cardKey)
    {
        if (string.IsNullOrEmpty(cardKey)) return;
        FavLoad();
        var keys = new List<string>();
        foreach (var kv in _favReg)
            if (kv.Value.Cards.Contains(cardKey))
                keys.Add(kv.Key);
        if (keys.Count == 0)
            return;

        bool allFav = true;
        for (int i = 0; i < keys.Count; i++)
            if (!_favSet.Contains(keys[i]))
            {
                allFav = false;
                break;
            }
        if (allFav)
        {
            for (int i = 0; i < keys.Count; i++)
                if (_favSet.Remove(keys[i]))
                    _favOrder.Remove(keys[i]);
            NocturneToast.Push(NocturneText.T("Избранное", "Favorites"), NocturneText.T("Карточка убрана", "Card removed"), 1.8f, NocturneNotifyKind.Info);
        }
        else
        {
            for (int i = 0; i < keys.Count; i++)
                if (_favSet.Add(keys[i]))
                    _favOrder.Add(keys[i]);
            NocturneToast.Push(NocturneText.T("Избранное", "Favorites"), NocturneText.T("Карточка в избранном", "Card added"), 1.8f, NocturneNotifyKind.Success);
        }
        FavSave();
    }

    private void FavRow(Rect r, string key)
    {
        if (key == null)
            return;
        FavLoad();
        Event e = NocturneStyle.Ev;
        if (e != null && e.type == EventType.MouseDown && e.button == 1 && r.Contains(e.mousePosition))
        {
            e.Use();
            if (_favSet.Remove(key))
            {
                _favOrder.Remove(key);
                NocturneToast.Push(NocturneText.T("Избранное", "Favorites"), NocturneText.T("Убрано из избранного", "Removed from favorites"), 1.6f, NocturneNotifyKind.Info);
            }
            else
            {
                _favSet.Add(key);
                _favOrder.Add(key);
                NocturneToast.Push(NocturneText.T("Избранное", "Favorites"), NocturneText.T("Добавлено в избранное", "Added to favorites"), 1.6f, NocturneNotifyKind.Success);
            }
            FavSave();
        }
        if (!NocturneStyle.Lite && _favSet.Count > 0 && _favSet.Contains(key))
            Lab(new Rect(r.x - 4f, r.y, 10f, r.height), "★", _favStar);
    }

    private void DrawRecent(float x, ref float y, float w)
    {
        RecentLoad();
        if (_recent.Count == 0) return;

        int shown = 0;
        for (int i = 0; i < _recent.Count; i++)
        {
            string key = _recent[i];
            if (_favSet.Contains(key) || !_favReg.TryGetValue(key, out FavItem it) || it.Label == null) continue;

            if (shown == 0)
                Sub(x, ref y, w, NocturneText.T("Недавние", "Recent"));
            shown++;
            FeatureRow(x, ref y, w, it);
        }
    }

    private void DrawFavorites(float x, ref float y, float w)
    {
        FavLoad();
        Lab(new Rect(x + 2f, y, w - 4f, 22f),
            NocturneText.T("Правый клик по строке или заголовку карточки — добавить/убрать.", "Right-click a row or card header to add/remove."), _muted);
        y += 28f;

        if (_favOrder.Count == 0)
        {
            Lab(new Rect(x + 2f, y, w - 4f, 24f), NocturneText.T("Пусто.", "Empty."), _muted);
            y += 26f;
            DrawRecent(x, ref y, w);
            return;
        }

        int missing = 0;
        for (int i = 0; i < _favOrder.Count; i++)
        {
            if (!_favReg.TryGetValue(_favOrder[i], out FavItem it))
            {
                missing++;
                continue;
            }
            switch (it.Kind)
            {
                case FavKind.Bool:
                    Toggle(x, ref y, w, it.Label, it.B);
                    break;
                case FavKind.Float:
                    Slider(x, ref y, w, it.Label, it.F, it.Min, it.Max, it.Fmt);
                    break;
                case FavKind.Int:
                    SliderInt(x, ref y, w, it.Label, it.I, (int)it.Min, (int)it.Max);
                    break;
                case FavKind.Cycle:
                    ActionCycle(x, ref y, w, it.Label, it.S, it.Vals, it.Disp);
                    break;
                case FavKind.Key:
                    KeyRow(x, ref y, w, it.Label, it.K);
                    break;
            }
        }
        if (missing > 0)
        {
            Lab(new Rect(x + 2f, y, w - 4f, 22f),
                NocturneText.T($"Ещё {missing} — откройте их разделы, чтобы подгрузить.", $"{missing} more — open their tabs to load."), _muted);
            y += 24f;
        }

        DrawRecent(x, ref y, w);
    }

    private static readonly int[][] RailGroups =
    {
        new[] { 0, 2, 4 },
        new[] { 5, 3, 1, 8 },
        new[] { 6, 7 },
    };

    private int[] _railOrder;

    private int[] RailOrder()
    {
        if (_railOrder != null)
            return _railOrder;

        var list = new List<int>(Tabs.Length + 2);
        for (int g = 0; g < RailGroups.Length; g++)
            list.AddRange(RailGroups[g]);
        list.Add(Tabs.Length + 1);
        list.Add(Tabs.Length);
        _railOrder = list.ToArray();
        return _railOrder;
    }

    private static string GroupName(int g) => g switch
    {
        0 => NocturneText.T("Игра", "Game"),
        1 => NocturneText.T("Фичи", "Features"),
        _ => NocturneText.T("Система", "System"),
    };

    private void DrawRail(Rect area)
    {
        NocturnePalette p = NocturneStyle.Current;
        int n = Tabs.Length + 2;

        Event e = NocturneStyle.Ev;
        if (e != null && e.type == EventType.ScrollWheel && area.Contains(e.mousePosition))
        {
            int[] order = RailOrder();
            int at = System.Array.IndexOf(order, _tab);
            if (at >= 0)
                SetTab(order[Mathf.Clamp(at + (e.delta.y > 0f ? 1 : -1), 0, order.Length - 1)]);
            e.Use();
        }

        bool paint = NocturneStyle.Painting;
        if (paint)
            DrawRailPill(area);

        float y = area.y + 2f;
        for (int g = 0; g < RailGroups.Length; g++)
        {
            if (paint)
                Lab(new Rect(area.x + 12f, y, area.width - 16f, 16f), Up(GroupName(g)), _railGroup);
            y += 18f;
            for (int k = 0; k < RailGroups[g].Length; k++)
                RailItem(area, ref y, RailGroups[g][k]);
            y += 6f;
        }

        if (paint)
            Lab(new Rect(area.x + 12f, y, area.width - 16f, 16f), Up(NocturneText.T("Мод", "Mod")), _railGroup);
        y += 18f;
        RailItem(area, ref y, Tabs.Length + 1);
        RailItem(area, ref y, Tabs.Length);
    }

    private void RailItem(Rect area, ref float y, int i)
    {
        NocturnePalette p = NocturneStyle.Current;
        var r = new Rect(area.x + 6f, y, area.width - 12f, 31f);
        y += 33f;

        bool hover = r.Contains(M);

        if (NocturneStyle.Painting)
        {
            float act = Mathf.Clamp01(1f - Mathf.Abs(r.y - area.y - _railSelOff) / 33f);
            if (hover && act < 1f)
                NocturneStyle.FillRounded(r, A(Color.white, 0.05f * (1f - act)), 8);

            NocturneIcon icon = i < Tabs.Length ? Tabs[i].Icon : (i == Tabs.Length ? NocturneIcon.Gear : NocturneIcon.Bookmark);
            Color ic = Color.Lerp(hover ? A(p.Text, 0.95f) : A(p.Text, 0.5f), Color.Lerp(p.Accent, Color.white, 0.5f), act);
            NocturneIcons.Draw(icon, new Rect(r.x + 9f, r.center.y - 8f, 17f, 17f), ic);

            string nm = i < Tabs.Length
                ? Tabs[i].Name
                : (i == Tabs.Length ? NocturneText.T("Настройки", "Settings") : NocturneText.T("Избранное", "Favorites"));
            _railLabel.normal.textColor = Color.Lerp(hover ? A(p.Text, 0.92f) : A(p.Text, 0.66f), p.Text, act);
            Lab(new Rect(r.x + 34f, r.y, r.width - 42f, r.height), nm, _railLabel);
        }

        if (GUI.Button(r, GUIContent.none, _invisible))
            SetTab(i);
    }

    private void DrawRailPill(Rect area)
    {
        float target = RailOffset(_tab);
        if (target < 0f)
            return;

        if (_railSelFrame != Time.frameCount)
        {
            _railSelFrame = Time.frameCount;
            if (_railSelOff < 0f)
                _railSelOff = target;
            else
            {
                _railSelOff = Mathf.Lerp(_railSelOff, target, 1f - Mathf.Exp(-18f * Time.unscaledDeltaTime));
                if (Mathf.Abs(_railSelOff - target) < 0.4f)
                    _railSelOff = target;
            }
        }

        NocturnePalette p = NocturneStyle.Current;
        var r = new Rect(area.x + 6f, area.y + _railSelOff, area.width - 12f, 31f);
        NocturneStyle.FillRounded(r, A(p.Accent, 0.16f), 8);
        NocturneStyle.FillRounded(new Rect(area.x, r.y + 4f, 3f, r.height - 8f), p.Accent, 1);
    }

    private static float RailOffset(int tab)
    {
        float y = 2f;
        for (int g = 0; g < RailGroups.Length; g++)
        {
            y += 18f;
            for (int k = 0; k < RailGroups[g].Length; k++)
            {
                if (RailGroups[g][k] == tab)
                    return y;
                y += 33f;
            }
            y += 6f;
        }

        y += 18f;
        if (tab == Tabs.Length + 1)
            return y;
        y += 33f;
        return tab == Tabs.Length ? y : -1f;
    }

    private readonly Dictionary<string, string> _cardKeys = new Dictionary<string, string>();
    private int _cardKeyTab = -1;

    private string CardKey(string title)
    {
        if (_cardKeyTab != _tab)
        {
            _cardKeyTab = _tab;
            _cardKeys.Clear();
        }
        if (!_cardKeys.TryGetValue(title, out string k))
        {
            k = _tab + "/" + title;
            _cardKeys[title] = k;
        }
        return k;
    }

    private int _layCols = 1;
    private readonly float[] _layY = new float[3];
    private float _layX;
    private float _layW;

    private const float LayGap = 8f;

    private const float LayMinCol = 236f;

    private int ColsFor(float w)
    {
        int forced = NocturneConfig.MenuColumns != null ? NocturneConfig.MenuColumns.Value : 0;
        if (forced > 0)
            return Mathf.Clamp(forced, 1, 3);
        return Mathf.Clamp(Mathf.FloorToInt((w + LayGap) / (LayMinCol + LayGap)), 1, 3);
    }

    private float CardW(float w) => _layCols > 1 ? LayColW() : w;

    private float RowPad => _layCols > 1 ? 60f : 74f;

    private void LayBegin(float x, float y, float w, int cols)
    {
        _layCols = Mathf.Clamp(cols, 1, 3);
        _layX = x;
        _layW = w;
        for (int i = 0; i < _layY.Length; i++)
            _layY[i] = y;

        bool tight = _layCols > 1 && LayColW() < 300f;
        if (_rowLabel != null)
        {
            _rowLabel.fontSize = tight ? 13 : 15;
            _muted.fontSize = tight ? 11 : 13;
            _mutedClip.fontSize = _muted.fontSize;
            _value.fontSize = tight ? 12 : 13;
            _valueClip.fontSize = _value.fontSize;
        }
    }

    private float LayBottom()
    {
        float b = _layY[0];
        for (int i = 1; i < _layCols; i++)
            if (_layY[i] > b)
                b = _layY[i];
        return b;
    }

    private float LayColW() => (_layW - (_layCols - 1) * LayGap) / _layCols;

    private int LayPick()
    {
        int best = 0;
        for (int i = 1; i < _layCols; i++)
            if (_layY[i] < _layY[best] - 0.5f)
                best = i;
        return best;
    }

    private int LaySuspend(ref float y)
    {
        if (_layCols <= 1)
            return 1;

        int cols = _layCols;
        y = LayBottom();
        _layCols = 1;
        return cols;
    }

    private void LayResume(int cols, float y)
    {
        if (cols <= 1)
            return;

        _layCols = cols;
        for (int i = 0; i < _layCols; i++)
            _layY[i] = y;
    }

    private Rect Card(float x, ref float y, float w, string title, float bodyH, bool wide = false)
    {
        NocturnePalette p = NocturneStyle.Current;
        const float head = 28f;
        _curCard = CardKey(title);

        if (string.IsNullOrEmpty(_search) && _tab != FavTab)
            NocturneSearchIndex.Note(_tab, _cardGrp, title);

        if (_cardGrp >= 0 && _cardGrp != _subTab[_tab])
            return new Rect(-20000f, -20000f, w - 20f, bodyH);

        int col = 0;
        if (_layCols > 1)
        {
            if (wide)
            {
                y = LayBottom();
                x = _layX;
                w = _layW;
            }
            else
            {
                col = LayPick();
                y = _layY[col];
                w = LayColW();
                x = _layX + col * (w + LayGap);
            }
        }

        if (_pendJumpTab == _tab && _pendJumpTitle == title)
        {
            _scroll = Mathf.Clamp((y + _scroll) - 6f, 0f, _maxScroll);
            _flashKey = _curCard;
            _flashUntil = NocturneStyle.Now + 1.6f;
            _pendJumpTab = -1;
            _pendJumpTitle = null;
        }

        _knownCards.Add(title);
        _cardCollapsed.TryGetValue(title, out bool collapsed);
        float shownBody = collapsed ? 0f : bodyH;
        var card = new Rect(x, y, w, head + shownBody + 6f);

        Event ce = NocturneStyle.Ev;
        var headRect = new Rect(card.x, card.y, card.width, head);
        if (ce != null && ce.type == EventType.MouseDown && ce.button == 0 && headRect.Contains(ce.mousePosition))
        {
            collapsed = !collapsed;
            _cardCollapsed[title] = collapsed;
            ce.Use();
        }
        else if (ce != null && ce.type == EventType.MouseDown && ce.button == 1 && headRect.Contains(ce.mousePosition))
        {
            FavCard(CardKey(title));
            ce.Use();
        }

        if (!Cull(y, card.height))
        {
            bool hover = headRect.Contains(M);
            if (hover)
                NocturneStyle.Fill(new Rect(card.x, card.y, card.width, head), A(Color.white, 0.03f));
            if (!NocturneStyle.Lite)
                NocturneStyle.FillRounded(new Rect(card.x + 4f, card.y + head / 2f - 6f, 3f, 12f), p.Accent, 2);
            Lab(new Rect(card.x + 16f, card.y, card.width - 44f, head), Up(title), _cardTitle);
            Lab(new Rect(card.xMax - 22f, card.y, 18f, head), collapsed ? "▸" : "▾", _centerMuted);
            NocturneStyle.Fill(new Rect(card.x + 4f, card.y + head - 1f, card.width - 8f, 1f), A(Color.white, 0.07f));
            if (_flashKey != null && _curCard == _flashKey)
            {
                float now = NocturneStyle.Now;
                if (now < _flashUntil)
                    NocturneStyle.StrokeRounded(card, A(p.Accent, 0.7f * Mathf.Clamp01((_flashUntil - now) / 1.6f)), 10, 2);
                else
                    _flashKey = null;
            }
        }

        y = card.yMax + 8f;
        if (_layCols > 1)
        {
            if (wide)
                for (int i = 0; i < _layCols; i++)
                    _layY[i] = y;
            else
                _layY[col] = y;
        }

        return collapsed
            ? new Rect(-20000f, -20000f, card.width - 20f, bodyH)
            : new Rect(card.x + 10f, card.y + head + 2f, card.width - 20f, bodyH);
    }

    private void Sub(float x, ref float y, float w, string title)
    {
        if (Cull(y, 26f))
        {
            y += 26f;
            return;
        }
        NocturnePalette p = NocturneStyle.Current;
        NocturneStyle.Fill(new Rect(x, y + 21f, w, 1f), A(Color.white, 0.06f));
        if (!NocturneStyle.Lite)
            NocturneStyle.FillRounded(new Rect(x, y + 6f, 2f, 12f), A(p.Accent, 0.75f), 1);
        Lab(new Rect(x + 10f, y - 1f, w - 12f, 22f), Up(title), _cardTitle);
        y += 26f;
    }

    private static readonly string[] KickBanVals = { "Kick", "Ban" };
    private static readonly string[] VoteVals = { "Null", "Warn", "Kick", "Ban" };
    private static string[] _kickBanDisp, _voteDisp;
    private static bool _dispRu;

    private static void EnsureDisp()
    {
        bool ru = NocturneText.IsRussian;
        if (_kickBanDisp != null && ru == _dispRu)
            return;
        _dispRu = ru;
        _kickBanDisp = new[] { NocturneText.T("Кик", "Kick"), NocturneText.T("Бан", "Ban") };
        _voteDisp = new[] { NocturneText.T("Нулл", "Null"), NocturneText.T("Варн", "Warn"), NocturneText.T("Кик", "Kick"), NocturneText.T("Бан", "Ban") };
    }

    private static string[] KickBanDisp
    {
        get
        {
            EnsureDisp();
            return _kickBanDisp;
        }
    }
    private static string[] VoteDisp
    {
        get
        {
            EnsureDisp();
            return _voteDisp;
        }
    }

    private static readonly string[] GuardSubsRu = { "Списки", "Фильтры", "Защита" };
    private static readonly string[] GuardSubsEn = { "Lists", "Filters", "Shield" };

    private void DrawGuard(float x, ref float y, float w)
    {
        SubBar(x, ref y, w, GuardSubsRu, GuardSubsEn);

        Grp(0);
        Rect b = Card(x, ref y, w, NocturneText.T("Списки доступа", "Access lists"), 2f * RowH + 62f, true);
        float by = b.y;
        Toggle(b.x, ref by, b.width, NocturneText.T("Бан-лист (кик по заходу)", "Ban list (kick on join)"), NocturneConfig.AccessBanEnabled);
        Toggle(b.x, ref by, b.width, NocturneText.T("Только вайтлист", "Whitelist only"), NocturneConfig.AccessWhitelistOnly);
        Lab(new Rect(b.x + 2f, by, b.width * 0.5f, 26f), $"{NocturneText.T("Бан", "Ban")}: <b>{NocturneAccess.BanCount}</b>   {NocturneText.T("Вайт", "White")}: <b>{NocturneAccess.WhiteCount}</b>", _muted);
        if (SmallButton(new Rect(b.x + b.width - 174f, by + 1f, 82f, 24f), NocturneText.T("ОЧ. БАН", "CLR BAN"), new Color(0.9f, 0.36f, 0.36f)))
            NocturneAccess.ClearBans();
        if (SmallButton(new Rect(b.x + b.width - 86f, by + 1f, 82f, 24f), NocturneText.T("ОЧ. ВАЙТ", "CLR WHITE"), NocturneStyle.Current.Accent))
            NocturneAccess.ClearWhites();
        by += 32f;
        if (SmallButton(new Rect(b.x + 2f, by, 112f, 24f), NocturneText.T("ИМПОРТ TXT", "IMPORT TXT"), NocturneStyle.Current.Accent))
            NocturneAccess.ImportTxt();
        Lab(new Rect(b.x + 122f, by, b.width - 122f, 24f), "Among Us/Nocturne/BanList.txt · WhiteList.txt", _muted);

        Grp(0);
        b = Card(x, ref y, w, NocturneText.T("Ник-бан и история", "Nick ban & history"), 4f * RowH + 62f, true);
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
        Lab(new Rect(b.x + 2f, by, b.width * 0.5f, 26f), $"{NocturneText.T("Ников", "Nicks")}: <b>{NocturneAccess.NickBanCount}</b>", _muted);
        if (SmallButton(new Rect(b.x + b.width - 96f, by + 1f, 92f, 24f), NocturneText.T("ОЧИСТИТЬ", "CLEAR"), new Color(0.9f, 0.36f, 0.36f)))
            NocturneAccess.ClearNickBans();

        Grp(0);
        b = Card(x, ref y, w, NocturneText.T("Платформ-бан", "Platform ban"), 2f * RowH + 32f, true);
        by = b.y;
        Toggle(b.x, ref by, b.width, NocturneText.T("Бан по имени платформы", "Ban by platform name"), NocturneConfig.AccessPlatformBanEnabled);
        _platformText = CustomText(new Rect(b.x + 2f, by, b.width - 168f, 26f), _platformText ?? "", "platformBan");
        if (SmallButton(new Rect(b.x + b.width - 160f, by + 1f, 158f, 24f), NocturneText.T("＋ В БАН", "＋ TO BAN"), NocturneStyle.Current.Accent) && !string.IsNullOrWhiteSpace(_platformText))
        {
            NocturneAccess.AddPlatformBan(_platformText);
            _platformText = "";
        }
        by += 32f;
        Lab(new Rect(b.x + 2f, by, b.width * 0.5f, 26f), $"{NocturneText.T("Платформ", "Platforms")}: <b>{NocturneAccess.PlatformBanCount}</b>", _muted);
        if (SmallButton(new Rect(b.x + b.width - 96f, by + 1f, 92f, 24f), NocturneText.T("ОЧИСТИТЬ", "CLEAR"), new Color(0.9f, 0.36f, 0.36f)))
            NocturneAccess.ClearPlatformBans();

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
        Lab(new Rect(b.x + 2f, by, b.width - 2f, 36f), NocturneText.T("Саботаж мирным, вент без права и через систему, скан/анимация импостером, репорт/двери в H&S.", "Crew sabotage, illegal vent (both paths), impostor scan/anim, report/doors in H&S."), _muted);

        Grp(2);
        b = Card(x, ref y, w, NocturneText.T("Анти-бан", "Anti-ban"), 2f * RowH + 42f);
        by = b.y;
        Toggle(b.x, ref by, b.width, NocturneText.T("На хосте: банить отправителя", "As host: punish sender"), NocturneConfig.AntiBanHost);
        ActionCycle(b.x, ref by, b.width, NocturneText.T("Реакция", "Action"), NocturneConfig.AntiBanHostAction, VoteVals, VoteDisp);
        Lab(new Rect(b.x + 2f, by, b.width - 2f, 36f), NocturneText.T("Гасит краш-бан (vent-kick) пакет. Вне хоста работает всегда.", "Kills the crash-ban (vent-kick) packet. Off-host it is always on."), _muted);

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
        Lab(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Режет чужие попытки катать/выбивать тебя.", "Drops others' attempts to ride/boot you."), _muted);

        Grp(1);
        b = Card(x, ref y, w, NocturneText.T("Модерация", "Moderation"), RowH);
        by = b.y;
        Toggle(b.x, ref by, b.width, NocturneText.T("Кик/бан в матче (хост)", "Kick/ban in match (host)"), NocturneConfig.UnlockMatchKickBan);

        Grp(1);
        b = Card(x, ref y, w, NocturneText.T("Детект входящих", "Join detect"), 2f * RowH + 26f);
        by = b.y;
        Toggle(b.x, ref by, b.width, NocturneText.T("Показывать платформу/ур./raw", "Show platform/lvl/raw"), NocturneConfig.JoinDetect);
        Toggle(b.x, ref by, b.width, NocturneText.T("Видеть своих (Nocturne)", "See other Nocturne users"), NocturneConfig.ModHandshake);
        Lab(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Тост при заходе; ⚠ на подозрит. raw-имя.", "Toast on join; ⚠ on suspicious raw name."), _muted);
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
            if (busy)
                Lab(r, "×", _star);
            NocturneStyle.StrokeRounded(r, i == want ? NocturneStyle.Current.Accent : A(Color.white, 0.10f), 6, i == want ? 2 : 1);
            if (GUI.Button(r, GUIContent.none, _invisible))
                NocturneConfig.SnipeColorId.Value = i;
        }
        by += rows * 32f;

        bool free = me != null && !NocturneColorSnipe.Taken(want, me);
        Lab(new Rect(b.x + 2f, by, b.width - 2f, 22f),
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
            Lab(new Rect(b.x + 2f, by, b.width - 2f, 22f), NocturneText.T("Хостом. Красит и новозашедших.", "Host only. Colors new joiners too."), _muted);
            return;
        }

        int pick = Mathf.Clamp(NocturneConfig.ColorAllId.Value, 0, max);
        for (int i = 0; i <= max; i++)
        {
            var r = new Rect(b.x + (i % cols) * 32f, by + (i / cols) * 32f, 26f, 26f);
            NocturneStyle.FillRounded(r, A(Color.black, 0.25f), 6);
            DrawColorDot(new Rect(r.x + 5f, r.y + 5f, 16f, 16f), i);
            NocturneStyle.StrokeRounded(r, i == pick ? NocturneStyle.Current.Accent : A(Color.white, 0.10f), 6, i == pick ? 2 : 1);
            if (GUI.Button(r, GUIContent.none, _invisible))
                NocturneConfig.ColorAllId.Value = i;
        }
        by += rows * 32f;
        Lab(new Rect(b.x + 2f, by, b.width - 2f, 22f), NocturneText.T("Красит и новозашедших. Выкл — вернут свои цвета.", "Colors new joiners. Off — they repick colors."), _muted);
    }

    private static Color[] _dotCols;

    [HideFromIl2Cpp]
    private void DrawColorDot(Rect r, PlayerControl pc, int round = 6)
    {
        if (!NocturneStyle.Painting)
            return;
        DrawColorDot(r, pc.Data != null && pc.Data.DefaultOutfit != null ? pc.Data.DefaultOutfit.ColorId : 0, round);
    }

    private void DrawColorDot(Rect r, int colorId, int round = 6)
    {
        if (!NocturneStyle.Painting)
            return;

        if (_dotCols == null)
        {
            try
            {
                if (Palette.PlayerColors != null)
                {
                    _dotCols = new Color[Palette.PlayerColors.Length];
                    for (int i = 0; i < _dotCols.Length; i++)
                    {
                        Color32 c32 = Palette.PlayerColors[i];
                        _dotCols[i] = new Color(c32.r / 255f, c32.g / 255f, c32.b / 255f, 1f);
                    }
                }
            }
            catch { }
        }

        Color c = _dotCols != null && colorId >= 0 && colorId < _dotCols.Length
            ? _dotCols[colorId]
            : new Color(0.5f, 0.5f, 0.5f, 1f);
        NocturneStyle.FillRounded(r, c, round);
        NocturneStyle.StrokeRounded(r, A(Color.white, 0.3f), round, 1);
    }

    private void PlayerRow(float x, ref float y, float w, InnerNetClient net, ClientData c)
    {
        if (Cull(y, RowH + 2f))
        {
            y += RowH + 6f;
            return;
        }
        var r = new Rect(x, y, w, RowH + 2f);
        HoverFill(r);
        if (NocturneStyle.Painting)
        {
            int nk = NocturneNameHistory.KnownNickCount(c.Character);
            string note = nk > 1 ? "  " + NocturneText.T($"·{nk} ников", $"·{nk} nicks") : string.Empty;
            Lab(new Rect(r.x + 10f, r.y, r.width - 200f, r.height),
                $"<b>{NocturneAccess.SafeName(c)}</b>   <color=#8A94AC><size=11>{ClientInfo(c)}{note}</size></color>", _rowName);
        }
        float cy = r.y + (r.height - 24f) / 2f;
        string mfc = NocturneColorReservations.Fc(c.Character);
        bool muted = NocturneMuteList.IsMuted(mfc);
        if (SmallButton(new Rect(r.xMax - 188f, cy, 44f, 24f), NocturneText.T("МУТ", "MUTE"), muted ? new Color(0.9f, 0.4f, 0.4f) : new Color(0.5f, 0.55f, 0.62f)))
            NocturneMuteList.Toggle(mfc);
        if (SmallButton(new Rect(r.xMax - 142f, cy, 44f, 24f), NocturneText.T("НИК", "NICK"), new Color(0.86f, 0.5f, 0.28f)))
            NocturneAccess.NickBanClient(net, c);
        if (SmallButton(new Rect(r.xMax - 96f, cy, 44f, 24f), NocturneText.T("БАН", "BAN"), new Color(0.9f, 0.36f, 0.36f)))
            NocturneAccess.BanClient(net, c);
        if (SmallButton(new Rect(r.xMax - 50f, cy, 44f, 24f), NocturneText.T("ВАЙТ", "WHITE"), NocturneStyle.Current.Accent))
            NocturneAccess.WhiteClient(c);
        y += RowH + 6f;
    }

    private void NickListCard(float x, ref float y, float w)
    {
        IReadOnlyList<string> nicks = NocturneAccess.NickBanEntries;
        float body = nicks.Count > 0 ? nicks.Count * 30f : 26f;
        Rect b = Card(x, ref y, w, $"{NocturneText.T("Ник-бан список", "Nick ban list")} ({nicks.Count})", body, true);
        float by = b.y;
        if (nicks.Count == 0)
        {
            Lab(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Пусто.", "Empty."), _muted);
            return;
        }

        for (int i = 0; i < nicks.Count; i++)
        {
            var r = new Rect(b.x, by, b.width, 28f);
            HoverFill(r);
            if (NocturneStyle.Painting)
                Lab(new Rect(r.x + 8f, r.y, r.width - 46f, r.height), $"<b>{nicks[i]}</b>", _rowName);
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
        Rect b = Card(x, ref y, w, $"{NocturneText.T("Кто уходил", "Who left")} ({recent.Count})", body, true);
        float by = b.y;

        if (SmallButton(new Rect(b.x + b.width - 96f, by, 92f, 24f), NocturneText.T("ОЧИСТИТЬ", "CLEAR"), new Color(0.9f, 0.36f, 0.36f)))
            NocturneRecent.Clear();
        by += 30f;

        if (recent.Count == 0)
        {
            Lab(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Пусто.", "Empty."), _muted);
            return;
        }

        for (int i = 0; i < recent.Count; i++)
        {
            RecentEntry e = recent[i];
            var r = new Rect(b.x, by, b.width, 44f);
            HoverFill(r);
            Lab(new Rect(r.x + 8f, r.y + 1f, r.width - 162f, 22f), e.Title, _rowName);
            Lab(new Rect(r.x + 8f, r.y + 21f, r.width - 162f, 20f), e.Info, _rowInfo);

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
        Rect b = Card(x, ref y, w, $"{NocturneText.T("Платформ-бан список", "Platform ban list")} ({plats.Count})", body, true);
        float by = b.y;
        if (plats.Count == 0)
        {
            Lab(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Пусто.", "Empty."), _muted);
            return;
        }

        for (int i = 0; i < plats.Count; i++)
        {
            var r = new Rect(b.x, by, b.width, 28f);
            HoverFill(r);
            if (NocturneStyle.Painting)
                Lab(new Rect(r.x + 8f, r.y, r.width - 46f, r.height), $"<b>{plats[i]}</b>", _rowName);
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
        Rect b = Card(x, ref y, w, $"{title} ({entries.Count})", body, true);
        float by = b.y;
        if (entries.Count == 0)
        {
            Lab(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Пусто.", "Empty."), _muted);
            return;
        }

        for (int i = 0; i < entries.Count; i++)
        {
            AccessEntry e = entries[i];
            var r = new Rect(b.x, by, b.width, 28f);
            HoverFill(r);
            string name = string.IsNullOrEmpty(e.Name) ? (string.IsNullOrEmpty(e.Code) ? NocturneText.T("гость", "guest") : e.Code) : e.Name;
            Lab(new Rect(r.x + 8f, r.y, r.width - 46f, r.height),
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
        Rect b = Card(x, ref y, w, $"{NocturneText.T("Недавние игроки", "Recent players")} ({rows.Count})", body, true);
        float by = b.y;

        Lab(new Rect(b.x + 2f, by, b.width - 100f, 24f),
            NocturneText.T("Кого игра запомнила по прошлым лобби — с ФК и PUID.", "Whom the game remembers from past lobbies — with FC and PUID."), _muted);
        if (SmallButton(new Rect(b.x + b.width - 96f, by + 1f, 92f, 24f), NocturneText.T("ОЧИСТИТЬ", "CLEAR"), new Color(0.9f, 0.36f, 0.36f)))
            NocturneRecentPlayers.Clear();
        by += 26f;

        if (rows.Count == 0)
        {
            Lab(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Пусто — сыграй хотя бы одно лобби.", "Empty — play at least one lobby."), _muted);
            return;
        }

        for (int i = 0; i < rows.Count; i++)
        {
            RecentRow e = rows[i];
            if (RowCull(ref by, 28f, 30f))
                continue;
            var r = new Rect(b.x, by, b.width, 28f);
            HoverFill(r);
            Lab(new Rect(r.x + 8f, r.y, r.width - 198f, r.height),
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
        if (!string.IsNullOrEmpty(e.Code))
            s = e.Code;
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
        int id = RectId(r);
        Pressed(r, id);

        if (NocturneStyle.Painting)
        {
            bool hover = r.Contains(M);
            float k = PressK(id);
            bool scaled = k < 1f;
            Matrix4x4 m = default;
            if (scaled)
            {
                m = GUI.matrix;
                GUIUtility.ScaleAroundPivot(new Vector2(k, k), r.center);
            }
            NocturneStyle.FillRounded(r, hover ? A(col, 0.4f) : A(col, 0.2f), 7);
            NocturneStyle.Fill(new Rect(r.x + 4f, r.y + 2f, r.width - 8f, 1f), A(Color.white, 0.14f));
            _smallBtn.normal.textColor = Color.Lerp(col, Color.white, 0.55f);
            Lab(r, label, _smallBtn);
            if (scaled)
                GUI.matrix = m;
        }

        return GUI.Button(r, GUIContent.none, _invisible);
    }

    [HideFromIl2Cpp]
    private void ActionCycle(float x, ref float y, float w, string label, ConfigEntry<string> entry, string[] vals, string[] disp)
    {
        string fk = FavKey(entry);
        var cit = FavReg(fk, FavKind.Cycle);
        if (cit != null)
        {
            cit.Label = label;
            cit.S = entry;
            cit.Vals = vals;
            cit.Disp = disp;
        }
        FavRow(new Rect(x, y, w, RowH - 4f), fk);
        int i = Mathf.Max(0, Array.IndexOf(vals, entry.Value));
        if (CycleRow(x, ref y, w, label, disp[i]))
            entry.Value = vals[(i + 1) % vals.Length];
    }

    [HideFromIl2Cpp]
    private void CollectClients(List<ClientData> into)
    {
        try
        {
            InnerNetClient net = GuardNet();
            if (net == null || net.allClients == null)
                return;
            var e = net.allClients.GetEnumerator();
            while (e.MoveNext())
            {
                ClientData c = e.Current;
                if (c != null && c.Id >= 0 && c.Id != net.ClientId)
                    into.Add(c);
            }
        }
        catch { }
    }

    private PlayerControl NetSrc()
    {
        if (_netSrc == 255)
            return PlayerControl.LocalPlayer;
        _netPick.Clear();
        CollectPlayers(_netPick);
        for (int i = 0; i < _netPick.Count; i++)
            if (_netPick[i].PlayerId == _netSrc)
                return _netPick[i];
        _netSrc = 255;
        return PlayerControl.LocalPlayer;
    }

    private void NextNetSrc()
    {
        _netPick.Clear();
        CollectPlayers(_netPick);
        _netPick.RemoveAll((Predicate<PlayerControl>)(p => p == PlayerControl.LocalPlayer));
        if (_netPick.Count == 0)
        {
            _netSrc = 255;
            return;
        }
        if (_netSrc == 255)
        {
            _netSrc = _netPick[0].PlayerId;
            return;
        }
        int i = _netPick.FindIndex((Predicate<PlayerControl>)(p => p.PlayerId == _netSrc));
        _netSrc = i < 0 || i + 1 >= _netPick.Count ? (byte)255 : _netPick[i + 1].PlayerId;
    }

    private readonly List<PlayerControl> _pcCache = new List<PlayerControl>();
    private PlayerControl _me;
    private int _pcFrame = -1;

    [HideFromIl2Cpp]
    private void PcRefresh()
    {
        int f = Time.frameCount;
        if (f == _pcFrame)
            return;

        _pcFrame = f;
        _pcCache.Clear();
        _me = PlayerControl.LocalPlayer;
        try
        {
            if (PlayerControl.AllPlayerControls == null)
                return;
            foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
                if (pc != null && pc.Data != null && !pc.Data.Disconnected)
                    _pcCache.Add(pc);
        }
        catch { }
    }

    [HideFromIl2Cpp]
    private PlayerControl Me()
    {
        PcRefresh();
        return _me;
    }

    [HideFromIl2Cpp]
    private void CollectPlayers(List<PlayerControl> into)
    {
        PcRefresh();
        for (int i = 0; i < _pcCache.Count; i++)
            into.Add(_pcCache[i]);
    }

    private void WhisperRow(float x, ref float y, float w, PlayerControl pc)
    {
        if (RowCull(ref y, 30f, 32f)) return;
        var r = new Rect(x, y, w, 30f);
        HoverFill(r);
        DrawColorDot(new Rect(r.x + 6f, r.y + 9f, 12f, 12f), pc);
        Lab(new Rect(r.x + 26f, r.y, r.width - 120f, r.height), pc.Data != null ? pc.Data.PlayerName : "?", _rowName);
        if (SmallButton(new Rect(r.xMax - 100f, r.y + 3f, 96f, 24f), NocturneText.T("ШЕПНУТЬ", "WHISPER"), new Color(0.55f, 0.7f, 1f)))
            NocturneWhisper.Prefill(pc.PlayerId.ToString());
        y += 32f;
    }

    private void TpRow(float x, ref float y, float w, PlayerControl pc)
    {
        if (RowCull(ref y, 30f, 32f))
            return;
        var r = new Rect(x, y, w, 30f);
        HoverFill(r);
        DrawColorDot(new Rect(r.x + 6f, r.y + 9f, 12f, 12f), pc);
        float tw = VoteW(r.width);
        float gw = AutoW(r.width) + 6f;
        Lab(new Rect(r.x + 26f, r.y, Mathf.Max(24f, r.width - 40f - gw - tw), r.height), pc.Data != null ? pc.Data.PlayerName : "?", _rowName);
        if (SmallButton(new Rect(r.xMax - tw - gw - 8f, r.y + 3f, gw, 24f), NocturneText.T("К НЕМУ", "GO"), new Color(0.5f, 0.78f, 0.92f)))
            TpTo(pc);
        bool follow = NocturneFollow.IsTarget(pc.PlayerId);
        if (SmallButton(new Rect(r.xMax - tw - 4f, r.y + 3f, tw, 24f), follow ? NocturneText.T("СТОП", "STOP") : NocturneText.T("ИДТИ ЗА", "FOLLOW"), follow ? new Color(0.9f, 0.4f, 0.4f) : new Color(0.55f, 0.7f, 1f)))
            NocturneToast.Push(NocturneText.T("Слежка", "Follow"), NocturneFollow.Toggle(pc), 2f, NocturneNotifyKind.Info);
        y += 32f;
    }

    private static void TpTo(PlayerControl pc)
    {
        try
        {
            PlayerControl me = PlayerControl.LocalPlayer;
            if (pc == null || me == null || me.NetTransform == null)
                return;
            me.NetTransform.RpcSnapTo(pc.GetTruePosition());
        }
        catch { }
    }

    private static float AutoW(float w) => Mathf.Clamp(w * 0.24f, 56f, 86f);

    private static float VoteW(float w) => Mathf.Clamp(w * 0.30f, 76f, 96f);

    private void VotekickRow(float x, ref float y, float w, PlayerControl pc)
    {
        if (RowCull(ref y, 30f, 32f)) return;
        var r = new Rect(x, y, w, 30f);
        HoverFill(r);
        DrawColorDot(new Rect(r.x + 6f, r.y + 9f, 12f, 12f), pc);
        if (NocturneStyle.Painting)
        {
            string nm = pc.Data != null ? pc.Data.PlayerName : "?";
            if (IsHost(pc))
                nm += "  [H]";
            Lab(new Rect(r.x + 26f, r.y, Mathf.Max(24f, r.width - 40f - AutoW(r.width) - VoteW(r.width)), r.height), nm, _rowName);
        }
        float vw = VoteW(r.width);
        float aw = AutoW(r.width);
        bool sel = NocturneVotekick.IsTarget(pc.PlayerId);
        if (SmallButton(new Rect(r.xMax - vw - aw - 8f, r.y + 3f, aw, 24f), sel ? NocturneText.T("АВТО ✓", "AUTO ✓") : NocturneText.T("АВТО", "AUTO"), sel ? NocturneStyle.Current.Accent : new Color(0.36f, 0.39f, 0.47f)))
            NocturneVotekick.ToggleTarget(pc.PlayerId);
        if (SmallButton(new Rect(r.xMax - vw - 4f, r.y + 3f, vw, 24f), NocturneText.T("ЗАЯВИТЬ", "VOTE"), new Color(0.78f, 0.42f, 0.95f)))
            NocturneVotekick.VoteOne(pc);
        y += 32f;
    }

    private void LoopRow(float x, ref float y, float w, PlayerControl pc)
    {
        if (RowCull(ref y, 30f, 32f))
            return;
        var r = new Rect(x, y, w, 30f);
        HoverFill(r);
        DrawColorDot(new Rect(r.x + 6f, r.y + 9f, 12f, 12f), pc);
        if (NocturneStyle.Painting)
        {
            string nm = pc.Data != null ? pc.Data.PlayerName : "?";
            if (pc.Data != null && pc.Data.IsDead)
                nm += "  <size=80%>†</size>";
            Lab(new Rect(r.x + 26f, r.y, r.width - 130f, r.height), nm, _rowName);
        }

        bool run = NocturneLobbyPranks.LoopTarget == pc.PlayerId && NocturneLobbyPranks.LoopLeft > 0;
        string lbl = run ? NocturneText.T("СТОП ", "STOP ") + NocturneLobbyPranks.LoopLeft : NocturneText.T("УБИТЬ ×20", "KILL ×20");
        if (SmallButton(new Rect(r.xMax - 110f, r.y + 3f, 106f, 24f), lbl, run ? new Color(0.9f, 0.4f, 0.4f) : new Color(0.78f, 0.42f, 0.95f)))
            NocturneToast.Push(NocturneText.T("Цикл смерти", "Murder loop"), NocturneLobbyPranks.MurderLoop(pc, 20), 2f, NocturneNotifyKind.Info);
        y += 32f;
    }

    private void KillRow(float x, ref float y, float w, PlayerControl pc)
    {
        if (RowCull(ref y, 30f, 32f))
            return;
        var r = new Rect(x, y, w, 30f);
        HoverFill(r);
        DrawColorDot(new Rect(r.x + 6f, r.y + 9f, 12f, 12f), pc);
        if (NocturneStyle.Painting)
        {
            string nm = pc.Data != null ? pc.Data.PlayerName : "?";
            if (pc.Data != null && pc.Data.IsDead)
                nm += "  <size=80%>†</size>";
            Lab(new Rect(r.x + 26f, r.y, r.width - 322f, r.height), nm, _rowName);
        }
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
        if (RowCull(ref y, 30f, 32f))
            return;
        var r = new Rect(x, y, w, 30f);
        HoverFill(r);
        DrawColorDot(new Rect(r.x + 6f, r.y + 9f, 12f, 12f), pc);
        if (NocturneStyle.Painting)
        {
            string nm = pc.Data != null ? pc.Data.PlayerName : "?";
            Lab(new Rect(r.x + 26f, r.y, r.width - 120f, r.height), nm, _rowName);
        }
        if (SmallButton(new Rect(r.xMax - 90f, r.y + 3f, 86f, 24f), NocturneText.T("ПОДСТАВИТЬ", "FRAME"), new Color(0.85f, 0.45f, 0.2f)))
            NocturneToast.Push(NocturneText.T("Подстава", "Frame"), NocturneFrameSabotage.Send(pc, system, (byte)_frameValue), 2.4f, NocturneNotifyKind.Info);
        y += 32f;
    }

    private void ShieldCard(float x, ref float y, float w)
    {
        bool ready = Utils.Host && Utils.InGame;
        _shieldPlayers.Clear();
        CollectPlayers(_shieldPlayers);
        int n = ready ? _shieldPlayers.Count : 0;

        Rect b = Card(x, ref y, w, NocturneText.T("Щит игрокам (хост)", "Shield players (host)"),
            ready ? 28f + 30f + RowH + (n > 0 ? n * 32f : 26f) : 26f, true);
        float by = b.y;

        if (!ready)
        {
            Lab(new Rect(b.x + 2f, by, b.width - 2f, 24f),
                Utils.Host ? NocturneText.T("Только в матче.", "In match only.") : NocturneText.T("Только хост.", "Host only."), _muted);
            return;
        }

        Lab(new Rect(b.x + 2f, by, b.width - 2f, 24f),
            NocturneText.T("Щит Ангела: не убить, пока держится.", "Angel shield: unkillable while held."), _muted);
        by += 28f;

        float half = (b.width - 8f) / 2f;
        if (SmallButton(new Rect(b.x, by, half, 26f), NocturneText.T("ЩИТ ВСЕМ", "SHIELD ALL"), new Color(0.35f, 0.75f, 0.55f)))
            NocturneToast.Push(NocturneText.T("Щит", "Shield"), NocturneShield.GiveAll(), 2.2f, NocturneNotifyKind.Success);
        if (SmallButton(new Rect(b.x + half + 8f, by, half, 26f), NocturneText.T("СНЯТЬ ОТМЕТКИ", "CLEAR MARKS"), new Color(0.9f, 0.4f, 0.4f)))
            NocturneShield.ClearMarks();
        by += 30f;

        Toggle(b.x, ref by, b.width, NocturneText.T("Держать щит на всех", "Keep everyone shielded"), NocturneConfig.ShieldAll);

        if (n == 0)
        {
            Lab(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Нет игроков.", "No players."), _muted);
            return;
        }

        for (int i = 0; i < n; i++)
            ShieldRow(b.x, ref by, b.width, _shieldPlayers[i]);
    }

    private void ShieldRow(float x, ref float y, float w, PlayerControl pc)
    {
        if (RowCull(ref y, 30f, 32f))
            return;
        var r = new Rect(x, y, w, 30f);
        HoverFill(r);
        DrawColorDot(new Rect(r.x + 6f, r.y + 9f, 12f, 12f), pc);

        bool sel = NocturneShield.IsMarked(pc.PlayerId);
        var box = new Rect(r.x + 24f, r.y + 5f, 20f, 20f);
        NocturneStyle.FillRounded(box, sel ? NocturneStyle.Current.Accent : A(Color.black, 0.25f), 5);
        if (sel)
            Lab(box, "✔", _star);
        NocturneStyle.StrokeRounded(box, A(Color.white, 0.14f), 5, 1);
        if (GUI.Button(box, GUIContent.none, _invisible))
            NocturneShield.ToggleMark(pc.PlayerId);

        if (NocturneStyle.Painting)
        {
            string nm = pc.Data != null ? pc.Data.PlayerName : "?";
            if (IsHost(pc))
                nm += "  [H]";
            Lab(new Rect(r.x + 50f, r.y, r.width - 140f, r.height), nm, _rowName);
        }
        if (SmallButton(new Rect(r.xMax - 90f, r.y + 3f, 86f, 24f), NocturneText.T("ЩИТ", "SHIELD"), new Color(0.35f, 0.75f, 0.55f)))
            NocturneToast.Push(NocturneText.T("Щит", "Shield"), NocturneShield.Give(pc), 2.2f, NocturneNotifyKind.Success);
        y += 32f;
    }

    private void VentKickRow(float x, ref float y, float w, PlayerControl pc)
    {
        if (RowCull(ref y, 30f, 32f))
            return;
        var r = new Rect(x, y, w, 30f);
        HoverFill(r);
        DrawColorDot(new Rect(r.x + 6f, r.y + 9f, 12f, 12f), pc);

        bool sel = NocturneVentKick.IsSelected(pc.PlayerId);
        var box = new Rect(r.x + 24f, r.y + 5f, 20f, 20f);
        NocturneStyle.FillRounded(box, sel ? NocturneStyle.Current.Accent : A(Color.black, 0.25f), 5);
        if (sel)
            Lab(box, "✔", _star);
        NocturneStyle.StrokeRounded(box, A(Color.white, 0.14f), 5, 1);
        if (GUI.Button(box, GUIContent.none, _invisible))
            NocturneVentKick.ToggleSelect(pc.PlayerId);

        if (NocturneStyle.Painting)
        {
            string nm = pc.Data != null ? pc.Data.PlayerName : "?";
            if (IsHost(pc))
                nm += "  [H]";
            Lab(new Rect(r.x + 50f, r.y, r.width - 140f, r.height), nm, _rowName);
        }
        if (SmallButton(new Rect(r.xMax - 90f, r.y + 3f, 86f, 24f), NocturneText.T("КИК", "KICK"), new Color(0.95f, 0.5f, 0.25f)))
            NocturneToast.Push(NocturneText.T("Вент кик", "Vent kick"), NocturneVentKick.Kick(pc), 2.4f, NocturneNotifyKind.Info);
        y += 32f;
    }

    private void JailRow(float x, ref float y, float w, PlayerControl pc)
    {
        if (RowCull(ref y, 30f, 32f))
            return;
        var r = new Rect(x, y, w, 30f);
        HoverFill(r);
        DrawColorDot(new Rect(r.x + 6f, r.y + 9f, 12f, 12f), pc);

        bool sel = NocturneJail.IsTarget(pc.PlayerId);
        var box = new Rect(r.x + 24f, r.y + 5f, 20f, 20f);
        NocturneStyle.FillRounded(box, sel ? NocturneStyle.Current.Accent : A(Color.black, 0.25f), 5);
        if (sel)
            Lab(box, "✔", _star);
        NocturneStyle.StrokeRounded(box, A(Color.white, 0.14f), 5, 1);
        if (GUI.Button(box, GUIContent.none, _invisible))
            NocturneJail.ToggleTarget(pc.PlayerId);

        if (NocturneStyle.Painting)
        {
            string nm = pc.Data != null ? pc.Data.PlayerName : "?";
            if (IsHost(pc))
                nm += "  [H]";
            Lab(new Rect(r.x + 50f, r.y, r.width - 50f, r.height), nm, _rowName);
        }
        y += 32f;
    }

    private void BlindRow(float x, ref float y, float w, PlayerControl pc)
    {
        if (RowCull(ref y, 30f, 32f))
            return;
        var r = new Rect(x, y, w, 30f);
        HoverFill(r);
        DrawColorDot(new Rect(r.x + 6f, r.y + 9f, 12f, 12f), pc);

        bool sel = NocturneBlind.IsSelected(pc.PlayerId);
        var box = new Rect(r.x + 24f, r.y + 5f, 20f, 20f);
        NocturneStyle.FillRounded(box, sel ? NocturneStyle.Current.Accent : A(Color.black, 0.25f), 5);
        if (sel)
            Lab(box, "✔", _star);
        NocturneStyle.StrokeRounded(box, A(Color.white, 0.14f), 5, 1);
        if (GUI.Button(box, GUIContent.none, _invisible))
            NocturneBlind.ToggleSelect(pc.PlayerId);

        if (NocturneStyle.Painting)
        {
            string nm = pc.Data != null ? pc.Data.PlayerName : "?";
            if (IsHost(pc))
                nm += "  [H]";
            Lab(new Rect(r.x + 50f, r.y, r.width - 140f, r.height), nm, _rowName);
        }

        Color sc = NocturneBlind.IsDark(pc.PlayerId) ? new Color(0.4f, 0.42f, 0.48f) : NocturneBlind.IsBright(pc.PlayerId) ? new Color(0.95f, 0.85f, 0.35f) : new Color(0.5f, 0.55f, 0.62f);
        if (SmallButton(new Rect(r.xMax - 90f, r.y + 3f, 86f, 24f), NocturneBlind.StateName(pc.PlayerId), sc))
            NocturneToast.Push(NocturneText.T("Свет", "Vision"), NocturneBlind.Cycle(pc), 2.2f, NocturneNotifyKind.Info);
        y += 32f;
    }


    private void PetRow(float x, ref float y, float w, PlayerControl pc)
    {
        if (RowCull(ref y, 30f, 32f))
            return;
        var r = new Rect(x, y, w, 30f);
        HoverFill(r);
        DrawColorDot(new Rect(r.x + 6f, r.y + 9f, 12f, 12f), pc);

        if (NocturneStyle.Painting)
        {
            string nm = pc.Data != null ? pc.Data.PlayerName : "?";
            if (IsHost(pc))
                nm += "  [H]";
            Lab(new Rect(r.x + 26f, r.y, r.width - 222f, r.height), nm, _rowName);
        }

        bool fol = NocturnePet.IsFollow(pc.PlayerId);
        Color fc = fol ? new Color(0.4f, 0.7f, 0.95f) : new Color(0.5f, 0.55f, 0.62f);
        if (SmallButton(new Rect(r.xMax - 190f, r.y + 3f, 92f, 24f), fol ? NocturneText.T("СЛЕДОМ ✓", "FOLLOW ✓") : NocturneText.T("СЛЕДОМ", "FOLLOW"), fc))
            NocturneToast.Push(NocturneText.T("Пет", "Pet"), fol ? NocturnePet.Stop2() : NocturnePet.Chase(pc), 2f, NocturneNotifyKind.Info);

        bool on = NocturnePet.IsTarget(pc.PlayerId);
        Color c = on ? new Color(0.4f, 0.7f, 0.95f) : NocturneStyle.Current.Accent;
        if (SmallButton(new Rect(r.xMax - 94f, r.y + 3f, 90f, 24f), on ? NocturneText.T("ГЛАЖУ ✓", "PETTING ✓") : NocturneText.T("ГЛАДИТЬ", "PET"), c))
            NocturneToast.Push(NocturneText.T("Пет", "Pet"), on ? NocturnePet.Stop2() : NocturnePet.Grab(pc), 2f, NocturneNotifyKind.Info);
        y += 32f;
    }

    private void TaskRow(float x, ref float y, float w, PlayerControl pc)
    {
        if (RowCull(ref y, 30f, 32f))
            return;
        var r = new Rect(x, y, w, 30f);
        HoverFill(r);
        DrawColorDot(new Rect(r.x + 6f, r.y + 9f, 12f, 12f), pc);

        if (NocturneStyle.Painting)
        {
            string nm = pc.Data != null ? pc.Data.PlayerName : "?";
            if (IsHost(pc))
                nm += "  [H]";
            Lab(new Rect(r.x + 26f, r.y, r.width - 260f, r.height), nm, _rowName);
        }

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
        DrawColorDot(new Rect(r.x + 6f, r.y + 9f, 12f, 12f), pc);

        bool sel = RideTargets.Has(pc.PlayerId);
        var box = new Rect(r.x + 24f, r.y + 5f, 20f, 20f);
        NocturneStyle.FillRounded(box, sel ? NocturneStyle.Current.Accent : A(Color.black, 0.25f), 5);
        if (sel)
            Lab(box, "✔", _star);
        NocturneStyle.StrokeRounded(box, A(Color.white, 0.14f), 5, 1);
        if (GUI.Button(box, GUIContent.none, _invisible))
            RideTargets.Toggle(pc.PlayerId);

        if (NocturneStyle.Painting)
        {
            string nm = pc.Data != null ? pc.Data.PlayerName : "?";
            if (IsHost(pc))
                nm += "  [H]";
            Lab(new Rect(r.x + 50f, r.y, r.width - 184f, r.height), nm, _rowName);
        }

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
                if (h != null)
                    _hostPc = h.Character;
            }
            catch { }
        }
        return _hostPc != null && _hostPc == pc;
    }

    private void RoleRow(float x, ref float y, float w, PlayerControl pc)
    {
        if (RowCull(ref y, 30f, 32f))
            return;
        var r = new Rect(x, y, w, 30f);
        HoverFill(r);
        DrawColorDot(new Rect(r.x + 6f, r.y + 9f, 12f, 12f), pc);
        Lab(new Rect(r.x + 26f, r.y, r.width - 240f, r.height), pc.Data != null ? pc.Data.PlayerName : "?", _rowName);
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
        DrawColorDot(new Rect(r.x + 6f, r.y + 9f, 12f, 12f), pc);

        bool me = pc == PlayerControl.LocalPlayer;
        bool on = NocturneGodMode.IsGranted(pc.PlayerId);

        if (!me)
        {
            bool mk = NocturneVentTp.IsMarked(pc.PlayerId);
            var box = new Rect(r.x + 24f, r.y + 5f, 20f, 20f);
            NocturneStyle.FillRounded(box, mk ? NocturneStyle.Current.Accent : A(Color.black, 0.25f), 5);
            if (mk)
                Lab(box, "✔", _star);
            NocturneStyle.StrokeRounded(box, A(Color.white, 0.14f), 5, 1);
            if (GUI.Button(box, GUIContent.none, _invisible))
                NocturneVentTp.ToggleMark(pc.PlayerId);
        }

        Lab(new Rect(r.x + 50f, r.y, r.width - 212f, r.height), pc.Data != null ? pc.Data.PlayerName : "?", _rowName);

        if (me)
        {
            Lab(new Rect(r.xMax - 158f, r.y, 154f, r.height), NocturneText.T("это ты", "you"), _muted);
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
        return AmongUsClient.Instance == null ? null : (InnerNetClient)AmongUsClient.Instance;
    }

    private static string ClientInfo(ClientData c)
    {
        string s = string.Empty;
        try
        {
            string lvl = Patches.NocturneJoinLevels.Display(c);
            if (lvl != "?")
                s = NocturneText.T("ур.", "lvl") + lvl;
        }
        catch { }
        try
        {
            if (c.PlatformData != null)
            {
                string pl = Plat(c.PlatformData.Platform);
                if (pl.Length > 0)
                    s = s.Length > 0 ? s + " · " + pl : pl;
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
        MeasureHome(about, CardW(w));
        float ah = _homeAboutH;
        Grp(0);
        Rect b = Card(x, ref y, w, NocturneText.T("О моде", "About"), ah + 5f * 30f + 8f);
        Lab(new Rect(b.x, b.y, b.width, ah), about, _wrapLabel);
        float by = b.y + ah + 8f;
        InfoRow(b.x, ref by, b.width, NocturneText.T("Версия", "Version"), "v" + NocturnePlugin.PluginVersion);
        InfoRow(b.x, ref by, b.width, NocturneText.T("Автор", "Author"), "Kawasaki");
        InfoRow(b.x, ref by, b.width, NocturneText.T("Со-разработчик", "Co-Developer"), "Zahraa");
        InfoRow(b.x, ref by, b.width, NocturneText.T("Контрибьютор", "Contributor"), "YosefUME");
        InfoRow(b.x, ref by, b.width, NocturneText.T("Тестер", "Tester"), "LITAFOM");

        Grp(1);
        b = Card(x, ref y, w, NocturneText.T("Горячие клавиши", "Hotkeys"), 12f * 30f, true);
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
        Lab(b, imp, _wrapLabel);

        Grp(0);
        DrawUpdate(x, ref y, w);

        Grp(0);
        b = Card(x, ref y, w, NocturneText.T("Ссылки", "Links"), 3f * RowH);
        by = b.y;
        LinkRow(b.x, ref by, b.width, NocturneText.T("Сайт", "Website"), "https://onyxmenu.kawas-set.workers.dev");
        LinkRow(b.x, ref by, b.width, "Discord", "https://discord.gg/cP4MrVUfM7");
        LinkRow(b.x, ref by, b.width, "GitHub", "https://github.com/Veltrix-s/OnyxMenu");

        Grp(2);
        b = Card(x, ref y, w, NocturneText.T("Быстрые действия", "Quick actions"), 48f, true);
        if (ActionRow(b, NocturneIcon.Bell, NocturneText.T("Тест уведомления", "Test notification"), NocturneText.T("Проверить", "Test")))
            NocturneToast.Push(NocturneText.T("Nocturne на связи ✓", "Nocturne is live ✓"));
    }

    private void MeasureHome(string about, float w)
    {
        string imp = NocturneText.T(
            "<color=#FFD166>Мод может конфликтовать с другими модами. Перед запуском отключи или удали остальные — меньше вылетов и багов.</color>",
            "<color=#FFD166>The mod may conflict with other mods. Disable or remove other mods before launching to avoid crashes and bugs.</color>");
        if (Mathf.Abs(w - _homeTextW) < 0.5f && ReferenceEquals(about, _homeAbout) && ReferenceEquals(imp, _homeImp))
            return;
        _homeTextW = w;
        _homeAbout = about;
        _homeImp = imp;
        float tw = w - 36f;
        _homeAboutH = _wrapLabel.CalcHeight(new GUIContent(about), tw);
        _homeImpH = _wrapLabel.CalcHeight(new GUIContent(imp), tw);
    }

    private void DrawUpdate(float x, ref float y, float w)
    {
        UpState st = NocturneUpdateCheck.State;
        Rect b = Card(x, ref y, w, NocturneText.T("Обновление", "Update"), 30f + 30f);
        float by = b.y;

        string info;
        switch (st)
        {
            case UpState.Checking:
                info = NocturneText.T("Проверяю…", "Checking…");
                break;
            case UpState.Found:
                info = NocturneText.T("Доступна v", "Version v") + NocturneUpdateCheck.Latest;
                break;
            case UpState.Loading:
                info = NocturneText.T("Качаю…", "Downloading…");
                break;
            case UpState.Done:
                info = NocturneText.T("Готово — перезапусти игру", "Done — restart the game");
                break;
            case UpState.Fail:
                info = NocturneText.T("Ошибка: ", "Error: ") + NocturneUpdateCheck.Err;
                break;
            default:
                info = NocturneText.T("Установлена последняя: v", "Up to date: v") + NocturnePlugin.PluginVersion;
                break;
        }
        Lab(new Rect(b.x + 2f, by, b.width - 2f, 24f), info, _muted);
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
        Lab(new Rect(r.x + 12f, r.y, r.width - 48f, r.height), label, _rowLabel);
        Lab(new Rect(r.x + w - 40f, r.y, 34f, r.height), "↗", _value);
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
        Lab(new Rect(b.x + 2f, by, b.width - 2f, 20f), NocturneText.T("Хост красит любого, не-хост — себя. Пример: /c red", "Host colors anyone, non-host colors self. e.g. /c red"), _muted);
        by += 22f;
        Toggle(b.x, ref by, b.width, NocturneText.T("Команды хоста (/kick /role /start…)", "Host commands (/kick /role /start…)"), NocturneConfig.ChatCmds);
        Lab(new Rect(b.x + 2f, by, b.width - 2f, 20f), NocturneText.T("Пиши /help в чат — список команд.", "Type /help in chat for the list."), _muted);

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
        for (int i = 0; i < _roleClients.Count; i++)
            if (_roleClients[i] != Me())
                wsCount++;
        Grp(1);
        b = Card(x, ref y, w, NocturneText.T("Шепот", "Whisper"), 26f + (wsCount > 0 ? wsCount * 32f : 26f), true);
        by = b.y;
        Lab(new Rect(b.x + 2f, by, b.width - 2f, 22f), NocturneText.T("Или в чате: /w [ник или ID] сообщение", "Or in chat: /w [name or ID] message"), _muted);
        by += 26f;
        if (wsCount == 0)
            Lab(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Нет других игроков.", "No other players."), _muted);
        else
            for (int i = 0; i < _roleClients.Count; i++)
                if (_roleClients[i] != Me())
                    WhisperRow(b.x, ref by, b.width, _roleClients[i]);
    }

    private struct RoleDef
    {
        public string Ru, En;
        public RoleTypes Role;
        public bool Imp;
        public float DetailH;
        public RoleDef(string ru, string en, RoleTypes role, bool imp, float dh)
        {
            Ru = ru;
            En = en;
            Role = role;
            Imp = imp;
            DetailH = dh;
        }
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
        if (hostReady)
            SubBar(x, ref y, w, HostSubsRu, HostSubsEn);
        else
            _subTab[_tab] = 0;

        Grp(0);
        Rect rb = Card(x, ref y, w, NocturneText.T("Власть хоста", "Host powers"), 38f + RowH * 3f);
        float rby = rb.y;
        Lab(new Rect(rb.x + 2f, rby, rb.width - 2f, 34f),
            NocturneText.T("Только хост, для своей катки.", "Host only, for your own game."), _muted);
        rby += 38f;
        Toggle(rb.x, ref rby, rb.width, NocturneText.T("Меня нельзя выгнать", "Immune to voting"), NocturneConfig.VoteImmune);
        Toggle(rb.x, ref rby, rb.width, NocturneText.T("Без репортов и собраний", "No reports & meetings"), NocturneConfig.NoReports);
        string[] ventModes = NocturneText.IsRussian ? VentModesRu : VentModesEn;

        int vm = Mathf.Clamp(NocturneConfig.VentMode.Value, 0, 3);
        if (CycleRow(rb.x, ref rby, rb.width, NocturneText.T("Кто может вентить", "Who may vent"), ventModes[vm]))
            NocturneConfig.VentMode.Value = (vm + 1) % 4;

        if (!hostReady)
        {
            Rect hb = Card(x, ref y, w, NocturneText.T("Правила лобби", "Lobby rules"), RowH + 24f);
            Lab(new Rect(hb.x + 4f, hb.y, hb.width - 8f, 44f),
                NocturneText.T("Доступно только хосту в лобби. Создай комнату и стань хостом.",
                           "Host in lobby only. Create a room and become host."), _muted);
            return;
        }

        Grp(0);
        DrawPresets(x, ref y, w);

        Grp(1);
        Rect b = Card(x, ref y, w, NocturneText.T("Основное", "Basics"), 2f * RowH + 302f);
        float by = b.y;

        string[] maps = MapsShort;
        int mp = Mathf.Clamp(NocturneLobbySettings.Map(), 0, 5);
        if (CycleRow(b.x, ref by, b.width, NocturneText.T("Карта", "Map"), maps[mp]))
            NocturneLobbySettings.SetMap((mp + 1) % 6);

        int pl = NocturneLobbySettings.Players();
        int nPl = SliderIntVal(b.x, ref by, b.width, NocturneText.T("Игроки", "Players"), pl, 4, 15, "");
        if (nPl != pl)
            NocturneLobbySettings.SetPlayers(nPl);

        int im = NocturneLobbySettings.Imps();
        int nIm = SliderIntVal(b.x, ref by, b.width, NocturneText.T("Импостеры", "Impostors"), im, 1, 3, "");
        if (nIm != im)
            NocturneLobbySettings.SetImps(nIm);

        float kc = NocturneLobbySettings.KillCd();
        float nKc = SliderFloatVal(b.x, ref by, b.width, NocturneText.T("Кулдаун убийства", "Kill cooldown"), kc, 0f, 60f, 0.1f, "0.0", NocturneText.T("с", "s"));
        if (Mathf.Abs(nKc - kc) > 0.001f)
            NocturneLobbySettings.SetKillCd(nKc);

        string[] dists = NocturneText.IsRussian ? DistsRu : DistsEn;
        int kd = Mathf.Clamp(NocturneLobbySettings.KillDist(), 0, 2);
        if (CycleRow(b.x, ref by, b.width, NocturneText.T("Дистанция убийств", "Kill distance"), dists[kd]))
            NocturneLobbySettings.SetKillDist((kd + 1) % 3);

        int sp = Mathf.RoundToInt(NocturneLobbySettings.Speed() * 100f);
        int nSp = SliderIntVal(b.x, ref by, b.width, NocturneText.T("Скорость", "Speed"), sp, 25, 300, "%");
        if (nSp != sp)
            NocturneLobbySettings.SetSpeed(nSp / 100f);

        int cv = Mathf.RoundToInt(NocturneLobbySettings.CrewVis() * 100f);
        int nCv = SliderIntVal(b.x, ref by, b.width, NocturneText.T("Обзор мирных", "Crew vision"), cv, 25, 500, "%");
        if (nCv != cv)
            NocturneLobbySettings.SetCrewVis(nCv / 100f);

        int iv = Mathf.RoundToInt(NocturneLobbySettings.ImpVis() * 100f);
        int nIv = SliderIntVal(b.x, ref by, b.width, NocturneText.T("Обзор предов", "Impostor vision"), iv, 25, 500, "%");
        if (nIv != iv)
            NocturneLobbySettings.SetImpVis(nIv / 100f);

        Grp(1);
        b = Card(x, ref y, w, NocturneText.T("Собрания и голосование", "Meetings & voting"), 2f * RowH + 210f);
        by = b.y;

        int me = NocturneLobbySettings.Meetings();
        int nMe = SliderIntVal(b.x, ref by, b.width, NocturneText.T("Экстренных собраний", "Emergency meetings"), me, 0, 15, "");
        if (nMe != me)
            NocturneLobbySettings.SetMeetings(nMe);

        int mc = NocturneLobbySettings.MeetingCd();
        int nMc = SliderIntVal(b.x, ref by, b.width, NocturneText.T("КД собрания", "Meeting cd"), mc, 0, 60, NocturneText.T("с", "s"));
        if (nMc != mc)
            NocturneLobbySettings.SetMeetingCd(nMc);

        int di = NocturneLobbySettings.Discuss();
        int nDi = SliderIntVal(b.x, ref by, b.width, NocturneText.T("Обсуждение", "Discussion"), di, 0, 120, NocturneText.T("с", "s"));
        if (nDi != di)
            NocturneLobbySettings.SetDiscuss(nDi);

        int vo = NocturneLobbySettings.Voting();
        int nVo = SliderIntVal(b.x, ref by, b.width, NocturneText.T("Голосование", "Voting"), vo, 0, 300, NocturneText.T("с", "s"));
        if (nVo != vo)
            NocturneLobbySettings.SetVoting(nVo);

        bool an = NocturneLobbySettings.Anon();
        bool nAn = ToggleVal(b.x, ref by, b.width, NocturneText.T("Анонимные голоса", "Anonymous votes"), an);
        if (nAn != an)
            NocturneLobbySettings.SetAnon(nAn);

        bool cf = NocturneLobbySettings.Confirm();
        bool nCf = ToggleVal(b.x, ref by, b.width, NocturneText.T("Подтверждать выброс", "Confirm ejects"), cf);
        if (nCf != cf)
            NocturneLobbySettings.SetConfirm(nCf);

        Grp(1);
        b = Card(x, ref y, w, NocturneText.T("Задания", "Tasks"), RowH + 194f);
        by = b.y;

        string[] taskBars = NocturneText.IsRussian ? TaskBarsRu : TaskBarsEn;
        int tb = Mathf.Clamp(NocturneLobbySettings.TaskBar(), 0, 2);
        if (CycleRow(b.x, ref by, b.width, NocturneText.T("Шкала заданий", "Task bar"), taskBars[tb]))
            NocturneLobbySettings.SetTaskBar((tb + 1) % 3);

        int co = NocturneLobbySettings.Common();
        int nCo = SliderIntVal(b.x, ref by, b.width, NocturneText.T("Общие задания", "Common tasks"), co, 0, 8, "");
        if (nCo != co)
            NocturneLobbySettings.SetCommon(nCo);

        int lo = NocturneLobbySettings.Long();
        int nLo = SliderIntVal(b.x, ref by, b.width, NocturneText.T("Длинные задания", "Long tasks"), lo, 0, 8, "");
        if (nLo != lo)
            NocturneLobbySettings.SetLong(nLo);

        int sh = NocturneLobbySettings.Short();
        int nSh = SliderIntVal(b.x, ref by, b.width, NocturneText.T("Короткие задания", "Short tasks"), sh, 0, 12, "");
        if (nSh != sh)
            NocturneLobbySettings.SetShort(nSh);

        bool vi = NocturneLobbySettings.Visual();
        bool nVi = ToggleVal(b.x, ref by, b.width, NocturneText.T("Визуальные задания", "Visual tasks"), vi);
        if (nVi != vi)
            NocturneLobbySettings.SetVisual(nVi);

        float rolesBody = 24f + HostRoles.Length * RowH;
        foreach (RoleDef d in HostRoles)
            if (_roleOpen.Contains((int)d.Role))
                rolesBody += d.DetailH;
        Grp(2);
        b = Card(x, ref y, w, NocturneText.T("Роли (кол-во/шанс)", "Roles (count/chance)"), rolesBody, true);
        by = b.y;
        Lab(new Rect(b.x + b.width - 190f, by, 76f, 20f), NocturneText.T("Кол-во", "Count"), _centerMuted);
        Lab(new Rect(b.x + b.width - 100f, by, 96f, 20f), NocturneText.T("Шанс", "Chance"), _centerMuted);
        by += 24f;
        Color crew = new Color(0.42f, 0.80f, 0.72f);
        Color imp = new Color(0.92f, 0.38f, 0.38f);
        foreach (RoleDef d in HostRoles)
        {
            bool open = RoleRow(b.x, ref by, b.width, d, d.Imp ? imp : crew);
            if (open)
                DrawRoleDetails(b.x, ref by, b.width, d.Role);
        }
    }

    private void DrawPresets(float x, ref float y, float w)
    {
        var names = NocturneLobbyPresets.Names();
        int pn = names.Count;
        Rect b = Card(x, ref y, w, NocturneText.T("Пресеты", "Presets"), 34f + (pn > 0 ? pn * RowH : RowH) + 6f, true);
        float by = b.y;

        _presetName = CustomText(new Rect(b.x + 2f, by, b.width - 128f, 26f), _presetName ?? "", "nocturnePreset");
        if (string.IsNullOrEmpty(_presetName) && _textFocus != "nocturnePreset")
            Lab(new Rect(b.x + 10f, by, b.width - 140f, 26f), NocturneText.T("Имя пресета…", "Preset name…"), _muted);
        if (SmallButton(new Rect(b.x + b.width - 120f, by + 1f, 118f, 24f), NocturneText.T("СОХРАНИТЬ", "SAVE"), NocturneStyle.Current.Accent))
        {
            NocturneToast.Push(NocturneText.T("Пресет", "Preset"), NocturneLobbyPresets.Save(_presetName), 2.5f, NocturneNotifyKind.Info);
            _presetName = "";
            _textFocus = null;
        }
        by += 34f;

        if (pn == 0)
        {
            Lab(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Пусто. Задай имя и сохрани текущие настройки.", "Empty. Name it and save current settings."), _muted);
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
            Lab(new Rect(r.x + 26f, r.y, r.width - 70f, r.height), name, _rowLabel);
            Lab(new Rect(r.x + r.width - 80f, r.y, 44f, r.height), "▸", _value);
            var del = new Rect(r.xMax - 28f, r.y + (r.height - 22f) / 2f, 24f, 22f);
            if (ArrowButton(del, "✕"))
                NocturneLobbyPresets.Delete(name);
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
            if (open)
                _roleOpen.Remove(key);
            else
                _roleOpen.Add(key);
            open = !open;
        }
        Lab(new Rect(r.x + 6f, r.y, 16f, r.height), open ? "▾" : "▸", _centerMuted);
        NocturneStyle.FillRounded(new Rect(r.x + 24f, r.y + (r.height - 8f) / 2f, 8f, 8f), team, 4);
        Lab(new Rect(r.x + 38f, r.y, Mathf.Max(40f, r.width - 238f), r.height), NocturneText.T(d.Ru, d.En), _rowLabel);

        int cnt = NocturneLobbySettings.RoleNum(d.Role);
        int chc = NocturneLobbySettings.RoleChance(d.Role);
        int nCnt = Stepper(new Rect(r.xMax - 190f, r.y, 76f, r.height), cnt, 0, 15, 1, "");
        int nChc = Stepper(new Rect(r.xMax - 100f, r.y, 96f, r.height), chc, 0, 100, 10, "%");
        if (nCnt != cnt || nChc != chc)
            NocturneLobbySettings.SetRole(d.Role, nCnt, nChc);
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
        if (nv != v)
            set(nv);
    }

    [HideFromIl2Cpp]
    private void RoleBool(float x, ref float y, float w, string ru, string en, Func<bool> get, Action<bool> set)
    {
        bool v = get();
        bool nv = ToggleVal(x + 14f, ref y, w - 14f, NocturneText.T(ru, en), v);
        if (nv != v)
            set(nv);
    }

    private int Stepper(Rect r, int value, int min, int max, int step, string suffix)
    {
        value = Mathf.Clamp(value, min, max);
        var lb = new Rect(r.x, r.y + (r.height - 20f) / 2f, 20f, 20f);
        var rb = new Rect(r.xMax - 20f, r.y + (r.height - 20f) / 2f, 20f, 20f);
        if (ArrowButton(lb, "◂"))
            value = Mathf.Clamp(value - step, min, max);
        if (ArrowButton(rb, "▸"))
            value = Mathf.Clamp(value + step, min, max);
        Lab(new Rect(lb.xMax, r.y, rb.x - lb.xMax, r.height), value + suffix, _valueC);
        return Mathf.Clamp(value, min, max);
    }

    private void DrawLobbyTab(float x, ref float y, float w)
    {
        SubBar(x, ref y, w, LobbySubsRu, LobbySubsEn);

        Grp(0);
        Rect b = Card(x, ref y, w, NocturneText.T("Старт", "Start"), 4f * RowH);
        float by = b.y;
        Toggle(b.x, ref by, b.width, NocturneText.T("Разблок. кнопку Старт", "Unlock Start button"), NocturneConfig.AlwaysUnlockStartButton);
        Toggle(b.x, ref by, b.width, NocturneText.T("Старт по Enter", "Start on Enter"), NocturneConfig.QuickStartOnEnter);
        Toggle(b.x, ref by, b.width, NocturneText.T("Мгновенный старт (Enter)", "Instant start (Enter)"), NocturneConfig.InstantStartOnEnter);
        Toggle(b.x, ref by, b.width, NocturneText.T("Авто-возврат в лобби", "Auto-return to lobby"), NocturneConfig.AutoReturnLobbyAfterMatch);

        Grp(0);
        b = Card(x, ref y, w, NocturneText.T("Прочее", "Misc"), 3f * RowH + 56f);
        by = b.y;
        Toggle(b.x, ref by, b.width, NocturneText.T("Сенсорная кнопка меню", "Touch menu button"), NocturneConfig.MenuButton);
        Toggle(b.x, ref by, b.width, NocturneText.T("Отключить музыку лобби", "Mute lobby music"), NocturneConfig.MuteLobbyMusic);
        Toggle(b.x, ref by, b.width, NocturneText.T("Расширенный браузер лобби", "Rich lobby browser"), NocturneConfig.RichLobbyRows);
        _lobbySearch = CustomText(new Rect(b.x + 2f, by, b.width - 92f, 26f), _lobbySearch ?? NocturneConfig.LobbySearchHost.Value ?? "", "lobbySearch");
        if (SmallButton(new Rect(b.x + b.width - 88f, by + 1f, 86f, 24f), NocturneText.T("ПОИСК", "SEARCH"), NocturneStyle.Current.Accent))
            NocturneConfig.LobbySearchHost.Value = (_lobbySearch ?? "").Trim();
        by += 30f;
        Lab(new Rect(b.x + 2f, by, b.width - 2f, 22f), NocturneText.T("Фильтр браузера по нику хоста. Пусто — все лобби.", "Browser filter by host name. Empty = all lobbies."), _muted);

        Grp(1);
        b = Card(x, ref y, w, NocturneText.T("Автохост", "Auto-host"), 7f * RowH + 100f, true);
        by = b.y;
        Toggle(b.x, ref by, b.width, NocturneText.T("Включить автохост", "Enable auto-host"), NocturneConfig.AutoHostEnabled);
        Toggle(b.x, ref by, b.width, NocturneText.T("Мгновенный старт", "Instant start"), NocturneConfig.AutoHostInstantStart);
        Toggle(b.x, ref by, b.width, NocturneText.T("Возврат в лобби после матча", "Return to lobby after match"), NocturneConfig.AutoHostReturnAfterMatch);
        Toggle(b.x, ref by, b.width, NocturneText.T("Ждать загрузку игроков", "Wait for players to load"), NocturneConfig.AutoHostWaitLoadedPlayers);
        Toggle(b.x, ref by, b.width, NocturneText.T("Форс в последнюю минуту", "Force in last minute"), NocturneConfig.AutoHostForceLastMinute);
        Toggle(b.x, ref by, b.width, NocturneText.T("Уведомления автохоста", "Auto-host notifications"), NocturneConfig.AutoHostNotifications);
        bool gmBefore = NocturneConfig.GameMaster.Value;
        Toggle(b.x, ref by, b.width, NocturneText.T("Мастер игры", "Game master"), NocturneConfig.GameMaster);
        if (NocturneConfig.GameMaster.Value && !gmBefore)
            NocturneConfig.GhostAfterStart.Value = false;
        SliderInt(b.x, ref by, b.width, NocturneText.T("Минимум игроков", "Min players"), NocturneConfig.AutoHostMinPlayers, 1, 15);
        SliderInt(b.x, ref by, b.width, NocturneText.T("Задержка старта, с", "Start delay, s"), NocturneConfig.AutoHostStartDelaySeconds, 0, 180);

        Grp(1);
        b = Card(x, ref y, w, NocturneText.T("Манекены (хост)", "Dummies (host)"), 4f * RowH + 40f, true);
        by = b.y;
        Toggle(b.x, ref by, b.width, NocturneText.T("Включить манекенов", "Enable dummies"), NocturneConfig.DummyEnabled);
        Toggle(b.x, ref by, b.width, NocturneText.T("Делают таски", "Do tasks"), NocturneConfig.DummyDoTasks);
        Toggle(b.x, ref by, b.width, NocturneText.T("Чинят саботаж", "Fix sabotage"), NocturneConfig.DummyFixSabotage);
        Toggle(b.x, ref by, b.width, NocturneText.T("Репорт тел + чат + голос", "Report bodies + chat + vote"), NocturneConfig.DummyReportBodies);
        Lab(new Rect(b.x + 2f, by, b.width - 160f, 24f), $"{NocturneText.T("Клавиша спавна", "Spawn key")}: <b>{KeyName(NocturneConfig.DummyKey)}</b>", _muted);
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
            if (nsc != sc)
                NocturneConfig.SeekerCount.Value = nsc;
        }
        Toggle(b.x, ref by, b.width, NocturneText.T("Пред без форы (прятки)", "Seeker: skip head start"), NocturneConfig.SeekerInstantStart);
        Toggle(b.x, ref by, b.width, NocturneText.T("4 импостера (≥9 игроков)", "4 impostors (≥9 players)"), NocturneConfig.FourImpostors);
        Toggle(b.x, ref by, b.width, NocturneText.T("Снять лимиты настроек", "Unlock option limits"), NocturneConfig.LooseHostOptions);
        Toggle(b.x, ref by, b.width, NocturneText.T("Шаг настроек 0.1", "Option step 0.1"), NocturneConfig.ForceMinValues);
        Toggle(b.x, ref by, b.width, NocturneText.T("Копировать код при дисконнекте", "Copy code on disconnect"), NocturneConfig.CopyCodeOnDisconnect);

        Grp(1);
        b = Card(x, ref y, w, NocturneText.T("Фейк-карта (хост)", "Fake map (host)"), RowH + 30f);
        by = b.y;
        string[] fmNames = MapsFull;
        int fmi = Mathf.Clamp(NocturneConfig.FakeMapId.Value, 0, fmNames.Length - 1);
        if (CycleRow(b.x, ref by, b.width, NocturneText.T("Карта", "Map"), fmNames[fmi]))
            NocturneConfig.FakeMapId.Value = (fmi + 1) % fmNames.Length;
        bool fmOn = NocturneFakeMap.Active;
        if (SmallButton(new Rect(b.x + 2f, by, 176f, 24f), fmOn ? NocturneText.T("ВЫКЛ ФЕЙК-КАРТУ", "FAKE MAP OFF") : NocturneText.T("ВКЛ ФЕЙК-КАРТУ", "FAKE MAP ON"), fmOn ? new Color(0.9f, 0.4f, 0.4f) : NocturneStyle.Current.Accent))
        {
            if (fmOn)
                NocturneFakeMap.DisableAndRestoreLobby();
            else
                NocturneFakeMap.Enable(NocturneConfig.FakeMapId.Value);
        }

        int wt = Mathf.Clamp(NocturneConfig.LobbyWeather.Value, 0, 4);
        Grp(0);
        b = Card(x, ref y, w, NocturneText.T("Оформление лобби", "Lobby appearance"), 5f * RowH + 12f);
        by = b.y;
        Toggle(b.x, ref by, b.width, NocturneText.T("Тёмная тема лобби", "Dark lobby theme"), NocturneConfig.LobbyTheme);
        Toggle(b.x, ref by, b.width, NocturneText.T("Своя картинка в главном меню", "Own main menu picture"), NocturneConfig.MainMenuArt);
        Toggle(b.x, ref by, b.width, NocturneText.T("Анимация панели", "Panel animation"), NocturneConfig.LobbyAnims);
        Toggle(b.x, ref by, b.width, NocturneText.T("Анимация кнопки старта", "Start button animation"), NocturneConfig.LobbyStartAnim);
        Toggle(b.x, ref by, b.width, NocturneText.T("Кастом кнопка Старт", "Custom Start button"), NocturneConfig.StartBtnGlass);

        Grp(0);
        b = Card(x, ref y, w, NocturneText.T("Погода лобби", "Lobby weather"), RowH + 24f + (wt > 0 ? 50f : 0f));
        by = b.y;
        string[] wNames = NocturneText.IsRussian ? WeatherRu : WeatherEn;

        if (CycleRow(b.x, ref by, b.width, NocturneText.T("Погода", "Weather"), wNames[wt]))
            NocturneConfig.LobbyWeather.Value = (wt + 1) % 5;
        if (wt > 0)
            SliderInt(b.x, ref by, b.width, NocturneText.T("Частиц", "Particles"), NocturneConfig.LobbySnowAmount, 10, 400);
        Lab(new Rect(b.x + 2f, by, b.width - 2f, 22f), NocturneText.T("Погоду видишь только ты.", "Only you see the weather."), _muted);

        LobbyHistoryCard(x, ref y, w);

        Grp(2);
        b = Card(x, ref y, w, NocturneText.T("Клоны лобби", "Lobby clones"), 6f * RowH + 482f + (NocturneConfig.CloneFormationAnim.Value ? 50f : 0f), true);
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

        string[] formNames = NocturneText.IsRussian ? FormNamesRu : FormNamesEn;
        int cfi = Mathf.Clamp(NocturneConfig.CloneFormation.Value, 0, formNames.Length - 1);
        if (CycleRow(b.x, ref by, b.width, NocturneText.T("Формация", "Formation"), formNames[cfi]))
            NocturneConfig.CloneFormation.Value = (cfi + 1) % formNames.Length;
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
        b = Card(x, ref y, w, NocturneText.T("Сетевые клоны", "Networked clones"), (ncReady ? 4f * RowH + 208f : 26f) + 32f, true);
        by = b.y;
        if (!ncReady)
        {
            Lab(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Только хост в лобби.", "Host in lobby only."), _muted);
            by += 26f;
        }
        else
        {
            Lab(new Rect(b.x + 2f, by, b.width - 2f, 22f), NocturneText.T("Копии тебя, видят все. Убираются на старте.", "Copies of you, everyone sees. Cleared on start."), _muted);
            by += 26f;
            Toggle(b.x, ref by, b.width, NocturneText.T("Режим клика (ЛКМ=спавн, ПКМ=удал.)", "Click mode (LMB=spawn, RMB=remove)"), NocturneConfig.NetCloneMode);

            PlayerControl nsrc = NetSrc();
            bool mine = nsrc == null || nsrc == PlayerControl.LocalPlayer;
            string sname = mine ? NocturneText.T("Я", "Me") : (nsrc.Data != null ? nsrc.Data.PlayerName : "?");
            if (CycleRow(b.x, ref by, b.width, NocturneText.T("Внешность", "Look like"), sname))
                NextNetSrc();
            if (!mine)
                DrawColorDot(new Rect(b.x + b.width - 132f, by - RowH + 11f, 12f, 12f), nsrc.Data != null && nsrc.Data.DefaultOutfit != null ? nsrc.Data.DefaultOutfit.ColorId : 0);

            int ncf = Mathf.Clamp(NocturneConfig.CloneFormation.Value, 0, formNames.Length - 1);
            if (CycleRow(b.x, ref by, b.width, NocturneText.T("Формация", "Formation"), formNames[ncf]))
                NocturneConfig.CloneFormation.Value = (ncf + 1) % formNames.Length;
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
            if (NocturneTwins.Queued > 0)
                ncInfo += $"   {NocturneText.T("в очереди", "queued")}: <b>{NocturneTwins.Queued}</b>";
            if (NocturneTwins.Figures > 0)
                ncInfo += $"   {NocturneText.T("фигур", "figures")}: <b>{NocturneTwins.Figures}</b>";
            Lab(new Rect(b.x + 2f, by, b.width - 2f, 24f), ncInfo, _muted);
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
        string[] mapNames = MapsAlt;
        int ms = Mathf.Clamp(_mapSel, 0, 5);
        if (CycleRow(b.x, ref by, b.width, NocturneText.T("Карта", "Map"), mapNames[ms]))
            _mapSel = (ms + 1) % 6;
        float mcw = (b.width - 10f) / 2f;
        if (SmallButton(new Rect(b.x, by, mcw, 26f), NocturneText.T("УБРАТЬ КАРТУ", "DESPAWN MAP"), new Color(0.9f, 0.4f, 0.4f)))
            NocturneToast.Push(NocturneText.T("Карта", "Map"), NocturneLobbyTools.DespawnMap(), 2.5f, NocturneNotifyKind.Warning);
        if (SmallButton(new Rect(b.x + mcw + 10f, by, mcw, 26f), NocturneText.T("СПАВН КАРТЫ", "SPAWN MAP"), NocturneStyle.Current.Accent))
            NocturneToast.Push(NocturneText.T("Карта", "Map"), NocturneLobbyTools.SpawnMap(ms), 2.5f, NocturneNotifyKind.Success);

        IReadOnlyList<NocturneColorReservations.Entry> res = NocturneColorReservations.All();
        Grp(1);
        b = Card(x, ref y, w, NocturneText.T("Резерв цветов", "Color reservations"), 2f * RowH + (res.Count > 0 ? res.Count * 30f : 26f), true);
        by = b.y;
        Toggle(b.x, ref by, b.width, NocturneText.T("Закреплять цвет (хост)", "Reserve color (host)"), NocturneConfig.ColorReservationsEnabled);
        if (SmallButton(new Rect(b.x + 2f, by, 260f, 24f), NocturneText.T("ЗАРЕЗЕРВИРОВАТЬ ВЫБРАННОГО", "RESERVE SELECTED"), NocturneStyle.Current.Accent))
            ReserveSelectedColor();
        by += 30f;
        if (res.Count == 0)
            Lab(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Пусто. Выбор игрока — ЛКМ (нужен «Выбор мышью»).", "Empty. Select a player with LMB (needs Mouse select)."), _muted);
        else
            for (int i = 0; i < res.Count; i++)
            {
                NocturneColorReservations.Entry en = res[i];
                var rr = new Rect(b.x, by, b.width, 28f);
                HoverFill(rr);
                DrawColorDot(new Rect(rr.x + 6f, rr.y + 8f, 12f, 12f), en.ColorId);
                if (NocturneStyle.Painting)
                    Lab(new Rect(rr.x + 26f, rr.y, rr.width - 66f, rr.height), $"<b>{en.Name}</b>   <color=#8A94AC><size=11>{en.Fc}</size></color>", _rowName);
                if (SmallButton(new Rect(rr.xMax - 38f, rr.y + 3f, 34f, 22f), "✕", new Color(0.9f, 0.4f, 0.4f)))
                {
                    NocturneColorReservations.Remove(en.Fc);
                    break;
                }
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

    private static readonly string[] PlayersSubsRu = { "Карточка", "Инфо", "Действия" };
    private static readonly string[] PlayersSubsEn = { "Card", "Info", "Actions" };

    private static readonly string[] VisualSubsRu = { "Камера", "Облик", "Эффекты" };
    private static readonly string[] VisualSubsEn = { "Camera", "Look", "Effects" };

    private static readonly string[] SettingsSubsRu = { "Интерфейс", "Клавиши", "Прочее" };
    private static readonly string[] SettingsSubsEn = { "Interface", "Keys", "Other" };

    private static readonly string[] HostSubsRu = { "Власть", "Настройки", "Роли" };
    private static readonly string[] HostSubsEn = { "Powers", "Options", "Roles" };

    private string[] PinnedSubs()
    {
        if (!string.IsNullOrEmpty(_search) || _tab == FavTab)
            return null;

        bool ru = NocturneText.IsRussian;
        switch (_tab)
        {
            case 0: return ru ? HomeSubsRu : HomeSubsEn;
            case 1: return ru ? QolSubsRu : QolSubsEn;
            case 2: return ru ? LobbySubsRu : LobbySubsEn;
            case 3: return ru ? VisualSubsRu : VisualSubsEn;
            case 4: return ru ? PlayersSubsRu : PlayersSubsEn;
            case 5: return ru ? CheatSubsRu : CheatSubsEn;
            case 6: return ru ? GuardSubsRu : GuardSubsEn;
            case 8: return NocturneLobbySettings.Ready() ? (ru ? HostSubsRu : HostSubsEn) : null;
            case 9: return ru ? SettingsSubsRu : SettingsSubsEn;
        }
        return null;
    }

    private int SubBar(float x, ref float y, float w, string[] ru, string[] en)
        => SubBar(x, ref y, w, NocturneText.IsRussian ? ru : en);

    private int SubBar(float x, ref float y, float w, string[] names)
    {
        int cur = Mathf.Clamp(_subTab[_tab], 0, names.Length - 1);
        _subTab[_tab] = cur;

        if (_subPinned)
            return cur;

        if (NocturneStyle.Lite)
        {
            float segW = w / names.Length;
            for (int i = 0; i < names.Length; i++)
            {
                Color bg = GUI.backgroundColor;
                if (cur == i)
                    GUI.backgroundColor = new Color(0.5f, 0.5f, 0.5f);
                if (GUI.Button(new Rect(x + i * segW, y, segW - 2f, 26f), names[i]) && cur != i)
                {
                    _subTab[_tab] = i;
                    _scroll = 0f;
                    _scrollPending = 0f;
                }
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
            if (Cull(bar.y, bar.height))
                continue;
            float segW = bar.width / cnt;
            bool paint = NocturneStyle.Painting;

            if (paint)
            {
                NocturneStyle.FillRounded(bar, A(Color.white, 0.04f), 7);
                NocturneStyle.StrokeRounded(bar, A(Color.white, 0.07f), 7, 1);
            }

            for (int c = 0; c < cnt; c++)
            {
                int i = from + c;
                var seg = new Rect(bar.x + c * segW, bar.y, segW, bar.height);
                bool on = cur == i;
                if (paint)
                {
                    if (on)
                        NocturneStyle.FillRounded(new Rect(seg.x + 2f, seg.y + 2f, seg.width - 4f, seg.height - 4f), A(p.Accent, 0.22f), 6);
                    else if (c > 0)
                        NocturneStyle.Fill(new Rect(seg.x, seg.y + 5f, 1f, seg.height - 10f), A(Color.white, 0.06f));
                    _smallBtn.normal.textColor = on ? Color.Lerp(p.Accent, Color.white, 0.55f) : p.Muted;
                    Lab(seg, names[i], _smallBtn);
                }
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
            case 1:
                CheatsPlayers(x, ref y, w);
                break;
            case 2:
                CheatsSelf(x, ref y, w);
                break;
            case 3:
                CheatsMap(x, ref y, w);
                break;
            case 4:
                CheatsTasks(x, ref y, w);
                break;
            case 5:
                CheatsChat(x, ref y, w);
                break;
            default:
                CheatsMeetings(x, ref y, w);
                break;
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
        for (int i = 0; i < _roleClients.Count; i++)
            if (_roleClients[i] != Me())
                vkCount++;
        float vkBody = 30f * 5f + RowH * 3f + (vkCount > 0 ? vkCount * 32f + 22f : 26f);
        Rect b = Card(x, ref y, w, NocturneText.T("Войткик", "Votekick"), vkBody);
        float by = b.y;
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
            Lab(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Нет других игроков.", "No other players."), _muted);
        else
        {
            Lab(new Rect(b.x + 2f, by, b.width - 2f, 20f), NocturneText.T("Выборочно — отметь «АВТО» у нужных:", "Selective — mark AUTO on players:"), _muted);
            by += 22f;
            for (int i = 0; i < _roleClients.Count; i++)
                if (_roleClients[i] != Me())
                    VotekickRow(b.x, ref by, b.width, _roleClients[i]);
        }

        List<string> pend = NocturneJudgeOverrule.Lines;
        int jTotal = NocturneJudgeOverrule.Total;
        float jBody = RowH + 24f + (pend.Count > 0 ? pend.Count * 20f + 4f : 22f) + 30f + (jTotal > 0 ? 22f : 0f);
        b = Card(x, ref y, w, NocturneText.T("Судья: оверрул", "Judge: overrule"), jBody);
        by = b.y;
        Toggle(b.x, ref by, b.width, NocturneText.T("Следить за оверрулами", "Watch overrules"), NocturneConfig.JudgeWatch);
        Lab(new Rect(b.x + 2f, by, b.width - 2f, 22f),
            NocturneText.T("Очередь видит хост — цель судьи до удара молотка.", "The host sees the queue — the judge's target before the gavel."), _muted);
        by += 24f;
        if (pend.Count == 0)
        {
            Lab(new Rect(b.x + 2f, by, b.width - 2f, 20f), NocturneText.T("Оверрулов нет.", "No overrules."), _muted);
            by += 22f;
        }
        else
        {
            for (int i = 0; i < pend.Count; i++)
            {
                Lab(new Rect(b.x + 2f, by, b.width - 2f, 20f), pend[i], _rowName);
                by += 20f;
            }
            by += 4f;
        }
        if (SmallButton(new Rect(b.x, by, b.width, 26f), NocturneText.T("СБРОСИТЬ ОЧЕРЕДЬ (ХОСТ)", "CLEAR QUEUE (HOST)"), new Color(0.5f, 0.55f, 0.62f)))
            NocturneToast.Push(NocturneText.T("Судья", "Judge"), NocturneJudgeOverrule.ClearAll(), 2f, NocturneNotifyKind.Info);
        by += 30f;
        if (jTotal > 0)
            Lab(new Rect(b.x + 2f, by, b.width - 2f, 20f), NocturneText.T("Оверрулов за матч: ", "Overrules this match: ") + jTotal, _muted);

        b = Card(x, ref y, w, NocturneText.T("Собрание: свободный ход", "Meeting: free roam"), inMatch ? 28f + 26f + 30f : 26f);
        by = b.y;
        if (!inMatch)
            Lab(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Только в матче.", "In match only."), _muted);
        else
        {
            Lab(new Rect(b.x + 2f, by, b.width - 2f, 24f),
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
        for (int i = 0; i < _roleClients.Count; i++)
            if (_roleClients[i] != Me())
                loopN++;

        Rect b = Card(x, ref y, w, NocturneText.T("Цикл смерти", "Murder loop"), inMatch ? 38f + (loopN > 0 ? loopN * 32f : 26f) : 26f, true);
        float by = b.y;
        if (!inMatch)
            Lab(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Только в матче.", "In match only."), _muted);
        else
        {
            Lab(new Rect(b.x + 2f, by, b.width - 2f, 34f),
                NocturneText.T("Только хост, только для своих — в пабликах забанят.", "Host only, friends only — publics will ban you."), _muted);
            by += 38f;
            if (loopN == 0)
                Lab(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Нет других игроков.", "No other players."), _muted);
            else
                for (int i = 0; i < _roleClients.Count; i++)
                    if (_roleClients[i] != Me())
                        LoopRow(b.x, ref by, b.width, _roleClients[i]);
        }

        _roleClients.Clear();
        CollectPlayers(_roleClients);
        int killN = 0;
        for (int i = 0; i < _roleClients.Count; i++)
            if (_roleClients[i] != Me())
                killN++;

        b = Card(x, ref y, w, NocturneText.T("Мгновенный килл", "Instant kill"), inMatch ? 68f + (killN > 0 ? killN * 32f : 26f) : 26f, true);
        by = b.y;
        if (!inMatch)
            Lab(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Только в матче.", "In match only."), _muted);
        else
        {
            Lab(new Rect(b.x + 2f, by, b.width - 2f, 34f),
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
                Lab(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Нет других игроков.", "No other players."), _muted);
            else
                for (int i = 0; i < _roleClients.Count; i++)
                    if (_roleClients[i] != Me())
                        KillRow(b.x, ref by, b.width, _roleClients[i]);
        }

        _roleClients.Clear();
        CollectPlayers(_roleClients);
        int vkN = 0;
        for (int i = 0; i < _roleClients.Count; i++)
            if (_roleClients[i] != Me())
                vkN++;

        float vkkBody = vkN > 0 ? 88f + vkN * 32f : 54f;
        b = Card(x, ref y, w, NocturneText.T("Вент кик", "Vent kick"), inMatch ? vkkBody : 26f, true);
        by = b.y;
        if (!inMatch)
            Lab(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Только в матче.", "In match only."), _muted);
        else
        {
            Lab(new Rect(b.x + 2f, by, b.width - 2f, 24f),
                NocturneText.T("Вент-кик.", "Vent kick."), _muted);
            by += 28f;
            if (vkN == 0)
                Lab(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Нет других игроков.", "No other players."), _muted);
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
                    if (_roleClients[i] != Me())
                        VentKickRow(b.x, ref by, b.width, _roleClients[i]);
            }
        }

        _roleClients.Clear();
        CollectPlayers(_roleClients);
        int jailN = 0;
        for (int i = 0; i < _roleClients.Count; i++)
            if (_roleClients[i] != Me())
                jailN++;

        float jailBody = jailN > 0 ? 88f + jailN * 32f : 82f;
        b = Card(x, ref y, w, NocturneText.T("Тюрьма", "Jail"), inMatch ? jailBody : 26f, true);
        by = b.y;
        if (!inMatch)
            Lab(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Только в матче.", "In match only."), _muted);
        else
        {
            Lab(new Rect(b.x + 2f, by, b.width - 2f, 24f),
                NocturneText.T("Держит отмеченных в комнате — выйдут, закинет обратно в вент.", "Keeps marked players in the room — leaving forces them back into a vent."), _muted);
            by += 28f;

            float jrNameW = b.width - 60f;
            if (SmallButton(new Rect(b.x, by, 26f, 24f), "◄", new Color(0.5f, 0.55f, 0.62f)))
                NocturneJail.CellStep(-1);
            Lab(new Rect(b.x + 30f, by, jrNameW, 24f), NocturneJail.CellName(), _centerMuted);
            if (SmallButton(new Rect(b.x + b.width - 26f, by, 26f, 24f), "►", new Color(0.5f, 0.55f, 0.62f)))
                NocturneJail.CellStep(1);
            by += 30f;

            if (jailN == 0)
                Lab(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Нет других игроков.", "No other players."), _muted);
            else
            {
                float jSelw = (b.width - 8f) / 2f;
                if (SmallButton(new Rect(b.x, by, jSelw, 24f), NocturneText.T("ВЫБРАТЬ ВСЕХ", "SELECT ALL"), NocturneStyle.Current.Accent))
                    NocturneJail.SelectAll();
                if (SmallButton(new Rect(b.x + jSelw + 8f, by, jSelw, 24f), NocturneText.T("СНЯТЬ ВСЕ", "CLEAR ALL"), new Color(0.9f, 0.4f, 0.4f)))
                    NocturneJail.ClearTargets();
                by += 30f;
                for (int i = 0; i < _roleClients.Count; i++)
                    if (_roleClients[i] != Me())
                        JailRow(b.x, ref by, b.width, _roleClients[i]);
            }
        }

        _roleClients.Clear();
        CollectPlayers(_roleClients);
        int blN = 0;
        for (int i = 0; i < _roleClients.Count; i++)
            if (_roleClients[i] != Me())
                blN++;

        float blBody = blN > 0 ? 88f + blN * 32f : 54f;
        b = Card(x, ref y, w, NocturneText.T("Слепота / фулбрайт", "Blind / fullbright"), inMatch ? blBody : 26f);
        by = b.y;
        if (!inMatch)
            Lab(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Только в матче.", "In match only."), _muted);
        else
        {
            Lab(new Rect(b.x + 2f, by, b.width - 2f, 24f),
                NocturneText.T("Тьма или слепящий свет.", "Darkness or blinding light."), _muted);
            by += 28f;
            if (blN == 0)
                Lab(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Нет других игроков.", "No other players."), _muted);
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
                    if (_roleClients[i] != Me())
                        BlindRow(b.x, ref by, b.width, _roleClients[i]);
            }
        }

        b = Card(x, ref y, w, NocturneText.T("Хост-тролль", "Host tricks"), 3f * RowH + 44f);
        by = b.y;
        Lab(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Только хост, в матче.", "Host only, in match."), _muted);
        by += 24f;
        Toggle(b.x, ref by, b.width, NocturneText.T("Фейк-цвет при морфе", "Fake color on morph"), NocturneConfig.FakeMorphColor);
        Toggle(b.x, ref by, b.width, NocturneText.T("Глушить камеры смотрящим", "Jam watchers' cameras"), NocturneConfig.CameraJam);
        Toggle(b.x, ref by, b.width, NocturneText.T("Спам голосов (в собрании)", "Vote spam (in meeting)"), NocturneConfig.VoteSpam);

        b = Card(x, ref y, w, NocturneText.T("Фантом", "Phantom"), RowH + 26f, true);
        by = b.y;
        Toggle(b.x, ref by, b.width, NocturneText.T("Дым-бомба (спам исчезновения)", "Smoke bomb (vanish spam)"), NocturneConfig.SmokeSpam);
        Lab(new Rect(b.x + 2f, by, b.width - 2f, 20f), NocturneText.T("Только Фантом, в матче. Облако дыма у всех.", "Phantom only, in match. Smoke cloud for everyone."), _muted);

        int petN = 0;
        for (int i = 0; i < _roleClients.Count; i++)
            if (_roleClients[i] != Me())
                petN++;
        b = Card(x, ref y, w, NocturneText.T("Пет-рука", "Pet hand"), 28f + 26f + 30f + 30f + 30f + (petN > 0 ? petN * 32f : 0f), true);
        by = b.y;
        Lab(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Джойстик, роспись мышью, ГЛАДИТЬ (стоя) или СЛЕДОМ (на ходу).", "Joystick, mouse paint, PET (standing) or FOLLOW (while moving)."), _muted);
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
        if (SmallButton(new Rect(b.x, by, 26f, 24f), "◄", new Color(0.5f, 0.55f, 0.62f)))
            NocturnePet.RoomStep(-1);
        Lab(new Rect(b.x + 30f, by, rNameW, 24f), NocturnePet.RoomName(), _centerMuted);
        if (SmallButton(new Rect(b.x + b.width - 26f, by, 26f, 24f), "►", new Color(0.5f, 0.55f, 0.62f)))
            NocturnePet.RoomStep(1);
        by += 28f;
        if (SmallButton(new Rect(b.x, by, b.width, 24f), NocturneText.T("ЗАЛИТЬ КОМНАТУ ПЕТОМ", "FILL ROOM WITH PET"), NocturneStyle.Current.Accent))
            NocturneToast.Push(NocturneText.T("Пет", "Pet"), NocturnePet.FillRoom(), 2.5f, NocturneNotifyKind.Info);
        by += 30f;
        for (int i = 0; i < _roleClients.Count; i++)
            if (_roleClients[i] != Me())
                PetRow(b.x, ref by, b.width, _roleClients[i]);

        string[] frameSystems = NocturneText.IsRussian ? FrameSysRu : FrameSysEn;
        SystemTypes[] frameSystemValues = FrameSysVals;
        _roleClients.Clear();
        CollectPlayers(_roleClients);
        int frameCount = 0;
        for (int i = 0; i < _roleClients.Count; i++)
            if (_roleClients[i] != Me())
                frameCount++;

        b = Card(x, ref y, w, NocturneText.T("Подстава саботажа", "Frame sabotage"), inMatch ? 132f + (frameCount > 0 ? frameCount * 32f : RowH) : 26f, true);
        by = b.y;
        if (!inMatch)
            Lab(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Только в матче.", "In match only."), _muted);
        else
        {
            Lab(new Rect(b.x + 2f, by, b.width - 2f, 34f),
                NocturneText.T("Подделывает actor в системном RPC — ловит только чужой guard, не проверено. Sabotage флагает не-импостора при любом значении.", "Fakes the actor in a system RPC — only catches a foreign guard, untested. Sabotage flags a non-impostor at any value."), _muted);
            by += 38f;
            if (CycleRow(b.x, ref by, b.width, NocturneText.T("Система", "System"), frameSystems[_frameSystemIdx]))
                _frameSystemIdx = (_frameSystemIdx + 1) % frameSystems.Length;
            _frameValue = SliderIntVal(b.x, ref by, b.width, NocturneText.T("Значение", "Value"), _frameValue, 0, 200, "");
            if (frameCount == 0)
                Lab(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Нет других игроков.", "No other players."), _muted);
            else
                for (int i = 0; i < _roleClients.Count; i++)
                    if (_roleClients[i] != Me())
                        FrameSabotageRow(b.x, ref by, b.width, _roleClients[i], frameSystemValues[_frameSystemIdx]);
        }

        _roleClients.Clear();
        CollectPlayers(_roleClients);
        int tpCount = 0;
        for (int i = 0; i < _roleClients.Count; i++)
            if (_roleClients[i] != Me())
                tpCount++;
        b = Card(x, ref y, w, NocturneText.T("Телепорт к игроку", "Teleport to player"), 24f + (tpCount > 0 ? tpCount * 32f : 26f), true);
        by = b.y;
        Lab(new Rect(b.x + 2f, by, b.width - 2f, 22f), NocturneText.T("Прыжок к выбранному или идти за ним.", "Snap to the selected player or follow them."), _muted);
        by += 24f;
        if (tpCount == 0)
            Lab(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Нет других игроков.", "No other players."), _muted);
        else
            for (int i = 0; i < _roleClients.Count; i++)
                if (_roleClients[i] != Me())
                    TpRow(b.x, ref by, b.width, _roleClients[i]);

        b = Card(x, ref y, w, NocturneText.T("Яйца (хост)", "Eggs (host)"), 5f * 30f + 24f, true);
        by = b.y;
        Lab(new Rect(b.x + 2f, by, b.width - 2f, 22f), NocturneText.T("В игре, не в лобби. На офиц. сервере может кикнуть.", "In-game only. May kick on official servers."), _muted);
        by += 24f;
        float cw2 = (b.width - 10f) / 2f;
        if (SmallButton(new Rect(b.x, by, cw2, 26f), NocturneText.T("ВСЕ В ЯЙЦА", "ALL TO EGGS"), NocturneStyle.Current.Accent))
            FunToast(NocturneLobbyPranks.MassMorphToEgg());
        if (SmallButton(new Rect(b.x + cw2 + 10f, by, cw2, 26f), NocturneText.T("МОРФ В ВЫБРАННОГО", "MORPH TO TARGET"), NocturneStyle.Current.Accent))
            FunToast(NocturneLobbyPranks.MorphAllIntoSelected());
        by += 30f;
        if (SmallButton(new Rect(b.x, by, cw2, 26f), NocturneText.T("РАДУГА", "RAINBOW") + St(NocturneLobbyPranks.RainbowActive), FunCol(NocturneLobbyPranks.RainbowActive)))
            FunToast(NocturneLobbyPranks.ToggleRainbow());
        if (SmallButton(new Rect(b.x + cw2 + 10f, by, cw2, 26f), NocturneText.T("ЦИКЛ КОСМЕТИКИ", "COSMETIC CYCLE") + St(NocturneLobbyPranks.SkinCycleActive), FunCol(NocturneLobbyPranks.SkinCycleActive)))
            FunToast(NocturneLobbyPranks.ToggleSkinCycle());
        by += 30f;
        if (SmallButton(new Rect(b.x, by, cw2, 26f), NocturneText.T("ТАКТ: ", "BEAT: ") + NocturneLobbyPranks.SyncName(), NocturneStyle.Current.Accent))
            FunToast(NocturneLobbyPranks.ToggleSync());
        if (SmallButton(new Rect(b.x + cw2 + 10f, by, cw2, 26f), NocturneText.T("РАЗМЕР: ", "SIZE: ") + NocturneLobbyPranks.ScaleName(), NocturneStyle.Current.Accent))
            FunToast(NocturneLobbyPranks.CycleScale());
        by += 30f;
        if (SmallButton(new Rect(b.x, by, cw2, 26f), NocturneText.T("ДВИЖЕНИЕ: ", "MOTION: ") + NocturneLobbyPranks.SpinName(), NocturneStyle.Current.Accent))
            FunToast(NocturneLobbyPranks.CycleSpin());
        if (SmallButton(new Rect(b.x + cw2 + 10f, by, cw2, 26f), NocturneText.T("АНИМАЦИЯ: ", "ANIM: ") + NocturneLobbyPranks.AnimName(), NocturneStyle.Current.Accent))
            FunToast(NocturneLobbyPranks.CycleAnim());
        by += 30f;
        if (SmallButton(new Rect(b.x, by, b.width, 26f), NocturneText.T("СБРОС ОБЛИКА", "RESET LOOK"), new Color(0.9f, 0.4f, 0.4f)))
            FunToast(NocturneLobbyPranks.ResetAppearance());
    }

    private void CheatsSelf(float x, ref float y, float w)
    {
        Rect b = Card(x, ref y, w, NocturneText.T("Фантом: тихий уход", "Phantom: silent vanish"), 28f + 26f + RowH);
        float by = b.y;
        Lab(new Rect(b.x + 2f, by, b.width - 2f, 24f),
            NocturneText.T("Дым исчезновения уходит за карту — не видно, где ты пропал.", "Vanish smoke goes off-map — nobody sees where you vanished."), _muted);
        by += 26f;
        Toggle(b.x, ref by, b.width, NocturneText.T("Скрывать точку исчезновения", "Hide vanish point"), NocturneConfig.PhantomNoVanish);

        bool reach = NocturneConfig.BuffKillReach.Value;
        b = Card(x, ref y, w, NocturneText.T("Бафы ролей", "Role buffs"), RowH * 30f + 234f + (reach ? 54f : 4f));
        by = b.y;

        Sub(b.x, ref by, b.width, NocturneText.T("Общее", "General"));
        Toggle(b.x, ref by, b.width, NocturneText.T("Без кулдаунов", "No cooldowns"), NocturneConfig.BuffNoCd);
        Toggle(b.x, ref by, b.width, NocturneText.T("Ходить внутри вента", "Move inside vent"), NocturneConfig.BuffVentWalk);
        Toggle(b.x, ref by, b.width, NocturneText.T("Лестницы/зиплайн без кд", "No ladder/zipline cd"), NocturneConfig.BuffMapCd);

        Sub(b.x, ref by, b.width, NocturneText.T("Импостер", "Impostor"));
        Toggle(b.x, ref by, b.width, NocturneText.T("Дальность убийства", "Kill reach"), NocturneConfig.BuffKillReach);
        if (reach)
            Slider(b.x, ref by, b.width, NocturneText.T("Радиус", "Radius"), NocturneConfig.BuffKillDist, 1f, 12f, "0.0");
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
        Toggle(b.x, ref by, b.width, NocturneText.T("Морф в мёртвых", "Shapeshift into dead"), NocturneConfig.BuffSsDead);

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
        if (creach)
            Slider(b.x, ref by, b.width, NocturneText.T("Дистанция", "Distance"), NocturneConfig.ConsoleDist, 1f, 15f, "0.0");
        Toggle(b.x, ref by, b.width, NocturneText.T("Свой спавн на Airship", "Pick Airship spawn"), NocturneConfig.AirshipSpawn);
        if (air)
            SliderInt(b.x, ref by, b.width, NocturneText.T("Точка спавна", "Spawn point"), NocturneConfig.AirshipSpawnId, 0, 6);

        b = Card(x, ref y, w, NocturneText.T("Выживание", "Survival"), 3f * RowH + 132f);
        by = b.y;
        Toggle(b.x, ref by, b.width, NocturneText.T("Бессмертие (God Mode)", "God Mode"), NocturneConfig.GodMode);
        Lab(new Rect(b.x + 2f, by, b.width - 2f, 22f), NocturneText.T("Не убить (фейк-вент). От голосования не спасает.", "Can't be killed (fake vent). Not vs vote-out."), _muted);
        by += 24f;
        Toggle(b.x, ref by, b.width, NocturneText.T("Невидимость", "Invisibility"), NocturneConfig.Invisible);
        Lab(new Rect(b.x + 2f, by, b.width - 2f, 22f), NocturneText.T("Для других уезжаешь за карту. Взаимодействий/килла нет. ⚠ античит.", "You warp off-map for others. No interaction/kill. ⚠ anti-cheat."), _muted);
        by += 24f;
        Toggle(b.x, ref by, b.width, NocturneText.T("Дымок Фантома", "Phantom poof"), NocturneConfig.InvisiblePoof);
        Lab(new Rect(b.x + 2f, by, b.width - 2f, 22f), NocturneText.T("Хлопок и звук как у Фантома. Роль не нужна, видишь только ты.", "Phantom-style poof and sound. No role needed, only you see it."), _muted);
        by += 24f;
        float phw = (b.width - 6f) / 2f;
        if (SmallButton(new Rect(b.x, by, phw, 24f), NocturneText.T("ФАНТОМ В ЛОББИ (ХОСТ)", "PHANTOM IN LOBBY (HOST)"), NocturneStyle.Current.Accent))
            LobbyPhantom.Vanish();
        if (SmallButton(new Rect(b.x + phw + 6f, by, phw, 24f), NocturneText.T("ВЕРНУТЬСЯ (ХОСТ)", "APPEAR (HOST)"), new Color(0.5f, 0.55f, 0.62f)))
            LobbyPhantom.Appear();
        by += 28f;
        if (SmallButton(new Rect(b.x, by, b.width, 24f), NocturneText.T("ОСТАВИТЬ ТЕЛО (ХОСТ)", "LEAVE A BODY (HOST)"), NocturneStyle.Current.Accent))
            Corpses.Drop();

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
        Lab(new Rect(b.x + 2f, by, b.width - 2f, 40f), NocturneText.T("Только для других — у себя двигаешься нормально. ⚠ палит античит.", "Others only — you move normally. ⚠ anti-cheat risk."), _muted);

        bool spd = NocturneConfig.SpeedMod.Value;
        b = Card(x, ref y, w, NocturneText.T("Движение", "Movement"), 2f * RowH + 22f + (spd ? 50f : 0f));
        by = b.y;
        Toggle(b.x, ref by, b.width, NocturneText.T("Своя скорость", "Custom speed"), NocturneConfig.SpeedMod);
        if (spd)
            Slider(b.x, ref by, b.width, NocturneText.T("Множитель", "Multiplier"), NocturneConfig.SpeedMult, 0f, 3f, "0.0");
        Toggle(b.x, ref by, b.width, NocturneText.T("Инверт управления", "Invert controls"), NocturneConfig.InvertControls);
        Lab(new Rect(b.x + 2f, by, b.width - 2f, 22f), NocturneText.T("Клиентское. Большие значения палит античит.", "Client-side. High values trip anti-cheat."), _muted);

        bool sd = NocturneConfig.SelfDrag.Value;
        bool sds = sd && NocturneConfig.SelfDragSmooth.Value;
        b = Card(x, ref y, w, NocturneText.T("Мышь и призрак", "Mouse & ghost"), 4f * RowH + 52f + (sd ? RowH : 0f) + (sds ? RowH : 0f));
        by = b.y;
        Toggle(b.x, ref by, b.width, NocturneText.T("Телепорт по ПКМ", "Teleport on RMB"), NocturneConfig.MouseTeleport);
        Toggle(b.x, ref by, b.width, NocturneText.T("Выбор мышью + ресайз колёсиком", "Mouse select + wheel resize"), NocturneConfig.MouseSelect);
        Toggle(b.x, ref by, b.width, NocturneText.T("Тащить себя (ЛКМ)", "Drag self (LMB)"), NocturneConfig.SelfDrag);
        if (sd)
            Toggle(b.x, ref by, b.width, NocturneText.T("— плавно (скольжение)", "— smooth glide"), NocturneConfig.SelfDragSmooth);
        if (sds)
            Slider(b.x, ref by, b.width, NocturneText.T("Скорость", "Speed"), NocturneConfig.SelfDragSpeed, 2f, 14f, "0.0");
        bool gaBefore = NocturneConfig.GhostAfterStart.Value;
        Toggle(b.x, ref by, b.width, NocturneText.T("Призрак после старта", "Ghost after start"), NocturneConfig.GhostAfterStart);
        if (NocturneConfig.GhostAfterStart.Value && !gaBefore)
            NocturneConfig.GameMaster.Value = false;
        if (SmallButton(new Rect(b.x + 2f, by, b.width - 4f, 26f), NocturneText.T("СУИЦИД (ПРЕД)", "SUICIDE (IMPOSTOR)"), new Color(0.9f, 0.4f, 0.4f)))
            NocturneToast.Push(NocturneText.T("Суицид", "Suicide"), Patches.GhostStart.Now(), 2.5f, NocturneNotifyKind.Warning);
        by += 30f;
        Lab(new Rect(b.x + 2f, by, b.width - 2f, 22f), NocturneText.T("Настоящая смерть, только за преда и при готовом кд.", "Real death, impostor only, needs kill cooldown ready."), _muted);
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
        for (int i = 0; i < _roleClients.Count; i++)
            if (_roleClients[i] != Me())
                zpN++;
        bool zpOn = inMatch && Zipline.OnMap;

        Rect b = Card(x, ref y, w, NocturneText.T("Зиплайн (Fungle)", "Zipline (Fungle)"), zpOn && zpN > 0 ? 88f + zpN * 32f : 26f);
        float by = b.y;
        if (!inMatch)
            Lab(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Только в матче.", "In match only."), _muted);
        else if (!Zipline.OnMap)
            Lab(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Зиплайна на этой карте нет.", "No zipline on this map."), _muted);
        else if (zpN == 0)
            Lab(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Нет других игроков.", "No other players."), _muted);
        else
        {
            Lab(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Отправить кататься.", "Send for a ride."), _muted);
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
                if (_roleClients[i] != Me())
                    RideRow(b.x, ref by, b.width, _roleClients[i]);
        }

        bool ptOn = inMatch && Platform.OnMap;
        b = Card(x, ref y, w, NocturneText.T("Платформа (Airship)", "Platform (Airship)"), ptOn ? 58f + RowH : 26f);
        by = b.y;
        if (!inMatch)
            Lab(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Только в матче.", "In match only."), _muted);
        else if (!Platform.OnMap)
            Lab(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Платформы на этой карте нет.", "No platform on this map."), _muted);
        else
        {
            Toggle(b.x, ref by, b.width, NocturneText.T("Разблокировать (прятки)", "Unlock (hide & seek)"), NocturneConfig.PlatformUnlock);
            string state = Platform.IsLeft ? NocturneText.T("слева", "left") : NocturneText.T("справа", "right");
            if (Platform.Locked)
                state += NocturneText.T(" · заблокирована", " · locked");
            Lab(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Сейчас: ", "Now: ") + state, _muted);
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
            Lab(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Только в матче.", "In match only."), _muted);
        else
        {
            cw = (b.width - 10f) / 2f;
            if (SmallButton(new Rect(b.x, by, cw, 26f), NocturneText.T("ЗАКРЫТЬ ВСЕ", "CLOSE ALL"), acc))
                NocturneDoors.CloseAll();
            if (SmallButton(new Rect(b.x + cw + 10f, by, cw, 26f), NocturneText.T("ОТКРЫТЬ ВСЕ", "OPEN ALL"), acc))
                NocturneDoors.OpenAll();
            by += 30f;
            if (SmallButton(new Rect(b.x, by, cw, 26f), NocturneText.T("ЗАПИНИТЬ ВСЕ", "PIN ALL"), acc))
                NocturneDoors.PinAll();
            if (SmallButton(new Rect(b.x + cw + 10f, by, cw, 26f), NocturneText.T("СНЯТЬ ПИНЫ", "UNPIN"), red))
                NocturneDoors.UnpinAll();
            by += 30f;
            Toggle(b.x, ref by, b.width, NocturneText.T("Авто-открытие (без миниигры)", "Auto-open (no minigame)"), NocturneConfig.DoorKeepOpen);
        }

        float sabBody = 30f * 4f + RowH * 3f + 4f;
        b = Card(x, ref y, w, NocturneText.T("Саботаж", "Sabotage"), (inMatch ? sabBody : 26f) + RowH);
        by = b.y;
        Toggle(b.x, ref by, b.width, NocturneText.T("Обход саботажа связи", "Bypass comms sabotage"), NocturneConfig.CommsBypass);
        if (!inMatch)
            Lab(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Только в матче.", "In match only."), _muted);
        else
        {
            cw = (b.width - 10f) / 2f;
            string mainLbl = mapId == 2 ? NocturneText.T("СЕЙСМИКА", "SEISMIC") : mapId == 4 ? NocturneText.T("КРУШЕНИЕ", "CRASH") : NocturneText.T("РЕАКТОР", "REACTOR");
            if (SmallButton(new Rect(b.x, by, b.width, 26f), NocturneText.T("ПОЧИНИТЬ ВСЁ", "FIX ALL"), new Color(0.30f, 0.72f, 0.40f)))
                NocturneSabotage.Fix();
            by += 30f;
            if (SmallButton(new Rect(b.x, by, cw, 26f), NocturneText.T("САБОТАЖ ВСЕГО", "SABOTAGE ALL"), red))
                NocturneSabotage.All();
            if (SmallButton(new Rect(b.x + cw + 10f, by, cw, 26f), NocturneText.T("СЛУЧАЙНЫЙ", "RANDOM"), acc))
                NocturneSabotage.Random();
            by += 30f;
            if (SmallButton(new Rect(b.x, by, cw, 26f), mainLbl, acc))
                NocturneSabotage.Main();
            if (SmallButton(new Rect(b.x + cw + 10f, by, cw, 26f), NocturneText.T("СВЯЗЬ", "COMMS"), acc))
                NocturneSabotage.Comms();
            by += 30f;
            if (!fungle && SmallButton(new Rect(b.x, by, hasO2 ? cw : b.width, 26f), NocturneText.T("СВЕТ", "LIGHTS"), acc))
                NocturneSabotage.Lights();
            if (hasO2 && SmallButton(new Rect(b.x + cw + 10f, by, cw, 26f), NocturneText.T("КИСЛОРОД", "OXYGEN"), acc))
                NocturneSabotage.Oxygen();
            if (!fungle || hasO2)
                by += 30f;
            if (fungle && SmallButton(new Rect(b.x, by, b.width, 26f), NocturneText.T("ГРИБЫ (MIXUP)", "MUSHROOM MIXUP"), acc))
            {
                NocturneSabotage.Mush();
            }
            if (fungle)
                by += 30f;
            Toggle(b.x, ref by, b.width, NocturneText.T("Спам главного саботажа", "Spam main sabotage"), NocturneConfig.SabSpamReactor);
            Toggle(b.x, ref by, b.width, NocturneText.T("Мульти-саботаж (пред, не хост)", "Multi sabotage (impostor, non-host)"), NocturneConfig.MultiSabotage);
            if (!fungle)
                Toggle(b.x, ref by, b.width, NocturneText.T("Держать свет выключенным", "Keep lights off"), NocturneConfig.SabAutoLights);
            if (fungle)
                Toggle(b.x, ref by, b.width, NocturneText.T("Бесконечные грибы", "Infinite mushroom"), NocturneConfig.SabInfMushroom);
        }
    }

    private void CheatsTasks(float x, ref float y, float w)
    {
        bool inMatch = ShipStatus.Instance != null;
        Color acc = NocturneStyle.Current.Accent;
        float cw;

        Rect b = Card(x, ref y, w, NocturneText.T("Прятки: слив таймера", "Hide & seek: drain timer"), 28f + RowH + 50f);
        float by = b.y;
        Lab(new Rect(b.x + 2f, by, b.width - 2f, 24f),
            TaskDrain.Running
                ? NocturneText.T("Идёт слив — таймер мирных утекает.", "Draining — the crew timer is running out.")
                : NocturneText.T("Только в прятках. Работает и призраком.", "Hide and seek only. Keeps working as a ghost."), _muted);
        by += 26f;
        Toggle(b.x, ref by, b.width, NocturneText.T("Сливать таймер", "Drain the timer"), NocturneConfig.HnsDrain);
        Slider(b.x, ref by, b.width, NocturneText.T("Шаг отправки, сек", "Send step, sec"), NocturneConfig.HnsDrainStep, 0.15f, 1.5f, "0.00");

        _roleClients.Clear();
        CollectPlayers(_roleClients);
        int tkN = 0;
        for (int i = 0; i < _roleClients.Count; i++)
            if (_roleClients[i] != Me())
                tkN++;
        bool tkOn = inMatch && Utils.Host;
        b = Card(x, ref y, w, NocturneText.T("Задания (хост)", "Tasks (host)"), 26f + (tkOn && tkN > 0 ? 24f + tkN * 32f : 24f));
        by = b.y;
        if (!inMatch)
            Lab(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Только в матче.", "In match only."), _muted);
        else if (!Utils.Host)
            Lab(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Только для хоста.", "Host only."), _muted);
        else if (tkN == 0)
            Lab(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Нет других игроков.", "No other players."), _muted);
        else
        {
            Lab(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Обнулить — палит, завалить — гора тасков.", "Clear exposes them, flood buries in tasks."), _muted);
            by += 26f;
            for (int i = 0; i < _roleClients.Count; i++)
                if (_roleClients[i] != Me())
                    TaskRow(b.x, ref by, b.width, _roleClients[i]);
        }

        float fakeBody = inMatch ? 40f + 30f + RowH * 2f : 26f;
        b = Card(x, ref y, w, NocturneText.T("Фейк-задания", "Fake tasks"), fakeBody);
        by = b.y;
        if (!inMatch)
            Lab(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Только в матче.", "In match only."), _muted);
        else
        {
            Lab(new Rect(b.x + 2f, by, b.width - 2f, 36f), NocturneText.T("Проиграть анимацию задания для окружающих (казаться занятым).", "Play a task animation for others (look busy)."), _muted);
            by += 40f;
            cw = (b.width - 20f) / 3f;
            if (SmallButton(new Rect(b.x, by, cw, 26f), NocturneText.T("ЩИТЫ", "SHIELDS"), acc))
                NocturneFakeTasks.Shields();
            if (SmallButton(new Rect(b.x + cw + 10f, by, cw, 26f), NocturneText.T("АСТЕРОИДЫ", "ASTEROIDS"), acc))
                NocturneFakeTasks.Asteroids();
            if (SmallButton(new Rect(b.x + (cw + 10f) * 2f, by, cw, 26f), NocturneText.T("МУСОР", "GARBAGE"), acc))
                NocturneFakeTasks.Garbage();
            by += 30f;
            Toggle(b.x, ref by, b.width, NocturneText.T("Скан в медбэе (постоянно)", "Medbay scan (held)"), NocturneConfig.FakeScan);
            if (NocturneFakeTasks.HasCams())
                Toggle(b.x, ref by, b.width, NocturneText.T("Камеры «заняты»", "Cameras in use"), NocturneConfig.FakeCams);
            else
                Lab(new Rect(b.x + 2f, by, b.width - 2f, 22f), NocturneText.T("Камеры — не на этой карте.", "Cameras — not on this map."), _muted);
        }

        bool autoOn = NocturneConfig.AutoTasks.Value;
        b = Card(x, ref y, w, NocturneText.T("Авто-задания", "Auto tasks"), RowH + (autoOn ? 76f : 24f));
        by = b.y;
        Toggle(b.x, ref by, b.width, NocturneText.T("Делать задания сами", "Finish tasks by itself"), NocturneConfig.AutoTasks);
        if (autoOn)
        {
            Slider(b.x, ref by, b.width, NocturneText.T("Пауза между заданиями", "Gap between tasks"), NocturneConfig.AutoTasksDelay, 0.8f, 6f, "0.0");
            int left = NocturneAutoTasks.Left();
            Lab(new Rect(b.x + 2f, by, b.width - 2f, 22f),
                inMatch ? NocturneText.T("Осталось: ", "Left: ") + $"<b>{left}</b>" : NocturneText.T("Только в матче, мирным.", "In match, crewmate only."), _muted);
        }
        else
            Lab(new Rect(b.x + 2f, by, b.width - 2f, 22f), NocturneText.T("По одному, не в собрании. Не для предов.", "One by one, never in a meeting. Not for impostors."), _muted);
    }

    private void CheatsChat(float x, ref float y, float w)
    {
        Rect b = Card(x, ref y, w, NocturneText.T("Отправить в чат", "Chat sender"), 4f * RowH + 50f);
        float by = b.y;
        _chatSend = CustomText(new Rect(b.x + 2f, by, b.width - 4f, 26f), _chatSend ?? "", "chatSend");
        NocturneChatSender.Message = _chatSend ?? "";
        by += 32f;
        float chHalf = (b.width - 10f) / 2f;
        if (SmallButton(new Rect(b.x, by, chHalf, 26f), NocturneText.T("ОТПРАВИТЬ", "SEND"), NocturneStyle.Current.Accent))
            NocturneChatSender.SendNow();
        if (SmallButton(new Rect(b.x + chHalf + 10f, by, chHalf, 26f), NocturneChatSender.Spamming ? NocturneText.T("СПАМ: ВКЛ", "SPAM: ON") : NocturneText.T("СПАМ: ВЫКЛ", "SPAM: OFF"), NocturneChatSender.Spamming ? new Color(0.9f, 0.4f, 0.4f) : NocturneStyle.Current.Accent))
            NocturneChatSender.Spamming = !NocturneChatSender.Spamming;
        by += 30f;
        bool floodReady = NocturneChatSender.FloodReady;
        string floodLabel = floodReady ? NocturneText.T("ЗАТОПИТЬ ЧАТ", "FLOOD CHAT") : NocturneText.T("ОСТЫВАЕТ ", "COOLDOWN ") + Mathf.CeilToInt(NocturneChatSender.FloodCooldownLeft) + "с";
        if (SmallButton(new Rect(b.x, by, b.width - 4f, 26f), floodLabel, floodReady ? new Color(0.9f, 0.4f, 0.4f) : NocturneStyle.Current.Button) && floodReady)
            NocturneToast.Push(NocturneText.T("Чат", "Chat"), NocturneChatSender.Flood(), 2f, NocturneNotifyKind.Warning);
        by += 30f;
        Slider(b.x, ref by, b.width, NocturneText.T("Задержка (с)", "Delay (s)"), NocturneConfig.ChatSpamDelay, 1.5f, 10f, "0.0");
        by += 22f;
        Lab(new Rect(b.x + 2f, by, b.width - 2f, 22f), NocturneText.T("Чат в лобби/митинге. Малая задержка — кик за флуд.", "Chat in lobby/meeting. Low delay — flood kick."), _muted);

        const int qcCols = 5;
        int qcRows = Mathf.CeilToInt(NocturneQuickChatChain.KnownSubs.Length / (float)qcCols);
        float qcListHeight = _qcWordListOpen ? qcRows * 30f + 4f : 0f;
        b = Card(x, ref y, w, NocturneText.T("Цепочка Quick Chat", "Quick Chat chain"), 5f * RowH + 34f + qcListHeight);
        by = b.y;
        Lab(new Rect(b.x + 2f, by, b.width - 2f, 22f),
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
        Lab(new Rect(b.x + 2f, by, b.width - 2f, 38f),
            NocturneText.T("ID корневой фразы-шаблона (например, обвинение). Игрок A берётся по ЛКМ-выбору.",
                "Template root phrase id (e.g. an accusation). Player A is whoever is LMB-selected."), _muted);
        by += 40f;
        _qcTemplate = CustomText(new Rect(b.x + 2f, by, b.width - 4f, 26f), _qcTemplate ?? "", "qcTemplate");
        by += 32f;
        Lab(new Rect(b.x + 2f, by, b.width - 2f, 38f),
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

        Grp(1);
        Rect b = Card(x, ref y, w, NocturneText.T("Роли и инфо", "Roles & info"), (NocturneConfig.RevealVotes.Value ? 9f : 8f) * RowH, true);
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
        Grp(2);
        b = Card(x, ref y, w, NocturneText.T("Форс ролей (хост)", "Force roles (host)"), rbBody, true);
        by = b.y;
        if (!Utils.Host)
            Lab(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Только хост. Роли выдаются на старте матча.", "Host only. Roles apply on match start."), _muted);
        else if (_roleClients.Count == 0)
            Lab(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Нет игроков в лобби.", "No players in lobby."), _muted);
        else
        {
            Lab(new Rect(b.x + 2f, by, b.width - 110f, 24f), $"{NocturneText.T("Назначено", "Assigned")}: <b>{NocturneForceRoles.Count}</b>   {NocturneText.T("выдача на старте", "applied on start")}", _muted);
            if (SmallButton(new Rect(b.x + b.width - 96f, by, 92f, 24f), NocturneText.T("СБРОС", "CLEAR"), new Color(0.9f, 0.4f, 0.4f)))
                NocturneForceRoles.Clear();
            by += 30f;
            for (int i = 0; i < _roleClients.Count; i++)
                RoleRow(b.x, ref by, b.width, _roleClients[i]);
        }

        _immPlayers.Clear();
        CollectPlayers(_immPlayers);
        bool immReady = ShipStatus.Instance != null && LobbyBehaviour.Instance == null;
        int ventCount = NocturneVentTp.VentCount();
        float autoH = immReady ? 2f * RowH + (NocturneConfig.VentTpAuto.Value ? 50f : 0f) : 0f;
        float immBody = 30f + (immReady ? 30f : 0f) + (immReady && _immPlayers.Count > 0 ? 60f : 0f) + autoH + (immReady && _immPlayers.Count > 0 ? _immPlayers.Count * 32f : 26f);
        Grp(2);
        b = Card(x, ref y, w, NocturneText.T("Действия на игроках", "Player actions"), immBody, true);
        by = b.y;
        Lab(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Работает вне хоста. Нужен ванильный хост.", "Works off-host. Needs a vanilla host."), _muted);
        by += 28f;
        if (!immReady)
            Lab(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Только в матче.", "In-match only."), _muted);
        else if (_immPlayers.Count == 0)
            Lab(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Нет игроков.", "No players."), _muted);
        else
        {
            int vt = ventCount > 0 ? ((NocturneVentTp.Vent % ventCount) + ventCount) % ventCount : 0;
            NocturneVentTp.Vent = vt;
            Lab(new Rect(b.x + 2f, by, b.width - 200f, 24f), NocturneText.T("Вент для ТП: ", "TP vent: ") + (ventCount > 0 ? vt.ToString() : "-") + $"   {NocturneText.T("отмечено", "marked")}: {NocturneVentTp.MarkedCount}", _muted);
            if (SmallButton(new Rect(b.x + b.width - 116f, by, 34f, 24f), "◂", NocturneStyle.Current.Accent) && ventCount > 0)
                NocturneVentTp.Vent = (vt - 1 + ventCount) % ventCount;
            if (SmallButton(new Rect(b.x + b.width - 40f, by, 34f, 24f), "▸", NocturneStyle.Current.Accent) && ventCount > 0)
                NocturneVentTp.Vent = (vt + 1) % ventCount;
            by += 30f;
            float selw = (b.width - 8f) / 2f;
            if (SmallButton(new Rect(b.x, by, selw, 24f), NocturneText.T("ВЫБРАТЬ ВСЕХ", "SELECT ALL"), NocturneStyle.Current.Accent))
                NocturneVentTp.MarkAll();
            if (SmallButton(new Rect(b.x + selw + 8f, by, selw, 24f), NocturneText.T("СНЯТЬ ВСЕ", "CLEAR ALL"), new Color(0.9f, 0.4f, 0.4f)))
                NocturneVentTp.ClearMarks();
            by += 30f;
            if (SmallButton(new Rect(b.x, by, b.width, 24f), NocturneText.T("ВЫКИНУТЬ ВСЕХ ИЗ ВЕНТОВ", "KICK EVERYONE FROM VENTS"), new Color(0.78f, 0.42f, 0.95f)))
                NocturneToast.Push(NocturneText.T("Венты", "Vents"), NocturneVentTp.KickAllFromVents(), 2.2f, NocturneNotifyKind.Info);
            by += 30f;
            Toggle(b.x, ref by, b.width, NocturneText.T("Авто-раскидывание отмеченных", "Auto-scatter marked"), NocturneConfig.VentTpAuto);
            if (NocturneConfig.VentTpAuto.Value)
                Slider(b.x, ref by, b.width, NocturneText.T("Интервал", "Interval"), NocturneConfig.VentTpAutoDelay, 0.3f, 10f, "0.0");
            Toggle(b.x, ref by, b.width, NocturneText.T("Сбор на предателя (килл/морф/невидимость)", "Rally to impostor (kill/morph/vanish)"), NocturneConfig.ImpTrap);
            for (int i = 0; i < _immPlayers.Count; i++)
                ImmortalRow(b.x, ref by, b.width, _immPlayers[i], vt);
        }

        Grp(2);
        ShieldCard(x, ref y, w);

        Grp(2);
        b = Card(x, ref y, w, NocturneText.T("Морф (хост)", "Morph (host)"), 2f * RowH + 22f, true);
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
        Grp(1);
        b = Card(x, ref y, w, NocturneText.T("Игроки в лобби", "Players in lobby"), listBody, true);
        by = b.y;
        if (_guardClients.Count == 0)
            Lab(new Rect(b.x + 2f, by, b.width - 2f, 26f), NocturneText.T("Зайди в лобби как хост.", "Join a lobby as host."), _muted);
        else
            for (int i = 0; i < _guardClients.Count; i++)
                PlayerRow(b.x, ref by, b.width, net, _guardClients[i]);

        Grp(0);
        int savedCols = LaySuspend(ref y);
        PlayerCard(x, ref y, w);
        LayResume(savedCols, y);
    }

    private static readonly Color CBlue = new Color(0.55f, 0.7f, 1f);
    private static readonly Color CGray = new Color(0.5f, 0.55f, 0.62f);
    private static readonly Color COrange = new Color(0.86f, 0.5f, 0.28f);
    private static readonly Color CRed = new Color(0.9f, 0.36f, 0.36f);
    private static readonly Color CPurple = new Color(0.78f, 0.42f, 0.95f);
    private static readonly Color CGold = new Color(0.95f, 0.78f, 0.3f);

    private readonly List<PlayerControl> _cardPlayers = new List<PlayerControl>();
    private byte _cardPid = 255;

    private PlayerControl CardTarget()
    {
        for (int i = 0; i < _cardPlayers.Count; i++)
        {
            PlayerControl pc = _cardPlayers[i];
            if (pc != null && pc.PlayerId == _cardPid)
                return pc;
        }
        return null;
    }

    private static readonly string[] ActTabsRu = { "ДЕЙСТ.", "ОБЛИК", "АТАКА" };
    private static readonly string[] ActTabsEn = { "ACTIONS", "LOOK", "ATTACK" };

    private int _cardAct;

    private void PlayerCard(float x, ref float y, float w)
    {
        _cardPlayers.Clear();
        CollectPlayers(_cardPlayers);

        PlayerControl sel = CardTarget();
        if (sel == null && NocturneMouseTools.Selected != null)
        {
            _cardPid = NocturneMouseTools.Selected.PlayerId;
            sel = CardTarget();
        }

        InnerNetClient net = GuardNet();
        ClientData c = sel != null && net != null ? NocturneAccess.FindClient(net, sel.OwnerId) : null;
        bool me = sel != null && sel == PlayerControl.LocalPlayer;

        bool narrow = w < 430f;
        float lw = narrow ? w : Mathf.Max(190f, w * 0.42f);
        float rw = narrow ? w : w - lw - 8f;
        float rx = narrow ? x : x + lw + 8f;
        float ly = y;
        float ry = y;

        float listH = _cardPlayers.Count > 0 ? _cardPlayers.Count * 30f + 10f : 26f;
        Rect b = Card(x, ref ly, lw, NocturneText.T("Игроки", "Players"), listH);
        float by = b.y;
        if (_cardPlayers.Count == 0)
            Lab(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Нет игроков.", "No players."), _muted);
        else
        {
            var box = new Rect(b.x, by, b.width, listH);
            if (NocturneStyle.Painting)
            {
                NocturneStyle.FillRounded(box, A(Color.black, 0.26f), 8);
                NocturneStyle.StrokeRounded(box, A(Color.white, 0.09f), 8, 1);
            }
            by += 5f;
            for (int i = 0; i < _cardPlayers.Count; i++)
                CardRow(b.x + 5f, ref by, b.width - 10f, _cardPlayers[i]);
        }

        b = Card(x, ref ly, lw, NocturneText.T("Карточка", "Card"), sel != null ? 296f : 26f);
        by = b.y;
        if (sel == null)
            Lab(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Выбери игрока.", "Pick a player."), _muted);
        else
            CardInfo(b.x, ref by, b.width, sel, c);

        if (narrow)
            ry = ly;

        bool acts = sel != null && !me;
        int rows = _cardAct == 0 ? 6 : _cardAct == 1 ? 3 : 2;
        b = Card(rx, ref ry, rw, NocturneText.T("Действия", "Actions"), acts ? 34f + rows * 30f : 26f);
        by = b.y;
        if (sel == null)
            Lab(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Выбери игрока.", "Pick a player."), _muted);
        else if (me)
            Lab(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Это ты.", "That's you."), _muted);
        else
        {
            ActTabs(b.x, ref by, b.width);
            if (_cardAct == 0)
                CardActs(b.x, ref by, b.width, sel);
            else if (_cardAct == 1)
                CardLook(b.x, ref by, b.width, sel);
            else
                CardAttack(b.x, ref by, b.width, sel);
        }

        b = Card(rx, ref ry, rw, NocturneText.T("Модерация", "Moderation"), acts ? 64f : 26f);
        by = b.y;
        if (!acts)
            Lab(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("—", "—"), _muted);
        else
            CardMod(b.x, ref by, b.width, sel, net, c);

        y = Mathf.Max(ly, ry);
    }

    private void ActTabs(float x, ref float y, float w)
    {
        string[] names = NocturneText.IsRussian ? ActTabsRu : ActTabsEn;
        float bw = (w - 8f) / names.Length;
        for (int i = 0; i < names.Length; i++)
        {
            var r = new Rect(x + (bw + 4f) * i, y, bw, 24f);
            bool on = _cardAct == i;
            if (NocturneStyle.Painting)
            {
                NocturnePalette p = NocturneStyle.Current;
                NocturneStyle.FillRounded(r, on ? A(p.Accent, 0.8f) : A(Color.black, 0.22f), 6);
                if (!on)
                    NocturneStyle.StrokeRounded(r, A(Color.white, 0.07f), 6, 1);
                Lab(r, names[i], on ? _tabOn : _tabOff);
            }
            if (GUI.Button(r, GUIContent.none, _invisible))
                _cardAct = i;
        }
        y += 30f;
    }

    private void CardRow(float x, ref float y, float w, PlayerControl pc)
    {
        if (RowCull(ref y, 28f, 30f))
            return;
        var r = new Rect(x, y, w, 28f);
        bool on = pc.PlayerId == _cardPid;
        if (on)
        {
            if (NocturneStyle.Painting)
                NocturneStyle.FillRounded(r, A(NocturneStyle.Current.Accent, 0.8f), 6);
        }
        else
            HoverFill(r);

        DrawColorDot(new Rect(r.x + 7f, r.y + 8f, 13f, 13f), pc, 3);
        if (NocturneStyle.Painting)
        {
            string nm = pc.Data != null ? pc.Data.PlayerName : "?";
            if (pc == PlayerControl.LocalPlayer)
                nm += "   <color=#8A94AC><size=11>" + NocturneText.T("это ты", "you") + "</size></color>";
            Lab(new Rect(r.x + 28f, r.y, r.width - 32f, r.height), nm, on ? _rowSel : _rowName);
        }
        if (GUI.Button(r, GUIContent.none, _invisible))
        {
            _cardPid = pc.PlayerId;
            NocturneMouseTools.Select(pc);
        }
        y += 30f;
    }

    private bool CardBtn(float x, float y, float bw, int col, string label, Color tint)
        => SmallButton(new Rect(x + (bw + 8f) * col, y, bw, 24f), label, tint);

    private void CardActs(float x, ref float y, float w, PlayerControl pc)
    {
        float bw = (w - 16f) / 3f;
        byte pid = pc.PlayerId;

        bool watch = NocturneFollow.IsTarget(pid);
        if (CardBtn(x, y, bw, 0, watch ? NocturneText.T("СТОП", "STOP") : NocturneText.T("СЛЕДИТЬ", "WATCH"), watch ? CGold : CBlue))
            NocturneToast.Push(NocturneText.T("Слежка", "Watch"), NocturneFollow.Toggle(pc), 2.2f, NocturneNotifyKind.Info);
        if (CardBtn(x, y, bw, 1, NocturneText.T("ЗРЕНИЕ", "VISION"), NocturneBlind.IsDark(pid) || NocturneBlind.IsBright(pid) ? CGold : CGray))
            NocturneToast.Push(NocturneText.T("Зрение", "Vision"), NocturneBlind.Cycle(pc), 2.2f, NocturneNotifyKind.Info);
        if (CardBtn(x, y, bw, 2, NocturneText.T("ШЕПНУТЬ", "WHISPER"), CBlue))
            NocturneWhisper.Prefill(pid.ToString());
        y += 30f;

        if (CardBtn(x, y, bw, 0, NocturneText.T("ЩИТ", "SHIELD"), NocturneStyle.Current.Accent))
            NocturneToast.Push(NocturneText.T("Щит", "Shield"), NocturneShield.Give(pc), 2.2f, NocturneNotifyKind.Info);
        bool god = NocturneGodMode.IsGranted(pid);
        if (CardBtn(x, y, bw, 1, god ? NocturneText.T("СНЯТЬ", "UNGOD") : NocturneText.T("БЕСС.", "GOD"), god ? CGold : CGray))
            NocturneToast.Push(NocturneText.T("Бессмертие", "Immortality"), NocturneGodMode.Toggle(pc), 2.2f, NocturneNotifyKind.Info);
        if (CardBtn(x, y, bw, 2, NocturneText.T("В ВЕНТ", "TO VENT"), CBlue))
            NocturneToast.Push(NocturneText.T("Вент-ТП", "Vent TP"), NocturneVentTp.Send(pc, NocturneVentTp.Vent), 2.2f, NocturneNotifyKind.Info);
        y += 30f;

        if (CardBtn(x, y, bw, 0, NocturneText.T("ВЕНТ-КИК", "VENT KICK"), CPurple))
            NocturneToast.Push(NocturneText.T("Венты", "Vents"), NocturneVentKick.Kick(pc), 2.2f, NocturneNotifyKind.Info);
        if (CardBtn(x, y, bw, 1, NocturneText.T("ВЫБИТЬ", "BOOT"), CPurple))
            NocturneToast.Push(NocturneText.T("Венты", "Vents"), NocturneVentTp.BootOnly(pc, NocturneVentTp.Vent), 2.2f, NocturneNotifyKind.Info);
        bool marked = NocturneVentKick.IsSelected(pid);
        if (CardBtn(x, y, bw, 2, marked ? NocturneText.T("ОТМЕЧЕН", "MARKED") : NocturneText.T("ОТМЕТИТЬ", "MARK"), marked ? CGold : CGray))
            NocturneVentKick.ToggleSelect(pid);
        y += 30f;

        if (CardBtn(x, y, bw, 0, NocturneText.T("КИК ОТМ.", "KICK MARKED") + " " + NocturneVentKick.SelectedCount, CPurple))
            NocturneToast.Push(NocturneText.T("Венты", "Vents"), NocturneVentKick.KickSelected(), 2.2f, NocturneNotifyKind.Info);
        if (CardBtn(x, y, bw, 1, NocturneText.T("ЗИПЛАЙН", "ZIPLINE"), CPurple))
            NocturneToast.Push(NocturneText.T("Зиплайн", "Zipline"), Zipline.Ride(pc, true), 2.2f, NocturneNotifyKind.Info);
        bool jail = NocturneJail.IsTarget(pid);
        if (CardBtn(x, y, bw, 2, NocturneText.T("ТЮРЬМА", "JAIL"), jail ? CGold : CGray))
            NocturneJail.ToggleTarget(pid);
        y += 30f;

        bool ride = RideTargets.Has(pid);
        if (CardBtn(x, y, bw, 0, NocturneText.T("КАТАТЬ", "RIDE"), ride ? CGold : CGray))
            RideTargets.Toggle(pid);
        bool petting = NocturnePet.IsTarget(pid);
        if (CardBtn(x, y, bw, 1, NocturneText.T("ГЛАДИТЬ", "PET"), petting ? CGold : CBlue))
            NocturneToast.Push(NocturneText.T("Пет", "Pet"), petting ? NocturnePet.Stop2() : NocturnePet.Grab(pc), 2.2f, NocturneNotifyKind.Info);
        bool chasing = NocturnePet.IsFollow(pid);
        if (CardBtn(x, y, bw, 2, NocturneText.T("ПЁС ЗА", "PET CHASE"), chasing ? CGold : CBlue))
            NocturneToast.Push(NocturneText.T("Пет", "Pet"), chasing ? NocturnePet.Stop2() : NocturnePet.Chase(pc), 2.2f, NocturneNotifyKind.Info);
        y += 30f;

        if (CardBtn(x, y, bw, 0, NocturneText.T("ТАСКИ+", "TASKS+"), CGray))
            NocturneToast.Push(NocturneText.T("Таски", "Tasks"), TaskTools.Flood(pc), 2.2f, NocturneNotifyKind.Info);
        if (CardBtn(x, y, bw, 1, NocturneText.T("ТАСКИ−", "TASKS−"), CGray))
            NocturneToast.Push(NocturneText.T("Таски", "Tasks"), TaskTools.Clear(pc), 2.2f, NocturneNotifyKind.Info);
        if (CardBtn(x, y, bw, 2, NocturneText.T("НОРМА", "NORMAL"), CGray))
            NocturneToast.Push(NocturneText.T("Таски", "Tasks"), TaskTools.Normal(pc), 2.2f, NocturneNotifyKind.Info);
        y += 30f;
    }

    private void CardLook(float x, ref float y, float w, PlayerControl pc)
    {
        float bw = (w - 16f) / 3f;

        if (CardBtn(x, y, bw, 0, NocturneText.T("МОРФ→Я", "MORPH→ME"), CPurple))
            NocturneToast.Push(NocturneText.T("Морф", "Morph"), NocturneMorph.Into(pc, PlayerControl.LocalPlayer), 2f, NocturneNotifyKind.Info);
        if (CardBtn(x, y, bw, 1, NocturneText.T("ОБЛИК→Я", "LOOK→ME"), CPurple))
        {
            NocturneOutfitApplier.Borrow(pc);
            NocturneToast.Push(NocturneText.T("Облик", "Look"), NocturneText.T("Скопирован.", "Copied."), 2f, NocturneNotifyKind.Success);
        }
        if (CardBtn(x, y, bw, 2, NocturneText.T("РАНДОМ", "RANDOM"), CGray))
            NocturneToast.Push(NocturneText.T("Облик", "Look"), NocturneOutfits.Randomize(pc), 2f, NocturneNotifyKind.Info);
        y += 30f;

        if (CardBtn(x, y, bw, 0, NocturneText.T("ВЕРНУТЬ", "RESTORE"), NocturneOutfitApplier.Borrowed ? CGold : CGray))
        {
            NocturneOutfitApplier.Restore();
            NocturneToast.Push(NocturneText.T("Облик", "Look"), NocturneText.T("Возвращён.", "Restored."), 2f, NocturneNotifyKind.Info);
        }
        if (CardBtn(x, y, bw, 1, NocturneText.T("КЛОН", "CLONE"), CBlue))
            NocturneToast.Push(NocturneText.T("Клон", "Clone"), NocturneTwins.CloneOf(pc), 2.2f, NocturneNotifyKind.Info);
        if (CardBtn(x, y, bw, 2, NocturneText.T("ИСЧЕЗ", "VANISH"), CBlue))
        {
            PhantomPoof.Vanish(pc);
            NocturneToast.Push(NocturneText.T("Фантом", "Phantom"), NocturneText.T("Исчез.", "Vanished."), 2f, NocturneNotifyKind.Info);
        }
        y += 30f;

        if (CardBtn(x, y, bw, 0, NocturneText.T("ЦВЕТ", "COLOR"), CPurple))
        {
            int cn = Palette.PlayerColors != null ? Palette.PlayerColors.Length : 12;
            NocturneOutfits.SetColor(pc, UnityEngine.Random.Range(0, cn));
            NocturneToast.Push(NocturneText.T("Облик", "Look"), NocturneText.T("Цвет сменён.", "Color changed."), 2f, NocturneNotifyKind.Info);
        }
        if (CardBtn(x, y, bw, 1, NocturneText.T("ПОЯВИТЬ", "APPEAR"), CBlue))
        {
            PhantomPoof.Appear(pc);
            NocturneToast.Push(NocturneText.T("Фантом", "Phantom"), NocturneText.T("Появился.", "Appeared."), 2f, NocturneNotifyKind.Info);
        }
        if (CardBtn(x, y, bw, 2, NocturneText.T("ЗАРЯД", "CHARGE"), CGray))
        {
            PhantomPoof.Charge(pc);
            NocturneToast.Push(NocturneText.T("Фантом", "Phantom"), NocturneText.T("Заряжен.", "Charged."), 2f, NocturneNotifyKind.Info);
        }
        y += 30f;
    }

    private void CardAttack(float x, ref float y, float w, PlayerControl pc)
    {
        float bw = (w - 16f) / 3f;

        if (CardBtn(x, y, bw, 0, NocturneText.T("УБИТЬ", "KILL"), CRed))
            NocturneToast.Push(NocturneText.T("Убийство", "Kill"), NocturneKillTools.KillOne(pc), 2.2f, NocturneNotifyKind.Warning);
        if (CardBtn(x, y, bw, 1, NocturneText.T("ТЕЛЕКИЛЛ", "TELEKILL"), CRed))
            NocturneToast.Push(NocturneText.T("Убийство", "Kill"), NocturneKillTools.Telekill(pc), 2.2f, NocturneNotifyKind.Warning);
        if (CardBtn(x, y, bw, 2, NocturneText.T("ЭНДЕР", "ENDER"), CRed))
            NocturneToast.Push(NocturneText.T("Эндермен", "Enderman"), NocturneEnderman.Kill(pc), 2.2f, NocturneNotifyKind.Warning);
        y += 30f;

        if (CardBtn(x, y, bw, 0, NocturneText.T("ВЫКИНУТЬ", "EJECT"), CRed))
            NocturneToast.Push(NocturneText.T("Выброс", "Eject"), NocturneMeetingTools.Eject(pc), 2.5f, NocturneNotifyKind.Warning);
        if (CardBtn(x, y, bw, 1, NocturneText.T("РЕПОРТ", "REPORT"), COrange))
            NocturneToast.Push(NocturneText.T("Собрание", "Meeting"), NocturneMeetingTools.RequestMeeting(PlayerControl.LocalPlayer, pc.Data), 2.5f, NocturneNotifyKind.Info);
        if (CardBtn(x, y, bw, 2, NocturneText.T("ЦИКЛ×3", "LOOP×3"), CRed))
            NocturneToast.Push(NocturneText.T("Пранк", "Prank"), NocturneLobbyPranks.MurderLoop(pc, 3), 2.5f, NocturneNotifyKind.Warning);
        y += 30f;
    }

    private void CardMod(float x, ref float y, float w, PlayerControl pc, InnerNetClient net, ClientData c)
    {
        float bw = (w - 16f) / 3f;
        string mfc = NocturneColorReservations.Fc(pc);

        if (CardBtn(x, y, bw, 0, NocturneText.T("ВОЙТКИК", "VOTEKICK"), COrange))
            NocturneVotekick.VoteOne(pc);
        bool muted = NocturneMuteList.IsMuted(mfc);
        if (CardBtn(x, y, bw, 1, NocturneText.T("МУТ", "MUTE"), muted ? CRed : CGray))
            NocturneMuteList.Toggle(mfc);
        if (CardBtn(x, y, bw, 2, NocturneText.T("КИК", "KICK"), COrange))
        {
            if (c != null && net != null)
                NocturneAccess.Kick(net, c.Id, false);
            else
                NoClientToast();
        }
        y += 30f;

        if (CardBtn(x, y, bw, 0, NocturneText.T("НИК-БАН", "NICK BAN"), COrange))
        {
            if (c != null && net != null)
                NocturneAccess.NickBanClient(net, c);
            else
                NoClientToast();
        }
        if (CardBtn(x, y, bw, 1, NocturneText.T("БАН", "BAN"), CRed))
        {
            if (c != null && net != null)
                NocturneAccess.BanClient(net, c);
            else
                NoClientToast();
        }
        if (CardBtn(x, y, bw, 2, NocturneText.T("ВАЙТ", "WHITE"), NocturneStyle.Current.Accent))
        {
            if (c != null)
                NocturneAccess.WhiteClient(c);
            else
                NoClientToast();
        }
        y += 30f;
    }

    private byte _livePid = 255;
    private float _liveAt = -99f;
    private string _liveSeen = "—";

    private void LiveRefresh(PlayerControl pc, ClientData c)
    {
        float now = NocturneStyle.Now;
        if (pc.PlayerId == _livePid && now - _liveAt < 0.4f)
            return;

        _livePid = pc.PlayerId;
        _liveAt = now;
        _liveSeen = "—";
        if (c != null && NocturneNameHistory.KnownBefore(c, out string since, out string knownNick))
            _liveSeen = since + (knownNick.Length > 0 && knownNick != (pc.Data != null ? pc.Data.PlayerName : string.Empty) ? " · " + knownNick : string.Empty);
    }

    private const int LobbyHistShown = 8;

    private void LobbyHistoryCard(float x, ref float y, float w)
    {
        IReadOnlyList<LobbyRow> rows = LobbyHistory.Entries;
        int n = Mathf.Min(rows.Count, LobbyHistShown);
        float body = 30f + (n > 0 ? n * 44f : 26f);

        Rect b = Card(x, ref y, w, $"{NocturneText.T("История лобби", "Lobby history")} ({rows.Count})", body, true);
        float by = b.y;

        if (SmallButton(new Rect(b.x + b.width - 96f, by, 92f, 24f), NocturneText.T("ОЧИСТИТЬ", "CLEAR"), CRed))
            LobbyHistory.Clear();
        Lab(new Rect(b.x + 2f, by, b.width - 104f, 24f), NocturneText.T("Где играл: код, хост, карта.", "Where you played: code, host, map."), _muted);
        by += 30f;

        if (n == 0)
        {
            Lab(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Пусто.", "Empty."), _muted);
            return;
        }

        for (int i = 0; i < n; i++)
            LobbyHistRow(b.x, ref by, b.width, rows[i]);
    }

    private void LobbyHistRow(float x, ref float y, float w, LobbyRow r)
    {
        if (RowCull(ref y, 42f, 44f))
            return;

        var box = new Rect(x, y, w, 42f);
        HoverFill(box);

        if (NocturneStyle.Painting)
        {
            Lab(new Rect(box.x + 8f, box.y + 1f, box.width - 190f, 22f), $"<b>{r.Code}</b>   <color=#8A94AC><size=11>{r.When}</size></color>", _rowName);
            string info = r.Host + " · " + r.Map + " · " + r.Players + NocturneText.T(" игр.", " ppl") + " · " + r.Region + (r.Public ? NocturneText.T(" · публ.", " · public") : string.Empty);
            Lab(new Rect(box.x + 8f, box.y + 21f, box.width - 190f, 20f), info, _rowInfo);
        }

        float by = box.y + (box.height - 24f) / 2f;
        if (SmallButton(new Rect(box.xMax - 176f, by, 84f, 24f), NocturneText.T("КОПИЯ", "COPY"), CGray))
        {
            try
            {
                GUIUtility.systemCopyBuffer = r.Code;
                NocturneToast.Push(NocturneText.T("Лобби", "Lobby"), NocturneText.T("Код скопирован.", "Code copied."), 2f, NocturneNotifyKind.Success);
            }
            catch { }
        }
        if (SmallButton(new Rect(box.xMax - 88f, by, 84f, 24f), NocturneText.T("ЗАЙТИ", "JOIN"), NocturneStyle.Current.Accent))
            LobbyHistory.Rejoin(r);

        y += 44f;
    }

    private float _copyFlash = -9f;
    private string _copyRow;

    private void CardKvCopy(float x, ref float y, float w, string label, string value, string copy)
    {
        var r = new Rect(x, y, w, 24f);
        bool can = !string.IsNullOrEmpty(copy);

        if (NocturneStyle.Painting)
        {
            float since = NocturneStyle.Now - _copyFlash;
            if (_copyRow == label && since < 0.45f)
                NocturneStyle.FillRounded(r, A(NocturneStyle.Current.Accent, 0.3f * (1f - since / 0.45f)), 6);
            else if (can && r.Contains(NocturneStyle.Mouse))
                NocturneStyle.FillRounded(r, A(Color.white, 0.05f), 6);
        }

        Lab(new Rect(x + 2f, y, w * 0.4f, 24f), label, _muted);
        Lab(new Rect(x + w * 0.4f, y, w * 0.6f - 2f, 24f), value, _cardVal);

        if (can && GUI.Button(r, GUIContent.none, _invisible))
        {
            try
            {
                GUIUtility.systemCopyBuffer = copy;
                _copyFlash = NocturneStyle.Now;
                _copyRow = label;
                NocturneToast.Push(label, NocturneText.T("Скопирован.", "Copied."), 2f, NocturneNotifyKind.Success);
            }
            catch { }
        }

        y += 26f;
    }

    private void CardKv(float x, ref float y, float w, string label, string value)
    {
        Lab(new Rect(x + 2f, y, w * 0.4f, 24f), label, _muted);
        Lab(new Rect(x + w * 0.4f, y, w * 0.6f - 2f, 24f), value, _cardVal);
        y += 26f;
    }

    private void CardInfo(float x, ref float y, float w, PlayerControl pc, ClientData c)
    {
        string fc = c != null ? (c.FriendCode ?? string.Empty).Trim() : NocturneColorReservations.Fc(pc);
        string puid = c != null ? (c.ProductUserId ?? string.Empty).Trim() : string.Empty;
        string lvl = c != null ? Patches.NocturneJoinLevels.Display(c) : Patches.NocturneJoinLevels.Display(pc);
        string plat = string.Empty;
        if (c != null && c.PlatformData != null)
        {
            plat = Plat(c.PlatformData.Platform);
            string raw = NocturneAccess.SafePlatform(c);
            if (raw.Length > 0 && raw != plat && raw != "TESTNAME")
                plat = plat.Length > 0 ? plat + " · " + raw : raw;
        }

        LiveRefresh(pc, c);

        string name = pc.Data != null ? pc.Data.PlayerName : "?";
        string ids = c != null ? pc.PlayerId + " · " + c.Id : pc.PlayerId.ToString();

        CardKv(x, ref y, w, NocturneText.T("Ник", "Name"), name);
        CardKv(x, ref y, w, "ID", ids);
        CardKvCopy(x, ref y, w, NocturneText.T("Френдкод", "Friend code"), fc.Length > 0 ? fc : "—", fc);

        Lab(new Rect(x + 2f, y, w * 0.3f, 24f), "PUID", _muted);
        Lab(new Rect(x + w * 0.3f, y, w * 0.7f - 42f, 24f), puid.Length > 12 ? puid.Substring(0, 11) + "…" : (puid.Length > 0 ? puid : "—"), _cardVal);
        if (puid.Length > 0 && SmallButton(new Rect(x + w - 38f, y, 38f, 24f), NocturneText.T("КОП", "COPY"), CGray))
        {
            try
            {
                GUIUtility.systemCopyBuffer = puid;
                NocturneToast.Push("PUID", NocturneText.T("Скопирован.", "Copied."), 2f, NocturneNotifyKind.Success);
            }
            catch { }
        }
        y += 26f;

        CardKv(x, ref y, w, NocturneText.T("Уровень", "Level"), lvl);
        CardKv(x, ref y, w, NocturneText.T("Платформа", "Platform"), plat.Length > 0 ? plat : "—");

        string mod = ModHandshake.ModOf(pc.PlayerId) ?? ForeignMods.Name(pc.PlayerId);
        CardKv(x, ref y, w, "Mod Client", mod != null ? mod : NocturneText.T("нет", "no"));

        int nicks = NocturneNameHistory.KnownNickCount(pc);
        string vk = c != null ? NocturneVoteTally.Count(c.Id).ToString() : "0";
        CardKv(x, ref y, w, NocturneText.T("Ники · ВК", "Nicks · VK"), (nicks > 0 ? nicks : 1) + " · " + vk);

        CardKv(x, ref y, w, NocturneText.T("Знаком", "Seen before"), _liveSeen);

        string st = string.Empty;
        if (NocturneAccess.IsBanned(fc, puid))
            st = NocturneText.T("в бане", "banned");
        else if (NocturneAccess.IsWhite(fc, puid))
            st = NocturneText.T("вайтлист", "whitelist");
        if (NocturneMuteList.IsMuted(fc))
            st = st.Length > 0 ? st + " · " + NocturneText.T("мут", "muted") : NocturneText.T("мут", "muted");
        CardKv(x, ref y, w, NocturneText.T("Статус", "Status"), st.Length > 0 ? st : "—");

        y += 2f;
        if (SmallButton(new Rect(x + 2f, y, w - 4f, 24f), NocturneText.T("КОПИРОВАТЬ ВСЁ", "COPY ALL"), CBlue))
            CopyCard(name, ids, fc, puid, lvl, plat, mod, nicks, vk, st);
        y += 26f;
    }

    private void CopyCard(string name, string ids, string fc, string puid, string lvl, string plat, string mod, int nicks, string vk, string st)
    {
        string txt = NocturneText.T("Ник", "Name") + ": " + name + "\n"
            + "ID: " + ids + "\n"
            + NocturneText.T("Френдкод", "Friend code") + ": " + (fc.Length > 0 ? fc : "—") + "\n"
            + "PUID: " + (puid.Length > 0 ? puid : "—") + "\n"
            + NocturneText.T("Уровень", "Level") + ": " + lvl + "\n"
            + NocturneText.T("Платформа", "Platform") + ": " + (plat.Length > 0 ? plat : "—") + "\n"
            + "Mod Client: " + (mod != null ? mod : NocturneText.T("нет", "no")) + "\n"
            + NocturneText.T("Ники · ВК", "Nicks · VK") + ": " + (nicks > 0 ? nicks : 1) + " · " + vk + "\n"
            + NocturneText.T("Знаком", "Seen before") + ": " + _liveSeen + "\n"
            + NocturneText.T("Статус", "Status") + ": " + (st.Length > 0 ? st : "—");

        try
        {
            GUIUtility.systemCopyBuffer = txt;
            NocturneToast.Push(NocturneText.T("Карточка", "Card"), NocturneText.T("Инфо скопировано.", "Info copied."), 2f, NocturneNotifyKind.Success);
        }
        catch { }
    }

    private static void NoClientToast()
    {
        NocturneToast.Push(NocturneText.T("Игрок", "Player"), NocturneText.T("Нет данных клиента.", "No client data."), 2.2f, NocturneNotifyKind.Warning);
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
                if (!string.IsNullOrEmpty(net.networkAddress))
                    server = net.networkAddress + ":" + net.networkPort;
            }
        }
        catch
        {
            ping = -1;
        }

        Rect b = Card(x, ref y, w, NocturneText.T("Соединение", "Connection"), 4f * 30f);
        float by = b.y;
        InfoRow(b.x, ref by, b.width, NocturneText.T("Пинг", "Ping"), ping >= 0 ? ping + " ms" : "—");
        InfoRow(b.x, ref by, b.width, "FPS", NocturneHud.CurrentFps.ToString());
        InfoRow(b.x, ref by, b.width, NocturneText.T("Сервер", "Server"), server);
        InfoRow(b.x, ref by, b.width, NocturneText.T("Лобби", "Lobby"), LobbyBehaviour.Instance != null ? NocturneText.T("в лобби", "in lobby") : NocturneText.T("нет", "no"));

        bool brOn = NocturneConfig.GlichRoomCycle.Value;
        b = Card(x, ref y, w, NocturneText.T("Баг-комнаты", "Glich Rooms"), 3f * RowH + 34f + (brOn ? 76f : 26f));
        by = b.y;
        Toggle(b.x, ref by, b.width, NocturneText.T("Крутить цикл", "Run cycle"), NocturneConfig.GlichRoomCycle);
        Toggle(b.x, ref by, b.width, NocturneText.T("Поиск: новая комната каждый круг", "Hunt: new room each cycle"), NocturneConfig.GlichRoomHunt);
        Toggle(b.x, ref by, b.width, NocturneText.T("Писать найденные в файл", "Log found rooms"), NocturneConfig.GlichRoomLog);

        Lab(new Rect(b.x + 2f, by, 118f, 26f), NocturneText.T("Хвосты кодов", "Code endings"), _rowLabel);
        NocturneConfig.GlichRoomTargets.Value = CustomText(new Rect(b.x + 122f, by, b.width - 124f, 26f), NocturneConfig.GlichRoomTargets.Value ?? "", "bugTails");
        by += 32f;

        if (brOn)
        {
            Slider(b.x, ref by, b.width, NocturneText.T("Пауза", "Delay"), NocturneConfig.GlichRoomDelay, 1f, 10f, "0.0");
            Lab(new Rect(b.x + 2f, by, b.width - 2f, 22f),
                $"{NocturneGlichRooms.Stage}   {NocturneText.T("кругов", "runs")}: <b>{NocturneGlichRooms.Runs}</b>   {NocturneText.T("ур.", "lvl")}: <b>{NocturneGlichRooms.Was}→{NocturneGlichRooms.Lvl}</b>   {NocturneText.T("найдено", "found")}: <b>{NocturneGlichRooms.Hits}</b>", _muted);
        }
        else
            Lab(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Пусто = судить по уровню. Nocturne/GlichRooms.txt", "Empty = judge by level. Nocturne/GlichRooms.txt"), _muted);

        float spoofBody = 6f * RowH + 30f + (NocturneConfig.SpoofLevelEnabled.Value ? 50f : 0f) + (NocturneConfig.SpoofFriendCodeEnabled.Value ? 58f : 0f);
        b = Card(x, ref y, w, NocturneText.T("Спуф", "Spoof"), spoofBody, true);
        by = b.y;
        Toggle(b.x, ref by, b.width, NocturneText.T("Спуф платформы", "Spoof platform"), NocturneConfig.SpoofPlatformEnabled);
        int pi = Mathf.Clamp(NocturneConfig.SpoofPlatformIndex.Value, 0, PlatNames.Length - 1);
        if (CycleRow(b.x, ref by, b.width, NocturneText.T("Платформа", "Platform"), PlatNames[pi]))
            NocturneConfig.SpoofPlatformIndex.Value = (pi + 1) % PlatNames.Length;
        Toggle(b.x, ref by, b.width, NocturneText.T("Спуф уровня", "Spoof level"), NocturneConfig.SpoofLevelEnabled);
        if (NocturneConfig.SpoofLevelEnabled.Value)
            SliderInt(b.x, ref by, b.width, NocturneText.T("Уровень", "Level"), NocturneConfig.SpoofLevelValue, 1, 9999);
        Toggle(b.x, ref by, b.width, NocturneText.T("Спуф Device ID", "Spoof Device ID"), NocturneConfig.SpoofDeviceId);
        Toggle(b.x, ref by, b.width, NocturneText.T("Спуф ФК", "Spoof friend code"), NocturneConfig.SpoofFriendCodeEnabled);
        if (NocturneConfig.SpoofFriendCodeEnabled.Value)
        {
            _fcText = CustomText(new Rect(b.x + 2f, by, b.width - 190f, 26f), _fcText ?? NocturneConfig.SpoofFriendCodeValue.Value ?? "", "fcSpoof", 10);
            if (SmallButton(new Rect(b.x + b.width - 184f, by + 1f, 86f, 24f), NocturneText.T("ЗАДАТЬ", "SET"), NocturneStyle.Current.Accent))
                NocturneConfig.SpoofFriendCodeValue.Value = (_fcText ?? "").Trim();
            if (SmallButton(new Rect(b.x + b.width - 92f, by + 1f, 88f, 24f), NocturneText.T("РАНДОМ", "RANDOM"), new Color(0.5f, 0.55f, 0.62f)))
            {
                _fcText = Patches.NocturneFcSpoof.Random();
                NocturneConfig.SpoofFriendCodeValue.Value = _fcText;
            }
            by += 30f;
            Lab(new Rect(b.x + 4f, by, b.width - 6f, 20f), NocturneText.T("Спуф ФК срабатывает при регистрации акка.", "Spoofs the FC set at account registration."), _muted);
            by += 24f;
        }
        if (SmallButton(new Rect(b.x, by, b.width, 24f), NocturneText.T("РАНДОМ ОБРАЗА", "RANDOM OUTFIT"), NocturneStyle.Current.Accent))
            NocturneToast.Push(NocturneText.T("Образ", "Outfit"), NocturneOutfits.Randomize(PlayerControl.LocalPlayer), 2f, NocturneNotifyKind.Info);
        by += 30f;
        Toggle(b.x, ref by, b.width, NocturneText.T("Рандом → сохранять в профиль", "Random → save to profile"), NocturneConfig.RandomOutfitSave);

    }

    private void DrawVisual(float x, ref float y, float w)
    {
        SubBar(x, ref y, w, VisualSubsRu, VisualSubsEn);

        Grp(1);
        Rect b = Card(x, ref y, w, NocturneText.T("Косметика", "Cosmetics"), 6f * RowH + 6f + (NocturneConfig.SeasonDecor.Value ? 28f : 0f), true);
        float by = b.y;
        Toggle(b.x, ref by, b.width, NocturneText.T("Разблокировать косметику", "Unlock cosmetics"), NocturneConfig.FreeCosmetics);
        Toggle(b.x, ref by, b.width, NocturneText.T("Прятать косметику в матче", "Hide cosmetics in match"), NocturneConfig.HideCosmeticsInMatch);
        Toggle(b.x, ref by, b.width, NocturneText.T("Одинаковые цвета (хост)", "Duplicate colors (host)"), NocturneConfig.AllowDuplicateColors);
        Toggle(b.x, ref by, b.width, NocturneText.T("Классический вид", "Classic look"), NocturneConfig.ClassicBody);
        Toggle(b.x, ref by, b.width, NocturneText.T("Классика 2018 полностью", "Full 2018 classic"), NocturneConfig.ClassicMode);
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
                if (seen > 0)
                    lbl += " " + seen;
                if (SmallButton(new Rect(b.x + i * (cw + 4f), by, cw, 24f), lbl, on ? NocturneStyle.Current.Accent : new Color(0.36f, 0.39f, 0.47f)))
                    NocturneConfig.SeasonDecorMask.Value = mask ^ (1 << i);
            }
            by += 28f;
        }

        Grp(1);
        DrawSnipe(x, ref y, w);
        Grp(1);
        DrawColorAll(x, ref y, w);

        float camBody = 4f * RowH
            + (NocturneConfig.VisualFreeCamera.Value ? 50f : 0f)
            + (NocturneConfig.VisualCameraZoom.Value ? RowH : 0f)
            + (NocturneConfig.WorldTilt.Value ? 50f : 0f);
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
        Toggle(b.x, ref by, b.width, NocturneText.T("Наклон мира — ты прямо (только у тебя)", "World tilt — you stay upright (only you)"), NocturneConfig.WorldTilt);
        if (NocturneConfig.WorldTilt.Value)
            Slider(b.x, ref by, b.width, NocturneText.T("Угол наклона", "Tilt angle"), NocturneConfig.WorldTiltAngle, -180f, 180f, "0");

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
            Lab(new Rect(b.x + 2f, by, b.width - 2f, 22f), NocturneText.T("Кого показывать:", "Who to show:"), _muted);
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
            if (CycleRow(b.x, ref by, b.width, NocturneText.T("Мирные", "Crewmates"), NocturneTracers.SideName(NocturneConfig.EspCrewColor.Value)))
                NocturneConfig.EspCrewColor.Value = (NocturneConfig.EspCrewColor.Value + 1) % NocturneTracers.SideCount;
            if (CycleRow(b.x, ref by, b.width, NocturneText.T("Предатели", "Impostors"), NocturneTracers.SideName(NocturneConfig.EspImpColor.Value)))
                NocturneConfig.EspImpColor.Value = (NocturneConfig.EspImpColor.Value + 1) % NocturneTracers.SideCount;
            if (CycleRow(b.x, ref by, b.width, NocturneText.T("Мёртвое тело", "Dead body"), NocturneTracers.SideName(NocturneConfig.EspDeadColor.Value)))
                NocturneConfig.EspDeadColor.Value = (NocturneConfig.EspDeadColor.Value + 1) % NocturneTracers.SideCount;
        }
        Toggle(b.x, ref by, b.width, NocturneText.T("КД килла над убийцами", "Kill CD over killers"), NocturneConfig.KillTimers);
        Toggle(b.x, ref by, b.width, NocturneText.T("Сообщения над игроками", "Chat above players"), NocturneConfig.OverheadChat);
        if (NocturneConfig.OverheadChat.Value)
        {
            string[] ocWhere = NocturneText.IsRussian ? OcWhereRu : OcWhereEn;
            int ocw = Mathf.Clamp(NocturneConfig.OverheadChatWhere.Value, 0, 2);
            if (CycleRow(b.x, ref by, b.width, NocturneText.T("Показывать", "Show in"), ocWhere[ocw]))
                NocturneConfig.OverheadChatWhere.Value = (ocw + 1) % 3;
            SliderInt(b.x, ref by, b.width, NocturneText.T("Держать сообщение", "Message time"), NocturneConfig.OverheadChatTime, 2, 15);
        }
        Toggle(b.x, ref by, b.width, NocturneText.T("Неон-обводка игроков", "Neon outline"), NocturneConfig.NeonOutline);
        if (NocturneConfig.NeonOutline.Value)
        {
            string[] noModes = NocturneText.IsRussian ? NoModesRu : NoModesEn;
            int nm = Mathf.Clamp(NocturneConfig.NeonOutlineMode.Value, 0, 2);
            if (CycleRow(b.x, ref by, b.width, NocturneText.T("Цвет обводки", "Outline color"), noModes[nm]))
                NocturneConfig.NeonOutlineMode.Value = (nm + 1) % 3;
        }

        Grp(0);
        b = Card(x, ref y, w, NocturneText.T("Обзор", "Vision"), 3f * RowH + 22f);
        by = b.y;
        Toggle(b.x, ref by, b.width, NocturneText.T("Видеть игроков в вентах", "See players in vents"), NocturneConfig.SeeVents);
        Toggle(b.x, ref by, b.width, NocturneText.T("Видеть призраков", "See ghosts"), NocturneConfig.SeeGhosts);
        Toggle(b.x, ref by, b.width, NocturneText.T("Wallhack (без тьмы)", "Wallhack (no darkness)"), NocturneConfig.Wallhack);
        Lab(new Rect(b.x + 2f, by, b.width - 2f, 20f), NocturneText.T("Убирает завесу обзора — видно всю карту и всех.", "Removes the vision veil — see the whole map and everyone."), _muted);

        Grp(0);
        b = Card(x, ref y, w, NocturneText.T("Карта", "Map"), RowH + 26f);
        by = b.y;
        Toggle(b.x, ref by, b.width, NocturneText.T("Dleks (перевёрнутый Skeld)", "Dleks (mirrored Skeld)"), NocturneConfig.Dleks);
        Lab(new Rect(b.x + 2f, by, b.width - 2f, 20f), NocturneText.T("Локально, применится на следующей карте.", "Local, applies on next map load."), _muted);

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
        Lab(new Rect(b.x + 2f, by, b.width - 2f, 20f), NocturneText.T("Двигать — ЛКМ по радару. Скелет карты + точки игроков.", "Drag with LMB. Map skeleton + player dots."), _muted);

        Grp(0);
        b = Card(x, ref y, w, NocturneText.T("Разбор матча (карта)", "Match review (map)"), RowH * 3f + 24f);
        by = b.y;
        Toggle(b.x, ref by, b.width, NocturneText.T("Окно разбора", "Review window"), NocturneConfig.ReplayView);
        Toggle(b.x, ref by, b.width, NocturneText.T("Чистить после собрания", "Clear after meeting"), NocturneConfig.ReplayClearAfterMeeting);
        KeyRow(b.x, ref by, b.width, NocturneText.T("Открыть разбор", "Open review"), NocturneConfig.ReplayKey);
        Lab(new Rect(b.x + 2f, by, b.width - 2f, 20f), NocturneText.T("Пути + события на карте. Виден и в лобби после раунда.", "Paths + events on a map. Visible in the lobby after a round."), _muted);

        Grp(2);
        bool ant = NocturneConfig.AntWalk.Value;
        b = Card(x, ref y, w, NocturneText.T("Анимации (на себе)", "Animations (self)"), 6f * 28f + 24f + RowH * (ant ? 4f : 2f));
        by = b.y;
        Toggle(b.x, ref by, b.width, NocturneText.T("Лунная походка", "Moonwalk"), NocturneConfig.WalkNoAnim);
        Toggle(b.x, ref by, b.width, NocturneText.T("Муравьиный шаг", "Ant walk"), NocturneConfig.AntWalk);
        if (ant)
        {
            Slider(b.x, ref by, b.width, NocturneText.T("Шаг между рывками", "Step between twitches"), NocturneConfig.AntWalkStep, 0.1f, 1.2f, "0.00");
            Slider(b.x, ref by, b.width, NocturneText.T("Длина рывка", "Twitch length"), NocturneConfig.AntWalkTwitch, 0.03f, 0.15f, "0.00");
        }
        Color anac = NocturneStyle.Current.Accent;
        Color anon = new Color(0.32f, 0.78f, 0.45f);
        float anbw = (b.width - 6f) / 2f;
        if (SmallButton(new Rect(b.x, by, anbw, 24f), NocturneText.T("Лезть ↑", "Climb ↑"), NocturneAnimations.Active(NocturneAnim.ClimbUp) ? anon : anac))
            NocturneAnimations.Toggle(NocturneAnim.ClimbUp);
        if (SmallButton(new Rect(b.x + anbw + 6f, by, anbw, 24f), NocturneText.T("Лезть ↓", "Climb ↓"), NocturneAnimations.Active(NocturneAnim.ClimbDown) ? anon : anac))
            NocturneAnimations.Toggle(NocturneAnim.ClimbDown);
        by += 28f;
        if (SmallButton(new Rect(b.x, by, anbw, 24f), NocturneText.T("В люк", "Enter vent"), NocturneAnimations.Active(NocturneAnim.EnterVent) ? anon : anac))
            NocturneAnimations.Toggle(NocturneAnim.EnterVent);
        if (SmallButton(new Rect(b.x + anbw + 6f, by, anbw, 24f), NocturneText.T("Из люка", "Exit vent"), NocturneAnimations.Active(NocturneAnim.ExitVent) ? anon : anac))
            NocturneAnimations.Toggle(NocturneAnim.ExitVent);
        by += 28f;
        if (SmallButton(new Rect(b.x, by, anbw, 24f), NocturneText.T("Прыжок", "Jump"), NocturneAnimations.Active(NocturneAnim.Jump) ? anon : anac))
            NocturneAnimations.Toggle(NocturneAnim.Jump);
        if (SmallButton(new Rect(b.x + anbw + 6f, by, anbw, 24f), NocturneText.T("Спавн", "Spawn"), NocturneAnimations.Active(NocturneAnim.Spawn) ? anon : anac))
            NocturneAnimations.Toggle(NocturneAnim.Spawn);
        by += 28f;
        if (SmallButton(new Rect(b.x, by, anbw, 24f), NocturneText.T("Туман вкл", "Fog on"), anac))
            NocturneAnimations.MushroomIn();
        if (SmallButton(new Rect(b.x + anbw + 6f, by, anbw, 24f), NocturneText.T("Туман выкл", "Fog off"), anac))
            NocturneAnimations.MushroomOut();
        by += 28f;
        if (SmallButton(new Rect(b.x, by, anbw, 24f), NocturneText.T("Вспышка", "Alert flash"), anac))
            NocturneAnimations.AlertFlash();
        if (SmallButton(new Rect(b.x + anbw + 6f, by, anbw, 24f), NocturneText.T("Звук митинга", "Meeting sting"), anac))
            NocturneAnimations.MeetingSting();
        by += 28f;
        if (SmallButton(new Rect(b.x, by, anbw, 24f), NocturneText.T("Звук выброса", "Eject sfx"), anac))
            NocturneAnimations.EjectSfx();
        if (SmallButton(new Rect(b.x + anbw + 6f, by, anbw, 24f), NocturneText.T("Сброс", "Reset"), new Color(0.85f, 0.32f, 0.32f)))
            NocturneAnimations.ResetAll();
        by += 28f;
        Lab(new Rect(b.x + 2f, by, b.width - 2f, 20f), NocturneText.T("Тумблеры держат позу, «Сброс» вернёт норму. Для роликов.", "Toggles hold the pose, 'Reset' restores. For clips."), _muted);

        Grp(2);
        b = Card(x, ref y, w, NocturneText.T("Стелс", "Stealth"), RowH);
        by = b.y;
        Toggle(b.x, ref by, b.width, NocturneText.T("Скрыть MOD-штамп", "Hide MOD stamp"), NocturneConfig.HideModStamp);

        Grp(2);
        b = Card(x, ref y, w, NocturneText.T("Пропуск анимаций", "Skip animations"), RowH);
        by = b.y;
        Toggle(b.x, ref by, b.width, NocturneText.T("Интро «Shhh»", "'Shhh' intro"), NocturneConfig.SkipShhh);

        Grp(1);
        _roleClients.Clear();
        CollectPlayers(_roleClients);
        int outfitN = 0;
        for (int i = 0; i < _roleClients.Count; i++)
            if (_roleClients[i] != Me())
                outfitN++;
        bool picking = _outfitPickSlot >= 0;
        float pickH = picking ? (24f + (outfitN > 0 ? outfitN * 32f : 26f) + 28f) : 0f;
        b = Card(x, ref y, w, NocturneText.T("Избранные образы", "Favorite outfits"), 4f * 30f + 60f + pickH, true);
        by = b.y;
        for (int i = 0; i < 4; i++)
            FavoriteRow(b.x, ref by, b.width, i);
        if (picking)
        {
            Lab(new Rect(b.x + 2f, by, b.width - 2f, 22f), NocturneText.T($"Взять образ в слот {_outfitPickSlot + 1}:", $"Copy outfit into slot {_outfitPickSlot + 1}:"), _muted);
            by += 24f;
            if (outfitN == 0)
            {
                Lab(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Нет других игроков.", "No other players."), _muted);
                by += 26f;
            }
            else
            {
                for (int i = 0; i < _roleClients.Count; i++)
                    if (_roleClients[i] != Me())
                        OutfitPickRow(b.x, ref by, b.width, _roleClients[i]);
            }
            if (SmallButton(new Rect(b.x, by, b.width, 24f), NocturneText.T("ЗАКРЫТЬ СПИСОК", "CLOSE LIST"), new Color(0.5f, 0.55f, 0.62f)))
                _outfitPickSlot = -1;
            by += 28f;
        }
        float cwHalf = (b.width - 8f) / 2f;
        if (SmallButton(new Rect(b.x, by, cwHalf, 24f), NocturneText.T("ОБРАЗ ВЫБР. → МНЕ", "OUTFIT → ME"), NocturneStyle.Current.Accent))
        {
            string cap = NocturneOutfits.Capture(NocturneMouseTools.Selected);
            NocturneToast.Push(NocturneText.T("Образ", "Outfit"), cap.Length > 0 && NocturneOutfits.Apply(PlayerControl.LocalPlayer, cap) ? NocturneText.T("скопирован", "copied") : NocturneText.T("нет цели", "no target"), 2f, NocturneNotifyKind.Info);
        }
        if (SmallButton(new Rect(b.x + cwHalf + 8f, by, cwHalf, 24f), NocturneText.T("МОРФ В ВЫБР. (хост)", "MORPH INTO (host)"), NocturneStyle.Current.Accent))
            NocturneToast.Push(NocturneText.T("Морф", "Morph"), NocturneMorph.Into(PlayerControl.LocalPlayer, NocturneMouseTools.Selected), 2.2f, NocturneNotifyKind.Info);
        by += 28f;
        Lab(new Rect(b.x + 2f, by, b.width - 2f, 22f), NocturneText.T("«ВЫБР.» — выбрать из списка. Кнопки снизу — цель мышью.", "'SEL.' — pick from list. Buttons below use mouse target."), _muted);

        Grp(1);
        _roleClients.Clear();
        CollectPlayers(_roleClients);
        int qoN = 0;
        for (int i = 0; i < _roleClients.Count; i++)
            if (_roleClients[i] != Me())
                qoN++;
        bool borrowed = NocturneOutfitApplier.Borrowed;
        float qoBody = 2f * RowH + 24f + (qoN > 0 ? qoN * 32f : 26f) + (borrowed ? 28f : 0f);
        b = Card(x, ref y, w, NocturneText.T("Быстрый образ", "Quick outfit"), qoBody);
        by = b.y;
        Toggle(b.x, ref by, b.width, NocturneText.T("Сброс после матча", "Reset after match"), NocturneConfig.QuickOutfitResetMatch);
        Toggle(b.x, ref by, b.width, NocturneText.T("Сброс после выхода из лобби", "Reset after leaving lobby"), NocturneConfig.QuickOutfitResetLobby);
        Lab(new Rect(b.x + 2f, by, b.width - 2f, 22f), NocturneText.T("Взять образ игрока на себя (временно):", "Borrow a player's outfit (temporary):"), _muted);
        by += 24f;
        if (qoN == 0)
        {
            Lab(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Нет других игроков.", "No other players."), _muted);
            by += 26f;
        }
        else
        {
            for (int i = 0; i < _roleClients.Count; i++)
                if (_roleClients[i] != Me())
                    QuickOutfitRow(b.x, ref by, b.width, _roleClients[i]);
        }
        if (borrowed)
        {
            if (SmallButton(new Rect(b.x, by, b.width, 24f), NocturneText.T("ВЕРНУТЬ СВОЙ ОБРАЗ", "RESTORE MY OUTFIT"), NocturneStyle.Current.Accent))
                NocturneOutfitApplier.Restore();
            by += 28f;
        }

        Grp(1);
        b = Card(x, ref y, w, NocturneText.T("Режим тела", "Body mode"), RowH);
        by = b.y;
        string[] bmVals = BmVals;
        string[] bmDisp = NocturneText.IsRussian ? BmDispRu : BmDispEn;
        int bi = Mathf.Max(0, Array.IndexOf(bmVals, NocturneConfig.BodyMode.Value));
        if (CycleRow(b.x, ref by, b.width, NocturneText.T("Стиль тела", "Body style"), bmDisp[bi]))
            NocturneConfig.BodyMode.Value = bmVals[(bi + 1) % bmVals.Length];

        Grp(1);
        b = Card(x, ref y, w, NocturneText.T("Цветной ник", "Colored name"), 3f * RowH);
        by = b.y;
        Toggle(b.x, ref by, b.width, NocturneText.T("Включить цветной ник", "Enable colored name"), NocturneConfig.NameColor);
        int nci = NocturneNameColor.Clamp(NocturneConfig.NameColorStyle.Value);
        if (CycleRow(b.x, ref by, b.width, NocturneText.T("Стиль", "Style"), NocturneNameColor.StyleName(nci)))
            NocturneConfig.NameColorStyle.Value = NocturneNameColor.Next(nci);
        Toggle(b.x, ref by, b.width, NocturneText.T("Анимация", "Animation"), NocturneConfig.NameColorAnimated);
    }

    private void FavoriteRow(float x, ref float y, float w, int i)
    {
        ConfigEntry<string> slot = i < NocturneConfig.FavoriteOutfits.Length ? NocturneConfig.FavoriteOutfits[i] : null;
        string data = slot != null ? slot.Value : "";
        bool has = !string.IsNullOrWhiteSpace(data);
        var r = new Rect(x, y, w, 26f);
        HoverFill(r);
        Lab(new Rect(r.x + 4f, r.y, 52f, 26f), NocturneText.T($"Слот {i + 1}", $"Slot {i + 1}"), _rowLabel);
        Lab(new Rect(r.x + 58f, r.y, 88f, 26f), has ? NocturneOutfits.Summary(data) : NocturneText.T("пусто", "empty"), _muted);

        var apply = new Rect(r.xMax - 208f, r.y + 1f, 66f, 24f);
        var mine = new Rect(r.xMax - 138f, r.y + 1f, 48f, 24f);
        var sel = new Rect(r.xMax - 86f, r.y + 1f, 52f, 24f);
        var clr = new Rect(r.xMax - 30f, r.y + 1f, 26f, 24f);
        if (SmallButton(apply, NocturneText.T("НАДЕТЬ", "APPLY"), has ? NocturneStyle.Current.Accent : new Color(0.5f, 0.55f, 0.62f)) && has)
            NocturneToast.Push(NocturneText.T("Образ", "Outfit"), NocturneOutfits.Apply(PlayerControl.LocalPlayer, data) ? NocturneText.T("надет", "applied") : NocturneText.T("не готов", "not ready"), 2f, NocturneNotifyKind.Info);
        if (SmallButton(mine, NocturneText.T("МОЙ", "MINE"), new Color(0.5f, 0.55f, 0.62f)) && slot != null)
        {
            string cap = NocturneOutfits.Capture(PlayerControl.LocalPlayer);
            if (cap.Length > 0)
                slot.Value = cap;
        }
        if (SmallButton(sel, NocturneText.T("ВЫБР.", "SEL."), _outfitPickSlot == i ? NocturneStyle.Current.Accent : new Color(0.5f, 0.55f, 0.62f)) && slot != null)
        {
            _outfitPickSlot = _outfitPickSlot == i ? -1 : i;
            NocturneToast.Push(NocturneText.T("Образ", "Outfit"), _outfitPickSlot == i ? NocturneText.T($"Слот {i + 1}: выбери игрока", $"Slot {i + 1}: pick a player") : NocturneText.T("список закрыт", "list closed"), 1.6f, NocturneNotifyKind.Info);
        }
        if (SmallButton(clr, "✕", new Color(0.9f, 0.4f, 0.4f)) && slot != null)
            slot.Value = "";
        y += 30f;
    }

    private void OutfitPickRow(float x, ref float y, float w, PlayerControl pc)
    {
        if (RowCull(ref y, 30f, 32f))
            return;
        var r = new Rect(x, y, w, 30f);
        HoverFill(r);
        DrawColorDot(new Rect(r.x + 6f, r.y + 9f, 12f, 12f), pc);
        Lab(new Rect(r.x + 26f, r.y, r.width - 110f, r.height), pc.Data != null ? pc.Data.PlayerName : "?", _rowName);
        if (SmallButton(new Rect(r.xMax - 100f, r.y + 3f, 96f, 24f), NocturneText.T("ВЗЯТЬ", "TAKE"), NocturneStyle.Current.Accent))
        {
            ConfigEntry<string> slot = _outfitPickSlot >= 0 && _outfitPickSlot < NocturneConfig.FavoriteOutfits.Length ? NocturneConfig.FavoriteOutfits[_outfitPickSlot] : null;
            string cap = NocturneOutfits.Capture(pc);
            if (slot != null && cap.Length > 0)
            {
                slot.Value = cap;
                NocturneToast.Push(NocturneText.T("Образ", "Outfit"), NocturneText.T($"Слот {_outfitPickSlot + 1}: {(pc.Data != null ? pc.Data.PlayerName : "?")}", $"Slot {_outfitPickSlot + 1}: {(pc.Data != null ? pc.Data.PlayerName : "?")}"), 2f, NocturneNotifyKind.Success);
            }
            else
                NocturneToast.Push(NocturneText.T("Образ", "Outfit"), NocturneText.T("не вышло", "failed"), 1.8f, NocturneNotifyKind.Warning);
            _outfitPickSlot = -1;
        }
        y += 32f;
    }

    private void QuickOutfitRow(float x, ref float y, float w, PlayerControl pc)
    {
        if (RowCull(ref y, 30f, 32f))
            return;
        var r = new Rect(x, y, w, 30f);
        HoverFill(r);
        DrawColorDot(new Rect(r.x + 6f, r.y + 9f, 12f, 12f), pc);
        Lab(new Rect(r.x + 26f, r.y, r.width - 110f, r.height), pc.Data != null ? pc.Data.PlayerName : "?", _rowName);
        if (SmallButton(new Rect(r.xMax - 100f, r.y + 3f, 96f, 24f), NocturneText.T("ВЗЯТЬ", "TAKE"), NocturneStyle.Current.Accent))
        {
            NocturneOutfitApplier.Borrow(pc);
            NocturneToast.Push(NocturneText.T("Образ", "Outfit"), NocturneText.T($"надет: {(pc.Data != null ? pc.Data.PlayerName : "?")}", $"applied: {(pc.Data != null ? pc.Data.PlayerName : "?")}"), 2f, NocturneNotifyKind.Success);
        }
        y += 32f;
    }

    private void DrawSettings(float x, ref float y, float w)
    {
        SubBar(x, ref y, w, SettingsSubsRu, SettingsSubsEn);

        float gw = CardW(w) - 56f;
        float gridH = ThemeGridHeight(gw);
        float scaleH = 100f + RowH;
        float accentExtra = RowH + (NocturneConfig.AccentCustom.Value ? 44f + 2f * 50f + 6f : 0f);
        Grp(0);
        Rect b = Card(x, ref y, w, NocturneText.T("Интерфейс", "Interface"), 3f * RowH + scaleH + 30f + gridH + accentExtra);
        float by = b.y;
        if (CycleRow(b.x, ref by, b.width, NocturneText.T("Язык", "Language"), NocturneText.LangName))
            NocturneText.Toggle();
        Toggle(b.x, ref by, b.width, NocturneText.T("Лёгкий режим (для слабых ПК)", "Lite mode (weak PCs)"), NocturneConfig.LiteMenu);
        Toggle(b.x, ref by, b.width, NocturneText.T("Ванильный стиль (без кастом-эффектов)", "Vanilla look (no custom effects)"), NocturneConfig.VanillaStyle);
        Slider(b.x, ref by, b.width, NocturneText.T("Прозрачность окна", "Window opacity"), NocturneConfig.MenuOpacity, 0.45f, 1f, "0.00");
        int mc = Mathf.Clamp(NocturneConfig.MenuColumns.Value, 0, 3);
        if (CycleRow(b.x, ref by, b.width, NocturneText.T("Колонки карточек", "Card columns"), mc == 0 ? NocturneText.T("авто", "auto") : mc.ToString()))
            NocturneConfig.MenuColumns.Value = (mc + 1) % 4;
        Slider(b.x, ref by, b.width, NocturneText.T("Масштаб меню (1.00 — авто)", "Menu scale (1.00 = auto)"), NocturneConfig.MenuScale, 0.7f, 1.8f, "0.00");
        Lab(new Rect(b.x + 12f, by, b.width - 24f, 20f), NocturneText.T("Тема (акцент)", "Theme (accent)"), _muted);
        by += 24f;
        ThemeSwatches(b.x + 10f, by, b.width - 20f);
        by += gridH + 8f;
        Toggle(b.x, ref by, b.width, NocturneText.T("Свой акцент (по оттенку)", "Custom accent (hue)"), NocturneConfig.AccentCustom);
        if (NocturneConfig.AccentCustom.Value)
        {
            HueRow(b.x, ref by, b.width);
            Slider(b.x, ref by, b.width, NocturneText.T("Насыщенность", "Saturation"), NocturneConfig.AccentSat, 0f, 1f, "0.00");
            Slider(b.x, ref by, b.width, NocturneText.T("Яркость", "Brightness"), NocturneConfig.AccentVal, 0.35f, 1f, "0.00");
        }

        Grp(0);
        Rect wp = Card(x, ref y, w, NocturneText.T("Свои обои главного меню", "Own main menu wallpaper"), 2f * 48f + 26f);
        if (ActionRow(wp, NocturneIcon.Bookmark, NocturneText.T("Папка с картинкой", "Picture folder"), NocturneText.T("Открыть", "Open")))
            OpenWallpaperDir();
        if (ActionRow(new Rect(wp.x, wp.y + 48f, wp.width, 44f), NocturneIcon.Tune, NocturneText.T("Применить картинку", "Apply picture"), NocturneText.T("Обновить", "Reload")))
            ReloadWallpaper();
        Lab(new Rect(wp.x + 2f, wp.y + 96f, wp.width - 4f, 22f),
            NocturneText.T("Картинка: png, jpg. Живые обои: mp4, webm, m4v.", "Picture: png, jpg. Live: mp4, webm, m4v."), _muted);

        Grp(0);
        float mbExtra = NocturneConfig.MenuBg.Value ? 50f + 2f * 48f : 0f;
        Rect mb = Card(x, ref y, w, NocturneText.T("Фон окна мода", "Mod window background"), RowH + mbExtra + 26f);
        float mby = mb.y;
        Toggle(mb.x, ref mby, mb.width, NocturneText.T("Своя картинка за меню", "Own picture behind menu"), NocturneConfig.MenuBg);
        if (NocturneConfig.MenuBg.Value)
        {
            Slider(mb.x, ref mby, mb.width, NocturneText.T("Прозрачность", "Opacity"), NocturneConfig.MenuBgAlpha, 0.05f, 1f, "0.00");
            if (ActionRow(new Rect(mb.x, mby, mb.width, 44f), NocturneIcon.Bookmark, NocturneText.T("Папка фона меню", "Menu background folder"), NocturneText.T("Открыть", "Open")))
                OpenMenuBgDir();
            mby += 48f;
            if (ActionRow(new Rect(mb.x, mby, mb.width, 44f), NocturneIcon.Tune, NocturneText.T("Применить картинку", "Apply picture"), NocturneText.T("Обновить", "Reload")))
                ReloadMenuBg();
            mby += 48f;
        }
        Lab(new Rect(mb.x + 2f, mby, mb.width - 4f, 22f),
            NocturneText.T("png или jpg — рисуется за содержимым меню.", "png or jpg — drawn behind the menu content."), _muted);

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
        Lab(new Rect(b.x + 2f, by, b.width - 2f, 22f), NocturneText.T("Клик — назначить, корзина/ПКМ — сброс, Esc — отмена.", "Click to set, trash/RMB to clear, Esc to cancel."), _muted);

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
        b = Card(x, ref y, w, NocturneText.T("Призраки и лобби", "Ghosts & lobby"), 10f * RowH + 22f, true);
        by = b.y;
        KeyRow(b.x, ref by, b.width, NocturneText.T("Фантом в лобби (хост)", "Phantom in lobby (host)"), NocturneConfig.PhantomKey);
        KeyRow(b.x, ref by, b.width, NocturneText.T("Оставить тело (хост)", "Leave a body (host)"), NocturneConfig.CorpseKey);
        KeyRow(b.x, ref by, b.width, NocturneText.T("Призрак после старта", "Ghost after start"), NocturneConfig.GhostKey);
        KeyRow(b.x, ref by, b.width, NocturneText.T("Суицид (пред)", "Suicide (impostor)"), NocturneConfig.GhostNowKey);
        KeyRow(b.x, ref by, b.width, NocturneText.T("ТП отмеченных в люк", "TP marked to vent"), NocturneConfig.VentTpKey);
        KeyRow(b.x, ref by, b.width, NocturneText.T("Сменить люк", "Cycle vent"), NocturneConfig.VentCycleKey);
        KeyRow(b.x, ref by, b.width, NocturneText.T("Видеть призраков", "See ghosts"), NocturneConfig.SeeGhostsKey);
        KeyRow(b.x, ref by, b.width, NocturneText.T("Спавн лобби (хост)", "Spawn lobby (host)"), NocturneConfig.SpawnLobbyKey);
        KeyRow(b.x, ref by, b.width, NocturneText.T("Деспавн лобби (хост)", "Despawn lobby (host)"), NocturneConfig.DespawnLobbyKey);
        KeyRow(b.x, ref by, b.width, NocturneText.T("Выйти из лобби", "Leave lobby"), NocturneConfig.LeaveLobbyKey);
        Lab(new Rect(b.x + 2f, by, b.width - 2f, 20f), NocturneText.T("Выход — двойное нажатие в течение 3 с.", "Leave — press twice within 3s."), _muted);

        Grp(2);
        b = Card(x, ref y, w, NocturneText.T("Яйца (хост)", "Eggs (host)"), 9f * RowH, true);
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
        b = Card(x, ref y, w, NocturneText.T("Аккаунт / штрафы", "Account / penalties"), 3f * RowH + 6f, true);
        by = b.y;
        Toggle(b.x, ref by, b.width, NocturneText.T("Сбросить штраф за выход", "Clear disconnect penalty"), NocturneConfig.ClearDisconnectPenalty);
        Toggle(b.x, ref by, b.width, NocturneText.T("Гостю доп. функции", "Guest extra features"), NocturneConfig.RemoveGuestLimits);
        Toggle(b.x, ref by, b.width, NocturneText.T("Игнор. ограничения", "Ignore restrictions"), NocturneConfig.RemoveMinorLimits);

        Grp(0);
        b = Card(x, ref y, w, NocturneText.T("Приватность", "Privacy"), RowH + 26f);
        by = b.y;
        Toggle(b.x, ref by, b.width, NocturneText.T("Блок телеметрии / данных", "Block telemetry / data"), NocturneConfig.BlockTelemetry);
        Lab(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Применяется при запуске игры.", "Applied on game start."), _muted);
    }

    private void DrawEmpty(Rect area, NocturneIcon icon, string msg)
    {
        NocturnePalette p = NocturneStyle.Current;
        float s = 66f;
        var ic = new Rect(area.center.x - s / 2f, area.y + area.height * 0.30f, s, s);
        NocturneStyle.FillRounded(new Rect(ic.x - 12f, ic.y - 12f, s + 24f, s + 24f), A(p.Accent, 0.06f), 22);
        NocturneIcons.Draw(icon, ic, A(p.Muted, 0.55f));
        Lab(new Rect(area.x, ic.yMax + 16f, area.width, 24f), msg, _centerMuted);
    }

    [HideFromIl2Cpp]
    private static void OpenWallpaperDir()
    {
        try
        {
            string dir = Patches.NocturneMainArt.SkinDir;
            System.IO.Directory.CreateDirectory(dir);
            Application.OpenURL("file:///" + dir.Replace('\\', '/'));
        }
        catch { }
    }

    private static void OpenMenuBgDir()
    {
        try
        {
            string dir = NocturneMenuBg.Dir;
            System.IO.Directory.CreateDirectory(dir);
            Application.OpenURL("file:///" + dir.Replace('\\', '/'));
        }
        catch { }
    }

    private static void ReloadMenuBg()
    {
        NocturneMenuBg.Reload();
        NocturneToast.Push(NocturneText.T("Фон меню", "Menu background"),
            NocturneText.T("Картинка перечитана.", "Picture reloaded."), 2.2f, NocturneNotifyKind.Success);
    }

    private static void ReloadWallpaper()
    {
        Patches.NocturneMainArt.ReloadManual();
        NocturneToast.Push(NocturneText.T("Обои", "Wallpaper"),
            NocturneText.T("Картинка перечитана.", "Picture reloaded."), 2.2f, NocturneNotifyKind.Success);
    }

    private bool ActionRow(Rect body, NocturneIcon icon, string label, string btn)
    {
        var row = new Rect(body.x, body.y, body.width, 44f);
        float bw = _layCols > 1 ? 84f : 112f;
        var bt = new Rect(row.xMax - bw, row.y + 5f, bw, 34f);

        if (NocturneStyle.Painting)
        {
            NocturnePalette p = NocturneStyle.Current;
            var sq = new Rect(row.x, row.y + 2f, 40f, 40f);
            NocturneStyle.FillRounded(sq, A(p.Accent, 0.14f), 10);
            NocturneIcons.Draw(icon, new Rect(sq.x + 10f, sq.y + 10f, 20f, 20f), p.Accent);
            Lab(new Rect(sq.xMax + 14f, row.y, row.width - (bw + 58f), 44f), label, _rowLabel);

            bool hover = bt.Contains(M);
            NocturneStyle.FillRounded(bt, p.Accent, 9);
            NocturneStyle.FillRounded(new Rect(bt.x + 1.5f, bt.y + 1.5f, bt.width - 3f, bt.height - 3f), hover ? A(p.Accent, 0.30f) : p.Panel, 8);
            NocturneStyle.Fill(new Rect(bt.x + 8f, bt.y + 4f, bt.width - 16f, 1f), A(Color.white, 0.10f));
            Lab(bt, btn, _btnLabel);
        }

        return GUI.Button(bt, GUIContent.none, _invisible);
    }

    private readonly Dictionary<object, string> _numStr = new Dictionary<object, string>();
    private readonly Dictionary<object, float> _numVal = new Dictionary<object, float>();

    [HideFromIl2Cpp]
    private string Num(object key, float v, string fmt)
    {
        if (_numVal.TryGetValue(key, out float had) && Mathf.Abs(had - v) < 0.0005f
            && _numStr.TryGetValue(key, out string cached) && cached != null)
            return cached;

        string s = fmt == null ? v.ToString() : v.ToString(fmt);
        _numVal[key] = v;
        _numStr[key] = s;
        return s;
    }

    [HideFromIl2Cpp]
    private string NumInt(object key, int v)
    {
        if (_numVal.TryGetValue(key, out float had) && (int)had == v
            && _numStr.TryGetValue(key, out string cached) && cached != null)
            return cached;

        string s = v.ToString();
        _numVal[key] = v;
        _numStr[key] = s;
        return s;
    }

    [HideFromIl2Cpp]
    private string Suf(object key, int v, string suffix)
    {
        if (_numVal.TryGetValue(key, out float had) && (int)had == v
            && _numStr.TryGetValue(key, out string cached) && cached != null)
            return cached;

        string s = v.ToString() + suffix;
        _numVal[key] = v;
        _numStr[key] = s;
        return s;
    }

    [HideFromIl2Cpp]
    private string Suf(object key, float v, string fmt, string suffix)
    {
        if (_numVal.TryGetValue(key, out float had) && Mathf.Abs(had - v) < 0.0005f
            && _numStr.TryGetValue(key, out string cached) && cached != null)
            return cached;

        string s = (fmt == null ? v.ToString() : v.ToString(fmt)) + suffix;
        _numVal[key] = v;
        _numStr[key] = s;
        return s;
    }

    private void InfoRow(float x, ref float y, float w, string label, string value)
    {
        Lab(new Rect(x + 2f, y, w * 0.38f, 28f), label, _muted);
        Lab(new Rect(x + w * 0.38f, y, w * 0.62f - 2f, 28f), value, _value);
        y += 30f;
    }

    [HideFromIl2Cpp]
    private void Toggle(float x, ref float y, float w, string label, ConfigEntry<bool> entry)
    {
        if (entry == null)
        {
            y += RowH;
            return;
        }
        string fk = FavKey(entry);
        var it = FavReg(fk, FavKind.Bool);
        if (it != null)
        {
            it.Label = label;
            it.B = entry;
        }
        if (Cull(y, RowH))
        {
            y += RowH;
            return;
        }
        FavRow(new Rect(x, y, w, RowH - 4f), fk);
        if (NocturneStyle.Lite)
        {
            bool nv = GUI.Toggle(new Rect(x + 4f, y, w - 8f, RowH - 4f), entry.Value, label);
            if (nv != entry.Value)
                entry.Value = nv;
            y += RowH;
            return;
        }
        var r = new Rect(x, y, w, RowH - 4f);
        HoverFill(r);
        if (GUI.Button(r, GUIContent.none, _invisible))
        {
            entry.Value = !entry.Value;
            RecentTouch(fk);
        }

        Lab(new Rect(r.x + 10f, r.y, r.width - RowPad, r.height), label, _rowLabel);
        DrawPill(new Rect(r.xMax - 50f, r.y + (r.height - 24f) / 2f, 46f, 24f), entry.Value, entry);
        y += RowH;
    }

    [HideFromIl2Cpp]
    private void Slider(float x, ref float y, float w, string label, ConfigEntry<float> entry, float min, float max, string fmt)
    {
        if (entry == null)
        {
            y += RowH;
            return;
        }
        string fk = FavKey(entry);
        var fit = FavReg(fk, FavKind.Float);
        if (fit != null)
        {
            fit.Label = label;
            fit.F = entry;
            fit.Min = min;
            fit.Max = max;
            fit.Fmt = fmt;
        }
        if (Cull(y, 50f))
        {
            y += 50f;
            return;
        }
        FavRow(new Rect(x, y, w, 46f), fk);
        if (NocturneStyle.Lite)
        {
            Lab(new Rect(x + 6f, y, w - RowPad + 4f, 20f), label, _rowLabel);
            Lab(new Rect(x + w - 60f, y, 56f, 20f), Num(entry, entry.Value, fmt), _value);
            y += 22f;
            entry.Value = GUI.HorizontalSlider(new Rect(x + 6f, y + 3f, w - 12f, 16f), entry.Value, min, max);
            y += 24f;
            return;
        }
        NocturnePalette p = NocturneStyle.Current;
        Lab(new Rect(x + 10f, y, w - RowPad, 22f), label, _rowLabel);
        Lab(new Rect(x + w - 58f, y, 54f, 22f), Num(entry, entry.Value, fmt), _value);
        y += 26f;

        var track = new Rect(x + 10f, y + 2f, w - 20f, 8f);
        int id = label.GetHashCode();
        Event e = NocturneStyle.Ev;
        var hit = new Rect(track.x - 8f, track.y - 10f, track.width + 16f, 28f);
        if (e != null && e.type == EventType.MouseDown && hit.Contains(e.mousePosition))
        {
            _slider = id;
            e.Use();
        }
        if (_slider == id && e != null)
        {
            if (e.type == EventType.MouseDrag || e.type == EventType.MouseDown)
            {
                entry.Value = Mathf.Lerp(min, max, Mathf.Clamp01((e.mousePosition.x - track.x) / track.width));
                e.Use();
            }
            else if (e.type == EventType.MouseUp)
            {
                _slider = 0;
                e.Use();
            }
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
        if (entry == null)
        {
            y += RowH;
            return;
        }
        string fk = FavKey(entry);
        var iit = FavReg(fk, FavKind.Int);
        if (iit != null)
        {
            iit.Label = label;
            iit.I = entry;
            iit.Min = min;
            iit.Max = max;
        }
        if (Cull(y, 50f))
        {
            y += 50f;
            return;
        }
        FavRow(new Rect(x, y, w, 46f), fk);
        if (NocturneStyle.Lite)
        {
            Lab(new Rect(x + 6f, y, w - RowPad + 4f, 20f), label, _rowLabel);
            Lab(new Rect(x + w - 60f, y, 56f, 20f), NumInt(entry, entry.Value), _value);
            y += 22f;
            entry.Value = Mathf.RoundToInt(GUI.HorizontalSlider(new Rect(x + 6f, y + 3f, w - 12f, 16f), entry.Value, min, max));
            y += 24f;
            return;
        }
        NocturnePalette p = NocturneStyle.Current;
        Lab(new Rect(x + 10f, y, w - RowPad, 22f), label, _rowLabel);
        Lab(new Rect(x + w - 58f, y, 54f, 22f), NumInt(entry, entry.Value), _value);
        y += 26f;

        const float arrowW = 24f;
        var leftBtn = new Rect(x + 8f, y - 3f, arrowW, 20f);
        var rightBtn = new Rect(x + w - arrowW - 8f, y - 3f, arrowW, 20f);
        if (ArrowButton(leftBtn, "◂"))
            entry.Value = Mathf.Clamp(entry.Value - 1, min, max);
        if (ArrowButton(rightBtn, "▸"))
            entry.Value = Mathf.Clamp(entry.Value + 1, min, max);

        var track = new Rect(leftBtn.xMax + 8f, y + 2f, rightBtn.x - leftBtn.xMax - 16f, 8f);
        int id = label.GetHashCode();
        Event e = NocturneStyle.Ev;
        var hit = new Rect(track.x, track.y - 10f, track.width, 28f);
        if (e != null && e.type == EventType.MouseDown && hit.Contains(e.mousePosition))
        {
            _slider = id;
            e.Use();
        }
        if (_slider == id && e != null)
        {
            if (e.type == EventType.MouseDrag || e.type == EventType.MouseDown)
            {
                entry.Value = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(min, max, Mathf.Clamp01((e.mousePosition.x - track.x) / track.width))), min, max);
                e.Use();
            }
            else if (e.type == EventType.MouseUp)
            {
                _slider = 0;
                e.Use();
            }
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
        value = Mathf.Clamp(value, min, max);
        if (Cull(y, 50f))
        {
            y += 50f;
            return value;
        }
        NocturnePalette p = NocturneStyle.Current;
        Lab(new Rect(x + 10f, y, w - RowPad, 22f), label, _rowLabel);
        Lab(new Rect(x + w - 58f, y, 54f, 22f), Suf(label, value, suffix), _value);
        y += 26f;

        const float arrowW = 24f;
        var leftBtn = new Rect(x + 8f, y - 3f, arrowW, 20f);
        var rightBtn = new Rect(x + w - arrowW - 8f, y - 3f, arrowW, 20f);
        if (ArrowButton(leftBtn, "◂"))
            value = Mathf.Clamp(value - 1, min, max);
        if (ArrowButton(rightBtn, "▸"))
            value = Mathf.Clamp(value + 1, min, max);

        var track = new Rect(leftBtn.xMax + 8f, y + 2f, rightBtn.x - leftBtn.xMax - 16f, 8f);
        int id = key != 0 ? key : label.GetHashCode();
        Event e = NocturneStyle.Ev;
        var hit = new Rect(track.x, track.y - 10f, track.width, 28f);
        if (e != null && e.type == EventType.MouseDown && hit.Contains(e.mousePosition))
        {
            _slider = id;
            e.Use();
        }
        if (_slider == id && e != null)
        {
            if (e.type == EventType.MouseDrag || e.type == EventType.MouseDown)
            {
                value = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(min, max, Mathf.Clamp01((e.mousePosition.x - track.x) / track.width))), min, max);
                e.Use();
            }
            else if (e.type == EventType.MouseUp)
            {
                _slider = 0;
                e.Use();
            }
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
        value = Mathf.Clamp(value, min, max);
        if (Cull(y, 50f))
        {
            y += 50f;
            return value;
        }
        NocturnePalette p = NocturneStyle.Current;
        Lab(new Rect(x + 10f, y, w - RowPad, 22f), label, _rowLabel);
        Lab(new Rect(x + w - 58f, y, 54f, 22f), Suf(label, value, fmt, suffix), _value);
        y += 26f;

        const float arrowW = 24f;
        var leftBtn = new Rect(x + 8f, y - 3f, arrowW, 20f);
        var rightBtn = new Rect(x + w - arrowW - 8f, y - 3f, arrowW, 20f);
        if (ArrowButton(leftBtn, "◂"))
            value = Mathf.Clamp(value - step, min, max);
        if (ArrowButton(rightBtn, "▸"))
            value = Mathf.Clamp(value + step, min, max);

        var track = new Rect(leftBtn.xMax + 8f, y + 2f, rightBtn.x - leftBtn.xMax - 16f, 8f);
        int id = label.GetHashCode();
        Event e = NocturneStyle.Ev;
        var hit = new Rect(track.x, track.y - 10f, track.width, 28f);
        if (e != null && e.type == EventType.MouseDown && hit.Contains(e.mousePosition))
        {
            _slider = id;
            e.Use();
        }
        if (_slider == id && e != null)
        {
            if (e.type == EventType.MouseDrag || e.type == EventType.MouseDown)
            {
                float raw = Mathf.Lerp(min, max, Mathf.Clamp01((e.mousePosition.x - track.x) / track.width));
                value = Mathf.Clamp(Mathf.Round(raw / step) * step, min, max);
                e.Use();
            }
            else if (e.type == EventType.MouseUp)
            {
                _slider = 0;
                e.Use();
            }
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
        if (Cull(y, RowH))
        {
            y += RowH;
            return value;
        }
        var r = new Rect(x, y, w, RowH - 4f);
        HoverFill(r);
        bool clicked = GUI.Button(r, GUIContent.none, _invisible);
        Lab(new Rect(r.x + 10f, r.y, r.width - RowPad, r.height), label, _rowLabel);
        bool next = clicked ? !value : value;
        DrawPill(new Rect(r.xMax - 50f, r.y + (r.height - 24f) / 2f, 46f, 24f), next, label);
        y += RowH;
        return next;
    }

    [HideFromIl2Cpp]
    private void FpsSlider(float x, ref float y, float w, string label, ConfigEntry<int> entry)
    {
        if (entry == null)
        {
            y += RowH;
            return;
        }
        if (Cull(y, 50f))
        {
            y += 50f;
            return;
        }
        const int min = 30, max = 300, step = 5;
        NocturnePalette p = NocturneStyle.Current;
        Lab(new Rect(x + 10f, y, w - RowPad, 22f), label, _rowLabel);
        Lab(new Rect(x + w - 58f, y, 54f, 22f), entry.Value >= max ? "∞" : NumInt(entry, entry.Value), _value);
        y += 26f;

        const float arrowW = 24f;
        var leftBtn = new Rect(x + 8f, y - 3f, arrowW, 20f);
        var rightBtn = new Rect(x + w - arrowW - 8f, y - 3f, arrowW, 20f);
        if (ArrowButton(leftBtn, "◂"))
            entry.Value = Mathf.Clamp(entry.Value - step, min, max);
        if (ArrowButton(rightBtn, "▸"))
            entry.Value = Mathf.Clamp(entry.Value + step, min, max);

        var track = new Rect(leftBtn.xMax + 8f, y + 2f, rightBtn.x - leftBtn.xMax - 16f, 8f);
        int id = label.GetHashCode();
        Event e = NocturneStyle.Ev;
        var hit = new Rect(track.x, track.y - 10f, track.width, 28f);
        if (e != null && e.type == EventType.MouseDown && hit.Contains(e.mousePosition))
        {
            _slider = id;
            e.Use();
        }
        if (_slider == id && e != null)
        {
            if (e.type == EventType.MouseDrag || e.type == EventType.MouseDown)
            {
                int raw = Mathf.RoundToInt(Mathf.Lerp(min, max, Mathf.Clamp01((e.mousePosition.x - track.x) / track.width)));
                entry.Value = Mathf.Clamp(Mathf.RoundToInt(raw / (float)step) * step, min, max);
                e.Use();
            }
            else if (e.type == EventType.MouseUp)
            {
                _slider = 0;
                e.Use();
            }
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
        if (NocturneStyle.Painting)
        {
            bool hover = r.Contains(M);
            NocturneStyle.FillRounded(r, hover ? A(NocturneStyle.Current.Accent, 0.22f) : A(Color.white, 0.05f), 6);
            Lab(r, glyph, _arrow);
        }
        return GUI.Button(r, GUIContent.none, _invisible);
    }

    private const float SwW = 28f, SwH = 22f, SwGap = 6f;

    private static int ThemeCols(float w) => Mathf.Max(1, Mathf.FloorToInt((w + SwGap) / (SwW + SwGap)));

    private static float ThemeGridHeight(float w) =>
        Mathf.CeilToInt((float)NocturneStyle.ThemeCount / ThemeCols(w)) * (SwH + SwGap) - SwGap;

    private bool _hueDrag;

    private void HueRow(float x, ref float y, float w)
    {
        Lab(new Rect(x + 12f, y, w - 24f, 20f), NocturneText.T("Оттенок акцента", "Accent hue"), _muted);
        y += 22f;

        var track = new Rect(x + 14f, y, w - 28f, 16f);
        NocturneStyle.DrawHueBar(track);
        NocturneStyle.StrokeRounded(track, A(Color.black, 0.3f), 4, 1);

        float hue = Mathf.Clamp01(NocturneConfig.AccentHue.Value);
        float kx = track.x + track.width * hue;
        var knob = new Rect(kx - 5f, track.y - 3f, 10f, track.height + 6f);
        NocturneStyle.FillRounded(knob, Color.white, 4);
        NocturneStyle.StrokeRounded(knob, A(Color.black, 0.5f), 4, 1);

        Event e = NocturneStyle.Ev;
        var hit = new Rect(track.x, track.y - 8f, track.width, track.height + 16f);
        if (e != null && (e.type == EventType.MouseDown || (e.type == EventType.MouseDrag && _hueDrag)) && hit.Contains(e.mousePosition))
        {
            if (e.type == EventType.MouseDown)
                _hueDrag = true;
            NocturneConfig.AccentHue.Value = Mathf.Clamp01((e.mousePosition.x - track.x) / track.width);
            e.Use();
        }
        if (e != null && e.type == EventType.MouseUp)
            _hueDrag = false;

        y += 22f;
    }

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
            if (GUI.Button(r, GUIContent.none, _invisible))
                NocturneConfig.ThemeIndex.Value = i;
        }
    }

    private void DrawSearchBar(Rect r)
    {
        string prev = _search;
        _search = CustomText(r, _search ?? "", "nocturneSearch", 24, _searchText);
        if (_search != prev)
        {
            _scroll = 0f;
            _scrollPending = 0f;
        }
        if (string.IsNullOrEmpty(_search) && _textFocus != "nocturneSearch")
            Lab(new Rect(r.x + 8f, r.y, r.width - 16f, r.height), NocturneText.T("Поиск фич…", "Search features…"), _searchHint);
        if (!string.IsNullOrEmpty(_search))
        {
            var clr = new Rect(r.xMax - 24f, r.y, 22f, r.height);
            if (GUI.Button(clr, GUIContent.none, _invisible))
            {
                _search = "";
                _textFocus = null;
            }
            Lab(clr, "×", _star);
        }
    }

    private void DrawCollapseAll(Rect r)
    {
        if (_knownCards.Count == 0)
            return;
        int col = 0;
        foreach (string t in _knownCards)
            if (_cardCollapsed.TryGetValue(t, out bool c) && c)
                col++;
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
            Lab(r, mostCollapsed ? "⊞" : "⊟", _centerMuted);
            clicked = GUI.Button(r, GUIContent.none, _invisible);
        }
        if (clicked)
        {
            bool target = !mostCollapsed;
            foreach (string t in _knownCards)
                _cardCollapsed[t] = target;
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
            _searchCards.Clear();
            NocturneSearchIndex.Collect(q, _searchCards);

            _searchFeat.Clear();
            foreach (KeyValuePair<string, FavItem> kv in _favReg)
            {
                FavItem f = kv.Value;
                if (f.Label != null && f.Label.ToLowerInvariant().Contains(q))
                    _searchFeat.Add(f);
            }
            _searchFeat.Sort((a, b2) => string.CompareOrdinal(a.Label, b2.Label));
        }

        int n = _searchHits.Count;
        int m = _searchCards.Count;
        int k = _searchFeat.Count;

        Rect bf = Card(x, ref y, w, NocturneText.T("Все фичи", "All features") + "  (" + k + ")", k > 0 ? k * RowH + 6f : 30f);
        float byf = bf.y;
        if (k == 0)
            Lab(new Rect(bf.x + 2f, byf, bf.width - 2f, 24f),
                NocturneText.T("Пусто. Открой разделы один раз, чтобы собрать список.", "Empty. Open the tabs once to collect the list."), _muted);
        else
            for (int i = 0; i < k; i++)
                FeatureRow(bf.x, ref byf, bf.width, _searchFeat[i]);

        Rect b = Card(x, ref y, w, NocturneText.T("Быстрые", "Quick") + "  (" + n + ")", n > 0 ? n * RowH + 6f : 30f);
        float by = b.y;
        if (n == 0)
            Lab(new Rect(b.x + 2f, by, b.width - 2f, 24f), NocturneText.T("Ничего не найдено.", "Nothing found."), _muted);
        else
            for (int i = 0; i < n; i++)
                QuickRow(b.x, ref by, b.width, _searchHits[i]);

        Rect b2 = Card(x, ref y, w, NocturneText.T("Разделы", "Sections") + "  (" + m + ")", m > 0 ? m * RowH + 6f : 30f);
        float by2 = b2.y;
        if (m == 0)
            Lab(new Rect(b2.x + 2f, by2, b2.width - 2f, 24f), NocturneText.T("Пусто. Открой вкладки один раз, чтобы проиндексировать.", "Empty. Open the tabs once to index them."), _muted);
        else
            for (int i = 0; i < m; i++)
                SearchCardRow(b2.x, ref by2, b2.width, _searchCards[i]);
    }

    private void FeatureRow(float x, ref float y, float w, FavItem it)
    {
        switch (it.Kind)
        {
            case FavKind.Bool:
                Toggle(x, ref y, w, it.Label, it.B);
                break;
            case FavKind.Float:
                Slider(x, ref y, w, it.Label, it.F, it.Min, it.Max, it.Fmt);
                break;
            case FavKind.Int:
                SliderInt(x, ref y, w, it.Label, it.I, (int)it.Min, (int)it.Max);
                break;
            case FavKind.Cycle:
                ActionCycle(x, ref y, w, it.Label, it.S, it.Vals, it.Disp);
                break;
            case FavKind.Key:
                KeyRow(x, ref y, w, it.Label, it.K);
                break;
            default:
                y += RowH;
                break;
        }
    }

    private void SearchCardRow(float x, ref float y, float w, SearchCard c)
    {
        if (Cull(y, RowH))
        {
            y += RowH;
            return;
        }
        var r = new Rect(x, y, w, RowH - 2f);
        HoverFill(r);
        Lab(new Rect(r.x + 10f, r.y, r.width - 190f, r.height), c.Title, _rowName);
        Lab(new Rect(r.xMax - 178f, r.y, 82f, r.height), TabName(c.Tab), _muted);
        if (SmallButton(new Rect(r.xMax - 92f, r.y + 3f, 88f, 24f), NocturneText.T("ПЕРЕЙТИ", "GO"), NocturneStyle.Current.Accent))
            JumpTo(c);
        y += RowH;
    }

    private static string TabName(int tab)
    {
        if (tab == FavTab)
            return NocturneText.T("Избранное", "Favorites");
        if (tab >= 0 && tab < Tabs.Length)
            return Tabs[tab].Name;
        return NocturneText.T("Настройки", "Settings");
    }

    private void JumpTo(SearchCard c)
    {
        _search = "";
        _searchDone = _search;
        _textFocus = null;
        _searchHits.Clear();
        _searchCards.Clear();
        _searchFeat.Clear();
        if (_tab != c.Tab)
        {
            _tabDir = c.Tab > _tab ? 1 : -1;
            _tabAnimAt = Time.unscaledTime;
            _tab = c.Tab;
        }
        if (c.Grp >= 0 && c.Tab >= 0 && c.Tab < _subTab.Length)
            _subTab[c.Tab] = c.Grp;
        _scroll = 0f;
        _scrollPending = 0f;
        _pendJumpTab = c.Tab;
        _pendJumpTitle = c.Title;
    }

    [HideFromIl2Cpp]
    private void QuickRow(float x, ref float y, float w, QuickItem it)
    {
        if (Cull(y, RowH))
        {
            y += RowH;
            return;
        }
        var r = new Rect(x, y, w, RowH - 2f);
        HoverFill(r);
        bool fav = NocturneQuick.IsFav(it.Id);
        var star = new Rect(r.x + 6f, r.y, 26f, r.height);
        if (GUI.Button(star, GUIContent.none, _invisible))
            NocturneQuick.ToggleFav(it.Id);
        if (NocturneStyle.Painting)
        {
            _star.normal.textColor = fav ? NocturneStyle.Current.Accent : A(NocturneStyle.Current.Muted, 0.7f);
            Lab(star, fav ? "★" : "☆", _star);
            Lab(new Rect(r.x + 36f, r.y, r.width - 92f, r.height), it.Label, _rowLabel);
        }
        var pill = new Rect(r.xMax - 50f, r.y + (r.height - 24f) / 2f, 46f, 24f);
        if (GUI.Button(pill, GUIContent.none, _invisible))
            it.Cfg.Value = !it.Cfg.Value;
        DrawPill(pill, it.Cfg.Value, it.Cfg);
        y += RowH;
    }

    [HideFromIl2Cpp]
    private bool CycleRow(float x, ref float y, float w, string label, string valueText)
    {
        if (Cull(y, RowH))
        {
            y += RowH;
            return false;
        }
        if (NocturneStyle.Lite)
        {
            bool lc = GUI.Button(new Rect(x + 4f, y, w - 8f, RowH - 4f), label + ":  " + valueText);
            y += RowH;
            return lc;
        }
        var r = new Rect(x, y, w, RowH - 4f);
        HoverFill(r);
        bool clicked = GUI.Button(r, GUIContent.none, _invisible);
        if (NocturneStyle.Painting)
        {
            Lab(new Rect(r.x + 12f, r.y, r.width * 0.45f, r.height), label, _rowLabel);
            Lab(new Rect(r.x + r.width - 168f, r.y, 164f, r.height), valueText + "   ▸", _value);
        }
        y += RowH;
        return clicked;
    }

    [HideFromIl2Cpp]
    private void KeyRow(float x, ref float y, float w, string label, ConfigEntry<KeyCode> entry)
    {
        if (entry == null)
        {
            y += RowH;
            return;
        }
        string fk = FavKey(entry);
        var kit = FavReg(fk, FavKind.Key);
        if (kit != null)
        {
            kit.Label = label;
            kit.K = entry;
        }
        if (Cull(y, RowH))
        {
            y += RowH;
            return;
        }
        FavRow(new Rect(x, y, w, RowH - 4f), fk);
        NocturnePalette p = NocturneStyle.Current;
        var r = new Rect(x, y, w, RowH - 4f);
        HoverFill(r);
        bool listening = _rebinding && ReferenceEquals(_rebindTarget, entry);
        float bw = listening || NocturneKeys.Mod(entry) != NocturneKeys.NoMod ? 138f : 96f;
        bool canClear = !listening && entry.Value != KeyCode.None && entry != NocturneConfig.MenuKey;
        float labW = r.width - bw - 20f - (canClear ? 26f : 0f);
        Lab(new Rect(r.x + 12f, r.y, Mathf.Max(40f, labW), r.height), label, _rowLabel);

        var badge = new Rect(r.xMax - bw - 4f, r.y + (r.height - 24f) / 2f, bw, 24f);
        NocturneStyle.FillRounded(badge, listening ? A(p.Accent, 0.85f) : A(Color.white, 0.06f), 7);
        NocturneStyle.StrokeRounded(badge, listening ? A(p.Accent, 0.9f) : A(Color.white, 0.10f), 7, 1);
        if (NocturneStyle.Painting)
        {
            _keyBadge.normal.textColor = listening ? Color.white : p.Text;
            Lab(badge, listening ? NocturneText.T("Нажми клавишу…", "Press a key…") : KeyDisp(entry), _keyBadge);
        }

        Event e = NocturneStyle.Ev;

        if (canClear)
        {
            var del = new Rect(badge.x - 26f, badge.y, 22f, 24f);
            bool dh = e != null && del.Contains(e.mousePosition);
            NocturneStyle.FillRounded(del, dh ? A(new Color(0.9f, 0.3f, 0.3f), 0.30f) : A(Color.white, 0.06f), 6);
            NocturneIcons.Draw(NocturneIcon.Trash, new Rect(del.x + 5f, del.y + 5f, 14f, 14f), dh ? new Color(0.95f, 0.5f, 0.5f) : A(p.Muted, 0.9f));
            if (GUI.Button(del, GUIContent.none, _invisible))
            {
                NocturneKeys.SetMod(entry, NocturneKeys.NoMod);
                entry.Value = KeyCode.None;
                _rebinding = false;
                _rebindTarget = null;
                Rebinding = false;
            }
        }

        bool rightClear = e != null && e.type == EventType.MouseDown && e.button == 1 && badge.Contains(e.mousePosition) && entry != NocturneConfig.MenuKey;
        bool clicked = GUI.Button(badge, GUIContent.none, _invisible);
        if (rightClear)
        {
            NocturneKeys.SetMod(entry, NocturneKeys.NoMod);
            entry.Value = KeyCode.None;
            _rebinding = false;
            _rebindTarget = null;
            Rebinding = false;
            e.Use();
        }
        else if (clicked)
        {
            if (listening)
            {
                _rebinding = false;
                _rebindTarget = null;
            }
            else
            {
                _rebinding = true;
                _rebindTarget = entry;
            }
            Rebinding = _rebinding;
        }
        y += RowH;
    }

    private void HandleRebindCapture()
    {
        Rebinding = _rebinding;
        if (!_rebinding || _rebindTarget == null) return;
        Event e = NocturneStyle.Ev;
        if (e == null || e.type != EventType.KeyDown)
            return;
        KeyCode kc = e.keyCode;
        if (kc == KeyCode.Escape)
        {
            _rebinding = false;
            _rebindTarget = null;
        }
        else if (NocturneKeys.IsModKey(kc))
            return;
        else if (kc != KeyCode.None)
        {
            int mod = e.control ? NocturneKeys.Ctrl : e.alt ? NocturneKeys.Alt : e.shift ? NocturneKeys.Shift : NocturneKeys.NoMod;
            NocturneKeys.SetMod(_rebindTarget, mod);
            _rebindTarget.Value = kc;
            _rebinding = false;
            _rebindTarget = null;
        }
        Rebinding = _rebinding;
        e.Use();
    }

    [HideFromIl2Cpp]
    private void DrawPill(Rect track, bool on, object key)
    {
        if (!NocturneStyle.Painting) return;

        NocturnePalette p = NocturneStyle.Current;
        float target = on ? 1f : 0f;
        float a = target, v = 0f;
        if (key != null)
        {
            bool known = _pillAnim.TryGetValue(key, out a);
            if (!known)
            {
                a = target;
                _pillAnim[key] = a;
                _pillVel[key] = 0f;
            }
            else
            {
                _pillVel.TryGetValue(key, out v);
                if (Mathf.Abs(a - target) > 0.0004f || Mathf.Abs(v) > 0.0004f)
                {
                    float dt = NocturneStyle.Dt;
                    v += (-300f * (a - target) - 24f * v) * dt;
                    a += v * dt;
                    if (Mathf.Abs(a - target) < 0.0004f && Mathf.Abs(v) < 0.0004f)
                    {
                        a = target;
                        v = 0f;
                    }
                    _pillAnim[key] = a;
                    _pillVel[key] = v;
                }
            }
        }
        float e = Mathf.Clamp01(a);
        float kpos = Mathf.Clamp(a, -0.12f, 1.12f);
        int r = Mathf.RoundToInt(track.height / 2f);

        bool hot = track.Contains(M);
        if (e > 0.01f && hot)
            NocturneStyle.FillRounded(new Rect(track.x - 2f, track.y - 2f, track.width + 4f, track.height + 4f), A(p.Accent, 0.30f * e), r + 2);

        NocturneStyle.FillRounded(track, Color.Lerp(p.Button, p.Accent, e), r);
        if (e < 0.99f)
            NocturneStyle.StrokeRounded(track, A(Color.white, 0.06f * (1f - e)), r, 1);
        else
            NocturneStyle.Fill(new Rect(track.x + 5f, track.y + 2f, track.width - 10f, 1f), A(Color.white, 0.30f));

        float knob = track.height - 6f;
        float kx = Mathf.Lerp(track.x + 3f, track.xMax - knob - 3f, kpos);
        int kr = Mathf.RoundToInt(knob / 2f);
        NocturneStyle.FillRounded(new Rect(kx, track.y + 3f, knob, knob), Color.Lerp(p.Muted, Color.white, e), kr);
    }


    private void HoverFill(Rect r)
    {
        if (!NocturneStyle.Painting || !r.Contains(NocturneStyle.Mouse))
            return;
        NocturnePalette p = NocturneStyle.Current;
        NocturneStyle.FillRounded(r, A(p.Accent, 0.06f), 8);
        NocturneStyle.FillRounded(new Rect(r.x, r.y, r.width * 0.42f, r.height), A(p.Accent, 0.05f), 8);
        NocturneStyle.FillRounded(new Rect(r.x + 1f, r.y + 4f, 3f, r.height - 8f), p.Accent, 2);
    }

    private string CustomText(Rect r, string text, string key, int max = 24, GUIStyle st = null)
    {
        bool focused = _textFocus == key;
        if (NocturneStyle.Painting)
        {
            NocturnePalette p = NocturneStyle.Current;
            NocturneStyle.FillRounded(r, p.Button, 7);
            NocturneStyle.StrokeRounded(r, focused ? A(p.Accent, 0.8f) : A(Color.white, 0.08f), 7, 1);
        }

        Event e = NocturneStyle.Ev;
        if (e != null && e.type == EventType.MouseDown)
            _textFocus = r.Contains(e.mousePosition) ? key : (_textFocus == key ? null : _textFocus);

        if (focused && e != null && e.type == EventType.KeyDown)
        {
            bool mod = e.control || e.command;
            if (mod && (e.keyCode == KeyCode.V || e.keyCode == KeyCode.C || e.keyCode == KeyCode.X))
            {
                try
                {
                    if (e.keyCode == KeyCode.V)
                    {
                        string clip = GUIUtility.systemCopyBuffer ?? string.Empty;
                        for (int i = 0; i < clip.Length && text.Length < max; i++)
                        {
                            char ch = clip[i];
                            if (!char.IsControl(ch))
                                text += ch;
                        }
                    }
                    else
                    {
                        GUIUtility.systemCopyBuffer = text;
                        if (e.keyCode == KeyCode.X)
                            text = string.Empty;
                    }
                }
                catch { }
                e.Use();
            }
            else if (e.keyCode == KeyCode.Backspace)
            {
                if (text.Length > 0)
                    text = text.Substring(0, text.Length - 1);
                e.Use();
            }
            else if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
            {
                _textFocus = null;
                e.Use();
            }
            else if (e.character != '\0' && !char.IsControl(e.character) && text.Length < max)
            {
                text += e.character;
                e.Use();
            }
        }

        if (NocturneStyle.Painting)
        {
            bool caret = focused && NocturneStyle.Now % 1f < 0.5f;
            Lab(new Rect(r.x + 8f, r.y, r.width - 16f, r.height), caret ? text + "|" : text, st != null ? st : _rowLabel);
        }
        return text;
    }

    private static void OpenLink(string url)
    {
        try
        {
            GUIUtility.systemCopyBuffer = url;
        }
        catch { }
        try
        {
            Application.OpenURL(url);
        }
        catch { }
        NocturneToast.Push(NocturneText.T("Ссылка", "Link"), NocturneText.T("Открыта в браузере · скопирована", "Opened in browser · copied"), 2.5f, NocturneNotifyKind.Info);
    }

    private static readonly Dictionary<KeyCode, string> _keyStr = new Dictionary<KeyCode, string>();
    private static string KeyStr(KeyCode k)
    {
        if (!_keyStr.TryGetValue(k, out string s))
        {
            s = k.ToString();
            _keyStr[k] = s;
        }
        return s;
    }
    private static string KeyName(ConfigEntry<KeyCode> key) =>
        key != null ? NocturneKeys.Prefix(NocturneKeys.Mod(key)) + KeyStr(key.Value) : "?";

    private static string KeyDisp(ConfigEntry<KeyCode> key) =>
        key == null || key.Value == KeyCode.None ? "—" : NocturneKeys.Prefix(NocturneKeys.Mod(key)) + KeyStr(key.Value);
    private readonly Dictionary<string, float> _labW = new Dictionary<string, float>();
    private readonly Dictionary<string, float> _hintW = new Dictionary<string, float>();
    private readonly Dictionary<string, float> _valW = new Dictionary<string, float>();
    private int _labFont = -1;

    private void Lab(Rect r, string text, GUIStyle st)
    {
        if (!NocturneStyle.Painting)
            return;

        bool hint = st == _muted && r.height <= 26f;
        bool val = st == _value;
        if ((st == _rowLabel || hint || val) && r.width > 24f && !string.IsNullOrEmpty(text))
        {
            GUIStyle use = hint ? _mutedClip : val ? _valueClip : st;
            float tw = LabWidth(text, use, hint ? 1 : val ? 2 : 0);
            if (tw > r.width)
            {
                float over = tw - r.width + 4f;
                float slide = Mathf.Clamp(Mathf.PingPong(NocturneStyle.Now * 26f, over + 60f) - 30f, 0f, over);
                GUI.BeginGroup(r);
                GUI.Label(new Rect(-slide, 0f, tw + 8f, r.height), text, use);
                GUI.EndGroup();
                return;
            }
        }

        GUI.Label(r, text, st);
    }

    private float LabWidth(string text, GUIStyle st, int kind)
    {
        Dictionary<string, float> cache = kind == 1 ? _hintW : kind == 2 ? _valW : _labW;
        if (_labFont != _rowLabel.fontSize)
        {
            _labFont = _rowLabel.fontSize;
            _labW.Clear();
            _hintW.Clear();
            _valW.Clear();
        }
        if (cache.Count > 512)
            cache.Clear();
        if (!cache.TryGetValue(text, out float w))
        {
            w = st.CalcSize(new GUIContent(text)).x;
            cache[text] = w;
        }
        return w;
    }

    private static Color A(Color c, float a) => new Color(c.r, c.g, c.b, a);

    private static Rect LerpRect(Rect a, Rect b, float t) =>
        new Rect(Mathf.Lerp(a.x, b.x, t), Mathf.Lerp(a.y, b.y, t), Mathf.Lerp(a.width, b.width, t), Mathf.Lerp(a.height, b.height, t));

    private static string Hex(Color c)
    {
        Color32 c32 = c;
        return c32.r.ToString("X2") + c32.g.ToString("X2") + c32.b.ToString("X2");
    }

    private Color _accentApplied = new Color(-1f, -1f, -1f, -1f);

    private void RefreshAccentStyles(Color a)
    {
        if (_verPill == null || a == _accentApplied)
            return;
        _accentApplied = a;
        _verPill.normal.textColor = a;
        _favStar.normal.textColor = a;
        _value.normal.textColor = a;
        _valueC.normal.textColor = a;
        _valueClip.normal.textColor = a;
        _btnLabel.normal.textColor = a;
        _arrow.normal.textColor = a;
        _star.normal.textColor = a;
    }

    private void Build()
    {
        int ti = NocturneConfig.ThemeIndex.Value;
        if (_built && ti == _themeBuilt) return;
        _built = true;
        _themeBuilt = ti;
        NocturnePalette p = NocturneStyle.Current;

        _invisible = new GUIStyle();
        _gradient = NocturneStyle.BuildGradient((int)FullH, Rgb(30, 30, 32), Rgb(14, 14, 15), 16);
        _gloss = NocturneStyle.BuildVFade(96, Color.white, 0.05f, 0f);

        _brand = Label(p.Text, 19, FontStyle.Bold, TextAnchor.MiddleLeft);
        _verPill = Label(p.Accent, 12, FontStyle.Bold, TextAnchor.MiddleCenter);
        _status = Label(p.Text, 11, FontStyle.Bold, TextAnchor.MiddleLeft);
        _secTitle = Label(p.Text, 15, FontStyle.Bold, TextAnchor.MiddleLeft);
        _railGroup = Label(A(p.Text, 0.42f), 11, FontStyle.Bold, TextAnchor.MiddleLeft);
        _railLabel = Label(p.Text, 15, FontStyle.Normal, TextAnchor.MiddleLeft);
        _favStar = Label(p.Accent, 11, FontStyle.Bold, TextAnchor.MiddleCenter);
        _cardTitle = Label(A(p.Text, 0.82f), 11, FontStyle.Bold, TextAnchor.MiddleLeft);
        _rowLabel = Label(p.Text, 15, FontStyle.Normal, TextAnchor.MiddleLeft);
        _rowLabel.wordWrap = false;
        _rowLabel.clipping = TextClipping.Clip;
        _wrapLabel = Label(p.Text, 15, FontStyle.Normal, TextAnchor.MiddleLeft);
        _wrapLabel.wordWrap = true;
        _value = Label(p.Accent, 13, FontStyle.Bold, TextAnchor.MiddleRight);
        _value.wordWrap = false;
        _value.clipping = TextClipping.Clip;
        _valueC = Label(p.Accent, 13, FontStyle.Bold, TextAnchor.MiddleCenter);
        _valueC.wordWrap = false;
        _valueC.clipping = TextClipping.Clip;
        _valueClip = Label(p.Accent, 13, FontStyle.Bold, TextAnchor.MiddleLeft);
        _valueClip.wordWrap = false;
        _valueClip.clipping = TextClipping.Clip;
        _muted = Label(p.Muted, 13, FontStyle.Normal, TextAnchor.MiddleLeft);
        _mutedClip = Label(p.Muted, 13, FontStyle.Normal, TextAnchor.MiddleLeft);
        _mutedClip.wordWrap = false;
        _mutedClip.clipping = TextClipping.Clip;
        _searchHint = Label(p.Muted, 13, FontStyle.Normal, TextAnchor.MiddleLeft);
        _searchHint.wordWrap = false;
        _searchHint.clipping = TextClipping.Clip;
        _searchText = Label(p.Text, 15, FontStyle.Normal, TextAnchor.MiddleLeft);
        _searchText.wordWrap = false;
        _searchText.clipping = TextClipping.Clip;
        _footer = Label(p.Muted, 12, FontStyle.Normal, TextAnchor.MiddleLeft);
        _btnLabel = Label(p.Accent, 13, FontStyle.Bold, TextAnchor.MiddleCenter);
        _centerMuted = Label(A(p.Muted, 0.8f), 14, FontStyle.Normal, TextAnchor.MiddleCenter);
        _arrow = Label(p.Accent, 15, FontStyle.Bold, TextAnchor.MiddleCenter);
        _smallBtn = Label(Color.white, 11, FontStyle.Bold, TextAnchor.MiddleCenter);
        _cardVal = Label(p.Accent, 12, FontStyle.Bold, TextAnchor.MiddleRight);
        _cardVal.wordWrap = false;
        _cardVal.clipping = TextClipping.Clip;
        _tabOn = Label(Color.white, 10, FontStyle.Bold, TextAnchor.MiddleCenter);
        _tabOn.wordWrap = false;
        _tabOff = Label(p.Muted, 10, FontStyle.Bold, TextAnchor.MiddleCenter);
        _tabOff.wordWrap = false;
        _rowName = Label(p.Text, 14, FontStyle.Normal, TextAnchor.MiddleLeft);
        _rowName.wordWrap = false;
        _rowName.clipping = TextClipping.Clip;
        _rowSel = Label(Color.white, 14, FontStyle.Bold, TextAnchor.MiddleLeft);
        _rowSel.wordWrap = false;
        _rowSel.clipping = TextClipping.Clip;
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
