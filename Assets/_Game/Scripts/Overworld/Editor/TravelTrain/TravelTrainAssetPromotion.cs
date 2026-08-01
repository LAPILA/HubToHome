using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum TravelTrainPromotionStatus
{
    Promoted,
    AlreadyPromoted,
    InitialCreationRequired,
    Failed
}

public sealed class TravelTrainPromotionPaths
{
    public string OldPrefab;
    public string NewPrefab;
    public string OldRoomDefinition;
    public string NewRoomDefinition;
    public string OldAreaDefinition;
    public string NewAreaDefinition;
    public string LegacyScene;

    public static TravelTrainPromotionPaths Default => new TravelTrainPromotionPaths
    {
        OldPrefab = ShowcaseStationPaths.PrefabRoot + "/Room_ShowcaseCabin.prefab",
        NewPrefab = TravelTrainPaths.Prefab,
        OldRoomDefinition = ShowcaseStationPaths.RoomDataRoot + "/Room_ShowcaseCabin_Definition.asset",
        NewRoomDefinition = TravelTrainPaths.RoomDefinition,
        OldAreaDefinition = ShowcaseStationPaths.RoomDataRoot + "/Room_ShowcaseCabin_Area.asset",
        NewAreaDefinition = TravelTrainPaths.AreaDefinition,
        LegacyScene = ShowcaseStationScenePaths.SceneRoot + "/Sublocation_ShowcaseCabin.unity"
    };
}

public sealed class TravelTrainPromotionResult
{
    public TravelTrainPromotionStatus Status;
    public string Error = string.Empty;
    public TravelTrainPromotionPaths Paths;
    public bool Success => Status != TravelTrainPromotionStatus.Failed;
}

public static class TravelTrainAssetPromotion
{
    public static TravelTrainPromotionResult PromoteCoreAssets(
        TravelTrainPromotionPaths paths)
    {
        paths ??= TravelTrainPromotionPaths.Default;
        var result = new TravelTrainPromotionResult { Paths = paths };
        if (!TryClassify(paths, out TravelTrainPromotionStatus status, out string error))
        {
            result.Status = TravelTrainPromotionStatus.Failed;
            result.Error = error;
            return result;
        }

        if (status != TravelTrainPromotionStatus.Promoted)
        {
            result.Status = status;
            return result;
        }

        if (!TravelWorldBuildPreflight.ValidateNoDirtyOwnedContent(out error))
        {
            result.Status = TravelTrainPromotionStatus.Failed;
            result.Error = error;
            return result;
        }

        var pairs = new[]
        {
            (paths.OldPrefab, paths.NewPrefab),
            (paths.OldRoomDefinition, paths.NewRoomDefinition),
            (paths.OldAreaDefinition, paths.NewAreaDefinition)
        };
        var moved = new List<(string OldPath, string NewPath)>();
        try
        {
            TravelTrainEditorAssetUtility.EnsureFolder(TravelTrainPaths.PrefabRoot);
            TravelTrainEditorAssetUtility.EnsureFolder(TravelTrainPaths.RoomDataRoot);
            for (int i = 0; i < pairs.Length; i++)
            {
                string moveError = AssetDatabase.MoveAsset(pairs[i].Item1, pairs[i].Item2);
                if (!string.IsNullOrEmpty(moveError))
                    throw new InvalidOperationException(moveError);
                moved.Add((pairs[i].Item1, pairs[i].Item2));
            }

            AssetDatabase.ImportAsset(paths.NewPrefab, ImportAssetOptions.ForceSynchronousImport);
            MigrateIdentity(paths);
            AssetDatabase.SaveAssets();
            result.Status = TravelTrainPromotionStatus.Promoted;
            return result;
        }
        catch (Exception exception)
        {
            var rollbackErrors = new List<string>();
            for (int i = moved.Count - 1; i >= 0; i--)
            {
                string rollbackError = AssetDatabase.MoveAsset(
                    moved[i].NewPath,
                    moved[i].OldPath);
                if (!string.IsNullOrEmpty(rollbackError))
                    rollbackErrors.Add(rollbackError);
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            result.Status = TravelTrainPromotionStatus.Failed;
            result.Error = exception.Message;
            if (rollbackErrors.Count > 0)
                result.Error += " | rollback: " + string.Join(" | ", rollbackErrors);
            return result;
        }
    }

    public static bool RemoveLegacySceneAfterValidation(
        TravelTrainPromotionPaths paths,
        out string error)
    {
        paths ??= TravelTrainPromotionPaths.Default;
        Scene loaded = FindLoadedScene(paths.LegacyScene);
        if (loaded.IsValid() && loaded.isDirty)
        {
            error = "기존 Cabin Scene에 저장되지 않은 변경이 있습니다: " + paths.LegacyScene;
            return false;
        }

        EditorBuildSettingsScene[] before = EditorBuildSettings.scenes;
        try
        {
            EditorBuildSettings.scenes = before
                .Where(scene => !string.Equals(
                    Normalize(scene.path),
                    Normalize(paths.LegacyScene),
                    StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(paths.LegacyScene) != null
                && !AssetDatabase.DeleteAsset(paths.LegacyScene))
            {
                throw new InvalidOperationException(
                    "기존 Cabin Scene 삭제에 실패했습니다: " + paths.LegacyScene);
            }

            error = string.Empty;
            return true;
        }
        catch (Exception exception)
        {
            EditorBuildSettings.scenes = before;
            error = exception.Message;
            return false;
        }
    }

    private static bool TryClassify(
        TravelTrainPromotionPaths paths,
        out TravelTrainPromotionStatus status,
        out string error)
    {
        bool[] oldExists =
        {
            Exists(paths.OldPrefab),
            Exists(paths.OldRoomDefinition),
            Exists(paths.OldAreaDefinition)
        };
        bool[] newExists =
        {
            Exists(paths.NewPrefab),
            Exists(paths.NewRoomDefinition),
            Exists(paths.NewAreaDefinition)
        };

        bool allOld = oldExists.All(value => value);
        bool noOld = oldExists.All(value => !value);
        bool allNew = newExists.All(value => value);
        bool noNew = newExists.All(value => !value);

        if (allOld && noNew)
        {
            status = TravelTrainPromotionStatus.Promoted;
            error = string.Empty;
            return true;
        }
        if (noOld && allNew)
        {
            status = TravelTrainPromotionStatus.AlreadyPromoted;
            error = string.Empty;
            return true;
        }
        if (noOld && noNew)
        {
            status = TravelTrainPromotionStatus.InitialCreationRequired;
            error = string.Empty;
            return true;
        }

        status = TravelTrainPromotionStatus.Failed;
        error = "기존 Cabin과 본편 열차 자산 경로가 부분적으로 점유되어 자동 승격할 수 없습니다.";
        return false;
    }

    private static void MigrateIdentity(TravelTrainPromotionPaths paths)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(paths.NewPrefab);
        try
        {
            root.name = "Room_TravelTrainInterior";
            RoomInstance room = root.GetComponent<RoomInstance>();
            if (room == null)
                room = root.AddComponent<RoomInstance>();
            TravelTrainEditorAssetUtility.Set(
                room,
                "_roomId",
                property => property.stringValue = TravelTrainIds.Room);

            SignMarker[] signs = root.GetComponentsInChildren<SignMarker>(true);
            SignMarker note = signs.SingleOrDefault(
                marker => string.Equals(
                    marker.MarkerId,
                    "showcase.cabin.travel_note",
                    StringComparison.Ordinal));
            if (note != null)
                ConfigureMarkerIdentity(note, "travel_note", "여행 기록");

            SublocationMarker[] legacy = root.GetComponentsInChildren<SublocationMarker>(true);
            SublocationMarker returnMarker = legacy.SingleOrDefault(
                marker => string.Equals(
                    marker.MarkerId,
                    "showcase.cabin.return_to_train",
                    StringComparison.Ordinal));
            if (returnMarker != null)
            {
                GameObject owner = returnMarker.gameObject;
                UnityEngine.Object.DestroyImmediate(returnMarker, true);
                TrainExitMarker exit = owner.GetComponent<TrainExitMarker>();
                if (exit == null)
                    exit = owner.AddComponent<TrainExitMarker>();
                ConfigureMarkerIdentity(exit, "train_exit", "현재 정류소로 내리기");
            }

            if (root.GetComponentsInChildren<SublocationMarker>(true).Length > 0)
                throw new InvalidOperationException("승격된 열차 Prefab에 legacy SublocationMarker가 남아 있습니다.");

            PrefabUtility.SaveAsPrefabAsset(root, paths.NewPrefab);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        RoomDefinition roomDefinition =
            TravelTrainEditorAssetUtility.RequireAsset<RoomDefinition>(paths.NewRoomDefinition);
        AreaDefinition areaDefinition =
            TravelTrainEditorAssetUtility.RequireAsset<AreaDefinition>(paths.NewAreaDefinition);
        RoomInstance prefab =
            TravelTrainEditorAssetUtility.RequireAsset<GameObject>(paths.NewPrefab)
                .GetComponent<RoomInstance>();
        if (prefab == null)
            throw new InvalidOperationException("승격된 열차 Prefab에 RoomInstance가 없습니다.");

        roomDefinition.name = "Room_TravelTrainInterior_Definition";
        areaDefinition.name = "Room_TravelTrainInterior_Area";
        TravelTrainEditorAssetUtility.Set(roomDefinition, "_roomId", p => p.stringValue = TravelTrainIds.Room);
        TravelTrainEditorAssetUtility.Set(roomDefinition, "_roomPrefab", p => p.objectReferenceValue = prefab);
        TravelTrainEditorAssetUtility.Set(roomDefinition, "_areaDefinition", p => p.objectReferenceValue = areaDefinition);
        TravelTrainEditorAssetUtility.Set(areaDefinition, "_areaId", p => p.stringValue = TravelTrainIds.Room);
        TravelTrainEditorAssetUtility.Set(areaDefinition, "_roomDefinition", p => p.objectReferenceValue = roomDefinition);
        TravelTrainEditorAssetUtility.Set(
            areaDefinition,
            "_description",
            p => p.stringValue = "정류소 사이를 이동할 때 공용으로 사용하는 열차 본실입니다.");
    }

    private static void ConfigureMarkerIdentity(
        AreaMarkerBase marker,
        string suffix,
        string displayName)
    {
        var serialized = new SerializedObject(marker);
        serialized.FindProperty("markerId").stringValue = TravelTrainIds.Room + "." + suffix;
        serialized.FindProperty("areaId").stringValue = TravelTrainIds.Room;
        serialized.FindProperty("displayName").stringValue = displayName;
        serialized.FindProperty("isOneShot").boolValue = false;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static bool Exists(string path)
    {
        return AssetDatabase.LoadMainAssetAtPath(path) != null;
    }

    private static Scene FindLoadedScene(string path)
    {
        string normalized = Normalize(path);
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (string.Equals(Normalize(scene.path), normalized, StringComparison.OrdinalIgnoreCase))
                return scene;
        }
        return default;
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Replace('\\', '/').Trim();
    }
}

public static class TravelWorldBuildPreflight
{
    private static readonly string[] OwnedScenePaths =
    {
        ShowcaseStationScenePaths.MainScene,
        ShowcaseStationScenePaths.SceneRoot + "/Sublocation_ShowcaseCabin.unity",
        TravelTrainPaths.Scene,
        WideFieldPaths.Scene
    };

    public static bool SaveGeneratedChangesInOwnedScenes(out string error)
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.IsValid() || !scene.isLoaded || !scene.isDirty || !IsOwnedScene(scene.path))
                continue;
            if (!EditorSceneManager.SaveScene(scene, scene.path, false))
            {
                error = "자동 생성 대상 Scene 저장에 실패했습니다: " + scene.path;
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    public static bool ValidateNoDirtyOwnedContent(out string error)
    {
        PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
        if (stage != null && stage.scene.IsValid() && stage.scene.isDirty)
        {
            string assetPath = AssetDatabase.GetAssetPath(stage.prefabContentsRoot);
            if (string.Equals(assetPath, TravelTrainPaths.Prefab, StringComparison.OrdinalIgnoreCase)
                || string.Equals(
                    assetPath,
                    ShowcaseStationPaths.PrefabRoot + "/Room_ShowcaseCabin.prefab",
                    StringComparison.OrdinalIgnoreCase))
            {
                error = "열차 Prefab Stage에 저장되지 않은 변경이 있습니다: " + assetPath;
                return false;
            }
        }

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.IsValid() || !scene.isLoaded || !scene.isDirty)
                continue;
            if (IsOwnedScene(scene.path))
            {
                error = "자동 생성 대상 Scene에 저장되지 않은 변경이 있습니다: " + scene.path;
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    private static bool IsOwnedScene(string path)
    {
        string normalized = string.IsNullOrWhiteSpace(path)
            ? string.Empty
            : path.Replace('\\', '/');
        for (int i = 0; i < OwnedScenePaths.Length; i++)
        {
            if (string.Equals(normalized, OwnedScenePaths[i], StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
