using System;
using System.Collections.Generic;
using System.IO;
using Il2CppInterop.Runtime.Attributes;
using UnityEngine;

namespace Nocturne;

internal enum NocturneEventCat
{
    Kill, Meeting, Sabotage, Vent, Role, Report, Join, Other
}

public sealed class NocturneEventLog : MonoBehaviour
{
    private sealed class Row
    {
        public string Clock; public string Text; public NocturneNotifyKind Kind; public NocturneEventCat Cat; public float H;
    }

    private const int Cap = 200;
    private const float HeadH = 30f;
    private const float RowH = 18f;

    private static readonly List<Row> _rows = new List<Row>(Cap);
    private static readonly List<Row> _chat = new List<Row>(Cap);
    private static bool ConsoleOn => NocturneConfig.EventConsole.Value;
    private static bool _stick = true;

    internal static void Add(string text, NocturneNotifyKind kind) => Add(text, kind, NocturneEventCat.Other);

    internal static void Add(string text, NocturneNotifyKind kind, NocturneEventCat cat)
    {
        if (string.IsNullOrEmpty(text))
            return;
        if (cat != NocturneEventCat.Other)
        {
            int mask = NocturneConfig.EventFilterMask.Value;
            if ((mask & (1 << (int)cat)) == 0) return;
        }
        while (_rows.Count >= Cap)
            _rows.RemoveAt(0);
        _rows.Add(new Row { Clock = DateTime.Now.ToString("HH:mm:ss"), Text = text, Kind = kind, Cat = cat });
        _stick = true;
    }

    private Rect _win = Ui.R(24f, 130f, 430f, 366f);
    private float _scroll;
    private bool _drag;
    private Vector2 _grab;
    private bool _built;
    private int _tab;
    private float _chatAt = -99f;
    private long _chatLen = -1L;
    private GUIStyle _clock, _row, _tabS;
    private readonly GUIContent _gc = new GUIContent();

    private float _measuredAt;

    [HideFromIl2Cpp]
    private List<Row> Active => _tab == 1 ? _chat : _rows;

    private void LoadChat()
    {
        _chat.Clear();
        try
        {
            string path = Patches.NocturneChatLog.FilePath;
            if (!File.Exists(path)) return;
            string[] all = File.ReadAllLines(path);
            int start = Ui.Max(0, all.Length - Cap);
            for (int i = start; i < all.Length; i++)
            {
                string s = all[i];
                if (!string.IsNullOrWhiteSpace(s)) _chat.Add(ParseChat(s));
            }
        }
        catch { }
    }

    private static Row ParseChat(string line)
    {
        string clock = string.Empty, text = line;
        try
        {
            if (line.StartsWith("[") && line.IndexOf(']') > 0)
            {
                int rb = line.IndexOf(']');
                string ts = line.Substring(1, rb - 1);
                int sp = ts.LastIndexOf(' ');
                clock = sp >= 0 ? ts.Substring(sp + 1) : ts;
                string rest = line.Substring(rb + 1).Trim();
                string[] parts = rest.Split(new[] { " | " }, StringSplitOptions.None);
                text = parts.Length >= 2 ? parts[0].Trim() + ": " + parts[parts.Length - 1].Trim() : rest;
            }
        }
        catch
        {
            text = line;
        }
        return new Row { Clock = clock, Text = text, Kind = NocturneNotifyKind.Info };
    }

    [HideFromIl2Cpp]
    private float RowHeight(Row r, float textW)
    {
        if (r.H > 0f)
            return r.H;
        _gc.text = r.Text;
        r.H = Ui.Max(RowH, _row.CalcHeight(_gc, textW) + 2f);
        return r.H;
    }

    private void Remeasure(float textW)
    {
        if (Ui.Abs(textW - _measuredAt) < 0.5f) return;
        _measuredAt = textW;
        for (int i = 0; i < _rows.Count; i++)
            _rows[i].H = 0f;
        for (int i = 0; i < _chat.Count; i++)
            _chat[i].H = 0f;
    }

    internal void DrawGui()
    {
        if (!ConsoleOn) return;
        Build();

        float sc = Ui.Clamp(Screen.height / 1080f, 1f, 2.4f);
        Matrix4x4 mtx = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(Ui.V3(sc, sc, 1f));
        try
        {
            float sw = Screen.width / sc, sh = Screen.height / sc;
            Event e = Event.current;
            NocturneStyle.ClampWindow(ref _win, sw, sh, 300f, 150f);
            Patches.MouseShield.Cover(_win);

            NocturnePalette p = NocturneStyle.Current;
            float w = _win.m_Width, h = _win.m_Height;

            NocturneStyle.FillRounded(_win, A(p.Window, 0.95f), 12);
            NocturneStyle.StrokeRounded(_win, A(p.Accent, 0.28f), 12, 1);
            NocturneStyle.Fill(Ui.R(_win.m_XMin + 10f, _win.m_YMin + HeadH - 1f, w - 20f, 1f), A(p.Accent, 0.35f));

            NocturneStyle.FillRounded(Ui.R(_win.m_XMin + 12f, _win.m_YMin + 9f, 3f, 12f), p.Accent, 1);
            Tab(Ui.R(_win.m_XMin + 20f, _win.m_YMin + 5f, 84f, 20f), 0, NocturneText.T("СОБЫТИЯ", "EVENTS"), p);
            Tab(Ui.R(_win.m_XMin + 108f, _win.m_YMin + 5f, 52f, 20f), 1, NocturneText.T("ЧАТ", "CHAT"), p);

            if (_tab == 1 && Time.unscaledTime - _chatAt > 1f)
            {
                _chatAt = Time.unscaledTime;
                try
                {
                    string cp = Patches.NocturneChatLog.FilePath;
                    long len = File.Exists(cp) ? new FileInfo(cp).Length : 0L;
                    if (len != _chatLen)
                    {
                        _chatLen = len;
                        bool bottom = _stick;
                        LoadChat();
                        _stick = bottom;
                    }
                }
                catch { }
            }

            List<Row> list = Active;

            _clock.normal.textColor = p.Muted;
            GUI.Label(Ui.R(_win.m_XMin + w - 150f, _win.m_YMin + 7f, 56f, 16f), list.Count + "/" + Cap, _clock);

            Rect cpy = Ui.R(_win.m_XMin + w - 80f, _win.m_YMin + 6f, 22f, 18f);
            Rect clr = Ui.R(_win.m_XMin + w - 54f, _win.m_YMin + 6f, 22f, 18f);
            Rect cls = Ui.R(_win.m_XMin + w - 28f, _win.m_YMin + 6f, 22f, 18f);
            if (Hover(cpy))
                NocturneStyle.FillRounded(cpy, A(p.Accent, 0.20f), 6);
            if (Hover(clr))
                NocturneStyle.FillRounded(clr, A(Ui.White, 0.07f), 6);
            if (Hover(cls))
                NocturneStyle.FillRounded(cls, A(Ui.C(0.9f, 0.3f, 0.3f, 1f), 0.25f), 6);
            Icon(cpy, NocturneIcon.Copy, Hover(cpy) ? p.Text : p.Muted);
            Icon(clr, NocturneIcon.Trash, Hover(clr) ? p.Text : p.Muted);
            Icon(cls, NocturneIcon.Close, Hover(cls) ? Ui.C(0.95f, 0.5f, 0.5f, 1f) : p.Muted);
            if (GUI.Button(cpy, NocturneStyle.Blank, GUIStyle.none)) CopyAll(list);
            if (GUI.Button(clr, NocturneStyle.Blank, GUIStyle.none))
                list.Clear();
            if (GUI.Button(cls, NocturneStyle.Blank, GUIStyle.none))
            {
                NocturneConfig.EventConsole.Value = false;
                return;
            }

            float filterH = 0f;
            if (_tab == 0)
            {
                filterH = 42f;
                string[] fru = { "Килл", "Собр", "Сабо", "Вент", "Роль", "Реп", "Вход" };
                string[] fen = { "Kill", "Meet", "Sabo", "Vent", "Role", "Rep", "Join" };
                int cols = 4;
                float gap = 4f;
                float fw = (w - 12f - (cols - 1) * gap) / cols;
                int mask = NocturneConfig.EventFilterMask.Value;
                for (int i = 0; i < fru.Length; i++)
                {
                    Rect fr = Ui.R(_win.m_XMin + 6f + (i % cols) * (fw + gap), _win.m_YMin + HeadH + 4f + (i / cols) * 20f, fw, 18f);
                    bool fon = (mask & (1 << i)) != 0;
                    NocturneStyle.FillRounded(fr, fon ? A(p.Accent, 0.22f) : A(Ui.White, 0.05f), 5);
                    _tabS.normal.textColor = fon ? p.Accent : A(p.Muted, 0.8f);
                    GUI.Label(fr, NocturneText.T(fru[i], fen[i]), _tabS);
                    if (GUI.Button(fr, NocturneStyle.Blank, GUIStyle.none))
                        NocturneConfig.EventFilterMask.Value = mask ^ (1 << i);
                }
            }

            Rect body = Ui.R(_win.m_XMin + 6f, _win.m_YMin + HeadH + 4f + filterH, w - 12f, h - HeadH - 10f - filterH);
            if (e != null && NocturneStyle.Is(EventType.ScrollWheel) && Ui.In(body, e.mousePosition))
            {
                _scroll += e.delta.y * 18f;
                _stick = false;
                e.Use();
            }

            float textW = body.m_Width - 66f;
            Remeasure(textW);
            float total = 0f;
            for (int i = 0; i < list.Count; i++)
                total += RowHeight(list[i], textW);

            float maxScroll = Ui.Max(0f, total - body.m_Height);
            if (_stick) _scroll = maxScroll;
            _scroll = Ui.Clamp(_scroll, 0f, maxScroll);
            if (_scroll >= maxScroll - 1f)
                _stick = true;

            GUI.BeginGroup(body);
            Event ge = Event.current;
            float y = -_scroll;
            for (int i = 0; i < list.Count; i++)
            {
                Row r = list[i];
                float rh = RowHeight(r, textW);
                if (y + rh >= 0f && y <= body.m_Height)
                {
                    Rect rr = Ui.R(0f, y, body.m_Width, rh);
                    bool hov = ge != null && Ui.In(rr, ge.mousePosition);
                    if (hov)
                        NocturneStyle.Fill(rr, A(p.Accent, 0.10f));
                    _clock.normal.textColor = A(p.Muted, 0.8f);
                    GUI.Label(Ui.R(2f, y + 1f, 58f, RowH), r.Clock, _clock);
                    _row.normal.textColor = KindColor(r.Kind, p);
                    GUI.Label(Ui.R(62f, y + 1f, textW, rh), r.Text, _row);
                    if (hov && NocturneStyle.Is(EventType.MouseDown) && ge.button == 0)
                    {
                        Copy(r.Clock + "  " + r.Text);
                        ge.Use();
                    }
                }
                y += rh;
            }
            GUI.EndGroup();

            if (maxScroll > 1f && total > 0f)
            {
                float th = Ui.Max(24f, body.m_Height * (body.m_Height / total));
                float ty = body.m_YMin + (body.m_Height - th) * (_scroll / maxScroll);
                NocturneStyle.FillRounded(Ui.R(Ui.Right(body) - 3f, ty, 3f, th), A(p.Accent, 0.5f), 1);
            }

            Drag(e, Ui.R(_win.m_XMin + 166f, _win.m_YMin, Ui.Max(0f, w - 166f - 66f), HeadH));
        }
        finally { GUI.matrix = mtx; }
    }

    [HideFromIl2Cpp]
    private void Tab(Rect r, int idx, string label, NocturnePalette p)
    {
        bool active = _tab == idx;
        if (active)
            NocturneStyle.FillRounded(r, A(p.Accent, 0.16f), 6);
        else if (Hover(r))
            NocturneStyle.FillRounded(r, A(Ui.White, 0.05f), 6);
        _tabS.normal.textColor = active ? p.Accent : A(p.Muted, 0.9f);
        GUI.Label(r, label, _tabS);
        if (GUI.Button(r, NocturneStyle.Blank, GUIStyle.none) && _tab != idx)
        {
            _tab = idx;
            _scroll = 0f;
            _stick = true;
            _measuredAt = -1f;
            if (idx == 1)
            {
                _chatAt = -99f;
                _chatLen = -1L;
            }
        }
    }

    private void Drag(Event e, Rect head)
    {
        if (e == null) return;
        if (e.type == EventType.MouseDown && e.button == 0 && Ui.In(head, e.mousePosition))
        {
            _drag = true;
            _grab = e.mousePosition - Ui.V(_win.m_XMin, _win.m_YMin);
            e.Use();
        }
        else if (_drag && e.type == EventType.MouseDrag)
        {
            _win.m_XMin = e.mousePosition.x - _grab.x;
            _win.m_YMin = e.mousePosition.y - _grab.y;
            e.Use();
        }
        else if (_drag && e.type == EventType.MouseUp)
            _drag = false;
    }

    private static void CopyAll(List<Row> list)
    {
        if (list.Count == 0)
            return;
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < list.Count; i++)
            sb.Append(list[i].Clock).Append("  ").Append(list[i].Text).Append('\n');
        Copy(sb.ToString());
    }

    private static void Copy(string s)
    {
        try
        {
            GUIUtility.systemCopyBuffer = s;
        }
        catch { }
        NocturneToast.Push(NocturneText.T("Скопировано", "Copied"), null, 1.6f, NocturneNotifyKind.Success);
    }

    private static void Icon(Rect r, NocturneIcon icon, Color c) => NocturneIcons.Draw(icon, Ui.R(r.m_XMin + r.m_Width / 2f - 7f, r.m_YMin + r.m_Height / 2f - 7f, 14f, 14f), c);

    private static bool Hover(Rect r) => Event.current != null && Ui.In(r, Event.current.mousePosition);

    private static Color KindColor(NocturneNotifyKind k, NocturnePalette p) => k switch
    {
        NocturneNotifyKind.Success => Ui.C(0.32f, 0.85f, 0.46f, 1f),
        NocturneNotifyKind.Danger => Ui.C(0.93f, 0.36f, 0.36f, 1f),
        NocturneNotifyKind.Warning => Ui.C(0.96f, 0.76f, 0.20f, 1f),
        _ => Ui.Mix(p.Text, p.Accent, 0.55f),
    };

    private void Build()
    {
        if (_built) return;
        _built = true;
        _clock = new GUIStyle(GUI.skin.label) { fontSize = 11, alignment = TextAnchor.UpperLeft, clipping = TextClipping.Clip };
        _row = new GUIStyle(GUI.skin.label) { fontSize = 12, richText = true, wordWrap = true, clipping = TextClipping.Clip, alignment = TextAnchor.UpperLeft };
        _tabS = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Clip };
    }

    private static Color A(Color c, float a) => Ui.C(c.r, c.g, c.b, a);
}
