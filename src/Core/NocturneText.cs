namespace Nocturne;

internal static class NocturneText
{
    private static string _langRaw;
    private static bool _langRu;

    internal static bool IsRussian => _langRu;

    internal static void Refresh()
    {
        string v = NocturneConfig.Language != null ? NocturneConfig.Language.Value : null;
        if (v == _langRaw) return;

        _langRaw = v;
        _langRu = !string.IsNullOrEmpty(v) && v.Trim().ToLowerInvariant() == "ru";
    }

    internal static string T(string ru, string en) => IsRussian ? ru : en;

    internal static string LangName => IsRussian ? "Русский" : "English";

    internal static void Toggle()
    {
        NocturneConfig.Language.Value = IsRussian ? "en" : "ru";
    }
}
