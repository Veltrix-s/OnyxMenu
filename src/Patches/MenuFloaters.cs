using System.Collections.Generic;
using HarmonyLib;

namespace Nocturne;

internal static class MenuFloaters
{
    private static readonly List<PlayerParticles> Live = new List<PlayerParticles>(2);
    private static readonly List<PlayerParticles> Held = new List<PlayerParticles>(2);

    internal static bool Want => !NocturneConfig.Vanilla && NocturneConfig.HideMenuFloaters.Value;

    internal static void Track(PlayerParticles pp)
    {
        if (Live.Contains(pp) || Held.Contains(pp))
            return;
        Live.RemoveAll(Gone);
        Held.RemoveAll(Gone);
        Live.Add(pp);
        Sync();
    }

    internal static void Sync()
    {
        bool hide = Want;
        List<PlayerParticles> from = hide ? Live : Held;
        List<PlayerParticles> to = hide ? Held : Live;
        for (int i = 0; i < from.Count; i++)
        {
            PlayerParticles pp = from[i];
            if (pp == null)
                continue;
            if (hide)
            {
                pp.StopAllCoroutines();
                if (pp.pool != null)
                    pp.pool.ReclaimAll();
                pp.enabled = false;
            }
            else
            {
                pp.enabled = true;
                pp.Start();
            }
            to.Add(pp);
        }
        from.Clear();
    }

    private static bool Gone(PlayerParticles pp) => pp == null;
}

[HarmonyPatch(typeof(PlayerParticles), nameof(PlayerParticles.Start))]
internal static class MenuFloatersPatch
{
    public static void Postfix(PlayerParticles __instance) => MenuFloaters.Track(__instance);
}
