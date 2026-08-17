using System.Collections.Generic;
using Hazel;
using InnerNet;
using UnityEngine;

namespace Nocturne;

internal static class NocturneVentKick
{
    private const float StepDelay = 0.2f;

    private static readonly Dictionary<byte, ushort> _seq = new Dictionary<byte, ushort>();
    private static readonly HashSet<byte> _selected = new HashSet<byte>();
    private static readonly Queue<byte> _pending = new Queue<byte>();
    private static float _nextAt;

    internal static int SelectedCount => _selected.Count;
    internal static int PendingCount => _pending.Count;
    internal static bool IsSelected(byte pid) => _selected.Contains(pid);
    internal static void ToggleSelect(byte pid) { if (!_selected.Remove(pid)) _selected.Add(pid); }
    internal static void ClearSelection() => _selected.Clear();

    internal static void SelectAll()
    {
        try
        {
            var net = AmongUsClient.Instance != null ? (InnerNetClient)AmongUsClient.Instance : null;
            foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
            {
                if (pc == null || pc.Data == null || pc.Data.Disconnected) continue;
                if (pc == PlayerControl.LocalPlayer) continue;
                if (net != null && pc.OwnerId == net.HostId) continue;
                _selected.Add(pc.PlayerId);
            }
        }
        catch { }
    }

    internal static string KickSelected()
    {
        if (_selected.Count == 0) return NocturneText.T("никто не отмечен", "nobody selected");
        int total = _selected.Count;
        foreach (byte pid in _selected) _pending.Enqueue(pid);
        _selected.Clear();
        return NocturneText.T($"в очереди: {total}", $"queued: {total}");
    }

    internal static void Tick()
    {
        if (_pending.Count == 0) return;
        float now = Time.unscaledTime;
        if (now < _nextAt) return;
        _nextAt = now + StepDelay;

        byte pid = _pending.Dequeue();
        PlayerControl pc = ById(pid);
        if (pc == null || pc.Data == null || pc.Data.Disconnected) return;
        NocturneToast.Push(NocturneText.T("Вент кик", "Vent kick"), Kick(pc), 2.4f, NocturneNotifyKind.Info);
    }

    private static PlayerControl ById(byte pid)
    {
        try
        {
            var e = PlayerControl.AllPlayerControls.GetEnumerator();
            while (e.MoveNext())
                if (e.Current != null && e.Current.PlayerId == pid) return e.Current;
        }
        catch { }
        return null;
    }

    internal static void Reset()
    {
        _seq.Clear();
        _selected.Clear();
        _pending.Clear();
        _nextAt = 0f;
    }

    private static ushort Next(byte pid)
    {
        _seq.TryGetValue(pid, out ushort v);
        if (v < 10000) v = 10000;
        v++;
        _seq[pid] = v;
        return v;
    }

    private static bool HasAnticheat()
    {
        try { return AmongUsClient.Instance != null && (int)AmongUsClient.Instance.NetworkMode == 1; }
        catch { return false; }
    }

    internal static string Kick(PlayerControl target)
    {
        if (target == null || target.Data == null || target.Data.Disconnected) return NocturneText.T("нет цели", "no target");
        if (target == PlayerControl.LocalPlayer) return NocturneText.T("это ты", "that is you");
        if (AmongUsClient.Instance == null || ShipStatus.Instance == null) return NocturneText.T("только в матче", "in-match only");

        var net = (InnerNetClient)AmongUsClient.Instance;
        if (target.OwnerId == net.HostId) return NocturneText.T("хоста так не выкинуть", "can't kick the host this way");

        if (net.AmHost)
        {
            net.KickPlayer(target.OwnerId, false);
            return NocturneText.T("кикнут: ", "kicked: ") + target.Data.PlayerName;
        }

        if (!HasAnticheat()) return NocturneText.T("нужен официальный сервер", "needs an official server");

        return Send(net, target) ? NocturneText.T("кикнут: ", "kicked: ") + target.Data.PlayerName : NocturneText.T("не удалось", "failed");
    }

    private static bool Push(InnerNetClient net, PlayerControl target, byte op)
    {
        MessageWriter body = null, w = null;
        try
        {
            body = MessageWriter.Get(SendOption.None);
            body.Write(Next(target.PlayerId));
            body.Write(op);
            body.Write((byte)0);

            w = net.StartRpcImmediately(((InnerNetObject)ShipStatus.Instance).NetId, 35, SendOption.Reliable, target.OwnerId);
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

    private static bool Send(InnerNetClient net, PlayerControl target)
    {
        if (!Push(net, target, 2)) return false;
        return Push(net, target, 5);
    }
}

[HarmonyLib.HarmonyPatch(typeof(LobbyBehaviour), nameof(LobbyBehaviour.Start))]
internal static class NocturneVentKickReset
{
    public static void Postfix() => NocturneVentKick.Reset();
}
