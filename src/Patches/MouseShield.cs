using HarmonyLib;
using UnityEngine;

namespace Nocturne.Patches;

internal static class MouseShield
{
    private static readonly Rect[] _rects = new Rect[16];
    private static int _count;
    private static int _rectFrame = -10;
    private static int _stateFrame = -10;
    private static bool _over;
    private static readonly bool[] _held = new bool[3];
    private static readonly bool[] _down = new bool[3];

    internal static void Cover(Rect r)
    {
        Event e = Event.current;
        if (e == null || e.type != EventType.Repaint) return;

        int frame = Time.frameCount;
        if (_rectFrame != frame)
        {
            _rectFrame = frame;
            _count = 0;
        }
        if (_count == _rects.Length) return;

        Matrix4x4 m = GUI.matrix;
        _rects[_count++] = Ui.R(r.m_XMin * m.m00 + m.m03, r.m_YMin * m.m11 + m.m13, r.m_Width * m.m00, r.m_Height * m.m11);
    }

    internal static int Count => _rectFrame == Time.frameCount ? _count : 0;

    internal static Rect At(int i) => _rects[i];

    internal static bool Over
    {
        get
        {
            if (Time.frameCount - _rectFrame > 1) return false;

            Vector3 mp = Input.mousePosition;
            var pt = Ui.V(mp.x, Screen.height - mp.y);
            for (int i = 0; i < _count; i++)
                if (Ui.In(_rects[i], pt)) return true;
            return false;
        }
    }

    internal static bool Blocked(int button)
    {
        int frame = Time.frameCount;
        if (_stateFrame != frame)
        {
            _stateFrame = frame;
            _over = Over;

            for (int i = 0; i < _held.Length; i++)
            {
                bool down = Input.GetMouseButton(i);
                if (!down)
                    _held[i] = false;
                else if (!_down[i])
                    _held[i] = _over;
                _down[i] = down;
            }
        }

        return _held[button] || (!_down[button] && _over);
    }
}

[HarmonyPatch(typeof(Controller), nameof(Controller.Update))]
internal static class MouseShieldPatch
{
    public static void Postfix(Controller __instance)
    {
        if (!MouseShield.Blocked(0)) return;
        if (Controller.currentTouchType != Controller.TouchType.Mouse) return;

        var touch = __instance.Touches[0];
        touch.TouchStart = false;
        touch.TouchEnd = false;
        touch.IsDown = false;
        touch.active = false;
    }
}
