using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public sealed class BattleTurnQteModuleControllerService : IBattleTurnQteModuleController
{
    private const float PlayerAttackReadyDuration = 0.08f;

    private readonly IBattleTurnQteHost _host;
    private readonly BattleLinkCounterService _linkCounterService;
    private BattleCameraActionScope _activeCameraScope;

    public BattleTurnQteModuleControllerService(IBattleTurnQteHost host, BattleLinkCounterService linkCounterService = null)
    {
        _host = host;
        _linkCounterService = linkCounterService ?? new BattleLinkCounterService(host);
    }

    public IEnumerator EnterTurnQteModule(GameModuleRuntimeContext context)
    {
        BattleUIController.Instance?.ResumeBattleModuleInput();
        yield break;
    }

    public IEnumerator ExitTurnQteModule(GameModuleRuntimeContext context)
    {
        _linkCounterService.CancelActive();
        CancelActiveCameraPresentation();
        QTEManager.Instance?.ForceStop();
        ClearDefenseInputBuffers();
        _host?.ClearTurnQtePendingActionState();
        BattleUIController.Instance?.SuspendBattleModuleInput();
        yield break;
    }

    public IEnumerator StartTurnQteModule(GameModuleRuntimeContext context)
    {
        BattleUIController.Instance?.ResumeBattleModuleInput();
        _host?.StartTurnQteCombatLoop();
        yield break;
    }

    public IEnumerator RunTurnCalculation()
    {
        if (_host == null || !_host.IsTurnQteCombatInputActive())
        {
            yield break;
        }

        yield return null;
        _host.TurnQueue.Clear();

        if (_host.Enemies == null || _host.Enemies.Count == 0)
        {
            Debug.LogError("[BattleTurnQteModuleControllerService] 전투 시작 시 적 리스트가 비어 있습니다. EnemyData.Prefab 또는 EnemyCharacter 설정을 확인해주세요.");
            yield break;
        }

        var aliveChars = new List<CharacterBase>();
        AddAlivePlayers(aliveChars);
        AddAliveEnemies(aliveChars);

        if (aliveChars.Count == 0 || _host.CheckVictory() || _host.CheckDefeat())
        {
            CompleteAction();
            yield break;
        }

        aliveChars.Sort((a, b) => b.SPD.CompareTo(a.SPD));
        for (int i = 0; i < _host.MaxTurnQueueSize; i++)
            _host.TurnQueue.Add(aliveChars[i % aliveChars.Count]);

        if (_host.ConsumePlayerPreemptiveAttack())
            BattleTurnQueuePolicy.PromoteFirstPlayer(_host.TurnQueue);

        _host.CurrentActorIndex = 0;
        _host.BroadcastVisibleTurnQueue();
        yield return _host.WaitShort;

        AdvanceTurn();
    }

    public void AdvanceTurn()
    {
        if (_host == null || !_host.IsTurnQteCombatInputActive())
        {
            return;
        }

        if (_host.CurrentActorIndex >= _host.TurnQueue.Count)
        {
            _host.ChangeBattleState(BattleState.TurnCalc);
            return;
        }

        CharacterBase actor = _host.TurnQueue[_host.CurrentActorIndex++];
        if (actor == null || !actor.IsAlive)
        {
            _host.BroadcastVisibleTurnQueue();
            AdvanceTurn();
            return;
        }

        // Remember the restriction before ticking: a one-turn stun still skips this turn.
        bool canTakeTurn = actor.CanTakeTurn();
        actor.ProcessEffects();
        if (!actor.IsAlive || !canTakeTurn || !actor.CanTakeTurn())
        {
            _host.BroadcastVisibleTurnQueue();
            CompleteAction();
            return;
        }

        if (actor is PlayerCharacter player)
        {
            _host.BattleTurnCounter++;
            _host.StartManagedCoroutine(BeginPlayerTurn(player));
        }
        else if (actor is EnemyCharacter)
        {
            _host.StartManagedCoroutine(BeginEnemyTurn());
        }
    }

    public IEnumerator BeginPlayerTurn(PlayerCharacter player)
    {
        if (_host == null || player == null || !_host.IsTurnQteCombatInputActive())
        {
            yield break;
        }

        _host.ResetAllPlayerBattlePoses();
        player.GetComponent<PlayerController>()?.PlayBattleAnim(PlayerCharacter.HashBattleIdle);
        player.RestoreAP(_host.ApPerTurn);
        _host.EmitApChanged(player, player.CurrentAP);
        if (player.TryShowBattleSpeech(BattleSpeechTrigger.TurnStart, null, null, _host.BattleTurnCounter))
        {
            yield return _host.StartManagedCoroutine(player.WaitForBattleSpeech());
        }

        _host.TryRequestFlavorNarration();
        yield return _host.StartManagedCoroutine(_host.WaitForNarrationToFinish());
        _host.NotifyPlayerTurnStarted(player);
        _host.ChangeBattleState(BattleState.PlayerActionSelect);
    }

    public IEnumerator BeginEnemyTurn()
    {
        if (_host == null || !_host.IsTurnQteCombatInputActive())
        {
            yield break;
        }

        _host.ResetAllPlayerBattlePoses();
        yield return _host.StartManagedCoroutine(_host.WaitForNarrationToFinish());
        _host.ChangeBattleState(BattleState.EnemyAction);
    }

    public IEnumerator RunEnemyAction()
    {
        if (_host == null || !_host.IsTurnQteCombatInputActive())
        {
            yield break;
        }

        EnemyCharacter enemy = _host.CurrentActorIndex > 0 && _host.CurrentActorIndex - 1 < _host.TurnQueue.Count
            ? _host.TurnQueue[_host.CurrentActorIndex - 1] as EnemyCharacter
            : null;
        if (enemy == null)
        {
            CompleteAction();
            yield break;
        }

        SkillData enemySkill = null;
        bool actionExecuted = false;
        EnemyAction action;
        bool isExecutingReservedAction = false;

        if (_host.ReservedEnemyActions.TryGetValue(enemy, out BattleQueuedEnemyAction reservedAction))
        {
            reservedAction.TurnsRemaining--;
            if (reservedAction.TurnsRemaining > 0)
            {
                _host.ReservedEnemyActions[enemy] = reservedAction;
                CompleteAction();
                yield break;
            }

            action = reservedAction.Action;
            enemySkill = reservedAction.Skill;
            _host.ReservedEnemyActions.Remove(enemy);
            isExecutingReservedAction = true;
        }
        else
        {
            action = enemy.DecideAction();
            enemySkill = _host.ResolveEnemySequenceSkill(enemy, action);
        }

        if (action == EnemyAction.Wait)
        {
            BattleNarrationMessage narration = enemy is BunnySlimeCharacter bunnySlime
                ? bunnySlime.GetNextWaitNarration()
                : new BattleNarrationMessage(
                    $"{BattleNarrationFormatter.ActorName(enemy)}은 가만히 있다...",
                    BattleNarrationStyle.Normal,
                    BattleNarrationPriority.Normal,
                    0.55f,
                    requiresConfirm: false);

            _host.RequestNarration(narration);
            yield return _host.StartManagedCoroutine(_host.WaitForNarrationToFinish());
            CompleteAction();
            yield break;
        }

        EnemyAttackType attackType = action switch
        {
            EnemyAction.UseSkill when enemySkill != null => _host.ResolveEnemySkillAttackType(enemySkill),
            EnemyAction.UseStrongSkill when enemySkill != null => _host.ResolveEnemySkillAttackType(enemySkill),
            EnemyAction.EnragedAttack => EnemyAttackType.AoEAll,
            _ => EnemyAttackType.MeleeClose
        };

        _host.NotifyEnemyActionStarted(enemy, attackType);

        bool shouldTelegraphSkillThisTurn = enemy.Data != null
            && enemy.Data.TelegraphStrongSkill
            && action == EnemyAction.UseStrongSkill
            && enemySkill != null
            && !isExecutingReservedAction
            && !_host.ReservedEnemyActions.ContainsKey(enemy);

        if (shouldTelegraphSkillThisTurn)
        {
            string enemyName = enemy.Data != null && !string.IsNullOrWhiteSpace(enemy.Data.EnemyName) ? enemy.Data.EnemyName : "적";
            string warnText = $"{enemyName}가 강한 공격을 준비한다...";
            _host.RequestNarration(new BattleNarrationMessage(warnText, BattleNarrationStyle.Warning, BattleNarrationPriority.High, 0.4f, true));
            yield return _host.StartManagedCoroutine(_host.WaitForNarrationToFinish());

            _host.ReservedEnemyActions[enemy] = new BattleQueuedEnemyAction
            {
                Action = action,
                Skill = enemySkill,
                TurnsRemaining = Mathf.Max(1, enemy.Data.TelegraphTurns)
            };
            CompleteAction();
            yield break;
        }

        if ((action == EnemyAction.UseSkill || action == EnemyAction.UseStrongSkill) && enemySkill != null)
        {
            if (enemy.TryShowBattleSpeech(BattleSpeechTrigger.SkillUse, enemySkill, null, _host.BattleTurnCounter, 1.2f))
            {
                yield return _host.StartManagedCoroutine(enemy.WaitForBattleSpeech());
            }
            else
            {
                yield return new WaitForSeconds(0.18f);
            }

            yield return _host.StartManagedCoroutine(ExecuteEnemySequenceSkill(enemy, enemySkill, () => actionExecuted = true));
        }
        else if (attackType == EnemyAttackType.MeleeClose)
        {
            int targetIdx = FindFirstAlivePlayerIndex();
            if (targetIdx >= 0)
            {
                PlayerCharacter target = _host.PlayerParty[targetIdx];
                BattleCameraActionScope cameraScope = BeginActiveCameraScope(enemy.transform, target.transform);
                BattleUIController.Instance?.ShowEnemyTarget(target);
                PlayerController targetCtrl = target != null ? target.GetComponent<PlayerController>() : null;
                QteExecution qteExecution = null;
                var defenderPresentation = new BattleDefenderPresentationScope(target, _host.IsTurnQteCombatInputActive);
                try
                {
                    bool movedToCenter = enemy.Data == null || !enemy.Data.IsLargeEnemy;

                    yield return defenderPresentation.Enter();
                    if (!defenderPresentation.IsStaged || !_host.IsTurnQteCombatInputActive() || target == null || !target.IsAlive)
                        yield break;
                    // 방어 입력은 적의 접근/준비 단계부터 받을 수 있어야 합니다.
                    // 창을 이동 뒤에 열면 선입력한 Z/X가 유실됩니다.
                    targetCtrl?.PrepareDefenseWindow();
                    yield return _host.StartManagedCoroutine(_host.MoveEnemyToCenterIfNeeded(enemy));
                    if (!_host.IsTurnQteCombatInputActive() || enemy == null || !enemy.IsAlive)
                        yield break;
                    _host.SetActorForeground(enemy, true);

                    DefenseQteResult finalResult = default;
                    bool resultReceived = false;
                    bool impactApplied = false;

                    enemy.PlayAttackReady();
                    if (_host.EnemyAttackVisualDuration > 0f)
                        yield return new WaitForSecondsRealtime(_host.EnemyAttackVisualDuration);
                    if (!_host.IsTurnQteCombatInputActive() || enemy == null || !enemy.IsAlive)
                        yield break;

                    QTEManager qteManager = QTEManager.Instance;
                    if (qteManager != null)
                    {
                        DefenseQteRequest request = qteManager.CreateDefenseRequest(
                            _host.EnemyDefenseQteWindow,
                            1f,
                            DefenseRequirement.Any);
                        // 적 공격은 방어 판정만 공유하고 QTE UI는 열지 않습니다.
                        qteExecution = qteManager.StartBattleDefenseWindow(
                            request,
                            targetCtrl,
                            result =>
                            {
                                finalResult = result;
                                resultReceived = true;
                                // 결과가 확정된 바로 그 프레임에 공격 모션과 피해를
                                // 함께 시작합니다. 기존처럼 창이 끝난 뒤 별도 대기하지 않습니다.
                                if (!result.IsCounterSuccess && !impactApplied
                                    && enemy != null && enemy.IsAlive
                                    && target != null && target.IsAlive)
                                {
                                    PlayDefenseResultVisual(targetCtrl, result);
                                    PlayEnemyBasicAttackImpact(enemy);
                                    ApplyEnemyDefenseDamage(enemy, target, targetCtrl, result, true, true);
                                    ApplyPerfectParryReward(target, result);
                                    impactApplied = true;
                                    actionExecuted = true;
                                }
                             },
                             onAttackAnimationStart: () => enemy.PlayBattleAnim(EnemyCharacter.HashAttack),
                             attackAnimationLeadTime: enemy.BasicAttackImpactLeadTime,
                             attacker: enemy);
                    }

                    if (qteExecution == null)
                    {
                        enemy.PlayBattleAnim(EnemyCharacter.HashAttack);
                        // QTE 서비스가 없는 테스트/복구 상황에서도 적의 턴을
                        // 정지시키지 않고 기본 타격을 즉시 확정합니다.
                        PlayEnemyBasicAttackImpact(enemy);
                        ApplyEnemyDefenseDamage(enemy, target, targetCtrl, default, false, true);
                        impactApplied = true;
                        actionExecuted = true;
                    }
                    else
                    {
                        yield return new WaitUntil(() => qteExecution.IsDone);

                        if (qteExecution.Termination == QteTermination.Cancelled
                            || qteExecution.Termination == QteTermination.Failed
                            || !_host.IsTurnQteCombatInputActive()
                            || enemy == null || !enemy.IsAlive)
                        {
                            targetCtrl?.ResetDefenseReactionLock();
                            _host.SetActorForeground(enemy, false);
                            yield break;
                        }

                        // 콜백이 예외/씬 종료로 실행되지 않은 경우의 단일 안전망입니다.
                        if (!impactApplied)
                        {
                            PlayEnemyBasicAttackImpact(enemy);
                            ApplyEnemyDefenseDamage(enemy, target, targetCtrl, finalResult, resultReceived, true);
                            if (resultReceived)
                                ApplyPerfectParryReward(target, finalResult);
                            impactApplied = true;
                            actionExecuted = true;
                        }
                    }

                    // 방어 결과와 후속 리액션은 전투 연출 시간 기준으로 유지합니다.
                    // 슬로모션/히트스톱이 남아 있어도 피해 직후의 모션을 지연시키지 않습니다.
                    yield return new WaitForSecondsRealtime(Mathf.Max(0f, _host.EnemyPostHitDelay));
                    if (!_host.IsTurnQteCombatInputActive())
                        yield break;

                    bool tookDamage = target != null
                        && target.IsAlive
                        && (!resultReceived || !finalResult.PreventsDamage);
                    DefenseInput reactionInput = !tookDamage && resultReceived
                        ? finalResult.Input
                        : DefenseInput.None;
                    if (targetCtrl != null)
                    {
                        yield return _host.StartManagedCoroutine(
                            targetCtrl.WaitForDefenseReactionComplete(reactionInput, tookDamage));
                    }

                    targetCtrl?.ResetDefenseReactionLock();
                    if (target != null && target.IsAlive)
                    {
                        targetCtrl?.PlayBattleAnim(PlayerCharacter.HashBattleIdle);
                    }

                    yield return defenderPresentation.Return();
                    if (!_host.IsTurnQteCombatInputActive() || enemy == null) yield break;
                    enemy.PlayBattleAnim(EnemyCharacter.HashBattleIdle);

                    if (movedToCenter)
                    {
                        enemy.PlayBattleAnim(_host.ResolveEnemyReturnMoveHash(enemy));
                        BattleManager.SetGhostTrail(enemy, true);
                        yield return enemy.transform.DOMove(PositionManager.Instance.GetEnemyDefaultPos(FindEnemyIndex(enemy)), 0.3f).SetEase(Ease.InQuad).WaitForCompletion();
                        BattleManager.SetGhostTrail(enemy, false);
                    }

                    _host.SetActorForeground(enemy, false);
                    if (enemy.IsAlive)
                    {
                        enemy.PlayBattleAnim(EnemyCharacter.HashBattleIdle);
                    }
                }
                finally
                {
                    if (qteExecution != null && !qteExecution.IsDone)
                        QTEManager.Instance?.Cancel(qteExecution);
                    targetCtrl?.ResetDefenseReactionLock();
                    defenderPresentation.Dispose();
                    if (enemy != null)
                        _host.SetActorForeground(enemy, false);
                    EndActiveCameraScope(cameraScope);
                }
            }
        }
        else
        {
            var cameraTargets = new List<CharacterBase>();
            AddAlivePlayers(cameraTargets);
            BattleCameraActionScope cameraScope = BeginActiveCameraScope(enemy.transform, cameraTargets);
            PlayerController representativeController = null;
            QteExecution qteExecution = null;
            BattleDefenderPresentationScope defenderPresentation = null;
            try
            {
                int representativeIndex = FindFirstAlivePlayerIndex();
                if (representativeIndex < 0)
                {
                    CompleteAction();
                    yield break;
                }

                PlayerCharacter representative = _host.PlayerParty[representativeIndex];
                BattleUIController.Instance?.ShowEnemyTarget(representative, true);
                representativeController = representative.GetComponent<PlayerController>();
                defenderPresentation = new BattleDefenderPresentationScope(representative, _host.IsTurnQteCombatInputActive);
                yield return defenderPresentation.Enter();
                if (!defenderPresentation.IsStaged || !_host.IsTurnQteCombatInputActive() || representative == null || !representative.IsAlive)
                    yield break;
                representativeController?.PrepareDefenseWindow();
                QTEManager qteManager = QTEManager.Instance;
                if (qteManager == null)
                {
                    CompleteAction();
                    yield break;
                }

                DefenseQteResult finalResult = default;
                bool resultReceived = false;
                bool impactApplied = false;
                enemy.PlayAttackReady();
                if (_host.EnemyAttackVisualDuration > 0f)
                    yield return new WaitForSecondsRealtime(_host.EnemyAttackVisualDuration);
                if (!_host.IsTurnQteCombatInputActive() || enemy == null || !enemy.IsAlive)
                    yield break;
                DefenseQteRequest request = qteManager.CreateDefenseRequest(
                    _host.EnemyDefenseQteWindow,
                    1f,
                    DefenseRequirement.Any);
                qteExecution = qteManager.StartBattleDefenseWindow(
                    request,
                    representativeController,
                    result =>
                    {
                        finalResult = result;
                        resultReceived = true;
                        if (!result.IsCounterSuccess && !impactApplied
                            && enemy != null && enemy.IsAlive)
                        {
                            // 광역 공격도 방어 결과가 확정되는 프레임에 공격 모션과
                            // 전열 피해를 함께 시작합니다.
                            PlayDefenseResultVisual(representativeController, result);
                            PlayEnemyBasicAttackImpact(enemy);
                            for (int i = 0; i < _host.PlayerParty.Count; i++)
                            {
                                PlayerCharacter player = _host.PlayerParty[i];
                                if (player == null || !player.IsAlive)
                                    continue;

                                ApplyEnemyDefenseDamage(
                                    enemy,
                                    player,
                                    player.GetComponent<PlayerController>(),
                                    result,
                                    true,
                                    false);
                            }

                            ApplyPerfectParryReward(representative, result);
                            impactApplied = true;
                            actionExecuted = true;
                        }
                     },
                     onAttackAnimationStart: () => enemy.PlayBattleAnim(EnemyCharacter.HashAttack),
                     attackAnimationLeadTime: enemy.BasicAttackImpactLeadTime,
                     attacker: enemy);

                if (qteExecution == null)
                {
                    PlayEnemyBasicAttackImpact(enemy);
                    for (int i = 0; i < _host.PlayerParty.Count; i++)
                    {
                        PlayerCharacter player = _host.PlayerParty[i];
                        if (player == null || !player.IsAlive)
                            continue;

                        ApplyEnemyDefenseDamage(
                            enemy,
                            player,
                            player.GetComponent<PlayerController>(),
                            default,
                            false,
                            false);
                    }
                    impactApplied = true;
                    actionExecuted = true;
                }
                else
                {
                    if (!impactApplied && _host.EnemyAoeWindup > 0f)
                        yield return new WaitForSecondsRealtime(_host.EnemyAoeWindup);
                    yield return new WaitUntil(() => qteExecution.IsDone);
                    if (qteExecution.Termination == QteTermination.Cancelled
                        || qteExecution.Termination == QteTermination.Failed
                        || !_host.IsTurnQteCombatInputActive()
                        || enemy == null || !enemy.IsAlive)
                    {
                        yield break;
                    }

                    if (!impactApplied)
                    {
                        PlayEnemyBasicAttackImpact(enemy);
                        for (int i = 0; i < _host.PlayerParty.Count; i++)
                        {
                            PlayerCharacter player = _host.PlayerParty[i];
                            if (player == null || !player.IsAlive)
                                continue;

                            ApplyEnemyDefenseDamage(
                                enemy,
                                player,
                                player.GetComponent<PlayerController>(),
                                finalResult,
                                resultReceived,
                                false);
                        }

                        // A party-wide strike owns one defense window and one AP reward.
                        if (resultReceived)
                            ApplyPerfectParryReward(representative, finalResult);
                        impactApplied = true;
                        actionExecuted = true;
                    }
                }

                yield return new WaitForSecondsRealtime(Mathf.Max(0f, _host.EnemyPostHitDelay));

                bool partyTookDamage = !resultReceived || !finalResult.PreventsDamage;
                if (partyTookDamage)
                {
                    for (int i = 0; i < _host.PlayerParty.Count; i++)
                    {
                        PlayerCharacter player = _host.PlayerParty[i];
                        if (player == null || !player.IsAlive)
                            continue;

                        PlayerController controller = player.GetComponent<PlayerController>();
                        if (controller != null)
                        {
                            yield return _host.StartManagedCoroutine(
                                controller.WaitForDefenseReactionComplete(DefenseInput.None, true));
                        }
                    }
                }
                else if (resultReceived && representativeController != null)
                {
                    yield return _host.StartManagedCoroutine(
                        representativeController.WaitForDefenseReactionComplete(finalResult.Input, false));
                }
                yield return defenderPresentation.Return();
            }
            finally
            {
                if (qteExecution != null && !qteExecution.IsDone)
                    QTEManager.Instance?.Cancel(qteExecution);
                representativeController?.ResetDefenseReactionLock();
                defenderPresentation?.Dispose();
                EndActiveCameraScope(cameraScope);
            }
        }

        if (!_host.IsTurnQteCombatInputActive())
            yield break;
        yield return _host.StartManagedCoroutine(_host.WaitForNarrationToFinish());
        // 반격 피해로 발생한 HP/사망 이벤트는 연출 종료 뒤에만 처리합니다.
        yield return _host.StartManagedCoroutine(_host.FlushBattleScenarioEvents(BattleRuleTiming.AfterCurrentAction));
        if (!_host.IsTurnQteCombatInputActive())
            yield break;
        CompleteAction(actionExecuted ? enemy : null);
    }

    private void ApplyEnemyDefenseDamage(
        EnemyCharacter enemy,
        PlayerCharacter target,
        PlayerController controller,
        DefenseQteResult result,
        bool resultReceived,
        bool shakeOnHit)
    {
        if (!_host.IsTurnQteCombatInputActive() || enemy == null || !enemy.IsAlive
            || target == null || !target.IsAlive || (resultReceived && result.PreventsDamage))
            return;

        bool guarded = resultReceived && result.IsGuard;
        float multiplier = guarded ? result.DamageMultiplier : 1f;
        DamageResult damageResult = target.TakeDamage(
            Mathf.RoundToInt(enemy.ATK * multiplier),
            DamageElement.Physical,
            enemy);
        if (guarded)
        {
            if (target.IsAlive)
                controller?.ConfirmGuardSuccess();
        }
        else
        {
            if (shakeOnHit)
                CameraController.Instance?.PlayHeavySlam(Vector3.left, 1.0f, true);
        }
        _host.EmitDamage(enemy, target, damageResult.FinalDamage, false);
    }

    private void ApplyPerfectParryReward(
        PlayerCharacter target,
        DefenseQteResult result)
    {
        if (!_host.IsTurnQteCombatInputActive() || !result.PreventsDamage
            || target == null || !target.IsAlive)
            return;

        if (!result.IsPerfectParry)
            return;

        target.RestoreAP(_host.ApOnParryPerfect);
        _host.EmitApChanged(target, target.CurrentAP);
    }

    private static void PlayDefenseResultVisual(
        PlayerController controller,
        DefenseQteResult result)
    {
        if (controller == null || !result.PreventsDamage)
            return;

        // 결과 콜백은 충돌 시점에 실행됩니다. 공격 연출의 남은 대기시간과 분리합니다.
        controller.ConfirmDefenseSuccess(
            result.IsPerfectParry ? DefenseInput.Parry : result.Input);
    }

    private static void PlayEnemyBasicAttackImpact(EnemyCharacter enemy)
    {
        if (enemy == null || !enemy.IsAlive)
            return;

        enemy.PlayBasicAttackEffect();
    }

    public void SelectPlayerAction(PlayerCharacter actor, PlayerMenuAction action)
    {
        if (_host == null || actor == null || !_host.IsTurnQteCombatInputActive())
        {
            return;
        }

        if (action == PlayerMenuAction.Run && !_host.CanEscape)
        {
            return;
        }

        _host.PendingActor = actor;
        _host.PendingAction = action;
        _host.PendingSkill = null;
        _host.PendingItem = null;

        // 일반 공격은 타겟 앞에 도착한 뒤 준비 자세를 재생합니다. 타겟 선택 중에
        // 미리 준비 모션을 재생하면 공격 타임라인에서 두 번 보이거나 너무 일찍 끝납니다.
        if (action != PlayerMenuAction.Run && action != PlayerMenuAction.Attack)
        {
            actor.PlayAttackReady();
        }

        if (action == PlayerMenuAction.Attack)
        {
            _host.NotifyTargetSelectionStarted(action);
        }
        else if (action == PlayerMenuAction.Run)
        {
            _host.StartManagedCoroutine(_host.RunAwayRoutine());
        }
    }

    public void SelectSubMenuAction(PlayerCharacter actor, PlayerMenuAction action, SkillData skill, ItemData item)
    {
        if (_host == null || actor == null || !_host.IsTurnQteCombatInputActive())
        {
            return;
        }

        _host.PendingActor = actor;
        _host.PendingAction = action;
        _host.PendingSkill = skill;
        _host.PendingItem = item;

        bool isAoE = (skill != null && skill.IsAoE) || (item != null && item.IsAoE);
        if (isAoE)
        {
            ConfirmTargetAndExecute(-1);
        }
        else
        {
            _host.NotifyTargetSelectionStarted(action);
        }
    }

    public void CancelActionSelection()
    {
        if (_host == null || !_host.IsTurnQteCombatInputActive())
        {
            return;
        }

        _host.PendingActor?.PlayBattleAnim(PlayerCharacter.HashBattleIdle);
        _host.ChangeBattleState(BattleState.PlayerActionSelect);
    }

    public void CancelTargetSelection()
    {
        if (_host == null || !_host.IsTurnQteCombatInputActive())
        {
            return;
        }

        _host.PendingActor?.PlayBattleAnim(PlayerCharacter.HashBattleIdle);
        _host.ChangeBattleState(BattleState.PlayerActionSelect);
    }

    public void ConfirmTargetAndExecute(int targetIndex)
    {
        if (_host == null || !_host.IsTurnQteCombatInputActive())
        {
            return;
        }

        if (_host.CurrentBattleState == BattleState.ActionExecute)
        {
            return;
        }

        if (_host.PendingAction == PlayerMenuAction.Attack)
        {
            _host.ChangeBattleState(BattleState.ActionExecute);
            _host.StartManagedCoroutine(ExecuteAttack(_host.PendingActor, targetIndex));
        }
        else if (_host.PendingAction == PlayerMenuAction.Skill && _host.PendingSkill != null)
        {
            if (_host.PendingActor.CurrentAP < _host.PendingSkill.APCost)
            {
                _host.RequestNarration(new BattleNarrationMessage("AP가 부족하다.", BattleNarrationStyle.Warning, BattleNarrationPriority.High, 0.2f, true));
                _host.PendingActor?.PlayBattleAnim(PlayerCharacter.HashBattleIdle);
                _host.PendingSkill = null;
                _host.PendingItem = null;
                _host.ChangeBattleState(BattleState.PlayerActionSelect);
                return;
            }

            _host.ChangeBattleState(BattleState.ActionExecute);
            _host.StartManagedCoroutine(ExecuteSkill(_host.PendingActor, targetIndex, _host.PendingSkill));
        }
        else if (_host.PendingAction == PlayerMenuAction.Item && _host.PendingItem != null)
        {
            _host.ChangeBattleState(BattleState.ActionExecute);
            _host.StartManagedCoroutine(ExecuteItem(_host.PendingActor, targetIndex, _host.PendingItem));
        }
        else
        {
            CompleteAction();
        }
    }

    public void CompleteAction()
    {
        CompleteAction(null);
    }

    private void CompleteAction(CharacterBase executedActor)
    {
        if (_host == null)
        {
            return;
        }

        if (_host.IsTurnQteCombatInputActive())
        {
            CharacterBase turnActor = executedActor;
            int turnIndex = _host.CurrentActorIndex - 1;
            if (turnActor == null && turnIndex >= 0 && turnIndex < _host.TurnQueue.Count)
                turnActor = _host.TurnQueue[turnIndex];

            if (executedActor != null && executedActor.IsAlive)
            {
                int previousHp = executedActor.CurrentHP;
                executedActor.NotifyActionExecuted();
                int damage = previousHp - executedActor.CurrentHP;
                if (damage > 0)
                {
                    _host.EmitDamage(executedActor, damage, false, previousHp);
                    _host.PublishEnemyDefeatedScenarioEvent(executedActor, null);
                }
            }

            // Bleed remains subscribed through the final action, then expires.
            // A skipped turn spends duration without firing an action notification.
            // Unity의 파괴된 MonoBehaviour는 C# 참조 자체가 null이 아닐 수 있으므로
            // null 조건부 연산자(?..) 대신 Unity null 판정을 먼저 사용합니다.
            if (turnActor != null)
                turnActor.ProcessEffects(endOfTurn: true);
        }

        _host.ClearTurnQtePendingActionState();
        // 방어창이 없는 상태/보조 스킬에서도 Z/X/C 선입력이 다음 적 공격으로
        // 넘어가지 않도록 턴 경계에서 모든 전열 버퍼를 비웁니다.
        ClearDefenseInputBuffers();
        _host.ResetAllPlayerBattlePoses();
        CameraController.Instance?.ResetCamera(0.4f);
        CancelActiveCameraPresentation();
        _host.BroadcastVisibleTurnQueue();

        if (!_host.IsTurnQteCombatInputActive())
        {
            return;
        }

        if (_host.CheckVictory())
        {
            _host.ChangeBattleState(BattleState.BattleEnd);
        }
        else if (_host.TryStartNextPartyWave())
        {
            return;
        }
        else if (_host.CheckDefeat())
        {
            _host.ChangeBattleState(BattleState.BattleEnd);
        }
        else
        {
            AdvanceTurn();
        }
    }

    private IEnumerator ExecuteAttack(PlayerCharacter actor, int targetIndex)
    {
        EnemyCharacter target = GetEnemy(targetIndex);
        if (_host == null || actor == null || target == null || !target.IsAlive)
        {
            CompleteAction();
            yield break;
        }

        BattleCameraActionScope cameraScope = BeginActiveCameraScope(actor.transform, target.transform);
        try
        {
            PositionManager pm = PositionManager.Instance;
            // 공격 위치는 고정 오프셋이 아니라 대상의 Front 피벗을 기준으로
            // 계산합니다. 캐릭터 크기/방향이 달라도 타겟 앞에 정확히 멈추며,
            // 도착 뒤에는 별도 돌진 없이 제자리 공격만 재생합니다.
            Vector3 frontPos = pm != null
                ? pm.GetAttackStagingPos(actor, target)
                : target.GetPivot(CharacterPivotId.Front).position;

            actor.PlayBattleAnim(PlayerCharacter.HashBattleMove);
            _host.SetActorForeground(actor, true);
            BattleManager.SetGhostTrail(actor, true);
            yield return actor.transform.DOMove(frontPos, 0.2f).SetEase(Ease.OutCubic).WaitForCompletion();

            // 타겟 앞에서 준비 자세를 한 번 보여준 뒤 제자리에서 공격합니다.
            // 공격 중 추가 돌진/뒤쪽 이동은 제거해 피격 프레임과 모션을 일치시킵니다.
            actor.PlayAttackReady();
            yield return new WaitForSecondsRealtime(PlayerAttackReadyDuration);

            actor.PlayBasicAttackEffect();
            actor.PlayBattleAnim(PlayerCharacter.HashAttack);

            // 피해 프레임은 Animator/DOTween의 실시간 타이밍과 맞춰야 합니다.
            // 슬로모션이나 히트스톱이 남아 있어도 공격 모션만 먼저 진행되어
            // 피격/피해가 뒤늦게 발생하지 않도록 unscaled 대기를 사용합니다.
            yield return new WaitForSecondsRealtime(Mathf.Max(0f, _host.PlayerAttackHitDelay));

            int previousHp = target.CurrentHP;
            DamageResult damageResult = target.TakeDamage(
                actor.ATK,
                DamageElement.Physical,
                actor);
            int dmg = damageResult.FinalDamage;
            CameraController.Instance?.PlayHeavySlam(Vector3.right, 0.75f, true);
            _host.PublishEnemyHpScenarioEvent(target, previousHp, target.CurrentHP, target.MaxHP, BattleRuleTiming.AfterCurrentAction);
            _host.EmitDamageNotificationOnly(actor, target, dmg, false);
            _host.PublishEnemyDefeatedScenarioEvent(target, actor);

            yield return new WaitForSecondsRealtime(Mathf.Max(0f, _host.PlayerAttackRecoverDelay));
            yield return _host.StartManagedCoroutine(actor.WaitForAttackAnimationComplete());
            BattleManager.SetGhostTrail(actor, false);
            _host.SetActorForeground(actor, false);

            int idx = FindPlayerIndex(actor);
            actor.PlayBattleAnim(PlayerCharacter.HashBattleMove);
            BattleManager.SetGhostTrail(actor, true);
            Vector3 returnPos = pm != null ? pm.GetPlayerDefaultPos(idx) : actor.transform.position;
            yield return actor.transform.DOMove(returnPos, 0.3f).SetEase(Ease.OutQuad).WaitForCompletion();
            BattleManager.SetGhostTrail(actor, false);

            actor.PlayBattleAnim(PlayerCharacter.HashBattleIdle);
            CameraController.Instance?.ResetCamera(0.4f);

            yield return _host.StartManagedCoroutine(_host.WaitForNarrationToFinish());
            yield return _host.StartManagedCoroutine(_host.FlushBattleScenarioEvents(BattleRuleTiming.AfterCurrentAction));
            CompleteAction(actor);
        }
        finally
        {
            EndActiveCameraScope(cameraScope);
        }
    }

    private IEnumerator ExecuteSkill(PlayerCharacter actor, int targetIndex, SkillData skill)
    {
        if (_host == null || actor == null || skill == null)
        {
            CompleteAction();
            yield break;
        }

        if (!_host.IsTurnQteCombatInputActive() || !actor.IsAlive)
            yield break;

        actor.ConsumeAP(skill.APCost);
        _host.EmitApChanged(actor, actor.CurrentAP);
        if (actor.TryShowBattleSpeech(BattleSpeechTrigger.SkillUse, skill, null, _host.BattleTurnCounter))
        {
            yield return _host.StartManagedCoroutine(actor.WaitForBattleSpeech());
        }

        var targets = new List<CharacterBase>();
        if (skill.IsAoE)
        {
            if (skill.TargetType == TargetAreaType.AllyOnly)
            {
                AddAlivePlayers(targets);
            }
            else
            {
                AddAliveEnemies(targets);
            }
        }
        else
        {
            if (skill.TargetType == TargetAreaType.AllyOnly)
            {
                PlayerCharacter ally = GetPlayer(targetIndex);
                if (ally != null) targets.Add(ally);
            }
            else
            {
                EnemyCharacter enemy = GetEnemy(targetIndex);
                if (enemy != null) targets.Add(enemy);
            }
        }

        if (targets.Count == 0)
        {
            CompleteAction();
            yield break;
        }

        var scenarioTargets = new List<CharacterBase>(targets);
        Vector3 originalPos = PositionManager.Instance.GetPlayerDefaultPos(FindPlayerIndex(actor));
        var context = new SkillContext
        {
            Actor = actor,
            Targets = targets,
            CurrentDamageMultiplier = 1.0f,
            IsPerfectQTE = false,
            IsExecutionActive = _host.IsTurnQteCombatInputActive
        };

        BattleCameraActionScope cameraScope = BeginActiveCameraScope(actor.transform, targets);
        try
        {
            if (skill.ActionTimeline != null)
            {
                foreach (SkillActionBlock block in skill.ActionTimeline)
                {
                    if (!context.CanContinueExecution)
                        yield break;
                    if (block == null || block.Disabled)
                    {
                        continue;
                    }

                    context.Targets.RemoveAll(t => t == null || !t.IsAlive);
                    if (context.Targets.Count == 0)
                    {
                        break;
                    }

                    yield return _host.StartManagedCoroutine(context.ExecuteBlock(block, skill.ActionTimeline));
                    if (!context.CanContinueExecution)
                        yield break;
                }
            }

            // QTE가 피해/상태 블록 없이 끝나는 스킬도 마지막 입력 결과를 회수한 뒤
            // 정상적으로 종료합니다. 이동·애니메이션 블록은 이 지점까지 기다리지 않습니다.
            yield return context.WaitForActiveSkillQte();

            if (!context.CanContinueExecution)
                yield break;

            // 방어창의 정리 시간은 충돌 소비자 뒤에 적용합니다. 피해 블록이 없는
            // 상태 전용 스킬도 타임라인 끝에서 남은 시간을 잃지 않습니다.
            yield return context.WaitForPendingDefensePostImpactDelay();
            if (!context.CanContinueExecution)
                yield break;

            if (Vector3.Distance(actor.transform.position, originalPos) > 0.1f)
            {
                actor.PlayBattleAnim(PlayerCharacter.HashBattleMove);
                _host.SetActorForeground(actor, true);
                BattleManager.SetGhostTrail(actor, true);
                yield return actor.transform.DOMove(originalPos, 0.3f).SetEase(Ease.OutBack).WaitForCompletion();
                BattleManager.SetGhostTrail(actor, false);
                _host.SetActorForeground(actor, false);
            }

            for (int i = 0; i < scenarioTargets.Count; i++)
                _host.PublishEnemyDefeatedScenarioEvent(scenarioTargets[i], actor);

            _host.PublishSkillCompletedScenarioEvent(skill, actor);
            actor.PlayBattleAnim(PlayerCharacter.HashBattleIdle);
            CameraController.Instance?.ResetCamera(0.4f);
            yield return _host.StartManagedCoroutine(_host.WaitForNarrationToFinish());
            yield return _host.StartManagedCoroutine(_host.FlushBattleScenarioEvents(BattleRuleTiming.AfterCurrentSkill));
            CompleteAction(actor);
        }
        finally
        {
            if (context != null && context.ActiveSkillQte != null
                && !context.ActiveSkillQte.IsDone)
            {
                QTEManager.Instance?.Cancel(context.ActiveSkillQte);
            }
            EndActiveCameraScope(cameraScope);
        }
    }

    private IEnumerator ExecuteItem(PlayerCharacter actor, int targetIndex, ItemData item)
    {
        if (_host == null || actor == null || item == null)
        {
            CompleteAction();
            yield break;
        }

        var targets = new List<CharacterBase>();
        if (item.IsAoE)
        {
            if (item.TargetType == TargetAreaType.AllyOnly)
            {
                AddAlivePlayers(targets);
            }
            else
            {
                AddAliveEnemies(targets);
            }
        }
        else if (item.TargetType == TargetAreaType.AllyOnly)
        {
            PlayerCharacter player = GetPlayer(targetIndex);
            if (player != null) targets.Add(player);
        }
        else
        {
            EnemyCharacter enemy = GetEnemy(targetIndex);
            if (enemy != null && enemy.IsAlive) targets.Add(enemy);
        }

        if (targets.Count == 0)
        {
            CompleteAction();
            yield break;
        }

        GlobalDataManager global = GlobalDataManager.Instance;
        if (global == null || global.GetItemCount(item.ItemID) <= 0)
        {
            Debug.LogWarning($"[BattleItem] Item is not owned: {item.ItemID}");
            CompleteAction();
            yield break;
        }

        for (int i = 0; i < targets.Count; i++)
        {
            if (!ItemEffectService.CanApply(item, targets[i], true, out string validationError))
            {
                Debug.LogWarning($"[BattleItem] Item cannot be applied: {validationError}");
                CompleteAction();
                yield break;
            }
        }

        if (!global.RemoveItem(item.ItemID, 1))
        {
            CompleteAction();
            yield break;
        }

        PlayerController actorCtrl = actor.GetComponent<PlayerController>();
        PositionManager pm = PositionManager.Instance;

        actorCtrl?.PlayBattleAnim(PlayerCharacter.HashBattleMove);
        _host.SetActorForeground(actor, true);
        yield return actor.transform.DOMove(actor.transform.position + Vector3.right * 1f, 0.2f).SetEase(Ease.OutQuad).WaitForCompletion();
        actorCtrl?.PlayBattleAnim(PlayerCharacter.HashBattleIdle);

        yield return new WaitForSeconds(0.3f);

        foreach (CharacterBase target in targets)
            BattleManager.ExecuteItemEffect(target, item);

        yield return new WaitForSeconds(0.5f);

        int idx = FindPlayerIndex(actor);
        actorCtrl?.PlayBattleAnim(PlayerCharacter.HashBattleMove);
        yield return actor.transform.DOMove(pm.GetPlayerDefaultPos(idx), 0.3f).SetEase(Ease.OutBack).WaitForCompletion();
        _host.SetActorForeground(actor, false);
        actorCtrl?.PlayBattleAnim(PlayerCharacter.HashBattleIdle);

        yield return _host.StartManagedCoroutine(_host.WaitForNarrationToFinish());
        CompleteAction(actor);
    }

    private IEnumerator ExecuteEnemySequenceSkill(EnemyCharacter enemy, SkillData skill, System.Action onCompleted = null)
    {
        if (_host == null || enemy == null || skill == null || skill.ActionTimeline == null || skill.ActionTimeline.Count == 0)
        {
            yield break;
        }

        if (!_host.IsTurnQteCombatInputActive() || !enemy.IsAlive)
            yield break;

        int enemyIndex = FindEnemyIndex(enemy);
        Vector3 defaultPos = enemyIndex >= 0 && PositionManager.Instance != null
            ? PositionManager.Instance.GetEnemyDefaultPos(enemyIndex)
            : enemy.transform.position;

        var targets = new List<CharacterBase>();
        if (skill.IsAoE || skill.TargetType == TargetAreaType.AoEAll)
        {
            AddAlivePlayers(targets);
        }
        else
        {
            int targetIdx = FindFirstAlivePlayerIndex();
            if (targetIdx >= 0)
            {
                targets.Add(_host.PlayerParty[targetIdx]);
            }
        }

        if (targets.Count == 0)
        {
            yield break;
        }

        var context = new SkillContext
        {
            Actor = enemy,
            Targets = targets,
            CurrentDamageMultiplier = 1.0f,
            IsPerfectQTE = false,
            IsExecutionActive = _host.IsTurnQteCombatInputActive,
            LinkCounterService = _linkCounterService
        };

        Tween returnMovement = null;
        BattleCameraActionScope cameraScope = BeginActiveCameraScope(enemy.transform, targets);
        try
        {
            foreach (SkillActionBlock block in skill.ActionTimeline)
            {
                if (!context.CanContinueExecution)
                    yield break;
                if (block == null || block.Disabled)
                {
                    continue;
                }

                context.Targets.RemoveAll(t => t == null || !t.IsAlive);
                if (context.Targets.Count == 0)
                {
                    break;
                }

                yield return _host.StartManagedCoroutine(context.ExecuteBlock(block, skill.ActionTimeline));
                if (context.AttackInterruptedByCounter)
                    break;
                if (!context.CanContinueExecution)
                    yield break;
            }

            if (context.StopTimelineExecution || !_host.IsTurnQteCombatInputActive()
                || (!context.AttackInterruptedByCounter && !context.CanContinueExecution))
                yield break;

            // 타임라인형 적 공격은 방어 결과 모션이 끝난 뒤에야 전투 포즈를
            // 초기화해야 합니다. 피해/상태 소비자가 놓친 경우에도 안전망으로
            // 여기서 한 번 소비합니다.
            yield return context.WaitForPendingDefenseReaction();
            yield return context.WaitForPendingDefensePostImpactDelay();
            yield return context.ReturnDefender();
            if (!_host.IsTurnQteCombatInputActive())
                yield break;

            if (enemy != null && enemy.IsAlive
                && Vector3.Distance(enemy.transform.position, defaultPos) > 0.05f)
            {
                enemy.PlayBattleAnim(_host.ResolveEnemyReturnMoveHash(enemy));
                BattleManager.SetGhostTrail(enemy, true);
                returnMovement = enemy.transform.DOMove(defaultPos, 0.25f)
                    .SetEase(Ease.OutQuad).SetRecyclable(false);
                while (_host.IsTurnQteCombatInputActive() && enemy != null && enemy.IsAlive
                    && returnMovement.IsActive() && !returnMovement.IsComplete())
                    yield return null;
                BattleManager.SetGhostTrail(enemy, false);
            }

            if (!_host.IsTurnQteCombatInputActive())
                yield break;
            if (enemy != null && enemy.IsAlive)
                enemy.PlayBattleAnim(EnemyCharacter.HashBattleIdle);
            onCompleted?.Invoke();
        }
        finally
        {
            returnMovement?.Kill();
            // 씬 전환/전투 취소로 타임라인이 중단돼도 입력창과 방어 리액션이
            // 다음 전투로 새지 않도록 동기 복구합니다. 정상 완료 시에는 앞선
            // 소비 경로가 필드를 비워 두므로 아무 작업도 하지 않습니다.
            context.CancelPendingDefenseReaction();
            CloseDefenseInputs(context.Targets);
            if (enemy != null)
                BattleManager.SetGhostTrail(enemy, false);
            EndActiveCameraScope(cameraScope);
        }
    }

    private void AddAlivePlayers(List<CharacterBase> targets)
    {
        for (int i = 0; i < _host.PlayerParty.Count; i++)
        {
            PlayerCharacter player = _host.PlayerParty[i];
            if (player != null && player.IsAlive)
            {
                targets.Add(player);
            }
        }
    }

    private static void CloseDefenseInputs(IReadOnlyList<CharacterBase> targets)
    {
        if (targets == null)
            return;

        for (int i = 0; i < targets.Count; i++)
        {
            CharacterBase target = targets[i];
            // Destroy된 Unity 오브젝트에는 C#의 null 조건부 연산자(?..)가
            // 안전하지 않으므로, Unity의 overloaded null 비교 후 접근합니다.
            if (target == null)
                continue;

            PlayerController controller = target.GetComponent<PlayerController>();
            if (controller != null)
                controller.CloseDefenseInputWindow();
        }
    }

    private void ClearDefenseInputBuffers()
    {
        if (_host == null || _host.PlayerParty == null)
            return;

        for (int i = 0; i < _host.PlayerParty.Count; i++)
        {
            PlayerCharacter player = _host.PlayerParty[i];
            // 전투 종료 중 PlayerParty가 파괴된 캐릭터를 잠시 보유할 수 있습니다.
            // Unity null 판정을 통과한 경우에만 GetComponent를 호출해야
            // MissingReferenceException이 발생하지 않습니다.
            if (player == null)
                continue;

            PlayerController controller = player.GetComponent<PlayerController>();
            if (controller != null)
            {
                controller.ActiveDefensePresentation?.Dispose();
                controller.CloseDefenseInputWindow();
            }
        }
    }

    private void AddAliveEnemies(List<CharacterBase> targets)
    {
        for (int i = 0; i < _host.Enemies.Count; i++)
        {
            EnemyCharacter enemy = _host.Enemies[i];
            if (enemy != null && enemy.IsAlive)
            {
                targets.Add(enemy);
            }
        }
    }

    public void CancelActiveCameraPresentation()
    {
        BattleUIController.Instance?.ShowEnemyTarget(null);
        BattleCameraActionScope scope = _activeCameraScope;
        _activeCameraScope = null;
        scope?.Dispose();
    }

    private BattleCameraActionScope BeginActiveCameraScope(
        Transform actor,
        IReadOnlyList<CharacterBase> targets)
    {
        CancelActiveCameraPresentation();

        int targetCount = targets != null ? targets.Count : 0;
        var transforms = new List<Transform>(targetCount + 1);
        if (actor != null)
        {
            transforms.Add(actor);
        }

        for (int i = 0; i < targetCount; i++)
        {
            CharacterBase target = targets[i];
            if (target != null)
            {
                transforms.Add(target.transform);
            }
        }

        _activeCameraScope = BattleCameraActionScope.Begin(transforms);
        return _activeCameraScope;
    }

    private BattleCameraActionScope BeginActiveCameraScope(Transform first, Transform second)
    {
        CancelActiveCameraPresentation();
        _activeCameraScope = BattleCameraActionScope.Begin(first, second);
        return _activeCameraScope;
    }

    private void EndActiveCameraScope(BattleCameraActionScope scope)
    {
        if (scope == null)
        {
            return;
        }

        if (ReferenceEquals(_activeCameraScope, scope))
        {
            _activeCameraScope = null;
            BattleUIController.Instance?.ShowEnemyTarget(null);
        }

        scope.Dispose();
    }

    private int FindFirstAlivePlayerIndex()
    {
        for (int i = 0; i < _host.PlayerParty.Count; i++)
        {
            PlayerCharacter player = _host.PlayerParty[i];
            if (player != null && player.IsAlive)
            {
                return i;
            }
        }

        return -1;
    }

    private int FindPlayerIndex(PlayerCharacter actor)
    {
        for (int i = 0; i < _host.PlayerParty.Count; i++)
        {
            if (_host.PlayerParty[i] == actor)
            {
                return i;
            }
        }

        return -1;
    }

    private int FindEnemyIndex(EnemyCharacter enemy)
    {
        for (int i = 0; i < _host.Enemies.Count; i++)
        {
            if (_host.Enemies[i] == enemy)
            {
                return i;
            }
        }

        return -1;
    }

    private PlayerCharacter GetPlayer(int index)
    {
        return index >= 0 && index < _host.PlayerParty.Count ? _host.PlayerParty[index] : null;
    }

    private EnemyCharacter GetEnemy(int index)
    {
        return index >= 0 && index < _host.Enemies.Count ? _host.Enemies[index] : null;
    }
}
