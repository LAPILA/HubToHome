using NUnit.Framework;
using UnityEngine;

public class BattleDamageFeedbackTests
{
    [Test]
    public void CharacterDataUsesWhiteBattleSymbolColorByDefault()
    {
        CharacterData data = ScriptableObject.CreateInstance<CharacterData>();
        try
        {
            Assert.That(data.BattleSymbolColor, Is.EqualTo(Color.white));
        }
        finally
        {
            Object.DestroyImmediate(data);
        }
    }

    [Test]
    public void PlayerCharacterExposesAssignedBattleSymbolColor()
    {
        CharacterData data = ScriptableObject.CreateInstance<CharacterData>();
        var playerObject = new GameObject("Player");
        try
        {
            Color symbolColor = new Color(0.2f, 0.7f, 0.4f, 1f);
            data.BattleSymbolColor = symbolColor;
            PlayerCharacter player = playerObject.AddComponent<PlayerCharacter>();
            player.SetCharacterData(data);

            Assert.That(player.BattleSymbolColor, Is.EqualTo(symbolColor));
        }
        finally
        {
            Object.DestroyImmediate(playerObject);
            Object.DestroyImmediate(data);
        }
    }

    [Test]
    public void PlayerCharacterUsesWhiteWhenStoredSymbolColorIsTransparent()
    {
        CharacterData data = ScriptableObject.CreateInstance<CharacterData>();
        var playerObject = new GameObject("Player");
        try
        {
            data.BattleSymbolColor = Color.clear;
            PlayerCharacter player = playerObject.AddComponent<PlayerCharacter>();
            player.SetCharacterData(data);

            Assert.That(player.BattleSymbolColor, Is.EqualTo(Color.white));
        }
        finally
        {
            Object.DestroyImmediate(playerObject);
            Object.DestroyImmediate(data);
        }
    }

    [Test]
    public void DamageUsesSourcePlayerColorBeforeTargetPlayerColor()
    {
        CharacterData sourceData = ScriptableObject.CreateInstance<CharacterData>();
        CharacterData targetData = ScriptableObject.CreateInstance<CharacterData>();
        var sourceObject = new GameObject("Source");
        var targetObject = new GameObject("Target");
        try
        {
            sourceData.BattleSymbolColor = Color.cyan;
            targetData.BattleSymbolColor = Color.magenta;
            PlayerCharacter source = sourceObject.AddComponent<PlayerCharacter>();
            PlayerCharacter target = targetObject.AddComponent<PlayerCharacter>();
            source.SetCharacterData(sourceData);
            target.SetCharacterData(targetData);

            var feedback = new BattleDamageFeedback(
                source,
                target,
                42,
                true,
                BattleDamageFeedbackKind.Damage);

            Assert.That(feedback.Source, Is.SameAs(source));
            Assert.That(feedback.Target, Is.SameAs(target));
            Assert.That(feedback.Amount, Is.EqualTo(42));
            Assert.That(feedback.IsCritical, Is.True);
            Assert.That(feedback.Kind, Is.EqualTo(BattleDamageFeedbackKind.Damage));
            Assert.That(feedback.ResolveColor(), Is.EqualTo(Color.cyan));
        }
        finally
        {
            Object.DestroyImmediate(sourceObject);
            Object.DestroyImmediate(targetObject);
            Object.DestroyImmediate(sourceData);
            Object.DestroyImmediate(targetData);
        }
    }

    [Test]
    public void DamageUsesTargetPlayerColorWhenSourceIsNotAPlayer()
    {
        CharacterData targetData = ScriptableObject.CreateInstance<CharacterData>();
        var sourceObject = new GameObject("Enemy Source");
        var targetObject = new GameObject("Player Target");
        try
        {
            targetData.BattleSymbolColor = Color.yellow;
            EnemyCharacter source = sourceObject.AddComponent<EnemyCharacter>();
            PlayerCharacter target = targetObject.AddComponent<PlayerCharacter>();
            target.SetCharacterData(targetData);

            var feedback = new BattleDamageFeedback(
                source,
                target,
                17,
                false,
                BattleDamageFeedbackKind.Damage);

            Assert.That(feedback.ResolveColor(), Is.EqualTo(Color.yellow));
        }
        finally
        {
            Object.DestroyImmediate(sourceObject);
            Object.DestroyImmediate(targetObject);
            Object.DestroyImmediate(targetData);
        }
    }

    [Test]
    public void DamageUsesWhiteWhenNeitherParticipantIsAPlayer()
    {
        var sourceObject = new GameObject("Enemy Source");
        var targetObject = new GameObject("Enemy Target");
        try
        {
            EnemyCharacter source = sourceObject.AddComponent<EnemyCharacter>();
            EnemyCharacter target = targetObject.AddComponent<EnemyCharacter>();
            var feedback = new BattleDamageFeedback(
                source,
                target,
                9,
                false,
                BattleDamageFeedbackKind.Damage);

            Assert.That(feedback.ResolveColor(), Is.EqualTo(Color.white));
        }
        finally
        {
            Object.DestroyImmediate(sourceObject);
            Object.DestroyImmediate(targetObject);
        }
    }

    [Test]
    public void MissAlwaysUsesWhiteEvenWhenSourceIsAPlayer()
    {
        CharacterData sourceData = ScriptableObject.CreateInstance<CharacterData>();
        var sourceObject = new GameObject("Player Source");
        var targetObject = new GameObject("Enemy Target");
        try
        {
            sourceData.BattleSymbolColor = Color.red;
            PlayerCharacter source = sourceObject.AddComponent<PlayerCharacter>();
            EnemyCharacter target = targetObject.AddComponent<EnemyCharacter>();
            source.SetCharacterData(sourceData);
            var feedback = new BattleDamageFeedback(
                source,
                target,
                0,
                false,
                BattleDamageFeedbackKind.Miss);

            Assert.That(feedback.ResolveColor(), Is.EqualTo(Color.white));
        }
        finally
        {
            Object.DestroyImmediate(sourceObject);
            Object.DestroyImmediate(targetObject);
            Object.DestroyImmediate(sourceData);
        }
    }
}