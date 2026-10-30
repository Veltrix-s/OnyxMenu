using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace Nocturne.Patches;

internal static class FriendsArt
{
    private enum Role
    {
        Frame,
        Glass,
        Close,
        Row,
        Plate,
        Hot,
        Pick,
        Outline,
        Field,
        Action,
        Tab,
        Head,
        Haze,
        Line,
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
        internal Material Material;
        internal Role Role;
        internal Sprite Art;
    }

    private static readonly string[][] ActionFiles =
    {
        new[] { "BUTTON-AddFriendTap", "fr_add.png" },
        new[] { "BUTTON-BlockPlayerTap", "fr_block.png" },
        new[] { "BUTTON-InvFriendTap", "fr_invite.png" },
        new[] { "BUTTON-RemoveFriendTap", "fr_remove.png" },
        new[] { "BUTTON-ReportPlayerTapped", "fr_report.png" },
        new[] { "BUTTON-Tick", "fr_tick.png" },
        new[] { "BUTTON-TickTap", "fr_tick_hot.png" },
        new[] { "BUTTON-RejectInvite", "fr_reject.png" },
        new[] { "BUTTON-RejectInviteActive", "fr_reject_hot.png" },
        new[] { "BUTTON-ShowCode", "fr_show.png" },
        new[] { "BUTTON-HideCode", "fr_hide.png" }
    };

    private static readonly Dictionary<string, Sprite> Actions = new Dictionary<string, Sprite>();
    private static readonly List<Skin> Worn = new List<Skin>();
    private static readonly List<Skin> Held = new List<Skin>();
    private static readonly HashSet<int> Seen = new HashSet<int>();
    private static readonly HashSet<int> InkSeen = new HashSet<int>();
    private static readonly List<TMP_Text> Inked = new List<TMP_Text>();
    private static readonly List<Color> InkWas = new List<Color>();
    private static readonly List<Color> InkTo = new List<Color>();
    private static readonly Color Ink = Ui.C(0.93f, 0.95f, 0.98f, 1f);
    private static readonly Color Muted = Ui.C(0.70f, 0.73f, 0.79f, 1f);
    private static readonly Color Picked = Ui.C(0.29f, 0.31f, 0.37f, 1f);
    private static readonly Color TabColor = Ui.C(0.22f, 0.24f, 0.30f, 1f);
    private static readonly Color HeadColor = Ui.C(0.02f, 0.025f, 0.035f, 1f);
    private static readonly Color HazeColor = Ui.C(0.23f, 0.26f, 0.33f, 1f);
    private static readonly Color LineColor = Ui.C(0.28f, 0.31f, 0.38f, 1f);
    internal static readonly Color TrackColor = Ui.C(0.24f, 0.26f, 0.31f, 1f);
    private static readonly Color RimColor = Ui.C(0.035f, 0.04f, 0.055f, 1f);

    private static FriendsListUI _ui;
    private static Color _noWas;
    private static Color _newWas;
    private static bool _kept;
    private static Sprite _frame;
    private static Sprite _glass;
    private static Sprite _close;
    private static Sprite _row;
    private static Sprite _field;
    private static Sprite _plate;
    private static Sprite _hot;
    private static Material _flat;
    private static bool _loaded;
    private static bool _failed;
    private static float _next;

    internal static void Open(FriendsListUI ui)
    {
        Calm(ui);
        Apply(ui.transform);
    }

    internal static void Tick(FriendsListUI ui)
    {
        if (_failed)
        {
            return;
        }

        Enforce();
        if (Time.unscaledTime < _next)
        {
            return;
        }

        _next = Time.unscaledTime + 0.12f;
        if (!NocturneConfig.ChatBtnArt.Value)
        {
            Release();
            return;
        }

        Calm(ui);
        Apply(ui.transform);
    }

    private static void Calm(FriendsListUI ui)
    {
        if (!NocturneConfig.ChatBtnArt.Value)
        {
            return;
        }

        _ui = ui;
        if (!_kept)
        {
            _kept = true;
            _noWas = ui.NoRequestsColor;
            _newWas = ui.NewRequestColor;
        }

        ui.NoRequestsColor = Ui.White;
        ui.NewRequestColor = Ui.White;
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
        if (FriendsListManager.InstanceExists)
        {
            FriendListNotification note = FriendsListManager.Instance.FriendListNotification;
            if (note != null)
            {
                Scan(note.transform);
                Recolor(note.transform);
            }
        }

        NocturnePalette p = NocturneStyle.Current;
        for (int i = 0; i < Worn.Count; i++)
        {
            Paint(Worn[i], p);
        }
    }

    internal static void Dress(FriendsListBar row)
    {
        if (!_loaded || !NocturneConfig.ChatBtnArt.Value)
        {
            return;
        }

        TextMeshPro name = row.SenderName;
        if (name != null)
        {
            string text = name.text;
            if (text != null && text.Contains("#666666"))
            {
                name.text = text.Replace("#666666", "#A3A9B6");
            }
        }

        Transform bar = row.transform;
        int from = Worn.Count;
        Scan(bar);
        Recolor(bar);
        NocturnePalette p = NocturneStyle.Current;
        for (int i = from; i < Worn.Count; i++)
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

            s.Sr.sharedMaterial = s.Material;
            s.Sr.color = s.Color;
        }

        for (int i = 0; i < Inked.Count; i++)
        {
            if (Inked[i] != null && IsInk(Inked[i].color))
            {
                Inked[i].color = InkWas[i];
            }
        }

        if (_kept && _ui != null)
        {
            _ui.NoRequestsColor = _noWas;
            _ui.NewRequestColor = _newWas;
        }

        _kept = false;
        Worn.Clear();
        Held.Clear();
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

            Material mat = sr.sharedMaterial;
            if (_flat == null && mat != null && mat.name == "Sprites-Default")
            {
                _flat = mat;
            }

            if (Classify(sr, out Role role, out Sprite art))
            {
                var skin = new Skin { Sr = sr, Id = sr.GetInstanceID(), Sprite = sr.sprite, Size = sr.size, Color = sr.color, Material = mat, Role = role, Art = art };
                Worn.Add(skin);
                if (role == Role.Plate)
                {
                    Held.Add(skin);
                }
            }
        }
    }

    private static bool Classify(SpriteRenderer sr, out Role role, out Sprite art)
    {
        art = null;
        role = Role.Plate;
        string n = sr.gameObject.name;
        switch (sr.sprite.name)
        {
            case "UI_Cosmicube_NodeSquare":
                role = Role.Frame;
                art = _frame;
                return true;
            case "UI_Phone_Glass":
                role = Role.Glass;
                art = _glass;
                return true;
            case "closeButton":
                role = Role.Close;
                art = _close;
                return true;
            case "votePlayerBase":
                role = Role.Row;
                art = _row;
                return true;
            case "buttonOutline":
                role = Role.Outline;
                return true;
            case "buttonClick":
                if (n == "Highlight")
                {
                    role = Role.Hot;
                    art = _hot;
                    return true;
                }

                if (n == "Back")
                {
                    role = Role.Pick;
                    art = _hot;
                    return true;
                }

                art = _plate;
                return n == "Fill" || n == "FIll" || n == "RequestCounterButton" || n == "AddFriendButton";
            case "chatBar":
                if (n == "AddFriend")
                {
                    role = Role.Field;
                    art = _field;
                    return true;
                }

                if (n == "Highlight")
                {
                    role = Role.Hot;
                    art = _hot;
                    return true;
                }

                art = _plate;
                return n == "Button";
            case "IMAGE-MenuTab":
                role = Role.Tab;
                return true;
            case "IMAGE-HeaderGradient":
                role = n == "Header Grade" ? Role.Head : Role.Haze;
                return n == "Header Grade" || n == "White Grade";
            case "IMAGE-HeaderDivider":
                role = Role.Line;
                return true;
            case "UI_ScrollbarTrack":
                role = Role.Track;
                return true;
            case "UI_ScrollbarPip":
                role = Role.Handle;
                return true;
        }

        role = Role.Action;
        return n != "ControllerHighlight" && Actions.TryGetValue(sr.sprite.name, out art);
    }

    private static void Paint(Skin s, NocturnePalette p)
    {
        SpriteRenderer sr = s.Sr;
        Color tint = Ui.White;
        float alpha = s.Color.a;
        switch (s.Role)
        {
            case Role.Hot:
                tint = p.Accent;
                break;
            case Role.Pick:
                tint = Picked;
                break;
            case Role.Outline:
                tint = RimColor;
                break;
            case Role.Row:
                if (sr.gameObject.name == "Inactive")
                {
                    tint = s.Color;
                }

                break;
            case Role.Tab:
                tint = TabColor;
                break;
            case Role.Head:
                tint = HeadColor;
                alpha = 0.95f;
                break;
            case Role.Haze:
                tint = HazeColor;
                alpha = 0.3f;
                break;
            case Role.Line:
                tint = LineColor;
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

        if (s.Art != null && sr.sprite != s.Art)
        {
            Vector2 size = sr.size;
            sr.sprite = s.Art;
            if (sr.drawMode != SpriteDrawMode.Simple)
            {
                sr.size = size;
            }
        }

        bool flat = s.Role == Role.Plate || s.Role == Role.Hot || s.Role == Role.Pick || s.Role == Role.Field;
        if (flat && _flat != null && sr.sharedMaterial != _flat)
        {
            sr.sharedMaterial = _flat;
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
            if (t == null || !InkSeen.Add(t.GetInstanceID()) || !IsDark(t.color))
            {
                continue;
            }

            Inked.Add(t);
            InkWas.Add(t.color);
            InkTo.Add(t.gameObject.name == "FriendCodeText" ? Muted : Ink);
        }

        Enforce();
    }

    private static void Enforce()
    {
        for (int i = 0; i < Held.Count; i++)
        {
            SpriteRenderer sr = Held[i].Sr;
            if (sr == null)
            {
                continue;
            }

            Color c = sr.color;
            if (c.r != 1f || c.g != 1f || c.b != 1f)
            {
                sr.color = Ui.C(1f, 1f, 1f, c.a);
            }
        }

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

    private static bool Alive()
    {
        bool ok = _frame != null && _glass != null && _close != null && _row != null && _field != null && _plate != null && _hot != null;
        foreach (KeyValuePair<string, Sprite> kv in Actions)
        {
            ok &= kv.Value != null;
        }

        return ok;
    }

    private static bool Load()
    {
        _frame = ChatArt.Art("fr_frame.png", 64f);
        _glass = ChatArt.Art("fr_glass.png");
        _close = ChatArt.Art("fr_close.png");
        _row = ChatArt.Art("fr_row.png", 10f);
        _field = ChatArt.Art("fr_field.png", 16f);
        _plate = ChatArt.Art("fr_plate.png", 12f);
        _hot = ChatArt.Art("fr_plate_hot.png", 12f);
        Actions.Clear();
        for (int i = 0; i < ActionFiles.Length; i++)
        {
            Actions[ActionFiles[i][0]] = ChatArt.Art(ActionFiles[i][1]);
        }

        _loaded = Alive();
        _failed = !_loaded;
        return _loaded;
    }
}

[HarmonyPatch(typeof(FriendsListUI), nameof(FriendsListUI.Open))]
internal static class FriendsArtOpenPatch
{
    public static void Postfix(FriendsListUI __instance) => FriendsArt.Open(__instance);
}

[HarmonyPatch(typeof(FriendsListUI), nameof(FriendsListUI.Update))]
internal static class FriendsArtTickPatch
{
    public static void Postfix(FriendsListUI __instance) => FriendsArt.Tick(__instance);
}

[HarmonyPatch(typeof(FriendsListBar), "Start")]
internal static class FriendsArtBarPatch
{
    public static void Postfix(FriendsListBar __instance) => FriendsArt.Dress(__instance);
}
