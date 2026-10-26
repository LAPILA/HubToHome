using System.Collections.Generic;
using NUnit.Framework;

public class BattleFlagPresentationTests
{
    [Test]
    public void OnlyRealFlagChangesNotifyAfterMutation()
    {
        var state = BattleSessionState.Create(null);
        var values = new List<string>();
        state.FlagChanged += key => values.Add(state.TryGetFlagValue(key, out string value) ? value : "missing");
        Assert.That(state.SetFlag(" phase ", "true"), Is.True);
        state.SetFlag("phase", "true");
        state.SetFlag("phase", "false");
        state.ClearFlag("phase");
        state.ClearFlag("phase");
        CollectionAssert.AreEqual(new[] { "true", "false", "missing" }, values);
    }

    [Test]
    public void FlagGateIsIdempotentAndNeverPersistsAcrossSessions()
    {
        var state = BattleSessionState.Create(null);
        var visible = new List<bool>();
        var gate = new BattleFlagPresentationGate(state, "phase", value => visible.Add(value));
        state.SetFlag("other", "true");
        state.SetFlag("phase", "true");
        state.SetFlag("phase", "true");
        state.SetFlag("phase", "false");
        state.SetFlag("phase", "true");
        gate.Dispose();
        gate.Dispose();
        state.ClearFlag("phase");
        state.SetFlag("phase", "true");
        CollectionAssert.AreEqual(new[] { true, false, true, false }, visible);
        Assert.That(gate.IsShown, Is.False);
        Assert.That(BattleSessionState.Create(null).HasFlag("phase"), Is.False);
    }

    [Test]
    public void BindingToActiveStateShowsOnceAndClearHides()
    {
        var state = BattleSessionState.Create(null);
        state.SetFlag("phase", "true");
        int shows = 0, hides = 0;
        using var gate = new BattleFlagPresentationGate(state, "phase", enabled => { if (enabled) shows++; else hides++; });
        Assert.That(shows, Is.EqualTo(1));
        state.ClearFlag("phase");
        Assert.That(hides, Is.EqualTo(1));
    }
}
