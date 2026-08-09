using NUnit.Framework;
using UnityEditor;
using UnityEngine.UIElements;

public sealed class DialogueUIAssetValidationTests
{
    private const string UxmlPath = "Assets/_Game/Presentation/UI/Dialogue/DialogueUI.uxml";
    private const string UssPath = "Assets/_Game/Presentation/UI/Dialogue/DialogueUI.uss";
    private const string TokenPath = "Assets/_Game/Presentation/UI/Dialogue/DialogueUI_Tokens.uss";

    [Test]
    public void DialogueUxmlAssetExists()
    {
        Assert.That(AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath), Is.Not.Null);
    }

    [Test]
    public void DialogueStylesheetAssetsExist()
    {
        Assert.That(AssetDatabase.LoadAssetAtPath<StyleSheet>(UssPath), Is.Not.Null);
        Assert.That(AssetDatabase.LoadAssetAtPath<StyleSheet>(TokenPath), Is.Not.Null);
    }
}
