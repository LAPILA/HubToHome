using System;
using System.Collections.Generic;

public sealed class ActionExecutionContext
{
    private readonly Dictionary<Type, object> _services;

    public ActionExecutionContext()
        : this(new ActionExecutionHandle(), new Dictionary<Type, object>())
    {
    }

    public ActionExecutionContext(ActionExecutionHandle handle)
        : this(handle, new Dictionary<Type, object>())
    {
    }

    private ActionExecutionContext(
        ActionExecutionHandle handle,
        Dictionary<Type, object> services)
    {
        Handle = handle ?? new ActionExecutionHandle();
        _services = services ?? new Dictionary<Type, object>();
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

    public ActionExecutionContext CreateChild(ActionExecutionHandle handle)
    {
        return new ActionExecutionContext(handle, _services)
        {
            ScenarioId = ScenarioId,
            PrimaryMode = PrimaryMode,
            ModuleId = ModuleId
        };
    }
}
