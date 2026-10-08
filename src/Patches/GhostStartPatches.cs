using HarmonyLib;
using UnityEngine;

namespace Nocturne.Patches;

internal static class GhostStart
{
    private static int tries = -1;
    private static float nextAt;

    private static bool On =>
        (NocturneConfig.GhostAfterStart.Value) ||
        (NocturneConfig.GameMaster.Value);

    internal static void Arm()
    {
        tries = On ? 0 : -1;
        nextAt = Time.realtimeSinceStartup + 1f;
    }

    internal static void Tick()
    {
        if (tries < 0) return;
        if (!On)
        {
            tries = -1;
            return;
        }
        if (Time.realtimeSinceStartup < nextAt) return;

        PlayerControl me = PlayerControl.LocalPlayer;
        if (IsDead(me))
        {
            tries = -1;
            return;
        }

        bool ready = ShipStatus.Instance != null && LobbyBehaviour.Instance == null
            && IntroCutscene.Instance == null && MeetingHud.Instance == null && ExileController.Instance == null
            && me != null && me.Data != null && me.Data.Role != null;
        if (!ready)
        {
            Retry();
            return;
        }

        if (Activate(me))
        {
            NocturneToast.Push(NocturneText.T("Призрак", "Ghost"), NocturneText.T("Режим призрака включён.", "Ghost mode enabled."), 2.5f, NocturneNotifyKind.Info);
            tries = -1;
            return;
        }
        Retry();
    }

    private static void Retry()
    {
        if (tries >= 60)
        {
            tries = -1;
            return;
        }
        tries++;
        nextAt = Time.realtimeSinceStartup + 0.5f;
    }

    internal static string Now()
    {
        PlayerControl me = PlayerControl.LocalPlayer;
        if (me == null || me.Data == null)
            return NocturneText.NoPlayer;
        if (ShipStatus.Instance == null || LobbyBehaviour.Instance != null)
            return NocturneText.T("Только в матче.", "In-match only.");
        if (MeetingHud.Instance != null || ExileController.Instance != null)
            return NocturneText.T("Не во время собрания.", "Not during a meeting.");
        if (IsDead(me)) return NocturneText.T("Ты уже мёртв.", "You are already dead.");
        if (!IsImpostor(me))
            return NocturneText.T("Только за предателя.", "Impostor only.");

        tries = -1;
        try
        {
            bool host = AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost;
            if (host)
                me.RpcMurderPlayer(me, true);
            else
                me.CmdCheckMurder(me);
        }
        catch
        {
            return NocturneText.Failed;
        }

        return NocturneText.T("Суицид отправлен.", "Suicide sent.");
    }

    private static bool IsImpostor(PlayerControl me)
    {
        try
        {
            return me.Data.Role != null && me.Data.Role.IsImpostor;
        }
        catch
        {
            return false;
        }
    }

    private static bool Activate(PlayerControl me)
    {
        if (IsDead(me))
            return true;

        try
        {
            me.Die(DeathReason.Exile, true);
        }
        catch { }
        return IsDead(me);
    }

    private static bool IsDead(PlayerControl me)
    {
        return me != null && me.Data != null && me.Data.IsDead;
    }
}

[HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.CoStartGame))]
internal static class GhostStartArmPatch
{
    public static void Postfix() => GhostStart.Arm();
}
