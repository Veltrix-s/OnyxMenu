using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Nocturne.Patches;

internal static class HeaderArt
{
    private enum Role
    {
        Strip,
        Glass,
        Close,
        Track,
        Handle,
        Tab,
        Cell,
        Pick
    }

    private sealed class Skin
    {
        internal SpriteRenderer Sr;
        internal int Id;
        internal Sprite Sprite;
        internal Vector2 Size;
        internal Color Color;
        internal Role Role;
        internal Sprite Art;
        internal bool Dyed;
    }

    private static readonly string[] Tracks =
    {
        "HatsGroup/UI_ScrollbarTrack",
        "PetsGroup/UI_ScrollbarTrack",
        "SkinGroup/UI_ScrollbarTrack",
        "VisorGroup/UI_ScrollbarTrack",
        "CubeGroup/UI_ScrollbarTrack",
        "NameplateGroup/UI_ScrollbarTrack"
    };

    private static readonly string[] Handles =
    {
        "HatsGroup/UI_Scrollbar",
        "PetsGroup/UI_Scrollbar",
        "SkinGroup/UI_Scrollbar",
        "VisorGroup/UI_Scrollbar",
        "CubeGroup/UI_Scrollbar",
        "NameplateGroup/UI_Scrollbar"
    };

    private static readonly List<Skin> Worn = new List<Skin>();
    private static readonly HashSet<int> Seen = new HashSet<int>();
    private static readonly HashSet<int> Scanned = new HashSet<int>();

    private static Sprite _strip;
    private static Sprite _close;
    private static Sprite _tab;
    private static Sprite _cell;
    private static Sprite _pick;
    private static bool _loaded;
    private static bool _failed;
    private static float _next;

    internal static void Open(Component menu, string closePath, bool grid) => Apply(menu, closePath, grid);

    internal static void Tick(Component menu, string closePath, bool grid)
    {
        if (_failed)
        {
            return;
        }

        if (!NocturneConfig.ChatBtnArt.Value)
        {
            if (Worn.Count > 0)
            {
                Release();
            }

            return;
        }

        if (Time.unscaledTime < _next)
        {
            return;
        }

        _next = Time.unscaledTime + 0.25f;
        Apply(menu, closePath, grid);
    }

    private static void Apply(Component menu, string closePath, bool grid)
    {
        if (_failed || !NocturneConfig.ChatBtnArt.Value)
        {
            return;
        }

        if (_loaded && !Alive())
        {
            Release();
            _loaded = false;
        }

        if (!_loaded && !Load())
        {
            return;
        }

        Prune();
        Transform root = menu.transform;
        Take(root.Find("Header"), Role.Strip, "UI_ShopHeader");
        Take(root.Find("Header/baseGlass"), Role.Glass, null);
        Take(root.Find(closePath), Role.Close, "closeButton");
        Transform tabs = root.Find("Header/Tabs");
        if (tabs != null && Scanned.Add(tabs.GetInstanceID()))
        {
            foreach (SpriteRenderer sr in tabs.GetComponentsInChildren<SpriteRenderer>(true))
            {
                Take(sr.transform, Role.Tab, "UI_Inventory_Selected");
            }
        }

        if (grid)
        {
            for (int i = 0; i < Tracks.Length; i++)
            {
                Take(root.Find(Tracks[i]), Role.Track, "UI_ScrollbarTrack");
                Take(root.Find(Handles[i]), Role.Handle, "UI_ScrollbarPip");
            }

            Chips(menu as PlayerCustomizationMenu);
        }

        NocturnePalette p = NocturneStyle.Current;
        for (int i = 0; i < Worn.Count; i++)
        {
            Paint(Worn[i], p);
        }
    }

    private static void Chips(PlayerCustomizationMenu menu)
    {
        if (menu == null || !Scanned.Add(menu.GetInstanceID()))
        {
            return;
        }

        for (int i = 0; i < menu.Tabs.Length; i++)
        {
            InventoryTab tab = menu.Tabs[i].Tab;
            if (tab == null || tab.ColorTabPrefab == null)
            {
                continue;
            }

            Transform chip = tab.ColorTabPrefab.transform;
            Take(chip.Find("Background"), Role.Cell, "UI_Inventory_Normal");
            Take(chip.Find("Background/SelectedHighlight"), Role.Pick, "UI_Inventory_Hover");
            Take(chip.Find("Background/ControllerHighlight"), Role.Pick, "UI_Inventory_Hover");
            Take(chip.Find("ForeGround/Shade"), Role.Tab, "UI_Inventory_Selected");
            Take(chip.Find("EquippedForeground/Shade"), Role.Tab, "UI_Inventory_Selected");
        }
    }

    internal static void Release()
    {
        for (int i = 0; i < Worn.Count; i++)
        {
            Skin s = Worn[i];
            if (s.Sr == null)
            {
                continue;
            }

            s.Sr.sprite = s.Sprite;
            if (s.Sr.drawMode != SpriteDrawMode.Simple)
            {
                s.Sr.size = s.Size;
            }

            s.Sr.color = s.Color;
        }

        Worn.Clear();
        Seen.Clear();
        Scanned.Clear();
    }

    private static void Prune()
    {
        for (int i = Worn.Count - 1; i >= 0; i--)
        {
            if (Worn[i].Sr == null)
            {
                Seen.Remove(Worn[i].Id);
                Worn.RemoveAt(i);
            }
        }
    }

    private static void Take(Transform t, Role role, string sprite)
    {
        if (t == null)
        {
            return;
        }

        SpriteRenderer sr = t.GetComponent<SpriteRenderer>();
        if (sr == null || sr.sprite == null || (sprite != null && sr.sprite.name != sprite) || !Seen.Add(sr.GetInstanceID()))
        {
            return;
        }

        Worn.Add(new Skin { Sr = sr, Id = sr.GetInstanceID(), Sprite = sr.sprite, Size = sr.size, Color = sr.color, Role = role, Art = Art(role) });
    }

    private static Sprite Art(Role role)
    {
        switch (role)
        {
            case Role.Strip:
                return _strip;
            case Role.Close:
                return _close;
            case Role.Tab:
                return _tab;
            case Role.Cell:
                return _cell;
            case Role.Pick:
                return _pick;
        }

        return null;
    }

    private static void Paint(Skin s, NocturnePalette p)
    {
        SpriteRenderer sr = s.Sr;
        if (s.Art != null && sr.sprite != s.Art)
        {
            Vector2 size = sr.size;
            sr.sprite = s.Art;
            if (sr.drawMode != SpriteDrawMode.Simple)
            {
                sr.size = size;
            }
        }

        switch (s.Role)
        {
            case Role.Glass:
                if (!s.Dyed)
                {
                    s.Dyed = true;
                    sr.color = Ui.C(s.Color.r, s.Color.g, s.Color.b, 0f);
                }

                break;
            case Role.Track:
                if (!s.Dyed)
                {
                    s.Dyed = true;
                    sr.color = FriendsArt.TrackColor;
                }

                break;
            case Role.Handle:
                Color now = sr.color;
                if (now.r != p.Accent.r || now.g != p.Accent.g || now.b != p.Accent.b || now.a != 1f)
                {
                    sr.color = Ui.C(p.Accent.r, p.Accent.g, p.Accent.b, 1f);
                }

                break;
            case Role.Pick:
                Color ring = sr.color;
                float r = p.Accent.r * s.Color.r, g = p.Accent.g * s.Color.g, b = p.Accent.b * s.Color.b;
                if (ring.r != r || ring.g != g || ring.b != b)
                {
                    sr.color = Ui.C(r, g, b, s.Color.a);
                }

                break;
        }
    }

    private static bool Alive() => _strip != null && _close != null && _tab != null && _cell != null && _pick != null;

    private static bool Load()
    {
        Vector2 pivot = Ui.V(88f / 177f, 98f / 187f);
        Vector4 none = new Vector4(0f, 0f, 0f, 0f);
        _strip = ChatArt.Art("strip_hd.png", new Vector4(64f, 128f, 64f, 128f), Ui.V(0.5f, 0.5f), 400f);
        _close = ChatArt.Art("close_hd.png", none, Ui.V(0.5f, 0.5f), 400f);
        _tab = ChatArt.Art("inv_tab.png", none, pivot, 100f);
        _cell = ChatArt.Art("inv_cell.png", none, pivot, 100f);
        _pick = ChatArt.Art("inv_pick.png", none, pivot, 100f);
        _loaded = Alive();
        _failed = !_loaded;
        return _loaded;
    }
}

[HarmonyPatch(typeof(PlayerCustomizationMenu), nameof(PlayerCustomizationMenu.Start))]
internal static class HeaderArtInventoryOpenPatch
{
    public static void Postfix(PlayerCustomizationMenu __instance) => HeaderArt.Open(__instance, "Header/CloseButton", true);
}

[HarmonyPatch(typeof(PlayerCustomizationMenu), nameof(PlayerCustomizationMenu.Update))]
internal static class HeaderArtInventoryTickPatch
{
    public static void Postfix(PlayerCustomizationMenu __instance) => HeaderArt.Tick(__instance, "Header/CloseButton", true);
}

[HarmonyPatch(typeof(StoreMenu), nameof(StoreMenu.Open))]
internal static class HeaderArtShopOpenPatch
{
    public static void Postfix(StoreMenu __instance) => HeaderArt.Open(__instance, "Header/Tabs/CloseButton", false);
}

[HarmonyPatch(typeof(StoreMenu), nameof(StoreMenu.Update))]
internal static class HeaderArtShopTickPatch
{
    public static void Postfix(StoreMenu __instance) => HeaderArt.Tick(__instance, "Header/Tabs/CloseButton", false);
}
