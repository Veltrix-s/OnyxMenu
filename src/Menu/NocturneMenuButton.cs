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
            try
            {
                _atMenu = Object.FindObjectOfType<MainMenuManager>() != null;
            }
            catch
            {
                _atMenu = false;
            }
        }
        return _atMenu;
    }

    private static void EnsurePos()
    {
        if (_loaded)
            return;
        _loaded = true;
        _x = NocturneConfig.MenuButtonX.Value;
        _y = NocturneConfig.MenuButtonY.Value;
    }

    private static Rect Box()
    {
        EnsurePos();
        if (_x >= 0f && _y >= 0f)
            return Ui.R(_x, _y, W, H);
        return Ui.R(22f, HudManager.InstanceExists ? 70f : 112f, W, H);
    }

    private static bool Visible()
    {
        if (!NocturneConfig.MenuButton.Value || NocturneMenu.Rebinding)
            return false;
        return HudManager.InstanceExists || AtMainMenu();
    }

    private static bool ReadPointer(out Vector2 gui, out bool down, out bool held, out bool up)
    {
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
        gui = Ui.V(sp.x, Screen.height - sp.y);
        return has;
    }

    public void Update()
    {
        if (!Visible())
        {
            _grabbing = false;
            _dragging = false;
            return;
        }

        ReadPointer(out Vector2 gui, out bool down, out bool held, out bool up);
        Rect box = Box();

        if (down && Ui.In(box, gui))
        {
            _grabbing = true;
            _dragging = false;
            _grab = gui - Ui.V(box.m_XMin, box.m_YMin);
            _down = gui;
            return;
        }

        if (!_grabbing) return;

        if (up)
        {
            if (!_dragging && Ui.In(box, gui))
            {
                NocturneMenu.ToggleRequest = true;
                _pressAt = Time.unscaledTime;
            }
            else if (_dragging)
            {
                NocturneConfig.MenuButtonX.Value = _x;
                NocturneConfig.MenuButtonY.Value = _y;
            }
            _grabbing = false;
            _dragging = false;
            return;
        }

        if (!held)
        {
            _grabbing = false;
            _dragging = false;
            return;
        }

        if (!_dragging && (gui - _down).sqrMagnitude > 64f)
            _dragging = true;
        if (_dragging)
        {
            _x = Ui.Clamp(gui.x - _grab.x, 2f, Screen.width - W - 2f);
            _y = Ui.Clamp(gui.y - _grab.y, 2f, Screen.height - H - 2f);
        }
    }

    internal void DrawGui()
    {
        NocturnePet.DrawJoystick(Event.current);
        NocturnePet.DrawPaint(Event.current);
        if (!Visible() || !NocturneStyle.Painting)
            return;

        EnsureStyles();
        NocturnePalette p = NocturneStyle.Current;
        Color accent = Patches.NocturneLobbyTheme.LobbyAccent(p.Accent);
        bool open = NocturneMenu.Opened;
        Rect r = Box();
        Patches.MouseShield.Cover(r);
        bool hover = _dragging || Ui.In(r, NocturneStyle.Mouse);

        float press = 0f;
        if (_pressAt >= 0f)
        {
            press = 1f - Ui.Clamp01((Time.unscaledTime - _pressAt) / 0.16f);
            if (press <= 0f) _pressAt = -1f;
        }

        float glow = (open ? 0.22f : hover ? 0.14f : 0.07f) + press * 0.20f;

        NocturneStyle.FillRounded(Ui.R(r.m_XMin - 2f, r.m_YMin + 3f, r.m_Width + 4f, r.m_Height + 4f), Ui.C(0f, 0f, 0f, 0.30f), 13);
        NocturneStyle.FillRounded(r, Ui.C(p.Window.r, p.Window.g, p.Window.b, 0.82f), 12);
        NocturneStyle.FillRounded(r, A(accent, glow), 12);
        NocturneStyle.FillRounded(Ui.R(r.m_XMin, r.m_YMin, r.m_Width, r.m_Height * 0.5f), A(Ui.White, 0.04f), 12);
        NocturneStyle.StrokeRounded(r, A(accent, open || _dragging ? 0.75f : 0.5f), 12, 1);

        float ix = r.m_XMin + 17f, iy = Ui.Mid(r).y;
        Color bar = open ? accent : p.Text;
        for (int i = -1; i <= 1; i++)
            NocturneStyle.FillRounded(Ui.R(ix, iy - 1.4f + i * 6f, 20f, 2.8f), bar, 1);

        string txt = NocturneText.T("МЕНЮ", "MENU");
        NocturneStyle.Tint = Ui.C(0f, 0f, 0f, 0.45f);
        GUI.Label(Ui.R(r.m_XMin + 48f, r.m_YMin + 1f, r.m_Width - 52f, r.m_Height), txt, _label);
        NocturneStyle.Tint = p.Text;
        GUI.Label(Ui.R(r.m_XMin + 47f, r.m_YMin, r.m_Width - 52f, r.m_Height), txt, _label);
        NocturneStyle.Tint = Ui.White;
    }

    private static Color A(Color c, float a) => Ui.C(c.r, c.g, c.b, a);

    private void EnsureStyles()
    {
        if (_label != null) return;
        _label = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleLeft,
            fontStyle = FontStyle.Bold,
            fontSize = 18
        };
        _label.normal.textColor = Ui.White;
    }
}
