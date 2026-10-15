using HarmonyLib;

namespace Nocturne;

internal static class MixupNames
{
    internal static bool Reveal(PlayerControl pc) =>
        NocturneConfig.MixupNames.Value && pc.CurrentOutfitType == PlayerOutfitType.MushroomMixup;
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.MixUpOutfit))]
internal static class MixupNamesPatch
{
    public static void Postfix(PlayerControl __instance)
    {
        if (MixupNames.Reveal(__instance))
            __instance.cosmetics.ToggleNameVisible(true);
    }
}
