using Sirenix.OdinInspector;
using UnityEngine;

public sealed class TrainDestinationInteractable : InteractableBase
{
    [BoxGroup("Train Destination")]
    [SerializeField] private TrainTravelController _controller;
    [BoxGroup("Train Destination")]
    [SerializeField] private TrainStopDefinition _destination;

    public TrainStopDefinition Destination => _destination;

    public void Configure(
        TrainTravelController controller,
        TrainStopDefinition destination)
    {
        _controller = controller;
        _destination = destination;
    }

    public override bool CanInteract(PlayerController player)
    {
        return _controller != null
            && _destination != null
            && !_controller.IsBusy
            && base.CanInteract(player);
    }

    public override void Interact(PlayerController player)
    {
        if (CanInteract(player))
            _controller.TryTravel(_destination, player, this);
    }

    private void OnValidate()
    {
        if (_controller != null && _destination != null
            && !_controller.ContainsDestination(_destination))
        {
            Debug.LogWarning("[TrainDestinationInteractable] 목적지가 Controller Network에 없습니다.", this);
        }
    }
}