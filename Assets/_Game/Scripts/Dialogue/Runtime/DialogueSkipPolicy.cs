/// <summary>대사 표시만 생략할 수 있으며, 결정이 필요한 노드는 생략할 수 없습니다.</summary>
public static class DialogueSkipPolicy
{
    public static bool RequiresInput(DialogueNode node)
        => node == null || node.IsChoiceNode || node.EventTriggerID == "RequestName";
}
