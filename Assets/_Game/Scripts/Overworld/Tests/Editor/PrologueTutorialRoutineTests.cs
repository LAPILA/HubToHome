using System;
using System.Collections;
using NUnit.Framework;

public sealed class PrologueTutorialRoutineTests
{
    private int _cleaned;
    private bool _completed;
    private IEnumerator Inner(bool throwOnResume = false)
    {
        try { yield return null; if (throwOnResume) throw new InvalidOperationException("sample failure"); _completed = true; }
        finally { _cleaned++; }
    }
    private IEnumerator Outer(bool throwOnResume = false)
    {
        try { yield return Inner(throwOnResume); }
        finally { _cleaned++; }
    }

    [Test]
    public void ExplicitCancellationDisposesNestedPlaybackWithoutCompletingIt()
    {
        var routine = new PrologueTutorialRoutine(Outer(), () => true);
        Assert.That(routine.MoveNext());
        routine.Dispose();
        routine.Dispose();
        Assert.That(_cleaned, Is.EqualTo(2));
        Assert.That(_completed, Is.False);
        Assert.That(routine.MoveNext(), Is.False);
    }

    [Test]
    public void OwnerGenerationChangeDisposesAndDoesNotCommitNextStep()
    {
        bool current = true;
        var routine = new PrologueTutorialRoutine(Outer(), () => current);
        Assert.That(routine.MoveNext());
        current = false;
        Assert.That(routine.MoveNext(), Is.False);
        Assert.That(_cleaned, Is.EqualTo(2));
        Assert.That(_completed, Is.False);
    }

    [Test]
    public void NestedExceptionAlsoReleasesEveryOwner()
    {
        var routine = new PrologueTutorialRoutine(Outer(true), () => true);
        Assert.That(routine.MoveNext());
        Assert.Throws<InvalidOperationException>(() => routine.MoveNext());
        Assert.That(_cleaned, Is.EqualTo(2));
        Assert.That(_completed, Is.False);
        Assert.That(routine.MoveNext(), Is.False);
    }
}
