#if UNITY_INCLUDE_TESTS
using System;
using NUnit.Framework;

namespace HubToHome.EditorTools.ContentMaker
{
    internal sealed class ContentMakerAssetUtilityTests
    {
        [TestCase("Assets/_Game/Content/Maps/Regions/Chapter01", "Assets/_Game/Content/Maps/Regions/Chapter01")]
        [TestCase("Assets\\_Game\\Content\\Dialogue\\Data\\", "Assets/_Game/Content/Dialogue/Data")]
        public void NormalizeContentPathKeepsContentBoundary(string input, string expected)
        {
            Assert.That(ContentMakerAssetUtility.NormalizeContentPath(input), Is.EqualTo(expected));
        }

        [TestCase("Assets/_Game/Content/../Scripts")]
        [TestCase("Assets/_Game/ContentBackup/Maps")]
        [TestCase("C:/Outside/Assets/_Game/Content")]
        [TestCase("Assets/_Game/Content//Rooms")]
        [TestCase("Assets/_Game/Content/Rooms.")]
        public void InvalidContentPathIsRejectedBeforeWriting(string input)
        {
            Assert.Throws<ArgumentException>(() => ContentMakerAssetUtility.NormalizeContentPath(input));
        }

        [TestCase(" CON ", "_CON")]
        [TestCase("풍차/실내", "풍차_실내")]
        [TestCase("Room<new>", "Room_new_")]
        [TestCase("..", "Untitled")]
        public void FileNamesPreserveKoreanButRejectUnsafeCharacters(string input, string expected)
        {
            Assert.That(ContentMakerAssetUtility.MakeFileName(input), Is.EqualTo(expected));
        }

        [Test]
        public void GeneratedIdsAreNonEmptyWithoutRewritingDisplayNames()
        {
            Assert.That(ContentMakerAssetUtility.MakeId("Windmill Exterior"), Is.EqualTo("windmill_exterior"));
            Assert.That(ContentMakerAssetUtility.MakeId("풍차 실내"), Does.Match("^content_[a-f0-9]{8}$"));
        }
    }
}
#endif
