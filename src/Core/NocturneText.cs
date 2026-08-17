namespace Nocturne;

internal static class NocturneText
{
    private static string _langRaw;
    private static bool _langRu;

    internal static bool IsRussian
    {
        get
        {
            string v = NocturneConfig.Language.Value;
            if (v != _langRaw)
            {
                _langRaw = v;
                _langRu = !string.IsNullOrEmpty(v) && v.Trim().ToLowerInvariant() == "ru";
            }
            return _langRu;
        }
    }

    internal static string T(string ru, string en) => IsRussian ? ru : en;

    internal static string LangName => IsRussian ? "Русский" : "English";

    internal static void Toggle()
    {
        NocturneConfig.Language.Value = IsRussian ? "en" : "ru";
    }
}
