using System.Collections.Generic;
using System.Reflection;
using DG.Tweening;
using NUnit.Framework;
using UnityEngine;

public sealed class OverworldPartyFollowerTests
{
    private GameObject _globalObject;
    private GlobalDataManager _global;
    private GameObject _leaderObject;
    private PlayerController _leader;
    private PlayerCharacter _leaderCharacter;
    private CharacterData _data;
    private OverworldPartyFollowerController _controller;
    private object _previousCharacterCache;

    [SetUp]
    public void SetUp()
    {
        _previousCharacterCache = typeof(CharacterDatabase).GetField("_cache", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        _data = ScriptableObject.CreateInstance<CharacterData>();
        _data.CharacterID = "party.follow.tests";
        typeof(CharacterDatabase).GetField("_cache", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null,
            new Dictionary<string, CharacterData> { { _data.CharacterID, _data } });
        _globalObject = new GameObject("FollowerTests Global");
        _global = _globalObject.AddComponent<GlobalDataManager>();
        _leaderObject = new GameObject("FollowerTests Leader", typeof(SpriteRenderer), typeof(Rigidbody2D), typeof(Animator));
        _leaderCharacter = _leaderObject.AddComponent<PlayerCharacter>();
        _leaderCharacter.SetCharacterData(_data);
        _leader = _leaderObject.AddComponent<PlayerController>();
        CharacterSaveData leaderSave = Member();
        _global.Party.Add(leaderSave);
        _leaderCharacter.LoadDataFromGlobal(leaderSave);
        _global.Party.Add(Member());
        _global.Party.Add(Member());
        _controller = _leaderObject.AddComponent<OverworldPartyFollowerController>();
        _controller.Configure(_leader);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_leaderObject);
        Object.DestroyImmediate(_globalObject);
        Object.DestroyImmediate(_data);
        typeof(CharacterDatabase).GetField("_cache", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, _previousCharacterCache);
    }

    [Test]
    public void SameDatabaseMembers_RemainDistinctAndHaveNoGameplayComponents()
    {
        Assert.That(_controller.FollowerCount, Is.EqualTo(2));
        Assert.That(_controller.TryGetFollower(1, out var first), Is.True);
        Assert.That(_controller.TryGetFollower(2, out var second), Is.True);
        Assert.That(first.Member, Is.SameAs(_global.Party[1]));
        Assert.That(second.Member, Is.SameAs(_global.Party[2]));
        Assert.That(first, Is.Not.SameAs(second));
        foreach (var actor in new[] { first, second })
        {
            Assert.That(actor.GetComponentsInChildren<PlayerController>(true), Is.Empty);
            Assert.That(actor.GetComponentsInChildren<CharacterBase>(true), Is.Empty);
            Assert.That(actor.GetComponentsInChildren<InteractionSystem>(true), Is.Empty);
            Assert.That(actor.GetComponentsInChildren<Rigidbody2D>(true), Is.Empty);
            Assert.That(actor.GetComponentsInChildren<Collider2D>(true), Is.Empty);
            Assert.That(actor.CompareTag("Player"), Is.False);
            Assert.That(actor.GetComponent<SpriteRenderer>().sortingOrder, Is.EqualTo(3));
            Assert.That(actor.transform.parent, Is.Not.SameAs(_leader.transform));
        }
    }

    [Test]
    public void EnsureFor_PreservesManuallyConfiguredMaximum()
    {
        SetField(_controller, "_maxFollowers", 5);
        Assert.That(OverworldPartyFollowerController.EnsureFor(_leader), Is.SameAs(_controller));
        Assert.That(_controller.MaxFollowers, Is.EqualTo(5));
        _controller.Configure(_leader);
        Assert.That(_controller.MaxFollowers, Is.EqualTo(5));
    }

    [Test]
    public void EnsureFor_BattleCloneDoesNotCreateController()
    {
        GameObject clone = new GameObject("FollowerTests Battle Clone", typeof(Rigidbody2D), typeof(Animator));
        try
        {
            var controller = clone.AddComponent<PlayerController>();
            controller.HoldForBattleEntry();
            Assert.That(OverworldPartyFollowerController.EnsureFor(controller), Is.Null);
            Assert.That(clone.GetComponent<OverworldPartyFollowerController>(), Is.Null);
        }
        finally { Object.DestroyImmediate(clone); }
    }

    [Test]
    public void Follow_ConsumesPolylineDistanceWithoutCuttingCorner()
    {
        MoveLeader(new Vector3(2f, 0f), 0f);
        MoveLeader(new Vector3(2f, 2f), 0f);
        Tick(0.3f);
        _controller.TryGetFollower(1, out var follower);
        Assert.That(follower.transform.position.x, Is.EqualTo(2f).Within(0.0001f));
        Assert.That(follower.transform.position.y, Is.GreaterThan(0f));
        Assert.That(follower.transform.position.y, Is.LessThanOrEqualTo(1.1001f));
    }

    [Test]
    public void HoldResume_CatchesUpFromRetainedCursorWithoutGather()
    {
        Assert.That(_controller.TrySetMode(1, OverworldFollowerMode.Hold, out _), Is.True);
        MoveLeader(new Vector3(2f, 0f), 0.1f);
        _controller.TryGetFollower(1, out var follower);
        Assert.That(follower.transform.position, Is.EqualTo(Vector3.zero));
        Assert.That(_controller.TrySetMode(1, OverworldFollowerMode.Follow, out _), Is.True);
        Tick(0.1f);
        Assert.That(follower.transform.position.x, Is.GreaterThan(0f));
        Assert.That(follower.transform.position.x, Is.LessThan(1.1f));
    }

    [Test]
    public void FollowExpiredCursor_FailsAndDoesNotTeleport()
    {
        _controller.TryGetFollower(1, out var follower);
        _controller.TrySetMode(1, OverworldFollowerMode.Hold, out _);
        var trail = (OverworldFollowerTrail)GetField(_controller, "_trail");
        for (int i = 1; i <= 1100; i++) trail.Record(new Vector3(i * 0.01f, 0f), 3);
        Vector3 held = follower.transform.position;
        Assert.That(_controller.TrySetMode(1, OverworldFollowerMode.Follow, out string error), Is.False);
        Assert.That(error, Does.Contain("만료"));
        Assert.That(follower.Mode, Is.EqualTo(OverworldFollowerMode.Hold));
        Assert.That(follower.transform.position, Is.EqualTo(held));
    }

    [Test]
    public void FollowAll_PreflightFailureLeavesOtherMembersUnchanged()
    {
        _controller.TrySetMode(0, OverworldFollowerMode.Hold, out _);
        _controller.TryGetFollower(2, out var second);
        second.transform.position = new Vector3(10f, 10f);
        Assert.That(_controller.TrySetMode(0, OverworldFollowerMode.Follow, out _), Is.False);
        _controller.TryGetFollower(1, out var first);
        Assert.That(first.Mode, Is.EqualTo(OverworldFollowerMode.Hold));
        Assert.That(second.Mode, Is.EqualTo(OverworldFollowerMode.Hold));
    }

    [Test]
    public void ExplicitMoveOffTrail_RejoinFailsInHoldWithoutSnap()
    {
        Vector3 outside = new Vector3(4f, 4f);
        Assert.That(_controller.TryBeginMove(1, new[] { outside }, 0f, true, out var command, out _), Is.True);
        Assert.That(command.IsDone, Is.True);
        Assert.That(command.WasCompleted, Is.False);
        Assert.That(command.Error, Does.Contain("경로 밖"));
        _controller.TryGetFollower(1, out var follower);
        Assert.That(follower.Mode, Is.EqualTo(OverworldFollowerMode.Hold));
        Assert.That(follower.transform.position, Is.EqualTo(outside));
    }

    [Test]
    public void ExplicitMoveBackOntoRecordedPath_CanRejoin()
    {
        MoveLeader(new Vector3(3f, 0f), 0f);
        Assert.That(_controller.TryBeginMove(1, new[] { new Vector3(1f, 0f) }, 0f, true, out var command, out _), Is.True);
        Assert.That(command.WasCompleted, Is.True);
        _controller.TryGetFollower(1, out var follower);
        Assert.That(follower.Mode, Is.EqualTo(OverworldFollowerMode.Follow));
        Assert.That(follower.TrailDistance, Is.EqualTo(1f).Within(0.0001f));
    }

    [Test]
    public void NewMoveAndArrival_CancelOwnedCommandsAndPreserveNewOrigin()
    {
        Assert.That(_controller.TryBeginMove(1, new[] { Vector3.right * 4f }, 5f, false, out var old, out _), Is.True);
        Assert.That(_controller.TryBeginMove(1, new[] { Vector3.up * 4f }, 5f, false, out var next, out _), Is.True);
        Assert.That(old.IsCancelled, Is.True);
        old.Cancel();
        Assert.That(next.IsDone, Is.False);
        uint epoch = _controller.TrailEpoch;
        _leader.transform.position = new Vector3(30f, 25f);
        _controller.ResetAtLeader();
        Assert.That(next.IsCancelled, Is.True);
        Assert.That(_controller.TrailEpoch, Is.Not.EqualTo(epoch));
        next.Cancel();
        _controller.TryGetFollower(1, out var follower);
        Assert.That(follower.transform.position, Is.EqualTo(_leader.transform.position));
    }

    [Test]
    public void SameCoordinateMapArrival_ResetsEpochAndCancelsPendingMove()
    {
        _controller.TryBeginMove(1, new[] { Vector3.right * 4f }, 5f, false, out var command, out _);
        uint epoch = _controller.TrailEpoch;
        var request = new MapTransitionRequest
        {
            UseFallbackPosition = true,
            FallbackPosition = Vector2.zero,
            FacingAfterEnter = FacingDirection.Keep
        };
        Assert.That(MapTransitionService.TryApplyArrival(_leader, request, null, out _), Is.True);
        Assert.That(command.IsCancelled, Is.True);
        Assert.That(_controller.TrailEpoch, Is.Not.EqualTo(epoch));
        _controller.TryGetFollower(1, out var follower);
        Assert.That(follower.transform.position, Is.EqualTo(Vector3.zero));
    }

    [Test]
    public void OwnedTweenKilledExternally_EndsWaitingCommand()
    {
        _controller.TryBeginMove(1, new[] { Vector3.right * 4f }, 5f, false, out var command, out _);
        _controller.TryGetFollower(1, out var follower);
        Tween tween = (Tween)GetField(follower, "_moveTween");
        tween.Kill(false);
        Assert.That(command.IsDone, Is.True);
        Assert.That(command.IsCancelled, Is.True);
        Assert.That(command.WasCompleted, Is.False);
    }

    [Test]
    public void HiddenAndDetached_AreIndependentAndArrivalRestoresDefaults()
    {
        Assert.That(_controller.TrySetMode(1, OverworldFollowerMode.Detached, out _), Is.True);
        Assert.That(_controller.TrySetVisible(1, false, out _), Is.True);
        _controller.TryGetFollower(1, out var follower);
        Assert.That(follower.Mode, Is.EqualTo(OverworldFollowerMode.Detached));
        Assert.That(follower.Visible, Is.False);
        _controller.ResetAtLeader();
        Assert.That(follower.Mode, Is.EqualTo(OverworldFollowerMode.Follow));
        Assert.That(follower.Visible, Is.True);
    }

    [Test]
    public void RosterReferenceReplacement_RemovesOldViewsAndKeepsDuplicateIds()
    {
        _controller.TryGetFollower(1, out var before);
        var replaced = new List<CharacterSaveData> { Member(), Member(), Member() };
        typeof(GlobalDataManager).GetProperty("Party").SetValue(_global, replaced);
        Assert.That(_controller.TryGetFollower(1, out var after), Is.True);
        Assert.That(after, Is.Not.SameAs(before));
        Assert.That(after.Member, Is.SameAs(replaced[1]));
        Assert.That(_controller.FollowerCount, Is.EqualTo(2));
    }

    [Test]
    public void MoveAllAndMissingSlots_FailWithoutMutation()
    {
        Assert.That(_controller.TryBeginMove(0, new[] { Vector3.right }, 0f, false, out _, out _), Is.False);
        Assert.That(_controller.TrySetMode(5, OverworldFollowerMode.Detached, out _), Is.False);
        Assert.That(_controller.TrySetVisible(-1, false, out _), Is.False);
        _controller.TryGetFollower(1, out var follower);
        Assert.That(follower.Mode, Is.EqualTo(OverworldFollowerMode.Follow));
        Assert.That(follower.transform.position, Is.EqualTo(Vector3.zero));
    }

    private CharacterSaveData Member() => new CharacterSaveData
    { CharacterDataID = _data.CharacterID, CharacterID = "동일 원본 동료", HP = 100, MaxHP = 100, AP = 50, MaxAP = 50 };

    private void MoveLeader(Vector3 position, float deltaTime) { _leader.transform.position = position; Tick(deltaTime); }
    private void Tick(float deltaTime) => typeof(OverworldPartyFollowerController)
        .GetMethod("Tick", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_controller, new object[] { deltaTime });
    private static object GetField(object target, string name) => target.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    private static void SetField(object target, string name, object value) => target.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
}
