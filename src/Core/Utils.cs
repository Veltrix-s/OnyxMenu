using UnityEngine;

namespace Nocturne;

internal static class Utils
{
    internal static bool Host => AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost;

    internal static bool InGame => ShipStatus.Instance != null && LobbyBehaviour.Instance == null;

    internal static bool Anticheat => AmongUsClient.Instance != null && AmongUsClient.Instance.NetworkMode == NetworkModes.OnlineGame;

    internal static PlayerControl ById(byte id)
    {
        var all = PlayerControl.AllPlayerControls;
        if (all == null) return null;

        var e = all.GetEnumerator();
        while (e.MoveNext())
        {
            if (e.Current != null && e.Current.PlayerId == id) return e.Current;
        }
        return null;
    }

    internal static Vent NearestVent(Vector2 pos, out int index)
    {
        index = -1;
        if (ShipStatus.Instance == null) return null;

        var vents = ShipStatus.Instance.AllVents;
        if (vents == null) return null;

        Vent best = null;
        float bd = float.MaxValue;
        for (int i = 0; i < vents.Count; i++)
        {
            Vent v = vents[i];
            if (v == null) continue;

            float d = Vector2.Distance(pos, v.transform.position);
            if (d < bd)
            {
                bd = d;
                best = v;
                index = i;
            }
        }
        return best;
    }

    internal static int NearestVentIndex(Vector2 pos)
    {
        NearestVent(pos, out int i);
        return i;
    }

    internal static string Platform(Platforms p) => p switch
    {
        Platforms.StandaloneEpicPC => "Epic",
        Platforms.StandaloneSteamPC => "Steam",
        Platforms.StandaloneMac => "Mac",
        Platforms.StandaloneWin10 => "MS Store",
        Platforms.StandaloneItch => "Itch.io",
        Platforms.IPhone => "iOS",
        Platforms.Android => "Android",
        Platforms.Switch => "Switch",
        Platforms.Xbox => "Xbox",
        Platforms.Playstation => "PS",
        (Platforms)112 => "Starlight",
        _ => p.ToString(),
    };
}
