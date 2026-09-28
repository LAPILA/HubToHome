using System.Collections.Generic;

public static class BattleTurnQueueProjection
{
    public static List<CharacterBase> BuildVisible(
        IReadOnlyList<CharacterBase> turnQueue,
        int currentActorIndex,
        int visibleCount)
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

        // The turn scheduler owns all future entries. Never invent a round-robin fallback here.
        return visible;
    }
}

