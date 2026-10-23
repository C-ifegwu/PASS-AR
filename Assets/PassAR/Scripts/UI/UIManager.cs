using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace PassAR
{
    /// <summary>
    /// Everything the user sees and taps. It makes no training decisions. It shows
    /// what it is told and announces when a button is pressed.
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
        [SerializeField] private TMP_Text soundToggleLabel;
        [SerializeField] private Button clearButton;
        [SerializeField] private TMP_Text clearLabel;
        [SerializeField] private TMP_Text menuBestText;
        [SerializeField] private TMP_Text rulesText;
        [SerializeField] private TMP_Text savedInfoText;
        [SerializeField] private Button menuButton;
        [SerializeField] private Button[] extinguisherButtons;
        [SerializeField] private Image[] extinguisherButtonImages;

        [Header("Welcome / permission")]
        [SerializeField] private Button startButton;
        [SerializeField] private Button retryButton;
        [SerializeField] private TMP_Text permissionMessage;
        [SerializeField] private TMP_Text busyText;

        [Header("HUD")]
        [SerializeField] private TMP_Text instructionText;
        [SerializeField] private TMP_Text messageText;
        [SerializeField] private TMP_Text timerText;
        [SerializeField] private Button pullPinButton;
        [SerializeField] private HoldButton squeezeButton;
        [SerializeField] private Image squeezeImage;
        [SerializeField] private Button muteButton;
        [SerializeField] private TMP_Text muteLabel;
        [SerializeField] private GameObject reticle;
        [SerializeField] private GameObject actionButtons;

        [Header("Summary")]
        [SerializeField] private TMP_Text summaryTime;
        [SerializeField] private TMP_Text summaryErrors;
        [SerializeField] private TMP_Text summaryScore;
        [SerializeField] private TMP_Text summaryBest;
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
        public event Action<int> ExtinguisherChosen;

        private static readonly Color WarningColor = Theme.Danger;
        private static readonly Color HintColor = Theme.Accent2;
        private static readonly Color FeedbackColor = Theme.Success;

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
            for (int i = 0; i < extinguisherButtons.Length; i++)
            {
                int index = i;
                extinguisherButtons[i].onClick.AddListener(() => ExtinguisherChosen?.Invoke(index));
            }
            squeezeButton.Pressed += () => SqueezeDown?.Invoke();
            squeezeButton.Released += () => SqueezeUp?.Invoke();

            ShowOnly(null);
        }

        private void Update()
        {
            // The Android Back button (Escape key in the editor) leaves How to Play and Settings.
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame &&
                (howToPanel.activeSelf || settingsPanel.activeSelf))
                ShowWelcome();

            if (clearArmedUntil > 0f && Time.unscaledTime > clearArmedUntil) DisarmClear();

            // Warnings and feedback are shown before the hint. The hint stays until it is cleared.
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

        public void SetExtinguisherChoice(int index)
        {
            for (int i = 0; i < extinguisherButtonImages.Length; i++)
            {
                bool on = i == index;
                extinguisherButtonImages[i].color = on ? Theme.Accent : Theme.Surface2;
                TMP_Text label = extinguisherButtons[i].GetComponentInChildren<TMP_Text>();
                label.color = on ? Theme.OnAccent : Theme.TextMuted;
            }
        }

        public void SetBestScore(int best)
        {
            menuBestText.text = best >= 0 ? "BEST SCORE  " + best + " / 100" : "NO COMPLETED TRAINING YET";
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
            // The button still receives presses when it looks "disabled" so that pressing it
            // too early can be counted as a mistake.
            squeezeImage.color = enabled ? Theme.Danger : Theme.Surface2;
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

            summaryTime.text = "TIME  <color=#F4F5F7>" + record.durationSec.ToString("0.0") + " s</color>";
            summaryErrors.text = "ERRORS  <color=#F4F5F7>" + record.errors + "</color>";
            summaryScore.text = record.score + "<size=32%><color=#A9AFBA> / 100</color></size>";
            summaryBest.text = bestScore >= 0 ? "BEST SCORE  " + bestScore : "";
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
