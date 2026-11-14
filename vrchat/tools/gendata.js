// index.html のデータ部分を読み、KeyswitchCrusher.cs の InitData() の表を書き換える。
// ブラウザ版の工具・強化・実績・軸を変えたら、これを実行してから difftest/run.sh で確かめる。
//   node vrchat/tools/gendata.js
const fs = require('fs'), path = require('path');
const ROOT = path.join(__dirname, '..', '..');
const CS = path.join(ROOT, 'vrchat/Assets/KeyswitchCrusher/Scripts/KeyswitchCrusher.cs');
const html = fs.readFileSync(path.join(ROOT, 'index.html'), 'utf8');
const block = html.slice(html.indexOf('const SWITCH_TYPES'), html.indexOf("const SAVE_KEY"));
const D = new Function('let S={}; const lv=()=>0, autoPower=()=>0, clickPower=()=>0, kbCount=()=>0; let achOwned=0;' + block +
  '; return {SWITCH_TYPES, ITEMS, UPGRADES, ACH, ACH_CATS, ORDER, KB_ROWS, DIFF};')();
const { SWITCH_TYPES, ITEMS, UPGRADES, ACH, ACH_CATS } = D;
const q = s => '"' + s.replace(/\\/g, '\\\\').replace(/"/g, '\\"') + '"';
const num = n => { const s = String(n); return /[.e]/.test(s) ? s : s + '.0'; };
const arr = (type, name, vals) => `        ${name} = new ${type}[] { ${vals.join(', ')} };`;
const idx = id => ITEMS.findIndex(i => i.id === id);
const out = [];
const kindN = { click: 0, crit: 1, auto: 2 };
out.push(arr('string', 'itName', ITEMS.map(i => q(i.name))));
out.push(arr('int', 'itKind', ITEMS.map(i => kindN[i.kind])));
out.push(arr('double', 'itVal', ITEMS.map(i => num(i.val))));
out.push(arr('double', 'itBase', ITEMS.map(i => num(i.base))));
out.push(arr('double', 'itGrowth', ITEMS.map(i => num(i.growth || 1.15))));
out.push(arr('int', 'itMax', ITEMS.map(i => i.max || 0)));
const hex = c => { const n = parseInt(c.slice(1), 16); return `new Color(${((n >> 16) / 255).toFixed(3)}f, ${((n >> 8 & 255) / 255).toFixed(3)}f, ${((n & 255) / 255).toFixed(3)}f)`; };
out.push(arr('string', 'swName', SWITCH_TYPES.map(t => q(t.name))));
out.push(arr('string', 'swKind', SWITCH_TYPES.map(t => q(t.kind))));
out.push(arr('Color', 'swColor', SWITCH_TYPES.map(t => hex(t.color))));
out.push(arr('int', 'swNeed', SWITCH_TYPES.map(t => t.need)));
// 強化
const groupN = { gclick: 1, gauto: 2, critx: 3, syn: 4, kbx: 5, achx: 6, combo: 7 };
const ups = UPGRADES.map(u => {
  const val = Number(u.need.replace(/,/g, '').match(/[\d.]+/)[0]);
  return { name: u.name, desc: u.desc, need: u.need, cost: u.cost, group: u.tool ? 0 : groupN[u.group], arg: u.tool ? idx(u.tool) : (u.cap || 0), val };
});
if (ups.some(u => u.group === undefined || !Number.isFinite(u.val))) throw new Error('upgrade parse');
out.push(arr('string', 'upName', ups.map(u => q(u.name))));
out.push(arr('string', 'upDesc', ups.map(u => q(u.desc))));
out.push(arr('string', 'upNeed', ups.map(u => q(u.need))));
out.push(arr('double', 'upCost', ups.map(u => num(u.cost))));
out.push(arr('int', 'upGroup', ups.map(u => u.group)));
out.push(arr('int', 'upArg', ups.map(u => u.arg)));
out.push(arr('double', 'upReq', ups.map(u => num(u.val))));
// 実績
const F = { clicks: 0, crits: 1, brokenAll: 2, kbAll: 3, prestiges: 4, gold: 5, total: 6, maxCombo: 13, keys: 14, maxHit: 15, level: 17, bp: 19, playSec: 20 };
const ach = ACH.map(a => {
  const s = a.ok.toString().replace(/^\(\) => /, '');
  let m, stat, arg = 0, val = 0;
  if ((m = s.match(/^S\.(\w+) >= ([\d.e]+)$/))) { stat = F[m[1]]; val = Number(m[2]); }
  else if (s === 'S.fast') { stat = 7; val = 1; }
  else if (s === 'S.kbAll >= SWITCH_TYPES.length - 1') { stat = 3; val = SWITCH_TYPES.length - 1; }
  else if (/every/.test(s) && /'click'/.test(s)) { stat = 8; val = 1; }
  else if (/every/.test(s) && /'auto'/.test(s)) { stat = 9; val = 1; }
  else if ((m = s.match(/^lv\('(\w+)'\) >= (\d+)$/))) { stat = 10; arg = idx(m[1]); val = Number(m[2]); }
  else if ((m = s.match(/^autoPower\(\) >= ([\d.e]+)$/))) { stat = 11; val = Number(m[1]); }
  else if ((m = s.match(/^upCount\(\) >= (\d+)$/))) { stat = 12; val = Number(m[1]); }
  else if (s === 'upCount() >= UPGRADES.length') { stat = 12; val = UPGRADES.length; }
  else if ((m = s.match(/^clickPower\(\) >= ([\d.e]+)$/))) { stat = 16; val = Number(m[1]); }
  else if ((m = s.match(/^ownedAll\(\) >= (\d+)$/))) { stat = 18; val = Number(m[1]); }
  else if (s === 'lv(it.id) >= 50' || s === 'lv(it.id) >= 100') { stat = 10; val = Number(s.match(/\d+$/)[0]); arg = idx(a.id.slice(0, -String(val).length)); }
  if (stat === undefined || arg < 0) throw new Error('ach parse: ' + a.id + ' ' + s);
  return { id: a.id, name: a.name, desc: a.desc, stat, arg, val, cat: ACH_CATS.findIndex(([, re]) => re.test(a.id)) };
});
if (ach.some(a => a.cat < 0)) throw new Error('ach cat');
out.push(arr('string', 'achName', ach.map(a => q(a.name))));
out.push(arr('string', 'achDesc', ach.map(a => q(a.desc.replace('キーボードで', 'キー入力か手で')))));
out.push(arr('int', 'achStat', ach.map(a => a.stat)));
out.push(arr('int', 'achArg', ach.map(a => a.arg)));
out.push(arr('double', 'achVal', ach.map(a => num(a.val))));
out.push(arr('int', 'achCat', ach.map(a => a.cat)));
out.push(arr('string', 'achCatName', ACH_CATS.map(c => q(c[0]))));
const order = ach.map((a, i) => i).sort((x, y) => ach[x].cat - ach[y].cat || x - y);
out.push(arr('int', 'achOrder', order));
{
  const byCost = ups.map((u, i) => i).sort((x, y) => ups[x].cost - ups[y].cost || x - y);
  out.push(arr('int', 'upByCost', byCost));
}
const cs = fs.readFileSync(CS, 'utf8');
const from = cs.indexOf('        itName = new string[]'), to = cs.indexOf('        UNITS = new string[]');
if (from < 0 || to < from) throw new Error('KeyswitchCrusher.cs の InitData() が見つかりません');
fs.writeFileSync(CS, cs.slice(0, from) + out.join('\n') + '\n' + cs.slice(to));
console.log(`工具・自動破壊 ${ITEMS.length} 種、強化 ${ups.length} 個、実績 ${ach.length} 個、軸 ${SWITCH_TYPES.length} 種を書き出しました`);
