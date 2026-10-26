using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed partial class PrologueTutorialController : IEncounterSource, IEncounterOutcomeSource,
    IEncounterDefeatPolicy, IEncounterAbortSource, IEncounterRetrySource,
    IEncounterRosterPolicy, IEncounterStoryWithdrawalPolicy, IEncounterEntryPresentationPolicy
{
    private readonly List<string> _battleRoster = new List<string>(1);
    private BattleManager _battle;
    private QTEManager _qte;
    private PlayerCharacter _tutorialActor;
    private SaveData _battleRetrySnapshot;
    private bool _battleRunning;
    private bool _enemyDefenseWindow;
    private bool _withdrawalRequested;
    private PrologueTutorialPractice _practice;
    private BattleEncounterOutcome _pendingOutcome;
    private bool _hasPendingOutcome;

    public IReadOnlyList<string> BattleRosterCharacterIds => _battleRoster;
    public bool AllowStoryWithdrawal => true;
    public bool WithdrawOnEarlyVictory => true;
    public bool ReturnToExplorationOnDefeat => true;
    public bool PlayBattleEntryPresentation => true;
    public string RetryEncounterId => "prologue.tutorial.white_entity";

    private IEnumerator StartTutorialBattle()
    {
        _stepSucceeded = false;
        if (!TryPrepareBattle(out string error)) { Fail(error); yield break; }
        ReleaseCutscene();
        if (!isActiveAndEnabled || !_initialized) yield break;
        _practice = new PrologueTutorialPractice(_definition.MaxDefenseAttempts, _definition.MaxItemPracticeActions);
        _withdrawalRequested = false;
        _enemyDefenseWindow = false;
        _battleRunning = true;
        BindBattle();
        bool started = BattleEncounterService.StartEncounter(_player, new List<EnemyData> { _definition.Enemy },
            encounterId: RetryEncounterId, defeatsOnVictory: false, encounterSource: this,
            battleScenarioData: _definition.BattleScenario, allowEscape: false, playEntryPresentation: true);
        if (!started)
        {
            UnbindBattle();
            _battleRunning = false;
            _battleRetrySnapshot = null;
            Fail("prologue.battle.start.rejected");
            yield break;
        }
        _stepSucceeded = true;
    }

    private bool TryPrepareBattle(out string error)
    {
        error = string.Empty;
        if (_definition.Protagonist == null || _definition.Enemy == null || _definition.HealingItem == null
            || _player == null || _global == null || _definition.BattleScenario == null)
        { error = "prologue.battle.references.missing"; return false; }
        _battle = BattleManager.Instance;
        _qte = QTEManager.Instance;
        if (_battle == null || _qte == null || _battle.gameObject.scene != gameObject.scene)
        { error = "prologue.battle.host.missing"; return false; }

        string protagonistId = _definition.Protagonist.CharacterID;
        CharacterSaveData member = FindProtagonistSave(out bool ambiguous);
        if (ambiguous || member == null || string.IsNullOrWhiteSpace(member.CharacterDataID)
            || string.IsNullOrWhiteSpace(protagonistId))
        { error = "prologue.battle.protagonist.not_unique"; return false; }
        PlayerCharacter fieldCharacter = _player.GetComponent<PlayerCharacter>();
        if (fieldCharacter == null || !string.Equals(fieldCharacter.CharacterID, member.CharacterDataID, StringComparison.Ordinal))
        { error = "prologue.battle.field_protagonist.mismatch"; return false; }

        ItemData item = _definition.HealingItem;
        if (item.Type != ItemType.Consumable || !item.UsableInBattle || item.UsableInOverworld || item.IsSellable
            || string.IsNullOrWhiteSpace(item.ItemID) || ItemDatabase.FindById(item.ItemID) != item)
        { error = "prologue.battle.healing_item.not_registered"; return false; }
        if (_global.GetItemCount(item.ItemID) < 1)
        {
            if (ReadFlag(PrologueTutorialProgress.SuppliesReady) != 0)
            { error = "prologue.battle.supplies.already_used"; return false; }
            if (_global.AddItemAndGetAddedAmount(item.ItemID, 1) != 1)
            { error = "prologue.battle.supplies.full"; return false; }
        }
        SetFact(PrologueTutorialProgress.SuppliesReady);
        _battleRoster.Clear();
        _battleRoster.Add(member.CharacterDataID);
        // 지급/확보 사실을 먼저 확정한 snapshot입니다. 재시도에서 별도 재지급하지 않습니다.
        _battleRetrySnapshot = _global.ToSaveData();
        _battleRetrySnapshot.currentScene = gameObject.scene.name;
        _battleRetrySnapshot.currentRoomId = _definition.GetRoom(PrologueTutorialRoom.Rear).RoomId;
        _battleRetrySnapshot.spawnPointId = _definition.GetRoomBinding(PrologueTutorialRoom.Rear).SpawnPointId;
        _battleRetrySnapshot.playerX = _player.transform.position.x;
        _battleRetrySnapshot.playerY = _player.transform.position.y;
        _battleRetrySnapshot.lookingDirection = _player.FacingDirection;
        return true;
    }

    private CharacterSaveData FindProtagonistSave(out bool ambiguous)
    {
        ambiguous = false;
        if (_global == null || _definition.Protagonist == null) return null;
        CharacterSaveData found = null;
        for (int i = 0; i < _global.Party.Count; i++)
        {
            CharacterSaveData candidate = _global.Party[i];
            if (candidate == null) continue;
            string dataId = string.IsNullOrWhiteSpace(candidate.CharacterDataID) ? candidate.CharacterID : candidate.CharacterDataID;
            if (!string.Equals(dataId, _definition.Protagonist.CharacterID, StringComparison.Ordinal)) continue;
            if (found != null) { ambiguous = true; return null; }
            found = candidate;
        }
        return found;
    }

    private void BindBattle()
    {
        _battle.OnBattleStarted += OnTutorialBattleStarted;
        _battle.OnPlayerTurnStarted += OnTutorialPlayerTurn;
        _battle.OnEnemyActionStarted += OnTutorialEnemyAction;
        _battle.OnPlayerActionCompleted += OnTutorialPlayerAction;
        _qte.BattleDefenseResolved += OnTutorialDefense;
    }

    private void UnbindBattle()
    {
        if (_battle != null)
        {
            _battle.OnBattleStarted -= OnTutorialBattleStarted;
            _battle.OnPlayerTurnStarted -= OnTutorialPlayerTurn;
            _battle.OnEnemyActionStarted -= OnTutorialEnemyAction;
            _battle.OnPlayerActionCompleted -= OnTutorialPlayerAction;
        }
        if (_qte != null) _qte.BattleDefenseResolved -= OnTutorialDefense;
        _tutorialActor = null;
        _battleRunning = false;
        _enemyDefenseWindow = false;
    }

    private bool OwnsBattle => _battleRunning && _battle != null && ReferenceEquals(_battle, BattleManager.Instance)
        && _battleRoster.Count == 1 && ReferenceEquals(_global, GlobalDataManager.Instance);

    private void OnTutorialBattleStarted(List<PlayerCharacter> party, List<EnemyCharacter> enemies)
    {
        if (!OwnsBattle || party == null || party.Count != 1 || party[0] == null
            || !string.Equals(party[0].CharacterID, _battleRoster[0], StringComparison.Ordinal)) return;
        _tutorialActor = party[0];
        ShowLessonHint();
    }

    private void OnTutorialPlayerTurn(PlayerCharacter actor)
    {
        if (!OwnsBattle || actor == null || actor != _tutorialActor) return;
        _enemyDefenseWindow = false;
        ShowLessonHint();
    }

    private void OnTutorialEnemyAction(EnemyCharacter actor, EnemyAttackType type)
    {
        if (!OwnsBattle || actor == null || _tutorialActor == null) return;
        _enemyDefenseWindow = true;
        ShowLessonHint();
    }

    private void OnTutorialPlayerAction(BattlePlayerActionResult result)
    {
        if (!OwnsBattle || result.Actor == null || result.Actor != _tutorialActor) return;
        bool validItem = result.Action == PlayerMenuAction.Item && result.Item != null
            && result.Item.Type == ItemType.Consumable;
        _practice.RecordPlayerAction(result.Action == PlayerMenuAction.Attack, validItem);
        if (_practice.Stage == PrologueTutorialLessonStage.Complete) RequestWithdrawal();
        else ShowLessonHint();
    }

    private void OnTutorialDefense(DefenseQteResult result)
    {
        // 이 조우의 roster는 위젤 하나뿐입니다. 공격 QTE/다른 전투 결과는 듣지 않습니다.
        if (!OwnsBattle || !_enemyDefenseWindow || _tutorialActor == null
            || _practice == null) return;
        _practice.RecordDefense(result.IsGuard, result.IsJustGuard);
        ShowLessonHint();
    }

    private void ShowLessonHint()
    {
        if (!OwnsBattle) return;
        string key;
        if (_practice == null) return;
        switch (_practice.Stage)
        {
            case PrologueTutorialLessonStage.Attack: key = "TUTORIAL_ATTACK"; break;
            case PrologueTutorialLessonStage.Guard: key = "TUTORIAL_GUARD"; break;
            case PrologueTutorialLessonStage.JustGuard: key = "TUTORIAL_JUST_GUARD"; break;
            case PrologueTutorialLessonStage.Item: key = "TUTORIAL_ITEM"; break;
            default: return;
        }
        _battle.RequestNarration(new BattleNarrationMessage(L(key), BattleNarrationStyle.System,
            BattleNarrationPriority.Critical, 4f, false));
    }

    private void RequestWithdrawal()
    {
        if (_withdrawalRequested || !OwnsBattle) return;
        _withdrawalRequested = _battle.TryRequestStoryWithdrawal(this);
        if (!_withdrawalRequested) Fail("prologue.battle.withdrawal.rejected");
    }

    public void OnEncounterResolved(bool victory, PlayerController player) { }

    public void OnEncounterResolved(BattleEncounterOutcome outcome, PlayerController player)
    {
        if (!_battleRunning || player != _player) return;
        UnbindBattle();
        if (outcome == BattleEncounterOutcome.StoryWithdrawn)
        {
            _battleRetrySnapshot = null;
            SetFact(PrologueTutorialProgress.WithdrawalCompleted);
            _pendingOutcome = outcome;
            _hasPendingOutcome = true;
        }
        else if (outcome == BattleEncounterOutcome.PartyDefeated)
        {
            if (SaveManager.HasPendingAutoSave && SaveManager.AutoSaveRequestVersion == _autoSaveVersion)
                SaveManager.CancelPendingAutoSave();
            _pendingOutcome = outcome;
            _hasPendingOutcome = true;
        }
        else Fail("prologue.battle.unexpected_outcome: " + outcome);
    }

    public void OnEncounterAborted(PlayerController player)
    {
        if (!_battleRunning) return;
        UnbindBattle();
        if (SaveManager.HasPendingAutoSave && SaveManager.AutoSaveRequestVersion == _autoSaveVersion)
            SaveManager.CancelPendingAutoSave();
        // 강제 취소는 학습/후퇴/구조 성공이 아닙니다. 진입 전 상태만 복구할 수 있습니다.
        if (isActiveAndEnabled && _battleRetrySnapshot != null && player == _player)
        { _pendingOutcome = BattleEncounterOutcome.PartyDefeated; _hasPendingOutcome = true; }
    }

    private void Update()
    {
        // 결과 콜백 뒤 기존 BattleManager의 세션/표시 cleanup이 끝난 안전 프레임에만 이어갑니다.
        if (!_hasPendingOutcome || !_initialized || _busy || _battle == null || _battle.IsBattleSessionActive
            || _stateOwner == null || _stateOwner.CurrentState != GameState.Exploration
            || (DialogueManager.Instance != null && DialogueManager.Instance.IsPresentationVisible)) return;
        _hasPendingOutcome = false;
        StartFlow(_pendingOutcome == BattleEncounterOutcome.StoryWithdrawn ? Rescue() : RetryPrompt());
    }

    private IEnumerator RetryPrompt()
    {
        if (_battleRetrySnapshot == null) { Fail("prologue.retry.snapshot.missing"); yield break; }
        while (true)
        {
        bool closed = false;
        int selected = -1;
        _dialogueOwner = DialogueManager.Instance;
        string[] keys = { "HTH_PROLOGUE_RETRY", "HTH_PROLOGUE_RETURN_TITLE" };
        if (_dialogueOwner == null || !_dialogueOwner.TryStartLocalizedChoicePrompt("HTH_PROLOGUE_RETRY_PROMPT", "HTH_PROLOGUE_RETRY_PROMPT",
            keys, keys, choice => { selected = choice; closed = true; }, () => closed = true))
        { Fail("prologue.retry.prompt.rejected"); yield break; }
        _dialogueGeneration = _dialogueOwner.PlaybackGeneration;
        while (!closed && _dialogueOwner != null && _dialogueOwner.PlaybackGeneration == _dialogueGeneration) yield return null;
        while (_dialogueOwner != null && _dialogueOwner.PlaybackGeneration == _dialogueGeneration && _dialogueOwner.IsPresentationVisible) yield return null;
        _dialogueOwner = null;
        _dialogueGeneration = 0;
        if (selected < 0) { yield return null; continue; }
        if (selected == 0)
        {
            GameInput.ConsumeBattleUIInput();
            GameLoadStartResult result = GameLoadCoordinator.LoadSnapshot(_battleRetrySnapshot, null);
            if (!result.Accepted) Fail("prologue.retry.load.rejected: " + result.Failure);
        }
        else if (_loader != null)
            _loader.LoadSceneWithResult(_definition.TitleSceneName, _definition.RoomFadeDuration, null);
        yield break;
        }
    }

    private void RestoreProtagonistAfterRescue()
    {
        CharacterSaveData member = FindProtagonistSave(out bool ambiguous);
        if (member == null || ambiguous || _player == null) return;
        PlayerCharacter character = _player.GetComponent<PlayerCharacter>();
        if (character == null) return;
        member.HP = character.MaxHP;
        member.AP = character.MaxAP;
        character.SynchronizePersistentVitals(member);
    }
}
