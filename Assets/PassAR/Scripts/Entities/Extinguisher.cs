using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace PassAR
{
    /// <summary>
    /// The pretend extinguisher held in front of the camera. It knows whether its pin is out
    /// and whether it is spraying, works out what you are pointing at, and puts out the
    /// fire zone it hits. Which actions are allowed at any moment is decided by TaskManager.
    /// </summary>
    public class Extinguisher : MonoBehaviour
    {
        [SerializeField] private Camera arCamera;
        [SerializeField] private ParticleSystem sprayParticles;
        [SerializeField] private ParticleSystem puffPrefab;
        [SerializeField] private GameObject[] models;
        [SerializeField] private GameObject[] pinVisuals;
        [SerializeField] private Transform[] nozzles;
        [SerializeField] private Transform sprayEmitter;
        private GameObject pinVisual;

        private const float PuffInterval = 0.2f;
        private const float PuffLifetime = 0.9f;

        private ScenarioConfig config;
        private ObjectPool<ParticleSystem> puffPool;
        private readonly List<ActivePuff> activePuffs = new List<ActivePuff>();

        private bool pinPulled;
        private bool squeezeHeld;
        private bool wasSpraying;
        private float puffTimer;

        private struct ActivePuff
        {
            public ParticleSystem System;
            public float ReleaseTime;
        }

        public event Action PinPulled;
        public event Action<bool> SprayChanged;

        public bool PinIsPulled => pinPulled;
        public bool SprayEnabled { get; set; }
        public AimInfo CurrentAim { get; private set; }

        /// <summary>True only when the handle is held, the pin is out and the current step allows it.</summary>
        public bool Spraying => squeezeHeld && pinPulled && SprayEnabled;

        public void Initialise(ScenarioConfig scenarioConfig)
        {
            config = scenarioConfig;

            if (puffPool == null)
            {
                puffPool = new ObjectPool<ParticleSystem>(
                    createFunc: () => Instantiate(puffPrefab),
                    actionOnGet: p => p.gameObject.SetActive(true),
                    actionOnRelease: p => p.gameObject.SetActive(false),
                    actionOnDestroy: p => Destroy(p.gameObject),
                    collectionCheck: false,
                    defaultCapacity: 6,
                    maxSize: 12);
            }
        }

        /// <summary>Shows one of the two extinguishers (level 1 or level 2) and moves the spray to its nozzle.</summary>
        public void SelectModel(int index)
        {
            if (models == null || models.Length == 0) return;
            index = Mathf.Clamp(index, 0, models.Length - 1);
            for (int i = 0; i < models.Length; i++) models[i].SetActive(i == index);
            pinVisual = pinVisuals[index];
            if (sprayEmitter != null && nozzles[index] != null)
                sprayEmitter.position = nozzles[index].position;
        }

        public void PullPin()
        {
            if (pinPulled) return;
            pinPulled = true;
            if (pinVisual != null) pinVisual.SetActive(false);
            PinPulled?.Invoke();
        }

        public void StartSpray()
        {
            squeezeHeld = true;
        }

        public void StopSpray()
        {
            squeezeHeld = false;
            Refresh(0f);
        }

        public void ResetState()
        {
            pinPulled = false;
            squeezeHeld = false;
            SprayEnabled = false;
            wasSpraying = false;
            CurrentAim = default;
            if (pinVisual != null) pinVisual.SetActive(true);
            SetEmission(false);
        }

        private void Update()
        {
            CurrentAim = ComputeAim();
            Refresh(Time.deltaTime);
            ReleaseFinishedPuffs();
        }

        private void OnDisable()
        {
            if (puffPool != null)
                foreach (ActivePuff puff in activePuffs) puffPool.Release(puff.System);
            activePuffs.Clear();
            SetEmission(false);
        }

        /// <summary>Looks straight ahead from the middle of the screen and checks how far away the fire is.</summary>
        public AimInfo AimRay()
        {
            return ComputeAim();
        }

        private AimInfo ComputeAim()
        {
            var aim = new AimInfo();
            if (config == null) return aim;

            Ray ray = arCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (Physics.Raycast(ray, out RaycastHit hit, config.maxRayLength))
            {
                FireZone zone = hit.collider.GetComponentInParent<FireZone>();
                if (zone != null && !zone.IsOut())
                {
                    aim.Zone = zone;
                    aim.Distance = hit.distance;
                    aim.DistanceOk = hit.distance >= config.minDist && hit.distance <= config.maxDist;
                    lastHitPoint = hit.point;
                }
            }
            return aim;
        }

        private Vector3 lastHitPoint;

        private void Refresh(float dt)
        {
            bool spraying = Spraying;

            if (spraying != wasSpraying)
            {
                wasSpraying = spraying;
                SetEmission(spraying);
                SprayChanged?.Invoke(spraying);
            }

            if (!spraying || dt <= 0f) return;

            AimInfo aim = CurrentAim;
            if (!aim.Valid) return;

            aim.Zone.Owner.ApplyAgent(aim.Zone.Id, dt);

            puffTimer -= dt;
            if (puffTimer <= 0f)
            {
                puffTimer = PuffInterval;
                SpawnPuff(lastHitPoint);
            }
        }

        private void SetEmission(bool on)
        {
            if (sprayParticles == null) return;
            var emission = sprayParticles.emission;
            emission.enabled = on;
            if (on && !sprayParticles.isPlaying) sprayParticles.Play();
        }

        private void SpawnPuff(Vector3 position)
        {
            ParticleSystem puff = puffPool.Get();
            puff.transform.position = position;
            puff.Clear();
            puff.Play();
            activePuffs.Add(new ActivePuff { System = puff, ReleaseTime = Time.time + PuffLifetime });
        }

        private void ReleaseFinishedPuffs()
        {
            for (int i = activePuffs.Count - 1; i >= 0; i--)
            {
                if (Time.time < activePuffs[i].ReleaseTime) continue;
                puffPool.Release(activePuffs[i].System);
                activePuffs.RemoveAt(i);
            }
        }
    }
}
