using Sirenix.OdinInspector;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PrologueTutorialInteractable : InteractableBase
{
    [SerializeField, LabelText("필수 조작")] private PrologueTutorialInteraction _interaction;
    [SerializeField, LabelText("선택 대사 ID")] private string _dialogueId;
    [SerializeField, LabelText("진입하면 자동 실행")] private bool _enterOnTrigger;
    private PrologueTutorialController _controller;

    public void Configure(PrologueTutorialInteraction interaction, string dialogueId = null, bool enterOnTrigger = false)
    { _interaction = interaction; _dialogueId = dialogueId; _enterOnTrigger = enterOnTrigger; }

    private void OnEnable() => ResolveController();
    private void Start() { if (_controller == null) ResolveController(); }

    private void ResolveController()
    {
        _controller = null;
        PrologueTutorialController[] controllers = FindObjectsByType<PrologueTutorialController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < controllers.Length; i++)
        {
            if (controllers[i].gameObject.scene != gameObject.scene) continue;
            if (_controller != null) { _controller = null; return; }
            _controller = controllers[i];
        }
    }

    public override bool CanInteract(PlayerController player)
        => base.CanInteract(player) && _controller != null && _controller.CanInteract(_interaction);

    public override void Interact(PlayerController player)
    {
        if (CanInteract(player)) _controller.TryInteract(_interaction, player, _dialogueId);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!_enterOnTrigger || other == null) return;
        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player != null) Interact(player);
    }
}
