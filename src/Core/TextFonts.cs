using System.Text;

namespace Nocturne;

internal static class TextFonts
{
    internal const string Close = "</font>";

    private const string Cyrillic = "LiberationSans SDF";

    private static readonly string[] Assets =
    {
        "Barlow-Light SDF", "Barlow-Regular SDF", "Barlow-Medium SDF", "Barlow-SemiBold SDF",
        "Barlow-Bold SDF", "Barlow-ExtraBold SDF", "Barlow-Italic SDF", "Barlow-BoldItalic SDF",
        "Brook SDF", "LiberationSans SDF", "NotoSansJP-Regular SDF", "CONSOLA SDF",
        "digital-7 SDF", "OCRAEXT SDF", "VCR SDF",
    };

    private static readonly string[] Titles =
    {
        "Barlow Light", "Barlow Regular", "Barlow Medium", "Barlow SemiBold",
        "Barlow Bold", "Barlow ExtraBold", "Barlow Italic", "Barlow BoldItalic",
        "Brook", "Liberation Sans", "Noto Sans", "Consolas",
        "Digital-7", "OCR-A", "VCR",
    };

    private static readonly int[] Looks = { 0, 0, 0, 1, 1, 1, 2, 3, 0, 0, 0, 0, 0, 0, 0 };

    private static readonly string[] Tags = new string[Assets.Length];
    private static readonly string[] CyrOpen = new string[Assets.Length];
    private static readonly string[] CyrClose = new string[Assets.Length];

    static TextFonts()
    {
        for (int i = 0; i < Assets.Length; i++)
        {
            bool bold = (Looks[i] & 1) != 0;
            bool italic = (Looks[i] & 2) != 0;
            Tags[i] = "<font=\"" + Assets[i] + "\">";
            CyrOpen[i] = "<font=\"" + Cyrillic + "\">" + (bold ? "<b>" : "") + (italic ? "<i>" : "");
            CyrClose[i] = (italic ? "</i>" : "") + (bold ? "</b>" : "") + Close;
        }
    }

    internal static int Clamp(int i) => i < 0 || i > Assets.Length ? 0 : i;

    internal static int Next(int i)
    {
        i = Clamp(i) + 1;
        return i > Assets.Length ? 0 : i;
    }

    internal static string Title(int i)
    {
        i = Clamp(i);
        return i == 0 ? NocturneText.T("Обычный", "Default") : Titles[i - 1];
    }

    internal static string Open(int i)
    {
        i = Clamp(i);
        return i == 0 ? string.Empty : Tags[i - 1];
    }

    internal static string CyrillicOpen(int i)
    {
        i = Clamp(i);
        return i == 0 || Assets[i - 1] == Cyrillic ? string.Empty : CyrOpen[i - 1];
    }

    internal static string CyrillicClose(int i)
    {
        i = Clamp(i);
        return i == 0 || Assets[i - 1] == Cyrillic ? string.Empty : CyrClose[i - 1];
    }

    internal static bool IsCyrillic(char ch) => (ch >= 'Ѐ' && ch <= 'ԯ') || ch == '№';

    internal static string Wrap(string text, int i)
    {
        i = Clamp(i);
        if (i == 0 || string.IsNullOrEmpty(text))
            return text;

        string stand = CyrillicOpen(i);
        bool any = false;
        if (stand.Length > 0)
            for (int k = 0; k < text.Length && !any; k++)
                any = IsCyrillic(text[k]);
        if (!any)
            return Tags[i - 1] + text + Close;

        string standEnd = CyrillicClose(i);
        var sb = new StringBuilder(text.Length + 96);
        sb.Append(Tags[i - 1]);
        bool inTag = false;
        bool inRun = false;
        for (int k = 0; k < text.Length; k++)
        {
            char ch = text[k];
            if (inTag)
            {
                sb.Append(ch);
                if (ch == '>')
                    inTag = false;
                continue;
            }

            if (ch == '<')
            {
                if (inRun)
                {
                    sb.Append(standEnd);
                    inRun = false;
                }
                inTag = true;
                sb.Append(ch);
                continue;
            }

            bool cyr = IsCyrillic(ch);
            if (cyr != inRun)
            {
                sb.Append(cyr ? stand : standEnd);
                inRun = cyr;
            }
            sb.Append(ch);
        }

        if (inRun)
            sb.Append(standEnd);
        sb.Append(Close);
        return sb.ToString();
    }
}
