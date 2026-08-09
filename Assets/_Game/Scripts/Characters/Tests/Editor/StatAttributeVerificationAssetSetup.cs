using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class StatAttributeVerificationAssetSetup
{
    private const string Root = "Assets/_Game/Content/Characters/Tests/StatAttributeVerification";
    private const string PlayerPath = Root + "/StatVerification_PlayerData.asset";
    private const string EnemyPath = Root + "/StatVerification_EnemyData.asset";
    private const string ScenePath = Root + "/StatAttributeVerification.unity";

    [MenuItem("HubToHome/Tests/Create Stat Attribute Verification Scene")]
    public static void Create()
    {
        EnsureFolder("Assets/_Game/Content/Characters/Tests");
        EnsureFolder(Root);

        var playerData = AssetDatabase.LoadAssetAtPath<CharacterData>(PlayerPath);
        if (playerData == null)
        {
            playerData = ScriptableObject.CreateInstance<CharacterData>();
            playerData.CharacterID = "stat_verification_player";
            playerData.DisplayName = "Stat Verification Player";
            playerData.BaseStats = new StatBlock
            {
                MaxHP = 100, MaxAP = 40, ATK = 10, DEF = 10, SPD = 10,
                PhysicalResistance = 1f, FireResistance = 1f, IceResistance = 1f,
                ElectricResistance = 1f, CorrosionResistance = 1f,
                IncomingDamageMultiplier = 1f, OutgoingDamageMultiplier = 1.25f
            };
            AssetDatabase.CreateAsset(playerData, PlayerPath);
        }
        else
        {
            playerData.BaseStats.OutgoingDamageMultiplier = 1.25f;
            EditorUtility.SetDirty(playerData);
        }

        var enemyData = AssetDatabase.LoadAssetAtPath<EnemyData>(EnemyPath);
        if (enemyData == null)
        {
            enemyData = ScriptableObject.CreateInstance<EnemyData>();
            enemyData.EnemyId = "stat_verification_enemy";
            enemyData.EnemyName = "Stat Verification Enemy";
            enemyData.BaseStats = new StatBlock
            {
                MaxHP = 1000, MaxAP = 100, ATK = 10, DEF = 300, SPD = 5,
                PhysicalResistance = 1f, FireResistance = 0.5f, IceResistance = 2f,
                ElectricResistance = 1f, CorrosionResistance = 1f,
                IncomingDamageMultiplier = 1f, OutgoingDamageMultiplier = 1f
            };
            AssetDatabase.CreateAsset(enemyData, EnemyPath);
        }

        AssetDatabase.SaveAssets();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var runnerObject = new GameObject("StatAttributeVerificationRunner");
        var runner = runnerObject.AddComponent<StatAttributeVerificationRunner>();

        var playerObject = new GameObject("DummyPlayer");
        playerObject.transform.position = new Vector3(-1.5f, 0f, 0f);
        var player = playerObject.AddComponent<PlayerCharacter>();
        var playerRenderer = playerObject.AddComponent<SpriteRenderer>();
        playerRenderer.color = new Color(0.2f, 0.7f, 1f, 1f);

        var enemyObject = new GameObject("DummyEnemy");
        enemyObject.transform.position = new Vector3(1.5f, 0f, 0f);
        var enemy = enemyObject.AddComponent<EnemyCharacter>();
        var enemyRenderer = enemyObject.AddComponent<SpriteRenderer>();
        enemyRenderer.color = new Color(1f, 0.3f, 0.2f, 1f);

        var cameraObject = new GameObject("Main Camera");
        var camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 4f;
        camera.transform.position = new Vector3(0f, 0f, -10f);
        cameraObject.tag = "MainCamera";

        var lightObject = new GameObject("Directional Light");
        var light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        runner.Player = player;
        runner.Enemy = enemy;
        runner.PlayerData = playerData;
        runner.EnemyData = enemyData;
        runner.RunOnStart = true;

        EditorSceneManager.SaveScene(scene, ScenePath);
        Selection.activeGameObject = runnerObject;
        Debug.Log("[StatAttributeVerification] Created " + ScenePath);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        var slash = path.LastIndexOf('/');
        var parent = path.Substring(0, slash);
        var name = path.Substring(slash + 1);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }
}
