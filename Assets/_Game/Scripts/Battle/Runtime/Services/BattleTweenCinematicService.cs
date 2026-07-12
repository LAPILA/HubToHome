using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public sealed class BattleTweenCinematicService : IBattleTweenCinematicService
{
    private readonly IBattleCinematicHost _host;

    public BattleTweenCinematicService(IBattleCinematicHost host)
    {
        _host = host;
    }

    public IEnumerator SetLetterbox(
        bool visible,
        float thickness,
        float duration,
        object tweenTarget,
        ActionExecutionHandle handle)
    {
        CinematicLetterboxOverlay overlay = null;
        try
        {
            overlay = CinematicLetterboxOverlay.GetOrCreate();
        }
        catch (Exception exception)
        {
            BattleCinematicService.SafeWarn("cinematic.letterbox overlay could not be created.", exception);
        }

        if (overlay == null)
        {
            yield break;
        }

        float targetThickness = visible ? Mathf.Clamp01(thickness) : 0f;
        float clampedDuration = Mathf.Max(0f, duration);
        if (clampedDuration <= 0f)
        {
            overlay.SetThicknessImmediate(targetThickness);
            yield break;
        }

        Tween tween = DOTween.To(
                () => overlay.CurrentThickness,
                overlay.SetThicknessImmediate,
                targetThickness,
                clampedDuration)
            .SetEase(Ease.InOutSine)
            .SetUpdate(true)
            .SetTarget(tweenTarget ?? overlay);

        IEnumerator routine = BattleCinematicService.WaitTween(tween, handle);
        while (routine.MoveNext())
        {
            yield return routine.Current;
        }
    }

    public IEnumerator MoveActor(
        string subjectId,
        string anchor,
        float x,
        float y,
        float duration,
        string pose,
        float impact,
        ActionExecutionHandle handle)
    {
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
            BattleCinematicService.PlayPose(subject, string.IsNullOrWhiteSpace(pose) ? "move" : pose);

            Sequence sequence = DOTween.Sequence()
                .SetTarget(subject)
                .Append(BattleCinematicService.StartMoveTween(subject, destination, duration, Ease.InOutSine));

            IEnumerator routine = BattleCinematicService.WaitTween(sequence, handle);
            while (routine.MoveNext())
            {
                yield return routine.Current;
            }

            if (handle != null && handle.IsCancellationRequested)
            {
                yield break;
            }

            if (impact > 0f)
            {
                IEnumerator shakeRoutine = PlayCameraShake(Vector3.right, impact, 0.12f, true, subject, handle);
                while (shakeRoutine.MoveNext())
                {
                    yield return shakeRoutine.Current;
                }
            }

            BattleCinematicService.PlayPose(subject, "idle");
        }
        finally
        {
            BattleManager.SetGhostTrail(subject, false);
            BattleCinematicService.PlayPose(subject, "idle");
            if (foregroundApplied)
            {
                _host?.SetActorForeground(subject, false);
            }
        }
    }

    public IEnumerator DropActorIn(
        string subjectId,
        float height,
        float hangDuration,
        float fallDuration,
        float settleDuration,
        float impact,
        ActionExecutionHandle handle)
    {
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
            BattleCinematicService.PlayPose(subject, "move");

            IEnumerator hangRoutine = BattleCinematicService.WaitRealtime(hangDuration, handle);
            while (hangRoutine.MoveNext())
            {
                yield return hangRoutine.Current;
            }

            if (handle != null && handle.IsCancellationRequested)
            {
                yield break;
            }

            Sequence fallSequence = DOTween.Sequence()
                .SetTarget(subject)
                .Append(BattleCinematicService.StartMoveTween(subject, landingPosition, fallDuration, Ease.InExpo));

            IEnumerator fallRoutine = BattleCinematicService.WaitTween(fallSequence, handle);
            while (fallRoutine.MoveNext())
            {
                yield return fallRoutine.Current;
            }

            if (handle != null && handle.IsCancellationRequested)
            {
                yield break;
            }

            BattleManager.SetGhostTrail(subject, false);
            BattleCinematicService.PlayPose(subject, "attack");

            IEnumerator shakeRoutine = PlayCameraShake(Vector3.down, impact, 0.12f, true, subject, handle);
            while (shakeRoutine.MoveNext())
            {
                yield return shakeRoutine.Current;
            }

            IEnumerator settleRoutine = BattleCinematicService.WaitRealtime(settleDuration, handle);
            while (settleRoutine.MoveNext())
            {
                yield return settleRoutine.Current;
            }

            BattleCinematicService.PlayPose(subject, "idle");
        }
        finally
        {
            subject.transform.DOKill(false);
            subject.transform.position = landingPosition;
            BattleManager.SetGhostTrail(subject, false);
            BattleCinematicService.PlayPose(subject, "idle");
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
            BattleCinematicService.PlayPose(actor, "move");

            Sequence lungeSequence = DOTween.Sequence()
                .SetTarget(actor)
                .Append(BattleCinematicService.StartMoveTween(actor, strikePosition, lungeDuration, Ease.OutExpo));

            IEnumerator lungeRoutine = BattleCinematicService.WaitTween(lungeSequence, handle);
            while (lungeRoutine.MoveNext())
            {
                yield return lungeRoutine.Current;
            }

            if (handle != null && handle.IsCancellationRequested)
            {
                yield break;
            }

            BattleManager.SetGhostTrail(actor, false);
            BattleCinematicService.PlayPose(actor, "attack");
            BattleCinematicService.PlayAttackEffect(actor, "attack");
            BattleCinematicService.PlayPose(target, string.IsNullOrWhiteSpace(targetPose) ? "hurt" : targetPose);

            IEnumerator shakeRoutine = PlayCameraShake(
                actor is PlayerCharacter ? Vector3.right : Vector3.left,
                impact,
                0.12f,
                true,
                actor,
                handle);
            while (shakeRoutine.MoveNext())
            {
                yield return shakeRoutine.Current;
            }

            IEnumerator holdRoutine = BattleCinematicService.WaitRealtime(holdDuration, handle);
            while (holdRoutine.MoveNext())
            {
                yield return holdRoutine.Current;
            }

            if (handle != null && handle.IsCancellationRequested)
            {
                yield break;
            }

            BattleCinematicService.PlayPose(actor, "move");
            BattleManager.SetGhostTrail(actor, true);

            Sequence recoverSequence = DOTween.Sequence()
                .SetTarget(actor)
                .Append(BattleCinematicService.StartMoveTween(actor, originalPosition, recoverDuration, Ease.OutQuad));

            IEnumerator recoverRoutine = BattleCinematicService.WaitTween(recoverSequence, handle);
            while (recoverRoutine.MoveNext())
            {
                yield return recoverRoutine.Current;
            }

            BattleManager.SetGhostTrail(actor, false);
            BattleCinematicService.PlayPose(actor, "idle");
            BattleCinematicService.PlayPose(target, "idle");
        }
        finally
        {
            BattleManager.SetGhostTrail(actor, false);
            BattleCinematicService.PlayPose(actor, "idle");
            BattleCinematicService.PlayPose(target, "idle");
            if (foregroundApplied)
            {
                _host?.SetActorForeground(actor, false);
            }
        }
    }

    public IEnumerator ReturnActorsToSlots(float duration, ActionExecutionHandle handle)
    {
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
            BattleCinematicService.PlayPose(player, "move");
            Tween tween = BattleCinematicService.StartMoveTween(player, targetPosition, clampedDuration, Ease.OutQuad);
            if (tween != null)
            {
                tween.SetTarget(player);
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
            BattleCinematicService.PlayPose(enemy, "move_back");
            Tween tween = BattleCinematicService.StartMoveTween(enemy, targetPosition, clampedDuration, Ease.OutQuad);
            if (tween != null)
            {
                tween.SetTarget(enemy);
                tweens.Add(tween);
            }
        }

        float elapsed = 0f;
        while (elapsed < clampedDuration)
        {
            if (handle != null && handle.IsCancellationRequested)
            {
                BattleCinematicService.KillTweens(tweens);
                yield break;
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        for (int i = 0; i < players.Count; i++)
        {
            BattleCinematicService.PlayPose(players[i], "idle");
        }

        for (int i = 0; i < enemies.Count; i++)
        {
            BattleCinematicService.PlayPose(enemies[i], "idle");
        }

        CameraController.Instance?.ResetCamera(0.35f);
    }

    public IEnumerator PlayCameraShake(
        Vector3 direction,
        float intensity,
        float duration,
        bool lockHorizontal,
        object tweenTarget,
        ActionExecutionHandle handle)
    {
        CameraController controller = CameraController.Instance;
        if (controller == null || intensity <= 0f)
        {
            yield break;
        }

        Vector3 safeDirection = lockHorizontal
            ? new Vector3(direction.x, 0f, 0f)
            : direction;
        if (safeDirection.sqrMagnitude <= 0.000001f)
        {
            safeDirection = Vector3.right;
        }
        if (!controller.TryImpulse(
                safeDirection,
                intensity,
                Mathf.Max(0.01f, duration),
                CameraShakeSafety.GameplaySafe,
                out string error))
        {
            if (handle != null)
            {
                handle.Fail(error);
            }
            else
            {
                BattleCinematicService.SafeWarn(error);
            }
            yield break;
        }

        IEnumerator routine = BattleCinematicService.WaitRealtime(duration, handle);
        while (routine.MoveNext())
        {
            yield return routine.Current;
        }
    }

    public IEnumerator PlayUiFlash(
        Color color,
        float alpha,
        float duration,
        object tweenTarget,
        ActionExecutionHandle handle)
    {
        BattleUIController controller = BattleUIController.Instance;
        if (controller == null)
        {
            yield break;
        }

        Sequence sequence = controller.PlayScenarioUiFlash(color, alpha, duration, tweenTarget);
        IEnumerator routine = BattleCinematicService.WaitTween(sequence, handle);
        while (routine.MoveNext())
        {
            yield return routine.Current;
        }
    }

    public IEnumerator PlayUiShake(
        Vector2 strength,
        float duration,
        int vibrato,
        float randomness,
        object tweenTarget,
        ActionExecutionHandle handle)
    {
        BattleUIController controller = BattleUIController.Instance;
        if (controller == null)
        {
            yield break;
        }

        Tween tween = controller.PlayScenarioUiShake(strength, duration, vibrato, randomness, tweenTarget);
        IEnumerator routine = BattleCinematicService.WaitTween(tween, handle);
        while (routine.MoveNext())
        {
            yield return routine.Current;
        }
    }

    private CharacterBase FindParticipantOrSkip(string subjectId)
    {
        if (_host == null)
        {
            BattleCinematicService.SafeWarn("Battle tween cinematic host is missing.");
            return null;
        }

        CharacterBase subject = _host.FindBattleParticipantBySubjectId(subjectId);
        if (subject == null)
        {
            BattleCinematicService.SafeWarn("Battle tween cinematic participant was not found: " + subjectId);
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

    private Vector3 ResolveAnchorPosition(CharacterBase subject, string anchor)
    {
        PositionManager pm = PositionManager.Instance;
        string normalizedAnchor = string.IsNullOrWhiteSpace(anchor) ? "current" : anchor.Trim().ToLowerInvariant();
        switch (normalizedAnchor)
        {
            case "center":
                return pm != null ? pm.GetCenterPos() : subject.transform.position;
            case "player_slot":
                return subject is PlayerCharacter ? ResolveSlotPosition(subject) : subject.transform.position;
            case "enemy_slot":
                return subject is EnemyCharacter ? ResolveSlotPosition(subject) : subject.transform.position;
            default:
                return subject.transform.position;
        }
    }
}