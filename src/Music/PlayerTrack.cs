using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using NVorbis;

namespace Nocturne;

internal sealed class PlayerTrack
{
    private const int Lead = 3;

    private static readonly List<float[]> Spare = new List<float[]>();

    internal float[] Samples;
    internal int Channels;
    internal int Frequency;
    internal int Total;
    internal volatile int Ready;
    internal volatile bool Started;
    internal volatile bool Done;
    internal volatile bool Cancelled;
    internal volatile bool Finished;
    internal volatile bool Pinned;
    internal string Error;
    internal PlayerLoud Loud;
    internal float Gain = 1f;
    internal int Key;
    internal string Info = string.Empty;
    internal bool Retired;
    internal float ReleaseAt;

    internal bool Failed => Error != null;

    internal static PlayerTrack Begin(string path, int rate, Action<PlayerTrack, bool> measure)
    {
        var t = new PlayerTrack();
        new Thread(() => t.Run(path, rate, measure))
        {
            IsBackground = true,
            Priority = ThreadPriority.BelowNormal,
            Name = "Nocturne decode",
        }.Start();
        return t;
    }

    internal void Give()
    {
        float[] a = Samples;
        Samples = null;
        Loud = null;
        if (a == null)
            return;
        lock (Spare)
        {
            Spare.Add(a);
            if (Spare.Count > 2)
                Spare.RemoveAt(0);
        }
    }

    private void Run(string path, int rate, Action<PlayerTrack, bool> measure)
    {
        IDisposable source = null;
        try
        {
            ISampleProvider provider;
            double seconds;
            int srcRate;
            if (path.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase))
            {
                var v = new VorbisReader(path);
                source = v;
                provider = new VorbisProvider(v);
                seconds = v.TotalTime.TotalSeconds;
                srcRate = v.SampleRate;
                Channels = v.Channels;
            }
            else
            {
                WaveStream w = path.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) ? new Mp3FileReader(path) : new WaveFileReader(path);
                source = w;
                provider = w.ToSampleProvider();
                seconds = w.TotalTime.TotalSeconds;
                srcRate = w.WaveFormat.SampleRate;
                Channels = w.WaveFormat.Channels;
            }
            if (seconds > 720)
                throw new InvalidOperationException($"Track too long ({TimeSpan.FromSeconds(seconds):mm\\:ss}), max 12 min");
            if (seconds <= 0 || Channels < 1)
                throw new InvalidOperationException("Empty track");
            if (srcRate != rate)
                provider = new WdlResamplingSampleProvider(provider, rate);

            Frequency = rate;
            Total = (int)Math.Ceiling(seconds * rate);
            int limit = (Total + Total / 50 + 8192) * Channels;
            Samples = Rent(limit);

            int at = 0;
            int step = Channels * rate / 10;
            var chunk = new float[step];
            while (!Cancelled)
            {
                int got = provider.Read(chunk, 0, Math.Min(step, limit - at));
                if (got <= 0)
                    break;
                Array.Copy(chunk, 0, Samples, at, got);
                at += got;
                Ready = at / Channels;
                if (!Started && Ready >= rate * Lead)
                {
                    measure?.Invoke(this, false);
                    Started = true;
                }
            }
            if (Cancelled)
                return;
            Done = true;
            measure?.Invoke(this, true);
        }
        catch (Exception e)
        {
            Error = e.Message;
        }
        finally
        {
            source?.Dispose();
            Started = true;
            Finished = true;
        }
    }

    private static float[] Rent(int length)
    {
        length = (length + 65535) & ~65535;
        lock (Spare)
        {
            int best = -1;
            for (int i = 0; i < Spare.Count; i++)
            {
                int n = Spare[i].Length;
                if (n >= length && n <= length + length / 2 && (best < 0 || n < Spare[best].Length))
                    best = i;
            }
            if (best >= 0)
            {
                float[] a = Spare[best];
                Spare.RemoveAt(best);
                return a;
            }
        }
        return GC.AllocateUninitializedArray<float>(length);
    }

    private sealed class VorbisProvider : ISampleProvider
    {
        private readonly VorbisReader _reader;

        public WaveFormat WaveFormat { get; }

        public VorbisProvider(VorbisReader reader)
        {
            _reader = reader;
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(reader.SampleRate, reader.Channels);
        }

        public int Read(float[] buffer, int offset, int count) => _reader.ReadSamples(buffer, offset, count);
    }
}
