namespace Nocturne;

internal static class NocturneSecurityNotify
{
    private static bool On => NocturneConfig.SecurityNotify != null && NocturneConfig.SecurityNotify.Value;

    internal static void Fire(string ru, string en, NocturneNotifyKind kind = NocturneNotifyKind.Danger)
    {
        string msg = NocturneText.T(ru, en);
        try { NocturneEventLog.Add(msg, kind); } catch { }
        if (!On) return;
        try { NocturneToast.Push(NocturneText.T("Защита", "Guard"), msg, 3.5f, kind); } catch { }
    }
}
