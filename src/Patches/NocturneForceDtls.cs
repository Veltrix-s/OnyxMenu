using HarmonyLib;
using InnerNet;

namespace Nocturne.Patches;

[HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.SetEndpoint))]
internal static class NocturneForceDtls
{
    public static void Prefix(ref bool dtls)
    {
        if (NocturneConfig.ForceDtls.Value) dtls = true;
    }
}
