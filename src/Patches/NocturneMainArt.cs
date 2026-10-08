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
        Top();
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

    private static void Dye(SpriteRenderer sr, Color c)
    {
        if (sr == null || !Face(sr)) return;
        Color o = Was(sr);
        Color t = Shiny(o) ? InkShine : c;
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
        RenderTexture rt = null;
        RenderTexture prev = RenderTexture.active;
        Texture2D one = null;
        try
        {
            Texture2D src = sp.texture;
            Rect r = sp.textureRect;
            rt = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(src, rt);
            RenderTexture.active = rt;

            one = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            one.ReadPixels(Ui.R(r.m_XMin + r.m_Width * 0.5f, r.m_YMin + r.m_Height * 0.5f, 1f, 1f), 0, 0);
            one.Apply();

            Color px = one.GetPixel(0, 0);
            if (px.a > 0.4f)
                res = Ui.C(Ui.Max(0.12f, px.r), Ui.Max(0.12f, px.g), Ui.Max(0.12f, px.b), 1f);
        }
        catch { }
        finally
        {
            RenderTexture.active = prev;
            if (rt != null) RenderTexture.ReleaseTemporary(rt);
            if (one != null) Object.Destroy(one);
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
        if (!_live) return;
        for (int i = 0; i < _offGo.Count; i++)
            if (_offGo[i] != null && _offGo[i].activeSelf) _offGo[i].SetActive(false);
        for (int i = 0; i < _offRen.Count; i++)
            if (_offRen[i] != null && _offRen[i].enabled) _offRen[i].enabled = false;
    }

    internal static void Sleep()
    {
        if (_vid == null) return;

        StopVideo();
        if (_back != null)
        {
            Object.Destroy(_back);
            _back = null;
        }
        _art = null;
        _bound = 0;
        _live = false;
    }

    internal static void Restore()
    {
        Unpaint();
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
        StopVideo();

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
            }
            else
                _art = Load();

            if (_art == null)
            {
                _failed = true;
                return;
            }
        }

        if (_back == null)
        {
            _back = new GameObject("NocturneMainArt");
            SpriteRenderer sr = _back.AddComponent<SpriteRenderer>();
            sr.sprite = _art;
            sr.sortingOrder = -9999;
            BindVideoTexture(sr);
        }
        Fit();
    }

    private static void BindVideoTexture(SpriteRenderer sr)
    {
        if (_rt == null || sr == null)
            return;
        try
        {
            var mpb = new MaterialPropertyBlock();
            sr.GetPropertyBlock(mpb);
            mpb.SetTexture("_MainTex", _rt);
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
        float h = cam.orthographicSize * 2f;
        float w = h * (Screen.width / Ui.Max(1f, Screen.height));
        if (_vid != null && !_vid.isPlaying && Time.unscaledTime - _vidAt > 6f)
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
        try
        {
            int rw = Ui.Max(640, Screen.width);
            int rh = Ui.Max(360, Screen.height);
            _rt = new RenderTexture(rw, rh, 0) { hideFlags = HideFlags.HideAndDontSave };
            _rt.Create();

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
        try
        {
            if (_vid != null)
            {
                _vid.Stop();
                Object.Destroy(_vid.gameObject);
                _vid = null;
            }
            if (_rt != null)
            {
                _rt.Release();
                Object.Destroy(_rt);
                _rt = null;
            }
        }
        catch { }
        _vidAspect = 0f;
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
        StopVideo();
        if (_back != null)
        {
            Object.Destroy(_back);
            _back = null;
        }
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
