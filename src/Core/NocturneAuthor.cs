using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using InnerNet;
using UnityEngine;

namespace Nocturne;

internal static class NocturneAuthor
{
    private static readonly HashSet<string> Marks = new HashSet<string>
    {
        "5e1cdc2d62706debc5f9760bd5a6bb579cf22cfb244f79eeba4057027e2e70d0",
        "a525a33119112bd9cbe4919d0771b77695237f498097b4fcedb1050f598c9c75",
    };

    private static readonly Dictionary<string, bool> seen = new Dictionary<string, bool>();

    internal static string Tag => "<color=#FF9A3C><b>◆ " + NocturneText.T("АВТОР NOCTURNE", "NOCTURNE AUTHOR") + "</b></color>";

    internal static string TagShort => "<color=#FF9A3C><b>◆ NOCTURNE</b></color>";

    private struct Mark { public bool Ok; public float Until; }

    private const float Ttl = 3f;

    private static readonly Dictionary<byte, Mark> byPlayer = new Dictionary<byte, Mark>();

    internal static bool Is(PlayerControl pc)
    {
        if (pc == null || pc == PlayerControl.LocalPlayer) return false;

        byte pid;
        try { pid = pc.PlayerId; } catch { return false; }

        float now = Time.unscaledTime;
        if (byPlayer.TryGetValue(pid, out Mark m) && now < m.Until) return m.Ok;

        bool ok = Resolve(pc);
        byPlayer[pid] = new Mark { Ok = ok, Until = now + Ttl };
        return ok;
    }

    private static bool Resolve(PlayerControl pc)
    {
        try { if (pc.Data != null && (Match(pc.Data.FriendCode) || Match(pc.Data.Puid))) return true; }
        catch { }

        try
        {
            ClientData c = AmongUsClient.Instance != null ? AmongUsClient.Instance.GetClientFromCharacter(pc) : null;
            if (c != null && (Match(c.FriendCode) || Match(c.ProductUserId))) return true;
        }
        catch { }

        return false;
    }

    internal static bool Match(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;

        string key = id.Trim().ToLowerInvariant();
        if (seen.TryGetValue(key, out bool known)) return known;

        bool ok = Marks.Contains(Sha(key));
        seen[key] = ok;
        return ok;
    }

    private static string Sha(string s)
    {
        try
        {
            using SHA256 sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(s));
            StringBuilder sb = new StringBuilder(64);
            foreach (byte b in hash) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
        catch { return string.Empty; }
    }
}
