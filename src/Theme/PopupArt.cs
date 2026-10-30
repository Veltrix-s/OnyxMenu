using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Nocturne.Patches;

internal static class PopupArt
{
    private sealed class Skin
    {
        internal SpriteRenderer Sr;
        internal int Id;
        internal Sprite Sprite;
        internal Color Color;
        internal Sprite Art;
        internal bool Hot;
    }

    private static readonly List<Skin> Worn = new List<Skin>();
    private static readonly HashSet<int> Seen = new HashSet<int>();

    private static Sprite _body;
    private static Sprite _exit;
    private static Sprite _exitHot;
    private static bool _loaded;
    private static bool _failed;

    internal static void Open(DisconnectPopup popup)
    {
        NocturneMainArt.Okay(popup);
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
        Transform t = popup.transform;
        Take(t.Find("Background"), "IMAGE-ErrorPrompt", _body, false);
        Take(t.Find("CloseButton/Normal"), "BUTTON-ExitStandard", _exit, false);
        Take(t.Find("CloseButton/Highlight"), "BUTTON-ExitHover", _exitHot, true);
        Color accent = NocturneStyle.Current.Accent;
        for (int i = 0; i < Worn.Count; i++)
        {
            Paint(Worn[i], accent);
        }
    }

    private static void Release()
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

        Worn.Clear();
        Seen.Clear();
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

    private static void Take(Transform t, string sprite, Sprite art, bool hot)
    {
        if (t == null)
        {
            return;
        }

        SpriteRenderer sr = t.GetComponent<SpriteRenderer>();
        if (sr == null || sr.sprite == null || sr.sprite.name != sprite || !Seen.Add(sr.GetInstanceID()))
        {
            return;
        }

        Worn.Add(new Skin { Sr = sr, Id = sr.GetInstanceID(), Sprite = sr.sprite, Color = sr.color, Art = art, Hot = hot });
    }

    private static void Paint(Skin s, Color accent)
    {
        SpriteRenderer sr = s.Sr;
        if (sr.sprite != s.Art)
        {
            sr.sprite = s.Art;
        }

        if (!s.Hot)
        {
            return;
        }

        Color now = sr.color;
        if (now.r != accent.r || now.g != accent.g || now.b != accent.b)
        {
            sr.color = Ui.C(accent.r, accent.g, accent.b, s.Color.a);
        }
    }

    private static bool Alive() => _body != null && _exit != null && _exitHot != null;

    private static bool Load()
    {
        _body = ChatArt.Art("popup_bg.png", new Vector4(0f, 0f, 0f, 0f), Ui.V(660f / 1317f, 0.5f), 100f);
        _exit = ChatArt.Art("popup_exit.png");
        _exitHot = ChatArt.Art("popup_exit_hot.png");
        _loaded = Alive();
        _failed = !_loaded;
        return _loaded;
    }
}

[HarmonyPatch(typeof(DisconnectPopup), nameof(DisconnectPopup.SetText))]
internal static class PopupArtPatch
{
    public static void Postfix(DisconnectPopup __instance) => PopupArt.Open(__instance);
}
