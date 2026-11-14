#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using VRC.Core;
using VRC.SDK3.Components;
using VRC.Udon;

// メニュー「Keyswitch Crusher > ワールドを組み立てる」で、いま開いているシーンに
// 部屋・スイッチ・看板・音を置き、ゲーム本体(KeyswitchCrusher)につなぐ。
// 置き場所や大きさを変えたいときは、このファイルの数字を直してもう一度実行する。
public static class KeyswitchCrusherBuilder
{
    private const string RootName = "KeyswitchCrusher";
    private const float Px = 0.002f; // 看板の 1 ピクセルの大きさ(メートル)
    private static readonly string[] AxisIds = { "blue", "red", "brown", "black", "silver", "pink", "green", "purple", "gold", "white" };
    private static readonly string[] KbRows = { "1234567890", "QWERTYUIOP", "ASDFGHJKL", "ZXCVBNM" };
    private static readonly float[] KbRowShift = { 0f, 0.3f, 0.55f, 1.05f };
    private const string Order = "ABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890";

    private static string _dir;      // Assets/KeyswitchCrusher
    private static Font _font;
    private static KeyswitchCrusher _game;
    private static readonly Dictionary<string, Material> Mats = new Dictionary<string, Material>();

    [MenuItem("Keyswitch Crusher/ワールドを組み立てる")]
    public static void Build()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Keyswitch Crusher", "再生を止めてから実行してください。", "OK");
            return;
        }
        string gameScript = FindScript(typeof(KeyswitchCrusher));
        string buttonScript = FindScript(typeof(KscButton));
        if (gameScript == null || buttonScript == null)
        {
            EditorUtility.DisplayDialog("Keyswitch Crusher", "KeyswitchCrusher.cs か KscButton.cs が見つかりません。", "OK");
            return;
        }
        _dir = Path.GetDirectoryName(Path.GetDirectoryName(gameScript)).Replace('\\', '/');

        // Udon のプログラム(スクリプトと対になるアセット)がなければ作る。作った直後はコンパイルを待つ
        bool made = EnsureProgram(typeof(KeyswitchCrusher), gameScript);
        made |= EnsureProgram(typeof(KscButton), buttonScript);
        if (made)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            UdonSharpProgramAsset.CompileAllCsPrograms(true);
            EditorUtility.DisplayDialog("Keyswitch Crusher",
                "Udon のプログラムを作りました。\nコンソールにエラーが出ていないことを確かめてから、もう一度このメニューを実行してください。", "OK");
            return;
        }

        GameObject old = GameObject.Find(RootName);
        if (old != null)
        {
            if (!EditorUtility.DisplayDialog("Keyswitch Crusher", "シーンにある「" + RootName + "」を消して、作り直します。よろしいですか？", "作り直す", "やめる")) return;
            Undo.DestroyObjectImmediate(old);
        }

        _font = FindFont();
        Mats.Clear();
        GameObject root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, "Build Keyswitch Crusher");
        _game = UdonSharpUndo.AddComponent<KeyswitchCrusher>(root);

        BuildRoom(root.transform);
        BuildSwitch(root.transform);
        BuildHud(root.transform);
        BuildKeyboard(root.transform);
        BuildShop(root.transform);
        BuildInfo(root.transform);
        BuildGold(root.transform);
        BuildAudio(root);
        BuildWorldSettings(root.transform);

        UdonSharpEditorUtility.CopyProxyToUdon(_game);
        EditorUtility.SetDirty(_game);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(root.scene);
        Selection.activeGameObject = root;
        Debug.Log("[Keyswitch Crusher] ワールドを組み立てました。");
    }

    // ---------- 下ごしらえ ----------
    private static string FindScript(Type type)
    {
        foreach (string guid in AssetDatabase.FindAssets(type.Name + " t:MonoScript"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
            if (script != null && script.GetClass() == type) return path;
        }
        return null;
    }

    private static bool EnsureProgram(Type type, string scriptPath)
    {
        if (UdonSharpEditorUtility.GetUdonSharpProgramAsset(type) != null) return false;
        UdonSharpProgramAsset asset = ScriptableObject.CreateInstance<UdonSharpProgramAsset>();
        asset.sourceCsScript = AssetDatabase.LoadAssetAtPath<MonoScript>(scriptPath);
        AssetDatabase.CreateAsset(asset, Path.ChangeExtension(scriptPath, ".asset"));
        return true;
    }

    // Font フォルダーにフォントがあればそれを使う。なければ Unity に入っているフォント
    private static Font FindFont()
    {
        string fontDir = _dir + "/Font";
        if (AssetDatabase.IsValidFolder(fontDir))
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Font", new[] { fontDir }))
            {
                Font f = AssetDatabase.LoadAssetAtPath<Font>(AssetDatabase.GUIDToAssetPath(guid));
                if (f != null) return f;
            }
        }
        Font builtin = null;
        try { builtin = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch (Exception) { }
        if (builtin == null) builtin = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return builtin;
    }

    private static Material Mat(string name, Color color, float emission = 0f, float smooth = 0.3f)
    {
        if (Mats.TryGetValue(name, out Material found)) return found;
        string folder = _dir + "/Generated";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(_dir, "Generated");
        string path = folder + "/" + name + ".mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.color = color;
        m.SetFloat("_Glossiness", smooth);
        if (emission > 0f)
        {
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            m.SetColor("_EmissionColor", color * emission);
        }
        EditorUtility.SetDirty(m);
        Mats[name] = m;
        return m;
    }

    private static GameObject Box(string name, Transform parent, Vector3 pos, Vector3 scale, Material mat, bool solid)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        if (!solid) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    private static Transform Group(string name, Transform parent, Vector3 pos, float rotY = 0f)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(0f, rotY, 0f);
        return go.transform;
    }

    // 空間に置く表示用のキャンバス。読む人は、置いた場所の手前(-Z 側)に立つ
    private static RectTransform MakeCanvas(string name, Transform parent, Vector3 pos, float wPx, float hPx)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        CanvasScaler scaler = go.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 3f;
        RectTransform rt = (RectTransform)go.transform;
        rt.sizeDelta = new Vector2(wPx, hPx);
        rt.localPosition = pos;
        rt.localScale = new Vector3(Px, Px, Px);
        return rt;
    }

    private static Text Label(RectTransform canvas, string name, float x, float y, float w, float h, int size, TextAnchor anchor, Color color, string text)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(canvas, false);
        RectTransform rt = (RectTransform)go.transform;
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = new Vector2(x, y);
        Text t = go.AddComponent<Text>();
        t.font = _font;
        t.fontSize = size;
        t.alignment = anchor;
        t.color = color;
        t.text = text;
        t.supportRichText = true;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    private static RectTransform Bar(RectTransform canvas, string name, float x, float y, float w, float h, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(canvas, false);
        RectTransform rt = (RectTransform)go.transform;
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = new Vector2(x, y);
        Image img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return rt;
    }

    // 押せるボタン。当たり判定のある箱に KscButton を付け、その手前に文字を重ねる。
    // 箱と文字を 1 つの親にまとめてあるので、親を隠せばボタンごと消える
    private static GameObject Button(RectTransform canvas, string name, float x, float y, float w, float h,
        int kind, int index, string label, string tip, out Text labelText)
    {
        GameObject holder = new GameObject(name, typeof(RectTransform));
        holder.transform.SetParent(canvas, false);
        RectTransform rt = (RectTransform)holder.transform;
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = new Vector2(x, y);
        // キャンバスは 1 ピクセル = Px メートルに縮めてあるので、箱の奥行きはその分だけ大きく書く
        GameObject box = Box("Box", rt, new Vector3(0f, 0f, 0.02f / Px), new Vector3(w, h, 0.03f / Px), Mat("Button", new Color(0.2f, 0.24f, 0.33f)), true);
        Wire(box, kind, index, tip);
        labelText = Label(rt, "Label", 0f, 0f, w - 10f, h, 30, TextAnchor.MiddleCenter, Color.white, label);
        labelText.resizeTextForBestFit = true;
        labelText.resizeTextMinSize = 14;
        labelText.resizeTextMaxSize = 30;
        return holder;
    }

    private static void Wire(GameObject go, int kind, int index, string tip)
    {
        KscButton b = UdonSharpUndo.AddComponent<KscButton>(go);
        b.game = _game;
        b.kind = kind;
        b.index = index;
        UdonSharpEditorUtility.CopyProxyToUdon(b);
        UdonBehaviour udon = UdonSharpEditorUtility.GetBackingUdonBehaviour(b);
        udon.interactText = tip;
        udon.proximity = 8f;
        EditorUtility.SetDirty(udon);
    }

    // ---------- 部屋 ----------
    private static void BuildRoom(Transform root)
    {
        Transform room = Group("Room", root, Vector3.zero);
        Material floor = Mat("Floor", new Color(0.13f, 0.14f, 0.17f));
        Material wall = Mat("Wall", new Color(0.08f, 0.09f, 0.11f));
        Box("Floor", room, new Vector3(0f, -0.1f, 0f), new Vector3(14f, 0.2f, 14f), floor, true);
        Box("WallBack", room, new Vector3(0f, 2.5f, 7f), new Vector3(14f, 5f, 0.2f), wall, true);
        Box("WallFront", room, new Vector3(0f, 2.5f, -7f), new Vector3(14f, 5f, 0.2f), wall, true);
        Box("WallLeft", room, new Vector3(-7f, 2.5f, 0f), new Vector3(0.2f, 5f, 14f), wall, true);
        Box("WallRight", room, new Vector3(7f, 2.5f, 0f), new Vector3(0.2f, 5f, 14f), wall, true);
        if (UnityEngine.Object.FindObjectOfType<Light>() == null)
        {
            GameObject light = new GameObject("Directional Light");
            light.transform.SetParent(room, false);
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            Light l = light.AddComponent<Light>();
            l.type = LightType.Directional;
            l.intensity = 1f;
        }
    }

    // ---------- スイッチ ----------
    private static void BuildSwitch(Transform root)
    {
        // スイッチは壁掛けの大きなボタンのように、入口のほう(-Z)を向いている
        Box("Stand", root, new Vector3(0f, 0.81f, 0.45f), new Vector3(1.2f, 1.62f, 0.3f), Mat("Stand", new Color(0.16f, 0.17f, 0.2f)), true);
        Transform sw = Group("Switch", root, new Vector3(0f, 1.25f, 0f));
        Material stem = Mat("Stem", new Color(0.24f, 0.48f, 0.88f));
        Material glow = Mat("Glow", new Color(0.4f, 0.75f, 1f), 0.9f);
        Material cap = Mat("Cap", new Color(0.17f, 0.18f, 0.2f), 0f, 0.5f);
        Box("Housing", sw, new Vector3(0f, 0f, 0.175f), new Vector3(0.6f, 0.6f, 0.25f), Mat("Housing", new Color(0.09f, 0.09f, 0.11f)), false);
        Box("StemV", sw, new Vector3(0f, 0f, 0f), new Vector3(0.07f, 0.22f, 0.2f), stem, false);
        Box("StemH", sw, new Vector3(0f, 0f, 0f), new Vector3(0.22f, 0.07f, 0.2f), stem, false);

        Transform keycap = Group("Keycap", sw, new Vector3(0f, 0f, -0.12f));
        Box("Cap", keycap, Vector3.zero, new Vector3(0.5f, 0.5f, 0.12f), cap, false);
        Box("Backlight", keycap, new Vector3(0f, 0f, 0.065f), new Vector3(0.54f, 0.54f, 0.01f), glow, false);
        RectTransform face = MakeCanvas("Face", keycap, new Vector3(0f, 0f, -0.0615f), 250f, 250f);
        Text legend = Label(face, "Legend", 0f, 0f, 250f, 250f, 150, TextAnchor.MiddleCenter, Color.white, "A");
        // ひび: 耐久が減るほど増える
        Material crackMat = Mat("Crack", new Color(0.02f, 0.02f, 0.02f));
        GameObject[] cracks = new GameObject[3];
        float[] crackX = { -0.12f, 0.1f, 0.02f }, crackY = { 0.1f, -0.06f, 0.14f }, crackRot = { 35f, -50f, 80f }, crackLen = { 0.3f, 0.34f, 0.24f };
        for (int i = 0; i < 3; i++)
        {
            cracks[i] = Box("Crack" + (i + 1), keycap, new Vector3(crackX[i], crackY[i], -0.0605f), new Vector3(crackLen[i], 0.012f, 0.002f), crackMat, false);
            cracks[i].transform.localRotation = Quaternion.Euler(0f, 0f, crackRot[i]);
        }

        // 叩く場所。レーザーやクリックが当たる箱(見えない)
        GameObject hit = new GameObject("HitArea");
        hit.transform.SetParent(sw, false);
        hit.transform.localPosition = new Vector3(0f, 0f, -0.05f);
        BoxCollider col = hit.AddComponent<BoxCollider>();
        col.size = new Vector3(0.62f, 0.62f, 0.4f);
        Wire(hit, 0, 0, "叩く");

        Transform center = Group("HitCenter", sw, new Vector3(0f, 0f, -0.18f));

        // 破片
        GameObject fxGo = new GameObject("Chips");
        fxGo.transform.SetParent(center, false);
        ParticleSystem ps = fxGo.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 4f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.06f);
        main.startRotation3D = true;
        main.startRotationX = new ParticleSystem.MinMaxCurve(0f, 6.28f);
        main.startRotationY = new ParticleSystem.MinMaxCurve(0f, 6.28f);
        main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, 6.28f);
        main.gravityModifier = 1f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 400;
        ParticleSystem.EmissionModule emission = ps.emission;
        emission.enabled = false;
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Hemisphere;
        shape.radius = 0.12f;
        shape.rotation = new Vector3(180f, 0f, 0f); // 手前へ飛ばす
        ParticleSystemRenderer pr = fxGo.GetComponent<ParticleSystemRenderer>();
        pr.renderMode = ParticleSystemRenderMode.Mesh;
        pr.mesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        pr.sharedMaterial = cap;
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        // 飛び出す数字
        RectTransform floatCanvas = MakeCanvas("Floats", sw, new Vector3(0f, 0.15f, -0.3f), 600f, 400f);
        Text[] floats = new Text[8];
        for (int i = 0; i < floats.Length; i++)
        {
            floats[i] = Label(floatCanvas, "Float" + i, 0f, 0f, 600f, 80f, 56, TextAnchor.MiddleCenter, Color.white, "");
            floats[i].horizontalOverflow = HorizontalWrapMode.Overflow;
        }

        _game.keycap = keycap;
        _game.hitCenter = center;
        _game.legend = legend;
        _game.cracks = cracks;
        _game.stemMat = stem;
        _game.glowMat = glow;
        _game.fx = ps;
        _game.floats = floats;
    }

    // ---------- 上の表示 ----------
    private static void BuildHud(Transform root)
    {
        Transform hud = Group("Hud", root, new Vector3(0f, 2.55f, 0.55f));
        Box("HudBoard", hud, new Vector3(0f, 0f, 0.03f), new Vector3(3.3f, 1.8f, 0.04f), Mat("Board", new Color(0.05f, 0.06f, 0.08f)), false);
        RectTransform c = MakeCanvas("HudCanvas", hud, Vector3.zero, 1600f, 860f);
        Color gold = new Color(1f, 0.82f, 0.29f), dim = new Color(0.7f, 0.73f, 0.8f);
        _game.txtScrap = Label(c, "Scrap", 0f, 350f, 1560f, 110f, 88, TextAnchor.MiddleCenter, gold, "スクラップ 0");
        _game.txtPower = Label(c, "Power", 0f, 265f, 1560f, 50f, 36, TextAnchor.MiddleCenter, dim, "");
        _game.txtFever = Label(c, "Fever", 0f, 195f, 1560f, 56f, 42, TextAnchor.MiddleCenter, new Color(1f, 0.55f, 0.2f), "");
        _game.txtCombo = Label(c, "Combo", 0f, 135f, 1560f, 50f, 38, TextAnchor.MiddleCenter, new Color(0.56f, 0.82f, 1f), "");
        _game.txtToast = Label(c, "Toast", 0f, 40f, 1540f, 110f, 36, TextAnchor.MiddleCenter, Color.white, "");
        _game.txtTarget = Label(c, "Target", 0f, -95f, 1560f, 70f, 54, TextAnchor.MiddleCenter, Color.white, "");
        _game.txtSub = Label(c, "Sub", 0f, -155f, 1560f, 44f, 30, TextAnchor.MiddleCenter, dim, "");
        Bar(c, "HpBack", 0f, -235f, 1200f, 60f, new Color(0.16f, 0.17f, 0.2f));
        RectTransform fill = Bar(c, "HpFill", -600f, -235f, 1200f, 60f, new Color(0.88f, 0.27f, 0.24f));
        fill.pivot = new Vector2(0f, 0.5f); // 左端を基準に縮む
        fill.anchoredPosition = new Vector2(-600f, -235f);
        _game.hpFill = fill;
        _game.txtHp = Label(c, "HpText", 0f, -235f, 1200f, 60f, 34, TextAnchor.MiddleCenter, Color.white, "");
        Label(c, "Help", 0f, -360f, 1560f, 90f, 28, TextAnchor.MiddleCenter, dim,
            "下の大きなキーを叩いて壊そう。デスクトップはクリックか、出ている文字と同じキー。VR はトリガーか、手で直接叩く。\n左の看板で工具を買い、右の看板で実績・軸・周回・音を見られます。");
    }

    // ---------- 36 キーの並び ----------
    private static void BuildKeyboard(Transform root)
    {
        Transform kb = Group("Keyboard", root, new Vector3(0f, 0.6f, 0.29f));
        Material idle = Mat("Key", new Color(0.24f, 0.25f, 0.29f));
        Material done = Mat("KeyDone", new Color(0.05f, 0.05f, 0.06f));
        RectTransform c = MakeCanvas("KeyboardCanvas", kb, new Vector3(0f, 0f, -0.035f), 560f, 260f);
        Renderer[] keys = new Renderer[Order.Length];
        const float pitch = 0.085f;
        for (int r = 0; r < KbRows.Length; r++)
        {
            for (int k = 0; k < KbRows[r].Length; k++)
            {
                string ch = KbRows[r].Substring(k, 1);
                float x = (k - 4.5f + KbRowShift[r]) * pitch;
                float y = (1.5f - r) * pitch - 0.02f;
                GameObject key = Box("Key" + ch, kb, new Vector3(x, y, -0.015f), new Vector3(0.07f, 0.07f, 0.03f), idle, false);
                keys[Order.IndexOf(ch, StringComparison.Ordinal)] = key.GetComponent<Renderer>();
                Label(c, "L" + ch, x / Px, y / Px, 40f, 40f, 24, TextAnchor.MiddleCenter, Color.white, ch);
            }
        }
        _game.txtKbd = Label(c, "Title", 0f, 110f, 560f, 30f, 20, TextAnchor.MiddleCenter, new Color(0.7f, 0.73f, 0.8f), "");
        _game.kbKeys = keys;
        _game.matKey = idle;
        _game.matKeyDone = done;
    }

    // ---------- 工具店(左の看板) ----------
    private static void BuildShop(Transform root)
    {
        Transform p = Group("Shop", root, new Vector3(-3.2f, 1.5f, 0.6f), -55f);
        Box("Board", p, new Vector3(0f, 0f, 0.03f), new Vector3(2.7f, 2.3f, 0.04f), Mat("Board", new Color(0.05f, 0.06f, 0.08f)), true);
        RectTransform c = MakeCanvas("ShopCanvas", p, new Vector3(0f, 0f, -0.04f), 1300f, 1100f);
        Color gold = new Color(1f, 0.82f, 0.29f), dim = new Color(0.7f, 0.73f, 0.8f);
        Text t;
        Label(c, "Heading", -430f, 505f, 400f, 60f, 44, TextAnchor.MiddleLeft, Color.white, "工具店");
        _game.shopTitle = Label(c, "Title", 180f, 505f, 800f, 60f, 34, TextAnchor.MiddleRight, gold, "");
        string[] tabs = { "工具", "技・自動破壊", "強化" };
        for (int i = 0; i < tabs.Length; i++) Button(c, "Tab" + i, -480f + i * 250f, 425f, 235f, 70f, 2, i, tabs[i], tabs[i], out t);
        Button(c, "BuyMode", 445f, 425f, 330f, 70f, 5, 0, "まとめ買い: ×1", "まとめ買いの個数を替える", out t);
        _game.txtBuyMode = t;

        const int rows = 8;
        Text[] names = new Text[rows], infos = new Text[rows], costs = new Text[rows];
        GameObject[] btns = new GameObject[rows];
        for (int i = 0; i < rows; i++)
        {
            float y = 320f - i * 92f;
            Bar(c, "RowBack" + i, 0f, y, 1260f, 84f, new Color(1f, 1f, 1f, 0.04f));
            names[i] = Label(c, "Name" + i, -250f, y + 18f, 740f, 44f, 34, TextAnchor.MiddleLeft, Color.white, "");
            infos[i] = Label(c, "Info" + i, -250f, y - 22f, 740f, 34f, 24, TextAnchor.MiddleLeft, dim, "");
            costs[i] = Label(c, "Cost" + i, 290f, y, 320f, 60f, 34, TextAnchor.MiddleRight, gold, "");
            names[i].horizontalOverflow = HorizontalWrapMode.Overflow;
            infos[i].horizontalOverflow = HorizontalWrapMode.Overflow;
            btns[i] = Button(c, "Buy" + i, 545f, y, 150f, 70f, 3, i, "買う", "買う", out t);
        }
        Button(c, "Prev", -220f, -480f, 160f, 70f, 4, -1, "◀ 前", "前のページ", out t);
        Button(c, "Next", 220f, -480f, 160f, 70f, 4, 1, "次 ▶", "次のページ", out t);
        _game.shopPage = Label(c, "Page", 0f, -480f, 240f, 60f, 34, TextAnchor.MiddleCenter, Color.white, "1 / 1");
        _game.rowName = names;
        _game.rowInfo = infos;
        _game.rowCost = costs;
        _game.rowBtn = btns;
    }

    // ---------- 記録の看板(右) ----------
    private static void BuildInfo(Transform root)
    {
        Transform p = Group("Info", root, new Vector3(3.2f, 1.5f, 0.6f), 55f);
        Box("Board", p, new Vector3(0f, 0f, 0.03f), new Vector3(2.7f, 2.3f, 0.04f), Mat("Board", new Color(0.05f, 0.06f, 0.08f)), true);
        RectTransform c = MakeCanvas("InfoCanvas", p, new Vector3(0f, 0f, -0.04f), 1300f, 1100f);
        Text t;
        string[] tabs = { "実績", "統計", "軸", "周回", "音" };
        for (int i = 0; i < tabs.Length; i++) Button(c, "Tab" + i, -500f + i * 250f, 505f, 235f, 70f, 6, i, tabs[i], tabs[i], out t);
        _game.infoTitle = Label(c, "Title", -210f, 420f, 820f, 60f, 40, TextAnchor.MiddleLeft, new Color(1f, 0.82f, 0.29f), "");
        Button(c, "Prev", 330f, 420f, 110f, 60f, 7, -1, "◀", "前のページ", out t);
        Button(c, "Next", 570f, 420f, 110f, 60f, 7, 1, "▶", "次のページ", out t);
        _game.infoPage = Label(c, "Page", 450f, 420f, 130f, 60f, 30, TextAnchor.MiddleCenter, Color.white, "");
        _game.infoBody = Label(c, "Body", 0f, 110f, 1240f, 530f, 26, TextAnchor.UpperLeft, Color.white, "");

        const int acts = 10;
        Text[] labels = new Text[acts];
        GameObject[] btns = new GameObject[acts];
        for (int i = 0; i < acts; i++)
        {
            float x = i % 2 == 0 ? -320f : 320f;
            float y = -205f - (i / 2) * 64f;
            btns[i] = Button(c, "Act" + i, x, y, 610f, 56f, 8, i, "", "押す", out t);
            labels[i] = t;
        }
        _game.actLabel = labels;
        _game.actBtn = btns;
    }

    // ---------- 金のキー ----------
    private static void BuildGold(Transform root)
    {
        GameObject gold = Box("GoldKey", root, new Vector3(1f, 1.3f, -0.8f), new Vector3(0.18f, 0.18f, 0.18f), Mat("Gold", new Color(1f, 0.79f, 0.24f), 0.8f, 0.8f), true);
        gold.transform.localRotation = Quaternion.Euler(35f, 0f, 45f);
        gold.GetComponent<Collider>().isTrigger = true; // 体が引っかからないように
        Wire(gold, 1, 0, "金のキー");
        _game.goldKey = gold;
    }

    // ---------- 音 ----------
    private static void BuildAudio(GameObject root)
    {
        _game.srcHit = Source(root.transform, "SoundHit");
        _game.srcAuto = Source(root.transform, "SoundAuto");
        _game.srcBreak = Source(root.transform, "SoundBreak");
        AudioClip[] clips = new AudioClip[AxisIds.Length * 6];
        int missing = 0;
        for (int a = 0; a < AxisIds.Length; a++)
        {
            for (int i = 0; i < 6; i++)
            {
                clips[a * 6 + i] = AssetDatabase.LoadAssetAtPath<AudioClip>(_dir + "/Audio/hit_" + AxisIds[a] + "_" + i + ".wav");
                if (clips[a * 6 + i] == null) missing++;
            }
        }
        _game.hitClips = clips;
        _game.breakSmall = AssetDatabase.LoadAssetAtPath<AudioClip>(_dir + "/Audio/break_small.wav");
        _game.breakBig = AssetDatabase.LoadAssetAtPath<AudioClip>(_dir + "/Audio/break_big.wav");
        if (missing > 0 || _game.breakSmall == null || _game.breakBig == null)
            Debug.LogWarning("[Keyswitch Crusher] 音のファイルが見つかりません(" + _dir + "/Audio)。音は鳴りません。");
    }

    private static AudioSource Source(Transform root, string name)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(root, false);
        AudioSource s = go.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.loop = false;
        s.spatialBlend = 0f; // 自分にだけ聞こえる音なので、場所によらず同じ大きさで鳴らす
        return s;
    }

    // ---------- VRChat のワールド設定 ----------
    private static void BuildWorldSettings(Transform root)
    {
        Transform spawn = Group("Spawn", root, new Vector3(0f, 0f, -2.2f));
        VRCSceneDescriptor desc = UnityEngine.Object.FindObjectOfType<VRCSceneDescriptor>();
        if (desc == null)
        {
            GameObject world = new GameObject("VRCWorld");
            Undo.RegisterCreatedObjectUndo(world, "Build Keyswitch Crusher");
            desc = world.AddComponent<VRCSceneDescriptor>();
            if (world.GetComponent<PipelineManager>() == null) world.AddComponent<PipelineManager>();
        }
        desc.spawns = new[] { spawn };
        desc.RespawnHeightY = -20f;
        EditorUtility.SetDirty(desc);
    }
}
#endif
