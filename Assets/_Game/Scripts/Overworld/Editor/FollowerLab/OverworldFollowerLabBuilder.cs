using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

/// <summary>Explicit, create-only development fixture. Never regenerates an authored room.</summary>
public static class OverworldFollowerLabBuilder
{
    public const string Root = "Assets/_Game/Content/Maps/Development/Regions/FollowerLab";
    public const string ScenePath = Root + "/Scenes/FollowerLab.unity";
    public const string DefinitionPath = Root + "/Data/FollowerLabDefinition.asset";
    private const string CatalogPath = "Assets/_Game/Resources/HubToHome/GameContentCatalog.asset";
    private const string PlayerPath = "Assets/_Game/Content/Characters/Prefabs/Player/Player_Base.prefab";
    private const string CameraPath = "Assets/_Game/Core/Prefabs/Camera/GameplayCameraRig.prefab";
    private const string BootstrapPath = "Assets/_Game/Core/Prefabs/[GameBootstrap].prefab";
    private const string WizzelPath = "Assets/_Game/Content/Characters/AllyDB/WizzelDB.asset";

    // Used through -executeMethod; deliberately not another permanent editor menu.
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null)
            throw new InvalidOperationException("Play/Prefab Mode를 종료한 뒤 생성하세요.");
        if (Directory.Exists(Root) && HasAuthoredFiles(Root))
            throw new InvalidOperationException("기존 FollowerLab은 덮어쓰지 않습니다. 이미 생성된 Scene/데이터를 직접 편집하세요.");

        GameContentCatalog catalog = Require<GameContentCatalog>(CatalogPath);
        if (EditorUtility.IsDirty(catalog))
            throw new InvalidOperationException("콘텐츠 카탈로그의 편집을 먼저 저장하세요.");
        CharacterData leader = Require<CharacterData>(WizzelPath);
        SpeakerData speakerTemplate = Require<SpeakerData>("Assets/_Game/Content/Dialogue/Data/Speakers/Speaker_Wizzel.asset");
        GameObject playerPrefab = Require<GameObject>(PlayerPath);
        GameObject cameraPrefab = Require<GameObject>(CameraPath);
        GameObject bootstrapPrefab = Require<GameObject>(BootstrapPath);
        OverworldFollowerLabRoomBuilder.Preflight();
        for (int i = 1; i <= 5; i++)
            foreach (CharacterData existing in catalog.Characters)
                if (existing != null && existing.CharacterID == "lab.follower." + i)
                    throw new InvalidOperationException("실험용 캐릭터 ID가 이미 있습니다: " + existing.CharacterID);

        Scene previous = SceneManager.GetActiveScene();
        Scene temporary = default;
        var added = new List<CharacterData>(5);
        bool catalogCommitted = false;
        try
        {
            EnsureFolder(Root + "/Scenes");
            EnsureFolder(Root + "/Prefabs/Rooms");
            EnsureFolder(Root + "/Data/Rooms");
            EnsureFolder(Root + "/Data/Areas");
            EnsureFolder(Root + "/Data/Characters");
            EnsureFolder(Root + "/Data/Dialogue");
            // A fresh batch editor starts with an unnamed empty scene. Unity refuses additive creation
            // beside it; replacing only that clean startup scene cannot discard authored scene changes.
            bool cleanBatchStartup = Application.isBatchMode && SceneManager.sceneCount == 1
                && previous.IsValid() && string.IsNullOrEmpty(previous.path) && !previous.isDirty;
            temporary = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                cleanBatchStartup ? NewSceneMode.Single : NewSceneMode.Additive);
            SceneManager.SetActiveScene(temporary);
            // Single scene replacement can unload preflight-only assets; never retain stale native handles.
            catalog = Require<GameContentCatalog>(CatalogPath);
            leader = Require<CharacterData>(WizzelPath);
            speakerTemplate = Require<SpeakerData>("Assets/_Game/Content/Dialogue/Data/Speakers/Speaker_Wizzel.asset");
            playerPrefab = Require<GameObject>(PlayerPath);
            cameraPrefab = Require<GameObject>(CameraPath);
            bootstrapPrefab = Require<GameObject>(BootstrapPath);

            string[] names = { "Warehouse", "Exhibition", "Detour" };
            var rooms = new RoomDefinition[3];
            var areas = new AreaDefinition[3];
            for (int i = 0; i < rooms.Length; i++)
            {
                rooms[i] = Create<RoomDefinition>(Root + "/Data/Rooms/Room_FollowerLab_" + names[i] + "_Definition.asset");
                Set(rooms[i], "_roomId", p => p.stringValue = "lab.follower." + names[i].ToLowerInvariant());
                areas[i] = Create<AreaDefinition>(Root + "/Data/Areas/Area_FollowerLab_" + names[i] + ".asset");
                Set(areas[i], "_areaId", p => p.stringValue = rooms[i].RoomId);
                Set(areas[i], "_description", p => p.stringValue = "동료 추종 실험실 / " + names[i]);
                Set(areas[i], "_roomDefinition", p => p.objectReferenceValue = rooms[i]);
                Set(rooms[i], "_areaDefinition", p => p.objectReferenceValue = areas[i]);
            }

            var colors = new[] { new Color(.45f, 1f, .9f), new Color(.75f, .5f, 1f),
                new Color(1f, .72f, .4f), new Color(.55f, .8f, 1f), new Color(1f, .55f, .72f) };
            string[] labels = { "청록", "보라", "주황", "파랑", "분홍" };
            for (int i = 0; i < 5; i++)
            {
                CharacterData member = Object.Instantiate(leader);
                member.name = "Wizzel_Follower_" + (i + 1);
                member.CharacterID = "lab.follower." + (i + 1);
                member.DisplayName = "위젤 · " + labels[i];
                member.DisplayNameLocalizationKey = "follower_lab.member." + (i + 1);
                member.DisplayNameMode = CharacterDisplayNameMode.StaticData;
                member.BattleSpriteTint = member.BattleSymbolColor = colors[i];
                // Copy only the visual subtree at runtime; no duplicate player controller or collider.
                member.OverworldVisualPrefab = playerPrefab;
                AssetDatabase.CreateAsset(member, Root + "/Data/Characters/" + member.name + ".asset");
                added.Add(member);
            }

            DialogueData[] signs = {
                Dialogue("WarehouseGuide", "follower_lab.sign.warehouse", "창고 통로입니다. 위아래로 돌아가며 달리거나 급히 방향을 바꿔 보세요. 동료는 지나간 길을 따라옵니다."),
                Dialogue("ExhibitionGuide", "follower_lab.sign.exhibition", "전시 공간입니다. 파랑·분홍 동료는 입장하면 전시품을 구경하러 갑니다. 도착한 동료에게 다가가 대화한 뒤 함께 가기를 선택해 보세요."),
                Dialogue("DetourGuide", "follower_lab.sign.detour", "우회로입니다. 중앙 섬을 한 바퀴 돌거나 막다른 통로에서 되돌아가 보세요. 좌우 출구로 세 방을 순환할 수 있습니다.") };
            DialogueData upper = Dialogue("ObserveBlue", "follower_lab.observe.blue", "이 장치의 톱니가 신기한데요. 조금만 더 구경하고 갈까요?");
            DialogueData lower = Dialogue("ObservePink", "follower_lab.observe.pink", "상자 안에 무엇이 들었는지 궁금하네요. 먼저 다녀오셔도 괜찮아요.");
            AttachSpeaker(upper, speakerTemplate, added[3]);
            AttachSpeaker(lower, speakerTemplate, added[4]);
            for (int i = 0; i < rooms.Length; i++)
            {
                GeneratedRoomContext room = OverworldFollowerLabRoomBuilder.Build(i, names[i], rooms,
                    signs[i], upper, lower);
                try
                {
                    GameObject saved = PrefabUtility.SaveAsPrefabAsset(room.Root, Root + "/Prefabs/Rooms/Room_FollowerLab_" + names[i] + ".prefab");
                    if (saved == null) throw new InvalidOperationException("Room 저장 실패: " + names[i]);
                    Set(rooms[i], "_roomPrefab", p => p.objectReferenceValue = saved.GetComponent<RoomInstance>());
                    AssetDatabase.SaveAssetIfDirty(rooms[i]);
                    AssetDatabase.SaveAssetIfDirty(areas[i]);
                }
                finally { Object.DestroyImmediate(room.Root); }
            }
            var definition = Create<OverworldFollowerLabDefinition>(DefinitionPath);
            definition.Leader = leader;
            definition.Companions = added;
            definition.InitialRoom = rooms[0];
            definition.InitialArea = areas[0];
            definition.InitialSpawnPointId = "default";
            definition.InitialFacing = FacingDirection.Right;
            EditorUtility.SetDirty(definition);
            AssetDatabase.SaveAssetIfDirty(definition);
            BuildScene(temporary, definition, rooms, playerPrefab, cameraPrefab, bootstrapPrefab);
            if (!EditorSceneManager.SaveScene(temporary, ScenePath, false))
                throw new InvalidOperationException("FollowerLab Scene 저장 실패");

            // Commit shared references last. All preceding writes are confined to the new fixture root.
            RegisterCreatedCharacters(added);
            catalogCommitted = true;
            GameContentCatalog.InvalidateRuntimeCache();
            Debug.Log("[FollowerLab] 생성 완료: " + ScenePath + " / 총 6명 / Room 3개. Build Settings는 변경하지 않았습니다.");
        }
        catch (Exception exception)
        {
            if (!catalogCommitted)
                foreach (CharacterData member in added) catalog.Characters.Remove(member);
            // Preserve partial assets for inspection. Never recursively delete an authored root on retry.
            Debug.LogError("[FollowerLab] 생성 중단. 새 폴더의 부분 결과는 보존했습니다: " + Root
                + ". 카탈로그 반영=" + catalogCommitted + ". " + exception.Message);
            throw;
        }
        finally
        {
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            if (temporary.IsValid() && temporary.isLoaded && SceneManager.sceneCount > 1)
                EditorSceneManager.CloseScene(temporary, true);
        }
    }

    private static void BuildScene(Scene scene, OverworldFollowerLabDefinition definition, RoomDefinition[] rooms,
        GameObject playerPrefab, GameObject cameraPrefab, GameObject bootstrapPrefab)
    {
        PrefabUtility.InstantiatePrefab(bootstrapPrefab, scene);
        var playerRoot = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, scene);
        PlayerController player = playerRoot.GetComponentInChildren<PlayerController>(true);
        var cameraRoot = (GameObject)PrefabUtility.InstantiatePrefab(cameraPrefab, scene);
        CameraController camera = cameraRoot.GetComponentInChildren<CameraController>(true);
        if (player == null || camera == null || camera.VirtualCamera == null)
            throw new InvalidOperationException("공용 Player/Camera 구성 누락");
        var systems = new GameObject("Follower Lab — Settings");
        RoomContainer container = systems.AddComponent<RoomContainer>();
        Set(container, "_initialRoom", p => p.objectReferenceValue = rooms[0]);
        Set(container, "_loadInitialRoomOnStart", p => p.boolValue = false);
        MapTransitionService transitions = systems.AddComponent<MapTransitionService>();
        Set(transitions, "_roomContainer", p => p.objectReferenceValue = container);
        Set(transitions, "_dontDestroyOnLoad", p => p.boolValue = false);
        RegionEntryCoordinator entry = systems.AddComponent<RegionEntryCoordinator>();
        entry.Configure(container, player, rooms[0], rooms, true);
        Set(entry, "_prepareOnAwake", p => p.boolValue = false);
        systems.AddComponent<OverworldFollowerLabSession>().Configure(definition, player, entry);
        camera.VirtualCamera.Follow = player.transform;
        Set(camera, "_centerTarget", p => p.objectReferenceValue = player.transform);
        PrefabUtility.RecordPrefabInstancePropertyModifications(camera.VirtualCamera);
        PrefabUtility.RecordPrefabInstancePropertyModifications(camera);
        Light2D light = new GameObject("Neutral Global Light").AddComponent<Light2D>();
        light.lightType = Light2D.LightType.Global;
        light.intensity = 1f;
        light.color = Color.white;
    }

    internal static DialogueData Dialogue(string name, string key, string text)
    {
        DialogueData data = Create<DialogueData>(Root + "/Data/Dialogue/" + name + ".asset");
        data.RevealTextImmediately = true;
        data.Nodes.Add(new DialogueNode { LocalizationKey = key, DefaultText = text });
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssetIfDirty(data);
        return data;
    }

    private static void AttachSpeaker(DialogueData dialogue, SpeakerData template, CharacterData member)
    {
        SpeakerData speaker = Object.Instantiate(template);
        speaker.name = "Speaker_" + member.name;
        speaker.SpeakerID = member.CharacterID;
        speaker.DisplayName = member.DisplayName;
        speaker.DisplayNameLocalizationKey = member.DisplayNameLocalizationKey;
        AssetDatabase.CreateAsset(speaker, Root + "/Data/Dialogue/" + speaker.name + ".asset");
        dialogue.Nodes[0].Speaker = speaker;
        EditorUtility.SetDirty(dialogue);
        AssetDatabase.SaveAssetIfDirty(dialogue);
    }

    /// <summary>Recovers a completed fixture whose final catalog write was interrupted; no scene is regenerated.</summary>
    public static void CompleteCatalogRegistration()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Play 중에는 등록하지 않습니다.");
        var definition = Require<OverworldFollowerLabDefinition>(DefinitionPath);
        Require<SceneAsset>(ScenePath);
        if (!definition.TryValidate(out string error) || definition.Companions.Count != 5)
            throw new InvalidOperationException("생성 결과 검증 실패: " + error);
        RegisterCreatedCharacters(definition.Companions);
        Debug.Log("[FollowerLab] 카탈로그 등록 완료 / 기존 Scene과 Room은 변경하지 않았습니다.");
    }

    private static void RegisterCreatedCharacters(List<CharacterData> members)
    {
        // NewScene(Single) may unload an asset referenced only by a managed local. Resolve again at commit.
        GameContentCatalog catalog = Require<GameContentCatalog>(CatalogPath);
        if (EditorUtility.IsDirty(catalog)) throw new InvalidOperationException("카탈로그에 저장하지 않은 편집이 있습니다.");
        for (int i = 0; i < members.Count; i++)
        {
            CharacterData member = members[i];
            if (member == null || member.CharacterID != "lab.follower." + (i + 1)
                || !AssetDatabase.GetAssetPath(member).StartsWith(Root + "/Data/Characters/", StringComparison.Ordinal))
                throw new InvalidOperationException("실험실 생성 캐릭터의 경로/ID가 일치하지 않습니다.");
            foreach (CharacterData existing in catalog.Characters)
                if (existing != null && existing.CharacterID == member.CharacterID && existing != member)
                    throw new InvalidOperationException("다른 캐릭터가 동일 ID를 사용합니다: " + member.CharacterID);
        }
        foreach (CharacterData member in members)
            if (!catalog.Characters.Contains(member)) catalog.Characters.Add(member);
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssetIfDirty(catalog);
        GameContentCatalog.InvalidateRuntimeCache();
    }

    internal static T Require<T>(string path) where T : Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null) throw new InvalidOperationException("필수 자산 누락: " + path);
        return asset;
    }

    private static T Create<T>(string path) where T : ScriptableObject
    {
        if (File.Exists(path)) throw new InvalidOperationException("기존 파일은 덮어쓰지 않습니다: " + path);
        T asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    internal static void Set(Object target, string field, Action<SerializedProperty> write)
    {
        var serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(field);
        if (property == null) throw new InvalidOperationException(target.GetType().Name + "." + field + " 누락");
        write(property);
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    private static bool HasAuthoredFiles(string path)
    {
        foreach (string file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            if (!file.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }
}
