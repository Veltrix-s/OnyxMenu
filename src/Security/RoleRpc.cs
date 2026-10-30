using System.Collections.Generic;
using HarmonyLib;

namespace Nocturne;

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.HandleRoleRpc))]
internal static class RoleRpcGuard
{
    private static readonly HashSet<byte> Told = new HashSet<byte>();

    private static bool Prefix(PlayerControl __instance, [HarmonyArgument(0)] byte callId)
    {
        NetworkedPlayerInfo data = __instance.Data;
        if (data != null && data.Role != null) return true;

        if (Told.Add(callId))
            NocturnePlugin.Logger?.LogInfo($"role rpc {callId} dropped: client {__instance.OwnerId} has no {(data == null ? "Data" : "Role")}");
        return false;
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.HandleRpc))]
[HarmonyPriority(Priority.High)]
internal static class ChatNoteGuard
{
    private static bool Prefix([HarmonyArgument(0)] byte callId)
    {
        return callId != (byte)RpcCalls.SendChatNote || MeetingHud.Instance != null;
    }
}
