using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;

public sealed class SceneContractTests
{
    [TestCase(SceneName.Title)]
    [TestCase(SceneName.Overworld)]
    [TestCase(SceneName.Battle)]
    public void RuntimeSceneConstantResolvesToEnabledBuildScene(string sceneName)
    {
        string expectedFile = sceneName + ".unity";
        bool exists = EditorBuildSettings.scenes.Any(scene =>
            scene.enabled
            && string.Equals(
                Path.GetFileName(scene.path),
                expectedFile,
                StringComparison.Ordinal));

        Assert.That(exists, Is.True, sceneName + " must match an enabled Build Settings scene file.");
    }

    [Test]
    public void TitleShortcutScenePathExists()
    {
        const string titlePath = "Assets/_Game/Content/Maps/Regions-TEST/Title/00_TitleScene.unity";
        Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(titlePath), Is.Not.Null);
    }
}