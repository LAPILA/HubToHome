using System;
using System.Reflection;
using NUnit.Framework;

public sealed class DialogueUIContractTests
{
    [Test]
    public void ToolkitPresenterExposesDialogueManagerCommandContract()
    {
        Type presenterType = typeof(DialogueUIToolkit);
        string[] methodNames =
        {
            "RebindCanvasCameraImmediate",
            "OpenPanel",
            "DisplayNode",
            "DisplayPrompt",
            "ShowChoices",
            "SkipTyping",
            "ClosePanel",
            "HideImmediate"
        };

        for (int i = 0; i < methodNames.Length; i++)
            Assert.That(presenterType.GetMethod(methodNames[i], BindingFlags.Instance | BindingFlags.Public), Is.Not.Null, methodNames[i]);
    }

    [Test]
    public void DialogueManagerPresentationFieldsUseToolkitImplementation()
    {
        FieldInfo overworld = typeof(DialogueManager).GetField("_overworldPanel", BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo cinematic = typeof(DialogueManager).GetField("_cinematicPanel", BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.That(overworld, Is.Not.Null);
        Assert.That(cinematic, Is.Not.Null);
        Assert.That(overworld.FieldType, Is.EqualTo(typeof(DialogueUIToolkit)));
        Assert.That(cinematic.FieldType, Is.EqualTo(typeof(DialogueUIToolkit)));
    }
}
