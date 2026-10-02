using System.Net;
using System.Net.Sockets;
using System.Text;
using HarmonyLib;
using InnerNet;

namespace Nocturne.Patches;

internal static class CustomServer
{
    internal static bool Valid(string ip)
    {
        return !string.IsNullOrEmpty(ip) && ip.Split('.').Length == 4
            && IPAddress.TryParse(ip, out IPAddress addr) && addr.AddressFamily == AddressFamily.InterNetwork;
    }

    internal static bool Target(out string ip, out ushort port)
    {
        ip = (NocturneConfig.CustomServerIp.Value ?? string.Empty).Trim();
        int p = NocturneConfig.CustomServerPort.Value;
        port = (ushort)p;
        return NocturneConfig.CustomServerEnabled.Value && p >= 1 && p <= 65535 && Valid(ip);
    }

    internal static string SetIp(string text)
    {
        string clean = Only(text, "0123456789.:");
        int colon = clean.IndexOf(':');
        if (colon >= 0)
        {
            SetPort(clean.Substring(colon + 1));
            clean = clean.Substring(0, colon);
        }

        NocturneConfig.CustomServerIp.Value = clean;
        return clean;
    }

    internal static string SetPort(string text)
    {
        string digits = Only(text, "0123456789");
        if (int.TryParse(digits, out int port) && port >= 1 && port <= 65535)
            NocturneConfig.CustomServerPort.Value = port;
        return digits;
    }

    private static string Only(string s, string allowed)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;

        for (int i = 0; i < s.Length; i++)
        {
            if (allowed.IndexOf(s[i]) >= 0) continue;

            var sb = new StringBuilder(s.Length);
            foreach (char c in s)
            {
                if (allowed.IndexOf(c) >= 0)
                    sb.Append(c);
            }
            return sb.ToString();
        }
        return s;
    }
}

[HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.Connect))]
internal static class CustomServerPatch
{
    private static void Prefix(InnerNetClient __instance, [HarmonyArgument(0)] MatchMakerModes mode, [HarmonyArgument(1)] string matchmakerToken)
    {
        if (mode != MatchMakerModes.HostAndClient || string.IsNullOrEmpty(matchmakerToken))
            return;
        if ((int)__instance.NetworkMode != 1 || !CustomServer.Target(out string ip, out ushort port))
            return;

        __instance.SetEndpoint(ip, port, __instance.useDtls);
        NocturneToast.Push(NocturneText.T("Кастом сервер", "Custom server"), ip + ":" + port, 3f, NocturneNotifyKind.Info);
    }
}
