using UnityEngine;
using Object = UnityEngine.Object;

namespace Nocturne;

public sealed class NocturneAutoLobbyReturn : MonoBehaviour
{
    private const float AutoReturnDelaySeconds = 3f;
    private const float AutoReturnRetrySeconds = 0.4f;
    private const int AutoReturnMaxAttempts = 40;

    private int shown;
    private int attempt;
    private float nextAt;

    public void FixedUpdate()
    {
        if (!ShouldAutoReturn() || LobbyBehaviour.Instance != null)
        {
            shown = 0;
            return;
        }

        if (Time.unscaledTime < nextAt) return;
        nextAt = Time.unscaledTime + AutoReturnRetrySeconds;

        EndGameManager end = Object.FindObjectOfType<EndGameManager>();
        if (end == null) return;

        int id = end.GetInstanceID();
        if (shown != id)
        {
            shown = id;
            attempt = 0;
            nextAt = Time.unscaledTime + AutoReturnDelaySeconds;
            return;
        }

        if (attempt < 0 || attempt >= AutoReturnMaxAttempts) return;

        try
        {
            end.Navigation.NextGame();
            attempt = -1;
        }
        catch
        {
            attempt++;
        }
    }

    private static bool ShouldAutoReturn()
    {
        return NocturneConfig.AutoReturnLobbyAfterMatch.Value
            || Patches.NocturneAutoHostService.ShouldReturnAfterMatch;
    }
}
