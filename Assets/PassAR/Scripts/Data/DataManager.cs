using System;
using System.IO;
using UnityEngine;

namespace PassAR
{
    /// <summary>
    /// The only script that reads or writes saved information: the settings,
    /// the sound setting and the saved results. If saving fails, the training still carries on.
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

        private const string ExtinguisherKey = "extinguisher";

        public int LoadExtinguisher()
        {
            return PlayerPrefs.GetInt(ExtinguisherKey, 0);
        }

        public void SaveExtinguisher(int index)
        {
            PlayerPrefs.SetInt(ExtinguisherKey, index);
            PlayerPrefs.Save();
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

        /// <summary>Returns false if the result could not be saved.</summary>
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
