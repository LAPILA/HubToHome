#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class AreaMarkerCreateMenu
{
    [MenuItem("GameObject/HubToHome/Area Marker/Connection", false, 10)]
    private static void CreateConnection() => CreateMarker<AreaConnectionMarker>("Marker_Connection", true);

    [MenuItem("GameObject/HubToHome/Area Marker/Enemy", false, 11)]
    private static void CreateEnemy() => CreateMarker<OverworldEnemyMarker>("Marker_Enemy", true);

    [MenuItem("GameObject/HubToHome/Area Marker/Hazard", false, 12)]
    private static void CreateHazard() => CreateMarker<HazardMarker>("Marker_Hazard", true);

    [MenuItem("GameObject/HubToHome/Area Marker/Puzzle", false, 13)]
    private static void CreatePuzzle() => CreateMarker<PuzzleMarker>("Marker_Puzzle", true);

    [MenuItem("GameObject/HubToHome/Area Marker/Vendor", false, 14)]
    private static void CreateVendor() => CreateMarker<VendorMarker>("Marker_Vendor", true);

    [MenuItem("GameObject/HubToHome/Area Marker/Shortcut Door", false, 15)]
    private static void CreateShortcutDoor() => CreateMarker<ShortcutDoorMarker>("Marker_ShortcutDoor", true);

    [MenuItem("GameObject/HubToHome/Area Marker/NPC", false, 16)]
    private static void CreateNpc() => CreateMarker<NPCMarker>("Marker_NPC", true);

    [MenuItem("GameObject/HubToHome/Area Marker/Item", false, 17)]
    private static void CreateItem() => CreateMarker<ItemPickupMarker>("Marker_Item", true);

    [MenuItem("GameObject/HubToHome/Area Marker/Sign", false, 18)]
    private static void CreateSign() => CreateMarker<SignMarker>("Marker_Sign", true);

    [MenuItem("GameObject/HubToHome/Area Marker/SAVE Point", false, 19)]
    private static void CreateSavePoint() => CreateMarker<SavePointMarker>("Marker_SavePoint", true);

    [MenuItem("GameObject/HubToHome/Area Marker/Plot Point", false, 20)]
    private static void CreatePlotPoint() => CreateMarker<PlotPointMarker>("Marker_PlotPoint", true);

    [MenuItem("GameObject/HubToHome/Area Marker/Sublocation", false, 21)]
    private static void CreateSublocation() => CreateMarker<SublocationMarker>("Marker_Sublocation", true);

    private static void CreateMarker<T>(string objectName, bool addTriggerCollider) where T : AreaMarkerBase
    {
        var markerObject = new GameObject(objectName);
        GameObjectUtility.SetParentAndAlign(markerObject, Selection.activeGameObject);
        Undo.RegisterCreatedObjectUndo(markerObject, "Create Area Marker");

        T marker = markerObject.AddComponent<T>();
        if (addTriggerCollider && markerObject.GetComponent<Collider2D>() == null)
        {
            CircleCollider2D collider = markerObject.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            collider.radius = 0.5f;
        }

        Selection.activeGameObject = marker.gameObject;
        EditorGUIUtility.PingObject(marker.gameObject);
    }
}
#endif