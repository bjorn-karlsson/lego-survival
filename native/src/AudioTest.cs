using System.Reflection;

namespace Brickblade;

/* --audiotest OUTDIR: every sound effect, and a stretch of every music track,
   rendered offline through the Web Audio work-alike into raw float32 files
   (stereo, interleaved, 48 kHz). tests/AudioTest/audioref.js renders the same
   list in Chromium with the same random numbers (a seeded mulberry32 in place
   of Math.random in both), so every pitch, pan and noise sample matches and the
   two can be compared sample for sample with tests/AudioTest/audiocmp.js. */
public static class AudioTest
{
    public const int Rate = 48000;
    public const double Lead = 1.0, SfxSecs = 2.0, MusicSecs = 8.0;
    static uint ms;
    static double Mulberry()
    {
        ms += 0x6D2B79F5;
        uint t = (ms ^ (ms >> 15)) * (1 | ms);
        t = (t + (t ^ (t >> 7)) * (61 | t)) ^ t;
        return (t ^ (t >> 14)) / 4294967296.0;
    }
    /* a fresh mixer, as the game builds it, with only the bus under test open */
    static void Fresh(bool sfx, bool mus)
    {
        ms = 12345;
        G.A.ctx = null; G.A.muted = false; G.A.sfxOn = sfx; G.A.musicOn = mus; G.A.sfxVol = 1; G.A.musVol = 1;
        G.sfxLast.Clear(); G.sfxPanClear();
        G.audioGraph(new AudioContext(Rate));
        G.audioGains();
        /* the mixer runs a second before anything plays (as in audioref.js) */
        var lead = new float[(int)(Lead * Rate) * 2];
        G.A.ctx.Render(lead, (int)(Lead * Rate));
    }
    static void Save(string path, double secs)
    {
        int n = (int)(secs * Rate);
        var buf = new float[n * 2];
        G.A.ctx.Render(buf, n);
        var bytes = new byte[buf.Length * 4];
        Buffer.BlockCopy(buf, 0, bytes, 0, bytes.Length);
        File.WriteAllBytes(path, bytes);
    }
    /* a busy fight: the music, and two effects every 21 ms for 10 s (a hit, a
       brick, a stud, a swing, now and then a bomb), timed against real time */
    static void Stress()
    {
        Fresh(true, true);
        var tr = G.TRACKS[^1]; var mu = G.music; double t0 = G.A.ctx.currentTime;
        mu.next = t0 + 0.1; mu.step = 0; mu.loop = 0; mu.track = tr; double spb = 60 / tr.bpm / 2;
        var buf = new float[1024 * 2]; var sw = System.Diagnostics.Stopwatch.StartNew(); int peak = 0; double worst = 0;
        int chunks = (int)(10.0 * Rate / 1024);
        for (int c = 0; c < chunks; c++)
        {
            while (mu.next < G.A.ctx.currentTime + 0.18) { G.scheduleStep(mu.step, mu.next); if (++mu.step >= 16) { mu.step = 0; mu.loop++; } mu.next += spb; }
            G.sfxLast.Clear();
            switch (c % 4) { case 0: G.SFX.hit(); G.SFX.brick(6); break; case 1: G.SFX.stud(c % 12); G.SFX.swing(); break; case 2: G.SFX.hit(); G.SFX.grunt(); break; default: if (c % 16 == 3) G.SFX.bomb(); else G.SFX.pick(); break; }
            long a = System.Diagnostics.Stopwatch.GetTimestamp();
            G.A.ctx.Render(buf, 1024);
            double ms = (System.Diagnostics.Stopwatch.GetTimestamp() - a) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            if (c >= 20) worst = Math.Max(worst, ms); peak = Math.Max(peak, G.A.ctx.LiveSources);
        }
        double audio = chunks * 1024.0 / Rate * 1000;
        Console.WriteLine($"audiotest stress: {audio / 1000:0.0} s of a busy fight rendered in {sw.ElapsedMilliseconds} ms ({sw.ElapsedMilliseconds / audio * 100:0.0}% of one core), worst 1024-frame buffer after the first 20 (warm-up) {worst:0.00} ms of {1024.0 / Rate * 1000:0.0} ms, up to {peak} voices");
    }
    public static int Run(string outDir)
    {
        Directory.CreateDirectory(outDir);
        Js.RandomOverride = Mulberry;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        foreach (var m in typeof(Sfx).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            Fresh(true, false);
            m.Invoke(G.SFX, m.GetParameters().Select(p => p.DefaultValue).ToArray());
            Save(Path.Combine(outDir, "sfx_" + m.Name + ".f32"), SfxSecs);
        }
        foreach (var tr in G.TRACKS)
        {
            Fresh(false, true);
            var mu = G.music;
            double t0 = G.A.ctx.currentTime;
            mu.next = t0 + 0.1; mu.step = 0; mu.loop = 0; mu.bars = 0; mu.track = tr; mu.pending = tr;
            double spb = 60 / tr.bpm / 2;
            while (mu.next < t0 + MusicSecs - 0.5)
            {
                G.scheduleStep(mu.step, mu.next);
                if (++mu.step >= 16) { mu.step = 0; mu.loop++; mu.bars++; }
                mu.next += spb;
            }
            Save(Path.Combine(outDir, "music_" + tr.id + ".f32"), MusicSecs);
        }
        Console.WriteLine($"audiotest: {Directory.GetFiles(outDir, "*.f32").Length} renders in {sw.ElapsedMilliseconds} ms");
        Stress();
        Js.RandomOverride = null;
        return 0;
    }
}
