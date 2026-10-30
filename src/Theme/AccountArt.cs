using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace Nocturne.Patches;

internal static class AccountArt
{
    private enum Role
    {
        Card,
        Portrait,
        Plate,
        Action,
        Track
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

    private static readonly List<Skin> Worn = new List<Skin>();
    private static readonly HashSet<int> Seen = new HashSet<int>();
    private static readonly HashSet<int> InkSeen = new HashSet<int>();
    private static readonly HashSet<int> RollSeen = new HashSet<int>();
    private static readonly List<TMP_Text> Inked = new List<TMP_Text>();
    private static readonly List<Color> InkWas = new List<Color>();
    private static readonly List<Color> InkTo = new List<Color>();
    private static readonly List<ButtonRolloverHandler> Rolls = new List<ButtonRolloverHandler>();
    private static readonly List<Color> RollWas = new List<Color>();
    private static readonly Color Ink = Ui.C(0.93f, 0.95f, 0.98f, 1f);
    private static readonly Color Muted = Ui.C(0.62f, 0.65f, 0.71f, 1f);
    private static readonly Color TrackColor = Ui.C(0.27f, 0.29f, 0.34f, 1f);

    private static GameObject _window;
    private static Sprite _card;
    private static Sprite _portrait;
    private static Sprite _plate;
    private static Sprite _show;
    private static Sprite _hide;
    private static bool _loaded;
    private static bool _failed;
    private static float _next;

    internal static void Open()
    {
        GameObject window = Window();
        if (window != null)
        {
            Apply(window.transform);
        }
    }

    internal static void Tick()
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

        GameObject window = Window();
        if (window == null || !window.activeInHierarchy)
        {
            return;
        }

        Enforce();
        if (Time.unscaledTime < _next)
        {
            return;
        }

        _next = Time.unscaledTime + 0.12f;
        Apply(window.transform);
    }

    private static GameObject Window()
    {
        if (_window == null && AccountManager.InstanceExists)
        {
            _window = AccountManager.Instance.accountWindow;
        }

        return _window;
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
            Paint(Worn[i]);
        }

        Hover(root, Ui.Mix(p.Accent, Ui.White, 0.3f));
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

        for (int i = 0; i < Rolls.Count; i++)
        {
            if (Rolls[i] != null)
            {
                Rolls[i].OverColor = RollWas[i];
            }
        }

        Worn.Clear();
        Seen.Clear();
        Inked.Clear();
        InkWas.Clear();
        InkTo.Clear();
        InkSeen.Clear();
        Rolls.Clear();
        RollWas.Clear();
        RollSeen.Clear();
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
                InkTo.RemoveAt(i);
            }
        }

        for (int i = Rolls.Count - 1; i >= 0; i--)
        {
            if (Rolls[i] == null)
            {
                Rolls.RemoveAt(i);
                RollWas.RemoveAt(i);
            }
        }
    }

    private static void Scan(Transform root)
    {
        foreach (SpriteRenderer sr in root.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (sr == null || sr.sprite == null || !Seen.Add(sr.GetInstanceID()) || Popup(sr.transform))
            {
                continue;
            }

            if (Classify(sr, out Role role, out Sprite art))
            {
                Worn.Add(new Skin { Sr = sr, Id = sr.GetInstanceID(), Sprite = sr.sprite, Size = sr.size, Color = sr.color, Role = role, Art = art });
            }
        }
    }

    private static bool Classify(SpriteRenderer sr, out Role role, out Sprite art)
    {
        art = null;
        role = Role.Plate;
        switch (sr.sprite.name)
        {
            case "accounts_IDcard":
                role = Role.Card;
                art = _card;
                return true;
            case "cardPortraitBg":
                role = Role.Portrait;
                art = _portrait;
                return sr.gameObject.name != "IDBgMask";
            case "buttonClick":
                art = _plate;
                return true;
            case "BUTTON-ShowCode":
                role = Role.Action;
                art = _show;
                return true;
            case "BUTTON-HideCode":
                role = Role.Action;
                art = _hide;
                return true;
            case "UI_EndRound_XPBar":
                role = Role.Track;
                return true;
        }

        return false;
    }

    private static void Paint(Skin s)
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

        if (s.Dyed)
        {
            return;
        }

        s.Dyed = true;
        Color tint = s.Role == Role.Track ? TrackColor : Ui.White;
        sr.color = Ui.C(tint.r, tint.g, tint.b, s.Color.a);
    }

    private static void Hover(Transform root, Color over)
    {
        foreach (ButtonRolloverHandler h in root.GetComponentsInChildren<ButtonRolloverHandler>(true))
        {
            if (h == null || !RollSeen.Add(h.GetInstanceID()) || Popup(h.transform))
            {
                continue;
            }

            Rolls.Add(h);
            RollWas.Add(h.OverColor);
        }

        for (int i = 0; i < Rolls.Count; i++)
        {
            Color now = Rolls[i].OverColor;
            if (now.r != over.r || now.g != over.g || now.b != over.b)
            {
                Rolls[i].OverColor = over;
            }
        }
    }

    private static void Recolor(Transform root)
    {
        foreach (TMP_Text t in root.GetComponentsInChildren<TMP_Text>(true))
        {
            if (t == null || !InkSeen.Add(t.GetInstanceID()) || !IsDark(t.color) || Popup(t.transform))
            {
                continue;
            }

            Inked.Add(t);
            InkWas.Add(t.color);
            InkTo.Add(t.gameObject.name.Contains("Title") ? Muted : Ink);
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
            if (IsDark(c))
            {
                Color to = InkTo[i];
                t.color = Ui.C(to.r, to.g, to.b, c.a);
            }
        }
    }

    private static bool IsDark(Color c) => c.r * 0.299f + c.g * 0.587f + c.b * 0.114f < 0.5f && !(c.r > 0.8f && c.g < 0.55f && c.b < 0.55f);

    private static bool IsInk(Color c) =>
        (Ui.Abs(c.r - Ink.r) < 0.01f && Ui.Abs(c.g - Ink.g) < 0.01f && Ui.Abs(c.b - Ink.b) < 0.01f)
        || (Ui.Abs(c.r - Muted.r) < 0.01f && Ui.Abs(c.g - Muted.g) < 0.01f && Ui.Abs(c.b - Muted.b) < 0.01f);

    private static bool Popup(Transform t)
    {
        for (; t != null; t = t.parent)
        {
            if (t.GetComponent<EditName>() != null || t.GetComponent<InfoTextBox>() != null)
            {
                return true;
            }
        }

        return false;
    }

    private static bool Alive() =>
        _card != null && _portrait != null && _plate != null && _show != null && _hide != null;

    private static bool Load()
    {
        _card = ChatArt.Art("acc_card.png");
        _portrait = ChatArt.Art("acc_portrait.png", new Vector4(80f, 80f, 80f, 80f), Ui.V(0.5f, 0.5f), 400f);
        _plate = ChatArt.Art("acc_btn.png", 12f);
        _show = ChatArt.Art("fr_show.png");
        _hide = ChatArt.Art("fr_hide.png");
        _loaded = Alive();
        _failed = !_loaded;
        return _loaded;
    }
}

[HarmonyPatch(typeof(AccountTab), nameof(AccountTab.Open))]
internal static class AccountArtOpenPatch
{
    public static void Postfix() => AccountArt.Open();
}
