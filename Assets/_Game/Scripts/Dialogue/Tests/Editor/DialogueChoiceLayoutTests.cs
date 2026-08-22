using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;

public sealed class DialogueChoiceLayoutTests
{
    private const string DialogueCanvasPath =
        "Assets/_Game/Content/Dialogue/Prefabs/DialogueCanvas.prefab";
    private const string DialogueManagerPath =
        "Assets/_Game/Core/Prefabs/CoreSettings/DialogueManager.prefab";
    private const float PositionTolerance = 0.01f;

    [Test]
    public void DialogueCanvasKeepsBothChoiceRootsAtZero()
    {
        AssertPrefabChoiceLayout(DialogueCanvasPath);
    }

    [Test]
    public void DialogueManagerNestedCanvasKeepsBothChoiceRootsAtZero()
    {
        AssertPrefabChoiceLayout(DialogueManagerPath);
    }

    [Test]
    public void ShowChoicesReappliesTheZeroPositionAtRuntime()
    {
        GameObject panelObject = new GameObject(
            "DialogueChoiceLayoutTests_Panel",
            typeof(RectTransform));

        try
        {
            DialogueUI panel = panelObject.AddComponent<DialogueUI>();
            GameObject choiceRootObject = new GameObject(
                "ChoiceRoot",
                typeof(RectTransform));
            choiceRootObject.transform.SetParent(panelObject.transform, false);
            RectTransform choiceRoot = choiceRootObject.GetComponent<RectTransform>();
            choiceRoot.anchoredPosition = new Vector2(24f, 120f);

            GameObject templateObject = new GameObject(
                "ChoiceTemplate",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            templateObject.transform.SetParent(choiceRoot, false);
            TextMeshProUGUI choiceTemplate = templateObject.GetComponent<TextMeshProUGUI>();

            var serializedPanel = new SerializedObject(panel);
            serializedPanel.FindProperty("_choiceRoot").objectReferenceValue = choiceRoot;
            serializedPanel.FindProperty("_choiceTemplate").objectReferenceValue = choiceTemplate;
            serializedPanel.ApplyModifiedPropertiesWithoutUndo();

            panel.ShowChoices(
                new List<ChoiceData>
                {
                    new ChoiceData { ChoiceText = "테스트 선택지" }
                },
                _ => { });

            AssertVector2(choiceRoot.anchorMin, new Vector2(0.5f, 0f), "anchorMin");
            AssertVector2(choiceRoot.anchorMax, new Vector2(0.5f, 0f), "anchorMax");
            AssertVector2(choiceRoot.pivot, new Vector2(0.5f, 0f), "pivot");
            AssertVector2(choiceRoot.anchoredPosition, Vector2.zero, "anchoredPosition");
        }
        finally
        {
            Object.DestroyImmediate(panelObject);
        }
    }

    private static void AssertPrefabChoiceLayout(string prefabPath)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        Assert.That(prefab, Is.Not.Null, "Prefab을 찾을 수 없습니다: " + prefabPath);

        DialogueUI[] panels = prefab.GetComponentsInChildren<DialogueUI>(true);
        Assert.That(
            panels,
            Has.Length.EqualTo(2),
            prefabPath + "에는 Overworld/Cinematic DialogueUI가 하나씩 있어야 합니다.");

        for (int i = 0; i < panels.Length; i++)
        {
            DialogueUI panel = panels[i];
            var serializedPanel = new SerializedObject(panel);
            SerializedProperty configuredPosition =
                serializedPanel.FindProperty("_choiceAnchoredPosition");
            SerializedProperty choiceRootProperty = serializedPanel.FindProperty("_choiceRoot");

            Assert.That(configuredPosition, Is.Not.Null, panel.name + " 설정 위치");
            Assert.That(choiceRootProperty, Is.Not.Null, panel.name + " ChoiceRoot 참조");
            AssertVector2(
                configuredPosition.vector2Value,
                Vector2.zero,
                panel.name + " configuredPosition");

            RectTransform choiceRoot = choiceRootProperty.objectReferenceValue as RectTransform;
            Assert.That(choiceRoot, Is.Not.Null, panel.name + " ChoiceRoot가 연결돼야 합니다.");
            AssertVector2(
                choiceRoot.anchoredPosition,
                Vector2.zero,
                panel.name + " ChoiceRoot anchoredPosition");
        }
    }

    private static void AssertVector2(Vector2 actual, Vector2 expected, string label)
    {
        Assert.That(actual.x, Is.EqualTo(expected.x).Within(PositionTolerance), label + " x");
        Assert.That(actual.y, Is.EqualTo(expected.y).Within(PositionTolerance), label + " y");
    }
}
