using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using DG.Tweening;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEngine;

public class BattleTurnQteModuleControllerServiceTests
{
    [TestCase(false)]
    [TestCase(true)]
    public void EnemyCounterInterruption_SkipsRemainingBlocksAndCompletesTurnEvenOnLethalCounter(bool lethal)
    {
        using (var fixture = new TurnQteFixture())
        {
            SkillData skill = ScriptableObject.CreateInstance<SkillData>();
            try
            {
                RecordingSkillActionBlock.Reset();
                int playerHp = fixture.Player.CurrentHP;
                int playerAp = fixture.Player.CurrentAP;
                int playerActions = 0;
                fixture.Player.OnActionExecuted += () => playerActions++;
                skill.ActionTimeline.Add(new InterruptingSkillActionBlock
                {
                    OnExecute = context =>
                    {
                        Assert.That(context.LinkCounterService, Is.Not.Null);
                        context.AttackInterruptedByCounter = true;
                        context.CurrentDamageMultiplier = 0f;
                        if (lethal)
                            fixture.Enemy.TakePureDamage(fixture.Enemy.CurrentHP);
                    }
                });
                skill.ActionTimeline.Add(new RecordingSkillActionBlock());
                fixture.Host.QueueEnemyAction(EnemyAction.UseStrongSkill);
                fixture.Host.ReservedEnemyActions[fixture.Enemy] = new BattleQueuedEnemyAction
                {
                    Action = EnemyAction.UseStrongSkill,
                    Skill = skill,
                    TurnsRemaining = 1
                };
                fixture.Host.Victory = lethal;
                fixture.Host.ChangeBattleState(BattleState.EnemyAction);

                RunToCompletion(new BattleTurnQteModuleControllerService(fixture.Host).RunEnemyAction());

                Assert.That(RecordingSkillActionBlock.Calls, Is.Zero);
                Assert.That(fixture.Player.CurrentHP, Is.EqualTo(playerHp));
                Assert.That(fixture.Player.CurrentAP, Is.EqualTo(playerAp));
                Assert.That(playerActions, Is.Zero);
                Assert.That(fixture.Host.FlushCalls, Is.EqualTo(1));
                Assert.That(fixture.Host.CurrentBattleState,
                    Is.EqualTo(lethal ? BattleState.BattleEnd : BattleState.TurnCalc));
                Assert.That(fixture.CameraController.IsFramingTargets, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(skill);
            }
        }
    }

    [TestCase(DefenseInput.Dodge)]
    [TestCase(DefenseInput.Counter)]
    public void SuccessfulDodgeOrCounter_PreventsDamageWithoutPerfectGuardAp(DefenseInput input)
    {
        using (var fixture = new TurnQteFixture())
        {
            fixture.Player.ConsumeAP(fixture.Player.CurrentAP);
            int hp = fixture.Player.CurrentHP;
            var service = new BattleTurnQteModuleControllerService(fixture.Host);
            var result = new DefenseQteResult(DefenseInputReadStatus.Valid, input,
                QTEManager.QTEGrade.Perfect, DefenseOutcome.Success,
                DefenseRequirement.Counterable, 0.05f, true, true);
            InvokePrivateApplyEnemyDefenseDamage(service, fixture.Enemy, fixture.Player, result, true);
            typeof(BattleTurnQteModuleControllerService)
                .GetMethod("ApplyPerfectParryReward", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(service, new object[] { fixture.Player, null, result });
            Assert.That(fixture.Player.CurrentHP, Is.EqualTo(hp));
            Assert.That(fixture.Player.CurrentAP, Is.Zero);
            Assert.That(fixture.Host.ApNotifications, Is.Zero);
        }
    }

    [TestCase(1)]
    [TestCase(2)]
    public void AdvanceTurn_StunSkipsExactlyItsRemainingTurns(int duration)
    {
        using (var fixture = new TurnQteFixture())
        {
            fixture.Player.TryApplyStatusEffect(new StunEffect(duration));
            fixture.Host.TurnQueue.Add(fixture.Player);
            var service = new BattleTurnQteModuleControllerService(fixture.Host);
            for (int turn = 0; turn < duration; turn++)
            {
                fixture.Host.CurrentActorIndex = 0;
                service.AdvanceTurn();
                Assert.That(fixture.Host.PlayerTurnNotifications, Is.Zero);
            }

            Assert.That(fixture.Player.IsStunned, Is.False);
            fixture.Host.CurrentActorIndex = 0;
            service.AdvanceTurn();
            Assert.That(fixture.Host.PlayerTurnNotifications, Is.EqualTo(1));
        }
    }

    [Test]
    public void AdvanceTurn_SkippedStunnedTurnExpiresBleedWithoutActionDamage()
    {
        using (var fixture = new TurnQteFixture())
        {
            fixture.Player.TryApplyStatusEffect(new StunEffect(1));
            fixture.Player.TryApplyStatusEffect(new BleedEffect(1));
            int previousHp = fixture.Player.CurrentHP;
            fixture.Host.TurnQueue.Add(fixture.Player);
            new BattleTurnQteModuleControllerService(fixture.Host).AdvanceTurn();

            Assert.That(fixture.Player.CurrentHP, Is.EqualTo(previousHp));
            Assert.That(fixture.Player.HasEffect(StatusEffectIds.Bleed), Is.False);
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ExecuteSkill_ActionNotificationAndFinalTurnBleedOnlyRunOnCompletion(bool cancelled)
    {
        using (var fixture = new TurnQteFixture())
        {
            SkillData skill = ScriptableObject.CreateInstance<SkillData>();
            try
            {
                skill.SkillID = "bleed_completion";
                skill.APCost = 0;
                skill.TargetType = TargetAreaType.EnemyOnly;
                skill.ActionTimeline.Add(new InterruptingSkillActionBlock
                {
                    OnExecute = context => context.StopTimelineExecution = cancelled
                });
                fixture.Player.TryApplyStatusEffect(new BleedEffect(1));
                fixture.Player.ProcessEffects();
                int previousHp = fixture.Player.CurrentHP;
                int notifications = 0;
                fixture.Player.OnActionExecuted += () => notifications++;

                var service = new BattleTurnQteModuleControllerService(fixture.Host);
                RunToCompletion(InvokePrivateExecuteSkill(service, fixture.Player, 0, skill));

                Assert.That(notifications, Is.EqualTo(cancelled ? 0 : 1));
                Assert.That(fixture.Player.CurrentHP, Is.EqualTo(cancelled ? previousHp : previousHp - Mathf.Max(1, fixture.Player.MaxHP / 100)));
                Assert.That(fixture.Player.HasEffect(StatusEffectIds.Bleed), Is.EqualTo(cancelled));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(skill);
            }
        }
    }

    [Test]
    public void ExecuteSkill_LethalBleedIsAppliedBeforeReserveWaveDecision()
    {
        using (var fixture = new TurnQteFixture())
        {
            SkillData skill = ScriptableObject.CreateInstance<SkillData>();
            try
            {
                skill.SkillID = "lethal_bleed_completion";
                skill.APCost = 0;
                skill.TargetType = TargetAreaType.EnemyOnly;
                skill.ActionTimeline.Add(new RecordingSkillActionBlock());
                fixture.Player.TakePureDamage(fixture.Player.CurrentHP - 1);
                fixture.Player.TryApplyStatusEffect(new BleedEffect(1));
                fixture.Host.EvaluatePartySurvival = true;
                fixture.Host.CanStartNextPartyWave = true;

                RunToCompletion(InvokePrivateExecuteSkill(new BattleTurnQteModuleControllerService(fixture.Host), fixture.Player, 0, skill));

                Assert.That(fixture.Player.IsAlive, Is.False);
                Assert.That(fixture.Host.PartyWaveStartCalls, Is.EqualTo(1));
                Assert.That(fixture.Host.CurrentBattleState, Is.Not.EqualTo(BattleState.BattleEnd));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(skill);
            }
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void MoveBlock_CancellationStopsOnlyOwnedTweenAndClearsTrail(bool disposeRoutine)
    {
        using (var fixture = new TurnQteFixture())
        {
            CharacterGhostTrail trail = fixture.Enemy.gameObject.AddComponent<CharacterGhostTrail>();
            bool active = true;
            var context = new SkillContext
            {
                Actor = fixture.Enemy,
                Targets = new List<CharacterBase> { fixture.Player },
                IsExecutionActive = () => active
            };
            IEnumerator routine = new Action_Move { Destination = Action_Move.MoveDest.Center, Duration = 10f }.Execute(context);
            Tween unrelated = null;
            try
            {
                Assert.That(routine.MoveNext(), Is.True);
                Assert.That(trail.enabled, Is.True);
                unrelated = fixture.Enemy.transform.DOMove(Vector3.up, 20f).SetRecyclable(false);
                if (disposeRoutine)
                    (routine as IDisposable)?.Dispose();
                else
                {
                    active = false;
                    Assert.That(routine.MoveNext(), Is.False);
                    Assert.That(context.StopTimelineExecution, Is.True);
                }

                Assert.That(trail.enabled, Is.False);
                Assert.That(unrelated.IsActive(), Is.True);
                Assert.That(DOTween.TweensByTarget(fixture.Enemy.transform).Count, Is.EqualTo(1));
            }
            finally
            {
                (routine as IDisposable)?.Dispose();
                unrelated?.Kill();
            }
        }
    }

    [Test]
    public void SelectPlayerAction_WhenEscapeIsDisabled_RejectsRunWithoutMutatingPendingState()
    {
        var fixture = new TurnQteFixture();
        try
        {
            fixture.Host.CanEscape = false;
            fixture.Host.PendingAction = PlayerMenuAction.Attack;
            var service = new BattleTurnQteModuleControllerService(fixture.Host);

            service.SelectPlayerAction(fixture.Player, PlayerMenuAction.Run);

            Assert.That(fixture.Host.PendingAction, Is.EqualTo(PlayerMenuAction.Attack));
            Assert.That(fixture.Host.RunAwayCalls, Is.Zero);
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Test]
    public void SelectPlayerAction_WhenEscapeIsEnabled_StartsRunNormally()
    {
        var fixture = new TurnQteFixture();
        try
        {
            fixture.Host.CanEscape = true;
            var service = new BattleTurnQteModuleControllerService(fixture.Host);

            service.SelectPlayerAction(fixture.Player, PlayerMenuAction.Run);

            Assert.That(fixture.Host.PendingAction, Is.EqualTo(PlayerMenuAction.Run));
            Assert.That(fixture.Host.RunAwayCalls, Is.EqualTo(1));
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Test]
    public void CompleteAction_WhenPartyIsDefeatedAndReserveExists_StartsNextWave()
    {
        var fixture = new TurnQteFixture();
        try
        {
            fixture.Host.Defeat = true;
            fixture.Host.CanStartNextPartyWave = true;
            var service = new BattleTurnQteModuleControllerService(fixture.Host);

            service.CompleteAction();

            Assert.That(fixture.Host.PartyWaveStartCalls, Is.EqualTo(1));
            Assert.That(fixture.Host.CurrentBattleState, Is.Not.EqualTo(BattleState.BattleEnd));
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Test]
    public void CompleteAction_WhenVictoryAndReserveExists_EndsBattleWithoutStartingWave()
    {
        var fixture = new TurnQteFixture();
        try
        {
            fixture.Host.Victory = true;
            fixture.Host.Defeat = true;
            fixture.Host.CanStartNextPartyWave = true;
            var service = new BattleTurnQteModuleControllerService(fixture.Host);

            service.CompleteAction();

            Assert.That(fixture.Host.PartyWaveStartCalls, Is.Zero);
            Assert.That(fixture.Host.CurrentBattleState, Is.EqualTo(BattleState.BattleEnd));
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Test]
    public void CompleteAction_WhenPartyIsDefeatedWithoutReserve_EndsBattle()
    {
        var fixture = new TurnQteFixture();
        try
        {
            fixture.Host.Defeat = true;
            var service = new BattleTurnQteModuleControllerService(fixture.Host);

            service.CompleteAction();

            Assert.That(fixture.Host.PartyWaveStartCalls, Is.EqualTo(1));
            Assert.That(fixture.Host.CurrentBattleState, Is.EqualTo(BattleState.BattleEnd));
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Test]
    public void ExecuteSkill_SkipsDisabledSkillBlocksInTurnQtePath()
    {
        var fixture = new TurnQteFixture();
        try
        {
            RecordingSkillActionBlock.Reset();
            SkillData skill = ScriptableObject.CreateInstance<SkillData>();
            skill.SkillID = "player_slash";
            skill.APCost = 0;
            skill.TargetType = TargetAreaType.EnemyOnly;
            skill.ActionTimeline.Add(new RecordingSkillActionBlock { Disabled = true });
            skill.ActionTimeline.Add(new RecordingSkillActionBlock());
            fixture.Player.Skills.Add(skill);

            var service = new BattleTurnQteModuleControllerService(fixture.Host);
            IEnumerator routine = InvokePrivateExecuteSkill(service, fixture.Player, 0, skill);
            RunToCompletion(routine);

            Assert.That(RecordingSkillActionBlock.Calls, Is.EqualTo(1));
            Assert.That(fixture.Host.FlushCalls, Is.EqualTo(1));

            UnityEngine.Object.DestroyImmediate(skill);
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Test]
    public void ExecuteSkill_FramesActorAndTargetDuringTimelineAndRestoresAfter()
    {
        var fixture = new TurnQteFixture();
        try
        {
            RecordingSkillActionBlock.Reset();
            SkillData skill = ScriptableObject.CreateInstance<SkillData>();
            skill.SkillID = "camera_slash";
            skill.APCost = 0;
            skill.TargetType = TargetAreaType.EnemyOnly;
            skill.ActionTimeline.Add(new RecordingSkillActionBlock());

            var service = new BattleTurnQteModuleControllerService(fixture.Host);
            IEnumerator routine = InvokePrivateExecuteSkill(service, fixture.Player, 0, skill);
            RunToCompletion(routine);

            Assert.That(RecordingSkillActionBlock.SawActiveFraming, Is.True);
            Assert.That(fixture.CameraController.IsFramingTargets, Is.False);
            Assert.That(
                fixture.CameraController.VirtualCamera.Follow,
                Is.EqualTo(fixture.PositionManager.transform));

            UnityEngine.Object.DestroyImmediate(skill);
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Test]
    public void ExecuteSkill_DisposeMidActionRestoresCamera()
    {
        var fixture = new TurnQteFixture();
        SkillData skill = null;
        IEnumerator routine = null;
        try
        {
            skill = ScriptableObject.CreateInstance<SkillData>();
            skill.SkillID = "interrupt_camera_slash";
            skill.APCost = 0;
            skill.TargetType = TargetAreaType.EnemyOnly;
            skill.ActionTimeline.Add(new RecordingSkillActionBlock());

            var service = new BattleTurnQteModuleControllerService(fixture.Host);
            routine = InvokePrivateExecuteSkill(service, fixture.Player, 0, skill);

            Assert.That(routine.MoveNext(), Is.True);
            Assert.That(fixture.CameraController.IsFramingTargets, Is.True);

            (routine as IDisposable)?.Dispose();
            routine = null;

            Assert.That(fixture.CameraController.IsFramingTargets, Is.False);
            Assert.That(
                fixture.CameraController.VirtualCamera.Follow,
                Is.EqualTo(fixture.PositionManager.transform));
        }
        finally
        {
            (routine as IDisposable)?.Dispose();
            if (skill != null) UnityEngine.Object.DestroyImmediate(skill);
            fixture.Dispose();
        }
    }

    [Test]
    public void ExecuteAttack_AnimationFailureStillRestoresCamera()
    {
        var fixture = new TurnQteFixture();
        IEnumerator routine = null;
        try
        {
            var service = new BattleTurnQteModuleControllerService(fixture.Host);
            routine = InvokePrivateExecuteAttack(service, fixture.Player, 0);

            Assert.Throws<NullReferenceException>(() => routine.MoveNext());

            Assert.That(fixture.CameraController.IsFramingTargets, Is.False);
            Assert.That(
                fixture.CameraController.VirtualCamera.Follow,
                Is.EqualTo(fixture.PositionManager.transform));
        }
        finally
        {
            (routine as IDisposable)?.Dispose();
            fixture.Dispose();
        }
    }

    [Test]
    public void ExecuteEnemySequenceSkill_FramesEnemyAndTargetDuringTimeline()
    {
        var fixture = new TurnQteFixture();
        try
        {
            RecordingSkillActionBlock.Reset();
            SkillData skill = ScriptableObject.CreateInstance<SkillData>();
            skill.SkillID = "enemy_camera_slash";
            skill.APCost = 0;
            skill.TargetType = TargetAreaType.EnemyOnly;
            skill.ActionTimeline.Add(new RecordingSkillActionBlock());

            var service = new BattleTurnQteModuleControllerService(fixture.Host);
            IEnumerator routine = InvokePrivateExecuteEnemySequenceSkill(service, fixture.Enemy, skill);
            RunToCompletion(routine);

            Assert.That(RecordingSkillActionBlock.SawActiveFraming, Is.True);
            Assert.That(fixture.CameraController.IsFramingTargets, Is.False);

            UnityEngine.Object.DestroyImmediate(skill);
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Test]
    public void RunEnemyAction_MeleeFramesEnemyAndDefenderBeforeQteSetup()
    {
        var fixture = new TurnQteFixture();
        try
        {
            fixture.Host.QueueEnemyAction(EnemyAction.BasicAttack);
            var service = new BattleTurnQteModuleControllerService(fixture.Host);

            RunToCompletion(service.RunEnemyAction());

            Assert.That(fixture.Host.SawActiveCameraDuringEnemyMove, Is.True);
            Assert.That(fixture.CameraController.IsFramingTargets, Is.False);
            Assert.That(
                fixture.CameraController.VirtualCamera.Follow,
                Is.EqualTo(fixture.PositionManager.transform));
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Test]
    public void RunEnemyAction_AoeWithoutQteDoesNotDamageAndRestoresCamera()
    {
        var fixture = new TurnQteFixture();
        try
        {
            fixture.Host.QueueEnemyAction(EnemyAction.EnragedAttack);
            var service = new BattleTurnQteModuleControllerService(fixture.Host);

            RunToCompletion(service.RunEnemyAction());

            Assert.That(fixture.Host.DamageNotifications, Is.Zero);
            Assert.That(fixture.CameraController.IsFramingTargets, Is.False);
            Assert.That(
                fixture.CameraController.VirtualCamera.Follow,
                Is.EqualTo(fixture.PositionManager.transform));
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Test]
    public void RunEnemyAction_WaitOnlyShowsNarrationAndCompletesTurn()
    {
        var fixture = new TurnQteFixture();
        try
        {
            fixture.Host.QueueEnemyAction(EnemyAction.Wait);
            var service = new BattleTurnQteModuleControllerService(fixture.Host);

            RunToCompletion(service.RunEnemyAction());

            Assert.That(fixture.Host.NarrationRequests, Is.EqualTo(1));
            Assert.That(fixture.Host.LastNarration.Text, Is.EqualTo("ZEV은 가만히 있다..."));
            Assert.That(fixture.Host.EnemyActionNotifications, Is.Zero);
            Assert.That(fixture.Host.SawActiveCameraDuringEnemyMove, Is.False);
            Assert.That(fixture.Host.SawActiveCameraDuringDamage, Is.False);
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [TestCase(100, 1f, 1f, 1f, 50)]
    [TestCase(0, 0.5f, 1f, 1f, 50)]
    [TestCase(0, 1f, 0.5f, 1f, 50)]
    [TestCase(0, 1f, 1f, 2f, 200)]
    [TestCase(100, 0.5f, 0.5f, 2f, 25)]
    [TestCase(0, 1f, 1f, 1f, 100)]
    public void ApplyEnemyDefenseDamage_UsesPhysicalMitigationAndPublishesResolvedDamage(
        int defense,
        float physicalResistance,
        float incomingMultiplier,
        float outgoingMultiplier,
        int expectedDamage)
    {
        var fixture = new TurnQteFixture(
            new StatBlock
            {
                MaxHP = 1000,
                DEF = defense,
                PhysicalResistance = physicalResistance,
                IncomingDamageMultiplier = incomingMultiplier
            },
            new StatBlock
            {
                ATK = 100,
                OutgoingDamageMultiplier = outgoingMultiplier
            });
        try
        {
            var service = new BattleTurnQteModuleControllerService(fixture.Host);
            int previousHp = fixture.Player.CurrentHP;

            InvokePrivateApplyEnemyDefenseDamage(service, fixture.Enemy, fixture.Player);

            Assert.That(fixture.Player.CurrentHP, Is.EqualTo(previousHp - expectedDamage));
            Assert.That(fixture.Host.DamageNotifications, Is.EqualTo(1));
            Assert.That(fixture.Host.LastDamage, Is.EqualTo(expectedDamage));
            Assert.That(fixture.Host.LastDamageSource, Is.SameAs(fixture.Enemy));
            Assert.That(fixture.Host.LastDamageTarget, Is.SameAs(fixture.Player));
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [TestCase(DefenseOutcome.Failure, 50, 0, 1)]
    [TestCase(DefenseOutcome.Guarded, 25, 0, 1)]
    [TestCase(DefenseOutcome.Success, 0, 20, 0)]
    public void EnemyDefenseResult_OnlyPerfectPreventsAllDamageAndRewardsAp(
        DefenseOutcome outcome,
        int expectedDamage,
        int expectedAp,
        int expectedDamageNotifications)
    {
        var fixture = new TurnQteFixture(
            new StatBlock { MaxHP = 1000, MaxAP = 100, DEF = 100 },
            new StatBlock { ATK = 100 });
        try
        {
            var service = new BattleTurnQteModuleControllerService(fixture.Host);
            fixture.Player.ConsumeAP(fixture.Player.CurrentAP);
            int previousHp = fixture.Player.CurrentHP;
            bool perfect = outcome == DefenseOutcome.Success;
            bool guarded = outcome == DefenseOutcome.Guarded;
            var result = new DefenseQteResult(
                DefenseInputReadStatus.Valid,
                DefenseInput.Parry,
                perfect ? QTEManager.QTEGrade.Perfect
                    : guarded ? QTEManager.QTEGrade.Good : QTEManager.QTEGrade.Miss,
                outcome,
                DefenseRequirement.Any,
                perfect ? 0.01f : 0.2f,
                true,
                perfect,
                guarded ? 0.5f : 1f);

            InvokePrivateApplyEnemyDefenseDamage(service, fixture.Enemy, fixture.Player, result, true);
            MethodInfo rewardMethod = typeof(BattleTurnQteModuleControllerService).GetMethod(
                "ApplyPerfectParryReward", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(rewardMethod, Is.Not.Null);
            rewardMethod.Invoke(service, new object[] { fixture.Player, null, result });

            Assert.That(fixture.Player.CurrentHP, Is.EqualTo(previousHp - expectedDamage));
            Assert.That(fixture.Player.CurrentAP, Is.EqualTo(expectedAp));
            Assert.That(fixture.Host.DamageNotifications, Is.EqualTo(expectedDamageNotifications));
            Assert.That(fixture.Host.ApNotifications, Is.EqualTo(perfect ? 1 : 0));
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void SkillTimeline_WhenModuleExitsDuringBlock_SkipsFollowingBlocks(bool playerSkill)
    {
        var fixture = new TurnQteFixture();
        SkillData skill = null;
        try
        {
            RecordingSkillActionBlock.Reset();
            skill = ScriptableObject.CreateInstance<SkillData>();
            skill.SkillID = "module_exit_guard_regression";
            skill.APCost = 0;
            skill.TargetType = TargetAreaType.EnemyOnly;
            skill.ActionTimeline.Add(new InterruptingSkillActionBlock
            {
                OnExecute = _ => fixture.Host.ModuleActive = false
            });
            skill.ActionTimeline.Add(new RecordingSkillActionBlock());

            var service = new BattleTurnQteModuleControllerService(fixture.Host);
            IEnumerator routine = playerSkill
                ? InvokePrivateExecuteSkill(service, fixture.Player, 0, skill)
                : InvokePrivateExecuteEnemySequenceSkill(service, fixture.Enemy, skill);
            RunToCompletion(routine);

            Assert.That(RecordingSkillActionBlock.Calls, Is.Zero);
            Assert.That(fixture.CameraController.IsFramingTargets, Is.False);
        }
        finally
        {
            if (skill != null) UnityEngine.Object.DestroyImmediate(skill);
            fixture.Dispose();
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void SkillTimeline_WhenBlockRequestsStop_SkipsFollowingBlocks(bool playerSkill)
    {
        var fixture = new TurnQteFixture();
        SkillData skill = null;
        try
        {
            RecordingSkillActionBlock.Reset();
            skill = ScriptableObject.CreateInstance<SkillData>();
            skill.SkillID = "cancelled_defense_guard_regression";
            skill.APCost = 0;
            skill.TargetType = TargetAreaType.EnemyOnly;
            skill.ActionTimeline.Add(new InterruptingSkillActionBlock
            {
                OnExecute = context => context.StopTimelineExecution = true
            });
            skill.ActionTimeline.Add(new RecordingSkillActionBlock());

            var service = new BattleTurnQteModuleControllerService(fixture.Host);
            IEnumerator routine = playerSkill
                ? InvokePrivateExecuteSkill(service, fixture.Player, 0, skill)
                : InvokePrivateExecuteEnemySequenceSkill(service, fixture.Enemy, skill);
            RunToCompletion(routine);

            Assert.That(RecordingSkillActionBlock.Calls, Is.Zero);
            Assert.That(fixture.CameraController.IsFramingTargets, Is.False);
        }
        finally
        {
            if (skill != null) UnityEngine.Object.DestroyImmediate(skill);
            fixture.Dispose();
        }
    }

    [Test]
    public void DamageBlock_WhenExecutionOwnerIsInactive_DoesNotApplyDamage()
    {
        var fixture = new TurnQteFixture();
        try
        {
            int previousHp = fixture.Player.CurrentHP;
            var context = new SkillContext
            {
                Actor = fixture.Enemy,
                Targets = new List<CharacterBase> { fixture.Player },
                IsExecutionActive = () => false
            };

            RunToCompletion(new Action_Damage().Execute(context));

            Assert.That(fixture.Player.CurrentHP, Is.EqualTo(previousHp));
            Assert.That(context.StopTimelineExecution, Is.True);
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Test]
    public void ExitTurnQteModuleCancelsActiveCameraScope()
    {
        var fixture = new TurnQteFixture();
        try
        {
            var service = new BattleTurnQteModuleControllerService(fixture.Host);
            MethodInfo beginMethod = typeof(BattleTurnQteModuleControllerService).GetMethod(
                "BeginActiveCameraScope",
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(Transform), typeof(Transform) },
                null);
            Assert.That(beginMethod, Is.Not.Null);
            beginMethod.Invoke(
                service,
                new object[] { fixture.Player.transform, fixture.Enemy.transform });
            Assert.That(fixture.CameraController.IsFramingTargets, Is.True);

            RunToCompletion(service.ExitTurnQteModule(null));

            Assert.That(fixture.CameraController.IsFramingTargets, Is.False);
        }
        finally
        {
            fixture.Dispose();
        }
    }

    private static void InvokePrivateApplyEnemyDefenseDamage(
        BattleTurnQteModuleControllerService service,
        EnemyCharacter enemy,
        PlayerCharacter target,
        DefenseQteResult result = default(DefenseQteResult),
        bool resultReceived = false)
    {
        MethodInfo method = typeof(BattleTurnQteModuleControllerService).GetMethod(
            "ApplyEnemyDefenseDamage",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        method.Invoke(service, new object[] { enemy, target, null, result, resultReceived, false });
    }

    private static IEnumerator InvokePrivateExecuteAttack(
        BattleTurnQteModuleControllerService service,
        PlayerCharacter actor,
        int targetIndex)
    {
        MethodInfo method = typeof(BattleTurnQteModuleControllerService).GetMethod(
            "ExecuteAttack",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        return (IEnumerator)method.Invoke(service, new object[] { actor, targetIndex });
    }

    private static IEnumerator InvokePrivateExecuteEnemySequenceSkill(
        BattleTurnQteModuleControllerService service,
        EnemyCharacter actor,
        SkillData skill)
    {
        MethodInfo method = typeof(BattleTurnQteModuleControllerService).GetMethod(
            "ExecuteEnemySequenceSkill",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        return (IEnumerator)method.Invoke(service, new object[] { actor, skill, null });
    }

    private static IEnumerator InvokePrivateExecuteSkill(
        BattleTurnQteModuleControllerService service,
        PlayerCharacter actor,
        int targetIndex,
        SkillData skill)
    {
        MethodInfo method = typeof(BattleTurnQteModuleControllerService).GetMethod(
            "ExecuteSkill",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        return (IEnumerator)method.Invoke(service, new object[] { actor, targetIndex, skill });
    }

    private static void RunToCompletion(IEnumerator routine, int maxSteps = 128)
    {
        int steps = 0;
        while (routine.MoveNext())
        {
            IEnumerator nested = routine.Current as IEnumerator;
            if (nested != null)
            {
                RunToCompletion(nested, maxSteps);
            }

            steps++;
            if (steps > maxSteps)
            {
                Assert.Fail("Routine did not complete within " + maxSteps + " steps.");
            }
        }
    }

    private sealed class TurnQteFixture : IDisposable
    {
        private readonly List<UnityEngine.Object> _assets = new List<UnityEngine.Object>();
        private readonly GameObject _positionManagerObject;
        private readonly PositionManager _positionManager;
        private readonly GameObject _cameraObject;
        private readonly CameraController _cameraController;
        private readonly GameObject _playerObject;
        private readonly GameObject _enemyObject;

        public TurnQteFixture(StatBlock playerStats = null, StatBlock enemyStats = null)
        {
            _positionManagerObject = new GameObject("PositionManager");
            _positionManager = _positionManagerObject.AddComponent<PositionManager>();
            SetStaticProperty(typeof(PositionManager), "Instance", _positionManager);
            SetPrivateField(
                _positionManager,
                "_playerDefaultPos",
                new List<Transform> { _positionManagerObject.transform });

            SetStaticProperty(typeof(CameraController), "Instance", null);
            _cameraObject = new GameObject("TurnQteCamera");
            CinemachineCamera virtualCamera = _cameraObject.AddComponent<CinemachineCamera>();
            virtualCamera.Lens.ModeOverride = LensSettings.OverrideModes.Orthographic;
            virtualCamera.Lens.OrthographicSize = CameraLensDefaults.GameplayOrthographicSize;
            _cameraObject.AddComponent<CinemachineFollow>();
            _cameraObject.AddComponent<CinemachineImpulseSource>();
            _cameraController = _cameraObject.AddComponent<CameraController>();
            typeof(CameraController)
                .GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(_cameraController, null);
            // 이 fixture는 구형 동적 카메라 소유권/취소 계약을 계속 검사합니다.
            // 고정 구도 기본값은 CameraPresentationTests에서 별도로 검사합니다.
            typeof(CameraController).GetField("_staticBattlePresentation", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(_cameraController, false);
            _cameraController.SetDefaultTarget(_positionManagerObject.transform, true);
            _cameraController.ResetCamera(0f);

            _playerObject = new GameObject("Player");
            Player = _playerObject.AddComponent<PlayerCharacter>();
            CharacterData playerData = ScriptableObject.CreateInstance<CharacterData>();
            playerData.CharacterID = "player";
            playerData.DisplayName = "Player";
            if (playerStats != null)
                playerData.BaseStats = playerStats;
            Player.SetCharacterData(playerData);
            Player.HealHP(Player.MaxHP);
            Player.RestoreAP(Player.MaxAP);
            _assets.Add(playerData);

            _enemyObject = new GameObject("Enemy");
            Enemy = _enemyObject.AddComponent<EnemyCharacter>();
            EnemyData enemyData = ScriptableObject.CreateInstance<EnemyData>();
            enemyData.EnemyId = "zev";
            enemyData.EnemyName = "ZEV";
            if (enemyStats != null)
                enemyData.BaseStats = enemyStats;
            Enemy.Setup(enemyData);
            _assets.Add(enemyData);
            SetPrivateField(
                _positionManager,
                "_enemyDefaultPos",
                new List<Transform> { _enemyObject.transform });
            SetPrivateField(_positionManager, "_centerPos", _positionManagerObject.transform);
            DOTween.Init();
            _positionManagerObject.transform.position = new Vector3(-8f, 0f, 0f);
            _playerObject.transform.position = new Vector3(-8f, 0f, 0f);
            _enemyObject.transform.position = new Vector3(8f, 0f, 0f);

            Host = new FakeTurnQteHost(Player, Enemy);
        }

        public PlayerCharacter Player { get; }
        public EnemyCharacter Enemy { get; }
        public FakeTurnQteHost Host { get; }
        public CameraController CameraController => _cameraController;
        public PositionManager PositionManager => _positionManager;

        public void Dispose()
        {
            for (int i = 0; i < _assets.Count; i++)
            {
                UnityEngine.Object.DestroyImmediate(_assets[i]);
            }

            UnityEngine.Object.DestroyImmediate(_enemyObject);
            UnityEngine.Object.DestroyImmediate(_playerObject);
            UnityEngine.Object.DestroyImmediate(_cameraObject);
            UnityEngine.Object.DestroyImmediate(_positionManagerObject);
            SetStaticProperty(typeof(CameraController), "Instance", null);
            SetStaticProperty(typeof(PositionManager), "Instance", null);
        }

        private static void SetStaticProperty(Type type, string propertyName, object value)
        {
            PropertyInfo property = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Static);
            property.GetSetMethod(true).Invoke(null, new[] { value });
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(target, value);
        }
    }

    private sealed class FakeTurnQteHost : IBattleTurnQteHost
    {
        private readonly List<PlayerCharacter> _players = new List<PlayerCharacter>();
        private readonly List<EnemyCharacter> _enemies = new List<EnemyCharacter>();
        private readonly List<CharacterBase> _turnQueue = new List<CharacterBase>();
        private readonly Dictionary<EnemyCharacter, BattleQueuedEnemyAction> _reserved = new Dictionary<EnemyCharacter, BattleQueuedEnemyAction>();
        private readonly WaitForSeconds _waitShort = new WaitForSeconds(0f);

        public FakeTurnQteHost(PlayerCharacter player, EnemyCharacter enemy)
        {
            _players.Add(player);
            _enemies.Add(enemy);
            PendingActor = player;
        }

        public int FlushCalls { get; private set; }
        public bool Victory { get; set; }
        public bool Defeat { get; set; }
        public bool EvaluatePartySurvival { get; set; }
        public bool CanStartNextPartyWave { get; set; }
        public bool CanEscape { get; set; } = true;
        public bool ModuleActive { get; set; } = true;
        public int PartyWaveStartCalls { get; private set; }
        public int RunAwayCalls { get; private set; }
        public IReadOnlyList<PlayerCharacter> PlayerParty => _players;
        public IReadOnlyList<EnemyCharacter> Enemies => _enemies;
        public IList<CharacterBase> TurnQueue => _turnQueue;
        public IDictionary<EnemyCharacter, BattleQueuedEnemyAction> ReservedEnemyActions => _reserved;
        public WaitForSeconds WaitShort => _waitShort;
        public int MaxTurnQueueSize => 8;
        public int ApPerTurn => 5;
        public int ApOnParryPerfect => 20;
        public float EnemyDefenseQteWindow => 0.8f;
        public float EnemyAttackVisualDuration => 0f;
        public float EnemyPostHitDelay => 0f;
        public float EnemyAoeWindup => 0f;
        public float PlayerAttackHitDelay => 0f;
        public float PlayerAttackRecoverDelay => 0f;
        public Vector3 MeleeAttackOffset => new Vector3(-1f, 0f, 0f);
        public Vector3 MeleePullbackOffset => new Vector3(-0.2f, 0f, 0f);
        public int BattleTurnCounter { get; set; }
        public int CurrentActorIndex { get; set; }
        public PlayerCharacter PendingActor { get; set; }
        public PlayerMenuAction PendingAction { get; set; }
        public SkillData PendingSkill { get; set; }
        public ItemData PendingItem { get; set; }
        public BattleState CurrentBattleState { get; private set; } = BattleState.ActionExecute;

        public bool SawActiveCameraDuringEnemyMove { get; private set; }
        public bool SawActiveCameraDuringDamage { get; private set; }
        public int DamageNotifications { get; private set; }
        public int ApNotifications { get; private set; }
        public int LastDamage { get; private set; }
        public CharacterBase LastDamageSource { get; private set; }
        public CharacterBase LastDamageTarget { get; private set; }
        public int NarrationRequests { get; private set; }
        public int EnemyActionNotifications { get; private set; }
        public int PlayerTurnNotifications { get; private set; }
        public BattleNarrationMessage LastNarration { get; private set; }

        public void QueueEnemyAction(EnemyAction action)
        {
            _turnQueue.Clear();
            _turnQueue.Add(_enemies[0]);
            CurrentActorIndex = 1;
            _reserved[_enemies[0]] = new BattleQueuedEnemyAction
            {
                Action = action,
                TurnsRemaining = 1
            };
        }

        public bool IsTurnQteCombatInputActive() => ModuleActive;
        public void StartTurnQteCombatLoop() { }
        public void ChangeBattleState(BattleState state) => CurrentBattleState = state;
        public bool CheckVictory() => Victory;
        public bool CheckDefeat() => EvaluatePartySurvival ? _players.TrueForAll(player => !player.IsAlive) : Defeat;
        public bool TryStartNextPartyWave()
        {
            PartyWaveStartCalls++;
            return CanStartNextPartyWave;
        }
        public bool ConsumePlayerPreemptiveAttack() => false;
        public void BroadcastVisibleTurnQueue() { }
        public void ResetAllPlayerBattlePoses() { }
        public IEnumerator WaitForNarrationToFinish() { yield break; }
        public void TryRequestFlavorNarration() { }
        public void NotifyPlayerTurnStarted(PlayerCharacter player) { PlayerTurnNotifications++; }
        public void NotifyEnemyActionStarted(EnemyCharacter enemy, EnemyAttackType attackType)
        {
            EnemyActionNotifications++;
        }
        public void NotifyTargetSelectionStarted(PlayerMenuAction action) { }
        public void RequestNarration(BattleNarrationMessage message)
        {
            NarrationRequests++;
            LastNarration = message;
        }
        public IEnumerator RunAwayRoutine() { RunAwayCalls++; yield break; }
        public void ClearTurnQtePendingActionState() { PendingSkill = null; PendingItem = null; PendingAction = default; }
        public Coroutine StartManagedCoroutine(IEnumerator routine)
        {
            if (routine != null)
            {
                while (routine.MoveNext())
                {
                    IEnumerator nested = routine.Current as IEnumerator;
                    if (nested != null)
                    {
                        while (nested.MoveNext()) { }
                    }
                }
            }

            return null;
        }

        public void SetActorForeground(CharacterBase actor, bool active) { }
        public void EmitDamage(CharacterBase target, int damage, bool isPerfect)
        {
            SawActiveCameraDuringDamage |= CameraController.Instance != null && CameraController.Instance.IsFramingTargets;
        }
        public void EmitDamage(CharacterBase target, int damage, bool isPerfect, int previousHp) { }
        public void EmitDamage(CharacterBase source, CharacterBase target, int damage, bool isCritical)
        {
            SawActiveCameraDuringDamage |= CameraController.Instance != null && CameraController.Instance.IsFramingTargets;
            DamageNotifications++;
            LastDamage = damage;
            LastDamageSource = source;
            LastDamageTarget = target;
        }
        public void EmitApChanged(PlayerCharacter player, int newMp) { ApNotifications++; }
        public void EmitDamageNotificationOnly(CharacterBase target, int damage, bool isPerfect) { }
        public void EmitDamageNotificationOnly(CharacterBase source, CharacterBase target, int damage, bool isCritical) { }
        public void EmitMiss(CharacterBase source, CharacterBase target) { }
        public void PublishEnemyHpScenarioEvent(CharacterBase target, int previousHp, int currentHp, int maxHp, BattleRuleTiming timing) { }
        public void PublishEnemyDefeatedScenarioEvent(CharacterBase target, CharacterBase sourceActor) { }
        public void PublishSkillCompletedScenarioEvent(SkillData skill, CharacterBase sourceActor) { }
        public IEnumerator FlushBattleScenarioEvents(BattleRuleTiming timing) { FlushCalls++; yield break; }
        public SkillData ResolveEnemySequenceSkill(EnemyCharacter enemy, EnemyAction action) => null;
        public EnemyAttackType ResolveEnemySkillAttackType(SkillData skill) => EnemyAttackType.MeleeClose;
        public IEnumerator MoveEnemyToCenterIfNeeded(EnemyCharacter enemy)
        {
            SawActiveCameraDuringEnemyMove |= CameraController.Instance != null && CameraController.Instance.IsFramingTargets;
            yield break;
        }
        public int ResolveEnemyReturnMoveHash(EnemyCharacter enemy) => EnemyCharacter.HashBattleMove;
    }

    private sealed class InterruptingSkillActionBlock : SkillActionBlock
    {
        public Action<SkillContext> OnExecute;

        public override IEnumerator Execute(SkillContext context)
        {
            OnExecute?.Invoke(context);
            yield break;
        }
    }

    private sealed class RecordingSkillActionBlock : SkillActionBlock
    {
        public static int Calls { get; private set; }
        public static bool SawActiveFraming { get; private set; }

        public static void Reset()
        {
            Calls = 0;
            SawActiveFraming = false;
        }

        public override IEnumerator Execute(SkillContext context)
        {
            Calls++;
            SawActiveFraming |= CameraController.Instance != null && CameraController.Instance.IsFramingTargets;
            yield break;
        }
    }
}
