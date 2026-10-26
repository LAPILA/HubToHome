using System;
using UnityEngine;

public readonly struct OverworldFollowerPose
{
    public OverworldFollowerPose(Vector3 position, int facingDirection)
    {
        Position = position;
        FacingDirection = facingDirection;
    }

    public Vector3 Position { get; }
    public int FacingDirection { get; }
}

/// <summary>
/// Bounded recording of actual world positions. Distances are absolute within one Epoch;
/// expired samples fail rather than clamping across an unrecorded part of the path.
/// </summary>
public sealed class OverworldFollowerTrail
{
    private struct Point
    {
        public Vector3 Position;
        public int FacingDirection;
        public int TravelFacingDirection;
        public double Distance;
    }

    private readonly Point[] _points;
    private int _oldest;
    private int _count;
    private uint _epoch;

    public OverworldFollowerTrail(int capacity = 1024)
    {
        if (capacity < 2) throw new ArgumentOutOfRangeException(nameof(capacity));
        _points = new Point[capacity];
    }

    public uint Epoch => _epoch;
    public int Count => _count;
    public int Capacity => _points.Length;
    public float HeadDistance => _count == 0 ? 0f : (float)GetPoint(_count - 1).Distance;
    public float OldestDistance => _count == 0 ? 0f : (float)GetPoint(0).Distance;

    /// <summary>Starts a new cursor generation without inventing a path behind the leader.</summary>
    public void Reset(Vector3 position, int facing)
    {
        if (!IsFinite(position)) throw new ArgumentException("Position must be finite.", nameof(position));
        if (!IsFacingValid(facing)) throw new ArgumentOutOfRangeException(nameof(facing));

        unchecked { _epoch++; }
        if (_epoch == 0) _epoch = 1;
        _oldest = 0;
        _count = 1;
        _points[0] = new Point
        {
            Position = position, FacingDirection = facing, TravelFacingDirection = facing, Distance = 0d
        };
    }

    /// <summary>
    /// Returns true only when a new point was appended. A stationary record updates the
    /// head's facing without consuming capacity. Invalid input leaves the trail unchanged.
    /// </summary>
    public bool Record(Vector3 position, int facing)
    {
        if (!IsFinite(position) || !IsFacingValid(facing)) return false;
        if (_count == 0)
        {
            Reset(position, facing);
            return true;
        }

        int headIndex = (_oldest + _count - 1) % Capacity;
        Point head = _points[headIndex];
        if (SamePosition(head.Position, position))
        {
            head.FacingDirection = facing;
            _points[headIndex] = head;
            return false;
        }

        // Double accumulation avoids per-frame float drift; the public cursor remains float.
        double dx = (double)position.x - head.Position.x;
        double dy = (double)position.y - head.Position.y;
        double dz = (double)position.z - head.Position.z;
        double distance = head.Distance + Math.Sqrt(dx * dx + dy * dy + dz * dz);
        if (distance > float.MaxValue || distance <= head.Distance) return false;

        int index = (_oldest + _count) % Capacity;
        if (_count == Capacity)
            _oldest = (_oldest + 1) % Capacity;
        else
            _count++;
        _points[index] = new Point
        {
            Position = position, FacingDirection = facing, TravelFacingDirection = facing, Distance = distance
        };
        return true;
    }

    public bool TrySample(float distance, out OverworldFollowerPose pose)
    {
        pose = default;
        if (_count == 0 || !IsFinite(distance)
            || distance < OldestDistance || distance > HeadDistance)
            return false;

        // Keep the public float boundary values anchored to the actual stored endpoints.
        if (distance == HeadDistance)
        {
            pose = ToPose(GetPoint(_count - 1));
            return true;
        }
        if (distance == OldestDistance)
        {
            pose = ToPose(GetPoint(0));
            return true;
        }

        int low = 0;
        int high = _count - 1;
        while (low + 1 < high)
        {
            int middle = low + (high - low) / 2;
            if (GetPoint(middle).Distance <= distance) low = middle;
            else high = middle;
        }

        Point from = GetPoint(low);
        Point to = GetPoint(high);
        if (distance == from.Distance)
        {
            pose = ToPose(from);
            return true;
        }
        double t = (distance - from.Distance) / (to.Distance - from.Distance);
        Vector3 position = new Vector3(
            (float)(from.Position.x + ((double)to.Position.x - from.Position.x) * t),
            (float)(from.Position.y + ((double)to.Position.y - from.Position.y) * t),
            (float)(from.Position.z + ((double)to.Position.z - from.Position.z) * t));
        // Turning in place at the head changes its idle pose, not the recorded travel leg.
        pose = new OverworldFollowerPose(position, to.TravelFacingDirection);
        return true;
    }

    public bool TrySample(float distance, uint epoch, out OverworldFollowerPose pose)
    {
        if (epoch != _epoch)
        {
            pose = default;
            return false;
        }
        return TrySample(distance, out pose);
    }

    /// <summary>
    /// Finds the nearest retained segment within tolerance. At an exact self-intersection,
    /// prefer the newest distance. This resolves a cursor only; it never moves an actor.
    /// </summary>
    public bool TryFindDistance(Vector3 position, float tolerance, out float distance)
    {
        distance = 0f;
        if (_count == 0 || !IsFinite(position) || !IsFinite(tolerance) || tolerance < 0f)
            return false;

        Point first = GetPoint(0);
        double bestSquared = SquaredDistance(position, first.Position);
        double bestDistance = first.Distance;
        for (int i = 1; i < _count; i++)
        {
            Point from = GetPoint(i - 1);
            Point to = GetPoint(i);
            double dx = (double)to.Position.x - from.Position.x;
            double dy = (double)to.Position.y - from.Position.y;
            double dz = (double)to.Position.z - from.Position.z;
            double lengthSquared = dx * dx + dy * dy + dz * dz;
            double t = (((double)position.x - from.Position.x) * dx
                + ((double)position.y - from.Position.y) * dy
                + ((double)position.z - from.Position.z) * dz) / lengthSquared;
            t = Math.Max(0d, Math.Min(1d, t));
            double offsetX = from.Position.x + dx * t - position.x;
            double offsetY = from.Position.y + dy * t - position.y;
            double offsetZ = from.Position.z + dz * t - position.z;
            double squared = offsetX * offsetX + offsetY * offsetY + offsetZ * offsetZ;
            double candidateDistance = from.Distance + (to.Distance - from.Distance) * t;
            if (squared < bestSquared || (squared == bestSquared && candidateDistance > bestDistance))
            {
                bestSquared = squared;
                bestDistance = candidateDistance;
            }
        }

        if (bestSquared > (double)tolerance * tolerance) return false;
        distance = (float)bestDistance;
        return true;
    }

    private Point GetPoint(int logicalIndex) => _points[(_oldest + logicalIndex) % Capacity];

    private static OverworldFollowerPose ToPose(Point point)
        => new OverworldFollowerPose(point.Position, point.FacingDirection);

    private static bool SamePosition(Vector3 a, Vector3 b)
        => a.x == b.x && a.y == b.y && a.z == b.z;

    private static bool IsFacingValid(int facing) => facing >= 0 && facing <= 3;
    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private static bool IsFinite(Vector3 value) => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

    private static double SquaredDistance(Vector3 a, Vector3 b)
    {
        double dx = (double)a.x - b.x;
        double dy = (double)a.y - b.y;
        double dz = (double)a.z - b.z;
        return dx * dx + dy * dy + dz * dz;
    }
}
