using UnityEngine;

public readonly struct PreemptiveAttackArea
{
    public PreemptiveAttackArea(Vector2 center, Vector2 size, Vector2 facing)
    {
        Center = center;
        Size = size;
        Facing = facing;
    }

    public Vector2 Center { get; }
    public Vector2 Size { get; }
    public Vector2 Facing { get; }
}

public static class PreemptiveAttackGeometry
{
    public static PreemptiveAttackArea Create(
        Vector2 origin,
        Vector2 facing,
        float forwardRange,
        float width)
    {
        Vector2 cardinalFacing = ToCardinal(facing);
        float safeRange = Mathf.Max(0f, forwardRange);
        float safeWidth = Mathf.Max(0f, width);
        Vector2 size = Mathf.Abs(cardinalFacing.x) > 0f
            ? new Vector2(safeRange, safeWidth)
            : new Vector2(safeWidth, safeRange);
        Vector2 center = origin + cardinalFacing * (safeRange * 0.5f);

        return new PreemptiveAttackArea(center, size, cardinalFacing);
    }

    private static Vector2 ToCardinal(Vector2 facing)
    {
        if (facing.sqrMagnitude < 0.0001f)
            return Vector2.down;

        if (Mathf.Abs(facing.x) >= Mathf.Abs(facing.y))
            return facing.x >= 0f ? Vector2.right : Vector2.left;

        return facing.y >= 0f ? Vector2.up : Vector2.down;
    }
}
