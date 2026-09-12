namespace Nocturne;

internal static class Dleks
{
    private const int Skeld = 0;
    private const int Mirror = 3;

    private static bool _applied;

    internal static void Sync()
    {
        bool want = NocturneConfig.Dleks.Value;
        if (want == _applied) return;

        if (AmongUsClient.Instance == null || ShipStatus.Instance != null) return;

        var prefabs = AmongUsClient.Instance.ShipPrefabs;
        if (prefabs == null || prefabs.Count <= Mirror) return;

        var keep = prefabs[Skeld];
        prefabs[Skeld] = prefabs[Mirror];
        prefabs[Mirror] = keep;
        _applied = want;
    }
}
