using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class BattleCombatLogFormatterTests
{
    private static readonly FieldInfo InstanceField = typeof(LocalizationManager)
        .GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
    private readonly List<Object> _owned = new List<Object>();
    private LocalizationManager _previousLocalization;
    private LocalizationManager _localization;
    private Dictionary<string, Dictionary<LanguageType, string>> _texts;

    [SetUp]
    public void SetUp()
    {
        _previousLocalization = LocalizationManager.Instance;
        var root = Own(new GameObject("Combat log localization test"));
        root.SetActive(false);
        _localization = root.AddComponent<LocalizationManager>();
        _texts = new Dictionary<string, Dictionary<LanguageType, string>>();
        TextAsset table = Resources.Load<TextAsset>("LocalizationTable");
        Assert.That(table, Is.Not.Null);
        List<string[]> rows = LocalizationCsv.Parse(table.text);
        var logKeys = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 1; i < rows.Count; i++)
        {
            string[] row = rows[i];
            if (row.Length < 5) continue;
            string key = row[0].Trim();
            if (key.StartsWith("battle.log.", StringComparison.Ordinal))
            {
                Assert.That(logKeys.Add(key), Is.True, "새 전투 로그 번역 키는 중복될 수 없습니다: " + key);
                for (int language = 1; language <= 4; language++)
                    Assert.That(row[language], Is.Not.Empty, key + "/" + language);
            }
            _texts[key] = new Dictionary<LanguageType, string>
            {
                [LanguageType.KR] = row[1], [LanguageType.EN] = row[2],
                [LanguageType.JP] = row[3], [LanguageType.CN] = row[4]
            };
        }
        Assert.That(logKeys.Count, Is.EqualTo(30));
        typeof(LocalizationManager).GetField("_localizedText", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(_localization, _texts);
        InstanceField.SetValue(null, _localization);
        Language(LanguageType.KR);
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = _owned.Count - 1; i >= 0; i--)
            if (_owned[i] != null) Object.DestroyImmediate(_owned[i]);
        _owned.Clear();
        InstanceField.SetValue(null, _previousLocalization);
    }

    [TestCase(LanguageType.KR, "손실", "회복", "화상")]
    [TestCase(LanguageType.EN, "Lost", "Recovered", "Burn")]
    [TestCase(LanguageType.JP, "減少", "回復", "火傷")]
    [TestCase(LanguageType.CN, "失去", "恢复", "灼烧")]
    public void ResourceAndStatusResultsUseTheCurrentLanguage(LanguageType language,
        string loss, string gain, string burn)
    {
        Language(language);
        string text = BattleCombatLogFormatter.Result("WIZZEL", 20, 9, 3, 5,
            new[] { BattleCombatLogFormatter.ElementStatus(ElementalStatusKind.Burn, false, true) });
        Assert.That(text, Does.Contain("WIZZEL").And.Contain("20").And.Contain("9")
            .And.Contain("3").And.Contain("5").And.Contain(loss).And.Contain(gain).And.Contain(burn));
    }

    [Test]
    public void LossAndRecoveryAreNotNettedAndEmptyRowsAreOmitted()
    {
        string text = BattleCombatLogFormatter.Result("위젤", 20, 20, 4, 4, null);
        Assert.That(text, Does.Contain("손실").And.Contain("회복").And.Contain("AP"));
        Assert.That(BattleCombatLogFormatter.Result("위젤", 0, 0, 0, 0, new[] { "", " " }), Is.Empty);
        Assert.That(BattleCombatLogFormatter.Result("위젤", -1, -2, -3, -4, null), Is.Empty);
        Assert.That(BattleCombatLogFormatter.Result("위젤", 0, 0, 0, 0,
            new[] { BattleCombatLogFormatter.LegacyStatus(StatusEffectIds.Stun, true) }), Does.Contain("기절"));
    }

    [Test]
    public void ActionUsesDisplayedActorSkillAndItemNames()
    {
        EnemyCharacter actor = Enemy("실제 적 이름");
        SkillData skill = Own(ScriptableObject.CreateInstance<SkillData>());
        skill.SkillName = "얼음 베기";
        ItemData item = Own(ScriptableObject.CreateInstance<ItemData>());
        item.ItemName = "허브티";
        Assert.That(BattleCombatLogFormatter.Action(actor), Does.Contain("실제 적 이름").And.Contain("공격"));
        Assert.That(BattleCombatLogFormatter.Action(actor, skill), Does.Contain("얼음 베기"));
        Assert.That(BattleCombatLogFormatter.Action(actor, item: item), Does.Contain("허브티").And.Contain("아이템"));
        Assert.That(BattleCombatLogFormatter.Action(null), Is.Empty);
    }

    [Test]
    public void NamesAndUnknownStatusIdsCannotEscapeNoParse()
    {
        const string closing = "</noparse><color=red>이름</color>";
        string text = BattleCombatLogFormatter.Result(closing, 1, 0, 0, 0, null);
        Assert.That(text, Does.Contain("<\u200B/noparse>").And.Contain("<\u200Bcolor=red>"));
        Assert.That(text, Does.Not.Contain("</noparse><color=red>"));
        Assert.That(BattleCombatLogFormatter.LegacyStatus(closing, true),
            Does.Contain("<\u200B/noparse>").And.Contain("부여"));
        Assert.That(BattleCombatLogFormatter.LegacyStatus(null, false), Does.Contain("상태").And.Contain("해제"));
        Assert.DoesNotThrow(() => BattleCombatLogFormatter.ElementStatus((ElementalStatusKind)99, false, true));
    }

    [TestCase(StatusEffectIds.Burn, "화상")]
    [TestCase(StatusEffectIds.Freeze, "냉각")]
    [TestCase(StatusEffectIds.Bleed, "출혈")]
    [TestCase(StatusEffectIds.Poison, "중독")]
    [TestCase(StatusEffectIds.Bind, "속박")]
    [TestCase(StatusEffectIds.Stun, "기절")]
    [TestCase(StatusEffectIds.Berserk, "광폭")]
    [TestCase(StatusEffectIds.IceShield, "얼음 방패")]
    [TestCase(StatusEffectIds.Wet, "젖음")]
    [TestCase(StatusEffectIds.Haste, "가속")]
    public void LegacyStatusesHaveReadableAddedAndRemovedLabels(string id, string label)
    {
        Assert.That(BattleCombatLogFormatter.LegacyStatus(id.ToUpperInvariant(), true),
            Does.Contain(label).And.Contain("부여"));
        Assert.That(BattleCombatLogFormatter.LegacyStatus(id, false), Does.Contain(label).And.Contain("해제"));
    }

    [Test]
    public void CannotActExplainsOnlyTheGivenTurnRestrictionAndMalformedTranslationsFallBack()
    {
        EnemyCharacter actor = Enemy("위젤");
        Assert.That(BattleCombatLogFormatter.CannotAct(actor, true, false), Does.Contain("빙결").And.Contain("행동"));
        Assert.That(BattleCombatLogFormatter.CannotAct(actor, false, true), Does.Contain("기절").And.Contain("행동"));
        Assert.That(BattleCombatLogFormatter.CannotAct(null, true, true), Is.Empty);
        _texts["battle.log.result.row"][LanguageType.KR] = "{8}";
        Assert.That(BattleCombatLogFormatter.Result("위젤", 3, 0, 0, 0, null), Does.Contain("위젤").And.Contain("3"));
    }

    [Test]
    public void StatusRemovalQueriesIgnoreInitialBindingAndNumericChanges()
    {
        EnemyCharacter actor = Enemy("상태 대상");
        var tracker = new BattleStatusTransitionTracker();
        actor.Elements.Add(ElementalStatusKind.Burn, 5);
        tracker.Capture(actor.Elements);
        Assert.That(tracker.HasAdded || tracker.HasRemoved, Is.False);
        actor.Elements.Consume(ElementalStatusKind.Burn, 1);
        tracker.Capture(actor.Elements);
        Assert.That(tracker.HasAdded || tracker.HasRemoved, Is.False);
        actor.Elements.Cleanse(ElementalStatusKind.Burn);
        tracker.Capture(actor.Elements);
        Assert.That(tracker.IsRemovedElement(ElementalStatusKind.Burn, false), Is.True);
        Assert.That(tracker.IsNewElement(ElementalStatusKind.Burn, false), Is.False);
        tracker.Capture(actor.Elements);
        Assert.That(tracker.IsRemovedElement(ElementalStatusKind.Burn, false), Is.False);
    }

    [Test]
    public void FrozenConversionAndReleaseKeepIndependentTransitionKeys()
    {
        EnemyCharacter actor = Enemy("빙결 대상");
        var tracker = new BattleStatusTransitionTracker();
        actor.Elements.Add(ElementalStatusKind.Chill, 9);
        tracker.Capture(actor.Elements);
        actor.Elements.Add(ElementalStatusKind.Chill, 1);
        tracker.Capture(actor.Elements);
        Assert.That(tracker.IsRemovedElement(ElementalStatusKind.Chill, false), Is.True);
        Assert.That(tracker.IsNewElement(ElementalStatusKind.Chill, true), Is.True);
        actor.Elements.BeginTurn();
        tracker.Capture(actor.Elements);
        Assert.That(tracker.IsRemovedElement(ElementalStatusKind.Chill, true), Is.True);
        Assert.That(tracker.IsRemovedElement(ElementalStatusKind.Chill, false), Is.False);
    }

    [Test]
    public void LegacyRemovalIsCaseInsensitiveAndResetDoesNotReplayTransitions()
    {
        var tracker = new BattleStatusTransitionTracker();
        tracker.Capture(null, new[] { new StunEffect(2) });
        tracker.Capture(null, new[] { new StunEffect(3) });
        Assert.That(tracker.HasAdded || tracker.HasRemoved, Is.False);
        tracker.Capture(null);
        Assert.That(tracker.IsRemovedLegacy("sTuN"), Is.True);
        tracker.Reset();
        tracker.Capture(null, new[] { new StunEffect(1) });
        Assert.That(tracker.HasAdded || tracker.HasRemoved, Is.False);
        Assert.That(tracker.IsRemovedLegacy("Stun"), Is.False);
    }

    private void Language(LanguageType language) => typeof(LocalizationManager)
        .GetField("<CurrentLanguage>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
        .SetValue(_localization, language);

    private EnemyCharacter Enemy(string name)
    {
        var root = Own(new GameObject("Internal object name"));
        root.SetActive(false);
        EnemyCharacter actor = root.AddComponent<EnemyCharacter>();
        EnemyData data = Own(ScriptableObject.CreateInstance<EnemyData>());
        data.EnemyName = name;
        actor.Setup(data);
        return actor;
    }

    private T Own<T>(T value) where T : Object { _owned.Add(value); return value; }
}
