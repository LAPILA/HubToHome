#if UNITY_EDITOR
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed class ItemContentValidationTests
{
    [Test]
    public void ValidatorReportsInvalidConsumableEffectsAndStackValues()
    {
        ItemData noEffect = ScriptableObject.CreateInstance<ItemData>();
        ItemData badTarget = ScriptableObject.CreateInstance<ItemData>();
        ItemData badStatus = ScriptableObject.CreateInstance<ItemData>();
        try
        {
            noEffect.ItemID = "item.no_effect";
            noEffect.ActionType = EffectActionType.None;
            noEffect.IsStackable = true;
            noEffect.MaxStackSize = 0;

            badTarget.ItemID = "item.bad_target";
            badTarget.ActionType = EffectActionType.Heal;
            badTarget.TargetStat = TargetStatType.None;

            badStatus.ItemID = "item.bad_status";
            badStatus.ActionType = EffectActionType.ApplyStatus;
            badStatus.StatusEffectID = "not_registered";
            badStatus.StatusDurationTurns = 0;

            var snapshot = new ProjectContentSnapshot();
            snapshot.Items.Add(noEffect);
            snapshot.Items.Add(badTarget);
            snapshot.Items.Add(badStatus);

            ContentValidationReport report = ProjectContentValidator.Validate(snapshot);
            string[] codes = report.Issues.Select(issue => issue.Code).ToArray();

            Assert.That(codes, Does.Contain("item.consumable.effect.missing"));
            Assert.That(codes, Does.Contain("item.consumable.target_stat.invalid"));
            Assert.That(codes, Does.Contain("item.consumable.status.unknown"));
            Assert.That(codes, Does.Contain("item.consumable.status_duration.invalid"));
            Assert.That(codes, Does.Contain("item.stack.max.invalid"));
        }
        finally
        {
            Object.DestroyImmediate(noEffect);
            Object.DestroyImmediate(badTarget);
            Object.DestroyImmediate(badStatus);
        }
    }
}
#endif
