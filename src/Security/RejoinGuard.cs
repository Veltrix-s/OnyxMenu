using System;
using System.Collections.Generic;
using HarmonyLib;
using InnerNet;
using UnityEngine;

namespace Nocturne;

internal static class RejoinGuard
{
    private const float Window = 60f;
    private const int Limit = 5;

    private sealed class Trail
    {
        internal float LeftAt = -1f;
        internal readonly List<float> Back = new List<float>();
    }

    private static readonly Dictionary<string, Trail> Trails = new Dictionary<string, Trail>();
    private static int _room;

    internal static void Joined(InnerNetClient net, ClientData c)
    {
        string key = Tracked(net, c);
        if (key == null || !Returned(key, Time.unscaledTime)) return;
        if (NocturneAccess.IsWhite(c) || NocturneAccess.OnCooldown(c.Id)) return;

        string who = NocturneAccess.SafeName(c);
        string action = NocturneConfig.RejoinAction.Value;
        NocturneAccess.Act(net, c.Id, action, who, NocturneText.T($"перезаходов больше {Limit} за {Window / 60f:0} мин", $"more than {Limit} rejoins in {Window / 60f:0} min"));
        NocturneEventLog.Add(NocturneText.T("Перезаходы: ", "Rejoins: ") + who + " · " + action.ToLowerInvariant(), NocturneNotifyKind.Warning, NocturneEventCat.Join);
    }

    internal static void Left(InnerNetClient net, ClientData c)
    {
        string key = Tracked(net, c);
        if (key != null) TrailOf(key).LeftAt = Time.unscaledTime;
    }

    private static string Tracked(InnerNetClient net, ClientData c)
    {
        if (net == null || !net.AmHost || c == null || c.Id == net.ClientId) return null;
        if (string.Equals(NocturneConfig.RejoinAction.Value, "Null", StringComparison.OrdinalIgnoreCase)) return null;

        if (net.GameId != _room)
        {
            Trails.Clear();
            _room = net.GameId;
        }
        if (!string.IsNullOrEmpty(c.ProductUserId)) return c.ProductUserId;
        return string.IsNullOrEmpty(c.FriendCode) ? null : c.FriendCode;
    }

    private static bool Returned(string key, float now)
    {
        Trail t = TrailOf(key);
        bool quick = t.LeftAt >= 0f && now - t.LeftAt <= Window;
        t.LeftAt = -1f;
        if (!quick) return false;

        t.Back.RemoveAll(at => now - at > Window);
        t.Back.Add(now);
        if (t.Back.Count <= Limit) return false;

        t.Back.Clear();
        return true;
    }

    private static Trail TrailOf(string key)
    {
        if (!Trails.TryGetValue(key, out Trail t))
        {
            t = new Trail();
            Trails[key] = t;
        }
        return t;
    }
}

[HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnPlayerLeft))]
internal static class RejoinLeavePatch
{
    public static void Postfix(AmongUsClient __instance, [HarmonyArgument(0)] ClientData client) => RejoinGuard.Left(__instance, client);
}
