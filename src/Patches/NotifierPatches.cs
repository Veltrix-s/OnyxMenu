using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using InnerNet;
using UnityEngine;

namespace Nocturne.Patches;

internal static class Notes
{
    private static readonly List<string> Names = new List<string>(6);
    private static readonly List<string> Vals = new List<string>(6);
    private static readonly StringBuilder Sb = new StringBuilder(160);

    private static string _who;
    private static DisconnectReasons _why;
    private static float _whoAt;
    private static float _setAt;

    internal static bool On => NocturneConfig.Toasts.Value && NocturneConfig.VanillaToasts.Value;

    internal static void Remember(PlayerControl pc, DisconnectReasons reason)
    {
        _who = pc != null && pc.Data != null ? pc.Data.PlayerName : null;
        _why = reason;
        _whoAt = Time.unscaledTime;
    }

    internal static void Leave(string item)
    {
        bool known = _who != null && Time.unscaledTime - _whoAt < 1f;
        string who = _who;
        _who = null;
        if (!known)
        {
            NocturneToast.Push(item, null, 3.5f, NocturneNotifyKind.Warning, NocturneIcon.Door);
            return;
        }

        switch (_why)
        {
            case DisconnectReasons.Banned:
                NocturneToast.Push(who, NocturneText.T("забанен хостом ", "banned by ") + HostName(), 5f, NocturneNotifyKind.Danger);
                break;
            case DisconnectReasons.Kicked:
                NocturneToast.Push(who, NocturneText.T("кикнут хостом ", "kicked by ") + HostName(), 5f, NocturneNotifyKind.Danger);
                break;
            case DisconnectReasons.ExitGame:
                NocturneToast.Push(who, NocturneText.T("вышел из игры", "left the game"), 3.5f, NocturneNotifyKind.Warning, NocturneIcon.Door);
                break;
            default:
                NocturneToast.Push(who, NocturneText.T("вылетел из игры", "dropped out"), 3.5f, NocturneNotifyKind.Warning, NocturneIcon.Door);
                break;
        }
    }

    internal static void Setting(string name, string value)
    {
        float now = Time.unscaledTime;
        if (now - _setAt > 0.6f)
        {
            Names.Clear();
            Vals.Clear();
        }
        _setAt = now;

        int at = Names.IndexOf(name);
        if (at < 0)
        {
            Names.Add(name);
            Vals.Add(value);
        }
        else
            Vals[at] = value;

        if (Names.Count == 1)
        {
            NocturneToast.Push(name, value, 2.6f, NocturneNotifyKind.Info, NocturneIcon.Tune, "setting");
            return;
        }

        Sb.Clear();
        for (int i = 0; i < Names.Count; i++)
        {
            if (i > 0) Sb.Append(" · ");
            Sb.Append(Names[i]).Append(": ").Append(Vals[i]);
        }
        NocturneToast.Push(NocturneText.T("Настройки лобби", "Lobby settings"), Sb.ToString(), 3.2f, NocturneNotifyKind.Info, NocturneIcon.Tune, "setting");
    }

    private static string HostName()
    {
        ClientData host = AmongUsClient.Instance.GetHost();
        if (host == null || host.Character == null || host.Character.Data == null) return "?";
        return host.Character.Data.PlayerName;
    }
}

[HarmonyPatch(typeof(GameData), nameof(GameData.HandleDisconnect), new Type[] { typeof(PlayerControl), typeof(DisconnectReasons) })]
internal static class LeaveReasonPatch
{
    public static void Prefix(PlayerControl player, DisconnectReasons reason)
    {
        Notes.Remember(player, reason);
    }
}

[HarmonyPatch(typeof(NotificationPopper), nameof(NotificationPopper.AddDisconnectMessage))]
internal static class LeaveNotePatch
{
    public static bool Prefix(NotificationPopper __instance, string item)
    {
        if (!Notes.On) return HarmonyControl.Continue;

        Notes.Leave(item);
        SoundManager.Instance.PlaySound(__instance.playerDisconnectSound, false);
        return HarmonyControl.SkipOriginal;
    }
}

[HarmonyPatch(typeof(NotificationPopper), nameof(NotificationPopper.AddSettingsChangeMessage))]
internal static class SettingNotePatch
{
    public static bool Prefix(NotificationPopper __instance, StringNames key, string value, bool playSound)
    {
        if (!Notes.On) return HarmonyControl.Continue;

        Notes.Setting(TranslationController.Instance.GetString(key), value);
        if (playSound)
            SoundManager.Instance.PlaySoundImmediate(__instance.settingsChangeSound, false);
        return HarmonyControl.SkipOriginal;
    }
}

[HarmonyPatch(typeof(NotificationPopper), nameof(NotificationPopper.AddRoleSettingsChangeMessage))]
internal static class RoleNotePatch
{
    public static bool Prefix(NotificationPopper __instance, StringNames key, int roleCount, int roleChance, bool playSound)
    {
        if (!Notes.On) return HarmonyControl.Continue;

        Notes.Setting(TranslationController.Instance.GetString(key), roleCount + " · " + roleChance + "%");
        if (playSound)
            SoundManager.Instance.PlaySoundImmediate(__instance.settingsChangeSound, false);
        return HarmonyControl.SkipOriginal;
    }
}
