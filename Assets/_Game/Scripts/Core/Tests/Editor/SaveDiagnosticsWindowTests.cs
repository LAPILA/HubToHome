using NUnit.Framework;

public class SaveDiagnosticsWindowTests
{
    [Test]
    public void GetInspectableSlotIndices_ListsManualSlotsBeforeAutoSlot()
    {
        int[] slots = SaveDiagnosticsWindow.GetInspectableSlotIndices();

        Assert.That(slots, Is.EqualTo(new[] { 0, 1, 2, 99 }));
        Assert.That(slots, Is.Not.SameAs(
            SaveDiagnosticsWindow.GetInspectableSlotIndices()));
    }

    [TestCase(0, "수동 슬롯 1")]
    [TestCase(2, "수동 슬롯 3")]
    [TestCase(99, "자동 슬롯")]
    public void GetSlotDisplayName_UsesDesignerFacingLabels(
        int slotIndex,
        string expected)
    {
        Assert.That(
            SaveDiagnosticsWindow.GetSlotDisplayName(slotIndex),
            Is.EqualTo(expected));
    }
}
