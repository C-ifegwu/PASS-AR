using UnityEngine;

namespace PassAR
{
    /// <summary>
    /// Every number you might want to change in the training lives here.
    /// No other script has these numbers written inside it.
    /// </summary>
    [CreateAssetMenu(fileName = "ScenarioConfig", menuName = "PASS-AR/Scenario Config")]
    public class ScenarioConfig : ScriptableObject
    {
        [Header("Extinguishing (FR-10)")]
        [Min(0.5f)] public float zoneSeconds = 3f;

        [Header("Safe distance in metres (FR-08)")]
        [Min(0f)] public float minDist = 1f;
        [Min(0.1f)] public float maxDist = 3f;
        [Min(1f)] public float maxRayLength = 6f;

        [Header("Aiming")]
        [Tooltip("Seconds the aim must stay valid to complete the Aim step.")]
        [Min(0f)] public float aimHoldSeconds = 0.5f;
        [Tooltip("Seconds of lost aim tolerated in the Squeeze step before going back to Aim.")]
        [Min(0f)] public float aimLostGrace = 1f;

        [Header("Scoring (FR-15)")]
        [Min(0)] public int errorPenalty = 10;

        [Header("Storage (DMP)")]
        [Min(1)] public int maxSavedSessions = 50;
    }
}
