using UnityEngine;
using Object = UnityEngine.Object;

namespace Nocturne;

internal static class PhantomPoof
{
    internal static void Vanish(PlayerControl pc)
    {
        RoleManager rm = RoleManager.Instance;
        if (rm != null) Play(rm.vanish_PoofAnim, pc);
    }

    internal static void Appear(PlayerControl pc)
    {
        RoleManager rm = RoleManager.Instance;
        if (rm != null) Play(rm.appear_PoofAnim, pc);
    }

    internal static void Charge(PlayerControl pc)
    {
        RoleManager rm = RoleManager.Instance;
        if (rm != null) Play(rm.vanish_ChargeAnim, pc);
    }

    private static void Play(RoleEffectAnimation prefab, PlayerControl pc)
    {
        if (prefab == null || pc == null || pc.cosmetics == null)
            return;

        try
        {
            RoleEffectAnimation anim = Object.Instantiate(prefab, pc.transform);
            anim.SetMaterialColor(pc.CurrentOutfit != null ? pc.CurrentOutfit.ColorId : 0);
            anim.Play(pc, null, pc.cosmetics.FlipX, RoleEffectAnimation.SoundType.Local, 0f, true, 0f);
        }
        catch { }
    }
}
