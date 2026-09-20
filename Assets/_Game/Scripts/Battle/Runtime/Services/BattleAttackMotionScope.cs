using System;
using UnityEngine;

/// <summary>현재 공격자의 Animator만 감속/정지합니다. QTE 종료·취소가 반드시 Dispose합니다.</summary>
public sealed class BattleAttackMotionScope : IDisposable
{
    private Animator _animator;
    private readonly float _baselineSpeed;
    private readonly AnimatorUpdateMode _baselineMode;
    private float _ownedSpeed;

    public BattleAttackMotionScope(EnemyCharacter attacker)
    {
        _animator = attacker != null ? attacker.GetComponent<Animator>() : null;
        if (_animator == null) return;
        _baselineSpeed = _ownedSpeed = _animator.speed;
        _baselineMode = _animator.updateMode;
        _animator.updateMode = AnimatorUpdateMode.UnscaledTime;
    }

    public void SetRate(float rate)
    {
        if (_animator == null) return;
        // 다른 소유자가 속도를 바꾸면 그 값을 덮지 않습니다.
        if (!Mathf.Approximately(_animator.speed, _ownedSpeed)) { Dispose(); return; }
        _ownedSpeed = _baselineSpeed * Mathf.Max(0f, rate);
        _animator.speed = _ownedSpeed;
    }

    public void Dispose()
    {
        if (_animator != null)
        {
            if (Mathf.Approximately(_animator.speed, _ownedSpeed)) _animator.speed = _baselineSpeed;
            if (_animator.updateMode == AnimatorUpdateMode.UnscaledTime) _animator.updateMode = _baselineMode;
        }
        _animator = null;
    }
}
