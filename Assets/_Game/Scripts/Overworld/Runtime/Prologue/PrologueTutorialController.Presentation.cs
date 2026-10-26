using System;
using System.Collections;
using DG.Tweening;
using UnityEngine;

public sealed partial class PrologueTutorialController
{
    private Tween _roleTween;
    private Transform _movingRole;
    private Vector3 _roleBaseline;
    private int _autoSaveVersion;
    private bool _awaitingFinalSave;
    private SaveStorageResult _finalSaveResult;
    private int _finalSaveGeneration;
    private GameObject _rescueRole;
    private bool _rescueRoleWasActive;

    private IEnumerator Resume()
    {
        while ((_loader != null && _loader.IsLoading) || (_transitions != null && _transitions.IsTransitioning)) yield return null;
        if (GameLoadCoordinator.IsLoading) { while (GameLoadCoordinator.IsLoading) yield return null; }
        if (_needsRetryAfterDisable && _battleRetrySnapshot != null)
        {
            _needsRetryAfterDisable = false;
            yield return RetryPrompt();
            yield break;
        }
        _audioSession = PrologueTrainAudioSession.GetOrCreate(AudioManager.Instance, _definition.TrainAudio, gameObject.scene.name);
        if (Stage != PrologueTutorialStage.Completed) _audioSession?.StartRunning();

        PrologueTutorialRoom safeRoom = PrologueTutorialProgress.SafeRoom(ReadFlag);
        string spawnId = null;
        string checkpoint = PrologueTutorialProgress.LastCompletedFlag(ReadFlag);
        for (int i = 0; _definition.ResumePoints != null && i < _definition.ResumePoints.Count; i++)
        {
            PrologueTutorialResumePoint point = _definition.ResumePoints[i];
            if (point == null || !string.Equals(point.CompletedFlag ?? string.Empty, checkpoint, StringComparison.Ordinal)) continue;
            safeRoom = point.Room;
            spawnId = point.SpawnPointId;
            break;
        }
        yield return MoveTo(safeRoom, spawnId);
        if (!_stepSucceeded) yield break;
        if (PrologueTutorialProgress.ShouldPlayIntroduction(ReadFlag))
        {
            yield return PlayIntroduction();
            if (!_stepSucceeded) yield break;
        }
        switch (Stage)
        {
            case PrologueTutorialStage.Battle:
                yield return StartTutorialBattle();
                break;
            case PrologueTutorialStage.Rescue:
                yield return Rescue();
                break;
            case PrologueTutorialStage.Derail:
                yield return Derail();
                break;
            case PrologueTutorialStage.Completed:
                ReleaseCutscene();
                yield return Ending(false);
                break;
        }
    }

    private IEnumerator TravelAndContinue(PrologueTutorialRoom room, string spawnId)
    {
        yield return MoveTo(room, spawnId);
        if (!_stepSucceeded) yield break;
        if (room == PrologueTutorialRoom.Rear && (Stage == PrologueTutorialStage.InvestigateRear || Stage == PrologueTutorialStage.Battle))
            yield return RearAndBattle();
        else if (room == PrologueTutorialRoom.Rear && Stage == PrologueTutorialStage.Rescue)
            yield return Rescue();
        else if (room == PrologueTutorialRoom.Control && Stage == PrologueTutorialStage.Forward)
        {
            yield return PlayBeat("forward");
            if (_stepSucceeded) { SetFact(PrologueTutorialProgress.ForwardCompleted); QueueAutoSave(); }
        }
    }

    private IEnumerator Interact(PrologueTutorialInteraction kind, string dialogueId)
    {
        switch (kind)
        {
            case PrologueTutorialInteraction.InspectMachine:
                yield return CompleteInteraction("inspect_machine", PrologueTutorialProgress.InspectCompleted);
                break;
            case PrologueTutorialInteraction.AdjustValve:
                yield return CompleteInteraction("adjust_valve", PrologueTutorialProgress.ValveCompleted);
                break;
            case PrologueTutorialInteraction.TestMachine:
                yield return CompleteInteraction("test_machine", PrologueTutorialProgress.RepairCompleted);
                if (_stepSucceeded) QueueAutoSave();
                break;
            case PrologueTutorialInteraction.TalkToDragon:
                yield return PlayBeat("dragon");
                if (!_stepSucceeded) yield break;
                yield return WaitPresentation(_definition.IncidentQuietDuration);
                _audioSession?.PlayPowerDown();
                CameraController.Instance?.TryImpulse(new Vector3(1f, 0.4f, 0f), 0.65f,
                    Mathf.Max(0.1f, _definition.IncidentDuration), CameraShakeSafety.Cinematic, out _);
                yield return PlayBeat("incident");
                if (_stepSucceeded) { SetFact(PrologueTutorialProgress.IncidentCompleted); QueueAutoSave(); }
                break;
            case PrologueTutorialInteraction.EnterRear:
                if (_roomContainer.CurrentDefinition != _definition.GetRoom(PrologueTutorialRoom.Rear))
                {
                    yield return MoveTo(PrologueTutorialRoom.Rear, null);
                    if (!_stepSucceeded) yield break;
                }
                yield return RearAndBattle();
                break;
            case PrologueTutorialInteraction.SealDoor:
                yield return Rescue();
                if (!_stepSucceeded) yield break;
                yield return CompleteInteraction("seal", PrologueTutorialProgress.SealCompleted);
                if (!_stepSucceeded) yield break;
                // 봉쇄한 현재 객차를 유지하고 다음 오른쪽 문으로 이동합니다.
                QueueAutoSave();
                break;
            case PrologueTutorialInteraction.UseEmergencyControl:
                if (Stage == PrologueTutorialStage.EmergencyControl)
                {
                    yield return CompleteInteraction("control", PrologueTutorialProgress.ControlCompleted);
                    if (!_stepSucceeded) yield break;
                    QueueAutoSave();
                }
                yield return Derail();
                break;
            case PrologueTutorialInteraction.OptionalDialogue:
                yield return ShowDialogue(dialogueId);
                break;
        }
    }

    private IEnumerator CompleteInteraction(string dialogueId, string flag)
    {
        yield return PlayBeat(dialogueId);
        if (_stepSucceeded) SetFact(flag);
    }

    private IEnumerator RearAndBattle()
    {
        if (ReadFlag(PrologueTutorialProgress.RearCompleted) == 0)
        {
            AcquireCutscene();
            if (_definition.RearRevealStory == null) { Fail("prologue.rear.story.missing"); yield break; }
            if (_storyPlayer == null)
            {
                var root = new GameObject("Prologue Illustrated Presentation");
                root.transform.SetParent(transform, false);
                _storyPlayer = root.AddComponent<IllustratedStoryPlayer>();
            }
            _storyPlayer.PageStarted += OnRearPage;
            try { yield return _storyPlayer.Play(_definition.RearRevealStory); }
            finally { if (_storyPlayer != null) _storyPlayer.PageStarted -= OnRearPage; _audioSession?.EndRearWalk(); }
            if (_storyPlayer == null || !_storyPlayer.Succeeded) { _stepSucceeded = false; yield break; }
            yield return CompleteInteraction("rear", PrologueTutorialProgress.RearCompleted);
            if (!_stepSucceeded) yield break;
        }
        yield return StartTutorialBattle();
    }

    private void OnRearPage(int index)
    {
        if (_definition.RearRevealStory == null || _audioSession == null) return;
        if (index == 0 && _definition.RearRevealStory.Pages.Count > 0)
        {
            IllustratedStoryPage page = _definition.RearRevealStory.Pages[0];
            _audioSession.BeginRearWalk(Mathf.Max(page.MinimumDuration, page.AnimationDuration));
        }
        else _audioSession.EndRearWalk();
    }

    private IEnumerator Rescue()
    {
        _stepSucceeded = true;
        if (ReadFlag(PrologueTutorialProgress.RescueCompleted) != 0)
        {
            PrologueTutorialRoleAnchor existing = FindRole("companion_dragon");
            if (existing != null) existing.gameObject.SetActive(true);
            yield break;
        }
        AcquireCutscene();
        if (!isActiveAndEnabled) yield break;
        yield return PlayBeat("pressure");
        if (!_stepSucceeded) yield break;
        PrologueTutorialRoleAnchor anchor = FindRole("companion_dragon");
        if (anchor == null) { Fail("prologue.rescue.role.missing"); yield break; }
        _rescueRole = anchor.gameObject;
        _rescueRoleWasActive = _rescueRole.activeSelf;
        _rescueRole.SetActive(true);
        _movingRole = anchor.transform;
        _roleBaseline = _movingRole.position;
        Vector2 authoredOffset = _definition.RescueEntranceOffset;
        if (!Finite(authoredOffset.x) || !Finite(authoredOffset.y)) { Fail("prologue.rescue.offset.invalid"); yield break; }
        float duration = _definition.RescueEntranceDuration;
        if (!Finite(duration)) { Fail("prologue.rescue.duration.invalid"); yield break; }
        duration = Mathf.Max(0f, duration);
        _movingRole.position = _roleBaseline + new Vector3(authoredOffset.x, authoredOffset.y, 0f);
        _roleTween = _movingRole.DOMove(_roleBaseline, duration).SetEase(Ease.OutCubic)
            .SetUpdate(true).SetAutoKill(false).SetRecyclable(false).Pause();
        float elapsed = 0f;
        while (_roleTween != null && _roleTween.IsActive() && elapsed < duration)
        {
            if (!PresentationPaused) { elapsed = Mathf.Min(duration, elapsed + Time.unscaledDeltaTime); _roleTween.Goto(elapsed, false); }
            yield return null;
        }
        ReleaseRoleMotion();
        yield return PlayBeat("rescue");
        if (!_stepSucceeded) yield break;
        RestoreProtagonistAfterRescue();
        SetFact(PrologueTutorialProgress.RescueCompleted);
        _rescueRole = null;
        _audioSession?.StartRunning();
        QueueAutoSave();
    }

    private PrologueTutorialRoleAnchor FindRole(string roleId)
    {
        if (_roomContainer == null || _roomContainer.CurrentRoom == null) return null;
        PrologueTutorialRoleAnchor result = null;
        PrologueTutorialRoleAnchor[] roles = _roomContainer.CurrentRoom.GetComponentsInChildren<PrologueTutorialRoleAnchor>(true);
        for (int i = 0; i < roles.Length; i++)
        {
            if (!string.Equals(roles[i].RoleId, roleId, StringComparison.Ordinal)) continue;
            if (result != null) return null;
            result = roles[i];
        }
        return result;
    }

    private void ReleaseRoleMotion()
    {
        _roleTween?.Kill(false);
        _roleTween = null;
        if (_movingRole != null) _movingRole.position = _roleBaseline;
        _movingRole = null;
    }

    private void RestoreUnfinishedRescueRole()
    {
        if (_rescueRole != null && ReadFlag(PrologueTutorialProgress.RescueCompleted) == 0)
            _rescueRole.SetActive(_rescueRoleWasActive);
        _rescueRole = null;
    }

    private IEnumerator Derail()
    {
        yield return PlayBeat("derail");
        if (!_stepSucceeded) yield break;
        _audioSession?.Stop();
        yield return MoveTo(PrologueTutorialRoom.Completed, null);
        if (!_stepSucceeded) yield break;
        SetFact(PrologueTutorialProgress.Completed);
        ReleaseCutscene();
        yield return Ending(true);
    }

    private IEnumerator MoveTo(PrologueTutorialRoom room, string spawnId)
    {
        _stepSucceeded = false;
        if (_transitions == null || _player == null) { Fail("prologue.transition.missing"); yield break; }
        PrologueTutorialRoomBinding binding = _definition.GetRoomBinding(room);
        if (binding == null || binding.Definition == null) { Fail("prologue.destination.missing"); yield break; }
        bool done = false;
        SceneLoadResult result = SceneLoadResult.None;
        int generation = _generation;
        var request = new MapTransitionRequest
        {
            TransitionType = MapTransitionType.Room, TargetRoom = binding.Definition,
            TargetSpawnPointId = string.IsNullOrWhiteSpace(spawnId) ? binding.SpawnPointId : spawnId,
            FacingAfterEnter = FacingDirection.Keep, FadeDuration = _definition.RoomFadeDuration,
            UseFallbackPosition = false
        };
        _ownedTransition = request;
        if (!_transitions.TryRequestTransition(request, _player, value =>
            { if (!IsCurrent(generation)) return; result = value; done = true; if (ReferenceEquals(_ownedTransition, request)) _ownedTransition = null; }))
        { _ownedTransition = null; Fail("prologue.transition.rejected"); yield break; }
        while (!done) yield return null;
        _stepSucceeded = result == SceneLoadResult.Succeeded;
        if (!_stepSucceeded) Fail("prologue.transition.failed: " + result);
    }

    private IEnumerator PlayBeat(string id)
    {
        AcquireCutscene();
        if (!isActiveAndEnabled) { _stepSucceeded = false; yield break; }
        ActionSequenceAsset sequence = _definition.GetSequence(id);
        if (sequence == null) { yield return ShowDialogue(id); yield break; }
        if (_sequencePlayer == null) _sequencePlayer = gameObject.AddComponent<SceneActionSequencePlayer>();
        _sequencePlayer.Configure(sequence, null, string.Empty, _dialogues);
        bool finished = false;
        ActionExecutionResult result = null;
        int generation = _generation;
        _stepSucceeded = false;
        if (!_sequencePlayer.TryPlay(value => { if (IsCurrent(generation)) { result = value; finished = true; } }))
        { Fail("prologue.sequence.rejected: " + id); yield break; }
        while (!finished && _sequencePlayer != null && _sequencePlayer.IsPlaying) yield return null;
        _stepSucceeded = finished && result != null && result.Status == ActionExecutionStatus.Succeeded;
        if (!_stepSucceeded) Fail("prologue.sequence.incomplete: " + id);
    }

    private IEnumerator ShowDialogue(string id)
    {
        AcquireCutscene();
        _stepSucceeded = false;
        if (!isActiveAndEnabled) yield break;
        DialogueData dialogue = _definition.GetDialogue(id);
        _dialogueOwner = DialogueManager.Instance;
        if (dialogue == null || _dialogueOwner == null) { Fail("prologue.dialogue.missing: " + id); yield break; }
        bool finished = false;
        bool succeeded = false;
        int generation = _generation;
        if (!_dialogueOwner.TryStartDialogue(dialogue,
            () => { if (IsCurrent(generation)) { succeeded = true; finished = true; } },
            () => { if (IsCurrent(generation)) finished = true; }, null, out _dialogueGeneration))
        { Fail("prologue.dialogue.rejected: " + id); yield break; }
        while (!finished && _dialogueOwner != null && _dialogueOwner.PlaybackGeneration == _dialogueGeneration) yield return null;
        while (_dialogueOwner != null && _dialogueOwner.PlaybackGeneration == _dialogueGeneration && _dialogueOwner.IsPresentationVisible) yield return null;
        _stepSucceeded = finished && succeeded && _dialogueOwner != null
            && _dialogueOwner.PlaybackGeneration == _dialogueGeneration && !_dialogueOwner.IsPresentationVisible;
        _dialogueOwner = null;
        _dialogueGeneration = 0;
    }

    private static bool PresentationPaused => GameInput.IsConfigModalActive
        || (GameStateManager.Instance != null && GameStateManager.Instance.CurrentState == GameState.Paused);

    private IEnumerator WaitPresentation(float duration)
    {
        float elapsed = 0f;
        while (elapsed < Mathf.Max(0f, duration))
        { if (!PresentationPaused) elapsed += Time.unscaledDeltaTime; yield return null; }
    }

    private void QueueAutoSave()
    {
        if (isActiveAndEnabled && _initialized && ReferenceEquals(_global, GlobalDataManager.Instance)
            && SaveManager.RequestAutoSave()) _autoSaveVersion = SaveManager.AutoSaveRequestVersion;
    }

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    private void UnbindAutoSave()
    {
        SaveManager.AutoSaveCompletedWithVersion -= OnFinalSaved;
        _awaitingFinalSave = false;
    }

    private void OnFinalSaved(int version, SaveStorageResult result)
    {
        if (!_awaitingFinalSave || version != _autoSaveVersion || !IsCurrent(_finalSaveGeneration)
            || !ReferenceEquals(_global, GlobalDataManager.Instance)
            || _roomContainer.CurrentDefinition != _definition.GetRoom(PrologueTutorialRoom.Completed)
            || ReadFlag(PrologueTutorialProgress.Completed) == 0) return;
        _finalSaveResult = result;
        _awaitingFinalSave = false;
    }

    private IEnumerator Ending(bool requestSave)
    {
        ReleaseCutscene();
        bool saveSucceeded = !requestSave;
        while (true)
        {
            if (requestSave)
            {
                _finalSaveResult = null;
                _finalSaveGeneration = _generation;
                _awaitingFinalSave = true;
                SaveManager.AutoSaveCompletedWithVersion += OnFinalSaved;
                if (!SaveManager.RequestAutoSave())
                {
                    _finalSaveResult = SaveStorageResult.Failed(SaveStorageFailure.NotAllowed, "prologue.autosave.rejected");
                    _awaitingFinalSave = false;
                }
                else _autoSaveVersion = SaveManager.AutoSaveRequestVersion;
                while (_awaitingFinalSave)
                {
                    if (!SaveManager.HasPendingAutoSave || SaveManager.AutoSaveRequestVersion != _autoSaveVersion)
                    {
                        _finalSaveResult = SaveStorageResult.Failed(SaveStorageFailure.NotAllowed, "prologue.autosave.interrupted");
                        _awaitingFinalSave = false;
                        break;
                    }
                    yield return null;
                }
                UnbindAutoSave();
                saveSucceeded = _finalSaveResult != null && _finalSaveResult.Success;
            }
            int selection = -1;
            bool closed = false;
            _dialogueOwner = DialogueManager.Instance;
            if (_dialogueOwner == null) { Fail("prologue.ending.dialogue.missing"); yield break; }
            string promptKey = "HTH_PROLOGUE_" + (saveSucceeded ? "ENDING" : "SAVE_FAILED");
            string[] keys = saveSucceeded ? new[] { "HTH_PROLOGUE_RETURN_TITLE" }
                : new[] { "HTH_PROLOGUE_SAVE_RETRY", "HTH_PROLOGUE_RETURN_WITHOUT_SAVE" };
            if (!_dialogueOwner.TryStartLocalizedChoicePrompt(promptKey, promptKey, keys, keys,
                selected => { selection = selected; closed = true; }, () => closed = true))
            { Fail("prologue.ending.prompt.rejected"); yield break; }
            _dialogueGeneration = _dialogueOwner.PlaybackGeneration;
            while (!closed && _dialogueOwner != null && _dialogueOwner.PlaybackGeneration == _dialogueGeneration) yield return null;
            while (_dialogueOwner != null && _dialogueOwner.PlaybackGeneration == _dialogueGeneration && _dialogueOwner.IsPresentationVisible) yield return null;
            _dialogueOwner = null;
            _dialogueGeneration = 0;
            if (!saveSucceeded && selection == 0) { requestSave = true; continue; }
            if (selection >= 0)
            {
                GameInput.ConsumeBattleUIInput();
                if (_loader == null) { Fail("prologue.title.loader.missing"); yield break; }
                _loader.LoadSceneWithResult(_definition.TitleSceneName, _definition.RoomFadeDuration, null);
                yield break;
            }
            // 취소는 완료 공간을 탈출하거나 저장 성공으로 위장하지 않습니다.
            requestSave = false;
            yield return null;
        }
    }

    private static string L(string suffix) => LocalizationManager.Localize("HTH_PROLOGUE_" + suffix, "HTH_PROLOGUE_" + suffix);
}
