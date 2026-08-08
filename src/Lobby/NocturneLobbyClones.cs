using System.Collections.Generic;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Attributes;
using InnerNet;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Nocturne;

public sealed class NocturneLobbyClones : MonoBehaviour
{
    private const float PickRadius = 0.5f;
    private const float CloneZ = -0.5f;
    private const float ShadowDelay = 0.35f;
    private const float DriftSpeed = 0.6f;
    private const float DriftMaxR = 7f;

    private sealed class Clone
    {
        public GameObject Go;
        public bool Shadow;
        public bool Static;
        public Vector2 Vel;
        public float DirTimer;
        public SpriteRenderer[] Rends;
        public Vector3[] Pose;
        public int Form = -1;
        public Vector3 Off;
        public Vector3 Home;
        public float Phase;
    }

    internal static NocturneLobbyClones Instance;

    private readonly List<Clone> _clones = new List<Clone>();
    private readonly List<Clone> _regular = new List<Clone>();
    private readonly Queue<GameObject> _pool = new Queue<GameObject>();
    private readonly List<(float t, Vector3 p)> _trail = new List<(float, Vector3)>(256);
    private Clone _drag;
    private Vector3 _dragOff;
    private Clone _shadow;
    private bool _wasLobby;

    public void Awake() => Instance = this;

    public void Update()
    {
        bool inLobby = LobbyBehaviour.Instance != null || NocturneFakeMap.Active;
        if (_wasLobby && !inLobby) ClearAll();
        _wasLobby = inLobby;

        RecordTrail();
        ManageShadow();
        Dance(Time.time);

        if (!Active()) return;

        Vector3 world = MouseWorld();
        LeftClick(world);
        RightClick(world);
        Drag(world);

        Guard(Time.deltaTime);
        Drift(Time.deltaTime);
    }

    public void LateUpdate()
    {
        for (int i = 0; i < _clones.Count; i++)
        {
            Clone e = _clones[i];
            if (e.Go == null || e.Rends == null) continue;
            for (int j = 0; j < e.Rends.Length; j++)
            {
                SpriteRenderer sr = e.Rends[j];
                if (sr != null) sr.transform.localPosition = e.Pose[j];
            }
        }
    }

    private static bool Active()
    {
        return NocturneConfig.LobbyCloneMode.Value
            && (LobbyBehaviour.Instance != null || NocturneFakeMap.Active)
            && PlayerControl.LocalPlayer != null
            && !NocturneMenu.Opened;
    }

    private void LeftClick(Vector3 world)
    {
        if (!Input.GetMouseButtonDown(0)) return;

        Clone hit = Nearest(world);
        if (hit != null && !hit.Shadow)
        {
            _drag = hit;
            hit.Form = -1;
            _dragOff = hit.Go.transform.position - world;
            return;
        }

        int count = NocturneConfig.LobbyCloneSpawnCount.Value;
        for (int i = 0; i < count; i++)
        {
            if (_regular.Count >= NocturneConfig.LobbyCloneMax.Value) break;
            Vector3 p = world;
            if (count > 1) { Vector2 o = Random.insideUnitCircle * 0.6f; p += new Vector3(o.x, o.y, 0f); }
            Clone e = Spawn(p, false);
            if (e != null) RandomDrift(e);
        }
    }

    private void RightClick(Vector3 world)
    {
        if (!Input.GetMouseButtonDown(1)) return;
        Clone hit = Nearest(world);
        if (hit != null && !hit.Shadow) Remove(hit);
    }

    private void Drag(Vector3 world)
    {
        if (_drag == null) return;
        if (Input.GetMouseButton(0))
        {
            if (_drag.Go != null) _drag.Go.transform.position = world + _dragOff;
        }
        else _drag = null;
    }

    private void Guard(float dt)
    {
        if (!NocturneConfig.LobbyCloneGuard.Value || PlayerControl.LocalPlayer == null || _regular.Count == 0) return;
        Vector3 c = PlayerControl.LocalPlayer.transform.position;
        float r = NocturneConfig.LobbyCloneGuardRadius.Value;
        for (int i = 0; i < _regular.Count; i++)
        {
            if (_regular[i].Go == null || _regular[i].Static) continue;
            float ang = i * (Mathf.PI * 2f / _regular.Count) - Mathf.PI * 0.5f;
            Vector3 target = c + new Vector3(Mathf.Cos(ang) * r, Mathf.Sin(ang) * r, 0f);
            target.z = CloneZ;
            _regular[i].Go.transform.position = Vector3.Lerp(_regular[i].Go.transform.position, target, dt * 6f);
        }
    }

    private void Drift(float dt)
    {
        if (!NocturneConfig.LobbyCloneDrift.Value || NocturneConfig.LobbyCloneGuard.Value || PlayerControl.LocalPlayer == null) return;
        Vector3 c = PlayerControl.LocalPlayer.transform.position;
        foreach (Clone e in _regular)
        {
            if (e.Go == null || e.Static) continue;
            e.DirTimer -= dt;
            if (e.DirTimer <= 0f) RandomDrift(e);
            Vector3 p = e.Go.transform.position;
            p.x += e.Vel.x * dt;
            p.y += e.Vel.y * dt;
            Vector2 diff = new Vector2(p.x - c.x, p.y - c.y);
            if (diff.sqrMagnitude > DriftMaxR * DriftMaxR)
                e.Vel = new Vector2(c.x - p.x, c.y - p.y).normalized * DriftSpeed;
            p.z = CloneZ;
            e.Go.transform.position = p;
        }
    }

    private static void RandomDrift(Clone e)
    {
        float a = Random.Range(0f, Mathf.PI * 2f);
        e.Vel = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * DriftSpeed;
        e.DirTimer = Random.Range(1.5f, 4f);
    }

    private void RecordTrail()
    {
        if (!NocturneConfig.LobbyCloneShadow.Value || PlayerControl.LocalPlayer == null) return;
        _trail.Add((Time.time, PlayerControl.LocalPlayer.transform.position));
        float cutoff = Time.time - ShadowDelay - 0.5f;
        while (_trail.Count > 0 && _trail[0].t < cutoff) _trail.RemoveAt(0);
    }

    private void ManageShadow()
    {
        bool want = NocturneConfig.LobbyCloneShadow.Value;
        bool can = (LobbyBehaviour.Instance != null || NocturneFakeMap.Active) && PlayerControl.LocalPlayer != null;
        if (!want || !can)
        {
            if (_shadow != null) { Remove(_shadow); _shadow = null; }
            return;
        }

        if (_shadow == null || _shadow.Go == null)
            _shadow = Spawn(PlayerControl.LocalPlayer.transform.position, true);
        if (_shadow == null || _shadow.Go == null || _trail.Count == 0) return;

        float target = Time.time - ShadowDelay;
        Vector3 pos = PlayerControl.LocalPlayer.transform.position;
        for (int i = 0; i < _trail.Count; i++)
        {
            if (_trail[i].t <= target) pos = _trail[i].p;
            else break;
        }
        pos.z = CloneZ;
        _shadow.Go.transform.position = pos;
    }

    internal void BuildFormation(int idx)
    {
        if (PlayerControl.LocalPlayer == null || _regular.Count == 0) return;
        Vector3 c = PlayerControl.LocalPlayer.transform.position;
        int total = _regular.Count;
        int copies = Mathf.Clamp(NocturneConfig.LobbyCloneFormationCopies.Value, 1, 5);
        int per = Mathf.Max(1, Mathf.CeilToInt(total / (float)copies));
        float scale = FormScale();
        for (int i = 0; i < _regular.Count; i++)
        {
            Clone e = _regular[i];
            if (e.Go == null) continue;
            e.Static = true;
            int ring = Mathf.Min(copies - 1, i / per);
            Vector3 off = FormationPos(idx, i % per, per, c) - c;
            e.Form = idx;
            e.Home = c;
            e.Off = off * scale * (1f + ring * 0.55f);
            e.Phase = per > 0 ? (i % per) / (float)per : 0f;
            Vector3 p = c + e.Off;
            p.z = CloneZ;
            e.Go.transform.position = p;
        }
    }

    private void Dance(float t)
    {
        if (!NocturneConfig.CloneFormationAnim.Value) return;
        for (int i = 0; i < _regular.Count; i++)
        {
            Clone e = _regular[i];
            if (e.Go == null || e.Form < 0) continue;
            Vector3 o = Sway(e.Form, e.Off, t, e.Phase);
            e.Go.transform.position = new Vector3(e.Home.x + o.x, e.Home.y + o.y, CloneZ);
        }
    }

    private static Vector3 Spin(Vector3 v, float deg)
    {
        float a = deg * Mathf.Deg2Rad;
        float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
        return new Vector3(v.x * ca - v.y * sa, v.x * sa + v.y * ca, 0f);
    }

    private static Vector3 Sway(int idx, Vector3 off, float t, float phase)
    {
        float s = Mathf.Clamp(NocturneConfig.CloneFormationAnimSpeed.Value, 0.2f, 3f);
        t *= s;
        switch (idx)
        {
            case 1:
            case 2:
            case 5:
            case 15:
            case 17:
                return Spin(off, t * 24f);
            case 6:
                return Spin(off, t * 46f);
            case 3:
            case 13:
                return Spin(off, t * 14f) * (1f + 0.07f * Mathf.Sin(t * 2.4f));
            case 4:
            {
                float beat = Mathf.Pow(Mathf.Abs(Mathf.Sin(t * 2.1f)), 3f);
                return off * (1f + 0.13f * beat);
            }
            case 9:
            {
                float lift = Mathf.Max(0f, off.y - 1.2f) * 0.16f;
                return new Vector3(off.x, off.y + Mathf.Sin(t * 3.1f) * lift, 0f);
            }
            case 0:
            case 8:
            case 12:
                return new Vector3(off.x, off.y + Mathf.Sin(t * 2.6f + off.x * 0.7f) * 0.38f, 0f);
            case 11:
                return new Vector3(off.x, off.y + Mathf.Sin(t * 2.2f + phase * 6.28f) * 0.30f, 0f);
            case 14:
                return off + new Vector3(Mathf.Sin(t * 17f + phase * 9f) * 0.07f, Mathf.Cos(t * 15f + phase * 7f) * 0.07f, 0f);
            case 10:
            case 18:
                return Spin(off, Mathf.Sin(t * 1.4f) * 7f);
            default:
                return Spin(off, Mathf.Sin(t * 1.2f) * 10f);
        }
    }

    internal void BuildText(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || PlayerControl.LocalPlayer == null) return;
        text = text.ToUpperInvariant();

        float px = 0.38f;
        float need = NocturneCloneFont.GetTextWidth(text, px);
        if (need > 15f) px = px * 15f / need;

        List<Vector3> offsets = NocturneCloneFont.GetPositions(text, px);
        if (offsets.Count == 0) return;

        ClearRegular();
        Vector3 c = PlayerControl.LocalPlayer.transform.position;
        for (int i = 0; i < offsets.Count && i < 2000; i++)
        {
            Clone e = Spawn(new Vector3(c.x + offsets[i].x, c.y + offsets[i].y, CloneZ), false);
            if (e != null) e.Static = true;
        }
    }

    internal static float FormScale() => Mathf.Clamp(NocturneConfig.CloneFormationScale.Value, 0.3f, 3f);

    internal void BuildSelf()
    {
        PlayerControl me = PlayerControl.LocalPlayer;
        if (me == null || me.cosmetics == null) return;

        float aspect;
        Texture2D shot = Capture(me, out aspect);
        if (shot == null) return;

        int want = Mathf.Clamp(NocturneConfig.LobbyCloneMax.Value, 20, 2000);
        int ny = Mathf.Max(12, Mathf.RoundToInt(Mathf.Sqrt(want / Mathf.Max(0.3f, aspect)) * 1.3f));
        int nx = Mathf.Max(7, Mathf.RoundToInt(ny * aspect));
        float step = 0.55f * FormScale();

        var pts = new List<KeyValuePair<Vector2, Color>>();
        for (int gy = ny - 1; gy >= 0; gy--)
            for (int gx = 0; gx < nx; gx++)
            {
                Color col = shot.GetPixelBilinear((gx + 0.5f) / nx, (gy + 0.5f) / ny);
                if (col.a < 0.22f) continue;
                col.a = 1f;
                pts.Add(new KeyValuePair<Vector2, Color>(new Vector2((gx - nx * 0.5f) * step, (gy - ny * 0.5f) * step), col));
            }
        try { Object.Destroy(shot); } catch { }
        if (pts.Count == 0) return;

        ClearRegular();
        Vector3 c = me.transform.position;
        for (int i = 0; i < pts.Count && i < 2000; i++)
        {
            Vector2 p = pts[i].Key;
            Clone e = Spawn(new Vector3(c.x + p.x, c.y + p.y, CloneZ), false);
            if (e == null) continue;
            e.Static = true;
            Strip(e.Go);
            TintBody(e, pts[i].Value);
        }
    }

    private static Texture2D Capture(PlayerControl me, out float aspect)
    {
        aspect = 0.72f;
        Camera cam = null;
        RenderTexture rt = null;
        GameObject camGo = null;
        GameObject nameGo = null;
        RenderTexture prev = RenderTexture.active;
        try
        {
            try
            {
                if (me.cosmetics.nameTextContainer != null) nameGo = me.cosmetics.nameTextContainer;
                else if (me.cosmetics.nameText != null) nameGo = ((Component)me.cosmetics.nameText).gameObject;
                if (nameGo != null) nameGo.SetActive(false);
            }
            catch { }

            Bounds b = default;
            bool has = false;
            int mask = 0;
            SpriteRenderer bodySr = me.cosmetics.currentBodySprite != null ? me.cosmetics.currentBodySprite.BodySprite : null;
            if (bodySr != null && bodySr.sprite != null) { b = bodySr.bounds; mask = 1 << bodySr.gameObject.layer; has = true; }

            foreach (SpriteRenderer sr in me.cosmetics.GetComponentsInChildren<SpriteRenderer>(false))
            {
                if (sr == null || !sr.enabled || sr.sprite == null) continue;
                mask |= 1 << sr.gameObject.layer;
                Vector3 sz = sr.bounds.size;
                if (sz.x < 0.02f || sz.x > 3f || sz.y > 3f) continue;
                if (!has) { b = sr.bounds; has = true; }
                else b.Encapsulate(sr.bounds);
            }
            if (!has) return null;
            if (mask == 0) mask = 1 << me.gameObject.layer;
            b.Expand(0.12f);
            aspect = b.size.x / Mathf.Max(0.01f, b.size.y);

            int h = 128;
            int wpx = Mathf.Clamp(Mathf.RoundToInt(h * aspect), 8, 160);
            rt = RenderTexture.GetTemporary(wpx, h, 16, RenderTextureFormat.ARGB32);

            camGo = new GameObject("NocturneShot");
            cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = b.size.y * 0.5f;
            cam.aspect = aspect;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.cullingMask = mask;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 60f;
            cam.transform.position = new Vector3(b.center.x, b.center.y, b.center.z - 20f);
            cam.targetTexture = rt;
            cam.Render();

            RenderTexture.active = rt;
            var tex = new Texture2D(wpx, h, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0f, 0f, wpx, h), 0, 0);
            tex.Apply();
            return tex;
        }
        catch { return null; }
        finally
        {
            RenderTexture.active = prev;
            try { if (nameGo != null) nameGo.SetActive(true); } catch { }
            try { if (cam != null) cam.targetTexture = null; } catch { }
            try { if (rt != null) RenderTexture.ReleaseTemporary(rt); } catch { }
            try { if (camGo != null) Object.Destroy(camGo); } catch { }
        }
    }

    private static void TintBody(Clone e, Color col)
    {
        try
        {
            PlayerControl pc = e.Go.GetComponent<PlayerControl>();
            if (pc == null || pc.cosmetics == null || pc.cosmetics.currentBodySprite == null) return;
            SpriteRenderer body = pc.cosmetics.currentBodySprite.BodySprite;
            if (body != null) PlayerMaterial.SetColors(col, (Renderer)body);
        }
        catch { }
    }

    private static void Strip(GameObject go)
    {
        try
        {
            PlayerControl pc = go.GetComponent<PlayerControl>();
            if (pc == null || pc.cosmetics == null) return;
            CosmeticsLayer cos = pc.cosmetics;
            if (cos.hat != null) ((Component)cos.hat).gameObject.SetActive(false);
            if (cos.skin != null) ((Component)cos.skin).gameObject.SetActive(false);
            if (cos.visor != null) ((Component)cos.visor).gameObject.SetActive(false);
            if (cos.currentPet != null) ((Component)cos.currentPet).gameObject.SetActive(false);
        }
        catch { }
    }

    private static bool Ell(float x, float y, float cx, float cy, float rx, float ry)
    {
        float dx = (x - cx) / rx, dy = (y - cy) / ry;
        return dx * dx + dy * dy <= 1f;
    }

    private static bool Cap(float x, float y, float ax, float ay, float bx, float by, float r)
    {
        float vx = bx - ax, vy = by - ay, wx = x - ax, wy = y - ay;
        float l = vx * vx + vy * vy;
        float t = l > 0f ? Mathf.Clamp01((wx * vx + wy * vy) / l) : 0f;
        float dx = x - (ax + vx * t), dy = y - (ay + vy * t);
        return dx * dx + dy * dy <= r * r;
    }

    private static bool Tri(float x, float y, float x1, float y1, float x2, float y2, float x3, float y3)
    {
        float d1 = (x - x2) * (y1 - y2) - (x1 - x2) * (y - y2);
        float d2 = (x - x3) * (y2 - y3) - (x2 - x3) * (y - y3);
        float d3 = (x - x1) * (y3 - y1) - (x3 - x1) * (y - y1);
        bool neg = d1 < 0f || d2 < 0f || d3 < 0f;
        bool pos = d1 > 0f || d2 > 0f || d3 > 0f;
        return !(neg && pos);
    }

    private static readonly float[] WingTips = { -3.9f, 7.9f, -1.3f, 7.9f, 1.3f, 7.0f, 3.5f, 5.2f };

    private static bool Wing(float x, float y, float sx, float sy, float k)
    {
        for (int i = 0; i < 4; i++)
        {
            float tx = sx + (WingTips[i * 2] - 0.4f) * k, ty = sy + (WingTips[i * 2 + 1] - 1.2f) * k;
            if (Cap(x, y, sx, sy, tx, ty, 0.17f)) return true;
            if (i == 3) break;
            float nx = sx + (WingTips[i * 2 + 2] - 0.4f) * k, ny = sy + (WingTips[i * 2 + 3] - 1.2f) * k;
            if (Tri(x, y, sx, sy, sx + (tx - sx) * 0.8f, sy + (ty - sy) * 0.8f, sx + (nx - sx) * 0.8f, sy + (ny - sy) * 0.8f)) return true;
        }
        return false;
    }

    private static bool DragonTail(float x, float y)
    {
        for (int k = 0; k < 40; k++)
        {
            float t = k / 40f, u = (k + 1) / 40f;
            float ax = -2.4f - 6.6f * t, ay = -0.5f + 0.2f * t + 3.2f * t * t;
            float bx = -2.4f - 6.6f * u, by = -0.5f + 0.2f * u + 3.2f * u * u;
            if (Cap(x, y, ax, ay, bx, by, 1.0f - 0.82f * t)) return true;
        }
        return false;
    }

    private static bool InDragon(float x, float y)
    {
        if (Ell(x, y, 0f, 0f, 3.0f, 1.7f)) return true;
        if (Cap(x, y, 2.4f, 0.8f, 4.6f, 2.5f, 0.75f)) return true;
        if (Cap(x, y, 4.6f, 2.5f, 6.4f, 3.6f, 0.6f)) return true;
        if (Ell(x, y, 7.2f, 3.9f, 1.35f, 0.9f)) return true;
        if (Cap(x, y, 7.9f, 3.6f, 9.3f, 3.3f, 0.4f)) return true;
        if (Cap(x, y, 7.0f, 4.6f, 6.2f, 6.1f, 0.22f)) return true;
        if (Wing(x, y, 0.4f, 1.2f, 1f)) return true;
        if (Wing(x, y, -1.2f, 0.9f, 0.72f)) return true;
        if (DragonTail(x, y)) return true;
        if (Tri(x, y, -8.8f, 3.0f, -10.6f, 4.6f, -10.2f, 1.9f)) return true;
        if (Cap(x, y, -1.5f, -1.3f, -2.0f, -3.2f, 0.34f)) return true;
        if (Cap(x, y, 1.4f, -1.3f, 1.8f, -3.2f, 0.34f)) return true;
        return false;
    }

    private static List<Vector2> _dragCells;
    private static int _dragFor = -1;

    private static void DragonCells(int total)
    {
        if (_dragFor == total && _dragCells != null) return;
        _dragFor = total;
        _dragCells = new List<Vector2>();
        int ny = Mathf.Max(6, Mathf.RoundToInt(Mathf.Sqrt(total / 0.52f)));
        int nx = Mathf.Max(10, Mathf.RoundToInt(ny * 1.73f));
        for (int gy = ny - 1; gy >= 0; gy--)
            for (int gx = 0; gx < nx; gx++)
            {
                float px = Mathf.Lerp(-10.6f, 9.8f, (gx + 0.5f) / nx);
                float py = Mathf.Lerp(-3.6f, 8.2f, (gy + 0.5f) / ny);
                if (InDragon(px, py)) _dragCells.Add(new Vector2(px, py));
            }
    }

    private static Vector3 DragonPos(int i, int total, Vector3 c)
    {
        DragonCells(total);
        if (_dragCells.Count == 0) return c;
        int k = Mathf.Clamp(total > 0 ? i * _dragCells.Count / total : 0, 0, _dragCells.Count - 1);
        Vector2 p = _dragCells[k];
        float s = Mathf.Clamp(Mathf.Sqrt(total) * 0.042f, 0.35f, 2.2f);
        return new Vector3(c.x + p.x * s, c.y + p.y * s, 0f);
    }

    internal static Vector3 FormationPos(int idx, int i, int total, Vector3 c)
    {
        float t;
        switch (idx)
        {
            case 1:
            {
                float r = Mathf.Max(1.4f, total * 0.28f);
                float a = i * (Mathf.PI * 2f / total) - Mathf.PI * 0.5f;
                return new Vector3(c.x + Mathf.Cos(a) * r, c.y + Mathf.Sin(a) * r, 0f);
            }
            case 2:
            {
                float baseR = Mathf.Max(1.4f, total * 0.22f);
                float a = (i % 3) * (Mathf.PI * 2f / 3f) - Mathf.PI * 0.5f;
                float r = baseR + (i / 3) * 0.5f;
                return new Vector3(c.x + Mathf.Cos(a) * r, c.y + Mathf.Sin(a) * r, 0f);
            }
            case 3:
            {
                float outer = Mathf.Max(1.8f, total * 0.22f);
                int slot = i % 10;
                float r = slot % 2 == 0 ? outer : outer * 0.42f;
                float a = slot * (Mathf.PI / 5f) - Mathf.PI * 0.5f;
                return new Vector3(c.x + Mathf.Cos(a) * r, c.y + Mathf.Sin(a) * r, 0f);
            }
            case 4:
            {
                float tt = i * (Mathf.PI * 2f / total) - Mathf.PI;
                float x = 16f * Mathf.Pow(Mathf.Sin(tt), 3f);
                float y = 13f * Mathf.Cos(tt) - 5f * Mathf.Cos(2f * tt) - 2f * Mathf.Cos(3f * tt) - Mathf.Cos(4f * tt);
                return new Vector3(c.x + x * 0.12f, c.y + y * 0.12f, 0f);
            }
            case 5:
            {
                float r = Mathf.Max(1.4f, total * 0.28f);
                int side = (int)((float)i / total * 4f);
                float tt = (float)i / total * 4f - side;
                float a = side * Mathf.PI * 0.5f - Mathf.PI * 0.25f;
                float na = a + Mathf.PI * 0.5f;
                float px = Mathf.Lerp(Mathf.Cos(a), Mathf.Cos(na), tt) * r;
                float py = Mathf.Lerp(Mathf.Sin(a), Mathf.Sin(na), tt) * r;
                return new Vector3(c.x + px, c.y + py, 0f);
            }
            case 6:
            {
                float maxR = Mathf.Max(2f, total * 0.3f);
                t = (float)i / Mathf.Max(1, total - 1);
                float a = t * Mathf.PI * 2f * 2.5f;
                return new Vector3(c.x + Mathf.Cos(a) * t * maxR, c.y + Mathf.Sin(a) * t * maxR, 0f);
            }
            case 7:
            {
                float len = Mathf.Max(1.2f, total * 0.25f);
                int arm = i % 4;
                t = ((i / 4) + 1f) / Mathf.Max(1f, total / 4f);
                float x = arm == 1 ? len * t : arm == 3 ? -len * t : 0f;
                float y = arm == 0 ? len * t : arm == 2 ? -len * t : 0f;
                return new Vector3(c.x + x, c.y + y, 0f);
            }
            case 8:
            {
                float width = Mathf.Max(3f, total * 0.5f);
                t = total > 1 ? (float)i / (total - 1) : 0.5f;
                return new Vector3(c.x + (t - 0.5f) * width, c.y + Mathf.Sin(t * Mathf.PI * 3f) * 0.9f, 0f);
            }
            case 9:
                return DragonPos(i, total, c);
            case 10:
            {
                int visorN = Mathf.Clamp(total / 6, 3, 12);
                if (i < visorN)
                {
                    float va = (float)i / visorN * Mathf.PI * 2f;
                    return new Vector3(c.x + 0.5f + Mathf.Cos(va) * 0.55f, c.y + 0.85f + Mathf.Sin(va) * 0.34f, 0f);
                }
                int bi = i - visorN, bn = Mathf.Max(1, total - visorN);
                float a = (float)bi / bn * Mathf.PI * 2f - Mathf.PI * 0.5f;
                return new Vector3(c.x + Mathf.Cos(a) * 1.3f, c.y + Mathf.Sin(a) * 2.2f, 0f);
            }
            case 11:
            {
                float s = Mathf.Max(2f, total * 0.24f);
                float tt = (float)i / total * Mathf.PI * 2f;
                float d = 1f + Mathf.Sin(tt) * Mathf.Sin(tt);
                return new Vector3(c.x + Mathf.Cos(tt) / d * s, c.y + Mathf.Sin(tt) * Mathf.Cos(tt) / d * s, 0f);
            }
            case 12:
            {
                float len = Mathf.Max(3f, total * 0.32f);
                int headN = Mathf.Clamp(total / 3, 4, 60);
                if (i < headN)
                {
                    float h = (float)i / headN;
                    float hx = ((i % 2 == 0) ? -1f : 1f) * h * len * 0.4f;
                    return new Vector3(c.x + hx, c.y + len * 0.5f - h * len * 0.45f, 0f);
                }
                int si = i - headN, sn = Mathf.Max(1, total - headN);
                return new Vector3(c.x, c.y + len * 0.5f - (float)si / sn * len, 0f);
            }
            case 13:
            {
                float width = Mathf.Max(3f, total * 0.38f);
                t = total > 1 ? (float)i / (total - 1) : 0.5f;
                float zy = Mathf.Abs(Mathf.Sin(t * Mathf.PI * 3f)) * 1.5f;
                return new Vector3(c.x + (t - 0.5f) * width, c.y + zy - 0.4f, 0f);
            }
            case 14:
            {
                float hh = Mathf.Max(3.5f, total * 0.34f);
                t = total > 1 ? (float)i / (total - 1) : 0.5f;
                float y = (0.5f - t) * hh;
                float x = t < 0.4f ? Mathf.Lerp(0.5f, -0.6f, t / 0.4f)
                        : t < 0.5f ? Mathf.Lerp(-0.6f, 0.4f, (t - 0.4f) / 0.1f)
                        : Mathf.Lerp(0.4f, -0.7f, (t - 0.5f) / 0.5f);
                return new Vector3(c.x + x, c.y + y, 0f);
            }
            case 15:
            {
                float s = Mathf.Max(2f, total * 0.26f);
                float th = (float)i / total * Mathf.PI * 2f;
                float r = (0.55f + 0.45f * Mathf.Cos(6f * th)) * s;
                return new Vector3(c.x + Mathf.Cos(th) * r, c.y + Mathf.Sin(th) * r, 0f);
            }
            case 16:
            {
                float r = Mathf.Max(1.6f, total * 0.26f);
                int outN = Mathf.Max(1, Mathf.RoundToInt(total * 0.6f));
                if (i < outN)
                {
                    float a = Mathf.Lerp(-Mathf.PI * 0.72f, Mathf.PI * 0.72f, (float)i / outN);
                    return new Vector3(c.x + Mathf.Cos(a) * r, c.y + Mathf.Sin(a) * r, 0f);
                }
                int j = i - outN, jn = Mathf.Max(1, total - outN);
                float a2 = Mathf.Lerp(Mathf.PI * 0.72f, -Mathf.PI * 0.72f, (float)j / jn);
                return new Vector3(c.x + 0.55f + Mathf.Cos(a2) * r * 0.6f, c.y + Mathf.Sin(a2) * r * 0.6f, 0f);
            }
            case 17:
            {
                float s = Mathf.Max(2.2f, total * 0.3f);
                float th = (float)i / total * Mathf.PI * 2f;
                float r = Mathf.Abs(Mathf.Sin(2f * th)) * s;
                return new Vector3(c.x + Mathf.Cos(th) * r, c.y + Mathf.Sin(th) * r, 0f);
            }
            case 18:
            {
                float hh = Mathf.Max(3f, total * 0.3f);
                int trunkN = Mathf.Clamp(total / 8, 2, 12);
                if (i < trunkN)
                {
                    float tt = (float)i / trunkN;
                    return new Vector3(c.x + ((i % 2 == 0) ? -0.25f : 0.25f), c.y - hh * 0.5f - tt * 0.6f, 0f);
                }
                int j = i - trunkN, jn = Mathf.Max(1, total - trunkN);
                float t2 = (float)j / jn;
                float halfW = t2 * hh * 0.5f;
                return new Vector3(c.x + ((j % 2 == 0) ? -1f : 1f) * halfW, c.y + hh * 0.5f - t2 * hh, 0f);
            }
            case 19:
            {
                float s = Mathf.Max(1.6f, total * 0.24f);
                float p = (float)i / Mathf.Max(1, total) * 4f;
                int side = Mathf.Min(3, (int)p);
                float f = p - side;
                float x, y;
                switch (side)
                {
                    case 0: x = -s + 2f * s * f; y = s; break;
                    case 1: x = s; y = s - 2f * s * f; break;
                    case 2: x = s - 2f * s * f; y = -s; break;
                    default: x = -s; y = -s + 2f * s * f; break;
                }
                return new Vector3(c.x + x, c.y + y, 0f);
            }
            case 20:
            {
                float r = Mathf.Max(1.8f, total * 0.24f);
                int faceN = Mathf.Max(8, total * 6 / 10);
                if (i < faceN)
                {
                    float a = (float)i / faceN * Mathf.PI * 2f;
                    return new Vector3(c.x + Mathf.Cos(a) * r, c.y + Mathf.Sin(a) * r, 0f);
                }
                int j = i - faceN;
                if (j == 0) return new Vector3(c.x - r * 0.38f, c.y + r * 0.32f, 0f);
                if (j == 1) return new Vector3(c.x + r * 0.38f, c.y + r * 0.32f, 0f);
                int sj = j - 2, sn = Mathf.Max(1, total - faceN - 2);
                float sa = Mathf.Lerp(Mathf.PI * 1.18f, Mathf.PI * 1.82f, (float)sj / sn);
                return new Vector3(c.x + Mathf.Cos(sa) * r * 0.6f, c.y + Mathf.Sin(sa) * r * 0.6f - r * 0.05f, 0f);
            }
            case 21:
            {
                float tt = (float)i / Mathf.Max(1, total) * Mathf.PI * 2f;
                float e = Mathf.Exp(Mathf.Cos(tt)) - 2f * Mathf.Cos(4f * tt) - Mathf.Pow(Mathf.Sin(tt / 12f), 5f);
                float s = Mathf.Max(0.35f, total * 0.045f);
                return new Vector3(c.x + Mathf.Sin(tt) * e * s * 0.3f, c.y + Mathf.Cos(tt) * e * s * 0.3f, 0f);
            }
            case 22:
            {
                float r = Mathf.Max(1.5f, total * 0.2f);
                int coreN = Mathf.Max(6, total / 2);
                if (i < coreN)
                {
                    float a = (float)i / coreN * Mathf.PI * 2f;
                    return new Vector3(c.x + Mathf.Cos(a) * r * 0.55f, c.y + Mathf.Sin(a) * r * 0.55f, 0f);
                }
                int j = i - coreN;
                float ra = (float)(j % 8) / 8f * Mathf.PI * 2f;
                float rr = r * 0.85f + (j / 8) * 0.35f;
                return new Vector3(c.x + Mathf.Cos(ra) * rr, c.y + Mathf.Sin(ra) * rr, 0f);
            }
            case 23:
            {
                float s = Mathf.Max(1.6f, total * 0.24f);
                float p = (float)i / Mathf.Max(1, total) * 5f;
                int side = Mathf.Min(4, (int)p);
                float f = p - side;
                float a0 = side * (Mathf.PI * 2f / 5f) - Mathf.PI * 0.5f;
                float a1 = (side + 1) * (Mathf.PI * 2f / 5f) - Mathf.PI * 0.5f;
                float x = Mathf.Lerp(Mathf.Cos(a0), Mathf.Cos(a1), f) * s;
                float y = Mathf.Lerp(Mathf.Sin(a0), Mathf.Sin(a1), f) * s;
                return new Vector3(c.x + x, c.y + y, 0f);
            }
            case 24:
            {
                float len = Mathf.Max(2f, total * 0.28f);
                int half = i / 2;
                int hn = Mathf.Max(1, total / 2 - 1);
                float u = (float)half / hn * 2f - 1f;
                float y = (i % 2 == 0) ? u * len : -u * len;
                return new Vector3(c.x + u * len, c.y + y, 0f);
            }
            case 25:
            {
                int cols = Mathf.Max(2, Mathf.RoundToInt(Mathf.Sqrt(total)));
                int rows = Mathf.CeilToInt((float)total / cols);
                float gap = 0.72f;
                int gx = i % cols, gy = i / cols;
                return new Vector3(c.x + (gx - (cols - 1) * 0.5f) * gap, c.y + ((rows - 1) * 0.5f - gy) * gap, 0f);
            }
            case 26:
            {
                float r = Mathf.Max(1.6f, total * 0.24f);
                if (i == 0) return new Vector3(c.x + r * 0.12f, c.y + r * 0.45f, 0f);
                float a = Mathf.Lerp(Mathf.PI * 0.22f, Mathf.PI * 1.78f, (float)(i - 1) / Mathf.Max(1, total - 1));
                return new Vector3(c.x + Mathf.Cos(a) * r, c.y + Mathf.Sin(a) * r, 0f);
            }
            case 27:
            {
                int nucN = Mathf.Max(1, total / 12);
                if (i < nucN)
                {
                    float na = (float)i / nucN * Mathf.PI * 2f;
                    return new Vector3(c.x + Mathf.Cos(na) * 0.3f, c.y + Mathf.Sin(na) * 0.3f, 0f);
                }
                int j = i - nucN;
                float oa = (j % 3) * (Mathf.PI / 3f);
                float th = (float)(j / 3) / Mathf.Max(1, (total - nucN) / 3) * Mathf.PI * 2f;
                float rx = Mathf.Max(2f, total * 0.26f), ry = rx * 0.38f;
                float ex = Mathf.Cos(th) * rx, ey = Mathf.Sin(th) * ry;
                float x = ex * Mathf.Cos(oa) - ey * Mathf.Sin(oa);
                float y = ex * Mathf.Sin(oa) + ey * Mathf.Cos(oa);
                return new Vector3(c.x + x, c.y + y, 0f);
            }
            case 28:
            {
                float len = Mathf.Max(3f, total * 0.3f);
                int headN = Mathf.Max(4, total * 3 / 10);
                if (i < headN)
                {
                    float a = (float)i / headN * Mathf.PI * 2f;
                    return new Vector3(c.x - 0.45f + Mathf.Cos(a) * 0.55f, c.y - len * 0.4f + Mathf.Sin(a) * 0.38f, 0f);
                }
                int rest = Mathf.Max(1, total - headN);
                int j = i - headN;
                int flagN = Mathf.Max(1, rest / 4);
                if (j >= rest - flagN)
                {
                    float f = (float)(j - (rest - flagN)) / flagN;
                    return new Vector3(c.x + 0.1f + f * 0.55f, c.y + len * 0.5f - f * 0.45f, 0f);
                }
                float t2 = (float)j / Mathf.Max(1, rest - flagN);
                return new Vector3(c.x + 0.1f, c.y - len * 0.4f + t2 * len * 0.9f, 0f);
            }
            case 29:
            {
                float s = Mathf.Max(1.6f, total * 0.22f);
                if (i == 0) return new Vector3(c.x, c.y - s, 0f);
                t = (float)(i - 1) / Mathf.Max(1, total - 1);
                if (t < 0.65f)
                {
                    float a = Mathf.Lerp(Mathf.PI * 1.15f, -Mathf.PI * 0.25f, t / 0.65f);
                    return new Vector3(c.x + Mathf.Cos(a) * s * 0.6f, c.y + s * 0.35f + Mathf.Sin(a) * s * 0.55f, 0f);
                }
                float tt = (t - 0.65f) / 0.35f;
                return new Vector3(c.x, c.y + s * 0.35f - tt * s * 0.7f, 0f);
            }
            case 30:
            {
                float width = Mathf.Max(3f, total * 0.5f);
                t = total > 1 ? (float)i / (total - 1) : 0.5f;
                float x = (t - 0.5f) * width;
                float y;
                if (t < 0.42f || t > 0.68f) y = 0f;
                else if (t < 0.5f) y = (t - 0.42f) / 0.08f * 1.6f;
                else if (t < 0.56f) y = 1.6f - (t - 0.5f) / 0.06f * 2.5f;
                else if (t < 0.62f) y = -0.9f + (t - 0.56f) / 0.06f * 1.3f;
                else y = 0.4f - (t - 0.62f) / 0.06f * 0.4f;
                return new Vector3(c.x + x, c.y + y, 0f);
            }
            default:
                return new Vector3(c.x + (i - (total - 1) * 0.5f) * 0.75f, c.y, 0f);
        }
    }

    [HideFromIl2Cpp]
    private Clone Spawn(Vector3 pos, bool shadow)
    {
        PlayerControl src = PlayerControl.LocalPlayer;
        if (src == null) return null;

        GameObject go = Acquire(src);
        if (go == null) return null;

        int idx = _clones.Count;
        Reinit(go, src, idx * 10);
        if (NocturneConfig.CloneNaked.Value) Strip(go);
        pos.z = CloneZ;
        go.transform.position = pos;
        go.transform.localScale = src.gameObject.transform.localScale * NocturneConfig.LobbyCloneScale.Value;
        go.SetActive(true);

        Clone e = new Clone { Go = go, Shadow = shadow };
        CapturePose(e, go);
        _clones.Add(e);
        if (!shadow) _regular.Add(e);
        return e;
    }

    private static void CapturePose(Clone e, GameObject go)
    {
        SpriteRenderer[] rends = go.GetComponentsInChildren<SpriteRenderer>(true);
        var pose = new Vector3[rends.Length];
        for (int i = 0; i < rends.Length; i++)
            if (rends[i] != null) pose[i] = rends[i].transform.localPosition;
        e.Rends = rends;
        e.Pose = pose;
    }

    private GameObject Acquire(PlayerControl src)
    {
        while (_pool.Count > 0)
        {
            GameObject g = _pool.Dequeue();
            if (g != null) return g;
        }
        return MakeClone(src);
    }

    private static GameObject MakeClone(PlayerControl src)
    {
        PlayerControl realLocal = PlayerControl.LocalPlayer;
        GameObject go = Object.Instantiate(src.gameObject);
        go.name = "NocturneLobbyClone";

        try
        {
            foreach (MonoBehaviour mb in go.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null) continue;
                string tn;
                try { tn = mb.GetIl2CppType().Name; } catch { tn = string.Empty; }
                if (tn == "CosmeticsLayer" || tn == "HatParent" || tn == "VisorLayer" || tn == "SkinLayer"
                    || tn == "SpriteAnim" || tn == "SpriteAnimNodes" || tn == "SpriteAnimNodeSync"
                    || tn == "PlayerAnimations") continue;
                mb.enabled = false;
            }
        }
        catch { }

        try
        {
            PlayerControl pc = go.GetComponent<PlayerControl>();
            if (pc != null && PlayerControl.AllPlayerControls != null) PlayerControl.AllPlayerControls.Remove(pc);
            if (realLocal != null && PlayerControl.LocalPlayer != realLocal) PlayerControl.LocalPlayer = realLocal;
            if (pc != null && pc.cosmetics != null && pc.cosmetics.nameText != null)
                ((Component)pc.cosmetics.nameText).gameObject.SetActive(false);
        }
        catch { }

        try
        {
            foreach (Component comp in go.GetComponentsInChildren<Component>(true))
            {
                switch (comp)
                {
                    case InnerNetObject net: net.NetId = uint.MaxValue; break;
                    case Collider2D col: col.enabled = false; break;
                    case AudioSource au: au.enabled = false; break;
                    case Rigidbody2D rb: rb.isKinematic = true; break;
                }
            }
        }
        catch { }

        return go;
    }

    private static void Reinit(GameObject go, PlayerControl src, int off)
    {
        try
        {
            PlayerControl pc = go.GetComponent<PlayerControl>();
            if (pc != null && pc.cosmetics != null && src.Data != null && src.Data.DefaultOutfit != null)
            {
                NetworkedPlayerInfo.PlayerOutfit o = src.Data.DefaultOutfit;
                int over = NocturneConfig.LobbyCloneColorId.Value;
                int color = over >= 0 ? over : o.ColorId;
                pc.cosmetics.SetHat(o.HatId, color);
                pc.cosmetics.SetSkin(o.SkinId, color);
                pc.cosmetics.SetVisor(o.VisorId, color);
                pc.cosmetics.SetColor(color);
            }
        }
        catch { }

        CopySprites(src.gameObject, go, off);
    }

    private static void CopySprites(GameObject src, GameObject clone, int off)
    {
        try
        {
            SpriteRenderer[] s = src.GetComponentsInChildren<SpriteRenderer>(true);
            SpriteRenderer[] d = clone.GetComponentsInChildren<SpriteRenderer>(true);
            var map = new Dictionary<string, SpriteRenderer>(s.Length);
            Transform sroot = src.transform;
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == null) continue;
                string key = PathOf(s[i].transform, sroot);
                if (!map.ContainsKey(key)) map[key] = s[i];
            }
            Transform droot = clone.transform;
            for (int i = 0; i < d.Length; i++)
            {
                if (d[i] == null) continue;
                if (!map.TryGetValue(PathOf(d[i].transform, droot), out SpriteRenderer sr) || sr == null) continue;
                d[i].sprite = sr.sprite;
                d[i].color = sr.color;
                d[i].flipX = sr.flipX;
                d[i].flipY = sr.flipY;
                d[i].sortingOrder = sr.sortingOrder + off;
                Transform st = sr.transform;
                Transform dt = d[i].transform;
                dt.localPosition = st.localPosition;
                dt.localRotation = st.localRotation;
                dt.localScale = st.localScale;
            }
        }
        catch { }
    }

    private static string PathOf(Transform t, Transform root)
    {
        string p = t.name;
        Transform cur = t.parent;
        while (cur != null && cur != root)
        {
            p = cur.name + "/" + p;
            cur = cur.parent;
        }
        return p;
    }

    [HideFromIl2Cpp]
    private void Remove(Clone e)
    {
        Pool(e.Go);
        _clones.Remove(e);
        if (!e.Shadow) _regular.Remove(e);
        if (_drag == e) _drag = null;
        if (_shadow == e) _shadow = null;
    }

    private void ClearRegular()
    {
        for (int i = _regular.Count - 1; i >= 0; i--)
        {
            Pool(_regular[i].Go);
            _clones.Remove(_regular[i]);
        }
        _regular.Clear();
        _drag = null;
    }

    internal void ClearAll()
    {
        foreach (Clone e in _clones) Pool(e.Go);
        _clones.Clear();
        _regular.Clear();
        _trail.Clear();
        _drag = null;
        _shadow = null;
    }

    private void Pool(GameObject go)
    {
        if (go == null) return;
        go.SetActive(false);
        if (_pool.Count < 80) _pool.Enqueue(go);
    }

    [HideFromIl2Cpp]
    private Clone Nearest(Vector3 world)
    {
        Clone best = null;
        float bestD = PickRadius;
        for (int i = 0; i < _clones.Count; i++)
        {
            Clone e = _clones[i];
            if (e.Go == null) continue;
            float d = Vector2.Distance(e.Go.transform.position, world);
            if (d < bestD) { bestD = d; best = e; }
        }
        return best;
    }

    private static Vector3 MouseWorld()
    {
        if (Camera.main == null) return Vector3.zero;
        Vector3 v = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        v.z = 0f;
        return v;
    }

    public void OnDestroy()
    {
        foreach (Clone e in _clones) if (e.Go != null) e.Go.SetActive(false);
        _clones.Clear();
        _regular.Clear();
    }
}
