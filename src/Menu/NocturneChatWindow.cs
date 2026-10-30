using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using Il2CppInterop.Runtime.Attributes;
using UnityEngine;

namespace Nocturne;

public sealed class NocturneChatWindow : MonoBehaviour
{
    private const int Cap = 80;
    private const int MaxInput = 100;
    private const float GroupGap = 60f;
    private const float NameRow = 26f;

    private static readonly string[] Syms =
    {
        "★", "☆", "♥", "♦", "♣", "♠", "♪", "♫", "☺", "☻", "●", "○", "◆", "◇", "■", "□",
        "▲", "▼", "◄", "►", "←", "→", "↑", "↓", "«", "»", "‹", "›", "„", "”", "…", "—",
        "•", "✦", "✿", "！", "？", "™", "©", "°", "§", "±", "×", "÷"
    };

    private static readonly Color Ghost = Ui.C(0.84f, 0.72f, 1f, 1f);
    private static readonly Color Ink = Ui.C(0.10f, 0.045f, 0.08f, 1f);

    private sealed class Line
    {
        public string Who;
        public string Clock;
        public string Text;
        public bool Mine;
        public bool Dead;
        public float At;
        public float TextH;
        public float NameW;
    }

    private static readonly List<Line> _lines = new List<Line>(Cap);
    private static readonly PaneScroll _scroll = new PaneScroll();
    private static string _lastKey = "";
    private static float _lastAt = -10f;

    internal static bool Open => NocturneConfig.ChatWindow.Value;
    internal static bool Typing;

    internal static void Feed(PlayerControl src, string chatText)
    {
        if (string.IsNullOrWhiteSpace(chatText))
            return;
        string name = "?";
        bool local = false, dead = false;
        if (src != null && src.Data != null)
        {
            name = src.Data.PlayerName;
            local = src == PlayerControl.LocalPlayer;
            dead = src.Data.IsDead;
        }
        string sn = Clean(name, 24), st = Clean(chatText, 200);
        if (st.Length == 0)
            return;

        float now = Time.unscaledTime;
        string key = sn + "|" + st;
        if (key == _lastKey && now - _lastAt < 0.75f)
            return;
        _lastKey = key;
        _lastAt = now;

        while (_lines.Count >= Cap)
            _lines.RemoveAt(0);
        _lines.Add(new Line { Who = sn, Text = st, Clock = DateTime.Now.ToString("HH:mm"), Mine = local, Dead = dead, At = now });
        _scroll.Stick = true;
    }

    private Rect _win = Ui.R(24f, 320f, 560f, 440f);
    private bool _drag;
    private Vector2 _grab;
    private bool _built;
    private GUIStyle _title, _name, _clock, _chip, _row, _input, _sym, _empty, _hint;
    private readonly GUIContent _gc = new GUIContent();
    private string _text = "";
    private bool _edit;
    private bool _syms;
    private bool _moveBlocked;
    private float _a;
    private float _measuredAt;
    private int _countN = -1;
    private string _countText = "";
    private int _typedN = -1;
    private string _typedText = "";

    public void Update()
    {
        bool block = Open && _edit;
        Typing = block;
        if (!block && !_moveBlocked)
            return;

        PlayerControl pc = PlayerControl.LocalPlayer;

        if (block && pc != null)
        {
            if (!_moveBlocked)
            {
                if (!pc.moveable)
                    return;
                _moveBlocked = true;
            }
            pc.moveable = false;
        }
        else if (_moveBlocked)
        {
            _moveBlocked = false;
            if (pc != null)
                pc.moveable = true;
        }
    }

    internal void DrawGui()
    {
        if (!Open)
            _edit = false;
        if (NocturneStyle.Painting)
            _a = Ui.MoveTowards(_a, Open ? 1f : 0f, NocturneStyle.Dt * (Open ? 7f : 10f));
        if (_a <= 0f && !Open)
            return;
        Build();

        float sc = Ui.Clamp(Screen.height / 1080f, 1f, 2.4f);
        Matrix4x4 mtx = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(Ui.V3(sc, sc, 1f));
        try
        {
            float sw = Screen.width / sc, sh = Screen.height / sc;
            Event e = Event.current;
            NocturneStyle.ClampWindow(ref _win, sw, sh, 360f, 240f);

            float k = _a * _a * (3f - 2f * _a);
            Rect win = Ui.R(_win.m_XMin, _win.m_YMin + Ui.Round((1f - k) * 10f), _win.m_Width, _win.m_Height);
            Patches.MouseShield.Cover(win);
            NocturneStyle.Tint = Ui.C(1f, 1f, 1f, k);

            NocturnePalette p = NocturneStyle.Current;
            float w = win.m_Width, h = win.m_Height;
            float x0 = win.m_XMin, y0 = win.m_YMin;

            Skin.Drop(win, 0.38f);
            Skin.Frame(win, 16);

            Pane.Tile(Ui.R(x0 + 14f, y0 + 9f, 26f, 26f), NocturneIcon.Chat);
            _title.normal.textColor = p.Text;
            GUI.Label(Ui.R(x0 + 50f, y0 + 6f, 140f, 32f), NocturneText.T("Чат", "Chat"), _title);
            if (_lines.Count != _countN)
            {
                _countN = _lines.Count;
                _countText = _countN + "/" + Cap;
            }
            Pane.Counter(Ui.R(x0 + w - Pane.ToolsW - 56f, y0 + 12f, 56f, 20f), _countText, _chip);

            int tool = Pane.Tools(win, y0 + 8f);
            if (tool == 1)
                CopyAll();
            else if (tool == 2)
                _lines.Clear();
            else if (tool == 3)
            {
                _edit = false;
                NocturneConfig.ChatWindow.Value = false;
                return;
            }
            Skin.Hair(x0 + 4f, y0 + Pane.HeadH, w - 8f);

            int perRow = Ui.Max(1, Ui.FloorToInt((w - 20f) / 34f));
            int symRows = _syms ? Ui.CeilToInt((float)Syms.Length / perRow) : 0;
            float symH = symRows * 32f;
            Rect inp = Ui.R(x0 + 12f, y0 + h - 44f, w - 104f, 32f);
            Rect smile = Ui.R(Ui.Right(inp) + 8f, inp.m_YMin, 32f, 32f);
            Rect send = Ui.R(Ui.Right(smile) + 8f, inp.m_YMin, 32f, 32f);
            float symTop = inp.m_YMin - symH - (symH > 0f ? 6f : 0f);
            float bodyTop = y0 + Pane.HeadH + 2f;
            float bodyBottom = (symH > 0f ? symTop : inp.m_YMin) - 6f;
            Rect body = Ui.R(x0 + 8f, bodyTop, w - 16f, Ui.Max(40f, bodyBottom - bodyTop));

            Log(body, p, e);

            if (symH > 0f)
            {
                float bw = (w - 24f - (perRow - 1) * 4f) / perRow;
                for (int i = 0; i < Syms.Length; i++)
                {
                    Rect sb = Ui.R(x0 + 12f + (i % perRow) * (bw + 4f), symTop + (i / perRow) * 32f, bw, 28f);
                    if (SymTile(sb, Syms[i]) && _text.Length + Syms[i].Length <= MaxInput)
                        _text += Syms[i];
                }
            }

            InputBox(inp, p, e);

            bool hotSmile = Pane.Hover(smile);
            if (_syms || hotSmile)
                Skin.Lit(smile, 15);
            else
                Skin.Glass(smile, 15);
            NocturneIcons.Draw(NocturneIcon.Smile, Ui.R(smile.m_XMin + 8f, smile.m_YMin + 8f, 16f, 16f), _syms ? p.Accent : (hotSmile ? p.Text : Pane.A(p.Muted, 0.95f)));
            if (GUI.Button(smile, NocturneStyle.Blank, GUIStyle.none))
                _syms = !_syms;

            Skin.Orb(send);
            if (Pane.Hover(send))
                NocturneStyle.FillRounded(send, Ui.C(1f, 1f, 1f, 0.16f), 15);
            NocturneIcons.Draw(NocturneIcon.Send, Ui.R(send.m_XMin + 7f, send.m_YMin + 7f, 18f, 18f), Ink);
            if (GUI.Button(send, NocturneStyle.Blank, GUIStyle.none))
                Send();

            Pane.Drag(ref _win, ref _drag, ref _grab, e, Ui.R(x0, y0, w - Pane.ToolsW, Pane.HeadH));
        }
        finally
        {
            GUI.matrix = mtx;
        }
    }

    [HideFromIl2Cpp]
    private void Log(Rect body, NocturnePalette p, Event e)
    {
        float textW = body.m_Width - 36f;
        Remeasure(textW);
        float total = 10f;
        for (int i = 0; i < _lines.Count; i++)
            total += (IsHead(i) ? NameRow : 2f) + TextHeight(_lines[i], textW) + 3f;
        _scroll.Step(body, total, e);

        if (_lines.Count == 0)
        {
            Pane.Empty(body, NocturneIcon.Chat, NocturneText.T("Пока пусто", "Nothing yet"),
                NocturneText.T("Сообщения игрового чата появятся здесь.", "Messages from the game chat show up here."), _empty, _hint);
            return;
        }

        if (NocturneStyle.Painting)
        {
            Color say = Ui.Mix(p.Text, p.Window, 0.10f);
            GUI.BeginGroup(body);
            float y = 4f - _scroll.Pos;
            for (int i = 0; i < _lines.Count; i++)
            {
                Line l = _lines[i];
                bool head = IsHead(i);
                float th = TextHeight(l, textW);
                float rh = (head ? NameRow : 2f) + th + 3f;
                if (y + rh >= 0f && y <= body.m_Height)
                {
                    if (head)
                    {
                        _name.normal.textColor = l.Mine ? p.Accent : l.Dead ? Ghost : p.Text;
                        GUI.Label(Ui.R(10f, y + 7f, textW, 18f), l.Who, _name);
                        _clock.normal.textColor = Pane.A(p.Muted, 0.85f);
                        GUI.Label(Ui.R(10f + NameWidth(l) + 8f, y + 7f, 60f, 18f), l.Clock, _clock);
                    }
                    _row.normal.textColor = say;
                    GUI.Label(Ui.R(10f, y + (head ? NameRow : 2f), textW, th), l.Text, _row);
                }
                y += rh;
            }
            GUI.EndGroup();
        }
        _scroll.Bar(body, total, e);

        if (!_scroll.Stick)
        {
            Rect jump = Ui.R(body.m_XMin + (body.m_Width - 96f) * 0.5f, Ui.Bottom(body) - 36f, 96f, 28f);
            Skin.Grad(jump, 14);
            if (Pane.Hover(jump))
                NocturneStyle.FillRounded(jump, Ui.C(1f, 1f, 1f, 0.16f), 14);
            NocturneIcons.Draw(NocturneIcon.Down, Ui.R(jump.m_XMin + 12f, jump.m_YMin + 6f, 16f, 16f), Ink);
            _clock.normal.textColor = Ink;
            GUI.Label(Ui.R(jump.m_XMin + 32f, jump.m_YMin, 58f, 28f), NocturneText.T("Вниз", "Latest"), _clock);
            if (GUI.Button(jump, NocturneStyle.Blank, GUIStyle.none))
                _scroll.Stick = true;
        }
    }

    private static bool IsHead(int i)
    {
        if (i == 0)
            return true;
        Line a = _lines[i - 1], b = _lines[i];
        return a.Who != b.Who || b.At - a.At > GroupGap;
    }

    [HideFromIl2Cpp]
    private float TextHeight(Line l, float textW)
    {
        if (l.TextH > 0f)
            return l.TextH;
        _gc.text = l.Text;
        l.TextH = Ui.Max(16f, _row.CalcHeight(_gc, textW));
        return l.TextH;
    }

    [HideFromIl2Cpp]
    private float NameWidth(Line l)
    {
        if (l.NameW <= 0f)
        {
            _gc.text = l.Who;
            l.NameW = _name.CalcSize(_gc).x;
        }
        return l.NameW;
    }

    private void Remeasure(float textW)
    {
        if (Ui.Abs(textW - _measuredAt) < 0.5f)
            return;
        _measuredAt = textW;
        for (int i = 0; i < _lines.Count; i++)
            _lines[i].TextH = 0f;
    }

    private bool SymTile(Rect r, string glyph)
    {
        NocturneStyle.FillRounded(r, Ui.C(1f, 1f, 1f, Pane.Hover(r) ? 0.14f : 0.06f), 9);
        _sym.normal.textColor = NocturneStyle.Current.Text;
        GUI.Label(r, glyph, _sym);
        return GUI.Button(r, NocturneStyle.Blank, GUIStyle.none);
    }

    [HideFromIl2Cpp]
    private void InputBox(Rect r, NocturnePalette p, Event e)
    {
        if (_edit)
            Skin.Halo(r, 15, Pane.A(p.Accent, 0.22f));
        Skin.Glass(r, 15);
        if (_edit)
            NocturneStyle.StrokeRounded(r, Pane.A(p.Accent, 0.75f), 15, 1);

        if (e != null && e.type == EventType.MouseDown)
        {
            if (Ui.In(r, e.mousePosition))
                _edit = true;
            else if (!Ui.In(_win, e.mousePosition))
                _edit = false;
        }

        if (_edit && e != null && e.type == EventType.KeyDown)
        {
            bool ctrl = e.control || e.command;
            if (ctrl && e.keyCode == KeyCode.V)
            {
                Paste();
                e.Use();
            }
            else if (ctrl && e.keyCode == KeyCode.C)
            {
                GUIUtility.systemCopyBuffer = _text;
                e.Use();
            }
            else if (ctrl && e.keyCode == KeyCode.X)
            {
                GUIUtility.systemCopyBuffer = _text;
                _text = string.Empty;
                e.Use();
            }
            else if (e.keyCode == KeyCode.Backspace)
            {
                if (_text.Length > 0)
                    _text = _text.Substring(0, _text.Length - 1);
                e.Use();
            }
            else if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
            {
                Send();
                e.Use();
            }
            else if (e.keyCode == KeyCode.Escape)
            {
                _edit = false;
                e.Use();
            }
            else if (e.character != '\0' && !char.IsControl(e.character) && _text.Length < MaxInput)
            {
                _text += e.character;
                e.Use();
            }
        }

        if (!NocturneStyle.Painting)
            return;

        bool counting = _text.Length > 60;
        Rect inner = Ui.R(r.m_XMin + 15f, r.m_YMin, r.m_Width - 30f - (counting ? 48f : 0f), r.m_Height);
        if (_text.Length == 0 && !_edit)
        {
            _input.normal.textColor = Pane.A(p.Muted, 0.7f);
            GUI.Label(inner, NocturneText.T("Сообщение…", "Message…"), _input);
            return;
        }

        _gc.text = _text;
        float tw = _input.CalcSize(_gc).x;
        float room = inner.m_Width - 4f;
        float ox = tw > room ? room - tw : 0f;
        _input.normal.textColor = p.Text;
        GUI.BeginGroup(inner);
        GUI.Label(Ui.R(ox, 0f, tw + 8f, inner.m_Height), _text, _input);
        if (_edit && NocturneStyle.Now % 1f < 0.5f)
            NocturneStyle.Fill(Ui.R(ox + tw + 1f, (inner.m_Height - 16f) * 0.5f, 1f, 16f), p.Text);
        GUI.EndGroup();

        if (counting)
        {
            if (_text.Length != _typedN)
            {
                _typedN = _text.Length;
                _typedText = _typedN + "/" + MaxInput;
            }
            _clock.normal.textColor = _typedN >= MaxInput ? Ui.C(1f, 0.55f, 0.52f, 1f) : Pane.A(p.Muted, 0.9f);
            GUI.Label(Ui.R(Ui.Right(r) - 58f, r.m_YMin, 44f, r.m_Height), _typedText, _clock);
        }
    }

    private void Paste()
    {
        string clip = GUIUtility.systemCopyBuffer;
        if (string.IsNullOrEmpty(clip))
            return;
        clip = clip.Replace('\r', ' ').Replace('\n', ' ').Trim();
        if (clip.Length == 0)
            return;
        int room = MaxInput - _text.Length;
        if (room <= 0)
            return;
        _text += clip.Length > room ? clip.Substring(0, room) : clip;
    }

    private void Send()
    {
        string msg = (_text ?? "").Trim();
        if (msg.Length == 0)
            msg = "​";
        PlayerControl me = PlayerControl.LocalPlayer;
        if (me == null)
        {
            NocturneToast.Push(NocturneText.T("Чат", "Chat"), NocturneText.T("Не в игре.", "Not in game."), 1.8f, NocturneNotifyKind.Warning);
            return;
        }
        me.RpcSendChat(msg);
        _text = "";
        _scroll.Stick = true;
    }

    private static void CopyAll()
    {
        if (_lines.Count == 0)
            return;
        var sb = new StringBuilder();
        for (int i = 0; i < _lines.Count; i++)
        {
            Line l = _lines[i];
            sb.Append('[').Append(l.Clock).Append("] ").Append(l.Who).Append(": ").Append(l.Text).Append('\n');
        }
        GUIUtility.systemCopyBuffer = sb.ToString();
        NocturneToast.Push(NocturneText.T("Скопировано", "Copied"), null, 1.6f, NocturneNotifyKind.Success);
    }

    private static string Clean(string v, int max)
    {
        if (string.IsNullOrEmpty(v))
            return "";
        string s = StripTags(v).Replace('\n', ' ').Replace('\r', ' ').Trim();
        return s.Length > max ? s.Substring(0, max) : s;
    }

    private static string StripTags(string s)
    {
        var sb = new StringBuilder(s.Length);
        bool tag = false;
        foreach (char c in s)
        {
            if (c == '<')
                tag = true;
            else if (c == '>')
                tag = false;
            else if (!tag)
                sb.Append(c);
        }
        return sb.ToString();
    }

    private void Build()
    {
        if (_built)
            return;
        _built = true;
        _title = Pane.Label(14, FontStyle.Bold, TextAnchor.MiddleLeft);
        _name = Pane.Label(12, FontStyle.Bold, TextAnchor.MiddleLeft);
        _clock = Pane.Label(11, FontStyle.Normal, TextAnchor.MiddleLeft);
        _chip = Pane.Label(11, FontStyle.Bold, TextAnchor.MiddleCenter);
        _row = Pane.Label(12, FontStyle.Normal, TextAnchor.UpperLeft, true);
        _input = Pane.Label(12, FontStyle.Normal, TextAnchor.MiddleLeft);
        _sym = Pane.Label(14, FontStyle.Normal, TextAnchor.MiddleCenter);
        _empty = Pane.Label(13, FontStyle.Bold, TextAnchor.MiddleCenter);
        _hint = Pane.Label(11, FontStyle.Normal, TextAnchor.UpperCenter, true);
    }
}

[HarmonyPatch(typeof(ChatController), nameof(ChatController.AddChat))]
internal static class NocturneChatWindowFeed
{
    public static void Postfix(PlayerControl sourcePlayer, string chatText)
    {
        if (!NocturneMuteList.Hides(sourcePlayer))
            NocturneChatWindow.Feed(sourcePlayer, chatText);
    }
}
