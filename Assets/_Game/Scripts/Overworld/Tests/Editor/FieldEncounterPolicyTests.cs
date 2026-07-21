using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class FieldEncounterPolicyTests
{
    private readonly List<Object> _created = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        for (int i = 0; i < _created.Count; i++)
            if (_created[i] != null) Object.DestroyImmediate(_created[i]);
        _created.Clear();
    }

    [Test]
    public void FirstEncounterAlwaysStartsPreemptiveBattle()
    {
        EnemyData enemy = CreateEnemy(1, 5, true);
        FieldEncounterResolution result = FieldEncounterPolicy.Evaluate(
            99,
            new[] { enemy },
            false,
            true);
        Assert.That(result, Is.EqualTo(FieldEncounterResolution.PreemptiveBattle));
    }

    [Test]
    public void PreviouslyDefeatedLowThreatGroupCanBeInstantKilled()
    {
        EnemyData first = CreateEnemy(2, 5, true);
        EnemyData second = CreateEnemy(3, 5, true);
        FieldEncounterResolution result = FieldEncounterPolicy.Evaluate(
            8,
            new[] { first, second },
            true,
            true);
        Assert.That(result, Is.EqualTo(FieldEncounterResolution.InstantVictory));
    }

    [Test]
    public void OneProtectedEnemyPreventsGroupInstantKill()
    {
        EnemyData first = CreateEnemy(1, 0, true);
        EnemyData protectedEnemy = CreateEnemy(1, 0, false);
        FieldEncounterResolution result = FieldEncounterPolicy.Evaluate(
            99,
            new[] { first, protectedEnemy },
            true,
            true);
        Assert.That(result, Is.EqualTo(FieldEncounterResolution.PreemptiveBattle));
    }

    private EnemyData CreateEnemy(int threat, int gap, bool allow)
    {
        EnemyData enemy = ScriptableObject.CreateInstance<EnemyData>();
        enemy.ThreatLevel = threat;
        enemy.InstantKillLevelGap = gap;
        enemy.AllowInstantKillAfterDefeat = allow;
        _created.Add(enemy);
        return enemy;
    }
}
