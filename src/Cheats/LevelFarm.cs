using System;
using AmongUs.Data;
using AmongUs.GameOptions;
using UnityEngine;

namespace Nocturne;

internal static class LevelFarm
{
    private const float Rate = 40f;
    private const float ShowGap = 0.08f;
    private const float MaxStep = 0.5f;
    private const int MaxPerFrame = 40;
    private const GameOverReason ByKill = (GameOverReason)3;
    private const GameOverReason HnsByKill = (GameOverReason)8;
    private const MurderResultFlags Block = MurderResultFlags.FailedProtected | MurderResultFlags.DecisionByHost;

    private static float lastAt;
    private static float acc;
    private static float showAt;
    private static float endAt;
    private static float startAt = -1f;
    private static float capAt;
    private static float speedAt;
    private static float lineAt = -9f;
    private static string line = string.Empty;
    private static int real;
    private static int speedTotal;

    internal static bool Busy;
    internal static int Kills;
    internal static int Total;
    internal static int Matches;
    internal static int LevelsGained;
    internal static int LastXp = -1;
    internal static int Speed;
    internal static int Faults;

    internal static int Goal => Ui.Clamp(NocturneConfig.LevelFarmKills.Value, 500, 10000);

    internal static int StopAt => NocturneConfig.LevelFarmStop.Value ? Ui.Clamp(NocturneConfig.LevelFarmStopAt.Value, 2, 100000) : 0;

    internal static int Level()
    {
        if (NocturneConfig.SpoofLevelEnabled.Value)
            return real;

        uint lv = DataManager.Player.Stats.Level;
        return lv < 10000u ? (int)lv + 1 : 0;
    }

    private static void Cap()
    {
        int stop = StopAt;
        if (stop == 0 || !NocturneConfig.LevelFarm.Value)
            return;

        int lv = Level();
        if (lv < stop)
            return;

        NocturneConfig.LevelFarm.Value = false;
        NocturneToast.Push(NocturneText.T("Фарм лвл", "Level farm"), NocturneText.T("Остановлен: уровень ", "Stopped at level ") + lv, 5f, NocturneNotifyKind.Success);
    }

    private static bool Imp(PlayerControl me)
    {
        NetworkedPlayerInfo data = me.Data;
        return data != null && !data.IsDead && data.Role != null && data.Role.IsImpostor;
    }

    internal static int State()
    {
        if (!NocturneConfig.LevelFarm.Value)
            return 0;

        PlayerControl me = PlayerControl.LocalPlayer;
        bool farming = Utils.Host && me != null && LobbyBehaviour.Instance == null && ShipStatus.Instance != null && Imp(me);
        return farming ? 2 : 1;
    }

    internal static void NewMatch()
    {
        if (Kills > 0)
            Matches++;

        Kills = 0;
        acc = 0f;
        lastAt = 0f;
        endAt = 0f;
    }

    internal static void Finish()
    {
        var grant = EndGameResult.CachedXpGrantResult;
        if (grant != null)
        {
            LastXp = (int)grant.GrantedXp;
            if (grant.GrantedXp > 0u)
                real = (int)grant.NewLevel + 1;
            if (grant.NewLevel > grant.OldLevel)
                LevelsGained += (int)(grant.NewLevel - grant.OldLevel);
        }

        NewMatch();
        Cap();
    }

    internal static void Claim()
    {
        if (!NocturneConfig.LevelFarm.Value || !Utils.Host)
            return;

        PlayerControl me = PlayerControl.LocalPlayer;
        if (me != null && !NocturneForceRoles.ImpTeam(NocturneForceRoles.Roles[NocturneForceRoles.IndexOf(me.PlayerId)].Role))
            NocturneForceRoles.Set(me.PlayerId, RoleTypes.Impostor);
    }

    internal static void Tick()
    {
        PlayerControl me = PlayerControl.LocalPlayer;
        if (me == null || !Utils.Host)
        {
            lastAt = 0f;
            return;
        }

        float now = Time.unscaledTime;
        if (now - speedAt >= 1f)
        {
            Speed = Ui.RoundToInt((Total - speedTotal) / (now - speedAt));
            speedAt = now;
            speedTotal = Total;
        }

        if (now >= capAt)
        {
            capAt = now + 1f;
            Cap();
            if (!NocturneConfig.LevelFarm.Value)
                return;
        }

        if (LobbyBehaviour.Instance != null)
        {
            lastAt = 0f;
            Lobby(now);
            return;
        }

        startAt = -1f;
        if (ShipStatus.Instance == null || now < endAt || !Imp(me)
            || MeetingHud.Instance != null || ExileController.Instance != null || IntroCutscene.Instance != null
            || AmongUsClient.Instance.IsGameOver)
        {
            lastAt = 0f;
            return;
        }

        float dt = lastAt > 0f ? Ui.Min(now - lastAt, MaxStep) : 0f;
        lastAt = now;
        acc += dt * Rate;
        int n = Ui.Min((int)acc, MaxPerFrame);
        if (n <= 0)
            return;
        acc = Ui.Min(acc - n, 1f);

        Kills += n;
        Total += n;
        Busy = true;
        try
        {
            if (now >= showAt)
            {
                showAt = now + ShowGap;
                Visual(me);
            }

            Credit(me, n);
            Send(me, n);
        }
        finally
        {
            Busy = false;
        }

        if (Kills >= Goal)
        {
            endAt = now + 6f;
            End();
        }
    }

    private static void Fault(Exception ex)
    {
        if (Faults++ == 0)
            NocturnePlugin.Logger?.LogWarning("[Farm] " + ex.Message);
    }

    private static void Visual(PlayerControl me)
    {
        try
        {
            me.protectedByGuardianId = me.PlayerId;
            me.MurderPlayer(me, Block);
        }
        catch (Exception ex)
        {
            Fault(ex);
        }

        me.protectedByGuardianId = -1;
    }

    private static void Credit(PlayerControl me, int n)
    {
        try
        {
            GameManager gm = GameManager.Instance;
            bool hns = gm != null && gm.IsHideAndSeek();
            Add(hns ? StatID.HideAndSeek_ImpostorKills : StatID.ImpostorKills, n);
            if (me.CurrentOutfitType == PlayerOutfitType.Shapeshifted)
                Add(StatID.Role_Shapeshifter_ShiftedKills, n);
        }
        catch (Exception ex)
        {
            Fault(ex);
        }
    }

    private static void End()
    {
        try
        {
            GameManager gm = GameManager.Instance;
            gm.RpcEndGame(gm.IsHideAndSeek() ? HnsByKill : ByKill, false);
        }
        catch (Exception ex)
        {
            Fault(ex);
        }
    }

    private static void Lobby(float now)
    {
        GameStartManager gsm = null;
        if (NocturneConfig.LevelFarmStart.Value && DestroyableSingleton<GameStartManager>.InstanceExists)
            gsm = DestroyableSingleton<GameStartManager>.Instance;
        if (gsm == null || gsm.startState != GameStartManager.StartingStates.NotStarting)
        {
            startAt = -1f;
            return;
        }

        if (startAt < 0f)
            startAt = now + Ui.Clamp(NocturneConfig.LevelFarmDelay.Value, 3, 120);
        if (now < startAt)
            return;

        startAt = -1f;
        gsm.MinPlayers = 1;
        try
        {
            Patches.StartControl.TryInstantStart(gsm);
            if (gsm.startState == GameStartManager.StartingStates.NotStarting)
                gsm.ReallyBegin(false);
        }
        catch { }
    }

    private static void Add(StatID id, int count)
    {
        var stats = DataManager.Player.Stats;
        if (!stats.IsTrackingStats)
            return;

        var all = stats.statsToIngest;
        if (!all.ContainsKey(id))
            all.Add(id, 0u);
        all[id] = all[id] + (uint)count;
    }

    private static void Send(PlayerControl me, int cycles)
    {
        try
        {
            byte color = (byte)me.Data.DefaultOutfit.ColorId;
            int per = Ui.Max(1, AmongUsClient.Instance.GetMaxMessagePackingLimit() / 2);
            while (cycles > 0)
            {
                int take = Ui.Min(cycles, per);
                RpcBatch batch = RpcBatch.All();
                for (int i = 0; i < take; i++)
                    batch.Protect(me, me, color).Murder(me, me, Block);
                batch.Send();
                cycles -= take;
            }
        }
        catch (Exception ex)
        {
            Fault(ex);
        }
    }

    internal static string Line()
    {
        float now = Time.unscaledTime;
        if (now - lineAt < 0.25f)
            return line;

        lineAt = now;
        PlayerControl me = PlayerControl.LocalPlayer;
        string count = NocturneText.T("Киллов в матче: ", "Kills this match: ") + Kills + " / " + Goal;
        if (!Utils.Host)
            line = NocturneText.T("Нужен хост лобби.", "Needs the lobby host.");
        else if (!NocturneConfig.LevelFarm.Value)
            line = count + NocturneText.T(" · выключен", " · off");
        else if (LobbyBehaviour.Instance != null)
            line = startAt > 0f ? count + NocturneText.T(" · старт через ", " · start in ") + Ui.CeilToInt(startAt - now) + NocturneText.T(" с", " s") : count;
        else if (now < endAt)
            line = count + NocturneText.T(" · завершаю матч", " · ending the match");
        else if (me != null && Imp(me))
            line = count + " · " + Speed + NocturneText.T("/с", "/s") + (Faults > 0 ? NocturneText.T(" · сбоев ", " · faults ") + Faults : string.Empty);
        else
            line = count + NocturneText.T(" · нет роли преда", " · not an impostor");
        return line;
    }
}
