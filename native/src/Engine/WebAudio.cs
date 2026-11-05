using System.Runtime.CompilerServices;

namespace Brickblade;

/* A small Web Audio work-alike: the node graph the game's sound is written
   against (oscillators, buffer sources, gains, biquads, a waveshaper, a stereo
   panner, a reverb, a delay, a compressor), AudioParam automation
   (setValueAtTime / linearRamp / exponentialRamp, and a-rate modulation by a
   connected node), rendered in 128-frame blocks on the audio thread. The game
   builds voices on its own thread; one lock keeps the two apart. */
public sealed class AudioParam
{
    readonly AudioContext ctx;
    double _v;
    struct Ev { public double t, v; public int kind; } /* 0 set, 1 linear, 2 exponential */
    readonly List<Ev> evs = new();
    internal readonly List<AudioNode> mods = new();
    public AudioParam(AudioContext c, double v) { ctx = c; _v = v; }
    public double value
    {
        get => _v;
        set { lock (ctx.Lock) { _v = value; evs.Clear(); } }
    }
    public AudioParam setValueAtTime(double v, double t) { lock (ctx.Lock) Insert(new Ev { t = t, v = v, kind = 0 }); return this; }
    public AudioParam linearRampToValueAtTime(double v, double t) { lock (ctx.Lock) Insert(new Ev { t = t, v = v, kind = 1 }); return this; }
    public AudioParam exponentialRampToValueAtTime(double v, double t) { lock (ctx.Lock) Insert(new Ev { t = t, v = Math.Abs(v) < 1e-30 ? 1e-30 : v, kind = 2 }); return this; }
    void Insert(Ev e) { int i = evs.Count; while (i > 0 && evs[i - 1].t > e.t) i--; evs.Insert(i, e); }
    /* the value at time t, from the automation (the events before t, and the
       ramp that t is inside) */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double At(double t)
    {
        if (evs.Count == 0) return _v;
        double prevT = 0, prevV = _v;
        bool any = false;
        for (int i = 0; i < evs.Count; i++)
        {
            var e = evs[i];
            if (e.t <= t) { prevT = e.t; prevV = e.v; any = true; continue; }
            /* t is before this event: a ramp toward it runs from the last one */
            if (e.kind == 1) { double f = (t - prevT) / Math.Max(1e-9, e.t - prevT); return prevV + (e.v - prevV) * f; }
            if (e.kind == 2)
            {
                if (prevV == 0 || Math.Sign(prevV) != Math.Sign(e.v)) return prevV;
                double f = (t - prevT) / Math.Max(1e-9, e.t - prevT);
                return prevV * Math.Pow(e.v / prevV, f);
            }
            return prevV;
        }
        _ = any;
        return prevV;
    }
    /* drop events wholly in the past, keeping the last one as the base */
    internal void Prune(double t)
    {
        int keep = -1;
        for (int i = 0; i < evs.Count; i++) if (evs[i].t <= t) keep = i; else break;
        if (keep > 0) { _v = evs[keep].v; evs.RemoveRange(0, keep); var e = evs[0]; e.kind = 0; evs[0] = e; }
    }
    internal bool Automated => evs.Count > 0;

    /* the values for the block being rendered, once per block: a walk over the
       events' segments, exponential ramps by a per-frame multiplier from an
       exact start (as Chromium's timeline does), events that are over dropped.
       `Constant` says whether the block holds one value throughout. */
    readonly float[] vals = new float[AudioContext.BLOCK];
    long valsBlock = -1;
    internal bool Constant;
    internal float[] Values()
    {
        if (valsBlock == ctx.blockId) return vals;
        valsBlock = ctx.blockId;
        const int B = AudioContext.BLOCK;
        long f0 = ctx.blockFrame; double sr = ctx.sampleRate;
        Prune(f0 / sr);
        if (evs.Count == 0 || (evs.Count == 1 && evs[0].t <= f0 / sr))
        {
            float c = (float)(evs.Count == 0 ? _v : evs[0].v);
            Array.Fill(vals, c); Constant = true; return vals;
        }
        Constant = false;
        int i = 0;
        while (i < B)
        {
            double t = (f0 + i) / sr;
            int k = 0; while (k < evs.Count && evs[k].t <= t) k++;
            if (k == evs.Count) { Array.Fill(vals, (float)evs[^1].v, i, B - i); break; }
            var e = evs[k];
            double pT = k == 0 ? 0 : evs[k - 1].t, pV = k == 0 ? _v : evs[k - 1].v;
            /* the frames before this event's time */
            long endF = (long)Math.Ceiling(e.t * sr);
            int n = (int)Math.Clamp(endF - f0 - i, 1, B - i);
            if (e.kind == 1)
            {
                double span = Math.Max(1e-9, e.t - pT);
                for (int j = 0; j < n; j++) vals[i + j] = (float)(pV + (e.v - pV) * (((f0 + i + j) / sr) - pT) / span);
            }
            else if (e.kind == 2 && pV != 0 && Math.Sign(pV) == Math.Sign(e.v))
            {
                double span = Math.Max(1e-9, e.t - pT), ratio = e.v / pV;
                double v = pV * Math.Pow(ratio, (t - pT) / span), m = Math.Pow(ratio, 1 / (span * sr));
                for (int j = 0; j < n; j++) { vals[i + j] = (float)v; v *= m; }
            }
            else Array.Fill(vals, (float)pV, i, n);
            i += n;
        }
        return vals;
    }
}

public abstract class AudioNode
{
    public readonly AudioContext context;
    internal readonly List<AudioNode> inputs = new();
    internal readonly List<AudioNode> outputs = new();
    internal readonly List<AudioParam> paramOuts = new();
    internal float[] L = new float[AudioContext.BLOCK], R = new float[AudioContext.BLOCK];
    internal long block = -1;
    /* this block's output is exact silence (a source outside its play time, or
       a stateless node fed only silence): downstream nodes skip it */
    internal bool silent;
    public bool persistent;               /* a bus: never finishes */
    protected AudioNode(AudioContext c) { context = c; }
    public AudioNode connect(AudioNode n) { lock (context.Lock) { n.inputs.Add(this); outputs.Add(n); } return n; }
    public void connect(AudioParam p) { lock (context.Lock) { p.mods.Add(this); paramOuts.Add(p); } }
    public void disconnect() { lock (context.Lock) { foreach (var o in outputs) o.inputs.Remove(this); foreach (var p in paramOuts) p.mods.Remove(this); outputs.Clear(); paramOuts.Clear(); } }
    /* this node's output for the current block (computed once) */
    internal void Pull()
    {
        if (block == context.blockId) return;
        block = context.blockId;
        Process();
    }
    protected abstract void Process();
    /* the sum of the inputs into L/R; true when every input was silent */
    protected bool SumInputs()
    {
        Array.Clear(L); Array.Clear(R);
        bool quiet = true;
        for (int k = 0; k < inputs.Count; k++)
        {
            var n = inputs[k]; n.Pull();
            if (n.silent) continue;
            quiet = false;
            var a = n.L; var b = n.R;
            for (int i = 0; i < AudioContext.BLOCK; i++) { L[i] += a[i]; R[i] += b[i]; }
        }
        return quiet;
    }
    /* a modulator runs every block whether or not its result is used, so its
       phase moves on as Chromium's does */
    protected static void RunMods(AudioParam p) { foreach (var n in p.mods) n.Pull(); }
    /* finished: a source past its stop, or a node every input of which has finished */
    internal virtual bool Finished
    {
        get
        {
            if (persistent) return false;
            if (inputs.Count == 0) return true;
            return false;
        }
    }
    internal double ModAt(AudioParam p, int i)
    {
        double m = 0;
        foreach (var n in p.mods) { n.Pull(); m += n.L[i]; }
        return m;
    }
}

public sealed class GainNode : AudioNode
{
    public readonly AudioParam gain;
    public GainNode(AudioContext c) : base(c) { gain = new AudioParam(c, 1); }
    protected override void Process()
    {
        bool mod = gain.mods.Count > 0;
        if (silent = SumInputs()) { if (mod) RunMods(gain); return; }
        if (!gain.Automated && !mod)
        {
            float g = (float)gain.value;
            if (g != 1) for (int i = 0; i < AudioContext.BLOCK; i++) { L[i] *= g; R[i] *= g; }
            return;
        }
        var gv = gain.Values();
        for (int i = 0; i < AudioContext.BLOCK; i++)
        {
            float g = mod ? (float)(gv[i] + ModAt(gain, i)) : gv[i];
            L[i] *= g; R[i] *= g;
        }
    }
}

public abstract class AudioScheduledSourceNode : AudioNode
{
    protected double startT = double.PositiveInfinity, stopT = double.PositiveInfinity;
    /* the first frame that plays, how far after the start time it falls (0..1
       of a frame: a source started between two frames begins that far into its
       signal, as Chromium's do) and the first frame that is silent again */
    protected long startF = long.MaxValue, stopF = long.MaxValue; protected double startOff;
    protected AudioScheduledSourceNode(AudioContext c) : base(c) { c.Track(this); }
    public virtual void start(double when = 0)
    {
        startT = Math.Max(when, 0);
        /* a time that is a whole frame but for the last bits of a double (0.17 *
           48000 = 8160.0000000000009) is that frame, as in Chromium */
        double x = startT * context.sampleRate, rx = System.Math.Round(x);
        if (Math.Abs(x - rx) < 1e-6) x = rx;
        startF = (long)Math.Ceiling(x); startOff = startF - x;
    }
    public void stop(double when = 0) { stopT = when; stopF = (long)Math.Ceiling(Math.Max(0, when) * context.sampleRate); }
    /* the offset into the signal at this frame, the first time it plays: a source
       whose start time had already passed starts on the frame, with none */
    protected double FirstOffset(long frame) => frame == startF ? startOff : 0;
    internal override bool Finished => context.blockTime > stopT + 0.01;
}

/* Chromium's oscillator tables: every shape as a Fourier series, in 36 tables
   a third of an octave apart, each with the partials that would alias at its
   pitch cut away, all scaled so the fullest table peaks at 1. A note reads the
   two tables either side of its pitch, linearly within each and between them. */
sealed class PeriodicWave
{
    public readonly int N, Ranges; public readonly float RateScale; readonly float lowest; readonly float[][] tables;
    const float CentsPerRange = 1200f / 3;
    static readonly Dictionary<(string, double), PeriodicWave> cache = new();
    public static PeriodicWave For(string type, double sr)
    {
        type = type is "square" or "sawtooth" or "triangle" ? type : "sine";
        lock (cache) { if (!cache.TryGetValue((type, sr), out var w)) cache[(type, sr)] = w = new PeriodicWave(type, sr); return w; }
    }
    PeriodicWave(string type, double sr)
    {
        N = sr <= 24000 ? 2048 : sr <= 88200 ? 4096 : 16384;
        int half = N / 2;
        lowest = 0.5f * (float)sr / half; RateScale = N / (float)sr;
        Ranges = (int)(0.5f + 3 * MathF.Log2(N));
        var b = new float[half];
        for (int n = 1; n < half; n++)
        {
            float pf = 2 / (n * MathF.PI);
            b[n] = type switch
            {
                "square" => (n & 1) != 0 ? 2 * pf : 0,
                "sawtooth" => pf * ((n & 1) != 0 ? 1 : -1),
                "triangle" => (n & 1) != 0 ? 2 * (pf * pf) * ((((n - 1) >> 1) & 1) != 0 ? -1 : 1) : 0,
                _ => n == 1 ? 1 : 0,
            };
        }
        var fft = new Fft(N);
        tables = new float[Ranges][];
        float norm = 1;
        for (int r = 0; r < Ranges; r++)
        {
            int partials = (int)(MathF.Pow(2, -r * CentsPerRange / 1200) * half);
            var re = new float[N]; var im = new float[N];
            for (int n = 1; n < Math.Min(half, partials + 1); n++) { im[n] = -b[n] / 2; im[N - n] = b[n] / 2; }
            fft.Run(re, im, true);
            if (r == 0) { float mx = 0; foreach (var v in re) mx = Math.Max(mx, Math.Abs(v)); if (mx > 0) norm = 1 / mx; }
            for (int k = 0; k < N; k++) re[k] *= norm;
            tables[r] = re;
        }
    }
    /* the two tables either side of a pitch, and how far between them */
    public void Select(float freq, out float[] hi, out float[] lo, out float tif)
    {
        freq = MathF.Abs(freq);
        float ratio = freq > 0 ? freq / lowest : 0.5f;
        float pitch = 1 + MathF.Log2(ratio) * 1200 / CentsPerRange;
        pitch = Math.Clamp(pitch, 0, Ranges - 1);
        int r1 = (int)pitch, r2 = r1 < Ranges - 1 ? r1 + 1 : r1;
        tif = pitch - r1; hi = tables[r1]; lo = tables[r2];
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Read(double vri, int mask, float[] hi, float[] lo, float tif)
    {
        int i0 = (int)vri, i1 = (i0 + 1) & mask; i0 &= mask;
        double f = (float)vri - (int)vri;
        double sh = (1 - f) * hi[i0] + f * hi[i1], sl = (1 - f) * lo[i0] + f * lo[i1];
        return (float)((1 - tif) * sh + tif * sl);
    }
}

public sealed class OscillatorNode : AudioScheduledSourceNode
{
    public string type = "sine";
    public readonly AudioParam frequency, detune;
    double vri = -1;      /* where in the wave table, -1 before the first frame */
    public OscillatorNode(AudioContext c) : base(c) { frequency = new AudioParam(c, 440); detune = new AudioParam(c, 0); }
    protected override void Process()
    {
        double sr = context.sampleRate, nyq = sr / 2; long f0 = context.blockFrame;
        bool fm = frequency.mods.Count > 0, dm = detune.mods.Count > 0;
        if (stopF <= f0 || startF >= f0 + AudioContext.BLOCK)
        {
            if (!silent) { Array.Clear(L); Array.Clear(R); silent = true; }
            if (fm) RunMods(frequency); if (dm) RunMods(detune);
            return;
        }
        silent = false;
        var wave = PeriodicWave.For(type, sr);
        int n = wave.N;
        /* CHROMIUM'S START QUANTUM: an oscillator that starts part-way into a
           render quantum reads its sample-accurate frequency and detune from the
           START of the quantum (the values are computed for all 128 frames and
           read from the first, not from where the oscillator begins), so its
           first frames play the values from that many frames earlier -- for a
           pitch set at the start time, the param's default (440 Hz). It is what
           every browser build of the game sounds like, so it is kept. */
        bool aRate = fm || dm || frequency.Automated || detune.Automated;
        int lag = aRate && startF > f0 && startF < f0 + AudioContext.BLOCK ? (int)(startF - f0) : 0;
        var fv = frequency.Values(); var dv = detune.Values();
        float lastF = float.NaN; float[] hi = null, lo = null; float tif = 0;
        for (int i = 0; i < AudioContext.BLOCK; i++)
        {
            long fr = f0 + i;
            if (fr < startF || fr >= stopF) { L[i] = R[i] = 0; continue; }
            int j = i - lag;
            double f = fv[j] + (fm ? ModAt(frequency, j) : 0);
            double dt = dv[j] + (dm ? ModAt(detune, j) : 0);
            if (dt != 0) f *= Math.Pow(2, dt / 1200);
            f = Math.Clamp(f, -nyq, nyq);
            float ff = (float)f, incr = ff * wave.RateScale;
            if (ff != lastF) { wave.Select(ff, out hi, out lo, out tif); lastF = ff; }
            if (vri < 0) { vri = FirstOffset(fr) * incr; vri -= Math.Floor(vri / n) * n; }
            L[i] = R[i] = PeriodicWave.Read(vri, n - 1, hi, lo, tif);
            vri += incr; vri -= Math.Floor(vri / n) * n;
        }
    }
}

public sealed class AudioBuffer
{
    public readonly int length, numberOfChannels; public readonly double sampleRate;
    readonly float[][] ch;
    public AudioBuffer(int channels, int len, double rate) { numberOfChannels = channels; length = len; sampleRate = rate; ch = new float[channels][]; for (int i = 0; i < channels; i++) ch[i] = new float[len]; }
    public float[] getChannelData(int c) => ch[c];
}

public sealed class AudioBufferSourceNode : AudioScheduledSourceNode
{
    public AudioBuffer buffer; public bool loop;
    double pos = -1, offset;
    bool ended;
    public AudioBufferSourceNode(AudioContext c) : base(c) { }
    public void start(double when, double off) { base.start(when); offset = Math.Max(0, off); pos = -1; }
    public override void start(double when = 0) { start(when, 0); }
    protected override void Process()
    {
        var b = buffer; long fb = context.blockFrame;
        if (b == null || ended || stopF <= fb || startF >= fb + AudioContext.BLOCK)
        {
            if (!silent) { Array.Clear(L); Array.Clear(R); silent = true; }
            return;
        }
        silent = false;
        var d0 = b.getChannelData(0); var d1 = b.numberOfChannels > 1 ? b.getChannelData(1) : d0;
        int len = b.length; double rate = b.sampleRate / context.sampleRate; long f0 = context.blockFrame;
        for (int i = 0; i < AudioContext.BLOCK; i++)
        {
            long fr = f0 + i;
            if (fr < startF || fr >= stopF || ended) { L[i] = R[i] = 0; continue; }
            /* the offset lands on a whole frame (Chromium keeps rate-1 playback
               bit-exact); a start between frames moves in by the fraction */
            if (pos < 0) pos = System.Math.Round(offset * b.sampleRate, MidpointRounding.AwayFromZero) + FirstOffset(fr) * rate;
            if (pos >= len) { if (loop && len > 0) pos %= len; else { ended = true; L[i] = R[i] = 0; continue; } }
            int k = (int)pos; double f = pos - k;
            int k2 = k + 1; if (k2 >= len) k2 = loop ? k2 - len : k;
            if (k2 == k && k >= 1)
            {   /* past the last frame (not looping): Chromium extrapolates from the last two */
                L[i] = (float)(d0[k] + (d0[k] - d0[k - 1]) * f); R[i] = (float)(d1[k] + (d1[k] - d1[k - 1]) * f);
            }
            else { L[i] = (float)((1 - f) * d0[k] + f * d0[k2]); R[i] = (float)((1 - f) * d1[k] + f * d1[k2]); }
            pos += rate;
        }
    }
}

public sealed class BiquadFilterNode : AudioNode
{
    public string type = "lowpass";
    public readonly AudioParam frequency, Q, gain;
    double b0, b1, b2, a1, a2;
    double lx1, lx2, ly1, ly2, rx1, rx2, ry1, ry2;
    double lastF = -1, lastQ = -1;
    public BiquadFilterNode(AudioContext c) : base(c) { frequency = new AudioParam(c, 350); Q = new AudioParam(c, 1); gain = new AudioParam(c, 0); }
    void Coef(double f, double q)
    {
        double sr = context.sampleRate;
        f = Math.Clamp(f, 10, sr / 2 - 10);
        double w0 = 2 * Math.PI * f / sr, cw = Math.Cos(w0), sw = Math.Sin(w0);
        double alpha, a0;
        /* Web Audio's Q is decibels for lowpass and highpass, a plain Q otherwise */
        switch (type)
        {
            case "highpass":
                alpha = sw / (2 * Math.Pow(10, q / 20));
                a0 = 1 + alpha; b0 = (1 + cw) / 2; b1 = -(1 + cw); b2 = (1 + cw) / 2; a1 = -2 * cw; a2 = 1 - alpha; break;
            case "bandpass":
                alpha = sw / (2 * Math.Max(1e-4, q));
                a0 = 1 + alpha; b0 = alpha; b1 = 0; b2 = -alpha; a1 = -2 * cw; a2 = 1 - alpha; break;
            default:
                alpha = sw / (2 * Math.Pow(10, q / 20));
                a0 = 1 + alpha; b0 = (1 - cw) / 2; b1 = 1 - cw; b2 = (1 - cw) / 2; a1 = -2 * cw; a2 = 1 - alpha; break;
        }
        b0 /= a0; b1 /= a0; b2 /= a0; a1 /= a0; a2 /= a0;
    }
    protected override void Process()
    {
        bool quiet = SumInputs();
        bool fm = frequency.mods.Count > 0;
        if (quiet && Math.Abs(ly1) + Math.Abs(ly2) + Math.Abs(ry1) + Math.Abs(ry2) + Math.Abs(lx1) + Math.Abs(lx2) + Math.Abs(rx1) + Math.Abs(rx2) < 1e-12)
        {
            lx1 = lx2 = ly1 = ly2 = rx1 = rx2 = ry1 = ry2 = 0;
            silent = true; if (fm) RunMods(frequency); return;
        }
        silent = false;
        var fv = frequency.Values();
        for (int i = 0; i < AudioContext.BLOCK; i++)
        {
            if ((i & 7) == 0)
            {
                double f = fv[i] + (fm ? ModAt(frequency, i) : 0), q = Q.value;
                if (Math.Abs(f - lastF) > 0.01 || q != lastQ) { Coef(f, q); lastF = f; lastQ = q; }
            }
            double x = L[i], y = b0 * x + b1 * lx1 + b2 * lx2 - a1 * ly1 - a2 * ly2;
            lx2 = lx1; lx1 = x; ly2 = ly1; ly1 = y; L[i] = (float)y;
            x = R[i]; y = b0 * x + b1 * rx1 + b2 * rx2 - a1 * ry1 - a2 * ry2;
            rx2 = rx1; rx1 = x; ry2 = ry1; ry1 = y; R[i] = (float)y;
        }
        if (double.IsNaN(ly1) || double.IsInfinity(ly1)) { lx1 = lx2 = ly1 = ly2 = rx1 = rx2 = ry1 = ry2 = 0; }
    }
    internal override bool Finished => !persistent && inputs.Count == 0;
}

public sealed class WaveShaperNode : AudioNode
{
    public float[] curve;
    public WaveShaperNode(AudioContext c) : base(c) { }
    protected override void Process()
    {
        bool quiet = SumInputs();
        var cv = curve; if (cv == null || cv.Length < 2) { silent = quiet; return; }
        int n = cv.Length;
        if (quiet && Shape(0, cv, n) == 0) { silent = true; return; }
        silent = false;
        for (int i = 0; i < AudioContext.BLOCK; i++)
        {
            L[i] = Shape(L[i], cv, n); R[i] = Shape(R[i], cv, n);
        }
    }
    static float Shape(float x, float[] cv, int n)
    {
        double v = (n - 1) * (x + 1) / 2;
        if (v <= 0) return cv[0];
        if (v >= n - 1) return cv[n - 1];
        int k = (int)v; double f = v - k;
        return (float)(cv[k] + (cv[k + 1] - cv[k]) * f);
    }
}

public sealed class StereoPannerNode : AudioNode
{
    public readonly AudioParam pan;
    public StereoPannerNode(AudioContext c) : base(c) { pan = new AudioParam(c, 0); }
    protected override void Process()
    {
        if (silent = SumInputs()) return;
        double x = (Math.Clamp(pan.value, -1, 1) + 1) / 2;
        float gl = (float)Math.Cos(x * Math.PI / 2), gr = (float)Math.Sin(x * Math.PI / 2);
        for (int i = 0; i < AudioContext.BLOCK; i++) { float m = (L[i] + R[i]) * 0.5f; L[i] = m * gl; R[i] = m * gr; }
    }
}

/* a complex radix-2 FFT of one size, twiddles and bit reversal made once */
sealed class Fft
{
    public readonly int n; readonly int[] rev; readonly float[] wr, wi;
    public Fft(int n)
    {
        this.n = n; rev = new int[n]; wr = new float[n / 2]; wi = new float[n / 2];
        int bits = System.Numerics.BitOperations.Log2((uint)n);
        for (int i = 0; i < n; i++) { int r = 0; for (int b = 0; b < bits; b++) r |= ((i >> b) & 1) << (bits - 1 - b); rev[i] = r; }
        for (int i = 0; i < n / 2; i++) { wr[i] = (float)Math.Cos(-2 * Math.PI * i / n); wi[i] = (float)Math.Sin(-2 * Math.PI * i / n); }
    }
    /* in place; the inverse is unscaled */
    public void Run(float[] re, float[] im, bool inverse)
    {
        for (int i = 0; i < n; i++) { int j = rev[i]; if (i < j) { (re[i], re[j]) = (re[j], re[i]); (im[i], im[j]) = (im[j], im[i]); } }
        float sg = inverse ? -1 : 1;
        for (int len = 2; len <= n; len <<= 1)
        {
            int half = len >> 1, step = n / len;
            for (int i = 0; i < n; i += len)
                for (int k = 0; k < half; k++)
                {
                    float cr = wr[k * step], ci = wi[k * step] * sg;
                    int u = i + k, v = u + half;
                    float tr = re[v] * cr - im[v] * ci, ti = re[v] * ci + im[v] * cr;
                    re[v] = re[u] - tr; im[v] = im[u] - ti; re[u] += tr; im[u] += ti;
                }
        }
    }
}

/* Convolution with the impulse response the game gives it, as Chromium's
   Reverb does: the response is normalised by its RMS power and calibrated
   (-58 dB, for 44.1 kHz), and the input is convolved with channel 0 for the
   left and channel 1 for the right. Uniformly partitioned overlap-save in two
   stages: 128-frame partitions for the first 4096 frames (zero latency, one
   render quantum at a time) and 4096-frame partitions for the rest, computed
   once every 32 quanta, just as their output falls due. The input is taken as
   mono, which is all the game sends it (the voices' room sends). */
public sealed class ConvolverNode : AudioNode
{
    public AudioBuffer buffer;
    public bool normalize = true;
    AudioBuffer built;
    sealed class Stage
    {
        public int B, P, slot; public Fft fft;
        public float[][] hRe, hIm, xRe, xIm;     /* the response's partitions, and the input's spectra (a ring) */
        public float[] inBuf, re, im, outRe, outIm; public int fill;
        public Stage(int b, float[] ir0, float[] ir1, int from, int to, float scale)
        {
            B = b; P = Math.Max(0, (to - from + b - 1) / b); fft = new Fft(2 * b);
            hRe = new float[P][]; hIm = new float[P][]; xRe = new float[P][]; xIm = new float[P][];
            for (int p = 0; p < P; p++)
            {
                var r = new float[2 * b]; var i = new float[2 * b];
                for (int k = 0; k < b; k++) { int at = from + p * b + k; if (at < to) { r[k] = ir0[at] * scale; i[k] = ir1[at] * scale; } }
                fft.Run(r, i, false); hRe[p] = r; hIm[p] = i;
                xRe[p] = new float[2 * b]; xIm[p] = new float[2 * b];
            }
            inBuf = new float[2 * b]; re = new float[2 * b]; im = new float[2 * b]; outRe = new float[b]; outIm = new float[b];
        }
        /* a full block of input is in inBuf's second half: its contribution, and
           the earlier blocks', for the next B frames of this stage's span */
        public void Step()
        {
            int n = 2 * B;
            var xr = xRe[slot]; var xi = xIm[slot];
            Array.Copy(inBuf, xr, n); Array.Clear(xi);
            fft.Run(xr, xi, false);
            Array.Clear(re); Array.Clear(im);
            for (int p = 0; p < P; p++)
            {
                int s = (slot - p + P) % P; var ar = xRe[s]; var ai = xIm[s]; var br = hRe[p]; var bi = hIm[p];
                for (int k = 0; k < n; k++) { re[k] += ar[k] * br[k] - ai[k] * bi[k]; im[k] += ar[k] * bi[k] + ai[k] * br[k]; }
            }
            fft.Run(re, im, true);
            float inv = 1f / n;
            for (int k = 0; k < B; k++) { outRe[k] = re[B + k] * inv; outIm[k] = im[B + k] * inv; }
            slot = (slot + 1) % P;
            Array.Copy(inBuf, B, inBuf, 0, B);
        }
    }
    Stage head, tail;
    float[] tailRe = Array.Empty<float>(), tailIm = Array.Empty<float>(); int tailAt;
    const int HeadB = AudioContext.BLOCK, HeadLen = 4096, TailB = 4096;
    public ConvolverNode(AudioContext c) : base(c) { persistent = true; }
    void Build()
    {
        built = buffer; head = tail = null;
        if (buffer == null || buffer.length == 0) return;
        var ir0 = buffer.getChannelData(0); var ir1 = buffer.numberOfChannels > 1 ? buffer.getChannelData(1) : ir0;
        int len = buffer.length;
        float scale = 1;
        if (normalize)
        {
            double pw = 0; for (int c = 0; c < buffer.numberOfChannels; c++) foreach (var v in buffer.getChannelData(c)) pw += v * v;
            double power = Math.Sqrt(pw / (buffer.numberOfChannels * (double)len));
            if (!double.IsFinite(power) || power < 0.000125) power = 0.000125;
            scale = (float)(1 / power * Math.Pow(10, -58 / 20.0) * (44100 / buffer.sampleRate));
        }
        head = new Stage(HeadB, ir0, ir1, 0, Math.Min(len, HeadLen), scale);
        if (len > HeadLen) { tail = new Stage(TailB, ir0, ir1, HeadLen, len, scale); tailRe = new float[TailB]; tailIm = new float[TailB]; tailAt = 0; }
    }
    protected override void Process()
    {
        SumInputs();
        if (buffer != built) Build();
        if (head == null) { Array.Clear(L); Array.Clear(R); return; }
        const int Q = AudioContext.BLOCK;
        for (int i = 0; i < Q; i++) head.inBuf[Q + i] = 0.5f * (L[i] + R[i]);
        if (tail != null) for (int i = 0; i < Q; i++) tail.inBuf[TailB + tail.fill + i] = 0.5f * (L[i] + R[i]);
        head.Step();
        for (int i = 0; i < Q; i++) { L[i] = head.outRe[i]; R[i] = head.outIm[i]; }
        if (tail != null)
        {
            /* the tail's output for these frames was computed when the block before them filled */
            for (int i = 0; i < Q; i++) { L[i] += tailRe[tailAt + i]; R[i] += tailIm[tailAt + i]; }
            tailAt += Q;
            tail.fill += Q;
            if (tail.fill == TailB)
            {
                tail.fill = 0;
                tail.Step();
                Array.Copy(tail.outRe, tailRe, TailB); Array.Copy(tail.outIm, tailIm, TailB); tailAt = 0;
            }
        }
    }
}

/* a delay line; its output for a block is read before its input for the block
   is written, which is what lets it sit inside a feedback loop */
public sealed class DelayNode : AudioNode
{
    public readonly AudioParam delayTime;
    readonly float[] bl, br; int w;
    public DelayNode(AudioContext c, double max) : base(c)
    {
        delayTime = new AudioParam(c, 0);
        int n = (int)(max * c.sampleRate) + AudioContext.BLOCK * 2;
        bl = new float[n]; br = new float[n];
        persistent = true;
        c.delays.Add(this);
    }
    protected override void Process()
    {
        int n = bl.Length, d = Math.Clamp((int)(delayTime.value * context.sampleRate), AudioContext.BLOCK, n - 1);
        for (int i = 0; i < AudioContext.BLOCK; i++) { int r = (w + i - d + n) % n; L[i] = bl[r]; R[i] = br[r]; }
    }
    /* after the block: what came in goes into the line */
    internal void Write()
    {
        float[] sl = new float[AudioContext.BLOCK], sr = new float[AudioContext.BLOCK];
        foreach (var inp in inputs) { inp.Pull(); for (int i = 0; i < AudioContext.BLOCK; i++) { sl[i] += inp.L[i]; sr[i] += inp.R[i]; } }
        int n = bl.Length;
        for (int i = 0; i < AudioContext.BLOCK; i++) { bl[(w + i) % n] = sl[i]; br[(w + i) % n] = sr[i]; }
        w = (w + AudioContext.BLOCK) % n;
    }
}

/* Chromium's compressor, step for step (DynamicsCompressorKernel): a 6 ms
   look-ahead, a detector on the undelayed signal, a soft knee that turns into
   the ratio, attack toward the wanted gain and an adaptive release (deeper
   compression lets go faster), a sine warp on the gain, and the automatic
   make-up gain the Web Audio spec calls for, which lifts everything under the
   threshold. In float, as Chromium runs it, in 32-frame steps. */
public sealed class DynamicsCompressorNode : AudioNode
{
    public readonly AudioParam threshold, knee, ratio, attack, release;
    const int MaxPre = 1024, PreMask = MaxPre - 1, Div = 32;
    const float PiOver2 = MathF.PI / 2;
    readonly float[] preL = new float[MaxPre], preR = new float[MaxPre];
    int preRead, preWrite, lastPre = -1;
    float detectorAverage = 0, compressorGain = 1, maxAttackDiffDb = -1;
    /* the static curve, rebuilt when threshold, knee or ratio change */
    float cTh = float.NaN, cKnee = float.NaN, cRatio = float.NaN, linThreshold, slope, kneeThreshold, kneeThresholdDb, yKneeThresholdDb, kCurve;
    public DynamicsCompressorNode(AudioContext c) : base(c)
    {
        threshold = new AudioParam(c, -24); knee = new AudioParam(c, 30); ratio = new AudioParam(c, 12);
        attack = new AudioParam(c, 0.003); release = new AudioParam(c, 0.25); persistent = true;
    }
    static float Db2Lin(float db) => MathF.Pow(10, 0.05f * db);
    static float Lin2Db(float x) => 20 * MathF.Log10(x);
    float KneeCurve(float x, float k) => x < linThreshold ? x : linThreshold + (1 - MathF.Exp(-k * (x - linThreshold))) / k;
    float Saturate(float x, float k)
    {
        if (x < kneeThreshold) return KneeCurve(x, k);
        float xDb = Lin2Db(x), yDb = yKneeThresholdDb + slope * (xDb - kneeThresholdDb);
        return Db2Lin(yDb);
    }
    float SlopeAt(float x, float k)
    {
        if (x < linThreshold) return 1;
        float x2 = x * 1.001f;
        float xDb = Lin2Db(x), x2Db = Lin2Db(x2), yDb = Lin2Db(KneeCurve(x, k)), y2Db = Lin2Db(KneeCurve(x2, k));
        return (y2Db - yDb) / (x2Db - xDb);
    }
    float KAtSlope(float desired)
    {
        float x = Db2Lin(cTh + cKnee), minK = 0.1f, maxK = 10000, k = 5;
        for (int i = 0; i < 15; i++)
        {
            if (SlopeAt(x, k) < desired) maxK = k; else minK = k;
            k = MathF.Sqrt(minK * maxK);
        }
        return k;
    }
    float Curve(float th, float kn, float ra)
    {
        if (th != cTh || kn != cKnee || ra != cRatio)
        {
            cTh = th; linThreshold = Db2Lin(th); cKnee = kn; cRatio = ra; slope = 1 / ra;
            float k = KAtSlope(1 / ra);
            kneeThresholdDb = th + kn; kneeThreshold = Db2Lin(kneeThresholdDb);
            yKneeThresholdDb = Lin2Db(KneeCurve(kneeThreshold, k));
            kCurve = k;
        }
        return kCurve;
    }
    protected override void Process()
    {
        SumInputs();
        float sr = (float)context.sampleRate;
        /* the spec's ranges */
        float th = (float)Math.Clamp(threshold.value, -100, 0), kn = (float)Math.Clamp(knee.value, 0, 40), ra = (float)Math.Clamp(ratio.value, 1, 20);
        float at = (float)Math.Clamp(attack.value, 0, 1), rel = (float)Math.Clamp(release.value, 0, 1);
        float k = Curve(th, kn, ra);
        float makeup = MathF.Pow(1 / Saturate(1, k), 0.6f);
        float attackFrames = MathF.Max(0.001f, at) * sr, releaseFrames = sr * rel, satReleaseFrames = 0.0025f * sr;
        float y1 = releaseFrames * 0.09f, y2 = releaseFrames * 0.16f, y3 = releaseFrames * 0.42f, y4 = releaseFrames * 0.98f;
        float kA = 0.9999999999999998f * y1 + 1.8432219684323923e-16f * y2 - 1.9373394351676423e-16f * y3 + 8.824516011816245e-18f * y4;
        float kB = -1.5788320352845888f * y1 + 2.3305837032074286f * y2 - 0.9141194204840429f * y3 + 0.1623677525612032f * y4;
        float kC = 0.5334142869106424f * y1 - 1.272736789213631f * y2 + 0.9258856042207512f * y3 - 0.18656310191776226f * y4;
        float kD = 0.08783463138207234f * y1 - 0.1694162967925622f * y2 + 0.08588057951595272f * y3 - 0.00429891410546283f * y4;
        float kE = -0.042416883008123074f * y1 + 0.1115693827987602f * y2 - 0.09764676325265872f * y3 + 0.028494263462021576f * y4;
        int preFrames = Math.Min(MaxPre - 1, (int)(0.006f * sr));
        if (preFrames != lastPre)
        {
            lastPre = preFrames; Array.Clear(preL); Array.Clear(preR);
            preRead = 0; preWrite = preFrames;
        }
        int fi = 0;
        for (int d = 0; d < AudioContext.BLOCK / Div; d++)
        {
            if (!float.IsFinite(detectorAverage)) detectorAverage = 1;
            float desired = detectorAverage;
            float scaledDesired = MathF.Asin(desired) / PiOver2;
            float envRate;
            bool releasing = scaledDesired > compressorGain;
            float diffDb = Lin2Db(compressorGain / scaledDesired);
            if (releasing)
            {
                maxAttackDiffDb = -1;
                if (!float.IsFinite(diffDb)) diffDb = -1;
                float x = Math.Clamp(diffDb, -12f, 0f);
                x = 0.25f * (x + 12);
                float x2 = x * x, x3 = x2 * x, x4 = x2 * x2;
                float relFrames = kA + kB * x + kC * x2 + kD * x3 + kE * x4;
                envRate = Db2Lin(5 / relFrames);
            }
            else
            {
                if (!float.IsFinite(diffDb)) diffDb = 1;
                if (maxAttackDiffDb == -1 || maxAttackDiffDb < diffDb) maxAttackDiffDb = diffDb;
                float eff = MathF.Max(0.5f, maxAttackDiffDb);
                envRate = 1 - MathF.Pow(0.25f / eff, 1 / attackFrames);
            }
            for (int n = 0; n < Div; n++, fi++)
            {
                float l = L[fi], r = R[fi];
                preL[preWrite] = l; preR[preWrite] = r;
                float input = MathF.Max(MathF.Abs(l), MathF.Abs(r));
                float shaped = Saturate(input, k);
                float atten = input <= 0.0001f ? 1 : shaped / input;
                float attenDb = MathF.Max(2f, -Lin2Db(atten));
                float satRate = Db2Lin(attenDb / satReleaseFrames) - 1;
                float rate = atten > detectorAverage ? satRate : 1;
                detectorAverage += (atten - detectorAverage) * rate;
                detectorAverage = MathF.Min(1f, detectorAverage);
                if (!float.IsFinite(detectorAverage)) detectorAverage = 1;
                if (envRate < 1) compressorGain += (scaledDesired - compressorGain) * envRate;
                else { compressorGain *= envRate; compressorGain = MathF.Min(1f, compressorGain); }
                float gain = makeup * MathF.Sin(PiOver2 * compressorGain);
                L[fi] = preL[preRead] * gain; R[fi] = preR[preRead] * gain;
                preRead = (preRead + 1) & PreMask; preWrite = (preWrite + 1) & PreMask;
            }
        }
    }
}

public sealed class AudioDestination : AudioNode
{
    public AudioDestination(AudioContext c) : base(c) { persistent = true; }
    protected override void Process() => SumInputs();
}

public sealed class AudioContext
{
    public const int BLOCK = 128;
    public readonly object Lock = new();
    public readonly double sampleRate;
    public readonly AudioDestination destination;
    public string state = "running";
    long frames;
    internal long blockId;
    internal double blockTime;
    internal long blockFrame;
    internal readonly List<DelayNode> delays = new();
    readonly List<AudioScheduledSourceNode> sources = new();
    public AudioContext(double rate = 48000) { sampleRate = rate; destination = new AudioDestination(this); }
    public double currentTime => Interlocked.Read(ref frames) / sampleRate;
    public void resume() { state = "running"; }
    internal void Track(AudioScheduledSourceNode s) { lock (Lock) sources.Add(s); }
    public GainNode createGain() => new(this);
    public OscillatorNode createOscillator() => new(this);
    public AudioBufferSourceNode createBufferSource() => new(this);
    public BiquadFilterNode createBiquadFilter() => new(this);
    public WaveShaperNode createWaveShaper() => new(this);
    public StereoPannerNode createStereoPanner() => new(this);
    public ConvolverNode createConvolver() => new(this);
    public DelayNode createDelay(double max = 1) => new(this, max);
    public DynamicsCompressorNode createDynamicsCompressor() => new(this);
    public AudioBuffer createBuffer(int channels, int length, double rate) => new(channels, length, rate);

    /* render `n` interleaved stereo frames */
    readonly float[] pend = new float[BLOCK * 2]; int pendAt = BLOCK;
    public void Render(Span<float> output, int n)
    {
        int o = 0;
        while (o < n)
        {
            if (pendAt >= BLOCK)
            {
                lock (Lock) RenderBlock();
                pendAt = 0;
            }
            int take = Math.Min(BLOCK - pendAt, n - o);
            for (int i = 0; i < take; i++)
            {
                output[(o + i) * 2] = pend[(pendAt + i) * 2];
                output[(o + i) * 2 + 1] = pend[(pendAt + i) * 2 + 1];
            }
            o += take; pendAt += take;
        }
    }
    void RenderBlock()
    {
        blockId++;
        blockFrame = Interlocked.Read(ref frames);
        blockTime = blockFrame / sampleRate;
        destination.Pull();
        foreach (var d in delays) d.Write();
        for (int i = 0; i < BLOCK; i++)
        {
            pend[i * 2] = destination.L[i];
            pend[i * 2 + 1] = destination.R[i];
        }
        Interlocked.Add(ref frames, BLOCK);
        /* every few blocks: let go of what has finished */
        if ((blockId & 7) == 0) Collect();
    }
    void Collect()
    {
        double t = blockTime;
        for (int i = sources.Count - 1; i >= 0; i--)
        {
            var s = sources[i];
            if (!s.Finished) continue;
            sources.RemoveAt(i);
            Retire(s);
        }
    }
    /* a finished source leaves the graph, and so does every node downstream of it
       that nothing else is feeding any more */
    void Retire(AudioNode n)
    {
        var outs = n.outputs.ToList(); var pouts = n.paramOuts.ToList();
        foreach (var o in outs) o.inputs.Remove(n);
        foreach (var p in pouts) p.mods.Remove(n);
        n.outputs.Clear(); n.paramOuts.Clear();
        foreach (var o in outs) if (!o.persistent && o.inputs.Count == 0 && !(o is AudioScheduledSourceNode)) Retire(o);
    }
    /* how many sources are still alive (the test bench and the stats read it) */
    public int LiveSources { get { lock (Lock) return sources.Count; } }
}
