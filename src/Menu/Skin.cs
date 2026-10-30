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
    private const int HaloReach = 10;
    private const int Bend = 18;
    private const int KnobPad = 4;

    private static readonly Dictionary<int, GUIStyle> Pills = new Dictionary<int, GUIStyle>();
    private static readonly Dictionary<int, GUIStyle> Grads = new Dictionary<int, GUIStyle>();
    private static readonly Dictionary<int, GUIStyle> Knobs = new Dictionary<int, GUIStyle>();
    private static readonly Dictionary<int, GUIStyle[]> Plates = new Dictionary<int, GUIStyle[]>();
    private static readonly Dictionary<int, GUIStyle> Baked = new Dictionary<int, GUIStyle>();
    private static readonly List<Texture2D> BakedTex = new List<Texture2D>();
    private static readonly Dictionary<int, Color> Mates = new Dictionary<int, Color>();
    private static readonly Dictionary<int, GUIStyle> Frames = new Dictionary<int, GUIStyle>();
    private static readonly List<Texture2D> GradTex = new List<Texture2D>();
    private static readonly Dictionary<int, GUIStyle> Halos = new Dictionary<int, GUIStyle>();
    private static GUIStyle _orb;
    private static int _orbKey = -1;
    private static GUIStyle _scrim;
    private static GUIStyle _drop;
    private static GUIStyle _hair;
    private static GUIStyle _grip;
    private static GUIStyle _switchOff, _switchOn;
    private static int _switchKey;
    private static int _gradKey = -1;
    private static int _bakePass = -1;
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

    internal static void Orb(Rect r)
    {
        if (!NocturneStyle.Painting)
            return;

        NocturnePalette p = NocturneStyle.Current;
        if (NocturneStyle.Lite)
        {
            NocturneStyle.FillRounded(r, p.Accent, Ui.RoundToInt(r.m_Width * 0.5f));
            return;
        }

        int key = (Key(p.Accent) * 397) ^ Key(p.Accent2);
        if (_orb == null || key != _orbKey)
        {
            if (_orb != null)
                UnityEngine.Object.Destroy(_orb.normal.background);
            _orb = BakeOrb(p.Accent, p.Accent2);
            _orbKey = key;
        }
        Put(r, _orb, Ui.White);
    }

    internal static void Frame(Rect r, int radius)
    {
        if (!NocturneStyle.Painting)
            return;
        if (NocturneStyle.Lite)
        {
            NocturneStyle.FillRounded(r, Ui.C(0.07f, 0.07f, 0.08f, 0.96f), radius);
            NocturneStyle.StrokeRounded(r, Ui.C(1f, 1f, 1f, 0.12f), radius, 1);
            return;
        }

        radius = Ui.Clamp(radius, 2, 20);
        int h = Ui.RoundToInt(r.m_Height);
        int key = (h << 5) | radius;
        if (!Frames.TryGetValue(key, out GUIStyle st) || st == null)
        {
            st = BakeFrame(h, radius);
            Frames[key] = st;
        }
        Put(r, st, Ui.White);
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
        if (NocturneStyle.Off(r))
            return;

        radius = Ui.Clamp(radius, 2, 20);
        int key = Key(col);
        if (!Mates.TryGetValue(key, out Color mate))
        {
            if (Mates.Count > 96)
                Mates.Clear();
            mate = NocturneStyle.Shift(col);
            Mates[key] = mate;
        }

        int bk = (key << 6) | (radius << 1) | (hot ? 1 : 0);
        if (!Baked.TryGetValue(bk, out GUIStyle st) && _bakePass != NocturneStyle.Pass)
        {
            _bakePass = NocturneStyle.Pass;
            if (Baked.Count >= 96)
            {
                for (int i = 0; i < BakedTex.Count; i++)
                    UnityEngine.Object.Destroy(BakedTex[i]);
                BakedTex.Clear();
                Baked.Clear();
            }
            st = BakePlate(radius, col, mate, hot);
            Baked[bk] = st;
        }
        if (st != null)
        {
            Put(r, st, Ui.White);
            return;
        }

        if (!Plates.TryGetValue(radius, out GUIStyle[] set))
        {
            set = new[] { BuildPlate(radius), BuildRim(radius, false), BuildRim(radius, true) };
            Plates[radius] = set;
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

    internal static bool Switch(Rect track, bool on)
    {
        if (!NocturneStyle.Painting || NocturneStyle.Lite || track.m_Width != 46f || track.m_Height != 24f)
            return false;

        GUIStyle st;
        if (on)
        {
            NocturnePalette p = NocturneStyle.Current;
            int key = (Key(p.Accent) * 397) ^ Key(p.Accent2);
            if (_switchOn == null || key != _switchKey)
            {
                if (_switchOn != null)
                    UnityEngine.Object.Destroy(_switchOn.normal.background);
                _switchOn = BakeSwitch(true, p.Accent, p.Accent2);
                _switchKey = key;
            }
            st = _switchOn;
        }
        else
            st = _switchOff ??= BakeSwitch(false, Ui.White, Ui.White);

        Put(track, st, Ui.White);
        return true;
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

    internal static void Grip(float right, float bottom, Color c)
    {
        if (!NocturneStyle.Painting)
            return;
        if (_grip == null)
        {
            var px = new Color32[100];
            for (int gi = 0; gi < 3; gi++)
                for (int gj = 0; gj <= gi; gj++)
                    for (int k = 0; k < 4; k++)
                        px[(gi * 4 + (k >> 1)) * 10 + 8 - gj * 4 + (k & 1)] = PlayerGfx.Px(255, 255, 255, 255);
            Texture2D t = PlayerGfx.Upload(10, 10, px);
            t.filterMode = FilterMode.Point;
            _grip = new GUIStyle();
            _grip.normal.background = t;
        }
        Put(Ui.R(right - 16f, bottom - 16f, 10f, 10f), _grip, c);
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

    internal static void Halo(Rect r, int radius, Color c)
    {
        if (!NocturneStyle.Painting || NocturneStyle.Lite)
            return;

        radius = Ui.Clamp(radius, 2, 20);
        if (!Halos.TryGetValue(radius, out GUIStyle st) || st == null)
        {
            st = BakeHalo(radius);
            Halos[radius] = st;
        }
        Put(Ui.R(r.m_XMin - HaloReach, r.m_YMin - HaloReach, r.m_Width + HaloReach * 2f, r.m_Height + HaloReach * 2f), st, c);
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
        if (c.a * prev.a < 0.004f)
            return;
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

    private static GUIStyle BakePlate(int radius, Color col, Color mate, bool hot)
    {
        int h = radius * 2 + 2;
        int w = radius * 2 + 32;
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
        {
            float t = 1f - (y + 0.5f) / h;
            float s = Ui.Clamp01(1f - t * 2.2f) * 0.10f;
            float fa = s + 0.60f * (1f - s);
            float fk = 0.60f * (1f - s) / fa;
            float gc = B(s / fa + 0.04f * fk) / 255f;
            Color glass = Ui.C(gc, gc, B(s / fa + 0.065f * fk) / 255f, 1f);
            float lit = Ui.Lerp(0.55f, 1f, t) * 0.95f;
            float wash = Ui.Lerp(0.28f, 0.52f, t);
            for (int x = 0; x < w; x++)
            {
                float sd = Sd(x + 0.5f, y + 0.5f, w, h, radius);
                float co = Ui.Clamp01(0.5f - sd);
                if (co <= 0.001f)
                    continue;

                float u = Ui.Clamp01((x + 0.5f - radius) / (w - 2f * radius));
                float ring = Ui.Clamp01(1.4f + sd) * lit;
                float left = B((ring + wash * (1f - ring)) * (1f - u) * co) / 255f;
                float right = B((ring + wash * 0.8f * (1f - ring)) * u * 0.9f * co) / 255f;

                float r = 0f, g = 0f, b = 0f, a = 0f;
                Over(ref r, ref g, ref b, ref a, glass, B(fa * co) / 255f);
                Over(ref r, ref g, ref b, ref a, col, left);
                Over(ref r, ref g, ref b, ref a, mate, right);
                if (hot)
                {
                    Over(ref r, ref g, ref b, ref a, col, left * 0.55f);
                    Over(ref r, ref g, ref b, ref a, mate, right * 0.55f);
                }
                if (a > 0f)
                    px[y * w + x] = PlayerGfx.Px(B(r / a), B(g / a), B(b / a), B(a));
            }
        }

        Texture2D tex = PlayerGfx.Upload(w, h, px);
        BakedTex.Add(tex);
        var st = new GUIStyle();
        st.normal.background = tex;
        st.border = NocturneStyle.Offset(radius, radius, radius, radius);
        return st;
    }

    private static GUIStyle BakeHalo(int radius)
    {
        int n = (radius + HaloReach) * 2 + 2;
        int core = n - HaloReach * 2;
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float d = Sd(x + 0.5f - HaloReach, y + 0.5f - HaloReach, core, core, radius);
                float t = Smooth(1f - Ui.Clamp01(d / HaloReach));
                px[y * n + x] = PlayerGfx.Px(255, 255, 255, B(t * t));
            }
        }

        var s = new GUIStyle();
        s.normal.background = PlayerGfx.Upload(n, n, px);
        int b = radius + HaloReach;
        s.border = NocturneStyle.Offset(b, b, b, b);
        return s;
    }

    private static GUIStyle BakeOrb(Color c1, Color c2)
    {
        const int N = 32;
        var px = new Color32[N * N];
        for (int y = 0; y < N; y++)
        {
            for (int x = 0; x < N; x++)
            {
                float dx = x + 0.5f - N * 0.5f;
                float dy = y + 0.5f - N * 0.5f;
                float co = Ui.Clamp01(N * 0.5f - Ui.Sqrt(dx * dx + dy * dy) + 0.5f);
                if (co <= 0.001f)
                    continue;

                float u = Ui.Clamp01((dx - dy + N) / (2f * N));
                px[y * N + x] = PlayerGfx.Px(B(Ui.Lerp(c1.r, c2.r, u)), B(Ui.Lerp(c1.g, c2.g, u)), B(Ui.Lerp(c1.b, c2.b, u)), B(co));
            }
        }

        var s = new GUIStyle();
        s.normal.background = PlayerGfx.Upload(N, N, px);
        return s;
    }

    private static GUIStyle BakeFrame(int h, int radius)
    {
        int w = radius * 2 + 2;
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
        {
            float t = (y + 0.5f) / h;
            float fr = Ui.Lerp(0.066f, 0.122f, t);
            float fg = Ui.Lerp(0.070f, 0.129f, t);
            float fb = Ui.Lerp(0.086f, 0.149f, t);
            for (int x = 0; x < w; x++)
            {
                float sd = Sd(x + 0.5f, y + 0.5f, w, h, radius);
                float co = Ui.Clamp01(0.5f - sd);
                float ci = Ui.Clamp01(-0.5f - sd);
                float af = co * 0.95f;
                float ae = (co - ci) * 0.10f;
                float at = ae + af * (1f - ae);
                if (at <= 0.001f)
                    continue;

                float k = af * (1f - ae);
                px[y * w + x] = PlayerGfx.Px(B((ae + fr * k) / at), B((ae + fg * k) / at), B((ae + fb * k) / at), B(at));
            }
        }

        var s = new GUIStyle();
        s.normal.background = PlayerGfx.Upload(w, h, px);
        s.border = NocturneStyle.Offset(radius, radius, 0, 0);
        return s;
    }

    private static GUIStyle BakeSwitch(bool on, Color c1, Color c2)
    {
        const int W = 46, H = 24, R = 12, KN = 26;
        int kOff = on ? 21 : -1;
        float knobA = on ? 1f : 0.8f;
        var px = new Color32[W * H];
        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                float r = 0f, g = 0f, b = 0f, a = 0f;
                float sd = Sd(x + 0.5f, y + 0.5f, W, H, R);
                float co = Ui.Clamp01(0.5f - sd);
                if (on)
                {
                    if (co > 0.001f)
                    {
                        float u = Ui.Clamp01((x + 0.5f - R) / (W - 2f * R));
                        Color fill = Ui.C(B(Ui.Lerp(c1.r, c2.r, u)) / 255f, B(Ui.Lerp(c1.g, c2.g, u)) / 255f, B(Ui.Lerp(c1.b, c2.b, u)) / 255f, 1f);
                        Over(ref r, ref g, ref b, ref a, fill, B(co) / 255f);
                    }
                }
                else
                {
                    float ci = Ui.Clamp01(-0.5f - sd);
                    float af = co * 0.60f;
                    float ae = (co - ci) * 0.16f;
                    float at = ae + af * (1f - ae);
                    if (at > 0.001f)
                    {
                        float k = af * (1f - ae);
                        float gc = B((ae + 0.04f * k) / at) / 255f;
                        Over(ref r, ref g, ref b, ref a, Ui.C(gc, gc, B((ae + 0.065f * k) / at) / 255f, 1f), B(at) / 255f);
                    }
                }

                int kx = x - kOff;
                if (kx >= 0 && kx < KN)
                {
                    float dx = kx + 0.5f - KN * 0.5f;
                    float dy = y + 1.5f - KN * 0.5f;
                    float ad = Ui.Clamp01(9f - Ui.Sqrt(dx * dx + dy * dy) + 0.5f);
                    float sy = dy + 1.6f;
                    float sa = 0.34f * Ui.Clamp01((11f - Ui.Sqrt(dx * dx + sy * sy)) / 3.2f);
                    float at = ad + sa * (1f - ad);
                    if (at > 0.001f)
                    {
                        float kg = B(ad / at) / 255f;
                        Over(ref r, ref g, ref b, ref a, Ui.C(kg, kg, kg, 1f), B(at) / 255f * knobA);
                    }
                }

                if (a > 0f)
                    px[y * W + x] = PlayerGfx.Px(B(r / a), B(g / a), B(b / a), B(a));
            }
        }

        var st = new GUIStyle();
        st.normal.background = PlayerGfx.Upload(W, H, px);
        return st;
    }

    private static void Over(ref float r, ref float g, ref float b, ref float a, Color c, float ca)
    {
        r = c.r * ca + r * (1f - ca);
        g = c.g * ca + g * (1f - ca);
        b = c.b * ca + b * (1f - ca);
        a = ca + a * (1f - ca);
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
