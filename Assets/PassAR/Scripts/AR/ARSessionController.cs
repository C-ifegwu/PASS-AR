using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

namespace PassAR
{
    /// <summary>
    /// The only class that talks to AR Foundation. Handles camera permission,
    /// plane detection, tap-to-place, anchoring and tracking state, and tells
    /// the rest of the app about it through events.
    /// </summary>
    public class ARSessionController : MonoBehaviour
    {
        [Header("AR Foundation")]
        [SerializeField] private ARSession session;
        [SerializeField] private ARPlaneManager planeManager;
        [SerializeField] private ARRaycastManager raycastManager;
        [SerializeField] private ARAnchorManager anchorManager;
        [SerializeField] private Camera arCamera;

        [Header("Scenario")]
        [SerializeField] private FireHazard hazardPrefab;

        public event Action<bool> CameraPermissionResult;
        public event Action<string> SessionFailed;
        public event Action<FireHazard> ScenarioPlaced;
        public event Action<bool> TrackingChanged;

        private static readonly List<ARRaycastHit> Hits = new List<ARRaycastHit>();

        private bool placementEnabled;
        private bool isPlaced;
        private bool sessionStarted;
        private bool wasTracking;
        private GameObject anchorObject;

        public bool IsPlaced => isPlaced;

        private void OnEnable()
        {
            ARSession.stateChanged += OnSessionStateChanged;
        }

        private void OnDisable()
        {
            ARSession.stateChanged -= OnSessionStateChanged;
        }

        // ---- camera permission and session start (FR-01) --------------------------

        public void BeginSession()
        {
            if (sessionStarted)
            {
                CameraPermissionResult?.Invoke(true);
                return;
            }
            StartCoroutine(StartRoutine());
        }

        private IEnumerator StartRoutine()
        {
#if UNITY_EDITOR
            // No camera or ARCore in the editor: go straight to the scanning screen for UI testing.
            sessionStarted = true;
            CameraPermissionResult?.Invoke(true);
            yield break;
#elif UNITY_ANDROID
            if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
            {
                bool answered = false;
                bool granted = false;

                var callbacks = new PermissionCallbacks();
                callbacks.PermissionGranted += _ => { answered = true; granted = true; };
                callbacks.PermissionDenied += _ => { answered = true; };
                callbacks.PermissionDeniedAndDontAskAgain += _ => { answered = true; };
                Permission.RequestUserPermission(Permission.Camera, callbacks);

                while (!answered) yield return null;
                if (!granted)
                {
                    CameraPermissionResult?.Invoke(false);
                    yield break;
                }
            }
#endif
#if !UNITY_EDITOR
            if (ARSession.state == ARSessionState.None || ARSession.state == ARSessionState.CheckingAvailability)
                yield return ARSession.CheckAvailability();

            if (ARSession.state == ARSessionState.NeedsInstall)
                yield return ARSession.Install();

            if (ARSession.state == ARSessionState.Unsupported)
            {
                SessionFailed?.Invoke("This phone does not support AR (Google Play Services for AR is required).");
                yield break;
            }

            session.enabled = true;
            sessionStarted = true;
            CameraPermissionResult?.Invoke(true);
#endif
        }

        // ---- tracking (FR-19) -------------------------------------------------------

        private void OnSessionStateChanged(ARSessionStateChangedEventArgs args)
        {
            bool tracking = args.state == ARSessionState.SessionTracking;
            if (tracking == wasTracking) return;

            // Do not report "lost" before tracking has ever started.
            if (!tracking && !wasTracking) return;

            wasTracking = tracking;
            TrackingChanged?.Invoke(tracking);
        }

        // ---- planes (FR-02) ---------------------------------------------------------

        public void SetPlanesVisible(bool visible)
        {
            planeManager.enabled = visible;
            planeManager.SetTrackablesActive(visible);
        }

        public void SetPlacementEnabled(bool enabled)
        {
            placementEnabled = enabled;
        }

        // ---- placement (FR-03, FR-04) -------------------------------------------------

        private void Update()
        {
            if (!placementEnabled || isPlaced) return;

            if (TryGetTapPosition(out Vector2 screenPoint) && !IsOverUI())
                TryPlace(screenPoint);
        }

        private static bool TryGetTapPosition(out Vector2 position)
        {
            Touchscreen screen = Touchscreen.current;
            if (screen != null && screen.primaryTouch.press.wasPressedThisFrame)
            {
                position = screen.primaryTouch.position.ReadValue();
                return true;
            }
#if UNITY_EDITOR
            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                position = mouse.position.ReadValue();
                return true;
            }
#endif
            position = default;
            return false;
        }

        private static bool IsOverUI()
        {
            if (EventSystem.current == null) return false;

            Touchscreen screen = Touchscreen.current;
            if (screen != null && screen.primaryTouch.press.wasPressedThisFrame)
                return EventSystem.current.IsPointerOverGameObject(screen.primaryTouch.touchId.ReadValue());

            return EventSystem.current.IsPointerOverGameObject();
        }

        /// <summary>
        /// Places the scenario at a screen point if it lands on a detected
        /// horizontal plane. Taps anywhere else are ignored.
        /// </summary>
        public bool TryPlace(Vector2 screenPoint)
        {
            if (isPlaced) return false;

#if UNITY_EDITOR
            // No AR in the editor: drop the fire two metres in front of the camera for UI testing.
            if (!raycastManager.Raycast(screenPoint, Hits, TrackableType.PlaneWithinPolygon))
            {
                Vector3 forward = Vector3.ProjectOnPlane(arCamera.transform.forward, Vector3.up).normalized;
                Vector3 spot = arCamera.transform.position + forward * 2f;
                spot.y = arCamera.transform.position.y - 1.4f;
                Spawn(new Pose(spot, Quaternion.identity), null);
                return true;
            }
#endif
            if (!raycastManager.Raycast(screenPoint, Hits, TrackableType.PlaneWithinPolygon)) return false;

            ARRaycastHit hit = Hits[0];
            ARPlane plane = planeManager.GetPlane(hit.trackableId);
            if (plane == null || plane.alignment != PlaneAlignment.HorizontalUp) return false;

            Spawn(hit.pose, plane);
            return true;
        }

        private void Spawn(Pose pose, ARPlane plane)
        {
            // Face the row of fire zones towards the player so left/centre/right read correctly.
            float yaw = arCamera.transform.eulerAngles.y;
            Pose placed = new Pose(pose.position, Quaternion.Euler(0f, yaw, 0f));

            ARAnchor anchor = plane != null ? anchorManager.AttachAnchor(plane, placed) : null;
            anchorObject = anchor != null ? anchor.gameObject : new GameObject("Scenario Anchor");
            if (anchor == null) anchorObject.transform.SetPositionAndRotation(placed.position, placed.rotation);

            FireHazard hazard = Instantiate(hazardPrefab, anchorObject.transform);
            hazard.transform.localPosition = Vector3.zero;
            hazard.transform.localRotation = Quaternion.identity;

            isPlaced = true;
            placementEnabled = false;
            ScenarioPlaced?.Invoke(hazard);
        }

        public void ClearScenario()
        {
            if (anchorObject != null) Destroy(anchorObject);
            anchorObject = null;
            isPlaced = false;
            placementEnabled = true;
        }
    }
}
