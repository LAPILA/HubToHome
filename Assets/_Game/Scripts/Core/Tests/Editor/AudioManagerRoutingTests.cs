using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class AudioManagerRoutingTests
{
    private const string AudioManagerPrefabPath =
        "Assets/_Game/Core/Prefabs/CoreSettings/AudioManager.prefab";

    [TestCase("BGM_A", "BGM")]
    [TestCase("BGM_B", "BGM")]
    [TestCase("SFX", "SFX")]
    [TestCase("UI", "UI")]
    [TestCase("Voice", "Voice")]
    [TestCase("Ambience", "Ambience")]
    public void PrefabSourceUsesExpectedMixerGroup(
        string sourceObjectName,
        string expectedGroupName)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            AudioManagerPrefabPath);
        Assert.That(prefab, Is.Not.Null);

        var sourcesByName = new Dictionary<string, AudioSource>(
            System.StringComparer.Ordinal);
        AudioSource[] sources = prefab.GetComponentsInChildren<AudioSource>(true);
        for (int i = 0; i < sources.Length; i++)
            sourcesByName[sources[i].gameObject.name] = sources[i];

        Assert.That(
            sourcesByName.TryGetValue(sourceObjectName, out AudioSource source),
            Is.True,
            $"AudioManager prefab is missing source '{sourceObjectName}'.");
        Assert.That(source.outputAudioMixerGroup, Is.Not.Null);
        Assert.That(source.outputAudioMixerGroup.name, Is.EqualTo(expectedGroupName));
    }
}
