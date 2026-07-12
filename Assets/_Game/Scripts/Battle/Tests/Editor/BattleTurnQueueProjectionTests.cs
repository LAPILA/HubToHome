using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class BattleTurnQueueProjectionTests
{
    private readonly List<GameObject> _objects = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        for (int i = 0; i < _objects.Count; i++)
        {
            Object.DestroyImmediate(_objects[i]);
        }

        _objects.Clear();
    }

    [Test]
    public void BuildVisibleStartsAtCurrentActorAndSkipsDefeatedActors()
    {
        PlayerCharacter first = CreatePlayer("First", 10);
        PlayerCharacter defeated = CreatePlayer("Defeated", 20);
        defeated.TakePureDamage(defeated.MaxHP);
        EnemyCharacter current = CreateEnemy("Current", 15);
        EnemyCharacter next = CreateEnemy("Next", 5);

        var queue = new List<CharacterBase> { first, defeated, current, next };
        List<CharacterBase> result = BattleTurnQueueProjection.BuildVisible(
            queue,
            1,
            2,
            new[] { first, defeated },
            new[] { current, next });

        Assert.That(result, Is.EqualTo(new CharacterBase[] { current, next }));
    }

    [Test]
    public void BuildVisibleRefillsFutureRoundsBySpeed()
    {
        PlayerCharacter slow = CreatePlayer("Slow", 5);
        EnemyCharacter fast = CreateEnemy("Fast", 20);

        List<CharacterBase> result = BattleTurnQueueProjection.BuildVisible(
            new CharacterBase[] { slow },
            0,
            4,
            new[] { slow },
            new[] { fast });

        Assert.That(result, Is.EqualTo(new CharacterBase[] { slow, fast, slow, fast }));
    }

    [Test]
    public void BuildVisibleReturnsEmptyForNonPositiveVisibleCount()
    {
        List<CharacterBase> result = BattleTurnQueueProjection.BuildVisible(
            new CharacterBase[0],
            0,
            0,
            new PlayerCharacter[0],
            new EnemyCharacter[0]);

        Assert.That(result, Is.Empty);
    }

    private PlayerCharacter CreatePlayer(string name, int speed)
    {
        var gameObject = new GameObject(name);
        _objects.Add(gameObject);
        PlayerCharacter character = gameObject.AddComponent<PlayerCharacter>();
        character.BaseSPD = speed;
        character.HealHP(character.MaxHP);
        return character;
    }

    private EnemyCharacter CreateEnemy(string name, int speed)
    {
        var gameObject = new GameObject(name);
        _objects.Add(gameObject);
        EnemyCharacter character = gameObject.AddComponent<EnemyCharacter>();
        character.BaseSPD = speed;
        character.HealHP(character.MaxHP);
        return character;
    }
}

