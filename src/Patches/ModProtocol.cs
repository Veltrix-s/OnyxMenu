using HarmonyLib;

namespace Nocturne.Patches;

[HarmonyPatch(typeof(Constants), nameof(Constants.GetBroadcastVersion))]
internal static class ModProtocolVersionPatch
{
    public static void Postfix(ref int __result)
    {
        if (NocturneConfig.ModProtocol.Value)
            __result += 25;
    }
}

[HarmonyPatch(typeof(Constants), nameof(Constants.IsVersionModded))]
internal static class ModProtocolHostPatch
{
    public static bool Prefix(ref bool __result)
    {
        if (!NocturneConfig.ModProtocol.Value)
            return HarmonyControl.Continue;

        __result = true;
        return HarmonyControl.SkipOriginal;
    }
}
