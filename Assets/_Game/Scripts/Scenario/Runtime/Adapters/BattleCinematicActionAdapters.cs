using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public interface IBattleCinematicRunner
{
    IEnumerator SetLetterbox(bool visible, float thickness, float duration, ActionExecutionHandle handle);

    IEnumerator FocusCamera(string subjectId, float zoom, float duration, CameraShotStyle style, ActionExecutionHandle handle);

    IEnumerator ResetCamera(float duration, CameraShotStyle style, ActionExecutionHandle handle);

    IEnumerator ShakeCamera(Vector3 direction, float intensity, float duration, CameraShakeSafety safety, ActionExecutionHandle handle);

    IEnumerator PlayActorPose(string subjectId, string pose, float duration, float impact, ActionExecutionHandle handle);

    IEnumerator SetActorFlip(string subjectId, string mode, ActionExecutionHandle handle);

    IEnumerator MoveActor(string subjectId, string anchor, float x, float y, float duration, string pose, float impact, ActionExecutionHandle handle);

    IEnumerator DropActorIn(string subjectId, float height, float hangDuration, float fallDuration, float settleDuration, float impact, ActionExecutionHandle handle);

    IEnumerator PlayFakeAttack(
        string actorId,
        string targetId,
        string targetPose,
        float approachDistance,
        float lungeDuration,
        float holdDuration,
        float recoverDuration,
        float impact,
        ActionExecutionHandle handle);

    IEnumerator ReturnActorsToSlots(float duration, ActionExecutionHandle handle);
}

public sealed class CinematicLetterboxActionAdapter : IActionAdapter
{
    public const string Id = "cinematic.letterbox";

    public string ActionId
    {
        get { return Id; }
    }

    public IEnumerator Execute(ScenarioActionData action, ActionExecutionContext context)
    {
        IBattleCinematicRunner runner = context.GetService<IBattleCinematicRunner>();
        if (runner == null)
        {
            BattleCinematicActionAdapterSafety.Warn("cinematic.letterbox skipped because IBattleCinematicRunner is missing.");
            yield break;
        }

        string mode;
        string error;
        if (!ScenarioActionParameterReader.TryGetString(action, "mode", out mode, out error))
        {
            BattleCinematicActionAdapterSafety.Warn(error);
            mode = "show";
        }

        bool visible = !string.Equals(mode != null ? mode.Trim() : string.Empty, "hide", System.StringComparison.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(mode))
        {
            visible = true;
        }

        float thickness;
        if (!ScenarioActionParameterReader.TryGetFloat(action, "thickness", 0.12f, out thickness, out error))
        {
            BattleCinematicActionAdapterSafety.Warn(error);
            thickness = 0.12f;
        }

        float duration;
        if (!ScenarioActionParameterReader.TryGetFloat(action, "duration", 0.2f, out duration, out error))
        {
            BattleCinematicActionAdapterSafety.Warn(error);
            duration = 0.2f;
        }

        IEnumerator routine = runner.SetLetterbox(visible, thickness, duration, context.Handle);
        while (routine.MoveNext())
        {
            yield return routine.Current;
        }
    }
}

public sealed class BattleCameraFocusActionAdapter : IActionAdapter
{
    public const string Id = "battle.camera.focus";
    public string ActionId => Id;

    public IEnumerator Execute(ScenarioActionData action, ActionExecutionContext context)
    {
        IBattleCinematicRunner runner = context.GetService<IBattleCinematicRunner>();
        if (runner == null)
        {
            context.Handle.Fail("IBattleCinematicRunner is missing for battle.camera.focus.");
            yield break;
        }

        if (!BattleCameraActionParsing.TryReadRequiredString(action, "subject", out string subject, out string error)
            || !BattleCameraActionParsing.TryReadPositiveFloat(action, "zoom", 3.2f, out float zoom, out error)
            || !BattleCameraActionParsing.TryReadNonNegativeFloat(action, "duration", 0.2f, out float duration, out error)
            || !BattleCameraActionParsing.TryReadStyle(action, "style", CameraShotStyle.Dynamic, out CameraShotStyle style, out error))
        {
            context.Handle.Fail(error);
            yield break;
        }

        IEnumerator routine = runner.FocusCamera(subject, zoom, duration, style, context.Handle);
        while (!context.Handle.IsDone && routine.MoveNext())
        {
            yield return routine.Current;
        }
    }
}

public sealed class BattleCameraResetActionAdapter : IActionAdapter
{
    public const string Id = "battle.camera.reset";
    public string ActionId => Id;

    public IEnumerator Execute(ScenarioActionData action, ActionExecutionContext context)
    {
        IBattleCinematicRunner runner = context.GetService<IBattleCinematicRunner>();
        if (runner == null)
        {
            context.Handle.Fail("IBattleCinematicRunner is missing for battle.camera.reset.");
            yield break;
        }

        if (!BattleCameraActionParsing.TryReadNonNegativeFloat(action, "duration", 0.35f, out float duration, out string error)
            || !BattleCameraActionParsing.TryReadStyle(action, "style", CameraShotStyle.GameplaySafe, out CameraShotStyle style, out error))
        {
            context.Handle.Fail(error);
            yield break;
        }

        IEnumerator routine = runner.ResetCamera(duration, style, context.Handle);
        while (!context.Handle.IsDone && routine.MoveNext())
        {
            yield return routine.Current;
        }
    }
}

public sealed class BattleCameraShakeActionAdapter : IActionAdapter
{
    public const string Id = "battle.camera.shake";
    public string ActionId => Id;

    public IEnumerator Execute(ScenarioActionData action, ActionExecutionContext context)
    {
        IBattleCinematicRunner runner = context.GetService<IBattleCinematicRunner>();
        if (runner == null)
        {
            context.Handle.Fail("IBattleCinematicRunner is missing for battle.camera.shake.");
            yield break;
        }

        if (!BattleCameraActionParsing.TryReadDirection(action, out Vector3 direction, out string error)
            || !BattleCameraActionParsing.TryReadPositiveFloat(action, "intensity", 0.5f, out float intensity, out error)
            || !BattleCameraActionParsing.TryReadPositiveFloat(action, "duration", 0.12f, out float duration, out error)
            || !BattleCameraActionParsing.TryReadSafety(action, out CameraShakeSafety safety, out error))
        {
            context.Handle.Fail(error);
            yield break;
        }

        IEnumerator routine = runner.ShakeCamera(direction, intensity, duration, safety, context.Handle);
        while (!context.Handle.IsDone && routine.MoveNext())
        {
            yield return routine.Current;
        }
    }
}

internal static class BattleCameraActionParsing
{
    public static bool TryReadRequiredString(ScenarioActionData action, string name, out string value, out string error)
    {
        if (!ScenarioActionParameterReader.TryGetString(action, name, out value, out error)
            || string.IsNullOrWhiteSpace(value))
        {
            error = string.IsNullOrWhiteSpace(error) ? "Parameter '" + name + "' is required." : error;
            value = string.Empty;
            return false;
        }

        value = value.Trim();
        return true;
    }

    public static bool TryReadPositiveFloat(ScenarioActionData action, string name, float fallback, out float value, out string error)
    {
        if (!ScenarioActionParameterReader.TryGetFloat(action, name, fallback, out value, out error))
            return false;
        if (value > 0f)
            return true;
        error = "Parameter '" + name + "' must be greater than zero.";
        return false;
    }

    public static bool TryReadNonNegativeFloat(ScenarioActionData action, string name, float fallback, out float value, out string error)
    {
        if (!ScenarioActionParameterReader.TryGetFloat(action, name, fallback, out value, out error))
            return false;
        if (value >= 0f)
            return true;
        error = "Parameter '" + name + "' must be zero or greater.";
        return false;
    }

    public static bool TryReadStyle(ScenarioActionData action, string name, CameraShotStyle fallback, out CameraShotStyle style, out string error)
    {
        style = fallback;
        if (!ScenarioActionParameterReader.TryGetString(action, name, out string raw, out error))
            return false;
        if (string.IsNullOrWhiteSpace(raw))
            return true;

        string normalized = raw.Trim().Replace("-", "_").ToLowerInvariant();
        switch (normalized)
        {
            case "static": style = CameraShotStyle.Static; return true;
            case "dynamic": style = CameraShotStyle.Dynamic; return true;
            case "gameplay_safe":
            case "gameplaysafe": style = CameraShotStyle.GameplaySafe; return true;
            default:
                error = "Unknown camera style: " + raw;
                return false;
        }
    }

    public static bool TryReadDirection(ScenarioActionData action, out Vector3 direction, out string error)
    {
        direction = Vector3.zero;
        if (!TryReadRequiredString(action, "direction", out string raw, out error))
            return false;
        switch (raw.ToLowerInvariant())
        {
            case "left": direction = Vector3.left; return true;
            case "right": direction = Vector3.right; return true;
            case "up": direction = Vector3.up; return true;
            case "down": direction = Vector3.down; return true;
            default:
                error = "Unknown camera shake direction: " + raw;
                return false;
        }
    }

    public static bool TryReadSafety(ScenarioActionData action, out CameraShakeSafety safety, out string error)
    {
        safety = CameraShakeSafety.GameplaySafe;
        if (!ScenarioActionParameterReader.TryGetString(action, "safety", out string raw, out error))
            return false;
        if (string.IsNullOrWhiteSpace(raw))
            return true;
        string normalized = raw.Trim().Replace("-", "_").ToLowerInvariant();
        if (normalized == "gameplay_safe" || normalized == "gameplaysafe")
            return true;
        if (normalized == "cinematic")
        {
            safety = CameraShakeSafety.Cinematic;
            return true;
        }
        error = "Unknown camera shake safety: " + raw;
        return false;
    }
}

public sealed class BattleActorPoseActionAdapter : IActionAdapter
{
    public const string Id = "battle.actor.pose";

    public string ActionId
    {
        get { return Id; }
    }

    public IEnumerator Execute(ScenarioActionData action, ActionExecutionContext context)
    {
        IBattleCinematicRunner runner = context.GetService<IBattleCinematicRunner>();
        if (runner == null)
        {
            BattleCinematicActionAdapterSafety.Warn("battle.actor.pose skipped because IBattleCinematicRunner is missing.");
            yield break;
        }

        string actor;
        string error;
        if (!ScenarioActionParameterReader.TryGetString(action, "actor", out actor, out error))
        {
            BattleCinematicActionAdapterSafety.Warn(error);
            yield break;
        }

        if (string.IsNullOrWhiteSpace(actor))
        {
            BattleCinematicActionAdapterSafety.Warn("battle.actor.pose skipped because parameter 'actor' is missing.");
            yield break;
        }

        string pose;
        if (!ScenarioActionParameterReader.TryGetString(action, "pose", out pose, out error))
        {
            BattleCinematicActionAdapterSafety.Warn(error);
            pose = "idle";
        }

        if (string.IsNullOrWhiteSpace(pose))
        {
            pose = "idle";
        }

        float duration;
        if (!ScenarioActionParameterReader.TryGetFloat(action, "duration", 0.25f, out duration, out error))
        {
            BattleCinematicActionAdapterSafety.Warn(error);
            duration = 0.25f;
        }

        float impact;
        if (!ScenarioActionParameterReader.TryGetFloat(action, "impact", 0f, out impact, out error))
        {
            BattleCinematicActionAdapterSafety.Warn(error);
            impact = 0f;
        }

        IEnumerator routine = runner.PlayActorPose(actor.Trim(), pose.Trim(), duration, impact, context.Handle);
        while (routine.MoveNext())
        {
            yield return routine.Current;
        }
    }
}

public sealed class BattleActorFlipActionAdapter : IActionAdapter
{
    public const string Id = "battle.actor.flip";

    public string ActionId
    {
        get { return Id; }
    }

    public IEnumerator Execute(ScenarioActionData action, ActionExecutionContext context)
    {
        IBattleCinematicRunner runner = context.GetService<IBattleCinematicRunner>();
        if (runner == null)
        {
            BattleCinematicActionAdapterSafety.Warn("battle.actor.flip skipped because IBattleCinematicRunner is missing.");
            yield break;
        }

        string actor;
        string error;
        if (!ScenarioActionParameterReader.TryGetString(action, "actor", out actor, out error))
        {
            BattleCinematicActionAdapterSafety.Warn(error);
            yield break;
        }

        if (string.IsNullOrWhiteSpace(actor))
        {
            BattleCinematicActionAdapterSafety.Warn("battle.actor.flip skipped because parameter 'actor' is missing.");
            yield break;
        }

        string mode;
        if (!ScenarioActionParameterReader.TryGetString(action, "mode", out mode, out error))
        {
            BattleCinematicActionAdapterSafety.Warn(error);
            mode = "default";
        }

        if (string.IsNullOrWhiteSpace(mode))
        {
            mode = "default";
        }

        IEnumerator routine = runner.SetActorFlip(actor.Trim(), mode.Trim(), context.Handle);
        while (routine.MoveNext())
        {
            yield return routine.Current;
        }
    }
}

public sealed class BattleActorMoveActionAdapter : IActionAdapter
{
    public const string Id = "battle.actor.move_to";

    public string ActionId
    {
        get { return Id; }
    }

    public IEnumerator Execute(ScenarioActionData action, ActionExecutionContext context)
    {
        IBattleCinematicRunner runner = context.GetService<IBattleCinematicRunner>();
        if (runner == null)
        {
            BattleCinematicActionAdapterSafety.Warn("battle.actor.move_to skipped because IBattleCinematicRunner is missing.");
            yield break;
        }

        string actor;
        string error;
        if (!ScenarioActionParameterReader.TryGetString(action, "actor", out actor, out error))
        {
            BattleCinematicActionAdapterSafety.Warn(error);
            yield break;
        }

        if (string.IsNullOrWhiteSpace(actor))
        {
            BattleCinematicActionAdapterSafety.Warn("battle.actor.move_to skipped because parameter 'actor' is missing.");
            yield break;
        }

        string anchor;
        if (!ScenarioActionParameterReader.TryGetString(action, "anchor", out anchor, out error))
        {
            BattleCinematicActionAdapterSafety.Warn(error);
            anchor = "current";
        }

        if (string.IsNullOrWhiteSpace(anchor))
        {
            anchor = "current";
        }

        float x;
        if (!ScenarioActionParameterReader.TryGetFloat(action, "x", 0f, out x, out error))
        {
            BattleCinematicActionAdapterSafety.Warn(error);
            x = 0f;
        }

        float y;
        if (!ScenarioActionParameterReader.TryGetFloat(action, "y", 0f, out y, out error))
        {
            BattleCinematicActionAdapterSafety.Warn(error);
            y = 0f;
        }

        float duration;
        if (!ScenarioActionParameterReader.TryGetFloat(action, "duration", 0.25f, out duration, out error))
        {
            BattleCinematicActionAdapterSafety.Warn(error);
            duration = 0.25f;
        }

        string pose;
        if (!ScenarioActionParameterReader.TryGetString(action, "pose", out pose, out error))
        {
            BattleCinematicActionAdapterSafety.Warn(error);
            pose = "move";
        }

        if (string.IsNullOrWhiteSpace(pose))
        {
            pose = "move";
        }

        float impact;
        if (!ScenarioActionParameterReader.TryGetFloat(action, "impact", 0f, out impact, out error))
        {
            BattleCinematicActionAdapterSafety.Warn(error);
            impact = 0f;
        }

        IEnumerator routine = runner.MoveActor(actor.Trim(), anchor.Trim(), x, y, duration, pose.Trim(), impact, context.Handle);
        while (routine.MoveNext())
        {
            yield return routine.Current;
        }
    }
}

public sealed class BattleActorFakeAttackActionAdapter : IActionAdapter
{
    public const string Id = "battle.actor.fake_attack";

    public string ActionId
    {
        get { return Id; }
    }

    public IEnumerator Execute(ScenarioActionData action, ActionExecutionContext context)
    {
        IBattleCinematicRunner runner = context.GetService<IBattleCinematicRunner>();
        if (runner == null)
        {
            BattleCinematicActionAdapterSafety.Warn("battle.actor.fake_attack skipped because IBattleCinematicRunner is missing.");
            yield break;
        }

        string actor;
        string error;
        if (!ScenarioActionParameterReader.TryGetString(action, "actor", out actor, out error))
        {
            BattleCinematicActionAdapterSafety.Warn(error);
            yield break;
        }

        string target;
        if (!ScenarioActionParameterReader.TryGetString(action, "target", out target, out error))
        {
            BattleCinematicActionAdapterSafety.Warn(error);
            yield break;
        }

        if (string.IsNullOrWhiteSpace(actor) || string.IsNullOrWhiteSpace(target))
        {
            BattleCinematicActionAdapterSafety.Warn("battle.actor.fake_attack skipped because parameter 'actor' or 'target' is missing.");
            yield break;
        }

        string targetPose;
        if (!ScenarioActionParameterReader.TryGetString(action, "targetPose", out targetPose, out error))
        {
            BattleCinematicActionAdapterSafety.Warn(error);
            targetPose = "hurt";
        }

        if (string.IsNullOrWhiteSpace(targetPose))
        {
            targetPose = "hurt";
        }

        float approachDistance;
        if (!ScenarioActionParameterReader.TryGetFloat(action, "approach", 0.85f, out approachDistance, out error))
        {
            BattleCinematicActionAdapterSafety.Warn(error);
            approachDistance = 0.85f;
        }

        float lungeDuration;
        if (!ScenarioActionParameterReader.TryGetFloat(action, "lunge", 0.12f, out lungeDuration, out error))
        {
            BattleCinematicActionAdapterSafety.Warn(error);
            lungeDuration = 0.12f;
        }

        float holdDuration;
        if (!ScenarioActionParameterReader.TryGetFloat(action, "hold", 0.05f, out holdDuration, out error))
        {
            BattleCinematicActionAdapterSafety.Warn(error);
            holdDuration = 0.05f;
        }

        float recoverDuration;
        if (!ScenarioActionParameterReader.TryGetFloat(action, "recover", 0.18f, out recoverDuration, out error))
        {
            BattleCinematicActionAdapterSafety.Warn(error);
            recoverDuration = 0.18f;
        }

        float impact;
        if (!ScenarioActionParameterReader.TryGetFloat(action, "impact", 0.6f, out impact, out error))
        {
            BattleCinematicActionAdapterSafety.Warn(error);
            impact = 0.6f;
        }

        IEnumerator routine = runner.PlayFakeAttack(
            actor.Trim(),
            target.Trim(),
            targetPose.Trim(),
            approachDistance,
            lungeDuration,
            holdDuration,
            recoverDuration,
            impact,
            context.Handle);
        while (routine.MoveNext())
        {
            yield return routine.Current;
        }
    }
}

public sealed class BattleActorDropInActionAdapter : IActionAdapter
{
    public const string Id = "battle.actor.drop_in";

    public string ActionId
    {
        get { return Id; }
    }

    public IEnumerator Execute(ScenarioActionData action, ActionExecutionContext context)
    {
        IBattleCinematicRunner runner = context.GetService<IBattleCinematicRunner>();
        if (runner == null)
        {
            BattleCinematicActionAdapterSafety.Warn("battle.actor.drop_in skipped because IBattleCinematicRunner is missing.");
            yield break;
        }

        string actor;
        string error;
        if (!ScenarioActionParameterReader.TryGetString(action, "actor", out actor, out error))
        {
            BattleCinematicActionAdapterSafety.Warn(error);
            yield break;
        }

        if (string.IsNullOrWhiteSpace(actor))
        {
            BattleCinematicActionAdapterSafety.Warn("battle.actor.drop_in skipped because parameter 'actor' is missing.");
            yield break;
        }

        float height;
        if (!ScenarioActionParameterReader.TryGetFloat(action, "height", 3.5f, out height, out error))
        {
            BattleCinematicActionAdapterSafety.Warn(error);
            height = 3.5f;
        }

        float hangDuration;
        if (!ScenarioActionParameterReader.TryGetFloat(action, "hang", 0.18f, out hangDuration, out error))
        {
            BattleCinematicActionAdapterSafety.Warn(error);
            hangDuration = 0.18f;
        }

        float fallDuration;
        if (!ScenarioActionParameterReader.TryGetFloat(action, "fall", 0.22f, out fallDuration, out error))
        {
            BattleCinematicActionAdapterSafety.Warn(error);
            fallDuration = 0.22f;
        }

        float settleDuration;
        if (!ScenarioActionParameterReader.TryGetFloat(action, "settle", 0.12f, out settleDuration, out error))
        {
            BattleCinematicActionAdapterSafety.Warn(error);
            settleDuration = 0.12f;
        }

        float impact;
        if (!ScenarioActionParameterReader.TryGetFloat(action, "impact", 1.1f, out impact, out error))
        {
            BattleCinematicActionAdapterSafety.Warn(error);
            impact = 1.1f;
        }

        IEnumerator routine = runner.DropActorIn(
            actor.Trim(),
            height,
            hangDuration,
            fallDuration,
            settleDuration,
            impact,
            context.Handle);
        while (routine.MoveNext())
        {
            yield return routine.Current;
        }
    }
}

public sealed class BattleActorReturnSlotsActionAdapter : IActionAdapter
{
    public const string Id = "battle.actor.return_slots";

    public string ActionId
    {
        get { return Id; }
    }

    public IEnumerator Execute(ScenarioActionData action, ActionExecutionContext context)
    {
        IBattleCinematicRunner runner = context.GetService<IBattleCinematicRunner>();
        if (runner == null)
        {
            BattleCinematicActionAdapterSafety.Warn("battle.actor.return_slots skipped because IBattleCinematicRunner is missing.");
            yield break;
        }

        string error;
        float duration;
        if (!ScenarioActionParameterReader.TryGetFloat(action, "duration", 0.28f, out duration, out error))
        {
            BattleCinematicActionAdapterSafety.Warn(error);
            duration = 0.28f;
        }

        IEnumerator routine = runner.ReturnActorsToSlots(duration, context.Handle);
        while (routine.MoveNext())
        {
            yield return routine.Current;
        }
    }
}

internal static class BattleCinematicActionAdapterSafety
{
    public static void Warn(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        Debug.LogWarning("[BattleCinematicAction] " + message);
    }
}

public sealed class CinematicLetterboxOverlay : MonoBehaviour
{
    private const string OverlayName = "ScenarioCinematicLetterboxOverlay";
    private static CinematicLetterboxOverlay _instance;

    private RectTransform _top;
    private RectTransform _bottom;
    private float _currentThickness;

    public float CurrentThickness
    {
        get { return _currentThickness; }
    }

    public static CinematicLetterboxOverlay GetOrCreate()
    {
        if (_instance != null)
        {
            return _instance;
        }

        var root = new GameObject(OverlayName);
        DontDestroyOnLoad(root);
        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue - 1;
        root.AddComponent<CanvasScaler>();
        root.AddComponent<GraphicRaycaster>();
        _instance = root.AddComponent<CinematicLetterboxOverlay>();
        _instance.EnsureInitialized();
        _instance.ApplyThickness(0f);
        return _instance;
    }

    public IEnumerator SetVisible(bool visible, float thickness, float duration, ActionExecutionHandle handle)
    {
        EnsureInitialized();
        float target = visible ? Mathf.Clamp01(thickness) : 0f;
        float start = _currentThickness;
        float clampedDuration = Mathf.Max(0f, duration);
        gameObject.SetActive(true);

        if (clampedDuration <= 0f)
        {
            ApplyThickness(target);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < clampedDuration)
        {
            if (handle != null && handle.IsCancellationRequested)
            {
                yield break;
            }

            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / clampedDuration);
            ApplyThickness(Mathf.Lerp(start, target, t));
            yield return null;
        }

        ApplyThickness(target);
    }

    public void SetThicknessImmediate(float thickness)
    {
        EnsureInitialized();
        gameObject.SetActive(true);
        ApplyThickness(thickness);
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        EnsureInitialized();
    }

    private void EnsureInitialized()
    {
        if (_top != null && _bottom != null)
        {
            return;
        }

        _top = CreateBar("TopBar", true);
        _bottom = CreateBar("BottomBar", false);
    }

    private RectTransform CreateBar(string objectName, bool top)
    {
        var bar = new GameObject(objectName);
        bar.transform.SetParent(transform, false);
        Image image = bar.AddComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = false;
        RectTransform rect = bar.GetComponent<RectTransform>();
        rect.anchorMin = top ? new Vector2(0f, 1f) : Vector2.zero;
        rect.anchorMax = top ? Vector2.one : new Vector2(1f, 0f);
        rect.pivot = top ? new Vector2(0.5f, 1f) : new Vector2(0.5f, 0f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return rect;
    }

    private void ApplyThickness(float thickness)
    {
        _currentThickness = Mathf.Clamp01(thickness);
        float height = Screen.height * _currentThickness;
        if (_top != null)
        {
            _top.sizeDelta = new Vector2(0f, height);
        }

        if (_bottom != null)
        {
            _bottom.sizeDelta = new Vector2(0f, height);
        }

        if (_currentThickness <= 0.001f)
        {
            gameObject.SetActive(false);
        }
    }
}