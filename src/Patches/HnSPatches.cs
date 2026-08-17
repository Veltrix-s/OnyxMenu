using System;
using AmongUs.GameOptions;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes;
using InnerNet;
using UnityEngine;

namespace Nocturne.Patches;

internal static class HnSSeekers
{
    private const int ImpostorCountTitle = 133;
    private const int HideAndSeekGameMode = 2;
    private const int HideAndSeekAlternateGameMode = 4;
    private static bool _defaultApplied;

    private static bool Enabled => NocturneConfig.HideAndSeekTwoSeekers.Value;

    private static int Count
    {
        get
        {
            int c = NocturneConfig.SeekerCount.Value;
            return c < 1 ? 1 : (c > 15 ? 15 : c);
        }
    }

    internal static bool TryStep(NumberOption option, float direction)
    {
        if (!Enabled || !IsHnS() || !IsImpOption(option)) return HarmonyControl.Continue;
        option.ValidRange = new FloatRange(1f, Count);
        option.Value = Clamp(option.Value + option.Increment * direction, 1f, Count);
        Refresh(option);
        return HarmonyControl.SkipOriginal;
    }

    internal static void RelaxRange(NumberOption option)
    {
        if (!Enabled || !IsHnS() || !IsImpOption(option))
        {
            _defaultApplied = false;
            return;
        }

        option.ValidRange = new FloatRange(1f, Count);
        if (!_defaultApplied)
        {
            option.Value = Count;
            _defaultApplied = true;
        }
        else
        {
            option.Value = Clamp(option.Value, 1f, Count);
        }
    }

    internal static bool TryImpostorCount(ref int count)
    {
        if (!Enabled || !IsHnS()) return HarmonyControl.Continue;
        int players = CountAlive();
        count = players <= 1 ? 1 : Math.Min(Count, players - 1);
        return HarmonyControl.SkipOriginal;
    }

    internal static bool TryAssignSeekers(Il2CppSystem.Collections.Generic.List<NetworkedPlayerInfo> players, IGameOptions opts, RoleTeamTypes team, ref int teamMax)
    {
        if (!Enabled || !IsHnS() || !IsHost() || (int)team != 1) return HarmonyControl.Continue;

        try
        {
            if (players == null || players.Count <= 0) return HarmonyControl.SkipOriginal;

            int seekerCount = players.Count <= 1 ? 1 : Math.Min(Count, players.Count - 1);
            teamMax = seekerCount;
            SetHnSImpostorCount(seekerCount);

            int assigned = 0;
            while (assigned < seekerCount && players.Count > 0)
            {
                int index = UnityEngine.Random.Range(0, players.Count);
                NetworkedPlayerInfo info = players[index];
                if (info != null && info.Object != null)
                {
                    info.Object.RpcSetRole((RoleTypes)1, false);
                    assigned++;
                }

                players.RemoveAt(index);
            }

            return HarmonyControl.SkipOriginal;
        }
        catch
        {
            return HarmonyControl.Continue;
        }
    }

    private static void SetHnSImpostorCount(int count)
    {
        try
        {
            if (GameOptionsManager.Instance == null || GameOptionsManager.Instance.CurrentGameOptions == null) return;
            HideNSeekGameOptionsV10 options = ((Il2CppObjectBase)GameOptionsManager.Instance.CurrentGameOptions).Cast<HideNSeekGameOptionsV10>();
            if (options != null) options.NumImpostors = count;
        }
        catch { }
    }

    private static int CountAlive()
    {
        int count = 0;
        try
        {
            var cursor = PlayerControl.AllPlayerControls.GetEnumerator();
            while (cursor.MoveNext())
            {
                PlayerControl p = cursor.Current;
                if (p != null && p.Data != null && !p.Data.Disconnected && !p.Data.IsDead) count++;
            }
        }
        catch { }
        return count;
    }

    private static bool IsImpOption(NumberOption option) => option != null && (int)((OptionBehaviour)option).Title == ImpostorCountTitle;

    private static bool IsHost()
    {
        try { return AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost; }
        catch { return false; }
    }

    internal static bool IsHnS()
    {
        try
        {
            if (GameManager.Instance != null && GameManager.Instance.IsHideAndSeek()) return true;
            if (GameOptionsManager.Instance == null || GameOptionsManager.Instance.CurrentGameOptions == null) return false;
            int mode = (int)GameOptionsManager.Instance.CurrentGameOptions.GameMode;
            return mode == HideAndSeekGameMode || mode == HideAndSeekAlternateGameMode;
        }
        catch { return false; }
    }

    private static float Clamp(float v, float min, float max) => v < min ? min : (v > max ? max : v);

    private static void Refresh(NumberOption option)
    {
        option.UpdateValue();
        ((OptionBehaviour)option).OnValueChanged.Invoke((OptionBehaviour)(object)option);
        option.AdjustButtonsActiveState();
    }
}

[HarmonyPatch(typeof(NumberOption), nameof(NumberOption.Increase))]
internal static class HnSNumberIncreasePatch
{
    public static bool Prefix(NumberOption __instance)
    {
        try
        {
            if (HnSSeekers.TryStep(__instance, 1f) == HarmonyControl.SkipOriginal) return HarmonyControl.SkipOriginal;
            return NocturneHostOptions.TryStep(__instance, 1f);
        }
        catch { return HarmonyControl.Continue; }
    }
}

[HarmonyPatch(typeof(NumberOption), nameof(NumberOption.Decrease))]
internal static class HnSNumberDecreasePatch
{
    public static bool Prefix(NumberOption __instance)
    {
        try
        {
            if (HnSSeekers.TryStep(__instance, -1f) == HarmonyControl.SkipOriginal) return HarmonyControl.SkipOriginal;
            return NocturneHostOptions.TryStep(__instance, -1f);
        }
        catch { return HarmonyControl.Continue; }
    }
}

[HarmonyPatch(typeof(NumberOption), nameof(NumberOption.Initialize))]
internal static class HnSNumberInitPatch
{
    public static void Postfix(NumberOption __instance)
    {
        try { HnSSeekers.RelaxRange(__instance); NocturneHostOptions.RelaxRange(__instance); } catch { }
    }
}

[HarmonyPatch(typeof(NumberOption), nameof(NumberOption.AdjustButtonsActiveState))]
internal static class HnSNumberAdjustPatch
{
    public static void Prefix(NumberOption __instance)
    {
        try { HnSSeekers.RelaxRange(__instance); NocturneHostOptions.RelaxRange(__instance); } catch { }
    }
}

[HarmonyPatch(typeof(IGameOptionsExtensions), nameof(IGameOptionsExtensions.GetAdjustedNumImpostors))]
internal static class HnSImpostorCountPatch
{
    public static bool Prefix(ref int __result)
    {
        try
        {
            if (LobbyBehaviour.Instance == null && ShipStatus.Instance == null) return HarmonyControl.Continue;
            if (HnSSeekers.TryImpostorCount(ref __result) == HarmonyControl.SkipOriginal) return HarmonyControl.SkipOriginal;
            return NocturneHostOptions.TryImpostorCount(ref __result);
        }
        catch { return HarmonyControl.Continue; }
    }
}

[HarmonyPatch(typeof(LogicRoleSelectionHnS), nameof(LogicRoleSelectionHnS.AssignRolesForTeam))]
internal static class HnSRoleSelectionPatch
{
    public static bool Prefix(Il2CppSystem.Collections.Generic.List<NetworkedPlayerInfo> players, IGameOptions opts, RoleTeamTypes team, ref int teamMax)
    {
        try { return HnSSeekers.TryAssignSeekers(players, opts, team, ref teamMax); }
        catch { return HarmonyControl.Continue; }
    }
}

[HarmonyPatch(typeof(LogicOptionsHnS), nameof(LogicOptionsHnS.GetCrewmateLeadTime))]
internal static class HnSLeadTimePatch
{
    public static void Postfix(ref int __result)
    {
        if (NocturneConfig.SeekerInstantStart.Value) __result = 0;
    }
}
