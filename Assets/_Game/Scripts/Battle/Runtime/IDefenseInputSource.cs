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
