using UnityEngine;

[CreateAssetMenu(
    fileName = "TrainStopDefinition",
    menuName = "HubToHome/Overworld/Travel/Train Stop")]
public sealed class TrainStopDefinition : ScriptableObject
{
    [SerializeField] private string _stopId = string.Empty;
    [SerializeField] private string _displayName = string.Empty;
    [SerializeField] private string _targetSceneName = string.Empty;
    [SerializeField] private RoomDefinition _targetRoom;
    [SerializeField] private string _targetSpawnPointId = string.Empty;
    [SerializeField] private FacingDirection _arrivalFacing = FacingDirection.Keep;
    [SerializeField] private string _unlockFlagId = string.Empty;
    [SerializeField] private int _unlockRequiredValue = 1;
    [SerializeField] private string _currentStopFlagId = string.Empty;
    [SerializeField] private DialogueData _unavailableDialogue;
    [SerializeField] private DialogueData _alreadyHereDialogue;

    public string StopId => Normalize(_stopId);
    public string DisplayName => string.IsNullOrWhiteSpace(_displayName)
        ? StopId
        : _displayName.Trim();
    public string TargetSceneName => Normalize(_targetSceneName);
    public RoomDefinition TargetRoom => _targetRoom;
    public string TargetSpawnPointId => Normalize(_targetSpawnPointId);
    public FacingDirection ArrivalFacing => _arrivalFacing;
    public string UnlockFlagId => Normalize(_unlockFlagId);
    public int UnlockRequiredValue => _unlockRequiredValue;
    public string CurrentStopFlagId => Normalize(_currentStopFlagId);
    public DialogueData UnavailableDialogue => _unavailableDialogue;
    public DialogueData AlreadyHereDialogue => _alreadyHereDialogue;

    public void Configure(
        string stopId,
        string displayName,
        string targetSceneName,
        RoomDefinition targetRoom,
        string targetSpawnPointId,
        FacingDirection arrivalFacing,
        string unlockFlagId,
        int unlockRequiredValue,
        string currentStopFlagId,
        DialogueData unavailableDialogue,
        DialogueData alreadyHereDialogue)
    {
        _stopId = Normalize(stopId);
        _displayName = Normalize(displayName);
        _targetSceneName = Normalize(targetSceneName);
        _targetRoom = targetRoom;
        _targetSpawnPointId = Normalize(targetSpawnPointId);
        _arrivalFacing = arrivalFacing;
        _unlockFlagId = Normalize(unlockFlagId);
        _unlockRequiredValue = unlockRequiredValue;
        _currentStopFlagId = Normalize(currentStopFlagId);
        _unavailableDialogue = unavailableDialogue;
        _alreadyHereDialogue = alreadyHereDialogue;
    }

    public bool IsUnlocked(GlobalDataManager global)
    {
        if (string.IsNullOrEmpty(UnlockFlagId))
            return true;

        return global != null
            && global.GetFlag(UnlockFlagId, 0) >= UnlockRequiredValue;
    }

    public bool TryValidateRuntime(out string error)
    {
        if (string.IsNullOrEmpty(StopId))
            return Fail("Stop ID가 비어 있습니다.", out error);
        if (string.IsNullOrEmpty(TargetSceneName))
            return Fail($"대상 Scene이 비어 있습니다. Stop={StopId}", out error);
        if (_targetRoom == null || !_targetRoom.IsValid)
            return Fail($"대상 RoomDefinition이 유효하지 않습니다. Stop={StopId}", out error);
        if (string.IsNullOrEmpty(TargetSpawnPointId))
            return Fail($"대상 Spawn ID가 비어 있습니다. Stop={StopId}", out error);
        if (string.IsNullOrEmpty(CurrentStopFlagId))
            return Fail($"현재 정류소 Flag ID가 비어 있습니다. Stop={StopId}", out error);
        if (!string.IsNullOrEmpty(UnlockFlagId) && _unlockRequiredValue < 0)
            return Fail($"해금 요구 값은 0 이상이어야 합니다. Stop={StopId}", out error);

        error = string.Empty;
        return true;
    }

    private static bool Fail(string message, out string error)
    {
        error = message;
        return false;
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}