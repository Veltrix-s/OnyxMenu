using System;

namespace Nocturne;

internal static class NocturneProfiler
{
    internal static bool Enabled => false;
    internal static IDisposable Sample(string name) => null;
    internal static void RecordAudioCallback(long startTicks) { }
}
