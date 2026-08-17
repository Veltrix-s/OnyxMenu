using HarmonyLib;
using Hazel;
using InnerNet;
using UnityEngine;

namespace Nocturne.Patches;

internal static class NocturneVentTpProtect
{
    private static float _lastToast;

    internal static bool On()
    {
        if (AmongUsClient.Instance == null) return false;
        return NocturneConfig.VentTpProtect.Value;
    }

    internal static void Notify(string who)
    {
        float now = Time.unscaledTime;
        if (now - _lastToast < 2f) return;
        _lastToast = now;
        NocturneToast.Push(NocturneText.T("Защита", "Protection"),
            NocturneText.T("Вент-ТП заблокирован: ", "Vent TP blocked: ") + who, 2.5f, NocturneNotifyKind.Warning);
    }

    internal static string Name(PlayerControl pc)
        => pc != null && pc.Data != null && pc.Data.PlayerName != null ? pc.Data.PlayerName : "?";
}

[HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.HandleRpc))]
internal static class NocturneVentTpProtectPhysics
{
    private static bool Prefix(PlayerPhysics __instance, [HarmonyArgument(0)] byte callId)
    {
        try
        {
            if (__instance == null || callId != (byte)RpcCalls.BootFromVent) return true;
            if (!NocturneConfig.VentTpProtect.Value || AmongUsClient.Instance == null) return true;
            NocturneVentTpProtect.Notify(NocturneText.T("выбивание из вента", "vent boot"));
            return false;
        }
        catch { return true; }
    }
}

[HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.HandleRpc))]
internal static class NocturneVentTpProtectShip
{
    private static bool Prefix([HarmonyArgument(0)] byte callId, [HarmonyArgument(1)] MessageReader reader)
    {
        try
        {
            if (callId != (byte)RpcCalls.UpdateSystem || reader == null) return true;
            if (!NocturneVentTpProtect.On()) return true;

            int pos = reader.Position;
            PlayerControl actor = null;
            bool block = false;
            try
            {
                int sys = reader.ReadByte();
                if (sys != (int)SystemTypes.Ventilation) return true;
                actor = reader.ReadNetObject<PlayerControl>();
                reader.ReadUInt16();
                int op = reader.Position < reader.Length ? reader.ReadByte() : -1;
                if (op == 1 || op == 2) block = true;
            }
            finally { try { reader.Position = pos; } catch { } }

            if (!block) return true;
            NocturneVentTpProtect.Notify(NocturneVentTpProtect.Name(actor));
            return false;
        }
        catch { return true; }
    }
}
