using System.Collections.Generic;
using System.IO;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class OverworldSubwayCinematicSampleBuilder
{
    private const string SequenceAssetPath = "Assets/_Game/Content/Scenarios/Runtime/Overworld/overworld_intro_subway.asset";
    private const string ShotAssetPath = DevelopmentContentPaths.PrologueSubwayCinematicRoot + "/overworld_intro_subway_arrival.asset";
    private const string CatalogAssetPath = "Assets/_Game/Content/Scenarios/ActionCatalogs/OverworldCinematicActionCatalog.asset";
    private const string SourcePath = "Assets/_Game/Content/Scenarios/Source/Overworld/overworld_intro_subway.sequence.yaml";
    private const string StageName = "OverworldCinematicStage_Subway";
    private const string TriggerName = "OverworldIntroSequenceTrigger";
    private const string GameplayRevealFocusName = "OverworldGameplayRevealFocus";
    private const string StageId = "overworld.subway_intro";
    private const string ShotId = "subway_arrival";

    public static void BuildOrUpdate()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded || scene.name != "OverworldScene")
        {
            Debug.LogError("[OverworldSubwayCinematicSampleBuilder] OverworldScene을 연 뒤 실행해야 합니다.");
            return;
        }

        if (scene.isDirty)
        {
            Debug.LogWarning("[OverworldSubwayCinematicSampleBuilder] 저장되지 않은 씬 변경이 있어 안전상 중단했습니다. 씬을 먼저 저장한 뒤 다시 실행하세요.");
            return;
        }

        ActionCatalogAsset catalog = BuildCatalog();
        ActionSequenceAsset sequence = BuildSequence();
        CinematicShotAsset shot = BuildShot();
        OverworldCinematicStage stage = BuildStage(shot);
        BuildGameplayRevealFraming();
        BuildTrigger(sequence, stage);

        EnsureFolderForAsset(SourcePath);
        ActionSequenceSourceSync.SaveToSourcePath(sequence);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeObject = sequence;
        Debug.Log("[OverworldSubwayCinematicSampleBuilder] 지하철 인트로 시퀀스와 OverworldScene 리그를 생성 또는 갱신했습니다. Catalog=" + catalog.name, sequence);
    }

    private static ActionCatalogAsset BuildCatalog()
    {
        EnsureFolderForAsset(CatalogAssetPath);
        ActionCatalogAsset catalog = LoadOrCreate<ActionCatalogAsset>(CatalogAssetPath);
        catalog.CatalogId = "overworld.cinematic";
        catalog.Entries = new List<ActionCatalogEntry>
        {
            Entry("flow.wait", "flow", "대기", "정해진 시간만큼 시퀀스를 대기합니다.", "FlowWaitActionAdapter", "- action: flow.wait\n  params: { duration: 1.0 }", Param("duration", "float", "시간", "초 단위 대기 시간", true)),
            Entry("screen.fade", "presentation", "화면 페이드", "전역 화면 전환 오버레이를 페이드합니다.", "ScreenFadeActionAdapter", "- action: screen.fade\n  params: { mode: out, color: black, duration: 0.5 }", Param("mode", "string", "방향", "out 또는 in", true), Param("color", "string", "색상", "black, white 또는 HTML 색상", false), Param("duration", "float", "시간", "초 단위 페이드 시간", true)),
            Entry("cinematic.stage.prepare", "cinematic", "시네마틱 스테이지 준비", "씬 공개 전에 전용 카메라와 대상의 시작 상태를 준비합니다.", "CinematicStagePrepareActionAdapter", "- action: cinematic.stage.prepare\n  params: { stage: overworld.subway_intro, shot: subway_arrival }", Param("stage", "id", "스테이지", "Cinematic Stage ID", true), Param("shot", "id", "샷", "Cinematic Shot ID", true)),
            Entry("cinematic.shot.play", "cinematic", "시네마틱 샷 재생", "전용 카메라 레일과 여러 대상 모션을 동시에 재생합니다.", "CinematicShotPlayActionAdapter", "- action: cinematic.shot.play\n  params: { stage: overworld.subway_intro, shot: subway_arrival }", Param("stage", "id", "스테이지", "Cinematic Stage ID", true), Param("shot", "id", "샷", "Cinematic Shot ID", true)),
            Entry("cinematic.stage.release", "cinematic", "시네마틱 스테이지 해제", "전용 카메라를 끄고 기본 게임 카메라로 복귀합니다.", "CinematicStageReleaseActionAdapter", "- action: cinematic.stage.release\n  params: { stage: overworld.subway_intro }", Param("stage", "id", "스테이지", "Cinematic Stage ID", true))
        };
        EditorUtility.SetDirty(catalog);
        return catalog;
    }

    private static ActionSequenceAsset BuildSequence()
    {
        EnsureFolderForAsset(SequenceAssetPath);
        ActionSequenceAsset sequence = LoadOrCreate<ActionSequenceAsset>(SequenceAssetPath);
        sequence.SequenceId = "overworld.intro.subway";
        sequence.DisplayNameKo = "오버월드 시작 - 지하철 도착";
        if (sequence.Source == null)
        {
            sequence.Source = new ScenarioSourceMetadata();
        }

        sequence.Source.SourcePath = SourcePath;
        sequence.Actions = new List<ScenarioActionData>
        {
            Action("밝아진 장면을 잠시 보여주기", "flow.wait", "{\"duration\":0.75}"),
            Action("지하철 도착 샷", "cinematic.shot.play", "{\"stage\":\"overworld.subway_intro\",\"shot\":\"subway_arrival\"}"),
            Action("기차가 지나간 뒤 잠시 유지", "flow.wait", "{\"duration\":0.35}"),
            Action("장면을 검게 전환", "screen.fade", "{\"mode\":\"out\",\"color\":\"black\",\"duration\":1.0}"),
            Action("암전 상태로 2초 대기", "flow.wait", "{\"duration\":2.0}"),
            Action("시네마틱 카메라 해제", "cinematic.stage.release", "{\"stage\":\"overworld.subway_intro\"}"),
            Action("오버월드 공개", "screen.fade", "{\"mode\":\"in\",\"color\":\"black\",\"duration\":0.9}")
        };
        EditorUtility.SetDirty(sequence);
        return sequence;
    }

    private static CinematicShotAsset BuildShot()
    {
        EnsureFolderForAsset(ShotAssetPath);
        CinematicShotAsset shot = LoadOrCreate<CinematicShotAsset>(ShotAssetPath);
        shot.StageId = StageId;
        shot.ShotId = ShotId;
        shot.DisplayNameKo = "지하철 도착 카메라 이동";
        shot.CameraRailSubjectId = "camera_rail";
        shot.StartOrthographicSize = 10f;
        shot.EndOrthographicSize = 7f;
        shot.CameraDelay = 4.45f;
        shot.CameraDuration = 3.55f;
        shot.CameraEase = DG.Tweening.Ease.InOutSine;
        shot.CameraPositionDamping = Vector3.zero;
        shot.Motions = new List<CinematicShotMotion>
        {
            new CinematicShotMotion
            {
                SubjectId = "subway",
                StartLocalPosition = new Vector3(-30f, 0f, 0f),
                EndLocalPosition = new Vector3(24f, 0f, 0f),
                Duration = 8f,
                Ease = DG.Tweening.Ease.Linear
            },
            new CinematicShotMotion
            {
                SubjectId = "camera_rail",
                StartLocalPosition = new Vector3(-2.0625f, 3.75f, 0f),
                EndLocalPosition = new Vector3(21.9f, 3.75f, 0f),
                Delay = 4.45f,
                Duration = 3.55f,
                Ease = DG.Tweening.Ease.Linear
            }
        };
        EditorUtility.SetDirty(shot);
        return shot;
    }

    private static OverworldCinematicStage BuildStage(CinematicShotAsset shot)
    {
        GameObject stageObject = GameObject.Find(StageName);
        if (stageObject == null)
        {
            stageObject = new GameObject(StageName);
            stageObject.transform.position = new Vector3(100f, 0f, 0f);
        }

        OverworldCinematicStage stage = stageObject.GetComponent<OverworldCinematicStage>();
        if (stage == null)
        {
            stage = stageObject.AddComponent<OverworldCinematicStage>();
        }

        Transform rail = FindOrCreateChild(stageObject.transform, "CameraRail");
        rail.localPosition = new Vector3(0f, 3.75f, 0f);
        GameObject subway = GameObject.Find("Subway");
        if (subway == null)
        {
            throw new System.InvalidOperationException("OverworldScene의 Subway 오브젝트를 찾지 못했습니다.");
        }

        subway.transform.SetParent(stageObject.transform, false);
        subway.transform.localPosition = new Vector3(-30f, 0f, 0f);

        Transform cameraTransform = FindOrCreateChild(stageObject.transform, "CinematicCamera_Subway");
        cameraTransform.localPosition = new Vector3(0f, 0f, -1f);
        CinemachineCamera camera = cameraTransform.GetComponent<CinemachineCamera>();
        if (camera == null)
        {
            camera = cameraTransform.gameObject.AddComponent<CinemachineCamera>();
        }

        CinemachineFollow follow = cameraTransform.GetComponent<CinemachineFollow>();
        if (follow == null)
        {
            follow = cameraTransform.gameObject.AddComponent<CinemachineFollow>();
        }

        var trackerSettings = follow.TrackerSettings;
        trackerSettings.PositionDamping = shot.CameraPositionDamping;
        follow.TrackerSettings = trackerSettings;

        camera.Priority = new PrioritySettings { Enabled = true, Value = 100 };
        camera.Lens.ModeOverride = LensSettings.OverrideModes.Orthographic;
        camera.Lens.OrthographicSize = shot.StartOrthographicSize;
        ConfigureStage(stage, camera, rail, subway.transform, shot);
        camera.gameObject.SetActive(false);
        return stage;
    }

    private static void BuildTrigger(ActionSequenceAsset sequence, OverworldCinematicStage stage)
    {
        GameObject triggerObject = GameObject.Find(TriggerName);
        if (triggerObject == null)
        {
            triggerObject = new GameObject(TriggerName);
        }

        SceneActionSequenceTrigger trigger = triggerObject.GetComponent<SceneActionSequenceTrigger>();
        if (trigger == null)
        {
            trigger = triggerObject.AddComponent<SceneActionSequenceTrigger>();
        }

        SerializedObject serialized = new SerializedObject(trigger);
        serialized.FindProperty("_sequence").objectReferenceValue = sequence;
        serialized.FindProperty("_cinematicStage").objectReferenceValue = stage;
        serialized.FindProperty("_initialShotId").stringValue = ShotId;
        serialized.FindProperty("_runOncePerSave").boolValue = true;
        serialized.FindProperty("_completionFlagId").stringValue = "overworld.intro.subway.completed";
        serialized.FindProperty("_setExplorationWhenFinished").boolValue = true;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void BuildGameplayRevealFraming()
    {
        GameObject player = GameObject.Find("Player_Base");
        GameObject zev = GameObject.Find("ZEV");
        GameObject gameplayCameraObject = GameObject.Find("CinemachineCamera");
        if (player == null || zev == null || gameplayCameraObject == null)
        {
            throw new System.InvalidOperationException("게임플레이 reveal 구도에 필요한 Player_Base, ZEV 또는 CinemachineCamera를 찾지 못했습니다.");
        }

        GameObject focusObject = GameObject.Find(GameplayRevealFocusName);
        if (focusObject == null)
        {
            focusObject = new GameObject(GameplayRevealFocusName);
        }

        Vector3 playerPosition = player.transform.position;
        Vector3 zevPosition = zev.transform.position;
        focusObject.transform.position = new Vector3(
            (playerPosition.x + zevPosition.x) * 0.5f,
            (playerPosition.y + zevPosition.y) * 0.5f,
            -1f);

        CinemachineCamera gameplayCamera = gameplayCameraObject.GetComponent<CinemachineCamera>();
        if (gameplayCamera == null)
        {
            throw new System.InvalidOperationException("기본 CinemachineCamera 컴포넌트를 찾지 못했습니다.");
        }

        gameplayCamera.Follow = focusObject.transform;
        gameplayCamera.Lens.ModeOverride = LensSettings.OverrideModes.Orthographic;
        gameplayCamera.Lens.OrthographicSize = CameraLensDefaults.GameplayOrthographicSize;

        SpriteRenderer zevRenderer = zev.GetComponent<SpriteRenderer>();
        if (zevRenderer == null)
        {
            throw new System.InvalidOperationException("ZEV의 SpriteRenderer를 찾지 못했습니다.");
        }

        // The scene backdrop covers ZEV's gameplay coordinates on the Default layer.
        zevRenderer.sortingLayerName = "Default";
        zevRenderer.sortingOrder = 1;
        EditorUtility.SetDirty(gameplayCamera);
        EditorUtility.SetDirty(zevRenderer);
    }

    private static void ConfigureStage(
        OverworldCinematicStage stage,
        CinemachineCamera camera,
        Transform rail,
        Transform subway,
        CinematicShotAsset shot)
    {
        SerializedObject serialized = new SerializedObject(stage);
        serialized.FindProperty("_stageId").stringValue = StageId;
        serialized.FindProperty("_cinematicCamera").objectReferenceValue = camera;

        SerializedProperty subjects = serialized.FindProperty("_subjects");
        subjects.arraySize = 2;
        ConfigureSubject(subjects.GetArrayElementAtIndex(0), "camera_rail", rail);
        ConfigureSubject(subjects.GetArrayElementAtIndex(1), "subway", subway);

        SerializedProperty shots = serialized.FindProperty("_shots");
        shots.arraySize = 1;
        shots.GetArrayElementAtIndex(0).objectReferenceValue = shot;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureSubject(SerializedProperty subject, string subjectId, Transform target)
    {
        subject.FindPropertyRelative("SubjectId").stringValue = subjectId;
        subject.FindPropertyRelative("Target").objectReferenceValue = target;
    }

    private static Transform FindOrCreateChild(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        if (child != null)
        {
            return child;
        }

        var gameObject = new GameObject(name);
        gameObject.transform.SetParent(parent, false);
        return gameObject.transform;
    }

    private static ActionCatalogEntry Entry(
        string actionId,
        string category,
        string displayNameKo,
        string descriptionKo,
        string runtimeAdapterId,
        string exampleYaml,
        params ActionCatalogParameter[] parameters)
    {
        return new ActionCatalogEntry
        {
            ActionId = actionId,
            Category = category,
            DisplayNameKo = displayNameKo,
            DescriptionKo = descriptionKo,
            RuntimeAdapterId = runtimeAdapterId,
            ExampleYaml = exampleYaml,
            Parameters = new List<ActionCatalogParameter>(parameters)
        };
    }

    private static ActionCatalogParameter Param(string name, string type, string displayNameKo, string descriptionKo, bool required)
    {
        return new ActionCatalogParameter
        {
            Name = name,
            Type = type,
            DisplayNameKo = displayNameKo,
            DescriptionKo = descriptionKo,
            Required = required
        };
    }

    private static ScenarioActionData Action(string label, string actionId, string parametersJson)
    {
        return new ScenarioActionData
        {
            DesignerLabel = label,
            ActionId = actionId,
            ParametersJson = parametersJson,
            Note = string.Empty,
            Disabled = false,
            Children = new List<ScenarioActionData>()
        };
    }

    private static T LoadOrCreate<T>(string assetPath) where T : ScriptableObject
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
        if (asset != null)
        {
            return asset;
        }

        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, assetPath);
        return asset;
    }

    private static void EnsureFolderForAsset(string assetPath)
    {
        string directory = Path.GetDirectoryName(assetPath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        string[] parts = directory.Replace('\\', '/').Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }
    }
}
