using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace Nocturne.Patches;

internal static class SettingsArt
{
    private enum Role
    {
        Panel,
        Popup,
        Card,
        Exit,
        Check,
        Tab,
        Item,
        Cta,
        Shine,
        Track,
        Handle
    }

    private sealed class Skin
    {
        internal SpriteRenderer Sr;
        internal int Id;
        internal Sprite Sprite;
        internal Vector2 Size;
        internal Color Color;
        internal Role Role;
        internal bool Hot;
    }

    private static readonly List<Skin> Worn = new List<Skin>();
    private static readonly HashSet<int> Seen = new HashSet<int>();
    private static readonly HashSet<int> InkSeen = new HashSet<int>();
    private static readonly HashSet<int> Plated = new HashSet<int>();
    private static readonly List<TMP_Text> Inked = new List<TMP_Text>();
    private static readonly List<Color> InkWas = new List<Color>();
    private static readonly Color Ink = Ui.C(0.93f, 0.95f, 0.98f, 1f);
    private static readonly Color TrackColor = Ui.C(0.24f, 0.26f, 0.31f, 1f);

    private static GameSettingMenu _game;
    private static LobbyViewSettingsPane _view;
    private static Sprite _panel;
    private static Sprite _popup;
    private static Sprite _card;
    private static Sprite _cardHot;
    private static Sprite _exit;
    private static Sprite _check;
    private static Sprite _tab;
    private static Sprite _tabHot;
    private static Sprite _item;
    private static Sprite _itemHot;
    private static Sprite _cta;
    private static Sprite _ctaHot;
    private static bool _loaded;
    private static bool _failed;
    private static bool _open;
    private static float _next;

    internal static void Open(GameSettingMenu menu)
    {
        _game = menu;
        _open = true;
        Apply(menu.transform);
    }

    internal static void Open(LobbyViewSettingsPane pane)
    {
        _view = pane;
        _open = true;
        Apply(pane.transform);
    }

    internal static void Tick()
    {
        if (_failed)
        {
            return;
        }

        if (_open)
        {
            Enforce();
        }

        if (Time.unscaledTime < _next)
        {
            return;
        }

        _next = Time.unscaledTime + 0.25f;
        if (!NocturneConfig.ChatBtnArt.Value)
        {
            _open = false;
            Release();
            return;
        }

        if (_game == null)
        {
            _game = GameSettingMenu.Instance;
        }

        bool game = _game != null && _game.gameObject.activeInHierarchy;
        bool view = _view != null && _view.gameObject.activeInHierarchy;
        _open = game || view;
        if (game)
        {
            Apply(_game.transform);
        }

        if (view)
        {
            Apply(_view.transform);
        }

        if (!_open)
        {
            Prune();
            if (Worn.Count == 0)
            {
                Seen.Clear();
                InkSeen.Clear();
                Plated.Clear();
            }
        }
    }

    private static void Apply(Transform root)
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
        Scan(root);
        Recolor(root);
        NocturnePalette p = NocturneStyle.Current;
        for (int i = 0; i < Worn.Count; i++)
        {
            Paint(Worn[i], p);
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

        for (int i = 0; i < Inked.Count; i++)
        {
            if (Inked[i] != null && IsInk(Inked[i].color))
            {
                Inked[i].color = InkWas[i];
            }
        }

        Worn.Clear();
        Seen.Clear();
        Inked.Clear();
        InkWas.Clear();
        InkSeen.Clear();
        Plated.Clear();
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

        for (int i = Inked.Count - 1; i >= 0; i--)
        {
            if (Inked[i] == null)
            {
                Inked.RemoveAt(i);
                InkWas.RemoveAt(i);
            }
        }
    }

    private static void Scan(Transform root)
    {
        foreach (SpriteRenderer sr in root.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (sr == null || sr.sprite == null || !Seen.Add(sr.GetInstanceID()))
            {
                continue;
            }

            if (!Classify(sr, out Role role, out bool hot) || Under<RolesSettingsMenu>(sr.transform))
            {
                continue;
            }

            Worn.Add(new Skin { Sr = sr, Id = sr.GetInstanceID(), Sprite = sr.sprite, Size = sr.size, Color = sr.color, Role = role, Hot = hot });
            if (role == Role.Item || role == Role.Cta)
            {
                int button = ButtonId(sr.transform);
                if (button != 0)
                {
                    Plated.Add(button);
                }
            }
        }
    }

    private static bool Classify(SpriteRenderer sr, out Role role, out bool hot)
    {
        hot = false;
        role = Role.Panel;
        switch (sr.sprite.name)
        {
            case "1x-IMAGE-SettingsPanel":
                return true;
            case "IMAGE-PanelBG":
                role = Role.Popup;
                return true;
            case "IMAGE-FrameInactive":
                role = Role.Card;
                return true;
            case "IMAGE-FrameActive":
                role = Role.Card;
                hot = true;
                return true;
            case "BUTTON-ExitHover":
                role = Role.Exit;
                hot = true;
                return true;
            case "IMAGE-CheckMark":
                role = Role.Check;
                hot = true;
                return sr.transform.parent != null && sr.transform.parent.name == "SelectionBackground";
            case "BUTTON-QCSidebarHide-Standard":
                role = Role.Tab;
                return true;
            case "BUTTON-QCSidebarHide-Active":
                role = Role.Tab;
                hot = true;
                return true;
            case "StandardInactive":
                role = Role.Item;
                return true;
            case "StandardActive":
                role = Role.Item;
                hot = true;
                return true;
            case "CTAInactive":
                role = Role.Cta;
                return true;
            case "CTAActive":
                role = Role.Cta;
                hot = true;
                return true;
            case "BUTTON-CTAShine":
                role = Role.Shine;
                return true;
            case "BUTTON-CTAActiveShine":
                role = Role.Shine;
                hot = true;
                return true;
            case "blank":
                string n = sr.gameObject.name;
                if (n == "UI_ScrollbarTrack")
                {
                    role = Role.Track;
                    return true;
                }

                role = Role.Handle;
                return n == "UI_Scrollbar";
        }

        return false;
    }

    private static void Paint(Skin s, NocturnePalette p)
    {
        SpriteRenderer sr = s.Sr;
        Color tint = s.Hot ? p.Accent : Ui.White;
        Sprite art = null;
        float alpha = s.Color.a;
        switch (s.Role)
        {
            case Role.Panel:
                art = _panel;
                break;
            case Role.Popup:
                art = _popup;
                break;
            case Role.Card:
                art = s.Hot ? _cardHot : _card;
                break;
            case Role.Exit:
                art = _exit;
                break;
            case Role.Check:
                art = _check;
                break;
            case Role.Tab:
                art = s.Hot ? _tabHot : _tab;
                break;
            case Role.Item:
                art = s.Hot ? _itemHot : _item;
                break;
            case Role.Cta:
                art = s.Hot ? _ctaHot : _cta;
                break;
            case Role.Shine:
                tint = p.Accent;
                if (s.Hot)
                {
                    alpha = 0f;
                }

                break;
            case Role.Track:
                tint = TrackColor;
                alpha = 1f;
                break;
            case Role.Handle:
                tint = p.Accent;
                alpha = 1f;
                break;
        }

        if (art != null && sr.sprite != art)
        {
            Vector2 size = sr.size;
            sr.sprite = art;
            if (sr.drawMode != SpriteDrawMode.Simple)
            {
                sr.size = size;
            }
        }

        Color now = sr.color;
        if (now.r != tint.r || now.g != tint.g || now.b != tint.b || now.a != alpha)
        {
            sr.color = Ui.C(tint.r, tint.g, tint.b, alpha);
        }
    }

    private static void Recolor(Transform root)
    {
        foreach (TMP_Text t in root.GetComponentsInChildren<TMP_Text>(true))
        {
            if (t == null || !InkSeen.Add(t.GetInstanceID()))
            {
                continue;
            }

            int button = ButtonId(t.transform);
            if (button == 0 || !Plated.Contains(button) || Under<RolesSettingsMenu>(t.transform))
            {
                continue;
            }

            Inked.Add(t);
            InkWas.Add(t.color);
        }

        Enforce();
    }

    private static void Enforce()
    {
        for (int i = 0; i < Inked.Count; i++)
        {
            TMP_Text t = Inked[i];
            if (t == null)
            {
                continue;
            }

            Color c = t.color;
            if (IsTeal(c))
            {
                InkWas[i] = c;
                t.color = Ui.C(Ink.r, Ink.g, Ink.b, c.a);
            }
        }
    }

    private static bool IsTeal(Color c) => c.g >= c.r * 1.15f && c.b >= c.r * 1.05f;

    private static bool IsInk(Color c) => Ui.Abs(c.r - Ink.r) < 0.01f && Ui.Abs(c.g - Ink.g) < 0.01f && Ui.Abs(c.b - Ink.b) < 0.01f;

    private static int ButtonId(Transform t)
    {
        for (; t != null; t = t.parent)
        {
            PassiveButton button = t.GetComponent<PassiveButton>();
            if (button != null)
            {
                return button.GetInstanceID();
            }
        }

        return 0;
    }

    private static bool Under<T>(Transform t) where T : Component
    {
        for (; t != null; t = t.parent)
        {
            if (t.GetComponent<T>() != null)
            {
                return true;
            }
        }

        return false;
    }

    private static bool Alive() =>
        _panel != null && _popup != null && _card != null && _cardHot != null && _exit != null && _check != null
        && _tab != null && _tabHot != null && _item != null && _itemHot != null && _cta != null && _ctaHot != null;

    private static bool Load()
    {
        _panel = ChatArt.Art("win_panel.png");
        _popup = ChatArt.Art("win_view.png");
        _card = ChatArt.Art("win_card.png");
        _cardHot = ChatArt.Art("win_card_hot.png");
        _exit = ChatArt.Art("win_exit_hot.png");
        _check = ChatArt.Art("win_check.png");
        Vector2 tabPivot = Ui.V(95f / 185f, 217f / 429f);
        _tab = ChatArt.Art("qc_tab.png", new Vector4(0f, 0f, 0f, 0f), tabPivot, 100f);
        _tabHot = ChatArt.Art("qc_tab_hot.png", new Vector4(0f, 0f, 0f, 0f), tabPivot, 100f);
        _item = ChatArt.Art("qc_item.png", 16f);
        _itemHot = ChatArt.Art("qc_item_hot.png", 16f);
        _cta = ChatArt.Art("lobby_cta.png", 23f);
        _ctaHot = ChatArt.Art("lobby_cta_hot.png", 23f);
        _loaded = Alive();
        _failed = !_loaded;
        return _loaded;
    }
}

[HarmonyPatch(typeof(GameSettingMenu), "OnEnable")]
internal static class SettingsEditorOpenPatch
{
    public static void Postfix(GameSettingMenu __instance) => SettingsArt.Open(__instance);
}

[HarmonyPatch(typeof(GameSettingMenu), "Update")]
internal static class SettingsEditorTickPatch
{
    public static void Postfix() => SettingsArt.Tick();
}

[HarmonyPatch(typeof(LobbyViewSettingsPane), "OnEnable")]
internal static class SettingsViewOpenPatch
{
    public static void Postfix(LobbyViewSettingsPane __instance) => SettingsArt.Open(__instance);
}

[HarmonyPatch(typeof(LobbyViewSettingsPane), "Update")]
internal static class SettingsViewTickPatch
{
    public static void Postfix() => SettingsArt.Tick();
}
