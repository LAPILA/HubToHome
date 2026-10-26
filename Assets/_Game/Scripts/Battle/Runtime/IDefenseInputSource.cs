/// <summary>
/// Supplies one actor's buffered defense input and immediate visual preview.
/// </summary>
public interface IDefenseInputSource
{
    bool TryConsumeBufferedDefenseInput(out DefenseInput input, out float inputTime);
    void PreviewDefenseInput(DefenseInput input);
}

/// <summary>선택적인 유지 입력 공급자. 미구현 시 기본 플레이어 입력을 사용합니다.</summary>
public interface ITimedGuardInputSource : IDefenseInputSource
{
    bool IsGuardHeld { get; }
}

/// <summary>대상별 Z/X/C 공통 재준비. 일반 홀드 방어는 제한하지 않습니다.</summary>
public interface IParryRearmInputSource : ITimedGuardInputSource
{
    void ConfigureParryRearm(float interval);
    bool TryAcceptPerfectGuardPress(float inputTime);
}

/// <summary>첫 입력과 동시 입력을 유실 없이 전달합니다. 기존 입력 공급자는 호환 인터페이스를 유지합니다.</summary>
public interface IActiveDefenseAttemptSource : IParryRearmInputSource
{
    bool TryConsumeBufferedDefenseAttempt(out DefenseInputReadStatus status, out DefenseInput input,
        out float inputTime, out bool allowsPrecision);
}
