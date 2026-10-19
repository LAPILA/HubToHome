using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class PlayerSortingOrderTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private GameObject _playerObject;
    private PlayerController _player;
    private SpriteRenderer _renderer;

    [SetUp]
    public void SetUp()
    {
        _playerObject = new GameObject("Player Sorting Order Test");
        _playerObject.SetActive(false);
        _renderer = _playerObject.AddComponent<SpriteRenderer>();
        _renderer.sortingOrder = 55;
        _renderer.spriteSortPoint = SpriteSortPoint.Center;
        _player = _playerObject.AddComponent<PlayerController>();
        InvokePrivate("Awake");
    }

    [TearDown]
    public void TearDown()
    {
        if (_playerObject != null) Object.DestroyImmediate(_playerObject);
    }

    [Test]
    public void AwakeSetsFixedBaseOrderAndUsesFootPivot()
    {
        FieldInfo baseOrder = typeof(PlayerController).GetField("_baseSortingOrder", PrivateInstance);
        Assert.That(baseOrder, Is.Not.Null);
        Assert.That(baseOrder.GetValue(_player), Is.EqualTo(3));
        Assert.That(_renderer.sortingOrder, Is.EqualTo(3));
        Assert.That(_renderer.spriteSortPoint, Is.EqualTo(SpriteSortPoint.Pivot));
    }

    [TestCase(-.54f)]
    [TestCase(0f)]
    [TestCase(2f)]
    public void OverworldSortingOrderRemainsThreeRegardlessOfWorldY(float worldY)
    {
        _player.transform.position = new Vector3(1f, worldY, 0f);
        _renderer.sortingOrder = 55;

        InvokePrivate("UpdateSortingOrder");

        Assert.That(_renderer.sortingOrder, Is.EqualTo(3));
    }

    [Test]
    public void BattleSortingBoostRemainsTemporaryAndCanRestoreFixedOrder()
    {
        PropertyInfo state = typeof(PlayerController).GetProperty("State", BindingFlags.Instance | BindingFlags.Public);
        Assert.That(state, Is.Not.Null);
        state.GetSetMethod(true).Invoke(_player, new object[] { PlayerController.PlayerState.InBattle });

        _player.SetBattleSortingBoost(5000);
        InvokePrivate("LateUpdate");
        Assert.That(_renderer.sortingOrder, Is.EqualTo(5003));

        _player.SetBattleSortingBoost(0);
        Assert.That(_renderer.sortingOrder, Is.EqualTo(3));

        state.GetSetMethod(true).Invoke(_player, new object[] { PlayerController.PlayerState.Idle });
        _player.transform.position = new Vector3(0f, -100f, 0f);
        InvokePrivate("LateUpdate");
        Assert.That(_renderer.sortingOrder, Is.EqualTo(3));
    }

    private void InvokePrivate(string name)
    {
        MethodInfo method = typeof(PlayerController).GetMethod(name, PrivateInstance);
        Assert.That(method, Is.Not.Null, name);
        method.Invoke(_player, null);
    }
}
