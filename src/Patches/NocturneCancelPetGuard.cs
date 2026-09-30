using System;
using HarmonyLib;

namespace Nocturne.Patches;

[HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.CancelPet))]
internal static class NocturneCancelPetGuard
{
    private static Exception Finalizer(Exception __exception) => null;
}

[HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.CheckCancelPetting))]
internal static class NocturnePetFollowGuard
{
    public static bool Prefix(PlayerPhysics __instance)
    {
        if (!NocturnePet.Holding || __instance == null || __instance.myPlayer == null)
            return true;
        return !__instance.myPlayer.AmOwner;
    }
}
