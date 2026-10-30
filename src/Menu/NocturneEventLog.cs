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
    private const float RowMin = 24f;
    private const float SegA = 98f;
    private const float SegB = 62f;

    private static readonly string[] FilterRu = { "Килл", "Собр", "Сабо", "Вент", "Роль", "Реп", "Вход" };
    private static readonly string[] FilterEn = { "Kill", "Meet", "Sabo", "Vent", "Role", "Rep", "Join" };

    private static readonly Color[] FilterTone =
    {
        Ui.C(0.94f, 0.36f, 0.34f, 1f), Ui.C(0.98f, 0.76f, 0.26f, 1f), Ui.C(1f, 0.55f, 0.28f, 1f), Ui.C(0.67f, 0.55f, 0.98f, 1f),
        Ui.C(0.38f, 0.66f, 0.98f, 1f), Ui.C(0.22f, 0.83f, 0.75f, 1f), Ui.C(0.30f, 0.87f, 0.50f, 1f)
    };

    private static readonly List<Row> _rows = new List<Row>(Cap);
    private static readonly List<Row> _chat = new List<Row>(Cap);
    private static readonly PaneScroll _scroll = new PaneScroll();
    private static bool ConsoleOn => NocturneConfig.EventConsole.Value;

    internal static void Add(string text, NocturneNotifyKind kind) => Add(text, kind, NocturneEventCat.Other);

    internal static void Add(string text, NocturneNotifyKind kind, NocturneEventCat cat)
    {
        if (string.IsNullOrEmpty(text))
            return;
        if (cat != NocturneEventCat.Other)
        {
            int mask = NocturneConfig.EventFilterMask.Value;
            if ((mask & (1 << (int)cat)) == 0)
                return;
        }
        while (_rows.Count >= Cap)
            _rows.RemoveAt(0);
        _rows.Add(new Row { Clock = DateTime.Now.ToString("HH:mm:ss"), Text = text, Kind = kind, Cat = cat });
        _scroll.Stick = true;
    }

    private Rect _win = Ui.R(24f, 130f, 460f, 380f);
    private bool _drag;
    private Vector2 _grab;
    private bool _built;
    private int _tab;
    private float _chatAt = -99f;
    private long _chatLen = -1L;
    private GUIStyle _clock, _row, _seg, _chipS, _chip, _empty, _hint;
    private readonly GUIContent _gc = new GUIContent();
    private readonly float[] _chipW = new float[7];
    private bool _chipRu;
    private float _a;
    private float _px;
    private float _pw = SegA;
    private float _measuredAt;
    private int _countN = -1;
    private int _countTab = -1;
    private string _countText = "";

    [HideFromIl2Cpp]
    private List<Row> Active => _tab == 1 ? _chat : _rows;

    private void LoadChat()
    {
        _chat.Clear();
        try
        {
            string path = Patches.NocturneChatLog.FilePath;
            if (!File.Exists(path))
                return;
            string[] all = File.ReadAllLines(path);
            int start = Ui.Max(0, all.Length - Cap);
            for (int i = start; i < all.Length; i++)
            {
                string s = all[i];
                if (!string.IsNullOrWhiteSpace(s))
                    _chat.Add(ParseChat(s));
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
        r.H = Ui.Max(RowMin, _row.CalcHeight(_gc, textW) + 8f);
        return r.H;
    }

    private void Remeasure(float textW)
    {
        if (Ui.Abs(textW - _measuredAt) < 0.5f)
            return;
        _measuredAt = textW;
        for (int i = 0; i < _rows.Count; i++)
            _rows[i].H = 0f;
        for (int i = 0; i < _chat.Count; i++)
            _chat[i].H = 0f;
    }

    private void Chips()
    {
        _chipRu = NocturneText.IsRussian;
        string[] names = _chipRu ? FilterRu : FilterEn;
        for (int i = 0; i < _chipW.Length; i++)
        {
            _gc.text = names[i];
            _chipW[i] = Ui.Ceil(_chipS.CalcSize(_gc).x) + 29f;
        }
    }

    internal void DrawGui()
    {
        bool on = ConsoleOn;
        if (NocturneStyle.Painting)
            _a = Ui.MoveTowards(_a, on ? 1f : 0f, NocturneStyle.Dt * (on ? 7f : 10f));
        if (_a <= 0f && !on)
            return;
        Build();

        float sc = Ui.Clamp(Screen.height / 1080f, 1f, 2.4f);
        Matrix4x4 mtx = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(Ui.V3(sc, sc, 1f));
        try
        {
            float sw = Screen.width / sc, sh = Screen.height / sc;
            Event e = Event.current;
            NocturneStyle.ClampWindow(ref _win, sw, sh, 340f, 200f);

            float k = _a * _a * (3f - 2f * _a);
            Rect win = Ui.R(_win.m_XMin, _win.m_YMin + Ui.Round((1f - k) * 10f), _win.m_Width, _win.m_Height);
            Patches.MouseShield.Cover(win);
            NocturneStyle.Tint = Ui.C(1f, 1f, 1f, k);

            NocturnePalette p = NocturneStyle.Current;
            float w = win.m_Width, h = win.m_Height;
            float x0 = win.m_XMin, y0 = win.m_YMin;

            Skin.Drop(win, 0.38f);
            Skin.Frame(win, 16);

            Rect track = Ui.R(x0 + 12f, y0 + 8f, SegA + SegB + 6f, 28f);
            Skin.Glass(track, 14);
            float gx = _tab == 0 ? 0f : SegA;
            float gw = _tab == 0 ? SegA : SegB;
            if (NocturneStyle.Painting)
            {
                float s = 1f - Ui.Exp(-NocturneStyle.Dt * 18f);
                _px = Ui.Abs(gx - _px) < 0.25f ? gx : _px + (gx - _px) * s;
                _pw = Ui.Abs(gw - _pw) < 0.25f ? gw : _pw + (gw - _pw) * s;
            }
            Skin.Lit(Ui.R(track.m_XMin + 3f + Ui.Round(_px), track.m_YMin + 3f, Ui.Round(_pw), 22f), 11);
            Tab(Ui.R(track.m_XMin + 3f, track.m_YMin + 3f, SegA, 22f), 0, NocturneText.T("События", "Events"), p);
            Tab(Ui.R(track.m_XMin + 3f + SegA, track.m_YMin + 3f, SegB, 22f), 1, NocturneText.T("Чат", "Chat"), p);

            if (_tab == 1 && Time.unscaledTime - _chatAt > 1f)
            {
                _chatAt = Time.unscaledTime;
                string cp = Patches.NocturneChatLog.FilePath;
                long len = File.Exists(cp) ? new FileInfo(cp).Length : 0L;
                if (len != _chatLen)
                {
                    _chatLen = len;
                    bool bottom = _scroll.Stick;
                    LoadChat();
                    _scroll.Stick = bottom;
                }
            }

            List<Row> list = Active;

            if (list.Count != _countN || _tab != _countTab)
            {
                _countN = list.Count;
                _countTab = _tab;
                _countText = _countN + "/" + Cap;
            }
            Pane.Counter(Ui.R(x0 + w - Pane.ToolsW - 62f, y0 + 12f, 62f, 20f), _countText, _chip);

            int tool = Pane.Tools(win, y0 + 8f);
            if (tool == 1)
                CopyAll(list);
            else if (tool == 2)
                list.Clear();
            else if (tool == 3)
            {
                NocturneConfig.EventConsole.Value = false;
                return;
            }
            Skin.Hair(x0 + 4f, y0 + Pane.HeadH, w - 8f);

            float bodyTop = y0 + Pane.HeadH + 8f;
            if (_tab == 0)
            {
                if (_chipW[0] <= 0f || _chipRu != NocturneText.IsRussian)
                    Chips();

                string[] names = _chipRu ? FilterRu : FilterEn;
                int mask = NocturneConfig.EventFilterMask.Value;
                float cx = x0 + 12f;
                float cy = bodyTop;
                for (int i = 0; i < names.Length; i++)
                {
                    if (cx + _chipW[i] > x0 + w - 12f && cx > x0 + 12f)
                    {
                        cx = x0 + 12f;
                        cy += 28f;
                    }
                    Rect fr = Ui.R(cx, cy, _chipW[i], 22f);
                    bool fon = (mask & (1 << i)) != 0;
                    if (Chip(fr, names[i], FilterTone[i], fon, p))
                        NocturneConfig.EventFilterMask.Value = mask ^ (1 << i);
                    cx += _chipW[i] + 5f;
                }
                bodyTop = cy + 22f + 8f;
            }

            Rect body = Ui.R(x0 + 8f, bodyTop, w - 16f, Ui.Max(40f, y0 + h - 10f - bodyTop));
            float textW = body.m_Width - 102f;
            Remeasure(textW);
            float total = 6f;
            for (int i = 0; i < list.Count; i++)
                total += RowHeight(list[i], textW);
            _scroll.Step(body, total, e);

            if (list.Count == 0)
            {
                if (_tab == 1)
                    Pane.Empty(body, NocturneIcon.Chat, NocturneText.T("Чат пуст", "Chat is empty"), NocturneText.T("Лог чата появится здесь.", "The chat log shows up here."), _empty, _hint);
                else
                    Pane.Empty(body, NocturneIcon.List, NocturneText.T("Событий нет", "No events yet"), NocturneText.T("Включённые категории будут появляться здесь.", "Enabled categories show up here."), _empty, _hint);
            }
            else if (NocturneStyle.Painting || NocturneStyle.Is(EventType.MouseDown))
            {
                GUI.BeginGroup(body);
                Event ge = Event.current;
                float y = 3f - _scroll.Pos;
                for (int i = 0; i < list.Count; i++)
                {
                    Row r = list[i];
                    float rh = RowHeight(r, textW);
                    if (y + rh >= 0f && y <= body.m_Height)
                    {
                        Rect rr = Ui.R(2f, y, body.m_Width - 20f, rh);
                        bool hov = ge != null && Ui.In(rr, ge.mousePosition);
                        Color tone = KindColor(r.Kind, p);
                        if (hov)
                        {
                            NocturneStyle.FillRounded(rr, Ui.C(1f, 1f, 1f, 0.06f), 8);
                            NocturneIcons.Draw(NocturneIcon.Copy, Ui.R(Ui.Right(rr) - 22f, y + (rh - 12f) * 0.5f, 12f, 12f), Pane.A(p.Muted, 0.9f));
                        }
                        NocturneStyle.FillRounded(Ui.R(8f, y + 6f, 4f, rh - 12f), Pane.A(tone, r.Kind == NocturneNotifyKind.Info ? 0.6f : 0.9f), 2);
                        _clock.normal.textColor = Pane.A(p.Muted, 0.85f);
                        GUI.Label(Ui.R(20f, y + 5f, 58f, 16f), r.Clock, _clock);
                        _row.normal.textColor = Ui.Mix(p.Text, tone, r.Kind == NocturneNotifyKind.Info ? 0f : 0.10f);
                        GUI.Label(Ui.R(82f, y + 4f, textW, rh - 6f), r.Text, _row);
                        if (hov && NocturneStyle.Is(EventType.MouseDown) && ge.button == 0)
                        {
                            Copy(r.Clock + "  " + r.Text);
                            ge.Use();
                        }
                    }
                    y += rh;
                }
                GUI.EndGroup();
            }
            if (list.Count > 0)
                _scroll.Bar(body, total, e);

            Pane.Drag(ref _win, ref _drag, ref _grab, e, Ui.R(x0 + SegA + SegB + 26f, y0, Ui.Max(0f, w - SegA - SegB - 26f - Pane.ToolsW - 62f), Pane.HeadH));
        }
        finally
        {
            GUI.matrix = mtx;
        }
    }

    [HideFromIl2Cpp]
    private void Tab(Rect r, int idx, string label, NocturnePalette p)
    {
        bool active = _tab == idx;
        _seg.normal.textColor = active ? p.Text : Pane.A(p.Muted, Pane.Hover(r) ? 1f : 0.9f);
        GUI.Label(r, label, _seg);
        if (GUI.Button(r, NocturneStyle.Blank, GUIStyle.none) && _tab != idx)
        {
            _tab = idx;
            _scroll.Pos = 0f;
            _scroll.Stick = true;
            _measuredAt = -1f;
            if (idx == 1)
            {
                _chatAt = -99f;
                _chatLen = -1L;
            }
        }
    }

    [HideFromIl2Cpp]
    private bool Chip(Rect r, string label, Color tone, bool on, NocturnePalette p)
    {
        bool hot = Pane.Hover(r);
        if (on)
        {
            Skin.Glass(r, 11);
            NocturneStyle.FillRounded(r, Pane.A(tone, hot ? 0.24f : 0.16f), 11);
            NocturneStyle.StrokeRounded(r, Pane.A(tone, 0.42f), 11, 1);
        }
        else if (hot)
            Skin.Lit(r, 11);
        else
            Skin.Glass(r, 11);

        NocturneStyle.FillRounded(Ui.R(r.m_XMin + 9f, r.m_YMin + 8f, 6f, 6f), Pane.A(tone, on ? 1f : 0.35f), 3);
        _chipS.normal.textColor = on ? Ui.Mix(p.Text, tone, 0.18f) : Pane.A(p.Muted, hot ? 1f : 0.85f);
        GUI.Label(Ui.R(r.m_XMin + 20f, r.m_YMin, r.m_Width - 22f, r.m_Height), label, _chipS);
        return GUI.Button(r, NocturneStyle.Blank, GUIStyle.none);
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
        GUIUtility.systemCopyBuffer = s;
        NocturneToast.Push(NocturneText.T("Скопировано", "Copied"), null, 1.6f, NocturneNotifyKind.Success);
    }

    private static Color KindColor(NocturneNotifyKind k, NocturnePalette p) => k switch
    {
        NocturneNotifyKind.Success => Ui.C(0.32f, 0.85f, 0.46f, 1f),
        NocturneNotifyKind.Danger => Ui.C(0.93f, 0.36f, 0.36f, 1f),
        NocturneNotifyKind.Warning => Ui.C(0.96f, 0.76f, 0.20f, 1f),
        _ => p.Accent,
    };

    private void Build()
    {
        if (_built)
            return;
        _built = true;
        _clock = Pane.Label(11, FontStyle.Normal, TextAnchor.UpperLeft);
        _row = Pane.Label(12, FontStyle.Normal, TextAnchor.UpperLeft, true, true);
        _seg = Pane.Label(12, FontStyle.Bold, TextAnchor.MiddleCenter);
        _chipS = Pane.Label(11, FontStyle.Bold, TextAnchor.MiddleLeft);
        _chip = Pane.Label(11, FontStyle.Bold, TextAnchor.MiddleCenter);
        _empty = Pane.Label(13, FontStyle.Bold, TextAnchor.MiddleCenter);
        _hint = Pane.Label(11, FontStyle.Normal, TextAnchor.UpperCenter, true);
    }
}
