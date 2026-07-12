using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class SequenceLiveContextRegistry
{
    private readonly List<ISequenceLiveContextProvider> _providers =
        new List<ISequenceLiveContextProvider>();

    public SequenceLiveContextRegistry(bool includeBuiltIns = true)
    {
        if (includeBuiltIns)
        {
            Register(new RuntimeSequenceLiveContextProvider());
        }
    }

    public int Count => _providers.Count;

    public void Register(ISequenceLiveContextProvider provider)
    {
        if (provider == null)
        {
            throw new ArgumentNullException(nameof(provider));
        }
        if (_providers.Contains(provider))
        {
            return;
        }
        _providers.Add(provider);
        _providers.Sort((left, right) => right.Priority.CompareTo(left.Priority));
    }

    public bool TryCreate(
        BattleScenarioData battle,
        ActionSequenceAsset sequence,
        out SequenceLiveContext context,
        out string error)
    {
        context = null;
        if (!Application.isPlaying)
        {
            error = "Play Mode가 아닙니다.";
            return false;
        }
        if (sequence == null)
        {
            error = "테스트할 Action Sequence가 없습니다.";
            return false;
        }

        var reasons = new List<string>();
        for (int i = 0; i < _providers.Count; i++)
        {
            if (_providers[i].TryCreate(battle, sequence, out context, out string reason))
            {
                if (context?.CoroutineHost == null
                    || context.Director == null
                    || context.ExecutionContext == null)
                {
                    error = "Live Context Provider가 불완전한 실행 문맥을 반환했습니다.";
                    context = null;
                    return false;
                }
                error = string.Empty;
                return true;
            }
            if (!string.IsNullOrWhiteSpace(reason))
            {
                reasons.Add(reason.Trim());
            }
        }

        error = reasons.Count == 0
            ? "현재 씬에서 이 시퀀스를 실행할 Live Context Provider를 찾지 못했습니다."
            : string.Join("\n", reasons);
        return false;
    }

    private sealed class RuntimeSequenceLiveContextProvider : ISequenceLiveContextProvider
    {
        public int Priority => 1000;

        public bool TryCreate(
            BattleScenarioData battle,
            ActionSequenceAsset sequence,
            out SequenceLiveContext context,
            out string error)
        {
            context = null;
            MonoBehaviour[] behaviours =
                UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            var sources = new List<IActionSequenceLiveContextSource>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IActionSequenceLiveContextSource source)
                {
                    sources.Add(source);
                }
            }
            sources.Sort((left, right) =>
                right.LiveContextPriority.CompareTo(left.LiveContextPriority));
            var reasons = new List<string>();
            for (int i = 0; i < sources.Count; i++)
            {
                IActionSequenceLiveContextSource source = sources[i];
                if (source.TryCreateLiveContext(
                        battle,
                        sequence,
                        out ActionDirector director,
                        out ActionExecutionContext executionContext,
                        out string reason))
                {
                    context = new SequenceLiveContext(
                        source.LiveContextLabel,
                        source as MonoBehaviour,
                        director,
                        executionContext);
                    error = string.Empty;
                    return true;
                }
                if (!string.IsNullOrWhiteSpace(reason))
                {
                    reasons.Add(reason.Trim());
                }
            }
            error = reasons.Count > 0 ? string.Join("\n", reasons) : string.Empty;
            return false;
        }
    }
}
