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
            2);

        Assert.That(result, Is.EqualTo(new CharacterBase[] { current, next }));
    }

    [Test]
    public void BuildVisiblePreservesRepeatedActorsFromRealSchedule()
    {
        PlayerCharacter slow = CreatePlayer("Slow", 5);
        EnemyCharacter fast = CreateEnemy("Fast", 20);

        List<CharacterBase> result = BattleTurnQueueProjection.BuildVisible(
            new CharacterBase[] { fast, fast, fast, slow },
            0,
            4);

        Assert.That(result, Is.EqualTo(new CharacterBase[] { fast, fast, fast, slow }));
    }

    [Test]
    public void BuildVisibleNeverInventsFutureTurnsWhenScheduleIsShort()
    {
        PlayerCharacter actor = CreatePlayer("Only", 10);
        Assert.That(BattleTurnQueueProjection.BuildVisible(new[] { actor }, 0, 6),
            Is.EqualTo(new[] { actor }));
    }

    [Test]
    public void BuildVisibleReturnsEmptyForNonPositiveVisibleCount()
    {
        List<CharacterBase> result = BattleTurnQueueProjection.BuildVisible(
            new CharacterBase[0],
            0,
            0);

        Assert.That(result, Is.Empty);
    }

    private PlayerCharacter CreatePlayer(string name, int speed)
    {
        var gameObject = new GameObject(name);
        _objects.Add(gameObject);
        PlayerCharacter character = gameObject.AddComponent<PlayerCharacter>();
        StatBlock stats = character.Stats.ProgressedBaseStats.Clone();
        stats.SPD = speed;
        character.Stats.SetProgressedBaseStats(stats);
        character.HealHP(character.MaxHP);
        return character;
    }

    private EnemyCharacter CreateEnemy(string name, int speed)
    {
        var gameObject = new GameObject(name);
        _objects.Add(gameObject);
        EnemyCharacter character = gameObject.AddComponent<EnemyCharacter>();
        StatBlock stats = character.Stats.ProgressedBaseStats.Clone();
        stats.SPD = speed;
        character.Stats.SetProgressedBaseStats(stats);
        character.HealHP(character.MaxHP);
        return character;
    }
}

