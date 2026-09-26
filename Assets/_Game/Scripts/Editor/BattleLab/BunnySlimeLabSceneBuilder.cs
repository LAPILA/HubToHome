#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>새 실험 씬만 생성합니다. 기존 씬, 공용 프리팹, Build Settings는 변경하지 않습니다.</summary>
public static class BunnySlimeLabSceneBuilder
{
    private const string BootstrapPath = "Assets/_Game/Core/Prefabs/[GameBootstrap].prefab";
    private const string PlayerPath = "Assets/_Game/Content/Characters/Prefabs/Player/Player_Base.prefab";
    private const string CameraPath = "Assets/_Game/Core/Prefabs/Camera/GameplayCameraRig.prefab";
    private const string HostPath = "Assets/_Game/Content/Battle/Prefabs/System/SeamlessBattleHost.prefab";
    private const string ArtRoot = "Assets/_Game/Content/Maps/Development/Shared/Art/";
    private const string LightingRoot = "Assets/_Game/Content/Maps/Development/BunnySlimeBattleLab/";
    private const string LightingPath = LightingRoot + "Prefabs/BunnySlimeLab_Lighting.prefab";
    private const string RendererPath = LightingRoot + "Presentation/BunnySlimeLab_Renderer2D.asset";
    private const int LabRendererIndex = 1;
    private const string SpriteLitMaterialGuid = "a97c105638bdf8b4a8650670310a4cd3";

    public static string Build(string root, BunnySlimeBattleLabData data)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Play 중에는 전투 실험 씬을 생성하지 않습니다.");
        if (data == null || data.Party == null || data.Party.Length != 6 || data.Party[0] == null)
            throw new ArgumentException("먼저 샘플 전투 데이터와 파티 6명을 생성해야 합니다.", nameof(data));
        string normalized = (root ?? string.Empty).Replace('\\', '/').TrimEnd('/');
        if (!normalized.StartsWith("Assets/_Game/Content/Maps/Development/", StringComparison.Ordinal)
            || normalized.Contains(".."))
            throw new ArgumentException("실험 씬은 Content/Maps/Development 하위에만 생성합니다.", nameof(root));
        string scenePath = normalized + "/Scenes/BunnySlimeBattleLab.unity";
        if (File.Exists(scenePath))
            return scenePath; // 사용자가 편집한 기존 실험 씬도 자동 덮어쓰지 않습니다.

        GameObject bootstrap = RequirePrefab(BootstrapPath);
        GameObject camera = RequirePrefab(CameraPath);
        GameObject player = RequirePrefab(PlayerPath);
        GameObject host = RequirePrefab(HostPath);
        GameObject lighting = RequirePrefab(LightingPath);
        ValidateLabRenderer("Assets/RenderSettings/PC_RPAsset.asset");
        ValidateLabRenderer("Assets/RenderSettings/Mobile_RPAsset.asset");
        EnsureFolder(normalized + "/Scenes");
        Scene previousActive = SceneManager.GetActiveScene();
        Scene created = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        try
        {
            SceneManager.SetActiveScene(created);
            Instantiate(bootstrap, created);
            GameObject cameraObject = Instantiate(camera, created);
            Camera realCamera = cameraObject.GetComponentInChildren<Camera>(true);
            if (realCamera != null)
            {
                realCamera.backgroundColor = new Color(0.035f, 0.045f, 0.09f);
                realCamera.clearFlags = CameraClearFlags.SolidColor;
                UniversalAdditionalCameraData cameraData = realCamera.GetUniversalAdditionalCameraData();
                cameraData.SetRenderer(LabRendererIndex);
                cameraData.renderPostProcessing = true;
                cameraData.requiresDepthTexture = false;
                cameraData.requiresColorTexture = false;
            }
            Instantiate(host, created);
            GameObject playerObject = Instantiate(player, created);
            playerObject.name = "Lab Player";
            playerObject.transform.position = new Vector3(-3.25f, 0f, 1f);
            var playerData = new SerializedObject(playerObject.GetComponent<PlayerCharacter>());
            SerializedProperty characterReference = playerData.FindProperty("_characterData");
            if (characterReference == null) throw new InvalidOperationException("PlayerCharacter 데이터 연결 필드를 찾지 못했습니다.");
            characterReference.objectReferenceValue = data.Party[0];
            playerData.ApplyModifiedPropertiesWithoutUndo();

            var sessionObject = new GameObject("BunnySlime Battle Lab");
            SceneManager.MoveGameObjectToScene(sessionObject, created);
            sessionObject.AddComponent<BunnySlimeBattleLabSession>().Configure(data, playerObject.GetComponent<PlayerController>());
            BuildBackdrop(created);
            Instantiate(lighting, created);
            if (!EditorSceneManager.SaveScene(created, scenePath))
                throw new IOException("새 실험 씬을 저장하지 못했습니다: " + scenePath);
        }
        finally
        {
            if (created.IsValid() && created.isLoaded)
                EditorSceneManager.CloseScene(created, true);
            if (previousActive.IsValid() && previousActive.isLoaded)
                SceneManager.SetActiveScene(previousActive);
        }
        return scenePath;
    }

    private static void BuildBackdrop(Scene scene)
    {
        var art = new GameObject("Lab Backdrop - Shared Sample Art");
        SceneManager.MoveGameObjectToScene(art, scene);
        AddSprite(art.transform, "Distant Stage", ArtRoot + "EXBG_Far.png", new Vector3(0f, 4f, 5f), 34f, -120);
        AddSprite(art.transform, "Floor", ArtRoot + "EXBG_Floor.png", new Vector3(0f, -3.5f, 4f), 34f, -100);
    }

    // Rendering is opt-in on this scene's real camera. Never change the shared default renderer.
    private static void ValidateLabRenderer(string pipelinePath)
    {
        var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath);
        var renderer = AssetDatabase.LoadAssetAtPath<Renderer2DData>(RendererPath);
        if (pipeline == null || renderer == null)
            throw new InvalidOperationException("실험실 전용 2D Renderer 또는 렌더 파이프라인이 없습니다.");

        var serialized = new SerializedObject(pipeline);
        SerializedProperty renderers = serialized.FindProperty("m_RendererDataList");
        if (renderers == null || renderers.arraySize <= LabRendererIndex
            || renderers.GetArrayElementAtIndex(LabRendererIndex).objectReferenceValue != renderer)
            throw new InvalidOperationException(
                pipelinePath + "의 Renderer List [1]에 BunnySlimeLab_Renderer2D를 연결해 주세요. 기본 Renderer [0]은 유지합니다.");
    }

    private static void AddSprite(Transform parent, string name, string path, Vector3 position, float width, int order)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null)
        {
            UnityEngine.Object[] parts = AssetDatabase.LoadAllAssetsAtPath(path);
            for (int i = 0; i < parts.Length; i++)
                if (parts[i] is Sprite candidate) { sprite = candidate; break; }
        }
        if (sprite == null) return;
        var root = new GameObject(name, typeof(SpriteRenderer));
        root.transform.SetParent(parent, false);
        root.transform.position = position;
        root.transform.localScale = Vector3.one * (width / Mathf.Max(0.01f, sprite.bounds.size.x));
        SpriteRenderer renderer = root.GetComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingLayerName = "Background";
        renderer.sortingOrder = order;
        renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(
            AssetDatabase.GUIDToAssetPath(SpriteLitMaterialGuid));
    }

    private static GameObject RequirePrefab(string path)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) throw new InvalidOperationException("필수 공용 프리팹이 없습니다: " + path);
        return prefab;
    }

    private static GameObject Instantiate(GameObject prefab, Scene scene)
    {
        var instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
        if (instance == null) throw new InvalidOperationException("공용 프리팹 배치 실패: " + prefab.name);
        return instance;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = path.Substring(0, path.LastIndexOf('/'));
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, path.Substring(path.LastIndexOf('/') + 1));
    }
}
#endif
