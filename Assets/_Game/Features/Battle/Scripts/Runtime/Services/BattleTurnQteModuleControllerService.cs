using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public sealed class BattleTurnQteModuleControllerService : IBattleTurnQteModuleController
{
    private readonly IBattleTurnQteHost _host;

    public BattleTurnQteModuleControllerService(IBattleTurnQteHost host)
    {
        _host = host;
    }

    public IEnumerator EnterTurnQteModule(GameModuleRuntimeContext context)
    {
        BattleUIController.Instance?.ResumeBattleModuleInput();
        yield break;
    }

    public IEnumerator ExitTurnQteModule(GameModuleRuntimeContext context)
    {
        QTEManager.Instance?.ForceStop();
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
            Debug.LogError("[BattleTurnQteModuleControllerService] 전투 시작 시 적 리스트가 비어 있습니다. BattlePrefab 또는 EnemyCharacter 설정을 확인해주세요.");
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

        for (int i = 0; i < _host.MaxTurnQueueSize; i++)
        {
            aliveChars.Sort((a, b) => b.SPD.CompareTo(a.SPD));
            _host.TurnQueue.Add(aliveChars[i % aliveChars.Count]);
        }

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

        actor.ProcessEffects();
        if (!actor.IsAlive)
        {
            _host.BroadcastVisibleTurnQueue();
            AdvanceTurn();
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
        player.HealMP(_host.MpPerTurn);
        _host.EmitMpChanged(player, player.CurrentMP);
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

            yield return _host.StartManagedCoroutine(ExecuteEnemySequenceSkill(enemy, enemySkill));
        }
        else if (attackType == EnemyAttackType.MeleeClose)
        {
            int targetIdx = FindFirstAlivePlayerIndex();
            if (targetIdx >= 0)
            {
                PlayerCharacter target = _host.PlayerParty[targetIdx];
                PlayerController targetCtrl = target != null ? target.GetComponent<PlayerController>() : null;
                bool movedToCenter = enemy.Data == null || !enemy.Data.IsLargeEnemy;

                yield return _host.StartManagedCoroutine(_host.MoveEnemyToCenterIfNeeded(enemy));
                _host.SetActorForeground(enemy, true);

                enemy.PlayBasicAttackEffect();
                enemy.PlayBattleAnim(EnemyCharacter.HashAttack);

                bool qteFinished = false;
                DefenseInput finalInput = DefenseInput.None;
                QTEManager.QTEGrade finalGrade = QTEManager.QTEGrade.Miss;

                targetCtrl?.PrepareDefenseWindow();
                QTEManager.Instance.StartDefenseQTE(_host.EnemyDefenseQteWindow, 1.0f, (input, grade) =>
                {
                    finalInput = input;
                    finalGrade = grade;
                    qteFinished = true;
                });
                yield return new WaitForSeconds(_host.EnemyAttackVisualDuration);
                enemy.PlayBattleAnim(EnemyCharacter.HashBattleIdle);
                yield return new WaitUntil(() => qteFinished);

                if (finalGrade == QTEManager.QTEGrade.Miss)
                {
                    int dmg = target.TakePureDamage(enemy.ATK);
                    targetCtrl?.PlayHurtEffect();
                    CameraController.Instance?.PlayHeavySlam(Vector3.left, 1.0f, true);
                    _host.EmitDamage(target, dmg, false);
                }
                else
                {
                    targetCtrl?.ConfirmDefenseSuccess(finalInput);
                    if (finalInput == DefenseInput.Parry && finalGrade == QTEManager.QTEGrade.Perfect)
                    {
                        target.HealMP(_host.MpOnParryPerfect);
                        _host.EmitMpChanged(target, target.CurrentMP);
                    }

                    if (finalInput == DefenseInput.Dodge || finalInput == DefenseInput.Jump)
                    {
                        yield return targetCtrl != null ? _host.StartManagedCoroutine(targetCtrl.WaitForDefenseVisualComplete(0.5f)) : null;
                    }
                }

                yield return new WaitForSeconds(_host.EnemyPostHitDelay);
                targetCtrl?.ResetDefenseReactionLock();
                if (target != null && target.IsAlive)
                {
                    targetCtrl?.PlayBattleAnim(PlayerCharacter.HashBattleIdle);
                }

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
        }
        else
        {
            yield return new WaitForSeconds(_host.EnemyAoeWindup);
            for (int i = 0; i < _host.PlayerParty.Count; i++)
            {
                PlayerCharacter player = _host.PlayerParty[i];
                if (player == null || !player.IsAlive)
                {
                    continue;
                }

                int dmg = player.TakePureDamage(enemy.ATK);
                player.GetComponent<PlayerController>()?.PlayHurtEffect();
                _host.EmitDamage(player, dmg, false);
            }

            yield return new WaitForSeconds(_host.EnemyPostHitDelay);
        }

        yield return _host.StartManagedCoroutine(_host.WaitForNarrationToFinish());
        CompleteAction();
    }

    public void SelectPlayerAction(PlayerCharacter actor, PlayerMenuAction action)
    {
        if (_host == null || actor == null || !_host.IsTurnQteCombatInputActive())
        {
            return;
        }

        _host.PendingActor = actor;
        _host.PendingAction = action;
        _host.PendingSkill = null;
        _host.PendingItem = null;

        if (action != PlayerMenuAction.Run)
        {
            actor.PlayBattleAnim(PlayerCharacter.HashBattleReady);
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
            if (_host.PendingActor.CurrentMP < _host.PendingSkill.MPCost)
            {
                _host.RequestNarration(new BattleNarrationMessage("MP가 부족하다.", BattleNarrationStyle.Warning, BattleNarrationPriority.High, 0.2f, true));
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
        if (_host == null)
        {
            return;
        }

        _host.ClearTurnQtePendingActionState();
        _host.ResetAllPlayerBattlePoses();
        CameraController.Instance?.ResetCamera(0.4f);
        _host.BroadcastVisibleTurnQueue();

        if (!_host.IsTurnQteCombatInputActive())
        {
            return;
        }

        if (_host.CheckVictory() || _host.CheckDefeat())
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

        PositionManager pm = PositionManager.Instance;
        Vector3 frontPos = target.transform.position + _host.MeleeAttackOffset;

        actor.PlayBattleAnim(PlayerCharacter.HashBattleMove);
        _host.SetActorForeground(actor, true);
        BattleManager.SetGhostTrail(actor, true);
        yield return actor.transform.DOMove(frontPos, 0.2f).SetEase(Ease.OutCubic).WaitForCompletion();

        Vector3 pullBackPos = frontPos + _host.MeleePullbackOffset;
        yield return actor.transform.DOMove(pullBackPos, 0.15f).SetEase(Ease.OutBack).WaitForCompletion();

        Vector3 behindPos = target.transform.position + new Vector3(-_host.MeleeAttackOffset.x, 0, 0);

        actor.PlayBasicAttackEffect();
        actor.PlayBattleAnim(PlayerCharacter.HashAttack);
        actor.transform.DOMove(behindPos, 0.15f).SetEase(Ease.InExpo);

        yield return new WaitForSeconds(_host.PlayerAttackHitDelay);

        int previousHp = target.CurrentHP;
        int dmg = target.TakeDamage(actor.ATK);
        CameraController.Instance?.PlayHeavySlam(Vector3.right, 0.75f, true);
        _host.PublishEnemyHpScenarioEvent(target, previousHp, target.CurrentHP, target.MaxHP, BattleRuleTiming.AfterCurrentAction);
        _host.EmitDamageNotificationOnly(target, dmg, false);

        yield return new WaitForSeconds(_host.PlayerAttackRecoverDelay);
        BattleManager.SetGhostTrail(actor, false);
        _host.SetActorForeground(actor, false);

        int idx = FindPlayerIndex(actor);
        actor.PlayBattleAnim(PlayerCharacter.HashBattleMove);
        BattleManager.SetGhostTrail(actor, true);
        yield return actor.transform.DOJump(pm.GetPlayerDefaultPos(idx), 0.5f, 1, 0.3f).SetEase(Ease.OutQuad).WaitForCompletion();
        BattleManager.SetGhostTrail(actor, false);

        actor.PlayBattleAnim(PlayerCharacter.HashBattleIdle);
        CameraController.Instance?.ResetCamera(0.4f);

        yield return _host.StartManagedCoroutine(_host.WaitForNarrationToFinish());
        yield return _host.StartManagedCoroutine(_host.FlushBattleScenarioEvents(BattleRuleTiming.AfterCurrentAction));
        CompleteAction();
    }

    private IEnumerator ExecuteSkill(PlayerCharacter actor, int targetIndex, SkillData skill)
    {
        if (_host == null || actor == null || skill == null)
        {
            CompleteAction();
            yield break;
        }

        actor.ConsumeMP(skill.MPCost);
        _host.EmitMpChanged(actor, actor.CurrentMP);
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

        Vector3 originalPos = PositionManager.Instance.GetPlayerDefaultPos(FindPlayerIndex(actor));
        var context = new SkillContext
        {
            Actor = actor,
            Targets = targets,
            CurrentDamageMultiplier = 1.0f,
            IsPerfectQTE = false
        };

        if (skill.ActionTimeline != null)
        {
            foreach (SkillActionBlock block in skill.ActionTimeline)
            {
                if (block == null || block.Disabled)
                {
                    continue;
                }

                context.Targets.RemoveAll(t => t == null || !t.IsAlive);
                if (context.Targets.Count == 0)
                {
                    break;
                }

                yield return _host.StartManagedCoroutine(block.Execute(context));
            }
        }

        if (Vector3.Distance(actor.transform.position, originalPos) > 0.1f)
        {
            actor.PlayBattleAnim(PlayerCharacter.HashBattleMove);
            _host.SetActorForeground(actor, true);
            BattleManager.SetGhostTrail(actor, true);
            yield return actor.transform.DOMove(originalPos, 0.3f).SetEase(Ease.OutBack).WaitForCompletion();
            BattleManager.SetGhostTrail(actor, false);
            _host.SetActorForeground(actor, false);
        }

        actor.PlayBattleAnim(PlayerCharacter.HashBattleIdle);
        CameraController.Instance?.ResetCamera(0.4f);
        yield return _host.StartManagedCoroutine(_host.WaitForNarrationToFinish());
        yield return _host.StartManagedCoroutine(_host.FlushBattleScenarioEvents(BattleRuleTiming.AfterCurrentSkill));
        CompleteAction();
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

        PlayerController actorCtrl = actor.GetComponent<PlayerController>();
        PositionManager pm = PositionManager.Instance;

        actorCtrl?.PlayBattleAnim(PlayerCharacter.HashBattleMove);
        _host.SetActorForeground(actor, true);
        yield return actor.transform.DOMove(actor.transform.position + Vector3.right * 1f, 0.2f).SetEase(Ease.OutQuad).WaitForCompletion();
        actorCtrl?.PlayBattleAnim(PlayerCharacter.HashBattleIdle);

        yield return new WaitForSeconds(0.3f);

        foreach (CharacterBase target in targets)
        {
            BattleManager.ExecuteItemEffect(target, item);
        }

        yield return new WaitForSeconds(0.5f);

        int idx = FindPlayerIndex(actor);
        actorCtrl?.PlayBattleAnim(PlayerCharacter.HashBattleMove);
        yield return actor.transform.DOMove(pm.GetPlayerDefaultPos(idx), 0.3f).SetEase(Ease.OutBack).WaitForCompletion();
        _host.SetActorForeground(actor, false);
        actorCtrl?.PlayBattleAnim(PlayerCharacter.HashBattleIdle);

        yield return _host.StartManagedCoroutine(_host.WaitForNarrationToFinish());
        CompleteAction();
    }

    private IEnumerator RunRoutine()
    {
        if (_host == null)
        {
            yield break;
        }

        _host.RequestNarration(new BattleNarrationMessage("도망을 시도했다...", BattleNarrationStyle.Normal, BattleNarrationPriority.High, 0.2f, true));
        yield return _host.StartManagedCoroutine(_host.WaitForNarrationToFinish());

        bool success = Random.value < 0.6f;
        _host.RequestNarration(new BattleNarrationMessage(success ? "도망에 성공했다!" : "도망에 실패했다...", BattleNarrationStyle.Warning, BattleNarrationPriority.High, 0.2f, true));
        yield return _host.StartManagedCoroutine(_host.WaitForNarrationToFinish());

        if (success)
        {
            Debug.LogWarning("[BattleTurnQteModuleControllerService] Run action succeeded, but battle escape resolution still lives in BattleManager. Ensure orchestration path remains wired.");
            CompleteAction();
        }
        else
        {
            CompleteAction();
        }
    }

    private IEnumerator ExecuteEnemySequenceSkill(EnemyCharacter enemy, SkillData skill)
    {
        if (_host == null || enemy == null || skill == null || skill.ActionTimeline == null || skill.ActionTimeline.Count == 0)
        {
            yield break;
        }

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
            IsPerfectQTE = false
        };

        foreach (SkillActionBlock block in skill.ActionTimeline)
        {
            if (block == null || block.Disabled)
            {
                continue;
            }

            context.Targets.RemoveAll(t => t == null || !t.IsAlive);
            if (context.Targets.Count == 0 || context.StopTimelineExecution)
            {
                break;
            }

            yield return _host.StartManagedCoroutine(block.Execute(context));
            if (context.StopTimelineExecution)
            {
                break;
            }
        }

        if (Vector3.Distance(enemy.transform.position, defaultPos) > 0.05f)
        {
            enemy.PlayBattleAnim(_host.ResolveEnemyReturnMoveHash(enemy));
            BattleManager.SetGhostTrail(enemy, true);
            yield return enemy.transform.DOMove(defaultPos, 0.25f).SetEase(Ease.OutQuad).WaitForCompletion();
            BattleManager.SetGhostTrail(enemy, false);
        }

        enemy.PlayBattleAnim(EnemyCharacter.HashBattleIdle);
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