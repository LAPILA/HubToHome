using UnityEngine;

/// <summary>슬로모션과 무관한 전투 지연. 메뉴/설정 중에는 시간 예산을 소비하지 않습니다.</summary>
public sealed class BattleDelay : CustomYieldInstruction
{
    public static bool IsPaused => GameInput.IsConfigModalActive
        || (GameStateManager.Instance != null && GameStateManager.Instance.CurrentState == GameState.Paused);
    private float _remaining;

    public BattleDelay(float duration)
    {
        _remaining = float.IsNaN(duration) || float.IsInfinity(duration) ? 0f : Mathf.Max(0f, duration);
    }

    public override bool keepWaiting
    {
        get
        {
            if (IsPaused) return true;
            _remaining -= Time.unscaledDeltaTime;
            return _remaining > 0f;
        }
    }
}
