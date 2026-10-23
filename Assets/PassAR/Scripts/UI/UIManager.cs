using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace PassAR
{
    /// <summary>
    /// Everything the user sees and taps. Contains no simulation logic: it shows
    /// what it is told and raises events when buttons are used.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] private GameObject welcomePanel;
        [SerializeField] private GameObject permissionPanel;
        [SerializeField] private GameObject hudPanel;
        [SerializeField] private GameObject summaryPanel;
        [SerializeField] private GameObject trackingLostBanner;

        [Header("Main menu, how to play and settings")]
        [SerializeField] private GameObject howToPanel;
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private Button howToButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private Button howToBackButton;
        [SerializeField] private Button settingsBackButton;
        [SerializeField] private Button soundToggleButton;
        [SerializeField] private Text soundToggleLabel;
        [SerializeField] private Button clearButton;
        [SerializeField] private Text clearLabel;
        [SerializeField] private Text menuBestText;
        [SerializeField] private Text rulesText;
        [SerializeField] private Text savedInfoText;
        [SerializeField] private Button menuButton;

        [Header("Welcome / permission")]
        [SerializeField] private Button startButton;
        [SerializeField] private Button retryButton;
        [SerializeField] private Text permissionMessage;
        [SerializeField] private Text busyText;

        [Header("HUD")]
        [SerializeField] private Text instructionText;
        [SerializeField] private Text messageText;
        [SerializeField] private Text timerText;
        [SerializeField] private Button pullPinButton;
        [SerializeField] private HoldButton squeezeButton;
        [SerializeField] private Image squeezeImage;
        [SerializeField] private Button muteButton;
        [SerializeField] private Text muteLabel;
        [SerializeField] private GameObject reticle;
        [SerializeField] private GameObject actionButtons;

        [Header("Summary")]
        [SerializeField] private Text summaryTime;
        [SerializeField] private Text summaryErrors;
        [SerializeField] private Text summaryScore;
        [SerializeField] private Text summaryBest;
        [SerializeField] private Button restartButton;

        public event Action StartPressed;
        public event Action RetryPressed;
        public event Action PullPinPressed;
        public event Action SqueezeDown;
        public event Action SqueezeUp;
        public event Action RestartPressed;
        public event Action MutePressed;
        public event Action MenuPressed;
        public event Action ClearHistoryPressed;
        public event Action QuitPressed;

        private static readonly Color WarningColor = new Color(1f, 0.45f, 0.4f);
        private static readonly Color HintColor = new Color(1f, 0.72f, 0.42f);
        private static readonly Color FeedbackColor = new Color(0.55f, 1f, 0.6f);

        private const float WarningSeconds = 2.5f;
        private const float FeedbackSeconds = 1.5f;

        private string hint = "";
        private string transientText = "";
        private Color transientColor;
        private float transientUntil;
        private float clearArmedUntil;
        private const float ClearConfirmSeconds = 4f;

        private void Awake()
        {
            startButton.onClick.AddListener(() => StartPressed?.Invoke());
            retryButton.onClick.AddListener(() => RetryPressed?.Invoke());
            pullPinButton.onClick.AddListener(() => PullPinPressed?.Invoke());
            restartButton.onClick.AddListener(() => RestartPressed?.Invoke());
            muteButton.onClick.AddListener(() => MutePressed?.Invoke());
            soundToggleButton.onClick.AddListener(() => MutePressed?.Invoke());
            menuButton.onClick.AddListener(() => MenuPressed?.Invoke());
            quitButton.onClick.AddListener(() => QuitPressed?.Invoke());
            howToButton.onClick.AddListener(ShowHowTo);
            settingsButton.onClick.AddListener(ShowSettings);
            howToBackButton.onClick.AddListener(ShowWelcome);
            settingsBackButton.onClick.AddListener(ShowWelcome);
            clearButton.onClick.AddListener(OnClearPressed);
            squeezeButton.Pressed += () => SqueezeDown?.Invoke();
            squeezeButton.Released += () => SqueezeUp?.Invoke();

            ShowOnly(null);
        }

        private void Update()
        {
            // Android Back key (Escape in the Input System) leaves How to play and Settings.
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame &&
                (howToPanel.activeSelf || settingsPanel.activeSelf))
                ShowWelcome();

            if (clearArmedUntil > 0f && Time.unscaledTime > clearArmedUntil) DisarmClear();

            // Warnings and feedback win over the hint; the hint stays until cleared.
            if (Time.unscaledTime < transientUntil)
            {
                messageText.text = transientText;
                messageText.color = transientColor;
            }
            else
            {
                messageText.text = hint;
                messageText.color = HintColor;
            }
        }

        // ---- panels ---------------------------------------------------------------

        private void ShowOnly(GameObject panel)
        {
            welcomePanel.SetActive(panel == welcomePanel);
            permissionPanel.SetActive(panel == permissionPanel);
            howToPanel.SetActive(panel == howToPanel);
            settingsPanel.SetActive(panel == settingsPanel);
            hudPanel.SetActive(panel == hudPanel);
            summaryPanel.SetActive(false);
        }

        public void ShowWelcome()
        {
            ShowOnly(welcomePanel);
            busyText.text = "";
            startButton.gameObject.SetActive(true);
        }

        public void ShowHowTo()
        {
            ShowOnly(howToPanel);
        }

        public void ShowSettings()
        {
            DisarmClear();
            ShowOnly(settingsPanel);
        }

        public void SetBestScore(int best)
        {
            menuBestText.text = best >= 0 ? "Best score: " + best + " / 100" : "No completed training yet";
        }

        public void SetSettingsInfo(string rules, int savedSessions, int maxSessions)
        {
            rulesText.text = rules;
            savedInfoText.text = "Saved results on this phone: " + savedSessions + " (newest " + maxSessions + " kept)";
        }

        private void OnClearPressed()
        {
            if (clearArmedUntil > 0f)
            {
                DisarmClear();
                ClearHistoryPressed?.Invoke();
                return;
            }
            clearArmedUntil = Time.unscaledTime + ClearConfirmSeconds;
            clearLabel.text = "TAP AGAIN TO CONFIRM";
        }

        private void DisarmClear()
        {
            clearArmedUntil = 0f;
            clearLabel.text = "CLEAR SAVED RESULTS";
        }

        public void ShowBusy(string message)
        {
            busyText.text = message;
            startButton.gameObject.SetActive(false);
        }

        public void ShowPermissionDenied(string message = null)
        {
            ShowOnly(permissionPanel);
            permissionMessage.text = message ??
                "Camera access is needed to place the fire in your room. Please allow the camera and try again.";
        }

        public void ShowScanning()
        {
            ShowOnly(hudPanel);
            actionButtons.SetActive(false);
            reticle.SetActive(false);
            timerText.text = "";
            ClearHint();
            ShowInstruction("Move your phone slowly to scan the floor, then TAP a floor surface to place the fire.");
        }

        public void ShowTraining()
        {
            ShowOnly(hudPanel);
            actionButtons.SetActive(true);
            reticle.SetActive(true);
            timerText.text = "0 s";
            SetSqueezeLooksEnabled(false);
        }

        // ---- text -------------------------------------------------------------------

        public void ShowInstruction(string message)
        {
            instructionText.text = message;
        }

        public void ShowWarning(string message)
        {
            SetTransient(message, WarningColor, WarningSeconds);
        }

        public void ShowFeedback(string message)
        {
            SetTransient(message, FeedbackColor, FeedbackSeconds);
        }

        public void ShowHint(string message)
        {
            hint = message;
        }

        public void ClearHint()
        {
            hint = "";
        }

        private void SetTransient(string message, Color color, float seconds)
        {
            transientText = message;
            transientColor = color;
            transientUntil = Time.unscaledTime + seconds;
        }

        public void SetTimer(int seconds)
        {
            timerText.text = seconds + " s";
        }

        public void SetSqueezeLooksEnabled(bool enabled)
        {
            // The button still receives presses when "disabled" so that pressing it
            // too early can be counted as an out-of-order action (FR-07, FR-12).
            squeezeImage.color = enabled ? new Color(0.75f, 0.22f, 0.17f) : new Color(0.35f, 0.35f, 0.37f);
        }

        public void SetMuteLabel(bool muted)
        {
            string label = muted ? "Sound: OFF" : "Sound: ON";
            muteLabel.text = label;
            soundToggleLabel.text = label;
        }

        public void ShowTrackingLost(bool lost)
        {
            trackingLostBanner.SetActive(lost);
        }

        // ---- summary ------------------------------------------------------------------

        public void ShowSummary(SessionRecord record, int bestScore)
        {
            summaryPanel.SetActive(true);
            actionButtons.SetActive(false);
            reticle.SetActive(false);

            summaryTime.text = "Time: " + record.durationSec.ToString("0.0") + " s";
            summaryErrors.text = "Errors: " + record.errors;
            summaryScore.text = "Score: " + record.score + " / 100";
            summaryBest.text = bestScore >= 0 ? "Best score: " + bestScore : "";
            ShowInstruction(record.errors == 0
                ? "Well done - fire out with no mistakes."
                : "Fire out. Try again to improve your score.");
        }

        public void HideSummary()
        {
            summaryPanel.SetActive(false);
        }
    }
}
