using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using InnerNet;
using UnityEngine;

namespace Nocturne;

internal static class NocturneGate
{
    private const string Salt = "Nocturne::gate::v1";

    private static readonly HashSet<string> Blocked = new HashSet<string>
    {
        "9357aafd08701bd3edc54e05ff75e7a2864d0914780a069c4872f8b191c32ad2",
    };

    internal static bool Tripped { get; private set; }

    private static bool _done;
    private static float _next;
    private static string _stored;

    private static string CacheFile => Path.Combine(BepInEx.Paths.GameRootPath, "Nocturne", "id.dat");

    internal static void Init()
    {
        try
        {
            if (!File.Exists(CacheFile))
                return;
            string h = File.ReadAllText(CacheFile).Trim().ToLowerInvariant();
            if (h.Length != 64)
                return;
            _stored = h;
            if (Blocked.Contains(h))
            {
                _done = true;
                Tripped = true;
                NocturnePlugin.Disable();
            }
        }
        catch { }
    }

    internal static void Tick()
    {
        if (_done)
            return;
        if (Time.unscaledTime < _next)
            return;
        _next = Time.unscaledTime + 1f;

        string puid = LocalPuid();
        if (string.IsNullOrEmpty(puid))
            return;

        _done = true;
        string h = Hash(puid);
        if (h != _stored)
            Save(h);
        if (Blocked.Contains(h))
        {
            Tripped = true;
            NocturnePlugin.Disable();
        }
    }

    private static void Save(string hash)
    {
        _stored = hash;
        try
        {
            string dir = Path.Combine(BepInEx.Paths.GameRootPath, "Nocturne");
            Directory.CreateDirectory(dir);
            File.WriteAllText(CacheFile, hash);
        }
        catch { }
    }

    private static string LocalPuid()
    {
        try
        {
            InnerNetClient net = (InnerNetClient)AmongUsClient.Instance;
            if (net == null || net.allClients == null)
                return null;
            if (net.GameState != InnerNetClient.GameStates.Joined)
                return null;

            int myId = net.ClientId;
            if (myId < 0)
                return null;

            var e = net.allClients.GetEnumerator();
            while (e.MoveNext())
            {
                ClientData c = e.Current;
                if (c != null && c.Id == myId)
                {
                    string p = c.ProductUserId;
                    return string.IsNullOrWhiteSpace(p) ? null : p.Trim();
                }
            }
        }
        catch { }
        return null;
    }

    private static string Hash(string puid)
    {
        using SHA256 sha = SHA256.Create();
        byte[] raw = sha.ComputeHash(Encoding.UTF8.GetBytes(Salt + puid));
        StringBuilder sb = new StringBuilder(raw.Length * 2);
        for (int i = 0; i < raw.Length; i++)
            sb.Append(raw[i].ToString("x2"));
        return sb.ToString();
    }
}
