using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum SublocationTravelMode
{
    Enter,
    Return
}

public class SublocationMarker : AreaMarkerBase
{
    [TitleGroup("Sublocation 설정")]
    [SerializeField, LabelText("이동 모드")] private SublocationTravelMode travelMode;
    [TitleGroup("Sublocation 설정")]
    [SerializeField, LabelText("서브로케이션 ID")] private string sublocationId;
    [TitleGroup("Sublocation 설정")]
    [SerializeField, LabelText("대상 Scene")] private string targetSceneName;
    [TitleGroup("Sublocation 설정")]
    [SerializeField, LabelText("대상 Room ID")] private string targetAreaId;
    [TitleGroup("Sublocation 설정")]
    [SerializeField, LabelText("대상 SpawnPoint")] private string targetSpawnId;
    [TitleGroup("Sublocation 설정")]
    [SerializeField, LabelText("복귀 SpawnPoint")]
    [ShowIf(nameof(IsEntryMode))]
    private string returnSpawnPointId;
    [TitleGroup("Sublocation 설정")]
    [SerializeField, Min(0f), LabelText("페이드 시간")] private float fadeDuration = 0.2f;

    private bool IsEntryMode => travelMode == SublocationTravelMode.Enter;

    protected override void Reset()
    {
        markerType = AreaMarkerType.Sublocation;
        gizmoColor = AreaMarkerDefaults.GetColor(markerType);
        base.Reset();
    }

    public override void Interact(PlayerController player)
    {
        if (!CanInteract(player) || !IsPlayerInRange(player))
            return;

        if (travelMode == SublocationTravelMode.Return)
            TryReturn(player);
        else
            TryEnter(player);
    }

    public override void CollectValidationIssues(List<string> issues)
    {
        base.CollectValidationIssues(issues);
        if (string.IsNullOrWhiteSpace(sublocationId))
            issues.Add("sublocationId가 비어 있습니다.");

        if (travelMode == SublocationTravelMode.Return)
            return;

        if (string.IsNullOrWhiteSpace(targetSceneName))
            issues.Add("targetSceneName이 비어 있습니다.");
        if (string.IsNullOrWhiteSpace(targetAreaId))
            issues.Add("대상 Room ID가 비어 있습니다.");
        if (string.IsNullOrWhiteSpace(targetSpawnId))
            issues.Add("targetSpawnId가 비어 있습니다.");
        if (string.IsNullOrWhiteSpace(returnSpawnPointId))
            issues.Add("returnSpawnPointId가 비어 있습니다.");
    }

    private void TryEnter(PlayerController player)
    {
        GlobalDataManager global = GlobalDataManager.Instance;
        if (global == null || player == null)
        {
            Debug.LogError("[SublocationMarker] 복귀 주소를 기록할 GlobalDataManager 또는 Player가 없습니다.", this);
            return;
        }

        SublocationCompletionReceipt completionReceipt = CaptureCompletionReceipt();
        Scene scene = SceneManager.GetActiveScene();
        var bookmark = new MapReturnBookmark(
            scene.IsValid() ? scene.name : global.SpawnScene,
            global.CurrentRoomId,
            returnSpawnPointId,
            player.transform.position,
            ResolveFacing(player.FacingDirection));

        MapReturnBookmarkToken token;
        try
        {
            token = global.PushPendingMapReturnBookmark(bookmark);
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception, this);
            return;
        }

        var request = new MapTransitionRequest
        {
            TransitionType = MapTransitionType.Scene,
            TargetSceneName = targetSceneName,
            TargetRoomId = targetAreaId,
            TargetAreaId = targetAreaId,
            TargetSpawnPointId = targetSpawnId,
            FadeDuration = fadeDuration
        };

        bool accepted = AreaMarkerRuntimeService.RequestSublocation(
            this,
            request,
            result => CompleteEntry(global, token, completionReceipt, result));
        if (!accepted)
            global.RollbackMapReturnBookmark(token);
    }

    private void TryReturn(PlayerController player)
    {
        GlobalDataManager global = GlobalDataManager.Instance;
        if (global == null
            || !global.TryPeekMapReturnBookmark(
                out MapReturnBookmark bookmark,
                out MapReturnBookmarkToken token))
        {
            Debug.LogWarning("[SublocationMarker] 돌아갈 복귀 주소가 없습니다.", this);
            return;
        }

        SublocationCompletionReceipt completionReceipt = CaptureCompletionReceipt();
        var request = new MapTransitionRequest
        {
            TransitionType = MapTransitionType.Scene,
            TargetSceneName = bookmark.SceneName,
            TargetRoomId = bookmark.RoomId,
            TargetAreaId = bookmark.RoomId,
            TargetSpawnPointId = bookmark.SpawnPointId,
            FallbackPosition = bookmark.FallbackPosition,
            UseFallbackPosition = true,
            FacingAfterEnter = bookmark.Facing,
            FadeDuration = fadeDuration
        };

        AreaMarkerRuntimeService.RequestSublocation(
            this,
            request,
            result => CompleteReturn(global, token, completionReceipt, result));
    }

    private void CompleteEntry(
        GlobalDataManager global,
        MapReturnBookmarkToken token,
        SublocationCompletionReceipt completionReceipt,
        SceneLoadResult result)
    {
        if (global == null)
            return;

        if (!SceneLoadResultUtility.WasDestinationActivated(result))
        {
            global.RollbackMapReturnBookmark(token);
            return;
        }

        global.CommitMapReturnBookmark(token);
        if (result == SceneLoadResult.Succeeded)
            CompleteOneShotAfterSuccess(global, completionReceipt);
    }

    private void CompleteReturn(
        GlobalDataManager global,
        MapReturnBookmarkToken token,
        SublocationCompletionReceipt completionReceipt,
        SceneLoadResult result)
    {
        if (global == null || !SceneLoadResultUtility.WasDestinationActivated(result))
            return;

        global.TryPopMapReturnBookmark(token, out _);
        if (result == SceneLoadResult.Succeeded)
            CompleteOneShotAfterSuccess(global, completionReceipt);
    }

    private SublocationCompletionReceipt CaptureCompletionReceipt()
    {
        Scene sourceScene = gameObject.scene;
        return new SublocationCompletionReceipt(
            isOneShot,
            sourceScene.IsValid() ? sourceScene.name : string.Empty,
            areaId,
            markerId,
            setFlagOnComplete);
    }

    private void CompleteOneShotAfterSuccess(
        GlobalDataManager global,
        SublocationCompletionReceipt completionReceipt)
    {
        completionReceipt.Apply(global);
        if (this != null && completionReceipt.IsOneShot)
            RestoreCompletionState();
    }

    private static FacingDirection ResolveFacing(int value)
    {
        return value >= (int)FacingDirection.Down && value <= (int)FacingDirection.Right
            ? (FacingDirection)value
            : FacingDirection.Keep;
    }
}