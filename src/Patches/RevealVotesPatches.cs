using System.Collections.Generic;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace Nocturne.Patches;

[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Update))]
internal static class RevealVotesPatch
{
    private static readonly HashSet<byte> _drawn = new HashSet<byte>();
    private static readonly Dictionary<byte, byte> _targets = new Dictionary<byte, byte>();

    internal static Dictionary<byte, byte> Targets => _targets;

    internal static void ResetDrawn() => _drawn.Clear();
    internal static void ForgetAll() { _drawn.Clear(); _targets.Clear(); }

    private static void Remember(byte voter, byte votedFor)
    {
        if (votedFor == PlayerVoteArea.HasNotVoted || votedFor == PlayerVoteArea.MissedVote || votedFor == PlayerVoteArea.DeadVote)
            _targets.Remove(voter);
        else _targets[voter] = votedFor;
    }

    public static void Postfix(MeetingHud __instance)
    {
        if (!NocturneConfig.RevealVotes.Value || __instance == null || __instance.playerStates == null || GameData.Instance == null) return;
        try
        {
            if ((int)__instance.state >= 4) return;

            foreach (PlayerVoteArea voter in __instance.playerStates)
            {
                if (voter == null) continue;
                Remember(voter.TargetPlayerId, voter.VotedFor);
                if (_drawn.Contains(voter.TargetPlayerId)) continue;
                byte tgt = _targets.TryGetValue(voter.TargetPlayerId, out byte t) ? t : voter.VotedFor;
                DrawVote(__instance, voter.TargetPlayerId, tgt);
            }
            ShowSprites(__instance);
        }
        catch { }
    }

    internal static void DrawVote(MeetingHud hud, byte voterId, byte votedFor)
    {
        if (hud == null || hud.playerStates == null || GameData.Instance == null || _drawn.Contains(voterId)) return;
        if (votedFor == PlayerVoteArea.HasNotVoted || votedFor == PlayerVoteArea.MissedVote || votedFor == PlayerVoteArea.DeadVote) return;
        NetworkedPlayerInfo info = GameData.Instance.GetPlayerById(voterId);
        if (info == null || info.Disconnected) return;
        _drawn.Add(voterId);

        if (votedFor == PlayerVoteArea.SkippedVote)
        {
            if (hud.SkippedVoting != null) { hud.BloopAVoteIcon(info, 0, hud.SkippedVoting.transform); Paint(hud.SkippedVoting.transform, info); }
            return;
        }

        foreach (PlayerVoteArea target in hud.playerStates)
        {
            if (target == null || target.TargetPlayerId != votedFor) continue;
            hud.BloopAVoteIcon(info, 0, target.transform);
            Paint(target.transform, info);
            break;
        }
    }

    private static void Paint(Transform holder, NetworkedPlayerInfo voter)
    {
        if (!NocturneConfig.RevealAnonVotes.Value) return;
        try
        {
            if (holder == null || voter == null || voter.DefaultOutfit == null) return;
            int colorId = voter.DefaultOutfit.ColorId;
            VoteSpreader spread = holder.GetComponent<VoteSpreader>();
            if (spread == null || spread.Votes == null || spread.Votes.Count == 0) return;
            SpriteRenderer sr = spread.Votes[spread.Votes.Count - 1];
            if (sr == null) return;
            PlayerMaterial.SetColors(colorId, sr);
            sr.color = Color.white;
        }
        catch { }
    }

    internal static void ShowSprites(MeetingHud hud)
    {
        if (hud == null || hud.playerStates == null) return;
        foreach (PlayerVoteArea area in hud.playerStates)
        {
            if (area == null) continue;
            VoteSpreader spread = area.transform.GetComponent<VoteSpreader>();
            if (spread == null) continue;
            foreach (SpriteRenderer sr in spread.Votes) sr.gameObject.SetActive(true);
        }
        if (hud.SkippedVoting != null) hud.SkippedVoting.SetActive(true);
    }

    internal static void ClearIcons(MeetingHud hud)
    {
        if (hud == null || hud.playerStates == null) return;
        foreach (PlayerVoteArea area in hud.playerStates)
        {
            if (area == null) continue;
            VoteSpreader spread = area.transform.GetComponent<VoteSpreader>();
            if (spread == null || spread.Votes == null || spread.Votes.Count == 0) continue;
            foreach (SpriteRenderer sr in spread.Votes) if (sr != null) UnityEngine.Object.DestroyImmediate(sr.gameObject);
            spread.Votes.Clear();
        }
    }
}

[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.PopulateResults))]
internal static class RevealVotesResultsPatch
{
    public static void Prefix(MeetingHud __instance)
    {
        if (!NocturneConfig.RevealVotes.Value) return;
        try { RevealVotesPatch.ClearIcons(__instance); RevealVotesPatch.ResetDrawn(); } catch { }
    }

    public static void Postfix(MeetingHud __instance, Il2CppStructArray<MeetingHud.VoterState> states)
    {
        if (!NocturneConfig.RevealVotes.Value) return;
        try
        {
            RevealVotesPatch.ClearIcons(__instance);
            RevealVotesPatch.ResetDrawn();
            if (states != null)
            {
                for (int i = 0; i < states.Length; i++)
                {
                    MeetingHud.VoterState st = states[i];
                    byte tgt = RevealVotesPatch.Targets.TryGetValue(st.VoterId, out byte t) ? t : st.VotedForId;
                    RevealVotesPatch.DrawVote(__instance, st.VoterId, tgt);
                }
            }
            RevealVotesPatch.ShowSprites(__instance);
        }
        catch { }
    }
}

[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
internal static class RevealVotesStartPatch
{
    public static void Postfix() => RevealVotesPatch.ForgetAll();
}
