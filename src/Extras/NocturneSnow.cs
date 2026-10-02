using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Nocturne;

public sealed class NocturneSnow : MonoBehaviour
{
    private sealed class Flake
    {
        public Transform Tr;
        public SpriteRenderer Sr;
        public Color Col;
        public float X;
        public float Y;
        public float Vx;
        public float Vy;
        public float Fall;
        public float Drift;
        public float Phase;
        public float Spin;
        public float Rot;
        public float Flap;
        public float Angle;
        public float Size;
        public float Alpha;
        public float Wait;
    }

    private const int PerFrame = 40;

    private static readonly (int Art, float Share, int Cap)[] Preset =
    {
        (0, 0f, 0),
        (0, 1f, 400),
        (0, 1f, 400),
        (1, 1f, 400),
        (1, 1f, 400),
        (3, 0.08f, 30),
        (2, 0.35f, 160),
        (6, 0.09f, 14),
        (2, 0.55f, 240),
        (5, 0.1f, 40),
        (4, 0.7f, 280)
    };

    private static readonly Color[] Leaves =
    {
        new Color(0.85f, 0.42f, 0.12f, 0.95f),
        new Color(0.72f, 0.20f, 0.12f, 0.95f),
        new Color(0.92f, 0.68f, 0.18f, 0.95f),
        new Color(0.55f, 0.35f, 0.13f, 0.95f)
    };

    private static readonly Color[] Petals =
    {
        new Color(1f, 0.72f, 0.82f, 0.95f),
        new Color(1f, 0.82f, 0.89f, 0.95f),
        new Color(0.96f, 0.60f, 0.76f, 0.95f)
    };

    private static readonly Color[] Sparks =
    {
        new Color(1f, 0.92f, 0.35f),
        new Color(0.72f, 1f, 0.42f),
        new Color(1f, 1f, 0.72f)
    };

    private static readonly List<Flake> _live = new List<Flake>();
    private static readonly Sprite[] _art = new Sprite[7];
    private int _type;

    public void Update()
    {
        int type = Mathf.Clamp(NocturneConfig.LobbyWeather.Value, 0, Preset.Length - 1);
        if (type == 0 || LobbyBehaviour.Instance == null)
        {
            if (_type != 0)
            {
                _type = 0;
                Clear();
            }
            return;
        }

        Camera cam = Camera.main;
        if (cam == null) return;

        if (_type != type)
        {
            _type = type;
            Clear();
        }

        int n = Count(type, NocturneConfig.LobbySnowAmount.Value);
        if (_live.Count != n) Fit(cam, n, type);
        if (_live.Count == 0) return;

        float h = cam.orthographicSize;
        float w = h * cam.aspect;
        Vector3 c = cam.transform.position;
        float top = c.y + h + 0.5f;
        float bot = c.y - h - 0.5f;
        float dt = Mathf.Min(Time.deltaTime, 0.1f);
        float t = Time.time;

        Vector3 p = default;
        p.z = c.z + 5f;
        Vector3 sc = default;
        sc.z = 1f;
        Quaternion q = default;
        Color col = default;

        for (int i = 0; i < _live.Count; i++)
        {
            Flake f = _live[i];
            float py;

            switch (type)
            {
                case 5:
                {
                    f.X += f.Vx * dt;
                    if (MathF.Abs(f.X - c.x) > w + 1.5f)
                        Spawn(f, type, c, w, h, false);
                    else if (MathF.Abs(f.Y - c.y) > h + 1.5f)
                        f.Y = c.y + Random.Range(-h * 0.6f, h * 0.85f);

                    float a = t * f.Spin + f.Phase;
                    py = f.Y + (MathF.Sin(a) + MathF.Sin(a * 2.3f + 1.7f) * 0.4f) * f.Drift;
                    sc.x = f.Size;
                    sc.y = f.Size * (0.7f + 0.3f * MathF.Sin(t * f.Flap + f.Phase));
                    f.Tr.localScale = sc;
                    break;
                }
                case 6:
                {
                    f.X += (f.Vx + MathF.Cos(t * 0.8f + f.Phase) * f.Drift) * dt;
                    f.Y += (f.Vy + MathF.Sin(t * 1.1f + f.Phase * 1.7f) * f.Drift) * dt;
                    if (f.X > c.x + w + 1f)
                        f.X -= 2f * w + 2f;
                    else if (f.X < c.x - w - 1f)
                        f.X += 2f * w + 2f;
                    if (f.Y > c.y + h + 1f)
                        f.Y -= 2f * h + 2f;
                    else if (f.Y < c.y - h - 1f)
                        f.Y += 2f * h + 2f;

                    py = f.Y;
                    float blink = 0.5f + 0.5f * MathF.Sin(t * f.Spin + f.Phase);
                    col = f.Col;
                    col.a = f.Alpha * (0.12f + 0.88f * blink * MathF.Sqrt(blink));
                    f.Sr.color = col;
                    break;
                }
                case 7:
                    f.X += f.Vx * dt;
                    if (MathF.Abs(f.X - c.x) > w + f.Size)
                        Spawn(f, type, c, w, h, false);
                    else if (MathF.Abs(f.Y - c.y) > h + f.Size * 0.35f)
                        f.Y = c.y + Random.Range(-h * 0.8f, h * 0.8f);
                    py = f.Y + MathF.Sin(t * 0.2f + f.Phase) * 0.3f;
                    break;
                case 8:
                {
                    f.Y -= f.Fall * dt;
                    f.X += MathF.Sin(t * f.Spin + f.Phase) * f.Drift * dt;
                    if (f.Y > top || f.X < c.x - w - 1f || f.X > c.x + w + 1f)
                        Spawn(f, type, c, w, h, false);
                    else if (f.Y < bot - 1.5f)
                    {
                        Spawn(f, type, c, w, h, false);
                        f.Y = top - Random.Range(0f, 0.4f);
                    }

                    py = f.Y;
                    float k = Math.Clamp((f.Y - bot) / (top - bot), 0f, 1f);
                    col.r = 1f;
                    col.g = 0.85f - 0.6f * k;
                    col.b = 0.3f - 0.25f * k;
                    col.a = f.Alpha * Math.Min(1f, (1f - k) * 1.6f) * (0.75f + 0.25f * MathF.Sin(t * 17f + f.Phase));
                    f.Sr.color = col;
                    break;
                }
                case 9:
                    if (f.Wait > 0f)
                    {
                        f.Wait -= dt;
                        if (f.Wait > 0f) continue;
                        Spawn(f, type, c, w, h, false);
                    }

                    f.X += f.Vx * dt;
                    f.Y += f.Vy * dt;
                    if (f.Y < bot || f.X < c.x - w - 3f)
                    {
                        f.Wait = Random.Range(0.3f, 3.5f);
                        f.Sr.enabled = false;
                        continue;
                    }
                    py = f.Y;
                    break;
                default:
                    f.Y -= f.Fall * dt;
                    if (f.Drift > 0f)
                        f.X += MathF.Sin(t * f.Spin + f.Phase) * f.Drift * dt;
                    if (f.Y < bot || f.X < c.x - w - 1f || f.X > c.x + w + 1f)
                        Spawn(f, type, c, w, h, false);
                    else if (f.Y > top + 1.5f)
                    {
                        Spawn(f, type, c, w, h, false);
                        f.Y = bot + Random.Range(0f, 0.4f);
                    }

                    py = f.Y;
                    if (f.Rot != 0f)
                    {
                        f.Angle = (f.Angle + f.Rot * dt) % 360f;
                        float half = f.Angle * Mathf.Deg2Rad * 0.5f;
                        q.z = MathF.Sin(half);
                        q.w = MathF.Cos(half);
                        f.Tr.rotation = q;
                    }
                    if (f.Flap > 0f)
                    {
                        sc.x = f.Size * (0.35f + 0.65f * MathF.Abs(MathF.Cos(t * f.Flap + f.Phase)));
                        sc.y = f.Size;
                        f.Tr.localScale = sc;
                    }
                    break;
            }

            p.x = f.X;
            p.y = py;
            f.Tr.position = p;
        }
    }

    private static int Count(int type, int amount)
    {
        var pre = Preset[type];
        return Mathf.Clamp(Mathf.RoundToInt(amount * pre.Share), 2, pre.Cap);
    }

    private void Fit(Camera cam, int n, int type)
    {
        while (_live.Count > n)
        {
            int last = _live.Count - 1;
            Destroy(_live[last].Tr.gameObject);
            _live.RemoveAt(last);
        }
        if (_live.Count >= n) return;

        Sprite s = Art(type);
        float h = cam.orthographicSize;
        float w = h * cam.aspect;
        Vector3 c = cam.transform.position;
        int stop = Mathf.Min(n, _live.Count + PerFrame);

        while (_live.Count < stop)
        {
            var go = new GameObject("nocturne_weather");
            go.transform.SetParent(transform, false);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = s;

            var f = new Flake { Tr = go.transform, Sr = sr };
            Spawn(f, type, c, w, h, true);
            _live.Add(f);
        }
    }

    private static void Spawn(Flake f, int type, Vector3 c, float w, float h, bool first)
    {
        float sx;
        float sy;
        Color col;

        f.Phase = Random.Range(0f, 10f);
        f.Spin = Random.Range(0.6f, 1.8f);
        f.Drift = 0f;
        f.Rot = 0f;
        f.Flap = 0f;
        f.Alpha = 1f;

        switch (type)
        {
            case 2:
            {
                float s = Random.Range(0.03f, 0.06f);
                sx = s * 0.4f;
                sy = s * 6f;
                col = new Color(0.62f, 0.78f, 1f, Random.Range(0.35f, 0.7f));
                f.Fall = Random.Range(7f, 12f);
                break;
            }
            case 3:
            {
                float s = Random.Range(0.08f, 0.16f);
                sx = s * 1.5f;
                sy = s * 0.75f;
                col = Leaves[Random.Range(0, Leaves.Length)];
                f.Fall = Random.Range(0.5f, 1.3f);
                f.Drift = Random.Range(1.1f, 2.2f);
                f.Rot = Random.Range(-120f, 120f);
                f.Angle = Random.Range(0f, 360f);
                break;
            }
            case 4:
            {
                float s = Random.Range(0.05f, 0.1f);
                sx = s;
                sy = s * Random.Range(0.4f, 1f);
                col = Color.HSVToRGB(Random.value, 0.85f, 1f);
                f.Fall = Random.Range(1.2f, 2.8f);
                f.Drift = Random.Range(0.8f, 1.8f);
                f.Rot = Random.Range(-260f, 260f);
                f.Angle = Random.Range(0f, 360f);
                break;
            }
            case 5:
            {
                float s = Random.Range(0.55f, 1f);
                f.Size = s;
                sx = s;
                sy = s;
                col = new Color(0.07f, 0.05f, 0.1f, Random.Range(0.8f, 0.95f));
                f.Vx = Random.Range(2.2f, 4.4f) * (Random.value < 0.5f ? -1f : 1f);
                f.Drift = Random.Range(0.25f, 0.7f);
                f.Spin = Random.Range(1.3f, 2.6f);
                f.Flap = Random.Range(22f, 30f);
                f.X = first ? c.x + Random.Range(-w, w) : c.x - Math.Sign(f.Vx) * (w + 1.2f);
                f.Y = c.y + Random.Range(-h * 0.6f, h * 0.85f);
                break;
            }
            case 6:
            {
                float s = Random.Range(0.26f, 0.46f);
                sx = s;
                sy = s;
                col = Sparks[Random.Range(0, Sparks.Length)];
                f.Alpha = Random.Range(0.7f, 1f);
                f.Vx = Random.Range(-0.18f, 0.18f);
                f.Vy = Random.Range(-0.12f, 0.12f);
                f.Drift = Random.Range(0.35f, 0.8f);
                f.Spin = Random.Range(1.2f, 3f);
                f.X = c.x + Random.Range(-w, w);
                f.Y = c.y + Random.Range(-h, h);
                break;
            }
            case 7:
            {
                float s = Random.Range(6f, 12f);
                f.Size = s;
                sx = s;
                sy = s * Random.Range(0.35f, 0.6f);
                col = new Color(0.82f, 0.87f, 0.96f, Random.Range(0.1f, 0.2f));
                f.Vx = Random.Range(0.15f, 0.5f) * (Random.value < 0.5f ? -1f : 1f);
                f.X = first ? c.x + Random.Range(-w, w) : c.x - Math.Sign(f.Vx) * (w + s * 0.5f);
                f.Y = c.y + Random.Range(-h * 0.8f, h * 0.8f);
                break;
            }
            case 8:
            {
                float s = Random.Range(0.1f, 0.24f);
                sx = s;
                sy = s;
                col = new Color(1f, 0.85f, 0.3f);
                f.Fall = -Random.Range(0.9f, 2.4f);
                f.Drift = Random.Range(0.3f, 1f);
                f.Alpha = Random.Range(0.7f, 1f);
                f.X = c.x + Random.Range(-w, w);
                f.Y = first ? c.y + Random.Range(-h, h) : c.y - h - 0.5f - Random.Range(0f, 1f);
                break;
            }
            case 9:
            {
                if (first)
                {
                    f.Wait = Random.Range(0f, 3.5f);
                    f.Sr.enabled = false;
                    return;
                }

                float ang = Random.Range(205f, 235f) * Mathf.Deg2Rad;
                float speed = Random.Range(9f, 15f);
                f.Vx = MathF.Cos(ang) * speed;
                f.Vy = MathF.Sin(ang) * speed;
                sx = Random.Range(1.4f, 3f);
                sy = Random.Range(0.6f, 1.2f);
                col = new Color(0.85f, 0.93f, 1f, Random.Range(0.7f, 1f));
                f.X = c.x + Random.Range(-w * 0.4f, w * 1.3f);
                f.Y = c.y + h + 0.5f + Random.Range(0f, 1.2f);

                Quaternion q = default;
                q.z = MathF.Sin(ang * 0.5f);
                q.w = MathF.Cos(ang * 0.5f);
                f.Tr.rotation = q;
                f.Sr.enabled = true;
                break;
            }
            case 10:
            {
                float s = Random.Range(0.1f, 0.2f);
                f.Size = s;
                sx = s;
                sy = s;
                col = Petals[Random.Range(0, Petals.Length)];
                f.Fall = Random.Range(0.55f, 1.25f);
                f.Drift = Random.Range(0.8f, 1.8f);
                f.Rot = Random.Range(-90f, 90f);
                f.Flap = Random.Range(1.5f, 3f);
                f.Angle = Random.Range(0f, 360f);
                break;
            }
            default:
            {
                float s = Random.Range(0.035f, 0.11f);
                sx = s;
                sy = s;
                col = new Color(1f, 1f, 1f, Random.Range(0.45f, 0.95f));
                f.Fall = Random.Range(0.7f, 2.1f);
                f.Drift = Random.Range(0.2f, 0.9f);
                break;
            }
        }

        if (type <= 4 || type == 10)
        {
            f.X = c.x + Random.Range(-w, w);
            f.Y = first ? c.y + Random.Range(-h, h) : c.y + h + 0.5f + Random.Range(0f, 1f);
        }

        f.Col = col;
        f.Tr.localScale = new Vector3(sx, sy, 1f);
        f.Sr.color = col;
    }

    private static void Clear()
    {
        for (int i = 0; i < _live.Count; i++)
            Destroy(_live[i].Tr.gameObject);
        _live.Clear();
    }

    private static Sprite Art(int type)
    {
        int k = Preset[type].Art;
        if (_art[k] != null) return _art[k];

        switch (k)
        {
            case 0:
                _art[k] = Bake(16, 16, 16f, (u, v) => Math.Clamp((1f - MathF.Sqrt(u * u + v * v)) * 8f, 0f, 1f));
                break;
            case 1:
                _art[k] = Bake(4, 4, 4f, (u, v) => 1f);
                break;
            case 2:
                _art[k] = Bake(64, 64, 64f, Glow);
                break;
            case 3:
                _art[k] = Bake(64, 32, 64f, Bat);
                break;
            case 4:
                _art[k] = Bake(32, 32, 32f, Petal);
                break;
            case 5:
                _art[k] = Bake(64, 4, 64f, Streak);
                break;
            default:
                _art[k] = Bake(64, 64, 64f, Mist);
                break;
        }
        return _art[k];
    }

    private static Sprite Bake(int w, int h, float ppu, Func<float, float, float> alpha)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float sum = 0f;
                for (int j = 0; j < 3; j++)
                {
                    for (int i = 0; i < 3; i++)
                        sum += alpha((x + (i + 0.5f) / 3f) / w * 2f - 1f, (y + (j + 0.5f) / 3f) / h * 2f - 1f);
                }
                px[y * w + x] = new Color32(255, 255, 255, (byte)(sum / 9f * 255f + 0.5f));
            }
        }

        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.5f), ppu);
    }

    private static float Glow(float u, float v)
    {
        float d = MathF.Sqrt(u * u + v * v);
        if (d >= 1f) return 0f;

        float k = 1f - d;
        return Math.Min(1f, k * k * k * 0.85f + Math.Max(0f, 1f - d * 4.5f) * 0.5f);
    }

    private static float Mist(float u, float v)
    {
        float d = MathF.Sqrt(u * u + v * v);
        if (d >= 1f) return 0f;

        float k = 1f - d;
        return k * k * (3f - 2f * k);
    }

    private static float Bat(float u, float v)
    {
        float x = MathF.Abs(u);
        float y = v * 0.5f;

        float bx = x / 0.085f;
        float by = (y + 0.02f) / 0.21f;
        if (bx * bx + by * by < 1f) return 1f;

        float hy = y - 0.2f;
        if (x * x + hy * hy < 0.0064f) return 1f;
        if (y > 0.24f && y < 0.4f && x > 0.03f && x < 0.12f && x < 0.12f - (y - 0.24f) * 0.5f)
            return 1f;

        if (x < 0.07f || x > 0.97f)
            return 0f;

        float s = (x - 0.07f) / 0.9f;
        float up = 0.15f + 0.07f * s;
        float low = -0.2f + 0.38f * s + 0.05f * MathF.Abs(MathF.Sin(3f * MathF.PI * s));
        return y < up && y > low ? 1f : 0f;
    }

    private static float Petal(float u, float v)
    {
        float e = (v + 0.05f) / 0.95f;
        float half = 0.62f * MathF.Sqrt(Math.Max(0f, 1f - e * e)) * (1f - 0.2f * v);
        if (MathF.Abs(u) > half) return 0f;
        if (v > 0.74f && MathF.Abs(u) < (v - 0.74f) * 0.9f)
            return 0f;
        return 1f;
    }

    private static float Streak(float u, float v)
    {
        float t = (u + 1f) * 0.5f;
        float a = MathF.Pow(t, 2.2f) * (1f - v * v);
        return t > 0.94f ? a * (1f - t) / 0.06f : a;
    }
}
