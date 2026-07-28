using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public sealed class PuzzleSwitch : InteractableBase
{
    [BoxGroup("Puzzle Switch")]
    [SerializeField, Required, LabelText("Node ID")]
    private string _nodeId;

    [BoxGroup("Puzzle Switch")]
    [SerializeField, Required, LabelText("Controller")]
    private SequencePuzzleController _controller;

    [BoxGroup("Feedback")]
    [SerializeField, LabelText("정답/완료")]
    private UnityEvent _onAccepted = new UnityEvent();

    [BoxGroup("Feedback")]
    [SerializeField, LabelText("오답")]
    private UnityEvent _onRejected = new UnityEvent();

    public string NodeId => string.IsNullOrWhiteSpace(_nodeId) ? string.Empty : _nodeId.Trim();
    public SequencePuzzleController Controller => _controller;

    public void Configure(string nodeId, SequencePuzzleController controller)
    {
        _nodeId = string.IsNullOrWhiteSpace(nodeId) ? string.Empty : nodeId.Trim();
        _controller = controller;
    }

    public override bool CanInteract(PlayerController player)
    {
        return base.CanInteract(player)
            && _controller != null
            && !_controller.IsCompleted
            && !_controller.IsResetPending
            && !string.IsNullOrEmpty(NodeId);
    }

    public override void Interact(PlayerController player)
    {
        if (!CanInteract(player))
            return;

        SequencePuzzleInputResult result = _controller.Submit(NodeId);
        switch (result.Status)
        {
            case SequencePuzzleInputStatus.Advanced:
            case SequencePuzzleInputStatus.Completed:
                _onAccepted?.Invoke();
                break;

            case SequencePuzzleInputStatus.Incorrect:
                _onRejected?.Invoke();
                break;
        }
    }

    private void Reset()
    {
        _controller = GetComponentInParent<SequencePuzzleController>();
    }

    private void OnValidate()
    {
        _nodeId = string.IsNullOrWhiteSpace(_nodeId) ? string.Empty : _nodeId.Trim();
    }

    [Button("Switch 검증")]
    private void ValidateAndLog()
    {
        if (_controller == null)
        {
            Debug.LogError("[PuzzleSwitch] Controller가 지정되지 않았습니다.", this);
            return;
        }

        if (string.IsNullOrEmpty(NodeId))
        {
            Debug.LogError("[PuzzleSwitch] Node ID가 비어 있습니다.", this);
            return;
        }

        Debug.Log($"[PuzzleSwitch] 검증 통과: {NodeId}", this);
    }
}