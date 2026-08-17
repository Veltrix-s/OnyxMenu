using UnityEngine;

namespace Nocturne;

public sealed class NocturneAutoTasks : MonoBehaviour
{
    private float _next;

    private static bool Ready()
    {
        try
        {
            if (ShipStatus.Instance == null) return false;
            if (MeetingHud.Instance != null || ExileController.Instance != null) return false;
            PlayerControl me = PlayerControl.LocalPlayer;
            if (me == null || me.Data == null || me.Data.IsDead || me.Data.Disconnected) return false;
            if (me.Data.Role != null && me.Data.Role.IsImpostor) return false;
            return me.myTasks != null;
        }
        catch { return false; }
    }

    internal static int Left()
    {
        int n = 0;
        try
        {
            PlayerControl me = PlayerControl.LocalPlayer;
            if (me == null || me.myTasks == null) return 0;
            for (int i = 0; i < me.myTasks.Count; i++)
            {
                PlayerTask t = me.myTasks[i];
                if (t != null && !t.IsComplete) n++;
            }
        }
        catch { }
        return n;
    }

    public void Update()
    {
        if (!NocturneConfig.AutoTasks.Value || !Ready()) return;
        if (Time.time < _next) return;

        float gap = Mathf.Max(0.8f, NocturneConfig.AutoTasksDelay.Value);
        _next = Time.time + gap + UnityEngine.Random.Range(0f, gap * 0.35f);

        try
        {
            PlayerControl me = PlayerControl.LocalPlayer;
            for (int i = 0; i < me.myTasks.Count; i++)
            {
                PlayerTask t = me.myTasks[i];
                if (t == null || t.IsComplete) continue;
                me.RpcCompleteTask((uint)t.Id);
                return;
            }
        }
        catch { }
    }
}
