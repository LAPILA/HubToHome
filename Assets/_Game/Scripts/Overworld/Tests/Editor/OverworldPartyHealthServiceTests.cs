using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class OverworldPartyHealthServiceTests
{
    private GameObject _globalObject;
    private GlobalDataManager _global;

    [SetUp]
    public void SetUp()
    {
        _globalObject = new GameObject("OverworldPartyHealthServiceTests_Global");
        _global = _globalObject.AddComponent<GlobalDataManager>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_globalObject);
    }

    [Test]
    public void ApplyDamage_ChangesPartyLeaderAndNeverDropsBelowOneHP()
    {
        CharacterSaveData leader = PartyMember("hero", hp: 8, maxHP: 20);
        _global.Party.Add(leader);
        var service = new OverworldPartyHealthService(_global);

        OverworldPartyDamageResult first = service.ApplyDamage(3);
        OverworldPartyDamageResult second = service.ApplyDamage(99);

        Assert.That(first.Status, Is.EqualTo(OverworldPartyDamageStatus.Applied));
        Assert.That(first.AppliedDamage, Is.EqualTo(3));
        Assert.That(second.CurrentHP, Is.EqualTo(1));
        Assert.That(leader.HP, Is.EqualTo(1));
    }

    [Test]
    public void ApplyDamage_WithoutPartyReturnsExplicitFailure()
    {
        var service = new OverworldPartyHealthService(_global);

        OverworldPartyDamageResult result = service.ApplyDamage(5);

        Assert.That(result.Status, Is.EqualTo(OverworldPartyDamageStatus.PartyMissing));
        Assert.That(result.Changed, Is.False);
    }

    [Test]
    public void ApplyDamage_SynchronizesBoundScenePlayerVitals()
    {
        CharacterSaveData leader = PartyMember("hero", hp: 12, maxHP: 20);
        _global.Party.Add(leader);
        GameObject playerObject = new GameObject("OverworldPartyHealthServiceTests_Player");
        PlayerCharacter scenePlayer = playerObject.AddComponent<PlayerCharacter>();
        try
        {
            scenePlayer.LoadDataFromGlobal(leader);
            var service = new OverworldPartyHealthService(_global);

            OverworldPartyDamageResult result = service.ApplyDamage(4, scenePlayer);

            Assert.That(result.CurrentHP, Is.EqualTo(8));
            Assert.That(scenePlayer.CurrentHP, Is.EqualTo(8));
            scenePlayer.SaveDataToGlobal();
            Assert.That(leader.HP, Is.EqualTo(8));
        }
        finally
        {
            Object.DestroyImmediate(playerObject);
        }
    }

    [Test]
    public void HazardMarker_RehitDelayBlocksSamePlayerUntilClockAdvances()
    {
        GameObject markerObject = new GameObject("HazardMarkerTests_Marker");
        markerObject.AddComponent<CircleCollider2D>().isTrigger = true;
        HazardMarker marker = markerObject.AddComponent<HazardMarker>();
        GameObject playerObject = new GameObject(
            "HazardMarkerTests_Player",
            typeof(Rigidbody2D),
            typeof(Animator),
            typeof(PlayerController));
        PlayerController player = playerObject.GetComponent<PlayerController>();
        var clock = new FakeTimeSource();
        var health = new FakeHealthService();
        try
        {
            SetField(marker, "rehitDelay", 1f);
            SetField(marker, "knockback", 0f);
            marker.SetRuntimeServices(health, clock);

            Assert.That(marker.TryApplyHazard(player), Is.True);
            Assert.That(marker.TryApplyHazard(player), Is.False);
            clock.Now = 1f;
            Assert.That(marker.TryApplyHazard(player), Is.True);
            Assert.That(health.CallCount, Is.EqualTo(2));
        }
        finally
        {
            Object.DestroyImmediate(playerObject);
            Object.DestroyImmediate(markerObject);
        }
    }

    [Test]
    public void PeriodicHazard_UsesDeterministicBoundariesAndDisablesSafely()
    {
        GameObject root = new GameObject("PeriodicHazardTests_Root");
        BoxCollider2D collider = root.AddComponent<BoxCollider2D>();
        GameObject visual = new GameObject("ActiveVisual");
        visual.transform.SetParent(root.transform, false);
        PeriodicHazardController controller = root.AddComponent<PeriodicHazardController>();
        var clock = new FakeTimeSource();
        try
        {
            controller.SetTimeSource(clock);
            controller.Configure(2f, 1f, 2f, new Collider2D[] { collider }, new[] { visual });

            clock.Now = 1.99f;
            controller.Tick();
            Assert.That(collider.enabled, Is.False);
            clock.Now = 2f;
            controller.Tick();
            Assert.That(collider.enabled, Is.True);
            clock.Now = 3f;
            controller.Tick();
            Assert.That(collider.enabled, Is.False);
            clock.Now = 5f;
            controller.Tick();
            Assert.That(collider.enabled, Is.True);

            controller.StopCycle();
            Assert.That(collider.enabled, Is.False);
            Assert.That(visual.activeSelf, Is.False);

            clock.Now = 10f;
            controller.RestartCycle();
            controller.Tick();
            Assert.That(collider.enabled, Is.False, "Re-enable must restart the first delay.");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static CharacterSaveData PartyMember(string id, int hp, int maxHP)
    {
        return new CharacterSaveData
        {
            CharacterDataID = id,
            CharacterID = "Different Display Name",
            HP = hp,
            MaxHP = maxHP,
            MP = 5,
            MaxMP = 10
        };
    }

    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, fieldName);
        field.SetValue(target, value);
    }

    private sealed class FakeTimeSource : IOverworldTimeSource
    {
        public float Now;
        public float UnscaledTime => Now;
    }

    private sealed class FakeHealthService : IOverworldPartyHealthService
    {
        public int CallCount { get; private set; }

        public OverworldPartyDamageResult ApplyDamage(int damage, PlayerCharacter scenePlayer = null)
        {
            CallCount++;
            return new OverworldPartyDamageResult(
                OverworldPartyDamageStatus.Applied,
                damage,
                damage,
                10,
                10 - damage);
        }
    }
}