using UnityEngine;

namespace Nocturne;

public sealed class NocturneMouseTools : MonoBehaviour
{
    private static PlayerControl _sel;
    private float _lastPick = -99f;
    private bool _dragging;
    private float _lastTp = -99f;
    private EdgeCollider2D _dragBnd;
    private static readonly Color Outline = new Color(0.30f, 0.62f, 1f, 1f);

    internal static PlayerControl Selected => _sel;

    private static bool GameChatOpen()
    {
        try
        {
            HudManager hud = HudManager.Instance;
            ChatController chat = hud != null ? hud.Chat : null;
            return chat != null && chat.IsOpenOrOpening;
        }
        catch { return false; }
    }

    public void Update()
    {
        bool tp = NocturneConfig.MouseTeleport.Value;
        bool sel = NocturneConfig.MouseSelect.Value;
        bool drag = NocturneConfig.SelfDrag.Value;
        if ((!tp && !sel && !drag) || AmongUsClient.Instance == null || PlayerControl.LocalPlayer == null)
        {
            _dragging = false;
            Clear();
            return;
        }

        if (NocturneChatWindow.Open || NocturneMenu.Typing || GameChatOpen()) { _dragging = false; return; }

        Camera cam = Camera.main;
        if (cam == null) return;

        if (tp && Input.GetMouseButton(1))
        {
            Vector2 pos = World(cam, PlayerControl.LocalPlayer);
            try { PlayerControl.LocalPlayer.NetTransform.RpcSnapTo(pos); } catch { }
        }

        bool grabbed = drag && SelfDrag(cam);

        if (!sel) { Clear(); return; }

        if (_sel != null && (_sel.Data == null || _sel.Data.Disconnected)) Clear();
        if (Input.GetKeyDown(KeyCode.Escape)) { Clear(); return; }

        if (!grabbed && Input.GetMouseButtonDown(0) && Time.unscaledTime - _lastPick > 0.2f)
        {
            _lastPick = Time.unscaledTime;
            Pick(cam);
        }

        Resize();
    }

    private bool SelfDrag(Camera cam)
    {
        PlayerControl me = PlayerControl.LocalPlayer;
        if (me == null || me.inVent) { _dragging = false; return false; }

        Vector2 m = World(cam, me);

        if (Input.GetMouseButtonDown(0))
        {
            _dragging = me.CanMove && !me.walkingToVent && !me.onLadder;
            if (_dragging) _dragBnd = Boundary();
        }
        else if (Input.GetMouseButtonUp(0)) _dragging = false;

        if (!_dragging || !me.CanMove) return _dragging;
        if (_dragBnd != null && !_dragBnd.OverlapPoint(m)) return true;

        Vector2 cur = me.transform.position;
        Vector2 next = NocturneConfig.SelfDragSmooth.Value ? Vector2.MoveTowards(cur, m, NocturneConfig.SelfDragSpeed.Value * 0.05f) : m;
        if ((next - cur).sqrMagnitude < 0.0001f) return true;
        if (Time.time - _lastTp < 0.05f) return true;

        _lastTp = Time.time;
        try { me.NetTransform.RpcSnapTo(next); } catch { }
        return true;
    }

    private static Vector2 World(Camera cam, PlayerControl me)
    {
        Vector3 sp = Input.mousePosition;
        sp.z = me != null ? cam.WorldToScreenPoint(me.transform.position).z : -cam.transform.position.z;
        Vector3 w = cam.ScreenToWorldPoint(sp);
        return new Vector2(w.x, w.y);
    }

    private static EdgeCollider2D Boundary()
    {
        try
        {
            ShipStatus s = ShipStatus.Instance;
            if (s == null) return null;
            Transform b = null;
            if (s.TryCast<SkeldShipStatus>() != null) b = s.transform.Find("starfield");
            else if (s.TryCast<MiraShipStatus>() != null) b = s.transform.Find("CloudGen");
            else if (s.TryCast<PolusShipStatus>() != null) b = s.transform.Find("OuterBoundary");
            else if (s.TryCast<AirshipStatus>() != null) b = s.transform.Find("Boundary");
            else if (s.TryCast<FungleShipStatus>() != null) b = s.transform.Find("GhostBoundary");
            return b != null ? b.GetComponent<EdgeCollider2D>() : null;
        }
        catch { return null; }
    }

    private void Pick(Camera cam)
    {
        Vector2 m = cam.ScreenToWorldPoint(Input.mousePosition);
        PlayerControl best = null;
        float bestD = 1.6f;
        var e = PlayerControl.AllPlayerControls.GetEnumerator();
        while (e.MoveNext())
        {
            PlayerControl p = e.Current;
            if (p == null || p.Data == null || p.Data.Disconnected) continue;
            float d = Vector2.Distance(p.transform.position, m);
            if (d < bestD) { bestD = d; best = p; }
        }

        if (best == null) return;
        if (best == _sel) { Clear(); return; }
        Clear();
        _sel = best;
        Outlined(best, true);
    }

    private static void Resize()
    {
        if (_sel == null) return;
        float w = Input.mouseScrollDelta.y;
        if (Mathf.Abs(w) < 0.01f) return;
        Transform t = _sel.transform;
        float s = Mathf.Clamp(t.localScale.x + (w > 0f ? 0.15f : -0.15f), 0.25f, 2f);
        t.localScale = new Vector3(s, s, 1f);
    }

    internal static void Clear()
    {
        if (_sel == null) return;
        Outlined(_sel, false);
        _sel = null;
    }

    private static void Outlined(PlayerControl p, bool on)
    {
        try
        {
            CosmeticsLayer c = p != null ? p.cosmetics : null;
            if (c == null) return;
            if (on)
            {
                c.SetOutline(true, new Il2CppSystem.Nullable<Color>(Outline));
            }
            else
            {
                c.SetOutline(true, new Il2CppSystem.Nullable<Color>(Color.clear));
                c.SetOutline(false, (Il2CppSystem.Nullable<Color>)null);
            }
        }
        catch { }
    }
}
