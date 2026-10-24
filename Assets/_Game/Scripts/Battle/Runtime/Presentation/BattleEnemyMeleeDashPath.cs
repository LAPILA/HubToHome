using UnityEngine;

/// <summary>한 번 포착한 목표점으로만 이동합니다. 회피 중인 대상은 추적하지 않습니다.</summary>
public readonly struct BattleEnemyMeleeDashPath
{
    public Vector3 Start { get; }
    public Vector3 Contact { get; }
    public Vector3 Exit { get; }
    public float ImpactTime { get; }
    public float DashDuration { get; }
    public float ExitDuration { get; }
    public float Direction { get; }
    public float DashStartTime => ImpactTime - DashDuration;

    public BattleEnemyMeleeDashPath(Vector3 start, Vector3 target, float impactTime,
        float dashDuration, float passDistance, float exitDuration)
    {
        Start = start;
        ImpactTime = Safe(impactTime, 0.4f, 0.01f, float.MaxValue);
        DashDuration = Mathf.Min(ImpactTime, Safe(dashDuration, 0.12f, 0.04f, 0.3f));
        ExitDuration = Safe(exitDuration, 0.09f, 0.04f, 0.25f);
        Direction = target.x < start.x ? -1f : 1f;
        Contact = new Vector3(target.x - Direction * 0.35f, start.y, start.z);
        Exit = new Vector3(target.x + Direction * Safe(passDistance, 0.95f, 0.4f, 1.5f), start.y, start.z);
    }

    public float DashElapsed(float authoredAttackTime)
    {
        if (float.IsNaN(authoredAttackTime)) return 0f;
        return Mathf.Clamp(authoredAttackTime - DashStartTime, 0f, DashDuration);
    }

    public Vector3 SampleApproach(float authoredAttackTime)
    {
        float t = DashElapsed(authoredAttackTime) / DashDuration;
        return Vector3.LerpUnclamped(Start, Contact, t * t * t);
    }

    public static bool CanUse(bool enabled, bool isProjectile, int targetCount,
        bool targetIsPlayer, DefenseRequirement requirement, EnemyDefensePatternMode pattern)
    {
        return enabled && !isProjectile && targetCount == 1 && targetIsPlayer
            && requirement != DefenseRequirement.Counterable
            && pattern != EnemyDefensePatternMode.TelegraphThenNextTurnWindow;
    }

    private static float Safe(float value, float fallback, float min, float max)
    {
        return float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp(value, min, max);
    }
}
