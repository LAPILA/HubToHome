using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

public class SequenceRecoveryStoreTests
{
    private string _root;
    private ActionSequenceAsset _sequence;
    private StandaloneSequenceSaveTarget _target;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(
            Path.GetTempPath(),
            "HubToHome-SequenceRecoveryTests-" + Guid.NewGuid().ToString("N"));
        _sequence = ScriptableObject.CreateInstance<ActionSequenceAsset>();
        _sequence.SequenceId = "recovery.test";
        _sequence.DisplayNameKo = "복구 원본";
        _sequence.Source.SourcePath = "Assets/recovery.test.sequence.yaml";
        _sequence.Actions.Add(new ScenarioActionData
        {
            BlockId = "wait",
            ActionId = FlowWaitActionAdapter.Id,
            ParametersJson = "{\"duration\":0}"
        });
        _target = new StandaloneSequenceSaveTarget(_sequence);
    }

    [TearDown]
    public void TearDown()
    {
        if (_sequence != null)
        {
            UnityEngine.Object.DestroyImmediate(_sequence);
        }
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }

    [Test]
    public void CaptureWritesHashVerifiedYamlAndMetadata()
    {
        var store = new SequenceRecoveryStore(_root);

        SequenceRecoverySnapshot snapshot = store.Capture(_target);

        Assert.That(snapshot, Is.Not.Null);
        Assert.That(File.Exists(snapshot.YamlFilePath), Is.True);
        Assert.That(
            ScenarioSourceHash.Compute(File.ReadAllText(snapshot.YamlFilePath)),
            Is.EqualTo(snapshot.ContentHash));
        Assert.That(store.List(_target).Count, Is.EqualTo(1));
    }

    [Test]
    public void IdenticalContentReusesLatestSnapshot()
    {
        var store = new SequenceRecoveryStore(_root);

        SequenceRecoverySnapshot first = store.Capture(_target);
        SequenceRecoverySnapshot second = store.Capture(_target);

        Assert.That(second.SnapshotId, Is.EqualTo(first.SnapshotId));
        Assert.That(store.List(_target).Count, Is.EqualTo(1));
    }

    [Test]
    public void SnapshotRotationKeepsConfiguredNewestCount()
    {
        var store = new SequenceRecoveryStore(_root, 2);
        store.Capture(_target);
        _sequence.DisplayNameKo = "두 번째";
        store.Capture(_target);
        _sequence.DisplayNameKo = "세 번째";
        store.Capture(_target);

        Assert.That(store.List(_target).Count, Is.EqualTo(2));
    }

    [Test]
    public void RestoreReimportsSnapshotIntoExistingRuntimeAsset()
    {
        var store = new SequenceRecoveryStore(_root);
        SequenceRecoverySnapshot snapshot = store.Capture(_target);
        _sequence.DisplayNameKo = "손상된 편집";
        _sequence.Actions.Clear();

        SequenceRecoveryResult result = store.Restore(snapshot, _sequence, null);

        Assert.That(result.Success, Is.True, result.Error);
        Assert.That(_sequence.DisplayNameKo, Is.EqualTo("복구 원본"));
        Assert.That(_sequence.Actions.Count, Is.EqualTo(1));
        Assert.That(_sequence.Actions[0].BlockId, Is.EqualTo("wait"));
    }

    [Test]
    public void TamperedSnapshotIsRejectedWithoutChangingTarget()
    {
        var store = new SequenceRecoveryStore(_root);
        SequenceRecoverySnapshot snapshot = store.Capture(_target);
        File.AppendAllText(snapshot.YamlFilePath, "\n# tampered");
        _sequence.DisplayNameKo = "현재 편집";

        SequenceRecoveryResult result = store.Restore(snapshot, _sequence, null);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Error, Does.Contain("해시"));
        Assert.That(_sequence.DisplayNameKo, Is.EqualTo("현재 편집"));
    }

    [Test]
    public void ClearRemovesAllSnapshotsForTarget()
    {
        var store = new SequenceRecoveryStore(_root);
        store.Capture(_target);

        store.Clear(_target);

        Assert.That(store.List(_target), Is.Empty);
    }
}
