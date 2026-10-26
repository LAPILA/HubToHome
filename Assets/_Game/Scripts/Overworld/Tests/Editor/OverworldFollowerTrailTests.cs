using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class OverworldFollowerTrailTests
{
    [Test]
    public void EmptyTrailRejectsQueriesAndHasFixedCapacity()
    {
        var trail = new OverworldFollowerTrail();
        Assert.AreEqual(1024, trail.Capacity);
        Assert.AreEqual(0, trail.Count);
        Assert.IsFalse(trail.TrySample(0f, out _));
        Assert.IsFalse(trail.TryFindDistance(Vector3.zero, 1f, out _));
        Assert.Throws<ArgumentOutOfRangeException>(() => new OverworldFollowerTrail(1));
    }

    [Test]
    public void ResetHasOneRealPointAndInvalidatesPreviousEpoch()
    {
        var trail = new OverworldFollowerTrail();
        trail.Reset(new Vector3(4f, 3f, 0f), 2);
        uint oldEpoch = trail.Epoch;
        AssertPose(trail, 0f, 4f, 3f, 2);
        Assert.IsFalse(trail.TrySample(-.1f, out _));
        Assert.IsFalse(trail.TrySample(.1f, out _));
        trail.Record(new Vector3(5f, 3f, 0f), 3);
        trail.Reset(new Vector3(-2f, 7f, 0f), 1);
        Assert.AreEqual(1, trail.Count);
        Assert.AreEqual(0f, trail.HeadDistance);
        Assert.IsTrue(trail.Epoch != oldEpoch);
        Assert.IsFalse(trail.TrySample(0f, oldEpoch, out _));
        Assert.IsTrue(trail.TrySample(0f, trail.Epoch, out _));
        AssertPose(trail, 0f, -2f, 7f, 1);
    }

    [Test]
    public void RightAngleSamplingStaysOnRecordedLegs()
    {
        var trail = new OverworldFollowerTrail();
        trail.Reset(Vector3.zero, 3);
        trail.Record(new Vector3(2f, 0f, 0f), 3);
        trail.Record(new Vector3(2f, 2f, 0f), 1);
        Assert.AreEqual(4f, trail.HeadDistance, .000001f);
        AssertPose(trail, 1f, 1f, 0f, 3);
        AssertPose(trail, 2f, 2f, 0f, 3);
        AssertPose(trail, 3f, 2f, 1f, 1);
    }

    [Test]
    public void DiagonalDistanceIsWorldDistanceAndIncludesZ()
    {
        var trail = new OverworldFollowerTrail();
        trail.Reset(Vector3.zero, 3);
        trail.Record(new Vector3(3f, 4f, 0f), 1);
        Assert.AreEqual(5f, trail.HeadDistance, .000001f);
        AssertPose(trail, 2.5f, 1.5f, 2f, 1);
        trail.Record(new Vector3(3f, 4f, 2f), 1);
        Assert.IsTrue(trail.TrySample(6f, out OverworldFollowerPose pose));
        Assert.AreEqual(1f, pose.Position.z, .000001f);
    }

    [Test]
    public void StationaryRecordUpdatesFacingWithoutConsumingPoints()
    {
        var trail = new OverworldFollowerTrail(3);
        Assert.IsTrue(trail.Record(Vector3.zero, 0));
        uint epoch = trail.Epoch;
        for (int i = 0; i < 100; i++) Assert.IsFalse(trail.Record(Vector3.zero, i % 4));
        Assert.AreEqual(1, trail.Count);
        Assert.AreEqual(epoch, trail.Epoch);
        AssertPose(trail, 0f, 0f, 0f, 3);
    }

    [Test]
    public void TurningInPlaceDoesNotRewriteFacingOnTheTravelLeg()
    {
        var trail = new OverworldFollowerTrail();
        trail.Reset(Vector3.zero, 3);
        trail.Record(new Vector3(2f, 0f, 0f), 3);
        Assert.IsFalse(trail.Record(new Vector3(2f, 0f, 0f), 1));
        Assert.AreEqual(2, trail.Count);
        AssertPose(trail, 1f, 1f, 0f, 3);
        AssertPose(trail, 2f, 2f, 0f, 1);
    }

    [Test]
    public void TinyNonzeroMovesAndCornersAreNotSimplifiedAway()
    {
        var trail = new OverworldFollowerTrail();
        trail.Reset(Vector3.zero, 3);
        Assert.IsTrue(trail.Record(new Vector3(.000001f, 0f, 0f), 3));
        Assert.IsTrue(trail.Record(new Vector3(.000001f, .000001f, 0f), 1));
        Assert.AreEqual(3, trail.Count);
        AssertPose(trail, .0000015f, .000001f, .0000005f, 1);
    }

    [Test]
    public void RingExpiresOldSegmentsWithoutRebasingLiveCursors()
    {
        var trail = new OverworldFollowerTrail(3);
        trail.Reset(Vector3.zero, 3);
        uint epoch = trail.Epoch;
        for (int i = 1; i <= 100; i++)
        {
            Assert.IsTrue(trail.Record(new Vector3(i, 0f, 0f), 3));
            Assert.IsTrue(trail.Count <= trail.Capacity);
        }
        Assert.AreEqual(3, trail.Count);
        Assert.AreEqual(98f, trail.OldestDistance);
        Assert.AreEqual(100f, trail.HeadDistance);
        Assert.AreEqual(epoch, trail.Epoch);
        Assert.IsFalse(trail.TrySample(97.999f, out _));
        AssertPose(trail, 98f, 98f, 0f, 3);
        AssertPose(trail, 99.5f, 99.5f, 0f, 3);
        Assert.IsFalse(trail.TryFindDistance(new Vector3(97f, 0f, 0f), .5f, out _));
    }

    [Test]
    public void ReverseMovementRemainsAnOrderedPathInsteadOfAChord()
    {
        var trail = new OverworldFollowerTrail();
        trail.Reset(Vector3.zero, 3);
        trail.Record(new Vector3(2f, 0f, 0f), 3);
        trail.Record(Vector3.zero, 2);
        Assert.AreEqual(3, trail.Count);
        Assert.AreEqual(4f, trail.HeadDistance);
        AssertPose(trail, 3f, 1f, 0f, 2);
        Assert.IsTrue(trail.TryFindDistance(new Vector3(1f, 0f, 0f), 0f, out float distance));
        Assert.AreEqual(3f, distance);
    }

    [Test]
    public void SubdividingStraightLegsDoesNotChangeSamples()
    {
        var sparse = new OverworldFollowerTrail();
        var dense = new OverworldFollowerTrail();
        sparse.Reset(Vector3.zero, 3);
        dense.Reset(Vector3.zero, 3);
        sparse.Record(new Vector3(3f, 0f, 0f), 3);
        sparse.Record(new Vector3(3f, 4f, 0f), 1);
        for (int i = 1; i <= 30; i++) dense.Record(new Vector3(i * .1f, 0f, 0f), 3);
        for (int i = 1; i <= 40; i++) dense.Record(new Vector3(3f, i * .1f, 0f), 1);
        Assert.AreEqual(sparse.HeadDistance, dense.HeadDistance, .000001f);
        for (int i = 0; i <= 140; i++)
        {
            float distance = i * .05f;
            Assert.IsTrue(sparse.TrySample(distance, out OverworldFollowerPose a));
            Assert.IsTrue(dense.TrySample(distance, out OverworldFollowerPose b));
            Assert.AreEqual(a.Position.x, b.Position.x, .00001f);
            Assert.AreEqual(a.Position.y, b.Position.y, .00001f);
        }
    }

    [Test]
    public void RandomPolylineSamplesMatchAnIndependentLinearReference()
    {
        const int pointCount = 200;
        var trail = new OverworldFollowerTrail(256);
        var positions = new Vector3[pointCount];
        var distances = new double[pointCount];
        var random = new System.Random(20261023);
        trail.Reset(Vector3.zero, 0);
        for (int i = 1; i < pointCount; i++)
        {
            Vector3 previous = positions[i - 1];
            positions[i] = new Vector3(previous.x + (float)random.NextDouble() - .5f,
                previous.y + (float)random.NextDouble() - .5f, previous.z);
            double dx = (double)positions[i].x - previous.x;
            double dy = (double)positions[i].y - previous.y;
            distances[i] = distances[i - 1] + Math.Sqrt(dx * dx + dy * dy);
            Assert.IsTrue(trail.Record(positions[i], i % 4));
        }
        Assert.AreEqual(pointCount, trail.Count);
        for (int i = 0; i < 2000; i++)
        {
            float distance = trail.HeadDistance * (float)random.NextDouble();
            int segment = 1;
            while (segment < pointCount - 1 && distances[segment] < distance) segment++;
            double t = (distance - distances[segment - 1]) / (distances[segment] - distances[segment - 1]);
            Vector3 from = positions[segment - 1];
            Vector3 to = positions[segment];
            Assert.IsTrue(trail.TrySample(distance, out OverworldFollowerPose pose));
            Assert.AreEqual((float)(from.x + ((double)to.x - from.x) * t), pose.Position.x, .000001f);
            Assert.AreEqual((float)(from.y + ((double)to.y - from.y) * t), pose.Position.y, .000001f);
            Assert.AreEqual(segment % 4, pose.FacingDirection);
        }
    }

    [Test]
    public void NonfiniteAndOutOfRangeInputsCannotMutateOrClamp()
    {
        var trail = new OverworldFollowerTrail();
        trail.Reset(Vector3.zero, 0);
        uint epoch = trail.Epoch;
        float[] invalid = { float.NaN, float.PositiveInfinity, float.NegativeInfinity };
        for (int i = 0; i < invalid.Length; i++)
        {
            float value = invalid[i];
            Assert.IsFalse(trail.Record(new Vector3(value, 0f, 0f), 0));
            Assert.IsFalse(trail.TrySample(value, out _));
            Assert.IsFalse(trail.TryFindDistance(Vector3.zero, value, out _));
            Assert.IsFalse(trail.TryFindDistance(new Vector3(0f, value, 0f), 1f, out _));
            Assert.Throws<ArgumentException>(() => trail.Reset(new Vector3(0f, 0f, value), 0));
        }
        Assert.IsFalse(trail.Record(Vector3.zero, -1));
        Assert.IsFalse(trail.Record(Vector3.zero, 4));
        Assert.Throws<ArgumentOutOfRangeException>(() => trail.Reset(Vector3.zero, 4));
        Assert.IsFalse(trail.TryFindDistance(Vector3.zero, -1f, out _));
        Assert.IsFalse(trail.TrySample(-1f, out _));
        Assert.IsFalse(trail.TrySample(1f, out _));
        Assert.AreEqual(epoch, trail.Epoch);
        Assert.AreEqual(1, trail.Count);
        AssertPose(trail, 0f, 0f, 0f, 0);
    }

    [Test]
    public void UnrepresentableTotalDistanceIsRejectedWithoutPoisoningTheRing()
    {
        var trail = new OverworldFollowerTrail();
        trail.Reset(new Vector3(-float.MaxValue, 0f, 0f), 3);
        Assert.IsFalse(trail.Record(new Vector3(float.MaxValue, 0f, 0f), 3));
        Assert.AreEqual(1, trail.Count);
        Assert.AreEqual(0f, trail.HeadDistance);
    }

    [Test]
    public void FindDistanceUsesSegmentsNotJustVerticesAndNeverInventsASample()
    {
        var trail = new OverworldFollowerTrail();
        trail.Reset(Vector3.zero, 3);
        trail.Record(new Vector3(4f, 0f, 0f), 3);
        trail.Record(new Vector3(4f, 3f, 0f), 1);
        Assert.IsTrue(trail.TryFindDistance(new Vector3(2f, .1f, 0f), .11f, out float distance));
        Assert.AreEqual(2f, distance, .000001f);
        AssertPose(trail, distance, 2f, 0f, 3);
        Assert.IsFalse(trail.TryFindDistance(new Vector3(2f, .1f, 0f), .09f, out _));
        Assert.IsTrue(trail.TryFindDistance(new Vector3(4f, 1.5f, 0f), 0f, out distance));
        Assert.AreEqual(5.5f, distance, .000001f);
    }

    [Test]
    public void RecordingAndSamplingReuseTheOriginalStorage()
    {
        var trail = new OverworldFollowerTrail(8);
        FieldInfo storage = typeof(OverworldFollowerTrail).GetField("_points", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsTrue(storage != null && storage.IsInitOnly);
        object points = storage.GetValue(trail);
        trail.Reset(Vector3.zero, 3);
        for (int i = 1; i <= 2000; i++)
        {
            trail.Record(new Vector3(i, 0f, 0f), 3);
            Assert.IsTrue(trail.TrySample(trail.HeadDistance - .5f, out _));
        }
        Assert.AreEqual(8, trail.Count);
        Assert.IsTrue(ReferenceEquals(points, storage.GetValue(trail)));
    }

    private static void AssertPose(OverworldFollowerTrail trail, float distance, float x, float y, int facing)
    {
        Assert.IsTrue(trail.TrySample(distance, out OverworldFollowerPose pose));
        Assert.AreEqual(x, pose.Position.x, .000001f);
        Assert.AreEqual(y, pose.Position.y, .000001f);
        Assert.AreEqual(facing, pose.FacingDirection);
    }
}
