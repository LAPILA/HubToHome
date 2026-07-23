using Unity.Cinemachine;
using UnityEngine;

public static class OverworldCameraBinding
{
    public static bool TryApply(
        PlayerController player,
        PolygonCollider2D cameraBounds,
        Object context = null)
    {
        CameraController controller = CameraController.Instance;
        if (controller == null)
        {
            LogWarning("CameraController is missing. Overworld camera binding was skipped.", context);
            return false;
        }

        CinemachineCamera virtualCamera = controller.VirtualCamera;
        if (virtualCamera == null)
        {
            LogWarning("CameraController has no Cinemachine camera.", context);
            return false;
        }

        if (player != null)
        {
            controller.SetDefaultTarget(player.transform);
            controller.ResetCamera(0f);
        }

        CinemachineConfiner2D confiner =
            virtualCamera.GetComponent<CinemachineConfiner2D>();
        if (cameraBounds != null)
        {
            if (confiner == null)
            {
                confiner = virtualCamera.gameObject.AddComponent<CinemachineConfiner2D>();
            }

            confiner.BoundingShape2D = cameraBounds;
            confiner.enabled = true;
            confiner.InvalidateBoundingShapeCache();
        }
        else if (confiner != null)
        {
            confiner.enabled = false;
            confiner.BoundingShape2D = null;
            confiner.InvalidateBoundingShapeCache();
        }

        return true;
    }

    private static void LogWarning(string message, Object context)
    {
        if (context != null)
        {
            Debug.LogWarning($"[OverworldCameraBinding] {message}", context);
            return;
        }

        Debug.LogWarning($"[OverworldCameraBinding] {message}");
    }
}
