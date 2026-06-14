using System;
using System.Collections.Generic;

public sealed class GameModuleRegistry
{
    private readonly Dictionary<string, IGameModuleRuntime> _modules =
        new Dictionary<string, IGameModuleRuntime>(StringComparer.Ordinal);

    public bool Register(IGameModuleRuntime module)
    {
        if (module == null)
        {
            return false;
        }

        string moduleId = Normalize(module.ModuleId);
        if (string.IsNullOrEmpty(moduleId) || _modules.ContainsKey(moduleId))
        {
            return false;
        }

        _modules.Add(moduleId, module);
        return true;
    }

    public bool TryGet(string moduleId, out IGameModuleRuntime module)
    {
        return _modules.TryGetValue(Normalize(moduleId), out module);
    }

    public bool Contains(string moduleId)
    {
        return _modules.ContainsKey(Normalize(moduleId));
    }

    private static string Normalize(string moduleId)
    {
        return string.IsNullOrWhiteSpace(moduleId) ? string.Empty : moduleId.Trim();
    }
}
