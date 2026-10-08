using HarmonyLib;
using UnityEngine;

namespace Nocturne;

internal static class NocturneAntiFakeMeeting
{
    internal static bool On => NocturneConfig.BlockFakeMeetings.Value;

    internal static bool Illegal()
    {
        return ShipStatus.Instance == null || LobbyBehaviour.Instance != null;
    }

    internal static void Kill(MeetingHud hud)
    {
        try
        {
            if (hud != null) Object.Destroy(hud.gameObject);
        }
        catch { }

        NocturneSecurityNotify.Once("meeting", 1f, "Заблокирован фейк-митинг в лобби", "Blocked a fake lobby meeting");
    }
}

[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
internal static class NocturneFakeMeetingStartPatch
{
    public static bool Prefix(MeetingHud __instance)
    {
        if (!NocturneAntiFakeMeeting.On || !NocturneAntiFakeMeeting.Illegal())
            return true;
        NocturneAntiFakeMeeting.Kill(__instance);
        return false;
    }
}
