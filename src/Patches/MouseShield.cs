using HarmonyLib;
using UnityEngine;

namespace Nocturne.Patches;

internal static class MouseShield
{
    private static readonly Rect[] _rects = new Rect[16];
    private static int _count;
    private static int _rectFrame = -10;
    private static int _stateFrame = -10;
    private static bool _blocked;
    private static bool _held;
    private static bool _wasDown;

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

    internal static bool Blocked
    {
        get
        {
            int frame = Time.frameCount;
            if (_stateFrame == frame) return _blocked;
            _stateFrame = frame;

            bool down = Input.GetMouseButton(0);
            bool over = false;
            if (frame - _rectFrame <= 1)
            {
                Vector3 mp = Input.mousePosition;
                var pt = Ui.V(mp.x, Screen.height - mp.y);
                for (int i = 0; i < _count && !over; i++)
                    over = Ui.In(_rects[i], pt);
            }

            if (!down)
                _held = false;
            else if (!_wasDown)
                _held = over;
            _wasDown = down;

            _blocked = _held || (!down && over);
            return _blocked;
        }
    }
}

[HarmonyPatch(typeof(Controller), nameof(Controller.Update))]
internal static class MouseShieldPatch
{
    public static void Postfix(Controller __instance)
    {
        if (!MouseShield.Blocked) return;
        if (Controller.currentTouchType != Controller.TouchType.Mouse) return;

        var touch = __instance.Touches[0];
        touch.TouchStart = false;
        touch.TouchEnd = false;
        touch.IsDown = false;
        touch.active = false;
    }
}
