// The reference: the game's own audio code, rendered in Chromium's
// OfflineAudioContext, one file per sound effect and per music track, raw
// float32 stereo interleaved at 48 kHz -- the same list, lengths and random
// numbers as the native build's --audiotest.
// node audioref.js <lego-survival.html> <outdir>
const { chromium } = require('playwright');
const fs = require('fs'), path = require('path');
/* the mixer runs a second before anything plays, as it has in a game by the
   time the first sound comes: Chromium's compressor starts fully closed */
const RATE = 48000, LEAD = 1.0, SFX_SECS = 2.0, MUSIC_SECS = 8.0;
(async () => {
  const html = fs.readFileSync(process.argv[2], 'utf8');
  const out = process.argv[3]; fs.mkdirSync(out, { recursive: true });
  const a0 = html.indexOf('/* =============================== AUDIO');
  const a1 = html.indexOf('/* =============================== INPUT');
  if (a0 < 0 || a1 < 0) throw new Error('audio section not found');
  const audio = html.slice(a0, a1);
  const browser = await chromium.launch({ args: ['--autoplay-policy=no-user-gesture-required'] });
  const page = await browser.newPage();
  await page.setContent('<!doctype html><html><body></body></html>');
  const harness = `
    let __ms = 0;
    Math.random = function(){ __ms |= 0; __ms = __ms + 0x6D2B79F5 | 0; let t = Math.imul(__ms ^ __ms >>> 15, 1 | __ms);
      t = t + Math.imul(t ^ t >>> 7, 61 | t) ^ t; return ((t ^ t >>> 14) >>> 0) / 4294967296; };
    const TAU = Math.PI*2;
    const rnd = (a=1,b)=> b===undefined ? Math.random()*a : a+Math.random()*(b-a);
    const clamp = (v,a,b)=> v<a?a:v>b?b:v;
    let optOn = false; function optRender(){}
    let previewing = false;
    const wave = { n: 1 }; const enemies = []; const cam = { x: 0, y: 0 }; let cw = 960, zoom = 1;
    let __len = 0;
    window.AudioContext = function(){ return new OfflineAudioContext(2, __len, ${RATE}); };
    ${audio}
    function __fresh(sfx, mus, secs){
      __ms = 12345; __len = Math.round(secs * ${RATE});
      if (music.timer) { clearInterval(music.timer); music.timer = null; }
      A.ctx = null; A.muted = false; A.sfxOn = sfx; A.musicOn = mus; A.sfxVol = 1; A.musVol = 1;
      for (const k in sfxLast) delete sfxLast[k];
      sfxPanNext = null;
      audioInit();
      if (music.timer) { clearInterval(music.timer); music.timer = null; }
    }
    async function __render(){
      const b = await A.ctx.startRendering();
      const s0 = Math.round(${LEAD} * ${RATE}), L = b.getChannelData(0).subarray(s0), R = b.getChannelData(1).subarray(s0), o = new Float32Array(L.length * 2);
      for (let i = 0; i < L.length; i++) { o[i*2] = L[i]; o[i*2+1] = R[i]; }
      let s = ''; const u = new Uint8Array(o.buffer);
      for (let i = 0; i < u.length; i += 0x8000) s += String.fromCharCode.apply(null, u.subarray(i, i + 0x8000));
      return btoa(s);
    }
    window.__sfxNames = Object.keys(SFX);
    window.__sfx = async (name) => {
      __fresh(true, false, ${LEAD} + ${SFX_SECS});
      A.ctx.suspend(${LEAD}).then(() => { SFX[name](); A.ctx.resume(); });
      return __render();
    };
    window.__trackIds = TRACKS.map(t => t.id);
    window.__music = async (id) => {
      __fresh(false, true, ${LEAD} + ${MUSIC_SECS});
      const tr = TRACKS.find(t => t.id === id);
      A.ctx.suspend(${LEAD}).then(() => {
        const t0 = A.ctx.currentTime;
        music.next = t0 + 0.1; music.step = 0; music.loop = 0; music.bars = 0; music.track = tr; music.pending = tr;
        const spb = 60/tr.bpm/2;
        while (music.next < t0 + ${MUSIC_SECS} - 0.5) {
          scheduleStep(music.step, music.next);
          if (++music.step >= 16) { music.step = 0; music.loop++; music.bars++; }
          music.next += spb;
        }
        A.ctx.resume();
      });
      return __render();
    };`;
  await page.addScriptTag({ content: harness });
  const names = await page.evaluate(() => window.__sfxNames);
  for (const n of names) {
    const b64 = await page.evaluate(n => window.__sfx(n), n);
    fs.writeFileSync(path.join(out, 'sfx_' + n + '.f32'), Buffer.from(b64, 'base64'));
  }
  const ids = await page.evaluate(() => window.__trackIds);
  for (const id of ids) {
    const b64 = await page.evaluate(id => window.__music(id), id);
    fs.writeFileSync(path.join(out, 'music_' + id + '.f32'), Buffer.from(b64, 'base64'));
  }
  console.log(`audioref: ${names.length} effects, ${ids.length} tracks`);
  await browser.close();
})().catch(e => { console.error(e); process.exit(1); });
