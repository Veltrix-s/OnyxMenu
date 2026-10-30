using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;

namespace Nocturne;

internal static class ErrLog
{
    private const int MaxSites = 256;
    private const int Burst = 200;
    private const double PerSecond = 100.0;
    private const int Depth = 3;

    private static readonly Assembly Mine = typeof(ErrLog).Assembly;
    private static readonly Type Self = typeof(ErrLog);
    private static readonly Dictionary<string, int> Sites = new Dictionary<string, int>();

    [ThreadStatic]
    private static bool _busy;

    [ThreadStatic]
    private static Exception _last;

    private static double _budget = Burst;
    private static long _stamp = Stopwatch.GetTimestamp();
    private static int _skipped;
    private static bool _on;

    internal static void Start()
    {
        if (_on) return;
        _on = true;
        AppDomain.CurrentDomain.FirstChanceException += OnThrow;
    }

    internal static void Stop()
    {
        if (!_on) return;
        _on = false;
        AppDomain.CurrentDomain.FirstChanceException -= OnThrow;
    }

    private static void OnThrow(object sender, FirstChanceExceptionEventArgs args)
    {
        Exception e = args.Exception;
        if (_busy || e is OperationCanceledException || ReferenceEquals(e, _last) || !NocturneConfig.ErrorLog.Value)
            return;

        _busy = true;
        _last = e;
        try
        {
            Report(e);
        }
        catch (Exception ex)
        {
            Stop();
            NocturnePlugin.Logger.LogWarning("[Err] tracer stopped: " + ex.Message);
        }
        finally
        {
            _busy = false;
        }
    }

    private static void Report(Exception e)
    {
        lock (Sites)
        {
            long now = Stopwatch.GetTimestamp();
            _budget = Math.Min(Burst, _budget + (now - _stamp) * PerSecond / Stopwatch.Frequency);
            _stamp = now;
            if (_budget < 1.0)
            {
                _skipped++;
                return;
            }
            _budget -= 1.0;
        }

        var st = new StackTrace(false);
        var at = new string[Depth];
        string callee = null;
        int found = 0;
        for (int i = 0; i < st.FrameCount && found < Depth; i++)
        {
            MethodBase m = st.GetFrame(i).GetMethod();
            Type t = m?.DeclaringType;
            if (t == null || t == Self)
                continue;
            string w = Where(m);
            if (t.Assembly != Mine)
            {
                if (found == 0)
                    callee = w;
            }
            else if (found == 0 || at[found - 1] != w)
                at[found++] = w;
        }
        if (found == 0) return;

        string type = e.GetType().Name;
        if (type == "FormatException" && callee == "Mp3FileReader..ctor") return;
        string key = type + "|" + callee + "|" + at[0];
        int n;
        int lost;
        lock (Sites)
        {
            Sites.TryGetValue(key, out n);
            if (n == 0 && Sites.Count >= MaxSites)
            {
                _skipped++;
                return;
            }
            Sites[key] = ++n;
            if (!Milestone(n))
                return;
            lost = _skipped;
            _skipped = 0;
        }

        var sb = new StringBuilder(256);
        sb.Append("[Err] ");
        if (n > 1)
            sb.Append('x').Append(n).Append(' ');
        sb.Append(type).Append(": ").Append(Brief(e)).Append(" | ");
        if (callee != null)
            sb.Append(callee).Append(" <- ");
        for (int i = 0; i < found; i++)
        {
            if (i > 0)
                sb.Append(" <- ");
            sb.Append(at[i]);
        }
        if (lost > 0)
            sb.Append(" (+").Append(lost).Append(" skipped)");
        NocturnePlugin.Logger.LogWarning(sb.ToString());
    }

    private static bool Milestone(int n)
    {
        while (n >= 10 && n % 10 == 0)
            n /= 10;
        return n == 1;
    }

    private static string Brief(Exception e)
    {
        string msg = e.Message;
        if (string.IsNullOrEmpty(msg))
            return string.Empty;

        var parts = new List<string>(2);
        foreach (string raw in msg.Split('\n'))
        {
            string l = raw.Trim();
            if (l.Length == 0 || l.StartsWith("---", StringComparison.Ordinal))
                continue;
            parts.Add(l);
            if (parts.Count == 2)
                break;
        }
        string s = string.Join(" @ ", parts);
        return s.Length > 200 ? s.Substring(0, 200) : s;
    }

    private static string Where(MethodBase m)
    {
        Type t = m.DeclaringType;
        string name = m.Name;
        if (name[0] == '<')
            name = Inner(name);
        while (t.DeclaringType != null && t.Name[0] == '<')
        {
            if (name == "MoveNext")
                name = Inner(t.Name);
            t = t.DeclaringType;
        }
        return t.Name + "." + name;
    }

    private static string Inner(string s)
    {
        int end = s.IndexOf('>');
        return end > 1 ? s.Substring(1, end - 1) : s;
    }
}
