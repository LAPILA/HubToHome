using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public sealed class BattleUILayoutAssetTests
{
    private const string SharedHostPath =
        "Assets/_Game/Content/Battle/Prefabs/System/SeamlessBattleHost.prefab";

    [Test]
    public void TurnQueueMaskKeepsPortraitsInsideExpandedClipBounds()
    {
        GameObject host = LoadPrefab(SharedHostPath);
        Transform panel = FindDescendant(host.transform, "TurnQueuePanel");

        Assert.That(panel, Is.Not.Null, "Shared Battle Host에 TurnQueuePanel이 필요합니다.");

        RectMask2D mask = panel.GetComponent<RectMask2D>();
        Assert.That(mask, Is.Not.Null, "TurnQueuePanel에 RectMask2D가 필요합니다.");
        Assert.That(mask.padding, Is.EqualTo(new Vector4(-100f, -130f, 0f, -100f)));
        Assert.That(mask.softness, Is.EqualTo(new Vector2Int(10, 10)));
    }

    [TestCase("Assets/_Game/Content/Characters/Prefabs/Player/Player_Base.prefab")]
    [TestCase("Assets/_Game/Content/Characters/Prefabs/Enemy/Enemy_Base.prefab")]
    [TestCase("Assets/_Game/Content/Characters/Prefabs/Enemy/ZEV_Prefab.prefab")]
    [TestCase("Assets/_Game/Content/Characters/Prefabs/Enemy/tests_BunnySlime.prefab")]
    public void CharacterSpeechBubbleUses640SafeWorldScale(string prefabPath)
    {
        GameObject prefab = LoadPrefab(prefabPath);
        BattleSpeechBubble bubble = prefab.GetComponentInChildren<BattleSpeechBubble>(true);

        Assert.That(bubble, Is.Not.Null, prefabPath + "에 BattleSpeechBubble이 필요합니다.");
        Assert.That(bubble.transform.localScale, Is.EqualTo(Vector3.one * 0.005f));
    }

    [TestCase("Assets/_Game/Content/Dialogue/Prefabs/BattleSpeech/BattleSpeechBubble_Player.prefab")]
    [TestCase("Assets/_Game/Content/Dialogue/Prefabs/BattleSpeech/BattleSpeechBubble_Enemy.prefab")]
    public void SpeechBubblePrefabCapsBoxAt640SafeSize(string prefabPath)
    {
        GameObject prefab = LoadPrefab(prefabPath);
        BattleSpeechBubble bubble = prefab.GetComponent<BattleSpeechBubble>();

        Assert.That(bubble, Is.Not.Null, prefabPath + "에 BattleSpeechBubble이 필요합니다.");

        var serializedBubble = new SerializedObject(bubble);
        SerializedProperty maxSize = serializedBubble.FindProperty("_maxSize");
        Assert.That(maxSize, Is.Not.Null);
        Assert.That(maxSize.vector2Value, Is.EqualTo(new Vector2(480f, 240f)));
    }

    private static GameObject LoadPrefab(string path)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        Assert.That(prefab, Is.Not.Null, "Prefab을 찾을 수 없습니다: " + path);
        return prefab;
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
}
