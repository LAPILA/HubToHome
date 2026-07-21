using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class BattlePreemptiveTurnPolicyTests
{
    [Test]
    public void PreemptivePolicyMovesFirstPlayerAheadOfFasterEnemy()
    {
        GameObject playerObject = new GameObject("Player");
        GameObject enemyObject = new GameObject("Enemy");
        try
        {
            PlayerCharacter player = playerObject.AddComponent<PlayerCharacter>();
            EnemyCharacter enemy = enemyObject.AddComponent<EnemyCharacter>();
            var queue = new List<CharacterBase> { enemy, player, enemy, player };

            bool promoted = BattleTurnQueuePolicy.PromoteFirstPlayer(queue);

            Assert.That(promoted, Is.True);
            Assert.That(queue[0], Is.SameAs(player));
            Assert.That(queue.Count, Is.EqualTo(4));
        }
        finally
        {
            Object.DestroyImmediate(playerObject);
            Object.DestroyImmediate(enemyObject);
        }
    }
}
