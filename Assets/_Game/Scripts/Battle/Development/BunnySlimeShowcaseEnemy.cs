using UnityEngine;

/// <summary>
/// 전투 실습에서 빠지는 패턴이 없도록 SkillList를 순서대로 사용하는 샘플 전용 AI입니다.
/// 공격 실행, 방어 판정과 턴 진행은 일반 적과 같은 전투 서비스가 담당합니다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BunnySlimeShowcaseEnemy : EnemyCharacter
{
    [Header("실습 전용 HP 페이즈")]
    public bool UseHealthPhases = true;
    [Min(1)] public int Phase2StartIndex = 3;
    [Min(2)] public int Phase3StartIndex = 6;

    private int _nextSkillIndex;
    private int _lastPhase = -1;

    private void OnEnable()
    {
        _nextSkillIndex = 0;
        _lastPhase = -1;
    }

    public override EnemyAction DecideAction()
    {
        int count = GetAvailableSkillCount(out _, out _);
        if (Data != null && Data.SkillList != null)
        {
            for (int i = 0; i < count; i++)
            {
                if (Data.SkillList[i] != null)
                    return EnemyAction.UseSkill;
            }
        }

        return EnemyAction.Wait;
    }

    public override SkillData SelectSkill(EnemyAction action)
    {
        if (action != EnemyAction.UseSkill || Data == null || Data.SkillList == null)
            return null;

        int count = GetAvailableSkillCount(out int phase, out int startIndex);
        if (phase != _lastPhase)
        {
            _lastPhase = phase;
            _nextSkillIndex = startIndex;
        }
        for (int attempt = 0; attempt < count; attempt++)
        {
            int index = _nextSkillIndex % count;
            _nextSkillIndex = (index + 1) % count;
            SkillData skill = Data.SkillList[index];
            if (skill != null)
                return skill;
        }

        return null;
    }

    private int GetAvailableSkillCount(out int phase, out int startIndex)
    {
        phase = -1;
        startIndex = 0;
        int count = Data != null && Data.SkillList != null ? Data.SkillList.Count : 0;
        // 짧은 단독 실습 목록과 잘못 설정된 페이즈 경계는 기존의 전체 순환을 유지합니다.
        if (!UseHealthPhases || count <= 6 || Phase2StartIndex <= 0
            || Phase3StartIndex <= Phase2StartIndex || Phase3StartIndex >= count)
            return count;

        long hpPercent = (long)CurrentHP * 100;
        long maxHp = Mathf.Max(1, MaxHP);
        if (hpPercent <= maxHp * 33)
        {
            phase = 2;
            startIndex = Phase3StartIndex;
            return count;
        }
        if (hpPercent <= maxHp * 66)
        {
            phase = 1;
            startIndex = Phase2StartIndex;
            return Phase3StartIndex;
        }

        phase = 0;
        return Phase2StartIndex;
    }
}
