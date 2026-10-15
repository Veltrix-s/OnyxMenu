using System;
using HarmonyLib;
using UnityEngine;

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

[HarmonyPatch(typeof(KeyboardJoystick), nameof(KeyboardJoystick.Update))]
internal static class PetKeyboardPatch
{
    public static void Postfix(KeyboardJoystick __instance)
    {
        if (!NocturnePet.Detached) return;

        bool typing = NocturneTextFocus.Any || NocturneMenu.Typing || NocturneChatWindow.Typing;
        NocturnePet.Feed(typing ? Ui.Zero2 : __instance.del);
        __instance.del = Ui.Zero2;
    }
}
