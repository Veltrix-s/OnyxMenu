using System;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using InnerNet;
using TMPro;
using UnityEngine;

namespace Nocturne.Patches;

internal static class LobbyBrowser
{
    private const string ExtendedLobbyScrollerName = "NocturneExtendedLobbyScroller";
    private const int ExtendedLobbyRowTarget = 24;

    private static int extendedLobbyScreenId;
    private static Scroller extendedLobbyScroller;

    internal static void RefreshLobbyTotal(FindAGameManager screen, HttpMatchmakerManager.FindGamesListFilteredResponse response)
    {
        if (NocturneConfig.RichLobbyRows.Value)
        {
            ((TMP_Text)screen.TotalText).text = response.Metadata.AllGamesCount.ToString();
        }
    }

    internal static void EnsureBigBrowser(FindAGameManager screen)
    {
        if (!NocturneConfig.RichLobbyRows.Value || screen == null || screen.gameContainers == null)
        {
            return;
        }

        int screenId = screen.GetInstanceID();
        if (extendedLobbyScreenId == screenId && extendedLobbyScroller != null)
        {
            return;
        }

        try
        {
            Il2CppReferenceArray<GameContainer> existingContainers = screen.gameContainers;
            int existingCount = existingContainers.Length;
            if (existingCount <= 0 || existingCount >= ExtendedLobbyRowTarget)
            {
                extendedLobbyScreenId = screenId;
                return;
            }

            GameContainer template = existingContainers[0];
            if (template == null)
            {
                return;
            }

            Transform rowParent = ((Component)template).transform.parent;
            if (rowParent == null)
            {
                return;
            }

            Transform oldScroller = rowParent.FindChild(ExtendedLobbyScrollerName);
            if (oldScroller != null && ((Component)oldScroller).GetComponent<Scroller>() != null)
            {
                extendedLobbyScroller = ((Component)oldScroller).GetComponent<Scroller>();
                extendedLobbyScreenId = screenId;
                return;
            }

            float rowSpacing = DetectLobbyRowSpacing(existingContainers);
            GameObject scrollerObject = new GameObject(ExtendedLobbyScrollerName);
            scrollerObject.transform.SetParent(rowParent, false);
            scrollerObject.transform.localPosition = Vector3.zero;
            scrollerObject.transform.localScale = Vector3.one;

            Scroller scroller = scrollerObject.AddComponent<Scroller>();
            scroller.Inner = scrollerObject.transform;
            scroller.MouseMustBeOverToScroll = true;
            scroller.allowY = true;
            scroller.ScrollWheelSpeed = 0.38f;
            scroller.SetYBoundsMin(0f);
            scroller.SetYBoundsMax(Mathf.Max(0f, (ExtendedLobbyRowTarget - existingCount) * rowSpacing));

            BoxCollider2D clickMask = ((Component)rowParent).GetComponent<BoxCollider2D>();
            if (clickMask == null)
            {
                clickMask = ((Component)rowParent).gameObject.AddComponent<BoxCollider2D>();
            }

            clickMask.size = new Vector2(16f, 12f);
            ((PassiveUiElement)scroller).ClickMask = clickMask;

            GameContainer[] expanded = new GameContainer[ExtendedLobbyRowTarget];
            Vector3 firstLocalPosition = ((Component)template).transform.localPosition;
            for (int i = 0; i < existingCount; i++)
            {
                GameContainer container = existingContainers[i];
                if (container == null)
                {
                    continue;
                }

                Transform transform = ((Component)container).transform;
                transform.SetParent(scrollerObject.transform, true);
                Vector3 position = transform.localPosition;
                position.z = 25f;
                transform.localPosition = position;
                expanded[i] = container;
            }

            for (int i = existingCount; i < expanded.Length; i++)
            {
                GameContainer clone = UnityEngine.Object.Instantiate(template, scrollerObject.transform);
                Transform cloneTransform = ((Component)clone).transform;
                cloneTransform.localPosition = new Vector3(firstLocalPosition.x, firstLocalPosition.y - (rowSpacing * i), 25f);
                cloneTransform.localScale = ((Component)template).transform.localScale;
                expanded[i] = clone;
            }

            screen.gameContainers = new Il2CppReferenceArray<GameContainer>(expanded);
            extendedLobbyScroller = scroller;
            extendedLobbyScreenId = screenId;
        }
        catch (Exception error)
        {
            NocturnePlugin.Logger?.LogWarning((object)$"Extended lobby browser setup failed: {error.Message}");
        }
    }

    internal static void ResetExtendedLobbyBrowserScroll()
    {
        try
        {
            if (extendedLobbyScroller != null)
            {
                extendedLobbyScroller.ScrollRelative(new Vector2(0f, -100f));
            }
        }
        catch { }
    }

    internal static void DecorateLobbyRow(GameContainer row)
    {
        if (!NocturneConfig.RichLobbyRows.Value)
        {
            return;
        }

        GameListing listing = row.gameListing;
        string capacity = ((TMP_Text)row.capacity).text;
        string[] details =
        {
            "<#0000>000000000000000</color>",
            listing.TrueHostName,
            capacity,
            $"<#fb0>{GameCode.IntToGameName(listing.GameId)}</color>",
            $"<#b0f>{PlatformLabel(listing.Platform)}</color>",
            FormatAge(listing.Age),
            "<#0000>000000000000000</color>",
        };

        ((TMP_Text)row.capacity).text = "<size=40%>" + string.Join("\n", details) + "</size>";
    }

    internal static void ApplyHostFilter(FindAGameManager screen)
    {
        string q = NocturneConfig.LobbySearchHost != null ? (NocturneConfig.LobbySearchHost.Value ?? string.Empty).Trim() : string.Empty;
        if (q.Length == 0 || screen == null || screen.gameContainers == null) return;

        try
        {
            float spacing = DetectLobbyRowSpacing(screen.gameContainers);
            float baseY = ((Component)screen.gameContainers[0]).transform.localPosition.y;
            int shown = 0;

            for (int i = 0; i < screen.gameContainers.Length; i++)
            {
                GameContainer row = screen.gameContainers[i];
                if (row == null) continue;

                GameObject go = ((Component)row).gameObject;
                if (!go.activeSelf) continue;

                GameListing listing = row.gameListing;
                string host = listing != null ? (listing.TrueHostName ?? string.Empty) : string.Empty;
                if (host.IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0) { go.SetActive(false); continue; }

                Transform t = ((Component)row).transform;
                Vector3 p = t.localPosition;
                t.localPosition = new Vector3(p.x, baseY - shown * spacing, p.z);
                shown++;
            }
        }
        catch { }
    }

    private static string FormatAge(int seconds)
    {
        return $"Age: {seconds / 60}:{(seconds % 60 < 10 ? "0" : "")}{seconds % 60}";
    }

    private static float DetectLobbyRowSpacing(Il2CppReferenceArray<GameContainer> containers)
    {
        try
        {
            if (containers != null && containers.Length > 1 && containers[0] != null && containers[1] != null)
            {
                float firstY = ((Component)containers[0]).transform.localPosition.y;
                float secondY = ((Component)containers[1]).transform.localPosition.y;
                float detected = Mathf.Abs(firstY - secondY);
                if (detected > 0.05f)
                {
                    return detected;
                }
            }
        }
        catch { }

        return 0.75f;
    }

    private static string PlatformLabel(Platforms platform)
    {
        return (int)platform switch
        {
            1 => "Epic",
            2 => "Steam",
            3 => "Mac",
            4 => "Microsoft Store",
            5 => "Itch.io",
            6 => "iPhone / iPad",
            7 => "Android",
            8 => "Nintendo Switch",
            9 => "Xbox",
            10 => "PlayStation",
            _ => "Unknown",
        };
    }
}

[HarmonyPatch(typeof(FindAGameManager), "HandleList")]
internal static class LobbyCountPatch
{
    public static void Postfix(HttpMatchmakerManager.FindGamesListFilteredResponse response, FindAGameManager __instance)
    {
        LobbyBrowser.RefreshLobbyTotal(__instance, response);
        LobbyBrowser.ApplyHostFilter(__instance);
    }
}

[HarmonyPatch(typeof(FindAGameManager), "Start")]
internal static class ExtendedLobbyBrowserStartPatch
{
    public static void Prefix(FindAGameManager __instance)
    {
        LobbyBrowser.EnsureBigBrowser(__instance);
    }
}

[HarmonyPatch(typeof(FindAGameManager), "RefreshList")]
internal static class ExtendedLobbyBrowserRefreshPatch
{
    public static void Postfix()
    {
        LobbyBrowser.ResetExtendedLobbyBrowserScroll();
    }
}

[HarmonyPatch(typeof(GameContainer), "SetupGameInfo")]
internal static class LobbyRowPatch
{
    public static void Postfix(GameContainer __instance)
    {
        LobbyBrowser.DecorateLobbyRow(__instance);
    }
}
