using HarmonyLib;
using Nocturne.Patches;

namespace Nocturne;

[HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnGameJoined))]
internal static class SessionJoin
{
    public static void Prefix()
    {
        NocturneSpawnFlood.Grace();
        NocturneJoinLevels.Reset();
    }

    public static void Postfix()
    {
        ForeignMods.Reset();
        ModHandshake.Reset();
        QuickRejoin.Remember();
    }
}

[HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnGameEnd))]
internal static class SessionEnd
{
    public static void Postfix() => ModHandshake.Reset();
}

[HarmonyPatch(typeof(LobbyBehaviour), nameof(LobbyBehaviour.Start))]
internal static class SessionLobby
{
    public static void Prefix() => NocturneSpawnFlood.Grace();

    public static void Postfix()
    {
        NocturneDummies.Forget();
        NocturneForceRoles.Clear();
        NocturneOverheadChat.Clear();
        NocturneTwins.Prune();
        NocturneWhisper.Reset();
        VisualAssist.ResetNameCaches();
    }
}

[HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.Start))]
internal static class SessionShip
{
    public static void Prefix() => NocturneSpawnFlood.Grace();

    public static void Postfix()
    {
        LobbyMusicMutePatch.RestoreAll();
        NocturneJudgeOverrule.ForgetMatch();
        NocturneSeasonDecor.Forget();
        NocturneVentNetworkPatch.Reset();
    }
}

[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
internal static class SessionMeeting
{
    public static void Prefix()
    {
        NocturneEnderman.Restore();
        NocturnePhantomVanish.Cancel();
    }

    public static void Postfix()
    {
        NocturneJudgeOverrule.ForgetMeeting();
        NocturneSeeThrough.RestoreAll();
        RevealVotesPatch.ForgetAll();
    }
}
