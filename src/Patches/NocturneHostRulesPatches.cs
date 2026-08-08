using HarmonyLib;

namespace Nocturne;

internal static class NocturneHostRules
{
    internal static bool Host => AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost;
    internal static bool VoteImmune => NocturneConfig.VoteImmune != null && NocturneConfig.VoteImmune.Value;
    internal static bool NoReports => NocturneConfig.NoReports != null && NocturneConfig.NoReports.Value;
}

[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.RpcVotingComplete))]
internal static class NocturneVoteImmunePatch
{
    public static void Prefix([HarmonyArgument(1)] ref NetworkedPlayerInfo exiledPlayer)
    {
        if (!NocturneHostRules.VoteImmune || !NocturneHostRules.Host) return;
        PlayerControl me = PlayerControl.LocalPlayer;
        if (me == null || me.Data == null) return;
        if (exiledPlayer != null && exiledPlayer.PlayerId == me.Data.PlayerId) exiledPlayer = null;
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.ReportDeadBody))]
internal static class NocturneNoReportsPatch
{
    public static bool Prefix() => !(NocturneHostRules.NoReports && NocturneHostRules.Host);
}
