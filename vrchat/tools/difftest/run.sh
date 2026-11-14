#!/bin/sh
# ブラウザ版(index.html)と VRChat 版(KeyswitchCrusher.cs)に同じ操作の列を流し、1 操作ごとの状態が一致するかを比べる。
# Node.js と .NET SDK 8 が要る。Unity は要らない(Unity や VRChat の部品は cs/Stub.cs の代用品に置き換えている)。
#   sh vrchat/tools/difftest/run.sh [乱数の種の数(標準 4)]
set -e
cd "$(dirname "$0")"
TMP=$(mktemp -d)
dotnet build cs -c Release -v q -nologo -o "$TMP/bin" | grep -E " error |rror\(s\)" || true
fail=0
for seed in $(seq 1 "${1:-4}"); do
  node genops.js "$seed" 400 "$TMP/ops.json" > /dev/null
  node jsref.js "$TMP/ops.json" "$TMP/js.txt" > /dev/null
  dotnet "$TMP/bin/cs.dll" "$TMP/ops.json" "$TMP/cs.txt" | grep -i "round trip\|mismatch"
  node cmp.js "$TMP/js.txt" "$TMP/cs.txt" | tail -1 | sed "s/^/seed $seed: /"
  node cmp.js "$TMP/js.txt" "$TMP/cs.txt" | grep -q "^MISMATCH" && fail=1
done
rm -rf "$TMP"
exit $fail
