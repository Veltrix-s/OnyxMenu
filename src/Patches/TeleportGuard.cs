using HarmonyLib;
using InnerNet;
using UnityEngine;

namespace Nocturne.Patches;

internal static class TeleportGuard
{
    private static float _ownUntil;
    private static float _lastToast;

    internal static void AllowOwnBoot() => _ownUntil = Time.unscaledTime + 3f;

    internal static bool TakeOwnBoot()
    {
        if (Time.unscaledTime >= _ownUntil)
            return false;

        _ownUntil = 0f;
        return true;
    }

    internal static void Notify(string text)
    {
        float now = Time.unscaledTime;
        if (now - _lastToast < 10f)
            return;

        _lastToast = now;
        NocturneToast.Push(NocturneText.T("Защита", "Protection"), text, 2.5f, NocturneNotifyKind.Warning);
    }
}

[HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.BootFromVent))]
internal static class BootFromVentGuard
{
    public static bool Prefix(PlayerPhysics __instance, [HarmonyArgument(0)] int ventId)
    {
        if (!NocturneConfig.VentTpProtect.Value)
            return true;

        PlayerControl pc = __instance.myPlayer;
        if (pc == null || !pc.AmOwner)
            return true;
        if (TeleportGuard.TakeOwnBoot()) return true;

        Vent cur = Vent.currentVent;
        if (pc.inVent && (cur == null || cur.Id == ventId))
            return true;

        TeleportGuard.Notify(NocturneText.T("Вент-ТП заблокирован", "Vent TP blocked"));
        return false;
    }
}

[HarmonyPatch(typeof(CustomNetworkTransform), nameof(CustomNetworkTransform.HandleRpc))]
internal static class SnapToGuard
{
    public static bool Prefix(CustomNetworkTransform __instance, [HarmonyArgument(0)] byte callId)
    {
        if (callId != (byte)RpcCalls.SnapTo)
            return true;
        if (!NocturneConfig.VentTpProtect.Value) return true;

        PlayerControl pc = __instance.myPlayer;
        if (pc == null || !pc.AmOwner) return true;

        TeleportGuard.Notify(NocturneText.T("Форс-телепорт заблокирован", "Forced teleport blocked"));
        return false;
    }
}
