using System;

/// <summary>
/// Scene 전환 중 Marker가 파괴돼도 출발 지점 완료 상태를 기록하기 위한 값 객체입니다.
/// </summary>
public readonly struct SublocationCompletionReceipt
{
    public SublocationCompletionReceipt(
        bool isOneShot,
        string sourceSceneId,
        string sourceAreaId,
        string markerId,
        string completionFlagId)
    {
        IsOneShot = isOneShot;
        SourceSceneId = Normalize(sourceSceneId);
        SourceAreaId = Normalize(sourceAreaId);
        MarkerId = Normalize(markerId);
        CompletionFlagId = Normalize(completionFlagId);
    }

    public bool IsOneShot { get; }
    public string SourceSceneId { get; }
    public string SourceAreaId { get; }
    public string MarkerId { get; }
    public string CompletionFlagId { get; }

    public bool Apply(GlobalDataManager global)
    {
        if (!IsOneShot || global == null || string.IsNullOrEmpty(MarkerId))
            return false;

        AreaMarkerStateService.MarkCompleted(
            global,
            SourceSceneId,
            SourceAreaId,
            MarkerId);
        if (!string.IsNullOrEmpty(CompletionFlagId))
            global.SetFlag(CompletionFlagId, 1);
        return true;
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
