using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Nocturne.Patches;

internal static class NocturneLobbyTheme
{
    private enum Role
    {
        Neutral, Accent, Start, Success, Danger, DarkBlue
    }

    private static readonly Dictionary<int, Color> spriteOrig = new Dictionary<int, Color>();
    private static readonly Dictionary<int, SpriteRenderer> spriteRef = new Dictionary<int, SpriteRenderer>();
    private static readonly Dictionary<int, Color> imageOrig = new Dictionary<int, Color>();
    private static readonly Dictionary<int, Image> imageRef = new Dictionary<int, Image>();
    private static readonly Dictionary<int, Color> textOrig = new Dictionary<int, Color>();
    private static readonly Dictionary<int, TMP_Text> textRef = new Dictionary<int, TMP_Text>();
    private static bool wasOn;
    private static float nextPaint;

    private static int paneId;
    private static SpriteRenderer[] paneSprites;
    private static Image[] paneImages;

    internal static Color OrangeAccent => NocturneStyle.Current.Accent;
    internal static bool AccentActive => NocturneConfig.LobbyTheme.Value;
    internal static Color LobbyAccent(Color fallback) => AccentActive ? OrangeAccent : fallback;

    internal static void Apply(GameStartManager m, bool immediate)
    {
        if (m == null)
            return;

        if (NocturneConfig.Vanilla || !NocturneConfig.LobbyTheme.Value)
        {
            if (wasOn)
                RestoreAll();
            wasOn = false;
            return;
        }

        if (!immediate && Time.unscaledTime < nextPaint)
            return;
        nextPaint = Time.unscaledTime + 0.5f;
        wasOn = true;

        NocturnePalette p = NocturneStyle.Current;
        Color header = Ui.Mix(p.Text, p.Accent, 0.5f);

        PaintText(m.PlayerCounter, header);
        PaintText(m.GameRoomNameCode, p.Accent);
        PaintText(m.GameStartText, p.Text);
        PaintText(m.RulesPresetText, p.Accent);
        PaintText(m.privatePublicPanelText, p.Text);

        PaintPanelBackdrop(m);
        PaintCopyButton(m);

        if (!NocturneConfig.Vanilla && NocturneConfig.StartBtnGlass.Value)
            PaintStartButton(m.StartButton);
        else
            PaintButton(m.StartButton, p, Role.Start);
        PaintActionButton(m.EditButton, false);
        PaintButton(m.HostPublicButton, p, Role.Success);
        PaintButton(m.HostPrivateButton, p, Role.Danger);
        PaintActionButton(m.HostViewButton, true);
        PaintActionButton(m.ClientViewButton, true);

        BlendSprite(m.MapImage, p.Accent, 0.18f);
    }

    private static void PaintPanelBackdrop(GameStartManager m)
    {
        if (m.LobbyInfoPane == null || m.GameRoomNameCode == null)
            return;

        Vector3 rc = m.GameRoomNameCode.transform.position;
        float left = rc.x - 0.72f, right = rc.x + 2.55f, top = rc.y + 0.78f, bottom = rc.y - 5.45f;
        Extend(m.EditButton, ref left, ref right, ref bottom);
        Extend(m.HostViewButton, ref left, ref right, ref bottom);
        Extend(m.ClientViewButton, ref left, ref right, ref bottom);
        if (!(right > left && top > bottom))
            return;

        int pid = m.LobbyInfoPane.GetInstanceID();
        if (pid != paneId || paneSprites == null)
        {
            paneId = pid;
            paneSprites = m.LobbyInfoPane.GetComponentsInChildren<SpriteRenderer>(true);
            paneImages = m.LobbyInfoPane.GetComponentsInChildren<Image>(true);
        }

        foreach (SpriteRenderer r in paneSprites)
        {
            if (r == null || !LayerGeom(r, out Vector3 c, out Vector2 size))
                continue;
            string n = r.name.ToLowerInvariant();
            if (!MainPanelLayer(n, c, size, left, right, bottom, top))
                continue;
            PaintSprite(r, PanelLayerColor(n, c, size, bottom, top));
        }
        foreach (Image img in paneImages)
        {
            if (img == null || !LayerGeom(img, out Vector3 c, out Vector2 size)) continue;
            string n = img.name.ToLowerInvariant();
            if (!MainPanelLayer(n, c, size, left, right, bottom, top))
                continue;
            PaintImage(img, PanelLayerColor(n, c, size, bottom, top));
        }
    }

    private static void Extend(PassiveButton b, ref float left, ref float right, ref float bottom)
    {
        if (b == null)
            return;
        Vector3 pos = b.transform.position;
        left = Ui.Min(left, pos.x - 0.72f);
        right = Ui.Max(right, pos.x + 0.72f);
        bottom = Ui.Min(bottom, pos.y - 0.56f);
    }

    private static bool LayerGeom(SpriteRenderer r, out Vector3 c, out Vector2 size)
    {
        Bounds b = r.bounds;
        size = Ui.V(Ui.Abs(b.size.x), Ui.Abs(b.size.y));
        c = b.center;
        return size.x > 0.001f && size.y > 0.001f;
    }

    private static bool LayerGeom(Image img, out Vector3 c, out Vector2 size)
    {
        c = Ui.Zero3;
        size = Ui.Zero2;
        RectTransform rt = img.rectTransform;
        if (rt == null)
            return false;
        size = Ui.V(Ui.Abs(rt.rect.m_Width * rt.lossyScale.x), Ui.Abs(rt.rect.m_Height * rt.lossyScale.y));
        c = img.transform.position;
        return size.x > 0.001f && size.y > 0.001f;
    }

    private static bool MainPanelLayer(string n, Vector3 c, Vector2 size, float left, float right, float bottom, float top)
    {
        if (string.IsNullOrWhiteSpace(n)) return false;
        if (n.Contains("copy") || n.Contains("edit") || n.Contains("view") || n.Contains("button") || n.Contains("icon") || n.Contains("glyph"))
            return false;
        if (size.x < 1.5f || size.y < 2.8f || size.x > 7.2f || size.y > 11.5f) return false;
        if (c.x < left - 0.42f || c.x > right + 0.42f) return false;
        if (c.y < bottom - 0.45f || c.y > top + 0.45f)
            return false;
        return !n.Contains("text");
    }

    private static Color PanelLayerColor(string n, Vector3 c, Vector2 size, float bottom, float top)
    {
        Color c1 = Ui.C(0.24f, 0.24f, 0.245f, 0.975f);
        Color c2 = Ui.C(0.145f, 0.145f, 0.15f, 0.985f);
        Color c3 = Ui.C(0.095f, 0.095f, 0.10f, 0.99f);
        Color c4 = Ui.C(0.125f, 0.125f, 0.13f, 0.995f);
        Color c5 = Ui.C(0.06f, 0.06f, 0.065f, 0.95f);
        Color c6 = Ui.C(0.34f, 0.34f, 0.35f, 0.90f);
        float t = Ui.Clamp01(Ui.InverseLerp(bottom, top, c.y));
        if (n.Contains("shadow")) return c5;
        if (n.Contains("border") || n.Contains("outline") || n.Contains("frame")) return Ui.Mix(c5, c4, 0.78f);
        if (n.Contains("highlight") || n.Contains("shine") || n.Contains("light") || n.Contains("edge") || n.Contains("top"))
            return Ui.Mix(c4, c6, 0.72f + 0.28f * t);
        if (n.Contains("background") || n.Contains("bg") || n.Contains("panel") || n.Contains("box") || n.Contains("fill"))
            return Ui.Mix(c2, c1, t);
        if (size.y > 4.8f)
            return Ui.Mix(c3, c1, t);
        return Ui.Mix(c4, c2, 0.18f + t * 0.82f);
    }

    private static void PaintCopyButton(GameStartManager m)
    {
        if (m.LobbyInfoPane == null || m.GameRoomNameCode == null) return;
        PassiveButton b = FindCopyButton(m);
        if (b == null)
            return;

        foreach (ButtonRolloverHandler ro in b.GetComponentsInChildren<ButtonRolloverHandler>(true))
            if (ro != null)
                Object.Destroy(ro);
        Button ui = b.GetComponent<Button>();
        if (ui != null)
            ui.transition = Selectable.Transition.None;

        bool oranged = false;
        SpriteRenderer firstIcon = null;
        foreach (SpriteRenderer r in b.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (r == null || SkipSprite(r))
                continue;
            firstIcon ??= r;
            string n = r.name.ToLowerInvariant();
            Color c = CopyLayerColor(n, r.transform.localPosition, out bool orange);
            PaintSprite(r, c);
            oranged |= orange;
        }
        foreach (Image img in b.GetComponentsInChildren<Image>(true))
        {
            if (img == null) continue;
            string n = img.name.ToLowerInvariant();
            Color c = CopyLayerColor(n, img.transform.localPosition, out bool orange);
            PaintImage(img, c);
            oranged |= orange;
        }
        if (!oranged && firstIcon != null)
            PaintSprite(firstIcon, NocturneStyle.Current.Accent);
    }

    private static PassiveButton FindCopyButton(GameStartManager m)
    {
        Vector3 pos = m.GameRoomNameCode.transform.position;
        PassiveButton best = null;
        float score = float.MaxValue;
        foreach (PassiveButton b in m.LobbyInfoPane.GetComponentsInChildren<PassiveButton>(true))
        {
            if (b == null)
                continue;
            Vector3 bp = b.transform.position;
            float dx = bp.x - pos.x, dy = Ui.Abs(bp.y - pos.y);
            if (dx < 0.15f || dy > 0.65f)
                continue;
            float sc = dx * 0.45f + dy;
            string n = b.name.ToLowerInvariant();
            if (n.Contains("copy") || n.Contains("clipboard") || n.Contains("code"))
                sc -= 0.4f;
            if (sc < score)
            {
                score = sc;
                best = b;
            }
        }
        return best;
    }

    private static Color CopyLayerColor(string n, Vector3 local, out bool orange)
    {
        orange = false;
        Color v1 = Ui.C(0.12f, 0.12f, 0.13f, 1f);
        Color v2 = Ui.C(0.19f, 0.19f, 0.21f, 1f);
        Color dark = Ui.C(0.03f, 0.02f, 0.01f, 1f);
        Color a = NocturneStyle.Current.Accent;
        Color oc = a;
        Color ochi = Ui.Mix(a, Ui.White, 0.12f);
        if (n.Contains("border") || n.Contains("outline") || n.Contains("shadow"))
            return dark;
        if (n.Contains("highlight") || n.Contains("shine") || n.Contains("light") || n.Contains("glow") || n.Contains("grad"))
        {
            orange = true;
            return ochi;
        }
        if (n.Contains("icon") || n.Contains("copy") || n.Contains("clip") || n.Contains("glyph") || n.Contains("line"))
        {
            orange = true;
            return oc;
        }
        return Ui.Mix(v1, v2, Ui.Clamp01(Ui.InverseLerp(-0.55f, 0.55f, local.y)));
    }

    private static void PaintStartButton(PassiveButton b)
    {
        if (b == null) return;

        foreach (ButtonRolloverHandler ro in b.GetComponentsInChildren<ButtonRolloverHandler>(true))
            if (ro != null)
                Object.Destroy(ro);
        Button btn = b.GetComponent<Button>();
        if (btn != null)
            btn.transition = Selectable.Transition.None;

        Color black = Ui.C(0.02f, 0.021f, 0.025f, 1f);
        Color stripe = OrangeAccent;

        foreach (SpriteRenderer r in b.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (r == null || SkipSprite(r)) continue;
            PaintSprite(r, ActionAccentLayer(r.name.ToLowerInvariant()) ? stripe : black);
        }

        foreach (Image img in b.GetComponentsInChildren<Image>(true))
        {
            if (img == null) continue;
            PaintImage(img, ActionAccentLayer(img.name.ToLowerInvariant()) ? stripe : black);
        }

        Color tw = Ui.White;
        b.activeTextColor = tw;
        b.inactiveTextColor = tw;
        b.selectedTextColor = tw;
        b.selectedInactiveTextColor = tw;
        b.disabledTextColor = Ui.C(0.62f, 0.65f, 0.70f, 0.55f);
    }

    private static void PaintActionButton(PassiveButton b, bool isView)
    {
        if (b == null)
            return;

        foreach (ButtonRolloverHandler ro in b.GetComponentsInChildren<ButtonRolloverHandler>(true))
            if (ro != null) Object.Destroy(ro);
        Button ui = b.GetComponent<Button>();
        if (ui != null)
            ui.transition = Selectable.Transition.None;

        bool accent = false;
        SpriteRenderer firstSprite = null;
        Image firstImage = null;

        foreach (SpriteRenderer r in b.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (r == null || SkipSprite(r)) continue;
            firstSprite ??= r;
            string n = r.name.ToLowerInvariant();
            if (!ActionLayerName(n)) continue;
            PaintSprite(r, ActionLayerColor(n, r.transform.localPosition, isView));
            if (ActionAccentLayer(n))
                accent = true;
        }
        foreach (Image img in b.GetComponentsInChildren<Image>(true))
        {
            if (img == null)
                continue;
            firstImage ??= img;
            string n = img.name.ToLowerInvariant();
            if (!ActionLayerName(n))
                continue;
            PaintImage(img, ActionLayerColor(n, img.transform.localPosition, isView));
            if (ActionAccentLayer(n)) accent = true;
        }

        if (!accent)
        {
            if (firstImage != null)
                PaintImage(firstImage, ActionAccentColor(firstImage.transform.localPosition, isView));
            else if (firstSprite != null)
                PaintSprite(firstSprite, ActionAccentColor(firstSprite.transform.localPosition, isView));
        }

        Color tw = Ui.White;
        b.activeTextColor = tw;
        b.inactiveTextColor = tw;
        b.selectedTextColor = tw;
        b.selectedInactiveTextColor = tw;

        TMP_Text txt = b.buttonText;
        if (txt != null)
        {
            txt.enableVertexGradient = true;
            Color warm = Ui.C(1f, 0.98f, 0.95f, 1f);
            Color low = isView ? Ui.C(0.97f, 0.87f, 0.74f, 1f) : Ui.C(1f, 0.80f, 0.56f, 1f);
            txt.colorGradient = new VertexGradient(warm, warm, low, low);
            txt.fontStyle = FontStyles.Bold;
            txt.outlineWidth = 0.16f;
            txt.outlineColor = Ui.C(0.02f, 0.015f, 0.01f, 1f);
            PaintText(txt, tw);
            txt.ForceMeshUpdate();
        }
    }

    private static bool ActionAccentLayer(string n) =>
        n.Contains("highlight") || n.Contains("shine") || n.Contains("light") || n.Contains("glow") || n.Contains("grad") ||
        n.Contains("stripe") || n.Contains("slash") || n.Contains("diag") || n.Contains("line") || n.Contains("accent");

    private static bool ActionLayerName(string n) =>
        n.Contains("bg") || n.Contains("background") || n.Contains("button") || n.Contains("btn") || n.Contains("panel") || n.Contains("box") ||
        n.Contains("normal") || n.Contains("inactive") || n.Contains("active") || n.Contains("border") || n.Contains("outline") || n.Contains("shadow") ||
        n.Contains("frame") || n.Contains("fill") || n.Contains("cap") || n.Contains("edge") || ActionAccentLayer(n);

    private static Color ActionLayerColor(string n, Vector3 local, bool isView)
    {
        Color a1 = Ui.C(0.18f, 0.19f, 0.22f, 0.99f);
        Color a2 = Ui.C(0.12f, 0.13f, 0.16f, 0.99f);
        Color a3 = Ui.C(0.05f, 0.055f, 0.07f, 1f);
        Color a4 = Ui.C(0.08f, 0.055f, 0.035f, 1f);
        Color a5 = Ui.C(0.015f, 0.015f, 0.02f, 1f);
        Color a6 = Ui.C(1f, 0.78f, 0.42f, 0.98f);
        if (n.Contains("shadow"))
            return a5;
        if (n.Contains("border") || n.Contains("outline") || n.Contains("frame")) return a4;
        if (ActionAccentLayer(n))
            return ActionAccentColor(local, isView);
        if (n.Contains("top") || n.Contains("cap") || n.Contains("edge")) return a6;
        if (local.y > 0.08f)
            return a1;
        if (local.y < -0.08f)
            return a3;
        return a2;
    }

    private static Color ActionAccentColor(Vector3 local, bool isView)
    {
        Color a = NocturneStyle.Current.Accent;
        float top = isView ? 0.9f : 1f;
        float bot = isView ? 0.62f : 0.72f;
        Color hi = Ui.C(a.r * top, a.g * top, a.b * top, 1f);
        Color lo = Ui.C(a.r * bot, a.g * bot, a.b * bot, 1f);
        return local.y < -0.02f ? lo : hi;
    }

    private static void PaintButton(PassiveButton b, NocturnePalette p, Role role)
    {
        if (b == null) return;

        Color fill, text;
        float alpha = -1f;
        switch (role)
        {
            case Role.Start:
                fill = Ui.Mix(p.ButtonHover, p.Accent, 0.45f);
                text = Ui.White;
                break;
            case Role.Accent:
                fill = Ui.Mix(p.Button, p.Accent, 0.42f);
                text = p.Text;
                break;
            case Role.Success:
                fill = Ui.C(0.10f, 0.40f, 0.22f, 1f);
                text = Ui.C(0.94f, 1f, 0.95f, 1f);
                break;
            case Role.Danger:
                fill = Ui.C(0.42f, 0.06f, 0.10f, 1f);
                text = Ui.C(1f, 0.96f, 0.96f, 1f);
                break;
            case Role.DarkBlue:
                fill = Ui.Mix(Ui.C(0.02f, 0.027f, 0.045f, 1f), p.Accent, 0.45f);
                text = Ui.White;
                break;
            default:
                fill = Ui.Mix(p.Panel, p.Button, 0.6f);
                text = p.Text;
                break;
        }

        b.activeTextColor = text;
        b.inactiveTextColor = text;
        b.selectedTextColor = text;
        b.selectedInactiveTextColor = text;
        b.disabledTextColor = Ui.C(p.Muted.r, p.Muted.g, p.Muted.b, 0.55f);

        foreach (SpriteRenderer r in b.GetComponentsInChildren<SpriteRenderer>(true))
            if (r != null && !SkipSprite(r)) PaintSprite(r, fill, alpha);

        foreach (Image img in b.GetComponentsInChildren<Image>(true))
            if (img != null)
                PaintImage(img, fill);

        foreach (ButtonRolloverHandler ro in b.GetComponentsInChildren<ButtonRolloverHandler>(true))
            if (ro != null) Object.Destroy(ro);

        Button ui = b.GetComponent<Button>();
        if (ui != null)
            ui.transition = Selectable.Transition.None;

        PaintText(b.buttonText, text);
    }

    private static bool SkipSprite(SpriteRenderer r)
    {
        Component c = r;
        return c.GetComponentInParent<PlayerControl>() != null || c.GetComponentInParent<PoolablePlayer>() != null;
    }

    private static void PaintSprite(SpriteRenderer r, Color col, float alpha = -1f)
    {
        if (r == null) return;
        int id = r.GetInstanceID();
        if (!spriteOrig.ContainsKey(id))
        {
            spriteOrig[id] = r.color;
            spriteRef[id] = r;
        }
        r.color = Ui.C(col.r, col.g, col.b, alpha >= 0f ? alpha * spriteOrig[id].a : spriteOrig[id].a);
    }

    private static void PaintImage(Image img, Color col)
    {
        if (img == null)
            return;
        int id = img.GetInstanceID();
        if (!imageOrig.ContainsKey(id))
        {
            imageOrig[id] = img.color;
            imageRef[id] = img;
        }
        img.color = Ui.C(col.r, col.g, col.b, imageOrig[id].a);
    }

    private static void BlendSprite(SpriteRenderer r, Color tint, float amount)
    {
        if (r == null)
            return;
        int id = r.GetInstanceID();
        if (!spriteOrig.ContainsKey(id))
        {
            spriteOrig[id] = r.color;
            spriteRef[id] = r;
        }
        Color mix = Ui.Mix(spriteOrig[id], tint, amount);
        mix.a = spriteOrig[id].a;
        r.color = mix;
    }

    private static void PaintText(TMP_Text t, Color col)
    {
        if (t == null)
            return;
        int id = t.GetInstanceID();
        if (!textOrig.ContainsKey(id))
        {
            textOrig[id] = t.color;
            textRef[id] = t;
        }
        t.color = Ui.C(col.r, col.g, col.b, textOrig[id].a);
    }

    private static void RestoreAll()
    {
        foreach (KeyValuePair<int, SpriteRenderer> pair in spriteRef)
            if (pair.Value != null && spriteOrig.TryGetValue(pair.Key, out Color c))
                pair.Value.color = c;
        foreach (KeyValuePair<int, Image> pair in imageRef)
            if (pair.Value != null && imageOrig.TryGetValue(pair.Key, out Color c))
                pair.Value.color = c;
        foreach (KeyValuePair<int, TMP_Text> pair in textRef)
            if (pair.Value != null && textOrig.TryGetValue(pair.Key, out Color c))
                pair.Value.color = c;

        spriteOrig.Clear();
        spriteRef.Clear();
        imageOrig.Clear();
        imageRef.Clear();
        textOrig.Clear();
        textRef.Clear();
    }
}

[HarmonyPatch(typeof(GameStartManager), nameof(GameStartManager.Start))]
internal static class LobbyThemeStartPatch
{
    public static void Postfix(GameStartManager __instance)
    {
        try
        {
            NocturneLobbyTheme.Apply(__instance, true);
        }
        catch { }
    }
}

[HarmonyPatch(typeof(GameStartManager), nameof(GameStartManager.Update))]
internal static class LobbyThemeUpdatePatch
{
    public static void Postfix(GameStartManager __instance)
    {
        try
        {
            NocturneLobbyTheme.Apply(__instance, false);
        }
        catch { }
    }
}
