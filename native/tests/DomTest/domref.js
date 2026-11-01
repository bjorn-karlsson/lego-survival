// The reference: the same page, scripts stripped, the same fonts, in Chromium.
// node domref.js <lego-survival.html> <outdir> [W H]
const { chromium } = require('playwright');
const fs = require('fs'), path = require('path');
(async () => {
  const [src, out, W = '1280', H = '800'] = process.argv.slice(2);
  fs.mkdirSync(out, { recursive: true });
  let html = fs.readFileSync(src, 'utf8');
  html = html.replace(/<script[\s\S]*?<\/script>/g, '').replace(/<link[^>]*fonts[^>]*>/g, '');
  const fonts = path.resolve(__dirname, '../../assets/fonts');
  const ff = (fam, file, w) => `@font-face{font-family:'${fam}';src:url('file://${fonts}/${file}');font-weight:${w}}`;
  const face = [ff('Titan One', 'TitanOne.ttf', 400), ff('Fredoka', 'Fredoka-400.ttf', 400), ff('Fredoka', 'Fredoka-500.ttf', 500),
                ff('Fredoka', 'Fredoka-600.ttf', 600), ff('Fredoka', 'Fredoka-700.ttf', 700)].join('\n');
  html = html.replace('<style>', '<style>' + face + '\n').replace('</style>', '.boxplate{transform:none !important}\n</style>');
  const tmp = path.join(out, 'page.html');
  fs.writeFileSync(tmp, html);
  const b = await chromium.launch({ ignoreDefaultArgs: ['--hide-scrollbars'] });
  const p = await b.newPage({ viewport: { width: +W, height: +H } });
  await p.goto('file://' + tmp);
  await p.evaluate(() => document.fonts.ready);
  const show = id => p.evaluate(i => document.getElementById(i).classList.remove('hidden'), id);
  const hide = id => p.evaluate(i => document.getElementById(i).classList.add('hidden'), id);
  const scenes = [['title', async () => {}], ['pause', () => show('pauseScreen')], ['options', async () => { await hide('pauseScreen'); await show('optScreen'); }],
                  ['over', async () => { await hide('optScreen'); await show('overScreen'); }]];
  const dump = () => p.evaluate(() => {
    const out = [];
    const walk = (e, path) => {
      for (const c of e.children) {
        const k = path + '/' + c.tagName.toLowerCase() + (c.id ? '#' + c.id : '') + (c.className && typeof c.className === 'string' ? '.' + c.className.trim().split(/\s+/).join('.') : '');
        const r = c.getBoundingClientRect();
        if (r.width || r.height) out.push(k + ' ' + [r.x, r.y, r.width, r.height].map(v => v.toFixed(1)).join(' '));
        walk(c, k);
      }
    };
    walk(document.body, 'body');
    return out.join('\n');
  });
  for (const [name, setup] of scenes) { await setup(); await p.waitForTimeout(150); await p.screenshot({ path: path.join(out, name + '.ref.png') }); fs.writeFileSync(path.join(out, name + '.ref.txt'), await dump()); }
  await b.close();
})();
