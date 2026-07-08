using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

[DisallowMultipleComponent]
public abstract class AreaMarkerBase : MonoBehaviour, IInteractable
{
    [TitleGroup("기본 정보")]
    [SerializeField, Tooltip("비워두면 OnValidate/Reset에서 GameObject 기준으로 생성합니다.")]
    [LabelText("마커 ID")]
    protected string markerId;
    [TitleGroup("기본 정보")]
    [SerializeField, Tooltip("마커가 속한 Area/Room ID입니다.")]
    [LabelText("Area/Room ID")]
    protected string areaId;
    [TitleGroup("기본 정보")]
    [SerializeField, ReadOnly]
    [LabelText("마커 타입")]
    protected AreaMarkerType markerType;
    [TitleGroup("기본 정보")]
    [SerializeField]
    [LabelText("표시 이름")]
    protected string displayName;
    [TitleGroup("기본 정보")]
    [TextArea, SerializeField]
    [LabelText("설명")]
    protected string description;

    [TitleGroup("런타임 규칙")]
    [SerializeField]
    [LabelText("1회성")]
    protected bool isOneShot;
    [TitleGroup("런타임 규칙")]
    [SerializeField]
    [LabelText("필수 플래그")]
    protected string requiredFlag;
    [TitleGroup("런타임 규칙")]
    [SerializeField]
    [LabelText("완료 시 설정 플래그")]
    protected string setFlagOnComplete;
    [TitleGroup("런타임 규칙")]
    [SerializeField]
    [LabelText("상호작용 거리")]
    protected float interactionRange = 1.5f;

    [TitleGroup("에디터 표시")]
    [SerializeField]
    [LabelText("씬 뷰 라벨 표시")]
    protected bool showLabelInSceneView = true;
    [TitleGroup("에디터 표시")]
    [SerializeField]
    [LabelText("Gizmo 색상")]
    protected Color gizmoColor = Color.white;

    private bool _completed;
    private bool _highlighted;

    public string MarkerId => markerId;
    public string AreaId => areaId;
    public AreaMarkerType MarkerType => markerType;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? gameObject.name : displayName;
    public string Description => description;
    public bool ShowLabelInSceneView => showLabelInSceneView;
    public Color GizmoColor => gizmoColor;
    public float InteractionRange => Mathf.Max(0.1f, interactionRange);
    public bool IsCompleted => _completed;
    public string ShortTypeLabel => AreaMarkerDefaults.GetShortLabel(markerType);

    protected virtual void Reset()
    {
        EnsureDefaults();
    }

    protected virtual void OnValidate()
    {
        EnsureDefaults();
    }

    protected virtual void EnsureDefaults()
    {
        if (string.IsNullOrWhiteSpace(markerId)) markerId = BuildDefaultMarkerId();
        if (string.IsNullOrWhiteSpace(displayName)) displayName = gameObject.name;
        if (interactionRange <= 0f) interactionRange = 1.5f;
        if (gizmoColor == default || gizmoColor.a <= 0f) gizmoColor = AreaMarkerDefaults.GetColor(markerType);
    }

    protected virtual string BuildDefaultMarkerId()
    {
        Vector3 p = transform.position;
        return $"{markerType}_{gameObject.name}_{p.x:0.##}_{p.y:0.##}";
    }

    public virtual bool CanInteract() => CanInteract(null);

    public virtual bool CanInteract(PlayerController player)
    {
        if (!isActiveAndEnabled) return false;
        if (isOneShot && _completed) return false;
        if (isOneShot && !string.IsNullOrWhiteSpace(setFlagOnComplete) && GlobalDataManager.Instance != null)
        {
            if (GlobalDataManager.Instance.GetFlag(setFlagOnComplete, 0) != 0)
                return false;
        }

        if (!string.IsNullOrWhiteSpace(requiredFlag) && GlobalDataManager.Instance != null)
            return GlobalDataManager.Instance.GetFlag(requiredFlag, 0) != 0;
        return true;
    }

    public virtual void Interact(GameObject interactor)
    {
        Interact(interactor != null ? interactor.GetComponent<PlayerController>() : null);
    }

    public virtual void Interact(PlayerController player)
    {
        Debug.Log($"[AreaMarker] {markerType} interact: {DisplayName}", this);
    }

    public virtual void CompleteMarker()
    {
        _completed = true;
        if (!string.IsNullOrWhiteSpace(setFlagOnComplete))
            GlobalDataManager.Instance?.SetFlag(setFlagOnComplete, 1);
    }

    public virtual void ShowHighlight(bool show) => _highlighted = show;

    protected bool TryStartDialogue(
        DialogueData dialogue,
        string fallbackText,
        SpeakerData fallbackSpeaker,
        EmotionType fallbackEmotion,
        System.Action onComplete = null)
    {
        return AreaMarkerRuntimeService.TryStartDialogue(
            this,
            dialogue,
            fallbackText,
            fallbackSpeaker,
            fallbackEmotion,
            onComplete);
    }

    protected bool IsPlayerInRange(PlayerController player)
    {
        if (player == null) return true;
        return Vector2.Distance(player.transform.position, transform.position) <= InteractionRange;
    }

    [TitleGroup("검증")]
    [Button("Validate Marker")]
    public void ValidateAndLog()
    {
        var issues = new List<string>();
        CollectValidationIssues(issues);
        if (issues.Count == 0)
        {
            Debug.Log($"[AreaMarker] Validation passed: {DisplayName} ({markerType})", this);
            return;
        }

        Debug.LogError(
            $"[AreaMarker] Validation failed: {DisplayName} ({markerType})\n- " + string.Join("\n- ", issues.ToArray()),
            this);
    }

    public virtual void CollectValidationIssues(List<string> issues)
    {
        if (issues == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(markerId))
            issues.Add("markerId가 비어 있습니다.");

        if (string.IsNullOrWhiteSpace(areaId))
            issues.Add("areaId가 비어 있습니다.");

        if (GetComponent<Collider2D>() == null)
            issues.Add("Collider2D가 없습니다. Interaction/Trigger용 콜라이더가 필요합니다.");
    }

#if UNITY_EDITOR
    protected virtual void OnDrawGizmos()
    {
        if (UnityEditor.SceneView.currentDrawingSceneView == null) return;

        Color drawColor = _highlighted ? Color.white : gizmoColor;
        UnityEditor.Handles.color = drawColor;
        Gizmos.color = drawColor;
        AreaMarkerDefaults.DrawSceneIcon(markerType, transform.position, drawColor);
        Gizmos.DrawWireSphere(transform.position, InteractionRange);

        if (showLabelInSceneView)
        {
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 0.35f,
                $"[{AreaMarkerDefaults.GetShortLabel(markerType)}] {DisplayName}");
        }
    }
#endif
}

public static class AreaMarkerDefaults
{
    public static Color GetColor(AreaMarkerType type)
    {
        switch (type)
        {
            case AreaMarkerType.Connection: return new Color(0.2f, 0.7f, 1f);
            case AreaMarkerType.Enemy: return new Color(1f, 0.2f, 0.2f);
            case AreaMarkerType.Hazard: return new Color(1f, 0.55f, 0f);
            case AreaMarkerType.Puzzle: return new Color(0.65f, 0.35f, 1f);
            case AreaMarkerType.Vendor: return new Color(1f, 0.85f, 0.2f);
            case AreaMarkerType.ShortcutDoor: return new Color(0.3f, 1f, 0.8f);
            case AreaMarkerType.NPC: return new Color(0.4f, 0.9f, 0.4f);
            case AreaMarkerType.Item: return new Color(1f, 1f, 1f);
            case AreaMarkerType.Sign: return new Color(0.75f, 0.55f, 0.25f);
            case AreaMarkerType.SavePoint: return new Color(0.2f, 1f, 1f);
            case AreaMarkerType.PlotPoint: return new Color(1f, 0.2f, 1f);
            case AreaMarkerType.Sublocation: return new Color(0.55f, 0.75f, 1f);
            default: return Color.white;
        }
    }

    public static string GetShortLabel(AreaMarkerType type)
    {
        switch (type)
        {
            case AreaMarkerType.Connection: return "CON";
            case AreaMarkerType.Enemy: return "ENM";
            case AreaMarkerType.Hazard: return "HAZ";
            case AreaMarkerType.Puzzle: return "PZL";
            case AreaMarkerType.Vendor: return "VND";
            case AreaMarkerType.ShortcutDoor: return "SCD";
            case AreaMarkerType.NPC: return "NPC";
            case AreaMarkerType.Item: return "ITM";
            case AreaMarkerType.Sign: return "SGN";
            case AreaMarkerType.SavePoint: return "SAV";
            case AreaMarkerType.PlotPoint: return "PLT";
            case AreaMarkerType.Sublocation: return "SUB";
            default: return "MRK";
        }
    }

#if UNITY_EDITOR
    public static void DrawSceneIcon(AreaMarkerType type, Vector3 position, Color color)
    {
        UnityEditor.Handles.color = color;
        Quaternion rotation = Quaternion.identity;
        const float size = 0.28f;

        switch (type)
        {
            case AreaMarkerType.Connection:
            case AreaMarkerType.ShortcutDoor:
                UnityEditor.Handles.RectangleHandleCap(0, position, rotation, size, EventType.Repaint);
                break;
            case AreaMarkerType.Enemy:
            case AreaMarkerType.Hazard:
            case AreaMarkerType.PlotPoint:
                UnityEditor.Handles.ConeHandleCap(0, position, rotation, size, EventType.Repaint);
                break;
            case AreaMarkerType.Vendor:
            case AreaMarkerType.SavePoint:
                UnityEditor.Handles.CylinderHandleCap(0, position, rotation, size, EventType.Repaint);
                break;
            case AreaMarkerType.Item:
            case AreaMarkerType.Sign:
                UnityEditor.Handles.CubeHandleCap(0, position, rotation, size, EventType.Repaint);
                break;
            default:
                UnityEditor.Handles.SphereHandleCap(0, position, rotation, size, EventType.Repaint);
                break;
        }
    }
#endif
}