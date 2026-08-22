using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class InteractionSystemTests
{
    private GameObject _systemObject;
    private GameObject _playerObject;
    private GameObject _targetObject;

    [SetUp]
    public void SetUp()
    {
        SetInteractionSingleton(null);

        _systemObject = new GameObject("Interaction System Test");
        _systemObject.SetActive(false);
        InteractionSystem system = _systemObject.AddComponent<InteractionSystem>();
        var serialized = new SerializedObject(system);
        serialized.FindProperty("_boxSize").vector2Value = new Vector2(0.8f, 0.8f);
        serialized.FindProperty("_boxDistance").floatValue = 0.4f;
        serialized.FindProperty("_interactLayer").intValue = 1 << 6;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        _systemObject.SetActive(true);
        typeof(InteractionSystem).GetMethod(
            "Awake",
            BindingFlags.Instance | BindingFlags.NonPublic).Invoke(system, null);

        _playerObject = new GameObject("Interaction Test Player");
        _playerObject.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
        PlayerController player = _playerObject.AddComponent<PlayerController>();
        player.SetFacingDirection(3);

        _targetObject = new GameObject("Interaction Test Target") { layer = 6 };
        _targetObject.transform.position = new Vector3(0.6f, 0f, 0f);
        BoxCollider2D collider = _targetObject.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;
        collider.size = new Vector2(0.2f, 0.2f);
        _targetObject.AddComponent<InteractionSystemProbe>();
        Physics2D.SyncTransforms();
    }

    [TearDown]
    public void TearDown()
    {
        if (_targetObject != null) Object.DestroyImmediate(_targetObject);
        if (_playerObject != null) Object.DestroyImmediate(_playerObject);
        if (_systemObject != null) Object.DestroyImmediate(_systemObject);
        SetInteractionSingleton(null);
    }

    [Test]
    public void TryInteract_DetectsCurrentFrontTargetWithoutWaitingForUpdate()
    {
        PlayerController player = _playerObject.GetComponent<PlayerController>();
        InteractionSystemProbe probe = _targetObject.GetComponent<InteractionSystemProbe>();

        InteractionSystem.Instance.TryInteract(player);

        Assert.That(probe.InteractionCount, Is.EqualTo(1),
            "Confirm 입력 시점의 대상이 캐시에 없어도 즉시 재탐색해야 합니다.");
    }

    private static void SetInteractionSingleton(InteractionSystem value)
    {
        FieldInfo field = typeof(InteractionSystem).GetField(
            "<Instance>k__BackingField",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        field.SetValue(null, value);
    }
}

public sealed class InteractionSystemProbe : MonoBehaviour, IInteractable
{
    public int InteractionCount { get; private set; }

    public void Interact(PlayerController player)
    {
        InteractionCount++;
    }

    public bool CanInteract(PlayerController player)
    {
        return player != null;
    }

    public void ShowHighlight(bool show)
    {
    }
}
