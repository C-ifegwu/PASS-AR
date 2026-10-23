using System;

namespace PassAR
{
    /// <summary>
    /// The result of one finished attempt. It is saved as a small text file,
    /// so every field is public. stepTimes holds the seconds spent in Pull, Aim, Squeeze, Sweep.
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
