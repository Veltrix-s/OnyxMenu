using HarmonyLib;
using InnerNet;
using UnityEngine;

namespace Nocturne.Patches;

[HarmonyPatch(typeof(InnerNetClient), "CanBan")]
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
        return NocturneConfig.UnlockMatchKickBan != null
            && NocturneConfig.UnlockMatchKickBan.Value
            && client != null
            && client.AmHost
            && ShipStatus.Instance != null;
    }
}

[HarmonyPatch(typeof(InnerNetClient), "CanKick")]
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

[HarmonyPatch(typeof(BanMenu), "SetVisible")]
internal static class MatchBanMenuVisibilityPatch
{
    public static void Postfix(BanMenu __instance, bool show)
    {
        if (__instance == null || !show || AmongUsClient.Instance == null || !MatchCanBanPatch.ShouldUnlock((InnerNetClient)AmongUsClient.Instance))
        {
            return;
        }

        try
        {
            ((Component)__instance.BanButton).gameObject.SetActive(true);
            ((Component)__instance.KickButton).gameObject.SetActive(true);
        }
        catch (System.Exception error)
        {
            NocturnePlugin.Logger?.LogWarning((object)$"Match ban menu unlock failed: {error.Message}");
        }
    }
}
