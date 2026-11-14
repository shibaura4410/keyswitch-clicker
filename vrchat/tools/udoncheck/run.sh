#!/bin/sh
# KeyswitchCrusher.cs と KscButton.cs が呼ぶ Unity・VRChat の命令が、Udon で使えるものかを調べる。
# VRChat SDK(Worlds・Base)と Unity の参照用ライブラリを .cache に取り寄せ、本物の SDK を相手に型を解決したうえで、
# SDK に入っている「Udon が公開している命令」の一覧と突き合わせる。Unity は要らない。.NET SDK 8・curl・unzip が要る。
# UdonSharp そのものの文法の制限(使えない書き方)までは調べない。それは Unity 上のコンパイルで確かめること。
#   sh vrchat/tools/udoncheck/run.sh [SDK の版(標準 3.9.0)]
set -e
cd "$(dirname "$0")"
VER="${1:-3.9.0}"
C="$PWD/.cache/$VER"
mkdir -p "$C"
if [ ! -d "$C/worlds" ]; then
  for p in worlds base; do
    curl -sSL -o "$C/$p.zip" "https://github.com/vrchat/packages/releases/download/$VER/com.vrchat.$p-$VER.zip"
    mkdir -p "$C/$p" && unzip -q -o "$C/$p.zip" -d "$C/$p" && rm "$C/$p.zip"
  done
  curl -sSL -o "$C/ue.zip" https://api.nuget.org/v3-flatcontainer/unityengine.modules/2021.3.33/unityengine.modules.2021.3.33.nupkg
  curl -sSL -o "$C/ui.zip" https://api.nuget.org/v3-flatcontainer/unity3d.unityengine.ui/2020.3.21/unity3d.unityengine.ui.2020.3.21.nupkg
  mkdir -p "$C/ue" "$C/ui" && unzip -q -o "$C/ue.zip" -d "$C/ue" && unzip -q -o "$C/ui.zip" -d "$C/ui" && rm "$C/ue.zip" "$C/ui.zip"
fi
dotnet build -c Release -v q -nologo -o "$C/bin" | grep -E " error |rror\(s\)" || true
W="$C/worlds/Runtime/Udon"
# Udon が公開している命令: ラッパーの DLL と、ソースで入っているラッパー
dotnet "$C/bin/udoncheck.dll" dump "$W/External/VRC.Udon.Wrapper.dll" "$W/External/VRC.Udon.VRCWrapperModules.dll" > "$C/exposed.txt"
for f in "$W"/WrapperModules/*.cs; do
  c=$(basename "$f" .cs)
  grep -o "__[A-Za-z0-9_]*__[A-Za-z0-9_]*" "$f" | sort -u | sed "s|^|$c.|"
done >> "$C/exposed.txt"
{ ls "$PWD"/../../Assets/KeyswitchCrusher/Scripts/*.cs "$C"/worlds/Integrations/UdonSharp/Runtime/*.cs
  find "$W" -name "*.cs" -not -path "*/Editor/*"; } > "$C/srcs.txt"
{ ls "$C"/ue/lib/netstandard2.0/*.dll "$C"/ui/lib/UnityEngine.UI.dll \
     "$C"/base/Runtime/VRCSDK/Plugins/VRCSDKBase.dll "$C"/base/Runtime/VRCSDK/Plugins/VRC.Utility.dll "$C"/base/Runtime/VRCSDK/Plugins/VRCCore-Standalone.dll \
     "$C"/worlds/Runtime/VRCSDK/Plugins/VRCSDK3.dll "$C"/worlds/Runtime/VRCSDK/Plugins/VRCEconomy.dll \
     "$W"/External/*.dll "$C"/base/Runtime/VRCSDK/Dependencies/Managed/*.dll
  ls "$(dirname "$(find "$(dirname "$(command -v dotnet | xargs readlink -f)")" /usr/lib/dotnet /usr/share/dotnet -name netstandard.dll -path "*NETStandard.Library.Ref*" 2>/dev/null | head -1)")"/*.dll; } > "$C/refs.txt"
dotnet "$C/bin/udoncheck.dll" "$C/exposed.txt" "$C/srcs.txt" "$C/refs.txt"
