#if UNITY_EDITOR
using System;

public static class ProjectContentValidator
{
    public static ContentValidationReport Validate(ProjectContentSnapshot snapshot)
    {
        if (snapshot == null)
            throw new ArgumentNullException(nameof(snapshot));

        var report = new ContentValidationReport();
        var context = new ContentValidationRuleContext(snapshot, report);

        ContentIdentityRules.Validate(context);
        RuntimeCatalogContentRules.Validate(context);
        ScenarioContentRules.Validate(context);
        BattleContentRules.Validate(context);
        SkillItemContentRules.Validate(context);

        return report;
    }
}
#endif
