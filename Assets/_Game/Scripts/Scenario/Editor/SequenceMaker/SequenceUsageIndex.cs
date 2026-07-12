using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

public enum SequenceUsageKind
{
    ScenarioOwnership,
    LegacyBattleRule,
    TriggerRule,
    SequenceCall
}

public sealed class SequenceUsageRecord
{
    internal SequenceUsageRecord(
        SequenceUsageKind kind,
        string targetSequenceId,
        BattleScenarioData sourceScenario,
        ActionSequenceAsset sourceSequence,
        string sourceRuleId,
        string sourceBlockId,
        bool targetMissing)
    {
        Kind = kind;
        TargetSequenceId = targetSequenceId ?? string.Empty;
        SourceScenario = sourceScenario;
        SourceSequence = sourceSequence;
        SourceRuleId = sourceRuleId ?? string.Empty;
        SourceBlockId = sourceBlockId ?? string.Empty;
        TargetMissing = targetMissing;
    }

    public SequenceUsageKind Kind { get; }
    public string TargetSequenceId { get; }
    public BattleScenarioData SourceScenario { get; }
    public ActionSequenceAsset SourceSequence { get; }
    public string SourceScenarioId => SourceScenario != null
        ? SourceScenario.ScenarioId ?? string.Empty
        : string.Empty;
    public string SourceSequenceId => SourceSequence != null
        ? SourceSequence.SequenceId ?? string.Empty
        : string.Empty;
    public string SourceRuleId { get; }
    public string SourceBlockId { get; }
    public bool TargetMissing { get; }
}

public sealed class SequenceUsageDiagnostic
{
    public SequenceUsageDiagnostic(
        string code,
        string message,
        string sourceSequenceId,
        string sourceBlockId)
    {
        Code = code ?? string.Empty;
        Message = message ?? string.Empty;
        SourceSequenceId = sourceSequenceId ?? string.Empty;
        SourceBlockId = sourceBlockId ?? string.Empty;
    }

    public string Code { get; }
    public string Message { get; }
    public string SourceSequenceId { get; }
    public string SourceBlockId { get; }
}

public sealed class SequenceReferenceImpact
{
    internal SequenceReferenceImpact(
        string sequenceId,
        int ownershipCount,
        int idRewriteCount,
        IList<string> affectedScenarioIds,
        IList<string> affectedSequenceIds)
    {
        SequenceId = sequenceId ?? string.Empty;
        OwnershipCount = ownershipCount;
        IdRewriteCount = idRewriteCount;
        AffectedScenarioIds = new List<string>(affectedScenarioIds ?? Array.Empty<string>());
        AffectedSequenceIds = new List<string>(affectedSequenceIds ?? Array.Empty<string>());
    }

    public string SequenceId { get; }
    public int OwnershipCount { get; }
    public int IdRewriteCount { get; }
    public int TotalReferenceCount => OwnershipCount + IdRewriteCount;
    public bool IsSafe => TotalReferenceCount == 0;
    public IReadOnlyList<string> AffectedScenarioIds { get; }
    public IReadOnlyList<string> AffectedSequenceIds { get; }
}

public sealed class SequenceUsageIndex
{
    private readonly Dictionary<string, List<SequenceUsageRecord>> _byTargetId =
        new Dictionary<string, List<SequenceUsageRecord>>(StringComparer.Ordinal);
    private readonly List<SequenceUsageRecord> _all = new List<SequenceUsageRecord>();
    private readonly List<SequenceUsageRecord> _missingTargets = new List<SequenceUsageRecord>();
    private readonly List<SequenceUsageDiagnostic> _diagnostics =
        new List<SequenceUsageDiagnostic>();
    private readonly HashSet<string> _knownSequenceIds = new HashSet<string>(StringComparer.Ordinal);

    private SequenceUsageIndex()
    {
    }

    public IReadOnlyList<SequenceUsageRecord> All => _all;
    public IReadOnlyList<SequenceUsageRecord> MissingTargets => _missingTargets;
    public IReadOnlyList<SequenceUsageDiagnostic> Diagnostics => _diagnostics;

    public static SequenceUsageIndex Build(SequenceAssetIndex assets)
    {
        var result = new SequenceUsageIndex();
        if (assets == null)
        {
            return result;
        }

        for (int i = 0; i < assets.Sequences.Count; i++)
        {
            string sequenceId = Normalize(assets.Sequences[i].SequenceId);
            if (!string.IsNullOrEmpty(sequenceId))
            {
                result._knownSequenceIds.Add(sequenceId);
            }
        }

        for (int i = 0; i < assets.BattleFlows.Count; i++)
        {
            result.ScanBattle(assets.BattleFlows[i].BattleScenario);
        }

        for (int i = 0; i < assets.Sequences.Count; i++)
        {
            result.ScanSequence(assets.Sequences[i].Sequence);
        }

        result.Sort();
        return result;
    }

    public IReadOnlyList<SequenceUsageRecord> GetUsages(string sequenceId)
    {
        return _byTargetId.TryGetValue(
            Normalize(sequenceId),
            out List<SequenceUsageRecord> records)
            ? records
            : (IReadOnlyList<SequenceUsageRecord>)Array.Empty<SequenceUsageRecord>();
    }

    public SequenceReferenceImpact GetRenameImpact(string sequenceId)
    {
        return BuildImpact(sequenceId);
    }

    public SequenceReferenceImpact GetDeleteImpact(string sequenceId)
    {
        return BuildImpact(sequenceId);
    }

    private void ScanBattle(BattleScenarioData battle)
    {
        if (battle == null)
        {
            return;
        }

        if (battle.Sequences != null)
        {
            for (int i = 0; i < battle.Sequences.Count; i++)
            {
                ActionSequenceAsset sequence = battle.Sequences[i];
                if (sequence != null)
                {
                    Add(
                        SequenceUsageKind.ScenarioOwnership,
                        sequence.SequenceId,
                        battle,
                        sequence,
                        string.Empty,
                        string.Empty);
                }
            }
        }

        if (battle.Rules != null)
        {
            for (int i = 0; i < battle.Rules.Count; i++)
            {
                BattleEventRuleData rule = battle.Rules[i];
                if (rule != null && !string.IsNullOrWhiteSpace(rule.SequenceId))
                {
                    Add(
                        SequenceUsageKind.LegacyBattleRule,
                        rule.SequenceId,
                        battle,
                        null,
                        rule.RuleId,
                        string.Empty);
                }
            }
        }

        if (battle.TriggerRules != null)
        {
            for (int i = 0; i < battle.TriggerRules.Count; i++)
            {
                ScenarioTriggerRuleData rule = battle.TriggerRules[i];
                if (rule != null && !string.IsNullOrWhiteSpace(rule.SequenceId))
                {
                    Add(
                        SequenceUsageKind.TriggerRule,
                        rule.SequenceId,
                        battle,
                        null,
                        rule.RuleId,
                        string.Empty);
                }
            }
        }
    }

    private void ScanSequence(ActionSequenceAsset sequence)
    {
        if (sequence == null || sequence.Actions == null)
        {
            return;
        }

        ScanActions(sequence, sequence.Actions);
    }

    private void ScanActions(
        ActionSequenceAsset source,
        IList<ScenarioActionData> actions)
    {
        if (actions == null)
        {
            return;
        }

        for (int i = 0; i < actions.Count; i++)
        {
            ScenarioActionData action = actions[i];
            if (action == null)
            {
                continue;
            }

            if (string.Equals(
                    Normalize(action.ActionId),
                    SequenceCallActionAdapter.Id,
                    StringComparison.Ordinal))
            {
                if (TryReadCallTarget(action, out string targetId, out string error))
                {
                    Add(
                        SequenceUsageKind.SequenceCall,
                        targetId,
                        null,
                        source,
                        string.Empty,
                        action.BlockId);
                }
                else
                {
                    _diagnostics.Add(new SequenceUsageDiagnostic(
                        "sequence.usage.call.invalid",
                        error,
                        source.SequenceId,
                        action.BlockId));
                }
            }

            ScanActions(source, action.Children);
        }
    }

    private void Add(
        SequenceUsageKind kind,
        string targetSequenceId,
        BattleScenarioData sourceScenario,
        ActionSequenceAsset sourceSequence,
        string sourceRuleId,
        string sourceBlockId)
    {
        string targetId = Normalize(targetSequenceId);
        if (string.IsNullOrEmpty(targetId))
        {
            return;
        }

        bool missing = !_knownSequenceIds.Contains(targetId);
        var record = new SequenceUsageRecord(
            kind,
            targetId,
            sourceScenario,
            sourceSequence,
            sourceRuleId,
            sourceBlockId,
            missing);
        _all.Add(record);
        if (!_byTargetId.TryGetValue(targetId, out List<SequenceUsageRecord> records))
        {
            records = new List<SequenceUsageRecord>();
            _byTargetId.Add(targetId, records);
        }

        records.Add(record);
        if (missing)
        {
            _missingTargets.Add(record);
        }
    }

    private SequenceReferenceImpact BuildImpact(string sequenceId)
    {
        IReadOnlyList<SequenceUsageRecord> records = GetUsages(sequenceId);
        int ownership = 0;
        int rewrites = 0;
        var scenarios = new HashSet<string>(StringComparer.Ordinal);
        var sequences = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < records.Count; i++)
        {
            SequenceUsageRecord record = records[i];
            if (record.Kind == SequenceUsageKind.ScenarioOwnership)
            {
                ownership++;
            }
            else
            {
                rewrites++;
            }

            if (!string.IsNullOrWhiteSpace(record.SourceScenarioId))
            {
                scenarios.Add(record.SourceScenarioId);
            }

            if (!string.IsNullOrWhiteSpace(record.SourceSequenceId)
                && !string.Equals(
                    record.SourceSequenceId,
                    sequenceId,
                    StringComparison.Ordinal))
            {
                sequences.Add(record.SourceSequenceId);
            }
        }

        var scenarioList = new List<string>(scenarios);
        var sequenceList = new List<string>(sequences);
        scenarioList.Sort(StringComparer.Ordinal);
        sequenceList.Sort(StringComparer.Ordinal);
        return new SequenceReferenceImpact(
            Normalize(sequenceId),
            ownership,
            rewrites,
            scenarioList,
            sequenceList);
    }

    private void Sort()
    {
        _all.Sort(Compare);
        _missingTargets.Sort(Compare);
        foreach (List<SequenceUsageRecord> records in _byTargetId.Values)
        {
            records.Sort(Compare);
        }

        _diagnostics.Sort((left, right) =>
        {
            int source = StringComparer.Ordinal.Compare(
                left.SourceSequenceId,
                right.SourceSequenceId);
            return source != 0
                ? source
                : StringComparer.Ordinal.Compare(left.SourceBlockId, right.SourceBlockId);
        });
    }

    private static int Compare(SequenceUsageRecord left, SequenceUsageRecord right)
    {
        int target = StringComparer.Ordinal.Compare(
            left.TargetSequenceId,
            right.TargetSequenceId);
        if (target != 0)
        {
            return target;
        }

        int kind = left.Kind.CompareTo(right.Kind);
        if (kind != 0)
        {
            return kind;
        }

        int scenario = StringComparer.Ordinal.Compare(
            left.SourceScenarioId,
            right.SourceScenarioId);
        if (scenario != 0)
        {
            return scenario;
        }

        int sequence = StringComparer.Ordinal.Compare(
            left.SourceSequenceId,
            right.SourceSequenceId);
        return sequence != 0
            ? sequence
            : StringComparer.Ordinal.Compare(left.SourceBlockId, right.SourceBlockId);
    }

    private static bool TryReadCallTarget(
        ScenarioActionData action,
        out string targetId,
        out string error)
    {
        targetId = string.Empty;
        error = string.Empty;
        try
        {
            JObject parameters = string.IsNullOrWhiteSpace(action.ParametersJson)
                ? new JObject()
                : JObject.Parse(action.ParametersJson);
            JToken token = parameters["sequence"];
            if (token == null || token.Type != JTokenType.String)
            {
                error = "sequence.call의 sequence 문자열이 없습니다.";
                return false;
            }

            targetId = Normalize(token.Value<string>());
            if (string.IsNullOrEmpty(targetId))
            {
                error = "sequence.call의 sequence ID가 비어 있습니다.";
                return false;
            }

            return true;
        }
        catch (Exception exception)
        {
            error = "sequence.call 파라미터 JSON을 읽을 수 없습니다: " + exception.Message;
            return false;
        }
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
