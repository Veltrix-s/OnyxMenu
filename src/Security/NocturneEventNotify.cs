using System;
using HarmonyLib;
using UnityEngine;

namespace Nocturne;

public sealed class NocturneEventNotify : MonoBehaviour
{
    private static readonly SystemTypes[] SabSys =
    {
        SystemTypes.Reactor, SystemTypes.LifeSupp, SystemTypes.Electrical, SystemTypes.Comms, SystemTypes.Laboratory
    };
    private static readonly bool[] SabPrev = new bool[5];

    private float _poll;

    internal static bool On => NocturneConfig.EventNotify.Value;

    internal static void Fire(NocturneEventCat cat, string ru, string en, NocturneNotifyKind kind, bool toast)
    {
        string msg = NocturneText.T(ru, en);
        NocturneEventLog.Add(msg, kind, cat);
        if (!toast || !On) return;
        NocturneToast.Push(NocturneText.T("Событие", "Event"), msg, 3f, kind);
        if (NocturneConfig.EventNotifyChat.Value)
        {
            try
            {
                if (HudManager.Instance != null && HudManager.Instance.Chat != null && PlayerControl.LocalPlayer != null)
                    HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, msg);
            }
            catch { }
        }
    }

    internal static string ByClient(int clientId)
    {
        foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
            if (pc != null && pc.OwnerId == clientId && pc.Data != null)
                return pc.Data.PlayerName;
        return "?";
    }

    internal static string PName(PlayerControl pc)
    {
        return pc != null && pc.Data != null ? pc.Data.PlayerName : "?";
    }

    private static bool Mine(int clientId)
    {
        return PlayerControl.LocalPlayer != null && PlayerControl.LocalPlayer.OwnerId == clientId;
    }

    public void FixedUpdate()
    {
        float t = Time.unscaledTime;
        if (t < _poll) return;
        _poll = t + 0.1f;

        if (ShipStatus.Instance == null || MeetingHud.Instance != null)
        {
            for (int i = 0; i < SabPrev.Length; i++)
                SabPrev[i] = false;
            return;
        }

        bool toast = NocturneConfig.NotifySabotage.Value;
        for (int i = 0; i < SabSys.Length; i++)
        {
            bool now = IsSab(SabSys[i]);
            if (now && !SabPrev[i])
            {
                Fire(NocturneEventCat.Sabotage, "⚠ Саботаж: " + SabRu(SabSys[i]), "⚠ Sabotage: " + SabEn(SabSys[i]), NocturneNotifyKind.Danger, toast);
                try
                {
                    NocturneReplay.Rec(NocturneReplay.Rt.Sabotage, 255, Ui.Zero2, Ui.Zero2);
                }
                catch { }
            }
            SabPrev[i] = now;
        }
    }

    private static bool IsSab(SystemTypes type)
    {
        var systems = ShipStatus.Instance.Systems;
        if (systems == null || !systems.ContainsKey(type)) return false;
        try
        {
            var sys = systems[type];
            if (sys == null) return false;
            var act = sys.TryCast<IActivatable>();
            return act != null && act.IsActive;
        }
        catch
        {
            return false;
        }
    }

    private static string SabRu(SystemTypes s) => s == SystemTypes.Reactor || s == SystemTypes.Laboratory ? "Реактор" : s == SystemTypes.LifeSupp ? "Кислород" : s == SystemTypes.Electrical ? "Свет" : s == SystemTypes.Comms ? "Связь" : "Саботаж";
    private static string SabEn(SystemTypes s) => s == SystemTypes.Reactor || s == SystemTypes.Laboratory ? "Reactor" : s == SystemTypes.LifeSupp ? "Oxygen" : s == SystemTypes.Electrical ? "Lights" : s == SystemTypes.Comms ? "Comms" : "Sabotage";

    [HarmonyPatch(typeof(VoteBanSystem), nameof(VoteBanSystem.AddVote))]
    private static class VkPatch
    {
        private static float _lastToast;

        public static void Postfix(int __0, int __1)
        {
            string a = ByClient(__0), b = ByClient(__1);
            float now = Time.unscaledTime;
            bool toast = NocturneConfig.NotifyVotekick.Value && !Mine(__0) && now - _lastToast >= 15f;
            if (toast) _lastToast = now;
            Fire(NocturneEventCat.Meeting, $"Войткик: {a} → {b}", $"Votekick: {a} → {b}", NocturneNotifyKind.Warning, toast);
        }
    }

    private static void Attempt(PlayerControl killer, PlayerControl target)
    {
        if (target != PlayerControl.LocalPlayer || killer == target) return;
        if (!NocturneGodMode.On) return;

        string k = PName(killer);
        Fire(NocturneEventCat.Kill, $"Попытка убийства: {k}", $"Kill attempt: {k}", NocturneNotifyKind.Warning, true);
    }

    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.MurderPlayer), new Type[] { typeof(PlayerControl), typeof(MurderResultFlags) })]
    private static class KillPatch
    {
        public static void Postfix(PlayerControl __instance, PlayerControl target)
        {
            if (__instance == null || target == null) return;
            if (target.Data == null || !target.Data.IsDead)
            {
                Attempt(__instance, target);
                return;
            }
            string k = PName(__instance), v = PName(target);
            bool toast = NocturneConfig.NotifyKill.Value && !Mine(__instance.OwnerId);
            Fire(NocturneEventCat.Kill, $"Убийство: {k} → {v}", $"Kill: {k} → {v}", NocturneNotifyKind.Danger, toast);
            try
            {
                NocturneReplay.Rec(NocturneReplay.Rt.Kill, __instance.PlayerId, __instance.GetTruePosition(), target.GetTruePosition());
            }
            catch { }
        }
    }

    [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
    private static class MeetPatch
    {
        public static void Postfix()
        {
            bool toast = NocturneConfig.NotifyMeeting.Value;
            Fire(NocturneEventCat.Meeting, "Началось собрание", "Meeting started", NocturneNotifyKind.Info, toast);
            try
            {
                NocturneReplay.Rec(NocturneReplay.Rt.Meeting, 255, Ui.Zero2, Ui.Zero2);
            }
            catch { }
        }
    }

    [HarmonyPatch(typeof(ExileController), nameof(ExileController.Begin))]
    private static class EjectPatch
    {
        public static void Postfix(ExileController __instance)
        {
            if (__instance == null) return;
            try
            {
                NetworkedPlayerInfo ex = __instance.initData.networkedPlayer;
                string who = ex != null ? ex.PlayerName : NocturneText.T("никто", "no one");
                bool toast = NocturneConfig.NotifyEject.Value;
                Fire(NocturneEventCat.Meeting, $"Изгнан: {who}", $"Ejected: {who}", NocturneNotifyKind.Warning, toast);
            }
            catch { }
        }
    }

    [HarmonyPatch(typeof(Vent), nameof(Vent.EnterVent))]
    private static class VentEnterPatch
    {
        public static void Postfix([HarmonyArgument(0)] PlayerControl pc)
        {
            if (pc == null) return;
            string n = PName(pc);
            Fire(NocturneEventCat.Vent, $"В люк: {n}", $"Entered vent: {n}", NocturneNotifyKind.Info, false);
            try
            {
                NocturneReplay.Rec(NocturneReplay.Rt.Vent, pc.PlayerId, pc.GetTruePosition(), Ui.Zero2);
            }
            catch { }
        }
    }

    [HarmonyPatch(typeof(Vent), nameof(Vent.ExitVent))]
    private static class VentExitPatch
    {
        public static void Postfix([HarmonyArgument(0)] PlayerControl pc)
        {
            if (pc == null) return;
            string n = PName(pc);
            Fire(NocturneEventCat.Vent, $"Из люка: {n}", $"Exited vent: {n}", NocturneNotifyKind.Info, false);
        }
    }

    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.Shapeshift))]
    private static class ShiftPatch
    {
        public static void Postfix(PlayerControl __instance, [HarmonyArgument(0)] PlayerControl target)
        {
            if (__instance == null || target == null)
                return;
            string a = PName(__instance), b = PName(target);
            bool revert = __instance == target;
            Fire(NocturneEventCat.Role, revert ? $"Оборот снят: {a}" : $"Оборот: {a} → {b}", revert ? $"Unshifted: {a}" : $"Shapeshift: {a} → {b}", NocturneNotifyKind.Info, false);
            try
            {
                NocturneReplay.Rec(NocturneReplay.Rt.Shift, __instance.PlayerId, __instance.GetTruePosition(), Ui.Zero2);
            }
            catch { }
        }
    }

    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.ProtectPlayer))]
    private static class ProtectPatch
    {
        public static void Postfix(PlayerControl __instance, [HarmonyArgument(0)] PlayerControl target)
        {
            if (__instance == null || target == null)
                return;
            string a = PName(__instance), b = PName(target);
            Fire(NocturneEventCat.Role, $"Защита: {a} → {b}", $"Protect: {a} → {b}", NocturneNotifyKind.Success, false);
            try
            {
                NocturneReplay.Rec(NocturneReplay.Rt.Protect, __instance.PlayerId, __instance.GetTruePosition(), Ui.Zero2);
            }
            catch { }
        }
    }

    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CmdReportDeadBody))]
    private static class ReportPatch
    {
        public static void Postfix(PlayerControl __instance, [HarmonyArgument(0)] NetworkedPlayerInfo target)
        {
            if (__instance == null)
                return;
            string r = PName(__instance);
            string who = target != null ? target.PlayerName : NocturneText.T("экстренка", "emergency");
            Fire(NocturneEventCat.Report, $"Репорт: {r} ({who})", $"Report: {r} ({who})", NocturneNotifyKind.Warning, false);
            try
            {
                NocturneReplay.Rec(NocturneReplay.Rt.Report, __instance.PlayerId, __instance.GetTruePosition(), Ui.Zero2);
            }
            catch { }
        }
    }
}
