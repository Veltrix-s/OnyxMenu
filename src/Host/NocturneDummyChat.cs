using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace Nocturne;

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.MurderPlayer), new Type[] { typeof(PlayerControl), typeof(MurderResultFlags) })]
internal static class NocturneDummyWitnessPatch
{
    public static void Postfix(PlayerControl __instance, PlayerControl target) => NocturneDummyChat.OnMurder(__instance, target);
}

internal static class NocturneDummyChat
{
    private const float SpotRange = 1.6f;
    private const float SightRange = 5.5f;
    private const byte Skip = 253;

    private sealed class Seen { public byte Id; public float T; public bool Killer; }
    private static readonly Dictionary<byte, List<Seen>> Mem = new Dictionary<byte, List<Seen>>();
    private static float _nextScan;

    private static float _reportUntil;
    private static byte _reporter = byte.MaxValue;
    private static byte _suspect = byte.MaxValue;
    private static readonly List<byte> _near = new List<byte>();
    private static string _room = "";

    private static bool _pending;
    private static float _nextLine;
    private static int _step;
    private static readonly List<byte> _talkers = new List<byte>();
    private static int _reply;
    private static bool _voted;
    private static float _voteAt;
    private static readonly List<byte> _queue = new List<byte>();
    private static byte _target = Skip;
    private static float _nextVote;
    private static bool _voting;

    private static bool On => NocturneConfig.DummyReportBodies != null && NocturneConfig.DummyReportBodies.Value;

    internal static void Reset()
    {
        Mem.Clear();
        _reportUntil = 0f; _reporter = byte.MaxValue; _suspect = byte.MaxValue;
        _near.Clear(); _room = "";
        _pending = false; _step = 0; _talkers.Clear(); _reply = 0;
        _voted = false; _queue.Clear(); _target = Skip; _nextVote = 0f; _voting = false;
    }

    internal static void OnMurder(PlayerControl killer, PlayerControl victim)
    {
        try
        {
            if (!On || killer == null) return;
            Vector2 at = victim != null ? (Vector2)victim.GetTruePosition() : (Vector2)killer.GetTruePosition();
            float now = Time.time;
            foreach (PlayerControl d in NocturneDummies.Live())
            {
                if (d == null || d.Data == null || d.Data.IsDead) continue;
                if (((Vector2)d.GetTruePosition() - at).magnitude > SightRange + 1.5f) continue;
                List<Seen> mem = MemOf(d.PlayerId);
                bool found = false;
                for (int i = 0; i < mem.Count; i++)
                    if (mem[i].Id == killer.PlayerId) { mem[i].T = now; mem[i].Killer = true; found = true; break; }
                if (!found) mem.Add(new Seen { Id = killer.PlayerId, T = now, Killer = true });
            }
        }
        catch { }
    }

    private static List<Seen> MemOf(byte id)
    {
        if (!Mem.TryGetValue(id, out List<Seen> m)) { m = new List<Seen>(); Mem[id] = m; }
        return m;
    }

    private static DeadBody[] _bodies;
    private static float _bodiesAt = -99f;

    private static DeadBody[] Bodies()
    {
        if (_bodies == null || Time.time - _bodiesAt > 0.4f)
        {
            _bodiesAt = Time.time;
            try { _bodies = UnityEngine.Object.FindObjectsOfType<DeadBody>(); } catch { _bodies = null; }
        }
        return _bodies;
    }

    internal static bool TryReport(PlayerControl d)
    {
        try
        {
            if (!On || d == null || d.Data == null || d.Data.IsDead) return false;
            if (Time.time < _reportUntil || MeetingHud.Instance != null) return false;

            Vector2 me = d.GetTruePosition();
            DeadBody[] bodies = Bodies();
            if (bodies == null) return false;

            for (int i = 0; i < bodies.Length; i++)
            {
                DeadBody b = bodies[i];
                if (b == null) continue;
                Vector2 bp = b.TruePosition;
                if ((bp - me).magnitude > SpotRange) continue;

                Capture(d, b, bp);
                _reportUntil = Time.time + 30f;
                NetworkedPlayerInfo victim = GameData.Instance.GetPlayerById(b.ParentId);
                try { d.CmdReportDeadBody(victim); } catch { return false; }

                _reporter = d.PlayerId;
                _pending = true; _voted = false; _step = 0; _reply = 0; _voteAt = 0f;
                _queue.Clear(); _nextVote = 0f; _voting = false;
                _nextLine = Time.time + 2.5f;
                return true;
            }
        }
        catch { }
        return false;
    }

    private static void Capture(PlayerControl reporter, DeadBody body, Vector2 bp)
    {
        _near.Clear();
        _suspect = byte.MaxValue;
        _room = RoomAt(bp);
        byte victimId = body.ParentId;

        try
        {
            byte witness = byte.MaxValue;
            if (Mem.TryGetValue(reporter.PlayerId, out List<Seen> mem))
            {
                float now = Time.time;
                for (int i = 0; i < mem.Count; i++)
                {
                    Seen s = mem[i];
                    if (s.Id == victimId) continue;
                    if (s.Killer && now - s.T <= 60f && !NocturneDummies.IsDummy(s.Id))
                    {
                        witness = s.Id;
                        if (!_near.Contains(s.Id)) _near.Add(s.Id);
                    }
                    else if (now - s.T <= 12f && !_near.Contains(s.Id)) _near.Add(s.Id);
                }
            }

            var all = PlayerControl.AllPlayerControls;
            int n = ((Il2CppArrayBase<PlayerControl>)(object)all).Count;
            float best = float.MaxValue;
            byte nearest = byte.MaxValue;
            for (int i = 0; i < n; i++)
            {
                PlayerControl p = ((Il2CppArrayBase<PlayerControl>)(object)all)[i];
                if (p == null || p.Data == null || p.Data.IsDead) continue;
                if (p.PlayerId == victimId || NocturneDummies.IsDummy(p.PlayerId)) continue;
                float dd = ((Vector2)p.GetTruePosition() - bp).magnitude;
                if (dd <= 7f)
                {
                    if (!_near.Contains(p.PlayerId)) _near.Add(p.PlayerId);
                    if (dd < best) { best = dd; nearest = p.PlayerId; }
                }
            }

            if (witness != byte.MaxValue) _suspect = witness;
            else if (nearest != byte.MaxValue) _suspect = nearest;
            else for (int i = _near.Count - 1; i >= 0; i--)
                if (!NocturneDummies.IsDummy(_near[i])) { _suspect = _near[i]; break; }
        }
        catch { }
    }

    internal static void TickVision()
    {
        try
        {
            if (!On || MeetingHud.Instance != null || Time.time < _nextScan) return;
            _nextScan = Time.time + 0.5f;
            float now = Time.time;
            foreach (PlayerControl d in NocturneDummies.Live())
            {
                if (d == null || d.Data == null || d.Data.IsDead) continue;
                Vector2 dp = d.GetTruePosition();
                List<Seen> mem = MemOf(d.PlayerId);
                var all = PlayerControl.AllPlayerControls;
                int n = ((Il2CppArrayBase<PlayerControl>)(object)all).Count;
                for (int i = 0; i < n; i++)
                {
                    PlayerControl p = ((Il2CppArrayBase<PlayerControl>)(object)all)[i];
                    if (p == null || p.Data == null || p.Data.IsDead || p.PlayerId == d.PlayerId) continue;
                    if (((Vector2)p.GetTruePosition() - dp).magnitude <= SightRange) Bump(mem, p.PlayerId, now);
                }
                mem.RemoveAll((System.Predicate<Seen>)(s => now - s.T > 20f));
            }
        }
        catch { }
    }

    private static void Bump(List<Seen> mem, byte id, float now)
    {
        for (int i = 0; i < mem.Count; i++) if (mem[i].Id == id) { mem[i].T = now; return; }
        mem.Add(new Seen { Id = id, T = now });
    }

    internal static void TickMeeting()
    {
        try
        {
            if (!On) return;
            MeetingHud hud = MeetingHud.Instance;
            if (hud == null) { _pending = false; return; }
            if (!_pending) return;

            float now = Time.time;
            if (now >= _nextLine && _step >= 0) NextLine();

            if (!_voted && _voteAt > 0f && now >= _voteAt && VotingPhase(hud))
            {
                if (!_voting) { _voting = true; BeginVotes(); }
                else PumpVotes(hud);
            }
        }
        catch { }
    }

    private static void NextLine()
    {
        PlayerControl rep = ById(_reporter);
        switch (_step)
        {
            case 0:
            {
                string w = string.IsNullOrEmpty(_room) ? NocturneText.T("где-то", "somewhere") : _room;
                string line = _near.Count == 0
                    ? NocturneText.T($"Тело в {w}, рядом никого. Скип?", $"Body in {w}, no one around. Skip?")
                    : NocturneText.T($"Нашёл тело в {w}! Рядом: {Names(_near)}.", $"Found a body in {w}! Nearby: {Names(_near)}.");
                Say(rep, line);
                _step = 1; _nextLine = Time.time + 2.2f;
                break;
            }
            case 1:
            {
                if (_suspect != byte.MaxValue)
                    Say(rep, NocturneText.T($"Думаю на {Name(_suspect)}, был ближе всех.", $"I suspect {Name(_suspect)}, was closest."));
                PickTalkers();
                _reply = 0; _step = 2; _nextLine = Time.time + 2f;
                break;
            }
            default:
            {
                if (_reply < _talkers.Count)
                {
                    Say(ById(_talkers[_reply]), Reply(_reply));
                    _reply++;
                    _nextLine = Time.time + UnityEngine.Random.Range(1.6f, 2.4f);
                }
                else { _step = -1; _voteAt = Time.time + 2f; }
                break;
            }
        }
    }

    private static readonly string[] Ru =
    {
        "Я на тасках был, не я.", "Мутно...", "Мало инфы, скип.", "А ты сам где был?",
        "Согласен, голосуем.", "Я никого не видел.", "Видел его у тела!", "Не я, честно.",
        "Логично, го за ним.", "Давай без рандома.", "Я чинил саботаж.", "Скип на всякий.",
        "Кто за кем шёл?", "Верю тебе.", "Слишком быстро обвиняешь.", "Голос или скип?",
    };

    private static readonly string[] En =
    {
        "Was on tasks, not me.", "Sus...", "Not enough info, skip.", "Where were you?",
        "Agree, let's vote.", "Saw no one.", "Saw them by the body!", "Not me, swear.",
        "Makes sense, vote them.", "No random votes.", "I was fixing sabotage.", "Skip to be safe.",
        "Who followed who?", "I believe you.", "Accusing too fast.", "Vote or skip?",
    };

    private static string Reply(int salt)
    {
        var a = NocturneText.IsRussian ? Ru : En;
        return a[(UnityEngine.Random.Range(0, a.Length) + salt) % a.Length];
    }

    private static void PickTalkers()
    {
        _talkers.Clear();
        try
        {
            foreach (PlayerControl pc in NocturneDummies.Live())
            {
                if (pc == null || pc.Data == null || pc.Data.IsDead || pc.PlayerId == _reporter) continue;
                _talkers.Add(pc.PlayerId);
                if (_talkers.Count >= 6) break;
            }
            Shuffle(_talkers);
        }
        catch { }
    }

    private static void Shuffle(List<byte> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private static void BeginVotes()
    {
        _queue.Clear();
        _target = _suspect != byte.MaxValue ? _suspect : Skip;
        try
        {
            foreach (PlayerControl pc in NocturneDummies.Live())
                if (pc != null && pc.Data != null && !pc.Data.IsDead) _queue.Add(pc.PlayerId);
            Shuffle(_queue);
        }
        catch { }
        _nextVote = Time.time + UnityEngine.Random.Range(0.5f, 1.2f);
    }

    private static void PumpVotes(MeetingHud hud)
    {
        if (Time.time < _nextVote) return;
        while (_queue.Count > 0)
        {
            byte id = _queue[0];
            _queue.RemoveAt(0);
            PlayerControl pc = ById(id);
            if (pc == null || pc.Data == null || pc.Data.IsDead) continue;
            try { hud.CastVote(id, _target); } catch { }
            _nextVote = Time.time + UnityEngine.Random.Range(1f, 2f);
            return;
        }
        _voted = true; _pending = false;
    }

    private static bool VotingPhase(MeetingHud hud)
    {
        try { return (int)hud.state == 2 || (int)hud.state == 3; } catch { return true; }
    }

    private static void Say(PlayerControl pc, string text)
    {
        if (pc == null || string.IsNullOrEmpty(text)) return;
        try { pc.RpcSendChat(text); } catch { }
    }

    private static PlayerControl ById(byte id)
    {
        try { return GameData.Instance.GetPlayerById(id)?.Object; } catch { return null; }
    }

    private static string Name(byte id)
    {
        try { var d = GameData.Instance.GetPlayerById(id); return d != null ? Strip(d.PlayerName) : "?"; }
        catch { return "?"; }
    }

    private static string Names(List<byte> ids)
    {
        if (ids == null || ids.Count == 0) return NocturneText.T("никого", "no one");
        var parts = new List<string>();
        for (int i = 0; i < ids.Count && i < 4; i++) parts.Add(Name(ids[i]));
        return string.Join(", ", parts);
    }

    private static string Strip(string s)
    {
        if (string.IsNullOrEmpty(s)) return "?";
        int lt;
        while ((lt = s.IndexOf('<')) >= 0)
        {
            int gt = s.IndexOf('>', lt);
            if (gt < 0) break;
            s = s.Remove(lt, gt - lt + 1);
        }
        return s;
    }

    private static string RoomAt(Vector2 pos)
    {
        try
        {
            ShipStatus ship = ShipStatus.Instance;
            if (ship == null) return "";
            var rooms = ship.AllRooms;
            int n = ((Il2CppArrayBase<PlainShipRoom>)(object)rooms).Count;
            for (int i = 0; i < n; i++)
            {
                PlainShipRoom r = ((Il2CppArrayBase<PlainShipRoom>)(object)rooms)[i];
                if (r == null || r.roomArea == null) continue;
                if (r.roomArea.OverlapPoint(pos)) return RoomName(r.RoomId);
            }
        }
        catch { }
        return "";
    }

    private static string RoomName(SystemTypes room)
    {
        bool ru = NocturneText.IsRussian;
        switch ((int)room)
        {
            case 1: return ru ? "Админка" : "Admin";
            case 2: return ru ? "Связь" : "Comms";
            case 3: return ru ? "Реактор" : "Reactor";
            case 4: return ru ? "Электрощит" : "Electrical";
            case 5: return ru ? "Навигация" : "Navigation";
            case 6: return ru ? "О2" : "O2";
            case 7: return ru ? "Щиты" : "Shields";
            case 8: return ru ? "Столовая" : "Cafeteria";
            case 9: return ru ? "Хранилище" : "Storage";
            case 10: return ru ? "Медотсек" : "MedBay";
            case 11: return ru ? "Камеры" : "Security";
            case 12: return ru ? "Оружейная" : "Weapons";
            case 13: return ru ? "Нижний двигатель" : "Lower Engine";
            case 14: return ru ? "Связь" : "Comms";
            case 16: return ru ? "Запуск" : "Launchpad";
            case 17: return ru ? "Верхний двигатель" : "Upper Engine";
            case 19: return ru ? "Двигатель" : "Engine Room";
            case 21: return ru ? "Реактор" : "Reactor";
            case 24: return ru ? "Кабина" : "Cockpit";
            case 25: return ru ? "Архив" : "Records";
            default: return ru ? "коридор" : "hallway";
        }
    }
}
