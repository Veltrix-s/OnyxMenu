using HarmonyLib;
using UnityEngine;

namespace Nocturne;

internal static class NocturneAntiFakeMeeting
{
    private static float lastNote;

    internal static bool On => NocturneConfig.BlockFakeMeetings != null && NocturneConfig.BlockFakeMeetings.Value;

    internal static bool Illegal()
    {
        try { return ShipStatus.Instance == null || LobbyBehaviour.Instance != null; }
        catch { return false; }
    }

    internal static void Kill(MeetingHud hud)
    {
        try { if (hud != null) Object.Destroy(hud.gameObject); } catch { }

        if (Time.unscaledTime - lastNote < 1f) return;
        lastNote = Time.unscaledTime;
        NocturneSecurityNotify.Fire("Заблокирован фейк-митинг в лобби", "Blocked a fake lobby meeting");
    }
}

[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
internal static class NocturneFakeMeetingStartPatch
{
    public static bool Prefix(MeetingHud __instance)
    {
        if (!NocturneAntiFakeMeeting.On || !NocturneAntiFakeMeeting.Illegal()) return true;
        NocturneAntiFakeMeeting.Kill(__instance);
        return false;
    }
}
