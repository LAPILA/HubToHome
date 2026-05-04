using UnityEngine;

public interface IMenuEntry
{
    string DisplayName { get; }
    string Description { get; }
    Sprite Icon { get; }
}

public class SkillMenuEntry : IMenuEntry
{
    public SkillData Data { get; }
    public bool IsAoE { get; }

    public string DisplayName => Data != null ? Data.SkillName : "???";
    public string Description => Data != null ? $"MP {Data.MPCost}  {Data.Description}" : "";
    public Sprite Icon => Data != null ? Data.Icon : null;

    public SkillMenuEntry(SkillData data, bool isAoE = false)
    {
        Data = data;
        IsAoE = isAoE;
    }
}

public class ItemMenuEntry : IMenuEntry
{
    public ItemData Data { get; }
    public int Count { get; }

    public string DisplayName => Data != null ? (Count > 1 ? $"{Data.ItemName}  x{Count}" : Data.ItemName) : "???";
    public string Description => Data != null ? Data.Description : "";
    public Sprite Icon => Data != null ? Data.Icon : null;

    // 타겟팅 시스템을 위한 위임 속성
    public TargetAreaType TargetType => Data != null ? Data.TargetType : TargetAreaType.AllyOnly;
    public bool IsAoE => Data != null && Data.IsAoE;

    public ItemMenuEntry(ItemData data, int count = 1)
    {
        Data = data;
        Count = count;
    }
}