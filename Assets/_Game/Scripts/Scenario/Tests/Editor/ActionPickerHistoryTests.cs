using System.Collections.Generic;
using NUnit.Framework;

public class ActionPickerHistoryTests
{
    [Test]
    public void FavoritesPersistAndToggleOff()
    {
        var preferences = new MemoryPreferences();
        var first = new ActionPickerHistory(preferences);

        Assert.That(first.ToggleFavorite("flow.wait"), Is.True);
        Assert.That(new ActionPickerHistory(preferences).IsFavorite("flow.wait"), Is.True);
        Assert.That(first.ToggleFavorite("flow.wait"), Is.False);
        Assert.That(new ActionPickerHistory(preferences).IsFavorite("flow.wait"), Is.False);
    }

    [Test]
    public void RecentMovesReusedActionToFrontWithoutDuplicates()
    {
        var history = new ActionPickerHistory(new MemoryPreferences());
        history.RecordRecent("flow.wait");
        history.RecordRecent("dialogue.wait");
        history.RecordRecent("flow.wait");

        Assert.That(history.Recent, Is.EqualTo(new[] { "flow.wait", "dialogue.wait" }));
    }

    [Test]
    public void RecentListKeepsOnlyConfiguredMaximum()
    {
        var history = new ActionPickerHistory(new MemoryPreferences());
        for (int i = 0; i < ActionPickerHistory.MaximumRecentCount + 4; i++)
        {
            history.RecordRecent("test.action." + i);
        }

        Assert.That(history.Recent, Has.Count.EqualTo(ActionPickerHistory.MaximumRecentCount));
        Assert.That(history.Recent[0], Is.EqualTo("test.action.15"));
        Assert.That(history.Recent[history.Recent.Count - 1], Is.EqualTo("test.action.4"));
    }

    private sealed class MemoryPreferences : ISequenceMakerPreferences
    {
        private readonly Dictionary<string, string> _strings = new Dictionary<string, string>();

        public float GetFloat(string key, float defaultValue) { return defaultValue; }
        public bool GetBool(string key, bool defaultValue) { return defaultValue; }
        public string GetString(string key, string defaultValue)
        {
            return _strings.TryGetValue(key, out string value) ? value : defaultValue;
        }
        public void SetFloat(string key, float value) { }
        public void SetBool(string key, bool value) { }
        public void SetString(string key, string value) { _strings[key] = value; }
    }
}
