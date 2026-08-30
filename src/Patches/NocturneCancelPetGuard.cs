using System;
using HarmonyLib;

namespace Nocturne.Patches;

[HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.CancelPet))]
internal static class NocturneCancelPetGuard
{
    private static Exception Finalizer(Exception __exception) => null;
}
