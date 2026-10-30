using HarmonyLib;
using Hazel;
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

[HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.HandleRpc))]
internal static class VentPacketGuard
{
    public static bool Prefix([HarmonyArgument(0)] byte callId, [HarmonyArgument(1)] MessageReader reader)
    {
        if (callId != (byte)RpcCalls.UpdateSystem || reader == null)
            return true;
        if (!NocturneConfig.BlockVentPackets.Value || AmongUsClient.Instance == null)
            return true;
        if (AmongUsClient.Instance.AmHost) return true;

        int pos = reader.Position;
        PlayerControl actor = null;
        int op = -1;
        bool vent = false;
        try
        {
            vent = (SystemTypes)reader.ReadByte() == SystemTypes.Ventilation;
            if (vent)
            {
                actor = reader.ReadNetObject<PlayerControl>();
                reader.ReadUInt16();
                op = reader.ReadByte();
            }
        }
        catch { }
        finally { try { reader.Position = pos; } catch { } }

        if (!vent) return true;

        if (op == (int)VentilationSystem.Operation.BootImpostors)
        {
            string who = actor != null && actor.Data != null ? actor.Data.PlayerName : "?";
            TeleportGuard.Notify(NocturneText.T("Вент-кик от ", "Vent kick from ") + who);
        }
        return false;
    }
}
