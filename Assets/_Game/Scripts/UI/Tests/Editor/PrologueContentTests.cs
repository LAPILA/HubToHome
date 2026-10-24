using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>프롤로그 콘텐츠 연결만 검사합니다. 씬 열기, 에셋 생성, Play Mode는 수행하지 않습니다.</summary>
public sealed class PrologueContentTests
{
    private const string ContentRoot = "Assets/_Game/Content/Maps/Regions/ALL";
    private const string StoryRoot = ContentRoot + "/Data/Prologue";
    private const string IntroScene = "Assets/_Game/Content/Maps/Development/Regions/Title/01_IntroScene.unity";
    private const string TitleScene = "Assets/_Game/Content/Maps/Development/Regions/Title/00_TitleScene.unity";
    private const string TrainScene = ContentRoot + "/Scenes/Region_Train.unity";

    [Test]
    public void OpeningStartsWithOnlyLoopingWindowAnimationAndDialogue()
    {
        IllustratedStoryAsset story = LoadStory("Prologue_Opening");
        Assert.That(story.TryValidate(out string error), Is.True, error);
        Assert.That(story.Pages.Count, Is.EqualTo(1));
        IllustratedStoryPage window = story.Pages[0];
        Assert.That(window.IsCaptionPage, Is.False);
        Assert.That(window.Picture, Is.Null);
        AssertReference(window.Animation, "1f8ddfd131bedaf499bbd19b9cd21567", -1394380028033222937L);
        Assert.That(window.CanvasSize, Is.EqualTo(new Vector2Int(128, 96)));
        Assert.That(window.PixelScale, Is.EqualTo(3));
        Assert.That(window.LoopAnimation, Is.True);
        Assert.That(window.Dialogue, Is.Not.Null);
        Assert.That(window.Dialogue.Nodes.Count, Is.GreaterThanOrEqualTo(1));
    }

    [Test]
    public void RearRevealPlaysMovementOnceThenWaitsOnMonsterPicture()
    {
        IllustratedStoryAsset story = LoadStory("Prologue_RearReveal");
        Assert.That(story.TryValidate(out string error), Is.True, error);
        Assert.That(story.Pages.Count, Is.EqualTo(2));

        IllustratedStoryPage movement = story.Pages[0];
        AssertReference(movement.Animation, "d6ca5208eda2dee4384700d3770301c7", -1394380028033222937L);
        Assert.That(movement.LoopAnimation, Is.False);
        Assert.That(movement.Dialogue, Is.Null);
        Assert.That(movement.WaitForConfirmWithoutDialogue, Is.False);
        Assert.That(movement.MinimumDuration, Is.GreaterThanOrEqualTo(2.4f));

        IllustratedStoryPage monster = story.Pages[1];
        AssertReference(monster.Picture, "fafda599b73c23346bd24072131eef2a", 21300000L);
        Assert.That(monster.Animation, Is.Null);
        Assert.That(monster.Dialogue, Is.Null);
        Assert.That(monster.WaitForConfirmWithoutDialogue, Is.True);
        Assert.That(monster.FadeInDuration, Is.GreaterThanOrEqualTo(1f));
    }

    [Test]
    public void OnlyRearRevealPagesUseFullScreenPictures()
    {
        IllustratedStoryAsset opening = LoadStory("Prologue_Opening");
        for (int i = 0; i < opening.Pages.Count; i++)
            Assert.That(opening.Pages[i].FullScreen, Is.False, "opening page " + i);
        foreach (IllustratedStoryPage page in LoadStory("Prologue_RearReveal").Pages)
            Assert.That(page.FullScreen, Is.True);
    }

    [Test]
    public void WindowConversationRetainsTypingSound()
    {
        IllustratedStoryAsset opening = LoadStory("Prologue_Opening");
        Assert.That(opening.Pages[0].MuteTypingSound, Is.False, "window conversation");
        Assert.That(new IllustratedStoryPage().MuteTypingSound, Is.False, "다른 대화의 기본 타이핑 소리는 유지합니다.");
    }

    [Test]
    public void NameInputHierarchyIsRemovedWithoutRemovingSharedDialoguePanels()
    {
        string canvas = ReadProjectText("Assets/_Game/Content/Dialogue/Prefabs/DialogueCanvas.prefab");
        string manager = ReadProjectText("Assets/_Game/Core/Prefabs/CoreSettings/DialogueManager.prefab");
        StringAssert.DoesNotContain("NameInputPanel", canvas);
        StringAssert.DoesNotContain("NameInputUI", canvas);
        StringAssert.DoesNotContain("NameInputUI", manager);
        StringAssert.DoesNotContain("_nameInputUI", manager);
        StringAssert.Contains("m_Name: DialogueCanvas", canvas);
        StringAssert.Contains("m_Name: OverworldPanel", canvas);
        StringAssert.Contains("m_Name: CinematicPanel", canvas);
        StringAssert.Contains("m_Name: BattleNarrationPanel", canvas);
        Assert.That(typeof(DialogueManager).GetField("_nameInputUI", BindingFlags.Instance | BindingFlags.NonPublic), Is.Null);
    }

    [Test]
    public void DefaultHeroAndSpeakerUseTheirAuthoredNamesWithoutChangingOtherCharacters()
    {
        CharacterData hero = AssetDatabase.LoadAssetAtPath<CharacterData>(
            "Assets/_Game/Content/Characters/AllyDB/WizzelDB.asset");
        SpeakerData speaker = AssetDatabase.LoadAssetAtPath<SpeakerData>(
            "Assets/_Game/Content/Dialogue/Data/Speakers/Speaker_Wizzel.asset");
        Assert.That(hero, Is.Not.Null);
        Assert.That(speaker, Is.Not.Null);
        Assert.That(hero.DisplayNameMode, Is.EqualTo(CharacterDisplayNameMode.StaticData));
        Assert.That(hero.ResolveDisplayName("옛 저장 이름"), Is.EqualTo(GlobalDataManager.DefaultPlayerName));
        Assert.That(speaker.DisplayName, Is.EqualTo(GlobalDataManager.DefaultPlayerName));

        CharacterData companion = ScriptableObject.CreateInstance<CharacterData>();
        try
        {
            companion.DisplayName = "동료 이름";
            Assert.That(companion.ResolveDisplayName(GlobalDataManager.DefaultPlayerName), Is.EqualTo("동료 이름"));
        }
        finally { Object.DestroyImmediate(companion); }
    }

    [Test]
    public void FullScreenAnimationKeeps640By480MaskAndFiveTimesScaleAcrossFrameSizeAndPivotChanges()
    {
        GameObject root = null;
        Texture2D texture = null;
        Sprite first = null;
        Sprite second = null;
        AnimationClip clip = null;
        try
        {
            texture = new Texture2D(256, 96);
            first = Sprite.Create(texture, new Rect(0f, 0f, 128f, 96f), new Vector2(0.5f, 0f));
            second = Sprite.Create(texture, new Rect(0f, 0f, 256f, 96f), new Vector2(0.25f, 0.75f));
            clip = new AnimationClip { frameRate = 10f };
            AnimationUtility.SetObjectReferenceCurve(clip,
                EditorCurveBinding.PPtrCurve(string.Empty, typeof(SpriteRenderer), "m_Sprite"),
                new[]
                {
                    new ObjectReferenceKeyframe { time = 0f, value = first },
                    new ObjectReferenceKeyframe { time = 0.2f, value = second },
                    new ObjectReferenceKeyframe { time = 0.5f, value = second }
                });
            var page = new IllustratedStoryPage
            {
                Animation = clip,
                CanvasSize = new Vector2Int(128, 96),
                FullScreen = true,
                Frames = new[]
                {
                    new IllustratedStoryFrame { Sprite = first, Time = 0f },
                    new IllustratedStoryFrame { Sprite = second, Time = 0.2f }
                }
            };
            IllustratedStoryPlayer player = CreatePictureHarness(out root, out RectTransform mask, out Image picture);
            SetPrivateField(player, "_page", page);
            InvokePrivate(player, "ConfigurePicture", page);
            Assert.That((bool)InvokePrivate(player, "RefreshPicture"), Is.True);

            Assert.That(mask.sizeDelta, Is.EqualTo(new Vector2(640f, 480f)));
            Assert.That(mask.anchoredPosition, Is.EqualTo(Vector2.zero));
            Assert.That(picture.rectTransform.sizeDelta, Is.EqualTo(new Vector2(640f, 480f)));
            Assert.That(picture.rectTransform.pivot, Is.EqualTo(new Vector2(0.5f, 0f)));

            SetPrivateField(player, "_pageElapsed", 0.25f);
            Assert.That((bool)InvokePrivate(player, "RefreshPicture"), Is.True);
            Assert.That(picture.sprite, Is.SameAs(second));
            Assert.That(mask.sizeDelta, Is.EqualTo(new Vector2(640f, 480f)));
            Assert.That(mask.anchoredPosition, Is.EqualTo(Vector2.zero));
            Assert.That(picture.rectTransform.sizeDelta, Is.EqualTo(new Vector2(1280f, 480f)));
            Assert.That(picture.rectTransform.pivot, Is.EqualTo(new Vector2(0.25f, 0.75f)));
            Assert.That(picture.rectTransform.anchoredPosition, Is.EqualTo(Vector2.zero));
        }
        finally
        {
            if (root != null) Object.DestroyImmediate(root);
            if (clip != null) Object.DestroyImmediate(clip);
            if (first != null) Object.DestroyImmediate(first);
            if (second != null) Object.DestroyImmediate(second);
            if (texture != null) Object.DestroyImmediate(texture);
        }
    }

    [Test]
    public void NonFullScreenStaticPicturePreservesThreeTimesSizeAndRaisedPosition()
    {
        GameObject root = null;
        Texture2D texture = null;
        Sprite sprite = null;
        try
        {
            texture = new Texture2D(108, 68);
            sprite = Sprite.Create(texture, new Rect(0f, 0f, 108f, 68f), new Vector2(0.5f, 0.5f));
            var page = new IllustratedStoryPage { Picture = sprite, PixelScale = 3 };
            Assert.That(page.FullScreen, Is.False, "기존 페이지의 기본값은 부분 화면입니다.");
            IllustratedStoryPlayer player = CreatePictureHarness(out root, out RectTransform mask, out Image picture);
            var caption = root.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
            caption.maxVisibleCharacters = 0;
            SetPrivateField(player, "_page", page);
            InvokePrivate(player, "ConfigurePicture", page);
            Assert.That((bool)InvokePrivate(player, "RefreshPicture"), Is.True);
            Assert.That(caption.maxVisibleCharacters, Is.EqualTo(int.MaxValue), "이전 중앙 자막의 글자 제한을 복원합니다.");

            Assert.That(mask.sizeDelta, Is.EqualTo(new Vector2(324f, 204f)));
            Assert.That(mask.anchoredPosition, Is.EqualTo(new Vector2(0f, 60f)));
            Assert.That(picture.rectTransform.sizeDelta, Is.EqualTo(new Vector2(324f, 204f)));
            Assert.That(picture.rectTransform.pivot, Is.EqualTo(new Vector2(0.5f, 0.5f)));
        }
        finally
        {
            if (root != null) Object.DestroyImmediate(root);
            if (sprite != null) Object.DestroyImmediate(sprite);
            if (texture != null) Object.DestroyImmediate(texture);
        }
    }

    [Test]
    public void IntroSceneReferencesOpeningAndExistingTrainArrivalWithBootstrap()
    {
        string scene = ReadProjectText(IntroScene);
        Match manager = Regex.Match(scene,
            @"m_EditorClassIdentifier: Assembly-CSharp::IntroManager(?<body>[\s\S]*?)(?:\r?\n--- |\z)");
        Assert.That(manager.Success, Is.True, "IntroManager component is missing.");
        string fields = manager.Groups["body"].Value;
        StringAssert.Contains("_openingStory: {fileID: 11400000, guid: e7493822dbc44df4abac65779127c25d, type: 2}", fields);
        StringAssert.Contains("_nextSceneName: Region_Train", fields);
        StringAssert.Contains("_nextRoomId: all.trainq0", fields);
        StringAssert.Contains("_nextSpawnPointId: default", fields);
        StringAssert.Contains("m_SourcePrefab: {fileID: 100100000, guid: bb413fa6573383244a4761e7125de72b, type: 3}", scene);
    }

    [Test]
    public void TrainRoomReferencesThreeSecondIncidentAndRearReveal()
    {
        GameObject room = AssetDatabase.LoadAssetAtPath<GameObject>(
            ContentRoot + "/Prefabs/Rooms/Room_ALL_TrainQ0.prefab");
        Assert.That(room, Is.Not.Null);
        PrologueTrainSequence[] sequences = room.GetComponentsInChildren<PrologueTrainSequence>(true);
        Assert.That(sequences.Length, Is.EqualTo(1));
        var serialized = new SerializedObject(sequences[0]);
        Assert.That(serialized.FindProperty("_enableTemporaryIncident").boolValue, Is.True);
        Assert.That(serialized.FindProperty("_temporaryIncidentDelay").floatValue, Is.EqualTo(3f));
        Assert.That(serialized.FindProperty("_requiredOpeningFlag").stringValue, Is.EqualTo("prologue.opening.completed"));
        Assert.That(serialized.FindProperty("_rearTriggerLocalX").floatValue, Is.GreaterThan(0f));
        Assert.That(serialized.FindProperty("_rearRevealStory").objectReferenceValue,
            Is.EqualTo(LoadStory("Prologue_RearReveal")));

        DialogueData incident = AssetDatabase.LoadAssetAtPath<DialogueData>(
            StoryRoot + "/Dialogue/Prologue_TrainIncident.asset");
        Assert.That(serialized.FindProperty("_incidentDialogue").objectReferenceValue, Is.EqualTo(incident));
        Assert.That(incident, Is.Not.Null);
        Assert.That(incident.Style, Is.EqualTo(DialogueStyle.Overworld));
        Assert.That(incident.Nodes.Count, Is.GreaterThanOrEqualTo(1));
        foreach (DialogueNode node in incident.Nodes)
            AssertReference(node.Speaker, "d0a8d7fc4bc8456e8ae9b7aaf71bcb65", 11400000L);
    }

    [Test]
    public void TrainAudioProfileUsesAuthoredClipsVolumesAndFadeDurations()
    {
        PrologueTrainAudioProfile profile = AssetDatabase.LoadAssetAtPath<PrologueTrainAudioProfile>(
            StoryRoot + "/Prologue_TrainAudio.asset");
        AssertReference(profile, "45ce4e91976b4cefbc1e31539d4cef99", 11400000L);
        AssertReference(profile.TrainLoop, "8628f03c8607e26428d4ccbec97442c6", 8300000L);
        AssertReference(profile.PowerDown, "2f53a3d72466f204ea7f88a3dafe43d2", 8300000L);
        AssertReference(profile.Footsteps, "3fea568341a92a5468d91e779ed0f70c", 8300000L);
        Assert.That(profile.TrainVolume, Is.EqualTo(0.85f).Within(0.0001f));
        Assert.That(profile.PowerDownVolume, Is.EqualTo(0.22f).Within(0.0001f));
        Assert.That(profile.FootstepsVolume, Is.EqualTo(0.35f).Within(0.0001f));
        Assert.That(profile.TrainFadeIn, Is.EqualTo(0.6f).Within(0.0001f));
        Assert.That(profile.TrainFadeOut, Is.EqualTo(1.8f).Within(0.0001f));
        Assert.That(profile.FootstepsFadeOut, Is.EqualTo(0.12f).Within(0.0001f));
    }

    [Test]
    public void RemovedNationNarrationCannotPlayItsBgmButOriginalAudioIsPreserved()
    {
        StringAssert.DoesNotContain("OpeningMusic", ReadProjectText(StoryRoot + "/Prologue_TrainAudio.asset"));
        StringAssert.DoesNotContain("OpeningMusic", ReadProjectText("Assets/_Game/Scripts/UI/Runtime/IntroManager.cs"));
        string originalMusicPath = AssetDatabase.GUIDToAssetPath("d7884bd51df49324580130cd753911ab");
        Assert.That(originalMusicPath, Is.Not.Empty);
        Assert.That(AssetDatabase.LoadAssetAtPath<AudioClip>(originalMusicPath), Is.Not.Null);
    }

    [Test]
    public void IntroAndTrainRoomShareAudioProfileAtWindowAndRearWalkingPages()
    {
        PrologueTrainAudioProfile profile = AssetDatabase.LoadAssetAtPath<PrologueTrainAudioProfile>(
            StoryRoot + "/Prologue_TrainAudio.asset");
        Assert.That(profile, Is.Not.Null);
        string scene = ReadProjectText(IntroScene);
        Match manager = Regex.Match(scene,
            @"m_EditorClassIdentifier: Assembly-CSharp::IntroManager(?<body>[\s\S]*?)(?:\r?\n--- |\z)");
        Assert.That(manager.Success, Is.True, "IntroManager component is missing.");
        string fields = manager.Groups["body"].Value;
        StringAssert.Contains("_trainAudioProfile: {fileID: 11400000, guid: 45ce4e91976b4cefbc1e31539d4cef99, type: 2}", fields);
        Assert.That(Regex.IsMatch(fields, @"(?m)^\s+_trainAudioStartPage: 0\s*$"), Is.True);
        StringAssert.DoesNotContain("_openingMusicFadeOutPage", fields);
        Assert.That(LoadStory("Prologue_Opening").Pages[0].LoopAnimation, Is.True);

        GameObject room = AssetDatabase.LoadAssetAtPath<GameObject>(
            ContentRoot + "/Prefabs/Rooms/Room_ALL_TrainQ0.prefab");
        Assert.That(room, Is.Not.Null);
        PrologueTrainSequence[] sequences = room.GetComponentsInChildren<PrologueTrainSequence>(true);
        Assert.That(sequences.Length, Is.EqualTo(1));
        var serialized = new SerializedObject(sequences[0]);
        SerializedProperty audioProfile = serialized.FindProperty("_trainAudioProfile");
        SerializedProperty walkingPage = serialized.FindProperty("_rearWalkingPage");
        Assert.That(audioProfile, Is.Not.Null);
        Assert.That(walkingPage, Is.Not.Null);
        Assert.That(audioProfile.objectReferenceValue, Is.SameAs(profile));
        Assert.That(walkingPage.intValue, Is.EqualTo(0));
        IllustratedStoryPage movement = LoadStory("Prologue_RearReveal").Pages[walkingPage.intValue];
        Assert.That(movement.Animation, Is.Not.Null);
        Assert.That(movement.LoopAnimation, Is.False);
    }

    [Test]
    public void BuildStartsAtTitleAndRegistersTrain()
    {
        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
        string firstEnabled = null;
        bool trainRegistered = false;
        foreach (EditorBuildSettingsScene scene in scenes)
        {
            if (!scene.enabled) continue;
            if (firstEnabled == null) firstEnabled = scene.path;
            if (scene.path == TrainScene) trainRegistered = true;
        }
        Assert.That(firstEnabled, Is.EqualTo(TitleScene));
        Assert.That(trainRegistered, Is.True);
    }

    [Test]
    public void PrologueTextUsesUniqueLocalizationKeysAndShortTwoLineNodes()
    {
        string[] guids = AssetDatabase.FindAssets("t:DialogueData", new[] { StoryRoot + "/Dialogue" });
        Assert.That(guids, Is.Not.Empty);
        var keys = new HashSet<string>();
        IllustratedStoryAsset opening = LoadStory("Prologue_Opening");
        foreach (IllustratedStoryPage page in opening.Pages)
        {
            if (!page.IsCaptionPage) continue;
            StringAssert.StartsWith("PROLOGUE_", page.CaptionLocalizationKey);
            Assert.That(keys.Add(page.CaptionLocalizationKey), Is.True, page.CaptionLocalizationKey);
        }
        foreach (string guid in guids)
        {
            DialogueData dialogue = AssetDatabase.LoadAssetAtPath<DialogueData>(AssetDatabase.GUIDToAssetPath(guid));
            Assert.That(dialogue.Nodes.Count, Is.GreaterThanOrEqualTo(1));
            foreach (DialogueNode node in dialogue.Nodes)
            {
                Assert.That(node, Is.Not.Null);
                StringAssert.StartsWith("PROLOGUE_", node.LocalizationKey);
                Assert.That(keys.Add(node.LocalizationKey), Is.True, node.LocalizationKey);
                Assert.That(string.IsNullOrWhiteSpace(node.DefaultText), Is.False);
                string[] lines = node.DefaultText.Split('\n');
                Assert.That(lines.Length, Is.LessThanOrEqualTo(2), node.LocalizationKey);
                foreach (string line in lines)
                    Assert.That(line.TrimEnd('\r').Length, Is.LessThanOrEqualTo(13), node.LocalizationKey);
                Assert.That(node.EventTriggerID, Is.Not.EqualTo("RequestName"));
            }
        }
    }

    [TestCase("Prologue_Opening", 0)]
    [TestCase("Prologue_RearReveal", 0)]
    public void AnimationFramesMatchSpriteCurveOrderWithoutRepeatedHoldKeys(string storyName, int pageIndex)
    {
        IllustratedStoryPage page = LoadStory(storyName).Pages[pageIndex];
        Assert.That(page.Animation, Is.Not.Null);
        var authoredFrames = new List<ObjectReferenceKeyframe>();
        int spriteCurves = 0;
        foreach (EditorCurveBinding binding in AnimationUtility.GetObjectReferenceCurveBindings(page.Animation))
        {
            if (binding.type != typeof(SpriteRenderer) || binding.propertyName != "m_Sprite") continue;
            spriteCurves++;
            foreach (ObjectReferenceKeyframe key in AnimationUtility.GetObjectReferenceCurve(page.Animation, binding))
            {
                // 마지막 동일 Sprite 유지 키는 캐시에서 생략하고 clip.length로 전체 시간을 보존합니다.
                if (authoredFrames.Count > 0 && authoredFrames[authoredFrames.Count - 1].value == key.value)
                    continue;
                authoredFrames.Add(key);
            }
        }

        Assert.That(spriteCurves, Is.EqualTo(1), storyName);
        Assert.That(authoredFrames, Is.Not.Empty);
        Assert.That(page.Frames, Is.Not.Null);
        Assert.That(page.Frames.Length, Is.EqualTo(authoredFrames.Count), storyName);
        for (int i = 0; i < authoredFrames.Count; i++)
        {
            Assert.That(page.Frames[i].Sprite, Is.SameAs(authoredFrames[i].value), storyName + " frame " + i);
            Assert.That(page.Frames[i].Time, Is.EqualTo(authoredFrames[i].time).Within(0.0001f), storyName + " frame " + i);
        }
        Assert.That(page.Frames[0].Time, Is.EqualTo(0f));
        Assert.That(page.ResolveAnimationSprite(0f), Is.SameAs(page.Frames[0].Sprite));
    }

    [TestCase(false, 0f, 0)]
    [TestCase(true, 0.5f, 0)]
    [TestCase(false, 2.4f, 2)]
    [TestCase(false, 0.2f, 1)]
    public void AnimationSpriteResolutionHandlesFirstFrameLoopAndOneShotEnd(bool loop, float elapsed, int expectedIndex)
    {
        Texture2D texture = null;
        var sprites = new Sprite[3];
        AnimationClip clip = null;
        try
        {
            texture = new Texture2D(3, 1);
            for (int i = 0; i < sprites.Length; i++)
                sprites[i] = Sprite.Create(texture, new Rect(i, 0f, 1f, 1f), Vector2.zero);
            clip = new AnimationClip { frameRate = 10f };
            AnimationUtility.SetObjectReferenceCurve(clip,
                EditorCurveBinding.PPtrCurve(string.Empty, typeof(SpriteRenderer), "m_Sprite"),
                new[]
                {
                    new ObjectReferenceKeyframe { time = 0f, value = sprites[0] },
                    new ObjectReferenceKeyframe { time = 0.2f, value = sprites[1] },
                    new ObjectReferenceKeyframe { time = 0.4f, value = sprites[2] },
                    new ObjectReferenceKeyframe { time = 0.5f, value = sprites[2] }
                });
            Assert.That(clip.length, Is.EqualTo(0.5f).Within(0.0001f));
            var page = new IllustratedStoryPage
            {
                Animation = clip,
                LoopAnimation = loop,
                Frames = new[]
                {
                    new IllustratedStoryFrame { Sprite = sprites[0], Time = 0f },
                    new IllustratedStoryFrame { Sprite = sprites[1], Time = 0.2f },
                    new IllustratedStoryFrame { Sprite = sprites[2], Time = 0.4f }
                }
            };

            Assert.That(page.ResolveAnimationSprite(elapsed), Is.SameAs(sprites[expectedIndex]));
        }
        finally
        {
            if (clip != null) Object.DestroyImmediate(clip);
            foreach (Sprite sprite in sprites)
                if (sprite != null) Object.DestroyImmediate(sprite);
            if (texture != null) Object.DestroyImmediate(texture);
        }
    }

    [Test]
    public void ValidationAcceptsCaptionOnlyPageWithoutPictureAnimationOrDialogue()
    {
        IllustratedStoryAsset story = ScriptableObject.CreateInstance<IllustratedStoryAsset>();
        try
        {
            var page = new IllustratedStoryPage
            {
                Caption = "그리고... 현재.",
                CaptionLocalizationKey = "PROLOGUE_TIME_PRESENT",
                MinimumDuration = 3.8f,
                FadeInDuration = 1.4f,
                FadeOutDuration = 1f
            };
            story.Pages.Add(page);
            Assert.That(page.IsCaptionPage, Is.True);
            Assert.That(story.TryValidate(out string error), Is.True, error);
            Assert.That(page.Picture, Is.Null);
            Assert.That(page.Animation, Is.Null);
            Assert.That(page.Dialogue, Is.Null);
            Assert.That(page.WaitForConfirmWithoutDialogue, Is.False);
        }
        finally { Object.DestroyImmediate(story); }
    }

    [Test]
    public void OpeningPreservesNarrationRateAndReusableCaptionDefaults()
    {
        IllustratedStoryAsset opening = LoadStory("Prologue_Opening");
        Assert.That(opening.NarrationCharactersPerSecond, Is.EqualTo(10f));
        Assert.That(opening.Pages.Count, Is.EqualTo(1));
        Assert.That(opening.Pages[0].IsCaptionPage, Is.False);
        Assert.That(new IllustratedStoryPage().CaptionCharactersPerSecond, Is.EqualTo(4f));
        Assert.That(new IllustratedStoryPage().CaptionHoldDuration, Is.EqualTo(1.2f).Within(0.0001f));
    }

    [Test]
    public void CaptionTypingAdvancesFourCharactersPerSecondPausesAndHoldsAfterCompletion()
    {
        GameObject root = null;
        bool previousConfigModal = GameInput.IsConfigModalActive;
        try
        {
            GameInput.SetConfigModalActive(false);
            IllustratedStoryPlayer player = CreatePictureHarness(out root, out _, out _);
            var caption = root.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
            caption.maxVisibleCharacters = 0;
            var page = new IllustratedStoryPage
            {
                Caption = "ABCDEFGHIJ",
                CaptionCharactersPerSecond = 4f,
                CaptionHoldDuration = 1.2f,
                MinimumDuration = 0f
            };
            SetPrivateField(player, "_page", page);
            // TMP 해석 결과만 주입하여 글꼴·화면 렌더링 없이 시간 판정을 검사합니다.
            SetPrivateField(player, "_captionCharacterCount", 10);
            SetPrivateField(player, "_lastCaptionVisible", 0);
            SetPrivateField(player, "_pageElapsed", 0.249f);
            InvokePrivate(player, "UpdateCaption");
            Assert.That(caption.maxVisibleCharacters, Is.EqualTo(0));
            SetPrivateField(player, "_pageElapsed", 0.25f);
            InvokePrivate(player, "UpdateCaption");
            Assert.That(caption.maxVisibleCharacters, Is.EqualTo(1));

            GameInput.SetConfigModalActive(true);
            SetPrivateField(player, "_pageElapsed", 1f);
            InvokePrivate(player, "UpdateCaption");
            Assert.That(caption.maxVisibleCharacters, Is.EqualTo(1));
            GameInput.SetConfigModalActive(false);
            InvokePrivate(player, "UpdateCaption");
            Assert.That(caption.maxVisibleCharacters, Is.EqualTo(4));
            SetPrivateField(player, "_pageElapsed", 2.5f);
            InvokePrivate(player, "UpdateCaption");
            Assert.That(caption.maxVisibleCharacters, Is.EqualTo(10));
            Assert.That((float)InvokePrivate(player, "GetMinimumPageDuration"), Is.EqualTo(3.7f).Within(0.0001f));

            SetPrivateField(player, "_pageElapsed", 4f);
            InvokePrivate(player, "UpdateCaption");
            Assert.That(caption.maxVisibleCharacters, Is.EqualTo(10));
            page.MinimumDuration = 5f;
            Assert.That((float)InvokePrivate(player, "GetMinimumPageDuration"), Is.EqualTo(5f));
        }
        finally
        {
            GameInput.SetConfigModalActive(previousConfigModal);
            if (root != null) Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void ValidationRejectsInvalidTypingRatesAndCaptionHoldButAcceptsZeroHold()
    {
        IllustratedStoryAsset story = ScriptableObject.CreateInstance<IllustratedStoryAsset>();
        try
        {
            var page = new IllustratedStoryPage { Caption = "현재" };
            story.Pages.Add(page);
            float[] invalidRates = { 0f, -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity };
            foreach (float value in invalidRates)
            {
                story.NarrationCharactersPerSecond = value;
                Assert.That(story.TryValidate(out string error), Is.False);
                Assert.That(error, Is.EqualTo("illustrated_story_narration_speed_invalid"));
            }
            story.NarrationCharactersPerSecond = 10f;
            foreach (float value in invalidRates)
            {
                page.CaptionCharactersPerSecond = value;
                Assert.That(story.TryValidate(out string error), Is.False);
                Assert.That(error, Is.EqualTo("illustrated_story_caption_timing_invalid: 0"));
            }
            page.CaptionCharactersPerSecond = 4f;
            foreach (float value in new[] { -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                page.CaptionHoldDuration = value;
                Assert.That(story.TryValidate(out string error), Is.False);
                Assert.That(error, Is.EqualTo("illustrated_story_caption_timing_invalid: 0"));
            }
            page.CaptionHoldDuration = 0f;
            Assert.That(story.TryValidate(out string validError), Is.True, validError);
        }
        finally { Object.DestroyImmediate(story); }
    }

    [TestCase(true)]
    [TestCase(false)]
    public void ValidationRejectsNullOrEmptyPageList(bool nullList)
    {
        IllustratedStoryAsset story = ScriptableObject.CreateInstance<IllustratedStoryAsset>();
        try
        {
            if (nullList) story.Pages = null;
            Assert.That(story.TryValidate(out string error), Is.False);
            Assert.That(error, Is.EqualTo("illustrated_story_pages_missing"));
        }
        finally { Object.DestroyImmediate(story); }
    }

    [TestCase(true)]
    [TestCase(false)]
    public void ValidationRejectsNullPageOrMissingArt(bool nullPage)
    {
        IllustratedStoryAsset story = ScriptableObject.CreateInstance<IllustratedStoryAsset>();
        try
        {
            story.Pages.Add(nullPage ? null : new IllustratedStoryPage());
            Assert.That(story.TryValidate(out string error), Is.False);
            Assert.That(error, Is.EqualTo("illustrated_story_art_missing: 0"));
        }
        finally { Object.DestroyImmediate(story); }
    }

    [TestCase(0, float.NaN)]
    [TestCase(1, float.NaN)]
    [TestCase(2, float.NaN)]
    [TestCase(0, float.PositiveInfinity)]
    [TestCase(1, -1f)]
    public void ValidationRejectsInvalidDuration(int field, float value)
    {
        IllustratedStoryAsset story = ScriptableObject.CreateInstance<IllustratedStoryAsset>();
        Texture2D texture = null;
        Sprite sprite = null;
        try
        {
            texture = new Texture2D(1, 1);
            sprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), Vector2.zero);
            var page = new IllustratedStoryPage { Picture = sprite };
            if (field == 0) page.MinimumDuration = value;
            else if (field == 1) page.FadeInDuration = value;
            else page.FadeOutDuration = value;
            story.Pages.Add(page);

            Assert.That(story.TryValidate(out string error), Is.False);
            Assert.That(error, Is.EqualTo("illustrated_story_duration_invalid: 0"));
        }
        finally
        {
            if (sprite != null) Object.DestroyImmediate(sprite);
            if (texture != null) Object.DestroyImmediate(texture);
            Object.DestroyImmediate(story);
        }
    }

    private static IllustratedStoryAsset LoadStory(string name)
    {
        IllustratedStoryAsset story = AssetDatabase.LoadAssetAtPath<IllustratedStoryAsset>(StoryRoot + "/" + name + ".asset");
        Assert.That(story, Is.Not.Null, name);
        return story;
    }

    private static IllustratedStoryPlayer CreatePictureHarness(
        out GameObject root, out RectTransform mask, out Image picture)
    {
        root = new GameObject("Prologue Picture Layout Test");
        IllustratedStoryPlayer player = root.AddComponent<IllustratedStoryPlayer>();
        mask = new GameObject("Mask", typeof(RectTransform)).GetComponent<RectTransform>();
        mask.SetParent(root.transform, false);
        picture = new GameObject("Picture", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        picture.transform.SetParent(mask, false);
        var caption = new GameObject("Caption", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
        caption.transform.SetParent(root.transform, false);
        SetPrivateField(player, "_pictureMask", mask);
        SetPrivateField(player, "_picture", picture);
        SetPrivateField(player, "_caption", caption.GetComponent<TMPro.TextMeshProUGUI>());
        return player;
    }

    private static void SetPrivateField(IllustratedStoryPlayer player, string name, object value)
    {
        FieldInfo field = typeof(IllustratedStoryPlayer).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, name);
        field.SetValue(player, value);
    }

    private static object InvokePrivate(IllustratedStoryPlayer player, string name, params object[] args)
    {
        MethodInfo method = typeof(IllustratedStoryPlayer).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, name);
        return method.Invoke(player, args);
    }

    private static void AssertReference(Object asset, string expectedGuid, long expectedFileId)
    {
        Assert.That(asset, Is.Not.Null);
        Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long fileId), Is.True);
        Assert.That(guid, Is.EqualTo(expectedGuid));
        Assert.That(fileId, Is.EqualTo(expectedFileId));
    }

    private static string ReadProjectText(string path)
        => File.ReadAllText(Path.GetFullPath(Path.Combine(Application.dataPath, "..", path)),
            new UTF8Encoding(false, true));
}
