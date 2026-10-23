using UnityEngine;

namespace PassAR
{
    /// <summary>
    /// One third of the fire (left, centre or right). Its strength goes from 1 to 0
    /// and the flames shrink with it. The invisible hit box sits at the base of the flames.
    /// </summary>
    public class FireZone : MonoBehaviour
    {
        [SerializeField] private Transform flameRoot;

        private float intensity = 1f;
        private Vector3 baseScale;
        private ParticleSystem[] systems;

        public Hazard Owner { get; private set; }
        public int Id { get; private set; }
        public float Intensity => intensity;

        private void Awake()
        {
            baseScale = flameRoot.localScale;
            systems = flameRoot.GetComponentsInChildren<ParticleSystem>(true);
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
            flameRoot.localScale = baseScale * Mathf.Lerp(0.3f, 1f, intensity);

            for (int i = 0; i < systems.Length; i++)
            {
                if (IsOut())
                    systems[i].Stop(false, ParticleSystemStopBehavior.StopEmitting);
                else if (!systems[i].isPlaying)
                    systems[i].Play(false);
            }
        }
    }
}
