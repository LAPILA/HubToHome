using NUnit.Framework;

public sealed class PrologueTutorialPracticeTests
{
    [Test]
    public void OnlyActualAttackAdvancesFirstLesson()
    {
        var practice = new PrologueTutorialPractice(3, 3);
        practice.RecordDefense(true, true);
        practice.RecordPlayerAction(false, true);
        Assert.That(practice.Stage, Is.EqualTo(PrologueTutorialLessonStage.Attack));
        practice.RecordPlayerAction(true, false);
        Assert.That(practice.Stage, Is.EqualTo(PrologueTutorialLessonStage.Guard));
        practice.RecordDefense(true, false);
        Assert.That(practice.Stage, Is.EqualTo(PrologueTutorialLessonStage.JustGuard));
        practice.RecordDefense(false, true);
        Assert.That(practice.Stage, Is.EqualTo(PrologueTutorialLessonStage.Item));
        practice.RecordPlayerAction(false, true);
        Assert.That(practice.Stage, Is.EqualTo(PrologueTutorialLessonStage.Complete));
    }

    [Test]
    public void RepeatedFailureOffersBoundedProgressWithoutChangingAnyJudgement()
    {
        var practice = new PrologueTutorialPractice(3, 3);
        practice.RecordPlayerAction(true, false);
        for (int i = 0; i < 2; i++) practice.RecordDefense(false, false);
        Assert.That(practice.Stage, Is.EqualTo(PrologueTutorialLessonStage.Guard));
        practice.RecordDefense(false, false);
        Assert.That(practice.Stage, Is.EqualTo(PrologueTutorialLessonStage.Item));
        for (int i = 0; i < 2; i++) practice.RecordPlayerAction(true, false);
        Assert.That(practice.Stage, Is.EqualTo(PrologueTutorialLessonStage.Item));
        practice.RecordPlayerAction(true, false);
        Assert.That(practice.Stage, Is.EqualTo(PrologueTutorialLessonStage.Complete));
        practice.RecordDefense(false, false);
        practice.RecordPlayerAction(true, true);
        Assert.That(practice.Stage, Is.EqualTo(PrologueTutorialLessonStage.Complete));
    }

    [Test]
    public void InvalidZeroLimitsStillRequireOneAttemptAndOneCompletedAction()
    {
        var practice = new PrologueTutorialPractice(0, 0);
        practice.RecordPlayerAction(true, false);
        Assert.That(practice.Stage, Is.EqualTo(PrologueTutorialLessonStage.Guard));
        practice.RecordDefense(false, false);
        Assert.That(practice.Stage, Is.EqualTo(PrologueTutorialLessonStage.Item));
        practice.RecordPlayerAction(false, false);
        Assert.That(practice.Stage, Is.EqualTo(PrologueTutorialLessonStage.Complete));
    }
}
