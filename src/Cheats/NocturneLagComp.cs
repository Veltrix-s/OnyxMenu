using HarmonyLib;
using UnityEngine;

namespace Nocturne;

internal static class NocturneLagComp
{
    private static int _hold;

    private static bool On => NocturneConfig.LagComp.Value;

    [HarmonyPatch(typeof(CustomNetworkTransform), nameof(CustomNetworkTransform.Serialize))]
    private static class Suppress
    {
        private static bool Prefix(CustomNetworkTransform __instance, bool __1, ref bool __result)
        {
            if (!On || __1) return true;
            if (__instance == null || __instance.myPlayer != PlayerControl.LocalPlayer) return true;
            if (MeetingHud.Instance != null) return true;

            if (NocturneConfig.LagCompFreeze.Value)
            {
                __result = false;
                return false;
            }

            if (NocturneConfig.LagCompJitter.Value)
            {
                if (_hold > 0)
                {
                    _hold--;
                    __result = false;
                    return false;
                }
                int lo = Mathf.Clamp(NocturneConfig.LagCompJitterMin.Value, 1, 30);
                int hi = Mathf.Clamp(NocturneConfig.LagCompJitterMax.Value, lo, 30);
                _hold = Random.Range(lo, hi + 1);
            }
            return true;
        }
    }
}
