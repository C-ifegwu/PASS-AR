using System;

namespace PassAR
{
    /// <summary>
    /// One finished attempt. Serialised with JsonUtility, so public fields only.
    /// stepTimes order: Pull, Aim, Squeeze, Sweep.
    /// </summary>
    [Serializable]
    public class SessionRecord
    {
        public string sessionId;
        public string startTime;
        public float durationSec;
        public int errors;
        public int score;
        public float[] stepTimes;
        public bool completed;
    }
}
