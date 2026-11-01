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
}

public abstract class AudioNode
{
    public readonly AudioContext context;
    internal readonly List<AudioNode> inputs = new();
    internal readonly List<AudioNode> outputs = new();
    internal readonly List<AudioParam> paramOuts = new();
    internal float[] L = new float[AudioContext.BLOCK], R = new float[AudioContext.BLOCK];
    internal long block = -1;
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
    /* the sum of the inputs into L/R */
    protected void SumInputs()
    {
        Array.Clear(L); Array.Clear(R);
        for (int k = 0; k < inputs.Count; k++)
        {
            var n = inputs[k]; n.Pull();
            var a = n.L; var b = n.R;
            for (int i = 0; i < AudioContext.BLOCK; i++) { L[i] += a[i]; R[i] += b[i]; }
        }
    }
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
        SumInputs();
        double t0 = context.blockTime, dt = 1.0 / context.sampleRate;
        bool mod = gain.mods.Count > 0;
        if (!gain.Automated && !mod)
        {
            float g = (float)gain.value;
            if (g != 1) for (int i = 0; i < AudioContext.BLOCK; i++) { L[i] *= g; R[i] *= g; }
            return;
        }
        for (int i = 0; i < AudioContext.BLOCK; i++)
        {
            double g = gain.At(t0 + i * dt) + (mod ? ModAt(gain, i) : 0);
            L[i] *= (float)g; R[i] *= (float)g;
        }
    }
}

public abstract class AudioScheduledSourceNode : AudioNode
{
    protected double startT = double.PositiveInfinity, stopT = double.PositiveInfinity;
    protected AudioScheduledSourceNode(AudioContext c) : base(c) { c.Track(this); }
    public virtual void start(double when = 0) { startT = Math.Max(when, 0); }
    public void stop(double when = 0) { stopT = when; }
    internal override bool Finished => context.blockTime > stopT + 0.01;
}

public sealed class OscillatorNode : AudioScheduledSourceNode
{
    public string type = "sine";
    public readonly AudioParam frequency, detune;
    double phase;
    public OscillatorNode(AudioContext c) : base(c) { frequency = new AudioParam(c, 440); detune = new AudioParam(c, 0); }
    static double Blep(double t, double dt)
    {
        if (t < dt) { t /= dt; return t + t - t * t - 1; }
        if (t > 1 - dt) { t = (t - 1) / dt; return t * t + t + t + 1; }
        return 0;
    }
    protected override void Process()
    {
        double t0 = context.blockTime, sdt = 1.0 / context.sampleRate;
        bool fm = frequency.mods.Count > 0;
        for (int i = 0; i < AudioContext.BLOCK; i++)
        {
            double t = t0 + i * sdt;
            if (t < startT || t >= stopT) { L[i] = R[i] = 0; continue; }
            double f = frequency.At(t) + (fm ? ModAt(frequency, i) : 0);
            double dtn = detune.value;
            if (dtn != 0 || detune.Automated) f *= Math.Pow(2, detune.At(t) / 1200);
            double inc = f * sdt;
            double v;
            switch (type)
            {
                case "square":
                    v = phase < 0.5 ? 1 : -1;
                    v += Blep(phase, Math.Abs(inc)); v -= Blep((phase + 0.5) % 1, Math.Abs(inc));
                    break;
                case "sawtooth":
                    v = 2 * phase - 1; v -= Blep(phase, Math.Abs(inc));
                    break;
                case "triangle":
                    v = phase < 0.5 ? 4 * phase - 1 : 3 - 4 * phase;
                    break;
                default:
                    v = Math.Sin(phase * 2 * Math.PI);
                    break;
            }
            L[i] = R[i] = (float)v;
            phase += inc; phase -= Math.Floor(phase);
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
    double pos, offset;
    public AudioBufferSourceNode(AudioContext c) : base(c) { }
    public void start(double when, double off) { base.start(when); offset = off; pos = -1; }
    public override void start(double when = 0) { start(when, 0); }
    protected override void Process()
    {
        double t0 = context.blockTime, sdt = 1.0 / context.sampleRate;
        var b = buffer;
        if (b == null) { Array.Clear(L); Array.Clear(R); return; }
        var d0 = b.getChannelData(0); var d1 = b.numberOfChannels > 1 ? b.getChannelData(1) : d0;
        for (int i = 0; i < AudioContext.BLOCK; i++)
        {
            double t = t0 + i * sdt;
            if (t < startT || t >= stopT) { L[i] = R[i] = 0; continue; }
            if (pos < 0) pos = offset * b.sampleRate;
            int k = (int)pos;
            if (k >= b.length) { if (loop) { pos %= b.length; k = (int)pos; } else { L[i] = R[i] = 0; continue; } }
            L[i] = d0[k]; R[i] = d1[k];
            pos += b.sampleRate * sdt;
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
        SumInputs();
        double t0 = context.blockTime, sdt = 1.0 / context.sampleRate;
        bool fm = frequency.mods.Count > 0;
        for (int i = 0; i < AudioContext.BLOCK; i++)
        {
            if ((i & 7) == 0)
            {
                double f = frequency.At(t0 + i * sdt) + (fm ? ModAt(frequency, i) : 0), q = Q.value;
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
        SumInputs();
        var cv = curve; if (cv == null || cv.Length < 2) return;
        int n = cv.Length;
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
        SumInputs();
        double x = (Math.Clamp(pan.value, -1, 1) + 1) / 2;
        float gl = (float)Math.Cos(x * Math.PI / 2), gr = (float)Math.Sin(x * Math.PI / 2);
        for (int i = 0; i < AudioContext.BLOCK; i++) { float m = (L[i] + R[i]) * 0.5f; L[i] = m * gl; R[i] = m * gr; }
    }
}

/* the room: Web Audio convolves with a generated impulse (decaying noise, about
   a second); this is the same idea as a Freeverb-style network, which costs a
   few dozen operations a sample instead of fifty thousand */
public sealed class ConvolverNode : AudioNode
{
    public AudioBuffer buffer;
    readonly float[][] combL, combR, apL, apR; readonly int[] ciL, ciR, aiL, aiR; readonly float[] fsL, fsR;
    static readonly int[] COMB = { 1116, 1188, 1277, 1356, 1422, 1491, 1557, 1617 };
    static readonly int[] AP = { 556, 441, 341, 225 };
    const float FEED = 0.78f, DAMP = 0.32f, SPREAD = 23;
    public ConvolverNode(AudioContext c) : base(c)
    {
        double k = c.sampleRate / 44100.0;
        combL = COMB.Select(n => new float[(int)(n * k)]).ToArray(); combR = COMB.Select(n => new float[(int)((n + SPREAD) * k)]).ToArray();
        apL = AP.Select(n => new float[(int)(n * k)]).ToArray(); apR = AP.Select(n => new float[(int)((n + SPREAD) * k)]).ToArray();
        ciL = new int[8]; ciR = new int[8]; aiL = new int[4]; aiR = new int[4]; fsL = new float[8]; fsR = new float[8];
        persistent = true;
    }
    protected override void Process()
    {
        SumInputs();
        for (int i = 0; i < AudioContext.BLOCK; i++)
        {
            float inp = (L[i] + R[i]) * 0.015f, ol = 0, or = 0;
            for (int c = 0; c < 8; c++)
            {
                var b = combL[c]; int j = ciL[c]; float y = b[j]; fsL[c] = y * (1 - DAMP) + fsL[c] * DAMP; b[j] = inp + fsL[c] * FEED; ciL[c] = (j + 1) % b.Length; ol += y;
                b = combR[c]; j = ciR[c]; y = b[j]; fsR[c] = y * (1 - DAMP) + fsR[c] * DAMP; b[j] = inp + fsR[c] * FEED; ciR[c] = (j + 1) % b.Length; or += y;
            }
            for (int a = 0; a < 4; a++)
            {
                var b = apL[a]; int j = aiL[a]; float bo = b[j]; b[j] = ol + bo * 0.5f; ol = bo - ol; aiL[a] = (j + 1) % b.Length;
                b = apR[a]; j = aiR[a]; bo = b[j]; b[j] = or + bo * 0.5f; or = bo - or; aiR[a] = (j + 1) % b.Length;
            }
            L[i] = ol * 3.2f; R[i] = or * 3.2f;
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

public sealed class DynamicsCompressorNode : AudioNode
{
    public readonly AudioParam threshold, knee, ratio, attack, release;
    double env = 0;
    public DynamicsCompressorNode(AudioContext c) : base(c)
    {
        threshold = new AudioParam(c, -24); knee = new AudioParam(c, 30); ratio = new AudioParam(c, 12);
        attack = new AudioParam(c, 0.003); release = new AudioParam(c, 0.25); persistent = true;
    }
    protected override void Process()
    {
        SumInputs();
        double sr = context.sampleRate, th = threshold.value, kn = knee.value, ra = ratio.value;
        double ca = Math.Exp(-1 / (Math.Max(1e-4, attack.value) * sr)), cr = Math.Exp(-1 / (Math.Max(1e-4, release.value) * sr));
        for (int i = 0; i < AudioContext.BLOCK; i++)
        {
            double x = Math.Max(Math.Abs(L[i]), Math.Abs(R[i]));
            env = x > env ? ca * env + (1 - ca) * x : cr * env + (1 - cr) * x;
            double db = 20 * Math.Log10(Math.Max(1e-9, env)), over = db - th, gr = 0;
            if (over > kn / 2) gr = over - over / ra;
            else if (over > -kn / 2) { double z = over + kn / 2; gr = (1 / ra - 1) * z * z / (2 * kn) * -1; }
            float g = (float)Math.Pow(10, -gr / 20);
            L[i] *= g; R[i] *= g;
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
        blockTime = (double)Interlocked.Read(ref frames) / sampleRate;
        destination.Pull();
        foreach (var d in delays) d.Write();
        for (int i = 0; i < BLOCK; i++)
        {
            pend[i * 2] = Math.Clamp(destination.L[i], -1f, 1f);
            pend[i * 2 + 1] = Math.Clamp(destination.R[i], -1f, 1f);
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
