using AmongUs.Data;
using AmongUs.Data.Player;
using HarmonyLib;
using InnerNet;

namespace Nocturne.Patches;

internal static class AccountGate
{
    internal static void OpenGuestResult(ref bool result)
    {
        if (NocturneConfig.RemoveGuestLimits.Value) result = true;
    }

    internal static void OpenGuestName(ref bool canSetName)
    {
        if (NocturneConfig.RemoveGuestLimits.Value) canSetName = true;
    }

    internal static void ClearMinorFlag(ref bool isMinorOrWaiting)
    {
        if (NocturneConfig.RemoveMinorLimits.Value) isMinorOrWaiting = false;
    }

    internal static void OpenMinorResult(ref bool result)
    {
        if (NocturneConfig.RemoveMinorLimits.Value) result = true;
    }

    internal static void MarkAccountReady()
    {
        if (NocturneConfig.RemoveMinorLimits.Value)
        {
            var account = DataManager.Player.Account;
            account.LoginStatus = (EOSManager.AccountLoginStatus)1;
        }
    }

    internal static bool ShouldNeutralizeDisconnectPenalty()
    {
        if (!NocturneConfig.ClearDisconnectPenalty.Value || AmongUsClient.Instance == null)
        {
            return false;
        }

        InnerNetClient client = (InnerNetClient)AmongUsClient.Instance;
        return (int)client.NetworkMode == 1;
    }
}

[HarmonyPatch(typeof(EOSManager), "IsFreechatAllowed")]
internal static class GuestFreeChatPatch
{
    public static void Postfix(ref bool __result)
    {
        AccountGate.OpenGuestResult(ref __result);
    }
}

[HarmonyPatch(typeof(EOSManager), "IsFriendsListAllowed")]
internal static class GuestFriendListPatch
{
    public static void Postfix(ref bool __result)
    {
        AccountGate.OpenGuestResult(ref __result);
    }
}

[HarmonyPatch(typeof(FullAccount), "CanSetCustomName")]
internal static class GuestNamePatch
{
    public static void Prefix(ref bool canSetName)
    {
        AccountGate.OpenGuestName(ref canSetName);
    }
}

[HarmonyPatch(typeof(EOSManager), "IsMinorOrWaiting")]
internal static class MinorStatePatch
{
    public static void Postfix(ref bool __result)
    {
        AccountGate.ClearMinorFlag(ref __result);
    }
}

[HarmonyPatch(typeof(EOSManager), "IsAllowedOnline")]
internal static class EosOnlinePatch
{
    public static void Prefix(ref bool canOnline)
    {
        AccountGate.OpenMinorResult(ref canOnline);
    }
}

[HarmonyPatch(typeof(AccountManager), "CanPlayOnline")]
internal static class AccountOnlinePatch
{
    public static void Postfix(ref bool __result)
    {
        AccountGate.OpenMinorResult(ref __result);
    }
}

[HarmonyPatch(typeof(InnerNetClient), "JoinGame")]
internal static class JoinLoginStatePatch
{
    public static void Prefix()
    {
        AccountGate.MarkAccountReady();
    }
}

[HarmonyPatch(typeof(PlayerBanData), "BanPoints", MethodType.Setter)]
internal static class DisconnectPenaltyPatch
{
    public static bool Prefix(ref float value)
    {
        if (!AccountGate.ShouldNeutralizeDisconnectPenalty())
        {
            return HarmonyControl.Continue;
        }

        value = 0;
        return HarmonyControl.SkipOriginal;
    }
}
