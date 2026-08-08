using System.Collections.Generic;
using InnerNet;
using UnityEngine;

namespace Nocturne;

public sealed class NocturneVotekick : MonoBehaviour
{
    private enum Phase { Off, Room, Voted, Left, Rejoin, Final }

    private const float Settle = 0.4f;
    private const float LeaveMin = 1.1f;
    private const float LeaveMax = 1.5f;
    private const float StableHold = 0.5f;
    private const float RejoinDelay = 1.5f;
    private const float RejoinTimeout = 22f;
    private const float ManualTimeout = 180f;
    private const float FinalDelay = 1.5f;
    private const float RapidStep = 0.12f;
    private const float PulseStep = 0.3f;
    private const int SweepPasses = 3;
    private const int Cycles = 2;

    private static Phase _phase = Phase.Off;
    private static int _code;
    private static int _cyclesDone;
    private static float _at;
    private static float _pulseAt;
    private static float _votedStart;
    private static int _votedCount;
    private static float _votedStableAt;
    private static bool _swept;

    private static readonly List<byte> _queue = new List<byte>();
    private static float _rapidAt;
    private static int _passesLeft;

    private const float AutoInterval = 3f;
    private static readonly HashSet<byte> _targets = new HashSet<byte>();
    private static bool _autoOn;
    private static float _autoAt;

    public static bool Armed => _phase != Phase.Off;
    public static int TargetCount => _targets.Count;
    public static bool AutoTargeting => _autoOn;
    public static bool IsTarget(byte id) => _targets.Contains(id);

    public static void ToggleTarget(byte id)
    {
        if (!_targets.Remove(id)) _targets.Add(id);
        if (_targets.Count == 0) _autoOn = false;
    }

    public static void ClearTargets()
    {
        _targets.Clear();
        _autoOn = false;
        Toast(NocturneText.T("цели", "targets"), NocturneText.T("Список целей очищен.", "Target list cleared."), NocturneNotifyKind.Info);
    }

    public static bool HostIsTarget()
    {
        PlayerControl h = HostPlayer();
        return h != null && _targets.Contains(h.PlayerId);
    }

    public static void ToggleHostTarget()
    {
        PlayerControl h = HostPlayer();
        if (h == null) { Toast(NocturneText.T("хост", "host"), NocturneText.T("Хост не найден.", "Host not found."), NocturneNotifyKind.Warning); return; }
        if (h == PlayerControl.LocalPlayer) { Toast(NocturneText.T("хост", "host"), NocturneText.T("Хост — это ты.", "You are the host."), NocturneNotifyKind.Warning); return; }
        ToggleTarget(h.PlayerId);
        string nm = h.Data != null && !string.IsNullOrEmpty(h.Data.PlayerName) ? h.Data.PlayerName : "?";
        Toast(NocturneText.T("хост", "host"), _targets.Contains(h.PlayerId)
            ? NocturneText.T("Отмечен целью: ", "Marked as target: ") + nm
            : NocturneText.T("Снят с целей: ", "Unmarked: ") + nm, NocturneNotifyKind.Info);
    }

    private static PlayerControl HostPlayer()
    {
        try
        {
            InnerNetClient net = (InnerNetClient)AmongUsClient.Instance;
            if (net == null) return null;
            int hostId = net.HostId;
            foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
                if (pc != null && pc.Data != null && !pc.Data.Disconnected && pc.OwnerId == hostId) return pc;
        }
        catch { }
        return null;
    }

    public static void ToggleTargetAuto()
    {
        if (_autoOn) { _autoOn = false; Toast(NocturneText.T("авто-цели", "auto-targets"), NocturneText.T("Выключено.", "Off."), NocturneNotifyKind.Info); return; }
        if (_targets.Count == 0) { Toast(NocturneText.T("авто-цели", "auto-targets"), NocturneText.T("Сначала отметь цели.", "Mark targets first."), NocturneNotifyKind.Warning); return; }
        _autoOn = true;
        _autoAt = Time.unscaledTime;
        Toast(NocturneText.T("авто-цели", "auto-targets"), NocturneText.T("Голосую по отмеченным: ", "Voting marked: ") + _targets.Count, NocturneNotifyKind.Success);
    }

    private static void TickAuto()
    {
        if (!_autoOn) return;
        if (_targets.Count == 0) { _autoOn = false; return; }
        if (Time.unscaledTime < _autoAt) return;
        _autoAt = Time.unscaledTime + AutoInterval;
        VoteTargets();
    }

    private static int VoteTargets()
    {
        if (VoteBanSystem.Instance == null || _targets.Count == 0) return 0;
        int n = 0;
        try
        {
            foreach (byte id in _targets)
            {
                PlayerControl pc = ById(id);
                if (pc == null || pc.AmOwner || pc.Data == null || pc.Data.Disconnected) continue;
                VoteBanSystem.Instance.CmdAddVote(pc.Data.ClientId);
                n++;
            }
        }
        catch { }
        return n;
    }

    private static bool IsSel(PlayerControl pc)
    {
        return _targets.Count == 0 || _targets.Contains(pc.PlayerId);
    }

    public static void ToggleAuto()
    {
        if (_phase != Phase.Off) { Stop(NocturneText.T("выключено", "off")); return; }
        _cyclesDone = 0;
        _phase = Phase.Room;
        _at = Time.unscaledTime + Settle;
        Toast(NocturneText.T("взведено", "armed"), NocturneText.T("Голосую и, если включён перезаход, сам перезайду.", "Voting; auto-rejoin if enabled."), NocturneNotifyKind.Info);
    }

    private static void Stop(string why)
    {
        _phase = Phase.Off;
        _swept = false;
        _passesLeft = 0;
        _queue.Clear();
        Toast(NocturneText.T("авто", "auto"), why, NocturneNotifyKind.Info);
    }

    public static void VoteEveryone()
    {
        if (VoteBanSystem.Instance == null || PlayerControl.AllPlayerControls == null)
        { Toast(NocturneText.T("все", "everyone"), NocturneText.T("Система голосования не готова.", "Vote system not ready."), NocturneNotifyKind.Warning); return; }
        int n = 0;
        try
        {
            foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
            {
                if (pc == null || pc.AmOwner || pc.Data == null || pc.Data.Disconnected) continue;
                for (int i = 0; i < 3; i++) { VoteBanSystem.Instance.CmdAddVote(pc.Data.ClientId); n++; }
            }
        }
        catch { }
        Toast(NocturneText.T("все", "everyone"), n > 0
            ? NocturneText.T("Голоса ушли: ", "Votes sent: ") + n + NocturneText.T(". Нужно 3 клиента.", ". Needs 3 clients.")
            : NocturneText.T("Некого кикать.", "No one to kick."), n > 0 ? NocturneNotifyKind.Success : NocturneNotifyKind.Warning);
    }

    public static void VoteHost()
    {
        PlayerControl h = HostPlayer();
        if (h == null || h == PlayerControl.LocalPlayer)
        { Toast(NocturneText.T("хост", "host"), NocturneText.T("Хост не найден или это ты.", "Host not found or it's you."), NocturneNotifyKind.Warning); return; }
        int cid = h.Data != null ? h.Data.ClientId : -1;
        int n = 0;
        for (int i = 0; i < 3; i++) if (TryVote(cid)) n++;
        string nm = h.Data != null && !string.IsNullOrEmpty(h.Data.PlayerName) ? h.Data.PlayerName : "?";
        Toast(NocturneText.T("хост", "host"), n > 0
            ? NocturneText.T("Голос на ", "Vote on ") + nm + NocturneText.T(". Нужно 3 клиента.", ". Needs 3 clients.")
            : NocturneText.T("Не удалось.", "Failed."), n > 0 ? NocturneNotifyKind.Success : NocturneNotifyKind.Warning);
    }

    public static void RejoinLast()
    {
        if (InRoom()) { Toast(NocturneText.T("перезаход", "rejoin"), NocturneText.T("Ты уже в игре.", "You're already in a game."), NocturneNotifyKind.Warning); return; }
        if (_code == 0) { Toast(NocturneText.T("перезаход", "rejoin"), NocturneText.T("Нет последнего кода.", "No last code."), NocturneNotifyKind.Warning); return; }
        Rejoin(_code);
        Toast(NocturneText.T("перезаход", "rejoin"), NocturneText.T("Захожу в ", "Joining ") + GameCode.IntToGameName(_code), NocturneNotifyKind.Info);
    }

    private static float _trackAt;

    private static void TrackCode()
    {
        if (Time.unscaledTime < _trackAt) return;
        _trackAt = Time.unscaledTime + 1f;
        try
        {
            if (!InRoom() || AmongUsClient.Instance == null) return;
            int code = ((InnerNetClient)AmongUsClient.Instance).GameId;
            if (code != 0) _code = code;
        }
        catch { }
    }

    public void Update()
    {
        TrackCode();
        TickAuto();
        TickRapid();
        if (_phase == Phase.Off) return;
        try
        {
            switch (_phase)
            {
                case Phase.Room: TickRoom(); break;
                case Phase.Voted: TickVoted(); break;
                case Phase.Left: TickLeft(); break;
                case Phase.Rejoin: TickRejoin(); break;
                case Phase.Final: TickFinal(); break;
            }
        }
        catch { }
    }

    private static void TickRoom()
    {
        if (!InRoom()) return;
        if (Time.unscaledTime < _at) return;

        bool auto = NocturneConfig.VkRejoin.Value;
        SaveCode(!auto);

        if (_cyclesDone >= Cycles)
        {
            _swept = false;
            _phase = Phase.Final;
            _at = Time.unscaledTime + FinalDelay;
            Toast(NocturneText.T("финал", "final"), NocturneText.T("Сейчас пройдусь по каждому…", "Sweeping each player shortly…"), NocturneNotifyKind.Success);
            return;
        }

        int sent = VoteAll(false);
        string tail = auto ? NocturneText.T(". Выхожу…", ". Leaving…") : NocturneText.T(". Выхожу, код скопирован.", ". Leaving, code copied.");
        Toast(NocturneText.T("раунд ", "round ") + (_cyclesDone + 1), NocturneText.T("Голоса ушли: ", "Votes sent: ") + sent + tail, NocturneNotifyKind.Success);
        float now = Time.unscaledTime;
        _phase = Phase.Voted;
        _votedStart = now;
        _pulseAt = now + PulseStep;
        _votedCount = -1;
        _votedStableAt = now + StableHold;
    }

    private static void TickVoted()
    {
        float now = Time.unscaledTime;
        if (now >= _pulseAt) { _pulseAt = now + PulseStep; VoteAll(true); }

        int cnt = CountTargets();
        if (cnt != _votedCount) { _votedCount = cnt; _votedStableAt = now + StableHold; }

        float since = now - _votedStart;
        bool ready = since >= LeaveMin && now >= _votedStableAt;
        if (!ready && since < LeaveMax) return;

        Leave();
        _phase = Phase.Left;
        _at = now + RejoinDelay;
    }

    private static int CountTargets()
    {
        int n = 0;
        try
        {
            foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
                if (pc != null && !pc.AmOwner && pc.Data != null && !pc.Data.Disconnected && IsSel(pc)) n++;
        }
        catch { }
        return n;
    }

    private static void TickLeft()
    {
        if (InRoom()) return;
        if (Time.unscaledTime < _at) return;

        if (NocturneConfig.VkRejoin.Value)
        {
            Rejoin(_code);
            _at = Time.unscaledTime + RejoinTimeout;
        }
        else
        {
            _at = Time.unscaledTime + ManualTimeout;
            Toast(NocturneText.T("ждём", "waiting"), NocturneText.T("Вставь код и зайди снова — продолжу.", "Paste the code and rejoin — I'll continue."), NocturneNotifyKind.Info);
        }
        _phase = Phase.Rejoin;
    }

    private static void TickRejoin()
    {
        if (InRoom())
        {
            _cyclesDone++;
            _phase = Phase.Room;
            _at = Time.unscaledTime + Settle;
            Toast(NocturneText.T("перезаход", "rejoin"), NocturneText.T("Зашёл, раунд ", "Joined, round ") + (_cyclesDone + 1), NocturneNotifyKind.Info);
            return;
        }
        if (Time.unscaledTime >= _at)
        {
            SaveCode(true);
            Stop(NocturneConfig.VkRejoin.Value
                ? NocturneText.T("Не смог сам зайти — код скопирован, зайди вручную.", "Auto-join failed — code copied, rejoin manually.")
                : NocturneText.T("Долго нет захода — отменил.", "No rejoin — cancelled."));
        }
    }

    private static void TickFinal()
    {
        if (_swept) return;
        if (Time.unscaledTime < _at) return;
        StartRapid();
        _swept = true;
    }

    private static void TickRapid()
    {
        if (_queue.Count == 0)
        {
            if (_passesLeft > 0) { _passesLeft--; FillQueue(); return; }
            if (_phase == Phase.Final && _swept) Stop(NocturneText.T("Готово.", "Done."));
            return;
        }
        if (Time.unscaledTime < _rapidAt) return;
        _rapidAt = Time.unscaledTime + RapidStep;

        byte id = _queue[0];
        _queue.RemoveAt(0);
        PlayerControl pc = ById(id);
        if (pc != null) TryVote(pc.Data != null ? pc.Data.ClientId : -1);
    }

    private static void StartRapid()
    {
        _passesLeft = SweepPasses - 1;
        FillQueue();
        _rapidAt = Time.unscaledTime;
    }

    private static void FillQueue()
    {
        _queue.Clear();
        try
        {
            foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
                if (pc != null && !pc.AmOwner && pc.Data != null && !pc.Data.Disconnected && IsSel(pc))
                    _queue.Add(pc.PlayerId);
        }
        catch { }
    }

    public static void RapidAll()
    {
        StartRapid();
        int targets = _queue.Count;
        if (targets > 0) Toast(NocturneText.T("перебор", "sweep"), NocturneText.T("Бью по всем ×", "Voting all ×") + SweepPasses + ": " + targets, NocturneNotifyKind.Success);
        else Toast(NocturneText.T("перебор", "sweep"), NocturneText.T("Нет целей.", "No targets."), NocturneNotifyKind.Warning);
    }

    public static int VoteAll(bool once)
    {
        if (VoteBanSystem.Instance == null || PlayerControl.AllPlayerControls == null) return 0;
        int n = 0;
        try
        {
            foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
            {
                if (pc == null || pc.AmOwner || pc.Data == null || pc.Data.Disconnected || !IsSel(pc)) continue;
                int reps = once ? 1 : 3;
                for (int i = 0; i < reps; i++) { VoteBanSystem.Instance.CmdAddVote(pc.Data.ClientId); n++; }
            }
        }
        catch { }
        return n;
    }

    public static void VoteAllStay()
    {
        int n = VoteAll(false);
        if (n > 0) Toast(NocturneText.T("вручную", "manual"), NocturneText.T("Отправлено: ", "Sent: ") + n + NocturneText.T(". Остаюсь.", ". Staying."), NocturneNotifyKind.Success);
        else Toast(NocturneText.T("вручную", "manual"), NocturneText.T("Нет целей или система не готова.", "No targets or system not ready."), NocturneNotifyKind.Warning);
    }

    public static void VoteOne(PlayerControl pc)
    {
        if (pc == null || pc.Data == null) return;
        if (TryVote(pc.Data.ClientId))
        {
            string nm = pc.Data.PlayerName;
            if (string.IsNullOrEmpty(nm)) nm = "?";
            Toast(nm, NocturneText.T("Голос ушёл. Нужно 3 разных клиента.", "Vote sent. Needs 3 unique clients."), NocturneNotifyKind.Info);
        }
    }

    private static bool TryVote(int clientId)
    {
        if (clientId < 0 || VoteBanSystem.Instance == null) return false;
        try { VoteBanSystem.Instance.CmdAddVote(clientId); return true; }
        catch { return false; }
    }

    private static void SaveCode(bool copyAlways = false)
    {
        try
        {
            int code = ((InnerNetClient)AmongUsClient.Instance).GameId;
            if (code != 0) _code = code;
            if ((copyAlways || NocturneConfig.VkCopyCode.Value) && _code != 0)
                GUIUtility.systemCopyBuffer = GameCode.IntToGameName(_code);
        }
        catch { }
    }

    private static void Rejoin(int code)
    {
        try
        {
            AmongUsClient au = AmongUsClient.Instance;
            if (au == null || code == 0) return;
            au.GameId = code;
            var e = au.CoJoinOnlineGameFromCode(code);
            if (e != null) au.StartCoroutine(e);
        }
        catch { }
    }

    private static void Leave()
    {
        try { if (AmongUsClient.Instance != null) AmongUsClient.Instance.ExitGame(DisconnectReasons.ExitGame); }
        catch { }
    }

    private static bool InRoom() => LobbyBehaviour.Instance != null || ShipStatus.Instance != null;

    private static PlayerControl ById(byte id)
    {
        try
        {
            foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
                if (pc != null && pc.PlayerId == id) return pc;
        }
        catch { }
        return null;
    }

    private static void Toast(string t, string d, NocturneNotifyKind k) => NocturneToast.Push(NocturneText.T("Войткик — ", "Votekick — ") + t, d, 3f, k);
}
