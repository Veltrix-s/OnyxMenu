using HarmonyLib;
using UnityEngine;

namespace Nocturne;

internal static class NocturneImpTrap
{
    private const float Gap = 1.5f;

    private static float _last = -99f;

    internal static void Fire(PlayerControl imp)
    {
        if (!NocturneConfig.ImpTrap.Value)
            return;
        if (imp == null || imp.Data == null)
            return;
        if (ShipStatus.Instance == null || LobbyBehaviour.Instance != null)
            return;
        if (MeetingHud.Instance != null || ExileController.Instance != null)
            return;

        float now = Time.unscaledTime;
        if (now - _last < Gap)
            return;

        int vent = Utils.NearestVentIndex(imp.GetTruePosition());
        if (vent < 0) return;
        _last = now;

        int n = 0;
        var e = PlayerControl.AllPlayerControls.GetEnumerator();
        while (e.MoveNext())
        {
            PlayerControl p = e.Current;
            if (p == null || p.Data == null || p.Data.Disconnected || p.Data.IsDead) continue;
            if (p.PlayerId == imp.PlayerId || p == PlayerControl.LocalPlayer) continue;
            NocturneVentTp.Send(p, vent);
            n++;
        }

        if (n > 0)
        {
            string who = NocturneNameColor.Strip(imp.Data.PlayerName ?? "?");
            NocturneToast.Push(NocturneText.T("Сбор", "Rally"),
                NocturneText.T($"{who} — стянуто: {n}", $"{who} — pulled: {n}"), 2.2f, NocturneNotifyKind.Warning);
        }
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.MurderPlayer))]
internal static class NocturneImpTrapKillPatch
{
    public static void Postfix(PlayerControl __instance) => NocturneImpTrap.Fire(__instance);
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.Shapeshift))]
internal static class NocturneImpTrapShiftPatch
{
    public static void Postfix(PlayerControl __instance) => NocturneImpTrap.Fire(__instance);
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.RpcShapeshift))]
internal static class NocturneImpTrapRpcShiftPatch
{
    public static void Postfix(PlayerControl __instance) => NocturneImpTrap.Fire(__instance);
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.HandleServerVanish))]
internal static class NocturneImpTrapVanishPatch
{
    public static void Postfix(PlayerControl __instance) => NocturneImpTrap.Fire(__instance);
}
