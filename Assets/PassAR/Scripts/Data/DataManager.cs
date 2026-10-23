using System;
using System.IO;
using UnityEngine;

namespace PassAR
{
    /// <summary>
    /// The only class that reads or writes stored data: the scenario config,
    /// the mute setting and the saved sessions. A storage failure never stops
    /// the simulation.
    /// </summary>
    public class DataManager : MonoBehaviour
    {
        private const string MutedKey = "muted";

        [SerializeField] private ScenarioConfig config;

        private IDataStore store;

        private void Awake()
        {
            if (config == null) config = ScriptableObject.CreateInstance<ScenarioConfig>();
            store = new JsonDataStore(Path.Combine(Application.persistentDataPath, "sessions"), config.maxSavedSessions);
        }

        public ScenarioConfig LoadConfig()
        {
            return config;
        }

        public bool LoadMuted()
        {
            return PlayerPrefs.GetInt(MutedKey, 0) == 1;
        }

        public void SaveMuted(bool muted)
        {
            PlayerPrefs.SetInt(MutedKey, muted ? 1 : 0);
            PlayerPrefs.Save();
        }

        /// <summary>Returns false if the file could not be written (FR-18).</summary>
        public bool SaveSession(SessionRecord record)
        {
            try
            {
                store.Save(record);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("Could not save session: " + e.Message);
                return false;
            }
        }

        public int SessionCount()
        {
            try { return store.LoadAll().Count; }
            catch (Exception) { return 0; }
        }

        /// <summary>Returns false if the files could not be deleted.</summary>
        public bool ClearSessions()
        {
            try
            {
                store.Clear();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("Could not clear sessions: " + e.Message);
                return false;
            }
        }

        /// <summary>Highest score among completed saved sessions, or -1 if there are none.</summary>
        public int BestScore()
        {
            int best = -1;
            try
            {
                foreach (SessionRecord record in store.LoadAll())
                    if (record.completed && record.score > best) best = record.score;
            }
            catch (Exception e)
            {
                Debug.LogWarning("Could not read sessions: " + e.Message);
            }
            return best;
        }
    }
}
