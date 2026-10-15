using AmongUs.GameOptions;
using HarmonyLib;
using UnityEngine;

namespace Nocturne.Patches;

internal static class NocturneClassic
{
    internal static bool On => NocturneConfig.ClassicBody.Value;
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.BodyType), MethodType.Getter)]
internal static class NocturneClassicBodyGetPatch
{
    public static bool Prefix(PlayerControl __instance, ref PlayerBodyTypes __result)
    {
        if (__instance == null)
        {
            __result = PlayerBodyTypes.Normal;
            return false;
        }
        if (!NocturneClassic.On) return true;
        __result = PlayerBodyTypes.Classic;
        return false;
    }
}

[HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.SetBodyType))]
internal static class NocturneClassicBodySetPatch
{
    public static void Prefix([HarmonyArgument(0)] ref PlayerBodyTypes bodyType)
    {
        if (NocturneClassic.On) bodyType = PlayerBodyTypes.Classic;
    }
}

[HarmonyPatch(typeof(NormalGameManager), nameof(NormalGameManager.GetDeadBody))]
internal static class ClassicDeadBodyPatch
{
    public static bool Prefix(NormalGameManager __instance, RoleBehaviour impostorRole, ref DeadBody __result)
    {
        if (!NocturneClassic.On || (impostorRole != null && impostorRole.Role == RoleTypes.Viper)) return true;

        __result = __instance.deadBodyPrefab[2];
        return false;
    }
}

[HarmonyPatch(typeof(HideAndSeekManager), nameof(HideAndSeekManager.GetDeadBody))]
internal static class ClassicHnsDeadBodyPatch
{
    public static bool Prefix(HideAndSeekManager __instance, ref DeadBody __result)
    {
        if (!NocturneClassic.On) return true;

        __result = __instance.deadBodyPrefab[1];
        return false;
    }
}

[HarmonyPatch(typeof(KillOverlay), nameof(KillOverlay.ShowKillAnimation), new[] { typeof(NetworkedPlayerInfo), typeof(NetworkedPlayerInfo) })]
internal static class ClassicKillAnimPatch
{
    public static bool Prefix(KillOverlay __instance, NetworkedPlayerInfo killer, NetworkedPlayerInfo victim)
    {
        if (!NocturneClassic.On || killer.Object == null || GameManager.Instance.IsHideAndSeek()) return true;

        var pool = killer.Role.CustomKillAnimations;
        if (pool.Length == 0) pool = __instance.ClassicKillAnims;
        if (pool.Length == 0) return true;

        __instance.ShowKillAnimation(pool[Random.Range(0, pool.Length)], killer, victim);
        return false;
    }
}

[HarmonyPatch(typeof(PoolablePlayer), "Awake")]
[HarmonyPatch(typeof(PoolablePlayer), nameof(PoolablePlayer.UpdateFromDataManager), new[] { typeof(PlayerMaterial.MaskType) })]
[HarmonyPatch(typeof(PoolablePlayer), nameof(PoolablePlayer.UpdateFromDataManager), new[] { typeof(PlayerMaterial.MaskType), typeof(int) })]
internal static class ClassicPortraitPatch
{
    public static void Postfix(PoolablePlayer __instance)
    {
        if (NocturneClassic.On) __instance.SetBodyType(PlayerBodyTypes.Classic);
    }
}
