using System;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.SDK3.Persistence;

// キースイッチ・クラッシャー(VRChat ワールド版)
// ブラウザ版(index.html)の仕組みをそのまま移したもの。1 人ずつ別々に進み、
// 進み具合は VRChat の保存機能(PlayerData)でプレイヤーごとに残る。
// 数字や並びを変えるときは、ブラウザ版と食い違わないよう両方を直すこと。
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class KeyswitchCrusher : UdonSharpBehaviour
{
    // ---------- シーン上の部品(組み立てスクリプトがつなぐ) ----------
    [Header("スイッチ")]
    public Transform keycap;        // 叩くと奥へ沈むキーキャップ
    public Transform hitCenter;     // 手で叩く判定と破片の中心
    public Text legend;             // キーキャップの文字
    public GameObject[] cracks;     // ひび 3 段階(耐久 75%・50%・25% を切ると出る)
    public Material stemMat;        // 軸の色
    public Material glowMat;        // バックライトの色(キーボードごとに変わる)
    public ParticleSystem fx;       // 破片
    public float pressDepth = 0.05f;
    public float punchRadius = 0.3f;

    [Header("表示")]
    public Text txtTarget;
    public Text txtSub;
    public Text txtHp;
    public Transform hpFill;
    public Text txtScrap;
    public Text txtPower;
    public Text txtFever;
    public Text txtCombo;
    public Text txtToast;
    public Text txtKbd;
    public Text[] floats;           // 飛び出すダメージの数字
    public Renderer[] kbKeys;       // 36 キーの並び(A→Z、1→0 の順)
    public Material matKey;
    public Material matKeyDone;

    [Header("工具店")]
    public Text shopTitle;
    public Text shopPage;
    public Text txtBuyMode;
    public Text[] rowName;
    public Text[] rowInfo;
    public Text[] rowCost;
    public GameObject[] rowBtn;

    [Header("記録の看板")]
    public Text infoTitle;
    public Text infoBody;
    public Text infoPage;
    public Text[] actLabel;
    public GameObject[] actBtn;

    [Header("金のキー")]
    public GameObject goldKey;

    [Header("音")]
    public AudioSource srcHit;
    public AudioSource srcAuto;
    public AudioSource srcBreak;
    public AudioClip[] hitClips;    // 軸 10 種 × 6 個(軸の並びは SWITCH_TYPES と同じ)
    public AudioClip breakSmall;
    public AudioClip breakBig;
    public float hitGain = 0.5f;

    // ---------- データ ----------
    private const string ORDER = "ABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890";
    private const int KEYS = 36;
    private const double DIFF = 5;              // 難易度: キーの耐久と値段にまとめて掛ける倍率
    private const int PS_MIN = 8, PS_STEP = 2;  // 周回の条件: 1 回目は 8 台、以降 2 台ずつ増える
    private const int BP_PER_RUN = 3, BP_PER_OVER = 1;
    private const double BP_BONUS = 0.05;       // 設計図 1 枚につき全ダメージ +5%
    private const double COMBO_S = 0.6;         // これより間があくと連打ボーナスは切れる
    private const int CLIPS_PER_AXIS = 6;
    private const float AUTO_VOL = 0.4f;
    private const int ROWS = 8;                 // 工具店に 1 度に並ぶ数
    private const int ACH_PER_PAGE = 10;
    private const int IT_CRIT = 10;             // ITEMS のうち「急所ねらい」の位置

    private string[] itName; private int[] itKind; private double[] itVal; private double[] itBase;
    private double[] itGrowth; private int[] itMax;
    private string[] swName; private string[] swKind; private Color[] swColor; private int[] swNeed;
    private string[] upName; private string[] upDesc; private string[] upNeed; private double[] upCost;
    private int[] upGroup; private int[] upArg; private double[] upReq; private int[] upByCost;
    private string[] achName; private string[] achDesc; private int[] achStat; private int[] achArg;
    private double[] achVal; private int[] achCat; private string[] achCatName; private int[] achOrder;
    private string[] UNITS;

    // index.html から機械的に書き出した表。手で直さず、ブラウザ版を直してから書き出し直す
    private void InitData()
    {
        itName = new string[] { "指トレ", "キープラー", "マイナスドライバー", "ハンマー", "電動ドリル", "チェーンソー", "削岩機", "ダイナマイト", "解体用の鉄球", "隕石", "急所ねらい", "キーボードに乗る猫", "こぼれたコーヒー", "台パン", "はんだごて", "油圧プレス", "衛星レーザー", "酸の雨", "火山", "ロボット軍団", "小型ブラックホール", "経年劣化の加速", "宇宙の収縮" };
        itKind = new int[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2 };
        itVal = new double[] { 1.0, 5.0, 25.0, 120.0, 600.0, 3000.0, 15000.0, 75000.0, 400000.0, 2000000.0, 2.0, 5.0, 30.0, 150.0, 750.0, 4000.0, 22500.0, 120000.0, 650000.0, 3500000.0, 20000000.0, 110000000.0, 600000000.0 };
        itBase = new double[] { 10.0, 120.0, 1500.0, 15000.0, 150000.0, 1500000.0, 15000000.0, 150000000.0, 1500000000.0, 15000000000.0, 250.0, 30.0, 400.0, 3500.0, 30000.0, 250000.0, 2500000.0, 25000000.0, 250000000.0, 2500000000.0, 25000000000.0, 250000000000.0, 2500000000000.0 };
        itGrowth = new double[] { 1.15, 1.15, 1.15, 1.15, 1.15, 1.15, 1.15, 1.15, 1.15, 1.15, 1.3, 1.15, 1.15, 1.15, 1.15, 1.15, 1.15, 1.15, 1.15, 1.15, 1.15, 1.15, 1.15 };
        itMax = new int[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 25, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
        swName = new string[] { "青軸", "赤軸", "茶軸", "黒軸", "銀軸", "桃軸", "緑軸", "紫軸", "金軸", "白軸" };
        swKind = new string[] { "クリッキー", "リニア", "タクタイル", "重めのリニア", "スピード", "静音", "重めのクリッキー", "光学式", "磁気式", "静電容量無接点" };
        swColor = new Color[] { new Color(0.239f, 0.482f, 0.878f), new Color(0.878f, 0.267f, 0.243f), new Color(0.541f, 0.353f, 0.235f), new Color(0.165f, 0.165f, 0.180f), new Color(0.788f, 0.808f, 0.847f), new Color(0.941f, 0.604f, 0.722f), new Color(0.247f, 0.682f, 0.353f), new Color(0.561f, 0.373f, 0.878f), new Color(0.878f, 0.698f, 0.239f), new Color(0.949f, 0.949f, 0.949f) };
        swNeed = new int[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };
        upName = new string[] { "指トレ・改", "指トレ・真", "指トレ・極", "指トレ・神", "指トレ・超", "指トレ・絶", "キープラー・改", "キープラー・真", "キープラー・極", "キープラー・神", "キープラー・超", "キープラー・絶", "マイナスドライバー・改", "マイナスドライバー・真", "マイナスドライバー・極", "マイナスドライバー・神", "マイナスドライバー・超", "マイナスドライバー・絶", "ハンマー・改", "ハンマー・真", "ハンマー・極", "ハンマー・神", "ハンマー・超", "ハンマー・絶", "電動ドリル・改", "電動ドリル・真", "電動ドリル・極", "電動ドリル・神", "電動ドリル・超", "電動ドリル・絶", "チェーンソー・改", "チェーンソー・真", "チェーンソー・極", "チェーンソー・神", "チェーンソー・超", "チェーンソー・絶", "削岩機・改", "削岩機・真", "削岩機・極", "削岩機・神", "削岩機・超", "削岩機・絶", "ダイナマイト・改", "ダイナマイト・真", "ダイナマイト・極", "ダイナマイト・神", "ダイナマイト・超", "ダイナマイト・絶", "解体用の鉄球・改", "解体用の鉄球・真", "解体用の鉄球・極", "解体用の鉄球・神", "解体用の鉄球・超", "解体用の鉄球・絶", "隕石・改", "隕石・真", "隕石・極", "隕石・神", "隕石・超", "隕石・絶", "キーボードに乗る猫・改", "キーボードに乗る猫・真", "キーボードに乗る猫・極", "キーボードに乗る猫・神", "キーボードに乗る猫・超", "キーボードに乗る猫・絶", "こぼれたコーヒー・改", "こぼれたコーヒー・真", "こぼれたコーヒー・極", "こぼれたコーヒー・神", "こぼれたコーヒー・超", "こぼれたコーヒー・絶", "台パン・改", "台パン・真", "台パン・極", "台パン・神", "台パン・超", "台パン・絶", "はんだごて・改", "はんだごて・真", "はんだごて・極", "はんだごて・神", "はんだごて・超", "はんだごて・絶", "油圧プレス・改", "油圧プレス・真", "油圧プレス・極", "油圧プレス・神", "油圧プレス・超", "油圧プレス・絶", "衛星レーザー・改", "衛星レーザー・真", "衛星レーザー・極", "衛星レーザー・神", "衛星レーザー・超", "衛星レーザー・絶", "酸の雨・改", "酸の雨・真", "酸の雨・極", "酸の雨・神", "酸の雨・超", "酸の雨・絶", "火山・改", "火山・真", "火山・極", "火山・神", "火山・超", "火山・絶", "ロボット軍団・改", "ロボット軍団・真", "ロボット軍団・極", "ロボット軍団・神", "ロボット軍団・超", "ロボット軍団・絶", "小型ブラックホール・改", "小型ブラックホール・真", "小型ブラックホール・極", "小型ブラックホール・神", "小型ブラックホール・超", "小型ブラックホール・絶", "経年劣化の加速・改", "経年劣化の加速・真", "経年劣化の加速・極", "経年劣化の加速・神", "経年劣化の加速・超", "経年劣化の加速・絶", "宇宙の収縮・改", "宇宙の収縮・真", "宇宙の収縮・極", "宇宙の収縮・神", "宇宙の収縮・超", "宇宙の収縮・絶", "工具の焼き入れ I", "工具の焼き入れ II", "工具の焼き入れ III", "工具の焼き入れ IV", "工具の焼き入れ V", "工具の焼き入れ VI", "過負荷運転 I", "過負荷運転 II", "過負荷運転 III", "過負荷運転 IV", "過負荷運転 V", "過負荷運転 VI", "会心の極意 I", "会心の極意 II", "会心の極意 III", "猫の手も借りる I", "猫の手も借りる II", "解体の心得 I", "解体の心得 II", "解体の心得 III", "解体の心得 IV", "職人の誇り I", "職人の誇り II", "職人の誇り III", "職人の誇り IV", "連打の呼吸 I", "連打の呼吸 II", "連打の呼吸 III" };
        upDesc = new string[] { "指トレの効果 2倍", "指トレの効果 2倍", "指トレの効果 2倍", "指トレの効果 2倍", "指トレの効果 2倍", "指トレの効果 2倍", "キープラーの効果 2倍", "キープラーの効果 2倍", "キープラーの効果 2倍", "キープラーの効果 2倍", "キープラーの効果 2倍", "キープラーの効果 2倍", "マイナスドライバーの効果 2倍", "マイナスドライバーの効果 2倍", "マイナスドライバーの効果 2倍", "マイナスドライバーの効果 2倍", "マイナスドライバーの効果 2倍", "マイナスドライバーの効果 2倍", "ハンマーの効果 2倍", "ハンマーの効果 2倍", "ハンマーの効果 2倍", "ハンマーの効果 2倍", "ハンマーの効果 2倍", "ハンマーの効果 2倍", "電動ドリルの効果 2倍", "電動ドリルの効果 2倍", "電動ドリルの効果 2倍", "電動ドリルの効果 2倍", "電動ドリルの効果 2倍", "電動ドリルの効果 2倍", "チェーンソーの効果 2倍", "チェーンソーの効果 2倍", "チェーンソーの効果 2倍", "チェーンソーの効果 2倍", "チェーンソーの効果 2倍", "チェーンソーの効果 2倍", "削岩機の効果 2倍", "削岩機の効果 2倍", "削岩機の効果 2倍", "削岩機の効果 2倍", "削岩機の効果 2倍", "削岩機の効果 2倍", "ダイナマイトの効果 2倍", "ダイナマイトの効果 2倍", "ダイナマイトの効果 2倍", "ダイナマイトの効果 2倍", "ダイナマイトの効果 2倍", "ダイナマイトの効果 2倍", "解体用の鉄球の効果 2倍", "解体用の鉄球の効果 2倍", "解体用の鉄球の効果 2倍", "解体用の鉄球の効果 2倍", "解体用の鉄球の効果 2倍", "解体用の鉄球の効果 2倍", "隕石の効果 2倍", "隕石の効果 2倍", "隕石の効果 2倍", "隕石の効果 2倍", "隕石の効果 2倍", "隕石の効果 2倍", "キーボードに乗る猫の効果 2倍", "キーボードに乗る猫の効果 2倍", "キーボードに乗る猫の効果 2倍", "キーボードに乗る猫の効果 2倍", "キーボードに乗る猫の効果 2倍", "キーボードに乗る猫の効果 2倍", "こぼれたコーヒーの効果 2倍", "こぼれたコーヒーの効果 2倍", "こぼれたコーヒーの効果 2倍", "こぼれたコーヒーの効果 2倍", "こぼれたコーヒーの効果 2倍", "こぼれたコーヒーの効果 2倍", "台パンの効果 2倍", "台パンの効果 2倍", "台パンの効果 2倍", "台パンの効果 2倍", "台パンの効果 2倍", "台パンの効果 2倍", "はんだごての効果 2倍", "はんだごての効果 2倍", "はんだごての効果 2倍", "はんだごての効果 2倍", "はんだごての効果 2倍", "はんだごての効果 2倍", "油圧プレスの効果 2倍", "油圧プレスの効果 2倍", "油圧プレスの効果 2倍", "油圧プレスの効果 2倍", "油圧プレスの効果 2倍", "油圧プレスの効果 2倍", "衛星レーザーの効果 2倍", "衛星レーザーの効果 2倍", "衛星レーザーの効果 2倍", "衛星レーザーの効果 2倍", "衛星レーザーの効果 2倍", "衛星レーザーの効果 2倍", "酸の雨の効果 2倍", "酸の雨の効果 2倍", "酸の雨の効果 2倍", "酸の雨の効果 2倍", "酸の雨の効果 2倍", "酸の雨の効果 2倍", "火山の効果 2倍", "火山の効果 2倍", "火山の効果 2倍", "火山の効果 2倍", "火山の効果 2倍", "火山の効果 2倍", "ロボット軍団の効果 2倍", "ロボット軍団の効果 2倍", "ロボット軍団の効果 2倍", "ロボット軍団の効果 2倍", "ロボット軍団の効果 2倍", "ロボット軍団の効果 2倍", "小型ブラックホールの効果 2倍", "小型ブラックホールの効果 2倍", "小型ブラックホールの効果 2倍", "小型ブラックホールの効果 2倍", "小型ブラックホールの効果 2倍", "小型ブラックホールの効果 2倍", "経年劣化の加速の効果 2倍", "経年劣化の加速の効果 2倍", "経年劣化の加速の効果 2倍", "経年劣化の加速の効果 2倍", "経年劣化の加速の効果 2倍", "経年劣化の加速の効果 2倍", "宇宙の収縮の効果 2倍", "宇宙の収縮の効果 2倍", "宇宙の収縮の効果 2倍", "宇宙の収縮の効果 2倍", "宇宙の収縮の効果 2倍", "宇宙の収縮の効果 2倍", "すべての工具の威力 2倍", "すべての工具の威力 2倍", "すべての工具の威力 2倍", "すべての工具の威力 2倍", "すべての工具の威力 2倍", "すべての工具の威力 2倍", "すべての自動破壊 2倍", "すべての自動破壊 2倍", "すべての自動破壊 2倍", "すべての自動破壊 2倍", "すべての自動破壊 2倍", "すべての自動破壊 2倍", "会心の倍率 +5倍", "会心の倍率 +5倍", "会心の倍率 +5倍", "1打に毎秒ダメージの3%を上乗せ", "1打に毎秒ダメージの3%を上乗せ", "すべてのダメージ 1.5倍", "すべてのダメージ 1.5倍", "すべてのダメージ 1.5倍", "すべてのダメージ 1.5倍", "実績1個につき、すべてのダメージ +1%", "実績1個につき、すべてのダメージ +1%", "実績1個につき、すべてのダメージ +1%", "実績1個につき、すべてのダメージ +1%", "連打すると1打ごとに威力 +1%(最大 +50%)", "連打すると1打ごとに威力 +1%(最大 +100%)", "連打すると1打ごとに威力 +1%(最大 +200%)" };
        upNeed = new string[] { "指トレを10個", "指トレを25個", "指トレを50個", "指トレを100個", "指トレを150個", "指トレを200個", "キープラーを10個", "キープラーを25個", "キープラーを50個", "キープラーを100個", "キープラーを150個", "キープラーを200個", "マイナスドライバーを10個", "マイナスドライバーを25個", "マイナスドライバーを50個", "マイナスドライバーを100個", "マイナスドライバーを150個", "マイナスドライバーを200個", "ハンマーを10個", "ハンマーを25個", "ハンマーを50個", "ハンマーを100個", "ハンマーを150個", "ハンマーを200個", "電動ドリルを10個", "電動ドリルを25個", "電動ドリルを50個", "電動ドリルを100個", "電動ドリルを150個", "電動ドリルを200個", "チェーンソーを10個", "チェーンソーを25個", "チェーンソーを50個", "チェーンソーを100個", "チェーンソーを150個", "チェーンソーを200個", "削岩機を10個", "削岩機を25個", "削岩機を50個", "削岩機を100個", "削岩機を150個", "削岩機を200個", "ダイナマイトを10個", "ダイナマイトを25個", "ダイナマイトを50個", "ダイナマイトを100個", "ダイナマイトを150個", "ダイナマイトを200個", "解体用の鉄球を10個", "解体用の鉄球を25個", "解体用の鉄球を50個", "解体用の鉄球を100個", "解体用の鉄球を150個", "解体用の鉄球を200個", "隕石を10個", "隕石を25個", "隕石を50個", "隕石を100個", "隕石を150個", "隕石を200個", "キーボードに乗る猫を10個", "キーボードに乗る猫を25個", "キーボードに乗る猫を50個", "キーボードに乗る猫を100個", "キーボードに乗る猫を150個", "キーボードに乗る猫を200個", "こぼれたコーヒーを10個", "こぼれたコーヒーを25個", "こぼれたコーヒーを50個", "こぼれたコーヒーを100個", "こぼれたコーヒーを150個", "こぼれたコーヒーを200個", "台パンを10個", "台パンを25個", "台パンを50個", "台パンを100個", "台パンを150個", "台パンを200個", "はんだごてを10個", "はんだごてを25個", "はんだごてを50個", "はんだごてを100個", "はんだごてを150個", "はんだごてを200個", "油圧プレスを10個", "油圧プレスを25個", "油圧プレスを50個", "油圧プレスを100個", "油圧プレスを150個", "油圧プレスを200個", "衛星レーザーを10個", "衛星レーザーを25個", "衛星レーザーを50個", "衛星レーザーを100個", "衛星レーザーを150個", "衛星レーザーを200個", "酸の雨を10個", "酸の雨を25個", "酸の雨を50個", "酸の雨を100個", "酸の雨を150個", "酸の雨を200個", "火山を10個", "火山を25個", "火山を50個", "火山を100個", "火山を150個", "火山を200個", "ロボット軍団を10個", "ロボット軍団を25個", "ロボット軍団を50個", "ロボット軍団を100個", "ロボット軍団を150個", "ロボット軍団を200個", "小型ブラックホールを10個", "小型ブラックホールを25個", "小型ブラックホールを50個", "小型ブラックホールを100個", "小型ブラックホールを150個", "小型ブラックホールを200個", "経年劣化の加速を10個", "経年劣化の加速を25個", "経年劣化の加速を50個", "経年劣化の加速を100個", "経年劣化の加速を150個", "経年劣化の加速を200個", "宇宙の収縮を10個", "宇宙の収縮を25個", "宇宙の収縮を50個", "宇宙の収縮を100個", "宇宙の収縮を150個", "宇宙の収縮を200個", "スイッチを5個破壊", "スイッチを20個破壊", "スイッチを50個破壊", "スイッチを100個破壊", "スイッチを180個破壊", "スイッチを300個破壊", "スイッチを10個破壊", "スイッチを30個破壊", "スイッチを70個破壊", "スイッチを140個破壊", "スイッチを220個破壊", "スイッチを320個破壊", "急所ねらいを5個", "急所ねらいを15個", "急所ねらいを25個", "毎秒ダメージ 50", "毎秒ダメージ 5,000", "キーボードを1台破壊", "キーボードを2台破壊", "キーボードを4台破壊", "キーボードを6台破壊", "実績を5個", "実績を10個", "実績を15個", "実績を20個", "500回叩く", "5,000回叩く", "20,000回叩く" };
        upCost = new double[] { 600.0, 7500.0, 125000.0, 150000000.0, 150000000000.0, 150000000000000.0, 7200.0, 90000.0, 1500000.0, 1800000000.0, 1800000000000.0, 1800000000000000.0, 90000.0, 1125000.0, 18750000.0, 22500000000.0, 22500000000000.0, 22500000000000000.0, 900000.0, 11250000.0, 187500000.0, 225000000000.0, 225000000000000.0, 225000000000000000.0, 9000000.0, 112500000.0, 1875000000.0, 2250000000000.0, 2250000000000000.0, 2250000000000000000.0, 90000000.0, 1125000000.0, 18750000000.0, 22500000000000.0, 22500000000000000.0, 22500000000000000000.0, 900000000.0, 11250000000.0, 187500000000.0, 225000000000000.0, 225000000000000000.0, 225000000000000000000.0, 9000000000.0, 112500000000.0, 1875000000000.0, 2250000000000000.0, 2250000000000000000.0, 2.25e+21, 90000000000.0, 1125000000000.0, 18750000000000.0, 22500000000000000.0, 22500000000000000000.0, 2.25e+22, 900000000000.0, 11250000000000.0, 187500000000000.0, 225000000000000000.0, 225000000000000000000.0, 2.25e+23, 1800.0, 22500.0, 375000.0, 450000000.0, 450000000000.0, 450000000000000.0, 24000.0, 300000.0, 5000000.0, 6000000000.0, 6000000000000.0, 6000000000000000.0, 210000.0, 2625000.0, 43750000.0, 52500000000.0, 52500000000000.0, 52500000000000000.0, 1800000.0, 22500000.0, 375000000.0, 450000000000.0, 450000000000000.0, 450000000000000000.0, 15000000.0, 187500000.0, 3125000000.0, 3750000000000.0, 3750000000000000.0, 3750000000000000000.0, 150000000.0, 1875000000.0, 31250000000.0, 37500000000000.0, 37500000000000000.0, 37500000000000000000.0, 1500000000.0, 18750000000.0, 312500000000.0, 375000000000000.0, 375000000000000000.0, 375000000000000000000.0, 15000000000.0, 187500000000.0, 3125000000000.0, 3750000000000000.0, 3750000000000000000.0, 3.75e+21, 150000000000.0, 1875000000000.0, 31250000000000.0, 37500000000000000.0, 37500000000000000000.0, 3.75e+22, 1500000000000.0, 18750000000000.0, 312500000000000.0, 375000000000000000.0, 375000000000000000000.0, 3.75e+23, 15000000000000.0, 187500000000000.0, 3125000000000000.0, 3750000000000000000.0, 3.75e+21, 3.75e+24, 150000000000000.0, 1875000000000000.0, 31250000000000000.0, 37500000000000000000.0, 3.75e+22, 3.75e+25, 25000.0, 2000000.0, 150000000.0, 15000000000.0, 1500000000000.0, 150000000000000.0, 100000.0, 7500000.0, 500000000.0, 50000000000.0, 5000000000000.0, 500000000000000.0, 150000.0, 25000000.0, 2500000000.0, 400000.0, 100000000.0, 1000000.0, 100000000.0, 25000000000.0, 5000000000000.0, 100000.0, 10000000.0, 1000000000.0, 100000000000.0, 25000.0, 5000000.0, 500000000.0 };
        upGroup = new int[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 2, 2, 3, 3, 3, 4, 4, 5, 5, 5, 5, 6, 6, 6, 6, 7, 7, 7 };
        upArg = new int[] { 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 2, 2, 3, 3, 3, 3, 3, 3, 4, 4, 4, 4, 4, 4, 5, 5, 5, 5, 5, 5, 6, 6, 6, 6, 6, 6, 7, 7, 7, 7, 7, 7, 8, 8, 8, 8, 8, 8, 9, 9, 9, 9, 9, 9, 11, 11, 11, 11, 11, 11, 12, 12, 12, 12, 12, 12, 13, 13, 13, 13, 13, 13, 14, 14, 14, 14, 14, 14, 15, 15, 15, 15, 15, 15, 16, 16, 16, 16, 16, 16, 17, 17, 17, 17, 17, 17, 18, 18, 18, 18, 18, 18, 19, 19, 19, 19, 19, 19, 20, 20, 20, 20, 20, 20, 21, 21, 21, 21, 21, 21, 22, 22, 22, 22, 22, 22, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 50, 100, 200 };
        upReq = new double[] { 10.0, 25.0, 50.0, 100.0, 150.0, 200.0, 10.0, 25.0, 50.0, 100.0, 150.0, 200.0, 10.0, 25.0, 50.0, 100.0, 150.0, 200.0, 10.0, 25.0, 50.0, 100.0, 150.0, 200.0, 10.0, 25.0, 50.0, 100.0, 150.0, 200.0, 10.0, 25.0, 50.0, 100.0, 150.0, 200.0, 10.0, 25.0, 50.0, 100.0, 150.0, 200.0, 10.0, 25.0, 50.0, 100.0, 150.0, 200.0, 10.0, 25.0, 50.0, 100.0, 150.0, 200.0, 10.0, 25.0, 50.0, 100.0, 150.0, 200.0, 10.0, 25.0, 50.0, 100.0, 150.0, 200.0, 10.0, 25.0, 50.0, 100.0, 150.0, 200.0, 10.0, 25.0, 50.0, 100.0, 150.0, 200.0, 10.0, 25.0, 50.0, 100.0, 150.0, 200.0, 10.0, 25.0, 50.0, 100.0, 150.0, 200.0, 10.0, 25.0, 50.0, 100.0, 150.0, 200.0, 10.0, 25.0, 50.0, 100.0, 150.0, 200.0, 10.0, 25.0, 50.0, 100.0, 150.0, 200.0, 10.0, 25.0, 50.0, 100.0, 150.0, 200.0, 10.0, 25.0, 50.0, 100.0, 150.0, 200.0, 10.0, 25.0, 50.0, 100.0, 150.0, 200.0, 10.0, 25.0, 50.0, 100.0, 150.0, 200.0, 5.0, 20.0, 50.0, 100.0, 180.0, 300.0, 10.0, 30.0, 70.0, 140.0, 220.0, 320.0, 5.0, 15.0, 25.0, 50.0, 5000.0, 1.0, 2.0, 4.0, 6.0, 5.0, 10.0, 15.0, 20.0, 500.0, 5000.0, 20000.0 };
        achName = new string[] { "はじめの一打", "百裂打ち", "千本ノック", "腱鞘炎予備軍", "目にも止まらぬ", "会心の手応え", "急所マニア", "初破壊", "解体屋", "スイッチの墓場", "一台まるごと", "壊し屋", "軸マニア", "生まれ変わり", "輪廻", "金のキー", "ゴールドラッシュ", "小銭かせぎ", "スクラップ長者", "スクラップ王", "工具コレクター", "全自動工場", "猫だまり", "ほったらかし名人", "道具は使いよう", "免許皆伝", "奥義", "乱れ打ち", "指が覚えている", "百万打", "タイピスト", "ブラインドタッチ", "速記者", "必殺仕事人", "一撃必殺", "千手", "雷鳴", "重い一撃", "痛恨の一撃", "隕石", "天変地異", "腕っぷし", "怪力", "剛腕", "解体工場", "部品の山", "千のスイッチ", "掃除屋", "廃品回収", "リサイクル工場", "軸コレクター", "長旅", "果てしない旅", "へそくり", "スクラップ財閥", "スクラップ帝国", "スクラップ銀河", "手放し運転", "寝ながら解体", "不労所得", "全自動の極み", "道具持ち", "倉庫番", "問屋", "百発百中", "達人", "極めし者", "常連", "終わらない解体", "設計士", "設計事務所", "設計図書館", "目ざとい", "黄金の指", "ひと休み", "没頭", "夜ふかし", "丸一日", "指トレの達人", "指トレの鬼", "キープラーの達人", "キープラーの鬼", "マイナスドライバーの達人", "マイナスドライバーの鬼", "ハンマーの達人", "ハンマーの鬼", "電動ドリルの達人", "電動ドリルの鬼", "チェーンソーの達人", "チェーンソーの鬼", "削岩機の達人", "削岩機の鬼", "ダイナマイトの達人", "ダイナマイトの鬼", "解体用の鉄球の達人", "解体用の鉄球の鬼", "隕石の達人", "隕石の鬼", "キーボードに乗る猫の達人", "キーボードに乗る猫の鬼", "こぼれたコーヒーの達人", "こぼれたコーヒーの鬼", "台パンの達人", "台パンの鬼", "はんだごての達人", "はんだごての鬼", "油圧プレスの達人", "油圧プレスの鬼", "衛星レーザーの達人", "衛星レーザーの鬼", "酸の雨の達人", "酸の雨の鬼", "火山の達人", "火山の鬼", "ロボット軍団の達人", "ロボット軍団の鬼", "小型ブラックホールの達人", "小型ブラックホールの鬼", "経年劣化の加速の達人", "経年劣化の加速の鬼", "宇宙の収縮の達人", "宇宙の収縮の鬼" };
        achDesc = new string[] { "スイッチを1回叩く", "100回叩く", "1,000回叩く", "10,000回叩く", "1秒間に10回叩く", "会心を出す", "会心を100回出す", "最初のスイッチを壊す", "スイッチを10個壊す", "スイッチを30個壊す", "キーボードを1台壊しきる", "キーボードを累計3台壊しきる", "すべての軸を手に入れる", "周回を1回する", "周回を5回する", "金のキーを押す", "金のキーを25回押す", "スクラップを累計1,000", "スクラップを累計100万", "スクラップを累計1億", "工具を全種類そろえる", "自動破壊を全種類そろえる", "猫を10匹乗せる", "毎秒ダメージ1,000", "強化を1つ習得する", "強化を10個習得する", "強化を30個習得する", "連打ボーナスを +50% まで伸ばす", "100,000回叩く", "1,000,000回叩く", "キー入力か手で100回叩く", "キー入力か手で1,000回叩く", "キー入力か手で10,000回叩く", "会心を1,000回出す", "会心を10,000回出す", "連打ボーナスを +100% まで伸ばす", "連打ボーナスを +200% まで伸ばす", "1打で100ダメージ", "1打で1万ダメージ", "1打で100万ダメージ", "1打で1億ダメージ", "1打の威力100", "1打の威力1万", "1打の威力100万", "スイッチを累計100個壊す", "スイッチを累計360個壊す", "スイッチを累計1,000個壊す", "キーボードを累計5台壊しきる", "キーボードを累計10台壊しきる", "キーボードを累計20台壊しきる", "軸を5種類そろえる", "1回の周回で100個目のキーに届く", "1回の周回で200個目のキーに届く", "スクラップを累計1万", "スクラップを累計100億", "スクラップを累計1兆", "スクラップを累計1京", "毎秒ダメージ100", "毎秒ダメージ1万", "毎秒ダメージ100万", "毎秒ダメージ1億", "工具と自動破壊を合わせて100個持つ", "工具と自動破壊を合わせて500個持つ", "工具と自動破壊を合わせて1,000個持つ", "急所ねらいを最大まで上げる", "強化を50個習得する", "強化をすべて習得する", "周回を10回する", "周回を25回する", "設計図を10枚持つ", "設計図を100枚持つ", "設計図を300枚持つ", "金のキーを5回押す", "金のキーを100回押す", "10分遊ぶ", "1時間遊ぶ", "5時間遊ぶ", "24時間遊ぶ", "指トレを50個持つ", "指トレを100個持つ", "キープラーを50個持つ", "キープラーを100個持つ", "マイナスドライバーを50個持つ", "マイナスドライバーを100個持つ", "ハンマーを50個持つ", "ハンマーを100個持つ", "電動ドリルを50個持つ", "電動ドリルを100個持つ", "チェーンソーを50個持つ", "チェーンソーを100個持つ", "削岩機を50個持つ", "削岩機を100個持つ", "ダイナマイトを50個持つ", "ダイナマイトを100個持つ", "解体用の鉄球を50個持つ", "解体用の鉄球を100個持つ", "隕石を50個持つ", "隕石を100個持つ", "キーボードに乗る猫を50個持つ", "キーボードに乗る猫を100個持つ", "こぼれたコーヒーを50個持つ", "こぼれたコーヒーを100個持つ", "台パンを50個持つ", "台パンを100個持つ", "はんだごてを50個持つ", "はんだごてを100個持つ", "油圧プレスを50個持つ", "油圧プレスを100個持つ", "衛星レーザーを50個持つ", "衛星レーザーを100個持つ", "酸の雨を50個持つ", "酸の雨を100個持つ", "火山を50個持つ", "火山を100個持つ", "ロボット軍団を50個持つ", "ロボット軍団を100個持つ", "小型ブラックホールを50個持つ", "小型ブラックホールを100個持つ", "経年劣化の加速を50個持つ", "経年劣化の加速を100個持つ", "宇宙の収縮を50個持つ", "宇宙の収縮を100個持つ" };
        achStat = new int[] { 0, 0, 0, 0, 7, 1, 1, 2, 2, 2, 3, 3, 3, 4, 4, 5, 5, 6, 6, 6, 8, 9, 10, 11, 12, 12, 12, 13, 0, 0, 14, 14, 14, 1, 1, 13, 13, 15, 15, 15, 15, 16, 16, 16, 2, 2, 2, 3, 3, 3, 3, 17, 17, 6, 6, 6, 6, 11, 11, 11, 11, 18, 18, 18, 10, 12, 12, 4, 4, 19, 19, 19, 5, 5, 20, 20, 20, 20, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10 };
        achArg = new int[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 11, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 10, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 2, 2, 3, 3, 4, 4, 5, 5, 6, 6, 7, 7, 8, 8, 9, 9, 11, 11, 12, 12, 13, 13, 14, 14, 15, 15, 16, 16, 17, 17, 18, 18, 19, 19, 20, 20, 21, 21, 22, 22 };
        achVal = new double[] { 1.0, 100.0, 1000.0, 10000.0, 1.0, 1.0, 100.0, 1.0, 10.0, 30.0, 1.0, 3.0, 9.0, 1.0, 5.0, 1.0, 25.0, 1000.0, 1000000.0, 100000000.0, 1.0, 1.0, 10.0, 1000.0, 1.0, 10.0, 30.0, 50.0, 100000.0, 1000000.0, 100.0, 1000.0, 10000.0, 1000.0, 10000.0, 100.0, 200.0, 100.0, 10000.0, 1000000.0, 100000000.0, 100.0, 10000.0, 1000000.0, 100.0, 360.0, 1000.0, 5.0, 10.0, 20.0, 4.0, 100.0, 200.0, 10000.0, 10000000000.0, 1000000000000.0, 10000000000000000.0, 100.0, 10000.0, 1000000.0, 100000000.0, 100.0, 500.0, 1000.0, 25.0, 50.0, 160.0, 10.0, 25.0, 10.0, 100.0, 300.0, 5.0, 100.0, 600.0, 3600.0, 18000.0, 86400.0, 50.0, 100.0, 50.0, 100.0, 50.0, 100.0, 50.0, 100.0, 50.0, 100.0, 50.0, 100.0, 50.0, 100.0, 50.0, 100.0, 50.0, 100.0, 50.0, 100.0, 50.0, 100.0, 50.0, 100.0, 50.0, 100.0, 50.0, 100.0, 50.0, 100.0, 50.0, 100.0, 50.0, 100.0, 50.0, 100.0, 50.0, 100.0, 50.0, 100.0, 50.0, 100.0, 50.0, 100.0 };
        achCat = new int[] { 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 5, 5, 5, 5, 2, 2, 2, 3, 3, 3, 3, 4, 4, 4, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 3, 3, 3, 3, 3, 3, 3, 3, 4, 4, 5, 5, 5, 5, 5, 5, 5, 6, 6, 6, 6, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3 };
        achCatName = new string[] { "叩く", "壊す", "スクラップ", "道具", "強化", "周回・金のキー", "時間" };
        achOrder = new int[] { 0, 1, 2, 3, 4, 5, 6, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 7, 8, 9, 10, 11, 12, 44, 45, 46, 47, 48, 49, 50, 51, 52, 17, 18, 19, 53, 54, 55, 56, 20, 21, 22, 23, 57, 58, 59, 60, 61, 62, 63, 64, 78, 79, 80, 81, 82, 83, 84, 85, 86, 87, 88, 89, 90, 91, 92, 93, 94, 95, 96, 97, 98, 99, 100, 101, 102, 103, 104, 105, 106, 107, 108, 109, 110, 111, 112, 113, 114, 115, 116, 117, 118, 119, 120, 121, 24, 25, 26, 65, 66, 13, 14, 15, 16, 67, 68, 69, 70, 71, 72, 73, 74, 75, 76, 77 };
        upByCost = new int[] { 0, 60, 6, 1, 61, 66, 132, 157, 7, 12, 138, 153, 2, 144, 72, 67, 62, 147, 18, 149, 13, 8, 78, 133, 73, 68, 158, 139, 24, 154, 19, 84, 14, 79, 145, 74, 30, 148, 150, 25, 3, 90, 134, 20, 85, 80, 63, 140, 159, 36, 155, 31, 96, 9, 26, 91, 146, 86, 69, 42, 37, 102, 135, 32, 97, 15, 151, 92, 141, 75, 48, 156, 43, 4, 108, 38, 103, 21, 98, 64, 81, 54, 49, 114, 136, 10, 44, 109, 27, 104, 87, 142, 152, 70, 55, 120, 50, 115, 16, 33, 110, 93, 76, 5, 126, 137, 56, 121, 22, 39, 116, 99, 65, 82, 143, 11, 127, 28, 45, 122, 88, 105, 71, 17, 34, 51, 128, 94, 111, 77, 23, 40, 57, 100, 117, 83, 29, 46, 89, 106, 123, 35, 52, 95, 112, 129, 41, 58, 101, 118, 47, 107, 124, 53, 113, 130, 59, 119, 125, 131 };
        UNITS = new string[] { "", "万", "億", "兆", "京", "垓", "秭", "穣" };
    }

    // ---------- 状態(保存するもの) ----------
    private double scrap, total, runTotal, hp, maxHit, playSec;
    private int broken, level, clicks, crits, keys, maxCombo;
    private int bp, prestiges, kbAll, brokenAll, gold;   // 周回しても残るもの
    private bool fast;
    private int[] owned; private bool[] ups; private bool[] ach;
    private int sw;                 // 選んでいる軸
    private int buyMode;            // まとめ買い: 0=×1, 1=×10, 2=×100, 3=全財産
    private bool soundOn;
    private int[] vol;              // 音量 0〜100: 全体・叩く音・自動破壊の音・壊れる音

    // ---------- 状態(保存しないもの) ----------
    private bool inited, ready, restored;
    private double clock;           // 遊びはじめてからの秒
    private float bootWait;
    private VRCPlayerApi localPlayer;
    private bool inVR;

    // 強化による倍率。買ったときと読み込んだときに作り直す
    private double[] toolMult;
    private double mGclick, mGauto, mCritMult, mSyn, mKb, mAchPct;
    private int mComboCap;
    private int upCount, achOwned, ownedAll;
    private bool powerDirty;
    private double cAuto, cClick, cCrit, cAll;

    private bool feverOn; private double feverAll, feverClick, feverUntil; private string feverName;
    private int comboN; private double lastHitT;
    private double[] hitRing; private int hitRingI, hitRingFill;
    private int bundleN; private double bundleCost;
    private bool psTold; private double psArmedUntil;

    // 見た目の更新待ち
    private bool dirty, dressDirty, hpDirty, saveDirty, ownedDirty, upsDirty, achDirty;
    private int fxBreak;            // 0=なし, 1=キーを壊した, 2=キーボードを壊しきった
    private double lastBreakFx;
    private float downTimer;
    private Vector3 capRest;
    private float hudWait, panelWait, saveWait;
    private float autoSndWait; private double lastSnd; private int lastClip;
    private float[] floatLife; private Vector3[] floatBase; private int floatI;
    private string toastA, toastB; private float toastWait;
    private float goldWait, goldLeft; private bool goldOn;
    private bool[] handIn; private bool[] handInGold;
    private int achCursor;
    private Color glow;

    private int shopTab, shopPageI, shopLen;
    private int[] shopList; private int[] rowRef; private int[] upState;
    private int infoTab, infoPageI;

    // ---------- はじまり ----------
    void Start()
    {
        Init();
        localPlayer = Networking.LocalPlayer;
        inVR = localPlayer != null && localPlayer.IsUserInVR();
        capRest = keycap.localPosition;
        floatLife = new float[floats.Length];
        floatBase = new Vector3[floats.Length];
        for (int i = 0; i < floats.Length; i++) { floatBase[i] = floats[i].transform.localPosition; floats[i].text = ""; }
        goldKey.SetActive(false);
        goldWait = 120f + UnityEngine.Random.value * 120f;
        if (!ready) txtToast.text = "保存データを読み込んでいます…";
        txtFever.text = "";
        txtCombo.text = "";
        DressSwitch();
        DrawHp();
        RenderHud();
        RenderShop();
        RenderInfo();
    }

    // 表と状態を用意する。Start より先にほかから呼ばれても 1 度しか動かない
    public void Init()
    {
        if (inited) return;
        inited = true;
        InitData();
        owned = new int[itName.Length];
        ups = new bool[upName.Length];
        ach = new bool[achName.Length];
        toolMult = new double[itName.Length];
        vol = new int[4];
        hitRing = new double[9];
        shopList = new int[upName.Length];
        upState = new int[upName.Length];
        rowRef = new int[ROWS];
        handIn = new bool[2];
        handInGold = new bool[2];
        Fresh();
        Recalc();
    }

    private void Fresh()
    {
        scrap = 0; total = 0; runTotal = 0; maxHit = 0; playSec = 0;
        broken = 0; level = 1; clicks = 0; crits = 0; keys = 0; maxCombo = 0;
        bp = 0; prestiges = 0; kbAll = 0; brokenAll = 0; gold = 0;
        fast = false;
        for (int i = 0; i < owned.Length; i++) owned[i] = 0;
        for (int i = 0; i < ups.Length; i++) ups[i] = false;
        for (int i = 0; i < ach.Length; i++) ach[i] = false;
        sw = 0; buyMode = 0; soundOn = true;
        for (int i = 0; i < 4; i++) vol[i] = 100;
        hp = MaxHp(1);
        achOwned = 0;
    }

    // ---------- 保存と読み込み ----------
    public override void OnPlayerRestored(VRCPlayerApi player)
    {
        if (player == null || !player.isLocal) return;
        Init();
        if (PlayerData.HasKey(player, "ksc_level")) Load(player);
        restored = true;
        if (!ready) Begin();
        else { dirty = true; dressDirty = true; hpDirty = true; }
        if (total > 0) Toast("おかえりなさい。つづきから再開します");
    }

    private void Begin()
    {
        ready = true;
        dirty = true; dressDirty = true; hpDirty = true;
        toastA = ""; toastB = ""; txtToast.text = "";
    }

    private void Load(VRCPlayerApi p)
    {
        Fresh();
        scrap = PlayerData.GetDouble(p, "ksc_scrap");
        total = PlayerData.GetDouble(p, "ksc_total");
        runTotal = PlayerData.GetDouble(p, "ksc_runTotal");
        hp = PlayerData.GetDouble(p, "ksc_hp");
        maxHit = PlayerData.GetDouble(p, "ksc_maxHit");
        playSec = PlayerData.GetDouble(p, "ksc_playSec");
        broken = PlayerData.GetInt(p, "ksc_broken");
        level = PlayerData.GetInt(p, "ksc_level");
        clicks = PlayerData.GetInt(p, "ksc_clicks");
        crits = PlayerData.GetInt(p, "ksc_crits");
        keys = PlayerData.GetInt(p, "ksc_keys");
        maxCombo = PlayerData.GetInt(p, "ksc_maxCombo");
        bp = PlayerData.GetInt(p, "ksc_bp");
        prestiges = PlayerData.GetInt(p, "ksc_prestiges");
        kbAll = PlayerData.GetInt(p, "ksc_kbAll");
        brokenAll = PlayerData.GetInt(p, "ksc_brokenAll");
        gold = PlayerData.GetInt(p, "ksc_gold");
        fast = PlayerData.GetBool(p, "ksc_fast");
        sw = PlayerData.GetInt(p, "ksc_sw");
        buyMode = PlayerData.GetInt(p, "ksc_buy");
        if (PlayerData.HasKey(p, "ksc_sound")) soundOn = PlayerData.GetBool(p, "ksc_sound");
        if (PlayerData.HasKey(p, "ksc_vol"))
        {
            byte[] v = PlayerData.GetBytes(p, "ksc_vol");
            for (int i = 0; i < 4 && i < v.Length; i++) vol[i] = Mathf.Clamp((int)v[i], 0, 100);
        }
        if (PlayerData.HasKey(p, "ksc_owned"))
        {
            // 個数は 2 バイトずつ(下位、上位の順)
            byte[] b = PlayerData.GetBytes(p, "ksc_owned");
            for (int i = 0; i < owned.Length && i * 2 + 1 < b.Length; i++) owned[i] = (int)b[i * 2] + (int)b[i * 2 + 1] * 256;
        }
        if (PlayerData.HasKey(p, "ksc_ups"))
        {
            byte[] b = PlayerData.GetBytes(p, "ksc_ups");
            for (int i = 0; i < ups.Length && i < b.Length; i++) ups[i] = b[i] != 0;
        }
        if (PlayerData.HasKey(p, "ksc_ach"))
        {
            byte[] b = PlayerData.GetBytes(p, "ksc_ach");
            for (int i = 0; i < ach.Length && i < b.Length; i++) ach[i] = b[i] != 0;
        }
        // 読めない値や範囲外の値は、標準の値で埋める
        if (level < 1) level = 1;
        if (sw < 0 || sw >= swName.Length || kbAll < swNeed[sw]) sw = 0;
        if (buyMode < 0 || buyMode > 3) buyMode = 0;
        if (!(hp > 0) || hp > MaxHp(level)) hp = MaxHp(level);
        achOwned = 0;
        for (int i = 0; i < ach.Length; i++) if (ach[i]) achOwned++;
        Recalc();
    }

    private void Save()
    {
        saveWait = 0; saveDirty = false;
        if (!restored) return; // 読み込みが済む前に書くと、前の保存を消してしまう
        PlayerData.SetDouble("ksc_scrap", scrap);
        PlayerData.SetDouble("ksc_total", total);
        PlayerData.SetDouble("ksc_runTotal", runTotal);
        PlayerData.SetDouble("ksc_hp", hp);
        PlayerData.SetDouble("ksc_maxHit", maxHit);
        PlayerData.SetDouble("ksc_playSec", playSec);
        PlayerData.SetInt("ksc_broken", broken);
        PlayerData.SetInt("ksc_level", level);
        PlayerData.SetInt("ksc_clicks", clicks);
        PlayerData.SetInt("ksc_crits", crits);
        PlayerData.SetInt("ksc_keys", keys);
        PlayerData.SetInt("ksc_maxCombo", maxCombo);
        PlayerData.SetInt("ksc_bp", bp);
        PlayerData.SetInt("ksc_prestiges", prestiges);
        PlayerData.SetInt("ksc_kbAll", kbAll);
        PlayerData.SetInt("ksc_brokenAll", brokenAll);
        PlayerData.SetInt("ksc_gold", gold);
        PlayerData.SetBool("ksc_fast", fast);
        PlayerData.SetInt("ksc_sw", sw);
        PlayerData.SetInt("ksc_buy", buyMode);
        PlayerData.SetBool("ksc_sound", soundOn);
        byte[] v = new byte[4];
        for (int i = 0; i < 4; i++) v[i] = (byte)vol[i];
        PlayerData.SetBytes("ksc_vol", v);
        if (ownedDirty)
        {
            ownedDirty = false;
            byte[] b = new byte[owned.Length * 2];
            for (int i = 0; i < owned.Length; i++)
            {
                int n = Mathf.Min(owned[i], 65535);
                b[i * 2] = (byte)(n % 256);
                b[i * 2 + 1] = (byte)(n / 256);
            }
            PlayerData.SetBytes("ksc_owned", b);
        }
        if (upsDirty)
        {
            upsDirty = false;
            byte[] b = new byte[ups.Length];
            for (int i = 0; i < ups.Length; i++) b[i] = ups[i] ? (byte)1 : (byte)0;
            PlayerData.SetBytes("ksc_ups", b);
        }
        if (achDirty)
        {
            achDirty = false;
            byte[] b = new byte[ach.Length];
            for (int i = 0; i < ach.Length; i++) b[i] = ach[i] ? (byte)1 : (byte)0;
            PlayerData.SetBytes("ksc_ach", b);
        }
    }

    // ---------- 計算 ----------
    private int KbCount() { return (level - 1) / KEYS; }   // この周回で壊しきったキーボードの台数
    private double MaxHp(int lv) { return Math.Floor(1000 * DIFF * Math.Pow(1.06, lv - 1)); }
    private double CostAt(int it, int lv) { return Math.Floor(itBase[it] * DIFF * Math.Pow(itGrowth[it], lv)); }

    // いまの「まとめ買い」設定で買う個数と合計額を bundleN・bundleCost に入れる
    private void Bundle(int it)
    {
        int have = owned[it];
        int room = itMax[it] > 0 ? itMax[it] - have : 1000000;
        bool all = buyMode == 3;
        int want = Mathf.Min(room, all ? 1000 : (buyMode == 0 ? 1 : buyMode == 1 ? 10 : 100));
        int n = 0; double sum = 0;
        while (n < want)
        {
            double c = CostAt(it, have + n);
            if (all && sum + c > scrap) break;
            sum += c;
            n++;
        }
        if (n == 0 && room > 0) sum = CostAt(it, have); // 全財産でも 1 個に届かないときは、1 個の値段を見せる
        bundleN = n; bundleCost = sum;
    }

    private void Recalc()
    {
        int[] n = new int[8];
        for (int i = 0; i < toolMult.Length; i++) toolMult[i] = 1;
        mComboCap = 0; upCount = 0;
        for (int u = 0; u < ups.Length; u++)
        {
            if (!ups[u]) continue;
            upCount++;
            int g = upGroup[u];
            n[g]++;
            if (g == 0) toolMult[upArg[u]] *= 2;
            if (g == 7 && upArg[u] > mComboCap) mComboCap = upArg[u];
        }
        mGclick = Math.Pow(2, n[1]);
        mGauto = Math.Pow(2, n[2]);
        mCritMult = 5 + 5 * n[3];
        mSyn = 0.03 * n[4];
        mKb = Math.Pow(1.5, n[5]);
        mAchPct = 0.01 * n[6];
        powerDirty = true;
    }

    private void RecalcPower()
    {
        powerDirty = false;
        double sClick = 0, sAuto = 0, sCrit = 0;
        ownedAll = 0;
        for (int i = 0; i < owned.Length; i++)
        {
            double v = itVal[i] * owned[i] * toolMult[i];
            if (itKind[i] == 0) sClick += v;
            else if (itKind[i] == 2) sAuto += v;
            else sCrit += v;
            if (itKind[i] != 1) ownedAll += owned[i];
        }
        // クリックにも自動破壊にも掛かる倍率
        cAll = mKb * (1 + achOwned * mAchPct) * (1 + bp * BP_BONUS) * (feverOn ? feverAll : 1);
        cAuto = sAuto * mGauto * cAll;
        cClick = (1 + sClick) * mGclick * cAll + cAuto * mSyn;
        cCrit = (5 + sCrit) / 100;
    }
    private double AutoPower() { if (powerDirty) RecalcPower(); return cAuto; }
    private double ClickPower() { if (powerDirty) RecalcPower(); return cClick; }
    private double CritChance() { if (powerDirty) RecalcPower(); return cCrit; }
    private double AllMult() { if (powerDirty) RecalcPower(); return cAll; }

    private int PsNeed() { return PS_MIN + PS_STEP * prestiges; }
    private int PsOver() { return Mathf.Max(0, KbCount() - PsNeed()); }
    private int BpGain() { return BP_PER_RUN + PsOver() * BP_PER_OVER; }
    private bool CanPrestige() { return KbCount() >= PsNeed(); }

    private string Comma(int n)
    {
        if (n < 1000) return n.ToString();
        return (n / 1000).ToString() + "," + (n % 1000).ToString("D3");
    }
    private string Fmt(double n)
    {
        n = Math.Floor(n);
        if (n < 10000) return Comma((int)n);
        int u = 0;
        while (n >= 10000 && u < UNITS.Length - 1) { n /= 10000; u++; }
        return (n >= 100 ? n.ToString("F0") : n >= 10 ? n.ToString("F1") : n.ToString("F2")) + UNITS[u];
    }
    private string Pct(double x) { return "+" + Math.Round(x * 100).ToString("F0") + "%"; }
    private string FmtTime(double sec)
    {
        int s = (int)Math.Floor(sec);
        int h = s / 3600, m = s % 3600 / 60, s2 = s % 60;
        if (h > 0) return h.ToString() + "時間" + m.ToString() + "分";
        if (m > 0) return m.ToString() + "分" + s2.ToString() + "秒";
        return s2.ToString() + "秒";
    }

    // ---------- 壊す ----------
    private void BreakSwitch()
    {
        broken++;
        brokenAll++;
        level++;
        hp = MaxHp(level);
        bool done = (level - 1) % KEYS == 0; // キーボードを 1 台壊しきった
        if (done)
        {
            kbAll++;
            string msg = "キーボード " + Fmt(KbCount()) + " 台目を壊しきった！";
            for (int i = 0; i < swNeed.Length; i++) if (swNeed[i] == kbAll) msg += " 新しい軸: " + swName[i];
            Toast(msg);
        }
        if (done) fxBreak = 2; else if (fxBreak < 1) fxBreak = 1;
        dressDirty = true;
        dirty = true;
    }

    // ダメージを与える。あふれた分は次のスイッチへ持ち越す
    private void Damage(double amount)
    {
        scrap += amount; total += amount; runTotal += amount; // 与えたダメージがそのままスクラップになる
        int guard = 0;
        while (amount > 0 && guard++ < 500)
        {
            double take = amount < hp ? amount : hp;
            hp -= take;
            amount -= take;
            if (hp <= 0) BreakSwitch();
        }
        hpDirty = true;
    }

    // 1 打ぶんの計算。会心かどうかは呼ぶ側が決める。与えたダメージを返す
    private double HitCore(bool crit)
    {
        double t = clock;
        int combo = 0;
        if (mComboCap > 0)
        {
            comboN = t - lastHitT < COMBO_S ? comboN + 1 : 0;
            combo = comboN < mComboCap ? comboN : mComboCap;
            if (combo > maxCombo) maxCombo = combo;
        }
        lastHitT = t;
        double dmg = ClickPower() * (crit ? mCritMult : 1) * (1 + combo / 100.0) * (feverOn ? feverClick : 1);
        if (dmg > maxHit) maxHit = dmg;
        clicks++;
        if (crit) crits++;
        // 1 秒間に 10 回叩いたか: 9 打前の時刻と比べる
        double old = hitRing[hitRingI];
        hitRing[hitRingI] = t;
        hitRingI = (hitRingI + 1) % hitRing.Length;
        if (hitRingFill >= hitRing.Length) { if (t - old <= 1.0) fast = true; }
        else hitRingFill++;
        Damage(dmg);
        return dmg;
    }

    // 時間を進める(自動破壊・連打切れ・フィーバー切れ)
    private void Tick(double dt)
    {
        clock += dt;
        double dps = AutoPower();
        if (dps > 0) Damage(dps * dt);
        if (comboN > 0 && clock - lastHitT > COMBO_S) comboN = 0;
        playSec += dt < 5 ? dt : 5;
        if (feverOn && clock > feverUntil) { feverOn = false; powerDirty = true; dirty = true; }
    }

    // ---------- 買う ----------
    private void Bought()
    {
        powerDirty = true;
        dirty = true;
        saveDirty = true;
    }
    private void BuyItem(int it)
    {
        Bundle(it);
        if (bundleN < 1 || scrap < bundleCost) return;
        scrap -= bundleCost;
        owned[it] += bundleN;
        ownedDirty = true;
        Bought();
    }
    private bool UpOk(int u)
    {
        int g = upGroup[u];
        double need = upReq[u];
        if (g == 0) return owned[upArg[u]] >= need;
        if (g == 1 || g == 2) return broken >= need;
        if (g == 3) return owned[IT_CRIT] >= need;
        if (g == 4) return AutoPower() >= need;
        if (g == 5) return KbCount() >= need;
        if (g == 6) return achOwned >= need;
        return clicks >= need;
    }
    private void BuyUp(int u)
    {
        if (ups[u] || scrap < upCost[u] || !UpOk(u)) return;
        scrap -= upCost[u];
        ups[u] = true;
        upsDirty = true;
        Recalc();
        Bought();
    }

    // ---------- 実績 ----------
    private double StatVal(int stat, int arg)
    {
        switch (stat)
        {
            case 0: return clicks;
            case 1: return crits;
            case 2: return brokenAll;
            case 3: return kbAll;
            case 4: return prestiges;
            case 5: return gold;
            case 6: return total;
            case 7: return fast ? 1 : 0;
            case 8: // 工具を全種類
                for (int i = 0; i < owned.Length; i++) if (itKind[i] == 0 && owned[i] < 1) return 0;
                return 1;
            case 9: // 自動破壊を全種類
                for (int i = 0; i < owned.Length; i++) if (itKind[i] == 2 && owned[i] < 1) return 0;
                return 1;
            case 10: return owned[arg];
            case 11: return AutoPower();
            case 12: return upCount;
            case 13: return maxCombo;
            case 14: return keys;
            case 15: return maxHit;
            case 16: return ClickPower();
            case 17: return level;
            case 18: if (powerDirty) RecalcPower(); return ownedAll;
            case 19: return bp;
            case 20: return playSec;
        }
        return 0;
    }
    // from から count 個ぶんの実績を調べる(1 度に全部は調べず、少しずつ回す)
    private void CheckAch(int from, int count)
    {
        // 実績の数は威力に効くので、調べ終わってからまとめて数に入れる(調べている途中で条件が動かないように)
        int got = 0;
        for (int k = 0; k < count; k++)
        {
            int a = (from + k) % ach.Length;
            if (ach[a] || StatVal(achStat[a], achArg[a]) < achVal[a]) continue;
            ach[a] = true;
            got++;
            Toast("実績解除: " + achName[a]);
        }
        if (got == 0) return;
        achOwned += got;
        achDirty = true;
        powerDirty = true;
        dirty = true;
        saveDirty = true;
    }

    // ---------- 金のキーとフィーバー ----------
    // r は 0〜1 の乱数。半分でフィーバー、4 分の 1 で連打フィーバー、残りは棚ぼた
    private void TakeGoldCore(double r)
    {
        gold++;
        string msg;
        if (r < 0.5)
        {
            feverOn = true; feverAll = 7; feverClick = 1; feverUntil = clock + 30;
            feverName = "フィーバー: すべてのダメージ ×7";
            msg = "フィーバー！ 30秒間すべてのダメージ ×7";
        }
        else if (r < 0.75)
        {
            feverOn = true; feverAll = 1; feverClick = 77; feverUntil = clock + 10;
            feverName = "連打フィーバー: 1打の威力 ×77";
            msg = "連打フィーバー！ 10秒間 1打の威力 ×77";
        }
        else
        {
            double a = ClickPower() * 100, b = AutoPower() * 600;
            double gain = a > b ? a : b;
            scrap += gain; total += gain; runTotal += gain;
            msg = "棚ぼた！ スクラップ +" + Fmt(gain);
        }
        powerDirty = true;
        dirty = true;
        saveDirty = true;
        Toast(msg);
    }

    // ---------- 周回 ----------
    private void DoPrestige()
    {
        if (!CanPrestige()) return;
        bp += BpGain();
        prestiges++;
        scrap = 0; level = 1; broken = 0; runTotal = 0;
        for (int i = 0; i < owned.Length; i++) owned[i] = 0;
        for (int i = 0; i < ups.Length; i++) ups[i] = false;
        hp = MaxHp(1);
        psTold = false;
        feverOn = false;
        hitRingFill = 0; hitRingI = 0;
        comboN = 0;
        ownedDirty = true; upsDirty = true;
        Recalc();
        dirty = true; dressDirty = true; hpDirty = true; saveDirty = true;
        Toast(prestiges.ToString() + " 回目の周回。設計図 " + Fmt(bp) + " 枚(すべてのダメージ " + Pct(bp * BP_BONUS) + ")");
    }

    // ---------- お知らせ ----------
    // 重なったら直近の 2 件を並べる
    private void Toast(string msg)
    {
        if (toastB == null || toastB == "") toastB = msg;
        else { toastA = toastB; toastB = msg; }
        toastWait = 4f;
        if (txtToast != null) txtToast.text = (toastA == null || toastA == "") ? toastB : toastA + " / " + toastB;
    }

    // ---------- 入力 ----------
    // 看板のボタン(KscButton)から呼ばれる
    public void Press(int kind, int index)
    {
        if (!ready) return;
        if (kind == 0) Hit(false);
        else if (kind == 1) TakeGold();
        else if (kind == 2) { shopTab = index; shopPageI = 0; RenderShop(); }
        else if (kind == 3) BuyRow(index);
        else if (kind == 4) { shopPageI += index; RenderShop(); }
        else if (kind == 5) { buyMode = (buyMode + 1) % 4; saveDirty = true; RenderShop(); }
        else if (kind == 6) { infoTab = index; infoPageI = 0; psArmedUntil = 0; RenderInfo(); }
        else if (kind == 7) { infoPageI += index; RenderInfo(); }
        else if (kind == 8) InfoAct(index);
    }

    private void Hit(bool viaKeys)
    {
        if (viaKeys) keys++;
        bool crit = UnityEngine.Random.value < CritChance();
        double dmg = HitCore(crit);
        saveDirty = true;
        // 数字を飛ばす
        Text f = floats[floatI];
        f.text = (crit ? "会心 " : "") + Fmt(dmg);
        f.color = crit ? new Color(1f, 0.82f, 0.29f, 1f) : Color.white;
        floatBase[floatI] = new Vector3((UnityEngine.Random.value - 0.5f) * 250f, (UnityEngine.Random.value - 0.5f) * 100f, 0f); // 看板のピクセル単位
        floatLife[floatI] = 0.8f;
        floatI = (floatI + 1) % floats.Length;
        fx.Emit(crit ? 10 : 4);
        SndHit(srcHit, VolOf(1));
        keycap.localPosition = capRest + new Vector3(0f, 0f, pressDepth);
        downTimer = 0.07f;
        if (mComboCap > 0) DrawCombo();
    }

    private void TakeGold()
    {
        if (!goldOn) return;
        goldOn = false;
        goldKey.SetActive(false);
        TakeGoldCore(UnityEngine.Random.value);
        fx.Emit(30);
        DrawFever();
    }

    private void BuyRow(int row)
    {
        int r = rowRef[row];
        if (r < 0) return;
        if (r >= 1000) BuyUp(r - 1000); else BuyItem(r);
        CheckAch(0, ach.Length);
        RenderShop();
        RenderHud();
    }

    private void InfoAct(int i)
    {
        if (infoTab == 2)
        {
            // 軸を替える
            if (i >= swName.Length || kbAll < swNeed[i]) return;
            sw = i;
            dressDirty = true;
            saveDirty = true;
            lastSnd = -1;
            SndHit(srcHit, VolOf(1)); // 選んだ軸の音を試し聞き
        }
        else if (infoTab == 3)
        {
            // 周回: 取り消せないので、2 回押してから行う
            if (i != 0 || !CanPrestige()) return;
            if (clock < psArmedUntil) { psArmedUntil = 0; DoPrestige(); CheckAch(0, ach.Length); }
            else psArmedUntil = clock + 5;
        }
        else if (infoTab == 4)
        {
            if (i == 8) soundOn = !soundOn;
            else if (i < 8)
            {
                int k = i / 2;
                vol[k] = Mathf.Clamp(vol[k] + (i % 2 == 0 ? -10 : 10), 0, 100);
                if (k == 3) SndBreak(false); else SndHit(k == 2 ? srcAuto : srcHit, k == 2 ? AUTO_VOL * VolOf(2) : VolOf(1));
            }
            saveDirty = true;
        }
        RenderInfo();
    }

    // ---------- 音 ----------
    // 全体の音量を掛けた、種類ごとの音量(0〜1)。k: 1=叩く音, 2=自動破壊の音, 3=壊れる音
    private float VolOf(int k) { return (vol[0] / 100f) * (vol[k] / 100f); }

    private void SndHit(AudioSource src, float v)
    {
        if (!soundOn || v <= 0f) return;
        if (clock - lastSnd < 0.025) return;
        lastSnd = clock;
        int i = (int)(UnityEngine.Random.value * CLIPS_PER_AXIS) % CLIPS_PER_AXIS;
        if (i == lastClip) i = (i + 1) % CLIPS_PER_AXIS; // 同じ音を続けない
        lastClip = i;
        AudioClip c = hitClips[sw * CLIPS_PER_AXIS + i];
        if (c == null) return;
        src.pitch = 0.98f + UnityEngine.Random.value * 0.04f; // 1 打ごとにわずかに揺らす
        src.PlayOneShot(c, hitGain * v * (0.85f + UnityEngine.Random.value * 0.3f));
    }
    private void SndBreak(bool big)
    {
        float v = VolOf(3);
        if (!soundOn || v <= 0f) return;
        srcBreak.PlayOneShot(big ? breakBig : breakSmall, v);
    }

    // ---------- 毎フレーム ----------
    void Update()
    {
        float fdt = Time.deltaTime;
        if (!ready)
        {
            // 保存データの読み込みを待つ。届かないときは、保存はせずに遊べるようにする
            bootWait += fdt;
            if (bootWait > 15f) { Begin(); Toast("保存データを読めませんでした。このまま遊べますが、進み具合は残りません"); }
            return;
        }
        Tick(fdt > 3600f ? 3600f : fdt);
        float dps = (float)AutoPower();

        // 自動破壊の音: 毎秒ダメージが大きいほど速く(毎秒 2〜8 回)
        if (dps > 0f)
        {
            autoSndWait -= fdt < 1f ? fdt : 1f;
            if (autoSndWait <= 0f)
            {
                SndHit(srcAuto, AUTO_VOL * VolOf(2));
                float rate = Mathf.Clamp(1f + Mathf.Log10(dps), 2f, 8f);
                autoSndWait = (0.7f + UnityEngine.Random.value * 0.6f) / rate;
            }
        }

        ReadInput();

        // キーキャップを戻す
        if (downTimer > 0f)
        {
            downTimer -= fdt;
            if (downTimer <= 0f) keycap.localPosition = capRest;
        }
        // 飛び出した数字を上へ流しながら消す
        for (int i = 0; i < floats.Length; i++)
        {
            if (floatLife[i] <= 0f) continue;
            floatLife[i] -= fdt;
            float k = Mathf.Clamp01(floatLife[i] / 0.8f);
            Text f = floats[i];
            if (floatLife[i] <= 0f) { f.text = ""; continue; }
            f.transform.localPosition = floatBase[i] + new Vector3(0f, (1f - k) * 220f, 0f);
            Color c = f.color; c.a = k; f.color = c;
        }
        // 壊れたときの演出。続けて壊れても、鳴らしすぎない
        if (fxBreak > 0)
        {
            if (fxBreak == 2 || clock - lastBreakFx > 0.09)
            {
                lastBreakFx = clock;
                fx.Emit(fxBreak == 2 ? 60 : 22);
                SndBreak(fxBreak == 2);
            }
            fxBreak = 0;
        }

        // 金のキー
        if (goldOn)
        {
            goldLeft -= fdt;
            if (goldLeft <= 0f) { goldOn = false; goldKey.SetActive(false); }
            else
            {
                goldKey.transform.Rotate(0f, 120f * fdt, 0f);
                // 消える前の 1.5 秒は点滅する
                if (goldLeft < 1.5f) goldKey.transform.localScale = Vector3.one * (Mathf.Repeat(goldLeft, 0.3f) < 0.15f ? 0.6f : 1f);
            }
        }
        else
        {
            goldWait -= fdt < 5f ? fdt : 5f;
            if (goldWait <= 0f) { SpawnGold(); goldWait = 180f + UnityEngine.Random.value * 240f; }
        }

        if (toastWait > 0f)
        {
            toastWait -= fdt;
            if (toastWait <= 0f) { toastA = ""; toastB = ""; txtToast.text = ""; }
        }

        achCursor = (achCursor + 8) % ach.Length;
        CheckAch(achCursor, 8);

        hudWait -= fdt;
        if (hudWait <= 0f)
        {
            hudWait = 0.1f;
            if (dressDirty) DressSwitch();
            if (hpDirty) DrawHp();
            RenderHud();
            if (comboN == 0) DrawCombo();
        }
        // 看板は、変化があれば 0.25 秒ごと、なければ 0.5 秒ごとに描き直す(毎フレーム描くと重い)
        panelWait += fdt;
        if (panelWait >= 0.5f || (dirty && panelWait >= 0.25f))
        {
            panelWait = 0f;
            dirty = false;
            RenderShop();
            RenderInfo();
            if (CanPrestige() && !psTold)
            {
                psTold = true;
                Toast("キーボードを " + PsNeed().ToString() + " 台壊しきりました。周回できます");
            }
        }

        saveWait += fdt;
        if (saveWait > 10f || (saveDirty && saveWait > 2f)) Save();
    }

    private void ReadInput()
    {
        if (localPlayer == null) return;
        Vector3 c = hitCenter.position;
        if (inVR)
        {
            // 手を スイッチに差し入れた瞬間に 1 打。抜くまで次は数えない
            for (int h = 0; h < 2; h++)
            {
                Vector3 p = localPlayer.GetBonePosition(h == 0 ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
                float d = Vector3.Distance(p, c);
                if (!handIn[h] && d < punchRadius)
                {
                    handIn[h] = true;
                    Hit(true);
                    localPlayer.PlayHapticEventInHand(h == 0 ? VRC_Pickup.PickupHand.Left : VRC_Pickup.PickupHand.Right, 0.05f, 0.6f, 150f);
                }
                else if (handIn[h] && d > punchRadius + 0.08f) handIn[h] = false;

                if (!goldOn) { handInGold[h] = false; continue; }
                float dg = Vector3.Distance(p, goldKey.transform.position);
                if (!handInGold[h] && dg < 0.25f) { handInGold[h] = true; TakeGold(); }
            }
        }
        else
        {
            // いま出ているキーと同じキーを押すと叩ける(スイッチの近くにいるときだけ)
            if (KeyDown((level - 1) % KEYS) && Vector3.Distance(localPlayer.GetPosition(), c) < 6f) Hit(true);
        }
    }

    // いま出ているキー(A→Z、1→0 の idx 番目)が押されたか
    private bool KeyDown(int idx)
    {
        switch (idx)
        {
            case 0: return Input.GetKeyDown(KeyCode.A);
            case 1: return Input.GetKeyDown(KeyCode.B);
            case 2: return Input.GetKeyDown(KeyCode.C);
            case 3: return Input.GetKeyDown(KeyCode.D);
            case 4: return Input.GetKeyDown(KeyCode.E);
            case 5: return Input.GetKeyDown(KeyCode.F);
            case 6: return Input.GetKeyDown(KeyCode.G);
            case 7: return Input.GetKeyDown(KeyCode.H);
            case 8: return Input.GetKeyDown(KeyCode.I);
            case 9: return Input.GetKeyDown(KeyCode.J);
            case 10: return Input.GetKeyDown(KeyCode.K);
            case 11: return Input.GetKeyDown(KeyCode.L);
            case 12: return Input.GetKeyDown(KeyCode.M);
            case 13: return Input.GetKeyDown(KeyCode.N);
            case 14: return Input.GetKeyDown(KeyCode.O);
            case 15: return Input.GetKeyDown(KeyCode.P);
            case 16: return Input.GetKeyDown(KeyCode.Q);
            case 17: return Input.GetKeyDown(KeyCode.R);
            case 18: return Input.GetKeyDown(KeyCode.S);
            case 19: return Input.GetKeyDown(KeyCode.T);
            case 20: return Input.GetKeyDown(KeyCode.U);
            case 21: return Input.GetKeyDown(KeyCode.V);
            case 22: return Input.GetKeyDown(KeyCode.W);
            case 23: return Input.GetKeyDown(KeyCode.X);
            case 24: return Input.GetKeyDown(KeyCode.Y);
            case 25: return Input.GetKeyDown(KeyCode.Z);
            case 26: return Input.GetKeyDown(KeyCode.Alpha1);
            case 27: return Input.GetKeyDown(KeyCode.Alpha2);
            case 28: return Input.GetKeyDown(KeyCode.Alpha3);
            case 29: return Input.GetKeyDown(KeyCode.Alpha4);
            case 30: return Input.GetKeyDown(KeyCode.Alpha5);
            case 31: return Input.GetKeyDown(KeyCode.Alpha6);
            case 32: return Input.GetKeyDown(KeyCode.Alpha7);
            case 33: return Input.GetKeyDown(KeyCode.Alpha8);
            case 34: return Input.GetKeyDown(KeyCode.Alpha9);
            case 35: return Input.GetKeyDown(KeyCode.Alpha0);
        }
        return false;
    }

    private void SpawnGold()
    {
        // スイッチの手前の、手が届く高さのどこかに出す
        float a = (UnityEngine.Random.value - 0.5f) * 2.4f;
        float r = 1.1f + UnityEngine.Random.value * 0.9f;
        goldKey.transform.position = hitCenter.position + new Vector3(Mathf.Sin(a) * r, -0.3f + UnityEngine.Random.value * 0.7f, -Mathf.Cos(a) * r * 0.6f - 0.3f);
        goldKey.transform.localScale = Vector3.one;
        goldKey.SetActive(true);
        goldOn = true;
        goldLeft = 13f;
    }

    // ---------- 描く ----------
    private void DressSwitch()
    {
        dressDirty = false;
        int kb = KbCount();
        int idx = (level - 1) % KEYS;
        if (sw < 0 || sw >= swName.Length || kbAll < swNeed[sw]) sw = 0;
        stemMat.color = swColor[sw];
        // バックライトの色はキーボードごとに変わる
        glow = Color.HSVToRGB(((200 + kb * 67) % 360) / 360f, 0.62f, 0.98f);
        glowMat.color = glow;
        glowMat.SetColor("_EmissionColor", glow * 0.9f);
        string ch = ORDER.Substring(idx, 1);
        legend.text = ch;
        txtTarget.text = "「" + ch + "」キー(" + swName[sw] + ")";
        txtSub.text = swKind[sw] + " ・ 通算 " + Fmt(level) + " 個目";
        for (int i = 0; i < kbKeys.Length; i++)
        {
            kbKeys[i].sharedMaterial = i < idx ? matKeyDone : (i == idx ? glowMat : matKey);
        }
        txtKbd.text = "キーボード " + Fmt(kb + 1) + " 台目 ・ のこり " + (KEYS - idx).ToString() + " / " + KEYS.ToString() + " キー";
    }

    private void DrawHp()
    {
        hpDirty = false;
        double m = MaxHp(level);
        float r = Mathf.Clamp01((float)(hp / m));
        hpFill.localScale = new Vector3(r, 1f, 1f);
        txtHp.text = Fmt(Math.Ceiling(hp)) + " / " + Fmt(m);
        cracks[0].SetActive(r < 0.75f);
        cracks[1].SetActive(r < 0.5f);
        cracks[2].SetActive(r < 0.25f);
    }

    private void DrawCombo()
    {
        int c = comboN < mComboCap ? comboN : mComboCap;
        txtCombo.text = c > 0 ? "連打 +" + c.ToString() + "%" : "";
    }

    private void DrawFever()
    {
        if (!feverOn) { txtFever.text = ""; return; }
        txtFever.text = feverName + " ・ のこり " + ((int)Math.Ceiling(feverUntil - clock)).ToString() + "秒";
    }

    private void RenderHud()
    {
        txtScrap.text = "スクラップ " + Fmt(scrap);
        txtPower.text = "1打 " + Fmt(ClickPower()) + " ・ 毎秒 " + Fmt(AutoPower())
            + " ・ 会心率 " + Math.Round(CritChance() * 100).ToString("F0") + "% ・ 破壊数 " + Fmt(broken);
        DrawFever();
    }

    private string DescOf(int it)
    {
        if (itKind[it] == 0) return "1打 +" + Fmt(itVal[it] * toolMult[it] * mGclick * AllMult());
        if (itKind[it] == 2) return "毎秒 +" + Fmt(itVal[it] * toolMult[it] * mGauto * AllMult());
        return "会心率 +" + itVal[it].ToString("F0") + "%(会心は" + mCritMult.ToString("F0") + "倍)";
    }

    // 工具店。タブ: 0=工具, 1=技と自動破壊, 2=強化
    private void RenderShop()
    {
        shopLen = 0;
        if (shopTab == 2)
        {
            // 買えるもの、条件を満たしていないもの、習得済みの順。それぞれ安い順
            for (int u = 0; u < ups.Length; u++) upState[u] = ups[u] ? 2 : (UpOk(u) ? 0 : 1);
            for (int pass = 0; pass < 3; pass++)
            {
                for (int k = 0; k < upByCost.Length; k++)
                {
                    int u = upByCost[k];
                    if (upState[u] == pass) shopList[shopLen++] = 1000 + u;
                }
            }
        }
        else
        {
            for (int i = 0; i < itName.Length; i++)
            {
                if ((shopTab == 0) == (itKind[i] == 0)) shopList[shopLen++] = i;
            }
        }
        int pages = Mathf.Max(1, (shopLen + ROWS - 1) / ROWS);
        if (shopPageI < 0) shopPageI = pages - 1;
        if (shopPageI >= pages) shopPageI = 0;
        shopTitle.text = shopTab == 0 ? "工具(1打の威力)" : shopTab == 1 ? "技・自動破壊(毎秒)"
            : "強化 " + upCount.ToString() + " / " + ups.Length.ToString() + " 習得";
        shopPage.text = (shopPageI + 1).ToString() + " / " + pages.ToString();
        txtBuyMode.text = "まとめ買い: " + (buyMode == 0 ? "×1" : buyMode == 1 ? "×10" : buyMode == 2 ? "×100" : "全財産");
        Color on = Color.white, off = new Color(0.55f, 0.57f, 0.62f, 1f);
        for (int row = 0; row < ROWS; row++)
        {
            int k = shopPageI * ROWS + row;
            if (k >= shopLen)
            {
                rowRef[row] = -1;
                rowName[row].text = ""; rowInfo[row].text = ""; rowCost[row].text = "";
                rowBtn[row].SetActive(false);
                continue;
            }
            int r = shopList[k];
            rowRef[row] = r;
            bool can;
            if (r >= 1000)
            {
                int u = r - 1000;
                bool ok = !ups[u] && UpOk(u);
                rowName[row].text = upName[u];
                rowInfo[row].text = ups[u] ? upDesc[u] + " ・ 習得済み" : ok ? upDesc[u] + " ・ 1回かぎり" : upDesc[u] + " ・ 条件: " + upNeed[u];
                rowCost[row].text = ups[u] ? "習得済み" : Fmt(upCost[u]);
                can = ok && scrap >= upCost[u];
                rowBtn[row].SetActive(!ups[u]);
            }
            else
            {
                bool maxed = itMax[r] > 0 && owned[r] >= itMax[r];
                Bundle(r);
                rowName[row].text = itName[r] + "  所持 " + owned[r].ToString();
                rowInfo[row].text = DescOf(r);
                rowCost[row].text = maxed ? "最大" : (buyMode == 0 ? "" : "×" + Mathf.Max(bundleN, 1).ToString() + " ") + Fmt(bundleCost);
                can = !maxed && bundleN >= 1 && scrap >= bundleCost;
                rowBtn[row].SetActive(!maxed);
            }
            rowName[row].color = can ? on : off;
            rowCost[row].color = can ? new Color(1f, 0.82f, 0.29f, 1f) : off;
        }
    }

    // 記録の看板。タブ: 0=実績, 1=統計, 2=軸, 3=周回, 4=音
    private void RenderInfo()
    {
        int acts = 0;
        int pages = 1;
        string body = "";
        if (infoTab == 0)
        {
            pages = (ach.Length + ACH_PER_PAGE - 1) / ACH_PER_PAGE;
            if (infoPageI < 0) infoPageI = pages - 1;
            if (infoPageI >= pages) infoPageI = 0;
            infoTitle.text = "実績 " + achOwned.ToString() + " / " + ach.Length.ToString();
            int cat = -1;
            for (int k = infoPageI * ACH_PER_PAGE; k < (infoPageI + 1) * ACH_PER_PAGE && k < ach.Length; k++)
            {
                int a = achOrder[k];
                if (achCat[a] != cat) { cat = achCat[a]; body += "<color=#8fd0ff>【" + achCatName[cat] + "】</color>\n"; }
                body += ach[a] ? "<color=#ffd24a>■ " + achName[a] + "</color> … " + achDesc[a] + "\n"
                    : "<color=#8c919e>□ " + achName[a] + " … " + achDesc[a] + "</color>\n";
            }
        }
        else if (infoTab == 1)
        {
            infoTitle.text = "統計";
            body = "<color=#8fd0ff>【全体】</color>\n"
                + "遊んだ時間: " + FmtTime(playSec) + "\n"
                + "累計スクラップ: " + Fmt(total) + "\n"
                + "壊したキー(累計): " + Fmt(brokenAll) + "\n"
                + "壊しきったキーボード(累計): " + Fmt(kbAll) + " 台\n"
                + "<color=#8fd0ff>【叩く】</color>\n"
                + "叩いた回数: " + Fmt(clicks) + "(うちキー入力か手で " + Fmt(keys) + ")\n"
                + "会心の回数: " + Fmt(crits) + "\n"
                + "1打の最高ダメージ: " + Fmt(maxHit) + "\n"
                + "連打ボーナスの最高: +" + maxCombo.ToString() + "%\n"
                + "金のキーを押した回数: " + Fmt(gold) + "\n"
                + "<color=#8fd0ff>【この周回】</color>\n"
                + "スクラップ: " + Fmt(runTotal) + " ・ 壊したキー: " + Fmt(broken) + "\n"
                + "習得した強化: " + upCount.ToString() + " / " + ups.Length.ToString() + "\n"
                + "すべてのダメージの倍率: ×" + AllMult().ToString("F2") + "\n"
                + "<color=#8fd0ff>【周回】</color>\n"
                + "周回した回数: " + Fmt(prestiges) + " ・ 設計図: " + Fmt(bp) + " 枚(" + Pct(bp * BP_BONUS) + ")";
        }
        else if (infoTab == 2)
        {
            int have = 0;
            acts = swName.Length;
            for (int i = 0; i < swName.Length; i++)
            {
                bool got = kbAll >= swNeed[i];
                if (got) have++;
                actLabel[i].text = got ? swName[i] + "(" + swKind[i] + ")" + (i == sw ? " ◀ 使用中" : "")
                    : "？？？ 累計" + swNeed[i].ToString() + "台破壊で解放";
            }
            infoTitle.text = "軸コレクション " + have.ToString() + " / " + swName.Length.ToString();
            body = "キーボードを 1 台壊しきるたびに、新しい軸が 1 つ手に入ります(周回しても失いません)。\n違いは打鍵音と軸の色だけです。下のボタンで替えられます。";
        }
        else if (infoTab == 3)
        {
            int g = BpGain();
            infoTitle.text = "周回";
            body = "スクラップや工具を手放して最初からやり直す代わりに、設計図を受け取ります。\n設計図 1 枚につき、すべてのダメージが永久に +5% されます。実績・軸・統計は残ります。\n\n"
                + "持っている設計図: " + Fmt(bp) + " 枚(" + Pct(bp * BP_BONUS) + ")\n"
                + "いま周回するともらえる設計図: " + Fmt(g) + " 枚(" + Pct(g * BP_BONUS) + ")\n"
                + "うち、条件を超えたぶん: " + PsOver().ToString() + " 台 → +" + Fmt(PsOver() * BP_PER_OVER) + " 枚\n"
                + "周回後のダメージ: " + Pct((bp + g) * BP_BONUS) + "\n"
                + "条件: この周回でキーボードを " + PsNeed().ToString() + " 台壊しきる("
                + (CanPrestige() ? "達成" : "いま " + KbCount().ToString() + " 台") + ")\n"
                + "次の周回の条件: " + (PsNeed() + PS_STEP).ToString() + " 台";
            acts = 1;
            actLabel[0].text = !CanPrestige() ? "あと " + (PsNeed() - KbCount()).ToString() + " 台で周回できます"
                : clock < psArmedUntil ? "もう一度押すと周回します" : "周回する(設計図 +" + Fmt(g) + ")";
        }
        else
        {
            infoTitle.text = "音";
            body = "音: " + (soundOn ? "入" : "切") + "\n"
                + "全体: " + vol[0].ToString() + "\n叩く音: " + vol[1].ToString()
                + "\n自動破壊の音: " + vol[2].ToString() + "\n壊れる音: " + vol[3].ToString();
            acts = 9;
            actLabel[0].text = "全体 −"; actLabel[1].text = "全体 ＋";
            actLabel[2].text = "叩く音 −"; actLabel[3].text = "叩く音 ＋";
            actLabel[4].text = "自動破壊の音 −"; actLabel[5].text = "自動破壊の音 ＋";
            actLabel[6].text = "壊れる音 −"; actLabel[7].text = "壊れる音 ＋";
            actLabel[8].text = soundOn ? "音を切る" : "音を入れる";
        }
        infoBody.text = body;
        infoPage.text = pages > 1 ? (infoPageI + 1).ToString() + " / " + pages.ToString() : "";
        for (int i = 0; i < actBtn.Length; i++) actBtn[i].SetActive(i < acts);
    }
}
