using System;
using UnityEngine;

/// <summary>전투 안전영역 안의 화면 관통 경로입니다. 전투 수치나 카메라를 변경하지 않습니다.</summary>
public readonly struct BattleScreenPassPath
{
    public readonly Vector3 Left;
    public readonly Vector3 Right;
    public readonly float TargetX;

    public BattleScreenPassPath(Vector3 left, Vector3 right, float targetX)
    {
        Left = left;
        Right = right;
        TargetX = targetX;
    }

    public float CrossingProgress(bool leftToRight)
    {
        float progress = (TargetX - Left.x) / (Right.x - Left.x);
        return leftToRight ? progress : 1f - progress;
    }
}

/// <summary>월드로 투영된 HUD 제외 영역과 실제 몸체 크기로 안전한 좌우 끝점을 계산합니다.</summary>
public static class BattleScreenPassGeometry
{
    public static bool TryCreate(Rect safeWorld, Vector2 bodyOffset, Vector2 halfSize, float margin,
        int pixelsPerUnit, float laneY, float z, float targetX, out BattleScreenPassPath path)
    {
        path = default;
        if (!Finite(safeWorld.xMin) || !Finite(safeWorld.xMax) || !Finite(safeWorld.yMin)
            || !Finite(safeWorld.yMax) || safeWorld.width <= 0f || safeWorld.height <= 0f
            || !Finite(bodyOffset.x) || !Finite(bodyOffset.y) || !Finite(halfSize.x) || !Finite(halfSize.y)
            || halfSize.x < 0f || halfSize.y < 0f || !Finite(margin) || margin < 0f
            || pixelsPerUnit <= 0 || !Finite(laneY) || !Finite(z) || !Finite(targetX)) return false;

        // 바깥쪽 반올림으로 몸체가 UI 영역을 침범하지 않도록 안쪽 격자를 선택합니다.
        float left = CeilGrid(safeWorld.xMin + halfSize.x + margin - bodyOffset.x, pixelsPerUnit);
        float right = FloorGrid(safeWorld.xMax - halfSize.x - margin - bodyOffset.x, pixelsPerUnit);
        float bottom = CeilGrid(safeWorld.yMin + halfSize.y + margin - bodyOffset.y, pixelsPerUnit);
        float top = FloorGrid(safeWorld.yMax - halfSize.y - margin - bodyOffset.y, pixelsPerUnit);
        if (left >= right || bottom > top || targetX <= left || targetX >= right) return false;
        float y = CameraPixelGridPolicy.SnapCoordinate(Mathf.Clamp(laneY, bottom, top), pixelsPerUnit);
        y = Mathf.Clamp(y, bottom, top);
        path = new BattleScreenPassPath(new Vector3(left, y, z), new Vector3(right, y, z), targetX);
        return true;
    }

    private static float CeilGrid(float value, int pixelsPerUnit) => (float)(Math.Ceiling((double)value * pixelsPerUnit) / pixelsPerUnit);
    private static float FloorGrid(float value, int pixelsPerUnit) => (float)(Math.Floor((double)value * pixelsPerUnit) / pixelsPerUnit);
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
