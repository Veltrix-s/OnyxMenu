using System;
using System.Collections.Concurrent;

namespace Nocturne;

internal static class MainThread
{
    private static readonly ConcurrentQueue<Action> Queue = new ConcurrentQueue<Action>();

    internal static void Post(Action work) => Queue.Enqueue(work);

    internal static void Drain()
    {
        while (Queue.TryDequeue(out Action work))
            work();
    }
}
