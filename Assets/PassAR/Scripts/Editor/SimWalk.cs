using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.XR.ARFoundation;

namespace PassAR.EditorTools
{
    /// <summary>
    /// Test helper for the editor's AR simulation. When switched on, it moves the pretend phone
    /// by itself so floors are found without someone using the keyboard.
    /// Menu: Glads Trainer > Sim Walk Test (checked = on).
    /// </summary>
    [InitializeOnLoad]
    public static class SimWalk
    {
        private const string Key = "PassAR.SimWalk";
        private static double startTime;
        private static double lastLog;
        private static bool active;
        private static double scanStart = -1;

        static SimWalk()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += s =>
            {
                if (s == PlayModeStateChange.EnteredPlayMode) { startTime = EditorApplication.timeSinceStartup; active = true; }
                if (s == PlayModeStateChange.ExitingPlayMode) active = false;
            };
        }

        [MenuItem("Glads Trainer/Sim Walk Test")]
        private static void Toggle() { EditorPrefs.SetBool(Key, !EditorPrefs.GetBool(Key, false)); }

        [MenuItem("Glads Trainer/Sim Walk Test", true)]
        private static bool ToggleValidate()
        {
            Menu.SetChecked("Glads Trainer/Sim Walk Test", EditorPrefs.GetBool(Key, false));
            return true;
        }

        [MenuItem("Glads Trainer/Capture Game Screenshot")]
        private static void Capture()
        {
            if (!EditorApplication.isPlaying) return;
            string path = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../Temp/shot_" + System.DateTime.Now.ToString("HHmmss") + ".png"));
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log("[SimWalk] screenshot " + path);
        }

        private static void Tick()
        {
            if (!active || !EditorApplication.isPlaying || !EditorPrefs.GetBool(Key, false)) return;
            double t = EditorApplication.timeSinceStartup - startTime;
            Mouse mouse = Mouse.current;
            if (mouse == null) return;

            // Only move around while the scanning screen is showing.
            var ui = Object.FindAnyObjectByType<UIManager>();
            Transform hud = ui != null ? ui.transform.Find("HudPanel") : null;
            bool scanning = hud != null && hud.gameObject.activeSelf;
            Camera cam0 = Camera.main;
            if (scanning && cam0 != null)
            {
                if (scanStart < 0) scanStart = EditorApplication.timeSinceStartup;
                float phase = (float)(EditorApplication.timeSinceStartup - scanStart);
                float pitch = cam0.transform.eulerAngles.x;
                if (pitch > 180f) pitch -= 360f;
                float target = 35f; // look down at the floor
                var ar = Object.FindAnyObjectByType<ARSessionController>();
                bool placed = ar != null && ar.IsPlaced;
                float dx = Mathf.Sin(phase * 1.0f) * 3f;
                if (placed)
                {
                    // Keep the middle circle on the fire so the Aim, Squeeze and Sweep steps can be tested.
                    var hazard = Object.FindAnyObjectByType<FireHazard>();
                    if (hazard != null)
                    {
                        Vector3 aimPoint = hazard.transform.position + Vector3.up * 0.1f;
                        var cols = hazard.GetComponentsInChildren<Collider>();
                        if (cols.Length > 0)
                        {
                            Bounds bb = cols[0].bounds;
                            for (int c = 1; c < cols.Length; c++) bb.Encapsulate(cols[c].bounds);
                            aimPoint = bb.center;
                        }
                        Vector3 to = aimPoint - cam0.transform.position;
                        Vector3 fwdFlat = Vector3.ProjectOnPlane(cam0.transform.forward, Vector3.up);
                        Vector3 toFlat = Vector3.ProjectOnPlane(to, Vector3.up);
                        float yawErr = Vector3.SignedAngle(fwdFlat, toFlat, Vector3.up) - Mathf.Sin(phase * 0.9f) * 9f;
                        dx = Mathf.Clamp(yawErr / 0.573f * 0.25f, -10f, 10f);
                        target = Mathf.Atan2(-to.y, toFlat.magnitude) * Mathf.Rad2Deg;
                    }
                }
                float dy = -Mathf.Clamp((target - pitch) / 0.573f * 0.25f, -10f, 10f);
                InputSystem.QueueDeltaStateEvent(mouse.delta, new Vector2(dx, dy));
            }
            else scanStart = -1;

            if (t - lastLog > 2)
            {
                lastLog = t;
                var pm = Object.FindAnyObjectByType<ARPlaneManager>();
                Camera cam = Camera.main;
                Debug.Log("[SimWalk] t=" + t.ToString("0") + " planes=" + (pm != null ? pm.trackables.count : -1) +
                          " camPitch=" + (cam != null ? cam.transform.eulerAngles.x.ToString("0") : "?") +
                          " camPos=" + (cam != null ? cam.transform.position.ToString("F2") : "?"));
            }
        }
    }
}
