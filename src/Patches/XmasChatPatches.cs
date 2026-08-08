using HarmonyLib;

namespace Nocturne.Patches;

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.RpcSendChat))]
internal static class XmasSendPatch
{
    public static bool Prefix(PlayerControl __instance, [HarmonyArgument(0)] string chatText)
    {
        try
        {
            if (__instance != PlayerControl.LocalPlayer || !NocturneXmas.IsCommand(chatText)) return true;
            bool on = NocturneXmas.Toggle(__instance.PlayerId);
            NocturneToast.Push(NocturneText.T("Ёлка", "Xmas"),
                on ? NocturneText.T("Цвета переливаются!", "Colors cycling!") : NocturneText.T("Остановлено.", "Stopped."),
                2f, NocturneNotifyKind.Info);
            return true;
        }
        catch { return true; }
    }
}

[HarmonyPatch(typeof(ChatController), nameof(ChatController.AddChat))]
internal static class XmasChatCommandPatch
{
    public static bool Prefix([HarmonyArgument(0)] PlayerControl sourcePlayer, [HarmonyArgument(1)] string chatText)
    {
        try
        {
            if (sourcePlayer == null || !NocturneXmas.IsCommand(chatText)) return true;
            if (sourcePlayer == PlayerControl.LocalPlayer) return true;
            if (NocturneConfig.ChatCmdXmas != null && NocturneConfig.ChatCmdXmas.Value)
                NocturneXmas.Toggle(sourcePlayer.PlayerId);
            return true;
        }
        catch { return true; }
    }
}
