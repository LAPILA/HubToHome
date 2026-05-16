using UnityEngine;

public static class BattleNarrationFormatter
{
    private const string DamageColor = "#FF4A4A";
    private const string HealColor = "#45E06F";

    public static BattleNarrationConfig Config { get; set; }

    public static string ActorName(CharacterBase actor)
    {
        return actor switch
        {
            PlayerCharacter player => player.DisplayName,
            EnemyCharacter enemy => enemy.Data != null && !string.IsNullOrWhiteSpace(enemy.Data.EnemyName) ? enemy.Data.EnemyName : enemy.name,
            _ => actor != null ? actor.name : "???"
        };
    }

    public static BattleNarrationMessage BattleStart() => Build(BattleNarrationEventType.BattleStart, new TokenMap());
    public static BattleNarrationMessage PlayerTurn(PlayerCharacter actor) => Build(BattleNarrationEventType.PlayerTurnStart, new TokenMap().Add("actor", ActorName(actor)), fallbackText: $"{ActorName(actor)}의 턴.", fallbackHold: 0.25f, fallbackPriority: BattleNarrationPriority.Low);
    public static BattleNarrationMessage PlayerAttack(PlayerCharacter actor) => Build(BattleNarrationEventType.PlayerAttack, new TokenMap().Add("actor", ActorName(actor)), fallbackText: $"{ActorName(actor)}가 일반 공격을 수행했다!", fallbackHold: 0.15f);
    public static BattleNarrationMessage SkillUse(PlayerCharacter actor, SkillData skill) => Build(BattleNarrationEventType.PlayerSkillUse, new TokenMap().Add("actor", ActorName(actor)).Add("skill", SafeSkillName(skill)), fallbackText: $"{ActorName(actor)}가 {SafeSkillName(skill)}을 사용했다!", fallbackHold: 0.15f);
    public static BattleNarrationMessage ItemUse(PlayerCharacter actor, ItemData item) => Build(BattleNarrationEventType.PlayerItemUse, new TokenMap().Add("actor", ActorName(actor)).Add("item", SafeItemName(item)), fallbackText: $"{ActorName(actor)}가 {SafeItemName(item)}을 사용했다!", fallbackHold: 0.15f);

    public static BattleNarrationMessage EnemyAction(EnemyCharacter enemy, EnemyAction action, EnemyAttackType attackType, SkillData skill)
    {
        string name = ActorName(enemy);
        if (action == global::EnemyAction.UseSkill && skill != null)
            return Build(BattleNarrationEventType.EnemySkillPrepare, new TokenMap().Add("enemy", name).Add("skill", SafeSkillName(skill)), fallbackText: $"{name}가 {SafeSkillName(skill)}을 준비한다...", fallbackStyle: BattleNarrationStyle.Warning, fallbackPriority: BattleNarrationPriority.High, fallbackHold: 0.2f);

        if (action == global::EnemyAction.EnragedAttack || attackType == EnemyAttackType.AoEAll)
            return Build(BattleNarrationEventType.EnemyStrongAttackPrepare, new TokenMap().Add("enemy", name), fallbackText: $"{name}가 강한 공격을 준비한다!", fallbackStyle: BattleNarrationStyle.Warning, fallbackPriority: BattleNarrationPriority.High, fallbackHold: 0.2f);

        return Build(BattleNarrationEventType.EnemyBasicAttack, new TokenMap().Add("enemy", name), fallbackText: $"{name}가 일반 공격을 수행한다!", fallbackHold: 0.2f);
    }

    public static BattleNarrationMessage Damage(CharacterBase target, int amount)
    {
        string colored = $"<color={DamageColor}>{Mathf.Abs(amount)}</color>";
        return Build(BattleNarrationEventType.DamageTaken, new TokenMap().Add("target", ActorName(target)).Add("value", colored), fallbackText: $"{ActorName(target)}에게 {colored}의 피해!", fallbackStyle: BattleNarrationStyle.Damage, fallbackHold: 0.1f);
    }

    public static BattleNarrationMessage Heal(CharacterBase target, int amount)
    {
        string colored = $"<color={HealColor}>{Mathf.Abs(amount)}</color>";
        return Build(BattleNarrationEventType.HealReceived, new TokenMap().Add("target", ActorName(target)).Add("value", colored), fallbackText: $"{ActorName(target)}가 {colored} 회복했다!", fallbackStyle: BattleNarrationStyle.Heal, fallbackHold: 0.1f);
    }

    public static BattleNarrationMessage Victory() => Build(BattleNarrationEventType.Victory, new TokenMap(), fallbackText: "승리했다!", fallbackStyle: BattleNarrationStyle.System, fallbackPriority: BattleNarrationPriority.Critical, fallbackHold: 1.4f);
    public static BattleNarrationMessage Defeat() => Build(BattleNarrationEventType.Defeat, new TokenMap(), fallbackText: "패배했다...", fallbackStyle: BattleNarrationStyle.System, fallbackPriority: BattleNarrationPriority.Critical, fallbackHold: 1.4f);

    public static BattleNarrationMessage Flavor(string text, BattleNarrationStyle style, BattleNarrationPriority priority, float hold)
    {
        return new BattleNarrationMessage(text, style, priority, hold, requiresConfirm: true);
    }

    private static BattleNarrationMessage Build(BattleNarrationEventType type, TokenMap tokens, string fallbackText = "", BattleNarrationStyle fallbackStyle = BattleNarrationStyle.Normal, BattleNarrationPriority fallbackPriority = BattleNarrationPriority.Normal, float fallbackHold = -1f)
    {
        BattleNarrationTemplate template = Config != null ? Config.GetTemplate(type) : null;
        if (template != null)
        {
            return new BattleNarrationMessage(tokens.Apply(template.Template), template.Style, template.Priority, template.HoldOverride, requiresConfirm: false);
        }

        return new BattleNarrationMessage(tokens.Apply(fallbackText), fallbackStyle, fallbackPriority, fallbackHold, requiresConfirm: false);
    }

    public readonly struct TokenMap
    {
        private readonly System.Collections.Generic.Dictionary<string, string> _tokens;

        public TokenMap Add(string key, string value)
        {
            var next = _tokens != null ? new System.Collections.Generic.Dictionary<string, string>(_tokens) : new System.Collections.Generic.Dictionary<string, string>();
            next[key] = value ?? string.Empty;
            return new TokenMap(next);
        }

        private TokenMap(System.Collections.Generic.Dictionary<string, string> tokens)
        {
            _tokens = tokens;
        }

        public string Apply(string source)
        {
            if (string.IsNullOrEmpty(source) || _tokens == null) return source;
            string result = source;
            foreach (var pair in _tokens)
                result = result.Replace("{" + pair.Key + "}", pair.Value ?? string.Empty);
            return result;
        }
    }

    private static string SafeSkillName(SkillData skill) => skill != null && !string.IsNullOrWhiteSpace(skill.SkillName) ? skill.SkillName : "스킬";
    private static string SafeItemName(ItemData item) => item != null && !string.IsNullOrWhiteSpace(item.ItemName) ? item.ItemName : "아이템";
}
