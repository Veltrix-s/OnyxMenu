using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using InnerNet;
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
            screen.TotalText.text = response.Metadata.AllGamesCount.ToString();
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

            Transform rowParent = template.transform.parent;
            if (rowParent == null)
            {
                return;
            }

            Transform oldScroller = rowParent.FindChild(ExtendedLobbyScrollerName);
            if (oldScroller != null && oldScroller.GetComponent<Scroller>() != null)
            {
                extendedLobbyScroller = oldScroller.GetComponent<Scroller>();
                extendedLobbyScreenId = screenId;
                return;
            }

            float rowSpacing = DetectLobbyRowSpacing(existingContainers);
            GameObject scrollerObject = new GameObject(ExtendedLobbyScrollerName);
            scrollerObject.transform.SetParent(rowParent, false);
            scrollerObject.transform.localPosition = Ui.Zero3;
            scrollerObject.transform.localScale = Ui.One3;

            Scroller scroller = scrollerObject.AddComponent<Scroller>();
            scroller.Inner = scrollerObject.transform;
            scroller.MouseMustBeOverToScroll = true;
            scroller.allowY = true;
            scroller.ScrollWheelSpeed = 0.38f;
            scroller.SetYBoundsMin(0f);
            scroller.SetYBoundsMax(Ui.Max(0f, (ExtendedLobbyRowTarget - existingCount) * rowSpacing));

            BoxCollider2D clickMask = rowParent.GetComponent<BoxCollider2D>();
            if (clickMask == null)
            {
                clickMask = rowParent.gameObject.AddComponent<BoxCollider2D>();
            }

            clickMask.size = Ui.V(16f, 12f);
            scroller.ClickMask = clickMask;

            GameContainer[] expanded = new GameContainer[ExtendedLobbyRowTarget];
            Vector3 firstLocalPosition = template.transform.localPosition;
            for (int i = 0; i < existingCount; i++)
            {
                GameContainer container = existingContainers[i];
                if (container == null)
                {
                    continue;
                }

                Transform transform = container.transform;
                transform.SetParent(scrollerObject.transform, true);
                Vector3 position = transform.localPosition;
                position.z = 25f;
                transform.localPosition = position;
                expanded[i] = container;
            }

            for (int i = existingCount; i < expanded.Length; i++)
            {
                GameContainer clone = UnityEngine.Object.Instantiate(template, scrollerObject.transform);
                Transform cloneTransform = clone.transform;
                cloneTransform.localPosition = Ui.V3(firstLocalPosition.x, firstLocalPosition.y - (rowSpacing * i), 25f);
                cloneTransform.localScale = template.transform.localScale;
                expanded[i] = clone;
            }

            screen.gameContainers = new Il2CppReferenceArray<GameContainer>(expanded);
            extendedLobbyScroller = scroller;
            extendedLobbyScreenId = screenId;
        }
        catch (Exception error)
        {
            NocturnePlugin.Logger?.LogWarning($"Extended lobby browser setup failed: {error.Message}");
        }
    }

    internal static void ResetExtendedLobbyBrowserScroll()
    {
        if (extendedLobbyScroller != null)
        {
            extendedLobbyScroller.ScrollRelative(Ui.V(0f, -100f));
        }
    }

    internal static void DecorateLobbyRow(GameContainer row)
    {
        if (!NocturneConfig.RichLobbyRows.Value)
        {
            return;
        }

        GameListing listing = row.gameListing;
        string capacity = row.capacity.text;
        bool server = NocturneConfig.CustomServerEnabled.Value;
        var details = new List<string>(8)
        {
            "<#0000>000000000000000</color>",
            listing.TrueHostName,
            capacity,
            $"<#fb0>{GameCode.IntToGameName(listing.GameId)}</color>",
            $"<#b0f>{PlatformLabel(listing.Platform)}</color>",
            FormatAge(listing.Age),
        };
        if (server)
            details.Add($"<#0fb>{listing.IPString}:{listing.Port}</color>");
        details.Add("<#0000>000000000000000</color>");

        row.capacity.text = (server ? "<size=35%>" : "<size=40%>") + string.Join("\n", details) + "</size>";
    }

    internal static void ApplyHostFilter(FindAGameManager screen)
    {
        string q = (NocturneConfig.LobbySearchHost.Value ?? string.Empty).Trim();
        if (q.Length == 0 || screen == null || screen.gameContainers == null) return;

        try
        {
            float spacing = DetectLobbyRowSpacing(screen.gameContainers);
            float baseY = screen.gameContainers[0].transform.localPosition.y;
            int shown = 0;

            for (int i = 0; i < screen.gameContainers.Length; i++)
            {
                GameContainer row = screen.gameContainers[i];
                if (row == null) continue;

                GameObject go = row.gameObject;
                if (!go.activeSelf)
                    continue;

                GameListing listing = row.gameListing;
                string host = listing != null ? (listing.TrueHostName ?? string.Empty) : string.Empty;
                if (host.IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    go.SetActive(false);
                    continue;
                }

                Transform t = row.transform;
                Vector3 p = t.localPosition;
                t.localPosition = Ui.V3(p.x, baseY - shown * spacing, p.z);
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
                float firstY = containers[0].transform.localPosition.y;
                float secondY = containers[1].transform.localPosition.y;
                float detected = Ui.Abs(firstY - secondY);
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
        return platform switch
        {
            Platforms.StandaloneEpicPC => "Epic",
            Platforms.StandaloneSteamPC => "Steam",
            Platforms.StandaloneMac => "Mac",
            Platforms.StandaloneWin10 => "Microsoft Store",
            Platforms.StandaloneItch => "Itch.io",
            Platforms.IPhone => "iPhone / iPad",
            Platforms.Android => "Android",
            Platforms.Switch => "Nintendo Switch",
            Platforms.Xbox => "Xbox",
            Platforms.Playstation => "PlayStation",
            _ => "Unknown",
        };
    }
}

[HarmonyPatch(typeof(FindAGameManager), nameof(FindAGameManager.HandleList))]
internal static class LobbyCountPatch
{
    public static void Postfix(HttpMatchmakerManager.FindGamesListFilteredResponse response, FindAGameManager __instance)
    {
        LobbyBrowser.RefreshLobbyTotal(__instance, response);
        LobbyBrowser.ApplyHostFilter(__instance);
    }
}

[HarmonyPatch(typeof(FindAGameManager), nameof(FindAGameManager.Start))]
internal static class ExtendedLobbyBrowserStartPatch
{
    public static void Prefix(FindAGameManager __instance)
    {
        LobbyBrowser.EnsureBigBrowser(__instance);
    }
}

[HarmonyPatch(typeof(FindAGameManager), nameof(FindAGameManager.RefreshList))]
internal static class ExtendedLobbyBrowserRefreshPatch
{
    public static void Postfix()
    {
        LobbyBrowser.ResetExtendedLobbyBrowserScroll();
    }
}

[HarmonyPatch(typeof(GameContainer), nameof(GameContainer.SetupGameInfo))]
internal static class LobbyRowPatch
{
    public static void Postfix(GameContainer __instance)
    {
        LobbyBrowser.DecorateLobbyRow(__instance);
    }
}
