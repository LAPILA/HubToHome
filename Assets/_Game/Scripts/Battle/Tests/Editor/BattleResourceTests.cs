using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public sealed class BattleResourceTests
{
    private readonly List<Object> _owned = new List<Object>();
    private T Own<T>(T value) where T : Object { _owned.Add(value); return value; }

    [TearDown]
    public void Cleanup()
    {
        for (int i = _owned.Count - 1; i >= 0; i--)
            if (_owned[i] != null) Object.DestroyImmediate(_owned[i]);
        _owned.Clear();
    }

    private BattleResourceDefinition Definition() => Own(ScriptableObject.CreateInstance<BattleResourceDefinition>());

    private PlayerCharacter Player(BattleResourceDefinition resource)
    {
        CharacterData data = Own(ScriptableObject.CreateInstance<CharacterData>());
        data.BattleResource = resource;
        data.BaseStats = new StatBlock { MaxHP = 100, MaxAP = 20 };
        GameObject go = Own(new GameObject("Resource test"));
        go.SetActive(false);
        PlayerCharacter player = go.AddComponent<PlayerCharacter>();
        player.SetCharacterData(data);
        return player;
    }

    private SkillData Skill(BattleResourceDefinition resource)
    {
        SkillData skill = Own(ScriptableObject.CreateInstance<SkillData>());
        skill.ActionTimeline = new List<SkillActionBlock> { new Action_Wait() };
        skill.ResourceEffect = new SkillResourceEffect { Resource = resource, EnableBoost = true,
            RequiredStep = 3, ConsumeStep = 2, DamageMultiplier = 1.5f, GainOnCompletion = 1 };
        return skill;
    }

    [Test]
    public void SharedDefinition_DoesNotShareRuntimePressure_AndClampsAtMaximum()
    {
        var definition = Definition();
        var first = new CharacterBattleResource();
        var second = new CharacterBattleResource();
        first.Configure(definition);
        second.Configure(definition);
        first.RewardPerfectParry();
        Assert.That(first.Step, Is.EqualTo(1));
        Assert.That(second.Step, Is.Zero);
        first.Gain(int.MaxValue);
        Assert.That(first.Step, Is.EqualTo(4));
        first.Reset();
        Assert.That(first.Step, Is.Zero);
        Assert.That(definition.InitialStep, Is.Zero);
    }

    [Test]
    public void Boost_SpendsConfiguredCostOnce_PreservesQteIndependentBonus_AndCompletesOnce()
    {
        var definition = Definition();
        PlayerCharacter player = Player(definition);
        SkillData skill = Skill(definition);
        player.BattleResource.Gain(4);
        var context = new SkillContext { Actor = player };
        context.BeginSkillResources(skill);
        context.BeginSkillResources(skill);
        Assert.That(player.BattleResource.Step, Is.EqualTo(2));
        context.CurrentDamageMultiplier = 2f;
        Assert.That(context.EffectiveDamageMultiplier, Is.EqualTo(3f));
        context.CurrentDamageMultiplier = 1f;
        Assert.That(context.EffectiveDamageMultiplier, Is.EqualTo(1.5f));
        context.CompleteSkillResources();
        context.CompleteSkillResources();
        Assert.That(player.BattleResource.Step, Is.EqualTo(3));
    }

    [Test]
    public void BelowThreshold_UsesBaseDamageWithoutSpending_AndStillGainsOnCompletion()
    {
        var definition = Definition();
        PlayerCharacter player = Player(definition);
        player.BattleResource.Gain(2);
        var context = new SkillContext { Actor = player };
        context.BeginSkillResources(Skill(definition));
        Assert.That(player.BattleResource.Step, Is.EqualTo(2));
        Assert.That(context.EffectiveDamageMultiplier, Is.EqualTo(1f));
        context.CompleteSkillResources();
        Assert.That(player.BattleResource.Step, Is.EqualTo(3));
    }

    [Test]
    public void Cancellation_DoesNotGiveCompletionGain_AndDoesNotRefundCommittedCost()
    {
        var definition = Definition();
        PlayerCharacter player = Player(definition);
        player.BattleResource.Gain(4);
        bool active = true;
        var context = new SkillContext { Actor = player, IsExecutionActive = () => active };
        context.BeginSkillResources(Skill(definition));
        active = false;
        context.CompleteSkillResources();
        Assert.That(player.BattleResource.Step, Is.EqualTo(2));
    }

    [Test]
    public void WrongResource_OrEmptyTimeline_DoesNotSpendOrGain()
    {
        var definition = Definition();
        PlayerCharacter player = Player(definition);
        player.BattleResource.Gain(4);
        SkillData skill = Skill(Definition());
        var context = new SkillContext { Actor = player };
        context.BeginSkillResources(skill);
        context.CompleteSkillResources();
        Assert.That(player.BattleResource.Step, Is.EqualTo(4));
        skill.ResourceEffect.Resource = definition;
        skill.ActionTimeline.Clear();
        context = new SkillContext { Actor = player };
        context.BeginSkillResources(skill);
        context.CompleteSkillResources();
        Assert.That(player.BattleResource.Step, Is.EqualTo(4));
        Assert.That(context.ResourceDamageMultiplier, Is.EqualTo(1));
    }

    [Test]
    public void View_RebindsWithoutKeepingPreviousActorsResource()
    {
        var definition = Definition();
        var texture = Own(new Texture2D(4, 4));
        var sprite = Own(Sprite.Create(texture, new Rect(0, 0, 4, 4), Vector2.zero));
        definition.Stages = new[] { new BattleResourceStage {
            Frames = new[] { new BattleResourceFrame { Sprite = sprite } } } };
        var state = new CharacterBattleResource();
        state.Configure(definition);
        var go = Own(new GameObject("Resource view", typeof(RectTransform), typeof(Image)));
        var image = go.GetComponent<Image>();
        var view = go.AddComponent<BattleResourceView>();
        view.Bind(image, state);
        Assert.That(image.sprite, Is.SameAs(sprite));
        view.Bind(image, null);
        state.Gain(1);
        Assert.That(image.enabled, Is.False);
        Assert.That(image.sprite, Is.Null);
    }

    [Test]
    public void WizelCounter_GainsOneOnlyForRecipient_AndClampsAtFour()
    {
        var definition = AssetDatabase.LoadAssetAtPath<BattleResourceDefinition>(
            "Assets/_Game/Content/Characters/BattleResources/WizelPressure.asset");
        Assert.That(definition, Is.Not.Null);
        Assert.That(definition.CounterGain, Is.EqualTo(1));
        var defender = new CharacterBattleResource();
        var other = new CharacterBattleResource();
        defender.Configure(definition);
        other.Configure(definition);
        defender.RewardCounter();
        Assert.That(defender.Step, Is.EqualTo(1));
        Assert.That(other.Step, Is.Zero);
        defender.Gain(3);
        defender.RewardCounter();
        Assert.That(defender.Step, Is.EqualTo(4));
    }

    [Test]
    public void WizelData_ContainsFiveValidImportedStages()
    {
        var definition = AssetDatabase.LoadAssetAtPath<BattleResourceDefinition>(
            "Assets/_Game/Content/Characters/BattleResources/WizelPressure.asset");
        Assert.That(definition, Is.Not.Null);
        Assert.That(definition.Stages.Length, Is.EqualTo(5));
        for (int i = 0; i <= 4; i++)
        {
            BattleResourceStage stage = definition.GetStage(i);
            Assert.That(stage.SourceClip, Is.Not.Null, "step" + i);
            Assert.That(stage.SourceClip.name, Is.EqualTo("step" + i));
            Assert.That(stage.Frames.Length, Is.GreaterThan(0));
            Assert.That(stage.Frames[0].Sprite, Is.Not.Null);
            bool matched = false;
            foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(stage.SourceClip))
            {
                if (binding.type != typeof(SpriteRenderer) || binding.propertyName != "m_Sprite") continue;
                var keys = AnimationUtility.GetObjectReferenceCurve(stage.SourceClip, binding);
                matched |= keys.Length > 0 && keys[0].value == stage.Frames[0].Sprite;
            }
            Assert.That(matched, Is.True, "Source clip/sprite mismatch: step" + i);
        }
    }
}
