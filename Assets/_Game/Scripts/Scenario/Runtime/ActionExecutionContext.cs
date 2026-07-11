using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

public sealed class ActionExecutionContext
{
    private readonly Dictionary<Type, object> _services;
    private readonly Dictionary<string, JToken> _values;
    private readonly ActionExecutionContext _valueParent;

    public ActionExecutionContext()
        : this(new ActionExecutionHandle(), new Dictionary<Type, object>(), null)
    {
    }

    public ActionExecutionContext(ActionExecutionHandle handle)
        : this(handle, new Dictionary<Type, object>(), null)
    {
    }

    private ActionExecutionContext(
        ActionExecutionHandle handle,
        Dictionary<Type, object> services,
        ActionExecutionContext valueParent)
    {
        Handle = handle ?? new ActionExecutionHandle();
        _services = services ?? new Dictionary<Type, object>();
        _values = new Dictionary<string, JToken>(StringComparer.Ordinal);
        _valueParent = valueParent;
    }

    public ActionExecutionHandle Handle { get; }

    public string ScenarioId { get; set; } = string.Empty;
    public string PrimaryMode { get; set; } = string.Empty;
    public string ModuleId { get; set; } = string.Empty;

    public void SetService<TService>(TService service)
        where TService : class
    {
        Type key = typeof(TService);
        if (service == null)
        {
            _services.Remove(key);
            return;
        }

        _services[key] = service;
    }

    public bool TryGetService<TService>(out TService service)
        where TService : class
    {
        object value;
        if (_services.TryGetValue(typeof(TService), out value))
        {
            service = value as TService;
            return service != null;
        }

        service = null;
        return false;
    }

    public TService GetService<TService>()
        where TService : class
    {
        TService service;
        return TryGetService(out service) ? service : null;
    }

    public void SetValue(string path, JToken value)
    {
        string normalized = NormalizePath(path);
        if (string.IsNullOrEmpty(normalized))
        {
            throw new ArgumentException("Context value path is required.", nameof(path));
        }

        _values[normalized] = value == null ? JValue.CreateNull() : value.DeepClone();
    }

    public bool TryGetValue(string path, out JToken value)
    {
        string normalized = NormalizePath(path);
        if (_values.TryGetValue(normalized, out JToken local))
        {
            value = local.DeepClone();
            return true;
        }

        if (_valueParent != null)
        {
            return _valueParent.TryGetValue(normalized, out value);
        }

        value = null;
        return false;
    }

    public bool RemoveLocalValue(string path)
    {
        return _values.Remove(NormalizePath(path));
    }

    public ActionExecutionContext CreateChild(ActionExecutionHandle handle)
    {
        return new ActionExecutionContext(handle, _services, this)
        {
            ScenarioId = ScenarioId,
            PrimaryMode = PrimaryMode,
            ModuleId = ModuleId
        };
    }

    private static string NormalizePath(string path)
    {
        return string.IsNullOrWhiteSpace(path) ? string.Empty : path.Trim();
    }
}
