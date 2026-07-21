using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct BattleQueuedEnemyAction
{
    public EnemyAction Action;
    public SkillData Skill;
    public int TurnsRemaining;
}

public interface IBattleParticipantCommandHost
{
    CharacterBase FindBattleParticipantBySubjectId(string subjectId);

    string ResolveBattleParticipantSubjectId(CharacterBase target, string fallbackSubjectId);

    void RefreshBattleSessionParticipants();

    void EmitParticipantDamage(CharacterBase target, int damage, bool isPerfect, int previousHp);

    void EmitParticipantHealed(CharacterBase target, int healedAmount);

    void EmitParticipantMpChanged(PlayerCharacter player, int newMp);
}

public interface IBattleCinematicHost
{
    IReadOnlyList<PlayerCharacter> PlayerParty { get; }

    IReadOnlyList<EnemyCharacter> Enemies { get; }

    CharacterBase FindBattleParticipantBySubjectId(string subjectId);

    void SetActorForeground(CharacterBase actor, bool active);

    int ResolveEnemyReturnMoveHash(EnemyCharacter enemy);
}

public interface IBattleTweenCinematicService
{
    IEnumerator SetLetterbox(
        bool visible,
        float thickness,
        float duration,
        object tweenTarget,
        ActionExecutionHandle handle);

    IEnumerator MoveActor(
        string subjectId,
        string anchor,
        float x,
        float y,
        float duration,
        string pose,
        float impact,
        ActionExecutionHandle handle);

    IEnumerator DropActorIn(
        string subjectId,
        float height,
        float hangDuration,
        float fallDuration,
        float settleDuration,
        float impact,
        ActionExecutionHandle handle);

    IEnumerator PlayFakeAttack(
        string actorId,
        string targetId,
        string targetPose,
        float approachDistance,
        float lungeDuration,
        float holdDuration,
        float recoverDuration,
        float impact,
        ActionExecutionHandle handle);

    IEnumerator ReturnActorsToSlots(float duration, ActionExecutionHandle handle);

    IEnumerator PlayCameraShake(
        Vector3 direction,
        float intensity,
        float duration,
        bool lockHorizontal,
        object tweenTarget,
        ActionExecutionHandle handle);

    IEnumerator PlayUiFlash(
        Color color,
        float alpha,
        float duration,
        object tweenTarget,
        ActionExecutionHandle handle);

    IEnumerator PlayUiShake(
        Vector2 strength,
        float duration,
        int vibrato,
        float randomness,
        object tweenTarget,
        ActionExecutionHandle handle);
}

public interface IBattleTurnQteHost
{
    IReadOnlyList<PlayerCharacter> PlayerParty { get; }

    IReadOnlyList<EnemyCharacter> Enemies { get; }

    IList<CharacterBase> TurnQueue { get; }

    IDictionary<EnemyCharacter, BattleQueuedEnemyAction> ReservedEnemyActions { get; }

    WaitForSeconds WaitShort { get; }

    int MaxTurnQueueSize { get; }

    int MpPerTurn { get; }

    int MpOnParryPerfect { get; }

    float EnemyDefenseQteWindow { get; }

    float EnemyAttackVisualDuration { get; }

    float EnemyPostHitDelay { get; }

    float EnemyAoeWindup { get; }

    float PlayerAttackHitDelay { get; }

    float PlayerAttackRecoverDelay { get; }

    Vector3 MeleeAttackOffset { get; }

    Vector3 MeleePullbackOffset { get; }

    int BattleTurnCounter { get; set; }

    int CurrentActorIndex { get; set; }

    PlayerCharacter PendingActor { get; set; }

    PlayerMenuAction PendingAction { get; set; }

    SkillData PendingSkill { get; set; }

    ItemData PendingItem { get; set; }

    BattleState CurrentBattleState { get; }

    bool IsTurnQteCombatInputActive();

    void StartTurnQteCombatLoop();

    void ChangeBattleState(BattleState state);

    bool CheckVictory();

    bool CheckDefeat();

    bool ConsumePlayerPreemptiveAttack();

    void BroadcastVisibleTurnQueue();

    void ResetAllPlayerBattlePoses();

    IEnumerator WaitForNarrationToFinish();

    void TryRequestFlavorNarration();

    void NotifyPlayerTurnStarted(PlayerCharacter player);

    void NotifyEnemyActionStarted(EnemyCharacter enemy, EnemyAttackType attackType);

    void NotifyTargetSelectionStarted(PlayerMenuAction action);

    void RequestNarration(BattleNarrationMessage message);

    IEnumerator RunAwayRoutine();

    void ClearTurnQtePendingActionState();

    Coroutine StartManagedCoroutine(IEnumerator routine);

    void SetActorForeground(CharacterBase actor, bool active);

    void EmitDamage(CharacterBase target, int damage, bool isPerfect);

    void EmitDamage(CharacterBase target, int damage, bool isPerfect, int previousHp);

    void EmitMpChanged(PlayerCharacter player, int newMp);

    void EmitDamageNotificationOnly(CharacterBase target, int damage, bool isPerfect);

    void PublishEnemyHpScenarioEvent(CharacterBase target, int previousHp, int currentHp, int maxHp, BattleRuleTiming timing);

    void PublishEnemyDefeatedScenarioEvent(CharacterBase target, CharacterBase sourceActor);

    void PublishSkillCompletedScenarioEvent(SkillData skill, CharacterBase sourceActor);

    IEnumerator FlushBattleScenarioEvents(BattleRuleTiming timing);

    SkillData ResolveEnemySequenceSkill(EnemyCharacter enemy, EnemyAction action);

    EnemyAttackType ResolveEnemySkillAttackType(SkillData skill);

    IEnumerator MoveEnemyToCenterIfNeeded(EnemyCharacter enemy);

    int ResolveEnemyReturnMoveHash(EnemyCharacter enemy);
}