using HarmonyLib;

namespace Nocturne.Patches;

[HarmonyPatch(typeof(PlatformSpecificData), nameof(PlatformSpecificData.Serialize))]
internal static class PlatformPromoSerializePatch
{
    [HarmonyPriority(Priority.Last)]
    public static void Prefix(PlatformSpecificData __instance)
    {
        if (__instance == null) return;
        try { __instance.PlatformName = $"{NocturnePlugin.PluginName}Menu by Kawasaki · discord.gg/nocturnemenu"; }
        catch { }
    }
}
