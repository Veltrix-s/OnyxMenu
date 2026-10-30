using System;
using System.Collections.Generic;
using HarmonyLib;

namespace Nocturne.Patches;

[HarmonyPatch(typeof(PetBehaviour), nameof(PetBehaviour.Update))]
internal static class PetPacePatch
{
    private sealed class Pace
    {
        public int Step = -1;
        public bool Free;
    }

    private static readonly Dictionary<IntPtr, Pace> Pets = new Dictionary<IntPtr, Pace>();

    public static bool Prefix(PetBehaviour __instance)
    {
        int step = NocturneHud.Steps;
        IntPtr key = __instance.Pointer;
        if (!Pets.TryGetValue(key, out Pace p))
        {
            if (Pets.Count > 128)
                Pets.Clear();
            p = new Pace();
            Pets[key] = p;
        }
        if (p.Step == step && !p.Free)
            return false;

        p.Step = step;
        PlayerControl owner = __instance.TargetPlayer;
        p.Free = owner != null && owner.Data != null && owner.Data.IsDead;
        return true;
    }
}
