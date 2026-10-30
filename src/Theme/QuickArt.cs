using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Nocturne.Patches;

internal static class QuickArt
{
    private enum Role
    {
        Frame,
        Tab,
        Item,
        Small,
        Cta,
        Shine,
        Line,
        Teal
    }

    private enum Face
    {
        Idle,
        Hot,
        Off
    }

    private sealed class Skin
    {
        internal SpriteRenderer Sr;
        internal int Id;
        internal Sprite Sprite;
        internal Vector2 Size;
        internal Color Color;
        internal Role Role;
        internal Face Face;
    }

    private static readonly List<Skin> Worn = new List<Skin>();
    private static readonly HashSet<int> Seen = new HashSet<int>();
    private static readonly Dictionary<int, Color> InkWas = new Dictionary<int, Color>();
    private static readonly Dictionary<int, TMP_Text> InkRef = new Dictionary<int, TMP_Text>();
    private static readonly Color Ink = Ui.C(0.93f, 0.95f, 0.98f, 1f);

    private static Sprite _frame;
    private static Sprite _tab;
    private static Sprite _tabHot;
    private static Sprite _item;
    private static Sprite _itemHot;
    private static Sprite _small;
    private static Sprite _smallHot;
    private static Sprite _cta;
    private static Sprite _ctaHot;
    private static bool _loaded;
    private static bool _failed;
    private static float _next;

    internal static void Tick(ChatController chat)
    {
        if (_failed || chat == null || Time.unscaledTime < _next)
        {
            return;
        }

        _next = Time.unscaledTime + 0.1f;
        var root = chat.quickChatMenu;
        if (root == null)
        {
            return;
        }

        if (!NocturneConfig.ChatBtnArt.Value)
        {
            Release();
            return;
        }

        if (!root.gameObject.activeInHierarchy)
        {
            return;
        }

        _next = Time.unscaledTime + 0.3f;
        if (_loaded && !Alive())
        {
            Release();
            _loaded = false;
        }

        if (!_loaded && !Load())
        {
            return;
        }

        NocturnePalette p = NocturneStyle.Current;
        Transform menu = root.transform;
        for (int i = Worn.Count - 1; i >= 0; i--)
        {
            if (Worn[i].Sr == null)
            {
                Seen.Remove(Worn[i].Id);
                Worn.RemoveAt(i);
            }
        }

        Scan(menu);
        for (int i = 0; i < Worn.Count; i++)
        {
            Paint(Worn[i], p);
        }

        Recolor(menu);
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

        foreach (KeyValuePair<int, TMP_Text> pair in InkRef)
        {
            if (pair.Value != null && InkWas.TryGetValue(pair.Key, out Color c))
            {
                pair.Value.color = c;
            }
        }

        Worn.Clear();
        Seen.Clear();
        InkWas.Clear();
        InkRef.Clear();
    }

    private static void Scan(Transform menu)
    {
        foreach (SpriteRenderer sr in menu.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (sr == null || sr.sprite == null || !Seen.Add(sr.GetInstanceID()))
            {
                continue;
            }

            if (!Classify(sr, out Role role, out Face face))
            {
                continue;
            }

            Worn.Add(new Skin { Sr = sr, Id = sr.GetInstanceID(), Sprite = sr.sprite, Size = sr.size, Color = sr.color, Role = role, Face = face });
        }
    }

    private static bool Classify(SpriteRenderer sr, out Role role, out Face face)
    {
        face = Face.Idle;
        role = Role.Frame;
        string n = sr.sprite.name;
        switch (n)
        {
            case "IMAGE-QCSidebarFrame":
                return true;
            case "BUTTON-QCSidebarHide-Standard":
                role = Role.Tab;
                return true;
            case "BUTTON-QCSidebarHide-Active":
                role = Role.Tab;
                face = Face.Hot;
                return true;
            case "StandardInactive":
                role = Role.Item;
                return true;
            case "StandardActive":
                role = Role.Item;
                face = Face.Hot;
                return true;
            case "MinorInactive":
                if (sr.size.x > 1f)
                {
                    role = Role.Item;
                    face = Face.Off;
                }
                else
                {
                    role = Role.Small;
                }

                return true;
            case "MinorActive":
                role = Role.Small;
                face = Face.Hot;
                return true;
            case "CTAInactive":
                role = Role.Cta;
                return true;
            case "CTAActive":
                role = Role.Cta;
                face = Face.Hot;
                return true;
            case "BUTTON-CTAShine":
                role = Role.Shine;
                return true;
            case "BUTTON-CTAActiveShine":
                role = Role.Shine;
                face = Face.Hot;
                return true;
            case "blank":
                role = Role.Line;
                return sr.gameObject.name == "Border";
            case "smallButton":
            case "smallButtonBorder":
                role = Role.Teal;
                face = Face.Hot;
                return IsTeal(sr.color);
        }

        return false;
    }

    private static bool IsTeal(Color c) => c.g >= c.r * 1.15f && c.b >= c.r * 1.05f;

    private static void Paint(Skin s, NocturnePalette p)
    {
        SpriteRenderer sr = s.Sr;
        bool hot = s.Face == Face.Hot;
        Color tint = hot ? p.Accent : Ui.White;
        Sprite art = null;
        float alpha = s.Color.a;
        switch (s.Role)
        {
            case Role.Frame:
                art = _frame;
                break;
            case Role.Tab:
                art = hot ? _tabHot : _tab;
                break;
            case Role.Item:
                art = hot ? _itemHot : _item;
                break;
            case Role.Small:
                art = hot ? _smallHot : _small;
                break;
            case Role.Cta:
                art = hot ? _ctaHot : _cta;
                break;
            case Role.Shine:
                tint = p.Accent;
                if (hot)
                {
                    alpha = 0f;
                }

                break;
            case Role.Line:
                tint = Ui.C(0.20f, 0.22f, 0.27f, 1f);
                break;
        }

        if (s.Face == Face.Off)
        {
            alpha *= 0.45f;
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

    private static void Recolor(Transform menu)
    {
        foreach (TMP_Text t in menu.GetComponentsInChildren<TMP_Text>(true))
        {
            if (t == null)
            {
                continue;
            }

            Color c = t.color;
            if (!IsTeal(c))
            {
                continue;
            }

            int id = t.GetInstanceID();
            if (!InkWas.ContainsKey(id))
            {
                InkWas[id] = c;
                InkRef[id] = t;
            }

            t.color = Ui.C(Ink.r, Ink.g, Ink.b, c.a);
        }
    }

    private static bool Alive() =>
        _frame != null && _tab != null && _tabHot != null && _item != null && _itemHot != null && _small != null && _smallHot != null && _cta != null && _ctaHot != null;

    private static bool Load()
    {
        _frame = ChatArt.Art("qc_frame.png", new Vector4(28f, 53f, 38f, 29f), Ui.V(0f, 1200f / 1177f), 300f);
        Vector2 tabPivot = Ui.V(95f / 185f, 217f / 429f);
        _tab = ChatArt.Art("qc_tab.png", new Vector4(0f, 0f, 0f, 0f), tabPivot, 100f);
        _tabHot = ChatArt.Art("qc_tab_hot.png", new Vector4(0f, 0f, 0f, 0f), tabPivot, 100f);
        _item = ChatArt.Art("qc_item.png", 16f);
        _itemHot = ChatArt.Art("qc_item_hot.png", 16f);
        _small = ChatArt.Art("qc_small.png", 12f);
        _smallHot = ChatArt.Art("qc_small_hot.png", 12f);
        _cta = ChatArt.Art("lobby_cta.png", 23f);
        _ctaHot = ChatArt.Art("lobby_cta_hot.png", 23f);
        _loaded = Alive();
        _failed = !_loaded;
        return _loaded;
    }
}
