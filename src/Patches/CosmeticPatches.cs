using System;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppSystem.Collections.Generic;

namespace Nocturne.Patches;

internal static class CosmeticAccess
{
    private static bool applyingDuplicateColor;

    internal static bool SupportedPlatform()
    {
        return !OperatingSystem.IsAndroid();
    }

    internal static bool OverridePurchase(ref bool purchaseResult)
    {
        if (!NocturneConfig.FreeCosmetics.Value) return HarmonyControl.Continue;
        purchaseResult = true;
        return HarmonyControl.SkipOriginal;
    }

    internal static void PublishCatalog(HatManager manager)
    {
        if (!NocturneConfig.FreeCosmetics.Value) return;

        var b = manager.allBundles.GetEnumerator();
        while (b.MoveNext()) b.Current.Free = true;
        var fb = manager.allFeaturedBundles.GetEnumerator();
        while (fb.MoveNext()) fb.Current.Free = true;
        var cubes = manager.allFeaturedCubes.GetEnumerator();
        while (cubes.MoveNext()) cubes.Current.Free = true;
        var items = manager.allFeaturedItems.GetEnumerator();
        while (items.MoveNext()) items.Current.Free = true;

        FreeArray(manager.allHats);
        FreeArray(manager.allNamePlates);
        FreeArray(manager.allPets);
        FreeArray(manager.allSkins);
        FreeArray(manager.allVisors);

        var star = manager.allStarBundles.GetEnumerator();
        while (star.MoveNext()) star.Current.price = 0;
    }

    private static readonly System.Collections.Generic.Dictionary<byte, float> nextHide = new System.Collections.Generic.Dictionary<byte, float>();

    internal static void HidePlayerLoadout(PlayerControl player)
    {
        if (!NocturneConfig.HideCosmeticsInMatch.Value || player == null) return;

        byte pid;
        try { pid = player.PlayerId; } catch { return; }
        float now = UnityEngine.Time.unscaledTime;
        if (nextHide.TryGetValue(pid, out float at) && now < at) return;
        nextHide[pid] = now + 0.4f;

        try
        {
            NetworkedPlayerInfo.PlayerOutfit outfit = player.CurrentOutfit;
            if (outfit == null)
            {
                return;
            }

            if (!string.IsNullOrEmpty(outfit.HatId)) player.SetHat(string.Empty, 0);
            if (!string.IsNullOrEmpty(outfit.SkinId)) player.SetSkin(string.Empty, 0);
            if (!string.IsNullOrEmpty(outfit.VisorId)) player.SetVisor(string.Empty, 0);
            if (!string.IsNullOrEmpty(outfit.NamePlateId)) player.SetNamePlate(string.Empty);
            if (!string.IsNullOrEmpty(outfit.PetId)) player.SetPet(string.Empty, 0);
        }
        catch { }
    }

    internal static bool AllowDuplicateColor(PlayerControl player, byte bodyColor)
    {
        if (applyingDuplicateColor || !NocturneConfig.AllowDuplicateColors.Value || player == null || AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost)
        {
            return HarmonyControl.Continue;
        }

        if (bodyColor == byte.MaxValue)
        {
            return HarmonyControl.Continue;
        }

        try
        {
            applyingDuplicateColor = true;
            player.RpcSetColor(bodyColor);
            return HarmonyControl.SkipOriginal;
        }
        catch
        {
            return HarmonyControl.Continue;
        }
        finally
        {
            applyingDuplicateColor = false;
        }
    }

    private static void FreeArray<T>(Il2CppArrayBase<T> data) where T : CosmeticData
    {
        foreach (T item in data)
        {
            item.Free = true;
        }
    }
}

[HarmonyPatch(typeof(PlayerPurchasesData), nameof(PlayerPurchasesData.GetPurchase))]
internal static class PurchaseStatePatch
{
    public static bool Prepare()
    {
        return CosmeticAccess.SupportedPlatform();
    }

    public static bool Prefix(ref bool __result)
    {
        return CosmeticAccess.OverridePurchase(ref __result);
    }
}

[HarmonyPatch(typeof(HatManager), nameof(HatManager.Initialize))]
internal static class CosmeticCatalogPatch
{
    public static bool Prepare()
    {
        return CosmeticAccess.SupportedPlatform();
    }

    public static void Postfix(HatManager __instance)
    {
        CosmeticAccess.PublishCatalog(__instance);
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
internal static class MatchCosmeticHiderPatch
{
    public static void Postfix(PlayerControl __instance)
    {
        CosmeticAccess.HidePlayerLoadout(__instance);
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CheckColor))]
internal static class AllowDuplicateColorsCheckColorPatch
{
    public static bool Prefix(PlayerControl __instance, byte bodyColor)
    {
        return CosmeticAccess.AllowDuplicateColor(__instance, bodyColor);
    }
}
