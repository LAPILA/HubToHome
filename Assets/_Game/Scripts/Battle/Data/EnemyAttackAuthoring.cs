using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public enum EnemyAttackAuthoringSeverity
{
    Error,
    Warning
}

public readonly struct SkillActionAuthoringTiming
{
    public string PhaseLabel { get; }
    public float Duration { get; }
    public bool IsVariable { get; }
    public bool IsSupported { get; }

    private SkillActionAuthoringTiming(
        string phaseLabel,
        float duration,
        bool isVariable,
        bool isSupported)
    {
        PhaseLabel = string.IsNullOrWhiteSpace(phaseLabel) ? "기타" : phaseLabel.Trim();
        Duration = Mathf.Max(0f, duration);
        IsVariable = isVariable;
        IsSupported = isSupported;
    }

    public static SkillActionAuthoringTiming Fixed(string phaseLabel, float duration)
    {
        return new SkillActionAuthoringTiming(phaseLabel, duration, false, true);
    }

    public static SkillActionAuthoringTiming Variable(string phaseLabel, float minimumDuration)
    {
        return new SkillActionAuthoringTiming(phaseLabel, minimumDuration, true, true);
    }

    public static SkillActionAuthoringTiming Unsupported(string phaseLabel)
    {
        return new SkillActionAuthoringTiming(phaseLabel, 0f, false, false);
    }
}

public readonly struct EnemyAttackAuthoringIssue
{
    public string Code { get; }
    public EnemyAttackAuthoringSeverity Severity { get; }
    public int BlockIndex { get; }
    public string Message { get; }

    public EnemyAttackAuthoringIssue(
        string code,
        EnemyAttackAuthoringSeverity severity,
        int blockIndex,
        string message)
    {
        Code = string.IsNullOrWhiteSpace(code)
            ? throw new ArgumentException("Issue code is required.", nameof(code))
            : code.Trim();
        Severity = severity;
        BlockIndex = blockIndex;
        Message = string.IsNullOrWhiteSpace(message)
            ? throw new ArgumentException("Issue message is required.", nameof(message))
            : message.Trim();
    }
}

public readonly struct EnemyAttackTimelineEntry
{
    public int BlockIndex { get; }
    public string Label { get; }
    public string PhaseLabel { get; }
    public float StartTime { get; }
    public float Duration { get; }
    public bool Enabled { get; }
    public bool IsVariable { get; }
    public bool TimingSupported { get; }

    public float EndTime => StartTime + Duration;

    public EnemyAttackTimelineEntry(
        int blockIndex,
        string label,
        string phaseLabel,
        float startTime,
        float duration,
        bool enabled,
        bool isVariable,
        bool timingSupported)
    {
        BlockIndex = blockIndex;
        Label = string.IsNullOrWhiteSpace(label) ? "블록" : label.Trim();
        PhaseLabel = string.IsNullOrWhiteSpace(phaseLabel) ? "기타" : phaseLabel.Trim();
        StartTime = Mathf.Max(0f, startTime);
        Duration = Mathf.Max(0f, duration);
        Enabled = enabled;
        IsVariable = isVariable;
        TimingSupported = timingSupported;
    }
}

public sealed class EnemyAttackAuthoringReport
{
    private readonly List<EnemyAttackTimelineEntry> _entries = new List<EnemyAttackTimelineEntry>();
    private readonly List<EnemyAttackAuthoringIssue> _issues = new List<EnemyAttackAuthoringIssue>();

    public IReadOnlyList<EnemyAttackTimelineEntry> Entries => _entries;
    public IReadOnlyList<EnemyAttackAuthoringIssue> Issues => _issues;
    public float EstimatedDuration { get; internal set; }
    public int DefenseWindowCount { get; internal set; }
    public int DamageBlockCount { get; internal set; }
    public int ErrorCount { get; private set; }
    public int WarningCount { get; private set; }
    public bool HasErrors => ErrorCount > 0;

    internal void AddEntry(EnemyAttackTimelineEntry entry)
    {
        _entries.Add(entry);
    }

    internal void AddIssue(
        string code,
        EnemyAttackAuthoringSeverity severity,
        int blockIndex,
        string message)
    {
        _issues.Add(new EnemyAttackAuthoringIssue(code, severity, blockIndex, message));
        if (severity == EnemyAttackAuthoringSeverity.Error)
            ErrorCount++;
        else
            WarningCount++;
    }

    public string BuildTimelinePreview()
    {
        if (_entries.Count == 0)
            return "활성 공격 블록이 없습니다.";

        var builder = new StringBuilder();
        builder.Append("예상 ")
            .Append(EstimatedDuration.ToString("0.00"))
            .Append("초 | 방어창 ")
            .Append(DefenseWindowCount)
            .Append(" | 피해 ")
            .Append(DamageBlockCount)
            .AppendLine();

        for (int i = 0; i < _entries.Count; i++)
        {
            EnemyAttackTimelineEntry entry = _entries[i];
            builder.Append(entry.BlockIndex.ToString("00")).Append("  ");
            if (!entry.Enabled)
            {
                builder.Append("[비활성] ");
            }
            else if (!entry.TimingSupported)
            {
                builder.Append(entry.StartTime.ToString("0.00")).Append("s  [?] ");
            }
            else
            {
                builder.Append(entry.StartTime.ToString("0.00"))
                    .Append("-")
                    .Append(entry.EndTime.ToString("0.00"))
                    .Append(entry.IsVariable ? "s+ " : "s  ");
            }

            builder.Append("[")
                .Append(entry.PhaseLabel)
                .Append("] ")
                .Append(entry.Label);

            if (i < _entries.Count - 1)
                builder.AppendLine();
        }

        return builder.ToString();
    }

    public string BuildValidationSummary()
    {
        if (_issues.Count == 0)
            return "오류와 경고가 없습니다.";

        var builder = new StringBuilder();
        builder.Append("오류 ")
            .Append(ErrorCount)
            .Append(" / 경고 ")
            .Append(WarningCount);

        for (int i = 0; i < _issues.Count; i++)
        {
            EnemyAttackAuthoringIssue issue = _issues[i];
            builder.AppendLine()
                .Append(issue.Severity == EnemyAttackAuthoringSeverity.Error ? "[오류] " : "[경고] ");
            if (issue.BlockIndex >= 0)
                builder.Append("Block[").Append(issue.BlockIndex).Append("] ");
            builder.Append(issue.Message);
        }

        return builder.ToString();
    }
}

public static class EnemyAttackAuthoringAnalyzer
{
    public const float MaxDefenseFeedbackDuration = 0.35f;
    public const float RecommendedCounterReactionWindow = 0.35f;

    public static EnemyAttackAuthoringReport Analyze(SkillData skill)
    {
        var report = new EnemyAttackAuthoringReport();
        if (skill == null)
        {
            report.AddIssue(
                "skill.enemy_attack.asset.missing",
                EnemyAttackAuthoringSeverity.Error,
                -1,
                "SkillData가 없습니다.");
            return report;
        }

        if (skill.ActionTimeline == null || skill.ActionTimeline.Count == 0)
        {
            report.AddIssue(
                "skill.timeline.empty",
                EnemyAttackAuthoringSeverity.Warning,
                -1,
                "전투 스킬 블록이 비어 있습니다.");
            return report;
        }

        float cursor = 0f;
        int pendingDefenseIndex = -1;
        bool sawDefenseWindow = false;
        int firstDamageBeforeDefenseIndex = -1;
        Action_DefenseWindow synchronizedDefense = null;
        for (int i = 0; i < skill.ActionTimeline.Count; i++)
        {
            SkillActionBlock block = skill.ActionTimeline[i];
            if (block == null)
            {
                report.AddEntry(new EnemyAttackTimelineEntry(
                    i,
                    "누락 블록",
                    "오류",
                    cursor,
                    0f,
                    true,
                    false,
                    false));
                report.AddIssue(
                    "skill.timeline.block.missing",
                    EnemyAttackAuthoringSeverity.Error,
                    i,
                    "ActionTimeline 블록이 비어 있습니다.");
                continue;
            }

            SkillActionAuthoringTiming timing = block.GetAuthoringTiming();
            bool enabled = block.Enabled;
            if (enabled && block is Action_DefenseWindow linkedDefense
                && SkillContext.HasSynchronizedImpact(linkedDefense, skill.ActionTimeline))
            {
                synchronizedDefense = linkedDefense;
                timing = SkillActionAuthoringTiming.Variable("다음 타격과 동시 실행", 0f);
            }
            else if (enabled && synchronizedDefense != null)
            {
                if (block is Action_Projectile projectile)
                    timing = synchronizedDefense.CopyForImpact(projectile.FlightDuration).GetAuthoringTiming();
                else if (block is Action_SequentialMelee melee)
                    timing = SkillActionAuthoringTiming.Variable("대상별 방어·타격",
                        synchronizedDefense.GetAuthoringTiming().Duration + Mathf.Max(0f, melee.DashSpeed) + 0.2f);
                synchronizedDefense = null;
            }
            float duration = enabled && timing.IsSupported ? timing.Duration : 0f;
            report.AddEntry(new EnemyAttackTimelineEntry(
                i,
                string.IsNullOrWhiteSpace(block.DesignerLabel) ? block.BlockName : block.DesignerLabel,
                timing.PhaseLabel,
                cursor,
                duration,
                enabled,
                timing.IsVariable,
                timing.IsSupported));

            if (!enabled)
                continue;

            if (!timing.IsSupported)
            {
                report.AddIssue(
                    "skill.enemy_attack.timeline.preview.unsupported",
                    EnemyAttackAuthoringSeverity.Warning,
                    i,
                    block.BlockName + " Custom Block은 GetAuthoringTiming()을 재정의해야 시간축에 표시됩니다.");
            }
            else
            {
                cursor += duration;
            }

            if (block is Action_DefenseWindow defense)
            {
                report.DefenseWindowCount++;
                sawDefenseWindow = true;
                if (firstDamageBeforeDefenseIndex >= 0)
                {
                    AddWarning(report, "skill.enemy_attack.damage.before_defense", firstDamageBeforeDefenseIndex,
                        "피해 블록이 실시간 방어 대응보다 앞에 있습니다. 방어 대응 → 공격 애니메이션/VFX → 피해 순서로 배치해야 Z/X/C가 타격을 막을 수 있습니다.");
                    firstDamageBeforeDefenseIndex = -1;
                }
                if (defense.PatternMode != EnemyDefensePatternMode.TelegraphThenNextTurnWindow)
                {
                    if (pendingDefenseIndex >= 0)
                        AddWarning(report, "skill.enemy_attack.defense.unconsumed_result", i,
                            "앞선 방어 대응(" + (pendingDefenseIndex + 1) + "번)의 피해 배율을 소비하기 전에 새 방어창이 열립니다. 방어 대응 → 피해/투사체/연쇄 근접 순서를 사용하세요. 별도 Custom Block에서 소비한다면 의도한 구성인지 확인하세요.");
                    pendingDefenseIndex = i;
                }
            }
            if (block is Action_Damage || block is Action_Projectile || block is Action_SequentialMelee)
            {
                report.DamageBlockCount++;
                if (!sawDefenseWindow && firstDamageBeforeDefenseIndex < 0)
                    firstDamageBeforeDefenseIndex = i;
                pendingDefenseIndex = -1;
            }

            if (block is Action_PresentationAccent && pendingDefenseIndex >= 0)
                AddError(report, "skill.accent.defense_impact_gap", i,
                    "강조 연출을 방어 대응과 피해 사이에 넣지 마세요. 명중 점멸 예약은 방어 대응 앞에, 실루엣은 준비 구간에 배치하세요.");

            ValidateBlock(block, i, report);
            if (block is Action_ScreenPassSlash pass)
            {
                if (skill.UsageProfile != SkillUsageProfile.PlayerOnly)
                    AddError(report, "skill.screen_pass.player_only", i, "화면 가로 관통은 아군 전용입니다.");
                if (!pass.HasValidSettings)
                    AddError(report, "skill.screen_pass.settings.invalid", i, "관통 횟수1~3, 이동/돌파/복귀 시간은 양수, 준비/정지/여백/피해는0 이상의 유효한 숫자여야 합니다.");
            }
            if (block is Action_EnemyAssaultCombo assault)
            {
                if (skill.UsageProfile != SkillUsageProfile.EnemyOnly)
                    AddError(report, "skill.assault.enemy_only", i, "연속 돌파 · 공중 난무는 적 전용 스킬에서 사용하세요.");
                if (!assault.HasValidSettings)
                    AddError(report, "skill.assault.settings.invalid", i, "연속 공격 단계의 타수/가속 시간/공중 높이/피해 배율을 확인하세요.");
            }
            if (block is Action_EnemyWindup windup)
            {
                if (skill.UsageProfile != SkillUsageProfile.EnemyOnly)
                    AddError(report, "skill.windup.enemy_only", i, "적 공격 준비는 적 전용 스킬에서 사용하세요.");
                if (!windup.HasValidSettings)
                    AddError(report, "skill.windup.settings.invalid", i, "준비 줌은 0.5~1, 확대/복귀는 0.05초 이상, 유지는 0초 이상이어야 합니다.");
            }
            if (block is Action_RapidStrikes rapid)
            {
                if (skill.UsageProfile != SkillUsageProfile.PlayerOnly)
                    AddError(report, "skill.rapid.player_only", i, "실시간 연격은 사용 범위를 아군 전용으로 지정하세요.");
                if (float.IsNaN(rapid.Duration) || float.IsInfinity(rapid.Duration) || rapid.Duration < 0.5f
                    || float.IsNaN(rapid.HitInterval) || float.IsInfinity(rapid.HitInterval)
                    || rapid.HitInterval < 0.05f || rapid.HitInterval > rapid.Duration || rapid.Duration / rapid.HitInterval > 200f)
                    AddError(report, "skill.rapid.timing.invalid", i, "연격은 0.5초 이상, 타격 간격 0.05초 이상, 총 1~200타여야 합니다.");
                if (float.IsNaN(rapid.InputWindow) || float.IsInfinity(rapid.InputWindow) || rapid.InputWindow <= 0f
                    || float.IsNaN(rapid.PromptInterval) || float.IsInfinity(rapid.PromptInterval)
                    || rapid.PromptInterval < 0.2f || rapid.InputWindow > rapid.PromptInterval)
                    AddError(report, "skill.rapid.input.invalid", i, "독립 QTE 주기는 0.2초 이상, 입력 시간은 0초 초과이며 표시 간격 이하여야 합니다.");
                if (float.IsNaN(rapid.DamagePerStrike) || float.IsInfinity(rapid.DamagePerStrike) || rapid.DamagePerStrike < 0f
                    || float.IsNaN(rapid.SuccessMultiplier) || float.IsInfinity(rapid.SuccessMultiplier) || rapid.SuccessMultiplier < 1f)
                    AddError(report, "skill.rapid.damage.invalid", i, "한 타 피해 배율은 0 이상, 성공 배율은 1 이상의 유효한 숫자여야 합니다.");
                if (float.IsNaN(rapid.SlashTravel) || float.IsInfinity(rapid.SlashTravel) || rapid.SlashTravel < 0f)
                    AddError(report, "skill.rapid.travel.invalid", i, "타격 좌우 이동 폭은 0 이상의 유효한 숫자여야 합니다.");
            }
            if (block is Action_AerialCrossSlash aerial)
            {
                if (skill.UsageProfile != SkillUsageProfile.PlayerOnly)
                    AddError(report, "skill.aerial.player_only", i, "공중 회전 베기는 아군 전용입니다.");
                if (!aerial.HasValidSettings)
                    AddError(report, "skill.aerial.settings.invalid", i, "공중 회전의 거리/시간은 양수, QTE 시간은 0.2초 이상이며 입력 시간 이상, 피해는 0 이상, 성공 배율은 1 이상이어야 합니다.");
            }
        }

        if (report.DamageBlockCount > 0 && !sawDefenseWindow)
        {
            AddWarning(report, "skill.enemy_attack.defense.missing", -1,
                "피해를 주는 적 스킬에 실시간 방어 대응 블록이 없습니다. 적 공격은 플레이어 전용 QTE가 아니라 방어 대응 → 피해 순서를 사용하세요.");
        }

        report.EstimatedDuration = cursor;
        return report;
    }

    private static void ValidateBlock(
        SkillActionBlock block,
        int blockIndex,
        EnemyAttackAuthoringReport report)
    {
        if (block is Action_PresentationAccent accent)
        {
            if (accent.Mode == Action_PresentationAccent.AccentMode.CutIn)
                AddError(report, "skill.accent.cutin.retired", blockIndex, "컷인은 제거된 연출입니다. 이 블록을 삭제하거나 실루엣/명중 점멸로 바꾸세요.");
            else if (!accent.HasValidSettings)
                AddError(report, "skill.accent.settings.invalid", blockIndex,
                    "강조 시간은0.03~1.5초, 강도/음량은0~1이며 모든 값이 유한해야 합니다.");
        }

        if (block is Action_Wait wait && wait.WaitTime < 0f)
        {
            AddError(report, "skill.timeline.wait.duration.invalid", blockIndex, "대기 시간은 0 이상이어야 합니다.");
        }

        if (block is Action_Move move && move.Duration < 0f)
        {
            AddError(report, "skill.timeline.move.duration.invalid", blockIndex, "이동 시간은 0 이상이어야 합니다.");
        }

        if (block is Action_PlayAnim playAnim)
        {
            if (string.IsNullOrWhiteSpace(playAnim.AnimTriggerName))
                AddError(report, "skill.timeline.animation.trigger.missing", blockIndex, "Animation Trigger가 비어 있습니다.");
            if (playAnim.DelayAfter < 0f)
                AddError(report, "skill.timeline.animation.delay.invalid", blockIndex, "애니메이션 후 대기는 0 이상이어야 합니다.");
        }

        if (block is Action_Damage damage && damage.SkillMultiplier < 0f)
        {
            AddError(report, "skill.timeline.damage.multiplier.invalid", blockIndex, "피해 배율은 0 이상이어야 합니다.");
        }

        if (block is Action_QTE qte)
        {
            AddError(report, "skill.enemy_attack.qte.unsupported", blockIndex,
                "적 스킬에서는 플레이어 전용 QTE를 사용할 수 없습니다. 적 공격은 방어 대응 블록의 실시간 Z/X/C 판정을 사용하세요.");
            if (qte.TimeLimit <= 0f)
                AddError(report, "skill.timeline.qte.duration.invalid", blockIndex, "QTE 제한 시간은 0보다 커야 합니다.");
            if (qte.Nodes == null || qte.Nodes.Count == 0)
                AddWarning(report, "skill.timeline.qte.nodes.empty", blockIndex, "QTE 노드가 비어 있습니다.");
        }

        if (block is Action_VFX vfx && vfx.VfxPrefab == null)
        {
            AddError(report, "skill.timeline.vfx_prefab.missing", blockIndex, "VFX Prefab이 비어 있습니다.");
        }

        if (block is Action_Projectile projectile)
        {
            if (projectile.ProjectilePrefab == null)
                AddError(report, "skill.timeline.projectile_prefab.missing", blockIndex, "Projectile Prefab이 비어 있습니다.");
            if (projectile.FlightDuration <= 0f)
                AddError(report, "skill.timeline.projectile.duration.invalid", blockIndex, "투사체 비행 시간은 0보다 커야 합니다.");
            if (projectile.DamageMultiplier < 0f)
                AddError(report, "skill.timeline.projectile.damage.invalid", blockIndex, "투사체 피해 배율은 0 이상이어야 합니다.");
            if (projectile.ImpactVFXPrefab == null)
                AddWarning(report, "skill.timeline.projectile.impact_vfx.missing", blockIndex, "Impact VFX Prefab이 비어 있습니다.");
        }

        if (block is Action_SequentialMelee sequential)
        {
            if (string.IsNullOrWhiteSpace(sequential.AttackAnimTrigger))
                AddError(report, "skill.timeline.sequential.animation.missing", blockIndex, "연쇄 근접 공격 Trigger가 비어 있습니다.");
            if (sequential.DashSpeed <= 0f)
                AddError(report, "skill.timeline.sequential.dash_duration.invalid", blockIndex, "연쇄 근접 이동 시간은 0보다 커야 합니다.");
            if (float.IsNaN(sequential.RecoveryDelay) || float.IsInfinity(sequential.RecoveryDelay) || sequential.RecoveryDelay < 0f)
                AddError(report, "skill.timeline.sequential.recovery.invalid", blockIndex, "연쇄 근접 타격 후 간격은 유한한 0 이상의 초 단위 값이어야 합니다.");
            if (sequential.DamageMultiplier < 0f)
                AddError(report, "skill.timeline.sequential.damage.invalid", blockIndex, "연쇄 근접 피해 배율은 0 이상이어야 합니다.");
            if (sequential.HitVfxPrefab == null)
                AddWarning(report, "skill.timeline.sequential.hit_vfx.missing", blockIndex, "연쇄 근접 Hit VFX Prefab이 비어 있습니다.");
        }

        if (block is Action_DefenseWindow defense)
            ValidateDefenseWindow(defense, blockIndex, report);
    }

    private static void ValidateDefenseWindow(
        Action_DefenseWindow defense,
        int blockIndex,
        EnemyAttackAuthoringReport report)
    {
        bool opensWindow = defense.PatternMode != EnemyDefensePatternMode.TelegraphThenNextTurnWindow;
        bool requiresTelegraphTime = defense.UseTelegraph
            && defense.PatternMode != EnemyDefensePatternMode.ImmediateReaction;

        if (float.IsNaN(defense.MeleeApproachHeight) || float.IsInfinity(defense.MeleeApproachHeight)
            || defense.MeleeApproachHeight < 0f || defense.MeleeApproachHeight > 8f)
            AddError(report, "skill.enemy_attack.pass.air_height.invalid", blockIndex, "공중 접근 높이는0~8의 유한한 값이어야 합니다.");
        if (defense.AttackVfxPrefab != null && (float.IsNaN(defense.AttackVfxScale) || float.IsInfinity(defense.AttackVfxScale)
            || defense.AttackVfxScale <= 0f || defense.AttackVfxScale > 8f
            || float.IsNaN(defense.AttackVfxLifetime) || float.IsInfinity(defense.AttackVfxLifetime)
            || defense.AttackVfxLifetime <= 0f || defense.AttackVfxLifetime > 10f))
            AddError(report, "skill.enemy_attack.attack_vfx.settings.invalid", blockIndex, "공격 이펙트 배율은0초과~8, 수명은0초과~10초의 유한한 값이어야 합니다.");

        if (defense.MeleePassExtent < EnemyMeleePassExtent.Local || defense.MeleePassExtent > EnemyMeleePassExtent.ScreenWide)
            AddError(report, "skill.enemy_attack.pass.extent.invalid", blockIndex, "알 수 없는 근접 관통 범위입니다.");
        if (defense.UseMeleePassThrough && defense.MeleePassExtent == EnemyMeleePassExtent.ScreenWide)
        {
            if (float.IsNaN(defense.ScreenPassDuration) || float.IsInfinity(defense.ScreenPassDuration) || defense.ScreenPassDuration <= 0f
                || float.IsNaN(defense.ScreenEdgeApproachDuration) || float.IsInfinity(defense.ScreenEdgeApproachDuration) || defense.ScreenEdgeApproachDuration <= 0f
                || float.IsNaN(defense.ScreenEdgeMarginPixels) || float.IsInfinity(defense.ScreenEdgeMarginPixels)
                || defense.ScreenEdgeMarginPixels < 0f || defense.ScreenEdgeMarginPixels > 64f)
                AddError(report, "skill.enemy_attack.pass.screen_settings.invalid", blockIndex, "화면 관통/출발 접근 시간은 양수, 여백은0~64픽셀의 유한한 숫자여야 합니다.");
            if (DefenseJudgementPolicy.IsCounterRequirement(defense.Requirement))
                AddWarning(report, "skill.enemy_attack.pass.counter_uses_center", blockIndex, "C 특수 반격 공격은 화면 관통 대신 기존 중앙 연출을 사용합니다.");
        }

        if (DefenseJudgementPolicy.IsCounterRequirement(defense.Requirement))
        {
            if (!defense.UseTelegraph || defense.PatternMode == EnemyDefensePatternMode.ImmediateReaction)
                AddWarning(report, "skill.enemy_attack.counter.telegraph.recommended", blockIndex,
                    "특수 공격은 가드와 회피로 피할 수 없으며 C 반격만 가능합니다. 전조 사용을 권장합니다. 별도 블록이 전조를 담당한다면 실제 재생 시 확인하세요.");
            if (opensWindow && defense.TimeWindow > 0f && defense.TimeWindow < RecommendedCounterReactionWindow)
                AddWarning(report, "skill.enemy_attack.counter.window.short", blockIndex,
                    "공격 대응 구간이 0.35초보다 짧습니다. C 반격 전용임을 읽을 수 있는 전조 길이를 확인하세요. 실제 C 성공 구간은 QTEManager 설정을 사용합니다.");
            if (float.IsNaN(defense.CounterDamageMultiplier) || float.IsInfinity(defense.CounterDamageMultiplier)
                || defense.CounterDamageMultiplier <= 0f)
                AddError(report, "skill.enemy_attack.counter.damage.invalid", blockIndex,
                    "연계 반격 피해 배율은 유효한 0 초과 숫자여야 합니다. 공격받은 생존 아군의 기본 공격에 적용됩니다.");
        }

        if (opensWindow && defense.TimeWindow <= 0f)
            AddError(report, "skill.enemy_attack.defense.window.invalid", blockIndex, "방어 판정 시간은 0보다 커야 합니다.");
        if (opensWindow && defense.ImpactCuePrefab != null)
        {
            if (!defense.ImpactCuePrefab.TryGetComponent<BattleTelegraphCue>(out _))
                AddError(report, "skill.enemy_attack.cue.component.missing", blockIndex,
                    "타격 직전 전조 프리팹에는 BattleTelegraphCue가 필요합니다.");
        }
        if (opensWindow && (float.IsNaN(defense.AttackAnimationLeadTime)
            || float.IsInfinity(defense.AttackAnimationLeadTime)
            || defense.AttackAnimationLeadTime < 0f || defense.AttackAnimationLeadTime > defense.TimeWindow))
            AddError(report, "skill.enemy_attack.animation.lead.invalid", blockIndex,
                "공격 모션 시작 → 타격 시간은 0 이상, 방어 판정 시간 이하의 유효한 초여야 합니다.");
        if (defense.DefenseOpenDelay < 0f)
            AddError(report, "skill.enemy_attack.defense.open_delay.invalid", blockIndex, "판정창 직전 대기는 0 이상이어야 합니다.");
        if (defense.DelayAfter < 0f)
            AddError(report, "skill.enemy_attack.defense.after_delay.invalid", blockIndex, "판정 후 대기는 0 이상이어야 합니다.");
        if (defense.AttackAnimDelay < 0f)
            AddError(report, "skill.enemy_attack.defense.animation_delay.invalid", blockIndex, "공격 애니메이션 대기는 0 이상이어야 합니다.");
        if (requiresTelegraphTime && defense.TelegraphDuration <= 0f)
            AddError(report, "skill.enemy_attack.defense.telegraph.duration.invalid", blockIndex, "전조 지속 시간은 0보다 커야 합니다.");

        if (defense.UseTelegraph)
        {
            switch (defense.TelegraphVisualMode)
            {
                case TelegraphVisualMode.PrefabVFX when defense.WarningVfxPrefab == null:
                    AddError(report, "skill.enemy_attack.defense.telegraph.prefab.missing", blockIndex, "전조 VFX Prefab이 비어 있습니다.");
                    break;
                case TelegraphVisualMode.Sprite when defense.WarningSprite == null:
                    AddError(report, "skill.enemy_attack.defense.telegraph.sprite.missing", blockIndex, "전조 Sprite가 비어 있습니다.");
                    break;
                case TelegraphVisualMode.AnimatorTrigger when string.IsNullOrWhiteSpace(defense.TelegraphAnimatorTriggerName):
                    AddError(report, "skill.enemy_attack.defense.telegraph.animation.missing", blockIndex, "전조 Animator Trigger가 비어 있습니다.");
                    break;
            }

            if (string.IsNullOrWhiteSpace(defense.TelegraphAttachPivotName))
                AddWarning(report, "skill.enemy_attack.defense.telegraph.pivot.missing", blockIndex, "전조 부착 Pivot이 비어 있어 Actor 원점을 사용합니다.");
        }

        if (defense.FailDamageMultiplier < 0f)
            AddError(report, "skill.enemy_attack.defense.damage_multiplier.invalid", blockIndex, "실패 피해 배율은 0 이상이어야 합니다.");

        if (opensWindow && defense.OverrideTimingProfile)
        {
            DefenseTimingProfile profile = defense.TimingProfile;
            if (profile.PerfectWindow < 0f || profile.GreatWindow < 0f || profile.GoodWindow < 0f)
            {
                AddError(report, "skill.enemy_attack.defense.timing.negative", blockIndex, "판정 구간은 0 이상이어야 합니다.");
            }

            if (profile.PerfectWindow > profile.GreatWindow || profile.GreatWindow > profile.GoodWindow)
            {
                AddError(report, "skill.enemy_attack.defense.timing.order.invalid", blockIndex, "판정 구간은 Perfect ≤ Great ≤ Good 순서여야 합니다.");
            }

            if (profile.GoodWindow > defense.TimeWindow)
            {
                AddError(report, "skill.enemy_attack.defense.timing.exceeds_window", blockIndex, "Good 판정 구간이 전체 판정 시간보다 깁니다.");
            }
        }

        if (!opensWindow || !defense.ShakeOnFail)
            return;

        if (defense.FailShakeIntensity <= 0f)
            AddError(report, "skill.enemy_attack.defense.camera.intensity.invalid", blockIndex, "실패 카메라 강도는 0보다 커야 합니다.");
        if (defense.FailShakeDuration <= 0f)
            AddError(report, "skill.enemy_attack.defense.camera.duration.invalid", blockIndex, "실패 카메라 시간은 0보다 커야 합니다.");
        if (defense.FailShakeSafety == CameraShakeSafety.Cinematic)
        {
            AddWarning(report, "skill.enemy_attack.defense.camera.safety.cinematic", blockIndex, "방어 반응은 GameplaySafe 카메라 등급을 권장합니다.");
        }

        CameraShotStyle style = defense.FailShakeSafety == CameraShakeSafety.Cinematic
            ? CameraShotStyle.Dynamic
            : CameraShotStyle.GameplaySafe;
        float maxIntensity = CameraShotSettings
            .CreateBuiltIn(style, CameraLensDefaults.BattleActionOrthographicSize)
            .MaxImpulseIntensity;
        if (defense.FailShakeIntensity > maxIntensity)
        {
            AddWarning(
                report,
                "skill.enemy_attack.defense.camera.intensity.excessive",
                blockIndex,
                "실패 카메라 강도가 " + style + " 한도 " + maxIntensity.ToString("0.00") + "보다 커 런타임에서 제한됩니다.");
        }

        if (defense.FailShakeDuration > MaxDefenseFeedbackDuration)
        {
            AddWarning(
                report,
                "skill.enemy_attack.defense.camera.duration.excessive",
                blockIndex,
                "실패 카메라 시간이 권장 한도 " + MaxDefenseFeedbackDuration.ToString("0.00") + "초보다 깁니다.");
        }
    }

    private static void AddError(
        EnemyAttackAuthoringReport report,
        string code,
        int blockIndex,
        string message)
    {
        report.AddIssue(code, EnemyAttackAuthoringSeverity.Error, blockIndex, message);
    }

    private static void AddWarning(
        EnemyAttackAuthoringReport report,
        string code,
        int blockIndex,
        string message)
    {
        report.AddIssue(code, EnemyAttackAuthoringSeverity.Warning, blockIndex, message);
    }
}

public static class EnemyAttackTemplateFactory
{
    public static List<SkillActionBlock> CreateCounterableStrike()
    {
        List<SkillActionBlock> blocks = CreateTelegraphedStrike();
        var movement = (Action_Move)blocks[0];
        movement.Destination = Action_Move.MoveDest.Center;
        movement.DesignerLabel = "전투 중앙으로 이동";
        movement.EnemyHopHeight = 0.45f;
        var defense = (Action_DefenseWindow)blocks[1];
        defense.DesignerLabel = "특수공격: C 연계 반격 전용";
        defense.Note = "적은 전투 중앙에서 궁극기를 준비합니다. Z 가드와 X 회피 불가. C 성공은 남은 스킬을 중단하고 공격받은 한 명이 반격합니다. 전조 Animator Trigger는 적의 실제 Trigger에 맞추세요.";
        defense.Requirement = DefenseRequirement.Counterable;
        defense.CounterDamageMultiplier = 1.5f;
        defense.TelegraphDuration = 0.6f;
        defense.TimeWindow = 0.8f;
        defense.OverrideTimingProfile = false;
        defense.FailDamageMultiplier = 1f;
        var damage = (Action_Damage)blocks[2];
        damage.DesignerLabel = "특수공격 원래 피해";
        damage.SkillMultiplier = 1.8f;
        return blocks;
    }

    public static List<SkillActionBlock> CreateTelegraphedStrike()
    {
        return new List<SkillActionBlock>
        {
            new Action_Move
            {
                DesignerLabel = "자동 공격 위치로 접근",
                Destination = Action_Move.MoveDest.AttackStaging,
                Duration = 0.25f
            },
            new Action_DefenseWindow
            {
                DesignerLabel = "전조 후 방어 판정",
                PatternMode = EnemyDefensePatternMode.TelegraphThenWindow,
                Requirement = DefenseRequirement.ParryOrDodge,
                UseTelegraph = true,
                TelegraphVisualMode = TelegraphVisualMode.AnimatorTrigger,
                TelegraphAnimatorTriggerName = "Telegraph",
                TelegraphDuration = 0.45f,
                DefenseOpenDelay = 0.1f,
                TimeWindow = 0.65f,
                AllowNearSuccess = true,
                OverrideTimingProfile = true,
                TimingProfile = new DefenseTimingProfile(0.12f, 0.24f, 0.42f),
                FailDamageMultiplier = 1f,
                ShakeOnFail = true,
                FailShakeIntensity = 0.35f,
                FailShakeDuration = 0.2f,
                FailShakeSafety = CameraShakeSafety.GameplaySafe,
                AttackAnimTriggerName = "Attack",
                DelayAfter = 0.1f
            },
            new Action_Damage
            {
                DesignerLabel = "판정 결과 피해",
                SkillMultiplier = 1f,
                ShakeCamera = false
            },
            new Action_Move
            {
                DesignerLabel = "원래 전투 위치로 복귀",
                Destination = Action_Move.MoveDest.OriginalPos,
                Duration = 0.25f
            }
        };
    }
}
