namespace Nocturne;

internal static class NocturneSecurityNotify
{
    private static bool On => NocturneConfig.SecurityNotify.Value;

    internal static void Fire(string ru, string en, NocturneNotifyKind kind = NocturneNotifyKind.Danger)
    {
        string msg = NocturneText.T(ru, en);
        NocturneEventLog.Add(msg, kind);
        if (!On)
            return;
        try
        {
            NocturneToast.Push(NocturneText.T("Защита", "Guard"), msg, 3.5f, kind);
        }
        catch { }
    }
}
