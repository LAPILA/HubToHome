using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class TrainDestinationSelectorInteractable : InteractableBase
{
    [BoxGroup("Train Destination")]
    [SerializeField] private TrainTravelController _controller;

    [BoxGroup("Train Destination")]
    [SerializeField] private List<TrainStopDefinition> _destinations =
        new List<TrainStopDefinition>();

    [BoxGroup("Train Destination")]
    [SerializeField, TextArea]
    private string _prompt = "* Select a destination.";

    private bool _isPromptOpen;

    public IReadOnlyList<TrainStopDefinition> Destinations => _destinations;

    public void Configure(
        TrainTravelController controller,
        IEnumerable<TrainStopDefinition> destinations,
        string prompt)
    {
        _controller = controller;
        _destinations.Clear();
        if (destinations != null)
            _destinations.AddRange(destinations);
        _prompt = string.IsNullOrWhiteSpace(prompt)
            ? "* Select a destination."
            : prompt.Trim();
        _isPromptOpen = false;
    }

    public override bool CanInteract(PlayerController player)
    {
        return !_isPromptOpen
            && _controller != null
            && !_controller.IsBusy
            && HasValidDestination()
            && base.CanInteract(player);
    }

    public override void Interact(PlayerController player)
    {
        if (!CanInteract(player))
            return;

        DialogueManager dialogue = DialogueManager.Instance;
        if (dialogue == null)
        {
            Debug.LogError(
                "[TrainDestinationSelector] DialogueManager is missing.",
                this);
            return;
        }

        var labels = new List<string>(_destinations.Count);
        for (int i = 0; i < _destinations.Count; i++)
            labels.Add(_destinations[i].DisplayName);

        _isPromptOpen = true;
        bool started = dialogue.TryStartChoicePrompt(
            _prompt,
            labels,
            selectedIndex => CompleteSelection(selectedIndex, player),
            CancelSelection);

        if (!started)
            _isPromptOpen = false;
    }

    private void CompleteSelection(int selectedIndex, PlayerController player)
    {
        _isPromptOpen = false;
        TryTravelTo(selectedIndex, player);
    }

    public bool TryTravelTo(int destinationIndex, PlayerController player)
    {
        if (_controller == null
            || _controller.IsBusy
            || player == null
            || destinationIndex < 0
            || destinationIndex >= _destinations.Count)
            return false;

        TrainStopDefinition destination = _destinations[destinationIndex];
        if (destination == null || !_controller.ContainsDestination(destination))
            return false;

        return _controller.TryTravel(destination, player, this);
    }

    private void CancelSelection()
    {
        _isPromptOpen = false;
    }

    private bool HasValidDestination()
    {
        if (_destinations == null || _destinations.Count == 0)
            return false;

        for (int i = 0; i < _destinations.Count; i++)
        {
            TrainStopDefinition destination = _destinations[i];
            if (destination == null || !_controller.ContainsDestination(destination))
                return false;
        }

        return true;
    }

    private void OnValidate()
    {
        if (_controller == null || _destinations == null)
            return;

        var seen = new HashSet<TrainStopDefinition>();
        for (int i = 0; i < _destinations.Count; i++)
        {
            TrainStopDefinition destination = _destinations[i];
            if (destination == null)
                continue;
            if (!seen.Add(destination))
                Debug.LogWarning("[TrainDestinationSelector] Duplicate destination.", this);
            if (!_controller.ContainsDestination(destination))
                Debug.LogWarning(
                    "[TrainDestinationSelector] Destination is not in the controller network.",
                    this);
        }
    }
}