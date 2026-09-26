/// <summary>목록 표시에 필요한 비용/대상 설명. 실행과 최종 유효성 검사는 기존 전투 모듈이 담당합니다.</summary>
public static class BattleMenuEntryPresentation
{
    public static bool CanUse(IMenuEntry entry, PlayerCharacter actor)
    {
        if (entry is EmptyMenuEntry) return false;
        if (entry is SkillMenuEntry skill)
            return skill.Data != null && (actor == null || actor.CurrentAP >= skill.Data.APCost);
        if (entry is ItemMenuEntry item) return item.Data != null && item.Count > 0;
        return entry != null;
    }

    public static string Describe(IMenuEntry entry, PlayerCharacter actor)
    {
        if (entry is SkillMenuEntry skill && skill.Data != null)
        {
            SkillData data = skill.Data;
            string insufficient = actor != null && actor.CurrentAP < data.APCost ? " · AP 부족" : "";
            return $"{data.Description}\n\n<color=#FFDE59>AP {data.APCost}{insufficient}</color>\n{Target(data.TargetType, data.IsAoE)}";
        }
        if (entry is ItemMenuEntry item && item.Data != null)
            return $"{item.Data.Description}\n\n{Target(item.Data.TargetType, item.Data.IsAoE)}\n보유 {item.Count}개";
        return entry?.Description ?? "";
    }

    private static string Target(TargetAreaType type, bool all)
    {
        if (type == TargetAreaType.AoEAll) return "전체 대상";
        string side = type == TargetAreaType.AllyOnly ? "아군" : type == TargetAreaType.EnemyOnly ? "적" : "아군 / 적";
        return side + (all ? " 전체" : " 1명");
    }
}

public sealed class BattleCommandPreviewEntry : IMenuEntry
{
    public string DisplayName { get; }
    public string Description { get; }
    public UnityEngine.Sprite Icon => null;
    public BattleCommandPreviewEntry(string name, string description)
    {
        DisplayName = name;
        Description = description;
    }
}
