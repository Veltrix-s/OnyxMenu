using System;
using System.Collections.Generic;
using HarmonyLib;
using InnerNet;
using UnityEngine;
using UnityEngine.Events;
using Object = UnityEngine.Object;

namespace Nocturne.Patches;

internal static class NocturneLobbyAnim
{
    private sealed class TState { public Transform T; public Vector3 Pos; public Vector3 Scale; }

    private static readonly Dictionary<int, TState> orig = new Dictionary<int, TState>();
    private static readonly List<int> stale = new List<int>(16);
    private static readonly List<Transform> targets = new List<Transform>(24);
    private static readonly List<Transform> roots = new List<Transform>(24);
    private static readonly HashSet<int> ids = new HashSet<int>();

    private const float PanelDelay = 3f;
    private const float PanelDuration = 0.94f;
    private const float SlideDistance = 5.95f;

    private static int mgrId, gameId, collectedMgr;
    private static float introAt = -1f, nextRefresh;
    private static bool ready;

    internal static void Apply(GameStartManager m)
    {
        if (m == null) return;
        if (!NocturneConfig.LobbyAnims.Value) { RestoreAll(); Reset(); return; }

        Arm(m);
        float el = Mathf.Max(0f, Time.unscaledTime - introAt);
        if (el <= PanelDelay + PanelDuration + 0.2f) AnimatePanel(m, el);
        AnimateStart(m.StartButton, Mathf.Clamp01(el / 0.85f));
    }

    private static void Arm(GameStartManager m)
    {
        int id = m.GetInstanceID();
        int gid = AmongUsClient.Instance == null ? 0 : ((InnerNetClient)AmongUsClient.Instance).GameId;
        if (mgrId == id && gameId == gid && introAt >= 0f) return;
        mgrId = id;
        gameId = gid;
        introAt = Time.unscaledTime;
        ready = false;
        collectedMgr = 0;
        nextRefresh = 0f;
    }

    private static void Reset()
    {
        mgrId = 0;
        gameId = 0;
        collectedMgr = 0;
        introAt = -1f;
        ready = false;
    }

    private static void AnimatePanel(GameStartManager m, float el)
    {
        Collect(m);
        float progress = Mathf.Clamp01((el - PanelDelay) / PanelDuration);
        float eased = EaseOutCubic(progress);
        Vector3 worldOffset = new Vector3((1f - eased) * SlideDistance, 0f, 0f);

        for (int i = 0; i < roots.Count; i++)
        {
            Transform t = roots[i];
            if (t == null) continue;
            TState s = Capture(t);
            Transform parent = t.parent;
            Vector3 local = parent == null ? worldOffset : parent.InverseTransformVector(worldOffset);
            t.localPosition = s.Pos + local;
            t.localScale = s.Scale;
        }
    }

    private static void AnimateStart(PassiveButton b, float intro)
    {
        if (b == null) return;
        Transform t = ((Component)b).transform;
        TState s = Capture(t);

        float eased = EaseOutBack(intro, 1.16f);
        float pop = Mathf.Sin(Mathf.Clamp01(intro) * Mathf.PI) * 0.040f;
        float breath = Mathf.Sin(Time.unscaledTime * 2.3f) * 0.011f;
        float glow = Mathf.Sin(Time.unscaledTime * 4.1f) * 0.003f;
        float scale = Mathf.Lerp(0.925f, 1f, eased) + pop + breath + glow;
        t.localScale = s.Scale * scale;
    }

    private static void Collect(GameStartManager m)
    {
        int id = m.GetInstanceID();
        if (ready && collectedMgr == id) return;
        if (Time.unscaledTime < nextRefresh) return;

        targets.Clear();
        ids.Clear();
        collectedMgr = id;

        if (m.LobbyInfoPane != null)
        {
            Add(m.LobbyInfoPane.InfoPaneBackground);
            Add(m.LobbyInfoPane.CopyCodeText);
        }
        Add(m.GameRoomButton);
        Add(m.RoomCodeHeader);
        Add(m.GameRoomNameCode);
        Add(m.PlayerCounter);
        Add(m.LocalLabel);
        Add(m.RulesPresetText);
        Add(m.privatePublicPanelText);
        Add(m.MapImage);
        Add(m.HostInfoPanelButtons);
        Add(m.ClientInfoPanelButtons);
        Add(m.HostPrivacyButtons);
        Add(m.ClientPrivacyValue);
        Add(m.InviteFriendsButton);
        Add(m.ShareOnDiscordButton);
        AddB(m.EditButton);
        AddB(m.HostPublicButton);
        AddB(m.HostPrivateButton);
        AddB(m.HostViewButton);
        AddB(m.ClientViewButton);
        AddVisuals(m);

        roots.Clear();
        for (int i = 0; i < targets.Count; i++)
        {
            Transform t = targets[i];
            if (t != null && !HasParent(t)) roots.Add(t);
        }

        ready = targets.Count > 8;
        nextRefresh = Time.unscaledTime + 0.3f;
    }

    private static bool HasParent(Transform t)
    {
        for (int i = 0; i < targets.Count; i++)
        {
            Transform o = targets[i];
            if (o != null && o != t && t.IsChildOf(o)) return true;
        }
        return false;
    }

    private static void Add(GameObject go) { if (go != null) AddT(go.transform); }
    private static void Add(SpriteRenderer r) { if (r != null) AddT(r.transform); }
    private static void Add(TMPro.TMP_Text t) { if (t != null) AddT(t.transform); }
    private static void AddB(PassiveButton b) { if (b != null) AddT(((Component)b).transform); }

    private static void AddT(Transform t)
    {
        if (t != null && ids.Add(t.GetInstanceID())) targets.Add(t);
    }

    private static void AddVisuals(GameStartManager m)
    {
        if (m.LobbyInfoPane == null) return;
        Component root = m.LobbyInfoPane;
        foreach (TMPro.TMP_Text t in root.GetComponentsInChildren<TMPro.TMP_Text>(true))
            if (t != null && IsRightPanel(t)) AddT(t.transform);
        foreach (SpriteRenderer r in root.GetComponentsInChildren<SpriteRenderer>(true))
            if (r != null && IsRightPanel(r)) AddT(r.transform);
        foreach (PassiveButton b in root.GetComponentsInChildren<PassiveButton>(true))
            if (b != null && IsRightPanel(b)) AddT(((Component)b).transform);
    }

    private static bool IsRightPanel(Component c)
    {
        Renderer r = c.GetComponent<Renderer>();
        Vector3 pos = r != null ? r.bounds.center : c.transform.position;
        return pos.x > 1.05f && pos.y < 2.45f && pos.y > -2.75f;
    }

    private static TState Capture(Transform t)
    {
        int id = t.GetInstanceID();
        if (!orig.TryGetValue(id, out TState s) || s.T == null)
        {
            s = new TState { T = t, Pos = t.localPosition, Scale = t.localScale };
            orig[id] = s;
        }
        return s;
    }

    private static void RestoreAll()
    {
        stale.Clear();
        foreach (KeyValuePair<int, TState> pair in orig)
        {
            TState s = pair.Value;
            if (s == null || s.T == null) { stale.Add(pair.Key); continue; }
            s.T.localPosition = s.Pos;
            s.T.localScale = s.Scale;
        }
        for (int i = 0; i < stale.Count; i++) orig.Remove(stale[i]);
    }

    private static float EaseOutCubic(float t)
    {
        t = 1f - Mathf.Clamp01(t);
        return 1f - t * t * t;
    }

    private static float EaseOutBack(float t, float overshoot)
    {
        t = Mathf.Clamp01(t);
        float v = t - 1f;
        return 1f + (overshoot + 1f) * v * v * v + overshoot * v * v;
    }
}

internal static class NocturneActionHover
{
    private sealed class SState { public Transform T; public Vector3 Scale; public float Last = float.NaN; }

    private const float ClickDur = 0.18f;
    private static readonly Dictionary<int, SState> baseScale = new Dictionary<int, SState>();
    private static readonly Dictionary<int, Collider2D> colliders = new Dictionary<int, Collider2D>();
    private static readonly List<int> stale = new List<int>(4);
    private static int mgrId, gameId;
    private static int hookedHostView = -1, hookedClientView = -1, hookedEdit = -1;
    private static float viewUntil, viewStart, editUntil, editStart;
    private static Camera cam;
    private static float nextCam;

    internal static void Apply(GameStartManager m)
    {
        if (m == null || LobbyBehaviour.Instance == null) { RestoreAll(); Reset(); return; }

        int id = m.GetInstanceID();
        int gid = AmongUsClient.Instance == null ? 0 : ((InnerNetClient)AmongUsClient.Instance).GameId;
        if (id != mgrId || gid != gameId) { RestoreAll(); Reset(); mgrId = id; gameId = gid; }

        PassiveButton hostView = m.HostViewButton, clientView = m.ClientViewButton, edit = m.EditButton;
        float now = Time.time;
        bool anyClick = now < viewUntil || now < editUntil;
        if (!Vis(hostView) && !Vis(clientView) && !Vis(edit) && !anyClick) return;

        Hook(hostView, ref hookedHostView, OnView);
        Hook(clientView, ref hookedClientView, OnView);
        Hook(edit, ref hookedEdit, OnEdit);

        Vector2 mouse = MouseWorld();
        Anim(hostView, viewStart, viewUntil, Hovered(hostView, mouse));
        Anim(clientView, viewStart, viewUntil, Hovered(clientView, mouse));
        Anim(edit, editStart, editUntil, Hovered(edit, mouse));
    }

    internal static void RestoreAll()
    {
        stale.Clear();
        foreach (KeyValuePair<int, SState> pair in baseScale)
        {
            SState s = pair.Value;
            if (s == null || s.T == null) { stale.Add(pair.Key); continue; }
            s.T.localScale = s.Scale;
            s.Last = 1f;
        }
        for (int i = 0; i < stale.Count; i++) baseScale.Remove(stale[i]);
    }

    private static void Reset()
    {
        mgrId = 0; gameId = 0;
        hookedHostView = hookedClientView = hookedEdit = -1;
        viewUntil = viewStart = editUntil = editStart = 0f;
        colliders.Clear();
    }

    private static Camera Cam()
    {
        if (cam == null || Time.unscaledTime >= nextCam) { cam = Camera.main; nextCam = Time.unscaledTime + 1f; }
        return cam;
    }

    private static bool Vis(PassiveButton b) => b != null && ((Component)b).gameObject.activeInHierarchy;

    private static void Hook(PassiveButton b, ref int hooked, Action cb)
    {
        if (b == null || (UnityEvent)(object)b.OnClick == null) return;
        int id = b.GetInstanceID();
        if (id == hooked) return;
        ((UnityEvent)b.OnClick).AddListener(cb);
        hooked = id;
    }

    private static void OnView() { viewStart = Time.time; viewUntil = Time.time + ClickDur; }
    private static void OnEdit() { editStart = Time.time; editUntil = Time.time + ClickDur; }

    private static Vector2 MouseWorld()
    {
        Camera c = Cam();
        if (c == null) return Vector2.zero;
        Vector3 p = c.ScreenToWorldPoint(Input.mousePosition);
        return new Vector2(p.x, p.y);
    }

    private static Collider2D Coll(PassiveButton b)
    {
        int id = b.GetInstanceID();
        if (colliders.TryGetValue(id, out Collider2D c)) return c;
        c = ((Component)b).GetComponent<Collider2D>();
        colliders[id] = c;
        return c;
    }

    private static bool Hovered(PassiveButton b, Vector2 mouse)
    {
        if (!Vis(b)) return false;
        Collider2D c = Coll(b);
        if (c != null) return c.OverlapPoint(mouse);
        Camera cam2 = Cam();
        if (cam2 == null) return false;
        Vector3 sp = cam2.WorldToScreenPoint(((Component)b).transform.position);
        Vector3 mp = Input.mousePosition;
        return Mathf.Abs(mp.x - sp.x) <= 112f && Mathf.Abs(mp.y - sp.y) <= 34f;
    }

    private static void Anim(PassiveButton b, float clickStart, float clickUntil, bool hover)
    {
        if (b == null) return;
        Transform t = ((Component)b).transform;
        int id = b.GetInstanceID();
        if (!baseScale.TryGetValue(id, out SState s) || s.T == null)
        {
            s = new SState { T = t, Scale = t.localScale };
            baseScale[id] = s;
        }

        float scale = 1f;
        float now = Time.time;
        if (now < clickUntil)
        {
            float k = Mathf.Clamp01((now - clickStart) / ClickDur);
            scale *= 1f - Mathf.Sin(k * Mathf.PI) * 0.085f;
        }
        if (hover) scale *= 1f + (0.5f + 0.5f * Mathf.Sin(now * 3.7f)) * 0.032f;

        if (float.IsNaN(s.Last) || Mathf.Abs(scale - s.Last) > 0.0001f)
        {
            t.localScale = s.Scale * scale;
            s.Last = scale;
        }
    }
}

public sealed class NocturneLobbyAnimDriver : MonoBehaviour
{
    private GameStartManager _mgr;
    private float _nextLookup;

    public void LateUpdate()
    {
        if (!NocturneConfig.LobbyAnims.Value || LobbyBehaviour.Instance == null)
        {
            NocturneActionHover.RestoreAll();
            _mgr = null;
            return;
        }

        if (_mgr == null && Time.unscaledTime >= _nextLookup)
        {
            _mgr = Object.FindObjectOfType<GameStartManager>();
            _nextLookup = Time.unscaledTime + 0.35f;
        }

        if (_mgr != null)
        {
            NocturneLobbyAnim.Apply(_mgr);
            NocturneActionHover.Apply(_mgr);
        }
    }
}
