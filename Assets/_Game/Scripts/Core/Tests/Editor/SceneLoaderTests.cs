using System.Collections;
using System.Reflection;
using DG.Tweening;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class SceneLoaderTests
{
    private SceneLoader _previousInstance;
    private GameObject _loaderObject;
    private SceneLoaderTestDouble _loader;
    private CanvasGroup _fadeCanvas;

    [SetUp]
    public void SetUp()
    {
        _previousInstance = SceneLoader.Instance;
        SetInstance(null);

        _loaderObject = new GameObject("SceneLoaderTests");
        _fadeCanvas = _loaderObject.AddComponent<CanvasGroup>();
        _loader = _loaderObject.AddComponent<SceneLoaderTestDouble>();
        SetPrivateField(_loader, "_fadeCanvas", _fadeCanvas);
    }

    [TearDown]
    public void TearDown()
    {
        if (_fadeCanvas != null)
            _fadeCanvas.DOKill();

        Object.DestroyImmediate(_loaderObject);
        SetInstance(_previousInstance);
    }

    [Test]
    public void LoadSceneWithResult_InvalidSceneIsRejectedBeforeFade()
    {
        _loader.CanLoadScene = false;
        _fadeCanvas.alpha = 0.35f;
        _fadeCanvas.blocksRaycasts = true;
        SceneLoadResult? callbackResult = null;

        SceneLoadOperation operation = _loader.LoadSceneWithResult(
            "MissingScene",
            0.5f,
            result => callbackResult = result);

        Assert.That(operation.IsDone, Is.True);
        Assert.That(operation.Result, Is.EqualTo(SceneLoadResult.InvalidScene));
        Assert.That(callbackResult, Is.EqualTo(SceneLoadResult.InvalidScene));
        Assert.That(_fadeCanvas.alpha, Is.EqualTo(0.35f));
        Assert.That(_fadeCanvas.blocksRaycasts, Is.True);
    }

    [Test]
    public void LoadSceneWithResult_SecondRequestIsRejectedWhileBusy()
    {
        _loader.CanLoadScene = true;

        SceneLoadOperation first = _loader.LoadSceneWithResult("ValidScene", 10f);
        SceneLoadOperation second = _loader.LoadSceneWithResult("OtherScene", 0f);

        Assert.That(first.IsDone, Is.False);
        Assert.That(second.IsDone, Is.True);
        Assert.That(second.Result, Is.EqualTo(SceneLoadResult.RejectedBusy));
    }

    [UnityTest]
    public IEnumerator LoadSceneWithResult_LoadStartFailureRecoversFadeAndLock()
    {
        _loader.CanLoadScene = true;
        _loader.ReturnNullLoadOperation = true;
        SceneLoadResult? callbackResult = null;

        SceneLoadOperation operation = _loader.LoadSceneWithResult(
            "ValidScene",
            0f,
            result => callbackResult = result);

        yield return WaitForCompletion(operation);

        Assert.That(operation.Result, Is.EqualTo(SceneLoadResult.LoadFailed));
        Assert.That(callbackResult, Is.EqualTo(SceneLoadResult.LoadFailed));
        Assert.That(_fadeCanvas.alpha, Is.Zero.Within(0.001f));
        Assert.That(_fadeCanvas.blocksRaycasts, Is.False);

        _loader.CanLoadScene = false;
        SceneLoadOperation next = _loader.LoadSceneWithResult("MissingScene", 0f);
        Assert.That(next.Result, Is.EqualTo(SceneLoadResult.InvalidScene));
    }

    private static IEnumerator WaitForCompletion(SceneLoadOperation operation)
    {
        const int maxFrames = 20;
        int frame = 0;
        while (!operation.IsDone && frame++ < maxFrames)
            yield return null;

        Assert.That(operation.IsDone, Is.True, "Scene load operation did not complete in time.");
    }

    private static void SetInstance(SceneLoader instance)
    {
        PropertyInfo property = typeof(SceneLoader).GetProperty(
            nameof(SceneLoader.Instance),
            BindingFlags.Public | BindingFlags.Static);
        property.SetValue(null, instance);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = typeof(SceneLoader).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        field.SetValue(target, value);
    }
}

public sealed class SceneLoaderTestDouble : SceneLoader
{
    public bool CanLoadScene { get; set; }
    public bool ReturnNullLoadOperation { get; set; }

    protected override bool IsSceneLoadable(string sceneName)
    {
        return CanLoadScene;
    }

    protected override AsyncOperation BeginLoadSceneAsync(string sceneName)
    {
        return ReturnNullLoadOperation ? null : base.BeginLoadSceneAsync(sceneName);
    }
}
