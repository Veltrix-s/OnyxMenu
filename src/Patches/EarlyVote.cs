using HarmonyLib;

namespace Nocturne.Patches;

internal static class EarlyVote
{
    internal static float Shift(MeetingHud m)
    {
        var opts = GameManager.Instance.LogicOptions.TryCast<LogicOptionsNormal>();
        float gap = opts == null ? 0f : opts.GetDiscussionTime() - m.discussionTimer;
        if (gap <= 0f) return 0f;

        m.discussionTimer += gap;
        return gap;
    }

    internal static void Unshift(MeetingHud m, float gap) => m.discussionTimer -= gap;
}

[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Update))]
internal static class EarlyVoteUpdatePatch
{
    public static void Prefix(MeetingHud __instance, out float __state)
    {
        __state = 0f;
        if (NocturneConfig.EarlyVote.Value && __instance.CurrentState == MeetingHud.MeetingStates.Discussion)
            __state = EarlyVote.Shift(__instance);
    }

    public static void Postfix(MeetingHud __instance, float __state) => EarlyVote.Unshift(__instance, __state);
}

[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Select))]
internal static class EarlyVoteSelectPatch
{
    public static void Prefix(MeetingHud __instance, out float __state)
    {
        __state = NocturneConfig.EarlyVote.Value ? EarlyVote.Shift(__instance) : 0f;
    }

    public static void Postfix(MeetingHud __instance, float __state) => EarlyVote.Unshift(__instance, __state);
}
