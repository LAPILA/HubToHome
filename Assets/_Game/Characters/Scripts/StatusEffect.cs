using UnityEngine;

/// <summary>
/// 버프 및 디버프의 기본이 되는 추상 클래스.
/// </summary>
public abstract class StatusEffect
{
    public string EffectID { get; protected set; }
    public int DurationTurns { get; protected set; }
    public bool IsExpired => DurationTurns <= 0;
    
    protected GameObject LoopVFXPrefab;
    protected string PivotName;

    public StatusEffect(string id, int duration, GameObject vfxPrefab = null, string pivot = "Pivots/Bottom")
    {
        EffectID = id;
        DurationTurns = duration;
        LoopVFXPrefab = vfxPrefab;
        PivotName = pivot;
    }

    public void RefreshDuration(int turns)
    {
        DurationTurns = Mathf.Max(DurationTurns, turns);
    }

    /// <summary>처음 부여될 때 실행 (스탯 증가, VFX 켜기 등)</summary>
    public virtual void OnApply(CharacterBase target)
    {
        if (LoopVFXPrefab != null)
            target.AddLoopVFX(EffectID, LoopVFXPrefab, PivotName);
    }

    /// <summary>매 턴마다 실행 (지속시간 감소, 도트 데미지 등)</summary>
    public virtual void OnTick(CharacterBase target)
    {
        DurationTurns--;
    }

    /// <summary>해제될 때 실행 (스탯 원상복구, VFX 끄기 등)</summary>
    public virtual void OnRemove(CharacterBase target)
    {
        if (LoopVFXPrefab != null)
            target.RemoveLoopVFX(EffectID);
    }
}