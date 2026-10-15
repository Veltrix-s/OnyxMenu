using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nocturne;

internal struct RadarMark
{
    internal Vector2 Pos;
    internal Vector2 Vel;
    internal Color Col;
    internal bool Dead;
    internal bool Me;
}

internal struct RadarRoom
{
    internal int Id;
    internal Vector2 Pos;
    internal GUIContent Name;
    internal bool Door;
    internal bool Pinned;
}

internal struct RadarLayout
{
    internal Rect Box;
    internal Rect Lock;
    internal Rect Grip;
    internal float Cs;
    internal float Ms;
    internal int Rad;
}

internal sealed class RadarFrame
{
    internal readonly List<RadarMark> Marks = new List<RadarMark>(16);
    internal readonly List<Vector2> Bodies = new List<Vector2>(8);
    internal RadarRoom[] Rooms = new RadarRoom[0];
    internal int RoomCount;
    internal GUIContent Title;
    internal bool Locked;
    internal bool Fx;
    internal bool Teleport;
    internal bool Doors;
    internal float Opacity = 1f;
    internal int Alive;
    internal int Total;
}

internal static class RadarView
{
    internal const float BaseW = 236f;

    private delegate void PixelFn(float u, float v, out float r, out float g, out float b, out float a);

    private static Texture2D _tMarker, _tArrow, _tRing, _tStreak, _tBody, _tGrip;
    private static GUIStyle _sMarker, _sArrow, _sRing, _sStreak, _sBody, _sGrip, _sMap;
    private static GUIStyle _title, _foot, _room, _roomSh;
    private static Texture2D _mapRef;
    private static float _fontSc = -1f;
    private static Color _fontAc;
    private static int _fa = -1, _ft = -1, _fd = -1, _roomFs;
    private static GUIContent _alive, _dead, _twFor;
    private static float _aliveW, _deadW, _tw;

    internal static float MaxScale(float aspect, float rs, float sw, float sh)
    {
        float lo = 0.6f, hi = 4f;
        for (int i = 0; i < 10; i++)
        {
            float mid = (lo + hi) * 0.5f;
            float x = 0f, y = 0f;
            RadarLayout l = Layout(aspect, mid, rs, ref x, ref y, 1e6f, 1e6f);
            if (l.Box.m_Width <= sw * 0.96f && l.Box.m_Height <= sh * 0.94f)
                lo = mid;
            else
                hi = mid;
        }
        return lo;
    }

    internal static RadarLayout Layout(float aspect, float userSc, float rs, ref float x, ref float y, float sw, float sh)
    {
        float k = Ui.Min(userSc, 1f);
        float over = Ui.Max(0f, userSc - 1f);
        float cs = rs * (k + 0.30f * over);
        float ms = rs * (k + 0.50f * over);

        float w = Ui.Round(BaseW * userSc * rs);
        float h = Ui.Round(Ui.Clamp(w / aspect, w * 0.42f, w * 1.15f));

        x = Ui.Round(Ui.Clamp(x, 0f, Ui.Max(0f, sw - w)));
        y = Ui.Round(Ui.Clamp(y, 0f, Ui.Max(0f, sh - h)));

        var l = new RadarLayout { Cs = cs, Ms = ms, Rad = Ui.Clamp(Ui.RoundToInt(12f * cs), 6, 40) };
        l.Box = Ui.R(x, y, w, h);
        float inset = Ui.Round(6f * cs);
        float lk = Ui.Round(19f * cs);
        l.Lock = Ui.R(x + w - inset - lk, y + inset, lk, lk);
        float gp = Ui.Round(18f * cs);
        l.Grip = Ui.R(x + w - inset - gp, y + h - inset - gp, gp, gp);
        return l;
    }

    internal static Rect DoorRect(RadarLayout b, RadarFit fit, Vector2 world)
    {
        float sz = Ui.Round(16f * b.Ms);
        Vector2 p = fit.Px(world);
        return Ui.R(Ui.Round(b.Box.m_XMin + p.x - sz * 0.5f), Ui.Round(b.Box.m_YMin + p.y - sz * 0.5f), sz, sz);
    }

    internal static void Draw(RadarLayout b, RadarFit fit, Texture2D map, RadarFrame f)
    {
        EnsureAssets();
        NocturnePalette pal = NocturneStyle.Current;
        Color ac = pal.Accent;
        float cs = b.Cs;
        bool lite = NocturneStyle.Lite;
        Vector2 mouse = NocturneStyle.Mouse;
        Rect box = b.Box;

        Color prev = NocturneStyle.Tint;
        NocturneStyle.Tint = Ui.C(1f, 1f, 1f, prev.a * f.Opacity);

        SetFonts(cs, ac, pal);

        if (!lite)
            NocturneStyle.Glow(Ui.R(box.m_XMin - 14f * cs, box.m_YMin - 8f * cs, box.m_Width + 28f * cs, box.m_Height + 30f * cs), Ui.C(ac.r * 0.20f, ac.g * 0.20f, ac.b * 0.20f, 0.5f));

        if (!ReferenceEquals(map, _mapRef))
        {
            _mapRef = map;
            _sMap.normal.background = map;
        }
        GUI.Box(box, NocturneStyle.Blank, _sMap);
        NocturneStyle.StrokeRounded(box, Ui.C(ac.r, ac.g, ac.b, 0.50f), b.Rad, 1);

        Labels(b, fit, f);

        if (f.Fx && !lite)
            Pings(b, fit, f, ac, NocturneStyle.Now);

        if (f.Doors)
            DrawDoors(b, fit, f, mouse);

        DrawBodies(b, fit, f);
        DrawMarks(b, fit, f, ac);

        if (f.Teleport && Ui.In(box, mouse))
        {
            float rg = Ui.Round(26f * b.Ms);
            Quad(Ui.R(mouse.x - rg * 0.5f, mouse.y - rg * 0.5f, rg, rg), _sRing, Ui.C(ac.r, ac.g, ac.b, 0.85f));
        }

        Overlays(b, f, ac, mouse);

        NocturneStyle.Tint = prev;
    }

    private static void Labels(RadarLayout b, RadarFit fit, RadarFrame f)
    {
        int fs = Ui.Min(16, Ui.RoundToInt(fit.S * 0.8f));
        if (fs < 9)
            return;
        if (fs != _roomFs)
        {
            _roomFs = fs;
            _room.fontSize = fs;
            _roomSh.fontSize = fs;
        }

        float lw = fs * 12f;
        float lh = fs * 1.5f;
        float lift = f.Doors ? Ui.Round(8f * b.Ms) + fs * 0.65f + 2f : fit.S * 0.8f;
        for (int i = 0; i < f.RoomCount; i++)
        {
            GUIContent name = f.Rooms[i].Name;
            if (name == null)
                continue;
            Vector2 p = fit.Px(f.Rooms[i].Pos);
            float x = b.Box.m_XMin + p.x - lw * 0.5f;
            float y = b.Box.m_YMin + p.y - lift - lh * 0.5f;
            GUI.Label(Ui.R(x + 1f, y + 1f, lw, lh), name, _roomSh);
            GUI.Label(Ui.R(x, y, lw, lh), name, _room);
        }
    }

    private static void Pings(RadarLayout b, RadarFit fit, RadarFrame f, Color ac, float now)
    {
        Vector2 me = Ui.Zero2;
        bool alive = false;
        for (int i = 0; i < f.Marks.Count; i++)
        {
            if (f.Marks[i].Me && !f.Marks[i].Dead)
            {
                me = fit.Px(f.Marks[i].Pos);
                alive = true;
            }
        }
        if (!alive)
            return;

        float ms = b.Ms;
        GUI.BeginGroup(b.Box);
        for (int k = 0; k < 2; k++)
        {
            float ph = (now / 2.6f + k * 0.5f) % 1f;
            float rad = (6f + ph * 52f) * ms;
            float a = (1f - ph) * (1f - ph) * 0.50f;
            Quad(Ui.R(me.x - rad, me.y - rad, rad * 2f, rad * 2f), _sRing, Ui.C(ac.r, ac.g, ac.b, a));
        }
        GUI.EndGroup();
    }

    private static void DrawBodies(RadarLayout b, RadarFit fit, RadarFrame f)
    {
        float sz = 16f * b.Ms;
        for (int i = 0; i < f.Bodies.Count; i++)
        {
            Vector2 p = fit.Px(f.Bodies[i]);
            float x = b.Box.m_XMin + p.x, y = b.Box.m_YMin + p.y;
            Quad(Ui.R(x - sz * 0.5f, y - sz * 0.5f, sz, sz), _sBody, Ui.White);
        }
    }

    private static void DrawMarks(RadarLayout b, RadarFit fit, RadarFrame f, Color ac)
    {
        float ms = b.Ms;
        float pxPerUnit = fit.S;
        for (int pass = 0; pass < 3; pass++)
        {
            for (int i = 0; i < f.Marks.Count; i++)
            {
                RadarMark m = f.Marks[i];
                int mine = m.Dead ? 0 : (m.Me ? 2 : 1);
                if (mine != pass)
                    continue;

                Vector2 p = fit.Px(m.Pos);
                float x = b.Box.m_XMin + p.x, y = b.Box.m_YMin + p.y;
                float speed = Ui.Sqrt(m.Vel.x * m.Vel.x + m.Vel.y * m.Vel.y);
                float ang = Ui.Atan2(-m.Vel.y, m.Vel.x) * Mathf.Rad2Deg;
                float a = m.Dead ? 0.38f : 1f;

                if (!m.Dead && speed > 0.6f)
                {
                    float len = Ui.Min(speed * 0.55f * pxPerUnit, 40f * ms);
                    if (len > 5f * ms)
                    {
                        float th = (m.Me ? 11f : 8f) * ms;
                        Matrix4x4 mx = GUI.matrix;
                        GUIUtility.RotateAroundPivot(ang, Ui.V(x, y));
                        Quad(Ui.R(x - len, y - th * 0.5f, len, th), _sStreak, Ui.C(m.Col.r, m.Col.g, m.Col.b, 0.65f));
                        GUI.matrix = mx;
                    }
                }

                if (m.Me)
                {
                    float rg = 24f * ms;
                    Quad(Ui.R(x - rg * 0.5f, y - rg * 0.5f, rg, rg), _sRing, Ui.C(ac.r, ac.g, ac.b, 0.9f));
                    if (speed > 0.6f)
                    {
                        float sz = 20f * ms;
                        Matrix4x4 mx = GUI.matrix;
                        GUIUtility.RotateAroundPivot(ang, Ui.V(x, y));
                        Quad(Ui.R(x - sz * 0.5f, y - sz * 0.5f, sz, sz), _sArrow, Ui.C(m.Col.r, m.Col.g, m.Col.b, a));
                        GUI.matrix = mx;
                    }
                    else
                    {
                        float sz = 18f * ms;
                        Quad(Ui.R(x - sz * 0.5f, y - sz * 0.5f, sz, sz), _sMarker, Ui.C(m.Col.r, m.Col.g, m.Col.b, a));
                    }
                }
                else
                {
                    float sz = (m.Dead ? 12f : 14f) * ms;
                    Quad(Ui.R(x - sz * 0.5f, y - sz * 0.5f, sz, sz), _sMarker, Ui.C(m.Col.r, m.Col.g, m.Col.b, a));
                }
            }
        }
    }

    private static void DrawDoors(RadarLayout b, RadarFit fit, RadarFrame f, Vector2 mouse)
    {
        int rr = Ui.RoundToInt(4f * b.Ms);
        for (int i = 0; i < f.RoomCount; i++)
        {
            if (!f.Rooms[i].Door)
                continue;
            Rect r = DoorRect(b, fit, f.Rooms[i].Pos);
            bool hot = Ui.In(r, mouse);
            bool pin = f.Rooms[i].Pinned;
            Color edge = pin ? Ui.C(0.96f, 0.30f, 0.30f, 1f) : (hot ? Ui.C(1f, 0.58f, 0.42f, 1f) : Ui.C(0.96f, 0.76f, 0.36f, 1f));
            Color fill = pin ? Ui.C(0.24f, 0.06f, 0.06f, 1f) : Ui.C(0.09f, 0.09f, 0.12f, 1f);
            NocturneStyle.FillRounded(r, Ui.C(fill.r, fill.g, fill.b, hot ? 0.97f : 0.86f), rr);
            NocturneStyle.StrokeRounded(r, edge, rr, 1);
            float isz = Ui.Round(r.m_Width * 0.66f);
            NocturneIcons.Draw(pin ? NocturneIcon.Lock : NocturneIcon.Door, Ui.R(r.m_XMin + Ui.Round((r.m_Width - isz) * 0.5f), r.m_YMin + Ui.Round((r.m_Height - isz) * 0.5f), isz, isz), edge);
        }
    }

    private static void Overlays(RadarLayout b, RadarFrame f, Color ac, Vector2 mouse)
    {
        float cs = b.Cs;
        Rect box = b.Box;
        float inset = Ui.Round(6f * cs);
        float ch = Ui.Round(19f * cs);
        float pad = Ui.Round(7f * cs);
        float gap = Ui.Round(5f * cs);
        int cr = Ui.RoundToInt(6f * cs);
        Color glass = Ui.C(0.035f, 0.04f, 0.055f, 0.72f);
        Color rim = Ui.C(1f, 1f, 1f, 0.09f);

        if (!ReferenceEquals(f.Title, _twFor))
        {
            _twFor = f.Title;
            _tw = _title.CalcSize(f.Title).x;
        }
        float isz = Ui.Round(12f * cs);
        Rect tr = Ui.R(box.m_XMin + inset, box.m_YMin + inset, pad + isz + gap + _tw + pad, ch);
        NocturneStyle.FillRounded(tr, glass, cr);
        NocturneStyle.StrokeRounded(tr, rim, cr, 1);
        NocturneIcons.Draw(NocturneIcon.Radar, Ui.R(tr.m_XMin + pad, tr.m_YMin + Ui.Round((ch - isz) * 0.5f), isz, isz), ac);
        GUI.Label(Ui.R(tr.m_XMin + pad + isz + gap, tr.m_YMin, _tw + 2f, ch), f.Title, _title);

        bool lockHot = Ui.In(b.Lock, mouse);
        Color lockCol = f.Locked ? Ui.C(0.96f, 0.34f, 0.34f, 1f) : (lockHot ? Ui.C(0.86f, 0.90f, 0.96f, 1f) : Ui.C(0.62f, 0.67f, 0.75f, 1f));
        NocturneStyle.FillRounded(b.Lock, f.Locked ? Ui.C(0.30f, 0.07f, 0.07f, 0.80f) : glass, cr);
        NocturneStyle.StrokeRounded(b.Lock, lockHot || f.Locked ? Ui.C(lockCol.r, lockCol.g, lockCol.b, 0.50f) : rim, cr, 1);
        float li = Ui.Round(12f * cs);
        NocturneIcons.Draw(f.Locked ? NocturneIcon.Lock : NocturneIcon.Unlock, Ui.R(b.Lock.m_XMin + Ui.Round((b.Lock.m_Width - li) * 0.5f), b.Lock.m_YMin + Ui.Round((b.Lock.m_Height - li) * 0.5f), li, li), lockCol);

        if (f.Alive != _fa || f.Total != _ft)
        {
            _fa = f.Alive;
            _ft = f.Total;
            _alive = new GUIContent(_fa + "/" + _ft);
            _aliveW = _foot.CalcSize(_alive).x;
        }
        int bodies = f.Bodies.Count;
        if (bodies > 0 && bodies != _fd)
        {
            _fd = bodies;
            _dead = new GUIContent(bodies.ToString());
            _deadW = _foot.CalcSize(_dead).x;
        }

        float dot = Ui.Round(6f * cs);
        float xs = Ui.Round(9f * cs);
        float sw = pad + dot + gap + _aliveW + pad;
        if (bodies > 0)
            sw += gap + xs + gap + _deadW;
        Rect sr = Ui.R(box.m_XMin + inset, box.m_YMin + box.m_Height - inset - ch, sw, ch);
        NocturneStyle.FillRounded(sr, glass, cr);
        NocturneStyle.StrokeRounded(sr, rim, cr, 1);
        float cx = sr.m_XMin + pad;
        NocturneStyle.FillRounded(Ui.R(cx, sr.m_YMin + Ui.Round((ch - dot) * 0.5f), dot, dot), Ui.C(0.36f, 0.92f, 0.52f, 1f), Ui.RoundToInt(dot * 0.5f));
        cx += dot + gap;
        GUI.Label(Ui.R(cx, sr.m_YMin, _aliveW + 2f, ch), _alive, _foot);
        if (bodies > 0)
        {
            cx += _aliveW + gap;
            NocturneIcons.Draw(NocturneIcon.Close, Ui.R(cx, sr.m_YMin + Ui.Round((ch - xs) * 0.5f), xs, xs), Ui.C(0.96f, 0.36f, 0.36f, 1f));
            cx += xs + gap;
            GUI.Label(Ui.R(cx, sr.m_YMin, _deadW + 2f, ch), _dead, _foot);
        }

        bool gripHot = Ui.In(b.Grip, mouse);
        NocturneStyle.FillRounded(b.Grip, glass, cr);
        float gs = Ui.Round(12f * cs);
        Quad(Ui.R(b.Grip.m_XMin + Ui.Round((b.Grip.m_Width - gs) * 0.5f), b.Grip.m_YMin + Ui.Round((b.Grip.m_Height - gs) * 0.5f), gs, gs), _sGrip, gripHot ? Ui.C(ac.r, ac.g, ac.b, 0.95f) : Ui.C(1f, 1f, 1f, 0.42f));
    }

    private static void Quad(Rect r, GUIStyle st, Color c)
    {
        Color prev = NocturneStyle.Tint;
        NocturneStyle.Tint = Ui.C(c.r, c.g, c.b, c.a * prev.a);
        GUI.Box(r, NocturneStyle.Blank, st);
        NocturneStyle.Tint = prev;
    }

    private static void SetFonts(float cs, Color ac, NocturnePalette pal)
    {
        if (Ui.Abs(cs - _fontSc) < 0.004f && Ui.Same(ac, _fontAc))
            return;
        _fontSc = cs;
        _fontAc = ac;
        _title.fontSize = Ui.Max(8, Ui.RoundToInt(10.5f * cs));
        _title.normal.textColor = pal.Text;
        _foot.fontSize = Ui.Max(8, Ui.RoundToInt(10f * cs));
        _foot.normal.textColor = pal.Muted;
        _room.normal.textColor = Ui.C(0.84f, 0.90f, 0.98f, 0.68f);
        _roomSh.normal.textColor = Ui.C(0f, 0f, 0f, 0.75f);
        _twFor = null;
        _fa = -1;
        _fd = -1;
    }

    private static void EnsureAssets()
    {
        if (_sMap != null)
            return;

        _tMarker = Make(64, 64, MarkerPx);
        _tArrow = Make(64, 64, ArrowPx);
        _tRing = Make(128, 128, RingPx);
        _tStreak = Make(64, 16, StreakPx);
        _tBody = Make(64, 64, BodyPx);
        _tGrip = Make(16, 16, GripPx);

        _sMarker = StyleOf(_tMarker);
        _sArrow = StyleOf(_tArrow);
        _sRing = StyleOf(_tRing);
        _sStreak = StyleOf(_tStreak);
        _sBody = StyleOf(_tBody);
        _sGrip = StyleOf(_tGrip);
        _sMap = new GUIStyle();

        _title = TextStyle(FontStyle.Bold, TextAnchor.MiddleLeft);
        _foot = TextStyle(FontStyle.Bold, TextAnchor.MiddleLeft);
        _room = TextStyle(FontStyle.Normal, TextAnchor.MiddleCenter);
        _roomSh = TextStyle(FontStyle.Normal, TextAnchor.MiddleCenter);
        _fontSc = -1f;
        _roomFs = 0;
    }

    private static GUIStyle StyleOf(Texture2D t)
    {
        var s = new GUIStyle();
        s.normal.background = t;
        return s;
    }

    private static GUIStyle TextStyle(FontStyle fs, TextAnchor an)
    {
        var s = new GUIStyle(GUI.skin.label)
        {
            fontStyle = fs,
            alignment = an,
            wordWrap = false,
            richText = false,
            clipping = TextClipping.Clip,
        };
        s.padding = NocturneStyle.Offset(0, 0, 0, 0);
        return s;
    }

    private static Texture2D Make(int w, int h, PixelFn fn)
    {
        var t = new Texture2D(w, h, TextureFormat.RGBA32, true)
        {
            hideFlags = HideFlags.HideAndDontSave,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Trilinear
        };
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                fn((x + 0.5f) / w * 2f - 1f, (y + 0.5f) / h * 2f - 1f, out float r, out float g, out float b, out float a);
                ref Color32 o = ref px[(h - 1 - y) * w + x];
                o.r = (byte)(Ui.Clamp01(r) * 255f + 0.5f);
                o.g = (byte)(Ui.Clamp01(g) * 255f + 0.5f);
                o.b = (byte)(Ui.Clamp01(b) * 255f + 0.5f);
                o.a = (byte)(Ui.Clamp01(a) * 255f + 0.5f);
            }
        }
        t.SetPixels32(px);
        t.Apply();
        return t;
    }

    private static float Cov(float d) => Ui.Clamp01(0.5f - d);

    private static void Over(ref float pc, ref float pa, float c, float a)
    {
        pc = c * a + pc * (1f - a);
        pa = a + pa * (1f - a);
    }

    private static void MarkerPx(float u, float v, out float r, out float g, out float b, out float a)
    {
        float rad = Ui.Sqrt(u * u + v * v);
        float px = 1f / 32f;
        float sh = Ui.Clamp01((0.98f - rad) / 0.28f);
        float pc = 0f, pa = sh * sh * 0.40f;
        Over(ref pc, ref pa, 0f, Cov((rad - 0.70f) / px) * 0.92f);
        float lit = Ui.Clamp01(0.5f - (u + v) * 0.5f);
        Over(ref pc, ref pa, 0.80f + 0.20f * lit, Cov((rad - 0.55f) / px));
        float c = pa > 0.0001f ? pc / pa : 0f;
        r = c;
        g = c;
        b = c;
        a = pa;
    }

    private static readonly float[] ArrowShape = { 0.82f, 0f, -0.60f, -0.62f, -0.20f, 0f, -0.60f, 0.62f };

    private static float PolySd(float[] p, float x, float y)
    {
        int n = p.Length / 2;
        float best = float.MaxValue;
        bool inside = false;
        for (int i = 0, j = n - 1; i < n; j = i, i++)
        {
            float ax = p[j * 2], ay = p[j * 2 + 1];
            float ex = p[i * 2] - ax, ey = p[i * 2 + 1] - ay;
            float wx = x - ax, wy = y - ay;
            float t = Ui.Clamp01((wx * ex + wy * ey) / (ex * ex + ey * ey));
            float dx = wx - ex * t, dy = wy - ey * t;
            best = MathF.Min(best, dx * dx + dy * dy);
            if ((ay > y) != (ay + ey > y) && x < ax + (y - ay) * ex / ey)
                inside = !inside;
        }
        float d = MathF.Sqrt(best);
        return inside ? -d : d;
    }

    private static void ArrowPx(float u, float v, out float r, out float g, out float b, out float a)
    {
        float px = 1f / 32f;
        float d = PolySd(ArrowShape, u, v) - 0.06f;
        float sh = Ui.Clamp01((0.42f - d) / 0.32f);
        float pc = 0f, pa = sh * sh * 0.38f;
        Over(ref pc, ref pa, 0f, Cov((d - 0.12f) / px) * 0.92f);
        float lit = Ui.Clamp01(0.5f - (u + v) * 0.5f);
        Over(ref pc, ref pa, 0.82f + 0.18f * lit, Cov(d / px));
        float c = pa > 0.0001f ? pc / pa : 0f;
        r = c;
        g = c;
        b = c;
        a = pa;
    }

    private static void RingPx(float u, float v, out float r, out float g, out float b, out float a)
    {
        float rad = Ui.Sqrt(u * u + v * v);
        float px = 1f / 64f;
        float ring = Cov((Ui.Abs(rad - 0.86f) - 0.04f) / px);
        float inner = rad < 0.86f ? 0.07f * Ui.Clamp01(rad / 0.86f) * Ui.Clamp01(rad / 0.86f) : 0f;
        r = 1f;
        g = 1f;
        b = 1f;
        a = Ui.Max(ring, inner);
    }

    private static void StreakPx(float u, float v, out float r, out float g, out float b, out float a)
    {
        float along = (u + 1f) * 0.5f;
        float hw = 0.16f + 0.62f * along;
        float across = Cov((Ui.Abs(v) - hw) / (2f / 16f));
        r = 1f;
        g = 1f;
        b = 1f;
        a = across * Ui.Pow(along, 1.3f) * 0.9f;
    }

    private static void BodyPx(float u, float v, out float r, out float g, out float b, out float a)
    {
        float px = 1f / 32f;
        float dd = Ui.Abs(u) + Ui.Abs(v);
        float k = 1.4142f;
        float sh = Ui.Clamp01((1.08f - dd) / 0.36f);
        float pr = 0f, pg = 0f, pb = 0f, pa = sh * sh * 0.40f;
        float oa = Cov(((dd - 0.82f) / k) / px) * 0.95f;
        pr = 0.30f * oa + pr * (1f - oa);
        pg = 0.04f * oa + pg * (1f - oa);
        pb = 0.05f * oa + pb * (1f - oa);
        pa = oa + pa * (1f - oa);
        float fa = Cov(((dd - 0.64f) / k) / px);
        float lit = Ui.Clamp01(0.5f - (u + v) * 0.5f);
        pr = (0.82f + 0.16f * lit) * fa + pr * (1f - fa);
        pg = (0.20f + 0.12f * lit) * fa + pg * (1f - fa);
        pb = (0.22f + 0.12f * lit) * fa + pb * (1f - fa);
        pa = fa + pa * (1f - fa);
        float m = Ui.Max(Ui.Abs(u), Ui.Abs(v));
        float cross = Ui.Max(Cov((Ui.Abs(u - v) / k - 0.06f) / px), Cov((Ui.Abs(u + v) / k - 0.06f) / px)) * Cov((m - 0.34f) / px);
        pr = cross + pr * (1f - cross);
        pg = cross + pg * (1f - cross);
        pb = cross + pb * (1f - cross);
        pa = cross + pa * (1f - cross);
        float inv = pa > 0.0001f ? 1f / pa : 0f;
        r = pr * inv;
        g = pg * inv;
        b = pb * inv;
        a = pa;
    }

    private static readonly float[] GripDots = { 3f, 12f, 7f, 12f, 11f, 12f, 7f, 8f, 11f, 8f, 11f, 4f };

    private static void GripPx(float u, float v, out float r, out float g, out float b, out float a)
    {
        float x = (u + 1f) * 8f, y = (v + 1f) * 8f;
        float best = 9f;
        for (int i = 0; i < GripDots.Length; i += 2)
        {
            float dx = x - GripDots[i], dy = y - GripDots[i + 1];
            best = Ui.Min(best, Ui.Sqrt(dx * dx + dy * dy));
        }
        r = 1f;
        g = 1f;
        b = 1f;
        a = Cov(best - 1.15f);
    }
}
