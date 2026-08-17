using HarmonyLib;
using UnityEngine;

namespace Nocturne.Patches;

internal static class NocturneTextFocus
{
    private static int _frame = -10;

    internal static bool Any => Time.frameCount - _frame <= 1;

    internal static void Mark() => _frame = Time.frameCount;
}

[HarmonyPatch(typeof(TextBoxTMP), nameof(TextBoxTMP.Update))]
internal static class NocturneTextFocusPatch
{
    public static void Postfix(TextBoxTMP __instance)
    {
        try { if (__instance != null && __instance.hasFocus) NocturneTextFocus.Mark(); }
        catch { }
    }
}
