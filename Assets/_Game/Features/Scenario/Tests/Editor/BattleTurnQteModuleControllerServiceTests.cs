using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
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
        private readonly GameObject _playerObject;
        private readonly GameObject _enemyObject;

        public TurnQteFixture()
        {
            _positionManagerObject = new GameObject("PositionManager");
            _positionManager = _positionManagerObject.AddComponent<PositionManager>();
            SetStaticProperty(typeof(PositionManager), "Instance", _positionManager);

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

            Host = new FakeTurnQteHost(Player, Enemy);
        }

        public PlayerCharacter Player { get; }
        public EnemyCharacter Enemy { get; }
        public FakeTurnQteHost Host { get; }

        public void Dispose()
        {
            for (int i = 0; i < _assets.Count; i++)
            {
                UnityEngine.Object.DestroyImmediate(_assets[i]);
            }

            UnityEngine.Object.DestroyImmediate(_enemyObject);
            UnityEngine.Object.DestroyImmediate(_playerObject);
            UnityEngine.Object.DestroyImmediate(_positionManagerObject);
            SetStaticProperty(typeof(PositionManager), "Instance", null);
        }

        private static void SetStaticProperty(Type type, string propertyName, object value)
        {
            PropertyInfo property = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Static);
            property.GetSetMethod(true).Invoke(null, new[] { value });
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

        public bool IsTurnQteCombatInputActive() => true;
        public void StartTurnQteCombatLoop() { }
        public void ChangeBattleState(BattleState state) => CurrentBattleState = state;
        public bool CheckVictory() => false;
        public bool CheckDefeat() => false;
        public void BroadcastVisibleTurnQueue() { }
        public void ResetAllPlayerBattlePoses() { }
        public IEnumerator WaitForNarrationToFinish() { yield break; }
        public void TryRequestFlavorNarration() { }
        public void NotifyPlayerTurnStarted(PlayerCharacter player) { }
        public void NotifyEnemyActionStarted(EnemyCharacter enemy, EnemyAttackType attackType) { }
        public void NotifyTargetSelectionStarted(PlayerMenuAction action) { }
        public void RequestNarration(BattleNarrationMessage message) { }
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
        public void EmitDamage(CharacterBase target, int damage, bool isPerfect) { }
        public void EmitDamage(CharacterBase target, int damage, bool isPerfect, int previousHp) { }
        public void EmitMpChanged(PlayerCharacter player, int newMp) { }
        public void EmitDamageNotificationOnly(CharacterBase target, int damage, bool isPerfect) { }
        public void PublishEnemyHpScenarioEvent(CharacterBase target, int previousHp, int currentHp, int maxHp, BattleRuleTiming timing) { }
        public IEnumerator FlushBattleScenarioEvents(BattleRuleTiming timing) { FlushCalls++; yield break; }
        public SkillData ResolveEnemySequenceSkill(EnemyCharacter enemy, EnemyAction action) => null;
        public EnemyAttackType ResolveEnemySkillAttackType(SkillData skill) => EnemyAttackType.MeleeClose;
        public IEnumerator MoveEnemyToCenterIfNeeded(EnemyCharacter enemy) { yield break; }
        public int ResolveEnemyReturnMoveHash(EnemyCharacter enemy) => EnemyCharacter.HashBattleMove;
    }

    private sealed class RecordingSkillActionBlock : SkillActionBlock
    {
        public static int Calls { get; private set; }

        public static void Reset()
        {
            Calls = 0;
        }

        public override IEnumerator Execute(SkillContext context)
        {
            Calls++;
            yield break;
        }
    }
}