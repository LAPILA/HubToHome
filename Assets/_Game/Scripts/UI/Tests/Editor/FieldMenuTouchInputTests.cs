using NUnit.Framework;
using UnityEngine;

public sealed class FieldMenuTouchInputTests
{
    private GameObject _owner;

    [TearDown]
    public void TearDown()
    {
        if (_owner != null) Object.DestroyImmediate(_owner);
    }

    [Test]
    public void OwnershipFollowsMenuVisibilityRatherThanPanelStackEntry()
    {
        FieldMenuTouchInput input = CreateInput(out UIPanel panel, out Canvas canvas);
        input.Initialize(panel, canvas, (RectTransform)_owner.transform, () => true);
        Assert.That(FieldMenuTouchInput.IsActive, Is.False);

        _owner.SetActive(true);
        Assert.That(FieldMenuTouchInput.IsActive, Is.True);

        // No stack entry is required: a closing menu keeps ownership until disabled.
        _owner.SetActive(false);
        Assert.That(FieldMenuTouchInput.IsActive, Is.False);
    }

    [Test]
    public void AnimationInputGuardDoesNotReleaseMenuOwnership()
    {
        FieldMenuTouchInput input = CreateInput(out UIPanel panel, out Canvas canvas);
        input.Initialize(panel, canvas, (RectTransform)_owner.transform, () => false);
        _owner.SetActive(true);
        Assert.That(FieldMenuTouchInput.IsActive, Is.True,
            "The virtual gamepad must stay hidden while menu animation rejects interaction.");
    }

    [Test]
    public void DisabledOrDestroyedAdapterReleasesOwnership()
    {
        FieldMenuTouchInput input = CreateInput(out UIPanel panel, out Canvas canvas);
        input.Initialize(panel, canvas, (RectTransform)_owner.transform, () => true);
        _owner.SetActive(true);
        input.enabled = false;
        Assert.That(FieldMenuTouchInput.IsActive, Is.False);
        input.enabled = true;
        Assert.That(FieldMenuTouchInput.IsActive, Is.True);
        Object.DestroyImmediate(input);
        Assert.That(FieldMenuTouchInput.IsActive, Is.False);
    }

    private FieldMenuTouchInput CreateInput(out UIPanel panel, out Canvas canvas)
    {
        _owner = new GameObject("Field menu touch owner", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
        canvas = _owner.GetComponent<Canvas>();
        panel = _owner.AddComponent<UIPanel>();
        _owner.SetActive(false);
        return _owner.AddComponent<FieldMenuTouchInput>();
    }
}
