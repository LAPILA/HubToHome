using System;
using System.Collections.Generic;
using System.IO;
using DG.Tweening;
using UnityEditor;
using UnityEngine;

public sealed class ShowcaseStationDataBundle
{
    public readonly Dictionary<string, RoomDefinition> Rooms =
        new Dictionary<string, RoomDefinition>(StringComparer.Ordinal);

    public ItemData SmallPotion;
    public EnemyData SteamEnemy;
    public ShopDefinition WorkshopShop;
    public SequencePuzzleDefinition WorkshopPuzzle;
    public FlagDialogueSelector StationNpcDialogue;
    public DialogueData IntroDialogue;
    public DialogueData FinaleDialogue;
    public DialogueData PuzzleGuideDialogue;
    public DialogueData PowerLockedDialogue;
    public ActionSequenceAsset IntroSequence;
    public ActionSequenceAsset FinaleSequence;
    public CinematicShotAsset FinalePowerShot;
    public CinematicShotAsset FinaleDepartureShot;
}

public static class ShowcaseStationDataBuilder
{
    public static ShowcaseStationDataBundle Build()
    {
        EnsureFolders();
        ValidateDependencies();

        var data = new ShowcaseStationDataBundle
        {
            SmallPotion = RequireAsset<ItemData>(ShowcaseStationPaths.SmallPotion),
            IntroDialogue = BuildDialogue(
                "Dialogue_IntroArrival.asset",
                "* 열차가 멈췄다. 역 안의 전력을 복구할 방법을 찾아보자."),
            FinaleDialogue = BuildDialogue(
                "Dialogue_FinalePowerRestored.asset",
                "* 정지했던 장치가 숨을 돌리듯 움직이기 시작한다."),
            PuzzleGuideDialogue = BuildDialogue(
                "Dialogue_PuzzleGuide.asset",
                "* 세 단자를 표시된 순서대로 작동시켜야 한다."),
            PowerLockedDialogue = BuildDialogue(
                "Dialogue_PowerLocked.asset",
                "* 전력이 부족하다. 정비 공방의 단자부터 확인해야 한다.")
        };

        DialogueData npcDefault = BuildDialogue(
            "Dialogue_StationNpc_Default.asset",
            "* 처음 보는 얼굴이네. 멈춘 열차 때문에 모두 발이 묶였어."),
            npcStarted = BuildDialogue(
                "Dialogue_StationNpc_Started.asset",
                "* 공방의 단자를 복구하면 증기 통로를 지나갈 수 있을 거야."),
            npcPowered = BuildDialogue(
                "Dialogue_StationNpc_Powered.asset",
                "* 통로가 열렸어. 폐열차의 전원 콘솔을 확인해 줘."),
            npcCompleted = BuildDialogue(
                "Dialogue_StationNpc_Completed.asset",
                "* 열차가 다시 움직이네. 덕분에 이 역도 한숨 돌리겠어.");

        data.StationNpcDialogue = LoadOrCreate<FlagDialogueSelector>(
            ShowcaseStationPaths.DialogueRoot + "/StationNpcDialogueSelector.asset",
            out _);
        data.StationNpcDialogue.Configure(
            new[]
            {
                new FlagDialogueRule(
                    "showcase.station.completed",
                    FlagValueComparison.Equal,
                    1,
                    30,
                    npcCompleted),
                new FlagDialogueRule(
                    "showcase.station.power_restored",
                    FlagValueComparison.Equal,
                    1,
                    20,
                    npcPowered),
                new FlagDialogueRule(
                    "showcase.station.started",
                    FlagValueComparison.Equal,
                    1,
                    10,
                    npcStarted)
            },
            npcDefault);
        EditorUtility.SetDirty(data.StationNpcDialogue);

        data.WorkshopShop = LoadOrCreate<ShopDefinition>(
            ShowcaseStationPaths.ShopRoot + "/Shop_Workshop.asset",
            out _);
        data.WorkshopShop.Configure(
            "showcase.workshop.supplies",
            "정비 공방 보급품",
            new[]
            {
                new ShopEntry(
                    "small_potion",
                    data.SmallPotion,
                    25,
                    1,
                    5,
                    "shop.showcase.workshop.small_potion.purchases")
            });
        if (!data.WorkshopShop.TryValidate(out string shopError))
            throw new InvalidOperationException("Showcase Shop validation failed: " + shopError);
        EditorUtility.SetDirty(data.WorkshopShop);

        data.WorkshopPuzzle = LoadOrCreate<SequencePuzzleDefinition>(
            ShowcaseStationPaths.PuzzleRoot + "/Puzzle_WorkshopPower.asset",
            out _);
        data.WorkshopPuzzle.Configure(
            "showcase.workshop.power_sequence",
            new[] { "terminal.a", "terminal.b", "terminal.c" },
            "showcase.station.power_restored",
            0.65f);
        if (!data.WorkshopPuzzle.TryValidate(out string puzzleError))
            throw new InvalidOperationException("Showcase Puzzle validation failed: " + puzzleError);
        EditorUtility.SetDirty(data.WorkshopPuzzle);

        BuildFinaleShots(data);
        data.SteamEnemy = BuildEnemy();
        EnsureRuntimeCatalogContains(data.SteamEnemy);
        data.IntroSequence = BuildRuntimeSequence(
            ShowcaseStationPaths.IntroRuntime,
            ShowcaseStationPaths.IntroSource);
        data.FinaleSequence = BuildRuntimeSequence(
            ShowcaseStationPaths.FinaleRuntime,
            ShowcaseStationPaths.FinaleSource);

        BuildRoomData(data);
        AssetDatabase.SaveAssets();
        return data;
    }

    private static void BuildRoomData(ShowcaseStationDataBundle data)
    {
        for (int i = 0; i < ShowcaseStationIds.GeneratedRoomIds.Length; i++)
        {
            string roomId = ShowcaseStationIds.GeneratedRoomIds[i];
            string assetStem = RoomAssetStem(roomId);
            RoomDefinition definition = LoadOrCreate<RoomDefinition>(
                ShowcaseStationPaths.RoomDataRoot + "/" + assetStem + "_Definition.asset",
                out _);
            AreaDefinition area = LoadOrCreate<AreaDefinition>(
                ShowcaseStationPaths.RoomDataRoot + "/" + assetStem + "_Area.asset",
                out _);

            SetObject(definition, "_roomId", roomId);
            SetObject(definition, "_areaDefinition", area);
            SetObject(definition, "_keepCurrentBgm", true);
            SetObject(area, "_areaId", roomId);
            SetObject(area, "_roomDefinition", definition);
            SetObject(area, "_description", "Showcase Station 기능 시연용 Room입니다.");
            EditorUtility.SetDirty(definition);
            EditorUtility.SetDirty(area);
            data.Rooms[roomId] = definition;
        }
    }

    private static DialogueData BuildDialogue(string fileName, string text)
    {
        DialogueData dialogue = LoadOrCreate<DialogueData>(
            ShowcaseStationPaths.DialogueRoot + "/" + fileName,
            out _);
        dialogue.Style = DialogueStyle.Overworld;
        dialogue.Nodes.Clear();
        dialogue.Nodes.Add(new DialogueNode
        {
            Emotion = EmotionType.Normal,
            DefaultText = text
        });
        EditorUtility.SetDirty(dialogue);
        return dialogue;
    }

    private static void BuildFinaleShots(ShowcaseStationDataBundle data)
    {
        data.FinalePowerShot = LoadOrCreate<CinematicShotAsset>(
            ShowcaseStationPaths.FinalePowerShot,
            out _);
        data.FinalePowerShot.StageId = "showcase.station.finale";
        data.FinalePowerShot.ShotId = "showcase.station.finale.power";
        data.FinalePowerShot.DisplayNameKo = "폐열차 전원 복구";
        data.FinalePowerShot.CameraRailSubjectId = "camera_rail";
        data.FinalePowerShot.StartOrthographicSize = 4.6f;
        data.FinalePowerShot.EndOrthographicSize = 3.15f;
        data.FinalePowerShot.CameraDelay = 0f;
        data.FinalePowerShot.CameraDuration = 1.2f;
        data.FinalePowerShot.CameraEase = Ease.InOutSine;
        data.FinalePowerShot.CameraPositionDamping = new Vector3(0.12f, 0.12f, 0f);
        data.FinalePowerShot.Motions = new List<CinematicShotMotion>
        {
            new CinematicShotMotion
            {
                SubjectId = "camera_rail",
                StartLocalPosition = new Vector3(1.2f, -0.1f, 0f),
                EndLocalPosition = new Vector3(0.2f, 0.55f, 0f),
                Duration = 1.2f,
                Ease = Ease.InOutSine
            },
            new CinematicShotMotion
            {
                SubjectId = "power_pulse",
                StartLocalPosition = new Vector3(1.2f, -0.4f, 0f),
                EndLocalPosition = new Vector3(0f, 1.45f, 0f),
                Delay = 0.18f,
                Duration = 0.72f,
                Ease = Ease.OutCubic
            }
        };
        ValidateAndDirtyShot(data.FinalePowerShot);

        data.FinaleDepartureShot = LoadOrCreate<CinematicShotAsset>(
            ShowcaseStationPaths.FinaleDepartureShot,
            out _);
        data.FinaleDepartureShot.StageId = "showcase.station.finale";
        data.FinaleDepartureShot.ShotId = "showcase.station.finale.departure";
        data.FinaleDepartureShot.DisplayNameKo = "폐열차 기동";
        data.FinaleDepartureShot.CameraRailSubjectId = "camera_rail";
        data.FinaleDepartureShot.StartOrthographicSize = 3.15f;
        data.FinaleDepartureShot.EndOrthographicSize = 4.35f;
        data.FinaleDepartureShot.CameraDelay = 0f;
        data.FinaleDepartureShot.CameraDuration = 1.35f;
        data.FinaleDepartureShot.CameraEase = Ease.InOutSine;
        data.FinaleDepartureShot.CameraPositionDamping = new Vector3(0.18f, 0.12f, 0f);
        data.FinaleDepartureShot.Motions = new List<CinematicShotMotion>
        {
            new CinematicShotMotion
            {
                SubjectId = "camera_rail",
                StartLocalPosition = new Vector3(0.2f, 0.55f, 0f),
                EndLocalPosition = new Vector3(2.65f, 0.25f, 0f),
                Duration = 1.35f,
                Ease = Ease.InOutSine
            },
            new CinematicShotMotion
            {
                SubjectId = "power_pulse",
                StartLocalPosition = new Vector3(0f, 1.45f, 0f),
                EndLocalPosition = new Vector3(0f, 5.5f, 0f),
                Delay = 0.15f,
                Duration = 0.7f,
                Ease = Ease.InCubic
            },
            new CinematicShotMotion
            {
                SubjectId = "steam_left",
                StartLocalPosition = new Vector3(1.05f, 0.45f, 0f),
                EndLocalPosition = new Vector3(-4.8f, 1.3f, 0f),
                Delay = 0.12f,
                Duration = 1.05f,
                Ease = Ease.OutQuad
            },
            new CinematicShotMotion
            {
                SubjectId = "steam_right",
                StartLocalPosition = new Vector3(1.35f, 0.45f, 0f),
                EndLocalPosition = new Vector3(5.4f, 1.25f, 0f),
                Delay = 0.12f,
                Duration = 1.05f,
                Ease = Ease.OutQuad
            }
        };
        ValidateAndDirtyShot(data.FinaleDepartureShot);
    }

    private static void ValidateAndDirtyShot(CinematicShotAsset shot)
    {
        ScenarioValidationResult validation = shot.ValidateDefinition();
        if (validation.HasErrors)
        {
            var messages = new List<string>();
            for (int i = 0; i < validation.Messages.Count; i++)
            {
                ScenarioValidationMessage message = validation.Messages[i];
                if (message.Severity == ScenarioValidationSeverity.Error)
                    messages.Add(message.Code + ": " + message.Message);
            }

            throw new InvalidOperationException(
                "Showcase Cinematic Shot validation failed: "
                + string.Join(" | ", messages));
        }

        EditorUtility.SetDirty(shot);
    }
    private static EnemyData BuildEnemy()
    {
        EnemyData source = RequireAsset<EnemyData>(ShowcaseStationPaths.SlimeEnemy);
        EnemyData target = LoadOrCreate<EnemyData>(
            ShowcaseStationPaths.EncounterRoot + "/Enemy_SteamWisp.asset",
            out _);
        EditorUtility.CopySerialized(source, target);
        target.name = "Enemy_SteamWisp";
        target.EnemyId = "showcase.steam_wisp";
        target.EnemyName = "Steam Wisp";
        target.ThreatLevel = Mathf.Max(1, source.ThreatLevel);
        target.AllowInstantKillAfterDefeat = true;
        target.InstantKillLevelGap = 2;
        target.EXPReward = Mathf.Max(1, source.EXPReward);
        target.GoldReward = Mathf.Max(1, source.GoldReward);
        EditorUtility.SetDirty(target);
        return target;
    }

    private static void EnsureRuntimeCatalogContains(EnemyData enemy)
    {
        if (enemy == null)
            throw new ArgumentNullException(nameof(enemy));

        GameContentCatalog catalog = RequireAsset<GameContentCatalog>(
            AssetDatabaseContentSource.DefaultCatalogAssetPath);
        bool found = false;
        bool changed = false;
        for (int i = 0; i < catalog.Enemies.Count; i++)
        {
            if (catalog.Enemies[i] != enemy)
                continue;
            if (!found)
            {
                found = true;
                continue;
            }

            catalog.Enemies.RemoveAt(i);
            i--;
            changed = true;
        }

        if (!found)
        {
            string enemyPath = AssetDatabase.GetAssetPath(enemy);
            int insertionIndex = catalog.Enemies.Count;
            for (int i = 0; i < catalog.Enemies.Count; i++)
            {
                string existingPath = AssetDatabase.GetAssetPath(catalog.Enemies[i]);
                if (string.CompareOrdinal(existingPath, enemyPath) <= 0)
                    continue;
                insertionIndex = i;
                break;
            }
            catalog.Enemies.Insert(insertionIndex, enemy);
            changed = true;
        }

        if (!changed)
            return;
        EditorUtility.SetDirty(catalog);
        GameContentCatalog.InvalidateRuntimeCache();
    }

    private static ActionSequenceAsset BuildRuntimeSequence(
        string runtimePath,
        string sourcePath)
    {
        ActionCatalogAsset catalog = RequireAsset<ActionCatalogAsset>(
            ProductionActionLibraryBuildCommand.GeneratedAssetPath);
        string sourceText = File.ReadAllText(Path.GetFullPath(sourcePath));
        string sourceHash = ScenarioSourceHash.Compute(sourceText);
        bool created;
        ActionSequenceAsset target = LoadOrCreate<ActionSequenceAsset>(runtimePath, out created);
        if (!created
            && target.Source != null
            && string.Equals(
                NormalizeAssetPath(target.Source.SourcePath),
                NormalizeAssetPath(sourcePath),
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
                sourcePath,
                catalog,
                "overworld");
        if (!result.Success)
        {
            if (created)
                AssetDatabase.DeleteAsset(runtimePath);
            throw new InvalidOperationException(
                "Showcase sequence import failed: " + FormatValidation(result.Validation));
        }

        if (target.Source == null
            || target.Source.SourceHash != sourceHash)
        {
            throw new InvalidOperationException(
                "Showcase sequence source hash mismatch: " + sourcePath);
        }

        EditorUtility.SetDirty(target);
        return target;
    }

    private static void ValidateScenarioSource(string sourcePath)
    {
        string sourceText = File.ReadAllText(Path.GetFullPath(sourcePath));
        ActionSequenceSourceImportResult importResult =
            ActionSequenceSourceSync.Import(sourceText, sourcePath);
        try
        {
            ScenarioValidationResult validation = importResult.Validation
                ?? new ScenarioValidationResult();
            if (importResult.Sequence != null)
            {
                validation.Merge(ScenarioCatalogValidator.ValidateSequence(
                    importResult.Sequence,
                    RequireAsset<ActionCatalogAsset>(
                        ProductionActionLibraryBuildCommand.GeneratedAssetPath)));
            }

            if (importResult.Sequence == null || validation.HasErrors)
            {
                throw new InvalidOperationException(
                    "Showcase sequence prevalidation failed: "
                    + FormatValidation(validation));
            }
        }
        finally
        {
            if (importResult.Sequence != null)
                UnityEngine.Object.DestroyImmediate(importResult.Sequence);
        }
    }
    private static void ValidateDependencies()
    {
        RequireAsset<Sprite>(ShowcaseStationPaths.SharedWhiteSprite);
        RequireAsset<Sprite>(ShowcaseStationPaths.TestNpcSprite);
        RequireAsset<ItemData>(ShowcaseStationPaths.SmallPotion);
        RequireAsset<EnemyData>(ShowcaseStationPaths.SlimeEnemy);
        RequireAsset<GameObject>(ShowcaseStationPaths.EnemyBasePrefab);
        RequireAsset<GameObject>(ShowcaseStationPaths.PlayerBasePrefab);
        RequireAsset<GameObject>(ShowcaseStationPaths.SeamlessBattleHostPrefab);
        RequireAsset<ActionCatalogAsset>(ProductionActionLibraryBuildCommand.GeneratedAssetPath);
        RequireFile(ShowcaseStationPaths.IntroSource);
        RequireFile(ShowcaseStationPaths.FinaleSource);
    }

    private static T RequireAsset<T>(string path) where T : UnityEngine.Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null)
            throw new InvalidOperationException("Required Showcase asset is missing: " + path);
        return asset;
    }

    private static void RequireFile(string path)
    {
        if (!File.Exists(Path.GetFullPath(path)))
            throw new InvalidOperationException("Required Showcase source is missing: " + path);
    }

    private static T LoadOrCreate<T>(string path, out bool created)
        where T : ScriptableObject
    {
        UnityEngine.Object existing = AssetDatabase.LoadMainAssetAtPath(path);
        if (existing != null && !(existing is T))
        {
            throw new InvalidOperationException(
                $"Showcase asset path is occupied by {existing.GetType().Name}, expected {typeof(T).Name}: {path}");
        }

        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null)
        {
            created = false;
            return asset;
        }

        EnsureFolder(Path.GetDirectoryName(path)?.Replace('\\', '/'));
        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        created = true;
        return asset;
    }

    private static string NormalizeAssetPath(string path)
    {
        return string.IsNullOrWhiteSpace(path)
            ? string.Empty
            : path.Replace('\\', '/').Trim();
    }
    private static void EnsureFolders()
    {
        EnsureFolder(ShowcaseStationPaths.PrefabRoot);
        EnsureFolder(ShowcaseStationPaths.RoomDataRoot);
        EnsureFolder(ShowcaseStationPaths.DialogueRoot);
        EnsureFolder(ShowcaseStationPaths.ShopRoot);
        EnsureFolder(ShowcaseStationPaths.PuzzleRoot);
        EnsureFolder(ShowcaseStationPaths.EncounterRoot);
        EnsureFolder(ShowcaseStationPaths.CinematicRoot);
        EnsureFolder(ShowcaseStationPaths.RuntimeSequenceRoot);
    }

    internal static void EnsureFolder(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || AssetDatabase.IsValidFolder(folderPath))
            return;

        string parent = Path.GetDirectoryName(folderPath)?.Replace('\\', '/');
        string name = Path.GetFileName(folderPath);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }

    internal static string RoomAssetStem(string roomId)
    {
        if (string.Equals(roomId, ShowcaseStationIds.Arrival, StringComparison.Ordinal))
            return "Room_ArrivalPlatform";
        if (string.Equals(roomId, ShowcaseStationIds.Square, StringComparison.Ordinal))
            return "Room_LanternSquare";
        if (string.Equals(roomId, ShowcaseStationIds.Workshop, StringComparison.Ordinal))
            return "Room_Workshop";
        if (string.Equals(roomId, ShowcaseStationIds.Passage, StringComparison.Ordinal))
            return "Room_SteamPassage";
        if (string.Equals(roomId, ShowcaseStationIds.Train, StringComparison.Ordinal))
            return "Room_AbandonedTrain";

        throw new ArgumentOutOfRangeException(nameof(roomId), roomId, "Unknown Showcase room ID.");
    }

    internal static void SetObject(UnityEngine.Object target, string propertyName, object value)
    {
        var serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
            throw new InvalidOperationException(
                $"Serialized property '{propertyName}' was not found on {target.GetType().Name}.");

        switch (value)
        {
            case string stringValue:
                property.stringValue = stringValue;
                break;
            case bool boolValue:
                property.boolValue = boolValue;
                break;
            case int intValue:
                property.intValue = intValue;
                break;
            case float floatValue:
                property.floatValue = floatValue;
                break;
            case UnityEngine.Object objectValue:
                property.objectReferenceValue = objectValue;
                break;
            case null:
                property.objectReferenceValue = null;
                break;
            default:
                throw new InvalidOperationException(
                    $"Unsupported serialized value type: {value.GetType().Name}");
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static string FormatValidation(ScenarioValidationResult validation)
    {
        if (validation == null || validation.Messages == null)
            return "No validation details.";
        return string.Join(" | ", validation.Messages.ConvertAll(
            message => message.Code + ": " + message.Message));
    }
}