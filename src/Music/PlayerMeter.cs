using System;

namespace Nocturne;

internal sealed class PlayerMeter
{
    private const int Win = 1024;
    private const int Warm = 256;

    internal float Bass;
    internal float Mean;

    private float _clock;
    private float _top = 0.05f;
    private float _bass;
    private float _all;

    internal void Step(float[] pcm, int ch, int rate, int frame, float dt, bool live)
    {
        live = live && pcm != null && ch > 0 && rate > 0;
        _clock = Math.Min(_clock + dt, 0.1f);
        if (live && _clock >= 1f / 60f)
        {
            _clock = 0f;
            Measure(pcm, ch, rate, frame);
        }

        float b = live ? _bass : 0f;
        float m = live ? _all : 0f;
        Bass += (b - Bass) * (1f - MathF.Exp(-dt * (b > Bass ? 40f : 9f)));
        Mean += (m - Mean) * (1f - MathF.Exp(-dt * (m > Mean ? 20f : 5f)));
    }

    private void Measure(float[] pcm, int ch, int rate, int frame)
    {
        int total = pcm.Length / ch;
        float a = 1f - MathF.Exp(-MathF.PI * 2f * 150f / rate);
        float inv = 1f / ch;
        float lp = 0f;
        float all = 0f;
        float low = 0f;
        for (int i = -Warm; i < Win; i++)
        {
            int f = frame - Win / 2 + i;
            float m = 0f;
            if (f >= 0 && f < total)
            {
                int o = f * ch;
                for (int c = 0; c < ch; c++)
                    m += pcm[o + c];
                m *= inv;
            }
            lp += a * (m - lp);
            if (i >= 0)
            {
                all += m * m;
                low += lp * lp;
            }
        }

        float rms = MathF.Sqrt(all / Win);
        _top = Math.Max(rms, _top * 0.999f);
        float scale = 1f / Math.Max(0.02f, _top);
        _all = Ui.Clamp01(rms * scale);
        _bass = Ui.Clamp01(MathF.Sqrt(low / Win) * scale);
    }
}
