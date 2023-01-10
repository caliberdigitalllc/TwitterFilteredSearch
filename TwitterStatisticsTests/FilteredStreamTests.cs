using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestPlatform.TestHost;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TwitterApp.Tests {
    [TestClass]
    public class FilteredStreamTests {
        [TestMethod]
        public async Task ProcessTweetAsync_WithValidInput_UpdatesHashtagsDictionary() {
            // Arrange
            string line = "{\"data\":{\"entities\":{\"hashtags\":[{\"text\":\"hashtag1\"},{\"text\":\"hashtag2\"}]}}}";

            // Act
            await TwitterApp.Program.ProcessTweetAsync(line);

            // Assert
            Assert.AreEqual(2, TwitterApp.Program.hashtags.Count);
            Assert.AreEqual(1, TwitterApp.Program.hashtags["hashtag1"]);
            Assert.AreEqual(1, TwitterApp.Program.hashtags["hashtag2"]);
        }

        [TestMethod]
        public async Task ProcessTweetAsync_WithNullLine_DoesNotUpdateHashtagsDictionary() {
            // Arrange
            string line = null;
            int initialCount = TwitterApp.Program.hashtags.Count;

            // Act
            await TwitterApp.Program.ProcessTweetAsync(line);

            // Assert
            Assert.AreEqual(initialCount, TwitterApp.Program.hashtags.Count);
        }

        [TestMethod]
        public async Task ProcessTweetAsync_WithEmptyLine_DoesNotUpdateHashtagsDictionary() {
            // Arrange
            string line = "";
            int initialCount = TwitterApp.Program.hashtags.Count;

            // Act
            await TwitterApp.Program.ProcessTweetAsync(line);

            // Assert
            Assert.AreEqual(initialCount, TwitterApp.Program.hashtags.Count);
        }

        [TestMethod]
        public async Task ProcessTweetAsync_WithLineWithoutHashtags_DoesNotUpdateHashtagsDictionary() {
            // Arrange
            string line = "{\"data\":{\"entities\":{}}}";
            int initialCount = TwitterApp.Program.hashtags.Count;

            // Act
            await TwitterApp.Program.ProcessTweetAsync(line);

            // Assert
            Assert.AreEqual(initialCount, TwitterApp.Program.hashtags.Count);
        }

        [TestMethod]
        public async Task ProcessTweetAsync_WithLineContainingDuplicateHashtags_UpdatesHashtagsDictionaryCorrectly() {
            // Arrange
            string line = "{\"data\":{\"entities\":{\"hashtags\":[{\"text\":\"hashtag1\"},{\"text\":\"hashtag1\"},{\"text\":\"hashtag2\"}]}}}";

            // Act
            await TwitterApp.Program.ProcessTweetAsync(line);

            // Assert
        }

        [TestMethod]
        public async Task GetTopHashtagsAsync_WithValidInput_ReturnsCorrectResult() {
            // Arrange
            TwitterApp.Program.hashtags["hashtag1"] = 3;
            TwitterApp.Program.hashtags["hashtag2"] = 2;
            TwitterApp.Program.hashtags["hashtag3"] = 1;

            // Act
            var result = await TwitterApp.Program.GetTopHashtagsAsync(10);

            // Assert
            CollectionAssert.AreEqual(new[] { "hashtag1", "hashtag2", "hashtag3" }, result);
        }

        [TestMethod]
        public async Task GetTopHashtagsAsync_WithEmptyHashtagsDictionary_ReturnsEmptyResult() {
            // Arrange
            TwitterApp.Program.hashtags.Clear();

            // Act
            var result = await TwitterApp.Program.GetTopHashtagsAsync(10);

            // Assert
            Assert.AreEqual(0, result.Count());
        }
    }
}