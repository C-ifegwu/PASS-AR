using System.IO;
using NUnit.Framework;

namespace PassAR.Tests
{
    public class JsonDataStoreTests
    {
        private string folder;

        [SetUp]
        public void SetUp()
        {
            folder = Path.Combine(Path.GetTempPath(), "passar_tests_" + System.Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }

        private static SessionRecord Record(string id, int score = 90)
        {
            return new SessionRecord
            {
                sessionId = id,
                startTime = "2026-10-14T10:15:30",
                durationSec = 74.2f,
                errors = 1,
                score = score,
                stepTimes = new[] { 3.1f, 9.8f, 12.4f, 48.9f },
                completed = true
            };
        }

        [Test]
        public void SavedRecord_CanBeReadBack()
        {
            var store = new JsonDataStore(folder, 50);
            store.Save(Record("20261014-101530"));

            var loaded = store.LoadAll();

            Assert.AreEqual(1, loaded.Count);
            Assert.AreEqual("20261014-101530", loaded[0].sessionId);
            Assert.AreEqual(90, loaded[0].score);
            Assert.AreEqual(4, loaded[0].stepTimes.Length);
            Assert.IsTrue(loaded[0].completed);
        }

        [Test]
        public void Clear_RemovesEverySavedSession()
        {
            var store = new JsonDataStore(folder, 50);
            store.Save(Record("20261014-101530"));
            store.Save(Record("20261014-101631"));

            store.Clear();

            Assert.AreEqual(0, store.LoadAll().Count);
        }

        [Test]
        public void FileName_FollowsTheDocumentedPattern()
        {
            new JsonDataStore(folder, 50).Save(Record("20261014-101530"));
            Assert.IsTrue(File.Exists(Path.Combine(folder, "session_20261014-101530.json")));
        }

        [Test]
        public void OnlyTheNewestFilesAreKept()
        {
            var store = new JsonDataStore(folder, 3);
            for (int i = 1; i <= 5; i++) store.Save(Record("2026101" + i + "-000000", i));

            var loaded = store.LoadAll();

            Assert.AreEqual(3, loaded.Count);
            Assert.AreEqual(3, loaded[0].score); // results 1 and 2 were removed
        }

        [Test]
        public void NoTemporaryFileIsLeftBehind()
        {
            new JsonDataStore(folder, 50).Save(Record("20261014-101530"));
            Assert.AreEqual(0, Directory.GetFiles(folder, "*.tmp").Length);
        }

        [Test]
        public void DamagedFile_IsSkipped()
        {
            var store = new JsonDataStore(folder, 50);
            store.Save(Record("20261014-101530"));
            File.WriteAllText(Path.Combine(folder, "session_20261015-000000.json"), "{ not json");

            Assert.AreEqual(1, store.LoadAll().Count);
        }

        [Test]
        public void MissingFolder_GivesAnEmptyList()
        {
            Assert.AreEqual(0, new JsonDataStore(folder, 50).LoadAll().Count);
        }
    }
}
