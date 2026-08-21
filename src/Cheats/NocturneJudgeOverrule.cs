using System.Collections.Generic;
using HarmonyLib;
using InnerNet;

namespace Nocturne;

internal static class NocturneJudgeOverrule
{
    private struct Pick
    {
        internal byte Judge;
        internal byte Target;
        internal ushort Nonce;
    }

    private static readonly List<Pick> _queue = new List<Pick>();
    private static readonly List<string> _lines = new List<string>();
    private static bool Host => AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost;
    private static bool Watching => NocturneConfig.JudgeWatch.Value;

    internal static List<string> Lines => _lines;
    internal static int Total { get; private set; }

    internal static void ForgetMeeting()
    {
        if (_queue.Count == 0) return;
        _queue.Clear();
        _lines.Clear();
    }

    internal static void ForgetMatch()
    {
        ForgetMeeting();
        Total = 0;
    }

    internal static void Note(byte judge, byte target, ushort nonce)
    {
        int at = IndexOf(judge);
        if (at >= 0 && _queue[at].Target == target) return;

        var p = new Pick { Judge = judge, Target = target, Nonce = nonce };
        if (at >= 0) _queue[at] = p;
        else _queue.Add(p);
        Rebuild();

        if (!Watching) return;
        string j = Name(judge), t = Name(target);
        NocturneEventNotify.Fire(NocturneEventCat.Meeting,
            $"⚖ Судья {j} целит в {t}", $"⚖ Judge {j} targets {t}", NocturneNotifyKind.Warning, true);
    }

    internal static void Drop(byte judge)
    {
        int at = IndexOf(judge);
        if (at < 0) return;
        _queue.RemoveAt(at);
        Rebuild();
    }

    internal static void Landed(ushort nonce, NetworkedPlayerInfo exiled)
    {
        byte judge = 255;
        for (int i = 0; i < _queue.Count; i++)
            if (_queue[i].Nonce == nonce) { judge = _queue[i].Judge; break; }

        Total++;
        ForgetMeeting();

        if (!Watching) return;
        string j = judge != 255 ? Name(judge) : NocturneText.T("судья", "judge");
        string t = exiled != null ? exiled.PlayerName : NocturneText.T("никто", "no one");
        NocturneEventNotify.Fire(NocturneEventCat.Meeting,
            $"⚖ Оверрул сработал: {j} выбросил {t}", $"⚖ Overrule landed: {j} ejected {t}", NocturneNotifyKind.Danger, true);
    }

    internal static string ClearAll()
    {
        if (!Host) return NocturneText.T("Только хост.", "Host only.");
        MeetingHud m = MeetingHud.Instance;
        if (m == null) return NocturneText.T("Нет собрания.", "No meeting.");

        if (m.judgeOverrulesQueue != null) m.judgeOverrulesQueue.Clear();
        m.ClearJudgeOverrule();
        ForgetMeeting();
        return NocturneText.T("Очередь оверрулов сброшена.", "Overrule queue cleared.");
    }

    private static int IndexOf(byte judge)
    {
        for (int i = 0; i < _queue.Count; i++) if (_queue[i].Judge == judge) return i;
        return -1;
    }

    private static void Rebuild()
    {
        _lines.Clear();
        for (int i = 0; i < _queue.Count; i++)
            _lines.Add("⚖ " + Name(_queue[i].Judge) + "  →  " + Name(_queue[i].Target));
    }

    private static string Name(byte pid)
    {
        if (GameData.Instance == null) return "?";
        NetworkedPlayerInfo info = GameData.Instance.GetPlayerById(pid);
        return info != null && !string.IsNullOrEmpty(info.PlayerName) ? info.PlayerName : "#" + pid;
    }
}

[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.AddOrUpdateJudgeOverrule))]
internal static class NocturneJudgeQueuePatch
{
    public static void Postfix([HarmonyArgument(0)] PlayerId judge, [HarmonyArgument(1)] PlayerId target, [HarmonyArgument(2)] ushort nonce)
        => NocturneJudgeOverrule.Note(judge.Value, target.Value, nonce);
}

[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.ClearJudgeOverrule))]
internal static class NocturneJudgeClearPatch
{
    public static void Postfix() => NocturneJudgeOverrule.ForgetMeeting();
}

[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.UpdateOverruleQueueFromDisconnection))]
internal static class NocturneJudgeLeavePatch
{
    public static void Postfix([HarmonyArgument(0)] PlayerId gone) => NocturneJudgeOverrule.Drop(gone.Value);
}

[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.VotingComplete))]
internal static class NocturneJudgeDonePatch
{
    public static void Prefix([HarmonyArgument(1)] NetworkedPlayerInfo exiled, [HarmonyArgument(3)] bool wasOverruled, [HarmonyArgument(4)] ushort nonce)
    {
        if (wasOverruled) NocturneJudgeOverrule.Landed(nonce, exiled);
        else NocturneJudgeOverrule.ForgetMeeting();
    }
}

[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
internal static class NocturneJudgeMeetingStartPatch
{
    public static void Postfix() => NocturneJudgeOverrule.ForgetMeeting();
}

[HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.Start))]
internal static class NocturneJudgeMatchStartPatch
{
    public static void Postfix() => NocturneJudgeOverrule.ForgetMatch();
}
