using HarmonyLib;

namespace Nocturne.Patches;

[HarmonyPatch(typeof(PlatformSpecificData), nameof(PlatformSpecificData.Serialize))]
internal static class PlatformNameSpoofPatch
{
    [HarmonyPriority(Priority.Last)]
    public static void Prefix(PlatformSpecificData __instance)
    {
        if (NocturneConfig.SpoofPlatformNameEnabled.Value)
            __instance.PlatformName = NocturneConfig.SpoofPlatformNameValue.Value;
    }
}
