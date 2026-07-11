using NUnit.Framework;
using UnityEngine;

public class BattleScenarioSubjectResolverTests
{
    [Test]
    public void EnemyIdIsThePrimaryScenarioSubjectId()
    {
        EnemyData data = ScriptableObject.CreateInstance<EnemyData>();
        data.EnemyId = "zev";
        data.EnemyName = "ZEV Display Name";
        data.name = "enemy_asset_name";

        Assert.That(BattleScenarioSubjectResolver.ResolveEnemySubjectId(data), Is.EqualTo("zev"));

        UnityEngine.Object.DestroyImmediate(data);
    }

    [Test]
    public void EnemyDataAssetNameIsTheFirstMigrationFallback()
    {
        EnemyData data = ScriptableObject.CreateInstance<EnemyData>();
        data.EnemyName = "ZEV Display Name";
        data.name = "zev_asset";

        Assert.That(BattleScenarioSubjectResolver.ResolveEnemySubjectId(data), Is.EqualTo("zev_asset"));

        UnityEngine.Object.DestroyImmediate(data);
    }

    [Test]
    public void EnemyDisplayNameIsTheLastDataFallback()
    {
        EnemyData data = ScriptableObject.CreateInstance<EnemyData>();
        data.EnemyName = "ZEV";

        Assert.That(BattleScenarioSubjectResolver.ResolveEnemySubjectId(data), Is.EqualTo("ZEV"));

        UnityEngine.Object.DestroyImmediate(data);
    }
}
