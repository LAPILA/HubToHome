using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>Shared literal/binding validation for authoring and runtime. Paths are explicit world XY.</summary>
public static class OverworldPartyActionContract
{
    public const string ModeId = "overworld.party.mode";
    public const string VisibilityId = "overworld.party.visibility";
    public const string MoveId = "overworld.party.move";
    public const string GatherId = "overworld.party.gather";
    public const int MaximumPathPoints = 128;

    public static bool Owns(string actionId)
    {
        return actionId == ModeId || actionId == VisibilityId || actionId == MoveId || actionId == GatherId;
    }

    public static bool TryRead(ScenarioActionData action, bool allowBindings,
        out OverworldPartyActionArguments arguments, out string error)
    {
        arguments = new OverworldPartyActionArguments();
        error = string.Empty;
        try
        {
            JObject root = JObject.Parse(string.IsNullOrWhiteSpace(action?.ParametersJson) ? "{}" : action.ParametersJson);
            string id = action?.ActionId;
            if (!Owns(id))
                throw new ArgumentException("알 수 없는 필드 동료 명령입니다.");
            foreach (JProperty property in root.Properties())
            {
                bool known = id != GatherId && property.Name == "slot"
                    || id == ModeId && property.Name == "mode"
                    || id == VisibilityId && property.Name == "visible"
                    || id == MoveId && (property.Name == "x" || property.Name == "y"
                        || property.Name == "duration" || property.Name == "path" || property.Name == "rejoin");
                if (!known)
                    throw new ArgumentException("지원하지 않는 동료 파라미터입니다: " + property.Name);
            }
            if (id == GatherId)
                return true;

            JToken slot = Required(root, "slot");
            if (!IsBinding(slot, allowBindings))
            {
                if (slot.Type != JTokenType.Integer)
                    throw new ArgumentException("slot은 정수여야 합니다.");
                long number = slot.Value<long>();
                if (number < (id == MoveId ? 1 : 0) || number > 5)
                    throw new ArgumentException(id == MoveId ? "개별 이동 slot은 1~5입니다. 0(전체)은 사용할 수 없습니다." : "slot은 0(전체) 또는 1~5입니다.");
                arguments.Slot = (int)number;
            }
            if (id == ModeId)
            {
                JToken mode = Required(root, "mode");
                if (!IsBinding(mode, allowBindings))
                {
                    string value = mode.Type == JTokenType.String ? mode.Value<string>() : string.Empty;
                    switch (value)
                    {
                        case "follow": arguments.Mode = OverworldFollowerMode.Follow; break;
                        case "hold": arguments.Mode = OverworldFollowerMode.Hold; break;
                        case "detach": arguments.Mode = OverworldFollowerMode.Detached; break;
                        default: throw new ArgumentException("mode는 follow, hold, detach 중 하나여야 합니다.");
                    }
                }
            }
            else if (id == VisibilityId)
            {
                JToken visible = Required(root, "visible");
                if (!IsBinding(visible, allowBindings))
                    arguments.Visible = Boolean(visible, "visible");
            }
            else
            {
                float x = Number(Required(root, "x"), "x", allowBindings);
                float y = Number(Required(root, "y"), "y", allowBindings);
                arguments.Duration = Number(Required(root, "duration"), "duration", allowBindings);
                if (arguments.Duration < 0)
                    throw new ArgumentException("duration은 0 이상의 유한한 초 단위 시간이어야 합니다.");
                JToken rejoin = root["rejoin"];
                if (rejoin != null && !IsBinding(rejoin, allowBindings))
                    arguments.Rejoin = Boolean(rejoin, "rejoin");
                JToken path = root["path"];
                if (path != null && !IsBinding(path, allowBindings))
                {
                    if (!(path is JArray points) || points.Count > MaximumPathPoints)
                        throw new ArgumentException("path는 최대128개의 {x,y} 경유점 배열이어야 합니다.");
                    foreach (JToken point in points)
                    {
                        if (IsBinding(point, allowBindings))
                            continue;
                        if (!(point is JObject position) || position.Count != 2)
                            throw new ArgumentException("각 path 경유점은 {x,y} 객체여야 합니다.");
                        arguments.Waypoints.Add(new Vector2(Number(Required(position, "x"), "path.x", allowBindings),
                            Number(Required(position, "y"), "path.y", allowBindings)));
                    }
                }
                arguments.Waypoints.Add(new Vector2(x, y));
            }
            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
    }

    private static JToken Required(JObject root, string key)
    {
        JToken token = root[key];
        if (token == null || token.Type == JTokenType.Null)
            throw new ArgumentException("필수 동료 파라미터가 없습니다: " + key);
        return token;
    }

    private static bool IsBinding(JToken token, bool allowBindings)
    {
        if (!ScenarioValueBinding.HasMarker(token))
            return false;
        if (!allowBindings || !ScenarioValueBinding.TryRead(token, out _, out _))
            throw new ArgumentException("해석되지 않았거나 잘못된 동료 파라미터 바인딩입니다.");
        return true;
    }

    private static float Number(JToken token, string name, bool allowBindings)
    {
        if (IsBinding(token, allowBindings))
            return 0;
        if (token.Type != JTokenType.Integer && token.Type != JTokenType.Float)
            throw new ArgumentException(name + "은 유한한 숫자여야 합니다.");
        float value = token.Value<float>();
        if (float.IsNaN(value) || float.IsInfinity(value))
            throw new ArgumentException(name + "은 유한한 숫자여야 합니다.");
        return value;
    }

    private static bool Boolean(JToken token, string name)
    {
        if (token.Type != JTokenType.Boolean)
            throw new ArgumentException(name + "은 true 또는 false여야 합니다.");
        return token.Value<bool>();
    }
}

public sealed class OverworldPartyActionArguments
{
    public int Slot;
    public OverworldFollowerMode Mode;
    public bool Visible;
    public float Duration;
    public bool Rejoin;
    public readonly List<Vector2> Waypoints = new List<Vector2>();
}
