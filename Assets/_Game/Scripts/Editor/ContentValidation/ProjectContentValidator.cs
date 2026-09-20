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
        ShopContentRules.Validate(context);

        return report;
    }

    public static ContentValidationReport ValidateSkillReferences(ProjectContentSnapshot snapshot, SkillData skill)
    {
        if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
        if (skill == null) throw new ArgumentNullException(nameof(skill));
        if (!snapshot.Skills.Contains(skill))
            throw new ArgumentException("선택한 스킬이 검사 목록에 없습니다. 참조 목록을 다시 검사하세요.", nameof(skill));
        var all = new ContentValidationReport();
        var context = new ContentValidationRuleContext(snapshot, all);
        ContentIdentityRules.ValidateSkills(context);
        RuntimeCatalogContentRules.ValidateSkills(context);
        var selected = new ContentValidationReport();
        foreach (ContentValidationIssue issue in all.Issues)
            if (issue.Context == skill || issue.Context == snapshot.Catalog || issue.Code == "catalog.missing")
                selected.Add(issue);
        return selected;
    }
}
#endif
