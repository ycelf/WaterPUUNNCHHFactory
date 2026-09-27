using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Editor-only setup. Automatic setup requires an explicit temporary request file.</summary>
public static class SwimmingStatusUISetup
{
    private const string ScenePath = "Assets/Scenes/Plant.unity";
    private const string RequestPath = "Temp/SwimmingStatusUISetup.request";
    private const string ResultPath = "Temp/SwimmingStatusUISetup.result.txt";

    [InitializeOnLoadMethod]
    private static void ScheduleRequestedSetup()
    {
        if (File.Exists(RequestPath))
        {
            EditorApplication.delayCall += InstallRequested;
        }
    }

    private static void InstallRequested()
    {
        if (!File.Exists(RequestPath)) return;

        if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.delayCall += InstallRequested;
            return;
        }

        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            File.WriteAllText(ResultPath, "WAITING: Open Plant and use Tools > Water UI > Create Swimming HUD.");
            return;
        }

        File.Delete(RequestPath);
        try
        {
            CreateInScene(scene);
            VerifyCounterBehaviour();
            File.WriteAllText(ResultPath, "SUCCESS: HUD created and references verified. Counter smoke checks passed. Save the scene with Ctrl+S.");
        }
        catch (Exception exception)
        {
            File.WriteAllText(ResultPath, "ERROR: " + exception);
            Debug.LogException(exception);
        }
    }

    [MenuItem("Tools/Water UI/Create Swimming HUD")]
    public static void CreateInActiveScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("请先退出 Play Mode，再创建游泳 HUD。");
            return;
        }

        CreateInScene(SceneManager.GetActiveScene());
    }

    private static void CreateInScene(Scene scene)
    {
        WaterPunchWaterSafety player = null;
        SwimmingStatusUI existing = null;
        int playerCount = 0;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (WaterPunchWaterSafety candidate in root.GetComponentsInChildren<WaterPunchWaterSafety>(true))
            {
                player = candidate;
                playerCount++;
            }

            if (existing == null) existing = root.GetComponentInChildren<SwimmingStatusUI>(true);
        }

        if (existing != null)
        {
            ValidateReferences(existing);
            Selection.activeGameObject = existing.gameObject;
            Debug.Log("当前场景已经有游泳 HUD，保留现有布局。", existing);
            return;
        }

        if (playerCount != 1)
        {
            throw new InvalidOperationException($"场景中找到 {playerCount} 个 WaterPunchWaterSafety，无法唯一确定玩家。");
        }

        GameObject canvasObject = new GameObject("SwimmingHUD", typeof(RectTransform));
        canvasObject.SetActive(false);
        SceneManager.MoveGameObjectToScene(canvasObject, scene);
        Undo.RegisterCreatedObjectUndo(canvasObject, "Create swimming HUD");

        try
        {
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            int widestRow = Mathf.Max(player.MaxSwimUses, player.MaxStruggleUses);
            float panelWidth = Mathf.Max(370f, 148f + widestRow * 44f);
            RectTransform panel = CreateRect("SwimmingPanel", canvasObject.transform,
                new Vector2(40f, 40f), new Vector2(panelWidth, 120f));
            panel.anchorMin = panel.anchorMax = panel.pivot = Vector2.zero;
            Image background = panel.gameObject.AddComponent<Image>();
            background.color = new Color(0.025f, 0.065f, 0.095f, 0.85f);
            background.raycastTarget = false;

            UIIconCounter swim = CreateRow(panel, "SwimRow", "SWIM", -16f,
                new Color(0.2f, 0.9f, 1f), true, panelWidth);
            UIIconCounter struggle = CreateRow(panel, "StruggleRow", "STRUGGLE", -66f,
                new Color(1f, 0.57f, 0.2f), false, panelWidth);

            SwimmingStatusUI presenter = canvasObject.AddComponent<SwimmingStatusUI>();
            SerializedObject serialized = new SerializedObject(presenter);
            serialized.FindProperty("playerWaterSafety").objectReferenceValue = player;
            serialized.FindProperty("panelRoot").objectReferenceValue = panel.gameObject;
            serialized.FindProperty("swimIcons").objectReferenceValue = swim;
            serialized.FindProperty("struggleIcons").objectReferenceValue = struggle;
            serialized.FindProperty("showOnlyInWater").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            ValidateReferences(presenter);
            canvasObject.SetActive(true);
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = canvasObject;
            Debug.Log("游泳 HUD 已创建：青色圆形=正常划水，橙色方形=挣扎；入水显示，用完变暗。Ctrl+S 保存场景。", canvasObject);
        }
        catch
        {
            Undo.DestroyObjectImmediate(canvasObject);
            throw;
        }
    }

    private static UIIconCounter CreateRow(Transform panel, string name, string label,
        float y, Color color, bool round, float panelWidth)
    {
        RectTransform caption = CreateRect(name + "Label", panel,
            new Vector2(16f, y), new Vector2(112f, 36f));
        Text text = caption.gameObject.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = label;
        text.fontSize = 17;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleLeft;
        text.color = color;
        text.raycastTarget = false;

        RectTransform row = CreateRect(name, panel,
            new Vector2(128f, y), new Vector2(panelWidth - 144f, 36f));
        HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = layout.childControlHeight = false;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;

        RectTransform template = CreateRect("IconTemplate", row, Vector2.zero, new Vector2(36f, 36f));
        Image image = template.gameObject.AddComponent<Image>();
        if (round) image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.color = color;
        template.gameObject.SetActive(false);

        UIIconCounter counter = row.gameObject.AddComponent<UIIconCounter>();
        SerializedObject serialized = new SerializedObject(counter);
        serialized.FindProperty("iconTemplate").objectReferenceValue = image;
        serialized.FindProperty("availableColor").colorValue = color;
        serialized.FindProperty("spentColor").colorValue = new Color(0.25f, 0.32f, 0.38f, 0.45f);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return counter;
    }

    private static RectTransform CreateRect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private static void ValidateReferences(SwimmingStatusUI presenter)
    {
        SerializedObject serialized = new SerializedObject(presenter);
        foreach (string field in new[] { "playerWaterSafety", "panelRoot", "swimIcons", "struggleIcons" })
        {
            if (serialized.FindProperty(field).objectReferenceValue == null)
                throw new InvalidOperationException("SwimmingStatusUI 缺少引用：" + field);
        }
    }

    [MenuItem("Tools/Water UI/Verify Icon Counter")]
    public static void VerifyCounterBehaviour()
    {
        GameObject testRoot = new GameObject("Swimming HUD verification", typeof(RectTransform));
        testRoot.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            UIIconCounter counter = CreateRow(testRoot.transform, "TestRow", "TEST", 0f, Color.cyan, true, 450f);
            counter.SetValue(6, 6);
            Require(CountIcons(counter, Color.cyan) == 6, "Initial count");
            int children = counter.transform.childCount;
            counter.SetValue(4, 6);
            Require(CountIcons(counter, Color.cyan) == 4, "Consumed count");
            Require(counter.transform.childCount == children, "Icon reuse");
            counter.SetValue(0, 6);
            Require(CountIcons(counter, Color.cyan) == 0, "Exhausted count");
            counter.SetValue(2, 2);
            Require(CountIcons(counter, Color.cyan) == 2, "Reduced capacity");
            counter.SetValue(8, 8);
            Require(CountIcons(counter, Color.cyan) == 8, "Increased capacity");
            counter.SetValue(0, 0);
            Require(counter.GetComponentsInChildren<Image>().Length == 0, "Zero capacity");
            Debug.Log("Swimming HUD: icon counter checks passed.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(testRoot);
        }
    }

    private static int CountIcons(UIIconCounter counter, Color color)
    {
        int result = 0;
        foreach (Image image in counter.GetComponentsInChildren<Image>())
            if (image.color == color) result++;
        return result;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Swimming HUD verification failed: " + message);
    }
}
