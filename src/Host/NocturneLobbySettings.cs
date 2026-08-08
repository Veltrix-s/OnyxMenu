using System.Collections.Generic;
using System.Globalization;
using System.Text;
using AmongUs.GameOptions;
using Il2CppInterop.Runtime.InteropTypes;
using InnerNet;
using UnityEngine;

namespace Nocturne;

public sealed class NocturneLobbySettings : MonoBehaviour
{
    private static bool _dirty;
    private static float _syncAt;

    internal static bool Ready()
    {
        try
        {
            if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost) return false;
            InnerNetClient c = (InnerNetClient)AmongUsClient.Instance;
            if (c.GameState != InnerNetClient.GameStates.Joined || c.IsGameStarted) return false;
            return GameOptionsManager.Instance != null && GameOptionsManager.Instance.CurrentGameOptions != null;
        }
        catch { return false; }
    }

    private static IGameOptions O => GameOptionsManager.Instance.CurrentGameOptions;
    private static IRoleOptionsCollection Roles => O.RoleOptions;

    internal static int Map() { try { return O.MapId; } catch { return 0; } }
    internal static int Players() { try { return O.MaxPlayers; } catch { return 10; } }
    internal static int Imps() { try { return O.NumImpostors; } catch { return 1; } }
    internal static float KillCd() { return GetF(FloatOptionNames.KillCooldown); }
    internal static float Speed() { return GetF(FloatOptionNames.PlayerSpeedMod); }
    internal static float CrewVis() { return GetF(FloatOptionNames.CrewLightMod); }
    internal static float ImpVis() { return GetF(FloatOptionNames.ImpostorLightMod); }
    internal static int Meetings() { return GetI(Int32OptionNames.NumEmergencyMeetings); }
    internal static int MeetingCd() { return GetI(Int32OptionNames.EmergencyCooldown); }
    internal static int Discuss() { return GetI(Int32OptionNames.DiscussionTime); }
    internal static int Voting() { return GetI(Int32OptionNames.VotingTime); }
    internal static int Common() { return GetI(Int32OptionNames.NumCommonTasks); }
    internal static int Long() { return GetI(Int32OptionNames.NumLongTasks); }
    internal static int Short() { return GetI(Int32OptionNames.NumShortTasks); }
    internal static bool Anon() { return GetB(BoolOptionNames.AnonymousVotes); }
    internal static bool Confirm() { return GetB(BoolOptionNames.ConfirmImpostor); }
    internal static bool Visual() { return GetB(BoolOptionNames.VisualTasks); }

    internal static void SetMap(int v) { try { O.SetByte(ByteOptionNames.MapId, (byte)Mathf.Clamp(v, 0, 5)); Touch(); } catch { } }
    internal static void SetPlayers(int v) { SetI(Int32OptionNames.MaxPlayers, v); }
    internal static void SetImps(int v) { SetI(Int32OptionNames.NumImpostors, v); }
    internal static void SetKillCd(float v) { SetF(FloatOptionNames.KillCooldown, v); }
    internal static void SetSpeed(float v) { SetF(FloatOptionNames.PlayerSpeedMod, v); }
    internal static void SetCrewVis(float v) { SetF(FloatOptionNames.CrewLightMod, v); }
    internal static void SetImpVis(float v) { SetF(FloatOptionNames.ImpostorLightMod, v); }
    internal static void SetMeetings(int v) { SetI(Int32OptionNames.NumEmergencyMeetings, v); }
    internal static void SetMeetingCd(int v) { SetI(Int32OptionNames.EmergencyCooldown, v); }
    internal static void SetDiscuss(int v) { SetI(Int32OptionNames.DiscussionTime, v); }
    internal static void SetVoting(int v) { SetI(Int32OptionNames.VotingTime, v); }
    internal static void SetCommon(int v) { SetI(Int32OptionNames.NumCommonTasks, v); }
    internal static void SetLong(int v) { SetI(Int32OptionNames.NumLongTasks, v); }
    internal static void SetShort(int v) { SetI(Int32OptionNames.NumShortTasks, v); }
    internal static void SetAnon(bool v) { SetB(BoolOptionNames.AnonymousVotes, v); }
    internal static void SetConfirm(bool v) { SetB(BoolOptionNames.ConfirmImpostor, v); }
    internal static void SetVisual(bool v) { SetB(BoolOptionNames.VisualTasks, v); }

    internal static int KillDist() { return GetI(Int32OptionNames.KillDistance); }
    internal static void SetKillDist(int v) { SetI(Int32OptionNames.KillDistance, Mathf.Clamp(v, 0, 2)); }
    internal static int TaskBar()
    {
        try
        {
            var n = ((Il2CppObjectBase)O).TryCast<NormalGameOptionsV10>();
            return n != null ? (int)n.TaskBarMode : 0;
        }
        catch { return 0; }
    }
    internal static void SetTaskBar(int v)
    {
        try
        {
            var n = ((Il2CppObjectBase)O).TryCast<NormalGameOptionsV10>();
            if (n != null) { n.TaskBarMode = (AmongUs.GameOptions.TaskBarMode)Mathf.Clamp(v, 0, 2); Touch(); }
        }
        catch { }
    }

    internal static int RoleNum(RoleTypes r) { try { return Roles.GetNumPerGame(r); } catch { return 0; } }
    internal static int RoleChance(RoleTypes r) { try { return Roles.GetChancePerGame(r); } catch { return 0; } }
    internal static void SetRole(RoleTypes r, int num, int chance) { try { Roles.SetRoleRate(r, num, chance); Touch(); } catch { } }

    private static T RoleOpt<T>(RoleTypes r) where T : Il2CppObjectBase
    {
        try
        {
            var col = ((Il2CppObjectBase)O.RoleOptions).TryCast<RoleOptionsCollectionV10>();
            if (col != null && col.TryGetRoleOptions<T>(r, out T o)) return o;
        }
        catch { }
        return null;
    }

    internal static float SciCd() { var o = RoleOpt<ScientistRoleOptionsV10>(RoleTypes.Scientist); return o != null ? o.ScientistCooldown : 0f; }
    internal static void SetSciCd(float v) { var o = RoleOpt<ScientistRoleOptionsV10>(RoleTypes.Scientist); if (o != null) { o.ScientistCooldown = v; Touch(); } }
    internal static float SciBat() { var o = RoleOpt<ScientistRoleOptionsV10>(RoleTypes.Scientist); return o != null ? o.ScientistBatteryCharge : 0f; }
    internal static void SetSciBat(float v) { var o = RoleOpt<ScientistRoleOptionsV10>(RoleTypes.Scientist); if (o != null) { o.ScientistBatteryCharge = v; Touch(); } }

    internal static float EngCd() { var o = RoleOpt<EngineerRoleOptionsV10>(RoleTypes.Engineer); return o != null ? o.EngineerCooldown : 0f; }
    internal static void SetEngCd(float v) { var o = RoleOpt<EngineerRoleOptionsV10>(RoleTypes.Engineer); if (o != null) { o.EngineerCooldown = v; Touch(); } }
    internal static float EngVent() { var o = RoleOpt<EngineerRoleOptionsV10>(RoleTypes.Engineer); return o != null ? o.EngineerInVentMaxTime : 0f; }
    internal static void SetEngVent(float v) { var o = RoleOpt<EngineerRoleOptionsV10>(RoleTypes.Engineer); if (o != null) { o.EngineerInVentMaxTime = v; Touch(); } }

    internal static float GaCd() { var o = RoleOpt<GuardianAngelRoleOptionsV10>(RoleTypes.GuardianAngel); return o != null ? o.GuardianAngelCooldown : 0f; }
    internal static void SetGaCd(float v) { var o = RoleOpt<GuardianAngelRoleOptionsV10>(RoleTypes.GuardianAngel); if (o != null) { o.GuardianAngelCooldown = v; Touch(); } }
    internal static float GaDur() { var o = RoleOpt<GuardianAngelRoleOptionsV10>(RoleTypes.GuardianAngel); return o != null ? o.ProtectionDurationSeconds : 0f; }
    internal static void SetGaDur(float v) { var o = RoleOpt<GuardianAngelRoleOptionsV10>(RoleTypes.GuardianAngel); if (o != null) { o.ProtectionDurationSeconds = v; Touch(); } }
    internal static bool GaImpSee() { var o = RoleOpt<GuardianAngelRoleOptionsV10>(RoleTypes.GuardianAngel); return o != null && o.ImpostorsCanSeeProtect; }
    internal static void SetGaImpSee(bool v) { var o = RoleOpt<GuardianAngelRoleOptionsV10>(RoleTypes.GuardianAngel); if (o != null) { o.ImpostorsCanSeeProtect = v; Touch(); } }

    internal static float TrCd() { var o = RoleOpt<TrackerRoleOptionsV10>(RoleTypes.Tracker); return o != null ? o.TrackerCooldown : 0f; }
    internal static void SetTrCd(float v) { var o = RoleOpt<TrackerRoleOptionsV10>(RoleTypes.Tracker); if (o != null) { o.TrackerCooldown = v; Touch(); } }
    internal static float TrDur() { var o = RoleOpt<TrackerRoleOptionsV10>(RoleTypes.Tracker); return o != null ? o.TrackerDuration : 0f; }
    internal static void SetTrDur(float v) { var o = RoleOpt<TrackerRoleOptionsV10>(RoleTypes.Tracker); if (o != null) { o.TrackerDuration = v; Touch(); } }
    internal static float TrDelay() { var o = RoleOpt<TrackerRoleOptionsV10>(RoleTypes.Tracker); return o != null ? o.TrackerDelay : 0f; }
    internal static void SetTrDelay(float v) { var o = RoleOpt<TrackerRoleOptionsV10>(RoleTypes.Tracker); if (o != null) { o.TrackerDelay = v; Touch(); } }

    internal static float NmDur() { var o = RoleOpt<NoisemakerRoleOptionsV10>(RoleTypes.Noisemaker); return o != null ? o.NoisemakerAlertDuration : 0f; }
    internal static void SetNmDur(float v) { var o = RoleOpt<NoisemakerRoleOptionsV10>(RoleTypes.Noisemaker); if (o != null) { o.NoisemakerAlertDuration = v; Touch(); } }
    internal static bool NmImpAlert() { var o = RoleOpt<NoisemakerRoleOptionsV10>(RoleTypes.Noisemaker); return o != null && o.NoisemakerImpostorAlert; }
    internal static void SetNmImpAlert(bool v) { var o = RoleOpt<NoisemakerRoleOptionsV10>(RoleTypes.Noisemaker); if (o != null) { o.NoisemakerImpostorAlert = v; Touch(); } }

    internal static float DetLimit() { var o = RoleOpt<DetectiveRoleOptionsV10>(RoleTypes.Detective); return o != null ? o.DetectiveSuspectLimit : 0f; }
    internal static void SetDetLimit(float v) { var o = RoleOpt<DetectiveRoleOptionsV10>(RoleTypes.Detective); if (o != null) { o.DetectiveSuspectLimit = v; Touch(); } }

    internal static float SsCd() { var o = RoleOpt<ShapeshifterRoleOptionsV10>(RoleTypes.Shapeshifter); return o != null ? o.ShapeshifterCooldown : 0f; }
    internal static void SetSsCd(float v) { var o = RoleOpt<ShapeshifterRoleOptionsV10>(RoleTypes.Shapeshifter); if (o != null) { o.ShapeshifterCooldown = v; Touch(); } }
    internal static float SsDur() { var o = RoleOpt<ShapeshifterRoleOptionsV10>(RoleTypes.Shapeshifter); return o != null ? o.ShapeshifterDuration : 0f; }
    internal static void SetSsDur(float v) { var o = RoleOpt<ShapeshifterRoleOptionsV10>(RoleTypes.Shapeshifter); if (o != null) { o.ShapeshifterDuration = v; Touch(); } }
    internal static bool SsSkin() { var o = RoleOpt<ShapeshifterRoleOptionsV10>(RoleTypes.Shapeshifter); return o != null && o.ShapeshifterLeaveSkin; }
    internal static void SetSsSkin(bool v) { var o = RoleOpt<ShapeshifterRoleOptionsV10>(RoleTypes.Shapeshifter); if (o != null) { o.ShapeshifterLeaveSkin = v; Touch(); } }

    internal static float PhCd() { var o = RoleOpt<PhantomRoleOptionsV10>(RoleTypes.Phantom); return o != null ? o.PhantomCooldown : 0f; }
    internal static void SetPhCd(float v) { var o = RoleOpt<PhantomRoleOptionsV10>(RoleTypes.Phantom); if (o != null) { o.PhantomCooldown = v; Touch(); } }
    internal static float PhDur() { var o = RoleOpt<PhantomRoleOptionsV10>(RoleTypes.Phantom); return o != null ? o.PhantomDuration : 0f; }
    internal static void SetPhDur(float v) { var o = RoleOpt<PhantomRoleOptionsV10>(RoleTypes.Phantom); if (o != null) { o.PhantomDuration = v; Touch(); } }

    internal static float VpDis() { var o = RoleOpt<ViperRoleOptionsV10>(RoleTypes.Viper); return o != null ? o.viperDissolveTime : 0f; }
    internal static void SetVpDis(float v) { var o = RoleOpt<ViperRoleOptionsV10>(RoleTypes.Viper); if (o != null) { o.viperDissolveTime = v; Touch(); } }

    private static int GetI(Int32OptionNames k) { try { return O.GetInt(k); } catch { return 0; } }
    private static float GetF(FloatOptionNames k) { try { return O.GetFloat(k); } catch { return 0f; } }
    private static bool GetB(BoolOptionNames k) { try { return O.GetBool(k); } catch { return false; } }
    private static void SetI(Int32OptionNames k, int v) { try { O.SetInt(k, v); Touch(); } catch { } }
    private static void SetF(FloatOptionNames k, float v) { try { O.SetFloat(k, v); Touch(); } catch { } }
    private static void SetB(BoolOptionNames k, bool v) { try { O.SetBool(k, v); Touch(); } catch { } }

    private static void Touch()
    {
        try
        {
            GameOptionsManager mgr = GameOptionsManager.Instance;
            if (mgr != null && mgr.CurrentGameOptions != null) mgr.GameHostOptions = mgr.CurrentGameOptions;
        }
        catch { }
        _dirty = true;
        _syncAt = Time.unscaledTime + 0.35f;
    }

    public void Update()
    {
        if (!_dirty || Time.unscaledTime < _syncAt) return;
        _dirty = false;
        if (!Ready()) return;
        try
        {
            if (GameManager.Instance != null && GameManager.Instance.LogicOptions != null)
            {
                Patches.NocturneHostOptions.SyncPass = true;
                GameManager.Instance.LogicOptions.SyncOptions();
            }
        }
        catch { }
        finally { Patches.NocturneHostOptions.SyncPass = false; }
    }

    private static readonly RoleTypes[] RateRoles =
    {
        RoleTypes.Scientist, RoleTypes.Engineer, RoleTypes.GuardianAngel, RoleTypes.Tracker,
        RoleTypes.Noisemaker, RoleTypes.Detective, RoleTypes.Shapeshifter, RoleTypes.Phantom, RoleTypes.Viper
    };

    internal static string Capture()
    {
        var sb = new StringBuilder("v1");
        void N(float v) { sb.Append(';').Append(v.ToString("0.###", CultureInfo.InvariantCulture)); }

        N(Map()); N(Players()); N(Imps()); N(KillCd()); N(Speed()); N(CrewVis()); N(ImpVis()); N(KillDist()); N(TaskBar());
        N(Meetings()); N(MeetingCd()); N(Discuss()); N(Voting()); N(Anon() ? 1 : 0); N(Confirm() ? 1 : 0);
        N(Common()); N(Long()); N(Short()); N(Visual() ? 1 : 0);
        foreach (RoleTypes r in RateRoles) { N(RoleNum(r)); N(RoleChance(r)); }
        N(SciCd()); N(SciBat());
        N(EngCd()); N(EngVent());
        N(GaCd()); N(GaDur()); N(GaImpSee() ? 1 : 0);
        N(TrCd()); N(TrDur()); N(TrDelay());
        N(NmDur()); N(NmImpAlert() ? 1 : 0);
        N(DetLimit());
        N(SsCd()); N(SsDur()); N(SsSkin() ? 1 : 0);
        N(PhCd()); N(PhDur());
        N(VpDis());
        return sb.ToString();
    }

    internal static bool ApplyState(string s)
    {
        if (string.IsNullOrEmpty(s)) return false;
        string[] p = s.Split(';');
        if (p.Length < 2 || p[0] != "v1") return false;
        int i = 1;
        float Next()
        {
            if (i >= p.Length) return 0f;
            float.TryParse(p[i++], NumberStyles.Float, CultureInfo.InvariantCulture, out float v);
            return v;
        }

        SetMap((int)Next()); SetPlayers((int)Next()); SetImps((int)Next()); SetKillCd(Next());
        SetSpeed(Next()); SetCrewVis(Next()); SetImpVis(Next()); SetKillDist((int)Next()); SetTaskBar((int)Next());
        SetMeetings((int)Next()); SetMeetingCd((int)Next()); SetDiscuss((int)Next()); SetVoting((int)Next());
        SetAnon(Next() > 0.5f); SetConfirm(Next() > 0.5f);
        SetCommon((int)Next()); SetLong((int)Next()); SetShort((int)Next()); SetVisual(Next() > 0.5f);
        foreach (RoleTypes r in RateRoles) SetRole(r, (int)Next(), (int)Next());
        SetSciCd(Next()); SetSciBat(Next());
        SetEngCd(Next()); SetEngVent(Next());
        SetGaCd(Next()); SetGaDur(Next()); SetGaImpSee(Next() > 0.5f);
        SetTrCd(Next()); SetTrDur(Next()); SetTrDelay(Next());
        SetNmDur(Next()); SetNmImpAlert(Next() > 0.5f);
        SetDetLimit(Next());
        SetSsCd(Next()); SetSsDur(Next()); SetSsSkin(Next() > 0.5f);
        SetPhCd(Next()); SetPhDur(Next());
        SetVpDis(Next());
        return true;
    }
}
