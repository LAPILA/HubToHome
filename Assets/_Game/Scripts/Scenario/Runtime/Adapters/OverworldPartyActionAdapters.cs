using System;
using System.Collections;

public abstract class OverworldPartyActionAdapter : IActionAdapter
{
    public abstract string ActionId { get; }

    public IEnumerator Execute(ScenarioActionData action, ActionExecutionContext context)
    {
        if (context.Handle.IsCancellationRequested || context.Handle.IsDone)
            yield break;
        if (context.GetService<IPreviewExecutionContextMarker>() != null)
        {
            context.Handle.Fail(ActionId + "은 실제 동료 상태를 변경하므로 안전 미리보기를 지원하지 않습니다.");
            yield break;
        }
        if (!string.Equals(context.PrimaryMode, "overworld", StringComparison.OrdinalIgnoreCase))
        {
            context.Handle.Fail(ActionId + "은 Overworld 전용 Action입니다.");
            yield break;
        }
        if (!OverworldPartyActionContract.TryRead(action, false, out OverworldPartyActionArguments arguments, out string error))
        {
            context.Handle.Fail(ActionId + ": " + error);
            yield break;
        }
        IOverworldPartyActionRunner runner = context.GetService<IOverworldPartyActionRunner>();
        if (runner == null)
        {
            context.Handle.Fail("IOverworldPartyActionRunner가 없습니다. 현재 씬의 등록된 필드 리더를 확인하십시오.");
            yield break;
        }

        if (ActionId == OverworldPartyActionContract.MoveId)
        {
            if (!runner.TryBeginMove(arguments.Slot, arguments.Waypoints, arguments.Duration, arguments.Rejoin,
                    out IOverworldPartyMoveOperation operation, out error) || operation == null)
            {
                context.Handle.Fail(ActionId + ": " + (string.IsNullOrEmpty(error) ? "동료 이동을 시작할 수 없습니다." : error));
                yield break;
            }

            Action<ActionExecutionHandle> cancel = _ => operation.Cancel();
            context.Handle.CancellationRequested += cancel;
            try
            {
                while (!operation.IsDone && !context.Handle.IsDone && !context.Handle.IsCancellationRequested)
                    yield return null;
                if (!context.Handle.IsDone && !context.Handle.IsCancellationRequested && !operation.WasCompleted)
                    context.Handle.Fail(ActionId + ": " + (string.IsNullOrEmpty(operation.Error)
                        ? "동료 이동이 다른 명령·맵 재배치·전투 진입으로 취소되었습니다." : operation.Error));
            }
            finally
            {
                context.Handle.CancellationRequested -= cancel;
                if (!operation.IsDone)
                    operation.Cancel();
            }
            yield break;
        }

        bool succeeded = ActionId == OverworldPartyActionContract.ModeId
            ? runner.TrySetMode(arguments.Slot, arguments.Mode, out error)
            : ActionId == OverworldPartyActionContract.VisibilityId
                ? runner.TrySetVisible(arguments.Slot, arguments.Visible, out error)
                : runner.TryGather(out error);
        if (!succeeded)
            context.Handle.Fail(ActionId + ": " + error);
    }
}

public sealed class OverworldPartyModeActionAdapter : OverworldPartyActionAdapter
{
    public const string Id = OverworldPartyActionContract.ModeId;
    public override string ActionId => Id;
}

public sealed class OverworldPartyVisibilityActionAdapter : OverworldPartyActionAdapter
{
    public const string Id = OverworldPartyActionContract.VisibilityId;
    public override string ActionId => Id;
}

public sealed class OverworldPartyMoveActionAdapter : OverworldPartyActionAdapter
{
    public const string Id = OverworldPartyActionContract.MoveId;
    public override string ActionId => Id;
}

public sealed class OverworldPartyGatherActionAdapter : OverworldPartyActionAdapter
{
    public const string Id = OverworldPartyActionContract.GatherId;
    public override string ActionId => Id;
}
