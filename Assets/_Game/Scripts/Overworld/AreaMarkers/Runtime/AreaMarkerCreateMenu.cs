#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class AreaMarkerCreateMenu
{
    [MenuItem("GameObject/Hub To Home/마커/이동 연결", false, 10)]
    private static void CreateConnection() => CreateMarker<AreaConnectionMarker>("Marker_Connection", true);

    [MenuItem("GameObject/Hub To Home/마커/적", false, 11)]
    private static void CreateEnemy() => CreateMarker<OverworldEnemyMarker>("Marker_Enemy", true);

    [MenuItem("GameObject/Hub To Home/마커/위험 지점", false, 12)]
    private static void CreateHazard() => CreateMarker<HazardMarker>("Marker_Hazard", true);

    [MenuItem("GameObject/Hub To Home/마커/퍼즐", false, 13)]
    private static void CreatePuzzle() => CreateMarker<PuzzleMarker>("Marker_Puzzle", true);

    [MenuItem("GameObject/Hub To Home/마커/상점", false, 14)]
    private static void CreateVendor() => CreateMarker<VendorMarker>("Marker_Vendor", true);

    [MenuItem("GameObject/Hub To Home/마커/지름길 문", false, 15)]
    private static void CreateShortcutDoor() => CreateMarker<ShortcutDoorMarker>("Marker_ShortcutDoor", true);

    [MenuItem("GameObject/Hub To Home/마커/대화 NPC", false, 16)]
    private static void CreateNpc() => CreateMarker<NPCMarker>("Marker_NPC", true);

    [MenuItem("GameObject/Hub To Home/마커/아이템", false, 17)]
    private static void CreateItem() => CreateMarker<ItemPickupMarker>("Marker_Item", true);

    [MenuItem("GameObject/Hub To Home/마커/표지판", false, 18)]
    private static void CreateSign() => CreateMarker<SignMarker>("Marker_Sign", true);

    [MenuItem("GameObject/Hub To Home/마커/저장 지점", false, 19)]
    private static void CreateSavePoint() => CreateMarker<SavePointMarker>("Marker_SavePoint", true);

    [MenuItem("GameObject/Hub To Home/마커/이벤트 지점", false, 20)]
    private static void CreatePlotPoint() => CreateMarker<PlotPointMarker>("Marker_PlotPoint", true);

    [MenuItem("GameObject/Hub To Home/마커/하위 구역", false, 21)]
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
