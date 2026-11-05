namespace Brickblade;

/* --audioprobe OUTDIR: the single-node graphs of tests/AudioTest/probes.js,
   built the same way on the work-alike, straight to the destination. */
public static class AudioProbes
{
    const int Rate = 48000; const double Secs = 0.5;
    static AudioBuffer Ramp(AudioContext c, int n) { var b = c.createBuffer(1, n, Rate); var d = b.getChannelData(0); for (int i = 0; i < n; i++) d[i] = i / (float)n; return b; }
    static OscillatorNode Osc(AudioContext c, string type, double f) { var o = c.createOscillator(); o.type = type; o.frequency.value = f; return o; }
    static readonly Dictionary<string, Action<AudioContext>> P = new()
    {
        ["osc_frac"] = c => { var o = Osc(c, "sine", 1000); o.connect(c.destination); o.start(0.1 + 0.3 / 48000); },
        ["osc_near"] = c => { var o = Osc(c, "sine", 1000); o.connect(c.destination); o.start(0.1 + 0.07); },
        ["buf_near"] = c => { var s = c.createBufferSource(); s.buffer = Ramp(c, 1000); s.connect(c.destination); s.start(0.1 + 0.07); },
        ["tri220"] = c => { var o = Osc(c, "triangle", 220); o.connect(c.destination); o.start(0.1); },
        ["saw220"] = c => { var o = Osc(c, "sawtooth", 220); o.connect(c.destination); o.start(0.1); },
        ["sq220"] = c => { var o = Osc(c, "square", 220); o.connect(c.destination); o.start(0.1); },
        ["saw2000"] = c => { var o = Osc(c, "sawtooth", 2000); o.connect(c.destination); o.start(0.1); },
        ["tri1500"] = c => { var o = Osc(c, "triangle", 1500); o.connect(c.destination); o.start(0.1); },
        ["saw200"] = c => { var o = Osc(c, "sawtooth", 200); o.connect(c.destination); o.start(0.1); },
        ["saw200a"] = c => { var o = Osc(c, "sawtooth", 440); o.frequency.setValueAtTime(200, 0.1); o.connect(c.destination); o.start(0.1); },
        ["saw200l"] = c => { var o = Osc(c, "sawtooth", 440); o.frequency.setValueAtTime(200, 0.1); o.frequency.linearRampToValueAtTime(201, 0.4); o.connect(c.destination); o.start(0.1); },
        ["saw_late"] = c => { var o = Osc(c, "sawtooth", 440); o.frequency.setValueAtTime(200, 0.1); o.connect(c.destination); o.start(0.05); },
        ["lp_start"] = c => { var o = Osc(c, "sawtooth", 110); var f = c.createBiquadFilter(); f.type = "lowpass";
            f.frequency.setValueAtTime(3000, 0.1); f.Q.value = 0.7; o.connect(f); f.connect(c.destination); o.start(0.1); },
        ["sawsweep"] = c => { var o = Osc(c, "sawtooth", 440); o.frequency.setValueAtTime(200, 0.1); o.frequency.exponentialRampToValueAtTime(3000, 0.4); o.connect(c.destination); o.start(0.1); },
        ["buf_off"] = c => { var s = c.createBufferSource(); s.buffer = Ramp(c, 1000); s.connect(c.destination); s.start(0.1, 10.4 / 48000); },
        ["buf_off6"] = c => { var s = c.createBufferSource(); s.buffer = Ramp(c, 1000); s.connect(c.destination); s.start(0.1, 10.6 / 48000); },
        ["buf_frac"] = c => { var s = c.createBufferSource(); s.buffer = Ramp(c, 1000); s.connect(c.destination); s.start(0.1 + 0.3 / 48000); },
        ["buf_loop"] = c => { var s = c.createBufferSource(); s.buffer = Ramp(c, 100); s.loop = true; s.connect(c.destination); s.start(0.1, 90.5 / 48000); },
        ["shaper"] = c => { var o = Osc(c, "sine", 300); var g = c.createGain(); g.gain.value = 0.9; var w = c.createWaveShaper();
            var cv = new float[1024]; for (int i = 0; i < 1024; i++) { double x = i / 512.0 - 1; cv[i] = (float)Math.Tanh(2.2 * x); } w.curve = cv;
            o.connect(g); g.connect(w); w.connect(c.destination); o.start(0.1); },
        ["lp_sweep"] = c => { var o = Osc(c, "sawtooth", 110); var f = c.createBiquadFilter(); f.type = "lowpass";
            f.frequency.setValueAtTime(6000, 0.1); f.frequency.exponentialRampToValueAtTime(160, 0.3); f.Q.value = 0.7; o.connect(f); f.connect(c.destination); o.start(0.1); },
        ["bp_sweep"] = c => { var o = Osc(c, "sawtooth", 110); var f = c.createBiquadFilter(); f.type = "bandpass";
            f.frequency.setValueAtTime(2000, 0.1); f.frequency.exponentialRampToValueAtTime(300, 0.3); f.Q.value = 1.4; o.connect(f); f.connect(c.destination); o.start(0.1); },
        ["env_frac"] = c => { var o = Osc(c, "sine", 500); var g = c.createGain(); double t = 0.1 + 0.37 / 48000;
            g.gain.setValueAtTime(0.0001, t); g.gain.exponentialRampToValueAtTime(0.8, t + 0.002); g.gain.exponentialRampToValueAtTime(0.0001, t + 0.05);
            o.connect(g); g.connect(c.destination); o.start(t); o.stop(t + 0.06); },
        ["lin_frac"] = c => { var o = Osc(c, "sine", 500); var g = c.createGain(); double t = 0.1 + 0.37 / 48000;
            g.gain.setValueAtTime(0, t); g.gain.linearRampToValueAtTime(0.8, t + 0.004); g.gain.exponentialRampToValueAtTime(0.0001, t + 0.05);
            o.connect(g); g.connect(c.destination); o.start(t); o.stop(t + 0.06); },
        ["stop_frac"] = c => { var o = Osc(c, "sine", 500); o.connect(c.destination); o.start(0.1); o.stop(0.1 + 100.6 / 48000); },
        ["fm"] = c => { var o = Osc(c, "sine", 400); var m = Osc(c, "sine", 30); var mg = c.createGain(); mg.gain.value = 150;
            m.connect(mg); mg.connect(o.frequency); o.connect(c.destination); o.start(0.1); m.start(0.1); },
        ["detune"] = c => { var o = Osc(c, "sawtooth", 100); o.detune.setValueAtTime(0, 0.1); o.detune.linearRampToValueAtTime(1200, 0.4); o.connect(c.destination); o.start(0.1); },
        ["pan"] = c => { var o = Osc(c, "sine", 300); var p = c.createStereoPanner(); p.pan.value = 0.6; o.connect(p); p.connect(c.destination); o.start(0.1); },
        ["delay"] = c => { var o = Osc(c, "sine", 300); var g = c.createGain(); g.gain.setValueAtTime(1, 0.1); g.gain.setValueAtTime(0, 0.12);
            var d = c.createDelay(1); d.delayTime.value = 0.245; var fb = c.createGain(); fb.gain.value = 0.3; o.connect(g); g.connect(d); d.connect(fb); fb.connect(d); d.connect(c.destination); g.connect(c.destination); o.start(0.1); },
    };
    public static int Run(string outDir)
    {
        Directory.CreateDirectory(outDir);
        foreach (var (name, build) in P)
        {
            var c = new AudioContext(Rate);
            build(c);
            int n = (int)Math.Round(Secs * Rate); var buf = new float[n * 2];
            c.Render(buf, n);
            var bytes = new byte[buf.Length * 4]; Buffer.BlockCopy(buf, 0, bytes, 0, bytes.Length);
            File.WriteAllBytes(Path.Combine(outDir, name + ".f32"), bytes);
        }
        Console.WriteLine($"audioprobe: {P.Count}");
        return 0;
    }
}
