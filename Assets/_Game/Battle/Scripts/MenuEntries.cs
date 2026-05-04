using UnityEngine;

/// <summary>
/// Data(ScriptableObject)를 UI에 띄우기 위해 규격을 맞추는 Adapter 인터페이스.
/// </summary>
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
    public string DisplayName => Data?.SkillName ?? "???";
    public string Description => Data != null ? $"MP {Data.MPCost}  {Data.Description}" : "";
    public Sprite Icon => Data?.Icon;

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
    public string Description => Data?.Description ?? "";
    public Sprite Icon => Data?.Icon;

    public TargetAreaType TargetType => Data?.TargetType ?? TargetAreaType.AllyOnly;
    public bool IsAoE => Data?.IsAoE ?? false;

    public ItemMenuEntry(ItemData data, int count = 1)
    {
        Data = data;
        Count = count;
    }
}