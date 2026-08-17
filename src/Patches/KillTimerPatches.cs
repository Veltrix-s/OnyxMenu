using System.Collections.Generic;
using AmongUs.GameOptions;
using HarmonyLib;

namespace Nocturne.Patches;

internal static class KillTimers
{
    private static readonly Dictionary<byte, float> cd = new Dictionary<byte, float>();
    private static readonly List<byte> keys = new List<byte>();

    internal static float KillCooldown()
    {
        try { return GameOptionsManager.Instance.CurrentGameOptions.GetFloat(FloatOptionNames.KillCooldown); }
        catch { return 30f; }
    }

    internal static bool Killer(PlayerControl p)
    {
        try { return p.Data != null && p.Data.Role != null && p.Data.Role.CanUseKillButton && !p.Data.IsDead; }
        catch { return false; }
    }

    internal static void SeedAll(float value)
    {
        var all = PlayerControl.AllPlayerControls;
        if (all == null) return;
        var e = all.GetEnumerator();
        while (e.MoveNext())
        {
            PlayerControl p = e.Current;
            if (p != null && Killer(p)) cd[p.PlayerId] = value;
        }
    }

    internal static void Hit(byte id) => cd[id] = KillCooldown();

    internal static void Tick(float dt)
    {
        if (cd.Count == 0) return;
        keys.Clear();
        keys.AddRange(cd.Keys);
        for (int i = 0; i < keys.Count; i++)
        {
            float v = cd[keys[i]] - dt;
            cd[keys[i]] = v < 0f ? 0f : v;
        }
    }

    internal static bool TryGet(byte id, out float seconds) => cd.TryGetValue(id, out seconds);
}

[HarmonyPatch(typeof(SabotageSystemType), nameof(SabotageSystemType.SetInitialSabotageCooldown))]
internal static class KillTimersSeedPatch
{
    public static void Postfix()
    {
        if (NocturneConfig.KillTimers.Value) KillTimers.SeedAll(KillTimers.KillCooldown());
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.MurderPlayer))]
internal static class KillTimersHitPatch
{
    public static void Postfix(PlayerControl __instance)
    {
        if (NocturneConfig.KillTimers.Value && __instance != null) KillTimers.Hit(__instance.PlayerId);
    }
}
