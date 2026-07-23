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

            if (block is Action_DefenseWindow)
                report.DefenseWindowCount++;
            if (block is Action_Damage || block is Action_Projectile || block is Action_SequentialMelee)
                report.DamageBlockCount++;

            ValidateBlock(block, i, report);
        }

        report.EstimatedDuration = cursor;
        return report;
    }

    private static void ValidateBlock(
        SkillActionBlock block,
        int blockIndex,
        EnemyAttackAuthoringReport report)
    {
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

        if (opensWindow && defense.TimeWindow <= 0f)
            AddError(report, "skill.enemy_attack.defense.window.invalid", blockIndex, "방어 판정 시간은 0보다 커야 합니다.");
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
