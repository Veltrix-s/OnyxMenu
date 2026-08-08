using System.Collections.Generic;
using HarmonyLib;
using InnerNet;
using UnityEngine;

namespace Nocturne;

public sealed class NocturneAntiVotekick : MonoBehaviour
{
    private const int Threshold = 2;

    private static readonly HashSet<int> _voters = new HashSet<int>();
    private static bool _armed;
    private static int _code;
    private static float _at;
    private static int _step;

    private static bool On => NocturneConfig.VkAutoRejoin != null && NocturneConfig.VkAutoRejoin.Value;

    internal static void OnVote(int srcClient, int clientId)
    {
        if (!On || _armed) return;
        try
        {
            InnerNetClient net = (InnerNetClient)AmongUsClient.Instance;
            if (net == null || net.AmHost) return;
            if (clientId != net.ClientId || srcClient == net.ClientId) return;
            _voters.Add(srcClient);
            if (_voters.Count >= Threshold) Trigger();
        }
        catch { }
    }

    private static void Trigger()
    {
        _armed = true;
        _step = 1;
        _at = Time.unscaledTime;
        SaveCode();
        NocturneToast.Push(NocturneText.T("Анти-войткик", "Anti-votekick"), NocturneText.T("Против тебя 2 голоса — перезахожу.", "2 votes on you — rejoining."), 3f, NocturneNotifyKind.Danger);
    }

    public void Update()
    {
        if (_step == 0) return;
        float now = Time.unscaledTime;
        try
        {
            switch (_step)
            {
                case 1:
                    Leave();
                    _at = now + 1.5f;
                    _step = 2;
                    break;
                case 2:
                    if (InRoom()) { Reset(); break; }
                    if (now >= _at) { Rejoin(_code); _at = now + 22f; _step = 3; }
                    break;
                case 3:
                    if (InRoom()) { Reset(); break; }
                    if (now >= _at)
                    {
                        SaveCode(true);
                        NocturneToast.Push(NocturneText.T("Анти-войткик", "Anti-votekick"), NocturneText.T("Сам не зашёл — код скопирован, зайди вручную.", "Auto-join failed — code copied, rejoin manually."), 4f, NocturneNotifyKind.Warning);
                        Reset();
                    }
                    break;
            }
        }
        catch { Reset(); }
    }

    private static void Reset()
    {
        _armed = false;
        _step = 0;
        _voters.Clear();
    }

    private static void SaveCode(bool copyAlways = false)
    {
        try
        {
            int code = ((InnerNetClient)AmongUsClient.Instance).GameId;
            if (code != 0) _code = code;
            if ((copyAlways || (NocturneConfig.VkCopyCode != null && NocturneConfig.VkCopyCode.Value)) && _code != 0)
                GUIUtility.systemCopyBuffer = GameCode.IntToGameName(_code);
        }
        catch { }
    }

    private static void Leave()
    {
        try { if (AmongUsClient.Instance != null) AmongUsClient.Instance.ExitGame(DisconnectReasons.ExitGame); }
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

    private static bool InRoom() => LobbyBehaviour.Instance != null || ShipStatus.Instance != null;

    [HarmonyPatch(typeof(VoteBanSystem), "AddVote")]
    private static class VotePatch
    {
        public static void Postfix(int srcClient, int clientId)
        {
            try { OnVote(srcClient, clientId); } catch { }
            try { NocturneVoteTally.Add(srcClient, clientId); } catch { }
        }
    }

    [HarmonyPatch(typeof(LobbyBehaviour), "Start")]
    private static class ResetPatch
    {
        public static void Postfix()
        {
            _voters.Clear();
            NocturneVoteTally.Clear();
        }
    }
}

internal static class NocturneVoteTally
{
    private static readonly Dictionary<int, HashSet<int>> _hits = new Dictionary<int, HashSet<int>>();

    internal static void Add(int srcClient, int clientId)
    {
        if (clientId < 0 || srcClient < 0) return;
        if (!_hits.TryGetValue(clientId, out HashSet<int> set))
        {
            set = new HashSet<int>();
            _hits[clientId] = set;
        }
        set.Add(srcClient);
    }

    internal static int Count(int clientId)
    {
        return clientId >= 0 && _hits.TryGetValue(clientId, out HashSet<int> set) ? set.Count : 0;
    }

    internal static void Clear() => _hits.Clear();
}
