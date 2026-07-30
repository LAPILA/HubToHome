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
    [Test]
    public void ExecuteSkill_SkipsDisabledSkillBlocksInTurnQtePath()
    {
        var fixture = new TurnQteFixture();
        try
        {
            RecordingSkillActionBlock.Reset();
            SkillData skill = ScriptableObject.CreateInstance<SkillData>();
            skill.SkillID = "player_slash";
            skill.MPCost = 0;
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
            skill.MPCost = 0;
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
            skill.MPCost = 0;
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
            skill.MPCost = 0;
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
    public void RunEnemyAction_AoeFramesEnemyAndAliveTargetsDuringDamage()
    {
        var fixture = new TurnQteFixture();
        try
        {
            fixture.Host.QueueEnemyAction(EnemyAction.EnragedAttack);
            var service = new BattleTurnQteModuleControllerService(fixture.Host);

            RunToCompletion(service.RunEnemyAction());

            Assert.That(fixture.Host.SawActiveCameraDuringDamage, Is.True);
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
        return (IEnumerator)method.Invoke(service, new object[] { actor, skill });
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

        public TurnQteFixture()
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
            _cameraController.SetDefaultTarget(_positionManagerObject.transform, true);
            _cameraController.ResetCamera(0f);

            _playerObject = new GameObject("Player");
            Player = _playerObject.AddComponent<PlayerCharacter>();
            CharacterData playerData = ScriptableObject.CreateInstance<CharacterData>();
            playerData.CharacterID = "player";
            playerData.DisplayName = "Player";
            Player.SetCharacterData(playerData);
            Player.HealHP(Player.MaxHP);
            Player.HealMP(Player.MaxMP);
            _assets.Add(playerData);

            _enemyObject = new GameObject("Enemy");
            Enemy = _enemyObject.AddComponent<EnemyCharacter>();
            EnemyData enemyData = ScriptableObject.CreateInstance<EnemyData>();
            enemyData.EnemyId = "zev";
            enemyData.EnemyName = "ZEV";
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
        public IReadOnlyList<PlayerCharacter> PlayerParty => _players;
        public IReadOnlyList<EnemyCharacter> Enemies => _enemies;
        public IList<CharacterBase> TurnQueue => _turnQueue;
        public IDictionary<EnemyCharacter, BattleQueuedEnemyAction> ReservedEnemyActions => _reserved;
        public WaitForSeconds WaitShort => _waitShort;
        public int MaxTurnQueueSize => 8;
        public int MpPerTurn => 5;
        public int MpOnParryPerfect => 20;
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
        public int NarrationRequests { get; private set; }
        public int EnemyActionNotifications { get; private set; }
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

        public bool IsTurnQteCombatInputActive() => true;
        public void StartTurnQteCombatLoop() { }
        public void ChangeBattleState(BattleState state) => CurrentBattleState = state;
        public bool CheckVictory() => false;
        public bool CheckDefeat() => false;
        public bool ConsumePlayerPreemptiveAttack() => false;
        public void BroadcastVisibleTurnQueue() { }
        public void ResetAllPlayerBattlePoses() { }
        public IEnumerator WaitForNarrationToFinish() { yield break; }
        public void TryRequestFlavorNarration() { }
        public void NotifyPlayerTurnStarted(PlayerCharacter player) { }
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
        public IEnumerator RunAwayRoutine() { yield break; }
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
        }
        public void EmitMpChanged(PlayerCharacter player, int newMp) { }
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
