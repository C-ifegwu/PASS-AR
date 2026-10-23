using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.InputSystem.XR;
using UnityEngine.Rendering;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Unity.XR.CoreUtils;

namespace PassAR.EditorTools
{
    /// <summary>
    /// Builds the scene, prefabs, materials and UI from the scripts and the
    /// assets in the project, so nothing has to be wired by hand.
    /// Menu: Glads Trainer > Build Scene and Prefabs.
    /// </summary>
    public static class GladsSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/PassAR.unity";
        private const string ConfigPath = "Assets/PassAR/Config/ScenarioConfig.asset";

        // ------------------------------------------------------------------------
        // Step 2: assets, prefabs and scene
        // ------------------------------------------------------------------------

        [MenuItem("Glads Trainer/Build Scene and Prefabs")]
        public static void BuildAll()
        {
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;   // NFR-06
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel28; // NFR-06: Android 9+

            EnsureFolder("Assets/PassAR/Materials");
            EnsureFolder("Assets/PassAR/Prefabs");
            EnsureFolder("Assets/Config");
            EnsureFolder("Assets/Scenes");

            AssetDatabase.Refresh();
            ImportSprite("Assets/PassAR/Art/Branding/glads_apartment.png");
            ImportSprite("Assets/PassAR/Art/Textures/reticle.png");

            ScenarioConfig config = CreateConfig();
            Texture2D soft = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/PassAR/Art/Textures/soft_particle.png");

            Material flameMat = MakeMaterial("Flame", "Sprites/Default", Color.white, soft);
            Material sprayMat = MakeMaterial("Spray", "Sprites/Default", Color.white, soft);
            Material planeMat = MakeMaterial("ARPlane", "Sprites/Default", new Color(0.91f, 0.50f, 0.23f, 0.30f), null);
            Material binMat = MakeMaterial("Bin", "Universal Render Pipeline/Lit", new Color(0.25f, 0.25f, 0.27f), null);
            Material charMat = MakeMaterial("Scorch", "Universal Render Pipeline/Lit", new Color(0.05f, 0.05f, 0.05f), null);
            Material redMat = MakeMaterial("ExtinguisherRed", "Universal Render Pipeline/Lit", new Color(0.8f, 0.1f, 0.08f), null);
            Material greyMat = MakeMaterial("ExtinguisherGrey", "Universal Render Pipeline/Lit", new Color(0.2f, 0.2f, 0.22f), null);
            Material yellowMat = MakeMaterial("PinYellow", "Universal Render Pipeline/Lit", new Color(1f, 0.85f, 0.1f), null);

            AudioClip fireClip = LoadClip("fire_loop");
            FireHazard hazardPrefab = BuildHazardPrefab(flameMat, binMat, charMat, fireClip);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            // Load the saved asset again so the scene points at the real prefab file.
            hazardPrefab = AssetDatabase.LoadAssetAtPath<FireHazard>("Assets/PassAR/Prefabs/FireBin.prefab");
            if (hazardPrefab == null)
                throw new System.InvalidOperationException("FireBin prefab has no FireHazard component");
            ParticleSystem puffPrefab = BuildPuffPrefab(sprayMat);
            GameObject planePrefab = BuildPlanePrefab(planeMat);

            BuildScene(config, hazardPrefab, puffPrefab, planePrefab, sprayMat, redMat, greyMat, yellowMat);

            AssetDatabase.SaveAssets();
            Debug.Log("Glads Trainer: scene built at " + ScenePath);
        }

        // ---- assets -----------------------------------------------------------------

        private static void EnsureFolder(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                string parent = Path.GetDirectoryName(path).Replace('\\', '/');
                AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
            }
        }

        private static void ImportSprite(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null || importer.textureType == TextureImporterType.Sprite) return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.SaveAndReimport();
        }

        private static ScenarioConfig CreateConfig()
        {
            var existing = AssetDatabase.LoadAssetAtPath<ScenarioConfig>(ConfigPath);
            if (existing != null) return existing;

            var config = ScriptableObject.CreateInstance<ScenarioConfig>();
            AssetDatabase.CreateAsset(config, ConfigPath);
            return config;
        }

        private static Material MakeMaterial(string name, string shaderName, Color color, Texture2D texture)
        {
            string path = "Assets/PassAR/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find(shaderName);
                if (shader == null) throw new System.InvalidOperationException("Shader not found: " + shaderName);
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            else if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (texture != null) material.mainTexture = texture;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static AudioClip LoadClip(string name)
        {
            return AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/PassAR/Audio/" + name + ".wav");
        }

        private static void SetRef(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(field);
            if (prop == null)
            {
                Debug.LogError("Glads Trainer: field '" + field + "' not found on " + target.GetType().Name);
                return;
            }
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
            if (value == null || new SerializedObject(target).FindProperty(field).objectReferenceValue == null)
                Debug.LogWarning("Glads Trainer: '" + field + "' on " + target.GetType().Name + " ended up empty (value " +
                                 (value == null ? "null" : value.GetType().Name) + ")");
        }

        // ---- prefabs ------------------------------------------------------------------

        private static GameObject Primitive(PrimitiveType type, string name, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        private static ParticleSystem BuildFlames(Transform parent, string name, Vector3 position, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);

            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.startLifetime = 0.9f;
            main.startSpeed = 0.7f;
            main.startSize = 0.45f;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 80;

            var emission = ps.emission;
            emission.rateOverTime = 35f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 7f;
            shape.radius = 0.09f;

            var colour = ps.colorOverLifetime;
            colour.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.85f, 0.25f), 0f),
                    new GradientColorKey(new Color(1f, 0.35f, 0.05f), 0.5f),
                    new GradientColorKey(new Color(0.3f, 0.05f, 0.02f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0.9f, 0f),
                    new GradientAlphaKey(0.6f, 0.5f),
                    new GradientAlphaKey(0f, 1f)
                });
            colour.color = gradient;

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.15f));

            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;
            return ps;
        }

        private static FireHazard BuildHazardPrefab(Material flameMat, Material binMat, Material charMat, AudioClip fireClip)
        {
            var root = new GameObject("FireBin");

            Primitive(PrimitiveType.Cylinder, "Bin", root.transform, new Vector3(0f, 0.22f, -0.22f), new Vector3(0.38f, 0.22f, 0.38f), binMat);
            Primitive(PrimitiveType.Cube, "ScorchMark", root.transform, new Vector3(0f, 0.005f, 0f), new Vector3(1.1f, 0.01f, 0.5f), charMat);

            // Three zones in a row so the trainee has to sweep (left, centre, right).
            string[] names = { "FireZone_Left", "FireZone_Centre", "FireZone_Right" };
            float[] xs = { -0.35f, 0f, 0.35f };
            var zones = new FireZone[3];

            for (int i = 0; i < 3; i++)
            {
                var zoneGo = new GameObject(names[i]);
                zoneGo.transform.SetParent(root.transform, false);
                zoneGo.transform.localPosition = new Vector3(xs[i], 0f, 0.05f);

                var box = zoneGo.AddComponent<BoxCollider>();
                box.center = new Vector3(0f, 0.1f, 0f);
                box.size = new Vector3(0.3f, 0.2f, 0.3f);

                ParticleSystem flames = BuildFlames(zoneGo.transform, "Flames", Vector3.zero, flameMat);

                zones[i] = zoneGo.AddComponent<FireZone>();
                SetRef(zones[i], "flames", flames);
            }

            var audioGo = new GameObject("FireAudio");
            audioGo.transform.SetParent(root.transform, false);
            audioGo.transform.localPosition = new Vector3(0f, 0.3f, 0f);
            var source = audioGo.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 0.5f;
            source.maxDistance = 8f;

            var hazard = root.AddComponent<FireHazard>();
            var so = new SerializedObject(hazard);
            SerializedProperty list = so.FindProperty("zones");
            list.arraySize = zones.Length;
            for (int i = 0; i < zones.Length; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = zones[i];
            so.FindProperty("fireSound").objectReferenceValue = fireClip;
            so.FindProperty("fireSource").objectReferenceValue = source;
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, "Assets/PassAR/Prefabs/FireBin.prefab");
            Object.DestroyImmediate(root);
            return saved.GetComponent<FireHazard>();
        }

        private static ParticleSystem BuildPuffPrefab(Material sprayMat)
        {
            var go = new GameObject("SprayPuff");
            var ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = 0.8f;
            main.startSpeed = 0.4f;
            main.startSize = 0.25f;
            main.startColor = new Color(0.9f, 0.95f, 1f, 0.6f);

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)12) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.08f;

            var colour = ps.colorOverLifetime;
            colour.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0.7f, 0f), new GradientAlphaKey(0f, 1f) });
            colour.color = gradient;

            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = sprayMat;

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(go, "Assets/PassAR/Prefabs/SprayPuff.prefab");
            Object.DestroyImmediate(go);
            return saved.GetComponent<ParticleSystem>();
        }

        private static GameObject BuildPlanePrefab(Material planeMat)
        {
            var go = new GameObject("ARPlaneVisual");
            go.AddComponent<MeshFilter>();
            go.AddComponent<MeshRenderer>().sharedMaterial = planeMat;
            go.AddComponent<ARPlane>();
            go.AddComponent<ARPlaneMeshVisualizer>();

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(go, "Assets/PassAR/Prefabs/ARPlaneVisual.prefab");
            Object.DestroyImmediate(go);
            return saved;
        }

        // ---- scene --------------------------------------------------------------------

        private static void BuildScene(ScenarioConfig config, FireHazard hazardPrefab, ParticleSystem puffPrefab,
            GameObject planePrefab, Material sprayMat, Material redMat, Material greyMat, Material yellowMat)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var lightGo = new GameObject("Directional Light");
            var sun = lightGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1f;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // --- AR rig
            var sessionGo = new GameObject("AR Session");
            var session = sessionGo.AddComponent<ARSession>();
            session.enabled = false; // switched on by ARSessionController after the camera permission

            var originGo = new GameObject("XR Origin");
            var origin = originGo.AddComponent<XROrigin>();
            var planeManager = originGo.AddComponent<ARPlaneManager>();
            var raycastManager = originGo.AddComponent<ARRaycastManager>();
            var anchorManager = originGo.AddComponent<ARAnchorManager>();
            planeManager.requestedDetectionMode = PlaneDetectionMode.Horizontal;
            planeManager.planePrefab = planePrefab;

            var offsetGo = new GameObject("Camera Offset");
            offsetGo.transform.SetParent(originGo.transform, false);

            var camGo = new GameObject("AR Camera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(offsetGo.transform, false);
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 20f;
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<ARCameraManager>();
            camGo.AddComponent<ARCameraBackground>();
            var poseDriver = camGo.AddComponent<TrackedPoseDriver>();
            poseDriver.positionInput = new InputActionProperty(new InputAction("Position", InputActionType.Value,
                "<HandheldARInputDevice>/devicePosition", expectedControlType: "Vector3"));
            poseDriver.rotationInput = new InputActionProperty(new InputAction("Rotation", InputActionType.Value,
                "<HandheldARInputDevice>/deviceRotation", expectedControlType: "Quaternion"));
            poseDriver.trackingStateInput = new InputActionProperty(new InputAction("Tracking State", InputActionType.Value,
                "<HandheldARInputDevice>/trackingState", expectedControlType: "Integer"));

            origin.Origin = originGo;
            origin.CameraFloorOffsetObject = offsetGo;
            origin.Camera = cam;

            // --- extinguisher, parented to the camera
            Extinguisher extinguisher = BuildExtinguisher(camGo.transform, cam, puffPrefab, sprayMat, redMat, greyMat, yellowMat);

            // --- systems
            var systems = new GameObject("Systems");

            var dataGo = new GameObject("DataManager");
            dataGo.transform.SetParent(systems.transform, false);
            var data = dataGo.AddComponent<DataManager>();
            SetRef(data, "config", AssetDatabase.LoadAssetAtPath<ScenarioConfig>(ConfigPath));

            var audioGo = new GameObject("AudioManager");
            audioGo.transform.SetParent(systems.transform, false);
            var audioManager = audioGo.AddComponent<AudioManager>();
            WireAudio(audioManager, audioGo);

            var arGo = new GameObject("ARSessionController");
            arGo.transform.SetParent(systems.transform, false);
            var arController = arGo.AddComponent<ARSessionController>();
            SetRef(arController, "session", session);
            SetRef(arController, "planeManager", planeManager);
            SetRef(arController, "raycastManager", raycastManager);
            SetRef(arController, "anchorManager", anchorManager);
            SetRef(arController, "arCamera", cam);
            SetRef(arController, "hazardPrefab", AssetDatabase.LoadAssetAtPath<FireHazard>("Assets/PassAR/Prefabs/FireBin.prefab"));

            // --- UI
            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
            UIManager ui = BuildUI();

            // --- coordinator
            var simGo = new GameObject("SimulationManager");
            simGo.transform.SetParent(systems.transform, false);
            var sim = simGo.AddComponent<SimulationManager>();
            SetRef(sim, "arController", arController);
            SetRef(sim, "ui", ui);
            SetRef(sim, "audioManager", audioManager);
            SetRef(sim, "data", data);
            SetRef(sim, "extinguisher", extinguisher);

            EditorSceneManager.SaveScene(scene, ScenePath);

            // Put our scene first and enabled; keep the project's other scenes, but disabled.
            var list = new System.Collections.Generic.List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(ScenePath, true)
            };
            foreach (EditorBuildSettingsScene existing in EditorBuildSettings.scenes)
            {
                if (existing.path == ScenePath) continue;
                list.Add(new EditorBuildSettingsScene(existing.path, false));
            }
            EditorBuildSettings.scenes = list.ToArray();
        }

        private static Extinguisher BuildExtinguisher(Transform cameraTransform, Camera cam, ParticleSystem puffPrefab,
            Material sprayMat, Material redMat, Material greyMat, Material yellowMat)
        {
            var root = new GameObject("Extinguisher");
            root.transform.SetParent(cameraTransform, false);

            var model = new GameObject("Model");
            model.transform.SetParent(root.transform, false);
            model.transform.localPosition = new Vector3(0.17f, -0.25f, 0.5f);
            model.transform.localRotation = Quaternion.Euler(35f, -12f, 0f);

            Primitive(PrimitiveType.Cylinder, "Body", model.transform, Vector3.zero, new Vector3(0.1f, 0.16f, 0.1f), redMat);
            Primitive(PrimitiveType.Cylinder, "Head", model.transform, new Vector3(0f, 0.18f, 0f), new Vector3(0.05f, 0.03f, 0.05f), greyMat);
            Primitive(PrimitiveType.Cube, "Handle", model.transform, new Vector3(0f, 0.22f, 0.02f), new Vector3(0.02f, 0.02f, 0.1f), greyMat);
            GameObject pin = Primitive(PrimitiveType.Sphere, "SafetyPin", model.transform, new Vector3(0.04f, 0.2f, 0f), new Vector3(0.035f, 0.035f, 0.035f), yellowMat);

            var emitter = new GameObject("SprayEmitter");
            emitter.transform.SetParent(root.transform, false);
            emitter.transform.localPosition = new Vector3(0.1f, -0.12f, 0.4f);

            var ps = emitter.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = 0.6f;
            main.startSpeed = 5f;
            main.startSize = 0.12f;
            main.startColor = new Color(0.9f, 0.95f, 1f, 0.5f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 150;

            var emission = ps.emission;
            emission.rateOverTime = 120f;
            emission.enabled = false;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 5f;
            shape.radius = 0.01f;

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.5f, 1f, 3f));

            emitter.GetComponent<ParticleSystemRenderer>().sharedMaterial = sprayMat;

            var extinguisher = root.AddComponent<Extinguisher>();
            SetRef(extinguisher, "arCamera", cam);
            SetRef(extinguisher, "sprayParticles", ps);
            SetRef(extinguisher, "puffPrefab", puffPrefab);
            SetRef(extinguisher, "pinVisual", pin);
            return extinguisher;
        }

        private static void WireAudio(AudioManager manager, GameObject host)
        {
            var effects = host.AddComponent<AudioSource>();
            effects.playOnAwake = false;
            var loop = host.AddComponent<AudioSource>();
            loop.playOnAwake = false;

            var so = new SerializedObject(manager);
            so.FindProperty("effectsSource").objectReferenceValue = effects;
            so.FindProperty("loopSource").objectReferenceValue = loop;

            SoundId[] ids = { SoundId.Spray, SoundId.Pin, SoundId.Success, SoundId.Error };
            string[] files = { "spray_loop", "pin_pull", "success", "error" };
            SerializedProperty sounds = so.FindProperty("sounds");
            sounds.arraySize = ids.Length;
            for (int i = 0; i < ids.Length; i++)
            {
                SerializedProperty entry = sounds.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("id").enumValueIndex = (int)ids[i];
                entry.FindPropertyRelative("clip").objectReferenceValue = LoadClip(files[i]);
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---- UI -------------------------------------------------------------------------
        // Reference resolution is 1080 x 1920. At 3x phone density 48 px is 16 sp and
        // 144 px is 48 dp, which is where the text and button sizes below come from (NFR-04).

        private static Font font;

        // Palette taken from the Glads Apartment photo: charcoal concrete, orange walls,
        // blue balconies, red roof, white trim. Text colours keep a 4.5:1 contrast (NFR-07).
        private static readonly Color Charcoal = new Color(0.18f, 0.18f, 0.19f, 1f);   // #2E2E30
        private static readonly Color Orange = new Color(0.91f, 0.50f, 0.23f, 1f);     // #E8803A
        private static readonly Color OrangeLight = new Color(1f, 0.72f, 0.42f, 1f);   // #FFB86B
        private static readonly Color Blue = new Color(0.17f, 0.34f, 0.60f, 1f);       // #2B5799
        private static readonly Color Red = new Color(0.70f, 0.15f, 0.12f, 1f);        // #B3261E
        private static readonly Color Ink = new Color(0.12f, 0.12f, 0.13f, 1f);        // dark text on orange

        private static UIManager BuildUI()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var canvasGo = new GameObject("UI");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            var ui = canvasGo.AddComponent<UIManager>();
            Transform root = canvasGo.transform;

            var dark = Charcoal;
            var shade = new Color(0.18f, 0.18f, 0.19f, 0.88f);

            // Main menu with the Glads Apartment photo
            var grey = new Color(0.30f, 0.30f, 0.32f, 1f);
            RectTransform welcome = Panel(root, "WelcomePanel", dark, Vector2.zero, Vector2.one);
            var photo = Panel(welcome, "GladsPhoto", Color.white, new Vector2(0.05f, 0.72f), new Vector2(0.95f, 0.96f)).GetComponent<Image>();
            photo.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/PassAR/Art/Branding/glads_apartment.png");
            photo.preserveAspect = true;
            Text menuTitle = Label(welcome, "Title", "Glads Apartment", 88, FontStyle.Bold, new Vector2(0f, 0.63f), new Vector2(1f, 0.71f), TextAnchor.MiddleCenter);
            menuTitle.color = OrangeLight;
            Label(welcome, "Subtitle", "Resident Fire Safety Training", 56, FontStyle.Normal, new Vector2(0f, 0.58f), new Vector2(1f, 0.63f), TextAnchor.MiddleCenter);
            Text menuBest = Label(welcome, "BestText", "", 48, FontStyle.Normal, new Vector2(0.05f, 0.535f), new Vector2(0.95f, 0.58f), TextAnchor.MiddleCenter);
            Button startButton = MakeButton(welcome, "StartButton", "START TRAINING", 56, Orange,
                new Vector2(0.15f, 0.435f), new Vector2(0.85f, 0.525f), Ink);
            Button howToButton = MakeButton(welcome, "HowToButton", "HOW TO PLAY", 52, Blue,
                new Vector2(0.15f, 0.335f), new Vector2(0.85f, 0.425f));
            Button settingsButton = MakeButton(welcome, "SettingsButton", "SETTINGS", 52, Blue,
                new Vector2(0.15f, 0.235f), new Vector2(0.85f, 0.325f));
            Button quitButton = MakeButton(welcome, "QuitButton", "QUIT", 52, grey,
                new Vector2(0.15f, 0.135f), new Vector2(0.85f, 0.225f));
            Text busyText = Label(welcome, "BusyText", "", 48, FontStyle.Normal, new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.125f), TextAnchor.MiddleCenter);

            // How to play
            RectTransform howTo = Panel(root, "HowToPanel", dark, Vector2.zero, Vector2.one);
            Label(howTo, "Title", "How to play", 80, FontStyle.Bold, new Vector2(0f, 0.86f), new Vector2(1f, 0.96f), TextAnchor.MiddleCenter).color = OrangeLight;
            Label(howTo, "Body",
                "Put out the virtual fire with the PASS method.\n\n" +
                "P - PULL the safety pin (press PULL PIN)\n\n" +
                "A - AIM at the BASE of the fire from 1 to 3 metres away\n\n" +
                "S - SQUEEZE and hold the SQUEEZE button\n\n" +
                "S - SWEEP side to side until all three flames are out\n\n" +
                "Each mistake costs 10 points.",
                48, FontStyle.Normal, new Vector2(0.08f, 0.22f), new Vector2(0.92f, 0.85f), TextAnchor.UpperLeft);
            Button howToBack = MakeButton(howTo, "BackButton", "BACK", 56, Blue,
                new Vector2(0.15f, 0.06f), new Vector2(0.85f, 0.15f));

            // Settings
            RectTransform settings = Panel(root, "SettingsPanel", dark, Vector2.zero, Vector2.one);
            Label(settings, "Title", "Settings", 80, FontStyle.Bold, new Vector2(0f, 0.89f), new Vector2(1f, 0.97f), TextAnchor.MiddleCenter).color = OrangeLight;
            Button soundToggle = MakeButton(settings, "SoundToggle", "Sound: ON", 56, Blue,
                new Vector2(0.15f, 0.77f), new Vector2(0.85f, 0.86f));
            Text soundToggleLabel = soundToggle.GetComponentInChildren<Text>();
            Text rules = Label(settings, "Rules", "", 44, FontStyle.Normal, new Vector2(0.08f, 0.54f), new Vector2(0.92f, 0.75f), TextAnchor.UpperLeft);
            Text savedInfo = Label(settings, "SavedInfo", "", 44, FontStyle.Normal, new Vector2(0.08f, 0.46f), new Vector2(0.92f, 0.53f), TextAnchor.MiddleLeft);
            Button clear = MakeButton(settings, "ClearButton", "CLEAR SAVED RESULTS", 48, Red,
                new Vector2(0.15f, 0.35f), new Vector2(0.85f, 0.44f));
            Text clearLabel = clear.GetComponentInChildren<Text>();
            Label(settings, "About",
                "Glads Fire Safety Trainer\n" +
                "The camera is used only to show AR. No pictures are saved or sent. The app works offline.",
                44, FontStyle.Normal, new Vector2(0.08f, 0.16f), new Vector2(0.92f, 0.33f), TextAnchor.UpperLeft);
            Button settingsBack = MakeButton(settings, "BackButton", "BACK", 56, Blue,
                new Vector2(0.15f, 0.04f), new Vector2(0.85f, 0.13f));

            // Camera permission / AR support problem
            RectTransform permission = Panel(root, "PermissionPanel", dark, Vector2.zero, Vector2.one);
            Text permissionMessage = Label(permission, "Message", "", 52, FontStyle.Normal, new Vector2(0.08f, 0.45f), new Vector2(0.92f, 0.7f), TextAnchor.MiddleCenter);
            Button retryButton = MakeButton(permission, "RetryButton", "TRY AGAIN", 56, Blue,
                new Vector2(0.2f, 0.3f), new Vector2(0.8f, 0.4f));

            // In-training HUD
            RectTransform hud = Panel(root, "HudPanel", Color.clear, Vector2.zero, Vector2.one);
            hud.GetComponent<Image>().raycastTarget = false;

            RectTransform instructionBar = Panel(hud, "InstructionBar", shade, new Vector2(0f, 1f), new Vector2(1f, 1f));
            instructionBar.pivot = new Vector2(0.5f, 1f);
            instructionBar.sizeDelta = new Vector2(-60f, 270f);
            instructionBar.anchoredPosition = new Vector2(0f, -40f);
            instructionBar.GetComponent<Image>().raycastTarget = false;
            Text instruction = Label(instructionBar, "InstructionText", "", 52, FontStyle.Bold, Vector2.zero, Vector2.one, TextAnchor.MiddleLeft);
            instruction.rectTransform.offsetMin = new Vector2(24f, 10f);
            instruction.rectTransform.offsetMax = new Vector2(-24f, -10f);

            Button muteButton = MakeButton(hud, "MuteButton", "Sound: ON", 48, new Color(0.30f, 0.30f, 0.32f, 0.95f),
                new Vector2(0f, 1f), new Vector2(0f, 1f));
            PlaceCorner(muteButton.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(40f, -330f), new Vector2(420f, 150f));
            Text muteLabel = muteButton.GetComponentInChildren<Text>();

            Text timer = Label(hud, "TimerText", "", 52, FontStyle.Bold, new Vector2(1f, 1f), new Vector2(1f, 1f), TextAnchor.MiddleRight);
            PlaceCorner(timer.rectTransform, new Vector2(1f, 1f), new Vector2(-40f, -330f), new Vector2(300f, 150f));
            AddOutline(timer);

            GameObject reticle = new GameObject("Reticle", typeof(RectTransform), typeof(Image));
            reticle.transform.SetParent(hud, false);
            var reticleRect = reticle.GetComponent<RectTransform>();
            reticleRect.anchorMin = reticleRect.anchorMax = new Vector2(0.5f, 0.5f);
            reticleRect.sizeDelta = new Vector2(140f, 140f);
            var reticleImage = reticle.GetComponent<Image>();
            reticleImage.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/PassAR/Art/Textures/reticle.png");
            reticleImage.color = new Color(1f, 1f, 1f, 0.9f);
            reticleImage.raycastTarget = false;

            Text message = Label(hud, "MessageText", "", 52, FontStyle.Bold, new Vector2(0f, 0f), new Vector2(1f, 0f), TextAnchor.MiddleCenter);
            PlaceCorner(message.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 330f), new Vector2(1000f, 130f));
            AddOutline(message);

            RectTransform actions = Panel(hud, "ActionButtons", Color.clear, Vector2.zero, Vector2.one);
            actions.GetComponent<Image>().raycastTarget = false;

            Button pullPin = MakeButton(actions, "PullPinButton", "PULL PIN", 56, Blue, Vector2.zero, Vector2.zero);
            PlaceCorner(pullPin.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(50f, 80f), new Vector2(440f, 200f));

            GameObject squeeze = new GameObject("SqueezeButton", typeof(RectTransform), typeof(Image), typeof(HoldButton));
            squeeze.transform.SetParent(actions, false);
            PlaceCorner(squeeze.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(-50f, 80f), new Vector2(440f, 200f));
            var squeezeImage = squeeze.GetComponent<Image>();
            squeezeImage.color = new Color(0.35f, 0.35f, 0.37f);
            Label(squeeze.transform, "Label", "SQUEEZE (hold)", 52, FontStyle.Bold, Vector2.zero, Vector2.one, TextAnchor.MiddleCenter).raycastTarget = false;

            RectTransform lost = Panel(hud, "TrackingLostBanner", Red, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f));
            lost.sizeDelta = new Vector2(0f, 230f);
            lost.GetComponent<Image>().raycastTarget = false;
            Label(lost, "Text", "Tracking lost - move the phone slowly", 52, FontStyle.Bold, Vector2.zero, Vector2.one, TextAnchor.MiddleCenter).raycastTarget = false;
            lost.gameObject.SetActive(false);

            // Summary card
            RectTransform summary = Panel(root, "SummaryPanel", new Color(0.18f, 0.18f, 0.19f, 0.96f), new Vector2(0.06f, 0.20f), new Vector2(0.94f, 0.80f));
            Label(summary, "Title", "Training complete", 68, FontStyle.Bold, new Vector2(0f, 0.85f), new Vector2(1f, 0.98f), TextAnchor.MiddleCenter).color = OrangeLight;
            Text sumTime = Label(summary, "Time", "", 56, FontStyle.Normal, new Vector2(0.08f, 0.74f), new Vector2(0.92f, 0.85f), TextAnchor.MiddleCenter);
            Text sumErrors = Label(summary, "Errors", "", 56, FontStyle.Normal, new Vector2(0.08f, 0.64f), new Vector2(0.92f, 0.74f), TextAnchor.MiddleCenter);
            Text sumScore = Label(summary, "Score", "", 68, FontStyle.Bold, new Vector2(0.08f, 0.52f), new Vector2(0.92f, 0.64f), TextAnchor.MiddleCenter);
            Text sumBest = Label(summary, "Best", "", 52, FontStyle.Normal, new Vector2(0.08f, 0.43f), new Vector2(0.92f, 0.52f), TextAnchor.MiddleCenter);
            Button restart = MakeButton(summary, "RestartButton", "TRY AGAIN", 60, Orange,
                new Vector2(0.15f, 0.23f), new Vector2(0.85f, 0.39f), Ink);
            Button menu = MakeButton(summary, "MenuButton", "MAIN MENU", 52, Blue,
                new Vector2(0.15f, 0.05f), new Vector2(0.85f, 0.19f));

            // Hook everything up
            var so = new SerializedObject(ui);
            void Ref(string field, Object value) => so.FindProperty(field).objectReferenceValue = value;
            Ref("welcomePanel", welcome.gameObject);
            Ref("permissionPanel", permission.gameObject);
            Ref("hudPanel", hud.gameObject);
            Ref("summaryPanel", summary.gameObject);
            Ref("trackingLostBanner", lost.gameObject);
            Ref("startButton", startButton);
            Ref("retryButton", retryButton);
            Ref("permissionMessage", permissionMessage);
            Ref("busyText", busyText);
            Ref("instructionText", instruction);
            Ref("messageText", message);
            Ref("timerText", timer);
            Ref("pullPinButton", pullPin);
            Ref("squeezeButton", squeeze.GetComponent<HoldButton>());
            Ref("squeezeImage", squeezeImage);
            Ref("muteButton", muteButton);
            Ref("muteLabel", muteLabel);
            Ref("reticle", reticle);
            Ref("actionButtons", actions.gameObject);
            Ref("summaryTime", sumTime);
            Ref("summaryErrors", sumErrors);
            Ref("summaryScore", sumScore);
            Ref("summaryBest", sumBest);
            Ref("restartButton", restart);
            Ref("howToPanel", howTo.gameObject);
            Ref("settingsPanel", settings.gameObject);
            Ref("howToButton", howToButton);
            Ref("settingsButton", settingsButton);
            Ref("quitButton", quitButton);
            Ref("howToBackButton", howToBack);
            Ref("settingsBackButton", settingsBack);
            Ref("soundToggleButton", soundToggle);
            Ref("soundToggleLabel", soundToggleLabel);
            Ref("clearButton", clear);
            Ref("clearLabel", clearLabel);
            Ref("menuBestText", menuBest);
            Ref("rulesText", rules);
            Ref("savedInfoText", savedInfo);
            Ref("menuButton", menu);
            so.ApplyModifiedPropertiesWithoutUndo();

            return ui;
        }

        private static RectTransform Panel(Transform parent, string name, Color colour, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            go.GetComponent<Image>().color = colour;
            return rect;
        }

        private static Text Label(Transform parent, string name, string text, int size, FontStyle style,
            Vector2 anchorMin, Vector2 anchorMax, TextAnchor alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = rect.offsetMax = Vector2.zero;

            var label = go.GetComponent<Text>();
            label.font = font;
            label.fontSize = size;
            label.fontStyle = style;
            label.alignment = alignment;
            label.color = Color.white;
            label.text = text;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.raycastTarget = false;
            return label;
        }

        private static Button MakeButton(Transform parent, string name, string label, int size, Color colour,
            Vector2 anchorMin, Vector2 anchorMax, Color? textColour = null)
        {
            RectTransform rect = Panel(parent, name, colour, anchorMin, anchorMax);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            Text text = Label(rect, "Label", label, size, FontStyle.Bold, Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);
            if (textColour.HasValue) text.color = textColour.Value;
            return button;
        }

        private static void PlaceCorner(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void AddOutline(Text text)
        {
            var outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(3f, -3f);
        }
    }
}
