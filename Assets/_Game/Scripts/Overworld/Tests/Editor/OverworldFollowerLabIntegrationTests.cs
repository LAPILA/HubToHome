using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>Native Unity fixture checks. No save slot is written; the production guard stays active.</summary>
public sealed class OverworldFollowerLabIntegrationTests
{
    private const string Root = "Assets/_Game/Content/Maps/Development/Regions/FollowerLab";

    [Test]
    public void AuthoredAssets_HaveSixDistinctCharactersAndBidirectionalRoomLinks()
    {
        var definition = AssetDatabase.LoadAssetAtPath<OverworldFollowerLabDefinition>(Root + "/Data/FollowerLabDefinition.asset");
        Assert.That(definition, Is.Not.Null);
        Assert.That(definition.TryValidate(out string error), Is.True, error);
        Assert.That(definition.Companions.Count, Is.EqualTo(5));
        var ids = new HashSet<string> { definition.Leader.CharacterID };
        foreach (CharacterData member in definition.Companions)
        {
            Assert.That(ids.Add(member.CharacterID), Is.True);
            Assert.That(CharacterDatabase.FindById(member.CharacterID), Is.SameAs(member));
            Assert.That(member.OverworldVisualPrefab, Is.Not.Null);
        }
        foreach (string roomName in new[] { "Warehouse", "Exhibition", "Detour" })
        {
            RoomDefinition room = Room(roomName);
            Assert.That(room.IsValid, Is.True);
            Assert.That(room.AreaDefinition.RoomDefinition, Is.SameAs(room));
            AreaConnectionMarker[] doors = room.RoomPrefab.GetComponentsInChildren<AreaConnectionMarker>(true);
            Assert.That(doors.Length, Is.EqualTo(2));
            foreach (AreaConnectionMarker door in doors)
            {
                MapTransitionRequest destination = door.MapTransition;
                Assert.That(destination.IsValid(out error), Is.True, error);
                bool found = false;
                foreach (SpawnPoint spawn in destination.TargetRoom.RoomPrefab.GetComponentsInChildren<SpawnPoint>(true))
                    found |= spawn.SpawnPointId == destination.TargetSpawnPointId;
                Assert.That(found, Is.True, destination.TargetSpawnPointId);
            }
        }
        foreach (OverworldFollowerObservation observation in Room("Exhibition").RoomPrefab.GetComponentsInChildren<OverworldFollowerObservation>(true))
            Assert.That(observation.TryValidateConfiguration(out error), Is.True, error);
    }

    [TestCase(-9.5f, 0f)]
    [TestCase(9.5f, 0f)]
    [TestCase(-9.5f, 2f)]
    [TestCase(9.5f, -2f)]
    public void AuthoredObservationAreas_IncludeBothEntrancesAndOffsetArrivals(float x, float y)
    {
        OverworldFollowerObservation[] observations = Room("Exhibition").RoomPrefab
            .GetComponentsInChildren<OverworldFollowerObservation>(true);
        Assert.That(observations.Length, Is.EqualTo(2));
        foreach (OverworldFollowerObservation observation in observations)
        {
            var serialized = new SerializedObject(observation);
            var area = (BoxCollider2D)serialized.FindProperty("_departureArea").objectReferenceValue;
            Assert.That(area, Is.Not.Null);
            Vector3 point = area.transform.InverseTransformPoint(new Vector3(x, y));
            Assert.That(OverworldFollowerObservationRoute.CrossesBox(point, point, area.offset, area.size),
                Is.True, "slot " + observation.Slot + ": a stationary arrival must arm observation");
        }
    }

    [UnityTest]
    public IEnumerator NativePlay_SixFollowers_Observation_Rejoin_AndRoomCleanup()
    {
        EditorSceneManager.OpenScene(Root + "/Scenes/FollowerLab.unity", OpenSceneMode.Single);
        yield return new EnterPlayMode();
        Time.captureDeltaTime = 1f / 60f;
        for (int i = 0; i < 8; i++) yield return null;
        var session = Object.FindFirstObjectByType<OverworldFollowerLabSession>();
        Assert.That(session, Is.Not.Null);
        Assert.That(session.IsRunning, Is.True, session.LastError);
        Assert.That(GlobalDataManager.Instance.Party.Count, Is.EqualTo(6));
        Assert.That(SaveManager.AreProgressWritesBlocked, Is.True);
        var player = Object.FindFirstObjectByType<PlayerController>();
        OverworldPartyFollowerController followers = OverworldPartyFollowerController.For(player);
        Assert.That(followers, Is.Not.Null);
        for (int slot = 1; slot <= 5; slot++)
            Assert.That(followers.TryGetFollower(slot, out _), Is.True, "slot " + slot);

        // Walk a real obstacle-free L-shaped corridor. The fixture does not invent a spawn trail.
        yield return Move(player, new Vector2(-9.5f, 4.8f));
        yield return Move(player, new Vector2(-3.3f, 4.8f));
        Assert.That(followers.TryGetFollower(5, out OverworldFollowerActor last), Is.True);
        Assert.That(Vector2.Distance(last.transform.position, player.transform.position), Is.GreaterThan(2f));

        yield return Transition(player, "Exhibition");
        Vector3 stationaryArrival = player.transform.position;
        for (int i = 0; i < 180; i++) yield return null;
        Assert.That(Vector3.Distance(player.transform.position, stationaryArrival), Is.LessThan(.01f));
        OverworldFollowerObservation[] observations = AssertObservationArrivals(player, followers);

        // Exercise the same public rejoin path that the localized choice invokes.
        foreach (OverworldFollowerObservation observation in observations)
        {
            Assert.That(observation.TryJoin(out string error), Is.True, error);
            for (int i = 0; i < 180; i++) yield return null;
            Assert.That(followers.TryGetFollower(observation.Slot, out OverworldFollowerActor actor), Is.True);
            Assert.That(actor.Mode, Is.EqualTo(OverworldFollowerMode.Follow));
            Assert.That(actor.IsScriptedMoving, Is.False);
        }
        Assert.That(GameStateManager.Instance.CurrentState, Is.EqualTo(GameState.Exploration));

        // Neither crossing the former hidden centre-left zone nor advancing the tail is required.
        foreach (Vector2 arrival in new[] { new Vector2(9.5f, 0), new Vector2(-9.5f, 2), new Vector2(9.5f, -2) })
        {
            yield return Transition(player, "Detour");
            bool namedSpawn = arrival.y == 0;
            yield return Transition(player, "Exhibition", namedSpawn ? "from_right" : string.Empty,
                namedSpawn ? (Vector2?)null : arrival);
            stationaryArrival = player.transform.position;
            Assert.That(Vector2.Distance(stationaryArrival, arrival), Is.LessThan(.01f));
            for (int i = 0; i < 180; i++) yield return null;
            Assert.That(Vector3.Distance(player.transform.position, stationaryArrival), Is.LessThan(.01f));
            AssertObservationArrivals(player, followers);
        }
        yield return Transition(player, "Detour");
        Assert.That(Object.FindObjectsByType<OverworldFollowerObservationInteraction>(FindObjectsSortMode.None).Length, Is.Zero);
        Assert.That(GlobalDataManager.Instance.Party.Count, Is.EqualTo(6));
        session.enabled = false;
        Assert.That(SaveManager.AreProgressWritesBlocked, Is.False);
        Assert.That(GlobalDataManager.Instance.Party.Count, Is.Zero);
        Time.captureDeltaTime = 0;
        yield return new ExitPlayMode();
    }

    [UnityTearDown]
    public IEnumerator LeavePlayModeAfterFailure()
    {
        Time.captureDeltaTime = 0;
        if (Application.isPlaying) yield return new ExitPlayMode();
    }

    private static IEnumerator Move(PlayerController player, Vector2 destination)
    {
        Rigidbody2D body = player.GetComponent<Rigidbody2D>();
        for (int frame = 0; Vector2.Distance(body.position, destination) > .001f && frame < 600; frame++)
        {
            body.position = Vector2.MoveTowards(body.position, destination, .04f);
            yield return null;
        }
        Assert.That(Vector2.Distance(body.position, destination), Is.LessThan(.01f));
    }

    private static OverworldFollowerObservation[] AssertObservationArrivals(PlayerController player,
        OverworldPartyFollowerController followers)
    {
        var observations = Object.FindObjectsByType<OverworldFollowerObservation>(FindObjectsSortMode.None);
        Assert.That(observations.Length, Is.EqualTo(2));
        foreach (OverworldFollowerObservation observation in observations)
        {
            string label = "slot " + observation.Slot + ": " + observation.LastError;
            Assert.That(observation.IsHolding, Is.True, label);
            Assert.That(observation.LastError, Is.Empty, label); // RecoverHold is not a successful arrival.
            Assert.That(followers.TryGetFollower(observation.Slot, out OverworldFollowerActor actor), Is.True, label);
            Transform destination = observation.transform.Find("Out 2");
            Assert.That(destination, Is.Not.Null, label);
            Assert.That(Vector3.Distance(actor.transform.position, destination.position), Is.LessThan(.01f), label);
            Assert.That(actor.Mode, Is.EqualTo(OverworldFollowerMode.Hold), label);
            Assert.That(actor.IsScriptedMoving, Is.False, label);
            var proxy = observation.GetComponentInChildren<OverworldFollowerObservationInteraction>(true);
            Assert.That(proxy, Is.Not.Null, label);
            Assert.That(Vector3.Distance(proxy.transform.position, actor.transform.position), Is.LessThan(.01f), label);
            Assert.That(proxy.CanInteract(player), Is.True, label);
        }
        return observations;
    }

    private static IEnumerator Transition(PlayerController player, string name, string spawn = "from_left",
        Vector2? fallbackPosition = null)
    {
        RoomDefinition room = Room(name);
        var request = new MapTransitionRequest { TransitionType = MapTransitionType.Room, TargetRoom = room,
            TargetRoomId = room.RoomId, TargetAreaId = room.AreaDefinition.AreaId, TargetSpawnPointId = spawn,
            UseFallbackPosition = fallbackPosition.HasValue, FallbackPosition = fallbackPosition.GetValueOrDefault(),
            FacingAfterEnter = spawn == "from_right" ? FacingDirection.Left : FacingDirection.Right, FadeDuration = 0 };
        bool complete = false;
        Assert.That(MapTransitionService.Instance.TryRequestTransition(request, player, _ => complete = true), Is.True);
        for (int i = 0; !complete && i < 300; i++) yield return null;
        Assert.That(complete, Is.True);
        for (int i = 0; i < 5; i++) yield return null;
        Assert.That(Object.FindFirstObjectByType<RoomContainer>().CurrentDefinition, Is.SameAs(room));
    }

    private static RoomDefinition Room(string name)
        => AssetDatabase.LoadAssetAtPath<RoomDefinition>(Root + "/Data/Rooms/Room_FollowerLab_" + name + "_Definition.asset");
}
