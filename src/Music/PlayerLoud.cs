using System;
using System.Collections.Generic;

namespace Nocturne;

internal sealed class PlayerLoud
{
    internal struct Result
    {
        internal float Lufs;
        internal float Peak;
    }

    private static readonly (float Target, float Floor, float Budget)[] Levels =
    {
        (-13f, -24f, 3f),
        (-8.5f, 0f, 5f),
        (-7f, 2f, 7f),
    };

    private const int Subs = 25;
    private const int MaxSegs = 6;

    private static readonly object Gate = new object();
    private static float[] _scratch = new float[0];

    private readonly float[] _s;
    private readonly int _ch;
    private readonly int _rate;
    private readonly int _warm;
    private readonly int _sub;
    private readonly int[] _start;
    private readonly int[] _len;

    internal PlayerLoud(float[] samples, int frames, int ch, int rate)
    {
        _s = samples;
        _ch = ch;
        _rate = rate;
        _sub = Math.Max(1, rate / 10);
        _warm = (int)(rate * 0.4f);

        int total = frames;
        int seg = Subs * _sub;
        if (total < seg * 3)
        {
            int whole = total / _sub * _sub;
            _start = new[] { 0 };
            _len = new[] { whole };
            return;
        }

        int n = Math.Min(MaxSegs, Math.Max(3, (int)MathF.Round(total / (20f * rate))));
        _start = new int[n];
        _len = new int[n];
        int lo = (int)(total * 0.08f);
        int span = (int)(total * 0.84f) - seg;
        for (int i = 0; i < n; i++)
        {
            _start[i] = lo + (n > 1 ? (int)((long)span * i / (n - 1)) : 0);
            _len[i] = seg;
        }
    }

    internal static int Key(float[] eq, int level, PlayerFx fx)
    {
        int h = level + 1 + (int)fx * 8191;
        for (int i = 0; i < eq.Length; i++)
            h = h * 31 + (int)MathF.Round(eq[i] * 10f);
        return h;
    }

    internal Result Measure(float[] eq, PlayerFx fx)
    {
        lock (Gate)
            return Run(eq, fx);
    }

    private Result Run(float[] eq, PlayerFx fx)
    {
        int ch = _ch;
        var z = new List<double>();
        float peak = 0f;

        int most = 0;
        for (int i = 0; i < _len.Length; i++)
            most = Math.Max(most, _len[i]);
        int need = (_warm + most) * ch;
        if (_scratch.Length < need)
            _scratch = new float[need];
        var buf = _scratch;
        var k = new KFilter(ch, _rate);

        for (int i = 0; i < _start.Length; i++)
        {
            int len = _len[i];
            int blocks = len / _sub;
            if (blocks < 4)
                continue;

            int from = Math.Max(0, _start[i] - _warm);
            int warm = _start[i] - from;
            var chain = new PlayerChain(ch, _rate);
            chain.SetEq(eq, true);
            chain.SetFx(fx, true);
            chain.Process(_s, from, warm + len, buf);
            k.Reset();

            var ss = new double[blocks * ch];
            for (int f = 0; f < warm + len; f++)
            {
                int at = f * ch;
                for (int c = 0; c < ch; c++)
                {
                    float x = buf[at + c];
                    double y = k.Run(c, x);
                    if (f < warm)
                        continue;
                    float a = x < 0f ? -x : x;
                    if (a > peak)
                        peak = a;
                    ss[(f - warm) / _sub * ch + c] += y * y;
                }
            }

            for (int j = 0; j + 3 < blocks; j++)
            {
                double sum = 0.0;
                for (int c = 0; c < ch; c++)
                    sum += (ss[j * ch + c] + ss[(j + 1) * ch + c] + ss[(j + 2) * ch + c] + ss[(j + 3) * ch + c]) / (4.0 * _sub);
                z.Add(ch == 1 ? sum * 2.0 : sum);
            }
        }

        var res = new Result { Lufs = -70f, Peak = peak };
        double gate = Math.Pow(10.0, (-70.0 + 0.691) / 10.0);
        double all = 0.0;
        int n = 0;
        for (int i = 0; i < z.Count; i++)
        {
            if (z[i] > gate)
            {
                all += z[i];
                n++;
            }
        }
        if (n == 0)
            return res;

        double rel = all / n * 0.1;
        double kept = 0.0;
        int m = 0;
        for (int i = 0; i < z.Count; i++)
        {
            if (z[i] > gate && z[i] > rel)
            {
                kept += z[i];
                m++;
            }
        }
        if (m > 0)
            res.Lufs = (float)(-0.691 + 10.0 * Math.Log10(kept / m));
        return res;
    }

    internal static float Gain(Result r, int level)
    {
        if (r.Lufs <= -69f)
            return 1f;
        var lv = Levels[level];
        float peakDb = 20f * MathF.Log10(Math.Max(r.Peak, 0.00001f));
        float room = 20f * MathF.Log10(PlayerLimiter.Ceil) - peakDb;
        float floor = lv.Floor > 0f ? Math.Min(lv.Floor, Math.Max(0f, -4.5f - r.Lufs)) : lv.Floor;
        float db = Math.Max(floor, Math.Min(lv.Target - r.Lufs, room + lv.Budget));
        return MathF.Pow(10f, Math.Min(12f, db) / 20f);
    }

    private sealed class KFilter
    {
        private readonly int _ch;
        private readonly double[] _sb = new double[5];
        private readonly double[] _rb = new double[5];
        private readonly double[] _st;
        private readonly double[] _rt;

        internal KFilter(int ch, int rate)
        {
            _ch = ch;
            _st = new double[ch * 4];
            _rt = new double[ch * 4];

            double k = Math.Tan(Math.PI * 1681.974450955533 / rate);
            double q = 0.7071752369554196;
            double vh = Math.Pow(10.0, 3.999843853973347 / 20.0);
            double vb = Math.Pow(vh, 0.4996667741545416);
            double a0 = 1.0 + k / q + k * k;
            _sb[0] = (vh + vb * k / q + k * k) / a0;
            _sb[1] = 2.0 * (k * k - vh) / a0;
            _sb[2] = (vh - vb * k / q + k * k) / a0;
            _sb[3] = 2.0 * (k * k - 1.0) / a0;
            _sb[4] = (1.0 - k / q + k * k) / a0;

            k = Math.Tan(Math.PI * 38.13547087602444 / rate);
            q = 0.5003270373238773;
            a0 = 1.0 + k / q + k * k;
            _rb[0] = 1.0;
            _rb[1] = -2.0;
            _rb[2] = 1.0;
            _rb[3] = 2.0 * (k * k - 1.0) / a0;
            _rb[4] = (1.0 - k / q + k * k) / a0;
        }

        internal void Reset()
        {
            Array.Clear(_st, 0, _st.Length);
            Array.Clear(_rt, 0, _rt.Length);
        }

        internal double Run(int c, double x)
        {
            int i = c * 4;
            double y = _sb[0] * x + _sb[1] * _st[i] + _sb[2] * _st[i + 1] - _sb[3] * _st[i + 2] - _sb[4] * _st[i + 3];
            _st[i + 1] = _st[i];
            _st[i] = x;
            _st[i + 3] = _st[i + 2];
            _st[i + 2] = y;

            double z = _rb[0] * y + _rb[1] * _rt[i] + _rb[2] * _rt[i + 1] - _rb[3] * _rt[i + 2] - _rb[4] * _rt[i + 3];
            _rt[i + 1] = _rt[i];
            _rt[i] = y;
            _rt[i + 3] = _rt[i + 2];
            _rt[i + 2] = z;
            return z;
        }
    }
}
