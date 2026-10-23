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
using UnityEngine.XR.Management;

namespace PassAR
{
    /// <summary>
    /// The only script that talks to the AR tools. It asks for camera permission,
    /// finds the floor, places the fire where you tap and keeps it fixed there,
    /// and tells the rest of the app when something changes.
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
        public event Action PlacementMissed;

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

        // ---- camera permission and start --------------------------

        public void BeginSession()
        {
            if (sessionStarted)
            {
                CameraPermissionResult?.Invoke(true);
                return;
            }
            StartCoroutine(StartRoutine());
        }

        /// <summary>
        /// True when AR is running. On a phone that is ARCore. In the Unity editor it is the
        /// built-in AR simulation, so the same floor and placement code can be tested on a PC.
        /// </summary>
        private static bool HasAr
        {
            get
            {
#if UNITY_EDITOR
                var manager = XRGeneralSettings.Instance != null ? XRGeneralSettings.Instance.Manager : null;
                return manager != null && manager.activeLoader != null;
#else
                return true;
#endif
            }
        }

        private IEnumerator StartRoutine()
        {
            if (!HasAr)
            {
                // In the editor with no AR simulation: go straight to the scanning screen so the screens can be tested.
                sessionStarted = true;
                CameraPermissionResult?.Invoke(true);
                yield break;
            }
#if UNITY_ANDROID && !UNITY_EDITOR
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
        }

        // ---- tracking -------------------------------------------------------

        private void OnSessionStateChanged(ARSessionStateChangedEventArgs args)
        {
            bool tracking = args.state == ARSessionState.SessionTracking;
            if (tracking == wasTracking) return;

            // Do not report "lost" before tracking has ever started.
            if (!tracking && !wasTracking) return;

            wasTracking = tracking;
            TrackingChanged?.Invoke(tracking);
        }

        // ---- floors ---------------------------------------------------------

        public void SetPlanesVisible(bool visible)
        {
            planeManager.enabled = visible;
            planeManager.SetTrackablesActive(visible);
        }

        public void SetPlacementEnabled(bool enabled)
        {
            placementEnabled = enabled;
        }

        // ---- placing the fire -------------------------------------------------

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
        /// Places the fire where you tapped, if you tapped a floor the phone has found.
        /// Taps anywhere else are ignored.
        /// </summary>
        public bool TryPlace(Vector2 screenPoint)
        {
            if (isPlaced) return false;

#if UNITY_EDITOR
            if (!HasAr)
            {
                // No AR in the editor: put the fire two metres in front of the camera so the screens can be tested.
                Vector3 forward = Vector3.ProjectOnPlane(arCamera.transform.forward, Vector3.up).normalized;
                Vector3 spot = arCamera.transform.position + forward * 2f;
                spot.y = arCamera.transform.position.y - 1.4f;
                Spawn(new Pose(spot, Quaternion.identity), null);
                return true;
            }
#endif
            // Try the exact tap spot first, then any floor along the tap direction, then the middle of the screen.
            Vector2 centre = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            if (TryFloorHit(screenPoint, TrackableType.PlaneWithinPolygon, out ARRaycastHit hit, out ARPlane plane) ||
                TryFloorHit(screenPoint, TrackableType.PlaneWithinInfinity, out hit, out plane) ||
                TryFloorHit(centre, TrackableType.PlaneWithinInfinity, out hit, out plane))
            {
                Spawn(hit.pose, plane);
                return true;
            }

            PlacementMissed?.Invoke();
            return false;
        }

        private bool TryFloorHit(Vector2 point, TrackableType types, out ARRaycastHit result, out ARPlane plane)
        {
            result = default;
            plane = null;
            if (!raycastManager.Raycast(point, Hits, types)) return false;

            foreach (ARRaycastHit hit in Hits)
            {
                ARPlane candidate = planeManager.GetPlane(hit.trackableId);
                if (candidate == null || candidate.alignment != PlaneAlignment.HorizontalUp) continue;
                result = hit;
                plane = candidate;
                return true;
            }
            return false;
        }

        private void Spawn(Pose pose, ARPlane plane)
        {
            // Turn the fire to face the player so left, centre and right are the right way round.
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
