using System.Collections.Generic;
using Il2CppInterop.Runtime.Attributes;
using UnityEngine;

namespace Nocturne;

public sealed class NocturneReplay : MonoBehaviour
{
    internal enum Rt
    {
        Kill, Vent, Report, Shift, Protect, Sabotage, Meeting
    }

    private struct Ev
    {
        public float T; public byte Type; public byte Pid; public Vector2 A; public Vector2 B;
    }
    private struct Pt
    {
        public Vector2 P; public float T;
    }

    private const int MaxEv = 500;
    private const int MaxPts = 600;

    private static readonly List<Ev> _events = new List<Ev>(MaxEv);
    private static readonly Dictionary<byte, List<Pt>> _paths = new Dictionary<byte, List<Pt>>();
    private static readonly Dictionary<byte, int> _color = new Dictionary<byte, int>();
    private static int _key = int.MinValue;
    private static float _tMin, _tMax;
    private static bool _hasT;

    private float _poll;
    private bool _keyPrev;
    private bool _hadShip;
    private bool _wasMeeting;

    private Rect _win = Ui.R(70f, 90f, 480f, 440f);
    private bool _drag;
    private Vector2 _grab;
    private bool _built;
    private float _scrub = 1f;
    private bool _live = true;
    private bool _playing;
    private byte _focus = 255;
    private GUIStyle _title, _chip, _muted;

    private static Vector2 _min, _max;
    private static int _bmap = -999;
    private static readonly List<(Vector2 mn, Vector2 mx)> _rooms = new List<(Vector2 mn, Vector2 mx)>();

    internal static bool Open => NocturneConfig.ReplayView.Value;
    private static int Mask => NocturneConfig.ReplayFilterMask.Value;

    internal static void Rec(Rt type, byte pid, Vector2 a, Vector2 b)
    {
        float t = Time.time;
        Stamp(t);
        while (_events.Count >= MaxEv)
            _events.RemoveAt(0);
        _events.Add(new Ev { T = t, Type = (byte)type, Pid = pid, A = a, B = b });
    }

    private static void Stamp(float t)
    {
        if (!_hasT)
        {
            _tMin = t;
            _hasT = true;
        }
        _tMax = t;
    }

    public void Update()
    {
        KeyToggle();
        Playback();

        float now = Time.realtimeSinceStartup;
        if (now < _poll) return;
        _poll = now + 0.1f;
        try
        {
            ShipStatus s = ShipStatus.Instance;
            PlayerControl me = PlayerControl.LocalPlayer;
            if (s == null || me == null)
            {
                _hadShip = false;
                _wasMeeting = false;
                return;
            }
            if (!_hadShip)
            {
                Clear();
                _hadShip = true;
            }
            int key = AmongUsClient.Instance != null ? AmongUsClient.Instance.GameId : 0;
            if (key != _key)
            {
                Clear();
                _key = key;
            }

            if (MeetingHud.Instance != null || ExileController.Instance != null)
            {
                _wasMeeting = true;
                return;
            }
            if (_wasMeeting)
            {
                _wasMeeting = false;
                if (NocturneConfig.ReplayClearAfterMeeting.Value)
                    Clear();
                else
                    ClearPaths();
            }

            float t = Time.time;
            foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
            {
                if (pc == null || pc.Data == null || pc.Data.IsDead)
                    continue;
                byte id = pc.PlayerId;
                Vector2 p = pc.GetTruePosition();
                if (!_paths.TryGetValue(id, out var list))
                {
                    list = new List<Pt>(64);
                    _paths[id] = list;
                }
                if (list.Count == 0 || (list[list.Count - 1].P - p).sqrMagnitude > 0.02f)
                {
                    Stamp(t);
                    list.Add(new Pt { P = p, T = t });
                    if (list.Count > MaxPts)
                        list.RemoveAt(0);
                }
                _color[id] = pc.Data.DefaultOutfit != null ? pc.Data.DefaultOutfit.ColorId : 0;
            }
        }
        catch { }
    }

    private void Playback()
    {
        if (!_playing)
            return;
        if (!Open || !_hasT || _tMax <= _tMin)
        {
            _playing = false;
            return;
        }
        float total = _tMax - _tMin;
        _scrub = Ui.Clamp01(_scrub + Time.unscaledDeltaTime / Ui.Max(0.5f, total));
        _live = false;
        if (_scrub >= 1f)
        {
            _scrub = 1f;
            _playing = false;
            _live = true;
        }
    }

    private void KeyToggle()
    {
        if (NocturneConfig.ReplayKey.Value == KeyCode.None)
        {
            _keyPrev = false;
            return;
        }
        bool down = NocturneKeys.Held(NocturneConfig.ReplayKey);
        if (down && !_keyPrev && !NocturneMenu.Typing)
            NocturneConfig.ReplayView.Value = !NocturneConfig.ReplayView.Value;
        _keyPrev = down;
    }

    private static void Clear()
    {
        _events.Clear();
        _paths.Clear();
        _color.Clear();
        _hasT = false;
        _tMin = _tMax = 0f;
    }

    private static void ClearPaths()
    {
        _paths.Clear();
        _color.Clear();
    }

    internal void DrawGui()
    {
        if (!Open) return;
        Build();

        float sc = Ui.Clamp(Screen.height / 1080f, 1f, 2.4f);
        Matrix4x4 mtx = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(Ui.V3(sc, sc, 1f));
        try
        {
            float sw = Screen.width / sc, sh = Screen.height / sc;
            Event e = Event.current;
            NocturneStyle.ClampWindow(ref _win, sw, sh, 360f, 260f);
            Patches.MouseShield.Cover(_win);

            NocturnePalette p = NocturneStyle.Current;
            float w = _win.m_Width, h = _win.m_Height;

            NocturneStyle.FillRounded(_win, A(p.Window, 0.95f), 12);
            NocturneStyle.StrokeRounded(_win, A(p.Accent, 0.28f), 12, 1);
            NocturneStyle.Fill(Ui.R(_win.m_XMin + 10f, _win.m_YMin + 30f - 1f, w - 20f, 1f), A(p.Accent, 0.35f));

            NocturneStyle.FillRounded(Ui.R(_win.m_XMin + 12f, _win.m_YMin + 9f, 3f, 12f), p.Accent, 1);
            _title.normal.textColor = p.Text;
            GUI.Label(Ui.R(_win.m_XMin + 20f, _win.m_YMin + 5f, w - 200f, 20f), NocturneText.T("РАЗБОР МАТЧА", "MATCH REVIEW"), _title);

            Rect clr = Ui.R(_win.m_XMin + w - 178f, _win.m_YMin + 6f, 20f, 18f);
            if (Hover(clr))
                NocturneStyle.FillRounded(clr, A(Ui.White, 0.08f), 6);
            NocturneIcons.Draw(NocturneIcon.Trash, Ui.R(clr.m_XMin + clr.m_Width / 2f - 7f, clr.m_YMin + 2f, 14f, 14f), Hover(clr) ? p.Text : p.Muted);
            if (GUI.Button(clr, NocturneStyle.Blank, GUIStyle.none))
                Clear();

            Rect foc = Ui.R(_win.m_XMin + w - 154f, _win.m_YMin + 6f, 118f, 18f);
            NocturneStyle.FillRounded(foc, _focus == 255 ? A(Ui.White, 0.06f) : A(p.Accent, 0.22f), 5);
            _chip.normal.textColor = _focus == 255 ? A(p.Muted, 0.9f) : p.Accent;
            GUI.Label(foc, _focus == 255 ? NocturneText.T("Игрок: все", "Player: all") : Trunc(NameById(_focus), 12), _chip);
            if (GUI.Button(foc, NocturneStyle.Blank, GUIStyle.none))
                CycleFocus();

            Rect cls = Ui.R(_win.m_XMin + w - 26f, _win.m_YMin + 6f, 20f, 18f);
            if (Hover(cls))
                NocturneStyle.FillRounded(cls, A(Ui.C(0.9f, 0.3f, 0.3f, 1f), 0.25f), 6);
            NocturneIcons.Draw(NocturneIcon.Close, Ui.R(cls.m_XMin + cls.m_Width / 2f - 7f, cls.m_YMin + 2f, 14f, 14f), Hover(cls) ? Ui.C(0.95f, 0.5f, 0.5f, 1f) : p.Muted);
            if (GUI.Button(cls, NocturneStyle.Blank, GUIStyle.none))
            {
                NocturneConfig.ReplayView.Value = false;
                return;
            }

            string[] cru = { "Килл", "Вент", "Реп", "Обор", "Щит", "Сабо", "Собр", "Пути" };
            string[] cen = { "Kill", "Vent", "Rep", "Shift", "GA", "Sabo", "Meet", "Paths" };
            int cols = cru.Length;
            float gap = 4f;
            float cw = (w - 12f - (cols - 1) * gap) / cols;
            int mask = Mask;
            for (int i = 0; i < cols; i++)
            {
                Rect fr = Ui.R(_win.m_XMin + 6f + i * (cw + gap), _win.m_YMin + 34f, cw, 18f);
                bool on = (mask & (1 << i)) != 0;
                NocturneStyle.FillRounded(fr, on ? A(p.Accent, 0.22f) : A(Ui.White, 0.05f), 5);
                _chip.normal.textColor = on ? p.Accent : A(p.Muted, 0.8f);
                GUI.Label(fr, NocturneText.T(cru[i], cen[i]), _chip);
                if (GUI.Button(fr, NocturneStyle.Blank, GUIStyle.none))
                    NocturneConfig.ReplayFilterMask.Value = mask ^ (1 << i);
            }

            Rect body = Ui.R(_win.m_XMin + 8f, _win.m_YMin + 58f, w - 16f, h - 58f - 30f);
            NocturneStyle.FillRounded(body, A(Ui.C(0.20f, 0.22f, 0.26f, 1f), 0.55f), 6);

            Rect playBtn = Ui.R(_win.m_XMin + 10f, _win.m_YMin + h - 26f, 22f, 20f);
            Rect slider = Ui.R(_win.m_XMin + 36f, _win.m_YMin + h - 24f, w - 46f, 16f);
            if (e != null && (NocturneStyle.Is(EventType.MouseDown) || NocturneStyle.Is(EventType.MouseDrag)) && e.button == 0 && Ui.In(slider, e.mousePosition))
            {
                _scrub = Ui.Clamp01((e.mousePosition.x - slider.m_XMin) / slider.m_Width);
                _live = _scrub >= 0.985f;
                if (_live)
                    _playing = false;
                e.Use();
            }

            NocturneStyle.FillRounded(playBtn, Hover(playBtn) ? A(p.Accent, 0.22f) : A(Ui.White, 0.06f), 5);
            _chip.normal.textColor = _playing ? p.Accent : p.Text;
            GUI.Label(playBtn, _playing ? "II" : "▶", _chip);
            if (e != null && NocturneStyle.Is(EventType.MouseDown) && e.button == 0 && Ui.In(playBtn, e.mousePosition))
            {
                if (_live || _scrub >= 0.999f)
                {
                    _scrub = 0f;
                    _playing = true;
                    _live = false;
                }
                else
                    _playing = !_playing;
                e.Use();
            }

            float viewT = _hasT ? (_live ? _tMax : Ui.Lerp(_tMin, _tMax, _scrub)) : 0f;

            if (ShipStatus.Instance != null && PlayerControl.LocalPlayer != null)
                Bounds();
            bool haveMap = _hasT && _min.x <= _max.x;
            if (!haveMap)
            {
                _muted.normal.textColor = A(p.Muted, 0.85f);
                GUI.Label(body, NocturneText.T("Нет данных. Сыграй раунд.", "No data yet. Play a round."), _muted);
            }
            else if ((e == null || NocturneStyle.Painting) && body.m_Width > 8f && body.m_Height > 8f)
            {
                GUI.BeginGroup(body);
                DrawMap(Ui.R(0f, 0f, body.m_Width, body.m_Height), sc, mask, viewT, _focus);
                GUI.EndGroup();
            }

            DrawSlider(slider, sc, mask, p, viewT);

            Drag(e, Ui.R(_win.m_XMin, _win.m_YMin, w - 160f, 30f));
        }
        finally { GUI.matrix = mtx; }
    }

    [HideFromIl2Cpp]
    private void DrawSlider(Rect s, float sc, int mask, NocturnePalette p, float viewT)
    {
        NocturneStyle.FillRounded(s, A(Ui.C(0.14f, 0.15f, 0.18f, 1f), 0.9f), 5);
        if (_hasT && _tMax > _tMin)
        {
            for (int i = 0; i < _events.Count; i++)
            {
                Ev ev = _events[i];
                if ((mask & (1 << ev.Type)) == 0)
                    continue;
                if (_focus != 255 && ev.Pid != 255 && ev.Pid != _focus) continue;
                float n = (ev.T - _tMin) / (_tMax - _tMin);
                float x = s.m_XMin + 3f + n * (s.m_Width - 6f);
                NocturneStyle.Fill(Ui.R(x - 1.2f * sc, s.m_YMin + 1f, 2.4f * sc, s.m_Height - 2f), EvColor(ev.Type));
            }
        }
        float hx = _live ? Ui.Right(s) - 4f : s.m_XMin + 3f + _scrub * (s.m_Width - 6f);
        NocturneStyle.FillRounded(Ui.R(hx - 2f * sc, s.m_YMin - 1f, 4f * sc, s.m_Height + 2f), _live ? Ui.C(0.35f, 0.85f, 0.5f, 1f) : p.Accent, 2);

        float total = Ui.Max(0f, _tMax - _tMin);
        float cur = Ui.Clamp(viewT - _tMin, 0f, total);
        Rect tl = Ui.R(Ui.Right(s) - 78f, s.m_YMin, 74f, s.m_Height);
        NocturneStyle.FillRounded(tl, A(Ui.C(0.07f, 0.08f, 0.10f, 1f), 0.92f), 4);
        _chip.normal.textColor = _live ? Ui.C(0.35f, 0.85f, 0.5f, 1f) : p.Text;
        GUI.Label(tl, _live ? "LIVE" : (Fmt(cur) + " / " + Fmt(total)), _chip);
    }

    private static string Fmt(float sec)
    {
        if (sec < 0f)
            sec = 0f;
        int m = (int)(sec / 60f), ss = (int)(sec % 60f);
        return m + ":" + ss.ToString("00");
    }

    private void DrawMap(Rect r, float sc, int mask, float viewT, byte focus)
    {
        for (int i = 0; i < _rooms.Count; i++)
        {
            Vector2 a = Map(_rooms[i].mn, r);
            Vector2 d = Map(_rooms[i].mx, r);
            NocturneStyle.FillRounded(Ui.R(Ui.Min(a.x, d.x), Ui.Min(a.y, d.y), Ui.Abs(d.x - a.x), Ui.Abs(d.y - a.y)), Ui.C(0.55f, 0.6f, 0.72f, 0.10f), 4);
        }

        if ((mask & (1 << 7)) != 0)
        {
            foreach (var kv in _paths)
            {
                if (focus != 255 && kv.Key != focus)
                    continue;
                List<Pt> list = kv.Value;
                if (list.Count < 2)
                    continue;
                Color col = PlayerColor(_color.TryGetValue(kv.Key, out int cid) ? cid : 0);
                float wdt = 2.2f * sc;
                int last = -1;
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i].T > viewT)
                        break;
                    last = i;
                }
                if (last < 1)
                    continue;
                bool detail = focus != 255;
                int stride = detail ? 1 : Ui.Max(1, last / 200);
                float jumpSq = 9f * stride;
                for (int i = stride; i <= last; i += stride)
                {
                    int j = i - stride;
                    Vector2 wa = list[j].P, wb = list[i].P;
                    if ((wb - wa).sqrMagnitude > jumpSq)
                        continue;
                    float a = 0.14f + 0.55f * ((float)i / (last + 1));
                    Color lc = Ui.C(col.r, col.g, col.b, a);
                    if (!detail)
                    {
                        Line(Map(wa, r), Map(wb, r), lc, wdt);
                        continue;
                    }
                    Vector2 w0 = list[Ui.Max(0, j - 1)].P, w3 = list[Ui.Min(last, i + 1)].P;
                    if ((wa - w0).sqrMagnitude > 9f)
                        w0 = wa;
                    if ((w3 - wb).sqrMagnitude > 9f)
                        w3 = wb;
                    Vector2 m0 = Map(w0, r), m1 = Map(wa, r), m2 = Map(wb, r), m3 = Map(w3, r);
                    int steps = Ui.Clamp((int)((m2 - m1).magnitude / 10f), 1, 4);
                    Vector2 pp = m1;
                    for (int s = 1; s <= steps; s++)
                    {
                        Vector2 q = steps == 1 ? m2 : Catmull(m0, m1, m2, m3, (float)s / steps);
                        Line(pp, q, lc, wdt);
                        pp = q;
                    }
                }
            }
        }

        for (int i = 0; i < _events.Count; i++)
        {
            Ev ev = _events[i];
            if (ev.Type > (byte)Rt.Protect)
                continue;
            if ((mask & (1 << ev.Type)) == 0 || ev.T > viewT)
                continue;
            if (focus != 255 && ev.Pid != focus)
                continue;
            Color col = EvColor(ev.Type);
            Vector2 pa = Map(ev.A, r);
            if (ev.Type == (byte)Rt.Kill)
            {
                Vector2 pb = Map(ev.B, r);
                if ((ev.B - ev.A).sqrMagnitude < 100f)
                    Line(pa, pb, Ui.C(0.95f, 0.25f, 0.25f, 0.8f), 2.4f * sc);
                Marker(pb, sc, Ui.C(0.9f, 0.28f, 0.28f, 1f), "x");
            }
            Marker(pa, sc, col, EvLetter(ev.Type));
        }

        foreach (var kv in _paths)
        {
            if (focus != 255 && kv.Key != focus)
                continue;
            if (!PosAt(kv.Value, viewT, out Vector2 pos))
                continue;
            Vector2 sp = Map(pos, r);
            Color col = PlayerColor(_color.TryGetValue(kv.Key, out int cid) ? cid : 0);
            Dot(sp, 9f * sc, A(Ui.Black, 0.55f));
            Dot(sp, 7f * sc, col);
        }
    }

    private void Marker(Vector2 c, float sc, Color col, string ch)
    {
        float s = 13f * sc;
        Dot(c, s + 4f * sc, A(Ui.Black, 0.7f));
        Dot(c, s, col);
        _chip.normal.textColor = Ui.White;
        GUI.Label(Ui.R(c.x - s / 2f, c.y - s / 2f - 1f, s, s), ch, _chip);
    }

    private static string EvLetter(byte t) => t switch
    {
        (byte)Rt.Kill => "K",
        (byte)Rt.Vent => "V",
        (byte)Rt.Report => "R",
        (byte)Rt.Shift => "S",
        (byte)Rt.Protect => "P",
        _ => "",
    };

    private static bool PosAt(List<Pt> list, float t, out Vector2 pos)
    {
        pos = default;
        int n = list.Count;
        if (n == 0)
            return false;
        if (t <= list[0].T)
        {
            pos = list[0].P;
            return true;
        }
        if (t >= list[n - 1].T)
        {
            pos = list[n - 1].P;
            return true;
        }
        for (int i = 0; i < n - 1; i++)
        {
            if (t >= list[i].T && t <= list[i + 1].T)
            {
                float span = list[i + 1].T - list[i].T;
                Vector2 a = list[i].P, b = list[i + 1].P;
                if (span <= 0.0001f || (b - a).sqrMagnitude > 9f)
                {
                    pos = a;
                    return true;
                }
                float u = (t - list[i].T) / span;
                Vector2 p0 = list[Ui.Max(0, i - 1)].P, p3 = list[Ui.Min(n - 1, i + 2)].P;
                if ((a - p0).sqrMagnitude > 9f)
                    p0 = a;
                if ((p3 - b).sqrMagnitude > 9f)
                    p3 = b;
                pos = Catmull(p0, a, b, p3, u);
                return true;
            }
        }
        pos = list[n - 1].P;
        return true;
    }

    private void CycleFocus()
    {
        var ids = new List<byte>(_paths.Keys);
        ids.Sort();
        if (ids.Count == 0)
        {
            _focus = 255;
            return;
        }
        if (_focus == 255)
        {
            _focus = ids[0];
            return;
        }
        int idx = ids.IndexOf(_focus);
        _focus = (idx < 0 || idx >= ids.Count - 1) ? (byte)255 : ids[idx + 1];
    }

    private static string NameById(byte id)
    {
        try
        {
            foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
                if (pc != null && pc.PlayerId == id && pc.Data != null)
                    return pc.Data.PlayerName;
        }
        catch { }
        return "#" + id;
    }

    private static string Trunc(string s, int n) => string.IsNullOrEmpty(s) ? "" : (s.Length > n ? s.Substring(0, n) : s);

    private static bool Bounds()
    {
        int map = NocturneNav.CurrentMapId();
        if (map == _bmap && _min.x <= _max.x) return true;
        Vector2 min = Ui.V(float.MaxValue, float.MaxValue), max = Ui.V(float.MinValue, float.MinValue);
        var rooms = new List<(Vector2 mn, Vector2 mx)>();
        ShipStatus s = ShipStatus.Instance;
        if (s != null && s.AllRooms != null)
        {
            var ar = s.AllRooms;
            for (int i = 0; i < ar.Length; i++)
            {
                Collider2D c = ar[i] != null ? ar[i].roomArea : null;
                if (c == null) continue;
                Bounds b = c.bounds;
                Vector2 rmn = Ui.V(b.min.x, b.min.y), rmx = Ui.V(b.max.x, b.max.y);
                Grow(ref min, ref max, rmn);
                Grow(ref min, ref max, rmx);
                rooms.Add((rmn, rmx));
            }
        }
        if (min.x > max.x)
            return false;
        Vector2 pad = (max - min) * 0.03f + Ui.One2 * 0.5f;
        _min = min - pad;
        _max = max + pad;
        _bmap = map;
        _rooms.Clear();
        _rooms.AddRange(rooms);
        return true;
    }

    private static void Grow(ref Vector2 min, ref Vector2 max, Vector2 v)
    {
        if (v.x < min.x)
            min.x = v.x;
        if (v.y < min.y)
            min.y = v.y;
        if (v.x > max.x)
            max.x = v.x;
        if (v.y > max.y)
            max.y = v.y;
    }

    private static Vector2 Map(Vector2 w, Rect r)
    {
        float tx = (w.x - _min.x) / Ui.Max(0.01f, _max.x - _min.x);
        float ty = (w.y - _min.y) / Ui.Max(0.01f, _max.y - _min.y);
        return Ui.V(r.m_XMin + tx * r.m_Width, r.m_YMin + (1f - ty) * r.m_Height);
    }

    private static Color PlayerColor(int id)
    {
        if (Palette.PlayerColors != null && id >= 0 && id < Palette.PlayerColors.Length)
        {
            Color32 c = Palette.PlayerColors[id];
            return Ui.C(c.r / 255f, c.g / 255f, c.b / 255f, 1f);
        }
        return Ui.White;
    }

    private static Color EvColor(byte t) => t switch
    {
        (byte)Rt.Kill => Ui.C(0.95f, 0.25f, 0.25f, 1f),
        (byte)Rt.Vent => Ui.C(0.6f, 0.6f, 0.65f, 1f),
        (byte)Rt.Report => Ui.C(0.30f, 0.55f, 1f, 1f),
        (byte)Rt.Shift => Ui.C(0.75f, 0.4f, 1f, 1f),
        (byte)Rt.Protect => Ui.C(0.35f, 0.85f, 0.5f, 1f),
        (byte)Rt.Sabotage => Ui.C(1f, 0.5f, 0.12f, 1f),
        (byte)Rt.Meeting => Ui.C(1f, 0.85f, 0.25f, 1f),
        _ => Ui.White,
    };

    private static void Dot(Vector2 c, float s, Color col)
        => NocturneStyle.FillRounded(Ui.R(c.x - s / 2f, c.y - s / 2f, s, s), col, Ui.Max(1, (int)(s / 2f)));

    private static Vector2 Catmull(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
    {
        float t2 = t * t, t3 = t2 * t;
        return 0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }

    private static void Line(Vector2 a, Vector2 b, Color col, float w)
    {
        float dx = b.x - a.x, dy = b.y - a.y;
        float len = Ui.Sqrt(dx * dx + dy * dy);
        if (len < 0.5f) return;
        Matrix4x4 m = GUI.matrix;
        GUIUtility.RotateAroundPivot(Ui.Atan2(dy, dx) * Mathf.Rad2Deg, a);
        NocturneStyle.Fill(Ui.R(a.x - w * 0.5f, a.y - w * 0.5f, len + w, w), col);
        GUI.matrix = m;
    }

    private void Drag(Event e, Rect head)
    {
        if (e == null)
            return;
        if (e.type == EventType.MouseDown && e.button == 0 && Ui.In(head, e.mousePosition))
        {
            _drag = true;
            _grab = e.mousePosition - Ui.V(_win.m_XMin, _win.m_YMin);
            e.Use();
        }
        else if (_drag && e.type == EventType.MouseDrag)
        {
            _win.m_XMin = e.mousePosition.x - _grab.x;
            _win.m_YMin = e.mousePosition.y - _grab.y;
            e.Use();
        }
        else if (_drag && e.type == EventType.MouseUp)
            _drag = false;
    }

    private static bool Hover(Rect r) => Event.current != null && Ui.In(r, Event.current.mousePosition);
    private static Color A(Color c, float a) => Ui.C(c.r, c.g, c.b, a);

    private void Build()
    {
        if (_built)
            return;
        _built = true;
        _title = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, richText = true };
        _chip = new GUIStyle(GUI.skin.label) { fontSize = 11, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Clip };
        _muted = new GUIStyle(GUI.skin.label) { fontSize = 12, alignment = TextAnchor.MiddleCenter };
    }
}
