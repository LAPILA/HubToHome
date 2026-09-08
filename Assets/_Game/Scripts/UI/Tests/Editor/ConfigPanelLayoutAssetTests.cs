using TMPro;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public sealed class ConfigPanelLayoutAssetTests
{
    private const string UiManagerPath =
        "Assets/_Game/Core/Prefabs/CoreSettings/UIManager.prefab";
    private const string RowPrefabPath =
        "Assets/_Game/Presentation/UI/Prefabs/Settings/DetailSettingsPanel.prefab";
    private const float PositionTolerance = 0.01f;

    [Test]
    public void SettingsLocalizationHasAllUsedKeysInAllFourLanguages()
    {
        TextAsset csv = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Resources/LocalizationTable.csv");
        MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(
            "Assets/_Game/Scripts/UI/Runtime/ConfigPanelUI.cs");
        Assert.That(csv, Is.Not.Null);
        Assert.That(script, Is.Not.Null);
        var rows = new Dictionary<string, string[]>(StringComparer.Ordinal);
        string[] lines = csv.text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 1; i < lines.Length; i++)
        {
            // 설정 번역은 기존 로더와 같은 한 줄 CSV 형식을 사용합니다.
            string[] columns = Regex.Split(lines[i], @",(?=(?:[^""]*""[^""]*"")*[^""]*$)");
            if (!columns[0].StartsWith("config.", StringComparison.Ordinal)
                && !columns[0].StartsWith("common.", StringComparison.Ordinal)) continue;
            Assert.That(columns, Has.Length.EqualTo(5), columns[0]);
            Assert.That(rows.ContainsKey(columns[0]), Is.False, "Duplicate key: " + columns[0]);
            rows.Add(columns[0], columns);
            for (int language = 1; language < 5; language++)
                Assert.That(columns[language].Trim(' ', '"'), Is.Not.Empty, columns[0] + " column " + language);
        }
        foreach (Match match in Regex.Matches(script.text, "\"((?:config|common)\\.[a-z_.]+)\""))
            Assert.That(rows.ContainsKey(match.Groups[1].Value), Is.True, "Missing key: " + match.Groups[1].Value);
        foreach (string key in new[] { "config.preview.shake", "config.preview.flash" })
        {
            for (int language = 1; language < 5; language++)
                Assert.That(rows[key][language], Does.Contain("{0}"), key);
        }
    }

    [Test]
    public void SharedEventSystemOnlyAcceptsNavigationSubmitAndCancel()
    {
        GameObject prefab = LoadPrefab(UiManagerPath);
        InputSystemUIInputModule module = prefab.GetComponentInChildren<InputSystemUIInputModule>(true);

        Assert.That(module, Is.Not.Null);
        Assert.That(module.enabled, Is.True);
        Assert.That(module.GetComponent<EventSystem>().sendNavigationEvents, Is.True);
        // 명시적 액션 자산을 유지해야 OnEnable이 기본 포인터 액션을 다시 할당하지 않습니다.
        Assert.That(module.actionsAsset, Is.Not.Null);
        Assert.That(module.move?.action, Is.Not.Null);
        Assert.That(module.submit?.action, Is.Not.Null);
        Assert.That(module.cancel?.action, Is.Not.Null);
        Assert.That(module.point, Is.Null);
        Assert.That(module.leftClick, Is.Null);
        Assert.That(module.rightClick, Is.Null);
        Assert.That(module.middleClick, Is.Null);
        Assert.That(module.scrollWheel, Is.Null);
        Assert.That(module.trackedDevicePosition, Is.Null);
        Assert.That(module.trackedDeviceOrientation, Is.Null);
        Assert.That(module.deselectOnBackgroundClick, Is.False);
    }

    [Test]
    public void SettingPanelUsesThe640By480CanvasContract()
    {
        GameObject prefab = LoadPrefab(UiManagerPath);
        RectTransform settingPanel = RequireRect(prefab.transform, "SettingPanel");
        CanvasScaler scaler = settingPanel.GetComponent<CanvasScaler>();

        Assert.That(scaler, Is.Not.Null, "SettingPanel에 CanvasScaler가 필요합니다.");
        AssertVector3(settingPanel.localScale, Vector3.one, "SettingPanel localScale");
        Assert.That(
            scaler.uiScaleMode,
            Is.EqualTo(CanvasScaler.ScaleMode.ScaleWithScreenSize));
        AssertVector2(
            scaler.referenceResolution,
            new Vector2(640f, 480f),
            "SettingPanel referenceResolution");
        Assert.That(
            scaler.screenMatchMode,
            Is.EqualTo(CanvasScaler.ScreenMatchMode.MatchWidthOrHeight));
    }

    [Test]
    public void SettingPanelOwnsModalSortingAndABackgroundRaycastBlocker()
    {
        GameObject prefab = LoadPrefab(UiManagerPath);
        RectTransform settingPanel = RequireRect(prefab.transform, "SettingPanel");
        Canvas canvas = settingPanel.GetComponent<Canvas>();
        RectTransform scrim = RequireRect(settingPanel, "ModalScrim");
        Image image = scrim.GetComponent<Image>();

        Assert.That(canvas.sortingOrder, Is.EqualTo(100));
        Assert.That(canvas.overrideSorting, Is.True);
        Assert.That(scrim.parent, Is.SameAs(settingPanel));
        Assert.That(scrim.GetSiblingIndex(), Is.EqualTo(0));
        AssertVector2(scrim.anchorMin, Vector2.zero, "ModalScrim anchorMin");
        AssertVector2(scrim.anchorMax, Vector2.one, "ModalScrim anchorMax");
        AssertVector2(scrim.sizeDelta, Vector2.zero, "ModalScrim sizeDelta");
        AssertVector2(scrim.anchoredPosition, Vector2.zero, "ModalScrim position");
        Assert.That(image, Is.Not.Null);
        Assert.That(image.raycastTarget, Is.True);
        Assert.That(image.color.a, Is.InRange(0.5f, 0.9f));
    }

    [Test]
    public void ConfigRegionsUseTheApprovedTwoColumnLayoutInsideBackground()
    {
        GameObject prefab = LoadPrefab(UiManagerPath);
        Transform settingPanel = RequireTransform(prefab.transform, "SettingPanel");
        RectTransform background = RequireRect(settingPanel, "BackGround");
        RectTransform title = RequireRect(background, "SettingsTitle");
        RectTransform categories = RequireRect(background, "SettingsCategories");
        RectTransform detail = RequireRect(background, "SettingsDetailViews");
        RectTransform preview = RequireRect(background, "ExTEXT");

        AssertCenteredRect(
            background,
            Vector2.zero,
            new Vector2(536.7313f, 389.67944f));
        AssertCenteredRect(title, new Vector2(0f, 140f), new Vector2(496f, 40f));
        AssertCenteredRect(categories, new Vector2(-178f, -5f), new Vector2(140f, 250f));
        AssertCenteredRect(detail, new Vector2(78f, -5f), new Vector2(340f, 250f));
        AssertCenteredRect(preview, new Vector2(78f, -160f), new Vector2(324f, 40f));

        Rect backgroundRect = LocalRect(background);
        Rect categoryRect = LocalRect(categories);
        Rect detailRect = LocalRect(detail);
        Rect titleRect = LocalRect(title);
        Rect previewRect = LocalRect(preview);

        AssertRectContains(backgroundRect, titleRect, "SettingsTitle");
        AssertRectContains(backgroundRect, categoryRect, "SettingsCategories");
        AssertRectContains(backgroundRect, detailRect, "SettingsDetailViews");
        AssertRectContains(backgroundRect, previewRect, "ExTEXT");
        Assert.That(
            categoryRect.Overlaps(detailRect),
            Is.False,
            "카테고리 열과 상세 열은 서로 겹치면 안 됩니다.");

        RectMask2D backgroundMask = background.GetComponent<RectMask2D>();
        Assert.That(backgroundMask, Is.Not.Null, "BackGround에 RectMask2D가 필요합니다.");
        Assert.That(backgroundMask.padding, Is.EqualTo(Vector4.zero));
        Assert.That(backgroundMask.softness, Is.EqualTo(Vector2Int.zero));
    }

    [Test]
    public void CategoriesOwnAStableFourItemVerticalLayout()
    {
        GameObject prefab = LoadPrefab(UiManagerPath);
        RectTransform categories = RequireRect(prefab.transform, "SettingsCategories");
        VerticalLayoutGroup layout = categories.GetComponent<VerticalLayoutGroup>();

        Assert.That(layout, Is.Not.Null, "SettingsCategories에 VerticalLayoutGroup이 필요합니다.");
        AssertPadding(layout.padding, 8, 8, 8, 8, "SettingsCategories padding");
        Assert.That(layout.spacing, Is.EqualTo(8f).Within(PositionTolerance));
        Assert.That(layout.childAlignment, Is.EqualTo(TextAnchor.UpperLeft));
        Assert.That(layout.childControlWidth, Is.True);
        Assert.That(layout.childControlHeight, Is.True);
        Assert.That(layout.childForceExpandWidth, Is.True);
        Assert.That(layout.childForceExpandHeight, Is.False);
        Assert.That(categories.childCount, Is.EqualTo(4));

        for (int i = 0; i < categories.childCount; i++)
        {
            Transform item = categories.GetChild(i);
            LayoutElement element = item.GetComponent<LayoutElement>();
            TextMeshProUGUI text = item.GetComponent<TextMeshProUGUI>();

            Assert.That(element, Is.Not.Null, item.name + "에 LayoutElement가 필요합니다.");
            Assert.That(
                element.preferredHeight,
                Is.EqualTo(44f).Within(PositionTolerance),
                item.name + " preferredHeight");
            Assert.That(text, Is.Not.Null, item.name + "에 TextMeshProUGUI가 필요합니다.");
            Assert.That(text.enableAutoSizing, Is.True, item.name + " auto size");
            Assert.That(text.fontSizeMin, Is.EqualTo(16f).Within(PositionTolerance));
            Assert.That(text.fontSizeMax, Is.EqualTo(22f).Within(PositionTolerance));
            Assert.That(text.textWrappingMode, Is.EqualTo(TextWrappingModes.NoWrap));
            Assert.That(text.margin, Is.EqualTo(Vector4.zero), item.name + " margin");
        }
    }

    [Test]
    public void DetailScrollRectUsesAnExplicitMaskedViewportAndContent()
    {
        GameObject prefab = LoadPrefab(UiManagerPath);
        Transform settingPanel = RequireTransform(prefab.transform, "SettingPanel");
        RectTransform detail = RequireRect(settingPanel, "SettingsDetailViews");
        ScrollRect scroll = detail.GetComponent<ScrollRect>();

        Assert.That(scroll, Is.Not.Null, "SettingsDetailViews에 ScrollRect가 필요합니다.");
        Assert.That(scroll.viewport, Is.SameAs(detail));
        Assert.That(scroll.content, Is.Not.Null, "ScrollRect.content가 명시적으로 연결돼야 합니다.");
        Assert.That(scroll.content.name, Is.EqualTo("Content"));
        Assert.That(scroll.content.parent, Is.SameAs(detail));
        Assert.That(scroll.content.IsChildOf(detail), Is.True);
        RectMask2D viewportMask = detail.GetComponent<RectMask2D>();
        Assert.That(viewportMask, Is.Not.Null);
        Assert.That(viewportMask.padding, Is.EqualTo(Vector4.zero));
        Assert.That(viewportMask.softness, Is.EqualTo(Vector2Int.zero));
        Assert.That(detail.GetComponent<VerticalLayoutGroup>(), Is.Null);

        RectTransform content = scroll.content;
        AssertVector2(content.anchorMin, new Vector2(0f, 1f), "Content anchorMin");
        AssertVector2(content.anchorMax, new Vector2(1f, 1f), "Content anchorMax");
        AssertVector2(content.pivot, new Vector2(0.5f, 1f), "Content pivot");
        AssertVector2(content.anchoredPosition, Vector2.zero, "Content anchoredPosition");
        AssertVector2(content.sizeDelta, Vector2.zero, "Content sizeDelta");

        VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
        ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
        Assert.That(layout, Is.Not.Null, "Content에 VerticalLayoutGroup이 필요합니다.");
        Assert.That(fitter, Is.Not.Null, "Content에 ContentSizeFitter가 필요합니다.");
        AssertPadding(layout.padding, 4, 4, 4, 4, "Content padding");
        Assert.That(layout.spacing, Is.EqualTo(4f).Within(PositionTolerance));
        Assert.That(layout.childAlignment, Is.EqualTo(TextAnchor.UpperLeft));
        Assert.That(layout.childControlWidth, Is.True);
        Assert.That(layout.childControlHeight, Is.True);
        Assert.That(layout.childForceExpandWidth, Is.True);
        Assert.That(layout.childForceExpandHeight, Is.False);
        Assert.That(fitter.horizontalFit, Is.EqualTo(ContentSizeFitter.FitMode.Unconstrained));
        Assert.That(fitter.verticalFit, Is.EqualTo(ContentSizeFitter.FitMode.PreferredSize));

        ConfigPanelUI panel = settingPanel.GetComponent<ConfigPanelUI>();
        Assert.That(panel, Is.Not.Null, "SettingPanel에 ConfigPanelUI가 필요합니다.");
        var serializedPanel = new SerializedObject(panel);
        SerializedProperty detailRoot = serializedPanel.FindProperty("_detailRoot");
        Assert.That(detailRoot, Is.Not.Null);
        Assert.That(detailRoot.objectReferenceValue, Is.SameAs(content));
    }

    [Test]
    public void RowPrefabFitsTheDetailColumnWithoutNameValueOverlap()
    {
        GameObject rowPrefab = LoadPrefab(RowPrefabPath);
        RectTransform rowRect = rowPrefab.transform as RectTransform;
        HorizontalLayoutGroup layout = rowPrefab.GetComponent<HorizontalLayoutGroup>();
        LayoutElement rowElement = rowPrefab.GetComponent<LayoutElement>();
        TextMeshProUGUI[] columns = rowPrefab.GetComponentsInChildren<TextMeshProUGUI>(true);

        Assert.That(rowRect, Is.Not.Null);
        AssertVector2(rowRect.sizeDelta, new Vector2(340f, 44f), "Row sizeDelta");
        Assert.That(layout, Is.Not.Null, "Row 루트에 HorizontalLayoutGroup이 필요합니다.");
        Assert.That(rowElement, Is.Not.Null, "Row 루트에 LayoutElement가 필요합니다.");
        Image rowBackground = rowPrefab.GetComponent<Image>();
        Assert.That(rowBackground, Is.Not.Null, "선택 표시는 Row 배경 Image를 사용합니다.");
        Assert.That(rowBackground.raycastTarget, Is.False);
        AssertPadding(layout.padding, 8, 8, 4, 4, "Row padding");
        Assert.That(layout.spacing, Is.EqualTo(12f).Within(PositionTolerance));
        Assert.That(layout.childControlWidth, Is.True);
        Assert.That(layout.childControlHeight, Is.True);
        Assert.That(layout.childForceExpandWidth, Is.False);
        Assert.That(layout.childForceExpandHeight, Is.False);
        Assert.That(rowElement.preferredHeight, Is.EqualTo(44f).Within(PositionTolerance));
        Assert.That(columns, Has.Length.EqualTo(2));

        LayoutElement nameColumn = columns[0].GetComponent<LayoutElement>();
        LayoutElement valueColumn = columns[1].GetComponent<LayoutElement>();
        Assert.That(nameColumn, Is.Not.Null, "이름 열에 LayoutElement가 필요합니다.");
        Assert.That(valueColumn, Is.Not.Null, "값 열에 LayoutElement가 필요합니다.");
        Assert.That(nameColumn.preferredWidth, Is.EqualTo(208f).Within(PositionTolerance));
        Assert.That(nameColumn.flexibleWidth, Is.EqualTo(1f).Within(PositionTolerance));
        Assert.That(valueColumn.preferredWidth, Is.EqualTo(96f).Within(PositionTolerance));
        Assert.That(valueColumn.flexibleWidth, Is.EqualTo(0f).Within(PositionTolerance));

        float requiredRowWidth = layout.padding.horizontal
            + layout.spacing
            + nameColumn.preferredWidth
            + valueColumn.preferredWidth;
        const float detailWidthAfterContentPadding = 340f - 8f;
        Assert.That(
            requiredRowWidth,
            Is.EqualTo(detailWidthAfterContentPadding).Within(PositionTolerance),
            "두 열의 요구 폭은 Content 좌우 padding을 뺀 상세 열 폭과 일치해야 합니다.");
        Assert.That(rowRect.sizeDelta.x, Is.LessThanOrEqualTo(340f + PositionTolerance));

        AssertTextContract(columns[0], 14f, 20f, "이름 열");
        AssertTextContract(columns[1], 14f, 18f, "값 열");
        Assert.That(columns[1].horizontalAlignment, Is.EqualTo(HorizontalAlignmentOptions.Right));
    }

    [Test]
    public void TitleAndPreviewTextStayInsideTheirVisualEnvelope()
    {
        GameObject prefab = LoadPrefab(UiManagerPath);
        RectTransform background = RequireRect(prefab.transform, "BackGround");
        RectTransform titleRect = RequireRect(background, "SettingsTitle");
        RectTransform detailRect = RequireRect(background, "SettingsDetailViews");
        RectTransform previewRect = RequireRect(background, "ExTEXT");
        TextMeshProUGUI title = titleRect.GetComponent<TextMeshProUGUI>();
        TextMeshProUGUI preview = previewRect.GetComponent<TextMeshProUGUI>();

        Assert.That(title, Is.Not.Null);
        Assert.That(title.fontSize, Is.EqualTo(28f).Within(PositionTolerance));
        Assert.That(title.textWrappingMode, Is.EqualTo(TextWrappingModes.NoWrap));

        Assert.That(preview, Is.Not.Null);
        Assert.That(preview.enableAutoSizing, Is.True);
        Assert.That(preview.fontSizeMin, Is.EqualTo(14f).Within(PositionTolerance));
        Assert.That(preview.fontSizeMax, Is.EqualTo(16f).Within(PositionTolerance));
        Assert.That(preview.margin, Is.EqualTo(Vector4.zero));

        Rect previewBounds = LocalRect(previewRect);
        var shakeEnvelope = new Rect(
            previewBounds.xMin - 8f,
            previewBounds.yMin - 8f,
            previewBounds.width + 16f,
            previewBounds.height + 16f);
        AssertRectContains(LocalRect(background), shakeEnvelope, "ExTEXT 최대 shake envelope");
        Assert.That(
            shakeEnvelope.Overlaps(LocalRect(detailRect)),
            Is.False,
            "ExTEXT 최대 shake envelope는 상세 Viewport와 겹치면 안 됩니다.");
    }

    private static GameObject LoadPrefab(string path)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        Assert.That(prefab, Is.Not.Null, "Prefab을 찾을 수 없습니다: " + path);
        return prefab;
    }

    private static Transform RequireTransform(Transform root, string objectName)
    {
        Transform result = FindDescendant(root, objectName);
        Assert.That(result, Is.Not.Null, objectName + " 오브젝트를 찾을 수 없습니다.");
        return result;
    }

    private static RectTransform RequireRect(Transform root, string objectName)
    {
        Transform result = RequireTransform(root, objectName);
        RectTransform rect = result as RectTransform;
        Assert.That(rect, Is.Not.Null, objectName + "에 RectTransform이 필요합니다.");
        return rect;
    }

    private static Transform FindDescendant(Transform root, string objectName)
    {
        Transform[] descendants = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < descendants.Length; i++)
        {
            Transform candidate = descendants[i];
            if (candidate != null && candidate.name == objectName)
                return candidate;
        }

        return null;
    }

    private static void AssertCenteredRect(
        RectTransform rect,
        Vector2 expectedPosition,
        Vector2 expectedSize)
    {
        AssertVector2(rect.anchorMin, new Vector2(0.5f, 0.5f), rect.name + " anchorMin");
        AssertVector2(rect.anchorMax, new Vector2(0.5f, 0.5f), rect.name + " anchorMax");
        AssertVector2(rect.pivot, new Vector2(0.5f, 0.5f), rect.name + " pivot");
        AssertVector2(rect.anchoredPosition, expectedPosition, rect.name + " anchoredPosition");
        AssertVector2(rect.sizeDelta, expectedSize, rect.name + " sizeDelta");
    }

    private static Rect LocalRect(RectTransform rect)
    {
        Vector2 min = rect.anchoredPosition - Vector2.Scale(rect.sizeDelta, rect.pivot);
        return new Rect(min, rect.sizeDelta);
    }

    private static void AssertRectContains(Rect outer, Rect inner, string label)
    {
        Assert.That(inner.xMin, Is.GreaterThanOrEqualTo(outer.xMin - PositionTolerance), label + " xMin");
        Assert.That(inner.xMax, Is.LessThanOrEqualTo(outer.xMax + PositionTolerance), label + " xMax");
        Assert.That(inner.yMin, Is.GreaterThanOrEqualTo(outer.yMin - PositionTolerance), label + " yMin");
        Assert.That(inner.yMax, Is.LessThanOrEqualTo(outer.yMax + PositionTolerance), label + " yMax");
    }

    private static void AssertPadding(
        RectOffset actual,
        int left,
        int right,
        int top,
        int bottom,
        string label)
    {
        Assert.That(actual.left, Is.EqualTo(left), label + " left");
        Assert.That(actual.right, Is.EqualTo(right), label + " right");
        Assert.That(actual.top, Is.EqualTo(top), label + " top");
        Assert.That(actual.bottom, Is.EqualTo(bottom), label + " bottom");
    }

    private static void AssertTextContract(
        TextMeshProUGUI text,
        float expectedMin,
        float expectedMax,
        string label)
    {
        Assert.That(text.enableAutoSizing, Is.True, label + " auto size");
        Assert.That(text.fontSizeMin, Is.EqualTo(expectedMin).Within(PositionTolerance), label + " fontSizeMin");
        Assert.That(text.fontSizeMax, Is.EqualTo(expectedMax).Within(PositionTolerance), label + " fontSizeMax");
        Assert.That(text.textWrappingMode, Is.EqualTo(TextWrappingModes.NoWrap), label + " wrapping");
        Assert.That(text.overflowMode, Is.EqualTo(TextOverflowModes.Ellipsis), label + " overflow");
        Assert.That(text.margin, Is.EqualTo(Vector4.zero), label + " margin");
    }

    private static void AssertVector2(Vector2 actual, Vector2 expected, string label)
    {
        Assert.That(actual.x, Is.EqualTo(expected.x).Within(PositionTolerance), label + " x");
        Assert.That(actual.y, Is.EqualTo(expected.y).Within(PositionTolerance), label + " y");
    }

    private static void AssertVector3(Vector3 actual, Vector3 expected, string label)
    {
        Assert.That(actual.x, Is.EqualTo(expected.x).Within(PositionTolerance), label + " x");
        Assert.That(actual.y, Is.EqualTo(expected.y).Within(PositionTolerance), label + " y");
        Assert.That(actual.z, Is.EqualTo(expected.z).Within(PositionTolerance), label + " z");
    }
}
