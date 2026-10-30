using HarmonyLib;
using InnerNet;

namespace Nocturne.Patches;

[HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.CanBan))]
internal static class MatchCanBanPatch
{
    public static void Postfix(InnerNetClient __instance, ref bool __result)
    {
        if (ShouldUnlock(__instance))
        {
            __result = true;
        }
    }

    internal static bool ShouldUnlock(InnerNetClient client)
    {
        return NocturneConfig.UnlockMatchKickBan.Value
            && client != null
            && client.AmHost
            && ShipStatus.Instance != null;
    }
}

[HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.CanKick))]
internal static class MatchCanKickPatch
{
    public static void Postfix(InnerNetClient __instance, ref bool __result)
    {
        if (MatchCanBanPatch.ShouldUnlock(__instance))
        {
            __result = true;
        }
    }
}

[HarmonyPatch(typeof(BanMenu), nameof(BanMenu.SetVisible))]
internal static class MatchBanMenuVisibilityPatch
{
    public static void Postfix(BanMenu __instance, bool show)
    {
        if (__instance == null || !show || AmongUsClient.Instance == null || !MatchCanBanPatch.ShouldUnlock(AmongUsClient.Instance))
        {
            return;
        }

        try
        {
            __instance.BanButton.gameObject.SetActive(true);
            __instance.KickButton.gameObject.SetActive(true);
        }
        catch (System.Exception error)
        {
            NocturnePlugin.Logger?.LogWarning($"Match ban menu unlock failed: {error.Message}");
        }
    }
}
