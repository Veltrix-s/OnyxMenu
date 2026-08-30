using HarmonyLib;
using UnityEngine;

namespace Nocturne.Patches;

internal static class GlideMovement
{
    private const float SendStep = 0.02f;
    private const float MinShift = 0.01f;

    private static bool _muteSnap;
    private static Vector2 _last;
    private static float _acc;

    internal static bool MuteSnap => _muteSnap;

    internal static bool On()
    {
        PlayerControl me = PlayerControl.LocalPlayer;
        return NocturneConfig.WalkNoAnim.Value
            && me != null && me.Data != null && me.NetTransform != null;
    }

    internal static void Tick()
    {
        if (!On())
            return;

        _acc += Time.deltaTime;
        if (_acc < SendStep)
            return;
        _acc = 0f;

        PlayerControl me = PlayerControl.LocalPlayer;
        Vector2 pos = ((Component)me).transform.position;
        if (Vector2.Distance(pos, _last) <= MinShift)
            return;

        _last = pos;
        _muteSnap = true;
        try
        {
            me.NetTransform.RpcSnapTo(pos);
        }
        catch { }
        _muteSnap = false;
    }
}

[HarmonyPatch(typeof(CustomNetworkTransform), nameof(CustomNetworkTransform.FixedUpdate))]
internal static class GlideMovementNetPatch
{
    public static bool Prefix(CustomNetworkTransform __instance)
    {
        if (__instance != null && __instance.AmOwner && NocturneConfig.WalkNoAnim.Value)
            return HarmonyControl.SkipOriginal;
        return HarmonyControl.Continue;
    }
}

[HarmonyPatch(typeof(CustomNetworkTransform), nameof(CustomNetworkTransform.SnapTo), new System.Type[] { typeof(Vector2), typeof(ushort) })]
internal static class GlideMovementSnapPatch
{
    public static bool Prefix(CustomNetworkTransform __instance, ushort __1)
    {
        if (GlideMovement.MuteSnap && __instance != null && __instance.AmOwner)
        {
            __instance.lastSequenceId = __1;
            return HarmonyControl.SkipOriginal;
        }
        return HarmonyControl.Continue;
    }
}
