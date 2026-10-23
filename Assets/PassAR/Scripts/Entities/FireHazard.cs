using UnityEngine;

namespace PassAR
{
    /// <summary>The fire: three burning zones with a crackling sound that gets quieter as the fire dies.</summary>
    public class FireHazard : Hazard
    {
        [SerializeField] private AudioClip fireSound;
        [SerializeField] private AudioSource fireSource;

        protected override void Awake()
        {
            base.Awake();
            if (fireSource != null && fireSound != null)
            {
                fireSource.clip = fireSound;
                fireSource.loop = true;
                fireSource.spatialBlend = 1f;
                fireSource.Play();
            }
        }

        private void Update()
        {
            if (fireSource == null || zones.Count == 0) return;

            float total = 0f;
            foreach (FireZone zone in zones) total += zone.Intensity;
            fireSource.volume = total / zones.Count;
        }

        public override bool IsNeutralised()
        {
            return ZonesOut >= ZoneCount;
        }
    }
}
