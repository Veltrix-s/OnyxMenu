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

    internal static bool On => NocturneConfig.GodMode.Value;

    private static ushort _seq;

    public void Update()
    {
        bool on = On;
        if (on == _last)
            return;

        PlayerControl me = PlayerControl.LocalPlayer;
        if (me == null)
            return;
        if (on && me.inVent)
            return;

        _last = on;
        Send(on);
    }

    internal static void Reenter()
    {
        if (!On)
            return;
        PlayerControl me = PlayerControl.LocalPlayer;
        if (me == null || me.Data == null || me.Data.IsDead)
            return;
        Send(true);
    }

    private static void Send(bool enter)
    {
        try
        {
            PlayerControl me = PlayerControl.LocalPlayer;
            var net = (InnerNetClient)AmongUsClient.Instance;
            if (me == null || me.MyPhysics == null || net == null || ShipStatus.Instance == null)
                return;

            if (net.AmHost)
            {
                if (enter)
                    me.MyPhysics.RpcEnterVent(VentId);
                else
                    me.MyPhysics.RpcExitVent(VentId);
                return;
            }

            MessageWriter body = null;
            try
            {
                body = MessageWriter.Get(SendOption.Reliable);
                body.Write(NextSeq());
                body.Write((byte)(enter ? 2 : 3));
                body.Write((byte)VentId);

                MessageWriter w = net.StartRpcImmediately(((InnerNetObject)ShipStatus.Instance).NetId, 35, SendOption.Reliable, net.HostId);
                if (w == null)
                    return;
                w.Write((byte)SystemTypes.Ventilation);
                w.WriteNetObject(me);
                w.Write(body, false);
                net.FinishRpcImmediately(w);
            }
            finally
            {
                try { body?.Recycle(); } catch { }
            }
        }
        catch { }
    }

    private static ushort NextSeq()
    {
        if (_seq < 10000)
            _seq = 10000;
        _seq++;
        return _seq;
    }

    private const byte GrantVent = 231;
    private static readonly HashSet<byte> _granted = new HashSet<byte>();

    internal static bool IsGranted(byte pid) => _granted.Contains(pid);

    internal static string Toggle(PlayerControl target)
    {
        if (target == null || target.Data == null)
            return NocturneText.T("нет цели", "no target");
        if (AmongUsClient.Instance == null || ShipStatus.Instance == null)
            return NocturneText.T("только в матче", "in-match only");
        if (target == PlayerControl.LocalPlayer)
            return NocturneText.T("это ты", "that is you");

        bool on = !_granted.Contains(target.PlayerId);
        if (!Push(target, on ? 2 : 3))
            return NocturneText.T("не удалось", "failed");

        if (on)
            _granted.Add(target.PlayerId);
        else
            _granted.Remove(target.PlayerId);
        return on ? NocturneText.T("бессмертие выдано", "immortality granted") : NocturneText.T("бессмертие снято", "immortality removed");
    }

    internal static void Forget()
    {
        _granted.Clear();
        _seq = 0;
    }

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
            if (w == null)
                return false;
            w.Write((byte)SystemTypes.Ventilation);
            w.WriteNetObject(target);
            w.Write(body, false);
            net.FinishRpcImmediately(w);
            return true;
        }
        catch
        {
            return false;
        }
        finally { try { body?.Recycle(); } catch { } }
    }

    [HarmonyPatch(typeof(VentilationSystem), nameof(VentilationSystem.Update))]
    private static class Block
    {
        private static bool Prefix(VentilationSystem.Operation __0, int __1)
        {
            if (!On || __1 == VentId)
                return true;
            int op = (int)__0;
            return !(op == 2 || op == 3 || op == 4);
        }
    }

    [HarmonyPatch(typeof(VentilationSystem), nameof(VentilationSystem.Update))]
    private static class HushVent
    {
        private static Exception Finalizer(Exception __exception) => On ? null : __exception;
    }

    [HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
    private static class HushHud
    {
        private static Exception Finalizer(Exception __exception) => On ? null : __exception;
    }

    [HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.OnEnable))]
    private static class OnShip
    {
        private static void Postfix() => Reenter();
    }

    [HarmonyPatch(typeof(LobbyBehaviour), nameof(LobbyBehaviour.Start))]
    private static class OnLobby
    {
        private static void Postfix() => Forget();
    }

    [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Close))]
    private static class OnClose
    {
        private static void Postfix() => Reenter();
    }
}
