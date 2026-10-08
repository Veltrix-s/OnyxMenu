using HarmonyLib;
using TMPro;

namespace Nocturne.Patches;

[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Update))]
internal static class NocturneRevealRolesMeetingPatch
{
    public static void Postfix(MeetingHud __instance)
    {
        try
        {
            if (!NocturneConfig.RevealRoles.Value)
                return;
            if (__instance == null || __instance.playerStates == null || GameData.Instance == null) return;

            foreach (PlayerVoteArea area in __instance.playerStates)
            {
                if (area == null || area.NameText == null) continue;

                NetworkedPlayerInfo info = GameData.Instance.GetPlayerById(area.PlayerId);
                if (info == null || info.Disconnected || info.DefaultOutfit == null) continue;

                string label = VisualAssist.RoleLabelForInfo(info);
                if (string.IsNullOrEmpty(label)) continue;

                TMP_Text txt = area.NameText;
                if (txt.text != null && txt.text.StartsWith("<size=58%>")) continue;

                txt.text = label + "\n" + (info.DefaultOutfit.PlayerName ?? "???");
            }
        }
        catch { }
    }
}
