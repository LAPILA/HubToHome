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

    [TestCase(DevelopmentContentPaths.TestMapScene)]
    [TestCase(DevelopmentContentPaths.MapFieldStarterScene)]
    [TestCase(DevelopmentContentPaths.TitleScene)]
    [TestCase(DevelopmentContentPaths.IntroScene)]
    [TestCase(DevelopmentContentPaths.PrologueSubwayScene)]
    [TestCase(DevelopmentContentPaths.ShowcaseStationScene)]
    [TestCase(DevelopmentContentPaths.TravelTrainScene)]
    [TestCase(DevelopmentContentPaths.WideFieldScene)]
    public void DevelopmentScenePathExists(string scenePath)
    {
        Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath), Is.Not.Null, scenePath);
    }

    [Test]
    public void BuildSettingsDoesNotContainDeprecatedRegionsTestPath()
    {
        bool containsDeprecatedPath = EditorBuildSettings.scenes.Any(scene =>
            scene.path.Contains("/Regions-TEST/", StringComparison.Ordinal));

        Assert.That(containsDeprecatedPath, Is.False);
    }
}
