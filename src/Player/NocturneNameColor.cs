using System.Text;
using UnityEngine;

namespace Nocturne;

internal static class NocturneNameColor
{
    private enum Kind { Gradient, Rgb, Pulse, Wave, Sweep }

    private struct Preset
    {
        internal readonly string Ru;
        internal readonly string En;
        internal readonly Kind Anim;
        internal readonly Color A;
        internal readonly Color B;
        internal Preset(string ru, string en, Kind anim, Color a, Color b) { Ru = ru; En = en; Anim = anim; A = a; B = b; }
    }

    private static readonly Preset[] Presets =
    {
        new Preset("Aqua → Violet", "Aqua → Violet", Kind.Gradient, Hex("#45FFD9"), Hex("#9B5CFF")),
        new Preset("Закат",         "Sunset",        Kind.Gradient, Hex("#FF9A3D"), Hex("#FF3D77")),
        new Preset("Огонь",         "Fire",          Kind.Gradient, Hex("#FFE259"), Hex("#FF512F")),
        new Preset("Лёд",           "Ice",           Kind.Gradient, Hex("#A8EDFF"), Hex("#2A6CFF")),
        new Preset("Токсик",        "Toxic",         Kind.Gradient, Hex("#C6FF4D"), Hex("#1FA34A")),
        new Preset("Золото",        "Gold",          Kind.Gradient, Hex("#FFE9A8"), Hex("#C9971B")),
        new Preset("Океан",         "Ocean",         Kind.Gradient, Hex("#00C6FF"), Hex("#0072FF")),
        new Preset("Галактика",     "Galaxy",        Kind.Gradient, Hex("#7F00FF"), Hex("#E100FF")),
        new Preset("Неон",          "Neon",          Kind.Gradient, Hex("#39FF14"), Hex("#00E5FF")),
        new Preset("Изумруд",       "Emerald",       Kind.Gradient, Hex("#43E97B"), Hex("#38F9D7")),
        new Preset("Радуга",        "Rainbow",       Kind.Rgb,      Color.white,    Color.white),
        new Preset("Пульс огня",    "Fire pulse",    Kind.Pulse,    Hex("#FFB347"), Hex("#FF2222")),
        new Preset("Пульс аква",    "Aqua pulse",    Kind.Pulse,    Hex("#45FFD9"), Hex("#1170FF")),
        new Preset("Мерцание",      "Shimmer",       Kind.Wave,     Hex("#45FFD9"), Hex("#9B5CFF")),
        new Preset("Комета",        "Comet",         Kind.Sweep,    Hex("#6A82FB"), Hex("#FC5C7D")),
        new Preset("Белый",         "White",         Kind.Gradient, Color.white,    Color.white),
    };

    private static readonly StringBuilder Builder = new StringBuilder(256);
    private static readonly StringBuilder StripBuilder = new StringBuilder(64);

    internal static int PresetCount => Presets.Length;
    internal static int Clamp(int i) => i < 0 ? 0 : (i >= Presets.Length ? Presets.Length - 1 : i);
    internal static int Next(int i) { i = Clamp(i) + 1; return i >= Presets.Length ? 0 : i; }
    internal static string StyleName(int i) { Preset p = Presets[Clamp(i)]; return NocturneText.T(p.Ru, p.En); }

    internal static string Strip(string value)
    {
        if (string.IsNullOrEmpty(value) || (value.IndexOf('<') < 0 && value.IndexOf('&') < 0))
            return value ?? string.Empty;

        StripBuilder.Length = 0;
        int i = 0;
        while (i < value.Length)
        {
            char ch = value[i];
            if (ch == '<')
            {
                int close = value.IndexOf('>', i + 1);
                if (close >= 0) { i = close + 1; continue; }
            }
            StripBuilder.Append(ch);
            i++;
        }
        StripBuilder.Replace("&lt;", "<").Replace("&gt;", ">").Replace("&amp;", "&");
        return StripBuilder.ToString();
    }

    internal static string Apply(string rawName, int styleIndex, bool animated)
    {
        if (string.IsNullOrEmpty(rawName)) return rawName ?? string.Empty;
        rawName = Strip(rawName);
        if (rawName.Length == 0) return string.Empty;

        Preset preset = Presets[Clamp(styleIndex)];
        float time = animated ? Time.unscaledTime : 0f;
        Builder.Length = 0;
        int n = rawName.Length;

        switch (preset.Anim)
        {
            case Kind.Rgb:
                for (int i = 0; i < n; i++)
                {
                    float phase = i * 0.5f + time * 2.5f;
                    AppendChar(rawName[i], Mathf.Sin(phase) * 0.5f + 0.5f, Mathf.Sin(phase + 4f) * 0.5f + 0.5f, Mathf.Sin(phase + 2f) * 0.5f + 0.5f);
                }
                break;

            case Kind.Pulse:
            {
                float t = animated ? Mathf.Sin(time * 2.2f) * 0.5f + 0.5f : 0.5f;
                Color c = Close(preset.A, preset.B) ? preset.A : LerpOkLab(preset.A, preset.B, t);
                AppendSolid(rawName, c);
                break;
            }

            case Kind.Wave:
            {
                float hi = Mathf.Repeat(time * 8f, n + 4f) - 2f;
                for (int i = 0; i < n; i++)
                {
                    float pos = n == 1 ? 0.5f : (float)i / (n - 1);
                    Color c = LerpOkLab(preset.A, preset.B, pos);
                    if (animated)
                    {
                        float glow = Mathf.Clamp01(1f - Mathf.Abs(i - hi) / 1.6f);
                        if (glow > 0f) c = Color.Lerp(c, Color.white, glow * 0.6f);
                    }
                    AppendChar(rawName[i], c.r, c.g, c.b);
                }
                break;
            }

            case Kind.Sweep:
            {
                float sweep = Mathf.Repeat(time * 10f, n + 6f) - 3f;
                for (int i = 0; i < n; i++)
                {
                    Color c;
                    if (animated)
                    {
                        float band = Mathf.Clamp01(1f - Mathf.Abs(i - sweep) / 2.2f);
                        c = band > 0f ? LerpOkLab(preset.A, preset.B, band) : preset.A;
                    }
                    else
                    {
                        float pos = n == 1 ? 0.5f : (float)i / (n - 1);
                        c = LerpOkLab(preset.A, preset.B, pos);
                    }
                    AppendChar(rawName[i], c.r, c.g, c.b);
                }
                break;
            }

            default:
            {
                bool solid = Close(preset.A, preset.B);
                if (solid || n == 1)
                {
                    AppendSolid(rawName, solid ? preset.A : LerpOkLab(preset.A, preset.B, 0.5f));
                    break;
                }
                float flow = time * 5.4f;
                float span = 2f * (n - 1);
                for (int i = 0; i < n; i++)
                {
                    float cycle = Mathf.Repeat(i + flow, span);
                    float t = cycle <= (n - 1) ? cycle / (n - 1) : (span - cycle) / (n - 1);
                    Color c = LerpOkLab(preset.A, preset.B, t);
                    AppendChar(rawName[i], c.r, c.g, c.b);
                }
                break;
            }
        }

        return Builder.ToString();
    }

    private static void AppendSolid(string text, Color c)
    {
        AppendOpen(c);
        for (int i = 0; i < text.Length; i++) AppendEscaped(text[i]);
        Builder.Append("</color>");
    }

    private static void AppendChar(char ch, float r, float g, float b)
    {
        AppendOpen(new Color(r, g, b, 1f));
        AppendEscaped(ch);
        Builder.Append("</color>");
    }

    private static void AppendOpen(Color c)
    {
        int r = Mathf.Clamp(Mathf.RoundToInt(c.r * 255f), 0, 255);
        int g = Mathf.Clamp(Mathf.RoundToInt(c.g * 255f), 0, 255);
        int b = Mathf.Clamp(Mathf.RoundToInt(c.b * 255f), 0, 255);
        const string hex = "0123456789ABCDEF";
        Builder.Append("<color=#");
        Builder.Append(hex[(r >> 4) & 0xF]).Append(hex[r & 0xF]);
        Builder.Append(hex[(g >> 4) & 0xF]).Append(hex[g & 0xF]);
        Builder.Append(hex[(b >> 4) & 0xF]).Append(hex[b & 0xF]);
        Builder.Append('>');
    }

    private static void AppendEscaped(char ch)
    {
        switch (ch)
        {
            case '<': Builder.Append("&lt;"); break;
            case '>': Builder.Append("&gt;"); break;
            case '&': Builder.Append("&amp;"); break;
            default: Builder.Append(ch); break;
        }
    }

    private static bool Close(Color a, Color b) =>
        Mathf.Abs(a.r - b.r) < 0.004f && Mathf.Abs(a.g - b.g) < 0.004f && Mathf.Abs(a.b - b.b) < 0.004f;

    private static Color LerpOkLab(Color a, Color b, float t)
    {
        RgbToOkLab(a, out float l1, out float a1, out float b1);
        RgbToOkLab(b, out float l2, out float a2, out float b2);
        return OkLabToRgb(Mathf.Lerp(l1, l2, t), Mathf.Lerp(a1, a2, t), Mathf.Lerp(b1, b2, t));
    }

    private static float ToLinear(float c) => c <= 0.04045f ? c / 12.92f : Mathf.Pow((c + 0.055f) / 1.055f, 2.4f);
    private static float ToSrgb(float c) { if (c < 0f) c = 0f; return c <= 0.0031308f ? 12.92f * c : 1.055f * Mathf.Pow(c, 1f / 2.4f) - 0.055f; }
    private static float Cbrt(float x) => x <= 0f ? 0f : Mathf.Pow(x, 1f / 3f);

    private static void RgbToOkLab(Color c, out float L, out float A, out float B)
    {
        float r = ToLinear(c.r), g = ToLinear(c.g), bl = ToLinear(c.b);
        float l = 0.4122214708f * r + 0.5363325363f * g + 0.0514459929f * bl;
        float m = 0.2119034982f * r + 0.6806995451f * g + 0.1073969566f * bl;
        float s = 0.0883024619f * r + 0.2817188376f * g + 0.6299787005f * bl;
        float l_ = Cbrt(l), m_ = Cbrt(m), s_ = Cbrt(s);
        L = 0.2104542553f * l_ + 0.7936177850f * m_ - 0.0040720468f * s_;
        A = 1.9779984951f * l_ - 2.4285922050f * m_ + 0.4505937099f * s_;
        B = 0.0259040371f * l_ + 0.7827717662f * m_ - 0.8086757660f * s_;
    }

    private static Color OkLabToRgb(float L, float A, float B)
    {
        float l_ = L + 0.3963377774f * A + 0.2158037573f * B;
        float m_ = L - 0.1055613458f * A - 0.0638541728f * B;
        float s_ = L - 0.0894841775f * A - 1.2914855480f * B;
        float l = l_ * l_ * l_, m = m_ * m_ * m_, s = s_ * s_ * s_;
        float r = 4.0767416621f * l - 3.3077115913f * m + 0.2309699292f * s;
        float g = -1.2684380046f * l + 2.6097574011f * m - 0.3413193965f * s;
        float b = -0.0041960863f * l - 0.7034186147f * m + 1.7076147010f * s;
        return new Color(Mathf.Clamp01(ToSrgb(r)), Mathf.Clamp01(ToSrgb(g)), Mathf.Clamp01(ToSrgb(b)), 1f);
    }

    private static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out Color c) ? c : Color.white;
}

public sealed class NocturneColoredName : MonoBehaviour
{
    private string _raw;
    private int _style = -1;
    private bool _anim;

    public void LateUpdate()
    {
        if (!NocturneConfig.NameColor.Value) return;
        PlayerControl me = PlayerControl.LocalPlayer;
        if (me == null || me.cosmetics == null || me.cosmetics.nameText == null) return;

        if (NocturneConfig.VisualPlayerInfoNames.Value || NocturneConfig.RevealRoles.Value
            || (NocturneConfig.UnmaskShapeshifter != null && NocturneConfig.UnmaskShapeshifter.Value)
            || NocturneAuthor.Is(me))
            return;

        string raw = me.CurrentOutfit != null ? me.CurrentOutfit.PlayerName : (me.Data != null ? me.Data.PlayerName : null);
        raw = NocturneNameColor.Strip(raw);
        if (string.IsNullOrEmpty(raw)) return;

        int style = NocturneConfig.NameColorStyle.Value;
        bool anim = NocturneConfig.NameColorAnimated.Value;
        if (!anim && raw == _raw && style == _style && !_anim) return;
        _raw = raw; _style = style; _anim = anim;

        try { me.cosmetics.nameText.text = NocturneNameColor.Apply(raw, style, anim); }
        catch { }
    }
}
