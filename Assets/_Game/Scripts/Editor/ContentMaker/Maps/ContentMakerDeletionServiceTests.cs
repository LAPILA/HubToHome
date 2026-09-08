#if UNITY_INCLUDE_TESTS
using NUnit.Framework;

namespace HubToHome.EditorTools.ContentMaker
{
    internal sealed class ContentMakerDeletionServiceTests
    {
        private const string Folder = "Assets/_Game/Content/Maps/Regions/Chapter01/Data/Rooms";

        [TestCase(Folder + "/Room.asset", true)]
        [TestCase(Folder + "/Nested/Room.asset", true)]
        [TestCase(Folder, false)]
        [TestCase(Folder + "Other/Room.asset", false)]
        [TestCase(Folder + "/../Room.asset", false)]
        [TestCase(Folder + "//Room.asset", false)]
        [TestCase(Folder + "\\Room.asset", false)]
        public void RoomCandidatesCannotEscapeTheirOwnedFolder(string path, bool expected)
        {
            Assert.That(ContentMakerDeletionService.IsInFolder(path, Folder), Is.EqualTo(expected));
        }

        [TestCase("TargetRoomId: chapter01.windmill", true)]
        [TestCase("room: \"chapter01.windmill\"", true)]
        [TestCase("TargetRoomId: chapter01.windmill_interior", false)]
        [TestCase("TargetRoomId: chapter01.windmill.other", false)]
        [TestCase("TargetRoomId: old_chapter01.windmill", false)]
        public void StringReferenceCheckUsesCompleteIds(string text, bool expected)
        {
            var pattern = ContentMakerDeletionService.MakeTokenPattern(new[] { "chapter01.windmill" });
            Assert.That(pattern.IsMatch(text), Is.EqualTo(expected));
        }

        [Test]
        public void EmptyOrBlockedPreviewCannotDelete()
        {
            Assert.That(new ContentMakerDeletionPreview().CanDelete, Is.False);
            Assert.That(new ContentMakerDeletionPreview { Paths = new[] { Folder + "/Room.asset" }, Blockers = new[] { "사용 중" } }.CanDelete, Is.False);
        }
    }
}
#endif
