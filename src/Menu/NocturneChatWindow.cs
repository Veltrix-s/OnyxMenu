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
    private const float HeadH = 30f;
    private const float RowMinH = 18f;
    private const int MaxInput = 100;

    private static readonly string[] Syms =
    {
        "★", "☆", "♥", "♦", "♣", "♠", "♪", "♫", "☺", "☻", "●", "○", "◆", "◇", "■", "□",
        "▲", "▼", "◄", "►", "←", "→", "↑", "↓", "«", "»", "‹", "›", "„", "”", "…", "—",
        "•", "✦", "✿", "！", "？", "™", "©", "°", "§", "±", "×", "÷"
    };

    private sealed class Line
    {
        public string Text; public float H;
    }

    private static readonly List<Line> _lines = new List<Line>(Cap);
    private static bool _stick = true;
    private static string _lastKey = "";
    private static float _lastAt = -10f;

    internal static bool Open => NocturneConfig.ChatWindow.Value;
    internal static bool Typing { get; private set; }

    internal static void Feed(PlayerControl src, string chatText)
    {
        if (string.IsNullOrWhiteSpace(chatText)) return;
        string name = "?";
        bool local = false, dead = false;
        if (src != null && src.Data != null)
        {
            name = src.Data.PlayerName;
            local = src == PlayerControl.LocalPlayer;
            dead = src.Data.IsDead;
        }
        string sn = Clean(name, 24), st = Clean(chatText, 200);
        if (st.Length == 0) return;

        float now = Time.unscaledTime;
        string key = sn + "|" + st;
        if (key == _lastKey && now - _lastAt < 0.75f) return;
        _lastKey = key;
        _lastAt = now;

        NocturnePalette p = NocturneStyle.Current;
        Color nc = local ? p.Accent : (dead ? Ui.C(0.84f, 0.72f, 1f, 1f) : p.Text);
        string line = $"<color=#8892A0>[{DateTime.Now:HH:mm}]</color> <color=#{ColorUtility.ToHtmlStringRGB(nc)}>{sn}</color>: {st}";
        while (_lines.Count >= Cap)
            _lines.RemoveAt(0);
        _lines.Add(new Line { Text = line });
        _stick = true;
    }

    private Rect _win = Ui.R(24f, 320f, 560f, 440f);
    private float _scroll;
    private bool _drag;
    private Vector2 _grab;
    private bool _built;
    private GUIStyle _head, _clock, _row, _input, _btn, _empty;
    private readonly GUIContent _gc = new GUIContent();
    private string _text = "";
    private bool _edit;
    private bool _syms;
    private bool _moveBlocked;

    public void Update()
    {
        bool block = Open && _edit;
        Typing = block;
        if (!block && !_moveBlocked) return;

        PlayerControl pc = PlayerControl.LocalPlayer;

        if (block && pc != null)
        {
            if (!_moveBlocked)
            {
                if (!pc.moveable) return;
                _moveBlocked = true;
            }
            pc.moveable = false;
        }
        else if (_moveBlocked)
        {
            _moveBlocked = false;
            if (pc != null) pc.moveable = true;
        }
    }

    internal void DrawGui()
    {
        if (!Open)
        {
            _edit = false;
            return;
        }
        Build();

        float sc = Ui.Clamp(Screen.height / 1080f, 1f, 2.4f);
        Matrix4x4 mtx = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(Ui.V3(sc, sc, 1f));
        try
        {
            float sw = Screen.width / sc, sh = Screen.height / sc;
            Event e = Event.current;
            NocturneStyle.ClampWindow(ref _win, sw, sh, 360f, 240f);
            Patches.MouseShield.Cover(_win);

            NocturnePalette p = NocturneStyle.Current;
            float w = _win.m_Width, h = _win.m_Height;

            NocturneStyle.FillRounded(_win, A(p.Window, 0.95f), 12);
            NocturneStyle.StrokeRounded(_win, A(p.Accent, 0.28f), 12, 1);
            NocturneStyle.Fill(Ui.R(_win.m_XMin + 10f, _win.m_YMin + HeadH - 1f, w - 20f, 1f), A(p.Accent, 0.35f));

            _head.normal.textColor = p.Text;
            NocturneStyle.FillRounded(Ui.R(_win.m_XMin + 12f, _win.m_YMin + 9f, 3f, 12f), p.Accent, 1);
            GUI.Label(Ui.R(_win.m_XMin + 20f, _win.m_YMin + 5f, w - 175f, 20f), NocturneText.T("ЧАТ", "CHAT"), _head);
            _clock.normal.textColor = p.Muted;
            GUI.Label(Ui.R(_win.m_XMin + w - 150f, _win.m_YMin + 7f, 56f, 16f), _lines.Count + "/" + Cap, _clock);

            Rect cpy = Ui.R(_win.m_XMin + w - 80f, _win.m_YMin + 6f, 22f, 18f);
            Rect clr = Ui.R(_win.m_XMin + w - 54f, _win.m_YMin + 6f, 22f, 18f);
            Rect cls = Ui.R(_win.m_XMin + w - 28f, _win.m_YMin + 6f, 22f, 18f);
            if (Hover(cpy))
                NocturneStyle.FillRounded(cpy, A(p.Accent, 0.20f), 6);
            if (Hover(clr))
                NocturneStyle.FillRounded(clr, A(Ui.White, 0.07f), 6);
            if (Hover(cls)) NocturneStyle.FillRounded(cls, A(Ui.C(0.9f, 0.3f, 0.3f, 1f), 0.25f), 6);
            Icon(cpy, NocturneIcon.Copy, Hover(cpy) ? p.Text : p.Muted);
            Icon(clr, NocturneIcon.Trash, Hover(clr) ? p.Text : p.Muted);
            Icon(cls, NocturneIcon.Close, Hover(cls) ? Ui.C(0.95f, 0.5f, 0.5f, 1f) : p.Muted);
            if (GUI.Button(cpy, NocturneStyle.Blank, GUIStyle.none))
                CopyAll();
            if (GUI.Button(clr, NocturneStyle.Blank, GUIStyle.none)) _lines.Clear();
            if (GUI.Button(cls, NocturneStyle.Blank, GUIStyle.none))
            {
                _edit = false;
                NocturneConfig.ChatWindow.Value = false;
                return;
            }

            int perRow = Ui.Max(1, Ui.FloorToInt((w - 12f) / 30f));
            int symRows = _syms ? Ui.CeilToInt((float)Syms.Length / perRow) : 0;
            float symH = symRows * 28f;
            Rect inp = Ui.R(_win.m_XMin + 8f, _win.m_YMin + h - 34f, w - 190f, 26f);
            Rect send = Ui.R(Ui.Right(inp) + 6f, inp.m_YMin, 80f, 26f);
            Rect symBtn = Ui.R(Ui.Right(send) + 6f, inp.m_YMin, 80f, 26f);
            float symTop = inp.m_YMin - symH - (symH > 0f ? 4f : 0f);
            float bodyTop = _win.m_YMin + HeadH + 4f;
            float bodyBottom = symH > 0f ? symTop : inp.m_YMin - 4f;
            Rect body = Ui.R(_win.m_XMin + 6f, bodyTop, w - 12f, Ui.Max(40f, bodyBottom - bodyTop));

            Log(body, p, e);

            if (symH > 0f)
            {
                float bw = (w - 16f - (perRow - 1) * 4f) / perRow;
                for (int i = 0; i < Syms.Length; i++)
                {
                    Rect sb = Ui.R(_win.m_XMin + 8f + (i % perRow) * (bw + 4f), symTop + (i / perRow) * 28f, bw, 24f);
                    if (Btn(sb, Syms[i], A(Ui.White, Hover(sb) ? 0.14f : 0.07f)))
                        if (_text.Length + Syms[i].Length <= MaxInput) _text += Syms[i];
                }
            }

            InputBox(inp, p, e);
            if (Btn(send, NocturneText.T("Отпр.", "Send"), Hover(send) ? A(p.Accent, 0.9f) : A(p.Accent, 0.72f)))
                Send();
            if (Btn(symBtn, "☺ +", _syms ? A(p.Accent, 0.7f) : A(Ui.White, Hover(symBtn) ? 0.14f : 0.07f)))
                _syms = !_syms;

            Drag(e, Ui.R(_win.m_XMin, _win.m_YMin, w - 90f, HeadH));
        }
        finally { GUI.matrix = mtx; }
    }

    [HideFromIl2Cpp]
    private void Log(Rect body, NocturnePalette p, Event e)
    {
        if (e != null && e.type == EventType.ScrollWheel && Ui.In(body, e.mousePosition))
        {
            _scroll += e.delta.y * 18f;
            _stick = false;
            e.Use();
        }

        float textW = body.m_Width - 12f;
        Remeasure(textW);
        float total = 0f;
        for (int i = 0; i < _lines.Count; i++)
            total += RowHeight(_lines[i], textW);

        float maxScroll = Ui.Max(0f, total - body.m_Height);
        if (_stick) _scroll = maxScroll;
        _scroll = Ui.Clamp(_scroll, 0f, maxScroll);
        if (_scroll >= maxScroll - 1f) _stick = true;

        if (_lines.Count == 0)
        {
            _empty.normal.textColor = A(p.Muted, 0.8f);
            GUI.Label(body, NocturneText.T("Пока пусто.", "Empty for now."), _empty);
            return;
        }

        GUI.BeginGroup(body);
        float y = -_scroll;
        for (int i = 0; i < _lines.Count; i++)
        {
            float rh = RowHeight(_lines[i], textW);
            if (y + rh >= 0f && y <= body.m_Height)
            {
                _row.normal.textColor = p.Text;
                GUI.Label(Ui.R(4f, y + 1f, textW, rh), _lines[i].Text, _row);
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
    }

    [HideFromIl2Cpp]
    private void InputBox(Rect r, NocturnePalette p, Event e)
    {
        NocturneStyle.FillRounded(r, p.Button, 7);
        NocturneStyle.StrokeRounded(r, _edit ? A(p.Accent, 0.8f) : A(Ui.White, 0.08f), 7, 1);

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
                try
                {
                    GUIUtility.systemCopyBuffer = _text;
                }
                catch { }
                e.Use();
            }
            else if (ctrl && e.keyCode == KeyCode.X)
            {
                try
                {
                    GUIUtility.systemCopyBuffer = _text;
                }
                catch { }
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

        bool caret = _edit && Time.unscaledTime % 1f < 0.5f;
        string shown = _text.Length == 0 && !_edit ? NocturneText.T("Сообщение…", "Message…") : _text + (caret ? "|" : "");
        _input.normal.textColor = _text.Length == 0 && !_edit ? A(p.Muted, 0.7f) : p.Text;
        GUI.Label(Ui.R(r.m_XMin + 8f, r.m_YMin, r.m_Width - 16f, r.m_Height), shown, _input);
    }

    private void Paste()
    {
        try
        {
            string clip = GUIUtility.systemCopyBuffer;
            if (string.IsNullOrEmpty(clip)) return;
            clip = clip.Replace('\r', ' ').Replace('\n', ' ').Trim();
            if (clip.Length == 0) return;
            int room = MaxInput - _text.Length;
            if (room <= 0)
                return;
            _text += clip.Length > room ? clip.Substring(0, room) : clip;
        }
        catch { }
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
        try
        {
            me.RpcSendChat(msg);
        }
        catch { }
        _text = "";
        _stick = true;
    }

    private float _measuredAt;

    [HideFromIl2Cpp]
    private float RowHeight(Line l, float textW)
    {
        if (l.H > 0f)
            return l.H;
        _gc.text = l.Text;
        l.H = Ui.Max(RowMinH, _row.CalcHeight(_gc, textW) + 2f);
        return l.H;
    }

    private void Remeasure(float textW)
    {
        if (Ui.Abs(textW - _measuredAt) < 0.5f)
            return;
        _measuredAt = textW;
        for (int i = 0; i < _lines.Count; i++)
            _lines[i].H = 0f;
    }

    private bool Btn(Rect r, string label, Color bg)
    {
        NocturneStyle.FillRounded(r, bg, 6);
        _btn.normal.textColor = NocturneStyle.Current.Text;
        GUI.Label(r, label, _btn);
        return GUI.Button(r, NocturneStyle.Blank, GUIStyle.none);
    }

    private void Drag(Event e, Rect head)
    {
        if (e == null)
            return;
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

    private static void CopyAll()
    {
        if (_lines.Count == 0) return;
        var sb = new StringBuilder();
        for (int i = 0; i < _lines.Count; i++)
            sb.Append(StripTags(_lines[i].Text)).Append('\n');
        try
        {
            GUIUtility.systemCopyBuffer = sb.ToString();
        }
        catch { }
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

    private static void Icon(Rect r, NocturneIcon icon, Color c) => NocturneIcons.Draw(icon, Ui.R(r.m_XMin + r.m_Width / 2f - 7f, r.m_YMin + r.m_Height / 2f - 7f, 14f, 14f), c);
    private static bool Hover(Rect r) => Event.current != null && Ui.In(r, Event.current.mousePosition);
    private static Color A(Color c, float a) => Ui.C(c.r, c.g, c.b, a);

    private void Build()
    {
        if (_built) return;
        _built = true;
        _head = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, richText = true, alignment = TextAnchor.MiddleLeft };
        _clock = new GUIStyle(GUI.skin.label) { fontSize = 11, alignment = TextAnchor.UpperLeft, clipping = TextClipping.Clip };
        _row = new GUIStyle(GUI.skin.label) { fontSize = 12, richText = true, wordWrap = true, clipping = TextClipping.Clip, alignment = TextAnchor.UpperLeft };
        _input = new GUIStyle(GUI.skin.label) { fontSize = 12, richText = false, alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip };
        _btn = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, richText = true, alignment = TextAnchor.MiddleCenter };
        _empty = new GUIStyle(GUI.skin.label) { fontSize = 12, alignment = TextAnchor.MiddleCenter };
    }
}

[HarmonyPatch(typeof(ChatController), nameof(ChatController.AddChat))]
internal static class NocturneChatWindowFeed
{
    public static void Postfix(PlayerControl sourcePlayer, string chatText) => NocturneChatWindow.Feed(sourcePlayer, chatText);
}
