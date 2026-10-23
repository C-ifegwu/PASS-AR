using UnityEngine;

namespace PassAR
{
    /// <summary>
    /// One third of the fire (left, centre or right). Intensity goes from 1 to 0
    /// and the flames shrink with it. The collider sits at the base of the flames.
    /// </summary>
    public class FireZone : MonoBehaviour
    {
        [SerializeField] private ParticleSystem flames;

        private float intensity = 1f;
        private float baseRate;
        private float baseSize;

        public Hazard Owner { get; private set; }
        public int Id { get; private set; }
        public float Intensity => intensity;

        private void Awake()
        {
            var emission = flames.emission;
            baseRate = emission.rateOverTime.constant;
            baseSize = flames.main.startSize.constant;
        }

        public void Bind(Hazard owner, int id)
        {
            Owner = owner;
            Id = id;
        }

        public void Reduce(float amount)
        {
            if (IsOut()) return;
            intensity = Mathf.Clamp01(intensity - amount);
            ApplyVisuals();
        }

        public bool IsOut()
        {
            return intensity <= 0f;
        }

        public void ResetZone()
        {
            intensity = 1f;
            ApplyVisuals();
        }

        private void ApplyVisuals()
        {
            var emission = flames.emission;
            emission.rateOverTime = baseRate * intensity;

            var main = flames.main;
            main.startSize = baseSize * Mathf.Lerp(0.35f, 1f, intensity);

            if (IsOut())
            {
                flames.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
            else if (!flames.isPlaying)
            {
                flames.Play();
            }
        }
    }
}
