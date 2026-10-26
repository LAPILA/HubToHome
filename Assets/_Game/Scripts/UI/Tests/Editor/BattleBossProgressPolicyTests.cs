using NUnit.Framework;

public sealed class BattleBossProgressPolicyTests
{
    [TestCase(0, 0, 0f)]
    [TestCase(-10, 100, 0f)]
    [TestCase(200, 100, 1f)]
    [TestCase(25, 100, .25f)]
    public void Ratio_IsBoundedAndHasNoZeroDivision(int current, int maximum, float expected)
        => Assert.That(BattleBossProgressPolicy.Ratio(current, maximum), Is.EqualTo(expected));

    [Test]
    public void Markers_AreFiniteSortedUniqueAndDoNotMutateAuthorData()
    {
        float[] source = { .75f, float.NaN, .5f, 0f, 1f, -.2f, float.PositiveInfinity, .50001f, .25f };
        Assert.That(BattleBossProgressPolicy.NormalizeMarkers(source), Is.EqualTo(new[] { .25f, .5f, .75f }));
        Assert.That(float.IsNaN(source[1]), Is.True);
        Assert.That(source[0], Is.EqualTo(.75f));
        Assert.That(BattleBossProgressPolicy.NormalizeMarkers(null), Is.Empty);
    }

    [Test]
    public void Markers_HaveABoundedDisplayCost()
    {
        var source = new float[20];
        for (int i = 0; i < source.Length; i++) source[i] = (i + 1) / 22f;
        Assert.That(BattleBossProgressPolicy.NormalizeMarkers(source), Has.Length.EqualTo(8));
    }

    [Test]
    public void EliteOptIn_DoesNotChangeItsRank()
    {
        EnemyData data = UnityEngine.ScriptableObject.CreateInstance<EnemyData>();
        try
        {
            data.Rank = EnemyRank.Elite;
            Assert.That(data.UsesHealthProgress, Is.False);
            data.ShowHealthProgress = true;
            Assert.That(data.UsesHealthProgress, Is.True);
            Assert.That(data.Rank, Is.EqualTo(EnemyRank.Elite));
            data.ShowHealthProgress = false;
            data.Rank = EnemyRank.Boss;
            Assert.That(data.UsesHealthProgress, Is.True);
        }
        finally { UnityEngine.Object.DestroyImmediate(data); }
    }

    [Test]
    public void View_ReusesItsHierarchyAndDropsAnEnemyThatExitsWithoutHpChange()
    {
        var host = new UnityEngine.GameObject("Boss progress test", typeof(UnityEngine.RectTransform));
        var enemyRoot = new UnityEngine.GameObject("Boss test");
        EnemyData data = UnityEngine.ScriptableObject.CreateInstance<EnemyData>();
        try
        {
            enemyRoot.SetActive(false);
            data.ShowHealthProgress = true;
            data.HealthProgressMarkers = new[] { .5f };
            EnemyCharacter actor = enemyRoot.AddComponent<EnemyCharacter>();
            actor.Setup(data);
            enemyRoot.SetActive(true);
            var view = host.AddComponent<BattleBossProgressPresenter>();
            var rect = (UnityEngine.RectTransform)host.transform;
            view.Bind(rect, new[] { actor }, null);
            view.Bind(rect, new[] { actor, actor }, null);
            Assert.That(rect.childCount, Is.EqualTo(1), "재바인딩으로 HUD를 복제하지 않습니다.");
            Assert.That(rect.GetChild(0).gameObject.activeSelf, Is.True);
            enemyRoot.SetActive(false); // HP 이벤트가 없는 이벤트 퇴장
            typeof(BattleBossProgressPresenter).GetMethod("LateUpdate",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(view, null);
            Assert.That(rect.GetChild(0).gameObject.activeSelf, Is.False);
            view.Clear();
            view.Clear();
            Assert.That(rect.GetChild(0).gameObject.activeSelf, Is.False);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
            UnityEngine.Object.DestroyImmediate(enemyRoot);
            UnityEngine.Object.DestroyImmediate(data);
        }
    }
}
