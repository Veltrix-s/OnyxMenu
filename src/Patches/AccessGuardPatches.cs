using HarmonyLib;
using InnerNet;
using UnityEngine;

namespace Nocturne.Patches;

[HarmonyPatch(typeof(AmongUsClient), "OnPlayerJoined")]
internal static class NocturneAccessJoinPatch
{
    public static void Postfix(AmongUsClient __instance, [HarmonyArgument(0)] ClientData client)
        => NocturneAccess.Enforce((InnerNetClient)__instance, client);
}

[HarmonyPatch(typeof(VoteBanSystem), "AddVote")]
internal static class NocturneVoteKickPatch
{
    public static bool Prefix([HarmonyArgument(0)] int srcClient, [HarmonyArgument(1)] int clientId)
    {
        if (!NocturneConfig.VoteKickProtect.Value || AmongUsClient.Instance == null) return true;
        var net = (InnerNetClient)AmongUsClient.Instance;
        if (!net.AmHost) return true;

        string action = NocturneConfig.VoteKickAction.Value;
        if (!string.Equals(action, "Null", System.StringComparison.OrdinalIgnoreCase))
            NocturneAccess.Act(net, srcClient, action, NocturneAccess.ClientName(net, srcClient), NocturneText.T($"войт-кик → {NocturneAccess.ClientName(net, clientId)}", $"vote-kick → {NocturneAccess.ClientName(net, clientId)}"));

        return false;
    }
}

[HarmonyPatch(typeof(InnerNetClient), "KickPlayer")]
internal static class NocturneKickSelfGuardPatch
{
    public static bool Prefix(InnerNetClient __instance, int clientId)
    {
        if (__instance == null || !__instance.AmHost) return true;
        return clientId != __instance.ClientId && clientId != __instance.HostId;
    }
}

public sealed class NocturneAccessGuard : MonoBehaviour
{
    private float _next;

    public void Update()
    {
        if (Time.realtimeSinceStartup < _next) return;
        _next = Time.realtimeSinceStartup + 0.6f;

        if (AmongUsClient.Instance == null || LobbyBehaviour.Instance == null) return;
        var net = (InnerNetClient)AmongUsClient.Instance;
        if (!net.AmHost || net.allClients == null) return;
        bool fg = NocturneConfig.KickFortegreen.Value;
        bool colorRes = NocturneConfig.ColorReservationsEnabled != null && NocturneConfig.ColorReservationsEnabled.Value;
        if (!fg && !colorRes && !NocturneConfig.AccessBanEnabled.Value && !NocturneConfig.AccessWhitelistOnly.Value
            && !NocturneConfig.AccessNickBanEnabled.Value && !NocturneConfig.MinLevelEnabled.Value && !NocturneConfig.MaxLevelEnabled.Value) return;

        try
        {
            var e = net.allClients.GetEnumerator();
            while (e.MoveNext())
            {
                ClientData c = e.Current;
                if (c == null) continue;
                if (colorRes) NocturneColorReservations.TryApplyOnJoin(c.Character);
                if (fg && c.Id != net.ClientId && c.Id != net.HostId && IsFortegreen(c))
                {
                    NocturneAccess.Kick(net, c.Id, false);
                    NocturneToast.Push("Fortegreen", NocturneAccess.SafeName(c), 2.5f, NocturneNotifyKind.Warning);
                    continue;
                }
                NocturneAccess.Enforce(net, c);
            }
        }
        catch { }
    }

    private static bool IsFortegreen(ClientData c)
    {
        try { return c.Character != null && c.Character.CurrentOutfit != null && c.Character.CurrentOutfit.ColorId == 18; }
        catch { return false; }
    }
}
