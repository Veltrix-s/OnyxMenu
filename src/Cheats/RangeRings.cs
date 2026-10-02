using System;
using UnityEngine;

namespace Nocturne;

internal static class RangeRings
{
    private const float ScanGap = 0.083f;
    private const float Span = 1.75f;

    private static readonly int[] Sizes = { 128, 256, 512, 1024 };
    private static readonly GUIStyle[] Sty = new GUIStyle[4];

    private static readonly Color KillCol = new Color(1f, 0.30f, 0.34f);
    private static readonly Color ReportCol = new Color(0.36f, 0.78f, 1f);
    private static readonly Color VentCol = new Color(0.74f, 0.52f, 1f);

    private static readonly PlayerControl[] Tp = new PlayerControl[16];
    private static readonly float[] Ta = new float[16];
    private static readonly bool[] Tin = new bool[16];

    private static Color32[] _buf;
    private static int _bake;
    private static int _row;
    private static int _bakeFrame = -1;

    private static int _frame = -1;
    private static float _scanAt;
    private static float _killR;
    private static float _reportR;
    private static bool _canKill;
    private static bool _canVent;
    private static bool _hns;
    private static bool _own;
    private static bool _anyone;
    private static bool _wasReady;
    private static float _ready;
    private static float _flash;
    private static int _prim = -1;

    internal static void Draw()
    {
        if (!NocturneConfig.RangeRings.Value || !NocturneStyle.Painting)
            return;

        if (_bake < Sizes.Length)
        {
            Bake();
            return;
        }

        if (ShipStatus.Instance == null || MeetingHud.Instance != null || ExileController.Instance != null)
            return;
        if (IntroCutscene.Instance != null || Minigame.Instance != null)
            return;

        PlayerControl me = PlayerControl.LocalPlayer;
        if (me == null || me.Data == null || me.Data.IsDead || me.inVent)
            return;

        MapBehaviour map = MapBehaviour.Instance;
        if (map != null && map.isActiveAndEnabled)
            return;

        Camera cam = Camera.main;
        if (cam == null) return;

        float now = NocturneStyle.Now;
        if (_frame != Time.frameCount)
        {
            _frame = Time.frameCount;
            if (now >= _scanAt)
            {
                _scanAt = now + ScanGap;
                Scan(me);
            }
            Step(me, NocturneStyle.Dt);
        }

        Vector2 pos = me.GetTruePosition();
        Vector2 c = NocturneTracers.ToScreen(cam, pos);
        Vector2 n = NocturneTracers.ToScreen(cam, pos + Vector2.right);
        float ppu = Mathf.Sqrt((c.x - n.x) * (c.x - n.x) + (c.y - n.y) * (c.y - n.y));
        if (ppu < 4f) return;

        bool lite = NocturneStyle.Lite;
        float op = NocturneConfig.RingOpacity.Value / 100f;
        float pulse = 0.9f + 0.1f * Mathf.Sin(now * 2.2f);

        if (!_hns && NocturneConfig.RingReport.Value)
            Ring(c, _reportR * ppu, ReportCol, 0.55f * op * pulse, now * 24f, lite);

        if (_canKill && NocturneConfig.RingKill.Value)
        {
            float rk = _killR * ppu;
            Ring(c, rk, KillCol, op * (0.38f + 0.62f * _ready) * pulse, now * (16f + 34f * _ready), lite);
            if (_flash > 0f && !lite)
                Ring(c, rk * (1f + 0.09f * (1f - _flash)), KillCol, op * _flash * 0.75f, 0f, true);
        }

        if (_canVent && NocturneConfig.RingVent.Value)
            Ring(c, 0.75f * ppu, VentCol, 0.8f * op * pulse, now * 70f, lite);

        if (_canKill && NocturneConfig.RingMarks.Value)
        {
            float ma = op * (0.45f + 0.55f * _ready);
            float rm = 0.6f * ppu;
            float gs = 2f * ppu;

            for (int i = 0; i < Tp.Length; i++)
            {
                PlayerControl t = Tp[i];
                if (t == null || Ta[i] < 0.02f)
                    continue;

                float a = Ta[i] * (i == _prim ? 1.25f : 0.6f) * ma;
                Vector2 sp = NocturneTracers.ToScreen(cam, t.GetTruePosition());
                NocturneStyle.Glow(new Rect(sp.x - gs * 0.5f, sp.y - gs * 0.5f, gs, gs), new Color(KillCol.r, KillCol.g, KillCol.b, 0.6f * a));
                Ring(sp, rm * (1.25f - 0.25f * Ta[i]), KillCol, a, now * 150f + i * 41f, lite);
            }
        }
    }

    private static void Ring(Vector2 c, float r, Color col, float a, float deg, bool still)
    {
        if (a < 0.01f || r < 6f)
            return;
        if (c.x + r < 0f || c.x - r > Screen.width || c.y + r < 0f || c.y - r > Screen.height)
            return;

        float fx = Mathf.Max(c.x, Screen.width - c.x);
        float fy = Mathf.Max(c.y, Screen.height - c.y);
        float inner = r * 0.84f;
        if (inner * inner > fx * fx + fy * fy)
            return;

        int lod = 0;
        while (lod < Sizes.Length - 1 && Sizes[lod] * 1.4f < r * 2f)
            lod++;

        float n = Sizes[lod];
        float side = r * n / (n * 0.5f - 3f);
        Matrix4x4 m = GUI.matrix;
        if (!still)
            GUIUtility.RotateAroundPivot(deg, c);
        GUI.color = new Color(col.r, col.g, col.b, Mathf.Min(a, 1f));
        GUI.Box(new Rect(c.x - side * 0.5f, c.y - side * 0.5f, side, side), GUIContent.none, Sty[lod]);
        GUI.matrix = m;
        GUI.color = Color.white;
    }

    private static void Scan(PlayerControl me)
    {
        RoleBehaviour role = me.Data.Role;
        GameManager gm = GameManager.Instance;

        _canKill = role != null && role.CanUseKillButton;
        _canVent = (role != null && role.CanVent) || NocturneRoleBuffs.VentAny;
        _hns = gm != null && gm.IsHideAndSeek();
        _reportR = me.MaxReportDistance;
        _killR = NocturneRoleBuffs.Radius();
        _anyone = NocturneRoleBuffs.KillAny;
        _own = NocturneRoleBuffs.Reach || _anyone || (NocturneConfig.BuffVanishKill.Value && NocturneRoleBuffs.Faded());

        for (int i = 0; i < Tin.Length; i++)
            Tin[i] = false;
        _prim = -1;
        if (!_canKill || !NocturneConfig.RingMarks.Value)
            return;

        Vector2 mp = me.GetTruePosition();
        float best = float.MaxValue;
        var it = PlayerControl.AllPlayerControls.GetEnumerator();
        while (it.MoveNext())
        {
            PlayerControl t = it.Current;
            if (t == null || t == me || t.Data == null || !Hittable(role, t))
                continue;

            Vector2 tp = t.GetTruePosition();
            float dx = tp.x - mp.x;
            float dy = tp.y - mp.y;
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            if (d > _killR) continue;
            if (!_own && d > 0.001f && PhysicsHelpers.AnyNonTriggersBetween(mp, new Vector2(dx / d, dy / d), d, Constants.ShipAndObjectsMask))
                continue;

            int s = Slot(t);
            if (s < 0) continue;
            Tin[s] = true;
            if (d < best)
            {
                best = d;
                _prim = s;
            }
        }
    }

    private static bool Hittable(RoleBehaviour role, PlayerControl t)
    {
        NetworkedPlayerInfo di = t.Data;
        if (_own)
        {
            RoleBehaviour tr = di.Role;
            return !di.IsDead && !di.Disconnected && !t.inVent && (_anyone || tr == null || (int)tr.TeamType != 1);
        }
        return role.IsValidTarget(di) && t.Collider.enabled;
    }

    private static int Slot(PlayerControl t)
    {
        int free = -1;
        for (int i = 0; i < Tp.Length; i++)
        {
            if (Tp[i] == t) return i;
            if (free < 0 && Tp[i] == null)
                free = i;
        }

        if (free >= 0)
        {
            Tp[free] = t;
            Ta[free] = 0f;
        }
        return free;
    }

    private static void Step(PlayerControl me, float dt)
    {
        bool ready = me.killTimer <= 0.05f;
        _ready = Mathf.MoveTowards(_ready, ready ? 1f : 0f, dt * 5f);
        if (ready && !_wasReady)
            _flash = 1f;
        _wasReady = ready;
        _flash = Mathf.Max(0f, _flash - dt * 2.2f);

        for (int i = 0; i < Tp.Length; i++)
        {
            if (Tp[i] == null) continue;
            Ta[i] = Mathf.MoveTowards(Ta[i], Tin[i] ? 1f : 0f, dt * 6f);
            if (Ta[i] <= 0f && !Tin[i])
                Tp[i] = null;
        }
    }

    private static void Bake()
    {
        if (_bakeFrame == Time.frameCount) return;
        _bakeFrame = Time.frameCount;

        int n = Sizes[_bake];
        _buf ??= new Color32[n * n];

        int left = 36000;
        while (_row < n && left > 0)
        {
            Row(n, _row++);
            left -= n;
        }
        if (_row < n) return;

        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        tex.SetPixels32(_buf);
        tex.Apply();

        var st = new GUIStyle();
        st.normal.background = tex;
        Sty[_bake] = st;

        _buf = null;
        _row = 0;
        _bake++;
    }

    private static void Row(int n, int y)
    {
        float cc = (n - 1) * 0.5f;
        float rad = n * 0.5f - 3f;
        float band = rad * 0.10f;
        float dy = n - 1 - y - cc;
        int o = y * n;

        Color32 px = default;
        px.r = 255;
        px.g = 255;
        px.b = 255;

        for (int x = 0; x < n; x++)
        {
            float dx = x - cc;
            float e = rad - MathF.Sqrt(dx * dx + dy * dy);
            float ae = MathF.Abs(e);
            float a = 0f;

            float cov = 1.4f - ae;
            if (cov > 0f)
                a = (cov > 1f ? 1f : cov) * 0.5f;

            if (e > 0f && e < band)
            {
                float g = 1f - e / band;
                g *= g * 0.26f;
                if (g > a)
                    a = g;
            }

            if (ae < 9f)
            {
                float v = -MathF.Atan2(dy, dx);
                v -= MathF.Floor(v / MathF.PI) * MathF.PI;

                float m = 0f;
                if (v < Span)
                {
                    m = 1f - v / Span;
                    m *= MathF.Sqrt(m);
                }
                else if (v > MathF.PI - 0.05f)
                    m = (v - (MathF.PI - 0.05f)) / 0.05f;

                if (m > 0f)
                {
                    float core = 1.4f + 1.7f * m - ae;
                    if (core > 0f)
                    {
                        float c = (core > 1f ? 1f : core) * m;
                        if (c > a)
                            a = c;
                    }

                    float halo = 0.34f * m * (1f - ae / 9f);
                    if (halo > a)
                        a = halo;
                }
            }

            px.a = (byte)(a * 255f + 0.5f);
            _buf[o + x] = px;
        }
    }
}
