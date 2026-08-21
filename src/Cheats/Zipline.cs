using System.Collections.Generic;
using Il2CppInterop.Runtime.InteropTypes;
using Object = UnityEngine.Object;

namespace Nocturne;

internal static class Zipline
{
    private static ShipStatus _ship;
    private static ZiplineBehaviour _line;

    internal static bool OnMap => Line() != null;

    private static ZiplineBehaviour Line()
    {
        ShipStatus ship = ShipStatus.Instance;
        if (ship == null) { _ship = null; _line = null; return null; }
        try { if (_ship == ship) return _line; } catch { _ship = null; }

        _ship = ship;
        FungleShipStatus fungle = ((Il2CppObjectBase)ship).TryCast<FungleShipStatus>();
        if (fungle != null && fungle.Zipline != null) _line = fungle.Zipline;
        else _line = Object.FindObjectOfType<ZiplineBehaviour>();
        return _line;
    }

    private static bool Send(PlayerControl target, ZiplineBehaviour line, bool fromTop)
    {
        try { target.RpcUseZipline(target, line, fromTop); }
        catch { return false; }
        return true;
    }

    internal static string Ride(PlayerControl target, bool fromTop)
    {
        if (target == null || target.Data == null || target.Data.Disconnected)
            return NocturneText.T("Нет цели.", "No target.");
        if (ShipStatus.Instance == null)
            return NocturneText.T("Только в матче.", "In match only.");

        ZiplineBehaviour line = Line();
        if (line == null)
            return NocturneText.T("Зиплайна нет — только Fungle.", "No zipline here — Fungle only.");

        if (!Send(target, line, fromTop)) return NocturneText.T("Не удалось.", "Failed.");
        return Dir(fromTop) + NocturneNameColor.Strip(target.Data.PlayerName);
    }

    internal static string RideSelected(bool fromTop)
    {
        if (ShipStatus.Instance == null) return NocturneText.T("Только в матче.", "In match only.");

        ZiplineBehaviour line = Line();
        if (line == null) return NocturneText.T("Зиплайна нет — только Fungle.", "No zipline here — Fungle only.");

        List<PlayerControl> targets = RideTargets.Players();
        if (targets.Count == 0) return NocturneText.T("Никто не отмечен.", "Nobody selected.");

        int n = 0;
        for (int i = 0; i < targets.Count; i++)
            if (Send(targets[i], line, fromTop)) n++;

        return Dir(fromTop) + n + "/" + targets.Count;
    }

    private static string Dir(bool fromTop) =>
        fromTop ? NocturneText.T("Вниз: ", "Down: ") : NocturneText.T("Вверх: ", "Up: ");
}
