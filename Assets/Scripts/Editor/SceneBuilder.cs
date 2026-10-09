#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace CatchGoose
{
    /// <summary>菜单 CatchGoose → 一键搭建场景：相机、锅、托盘、UI、五关数值，全部摆好。</summary>
    public static class SceneBuilder
    {
        const string ScenePath = "Assets/Scenes/Gameplay.unity";
        const string DataFolder = "Assets/Data";
        const int AnchorCount = 9;
        const float AnchorSpacing = 0.82f;
        const float TrayY = 0.45f;
        const float TrayZ = -4.3f;

        static Font _font;

        [MenuItem("CatchGoose/一键搭建场景")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            SetupCamera();
            var board = CreateBoard(CreatePot().transform);
            var tray = CreateTray();
            var ui = CreateUI();
            CreateManager(board, tray, ui, CreateLevelConfigs());

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[CatchGoose] 场景已生成：" + ScenePath + " —— 打开它，直接点 Play。");
        }

        static void SetupCamera()
        {
            var cam = Camera.main;
            if (cam == null) return;
            cam.transform.position = new Vector3(0f, 8.2f, -7.6f);
            cam.transform.rotation = Quaternion.Euler(52f, 0f, 0f);
            cam.fieldOfView = 46f;
            cam.backgroundColor = new Color(0.12f, 0.13f, 0.16f);
        }

        static GameObject CreatePot()
        {
            var pot = new GameObject("Pot");
            var slab = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            slab.name = "PotBase";
            slab.transform.SetParent(pot.transform, false);
            slab.transform.position = new Vector3(0f, -0.28f, 0f);
            slab.transform.localScale = new Vector3(6.2f, 0.35f, 6.2f);
            slab.GetComponent<Renderer>().sharedMaterial = MakeMaterial("Mat_Pot", new Color(0.2f, 0.19f, 0.24f));
            StripCollider(slab);
            return pot;
        }

        /// <summary>锅体/托盘只是装饰，留着碰撞体会挡住拾取射线（Cylinder 的 CapsuleCollider 会被半径撑高）。</summary>
        static void StripCollider(GameObject go)
        {
            var col = go.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
        }

        static Board CreateBoard(Transform parent)
        {
            var go = new GameObject("Board");
            go.transform.SetParent(parent, false);
            return go.AddComponent<Board>();
        }

        static Tray CreateTray()
        {
            var go = new GameObject("Tray");
            var tray = go.AddComponent<Tray>();

            var slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slab.name = "TraySlab";
            slab.transform.SetParent(go.transform, false);
            slab.transform.position = new Vector3(0f, TrayY - 0.38f, TrayZ);
            slab.transform.localScale = new Vector3(AnchorCount * AnchorSpacing + 0.5f, 0.16f, 1.15f);
            slab.GetComponent<Renderer>().sharedMaterial = MakeMaterial("Mat_Tray", new Color(0.26f, 0.28f, 0.35f));
            StripCollider(slab);

            var anchors = new Transform[AnchorCount];
            for (int i = 0; i < AnchorCount; i++)
            {
                var anchor = new GameObject("Anchor_" + i).transform;
                anchor.SetParent(go.transform, false);
                anchor.position = new Vector3((i - (AnchorCount - 1) / 2f) * AnchorSpacing, TrayY, TrayZ);
                anchors[i] = anchor;
            }
            SetArrayField(tray, "anchors", anchors);
            return tray;
        }

        static GameUI CreateUI()
        {
            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);

            var t = canvasGo.transform;
            var topLeft = new Vector2(0f, 1f);
            var levelText = CreateText(t, "LevelText", "第 1 关", 30, topLeft, topLeft, topLeft,
                new Vector2(26f, -24f), new Vector2(320f, 44f));
            var remainText = CreateText(t, "RemainText", "锅里剩 0", 22, topLeft, topLeft, topLeft,
                new Vector2(26f, -74f), new Vector2(320f, 36f));
            var trayText = CreateText(t, "TrayText", "托盘 0 / 8", 22, topLeft, topLeft, topLeft,
                new Vector2(26f, -112f), new Vector2(320f, 36f));

            var bottom = new Vector2(0.5f, 0f);
            var undoButton = CreateButton(t, "UndoBtn", "撤回 3", bottom, bottom, bottom,
                new Vector2(-190f, 34f), new Vector2(170f, 62f));
            var shuffleButton = CreateButton(t, "ShuffleBtn", "洗牌 2", bottom, bottom, bottom,
                new Vector2(0f, 34f), new Vector2(170f, 62f));
            var extractButton = CreateButton(t, "ExtractBtn", "清盘 1", bottom, bottom, bottom,
                new Vector2(190f, 34f), new Vector2(170f, 62f));

            var center = new Vector2(0.5f, 0.5f);
            var panel = new GameObject("ResultPanel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(t, false);
            var panelRt = panel.GetComponent<RectTransform>();
            panelRt.anchorMin = center; panelRt.anchorMax = center; panelRt.pivot = center;
            panelRt.anchoredPosition = Vector2.zero; panelRt.sizeDelta = new Vector2(460f, 260f);
            panel.GetComponent<Image>().color = new Color(0.1f, 0.11f, 0.14f, 0.94f);

            var resultText = CreateText(panel.transform, "ResultText", "过关", 30, center, center, center,
                new Vector2(0f, 46f), new Vector2(420f, 60f));
            var nextButton = CreateButton(panel.transform, "NextBtn", "下一关", center, center, center,
                new Vector2(-104f, -58f), new Vector2(180f, 62f));
            var retryButton = CreateButton(panel.transform, "RetryBtn", "再来一把", center, center, center,
                new Vector2(104f, -58f), new Vector2(180f, 62f));

            var ui = canvasGo.AddComponent<GameUI>();
            SetObjectField(ui, "levelText", levelText);
            SetObjectField(ui, "remainText", remainText);
            SetObjectField(ui, "trayText", trayText);
            SetObjectField(ui, "undoButton", undoButton);
            SetObjectField(ui, "shuffleButton", shuffleButton);
            SetObjectField(ui, "extractButton", extractButton);
            SetObjectField(ui, "resultPanel", panel);
            SetObjectField(ui, "resultText", resultText);
            SetObjectField(ui, "nextButton", nextButton);
            SetObjectField(ui, "retryButton", retryButton);
            return ui;
        }

        static void CreateManager(Board board, Tray tray, GameUI ui, LevelConfig[] levels)
        {
            var go = new GameObject("GameManager");
            var manager = go.AddComponent<GameManager>();
            SetObjectField(manager, "board", board);
            SetObjectField(manager, "tray", tray);
            SetObjectField(manager, "ui", ui);
            SetArrayField(manager, "levels", levels);
        }

        static LevelConfig[] CreateLevelConfigs()
        {
            if (!AssetDatabase.IsValidFolder(DataFolder)) AssetDatabase.CreateFolder("Assets", "Data");
            var specs = new[]
            {
                new LevelSpec("Level_01", 36, 6, 2, 0.00f, 8),
                new LevelSpec("Level_02", 54, 8, 3, 0.20f, 8),
                new LevelSpec("Level_03", 72, 9, 3, 0.35f, 8),
                new LevelSpec("Level_04", 90, 10, 4, 0.50f, 7),
                new LevelSpec("Level_05", 108, 12, 4, 0.60f, 7)
            };
            var list = new List<LevelConfig>();
            foreach (var spec in specs)
            {
                var cfg = ScriptableObject.CreateInstance<LevelConfig>();
                cfg.totalItems = spec.total;
                cfg.typeCount = spec.types;
                cfg.layers = spec.layers;
                cfg.scatter = spec.scatter;
                cfg.traySlots = spec.slots;
                cfg.undoCount = 3;
                cfg.shuffleCount = 2;
                cfg.extractCount = 1;
                cfg.useRandomSeed = true;
                AssetDatabase.CreateAsset(cfg, DataFolder + "/" + spec.name + ".asset");
                list.Add(cfg);
            }
            return list.ToArray();
        }

        struct LevelSpec
        {
            public string name;
            public int total, types, layers, slots;
            public float scatter;

            public LevelSpec(string name, int total, int types, int layers, float scatter, int slots)
            {
                this.name = name;
                this.total = total;
                this.types = types;
                this.layers = layers;
                this.scatter = scatter;
                this.slots = slots;
            }
        }

        static Text CreateText(Transform parent, string name, string content, int size,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 sizeDelta)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.pivot = pivot;
            rt.anchoredPosition = pos; rt.sizeDelta = sizeDelta;

            var text = go.AddComponent<Text>();
            text.text = content;
            text.font = UiFont;
            text.fontSize = size;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            return text;
        }

        static Button CreateButton(Transform parent, string name, string label,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 sizeDelta)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.pivot = pivot;
            rt.anchoredPosition = pos; rt.sizeDelta = sizeDelta;
            go.GetComponent<Image>().color = new Color(0.24f, 0.26f, 0.33f);

            var stretch = Vector2.zero;
            var one = Vector2.one;
            var half = new Vector2(0.5f, 0.5f);
            CreateText(go.transform, "Label", label, 22, stretch, one, half, Vector2.zero, Vector2.zero);
            return go.GetComponent<Button>();
        }

        static Material MakeMaterial(string name, Color color)
        {
            if (!AssetDatabase.IsValidFolder(DataFolder)) AssetDatabase.CreateFolder("Assets", "Data");
            var mat = new Material(Shader.Find("Standard"));
            mat.color = color;
            AssetDatabase.CreateAsset(mat, DataFolder + "/" + name + ".mat");
            return mat;
        }

        static Font UiFont
        {
            get
            {
                if (_font != null) return _font;
                _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (_font == null) _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                return _font;
            }
        }

        static void SetObjectField(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetArrayField(Object target, string field, Object[] values)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            prop.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
