using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class BattleLinkCounterServiceTests
{
    private readonly List<Object> _ownedObjects = new List<Object>();
    private readonly List<IEnumerator> _routines = new List<IEnumerator>();
    private FakeHost _host;
    private EnemyCharacter _enemy;

    [SetUp]
    public void SetUp()
    {
        DOTween.Init();
        _host = new FakeHost();
        _enemy = CreateObject("Counter Enemy").AddComponent<LinkCounterTestEnemy>();
        EnemyData data = ScriptableObject.CreateInstance<EnemyData>();
        _ownedObjects.Add(data);
        data.BaseStats = Stats(1000, 20);
        _enemy.Setup(data);
        _enemy.transform.position = new Vector3(5f, 0f, 0f);
        _host.EnemyList.Add(_enemy);
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = _routines.Count - 1; i >= 0; i--)
            (_routines[i] as IDisposable)?.Dispose();
        _routines.Clear();
        for (int i = _ownedObjects.Count - 1; i >= 0; i--)
        {
            if (_ownedObjects[i] != null)
                Object.DestroyImmediate(_ownedObjects[i]);
        }
        _ownedObjects.Clear();
    }

    [Test]
    public void Execute_OnlyDefenderCountersWithoutChangingOtherAlliesTurnsOrAp()
    {
        PlayerCharacter first = CreatePlayer("First", 10);
        PlayerCharacter defender = CreatePlayer("Defender", 20);
        PlayerCharacter third = CreatePlayer("Third", 30);
        _host.TurnQueue.Add(third);
        _host.CurrentActorIndex = 7;
        _host.BattleTurnCounter = 12;
        int actionExecutions = 0;
        first.OnActionExecuted += () => actionExecutions++;
        defender.OnActionExecuted += () => actionExecutions++;
        third.OnActionExecuted += () => actionExecutions++;
        first.ConsumeAP(5);
        int startingAp = first.CurrentAP;
        int startingHp = first.CurrentHP;
        first.TryApplyStatusEffect(new BleedEffect(3));

        Drain(Start(defender));

        Assert.That(_host.DamageSources, Is.EqualTo(new[] { defender }));
        Assert.That(_host.DamageAmounts, Is.EqualTo(new[] { 30 }));
        Assert.That(_enemy.CurrentHP, Is.EqualTo(970));
        Assert.That(_host.HpEvents, Is.EqualTo(1));
        Assert.That(_host.DefeatChecks, Is.EqualTo(1));
        Assert.That(_host.ApEvents, Is.Zero);
        Assert.That(_host.ForbiddenCalls, Is.Zero);
        Assert.That(first.CurrentAP, Is.EqualTo(startingAp));
        Assert.That(first.CurrentHP, Is.EqualTo(startingHp));
        Assert.That(first.HasEffect(StatusEffectIds.Bleed), Is.True);
        Assert.That(actionExecutions, Is.Zero);
        Assert.That(_host.TurnQueue, Is.EqualTo(new[] { third }));
        Assert.That(_host.CurrentActorIndex, Is.EqualTo(7));
        Assert.That(_host.BattleTurnCounter, Is.EqualTo(12));
        Assert.That(_host.ForegroundActors, Is.Empty);
        Assert.That(first.transform.position, Is.EqualTo(StartPosition));
        Assert.That(defender.transform.position, Is.EqualTo(StartPosition));
        Assert.That(third.transform.position, Is.EqualTo(StartPosition));
    }

    [Test]
    public void Execute_SoloMemberStillCounters()
    {
        PlayerCharacter defender = CreatePlayer("Solo", 20);

        Drain(Start(defender, 2f));

        Assert.That(_host.DamageSources, Is.EqualTo(new[] { defender }));
        Assert.That(_host.DamageAmounts, Is.EqualTo(new[] { 40 }));
    }

    [Test]
    public void Execute_ParryRecoilsOnlyDefenderThenLungesWithoutEarlyDamage()
    {
        PlayerCharacter defender = CreatePlayer("Recoil Defender", 20);
        Vector3 enemyPosition = _enemy.transform.position;
        IEnumerator routine = Start(defender);
        Assert.That(routine.MoveNext(), Is.True);
        GetOnlyTween(defender.transform).Complete(false);
        float recoilX = defender.transform.position.x;
        Assert.That(recoilX, Is.LessThan(StartPosition.x));
        Assert.That(_enemy.transform.position, Is.EqualTo(enemyPosition));
        Assert.That(_host.DamageSources, Is.Empty);
        Assert.That(routine.MoveNext(), Is.True);
        GetOnlyTween(defender.transform).Complete(false);
        Assert.That(defender.transform.position.x, Is.GreaterThan(recoilX));
        Assert.That(_enemy.transform.position, Is.EqualTo(enemyPosition));
        Assert.That(_host.DamageSources, Is.Empty);
        Drain(routine);
        Assert.That(_host.DamageSources, Is.EqualTo(new[] { defender }));
        Assert.That(defender.transform.position, Is.EqualTo(StartPosition));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Execute_PairedReturnRestoresBothActorsEvenWhenCancelled(bool cancel)
    {
        PlayerCharacter defender = CreatePlayer("Defender", 20);
        PlayerCharacter other = CreatePlayer("Other", 50);
        Vector3 enemyHome = new Vector3(8f, 0f, 0f);
        var service = new BattleLinkCounterService(_host);
        IEnumerator routine = service.Execute(_enemy, defender, attackerReturnPosition: enemyHome);
        _routines.Add(routine);
        Assert.That(routine.MoveNext(), Is.True);
        if (cancel)
        {
            GetOnlyTween(defender.transform).Goto(.07f, false);
            service.CancelActive();
            Assert.That(routine.MoveNext(), Is.False);
        }
        else Drain(routine);
        Assert.That(defender.transform.position, Is.EqualTo(StartPosition));
        Assert.That(other.transform.position, Is.EqualTo(StartPosition));
        Assert.That(_enemy.transform.position, Is.EqualTo(enemyHome));
        Assert.That(_host.DamageSources.Count, Is.EqualTo(cancel ? 0 : 1));
        Assert.That(_host.ForegroundActors, Is.Empty);
    }

    [Test]
    public void Execute_OtherMembersNeverJoinEvenWhenFrontContainsDuplicates()
    {
        PlayerCharacter defender = CreatePlayer("Defender", 20);
        PlayerCharacter dead = CreatePlayer("Dead", 100);
        dead.TakePureDamage(dead.MaxHP);
        PlayerCharacter reserve = CreatePlayer("Inactive Reserve", 100);
        reserve.gameObject.SetActive(false);
        PlayerCharacter parentInactive = CreatePlayer("Inactive Parent", 100);
        GameObject parent = CreateObject("Hidden Parent");
        parentInactive.transform.SetParent(parent.transform);
        parent.SetActive(false);
        _host.Front.Add(defender);
        PlayerCharacter second = CreatePlayer("Second", 10);
        PlayerCharacter third = CreatePlayer("Third", 30);
        CreatePlayer("Excess Active Member", 100);

        Drain(Start(defender));

        Assert.That(_host.DamageSources, Is.EqualTo(new[] { defender }));
        Assert.That(dead.IsAlive, Is.False);
        Assert.That(reserve.gameObject.activeSelf, Is.False);
        Assert.That(parentInactive.gameObject.activeInHierarchy, Is.False);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Execute_StagedDefenderReturnsDirectlyHome(bool cancel)
    {
        PlayerCharacter defender = CreatePlayer("Staged Defender", 20);
        defender.transform.position = StartPosition + Vector3.right;
        var service = new BattleLinkCounterService(_host);
        IEnumerator routine = service.Execute(_enemy, defender,
            attackerReturnPosition: Vector3.right * 8f, defenderReturnPosition: StartPosition);
        _routines.Add(routine);
        Assert.That(routine.MoveNext(), Is.True);
        if (cancel) service.CancelActive();
        else Drain(routine);
        Assert.That(defender.transform.position, Is.EqualTo(StartPosition));
        Assert.That(_enemy.transform.position, Is.EqualTo(Vector3.right * 8f));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void DefenderStep_RestoresAnchorAndKeepsVerticalPosition(bool cancel)
    {
        PlayerCharacter defender = CreatePlayer("Step Defender", 20);
        defender.transform.position = new Vector3(-5f, 2f, 0.5f);
        PlayerController controller = defender.gameObject.AddComponent<PlayerController>();
        controller.SetBattleMode(true);
        Vector3 home = defender.transform.position;
        var scope = new BattleDefenderPresentationScope(defender, () => _host.ModuleActive);
        try
        {
            DrainNested(scope.Enter());
            Assert.That(controller.BattleDefenseAnchorPosition, Is.EqualTo(home + Vector3.right));
            controller.ResetDefenseReactionLock();
            Assert.That(defender.transform.position, Is.EqualTo(home + Vector3.right));
            if (cancel) scope.Dispose();
            else DrainNested(scope.Return());
            Assert.That(controller.BattleDefenseAnchorPosition, Is.EqualTo(home));
            Assert.That(defender.transform.position, Is.EqualTo(home));
            defender.transform.position = Vector3.left * 9f;
            scope.Dispose();
            Assert.That(defender.transform.position, Is.EqualTo(Vector3.left * 9f), "Dispose must not run twice.");
        }
        finally { scope.Dispose(); }
    }

    [Test]
    public void DefenderStep_DisablingPlayerCancelsOwnedMovementAndRestoresAnchor()
    {
        PlayerCharacter defender = CreatePlayer("Disabled Defender", 20);
        PlayerController controller = defender.gameObject.AddComponent<PlayerController>();
        controller.SetBattleMode(true);
        using (var scope = new BattleDefenderPresentationScope(defender, () => true))
        {
            IEnumerator enter = scope.Enter();
            _routines.Add(enter);
            Assert.That(enter.MoveNext(), Is.True);
            IEnumerator move = enter.Current as IEnumerator;
            Assert.That(move, Is.Not.Null);
            _routines.Add(move);
            Assert.That(move.MoveNext(), Is.True);
            Tween tween = GetOnlyTween(defender.transform);
            tween.Goto(0.1f, false);
            defender.gameObject.SetActive(false);
            Assert.That(tween.IsActive(), Is.False);
            Assert.That(controller.BattleDefenseAnchorPosition, Is.EqualTo(StartPosition));
            Assert.That(defender.transform.position, Is.EqualTo(StartPosition));
            Assert.That(move.MoveNext(), Is.False);
        }
    }

    private void DrainNested(IEnumerator routine)
    {
        _routines.Add(routine);
        try
        {
            for (int i = 0; i < 50; i++)
            {
                CompletePlayerTweens();
                if (!routine.MoveNext()) return;
                if (routine.Current is IEnumerator child) DrainNested(child);
            }
            Assert.Fail("Defender movement did not finish.");
        }
        finally { (routine as IDisposable)?.Dispose(); }
    }

    [Test]
    public void DefenderStep_KilledMovementDoesNotBecomeSuccessfulStaging()
    {
        PlayerCharacter defender = CreatePlayer("Interrupted Step", 20);
        using (var scope = new BattleDefenderPresentationScope(defender, () => true))
        {
            IEnumerator enter = scope.Enter();
            _routines.Add(enter);
            Assert.That(enter.MoveNext(), Is.True);
            IEnumerator move = enter.Current as IEnumerator;
            Assert.That(move, Is.Not.Null);
            _routines.Add(move);
            Assert.That(move.MoveNext(), Is.True);
            GetOnlyTween(defender.transform).Kill(false);
            Assert.That(move.MoveNext(), Is.False);
            Assert.That(enter.MoveNext(), Is.False);
            Assert.That(scope.IsStaged, Is.False);
            Assert.That(defender.transform.position, Is.EqualTo(StartPosition));
        }
    }

    [Test]
    public void Execute_DefenderOutsideCurrentFrontCannotStartCounter()
    {
        CreatePlayer("Current Front", 10);
        PlayerCharacter outsider = CreatePlayer("Outside Front", 100);
        _host.Front.Remove(outsider);

        Drain(Start(outsider));

        Assert.That(_host.DamageSources, Is.Empty);
        Assert.That(_host.ForegroundChanges, Is.Zero);
    }

    [Test]
    public void Execute_ReservePromotionCannotJoinDefenderCounter()
    {
        PlayerCharacter defender = CreatePlayer("Defender", 20);
        PlayerCharacter removed = CreatePlayer("Removed", 30);
        PlayerCharacter third = CreatePlayer("Third", 10);
        IEnumerator routine = Start(defender);
        Assert.That(routine.MoveNext(), Is.True);
        _host.Front.Remove(removed);
        PlayerCharacter newReserve = CreatePlayer("New Reserve", 100);

        Drain(routine);

        Assert.That(_host.DamageSources, Is.EqualTo(new[] { defender }));
        Assert.That(_host.DamageSources, Has.No.Member(newReserve));
    }

    [Test]
    public void Execute_CancelledBeforeStartHasNoPresentationOrDamage()
    {
        PlayerCharacter defender = CreatePlayer("Defender", 20);

        Drain(Start(defender, isExecutionActive: () => false));

        Assert.That(_host.DamageSources, Is.Empty);
        Assert.That(_host.ForegroundChanges, Is.Zero);
    }

    [Test]
    public void Execute_CancellationDuringLungeRestoresOnlyOwnedPresentation()
    {
        PlayerCharacter defender = CreatePlayer("Defender", 20);
        bool active = true;
        IEnumerator routine = Start(defender, isExecutionActive: () => active);
        Assert.That(routine.MoveNext(), Is.True);
        Tween lunge = GetOnlyTween(defender.transform);
        lunge.Goto(0.06f, false);
        Assert.That(defender.transform.position, Is.Not.EqualTo(StartPosition));
        Tween unrelated = defender.transform.DOScale(Vector3.one * 2f, 2f).SetAutoKill(false);
        try
        {
            active = false;
            Assert.That(routine.MoveNext(), Is.False);

            Assert.That(_host.DamageSources, Is.Empty);
            Assert.That(lunge.IsActive(), Is.False);
            Assert.That(unrelated.IsActive(), Is.True);
            Assert.That(defender.transform.position, Is.EqualTo(StartPosition));
            Assert.That(_host.ForegroundActors, Is.Empty);
        }
        finally
        {
            unrelated.Kill(false);
        }
    }

    [Test]
    public void Execute_DisposedBeforeFirstTweenUpdateReleasesLunge()
    {
        PlayerCharacter defender = CreatePlayer("Defender", 20);
        IEnumerator routine = Start(defender);
        Assert.That(routine.MoveNext(), Is.True);
        Tween lunge = GetOnlyTween(defender.transform);

        ((IDisposable)routine).Dispose();

        Assert.That(lunge.IsActive(), Is.False);
        Assert.That(_host.DamageSources, Is.Empty);
        Assert.That(_host.ForegroundActors, Is.Empty);
        Assert.That(defender.transform.position, Is.EqualTo(StartPosition));
    }

    [Test]
    public void CancelActive_RestoresImmediatelyAndLateIteratorCannotOverwriteNextOwner()
    {
        PlayerCharacter defender = CreatePlayer("Defender", 20);
        var service = new BattleLinkCounterService(_host);
        IEnumerator routine = service.Execute(_enemy, defender);
        _routines.Add(routine);
        Assert.That(routine.MoveNext(), Is.True);
        Tween lunge = GetOnlyTween(defender.transform);
        lunge.Goto(0.06f, false);

        service.CancelActive();

        Assert.That(lunge.IsActive(), Is.False);
        Assert.That(defender.transform.position, Is.EqualTo(StartPosition));
        Assert.That(_host.ForegroundActors, Is.Empty);
        Vector3 nextOwnerPosition = new Vector3(2f, 3f, 0f);
        defender.transform.position = nextOwnerPosition;
        service.CancelActive();
        Assert.That(routine.MoveNext(), Is.False);
        ((IDisposable)routine).Dispose();
        Assert.That(defender.transform.position, Is.EqualTo(nextOwnerPosition));
        Assert.That(_host.DamageSources, Is.Empty);
    }

    [Test]
    public void ExitTurnQteModule_ExplicitlyReleasesSharedCounterBeforeCoroutineResumes()
    {
        PlayerCharacter defender = CreatePlayer("Defender", 20);
        var counter = new BattleLinkCounterService(_host);
        var controller = new BattleTurnQteModuleControllerService(_host, counter);
        IEnumerator routine = counter.Execute(_enemy, defender);
        _routines.Add(routine);
        Assert.That(routine.MoveNext(), Is.True);
        Tween lunge = GetOnlyTween(defender.transform);
        lunge.Goto(0.06f, false);

        IEnumerator exit = controller.ExitTurnQteModule(null);
        Assert.That(exit.MoveNext(), Is.False);

        Assert.That(lunge.IsActive(), Is.False);
        Assert.That(defender.transform.position, Is.EqualTo(StartPosition));
        Assert.That(_host.ForegroundActors, Is.Empty);
        Assert.That(routine.MoveNext(), Is.False);
        Assert.That(_host.DamageSources, Is.Empty);
    }

    [Test]
    public void Execute_ModuleExitBeforeHitStopsDamage()
    {
        PlayerCharacter defender = CreatePlayer("Defender", 20);
        IEnumerator routine = Start(defender);
        Assert.That(routine.MoveNext(), Is.True);
        _host.ModuleActive = false;

        Assert.That(routine.MoveNext(), Is.False);

        Assert.That(_host.DamageSources, Is.Empty);
        Assert.That(_host.ForegroundActors, Is.Empty);
        Assert.That(defender.transform.position, Is.EqualTo(StartPosition));
    }

    [Test]
    public void Execute_EnemyDefeatedByFirstHitStopsRemainingHits()
    {
        PlayerCharacter defender = CreatePlayer("Defender", 1000);
        CreatePlayer("Second", 10);
        CreatePlayer("Third", 10);

        Drain(Start(defender));

        Assert.That(_enemy.IsAlive, Is.False);
        Assert.That(_host.DamageSources, Is.EqualTo(new[] { defender }));
        Assert.That(_host.ForegroundActors, Is.Empty);
        Assert.That(defender.transform.position, Is.EqualTo(StartPosition));
    }

    [Test]
    public void Execute_DestroyedEnemyBeforeHitStopsWithoutDamage()
    {
        PlayerCharacter defender = CreatePlayer("Defender", 20);
        IEnumerator routine = Start(defender);
        Assert.That(routine.MoveNext(), Is.True);
        Object.DestroyImmediate(_enemy.gameObject);

        Assert.That(routine.MoveNext(), Is.False);

        Assert.That(_host.DamageSources, Is.Empty);
        Assert.That(_host.ForegroundActors, Is.Empty);
        Assert.That(defender.transform.position, Is.EqualTo(StartPosition));
    }

    [Test]
    public void Execute_ExternallyKilledLungeCannotApplyDamage()
    {
        PlayerCharacter defender = CreatePlayer("Defender", 20);
        IEnumerator routine = Start(defender);
        Assert.That(routine.MoveNext(), Is.True);
        GetOnlyTween(defender.transform).Kill(false);

        Assert.That(routine.MoveNext(), Is.False);

        Assert.That(_host.DamageSources, Is.Empty);
        Assert.That(_host.ForegroundActors, Is.Empty);
    }

    [Test]
    public void Execute_KilledLungeCannotAdoptAnotherRecycledMovement()
    {
        bool previousRecycling = DOTween.defaultRecyclable;
        DOTween.defaultRecyclable = true;
        Tween replacement = null;
        try
        {
            PlayerCharacter defender = CreatePlayer("Defender", 20);
            IEnumerator routine = Start(defender);
            Assert.That(routine.MoveNext(), Is.True);
            Tween lunge = GetOnlyTween(defender.transform);
            lunge.Kill(false);
            replacement = defender.transform.DOMove(Vector3.up, 2f).SetAutoKill(false);

            Assert.That(routine.MoveNext(), Is.False);

            Assert.That(_host.DamageSources, Is.Empty);
            Assert.That(replacement.IsActive(), Is.True);
            Assert.That(_host.ForegroundActors, Is.Empty);
        }
        finally
        {
            replacement?.Kill(false);
            DOTween.defaultRecyclable = previousRecycling;
        }
    }

    private static readonly Vector3 StartPosition = new Vector3(-5f, 0f, 0f);

    private IEnumerator Start(PlayerCharacter defender, float multiplier = 1.5f, Func<bool> isExecutionActive = null)
    {
        IEnumerator routine = new BattleLinkCounterService(_host).Execute(_enemy, defender, multiplier, isExecutionActive);
        _routines.Add(routine);
        return routine;
    }

    private void Drain(IEnumerator routine)
    {
        for (int frame = 0; frame < 30; frame++)
        {
            CompletePlayerTweens();
            if (!routine.MoveNext())
                return;
            Assert.That(routine.Current, Is.Null, "Counter must stay a flat iterator for cancellation disposal.");
        }
        Assert.Fail("Counter failed to finish after all owned movement tweens completed.");
    }

    private void CompletePlayerTweens()
    {
        for (int i = 0; i < _ownedObjects.Count; i++)
        {
            if (!(_ownedObjects[i] is GameObject playerObject) || playerObject == null)
                continue;
            if (playerObject.GetComponent<PlayerCharacter>() == null)
                continue;
            List<Tween> tweens = DOTween.TweensByTarget(playerObject.transform);
            if (tweens == null)
                continue;
            for (int j = 0; j < tweens.Count; j++)
                tweens[j].Complete(false);
        }
    }

    private static Tween GetOnlyTween(Transform target)
    {
        List<Tween> tweens = DOTween.TweensByTarget(target);
        Assert.That(tweens, Has.Count.EqualTo(1));
        return tweens[0];
    }

    private PlayerCharacter CreatePlayer(string name, int attack)
    {
        PlayerCharacter player = CreateObject(name).AddComponent<PlayerCharacter>();
        CharacterData data = ScriptableObject.CreateInstance<CharacterData>();
        _ownedObjects.Add(data);
        data.CharacterID = name;
        data.BaseStats = Stats(100, attack);
        player.SetCharacterData(data);
        player.transform.position = StartPosition;
        _host.Front.Add(player);
        return player;
    }

    private GameObject CreateObject(string name)
    {
        var result = new GameObject(name);
        _ownedObjects.Add(result);
        return result;
    }

    private static StatBlock Stats(int hp, int attack)
    {
        return new StatBlock { MaxHP = hp, MaxAP = 50, ATK = attack, DEF = 0, SPD = 10 };
    }

    private sealed class LinkCounterTestEnemy : EnemyCharacter
    {
        protected override void OnDamageTaken(int damage) { }
        protected override void OnDie() { }
    }

    private sealed class FakeHost : IBattleTurnQteHost
    {
        public readonly List<PlayerCharacter> Front = new List<PlayerCharacter>();
        public readonly List<EnemyCharacter> EnemyList = new List<EnemyCharacter>();
        public readonly List<CharacterBase> DamageSources = new List<CharacterBase>();
        public readonly List<int> DamageAmounts = new List<int>();
        public readonly HashSet<CharacterBase> ForegroundActors = new HashSet<CharacterBase>();
        public bool ModuleActive = true;
        public int ForegroundChanges;
        public int HpEvents;
        public int DefeatChecks;
        public int ApEvents;
        public int ForbiddenCalls;
        public IReadOnlyList<PlayerCharacter> PlayerParty => Front;
        public IReadOnlyList<EnemyCharacter> Enemies => EnemyList;
        public IList<CharacterBase> TurnQueue { get; } = new List<CharacterBase>();
        public IDictionary<EnemyCharacter, BattleQueuedEnemyAction> ReservedEnemyActions { get; }
            = new Dictionary<EnemyCharacter, BattleQueuedEnemyAction>();
        public WaitForSeconds WaitShort => null;
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
        public Vector3 MeleePullbackOffset => Vector3.zero;
        public int BattleTurnCounter { get; set; }
        public int CurrentActorIndex { get; set; }
        public PlayerCharacter PendingActor { get; set; }
        public PlayerMenuAction PendingAction { get; set; }
        public SkillData PendingSkill { get; set; }
        public ItemData PendingItem { get; set; }
        public BattleState CurrentBattleState => BattleState.ActionExecute;
        public bool CanEscape => true;
        public bool IsTurnQteCombatInputActive() => ModuleActive;
        public void StartTurnQteCombatLoop() { ForbiddenCalls++; }
        public void ChangeBattleState(BattleState state) { ForbiddenCalls++; }
        public bool CheckVictory() { ForbiddenCalls++; return false; }
        public bool CheckDefeat() { ForbiddenCalls++; return false; }
        public bool TryStartNextPartyWave() { ForbiddenCalls++; return false; }
        public bool ConsumePlayerPreemptiveAttack() { ForbiddenCalls++; return false; }
        public void BroadcastVisibleTurnQueue() { ForbiddenCalls++; }
        public void ResetAllPlayerBattlePoses() { ForbiddenCalls++; }
        public IEnumerator WaitForNarrationToFinish() { ForbiddenCalls++; yield break; }
        public void TryRequestFlavorNarration() { ForbiddenCalls++; }
        public void NotifyPlayerTurnStarted(PlayerCharacter player) { ForbiddenCalls++; }
        public void NotifyEnemyActionStarted(EnemyCharacter enemy, EnemyAttackType type) { ForbiddenCalls++; }
        public void NotifyTargetSelectionStarted(PlayerMenuAction action) { ForbiddenCalls++; }
        public void RequestNarration(BattleNarrationMessage message) { ForbiddenCalls++; }
        public IEnumerator RunAwayRoutine() { ForbiddenCalls++; yield break; }
        public void ClearTurnQtePendingActionState() { ForbiddenCalls++; }
        public Coroutine StartManagedCoroutine(IEnumerator routine) { ForbiddenCalls++; return null; }
        public void SetActorForeground(CharacterBase actor, bool active)
        {
            ForegroundChanges++;
            if (active) ForegroundActors.Add(actor);
            else ForegroundActors.Remove(actor);
        }
        public void EmitDamage(CharacterBase target, int damage, bool isPerfect) { ForbiddenCalls++; }
        public void EmitDamage(CharacterBase target, int damage, bool isPerfect, int previousHp) { ForbiddenCalls++; }
        public void EmitDamage(CharacterBase source, CharacterBase target, int damage, bool isCritical) { ForbiddenCalls++; }
        public void EmitApChanged(PlayerCharacter player, int newAp) { ApEvents++; }
        public void EmitDamageNotificationOnly(CharacterBase target, int damage, bool isPerfect) { ForbiddenCalls++; }
        public void EmitDamageNotificationOnly(CharacterBase source, CharacterBase target, int damage, bool isCritical)
        {
            DamageSources.Add(source);
            DamageAmounts.Add(damage);
            Assert.That(isCritical, Is.False);
        }
        public void EmitMiss(CharacterBase source, CharacterBase target) { ForbiddenCalls++; }
        public void PublishEnemyHpScenarioEvent(CharacterBase target, int previousHp, int currentHp, int maxHp, BattleRuleTiming timing)
        {
            HpEvents++;
            Assert.That(timing, Is.EqualTo(BattleRuleTiming.AfterCurrentAction));
            Assert.That(currentHp, Is.LessThan(previousHp));
        }
        public void PublishEnemyDefeatedScenarioEvent(CharacterBase target, CharacterBase sourceActor) { DefeatChecks++; }
        public void PublishSkillCompletedScenarioEvent(SkillData skill, CharacterBase sourceActor) { ForbiddenCalls++; }
        public IEnumerator FlushBattleScenarioEvents(BattleRuleTiming timing) { ForbiddenCalls++; yield break; }
        public SkillData ResolveEnemySequenceSkill(EnemyCharacter enemy, EnemyAction action) { ForbiddenCalls++; return null; }
        public EnemyAttackType ResolveEnemySkillAttackType(SkillData skill) { ForbiddenCalls++; return EnemyAttackType.MeleeClose; }
        public IEnumerator MoveEnemyToCenterIfNeeded(EnemyCharacter enemy) { ForbiddenCalls++; yield break; }
        public int ResolveEnemyReturnMoveHash(EnemyCharacter enemy) { ForbiddenCalls++; return EnemyCharacter.HashBattleMove; }
    }
}
