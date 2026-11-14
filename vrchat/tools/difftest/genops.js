// 乱数の種から、叩く・待つ・買う・金のキー・周回を混ぜた操作の列を作る
const fs = require('fs');
let seed = Number(process.argv[2] || 1);
const R = () => { seed = (seed * 1103515245 + 12345) & 0x7fffffff; return seed / 0x7fffffff; };
const ops = [];
const rounds = Number(process.argv[3] || 300);
for (let r = 0; r < rounds; r++) {
  const burst = 5 + Math.floor(R() * 40);
  const gap = R() < 0.3 ? 0.03 : R() < 0.5 ? 0.2 : 0.7;
  for (let i = 0; i < burst; i++) {
    if (R() < 0.3) ops.push(['key']);
    ops.push(['hit', R() < 0.15]);
    ops.push(['tick', gap * (0.5 + R())]);
  }
  ops.push(['mode', Math.floor(R() * 4)]);
  for (let i = 0; i < 23; i++) if (R() < 0.7) ops.push(['buy', Math.floor(R() * 23)]);
  for (let i = 0; i < 160; i++) if (R() < 0.5) ops.push(['up', i]);
  if (R() < 0.3) ops.push(['gold', R()]);
  const n = Math.floor(R() * 4);
  for (let i = 0; i < n; i++) ops.push(['tick', 20 + R() * 900]);
  if (R() < 0.15) ops.push(['tick', 4000]);
  if (R() < 0.4) ops.push(['ps']);
}
fs.writeFileSync(process.argv[4], JSON.stringify(ops));
console.log('ops', ops.length);
