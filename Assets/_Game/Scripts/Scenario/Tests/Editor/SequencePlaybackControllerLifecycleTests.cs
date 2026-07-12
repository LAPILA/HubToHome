using NUnit.Framework;
using UnityEngine;

public sealed class SequencePlaybackControllerLifecycleTests
{
    private ActionSequenceAsset _sequence;
    private ActionCatalogAsset _catalog;
    private SequencePlaybackController _controller;

    [SetUp]
    public void SetUp()
    {
        _sequence = ScriptableObject.CreateInstance<ActionSequenceAsset>();
        _sequence.SequenceId = "playback.lifecycle";
        _catalog = ScriptableObject.CreateInstance<ActionCatalogAsset>();
        _controller = new SequencePlaybackController(
            new SequenceLiveContextRegistry(false));
    }

    [TearDown]
    public void TearDown()
    {
        _controller?.Dispose();
        Object.DestroyImmediate(_catalog);
        Object.DestroyImmediate(_sequence);
    }

    [Test]
    public void SafePreviewCanRestartAfterValidationFailureAndSuccess()
    {
        Assert.That(
            _controller.StartSafePreview(null, null, _catalog),
            Is.False);
        Assert.That(_controller.State, Is.EqualTo(SequencePlaybackState.Failed));
        Assert.That(_controller.IsActive, Is.False);

        Assert.That(
            _controller.StartSafePreview(null, _sequence, _catalog),
            Is.True);
        CompleteSafePreview();
        Assert.That(_controller.State, Is.EqualTo(SequencePlaybackState.Succeeded));
        Assert.That(_controller.CanStop, Is.True);

        _controller.Stop();
        Assert.That(_controller.State, Is.EqualTo(SequencePlaybackState.Idle));

        Assert.That(
            _controller.StartSafePreview(null, _sequence, _catalog),
            Is.True);
        CompleteSafePreview();
        Assert.That(_controller.State, Is.EqualTo(SequencePlaybackState.Succeeded));
    }

    [Test]
    public void StopDuringSafePreviewReturnsIdleAndAllowsRestart()
    {
        Assert.That(
            _controller.StartSafePreview(null, _sequence, _catalog),
            Is.True);
        Assert.That(_controller.State, Is.EqualTo(SequencePlaybackState.Preparing));

        _controller.Stop();

        Assert.That(_controller.State, Is.EqualTo(SequencePlaybackState.Idle));
        Assert.That(_controller.IsActive, Is.False);
        Assert.That(
            _controller.StartSafePreview(null, _sequence, _catalog),
            Is.True);
    }

    [Test]
    public void EditModeLiveTestFailsClosedWithoutActivePlayback()
    {
        Assert.That(
            _controller.StartLiveTest(null, _sequence, _catalog),
            Is.False);

        Assert.That(_controller.State, Is.EqualTo(SequencePlaybackState.Failed));
        Assert.That(_controller.IsActive, Is.False);
        Assert.That(_controller.Trace, Has.Count.EqualTo(1));
        Assert.That(_controller.Trace[0].Message, Does.Contain("Play Mode"));
    }

    private void CompleteSafePreview()
    {
        for (int i = 0; i < 10 && _controller.IsActive; i++)
        {
            _controller.TickSafePreviewForTests();
        }
        Assert.That(_controller.IsActive, Is.False);
    }
}
