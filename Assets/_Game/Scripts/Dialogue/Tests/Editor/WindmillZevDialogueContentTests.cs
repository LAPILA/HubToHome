using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class WindmillZevDialogueContentTests
{
    private const string WizzelSpeakerPath =
        "Assets/_Game/Content/Dialogue/Data/Speakers/Speaker_Wizzel.asset";
    private const string ZevSpeakerPath =
        "Assets/_Game/Content/Dialogue/Data/Speakers/Speaker_ZEV.asset";
    private const string PreDialoguePath =
        "Assets/_Game/Content/Maps/Regions/Chapter01/Data/Dialogue/Dialogue_Wizzel_ZEV_PreClash.asset";
    private const string PostDialoguePath =
        "Assets/_Game/Content/Maps/Regions/Chapter01/Data/Dialogue/Dialogue_Wizzel_ZEV_PostClash.asset";
    private const string ZevEnemyDataPath =
        "Assets/_Game/Content/Characters/EnemyDB/ZEV/Enemy_ZEV.asset";
    private const string WindmillExteriorPrefabPath =
        "Assets/_Game/Content/Maps/Regions/Chapter01/Prefabs/Rooms/Room_Chapter01_WindmillExterior.prefab";
    private const string PortraitRoot =
        "Assets/_Game/Content/Art/Characters/Player/Wizzel/대화얼굴/";

    [Test]
    public void EmotionType_AppendsConfusedWithoutRenumberingExistingValues()
    {
        Assert.That((int)EmotionType.None, Is.Zero);
        Assert.That((int)EmotionType.Normal, Is.EqualTo(1));
        Assert.That((int)EmotionType.Happy, Is.EqualTo(2));
        Assert.That((int)EmotionType.Sad, Is.EqualTo(3));
        Assert.That((int)EmotionType.Angry, Is.EqualTo(4));
        Assert.That((int)EmotionType.Shocked, Is.EqualTo(5));
        Assert.That((int)EmotionType.Confused, Is.EqualTo(6));
    }

    [TestCase("wizzel_normal.png")]
    [TestCase("wizzel_happy.png")]
    [TestCase("wizzel_confuse.png")]
    [TestCase("wizzel_angry.png")]
    public void WizzelPortrait_UsesPixelArtSpriteImportSettings(string fileName)
    {
        string path = PortraitRoot + fileName;
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;

        Assert.That(importer, Is.Not.Null, "TextureImporter를 찾을 수 없습니다: " + path);
        Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite));
        Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Single));
        Assert.That(importer.mipmapEnabled, Is.False);
        Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point));
        Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
        Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
        Assert.That(importer.alphaIsTransparency, Is.True);
        Assert.That(AssetDatabase.LoadAssetAtPath<Sprite>(path), Is.Not.Null);
    }

    [Test]
    public void SpeakerAssets_DeserializeAllAuthoredPortraits()
    {
        SpeakerData wizzel = LoadAsset<SpeakerData>(WizzelSpeakerPath);
        SpeakerData zev = LoadAsset<SpeakerData>(ZevSpeakerPath);
        EnemyData zevEnemy = LoadAsset<EnemyData>(ZevEnemyDataPath);

        Assert.That(wizzel.SpeakerID, Is.EqualTo("wizzel"));
        Assert.That(wizzel.Portraits, Has.Count.EqualTo(4));
        AssertPortraitPath(wizzel, EmotionType.Normal, "wizzel_normal.png");
        AssertPortraitPath(wizzel, EmotionType.Happy, "wizzel_happy.png");
        AssertPortraitPath(wizzel, EmotionType.Confused, "wizzel_confuse.png");
        AssertPortraitPath(wizzel, EmotionType.Angry, "wizzel_angry.png");

        Assert.That(zev.SpeakerID, Is.EqualTo("zev"));
        Assert.That(zev.Portraits, Has.Count.EqualTo(1));
        Assert.That(zev.GetPortrait(EmotionType.Normal), Is.SameAs(zevEnemy.Portrait));
    }

    [Test]
    public void DialogueAssets_ContainTheAuthoredSixPlusTwoNodes()
    {
        SpeakerData wizzel = LoadAsset<SpeakerData>(WizzelSpeakerPath);
        SpeakerData zev = LoadAsset<SpeakerData>(ZevSpeakerPath);
        DialogueData pre = LoadAsset<DialogueData>(PreDialoguePath);
        DialogueData post = LoadAsset<DialogueData>(PostDialoguePath);

        AssertDialogue(
            pre,
            new[] { wizzel, zev, wizzel, wizzel, zev, wizzel },
            new[]
            {
                EmotionType.Normal,
                EmotionType.Normal,
                EmotionType.Confused,
                EmotionType.Happy,
                EmotionType.Normal,
                EmotionType.Angry
            },
            new[]
            {
                "chapter01.windmill.zev_duel.pre.001",
                "chapter01.windmill.zev_duel.pre.002",
                "chapter01.windmill.zev_duel.pre.003",
                "chapter01.windmill.zev_duel.pre.004",
                "chapter01.windmill.zev_duel.pre.005",
                "chapter01.windmill.zev_duel.pre.006"
            },
            new[]
            {
                "굳이 싸워야 하는 건가요...?",
                "어쩔 수 없습니다만, 의뢰인의 요청입니다.",
                "굳이 의뢰라고 싸워야 할 필요는 없잖아요!",
                "그냥 적당히 못 찾은 척 넘어가면 되죠!",
                "저희 용병단의 운영 원칙에 어긋납니다. 죄송하지만...",
                "그럼 싸워야겠네요."
            });

        AssertDialogue(
            post,
            new[] { zev, wizzel },
            new[] { EmotionType.Normal, EmotionType.Normal },
            new[]
            {
                "chapter01.windmill.zev_duel.post.001",
                "chapter01.windmill.zev_duel.post.002"
            },
            new[] { "너무 약하시군요...", "으윽..." });
    }

    [Test]
    public void WindmillExterior_ZevUsesStagedMandatorySeamlessEncounter()
    {
        GameObject room = LoadAsset<GameObject>(WindmillExteriorPrefabPath);
        DialogueBattleNPC zev = room.GetComponentInChildren<DialogueBattleNPC>(true);
        SeamlessBattleHost host = room.GetComponentInChildren<SeamlessBattleHost>(true);
        DialogueData pre = LoadAsset<DialogueData>(PreDialoguePath);
        DialogueData post = LoadAsset<DialogueData>(PostDialoguePath);
        EnemyData enemy = LoadAsset<EnemyData>(ZevEnemyDataPath);

        Assert.That(zev, Is.Not.Null, "Windmill Exterior에 DialogueBattleNPC 기반 ZEV가 필요합니다.");
        Assert.That(host, Is.Not.Null, "Windmill Exterior에 심리스 전투 Host가 필요합니다.");

        var serialized = new SerializedObject(zev);
        Assert.That(serialized.FindProperty("_dialogue").objectReferenceValue, Is.SameAs(pre));
        Assert.That(serialized.FindProperty("_postClashDialogue").objectReferenceValue, Is.SameAs(post));
        Assert.That(serialized.FindProperty("_useStagedEncounter").boolValue, Is.True);
        Assert.That(serialized.FindProperty("_requireSeamlessBattleHost").boolValue, Is.True);
        Assert.That(serialized.FindProperty("_useDedicatedBattleScene").boolValue, Is.False);
        Assert.That(serialized.FindProperty("_defeatOnVictory").boolValue, Is.True);
        Assert.That(serialized.FindProperty("_allowEscape").boolValue, Is.False);
        Assert.That(
            serialized.FindProperty("_encounterIdOverride").stringValue,
            Is.EqualTo("chapter01.windmill.zev_duel"));

        SerializedProperty enemies = serialized.FindProperty("_fallbackEncounterEnemies");
        Assert.That(enemies.arraySize, Is.GreaterThan(0));
        Assert.That(enemies.GetArrayElementAtIndex(0).objectReferenceValue, Is.SameAs(enemy));
    }

    private static T LoadAsset<T>(string path) where T : Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        Assert.That(asset, Is.Not.Null, "자산을 찾을 수 없습니다: " + path);
        return asset;
    }

    private static void AssertPortraitPath(
        SpeakerData speaker,
        EmotionType emotion,
        string expectedFileName)
    {
        Sprite portrait = speaker.GetPortrait(emotion);
        Assert.That(portrait, Is.Not.Null, emotion + " 초상화가 필요합니다.");
        Assert.That(AssetDatabase.GetAssetPath(portrait), Is.EqualTo(PortraitRoot + expectedFileName));
    }

    private static void AssertDialogue(
        DialogueData dialogue,
        SpeakerData[] speakers,
        EmotionType[] emotions,
        string[] localizationKeys,
        string[] defaultTexts)
    {
        Assert.That(dialogue.Style, Is.EqualTo(DialogueStyle.Overworld));
        Assert.That(dialogue.Nodes, Has.Count.EqualTo(speakers.Length));
        Assert.That(emotions, Has.Length.EqualTo(speakers.Length));
        Assert.That(localizationKeys, Has.Length.EqualTo(speakers.Length));
        Assert.That(defaultTexts, Has.Length.EqualTo(speakers.Length));

        for (int i = 0; i < speakers.Length; i++)
        {
            DialogueNode node = dialogue.Nodes[i];
            Assert.That(node.Speaker, Is.SameAs(speakers[i]), "Speaker mismatch at node " + i);
            Assert.That(node.Emotion, Is.EqualTo(emotions[i]), "Emotion mismatch at node " + i);
            Assert.That(node.LocalizationKey, Is.EqualTo(localizationKeys[i]), "Key mismatch at node " + i);
            Assert.That(node.DefaultText, Is.EqualTo(defaultTexts[i]), "Text mismatch at node " + i);
            Assert.That(node.EventTriggerID, Is.Empty, "Unexpected event at node " + i);
            Assert.That(node.IsChoiceNode, Is.False, "Unexpected choice at node " + i);
            Assert.That(node.Choices, Is.Empty, "Unexpected choices at node " + i);
        }
    }
}
