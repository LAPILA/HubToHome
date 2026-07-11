using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class AreaMarkerGizmos
{
    private const float MarkerRadius = 0.22f;
    private const float GlyphRadius = 0.11f;

    private static GUIStyle _labelStyle;

    private static GUIStyle LabelStyle
    {
        get
        {
            if (_labelStyle != null) return _labelStyle;

            GUIStyle baseStyle = EditorStyles.boldLabel ?? GUI.skin.label;
            _labelStyle = new GUIStyle(baseStyle)
            {
                alignment = TextAnchor.MiddleCenter
            };
            _labelStyle.normal.textColor = Color.white;
            return _labelStyle;
        }
    }

    [DrawGizmo(GizmoType.NonSelected | GizmoType.Selected | GizmoType.Active)]
    private static void DrawMarker(AreaMarkerBase marker, GizmoType gizmoType)
    {
        if (marker == null) return;
        if (Camera.current == null || Camera.current.cameraType != CameraType.SceneView) return;

        Color color = marker.GizmoColor;
        Gizmos.color = color;
        Gizmos.DrawWireSphere(marker.transform.position, marker.InteractionRange);

        DrawSceneViewMarker(marker.transform.position + Vector3.up * 0.35f, marker.MarkerType, color, gizmoType);

        if (!marker.ShowLabelInSceneView) return;

        Vector3 labelPosition = marker.transform.position + Vector3.up * 0.85f;
        string label = $"[{marker.MarkerType}] {marker.DisplayName}";
        using (new Handles.DrawingScope(color))
        {
            Handles.Label(labelPosition, label, LabelStyle);
        }
    }

    private static void DrawSceneViewMarker(Vector3 position, AreaMarkerType markerType, Color color, GizmoType gizmoType)
    {
        Color fillColor = new Color(color.r, color.g, color.b, 0.72f);
        Color outlineColor = (gizmoType & GizmoType.Selected) != 0 ? Color.white : color;

        using (new Handles.DrawingScope(outlineColor))
        {
            Handles.DrawSolidDisc(position, Vector3.forward, MarkerRadius);
            Handles.color = fillColor;
            Handles.DrawSolidDisc(position, Vector3.forward, MarkerRadius * 0.82f);
            Handles.color = outlineColor;
            Handles.DrawWireDisc(position, Vector3.forward, MarkerRadius);
            DrawMarkerGlyph(position, markerType, outlineColor);
        }
    }

    private static void DrawMarkerGlyph(Vector3 center, AreaMarkerType markerType, Color color)
    {
        Handles.color = color;

        switch (markerType)
        {
            case AreaMarkerType.Connection:
            case AreaMarkerType.ShortcutDoor:
            case AreaMarkerType.Sublocation:
                DrawArrowGlyph(center);
                break;
            case AreaMarkerType.Enemy:
            case AreaMarkerType.Hazard:
                DrawCrossGlyph(center);
                break;
            case AreaMarkerType.SavePoint:
                DrawPlusGlyph(center);
                break;
            case AreaMarkerType.Item:
                DrawDiamondGlyph(center);
                break;
            case AreaMarkerType.PlotPoint:
                DrawStarGlyph(center);
                break;
            default:
                DrawListGlyph(center);
                break;
        }
    }

    private static void DrawArrowGlyph(Vector3 center)
    {
        Vector3 left = center + Vector3.left * GlyphRadius;
        Vector3 right = center + Vector3.right * GlyphRadius;
        Handles.DrawAAPolyLine(3f, left, right);
        Handles.DrawAAPolyLine(3f, right, center + new Vector3(-0.045f, 0.055f, 0f));
        Handles.DrawAAPolyLine(3f, right, center + new Vector3(-0.045f, -0.055f, 0f));
    }

    private static void DrawCrossGlyph(Vector3 center)
    {
        Vector3 diagonal = new Vector3(GlyphRadius, GlyphRadius, 0f);
        Vector3 inverseDiagonal = new Vector3(GlyphRadius, -GlyphRadius, 0f);
        Handles.DrawAAPolyLine(3f, center - diagonal, center + diagonal);
        Handles.DrawAAPolyLine(3f, center - inverseDiagonal, center + inverseDiagonal);
    }

    private static void DrawPlusGlyph(Vector3 center)
    {
        Handles.DrawAAPolyLine(3f, center + Vector3.left * GlyphRadius, center + Vector3.right * GlyphRadius);
        Handles.DrawAAPolyLine(3f, center + Vector3.down * GlyphRadius, center + Vector3.up * GlyphRadius);
    }

    private static void DrawDiamondGlyph(Vector3 center)
    {
        Vector3 top = center + Vector3.up * GlyphRadius;
        Vector3 right = center + Vector3.right * GlyphRadius;
        Vector3 bottom = center + Vector3.down * GlyphRadius;
        Vector3 left = center + Vector3.left * GlyphRadius;
        Handles.DrawAAPolyLine(3f, top, right, bottom, left, top);
    }

    private static void DrawStarGlyph(Vector3 center)
    {
        Handles.DrawAAPolyLine(3f, center + Vector3.left * GlyphRadius, center + Vector3.right * GlyphRadius);
        Handles.DrawAAPolyLine(3f, center + Vector3.down * GlyphRadius, center + Vector3.up * GlyphRadius);
        Handles.DrawAAPolyLine(3f, center + new Vector3(-0.075f, -0.075f, 0f), center + new Vector3(0.075f, 0.075f, 0f));
        Handles.DrawAAPolyLine(3f, center + new Vector3(-0.075f, 0.075f, 0f), center + new Vector3(0.075f, -0.075f, 0f));
    }

    private static void DrawListGlyph(Vector3 center)
    {
        Handles.DrawAAPolyLine(3f, center + new Vector3(-GlyphRadius, 0.07f, 0f), center + new Vector3(GlyphRadius, 0.07f, 0f));
        Handles.DrawAAPolyLine(3f, center + new Vector3(-GlyphRadius, 0f, 0f), center + new Vector3(GlyphRadius, 0f, 0f));
        Handles.DrawAAPolyLine(3f, center + new Vector3(-GlyphRadius, -0.07f, 0f), center + new Vector3(GlyphRadius, -0.07f, 0f));
    }
}