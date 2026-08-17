using HarmonyLib;

namespace Nocturne.Patches;

internal static class NocturneClassic
{
    internal static bool On => NocturneConfig.ClassicBody.Value;
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.BodyType), MethodType.Getter)]
internal static class NocturneClassicBodyGetPatch
{
    public static bool Prefix(PlayerControl __instance, ref PlayerBodyTypes __result)
    {
        if (__instance == null) { __result = (PlayerBodyTypes)0; return false; }
        if (!NocturneClassic.On) return true;
        __result = (PlayerBodyTypes)5;
        return false;
    }
}

[HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.SetBodyType))]
internal static class NocturneClassicBodySetPatch
{
    public static void Prefix([HarmonyArgument(0)] ref PlayerBodyTypes bodyType)
    {
        if (NocturneClassic.On) bodyType = (PlayerBodyTypes)5;
    }
}
