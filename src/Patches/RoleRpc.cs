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
