using HarmonyLib;

namespace Nocturne.Patches;

internal static class NocturneSeasonDecor
{
    internal static readonly string[] Ru = { "Хэллоуин", "Прочее" };
    internal static readonly string[] En = { "Halloween", "Other" };

    private static readonly int[] _seen = new int[2];

    internal static int Seen(int i) => i >= 0 && i < _seen.Length ? _seen[i] : 0;

    internal static void Forget()
    {
        for (int i = 0; i < _seen.Length; i++) _seen[i] = 0;
    }

    internal static int Season(int monthStart) => monthStart == 10 ? 0 : 1;

    internal static void Note(int season)
    {
        if (season >= 0 && season < _seen.Length) _seen[season]++;
    }

    internal static bool On(int season) => (NocturneConfig.SeasonDecorMask.Value & (1 << season)) != 0;
}

[HarmonyPatch(typeof(DateHide), nameof(DateHide.ShouldHide))]
internal static class NocturneSeasonDecorPatch
{
    public static void Postfix(DateHide __instance, ref bool __result)
    {
        if (!NocturneConfig.SeasonDecor.Value || __instance == null) return;
        int s = NocturneSeasonDecor.Season(__instance.MonthStart);
        NocturneSeasonDecor.Note(s);
        if (NocturneSeasonDecor.On(s)) __result = false;
    }
}

[HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.Start))]
internal static class NocturneSeasonDecorResetPatch
{
    public static void Postfix() => NocturneSeasonDecor.Forget();
}
