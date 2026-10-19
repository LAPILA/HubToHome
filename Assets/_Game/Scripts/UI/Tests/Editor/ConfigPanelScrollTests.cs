using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using TMPro;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class ConfigPanelScrollTests
{
    private const string UiManagerPath =
        "Assets/_Game/Core/Prefabs/CoreSettings/UIManager.prefab";
    private const BindingFlags InstancePrivate =
        BindingFlags.Instance | BindingFlags.NonPublic;
    private const float PositionTolerance = 0.5f;

    private readonly List<GameObject> _createdObjects = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        for (int i = _createdObjects.Count - 1; i >= 0; i--)
        {
            GameObject created = _createdObjects[i];
            if (created == null)
                continue;

            ConfigPanelUI[] panels = created.GetComponentsInChildren<ConfigPanelUI>(true);
            for (int panelIndex = 0; panelIndex < panels.Length; panelIndex++)
                GetRows(panels[panelIndex]).Clear();

            UnityEngine.Object.DestroyImmediate(created);
        }

        _createdObjects.Clear();
    }

    [TestCase(-50f, 1f)]
    [TestCase(-250f, 0.5f)]
    [TestCase(-450f, 0f)]
    public void CalculateVerticalNormalizedPositionSupportsTopPivot(
        float rowCenterY,
        float expected)
    {
        MethodInfo method = typeof(ConfigPanelUI).GetMethod(
            "CalculateVerticalNormalizedPosition",
            BindingFlags.Static | BindingFlags.NonPublic,
            null,
            new[] { typeof(float), typeof(float), typeof(float), typeof(float) },
            null);

        Assert.That(
            method,
            Is.Not.Null,
            "ConfigPanelUI에 contentRectYMax, localRowCenterY, contentHeight, viewportHeight 순서의 "
            + "CalculateVerticalNormalizedPosition(float, float, float, float)가 필요합니다.");

        object result = method.Invoke(null, new object[] { 0f, rowCenterY, 500f, 100f });
        Assert.That(Convert.ToSingle(result), Is.EqualTo(expected).Within(0.001f));
    }

    [TestCase(LanguageType.KR, "한국어")]
    [TestCase(LanguageType.EN, "English")]
    [TestCase(LanguageType.JP, "日本語")]
    [TestCase(LanguageType.CN, "简体中文")]
    public void LanguageValuesUseReadableNativeNames(LanguageType language, string expected)
    {
        MethodInfo method = typeof(ConfigPanelUI).GetMethod(
            "LanguageDisplayName", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        Assert.That(method.Invoke(null, new object[] { language }), Is.EqualTo(expected));
    }

    [TestCase(RuntimePlatform.WindowsPlayer, true)]
    [TestCase(RuntimePlatform.OSXPlayer, true)]
    [TestCase(RuntimePlatform.LinuxPlayer, true)]
    [TestCase(RuntimePlatform.WindowsEditor, true)]
    [TestCase(RuntimePlatform.Android, false)]
    [TestCase(RuntimePlatform.IPhonePlayer, false)]
    [TestCase(RuntimePlatform.Switch, false)]
    public void WindowOptionsAreDesktopOnly(RuntimePlatform platform, bool expected)
    {
        MethodInfo method = typeof(ConfigPanelUI).GetMethod(
            "SupportsWindowSettings", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        Assert.That(method.Invoke(null, new object[] { platform }), Is.EqualTo(expected));
    }

    [TestCase(-1)]
    [TestCase(999)]
    public void InvalidStoredKeysUseTheLocalizedUnboundLabel(int invalidValue)
    {
        MethodInfo method = typeof(ConfigPanelUI).GetMethod(
            "KeyDisplayName", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        object unbound = method.Invoke(null, new object[] { Key.None });
        Assert.That(method.Invoke(null, new object[] { (Key)invalidValue }), Is.EqualTo(unbound));
    }

    [Test]
    public void AwakeNormalizesItsCanvasWithoutDependingOnTheOpenPath()
    {
        GameObject owner = CreateInactiveObject(
            "Config Canvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(CanvasGroup));
        ConfigPanelUI panel = owner.AddComponent<ConfigPanelUI>();
        GameObject previewObject = CreateChild(
            owner.transform,
            "Preview",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        TextMeshProUGUI preview = previewObject.GetComponent<TextMeshProUGUI>();
        preview.maxVisibleLines = 99999;
        Vector2 previewPosition = new Vector2(78f, -160f);
        preview.rectTransform.anchoredPosition = previewPosition;
        SetPrivateField(panel, "_gameplayPreviewText", preview);
        RectTransform rect = owner.transform as RectTransform;
        CanvasScaler scaler = owner.GetComponent<CanvasScaler>();
        rect.localScale = Vector3.zero;
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;

        InvokeInstance(panel, "Awake");

        Assert.That(rect.localScale, Is.EqualTo(Vector3.one));
        Assert.That(scaler.uiScaleMode, Is.EqualTo(CanvasScaler.ScaleMode.ScaleWithScreenSize));
        Assert.That(scaler.referenceResolution, Is.EqualTo(GameConfigPolicy.ReferenceResolution));
        Assert.That(scaler.screenMatchMode, Is.EqualTo(CanvasScaler.ScreenMatchMode.MatchWidthOrHeight));
        Assert.That(preview.maxVisibleLines, Is.EqualTo(2));

        preview.rectTransform.anchoredPosition = Vector2.zero;
        InvokeInstance(panel, "RestorePreviewPosition");
        Assert.That(preview.rectTransform.anchoredPosition, Is.EqualTo(previewPosition));
    }

    [Test]
    public void SelectionUsesColorWithoutChangingTextSizeOrScale()
    {
        GameObject owner = CreateInactiveObject("Config Selection", typeof(RectTransform), typeof(CanvasGroup));
        ConfigPanelUI panel = owner.AddComponent<ConfigPanelUI>();
        GameObject labelObject = CreateChild(owner.transform, "Label", typeof(RectTransform),
            typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
        label.fontSize = 20f;
        label.enableAutoSizing = true;
        label.rectTransform.localScale = Vector3.one;
        MethodInfo applyVisual = typeof(ConfigPanelUI).GetMethod("ApplyVisual", InstancePrivate);
        Assert.That(applyVisual, Is.Not.Null);

        applyVisual.Invoke(panel, new object[] { label, true, true });
        Color selectedColor = label.color;
        Assert.That(label.fontSize, Is.EqualTo(20f));
        Assert.That(label.enableAutoSizing, Is.True);
        Assert.That(label.rectTransform.localScale, Is.EqualTo(Vector3.one));

        applyVisual.Invoke(panel, new object[] { label, false, true });
        Assert.That(label.color, Is.Not.EqualTo(selectedColor));
        Assert.That(label.fontSize, Is.EqualTo(20f));
        Assert.That(label.rectTransform.localScale, Is.EqualTo(Vector3.one));
    }

    [Test]
    public void NonOwningPanelCannotReleaseAnotherConfigModal()
    {
        bool previous = GameInput.IsConfigModalActive;
        GameObject owner = CreateInactiveObject("Non Owner Config", typeof(RectTransform), typeof(CanvasGroup));
        ConfigPanelUI panel = owner.AddComponent<ConfigPanelUI>();
        try
        {
            GameInput.SetConfigModalActive(true);
            InvokeInstance(panel, "ReleaseModalState");
            Assert.That(GameInput.IsConfigModalActive, Is.True);
        }
        finally { GameInput.SetConfigModalActive(previous); }
    }

    [Test]
    public void RowLabelsHaveIndependentWidthsAndCannotOverflow()
    {
        GameObject owner = CreateInactiveObject("Label Layout", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        TextMeshProUGUI label = owner.GetComponent<TextMeshProUGUI>();
        label.fontSize = 18;
        typeof(ConfigPanelUI).GetMethod("ConfigureRowText", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { label, true });
        Assert.That(label.enableAutoSizing, Is.True);
        Assert.That(label.fontSizeMax, Is.EqualTo(18));
        Assert.That(label.overflowMode, Is.EqualTo(TextOverflowModes.Ellipsis));
        Assert.That(label.raycastTarget, Is.False);
        Assert.That(label.GetComponent<LayoutElement>().minWidth, Is.Zero);
    }

    [Test]
    public void MissingViewportIsRejectedWithoutCreatingOrInferringRuntimeContent()
    {
        GameObject rowPrefab = CreateValidRowPrefab("Valid Row Prefab");
        ConfigPanelUI panel = CreatePanelFixture(rowPrefab, bindViewport: false);
        RectTransform content = GetPrivateField<Transform>(panel, "_detailRoot") as RectTransform;

        LogAssert.Expect(
            LogType.Error,
            new Regex("config_panel_scroll_contract_invalid.*viewport"));

        InvokeInstance(panel, "RebuildRows");

        Assert.That(GetRows(panel).Count, Is.EqualTo(0));
        Assert.That(FindDescendant(content.parent, "__AutoScrollContent"), Is.Null);
    }

    [Test]
    public void InvalidRowPrefabIsRejectedBeforeSpawningAnyRows()
    {
        GameObject invalidRowPrefab = CreateInvalidRowPrefab("Invalid Row Prefab");
        ConfigPanelUI panel = CreatePanelFixture(invalidRowPrefab, bindViewport: true);

        LogAssert.Expect(
            LogType.Error,
            new Regex(
                "config_panel_row_contract_invalid.*(HorizontalLayoutGroup|LayoutElement|TMP)"));

        InvokeInstance(panel, "RebuildRows");

        Assert.That(GetRows(panel).Count, Is.EqualTo(0));
        RectTransform content = GetPrivateField<Transform>(panel, "_detailRoot") as RectTransform;
        for (int i = 0; i < content.childCount; i++)
        {
            Assert.That(
                content.GetChild(i).gameObject.activeSelf,
                Is.False,
                "거부된 Row 인스턴스는 지연 Destroy 전에 비활성화돼야 합니다.");
        }
    }

    [Test]
    public void SystemCategoryOnlyCreatesDisplaySizeAndResetRows()
    {
        GameObject rowPrefab = CreateValidRowPrefab("Valid Row Prefab");
        ConfigPanelUI panel = CreatePanelFixture(rowPrefab, bindViewport: true);
        SetEnumField(panel, "_selectedCategory", "System");

        InvokeInstance(panel, "RebuildRows");

        List<GameObject> rows = GetRowObjects(panel);
        Assert.That(rows, Has.Count.EqualTo(3));
        Assert.That(rows[0].name, Is.EqualTo("Row_Fullscreen"));
        Assert.That(rows[1].name, Is.EqualTo("Row_WindowScale"));
        Assert.That(rows[2].name, Is.EqualTo("Row_ResetDefault"));
    }

    [Test]
    public void PointerRowsAddLocalButtonsWithoutMutatingTheTemplate()
    {
        GameObject template = CreateValidRowPrefab("Pointer Row Template");
        ConfigPanelUI panel = CreatePanelFixture(template, bindViewport: true);

        InvokeInstance(panel, "RebuildRows");

        Assert.That(template.GetComponent<Button>(), Is.Null);
        Assert.That(template.GetComponentsInChildren<TextMeshProUGUI>(true), Has.Length.EqualTo(2));
        List<GameObject> rows = GetRowObjects(panel);
        Assert.That(rows, Has.Count.EqualTo(3));
        foreach (GameObject row in rows)
        {
            AssertLocalButton(row.GetComponent<Button>());
            Assert.That(row.GetComponent<Image>().raycastTarget, Is.True);
            foreach (string name in new[] { "Decrease", "Increase" })
            {
                Transform control = row.transform.Find(name);
                Assert.That(control, Is.Not.Null);
                AssertLocalButton(control.GetComponent<Button>());
                Assert.That(control.GetComponent<LayoutElement>().preferredWidth, Is.EqualTo(32f));
                Assert.That(control.GetComponentInChildren<TextMeshProUGUI>().raycastTarget, Is.False);
            }
        }
    }

    [Test]
    public void KeyBindingAndResetRowsDoNotCreateValueAdjustmentButtons()
    {
        ConfigPanelUI panel = CreatePanelFixture(CreateValidRowPrefab("Controls Template"), bindViewport: true);
        SetEnumField(panel, "_selectedCategory", "Controls");

        InvokeInstance(panel, "RebuildRows");

        foreach (GameObject row in GetRowObjects(panel))
        {
            AssertLocalButton(row.GetComponent<Button>());
            Assert.That(row.transform.Find("Decrease"), Is.Null);
            Assert.That(row.transform.Find("Increase"), Is.Null);
        }
    }

    [Test]
    public void PointerControlsAreReusedAcrossRepeatedOpenPreparation()
    {
        ConfigPanelUI panel = CreatePanelFixture(CreateValidRowPrefab("Reusable Template"), bindViewport: true);
        panel.gameObject.AddComponent<Canvas>();
        panel.gameObject.AddComponent<GraphicRaycaster>();

        InvokeInstance(panel, "EnsurePointerControls");
        FieldMenuTouchInput bridge = panel.GetComponent<FieldMenuTouchInput>();
        Button back = GetPrivateField<Button>(panel, "_backButton");
        InvokeInstance(panel, "EnsurePointerControls");

        Assert.That(panel.GetComponents<FieldMenuTouchInput>(), Has.Length.EqualTo(1));
        Assert.That(panel.GetComponent<FieldMenuTouchInput>(), Is.SameAs(bridge));
        Assert.That(GetPrivateField<Button>(panel, "_backButton"), Is.SameAs(back));
        AssertLocalButton(back);
        Assert.That(((RectTransform)back.transform).sizeDelta, Is.EqualTo(new Vector2(124f, 32f)));
    }

    [Test]
    public void PointerGateRejectsClosingOpeningFramesAndAlreadyHandledInput()
    {
        ConfigPanelUI panel = CreatePanelFixture(CreateValidRowPrefab("Guard Template"), bindViewport: true);
        FieldInfo modalOwner = typeof(ConfigPanelUI).GetField("_modalOwner", BindingFlags.Static | BindingFlags.NonPublic);
        object previousOwner = modalOwner.GetValue(null);
        panel.gameObject.SetActive(true);
        CanvasGroup group = panel.GetComponent<CanvasGroup>();
        typeof(UIPanel).GetField("_canvasGroup", InstancePrivate).SetValue(panel, group);
        group.alpha = 1f;
        SetPrivateField(panel, "_ownsModalState", true);
        SetPrivateField(panel, "_skipOneFrame", false);
        modalOwner.SetValue(null, panel);
        MethodInfo canPointer = typeof(ConfigPanelUI).GetMethod("CanPointerInteract", InstancePrivate);
        try
        {
            // An unrelated live UIManager may own another top panel in an editor test scene.
            if (UIManager.Instance != null && UIManager.Instance.TopPanel != panel)
                Assert.Ignore("An existing UIManager owns a different panel in this editor scene.");
            Assert.That(canPointer.Invoke(panel, null), Is.True);
            SetPrivateField(panel, "_closing", true);
            Assert.That(canPointer.Invoke(panel, null), Is.False);
            SetPrivateField(panel, "_closing", false);
            SetPrivateField(panel, "_skipOneFrame", true);
            Assert.That(canPointer.Invoke(panel, null), Is.False);
            SetPrivateField(panel, "_skipOneFrame", false);
            SetPrivateField(panel, "_lastHandledInputFrame", Time.frameCount);
            Assert.That(canPointer.Invoke(panel, null), Is.False);
            SetPrivateField(panel, "_lastHandledInputFrame", -1);
            modalOwner.SetValue(null, null);
            Assert.That(canPointer.Invoke(panel, null), Is.False);
        }
        finally
        {
            SetPrivateField(panel, "_ownsModalState", false);
            modalOwner.SetValue(null, previousOwner);
        }
    }

    private static void AssertLocalButton(Button button)
    {
        Assert.That(button, Is.Not.Null);
        Assert.That(button.navigation.mode, Is.EqualTo(Navigation.Mode.None));
        Assert.That(button.transition, Is.EqualTo(Selectable.Transition.None));
    }

    [Test]
    public void ControlsSelectionAndCategoryChangeKeepRowsInsideTheViewport()
    {
        ConfigPanelUI panel = InstantiateConfigPanelAsset();
        ScrollRect scroll = GetPrivateField<ScrollRect>(panel, "_scrollRect");

        Assert.That(scroll, Is.Not.Null);
        Assert.That(scroll.viewport, Is.Not.Null, "Prefab migration must bind ScrollRect.viewport.");
        Assert.That(scroll.content, Is.Not.Null, "Prefab migration must bind ScrollRect.content.");

        SetEnumField(panel, "_selectedCategory", "Controls");
        SetEnumField(panel, "_focus", "RowList");
        InvokeInstance(panel, "RebuildRows");
        RebuildLayout(scroll.content);

        List<GameObject> controlsRows = GetRowObjects(panel);
        Assert.That(controlsRows, Has.Count.EqualTo(9));
        Assert.That(FindDescendant(scroll.viewport, "__AutoScrollContent"), Is.Null);

        int[] indices = { 0, controlsRows.Count / 2, controlsRows.Count - 1 };
        for (int i = 0; i < indices.Length; i++)
        {
            int rowIndex = indices[i];
            SetPrivateField(panel, "_rowIndex", rowIndex);
            InvokeInstance(panel, "EnsureSelectedRowVisible");
            Canvas.ForceUpdateCanvases();
            AssertInsideViewport(
                scroll.viewport,
                controlsRows[rowIndex].transform as RectTransform,
                "Controls row " + rowIndex);
        }

        List<GameObject> oldRows = new List<GameObject>(controlsRows);
        SetEnumField(panel, "_selectedCategory", "Audio");
        SetPrivateField(panel, "_rowIndex", 0);
        InvokeInstance(panel, "RebuildRows");
        RebuildLayout(scroll.content);

        Assert.That(
            scroll.content.anchoredPosition.y,
            Is.EqualTo(0f).Within(0.01f),
            "카테고리 변경 시 Content는 상단으로 복귀해야 합니다.");
        if (scroll.content.rect.height > scroll.viewport.rect.height + 0.01f)
        {
            Assert.That(
                scroll.verticalNormalizedPosition,
                Is.EqualTo(1f).Within(0.001f),
                "스크롤 가능한 카테고리는 상단으로 복귀해야 합니다.");
        }

        for (int i = 0; i < oldRows.Count; i++)
        {
            if (oldRows[i] != null)
                Assert.That(oldRows[i].activeSelf, Is.False, "이전 행은 Destroy 전에 비활성화돼야 합니다.");
        }

        List<GameObject> audioRows = GetRowObjects(panel);
        Assert.That(audioRows, Has.Count.EqualTo(3));
        AssertInsideViewport(
            scroll.viewport,
            audioRows[0].transform as RectTransform,
            "Audio first row");
    }

    private ConfigPanelUI InstantiateConfigPanelAsset()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(UiManagerPath);
        Assert.That(prefab, Is.Not.Null, "Prefab을 찾을 수 없습니다: " + UiManagerPath);
        Transform settingPanel = FindDescendant(prefab.transform, "SettingPanel");
        Assert.That(settingPanel, Is.Not.Null);

        GameObject instance = UnityEngine.Object.Instantiate(settingPanel.gameObject);
        instance.name = "SettingPanel Test Instance";
        _createdObjects.Add(instance);
        if (!instance.activeSelf)
            instance.SetActive(true);

        ConfigPanelUI panel = instance.GetComponent<ConfigPanelUI>();
        Assert.That(panel, Is.Not.Null);
        return panel;
    }

    private ConfigPanelUI CreatePanelFixture(GameObject rowPrefab, bool bindViewport)
    {
        GameObject panelObject = CreateInactiveObject(
            "Config Panel Fixture",
            typeof(RectTransform),
            typeof(CanvasGroup));
        ConfigPanelUI panel = panelObject.AddComponent<ConfigPanelUI>();

        GameObject viewportObject = CreateChild(
            panelObject.transform,
            "Viewport",
            typeof(RectTransform),
            typeof(RectMask2D),
            typeof(ScrollRect));
        RectTransform viewport = viewportObject.transform as RectTransform;
        viewport.sizeDelta = new Vector2(340f, 250f);

        GameObject contentObject = CreateChild(
            viewport,
            "Content",
            typeof(RectTransform),
            typeof(VerticalLayoutGroup),
            typeof(ContentSizeFitter));
        RectTransform content = contentObject.transform as RectTransform;
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;

        ScrollRect scroll = viewportObject.GetComponent<ScrollRect>();
        scroll.content = content;
        scroll.viewport = bindViewport ? viewport : null;

        SetPrivateField(panel, "_detailRoot", content);
        SetPrivateField(panel, "_rowPrefab", rowPrefab);
        SetPrivateField(panel, "_scrollRect", scroll);
        return panel;
    }

    private GameObject CreateValidRowPrefab(string name)
    {
        GameObject row = CreateInactiveObject(
            name,
            typeof(RectTransform),
            typeof(HorizontalLayoutGroup),
            typeof(LayoutElement));
        CreateTextColumn(row.transform, "Name", 208f, 1f);
        CreateTextColumn(row.transform, "Value", 96f, 0f);
        return row;
    }

    private GameObject CreateInvalidRowPrefab(string name)
    {
        GameObject row = CreateInactiveObject(name, typeof(RectTransform));
        CreateTextColumn(row.transform, "Only Text", 208f, 1f);
        return row;
    }

    private static void CreateTextColumn(
        Transform parent,
        string name,
        float preferredWidth,
        float flexibleWidth)
    {
        var textObject = new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI),
            typeof(LayoutElement));
        textObject.transform.SetParent(parent, false);
        LayoutElement element = textObject.GetComponent<LayoutElement>();
        element.preferredWidth = preferredWidth;
        element.flexibleWidth = flexibleWidth;
    }

    private GameObject CreateInactiveObject(string name, params Type[] components)
    {
        var gameObject = new GameObject(name, components);
        gameObject.SetActive(false);
        _createdObjects.Add(gameObject);
        return gameObject;
    }

    private static GameObject CreateChild(
        Transform parent,
        string name,
        params Type[] components)
    {
        var child = new GameObject(name, components);
        child.transform.SetParent(parent, false);
        return child;
    }

    private static void RebuildLayout(RectTransform content)
    {
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        Canvas.ForceUpdateCanvases();
    }

    private static void AssertInsideViewport(
        RectTransform viewport,
        RectTransform row,
        string label)
    {
        Assert.That(row, Is.Not.Null, label + " RectTransform");
        Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, row);
        Assert.That(
            bounds.min.x,
            Is.GreaterThanOrEqualTo(viewport.rect.xMin - PositionTolerance),
            label + " xMin");
        Assert.That(
            bounds.max.x,
            Is.LessThanOrEqualTo(viewport.rect.xMax + PositionTolerance),
            label + " xMax");
        Assert.That(
            bounds.min.y,
            Is.GreaterThanOrEqualTo(viewport.rect.yMin - PositionTolerance),
            label + " yMin");
        Assert.That(
            bounds.max.y,
            Is.LessThanOrEqualTo(viewport.rect.yMax + PositionTolerance),
            label + " yMax");
    }

    private static IList GetRows(ConfigPanelUI panel)
    {
        FieldInfo rowsField = typeof(ConfigPanelUI).GetField("_rows", InstancePrivate);
        Assert.That(rowsField, Is.Not.Null);
        var rows = rowsField.GetValue(panel) as IList;
        Assert.That(rows, Is.Not.Null);
        return rows;
    }

    private static List<GameObject> GetRowObjects(ConfigPanelUI panel)
    {
        IList rows = GetRows(panel);
        var result = new List<GameObject>(rows.Count);
        for (int i = 0; i < rows.Count; i++)
        {
            object row = rows[i];
            FieldInfo gameObjectField = row.GetType().GetField(
                "go",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(gameObjectField, Is.Not.Null);
            result.Add(gameObjectField.GetValue(row) as GameObject);
        }

        return result;
    }

    private static void InvokeInstance(ConfigPanelUI panel, string methodName)
    {
        MethodInfo method = typeof(ConfigPanelUI).GetMethod(methodName, InstancePrivate);
        Assert.That(method, Is.Not.Null, methodName + " 메서드를 찾을 수 없습니다.");
        method.Invoke(panel, null);
    }

    private static T GetPrivateField<T>(ConfigPanelUI panel, string fieldName)
        where T : class
    {
        FieldInfo field = typeof(ConfigPanelUI).GetField(fieldName, InstancePrivate);
        Assert.That(field, Is.Not.Null, fieldName + " 필드를 찾을 수 없습니다.");
        return field.GetValue(panel) as T;
    }

    private static void SetPrivateField(ConfigPanelUI panel, string fieldName, object value)
    {
        FieldInfo field = typeof(ConfigPanelUI).GetField(fieldName, InstancePrivate);
        Assert.That(field, Is.Not.Null, fieldName + " 필드를 찾을 수 없습니다.");
        field.SetValue(panel, value);
    }

    private static void SetEnumField(ConfigPanelUI panel, string fieldName, string enumName)
    {
        FieldInfo field = typeof(ConfigPanelUI).GetField(fieldName, InstancePrivate);
        Assert.That(field, Is.Not.Null, fieldName + " 필드를 찾을 수 없습니다.");
        field.SetValue(panel, Enum.Parse(field.FieldType, enumName));
    }

    private static Transform FindDescendant(Transform root, string objectName)
    {
        if (root == null)
            return null;

        Transform[] descendants = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < descendants.Length; i++)
        {
            Transform candidate = descendants[i];
            if (candidate != null && candidate.name == objectName)
                return candidate;
        }

        return null;
    }
}
