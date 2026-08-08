using UnityEngine;

namespace Nocturne;

public sealed class NocturneMenuButton : MonoBehaviour
{
    private const float W = 158f, H = 40f;

    private GUIStyle _label;
    private float _pressAt = -1f;

    private static float _x = -1f, _y = -1f;
    private static bool _loaded;

    private bool _grabbing;
    private bool _dragging;
    private Vector2 _grab;
    private Vector2 _down;

    private static float _menuAt = -99f;
    private static bool _atMenu;

    private static bool AtMainMenu()
    {
        float t = Time.unscaledTime;
        if (t - _menuAt >= 0.4f)
        {
            _menuAt = t;
            try { _atMenu = Object.FindObjectOfType<MainMenuManager>() != null; }
            catch { _atMenu = false; }
        }
        return _atMenu;
    }

    private static void EnsurePos()
    {
        if (_loaded) return;
        _loaded = true;
        _x = NocturneConfig.MenuButtonX != null ? NocturneConfig.MenuButtonX.Value : -1f;
        _y = NocturneConfig.MenuButtonY != null ? NocturneConfig.MenuButtonY.Value : -1f;
    }

    private static Rect Box()
    {
        EnsurePos();
        if (_x >= 0f && _y >= 0f) return new Rect(_x, _y, W, H);
        return new Rect(22f, HudManager.Instance == null ? 112f : 70f, W, H);
    }

    private static bool Visible()
    {
        if (NocturneConfig.MenuButton == null || !NocturneConfig.MenuButton.Value || NocturneMenu.Rebinding) return false;
        return HudManager.Instance != null || AtMainMenu();
    }

    private static bool ReadPointer(out Vector2 gui, out bool down, out bool held, out bool up)
    {
        down = held = up = false;
        Vector2 sp = Input.mousePosition;
        bool has;
        if (Input.touchCount > 0)
        {
            Touch t = Input.GetTouch(0);
            sp = t.position;
            down = t.phase == TouchPhase.Began;
            up = t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled;
            held = down || t.phase == TouchPhase.Moved || t.phase == TouchPhase.Stationary;
            has = true;
        }
        else
        {
            down = Input.GetMouseButtonDown(0);
            up = Input.GetMouseButtonUp(0);
            held = Input.GetMouseButton(0);
            has = down || up || held;
        }
        gui = new Vector2(sp.x, Screen.height - sp.y);
        return has;
    }

    public void Update()
    {
        if (!Visible()) { _grabbing = false; _dragging = false; return; }

        ReadPointer(out Vector2 gui, out bool down, out bool held, out bool up);
        Rect box = Box();

        if (down && box.Contains(gui))
        {
            _grabbing = true;
            _dragging = false;
            _grab = gui - new Vector2(box.x, box.y);
            _down = gui;
            return;
        }

        if (!_grabbing) return;

        if (up)
        {
            if (!_dragging && box.Contains(gui)) { NocturneMenu.ToggleRequest = true; _pressAt = Time.unscaledTime; }
            else if (_dragging) { NocturneConfig.MenuButtonX.Value = _x; NocturneConfig.MenuButtonY.Value = _y; }
            _grabbing = false;
            _dragging = false;
            return;
        }

        if (!held) { _grabbing = false; _dragging = false; return; }

        if (!_dragging && (gui - _down).sqrMagnitude > 64f) _dragging = true;
        if (_dragging)
        {
            _x = Mathf.Clamp(gui.x - _grab.x, 2f, Screen.width - W - 2f);
            _y = Mathf.Clamp(gui.y - _grab.y, 2f, Screen.height - H - 2f);
        }
    }

    internal void DrawGui()
    {
        if (!Visible() || Event.current.type != EventType.Repaint) return;

        EnsureStyles();
        NocturnePalette p = NocturneStyle.Current;
        Color accent = Patches.NocturneLobbyTheme.LobbyAccent(p.Accent);
        bool open = NocturneMenu.Opened;
        Rect r = Box();
        bool hover = _dragging || r.Contains(Event.current.mousePosition);

        float press = 0f;
        if (_pressAt >= 0f)
        {
            press = 1f - Mathf.Clamp01((Time.unscaledTime - _pressAt) / 0.16f);
            if (press <= 0f) _pressAt = -1f;
        }

        float glow = (open ? 0.22f : hover ? 0.14f : 0.07f) + press * 0.20f;

        NocturneStyle.FillRounded(new Rect(r.x - 2f, r.y + 3f, r.width + 4f, r.height + 4f), new Color(0f, 0f, 0f, 0.30f), 13);
        NocturneStyle.FillRounded(r, new Color(p.Window.r, p.Window.g, p.Window.b, 0.82f), 12);
        NocturneStyle.FillRounded(r, A(accent, glow), 12);
        NocturneStyle.FillRounded(new Rect(r.x, r.y, r.width, r.height * 0.5f), A(Color.white, 0.04f), 12);
        NocturneStyle.StrokeRounded(r, A(accent, open || _dragging ? 0.75f : 0.5f), 12, 1);

        float ix = r.x + 17f, iy = r.center.y;
        Color bar = open ? accent : p.Text;
        for (int i = -1; i <= 1; i++)
            NocturneStyle.FillRounded(new Rect(ix, iy - 1.4f + i * 6f, 20f, 2.8f), bar, 1);

        string txt = NocturneText.T("МЕНЮ", "MENU");
        GUI.color = new Color(0f, 0f, 0f, 0.45f);
        GUI.Label(new Rect(r.x + 48f, r.y + 1f, r.width - 52f, r.height), txt, _label);
        GUI.color = p.Text;
        GUI.Label(new Rect(r.x + 47f, r.y, r.width - 52f, r.height), txt, _label);
        GUI.color = Color.white;
    }

    private static Color A(Color c, float a) => new Color(c.r, c.g, c.b, a);

    private void EnsureStyles()
    {
        if (_label != null) return;
        _label = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleLeft,
            fontStyle = FontStyle.Bold,
            fontSize = 18
        };
        _label.normal.textColor = Color.white;
    }
}
