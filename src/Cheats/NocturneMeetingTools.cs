using System;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using InnerNet;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Nocturne;

internal static class NocturneMeetingTools
{
    private static bool Host => AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost;
    private static float _nukeNext;

    internal static bool AnticheatLive()
    {
        PlayerControl me = PlayerControl.LocalPlayer;
        return me != null && me.Data != null && me.Data.OwnerId != -2;
    }

    internal static void Tick()
    {
        if (NocturneConfig.NukeGame == null || !NocturneConfig.NukeGame.Value) return;
        if (Time.unscaledTime < _nukeNext) return;
        _nukeNext = Time.unscaledTime + 0.15f;
        RequestMeeting(PlayerControl.LocalPlayer, null, silent: true);
    }

    internal static string RequestMeeting(PlayerControl reporter, NetworkedPlayerInfo target, bool silent = false)
    {
        try
        {
            InnerNetClient net = (InnerNetClient)AmongUsClient.Instance;
            bool ready = reporter != null && reporter.Data != null && net != null
                && ShipStatus.Instance != null && MeetingHud.Instance == null && !reporter.Data.IsDead;
            if (!ready) return silent ? "" : WhyNotReady(reporter, net);

            bool anticheat = AnticheatLive();
            if (anticheat && net.GameState != InnerNetClient.GameStates.Started)
                return silent ? "" : NocturneText.T("Игра должна начаться.", "The game must have started.");

            try { reporter.RemainingEmergencies = 999999; } catch { }

            if (!net.AmHost)
            {
                if (anticheat && reporter != PlayerControl.LocalPlayer)
                    return silent ? "" : NocturneText.T("Только для хоста.", "Host only for another player.");

                reporter.CmdReportDeadBody(target);
                return NocturneText.T("Собрание вызвано.", "Meeting called.");
            }

            MeetingRoomManager.Instance?.AssignSelf(reporter, target);
            reporter.RpcStartMeeting(target);
            HudManager.Instance?.OpenMeetingRoom(reporter);
            return NocturneText.T("Собрание начато.", "Meeting started.");
        }
        catch (Exception ex) { return silent ? "" : NocturneText.T("Ошибка: ", "Error: ") + ex.Message; }
    }

    private static string WhyNotReady(PlayerControl reporter, InnerNetClient net)
    {
        if (reporter == null || reporter.Data == null) return NocturneText.T("Нет игрока.", "No player.");
        if (net == null) return NocturneText.T("Не подключен.", "Not connected.");
        if (ShipStatus.Instance == null) return NocturneText.T("Только в матче.", "In match only.");
        if (MeetingHud.Instance != null) return NocturneText.T("Собрание уже идёт.", "Meeting already in progress.");
        return NocturneText.T("Ты мёртв.", "You're dead.");
    }

    internal static string CallMeeting() => RequestMeeting(PlayerControl.LocalPlayer, null);

    internal static string CloseVoting()
    {
        if (!Host) return NocturneText.T("Только хост.", "Host only.");
        MeetingHud m = MeetingHud.Instance;
        if (m == null) return NocturneText.T("Нет собрания.", "No meeting.");
        try
        {
            MeetingHud.MeetingStates s = m.CurrentState;
            if (s != MeetingHud.MeetingStates.NotVoted && s != MeetingHud.MeetingStates.Voted)
                return NocturneText.T("Голосование не активно.", "Voting isn't active.");
            m.ForceSkipAll();
            return NocturneText.T("Голосование закрыто.", "Voting closed.");
        }
        catch { return NocturneText.T("Не удалось закрыть.", "Failed to close."); }
    }

    internal static string CloseMeeting()
    {
        if (!Host) return NocturneText.T("Только хост.", "Host only.");
        MeetingHud m = MeetingHud.Instance;
        if (m == null) return NocturneText.T("Нет собрания.", "No meeting.");
        try
        {
            m.RpcVotingComplete(new Il2CppStructArray<MeetingHud.VoterState>(0), (NetworkedPlayerInfo)null, true, false, 0);
            m.Close();
            m.RpcClose();
            return NocturneText.T("Собрание закрыто (без выгона).", "Meeting closed (no ejection).");
        }
        catch { return NocturneText.T("Не удалось закрыть.", "Failed to close."); }
    }

    internal static string Eject(PlayerControl t)
    {
        try
        {
            if (!Host) return NocturneText.T("Только хост.", "Host only.");
            if (t == null || t.Data == null || t.Data.Disconnected) return NocturneText.T("Нет цели.", "No target.");
            if (ShipStatus.Instance == null) return NocturneText.T("Только в матче.", "In match only.");

            HudManager hud = HudManager.Instance;
            if (hud == null || hud.MeetingPrefab == null) return NocturneText.T("Недоступно.", "Unavailable.");

            MeetingHud m = MeetingHud.Instance;
            if (m == null)
            {
                m = Object.Instantiate(hud.MeetingPrefab);
                MeetingHud.Instance = m;
                ((InnerNetClient)AmongUsClient.Instance).Spawn(((Il2CppObjectBase)m).Cast<InnerNetObject>(), -2, (SpawnFlags)0);
            }

            m.RpcVotingComplete(new Il2CppStructArray<MeetingHud.VoterState>(0), t.Data, false, false, 0);
            m.RpcClose();
            return NocturneText.T("Выгнан: ", "Ejected: ") + t.Data.PlayerName;
        }
        catch { return NocturneText.T("Не удалось.", "Failed."); }
    }
}

[HarmonyPatch(typeof(EmergencyMinigame), nameof(EmergencyMinigame.Begin))]
internal static class NocturneUnlimitedMeetingsPatch
{
    private static void Prefix()
    {
        if (PlayerControl.LocalPlayer == null) return;
        try { PlayerControl.LocalPlayer.RemainingEmergencies = 999999; } catch { }
    }
}
