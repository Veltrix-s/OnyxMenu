using System.Collections.Generic;
using Hazel;
using HarmonyLib;
using InnerNet;
using UnityEngine;

namespace Nocturne.Patches;

internal static class NocturneRpcGuard
{
    private static readonly Dictionary<int, float> Hit = new Dictionary<int, float>();

    internal static readonly HashSet<int> SabSystems = new HashSet<int>
    {
        (int)SystemTypes.Reactor, (int)SystemTypes.LifeSupp, (int)SystemTypes.Comms,
        (int)SystemTypes.HeliSabotage, (int)SystemTypes.Laboratory, (int)SystemTypes.MushroomMixupSabotage
    };

    internal static bool On()
    {
        if (NocturneConfig.RpcGuard == null || !NocturneConfig.RpcGuard.Value || AmongUsClient.Instance == null) return false;
        try { return ((InnerNetClient)AmongUsClient.Instance).AmHost; }
        catch { return false; }
    }

    internal static bool IsImp(PlayerControl pc)
    {
        try { return pc != null && pc.Data != null && pc.Data.Role != null && pc.Data.Role.IsImpostor; }
        catch { return false; }
    }

    internal static bool IsHS()
    {
        try { return GameManager.Instance != null && GameManager.Instance.IsHideAndSeek(); }
        catch { return false; }
    }

    internal static bool RealVent(int id)
    {
        try
        {
            ShipStatus s = ShipStatus.Instance;
            if (s == null || s.AllVents == null) return true;
            foreach (Vent v in s.AllVents) if (v != null && v.Id == id) return true;
            return false;
        }
        catch { return true; }
    }

    internal static void Flag(PlayerControl actor, string reason)
    {
        try
        {
            if (actor == null || actor == PlayerControl.LocalPlayer) return;
            var net = (InnerNetClient)AmongUsClient.Instance;
            if (net == null || !net.AmHost) return;
            int cid = actor.OwnerId;
            if (cid < 0 || cid == net.ClientId || cid == net.HostId) return;
            if (Hit.TryGetValue(cid, out float t) && Time.realtimeSinceStartup - t < 6f) return;
            Hit[cid] = Time.realtimeSinceStartup;
            NocturneAccess.Act(net, cid, NocturneConfig.RpcGuardAction.Value, NocturneAccess.ClientName(net, cid), reason);
        }
        catch { }
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.HandleRpc))]
internal static class NocturneRpcGuardPlayer
{
    private static bool Prefix(PlayerControl __instance, [HarmonyArgument(0)] byte callId, [HarmonyArgument(1)] MessageReader reader)
    {
        if (!NocturneRpcGuard.On() || __instance == null || __instance == PlayerControl.LocalPlayer || __instance.Data == null || reader == null) return true;

        int pos = reader.Position;
        bool cheat = false;
        string reason = null;
        try
        {
            if (callId == (byte)RpcCalls.SetScanner)
            {
                if (reader.ReadBoolean() && NocturneRpcGuard.IsImp(__instance)) { cheat = true; reason = NocturneText.T("скан импостером", "scan by impostor"); }
            }
            else if (callId == (byte)RpcCalls.PlayAnimation)
            {
                reader.ReadByte();
                if (NocturneRpcGuard.IsImp(__instance)) { cheat = true; reason = NocturneText.T("таск-анимация импостером", "task anim by impostor"); }
            }
            else if (callId == (byte)RpcCalls.ReportDeadBody && NocturneRpcGuard.IsHS())
            {
                cheat = true;
                reason = NocturneText.T("репорт в H&S", "report in H&S");
            }
        }
        catch { }
        finally { try { reader.Position = pos; } catch { } }

        if (cheat) { NocturneRpcGuard.Flag(__instance, reason); return false; }
        return true;
    }
}

[HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.HandleRpc))]
internal static class NocturneRpcGuardVent
{
    private static bool Prefix(PlayerPhysics __instance, [HarmonyArgument(0)] byte callId, [HarmonyArgument(1)] MessageReader reader)
    {
        if (!NocturneRpcGuard.On() || __instance == null || reader == null) return true;
        if (callId != (byte)RpcCalls.EnterVent && callId != (byte)RpcCalls.ExitVent) return true;

        PlayerControl actor = __instance.myPlayer;
        if (actor == null || actor == PlayerControl.LocalPlayer || actor.Data == null) return true;

        int pos = reader.Position;
        bool cheat = false;
        string reason = null;
        try
        {
            int ventId = reader.ReadPackedInt32();
            if (!actor.Data.IsDead && actor.Data.Role != null && !actor.Data.Role.CanVent) { cheat = true; reason = NocturneText.T("вент без права", "vent without permission"); }
            else if (!NocturneRpcGuard.RealVent(ventId)) { cheat = true; reason = NocturneText.T("фейк vent id", "fake vent id"); }
        }
        catch { }
        finally { try { reader.Position = pos; } catch { } }

        if (cheat) { NocturneRpcGuard.Flag(actor, reason); return false; }
        return true;
    }
}

[HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.HandleRpc))]
internal static class NocturneRpcGuardShip
{
    private static bool Prefix(ShipStatus __instance, [HarmonyArgument(0)] byte callId, [HarmonyArgument(1)] MessageReader reader)
    {
        if (!NocturneRpcGuard.On() || __instance == null || reader == null) return true;

        int pos = reader.Position;
        bool block = false;
        PlayerControl actor = null;
        string reason = null;
        try
        {
            if (callId == (byte)RpcCalls.CloseDoorsOfType)
            {
                if (NocturneRpcGuard.IsHS()) { block = true; reason = NocturneText.T("двери в H&S", "doors in H&S"); }
            }
            else if (callId == (byte)RpcCalls.UpdateSystem)
            {
                int sys = reader.ReadByte();
                actor = reader.ReadNetObject<PlayerControl>();

                if (sys == (int)SystemTypes.Ventilation)
                {
                    reader.ReadUInt16();
                    int op = reader.Position < reader.Length ? reader.ReadByte() : -1;
                    int vid = reader.Position < reader.Length ? reader.ReadByte() : -1;

                    if (op == 2 || op == 5)
                    {
                        if (!NocturneRpcGuard.RealVent(vid)) { block = true; reason = NocturneText.T("фейк vent id", "fake vent id"); }
                        else if (actor != null && actor != PlayerControl.LocalPlayer && actor.Data != null
                            && !actor.Data.IsDead && actor.Data.Role != null && !actor.Data.Role.CanVent)
                        {
                            block = true;
                            reason = NocturneText.T("вент за чужого", "vent for another");
                        }
                    }
                }
                else if (NocturneRpcGuard.SabSystems.Contains(sys))
                {
                    int amount = reader.Position < reader.Length ? reader.ReadByte() : 0;

                    if (__instance.Systems != null && !__instance.Systems.ContainsKey((SystemTypes)sys))
                    {
                        block = true;
                        reason = NocturneText.T("саботаж не по карте", "sabotage off-map");
                    }
                    else if (actor != null && actor != PlayerControl.LocalPlayer && actor.Data != null && !actor.Data.IsDead && !NocturneRpcGuard.IsImp(actor)
                        && (sys == (int)SystemTypes.MushroomMixupSabotage || (amount & 0x80) != 0))
                    {
                        block = true;
                        reason = NocturneText.T("саботаж мирным", "sabotage by crew");
                    }
                }
            }
        }
        catch { }
        finally { try { reader.Position = pos; } catch { } }

        if (block)
        {
            if (actor != null) NocturneRpcGuard.Flag(actor, reason);
            else NocturneToast.Push(NocturneText.T("Защита", "Protection"), reason, 2.5f, NocturneNotifyKind.Warning);
            return false;
        }
        return true;
    }
}
