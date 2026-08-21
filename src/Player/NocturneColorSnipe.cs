using UnityEngine;

namespace Nocturne;

public sealed class NocturneColorSnipe : MonoBehaviour
{
    private float _next;
    private int _lastTry = -1;

    internal static int Max()
    {
        try { if (Palette.PlayerColors != null) return Mathf.Max(0, Palette.PlayerColors.Length - 1); }
        catch { }
        return 17;
    }

    internal static bool Taken(int id, PlayerControl self)
    {
        try
        {
            var e = PlayerControl.AllPlayerControls.GetEnumerator();
            while (e.MoveNext())
            {
                PlayerControl p = e.Current;
                if (p == null || p == self || p.Data == null || p.Data.Disconnected) continue;
                if (p.Data.DefaultOutfit != null && p.Data.DefaultOutfit.ColorId == id) return true;
            }
        }
        catch { }
        return false;
    }

    public void Update()
    {
        if (!NocturneConfig.SnipeColor.Value || LobbyBehaviour.Instance == null) { _lastTry = -1; return; }

        PlayerControl me = PlayerControl.LocalPlayer;
        if (me == null || me.Data == null || me.Data.DefaultOutfit == null) return;
        if (Time.time < _next) return;
        _next = Time.time + 0.25f;

        int want = Mathf.Clamp(NocturneConfig.SnipeColorId.Value, 0, Max());
        if (me.Data.DefaultOutfit.ColorId == want) { _lastTry = -1; return; }
        if (Taken(want, me)) { _lastTry = -1; return; }
        if (_lastTry == want) return;

        _lastTry = want;
        try { me.CmdCheckColor((byte)want); } catch { }
    }
}
