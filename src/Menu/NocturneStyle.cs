using System.Collections.Generic;
using UnityEngine;

namespace Nocturne;

internal sealed class NocturnePalette
{
    internal NocturnePalette(string id, string name, Color window, Color panel, Color accent, Color accentSoft,
        Color button, Color buttonHover, Color text, Color muted)
    {
        Id = id;
        Name = name;
        Window = window;
        Panel = panel;
        Accent = accent;
        AccentSoft = accentSoft;
        Button = button;
        ButtonHover = buttonHover;
        Text = text;
        Muted = muted;
    }

    internal string Id { get; }
    internal string Name { get; }
    internal Color Window { get; }
    internal Color Panel { get; }
    internal Color Accent { get; }
    internal Color AccentSoft { get; }
    internal Color Button { get; }
    internal Color ButtonHover { get; }
    internal Color Text { get; }
    internal Color Muted { get; }
}

internal static class NocturneStyle
{

    private static Color Soft(Color a) => new Color(a.r * 0.34f, a.g * 0.34f, a.b * 0.34f, 1f);

    private static NocturnePalette Theme(string name, Color accent) =>
        new NocturnePalette(name.ToLowerInvariant(), name, Rgb(13, 13, 13), Rgb(21, 21, 21), accent, Soft(accent),
            Rgb(31, 31, 31), Rgb(46, 46, 46), Rgb(237, 237, 236), Rgb(143, 143, 141));

    private static readonly NocturnePalette[] Themes =
    {
        Theme("Orange", Rgb(255, 143, 36)),
        Theme("Amber", Rgb(255, 176, 40)),
        Theme("Gold", Rgb(240, 205, 60)),
        Theme("Yellow", Rgb(235, 225, 72)),
        Theme("Lime", Rgb(178, 222, 60)),
        Theme("Green", Rgb(74, 201, 110)),
        Theme("Emerald", Rgb(45, 208, 140)),
        Theme("Mint", Rgb(120, 232, 182)),
        Theme("Teal", Rgb(40, 200, 182)),
        Theme("Cyan", Rgb(44, 206, 200)),
        Theme("Aqua", Rgb(60, 220, 236)),
        Theme("Sky", Rgb(82, 182, 255)),
        Theme("Blue", Rgb(74, 150, 255)),
        Theme("Indigo", Rgb(104, 98, 255)),
        Theme("Violet", Rgb(160, 110, 255)),
        Theme("Purple", Rgb(192, 100, 255)),
        Theme("Magenta", Rgb(236, 82, 232)),
        Theme("Pink", Rgb(255, 94, 168)),
        Theme("Rose", Rgb(255, 112, 142)),
        Theme("Red", Rgb(242, 32, 38)),
        Theme("Crimson", Rgb(226, 46, 82)),
        Theme("Coral", Rgb(255, 120, 92)),
        Theme("Slate", Rgb(122, 152, 208)),
        Theme("Silver", Rgb(212, 216, 224)),
    };

    private static Texture2D _white;
    private static GUIStyle _fill;
    private static GUIStyle _texStyle;
    private static readonly Dictionary<int, Texture2D> RoundedTex = new Dictionary<int, Texture2D>();
    private static readonly Dictionary<int, GUIStyle> RoundedStyles = new Dictionary<int, GUIStyle>();
    private static readonly Dictionary<int, GUIStyle> StrokeStyles = new Dictionary<int, GUIStyle>();

    internal static int ThemeCount => Themes.Length;
    internal static NocturnePalette ThemeAt(int i) => Themes[Mathf.Clamp(i, 0, Themes.Length - 1)];

    internal static NocturnePalette Current =>
        ThemeAt(NocturneConfig.ThemeIndex != null ? NocturneConfig.ThemeIndex.Value : 0);

    internal static Texture2D White
    {
        get
        {
            if (_white == null) _white = Solid(Color.white);
            return _white;
        }
    }

    internal static Texture2D Solid(Color c)
    {

        var t = new Texture2D(1, 1, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave,
            wrapMode = TextureWrapMode.Clamp
        };
        t.SetPixel(0, 0, c);
        t.Apply();
        return t;
    }

    internal static void Fill(Rect r, Color c)
    {
        if (_fill == null)
        {
            _fill = new GUIStyle();
            _fill.normal.background = White;
        }

        Color prev = GUI.color;
        GUI.color = new Color(c.r, c.g, c.b, c.a * prev.a);
        GUI.Box(r, GUIContent.none, _fill);
        GUI.color = prev;
    }

    internal static void FillRaw(Rect r)
    {
        if (_fill == null) { _fill = new GUIStyle(); _fill.normal.background = White; }
        GUI.Box(r, GUIContent.none, _fill);
    }

    internal static void FillRounded(Rect r, Color c, int radius)
    {
        GUIStyle st = RoundedStyle(radius);
        Color prev = GUI.color;
        GUI.color = new Color(c.r, c.g, c.b, c.a * prev.a);
        GUI.Box(r, GUIContent.none, st);
        GUI.color = prev;
    }

    internal static GUIStyle RoundedStyle(int radius)
    {
        radius = Mathf.Clamp(radius, 2, 40);
        if (RoundedStyles.TryGetValue(radius, out GUIStyle s) && s != null) return s;

        s = new GUIStyle();
        s.normal.background = RoundedTexture(radius);
        s.border = Offset(radius, radius, radius, radius);
        RoundedStyles[radius] = s;
        return s;
    }

    private static Texture2D RoundedTexture(int radius)
    {
        if (RoundedTex.TryGetValue(radius, out Texture2D t) && t != null) return t;

        int size = radius * 2 + 2;
        t = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float sx = x + 0.5f;
                float sy = y + 0.5f;
                float dx = sx < radius ? radius - sx : (sx > size - radius ? sx - (size - radius) : 0f);
                float dy = sy < radius ? radius - sy : (sy > size - radius ? sy - (size - radius) : 0f);
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(radius - dist + 0.5f);
                px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
        }

        t.SetPixels32(px);
        t.Apply();
        RoundedTex[radius] = t;
        return t;
    }

    internal static void StrokeRounded(Rect r, Color c, int radius, int thickness)
    {
        radius = Mathf.Clamp(radius, 3, 40);
        thickness = Mathf.Clamp(thickness, 1, 6);
        int key = radius * 16 + thickness;
        if (!StrokeStyles.TryGetValue(key, out GUIStyle st) || st == null)
        {
            st = new GUIStyle();
            st.normal.background = BuildStroke(radius, thickness);
            st.border = Offset(radius, radius, radius, radius);
            StrokeStyles[key] = st;
        }

        Color prev = GUI.color;
        GUI.color = new Color(c.r, c.g, c.b, c.a * prev.a);
        GUI.Box(r, GUIContent.none, st);
        GUI.color = prev;
    }

    private static Texture2D BuildStroke(int radius, int thickness)
    {
        int size = radius * 2 + 2;
        var t = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        float c = size / 2f;
        float ext = size / 2f - radius;
        float half = thickness * 0.5f;
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float qx = Mathf.Abs(x + 0.5f - c) - ext;
                float qy = Mathf.Abs(y + 0.5f - c) - ext;
                float outside = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f));
                float inside = Mathf.Min(Mathf.Max(qx, qy), 0f);
                float sd = outside + inside - radius;
                float a = Mathf.Clamp01(half - Mathf.Abs(sd) + 0.5f);
                px[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        }

        t.SetPixels32(px);
        t.Apply();
        return t;
    }

    internal static void DrawTex(Rect r, Texture2D tex)
    {
        if (tex == null) return;
        if (_texStyle == null) _texStyle = new GUIStyle();
        _texStyle.normal.background = tex;
        Color prev = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, prev.a);
        GUI.Box(r, GUIContent.none, _texStyle);
        GUI.color = prev;
    }

    internal static Texture2D BuildGradient(int w, int h, Color top, Color bottom, int radius)
    {
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
        {
            Color row = Color.Lerp(top, bottom, (float)y / Mathf.Max(1, h - 1));
            for (int x = 0; x < w; x++)
            {
                float dx = x < radius ? radius - x : (x > w - radius ? x - (w - radius) : 0f);
                float dy = y < radius ? radius - y : (y > h - radius ? y - (h - radius) : 0f);
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(radius - dist + 0.5f);
                px[y * w + x] = new Color(row.r, row.g, row.b, a);
            }
        }

        t.SetPixels32(px);
        t.Apply();
        return t;
    }

    internal static Texture2D BuildVFade(int h, Color color, float topAlpha, float bottomAlpha)
    {
        var t = new Texture2D(1, h, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        var px = new Color32[h];
        for (int y = 0; y < h; y++)
        {
            float a = Mathf.Lerp(topAlpha, bottomAlpha, (float)y / Mathf.Max(1, h - 1));
            px[y] = new Color(color.r, color.g, color.b, a);
        }
        t.SetPixels32(px);
        t.Apply();
        return t;
    }

    internal static Texture2D BuildDisc(int size)
    {
        var t = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        var px = new Color32[size * size];
        float cc = (size - 1) * 0.5f, rad = cc - 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x - cc, dy = y - cc;
                float a = Mathf.Clamp01(rad - Mathf.Sqrt(dx * dx + dy * dy) + 0.75f);
                px[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        t.SetPixels32(px);
        t.Apply();
        return t;
    }

    internal static Texture2D BuildGlow(int size)
    {
        var t = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        var px = new Color32[size * size];
        float cc = (size - 1) * 0.5f, rad = cc;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x - cc, dy = y - cc;
                float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) / rad);
                a = a * a * a;
                px[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        t.SetPixels32(px);
        t.Apply();
        return t;
    }

    internal static RectOffset Offset(int l, int r, int t, int b)
    {
        var o = new RectOffset();
        o.left = l;
        o.right = r;
        o.top = t;
        o.bottom = b;
        return o;
    }

    private static Color Rgb(byte r, byte g, byte b) => new Color(r / 255f, g / 255f, b / 255f, 1f);
}
