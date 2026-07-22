using NUnit.Framework;
using UnityEngine;

public sealed class BattleUIControllerCameraRebindingTests
{
    [Test]
    public void TryResolveWorldCamera_RebindsCanvasWhenCameraIsAlreadyAssigned()
    {
        GameObject cameraObject = null;
        GameObject uiObject = null;

        try
        {
            Camera expectedCamera = Camera.main;
            if (expectedCamera == null)
            {
                cameraObject = new GameObject("Test Main Camera");
                cameraObject.tag = "MainCamera";
                expectedCamera = cameraObject.AddComponent<Camera>();
            }

            uiObject = new GameObject("Inactive Battle UI", typeof(RectTransform), typeof(Canvas));
            Canvas canvas = uiObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            uiObject.SetActive(false);

            BattleUIController controller = uiObject.AddComponent<BattleUIController>();
            controller.BindWorldCamera(expectedCamera);
            canvas.worldCamera = null;

            Assert.That(controller.TryResolveWorldCamera(), Is.True);
            Assert.That(canvas.worldCamera, Is.SameAs(expectedCamera));
        }
        finally
        {
            if (uiObject != null) Object.DestroyImmediate(uiObject);
            if (cameraObject != null) Object.DestroyImmediate(cameraObject);
        }
    }
}
