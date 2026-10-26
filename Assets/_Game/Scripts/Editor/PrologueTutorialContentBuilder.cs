#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>명시적인 executeMethod에서만 실행하는 프롤로그 콘텐츠 연결 명령입니다.</summary>
public static class PrologueTutorialContentBuilder
{
    public const string RegionRoot = "Assets/_Game/Content/Maps/Regions/ALL";
    public const string Root = RegionRoot + "/Data/Prologue/Tutorial";
    public const string DefinitionPath = Root + "/PrologueTutorial.asset";
    public const string RegionScene = RegionRoot + "/Scenes/Region_Train.unity";
    public const string IntroScene = "Assets/_Game/Content/Maps/Development/Regions/Title/01_IntroScene.unity";
    public const string OriginalRoom = RegionRoot + "/Prefabs/Rooms/Room_ALL_TrainQ0.prefab";
    public const string CatalogPath = "Assets/_Game/Resources/HubToHome/GameContentCatalog.asset";
    internal const string ManifestPath = Root + "/TutorialContent.manifest.json";
    internal static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);
    internal static readonly List<string> WrittenPaths = new List<string>();

    public static void BuildBatch()
    {
        if (!Application.isBatchMode)
            throw new InvalidOperationException("BuildBatch는 명시적인 Unity batch 실행 전용입니다.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("수정 중인 Scene이 있어 batch 생성을 중단합니다.");
        if (PrefabStageUtility.GetCurrentPrefabStage() != null)
            throw new InvalidOperationException("열린 Prefab Stage를 닫지 않고 중단합니다.");
        bool targetRestored = false;
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            string path = SceneManager.GetSceneAt(i).path;
            targetRestored |= path == RegionScene || path == IntroScene;
        }
        if (targetRestored)
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Build();
    }

    // No MenuItem / InitializeOnLoad: generation is never a side effect of import or opening Unity.
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("프롤로그 생성은 Edit Mode에서만 실행합니다.");
        RequireClosedCleanTargets();
        List<string> previouslyWritten = ValidatePreviousManifest();
        PrologueTutorialAssetFactory.ValidateDependencies();
        PrologueTutorialAssetFactory.ValidateLocalization();
        WrittenPaths.Clear();
        WrittenPaths.AddRange(previouslyWritten);
        EnsureFolder(Root);
        Exception buildFailure = null;
        try
        {
            PrologueTutorialDefinition definition = PrologueTutorialAssetFactory.Build();
            PrologueTutorialRoomFactory.Build(definition);
            ConfigureRegion(definition);
            ConfigureIntro(definition);
            PrologueTutorialJournalFactory.Build();
            Save(definition);
        }
        catch (Exception exception)
        {
            buildFailure = exception;
            throw;
        }
        finally
        {
            try { WriteManifest(); }
            catch (Exception manifestFailure)
            {
                if (buildFailure == null) throw;
                Debug.LogError("[PrologueTutorialContentBuilder] 부분 생성 기록 저장 실패: " + manifestFailure.Message);
            }
        }
        Debug.Log("[PrologueTutorialContentBuilder] 콘텐츠 생성과 연결 완료: " + DefinitionPath);
    }

    private static void ConfigureRegion(PrologueTutorialDefinition definition)
    {
        EditClosedScene(RegionScene, scene =>
        {
            RoomContainer container = FindSingle<RoomContainer>(scene);
            RegionEntryCoordinator coordinator = FindSingle<RegionEntryCoordinator>(scene);
            PlayerController player = FindSingle<PlayerController>(scene);
            var rooms = new List<RoomDefinition>();
            foreach (PrologueTutorialRoomBinding binding in definition.Rooms) rooms.Add(binding.Definition);
            coordinator.Configure(container, player, definition.GetRoom(PrologueTutorialRoom.Repair), rooms);
            var controllers = FindInScene<PrologueTutorialController>(scene);
            if (controllers.Count > 1) throw new InvalidOperationException("중복 프롤로그 제어기입니다.");
            PrologueTutorialController controller = controllers.Count == 1 ? controllers[0]
                : NewSceneObject("Prologue Tutorial", scene).AddComponent<PrologueTutorialController>();
            controller.Configure(definition);

            List<SeamlessBattleHost> hosts = FindInScene<SeamlessBattleHost>(scene);
            if (hosts.Count > 1) throw new InvalidOperationException("Region_Train에 전투 Host가 중복되어 있습니다.");
            if (hosts.Count == 0)
            {
                GameObject prefab = Require<GameObject>("Assets/_Game/Content/Battle/Prefabs/System/SeamlessBattleHost.prefab");
                if (PrefabUtility.InstantiatePrefab(prefab, scene) == null)
                    throw new InvalidOperationException("공용 SeamlessBattleHost 배치에 실패했습니다.");
            }
        });
    }

    private static void ConfigureIntro(PrologueTutorialDefinition definition)
    {
        EditClosedScene(IntroScene, scene =>
        {
            IntroManager intro = FindSingle<IntroManager>(scene);
            Set(intro, "_tutorialDefinition", property => property.objectReferenceValue = definition);
        });
    }

    private static void EditClosedScene(string path, Action<Scene> edit)
    {
        Scene previous = SceneManager.GetActiveScene();
        Scene opened = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        try
        {
            SceneManager.SetActiveScene(opened);
            edit(opened);
            Track(path);
            if (!EditorSceneManager.SaveScene(opened, path)) throw new IOException("Scene 저장 실패: " + path);
        }
        finally
        {
            if (opened.IsValid() && opened.isLoaded) EditorSceneManager.CloseScene(opened, true);
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
        }
    }

    internal static void RequireClosedCleanTargets()
    {
        if (PrefabStageUtility.GetCurrentPrefabStage() != null)
            throw new InvalidOperationException("Prefab Mode를 닫은 뒤 실행하세요. 열린 편집 내용을 저장하지 않습니다.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (scene.isDirty || scene.path == RegionScene || scene.path == IntroScene)
                throw new InvalidOperationException("열린/수정 중인 대상 Scene을 덮어쓰지 않습니다: " + scene.path);
        }
    }

    internal static List<string> ValidatePreviousManifest()
    {
        var known = new HashSet<string>(StringComparer.Ordinal);
        if (!File.Exists(ManifestPath))
        {
            RequireNoUnknownGeneratedContent(known);
            return new List<string>();
        }
        Manifest manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(ManifestPath, StrictUtf8));
        if (manifest == null || manifest.Version != 1 || manifest.Files == null || manifest.Files.Count == 0)
            throw new InvalidOperationException("프롤로그 생성 기록이 유효하지 않습니다.");
        foreach (FileHash file in manifest.Files)
        {
            if (file == null || !IsOwnedPath(file.Path) || !File.Exists(file.Path)
                || !string.Equals(Hash(file.Path), file.Hash, StringComparison.Ordinal))
                throw new InvalidOperationException("생성 이후 수정되거나 사라진 파일입니다. 사용자 편집은 덮어쓰지 않습니다: " + file?.Path);
            known.Add(file.Path);
        }
        RequireNoUnknownGeneratedContent(known);
        var paths = new List<string>();
        foreach (string path in known)
            if (!path.EndsWith(".meta", StringComparison.Ordinal)) paths.Add(path);
        return paths;
    }

    private static void RequireNoUnknownGeneratedContent(HashSet<string> known)
    {
        string[] roots = { Root, "Assets/_Game/Content/Characters/EnemyDB/Prologue",
            "Assets/_Game/Content/Skills/Enemy/Prologue", "Assets/_Game/Content/Items/Consumables/Prologue" };
        foreach (string root in roots)
            if (Directory.Exists(root))
                foreach (string file in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
                {
                    string path = file.Replace('\\', '/');
                    if (path == ManifestPath || path.EndsWith(".meta", StringComparison.Ordinal)) continue;
                    if (!known.Contains(path)) throw new InvalidOperationException("생성 기록 없는 기존 콘텐츠를 덮어쓰지 않습니다: " + path);
                }
        string[] singleFolders = { RegionRoot + "/Prefabs/NPCs", RegionRoot + "/Prefabs/Rooms", "Assets/_Game/Content/Characters/Prefabs/Enemy" };
        foreach (string folder in singleFolders)
            if (Directory.Exists(folder))
                foreach (string file in Directory.GetFiles(folder,"*.prefab"))
                {
                    string path=file.Replace('\\','/');
                    string name=Path.GetFileName(path);
                    if ((name.StartsWith("Prologue_",StringComparison.Ordinal) || name.StartsWith("Room_ALL_TrainTutorial_",StringComparison.Ordinal))
                        && !known.Contains(path)) throw new InvalidOperationException("생성 기록 없는 프리팹을 덮어쓰지 않습니다: "+path);
                }
    }

    internal static void WriteManifest()
    {
        var manifest = new Manifest { Version = 1 };
        WrittenPaths.Sort(StringComparer.Ordinal);
        foreach (string path in WrittenPaths)
        {
            if (!IsOwnedPath(path)) throw new InvalidOperationException("소유 범위 밖 파일: " + path);
            // A native creation may fail before its first file write. Do not adopt any other file.
            if (!File.Exists(path)) continue;
            manifest.Files.Add(new FileHash { Path = path, Hash = Hash(path) });
            if (File.Exists(path + ".meta")) manifest.Files.Add(new FileHash { Path = path + ".meta", Hash = Hash(path + ".meta") });
        }
        if (manifest.Files.Count == 0) return;
        string temporary = ManifestPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonUtility.ToJson(manifest, true), StrictUtf8);
            if (File.Exists(ManifestPath)) File.Replace(temporary, ManifestPath, null);
            else File.Move(temporary, ManifestPath);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    internal static bool IsOwnedPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.IndexOf("..", StringComparison.Ordinal) >= 0) return false;
        string basePath = path.EndsWith(".meta", StringComparison.Ordinal) ? path.Substring(0, path.Length - 5) : path;
        return basePath.StartsWith(Root + "/", StringComparison.Ordinal)
            || basePath.StartsWith(RegionRoot + "/Prefabs/NPCs/Prologue_", StringComparison.Ordinal)
            || basePath.StartsWith(RegionRoot + "/Prefabs/Rooms/Room_ALL_TrainTutorial_", StringComparison.Ordinal)
            || basePath.StartsWith("Assets/_Game/Content/Characters/EnemyDB/Prologue/", StringComparison.Ordinal)
            || basePath.StartsWith("Assets/_Game/Content/Characters/Prefabs/Enemy/Prologue_", StringComparison.Ordinal)
            || basePath.StartsWith("Assets/_Game/Content/Skills/Enemy/Prologue/", StringComparison.Ordinal)
            || basePath.StartsWith("Assets/_Game/Content/Items/Consumables/Prologue/", StringComparison.Ordinal)
            || basePath == OriginalRoom || basePath == RegionScene || basePath == IntroScene || basePath == CatalogPath
            || basePath == "Assets/_Game/Resources/HubToHome/JournalCatalog.asset"
            || basePath == "Assets/_Game/Content/Characters/AllyDB/WizzelDB.asset";
    }

    internal static T Asset<T>(string path) where T : ScriptableObject
    {
        T result = AssetDatabase.LoadAssetAtPath<T>(path);
        if (result == null)
        {
            if (File.Exists(path)) throw new InvalidOperationException("형식이 다른 기존 파일: " + path);
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            result = ScriptableObject.CreateInstance<T>();
            result.name = Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(result, path);
        }
        Track(path);
        return result;
    }

    internal static void Save(Object asset)
    {
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssetIfDirty(asset);
        Track(AssetDatabase.GetAssetPath(asset));
    }

    internal static void Track(string path) { if (!WrittenPaths.Contains(path)) WrittenPaths.Add(path); }
    internal static T Require<T>(string path) where T : Object
        => AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new InvalidOperationException("필수 자산 없음: " + path);
    internal static void Set(Object target, string field, Action<SerializedProperty> write)
    {
        var serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(field);
        if (property == null) throw new InvalidOperationException(target.GetType().Name + "." + field + " 연결 필드가 없습니다.");
        write(property);
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
    internal static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int slash = path.LastIndexOf('/');
        if (slash < 0) throw new ArgumentException("Assets 내부 경로가 필요합니다.", nameof(path));
        EnsureFolder(path.Substring(0, slash));
        AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
    }
    private static GameObject NewSceneObject(string name, Scene scene)
    { var result = new GameObject(name); SceneManager.MoveGameObjectToScene(result, scene); return result; }
    private static T FindSingle<T>(Scene scene) where T : Component
    { List<T> found = FindInScene<T>(scene); return found.Count == 1 ? found[0] : throw new InvalidOperationException(typeof(T).Name + " 개수: " + found.Count); }
    private static List<T> FindInScene<T>(Scene scene) where T : Component
    { var result = new List<T>(); foreach (GameObject root in scene.GetRootGameObjects()) result.AddRange(root.GetComponentsInChildren<T>(true)); return result; }
    private static string Hash(string path)
    { using (SHA256 hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant(); }
    [Serializable] private sealed class Manifest { public int Version; public List<FileHash> Files = new List<FileHash>(); }
    [Serializable] private sealed class FileHash { public string Path; public string Hash; }
}
#endif
