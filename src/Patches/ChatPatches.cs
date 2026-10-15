using System;
using System.Collections.Generic;
using HarmonyLib;
using InnerNet;
using TMPro;
using UnityEngine;

namespace Nocturne.Patches;

internal static class ChatState
{
    private static readonly List<string> sentMessages = new List<string>();
    internal static int HistoryIndex = -1;
    internal static string DraftBeforeHistory = "";
    internal static bool BrowsingHistory;

    internal static int Count => sentMessages.Count;

    internal static void Remember(string message)
    {
        bool isNewEntry = sentMessages.Count == 0 || sentMessages[sentMessages.Count - 1] != message;
        if (isNewEntry)
        {
            sentMessages.Add(message);
        }

        HistoryIndex = sentMessages.Count;
    }

    internal static string At(int index) => sentMessages[index];
}

internal static class ChatInputPolicy
{
    private const float VanillaSafeChatCooldownSeconds = 3.15f;

    private const float ChatGapSeconds = 3f;
    private const int FreeInRow = 6;
    private const float IdleResetSeconds = 5f;

    private static int sentInRow;
    private static float lastSentAt = -99f;

    private static bool CanSendNow()
    {
        float since = Time.time - lastSentAt;
        if (since >= IdleResetSeconds)
            sentInRow = 0;
        if (sentInRow < FreeInRow)
            return true;
        return since >= ChatGapSeconds;
    }

    internal static void NoteSent()
    {
        if (Time.time - lastSentAt >= IdleResetSeconds)
            sentInRow = 0;
        sentInRow++;
        lastSentAt = Time.time;
    }

    internal static void TuneBubbleCache(ChatController chat)
    {
        if (chat == null || chat.chatBubblePool == null) return;
        chat.chatBubblePool.poolSize = NocturneConfig.ChatHistorySize.Value;
        chat.chatBubblePool.ReclaimOldest();
    }

    internal static void ApplyTypingRules(ChatController chat)
    {
        TextBoxTMP input = chat.freeChatField.textArea;

        if (NocturneConfig.SkipChatCooldown.Value || (NocturneConfig.BetterChat.Value && CanSendNow()))
        {
            chat.timeSinceLastMessage = Ui.Max(chat.timeSinceLastMessage, VanillaSafeChatCooldownSeconds + 0.1f);
        }

        if (NocturneConfig.UnlimitedChatLength.Value)
        {
            AllowExtendedInput(input, int.MaxValue);
        }
        else if (NocturneConfig.BetterChat.Value)
        {
            chat.timeSinceLastMessage = Ui.Max(chat.timeSinceLastMessage, 0.9f);
            AllowExtendedInput(input, 120);
        }
    }

    internal static bool CanSendEnhancedChat(ChatController chat, out float waitSeconds)
    {
        waitSeconds = 0f;
        if (chat == null || NocturneConfig.SkipChatCooldown.Value)
        {
            return true;
        }

        if (CanSendNow())
            return true;
        waitSeconds = ChatGapSeconds - (Time.time - lastSentAt);
        return waitSeconds <= 0f;
    }

    internal static void AllowExtendedInput(TextBoxTMP input, int limit)
    {
        input.AllowSymbols = true;
        input.AllowEmail = true;
        input.allowAllCharacters = true;
        input.characterLimit = limit;
    }

    internal static void DrawCount(FreeChatInputField field, int warningAt, int dangerAt)
    {
        if (field == null || field.textArea == null || field.charCountText == null)
        {
            return;
        }

        int length = field.textArea.text.Length;
        int limit = field.textArea.characterLimit;
        TMP_Text counter = field.charCountText;
        counter.enableWordWrapping = false;
        counter.overflowMode = TextOverflowModes.Overflow;
        counter.SetText($"{length}/{limit}", true);
        counter.color = CounterColor(length, warningAt, dangerAt);
    }

    internal static bool TryPrepareOutgoing(ChatController chat, out string message, out bool changed)
    {
        message = string.Empty;
        changed = false;
        if (chat == null || chat.freeChatField == null || chat.freeChatField.textArea == null)
        {
            return false;
        }

        string original = chat.freeChatField.textArea.text ?? string.Empty;
        message = SanitizeOutgoing(original);
        if (!NocturneConfig.UnlimitedChatLength.Value)
        {
            int limit = Ui.Max(1, chat.freeChatField.textArea.characterLimit);
            if (message.Length > limit)
            {
                message = message.Substring(0, limit).Trim();
            }
        }

        changed = !string.Equals(original, message, StringComparison.Ordinal);
        return !string.IsNullOrWhiteSpace(message);
    }


    private static Color CounterColor(int currentLength, int warningAt, int dangerAt)
    {
        if (currentLength >= dangerAt)
        {
            return Ui.Red;
        }

        if (currentLength >= warningAt)
        {
            return Ui.C(1f, 0.84f, 0.20f, 1f);
        }

        return NocturneConfig.DarkChatTheme.Value ? Ui.Mix(NocturneStyle.Current.Text, Ui.White, 0.20f) : Ui.Black;
    }

    private static string SanitizeOutgoing(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        string source = value.Replace("\r", string.Empty).Replace("\0", string.Empty);
        System.Text.StringBuilder builder = new System.Text.StringBuilder(source.Length);
        bool lastWasSpace = false;
        for (int i = 0; i < source.Length; i++)
        {
            char ch = source[i];
            if (char.IsControl(ch) && ch != '\n' && ch != '\t')
            {
                continue;
            }

            if (ch == '\t')
            {
                ch = ' ';
            }

            if (ch == ' ')
            {
                if (lastWasSpace)
                {
                    continue;
                }

                lastWasSpace = true;
            }
            else
            {
                lastWasSpace = false;
            }

            builder.Append(ch);
        }

        return builder.ToString().Trim();
    }
}

internal static class ChatHistoryNavigator
{
    internal static void Handle(ChatController chat)
    {
        if (ChatState.Count == 0)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.UpArrow))
        {
            MoveBack(chat);
        }
        else if (Input.GetKeyDown(KeyCode.DownArrow))
        {
            MoveForward(chat);
        }
    }

    private static void MoveBack(ChatController chat)
    {
        if (!ChatState.BrowsingHistory)
        {
            ChatState.DraftBeforeHistory = chat.freeChatField.textArea.text;
            ChatState.BrowsingHistory = true;
        }

        if (ChatState.HistoryIndex <= 0)
        {
            SoundManager.Instance.PlaySound(chat.warningSound, loop: false, 1f, null);
            return;
        }

        ChatState.HistoryIndex = Ui.Clamp(ChatState.HistoryIndex - 1, 0, ChatState.Count - 1);
        UseText(chat, ChatState.At(ChatState.HistoryIndex));
    }

    private static void MoveForward(ChatController chat)
    {
        ChatState.HistoryIndex += 1;
        if (ChatState.HistoryIndex < ChatState.Count)
        {
            UseText(chat, ChatState.At(ChatState.HistoryIndex));
            return;
        }

        UseText(chat, ChatState.DraftBeforeHistory);
        ChatState.BrowsingHistory = false;
    }

    private static void UseText(ChatController chat, string value)
    {
        chat.freeChatField.textArea.SetText(value, string.Empty);
    }
}

internal static class ChatThemeStyler
{
    private static int lastChatId;
    private static string lastThemeId = string.Empty;
    private static bool lastEnabled;
    private static float nextInputRefreshAt;

    internal static void RefreshInputs(ChatController chat, bool force = false)
    {
        if (chat == null || !NocturneConfig.DarkChatTheme.Value)
        {
            lastEnabled = false;
            return;
        }

        int chatId = chat.GetInstanceID();
        string themeId = NocturneStyle.Current.Id;
        if (!force && lastEnabled && lastChatId == chatId && string.Equals(lastThemeId, themeId, StringComparison.Ordinal) && Time.unscaledTime < nextInputRefreshAt)
        {
            return;
        }

        lastEnabled = true;
        lastChatId = chatId;
        lastThemeId = themeId;
        nextInputRefreshAt = Time.unscaledTime + 0.75f;

        NocturnePalette palette = NocturneStyle.Current;
        Color background = Ui.Mix(Ui.C(0.018f, 0.020f, 0.030f, 1f), palette.Panel, 0.46f);
        Color textColor = Ui.Mix(palette.Text, Ui.White, 0.10f);
        ApplyFreeChatField(chat, background, textColor);
        ApplyQuickChatField(chat, background, textColor);
    }

    internal static void ApplyBubble(ChatBubble bubble)
    {
        if (bubble == null || !NocturneConfig.DarkChatTheme.Value)
        {
            return;
        }

        NocturnePalette palette = NocturneStyle.Current;
        Color bubbleColor = Ui.Mix(Ui.C(0.018f, 0.020f, 0.030f, 0.84f), palette.Panel, 0.40f);
        Transform background = bubble.transform.Find("Background");
        if (background != null)
        {
            SpriteRenderer renderer = background.GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                renderer.color = bubbleColor;
            }
        }

        if (bubble.TextArea != null)
        {
            bubble.TextArea.color = Ui.Mix(palette.Text, Ui.White, 0.12f);
        }

        if (bubble.NameText != null)
        {
            bubble.NameText.color = Ui.Mix(palette.Accent, Ui.White, 0.24f);
        }
    }

    private static void ApplyFreeChatField(ChatController chat, Color background, Color textColor)
    {
        if (chat.freeChatField == null)
        {
            return;
        }

        AbstractChatInputField field = chat.freeChatField;
        if (field.background != null)
        {
            field.background.color = background;
        }

        TextBoxTMP textArea = chat.freeChatField.textArea;
        if (textArea != null && textArea.outputText != null)
        {
            textArea.outputText.color = textColor;
        }
    }

    private static void ApplyQuickChatField(ChatController chat, Color background, Color textColor)
    {
        if (chat.quickChatField == null)
        {
            return;
        }

        AbstractChatInputField field = chat.quickChatField;
        if (field.background != null)
        {
            field.background.color = background;
        }

        if (chat.quickChatField.text != null)
        {
            chat.quickChatField.text.color = textColor;
        }
    }
}

internal static class ChatBubbleCopyHandler
{
    private const float DoubleClickWindow = 0.38f;

    private static float lastClickAt = -10f;
    private static string lastClickText = string.Empty;
    private static float lastCopyAt = -10f;
    private static string lastCopiedText;

    internal static void Check(ChatController chat)
    {
        if (!NocturneConfig.BetterChat.Value || chat == null)
            return;
        bool right = Input.GetMouseButtonDown(1);
        if (!Input.GetMouseButtonDown(0) && !right) return;

        try
        {
            Camera cam = Camera.main;
            if (cam == null)
                return;
            Vector2 mouseScreen = Input.mousePosition;

            ChatBubble[] bubbles = chat.GetComponentsInChildren<ChatBubble>(false);
            for (int i = bubbles.Length - 1; i >= 0; i--)
            {
                ChatBubble bubble = bubbles[i];
                if (bubble == null)
                    continue;
                if (!HitsBubble(bubble, cam, mouseScreen))
                    continue;

                string text = right ? ReadName(bubble) : ReadText(bubble);
                if (string.IsNullOrEmpty(text)) continue;

                string key = (right ? "n|" : "m|") + text;
                float now = Time.unscaledTime;
                bool isDoubleClick = key == lastClickText && now - lastClickAt <= DoubleClickWindow;

                lastClickAt = now;
                lastClickText = key;

                if (!isDoubleClick) return;

                if (key == lastCopiedText && now - lastCopyAt < 2f) return;

                GUIUtility.systemCopyBuffer = text;
                lastCopyAt = now;
                lastCopiedText = key;
                lastClickAt = -10f;
                NocturneToast.Push(right ? NocturneText.T("Ник скопирован", "Nick copied") : NocturneText.T("Скопировано", "Copied"), 1.5f);
                return;
            }
        }
        catch { }
    }

    private static bool HitsBubble(ChatBubble bubble, Camera cam, Vector2 mouseScreen)
    {
        try
        {
            Transform bg = bubble.transform.Find("Background");
            if (bg == null)
                return false;
            SpriteRenderer sr = bg.GetComponent<SpriteRenderer>();
            if (sr == null) return false;
            Bounds b = sr.bounds;
            if (b.size.sqrMagnitude < 0.001f) return false;
            Vector3 smin = cam.WorldToScreenPoint(b.min);
            Vector3 smax = cam.WorldToScreenPoint(b.max);
            float x0 = Ui.Min(smin.x, smax.x);
            float x1 = Ui.Max(smin.x, smax.x);
            float y0 = Ui.Min(smin.y, smax.y);
            float y1 = Ui.Max(smin.y, smax.y);
            return mouseScreen.x >= x0 && mouseScreen.x <= x1 && mouseScreen.y >= y0 && mouseScreen.y <= y1;
        }
        catch
        {
            return false;
        }
    }

    private static string ReadText(ChatBubble bubble)
    {
        try
        {
            if (bubble.TextArea == null)
                return string.Empty;
            string raw = bubble.TextArea.text ?? string.Empty;
            int cut = raw.IndexOf("\n<align", StringComparison.OrdinalIgnoreCase);
            if (cut >= 0)
                raw = raw.Substring(0, cut);
            return raw.Trim();
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string ReadName(ChatBubble bubble)
    {
        try
        {
            if (bubble.NameText == null)
                return string.Empty;
            string raw = bubble.NameText.text ?? string.Empty;
            int cut = raw.IndexOf("<size=", StringComparison.OrdinalIgnoreCase);
            if (cut > 0)
                raw = raw.Substring(0, cut);
            return NocturneNameColor.Strip(raw).Trim();
        }
        catch
        {
            return string.Empty;
        }
    }
}

internal static class ClipboardBridge
{
    private static bool _pasting;
    private static bool _selectedAll;
    private static int _charPos;
    private static int _lastFrame = -1;

    internal static void Run(TextBoxTMP box)
    {
        if (!NocturneConfig.BetterChat.Value || box == null || !box.hasFocus) return;

        bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        bool copy = ctrl && (Input.GetKeyDown(KeyCode.C) || Input.GetKeyDown(KeyCode.Insert));
        bool paste = (ctrl && Input.GetKeyDown(KeyCode.V)) || (shift && Input.GetKeyDown(KeyCode.Insert));
        bool cut = ctrl && Input.GetKeyDown(KeyCode.X);
        bool selectAll = ctrl && Input.GetKeyDown(KeyCode.A);
        if ((!copy && !paste && !cut && !selectAll) || _lastFrame == Time.frameCount)
            return;
        _lastFrame = Time.frameCount;

        if (selectAll)
        {
            _selectedAll = true;
        }
        else if (copy)
        {
            GUIUtility.systemCopyBuffer = box.text ?? string.Empty;
        }
        else if (paste)
        {
            string clip = GUIUtility.systemCopyBuffer;
            if (!string.IsNullOrEmpty(clip))
            {
                string cur = box.text ?? string.Empty;
                string result;
                if (_selectedAll)
                {
                    result = clip;
                    _selectedAll = false;
                }
                else
                {
                    int at = Ui.Clamp(box.caretPos, 0, cur.Length);
                    result = cur.Insert(at, clip);
                }
                _pasting = true;
                box.SetText(result, string.Empty);
                _pasting = false;
            }
        }
        else if (cut)
        {
            GUIUtility.systemCopyBuffer = box.text ?? string.Empty;
            box.SetText(string.Empty, string.Empty);
            _selectedAll = false;
        }
    }

    internal static bool IsCharAllowed(TextBoxTMP box, ref bool result)
    {
        if (!NocturneConfig.BetterChat.Value || box == null)
            return true;

        string comp = Input.compositionString;
        if (!string.IsNullOrEmpty(comp))
        {
            result = true;
            return false;
        }

        string incoming = _pasting ? GUIUtility.systemCopyBuffer : Input.inputString;
        if (string.IsNullOrEmpty(incoming))
            return true;

        string cur = box.text ?? string.Empty;
        int at = Ui.Clamp(box.caretPos, 0, cur.Length);
        string full = _selectedAll ? incoming : cur.Insert(at, incoming);
        if (_selectedAll)
        {
            box.SetText(string.Empty, string.Empty);
            _selectedAll = false;
        }

        _charPos = Ui.Clamp(_charPos, 0, Ui.Max(0, full.Length - 1));
        char ch = full[_charPos];
        _charPos = _charPos < full.Length - 1 ? _charPos + 1 : 0;

        if (NocturneConfig.ChatColorTags.Value)
            result = ch != '\b' && ch != '\r' && ch != '\n';
        else
            result = ch != '\b' && ch != '\r' && ch != '\n' && ch != '>' && ch != '<' && ch != '[';
        return false;
    }
}

internal static class ChatBubbleAnimations
{
    private const int Reach = 12;
    private const int OpenMax = 9;
    private const float ViewH = 5f;
    private const float GlideRate = 13f;
    private const float Span = 0.62f;

    private struct Look
    {
        public float Slide;
        public float Rise;
        public float Dip;
        public float Pop;
        public float Speed;
        public float Flash;

        public Look(float slide, float rise, float dip, float pop, float speed, float flash)
        {
            Slide = slide;
            Rise = rise;
            Dip = dip;
            Pop = pop;
            Speed = speed;
            Flash = flash;
        }
    }

    private static readonly Look Others = new Look(0.30f, 0.16f, 0.06f, 0.35f, 1f, 1f);
    private static readonly Look Mine = new Look(0.10f, 0.30f, 0.05f, 0.45f, 1.15f, 0.7f);
    private static readonly Look Calm = new Look(0.14f, 0.10f, 0.025f, 0.6f, 1.4f, 0f);

    private sealed class Entry
    {
        public float Start;
        public float Side;
        public Look Style;
        public Transform Root;
        public Vector3 RootScale;
        public Transform Avatar;
        public Vector3 AvatarScale;
        public SpriteRenderer Bg;
        public Color BgColor;
        public Color FlashColor;
        public TMP_Text NameText;
        public Color NameColor;
        public float NameLast;
        public Transform NameTr;
        public Vector3 NamePos;
        public TMP_Text MsgText;
        public Color MsgColor;
        public float MsgLast;
        public Transform MsgTr;
        public Vector3 MsgPos;
        public bool BgDone;
        public bool NameDone;
        public bool MsgDone;
    }

    private sealed class Track
    {
        public Transform T;
        public float X;
        public float Y;
        public float Z;
        public float Glide;
        public float EnterX;
        public float EnterY;
        public float Hold;
        public bool Fresh;
        public int Stamp;
        public Entry Enter;
    }

    private static int _stamp;
    private static int _unread;
    private static float _sync = -1f;
    private static readonly Dictionary<int, Track> Tracks = new Dictionary<int, Track>();
    private static readonly List<int> Gone = new List<int>();
    private static readonly List<Transform> Seen = new List<Transform>();
    private static readonly List<int> SeenId = new List<int>();
    private static readonly List<float> SeenY = new List<float>();

    private static float Smooth(float t)
    {
        t = Ui.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    private static float Back(float t, float c)
    {
        float u = Ui.Clamp01(t) - 1f;
        return 1f + (c + 1f) * u * u * u + c * u * u;
    }

    internal static void Register(ChatBubble bubble)
    {
        if (bubble == null || !NocturneConfig.BetterChat.Value)
            return;
        try
        {
            if (bubble.gameObject.activeInHierarchy)
                Begin(bubble, Time.unscaledTime, 0);
            else if (_unread < 40)
                _unread++;
        }
        catch { }
    }

    private static float OpenSync(ChatController chat)
    {
        if (_sync >= 0f)
            return _sync;

        float t = 0.02f;
        AnimationCurve curve = chat.chatAnimationIn;
        if (curve != null)
        {
            while (t < 0.5f && curve.Evaluate(t) < 0.55f)
                t += 0.02f;
        }
        _sync = t * 0.8f;
        return _sync;
    }

    internal static void Cascade(ChatController chat)
    {
        int unread = _unread;
        _unread = 0;
        if (chat == null || !NocturneConfig.BetterChat.Value)
            return;
        try
        {
            var list = chat.chatBubblePool.activeChildren;
            float low = -1.85f - chat.scroller.Inner.localPosition.y - 0.4f;
            float high = low + ViewH;
            float start = Time.unscaledTime + OpenSync(chat);
            int shown = 0;
            for (int i = list.Count - 1; i >= 0 && shown < OpenMax; i--)
            {
                PoolableBehavior pb = list[i];
                float y = pb.transform.localPosition.y;
                if (y > high)
                    break;
                if (y < low)
                    continue;

                ChatBubble b = pb.TryCast<ChatBubble>();
                if (b == null || !b.gameObject.activeInHierarchy)
                    continue;
                Begin(b, start + shown * 0.032f, list.Count - 1 - i < unread ? 1 : 2);
                shown++;
            }
        }
        catch { }
    }

    private static void Begin(ChatBubble bubble, float start, int mode)
    {
        Transform root = bubble.transform;
        int id = root.GetInstanceID();
        Tracks.TryGetValue(id, out Track k);
        bool fresh = mode == 0;
        if (!fresh && k != null && k.Enter != null)
            return;

        Transform avatar = bubble.Player != null ? bubble.Player.transform : null;
        float side = avatar != null && avatar.localPosition.x > 2f ? 1f : -1f;
        Look look = side > 0f ? Mine : Others;
        if (mode == 2 || (fresh && Entering() >= 3))
            look = Calm;

        Vector3 pos = root.localPosition;
        SpriteRenderer bg = bubble.Background;
        TMP_Text nameText = bubble.NameText;
        TMP_Text msgText = bubble.TextArea;

        var e = new Entry
        {
            Start = start,
            Side = side,
            Style = look,
            Root = root,
            RootScale = root.localScale,
            Avatar = avatar,
            AvatarScale = avatar != null ? avatar.localScale : Ui.One3,
            Bg = bg,
            BgColor = bg != null ? bg.color : Ui.White,
            FlashColor = Ui.Mix(NocturneStyle.Current.Accent, Ui.White, 0.15f),
            NameText = nameText,
            NameColor = nameText != null ? nameText.color : Ui.White,
            NameLast = -1f,
            NameTr = nameText != null ? nameText.transform : null,
            MsgText = msgText,
            MsgColor = msgText != null ? msgText.color : Ui.White,
            MsgLast = -1f,
            MsgTr = msgText != null ? msgText.transform : null,
        };
        if (e.NameTr != null)
            e.NamePos = e.NameTr.localPosition;
        if (e.MsgTr != null)
            e.MsgPos = e.MsgTr.localPosition;

        if (k == null || fresh)
        {
            k = new Track { T = root, X = pos.x, Y = pos.y, Z = pos.z, Fresh = fresh };
            Tracks[id] = k;
        }
        k.Enter = e;
        Animate(k, e, Time.unscaledTime - start);
        Place(k);
    }

    private static int Entering()
    {
        int n = 0;
        foreach (KeyValuePair<int, Track> kv in Tracks)
        {
            if (kv.Value.Enter != null)
                n++;
        }
        return n;
    }

    internal static void Before(ChatController chat)
    {
        Seen.Clear();
        SeenId.Clear();
        SeenY.Clear();
        if (chat == null || !NocturneConfig.BetterChat.Value)
            return;
        try
        {
            if (chat.State == ChatControllerState.Closed)
                return;
            var list = chat.chatBubblePool.activeChildren;
            for (int i = Ui.Max(0, list.Count - Reach); i < list.Count; i++)
            {
                Transform t = list[i].transform;
                int id = t.GetInstanceID();
                if (Tracks.TryGetValue(id, out Track k) && k.Fresh)
                    continue;
                Seen.Add(t);
                SeenId.Add(id);
                SeenY.Add(t.localPosition.y);
            }
        }
        catch
        {
            Seen.Clear();
            SeenId.Clear();
            SeenY.Clear();
        }
    }

    internal static void After()
    {
        _stamp++;
        Gone.Clear();
        try
        {
            for (int i = 0; i < Seen.Count; i++)
            {
                Transform t = Seen[i];
                if (t == null)
                    continue;
                Vector3 p = t.localPosition;
                float jump = SeenY[i] - p.y;
                bool moved = Ui.Abs(jump) >= 0.002f;
                if (!Tracks.TryGetValue(SeenId[i], out Track k))
                {
                    if (!moved)
                        continue;
                    k = new Track { T = t, X = p.x, Y = p.y, Z = p.z };
                    Tracks[SeenId[i]] = k;
                }
                k.Y = p.y;
                k.Glide = moved ? jump - k.EnterY : 0f;
                k.Hold = moved ? Ui.Min(0.1f, (Seen.Count - 1 - i) * 0.018f) : 0f;
                k.Stamp = _stamp;
            }

            foreach (KeyValuePair<int, Track> kv in Tracks)
            {
                Track k = kv.Value;
                if (k.T == null)
                {
                    Gone.Add(kv.Key);
                    continue;
                }
                if (k.Stamp != _stamp)
                {
                    if (!k.T.gameObject.activeInHierarchy)
                    {
                        Release(k);
                        Gone.Add(kv.Key);
                        continue;
                    }
                    k.Y = k.T.localPosition.y;
                    k.Glide = 0f;
                }
                k.Fresh = false;
                Place(k);
            }
        }
        catch { }
        for (int i = 0; i < Gone.Count; i++)
            Tracks.Remove(Gone[i]);
        Seen.Clear();
        SeenId.Clear();
        SeenY.Clear();
    }

    internal static void Tick(ChatController chat)
    {
        if (Tracks.Count == 0)
            return;
        if (chat.State == ChatControllerState.Closed)
        {
            foreach (KeyValuePair<int, Track> kv in Tracks)
                Release(kv.Value);
            Tracks.Clear();
            return;
        }

        float now = Time.unscaledTime;
        float dt = Time.unscaledDeltaTime;
        float decay = Ui.Exp(-GlideRate * dt);
        Gone.Clear();
        foreach (KeyValuePair<int, Track> kv in Tracks)
        {
            Track k = kv.Value;
            try
            {
                if (k.T == null || !Step(k, now, dt, decay))
                    Gone.Add(kv.Key);
            }
            catch
            {
                Release(k);
                Gone.Add(kv.Key);
            }
        }
        for (int i = 0; i < Gone.Count; i++)
            Tracks.Remove(Gone[i]);
    }

    private static bool Step(Track k, float now, float dt, float decay)
    {
        k.Fresh = false;
        Entry e = k.Enter;
        if (e != null)
        {
            float t = now - e.Start;
            if (t * e.Style.Speed >= Span)
            {
                Finish(e);
                k.Enter = null;
                k.EnterX = 0f;
                k.EnterY = 0f;
            }
            else
                Animate(k, e, t);
        }

        if (k.Hold > 0f)
            k.Hold -= dt;
        else
            k.Glide *= decay;
        if (Ui.Abs(k.Glide) < 0.004f)
            k.Glide = 0f;
        Place(k);
        return k.Enter != null || k.Glide != 0f;
    }

    private static void Release(Track k)
    {
        try
        {
            if (k.Enter != null)
                Finish(k.Enter);
            k.Enter = null;
            k.EnterX = 0f;
            k.EnterY = 0f;
            k.Glide = 0f;
            if (k.T != null)
                Place(k);
        }
        catch { }
    }

    private static void Place(Track k) => k.T.localPosition = Ui.V3(k.X + k.EnterX, k.Y + k.EnterY + k.Glide, k.Z);

    private static void Move(Transform t, Vector3 home, float dx) => t.localPosition = Ui.V3(home.x + dx, home.y, home.z);

    private static void Finish(Entry e)
    {
        if (e.Root != null)
            e.Root.localScale = e.RootScale;
        if (e.Avatar != null)
            e.Avatar.localScale = e.AvatarScale;
        if (e.Bg != null)
            e.Bg.color = e.BgColor;
        if (e.NameText != null)
            e.NameText.color = e.NameColor;
        if (e.MsgText != null)
            e.MsgText.color = e.MsgColor;
        if (e.NameTr != null)
            e.NameTr.localPosition = e.NamePos;
        if (e.MsgTr != null)
            e.MsgTr.localPosition = e.MsgPos;
    }

    private static bool Fade(TMP_Text text, Color color, ref float last, float a, float steps)
    {
        float q = a >= 1f ? 1f : Ui.Floor(a * steps) / steps;
        if (q != last)
        {
            last = q;
            if (text != null)
            {
                color.a *= q;
                text.color = color;
            }
        }
        return q >= 1f;
    }

    private static void Animate(Track k, Entry e, float t)
    {
        Look s = e.Style;
        float u = t * s.Speed;
        float off = 1f - Back(u / 0.42f, 1.4f);
        k.EnterX = e.Side * s.Slide * off;
        k.EnterY = -s.Rise * off;

        float sc = 1f - s.Dip * off;
        e.Root.localScale = Ui.V3(e.RootScale.x * sc, e.RootScale.y * sc, e.RootScale.z);

        if (e.Avatar != null)
        {
            float pop = s.Pop + (1f - s.Pop) * Back((u - 0.04f) / 0.42f, 2.4f);
            e.Avatar.localScale = Ui.V3(e.AvatarScale.x * pop, e.AvatarScale.y * pop, e.AvatarScale.z);
        }

        if (e.NameTr != null)
            Move(e.NameTr, e.NamePos, e.Side * s.Slide * 0.35f * (1f - Back((u - 0.03f) / 0.45f, 1.2f)));
        if (e.MsgTr != null)
            Move(e.MsgTr, e.MsgPos, e.Side * s.Slide * 0.55f * (1f - Back((u - 0.08f) / 0.5f, 1.2f)));

        if (e.Bg != null && !e.BgDone)
        {
            float fade = Smooth(u / 0.2f);
            float flash = s.Flash * (1f - Smooth(u / 0.55f));
            Color c = flash > 0.001f ? Ui.Mix(e.BgColor, e.FlashColor, 0.4f * flash) : e.BgColor;
            c.a = e.BgColor.a * fade;
            e.Bg.color = c;
            e.BgDone = fade >= 1f && flash <= 0.001f;
        }
        float steps = s.Flash > 0f ? 8f : 4f;
        if (!e.NameDone)
            e.NameDone = Fade(e.NameText, e.NameColor, ref e.NameLast, Smooth((u - 0.04f) / 0.22f), steps);
        if (!e.MsgDone)
            e.MsgDone = Fade(e.MsgText, e.MsgColor, ref e.MsgLast, Smooth((u - 0.1f) / 0.22f), steps);
    }
}

[HarmonyPatch(typeof(ChatController), nameof(ChatController.AlignAllBubbles))]
internal static class ChatAlignPatch
{
    public static void Prefix(ChatController __instance) => ChatBubbleAnimations.Before(__instance);

    public static void Postfix() => ChatBubbleAnimations.After();
}

[HarmonyPatch(typeof(ChatController), nameof(ChatController.Toggle))]
internal static class ChatOpenAnimationPatch
{
    public static void Prefix(ChatController __instance, out bool __state) => __state = __instance != null && __instance.IsOpenOrOpening;

    public static void Postfix(ChatController __instance, bool __state)
    {
        if (!__state && __instance != null && __instance.IsOpenOrOpening)
            ChatBubbleAnimations.Cascade(__instance);
    }
}

[HarmonyPatch(typeof(ChatController), nameof(ChatController.Awake))]
internal static class ChatPoolPatch
{
    public static void Postfix(ChatController __instance)
    {
        try
        {
            if (__instance == null) return;
            ChatInputPolicy.TuneBubbleCache(__instance);
            ChatThemeStyler.RefreshInputs(__instance, true);
        }
        catch { }
    }
}

[HarmonyPatch(typeof(ChatController), nameof(ChatController.Update))]
internal static class ChatRuntimePatch
{
    public static void Postfix(ChatController __instance)
    {
        try
        {
            if (__instance == null)
                return;
            ChatBubbleAnimations.Tick(__instance);
            ChatInputPolicy.ApplyTypingRules(__instance);
            ChatHistoryNavigator.Handle(__instance);
            ChatThemeStyler.RefreshInputs(__instance);
            ChatBubbleCopyHandler.Check(__instance);
        }
        catch { }
    }
}

[HarmonyPatch(typeof(ChatBubble), nameof(ChatBubble.SetText))]
internal static class DarkChatBubblePatch
{
    public static void Postfix(ChatBubble __instance)
    {
        ChatThemeStyler.ApplyBubble(__instance);
    }
}

[HarmonyPatch(typeof(ChatBubble), nameof(ChatBubble.SetText))]
[HarmonyPriority(Priority.Low)]
internal static class ChatBubbleAnimationPatch
{
    public static void Postfix(ChatBubble __instance)
    {
        ChatBubbleAnimations.Register(__instance);
    }
}

[HarmonyPatch(typeof(ChatBubble), nameof(ChatBubble.SetText))]
[HarmonyPriority(Priority.First)]
internal static class NocturneChatTimestampPatch
{
    public static void Prefix([HarmonyArgument(0)] ref string chatText)
    {
        if (!NocturneConfig.ChatTimestamps.Value)
            return;
        if (string.IsNullOrEmpty(chatText))
            return;

        chatText += "\n<align=\"right\"><size=55%><color=#8A8A8A>" + DateTime.Now.ToString("HH:mm:ss") + "</color></size></align>";
    }
}

[HarmonyPatch(typeof(FreeChatInputField), nameof(FreeChatInputField.UpdateCharCount))]
internal static class ChatCounterPatch
{
    public static void Postfix(FreeChatInputField __instance)
    {
        if (NocturneConfig.UnlimitedChatLength.Value)
        {
            ChatInputPolicy.DrawCount(__instance, int.MaxValue, int.MaxValue);
            return;
        }

        if (NocturneConfig.BetterChat.Value)
        {
            ChatInputPolicy.DrawCount(__instance, 90, 120);
        }
    }
}

[HarmonyPatch(typeof(ChatController), nameof(ChatController.SendFreeChat))]
internal static class FreeChatSendPatch
{
    public static bool Prefix(ChatController __instance)
    {
        if (NocturneWhisper.TryHandle(__instance))
        {
            return HarmonyControl.SkipOriginal;
        }

        if (!NocturneConfig.BetterChat.Value)
        {
            return HarmonyControl.Continue;
        }

        if (!ChatInputPolicy.TryPrepareOutgoing(__instance, out string messageText, out bool changed))
        {
            __instance.freeChatField.textArea.SetText(string.Empty, string.Empty);
            return HarmonyControl.SkipOriginal;
        }

        if (changed)
        {
            __instance.freeChatField.textArea.SetText(messageText, string.Empty);
        }

        if (!ChatInputPolicy.CanSendEnhancedChat(__instance, out float chatWaitSeconds))
        {
            int wait = Ui.CeilToInt(chatWaitSeconds);
            NocturneToast.Push(NocturneText.T($"Чат: подожди {wait}с перед следующим", $"Chat: wait {wait}s before the next one"), 1.8f);
            return HarmonyControl.SkipOriginal;
        }

        ChatState.Remember(messageText);
        PlayerControl.LocalPlayer.RpcSendChat(messageText);
        ChatInputPolicy.NoteSent();
        __instance.timeSinceLastMessage = 0f;
        __instance.freeChatField.textArea.SetText(string.Empty, string.Empty);
        return HarmonyControl.SkipOriginal;
    }
}

[HarmonyPatch(typeof(TextBoxTMP), nameof(TextBoxTMP.Start))]
internal static class TextBoxRulesPatch
{
    public static void Postfix(TextBoxTMP __instance)
    {
        if (!NocturneConfig.BetterChat.Value)
        {
            return;
        }

        ChatInputPolicy.AllowExtendedInput(__instance, __instance.characterLimit);
    }
}

[HarmonyPatch(typeof(TextBoxTMP), nameof(TextBoxTMP.Update))]
internal static class ClipboardPatch
{
    public static void Postfix(TextBoxTMP __instance)
    {
        ClipboardBridge.Run(__instance);
    }
}

[HarmonyPatch(typeof(TextBoxTMP), nameof(TextBoxTMP.IsCharAllowed))]
internal static class NocturneCharAllowPatch
{
    public static bool Prefix(TextBoxTMP __instance, ref bool __result)
        => ClipboardBridge.IsCharAllowed(__instance, ref __result);
}

internal static class ChatBubbleSenderDecorator
{
    internal static void Apply(ChatBubble bubble)
    {
        if (bubble == null || bubble.playerInfo == null || bubble.NameText == null) return;

        try
        {
            TMP_Text authorText = bubble.NameText;
            PlayerControl me = PlayerControl.LocalPlayer;
            bool self = me != null && me.Data != null && bubble.playerInfo.PlayerId == me.Data.PlayerId;
            PlayerControl sender = bubble.playerInfo.Object;
            bool mark = NocturneAuthor.Is(sender) || NocturneAuthor.Match(bubble.playerInfo.FriendCode) || NocturneAuthor.Match(bubble.playerInfo.Puid);
            if (!self && mark && !authorText.text.Contains("◆"))
                authorText.text += "  <size=70%>" + NocturneAuthor.TagShort + "</size>";
        }
        catch { }

        if (!NocturneConfig.ChatBubbleSenderInfo.Value)
            return;

        try
        {
            TMP_Text nameText = bubble.NameText;
            if (nameText.text != null && nameText.text.Contains("<size=65%>")) return;

            string plainName = NocturneNameColor.Strip(nameText.text);
            if (plainName != null && plainName.Length > 24)
                return;

            PlayerControl player = bubble.playerInfo.Object;

            string levelStr = NocturneJoinLevels.Display(player);

            string platformStr = "?";
            string acctStr = string.Empty;
            bool isHost = false;
            if (AmongUsClient.Instance != null)
            {
                InnerNetClient client = AmongUsClient.Instance;
                if (player != null)
                {
                    ClientData clientData = client.GetClientFromCharacter(player);
                    if (clientData != null)
                    {
                        if (clientData.PlatformData != null)
                        {
                            platformStr = Utils.Platform(clientData.PlatformData.Platform);
                            string rawName = (clientData.PlatformData.PlatformName ?? string.Empty).Trim();
                            if (!string.IsNullOrEmpty(rawName) && !rawName.Equals("TESTNAME", StringComparison.OrdinalIgnoreCase))
                            {
                                rawName = TrimRawPlatformName(rawName);
                                int cap = Ui.Clamp(22 - (plainName != null ? plainName.Length : 0), 6, 16);
                                if (rawName.Length > cap)
                                    rawName = rawName.Substring(0, cap).TrimEnd() + "…";
                                if (!string.IsNullOrEmpty(rawName) && !rawName.Equals("TESTNAME", StringComparison.OrdinalIgnoreCase))
                                    platformStr += " · " + rawName;
                            }
                        }
                        ClientData host = client.GetHost();
                        isHost = host != null && clientData == host;
                    }
                }
            }

            Color accentColor = Ui.Mix(NocturneStyle.Current.Accent, Ui.White, 0.22f);
            Color dimColor = Ui.Mix(NocturneStyle.Current.Accent, Ui.White, 0.54f);
            string aHex = "#" + ColorUtility.ToHtmlStringRGB(accentColor);
            string dHex = "#" + ColorUtility.ToHtmlStringRGB(dimColor);
            string hostTag = isHost ? $"<color={aHex}>{NocturneText.T("Хост", "Host")}</color><color={dHex}> · </color>" : string.Empty;

            string roleTag = string.Empty;
            if (NocturneConfig.RevealRoles.Value && player != null && player.Data != null)
            {
                string rt = VisualAssist.RoleTag(player.Data);
                if (!string.IsNullOrEmpty(rt))
                    roleTag = $"{rt}<color={dHex}> · </color>";
            }

            string acctTag = acctStr.Length > 0 ? $"<color={dHex}> · </color><color={aHex}>{acctStr}</color>" : string.Empty;

            nameText.text += $" <size=55%><color={dHex}>│</color> {roleTag}{hostTag}<color={aHex}>{NocturneText.T("Ур.", "Lv.")}{levelStr}</color><color={dHex}> · {platformStr}</color>{acctTag}</size>";
            nameText.enableWordWrapping = false;
            nameText.overflowMode = TextOverflowModes.Overflow;
        }
        catch { }
    }

    private static string TrimRawPlatformName(string raw)
    {
        if (string.IsNullOrEmpty(raw))
            return string.Empty;

        int pipe = raw.IndexOf('|');
        if (pipe > 0)
            raw = raw.Substring(0, pipe).TrimEnd();

        int authorIdx = FindAuthorSeparator(raw);
        if (authorIdx > 0)
            raw = raw.Substring(0, authorIdx).TrimEnd();

        if (raw.Length > 24)
            raw = raw.Substring(0, 24).TrimEnd() + "…";
        return raw;
    }

    private static int FindAuthorSeparator(string raw)
    {
        string[] separators = { " by ", " от ", " - ", " — ", " – ", " :: " };
        int earliest = -1;
        for (int i = 0; i < separators.Length; i++)
        {
            int idx = raw.IndexOf(separators[i], StringComparison.OrdinalIgnoreCase);
            if (idx > 0 && (earliest < 0 || idx < earliest))
                earliest = idx;
        }
        return earliest;
    }
}

[HarmonyPatch(typeof(ChatBubble), nameof(ChatBubble.SetName))]
internal static class ChatBubbleSenderInfoPatch
{
    public static void Postfix(ChatBubble __instance)
    {
        ChatBubbleSenderDecorator.Apply(__instance);
    }
}
