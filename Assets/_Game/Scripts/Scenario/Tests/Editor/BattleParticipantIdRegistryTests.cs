using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class BattleParticipantIdRegistryTests
{
    [Test]
    public void DuplicateEnemyData_UsesStableRuntimeSuffixes()
    {
        EnemyData data = ScriptableObject.CreateInstance<EnemyData>();
        data.EnemyId = "white_body";
        GameObject firstObject = new GameObject("Enemy1");
        GameObject secondObject = new GameObject("Enemy2");
        EnemyCharacter first = firstObject.AddComponent<EnemyCharacter>();
        EnemyCharacter second = secondObject.AddComponent<EnemyCharacter>();
        first.Data = data;
        second.Data = data;
        var registry = new BattleParticipantIdRegistry();

        try
        {
            registry.Rebuild(
                new List<PlayerCharacter>(),
                new List<EnemyCharacter> { first, second });

            Assert.That(registry.ResolveId(first), Is.EqualTo("white_body"));
            Assert.That(registry.ResolveId(second), Is.EqualTo("white_body#2"));
            Assert.That(registry.TryResolve("white_body", out CharacterBase resolved), Is.True);
            Assert.That(resolved, Is.SameAs(first));
        }
        finally
        {
            Object.DestroyImmediate(firstObject);
            Object.DestroyImmediate(secondObject);
            Object.DestroyImmediate(data);
        }
    }
}
