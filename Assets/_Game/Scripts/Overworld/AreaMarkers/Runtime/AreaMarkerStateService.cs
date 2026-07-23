public static class AreaMarkerStateService
{
    private const string CompletionPrefix = "area_marker.completed:";

    public static string BuildCompletionFlag(
        string sceneId,
        string areaId,
        string markerId)
    {
        string marker = Normalize(markerId);
        if (string.IsNullOrEmpty(marker))
            return string.Empty;

        string area = Normalize(areaId);
        string scopeType;
        string scope;
        if (!string.IsNullOrEmpty(area))
        {
            scopeType = "area";
            scope = area;
        }
        else
        {
            scopeType = "scene";
            scope = Normalize(sceneId);
        }

        return CompletionPrefix
            + scopeType
            + ":"
            + scope.Length
            + ":"
            + scope
            + ":"
            + marker;
    }

    public static bool IsCompleted(
        GlobalDataManager global,
        string sceneId,
        string areaId,
        string markerId)
    {
        if (global == null)
            return false;

        string flag = BuildCompletionFlag(sceneId, areaId, markerId);
        return !string.IsNullOrEmpty(flag) && global.GetFlag(flag, 0) != 0;
    }

    public static void MarkCompleted(
        GlobalDataManager global,
        string sceneId,
        string areaId,
        string markerId)
    {
        if (global == null)
            return;

        string flag = BuildCompletionFlag(sceneId, areaId, markerId);
        if (!string.IsNullOrEmpty(flag))
            global.SetFlag(flag, 1);
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
