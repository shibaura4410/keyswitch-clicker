// index.html に入っている打鍵音(録音)と、ブラウザ上で合成している音を、Unity 用の WAV に書き出す。
// ffmpeg と Playwright(Chromium)が要る。
//   node vrchat/tools/export-audio.js
const fs = require('fs'), os = require('os'), path = require('path'), { execFileSync } = require('child_process');
const { chromium } = require('playwright');
const ROOT = path.join(__dirname, '..', '..');
const html = fs.readFileSync(path.join(ROOT, 'index.html'), 'utf8');
const OUT = path.join(ROOT, 'vrchat/Assets/KeyswitchCrusher/Audio');
const TMP = fs.mkdtempSync(path.join(os.tmpdir(), 'ksc-audio-'));
const src = html.slice(html.indexOf('const SWITCH_TYPES'), html.indexOf('// 1台のキーボードは36キー'));
const SWITCH_TYPES = new Function(src + '; return SWITCH_TYPES;')();
const s2 = html.slice(html.indexOf('const SAMPLES = {'), html.indexOf('const SAMPLE_GAIN'));
const SAMPLES = new Function(s2 + '; return SAMPLES;')();
function wav(f32, rate, file) {
  const b = Buffer.alloc(44 + f32.length * 2);
  b.write('RIFF', 0); b.writeUInt32LE(36 + f32.length * 2, 4); b.write('WAVEfmt ', 8); b.writeUInt32LE(16, 16);
  b.writeUInt16LE(1, 20); b.writeUInt16LE(1, 22); b.writeUInt32LE(rate, 24); b.writeUInt32LE(rate * 2, 28);
  b.writeUInt16LE(2, 32); b.writeUInt16LE(16, 34); b.write('data', 36); b.writeUInt32LE(f32.length * 2, 40);
  for (let i = 0; i < f32.length; i++) b.writeInt16LE(Math.round(Math.max(-1, Math.min(1, f32[i])) * 32767), 44 + i * 2);
  fs.writeFileSync(file, b);
}
(async () => {
  const report = [];
  for (const id of Object.keys(SAMPLES)) {
    SAMPLES[id].forEach((b64, i) => {
      const mp3 = path.join(TMP, `${id}_${i}.mp3`);
      fs.writeFileSync(mp3, Buffer.from(b64, 'base64'));
      const out = path.join(OUT, `hit_${id}_${i}.wav`);
      execFileSync('ffmpeg', ['-y', '-loglevel', 'error', '-i', mp3, '-af', 'silenceremove=start_periods=1:start_threshold=0.03', '-ac', '1', '-ar', '44100', '-c:a', 'pcm_s16le', out]);
      report.push(`${path.basename(out)} ${fs.statSync(out).size}`);
    });
  }
  const browser = await chromium.launch();
  const page = await browser.newPage();
  const synth = SWITCH_TYPES.filter(t => !SAMPLES[t.id]);
  const res = await page.evaluate(async (synth) => {
    const RATE = 44100, SND_GAIN = 4;
    const BAKE = 2.5; // 合成音は録音より小さいので、録音と同じ倍率(本体の hitGain)で鳴らせるよう先に持ち上げておく
    async function render(dur, build) {
      const a = new OfflineAudioContext(1, Math.floor(RATE * dur), RATE);
      build(a);
      const buf = await a.startRendering();
      return Array.from(buf.getChannelData(0));
    }
    function hit(a, s) {
      const master = a.createDynamicsCompressor();
      const out = a.createGain(); out.gain.value = BAKE;
      master.connect(out).connect(a.destination);
      const len = Math.floor(a.sampleRate * 0.004);
      const impulse = a.createBuffer(1, len, a.sampleRate);
      const d = impulse.getChannelData(0);
      for (let i = 0; i < len; i++) d[i] = (Math.random() * 2 - 1) * Math.pow(1 - i / len, 2);
      function strike(modes, when, vol, pitch) {
        const src = a.createBufferSource();
        src.buffer = impulse;
        for (const [f, q, g] of modes) {
          const bp = a.createBiquadFilter();
          bp.type = 'bandpass';
          bp.frequency.value = f * pitch * (0.96 + Math.random() * 0.08);
          bp.Q.value = q;
          const gn = a.createGain();
          gn.gain.value = g * vol * SND_GAIN * Math.sqrt(1000 / f);
          src.connect(bp).connect(gn).connect(master);
        }
        src.start(when);
      }
      const v = 1, p = 1, up = s.gap || 0.08;
      if (s.tick) strike(s.tick, 0, v, p);
      strike(s.down, s.tick ? 0.012 : 0, v, p);
      if (s.ping) strike([[s.ping, 45, 0.05]], 0.004, v, 1);
      if (s.tick) strike(s.tick, up - 0.01, v * 0.6, p * 1.06);
      strike(s.up, up, v * 0.7, p);
    }
    function noise(a, dur, freq, vol) {
      const len = Math.max(1, Math.floor(a.sampleRate * dur));
      const buf = a.createBuffer(1, len, a.sampleRate);
      const d = buf.getChannelData(0);
      for (let i = 0; i < len; i++) d[i] = (Math.random() * 2 - 1) * Math.pow(1 - i / len, 2.5);
      const src = a.createBufferSource(); src.buffer = buf;
      const f = a.createBiquadFilter(); f.type = 'bandpass'; f.frequency.value = freq; f.Q.value = 1.2;
      const g = a.createGain(); g.gain.value = vol * BAKE;
      src.connect(f).connect(g).connect(a.destination);
      src.start();
    }
    const out = {};
    for (const t of synth) for (let i = 0; i < 6; i++) out[`hit_${t.id}_${i}`] = await render(0.3, a => hit(a, t.snd));
    for (const big of [false, true]) out[big ? 'break_big' : 'break_small'] = await render(big ? 0.55 : 0.26, a => {
      noise(a, big ? 0.5 : 0.22, big ? 500 : 900, 0.6); noise(a, 0.12, 3200, 0.25);
    });
    return out;
  }, synth.map(t => ({ id: t.id, snd: t.snd })));
  await browser.close();
  for (const [name, arr] of Object.entries(res)) {
    const f = Float32Array.from(arr);
    let peak = 0; for (const x of f) peak = Math.max(peak, Math.abs(x));
    wav(f, 44100, path.join(OUT, name + '.wav'));
    report.push(`${name}.wav peak=${peak.toFixed(2)}`);
  }
  console.log(report.join('\n'));
})();
