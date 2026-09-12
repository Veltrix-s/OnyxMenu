using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using InnerNet;
using UnityEngine;

namespace Nocturne;

internal sealed class LobbyRow
{
    internal int Id;
    internal string Code = "?";
    internal string Region = "?";
    internal string Map = "?";
    internal string Host = "?";
    internal int Players;
    internal bool Public;
    internal string When = "";
}

internal static class LobbyHistory
{
    private const int Max = 25;

    private static readonly List<LobbyRow> Rows = new List<LobbyRow>();
    private static int _cur;
    private static float _next;
    private static bool _loaded;

    private static string Txt => Path.Combine(BepInEx.Paths.GameRootPath, "Nocturne", "LobbyHistory.txt");

    internal static IReadOnlyList<LobbyRow> Entries
    {
        get
        {
            Load();
            return Rows;
        }
    }

    internal static void Clear()
    {
        Rows.Clear();
        _cur = 0;
        Save();
    }

    internal static void Tick()
    {
        if (Time.unscaledTime < _next)
            return;
        _next = Time.unscaledTime + 2f;

        if (AmongUsClient.Instance == null)
            return;

        var net = (InnerNetClient)AmongUsClient.Instance;
        if (net.GameId == 0 || (LobbyBehaviour.Instance == null && ShipStatus.Instance == null))
        {
            _cur = 0;
            return;
        }

        Load();

        if (net.GameId != _cur)
        {
            _cur = net.GameId;
            var row = new LobbyRow
            {
                Id = _cur,
                Code = GameCode.IntToGameName(_cur),
                When = DateTime.Now.ToString("dd.MM HH:mm"),
            };
            Fill(row, net);
            Rows.Insert(0, row);
            while (Rows.Count > Max)
                Rows.RemoveAt(Rows.Count - 1);
            Save();
            return;
        }

        if (Rows.Count > 0 && Rows[0].Id == _cur)
            Fill(Rows[0], net);
    }

    private static void Fill(LobbyRow row, InnerNetClient net)
    {
        try
        {
            if (net.allClients != null && net.allClients.Count > row.Players)
                row.Players = net.allClients.Count;
            row.Public = AmongUsClient.Instance.IsGamePublic;
        }
        catch { }

        try
        {
            if (ServerManager.Instance != null && ServerManager.Instance.CurrentRegion != null)
                row.Region = ServerManager.Instance.CurrentRegion.Name;
        }
        catch { }

        row.Map = NocturneRadar.MapName();

        try
        {
            PlayerControl host = null;
            foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
                if (pc != null && pc.Data != null && pc.OwnerId == net.HostId)
                {
                    host = pc;
                    break;
                }
            if (host != null && host.Data != null)
            {
                string nm = NocturneNameColor.Strip(host.Data.PlayerName);
                if (nm.Length > 0)
                    row.Host = nm;
            }
        }
        catch { }
    }

    internal static void Rejoin(LobbyRow row)
    {
        if (row == null || row.Id == 0)
            return;

        try
        {
            AmongUsClient au = AmongUsClient.Instance;
            if (au == null)
                return;
            au.GameId = row.Id;
            var e = au.CoJoinOnlineGameFromCode(row.Id);
            if (e != null)
                au.StartCoroutine(e);
            NocturneToast.Push(NocturneText.T("Лобби", "Lobby"), NocturneText.T("Захожу в ", "Joining ") + row.Code, 2.5f, NocturneNotifyKind.Info);
        }
        catch { }
    }

    private static void Load()
    {
        if (_loaded)
            return;
        _loaded = true;

        try
        {
            if (!File.Exists(Txt))
                return;

            foreach (string line in File.ReadAllLines(Txt))
            {
                string[] p = line.Split('|');
                if (p.Length < 7)
                    continue;
                int.TryParse(p[0], out int id);
                int.TryParse(p[5], out int players);
                Rows.Add(new LobbyRow
                {
                    Id = id,
                    Code = p[1],
                    When = p[2],
                    Host = p[3],
                    Map = p[4],
                    Players = players,
                    Region = p[6],
                    Public = p.Length > 7 && p[7] == "1",
                });
                if (Rows.Count >= Max)
                    break;
            }
        }
        catch { }
    }

    private static void Save()
    {
        try
        {
            string dir = Path.GetDirectoryName(Txt);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var sb = new StringBuilder();
            for (int i = 0; i < Rows.Count; i++)
            {
                LobbyRow r = Rows[i];
                sb.Append(r.Id).Append('|').Append(r.Code).Append('|').Append(r.When).Append('|')
                  .Append(r.Host).Append('|').Append(r.Map).Append('|').Append(r.Players).Append('|')
                  .Append(r.Region).Append('|').Append(r.Public ? '1' : '0').Append('\n');
            }
            File.WriteAllText(Txt, sb.ToString());
        }
        catch { }
    }
}
