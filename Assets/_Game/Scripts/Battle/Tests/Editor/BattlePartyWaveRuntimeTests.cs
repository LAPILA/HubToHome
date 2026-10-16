using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using DG.Tweening;

public sealed class BattlePartyWaveRuntimeTests
{
    private readonly List<Object> _ownedObjects = new List<Object>();
    private BattleManager _manager;
    private PositionManager _positionManager;
    private List<PlayerCharacter> _front;
    private List<PlayerCharacter> _reserve;

    [SetUp]
    public void SetUp()
    {
        _positionManager = CreateComponent<PositionManager>("Party Wave Positions");
        var positions = new List<Transform>();
        for (int i = 0; i < 3; i++)
        {
            GameObject position = CreateObject("Player Slot " + i);
            position.transform.position = new Vector3(-6f + (i * 2f), -1f, 0f);
            positions.Add(position.transform);
        }
        SetPrivateField(_positionManager, "_playerDefaultPos", positions);

        _manager = CreateComponent<BattleManager>("Party Wave Battle Manager");
        _front = new List<PlayerCharacter>();
        _reserve = new List<PlayerCharacter>();
        for (int i = 0; i < 3; i++)
        {
            PlayerCharacter front = CreatePlayer("front." + i, true);
            front.TakePureDamage(front.MaxHP);
            _front.Add(front);
            _manager._playerParty.Add(front);

            PlayerCharacter reserve = CreatePlayer("reserve." + i, false);
            _reserve.Add(reserve);
            GetPrivateList("_reserveParty").Add(reserve);
        }

        List<PlayerCharacter> roster = GetPrivateList("_battlePartyRoster");
        roster.AddRange(_front);
        roster.AddRange(_reserve);
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = _ownedObjects.Count - 1; i >= 0; i--)
        {
            if (_ownedObjects[i] != null)
                Object.DestroyImmediate(_ownedObjects[i]);
        }
        _ownedObjects.Clear();
    }

    [Test]
    public void PromoteReserveWave_DeactivatesDefeatedFrontAndActivatesPreparedReserve()
    {
        GetPrivateTurnQueue().Add(_front[0]);

        bool promoted = InvokePromoteReserveWave();

        Assert.That(promoted, Is.True);
        Assert.That(_manager._playerParty, Is.EqualTo(_reserve));
        Assert.That(GetPrivateList("_reserveParty"), Is.Empty);
        Assert.That(GetPrivateTurnQueue(), Is.Empty);
        for (int i = 0; i < 3; i++)
        {
            Assert.That(_front[i].gameObject.activeSelf, Is.False);
            Assert.That(_reserve[i].gameObject.activeSelf, Is.True);
            Assert.That(
                _reserve[i].transform.position,
                Is.EqualTo(_positionManager.GetPlayerDefaultPos(i)));
        }
    }

    [Test]
    public void PromoteReserveWave_WhenFrontStillAlive_DoesNothing()
    {
        PlayerCharacter aliveFront = CreatePlayer("front.alive", true);
        _manager._playerParty[0] = aliveFront;

        bool promoted = InvokePromoteReserveWave();

        Assert.That(promoted, Is.False);
        Assert.That(_manager._playerParty[0], Is.SameAs(aliveFront));
        Assert.That(GetPrivateList("_reserveParty"), Has.Count.EqualTo(3));
        for (int i = 0; i < _reserve.Count; i++)
            Assert.That(_reserve[i].gameObject.activeSelf, Is.False);
    }

    [Test]
    public void WaveExitFadesForHalfASecondWithoutEarlyDeactivationAndRestoresOnCancel()
    {
        SpriteRenderer renderer = _front[0].gameObject.AddComponent<SpriteRenderer>();
        Color original = new Color(0.2f, 0.6f, 0.9f, 0.8f);
        renderer.color = original;
        using var presentation = new BattlePartyWavePresentation();
        IEnumerator fade = presentation.FadeOut(_front, 0.5f);
        Assert.That(fade.MoveNext(), Is.True);
        Sequence tween = GetWaveTween(presentation);
        tween.Pause();
        Assert.That(tween.Duration(), Is.EqualTo(0.5f).Within(0.001f));
        tween.Goto(0.25f);
        Assert.That(renderer.color.a, Is.EqualTo(0.4f).Within(0.01f));
        Assert.That(_front[0].gameObject.activeSelf, Is.True);
        Assert.That(_reserve[0].gameObject.activeSelf, Is.False);
        presentation.Dispose();
        Assert.That(renderer.color, Is.EqualTo(original));
        Assert.That(tween.IsActive(), Is.False);
    }

    [Test]
    public void WaveEntryStartsOutsideCameraAndCancellationRestoresEverySlot()
    {
        Assert.That(InvokePromoteReserveWave(), Is.True);
        Camera camera = CreateComponent<Camera>("Wave Camera");
        camera.orthographic = true;
        camera.orthographicSize = 6f;
        camera.aspect = 4f / 3f;
        camera.transform.position = new Vector3(0, 0, -10);
        using var presentation = new BattlePartyWavePresentation();
        IEnumerator enter = presentation.Enter(_reserve, camera, 0.55f, 1f);
        Assert.That(enter.MoveNext(), Is.True);
        Sequence tween = GetWaveTween(presentation);
        tween.Pause();
        for (int i = 0; i < _reserve.Count; i++)
        {
            Assert.That(camera.WorldToViewportPoint(_reserve[i].transform.position).x, Is.LessThan(0));
            Assert.That(_reserve[i].transform.position.y, Is.EqualTo(_positionManager.GetPlayerDefaultPos(i).y));
        }
        tween.Goto(0.2f);
        presentation.Dispose();
        for (int i = 0; i < _reserve.Count; i++)
            Assert.That(_reserve[i].transform.position, Is.EqualTo(_positionManager.GetPlayerDefaultPos(i)));
        Assert.That(tween.IsActive(), Is.False);
    }

    [Test]
    public void DisposingWavePhaseKillsItsTweenAndRestoresSprites()
    {
        SpriteRenderer renderer = _front[0].gameObject.AddComponent<SpriteRenderer>();
        renderer.color = Color.cyan;
        using var presentation = new BattlePartyWavePresentation();
        IEnumerator fade = presentation.FadeOut(_front, 0.5f);
        Assert.That(fade.MoveNext(), Is.True);
        Sequence tween = GetWaveTween(presentation);
        tween.Goto(0.25f);
        ((System.IDisposable)fade).Dispose();
        Assert.That(tween.IsActive(), Is.False);
        Assert.That(renderer.color, Is.EqualTo(Color.cyan));
    }

    [Test]
    public void InterruptedWaveRoutineClearsOnlyItsOwnTransitionLock()
    {
        IEnumerator current = BeginWaveRoutineForTest(7);
        Assert.That(current.MoveNext(), Is.True);
        ((System.IDisposable)current).Dispose();
        Assert.That(GetPrivateField<bool>(_manager, "_isPartyWaveTransitioning"), Is.False);

        IEnumerator stale = BeginWaveRoutineForTest(8);
        Assert.That(stale.MoveNext(), Is.True);
        SetPrivateField(_manager, "_partyWaveTransitionVersion", 9);
        ((System.IDisposable)stale).Dispose();
        Assert.That(GetPrivateField<bool>(_manager, "_isPartyWaveTransitioning"), Is.True);
    }

    [Test]
    public void KilledFadeStillPromotesReserveAndPartyEventCanCancelEntry()
    {
        BeginWaveRoutineForTest(7);
        IEnumerator routine = (IEnumerator)typeof(BattleManager).GetMethod("PresentPartyWaveTransition",
            BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_manager, new object[] { 7 });
        Assert.That(routine.MoveNext(), Is.True);
        IEnumerator fade = (IEnumerator)routine.Current;
        Assert.That(fade.MoveNext(), Is.True);
        var presentation = GetPrivateField<BattlePartyWavePresentation>(_manager, "_partyWavePresentation");
        GetWaveTween(presentation).Kill(false);
        Assert.That(((IEnumerator)fade.Current).MoveNext(), Is.False);
        Assert.That(fade.MoveNext(), Is.False);
        _manager.OnPlayerPartyChanged += _ => typeof(BattleManager).GetMethod("CancelPartyWaveTransition",
            BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_manager, null);
        Assert.That(routine.MoveNext(), Is.False);
        Assert.That(_manager._playerParty, Is.EqualTo(_reserve));
        Assert.That(GetPrivateField<bool>(_manager, "_isPartyWaveTransitioning"), Is.False);
        for (int i = 0; i < _reserve.Count; i++)
            Assert.That(_reserve[i].transform.position, Is.EqualTo(_positionManager.GetPlayerDefaultPos(i)));
    }

    private IEnumerator BeginWaveRoutineForTest(int version)
    {
        SetPrivateField(_manager, "_partyWaveTransitionVersion", version);
        SetPrivateField(_manager, "_isPartyWaveTransitioning", true);
        return (IEnumerator)typeof(BattleManager).GetMethod("CompletePartyWaveTransition",
            BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_manager, new object[] { version });
    }

    [TestCase(true)]
    [TestCase(false)]
    public void ReserveChoice_DoesNotPromoteBeforeDecisionAndConsumesOnlyOneDecision(bool continueBattle)
    {
        IEnumerator routine = BeginWaveRoutineForTest(12);
        Assert.That(routine.MoveNext(), Is.True);
        IEnumerator prompt = (IEnumerator)routine.Current;
        Assert.That(prompt.MoveNext(), Is.True);
        var view = GetPrivateField<BattleReserveChoiceUI>(_manager, "_partyWaveChoice");
        Assert.That(_manager.IsAwaitingReserveChoice, Is.True);
        Assert.That(_manager._playerParty, Is.EqualTo(_front));
        Assert.That(_reserve.TrueForAll(p => !p.gameObject.activeSelf), Is.True);
        Assert.That(view.TryChoose(continueBattle), Is.False, "열린 프레임에는 선택을 확정하지 않습니다.");
        SetPrivateField(view, "_openedFrame", -1);
        Assert.That(view.TryChoose(continueBattle), Is.True);
        Assert.That(view.TryChoose(!continueBattle), Is.False);
        Assert.That(view.Result, Is.EqualTo(continueBattle
            ? BattleReserveChoiceUI.Choice.Continue : BattleReserveChoiceUI.Choice.GiveUp));
        Assert.That(prompt.MoveNext(), Is.False);
        Assert.That(_manager.IsAwaitingReserveChoice, Is.False);
        Assert.That(routine.MoveNext(), Is.EqualTo(continueBattle));
        if (!continueBattle)
        {
            Assert.That(GetPrivateField<bool>(_manager, "_partyWaveAbandoned"), Is.True);
            Assert.That(_manager._playerParty, Is.EqualTo(_front));
            Assert.That(_reserve.TrueForAll(p => !p.gameObject.activeSelf), Is.True);
        }
        ((System.IDisposable)routine).Dispose();
    }

    [Test]
    public void CancelWave_ClosesPromptWithoutActivatingReserve()
    {
        IEnumerator routine = BeginWaveRoutineForTest(13);
        Assert.That(routine.MoveNext(), Is.True);
        IEnumerator prompt = (IEnumerator)routine.Current;
        Assert.That(prompt.MoveNext(), Is.True);
        typeof(BattleManager).GetMethod("CancelPartyWaveTransition", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(_manager, null);
        Assert.That(_manager.IsAwaitingReserveChoice, Is.False);
        Assert.That(prompt.MoveNext(), Is.False);
        Assert.That(routine.MoveNext(), Is.False);
        Assert.That(_manager._playerParty, Is.EqualTo(_front));
        Assert.That(GetPrivateField<bool>(_manager, "_isPartyWaveTransitioning"), Is.False);
    }

    private static T GetPrivateField<T>(object target, string name) => (T)target.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);

    [Test]
    public void WaveCompletionAndDestroyedActorCleanupAreSafe()
    {
        SpriteRenderer renderer = _front[0].gameObject.AddComponent<SpriteRenderer>();
        using var presentation = new BattlePartyWavePresentation();
        IEnumerator fade = presentation.FadeOut(_front, 0.5f);
        Assert.That(fade.MoveNext(), Is.True);
        Object.DestroyImmediate(renderer);
        GetWaveTween(presentation).Complete();
        Assert.That(((IEnumerator)fade.Current).MoveNext(), Is.False);
        Assert.That(fade.MoveNext(), Is.False);
        Assert.That(presentation.Completed, Is.True);
        Assert.That(InvokePromoteReserveWave(), Is.True);
        IEnumerator enter = presentation.Enter(_reserve, null, 0.55f, 1f);
        Assert.That(enter.MoveNext(), Is.True);
        Object.DestroyImmediate(_reserve[1].gameObject);
        Assert.DoesNotThrow(() => GetWaveTween(presentation).Complete());
        Assert.That(((IEnumerator)enter.Current).MoveNext(), Is.False);
        Assert.That(enter.MoveNext(), Is.False);
        Assert.That(presentation.Completed, Is.True);
        Assert.That(_reserve[0].transform.position, Is.EqualTo(_positionManager.GetPlayerDefaultPos(0)));
        Assert.DoesNotThrow(() => presentation.Dispose());
    }

    private static Sequence GetWaveTween(BattlePartyWavePresentation presentation)
    {
        return (Sequence)typeof(BattlePartyWavePresentation).GetField("_sequence",
            BindingFlags.Instance | BindingFlags.NonPublic).GetValue(presentation);
    }

    [Test]
    public void FindUniquePartySave_ReturnsOnlyUnambiguousCharacterId()
    {
        var saves = new List<CharacterSaveData>
        {
            new CharacterSaveData { CharacterDataID = "weasel" },
            new CharacterSaveData { CharacterDataID = "wolf" }
        };

        CharacterSaveData match = InvokeFindUniquePartySave(saves, " wolf ");

        Assert.That(match, Is.SameAs(saves[1]));
    }

    [Test]
    public void FindUniquePartySave_DuplicateCharacterIdReturnsNoMatch()
    {
        var saves = new List<CharacterSaveData>
        {
            new CharacterSaveData { CharacterDataID = "wolf" },
            new CharacterSaveData { CharacterDataID = "wolf" }
        };

        CharacterSaveData match = InvokeFindUniquePartySave(saves, "wolf");

        Assert.That(match, Is.Null);
    }

    [TestCase(BattleEncounterOutcome.Victory)]
    [TestCase(BattleEncounterOutcome.Escaped)]
    [TestCase(BattleEncounterOutcome.PartyDefeated)]
    public void BattleOutro_ClearsDeadFrontAndInactiveReserveBeforeEndingEvent(BattleEncounterOutcome outcome)
    {
        _front[0].IsStunned = true;
        _reserve[0].TryApplyStatusEffect(new PoisonEffect(3));
        _reserve[0].TryApplyStatusEffect(new BindEffect(3));
        bool eventObserved = false;
        _manager.OnBattleEnded += _ =>
        {
            eventObserved = true;
            Assert.That(_front[0].IsStunned, Is.False);
            Assert.That(_reserve[0].HasEffect(StatusEffectIds.Poison), Is.False);
            Assert.That(_reserve[0].IsBound, Is.False);
        };
        MethodInfo method = typeof(BattleManager).GetMethod(
            "BattleOutroRoutine", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        IEnumerator routine = (IEnumerator)method.Invoke(_manager, new object[] { outcome });

        Assert.That(routine.MoveNext(), Is.True);

        Assert.That(eventObserved, Is.True);
        Assert.That(_front[0].CurrentHP, Is.Zero);
        Assert.That(_reserve[0].CurrentHP, Is.EqualTo(100));
        Assert.That(_reserve[0].gameObject.activeSelf, Is.False);
        (routine as System.IDisposable)?.Dispose();
    }

    [Test]
    public void ManagerDestruction_ClearsBattleEffectsOnSurvivingSceneCharacters()
    {
        _reserve[0].TryApplyStatusEffect(new StunEffect(3));
        _reserve[0].TryApplyStatusEffect(new PoisonEffect(3));

        Object.DestroyImmediate(_manager.gameObject);

        Assert.That(_reserve[0].HasEffect(StatusEffectIds.Poison), Is.False);
        Assert.That(_reserve[0].IsStunned, Is.False);
        Assert.That(_reserve[0].CurrentHP, Is.EqualTo(100));
    }

    private PlayerCharacter CreatePlayer(string characterId, bool active)
    {
        GameObject playerObject = CreateObject(characterId);
        PlayerCharacter player = playerObject.AddComponent<PlayerCharacter>();
        CharacterData data = ScriptableObject.CreateInstance<CharacterData>();
        data.CharacterID = characterId;
        data.DisplayName = characterId;
        data.BaseStats = new StatBlock
        {
            MaxHP = 100,
            MaxAP = 50,
            ATK = 10,
            DEF = 5,
            SPD = 10
        };
        _ownedObjects.Add(data);
        player.SetCharacterData(data);
        playerObject.SetActive(active);
        return player;
    }

    private GameObject CreateObject(string name)
    {
        var gameObject = new GameObject(name);
        _ownedObjects.Add(gameObject);
        return gameObject;
    }

    private T CreateComponent<T>(string name) where T : Component
    {
        return CreateObject(name).AddComponent<T>();
    }

    private List<PlayerCharacter> GetPrivateList(string fieldName)
    {
        FieldInfo field = typeof(BattleManager).GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        return (List<PlayerCharacter>)field.GetValue(_manager);
    }

    private List<CharacterBase> GetPrivateTurnQueue()
    {
        FieldInfo field = typeof(BattleManager).GetField(
            "_turnQueue",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        return (List<CharacterBase>)field.GetValue(_manager);
    }

    private bool InvokePromoteReserveWave()
    {
        MethodInfo method = typeof(BattleManager).GetMethod(
            "TryPromoteReservePartyWave",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        return (bool)method.Invoke(_manager, null);
    }

    private static CharacterSaveData InvokeFindUniquePartySave(
        IReadOnlyList<CharacterSaveData> party,
        string characterId)
    {
        MethodInfo method = typeof(BattleManager).GetMethod(
            "FindUniquePartySave",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        return (CharacterSaveData)method.Invoke(null, new object[] { party, characterId });
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        field.SetValue(target, value);
    }
}
