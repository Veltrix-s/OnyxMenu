using HarmonyLib;
using Hazel;
using InnerNet;
using UnityEngine;

namespace Nocturne;

internal static class NocturneInvisible
{
    internal static bool On => NocturneConfig.Invisible != null && NocturneConfig.Invisible.Value;

    [HarmonyPatch(typeof(CustomNetworkTransform), "FixedUpdate")]
    private static class Cnt
    {
        private static bool Prefix(CustomNetworkTransform __instance)
        {
            if (!On || __instance == null) return true;
            if (!((InnerNetObject)__instance).AmOwner || __instance.myPlayer != PlayerControl.LocalPlayer) return true;
            if (MeetingHud.Instance != null) return true;

            try
            {
                ushort seq = (ushort)(__instance.lastSequenceId + 1);
                __instance.lastSequenceId = seq;
                MessageWriter w = AmongUsClient.Instance.StartRpcImmediately(((InnerNetObject)__instance).NetId, 21, SendOption.Reliable, -1);
                NetHelpers.WriteVector2(new Vector2(454f, 454f), w);
                w.Write(seq);
                AmongUsClient.Instance.FinishRpcImmediately(w);
            }
            catch { }
            return false;
        }
    }
}
