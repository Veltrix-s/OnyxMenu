using System.Collections.Generic;
using HarmonyLib;
using Hazel;
using InnerNet;
using UnityEngine;

namespace Nocturne;

internal static class ModHandshake
{
    private const byte Call = 242;
    private const float Interval = 3f;
    private const int Burst = 8;

    private static readonly Dictionary<byte, string> Seen = new Dictionary<byte, string>();
    private static readonly HashSet<byte> Toasted = new HashSet<byte>();
    private static float _next;
    private static int _sent;

    internal static int Count => Seen.Count;

    internal static string ModOf(byte pid) => Seen.TryGetValue(pid, out string s) ? s : null;

    internal static void Reset()
    {
        Seen.Clear();
        Toasted.Clear();
        _next = 0f;
        _sent = 0;
    }

    internal static void Tick()
    {
        if (_sent >= Burst || Time.unscaledTime < _next)
            return;

        PlayerControl me = PlayerControl.LocalPlayer;
        if (me == null || me.NetId == 0) return;

        var net = AmongUsClient.Instance;
        if (net == null || net.GameState != InnerNetClient.GameStates.Joined)
            return;
        if (net.NetworkMode == NetworkModes.FreePlay) return;

        _next = Time.unscaledTime + Interval;
        _sent++;

        try
        {
            MessageWriter w = net.StartRpcImmediately(me.NetId, Call, SendOption.Reliable, -1);
            if (w == null) return;
            w.Write(NocturnePlugin.PluginName);
            w.Write(NocturnePlugin.PluginVersion);
            w.Write(net.AmHost);
            net.FinishRpcImmediately(w);
        }
        catch { }
    }

    internal static void Receive(PlayerControl src, MessageReader reader)
    {
        if (src == null || src == PlayerControl.LocalPlayer) return;

        MessageReader copy = null;
        string name, ver;
        bool host;
        try
        {
            copy = MessageReader.Get(reader);
            name = copy.ReadString();
            ver = copy.ReadString();
            host = copy.ReadBoolean();
        }
        catch
        {
            return;
        }
        finally { try { copy?.Recycle(); } catch { } }

        if (string.IsNullOrEmpty(name) || name.Length > 32 || ver == null || ver.Length > 16) return;

        byte pid = src.PlayerId;
        Seen[pid] = name + " " + ver;

        if (!NocturneConfig.ModHandshake.Value || !Toasted.Add(pid)) return;

        string who = src.Data != null ? NocturneNameColor.Strip(src.Data.PlayerName) : "?";
        NocturneToast.Push(NocturneText.T("Свой", "Mod user"),
            who + " · " + name + " " + ver + (host ? NocturneText.T(" · хост", " · host") : ""),
            4f, NocturneNotifyKind.Info);
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.HandleRpc))]
internal static class ModHandshakeRpcPatch
{
    public static bool Prefix(PlayerControl __instance, [HarmonyArgument(0)] byte callId, [HarmonyArgument(1)] MessageReader reader)
    {
        if (callId != 242 || reader == null) return true;
        ModHandshake.Receive(__instance, reader);
        return false;
    }
}
