using System;
using System.Collections.Generic;
using HarmonyLib;
using Hazel;
using InnerNet;
using UnityEngine;

namespace Nocturne;

public sealed class NocturneGodMode : MonoBehaviour
{
    private const int VentId = 50;
    private bool _last;

    internal static bool On => NocturneConfig.GodMode != null && NocturneConfig.GodMode.Value;

    public void Update()
    {
        bool on = On;
        if (on == _last) return;

        PlayerControl me = PlayerControl.LocalPlayer;
        if (me == null) return;
        if (on && me.inVent) return;

        _last = on;
        Send(on ? 2 : 3);
    }

    internal static void Reenter()
    {
        if (!On) return;
        PlayerControl me = PlayerControl.LocalPlayer;
        if (me == null || me.Data == null || me.Data.IsDead) return;
        Send(2);
    }

    private static void Send(int op)
    {
        try { VentilationSystem.Update((VentilationSystem.Operation)op, VentId); }
        catch { }
    }

    private const byte GrantVent = 231;
    private static readonly HashSet<byte> _granted = new HashSet<byte>();

    internal static bool IsGranted(byte pid) => _granted.Contains(pid);

    internal static string Toggle(PlayerControl target)
    {
        if (target == null || target.Data == null) return NocturneText.T("нет цели", "no target");
        if (AmongUsClient.Instance == null || ShipStatus.Instance == null) return NocturneText.T("только в матче", "in-match only");
        if (target == PlayerControl.LocalPlayer) return NocturneText.T("это ты", "that is you");

        bool on = !_granted.Contains(target.PlayerId);
        if (!Push(target, on ? 2 : 3)) return NocturneText.T("не удалось", "failed");

        if (on) _granted.Add(target.PlayerId);
        else _granted.Remove(target.PlayerId);
        return on ? NocturneText.T("бессмертие выдано", "immortality granted") : NocturneText.T("бессмертие снято", "immortality removed");
    }

    internal static void Forget() => _granted.Clear();

    private static bool Push(PlayerControl target, int op)
    {
        MessageWriter body = null;
        try
        {
            var net = (InnerNetClient)AmongUsClient.Instance;
            body = MessageWriter.Get(SendOption.Reliable);
            body.Write((ushort)0);
            body.Write((byte)op);
            body.Write(GrantVent);

            MessageWriter w = net.StartRpcImmediately(((InnerNetObject)ShipStatus.Instance).NetId, 35, SendOption.Reliable, net.HostId);
            if (w == null) return false;
            w.Write((byte)SystemTypes.Ventilation);
            w.WriteNetObject(target);
            w.Write(body, false);
            net.FinishRpcImmediately(w);
            return true;
        }
        catch { return false; }
        finally { try { body?.Recycle(); } catch { } }
    }

    [HarmonyPatch(typeof(VentilationSystem), "Update")]
    private static class Block
    {
        private static bool Prefix(VentilationSystem.Operation __0, int __1)
        {
            if (!On || __1 == VentId) return true;
            int op = (int)__0;
            return !(op == 2 || op == 3 || op == 4);
        }
    }

    [HarmonyPatch(typeof(VentilationSystem), "Update")]
    private static class HushVent
    {
        private static Exception Finalizer(Exception __exception) => On ? null : __exception;
    }

    [HarmonyPatch(typeof(HudManager), "Update")]
    private static class HushHud
    {
        private static Exception Finalizer(Exception __exception) => On ? null : __exception;
    }

    [HarmonyPatch(typeof(ShipStatus), "OnEnable")]
    private static class OnShip
    {
        private static void Postfix() => Reenter();
    }

    [HarmonyPatch(typeof(LobbyBehaviour), nameof(LobbyBehaviour.Start))]
    private static class OnLobby
    {
        private static void Postfix() => Forget();
    }

    [HarmonyPatch(typeof(MeetingHud), "Close")]
    private static class OnClose
    {
        private static void Postfix() => Reenter();
    }
}
