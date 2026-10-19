using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class FieldMenuNavigationTests
{
    [TestCase(1, 0, 1)]
    [TestCase(-1, 0, -1)]
    [TestCase(0, -1, 2)]
    [TestCase(0, 1, -1)]
    public void NavigationUsesVisibleGeometryRatherThanPageShortcuts(float x, float y, int expected)
    {
        var controls = new[] { new Rect(0, 80, 100, 28), new Rect(110, 80, 100, 28),
            new Rect(0, 40, 100, 28), new Rect(110, 40, 100, 28) };
        Assert.That(FieldMenuNavigation.FindDirectional(controls, 0, new Vector2(x, y)), Is.EqualTo(expected));
    }

    [Test]
    public void AlignedControlWinsOverNearDiagonal()
    {
        var controls = new[] { new Rect(0, 0, 30, 30), new Rect(45, 0, 30, 30), new Rect(15, 45, 30, 30) };
        Assert.That(FieldMenuNavigation.FindDirectional(controls, 0, Vector2.right), Is.EqualTo(1));
    }

    [TestCase(-1)]
    [TestCase(3)]
    public void InvalidSelectionCannotIndexControls(int selected)
        => Assert.That(FieldMenuNavigation.FindDirectional(new[] { new Rect(0, 0, 10, 10) }, selected, Vector2.right), Is.EqualTo(-1));

    [Test]
    public void MissingControlsOrNeutralDirectionHasNoNextSelection()
    {
        Assert.That(FieldMenuNavigation.FindDirectional(null, 0, Vector2.right), Is.EqualTo(-1));
        Assert.That(FieldMenuNavigation.FindDirectional(new[] { new Rect(0, 0, 10, 10) }, 0, Vector2.zero), Is.EqualTo(-1));
    }

    [Test]
    public void RepresentativeMenuIncludingSettingsAndActionsIsReachableWithoutQeOrAd()
    {
        // Actual reference-sized control groups: tabs, actor arrows, filters, rows, actions, cards and footer.
        var controls = new List<Rect>();
        for (int i = 0; i < 4; i++) controls.Add(new Rect(12 + i * 144, -78, 136, 30));
        controls.Add(new Rect(14, -129, 28, 28)); controls.Add(new Rect(76, -129, 28, 28));
        for (int i = 0; i < 3; i++) controls.Add(new Rect(17 + i * 104, -161, 100, 28));
        for (int i = 0; i < 4; i++) controls.Add(new Rect(17, -196 - i * 33, 310, 30));
        controls.Add(new Rect(354, -306, 28, 28)); controls.Add(new Rect(386, -306, 28, 28));
        controls.Add(new Rect(422, -306, 150, 28));
        for (int i = 0; i < 3; i++) controls.Add(new Rect(12 + i * 192, -376, 184, 44));
        controls.Add(new Rect(430, -410, 68, 28)); controls.Add(new Rect(506, -410, 74, 28));
        var seen = new HashSet<int> { 0 }; var pending = new Queue<int>(); pending.Enqueue(0);
        Vector2[] directions = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };
        while (pending.Count > 0)
        {
            int current = pending.Dequeue();
            for (int i = 0; i < directions.Length; i++)
            {
                int next = FieldMenuNavigation.FindDirectional(controls, current, directions[i]);
                if (next >= 0 && seen.Add(next)) pending.Enqueue(next);
            }
        }
        Assert.That(seen.Count, Is.EqualTo(controls.Count), "Every control group including settings must have an arrow-only path.");
    }

    [TestCase(false, false, true)]
    [TestCase(false, true, true)]
    [TestCase(true, false, true)]
    [TestCase(true, true, false)]
    public void MobileTouchDoesNotAlsoGenerateMouseClicks(bool mobile, bool touch, bool expected)
        => Assert.That(FieldMenuTouchInput.CanReadMouse(mobile, touch), Is.EqualTo(expected));
}
