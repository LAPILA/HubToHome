using UnityEngine;

public enum EnemyMeleePassExtent
{
    [InspectorName("대상 뒤 짧게 통과")] Local = 0,
    [InspectorName("화면 양 끝 관통")] ScreenWide = 1
}

/// <summary>한 번 포착한 목표점으로만 이동합니다. 회피 중인 대상은 추적하지 않습니다.</summary>
public readonly struct BattleEnemyMeleeDashPath
{
    public Vector3 Start { get; }
    public Vector3 Entry { get; }
    public Vector3 Contact { get; }
    public Vector3 Exit { get; }
    public float ImpactTime { get; }
    public float DashDuration { get; }
    public float ExitDuration { get; }
    public float Direction { get; }
    public bool IsScreenWide { get; }
    public bool IsContinuous { get; }
    public float EntryDuration { get; }
    public float ApproachHeight { get; }
    public float DashStartTime => ImpactTime - DashDuration;

    public BattleEnemyMeleeDashPath(Vector3 start, Vector3 target, float impactTime,
        float dashDuration, float passDistance, float exitDuration)
    {
        Start = start;
        Entry = start;
        ImpactTime = Safe(impactTime, 0.4f, 0.01f, float.MaxValue);
        DashDuration = Mathf.Min(ImpactTime, Safe(dashDuration, 0.12f, 0.04f, 0.3f));
        ExitDuration = Safe(exitDuration, 0.09f, 0.04f, 0.25f);
        Direction = target.x < start.x ? -1f : 1f;
        Contact = new Vector3(target.x - Direction * 0.35f, start.y, start.z);
        Exit = new Vector3(target.x + Direction * Safe(passDistance, 0.95f, 0.4f, 1.5f), start.y, start.z);
        IsScreenWide = false;
        IsContinuous = false;
        EntryDuration = 0f;
        ApproachHeight = 0f;
    }

    private BattleEnemyMeleeDashPath(Vector3 start, BattleScreenPassPath screen, float impactTime,
        float duration, float entryDuration)
    {
        Start = start;
        bool leftToRight = screen.TargetX >= start.x;
        Direction = leftToRight ? 1f : -1f;
        Entry = leftToRight ? screen.Left : screen.Right;
        Exit = leftToRight ? screen.Right : screen.Left;
        Contact = new Vector3(screen.TargetX, Entry.y, Entry.z);
        ImpactTime = impactTime;
        float crossing = screen.CrossingProgress(leftToRight);
        // 화면 배치가 비대칭이어도 같은 속도로 교차합니다. 짧은 공격의 판정 시각은 늘리지 않습니다.
        float total = Mathf.Min(duration, impactTime / crossing);
        DashDuration = total * crossing;
        ExitDuration = total * (1f - crossing);
        EntryDuration = entryDuration;
        IsScreenWide = true;
        IsContinuous = false;
        ApproachHeight = 0f;
    }

    private BattleEnemyMeleeDashPath(BattleEnemyMeleeDashPath source, float height, float entryDuration)
    {
        Start = source.Start;
        Entry = source.Entry + Vector3.up * height;
        Contact = source.Contact;
        Exit = source.Exit;
        ImpactTime = source.ImpactTime;
        DashDuration = source.DashDuration;
        ExitDuration = source.ExitDuration;
        Direction = source.Direction;
        IsScreenWide = source.IsScreenWide;
        IsContinuous = source.IsContinuous;
        EntryDuration = entryDuration;
        ApproachHeight = height;
    }

    private BattleEnemyMeleeDashPath(BattleEnemyMeleeDashPath source, float minimumExitDuration, bool preserveImpactTime)
    {
        Start = source.Start;
        Entry = source.Entry;
        Contact = source.Contact;
        Exit = source.Exit;
        DashDuration = preserveImpactTime ? source.ImpactTime : source.DashDuration;
        ImpactTime = DashDuration;
        ExitDuration = Mathf.Max(source.ExitDuration,
            Safe(minimumExitDuration, 0f, 0f, float.MaxValue));
        Direction = source.Direction;
        IsScreenWide = source.IsScreenWide;
        IsContinuous = true;
        EntryDuration = source.EntryDuration;
        ApproachHeight = source.ApproachHeight;
    }

    public BattleEnemyMeleeDashPath WithContinuousTiming(float minimumExitDuration, bool preserveImpactTime = false)
        => Positive(DashDuration) && Positive(ExitDuration)
            ? new BattleEnemyMeleeDashPath(this, minimumExitDuration, preserveImpactTime) : this;

    public BattleEnemyMeleeDashPath WithApproachHeight(float height, float entryDuration)
        => Positive(height) && Positive(entryDuration)
            ? new BattleEnemyMeleeDashPath(this, Mathf.Min(height, 8f), entryDuration) : this;

    public static bool TryScreenWide(Vector3 start, BattleScreenPassPath screen, float impactTime,
        float duration, float entryDuration, out BattleEnemyMeleeDashPath path)
    {
        path = default;
        float crossing = screen.CrossingProgress(screen.TargetX >= start.x);
        if (!Positive(impactTime) || !Positive(duration) || !Positive(entryDuration)
            || !Positive(screen.Right.x - screen.Left.x) || !Positive(crossing) || crossing >= 1f
            || !Finite(start.x) || !Finite(start.y) || !Finite(start.z)
            || !Finite(screen.Left.y) || !Finite(screen.Left.z) || !Finite(screen.Right.y) || !Finite(screen.Right.z)
            || screen.Left.y != screen.Right.y || screen.Left.z != screen.Right.z || screen.Left.z != start.z) return false;
        path = new BattleEnemyMeleeDashPath(start, screen, impactTime, duration, entryDuration);
        return Positive(path.DashDuration) && Positive(path.ExitDuration);
    }

    public float DashElapsed(float authoredAttackTime)
    {
        if (float.IsNaN(authoredAttackTime)) return 0f;
        return Mathf.Clamp(authoredAttackTime - DashStartTime, 0f, DashDuration);
    }

    public Vector3 SampleApproach(float authoredAttackTime)
    {
        float t = DashElapsed(authoredAttackTime) / DashDuration;
        return Vector3.LerpUnclamped(Entry, Contact, IsScreenWide || IsContinuous ? t : t * t * t);
    }

    public static bool CanUse(bool enabled, bool isProjectile, int targetCount,
        bool targetIsPlayer, DefenseRequirement requirement, EnemyDefensePatternMode pattern)
    {
        return enabled && !isProjectile && targetCount == 1 && targetIsPlayer
            && !DefenseJudgementPolicy.IsCounterRequirement(requirement)
            && pattern != EnemyDefensePatternMode.TelegraphThenNextTurnWindow;
    }

    private static float Safe(float value, float fallback, float min, float max)
    {
        return float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp(value, min, max);
    }

    private static bool Positive(float value) => Finite(value) && value > 0f;
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
