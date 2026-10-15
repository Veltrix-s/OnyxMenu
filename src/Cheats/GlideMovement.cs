using HarmonyLib;
using UnityEngine;

namespace Nocturne.Patches;

internal static class GlideMovement
{
    private const float MinShift = 0.01f;

    private static bool _muteSnap;
    private static Vector2 _last;

    internal static bool MuteSnap => _muteSnap;

    internal static void Send(CustomNetworkTransform nt)
    {
        PlayerControl me = PlayerControl.LocalPlayer;
        if (me == null || me.Data == null || me.NetTransform != nt) return;

        Vector2 pos = nt.transform.position;
        if (Vector2.Distance(pos, _last) <= MinShift) return;

        _last = pos;
        _muteSnap = true;
        try
        {
            nt.RpcSnapTo(pos);
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
        if (!NocturneConfig.WalkNoAnim.Value || !__instance.AmOwner)
            return HarmonyControl.Continue;

        GlideMovement.Send(__instance);
        return HarmonyControl.SkipOriginal;
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
