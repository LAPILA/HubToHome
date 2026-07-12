using NUnit.Framework;
using UnityEngine;

public class CharacterPivotTests
{
    private GameObject _root;

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_root);
    }

    [Test]
    public void TryGetPivotResolvesNamedPivotUnderPivotRoot()
    {
        CharacterBase character = CreateCharacterWithFrontPivot(out Transform front);

        bool found = character.TryGetPivot(CharacterPivotId.Front, out Transform result);

        Assert.That(found, Is.True);
        Assert.That(result, Is.SameAs(front));
    }

    [Test]
    public void GetPivotFallsBackToCharacterTransform()
    {
        CharacterBase character = CreateCharacterWithFrontPivot(out _);

        Transform result = character.GetPivot("Missing");

        Assert.That(result, Is.SameAs(character.transform));
    }

    private CharacterBase CreateCharacterWithFrontPivot(out Transform front)
    {
        _root = new GameObject("Character");
        CharacterBase character = _root.AddComponent<PlayerCharacter>();
        var pivots = new GameObject(CharacterPivotId.Root);
        pivots.transform.SetParent(_root.transform, false);
        var frontObject = new GameObject(CharacterPivotId.Front);
        frontObject.transform.SetParent(pivots.transform, false);
        front = frontObject.transform;
        return character;
    }
}

