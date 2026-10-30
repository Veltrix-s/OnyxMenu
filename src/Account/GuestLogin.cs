using AmongUs.Data;
using HarmonyLib;
using Il2CppInterop.Runtime;
using InnerNet;

namespace Nocturne.Patches;

[HarmonyPatch(typeof(EOSManager), nameof(EOSManager.BeginLoginFlowWithDeviceID))]
internal static class GuestLoginPatch
{
    public static bool Prefix(EOSManager __instance)
    {
        if (!NocturneConfig.GuestMode.Value)
            return HarmonyControl.Continue;

        NocturnePlugin.Logger?.LogInfo("[Guest] starting temp account flow");
        __instance.StartTempAccountFlow();
        __instance.CloseStartupWaitScreen();
        return HarmonyControl.SkipOriginal;
    }
}

[HarmonyPatch(typeof(EOSManager), nameof(EOSManager.EndFinalPartsOfLoginFlow))]
internal static class GuestFinishPatch
{
    internal static bool Active;

    public static void Prefix(out bool __state)
    {
        __state = NocturneConfig.GuestMode.Value && DataManager.Player.Account.LoginStatus == EOSManager.AccountLoginStatus.TempAccount;
        if (!__state)
            return;

        Active = true;
        DataManager.Player.Account.LoginStatus = EOSManager.AccountLoginStatus.LoggedIn;
    }

    public static void Postfix(EOSManager __instance, bool __state)
    {
        if (!__state)
            return;

        DataManager.Settings.Multiplayer.ChatMode = QuickChatModes.QuickChatOnly;

        var logged = new System.Action(() => NocturnePlugin.Logger?.LogInfo("[Guest] device id removed"));
        __instance.DeleteDeviceID(DelegateSupport.ConvertDelegate<Il2CppSystem.Action>(logged));
        NocturneToast.Push(NocturneText.T("Режим гостя", "Guest mode"), NocturneText.T("Вход выполнен гостем, лобби только с быстрым чатом.", "Signed in as a guest, lobbies are Quick Chat only."), 3f, NocturneNotifyKind.Info);
    }
}
