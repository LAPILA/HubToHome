/// <summary>
/// Supplies one actor's buffered defense input and immediate visual preview.
/// </summary>
public interface IDefenseInputSource
{
    bool TryConsumeBufferedDefenseInput(out DefenseInput input, out float inputTime);
    void PreviewDefenseInput(DefenseInput input);
}
