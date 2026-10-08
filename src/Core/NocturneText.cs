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

    internal static string HostOnly => T("Только хост.", "Host only.");
    internal static string MatchOnly => T("Только в матче.", "In match only.");
    internal static string HostInMatch => T("Только хост, в матче.", "Host only, in match.");
    internal static string InGameOnly => T("Только в игре (не в лобби).", "In-game only (not in lobby).");
    internal static string NoOthers => T("Нет других игроков.", "No other players.");
    internal static string NoPlayer => T("Нет игрока.", "No player.");
    internal static string NoTarget => T("Нет цели.", "No target.");
    internal static string NoPet => T("Нет пета.", "No pet.");
    internal static string NoMeeting => T("Нет собрания.", "No meeting.");
    internal static string Failed => T("Не удалось.", "Failed.");
    internal static string Empty => T("Пусто.", "Empty.");

    internal static string NoTargetLow => T("нет цели", "no target");
    internal static string FailedLow => T("не удалось", "failed");
    internal static string MatchOnlyLow => T("только в матче", "in-match only");

    internal static string LangName => IsRussian ? "Русский" : "English";

    internal static void Toggle()
    {
        NocturneConfig.Language.Value = IsRussian ? "en" : "ru";
    }
}
