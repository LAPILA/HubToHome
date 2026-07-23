#if UNITY_EDITOR
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class AreaMarkerWorkbenchWindowTests
{
    private GameObject _markerObject;

    [TearDown]
    public void TearDown()
    {
        Selection.activeObject = null;
        if (_markerObject != null)
            Object.DestroyImmediate(_markerObject);
    }

    [Test]
    public void TrySelectAndFrame_SelectsMarkerGameObject()
    {
        SignMarker marker = CreateMarker("marker.sign");
        var issue = new RoomMapValidationIssue(
            RoomMapValidationCodes.MarkerConfiguration,
            RoomMapValidationSeverity.Error,
            "test",
            marker,
            null,
            marker);

        bool selected = AreaMarkerWorkbenchWindow.TrySelectAndFrame(issue);

        Assert.That(selected, Is.True);
        Assert.That(Selection.activeGameObject, Is.SameAs(marker.gameObject));
    }

    [Test]
    public void MatchesMarker_FiltersBySearchTypeAndProblemState()
    {
        SignMarker marker = CreateMarker("marker.sign");
        RoomMapValidationReport report = RoomMapValidationScanner.Scan(
            new RoomMapValidationInput
            {
                ScopeName = "Tests",
                Markers = new AreaMarkerBase[] { marker }
            });
        RoomMapMarkerEntry entry = report.Markers.Single();

        Assert.That(
            AreaMarkerWorkbenchWindow.MatchesMarker(
                entry,
                report,
                "marker.sign",
                AreaMarkerWorkbenchWindow.AllRoomsKey,
                (int)AreaMarkerType.Sign,
                AreaMarkerIssueFilter.Problems),
            Is.True);
        Assert.That(
            AreaMarkerWorkbenchWindow.MatchesMarker(
                entry,
                report,
                "enemy",
                AreaMarkerWorkbenchWindow.AllRoomsKey,
                -1,
                AreaMarkerIssueFilter.All),
            Is.False);
        Assert.That(
            AreaMarkerWorkbenchWindow.MatchesMarker(
                entry,
                report,
                string.Empty,
                AreaMarkerWorkbenchWindow.AllRoomsKey,
                (int)AreaMarkerType.Enemy,
                AreaMarkerIssueFilter.All),
            Is.False);
    }

    [Test]
    public void TrySelectAndFrame_WithoutContext_ReturnsFalse()
    {
        var issue = new RoomMapValidationIssue(
            RoomMapValidationCodes.MapTransitionServiceMissing,
            RoomMapValidationSeverity.Error,
            "test");

        Assert.That(AreaMarkerWorkbenchWindow.TrySelectAndFrame(issue), Is.False);
    }

    private SignMarker CreateMarker(string markerId)
    {
        _markerObject = new GameObject("Sign Marker");
        SignMarker marker = _markerObject.AddComponent<SignMarker>();
        SerializedObject serializedObject = new SerializedObject(marker);
        serializedObject.FindProperty("markerId").stringValue = markerId;
        serializedObject.FindProperty("areaId").stringValue = "test.area";
        serializedObject.FindProperty("signText").stringValue = "test";
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        return marker;
    }
}
#endif
