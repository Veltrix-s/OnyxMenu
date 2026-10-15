using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Nocturne.Patches;

internal static class NocturneMainArt
{
    private const string Res = "Nocturne.NOCTURNE.jpg";

    private static Sprite _art;
    private static bool _failed;
    private static GameObject _back;

    private static readonly List<GameObject> _offGo = new List<GameObject>(4);
    private static readonly List<Renderer> _offRen = new List<Renderer>(6);

    private static int _bound;
    private static int _tops;
    private static float _topAt;
    private static GameObject _bgTex, _shine, _stars;
    private static Renderer _left, _leftMask, _right, _rightMask;
    private static Transform _tint;
    private static Vector3 _tintAt;
    private static bool _live;

    internal static void Apply(MainMenuManager m)
    {
        if (m == null) return;
        if (NocturneConfig.Vanilla || !NocturneConfig.MainMenuArt.Value)
        {
            if (_live)
                Restore();
            return;
        }

        Bind(m);
        Hide();
        Paint(m);
        Backdrop();
        _live = true;
    }

    private static readonly Color Ink = Ui.C(0.14f, 0.14f, 0.16f, 1f);
    private static readonly Color InkHot = Ui.C(0.24f, 0.24f, 0.27f, 1f);
    private static readonly Color InkShine = Ui.C(0.28f, 0.28f, 0.32f, 1f);

    private static readonly Dictionary<int, Color> _wasCol = new Dictionary<int, Color>();
    private static readonly Dictionary<int, SpriteRenderer> _srRef = new Dictionary<int, SpriteRenderer>();
    private static readonly Dictionary<int, Color> _wasTxt = new Dictionary<int, Color>();
    private static readonly Dictionary<int, TMP_Text> _txtRef = new Dictionary<int, TMP_Text>();

    private static void Paint(MainMenuManager m)
    {
        try
        {
            var list = m.mainButtons;
            if (list != null)
                for (int i = 0; i < list.Count; i++)
                    Btn(list[i]);
        }
        catch { }

        Btn(m.playButton);
        Btn(m.inventoryButton);
        Btn(m.shopButton);
        Btn(m.myAccountButton);
        Btn(m.newsButton);
        Btn(m.settingsButton);
        Btn(m.creditsButton);
        Btn(m.quitButton);
        try
        {
            Menus(m);
        }
        catch { }
        DropAtlas();
        if (Time.unscaledTime < _topAt) return;

        Top();
        _topAt = Time.unscaledTime + (++_tops < 3 ? 1.5f : 20f);
    }

    private const float BarAspect = 2.4f;

    private static readonly Color InkIcon = Ui.C(0.88f, 0.90f, 0.94f, 1f);
    private static readonly Color InkThumb = Ui.C(0.46f, 0.48f, 0.54f, 1f);
    private static readonly Color TextHi = Ui.C(0.92f, 0.94f, 0.97f, 1f);
    private static readonly Color TextDark = Ui.C(0.10f, 0.10f, 0.12f, 1f);
    private static readonly Color PlateLight = Ui.C(0.80f, 0.82f, 0.86f, 1f);
    private static readonly Color FieldOver = Ui.C(0.70f, 0.72f, 0.78f, 1f);

    private sealed class Held
    {
        public SpriteRenderer sr;
        public Sprite sprite;
        public bool color;
        public float r;
        public float g;
        public float b;
    }

    private static readonly Dictionary<int, byte> _kind = new Dictionary<int, byte>();
    private static readonly Dictionary<int, byte> _plan = new Dictionary<int, byte>();
    private static readonly Dictionary<int, Vector2> _dim = new Dictionary<int, Vector2>();
    private static readonly Dictionary<int, bool> _ring = new Dictionary<int, bool>();
    private static readonly Dictionary<int, RenderTexture> _atlas = new Dictionary<int, RenderTexture>();
    private static readonly Dictionary<int, Sprite> _baked = new Dictionary<int, Sprite>();
    private static readonly Dictionary<long, Sprite> _tinted = new Dictionary<long, Sprite>();
    private static readonly Dictionary<int, SpriteRenderer> _swapRef = new Dictionary<int, SpriteRenderer>();
    private static readonly Dictionary<int, Sprite> _swapWas = new Dictionary<int, Sprite>();
    private static readonly Dictionary<int, ButtonRolloverHandler> _rollRef = new Dictionary<int, ButtonRolloverHandler>();
    private static readonly Dictionary<int, Color[]> _rollWas = new Dictionary<int, Color[]>();
    private static readonly Dictionary<int, PlayOnlineButtonSprite> _onlineRef = new Dictionary<int, PlayOnlineButtonSprite>();
    private static readonly Dictionary<int, Sprite> _onlineWas = new Dictionary<int, Sprite>();
    private static readonly Dictionary<int, Held> _last = new Dictionary<int, Held>();
    private static readonly List<Held> _held = new List<Held>();
    private static readonly HashSet<int> _heldIds = new HashSet<int>();
    private static readonly HashSet<int> _seen = new HashSet<int>();
    private static readonly List<int> _drop = new List<int>();
    private static Texture2D _one;
    private static int _menuPasses;

    private static void Menus(MainMenuManager m)
    {
        _menuPasses++;
        if (_menuPasses <= 4)
            ForgetNone();

        bool warm = _menuPasses <= 6;
        Tree(m.gameModeButtons, true, false, warm);
        Tree(m.onlineButtons, true, false, warm);
        Tree(m.enterCodeButtons, true, false, warm);
        Tree(m.accountButtons, false, false, warm);
        CreateGameOptions create = m.createGameScreen;
        if (create != null)
            Tree(create.gameObject, false, true, warm);
        if (warm || _menuPasses % 4 == 0)
            Online(m.gameObject);

    }

    private static void ForgetNone()
    {
        _drop.Clear();
        foreach (KeyValuePair<int, byte> kv in _kind)
            if (kv.Value == 1)
                _drop.Add(kv.Key);
        for (int i = 0; i < _drop.Count; i++)
            _kind.Remove(_drop[i]);
    }

    private static void Tree(GameObject root, bool cards, bool create, bool warm)
    {
        if (root == null) return;
        if (!warm && !root.activeInHierarchy) return;

        var rolls = root.GetComponentsInChildren<ButtonRolloverHandler>(true);
        for (int i = 0; i < rolls.Length; i++)
            Roll(rolls[i]);

        var buttons = root.GetComponentsInChildren<PassiveButton>(true);
        for (int i = 0; i < buttons.Length; i++)
            Teal(buttons[i], cards, create);

        var all = root.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < all.Length; i++)
        {
            SpriteRenderer sr = all[i];
            if (sr == null) continue;
            if (_plan.TryGetValue(sr.GetInstanceID(), out byte p))
            {
                Put(sr, p);
                continue;
            }

            byte k = Look(sr);
            if (k == 2)
                Plan(sr, BarPlan(sr, false, false));
            else if (k == 4 && create)
                Plan(sr, 7);
            else if (k == 6)
                Plan(sr, 8);
        }
    }

    private static byte BarPlan(SpriteRenderer sr, bool hot, bool light)
    {
        if (sr.name.IndexOf("shine", StringComparison.OrdinalIgnoreCase) >= 0)
            return 4;
        return (byte)(light ? 10 : hot ? 3 : 2);
    }

    private static void Plan(SpriteRenderer sr, byte plan)
    {
        _plan[sr.GetInstanceID()] = plan;
        Put(sr, plan);
    }

    private static void Put(SpriteRenderer sr, byte plan)
    {
        if (sr == null || sr.sprite == null) return;

        int id = sr.GetInstanceID();
        if (_last.TryGetValue(id, out Held was) && !_heldIds.Contains(id))
        {
            Color cur = sr.color;
            bool swapped = was.sprite != null && sr.sprite != was.sprite;
            bool tinted = was.color && (Ui.Abs(cur.r - was.r) > 0.004f || Ui.Abs(cur.g - was.g) > 0.004f || Ui.Abs(cur.b - was.b) > 0.004f);
            if (swapped || tinted)
            {
                _heldIds.Add(id);
                _held.Add(was);
            }
        }

        Sprite made = null;
        switch (plan)
        {
            case 2:
            case 6:
                made = Retint(sr, 0, Ink);
                break;
            case 3:
                made = Retint(sr, 1, InkHot);
                break;
            case 4:
                made = Retint(sr, 4, InkShine);
                break;
            case 5:
            case 8:
                made = Retint(sr, 2, InkIcon);
                break;
            case 9:
                made = Retint(sr, 0, Ink);
                break;
            case 10:
                made = Retint(sr, 3, PlateLight);
                break;
        }

        Color rgb;
        if (made == null)
        {
            Color c = plan == 3 ? InkHot : plan == 4 ? InkShine : plan == 5 || plan == 8 ? InkIcon : plan == 7 ? InkThumb : plan == 10 ? PlateLight : Ink;
            Dye(sr, c, true);
            rgb = sr.color;
        }
        else
            rgb = Was(sr);

        if (was == null)
        {
            was = new Held { sr = sr };
            _last[id] = was;
        }
        was.sprite = made;
        was.color = made == null;
        was.r = rgb.r;
        was.g = rgb.g;
        was.b = rgb.b;
    }

    private static Sprite Retint(SpriteRenderer sr, byte variant, Color target)
    {
        int id = sr.GetInstanceID();
        Sprite cur = sr.sprite;
        Sprite src = _swapWas.TryGetValue(id, out Sprite was) ? was : cur;
        Sprite made = Tinted(src, target, variant);
        if (made != null && cur != made)
            Replace(sr, made);
        return made;
    }

    private static void Replace(SpriteRenderer sr, Sprite made)
    {
        int id = sr.GetInstanceID();
        if (!_swapWas.ContainsKey(id))
        {
            _swapWas[id] = sr.sprite;
            _swapRef[id] = sr;
        }
        SpriteDrawMode mode = sr.drawMode;
        Vector2 size = sr.size;
        sr.sprite = made;
        if (mode != SpriteDrawMode.Simple)
            sr.size = size;
    }

    internal static void Hold()
    {
        for (int i = 0; i < _held.Count; i++)
        {
            Held h = _held[i];
            SpriteRenderer sr = h.sr;
            if (sr == null) continue;

            if (h.sprite != null && sr.sprite != h.sprite)
                sr.sprite = h.sprite;
            if (!h.color) continue;

            Color c = sr.color;
            if (Ui.Abs(c.r - h.r) > 0.004f || Ui.Abs(c.g - h.g) > 0.004f || Ui.Abs(c.b - h.b) > 0.004f)
                sr.color = Ui.C(h.r, h.g, h.b, c.a);
        }
    }

    private static byte Look(SpriteRenderer sr)
    {
        int id = sr.GetInstanceID();
        if (_kind.TryGetValue(id, out byte hit)) return hit;

        byte k = 1;
        Sprite sp = sr.sprite;
        if (sp != null && sr.name.IndexOf("crewmate", StringComparison.OrdinalIgnoreCase) < 0)
        {
            Vector3 b = sp.bounds.size;
            Vector2 s = sr.drawMode == SpriteDrawMode.Simple ? Ui.V(b.x, b.y) : sr.size;
            if (s.x < 0.001f || s.y < 0.001f)
                s = Ui.V(b.x, b.y);
            Vector3 ls = sr.transform.lossyScale;
            float w = s.x * Ui.Abs(ls.x);
            float h = s.y * Ui.Abs(ls.y);
            _dim[id] = Ui.V(w, h);

            Color o = _wasCol.TryGetValue(id, out Color was) ? was : sr.color;
            Color tex = TexTint(sp);
            if (IsTeal(Ui.C(tex.r * o.r, tex.g * o.g, tex.b * o.b, 1f)))
            {
                if (h > 0.001f && w / h >= BarAspect)
                    k = 2;
                else if (w <= 0.35f && h >= w * 2f)
                    k = 4;
                else if (w <= 0.9f && h <= 0.9f)
                    k = 3;
                else
                    k = 5;
            }
            else if (sr.drawMode == SpriteDrawMode.Sliced && Ring(sp))
                k = 6;
        }
        _kind[id] = k;
        return k;
    }

    private static bool IsTeal(Color c)
    {
        PlayerGfx.Hsv(c, out float h, out float s, out float v);
        return h >= 0.40f && h <= 0.56f && s >= 0.18f && v >= 0.30f;
    }

    private static bool IsTealPx(Color32 c) => c.a > 16 && c.g > c.r + 20 && c.b > c.r + 12;

    private static float Lum(Color32 c) => (0.299f * c.r + 0.587f * c.g + 0.114f * c.b) / 255f;

    private static byte ToByte(float v) => (byte)(Ui.Clamp01(v) * 255f + 0.5f);

    private static bool LightPlate(SpriteRenderer sr)
    {
        if (sr.drawMode != SpriteDrawMode.Sliced) return false;
        if (!_dim.TryGetValue(sr.GetInstanceID(), out Vector2 d) || d.x < 0.8f || d.y < 0.3f) return false;

        Color o = _wasCol.TryGetValue(sr.GetInstanceID(), out Color was) ? was : sr.color;
        if (o.a < 0.5f) return false;

        Color t = TexTint(sr.sprite);
        return 0.299f * t.r * o.r + 0.587f * t.g * o.g + 0.114f * t.b * o.b >= 0.7f;
    }

    private static bool Ring(Sprite sp)
    {
        int sid = sp.GetInstanceID();
        if (_ring.TryGetValue(sid, out bool hit)) return hit;

        bool res = false;
        RenderTexture prev = RenderTexture.active;
        try
        {
            Rect r = sp.textureRect;
            int w = (int)r.m_Width;
            int h = (int)r.m_Height;
            if (w >= 8 && h >= 8 && w <= 256 && h <= 256)
            {
                RenderTexture.active = Atlas(sp.texture);
                var tmp = new Texture2D(w, h, TextureFormat.RGBA32, false);
                tmp.ReadPixels(Ui.R(r.m_XMin, r.m_YMin, w, h), 0, 0);
                Color32[] px = tmp.GetPixels32();
                int solid = 0;
                int teal = 0;
                for (int i = 0; i < px.Length; i++)
                {
                    Color32 c = px[i];
                    if (c.a < 128) continue;
                    solid++;
                    if (c.g > c.r + 40 && c.b > c.r + 34)
                        teal++;
                }
                res = solid > 0 && teal * 100 >= solid * 3;
                Object.Destroy(tmp);
            }
        }
        catch { }
        finally
        {
            RenderTexture.active = prev;
        }
        _ring[sid] = res;
        return res;
    }

    private static void Bar(SpriteRenderer sr, bool hot, bool light, ref byte tone, ref bool fresh)
    {
        if (sr == null) return;

        int id = sr.GetInstanceID();
        if (_plan.TryGetValue(id, out byte p))
        {
            if (p == 2 || p == 3)
                tone = 1;
            else if (p == 10 && tone == 0)
                tone = 2;
            Put(sr, p);
            return;
        }
        if (Look(sr) != 2) return;

        byte plan = BarPlan(sr, hot, light);
        if (plan == 2 || plan == 3)
            tone = 1;
        else if (plan == 10 && tone == 0)
            tone = 2;
        fresh = true;
        Plan(sr, plan);
    }

    private static byte Mark(SpriteRenderer sr, byte tone, bool create)
    {
        if (sr.sprite.name.IndexOf("check", StringComparison.OrdinalIgnoreCase) >= 0)
            return 6;
        if (tone == 2)
            return 6;
        return (byte)(tone == 1 || !create ? 5 : 6);
    }

    private static byte Dark(GameObject root, bool hot, bool create, bool light, ref bool fresh)
    {
        if (root == null) return 0;

        var all = root.GetComponentsInChildren<SpriteRenderer>(true);
        byte tone = 0;
        bool local = false;
        for (int i = 0; i < all.Length; i++)
            Bar(all[i], hot, light, ref tone, ref local);

        bool bars = tone != 0;
        for (int i = 0; i < all.Length; i++)
        {
            SpriteRenderer sr = all[i];
            if (sr == null || sr.sprite == null) continue;
            if (_plan.ContainsKey(sr.GetInstanceID())) continue;

            byte k = Look(sr);
            if (bars && sr.name.IndexOf("shine", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Plan(sr, 4);
                local = true;
            }
            else if (k >= 3 && k <= 5)
            {
                Plan(sr, Mark(sr, tone, create));
                local = true;
            }
            else if (k == 6)
            {
                Plan(sr, (byte)(tone == 2 ? 9 : 8));
                local = true;
            }
            else if (k == 1 && tone == 0 && LightPlate(sr))
                tone = 2;
        }

        if (local)
            fresh = true;
        if (local || _seen.Add(root.GetInstanceID()))
            Words(root, tone);
        return tone;
    }

    private static void Words(GameObject root, byte tone)
    {
        var words = root.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < words.Length; i++)
        {
            TMP_Text t = words[i];
            if (t == null) continue;

            Color c = t.color;
            float l = c.r * 0.299f + c.g * 0.587f + c.b * 0.114f;
            bool teal = IsTeal(Ui.C(c.r, c.g, c.b, 1f));
            if (tone == 2)
            {
                if (teal || l > 0.7f)
                    Txt(t, TextDark);
            }
            else if (teal || (tone == 1 && l < 0.45f))
                Txt(t, TextHi);
        }
    }

    private static Color Fit(Color c, byte tone)
    {
        float l = c.r * 0.299f + c.g * 0.587f + c.b * 0.114f;
        bool teal = IsTeal(Ui.C(c.r, c.g, c.b, 1f));
        if (tone == 2)
            return teal || l > 0.7f ? Ui.C(TextDark.r, TextDark.g, TextDark.b, c.a) : c;
        return teal || (tone == 1 && l < 0.45f) ? Ui.C(TextHi.r, TextHi.g, TextHi.b, c.a) : c;
    }

    private static void Teal(PassiveButton b, bool cards, bool create)
    {
        if (b == null) return;

        bool first = _seen.Add(b.GetInstanceID());
        bool fresh = false;
        byte tSelIn = Dark(b.selectedInactiveSprites, false, create, false, ref fresh);
        byte tIn = Dark(b.inactiveSprites, false, create, false, ref fresh);
        byte tOff = Dark(b.disabledSprites, false, create, false, ref fresh);
        byte tAct = Dark(b.activeSprites, true, create, false, ref fresh);
        byte tSel = Dark(b.selectedSprites, true, create, tSelIn == 2, ref fresh);
        Dark(b.onClickSprites, true, create, false, ref fresh);

        bool held = false;
        byte tHeld = 0;
        Bar(b.HeldButtonSprite, true, false, ref tHeld, ref held);

        var all = b.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < all.Length; i++)
        {
            SpriteRenderer sr = all[i];
            if (sr == null || sr.sprite == null) continue;
            if (_plan.ContainsKey(sr.GetInstanceID())) continue;

            byte k = Look(sr);
            if (k == 2)
            {
                byte any = 0;
                Bar(sr, false, false, ref any, ref fresh);
                if (any == 1)
                    tIn = 1;
            }
            else if (k >= 3 && k <= 5)
            {
                Plan(sr, Mark(sr, tIn, create));
                fresh = true;
            }
            else if (k == 6)
            {
                Plan(sr, 8);
                fresh = true;
            }
            else if (cards && Rebake(sr))
            {
                fresh = true;
                Transform up = sr.transform.parent;
                if (up != null)
                    Words(up.gameObject, 1);
            }
        }

        if (!fresh && !first) return;
        if (b.buttonText == null) return;

        b.inactiveTextColor = Fit(b.inactiveTextColor, tIn);
        b.activeTextColor = Fit(b.activeTextColor, tAct);
        byte tSelected = tSelIn == 2 ? (byte)2 : (byte)(tSel == 1 || tSelIn == 1 ? 1 : 0);
        b.selectedTextColor = Fit(b.selectedTextColor, tSelected);
        b.selectedInactiveTextColor = Fit(b.selectedInactiveTextColor, tSelected);
        if (tOff == 1)
            b.disabledTextColor = Ui.C(0.55f, 0.57f, 0.62f, 1f);

        byte shown = tIn;
        if (b.disabledSprites != null && b.disabledSprites.activeInHierarchy)
            shown = tOff;
        else if (b.selectedInactiveSprites != null && b.selectedInactiveSprites.activeInHierarchy)
            shown = tSelIn;
        else if (b.selectedSprites != null && b.selectedSprites.activeInHierarchy)
            shown = tSel;
        else if (b.activeSprites != null && b.activeSprites.activeInHierarchy)
            shown = tAct;

        TMP_Text bt = b.buttonText;
        Color now = bt.color;
        Color fit = Fit(now, shown);
        if (fit.r != now.r || fit.g != now.g || fit.b != now.b)
            Txt(bt, fit);

    }

    private static Color Div(Color target, Color tex, float a) => Ui.C(
        Ui.Clamp01(target.r / Ui.Max(0.10f, tex.r)),
        Ui.Clamp01(target.g / Ui.Max(0.10f, tex.g)),
        Ui.Clamp01(target.b / Ui.Max(0.10f, tex.b)),
        a);

    private static void Roll(ButtonRolloverHandler h)
    {
        if (h == null) return;
        SpriteRenderer t = h.Target;
        if (t == null || t.sprite == null || h.TargetText != null || h.TargetMesh != null) return;

        int id = h.GetInstanceID();
        Color tex = TexTint(t.sprite);
        if (!_rollWas.TryGetValue(id, out Color[] was))
        {
            Color o = h.OutColor;
            Color v = h.OverColor;
            bool outTeal = IsTeal(Ui.C(tex.r * o.r, tex.g * o.g, tex.b * o.b, 1f));
            bool overTeal = IsTeal(Ui.C(tex.r * v.r, tex.g * v.g, tex.b * v.b, 1f));
            if (!outTeal && !overTeal) return;

            was = new[] { o, v, Ui.C(outTeal ? 1f : 0f, 0f, 0f, 1f) };
            _rollWas[id] = was;
            _rollRef[id] = h;
        }

        if (was[2].r > 0.5f)
        {
            Color want = Div(Ink, tex, was[0].a);
            h.OutColor = want;
            h.OverColor = Div(InkHot, tex, was[1].a);
            Was(t);
            t.color = want;
        }
        else
            h.OverColor = Div(FieldOver, tex, was[1].a);
    }

    private static void Online(GameObject root)
    {
        if (root == null) return;
        var all = root.GetComponentsInChildren<PlayOnlineButtonSprite>(true);
        for (int i = 0; i < all.Length; i++)
        {
            PlayOnlineButtonSprite c = all[i];
            if (c == null) continue;
            Sprite col = c.Colored;
            if (col == null) continue;

            int id = c.GetInstanceID();
            if (!_onlineWas.ContainsKey(id))
            {
                _onlineWas[id] = col;
                _onlineRef[id] = c;
            }
            Sprite made = Baked(_onlineWas[id]);
            if (made == null) continue;

            c.Colored = made;
            SpriteRenderer sr = c.Sprite;
            if (sr != null && sr.sprite != made && sr.sprite == _onlineWas[id])
            {
                Replace(sr, made);
                PassiveButton pb = sr.GetComponentInParent<PassiveButton>();
                if (pb != null)
                    Words(pb.gameObject, 1);
            }
        }
    }

    private static bool Rebake(SpriteRenderer sr)
    {
        Sprite sp = sr.sprite;
        if (sp == null) return false;
        if (!_dim.TryGetValue(sr.GetInstanceID(), out Vector2 d)) return false;
        float asp = d.y > 0.001f ? d.x / d.y : 0f;
        if (d.x < 1f || d.y < 0.8f || asp < 0.5f || asp > 2.6f) return false;

        Sprite made = Baked(sp);
        if (made == null || sr.sprite == made) return false;
        Replace(sr, made);
        return true;
    }

    private static Sprite Baked(Sprite sp)
    {
        if (sp == null) return null;
        int sid = sp.GetInstanceID();
        if (_baked.TryGetValue(sid, out Sprite hit)) return hit;

        Sprite made = null;
        RenderTexture prev = RenderTexture.active;
        try
        {
            made = Band(sp);
        }
        catch { }
        finally
        {
            RenderTexture.active = prev;
        }
        _baked[sid] = made;
        return made;
    }

    private static Sprite Band(Sprite sp)
    {
        Texture2D src = sp.texture;
        if (src == null) return null;
        Rect r = sp.textureRect;
        int w = (int)r.m_Width;
        int h = (int)r.m_Height;
        if (w < 64 || h < 48 || w > 1400 || h > 1400) return null;

        RenderTexture.active = Atlas(src);
        var tmp = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tmp.ReadPixels(Ui.R(r.m_XMin, r.m_YMin, w, h), 0, 0);
        tmp.Apply();
        Color32[] px = tmp.GetPixels32();

        int limit = (int)(h * 0.6f);
        int best = 0;
        int bestAt = -1;
        int run = 0;
        int gap = 0;
        int at = 0;
        for (int y = 0; y < limit; y++)
        {
            int row = y * w;
            int solid = 0;
            int teal = 0;
            for (int x = 0; x < w; x++)
            {
                Color32 c = px[row + x];
                if (c.a < 128) continue;
                solid++;
                if (c.g > c.r + 40 && c.b > c.r + 34)
                    teal++;
            }

            if (solid >= w / 2 && teal * 2 >= solid)
            {
                if (run == 0)
                    at = y;
                run += gap + 1;
                gap = 0;
                if (run > best)
                {
                    best = run;
                    bestAt = at;
                }
            }
            else if (run > 0 && ++gap > 2)
            {
                run = 0;
                gap = 0;
            }
        }

        if (bestAt < 0 || best < Ui.Max(8, h / 16) || best * 100 > h * 45)
        {
            Object.Destroy(tmp);
            return null;
        }

        int first = Ui.Max(0, bestAt - 1);
        int last = Ui.Min(h, bestAt + best + 1);
        var bins = new int[64];
        float hi = 0f;
        for (int y = first; y < last; y++)
        {
            int row = y * w;
            for (int x = 0; x < w; x++)
            {
                Color32 c = px[row + x];
                if (!IsTealPx(c)) continue;
                float l = Lum(c);
                if (l > hi)
                    hi = l;
                bins[Ui.Min(63, (int)(l * 64f))]++;
            }
        }
        int top = 0;
        for (int i = 1; i < bins.Length; i++)
            if (bins[i] > bins[top])
                top = i;
        float lb = Ui.Max(0.15f, (top + 0.5f) / 64f);

        for (int y = first; y < last; y++)
        {
            int row = y * w;
            for (int x = 0; x < w; x++)
            {
                Color32 c = px[row + x];
                if (!IsTealPx(c)) continue;

                float l = Lum(c);
                float rr, gg, bb;
                if (l <= lb)
                {
                    float k = l / lb;
                    rr = Ink.r * k;
                    gg = Ink.g * k;
                    bb = Ink.b * k;
                }
                else
                {
                    float k = hi > lb + 0.02f ? Ui.Clamp01((l - lb) / (hi - lb)) : 0f;
                    rr = Ink.r + (InkShine.r - Ink.r) * k;
                    gg = Ink.g + (InkShine.g - Ink.g) * k;
                    bb = Ink.b + (InkShine.b - Ink.b) * k;
                }
                px[row + x] = new Color32(ToByte(rr), ToByte(gg), ToByte(bb), c.a);
            }
        }

        return Rebuilt(sp, tmp, px, w, h, " ink");
    }

    private static Sprite Rebuilt(Sprite sp, Texture2D tmp, Color32[] px, int w, int h, string tag)
    {
        tmp.SetPixels32(px);
        tmp.Apply();
        tmp.filterMode = sp.texture.filterMode;
        tmp.wrapMode = TextureWrapMode.Clamp;
        tmp.hideFlags = HideFlags.HideAndDontSave;

        Vector2 pv = sp.pivot;
        Sprite ns = Sprite.Create(tmp, Ui.R(0f, 0f, w, h), Ui.V(pv.x / w, pv.y / h), sp.pixelsPerUnit, 0u, SpriteMeshType.FullRect, sp.border);
        ns.name = sp.name + tag;
        ns.hideFlags = HideFlags.HideAndDontSave;
        return ns;
    }

    private static Sprite Tinted(Sprite sp, Color target, byte variant)
    {
        if (sp == null) return null;
        long key = ((long)sp.GetInstanceID() << 4) | variant;
        if (_tinted.TryGetValue(key, out Sprite hit)) return hit;

        Sprite made = null;
        RenderTexture prev = RenderTexture.active;
        try
        {
            made = Recolor(sp, target);
        }
        catch { }
        finally
        {
            RenderTexture.active = prev;
        }
        _tinted[key] = made;
        return made;
    }

    private static Sprite Recolor(Sprite sp, Color target)
    {
        Texture2D src = sp.texture;
        if (src == null) return null;
        Rect r = sp.textureRect;
        int w = (int)r.m_Width;
        int h = (int)r.m_Height;
        if (w < 4 || h < 4 || w > 1400 || h > 1400) return null;

        RenderTexture.active = Atlas(src);
        var tmp = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tmp.ReadPixels(Ui.R(r.m_XMin, r.m_YMin, w, h), 0, 0);
        tmp.Apply();
        Color32[] px = tmp.GetPixels32();

        var bins = new int[64];
        int n = 0;
        for (int i = 0; i < px.Length; i++)
        {
            if (!IsTealPx(px[i])) continue;
            bins[Ui.Min(63, (int)(Lum(px[i]) * 64f))]++;
            n++;
        }
        if (n == 0)
        {
            Object.Destroy(tmp);
            return null;
        }

        int top = 0;
        for (int i = 1; i < bins.Length; i++)
            if (bins[i] > bins[top])
                top = i;
        float lref = Ui.Max(0.2f, (top + 0.5f) / 64f);
        for (int i = 0; i < px.Length; i++)
        {
            Color32 c = px[i];
            if (!IsTealPx(c)) continue;

            float l = Lum(c);
            float rr, gg, bb;
            if (l <= lref)
            {
                float k = Ui.Max(0.35f, l / lref);
                rr = target.r * k;
                gg = target.g * k;
                bb = target.b * k;
            }
            else
            {
                float m = Ui.Clamp01((l - lref) / Ui.Max(0.05f, 1f - lref));
                rr = target.r + (1f - target.r) * m;
                gg = target.g + (1f - target.g) * m;
                bb = target.b + (1f - target.b) * m;
            }
            px[i] = new Color32(ToByte(rr), ToByte(gg), ToByte(bb), c.a);
        }
        return Rebuilt(sp, tmp, px, w, h, " tint");
    }

    private static RenderTexture Atlas(Texture2D src)
    {
        int tid = src.GetInstanceID();
        if (_atlas.TryGetValue(tid, out RenderTexture rt) && rt != null)
            return rt;
        rt = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32);
        Graphics.Blit(src, rt);
        _atlas[tid] = rt;
        return rt;
    }

    private static void DropAtlas()
    {
        if (_atlas.Count == 0) return;
        foreach (KeyValuePair<int, RenderTexture> kv in _atlas)
            if (kv.Value != null)
                RenderTexture.ReleaseTemporary(kv.Value);
        _atlas.Clear();
    }

    private static void Undo()
    {
        foreach (KeyValuePair<int, ButtonRolloverHandler> kv in _rollRef)
            if (kv.Value != null && _rollWas.TryGetValue(kv.Key, out Color[] w))
            {
                kv.Value.OutColor = w[0];
                kv.Value.OverColor = w[1];
            }
        foreach (KeyValuePair<int, PlayOnlineButtonSprite> kv in _onlineRef)
            if (kv.Value != null && _onlineWas.TryGetValue(kv.Key, out Sprite s))
                kv.Value.Colored = s;
        foreach (KeyValuePair<int, SpriteRenderer> kv in _swapRef)
            if (kv.Value != null && _swapWas.TryGetValue(kv.Key, out Sprite s))
                kv.Value.sprite = s;
        foreach (KeyValuePair<int, Sprite> kv in _baked)
            Free(kv.Value);
        foreach (KeyValuePair<long, Sprite> kv in _tinted)
            Free(kv.Value);
        _rollRef.Clear();
        _rollWas.Clear();
        _onlineRef.Clear();
        _onlineWas.Clear();
        _swapRef.Clear();
        _swapWas.Clear();
        _baked.Clear();
        _tinted.Clear();
        _plan.Clear();
        _kind.Clear();
        _dim.Clear();
        _ring.Clear();
        _last.Clear();
        _held.Clear();
        _heldIds.Clear();
        _seen.Clear();
        DropAtlas();
    }

    private static void Free(Sprite s)
    {
        if (s == null) return;
        Texture2D tex = s.texture;
        Object.Destroy(s);
        if (tex != null)
            Object.Destroy(tex);
    }

    private static int _browser;

    internal static void Browser(FindAGameManager f)
    {
        if (f == null) return;
        if (NocturneConfig.Vanilla || !NocturneConfig.MainMenuArt.Value) return;

        int id = f.GetInstanceID();
        if (_browser != id)
        {
            _browser = id;
            _menuPasses = 0;
            Undo();
            _wasCol.Clear();
            _srRef.Clear();
            _wasTxt.Clear();
            _txtRef.Clear();
        }
        _menuPasses++;

        try
        {
            Pick(f.refreshButton);
            Pick(f.clearFilterButton);
            UiElement back = f.BackButton;
            if (back != null)
                Pick(back.GetComponent<PassiveButton>());
        }
        catch { }
        DropAtlas();
    }

    private static void Pick(PassiveButton b)
    {
        if (b == null) return;

        ButtonRolloverHandler h = b.GetComponent<ButtonRolloverHandler>();
        if (h != null)
            Roll(h);
        Teal(b, false, false);
    }

    private static void Top()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        var all = Object.FindObjectsOfType<SpriteRenderer>();
        for (int i = 0; i < all.Length; i++)
        {
            SpriteRenderer sr = all[i];
            if (sr == null || !Face(sr))
                continue;

            Component cp = sr;
            if (cp.GetComponentInParent<PoolablePlayer>() != null)
                continue;
            if (cp.GetComponentInParent<PlayerControl>() != null) continue;

            Vector3 vp = cam.WorldToViewportPoint(sr.bounds.center);
            if (vp.z <= 0f || vp.y < 0.88f) continue;

            Vector3 sz = sr.bounds.size;
            if (sz.x < 0.5f || sz.y < 0.05f) continue;
            Dye(sr, Ink);
        }

        var texts = Object.FindObjectsOfType<TMP_Text>();
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text t = texts[i];
            if (t == null) continue;
            Vector3 vp = cam.WorldToViewportPoint(t.transform.position);
            if (vp.z <= 0f || vp.y < 0.88f) continue;
            Txt(t, Ui.White);
        }
    }

    private static void Btn(PassiveButton b)
    {
        if (b == null) return;

        Group(b.inactiveSprites, Ink);
        Group(b.selectedInactiveSprites, Ink);
        Group(b.disabledSprites, Ink);
        Group(b.activeSprites, InkHot);
        Group(b.selectedSprites, InkHot);
        Group(b.onClickSprites, InkHot);
        Dye(b.HeldButtonSprite, InkHot);

        b.activeTextColor = Ui.White;
        b.inactiveTextColor = Ui.C(0.88f, 0.90f, 0.94f, 1f);
        b.selectedTextColor = Ui.White;
        b.selectedInactiveTextColor = Ui.White;
        b.disabledTextColor = Ui.C(0.55f, 0.57f, 0.62f, 1f);
        if (b.buttonText != null) Txt(b.buttonText, Ui.White);
    }

    private static void Group(GameObject root, Color c)
    {
        if (root == null) return;
        var all = root.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < all.Length; i++)
            Dye(all[i], c);
    }

    private static void Dye(SpriteRenderer sr, Color c, bool exact = false)
    {
        if (sr == null || sr.sprite == null) return;
        if (!exact && !Face(sr)) return;
        Color o = Was(sr);
        Color t = !exact && Shiny(o) ? InkShine : c;
        Color tex = TexTint(sr.sprite);
        sr.color = Ui.C(
            Ui.Clamp01(t.r / Ui.Max(0.10f, tex.r)),
            Ui.Clamp01(t.g / Ui.Max(0.10f, tex.g)),
            Ui.Clamp01(t.b / Ui.Max(0.10f, tex.b)),
            o.a);
    }

    private static bool Shiny(Color c)
    {
        float mx = Ui.Max(c.r, Ui.Max(c.g, c.b));
        float mn = Ui.Min(c.r, Ui.Min(c.g, c.b));
        return mx > 0.001f && (mx - mn) / mx > 0.15f && (c.r + c.g + c.b) / 3f > 0.7f;
    }

    private static readonly Dictionary<int, Color> _tex = new Dictionary<int, Color>();

    private static Color TexTint(Sprite sp)
    {
        if (sp == null || sp.texture == null)
            return Ui.White;
        int id = sp.GetInstanceID();
        if (_tex.TryGetValue(id, out Color hit)) return hit;

        Color res = Ui.White;
        RenderTexture prev = RenderTexture.active;
        try
        {
            Texture2D src = sp.texture;
            int tid = src.GetInstanceID();
            if (!_atlas.TryGetValue(tid, out RenderTexture rt) || rt == null)
            {
                rt = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32);
                Graphics.Blit(src, rt);
                _atlas[tid] = rt;
            }
            RenderTexture.active = rt;

            if (_one == null)
                _one = new Texture2D(1, 1, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            Rect r = sp.textureRect;
            _one.ReadPixels(Ui.R(r.m_XMin + r.m_Width * 0.5f, r.m_YMin + r.m_Height * 0.5f, 1f, 1f), 0, 0);
            _one.Apply();

            Color px = _one.GetPixel(0, 0);
            if (px.a > 0.4f)
                res = Ui.C(Ui.Max(0.12f, px.r), Ui.Max(0.12f, px.g), Ui.Max(0.12f, px.b), 1f);
        }
        catch { }
        finally
        {
            RenderTexture.active = prev;
        }

        _tex[id] = res;
        return res;
    }

    private static bool Face(SpriteRenderer sr)
    {
        if (sr.sprite == null)
            return false;
        string n = sr.name;
        return n.IndexOf("icon", StringComparison.OrdinalIgnoreCase) < 0
            && n.IndexOf("crewmate", StringComparison.OrdinalIgnoreCase) < 0;
    }


    private static Color Was(SpriteRenderer sr)
    {
        int id = sr.GetInstanceID();
        if (!_wasCol.TryGetValue(id, out Color c))
        {
            c = sr.color;
            _wasCol[id] = c;
            _srRef[id] = sr;
        }
        return c;
    }

    private static void Txt(TMP_Text t, Color c)
    {
        int id = t.GetInstanceID();
        if (!_wasTxt.ContainsKey(id))
        {
            _wasTxt[id] = t.color;
            _txtRef[id] = t;
        }
        t.color = c;
    }

    private static void Unpaint()
    {
        foreach (KeyValuePair<int, SpriteRenderer> kv in _srRef)
            if (kv.Value != null && _wasCol.TryGetValue(kv.Key, out Color c)) kv.Value.color = c;
        foreach (KeyValuePair<int, TMP_Text> kv in _txtRef)
            if (kv.Value != null && _wasTxt.TryGetValue(kv.Key, out Color c)) kv.Value.color = c;
        _wasCol.Clear();
        _srRef.Clear();
        _wasTxt.Clear();
        _txtRef.Clear();
    }

    private static void Bind(MainMenuManager m)
    {
        int id = m.GetInstanceID();
        if (_bound == id) return;
        _bound = id;
        _tops = 0;
        _topAt = 0f;
        _menuPasses = 0;
        Undo();
        _offGo.Clear();
        _offRen.Clear();
        _wasCol.Clear();
        _srRef.Clear();
        _wasTxt.Clear();
        _txtRef.Clear();

        GameObject root = m.gameObject;
        _bgTex = Kid(root, "BackgroundTexture");
        _shine = Kid(root, "WindowShine");

        GameObject l = Kid(root, "LeftPanel"), r = Kid(root, "RightPanel");
        _left = l != null ? l.GetComponent<Renderer>() : null;
        _right = r != null ? r.GetComponent<Renderer>() : null;
        GameObject lm = l != null ? Kid(l, "MaskedBlackScreen") : null;
        GameObject rm = r != null ? Kid(r, "MaskedBlackScreen") : null;
        _leftMask = lm != null ? lm.GetComponent<Renderer>() : null;
        _rightMask = rm != null ? rm.GetComponent<Renderer>() : null;

        try
        {
            _stars = GameObject.Find("BackgroundStarField");
        }
        catch
        {
            _stars = null;
        }

        try
        {
            SpriteRenderer t = m.screenTint;
            if (t != null)
            {
                _tint = t.transform;
                _tintAt = _tint.localPosition;
                _tint.localPosition = _tintAt + Ui.V3(1000f, 0f, 0f);
                Off(t);
            }
        }
        catch { }
    }

    private static void Hide()
    {
        if (_stars == null)
        {
            _stars = GameObject.Find("BackgroundStarField");
        }
        Kill(_bgTex);
        Kill(_shine);
        Kill(_stars);
        Off(_left);
        Off(_leftMask);
        Off(_right);
        Off(_rightMask);
    }

    internal static void Repeat()
    {
        Hold();
        if (!_live) return;
        for (int i = 0; i < _offGo.Count; i++)
            if (_offGo[i] != null && _offGo[i].activeSelf) _offGo[i].SetActive(false);
        for (int i = 0; i < _offRen.Count; i++)
            if (_offRen[i] != null && _offRen[i].enabled) _offRen[i].enabled = false;
        if (_back != null)
            Settle();
    }

    internal static void Sleep()
    {
        if (!_isVideo) return;

        if (_vidReady && _rt != null && _rt.IsCreated())
        {
            if (_snap == null || _snap.width != _rt.width || _snap.height != _rt.height)
            {
                DropSnap();
                _snap = new RenderTexture(_rt.width, _rt.height, 0) { hideFlags = HideFlags.HideAndDontSave };
                _snap.Create();
            }
            RenderTexture prev = RenderTexture.active;
            Graphics.Blit(_rt, _snap);
            RenderTexture.active = prev;
        }

        StopVideo();
        _art = null;
        _isVideo = false;
        if (_back != null)
        {
            Object.Destroy(_back);
            _back = null;
        }
        _backSr = null;
        _bound = 0;
        _live = false;
    }

    internal static void Restore()
    {
        Unpaint();
        Undo();
        _menuPasses = 0;
        for (int i = 0; i < _offGo.Count; i++)
            if (_offGo[i] != null)
                _offGo[i].SetActive(true);
        for (int i = 0; i < _offRen.Count; i++)
            if (_offRen[i] != null)
                _offRen[i].enabled = true;
        _offGo.Clear();
        _offRen.Clear();

        if (_tint != null)
        {
            _tint.localPosition = _tintAt;
            _tint = null;
        }
        if (_back != null)
        {
            Object.Destroy(_back);
            _back = null;
        }
        _backSr = null;
        StopVideo();
        DropSnap();
        if (_isVideo)
        {
            _art = null;
            _isVideo = false;
        }

        _bound = 0;
        _live = false;
    }

    private static void Backdrop()
    {
        if (_art == null)
        {
            if (_failed) return;

            string clip = FindVideo();
            if (clip != null && StartVideo(clip))
            {
                _art = Canvas16x9();
                _vidAspect = 16f / 9f;
                _isVideo = true;
            }
            else
                _art = Load();

            if (_art == null)
            {
                _failed = true;
                return;
            }
        }

        if (_isVideo)
        {
            CheckVideo();
            if (_art == null)
                return;
        }

        if (_back == null)
        {
            _back = new GameObject("NocturneMainArt");
            _backSr = _back.AddComponent<SpriteRenderer>();
            _backSr.sprite = _art;
            _backSr.sortingOrder = -9999;
            _snapOn = _isVideo && !_vidReady && _snap != null;
            BindVideoTexture(_backSr, _snapOn ? _snap : _rt);
            _backFade = _isVideo && !_vidReady && !_snapOn ? 0f : 1f;
            _backSr.color = Ui.C(1f, 1f, 1f, _backFade);
        }
        Fit();
    }

    private static void CheckVideo()
    {
        if (_vid != null && _rt != null && _rt.IsCreated() && Ui.Abs(_rt.width - Ui.Max(640, Screen.width)) <= 64)
            return;

        string clip = FindVideo();
        StopVideo();
        if (clip == null || !StartVideo(clip))
        {
            _vidFailed = true;
            Reload();
        }
        else if (_backSr != null)
        {
            _snapOn = false;
            BindVideoTexture(_backSr, _rt);
            _backFade = 0f;
            _backSr.color = Ui.C(1f, 1f, 1f, 0f);
        }
    }

    private static void DropSnap()
    {
        if (_snap != null)
            Object.Destroy(_snap);
        _snap = null;
        _snapOn = false;
    }

    private static void Settle()
    {
        if (_isVideo && !_vidReady && _vid != null && _vid.isPlaying && _vid.time > 0.05d)
        {
            _vidReady = true;
            if (_snapOn)
            {
                _snapOn = false;
                BindVideoTexture(_backSr, _rt);
            }
        }

        if (_backFade < 1f && _backSr != null && (_vidReady || !_isVideo))
        {
            _backFade = Ui.Min(1f, _backFade + Time.unscaledDeltaTime * 3f);
            _backSr.color = Ui.C(1f, 1f, 1f, _backFade);
        }

        Camera cam = Camera.main;
        if (cam == null)
            return;
        float a = Screen.width / Ui.Max(1f, Screen.height);
        if (cam.orthographicSize != _fitH || a != _fitA)
            Fit();
    }

    private static void BindVideoTexture(SpriteRenderer sr, Texture tex)
    {
        if (tex == null || sr == null)
            return;
        try
        {
            var mpb = new MaterialPropertyBlock();
            sr.GetPropertyBlock(mpb);
            mpb.SetTexture("_MainTex", tex);
            sr.SetPropertyBlock(mpb);
        }
        catch (Exception e)
        {
            NocturnePlugin.Logger?.LogWarning("[MainArt] видео-текстура: " + e.Message);
        }
    }

    private static Sprite _canvas;

    private static Sprite Canvas16x9()
    {
        if (_canvas != null)
            return _canvas;

        var tex = new Texture2D(16, 9, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave,
            wrapMode = TextureWrapMode.Clamp
        };

        var px = new Color32[16 * 9];
        for (int i = 0; i < px.Length; i++)
            px[i] = new Color32(0, 0, 0, 255);
        tex.SetPixels32(px);
        tex.Apply();
        Object.DontDestroyOnLoad(tex);

        Sprite sp = Sprite.Create(tex, Ui.R(0f, 0f, 16f, 9f), Ui.V(0.5f, 0.5f));
        Object.DontDestroyOnLoad(sp);
        _canvas = sp;
        return sp;
    }

    private static void Fit()
    {
        Camera cam = Camera.main;
        if (cam == null || _back == null || _art == null)
            return;
        float scr = Screen.width / Ui.Max(1f, Screen.height);
        float h = cam.orthographicSize * 2f;
        float w = h * scr;
        _fitH = cam.orthographicSize;
        _fitA = scr;
        if (_vid != null && !_vidReady && Time.unscaledTime - _vidAt > 10f)
        {
            NocturnePlugin.Logger?.LogWarning("[MainArt] видео не запустилось, беру картинку");
            _vidFailed = true;
            Reload();
            return;
        }

        Vector2 sz = _art.bounds.size;
        if (_vidAspect > 0f && _vid != null && _vid.isPrepared && _vid.height > 0u)
        {
            float a = _vid.width / (float)_vid.height;
            if (Ui.Abs(a - _vidAspect) > 0.01f)
            {
                _vidAspect = a;
                sz = Ui.V(sz.y * a, sz.y);
            }
        }

        float k = Ui.Max(w / Ui.Max(0.001f, sz.x), h / Ui.Max(0.001f, sz.y));
        _back.transform.localPosition = Ui.V3(0f, 0f, 20f);
        _back.transform.localScale = Ui.V3(k, k, 1f);
    }

    private static readonly string[] Exts = { ".png", ".jpg", ".jpeg" };
    private static readonly string[] VidExts = { ".mp4", ".webm", ".m4v" };

    private static UnityEngine.Video.VideoPlayer _vid;
    private static RenderTexture _rt;
    private static float _vidAspect;
    private static float _vidAt;
    private static bool _vidFailed;
    private static bool _isVideo;
    private static bool _vidReady;
    private static bool _snapOn;
    private static RenderTexture _snap;
    private static float _backFade;
    private static float _fitH;
    private static float _fitA;
    private static SpriteRenderer _backSr;

    private static string FindVideo()
    {
        if (_vidFailed)
            return null;

        try
        {
            string dir = SkinDir;
            if (!Directory.Exists(dir)) return null;

            foreach (string f in Directory.GetFiles(dir))
            {
                string ext = Path.GetExtension(f).ToLowerInvariant();
                for (int i = 0; i < VidExts.Length; i++)
                    if (ext == VidExts[i]) return f;
            }
        }
        catch { }
        return null;
    }

    private static bool StartVideo(string file)
    {
        if (_vid != null)
            StopVideo();
        try
        {
            int rw = Ui.Max(640, Screen.width);
            int rh = Ui.Max(360, Screen.height);
            _rt = new RenderTexture(rw, rh, 0) { hideFlags = HideFlags.HideAndDontSave };
            _rt.Create();
            Graphics.Blit(Texture2D.blackTexture, _rt);

            var go = new GameObject("NocturneMainVideo");
            Object.DontDestroyOnLoad(go);
            _vid = go.AddComponent<UnityEngine.Video.VideoPlayer>();
            _vid.source = UnityEngine.Video.VideoSource.Url;
            _vid.url = "file:///" + file.Replace('\\', '/');
            _vid.renderMode = UnityEngine.Video.VideoRenderMode.RenderTexture;
            _vid.targetTexture = _rt;
            _vid.isLooping = true;
            _vid.playOnAwake = false;
            _vid.waitForFirstFrame = false;
            _vid.audioOutputMode = UnityEngine.Video.VideoAudioOutputMode.None;
            _vid.Play();
            _vidAt = Time.unscaledTime;
            _vidReady = false;
            return true;
        }
        catch (Exception e)
        {
            NocturnePlugin.Logger?.LogWarning("[MainArt] видео: " + e.Message);
            StopVideo();
            return false;
        }
    }

    private static void StopVideo()
    {
        GameObject go = _vid != null ? _vid.gameObject : null;
        RenderTexture rt = _rt;
        _vid = null;
        _rt = null;
        if (go != null)
            Object.Destroy(go);
        if (rt != null)
            Object.Destroy(rt);
        _vidAspect = 0f;
        _vidReady = false;
    }

    internal static string SkinDir => Path.Combine(BepInEx.Paths.GameRootPath, "Nocturne", "Wallpaper");

    internal static void ReloadManual()
    {
        _vidFailed = false;
        Reload();
    }

    internal static void Reload()
    {
        _art = null;
        _failed = false;
        _isVideo = false;
        StopVideo();
        DropSnap();
        if (_back != null)
        {
            Object.Destroy(_back);
            _back = null;
        }
        _backSr = null;
    }

    private static Sprite Load()
    {
        Sprite own = LoadOwn();
        if (own != null)
            return own;

        try
        {
            using Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream(Res);
            if (s == null)
            {
                NocturnePlugin.Logger?.LogWarning("[MainArt] нет ресурса " + Res);
                return null;
            }
            using var ms = new MemoryStream();
            s.CopyTo(ms);
            return FromBytes(ms.ToArray());
        }
        catch (Exception e)
        {
            NocturnePlugin.Logger?.LogWarning("[MainArt] " + e.Message);
            return null;
        }
    }

    private static Sprite LoadOwn()
    {
        try
        {
            string dir = SkinDir;
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
                return null;
            }

            foreach (string f in Directory.GetFiles(dir))
            {
                string ext = Path.GetExtension(f).ToLowerInvariant();
                for (int i = 0; i < Exts.Length; i++)
                    if (ext == Exts[i]) return FromBytes(File.ReadAllBytes(f));
            }
        }
        catch (Exception e)
        {
            NocturnePlugin.Logger?.LogWarning("[MainArt] своя картинка: " + e.Message);
        }
        return null;
    }

    private static Sprite FromBytes(byte[] data)
    {
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        if (!ImageConversion.LoadImage(tex, data))
        {
            Object.Destroy(tex);
            return null;
        }
        Object.DontDestroyOnLoad(tex);

        Sprite sp = Sprite.Create(tex, Ui.R(0f, 0f, tex.width, tex.height), Ui.V(0.5f, 0.5f));
        Object.DontDestroyOnLoad(sp);
        return sp;
    }

    private static void Kill(GameObject go)
    {
        if (go == null || !go.activeSelf)
            return;
        go.SetActive(false);
        if (!_offGo.Contains(go)) _offGo.Add(go);
    }

    private static void Off(Renderer r)
    {
        if (r == null || !r.enabled) return;
        r.enabled = false;
        if (!_offRen.Contains(r))
            _offRen.Add(r);
    }

    private static GameObject Kid(GameObject root, string name)
    {
        if (root == null) return null;
        var all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t != null && string.Equals(t.name, name, StringComparison.OrdinalIgnoreCase))
                return t.gameObject;
        }
        return null;
    }
}

public sealed class NocturneMainArtDriver : MonoBehaviour
{
    internal static MainMenuManager Live;

    private MainMenuManager _m;
    private float _find;
    private float _at;

    private static bool InMenu()
    {
        if (LobbyBehaviour.Instance != null) return false;
        if (ShipStatus.Instance != null) return false;
        return true;
    }

    private bool Alive()
    {
        if (_m == null) return false;
        GameObject go = _m.gameObject;
        return go != null && go.activeInHierarchy;
    }

    public void Update()
    {
        if (!InMenu())
        {
            _m = null;
            NocturneMainArt.Sleep();
            return;
        }

        if (!Alive())
        {
            _m = null;
            if (FindAGameManager.InstanceExists && Time.unscaledTime >= _at)
            {
                _at = Time.unscaledTime + 1.5f;
                NocturneMainArt.Browser(FindAGameManager.Instance);
            }
            if (Time.unscaledTime < _find) return;
            _find = Time.unscaledTime + 2f;
            _m = Object.FindObjectOfType<MainMenuManager>();
            if (_m == null) return;
            Live = _m;
            _at = 0f;
        }

        if (Time.unscaledTime < _at) return;
        _at = Time.unscaledTime + 1.5f;
        NocturneMainArt.Apply(_m);
        MenuFloaters.Sync();
    }

    public void LateUpdate()
    {
        if (InMenu())
            NocturneMainArt.Repeat();
    }
}

[HarmonyPatch(typeof(MainMenuManager), nameof(MainMenuManager.Start))]
internal static class NocturneMainArtStartPatch
{
    public static void Postfix(MainMenuManager __instance)
    {
        NocturneMainArtDriver.Live = __instance;
        NocturneMainArt.Apply(__instance);
    }
}

[HarmonyPatch(typeof(FindAGameManager), nameof(FindAGameManager.Start))]
internal static class NocturneBrowserArtPatch
{
    public static void Postfix(FindAGameManager __instance)
    {
        NocturneMainArt.Browser(__instance);
    }
}
