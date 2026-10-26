#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>명시적 메뉴에서 실험실 데이터 연결만 복구합니다. 씬·스킬 타임라인·본편은 재생성하지 않습니다.</summary>
public static class BunnySlimeElementLabBuilder
{
    public const string AllyRoot = "Assets/_Game/Content/Skills/Ally/BattleLab/Elements";
    public const string EnemyRoot = "Assets/_Game/Content/Skills/Enemy/BunnySlime/Elements";
    public const string ShowcaseAllyRoot = "Assets/_Game/Content/Skills/Ally/BattleLab/Showcase";
    public const string ScreenSplitSkillPath = "Assets/_Game/Content/Skills/Ally/BattleLab/diagonal_screen_slash.asset";
    private const string DataRoot = BunnySlimeBattleLabBuilder.Root + "/Data";
    private static readonly string[] ShowcaseNames =
    {
        "PressureSlash", "TwinPressure", "SteamThrust", "ReverseFlow", "TurbineRush",
        "SteamTraverse", "OverheatDive", "BoilerBurst", "CriticalBreakthrough", "PressureRelease"
    };

    [MenuItem("Hub To Home/테스트/전투 실험실 스킬 연결 복원", false, 141)]
    public static void RestoreConnections()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Play를 종료한 뒤 전투 실험실 연결을 복원하세요.");
            return;
        }
        if (!EditorUtility.DisplayDialog("전투 실험실 스킬 연결 복원",
            "실험실 위젤 한 명에게 전용 스킬 10종과 기본 색·상태 프로필을 연결합니다.\n씬, 스킬 내용, 다른 동료, 적 데이터와 본편 위젤은 변경하지 않습니다.", "복원", "취소")) return;
        ApplyConnections();
    }

    public static void ApplyConnections()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Edit Mode에서만 실험실 연결을 변경할 수 있습니다.");
        var catalog = Require<GameContentCatalog>("Assets/_Game/Resources/HubToHome/GameContentCatalog.asset");
        var profile = Require<ElementalStatusProfile>(DataRoot + "/ElementalStatusLabProfile.asset");
        var front = Require<CharacterData>(DataRoot + "/Party/front.asset");
        if (front.CharacterID != "lab.wizzel.front")
            throw new InvalidOperationException("실험실 위젤 ID가 바뀌어 복원을 중단합니다.");
        var skills = new SkillData[ShowcaseNames.Length];
        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < skills.Length; i++)
        {
            skills[i] = Require<SkillData>(ShowcaseAllyRoot + "/Wizzel_" + ShowcaseNames[i] + ".asset");
            if (string.IsNullOrWhiteSpace(skills[i].SkillID)
                || !skills[i].SkillID.StartsWith("lab.showcase.wizzel.", StringComparison.Ordinal)
                || !ids.Add(skills[i].SkillID) || skills[i].UsageProfile != SkillUsageProfile.PlayerOnly)
                throw new InvalidOperationException("실험실 위젤 스킬의 고유 ID/사용 대상이 올바르지 않습니다: " + ShowcaseNames[i]);
        }
        // 검증은 모두 변경 전에 수행합니다. 기존 ID와 충돌하면 기존 카탈로그를 보존합니다.
        ValidateCatalogIds(catalog.Skills, skills);
        Undo.RecordObjects(new UnityEngine.Object[] { front, catalog }, "Restore Solo Lab Connections");
        front.DefaultSkills = new List<SkillData>(skills);
        front.StatusProfile = profile;
        front.BattleSpriteTint = Color.white;
        front.BattleSymbolColor = new Color(.95f, .72f, .3f);
        front.DisplayName = "위젤";
        Register(catalog.Skills, skills);
        EditorUtility.SetDirty(front);
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssetIfDirty(front);
        AssetDatabase.SaveAssetIfDirty(catalog);
        SkillDatabase.InvalidateCache();
        Debug.Log("위젤 1인 · 전용 스킬 10종 연결 완료. 씬·스킬 내용·적·본편은 변경하지 않았습니다.", front);
    }

    private static T Require<T>(string path) where T : UnityEngine.Object => BunnySlimeLabContentBuilder.Require<T>(path);

    private static void ValidateCatalogIds(List<SkillData> existing, SkillData[] samples)
    {
        foreach (SkillData sample in samples)
            foreach (SkillData item in existing)
                if (item != null && item != sample && item.SkillID == sample.SkillID)
                    throw new InvalidOperationException("기존 카탈로그 ID와 충돌합니다: " + sample.SkillID);
    }

    private static void Register(List<SkillData> target, SkillData[] samples)
    {
        foreach (SkillData sample in samples)
            if (!target.Contains(sample)) target.Add(sample);
    }
}
#endif
