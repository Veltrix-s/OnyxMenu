using System.Collections.Generic;
using Hazel;
using InnerNet;
using UnityEngine;

namespace Nocturne;

internal static class NocturneVentTp
{
    private static readonly Dictionary<byte, ushort> _seq = new Dictionary<byte, ushort>();
    private static readonly HashSet<byte> _marked = new HashSet<byte>();

    internal static int Vent;

    internal static int VentCount()
    {
        try { return ShipStatus.Instance != null ? ShipStatus.Instance.AllVents.Count : 0; }
        catch { return 0; }
    }

    internal static bool IsMarked(byte pid) => _marked.Contains(pid);
    internal static void ToggleMark(byte pid) { if (!_marked.Remove(pid)) _marked.Add(pid); }
    internal static int MarkedCount => _marked.Count;

    internal static void MarkAll()
    {
        try
        {
            var e = PlayerControl.AllPlayerControls.GetEnumerator();
            while (e.MoveNext())
            {
                PlayerControl p = e.Current;
                if (p == null || p.Data == null || p.Data.Disconnected) continue;
                if (p == PlayerControl.LocalPlayer) continue;
                _marked.Add(p.PlayerId);
            }
        }
        catch { }
    }

    internal static string KickAllFromVents()
    {
        if (AmongUsClient.Instance == null || ShipStatus.Instance == null) return NocturneText.T("Только в матче.", "In match only.");
        if (!AmongUsClient.Instance.AmHost) return NocturneText.T("Только хост.", "Host only.");
        if (MeetingHud.Instance != null) return NocturneText.T("Не в собрании.", "Not in a meeting.");

        int n = 0;
        try
        {
            var e = PlayerControl.AllPlayerControls.GetEnumerator();
            while (e.MoveNext())
            {
                PlayerControl p = e.Current;
                if (p == null || p.Data == null || p.Data.Disconnected || p.Data.IsDead) continue;
                if (p == PlayerControl.LocalPlayer || !p.inVent) continue;
                int idx = NearestVent(p.GetTruePosition());
                if (idx < 0) continue;
                BootOnly(p, idx);
                n++;
            }
        }
        catch { }
        return n > 0 ? NocturneText.T("Выкинуто из вентов: ", "Kicked from vents: ") + n : NocturneText.T("Никого нет в вентах.", "Nobody in vents.");
    }

    private static int NearestVent(Vector2 pos)
    {
        int best = -1;
        float bd = float.MaxValue;
        try
        {
            var vents = ShipStatus.Instance.AllVents;
            if (vents == null) return -1;
            for (int i = 0; i < vents.Count; i++)
            {
                Vent v = vents[i];
                if (v == null) continue;
                float d = Vector2.Distance(pos, v.transform.position);
                if (d < bd) { bd = d; best = i; }
            }
        }
        catch { }
        return best;
    }

    internal static void ClearMarks() => _marked.Clear();

    internal static void Reset()
    {
        _marked.Clear();
        _seq.Clear();
        _lastAuto = -99f;
    }

    internal static string CycleVent(int dir)
    {
        int c = VentCount();
        if (c <= 0) return NocturneText.T("нет вентов", "no vents");
        Vent = ((Vent + dir) % c + c) % c;
        return NocturneText.T("вент " + Vent, "vent " + Vent);
    }

    private static float _lastTp = -99f;
    private static float _lastAuto = -99f;

    internal static string SendMarked()
    {
        if (AmongUsClient.Instance == null || ShipStatus.Instance == null) return NocturneText.T("только в матче", "in-match only");
        if (_marked.Count == 0) return NocturneText.T("нет отмеченных", "no marked players");

        float now = Time.unscaledTime;
        if (now - _lastTp < 0.2f) return string.Empty;
        _lastTp = now;

        if (VentCount() <= 0) return NocturneText.T("нет вентов", "no vents");
        int n = Scatter();
        return NocturneText.T($"раскидано: {n}", $"scattered: {n}");
    }

    internal static void AutoTick()
    {
        if (!NocturneConfig.VentTpAuto.Value) return;
        if (AmongUsClient.Instance == null || ShipStatus.Instance == null || _marked.Count == 0) return;
        if (VentCount() <= 0) return;

        float now = Time.unscaledTime;
        float delay = Mathf.Clamp(NocturneConfig.VentTpAutoDelay.Value, 0.3f, 10f);
        if (now - _lastAuto < delay) return;
        _lastAuto = now;

        Scatter();
        int c = VentCount();
        if (c > 0) Vent = (Vent + 1) % c;
    }

    private static int Scatter()
    {
        int count = VentCount();
        if (count <= 0) return 0;

        int n = 0, idx = 0;
        try
        {
            var e = PlayerControl.AllPlayerControls.GetEnumerator();
            while (e.MoveNext())
            {
                PlayerControl p = e.Current;
                if (p == null || p.Data == null || p.Data.Disconnected || p.Data.IsDead) continue;
                if (!_marked.Contains(p.PlayerId)) continue;
                int vent = ((Vent + idx) % count + count) % count;
                if (Send(p, vent).StartsWith(NocturneText.T("в венте", "vent"))) n++;
                idx++;
            }
        }
        catch { }
        return n;
    }

    internal static string Send(PlayerControl target, int ventId)
    {
        if (target == null || target.Data == null) return NocturneText.T("нет цели", "no target");
        if (AmongUsClient.Instance == null || ShipStatus.Instance == null) return NocturneText.T("только в матче", "in-match only");
        if (target.Data.IsDead) return NocturneText.T("цель мертва", "target is dead");

        int count = VentCount();
        if (count <= 0) return NocturneText.T("нет вентов", "no vents");
        byte vent = (byte)Mathf.Clamp(ventId, 0, count - 1);

        try
        {
            var net = (InnerNetClient)AmongUsClient.Instance;
            if (net.AmHost)
            {
                target.MyPhysics.RpcBootFromVent(vent);
                return NocturneText.T("в венте " + vent, "vent " + vent);
            }

            return Batch(net, target, vent) ? NocturneText.T("в венте " + vent, "vent " + vent) : NocturneText.T("не удалось", "failed");
        }
        catch { return NocturneText.T("не удалось", "failed"); }
    }

    internal static string BootOnly(PlayerControl target, int ventId)
    {
        if (target == null || target.Data == null) return NocturneText.T("нет цели", "no target");
        if (AmongUsClient.Instance == null || ShipStatus.Instance == null) return NocturneText.T("только в матче", "in-match only");

        int count = VentCount();
        if (count <= 0) return NocturneText.T("нет вентов", "no vents");
        byte vent = (byte)Mathf.Clamp(ventId, 0, count - 1);

        try
        {
            var net = (InnerNetClient)AmongUsClient.Instance;
            if (net.AmHost)
            {
                target.MyPhysics.RpcBootFromVent(vent);
                return NocturneText.T("выкинут", "booted");
            }

            MessageWriter boot = null, w = null;
            try
            {
                boot = MessageWriter.Get(SendOption.None);
                boot.Write(Next(target.PlayerId));
                boot.Write((byte)5);
                boot.Write(vent);

                w = MessageWriter.Get(SendOption.Reliable);
                w.StartMessage(6);
                w.Write(net.GameId);
                w.WritePacked(net.HostId);
                Sub(w, target, boot);
                w.EndMessage();
                net.SendOrDisconnect(w);
                return NocturneText.T("выкинут", "booted");
            }
            finally
            {
                try { boot?.Recycle(); } catch { }
                try { w?.Recycle(); } catch { }
            }
        }
        catch { return NocturneText.T("не удалось", "failed"); }
    }

    private static ushort Next(byte pid)
    {
        _seq.TryGetValue(pid, out ushort v);
        if (v < 10000) v = 10000;
        v++;
        _seq[pid] = v;
        return v;
    }

    private static bool Batch(InnerNetClient net, PlayerControl target, byte vent)
    {
        MessageWriter enter = null, boot = null, w = null;
        try
        {
            enter = MessageWriter.Get(SendOption.None);
            enter.Write(Next(target.PlayerId));
            enter.Write((byte)2);
            enter.Write(vent);

            boot = MessageWriter.Get(SendOption.None);
            boot.Write(Next(target.PlayerId));
            boot.Write((byte)5);
            boot.Write(vent);

            w = MessageWriter.Get(SendOption.Reliable);
            w.StartMessage(6);
            w.Write(net.GameId);
            w.WritePacked(net.HostId);

            Sub(w, target, enter);
            Sub(w, target, boot);

            w.EndMessage();
            net.SendOrDisconnect(w);
            return true;
        }
        catch { return false; }
        finally
        {
            try { enter?.Recycle(); } catch { }
            try { boot?.Recycle(); } catch { }
            try { w?.Recycle(); } catch { }
        }
    }

    private static void Sub(MessageWriter w, PlayerControl target, MessageWriter body)
    {
        w.StartMessage(2);
        w.WritePacked(((InnerNetObject)ShipStatus.Instance).NetId);
        w.Write((byte)35);
        w.Write((byte)SystemTypes.Ventilation);
        w.WriteNetObject(target);
        w.Write(body, false);
        w.EndMessage();
    }
}

[HarmonyLib.HarmonyPatch(typeof(LobbyBehaviour), nameof(LobbyBehaviour.Start))]
internal static class NocturneVentTpReset
{
    public static void Postfix() => NocturneVentTp.Reset();
}
