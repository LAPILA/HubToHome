public static class DialoguePlaybackPolicy
{
    public static bool TryValidate(DialogueData dialogue, out string error)
    {
        if (dialogue == null)
        {
            error = "Dialogue data is missing.";
            return false;
        }

        if (dialogue.Nodes == null || dialogue.Nodes.Count == 0)
        {
            error = "Dialogue has no nodes.";
            return false;
        }

        for (int nodeIndex = 0; nodeIndex < dialogue.Nodes.Count; nodeIndex++)
        {
            DialogueNode node = dialogue.Nodes[nodeIndex];
            if (node == null)
            {
                error = "Dialogue contains null node at index " + nodeIndex + ".";
                return false;
            }

            if (!node.IsChoiceNode || node.Choices == null)
                continue;

            for (int choiceIndex = 0; choiceIndex < node.Choices.Count; choiceIndex++)
            {
                if (node.Choices[choiceIndex] != null)
                    continue;

                error = "Dialogue contains null choice at node "
                    + nodeIndex
                    + ", choice "
                    + choiceIndex
                    + ".";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }
}
