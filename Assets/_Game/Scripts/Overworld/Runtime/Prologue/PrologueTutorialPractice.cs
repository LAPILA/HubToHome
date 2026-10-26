using System;

public enum PrologueTutorialLessonStage { Attack, Guard, JustGuard, Item, Complete }

/// <summary>실제 완료 결과만 소비하는 학습 진행입니다. 피해·방어 판정에는 관여하지 않습니다.</summary>
public sealed class PrologueTutorialPractice
{
    private readonly int _maxDefenseAttempts;
    private readonly int _maxItemActions;
    private int _defenseAttempts;
    private int _itemActions;

    public PrologueTutorialLessonStage Stage { get; private set; } = PrologueTutorialLessonStage.Attack;

    public PrologueTutorialPractice(int maxDefenseAttempts, int maxItemActions)
    {
        _maxDefenseAttempts = Math.Max(1, maxDefenseAttempts);
        _maxItemActions = Math.Max(1, maxItemActions);
    }

    public void RecordPlayerAction(bool completedAttack, bool consumedItem)
    {
        if (Stage == PrologueTutorialLessonStage.Attack && completedAttack)
        { Stage = PrologueTutorialLessonStage.Guard; _defenseAttempts = 0; }
        else if (Stage == PrologueTutorialLessonStage.Item)
        {
            _itemActions++;
            if (consumedItem || _itemActions >= _maxItemActions) Stage = PrologueTutorialLessonStage.Complete;
        }
    }

    public void RecordDefense(bool guarded, bool justGuarded)
    {
        if (Stage != PrologueTutorialLessonStage.Guard && Stage != PrologueTutorialLessonStage.JustGuard) return;
        _defenseAttempts++;
        if (Stage == PrologueTutorialLessonStage.Guard && (guarded || justGuarded))
        { Stage = PrologueTutorialLessonStage.JustGuard; _defenseAttempts = 0; }
        else if (justGuarded || _defenseAttempts >= _maxDefenseAttempts)
        { Stage = PrologueTutorialLessonStage.Item; _itemActions = 0; }
    }
}
