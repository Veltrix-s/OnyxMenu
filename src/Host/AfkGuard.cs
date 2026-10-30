using System.Collections.Generic;
using InnerNet;
using UnityEngine;

namespace Nocturne;

public sealed class AfkGuard : MonoBehaviour
{
    private sealed class Spot
    {
        internal Vector2 Pos;
        internal float Since;
        internal int Pass;
        internal bool Warned;
        internal bool Done;
    }

    private readonly Dictionary<int, Spot> _spots = new Dictionary<int, Spot>();
    private readonly List<int> _gone = new List<int>();
    private float _next;
    private float _meetingAt;
    private int _pass;

    public void FixedUpdate()
    {
        if (!NocturneConfig.AfkGuardEnabled.Value)
        {
            _spots.Clear();
            return;
        }

        float now = Time.unscaledTime;
        if (now < _next) return;
        _next = now + 0.5f;

        if (!Utils.Host || !Utils.InGame)
        {
            _spots.Clear();
            _meetingAt = 0f;
            return;
        }

        if (MeetingHud.Instance != null || ExileController.Instance != null)
        {
            if (_meetingAt == 0f) _meetingAt = now;
            return;
        }

        if (_meetingAt > 0f)
        {
            float gap = now - _meetingAt;
            foreach (Spot s in _spots.Values)
                s.Since += gap;
            _meetingAt = 0f;
        }

        var net = AmongUsClient.Instance;
        if (net.allClients == null) return;

        _pass++;
        var e = net.allClients.GetEnumerator();
        while (e.MoveNext())
        {
            ClientData c = e.Current;
            if (c == null || c.Id == net.ClientId || c.Id == net.HostId) continue;

            PlayerControl pc = c.Character;
            if (pc == null || pc.Data == null || pc.Data.IsDead || pc.Data.Disconnected) continue;
            if (NocturneAccess.IsWhite(c)) continue;

            Vector2 pos = pc.GetTruePosition();
            if (!_spots.TryGetValue(c.Id, out Spot sp))
            {
                _spots[c.Id] = new Spot { Pos = pos, Since = now, Pass = _pass };
                continue;
            }

            sp.Pass = _pass;
            if (pc.inVent || (pos - sp.Pos).sqrMagnitude > 0.0025f)
            {
                sp.Pos = pos;
                sp.Since = now;
                sp.Warned = false;
                sp.Done = false;
                continue;
            }

            Check(net, c, sp, now);
        }

        foreach (var kv in _spots)
        {
            if (kv.Value.Pass != _pass) _gone.Add(kv.Key);
        }
        for (int i = 0; i < _gone.Count; i++)
            _spots.Remove(_gone[i]);
        _gone.Clear();
    }

    private static void Check(InnerNetClient net, ClientData c, Spot sp, float now)
    {
        int timeout = NocturneConfig.AfkTimeoutSeconds.Value;
        float left = timeout - (now - sp.Since);
        if (left > 0f)
        {
            int lead = Ui.Min(NocturneConfig.AfkWarningSeconds.Value, timeout);
            if (sp.Warned || lead == 0 || left > lead || !NocturneConfig.AfkNotifications.Value) return;

            sp.Warned = true;
            string who = NocturneAccess.SafeName(c);
            int sec = Ui.CeilToInt(left);
            NocturneToast.Push(NocturneText.T("AFK-контроль", "AFK guard"), NocturneText.T($"{who}: действие через {sec} с", $"{who}: action in {sec} s"), 3f, NocturneNotifyKind.Warning);
            return;
        }

        if (sp.Done) return;
        sp.Done = true;

        string name = NocturneAccess.SafeName(c);
        string act = NocturneConfig.AfkAction.Value;
        NocturneAccess.Act(net, c.Id, act, name, NocturneText.T("нет движения", "no movement"));

        bool ban = string.Equals(act, "Ban", System.StringComparison.OrdinalIgnoreCase);
        NocturneEventLog.Add("AFK: " + name + " · " + (ban ? "ban" : "kick"), ban ? NocturneNotifyKind.Danger : NocturneNotifyKind.Warning);
    }
}
