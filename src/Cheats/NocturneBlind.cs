using System;
using System.Collections.Generic;
using AmongUs.GameOptions;
using Hazel;
using InnerNet;

namespace Nocturne;

internal static class NocturneBlind
{
    private const float VisionDark = -1f;
    private const float VisionBright = 1000f;

    private static readonly HashSet<byte> _selected = new HashSet<byte>();
    private static readonly Dictionary<byte, int> _state = new Dictionary<byte, int>();
    private static int _logicIndex = -1;

    internal static bool IsSelected(byte pid) => _selected.Contains(pid);
    internal static int SelectedCount => _selected.Count;
    internal static void ClearSelection() => _selected.Clear();

    internal static void ToggleSelect(byte pid)
    {
        if (_selected.Contains(pid)) _selected.Remove(pid);
        else _selected.Add(pid);
    }
    internal static bool IsDark(byte pid) => _state.TryGetValue(pid, out int s) && s == 1;
    internal static bool IsBright(byte pid) => _state.TryGetValue(pid, out int s) && s == 2;

    internal static string StateName(byte pid)
    {
        _state.TryGetValue(pid, out int s);
        return s == 1 ? NocturneText.T("тьма", "dark") : s == 2 ? NocturneText.T("свет", "bright") : NocturneText.T("норма", "normal");
    }

    internal static void SelectAll()
    {
        try
        {
            foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
            {
                if (pc == null || pc.Data == null || pc.Data.Disconnected || pc == PlayerControl.LocalPlayer) continue;
                _selected.Add(pc.PlayerId);
            }
        }
        catch { }
    }

    internal static void Reset()
    {
        _selected.Clear();
        _state.Clear();
        _logicIndex = -1;
    }

    internal static string Cycle(PlayerControl target)
    {
        if (target == null || target.Data == null) return NocturneText.T("нет цели", "no target");
        _state.TryGetValue(target.PlayerId, out int s);
        if (s == 0) return Blind(target);
        if (s == 1) return Bright(target);
        return Restore(target);
    }

    internal static string Blind(PlayerControl target) => Apply(target, VisionDark, 1);
    internal static string Bright(PlayerControl target) => Apply(target, VisionBright, 2);

    internal static string BlindSelected() => Mass(Blind);
    internal static string RestoreSelected() => Mass(Restore);

    private static string Mass(Func<PlayerControl, string> action)
    {
        if (_selected.Count == 0) return NocturneText.T("никто не отмечен", "nobody selected");
        int total = _selected.Count, n = 0;
        try
        {
            foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
            {
                if (pc == null || pc.Data == null || !_selected.Contains(pc.PlayerId)) continue;
                if (action(pc).StartsWith(NocturneText.T("готово", "done"))) n++;
            }
        }
        catch { }
        return NocturneText.T($"готово: {n} из {total}", $"done: {n} of {total}");
    }

    private static bool Ready(PlayerControl target)
        => target != null && target.Data != null && !target.Data.Disconnected
        && AmongUsClient.Instance != null && ShipStatus.Instance != null
        && GameManager.Instance != null && GameManager.Instance.LogicOptions != null;

    private static bool HasAnticheat()
    {
        try { return AmongUsClient.Instance != null && (int)AmongUsClient.Instance.NetworkMode == 1; }
        catch { return false; }
    }

    private static string GuardSelf()
    {
        var net = (InnerNetClient)AmongUsClient.Instance;
        return !net.AmHost && HasAnticheat() ? NocturneText.T("кикнет нас — только у хоста-друга (не офиц. сервер)", "kicks us — host-authoritative lobbies only (not official servers)") : null;
    }

    private static string GuardTarget(PlayerControl target)
    {
        if (target == PlayerControl.LocalPlayer) return NocturneText.T("это ты", "that is you");
        if (!Ready(target)) return target == null || target.Data == null ? NocturneText.T("нет цели", "no target") : NocturneText.T("только в матче", "in-match only");
        return GuardSelf();
    }

    private static string Apply(PlayerControl target, float vis, int state)
    {
        string err = GuardTarget(target);
        if (err != null) return err;

        try
        {
            IGameOptions clone = Clone();
            if (clone == null) return NocturneText.T("не удалось", "failed");
            clone.SetFloat(FloatOptionNames.CrewLightMod, vis);
            clone.SetFloat(FloatOptionNames.ImpostorLightMod, vis);
            if (!Push(clone, target.OwnerId)) return NocturneText.T("не удалось", "failed");
            _state[target.PlayerId] = state;
            return NocturneText.T("готово: ", "done: ") + target.Data.PlayerName;
        }
        catch { return NocturneText.T("не удалось", "failed"); }
    }

    internal static string Restore(PlayerControl target)
    {
        string err = GuardTarget(target);
        if (err != null) return err;

        try
        {
            IGameOptions real = GameManager.Instance.LogicOptions.currentGameOptions;
            if (real == null || !Push(real, target.OwnerId)) return NocturneText.T("не удалось", "failed");
            _state.Remove(target.PlayerId);
            return NocturneText.T("готово: ", "done: ") + target.Data.PlayerName;
        }
        catch { return NocturneText.T("не удалось", "failed"); }
    }

    private static IGameOptions Clone()
    {
        try
        {
            LogicOptions lo = GameManager.Instance.LogicOptions;
            byte[] bytes = lo.gameOptionsFactory.ToBytes(lo.currentGameOptions, AprilFoolsMode.IsAprilFoolsModeToggledOn);
            return lo.gameOptionsFactory.FromBytes(bytes);
        }
        catch { return null; }
    }

    private static int LogicOptionsIndex()
    {
        if (_logicIndex >= 0) return _logicIndex;
        try
        {
            var list = GameManager.Instance.LogicComponents;
            for (int i = 0; i < list.Count; i++)
                if (list[i].GetType() == typeof(LogicOptions)) { _logicIndex = i; return _logicIndex; }
        }
        catch { }
        return _logicIndex;
    }

    private static bool Push(IGameOptions options, int targetClientId)
    {
        int idx = LogicOptionsIndex();
        if (idx < 0) return false;

        MessageWriter body = null, w = null;
        try
        {
            LogicOptions lo = GameManager.Instance.LogicOptions;
            byte[] bytes = lo.gameOptionsFactory.ToBytes(options, AprilFoolsMode.IsAprilFoolsModeToggledOn);

            body = MessageWriter.Get(SendOption.Reliable);
            body.StartMessage((byte)idx);
            body.WriteBytesAndSize(bytes);
            body.EndMessage();

            var net = (InnerNetClient)AmongUsClient.Instance;
            w = MessageWriter.Get(SendOption.Reliable);
            w.StartMessage(6);
            w.Write(net.GameId);
            w.WritePacked(targetClientId);

            w.StartMessage(1);
            w.WritePacked(((InnerNetObject)GameManager.Instance).NetId);
            w.Write(body, false);
            w.EndMessage();

            w.EndMessage();
            net.SendOrDisconnect(w);
            return true;
        }
        catch { return false; }
        finally
        {
            try { body?.Recycle(); } catch { }
            try { w?.Recycle(); } catch { }
        }
    }
}

[HarmonyLib.HarmonyPatch(typeof(LobbyBehaviour), nameof(LobbyBehaviour.Start))]
internal static class NocturneBlindReset
{
    public static void Postfix() => NocturneBlind.Reset();
}
