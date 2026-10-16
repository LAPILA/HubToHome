using NUnit.Framework;
using UnityEngine;

public sealed class BattleRetryCheckpointTests
{
    [TearDown]
    public void TearDown() => BattleRetryCheckpoint.Clear();

    [Test]
    public void BossClassificationIsIndependentOfSpecies()
    {
        EnemyData enemy = ScriptableObject.CreateInstance<EnemyData>();
        try
        {
            enemy.Kind = EnemyKind.Human;
            enemy.Rank = EnemyRank.Normal;
            Assert.That(BattleRetryCheckpoint.ContainsBoss(new[] { enemy }), Is.False);
            enemy.Rank = EnemyRank.Elite;
            Assert.That(BattleRetryCheckpoint.ContainsBoss(new[] { enemy }), Is.False);
            enemy.Rank = EnemyRank.Boss;
            Assert.That(BattleRetryCheckpoint.ContainsBoss(new[] { enemy }), Is.True);
            enemy.Kind = EnemyKind.WhiteEntity;
            Assert.That(BattleRetryCheckpoint.ContainsBoss(new[] { enemy }), Is.True);
        }
        finally { Object.DestroyImmediate(enemy); }
    }

    [Test]
    public void DefeatRetainsCheckpointButVictoryAndNormalEncounterDiscardIt()
    {
        BattleRetryCheckpoint.Commit(new BattleRetryCheckpoint.Candidate());
        BattleRetryCheckpoint.CompleteEncounter(BattleEncounterOutcome.PartyDefeated);
        Assert.That(BattleRetryCheckpoint.HasCheckpoint, Is.True);
        BattleRetryCheckpoint.CompleteEncounter(BattleEncounterOutcome.Victory);
        Assert.That(BattleRetryCheckpoint.HasCheckpoint, Is.False);
        BattleRetryCheckpoint.Commit(new BattleRetryCheckpoint.Candidate());
        BattleRetryCheckpoint.Commit(null);
        Assert.That(BattleRetryCheckpoint.HasCheckpoint, Is.False);
    }

    [Test]
    public void MissingCheckpointRejectsRetryWithoutStartingLoad()
    {
        BattleRetryCheckpoint.Clear();
        GameLoadStartResult result = BattleRetryCheckpoint.TryRetry();
        Assert.That(result.Accepted, Is.False);
        Assert.That(BattleRetryCheckpoint.IsRestoring, Is.False);
        Assert.That(GameLoadCoordinator.IsLoading, Is.False);
    }
}
