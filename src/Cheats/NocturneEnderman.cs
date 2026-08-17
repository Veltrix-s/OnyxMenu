using System.Collections;
using AmongUs.GameOptions;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using HarmonyLib;
using UnityEngine;

namespace Nocturne;

internal static class NocturneEnderman
{
    private static string _backup;
    private static Vector2 _backPos;

    internal static bool Running { get; private set; }

    private static bool Imp(PlayerControl pc)
    {
        try { return pc.Data != null && pc.Data.Role != null && pc.Data.Role.IsImpostor && pc.Data.Role.Role != RoleTypes.Viper; }
        catch { return false; }
    }

    private static bool Ready(out PlayerControl me)
    {
        me = PlayerControl.LocalPlayer;
        return me != null && me.Data != null && !me.Data.IsDead
            && ShipStatus.Instance != null && MeetingHud.Instance == null
            && me.NetTransform != null && Imp(me);
    }

    private static bool Target(PlayerControl t, PlayerControl me)
        => t != null && t.Data != null && !t.Data.Disconnected && !t.Data.IsDead
           && t != me && !t.inVent && !Imp(t);

    private static void Murder(PlayerControl me, PlayerControl t)
    {
        try
        {
            if (AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost) me.RpcMurderPlayer(t, true);
            else me.CmdCheckMurder(t);
        }
        catch { }
    }

    internal static string Kill(PlayerControl target)
    {
        if (Running) return NocturneText.T("Занято.", "Busy.");
        if (!Ready(out PlayerControl me)) return NocturneText.T("Только импостером, в матче.", "Impostor only, in match.");
        if (!Target(target, me)) return NocturneText.T("Нет цели.", "No target.");
        if (!NocturneConfig.BuffNoKillCd.Value && me.killTimer > 0f)
            return NocturneText.T("КД килла активен.", "Kill cooldown active.");

        try { ((MonoBehaviour)AmongUsClient.Instance).StartCoroutine(Co(target).WrapToIl2Cpp()); }
        catch { return NocturneText.T("Не удалось.", "Failed."); }
        return NocturneText.T("Эндермен: ", "Enderman: ") + NocturneNameColor.Strip(target.Data.PlayerName);
    }

    private static IEnumerator Co(PlayerControl target)
    {
        PlayerControl me = PlayerControl.LocalPlayer;
        Running = true;
        _backup = NocturneOutfits.Capture(me);
        _backPos = me.GetTruePosition();

        NocturneOutfits.Randomize(me);
        yield return new WaitForSeconds(0.05f);
        if (Interrupted()) { Restore(); yield break; }

        try { me.NetTransform.RpcSnapTo(target.GetTruePosition()); } catch { }
        yield return new WaitForSeconds(0.05f);
        if (Interrupted()) { Restore(); yield break; }

        Murder(me, target);

        float t = 0f;
        while (t < 0.35f)
        {
            if (Interrupted()) { Restore(); yield break; }
            t += Time.deltaTime;
            yield return null;
        }

        Restore();
    }

    private static bool Interrupted()
        => MeetingHud.Instance != null || PlayerControl.LocalPlayer == null || PlayerControl.LocalPlayer.Data == null;

    internal static void Restore()
    {
        if (!Running) return;
        Running = false;

        PlayerControl me = PlayerControl.LocalPlayer;
        if (me == null) return;

        try
        {
            if (me.NetTransform != null) { me.NetTransform.SnapTo(_backPos); me.NetTransform.RpcSnapTo(_backPos); }
        }
        catch { }
        try { if (!string.IsNullOrEmpty(_backup)) NocturneOutfits.Apply(me, _backup); } catch { }
    }
}

[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
internal static class NocturneEndermanMeetingPatch
{
    public static void Prefix() => NocturneEnderman.Restore();
}
