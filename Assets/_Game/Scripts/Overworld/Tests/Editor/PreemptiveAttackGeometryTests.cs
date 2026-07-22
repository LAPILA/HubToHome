using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class PreemptiveAttackGeometryTests
{
    private const string PlayerPrefabPath = "Assets/_Game/Content/Characters/Prefabs/Player/Player_Base.prefab";

    [TestCase(0f, -1f, 10f, 19f, 1f, 2f)]
    [TestCase(0f, 1f, 10f, 21f, 1f, 2f)]
    [TestCase(-1f, 0f, 9f, 20f, 2f, 1f)]
    [TestCase(1f, 0f, 11f, 20f, 2f, 1f)]
    public void Create_BuildsForwardAreaForCardinalDirection(
        float facingX,
        float facingY,
        float expectedCenterX,
        float expectedCenterY,
        float expectedSizeX,
        float expectedSizeY)
    {
        PreemptiveAttackArea area = PreemptiveAttackGeometry.Create(
            new Vector2(10f, 20f),
            new Vector2(facingX, facingY),
            2f,
            1f);

        Assert.That(area.Center, Is.EqualTo(new Vector2(expectedCenterX, expectedCenterY)));
        Assert.That(area.Size, Is.EqualTo(new Vector2(expectedSizeX, expectedSizeY)));
        Assert.That(area.Facing, Is.EqualTo(new Vector2(facingX, facingY)));
    }

    [Test]
    public void Create_ClampsNegativeDimensionsAndDefaultsToDown()
    {
        PreemptiveAttackArea area = PreemptiveAttackGeometry.Create(
            Vector2.zero,
            Vector2.zero,
            -2f,
            -1f);

        Assert.That(area.Center, Is.EqualTo(Vector2.zero));
        Assert.That(area.Size, Is.EqualTo(Vector2.zero));
        Assert.That(area.Facing, Is.EqualTo(Vector2.down));
    }

    [Test]
    public void PlayerPrefabHasPositiveDirectionalAttackWidth()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        Assert.That(prefab, Is.Not.Null);

        PlayerController controller = prefab.GetComponent<PlayerController>();
        Assert.That(controller, Is.Not.Null);

        var serialized = new SerializedObject(controller);
        SerializedProperty attackWidth = serialized.FindProperty("_attackWidth");
        Assert.That(attackWidth, Is.Not.Null);
        Assert.That(attackWidth.floatValue, Is.GreaterThan(0f));
    }
}
