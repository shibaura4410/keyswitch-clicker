using UdonSharp;
using UnityEngine;

// 看板やスイッチに付ける押しボタン。押されたことを本体へ伝えるだけ
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class KscButton : UdonSharpBehaviour
{
    public KeyswitchCrusher game;
    public int kind;    // 0=スイッチ, 1=金のキー, 2=店のタブ, 3=店の行, 4=店のページ, 5=まとめ買い, 6=看板のタブ, 7=看板のページ, 8=看板のボタン
    public int index;

    public override void Interact()
    {
        game.Press(kind, index);
    }
}
