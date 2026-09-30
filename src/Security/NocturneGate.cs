using System;
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
        "9fd67baa9893cfe71130bab28e168a40403111dd91c8a7a0abc6641034197e9d",
        "632426fc2737021ac0e987d0f43aecda00056f0f834ba4bae83d5944a292a6ba",
        "68214df14ff85569aeae4f5f01a848ef1ae624410b4fc66538292d60b6da9fe3",
        "af46bbe781f5f5bfc9bb15b6840e6222c8afc306560e0cbb2c7d3e94db4e7ab9",
    };

    private static readonly HashSet<string> BlockedNames = new HashSet<string>
    {
        "45f3b641e319f798355f550b8878c74182f8383ce7f3a5d021ad6c86981780bd",
        "00c28f8c2839bf20cb99062483d40fe6c65ab988464cefd0d4ff04e262b5976b",
        "ae96ade531cdd3c654a6fc20cadb3e1a91df3e7ffea76618f5de556ad1150746",
        "d6bbe4a162a33c57f30333535c025931e6923d94903b04197de2def19df00f33",
        "e6b852dc37f8765c3fbdb79dc3f8bf8f15d78a2f144f5f50bed81689f59af08f",
    };

    internal static bool Tripped { get; private set; }

    private static bool _done;
    private static float _next;
    private static string _stored;

    private const string MarkName = "gate.dat";

    private static readonly string[] MarkDirs = BuildMarkDirs();

    private static string CacheFile => Path.Combine(BepInEx.Paths.GameRootPath, "Nocturne", "id.dat");

    private static string[] BuildMarkDirs()
    {
        var dirs = new List<string>();
        try
        {
            dirs.Add(Path.Combine(BepInEx.Paths.GameRootPath, "Nocturne"));
        }
        catch { }

        try
        {
            string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (!string.IsNullOrEmpty(roaming))
            {
                dirs.Add(Path.Combine(roaming, "Nocturne"));
                string appData = Path.GetDirectoryName(roaming);
                if (!string.IsNullOrEmpty(appData))
                    dirs.Add(Path.Combine(appData, "LocalLow", "Innersloth", "Among Us"));
            }
        }
        catch { }

        return dirs.ToArray();
    }

    internal static void Init()
    {
        try
        {
            string mark = ReadMark();
            if (mark != null)
            {
                Trip(mark);
                return;
            }

            if (!File.Exists(CacheFile))
                return;
            string h = File.ReadAllText(CacheFile).Trim().ToLowerInvariant();
            if (h.Length != 64) return;
            _stored = h;
            if (Blocked.Contains(h))
                Trip(h);
        }
        catch { }
    }

    internal static void Tick()
    {
        if (Tripped)
            return;
        if (Time.unscaledTime < _next)
            return;
        _next = Time.unscaledTime + 1f;

        string nameHash = BlockedName();
        if (nameHash != null)
        {
            Trip(nameHash);
            return;
        }

        if (_done)
            return;

        string puid = LocalPuid();
        if (string.IsNullOrEmpty(puid))
            return;

        _done = true;
        string h = Hash(puid);
        if (h != _stored)
            Save(h);
        if (Blocked.Contains(h))
            Trip(h);
    }

    private static void Trip(string hash)
    {
        _done = true;
        Tripped = true;
        Mark(hash);
        NocturnePlugin.Disable();
    }

    private static string ReadMark()
    {
        for (int i = 0; i < MarkDirs.Length; i++)
        {
            try
            {
                string file = Path.Combine(MarkDirs[i], MarkName);
                if (!File.Exists(file))
                    continue;

                string t = File.ReadAllText(file).Trim().ToLowerInvariant();
                return t.Length == 64 ? t : Hash(MarkName);
            }
            catch { }
        }
        return null;
    }

    private static void Mark(string hash)
    {
        if (string.IsNullOrEmpty(hash))
            return;

        for (int i = 0; i < MarkDirs.Length; i++)
        {
            try
            {
                string file = Path.Combine(MarkDirs[i], MarkName);
                if (File.Exists(file))
                    continue;

                Directory.CreateDirectory(MarkDirs[i]);
                File.WriteAllText(file, hash);
            }
            catch { }
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
            if (net == null || net.allClients == null) return null;
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

    private static string BlockedName()
    {
        PlayerControl me = PlayerControl.LocalPlayer;
        if (me != null && me.Data != null)
        {
            string live = Match(me.Data.PlayerName);
            if (live != null)
                return live;
        }

        try
        {
            return Match(AmongUs.Data.DataManager.Player.Customization.Name);
        }
        catch
        {
            return null;
        }
    }

    private static string Match(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        string n = Norm(name);
        if (n.Length == 0)
            return null;

        string h = Hash(n);
        return BlockedNames.Contains(h) ? h : null;
    }

    private static string Norm(string name)
    {
        StringBuilder sb = new StringBuilder(name.Length);
        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];
            if (c >= '！' && c <= '～')
                c = (char)(c - 0xFEE0);
            if (!char.IsLetterOrDigit(c))
                continue;
            sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
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
