using UnityEngine;

namespace PassAR
{
    /// <summary>
    /// Every tunable number in the simulation. Nothing else in the code base
    /// hard-codes these values (NFR-08). Bundled in the build as an asset.
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
