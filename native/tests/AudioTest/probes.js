// Single-node probes in Chromium, straight to the destination (no mixer): the
// exact behaviour the native work-alike is checked against.
// node probes.js <outdir>   (the native side: Brickblade --audioprobe <outdir>)
const { chromium } = require('playwright');
const fs = require('fs'), path = require('path');
const RATE = 48000, SECS = 0.5;
// (0.1 + 0.07) * 48000 = 8160.0000000000009 in doubles
const PROBES = {
  // an oscillator starting between two samples
  osc_frac: `const o = c.createOscillator(); o.frequency.value = 1000; o.connect(c.destination); o.start(0.1 + 0.3/48000);`,
  // a start time a hair above a whole sample
  osc_near: `const o = c.createOscillator(); o.frequency.value = 1000; o.connect(c.destination); o.start(0.1 + 0.07);`,
  buf_near: `const b = c.createBuffer(1, 1000, 48000); const d = b.getChannelData(0); for (let i=0;i<1000;i++) d[i] = i/1000;
            const s = c.createBufferSource(); s.buffer = b; s.connect(c.destination); s.start(0.1 + 0.07);`,
  // the four shapes, low and high
  tri220: `const o = c.createOscillator(); o.type='triangle'; o.frequency.value = 220; o.connect(c.destination); o.start(0.1);`,
  saw220: `const o = c.createOscillator(); o.type='sawtooth'; o.frequency.value = 220; o.connect(c.destination); o.start(0.1);`,
  sq220: `const o = c.createOscillator(); o.type='square'; o.frequency.value = 220; o.connect(c.destination); o.start(0.1);`,
  saw2000: `const o = c.createOscillator(); o.type='sawtooth'; o.frequency.value = 2000; o.connect(c.destination); o.start(0.1);`,
  tri1500: `const o = c.createOscillator(); o.type='triangle'; o.frequency.value = 1500; o.connect(c.destination); o.start(0.1);`,
  saw200: `const o = c.createOscillator(); o.type='sawtooth'; o.frequency.value = 200; o.connect(c.destination); o.start(0.1);`,
  saw200a: `const o = c.createOscillator(); o.type='sawtooth'; o.frequency.setValueAtTime(200, 0.1); o.connect(c.destination); o.start(0.1);`,
  saw200l: `const o = c.createOscillator(); o.type='sawtooth'; o.frequency.setValueAtTime(200, 0.1); o.frequency.linearRampToValueAtTime(201, 0.4); o.connect(c.destination); o.start(0.1);`,
  // started mid-quantum (2400), the frequency changing later, mid-quantum too (4800)
  saw_late: `const o = c.createOscillator(); o.type='sawtooth'; o.frequency.setValueAtTime(200, 0.1); o.connect(c.destination); o.start(0.05);`,
  // a filter whose frequency is set at the start of a source that begins mid-quantum
  lp_start: `const o = c.createOscillator(); o.type='sawtooth'; o.frequency.value = 110; const f = c.createBiquadFilter(); f.type='lowpass';
             f.frequency.setValueAtTime(3000, 0.1); f.Q.value = 0.7; o.connect(f); f.connect(c.destination); o.start(0.1);`,
  sawsweep: `const o = c.createOscillator(); o.type='sawtooth'; o.frequency.setValueAtTime(200, 0.1); o.frequency.exponentialRampToValueAtTime(3000, 0.4); o.connect(c.destination); o.start(0.1);`,
  // a buffer read from a fractional offset, and started between samples
  buf_off: `const b = c.createBuffer(1, 1000, 48000); const d = b.getChannelData(0); for (let i=0;i<1000;i++) d[i] = i/1000;
            const s = c.createBufferSource(); s.buffer = b; s.connect(c.destination); s.start(0.1, 10.4/48000);`,
  buf_off6: `const b = c.createBuffer(1, 1000, 48000); const d = b.getChannelData(0); for (let i=0;i<1000;i++) d[i] = i/1000;
            const s = c.createBufferSource(); s.buffer = b; s.connect(c.destination); s.start(0.1, 10.6/48000);`,
  buf_frac: `const b = c.createBuffer(1, 1000, 48000); const d = b.getChannelData(0); for (let i=0;i<1000;i++) d[i] = i/1000;
            const s = c.createBufferSource(); s.buffer = b; s.connect(c.destination); s.start(0.1 + 0.3/48000);`,
  buf_loop: `const b = c.createBuffer(1, 100, 48000); const d = b.getChannelData(0); for (let i=0;i<100;i++) d[i] = i/100;
            const s = c.createBufferSource(); s.buffer = b; s.loop = true; s.connect(c.destination); s.start(0.1, 90.5/48000);`,
  // the waveshaper
  shaper: `const o = c.createOscillator(); o.frequency.value = 300; const g = c.createGain(); g.gain.value = 0.9; const w = c.createWaveShaper();
           const cv = new Float32Array(1024); for (let i=0;i<1024;i++){ const x = i/512 - 1; cv[i] = Math.tanh(2.2*x); } w.curve = cv;
           o.connect(g); g.connect(w); w.connect(c.destination); o.start(0.1);`,
  // a filter sweeping fast (per-sample coefficients?)
  lp_sweep: `const o = c.createOscillator(); o.type='sawtooth'; o.frequency.value = 110; const f = c.createBiquadFilter(); f.type='lowpass';
             f.frequency.setValueAtTime(6000, 0.1); f.frequency.exponentialRampToValueAtTime(160, 0.3); f.Q.value = 0.7; o.connect(f); f.connect(c.destination); o.start(0.1);`,
  bp_sweep: `const o = c.createOscillator(); o.type='sawtooth'; o.frequency.value = 110; const f = c.createBiquadFilter(); f.type='bandpass';
             f.frequency.setValueAtTime(2000, 0.1); f.frequency.exponentialRampToValueAtTime(300, 0.3); f.Q.value = 1.4; o.connect(f); f.connect(c.destination); o.start(0.1);`,
  // gain automation between samples
  env_frac: `const o = c.createOscillator(); o.frequency.value = 500; const g = c.createGain(); const t = 0.1 + 0.37/48000;
             g.gain.setValueAtTime(0.0001, t); g.gain.exponentialRampToValueAtTime(0.8, t + 0.002); g.gain.exponentialRampToValueAtTime(0.0001, t + 0.05);
             o.connect(g); g.connect(c.destination); o.start(t); o.stop(t + 0.06);`,
  lin_frac: `const o = c.createOscillator(); o.frequency.value = 500; const g = c.createGain(); const t = 0.1 + 0.37/48000;
             g.gain.setValueAtTime(0, t); g.gain.linearRampToValueAtTime(0.8, t + 0.004); g.gain.exponentialRampToValueAtTime(0.0001, t + 0.05);
             o.connect(g); g.connect(c.destination); o.start(t); o.stop(t + 0.06);`,
  // a stop between samples
  stop_frac: `const o = c.createOscillator(); o.frequency.value = 500; o.connect(c.destination); o.start(0.1); o.stop(0.1 + 100.6/48000);`,
  // frequency modulation from a node (a-rate)
  fm: `const o = c.createOscillator(); o.frequency.value = 400; const m = c.createOscillator(); m.frequency.value = 30; const mg = c.createGain(); mg.gain.value = 150;
       m.connect(mg); mg.connect(o.frequency); o.connect(c.destination); o.start(0.1); m.start(0.1);`,
  detune: `const o = c.createOscillator(); o.type='sawtooth'; o.frequency.value = 100; o.detune.setValueAtTime(0, 0.1); o.detune.linearRampToValueAtTime(1200, 0.4); o.connect(c.destination); o.start(0.1);`,
  pan: `const o = c.createOscillator(); o.frequency.value = 300; const p = c.createStereoPanner(); p.pan.value = 0.6; o.connect(p); p.connect(c.destination); o.start(0.1);`,
  delay: `const o = c.createOscillator(); o.frequency.value = 300; const g = c.createGain(); g.gain.setValueAtTime(1, 0.1); g.gain.setValueAtTime(0, 0.12);
          const d = c.createDelay(1); d.delayTime.value = 0.245; const fb = c.createGain(); fb.gain.value = 0.3; o.connect(g); g.connect(d); d.connect(fb); fb.connect(d); d.connect(c.destination); g.connect(c.destination); o.start(0.1);`,
};
(async () => {
  const out = process.argv[2]; fs.mkdirSync(out, { recursive: true });
  const browser = await chromium.launch();
  const page = await browser.newPage();
  await page.setContent('<!doctype html><html><body></body></html>');
  for (const [name, code] of Object.entries(PROBES)) {
    const b64 = await page.evaluate(async ({ code, RATE, SECS }) => {
      const c = new OfflineAudioContext(2, Math.round(RATE * SECS), RATE);
      new Function('c', code)(c);
      const b = await c.startRendering();
      const L = b.getChannelData(0), R = b.getChannelData(1), o = new Float32Array(L.length * 2);
      for (let i = 0; i < L.length; i++) { o[i * 2] = L[i]; o[i * 2 + 1] = R[i]; }
      let s = ''; const u = new Uint8Array(o.buffer);
      for (let i = 0; i < u.length; i += 0x8000) s += String.fromCharCode.apply(null, u.subarray(i, i + 0x8000));
      return btoa(s);
    }, { code, RATE, SECS });
    fs.writeFileSync(path.join(out, name + '.f32'), Buffer.from(b64, 'base64'));
  }
  console.log(`probes: ${Object.keys(PROBES).length}`);
  await browser.close();
})().catch(e => { console.error(e); process.exit(1); });
