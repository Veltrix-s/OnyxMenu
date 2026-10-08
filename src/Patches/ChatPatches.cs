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
    private const float Duration = 1.15f;

    private class Entry
    {
        public float StartTime;
        public Transform Root;
        public Vector3 TargetScale;
        public SpriteRenderer Bg;
        public Color BgColor;
        public TMP_Text NameText;
        public Color NameColor;
        public TMP_Text MsgText;
        public Color MsgColor;
    }

    private static readonly Dictionary<int, Entry> _active = new Dictionary<int, Entry>();

    internal static void Register(ChatBubble bubble)
    {
        if (!NocturneConfig.BetterChat.Value)
            return;
        try
        {
            Transform root = bubble.transform;

            SpriteRenderer bg = null;
            Transform bgT = root.Find("Background");
            if (bgT != null)
                bg = bgT.GetComponent<SpriteRenderer>();

            TMP_Text nameText = bubble.NameText != null ? (TMP_Text)bubble.NameText : null;
            TMP_Text msgText = bubble.TextArea != null ? (TMP_Text)bubble.TextArea : null;

            var entry = new Entry
            {
                StartTime = Time.unscaledTime,
                Root = root,
                TargetScale = root.localScale,
                Bg = bg,
                BgColor = bg != null ? bg.color : Ui.White,
                NameText = nameText,
                NameColor = nameText != null ? nameText.color : Ui.White,
                MsgText = msgText,
                MsgColor = msgText != null ? msgText.color : Ui.White,
            };

            _active[bubble.gameObject.GetInstanceID()] = entry;
            ApplyEntry(entry, 0f);
        }
        catch { }
    }

    private static readonly List<int> _done = new List<int>();

    internal static void Tick()
    {
        if (_active.Count == 0)
            return;
        float now = Time.unscaledTime;
        _done.Clear();

        foreach (var kvp in _active)
        {
            Entry e = kvp.Value;
            float t = Ui.Clamp01((now - e.StartTime) / Duration);
            try
            {
                ApplyEntry(e, t);
            }
            catch
            {
                _done.Add(kvp.Key);
                continue;
            }
            if (t >= 1f)
                _done.Add(kvp.Key);
        }

        for (int i = 0; i < _done.Count; i++)
            _active.Remove(_done[i]);
    }

    private static float EaseAlpha(float t) => 1f - Ui.Pow(1f - t, 3f);

    private static float EaseScale(float t)
    {
        float delayed = Ui.Clamp01((t - 0.04f) / 0.96f);
        return 1f - Ui.Pow(1f - delayed, 8f);
    }

    private static void ApplyEntry(Entry e, float t)
    {
        float alpha = EaseAlpha(t);
        float scale = EaseScale(t);

        float s = Ui.Lerp(0.92f, 1f, scale);
        e.Root.localScale = Ui.V3(e.TargetScale.x * s, e.TargetScale.y * s, e.TargetScale.z);

        if (e.Bg != null)
        {
            Color c = e.BgColor;
            c.a = e.BgColor.a * alpha;
            e.Bg.color = c;
        }
        if (e.NameText != null)
        {
            Color c = e.NameColor;
            c.a = e.NameColor.a * alpha;
            e.NameText.color = c;
        }
        if (e.MsgText != null)
        {
            Color c = e.MsgColor;
            c.a = e.MsgColor.a * alpha;
            e.MsgText.color = c;
        }
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
            ChatInputPolicy.ApplyTypingRules(__instance);
            ChatHistoryNavigator.Handle(__instance);
            ChatThemeStyler.RefreshInputs(__instance);
            ChatBubbleCopyHandler.Check(__instance);
            ChatBubbleAnimations.Tick();
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

            nameText.text += $" <size=55%><color={dHex}>│</color> {roleTag}{hostTag}<color={aHex}>{NocturneText.T("Ур.", "Lv.")}{levelStr}</color><color={dHex}> · {platformStr}</color></size>";
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
