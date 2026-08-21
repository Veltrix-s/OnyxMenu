using UnityEngine;

namespace Nocturne;

public sealed class NocturneSpeed : MonoBehaviour
{
    private const float BaseSpeed = 2.5f;
    private const float BaseGhost = 3f;
    private bool _touched;

    public void Update()
    {
        PlayerControl me = PlayerControl.LocalPlayer;
        if (me == null || me.MyPhysics == null) return;

        bool on = NocturneConfig.SpeedMod.Value;
        bool inv = NocturneConfig.InvertControls.Value;

        if (on)
        {
            float m = Mathf.Clamp(NocturneConfig.SpeedMult.Value, 0f, 3f);
            float s = BaseSpeed * m, g = BaseGhost * m;
            me.MyPhysics.Speed = inv ? -s : s;
            me.MyPhysics.GhostSpeed = inv ? -g : g;
            _touched = true;
        }
        else if (inv)
        {
            me.MyPhysics.Speed = -Mathf.Abs(me.MyPhysics.Speed);
            me.MyPhysics.GhostSpeed = -Mathf.Abs(me.MyPhysics.GhostSpeed);
            _touched = true;
        }
        else if (_touched)
        {
            me.MyPhysics.Speed = BaseSpeed;
            me.MyPhysics.GhostSpeed = BaseGhost;
            _touched = false;
        }
    }
}
