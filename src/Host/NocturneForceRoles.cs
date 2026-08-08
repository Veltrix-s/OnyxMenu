using System.Collections.Generic;
using AmongUs.GameOptions;
using HarmonyLib;

namespace Nocturne;

internal static class NocturneForceRoles
{
    internal static readonly (string Ru, string En, RoleTypes Role)[] Roles =
    {
        ("Без форса", "No force", (RoleTypes)255),
        ("Мирный", "Crewmate", RoleTypes.Crewmate),
        ("Импостер", "Impostor", RoleTypes.Impostor),
        ("Учёный", "Scientist", RoleTypes.Scientist),
        ("Инженер", "Engineer", RoleTypes.Engineer),
        ("Ангел", "Guardian Angel", RoleTypes.GuardianAngel),
        ("Оборотень", "Shapeshifter", RoleTypes.Shapeshifter),
        ("Фантом", "Phantom", RoleTypes.Phantom),
        ("Следопыт", "Tracker", RoleTypes.Tracker),
        ("Паникёр", "Noisemaker", RoleTypes.Noisemaker),
        ("Детектив", "Detective", RoleTypes.Detective),
        ("Гадюка", "Viper", RoleTypes.Viper),
    };

    private static readonly Dictionary<byte, RoleTypes> _forced = new Dictionary<byte, RoleTypes>();

    internal static int Count => _forced.Count;

    internal static bool Host()
    {
        try { return AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost; }
        catch { return false; }
    }

    internal static string Name(int idx)
    {
        if (idx < 0 || idx >= Roles.Length) idx = 0;
        return NocturneText.T(Roles[idx].Ru, Roles[idx].En);
    }

    internal static int IndexOf(byte pid)
    {
        if (_forced.TryGetValue(pid, out RoleTypes r))
            for (int i = 1; i < Roles.Length; i++)
                if (Roles[i].Role == r) return i;
        return 0;
    }

    internal static void Set(byte pid, RoleTypes role) => _forced[pid] = role;

    internal static void Cycle(byte pid)
    {
        int next = (IndexOf(pid) + 1) % Roles.Length;
        if (next == 0) _forced.Remove(pid);
        else _forced[pid] = Roles[next].Role;
    }

    internal static void Clear() => _forced.Clear();

    internal static void RegisterDefault(byte pid, RoleTypes role)
    {
        if (!_forced.ContainsKey(pid)) _forced[pid] = role;
    }

    internal static bool ImpTeam(RoleTypes r) =>
        r == RoleTypes.Impostor || r == RoleTypes.Shapeshifter || r == RoleTypes.Phantom || r == RoleTypes.Viper;

    internal static void ForceNow(byte pid)
    {
        if (!Host() || LobbyBehaviour.Instance == null) return;
        PlayerControl pc = ById(pid);
        if (pc == null || pc.Data == null) return;
        RoleTypes role = _forced.TryGetValue(pid, out RoleTypes r) ? r : RoleTypes.Crewmate;
        EnsureRate(role);
        try { pc.RpcSetRole(role, false); } catch { try { pc.RpcSetRole(role); } catch { } }
        Prime(pc);
    }

    internal static bool Distribute()
    {
        if (_forced.Count == 0 || !Host()) return true;

        GameOptionsManager gom = GameOptionsManager.Instance;
        if (gom == null || gom.CurrentGameOptions == null) return true;
        GameManager gm = GameManager.Instance;
        if (gm == null || gm.LogicRoleSelection == null) return true;
        try { if (gm.IsHideAndSeek()) return true; }
        catch { return true; }
        IGameOptions opt = gom.CurrentGameOptions;
        LogicRoleSelection logic = gm.LogicRoleSelection;

        var players = new List<PlayerControl>();
        try
        {
            foreach (PlayerControl p in PlayerControl.AllPlayerControls)
                if (p != null && p.Data != null && !p.Data.Disconnected && !p.Data.IsDead && p.PlayerId < 100)
                    players.Add(p);
        }
        catch { return true; }
        if (players.Count == 0) return true;

        var imps = new List<PlayerControl>();
        foreach (PlayerControl p in players)
            if (_forced.TryGetValue(p.PlayerId, out RoleTypes r) && ImpTeam(r)) imps.Add(p);

        int num;
        try { num = opt.GetInt(Int32OptionNames.NumImpostors); } catch { num = 1; }
        if (imps.Count > 0) num = imps.Count;
        else if (num >= players.Count) num = players.Count - 1;
        if (num < 1) num = 1;

        var rng = new System.Random();
        while (imps.Count < num)
        {
            var pool = new List<PlayerControl>();
            foreach (PlayerControl p in players) if (!imps.Contains(p)) pool.Add(p);
            if (pool.Count == 0) break;
            imps.Add(pool[rng.Next(pool.Count)]);
        }

        var impInfo = new Il2CppSystem.Collections.Generic.List<NetworkedPlayerInfo>();
        var crewInfo = new Il2CppSystem.Collections.Generic.List<NetworkedPlayerInfo>();
        foreach (PlayerControl p in players)
        {
            if (imps.Contains(p)) impInfo.Add(p.Data);
            else crewInfo.Add(p.Data);
        }

        try
        {
            logic.AssignRolesForTeam(impInfo, opt, (RoleTeamTypes)1, int.MaxValue, new Il2CppSystem.Nullable<RoleTypes>());
            logic.AssignRolesForTeam(crewInfo, opt, (RoleTeamTypes)0, int.MaxValue, new Il2CppSystem.Nullable<RoleTypes>(RoleTypes.Crewmate));
        }
        catch { return true; }

        foreach (PlayerControl p in players)
        {
            if (!_forced.TryGetValue(p.PlayerId, out RoleTypes role)) continue;
            if (role == RoleTypes.Crewmate || role == RoleTypes.Impostor || (int)role == 255) continue;
            try { RoleManager.Instance.SetRole(p, role); } catch { }
            try { p.RpcSetRole(role, false); } catch { }
        }

        foreach (PlayerControl p in players) Refresh(p);
        return false;
    }

    private static void Refresh(PlayerControl p)
    {
        try
        {
            if (p == null || p.Data == null) return;
            if (p.Data.Role != null) p.Data.Role.Initialize(p);
            if (ImpTeam(p.Data.RoleType)) p.SetKillTimer(0f);
        }
        catch { }
    }

    private static void EnsureRate(RoleTypes role)
    {
        if (role == RoleTypes.Crewmate || role == RoleTypes.Impostor || (int)role == 255) return;
        try
        {
            if (!NocturneLobbySettings.Ready() || NocturneLobbySettings.RoleNum(role) > 0) return;
            NocturneLobbySettings.SetRole(role, 1, 100);
        }
        catch { }
    }

    private static void Prime(PlayerControl pc)
    {
        try
        {
            if (pc == null || pc.Data == null || pc.Data.Role == null) return;
            pc.Data.Role.SetCooldown();
        }
        catch { }
    }

    private static PlayerControl ById(byte pid)
    {
        try { foreach (PlayerControl p in PlayerControl.AllPlayerControls) if (p != null && p.PlayerId == pid) return p; } catch { }
        return null;
    }
}

[HarmonyPatch(typeof(RoleManager), "SelectRoles")]
internal static class NocturneForceRolesSelectPatch
{
    public static bool Prefix()
    {
        try { return NocturneForceRoles.Distribute(); }
        catch { return true; }
    }
}

[HarmonyPatch(typeof(LobbyBehaviour), "Start")]
internal static class NocturneForceRolesResetPatch
{
    public static void Postfix() => NocturneForceRoles.Clear();
}
