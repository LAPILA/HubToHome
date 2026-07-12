using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public sealed class BattleCinematicService : IBattleCinematicRunner
{
    private readonly IBattleCinematicHost _host;
    private readonly IBattleTweenCinematicService _tweenService;
    private readonly Dictionary<CharacterBase, bool> _actorDefaultFlipXCache = new Dictionary<CharacterBase, bool>();

    public BattleCinematicService(
        IBattleCinematicHost host,
        IBattleTweenCinematicService tweenService = null)
    {
        _host = host;
        _tweenService = tweenService;
    }

    public IEnumerator SetLetterbox(bool visible, float thickness, float duration, ActionExecutionHandle handle)
    {
        if (_tweenService != null)
        {
            IEnumerator tweenRoutine = _tweenService.SetLetterbox(visible, thickness, duration, null, handle);
            while (tweenRoutine.MoveNext())
            {
                yield return tweenRoutine.Current;
            }

            yield break;
        }

        CinematicLetterboxOverlay overlay = null;
        try
        {
            overlay = CinematicLetterboxOverlay.GetOrCreate();
        }
        catch (Exception exception)
        {
            SafeWarn("cinematic.letterbox overlay could not be created.", exception);
        }

        if (overlay == null)
        {
            yield break;
        }

        IEnumerator routine = overlay.SetVisible(visible, thickness, Mathf.Max(0f, duration), handle);
        while (routine.MoveNext())
        {
            yield return routine.Current;
        }
    }

    public IEnumerator FocusCamera(
        string subjectId,
        float zoom,
        float duration,
        CameraShotStyle style,
        ActionExecutionHandle handle)
    {
        CharacterBase subject = FindParticipantOrSkip(subjectId);
        if (subject == null)
        {
            handle?.Fail("battle.camera.focus subject was not found: " + subjectId);
            yield break;
        }

        CameraController cameraController = CameraController.Instance;
        if (cameraController == null)
        {
            handle?.Fail("battle.camera.focus requires CameraController.");
            yield break;
        }

        if (!cameraController.TryFocus(
                subject.transform,
                zoom,
                style,
                duration,
                CameraControlLease.None,
                out CameraCommandToken token,
                out string error))
        {
            handle?.Fail(error);
            yield break;
        }

        Action<ActionExecutionHandle> onCanceled = null;
        onCanceled = _ =>
        {
            if (handle != null)
            {
                handle.CancellationRequested -= onCanceled;
            }
            cameraController.Cancel(token, true);
        };
        if (handle != null)
        {
            handle.CancellationRequested += onCanceled;
        }

        IEnumerator waitRoutine = WaitRealtime(duration, handle);
        while (waitRoutine.MoveNext())
        {
            yield return waitRoutine.Current;
        }

        if (handle != null)
        {
            handle.CancellationRequested -= onCanceled;
        }
    }

    public IEnumerator ResetCamera(float duration, CameraShotStyle style, ActionExecutionHandle handle)
    {
        CameraController cameraController = CameraController.Instance;
        if (cameraController == null)
        {
            handle?.Fail("battle.camera.reset requires CameraController.");
            yield break;
        }

        if (!cameraController.TryReset(
                duration,
                style,
                CameraControlLease.None,
                out CameraCommandToken token,
                out string error))
        {
            handle?.Fail(error);
            yield break;
        }

        Action<ActionExecutionHandle> onCanceled = null;
        onCanceled = _ =>
        {
            if (handle != null)
            {
                handle.CancellationRequested -= onCanceled;
            }
            cameraController.Cancel(token, true);
        };
        if (handle != null)
        {
            handle.CancellationRequested += onCanceled;
        }

        IEnumerator waitRoutine = WaitRealtime(duration, handle);
        while (waitRoutine.MoveNext())
        {
            yield return waitRoutine.Current;
        }

        if (handle != null)
        {
            handle.CancellationRequested -= onCanceled;
        }
    }

    public IEnumerator ShakeCamera(
        Vector3 direction,
        float intensity,
        float duration,
        CameraShakeSafety safety,
        ActionExecutionHandle handle)
    {
        CameraController cameraController = CameraController.Instance;
        if (cameraController == null)
        {
            handle?.Fail("battle.camera.shake requires CameraController.");
            yield break;
        }

        if (!cameraController.TryImpulse(direction, intensity, duration, safety, out string error))
        {
            handle?.Fail(error);
            yield break;
        }

        IEnumerator waitRoutine = WaitRealtime(duration, handle);
        while (waitRoutine.MoveNext())
        {
            yield return waitRoutine.Current;
        }
    }

    public IEnumerator PlayActorPose(string subjectId, string pose, float duration, float impact, ActionExecutionHandle handle)
    {
        CharacterBase subject = FindParticipantOrSkip(subjectId);
        if (subject == null)
        {
            yield break;
        }

        PlayPose(subject, pose);
        PlayAttackEffect(subject, pose);
        if (impact > 0f)
        {
            CameraController.Instance?.PlayHeavySlam(Vector3.right, Mathf.Max(0f, impact), true);
        }

        IEnumerator waitRoutine = WaitRealtime(duration, handle);
        while (waitRoutine.MoveNext())
        {
            yield return waitRoutine.Current;
        }
    }

    public IEnumerator SetActorFlip(string subjectId, string mode, ActionExecutionHandle handle)
    {
        CharacterBase subject = FindParticipantOrSkip(subjectId);
        if (subject == null)
        {
            yield break;
        }

        ApplyActorFlip(subject, mode);
        yield break;
    }

    public IEnumerator MoveActor(string subjectId, string anchor, float x, float y, float duration, string pose, float impact, ActionExecutionHandle handle)
    {
        if (_tweenService != null)
        {
            IEnumerator tweenRoutine = _tweenService.MoveActor(subjectId, anchor, x, y, duration, pose, impact, handle);
            while (tweenRoutine.MoveNext())
            {
                yield return tweenRoutine.Current;
            }

            yield break;
        }

        CharacterBase subject = FindParticipantOrSkip(subjectId);
        if (subject == null)
        {
            yield break;
        }

        bool foregroundApplied = false;
        try
        {
            Vector3 destination = ResolveAnchorPosition(subject, anchor) + new Vector3(x, y, 0f);
            _host?.SetActorForeground(subject, true);
            foregroundApplied = true;
            BattleManager.SetGhostTrail(subject, true);
            PlayPose(subject, string.IsNullOrWhiteSpace(pose) ? "move" : pose);

            Tween move = StartMoveTween(subject, destination, duration, Ease.InOutSine);
            yield return WaitTween(move, handle);
            if (handle != null && handle.IsCancellationRequested)
            {
                yield break;
            }

            if (impact > 0f)
            {
                CameraController.Instance?.PlayHeavySlam(Vector3.right, Mathf.Max(0f, impact), true);
            }

            PlayPose(subject, "idle");
        }
        finally
        {
            BattleManager.SetGhostTrail(subject, false);
            PlayPose(subject, "idle");
            if (foregroundApplied)
            {
                _host?.SetActorForeground(subject, false);
            }
        }
    }

    public IEnumerator DropActorIn(string subjectId, float height, float hangDuration, float fallDuration, float settleDuration, float impact, ActionExecutionHandle handle)
    {
        if (_tweenService != null)
        {
            IEnumerator tweenRoutine = _tweenService.DropActorIn(subjectId, height, hangDuration, fallDuration, settleDuration, impact, handle);
            while (tweenRoutine.MoveNext())
            {
                yield return tweenRoutine.Current;
            }

            yield break;
        }

        CharacterBase subject = FindParticipantOrSkip(subjectId);
        if (subject == null)
        {
            yield break;
        }

        Vector3 landingPosition = ResolveSlotPosition(subject);
        Vector3 currentPosition = subject.transform.position;
        Vector3 startPosition = currentPosition.y > landingPosition.y + 0.05f || Vector3.Distance(currentPosition, landingPosition) > 0.25f
            ? currentPosition
            : landingPosition + Vector3.up * Mathf.Max(0f, height);
        bool foregroundApplied = false;

        try
        {
            _host?.SetActorForeground(subject, true);
            foregroundApplied = true;
            subject.transform.DOKill(false);
            subject.transform.position = startPosition;
            CameraController.Instance?.SetTarget(subject.transform);
            BattleManager.SetGhostTrail(subject, true);
            PlayPose(subject, "move");

            yield return WaitRealtime(hangDuration, handle);
            if (handle != null && handle.IsCancellationRequested)
            {
                yield break;
            }

            Tween fall = StartMoveTween(subject, landingPosition, fallDuration, Ease.InExpo);
            yield return WaitTween(fall, handle);
            if (handle != null && handle.IsCancellationRequested)
            {
                yield break;
            }

            BattleManager.SetGhostTrail(subject, false);
            PlayPose(subject, "attack");
            CameraController.Instance?.PlayHeavySlam(Vector3.down, Mathf.Max(0f, impact), true);
            yield return WaitRealtime(settleDuration, handle);
            PlayPose(subject, "idle");
        }
        finally
        {
            subject.transform.DOKill(false);
            subject.transform.position = landingPosition;
            BattleManager.SetGhostTrail(subject, false);
            PlayPose(subject, "idle");
            if (foregroundApplied)
            {
                _host?.SetActorForeground(subject, false);
            }
        }
    }

    public IEnumerator PlayFakeAttack(
        string actorId,
        string targetId,
        string targetPose,
        float approachDistance,
        float lungeDuration,
        float holdDuration,
        float recoverDuration,
        float impact,
        ActionExecutionHandle handle)
    {
        if (_tweenService != null)
        {
            IEnumerator tweenRoutine = _tweenService.PlayFakeAttack(
                actorId,
                targetId,
                targetPose,
                approachDistance,
                lungeDuration,
                holdDuration,
                recoverDuration,
                impact,
                handle);
            while (tweenRoutine.MoveNext())
            {
                yield return tweenRoutine.Current;
            }

            yield break;
        }

        CharacterBase actor = FindParticipantOrSkip(actorId);
        CharacterBase target = FindParticipantOrSkip(targetId);
        if (actor == null || target == null)
        {
            yield break;
        }

        bool foregroundApplied = false;
        try
        {
            Vector3 originalPosition = actor.transform.position;
            Vector3 directionFromTarget = actor.transform.position - target.transform.position;
            if (directionFromTarget.sqrMagnitude < 0.0001f)
            {
                directionFromTarget = actor is PlayerCharacter ? Vector3.left : Vector3.right;
            }

            Vector3 strikePosition = target.transform.position + directionFromTarget.normalized * Mathf.Max(0.15f, approachDistance);
            _host?.SetActorForeground(actor, true);
            foregroundApplied = true;
            BattleManager.SetGhostTrail(actor, true);
            PlayPose(actor, "move");
            Tween lunge = StartMoveTween(actor, strikePosition, lungeDuration, Ease.OutExpo);
            yield return WaitTween(lunge, handle);
            if (handle != null && handle.IsCancellationRequested)
            {
                yield break;
            }

            BattleManager.SetGhostTrail(actor, false);
            PlayPose(actor, "attack");
            PlayAttackEffect(actor, "attack");
            PlayPose(target, string.IsNullOrWhiteSpace(targetPose) ? "hurt" : targetPose);
            CameraController.Instance?.PlayHeavySlam(actor is PlayerCharacter ? Vector3.right : Vector3.left, Mathf.Max(0f, impact), true);
            yield return WaitRealtime(holdDuration, handle);
            if (handle != null && handle.IsCancellationRequested)
            {
                yield break;
            }

            PlayPose(actor, "move");
            BattleManager.SetGhostTrail(actor, true);
            Tween recover = StartMoveTween(actor, originalPosition, recoverDuration, Ease.OutQuad);
            yield return WaitTween(recover, handle);
            BattleManager.SetGhostTrail(actor, false);

            PlayPose(actor, "idle");
            PlayPose(target, "idle");
        }
        finally
        {
            BattleManager.SetGhostTrail(actor, false);
            PlayPose(actor, "idle");
            PlayPose(target, "idle");
            if (foregroundApplied)
            {
                _host?.SetActorForeground(actor, false);
            }
        }
    }

    public IEnumerator ReturnActorsToSlots(float duration, ActionExecutionHandle handle)
    {
        if (_tweenService != null)
        {
            IEnumerator tweenRoutine = _tweenService.ReturnActorsToSlots(duration, handle);
            while (tweenRoutine.MoveNext())
            {
                yield return tweenRoutine.Current;
            }

            yield break;
        }

        if (_host == null)
        {
            yield break;
        }

        PositionManager pm = PositionManager.Instance;
        float clampedDuration = Mathf.Max(0f, duration);
        var tweens = new List<Tween>();

        IReadOnlyList<PlayerCharacter> players = _host.PlayerParty;
        for (int i = 0; i < players.Count; i++)
        {
            PlayerCharacter player = players[i];
            if (player == null || !player.IsAlive)
            {
                continue;
            }

            Vector3 targetPosition = pm != null ? pm.GetPlayerDefaultPos(i) : player.transform.position;
            _host.SetActorForeground(player, false);
            PlayPose(player, "move");
            Tween tween = StartMoveTween(player, targetPosition, clampedDuration, Ease.OutQuad);
            if (tween != null)
            {
                tweens.Add(tween);
            }
        }

        IReadOnlyList<EnemyCharacter> enemies = _host.Enemies;
        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyCharacter enemy = enemies[i];
            if (enemy == null || !enemy.IsAlive)
            {
                continue;
            }

            Vector3 targetPosition = pm != null ? pm.GetEnemyDefaultPos(i) : enemy.transform.position;
            _host.SetActorForeground(enemy, false);
            PlayPose(enemy, "move_back");
            Tween tween = StartMoveTween(enemy, targetPosition, clampedDuration, Ease.OutQuad);
            if (tween != null)
            {
                tweens.Add(tween);
            }
        }

        float elapsed = 0f;
        while (elapsed < clampedDuration)
        {
            if (handle != null && handle.IsCancellationRequested)
            {
                KillTweens(tweens);
                yield break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        for (int i = 0; i < players.Count; i++)
        {
            PlayPose(players[i], "idle");
        }

        for (int i = 0; i < enemies.Count; i++)
        {
            PlayPose(enemies[i], "idle");
        }

        CameraController.Instance?.ResetCamera(0.35f);
    }

    private CharacterBase FindParticipantOrSkip(string subjectId)
    {
        if (_host == null)
        {
            SafeWarn("Battle cinematic host is missing.");
            return null;
        }

        CharacterBase subject = _host.FindBattleParticipantBySubjectId(subjectId);
        if (subject == null)
        {
            SafeWarn("Battle cinematic participant was not found: " + subjectId);
        }

        return subject;
    }

    private Vector3 ResolveSlotPosition(CharacterBase subject)
    {
        if (subject == null)
        {
            return Vector3.zero;
        }

        PositionManager pm = PositionManager.Instance;
        if (pm == null || _host == null)
        {
            return subject.transform.position;
        }

        if (subject is PlayerCharacter player)
        {
            IReadOnlyList<PlayerCharacter> players = _host.PlayerParty;
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i] == player)
                {
                    return pm.GetPlayerDefaultPos(i);
                }
            }
        }

        if (subject is EnemyCharacter enemy)
        {
            IReadOnlyList<EnemyCharacter> enemies = _host.Enemies;
            for (int i = 0; i < enemies.Count; i++)
            {
                if (enemies[i] == enemy)
                {
                    return pm.GetEnemyDefaultPos(i);
                }
            }
        }

        return subject.transform.position;
    }

    private void ApplyActorFlip(CharacterBase subject, string mode)
    {
        if (subject == null)
        {
            return;
        }

        SpriteRenderer renderer = subject.GetComponent<SpriteRenderer>() ?? subject.GetComponentInChildren<SpriteRenderer>();
        if (renderer == null)
        {
            SafeWarn("battle.actor.flip skipped because SpriteRenderer was not found on " + subject.name);
            return;
        }

        if (!_actorDefaultFlipXCache.ContainsKey(subject))
        {
            _actorDefaultFlipXCache.Add(subject, renderer.flipX);
        }

        bool defaultFlip = _actorDefaultFlipXCache[subject];
        string normalized = string.IsNullOrWhiteSpace(mode) ? "default" : mode.Trim().ToLowerInvariant();
        switch (normalized)
        {
            case "invert":
            case "inverted":
            case "flipped":
                renderer.flipX = !defaultFlip;
                break;
            case "toggle":
                renderer.flipX = !renderer.flipX;
                break;
            default:
                renderer.flipX = defaultFlip;
                break;
        }
    }

    private Vector3 ResolveAnchorPosition(CharacterBase subject, string anchor)
    {
        PositionManager pm = PositionManager.Instance;
        string normalizedAnchor = string.IsNullOrWhiteSpace(anchor) ? "current" : anchor.Trim().ToLowerInvariant();
        switch (normalizedAnchor)
        {
            case "center":
                return pm != null ? pm.GetCenterPos() : subject.transform.position;
            case "player_slot":
                if (subject is PlayerCharacter)
                {
                    return ResolveSlotPosition(subject);
                }

                return subject.transform.position;
            case "enemy_slot":
                if (subject is EnemyCharacter)
                {
                    return ResolveSlotPosition(subject);
                }

                return subject.transform.position;
            default:
                return subject.transform.position;
        }
    }

    internal static void PlayPose(CharacterBase actor, string pose)
    {
        if (actor == null)
        {
            return;
        }

        string normalized = string.IsNullOrWhiteSpace(pose) ? "idle" : pose.Trim().ToLowerInvariant();
        if (actor is PlayerCharacter player)
        {
            TryPlayPose(() => player.PlayBattleAnim(ResolvePlayerPose(normalized)), player, normalized);
            return;
        }

        if (actor is EnemyCharacter enemy)
        {
            TryPlayPose(() => enemy.PlayBattleAnim(ResolveEnemyPose(normalized)), enemy, normalized);
        }
    }

    private static int ResolvePlayerPose(string pose)
    {
        switch (pose)
        {
            case "move": return PlayerCharacter.HashBattleMove;
            case "ready": return PlayerCharacter.HashBattleReady;
            case "attack": return PlayerCharacter.HashAttack;
            case "hurt": return PlayerCharacter.HashHurt;
            case "parry":
            case "guard":
            case "block": return PlayerCharacter.HashBattleReady;
            default: return PlayerCharacter.HashBattleIdle;
        }
    }

    private static int ResolveEnemyPose(string pose)
    {
        switch (pose)
        {
            case "move": return EnemyCharacter.HashBattleMove;
            case "move_back": return EnemyCharacter.HashBattleMoveBack;
            case "attack": return EnemyCharacter.HashAttack;
            case "skill":
            case "strong_skill": return EnemyCharacter.HashSkill;
            case "hurt": return EnemyCharacter.HashHurt;
            case "parry":
            case "guard":
            case "block": return EnemyCharacter.HashBattleIdle;
            default: return EnemyCharacter.HashBattleIdle;
        }
    }

    internal static void PlayAttackEffect(CharacterBase actor, string pose)
    {
        if (actor == null || string.IsNullOrWhiteSpace(pose))
        {
            return;
        }

        string normalized = pose.Trim().ToLowerInvariant();
        if (normalized != "attack" && normalized != "skill" && normalized != "strong_skill")
        {
            return;
        }

        if (actor is PlayerCharacter player)
        {
            TryPlayPose(player.PlayBasicAttackEffect, player, normalized + ":effect");
            return;
        }

        if (actor is EnemyCharacter enemy)
        {
            TryPlayPose(enemy.PlayBasicAttackEffect, enemy, normalized + ":effect");
        }
    }

    internal static Tween StartMoveTween(CharacterBase actor, Vector3 targetPosition, float duration, Ease ease)
    {
        if (actor == null)
        {
            return null;
        }

        Transform actorTransform = actor.transform;
        if (actorTransform == null)
        {
            return null;
        }

        actorTransform.DOKill(false);
        float clampedDuration = Mathf.Max(0f, duration);
        if (clampedDuration <= 0f)
        {
            actorTransform.position = targetPosition;
            return null;
        }

        return actorTransform.DOMove(targetPosition, clampedDuration).SetEase(ease);
    }

    private static void TryPlayPose(Action playAction, CharacterBase actor, string pose)
    {
        try
        {
            playAction?.Invoke();
        }
        catch (Exception exception)
        {
            string actorName = actor != null ? actor.name : "<missing>";
            SafeWarn("Battle cinematic pose skipped: " + actorName + " / " + pose, exception);
        }
    }

    internal static IEnumerator WaitTween(Tween tween, ActionExecutionHandle handle)
    {
        if (tween == null)
        {
            yield break;
        }

        while (tween.IsActive() && tween.IsPlaying())
        {
            if (handle != null && handle.IsCancellationRequested)
            {
                tween.Kill(false);
                yield break;
            }

            yield return null;
        }
    }

    internal static IEnumerator WaitRealtime(float duration, ActionExecutionHandle handle)
    {
        float elapsed = 0f;
        float clampedDuration = Mathf.Max(0f, duration);
        while (elapsed < clampedDuration)
        {
            if (handle != null && handle.IsCancellationRequested)
            {
                yield break;
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    internal static void KillTweens(List<Tween> tweens)
    {
        if (tweens == null)
        {
            return;
        }

        for (int i = 0; i < tweens.Count; i++)
        {
            Tween tween = tweens[i];
            if (tween != null && tween.IsActive())
            {
                tween.Kill(false);
            }
        }
    }

    internal static void SafeWarn(string message, Exception exception = null)
    {
        if (exception != null)
        {
            Debug.LogWarning(message + " " + exception.GetType().Name + ": " + exception.Message);
            return;
        }

        Debug.LogWarning(message);
    }
}