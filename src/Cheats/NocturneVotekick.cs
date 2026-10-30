using System.Collections.Generic;
using InnerNet;
using UnityEngine;

namespace Nocturne;

public sealed class NocturneVotekick : MonoBehaviour
{
    private enum Phase
    {
        Off, Room, Voted, Left, Rejoin, Final
    }

    private const float Settle = 0.4f;
    private const float HopCooldown = 1f;
    private const float FinalDelay = 1.5f;
    private const float NoTargetWait = 4f;
    private const float HopCap = 5f;
    private const float BatchGap = 0.1f;
    private const float ResendAt = 0.6f;
    private const float RejoinTimeout = 22f;
    private const float ManualTimeout = 180f;
    private const float RapidStep = 0.12f;
    private const int Batch = 4;
    private const int SweepPasses = 3;
    private const int Extra = 2;
    private const int Cycles = 2;

    private static Phase _phase = Phase.Off;
    private static int _cyclesDone;
    private static float _at;
    private static float _roomAt;
    private static float _votedStart;
    private static float _lastSend;
    private static float _passAt;
    private static int _pass;
    private static int _next;
    private static int _lobby;
    private static int _extra;
    private static bool _swept;

    private static readonly List<int> _ids = new List<int>();
    private static readonly List<int> _todo = new List<int>();
    private static readonly List<int> _queue = new List<int>();
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
        if (h == null)
        {
            Toast(NocturneText.T("хост", "host"), NocturneText.T("Хост не найден.", "Host not found."), NocturneNotifyKind.Warning);
            return;
        }
        if (h == PlayerControl.LocalPlayer)
        {
            Toast(NocturneText.T("хост", "host"), NocturneText.T("Хост — это ты.", "You are the host."), NocturneNotifyKind.Warning);
            return;
        }
        ToggleTarget(h.PlayerId);
        string nm = h.Data != null && !string.IsNullOrEmpty(h.Data.PlayerName) ? h.Data.PlayerName : "?";
        Toast(NocturneText.T("хост", "host"), _targets.Contains(h.PlayerId)
            ? NocturneText.T("Отмечен целью: ", "Marked as target: ") + nm
            : NocturneText.T("Снят с целей: ", "Unmarked: ") + nm, NocturneNotifyKind.Info);
    }

    private static PlayerControl HostPlayer()
    {
        InnerNetClient net = AmongUsClient.Instance;
        if (net == null) return null;
        int hostId = net.HostId;
        foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
            if (pc != null && pc.Data != null && !pc.Data.Disconnected && pc.OwnerId == hostId) return pc;
        return null;
    }

    public static void ToggleTargetAuto()
    {
        if (_autoOn)
        {
            _autoOn = false;
            Toast(NocturneText.T("авто-цели", "auto-targets"), NocturneText.T("Выключено.", "Off."), NocturneNotifyKind.Info);
            return;
        }
        if (_targets.Count == 0)
        {
            Toast(NocturneText.T("авто-цели", "auto-targets"), NocturneText.T("Сначала отметь цели.", "Mark targets first."), NocturneNotifyKind.Warning);
            return;
        }
        _autoOn = true;
        _autoAt = Time.unscaledTime;
        Toast(NocturneText.T("авто-цели", "auto-targets"), NocturneText.T("Голосую по отмеченным: ", "Voting marked: ") + _targets.Count, NocturneNotifyKind.Success);
    }

    private static void TickAuto()
    {
        if (!_autoOn) return;
        if (_targets.Count == 0)
        {
            _autoOn = false;
            return;
        }
        if (Time.unscaledTime < _autoAt) return;
        _autoAt = Time.unscaledTime + AutoInterval;
        VoteTargets();
    }

    private static int VoteTargets()
    {
        if (VoteBanSystem.Instance == null || _targets.Count == 0) return 0;
        int n = 0;
        foreach (byte id in _targets)
        {
            PlayerControl pc = Utils.ById(id);
            if (pc == null || pc.AmOwner || pc.Data == null || pc.Data.Disconnected) continue;
            VoteBanSystem.Instance.CmdAddVote(pc.OwnerId);
            n++;
        }
        return n;
    }

    public static void ToggleAuto()
    {
        if (_phase != Phase.Off)
        {
            Stop(NocturneText.T("выключено", "off"));
            return;
        }
        float now = Time.unscaledTime;
        _cyclesDone = 0;
        _lobby = 0;
        _phase = Phase.Room;
        _at = now + Settle;
        _roomAt = now;
        Toast(NocturneText.T("взведено", "armed"), NocturneText.T("Голосую и, если включён перезаход, сам перезайду.", "Voting; auto-rejoin if enabled."), NocturneNotifyKind.Info);
    }

    private static void Stop(string why)
    {
        _phase = Phase.Off;
        _swept = false;
        _passesLeft = 0;
        _queue.Clear();
        _todo.Clear();
        Toast(NocturneText.T("авто", "auto"), why, NocturneNotifyKind.Info);
    }

    public static void VoteEveryone()
    {
        if (VoteBanSystem.Instance == null)
        {
            Toast(NocturneText.T("все", "everyone"), NocturneText.T("Система голосования не готова.", "Vote system not ready."), NocturneNotifyKind.Warning);
            return;
        }
        int n = VoteAll();
        Toast(NocturneText.T("все", "everyone"), n > 0
            ? NocturneText.T("Голоса ушли: ", "Votes sent: ") + n + NocturneText.T(". Нужно 3 клиента.", ". Needs 3 clients.")
            : NocturneText.T("Некого кикать.", "No one to kick."), n > 0 ? NocturneNotifyKind.Success : NocturneNotifyKind.Warning);
    }

    public static void VoteHost()
    {
        PlayerControl h = HostPlayer();
        if (h == null || h == PlayerControl.LocalPlayer)
        {
            Toast(NocturneText.T("хост", "host"), NocturneText.T("Хост не найден или это ты.", "Host not found or it's you."), NocturneNotifyKind.Warning);
            return;
        }
        bool sent = TryVote(h.OwnerId);
        string nm = h.Data != null && !string.IsNullOrEmpty(h.Data.PlayerName) ? h.Data.PlayerName : "?";
        Toast(NocturneText.T("хост", "host"), sent
            ? NocturneText.T("Голос на ", "Vote on ") + nm + NocturneText.T(". Нужно 3 клиента.", ". Needs 3 clients.")
            : NocturneText.Failed, sent ? NocturneNotifyKind.Success : NocturneNotifyKind.Warning);
    }

    private static readonly Guard Work = new Guard("Votekick", TickAuto, TickRapid, TickPhase);

    public void FixedUpdate() => Work.Run();

    private static void TickPhase()
    {
        switch (_phase)
        {
            case Phase.Room:
                TickRoom();
                break;
            case Phase.Voted:
                TickVoted();
                break;
            case Phase.Left:
                TickLeft();
                break;
            case Phase.Rejoin:
                TickRejoin();
                break;
            case Phase.Final:
                TickFinal();
                break;
        }
    }

    private static void TickRoom()
    {
        float now = Time.unscaledTime;
        if (!Ready() || now < _at) return;

        Collect();
        if (_ids.Count == 0)
        {
            if (now - _roomAt >= NoTargetWait)
                Stop(NocturneText.T("Некого кикать.", "No one to kick."));
            return;
        }

        bool auto = NocturneConfig.VkRejoin.Value;
        if (!auto || NocturneConfig.VkCopyCode.Value) QuickRejoin.CopyCode();

        if (_cyclesDone >= Cycles)
        {
            _swept = false;
            _extra = 0;
            _phase = Phase.Final;
            _at = now + FinalDelay;
            Toast(NocturneText.T("финал", "final"), NocturneText.T("Сейчас пройдусь по каждому…", "Sweeping each player shortly…"), NocturneNotifyKind.Success);
            return;
        }

        _todo.Clear();
        _todo.AddRange(_ids);
        string tail = auto ? string.Empty : NocturneText.T(". Потом выйду, код скопирован.", ". Then leaving, code copied.");
        Toast(NocturneText.T("раунд ", "round ") + (_cyclesDone + 1), NocturneText.T("Голосую: ", "Voting: ") + _todo.Count + tail, NocturneNotifyKind.Success);
        _phase = Phase.Voted;
        _votedStart = now;
        _lastSend = now;
        _passAt = now;
        _pass = 0;
        _next = 0;
    }

    private static void TickVoted()
    {
        float now = Time.unscaledTime;
        Pour(now);
        Late(now);
        if (now - _votedStart < HopCap && (_pass < 2 || now - _lastSend < HopCooldown)) return;

        Hop(now);
    }

    private static void Late(float now)
    {
        VoteBanSystem vbs = VoteBanSystem.Instance;
        if (vbs == null) return;

        Collect();
        for (int i = 0; i < _ids.Count; i++)
        {
            if (_todo.Contains(_ids[i])) continue;
            _todo.Add(_ids[i]);
            if (_pass < 2) continue;

            vbs.CmdAddVote(_ids[i]);
            _lastSend = now;
        }
    }

    private static void TickLeft()
    {
        if (Utils.InRoom) return;

        _at = Time.unscaledTime + ManualTimeout;
        _phase = Phase.Rejoin;
        Toast(NocturneText.T("ждём", "waiting"), NocturneText.T("Вставь код и зайди снова — продолжу.", "Paste the code and rejoin, I'll continue."), NocturneNotifyKind.Info);
    }

    private static void TickRejoin()
    {
        float now = Time.unscaledTime;
        if (Ready())
        {
            _cyclesDone++;
            _phase = Phase.Room;
            _at = now + Settle;
            _roomAt = now;
            Toast(NocturneText.T("перезаход", "rejoin"), NocturneText.T("Зашёл, раунд ", "Joined, round ") + (_cyclesDone + 1), NocturneNotifyKind.Info);
            return;
        }
        if (now >= _at)
        {
            QuickRejoin.CopyCode();
            Stop(NocturneConfig.VkRejoin.Value
                ? NocturneText.T("Не смог сам зайти — код скопирован, зайди вручную.", "Auto-join failed. Code copied, rejoin manually.")
                : NocturneText.T("Долго нет захода — отменил.", "No rejoin, cancelled."));
        }
    }

    private static void TickFinal()
    {
        float now = Time.unscaledTime;
        if (!_swept)
        {
            if (now < _at) return;
            StartRapid();
            _swept = true;
            return;
        }

        if (_queue.Count > 0 || _passesLeft > 0) return;
        if (Missed(now)) return;

        Stop(NocturneText.T("Готово.", "Done."));
    }

    private static bool Missed(float now)
    {
        VoteBanSystem vbs = VoteBanSystem.Instance;
        if (vbs == null || _extra >= Extra) return false;

        Collect();
        for (int i = 0; i < _ids.Count; i++)
            if (!vbs.HasMyVote(_ids[i]))
                _queue.Add(_ids[i]);
        if (_queue.Count == 0) return false;

        _extra++;
        _rapidAt = now;
        return true;
    }

    private static void Hop(float now)
    {
        VoteBanSystem vbs = VoteBanSystem.Instance;
        _lobby = vbs != null ? vbs.GetInstanceID() : 0;

        if (!NocturneConfig.VkRejoin.Value)
        {
            QuickRejoin.Leave(true);
            _phase = Phase.Left;
            return;
        }

        if (!QuickRejoin.Reconnect(true))
        {
            QuickRejoin.CopyCode();
            Stop(NocturneText.T("Не смог перезайти — код скопирован, зайди вручную.", "Can't rejoin. Code copied, rejoin manually."));
            return;
        }
        _phase = Phase.Rejoin;
        _at = now + RejoinTimeout;
    }

    private static void Pour(float now)
    {
        if (_pass >= 2 || now < _passAt) return;

        VoteBanSystem vbs = VoteBanSystem.Instance;
        if (vbs == null)
        {
            _pass = 2;
            return;
        }

        int sent = 0;
        while (_next < _todo.Count && sent < Batch)
        {
            vbs.CmdAddVote(_todo[_next++]);
            sent++;
        }
        if (sent > 0)
        {
            _lastSend = now;
            _passAt = now + BatchGap;
        }
        if (_next < _todo.Count) return;

        _pass++;
        _next = 0;
        if (_pass == 1)
            _passAt = Ui.Max(_passAt, _votedStart + ResendAt);
    }

    private static void TickRapid()
    {
        if (_queue.Count == 0)
        {
            if (_passesLeft > 0)
            {
                _passesLeft--;
                FillQueue();
            }
            return;
        }
        if (Time.unscaledTime < _rapidAt) return;
        _rapidAt = Time.unscaledTime + RapidStep;

        int id = _queue[0];
        _queue.RemoveAt(0);
        TryVote(id);
    }

    private static void StartRapid()
    {
        _passesLeft = SweepPasses - 1;
        FillQueue();
        _rapidAt = Time.unscaledTime;
    }

    private static void FillQueue()
    {
        Collect();
        _queue.Clear();
        _queue.AddRange(_ids);
    }

    public static void RapidAll()
    {
        StartRapid();
        int targets = _queue.Count;
        if (targets > 0)
            Toast(NocturneText.T("перебор", "sweep"), NocturneText.T("Бью по всем ×", "Voting all ×") + SweepPasses + ": " + targets, NocturneNotifyKind.Success);
        else
            Toast(NocturneText.T("перебор", "sweep"), NocturneText.T("Нет целей.", "No targets."), NocturneNotifyKind.Warning);
    }

    private static bool Ready()
    {
        VoteBanSystem vbs = VoteBanSystem.Instance;
        return vbs != null && vbs.GetInstanceID() != _lobby && Utils.InRoom && PlayerControl.LocalPlayer != null;
    }

    private static void Collect()
    {
        _ids.Clear();
        InnerNetClient net = AmongUsClient.Instance;
        if (net == null || net.allClients == null) return;

        var e = net.allClients.GetEnumerator();
        while (e.MoveNext())
        {
            ClientData c = e.Current;
            if (c == null || c.Id < 0 || c.Id == net.ClientId) continue;
            if (_targets.Count > 0 && (c.Character == null || !_targets.Contains(c.Character.PlayerId))) continue;
            _ids.Add(c.Id);
        }

        int at = _ids.IndexOf(net.HostId);
        if (at >= 0 && at < _ids.Count - 1)
        {
            _ids.RemoveAt(at);
            _ids.Add(net.HostId);
        }
    }

    public static int VoteAll()
    {
        if (VoteBanSystem.Instance == null) return 0;
        Collect();
        for (int i = 0; i < _ids.Count; i++)
            VoteBanSystem.Instance.CmdAddVote(_ids[i]);
        return _ids.Count;
    }

    public static void VoteAllStay()
    {
        int n = VoteAll();
        if (n > 0)
            Toast(NocturneText.T("вручную", "manual"), NocturneText.T("Отправлено: ", "Sent: ") + n + NocturneText.T(". Остаюсь.", ". Staying."), NocturneNotifyKind.Success);
        else
            Toast(NocturneText.T("вручную", "manual"), NocturneText.T("Нет целей или система не готова.", "No targets or system not ready."), NocturneNotifyKind.Warning);
    }

    public static void VoteOne(PlayerControl pc)
    {
        if (pc == null || pc.Data == null) return;
        if (TryVote(pc.OwnerId))
        {
            string nm = pc.Data.PlayerName;
            if (string.IsNullOrEmpty(nm)) nm = "?";
            Toast(nm, NocturneText.T("Голос ушёл. Нужно 3 разных клиента.", "Vote sent. Needs 3 unique clients."), NocturneNotifyKind.Info);
        }
    }

    private static bool TryVote(int clientId)
    {
        if (clientId < 0 || VoteBanSystem.Instance == null) return false;
        VoteBanSystem.Instance.CmdAddVote(clientId);
        return true;
    }

    private static void Toast(string t, string d, NocturneNotifyKind k) => NocturneToast.Push(NocturneText.T("Войткик — ", "Votekick: ") + t, d, 3f, k);
}
