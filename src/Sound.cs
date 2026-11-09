using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

namespace Obsidot
{
    // Every sound is synthesized at startup: glass partials, shutter clicks, filtered noise. No asset files.
    static class Snd
    {
        const int SR = 44100;
        static readonly Random rnd = new Random(11);
        static readonly int P = IntPtr.Size, HDR = IntPtr.Size == 8 ? 48 : 32, FLAGS = IntPtr.Size == 8 ? 24 : 16;

        public static void Init()
        {
            float[] b;
            b = B(1.8); Glass(b, 1318.5, .15, 3.0, 0); Glass(b, 1975.5, .08, 3.6, .07); Add("chime", b);
            b = B(.8); Glass(b, 987.8, .10, 6, 0); Noise(b, .05, 12, .08, 0, .3); Add("close", b);
            b = B(.45); Buzz(b, .38, 0, .085); Buzz(b, .3, .14, .07); Add("buzz", b);    // a phone vibrating on a desk: two pulses
            b = B(.12); Buzz(b, .26, 0, .032); Noise(b, .12, 400, .9, 0, .004); Add("haptic", b); // one detent of the bezel
            b = B(.6); Glass(b, 2349, .07, 18, 0); Glass(b, 3136, .06, 20, .075); Add("fact", b);          // a crisp double tick
            b = B(1.8); double[] spark = { 1568, 1760, 2093, 2637, 3136, 3520 };
            for (int i = 0; i < spark.Length; i++) Glass(b, spark[i], .05, 3.5, i * .05); Add("magic", b); // a rising shimmer
            b = B(.06); Noise(b, .22, 400, .9, 0, .02); Sine(b, 3800, .06, 160, 0); Add("tick", b);
            b = B(.4); Sine(b, 2637, .06, 16, 0); Sine(b, 3520, .045, 20, .045); Add("open", b);
            b = B(2.4); Glass(b, 523.25, .09, 1.6, 0); Glass(b, 783.99, .07, 1.8, .13); Glass(b, 1046.5, .06, 2.0, .26); Glass(b, 1567.98, .045, 2.4, .40); Add("birth", b);
            b = B(.6); Sweep(b, .2, .6); Add("whoosh", b);
            b = B(.4); Noise(b, .5, 180, .7, 0, .03); Sine(b, 180, .22, 40, 0); Noise(b, .42, 160, .6, .075, .04); Sine(b, 140, .18, 30, .075); Add("shutter", b);
            b = B(.13); Chirp(b, 1700, 360, .09, .09, 0, true); LP(b, .3); Add("pew", b);
            b = B(.13); Chirp(b, 900, 280, .1, .08, 0, false); Chirp(b, 1900, 2500, .08, .025, 0, true); Noise(b, .05, 40, .5, 0, .1); LP(b, .45); Add("zap", b);
            b = B(.8); Rise(b, 200, 1250, .75, .08); Add("charge", b);
            b = B(1.4); Sine(b, 55, .5, 4, 0); Chirp(b, 150, 38, .45, .35, 0, false); Noise(b, .42, 8, .15, 0, .7); Glass(b, 2093, .05, 5, .01); Glass(b, 3136, .035, 6, .02); Add("rail", b);
            b = B(.25); Noise(b, .1, 30, .3, 0, .2); Add("fizzle", b);
            for (int i = 0; i < 3; i++) { b = B(.25); Noise(b, .32, 90, .85, 0, .05); Glass(b, 2400 + rnd.Next(1600), .045, 28, 0); Add("crack" + i, b); }
            b = B(1.1); Noise(b, .36, 10, .5, 0, .5); Sine(b, 90, .22, 10, 0); for (int i = 0; i < 10; i++) Glass(b, 1800 + rnd.NextDouble() * 3500, .045, 14 + rnd.NextDouble() * 10, rnd.NextDouble() * .18); Add("shatter", b);
            b = B(.8); Chirp(b, 110, 42, .38, .6, 0, false); Noise(b, .3, 14, .06, 0, .4); Noise(b, .18, 40, .1, .2, .1); Noise(b, .12, 40, .1, .34, .08); Add("thud", b);
            b = B(.7); for (int i = 0; i < 20; i++) Noise(b, .16, 120, .6, rnd.NextDouble() * .55, .025); Add("paper", b);
            b = B(.5); double[] ar = { 880, 1175, 1397, 1760, 2349 }; for (int i = 0; i < 5; i++) Chirp(b, ar[i], ar[i], .05, .06, i * .045, true); Noise(b, .1, 30, .5, 0, .1); LP(b, .5); Add("pixels", b);
            b = B(1.5); Chirp(b, 95, 35, .5, .6, 0, false); Noise(b, .42, 6, .3, 0, .8); for (int i = 0; i < 16; i++) Noise(b, .14, 80, .5, .05 + rnd.NextDouble() * .7, .03); for (int i = 0; i < 8; i++) Glass(b, 1500 + rnd.NextDouble() * 3000, .035, 12, rnd.NextDouble() * .4); Add("bin", b);
            b = B(.3); Glass(b, 1760, .07, 18, 0); Add("bounce", b);
            b = B(.3); Glass(b, 1760, .028, 22, 0); Add("bounce2", b);
            b = B(.35); Glass(b, 660, .09, 14, 0); Noise(b, .05, 200, .7, 0, .02); Add("rim", b);
            double[] pent = { 1046.5, 1174.7, 1318.5, 1568, 1760, 2093 };
            for (int i = 0; i < pent.Length; i++) { b = B(1.6); Glass(b, pent[i], .11, 2.8, 0); Glass(b, pent[i] * 1.5, .045, 3.5, .06); Add("score" + i, b); }
            b = B(.5); Sweep(b, .12, .4); Add("swish", b);
            b = B(3.0); for (int i = 0; i < 6; i++) Glass(b, pent[i] * 0.5, .07, 1.8, i * .11); Add("clear", b);
            b = B(.6); Glass(b, 2637, .035, 7, 0); Add("reborn", b);
            b = B(2.2); Rise(b, 38, 120, 2.1, .5); Noise(b, .5, .4, .015, 0, 2.2); LP(b, .25); Add("hole", b);              // the singularity winding up
            b = B(2.0); Chirp(b, 80, 24, 1.1, .9, 0, false); Sine(b, 42, .5, 2.6, .02); Noise(b, .5, 3, .05, 0, 1.6);
            Noise(b, .3, 30, .5, 0, .12); for (int i = 0; i < 8; i++) Glass(b, 900 + rnd.NextDouble() * 2400, .03, 5, .05 + rnd.NextDouble() * .3); Add("collapse", b);
            b = B(.35); Chirp(b, 300, 900, .16, .22, 0, false); Glass(b, 1760, .035, 12, .1); Add("boing", b);                        // the pet hops
            b = B(.4); Chirp(b, 700, 1400, .07, .12, 0, false); Chirp(b, 1000, 1900, .08, .1, .1, false); Add("chirp", b);             // the pet wakes, glad
            b = B(1.6); Glass(b, 1046.5, .1, 2.6, 0); Glass(b, 1568, .08, 2.8, .16); Glass(b, 2093, .07, 3, .32); Add("done", b);       // the focus timer ends
        }

        // One persistent output stream + a tiny software mixer on its own thread.
        // The game loop never touches the audio device; when nothing is playing the thread sleeps on an event.
        static readonly object gate = new object();
        static readonly List<int[]> voices = new List<int[]>(); // {sound id, position}
        static readonly List<short[]> clips = new List<short[]>();
        static readonly Dictionary<string, int> ids = new Dictionary<string, int>();
        static readonly AutoResetEvent signal = new AutoResetEvent(false);
        static Thread mixer;
        const int CH = 441, NB = 4; // 4 x 10 ms buffers

        public static void Play(string name)
        {
            if (!Settings.Sound) return;
            int id;
            if (!ids.TryGetValue(name, out id) && !ids.TryGetValue(name + rnd.Next(3), out id)) return;
            lock (gate) { if (voices.Count < 24) voices.Add(new[] { id, 0 }); }
            if (mixer == null) { mixer = new Thread(Mix) { IsBackground = true, Priority = ThreadPriority.AboveNormal, Name = "obsidot-audio" }; mixer.Start(); }
            signal.Set();
        }

        public static void Stop(string name)
        {
            int id; if (!ids.TryGetValue(name, out id)) return;
            lock (gate) voices.RemoveAll(v => v[0] == id);
        }

        static void Mix()
        {
            var fmt = new WAVEFORMATEX { wFormatTag = 1, nChannels = 1, nSamplesPerSec = SR, nAvgBytesPerSec = SR * 2, nBlockAlign = 2, wBitsPerSample = 16 };
            IntPtr h;
            if (waveOutOpen(out h, -1, ref fmt, signal.SafeWaitHandle.DangerousGetHandle(), IntPtr.Zero, 0x50000 /*CALLBACK_EVENT*/) != 0) return;
            var hdr = new IntPtr[NB]; var busy = new bool[NB];
            for (int i = 0; i < NB; i++)
            {
                hdr[i] = Marshal.AllocHGlobal(HDR);
                for (int k = 0; k < HDR; k += 4) Marshal.WriteInt32(hdr[i], k, 0);
                Marshal.WriteIntPtr(hdr[i], 0, Marshal.AllocHGlobal(CH * 2));
                Marshal.WriteInt32(hdr[i], P, CH * 2);
                waveOutPrepareHeader(h, hdr[i], HDR);
            }
            var acc = new int[CH]; var pcm = new short[CH];
            while (true)
            {
                for (int i = 0; i < NB; i++) if (busy[i] && (Marshal.ReadInt32(hdr[i], FLAGS) & 1) != 0) busy[i] = false;
                bool any;
                lock (gate) any = voices.Count > 0;
                if (any)
                    for (int i = 0; i < NB; i++)
                    {
                        if (busy[i]) continue;
                        Array.Clear(acc, 0, CH);
                        lock (gate)
                            for (int v = voices.Count - 1; v >= 0; v--)
                            {
                                var c = clips[voices[v][0]]; int pos = voices[v][1], n = Math.Min(CH, c.Length - pos);
                                for (int k = 0; k < n; k++) acc[k] += c[pos + k];
                                voices[v][1] = pos + n;
                                if (pos + n >= c.Length) voices.RemoveAt(v);
                            }
                        for (int k = 0; k < CH; k++) { int x = acc[k] * 3 / 4; pcm[k] = (short)(x > 32767 ? 32767 : x < -32768 ? -32768 : x); }
                        Marshal.Copy(pcm, 0, Marshal.ReadIntPtr(hdr[i], 0), CH);
                        waveOutWrite(h, hdr[i], HDR); busy[i] = true;
                    }
                signal.WaitOne(any ? 30 : Timeout.Infinite);
            }
        }
        // ---- synthesis ----
        static float[] B(double s) { return new float[(int)(s * SR)]; }

        static void Sine(float[] b, double f, double amp, double decay, double at)
        {
            int s0 = (int)(at * SR);
            for (int i = s0; i < b.Length; i++)
            {
                double t = (double)(i - s0) / SR, env = Math.Exp(-t * decay) * Math.Min(1, t / 0.002);
                if (env < 1e-4 && t > 0.01) break;
                b[i] += (float)(amp * env * Math.Sin(6.283185307 * f * t));
            }
        }

        static void Glass(float[] b, double f, double amp, double decay, double at)
        {
            Sine(b, f, amp, decay, at); Sine(b, f * 2.756, amp * .45, decay * 1.6, at);
            Sine(b, f * 5.404, amp * .2, decay * 2.4, at); Sine(b, f * 8.93, amp * .08, decay * 3.5, at);
        }

        static void Noise(float[] b, double amp, double decay, double lp, double at, double dur)
        {
            int s0 = (int)(at * SR), n = (int)(dur * SR); double y = 0;
            for (int i = 0; i < n && s0 + i < b.Length; i++)
            {
                double t = (double)i / SR; y += lp * (rnd.NextDouble() * 2 - 1 - y);
                b[s0 + i] += (float)(amp * y * Math.Exp(-t * decay) * Math.Min(1, t / 0.001));
            }
        }

        static void Chirp(float[] b, double f0, double f1, double dur, double amp, double at, bool square)
        {
            int s0 = (int)(at * SR), n = (int)(dur * SR); double ph = 0;
            for (int i = 0; i < n && s0 + i < b.Length; i++)
            {
                double t = (double)i / n, f = f0 * Math.Pow(f1 / f0, t); ph += f / SR;
                double v = square ? (ph % 1 < .5 ? 1 : -1) : Math.Sin(6.283185307 * ph);
                b[s0 + i] += (float)(amp * v * (1 - t) * (1 - t) * Math.Min(1, i / (0.002 * SR)));
            }
        }

        static void Rise(float[] b, double f0, double f1, double dur, double amp)
        {
            int n = (int)(dur * SR); double ph = 0;
            for (int i = 0; i < n && i < b.Length; i++)
            {
                double t = (double)i / n; ph += (f0 * Math.Pow(f1 / f0, t)) / SR;
                b[i] += (float)(amp * t * t * (1 + .3 * Math.Sin(t * 90)) * (Math.Sin(6.283185307 * ph) + .3 * Math.Sin(12.56637 * ph)));
            }
        }

        static void Sweep(float[] b, double amp, double dur)
        {
            int n = Math.Min(b.Length, (int)(dur * SR)); double y = 0;
            for (int i = 0; i < n; i++)
            {
                double t = (double)i / n, env = Math.Sin(Math.PI * t), lp = .01 + .18 * env;
                y += lp * (rnd.NextDouble() * 2 - 1 - y);
                b[i] += (float)(amp * y * env * env * 3);
            }
        }

        // a motor buzz: a low fundamental with its harmonics, rattled at ~26 Hz, quick attack, short tail
        static void Buzz(float[] b, double amp, double at, double dur)
        {
            int s0 = (int)(at * SR), n = (int)((dur + .03) * SR);
            for (int i = 0; i < n && s0 + i < b.Length; i++)
            {
                double t = (double)i / SR, env = Math.Min(1, t / .004) * (t < dur ? 1 : Math.Exp(-(t - dur) * 120));
                double rattle = .65 + .35 * Math.Sin(6.283185307 * 26 * t);
                double v = Math.Sin(6.283185307 * 155 * t) + .5 * Math.Sin(6.283185307 * 310 * t + .3) + .25 * Math.Sin(6.283185307 * 465 * t + .9);
                b[s0 + i] += (float)(amp * env * rattle * v * .6);
            }
        }

        static void LP(float[] b, double a) { double y = 0; for (int i = 0; i < b.Length; i++) { y += a * (b[i] - y); b[i] = (float)y; } }

        static void Add(string name, float[] b)
        {
            var s = new short[b.Length];
            for (int i = 0; i < b.Length; i++)
            {
                double fade = Math.Min(1, (b.Length - i) / (0.01 * SR)); // no click at the tail
                s[i] = (short)(Math.Tanh(b[i] * 1.1) * fade * 32000);
            }
            ids[name] = clips.Count; clips.Add(s);
        }

        [StructLayout(LayoutKind.Sequential, Pack = 2)]
        struct WAVEFORMATEX { public ushort wFormatTag, nChannels; public int nSamplesPerSec, nAvgBytesPerSec; public ushort nBlockAlign, wBitsPerSample, cbSize; }
        [DllImport("winmm.dll")] static extern int waveOutOpen(out IntPtr h, int dev, ref WAVEFORMATEX f, IntPtr cb, IntPtr inst, int flags);
        [DllImport("winmm.dll")] static extern int waveOutPrepareHeader(IntPtr h, IntPtr hdr, int size);
        [DllImport("winmm.dll")] static extern int waveOutWrite(IntPtr h, IntPtr hdr, int size);
    }

    // What the PC is playing right now (Spotify, YouTube, a game). Two sources, both read-only:
    // - the output's peak meter: one cheap COM call, always on, tells Obsidot that music started;
    // - while music plays, a loopback capture of the mix, cut into 48 bands by an FFT for the dot rays.
    // Samples live in a 2048-sample ring and are overwritten continuously; nothing is recorded or stored.
    static class Meter
    {
        static IAudioMeterInformation meter; static double nextGet;
        static readonly float[] chans = new float[16];

        static IMMDevice Device()
        {
            var en = (IMMDeviceEnumerator)new MMDeviceEnumerator(); IMMDevice dev;
            return en.GetDefaultAudioEndpoint(0, 1, out dev) == 0 ? dev : null; // eRender, eMultimedia
        }

        public static float Peak(float[] lr) // overall peak; lr gets the left and right channels
        {
            double now = U.Now;
            if (meter == null || now > nextGet) // re-acquire now and then: headphones plugged in change the default device
            {
                nextGet = now + 5; meter = null;
                try
                {
                    var dev = Device();
                    if (dev != null) { var iid = typeof(IAudioMeterInformation).GUID; object o; if (dev.Activate(ref iid, 23, IntPtr.Zero, out o) == 0) meter = (IAudioMeterInformation)o; }
                }
                catch { }
            }
            float p = 0; int n;
            if (meter == null || meter.GetPeakValue(out p) != 0) { meter = null; lr[0] = lr[1] = 0; return 0; }
            if (meter.GetMeteringChannelCount(out n) == 0 && n >= 2 && n <= chans.Length && meter.GetChannelsPeakValues(n, chans) == 0) { lr[0] = chans[0]; lr[1] = chans[1]; }
            else lr[0] = lr[1] = p;
            return p;
        }

        // ---- loopback capture ----
        static IAudioClient client; static IAudioCaptureClient cap; static int nch, bits, rate; static bool isFloat; static double retryT;
        const int NR = 2048; static readonly float[] ring = new float[NR]; static int wpos;
        static float[] fbuf = new float[0]; static short[] sbuf = new short[0];

        public static bool Capture()
        {
            if (cap != null) return true;
            if (U.Now < retryT) return false;
            retryT = U.Now + 3;
            try
            {
                var dev = Device(); if (dev == null) return false;
                var iid = typeof(IAudioClient).GUID; object o; if (dev.Activate(ref iid, 23, IntPtr.Zero, out o) != 0) return false;
                client = (IAudioClient)o; IntPtr fmt;
                if (client.GetMixFormat(out fmt) != 0) { StopCapture(); return false; }
                int tag = Marshal.ReadInt16(fmt, 0) & 0xFFFF; nch = Marshal.ReadInt16(fmt, 2); rate = Marshal.ReadInt32(fmt, 4); bits = Marshal.ReadInt16(fmt, 14);
                isFloat = tag == 3 || (tag == 0xFFFE && Marshal.ReadInt32(fmt, 24) == 3); // WAVE_FORMAT_EXTENSIBLE: the sub-format GUID starts with 3 for IEEE float
                int hr = client.Initialize(0, 0x00020000, 2000000, 0, fmt, IntPtr.Zero);  // shared, LOOPBACK, 200 ms buffer
                Marshal.FreeCoTaskMem(fmt);
                if (hr != 0 || (!isFloat && bits != 16) || nch < 1) { StopCapture(); return false; }
                var ciid = typeof(IAudioCaptureClient).GUID; object c2;
                if (client.GetService(ref ciid, out c2) != 0) { StopCapture(); return false; }
                cap = (IAudioCaptureClient)c2;
                if (client.Start() != 0) { StopCapture(); return false; }
                return true;
            }
            catch { StopCapture(); return false; }
        }

        public static void StopCapture()
        {
            if (client == null && cap == null) return;
            try { if (client != null) client.Stop(); } catch { }
            if (cap != null) Marshal.ReleaseComObject(cap); if (client != null) Marshal.ReleaseComObject(client);
            cap = null; client = null; Array.Clear(ring, 0, NR);
        }

        // drain what arrived since last time into the mono ring
        static void Pump()
        {
            int n;
            while (true)
            {
                if (cap.GetNextPacketSize(out n) != 0) { StopCapture(); return; } // device gone: retry later
                if (n == 0) return;
                IntPtr data; int frames, flags; long p1, p2;
                if (cap.GetBuffer(out data, out frames, out flags, out p1, out p2) != 0) { StopCapture(); return; }
                int cnt = frames * nch;
                if ((flags & 2) != 0) { for (int i = 0; i < frames; i++) { ring[wpos] = 0; wpos = (wpos + 1) % NR; } } // AUDCLNT_BUFFERFLAGS_SILENT
                else if (isFloat)
                {
                    if (fbuf.Length < cnt) fbuf = new float[cnt]; Marshal.Copy(data, fbuf, 0, cnt);
                    for (int i = 0; i < frames; i++) { float v = 0; for (int k = 0; k < nch && k < 2; k++) v += fbuf[i * nch + k]; ring[wpos] = v / Math.Min(2, nch); wpos = (wpos + 1) % NR; }
                }
                else
                {
                    if (sbuf.Length < cnt) sbuf = new short[cnt]; Marshal.Copy(data, sbuf, 0, cnt);
                    for (int i = 0; i < frames; i++) { float v = 0; for (int k = 0; k < nch && k < 2; k++) v += sbuf[i * nch + k] / 32768f; ring[wpos] = v / Math.Min(2, nch); wpos = (wpos + 1) % NR; }
                }
                cap.ReleaseBuffer(frames);
            }
        }

        // 48 bands, 40 Hz .. 16 kHz on a log scale, 0..1, self-levelling
        const int NF = 1024;
        static readonly double[] re = new double[NF], im = new double[NF], win = new double[NF];
        static double top = -40;
        public static bool Spectrum(float[] bands, double dt)
        {
            if (cap == null) return false;
            try { Pump(); } catch { StopCapture(); return false; }
            if (cap == null || rate <= 0) return false;
            if (win[1] == 0) for (int i = 0; i < NF; i++) win[i] = 0.5 - 0.5 * Math.Cos(2 * Math.PI * i / (NF - 1));
            for (int i = 0; i < NF; i++) { re[i] = ring[(wpos - NF + i + NR) % NR] * win[i]; im[i] = 0; }
            Fft(re, im);
            double peak = -120; int nb = bands.Length;
            var db = new double[nb];
            for (int b = 0; b < nb; b++)
            {
                double f0 = 40 * Math.Pow(400, (double)b / nb), f1 = 40 * Math.Pow(400, (double)(b + 1) / nb);
                int k0 = Math.Max(1, (int)(f0 * NF / rate)), k1 = Math.Max(k0, Math.Min(NF / 2 - 1, (int)(f1 * NF / rate)));
                double m = 0; for (int k = k0; k <= k1; k++) m = Math.Max(m, Math.Sqrt(re[k] * re[k] + im[k] * im[k]));
                double d = 20 * Math.Log10(m * 4 / NF + 1e-9) + 3 * Math.Log(Math.Max(1, (f0 + f1) / 2 / 200), 2); // music falls off ~3 dB/octave: tilt it back up
                db[b] = d; peak = Math.Max(peak, d);
            }
            top = Math.Max(Math.Max(peak, -48), top - dt * 4); // the loudest band sets the scale, and the scale relaxes slowly
            for (int b = 0; b < nb; b++) bands[b] = (float)Math.Max(0, Math.Min(1, (db[b] - (top - 40)) / 40));
            return true;
        }

        static void Fft(double[] xr, double[] xi)
        {
            int n = xr.Length;
            for (int i = 1, j = 0; i < n; i++)
            {
                int bit = n >> 1; for (; (j & bit) != 0; bit >>= 1) j ^= bit; j ^= bit;
                if (i < j) { double t = xr[i]; xr[i] = xr[j]; xr[j] = t; t = xi[i]; xi[i] = xi[j]; xi[j] = t; }
            }
            for (int len = 2; len <= n; len <<= 1)
            {
                double ang = -2 * Math.PI / len, wr = Math.Cos(ang), wi = Math.Sin(ang);
                for (int i = 0; i < n; i += len)
                {
                    double cr = 1, ci = 0;
                    for (int k = 0; k < len / 2; k++)
                    {
                        int a = i + k, b = a + len / 2;
                        double tr = xr[b] * cr - xi[b] * ci, ti = xr[b] * ci + xi[b] * cr;
                        xr[b] = xr[a] - tr; xi[b] = xi[a] - ti; xr[a] += tr; xi[a] += ti;
                        double nr = cr * wr - ci * wi; ci = cr * wi + ci * wr; cr = nr;
                    }
                }
            }
        }

        [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")] class MMDeviceEnumerator { }
        [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IMMDeviceEnumerator { [PreserveSig] int EnumAudioEndpoints(int flow, int mask, out IntPtr devs); [PreserveSig] int GetDefaultAudioEndpoint(int flow, int role, out IMMDevice dev); }
        [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IMMDevice { [PreserveSig] int Activate(ref Guid iid, int ctx, IntPtr p, [MarshalAs(UnmanagedType.IUnknown)] out object o); }
        [ComImport, Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IAudioMeterInformation { [PreserveSig] int GetPeakValue(out float peak); [PreserveSig] int GetMeteringChannelCount(out int n); [PreserveSig] int GetChannelsPeakValues(int n, [MarshalAs(UnmanagedType.LPArray), Out] float[] peaks); }
        [ComImport, Guid("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IAudioClient
        {
            [PreserveSig] int Initialize(int shareMode, int flags, long bufferDuration, long periodicity, IntPtr format, IntPtr sessionGuid);
            [PreserveSig] int GetBufferSize(out int frames);
            [PreserveSig] int GetStreamLatency(out long latency);
            [PreserveSig] int GetCurrentPadding(out int frames);
            [PreserveSig] int IsFormatSupported(int shareMode, IntPtr format, out IntPtr closest);
            [PreserveSig] int GetMixFormat(out IntPtr format);
            [PreserveSig] int GetDevicePeriod(out long def, out long min);
            [PreserveSig] int Start();
            [PreserveSig] int Stop();
            [PreserveSig] int Reset();
            [PreserveSig] int SetEventHandle(IntPtr h);
            [PreserveSig] int GetService(ref Guid iid, [MarshalAs(UnmanagedType.IUnknown)] out object o);
        }
        [ComImport, Guid("C8ADBD64-E71E-48a0-A4DE-185C395CD317"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IAudioCaptureClient
        {
            [PreserveSig] int GetBuffer(out IntPtr data, out int frames, out int flags, out long devicePosition, out long qpcPosition);
            [PreserveSig] int ReleaseBuffer(int frames);
            [PreserveSig] int GetNextPacketSize(out int frames);
        }
    }
}