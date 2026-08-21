using System.Collections.Generic;
using UnityEngine;

namespace Nocturne;

public sealed class NocturneSnow : MonoBehaviour
{
    private sealed class Flake
    {
        public Transform Tr;
        public float Fall;
        public float Drift;
        public float Phase;
        public float Spin;
        public float Rot;
    }

    private static readonly List<Flake> _live = new List<Flake>();
    private static Sprite _dot;
    private static Sprite _quad;
    private int _type;

    public void Update()
    {
        int type = Mathf.Clamp(NocturneConfig.LobbyWeather.Value, 0, 4);
        bool want = type > 0 && LobbyBehaviour.Instance != null && Camera.main != null;
        if (!want)
        {
            if (_type != 0) { _type = 0; Clear(); }
            return;
        }

        Camera cam = Camera.main;
        if (cam == null) return;

        if (_type != type) { _type = type; Clear(); }

        int n = Mathf.Clamp(NocturneConfig.LobbySnowAmount.Value, 10, 400);
        if (_live.Count != n) Fit(cam, n, type);
        if (_live.Count == 0) return;

        float h = cam.orthographicSize;
        float w = h * cam.aspect;
        Vector3 c = cam.transform.position;
        float top = c.y + h + 0.5f;
        float bot = c.y - h - 0.5f;
        float dt = Time.deltaTime;
        float t = Time.time;

        for (int i = 0; i < _live.Count; i++)
        {
            Flake f = _live[i];
            if (f.Tr == null) continue;

            Vector3 p = f.Tr.position;
            p.y -= f.Fall * dt;
            if (f.Drift > 0f) p.x += Mathf.Sin(t * f.Spin + f.Phase) * f.Drift * dt;

            if (p.y < bot || p.x < c.x - w - 1f || p.x > c.x + w + 1f)
            {
                p.x = c.x + Random.Range(-w, w);
                p.y = top;
            }
            p.z = c.z + 5f;
            f.Tr.position = p;

            if (f.Rot != 0f) f.Tr.Rotate(0f, 0f, f.Rot * dt);
        }
    }

    private void Fit(Camera cam, int n, int type)
    {
        while (_live.Count > n)
        {
            int last = _live.Count - 1;
            if (_live[last]?.Tr != null) Destroy(_live[last].Tr.gameObject);
            _live.RemoveAt(last);
        }
        if (_live.Count >= n) return;

        Sprite s = type == 4 ? Quad() : Dot();
        if (s == null) return;

        float h = cam.orthographicSize;
        float w = h * cam.aspect;
        Vector3 c = cam.transform.position;

        while (_live.Count < n)
        {
            var go = new GameObject("nocturne_weather");
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(c.x + Random.Range(-w, w), c.y + Random.Range(-h, h), c.z + 5f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = s;

            Flake f = new Flake { Tr = go.transform, Phase = Random.Range(0f, 10f), Spin = Random.Range(0.6f, 1.8f) };
            Shape(go.transform, sr, f, type);
            _live.Add(f);
        }
    }

    private static void Shape(Transform tr, SpriteRenderer sr, Flake f, int type)
    {
        switch (type)
        {
            case 2:
            {
                float sc = Random.Range(0.03f, 0.06f);
                tr.localScale = new Vector3(sc * 0.4f, sc * 6f, 1f);
                sr.color = new Color(0.62f, 0.78f, 1f, Random.Range(0.35f, 0.7f));
                f.Fall = Random.Range(7f, 12f);
                f.Drift = 0f;
                break;
            }
            case 3:
            {
                float sc = Random.Range(0.08f, 0.16f);
                tr.localScale = new Vector3(sc * 1.5f, sc * 0.75f, 1f);
                sr.color = Leaf();
                f.Fall = Random.Range(0.5f, 1.3f);
                f.Drift = Random.Range(1.1f, 2.2f);
                f.Rot = Random.Range(-120f, 120f);
                break;
            }
            case 4:
            {
                float sc = Random.Range(0.05f, 0.1f);
                tr.localScale = new Vector3(sc, sc * Random.Range(0.4f, 1f), 1f);
                sr.color = Color.HSVToRGB(Random.value, 0.85f, 1f);
                f.Fall = Random.Range(1.2f, 2.8f);
                f.Drift = Random.Range(0.8f, 1.8f);
                f.Rot = Random.Range(-260f, 260f);
                break;
            }
            default:
            {
                float sc = Random.Range(0.035f, 0.11f);
                tr.localScale = new Vector3(sc, sc, 1f);
                sr.color = new Color(1f, 1f, 1f, Random.Range(0.45f, 0.95f));
                f.Fall = Random.Range(0.7f, 2.1f);
                f.Drift = Random.Range(0.2f, 0.9f);
                break;
            }
        }
    }

    private static Color Leaf()
    {
        switch (Random.Range(0, 4))
        {
            case 0: return new Color(0.85f, 0.42f, 0.12f, 0.95f);
            case 1: return new Color(0.72f, 0.20f, 0.12f, 0.95f);
            case 2: return new Color(0.92f, 0.68f, 0.18f, 0.95f);
            default: return new Color(0.55f, 0.35f, 0.13f, 0.95f);
        }
    }

    private static void Clear()
    {
        for (int i = 0; i < _live.Count; i++)
            if (_live[i]?.Tr != null) Destroy(_live[i].Tr.gameObject);
        _live.Clear();
    }

    private static Sprite Quad()
    {
        if (_quad != null) return _quad;
        try
        {
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var px = new Color32[16];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(px);
            tex.Apply();
            _quad = Sprite.Create(tex, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 4f);
        }
        catch { }
        return _quad;
    }

    private static Sprite Dot()
    {
        if (_dot != null) return _dot;
        try
        {
            const int n = 16;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var px = new Color32[n * n];
            float r = n * 0.5f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Mathf.Sqrt((x - r + 0.5f) * (x - r + 0.5f) + (y - r + 0.5f) * (y - r + 0.5f));
                    float a = Mathf.Clamp01(r - d);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            tex.SetPixels32(px);
            tex.Apply();
            _dot = Sprite.Create(tex, new Rect(0f, 0f, n, n), new Vector2(0.5f, 0.5f), n);
        }
        catch { }
        return _dot;
    }
}
