using NUnit.Framework;
using UnityEngine;

public sealed class BunnySlimeLabVisualBuilderTests
{
    private GameObject _clone;
    private EnemyData _sample;
    private SkillData _skill;

    [SetUp]
    public void SetUp()
    {
        _sample = ScriptableObject.CreateInstance<EnemyData>();
        _skill = ScriptableObject.CreateInstance<SkillData>();
        _sample.SkillList.Add(_skill);
        _clone = new GameObject("Bunny clone with real Overworld dependency");
        _clone.SetActive(false);
        _clone.AddComponent<SpriteRenderer>();
        _clone.AddComponent<Animator>();
        _clone.AddComponent<BoxCollider2D>();
        _clone.AddComponent<Rigidbody2D>();
        _clone.AddComponent<BunnySlimeCharacter>();
        // 실제 prefab과 동일한 RequireComponent(EnemyCharacter) 관계를 사용합니다.
        _clone.AddComponent<OverworldEnemy>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_clone);
        Object.DestroyImmediate(_skill);
        Object.DestroyImmediate(_sample);
    }

    [Test]
    public void ConfigureCombatComponents_RemovesDependencyBeforeReplacingPassiveAi()
    {
        SpriteRenderer visual = _clone.GetComponent<SpriteRenderer>();
        Animator animator = _clone.GetComponent<Animator>();

        BunnySlimeShowcaseEnemy showcase = BunnySlimeLabVisualBuilder.ConfigureCombatComponents(_clone, _sample);

        Assert.That(_clone.GetComponent<OverworldEnemy>(), Is.Null);
        Assert.That(_clone.GetComponent<BunnySlimeCharacter>(), Is.Null);
        Assert.That(_clone.GetComponents<EnemyCharacter>(), Has.Length.EqualTo(1));
        // BattleManager가 사용하는 첫 GetComponent 호출에서도 공격 AI가 선택돼야 합니다.
        EnemyCharacter selectedByBattle = _clone.GetComponent<EnemyCharacter>();
        Assert.That(selectedByBattle, Is.SameAs(showcase));
        Assert.That(showcase.Data, Is.SameAs(_sample));
        showcase.Setup(_sample);
        Assert.That(selectedByBattle.DecideAction(), Is.EqualTo(EnemyAction.UseSkill));
        Assert.That(selectedByBattle.SelectSkill(EnemyAction.UseSkill), Is.SameAs(_skill));
        Assert.That(_clone.GetComponent<SpriteRenderer>(), Is.SameAs(visual));
        Assert.That(_clone.GetComponent<Animator>(), Is.SameAs(animator));
    }

    [Test]
    public void ConfigureCombatComponents_RepairsDuplicateLegacyAndShowcaseComponents()
    {
        BunnySlimeShowcaseEnemy existing = _clone.AddComponent<BunnySlimeShowcaseEnemy>();
        existing.Data = _sample;
        Assert.That(_clone.GetComponents<EnemyCharacter>(), Has.Length.EqualTo(2));

        BunnySlimeShowcaseEnemy repaired = BunnySlimeLabVisualBuilder.ConfigureCombatComponents(_clone, _sample);

        Assert.That(_clone.GetComponents<EnemyCharacter>(), Has.Length.EqualTo(1));
        Assert.That(_clone.GetComponent<EnemyCharacter>(), Is.SameAs(repaired));
        Assert.That(_clone.GetComponent<BunnySlimeCharacter>(), Is.Null);
        Assert.That(_clone.GetComponent<OverworldEnemy>(), Is.Null);
    }

    [Test]
    public void ConfigureCombatComponents_RepeatedConfigurationDoesNotAddAnotherEnemy()
    {
        BunnySlimeShowcaseEnemy first = BunnySlimeLabVisualBuilder.ConfigureCombatComponents(_clone, _sample);
        BunnySlimeShowcaseEnemy second = BunnySlimeLabVisualBuilder.ConfigureCombatComponents(_clone, _sample);

        Assert.That(second, Is.SameAs(first));
        Assert.That(_clone.GetComponents<EnemyCharacter>(), Has.Length.EqualTo(1));
        Assert.That(second.Data, Is.SameAs(_sample));
    }
}
