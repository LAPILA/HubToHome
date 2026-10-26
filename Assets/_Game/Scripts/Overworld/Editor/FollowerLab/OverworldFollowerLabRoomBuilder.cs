using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>Small static obstacle courses; runtime only contains the existing map and follower components.</summary>
internal static class OverworldFollowerLabRoomBuilder
{
    private const string GrassTile = "Assets/_Game/Content/Art/Environment/WindMill/grass_4.asset";
    private const string ChestTexture = "Assets/_Game/Content/Art/Common/Chest.png";
    private const string WhiteSprite = "Assets/_Game/Content/Maps/Shared/Generated/RoomMap_WhiteSquare.png";

    internal static void Preflight()
    {
        OverworldFollowerLabBuilder.Require<TileBase>(GrassTile);
        OverworldFollowerLabBuilder.Require<Sprite>(WhiteSprite);
        FindChest();
        if (LayerMask.NameToLayer("Interactable") < 0 || LayerMask.NameToLayer("Player") < 0)
            throw new InvalidOperationException("Player/Interactable 레이어가 필요합니다.");
    }

    internal static GeneratedRoomContext Build(int index, string name, RoomDefinition[] rooms,
        DialogueData sign, DialogueData upperDialogue, DialogueData lowerDialogue)
    {
        Color accent = index == 0 ? new Color(.8f, .64f, .37f)
            : index == 1 ? new Color(.4f, .75f, .73f) : new Color(.64f, .55f, .85f);
        GeneratedRoomContext room = GeneratedRoomEditorUtility.CreateRoom("Room_FollowerLab_" + name,
            rooms[index].RoomId, new Vector2(26, 18), new Vector2(26, 18),
            new Color(.045f, .055f, .08f), new Color(.12f, .17f, .2f), accent * .55f);
        Ground(room, index);
        GeneratedRoomEditorUtility.CreateSpawn(room, "default", new Vector2(-9.5f, 0), FacingDirection.Right);
        GeneratedRoomEditorUtility.CreateSpawn(room, "from_left", new Vector2(-9.5f, 0), FacingDirection.Right);
        GeneratedRoomEditorUtility.CreateSpawn(room, "from_right", new Vector2(9.5f, 0), FacingDirection.Left);
        Portal(room, "left", new Vector2(-11.6f, 0), rooms[(index + 2) % 3], "from_right", FacingDirection.Left, accent);
        Portal(room, "right", new Vector2(11.6f, 0), rooms[(index + 1) % 3], "from_left", FacingDirection.Right, accent);
        NPCMarker marker = GeneratedRoomEditorUtility.CreateMarker<NPCMarker>(room, rooms[index].RoomId + ".guide",
            "실험 안내판", new Vector2(-10, -2), AreaMarkerType.NPC);
        OverworldFollowerLabBuilder.Set(marker, "dialogueData", p => p.objectReferenceValue = sign);
        OverworldFollowerLabBuilder.Set(marker, "npcId", p => p.stringValue = rooms[index].RoomId + ".guide");
        Block(room, "Guide Stand", new Vector2(-10, -2), new Vector2(.65f, .75f), accent, false);
        if (index == 0) Warehouse(room, accent);
        else if (index == 1) Exhibition(room, accent, upperDialogue, lowerDialogue);
        else Detour(room, accent);
        return room;
    }

    private static void Ground(GeneratedRoomContext room, int index)
    {
        Grid grid = new GameObject("Grid").AddComponent<Grid>();
        grid.transform.SetParent(room.Geometry, false);
        Tilemap floor = new GameObject("Floor — No Collision").AddComponent<Tilemap>();
        floor.transform.SetParent(grid.transform, false);
        TilemapRenderer renderer = floor.gameObject.AddComponent<TilemapRenderer>();
        renderer.sortingLayerName = "Background";
        renderer.sortingOrder = -1;
        floor.color = index == 0 ? new Color(.47f, .48f, .4f) : index == 1
            ? new Color(.38f, .53f, .5f) : new Color(.45f, .43f, .56f);
        TileBase tile = OverworldFollowerLabBuilder.Require<TileBase>(GrassTile);
        var bounds = new BoundsInt(-13, -9, 0, 26, 18, 1);
        var tiles = new TileBase[26 * 18];
        for (int i = 0; i < tiles.Length; i++) tiles[i] = tile;
        floor.SetTilesBlock(bounds, tiles);
        floor.CompressBounds();
    }

    private static void Warehouse(GeneratedRoomContext room, Color color)
    {
        // Alternating barriers force wide turns; every corridor is wider than the player's body.
        Block(room, "Shelf A — pass above", new Vector2(-6, -2.2f), new Vector2(1.7f, 10), color);
        Block(room, "Shelf B — pass below", new Vector2(0, 2.2f), new Vector2(1.7f, 10), color);
        Block(room, "Shelf C — pass above", new Vector2(6, -2.2f), new Vector2(1.7f, 10), color);
        for (int x = -10; x <= 10; x += 4)
        {
            Chest(room, new Vector2(x, 7.5f), color);
            Chest(room, new Vector2(x + .4f, -7.5f), color);
        }
    }

    private static void Exhibition(GeneratedRoomContext room, Color color, DialogueData upper, DialogueData lower)
    {
        for (int i = 0; i < 3; i++)
        {
            Block(room, "Display North " + i, new Vector2(-7 + 6 * i, 6), new Vector2(3, 1.2f), color);
            Block(room, "Display South " + i, new Vector2(-7 + 6 * i, -6), new Vector2(3, 1.2f), color);
            Chest(room, new Vector2(-7 + 6 * i, 6.5f), Color.white);
        }
        Block(room, "Blue Exhibit", new Vector2(-1, 3.5f), new Vector2(2, 1.2f), new Color(.45f, .7f, 1f));
        Chest(room, new Vector2(3, -3.5f), new Color(1f, .55f, .72f));
        Observation(room, 4, new[] { new Vector2(-5, 1.4f), new Vector2(-1, 1.4f) },
            new[] { new Vector2(-5, 1.4f), new Vector2(-5, 0) }, upper);
        Observation(room, 5, new[] { new Vector2(-5, -2), new Vector2(3, -2) },
            new[] { new Vector2(-5, -2), new Vector2(-5, 0) }, lower);
    }

    private static void Detour(GeneratedRoomContext room, Color color)
    {
        Block(room, "Central Island", Vector2.zero, new Vector2(5, 5), color);
        Block(room, "Upper Dead End", new Vector2(-6, 5), new Vector2(1.3f, 4), color);
        Block(room, "Lower Dead End", new Vector2(6, -5), new Vector2(1.3f, 4), color);
        Block(room, "North Bend", new Vector2(3, 6.6f), new Vector2(6, 1.2f), color);
        Block(room, "South Bend", new Vector2(-3, -6.6f), new Vector2(6, 1.2f), color);
        foreach (Vector2 position in new[] { new Vector2(-9, 5), new Vector2(9, 5), new Vector2(-9, -5),
            new Vector2(9, -5), new Vector2(3, 4), new Vector2(-3, -4) }) Chest(room, position, Color.white);
    }

    private static void Observation(GeneratedRoomContext room, int slot, Vector2[] outward, Vector2[] inward, DialogueData dialogue)
    {
        Transform root = GeneratedRoomEditorUtility.CreateEmpty("Observe Slot " + slot, room.Systems);
        // Include both arrival spawns so observation starts when exploration resumes, even while stationary.
        // This only arms departure; the runtime still validates the authored route against obstacles.
        BoxCollider2D trigger = GeneratedRoomEditorUtility.CreateEmpty("Departure Zone", root).gameObject.AddComponent<BoxCollider2D>();
        trigger.isTrigger = true;
        trigger.size = new Vector2(22, 12);
        var observation = root.gameObject.AddComponent<OverworldFollowerObservation>();
        observation.Configure(slot, trigger, Waypoints(root, "Out", outward), Waypoints(root, "Return", inward),
            dialogue, 1 << LayerMask.NameToLayer("Default"), 1.5f, true);
    }

    private static Transform[] Waypoints(Transform parent, string name, Vector2[] positions)
    {
        var points = new Transform[positions.Length];
        for (int i = 0; i < points.Length; i++)
            points[i] = GeneratedRoomEditorUtility.CreateEmpty(name + " " + (i + 1), parent, positions[i]);
        return points;
    }

    private static void Portal(GeneratedRoomContext room, string side, Vector2 position, RoomDefinition target,
        string spawn, FacingDirection facing, Color color)
    {
        GeneratedRoomEditorUtility.CreateConnection(room, room.RoomId + "." + side, position, target, spawn, facing);
        Block(room, "Door " + side, position, new Vector2(.65f, 2.4f), color, false);
    }

    private static void Block(GeneratedRoomContext room, string name, Vector2 position, Vector2 size, Color color, bool solid = true)
    {
        GeneratedRoomEditorUtility.CreateBlock(name + " Shadow", room.Props, position + Vector2.down * .15f,
            size + Vector2.one * .15f, new Color(0, 0, 0, .45f), 0);
        GameObject item = GeneratedRoomEditorUtility.CreateBlock(name, room.Props, position, size, color, 1);
        if (solid) item.AddComponent<BoxCollider2D>().size = Vector2.one;
        // A contrasting cap makes obstacle boundaries readable without per-object lights.
        GeneratedRoomEditorUtility.CreateBlock(name + " Edge", room.Props, position + Vector2.up * (size.y / 2 - .08f),
            new Vector2(size.x, .16f), color * 1.2f, 2);
    }

    private static void Chest(GeneratedRoomContext room, Vector2 position, Color tint)
    {
        Transform root = GeneratedRoomEditorUtility.CreateEmpty("Crate — obstacle only", room.Props, position);
        var renderer = root.gameObject.AddComponent<SpriteRenderer>();
        renderer.sprite = FindChest();
        renderer.color = tint;
        renderer.sortingLayerName = "Characters";
        renderer.sortingOrder = 3;
        renderer.spriteSortPoint = SpriteSortPoint.Pivot;
        BoxCollider2D collider = root.gameObject.AddComponent<BoxCollider2D>();
        collider.size = new Vector2(.8f, .55f);
        collider.offset = new Vector2(0, .25f);
    }

    private static Sprite FindChest()
    {
        foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(ChestTexture))
            if (asset is Sprite sprite && sprite.name == "Chest_0") return sprite;
        throw new InvalidOperationException("Chest_0 Sprite가 없습니다: " + ChestTexture);
    }
}
