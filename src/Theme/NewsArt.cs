using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace Nocturne.Patches;

internal static class NewsArt
{
    private enum Role
    {
        Frame,
        Close,
        Pane,
        Line,
        Edge,
        Panel,
        Track,
        Handle
    }

    private sealed class Skin
    {
        internal SpriteRenderer Sr;
        internal int Id;
        internal Sprite Sprite;
        internal Color Color;
        internal Role Role;
        internal Sprite Art;
        internal bool Dyed;
    }

    private static readonly List<Skin> Worn = new List<Skin>();
    private static readonly HashSet<int> Seen = new HashSet<int>();
    private static readonly HashSet<int> InkSeen = new HashSet<int>();
    private static readonly List<TMP_Text> Inked = new List<TMP_Text>();
    private static readonly List<Color> InkWas = new List<Color>();
    private static readonly List<Color> InkTo = new List<Color>();
    private static readonly Color Ink = Ui.C(0.93f, 0.95f, 0.98f, 1f);
    private static readonly Color Muted = Ui.C(0.62f, 0.65f, 0.71f, 1f);
    private static readonly Color PaneColor = Ui.C(0.588f, 0.627f, 0.725f, 0.1f);
    private static readonly Color LineColor = Ui.C(0.275f, 0.298f, 0.361f, 0.8f);
    private static readonly Color PanelRead = Ui.C(0.031f, 0.035f, 0.047f, 0.4f);
    private static readonly Color PanelPick = Ui.C(0.235f, 0.267f, 0.345f, 0.85f);
    private static readonly Color TrackColor = Ui.C(0.57f, 0.5f, 0.6f, 1f);

    private static GameObject _window;
    private static Sprite _frame;
    private static Sprite _close;
    private static bool _loaded;
    private static bool _failed;
    private static float _next;

    internal static void Open(AnnouncementPopUp pop)
    {
        _window = pop.gameObject;
        Apply(pop.transform);
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

        if (_window == null || !_window.activeInHierarchy)
        {
            return;
        }

        Enforce();
        if (Time.unscaledTime < _next)
        {
            return;
        }

        _next = Time.unscaledTime + 0.12f;
        Apply(_window.transform);
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

    internal static void Dress(AnnouncementPanel panel)
    {
        if (_failed || !NocturneConfig.ChatBtnArt.Value || (!_loaded && !Load()))
        {
            return;
        }

        Prune();
        Transform t = panel.transform;
        Scan(t);
        Recolor(t);
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
        InkTo.Clear();
        InkSeen.Clear();
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
    }

    private static void Scan(Transform root)
    {
        foreach (SpriteRenderer sr in root.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (sr == null || sr.sprite == null || !Seen.Add(sr.GetInstanceID()))
            {
                continue;
            }

            if (Classify(sr, out Role role, out Sprite art))
            {
                Worn.Add(new Skin { Sr = sr, Id = sr.GetInstanceID(), Sprite = sr.sprite, Color = sr.color, Role = role, Art = art });
            }
        }
    }

    private static bool Classify(SpriteRenderer sr, out Role role, out Sprite art)
    {
        art = null;
        role = Role.Edge;
        switch (sr.sprite.name)
        {
            case "IMAGE-BG":
                role = Role.Frame;
                art = _frame;
                return true;
            case "closeButton":
                role = Role.Close;
                art = _close;
                return true;
            case "IMAGE-Scrollbar":
                role = Role.Track;
                return true;
            case "IMAGE-ScrollbarToggle":
                role = Role.Handle;
                return true;
            case "blank":
                switch (sr.gameObject.name)
                {
                    case "WhiteColor":
                        role = Role.Pane;
                        return true;
                    case "Border":
                        role = Role.Line;
                        return true;
                    case "Right Border":
                    case "Bottom Border":
                        return true;
                }

                if (sr.GetComponent<AnnouncementPanel>() != null)
                {
                    role = Role.Panel;
                    return true;
                }

                return false;
        }

        return false;
    }

    private static void Paint(Skin s, NocturnePalette p)
    {
        SpriteRenderer sr = s.Sr;
        if (s.Art != null && sr.sprite != s.Art)
        {
            sr.sprite = s.Art;
        }

        switch (s.Role)
        {
            case Role.Panel:
                Color now = sr.color;
                if (!IsPanel(now))
                {
                    s.Color = now;
                    sr.color = now.r > 0.9f ? PanelPick : PanelRead;
                }

                return;
            case Role.Handle:
                Color grip = sr.color;
                if (grip.r != p.Accent.r || grip.g != p.Accent.g || grip.b != p.Accent.b || grip.a != 1f)
                {
                    sr.color = Ui.C(p.Accent.r, p.Accent.g, p.Accent.b, 1f);
                }

                return;
        }

        if (s.Dyed || s.Role == Role.Frame || s.Role == Role.Close)
        {
            return;
        }

        s.Dyed = true;
        switch (s.Role)
        {
            case Role.Pane:
                sr.color = PaneColor;
                break;
            case Role.Line:
                sr.color = LineColor;
                break;
            case Role.Edge:
                sr.color = Ui.C(LineColor.r, LineColor.g, LineColor.b, s.Color.a);
                break;
            case Role.Track:
                sr.color = TrackColor;
                break;
        }
    }

    private static void Recolor(Transform root)
    {
        foreach (TMP_Text t in root.GetComponentsInChildren<TMP_Text>(true))
        {
            if (t == null || !InkSeen.Add(t.GetInstanceID()) || !IsDark(t.color))
            {
                continue;
            }

            Inked.Add(t);
            InkWas.Add(t.color);
            InkTo.Add(t.gameObject.name == "Date" ? Muted : Ink);
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

    private static bool IsPanel(Color c) => Near(c, PanelRead) || Near(c, PanelPick);

    private static bool Near(Color a, Color b) => Ui.Abs(a.r - b.r) < 0.004f && Ui.Abs(a.g - b.g) < 0.004f && Ui.Abs(a.b - b.b) < 0.004f;

    private static bool Alive() => _frame != null && _close != null;

    private static bool Load()
    {
        _frame = ChatArt.Art("news_bg.png");
        _close = ChatArt.Art("close_hd.png", new Vector4(0f, 0f, 0f, 0f), Ui.V(0.5f, 0.5f), 400f);
        _loaded = Alive();
        _failed = !_loaded;
        return _loaded;
    }
}

[HarmonyPatch(typeof(AnnouncementPopUp), nameof(AnnouncementPopUp.OnEnable))]
internal static class NewsArtOpenPatch
{
    public static void Postfix(AnnouncementPopUp __instance) => NewsArt.Open(__instance);
}

[HarmonyPatch(typeof(AnnouncementPanel), nameof(AnnouncementPanel.Select))]
internal static class NewsArtSelectPatch
{
    public static void Postfix(AnnouncementPanel __instance) => NewsArt.Dress(__instance);
}

[HarmonyPatch(typeof(AnnouncementPanel), nameof(AnnouncementPanel.MarkAsRead))]
internal static class NewsArtReadPatch
{
    public static void Postfix(AnnouncementPanel __instance) => NewsArt.Dress(__instance);
}
