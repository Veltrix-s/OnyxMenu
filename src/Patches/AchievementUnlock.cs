using AmongUs.Data;
using HarmonyLib;

namespace Nocturne.Patches;

[HarmonyPatch(typeof(AchievementManager), nameof(AchievementManager.Awake))]
internal static class AchievementUnlockPatch
{
    public static void Postfix(AchievementManager __instance)
    {
        if (NocturneConfig.UnlockAllAchievements.Value)
            AchievementUnlock.All(__instance);
    }
}

internal static class AchievementUnlock
{
    private static bool _done;

    internal static void All(AchievementManager am)
    {
        if (_done || am == null)
            return;

        bool ready = SteamManager.Initialized && DataManager.Player.Account.LoginStatus == EOSManager.AccountLoginStatus.LoggedIn;
        if (!ready)
            return;

        var e = AchievementManager.AchievementGameModeKey.Keys.GetEnumerator();
        while (e.MoveNext())
            am.UnlockAchievementImpl(e.Current);

        _done = true;
    }
}
