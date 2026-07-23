using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

public class SaveManagerCompatibilityTests
{
    [Test]
    public void LegacyPublicApiSignaturesRemainAvailable()
    {
        Type type = typeof(SaveManager);

        AssertMethod(type, "Save", typeof(void), typeof(SaveData), typeof(int));
        AssertMethod(type, "Load", typeof(SaveData), typeof(int));
        AssertMethod(type, "Delete", typeof(void), typeof(int));
        AssertMethod(type, "Exists", typeof(bool), typeof(int));
        AssertMethod(type, "HasAnySave", typeof(bool));
    }

    [Test]
    public void DetailedApiRejectsInvalidArgumentsWithoutThrowing()
    {
        SaveStorageResult nullData = null;
        SaveStorageResult negativeSlot = null;
        SaveLoadResult load = null;
        SaveSlotInspection inspection = null;

        Assert.DoesNotThrow(() => nullData = SaveManager.TrySave(null, 0));
        Assert.DoesNotThrow(
            () => negativeSlot = SaveManager.TrySave(new SaveData(), -1));
        Assert.DoesNotThrow(() => load = SaveManager.TryLoad(-1));
        Assert.DoesNotThrow(() => inspection = SaveManager.InspectSlot(-1));

        Assert.That(nullData.Success, Is.False);
        Assert.That(
            nullData.Failure,
            Is.EqualTo(SaveStorageFailure.InvalidArgument));
        Assert.That(negativeSlot.Success, Is.False);
        Assert.That(
            negativeSlot.Failure,
            Is.EqualTo(SaveStorageFailure.InvalidArgument));
        Assert.That(load.Success, Is.False);
        Assert.That(load.Failure, Is.EqualTo(SaveLoadFailure.InvalidSlot));
        Assert.That(inspection.IsLoadable, Is.False);
    }

    [Test]
    public void LegacyApiInvalidArgumentsRemainExceptionSafe()
    {
        Assert.DoesNotThrow(() => SaveManager.Save(null, -1));
        Assert.DoesNotThrow(() => SaveManager.Delete(-1));
        Assert.DoesNotThrow(() => SaveManager.Exists(-1));
        Assert.That(SaveManager.Load(-1), Is.Null);
        Assert.That(SaveManager.Exists(-1), Is.False);
    }

    private static void AssertMethod(
        Type type,
        string name,
        Type returnType,
        params Type[] parameterTypes)
    {
        MethodInfo method = type.GetMethod(
            name,
            BindingFlags.Public | BindingFlags.Static,
            null,
            parameterTypes,
            null);

        Assert.That(
            method,
            Is.Not.Null,
            name + "(" + string.Join(", ", parameterTypes.Select(x => x.Name))
            + ") public API가 필요합니다.");
        Assert.That(method.ReturnType, Is.EqualTo(returnType));
    }
}
