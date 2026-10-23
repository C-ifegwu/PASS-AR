using System;
using System.Collections.Generic;
using UnityEngine;

namespace PassAR
{
    /// <summary>
    /// Something the trainee has to deal with. Today there is only FireHazard,
    /// but the managers only know this base class.
    /// </summary>
    public abstract class Hazard : MonoBehaviour
    {
        [SerializeField] protected List<FireZone> zones = new List<FireZone>();

        private float extinguishSeconds = 3f;

        public event Action<int> ZoneOut;

        public int ZoneCount => zones.Count;

        public int ZonesOut
        {
            get
            {
                int count = 0;
                foreach (FireZone zone in zones)
                    if (zone.IsOut()) count++;
                return count;
            }
        }

        protected virtual void Awake()
        {
            for (int i = 0; i < zones.Count; i++)
                zones[i].Bind(this, i);
        }

        public void Initialise(float secondsToExtinguishZone)
        {
            extinguishSeconds = Mathf.Max(0.1f, secondsToExtinguishZone);
        }

        /// <summary>Applies extinguishing agent to one zone for dt seconds (FR-10).</summary>
        public void ApplyAgent(int zoneId, float dt)
        {
            if (zoneId < 0 || zoneId >= zones.Count) return;

            FireZone zone = zones[zoneId];
            if (zone.IsOut()) return;

            zone.Reduce(dt / extinguishSeconds);
            if (zone.IsOut()) ZoneOut?.Invoke(zoneId);
        }

        public void ResetHazard()
        {
            foreach (FireZone zone in zones) zone.ResetZone();
        }

        public abstract bool IsNeutralised();
    }
}
