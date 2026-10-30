using System.Collections.Generic;
using UnityEngine;

namespace Nocturne.Patches;

internal static class LobbyArt
{
    private enum Role
    {
        Pane,
        Label,
        Field,
        Cta,
        Cube,
        Copy,
        Start,
        Stripe,
        Shine
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
        internal Sprite Sprite;
        internal Vector2 Size;
        internal Color Color;
        internal Material Material;
        internal Role Role;
        internal Face Face;
        internal bool Green;
        internal bool Half;
    }

    private static readonly List<Skin> Worn = new List<Skin>();
    private static readonly Color RedHot = Ui.C(0.95f, 0.24f, 0.30f, 1f);
    private static readonly Color GreenHot = Ui.C(0.24f, 0.80f, 0.45f, 1f);
    private static readonly Color CopyIconHot = Ui.C(0.08f, 0.09f, 0.11f, 1f);

    private static Sprite _pane;
    private static Sprite _label;
    private static Sprite _field;
    private static Sprite _cta;
    private static Sprite _ctaHot;
    private static Sprite _redCube;
    private static Sprite _greenCube;
    private static Sprite _cubeHot;
    private static Sprite _copy;
    private static Sprite _copyHot;
    private static Sprite _start;
    private static Sprite _startHot;
    private static Sprite _startHalf;
    private static Sprite _startHotHalf;
    private static Sprite _stripes;
    private static Material _plain;
    private static SpriteRenderer _copyIcon;
    private static SpriteRenderer _copyPlate;
    private static bool _loaded;
    private static bool _failed;
    private static int _mgr;
    private static float _nextScan;

    internal static void Apply(GameStartManager m, NocturnePalette p)
    {
        if (_failed || m == null || m.LobbyInfoPane == null)
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

        Worn.RemoveAll(s => s.Sr == null);
        int id = m.GetInstanceID();
        if ((id != _mgr || Worn.Count == 0) && Time.unscaledTime >= _nextScan)
        {
            _mgr = id;
            _nextScan = Time.unscaledTime + 2f;
            Scan(m);
        }

        bool glass = NocturneConfig.StartBtnGlass.Value;
        for (int i = 0; i < Worn.Count; i++)
        {
            Paint(Worn[i], p, glass);
        }
    }

    internal static void Tick()
    {
        if (_copyIcon == null || _copyPlate == null || NocturneConfig.Vanilla || !NocturneConfig.LobbyTheme.Value)
        {
            return;
        }

        Color want = _copyPlate.gameObject.activeInHierarchy ? CopyIconHot : NocturneStyle.Current.Accent;
        Color now = _copyIcon.color;
        if (now.r != want.r || now.g != want.g || now.b != want.b)
        {
            _copyIcon.color = Ui.C(want.r, want.g, want.b, now.a);
        }
    }

    internal static void Release()
    {
        _copyIcon = null;
        _copyPlate = null;
        for (int i = 0; i < Worn.Count; i++)
        {
            Skin s = Worn[i];
            if (s.Sr == null)
            {
                continue;
            }

            Swap(s.Sr, s.Sprite, s.Size);
            s.Sr.color = s.Color;
            if (s.Material != null)
            {
                s.Sr.sharedMaterial = s.Material;
            }
        }

        Worn.Clear();
        _mgr = 0;
    }

    internal static bool Owns(SpriteRenderer r)
    {
        if (!_loaded || r == null || r.sprite == null)
        {
            return false;
        }

        return Classify(r, out _, out _, out _) || IsArt(r.sprite);
    }

    private static bool IsArt(Sprite sp) =>
        sp == _pane || sp == _label || sp == _field || sp == _cta || sp == _ctaHot || sp == _redCube || sp == _greenCube
        || sp == _cubeHot || sp == _copy || sp == _copyHot || sp == _start || sp == _startHot
        || sp == _startHalf || sp == _startHotHalf || sp == _stripes;

    private static void Scan(GameStartManager m)
    {
        Transform pane = m.LobbyInfoPane.transform;
        Take(pane, pane);
        if (m.StartButton != null && m.StartButton.transform.parent != null)
        {
            Take(m.StartButton.transform.parent, null);
        }
    }

    private static void Take(Transform root, Transform pane)
    {
        foreach (SpriteRenderer sr in root.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (sr == null || !sr.enabled || sr.sprite == null || Held(sr))
            {
                continue;
            }

            if (pane != null && Hidden(sr.transform, pane))
            {
                continue;
            }

            if (!Classify(sr, out Role role, out Face face, out bool green))
            {
                continue;
            }

            if (role == Role.Pane && _plain == null)
            {
                _plain = sr.sharedMaterial;
            }

            var skin = new Skin { Sr = sr, Sprite = sr.sprite, Size = sr.size, Color = sr.color, Material = sr.sharedMaterial, Role = role, Face = face, Green = green, Half = role == Role.Start && sr.transform.localScale.x < 0.75f };
            if (_plain != null && skin.Material != _plain)
            {
                sr.sharedMaterial = _plain;
            }

            Worn.Add(skin);
            if (role == Role.Copy && face == Face.Hot && sr.transform.parent != null)
            {
                Transform icon = sr.transform.parent.Find("Icon");
                if (icon != null)
                {
                    _copyIcon = icon.GetComponent<SpriteRenderer>();
                    _copyPlate = sr;
                }
            }
        }
    }

    private static bool Held(SpriteRenderer sr)
    {
        for (int i = 0; i < Worn.Count; i++)
        {
            if (Worn[i].Sr == sr)
            {
                return true;
            }
        }

        return false;
    }

    private static bool Hidden(Transform t, Transform root)
    {
        for (Transform c = t; c != null && c != root; c = c.parent)
        {
            string n = c.name;
            if (n == "RulesPopOutWindow" || n == "CopiedGameCode")
            {
                return true;
            }
        }

        return false;
    }

    private static bool Classify(SpriteRenderer sr, out Role role, out Face face, out bool green)
    {
        green = false;
        face = Face.Idle;
        string state = sr.gameObject.name;
        switch (sr.sprite.name)
        {
            case "IMAGE-LobbyInfoPane":
                role = Role.Pane;
                return true;
            case "IMAGE-LabelCube":
                role = Role.Label;
                return true;
            case "IMAGE-FieldCube":
                role = Role.Field;
                return true;
            case "CTAInactive":
                role = Role.Cta;
                face = state == "Disabled" ? Face.Off : Face.Idle;
                return true;
            case "CTAActive":
            case "MinorActive":
                role = Role.Cta;
                face = Face.Hot;
                return true;
            case "IMAGE-PrivateCube":
                role = Role.Cube;
                return true;
            case "IMAGE-PrivateCubeHighlight":
                role = Role.Cube;
                face = Face.Hot;
                return true;
            case "IMAGE-PublicCube":
                role = Role.Cube;
                green = true;
                return true;
            case "IMAGE-PublicCubeHighlight":
                role = Role.Cube;
                green = true;
                face = Face.Hot;
                return true;
            case "StandardNoShadow":
                role = Role.Copy;
                return true;
            case "StandardActive":
                role = Role.Copy;
                face = Face.Hot;
                return true;
            case "IMAGE-StartActiveCube":
                role = Role.Start;
                face = state == "Highlight" ? Face.Hot : Face.Idle;
                return true;
            case "IMAGE-StartInactiveCube":
                role = Role.Start;
                face = state == "Disabled" ? Face.Off : Face.Idle;
                return true;
            case "BUTTON-CTAShine":
            case "BUTTON-CTAActiveShine":
                Transform holder = sr.transform.parent;
                string kind = holder != null ? holder.name : string.Empty;
                role = Role.Shine;
                face = Face.Hot;
                if (holder != null && holder.parent != null && holder.parent.name == "StartButton (Host)")
                {
                    if (kind == "Inactive" && sr.color.a > 0.5f)
                    {
                        role = Role.Stripe;
                    }

                    return true;
                }

                return kind == "Highlight" || kind == "Selected" || kind == "Active";
        }

        role = Role.Pane;
        return false;
    }

    private static void Paint(Skin s, NocturnePalette p, bool glass)
    {
        if (s.Role == Role.Shine || (s.Role == Role.Stripe && !glass))
        {
            Color seen = s.Sr.color;
            if (seen.a != 0f)
            {
                s.Sr.color = Ui.C(seen.r, seen.g, seen.b, 0f);
            }

            return;
        }

        Sprite art;
        Color tint = Ui.White;
        bool hot = s.Face == Face.Hot;
        switch (s.Role)
        {
            case Role.Pane:
                art = _pane;
                break;
            case Role.Label:
                art = _label;
                break;
            case Role.Field:
                art = _field;
                break;
            case Role.Cta:
                art = hot ? _ctaHot : _cta;
                if (hot)
                {
                    tint = p.Accent;
                }

                break;
            case Role.Cube:
                art = hot ? _cubeHot : s.Green ? _greenCube : _redCube;
                if (hot)
                {
                    tint = s.Green ? GreenHot : RedHot;
                }

                break;
            case Role.Copy:
                art = hot ? _copyHot : _copy;
                if (hot)
                {
                    tint = p.Accent;
                }

                break;
            case Role.Stripe:
                art = _stripes;
                tint = p.Accent;
                break;
            default:
                art = hot || !glass ? (s.Half ? _startHotHalf : _startHot) : s.Half ? _startHalf : _start;
                if (!glass)
                {
                    tint = Ui.Mix(p.ButtonHover, p.Accent, 0.45f);
                }
                else if (hot)
                {
                    tint = p.Accent;
                }

                break;
        }

        SpriteRenderer sr = s.Sr;
        if (sr.sprite != art)
        {
            Swap(sr, art, sr.size);
        }

        Color now = sr.color;
        if (now.r != tint.r || now.g != tint.g || now.b != tint.b || now.a != s.Color.a)
        {
            sr.color = Ui.C(tint.r, tint.g, tint.b, s.Color.a);
        }
    }

    private static void Swap(SpriteRenderer sr, Sprite sprite, Vector2 size)
    {
        sr.sprite = sprite;
        if (sr.drawMode != SpriteDrawMode.Simple)
        {
            sr.size = size;
        }
    }

    private static bool Alive() =>
        _pane != null && _label != null && _field != null && _cta != null && _ctaHot != null && _redCube != null && _greenCube != null
        && _cubeHot != null && _copy != null && _copyHot != null && _start != null && _startHot != null
        && _startHalf != null && _startHotHalf != null && _stripes != null;

    private static bool Load()
    {
        _pane = ChatArt.Art("lobby_pane.png");
        _label = ChatArt.Art("lobby_label.png", 35f);
        _field = ChatArt.Art("lobby_field.png", 45f);
        _cta = ChatArt.Art("lobby_cta.png", 23f);
        _ctaHot = ChatArt.Art("lobby_cta_hot.png", 23f);
        _redCube = ChatArt.Art("lobby_cube_red.png", 21f);
        _greenCube = ChatArt.Art("lobby_cube_green.png", 21f);
        _cubeHot = ChatArt.Art("lobby_cube_hot.png", 21f);
        _copy = ChatArt.Art("lobby_copy.png", 65f);
        _copyHot = ChatArt.Art("lobby_copy_hot.png", 65f);
        Vector4 edge = new Vector4(70f, 70f, 70f, 70f);
        Vector4 none = new Vector4(0f, 0f, 0f, 0f);
        Vector2 mid = Ui.V(0.5f, 0.5f);
        _start = ChatArt.Art("start_plate.png", edge, mid, 400f);
        _startHot = ChatArt.Art("start_plate_hot.png", edge, mid, 400f);
        _startHalf = ChatArt.Art("start_plate.png", edge, mid, 200f);
        _startHotHalf = ChatArt.Art("start_plate_hot.png", edge, mid, 200f);
        _stripes = ChatArt.Art("start_stripes.png", none, mid, 300f);
        _loaded = Alive();
        _failed = !_loaded;
        return _loaded;
    }
}
