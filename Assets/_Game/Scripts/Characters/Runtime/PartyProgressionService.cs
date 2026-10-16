using System;
using System.Collections.Generic;

[Serializable]
public sealed class PartyProgressionSaveData
{
    public bool IsInitialized;
    public int Level = 1;
    public int EXP;
    public string CurveCharacterId = string.Empty;

    public PartyProgressionSaveData Clone() => new PartyProgressionSaveData
    {
        IsInitialized = IsInitialized, Level = Level, EXP = EXP,
        CurveCharacterId = CurveCharacterId ?? string.Empty
    };
}

/// <summary>파티 EXP는 한 번만 계산하고 개인 투자 포인트만 각 멤버에게 지급합니다.</summary>
public static class PartyProgressionService
{
    public const int MaxLevel = 99;

    // 저장 코덱에서도 사용하므로 카탈로그/씬/Unity 객체를 조회하지 않습니다.
    public static PartyProgressionSaveData NormalizeSnapshot(
        PartyProgressionSaveData source, IReadOnlyList<CharacterSaveData> members)
    {
        var result = source?.Clone() ?? new PartyProgressionSaveData();
        if (!result.IsInitialized)
        {
            result.Level = 1;
            result.EXP = 0;
            if (members != null)
                for (int i = 0; i < members.Count; i++)
                {
                    var member = members[i];
                    if (member == null) continue;
                    if (string.IsNullOrEmpty(result.CurveCharacterId))
                        result.CurveCharacterId = member.CharacterDataID ?? string.Empty;
                    int level = Math.Clamp(member.Level, 1, MaxLevel);
                    int experience = Math.Max(0, member.EXP);
                    if (level > result.Level || (level == result.Level && experience > result.EXP))
                    {
                        result.Level = level;
                        result.EXP = experience;
                    }
                }
            result.IsInitialized = true;
        }
        result.Level = Math.Clamp(result.Level, 1, MaxLevel);
        result.EXP = result.Level == MaxLevel ? 0 : Math.Max(0, result.EXP);
        result.CurveCharacterId = result.CurveCharacterId?.Trim() ?? string.Empty;
        return result;
    }

    public static void SynchronizeMember(CharacterSaveData member, CharacterData data, int level, int experience)
    {
        if (member == null) return;
        member.Level = Math.Clamp(member.Level, 1, MaxLevel);
        EnsureGrowth(member, data);
        int completed = Math.Max(0, Math.Clamp(level, 1, MaxLevel) - member.Level);
        if (completed > 0)
        {
            var profile = data != null ? data.GrowthProfile : null;
            member.Growth.AttributePointsEarned = Saturate((long)member.Growth.AttributePointsEarned
                + (long)completed * CharacterGrowthService.ResolveAttributePointsPerLevel(profile));
            member.Growth.SkillPointsEarned = Saturate((long)member.Growth.SkillPointsEarned
                + (long)completed * CharacterGrowthService.ResolveSkillPointsPerLevel(profile));
        }
        member.Level = Math.Clamp(level, 1, MaxLevel);
        member.EXP = member.Level == MaxLevel ? 0 : Math.Max(0, experience);
        // 레벨은 능력치를 올리지 않습니다. 기본값 + 이미 투자한 값만 유지합니다.
        if (data != null) CharacterGrowthService.EnsureInitialized(member, data);
    }

    public static List<CharacterLevelUpResult> Grant(
        PartyProgressionSaveData state, IReadOnlyList<CharacterSaveData> members,
        Func<string, CharacterData> resolveData, int amount)
    {
        if (state == null) throw new ArgumentNullException(nameof(state));
        var results = new List<CharacterLevelUpResult>();
        int gained = Math.Max(0, amount);
        var curve = resolveData?.Invoke(state.CurveCharacterId);
        long pool = (long)Math.Max(0, state.EXP) + gained;
        state.Level = Math.Clamp(state.Level, 1, MaxLevel);
        while (state.Level < MaxLevel)
        {
            int required = CharacterProgressionService.ExperienceRequiredForNextLevel(curve, state.Level);
            if (pool < required) break;
            pool -= required;
            state.Level++;
        }
        state.EXP = state.Level == MaxLevel ? 0 : Saturate(pool);
        state.IsInitialized = true;
        if (members == null) return results;
        for (int i = 0; i < members.Count; i++)
        {
            var member = members[i];
            if (member == null) continue;
            var data = resolveData?.Invoke(member.CharacterDataID);
            EnsureGrowth(member, data);
            int previous = member.Level;
            int oldAttributes = member.Growth.AttributePointsEarned;
            int oldSkills = member.Growth.SkillPointsEarned;
            SynchronizeMember(member, data, state.Level, state.EXP);
            results.Add(new CharacterLevelUpResult
            {
                CharacterDataId = member.CharacterDataID, PreviousLevel = previous,
                NewLevel = state.Level, ExperienceGained = gained, RemainingExperience = state.EXP,
                AttributePointsGained = member.Growth.AttributePointsEarned - oldAttributes,
                SkillPointsGained = member.Growth.SkillPointsEarned - oldSkills,
                AvailableAttributePoints = member.Growth.AvailableAttributePoints,
                AvailableSkillPoints = member.Growth.AvailableSkillPoints
            });
        }
        return results;
    }

    private static void EnsureGrowth(CharacterSaveData member, CharacterData data)
    {
        if (data != null)
        {
            CharacterGrowthService.EnsureInitialized(member, data);
            return;
        }
        member.Growth ??= new CharacterGrowthSaveData();
        member.Growth.Investments ??= new CharacterStatInvestments();
        member.Growth.SkillTreeUnlocks ??= new List<SkillTreeUnlockSaveData>();
        if (member.Growth.IsInitialized) return;
        int completed = Math.Clamp(member.Level, 1, MaxLevel) - 1;
        member.Growth.AttributePointsEarned = Math.Max(member.Growth.Investments.Total,
            completed * GrowthBalanceProfile.DefaultAttributePointsPerLevel);
        member.Growth.SkillPointsEarned = Math.Max(member.Growth.SkillPointsSpent,
            completed * GrowthBalanceProfile.DefaultSkillPointsPerLevel);
        member.Growth.IsInitialized = true;
    }

    private static int Saturate(long value) => (int)Math.Clamp(value, 0L, int.MaxValue);
}
