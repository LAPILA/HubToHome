using System;

/// <summary>
/// 상태 이상 베이스 클래스. 독, 화상, 버프 등 모든 효과가 이를 상속합니다.
/// </summary>
[Serializable]
public abstract class StatusEffect
{
    public string EffectName  { get; protected set; }
    public int    Duration    { get; protected set; } // 남은 턴 수
    public bool   IsExpired   => Duration <= 0;

    protected StatusEffect(string name, int duration)
    {
        EffectName = name;
        Duration   = duration;
    }

    /// <summary>매 턴 호출됩니다. 효과를 적용하고 Duration을 감소시킵니다.</summary>
    public virtual void OnTick(CharacterBase target)
    {
        ApplyEffect(target);
        Duration--;
    }

    protected abstract void ApplyEffect(CharacterBase target);
}
