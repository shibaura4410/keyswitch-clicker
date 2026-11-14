const fs = require('fs');
const [a, b] = [2, 3].map(i => fs.readFileSync(process.argv[i], 'utf8').trim().split('\n'));
let bad = 0, maxRel = 0, fmtDiff = 0;
if (a.length !== b.length) { console.log('length differs', a.length, b.length); bad++; }
for (let i = 0; i < Math.min(a.length, b.length) && bad < 5; i++) {
  const x = JSON.parse(a[i]), y = JSON.parse(b[i]);
  for (const k of Object.keys(x)) {
    // 丸めの差で表示の末尾が 1 だけずれることがある。数そのものは下で比べる
    if (k === 'fmt') { if (x[k] !== y[k]) fmtDiff++; continue; }
    if (typeof x[k] === 'number') {
      const rel = Math.abs(x[k] - y[k]) / Math.max(1, Math.abs(x[k]), x.total); // 大きな数どうしの引き算で出る丸めの差は、累計に対する比で見る
      maxRel = Math.max(maxRel, rel);
      if (rel > 1e-9) { console.log('DIFF at op', i, k, x[k], y[k]); bad++; }
    } else if (x[k] !== y[k]) { console.log('DIFF at op', i, k, '\n ', x[k], '\n ', y[k]); bad++; }
  }
}
console.log(bad ? 'MISMATCH' : 'all ' + a.length + ' snapshots match', 'maxRelErr', maxRel, 'fmtDiff', fmtDiff);
