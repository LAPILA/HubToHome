#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;

public sealed class ContentValidationWindow : EditorWindow
{
    private const string CatalogAssetPath = "Assets/_Game/Resources/HubToHome/GameContentCatalog.asset";
    private const string DefaultUiFontPath = "Assets/_Game/Presentation/UI/Fonts/Silver SDF.asset";
    private const string DefaultPotionPath = "Assets/_Game/Content/Items/Consumables/SmallPotion.asset";
    private readonly List<string> _issues = new List<string>();
    private Vector2 _scroll;

    [MenuItem("Hub To Home/Content/Validation Window")]
    private static void Open()
    {
        GetWindow<ContentValidationWindow>("Content Validation");
    }

    [MenuItem("Hub To Home/Content/Rebuild Runtime Catalog")]
    public static void RebuildCatalogMenu()
    {
        RebuildCatalog(true);
    }

    [MenuItem("Hub To Home/Content/Prepare Default Content")]
    public static void PrepareDefaultContent()
    {
        EnsureDefaultPotion();
        ContentValidationWindow window = CreateInstance<ContentValidationWindow>();
        try
        {
            window.GenerateMissingIds();
            RepairPrefabLinks();
            RebuildCatalog(true);
        }
        finally
        {
            DestroyImmediate(window);
        }
    }


    [MenuItem("Hub To Home/Content/Validate Project Content")]
    public static void ValidateProjectContent()
    {
        ContentValidationWindow window = CreateInstance<ContentValidationWindow>();
        try
        {
            window.Scan();
            if (window._issues.Count == 0)
            {
                Debug.Log("[ContentValidation] No issues found.");
                return;
            }

            for (int i = 0; i < window._issues.Count; i++)
                Debug.LogError("[ContentValidation] " + window._issues[i]);
            throw new InvalidOperationException($"Content validation failed with {window._issues.Count} issue(s).");
        }
        finally
        {
            DestroyImmediate(window);
        }
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Game Content", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Missing IDs can be generated safely. Duplicate IDs are reported and must be chosen manually.",
            MessageType.Info);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Scan", GUILayout.Height(28f))) Scan();
            if (GUILayout.Button("Generate Missing IDs", GUILayout.Height(28f)))
            {
                GenerateMissingIds();
                Scan();
            }
            if (GUILayout.Button("Repair Prefab Links", GUILayout.Height(28f)))
            {
                RepairPrefabLinks();
                Scan();
            }
            if (GUILayout.Button("Rebuild Catalog", GUILayout.Height(28f)))
            {
                RebuildCatalog(true);
                Scan();
            }
        }

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField($"Issues: {_issues.Count}", EditorStyles.boldLabel);
        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        if (_issues.Count == 0)
            EditorGUILayout.HelpBox("No content issues found.", MessageType.Info);
        for (int i = 0; i < _issues.Count; i++)
            EditorGUILayout.HelpBox(_issues[i], MessageType.Warning);
        EditorGUILayout.EndScrollView();
    }

    private void OnEnable()
    {
        Scan();
    }

    private void Scan()
    {
        _issues.Clear();
        List<CharacterData> characters = LoadAll<CharacterData>();
        List<EnemyData> enemies = LoadAll<EnemyData>();
        List<SkillData> skills = LoadAll<SkillData>();
        List<ItemData> items = LoadAll<ItemData>();
        GameContentCatalog catalog = AssetDatabase.LoadAssetAtPath<GameContentCatalog>(CatalogAssetPath);
        if (catalog == null)
            _issues.Add($"{CatalogAssetPath}: Runtime catalog is missing.");
        else if (catalog.DefaultUiFont == null)
            AddIssue(catalog, "Default UI font is missing.");

        ValidateIds(characters, item => item != null ? item.CharacterID : null, "Character");
        ValidateIds(enemies, item => item != null ? item.EnemyId : null, "Enemy");
        ValidateIds(skills, item => item != null ? item.SkillID : null, "Skill");
        ValidateIds(items, item => item != null ? item.ItemID : null, "Item");

        var itemIds = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < items.Count; i++)
            if (items[i] != null && !string.IsNullOrWhiteSpace(items[i].ItemID)) itemIds.Add(items[i].ItemID.Trim());

        for (int i = 0; i < characters.Count; i++)
        {
            CharacterData data = characters[i];
            if (data == null) continue;
            if (data.BattlePrefab == null)
                AddIssue(data, "Character battle prefab is missing.");
            else if (data.BattlePrefab.GetComponent<PlayerCharacter>() == null)
                AddIssue(data, "Character battle prefab has no PlayerCharacter.");
            ValidateReferences(data.DefaultSkills, data, "DefaultSkills");
        }

        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyData data = enemies[i];
            if (data == null) continue;
            if (data.BattlePrefab == null)
                AddIssue(data, "Enemy battle prefab is missing.");
            else if (data.BattlePrefab.GetComponent<EnemyCharacter>() == null)
                AddIssue(data, "Enemy battle prefab has no EnemyCharacter.");
            ValidateReferences(data.SkillList, data, "SkillList");
            ValidateReferences(data.StrongSkillList, data, "StrongSkillList");

            if (data.Drops != null)
            {
                for (int dropIndex = 0; dropIndex < data.Drops.Count; dropIndex++)
                {
                    EnemyDropEntry drop = data.Drops[dropIndex];
                    if (drop == null || string.IsNullOrWhiteSpace(drop.ItemId))
                        AddIssue(data, $"Drops[{dropIndex}] item ID is missing.");
                    else if (!itemIds.Contains(drop.ItemId.Trim()))
                        AddIssue(data, $"Drops[{dropIndex}] references unknown item '{drop.ItemId}'.");
                    if (drop != null && drop.MaxAmount < drop.MinAmount)
                        AddIssue(data, $"Drops[{dropIndex}] MaxAmount is smaller than MinAmount.");
                }
            }
        }

        for (int i = 0; i < items.Count; i++)
        {
            ItemData item = items[i];
            if (item == null || item.Type != ItemType.Consumable) continue;
            if (item.ActionType == EffectActionType.None)
                AddIssue(item, "Consumable item has no effect.");
            if ((item.ActionType == EffectActionType.Heal || item.ActionType == EffectActionType.Damage)
                && item.TargetStat != TargetStatType.HP
                && item.TargetStat != TargetStatType.MP)
                AddIssue(item, "Heal/Damage item must target HP or MP.");
            if (item.ActionType == EffectActionType.ApplyStatus
                && !StatusEffectFactory.IsKnown(item.StatusEffectID))
                AddIssue(item, $"Unknown status effect '{item.StatusEffectID}'.");
        }

        Repaint();
    }

    private void GenerateMissingIds()
    {
        GenerateMissingIds(LoadAll<CharacterData>(), data => data.CharacterID, (data, id) => data.CharacterID = id, "character");
        GenerateMissingIds(LoadAll<EnemyData>(), data => data.EnemyId, (data, id) => data.EnemyId = id, "enemy");
        GenerateMissingIds(LoadAll<SkillData>(), data => data.SkillID, (data, id) => data.SkillID = id, "skill");
        GenerateMissingIds(LoadAll<ItemData>(), data => data.ItemID, (data, id) => data.ItemID = id, "item");
        AssetDatabase.SaveAssets();
        RebuildCatalog(false);
    }

    private static void EnsureDefaultPotion()
    {
        ItemData potion = AssetDatabase.LoadAssetAtPath<ItemData>(DefaultPotionPath);
        if (potion == null)
        {
            string folder = Path.GetDirectoryName(DefaultPotionPath)?.Replace(Path.DirectorySeparatorChar, '/');
            EnsureAssetFolder(folder);
            potion = CreateInstance<ItemData>();
            potion.ItemID = "consumable.small_potion";
            potion.ItemName = "Small Potion";
            potion.Description = "Restores 30 HP.";
            potion.Type = ItemType.Consumable;
            potion.TargetType = TargetAreaType.AllyOnly;
            potion.UsableInBattle = true;
            potion.UsableInOverworld = true;
            potion.ActionType = EffectActionType.Heal;
            potion.TargetStat = TargetStatType.HP;
            potion.CalcType = ValueCalcType.Flat;
            potion.EffectValue = 30;
            potion.Price = 50;
            AssetDatabase.CreateAsset(potion, DefaultPotionPath);
        }

        List<EnemyData> enemies = LoadAll<EnemyData>();
        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyData enemy = enemies[i];
            if (enemy == null || enemy.Drops == null || enemy.Drops.Count > 0) continue;
            if (enemy.name.IndexOf("slime", StringComparison.OrdinalIgnoreCase) < 0
                && enemy.EnemyName.IndexOf("slime", StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            enemy.Drops.Add(new EnemyDropEntry
            {
                ItemId = potion.ItemID,
                MinAmount = 1,
                MaxAmount = 1,
                DropChance = 0.5f
            });
            EditorUtility.SetDirty(enemy);
        }

        AssetDatabase.SaveAssets();
    }

    private static void RepairPrefabLinks()
    {
        List<GameObject> prefabs = LoadAll<GameObject>("t:Prefab", new[] { "Assets/_Game" });

        for (int i = 0; i < prefabs.Count; i++)
        {
            GameObject prefab = prefabs[i];
            if (prefab == null) continue;
            PlayerCharacter player = prefab.GetComponent<PlayerCharacter>();
            if (player != null && player.CharacterData != null && player.CharacterData.BattlePrefab == null)
            {
                player.CharacterData.BattlePrefab = prefab;
                EditorUtility.SetDirty(player.CharacterData);
            }

            EnemyCharacter enemy = prefab.GetComponent<EnemyCharacter>();
            if (enemy != null && enemy.Data != null && enemy.Data.BattlePrefab == null)
            {
                enemy.Data.BattlePrefab = prefab;
                EditorUtility.SetDirty(enemy.Data);
            }
        }

        AssetDatabase.SaveAssets();
        RebuildCatalog(false);
    }

    public static GameContentCatalog RebuildCatalog(bool logResult)
    {
        string directory = Path.GetDirectoryName(CatalogAssetPath)?.Replace('\\', '/');
        EnsureAssetFolder(directory);

        GameContentCatalog catalog = AssetDatabase.LoadAssetAtPath<GameContentCatalog>(CatalogAssetPath);
        if (catalog == null)
        {
            catalog = CreateInstance<GameContentCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogAssetPath);
        }

        catalog.Characters = LoadAll<CharacterData>();
        catalog.Enemies = LoadAll<EnemyData>();
        catalog.Skills = LoadAll<SkillData>();
        catalog.Items = LoadAll<ItemData>();
        if (catalog.DefaultUiFont == null)
            catalog.DefaultUiFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(DefaultUiFontPath);
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        GameContentCatalog.InvalidateRuntimeCache();

        if (logResult)
            Debug.Log($"[GameContentCatalog] Rebuilt: {catalog.Characters.Count} characters, {catalog.Enemies.Count} enemies, {catalog.Skills.Count} skills, {catalog.Items.Count} items.", catalog);
        return catalog;
    }

    private void AddIssue(UnityEngine.Object context, string message)
    {
        _issues.Add($"{AssetDatabase.GetAssetPath(context)}: {message}");
    }

    private void ValidateIds<T>(IReadOnlyList<T> assets, Func<T, string> getId, string kind) where T : UnityEngine.Object
    {
        var owners = new Dictionary<string, T>(StringComparer.Ordinal);
        for (int i = 0; i < assets.Count; i++)
        {
            T asset = assets[i];
            if (asset == null) continue;
            string id = getId(asset);
            if (string.IsNullOrWhiteSpace(id))
            {
                AddIssue(asset, $"{kind} ID is missing.");
                continue;
            }

            id = id.Trim();
            if (owners.TryGetValue(id, out T previous))
                AddIssue(asset, $"Duplicate {kind} ID '{id}' (also {AssetDatabase.GetAssetPath(previous)}).");
            else
                owners.Add(id, asset);
        }
    }

    private void ValidateReferences<T>(IReadOnlyList<T> references, UnityEngine.Object owner, string fieldName)
        where T : UnityEngine.Object
    {
        if (references == null) return;
        for (int i = 0; i < references.Count; i++)
            if (references[i] == null) AddIssue(owner, $"{fieldName}[{i}] is missing.");
    }

    private static void GenerateMissingIds<T>(
        IReadOnlyList<T> assets,
        Func<T, string> getId,
        Action<T, string> setId,
        string prefix) where T : UnityEngine.Object
    {
        for (int i = 0; i < assets.Count; i++)
        {
            T asset = assets[i];
            if (asset == null || !string.IsNullOrWhiteSpace(getId(asset))) continue;
            string path = AssetDatabase.GetAssetPath(asset);
            string guid = AssetDatabase.AssetPathToGUID(path);
            string suffix = guid.Length >= 8 ? guid.Substring(0, 8) : Math.Abs(path.GetHashCode()).ToString("x8");
            setId(asset, $"{prefix}_{Slug(asset.name)}_{suffix}");
            EditorUtility.SetDirty(asset);
        }
    }

    private static string Slug(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "content";
        char[] buffer = value.Trim().ToLowerInvariant().ToCharArray();
        for (int i = 0; i < buffer.Length; i++)
            if (!char.IsLetterOrDigit(buffer[i])) buffer[i] = '_';
        return new string(buffer).Trim('_');
    }

    private static List<T> LoadAll<T>() where T : UnityEngine.Object
    {
        return LoadAll<T>($"t:{typeof(T).Name}", new[] { "Assets/_Game" });
    }

    private static List<T> LoadAll<T>(string filter, string[] folders) where T : UnityEngine.Object
    {
        string[] guids = AssetDatabase.FindAssets(filter, folders);
        var result = new List<T>(guids.Length);
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) result.Add(asset);
        }
        result.Sort((a, b) => string.CompareOrdinal(AssetDatabase.GetAssetPath(a), AssetDatabase.GetAssetPath(b)));
        return result;
    }

    private static void EnsureAssetFolder(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder) || AssetDatabase.IsValidFolder(folder)) return;
        string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
        EnsureAssetFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
    }
}
#endif
