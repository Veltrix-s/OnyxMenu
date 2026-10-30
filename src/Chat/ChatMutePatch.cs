using HarmonyLib;

namespace Nocturne.Patches;

[HarmonyPatch(typeof(ChatController), nameof(ChatController.AddChat))]
internal static class NocturneChatMutePatch
{
    public static bool Prefix([HarmonyArgument(0)] PlayerControl sourcePlayer) => !NocturneMuteList.Hides(sourcePlayer);
}
