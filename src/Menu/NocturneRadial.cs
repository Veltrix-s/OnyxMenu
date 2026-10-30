using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace Nocturne;

public sealed class NocturneRadial : MonoBehaviour
{
    internal void DrawGui() => RadialMenu.Draw();
}

internal static class RadialMenu
{
    private const int PageCap = 20;
    private const int Reach = 12;
    private const float OneH = 30f;
    private const float TwoH = 46f;
    private const float PadL = 24f;
    private const float PadR = 14f;
    private const float HubR = 62f;
    private const float Spread = 0.14f;
    private const float OpenSpeed = 1f / 0.46f;
    private const float CloseSpeed = 1f / 0.26f;

    private sealed class Slot
    {
        internal string Key;
        internal string Label;
        internal ConfigEntry<bool> Flag;
        internal ConfigEntry<string> Pick;
        internal string[] Vals;
        internal string[] Disp;
        internal float W0;
        internal float W;
        internal float H;
        internal float X;
        internal float Y;
        internal float D;
        internal float Hover;
        internal float Lit;
        internal float Flash;
    }

    private sealed class Ring
    {
        internal int First;
        internal int Count;
        internal float R;
        internal float Offset;
    }

    private static readonly List<Slot> Slots = new List<Slot>();
    private static readonly Ring[] Rings = { new Ring(), new Ring() };
    private static readonly Dictionary<int, GUIStyle> Auras = new Dictionary<int, GUIStyle>();

    private static GUIStyle _label, _center, _sub, _hint, _texStyle;
    private static Texture2D _disc, _glow, _halo;
    private static int _rev;
    private static int _version = -1;
    private static int _layKey = -1;
    private static int _textKey = -1;
    private static bool _ru;
    private static bool _held;
    private static bool _warm;
    private static int _page;
    private static int _pages = 1;
    private static int _pageStart;
    private static int _pageCount;
    private static int _ringCount;
    private static int _hit = -1;
    private static string _hintText = "";
    private static string _pageText = "";
    private static float _a;
    private static float _dt;
    private static float _lastAt;

    private static float Frac(float v) => v - Ui.Floor(v);

    private static float OutCubic(float t)
    {
        float b = 1f - Ui.Clamp01(t);
        return 1f - b * b * b;
    }

    private static float OutBack(float t)
    {
        float b = Ui.Clamp01(t) - 1f;
        return 1f + 2.2f * b * b * b + 1.2f * b * b;
    }

    internal static void Draw()
    {
        bool held = !NocturneMenu.Rebinding && NocturneKeys.Held(NocturneConfig.RadialKey);
        if (!held && _a <= 0f)
        {
            _held = false;
            return;
        }

        Event ev = Event.current;
        if (ev == null)
            return;

        Init();
        float tm = Time.unscaledTime;
        if (held && !_held)
        {
            _page = 0;
            _hit = -1;
            if (_a <= 0f)
                _lastAt = tm;
            Refresh(true);
        }
        else
            Refresh(false);

        _held = held;
        bool paint = NocturneStyle.Is(EventType.Repaint);
        if (paint)
        {
            float dt = Ui.Clamp(tm - _lastAt, 0f, 0.034f);
            _lastAt = tm;
            _dt = dt;
            _a = Ui.MoveTowards(_a, held ? 1f : 0f, dt * (held ? OpenSpeed : CloseSpeed));
            if (_a <= 0f)
            {
                ResetSlots();
                return;
            }
        }

        float sw = Screen.width, sh = Screen.height;
        float u = Ui.Clamp(Ui.Min(sw, sh) / 980f, 0.7f, 1.25f);
        Vector2 c = Ui.V(sw * 0.5f, sh * 0.5f);
        Patches.MouseShield.Cover(Ui.R(0f, 0f, sw, sh));
        Layout();
        Texts();
        bool live = held && _a > 0.45f;
        int hovered = live ? Hit(c, u, NocturneStyle.Mouse) : -1;

        if (NocturneStyle.Is(EventType.MouseDown))
        {
            if (live && hovered >= 0)
            {
                Slot s = Slots[hovered];
                if (ev.button == 0)
                {
                    Activate(s);
                    s.Flash = 1f;
                }
                else if (ev.button == 1)
                {
                    NocturneFavs.Toggle(s.Key);
                    NocturneToast.Push(NocturneText.T("Радиал", "Radial"), NocturneText.T("Убрано: ", "Removed: ") + s.Label, 1.4f, NocturneNotifyKind.Info);
                }
            }
            ev.Use();
            return;
        }

        if (NocturneStyle.Is(EventType.ScrollWheel))
        {
            if (live && _pages > 1)
                _page = (_page + (ev.delta.y > 0f ? 1 : _pages - 1)) % _pages;
            if (held)
                ev.Use();
            return;
        }

        if (!paint)
            return;

        Paint(c, u, hovered, tm);
    }

    private static void ResetSlots()
    {
        _hit = -1;
        for (int i = 0; i < Slots.Count; i++)
        {
            Slots[i].Hover = 0f;
            Slots[i].Flash = 0f;
        }
    }

    private static void Refresh(bool force)
    {
        bool ru = NocturneText.IsRussian;
        if (!force && _version == NocturneFavs.Version && _ru == ru)
            return;

        NocturneFavs.Load();
        _version = NocturneFavs.Version;
        _ru = ru;
        Slots.Clear();
        List<string> order = NocturneFavs.Order;
        for (int i = 0; i < order.Count; i++)
        {
            Slot s = Resolve(order[i]);
            if (s != null)
            {
                s.W0 = _label.CalcSize(new GUIContent(s.Label)).x;
                Slots.Add(s);
            }
        }

        _rev++;
    }

    private static Slot Resolve(string key)
    {
        int cut = key.IndexOf('/');
        if (cut <= 0)
            return null;

        string section = key.Substring(0, cut);
        string name = key.Substring(cut + 1);
        SearchFeat f = NocturneSearchIndex.Find(key);
        if (f != null && f.Kind == 3)
        {
            if (f.Vals == null || f.Disp == null || f.Vals.Length != f.Disp.Length || f.Vals.Length < 2)
                return null;
            ConfigEntry<string> pick = NocturneConfig.Find<string>(section, name);
            return pick == null ? null : new Slot { Key = key, Label = f.Label, Pick = pick, Vals = f.Vals, Disp = f.Disp };
        }

        if (f != null && f.Kind != 0)
            return null;

        ConfigEntry<bool> flag = NocturneConfig.Find<bool>(section, name);
        if (flag == null)
            return null;

        QuickItem quick = f == null ? NocturneQuick.ByCfg(flag) : null;
        return new Slot { Key = key, Label = f != null ? f.Label : quick != null ? quick.Label : name, Flag = flag };
    }

    private static bool IsOn(Slot s) => s.Flag != null ? s.Flag.Value : IndexOf(s.Vals, s.Pick.Value) > 0;

    private static int IndexOf(string[] vals, string v)
    {
        for (int i = 0; i < vals.Length; i++)
            if (vals[i] == v)
                return i;
        return 0;
    }

    private static void Activate(Slot s)
    {
        if (s.Flag != null)
            s.Flag.Value = !s.Flag.Value;
        else
            s.Pick.Value = s.Vals[(IndexOf(s.Vals, s.Pick.Value) + 1) % s.Vals.Length];
    }

    private static void Layout()
    {
        int pages = Ui.Max(1, Ui.CeilToInt(Slots.Count / (float)PageCap));
        int page = Ui.Clamp(_page, 0, pages - 1);
        int key = _rev * 64 + page;
        if (key == _layKey && page == _page)
            return;

        _layKey = key;
        _pages = pages;
        _page = page;
        _pageStart = page * PageCap;
        _pageCount = Ui.Max(0, Ui.Min(PageCap, Slots.Count - _pageStart));
        _textKey = -1;
        _hit = -1;
        _ringCount = _pageCount == 0 ? 0 : _pageCount <= 10 ? 1 : 2;
        if (_ringCount == 1)
        {
            Place(Rings[0], _pageStart, _pageCount, 225f, 0f, 0f);
            return;
        }

        if (_ringCount == 2)
        {
            int inner = Ui.Clamp(Ui.RoundToInt(_pageCount * 0.26f), 4, 5);
            int outer = _pageCount - inner;
            Place(Rings[0], _pageStart, inner, 150f, 0f, 0f);
            Place(Rings[1], _pageStart + inner, outer, 320f, 180f / outer, 0.12f);
        }
    }

    private static void Place(Ring ring, int first, int count, float radius, float offset, float delay)
    {
        ring.First = first;
        ring.Count = count;
        ring.R = radius;
        ring.Offset = offset;
        float step = 360f / count;
        for (int i = 0; i < count; i++)
        {
            float a = (-90f + offset + i * step) * Mathf.Deg2Rad;
            Slot s = Slots[first + i];
            s.X = Ui.Cos(a) * radius;
            s.Y = Ui.Sin(a) * radius;
            s.D = delay;
        }

        float baseCap = _ringCount == 1 ? 190f : 150f;
        for (int i = 0; i < count; i++)
        {
            Slot s = Slots[first + i];
            float cap = baseCap;
            if (count >= 2)
            {
                cap = Room(cap, s, Slots[first + (i + 1) % count]);
                cap = Room(cap, s, Slots[first + (i + count - 1) % count]);
            }

            float one = s.W0 + PadL + PadR + 12f;
            bool two = s.Pick != null || one > cap;
            s.W = two ? Ui.Max(Ui.Min(cap, one), 92f) : one;
            s.H = two ? TwoH : OneH;
        }
    }

    private static float Room(float cap, Slot s, Slot n)
    {
        float dx = Ui.Abs(s.X - n.X), dy = Ui.Abs(s.Y - n.Y);
        return dy < TwoH + 6f ? Ui.Min(cap, Ui.Max(dx - 8f, 92f)) : cap;
    }

    private static int Hit(Vector2 c, float u, Vector2 m)
    {
        if (_ringCount == 0)
            return -1;

        float dx = m.x - c.x, dy = m.y - c.y;
        float dist = Ui.Sqrt(dx * dx + dy * dy) / u;
        Ring last = Rings[_ringCount - 1];
        if (dist < HubR + 14f || dist > last.R + 150f)
        {
            _hit = -1;
            return -1;
        }

        Ring ring = _ringCount == 1 || dist < (Rings[0].R + Rings[1].R) * 0.5f ? Rings[0] : Rings[1];
        float step = 360f / ring.Count;
        float ang = Ui.Repeat(Ui.Atan2(dy, dx) * Mathf.Rad2Deg + 90f - ring.Offset, 360f);
        int idx = ring.First + Ui.RoundToInt(ang / step) % ring.Count;
        if (idx != _hit && _hit >= ring.First && _hit < ring.First + ring.Count)
        {
            float cur = (_hit - ring.First) * step;
            if (Ui.Abs(Ui.Repeat(ang - cur + 180f, 360f) - 180f) < step * 0.62f)
                idx = _hit;
        }

        _hit = idx;
        return idx;
    }

    private static void Warm()
    {
        if (_warm)
            return;

        _warm = true;
        Rect off = Ui.R(-9000f, -9000f, 64f, 64f);
        for (int r = 6; r <= 20; r += 2)
        {
            Skin.Glass(off, r);
            Skin.Grad(off, r, 1f);
            if (!Auras.ContainsKey(r))
                Auras[r] = BuildAura(r);
        }
    }

    private static float Smooth(float t)
    {
        t = Ui.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    private static void Paint(Vector2 c, float u, int hovered, float tm)
    {
        Warm();
        NocturnePalette p = NocturneStyle.Current;
        float fade = OutCubic(_a);
        float pulse = 0.5f + 0.5f * Ui.Sin(tm * 3f);
        float k = 1f - Ui.Exp(-_dt * 16f);
        float kf = 1f - Ui.Exp(-_dt * 9f);
        float sw = Screen.width, sh = Screen.height;

        NocturneStyle.Fill(Ui.R(0f, 0f, sw, sh), Ui.C(0f, 0f, 0f, 0.6f * fade));
        Glow(c, 340f * u * (0.8f + 0.2f * fade), Pane.A(p.Accent, 0.09f * fade));

        float outer = _ringCount > 0 ? Rings[_ringCount - 1].R : 225f;
        for (int t = 0; t < 36; t++)
        {
            float a = t * 10f * Mathf.Deg2Rad;
            float tr = (outer + 70f) * u * (0.82f + 0.18f * fade);
            bool major = t % 3 == 0;
            Disc(Ui.V(c.x + Ui.Cos(a) * tr, c.y + Ui.Sin(a) * tr), (major ? 2.1f : 1.3f) * u, Pane.A(major ? p.Accent : Ui.White, (major ? 0.30f : 0.12f) * fade));
        }

        for (int kr = 0; kr < _ringCount; kr++)
            Halo(c, Rings[kr].R * u * (0.62f + 0.38f * OutBack(_a)), Pane.A(Ui.White, 0.10f * fade));

        if (hovered >= 0 && _a > 0.8f)
        {
            Slot hs = Slots[hovered];
            Vector2 tip = Ui.V(c.x + hs.X * u, c.y + hs.Y * u);
            Line(c, tip, Pane.A(p.Accent, 0.38f * fade), 2f * u);
        }

        Hub(c, u, p, hovered, tm, fade, pulse);
        int fs = Ui.RoundToInt(12f * u);
        for (int pass = 0; pass < 2; pass++)
        {
            for (int kr = 0; kr < _ringCount; kr++)
            {
                Ring ring = Rings[kr];
                float rot = (1f - fade) * (kr == 0 ? 16f : -12f) * Mathf.Deg2Rad;
                float rs = Ui.Sin(rot), rc = Ui.Cos(rot);
                for (int i = 0; i < ring.Count; i++)
                {
                    int idx = ring.First + i;
                    if ((idx == hovered) != (pass == 1))
                        continue;

                    Slot s = Slots[idx];
                    s.Hover += ((idx == hovered ? 1f : 0f) - s.Hover) * k;
                    s.Lit += ((IsOn(s) ? 1f : 0f) - s.Lit) * k;
                    s.Flash -= s.Flash * kf;
                    float prog = Ui.Clamp01(_a * (1f + Spread) - s.D);
                    if (prog <= 0f)
                        continue;

                    float x = Ui.Clamp01(prog / 0.8f);
                    float travel = 0.5f * Smooth(x) + 0.5f * (1f - (1f - x) * (1f - x));
                    float gx = (s.X * rc - s.Y * rs) * u * travel;
                    float gy = (s.X * rs + s.Y * rc) * u * travel;
                    Chip(s, c.x + gx, c.y + gy, u, fs, Smooth((prog - 0.25f) / 0.6f), Ui.Clamp01(prog * 4f), Ui.Clamp01((prog - 0.7f) / 0.25f), p);
                }
            }
        }

        Hint(c, u, sh, p, Ui.Clamp01((_a - 0.4f) / 0.6f));
    }

    private static void Chip(Slot s, float cx, float cy, float u, int fs, float e, float alpha, float textAlpha, NocturnePalette p)
    {
        float bump = 1f + 0.06f * s.Hover + 0.12f * s.Flash;
        float bead = 14f * u;
        float w = (bead + (s.W * u - bead) * e) * bump;
        float h = (bead + (s.H * u - bead) * e) * bump;
        Rect r = Ui.R(cx - w * 0.5f, cy - h * 0.5f, w, h);
        int rad = Ui.Clamp(Ui.FloorToInt(h * 0.25f) * 2, 6, 20);
        Color prev = NocturneStyle.Tint;

        float glow = 0.5f * s.Lit + 0.5f * s.Hover + 0.5f * s.Flash;
        if (glow > 0.02f)
            Aura(r, rad, Pane.A(p.Accent, 0.42f * glow * alpha));

        NocturneStyle.Tint = Pane.A(Ui.White, alpha * (1f - s.Lit * 0.9f));
        Skin.Glass(r, rad);
        NocturneStyle.Tint = prev;
        if (s.Lit > 0.01f)
            Skin.Grad(r, rad, s.Lit * alpha);
        if (s.Hover > 0.02f)
            NocturneStyle.StrokeRounded(r, Pane.A(Ui.White, 0.55f * s.Hover * alpha), rad, 1);

        float bx = Ui.Round(cx);
        float by = Ui.Round(cy);
        float fw = s.W * u, fh = s.H * u;
        float left = Ui.Round(bx - fw * 0.5f);
        Disc(Ui.V(left + 13f * u, by), 3.1f * u, s.Lit > 0.02f ? Pane.A(Ui.White, s.Lit * alpha) : Pane.A(p.Muted, 0.55f * alpha));
        if (textAlpha <= 0.01f)
            return;

        float ink = 0.55f * s.Lit + 0.45f * s.Hover;
        _label.fontSize = fs;
        _label.normal.textColor = Pane.A(Ui.Mix(Ui.Mix(p.Text, p.Muted, 0.18f), Ui.White, ink), textAlpha);
        float tx = left + Ui.Round(PadL * u);
        float tw = Ui.Round(fw - (PadL + PadR) * u);
        float top = Ui.Round(by - fh * 0.5f);
        if (s.Pick == null)
        {
            GUI.Label(Ui.R(tx, top, tw, Ui.Round(fh)), s.Label, _label);
            return;
        }

        GUI.Label(Ui.R(tx, top + Ui.Round(3f * u), tw, Ui.Round(fh * 0.52f)), s.Label, _label);
        _sub.fontSize = Ui.RoundToInt(10f * u);
        _sub.normal.textColor = Pane.A(Ui.Mix(p.Accent, Ui.White, 0.25f * s.Lit), textAlpha);
        GUI.Label(Ui.R(tx, top + Ui.Round(fh * 0.5f), tw, Ui.Round(fh * 0.42f)), s.Disp[IndexOf(s.Vals, s.Pick.Value)], _sub);
    }

    private static void Hub(Vector2 c, float u, NocturnePalette p, int hovered, float tm, float fade, float pulse)
    {
        float hub = HubR * u * (0.55f + 0.45f * OutBack(Ui.Clamp01(_a * 1.5f)));
        float ha = Ui.Clamp01(_a * 2.2f);
        float rp = Frac(tm * 0.5f);
        Halo(c, hub + (8f + rp * 36f) * u, Pane.A(p.Accent, (1f - rp) * 0.36f * ha));
        Glow(c, hub + 30f * u, Pane.A(p.Accent, (0.12f + 0.08f * pulse) * ha));
        Disc(c, hub + 2.5f * u, Pane.A(Ui.Mix(p.Accent, p.Accent2, 0.5f), 0.65f * ha));
        Disc(c, hub, Pane.A(Ui.Mix(p.Window, Ui.Black, 0.45f), 0.98f * ha));
        if (_a < 0.6f)
            return;

        float ta = Ui.Clamp01((_a - 0.6f) / 0.3f);
        if (hovered >= 0)
        {
            Slot s = Slots[hovered];
            bool on = IsOn(s);
            _center.fontSize = Ui.RoundToInt(14f * u);
            _center.normal.textColor = Pane.A(Ui.White, ta);
            GUI.Label(Ui.R(Ui.Round(c.x - HubR * u * 0.84f), Ui.Round(c.y - 36f * u), Ui.Round(HubR * u * 1.68f), Ui.Round(40f * u)), s.Label, _center);
            var pill = Ui.R(Ui.Round(c.x - 29f * u), Ui.Round(c.y + 10f * u), Ui.Round(58f * u), Ui.Round(22f * u));
            int pr = Ui.RoundToInt(11f * u);
            Color prev = NocturneStyle.Tint;
            if (on)
                Skin.Grad(pill, pr, ta);
            else
            {
                NocturneStyle.Tint = Pane.A(Ui.White, ta);
                Skin.Glass(pill, pr);
                NocturneStyle.Tint = prev;
            }

            _sub.fontSize = Ui.RoundToInt(11f * u);
            _sub.normal.textColor = Pane.A(on ? Ui.White : p.Muted, ta);
            GUI.Label(pill, s.Flag != null ? (on ? NocturneText.T("ВКЛ", "ON") : NocturneText.T("ВЫКЛ", "OFF")) : NocturneText.T("ДАЛЕЕ", "NEXT"), _sub);
            return;
        }

        float gem = 10f * u;
        Matrix4x4 prevM = GUI.matrix;
        GUIUtility.RotateAroundPivot(45f, Ui.V(c.x, c.y - 14f * u));
        var gr = Ui.R(c.x - gem, c.y - 14f * u - gem, gem * 2f, gem * 2f);
        NocturneStyle.FillRounded(gr, Pane.A(p.Accent, 0.95f * ta), 3);
        NocturneStyle.StrokeRounded(gr, Pane.A(Ui.White, 0.28f * ta), 3, 1);
        GUI.matrix = prevM;

        _center.fontSize = Ui.RoundToInt(13f * u);
        _center.normal.textColor = Pane.A(Ui.White, ta);
        GUI.Label(Ui.R(Ui.Round(c.x - HubR * u), Ui.Round(c.y + 8f * u), Ui.Round(HubR * u * 2f), Ui.Round(22f * u)), "NOCTURNE", _center);
        if (_pages > 1)
        {
            _sub.fontSize = Ui.RoundToInt(11f * u);
            _sub.normal.textColor = Pane.A(p.Muted, ta);
            GUI.Label(Ui.R(Ui.Round(c.x - HubR * u), Ui.Round(c.y + 28f * u), Ui.Round(HubR * u * 2f), Ui.Round(16f * u)), _pageText, _sub);
        }
    }

    private static void Texts()
    {
        int key = _pages * 100 + _page + (_ru ? 10000 : 0) + (_pageCount == 0 ? 50000 : 0);
        if (key == _textKey)
            return;

        _textKey = key;
        _pageText = (_page + 1) + " / " + _pages;
        string wheel = _pages > 1 ? NocturneText.T("   ·   колесо — страница ", "   ·   wheel — page ") + _pageText : "";
        _hintText = _pageCount == 0
            ? NocturneText.T("Радиал пуст. Добавь фичи в избранное: ★ слева от строки в меню или ПКМ по строке.", "The radial is empty. Favorite features: ★ left of a row in the menu, or right-click a row.")
            : NocturneText.T("ЛКМ — вкл/выкл   ·   ПКМ — убрать из радиала", "LMB — toggle   ·   RMB — remove from radial") + wheel;
    }

    private static void Hint(Vector2 c, float u, float sh, NocturnePalette p, float fade)
    {
        if (fade <= 0.01f)
            return;

        float w = (_pageCount == 0 ? 660f : _pages > 1 ? 620f : 440f) * u;
        var r = Ui.R(Ui.Round(c.x - w * 0.5f), Ui.Round(sh - 76f * u + (1f - fade) * 10f * u), Ui.Round(w), Ui.Round(34f * u));
        Color prev = NocturneStyle.Tint;
        NocturneStyle.Tint = Pane.A(Ui.White, fade);
        Skin.Glass(r, Ui.RoundToInt(17f * u));
        NocturneStyle.Tint = prev;
        _hint.fontSize = Ui.RoundToInt(13f * u);
        _hint.normal.textColor = Pane.A(Ui.Mix(p.Text, p.Muted, 0.35f), fade);
        GUI.Label(r, _hintText, _hint);
    }

    private static void Aura(Rect r, int rad, Color col)
    {
        if (col.a <= 0.004f)
            return;
        if (!Auras.TryGetValue(rad, out GUIStyle st) || st == null)
        {
            st = BuildAura(rad);
            Auras[rad] = st;
        }

        Color prev = NocturneStyle.Tint;
        NocturneStyle.Tint = Ui.C(col.r, col.g, col.b, col.a * prev.a);
        GUI.Box(Ui.R(r.m_XMin - Reach, r.m_YMin - Reach, r.m_Width + Reach * 2f, r.m_Height + Reach * 2f), NocturneStyle.Blank, st);
        NocturneStyle.Tint = prev;
    }

    private static GUIStyle BuildAura(int rad)
    {
        int edge = rad + Reach;
        int n = edge * 2 + 2;
        float inner = n - 2f * Reach;
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float d = Dist(x + 0.5f - Reach, y + 0.5f - Reach, inner, inner, rad);
                if (d <= -0.5f)
                    continue;

                float fall = 1f - Ui.Clamp01(d / Reach);
                float a = Ui.Clamp01(d + 0.5f) * fall * fall;
                px[y * n + x] = PlayerGfx.Px(255, 255, 255, (byte)(a * 255f + 0.5f));
            }
        }

        var st = new GUIStyle();
        st.normal.background = PlayerGfx.Upload(n, n, px);
        st.border = NocturneStyle.Offset(edge, edge, edge, edge);
        return st;
    }

    private static float Dist(float x, float y, float w, float h, float rad)
    {
        float qx = Ui.Abs(x - w * 0.5f) - (w * 0.5f - rad);
        float qy = Ui.Abs(y - h * 0.5f) - (h * 0.5f - rad);
        float ox = Ui.Max(qx, 0f), oy = Ui.Max(qy, 0f);
        return Ui.Sqrt(ox * ox + oy * oy) + Ui.Min(Ui.Max(qx, qy), 0f) - rad;
    }

    private static void Tex(Texture2D t, Vector2 c, float r, Color col)
    {
        if (t == null || col.a <= 0.003f)
            return;
        if (_texStyle == null)
            _texStyle = new GUIStyle();
        _texStyle.normal.background = t;
        Color prev = NocturneStyle.Tint;
        NocturneStyle.Tint = Ui.C(col.r, col.g, col.b, col.a * prev.a);
        GUI.Box(Ui.R(c.x - r, c.y - r, r * 2f, r * 2f), NocturneStyle.Blank, _texStyle);
        NocturneStyle.Tint = prev;
    }

    private static void Disc(Vector2 c, float r, Color col) => Tex(_disc, c, r, col);

    private static void Glow(Vector2 c, float r, Color col) => Tex(_glow, c, r, col);

    private static void Halo(Vector2 c, float r, Color col) => Tex(_halo, c, r, col);

    private static void Line(Vector2 a, Vector2 b, Color col, float thick)
    {
        float dx = b.x - a.x, dy = b.y - a.y;
        float len = Ui.Sqrt(dx * dx + dy * dy);
        if (len < 0.01f)
            return;
        Matrix4x4 prev = GUI.matrix;
        GUIUtility.RotateAroundPivot(Ui.Atan2(dy, dx) * Mathf.Rad2Deg, a);
        NocturneStyle.Fill(Ui.R(a.x, a.y - thick * 0.5f, len, thick), col);
        GUI.matrix = prev;
    }

    private static Texture2D BuildRing(int size, float r0, float r1)
    {
        var px = new Color32[size * size];
        float cc = (size - 1) * 0.5f;
        float ts = (cc - 1f) / r1;
        float inner = r0 * ts, edge = r1 * ts;
        for (int y = 0; y < size; y++)
        {
            float dy = y - cc;
            for (int x = 0; x < size; x++)
            {
                float dx = x - cc;
                float rr = Ui.Sqrt(dx * dx + dy * dy);
                float a = Ui.Clamp01(Ui.Min(rr - inner, edge - rr) + 0.5f);
                px[y * size + x] = PlayerGfx.Px(255, 255, 255, (byte)(a * 255f + 0.5f));
            }
        }

        return PlayerGfx.Upload(size, size, px);
    }

    private static void Init()
    {
        if (_disc == null)
            _disc = NocturneStyle.BuildDisc(128);
        if (_glow == null)
            _glow = NocturneStyle.BuildGlow(128);
        if (_halo == null)
            _halo = BuildRing(512, 298.2f, 300f);
        if (_label != null)
            return;

        _label = new GUIStyle { alignment = TextAnchor.MiddleCenter, wordWrap = true, fontSize = 12, fontStyle = FontStyle.Bold, clipping = TextClipping.Clip };
        _label.normal.textColor = Ui.White;
        _center = new GUIStyle { alignment = TextAnchor.MiddleCenter, wordWrap = true, fontSize = 14, fontStyle = FontStyle.Bold };
        _center.normal.textColor = Ui.White;
        _sub = new GUIStyle { alignment = TextAnchor.MiddleCenter, fontSize = 11, fontStyle = FontStyle.Bold };
        _sub.normal.textColor = Ui.White;
        _hint = new GUIStyle { alignment = TextAnchor.MiddleCenter, fontSize = 13 };
        _hint.normal.textColor = Ui.White;
    }
}
