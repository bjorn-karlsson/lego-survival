namespace Brickblade;

/* =============================== AUDIO (JS 3553–3969) =============================== */
public sealed class AudioState
{
    public AudioContext ctx; public GainNode master, sfx, mus; public bool muted;
    public bool musicOn = true, sfxOn = true; public AudioBuffer noiseBuf; public DelayNode delay;
    public double musVol = 1, sfxVol = 1;
    public DynamicsCompressorNode comp; public BiquadFilterNode sfxLid;
}
public sealed class SfxLayer { public GainNode verbIn; public AudioBuffer noise; public float[] curve; }
public sealed class Track
{
    public string id, name, lead, bassW, kind; public double bpm;
    public int[] bass, leadA, leadB, pad, kick, snare; public int hat; public double hatHz; public bool sparkle;
}
public sealed class MusicState { public int step; public double next; public int loop; public Track track, pending; public int bars; public bool timer; }
public sealed class Toast { public string name; public double life; }

public static partial class G
{
    public static readonly AudioState A = new();
    public const double MUS_GAIN = 0.17, SFX_GAIN = 0.8, MASTER_GAIN = 0.9;
    public static void audioLoad()
    {
        try
        {
            A.musicOn = Store.getItem("bb_music") != "0";
            A.sfxOn = Store.getItem("bb_sfx") != "0";
            double mv = Js.ParseFloat(Store.getItem("bb_musvol")), sv = Js.ParseFloat(Store.getItem("bb_sfxvol"));
            if (Js.IsFinite(mv)) A.musVol = clamp(mv, 0, 1);
            if (Js.IsFinite(sv)) A.sfxVol = clamp(sv, 0, 1);
        }
        catch { }
        refreshAudioBtns();
    }
    public static void audioSave()
    {
        try
        {
            Store.setItem("bb_music", A.musicOn ? "1" : "0");
            Store.setItem("bb_sfx", A.sfxOn ? "1" : "0");
            Store.setItem("bb_musvol", N(A.musVol));
            Store.setItem("bb_sfxvol", N(A.sfxVol));
        }
        catch { }
    }
    public static bool musicLive() => A.ctx != null && !A.muted && A.musicOn;
    public static bool destOff(AudioNode dest) => (dest == A.mus || dest == A.delay) ? !A.musicOn : !A.sfxOn;
    public static void audioGains()
    {
        if (A.master != null) A.master.gain.value = A.muted ? 0 : MASTER_GAIN;
        if (A.mus != null) A.mus.gain.value = musicLive() ? MUS_GAIN * A.musVol : 0;
        if (A.sfx != null) A.sfx.gain.value = (!A.muted && A.sfxOn) ? SFX_GAIN * A.sfxVol : 0;
    }
    public static void audioApply()
    {
        audioGains();
        /* the scheduler stops while the music is off; catch it up so it comes back on the beat */
        if (A.ctx != null && musicLive() && music.next < A.ctx.currentTime)
            music.next = A.ctx.currentTime + 0.05;
        refreshAudioBtns();
        audioSave();
    }
    public static bool toggleMusic() { A.musicOn = !A.musicOn; audioApply(); return A.musicOn; }
    public static bool toggleSfx() { A.sfxOn = !A.sfxOn; audioApply(); return A.sfxOn; }
    public static bool toggleMute() { A.muted = !A.muted; audioApply(); return !A.muted; }
    public static void refreshAudioBtns()
    {
        /* the options screen and the title's two buttons read A directly each frame */
        if (optOn) optRender();
    }
    public static void audioInit()
    {
        if (A.ctx != null) { if (A.ctx.state == "suspended") A.ctx.resume(); return; }
        if (!SoundOut.Available) return;
        try
        {
            A.ctx = new AudioContext(SoundOut.Rate);
            A.master = A.ctx.createGain(); A.master.gain.value = MASTER_GAIN; A.master.persistent = true;
            A.comp = A.ctx.createDynamicsCompressor();
            A.comp.threshold.value = -20; A.comp.knee.value = 24;
            A.comp.ratio.value = 7; A.comp.attack.value = 0.004; A.comp.release.value = 0.16;
            A.master.connect(A.comp); A.comp.connect(A.ctx.destination);
            A.sfxLid = A.ctx.createBiquadFilter(); A.sfxLid.persistent = true;
            A.sfxLid.type = "lowpass"; A.sfxLid.frequency.value = 11000; A.sfxLid.Q.value = 0.4;
            A.sfxLid.connect(A.master);
            A.sfx = A.ctx.createGain(); A.sfx.gain.value = SFX_GAIN * A.sfxVol; A.sfx.persistent = true; A.sfx.connect(A.sfxLid);
            sfxLayerInit();
            A.mus = A.ctx.createGain(); A.mus.gain.value = MUS_GAIN * A.musVol; A.mus.persistent = true; A.mus.connect(A.master);
            var nb = A.ctx.createBuffer(1, (int)A.ctx.sampleRate, A.ctx.sampleRate);
            var d = nb.getChannelData(0); for (int i = 0; i < d.Length; i++) d[i] = (float)(Js.R() * 2 - 1);
            A.noiseBuf = nb;
            A.delay = A.ctx.createDelay(1); A.delay.delayTime.value = 0.245;
            var fb = A.ctx.createGain(); fb.gain.value = 0.3; fb.persistent = true;
            A.delay.connect(fb); fb.connect(A.delay); A.delay.connect(A.mus);
            SoundOut.Attach(A.ctx);
            startMusic();
            audioApply();
        }
        catch (Exception err) { Console.Error.WriteLine("audio unavailable: " + err.Message); }
    }
    public static void tone(string type, double f0, double f1, double dur, double vol, AudioNode dest = null, double when = 0)
    {
        dest ??= A.sfx;
        if (A.ctx == null || A.muted || destOff(dest)) return;
        double t = A.ctx.currentTime + when;
        var o = A.ctx.createOscillator(); var g = A.ctx.createGain();
        o.type = type; o.frequency.setValueAtTime(f0, t);
        if (Js.T(f1)) o.frequency.exponentialRampToValueAtTime(Math.Max(1, f1), t + dur);
        g.gain.setValueAtTime(0, t); g.gain.linearRampToValueAtTime(vol, t + 0.004);
        g.gain.exponentialRampToValueAtTime(0.0001, t + dur);
        o.connect(g); g.connect(dest); o.start(t); o.stop(t + dur + 0.02);
    }
    public static void noiseHit(double dur, double vol, double fc, double fc1, string type = "bandpass", double when = 0, AudioNode dest = null)
    {
        dest ??= A.sfx;
        if (A.ctx == null || A.muted || destOff(dest)) return;
        double t = A.ctx.currentTime + when;
        var s = A.ctx.createBufferSource(); s.buffer = A.noiseBuf; s.loop = true;
        var f = A.ctx.createBiquadFilter(); f.type = type; f.frequency.setValueAtTime(fc, t);
        if (Js.T(fc1)) f.frequency.exponentialRampToValueAtTime(fc1, t + dur);
        f.Q.value = 1.2;
        var g = A.ctx.createGain(); g.gain.setValueAtTime(vol, t);
        g.gain.exponentialRampToValueAtTime(0.0001, t + dur);
        s.connect(f); f.connect(g); g.connect(dest); s.start(t); s.stop(t + dur + 0.02);
    }
    /* ============================ THE SOUND, LAYERED ============================ */
    public static readonly SfxLayer SFXL = new();
    public static void sfxLayerInit()
    {
        var ctx = A.ctx;
        var verb = ctx.createConvolver();
        int len = (int)Math.Floor(ctx.sampleRate * 1.1);
        var ir = ctx.createBuffer(2, len, ctx.sampleRate);
        for (int c = 0; c < 2; c++) { var d = ir.getChannelData(c); for (int i = 0; i < len; i++) d[i] = (float)((Js.R() * 2 - 1) * Math.Pow(1 - (double)i / len, 3.2)); }
        verb.buffer = ir;
        SFXL.verbIn = ctx.createGain(); SFXL.verbIn.gain.value = 0.22; SFXL.verbIn.persistent = true;
        SFXL.verbIn.connect(verb); verb.connect(A.sfx);
        var nb = ctx.createBuffer(1, (int)ctx.sampleRate * 2, ctx.sampleRate); var dd = nb.getChannelData(0);
        for (int i = 0; i < dd.Length; i++) dd[i] = (float)(Js.R() * 2 - 1);
        SFXL.noise = nb;
        SFXL.curve = new float[1024];
        for (int i = 0; i < 1024; i++) { double x = i / 512.0 - 1; SFXL.curve[i] = (float)Math.Tanh(2.2 * x); }
    }
    public static bool sfxLive() => A.ctx != null && !A.muted && A.sfxOn && SFXL.noise != null && !previewing;
    static double? sfxPanNext = null;
    /* the next sound called after sfxAt(x) sits at that point of the screen */
    public static Sfx sfxAt(double x)
    {
        if (cw > 0) sfxPanNext = clamp((x - cam.x) * Js.Or(zoom, 1) / (cw / 2), -1, 1) * 0.8;
        return SFX;
    }
    public static void sfxPanClear() => sfxPanNext = null;
    public static double sfxPan(double spread = 0)
    {
        if (sfxPanNext != null) { double p = sfxPanNext.Value; sfxPanNext = null; return p; }
        double s = Js.Or(spread, 0.3);
        return rnd(-s, s);
    }
    public static GainNode voice(double pan, double wet = double.NaN)
    {
        var ctx = A.ctx; var g = ctx.createGain(); var p = ctx.createStereoPanner();
        p.pan.value = clamp(pan, -1, 1);
        g.connect(p); p.connect(A.sfx);
        var s = ctx.createGain(); s.gain.value = double.IsNaN(wet) ? 0.35 : wet;
        g.connect(s); s.connect(SFXL.verbIn);
        return g;
    }
    public static void lenv(GainNode g, double t, double a, double peak, double dec)
    {
        g.gain.setValueAtTime(0.0001, t); g.gain.exponentialRampToValueAtTime(Math.Max(0.0002, peak), t + a);
        g.gain.exponentialRampToValueAtTime(0.0001, t + a + dec);
    }
    public static OscillatorNode losc(AudioNode dest, string type, double f0, double f1, double t, double a, double peak, double dec)
    {
        var ctx = A.ctx; var o = ctx.createOscillator(); var g = ctx.createGain();
        o.type = type; o.frequency.setValueAtTime(f0, t);
        if (Js.T(f1)) o.frequency.exponentialRampToValueAtTime(Math.Max(20, f1), t + a + dec);
        lenv(g, t, a, peak, dec); o.connect(g); g.connect(dest); o.start(t); o.stop(t + a + dec + 0.05);
        return o;
    }
    public static void lnoise(AudioNode dest, double t, double a, double peak, double dec, string ftype, double f0, double f1, double q)
    {
        var ctx = A.ctx; var s = ctx.createBufferSource(); s.buffer = SFXL.noise;
        var f = ctx.createBiquadFilter(); f.type = ftype ?? "bandpass"; f.frequency.setValueAtTime(Js.Or(f0, 1000), t);
        if (Js.T(f1)) f.frequency.exponentialRampToValueAtTime(f1, t + a + dec);
        f.Q.value = Js.Or(q, 1);
        var g = ctx.createGain(); lenv(g, t, a, peak, dec);
        s.connect(f); f.connect(g); g.connect(dest);
        s.start(t, Js.R() * 1.5); s.stop(t + a + dec + 0.05);
    }
    public static readonly double[][] PLASTIC = { new double[] { 1, 1, 1 }, new double[] { 2.76, .5, .7 }, new double[] { 5.40, .28, .45 }, new double[] { 8.93, .15, .3 } };
    public static readonly double[][] METAL = { new double[] { 1, 1, 1 }, new double[] { 2.32, .7, .9 }, new double[] { 4.25, .45, .8 }, new double[] { 6.63, .3, .7 }, new double[] { 9.38, .18, .6 } };
    public static void modal(AudioNode dest, double t, double f, double[][] modes, double peak, double dec)
    {
        foreach (var m in modes) losc(dest, "sine", f * m[0], 0, t, 0.001, peak * m[1], dec * m[2]);
    }
    public static void lclick(AudioNode dest, double t, double peak, double f = 0) => lnoise(dest, t, 0.0005, peak, 0.006, "highpass", Js.Or(f, 2500), 0, 0.7);
    public static double jit(double v, double k) => v * (1 + rnd(-k, k));
    public static void whooshAt(AudioNode v, double t, double f0, double up, double dur, double peak, double q = 0)
    {
        var ctx = A.ctx; var s = ctx.createBufferSource(); s.buffer = SFXL.noise;
        var f = ctx.createBiquadFilter(); f.type = "bandpass"; f.Q.value = Js.Or(q, 2.2);
        f.frequency.setValueAtTime(f0, t); f.frequency.exponentialRampToValueAtTime(f0 * up, t + dur * 0.4); f.frequency.exponentialRampToValueAtTime(f0 * 1.1, t + dur);
        var g = ctx.createGain(); g.gain.setValueAtTime(0.0001, t); g.gain.exponentialRampToValueAtTime(peak, t + dur * 0.4); g.gain.exponentialRampToValueAtTime(0.0001, t + dur);
        s.connect(f); f.connect(g); g.connect(v); s.start(t, Js.R()); s.stop(t + dur + 0.03);
    }
    /* a repeat of the same sound inside a short window is dropped */
    public static readonly Dictionary<string, double> sfxLast = new();
    public static bool every(string key, double gap)
    {
        if (!sfxLive()) return false;
        double t = A.ctx.currentTime;
        if (sfxLast.TryGetValue(key, out var last) && t - last < gap) return false;
        sfxLast[key] = t;
        return true;
    }
    public static void sfxPlay(Action<double> fn)
    {
        if (!sfxLive()) { sfxPanNext = null; return; }
        try { fn(A.ctx.currentTime); } catch { }
    }
    public static readonly Sfx SFX = new();
    public static double mf(double m) => 440 * Math.Pow(2, (m - 69) / 12);
    /* six tracks: a bass line, two alternating lead phrases, a pad, and drums */
    public static readonly Track[] TRACKS =
    {
        new Track { id = "march", name = "BRICKFIELD MARCH", bpm = 132, lead = "square", bassW = "triangle", kind = "field",
            bass = new[] { 36,0,36,43, 41,0,41,48, 43,0,43,50, 36,43,36,0 },
            leadA = new[] { 72,76,79,76, 81,0,79,76, 74,77,81,77, 79,0,76,0 },
            leadB = new[] { 84,79,76,79, 77,74,72,74, 76,79,84,79, 76,0,72,0 },
            pad = new[] { 48,0,0,0, 53,0,0,0, 55,0,0,0, 48,0,0,0 },
            kick = new[] { 0, 8 }, snare = new[] { 4, 12 }, hat = 2, hatHz = 7000 },
        new Track { id = "quarry", name = "QUARRY STOMP", bpm = 122, lead = "sawtooth", bassW = "triangle", kind = "field",
            bass = new[] { 38,38,0,45, 43,0,43,0, 41,41,0,48, 45,0,43,0 },
            leadA = new[] { 74,77,81,77, 79,0,77,74, 72,74,77,74, 76,0,74,0 },
            leadB = new[] { 86,81,77,81, 79,76,74,76, 77,81,86,81, 77,0,74,0 },
            pad = new[] { 50,0,0,0, 55,0,0,0, 53,0,0,0, 57,0,0,0 },
            kick = new[] { 0, 3, 8, 11 }, snare = new[] { 4, 12 }, hat = 4, hatHz = 6000 },
        new Track { id = "caves", name = "CRYSTAL CAVERN", bpm = 110, lead = "triangle", bassW = "sine", kind = "field",
            bass = new[] { 33,0,0,40, 0,0,33,0, 36,0,0,43, 0,0,40,0 },
            leadA = new[] { 69,0,72,76, 0,74,72,0, 67,0,71,74, 0,72,0,0 },
            leadB = new[] { 81,0,76,72, 0,74,76,0, 79,0,74,71, 0,72,0,0 },
            pad = new[] { 45,0,0,0, 0,0,0,0, 48,0,0,0, 0,0,0,0 },
            kick = new[] { 0, 10 }, snare = new[] { 8 }, hat = 0, hatHz = 9000, sparkle = true },
        new Track { id = "foundry", name = "THE DEEP FOUNDRY", bpm = 116, lead = "triangle", bassW = "sawtooth", kind = "field",
            bass = new[] { 38,0,38,45, 36,0,36,43, 33,0,33,40, 34,0,36,37 },
            leadA = new[] { 74,0,77,0, 76,74,0,72, 69,0,72,0, 74,0,0,0 },
            leadB = new[] { 81,0,79,77, 76,0,74,0, 72,74,76,74, 72,0,69,0 },
            pad = new[] { 50,0,0,0, 48,0,0,0, 45,0,0,0, 46,0,0,0 },
            kick = new[] { 0, 6, 10 }, snare = new[] { 4, 12 }, hat = 2, hatHz = 6200 },
        new Track { id = "tyrant", name = "THE BRICK TYRANT", bpm = 138, lead = "sawtooth", bassW = "sawtooth", kind = "boss",
            bass = new[] { 36,36,0,36, 39,0,36,0, 34,34,0,34, 31,0,34,36 },
            leadA = new[] { 72,75,79,75, 80,0,79,75, 74,77,80,77, 79,0,75,0 },
            leadB = new[] { 84,79,75,79, 80,75,72,75, 77,80,84,80, 79,0,75,0 },
            pad = new[] { 48,0,0,0, 51,0,0,0, 46,0,0,0, 43,0,0,0 },
            kick = new[] { 0, 2, 8, 10 }, snare = new[] { 4, 12 }, hat = 1, hatHz = 5200 },
        new Track { id = "colossus", name = "COLOSSUS RISING", bpm = 152, lead = "sawtooth", bassW = "sawtooth", kind = "super",
            bass = new[] { 36,36,36,36, 38,38,38,38, 39,39,39,39, 41,41,43,44 },
            leadA = new[] { 79,84,87,84, 86,0,84,79, 77,82,86,82, 84,0,79,0 },
            leadB = new[] { 91,87,84,87, 86,82,79,82, 84,87,91,87, 86,0,84,0 },
            pad = new[] { 48,0,55,0, 50,0,57,0, 51,0,58,0, 53,0,56,0 },
            kick = new[] { 0, 2, 4, 6, 8, 10, 12, 14 }, snare = new[] { 4, 12 }, hat = 1, hatHz = 4800 },
    };
    public static readonly MusicState music = new() { track = TRACKS[0] };
    public static Toast musicToast = null;
    public static Track trackForWave(double n)
    {
        string id = n % 10 == 0 ? "colossus" : n % 5 == 0 ? "tyrant" : n <= 3 ? "caves" : n <= 8 ? "quarry" : n <= 14 ? "march" : "foundry";
        return TRACKS.FirstOrDefault(t => t.id == id) ?? TRACKS[0];
    }
    public static void setTrack(Track t, bool announce)
    {
        if (t == null || t == music.track) return;
        music.track = t; music.pending = t; music.bars = 0; music.loop = 0;
        if (announce && musicLive()) musicToast = new Toast { name = t.name, life = 3.2 };
    }
    public static void queueTrackForWave(double n) { music.pending = trackForWave(n); }
    public static void startMusic()
    {
        if (A.ctx == null) return;
        music.next = A.ctx.currentTime + 0.1; music.step = 0; music.loop = 0; music.bars = 0;
        music.track = trackForWave(Math.Max(1, wave.n));
        music.pending = music.track;
        if (musicLive()) musicToast = new Toast { name = music.track.name, life = 3.2 };
        music.timer = true;
    }
    /* the JS runs this every 60ms on a timer; the main loop calls it */
    static double musicTimerAcc;
    public static void musicTimer(double dt)
    {
        if (!music.timer) return;
        musicTimerAcc += dt; if (musicTimerAcc < 0.06) return; musicTimerAcc = 0;
        if (!musicLive()) return;
        double spb = 60 / music.track.bpm / 2;
        while (music.next < A.ctx.currentTime + 0.18)
        {
            scheduleStep(music.step, music.next);
            music.step++;
            if (music.step >= 16)
            {
                music.step = 0; music.loop++; music.bars++;
                if (music.pending != null && music.pending != music.track) setTrack(music.pending, true);
            }
            music.next += spb;
        }
    }
    public static void scheduleStep(int s, double t)
    {
        double when = Math.Max(0, t - A.ctx.currentTime);
        var T = music.track;
        int b = T.bass[s];
        if (b != 0) tone(T.bassW, mf(b), mf(b) * 0.99, 0.15, 0.55, A.mus, when);
        var L = ((music.loop >> 1) % 2) != 0 ? T.leadB : T.leadA;
        int l = L[s];
        if (l != 0)
        {
            tone(T.lead, mf(l) * 1.02, mf(l) * 0.985, 0.12, 0.34, A.mus, when);
            if (s % 4 == 0) tone("triangle", mf(l + 12), 0, 0.09, 0.12, A.mus, when + 0.02);
        }
        int pd = T.pad != null ? T.pad[s] : 0;
        if (pd != 0) tone("triangle", mf(pd), mf(pd), 0.5, 0.12, A.mus, when);
        if (T.sparkle && s % 4 == 2) tone("sine", mf((l != 0 ? l : 72) + 19), 0, 0.18, 0.07, A.mus, when + 0.03);
        if (T.kick.Contains(s)) tone("sine", 140, 50, 0.1, 0.5, A.mus, when);
        if (T.snare.Contains(s)) noiseHit(0.05, 0.3, 1900, 900, "bandpass", when, A.mus);
        if (T.hat != 0 && s % T.hat == 0) noiseHit(0.03, 0.13, T.hatHz, 0, "highpass", when, A.mus);
    }
    public static bool A_bossFight() => enemies.Any(e => e.type == "boss");
}

/* every sound effect, by name: SFX.hit(), SFX.brick(n) ... */
public sealed class Sfx
{
    static AudioContext C => G.A.ctx;
    static void P(Action<double> f) => G.sfxPlay(f);
    static GainNode V(double pan, double wet) => G.voice(pan, wet);
    static double J(double v, double k) => G.jit(v, k);
    static double R(double a, double b) => G.rnd(a, b);
    static bool Ev(string k, double gap) { if (!G.every(k, gap)) { G.sfxPanClear(); return false; } return true; }
    public void swing() => P(t => G.whooshAt(V(G.sfxPan(), 0.15), t, J(700, .1), 3.2, 0.19, 0.5));
    public void hit()
    {
        if (!Ev("hit", 0.035)) return;
        P(t => { var v = V(G.sfxPan(), 0.25);
            G.lclick(v, t, 0.5, 3000);
            G.losc(v, "sine", J(150, .08), 55, t, 0.002, 0.85, 0.11);
            G.lnoise(v, t, 0.001, 0.33, 0.05, "bandpass", J(1400, .15), 700, 1.4);
            G.modal(v, t + 0.002, J(420, .12), G.PLASTIC, 0.11, 0.06); });
    }
    public void brick(double n = 5) => P(t0 =>
    {
        double pan = G.sfxPan(); int k = (int)Math.Min(8, n + 1);
        for (int i = 0; i < k; i++) { var v = V(pan + R(-.3, .3), 0.3); double t = t0 + (i != 0 ? R(0.01, 0.16) : 0);
            G.lclick(v, t, 0.22, 3500); G.modal(v, t, R(900, 2600), G.PLASTIC, R(0.06, 0.14), R(0.03, 0.07)); }
        G.losc(V(pan, 0.2), "sine", 180, 70, t0, 0.002, 0.35, 0.08);
    });
    public void stud(double k = 0)
    {
        if (!G.every("stud", 0.045)) return;
        P(t => { var v = V(G.sfxPan(0.2), 0.3); double f = 1600 * Math.Pow(2, Math.Min(k, 12) / 12);
            G.lclick(v, t, 0.13, 5000); G.modal(v, t, J(f, .02), G.PLASTIC, 0.12, 0.09);
            G.losc(v, "sine", f * 1.5, 0, t + 0.01, 0.004, 0.08, 0.22); });
    }
    public void heart() => P(t =>
    {
        var v = V(0, 0.5);
        foreach (var (f, d) in new[] { (523.25, 0.0), (783.99, .09), (1046.5, .18) })
        { G.losc(v, "sine", f, 0, t + d, 0.015, 0.2, 0.35); G.losc(v, "triangle", f * 2.002, 0, t + d, 0.015, 0.035, 0.2); }
    });
    public void magnet() => P(t =>
    {
        var v = V(0, 0.45);
        G.losc(v, "sine", 300, 1400, t, 0.02, 0.22, 0.4); G.losc(v, "sine", 452, 2100, t + 0.05, 0.02, 0.12, 0.38);
        for (int i = 0; i < 5; i++) G.modal(v, t + 0.08 + i * 0.06, 1400 + i * 260, G.PLASTIC, 0.05, 0.06);
    });
    public void fortune() => P(t =>
    {
        var v = V(0, 0.5); var fs = new[] { 1318.5, 1568, 1975.5, 2637 };
        for (int i = 0; i < fs.Length; i++) { G.lclick(v, t + i * 0.07, 0.08, 6000); G.modal(v, t + i * 0.07, fs[i], G.METAL, 0.06, 0.25); }
    });
    public void dash() => P(t => G.whooshAt(V(0, 0.12), t, 900, 2.4, 0.16, 0.42, 1.6));
    public void hurt() => P(t =>
    {
        var v = V(0, 0.2); var sh = C.createWaveShaper(); sh.curve = G.SFXL.curve; sh.connect(v);
        G.losc(v, "sine", 120, 45, t, 0.002, 0.95, 0.18); G.losc(sh, "sawtooth", J(210, .06), 90, t, 0.004, 0.33, 0.2);
        G.lnoise(v, t, 0.001, 0.42, 0.12, "lowpass", 1800, 300, 0.8);
    });
    public void bomb()
    {
        if (!Ev("bomb", 0.05)) return;
        P(t => { var v = V(G.sfxPan(0.25), 0.7);
            G.lclick(v, t, 0.75, 1500); G.losc(v, "sine", 90, 28, t, 0.003, 1.1, 0.6);
            G.lnoise(v, t, 0.002, 0.95, 0.75, "lowpass", 6000, 160, 0.7);
            for (int i = 0; i < 12; i++) G.lclick(v, t + R(0.05, 0.6), R(0.05, 0.16), R(2000, 6000)); });
    }
    public void boomSmall()
    {
        if (!Ev("boomSmall", 0.05)) return;
        P(t => { var v = V(G.sfxPan(0.3), 0.5);
            G.lclick(v, t, 0.45, 1800); G.losc(v, "sine", 130, 45, t, 0.003, 0.6, 0.3);
            G.lnoise(v, t, 0.002, 0.5, 0.3, "lowpass", 3500, 220, 0.7);
            for (int i = 0; i < 5; i++) G.lclick(v, t + R(0.03, 0.3), R(0.04, 0.1), R(2000, 5000)); });
    }
    public void levelup() => P(t =>
    {
        var v = V(0, 0.55); var fs = new[] { 523.25, 659.25, 783.99, 1046.5 };
        for (int i = 0; i < fs.Length; i++)
        {
            double f = fs[i];
            G.losc(v, "triangle", f, 0, t + i * 0.075, 0.006, 0.18, 0.28); G.losc(v, "sine", f * 2, 0, t + i * 0.075, 0.006, 0.05, 0.2);
            G.losc(v, "sine", f * 1.003, 0, t + i * 0.075, 0.006, 0.07, 0.35);
        }
        G.lnoise(v, t + 0.28, 0.02, 0.07, 0.5, "highpass", 6000, 9000, 0.5);
    });
    public void pick() => P(t =>
    {
        var v = V(G.sfxPan(0.15), 0.3);
        G.lclick(v, t, 0.12, 5000); G.modal(v, t, J(1200, .03), G.PLASTIC, 0.1, 0.08); G.losc(v, "sine", 1800, 2400, t + 0.02, 0.005, 0.07, 0.12);
    });
    public void deflect()
    {
        if (!Ev("deflect", 0.05)) return;
        P(t => { var v = V(G.sfxPan(), 0.35); G.lclick(v, t, 0.5, 4500); G.modal(v, t, J(1300, .06), G.METAL, 0.14, 0.18); });
    }
    public void clank()
    {
        if (!Ev("clank", 0.05)) return;
        P(t => { var v = V(G.sfxPan(), 0.35); G.lclick(v, t, 0.55, 4000); G.modal(v, t, J(760, .06), G.METAL, 0.18, 0.3); });
    }
    public void grunt()
    {
        if (!Ev("grunt", 0.12)) return;
        P(t => { var v = V(G.sfxPan(), 0.3);
            var sh = C.createWaveShaper(); sh.curve = G.SFXL.curve;
            var lp = C.createBiquadFilter(); lp.type = "lowpass"; lp.frequency.value = 900;
            sh.connect(lp); lp.connect(v);
            G.losc(sh, "sawtooth", J(140, .1), 65, t, 0.02, 0.28, 0.28);
            G.lnoise(v, t, 0.02, 0.12, 0.25, "bandpass", 600, 300, 0.8); });
    }
    public void roar() => P(t =>
    {
        var ctx = C; var v = V(0, 0.45); double dur = 0.9;
        var sh = ctx.createWaveShaper(); sh.curve = G.SFXL.curve;
        var lp = ctx.createBiquadFilter(); lp.type = "lowpass"; lp.frequency.setValueAtTime(300, t); lp.frequency.linearRampToValueAtTime(1100, t + 0.25); lp.frequency.exponentialRampToValueAtTime(250, t + dur);
        var trem = ctx.createGain(); var lfo = ctx.createOscillator(); var lfoG = ctx.createGain();
        lfo.frequency.value = 28; lfoG.gain.value = 0.35; lfo.connect(lfoG); lfoG.connect(trem.gain); trem.gain.value = 0.65;
        sh.connect(lp); lp.connect(trem); trem.connect(v);
        foreach (var d in new double[] { 0, 7, -9 }) { var o = G.losc(sh, "sawtooth", 82 + d, 48 + d / 2, t, 0.06, 0.38, dur); o.detune.value = d * 3; }
        G.lnoise(v, t, 0.05, 0.22, dur, "bandpass", 700, 300, 0.7);
        lfo.start(t); lfo.stop(t + dur + 0.1);
    });
    public void click() => P(t => { var v = V(0, 0.1); G.lclick(v, t, 0.18, 4000); G.modal(v, t, 2200, G.PLASTIC, 0.06, 0.03); });
    public void build()
    {
        if (!Ev("build", 0.22)) return;
        P(t => { var v = V(G.sfxPan(), 0.3);
            G.losc(v, "sine", J(200, .1), 80, t, 0.004, 0.3, 0.1); G.modal(v, t + 0.01, J(700, .1), G.PLASTIC, 0.06, 0.05);
            G.lnoise(v, t, 0.003, 0.13, 0.08, "lowpass", 900, 300, 0.7); });
    }
    public void zap()
    {
        if (!Ev("zap", 0.04)) return;
        P(t => { var ctx = C; var v = V(G.sfxPan(), 0.25);
            var o = ctx.createOscillator(); var g = ctx.createGain();
            o.type = "square"; o.frequency.setValueAtTime(J(1600, .1), t);
            for (int i = 1; i < 8; i++) o.frequency.setValueAtTime(R(300, 2600), t + i * 0.012);
            G.lenv(g, t, 0.001, 0.15, 0.1); o.connect(g); g.connect(v); o.start(t); o.stop(t + 0.14);
            G.lnoise(v, t, 0.0008, 0.45, 0.05, "highpass", 4000, 2000, 0.7);
            G.lnoise(v, t, 0.002, 0.25, 0.2, "bandpass", 180, 90, 1); });
    }
    public void whoosh() => P(t => G.whooshAt(V(G.sfxPan(), 0.2), t, 500, 2.8, 0.35, 0.45, 1.8));
    public void freeze()
    {
        if (!Ev("freeze", 0.08)) return;
        P(t => { var v = V(G.sfxPan(), 0.6);
            foreach (var (f, d) in new[] { (2100.0, 0.0), (2900, .03), (3700, .05), (5100, .08) })
                G.modal(v, t + d, J(f, .03), new[] { new double[] { 1, 1, 1 }, new double[] { 2.4, .3, .6 } }, 0.06, 0.5);
            G.lnoise(v, t, 0.01, 0.18, 0.4, "highpass", 7000, 3000, 0.5);
            G.losc(v, "sine", 600, 240, t, 0.002, 0.18, 0.12); });
    }
    public void fireball()
    {
        if (!Ev("fireball", 0.05)) return;
        P(t => { var v = V(G.sfxPan(), 0.4);
            G.lnoise(v, t, 0.04, 0.45, 0.35, "lowpass", 400, 1600, 0.8);
            G.lnoise(v, t + 0.03, 0.02, 0.22, 0.4, "bandpass", 2400, 900, 0.9);
            for (int i = 0; i < 6; i++) G.lclick(v, t + R(0.05, 0.4), R(0.03, 0.08), R(1500, 4000));
            G.losc(v, "sine", 140, 60, t, 0.02, 0.3, 0.3); });
    }
    public void thunder()
    {
        if (!Ev("thunder", 0.06)) return;
        P(t => { var v = V(G.sfxPan(), 0.6);
            G.lnoise(v, t, 0.0008, 0.6, 0.06, "highpass", 3500, 1500, 0.7);
            G.lnoise(v, t + 0.02, 0.03, 0.5, 0.6, "lowpass", 900, 120, 0.8);
            G.losc(v, "sine", 90, 40, t, 0.01, 0.5, 0.5); });
    }
    public void vortex() => P(t =>
    {
        var ctx = C; var v = V(0, 0.3); double dur = 0.5;
        var s = ctx.createBufferSource(); s.buffer = G.SFXL.noise;
        var f = ctx.createBiquadFilter(); f.type = "bandpass"; f.Q.value = 3; f.frequency.value = 900;
        var lfo = ctx.createOscillator(); var lg = ctx.createGain(); lfo.frequency.value = 9; lg.gain.value = 500; lfo.connect(lg); lg.connect(f.frequency);
        var g = ctx.createGain(); G.lenv(g, t, 0.08, 0.45, dur);
        s.connect(f); f.connect(g); g.connect(v); s.start(t, Js.R()); s.stop(t + dur + 0.12); lfo.start(t); lfo.stop(t + dur + 0.12);
    });
    public void slam() => P(t =>
    {
        double pan = G.sfxPan(0.1); var v = V(pan, 0.55);
        G.lclick(v, t, 0.85, 1200); G.losc(v, "sine", 75, 30, t, 0.002, 1.2, 0.35);
        G.lnoise(v, t, 0.002, 0.65, 0.3, "lowpass", 2500, 200, 0.7);
        for (int i = 0; i < 5; i++) G.modal(V(pan + R(-.5, .5), 0.3), t + R(0.04, 0.3), R(500, 1300), G.PLASTIC, R(.04, .08), .05);
    });
}
