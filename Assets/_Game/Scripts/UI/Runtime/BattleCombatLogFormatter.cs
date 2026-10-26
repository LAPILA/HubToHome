using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

/// <summary>확정된 전투 결과의 표시만 담당합니다. 피해 계산이나 상태 변경은 수행하지 않습니다.</summary>
public static class BattleCombatLogFormatter
{
    private const string LossColor = "FF6A85";
    private const string GainColor = "7EDAAE";
    private const string ApColor = "FFE566";

    public static string Action(CharacterBase actor, SkillData skill = null, ItemData item = null)
    {
        if (actor == null) return string.Empty;
        string name = Plain(BattleNarrationFormatter.ActorName(actor), Text("actor.unknown", "대상"));
        if (skill != null)
            return Format("action.skill", "{0} — {1}", name,
                Plain(skill.LocalizedName, Text("skill.unknown", "스킬")));
        if (item != null)
            return Format("action.item", "{0} — 아이템: {1}", name,
                Plain(item.LocalizedName, Text("item.unknown", "아이템")));
        return Format("action.attack", "{0} — 공격", name);
    }

    public static string Result(string actorName, int hpLost, int hpGained, int apLost, int apGained,
        IReadOnlyList<string> statusChanges)
    {
        var resources = new StringBuilder(96);
        AppendAmount(resources, "result.hp_lost", "HP {0} 손실", hpLost, LossColor);
        AppendAmount(resources, "result.hp_gained", "HP {0} 회복", hpGained, GainColor);
        AppendAmount(resources, "result.ap_lost", "AP {0} 손실", apLost, ApColor);
        AppendAmount(resources, "result.ap_gained", "AP {0} 회복", apGained, ApColor);

        var changes = new StringBuilder(96);
        for (int i = 0; statusChanges != null && i < statusChanges.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(statusChanges[i])) continue;
            if (changes.Length > 0) changes.Append(" / ");
            // 이 목록은 ElementStatus/LegacyStatus가 만든 표시 문구입니다.
            changes.Append(statusChanges[i]);
        }
        if (resources.Length == 0 && changes.Length == 0) return string.Empty;
        if (resources.Length > 0 && changes.Length > 0) resources.Append(" · ");
        resources.Append(changes);
        return Format("result.row", "{0}: {1}",
            Plain(actorName, Text("actor.unknown", "대상")), resources.ToString());
    }

    public static string ElementStatus(ElementalStatusKind kind, bool frozen, bool added)
    {
        string label;
        string color;
        if (frozen)
        {
            label = Text("status.frozen", "빙결");
            color = ElementalStatusPresentation.FrozenColorHex;
        }
        else
        {
            switch (kind)
            {
                case ElementalStatusKind.Burn: label = Text("status.burn", "화상"); break;
                case ElementalStatusKind.Chill: label = Text("status.chill", "냉기"); break;
                case ElementalStatusKind.Electric: label = Text("status.electric", "감전"); break;
                case ElementalStatusKind.Bleed: label = Text("status.bleed", "출혈"); break;
                case ElementalStatusKind.Poison: label = Text("status.poison", "중독"); break;
                default: label = Text("status.unknown", "상태"); break;
            }
            color = ElementalStatusPresentation.GetColorHex(kind);
        }
        return StatusChange(label, color, added);
    }

    public static string LegacyStatus(string id, bool added)
    {
        string label;
        string color = "FFFFFF";
        switch (id?.ToLowerInvariant())
        {
            case "burn":
                label = Text("status.burn", "화상");
                color = ElementalStatusPresentation.GetColorHex(ElementalStatusKind.Burn);
                break;
            case "freeze":
                label = Text("status.legacy.freeze", "냉각");
                color = ElementalStatusPresentation.FrozenColorHex;
                break;
            case "bleed":
                label = Text("status.bleed", "출혈");
                color = ElementalStatusPresentation.GetColorHex(ElementalStatusKind.Bleed);
                break;
            case "poison":
                label = Text("status.poison", "중독");
                color = ElementalStatusPresentation.GetColorHex(ElementalStatusKind.Poison);
                break;
            case "bind": label = Text("status.bind", "속박"); break;
            case "stun": label = Text("status.stun", "기절"); break;
            case "berserk": label = Text("status.berserk", "광폭"); break;
            case "iceshield": label = Text("status.ice_shield", "얼음 방패"); break;
            case "wet": label = Text("status.wet", "젖음"); break;
            case "haste": label = Text("status.haste", "가속"); break;
            default: label = string.IsNullOrWhiteSpace(id) ? Text("status.unknown", "상태") : id; break;
        }
        return StatusChange(label, color, added);
    }

    public static string CannotAct(CharacterBase actor, bool frozen, bool stunned)
    {
        if (actor == null) return string.Empty;
        string name = Plain(BattleNarrationFormatter.ActorName(actor), Text("actor.unknown", "대상"));
        if (frozen) return Format("cannot.frozen", "{0}: 빙결로 행동 불가", name);
        if (stunned) return Format("cannot.stunned", "{0}: 기절로 행동 불가", name);
        return Format("cannot.other", "{0}: 행동할 수 없다", name);
    }

    public static string Recovery(CharacterBase actor)
    {
        if (actor == null) return string.Empty;
        return Format("cannot.recovery", "{0}: 탈진으로 한 턴 휴식",
            Plain(BattleNarrationFormatter.ActorName(actor), Text("actor.unknown", "대상")));
    }

    private static string StatusChange(string label, string color, bool added)
    {
        string colored = "<color=#" + color + ">" + Plain(label, Text("status.unknown", "상태")) + "</color>";
        return added ? Format("status.added", "{0} 부여", colored)
            : Format("status.removed", "{0} 해제", colored);
    }

    private static void AppendAmount(StringBuilder builder, string key, string fallback, int amount, string color)
    {
        if (amount <= 0) return;
        if (builder.Length > 0) builder.Append(" / ");
        string value = "<color=#" + color + ">" + amount.ToString(CultureInfo.InvariantCulture) + "</color>";
        builder.Append(Format(key, fallback, value));
    }

    private static string Text(string key, string fallback) => LocalizationManager.Localize("battle.log." + key, fallback);

    private static string Format(string key, string fallback, string first, string second = "")
    {
        string template = Text(key, fallback);
        try { return string.Format(CultureInfo.InvariantCulture, template, first, second); }
        catch (FormatException) { return string.Format(CultureInfo.InvariantCulture, fallback, first, second); }
    }

    private static string Plain(string value, string fallback)
    {
        string text = string.IsNullOrWhiteSpace(value) ? fallback : value;
        // noparse 종료 태그가 이름에 있어도 범위를 탈출하지 못하게 합니다. 삽입 문자는 화면에 보이지 않습니다.
        return "<noparse>" + text.Replace("<", "<\u200B").Replace('\r', ' ').Replace('\n', ' ') + "</noparse>";
    }
}
