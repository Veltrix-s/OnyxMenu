using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace Nocturne;

internal static class Prof
{
    private const float Period = 10f;
    private const int Top = 18;

    private sealed class Slot
    {
        internal string Name;
        internal long Ticks;
        internal long Max;
        internal int Calls;
    }

    private static readonly string[] Loops = { "Update", "FixedUpdate", "LateUpdate", "OnGUI", "DrawGui" };
    private static readonly string[] Statics = { "Tick", "AutoTick", "EnforceVentMode", "TickHud", "TickGhosts", "Draw", "Drag" };

    private static readonly Dictionary<MethodBase, Slot> Slots = new Dictionary<MethodBase, Slot>();
    private static readonly List<Slot> Rows = new List<Slot>();
    private static readonly StringBuilder Text = new StringBuilder(1536);

    private static Harmony _harmony;
    private static bool _on;
    private static float _since;
    private static float _accum;
    private static int _frames;
    private static long _alloc;
    private static int _gcs;

    internal static void Tick()
    {
        bool want = NocturneConfig.Profiler != null && NocturneConfig.Profiler.Value;
        if (want != _on)
        {
            if (want)
                Start();
            else
                Stop();
        }
        if (!_on)
            return;

        _accum += Time.unscaledDeltaTime;
        _frames++;
        float now = Time.unscaledTime;
        if (now - _since >= Period)
            Flush(now);
    }

    private static void Start()
    {
        _on = true;
        lock (Slots)
        {
            Slots.Clear();
            Rows.Clear();
        }
        _since = Time.unscaledTime;
        _accum = 0f;
        _frames = 0;
        _alloc = GC.GetTotalAllocatedBytes(false);
        _gcs = GC.CollectionCount(0);

        int patched = 0;
        try
        {
            _harmony = new Harmony("nocturne.prof");
            var pre = new HarmonyMethod(typeof(Prof).GetMethod(nameof(Pre), BindingFlags.Static | BindingFlags.NonPublic));
            var post = new HarmonyMethod(typeof(Prof).GetMethod(nameof(Post), BindingFlags.Static | BindingFlags.NonPublic));

            const BindingFlags all = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            foreach (Type t in typeof(NocturnePlugin).Assembly.GetTypes())
            {
                bool loop = typeof(MonoBehaviour).IsAssignableFrom(t);
                bool patch = t.IsDefined(typeof(HarmonyPatch), false);
                foreach (MethodInfo m in t.GetMethods(all))
                {
                    if (m.IsAbstract || m.IsGenericMethodDefinition || m.GetMethodBody() == null)
                        continue;

                    bool want = false;
                    if (loop && !m.IsStatic && m.GetParameters().Length == 0)
                        want = Array.IndexOf(Loops, m.Name) >= 0;
                    else if (m.IsStatic && m.GetParameters().Length == 0)
                        want = Array.IndexOf(Statics, m.Name) >= 0;
                    if (!want && patch && m.IsStatic)
                        want = m.Name == "Prefix" || m.Name == "Postfix";
                    if (!want)
                        continue;

                    try
                    {
                        _harmony.Patch(m, pre, post);
                        patched++;
                    }
                    catch { }
                }
            }
        }
        catch (Exception e)
        {
            NocturnePlugin.Logger?.LogWarning("[Prof] start: " + e.Message);
        }
        NocturnePlugin.Logger?.LogInfo($"[Prof] on, {patched} methods");
    }

    private static void Stop()
    {
        _on = false;
        try
        {
            _harmony?.UnpatchSelf();
        }
        catch { }
        _harmony = null;
        NocturnePlugin.Logger?.LogInfo("[Prof] off");
    }

    private static void Pre(out long __state) => __state = Stopwatch.GetTimestamp();

    private static void Post(MethodBase __originalMethod, long __state)
    {
        long dt = Stopwatch.GetTimestamp() - __state;
        lock (Slots)
        {
            if (!Slots.TryGetValue(__originalMethod, out Slot s))
            {
                string owner = __originalMethod.DeclaringType != null ? __originalMethod.DeclaringType.FullName : "?";
                if (owner.StartsWith("Nocturne.", StringComparison.Ordinal))
                    owner = owner.Substring(9);
                s = new Slot { Name = owner + "." + __originalMethod.Name };
                Slots[__originalMethod] = s;
                Rows.Add(s);
            }
            s.Ticks += dt;
            s.Calls++;
            if (dt > s.Max)
                s.Max = dt;
        }
    }

    private static void Flush(float now)
    {
        float span = now - _since;
        int frames = Math.Max(1, _frames);
        double ms = _accum * 1000.0 / frames;
        double fps = frames / Math.Max(0.0001f, _accum);
        long alloc = GC.GetTotalAllocatedBytes(false);
        int gcs = GC.CollectionCount(0);

        Text.Clear();
        Text.Append("[Prof] ")
            .Append(span.ToString("0.0", CultureInfo.InvariantCulture)).Append(" s, ")
            .Append(frames).Append(" frames, ")
            .Append(ms.ToString("0.00", CultureInfo.InvariantCulture)).Append(" ms/frame (")
            .Append(fps.ToString("0", CultureInfo.InvariantCulture)).Append(" fps), managed alloc ")
            .Append(((alloc - _alloc) / 1024.0 / Math.Max(0.1f, span)).ToString("0", CultureInfo.InvariantCulture)).Append(" KB/s, gen0 GCs ")
            .Append(gcs - _gcs).Append('\n');

        lock (Slots)
        {
            Rows.Sort((a, b) => b.Ticks.CompareTo(a.Ticks));
            double freq = Stopwatch.Frequency;
            int n = Math.Min(Top, Rows.Count);
            for (int i = 0; i < n; i++)
            {
                Slot s = Rows[i];
                Text.Append("  ").Append(s.Name.PadRight(48))
                    .Append((s.Ticks * 1000.0 / freq / frames).ToString("0.000", CultureInfo.InvariantCulture)).Append(" ms/frame  ")
                    .Append(((double)s.Calls / frames).ToString("0.0", CultureInfo.InvariantCulture)).Append(" calls/frame  max ")
                    .Append((s.Max * 1000.0 / freq).ToString("0.00", CultureInfo.InvariantCulture)).Append(" ms\n");
            }
            for (int i = 0; i < Rows.Count; i++)
            {
                Rows[i].Ticks = 0;
                Rows[i].Calls = 0;
                Rows[i].Max = 0;
            }
        }

        NocturnePlugin.Logger?.LogInfo(Text.ToString());
        _since = now;
        _accum = 0f;
        _frames = 0;
        _alloc = alloc;
        _gcs = gcs;
    }
}
