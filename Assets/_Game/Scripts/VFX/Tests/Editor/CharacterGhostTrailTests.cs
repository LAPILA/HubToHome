using System.Collections.Generic;
using System.Reflection;
using DG.Tweening;
using NUnit.Framework;
using UnityEngine;

public sealed class CharacterGhostTrailTests
{
    private GameObject _character;
    private CharacterGhostTrail _trail;

    [SetUp]
    public void SetUp()
    {
        _character = new GameObject("Ghost Trail Character");
        _character.AddComponent<SpriteRenderer>();
        _trail = _character.AddComponent<CharacterGhostTrail>();
        EnsureAwake(_trail);
    }

    [TearDown]
    public void TearDown()
    {
        if (_trail != null)
        {
            Transform poolContainer = GetPrivateField<Transform>(_trail, "_poolContainer");
            if (poolContainer != null && poolContainer.parent != _character.transform)
                Object.DestroyImmediate(poolContainer.gameObject);
        }

        if (_character != null)
            Object.DestroyImmediate(_character);
    }

    [Test]
    public void Awake_CreatesCharacterOwnedPoolAndStopsUnusedUpdates()
    {
        Transform poolContainer = GetPrivateField<Transform>(_trail, "_poolContainer");

        Assert.That(poolContainer, Is.Not.Null);
        Assert.That(poolContainer.parent, Is.SameAs(_character.transform));
        Assert.That(_trail.enabled, Is.False);
    }

    [Test]
    public void SetTrailActive_PreservesActivationContractAndControlsUpdates()
    {
        _trail.SetTrailActive(true);

        Assert.That(_trail.enabled, Is.True);

        _trail.SetTrailActive(false);

        Assert.That(_trail.enabled, Is.False);
    }

    [Test]
    public void EnabledButStationary_DoesNotCreateAttackAfterimages()
    {
        _trail.SetTrailActive(true);
        InvokePrivate(_trail, "UpdateTrail", 1f);
        InvokePrivate(_trail, "UpdateTrail", 1f);

        Assert.That(GetPrivateField<List<SpriteRenderer>>(_trail, "_ghostPool"), Is.Empty);
    }

    [Test]
    public void MovementCreatesGhost_StoppingClearsIt_AndResumingReusesPool()
    {
        _trail.SetTrailActive(true);
        _character.transform.position = Vector3.right;
        InvokePrivate(_trail, "UpdateTrail", 0.05f);
        var pool = GetPrivateField<List<SpriteRenderer>>(_trail, "_ghostPool");
        Assert.That(pool, Has.Count.EqualTo(1));
        SpriteRenderer ghost = pool[0];
        Assert.That(ghost.gameObject.activeSelf, Is.True);

        InvokePrivate(_trail, "UpdateTrail", 0.05f);
        Assert.That(ghost.gameObject.activeSelf, Is.False);
        Assert.That(DOTween.IsTweening(ghost), Is.False);

        _character.transform.position = Vector3.right * 2f;
        InvokePrivate(_trail, "UpdateTrail", 0.05f);
        Assert.That(pool, Has.Count.EqualTo(1));
        Assert.That(ghost.gameObject.activeSelf, Is.True);
    }

    [Test]
    public void EndingMovementClearsGhostsBeforeAttackPoseInSameFrame()
    {
        _trail.SetTrailActive(true);
        _character.transform.position = Vector3.right;
        InvokePrivate(_trail, "UpdateTrail", 0.05f);
        var pool = GetPrivateField<List<SpriteRenderer>>(_trail, "_ghostPool");

        _trail.SetTrailActive(false);

        Assert.That(pool[0].gameObject.activeSelf, Is.False);
        Assert.That(DOTween.IsTweening(pool[0]), Is.False);
        Assert.That(_trail.enabled, Is.False);
    }

    [Test]
    public void AnimatedVisualOffset_DoesNotCountAsCharacterMovement()
    {
        var actor = new GameObject("Actor Root");
        actor.SetActive(false);
        actor.AddComponent<PlayerCharacter>();
        var visual = new GameObject("Animated Visual", typeof(SpriteRenderer));
        visual.transform.SetParent(actor.transform, false);
        var trail = visual.AddComponent<CharacterGhostTrail>();
        EnsureAwake(trail);
        try
        {
            trail.SetTrailActive(true);
            visual.transform.localPosition = Vector3.right;
            InvokePrivate(trail, "UpdateTrail", 0.05f);
            Assert.That(GetPrivateField<List<SpriteRenderer>>(trail, "_ghostPool"), Is.Empty);
            actor.transform.position = Vector3.right;
            InvokePrivate(trail, "UpdateTrail", 0.05f);
            Assert.That(GetPrivateField<List<SpriteRenderer>>(trail, "_ghostPool"), Has.Count.EqualTo(1));
        }
        finally
        {
            Object.DestroyImmediate(actor);
        }
    }

    [Test]
    public void RepeatedEnable_DoesNotErasePendingMovement()
    {
        _trail.SetTrailActive(true);
        _character.transform.position = Vector3.right;
        _trail.SetTrailActive(true);
        InvokePrivate(_trail, "UpdateTrail", 0.05f);

        Assert.That(GetPrivateField<List<SpriteRenderer>>(_trail, "_ghostPool"), Has.Count.EqualTo(1));
    }

    [Test]
    public void SpawnGhost_ReusesOldestSlotAtConfiguredLimit()
    {
        SetPrivateField(_trail, "_maxGhostCount", 3);

        for (int i = 0; i < 8; i++)
            InvokePrivate(_trail, "SpawnGhost");

        List<SpriteRenderer> ghostPool =
            GetPrivateField<List<SpriteRenderer>>(_trail, "_ghostPool");
        Assert.That(ghostPool, Has.Count.EqualTo(3));
    }

    [Test]
    public void SpawnGhost_InvalidSerializedLimitStillUsesOneSafeSlot()
    {
        SetPrivateField(_trail, "_maxGhostCount", 0);

        InvokePrivate(_trail, "SpawnGhost");
        InvokePrivate(_trail, "SpawnGhost");

        List<SpriteRenderer> ghostPool =
            GetPrivateField<List<SpriteRenderer>>(_trail, "_ghostPool");
        Assert.That(ghostPool, Has.Count.EqualTo(1));
    }

    [Test]
    public void SpawnGhost_DetachesActiveGhostSoItDoesNotFollowCharacter()
    {
        _character.transform.position = new Vector3(2f, 3f, 0f);
        InvokePrivate(_trail, "SpawnGhost");
        List<SpriteRenderer> ghostPool =
            GetPrivateField<List<SpriteRenderer>>(_trail, "_ghostPool");
        Transform ghost = ghostPool[0].transform;
        Vector3 spawnPosition = ghost.position;

        _character.transform.position = new Vector3(8f, 9f, 0f);

        Assert.That(ghost.parent, Is.Null);
        Assert.That(ghost.position, Is.EqualTo(spawnPosition));
    }

    [Test]
    public void ReturnedGhost_IsReparentedAndSafelyReused()
    {
        SetPrivateField(_trail, "_maxGhostCount", 3);
        InvokePrivate(_trail, "SpawnGhost");
        Transform poolContainer = GetPrivateField<Transform>(_trail, "_poolContainer");
        List<SpriteRenderer> ghostPool =
            GetPrivateField<List<SpriteRenderer>>(_trail, "_ghostPool");
        SpriteRenderer ghost = ghostPool[0];

        InvokePrivate(_trail, "ReturnToPool", ghost);

        Assert.That(ghost.gameObject.activeSelf, Is.False);
        Assert.That(ghost.transform.parent, Is.SameAs(poolContainer));

        InvokePrivate(_trail, "SpawnGhost");

        Assert.That(ghostPool, Has.Count.EqualTo(1));
        Assert.That(ghost.gameObject.activeSelf, Is.True);
        Assert.That(ghost.transform.parent, Is.Null);
        Assert.That(DOTween.IsTweening(ghost), Is.True);
    }

    [Test]
    public void DestroyCharacter_KillsGhostTweensAndDestroysPool()
    {
        InvokePrivate(_trail, "SpawnGhost");
        Transform poolContainer = GetPrivateField<Transform>(_trail, "_poolContainer");
        List<SpriteRenderer> ghostPool =
            GetPrivateField<List<SpriteRenderer>>(_trail, "_ghostPool");
        SpriteRenderer ghost = ghostPool[0];
        Assert.That(DOTween.IsTweening(ghost), Is.True);

        Object.DestroyImmediate(_character);

        Assert.That(DOTween.IsTweening(ghost), Is.False);
        Assert.That(ghost == null, Is.True);
        Assert.That(poolContainer == null, Is.True);
        _character = null;
        _trail = null;
    }

    private static void EnsureAwake(CharacterGhostTrail trail)
    {
        if (GetPrivateField<Transform>(trail, "_poolContainer") != null)
            return;

        MethodInfo awake = typeof(CharacterGhostTrail).GetMethod(
            "Awake",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(awake, Is.Not.Null);
        awake.Invoke(trail, null);
    }

    private static T GetPrivateField<T>(CharacterGhostTrail trail, string fieldName)
    {
        FieldInfo field = typeof(CharacterGhostTrail).GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, fieldName);
        return (T)field.GetValue(trail);
    }

    private static void SetPrivateField<T>(CharacterGhostTrail trail, string fieldName, T value)
    {
        FieldInfo field = typeof(CharacterGhostTrail).GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, fieldName);
        field.SetValue(trail, value);
    }

    private static void InvokePrivate(
        CharacterGhostTrail trail,
        string methodName,
        params object[] arguments)
    {
        MethodInfo method = typeof(CharacterGhostTrail).GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, methodName);
        method.Invoke(trail, arguments);
    }
}
