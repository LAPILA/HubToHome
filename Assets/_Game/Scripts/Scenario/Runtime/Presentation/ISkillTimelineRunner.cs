using System.Collections;
using System.Collections.Generic;

public interface ISkillTimelineRunner
{
    IEnumerator PlaySkillTimeline(
        string skillId,
        string actorId,
        IReadOnlyList<string> targetIds,
        ActionExecutionContext context);
}
