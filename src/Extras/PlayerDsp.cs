using System;

namespace Nocturne;

[Flags]
internal enum PlayerFx
{
    None = 0,
    Voice = 1,
    Phones = 2,
}

internal sealed class PlayerChain
{
    internal const int Bands = 10;
    internal static readonly float[] Freqs = { 32f, 64f, 125f, 250f, 500f, 1000f, 2000f, 4000f, 8000f, 16000f };

    private const int Blk = 64;
    private const float WideHigh = 1.3f;
    private const float WideBass = 0.88f;
    private const float VoiceAmt = 0.26f;
    private const float BassAmt = 0.14f;
    private const float LcTreble = 0.05f;

    private readonly int _ch;
    private readonly int _rate;
    private readonly double[] _x1;
    private readonly double[] _x2;
    private readonly double[] _y1;
    private readonly double[] _y2;
    private readonly double[] _b0 = new double[Bands];
    private readonly double[] _b1 = new double[Bands];
    private readonly double[] _b2 = new double[Bands];
    private readonly double[] _a1 = new double[Bands];
    private readonly double[] _a2 = new double[Bands];
    private readonly int[] _act = new int[Bands];
    private readonly float[] _gNow = new float[Bands];
    private readonly float[] _gTo = new float[Bands];
    private readonly float[] _bass;
    private readonly float[] _lc;
    private readonly float[] _dcx;
    private readonly float[] _dcy;
    private readonly float _bassA;
    private readonly float _lcA;
    private readonly float _sideA;
    private readonly float _sideB;
    private readonly double _vb0;
    private readonly double _va1;
    private readonly double _va2;
    private readonly float _fadeStep;
    private readonly float _dcR;
    private readonly float _eqK;
    private readonly float _gainK;
    private readonly float _gainSlowK;
    private int _nAct;
    private int _mask;
    private int _left;
    private float _side;
    private float _sideCoef;
    private float _wideBass = WideBass;
    private double _vz1;
    private double _vz2;
    private float _voiceNow;
    private float _phonesNow;
    private float _bassNow;
    private float _bassTo;
    private float _fade = 1f;
    private float _gain = 1f;
    private volatile float _gainTo = 1f;
    private volatile float _lcGain;
    private volatile float _voiceTo;
    private volatile float _phonesTo;
    private volatile bool _slow;
    private volatile bool _clear;

    internal PlayerChain(int ch, int rate)
    {
        _ch = ch;
        _rate = rate;
        _x1 = new double[ch * Bands];
        _x2 = new double[ch * Bands];
        _y1 = new double[ch * Bands];
        _y2 = new double[ch * Bands];
        _bass = new float[ch];
        _lc = new float[ch];
        _dcx = new float[ch];
        _dcy = new float[ch];

        _bassA = Pole(140f, rate);
        _lcA = Pole(200f, rate);
        _sideA = Pole(250f, rate);
        _sideB = Pole(700f, rate);
        _sideCoef = _sideA;
        double w = 2.0 * Math.PI * 2800.0 / rate;
        double alpha = Math.Sin(w) / 1.8;
        _vb0 = alpha / (1.0 + alpha);
        _va1 = -2.0 * Math.Cos(w) / (1.0 + alpha);
        _va2 = (1.0 - alpha) / (1.0 + alpha);
        _fadeStep = 1f / (0.003f * rate);
        _dcR = MathF.Exp(-MathF.PI * 2f * 4f / rate);
        _eqK = 1f - MathF.Exp(-(float)Blk / (0.08f * rate));
        _gainK = 1f - MathF.Exp(-1f / (0.2f * rate));
        _gainSlowK = 1f - MathF.Exp(-1f / rate);
    }

    internal float GainTo
    {
        set
        {
            _slow = false;
            _gainTo = value;
        }
    }

    internal void Settle(float linear)
    {
        _slow = MathF.Abs(20f * MathF.Log10(Math.Max(linear, 0.0001f) / Math.Max(_gainTo, 0.0001f))) > 0.5f;
        _gainTo = linear;
    }

    internal void SetGain(float linear, bool now)
    {
        _gainTo = linear;
        if (now)
            _gain = linear;
    }

    internal void SetVolume(float v)
    {
        _lcGain = Math.Max(0f, (1f - v) * 0.22f);
    }

    internal void SetFx(PlayerFx fx, bool now)
    {
        _voiceTo = (fx & PlayerFx.Voice) != 0 ? 1f : 0f;
        _phonesTo = (fx & PlayerFx.Phones) != 0 ? 1f : 0f;
        if (!now)
            return;
        _voiceNow = _voiceTo;
        _phonesNow = _phonesTo;
        Blend();
    }

    internal void SetEq(float[] db, bool now)
    {
        float top = 0f;
        for (int b = 0; b < 3 && b < db.Length; b++)
            top = Math.Max(top, db[b]);
        _bassTo = BassAmt * (top > 3f ? Math.Max(0f, 1f - (top - 3f) / 9f) : 1f);

        Array.Copy(Compensate(db, _rate), _gTo, Bands);
        if (!now)
            return;
        Array.Copy(_gTo, _gNow, Bands);
        _bassNow = _bassTo;
        Coefs();
    }

    internal void Reset() => _clear = true;

    private void Wipe()
    {
        _clear = false;
        Array.Clear(_x1, 0, _x1.Length);
        Array.Clear(_x2, 0, _x2.Length);
        Array.Clear(_y1, 0, _y1.Length);
        Array.Clear(_y2, 0, _y2.Length);
        Array.Clear(_bass, 0, _ch);
        Array.Clear(_lc, 0, _ch);
        Array.Clear(_dcx, 0, _ch);
        Array.Clear(_dcy, 0, _ch);
        _side = 0f;
        _vz1 = 0.0;
        _vz2 = 0.0;
        _fade = 0f;
    }

    internal void Process(float[] src, int frame, int frames, float[] dst)
    {
        if (_clear)
            Wipe();
        int ch = _ch;
        int ip = frame * ch;
        int op = 0;
        for (int f = 0; f < frames; f++)
        {
            if (_left == 0)
                Tick();
            _left--;
            if (_gain != _gainTo)
            {
                float d = _gainTo - _gain;
                _gain = d > -0.00001f && d < 0.00001f ? _gainTo : _gain + d * (_slow ? _gainSlowK : _gainK);
            }
            float gain = _gain;
            if (_fade < 1f)
            {
                _fade = Math.Min(1f, _fade + _fadeStep);
                gain *= _fade;
            }

            if (ch == 2)
            {
                float l = src[ip];
                float r = src[ip + 1];
                ip += 2;
                Pair(ref l, ref r);
                dst[op] = l * gain;
                dst[op + 1] = r * gain;
                op += 2;
            }
            else
            {
                for (int c = 0; c < ch; c++)
                    dst[op++] = Lone(c, src[ip++]) * gain;
            }
        }
    }

    private void Tick()
    {
        _left = Blk;
        bool moved = false;
        for (int b = 0; b < Bands; b++)
        {
            float d = _gTo[b] - _gNow[b];
            if (d == 0f)
                continue;
            _gNow[b] = d > -0.004f && d < 0.004f ? _gTo[b] : _gNow[b] + d * _eqK;
            moved = true;
        }
        if (moved)
            Coefs();
        float e = _bassTo - _bassNow;
        _bassNow = e > -0.0002f && e < 0.0002f ? _bassTo : _bassNow + e * _eqK;

        float v = _voiceTo - _voiceNow;
        if (v != 0f)
            _voiceNow = v > -0.005f && v < 0.005f ? _voiceTo : _voiceNow + v * _eqK;
        if (_voiceNow == 0f)
        {
            _vz1 = 0.0;
            _vz2 = 0.0;
        }
        float p = _phonesTo - _phonesNow;
        if (p != 0f)
        {
            _phonesNow = p > -0.005f && p < 0.005f ? _phonesTo : _phonesNow + p * _eqK;
            Blend();
        }
    }

    private void Blend()
    {
        _sideCoef = _sideA + (_sideB - _sideA) * _phonesNow;
        _wideBass = WideBass + (0.42f - WideBass) * _phonesNow;
    }

    private void Coefs()
    {
        int n = 0;
        int mask = 0;
        for (int b = 0; b < Bands; b++)
        {
            if (MathF.Abs(_gNow[b]) < 0.01f || Freqs[b] >= 0.45f * _rate)
                continue;
            Design(b, _gNow[b], _rate, out _b0[b], out _b1[b], out _b2[b], out _a1[b], out _a2[b]);
            if ((_mask & (1 << b)) == 0)
            {
                for (int c = 0; c < _ch; c++)
                    _x1[c * Bands + b] = _x2[c * Bands + b] = _y1[c * Bands + b] = _y2[c * Bands + b] = 0.0;
            }
            mask |= 1 << b;
            _act[n++] = b;
        }
        _nAct = n;
        _mask = mask;
    }

    internal static void Design(int band, double db, double rate, out double b0, out double b1, out double b2, out double a1, out double a2)
    {
        double a = Math.Pow(10.0, db / 40.0);
        double w = Math.PI * 2.0 * Freqs[band] / rate;
        double sin = Math.Sin(w);
        double alpha = sin * Math.Sinh(Math.Log(2.0) * 0.5 * w / sin);
        double a0 = 1.0 + alpha / a;
        double cos = Math.Cos(w);
        b0 = (1.0 + alpha * a) / a0;
        b1 = -2.0 * cos / a0;
        b2 = (1.0 - alpha * a) / a0;
        a1 = -2.0 * cos / a0;
        a2 = (1.0 - alpha / a) / a0;
    }

    internal static double Response(float[] db, double hz, double rate)
    {
        float[] g = Compensate(db, (int)rate);
        double m = 1.0;
        for (int b = 0; b < Bands; b++)
        {
            if (Math.Abs(g[b]) < 0.01f || Freqs[b] >= 0.45 * rate)
                continue;
            m *= Single(b, g[b], hz, rate);
        }
        return m;
    }

    internal static void Curve(float[] db, double rate, double[] hz, float[] outDb)
    {
        float[] g = Compensate(db, (int)rate);
        var bq = new double[Bands * 5];
        int n = 0;
        for (int b = 0; b < Bands; b++)
        {
            if (Math.Abs(g[b]) < 0.01f || Freqs[b] >= 0.45 * rate)
                continue;
            Design(b, g[b], rate, out bq[n * 5], out bq[n * 5 + 1], out bq[n * 5 + 2], out bq[n * 5 + 3], out bq[n * 5 + 4]);
            n++;
        }
        for (int i = 0; i < hz.Length; i++)
        {
            double w = Math.PI * 2.0 * hz[i] / rate;
            double c1 = Math.Cos(w), s1 = Math.Sin(w), c2 = Math.Cos(2.0 * w), s2 = Math.Sin(2.0 * w);
            double m = 1.0;
            for (int k = 0; k < n; k++)
            {
                double nr = bq[k * 5] + bq[k * 5 + 1] * c1 + bq[k * 5 + 2] * c2;
                double ni = -(bq[k * 5 + 1] * s1 + bq[k * 5 + 2] * s2);
                double dr = 1.0 + bq[k * 5 + 3] * c1 + bq[k * 5 + 4] * c2;
                double di = -(bq[k * 5 + 3] * s1 + bq[k * 5 + 4] * s2);
                m *= (nr * nr + ni * ni) / (dr * dr + di * di);
            }
            outDb[i] = (float)(10.0 * Math.Log10(m));
        }
    }

    private static double Single(int band, double db, double hz, double rate)
    {
        double w = Math.PI * 2.0 * hz / rate;
        Design(band, db, rate, out double b0, out double b1, out double b2, out double a1, out double a2);
        double c1 = Math.Cos(w), s1 = Math.Sin(w), c2 = Math.Cos(2.0 * w), s2 = Math.Sin(2.0 * w);
        double nr = b0 + b1 * c1 + b2 * c2;
        double ni = -(b1 * s1 + b2 * s2);
        double dr = 1.0 + a1 * c1 + a2 * c2;
        double di = -(a1 * s1 + a2 * s2);
        return Math.Sqrt((nr * nr + ni * ni) / (dr * dr + di * di));
    }

    private static double[] _inv;
    private static int _invRate;
    private static readonly object InvGate = new object();

    internal static float[] Compensate(float[] db, int rate)
    {
        double[] inv;
        lock (InvGate)
        {
            if (_inv == null || _invRate != rate)
            {
                _inv = Invert(rate);
                _invRate = rate;
            }
            inv = _inv;
        }
        var g = new float[Bands];
        for (int i = 0; i < Bands; i++)
        {
            double s = 0.0;
            for (int j = 0; j < Bands && j < db.Length; j++)
                s += inv[i * Bands + j] * db[j];
            g[i] = (float)Math.Max(-18.0, Math.Min(18.0, s));
        }
        return g;
    }

    private static double[] Invert(int rate)
    {
        const double probe = 6.0;
        var m = new double[Bands * Bands * 2];
        int w = Bands * 2;
        for (int j = 0; j < Bands; j++)
        {
            bool live = Freqs[j] < 0.45 * rate;
            for (int i = 0; i < Bands; i++)
                m[i * w + j] = !live ? (i == j ? 1.0 : 0.0) : Freqs[i] >= 0.45 * rate ? 0.0 : 20.0 * Math.Log10(Single(j, probe, Freqs[i], rate)) / probe;
            m[j * w + Bands + j] = 1.0;
        }
        for (int c = 0; c < Bands; c++)
        {
            int top = c;
            for (int r = c + 1; r < Bands; r++)
                if (Math.Abs(m[r * w + c]) > Math.Abs(m[top * w + c]))
                    top = r;
            if (top != c)
                for (int k = 0; k < w; k++)
                    (m[c * w + k], m[top * w + k]) = (m[top * w + k], m[c * w + k]);
            double d = m[c * w + c];
            for (int k = 0; k < w; k++)
                m[c * w + k] /= d;
            for (int r = 0; r < Bands; r++)
            {
                if (r == c)
                    continue;
                double f = m[r * w + c];
                if (f == 0.0)
                    continue;
                for (int k = 0; k < w; k++)
                    m[r * w + k] -= f * m[c * w + k];
            }
        }
        var res = new double[Bands * Bands];
        for (int i = 0; i < Bands; i++)
            for (int j = 0; j < Bands; j++)
                res[i * Bands + j] = m[i * w + Bands + j];
        return res;
    }

    private double Eq(int c, double x)
    {
        int at = c * Bands;
        for (int k = 0; k < _nAct; k++)
        {
            int b = _act[k];
            int s = at + b;
            double y = _b0[b] * x + _b1[b] * _x1[s] + _b2[b] * _x2[s] - _a1[b] * _y1[s] - _a2[b] * _y2[s];
            _x2[s] = _x1[s];
            _x1[s] = x;
            _y2[s] = _y1[s];
            _y1[s] = y;
            x = y;
        }
        return x;
    }

    private void Pair(ref float l, ref float r)
    {
        float sl = l;
        float sr = r;
        if (_nAct > 0)
        {
            sl = (float)Eq(0, sl);
            sr = (float)Eq(1, sr);
        }

        float mid = (sl + sr) * 0.5f;
        float diff = (sl - sr) * 0.5f;
        if (_voiceNow > 0f)
            mid += Voice(mid) * _voiceNow * VoiceAmt;
        _side += _sideCoef * (diff - _side);
        float side = _side * _wideBass + (diff - _side) * WideHigh;
        sl = mid + side;
        sr = mid - side;

        _bass[0] += _bassA * (sl - _bass[0]);
        _bass[1] += _bassA * (sr - _bass[1]);
        sl += _bass[0] * MathF.Abs(_bass[0]) * _bassNow;
        sr += _bass[1] * MathF.Abs(_bass[1]) * _bassNow;

        float lc = _lcGain;
        if (lc > 0.005f)
        {
            _lc[0] += _lcA * (sl - _lc[0]);
            _lc[1] += _lcA * (sr - _lc[1]);
            sl += _lc[0] * lc + (sl - _lc[0]) * lc * LcTreble;
            sr += _lc[1] * lc + (sr - _lc[1]) * lc * LcTreble;
        }

        l = Dc(0, sl);
        r = Dc(1, sr);
    }

    private float Lone(int c, float x)
    {
        float s = _nAct > 0 ? (float)Eq(c, x) : x;
        if (_ch == 1 && _voiceNow > 0f)
            s += Voice(s) * _voiceNow * VoiceAmt;
        _bass[c] += _bassA * (s - _bass[c]);
        s += _bass[c] * MathF.Abs(_bass[c]) * _bassNow;
        return Dc(c, s);
    }

    private float Voice(float x)
    {
        double y = _vb0 * x + _vz1;
        _vz1 = -_va1 * y + _vz2;
        _vz2 = -_vb0 * x - _va2 * y;
        return (float)y;
    }

    private float Dc(int c, float x)
    {
        float y = x - _dcx[c] + _dcR * _dcy[c];
        _dcx[c] = x;
        _dcy[c] = y;
        return y;
    }

    private static float Pole(float hz, int rate) => 1f - MathF.Exp(-MathF.PI * 2f * hz / rate);
}

internal sealed class PlayerLimiter
{
    internal const float Ceil = 0.97f;

    private readonly int _ch;
    private readonly int _look;
    private readonly float _relFast;
    private readonly float _relSlow;
    private readonly float _avgK;
    private readonly float[] _delay;
    private readonly float[] _ring;
    private readonly float[] _qv;
    private readonly long[] _qi;
    private int _dp;
    private int _rp;
    private int _qh;
    private int _qn;
    private long _n;
    private double _sum;
    private float _prev = 1f;
    private float _avg;
    private volatile bool _clear;

    internal PlayerLimiter(int ch, int rate)
    {
        _ch = ch;
        _look = Math.Max(8, (int)(rate * 0.005f));
        _relFast = 1f - MathF.Exp(-1f / (0.03f * rate));
        _relSlow = 1f - MathF.Exp(-1f / (0.25f * rate));
        _avgK = 1f - MathF.Exp(-1f / (0.3f * rate));
        _delay = new float[(_look - 1) * ch];
        _ring = new float[_look];
        _qv = new float[_look];
        _qi = new long[_look];
        Wipe();
    }

    internal void Reset() => _clear = true;

    private void Wipe()
    {
        _clear = false;
        Array.Clear(_delay, 0, _delay.Length);
        Array.Fill(_ring, 1f);
        _sum = _look;
        _dp = 0;
        _rp = 0;
        _qh = 0;
        _qn = 0;
        _n = 0;
        _prev = 1f;
        _avg = 0f;
    }

    internal void Process(float[] buf, int frames)
    {
        if (_clear)
            Wipe();
        int ch = _ch;
        int look = _look;
        int hold = look - 1;
        int o = 0;
        for (int f = 0; f < frames; f++)
        {
            float peak = 0f;
            for (int c = 0; c < ch; c++)
            {
                float a = buf[o + c];
                if (a < 0f)
                    a = -a;
                if (a > peak)
                    peak = a;
            }
            float need = peak > Ceil ? Ceil / peak : 1f;

            while (_qn > 0 && _qi[_qh] <= _n - look)
            {
                _qh = (_qh + 1) % look;
                _qn--;
            }
            while (_qn > 0 && _qv[(_qh + _qn - 1) % look] >= need)
                _qn--;
            int t = (_qh + _qn) % look;
            _qv[t] = need;
            _qi[t] = _n++;
            _qn++;

            float m = _qv[_qh];
            _avg += (1f - _prev - _avg) * _avgK;
            float deep = _avg * 2.85f;
            float rel = deep >= 1f ? _relSlow : _relFast + (_relSlow - _relFast) * deep;
            float up = _prev + (1f - _prev) * rel;
            if (m < up)
                up = m;
            _prev = up;
            _sum += up - _ring[_rp];
            _ring[_rp] = up;
            _rp = (_rp + 1) % look;
            float g = (float)(_sum / look);

            int at = _dp * ch;
            for (int c = 0; c < ch; c++)
            {
                float held = _delay[at + c];
                _delay[at + c] = buf[o + c];
                float y = held * g;
                buf[o + c] = y > 1f ? 1f : y < -1f ? -1f : y;
            }
            _dp = (_dp + 1) % hold;
            o += ch;
        }
    }
}
