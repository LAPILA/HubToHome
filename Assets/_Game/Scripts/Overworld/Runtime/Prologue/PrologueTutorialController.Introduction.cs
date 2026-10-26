using System.Collections;
using DG.Tweening;
using UnityEngine;

public sealed partial class PrologueTutorialController
{
    private static readonly int IntroMoveX = Animator.StringToHash("MoveX");
    private static readonly int IntroMoveY = Animator.StringToHash("MoveY");
    private static readonly int IntroIsMoving = Animator.StringToHash("IsMoving");
    private Tween _introPlayerTween;
    private Tween _introPartnerTween;
    private PlayerController _introPlayer;
    private uint _introPlayerVersion;
    private Transform _introPartner;
    private Animator _introPartnerAnimator;
    private Vector3 _introPlayerBaseline;
    private Vector3 _introPartnerBaseline;
    private int _introPlayerFacing;
    private bool _introPartnerWasActive;
    private bool _introHasMoveX;
    private bool _introHasMoveY;
    private bool _introHasMoving;
    private float _introMoveXBaseline;
    private float _introMoveYBaseline;
    private bool _introMovingBaseline;
    private RoomInstance _introRoom;
    private int _introRunGeneration = -1;
    private int _introCancellationGeneration = -1;

    private IEnumerator PlayIntroduction()
    {
        _stepSucceeded = false;
        PrologueTutorialIntroduction authored = _definition.Introduction;
        if (authored == null || !Finite(authored.PlayerMeetingPosition.x) || !Finite(authored.PlayerMeetingPosition.y)
            || !Finite(authored.PartnerMeetingPosition.x) || !Finite(authored.PartnerMeetingPosition.y)
            || !Finite(authored.EntranceDuration) || !Finite(authored.ReturnDuration) || !Finite(authored.SettleDuration))
        { Fail("prologue.introduction.settings.invalid"); yield break; }
        PrologueTutorialRoleAnchor anchor = FindRole(authored.RoleId);
        if (anchor == null || _roomContainer.CurrentRoom == null || _definition.GetDialogue(authored.DialogueId) == null)
        { Fail("prologue.introduction.references.missing"); yield break; }

        AcquireCutscene();
        if (!isActiveAndEnabled || !_player.TryBeginOverworldPresentation(this, out _introPlayerVersion))
        { Fail("prologue.introduction.player.busy"); yield break; }
        _introPlayer = _player;
        _introRoom = _roomContainer.CurrentRoom;
        _introRunGeneration = _generation;
        _introCancellationGeneration = -1;
        _introPlayerBaseline = _player.transform.position;
        _introPlayerFacing = _player.FacingDirection;
        _introPartner = anchor.transform;
        _introPartnerBaseline = _introPartner.position;
        _introPartnerWasActive = anchor.gameObject.activeSelf;
        anchor.gameObject.SetActive(true);
        CaptureIntroductionAnimator(anchor.GetComponentInChildren<Animator>(true));
        bool completed = false;
        int generation = _generation;
        try
        {
            Transform room = _roomContainer.CurrentRoom.transform;
            Vector3 playerLocal = room.InverseTransformPoint(_introPlayerBaseline);
            Vector3 partnerLocal = room.InverseTransformPoint(_introPartnerBaseline);
            Vector3 playerTarget = room.TransformPoint(new Vector3(authored.PlayerMeetingPosition.x, authored.PlayerMeetingPosition.y, playerLocal.z));
            Vector3 partnerTarget = room.TransformPoint(new Vector3(authored.PartnerMeetingPosition.x, authored.PartnerMeetingPosition.y, partnerLocal.z));
            yield return MoveIntroductionPair(playerTarget, partnerTarget, authored.EntranceDuration, generation);
            if (!_stepSucceeded) yield break;
            FaceIntroductionPair();
            yield return WaitPresentation(authored.SettleDuration);
            if (!OwnsIntroduction(generation)) yield break;
            yield return PlayBeat(authored.DialogueId);
            if (!_stepSucceeded || !OwnsIntroduction(generation)) yield break;
            if (authored.ReturnToStart)
            {
                yield return MoveIntroductionPair(_introPlayerBaseline, _introPartnerBaseline, authored.ReturnDuration, generation);
                if (!_stepSucceeded) yield break;
            }
            SetFact(PrologueTutorialProgress.IntroductionCompleted);
            completed = true;
            _stepSucceeded = true;
            QueueAutoSave();
        }
        finally { ReleaseIntroduction(completed, generation); }
    }

    private bool OwnsIntroduction(int generation) => IsCurrent(generation) && generation == _introRunGeneration
        && _introRoom != null && _roomContainer != null && ReferenceEquals(_introRoom, _roomContainer.CurrentRoom)
        && _introPartner != null
        && _introPlayer != null && _introPlayer.OwnsOverworldPresentation(this, _introPlayerVersion)
        && (BattleManager.Instance == null || !BattleManager.Instance.IsBattleSessionActive);

    private IEnumerator MoveIntroductionPair(Vector3 playerTarget, Vector3 partnerTarget, float authoredDuration, int generation)
    {
        _stepSucceeded = false;
        if (!OwnsIntroduction(generation)) yield break;
        float duration = Mathf.Max(0f, authoredDuration);
        Vector2 playerDirection = playerTarget - _introPlayer.transform.position;
        Vector2 partnerDirection = partnerTarget - _introPartner.position;
        bool playerMoving = playerDirection.sqrMagnitude > .000001f;
        bool partnerMoving = partnerDirection.sqrMagnitude > .000001f;
        _introPlayer.TrySetOverworldPresentationMotion(this, _introPlayerVersion, playerDirection, playerMoving);
        SetIntroductionPartnerMotion(partnerDirection, partnerMoving);
        _introPlayerTween = _introPlayer.transform.DOMove(playerTarget, duration).SetEase(Ease.InOutSine)
            .SetUpdate(true).SetRecyclable(false).SetAutoKill(false).Pause();
        _introPartnerTween = _introPartner.DOMove(partnerTarget, duration).SetEase(Ease.InOutSine)
            .SetUpdate(true).SetRecyclable(false).SetAutoKill(false).Pause();
        float elapsed = 0f;
        while (OwnsIntroduction(generation) && elapsed < duration)
        {
            if (!PresentationPaused)
            {
                elapsed = Mathf.Min(duration, elapsed + Time.unscaledDeltaTime);
                _introPlayerTween.Goto(elapsed, false);
                _introPartnerTween.Goto(elapsed, false);
            }
            yield return null;
        }
        if (!OwnsIntroduction(generation)) yield break;
        _introPlayerTween.Goto(duration, false);
        _introPartnerTween.Goto(duration, false);
        KillIntroductionTweens();
        _introPlayer.TrySetOverworldPresentationMotion(this, _introPlayerVersion, playerDirection, false);
        SetIntroductionPartnerMotion(partnerDirection, false);
        _stepSucceeded = true;
    }

    private void FaceIntroductionPair()
    {
        if (_introPlayer == null || _introPartner == null) return;
        Vector2 direction = _introPartner.position - _introPlayer.transform.position;
        _introPlayer.TrySetOverworldPresentationMotion(this, _introPlayerVersion, direction, false);
        SetIntroductionPartnerMotion(-direction, false);
    }

    private void CaptureIntroductionAnimator(Animator animator)
    {
        _introPartnerAnimator = animator;
        _introHasMoveX = _introHasMoveY = _introHasMoving = false;
        if (animator == null) return;
        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];
            if (parameter.nameHash == IntroMoveX && parameter.type == AnimatorControllerParameterType.Float) _introHasMoveX = true;
            if (parameter.nameHash == IntroMoveY && parameter.type == AnimatorControllerParameterType.Float) _introHasMoveY = true;
            if (parameter.nameHash == IntroIsMoving && parameter.type == AnimatorControllerParameterType.Bool) _introHasMoving = true;
        }
        if (_introHasMoveX) _introMoveXBaseline = animator.GetFloat(IntroMoveX);
        if (_introHasMoveY) _introMoveYBaseline = animator.GetFloat(IntroMoveY);
        if (_introHasMoving) _introMovingBaseline = animator.GetBool(IntroIsMoving);
    }

    private void SetIntroductionPartnerMotion(Vector2 direction, bool moving)
    {
        if (_introPartnerAnimator == null) return;
        if (direction.sqrMagnitude > .000001f)
        {
            Vector2 cardinal = Mathf.Abs(direction.x) >= Mathf.Abs(direction.y)
                ? new Vector2(Mathf.Sign(direction.x), 0f) : new Vector2(0f, Mathf.Sign(direction.y));
            if (_introHasMoveX) _introPartnerAnimator.SetFloat(IntroMoveX, cardinal.x);
            if (_introHasMoveY) _introPartnerAnimator.SetFloat(IntroMoveY, cardinal.y);
        }
        if (_introHasMoving) _introPartnerAnimator.SetBool(IntroIsMoving, moving);
    }

    private void KillIntroductionTweens()
    {
        _introPlayerTween?.Kill(false);
        _introPartnerTween?.Kill(false);
        _introPlayerTween = _introPartnerTween = null;
    }

    private void MarkOwnedIntroductionCancellation()
    {
        if (_introRunGeneration == _generation && _introRoom != null && _roomContainer != null
            && ReferenceEquals(_introRoom, _roomContainer.CurrentRoom) && _introPlayer != null
            && _introPlayer.OwnsOverworldPresentation(this, _introPlayerVersion))
            _introCancellationGeneration = _introRunGeneration;
    }

    private void ReleaseIntroduction(bool completed, int expectedGeneration)
    {
        if (expectedGeneration != _introRunGeneration || _introRunGeneration < 0) return;
        KillIntroductionTweens();
        bool ownsLease = _introPlayer != null && _introPlayer.OwnsOverworldPresentation(this, _introPlayerVersion);
        bool canRestore = PrologueTutorialIntroductionOwnership.CanRestore(
            _introRoom != null && _roomContainer != null && ReferenceEquals(_introRoom, _roomContainer.CurrentRoom),
            _introRunGeneration == _generation, ownsLease,
            _introCancellationGeneration == _introRunGeneration,
            BattleManager.Instance != null && BattleManager.Instance.IsBattleSessionActive);
        bool returnToStart = !completed || (_definition != null && _definition.Introduction != null && _definition.Introduction.ReturnToStart);
        if (ownsLease)
        {
            if (canRestore && returnToStart)
            {
                _introPlayer.transform.position = _introPlayerBaseline;
                _introPlayer.SetFacingDirection(_introPlayerFacing);
            }
            _introPlayer.EndOverworldPresentation(this, _introPlayerVersion);
        }
        if (canRestore && _introPartner != null)
        {
            if (returnToStart) _introPartner.position = _introPartnerBaseline;
            if (_introPartnerAnimator != null)
            {
                if (returnToStart && _introHasMoveX) _introPartnerAnimator.SetFloat(IntroMoveX, _introMoveXBaseline);
                if (returnToStart && _introHasMoveY) _introPartnerAnimator.SetFloat(IntroMoveY, _introMoveYBaseline);
                if (_introHasMoving) _introPartnerAnimator.SetBool(IntroIsMoving, returnToStart && _introMovingBaseline);
            }
            if (!completed) _introPartner.gameObject.SetActive(_introPartnerWasActive);
        }
        _introPlayer = null;
        _introPartner = null;
        _introPartnerAnimator = null;
        _introPlayerVersion = 0;
        _introRoom = null;
        _introRunGeneration = _introCancellationGeneration = -1;
    }
}
