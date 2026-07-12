using System.Collections.Generic;

public static class BattleTurnQueueProjection
{
    public static List<CharacterBase> BuildVisible(
        IReadOnlyList<CharacterBase> turnQueue,
        int currentActorIndex,
        int visibleCount,
        IReadOnlyList<PlayerCharacter> players,
        IReadOnlyList<EnemyCharacter> enemies)
    {
        int safeVisibleCount = visibleCount > 0 ? visibleCount : 0;
        var visible = new List<CharacterBase>(safeVisibleCount);
        if (safeVisibleCount == 0 || turnQueue == null || turnQueue.Count == 0)
        {
            return visible;
        }

        int startIndex = currentActorIndex > 0 ? currentActorIndex : 0;
        for (int i = startIndex; i < turnQueue.Count && visible.Count < safeVisibleCount; i++)
        {
            CharacterBase actor = turnQueue[i];
            if (actor != null && actor.IsAlive)
            {
                visible.Add(actor);
            }
        }

        if (visible.Count >= safeVisibleCount)
        {
            return visible;
        }

        var aliveActors = new List<CharacterBase>();
        AddAlive(players, aliveActors);
        AddAlive(enemies, aliveActors);
        aliveActors.Sort(CompareSpeedDescending);

        int refillIndex = 0;
        while (visible.Count < safeVisibleCount && aliveActors.Count > 0)
        {
            visible.Add(aliveActors[refillIndex % aliveActors.Count]);
            refillIndex++;
        }

        return visible;
    }

    private static void AddAlive<T>(IReadOnlyList<T> source, List<CharacterBase> destination)
        where T : CharacterBase
    {
        if (source == null)
        {
            return;
        }

        for (int i = 0; i < source.Count; i++)
        {
            T actor = source[i];
            if (actor != null && actor.IsAlive)
            {
                destination.Add(actor);
            }
        }
    }

    private static int CompareSpeedDescending(CharacterBase left, CharacterBase right)
    {
        return right.SPD.CompareTo(left.SPD);
    }
}

