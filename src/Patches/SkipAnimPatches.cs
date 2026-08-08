using System.Collections;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using HarmonyLib;

namespace Nocturne.Patches;

internal static class SkipAnim
{
    internal static IEnumerator Nothing() { yield break; }
}

[HarmonyPatch(typeof(ShhhBehaviour), nameof(ShhhBehaviour.PlayAnimation))]
internal static class ShhhSkipPatch
{
    public static bool Prefix(ShhhBehaviour __instance, ref Il2CppSystem.Collections.IEnumerator __result)
    {
        if (NocturneConfig.SkipShhh == null || !NocturneConfig.SkipShhh.Value || __instance == null) return true;
        try { __instance.gameObject.SetActive(false); } catch { }
        __result = SkipAnim.Nothing().WrapToIl2Cpp();
        return false;
    }
}
