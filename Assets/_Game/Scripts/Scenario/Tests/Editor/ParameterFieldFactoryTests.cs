using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

public class ParameterFieldFactoryTests
{
    [TestCase("string", "text", ParameterEditorKind.Text)]
    [TestCase("number", "number", ParameterEditorKind.Number)]
    [TestCase("int", "number", ParameterEditorKind.Integer)]
    [TestCase("duration", "number", ParameterEditorKind.Duration)]
    [TestCase("bool", "toggle", ParameterEditorKind.Toggle)]
    [TestCase("enum", "segmented", ParameterEditorKind.Enum)]
    [TestCase("color", "color", ParameterEditorKind.Color)]
    [TestCase("vector2", "vector2", ParameterEditorKind.Vector2)]
    [TestCase("vector3", "vector3", ParameterEditorKind.Vector3)]
    [TestCase("actorRef", "actor", ParameterEditorKind.Reference)]
    [TestCase("dialogueRef", "dialogue", ParameterEditorKind.Reference)]
    [TestCase("audioRef", "audio", ParameterEditorKind.Reference)]
    [TestCase("uiRef", "ui", ParameterEditorKind.Reference)]
    [TestCase("moduleRef", "module", ParameterEditorKind.Reference)]
    [TestCase("animationRef", "animation", ParameterEditorKind.Reference)]
    [TestCase("object", "input_map", ParameterEditorKind.Json)]
    public void ResolvesCatalogTypesToStableEditorKinds(
        string type,
        string control,
        ParameterEditorKind expected)
    {
        var parameter = new ActionCatalogParameter
        {
            Type = type,
            EditorControlId = control
        };

        Assert.That(ParameterFieldFactory.ResolveKind(parameter), Is.EqualTo(expected));
    }

    [Test]
    public void NumberFieldCarriesRangeUnitRequiredAndQuickMetadata()
    {
        var parameter = new ActionCatalogParameter
        {
            Name = "duration",
            DisplayNameKo = "시간",
            Type = "duration",
            EditorControlId = "number",
            Required = true,
            QuickEdit = true,
            HasMinimum = true,
            Minimum = 0,
            HasMaximum = true,
            Maximum = 5,
            UnitKo = "초"
        };

        VisualElement field = ParameterFieldFactory.Create(
            parameter,
            new JValue(1.5),
            new ParameterFieldContext(),
            _ => { });

        Assert.That(field.ClassListContains("is-required"), Is.True);
        Assert.That(field.ClassListContains("is-quick"), Is.True);
        Assert.That(field.Q<Slider>(), Is.Not.Null);
        Assert.That(field.Q<Label>("parameter-unit").text, Is.EqualTo("초"));
    }

    [Test]
    public void BindingSourceControlReadsAndCreatesStructuredBindTokens()
    {
        var parameter = new ActionCatalogParameter
        {
            Name = "actor",
            DisplayNameKo = "캐릭터",
            Type = "actorRef",
            EditorControlId = "actor",
            ValueSources = { "literal", "input", "event" }
        };
        JToken changed = null;
        var context = new ParameterFieldContext();
        context.AddBindingOptions("input", new[] { "input.actor", "input.target" });

        VisualElement field = ParameterFieldFactory.Create(
            parameter,
            JObject.Parse("{\"$bind\":\"input.actor\"}"),
            context,
            value => changed = value);
        var source = field.Q<DropdownField>("value-source-dropdown");
        var path = field.Q<DropdownField>("binding-path-dropdown");

        Assert.That(source, Is.Not.Null);
        Assert.That(source.value, Is.EqualTo("시퀀스 입력"));
        Assert.That(path.value, Is.EqualTo("input.actor"));
        field.Q<ValueSourceField>().SetBindingPath("input.target");
        Assert.That(changed["$bind"].Value<string>(), Is.EqualTo("input.target"));
    }

    [Test]
    public void ReferenceControlOffersKnownValuesWithoutBlockingCustomIds()
    {
        var parameter = new ActionCatalogParameter
        {
            Name = "actor",
            Type = "actorRef",
            EditorControlId = "actor"
        };
        var context = new ParameterFieldContext();
        context.AddReferenceOptions("actor", new[] { "player", "zev" });

        VisualElement field = ParameterFieldFactory.Create(
            parameter,
            new JValue("player"),
            context,
            _ => { });

        ReferencePickerField reference = field.Q<ReferencePickerField>();
        Assert.That(reference, Is.Not.Null);
        Assert.That(reference.Options, Is.EqualTo(new[] { "player", "zev" }));
        Assert.That(reference.Value, Is.EqualTo("player"));
        Assert.That(reference.Q<TextField>(), Is.Not.Null);
    }

    [Test]
    public void EnumBoolColorAndVectorsCreateTypedUiToolkitControls()
    {
        Assert.That(Create("enum", "segmented", new JValue("all")).Q<DropdownField>(), Is.Not.Null);
        Assert.That(Create("bool", "toggle", new JValue(true)).Q<Toggle>(), Is.Not.Null);
        Assert.That(Create("color", "color", new JValue("#FFFFFFFF")).Q<ColorField>(), Is.Not.Null);
        Assert.That(Create("vector2", "vector2", new JArray(1, 2)).Q<Vector2Field>(), Is.Not.Null);
        Assert.That(Create("vector3", "vector3", new JArray(1, 2, 3)).Q<Vector3Field>(), Is.Not.Null);
    }

    private static VisualElement Create(string type, string control, JToken token)
    {
        var parameter = new ActionCatalogParameter
        {
            Name = "value",
            Type = type,
            EditorControlId = control,
            Options = { "all", "any", "race" }
        };
        return ParameterFieldFactory.Create(
            parameter,
            token,
            new ParameterFieldContext(),
            _ => { });
    }
}
