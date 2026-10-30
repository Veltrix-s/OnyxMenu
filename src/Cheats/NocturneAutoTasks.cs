using UnityEngine;

namespace Nocturne;

public sealed class NocturneAutoTasks : MonoBehaviour
{
    private float _next;

    private static bool Ready()
    {
        if (ShipStatus.Instance == null) return false;
        GameManager gm = GameManager.Instance;
        if (gm == null || !gm.GameHasStarted) return false;
        if (MeetingHud.Instance != null || ExileController.Instance != null) return false;
        PlayerControl me = PlayerControl.LocalPlayer;
        if (me == null || me.Data == null || me.Data.IsDead || me.Data.Disconnected) return false;
        if (me.Data.Role != null && me.Data.Role.IsImpostor) return false;
        return me.myTasks != null;
    }

    internal static int Left()
    {
        int n = 0;
        PlayerControl me = PlayerControl.LocalPlayer;
        if (me == null || me.myTasks == null) return 0;
        for (int i = 0; i < me.myTasks.Count; i++)
        {
            PlayerTask t = me.myTasks[i];
            if (t != null && !t.IsComplete)
                n++;
        }
        return n;
    }

    public void FixedUpdate()
    {
        if (!NocturneConfig.AutoTasks.Value || !Ready()) return;
        if (Time.time < _next) return;

        float gap = Ui.Max(0.8f, NocturneConfig.AutoTasksDelay.Value);
        _next = Time.time + gap + UnityEngine.Random.Range(0f, gap * 0.35f);

        PlayerControl me = PlayerControl.LocalPlayer;
        for (int i = 0; i < me.myTasks.Count; i++)
        {
            PlayerTask t = me.myTasks[i];
            if (t == null || t.IsComplete) continue;
            me.RpcCompleteTask(t.Id);
            return;
        }
    }
}
