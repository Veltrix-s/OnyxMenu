using UnityEngine;

namespace Nocturne;

internal sealed class PaneScroll
{
    internal float Pos;
    internal bool Stick = true;

    private bool _drag;
    private float _grab;

    internal void Step(Rect body, float total, Event e)
    {
        if (e != null && e.type == EventType.ScrollWheel && Ui.In(body, e.mousePosition))
        {
            Pos += e.delta.y * 18f;
            Stick = false;
            e.Use();
        }

        float max = Ui.Max(0f, total - body.m_Height);
        if (Stick)
            Pos = max;
        Pos = Ui.Clamp(Pos, 0f, max);
        if (Pos >= max - 1f)
            Stick = true;
    }

    internal void Bar(Rect body, float total, Event e)
    {
        float max = total - body.m_Height;
        if (max <= 1f)
        {
            _drag = false;
            return;
        }

        float th = Ui.Max(30f, body.m_Height * (body.m_Height / total));
        float travel = body.m_Height - th;
        float ty = body.m_YMin + travel * (Pos / max);
        Rect track = Ui.R(Ui.Right(body) - 14f, body.m_YMin, 14f, body.m_Height);
        Vector2 m = e != null ? e.mousePosition : Ui.V(-1f, -1f);
        EventType t = e != null ? e.type : EventType.Layout;

        bool down = t == EventType.MouseDown && e.button == 0 && Ui.In(track, m);
        if (down)
        {
            _grab = m.y >= ty && m.y <= ty + th ? m.y - ty : th * 0.5f;
            _drag = true;
        }
        if (_drag && (down || t == EventType.MouseDrag))
        {
            float k = travel > 0f ? Ui.Clamp01((m.y - _grab - body.m_YMin) / travel) : 0f;
            Pos = k * max;
            Stick = Pos >= max - 1f;
            ty = body.m_YMin + travel * k;
            e.Use();
        }
        else if (_drag && t == EventType.MouseUp)
            _drag = false;

        bool hot = _drag || Ui.In(track, m);
        NocturneStyle.FillRounded(Ui.R(Ui.Right(body) - 8f, ty, 4f, th), Ui.C(1f, 1f, 1f, hot ? 0.40f : 0.20f), 2);
    }
}

internal static class Pane
{
    internal const float HeadH = 44f;
    internal const float ToolsW = 106f;

    internal static bool Hover(Rect r) => Event.current != null && Ui.In(r, Event.current.mousePosition);

    internal static Color A(Color c, float a) => Ui.C(c.r, c.g, c.b, a);

    internal static GUIStyle Label(int size, FontStyle weight, TextAnchor anchor, bool wrap = false, bool rich = false)
    {
        var s = new GUIStyle(GUI.skin.label)
        {
            fontSize = size,
            fontStyle = weight,
            alignment = anchor,
            wordWrap = wrap,
            richText = rich,
            clipping = TextClipping.Clip
        };
        s.padding = NocturneStyle.Offset(0, 0, 0, 0);
        return s;
    }

    internal static void Drag(ref Rect win, ref bool drag, ref Vector2 grab, Event e, Rect head)
    {
        if (e == null)
            return;
        if (e.type == EventType.MouseDown && e.button == 0 && Ui.In(head, e.mousePosition))
        {
            drag = true;
            grab = e.mousePosition - Ui.V(win.m_XMin, win.m_YMin);
            e.Use();
        }
        else if (drag && e.type == EventType.MouseDrag)
        {
            win.m_XMin = e.mousePosition.x - grab.x;
            win.m_YMin = e.mousePosition.y - grab.y;
            e.Use();
        }
        else if (drag && e.type == EventType.MouseUp)
            drag = false;
    }

    internal static bool IconButton(Rect r, NocturneIcon icon, bool danger)
    {
        NocturnePalette p = NocturneStyle.Current;
        bool hot = Hover(r);
        if (hot)
            NocturneStyle.FillRounded(r, danger ? Ui.C(0.92f, 0.30f, 0.30f, 0.24f) : Ui.C(1f, 1f, 1f, 0.09f), 8);
        Color c = hot ? (danger ? Ui.C(1f, 0.60f, 0.58f, 1f) : p.Text) : A(p.Muted, 0.95f);
        NocturneIcons.Draw(icon, Ui.R(r.m_XMin + (r.m_Width - 14f) * 0.5f, r.m_YMin + (r.m_Height - 14f) * 0.5f, 14f, 14f), c);
        return GUI.Button(r, NocturneStyle.Blank, GUIStyle.none);
    }

    internal static int Tools(Rect win, float y)
    {
        float x = Ui.Right(win) - 38f;
        int hit = 0;
        if (IconButton(Ui.R(x - 60f, y, 28f, 28f), NocturneIcon.Copy, false))
            hit = 1;
        if (IconButton(Ui.R(x - 30f, y, 28f, 28f), NocturneIcon.Trash, false))
            hit = 2;
        if (IconButton(Ui.R(x, y, 28f, 28f), NocturneIcon.Close, true))
            hit = 3;
        return hit;
    }

    internal static void Tile(Rect r, NocturneIcon icon)
    {
        NocturnePalette p = NocturneStyle.Current;
        NocturneStyle.FillRounded(r, A(p.Accent, 0.16f), 9);
        NocturneStyle.StrokeRounded(r, A(p.Accent, 0.34f), 9, 1);
        NocturneIcons.Draw(icon, Ui.R(r.m_XMin + (r.m_Width - 15f) * 0.5f, r.m_YMin + (r.m_Height - 15f) * 0.5f, 15f, 15f), p.Accent);
    }

    internal static void Counter(Rect r, string text, GUIStyle st)
    {
        Skin.Glass(r, 10);
        st.normal.textColor = A(NocturneStyle.Current.Muted, 0.95f);
        GUI.Label(r, text, st);
    }

    internal static void Empty(Rect body, NocturneIcon icon, string title, string hint, GUIStyle ts, GUIStyle hs)
    {
        if (!NocturneStyle.Painting)
            return;

        NocturnePalette p = NocturneStyle.Current;
        float top = Ui.Round(body.m_YMin + body.m_Height * 0.5f - 54f);
        Rect disc = Ui.R(Ui.Round(body.m_XMin + body.m_Width * 0.5f - 20f), top, 40f, 40f);
        Skin.Glass(disc, 20);
        NocturneIcons.Draw(icon, Ui.R(disc.m_XMin + 10f, disc.m_YMin + 10f, 20f, 20f), A(p.Muted, 0.9f));
        ts.normal.textColor = A(p.Text, 0.9f);
        GUI.Label(Ui.R(body.m_XMin, top + 50f, body.m_Width, 20f), title, ts);
        hs.normal.textColor = A(p.Muted, 0.85f);
        GUI.Label(Ui.R(body.m_XMin + 28f, top + 72f, body.m_Width - 56f, 34f), hint, hs);
    }
}
