using System.Collections.Generic;
using NUnit.Framework;

public class ActionBlockSummaryTests
{
    [Test]
    public void CatalogTemplateFormatsLiteralParameters()
    {
        ActionCatalogEntry entry = Entry("actor.move", "캐릭터 이동", "{actor} → {to} · {duration}초");
        ScenarioActionData action = Action(
            "actor.move",
            "{\"actor\":\"ZEV\",\"to\":\"center\",\"duration\":0.5}");

        ActionBlockSummary summary = ActionBlockSummary.Build(action, entry);

        Assert.That(summary.Title, Is.EqualTo("캐릭터 이동"));
        Assert.That(summary.Summary, Is.EqualTo("ZEV → center · 0.5초"));
        Assert.That(summary.HasParameterError, Is.False);
    }

    [Test]
    public void DesignerLabelOverridesGlobalNameButKeepsCatalogSummary()
    {
        ActionCatalogEntry entry = Entry("screen.fade", "화면 페이드", "{mode} · {duration}초");
        ScenarioActionData action = Action(
            "screen.fade",
            "{\"mode\":\"out\",\"duration\":0.4}");
        action.DesignerLabel = "전투 전환 암전";
        action.Note = "슈팅 전환 직전";

        ActionBlockSummary summary = ActionBlockSummary.Build(action, entry);

        Assert.That(summary.Title, Is.EqualTo("전투 전환 암전"));
        Assert.That(summary.Summary, Is.EqualTo("out · 0.4초"));
        Assert.That(summary.Note, Is.EqualTo("슈팅 전환 직전"));
    }

    [Test]
    public void QuickValuesUseCatalogOrderAndFormatBindingsReadably()
    {
        ActionCatalogEntry entry = Entry("actor.move", "캐릭터 이동", "{actor} → {to}");
        entry.Parameters.Add(new ActionCatalogParameter
        {
            Name = "actor",
            DisplayNameKo = "캐릭터",
            QuickEdit = true
        });
        entry.Parameters.Add(new ActionCatalogParameter
        {
            Name = "duration",
            DisplayNameKo = "시간",
            UnitKo = "초",
            QuickEdit = true
        });
        ScenarioActionData action = Action(
            "actor.move",
            "{\"actor\":{\"$bind\":\"input.actor\"},\"duration\":1.25}");

        ActionBlockSummary summary = ActionBlockSummary.Build(action, entry);

        Assert.That(summary.QuickValues, Has.Count.EqualTo(2));
        Assert.That(summary.QuickValues[0].Label, Is.EqualTo("캐릭터"));
        Assert.That(summary.QuickValues[0].Value, Is.EqualTo("${input.actor}"));
        Assert.That(summary.QuickValues[1].Value, Is.EqualTo("1.25초"));
    }

    [Test]
    public void MissingTemplateParameterUsesClearPlaceholder()
    {
        ActionCatalogEntry entry = Entry("flow.wait", "대기", "{duration}초 대기 · {reason}");
        ScenarioActionData action = Action("flow.wait", "{\"duration\":0.2}");

        ActionBlockSummary summary = ActionBlockSummary.Build(action, entry);

        Assert.That(summary.Summary, Is.EqualTo("0.2초 대기 · 값 없음"));
    }

    [Test]
    public void InvalidJsonProducesStableErrorSummaryInsteadOfThrowing()
    {
        ActionCatalogEntry entry = Entry("flow.wait", "대기", "{duration}초 대기");
        ScenarioActionData action = Action("flow.wait", "{bad json");

        ActionBlockSummary summary = ActionBlockSummary.Build(action, entry);

        Assert.That(summary.HasParameterError, Is.True);
        Assert.That(summary.Summary, Is.EqualTo("파라미터 JSON 오류"));
    }

    [Test]
    public void ParallelBlockGetsStructuralSummaryAndPolicy()
    {
        ScenarioActionData action = Action(
            ActionDirector.ParallelActionId,
            "{\"policy\":\"race\"}");
        action.Children.Add(Action("flow.wait", "{\"duration\":0.1}"));
        action.Children.Add(Action("screen.fade", "{\"mode\":\"out\"}"));

        ActionBlockSummary summary = ActionBlockSummary.Build(action, null);

        Assert.That(summary.Title, Is.EqualTo("동시 실행"));
        Assert.That(summary.Summary, Is.EqualTo("2개 블록 · 먼저 끝난 결과 사용"));
        Assert.That(summary.IsStructural, Is.True);
    }

    [Test]
    public void UnknownActionFallsBackToActionIdAndCompactJson()
    {
        ScenarioActionData action = Action("custom.action", "{\"value\":42}");

        ActionBlockSummary summary = ActionBlockSummary.Build(action, null);

        Assert.That(summary.Title, Is.EqualTo("custom.action"));
        Assert.That(summary.Summary, Does.Contain("value"));
    }

    private static ActionCatalogEntry Entry(string id, string name, string template)
    {
        return new ActionCatalogEntry
        {
            ActionId = id,
            DisplayNameKo = name,
            SummaryTemplateKo = template,
            Category = "test",
            AccentHex = "#3F9CA3"
        };
    }

    private static ScenarioActionData Action(string id, string parameters)
    {
        return new ScenarioActionData
        {
            BlockId = ScenarioBlockIdentity.Create(),
            ActionId = id,
            ParametersJson = parameters
        };
    }
}
