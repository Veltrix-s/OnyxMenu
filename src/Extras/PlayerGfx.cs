using System;
using System.Runtime.CompilerServices;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace Nocturne;

internal static class PlayerGfx
{
    internal static GUIContent Blank;
    internal static GUIStyle None, Flat, Round14, Round8, Pill, Dot, Glow, Fade, Shadow, Ring;
    internal static GUIStyle Rise, Drop, Contact, Sheen;
    internal static GUIStyle Title, Line, Tiny, TinyRight, Chip, Mark, Head, Row, RowOn, Micro, Track, Thumb;
    internal static GUIStyle Artist, Clock, ClockRight, Count, Press;

    private static GUIStyle[] _icons;
    private static Texture2D _probe;

    internal static void Ensure()
    {
        if (_probe != null)
            return;

        Blank = GUIContent.none;
        None = GUIStyle.none;
        _icons = new GUIStyle[Enum.GetNames(typeof(NocturneIcon)).Length];

        _probe = Make(2, 2, (x, y) => 1f);
        Flat = Box(_probe, 0);
        Round14 = Box(Make(30, 30, (x, y) => Round(x, y, 30, 0f, 14f)), 14);
        Round8 = Box(Make(18, 18, (x, y) => Round(x, y, 18, 0f, 8f)), 8);
        Pill = Box(Make(8, 8, (x, y) => Round(x, y, 8, 0f, 4f)), 3);
        Dot = Box(Make(48, 48, (x, y) => 24.5f - Dist(x, y, 24f, 24f)), 0);
        Glow = Box(Make(128, 128, (x, y) =>
        {
            float d = Dist(x, y, 64f, 64f) / 64f;
            if (d >= 1f)
                return 0f;
            float k = 1f - d;
            return k * k * (0.35f + 0.65f * k);
        }), 0);
        Fade = Box(Make(2, 64, (x, y) =>
        {
            float t = 1f - y / 63f;
            if (t >= 0.78f)
                return 1f;
            t /= 0.78f;
            return t * t * (3f - 2f * t);
        }), 0);
        Shadow = Box(Make(98, 98, (x, y) =>
        {
            float d = Sd(x + 0.5f, y + 0.5f, 34f, 34f, 64f, 64f, 14f);
            if (d <= 0f)
                return 0.62f;
            float k = 1f - d / 34f;
            return k <= 0f ? 0f : 0.62f * k * k * (3f - 2f * k);
        }), 48);
        Ring = Box(Make(30, 30, (x, y) => 1.1f - MathF.Abs(Sd(x + 0.5f, y + 0.5f, 0f, 0f, 30f, 30f, 14f) + 0.6f)), 14);
        Rise = Box(Make(2, 64, (x, y) =>
        {
            float t = y / 63f;
            return t * t * (3f - 2f * t);
        }), 0);
        Drop = Box(Make(144, 144, (x, y) => Fall(Sd(x + 0.5f, y + 0.5f, 52f, 52f, 92f, 92f, 18f), 52f)), 70);
        Contact = Box(Make(64, 64, (x, y) => Fall(Sd(x + 0.5f, y + 0.5f, 14f, 14f, 50f, 50f, 14f), 14f)), 28);
        Sheen = Box(Make((int)PlayerPoster.W, 64, Gleam), 0);

        Fonts(out Font sans, out Font semi);
        string head = semi != null ? "Segoe UI Semibold" : sans != null ? "Segoe UI" : "default";
        if (semi == null)
            semi = sans;
        NocturnePlugin.Logger?.LogInfo($"[Player] fonts: text {(sans != null ? "Segoe UI" : "default")}, title {head}");
        Title = Text(18, semi != null ? FontStyle.Normal : FontStyle.Bold, TextAnchor.MiddleLeft, semi);
        Artist = Text(13, FontStyle.Normal, TextAnchor.MiddleLeft, sans);
        Clock = Text(12, FontStyle.Normal, TextAnchor.MiddleLeft, sans);
        ClockRight = Text(12, FontStyle.Normal, TextAnchor.MiddleRight, sans);
        Count = Text(11, semi != null ? FontStyle.Normal : FontStyle.Bold, TextAnchor.MiddleCenter, semi);
        Line = Text(12, FontStyle.Normal, TextAnchor.MiddleLeft);
        Tiny = Text(11, FontStyle.Normal, TextAnchor.MiddleLeft);
        TinyRight = Text(11, FontStyle.Normal, TextAnchor.MiddleRight);
        Chip = Text(11, FontStyle.Bold, TextAnchor.MiddleCenter);
        Mark = Text(34, FontStyle.Bold, TextAnchor.MiddleLeft);
        Head = Text(12, FontStyle.Bold, TextAnchor.MiddleLeft);
        Row = Text(13, FontStyle.Normal, TextAnchor.MiddleLeft);
        RowOn = Text(13, FontStyle.Bold, TextAnchor.MiddleLeft);
        Press = Text(13, FontStyle.Bold, TextAnchor.MiddleCenter);
        Micro = Text(9, FontStyle.Normal, TextAnchor.MiddleCenter);

        Track = new GUIStyle(GUI.skin.horizontalSlider);
        Track.normal.background = null;
        Track.border.left = Track.border.right = Track.border.top = Track.border.bottom = 0;
        Track.fixedHeight = 0f;
        Thumb = new GUIStyle(GUI.skin.horizontalSliderThumb);
        Thumb.normal.background = null;
        Thumb.fixedWidth = 0f;
        Thumb.fixedHeight = 0f;
    }

    internal static GUIStyle Icon(NocturneIcon icon)
    {
        int i = (int)icon;
        if (_icons[i] == null)
        {
            var s = new GUIStyle(None);
            s.normal.background = NocturneIcons.Tex(icon);
            _icons[i] = s;
        }
        return _icons[i];
    }

    internal static string Clean(string name)
    {
        if (name.IndexOf('_') >= 0)
            name = name.Replace('_', ' ');
        int n = name.Length;
        int d = 0;
        while (d < n && char.IsDigit(name[d]))
            d++;
        int i = 0;
        if (d > 0 && d <= 3 && (d >= 2 || (d < n && (name[d] == '.' || name[d] == '-' || name[d] == ')'))))
        {
            i = d;
            while (i < n && (name[i] == '.' || name[i] == '-' || name[i] == ')' || name[i] == ' '))
                i++;
            if (i >= n)
                i = 0;
        }
        int e = n;
        while (e > i && char.IsDigit(name[e - 1]))
            e--;
        if (n - e < 6 || e <= i + 1 || name[e - 1] != ' ')
            e = n;
        else
            e--;
        return name.Substring(i, e - i).Trim();
    }
    internal static void Hsv(Color c, out float h, out float s, out float v)
    {
        float mx = Math.Max(c.r, Math.Max(c.g, c.b));
        float mn = Math.Min(c.r, Math.Min(c.g, c.b));
        float d = mx - mn;
        v = mx;
        s = mx > 0f ? d / mx : 0f;
        if (d <= 0f)
            h = 0f;
        else if (mx == c.r)
            h = ((c.g - c.b) / d + 6f) % 6f / 6f;
        else if (mx == c.g)
            h = ((c.b - c.r) / d + 2f) / 6f;
        else
            h = ((c.r - c.g) / d + 4f) / 6f;
    }

    internal static Color FromHsv(float h, float s, float v)
    {
        h -= MathF.Floor(h);
        s = Ui.Clamp01(s);
        v = Ui.Clamp01(v);
        float c = v * s;
        float x = c * (1f - MathF.Abs(h * 6f % 2f - 1f));
        float m = v - c;
        float r, g, b;
        switch ((int)(h * 6f))
        {
            case 0:
                r = c;
                g = x;
                b = 0f;
                break;
            case 1:
                r = x;
                g = c;
                b = 0f;
                break;
            case 2:
                r = 0f;
                g = c;
                b = x;
                break;
            case 3:
                r = 0f;
                g = x;
                b = c;
                break;
            case 4:
                r = x;
                g = 0f;
                b = c;
                break;
            default:
                r = c;
                g = 0f;
                b = x;
                break;
        }
        return Ui.C(r + m, g + m, b + m, 1f);
    }

    private static GUIStyle Box(Texture2D tex, int border)
    {
        var s = new GUIStyle(None);
        s.normal.background = tex;
        s.border.left = s.border.right = s.border.top = s.border.bottom = border;
        return s;
    }

    private static void Fonts(out Font sans, out Font semi)
    {
        sans = null;
        semi = null;
        try
        {
            var names = Font.GetOSInstalledFontNames();
            for (int i = 0; i < names.Length; i++)
            {
                string n = names[i];
                if (sans == null && string.Equals(n, "Segoe UI", StringComparison.OrdinalIgnoreCase))
                    sans = Dynamic(n);
                else if (semi == null && string.Equals(n, "Segoe UI Semibold", StringComparison.OrdinalIgnoreCase))
                    semi = Dynamic(n);
            }
            new GUIStyle().font = sans != null ? sans : semi;
        }
        catch (Exception e)
        {
            sans = null;
            semi = null;
            NocturnePlugin.Logger?.LogWarning($"[Player] fonts: {e.GetType().Name} {e.Message}");
        }
    }

    private static Font Dynamic(string family)
    {
        Font f = Font.CreateDynamicFontFromOSFont(family, 16);
        f.hideFlags = HideFlags.HideAndDontSave;
        return f;
    }

    private static float Fall(float d, float reach)
    {
        if (d <= 0f)
            return 1f;
        float k = 1f - d / reach;
        if (k <= 0f)
            return 0f;
        k = k * k * (3f - 2f * k);
        return k * MathF.Sqrt(k);
    }

    private static float Gleam(int x, int y)
    {
        float top = 63.5f - y;
        float d = Sd(x + 0.5f, top, 0f, 0f, PlayerPoster.W, 4000f, 14f);
        float inside = Ui.Clamp01(0.5f - d);
        float line = Ui.Clamp01(1.2f - MathF.Abs(d + 0.7f));
        float edge = Ui.Clamp01(1f - top / 56f);
        float wash = Ui.Clamp01(1f - top / 44f);
        return inside * (line * edge * edge * (3f - 2f * edge) * 0.45f + wash * wash * 0.08f);
    }

    internal static void Cast(Rect r, float k)
    {
        GUI.color = Ui.C(0f, 0f, 0f, 0.55f * k);
        GUI.Box(Ui.R(r.m_XMin - 40f, r.m_YMin - 14f, r.m_Width + 80f, r.m_Height + 72f), Blank, Drop);
        GUI.color = Ui.C(0f, 0f, 0f, 0.60f * k);
        GUI.Box(Ui.R(r.m_XMin - 12f, r.m_YMin - 3f, r.m_Width + 24f, r.m_Height + 20f), Blank, Contact);
    }

    private static GUIStyle Text(int size, FontStyle fs, TextAnchor anchor, Font font = null)
    {
        var s = new GUIStyle(None);
        if (font != null)
            s.font = font;
        s.fontSize = size;
        s.fontStyle = fs;
        s.alignment = anchor;
        s.clipping = TextClipping.Clip;
        s.wordWrap = false;
        s.normal.textColor = Ui.White;
        return s;
    }

    internal static Color32 Px(byte r, byte g, byte b, byte a)
    {
        Color32 c = default;
        c.r = r;
        c.g = g;
        c.b = b;
        c.a = a;
        return c;
    }

    internal static unsafe Color32[] Copy(Il2CppStructArray<Color32> src)
    {
        var px = new Color32[src.Length];
        long bytes = (long)px.Length * Unsafe.SizeOf<Color32>();
        fixed (Color32* d = px)
            Buffer.MemoryCopy((byte*)src.Pointer + IntPtr.Size * 4, d, bytes, bytes);
        return px;
    }

    internal static Texture2D Upload(int w, int h, Color32[] px)
    {
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        Fill(t, px);
        return t;
    }

    internal static unsafe void Fill(Texture2D t, Color32[] px)
    {
        fixed (Color32* p = px)
            t.LoadRawTextureData((IntPtr)p, px.Length * Unsafe.SizeOf<Color32>());
        t.Apply();
    }

    private static Texture2D Make(int w, int h, Func<int, int, float> alpha)
    {
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                px[y * w + x] = Px(255, 255, 255, (byte)(Ui.Clamp01(alpha(x, y)) * 255f + 0.5f));
        return Upload(w, h, px);
    }

    private static float Dist(int x, int y, float cx, float cy)
    {
        float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    private static float Sd(float px, float py, float x0, float y0, float x1, float y1, float r)
    {
        float hx = (x1 - x0) * 0.5f - r, hy = (y1 - y0) * 0.5f - r;
        float qx = MathF.Abs(px - (x0 + x1) * 0.5f) - hx, qy = MathF.Abs(py - (y0 + y1) * 0.5f) - hy;
        float ox = MathF.Max(qx, 0f), oy = MathF.Max(qy, 0f);
        return MathF.Sqrt(ox * ox + oy * oy) + MathF.Min(MathF.Max(qx, qy), 0f) - r;
    }

    private static float Round(int x, int y, int n, float inset, float r) => 0.5f - Sd(x + 0.5f, y + 0.5f, inset, inset, n - inset, n - inset, r);
}
