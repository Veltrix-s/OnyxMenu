using System.Collections.Generic;
using UnityEngine;

namespace Nocturne;

internal static class NocturneShield
{
    private const float Gap = 2f;

    private static readonly HashSet<byte> _marked = new HashSet<byte>();
    private static float _last = -99f;

    internal static int MarkedCount => _marked.Count;
    internal static bool IsMarked(byte pid) => _marked.Contains(pid);
    internal static void ToggleMark(byte pid)
    {
        if (!_marked.Remove(pid))
            _marked.Add(pid);
    }

    internal static void ClearMarks()
    {
        _marked.Clear();
        if (NocturneConfig.ShieldAll != null) NocturneConfig.ShieldAll.Value = false;
    }

    internal static void MarkAll()
    {
        var e = PlayerControl.AllPlayerControls.GetEnumerator();
        while (e.MoveNext())
        {
            PlayerControl p = e.Current;
            if (Alive(p)) _marked.Add(p.PlayerId);
        }
    }

    internal static string Give(PlayerControl target)
    {
        if (!Utils.Host) return NocturneText.T("только хост", "host only");
        if (!Ready()) return NocturneText.T("только в матче", "in-match only");
        if (!Alive(target)) return NocturneText.T("нет цели", "no target");
        Send(target);
        return NocturneText.T("щит выдан", "shield given");
    }

    internal static string GiveAll()
    {
        if (!Utils.Host) return NocturneText.T("только хост", "host only");
        if (!Ready()) return NocturneText.T("только в матче", "in-match only");

        int n = 0;
        var e = PlayerControl.AllPlayerControls.GetEnumerator();
        while (e.MoveNext())
        {
            PlayerControl p = e.Current;
            if (!Alive(p)) continue;
            Send(p);
            n++;
        }
        return n > 0 ? NocturneText.T($"щитов: {n}", $"shields: {n}") : NocturneText.T("некому", "nobody");
    }

    internal static void Tick()
    {
        bool all = NocturneConfig.ShieldAll.Value;
        if (!all && _marked.Count == 0) return;
        if (!Ready())
            return;

        float now = Time.unscaledTime;
        if (now - _last < Gap) return;
        _last = now;

        var e = PlayerControl.AllPlayerControls.GetEnumerator();
        while (e.MoveNext())
        {
            PlayerControl p = e.Current;
            if (!Alive(p)) continue;
            if (!all && !_marked.Contains(p.PlayerId)) continue;
            Send(p);
        }
    }

    internal static void Reset()
    {
        _marked.Clear();
        _last = -99f;
    }

    private static void Send(PlayerControl target)
    {
        try
        {
            PlayerControl.LocalPlayer.RpcProtectPlayer(target, 0);
        }
        catch { }
    }

    private static bool Ready()
        => Utils.Host && ShipStatus.Instance != null && LobbyBehaviour.Instance == null
           && MeetingHud.Instance == null && PlayerControl.LocalPlayer != null;

    private static bool Alive(PlayerControl p)
        => p != null && p.Data != null && !p.Data.Disconnected && !p.Data.IsDead;
}

[HarmonyLib.HarmonyPatch(typeof(LobbyBehaviour), nameof(LobbyBehaviour.Start))]
internal static class NocturneShieldReset
{
    public static void Postfix() => NocturneShield.Reset();
}
