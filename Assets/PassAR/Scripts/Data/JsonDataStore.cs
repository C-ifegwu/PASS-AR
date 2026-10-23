using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace PassAR
{
    /// <summary>
    /// One small text file for each finished attempt, named session_[time].json.
    /// Only the newest files are kept. Each file is saved under a temporary name first and
    /// then renamed, so a failed save never leaves a half-written file.
    /// </summary>
    public class JsonDataStore : IDataStore
    {
        private const string Prefix = "session_";
        private const string Extension = ".json";

        private readonly string folder;
        private readonly int maxFiles;

        public JsonDataStore(string folder, int maxFiles)
        {
            this.folder = folder;
            this.maxFiles = maxFiles;
        }

        public void Save(SessionRecord record)
        {
            Directory.CreateDirectory(folder);

            string path = Path.Combine(folder, Prefix + record.sessionId + Extension);
            string temp = path + ".tmp";

            File.WriteAllText(temp, JsonUtility.ToJson(record, true));
            if (File.Exists(path)) File.Delete(path);
            File.Move(temp, path);

            Prune();
        }

        public List<SessionRecord> LoadAll()
        {
            var records = new List<SessionRecord>();
            if (!Directory.Exists(folder)) return records;

            foreach (string path in SessionFiles())
            {
                try
                {
                    var record = JsonUtility.FromJson<SessionRecord>(File.ReadAllText(path));
                    if (record != null) records.Add(record);
                }
                catch (Exception)
                {
                    // A damaged file should never stop the app; skip it.
                }
            }
            return records;
        }

        /// <summary>Deletes every saved result (Settings, Clear saved results).</summary>
        public void Clear()
        {
            if (!Directory.Exists(folder)) return;
            foreach (string path in SessionFiles()) File.Delete(path);
        }

        private string[] SessionFiles()
        {
            string[] files = Directory.GetFiles(folder, Prefix + "*" + Extension);
            Array.Sort(files, StringComparer.Ordinal);
            return files;
        }

        private void Prune()
        {
            string[] files = SessionFiles();
            for (int i = 0; i < files.Length - maxFiles; i++)
                File.Delete(files[i]);
        }
    }
}
