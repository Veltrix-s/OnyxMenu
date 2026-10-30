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
    private static int _joins;
    private static float _until;

    private static bool On => NocturneConfig.VkAutoRejoin.Value;

    internal static void OnVote(int srcClient, int clientId)
    {
        if (!On || _armed) return;
        InnerNetClient net = AmongUsClient.Instance;
        if (net == null || net.AmHost)
            return;
        if (clientId != net.ClientId || srcClient == net.ClientId)
            return;
        _voters.Add(srcClient);
        if (_voters.Count >= Threshold) Trigger();
    }

    private static void Trigger()
    {
        _armed = true;
        if (NocturneConfig.VkCopyCode.Value) QuickRejoin.CopyCode();
        NocturneToast.Push(NocturneText.T("Анти-войткик", "Anti-votekick"), NocturneText.T("Против тебя 2 голоса — перезахожу.", "2 votes on you, rejoining."), 3f, NocturneNotifyKind.Danger);

        if (!QuickRejoin.Reconnect(true))
        {
            QuickRejoin.CopyCode();
            NocturneToast.Push(NocturneText.T("Анти-войткик", "Anti-votekick"), NocturneText.T("Не смог перезайти — код скопирован, зайди вручную.", "Can't rejoin. Code copied, rejoin manually."), 4f, NocturneNotifyKind.Warning);
            Reset();
            return;
        }
        _joins = QuickRejoin.Joins;
        _until = Time.unscaledTime + 22f;
    }

    public void FixedUpdate()
    {
        if (!_armed) return;
        if (QuickRejoin.Joins != _joins)
        {
            Reset();
            return;
        }
        if (Time.unscaledTime < _until) return;

        QuickRejoin.CopyCode();
        NocturneToast.Push(NocturneText.T("Анти-войткик", "Anti-votekick"), NocturneText.T("Сам не зашёл — код скопирован, зайди вручную.", "Auto-join failed. Code copied, rejoin manually."), 4f, NocturneNotifyKind.Warning);
        Reset();
    }

    private static void Reset()
    {
        _armed = false;
        _voters.Clear();
    }

    internal static void ForgetVotes()
    {
        _voters.Clear();
        NocturneVoteTally.Clear();
    }

    [HarmonyPatch(typeof(VoteBanSystem), nameof(VoteBanSystem.AddVote))]
    private static class VotePatch
    {
        public static void Postfix(int srcClient, int clientId)
        {
            OnVote(srcClient, clientId);
            NocturneVoteTally.Add(srcClient, clientId);
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
