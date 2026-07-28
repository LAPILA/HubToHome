using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

public sealed class UIRuntimeGuardTests
{
    private GameObject _root;

    [TearDown]
    public void TearDown()
    {
        if (_root != null)
            Object.DestroyImmediate(_root);
    }

    [Test]
    public void NormalizeCanvasUsesGameReferenceResolutionAndRepairsZeroScale()
    {
        _root = new GameObject(
            "UI Runtime Guard Test",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler));
        _root.transform.localScale = Vector3.zero;

        GameObject owner = new GameObject("Owner", typeof(RectTransform));
        owner.transform.SetParent(_root.transform, false);

        CanvasScaler scaler = _root.GetComponent<CanvasScaler>();
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        UIRuntimeGuard.NormalizeCanvas(owner);

        Assert.That(scaler.uiScaleMode, Is.EqualTo(CanvasScaler.ScaleMode.ScaleWithScreenSize));
        Assert.That(scaler.referenceResolution, Is.EqualTo(GameConfigPolicy.ReferenceResolution));
        Assert.That(scaler.screenMatchMode, Is.EqualTo(CanvasScaler.ScreenMatchMode.Expand));
        Assert.That(_root.transform.localScale, Is.EqualTo(Vector3.one));
    }
}