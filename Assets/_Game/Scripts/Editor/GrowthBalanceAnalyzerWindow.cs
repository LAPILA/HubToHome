using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public sealed class GrowthBalanceAnalyzerWindow : EditorWindow
{
    private const string DefaultProfilePath =
        "Assets/_Game/Content/Characters/Growth/GrowthBalance_Default.asset";

    [SerializeField] private GrowthBalanceProfile _profile;
    [SerializeField] private bool _useLogarithmicScale = true;
    private Vector2 _scrollPosition;

    [MenuItem("Hub To Home/Growth/Balance Analyzer")]
    public static void Open()
    {
        GetWindow<GrowthBalanceAnalyzerWindow>("Growth Balance");
    }

    private void OnEnable()
    {
        minSize = new Vector2(700f, 540f);
        if (_profile == null)
        {
            _profile = AssetDatabase.LoadAssetAtPath<GrowthBalanceProfile>(
                DefaultProfilePath);
        }
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Growth Balance Analyzer", EditorStyles.boldLabel);
        EditorGUILayout.Space(4f);

        _profile = (GrowthBalanceProfile)EditorGUILayout.ObjectField(
            "Balance Profile",
            _profile,
            typeof(GrowthBalanceProfile),
            false);

        if (_profile == null)
        {
            EditorGUILayout.HelpBox(
                "GrowthBalanceProfile을 선택하세요.",
                MessageType.Info);
            return;
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Select Profile", GUILayout.Width(110f)))
                Selection.activeObject = _profile;
            if (GUILayout.Button("Export CSV", GUILayout.Width(110f)))
                ExportCsv();
            GUILayout.FlexibleSpace();
            _useLogarithmicScale = EditorGUILayout.ToggleLeft(
                "Logarithmic EXP graph",
                _useLogarithmicScale,
                GUILayout.Width(175f));
        }

        EditorGUILayout.Space(6f);
        DrawSummary();
        EditorGUILayout.Space(8f);
        DrawExperienceGraph();
        EditorGUILayout.Space(8f);
        DrawLevelTable();
    }

    private void DrawSummary()
    {
        int maxLevel = _profile.ResolveMaxLevel();
        long totalExperience =
            CharacterProgressionService.CumulativeExperienceRequiredForLevel(
                _profile,
                maxLevel);
        int totalAttributePoints = SaturatingMultiply(
            maxLevel - 1,
            Mathf.Max(0, _profile.AttributePointsPerLevel));
        int totalSkillPoints = SaturatingMultiply(
            maxLevel - 1,
            Mathf.Max(0, _profile.SkillPointsPerLevel));

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField(
                $"Level 1-{maxLevel}    Total EXP {FormatNumber(totalExperience)}    " +
                $"Attribute Points {FormatNumber(totalAttributePoints)}    " +
                $"Skill Points {FormatNumber(totalSkillPoints)}");
            EditorGUILayout.LabelField(
                $"VIT +{_profile.HealthPerVitalityRank} HP    " +
                $"ATK +{_profile.AttackPerRank}    " +
                $"DEF +{_profile.DefensePerRank}    " +
                $"SPD +{_profile.SpeedPerRank}    " +
                $"AP +{_profile.ActionPointsPerRank}    " +
                $"Rank cap {_profile.ResolveMaxInvestmentRank()}");
        }
    }

    private void DrawExperienceGraph()
    {
        int maxLevel = _profile.ResolveMaxLevel();
        Rect rect = GUILayoutUtility.GetRect(
            100f,
            210f,
            GUILayout.ExpandWidth(true));
        Color background = EditorGUIUtility.isProSkin
            ? new Color(0.12f, 0.12f, 0.12f)
            : new Color(0.91f, 0.91f, 0.91f);
        EditorGUI.DrawRect(rect, background);

        Rect graph = new Rect(
            rect.x + 54f,
            rect.y + 18f,
            Mathf.Max(1f, rect.width - 70f),
            Mathf.Max(1f, rect.height - 46f));
        DrawGrid(graph);

        int pointCount = Mathf.Max(1, maxLevel - 1);
        var points = new Vector3[pointCount];
        float maximumValue = 1f;
        for (int level = 1; level < maxLevel; level++)
        {
            int requirement =
                CharacterProgressionService.ExperienceRequiredForNextLevel(
                    _profile,
                    level);
            maximumValue = Mathf.Max(maximumValue, GraphValue(requirement));
        }

        for (int index = 0; index < pointCount; index++)
        {
            int level = index + 1;
            int requirement =
                CharacterProgressionService.ExperienceRequiredForNextLevel(
                    _profile,
                    level);
            float x = pointCount <= 1
                ? graph.x
                : Mathf.Lerp(graph.x, graph.xMax, index / (float)(pointCount - 1));
            float y = Mathf.Lerp(
                graph.yMax,
                graph.y,
                GraphValue(requirement) / maximumValue);
            points[index] = new Vector3(x, y, 0f);
        }

        Handles.BeginGUI();
        Handles.color = new Color(0.20f, 0.78f, 0.88f);
        Handles.DrawAAPolyLine(2.5f, points);
        Handles.EndGUI();

        GUI.Label(
            new Rect(rect.x + 6f, rect.y + 2f, rect.width - 12f, 18f),
            _useLogarithmicScale
                ? "EXP required per level (log scale)"
                : "EXP required per level");
        GUI.Label(
            new Rect(graph.x, graph.yMax + 3f, 80f, 18f),
            "LV 1");
        GUI.Label(
            new Rect(graph.xMax - 80f, graph.yMax + 3f, 80f, 18f),
            $"LV {maxLevel - 1}",
            EditorStyles.miniLabel);
        GUI.Label(
            new Rect(rect.x + 4f, graph.y - 8f, 48f, 18f),
            FormatNumber(
                CharacterProgressionService.ExperienceRequiredForNextLevel(
                    _profile,
                    Mathf.Max(1, maxLevel - 1))),
            EditorStyles.miniLabel);
    }

    private void DrawLevelTable()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            Header("Level", 55f);
            Header("Next EXP", 100f);
            Header("Cumulative EXP", 130f);
            Header("Attribute Total", 110f);
            Header("Skill Total", 90f);
        }

        _scrollPosition = EditorGUILayout.BeginScrollView(
            _scrollPosition,
            GUILayout.MinHeight(160f));
        int maxLevel = _profile.ResolveMaxLevel();
        for (int level = 1; level <= maxLevel; level++)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                Value(level.ToString(CultureInfo.InvariantCulture), 55f);
                Value(
                    level < maxLevel
                        ? FormatNumber(
                            CharacterProgressionService.ExperienceRequiredForNextLevel(
                                _profile,
                                level))
                        : "MAX",
                    100f);
                Value(
                    FormatNumber(
                        CharacterProgressionService.CumulativeExperienceRequiredForLevel(
                            _profile,
                            level)),
                    130f);
                Value(
                    FormatNumber(
                        SaturatingMultiply(
                            level - 1,
                            Mathf.Max(0, _profile.AttributePointsPerLevel))),
                    110f);
                Value(
                    FormatNumber(
                        SaturatingMultiply(
                            level - 1,
                            Mathf.Max(0, _profile.SkillPointsPerLevel))),
                    90f);
            }
        }
        EditorGUILayout.EndScrollView();
    }

    private void ExportCsv()
    {
        string path = EditorUtility.SaveFilePanel(
            "Export Growth Balance CSV",
            Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty,
            _profile.name + "_Levels_1-" + _profile.ResolveMaxLevel(),
            "csv");
        if (string.IsNullOrWhiteSpace(path))
            return;

        var builder = new StringBuilder();
        builder.AppendLine(
            "Level,NextExperience,CumulativeExperience,AttributePointsTotal,SkillPointsTotal");
        int maxLevel = _profile.ResolveMaxLevel();
        for (int level = 1; level <= maxLevel; level++)
        {
            int nextExperience = level < maxLevel
                ? CharacterProgressionService.ExperienceRequiredForNextLevel(
                    _profile,
                    level)
                : 0;
            long cumulative =
                CharacterProgressionService.CumulativeExperienceRequiredForLevel(
                    _profile,
                    level);
            int attributePoints = SaturatingMultiply(
                level - 1,
                Mathf.Max(0, _profile.AttributePointsPerLevel));
            int skillPoints = SaturatingMultiply(
                level - 1,
                Mathf.Max(0, _profile.SkillPointsPerLevel));
            builder.Append(level.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(nextExperience.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(cumulative.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(attributePoints.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(skillPoints.ToString(CultureInfo.InvariantCulture)).AppendLine();
        }

        File.WriteAllText(path, builder.ToString(), new UTF8Encoding(false));
        if (path.StartsWith(
            Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty,
            StringComparison.OrdinalIgnoreCase))
        {
            AssetDatabase.Refresh();
        }
    }

    private float GraphValue(int experience)
    {
        return _useLogarithmicScale
            ? Mathf.Log10(Mathf.Max(1, experience))
            : Mathf.Max(0, experience);
    }

    private static void DrawGrid(Rect graph)
    {
        Handles.BeginGUI();
        Handles.color = EditorGUIUtility.isProSkin
            ? new Color(1f, 1f, 1f, 0.12f)
            : new Color(0f, 0f, 0f, 0.16f);
        for (int index = 0; index <= 4; index++)
        {
            float y = Mathf.Lerp(graph.y, graph.yMax, index / 4f);
            Handles.DrawLine(
                new Vector3(graph.x, y),
                new Vector3(graph.xMax, y));
        }
        Handles.EndGUI();
    }

    private static void Header(string text, float width)
    {
        GUILayout.Label(text, EditorStyles.miniBoldLabel, GUILayout.Width(width));
    }

    private static void Value(string text, float width)
    {
        EditorGUILayout.LabelField(text, GUILayout.Width(width));
    }

    private static string FormatNumber(long value)
    {
        return value.ToString("N0", CultureInfo.InvariantCulture);
    }

    private static int SaturatingMultiply(int left, int right)
    {
        long product = (long)Mathf.Max(0, left) * Mathf.Max(0, right);
        return product >= int.MaxValue ? int.MaxValue : (int)product;
    }
}