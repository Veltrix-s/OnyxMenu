using UnityEngine;

namespace Nocturne;

internal static class TaskDrain
{
    private const float BurstTime = 2f;
    private const float RestTime = 1f;

    private static float _nextSend;
    private static float _phaseUntil;
    private static bool _resting = true;

    internal static bool Running => NocturneConfig.HnsDrain.Value && Ready();

    internal static void Tick()
    {
        if (!NocturneConfig.HnsDrain.Value || !Ready())
        {
            _resting = true;
            _phaseUntil = 0f;
            return;
        }

        float now = Time.unscaledTime;
        if (now >= _phaseUntil)
        {
            _resting = !_resting;
            _phaseUntil = now + (_resting ? RestTime : BurstTime);
        }
        if (_resting || now < _nextSend) return;

        _nextSend = now + NocturneConfig.HnsDrainStep.Value;
        Send();
    }

    private static bool Ready()
    {
        if (ShipStatus.Instance == null || !Patches.HnSSeekers.IsHnS()) return false;

        PlayerControl me = PlayerControl.LocalPlayer;
        return me != null && me.Data != null;
    }

    private static void Send()
    {
        PlayerControl me = PlayerControl.LocalPlayer;
        if (me == null || me.myTasks == null) return;

        try
        {
            for (int i = 0; i < me.myTasks.Count; i++)
            {
                PlayerTask t = me.myTasks[i];
                if (t != null) me.RpcCompleteTask((uint)t.Id);
            }
        }
        catch { }
    }
}
