using System;
using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using Discord;
using HarmonyLib;
using Il2CppInterop.Runtime.Attributes;
using InnerNet;
using UnityEngine;

namespace Nocturne;

public sealed class NocturneDiscordPresence : MonoBehaviour
{
    private const long AppId = 1523454610860412999L;
    private const float Tick = 2f;
    private const string Site = "https://onyxmenu.kawas-set.workers.dev";

    private NamedPipeClientStream _pipe;
    private volatile bool _up;
    private volatile bool _dialing;
    private volatile bool _dialFail;
    private volatile int _gen;
    private long _start;
    private float _next;
    private float _retryAt = -1f;
    private int _fails;
    private string _last;
    private int _pid;

    public void Start()
    {
        _start = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        try { _pid = Process.GetCurrentProcess().Id; } catch { _pid = 0; }
    }

    public void Update()
    {
        if ((int)Application.platform == 11) return;

        if (!_up)
        {
            if (_dialing) return;
            if (_dialFail)
            {
                _dialFail = false;
                _fails++;
                _retryAt = Time.unscaledTime + (_fails < 5 ? 30f : 120f);
                return;
            }
            if (_retryAt >= 0f && Time.unscaledTime < _retryAt) return;
            _dialing = true;
            ThreadPool.QueueUserWorkItem(DialWork);
            return;
        }

        _fails = 0;
        _retryAt = -1f;
        if (Time.unscaledTime < _next) return;
        _next = Time.unscaledTime + Tick;

        try { Push(); }
        catch { Drop(); _retryAt = Time.unscaledTime + 30f; }
    }

    [HideFromIl2Cpp]
    private void DialWork(object _) => Dial();

    public void OnDestroy() => Drop();

    [HideFromIl2Cpp]
    private void Dial()
    {
        int gen = _gen;
        try
        {
            for (int i = 0; i < 10; i++)
            {
                if (gen != _gen) return;
                NamedPipeClientStream p = null;
                try
                {
                    p = new NamedPipeClientStream(".", "discord-ipc-" + i, PipeDirection.InOut, PipeOptions.None);
                    p.Connect(200);
                    if (!p.IsConnected) { try { p.Dispose(); } catch { } continue; }

                    _pipe = p;
                    if (Frame(0, "{\"v\":1,\"client_id\":\"" + AppId + "\"}"))
                    {
                        if (gen != _gen) { try { p.Dispose(); } catch { } _pipe = null; return; }
                        _up = true;
                        return;
                    }
                    try { p.Dispose(); } catch { }
                    if (_pipe == p) _pipe = null;
                }
                catch { try { p?.Dispose(); } catch { } }
            }
            if (gen == _gen) { _pipe = null; _dialFail = true; }
        }
        finally { _dialing = false; }
    }

    [HideFromIl2Cpp]
    private void Push()
    {
        string details = "💎 NocturneMenu · v" + NocturnePlugin.PluginVersion;
        string state = Scene();
        string large = LargeText();
        var party = Party();

        var sb = new StringBuilder(640);
        sb.Append("{\"cmd\":\"SET_ACTIVITY\",\"args\":{\"pid\":").Append(_pid);
        sb.Append(",\"activity\":{\"details\":\"");
        Esc(sb, details);
        sb.Append("\",\"state\":\"");
        Esc(sb, state);
        sb.Append("\",\"timestamps\":{\"start\":").Append(_start).Append('}');
        if (party != null)
        {
            sb.Append(",\"party\":{\"id\":\"");
            Esc(sb, party.Value.id);
            sb.Append("\",\"size\":[").Append(party.Value.cur).Append(',').Append(party.Value.max).Append("]}");
        }
        sb.Append(",\"assets\":{\"large_image\":\"nocturne\",\"large_text\":\"");
        Esc(sb, large);
        sb.Append("\"}");
        sb.Append(",\"buttons\":[{\"label\":\"Website\",\"url\":\"");
        Esc(sb, Site);
        sb.Append("\"}]");
        sb.Append("}}");

        string body = sb.ToString();
        if (body == _last) return;
        _last = body;

        sb.Append(",\"nonce\":\"").Append(Guid.NewGuid().ToString()).Append("\"}");
        Frame(1, sb.ToString());
    }

    [HideFromIl2Cpp]
    private static string Scene()
    {
        try
        {
            string m = MapName();
            string tail = m != null ? " · " + m : "";
            if (MeetingHud.Instance != null) return "🗳 " + NocturneText.T("Собрание", "Meeting") + tail;
            if (ShipStatus.Instance != null) return "🎮 " + NocturneText.T("В игре", "In game") + tail;
            if (LobbyBehaviour.Instance != null) return "🚪 " + NocturneText.T("Лобби", "Lobby") + tail;
        }
        catch { }
        return "🌟 " + NocturneText.T("В меню", "In menu");
    }

    [HideFromIl2Cpp]
    private static string LargeText()
    {
        try
        {
            string m = MapName();
            if (m != null && (LobbyBehaviour.Instance != null || ShipStatus.Instance != null))
                return "Nocturne for Among Us • " + m;
        }
        catch { }
        return "Nocturne for Among Us";
    }

    [HideFromIl2Cpp]
    private static string MapName()
    {
        try
        {
            byte id = 0;
            if (GameOptionsManager.Instance != null && GameOptionsManager.Instance.CurrentGameOptions != null)
                id = GameOptionsManager.Instance.CurrentGameOptions.MapId;
            switch (id)
            {
                case 0: return "The Skeld";
                case 1: return "Mira HQ";
                case 2: return "Polus";
                case 3: return "dlekS";
                case 4: return "Airship";
                case 5: return "Fungle";
            }
        }
        catch { }
        return null;
    }

    [HideFromIl2Cpp]
    private static (int cur, int max, string id)? Party()
    {
        try
        {
            bool lobby = LobbyBehaviour.Instance != null;
            bool game = ShipStatus.Instance != null;
            if (!lobby && !game) return null;

            int max = 15;
            try
            {
                if (GameOptionsManager.Instance != null && GameOptionsManager.Instance.CurrentGameOptions != null)
                    max = GameOptionsManager.Instance.CurrentGameOptions.MaxPlayers;
            }
            catch { }

            int cur = 0;
            try
            {
                var c = AmongUsClient.Instance != null ? ((InnerNetClient)AmongUsClient.Instance).allClients : null;
                if (c != null) cur = c.Count;
            }
            catch { }
            if (cur <= 0)
            {
                try { if (PlayerControl.AllPlayerControls != null) cur = PlayerControl.AllPlayerControls.Count; } catch { }
            }
            if (cur <= 0) return null;
            if (max < cur) max = cur;
            if (max <= 0) max = 15;

            string id = "nocturne-party";
            try
            {
                if (AmongUsClient.Instance != null)
                {
                    int g = ((InnerNetClient)AmongUsClient.Instance).GameId;
                    if (g != 0) id = "au-" + g;
                }
            }
            catch { }
            return (cur, max, id);
        }
        catch { return null; }
    }

    [HideFromIl2Cpp]
    private static void Esc(StringBuilder sb, string s)
    {
        if (s == null) return;
        foreach (char c in s)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < 0x20) sb.AppendFormat("\\u{0:X4}", (int)c);
                    else sb.Append(c);
                    break;
            }
        }
    }

    [HideFromIl2Cpp]
    private bool Frame(int op, string json)
    {
        if (_pipe == null || !_pipe.IsConnected) return false;
        try
        {
            byte[] data = Encoding.UTF8.GetBytes(json);
            byte[] head = new byte[8];
            BitConverter.GetBytes(op).CopyTo(head, 0);
            BitConverter.GetBytes(data.Length).CopyTo(head, 4);
            _pipe.Write(head, 0, 8);
            _pipe.Write(data, 0, data.Length);
            _pipe.Flush();
            return true;
        }
        catch { return false; }
    }

    [HideFromIl2Cpp]
    private void Drop()
    {
        _gen++;
        _up = false;
        try { _pipe?.Dispose(); } catch { }
        _pipe = null;
        _last = null;
    }
}

[HarmonyPatch(typeof(ActivityManager), nameof(ActivityManager.UpdateActivity))]
internal static class NocturneDiscordSilence
{
    public static bool Prefix() => false;
}
