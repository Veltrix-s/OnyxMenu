using HarmonyLib;
using UnityEngine;

namespace Nocturne;

[HarmonyPatch(typeof(EngineerRole))]
internal static class HnsVentUses
{
    private static EngineerRole _role;
    private static int _savedUses;
    private static float _next;

    [HarmonyPatch(nameof(EngineerRole.Initialize))]
    [HarmonyPrefix]
    public static void BeforeInitialize(EngineerRole __instance)
    {
        if (_role == __instance)
            Restore();
    }

    [HarmonyPatch(nameof(EngineerRole.FixedUpdate))]
    [HarmonyPostfix]
    public static void Postfix(EngineerRole __instance)
    {
        if (!NocturneConfig.HnsUnlimitedVents.Value)
        {
            if (_role != null)
                Restore();
            return;
        }

        if (__instance == null) return;
        PlayerControl pc = __instance.Player;
        if (pc == null || pc != PlayerControl.LocalPlayer)
            return;

        float now = Time.unscaledTime;
        if (now < _next) return;
        _next = now + 0.1f;

        GameManager gm = GameManager.Instance;
        if (pc.Data == null || pc.Data.IsDead || gm == null || !gm.IsHideAndSeek())
        {
            Restore();
            return;
        }

        if (_role != __instance)
        {
            Restore();
            _role = __instance;
            _savedUses = __instance.usesRemaining;
        }

        if (__instance.usesRemaining == int.MaxValue)
            return;
        __instance.usesRemaining = int.MaxValue;
        if (__instance.buttonManager != null)
            __instance.buttonManager.SetInfiniteUses();
    }

    private static void Restore()
    {
        EngineerRole role = _role;
        _role = null;
        if (role == null) return;

        role.usesRemaining = _savedUses;
        PlayerControl pc = role.Player;
        if (pc != null && pc.Data != null && pc.Data.Role == role && role.buttonManager != null)
            role.buttonManager.SetUsesRemaining(_savedUses);
    }
}
