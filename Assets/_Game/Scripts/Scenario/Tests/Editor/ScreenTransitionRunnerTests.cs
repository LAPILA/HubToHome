using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

public class ScreenTransitionRunnerTests
{
    private GameObject _overlayObject;
    private ScreenTransitionOverlay _overlay;
    private CanvasGroup _canvasGroup;
    private Image _image;

    [SetUp]
    public void SetUp()
    {
        _overlayObject = new GameObject("ScreenTransitionOverlay_Test");
        _overlay = _overlayObject.AddComponent<ScreenTransitionOverlay>();
        RunToCompletion(_overlay.FadeTo(Color.black, 0f, 0f, new ActionExecutionHandle("initialize")));
        _canvasGroup = _overlayObject.GetComponent<CanvasGroup>();
        _image = _overlayObject.GetComponentInChildren<Image>(true);
    }

    [TearDown]
    public void TearDown()
    {
        if (_overlayObject != null)
        {
            Object.DestroyImmediate(_overlayObject);
        }
    }

    [Test]
    public void FadeTo_WhenCanceled_RestoresExactPriorOverlayStateImmediately()
    {
        var priorColor = new Color(0.2f, 0.4f, 0.6f, 0.8f);
        _canvasGroup.alpha = 0.35f;
        _canvasGroup.blocksRaycasts = false;
        _image.color = priorColor;
        _overlayObject.SetActive(false);
        var handle = new ActionExecutionHandle("cancel");
        IEnumerator routine = _overlay.FadeTo(Color.white, 1f, 10f, handle);

        Assert.That(routine.MoveNext(), Is.True);
        handle.Cancel("test cancellation");

        Assert.That(_canvasGroup.alpha, Is.EqualTo(0.35f));
        Assert.That(_canvasGroup.blocksRaycasts, Is.False);
        Assert.That(_image.color, Is.EqualTo(priorColor));
        Assert.That(_overlayObject.activeSelf, Is.False);
    }

    [Test]
    public void FadeTo_WhenNewRequestStarts_StaleRequestCannotOverwriteIt()
    {
        var firstHandle = new ActionExecutionHandle("first");
        IEnumerator first = _overlay.FadeTo(Color.black, 1f, 10f, firstHandle);
        Assert.That(first.MoveNext(), Is.True);

        RunToCompletion(_overlay.FadeTo(Color.white, 0f, 0f, new ActionExecutionHandle("second")));
        RunToCompletion(first);

        Assert.That(_canvasGroup.alpha, Is.Zero);
        Assert.That(_canvasGroup.blocksRaycasts, Is.False);
        Assert.That(_image.color, Is.EqualTo(Color.white));
        Assert.That(_overlayObject.activeSelf, Is.False);
    }

    [Test]
    public void FadeTo_WhenFadeOutSucceeds_KeepsOpaqueRaycastCover()
    {
        RunToCompletion(_overlay.FadeTo(
            Color.black,
            1f,
            0f,
            new ActionExecutionHandle("cover")));

        Assert.That(_canvasGroup.alpha, Is.EqualTo(1f));
        Assert.That(_canvasGroup.blocksRaycasts, Is.True);
        Assert.That(_image.color, Is.EqualTo(Color.black));
        Assert.That(_overlayObject.activeSelf, Is.True);
    }

    private static void RunToCompletion(IEnumerator routine, int maxSteps = 100)
    {
        int steps = 0;
        while (routine.MoveNext())
        {
            steps++;
            if (steps > maxSteps)
            {
                Assert.Fail("Routine did not complete within " + maxSteps + " steps.");
            }
        }
    }
}
