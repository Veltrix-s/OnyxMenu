using System;
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

    private static bool _ready;
    private static int _readyFrame = -1;

    internal static bool Ready()
    {
        int f = Time.frameCount;
        if (_readyFrame == f)
            return _ready;
        _readyFrame = f;

        if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost)
            return _ready = false;
        InnerNetClient c = AmongUsClient.Instance;
        if (c.GameState != InnerNetClient.GameStates.Joined || c.IsGameStarted)
            return _ready = false;
        return _ready = GameOptionsManager.Instance != null && GameOptionsManager.Instance.CurrentGameOptions != null;
    }

    private static IGameOptions _opt;
    private static IRoleOptionsCollection _roles;
    private static int _optFrame = -1;

    private static IGameOptions O
    {
        get
        {
            int f = Time.frameCount;
            if (_optFrame == f && _opt != null)
                return _opt;

            _optFrame = f;
            _roles = null;
            _opt = GameOptionsManager.Instance.CurrentGameOptions;
            return _opt;
        }
    }

    private static IRoleOptionsCollection Roles => _roles ??= O.RoleOptions;

    internal static int Map()
    {
        try
        {
            return O.MapId;
        }
        catch
        {
            return 0;
        }
    }
    internal static int Players()
    {
        try
        {
            return O.MaxPlayers;
        }
        catch
        {
            return 10;
        }
    }
    internal static int Imps()
    {
        try
        {
            return O.NumImpostors;
        }
        catch
        {
            return 1;
        }
    }
    internal static float KillCd()
    {
        return GetF(FloatOptionNames.KillCooldown);
    }
    internal static float Speed()
    {
        return GetF(FloatOptionNames.PlayerSpeedMod);
    }
    internal static float CrewVis()
    {
        return GetF(FloatOptionNames.CrewLightMod);
    }
    internal static float ImpVis()
    {
        return GetF(FloatOptionNames.ImpostorLightMod);
    }
    internal static int Meetings()
    {
        return GetI(Int32OptionNames.NumEmergencyMeetings);
    }
    internal static int MeetingCd()
    {
        return GetI(Int32OptionNames.EmergencyCooldown);
    }
    internal static int Discuss()
    {
        return GetI(Int32OptionNames.DiscussionTime);
    }
    internal static int Voting()
    {
        return GetI(Int32OptionNames.VotingTime);
    }
    internal static int Common()
    {
        return GetI(Int32OptionNames.NumCommonTasks);
    }
    internal static int Long()
    {
        return GetI(Int32OptionNames.NumLongTasks);
    }
    internal static int Short()
    {
        return GetI(Int32OptionNames.NumShortTasks);
    }
    internal static bool Anon()
    {
        return GetB(BoolOptionNames.AnonymousVotes);
    }
    internal static bool Confirm()
    {
        return GetB(BoolOptionNames.ConfirmImpostor);
    }
    internal static bool Visual()
    {
        return GetB(BoolOptionNames.VisualTasks);
    }

    internal static void SetMap(int v)
    {
        try
        {
            O.SetByte(ByteOptionNames.MapId, (byte)Ui.Clamp(v, 0, 5));
            Touch();
        }
        catch { }
    }
    internal static void SetPlayers(int v)
    {
        SetI(Int32OptionNames.MaxPlayers, v);
    }
    internal static void SetImps(int v)
    {
        SetI(Int32OptionNames.NumImpostors, v);
    }
    internal static void SetKillCd(float v)
    {
        SetF(FloatOptionNames.KillCooldown, v);
    }
    internal static void SetSpeed(float v)
    {
        SetF(FloatOptionNames.PlayerSpeedMod, v);
    }
    internal static void SetCrewVis(float v)
    {
        SetF(FloatOptionNames.CrewLightMod, v);
    }
    internal static void SetImpVis(float v)
    {
        SetF(FloatOptionNames.ImpostorLightMod, v);
    }
    internal static void SetMeetings(int v)
    {
        SetI(Int32OptionNames.NumEmergencyMeetings, v);
    }
    internal static void SetMeetingCd(int v)
    {
        SetI(Int32OptionNames.EmergencyCooldown, v);
    }
    internal static void SetDiscuss(int v)
    {
        SetI(Int32OptionNames.DiscussionTime, v);
    }
    internal static void SetVoting(int v)
    {
        SetI(Int32OptionNames.VotingTime, v);
    }
    internal static void SetCommon(int v)
    {
        SetI(Int32OptionNames.NumCommonTasks, v);
    }
    internal static void SetLong(int v)
    {
        SetI(Int32OptionNames.NumLongTasks, v);
    }
    internal static void SetShort(int v)
    {
        SetI(Int32OptionNames.NumShortTasks, v);
    }
    internal static void SetAnon(bool v)
    {
        SetB(BoolOptionNames.AnonymousVotes, v);
    }
    internal static void SetConfirm(bool v)
    {
        SetB(BoolOptionNames.ConfirmImpostor, v);
    }
    internal static void SetVisual(bool v)
    {
        SetB(BoolOptionNames.VisualTasks, v);
    }

    internal static int KillDist()
    {
        return GetI(Int32OptionNames.KillDistance);
    }
    internal static void SetKillDist(int v)
    {
        SetI(Int32OptionNames.KillDistance, Ui.Clamp(v, 0, 2));
    }
    internal static int TaskBar()
    {
        try
        {
            var n = O.TryCast<NormalGameOptionsV11>();
            if (n != null)
                return (int)n.TaskBarMode;

            var n12 = O.TryCast<NormalGameOptionsV12>();
            return n12 != null ? (int)n12.TaskBarMode : 0;
        }
        catch
        {
            return 0;
        }
    }
    internal static void SetTaskBar(int v)
    {
        var mode = (AmongUs.GameOptions.TaskBarMode)Ui.Clamp(v, 0, 2);
        try
        {
            var n = O.TryCast<NormalGameOptionsV11>();
            if (n != null)
            {
                n.TaskBarMode = mode;
                Touch();
                return;
            }

            var n12 = O.TryCast<NormalGameOptionsV12>();
            if (n12 != null)
            {
                n12.TaskBarMode = mode;
                Touch();
            }
        }
        catch { }
    }

    internal static int RoleNum(RoleTypes r)
    {
        try
        {
            return Roles.GetNumPerGame(r);
        }
        catch
        {
            return 0;
        }
    }
    internal static int RoleChance(RoleTypes r)
    {
        try
        {
            return Roles.GetChancePerGame(r);
        }
        catch
        {
            return 0;
        }
    }
    internal static void SetRole(RoleTypes r, int num, int chance)
    {
        try
        {
            Roles.SetRoleRate(r, num, chance);
            Touch();
        }
        catch { }
    }

    private static T RoleOpt<T>(RoleTypes r) where T : Il2CppObjectBase
    {
        try
        {
            var col = O.RoleOptions.TryCast<RoleOptionsCollectionV11>();
            if (col != null && col.TryGetRoleOptions<T>(r, out T o))
                return o;
        }
        catch { }
        return null;
    }

    private static T RoleOpt12<T>(RoleTypes r) where T : Il2CppObjectBase
    {
        try
        {
            var col = O.RoleOptions.TryCast<RoleOptionsCollectionV12>();
            if (col != null && col.TryGetRoleOptions<T>(r, out T o))
                return o;
        }
        catch { }
        return null;
    }

    private static float RoleF<T11, T12>(RoleTypes r, Func<T11, float> v11, Func<T12, float> v12)
        where T11 : Il2CppObjectBase
        where T12 : Il2CppObjectBase
    {
        T11 a = RoleOpt<T11>(r);
        if (a != null) return v11(a);

        T12 b = RoleOpt12<T12>(r);
        return b != null ? v12(b) : 0f;
    }

    private static bool RoleB<T11, T12>(RoleTypes r, Func<T11, bool> v11, Func<T12, bool> v12)
        where T11 : Il2CppObjectBase
        where T12 : Il2CppObjectBase
    {
        T11 a = RoleOpt<T11>(r);
        if (a != null) return v11(a);

        T12 b = RoleOpt12<T12>(r);
        return b != null && v12(b);
    }

    private static void RoleSet<T11, T12>(RoleTypes r, Action<T11> v11, Action<T12> v12)
        where T11 : Il2CppObjectBase
        where T12 : Il2CppObjectBase
    {
        T11 a = RoleOpt<T11>(r);
        if (a != null)
        {
            v11(a);
            Touch();
            return;
        }

        T12 b = RoleOpt12<T12>(r);
        if (b == null) return;

        v12(b);
        Touch();
    }

    internal static float SciCd() =>
        RoleF<ScientistRoleOptionsV11, ScientistRoleOptionsV12>(RoleTypes.Scientist, o => o.ScientistCooldown, o => o.ScientistCooldown);
    internal static void SetSciCd(float v) =>
        RoleSet<ScientistRoleOptionsV11, ScientistRoleOptionsV12>(RoleTypes.Scientist, o => o.ScientistCooldown = v, o => o.ScientistCooldown = v);
    internal static float SciBat() =>
        RoleF<ScientistRoleOptionsV11, ScientistRoleOptionsV12>(RoleTypes.Scientist, o => o.ScientistBatteryCharge, o => o.ScientistBatteryCharge);
    internal static void SetSciBat(float v) =>
        RoleSet<ScientistRoleOptionsV11, ScientistRoleOptionsV12>(RoleTypes.Scientist, o => o.ScientistBatteryCharge = v, o => o.ScientistBatteryCharge = v);

    internal static float EngCd() =>
        RoleF<EngineerRoleOptionsV11, EngineerRoleOptionsV12>(RoleTypes.Engineer, o => o.EngineerCooldown, o => o.EngineerCooldown);
    internal static void SetEngCd(float v) =>
        RoleSet<EngineerRoleOptionsV11, EngineerRoleOptionsV12>(RoleTypes.Engineer, o => o.EngineerCooldown = v, o => o.EngineerCooldown = v);
    internal static float EngVent() =>
        RoleF<EngineerRoleOptionsV11, EngineerRoleOptionsV12>(RoleTypes.Engineer, o => o.EngineerInVentMaxTime, o => o.EngineerInVentMaxTime);
    internal static void SetEngVent(float v) =>
        RoleSet<EngineerRoleOptionsV11, EngineerRoleOptionsV12>(RoleTypes.Engineer, o => o.EngineerInVentMaxTime = v, o => o.EngineerInVentMaxTime = v);

    internal static float GaCd() =>
        RoleF<GuardianAngelRoleOptionsV11, GuardianAngelRoleOptionsV12>(RoleTypes.GuardianAngel, o => o.GuardianAngelCooldown, o => o.GuardianAngelCooldown);
    internal static void SetGaCd(float v) =>
        RoleSet<GuardianAngelRoleOptionsV11, GuardianAngelRoleOptionsV12>(RoleTypes.GuardianAngel, o => o.GuardianAngelCooldown = v, o => o.GuardianAngelCooldown = v);
    internal static float GaDur() =>
        RoleF<GuardianAngelRoleOptionsV11, GuardianAngelRoleOptionsV12>(RoleTypes.GuardianAngel, o => o.ProtectionDurationSeconds, o => o.ProtectionDurationSeconds);
    internal static void SetGaDur(float v) =>
        RoleSet<GuardianAngelRoleOptionsV11, GuardianAngelRoleOptionsV12>(RoleTypes.GuardianAngel, o => o.ProtectionDurationSeconds = v, o => o.ProtectionDurationSeconds = v);
    internal static bool GaImpSee() =>
        RoleB<GuardianAngelRoleOptionsV11, GuardianAngelRoleOptionsV12>(RoleTypes.GuardianAngel, o => o.ImpostorsCanSeeProtect, o => o.ImpostorsCanSeeProtect);
    internal static void SetGaImpSee(bool v) =>
        RoleSet<GuardianAngelRoleOptionsV11, GuardianAngelRoleOptionsV12>(RoleTypes.GuardianAngel, o => o.ImpostorsCanSeeProtect = v, o => o.ImpostorsCanSeeProtect = v);

    internal static float TrCd() =>
        RoleF<TrackerRoleOptionsV11, TrackerRoleOptionsV12>(RoleTypes.Tracker, o => o.TrackerCooldown, o => o.TrackerCooldown);
    internal static void SetTrCd(float v) =>
        RoleSet<TrackerRoleOptionsV11, TrackerRoleOptionsV12>(RoleTypes.Tracker, o => o.TrackerCooldown = v, o => o.TrackerCooldown = v);
    internal static float TrDur() =>
        RoleF<TrackerRoleOptionsV11, TrackerRoleOptionsV12>(RoleTypes.Tracker, o => o.TrackerDuration, o => o.TrackerDuration);
    internal static void SetTrDur(float v) =>
        RoleSet<TrackerRoleOptionsV11, TrackerRoleOptionsV12>(RoleTypes.Tracker, o => o.TrackerDuration = v, o => o.TrackerDuration = v);
    internal static float TrDelay() =>
        RoleF<TrackerRoleOptionsV11, TrackerRoleOptionsV12>(RoleTypes.Tracker, o => o.TrackerDelay, o => o.TrackerDelay);
    internal static void SetTrDelay(float v) =>
        RoleSet<TrackerRoleOptionsV11, TrackerRoleOptionsV12>(RoleTypes.Tracker, o => o.TrackerDelay = v, o => o.TrackerDelay = v);

    internal static float NmDur() =>
        RoleF<NoisemakerRoleOptionsV11, NoisemakerRoleOptionsV12>(RoleTypes.Noisemaker, o => o.NoisemakerAlertDuration, o => o.NoisemakerAlertDuration);
    internal static void SetNmDur(float v) =>
        RoleSet<NoisemakerRoleOptionsV11, NoisemakerRoleOptionsV12>(RoleTypes.Noisemaker, o => o.NoisemakerAlertDuration = v, o => o.NoisemakerAlertDuration = v);
    internal static bool NmImpAlert() =>
        RoleB<NoisemakerRoleOptionsV11, NoisemakerRoleOptionsV12>(RoleTypes.Noisemaker, o => o.NoisemakerImpostorAlert, o => o.NoisemakerImpostorAlert);
    internal static void SetNmImpAlert(bool v) =>
        RoleSet<NoisemakerRoleOptionsV11, NoisemakerRoleOptionsV12>(RoleTypes.Noisemaker, o => o.NoisemakerImpostorAlert = v, o => o.NoisemakerImpostorAlert = v);

    internal static float DetLimit() =>
        RoleF<DetectiveRoleOptionsV11, DetectiveRoleOptionsV12>(RoleTypes.Detective, o => o.DetectiveSuspectLimit, o => o.DetectiveSuspectLimit);
    internal static void SetDetLimit(float v) =>
        RoleSet<DetectiveRoleOptionsV11, DetectiveRoleOptionsV12>(RoleTypes.Detective, o => o.DetectiveSuspectLimit = v, o => o.DetectiveSuspectLimit = v);

    internal static float SsCd() =>
        RoleF<ShapeshifterRoleOptionsV11, ShapeshifterRoleOptionsV12>(RoleTypes.Shapeshifter, o => o.ShapeshifterCooldown, o => o.ShapeshifterCooldown);
    internal static void SetSsCd(float v) =>
        RoleSet<ShapeshifterRoleOptionsV11, ShapeshifterRoleOptionsV12>(RoleTypes.Shapeshifter, o => o.ShapeshifterCooldown = v, o => o.ShapeshifterCooldown = v);
    internal static float SsDur() =>
        RoleF<ShapeshifterRoleOptionsV11, ShapeshifterRoleOptionsV12>(RoleTypes.Shapeshifter, o => o.ShapeshifterDuration, o => o.ShapeshifterDuration);
    internal static void SetSsDur(float v) =>
        RoleSet<ShapeshifterRoleOptionsV11, ShapeshifterRoleOptionsV12>(RoleTypes.Shapeshifter, o => o.ShapeshifterDuration = v, o => o.ShapeshifterDuration = v);
    internal static bool SsSkin() =>
        RoleB<ShapeshifterRoleOptionsV11, ShapeshifterRoleOptionsV12>(RoleTypes.Shapeshifter, o => o.ShapeshifterLeaveSkin, o => o.ShapeshifterLeaveSkin);
    internal static void SetSsSkin(bool v) =>
        RoleSet<ShapeshifterRoleOptionsV11, ShapeshifterRoleOptionsV12>(RoleTypes.Shapeshifter, o => o.ShapeshifterLeaveSkin = v, o => o.ShapeshifterLeaveSkin = v);

    internal static float PhCd() =>
        RoleF<PhantomRoleOptionsV11, PhantomRoleOptionsV12>(RoleTypes.Phantom, o => o.PhantomCooldown, o => o.PhantomCooldown);
    internal static void SetPhCd(float v) =>
        RoleSet<PhantomRoleOptionsV11, PhantomRoleOptionsV12>(RoleTypes.Phantom, o => o.PhantomCooldown = v, o => o.PhantomCooldown = v);
    internal static float PhDur() =>
        RoleF<PhantomRoleOptionsV11, PhantomRoleOptionsV12>(RoleTypes.Phantom, o => o.PhantomDuration, o => o.PhantomDuration);
    internal static void SetPhDur(float v) =>
        RoleSet<PhantomRoleOptionsV11, PhantomRoleOptionsV12>(RoleTypes.Phantom, o => o.PhantomDuration = v, o => o.PhantomDuration = v);

    internal static float VpDis() =>
        RoleF<ViperRoleOptionsV11, ViperRoleOptionsV12>(RoleTypes.Viper, o => o.ViperDissolveTime, o => o.ViperDissolveTime);
    internal static void SetVpDis(float v) =>
        RoleSet<ViperRoleOptionsV11, ViperRoleOptionsV12>(RoleTypes.Viper, o => o.ViperDissolveTime = v, o => o.ViperDissolveTime = v);

    internal static float SgCd() =>
        RoleF<SpiritGuideRoleOptionsV11, SpiritGuideRoleOptionsV12>(RoleTypes.SpiritGuide, o => o.SpiritGuideCooldownSeconds, o => o.SpiritGuideCooldownSeconds);
    internal static void SetSgCd(float v) =>
        RoleSet<SpiritGuideRoleOptionsV11, SpiritGuideRoleOptionsV12>(RoleTypes.SpiritGuide, o => o.SpiritGuideCooldownSeconds = v, o => o.SpiritGuideCooldownSeconds = v);

    internal static float JudgeTaskPct() =>
        RoleF<JudgeRoleOptionsV11, JudgeRoleOptionsV12>(RoleTypes.Judge, o => o.JudgeTaskRequirementPercentage, o => o.JudgeTaskRequirementPercentage);
    internal static void SetJudgeTaskPct(float v) =>
        RoleSet<JudgeRoleOptionsV11, JudgeRoleOptionsV12>(RoleTypes.Judge, o => o.JudgeTaskRequirementPercentage = Ui.RoundToInt(v), o => o.JudgeTaskRequirementPercentage = Ui.RoundToInt(v));

    private static int GetI(Int32OptionNames k)
    {
        try
        {
            return O.GetInt(k);
        }
        catch
        {
            return 0;
        }
    }
    private static float GetF(FloatOptionNames k)
    {
        try
        {
            return O.GetFloat(k);
        }
        catch
        {
            return 0f;
        }
    }
    private static bool GetB(BoolOptionNames k)
    {
        try
        {
            return O.GetBool(k);
        }
        catch
        {
            return false;
        }
    }
    private static void SetI(Int32OptionNames k, int v)
    {
        try
        {
            O.SetInt(k, v);
            Touch();
        }
        catch { }
    }
    private static void SetF(FloatOptionNames k, float v)
    {
        try
        {
            O.SetFloat(k, v);
            Touch();
        }
        catch { }
    }
    private static void SetB(BoolOptionNames k, bool v)
    {
        try
        {
            O.SetBool(k, v);
            Touch();
        }
        catch { }
    }

    private static void Touch()
    {
        GameOptionsManager mgr = GameOptionsManager.Instance;
        if (mgr != null && mgr.CurrentGameOptions != null)
            mgr.GameHostOptions = mgr.CurrentGameOptions;
        _dirty = true;
        _syncAt = Time.unscaledTime + 0.35f;
    }

    public void FixedUpdate()
    {
        if (!_dirty || Time.unscaledTime < _syncAt)
            return;
        _dirty = false;
        if (!Ready())
            return;
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
        void N(float v)
        {
            sb.Append(';').Append(v.ToString("0.###", CultureInfo.InvariantCulture));
        }

        N(Map());
        N(Players());
        N(Imps());
        N(KillCd());
        N(Speed());
        N(CrewVis());
        N(ImpVis());
        N(KillDist());
        N(TaskBar());
        N(Meetings());
        N(MeetingCd());
        N(Discuss());
        N(Voting());
        N(Anon() ? 1 : 0);
        N(Confirm() ? 1 : 0);
        N(Common());
        N(Long());
        N(Short());
        N(Visual() ? 1 : 0);
        foreach (RoleTypes r in RateRoles)
        {
            N(RoleNum(r));
            N(RoleChance(r));
        }
        N(SciCd());
        N(SciBat());
        N(EngCd());
        N(EngVent());
        N(GaCd());
        N(GaDur());
        N(GaImpSee() ? 1 : 0);
        N(TrCd());
        N(TrDur());
        N(TrDelay());
        N(NmDur());
        N(NmImpAlert() ? 1 : 0);
        N(DetLimit());
        N(SsCd());
        N(SsDur());
        N(SsSkin() ? 1 : 0);
        N(PhCd());
        N(PhDur());
        N(VpDis());
        N(RoleNum(RoleTypes.SpiritGuide));
        N(RoleChance(RoleTypes.SpiritGuide));
        N(SgCd());
        return sb.ToString();
    }

    internal static bool ApplyState(string s)
    {
        if (string.IsNullOrEmpty(s))
            return false;
        string[] p = s.Split(';');
        if (p.Length < 2 || p[0] != "v1")
            return false;
        int i = 1;
        float Next()
        {
            if (i >= p.Length)
                return 0f;
            float.TryParse(p[i++], NumberStyles.Float, CultureInfo.InvariantCulture, out float v);
            return v;
        }

        SetMap((int)Next());
        SetPlayers((int)Next());
        SetImps((int)Next());
        SetKillCd(Next());
        SetSpeed(Next());
        SetCrewVis(Next());
        SetImpVis(Next());
        SetKillDist((int)Next());
        SetTaskBar((int)Next());
        SetMeetings((int)Next());
        SetMeetingCd((int)Next());
        SetDiscuss((int)Next());
        SetVoting((int)Next());
        SetAnon(Next() > 0.5f);
        SetConfirm(Next() > 0.5f);
        SetCommon((int)Next());
        SetLong((int)Next());
        SetShort((int)Next());
        SetVisual(Next() > 0.5f);
        foreach (RoleTypes r in RateRoles)
            SetRole(r, (int)Next(), (int)Next());
        SetSciCd(Next());
        SetSciBat(Next());
        SetEngCd(Next());
        SetEngVent(Next());
        SetGaCd(Next());
        SetGaDur(Next());
        SetGaImpSee(Next() > 0.5f);
        SetTrCd(Next());
        SetTrDur(Next());
        SetTrDelay(Next());
        SetNmDur(Next());
        SetNmImpAlert(Next() > 0.5f);
        SetDetLimit(Next());
        SetSsCd(Next());
        SetSsDur(Next());
        SetSsSkin(Next() > 0.5f);
        SetPhCd(Next());
        SetPhDur(Next());
        SetVpDis(Next());
        if (i < p.Length)
        {
            SetRole(RoleTypes.SpiritGuide, (int)Next(), (int)Next());
            SetSgCd(Next());
        }
        return true;
    }
}
