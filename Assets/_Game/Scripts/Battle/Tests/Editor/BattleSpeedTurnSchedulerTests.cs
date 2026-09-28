using System.Collections.Generic;
using NUnit.Framework;

public sealed class BattleSpeedTurnSchedulerTests
{
    private sealed class Actor
    {
        public int Speed;
        public Actor(int speed) { Speed = speed; }
    }

    [Test]
    public void DoubleSpeedGetsTwoTurnsAndCanActConsecutively()
    {
        var fast = new Actor(20);
        var slow = new Actor(10);
        var schedule = Create(fast, slow);
        Assert.That(Take(schedule, 6), Is.EqualTo(new[] { fast, fast, slow, fast, fast, slow }));
    }

    [Test]
    public void EqualSpeedKeepsRegistrationOrderAcrossRosterReordering()
    {
        var a = new Actor(10); var b = new Actor(10); var c = new Actor(10);
        var schedule = Create(a, b, c);
        schedule.Synchronize(new[] { c, b, a }, ReadSpeed);
        Assert.That(Take(schedule, 6), Is.EqualTo(new[] { a, b, c, a, b, c }));
    }

    [Test]
    public void LongRunFrequencyMatchesSpeedRatio()
    {
        var a = new Actor(30); var b = new Actor(20); var c = new Actor(10);
        List<Actor> result = Take(Create(a, b, c), 600);
        Assert.That(result.FindAll(x => ReferenceEquals(x, a)), Has.Count.EqualTo(300));
        Assert.That(result.FindAll(x => ReferenceEquals(x, b)), Has.Count.EqualTo(200));
        Assert.That(result.FindAll(x => ReferenceEquals(x, c)), Has.Count.EqualTo(100));
    }

    [Test]
    public void PreviewIsRepeatableAndDoesNotConsumeRealTurns()
    {
        var a = new Actor(17); var b = new Actor(11); var c = new Actor(7);
        var schedule = Create(a, b, c);
        var first = new List<Actor>(); var second = new List<Actor>();
        schedule.AppendPreview(first, 37);
        schedule.AppendPreview(second, 37);
        Assert.That(second, Is.EqualTo(first));
        Assert.That(Take(schedule, 37), Is.EqualTo(first));
    }

    [Test]
    public void SpeedBuffRetainsAlreadyAccumulatedProgress()
    {
        var a = new Actor(20); var b = new Actor(10); var c = new Actor(20);
        var schedule = Create(a, b, c);
        Assert.That(Take(schedule, 2), Is.EqualTo(new[] { a, c }));
        b.Speed = 15; // Half ready: 500/15 beats 1000/20. Resetting progress would reverse this.
        schedule.Synchronize(new[] { a, b, c }, ReadSpeed);
        Assert.That(Take(schedule, 3), Is.EqualTo(new[] { b, a, c }));
    }

    [Test]
    public void SpeedDebuffChangesNextArrivalWithoutResettingEveryone()
    {
        var a = new Actor(20); var b = new Actor(10);
        var schedule = Create(a, b);
        Take(schedule, 1);
        b.Speed = 5;
        schedule.Synchronize(new[] { a, b }, ReadSpeed);
        Assert.That(Take(schedule, 3), Is.EqualTo(new[] { a, a, b }));
    }

    [Test]
    public void ReserveArrivalDoesNotResetSurvivingEnemyOrBankTurns()
    {
        var front = new Actor(20); var enemy = new Actor(10); var reserve = new Actor(10);
        var schedule = Create(front, enemy);
        Take(schedule, 1);
        schedule.Synchronize(new[] { reserve, enemy }, ReadSpeed);
        Assert.That(Take(schedule, 2), Is.EqualTo(new[] { enemy, reserve }));
        Assert.That(schedule.Count, Is.EqualTo(2));
    }

    [Test]
    public void OpeningTurnIsGrantedOnceAndPreviewDoesNotSpendIt()
    {
        var enemy = new Actor(20); var player = new Actor(5);
        var schedule = Create(enemy, player);
        Assert.That(schedule.GrantOpeningTurn(player), Is.True);
        Assert.That(schedule.GrantOpeningTurn(player), Is.False);
        var preview = new List<Actor>();
        schedule.AppendPreview(preview, 3);
        Assert.That(preview, Is.EqualTo(new[] { player, enemy, enemy }));
        Assert.That(Take(schedule, 3), Is.EqualTo(preview));
        Assert.That(schedule.GrantOpeningTurn(player), Is.False);
    }

    [Test]
    public void EmptyDeadAndDuplicateRostersAreSafe()
    {
        var a = new Actor(10); var b = new Actor(10);
        var schedule = Create(a, a, null, b);
        Assert.That(schedule.Count, Is.EqualTo(2));
        schedule.Synchronize(new Actor[0], ReadSpeed);
        Assert.That(schedule.TryTakeNext(out _), Is.False);
        schedule.Synchronize(new[] { b }, ReadSpeed);
        Assert.That(Take(schedule, 3), Is.EqualTo(new[] { b, b, b }));
    }

    [Test]
    public void InvalidSpeedClampsToOneAndMaximumIntRemainsFinite()
    {
        var a = new Actor(0); var b = new Actor(-10);
        Assert.That(Take(Create(a, b), 4), Is.EqualTo(new[] { a, b, a, b }));
        var fast = new Actor(int.MaxValue);
        List<Actor> result = Take(Create(fast, a), 1000);
        Assert.That(result.TrueForAll(x => ReferenceEquals(x, fast)), Is.True);
    }

    [Test]
    public void QueueLengthAndRepeatedSynchronizationDoNotAffectExecution()
    {
        var a = new Actor(23); var b = new Actor(9); var c = new Actor(7);
        var roster = new[] { a, b, c };
        var baseline = Create(roster); var schedule = Create(roster);
        List<Actor> expected = Take(baseline, 120);
        var actual = new List<Actor>(); var preview = new List<Actor>();
        for (int i = 0; i < expected.Count; i++)
        {
            schedule.Synchronize(roster, ReadSpeed);
            preview.Clear();
            schedule.AppendPreview(preview, i % 2 == 0 ? 6 : 17);
            schedule.TryTakeNext(out Actor next);
            Assert.That(next, Is.SameAs(preview[0]));
            actual.Add(next);
        }
        Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    public void LabSelfHaste_DoubleSpeedMovesNextTurnBeforeEnemyWithoutGrantingInstantTurn()
    {
        var wizel = new Actor(13); var support = new Actor(12);
        var striker = new Actor(11); var enemy = new Actor(25);
        var roster = new[] { wizel, support, striker, enemy };
        var schedule = Create(roster);
        Assert.That(Take(schedule, 2), Is.EqualTo(new[] { enemy, wizel }));
        var before = new List<Actor>();
        schedule.AppendPreview(before, 6);
        Assert.That(before, Is.EqualTo(new[] { enemy, support, striker, enemy, wizel, enemy }));

        wizel.Speed = 26;
        schedule.Synchronize(roster, ReadSpeed);
        var after = new List<Actor>();
        schedule.AppendPreview(after, 6);
        Assert.That(after, Is.EqualTo(new[] { enemy, support, striker, wizel, enemy, wizel }));
        Assert.That(Take(schedule, 6), Is.EqualTo(after));
    }

    [Test]
    public void HasteApplyAndRemoveWithoutTakingTurnRestoresOriginalForecast()
    {
        var a = new Actor(13); var b = new Actor(25);
        var roster = new[] { a, b };
        var schedule = Create(roster);
        Take(schedule, 2);
        var original = new List<Actor>();
        schedule.AppendPreview(original, 30);

        a.Speed *= 2;
        schedule.Synchronize(roster, ReadSpeed);
        var temporary = new List<Actor>();
        for (int i = 0; i < 100; i++)
        {
            temporary.Clear();
            schedule.AppendPreview(temporary, 30);
        }

        a.Speed /= 2;
        schedule.Synchronize(roster, ReadSpeed);
        Assert.That(Take(schedule, 30), Is.EqualTo(original));
    }

    [Test]
    public void HasteExpiryRetainsProgressEarnedByOtherActorsDuringAcceleratedTurn()
    {
        var a = new Actor(20); var b = new Actor(10);
        var roster = new[] { a, b };
        var schedule = Create(roster);
        Assert.That(Take(schedule, 1), Is.EqualTo(new[] { a })); // t=50, b half ready.
        b.Speed = 20;
        schedule.Synchronize(roster, ReadSpeed);
        Assert.That(Take(schedule, 1), Is.EqualTo(new[] { b })); // t=75, a half ready.
        b.Speed = 10;
        schedule.Synchronize(roster, ReadSpeed);
        Assert.That(Take(schedule, 3), Is.EqualTo(new[] { a, a, b })); // t=100,150,175.
    }

    [Test]
    public void SmallHasteMayKeepVisibleOrderButStillChangesLaterFrequency()
    {
        var wizel = new Actor(13); var support = new Actor(12);
        var striker = new Actor(11); var enemy = new Actor(25);
        var roster = new[] { wizel, support, striker, enemy };
        var schedule = Create(roster);
        Take(schedule, 2);
        var before = new List<Actor>();
        schedule.AppendPreview(before, 60);
        wizel.Speed = 17;
        schedule.Synchronize(roster, ReadSpeed);
        var after = new List<Actor>();
        schedule.AppendPreview(after, 60);

        // The HUD contains the active actor plus five future slots. Never force a fake reorder.
        Assert.That(after.GetRange(0, 5), Is.EqualTo(before.GetRange(0, 5)));
        Assert.That(after.FindAll(x => ReferenceEquals(x, wizel)).Count,
            Is.GreaterThan(before.FindAll(x => ReferenceEquals(x, wizel)).Count));
    }

    [Test]
    public void ForecastIncludesPendingCurrentTurnExpiryWithoutMutatingRealSpeed()
    {
        var wizel = new Actor(20); var enemy = new Actor(12);
        var roster = new[] { wizel, enemy };
        var schedule = Create(roster);
        Assert.That(Take(schedule, 1), Is.EqualTo(new[] { wizel }));
        var promised = new List<Actor>();
        // Active actor's one remaining Haste tick expires before anyone can act again.
        schedule.AppendPreview(promised, 12, (actor, turns) => actor == wizel ? 10 : actor.Speed);
        Assert.That(wizel.Speed, Is.EqualTo(20));
        wizel.Speed = 10;
        schedule.Synchronize(roster, ReadSpeed);
        Assert.That(Take(schedule, 12), Is.EqualTo(promised));
    }

    [Test]
    public void ForecastAcrossFutureHasteExpiryMatchesEveryActualTurnAndRemainingSuffix()
    {
        var wizel = new Actor(20); var enemy = new Actor(12);
        var roster = new[] { wizel, enemy };
        var schedule = Create(roster);
        int remainingHasteTurns = 3;
        System.Func<Actor, int, int> readFuture = (actor, completedTurns) =>
            actor == wizel && completedTurns >= remainingHasteTurns ? 10 : actor.Speed;
        var promised = new List<Actor>();
        schedule.AppendPreview(promised, 30, readFuture);
        var repeated = new List<Actor>();
        schedule.AppendPreview(repeated, 30, readFuture);
        Assert.That(repeated, Is.EqualTo(promised));
        Assert.That(remainingHasteTurns, Is.EqualTo(3));

        for (int i = 0; i < promised.Count; i++)
        {
            repeated.Clear();
            schedule.AppendPreview(repeated, promised.Count - i, readFuture);
            Assert.That(repeated, Is.EqualTo(promised.GetRange(i, promised.Count - i)), "at turn " + i);
            schedule.TryTakeNext(out Actor actual);
            Assert.That(actual, Is.SameAs(promised[i]), "at turn " + i);
            if (actual == wizel && remainingHasteTurns > 0 && --remainingHasteTurns == 0)
                wizel.Speed = 10;
            schedule.Synchronize(roster, ReadSpeed);
        }
    }

    [Test]
    public void ForecastIncludesSlowExpiryAndKeepsTheOtherActorsProgress()
    {
        var slowed = new Actor(5); var enemy = new Actor(12);
        var roster = new[] { slowed, enemy };
        var schedule = Create(roster);
        int slowTurns = 2;
        var promised = new List<Actor>();
        schedule.AppendPreview(promised, 24, (actor, turns) =>
            actor == slowed && turns >= slowTurns ? 10 : actor.Speed);
        for (int i = 0; i < promised.Count; i++)
        {
            schedule.TryTakeNext(out Actor actual);
            Assert.That(actual, Is.SameAs(promised[i]), "at turn " + i);
            if (actual == slowed && slowTurns > 0 && --slowTurns == 0)
                slowed.Speed = 10;
            schedule.Synchronize(roster, ReadSpeed);
        }
    }

    [Test]
    public void ChangingActorSpeedCurvesKeepEveryPreviouslyForecastSuffix()
    {
        var random = new System.Random(3340);
        for (int example = 0; example < 100; example++)
        {
            int size = random.Next(2, 7);
            var actors = new Actor[size];
            var curves = new int[size][];
            var completed = new int[size];
            for (int i = 0; i < size; i++)
            {
                curves[i] = new int[5];
                for (int turn = 0; turn < curves[i].Length; turn++) curves[i][turn] = random.Next(1, 61);
                actors[i] = new Actor(curves[i][0]);
            }
            var schedule = Create(actors);
            System.Func<Actor, int, int> futureSpeed = (actor, turns) =>
            {
                int index = System.Array.IndexOf(actors, actor);
                return curves[index][System.Math.Min(completed[index] + turns, curves[index].Length - 1)];
            };
            var promised = new List<Actor>();
            var suffix = new List<Actor>();
            schedule.AppendPreview(promised, 48, futureSpeed);
            for (int turn = 0; turn < promised.Count; turn++)
            {
                suffix.Clear();
                schedule.AppendPreview(suffix, promised.Count - turn, futureSpeed);
                Assert.That(suffix, Is.EqualTo(promised.GetRange(turn, promised.Count - turn)),
                    "example " + example + ", turn " + turn);
                schedule.TryTakeNext(out Actor actual);
                Assert.That(actual, Is.SameAs(promised[turn]));
                int index = System.Array.IndexOf(actors, actual);
                completed[index]++;
                actual.Speed = curves[index][System.Math.Min(completed[index], curves[index].Length - 1)];
                schedule.Synchronize(actors, ReadSpeed);
            }
        }
    }

    private static int ReadSpeed(Actor actor) => actor.Speed;

    private static BattleSpeedTurnScheduler<Actor> Create(params Actor[] actors)
    {
        var result = new BattleSpeedTurnScheduler<Actor>();
        result.Synchronize(actors, ReadSpeed);
        return result;
    }
    private static List<Actor> Take(BattleSpeedTurnScheduler<Actor> schedule, int count)
    {
        var result = new List<Actor>();
        for (int i = 0; i < count; i++)
        {
            Assert.That(schedule.TryTakeNext(out Actor actor), Is.True);
            result.Add(actor);
        }
        return result;
    }
}
