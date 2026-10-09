#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CatchGoose
{
    /// <summary>菜单 CatchGoose → 一键搭建场景：相机、锅、托盘、UI、五关数值，全部摆好。</summary>
    public static class SceneBuilder
    {
        const string ScenePath = "Assets/Scenes/Gameplay.unity";
        const string DataFolder = "Assets/Data";
        const string WokMeshPath = DataFolder + "/WokMesh.asset";
        const int AnchorCount = 9;
        const float AnchorSpacing = 0.82f;
        const float TrayY = 0.12f;
        const float TrayZ = -4.8f;

        static Font _font;

        [MenuItem("CatchGoose/一键搭建场景")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            CreateEventSystem();
            SetupCamera();
            var wok = CreateWok();
            var board = CreateBoard(wok.transform);
            SetObjectField(board, "wok", wok);
            var tilt = CreateTilt();
            SetObjectField(board, "tiltInput", tilt);
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
            cam.transform.position = new Vector3(0f, 9.0f, -8.6f);
            cam.transform.rotation = Quaternion.Euler(55f, 0f, 0f);
            cam.fieldOfView = 48f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.13f, 0.145f, 0.18f);
        }

        /// <summary>没有 EventSystem，UI 按钮点不动（画面能看见但没反应）。</summary>
        static void CreateEventSystem()
        {
            if (Object.FindObjectOfType<EventSystem>() != null) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        /// <summary>托盘只是装饰，留着碰撞体会挡住拾取射线（Cylinder 的 CapsuleCollider 会被半径撑高）。</summary>
        static void StripCollider(GameObject go)
        {
            var col = go.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
        }

        /// <summary>
        /// 一口大铁锅。网格是现场旋成的：内壁（锅心到锅口）→ 锅沿 → 外壁，剖面绕 Y 轴一圈。
        /// 不挂碰撞体——锅是纯装饰，有碰撞体反而会挡住拾取射线。
        /// </summary>
        static Wok CreateWok()
        {
            var pot = new GameObject("Pot");
            var wok = pot.AddComponent<Wok>();
            wok.shape = WokShape.Default;

            var mesh = SaveWokMesh(BuildWokMesh(wok.shape, 72, 24, 0.14f), WokMeshPath);
            var bowl = new GameObject("WokBowl", typeof(MeshFilter), typeof(MeshRenderer));
            bowl.transform.SetParent(pot.transform, false);
            bowl.GetComponent<MeshFilter>().sharedMesh = mesh;

            // 铁锅要深色，但不能死黑：金属度一高又没有反射探针，漫反射会被吃掉，
            // 锅壁就没有明暗过渡，看着像一块平板。所以留低金属度、靠漫反射的渐变撑出弧度。
            var mat = MakeMaterial("Mat_Wok", new Color(0.33f, 0.32f, 0.33f));
            mat.SetFloat("_Metallic", 0.12f);
            mat.SetFloat("_Glossiness", 0.42f);
            bowl.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return wok;
        }

        /// <summary>把剖面绕 Y 轴旋成实体。profiles 是一串 (半径, 高度)，顺序决定朝向。</summary>
        static Mesh BuildWokMesh(WokShape shape, int segments, int rings, float wall)
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            // 内壁：锅心 → 锅口。法线朝上朝内，正好是往锅里看的那一面。
            var inner = new List<Vector2>();
            for (int i = 0; i <= rings; i++)
            {
                float k = i / (float)rings;
                float r = shape.rimRadius * k;
                inner.Add(new Vector2(r, shape.SurfaceY(r)));
            }
            AddRevolveStrip(verts, uvs, tris, inner, segments);

            // 外壁：锅口 → 锅心，整体向外挪一个壁厚、向下沉一个壁厚。
            var outer = new List<Vector2>();
            for (int i = rings; i >= 0; i--)
            {
                float k = i / (float)rings;
                float r = shape.rimRadius * k;
                outer.Add(new Vector2(r + wall, shape.SurfaceY(r) - wall));
            }
            // 锅沿：内壁顶点 → 外壁顶点，一圈平沿
            AddRevolveStrip(verts, uvs, tris, new List<Vector2> { inner[rings], outer[0] }, segments);
            AddRevolveStrip(verts, uvs, tris, outer, segments);

            // 锅底封口：外壁收到半径 wall 的小圆，补一个朝下的扇面
            AddCenterFan(verts, uvs, tris, outer[outer.Count - 1], segments);

            var mesh = new Mesh { name = "WokMesh" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        static void AddRevolveStrip(List<Vector3> verts, List<Vector2> uvs, List<int> tris,
            List<Vector2> profile, int segments)
        {
            if (profile.Count < 2) return;
            int baseIndex = verts.Count;

            for (int i = 0; i < profile.Count; i++)
            {
                Vector2 p = profile[i];
                for (int a = 0; a < segments; a++)
                {
                    float t = a / (float)segments;
                    float ang = t * Mathf.PI * 2f;
                    verts.Add(new Vector3(Mathf.Cos(ang) * p.x, p.y, Mathf.Sin(ang) * p.x));
                    uvs.Add(new Vector2(t, i / (float)(profile.Count - 1)));
                }
            }

            for (int i = 0; i < profile.Count - 1; i++)
            {
                int rowA = baseIndex + i * segments;
                int rowB = rowA + segments;
                for (int a = 0; a < segments; a++)
                {
                    int a0 = rowA + a;
                    int a1 = rowA + (a + 1) % segments;
                    int b0 = rowB + a;
                    int b1 = rowB + (a + 1) % segments;
                    tris.Add(a0); tris.Add(a1); tris.Add(b1);
                    tris.Add(a0); tris.Add(b1); tris.Add(b0);
                }
            }
        }

        static void AddCenterFan(List<Vector3> verts, List<Vector2> uvs, List<int> tris,
            Vector2 ring, int segments)
        {
            int center = verts.Count;
            verts.Add(new Vector3(0f, ring.y, 0f));
            uvs.Add(new Vector2(0.5f, 0.5f));

            int ringStart = verts.Count;
            for (int a = 0; a < segments; a++)
            {
                float t = a / (float)segments;
                float ang = t * Mathf.PI * 2f;
                verts.Add(new Vector3(Mathf.Cos(ang) * ring.x, ring.y, Mathf.Sin(ang) * ring.x));
                uvs.Add(new Vector2(0.5f + Mathf.Cos(ang) * 0.5f, 0.5f + Mathf.Sin(ang) * 0.5f));
            }

            for (int a = 0; a < segments; a++)
            {
                tris.Add(center);
                tris.Add(ringStart + a);
                tris.Add(ringStart + (a + 1) % segments);
            }
        }

        static Mesh SaveWokMesh(Mesh built, string path)
        {
            if (!AssetDatabase.IsValidFolder(DataFolder)) AssetDatabase.CreateFolder("Assets", "Data");
            var asset = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (asset == null)
            {
                AssetDatabase.CreateAsset(built, path);
                return built;
            }
            asset.Clear();
            asset.SetVertices(new List<Vector3>(built.vertices));
            asset.SetUVs(0, new List<Vector2>(built.uv));
            asset.SetTriangles(built.triangles, 0);
            asset.RecalculateNormals();
            asset.RecalculateBounds();
            Object.DestroyImmediate(built);
            return asset;
        }

        /// <summary>倾斜输入：真机走陀螺仪，PC 用鼠标划动模拟；画面跟着轻微翻转。</summary>
        static TiltInput CreateTilt()
        {
            var go = new GameObject("TiltInput");
            var tilt = go.AddComponent<TiltInput>();
            var cam = Camera.main;
            if (cam != null) SetObjectField(tilt, "tiltVisual", cam.transform);
            return tilt;
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

            var bottomRight = new Vector2(1f, 0f);
            var tossButton = CreateButton(t, "TossBtn", "颠 锅", bottomRight, bottomRight, bottomRight,
                new Vector2(-122f, 108f), new Vector2(200f, 84f));

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
            SetObjectField(ui, "tossButton", tossButton);
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
                new LevelSpec("Level_01", 36, 6, 2, 0.00f, 8, 3.5f, 1.8f),
                new LevelSpec("Level_02", 63, 9, 3, 0.20f, 8, 3.6f, 2.0f),
                new LevelSpec("Level_03", 72, 9, 3, 0.35f, 8, 3.6f, 2.1f),
                new LevelSpec("Level_04", 90, 10, 4, 0.50f, 7, 3.7f, 2.2f),
                new LevelSpec("Level_05", 108, 12, 4, 0.60f, 7, 3.7f, 2.3f)
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
                cfg.spreadRadiusBottom = spec.radiusBottom;
                cfg.spreadRadiusTop = spec.radiusTop;
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
            public float radiusBottom, radiusTop;

            public LevelSpec(string name, int total, int types, int layers, float scatter, int slots,
                float radiusBottom, float radiusTop)
            {
                this.name = name;
                this.total = total;
                this.types = types;
                this.layers = layers;
                this.scatter = scatter;
                this.slots = slots;
                this.radiusBottom = radiusBottom;
                this.radiusTop = radiusTop;
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
