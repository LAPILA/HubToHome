using System.Text;
using UnityEngine;

/// <summary>누적 상태의 한국어 표식/색 계약. 계산이나 상태 변경은 수행하지 않습니다.</summary>
public static class ElementalStatusPresentation
{
    public const string FrozenColorHex = "B2E9FF";
    private static readonly ElementalStatusKind[] Kinds =
    {
        ElementalStatusKind.Burn, ElementalStatusKind.Chill, ElementalStatusKind.Electric,
        ElementalStatusKind.Bleed, ElementalStatusKind.Poison
    };

    public static int GetEntryCount(ElementalStatusState state)
    {
        if (state == null) return 0;
        int count = state.IsFrozen ? 1 : 0;
        for (int i = 0; i < Kinds.Length; i++) if (state.GetValue(Kinds[i]) > 0) count++;
        return count;
    }

    public static bool TryGetEntry(ElementalStatusState state, int index, out ElementalStatusKind kind,
        out int value, out bool frozen)
    {
        kind = default;
        value = 0;
        frozen = false;
        if (state == null || index < 0) return false;
        if (state.IsFrozen)
        {
            if (index == 0) { frozen = true; value = 1; return true; }
            index--;
        }
        for (int i = 0; i < Kinds.Length; i++)
        {
            int candidate = state.GetValue(Kinds[i]);
            if (candidate <= 0) continue;
            if (index-- != 0) continue;
            kind = Kinds[i];
            value = candidate;
            return true;
        }
        return false;
    }

    public static string FormatDetails(CharacterBase actor)
    {
        if (actor == null) return string.Empty;
        string actorName = actor is PlayerCharacter player ? player.DisplayName
            : actor is EnemyCharacter enemy && enemy.Data != null ? enemy.Data.EnemyName : "대상";
        var builder = new StringBuilder(256);
        builder.Append("<size=10><noparse>").Append(actorName).Append("</noparse> · 상태\n");
        int count = GetEntryCount(actor.Elements);
        for (int i = 0; i < count; i++)
        {
            TryGetEntry(actor.Elements, i, out ElementalStatusKind kind, out int value, out bool frozen);
            builder.Append("<color=#").Append(frozen ? FrozenColorHex : GetColorHex(kind)).Append('>')
                .Append(frozen ? "빙결" : GetLabel(kind)).Append(' ').Append(value).Append("</color>\n");
        }
        if (count == 0) builder.Append("누적 상태 없음\n");
        if (actor.ActiveStatusEffects.Count > 0) builder.Append("일반 효과 ").Append(actor.ActiveStatusEffects.Count);
        builder.Append("</size>");
        return builder.ToString();
    }

    public static string GetLabel(ElementalStatusKind kind) => kind switch
    {
        ElementalStatusKind.Burn => "화상",
        ElementalStatusKind.Chill => "냉기",
        ElementalStatusKind.Electric => "감전",
        ElementalStatusKind.Bleed => "출혈",
        ElementalStatusKind.Poison => "중독",
        _ => "상태"
    };

    public static string GetShortLabel(ElementalStatusKind kind) => kind switch
    {
        ElementalStatusKind.Burn => "화",
        ElementalStatusKind.Chill => "냉",
        ElementalStatusKind.Electric => "전",
        ElementalStatusKind.Bleed => "혈",
        ElementalStatusKind.Poison => "독",
        _ => "?"
    };

    public static string GetColorHex(ElementalStatusKind kind) => kind switch
    {
        ElementalStatusKind.Burn => "FF9856",
        ElementalStatusKind.Chill => "72D5FF",
        ElementalStatusKind.Electric => "FFE566",
        ElementalStatusKind.Bleed => "FF6A85",
        ElementalStatusKind.Poison => "A6DE65",
        _ => "FFFFFF"
    };

    public static Color GetColor(ElementalStatusKind kind)
    {
        ColorUtility.TryParseHtmlString("#" + GetColorHex(kind), out Color color);
        return color;
    }

    public static string FormatKeyword(ElementalStatusKind kind) =>
        $"<color=#{GetColorHex(kind)}>{GetLabel(kind)}</color>";

    public static string FormatCompact(ElementalStatusState state)
    {
        if (state == null) return string.Empty;
        var builder = new StringBuilder(192);
        Append(builder, state, ElementalStatusKind.Burn);
        Append(builder, state, ElementalStatusKind.Chill);
        Append(builder, state, ElementalStatusKind.Electric);
        Append(builder, state, ElementalStatusKind.Bleed);
        Append(builder, state, ElementalStatusKind.Poison);
        if (state.IsFrozen)
        {
            if (builder.Length > 0) builder.Append(' ');
            builder.Append("<color=#").Append(FrozenColorHex).Append(">빙1</color>");
        }
        return builder.ToString();
    }

    private static void Append(StringBuilder builder, ElementalStatusState state, ElementalStatusKind kind)
    {
        int value = state.GetValue(kind);
        if (value <= 0) return;
        if (builder.Length > 0) builder.Append(' ');
        builder.Append("<color=#").Append(GetColorHex(kind)).Append('>')
            .Append(GetShortLabel(kind)).Append(value).Append("</color>");
    }
}
