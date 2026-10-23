using System.Collections.Generic;
using UnityEngine;

namespace PassAR
{
    /// <summary>
    /// The boss of the app. It knows which stage the app is in and passes
    /// messages between the other scripts. It never does their work itself.
    /// There is only ever one of these.
    /// </summary>
    public class SimulationManager : MonoBehaviour
    {
        public static SimulationManager Instance { get; private set; }

        [Header("Systems")]
        [SerializeField] private ARSessionController arController;
        [SerializeField] private UIManager ui;
        [SerializeField] private AudioManager audioManager;
        [SerializeField] private DataManager data;
        [SerializeField] private Extinguisher extinguisher;

        private SimState state = SimState.Welcome;
        private ScenarioConfig config;
        private TaskManager tasks;
        private FireHazard hazard;
        private string lastHint = "";
        private int lastShownSecond = -1;

        public SimState State => state;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Unsubscribe();
        }

        private void Start()
        {
            config = data.LoadConfig();
            extinguisher.Initialise(config);
            extinguisher.gameObject.SetActive(false);

            bool muted = data.LoadMuted();
            audioManager.SetMuted(muted);
            ui.SetMuteLabel(muted);
            int chosen = data.LoadExtinguisher();
            extinguisher.SelectModel(chosen);
            ui.SetExtinguisherChoice(chosen);

            Subscribe();
            RefreshMenuData();
            ui.ShowWelcome();
        }

        private void Subscribe()
        {
            ui.StartPressed += OnStartPressed;
            ui.RetryPressed += OnStartPressed;
            ui.PullPinPressed += OnPullPinPressed;
            ui.SqueezeDown += OnSqueezeDown;
            ui.SqueezeUp += OnSqueezeUp;
            ui.RestartPressed += Restart;
            ui.MutePressed += OnMutePressed;
            ui.MenuPressed += GoToMenu;
            ui.ClearHistoryPressed += OnClearHistory;
            ui.QuitPressed += OnQuit;
            ui.ExtinguisherChosen += OnExtinguisherChosen;

            arController.CameraPermissionResult += OnCameraResult;
            arController.SessionFailed += OnSessionFailed;
            arController.ScenarioPlaced += OnScenarioPlaced;
            arController.PlacementMissed += OnPlacementMissed;
            arController.TrackingChanged += OnTrackingChanged;

            extinguisher.PinPulled += OnPinPulled;
            extinguisher.SprayChanged += OnSprayChanged;
        }

        private void Unsubscribe()
        {
            if (ui != null)
            {
                ui.StartPressed -= OnStartPressed;
                ui.RetryPressed -= OnStartPressed;
                ui.PullPinPressed -= OnPullPinPressed;
                ui.SqueezeDown -= OnSqueezeDown;
                ui.SqueezeUp -= OnSqueezeUp;
                ui.RestartPressed -= Restart;
                ui.MutePressed -= OnMutePressed;
                ui.MenuPressed -= GoToMenu;
                ui.ClearHistoryPressed -= OnClearHistory;
                ui.QuitPressed -= OnQuit;
                ui.ExtinguisherChosen -= OnExtinguisherChosen;
            }
            if (arController != null)
            {
                arController.CameraPermissionResult -= OnCameraResult;
                arController.SessionFailed -= OnSessionFailed;
                arController.ScenarioPlaced -= OnScenarioPlaced;
                arController.PlacementMissed -= OnPlacementMissed;
                arController.TrackingChanged -= OnTrackingChanged;
            }
            if (extinguisher != null)
            {
                extinguisher.PinPulled -= OnPinPulled;
                extinguisher.SprayChanged -= OnSprayChanged;
            }
        }

        private void Update()
        {
            if (state != SimState.Running || tasks == null) return;

            tasks.Tick(Time.deltaTime);
            UpdateTimerLabel();
            UpdateAimHint();
        }

        // ---- start and camera ------------------------------------------------

        private void OnStartPressed()
        {
            ui.ShowBusy("Starting camera...");
            arController.BeginSession();
        }

        private void OnCameraResult(bool granted)
        {
            if (!granted)
            {
                ui.ShowPermissionDenied();
                return;
            }
            EnterScanning();
        }

        private void OnSessionFailed(string reason)
        {
            ui.ShowPermissionDenied(reason);
        }

        private void EnterScanning()
        {
            state = SimState.Scanning;
            arController.SetPlacementEnabled(true);
            arController.SetPlanesVisible(true);
            ui.ShowScanning();
        }

        // ---- placement and scenario ---------------------------------------------

        private void OnPlacementMissed()
        {
            ui.ShowWarning("No floor found yet - keep moving the phone slowly, then tap the floor");
        }

        private void OnScenarioPlaced(FireHazard placed)
        {
            hazard = placed;
            hazard.Initialise(config.zoneSeconds);
            hazard.ZoneOut += OnZoneOut;
            arController.SetPlanesVisible(false);
            StartScenario();
        }

        public void StartScenario()
        {
            extinguisher.ResetState();
            extinguisher.gameObject.SetActive(true);

            var steps = new List<TrainingStep>
            {
                new PullPinStep(extinguisher),
                new AimStep(extinguisher, config),
                new SqueezeStep(extinguisher, hazard, config),
                new SweepStep(hazard)
            };

            tasks = new TaskManager(config.errorPenalty);
            tasks.StepChanged += OnStepChanged;
            tasks.ErrorReported += OnErrorReported;
            tasks.Completed += CompleteScenario;

            state = SimState.Running;
            lastHint = "";
            lastShownSecond = -1;
            ui.ShowTraining();
            tasks.Begin(steps);
        }

        private void OnStepChanged(TrainingStep step)
        {
            ui.ShowInstruction(step.Instruction);
            extinguisher.SprayEnabled = tasks.SprayAllowed;
            ui.SetSqueezeLooksEnabled(tasks.SprayAllowed);
        }

        private void OnErrorReported(string reason)
        {
            ui.ShowWarning(reason);
            audioManager.Play(SoundId.Error);
        }

        // ---- button handlers -----------------------------------------------------

        private void OnPullPinPressed()
        {
            if (state != SimState.Running) return;
            extinguisher.PullPin();
        }

        private void OnSqueezeDown()
        {
            if (state != SimState.Running) return;
            if (!tasks.TrySqueeze()) return;
            extinguisher.StartSpray();
        }

        private void OnSqueezeUp()
        {
            extinguisher.StopSpray();
        }

        private void OnMutePressed()
        {
            bool muted = !data.LoadMuted();
            data.SaveMuted(muted);
            audioManager.SetMuted(muted);
            ui.SetMuteLabel(muted);
        }

        // ---- extinguisher and hazard events ---------------------------------------

        private void OnPinPulled()
        {
            audioManager.Play(SoundId.Pin);
            ui.ShowFeedback("Pin removed");
        }

        private void OnSprayChanged(bool spraying)
        {
            audioManager.SetLoop(SoundId.Spray, spraying);
        }

        private void OnZoneOut(int zoneId)
        {
            ui.ShowFeedback("Fire zone " + (zoneId + 1) + " out");
        }

        // ---- tracking -------------------------------------------------------------

        private void OnTrackingChanged(bool tracking)
        {
            ui.ShowTrackingLost(!tracking);

            if (!tracking && state == SimState.Running)
            {
                state = SimState.Paused;
                extinguisher.StopSpray();
                extinguisher.enabled = false;
            }
            else if (tracking && state == SimState.Paused)
            {
                extinguisher.enabled = true;
                state = SimState.Running;
            }
        }

        // ---- finishing and restarting ---------------------------------------------

        public void CompleteScenario()
        {
            state = SimState.Summary;
            extinguisher.StopSpray();
            extinguisher.gameObject.SetActive(false);
            ui.ClearHint();

            SessionRecord record = tasks.BuildRecord();
            bool saved = data.SaveSession(record);
            int best = data.BestScore();

            audioManager.Play(SoundId.Success);
            ui.ShowSummary(record, best);
            RefreshMenuData();
            if (!saved) ui.ShowWarning("Result could not be saved on this phone");
        }

        public void Restart()
        {
            if (state != SimState.Summary) return;

            // Try Again: put the fire out of its burnt state, restart the steps and the timer. The fire stays where it was placed.
            hazard.ResetHazard();
            ui.HideSummary();
            StartScenario();
        }

        // ---- main menu and settings ------------------------------------------------

        /// <summary>Back to the main menu from the summary; the placed fire is removed.</summary>
        public void GoToMenu()
        {
            if (state != SimState.Summary) return;

            if (hazard != null) hazard.ZoneOut -= OnZoneOut;
            hazard = null;
            tasks = null;
            arController.ClearScenario();
            arController.SetPlacementEnabled(false);
            arController.SetPlanesVisible(false);

            state = SimState.Welcome;
            ui.HideSummary();
            RefreshMenuData();
            ui.ShowWelcome();
        }

        private void OnClearHistory()
        {
            if (!data.ClearSessions()) ui.ShowWarning("Could not clear saved results");
            RefreshMenuData();
        }

        private void OnQuit()
        {
            Application.Quit();
        }

        private void OnExtinguisherChosen(int index)
        {
            data.SaveExtinguisher(index);
            extinguisher.SelectModel(index);
            ui.SetExtinguisherChoice(index);
        }

        private void RefreshMenuData()
        {
            ui.SetBestScore(data.BestScore());

            string rules =
                "Training rules (ScenarioConfig)\n" +
                "Put out each flame: " + config.zoneSeconds.ToString("0.#") + " s of spray\n" +
                "Safe distance: " + config.minDist.ToString("0.0") + " to " + config.maxDist.ToString("0.0") + " m\n" +
                "Score: 100 minus " + config.errorPenalty + " per mistake";
            ui.SetSettingsInfo(rules, data.SessionCount(), config.maxSavedSessions);
        }

        // ---- small helpers ----------------------------------------------------------

        private void UpdateTimerLabel()
        {
            int whole = Mathf.FloorToInt(tasks.ElapsedSeconds);
            if (whole == lastShownSecond) return;
            lastShownSecond = whole;
            ui.SetTimer(whole);
        }

        private void UpdateAimHint()
        {
            string hint = "";
            AimInfo aim = extinguisher.CurrentAim;
            if (extinguisher.PinIsPulled && aim.HitsZone && !aim.DistanceOk)
                hint = aim.Distance < config.minDist ? "Step back" : "Move closer";

            if (hint == lastHint) return;
            lastHint = hint;
            if (hint.Length == 0) ui.ClearHint(); else ui.ShowHint(hint);
        }
    }
}
