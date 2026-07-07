using UnityEngine;

[DisallowMultipleComponent]
public abstract class AreaMarkerBase : MonoBehaviour, IInteractable
{
    [Header("Marker Identity")]
    [SerializeField, Tooltip("비워두면 OnValidate/Reset에서 GameObject 기준으로 생성합니다.")]
    protected string markerId;
    [SerializeField, Tooltip("마커가 속한 Area/Room ID입니다.")]
    protected string areaId;
    [SerializeField] protected AreaMarkerType markerType;
    [SerializeField] protected string displayName;
    [TextArea, SerializeField] protected string description;

    [Header("Runtime Rules")]
    [SerializeField] protected bool isOneShot;
    [SerializeField] protected string requiredFlag;
    [SerializeField] protected string setFlagOnComplete;
    [SerializeField] protected float interactionRange = 1.5f;

    [Header("Scene View Only")]
    [SerializeField] protected bool showLabelInSceneView = true;
    [SerializeField] protected Color gizmoColor = Color.white;

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
        DialogueManager manager = DialogueManager.Instance;
        if (manager == null)
        {
            Debug.LogWarning("[AreaMarker] DialogueManager가 없어 대화를 시작할 수 없습니다.", this);
            return false;
        }

        if (manager.IsPlaying)
        {
            Debug.Log("[AreaMarker] 이미 대화가 재생 중이라 새 Area Marker 대화를 무시합니다.", this);
            return false;
        }

        if (dialogue != null)
        {
            manager.StartDialogue(dialogue, onComplete);
            return true;
        }

        if (string.IsNullOrWhiteSpace(fallbackText))
        {
            Debug.LogWarning("[AreaMarker] DialogueData와 fallback text가 모두 비어 있습니다.", this);
            return false;
        }

        DialogueData transientDialogue = ScriptableObject.CreateInstance<DialogueData>();
        transientDialogue.name = "Runtime_AreaMarkerDialogue";
        transientDialogue.Style = DialogueStyle.Overworld;
        transientDialogue.Nodes.Add(new DialogueNode
        {
            Speaker = fallbackSpeaker,
            Emotion = fallbackEmotion,
            DefaultText = fallbackText
        });

        manager.StartDialogue(transientDialogue, () =>
        {
            Destroy(transientDialogue);
            onComplete?.Invoke();
        });

        return true;
    }

    protected bool IsPlayerInRange(PlayerController player)
    {
        if (player == null) return true;
        return Vector2.Distance(player.transform.position, transform.position) <= InteractionRange;
    }

#if UNITY_EDITOR
    protected virtual void OnDrawGizmos()
    {
        if (UnityEditor.SceneView.currentDrawingSceneView == null) return;

        Color drawColor = _highlighted ? Color.white : gizmoColor;
        UnityEditor.Handles.color = drawColor;
        Gizmos.color = drawColor;
        Gizmos.DrawWireSphere(transform.position, InteractionRange);
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

}