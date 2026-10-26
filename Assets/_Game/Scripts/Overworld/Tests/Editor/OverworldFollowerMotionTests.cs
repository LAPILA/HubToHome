using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class OverworldFollowerMotionTests
{
    private static readonly Action<OverworldFollowerActor, OverworldFollowerTrail, Vector3, int> ResetPose =
        (Action<OverworldFollowerActor, OverworldFollowerTrail, Vector3, int>)typeof(OverworldFollowerActor)
        .GetMethod("ResetPose", BindingFlags.Instance | BindingFlags.NonPublic)
        .CreateDelegate(typeof(Action<OverworldFollowerActor, OverworldFollowerTrail, Vector3, int>));
    private static readonly Action<OverworldFollowerActor, OverworldFollowerTrail, float, float, float> TickFollow =
        (Action<OverworldFollowerActor, OverworldFollowerTrail, float, float, float>)typeof(OverworldFollowerActor)
        .GetMethod("TickFollow", BindingFlags.Instance | BindingFlags.NonPublic)
        .CreateDelegate(typeof(Action<OverworldFollowerActor, OverworldFollowerTrail, float, float, float>));
    private static readonly Action<OverworldFollowerActor, OverworldFollowerTrail, float, float, float, float> TickStaggered =
        (Action<OverworldFollowerActor, OverworldFollowerTrail, float, float, float, float>)typeof(OverworldFollowerActor)
        .GetMethod("TickFollowStaggered", BindingFlags.Instance | BindingFlags.NonPublic)
        .CreateDelegate(typeof(Action<OverworldFollowerActor, OverworldFollowerTrail, float, float, float, float>));

    [TestCase(0.9f, 29)]
    [TestCase(1f, 32)]
    [TestCase(0.75f, 24)]
    [TestCase(float.NaN, 48)]
    public void Spacing_UsesWholeArtPixels(float requested, int pixels)
    {
        var root = new GameObject("Follower spacing test") { hideFlags = HideFlags.HideAndDontSave };
        try
        {
            var controller = root.AddComponent<OverworldPartyFollowerController>();
            typeof(OverworldPartyFollowerController).GetField("_spacing", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(controller, requested);
            Assert.That(controller.EffectiveSpacing * GameConfigPolicy.WorldPixelsPerUnit, Is.EqualTo(pixels));
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    [TestCase(30, 1, 0f)]
    [TestCase(60, 1, 0f)]
    [TestCase(120, 1, 0f)]
    [TestCase(60, -1, 0f)]
    [TestCase(60, 1, 4096f)]
    [TestCase(120, -1, 8192f)]
    public void SteadyFollow_DoesNotAccumulateGapOrAlternateRelativePixels(int fps, int direction, float initialDistance)
    {
        var root = new GameObject("Follower motion test") { hideFlags = HideFlags.HideAndDontSave };
        try
        {
            var actor = root.AddComponent<OverworldFollowerActor>();
            var trail = new OverworldFollowerTrail();
            trail.Reset(Vector3.zero, 3);
            ResetPose(actor, trail, Vector3.zero, 3);
            const float spacing = 29f / GameConfigPolicy.WorldPixelsPerUnit;
            if (initialDistance > 0f)
            {
                trail.Record(new Vector3(initialDistance * direction, 0, 0), direction > 0 ? 3 : 2);
                TickFollow(actor, trail, trail.HeadDistance - spacing, initialDistance, 0.02f);
            }
            float dt = 1f / fps;
            double elapsed = 0;
            for (int frame = 0; frame < fps * 24; frame++)
            {
                elapsed += dt;
                float x = (float)((initialDistance + elapsed * 8) * direction);
                trail.Record(new Vector3(x, 0, 0), direction > 0 ? 3 : 2);
                TickFollow(actor, trail, Mathf.Max(0f, trail.HeadDistance - spacing), 8f * dt, 0.02f);
                if (frame < fps * 2) continue;
                Assert.That(actor.Mode, Is.EqualTo(OverworldFollowerMode.Follow));
                Assert.That(Mathf.Abs(x - actor.transform.position.x), Is.EqualTo(spacing).Within(0.00005f));
                // 픽셀 격자 평행 이동 모델. ToEven은 정확한 반픽셀에서 홀수 간격의
                // 좌우 끝을 서로 반대로 반올림하므로 화면 이동의 검사 모델로 쓰지 않습니다.
                double relativePixel = Math.Floor(actor.transform.position.x * 32d + 0.5d)
                    - Math.Floor(x * 32d + 0.5d);
                Assert.That(relativePixel, Is.EqualTo(-direction * 29), "frame " + frame);
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    [Test]
    public void ZeroBudget_DoesNotConsumeRoundingTolerance()
    {
        var root = new GameObject("Follower paused motion test") { hideFlags = HideFlags.HideAndDontSave };
        try
        {
            var actor = root.AddComponent<OverworldFollowerActor>();
            var trail = new OverworldFollowerTrail();
            trail.Reset(Vector3.zero, 3);
            ResetPose(actor, trail, Vector3.zero, 3);
            trail.Record(Vector3.right, 3);
            TickFollow(actor, trail, 0.00005f, 0f, 0.02f);
            Assert.That(actor.TrailDistance, Is.Zero);
            Assert.That(actor.transform.position, Is.EqualTo(Vector3.zero));
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    [Test]
    public void StaggeredFollow_WaitsOnlyAtNaturalRest()
    {
        var root = new GameObject("Follower restart test") { hideFlags = HideFlags.HideAndDontSave };
        try
        {
            var actor = root.AddComponent<OverworldFollowerActor>();
            var trail = new OverworldFollowerTrail();
            trail.Reset(Vector3.zero, 3);
            ResetPose(actor, trail, Vector3.zero, 3);
            trail.Record(Vector3.right, 3);
            TickStaggered(actor, trail, 0.25f, 0.2f, 0.02f, 0.5f);
            Assert.That(actor.TrailDistance, Is.Zero);
            TickStaggered(actor, trail, 0.7f, 0.2f, 0.02f, 0.5f);
            Assert.That(actor.TrailDistance, Is.EqualTo(0.2f).Within(0.00001f));
            TickStaggered(actor, trail, 0.4f, 0.2f, 0.02f, 0.5f);
            Assert.That(actor.TrailDistance, Is.EqualTo(0.4f).Within(0.00001f));
            TickStaggered(actor, trail, 0.4f, 0.2f, 0.02f, 0.5f);
            TickStaggered(actor, trail, 0.65f, 0.2f, 0.02f, 0.5f);
            Assert.That(actor.TrailDistance, Is.EqualTo(0.4f).Within(0.00001f));
            TickStaggered(actor, trail, 1f, 1f, 0.02f, 0.5f);
            Assert.That(actor.TrailDistance, Is.EqualTo(1f));
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public void ForcedPauseOrRejoin_FinishesSmallRemainder(int interruption)
    {
        var root = new GameObject("Follower resume test") { hideFlags = HideFlags.HideAndDontSave };
        try
        {
            var actor = root.AddComponent<OverworldFollowerActor>();
            var trail = new OverworldFollowerTrail();
            trail.Reset(Vector3.zero, 3);
            ResetPose(actor, trail, Vector3.zero, 3);
            trail.Record(Vector3.right, 3);
            TickStaggered(actor, trail, 1f, 0.7f, 0.02f, 0.5f);
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            if (interruption == 0)
                typeof(OverworldFollowerActor).GetMethod("StopFollowingAnimation", flags).Invoke(actor, null);
            else if (interruption == 1)
                TickStaggered(actor, trail, 1f, 0f, 0.02f, 0.5f);
            else
            {
                MethodInfo mode = typeof(OverworldFollowerActor).GetMethod("CommitMode", flags);
                mode.Invoke(actor, new object[] { OverworldFollowerMode.Hold, trail, 0.7f });
                mode.Invoke(actor, new object[] { OverworldFollowerMode.Follow, trail, 0.7f });
            }
            TickStaggered(actor, trail, 1f, 1f, 0.02f, 0.5f);
            Assert.That(actor.TrailDistance, Is.EqualTo(1f));
            Assert.That(actor.Mode, Is.EqualTo(OverworldFollowerMode.Follow));
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
}
