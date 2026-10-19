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
    public const string ScreenSplitSkillPath = "Assets/_Game/Content/Skills/Ally/BattleLab/diagonal_screen_slash.asset";
    private const string DataRoot = BunnySlimeBattleLabBuilder.Root + "/Data";
    private static readonly string[] AllyNames =
    {
        "front_ember_cut", "front_rake", "front_furnace", "front_ignition", "front_rupture", "front_pressure_release",
        "support_chill", "support_freeze", "support_poison", "support_detonate", "support_cleanse", "support_heal",
        "striker_charge", "striker_arc_pair", "striker_arc_field", "striker_rapid50", "striker_stun", "striker_discharge"
    };
    private static readonly string[] EnemyNames = { "enemy_burn", "enemy_chill", "enemy_electric", "enemy_bleed", "enemy_poison" };

    [MenuItem("Hub To Home/테스트/원소 실험실 기본 연결 복원", false, 141)]
    public static void RestoreConnections()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Play를 종료한 뒤 원소 실험실 연결을 복원하세요.");
            return;
        }
        if (!EditorUtility.DisplayDialog("원소 실험실 연결 복원",
            "실험실 전열 3명의 기본 6스킬·색·상태 프로필과 토끼 공격 목록을 복원합니다. 첫 위젤의 사선 참격도 연결합니다.\n씬, 기존 스킬 내용, 후열 3명, ZEV, 본편 위젤은 변경하지 않습니다.", "복원", "취소")) return;
        ApplyConnections();
    }

    public static void ApplyConnections()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Edit Mode에서만 실험실 연결을 변경할 수 있습니다.");
        var catalog = Require<GameContentCatalog>("Assets/_Game/Resources/HubToHome/GameContentCatalog.asset");
        var profile = Require<ElementalStatusProfile>(DataRoot + "/ElementalStatusLabProfile.asset");
        var enemy = Require<EnemyData>(DataRoot + "/Enemy/BunnySlime_Lab.asset");
        if (enemy.EnemyId != "lab.bunny_slime") throw new InvalidOperationException("실험실 토끼 ID가 바뀌어 복원을 중단합니다.");
        var skills = new SkillData[AllyNames.Length];
        var enemySkills = new SkillData[EnemyNames.Length];
        for (int i = 0; i < skills.Length; i++) skills[i] = RequireSkill(AllyRoot, AllyNames[i]);
        for (int i = 0; i < enemySkills.Length; i++) enemySkills[i] = RequireSkill(EnemyRoot, EnemyNames[i]);
        SkillData screenSplit = Require<SkillData>(ScreenSplitSkillPath);
        if (screenSplit.SkillID != "lab.presentation.diagonal_screen_slash")
            throw new InvalidOperationException("사선 참격 실험실 스킬 ID가 바뀌어 복원을 중단합니다.");
        SkillData[] presentationSkills = { screenSplit };
        string commonEnemyRoot = "Assets/_Game/Content/Skills/Enemy/BunnySlime/";
        var ordered = new List<SkillData>
        {
            enemySkills[0], enemySkills[1], enemySkills[2],
            Require<SkillData>(commonEnemyRoot + "enemy_dodge.asset"),
            Require<SkillData>(commonEnemyRoot + "enemy_double_shot.asset"),
            enemySkills[3], enemySkills[4],
            Require<SkillData>(commonEnemyRoot + "enemy_dodge_shot.asset")
        };
        string[] names = { "front", "support", "striker" };
        string[] displayNames = { "위젤", "위젤 · 청록", "위젤 · 보라" };
        Color[] tints = { Color.white, new Color(.3f, .85f, .78f), new Color(.74f, .49f, 1f) };
        var party = new CharacterData[3];
        for (int i = 0; i < party.Length; i++)
        {
            party[i] = Require<CharacterData>(DataRoot + "/Party/" + names[i] + ".asset");
            if (party[i].CharacterID != "lab.wizzel." + names[i])
                throw new InvalidOperationException("실험실 캐릭터 ID가 바뀌어 복원을 중단합니다.");
        }
        // 검증은 모두 변경 전에 수행합니다. 기존 ID와 충돌하면 기존 카탈로그를 보존합니다.
        ValidateCatalogIds(catalog.Skills, skills);
        ValidateCatalogIds(catalog.Skills, enemySkills);
        ValidateCatalogIds(catalog.Skills, presentationSkills);
        Undo.RecordObjects(new UnityEngine.Object[] { party[0], party[1], party[2], enemy, catalog }, "Restore Element Lab Connections");
        for (int i = 0; i < party.Length; i++)
        {
            party[i].DefaultSkills = new List<SkillData>();
            for (int j = 0; j < 6; j++) party[i].DefaultSkills.Add(skills[i * 6 + j]);
            if (i == 0) party[i].DefaultSkills.Add(screenSplit);
            party[i].StatusProfile = profile;
            party[i].BattleSpriteTint = tints[i];
            party[i].BattleSymbolColor = i == 0 ? new Color(.95f, .72f, .3f) : tints[i];
            party[i].DisplayName = displayNames[i];
            EditorUtility.SetDirty(party[i]);
            AssetDatabase.SaveAssetIfDirty(party[i]);
        }
        enemy.StatusProfile = profile;
        enemy.SkillList = ordered;
        enemy.UseOrderedSkills = true;
        Register(catalog.Skills, skills);
        Register(catalog.Skills, enemySkills);
        Register(catalog.Skills, presentationSkills);
        EditorUtility.SetDirty(enemy);
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssetIfDirty(enemy);
        AssetDatabase.SaveAssetIfDirty(catalog);
        SkillDatabase.InvalidateCache();
        Debug.Log("원소 실험실 연결 복원 완료. 씬·스킬 타임라인·ZEV·후열·본편은 변경하지 않았습니다.", enemy);
    }

    private static T Require<T>(string path) where T : UnityEngine.Object => BunnySlimeLabContentBuilder.Require<T>(path);

    private static SkillData RequireSkill(string folder, string name)
    {
        SkillData skill = Require<SkillData>(folder + "/" + name + ".asset");
        if (skill.SkillID != "lab.elements." + name)
            throw new InvalidOperationException("원소 실험실 스킬 ID가 바뀌었습니다: " + name);
        return skill;
    }

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
