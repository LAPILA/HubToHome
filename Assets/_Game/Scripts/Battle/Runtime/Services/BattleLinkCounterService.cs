using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>C 성공 시 공격받은 전열 한 명만 반격합니다. 턴/AP는 소비하지 않습니다.</summary>
public sealed class BattleLinkCounterService
{
    private const float RecoverDuration = 0.20f;
    private readonly IBattleTurnQteHost _host;
    private CounterPresentation _activePresentation;
    private int _reactionVersion;

    public BattleLinkCounterService(IBattleTurnQteHost host) { _host = host; }

    public void CancelActive()
    {
        _reactionVersion++;
        CounterPresentation presentation = _activePresentation;
        _activePresentation = null;
        presentation?.Dispose();
    }

    public IEnumerator Execute(EnemyCharacter attacker, PlayerCharacter defender,
        float damageMultiplier = 1.5f, Func<bool> isExecutionActive = null,
        Vector3? attackerReturnPosition = null, Vector3? defenderReturnPosition = null)
    {
        if (!CanContinue(attacker, isExecutionActive) || !IsActiveFrontMember(defender)
            || float.IsNaN(damageMultiplier) || float.IsInfinity(damageMultiplier) || damageMultiplier <= 0f)
            yield break;

        CancelActive();
        int version = _reactionVersion;
        bool Active() => version == _reactionVersion && CanContinue(attacker, isExecutionActive)
            && IsActiveFrontMember(defender);
        var controller = defender.GetComponent<PlayerController>();
        controller?.ResetDefenseReactionLock();
        var presentation = new CounterPresentation(_host, defender, attacker, attackerReturnPosition, defenderReturnPosition);
        _activePresentation = presentation;
        try
        {
            _host.SetActorForeground(defender, true);
            presentation.ForegroundApplied = true;
            PositionManager positions = PositionManager.Instance;
            presentation.CameraStability = CameraController.Instance != null
                ? CameraController.Instance.StabilizeBattleDefense() : null;
            presentation.ResumeAttackMotion();
            if (controller != null) controller.PlayCounterParry();
            else defender.PlayBattleAnim(PlayerController.HashParry);
            Vector3 recoil = defender.transform.position + Vector3.left
                * (positions != null ? positions.CounterRecoilDistance : 0.85f);
            presentation.Movement = MoveSafely(defender, recoil,
                    positions != null ? positions.CounterRecoilDuration : 0.13f)
                .SetEase(Ease.OutCubic).SetUpdate(true).SetRecyclable(false).SetAutoKill(false);
            while (presentation.IsMoving)
            {
                if (!Active()) yield break;
                yield return null;
            }
            if (!Active() || !presentation.CompletedMovement) yield break;
            presentation.ClearMovement();
            presentation.ReleaseCameraStability();
            if (CameraController.Instance != null) CameraController.Instance.TrackBattleCounter(defender);

            // 패링 클립 전체가 끝나기를 기다리지 않고 후퇴 직후 재접근합니다.
            defender.PlayBattleAnim(PlayerCharacter.HashBattleMove);
            Vector3 destination = PositionManager.Instance != null
                ? PositionManager.Instance.GetAttackStagingPos(defender, attacker)
                : PositionManager.HorizontalApproach(defender, attacker.GetPivot(CharacterPivotId.Front).position);
            presentation.Movement = MoveSafely(defender, destination,
                    positions != null ? positions.CounterLungeDuration : 0.16f)
                .SetEase(Ease.InQuad).SetUpdate(true).SetRecyclable(false).SetAutoKill(false);
            while (presentation.IsMoving)
            {
                if (!Active()) yield break;
                yield return null;
            }
            if (!Active() || !presentation.CompletedMovement) yield break;
            presentation.ClearMovement();

            defender.PlayAttackReady();
            presentation.Movement = DOTween.Sequence().AppendInterval(0.08f)
                .AppendCallback(() => { if (Active()) defender.PlayBattleAnim(PlayerCharacter.HashAttack); })
                .AppendInterval(Mathf.Max(0f, _host.PlayerAttackHitDelay))
                .SetTarget(defender.transform).SetUpdate(true).SetRecyclable(false).SetAutoKill(false);
            while (presentation.IsMoving)
            {
                if (!Active()) yield break;
                yield return null;
            }
            if (!Active() || !presentation.CompletedMovement) yield break;
            presentation.ClearMovement();
            defender.PlayBasicAttackEffect();
            int previousHp = attacker.CurrentHP;
            int rawDamage = Mathf.Max(1, Mathf.RoundToInt(defender.ATK * damageMultiplier));
            DamageResult result = attacker.TakeDamage(rawDamage, DamageElement.Physical, defender);
            if (!IsHostAvailable() || attacker == null) yield break;
            _host.PublishEnemyHpScenarioEvent(attacker, previousHp, attacker.CurrentHP, attacker.MaxHP,
                BattleRuleTiming.AfterCurrentAction);
            _host.EmitDamageNotificationOnly(defender, attacker, result.FinalDamage, false);
            _host.PublishEnemyDefeatedScenarioEvent(attacker, defender);
            if (!Active()) yield break;

            IEnumerator attack = defender.WaitForAttackAnimationComplete();
            try
            {
                while (attack.MoveNext())
                {
                    if (!Active()) yield break;
                    yield return attack.Current;
                }
            }
            finally { (attack as IDisposable)?.Dispose(); }
            if (!Active()) yield break;

            defender.PlayBattleAnim(PlayerCharacter.HashBattleMove);
            attacker.PlayBattleAnim(_host.ResolveEnemyReturnMoveHash(attacker));
            Sequence recovery = DOTween.Sequence()
                .Append(MoveSafely(defender, presentation.StartPosition, RecoverDuration).SetEase(Ease.OutQuad));
            if (attackerReturnPosition.HasValue)
                recovery.Join(MoveSafely(attacker, attackerReturnPosition.Value, RecoverDuration).SetEase(Ease.OutQuad));
            presentation.Movement = recovery.SetTarget(defender.transform).SetUpdate(true).SetRecyclable(false).SetAutoKill(false);
            while (presentation.IsMoving)
            {
                if (!Active()) yield break;
                yield return null;
            }
        }
        finally
        {
            if (ReferenceEquals(_activePresentation, presentation)) _activePresentation = null;
            presentation.Dispose();
        }
    }

    private static Tween MoveSafely(CharacterBase actor, Vector3 destination, float duration)
    {
        Vector3 last = actor.transform.position;
        return DOTween.To(() => actor != null ? actor.transform.position : last,
            value => { last = value; if (actor != null) actor.transform.position = value; },
            destination, duration).SetTarget(actor.transform);
    }

    private bool CanContinue(EnemyCharacter attacker, Func<bool> isExecutionActive)
    {
        return IsHostAvailable() && (isExecutionActive == null || isExecutionActive())
            && _host.IsTurnQteCombatInputActive() && attacker != null && attacker.IsAlive
            && attacker.gameObject.activeInHierarchy;
    }

    private bool IsActiveFrontMember(PlayerCharacter player)
    {
        if (player == null || !player.IsAlive || !player.gameObject.activeInHierarchy || !IsHostAvailable())
            return false;
        IReadOnlyList<PlayerCharacter> front = _host.PlayerParty;
        if (front == null) return false;
        for (int i = 0; i < front.Count; i++)
            if (front[i] == player) return true;
        return false;
    }

    private bool IsHostAvailable() => _host != null
        && !(_host is UnityEngine.Object unityHost && unityHost == null);

    private sealed class CounterPresentation : IDisposable
    {
        private readonly IBattleTurnQteHost _host;
        private readonly PlayerCharacter _player;
        private readonly EnemyCharacter _enemy;
        private readonly Vector3? _enemyReturnPosition;
        private readonly BattleAttackMotionScope _attackMotion;
        private bool _disposed;
        public readonly Vector3 StartPosition;
        public Tween Movement;
        public bool ForegroundApplied;
        public IDisposable CameraStability;
        public bool IsMoving => Movement != null && Movement.IsActive() && !Movement.IsComplete();
        public bool CompletedMovement => Movement != null && Movement.IsActive() && Movement.IsComplete();

        public CounterPresentation(IBattleTurnQteHost host, PlayerCharacter player,
            EnemyCharacter enemy, Vector3? enemyReturnPosition, Vector3? playerReturnPosition)
        {
            _host = host; _player = player; _enemy = enemy;
            StartPosition = playerReturnPosition ?? player.transform.position;
            _enemyReturnPosition = enemyReturnPosition;
            _attackMotion = new BattleAttackMotionScope(enemy);
            _attackMotion.SetRate(0f); // 패링이 시작될 때까지 타격 프레임을 보존합니다.
        }

        public void ResumeAttackMotion() => _attackMotion.SetRate(1f);

        public void ReleaseCameraStability()
        {
            CameraStability?.Dispose();
            CameraStability = null;
        }

        public void ClearMovement()
        {
            if (Movement != null && Movement.IsActive()) Movement.Kill(false);
            Movement = null;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            ClearMovement();
            ReleaseCameraStability();
            _attackMotion.Dispose();
            if (_enemy != null && _enemyReturnPosition.HasValue)
            {
                _enemy.transform.position = _enemyReturnPosition.Value;
                if (_enemy.IsAlive && _enemy.gameObject.activeInHierarchy)
                    _enemy.PlayBattleAnim(EnemyCharacter.HashBattleIdle);
            }
            if (_player == null) return;
            _player.transform.position = StartPosition;
            if (_player.IsAlive && _player.gameObject.activeInHierarchy)
                _player.PlayBattleAnim(PlayerCharacter.HashBattleIdle);
            if (ForegroundApplied && _host != null
                && !(_host is UnityEngine.Object unityHost && unityHost == null))
                _host.SetActorForeground(_player, false);
        }
    }
}
