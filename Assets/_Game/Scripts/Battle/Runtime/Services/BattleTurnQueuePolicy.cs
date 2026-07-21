using System.Collections.Generic;

public static class BattleTurnQueuePolicy
{
    public static bool PromoteFirstPlayer(IList<CharacterBase> queue)
    {
        if (queue == null || queue.Count == 0) return false;
        if (queue[0] is PlayerCharacter) return true;

        for (int i = 1; i < queue.Count; i++)
        {
            if (!(queue[i] is PlayerCharacter)) continue;
            CharacterBase first = queue[0];
            queue[0] = queue[i];
            queue[i] = first;
            return true;
        }

        return false;
    }
}
