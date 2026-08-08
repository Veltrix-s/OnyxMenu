using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using HarmonyLib;
using InnerNet;

namespace Nocturne.Patches;

internal static class NocturneChatLog
{
    private static readonly object Lock = new object();
    private static string Dir => Path.Combine(BepInEx.Paths.GameRootPath, "Nocturne");
    private static string LogPath => Path.Combine(Dir, "ChatLog.txt");

    internal static string FilePath => LogPath;

    internal static void Record(PlayerControl src, string text)
    {
        if (!NocturneConfig.ChatLog.Value) return;
        try
        {
            string name = "?", fc = "-", puid = "-", plat = "-";
            int id = -1;

            try { if (src != null && src.Data != null && !string.IsNullOrEmpty(src.Data.PlayerName)) name = Strip(src.Data.PlayerName).Trim(); }
            catch { }

            try
            {
                InnerNetClient net = AmongUsClient.Instance != null ? (InnerNetClient)AmongUsClient.Instance : null;
                ClientData c = net != null && src != null ? net.GetClientFromCharacter(src) : null;
                if (c != null)
                {
                    id = c.Id;
                    if (!string.IsNullOrWhiteSpace(c.FriendCode)) fc = c.FriendCode.Trim();
                    if (!string.IsNullOrWhiteSpace(c.ProductUserId)) puid = c.ProductUserId.Trim();
                    if (puid.Length > 12) puid = puid.Substring(0, 12);
                    if (c.PlatformData != null) plat = c.PlatformData.Platform.ToString();
                }
            }
            catch { }

            string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {name} | fc:{fc} | id:{id} | puid:{puid} | {plat} | {OneLine(text)}";
            lock (Lock)
            {
                Directory.CreateDirectory(Dir);
                File.AppendAllText(LogPath, line + Environment.NewLine, Encoding.UTF8);
            }
        }
        catch { }
    }

    private static string OneLine(string v) => string.IsNullOrEmpty(v) ? "" : v.Replace("\r", " ").Replace("\n", " ").Trim();

    private static string Strip(string v)
    {
        if (string.IsNullOrEmpty(v)) return "";
        var sb = new StringBuilder(v.Length);
        bool tag = false;
        foreach (char c in v)
        {
            if (c == '<') { tag = true; continue; }
            if (c == '>') { tag = false; continue; }
            if (!tag) sb.Append(c);
        }
        return sb.ToString();
    }
}

internal static class NocturneBanWords
{
    private static readonly object Lock = new object();
    private static readonly List<string> Words = new List<string>();
    private static float nextReload;
    private static string Path0 => Path.Combine(Path.Combine(BepInEx.Paths.GameRootPath, "Nocturne"), "BanWords.txt");

    internal static void Init()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path0));
            string header = NocturneText.T("# BanWords.txt — одно слово на строку, # = комментарий", "# BanWords.txt — one word per line, # = comment");
            if (!File.Exists(Path0))
            {
                File.WriteAllText(Path0, header + "\n", Encoding.UTF8);
            }
            else
            {
                var lines = new List<string>(File.ReadAllLines(Path0, Encoding.UTF8));
                if (lines.Count > 0 && lines[0].StartsWith("# BanWords.txt", StringComparison.Ordinal) && lines[0] != header)
                {
                    lines[0] = header;
                    File.WriteAllText(Path0, string.Join("\n", lines) + "\n", Encoding.UTF8);
                }
            }
        }
        catch { }
        Reload();
    }

    internal static void TickReload()
    {
        float now = UnityEngine.Time.realtimeSinceStartup;
        if (now < nextReload) return;
        nextReload = now + 30f;
        Reload();
    }

    internal static string Censor(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        lock (Lock)
        {
            if (Words.Count == 0) return text;
            string lower = text.ToLowerInvariant();
            foreach (string w in Words)
            {
                int from = 0, idx;
                while ((idx = lower.IndexOf(w, from, StringComparison.Ordinal)) >= 0)
                {
                    text = text.Remove(idx, w.Length).Insert(idx, new string('*', w.Length));
                    lower = text.ToLowerInvariant();
                    from = idx + w.Length;
                }
            }
        }
        return text;
    }

    private static void Reload()
    {
        try
        {
            if (!File.Exists(Path0)) return;
            var fresh = new List<string>();
            foreach (string line in File.ReadAllLines(Path0, Encoding.UTF8))
            {
                string w = line.Trim().ToLowerInvariant();
                if (!string.IsNullOrEmpty(w) && !w.StartsWith("#")) fresh.Add(w);
            }
            lock (Lock) { Words.Clear(); Words.AddRange(fresh); }
        }
        catch { }
    }
}

[HarmonyPatch(typeof(ChatController), nameof(ChatController.AddChat))]
[HarmonyPriority(Priority.First)]
internal static class ChatCensorPatch
{
    public static void Prefix(ref string chatText)
    {
        if (!NocturneConfig.BanWords.Value) return;
        NocturneBanWords.TickReload();
        chatText = NocturneBanWords.Censor(chatText);
    }
}

[HarmonyPatch(typeof(ChatController), nameof(ChatController.AddChat))]
internal static class ChatLogPatch
{
    public static void Postfix(PlayerControl sourcePlayer, string chatText)
    {
        NocturneChatLog.Record(sourcePlayer, chatText);
    }
}
