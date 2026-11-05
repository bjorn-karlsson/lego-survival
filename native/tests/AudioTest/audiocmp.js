// Compare two sets of renders (audioref.js vs --audiotest): loudness, the
// delay between them, how alike the waveforms are, the loudness over time and
// the balance across the spectrum.  node audiocmp.js <refdir> <minedir> [filter]
const fs = require('fs'), path = require('path');
const [refDir, mineDir, filt = ''] = process.argv.slice(2);
const RATE = 48000;
function load(f) { const b = fs.readFileSync(f); return new Float32Array(b.buffer, b.byteOffset, b.length / 4); }
const db = x => 20 * Math.log10(Math.max(1e-9, x));
function mono(s) { const m = new Float32Array(s.length / 2); for (let i = 0; i < m.length; i++) m[i] = (s[i * 2] + s[i * 2 + 1]) / 2; return m; }
function rms(a, i0 = 0, i1 = a.length) { let e = 0; for (let i = i0; i < i1; i++) e += a[i] * a[i]; return Math.sqrt(e / Math.max(1, i1 - i0)); }
function peak(a) { let p = 0; for (const v of a) p = Math.max(p, Math.abs(v)); return p; }
/* the lag (mine behind ref, in samples) that lines the two up best */
function bestLag(r, m, max = 600) {
  let best = 0, bc = -Infinity; const n = Math.min(r.length, m.length, RATE * 2);
  for (let L = -max; L <= max; L += 1) {
    let c = 0; for (let i = Math.max(0, -L); i < n - Math.max(0, L); i += 2) c += r[i] * m[i + L];
    if (c > bc) { bc = c; best = L; }
  }
  return best;
}
function corr(r, m, L) {
  let c = 0, er = 0, em = 0; const n = Math.min(r.length, m.length);
  for (let i = Math.max(0, -L); i < n - Math.max(0, L); i++) { c += r[i] * m[i + L]; er += r[i] * r[i]; em += m[i + L] * m[i + L]; }
  return c / Math.sqrt(Math.max(1e-30, er * em));
}
function fft(re, im) {
  const n = re.length;
  for (let i = 1, j = 0; i < n; i++) { let b = n >> 1; for (; j & b; b >>= 1) j ^= b; j ^= b; if (i < j) { [re[i], re[j]] = [re[j], re[i]]; [im[i], im[j]] = [im[j], im[i]]; } }
  for (let len = 2; len <= n; len <<= 1) {
    const a = -2 * Math.PI / len, wr = Math.cos(a), wi = Math.sin(a);
    for (let i = 0; i < n; i += len) { let cr = 1, ci = 0;
      for (let k = 0; k < len / 2; k++) { const u = i + k, v = u + len / 2, tr = re[v] * cr - im[v] * ci, ti = re[v] * ci + im[v] * cr;
        re[v] = re[u] - tr; im[v] = im[u] - ti; re[u] += tr; im[u] += ti; const nr = cr * wr - ci * wi; ci = cr * wi + ci * wr; cr = nr; } }
  }
}
const BANDS = [63, 125, 250, 500, 1000, 2000, 4000, 8000, 16000];
function bands(a) {
  const N = 4096, e = new Float64Array(BANDS.length);
  for (let s = 0; s + N <= a.length; s += N / 2) {
    const re = new Float64Array(N), im = new Float64Array(N);
    for (let i = 0; i < N; i++) re[i] = a[s + i] * (0.5 - 0.5 * Math.cos(2 * Math.PI * i / N));
    fft(re, im);
    for (let k = 1; k < N / 2; k++) { const f = k * RATE / N, p = re[k] * re[k] + im[k] * im[k];
      const b = BANDS.findIndex(c => f < c * Math.SQRT2); if (b >= 0 && f >= BANDS[b] / Math.SQRT2) e[b] += p; }
  }
  return e;
}
const files = fs.readdirSync(refDir).filter(f => f.endsWith('.f32') && f.includes(filt)).sort();
console.log('sound              ref dB  mine dB   diff   lag  corr  env|d| env max  L-R ref/mine   worst band');
for (const f of files) {
  const mp = path.join(mineDir, f);
  if (!fs.existsSync(mp)) { console.log(f, 'MISSING in mine'); continue; }
  const rs = load(path.join(refDir, f)), ms = load(mp), r = mono(rs), m = mono(ms);
  const rr = rms(r), mr = rms(m), lag = bestLag(r, m), c = corr(r, m, lag);
  /* loudness every 20 ms, where the reference is audible */
  const W = RATE / 50; let sd = 0, nd = 0, mx = 0;
  for (let i = Math.max(0, -lag); i + W + Math.max(0, lag) <= r.length; i += W) {
    const a = rms(r, i, i + W), b = rms(m, i + lag, i + lag + W);
    if (db(a) < -50 && db(b) < -50) continue;
    const d = db(b) - db(a); sd += Math.abs(d); nd++; if (Math.abs(d) > Math.abs(mx)) mx = d;
  }
  const lr = s => { let l = 0, q = 0; for (let i = 0; i < s.length; i += 2) { l += s[i] * s[i]; q += s[i + 1] * s[i + 1]; } return 10 * Math.log10(Math.max(1e-30, l) / Math.max(1e-30, q)); };
  const br = bands(r), bm = bands(m); let wb = '', wd = 0;
  const tot = br.reduce((x, y) => x + y, 0);
  for (let b = 0; b < BANDS.length; b++) { if (br[b] < tot * 1e-4) continue; const d = 10 * Math.log10(Math.max(1e-30, bm[b]) / Math.max(1e-30, br[b])); if (Math.abs(d) > Math.abs(wd)) { wd = d; wb = BANDS[b] + 'Hz'; } }
  console.log(`${f.replace('.f32', '').padEnd(18)} ${db(rr).toFixed(1).padStart(6)} ${db(mr).toFixed(1).padStart(8)} ${(db(mr) - db(rr)).toFixed(1).padStart(6)} ${String(lag).padStart(5)} ${c.toFixed(2).padStart(5)} ${(sd / Math.max(1, nd)).toFixed(1).padStart(6)} ${mx.toFixed(1).padStart(7)}  ${lr(rs).toFixed(1).padStart(5)}/${lr(ms).toFixed(1).padEnd(5)}  ${wb} ${wd.toFixed(1)}`);
}
