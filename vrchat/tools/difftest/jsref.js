// ブラウザ版(index.html)のスクリプトを、にせの画面の上でそのまま動かす。操作の列を流し、状態を書き出す
const fs = require('fs'), path = require('path');
const html = fs.readFileSync(path.join(__dirname, '..', '..', '..', 'index.html'), 'utf8');
let src = html.slice(html.indexOf('<script>') + 8, html.lastIndexOf('</script>'));
src = src.replace(/^\s*\(\(\) => \{/, '').replace(/\}\)\(\);\s*$/, '');
src += `\nreturn { get S() { return S; }, hit, frame, buy, buyUp, ITEMS, UPGRADES, ACH, spawnGold, takeGold, checkAch, clickPower, autoPower, render, fmt };`;
let nowMs = 0, rnd = 0.999999;
const handlers = {};
function fake(id) {
  const store = {};
  const f = function () {};
  const p = new Proxy(f, {
    get(t, k) {
      if (k === Symbol.toPrimitive) return () => 0;
      if (k === Symbol.iterator) return function* () {};
      if (k === 'addEventListener') return (type, fn) => { if (id) handlers[id + ':' + type] = fn; };
      if (k === 'open') return false;
      if (k in store) return store[k];
      return (store[k] = fake(null));
    },
    set(t, k, v) { store[k] = v; return true; },
    apply() { return fake(null); },
  });
  return p;
}
const els = {};
const document = { getElementById: id => els[id] || (els[id] = fake(id)), createElement: () => fake(null), addEventListener() {}, querySelector: () => null, hidden: false };
const window = { addEventListener() {}, OfflineAudioContext: undefined, innerWidth: 1000, innerHeight: 800 };
const M2 = Object.create(Math); M2.random = () => rnd;
const G = new Function('document', 'window', 'localStorage', 'performance', 'requestAnimationFrame', 'setTimeout', 'clearTimeout', 'setInterval', 'confirm', 'Math', 'atob', 'devicePixelRatio', src)(
  document, window, { getItem: () => null, setItem() {} }, { now: () => nowMs }, () => {}, () => 0, () => {}, () => 0, () => true, M2, () => '', 1);
const ops = JSON.parse(fs.readFileSync(process.argv[2], 'utf8'));
const out = [];
const snap = () => { const S = G.S; return {
  scrap: S.scrap, total: S.total, runTotal: S.runTotal, hp: S.hp, maxHit: S.maxHit, playSec: S.playSec,
  broken: S.broken, level: S.level, clicks: S.clicks, crits: S.crits, keys: S.keys, maxCombo: S.maxCombo,
  bp: S.bp, prestiges: S.prestiges, kbAll: S.kbAll, brokenAll: S.brokenAll, gold: S.gold, fast: S.fast ? 1 : 0,
  owned: G.ITEMS.map(i => S.owned[i.id] || 0).join(','), ups: G.UPGRADES.map(u => S.ups[u.id] ? 1 : 0).join(''), ach: G.ACH.map(a => S.ach[a.id] ? 1 : 0).join(''),
  click: G.clickPower(), auto: G.autoPower(), fmt: G.fmt(S.scrap) + '|' + G.fmt(S.total) + '|' + G.fmt(S.hp),
}; };
G.frame(0);
for (const op of ops) {
  const S = G.S;
  switch (op[0]) {
    case 'hit': rnd = op[1] ? 0 : 0.999999; G.hit(null, null); break;
    case 'key': S.keys++; break;
    case 'tick': nowMs += op[1] * 1000; rnd = 0.5; G.frame(nowMs); break;
    case 'mode': S.buy = [1, 10, 100, 'max'][op[1]]; break;
    case 'buy': G.buy(G.ITEMS[op[1]]); break;
    case 'up': G.buyUp(G.UPGRADES[op[1]]); break;
    case 'gold': G.spawnGold(); rnd = op[1]; G.takeGold(0, 0); break;
    case 'ps': handlers['psGo:click'](); break;
  }
  G.checkAch();
  out.push(snap());
}
fs.writeFileSync(process.argv[3], out.map(o => JSON.stringify(o)).join('\n') + '\n');
const last = out[out.length - 1];
console.log('js done', ops.length, 'level', last.level, 'prestiges', last.prestiges, 'ups', (last.ups.match(/1/g) || []).length, 'ach', (last.ach.match(/1/g) || []).length, last.fmt);
