using HarmonyLib;
using Hazel;
using InnerNet;
using UnityEngine;

namespace Nocturne;

internal static class NocturneInvisible
{
    internal static bool On => NocturneConfig.Invisible.Value;

    private static bool _was;

    internal static void Tick()
    {
        bool on = On;
        if (on == _was) return;
        _was = on;

        if (!NocturneConfig.InvisiblePoof.Value) return;

        PlayerControl me = PlayerControl.LocalPlayer;
        if (me == null) return;

        if (on)
            PhantomPoof.Vanish(me);
        else
            PhantomPoof.Appear(me);
    }

    [HarmonyPatch(typeof(CustomNetworkTransform), nameof(CustomNetworkTransform.FixedUpdate))]
    private static class Cnt
    {
        private static bool Prefix(CustomNetworkTransform __instance)
        {
            if (!On || __instance == null) return true;
            if (!((InnerNetObject)__instance).AmOwner || __instance.myPlayer != PlayerControl.LocalPlayer)
                return true;
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
