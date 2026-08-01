using System;
using System.Collections.Generic;
using System.IO;
using DG.Tweening;
using UnityEditor;
using UnityEngine;

public static class TravelTrainContentBuilder
{
    private const string DepartureSourceText = @"id: travel_train.departure
title: ""공용 열차 - 출발""
primaryMode: overworld
openingModule: """"
memoryKey: """"

participants:
  party: []
  enemies: []

sequences:
  travel_train.departure:
    title: ""공용 열차 - 출발""
    description: ""창밖의 흐름과 카메라 이동으로 정류소 사이 출발을 표현한다.""
    usage: ""열차 내부 목적지 장치를 선택한 뒤 재생한다.""
    status: ready
    tags: [overworld, cinematic, travel, train]
    allowedPrimaryModes: [overworld]
    - cinematic.stage.prepare:
      blockId: 8c2e7f9d04b14be6a6e385c6afce0a11
      designerLabel: ""열차 출발 Stage 준비""
      stage: travel_train.departure
      shot: travel_train.departure.run
    - cinematic.shot.play:
      blockId: 92c47ed4c1df4e4a88c30ce167ab1c22
      designerLabel: ""창밖 이동 샷""
      stage: travel_train.departure
      shot: travel_train.departure.run
    - cinematic.stage.release:
      blockId: d41a9c6f30c6463ba8517f718c5ab933
      designerLabel: ""열차 출발 Stage 해제""
      stage: travel_train.departure
";

    public static TravelTrainCoreAssetBundle EnsureCoreRoomAssets(
        TravelTrainPromotionResult promotion)
    {
        if (promotion == null || !promotion.Success)
            throw new InvalidOperationException(promotion?.Error ?? "열차 자산 승격 결과가 없습니다.");

        TravelTrainEditorAssetUtility.EnsureFolder(TravelTrainPaths.PrefabRoot);
        TravelTrainEditorAssetUtility.EnsureFolder(TravelTrainPaths.RoomDataRoot);

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TravelTrainPaths.Prefab);
        if (prefab == null)
        {
            GameObject root = CreateInitialRoomRoot();
            try
            {
                prefab = PrefabUtility.SaveAsPrefabAsset(root, TravelTrainPaths.Prefab);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        RoomDefinition room = TravelTrainEditorAssetUtility.LoadOrCreate<RoomDefinition>(
            TravelTrainPaths.RoomDefinition,
            out _);
        AreaDefinition area = TravelTrainEditorAssetUtility.LoadOrCreate<AreaDefinition>(
            TravelTrainPaths.AreaDefinition,
            out _);
        RoomInstance instance = prefab.GetComponent<RoomInstance>();
        if (instance == null)
            throw new InvalidOperationException("본편 열차 Prefab에 RoomInstance가 없습니다.");

        TravelTrainEditorAssetUtility.Set(instance, "_roomId", p => p.stringValue = TravelTrainIds.Room);
        TravelTrainEditorAssetUtility.Set(room, "_roomId", p => p.stringValue = TravelTrainIds.Room);
        TravelTrainEditorAssetUtility.Set(room, "_roomPrefab", p => p.objectReferenceValue = instance);
        TravelTrainEditorAssetUtility.Set(room, "_areaDefinition", p => p.objectReferenceValue = area);
        TravelTrainEditorAssetUtility.Set(room, "_keepCurrentBgm", p => p.boolValue = true);
        TravelTrainEditorAssetUtility.Set(area, "_areaId", p => p.stringValue = TravelTrainIds.Room);
        TravelTrainEditorAssetUtility.Set(area, "_roomDefinition", p => p.objectReferenceValue = room);
        TravelTrainEditorAssetUtility.Set(
            area,
            "_description",
            p => p.stringValue = "정류소 사이를 이동할 때 공용으로 사용하는 열차 본실입니다.");
        AssetDatabase.SaveAssets();

        return new TravelTrainCoreAssetBundle
        {
            TrainRoomPrefab = prefab,
            TrainRoom = room,
            TrainArea = area
        };
    }

    public static TravelTrainDataBundle Build(
        TravelTrainCoreAssetBundle core,
        RoomDefinition showcaseTrainRoom,
        RoomDefinition wideFieldStationRoom)
    {
        if (core?.TrainRoom == null || showcaseTrainRoom == null || wideFieldStationRoom == null)
            throw new ArgumentNullException(nameof(core), "열차/정류소 RoomDefinition이 모두 필요합니다.");

        EnsureFolders();
        EnsureDepartureSource();

        var data = new TravelTrainDataBundle { Core = core };
        BuildDialogues(data);
        data.DepartureShot = BuildDepartureShot();
        data.DepartureSequence = BuildRuntimeSequence();

        data.ShowcaseStop = TravelTrainEditorAssetUtility.LoadOrCreate<TrainStopDefinition>(
            TravelTrainPaths.ShowcaseStop,
            out _);
        data.ShowcaseStop.Configure(
            TravelTrainIds.ShowcaseStop,
            "쇼케이스 정류소",
            "Region_ShowcaseStation",
            showcaseTrainRoom,
            "from_travel_train",
            FacingDirection.Left,
            "showcase.station.completed",
            1,
            TravelTrainIds.ShowcaseCurrentFlag,
            data.Dialogues["showcase_unavailable"],
            data.Dialogues["showcase_already"]);
        EditorUtility.SetDirty(data.ShowcaseStop);

        data.WideFieldStop = TravelTrainEditorAssetUtility.LoadOrCreate<TrainStopDefinition>(
            TravelTrainPaths.WideFieldStop,
            out _);
        data.WideFieldStop.Configure(
            TravelTrainIds.WideFieldStop,
            "넓은 들판 정류소",
            "Region_WideField",
            wideFieldStationRoom,
            "from_train",
            FacingDirection.Right,
            "showcase.station.completed",
            1,
            TravelTrainIds.WideFieldCurrentFlag,
            data.Dialogues["wide_unavailable"],
            data.Dialogues["wide_already"]);
        EditorUtility.SetDirty(data.WideFieldStop);

        data.Network = TravelTrainEditorAssetUtility.LoadOrCreate<TrainNetworkDefinition>(
            TravelTrainPaths.Network,
            out _);
        data.Network.Configure(
            TravelTrainIds.Network,
            "Region_TravelTrain",
            core.TrainRoom,
            "entry",
            new[] { data.ShowcaseStop, data.WideFieldStop },
            data.DepartureSequence);
        EditorUtility.SetDirty(data.Network);

        if (!data.Network.TryValidateRuntime(out string error))
            throw new InvalidOperationException("Train Network validation failed: " + error);

        AssetDatabase.SaveAssets();
        return data;
    }

    private static GameObject CreateInitialRoomRoot()
    {
        GameObject root = new GameObject("Room_TravelTrainInterior");
        RoomInstance instance = root.AddComponent<RoomInstance>();
        TravelTrainEditorAssetUtility.Set(instance, "_roomId", p => p.stringValue = TravelTrainIds.Room);
        new GameObject("Geometry").transform.SetParent(root.transform, false);
        new GameObject("Props").transform.SetParent(root.transform, false);
        new GameObject("Actors").transform.SetParent(root.transform, false);
        new GameObject("Markers").transform.SetParent(root.transform, false);
        new GameObject("Event Anchors").transform.SetParent(root.transform, false);
        new GameObject("Cinematics").transform.SetParent(root.transform, false);
        new GameObject("Systems").transform.SetParent(root.transform, false);
        return root;
    }

    private static void BuildDialogues(TravelTrainDataBundle data)
    {
        data.Dialogues["showcase_unavailable"] = BuildDialogue(
            "Dialogue_ShowcaseStopUnavailable.asset",
            "* 전력이 안정되기 전에는 이 정류소로 갈 수 없다.");
        data.Dialogues["showcase_already"] = BuildDialogue(
            "Dialogue_ShowcaseStopAlreadyHere.asset",
            "* 지금 열차는 쇼케이스 정류소에 서 있다.");
        data.Dialogues["wide_unavailable"] = BuildDialogue(
            "Dialogue_WideFieldStopUnavailable.asset",
            "* 넓은 들판으로 향하는 노선은 아직 닫혀 있다.");
        data.Dialogues["wide_already"] = BuildDialogue(
            "Dialogue_WideFieldStopAlreadyHere.asset",
            "* 창밖에 넓은 들판 정류소가 보인다.");
        data.Dialogues["conductor_showcase"] = BuildDialogue(
            "Dialogue_Conductor_Showcase.asset",
            "* 쇼케이스 정류소입니다. 준비가 되면 노선 장치를 확인하세요.");
        data.Dialogues["conductor_wide"] = BuildDialogue(
            "Dialogue_Conductor_WideField.asset",
            "* 넓은 들판 정류소입니다. 바람이 강하니 발밑을 조심하세요.");
        data.Dialogues["conductor_default"] = BuildDialogue(
            "Dialogue_Conductor_Default.asset",
            "* 현재 정류소 정보를 확인하고 있습니다.");
        data.Dialogues["window_showcase"] = BuildDialogue(
            "Dialogue_Window_Showcase.asset",
            "* 창밖에 정비등이 켜진 오래된 역이 보인다.");
        data.Dialogues["window_wide"] = BuildDialogue(
            "Dialogue_Window_WideField.asset",
            "* 낮은 풀밭이 선로 끝까지 길게 이어진다.");
        data.Dialogues["window_default"] = BuildDialogue(
            "Dialogue_Window_Default.asset",
            "* 창밖 풍경이 천천히 뒤로 흐른다.");

        data.ConductorDialogue = BuildSelector(
            "ConductorDialogueSelector.asset",
            data.Dialogues["conductor_default"],
            data.Dialogues["conductor_showcase"],
            data.Dialogues["conductor_wide"]);
        data.WindowDialogue = BuildSelector(
            "WindowDialogueSelector.asset",
            data.Dialogues["window_default"],
            data.Dialogues["window_showcase"],
            data.Dialogues["window_wide"]);
    }

    private static DialogueData BuildDialogue(string fileName, string text)
    {
        return TravelTrainEditorAssetUtility.BuildDialogue(
            TravelTrainPaths.DialogueRoot + "/" + fileName,
            text);
    }

    private static FlagDialogueSelector BuildSelector(
        string fileName,
        DialogueData fallback,
        DialogueData showcase,
        DialogueData wide)
    {
        FlagDialogueSelector selector =
            TravelTrainEditorAssetUtility.LoadOrCreate<FlagDialogueSelector>(
                TravelTrainPaths.DialogueRoot + "/" + fileName,
                out _);
        selector.Configure(
            new[]
            {
                new FlagDialogueRule(
                    TravelTrainIds.ShowcaseCurrentFlag,
                    FlagValueComparison.Equal,
                    1,
                    20,
                    showcase),
                new FlagDialogueRule(
                    TravelTrainIds.WideFieldCurrentFlag,
                    FlagValueComparison.Equal,
                    1,
                    10,
                    wide)
            },
            fallback);
        if (!selector.TryValidate(out string error))
            throw new InvalidOperationException("Train dialogue selector invalid: " + error);
        EditorUtility.SetDirty(selector);
        return selector;
    }

    private static CinematicShotAsset BuildDepartureShot()
    {
        CinematicShotAsset shot =
            TravelTrainEditorAssetUtility.LoadOrCreate<CinematicShotAsset>(
                TravelTrainPaths.DepartureShot,
                out _);
        shot.StageId = TravelTrainIds.DepartureSequence;
        shot.ShotId = TravelTrainIds.DepartureShot;
        shot.DisplayNameKo = "공용 열차 출발";
        shot.CameraRailSubjectId = "camera_rail";
        shot.StartOrthographicSize = 4f;
        shot.EndOrthographicSize = 3.6f;
        shot.CameraDelay = 0f;
        shot.CameraDuration = 1.1f;
        shot.CameraEase = Ease.InOutSine;
        shot.CameraPositionDamping = new Vector3(0.12f, 0.08f, 0f);
        shot.Motions = new List<CinematicShotMotion>
        {
            new CinematicShotMotion
            {
                SubjectId = "camera_rail",
                StartLocalPosition = Vector3.zero,
                EndLocalPosition = new Vector3(1.8f, 0.15f, 0f),
                Delay = 0f,
                Duration = 1.1f,
                Ease = Ease.InOutSine
            },
            new CinematicShotMotion
            {
                SubjectId = "window_streaks",
                StartLocalPosition = new Vector3(-3.6f, 0.65f, 0f),
                EndLocalPosition = new Vector3(3.6f, 0.65f, 0f),
                Delay = 0.05f,
                Duration = 0.85f,
                Ease = Ease.InOutQuad
            }
        };
        ScenarioValidationResult validation = shot.ValidateDefinition();
        if (validation.HasErrors)
            throw new InvalidOperationException("Train departure Shot validation failed.");
        EditorUtility.SetDirty(shot);
        return shot;
    }

    private static ActionSequenceAsset BuildRuntimeSequence()
    {
        ActionCatalogAsset catalog = TravelTrainEditorAssetUtility.RequireAsset<ActionCatalogAsset>(
            ProductionActionLibraryBuildCommand.GeneratedAssetPath);
        string sourceText = File.ReadAllText(Path.GetFullPath(TravelTrainPaths.DepartureSource));
        string sourceHash = ScenarioSourceHash.Compute(sourceText);
        ActionSequenceAsset target =
            TravelTrainEditorAssetUtility.LoadOrCreate<ActionSequenceAsset>(
                TravelTrainPaths.DepartureRuntime,
                out bool created);
        if (!created
            && target.Source != null
            && string.Equals(
                TravelTrainEditorAssetUtility.NormalizeAssetPath(target.Source.SourcePath),
                TravelTrainPaths.DepartureSource,
                StringComparison.Ordinal)
            && string.Equals(target.Source.SourceHash, sourceHash, StringComparison.Ordinal)
            && !ScenarioCatalogValidator.ValidateSequence(target, catalog).HasErrors)
        {
            return target;
        }

        ActionSequenceSourceRuntimeAssetReimportResult result =
            ActionSequenceSourceSync.ReimportFromText(
                target,
                sourceText,
                TravelTrainPaths.DepartureSource,
                catalog,
                "overworld");
        if (!result.Success)
        {
            if (created)
                AssetDatabase.DeleteAsset(TravelTrainPaths.DepartureRuntime);
            throw new InvalidOperationException("Train sequence import failed.");
        }

        EditorUtility.SetDirty(target);
        return target;
    }

    private static void EnsureDepartureSource()
    {
        TravelTrainEditorAssetUtility.EnsureFolder(TravelTrainPaths.SourceRoot);
        string fullPath = Path.GetFullPath(TravelTrainPaths.DepartureSource);
        if (!File.Exists(fullPath))
            File.WriteAllText(fullPath, DepartureSourceText, new System.Text.UTF8Encoding(false));
        AssetDatabase.ImportAsset(
            TravelTrainPaths.DepartureSource,
            ImportAssetOptions.ForceSynchronousImport);
    }

    private static void EnsureFolders()
    {
        TravelTrainEditorAssetUtility.EnsureFolder(TravelTrainPaths.StopDataRoot);
        TravelTrainEditorAssetUtility.EnsureFolder(TravelTrainPaths.DialogueRoot);
        TravelTrainEditorAssetUtility.EnsureFolder(TravelTrainPaths.RuntimeRoot);
        TravelTrainEditorAssetUtility.EnsureFolder(TravelTrainPaths.CinematicRoot);
    }
}