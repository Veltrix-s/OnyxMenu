using System;
using HarmonyLib;

namespace Nocturne.Patches;

internal static class BodyModeResolver
{
    private const int Horse = 1;
    private const int Seeker = 2;
    private const int Long = 3;
    private const int LongHorse = 4;

    internal static bool TryGetForcedBody(out PlayerBodyTypes bodyType)
    {
        int? value = NocturneConfig.BodyMode.Value switch
        {
            "Horse" => Horse,
            "Seeker" => Seeker,
            "Long" => Long,
            "LongHorse" => LongHorse,
            _ => null,
        };

        bodyType = value.HasValue ? (PlayerBodyTypes)value.Value : default;
        return value.HasValue;
    }

    internal static void ForcePhysicsBody(ref PlayerBodyTypes bodyType)
    {
        if (TryGetForcedBody(out PlayerBodyTypes replacement))
        {
            bodyType = replacement;
        }
    }

    internal static bool TryOverrideBodyGetter(ref PlayerBodyTypes result)
    {
        if (!TryGetForcedBody(out PlayerBodyTypes forcedBody))
        {
            return HarmonyControl.Continue;
        }

        result = forcedBody;
        return HarmonyControl.SkipOriginal;
    }
}

internal static class LongBodyBootstrap
{
    internal static void Rewire(LongBoiPlayerBody body)
    {
        CosmeticsLayer layer = body.cosmeticLayer;
        layer.OnSetBodyAsGhost += (Action)body.SetPoolableGhost;
        layer.OnColorChange += (Action<int>)body.SetHeightFromColor;
        layer.OnCosmeticSet += (Action<string, int, CosmeticsLayer.CosmeticKind>)body.OnCosmeticSet;
    }

    internal static void Start(LongBoiPlayerBody body)
    {
        body.ShouldLongAround = true;

        if (body.hideCosmeticsQC)
        {
            body.cosmeticLayer.SetHatVisorVisible(false);
        }

        body.SetupNeckGrowth(false, true);
        if (body.isExiledPlayer && (ShipStatus.Instance == null || (int)ShipStatus.Instance.Type != 3))
        {
            body.cosmeticLayer.AdjustCosmeticRotations(-17.75f);
        }

        if (!body.isPoolablePlayer)
        {
            body.cosmeticLayer.ValidateCosmetics();
        }
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.BodyType), MethodType.Getter)]
internal static class PlayerBodyGetterPatch
{
    public static bool Prefix(PlayerControl __instance, ref PlayerBodyTypes __result)
    {
        if (__instance == null) { __result = (PlayerBodyTypes)0; return HarmonyControl.SkipOriginal; }
        return BodyModeResolver.TryOverrideBodyGetter(ref __result);
    }
}

[HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.SetBodyType))]
internal static class PhysicsBodyPatch
{
    public static void Prefix(ref PlayerBodyTypes bodyType)
    {
        BodyModeResolver.ForcePhysicsBody(ref bodyType);
    }
}

[HarmonyPatch(typeof(LongBoiPlayerBody), nameof(LongBoiPlayerBody.Awake))]
internal static class LongBodyAwakePatch
{
    public static bool Prefix(LongBoiPlayerBody __instance)
    {
        LongBodyBootstrap.Rewire(__instance);
        return HarmonyControl.SkipOriginal;
    }
}

[HarmonyPatch(typeof(LongBoiPlayerBody), nameof(LongBoiPlayerBody.Start))]
internal static class LongBodyStartPatch
{
    public static bool Prefix(LongBoiPlayerBody __instance)
    {
        LongBodyBootstrap.Start(__instance);
        return HarmonyControl.SkipOriginal;
    }
}
