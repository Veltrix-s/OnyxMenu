using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace Nocturne.Patches;

internal static class ChatArt
{
    private enum Face
    {
        Idle,
        Hover,
        Selected,
        Icon,
        Frame
    }

    private sealed class Skin
    {
        internal SpriteRenderer Plate;
        internal Transform Mark;
        internal Face Face;
        internal Sprite Sprite;
        internal Color Color;
        internal PassiveButton Button;
        internal ButtonRolloverHandler Handler;
        internal SpriteRenderer Target;
        internal Color Over;
        internal Color Out;
    }

    private static readonly string Folder = Path.Combine(BepInEx.Paths.GameRootPath, "Nocturne", "ChatArt");
    private static readonly string[] Names = { "chat", "settings", "guide", "friends", "map", "keyboard", "quick", "report" };
    private static readonly Sprite[] Marks = new Sprite[Names.Length];
    private static readonly List<Skin> Worn = new List<Skin>();
    private static readonly Dictionary<string, Texture2D> Textures = new Dictionary<string, Texture2D>();

    private static Sprite _top;
    private static Sprite _side;
    private static Sprite _report;
    private static Sprite _left;
    private static Sprite _right;
    private static bool _loaded;
    private static bool _failed;
    private static float _next;
    private static long _key;
    private static Color _idle;
    private static Color _hover;
    private static Color _picked;

    internal static void Tick(ChatController chat, bool force = false)
    {
        if (chat == null || _failed || !force && Time.unscaledTime < _next)
        {
            return;
        }

        _next = Time.unscaledTime + 0.25f;
        if (!NocturneConfig.ChatBtnArt.Value)
        {
            Release();
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

        NocturnePalette palette = NocturneStyle.Current;
        _idle = NocturneConfig.ChatChrome.Value ? ChatThemeStyler.ButtonColor() : Ui.Mix(Ui.C(0.118f, 0.129f, 0.161f, 1f), palette.Accent, 0.05f);
        _hover = Ui.Mix(palette.Accent, Ui.Black, 0.2f);
        _picked = palette.Accent;

        long key = ((long)Pack(_idle) << 24) | (long)Pack(palette.Accent);
        bool recolor = key != _key;
        _key = key;

        Worn.RemoveAll(s => s.Plate == null);
        Bind(chat);
        for (int i = 0; i < Worn.Count; i++)
        {
            Paint(Worn[i], recolor);
        }
    }

    private static bool Alive()
    {
        bool ok = _top != null && _side != null && _report != null && _left != null && _right != null;
        for (int i = 0; ok && i < Marks.Length; i++)
        {
            ok = Marks[i] != null;
        }

        return ok;
    }

    private static void Bind(ChatController chat)
    {
        Top(Of(chat.chatButton), 0, _left);
        Icon(Of(chat.openKeyboardButton), "OpenKeyboardIcon", 5, _side);
        Icon(Of(chat.quickChatButton), "QuickChatIcon", 6, _side);
        if (chat.banButton != null)
        {
            Icon(Of(chat.banButton.MenuButton), "OpenBanMenuIcon", 7, _report);
        }

        if (HudManager.InstanceExists)
        {
            HudManager hud = HudManager.Instance;
            Top(Of(hud.SettingsButton), 1, _right);
            Top(Of(hud.MatchInfoButton), 2, _right);
            Top(Of(hud.MapButton), 4, _right);
        }

        if (FriendsListManager.InstanceExists)
        {
            FriendsListButton friends = FriendsListManager.Instance.FriendsListButton;
            if (friends != null)
            {
                Top(Of(friends.Button), 3, _right);
            }
        }
    }

    private static Transform Of(Component c) => c != null ? c.transform : null;

    private static Transform Of(GameObject go) => go != null ? go.transform : null;

    private static void Top(Transform root, int mark, Sprite frame)
    {
        if (root == null)
        {
            return;
        }

        Wear(root.Find("Inactive"), _top, Marks[mark], Face.Idle, null);
        Wear(root.Find("Active"), _top, Marks[mark], Face.Hover, null);
        Wear(root.Find("Selected"), _top, Marks[mark], Face.Selected, null);

        Transform back = root.Find("Background");
        if (back == null)
        {
            back = root.Find("background");
        }

        SpriteRenderer sr = back != null ? back.GetComponent<SpriteRenderer>() : null;
        if (sr != null && sr.sprite != frame)
        {
            Skin skin = Worn.Find(w => w.Plate == sr);
            if (skin == null)
            {
                Worn.Add(new Skin { Plate = sr, Face = Face.Frame, Sprite = sr.sprite, Color = sr.color });
            }

            sr.sprite = frame;
        }
    }

    private static void Icon(Transform root, string icon, int mark, Sprite plate)
    {
        if (root != null)
        {
            Wear(root.Find(icon), plate, Marks[mark], Face.Icon, root);
        }
    }

    private static void Wear(Transform at, Sprite plate, Sprite mark, Face face, Transform button)
    {
        SpriteRenderer sr = at != null ? at.GetComponent<SpriteRenderer>() : null;
        if (sr == null || sr.sprite == plate)
        {
            return;
        }

        Skin skin = Worn.Find(w => w.Plate == sr);
        if (skin == null)
        {
            skin = new Skin { Plate = sr, Face = face, Sprite = sr.sprite, Color = sr.color };
            if (button != null)
            {
                skin.Button = button.GetComponent<PassiveButton>();
                skin.Handler = button.GetComponent<ButtonRolloverHandler>();
                if (skin.Handler != null)
                {
                    skin.Target = skin.Handler.Target;
                    skin.Over = skin.Handler.OverColor;
                    skin.Out = skin.Handler.OutColor;
                    skin.Handler.Target = sr;
                }
            }

            Worn.Add(skin);
        }

        sr.sprite = plate;
        skin.Mark = Overlay(sr, mark);
        Paint(skin, true);
    }

    private static Transform Overlay(SpriteRenderer plate, Sprite mark)
    {
        Transform old = plate.transform.Find("NocturneGlyph");
        if (old != null)
        {
            old.GetComponent<SpriteRenderer>().sprite = mark;
            return old;
        }

        var go = new GameObject("NocturneGlyph");
        go.layer = plate.gameObject.layer;
        Transform t = go.transform;
        t.SetParent(plate.transform, false);
        t.localPosition = Ui.V3(0f, 0f, -0.05f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = mark;
        sr.sharedMaterial = plate.sharedMaterial;
        sr.sortingLayerID = plate.sortingLayerID;
        sr.sortingOrder = plate.sortingOrder;
        return t;
    }

    private static void Paint(Skin s, bool force)
    {
        if (s.Face == Face.Frame)
        {
            return;
        }

        Color color = s.Face == Face.Hover ? _hover : s.Face == Face.Selected ? _picked : _idle;
        if (force)
        {
            s.Plate.color = color;
        }

        if (s.Handler == null || s.Button == null || !s.Button.enabled)
        {
            return;
        }

        if (!Same(s.Handler.OutColor, _idle))
        {
            s.Handler.OutColor = _idle;
        }

        if (!Same(s.Handler.OverColor, _hover))
        {
            s.Handler.OverColor = _hover;
        }
    }

    private static void Release()
    {
        for (int i = 0; i < Worn.Count; i++)
        {
            Skin s = Worn[i];
            if (s.Plate != null)
            {
                s.Plate.sprite = s.Sprite;
                s.Plate.color = s.Color;
            }

            if (s.Mark != null)
            {
                Object.Destroy(s.Mark.gameObject);
            }

            if (s.Handler != null)
            {
                s.Handler.Target = s.Target;
                s.Handler.OverColor = s.Over;
                s.Handler.OutColor = s.Out;
            }
        }

        Worn.Clear();
    }

    private static bool Same(Color a, Color b) => a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;

    private static int Pack(Color c) => ((int)(c.r * 255f + 0.5f) << 16) | ((int)(c.g * 255f + 0.5f) << 8) | (int)(c.b * 255f + 0.5f);

    private static bool Load()
    {
        _top = Art("plate_top.png");
        _side = Art("plate_side.png");
        _report = Art("plate_report.png");
        _left = Art("frame_left.png");
        _right = Art("frame_right.png");
        bool ok = _top != null && _side != null && _report != null && _left != null && _right != null;
        for (int i = 0; i < Names.Length; i++)
        {
            Marks[i] = Art("glyph_" + Names[i] + ".png");
            ok &= Marks[i] != null;
        }

        _loaded = ok;
        _failed = !ok;
        return ok;
    }

    internal static Sprite Art(string file, float border = 0f) =>
        Art(file, new Vector4(border, border, border, border), Ui.V(0.5f, 0.5f), 100f);

    internal static Sprite Art(string file, Vector4 border, Vector2 pivot, float ppu)
    {
        if (!Textures.TryGetValue(file, out Texture2D tex) || tex == null)
        {
            tex = Decode(file);
            if (tex == null)
            {
                return null;
            }

            Textures[file] = tex;
        }

        Sprite sp = border.x > 0f || border.y > 0f || border.z > 0f || border.w > 0f
            ? Sprite.Create(tex, Ui.R(0f, 0f, tex.width, tex.height), pivot, ppu, 0u, SpriteMeshType.FullRect, border)
            : Sprite.Create(tex, Ui.R(0f, 0f, tex.width, tex.height), pivot, ppu, 0u, SpriteMeshType.FullRect);
        sp.hideFlags = HideFlags.HideAndDontSave;
        return sp;
    }

    private static Texture2D Decode(string file)
    {
        byte[] data = ReadArt(file);
        if (data == null)
        {
            return null;
        }

        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, true)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        if (!ImageConversion.LoadImage(tex, data, true))
        {
            Object.Destroy(tex);
            return null;
        }

        return tex;
    }

    private static byte[] ReadArt(string file)
    {
        string own = Path.Combine(Folder, file);
        if (File.Exists(own))
        {
            try
            {
                return File.ReadAllBytes(own);
            }
            catch (IOException e)
            {
                NocturnePlugin.Logger?.LogWarning("[ChatArt] " + file + ": " + e.Message);
            }
        }

        using Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("Nocturne.textures." + file);
        if (s == null)
        {
            NocturnePlugin.Logger?.LogWarning("[ChatArt] нет ресурса " + file);
            return null;
        }

        using var ms = new MemoryStream();
        s.CopyTo(ms);
        return ms.ToArray();
    }
}
