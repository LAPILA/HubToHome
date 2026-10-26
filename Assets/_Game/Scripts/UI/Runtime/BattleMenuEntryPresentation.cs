/// <summary>목록 표시에 필요한 비용/대상 설명. 실행과 최종 유효성 검사는 기존 전투 모듈이 담당합니다.</summary>
public static class BattleMenuEntryPresentation
{
    public static bool CanUse(IMenuEntry entry, PlayerCharacter actor)
    {
        if (entry is EmptyMenuEntry) return false;
        if (entry is SkillMenuEntry skill)
            return skill.Data != null && (actor == null
                || (!actor.IsBattleSkillLocked(skill.Data) && actor.CurrentAP >= skill.Data.APCost));
        if (entry is ItemMenuEntry item) return item.Data != null && item.Count > 0;
        return entry != null;
    }

    public static string UnavailableReason(IMenuEntry entry, PlayerCharacter actor)
    {
        if (entry is SkillMenuEntry skill && actor != null && skill.Data != null)
        {
            if (actor.IsBattleSkillLocked(skill.Data))
                return LocalizationManager.Localize("battle.skill_locked", "사용 불가 · 잠긴 스킬입니다.");
            if (actor.CurrentAP < skill.Data.APCost) return L("AP_INSUFFICIENT", "AP 부족").Trim();
        }
        return string.Empty;
    }

    public static string Describe(IMenuEntry entry, PlayerCharacter actor)
    {
        if (entry is SkillMenuEntry skill && skill.Data != null)
        {
            SkillData data = skill.Data;
            string insufficient = actor != null && actor.CurrentAP < data.APCost ? L("AP_INSUFFICIENT", " · AP 부족") : "";
            string locked = actor != null && actor.IsBattleSkillLocked(data)
                ? "<color=#FF7373>" + UnavailableReason(entry, actor) + "</color>\n\n" : "";
            string resource = data.ResourceEffect?.Describe(actor != null ? actor.BattleResource : null) ?? "";
            return $"{locked}{data.LocalizedDescription}\n\n<color=#FFDE59>AP {data.APCost}{insufficient}</color>\n{Target(data.TargetType, data.IsAoE)}{resource}";
        }
        if (entry is ItemMenuEntry item && item.Data != null)
            return $"{item.Data.LocalizedDescription}\n\n{Target(item.Data.TargetType, item.Data.IsAoE)}\n{string.Format(L("ITEM_COUNT", "보유 {0}개"), item.Count)}";
        return entry?.Description ?? "";
    }

    private static string Target(TargetAreaType type, bool all)
    {
        if (type == TargetAreaType.AoEAll) return L("TARGET_EVERYONE", "전체 대상");
        if (type == TargetAreaType.AllyOnly) return L(all ? "TARGET_ALL_ALLIES" : "TARGET_ALLY", all ? "아군 전체" : "아군 1명");
        if (type == TargetAreaType.EnemyOnly) return L(all ? "TARGET_ALL_ENEMIES" : "TARGET_ENEMY", all ? "적 전체" : "적 1명");
        return L(all ? "TARGET_ALL_BOTH" : "TARGET_BOTH", all ? "아군 / 적 전체" : "아군 / 적 1명");
    }

    private static string L(string key, string fallback) => LocalizationManager.Localize("HTH_PROLOGUE_BATTLE_" + key, fallback);
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
