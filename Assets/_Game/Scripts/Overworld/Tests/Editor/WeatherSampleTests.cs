using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class WeatherSampleTests
{
    private const string PrefabPath = "Assets/_Game/Content/Maps/Shared/Weather/WeatherSample.prefab";
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [TestCase(-12f, -7f, -1.8f, -14f, .55f)]
    [TestCase(12f, 7f, -5f, -20f, .05f)]
    [TestCase(0f, 0f, 0f, -1f, 2f)]
    public void FixedVelocityEndsAtChosenLandingPoint(float x, float y, float vx, float vy, float lifetime)
    {
        var landing = new Vector2(x, y);
        var velocity = new Vector2(vx, vy);
        Vector2 spawn = WeatherSampleMath.CalculateSpawn(landing, velocity, lifetime);
        Assert.That((spawn + velocity * lifetime - landing).sqrMagnitude, Is.LessThan(.000001f));
    }

    [TestCase(0f, 0f)]
    [TestCase(.8f, 0f)]
    [TestCase(1.075f, .5f)]
    [TestCase(1.35f, 1f)]
    [TestCase(5f, 1f)]
    public void FogIsClearAtPlayerAndOpaqueOutsideFeather(float distance, float expected)
    {
        Assert.That(WeatherSampleMath.FogVisibility(distance, .8f, .55f), Is.EqualTo(expected).Within(.00001f));
    }

    [Test]
    public void AreaContainsEdgesButNotOutsideAndFogEnvelopeIsMonotone()
    {
        Assert.That(WeatherSampleMath.Contains(new Vector2(12f, 7f), Vector2.zero, new Vector2(24f, 14f)), Is.True);
        Assert.That(WeatherSampleMath.Contains(new Vector2(12.01f, 7f), Vector2.zero, new Vector2(24f, 14f)), Is.False);
        float previous = 0f;
        for (int i = 0; i <= 200; i++)
        {
            float current = WeatherSampleMath.FogVisibility(i * .01f, .8f, .55f);
            Assert.That(current, Is.InRange(previous, 1f));
            previous = current;
        }
    }

    [Test]
    public void EmissionPreservesFractionsAndBoundsStallWork()
    {
        float remainder = 0f;
        int total = 0;
        for (int i = 0; i < 60; i++) total += WeatherSampleMath.AccumulateEmission(ref remainder, 90f, 1f / 60f, 64);
        Assert.That(total, Is.EqualTo(90));
        Assert.That(WeatherSampleMath.AccumulateEmission(ref remainder, 500f, 20f, 64), Is.EqualTo(50));
        Assert.That(remainder, Is.InRange(0f, .999f));
    }

    [Test]
    public void PrefabHasDirectMaterialsAndReplaceableArtSlots()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Assert.That(prefab, Is.Not.Null);
        var serialized = new SerializedObject(prefab.GetComponent<WeatherSample>());
        Assert.That(serialized.FindProperty("_fogMaterial").objectReferenceValue, Is.Not.Null);
        Assert.That(serialized.FindProperty("_particleMaterial").objectReferenceValue, Is.Not.Null);
        Assert.That(serialized.FindProperty("_fogSprite"), Is.Not.Null);
        Assert.That(serialized.FindProperty("_rainSprite"), Is.Not.Null);
        Assert.That(serialized.FindProperty("_splashSprite"), Is.Not.Null);
        Assert.That(serialized.FindProperty("_fogEnabled").boolValue, Is.True);
        Assert.That(serialized.FindProperty("_rainEnabled").boolValue, Is.True);
    }

    [Test]
    public void GeneratedVisualsAreReusedAndDeathSplashHasNoPhysicsOrNoise()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Assert.That(prefab, Is.Not.Null);
        GameObject instance = Object.Instantiate(prefab);
        GameObject actor = new GameObject("Weather Target");
        try
        {
            WeatherSample weather = instance.GetComponent<WeatherSample>();
            Invoke(weather, "EnsureVisuals");
            Invoke(weather, "ApplyConfiguration");
            Transform visuals = instance.transform.Find("Weather Visuals (Runtime)");
            Invoke(weather, "EnsureVisuals");
            Assert.That(instance.transform.Find("Weather Visuals (Runtime)"), Is.SameAs(visuals));
            ParticleSystem rain = visuals.Find("Rain").GetComponent<ParticleSystem>();
            Assert.That(rain.collision.enabled, Is.False);
            Assert.That(rain.noise.enabled, Is.False);
            Assert.That(rain.main.gravityModifier.constant, Is.EqualTo(0f));
            Assert.That(rain.emission.enabled, Is.False);
            Assert.That(rain.main.simulationSpace, Is.EqualTo(ParticleSystemSimulationSpace.World));
            Assert.That(rain.subEmitters.subEmittersCount, Is.EqualTo(1));
            Assert.That(rain.subEmitters.GetSubEmitterType(0), Is.EqualTo(ParticleSystemSubEmitterType.Death));
            Assert.That(rain.subEmitters.GetSubEmitterProperties(0), Is.EqualTo(ParticleSystemSubEmitterProperties.InheritNothing));
            Assert.That(rain.subEmitters.GetSubEmitterSystem(0).transform.parent, Is.EqualTo(rain.transform));
            weather.SetTarget(actor.transform);
            Assert.That(weather.Target, Is.SameAs(actor.transform));
            SpriteRenderer fog = visuals.Find("Fog").GetComponent<SpriteRenderer>();
            var properties = new MaterialPropertyBlock();
            fog.GetPropertyBlock(properties);
            Assert.That(properties.GetFloat("_WeatherHoleEnabled"), Is.EqualTo(1f));
            actor.transform.position = new Vector3(1000f, 1000f, 0f);
            Invoke(weather, "LateUpdate");
            fog.GetPropertyBlock(properties);
            Assert.That(properties.GetFloat("_WeatherHoleEnabled"), Is.EqualTo(0f));
            actor.SetActive(false);
            Invoke(weather, "LateUpdate");
            fog.GetPropertyBlock(properties);
            Assert.That(properties.GetFloat("_WeatherHoleEnabled"), Is.EqualTo(0f));
            Invoke(weather, "OnDisable");
            Assert.That(weather.Target, Is.Null);
            Assert.That(visuals.gameObject.activeSelf, Is.False);
            Assert.That(rain.IsAlive(true), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(actor);
            Object.DestroyImmediate(instance);
        }
    }

    [TestCase("Assets/_Game/Presentation/Weather/WeatherFog.shader")]
    [TestCase("Assets/_Game/Presentation/Weather/WeatherParticle.shader")]
    public void ImportedWeatherShaderHasNoCompilerErrors(string path)
    {
        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
        Assert.That(shader, Is.Not.Null);
        Assert.That(ShaderUtil.ShaderHasError(shader), Is.False, path);
    }

    private static void Invoke(WeatherSample weather, string method)
    {
        MethodInfo info = typeof(WeatherSample).GetMethod(method, PrivateInstance);
        Assert.That(info, Is.Not.Null);
        info.Invoke(weather, null);
    }
}
