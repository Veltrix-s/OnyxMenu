using System;
using System.Collections.Generic;

namespace Nocturne;

internal sealed class PlayerMixer
{
    private const float Seconds = 2.5f;
    private const float Hurry = 0.15f;
    private const int MaxVoices = 4;

    private sealed class Voice
    {
        internal PlayerTrack Track;
        internal PlayerChain Chain;
        internal int Pos;
        internal float Level;
        internal int Done;
        internal int Span;
    }

    private readonly List<Voice> _voices = new List<Voice>();
    private float[] _tmp = new float[0];
    private volatile int _count;
    private volatile int _inDone;
    private volatile int _inSpan;

    internal bool Busy => _count > 0;

    internal static int Plan(PlayerTrack old, int at)
    {
        int left = Math.Min(old.Total, old.Ready) - at;
        int span = Math.Min((int)(old.Frequency * Seconds), left);
        return span >= old.Frequency / 12 ? span : 0;
    }

    internal void Enter(PlayerTrack old, PlayerChain chain, int at, int span)
    {
        float level = Rising();
        old.Pinned = true;
        lock (_voices)
        {
            if (_voices.Count >= MaxVoices)
                Hasten(_voices[0], old.Frequency);
            _voices.Add(new Voice { Track = old, Chain = chain, Pos = at, Level = level, Span = span });
            _count = _voices.Count;
        }
        _inSpan = 0;
        _inDone = 0;
        _inSpan = span;
    }

    internal void Begin() => _inSpan = 0;

    internal void Clear()
    {
        _inSpan = 0;
        lock (_voices)
        {
            for (int i = 0; i < _voices.Count; i++)
                _voices[i].Track.Pinned = false;
            _voices.Clear();
            _count = 0;
        }
    }

    internal void SetEq(float[] eq)
    {
        lock (_voices)
        {
            for (int i = 0; i < _voices.Count; i++)
                _voices[i].Chain.SetEq(eq, false);
        }
    }

    internal void SetFx(PlayerFx fx)
    {
        lock (_voices)
        {
            for (int i = 0; i < _voices.Count; i++)
                _voices[i].Chain.SetFx(fx, false);
        }
    }

    internal void SetVolume(float volume)
    {
        lock (_voices)
        {
            for (int i = 0; i < _voices.Count; i++)
                _voices[i].Chain.SetVolume(volume);
        }
    }

    internal void Mix(float[] mix, int n, int frames, int ch)
    {
        int span = _inSpan;
        int at = _inDone;
        if (at < span)
        {
            for (int f = 0; f < n; f++)
            {
                float g = MathF.Sin(Arc(at + f, span));
                int o = f * ch;
                for (int c = 0; c < ch; c++)
                    mix[o + c] *= g;
            }
            _inDone = at + frames;
        }
        if (_count == 0)
            return;

        lock (_voices)
        {
            if (_tmp.Length < frames * ch)
                _tmp = new float[frames * ch];
            for (int i = _voices.Count - 1; i >= 0; i--)
            {
                Voice v = _voices[i];
                PlayerTrack t = v.Track;
                float[] src = t.Samples;
                int nb = src == null ? 0 : Math.Min(frames, Math.Max(0, t.Ready - v.Pos));
                if (nb > 0)
                {
                    v.Chain.Process(src, v.Pos, nb, _tmp);
                    for (int f = 0; f < nb; f++)
                    {
                        float w = v.Level * MathF.Cos(Arc(v.Done + f, v.Span));
                        int o = f * ch;
                        for (int c = 0; c < ch; c++)
                            mix[o + c] += _tmp[o + c] * w;
                    }
                    v.Pos += nb;
                }
                v.Done += frames;
                if (src == null || v.Done >= v.Span || (t.Done && v.Pos >= t.Ready))
                {
                    t.Pinned = false;
                    _voices.RemoveAt(i);
                }
            }
            _count = _voices.Count;
        }
    }

    private float Rising()
    {
        int span = _inSpan;
        int done = _inDone;
        return span > 0 && done < span ? MathF.Sin(Arc(done, span)) : 1f;
    }

    private static void Hasten(Voice v, int rate)
    {
        int quick = (int)(rate * Hurry);
        if (v.Span - v.Done <= quick)
            return;
        v.Level *= MathF.Cos(Arc(v.Done, v.Span));
        v.Done = 0;
        v.Span = quick;
    }

    private static float Arc(int at, int span)
    {
        float t = at >= span ? 1f : (float)at / span;
        return t * t * (3f - 2f * t) * 1.5707964f;
    }
}
