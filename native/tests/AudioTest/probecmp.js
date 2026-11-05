// probes: the largest sample difference and the error energy against the signal, per probe
// node probecmp.js <refdir> <minedir>
const fs = require('fs'), path = require('path');
const [a, b] = process.argv.slice(2);
const ld = f => { const x = fs.readFileSync(f); return new Float32Array(x.buffer, x.byteOffset, x.length / 4); };
for (const f of fs.readdirSync(a).filter(f => f.endsWith('.f32')).sort()) {
  if (!fs.existsSync(path.join(b, f))) { console.log(f, 'MISSING'); continue; }
  const r = ld(path.join(a, f)), m = ld(path.join(b, f));
  let mx = 0, at = -1, es = 0, ee = 0;
  for (let i = 0; i < r.length; i++) { const d = Math.abs(r[i] - m[i]); if (d > mx) { mx = d; at = i; } es += r[i] * r[i]; ee += d * d; }
  const snr = 10 * Math.log10(es / Math.max(1e-30, ee));
  console.log(`${f.replace('.f32', '').padEnd(10)} max|d| ${mx.toFixed(6)} at frame ${at >> 1}${at & 1 ? 'R' : 'L'}  SNR ${snr.toFixed(1)} dB`);
}
