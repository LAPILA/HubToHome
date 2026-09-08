#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;

public sealed class ContentValidationWindow : EditorWindow
{
    private const string DefaultUiFontPath = "Assets/_Game/Presentation/UI/Fonts/Silver SDF.asset";
    private const string DefaultPotionPath = "Assets/_Game/Content/Items/Consumables/SmallPotion.asset";

    private ContentValidationReport _report = new ContentValidationReport();
    private Vector2 _scroll;
    private string _search = string.Empty;
    private bool _showErrors = true;
    private bool _showWarnings = true;
    private bool _showMaintenance;

    [MenuItem("Hub To Home/검사/콘텐츠 검사", false, 200)]
    private static void Open()
    {
        GetWindow<ContentValidationWindow>("콘텐츠 검사");
    }

    public static void RebuildCatalogMenu()
    {
        RebuildCatalog(true);
    }

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

    public static void ValidateProjectContent()
    {
        ContentValidationReport report = ScanProject();
        if (report.Issues.Count == 0)
        {
            Debug.Log("[ContentValidation] No issues found.");
            return;
        }

        for (int i = 0; i < report.Issues.Count; i++)
        {
            ContentValidationIssue issue = report.Issues[i];
            string location = string.IsNullOrWhiteSpace(issue.AssetPath)
                ? "<no asset>"
                : issue.AssetPath;
            string message = "[ContentValidation][" + issue.Code + "] "
                + location + ": " + issue.Message;
            if (issue.Severity == ContentValidationSeverity.Error)
                Debug.LogError(message, issue.Context);
            else
                Debug.LogWarning(message, issue.Context);
        }

        Debug.Log(
            "[ContentValidation] " + report.ErrorCount + " error(s), "
            + report.WarningCount + " warning(s).");
        EnsureNoErrors(report);
    }

    public static ContentValidationReport ScanProject()
    {
        return ProjectContentValidator.Validate(AssetDatabaseContentSource.Capture());
    }

    public static bool TrySelectIssue(ContentValidationIssue issue)
    {
        if (issue == null || !issue.CanSelect)
            return false;

        Selection.activeObject = issue.Context;
        EditorGUIUtility.PingObject(issue.Context);
        return true;
    }

    public static void EnsureNoErrors(ContentValidationReport report)
    {
        if (report == null)
            throw new ArgumentNullException(nameof(report));
        if (report.HasErrors)
        {
            throw new InvalidOperationException(
                "Content validation failed with " + report.ErrorCount + " error(s).");
        }
    }

    private void OnEnable()
    {
        titleContent = new GUIContent("콘텐츠 검사");
        Scan();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("프로젝트 콘텐츠 검사", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "검사는 자산을 변경하지 않습니다. 오류 행의 '선택'을 눌러 해당 자산을 확인하세요.",
            MessageType.Info);

        DrawCommandToolbar();
        DrawMaintenance();
        DrawFilterToolbar();

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField(
            "오류 " + _report.ErrorCount + "  |  경고 " + _report.WarningCount,
            EditorStyles.boldLabel);

        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        int visibleCount = 0;
        for (int i = 0; i < _report.Issues.Count; i++)
        {
            ContentValidationIssue issue = _report.Issues[i];
            if (!IsVisible(issue))
                continue;

            DrawIssue(issue);
            visibleCount++;
        }

        if (visibleCount == 0)
            EditorGUILayout.HelpBox("현재 필터에 표시할 문제가 없습니다.", MessageType.Info);
        EditorGUILayout.EndScrollView();
    }

    private void DrawCommandToolbar()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("다시 검사", GUILayout.Height(28f)))
                Scan();
        }
    }

    private void DrawMaintenance()
    {
        _showMaintenance = EditorGUILayout.Foldout(_showMaintenance, "고급 유지보수", true);
        if (!_showMaintenance)
            return;

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.HelpBox(
                "아래 명령은 프로젝트 자산을 수정하고 저장합니다. 검사 결과를 확인한 뒤 필요한 명령만 실행하세요.",
                MessageType.Warning);
            if (GUILayout.Button("누락된 ID 생성"))
            {
                GenerateMissingIds();
                Scan();
            }
            if (GUILayout.Button("프리팹 연결 복구"))
            {
                RepairPrefabLinks();
                Scan();
            }
            if (GUILayout.Button("런타임 카탈로그 다시 만들기"))
            {
                RebuildCatalog(true);
                Scan();
            }
            if (GUILayout.Button("기본 콘텐츠 준비")
                && EditorUtility.DisplayDialog(
                    "기본 콘텐츠 준비",
                    "기본 포션과 슬라임 드롭을 준비하고, 누락된 ID·프리팹 연결·런타임 카탈로그를 수정합니다. 실행할까요?",
                    "실행",
                    "취소"))
            {
                PrepareDefaultContent();
                Scan();
            }
        }
    }

    private void DrawFilterToolbar()
    {
        EditorGUILayout.Space(5f);
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            _search = GUILayout.TextField(_search, EditorStyles.toolbarSearchField);
            _showErrors = GUILayout.Toggle(
                _showErrors,
                "오류 " + _report.ErrorCount,
                EditorStyles.toolbarButton,
                GUILayout.Width(80f));
            _showWarnings = GUILayout.Toggle(
                _showWarnings,
                "경고 " + _report.WarningCount,
                EditorStyles.toolbarButton,
                GUILayout.Width(95f));
        }
    }

    private bool IsVisible(ContentValidationIssue issue)
    {
        if (issue.Severity == ContentValidationSeverity.Error && !_showErrors)
            return false;
        if (issue.Severity == ContentValidationSeverity.Warning && !_showWarnings)
            return false;
        if (string.IsNullOrWhiteSpace(_search))
            return true;

        return ContainsIgnoreCase(issue.Code, _search)
            || ContainsIgnoreCase(issue.Message, _search)
            || ContainsIgnoreCase(issue.AssetPath, _search);
    }

    private static bool ContainsIgnoreCase(string value, string search)
    {
        return !string.IsNullOrEmpty(value)
            && value.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static void DrawIssue(ContentValidationIssue issue)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUIContent icon = EditorGUIUtility.IconContent(
                    issue.Severity == ContentValidationSeverity.Error
                        ? "console.erroricon.sml"
                        : "console.warnicon.sml");
                GUILayout.Label(icon, GUILayout.Width(20f), GUILayout.Height(18f));
                EditorGUILayout.LabelField(issue.Code, EditorStyles.boldLabel);
                using (new EditorGUI.DisabledScope(!issue.CanSelect))
                {
                    if (GUILayout.Button("선택", GUILayout.Width(58f)))
                        TrySelectIssue(issue);
                }
            }

            EditorGUILayout.LabelField(issue.Message, EditorStyles.wordWrappedLabel);
            if (!string.IsNullOrWhiteSpace(issue.AssetPath))
                EditorGUILayout.SelectableLabel(issue.AssetPath, GUILayout.Height(17f));
        }
    }

    private void Scan()
    {
        _report = ScanProject();
        Repaint();
    }

    private void GenerateMissingIds()
    {
        GenerateMissingIds(
            LoadAll<CharacterData>(),
            data => data.CharacterID,
            (data, id) => data.CharacterID = id,
            "character");
        GenerateMissingIds(
            LoadAll<EnemyData>(),
            data => data.EnemyId,
            (data, id) => data.EnemyId = id,
            "enemy");
        GenerateMissingIds(
            LoadAll<SkillData>(),
            data => data.SkillID,
            (data, id) => data.SkillID = id,
            "skill");
        GenerateMissingIds(
            LoadAll<ItemData>(),
            data => data.ItemID,
            (data, id) => data.ItemID = id,
            "item");
        GenerateMissingIds(
            LoadAll<BattleScenarioData>(),
            data => data.ScenarioId,
            (data, id) => data.ScenarioId = id,
            "scenario");
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
            Undo.RegisterCreatedObjectUndo(potion, "Create default potion");
        }

        List<EnemyData> enemies = LoadAll<EnemyData>();
        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyData enemy = enemies[i];
            if (enemy == null || enemy.Drops == null || enemy.Drops.Count > 0)
                continue;
            if (enemy.name.IndexOf("slime", StringComparison.OrdinalIgnoreCase) < 0
                && enemy.EnemyName.IndexOf("slime", StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            Undo.RecordObject(enemy, "Add default enemy drop");
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
        List<GameObject> prefabs = LoadAll<GameObject>(
            "t:Prefab",
            new[] { AssetDatabaseContentSource.DefaultRootPath });
        for (int i = 0; i < prefabs.Count; i++)
        {
            GameObject prefab = prefabs[i];
            if (prefab == null)
                continue;

            PlayerCharacter player = prefab.GetComponent<PlayerCharacter>();
            if (player != null && player.CharacterData != null && player.CharacterData.BattlePrefab == null)
            {
                Undo.RecordObject(player.CharacterData, "Repair character battle prefab");
                player.CharacterData.BattlePrefab = prefab;
                EditorUtility.SetDirty(player.CharacterData);
            }

            EnemyCharacter enemy = prefab.GetComponent<EnemyCharacter>();
            if (enemy != null && enemy.Data != null && enemy.Data.Prefab == null)
            {
                Undo.RecordObject(enemy.Data, "Repair enemy prefab");
                enemy.Data.Prefab = prefab;
                EditorUtility.SetDirty(enemy.Data);
            }
        }

        AssetDatabase.SaveAssets();
        RebuildCatalog(false);
    }

    public static GameContentCatalog RebuildCatalog(bool logResult)
    {
        string catalogPath = AssetDatabaseContentSource.DefaultCatalogAssetPath;
        string directory = Path.GetDirectoryName(catalogPath)?.Replace('\\', '/');
        EnsureAssetFolder(directory);

        GameContentCatalog catalog = AssetDatabase.LoadAssetAtPath<GameContentCatalog>(catalogPath);
        if (catalog == null)
        {
            catalog = CreateInstance<GameContentCatalog>();
            AssetDatabase.CreateAsset(catalog, catalogPath);
            Undo.RegisterCreatedObjectUndo(catalog, "Create Runtime Catalog");
        }
        else
        {
            Undo.RecordObject(catalog, "Rebuild Runtime Catalog");
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
        {
            Debug.Log(
                "[GameContentCatalog] Rebuilt: " + catalog.Characters.Count + " characters, "
                + catalog.Enemies.Count + " enemies, " + catalog.Skills.Count + " skills, "
                + catalog.Items.Count + " items.",
                catalog);
        }

        return catalog;
    }

    private static void GenerateMissingIds<T>(
        IReadOnlyList<T> assets,
        Func<T, string> getId,
        Action<T, string> setId,
        string prefix) where T : UnityEngine.Object
    {
        ContentIdAssignment.AssignMissingIds(
            assets,
            getId,
            setId,
            prefix,
            asset =>
            {
                string path = AssetDatabase.GetAssetPath(asset);
                string guid = AssetDatabase.AssetPathToGUID(path);
                return guid.Length >= 8 ? guid.Substring(0, 8) : "00000000";
            },
            asset => Undo.RecordObject(asset, "Generate content ID"),
            EditorUtility.SetDirty);
    }

    private static List<T> LoadAll<T>() where T : UnityEngine.Object
    {
        return LoadAll<T>(
            "t:" + typeof(T).Name,
            new[] { AssetDatabaseContentSource.DefaultRootPath });
    }

    private static List<T> LoadAll<T>(string filter, string[] folders)
        where T : UnityEngine.Object
    {
        string[] guids = AssetDatabase.FindAssets(filter, folders);
        var paths = new List<string>(guids.Length);
        for (int i = 0; i < guids.Length; i++)
            paths.Add(AssetDatabase.GUIDToAssetPath(guids[i]));
        paths.Sort(StringComparer.Ordinal);

        var result = new List<T>(paths.Count);
        for (int i = 0; i < paths.Count; i++)
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(paths[i]);
            if (asset != null)
                result.Add(asset);
        }

        return result;
    }

    private static void EnsureAssetFolder(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder) || AssetDatabase.IsValidFolder(folder))
            return;

        string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
        EnsureAssetFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
    }
}
#endif
