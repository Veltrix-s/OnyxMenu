using System.Collections.Generic;
using UnityEngine;

namespace Nocturne;

internal static class Skin
{
    private const int LeftW = 320;
    private const int TopH = 92;
    private const int BotH = 230;
    private const int Edge = 16;

    private const int KGlass = 0;
    private const int KLit = 1;

    private const int Reach = 40;
    private const int Bend = 18;
    private const int KnobPad = 4;

    private static readonly Dictionary<int, GUIStyle> Pills = new Dictionary<int, GUIStyle>();
    private static readonly Dictionary<int, GUIStyle> Grads = new Dictionary<int, GUIStyle>();
    private static readonly Dictionary<int, GUIStyle> Knobs = new Dictionary<int, GUIStyle>();
    private static readonly Dictionary<int, GUIStyle[]> Plates = new Dictionary<int, GUIStyle[]>();
    private static readonly Dictionary<int, Color> Mates = new Dictionary<int, Color>();
    private static readonly List<Texture2D> GradTex = new List<Texture2D>();
    private static GUIStyle _scrim;
    private static GUIStyle _drop;
    private static GUIStyle _hair;
    private static int _gradKey = -1;
    private static bool _shadow;

    internal static bool Shadow => _shadow;

    internal static void Begin(bool lite) => _shadow = !lite && NocturneMenuBg.Active && NocturneConfig.MenuBgAlpha.Value >= 0.45f;

    internal static void Glass(Rect r, int radius)
    {
        if (!NocturneStyle.Painting)
            return;
        if (NocturneStyle.Lite)
        {
            NocturneStyle.Fill(r, Ui.C(0f, 0f, 0f, 0.35f));
            return;
        }
        Put(r, Pill(KGlass, radius), Ui.White);
    }

    internal static void Lit(Rect r, int radius)
    {
        if (!NocturneStyle.Painting)
            return;
        if (NocturneStyle.Lite)
        {
            NocturneStyle.Fill(r, Ui.C(1f, 1f, 1f, 0.12f));
            return;
        }
        Put(r, Pill(KLit, radius), Ui.White);
    }

    internal static void Grad(Rect r, int radius, float a = 1f)
    {
        if (!NocturneStyle.Painting)
            return;

        NocturnePalette p = NocturneStyle.Current;
        if (NocturneStyle.Lite)
        {
            NocturneStyle.FillRounded(r, Ui.C(p.Accent.r, p.Accent.g, p.Accent.b, a), radius);
            return;
        }

        int key = (Key(p.Accent) * 397) ^ Key(p.Accent2);
        if (key != _gradKey)
        {
            for (int i = 0; i < GradTex.Count; i++)
                UnityEngine.Object.Destroy(GradTex[i]);
            GradTex.Clear();
            Grads.Clear();
            _gradKey = key;
        }

        radius = Ui.Clamp(radius, 2, 20);
        if (!Grads.TryGetValue(radius, out GUIStyle st) || st == null)
        {
            st = BuildGrad(radius, p.Accent, p.Accent2);
            Grads[radius] = st;
        }
        Put(r, st, Ui.C(1f, 1f, 1f, a));
    }

    internal static void Plate(Rect r, int radius, Color col, bool hot)
    {
        if (!NocturneStyle.Painting)
            return;
        if (NocturneStyle.Lite)
        {
            NocturneStyle.FillRounded(r, Ui.C(col.r, col.g, col.b, hot ? 0.4f : 0.22f), radius);
            return;
        }

        radius = Ui.Clamp(radius, 2, 20);
        if (!Plates.TryGetValue(radius, out GUIStyle[] set))
        {
            set = new[] { BuildPlate(radius), BuildRim(radius, false), BuildRim(radius, true) };
            Plates[radius] = set;
        }

        int key = Key(col);
        if (!Mates.TryGetValue(key, out Color mate))
        {
            if (Mates.Count > 96)
                Mates.Clear();
            mate = NocturneStyle.Shift(col);
            Mates[key] = mate;
        }

        Put(r, set[0], Ui.White);
        Put(r, set[1], Ui.C(col.r, col.g, col.b, 1f));
        Put(r, set[2], Ui.C(mate.r, mate.g, mate.b, 1f));
        if (hot)
        {
            Put(r, set[1], Ui.C(col.r, col.g, col.b, 0.55f));
            Put(r, set[2], Ui.C(mate.r, mate.g, mate.b, 0.55f));
        }
    }

    internal static void Knob(float x, float y, float d, float a)
    {
        if (!NocturneStyle.Painting)
            return;
        if (NocturneStyle.Lite)
        {
            NocturneStyle.FillRounded(Ui.R(x, y, d, d), Ui.C(1f, 1f, 1f, a), Ui.RoundToInt(d * 0.5f));
            return;
        }

        int di = Ui.Max(4, Ui.RoundToInt(d));
        if (!Knobs.TryGetValue(di, out GUIStyle st) || st == null)
        {
            st = BuildKnob(di);
            Knobs[di] = st;
        }
        Put(Ui.R(x - KnobPad, y - KnobPad, di + KnobPad * 2f, di + KnobPad * 2f), st, Ui.C(1f, 1f, 1f, a));
    }

    internal static void Hair(float x, float y, float w)
    {
        if (!NocturneStyle.Painting || NocturneStyle.Lite)
            return;
        if (_hair == null)
        {
            _hair = new GUIStyle();
            _hair.normal.background = PlayerGfx.Upload(1, 1, new[] { PlayerGfx.Px(255, 255, 255, 20) });
        }
        Put(Ui.R(x + 4f, y, w - 8f, 1f), _hair, Ui.White);
    }

    internal static void Scrim(Rect r, float k)
    {
        if (!NocturneStyle.Painting || NocturneStyle.Lite)
            return;
        if (r.m_Width < LeftW + Edge + 40f || r.m_Height < TopH + BotH + 40f)
            return;
        if (_scrim == null)
            _scrim = BuildScrim();
        Put(r, _scrim, Ui.C(1f, 1f, 1f, k));
    }

    internal static float ScrimAt(float x, float y, float w, float h)
    {
        if (w < LeftW + Edge + 40f || h < TopH + BotH + 40f)
            return 0f;

        float left = 0f;
        if (x < 150f)
            left = Ui.Lerp(0.82f, 0.68f, Ui.Clamp01(x / 150f));
        else if (x < LeftW)
            left = 0.68f * (1f - Smooth((x - 150f) / (LeftW - 150f)));

        float top = y < TopH ? 0.58f * (1f - Smooth(y / TopH)) : 0f;
        float bot = h - y < BotH ? 0.66f * (1f - Smooth((h - y) / BotH)) : 0f;
        return 1f - (1f - left) * (1f - top) * (1f - bot);
    }

    internal static void Glow(Rect r, Color c, float grow)
    {
        if (!NocturneStyle.Painting || NocturneStyle.Lite)
            return;
        if (_drop == null)
            _drop = BuildDrop();
        float g = Reach + grow;
        Put(Ui.R(r.m_XMin - g, r.m_YMin - g, r.m_Width + g * 2f, r.m_Height + g * 2f), _drop, c);
    }

    internal static void Drop(Rect r, float k)
    {
        if (!NocturneStyle.Painting || NocturneStyle.Lite)
            return;
        if (_drop == null)
            _drop = BuildDrop();
        Put(Ui.R(r.m_XMin - Reach, r.m_YMin - Reach + 12f, r.m_Width + Reach * 2f, r.m_Height + Reach * 2f), _drop, Ui.C(0f, 0f, 0f, k));
    }

    private static void Put(Rect r, GUIStyle st, Color c)
    {
        if (NocturneStyle.Off(r))
            return;
        Color prev = NocturneStyle.Tint;
        NocturneStyle.Use(Ui.C(c.r, c.g, c.b, c.a * prev.a));
        GUI.Box(r, NocturneStyle.Blank, st);
        NocturneStyle.Done(prev);
    }

    private static int Key(Color c) =>
        (Ui.RoundToInt(c.r * 255f) << 16) | (Ui.RoundToInt(c.g * 255f) << 8) | Ui.RoundToInt(c.b * 255f);

    private static float Sd(float x, float y, float w, float h, float rad)
    {
        float qx = Ui.Abs(x - w * 0.5f) - (w * 0.5f - rad);
        float qy = Ui.Abs(y - h * 0.5f) - (h * 0.5f - rad);
        float ox = Ui.Max(qx, 0f);
        float oy = Ui.Max(qy, 0f);
        return Ui.Sqrt(ox * ox + oy * oy) + Ui.Min(Ui.Max(qx, qy), 0f) - rad;
    }

    private static float Smooth(float t)
    {
        t = Ui.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    private static byte B(float v) => (byte)(Ui.Clamp01(v) * 255f + 0.5f);

    private static GUIStyle Pill(int kind, int radius)
    {
        radius = Ui.Clamp(radius, 2, 20);
        int key = kind * 32 + radius;
        if (Pills.TryGetValue(key, out GUIStyle s) && s != null)
            return s;

        int n = radius * 2 + 2;
        float fa = kind == KGlass ? 0.60f : 0.12f;
        float ea = kind == KGlass ? 0.16f : 0.26f;
        float fr = kind == KGlass ? 0.04f : 1f;
        float fb = kind == KGlass ? 0.065f : 1f;

        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float sd = Sd(x + 0.5f, y + 0.5f, n, n, radius);
                float co = Ui.Clamp01(0.5f - sd);
                float ci = Ui.Clamp01(-0.5f - sd);
                float af = co * fa;
                float ae = (co - ci) * ea;
                float at = ae + af * (1f - ae);
                if (at <= 0.001f)
                    continue;

                float k = af * (1f - ae);
                byte c = B((ae + fr * k) / at);
                px[y * n + x] = PlayerGfx.Px(c, c, B((ae + fb * k) / at), B(at));
            }
        }

        s = new GUIStyle();
        s.normal.background = PlayerGfx.Upload(n, n, px);
        s.border = NocturneStyle.Offset(radius, radius, radius, radius);
        Pills[key] = s;
        return s;
    }

    private static GUIStyle BuildGrad(int radius, Color c1, Color c2)
    {
        int h = radius * 2 + 2;
        int w = radius * 2 + 32;
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float co = Ui.Clamp01(0.5f - Sd(x + 0.5f, y + 0.5f, w, h, radius));
                if (co <= 0.001f)
                    continue;

                float u = Ui.Clamp01((x + 0.5f - radius) / (w - 2f * radius));
                px[y * w + x] = PlayerGfx.Px(B(Ui.Lerp(c1.r, c2.r, u)), B(Ui.Lerp(c1.g, c2.g, u)), B(Ui.Lerp(c1.b, c2.b, u)), B(co));
            }
        }

        Texture2D tex = PlayerGfx.Upload(w, h, px);
        GradTex.Add(tex);
        var s = new GUIStyle();
        s.normal.background = tex;
        s.border = NocturneStyle.Offset(radius, radius, radius, radius);
        return s;
    }

    private static GUIStyle BuildPlate(int radius)
    {
        int h = radius * 2 + 2;
        int w = radius * 2 + 32;
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
        {
            float t = 1f - (y + 0.5f) / h;
            float s = Ui.Clamp01(1f - t * 2.2f) * 0.10f;
            float a = s + 0.60f * (1f - s);
            float k = 0.60f * (1f - s) / a;
            byte c = B(s / a + 0.04f * k);
            byte b = B(s / a + 0.065f * k);
            for (int x = 0; x < w; x++)
            {
                float co = Ui.Clamp01(0.5f - Sd(x + 0.5f, y + 0.5f, w, h, radius));
                if (co > 0.001f)
                    px[y * w + x] = PlayerGfx.Px(c, c, b, B(a * co));
            }
        }

        var st = new GUIStyle();
        st.normal.background = PlayerGfx.Upload(w, h, px);
        st.border = NocturneStyle.Offset(radius, radius, radius, radius);
        return st;
    }

    private static GUIStyle BuildRim(int radius, bool right)
    {
        int h = radius * 2 + 2;
        int w = radius * 2 + 32;
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
        {
            float t = 1f - (y + 0.5f) / h;
            float lit = Ui.Lerp(0.55f, 1f, t) * 0.95f;
            float wash = Ui.Lerp(0.28f, 0.52f, t) * (right ? 0.8f : 1f);
            for (int x = 0; x < w; x++)
            {
                float sd = Sd(x + 0.5f, y + 0.5f, w, h, radius);
                float co = Ui.Clamp01(0.5f - sd);
                if (co <= 0.001f)
                    continue;

                float u = Ui.Clamp01((x + 0.5f - radius) / (w - 2f * radius));
                float ring = Ui.Clamp01(1.4f + sd) * lit;
                float a = (ring + wash * (1f - ring)) * (right ? u * 0.9f : 1f - u) * co;
                px[y * w + x] = PlayerGfx.Px(255, 255, 255, B(a));
            }
        }

        var st = new GUIStyle();
        st.normal.background = PlayerGfx.Upload(w, h, px);
        st.border = NocturneStyle.Offset(radius, radius, radius, radius);
        return st;
    }

    private static GUIStyle BuildKnob(int d)
    {
        int n = d + KnobPad * 2;
        float c = n * 0.5f;
        float r = d * 0.5f;
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float dx = x + 0.5f - c;
                float dy = y + 0.5f - c;
                float ad = Ui.Clamp01(r - Ui.Sqrt(dx * dx + dy * dy) + 0.5f);
                float sy = dy + 1.6f;
                float ds = Ui.Sqrt(dx * dx + sy * sy);
                float sa = 0.34f * Ui.Clamp01((r + 2f - ds) / 3.2f);
                float at = ad + sa * (1f - ad);
                if (at <= 0.001f)
                    continue;

                byte g = B(ad / at);
                px[y * n + x] = PlayerGfx.Px(g, g, g, B(at));
            }
        }

        var s = new GUIStyle();
        s.normal.background = PlayerGfx.Upload(n, n, px);
        return s;
    }

    private static GUIStyle BuildDrop()
    {
        int n = (Reach + Bend) * 2 + 2;
        int core = n - Reach * 2;
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float d = Sd(x + 0.5f - Reach, y + 0.5f - Reach, core, core, Bend);
                float t = Smooth(1f - Ui.Clamp01(d / Reach));
                px[y * n + x] = PlayerGfx.Px(255, 255, 255, B(t * Ui.Sqrt(t)));
            }
        }

        var s = new GUIStyle();
        s.normal.background = PlayerGfx.Upload(n, n, px);
        s.border = NocturneStyle.Offset(Reach + Bend, Reach + Bend, Reach + Bend, Reach + Bend);
        return s;
    }

    private static GUIStyle BuildScrim()
    {
        int w = LeftW + 4 + Edge;
        int h = TopH + 4 + BotH;
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
        {
            int down = h - 1 - y;
            float top = down < TopH ? 0.58f * (1f - Smooth(down / (float)TopH)) : 0f;
            float bot = y < BotH ? 0.66f * (1f - Smooth(y / (float)BotH)) : 0f;

            for (int x = 0; x < w; x++)
            {
                float left = 0f;
                if (x < 150)
                    left = Ui.Lerp(0.82f, 0.68f, x / 150f);
                else if (x < LeftW)
                    left = 0.68f * (1f - Smooth((x - 150) / (float)(LeftW - 150)));

                float a = 1f - (1f - left) * (1f - top) * (1f - bot);
                a *= Ui.Clamp01(0.5f - Sd(x + 0.5f, y + 0.5f, w, h, Edge));
                px[y * w + x] = PlayerGfx.Px(6, 6, 10, B(a));
            }
        }

        var s = new GUIStyle();
        s.normal.background = PlayerGfx.Upload(w, h, px);
        s.border = NocturneStyle.Offset(LeftW, Edge, TopH, BotH);
        return s;
    }
}
