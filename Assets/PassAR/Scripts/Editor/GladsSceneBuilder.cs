using System.IO;
using TMPro;
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
    /// Builds the scene, the fire and furniture, the materials and the screens from the
    /// project files, so nothing has to be connected by hand.
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
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;   // phone held upright
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel28; // Android 9 or newer

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
            Material redMat = MakeMaterial("ExtinguisherRed", "Universal Render Pipeline/Lit", new Color(0.8f, 0.1f, 0.08f), null);
            Material greyMat = MakeMaterial("ExtinguisherGrey", "Universal Render Pipeline/Lit", new Color(0.2f, 0.2f, 0.22f), null);
            Material yellowMat = MakeMaterial("PinYellow", "Universal Render Pipeline/Lit", new Color(1f, 0.85f, 0.1f), null);

            AudioClip fireClip = LoadClip("fire_loop");
            FireHazard hazardPrefab = BuildHazardPrefab(flameMat, fireClip);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            // Load the saved file again so the scene uses the real saved copy.
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

        private const string FlamePrefab = "Assets/Vefects/Free Fire VFX URP/Particles/VFX_Fire_01_Medium_Simple.prefab";

        private static Transform BuildFlames(Transform parent, string name, Vector3 position)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FlamePrefab);
            if (prefab == null) throw new System.Exception("Fire VFX prefab missing: " + FlamePrefab);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = name;
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one * 0.5f;
            // Keep the fire light on the phone: no extra lights.
            foreach (Light l in go.GetComponentsInChildren<Light>(true)) Object.DestroyImmediate(l.gameObject);
            return go.transform;
        }

        private static FireHazard BuildHazardPrefab(Material flameMat, AudioClip fireClip)
        {
            var root = new GameObject("FireBin");

            // A small living room. The trainee stands at the front looking forward, so the
            // table with the fire is in front, the sofa is behind it, and a nightstand and shelf are at the ends.
            const float tableTop = 0.64f; // height of the table top in the furniture pack
            BuildFurniture(root.transform);

            // Three fire zones in a row so the trainee has to sweep (left, centre, right).
            string[] names = { "FireZone_Left", "FireZone_Centre", "FireZone_Right" };
            float[] xs = { -0.35f, 0f, 0.35f };
            var zones = new FireZone[3];

            for (int i = 0; i < 3; i++)
            {
                var zoneGo = new GameObject(names[i]);
                zoneGo.transform.SetParent(root.transform, false);
                zoneGo.transform.localPosition = new Vector3(xs[i], tableTop, 0f);

                var box = zoneGo.AddComponent<BoxCollider>();
                box.center = new Vector3(0f, 0.1f, 0f);
                box.size = new Vector3(0.3f, 0.2f, 0.3f);

                Transform flames = BuildFlames(zoneGo.transform, "Flames", Vector3.zero);

                zones[i] = zoneGo.AddComponent<FireZone>();
                SetRef(zones[i], "flameRoot", flames);
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

        private const string FurnitureDir = "Assets/WoodInterior/prefabs/";

        private static Material furnitureMat;

        // The furniture pack's own material showed coloured dots in the editor, so the furniture
        // gets a simple material made only from its colour picture.
        private static Material FurnitureMaterial()
        {
            if (furnitureMat != null) return furnitureMat;
            furnitureMat = MakeLit("FurnitureWood",
                "Assets/WoodInterior/textures/wood_cabin_props_WoodCabinProps_AlbedoTransparency.png",
                null, // no extra surface-detail picture (it caused sparkle in the editor)
                
                null, null);
            furnitureMat.SetFloat("_Smoothness", 0.3f);
            EditorUtility.SetDirty(furnitureMat);
            return furnitureMat;
        }

        private static GameObject Furniture(string prefabName, string label, Transform parent, Vector3 position, float yaw)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FurnitureDir + prefabName + ".prefab");
            if (prefab == null) throw new System.Exception("Furniture prefab missing: " + FurnitureDir + prefabName);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = label;
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            // Only the fire zones can be aimed at. The furniture must never block the spray.
            foreach (Collider c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
            {
                var mats = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = FurnitureMaterial();
                r.sharedMaterials = mats;
            }
            return go;
        }

        private static void BuildFurniture(Transform root)
        {
            furnitureMat = null;
            var set = new GameObject("Furniture");
            set.transform.SetParent(root, false);
            Transform t = set.transform;

            // The table under the fire.
            Furniture("wood_props_table", "Table", t, new Vector3(0f, 0f, 0f), 0f);

            // The sofa behind the table, facing the trainee, with its seat cushion.
            var sofa = new GameObject("Sofa");
            sofa.transform.SetParent(t, false);
            sofa.transform.localPosition = new Vector3(0f, 0f, 0.95f);
            sofa.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            Furniture("wood_props_couch", "SofaFrame", sofa.transform, Vector3.zero, 0f);
            Furniture("wood_props_couch_cussion", "SofaCushion", sofa.transform, new Vector3(-0.011f, 0.363f, 0.026f), 0f);

            // The nightstand at the left end of the sofa.
            Furniture("wood_props_night_stand", "NightStand", t, new Vector3(-1.3f, 0f, 0.95f), 180f);

            // The shelf at the right end of the sofa.
            Furniture("wood_props_shelf", "Shelf", t, new Vector3(1.35f, 0f, 0.95f), 180f);
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

            // A new empty scene has no sky, so the room light was undefined and lit objects showed
            // coloured dots. A plain, even room light fixes this and suits the camera view.
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.60f, 0.58f, 0.56f, 1f);
            RenderSettings.defaultReflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Custom;
            RenderSettings.reflectionIntensity = 0f;

            // --- AR camera and tracking
            var sessionGo = new GameObject("AR Session");
            var session = sessionGo.AddComponent<ARSession>();
            // Lets the phone's movement move the camera. Without it the camera never moves
            // and everything stays stuck to the screen.
            sessionGo.AddComponent<ARInputManager>();
            session.enabled = false; // switched on later, after camera permission is given

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

            // --- the extinguisher, held on the camera
            Extinguisher extinguisher = BuildExtinguisher(camGo.transform, cam, puffPrefab, sprayMat, yellowMat);

            // --- the helper scripts
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

            // --- the boss script
            var simGo = new GameObject("SimulationManager");
            simGo.transform.SetParent(systems.transform, false);
            var sim = simGo.AddComponent<SimulationManager>();
            SetRef(sim, "arController", arController);
            SetRef(sim, "ui", ui);
            SetRef(sim, "audioManager", audioManager);
            SetRef(sim, "data", data);
            SetRef(sim, "extinguisher", extinguisher);

            EditorSceneManager.SaveScene(scene, ScenePath);

            // Put this scene first and switched on. Other scenes in the project are kept but switched off.
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

        private const string AkPrefab = "Assets/AK STUDIO ART/Fire Extinguisher/Prefabs/Fire Extinguisher.prefab";
        private const string OldPrefab = "Assets/OldExtinguisher/Model/OldExtinguisher.prefab";

        private static Texture2D Tex(string path)
        {
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static void MarkNormalMap(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null || importer.textureType == TextureImporterType.NormalMap) return;
            importer.textureType = TextureImporterType.NormalMap;
            importer.SaveAndReimport();
        }

        // The downloaded models use old materials that look pink in this project, and the normal
        // material showed coloured dots in the editor. A simple material works cleanly and is lighter
        // on a phone, so every model gets one made from its own colour picture.
        // The other pictures (detail, shine and shadow) are deliberately not used.
        private static Material MakeLit(string name, string albedo, string normal, string metalSmooth, string ao)
        {
            string path = "Assets/PassAR/Materials/" + name + ".mat";
            Shader shader = Shader.Find("Universal Render Pipeline/Simple Lit");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            mat.SetColor("_BaseColor", Color.white);
            mat.SetTexture("_BaseMap", Tex(albedo));
            mat.SetTexture("_BumpMap", null);
            mat.DisableKeyword("_NORMALMAP");
            mat.SetColor("_SpecColor", new Color(0.2f, 0.2f, 0.2f, 1f));
            mat.SetFloat("_Smoothness", 0.4f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Extinguisher BuildExtinguisher(Transform cameraTransform, Camera cam, ParticleSystem puffPrefab,
            Material sprayMat, Material yellowMat)
        {
            var root = new GameObject("Extinguisher");
            root.transform.SetParent(cameraTransform, false);

            Material akMat = MakeLit("ExtinguisherModern",
                "Assets/AK STUDIO ART/Fire Extinguisher/Models/Fire Extinguisher/Fire Extinguisher Albedo.png",
                null, // no extra surface-detail picture (it caused sparkle in the editor)
                
                null,
                "Assets/AK STUDIO ART/Fire Extinguisher/Models/Fire Extinguisher/Fire Extinguisher AO.png");
            akMat.SetFloat("_Smoothness", 0.55f);
            Material oldMat = MakeLit("ExtinguisherClassic",
                "Assets/OldExtinguisher/Textures/old_extinguisher_diffSpecA_2022.png",
                null, // no extra surface-detail picture (it caused sparkle in the editor)
                
                null, null);
            oldMat.SetFloat("_Smoothness", 0.45f);

            var models = new GameObject[2];
            var pins = new GameObject[2];
            var nozzles = new Transform[2];
            string[] prefabs = { AkPrefab, OldPrefab };
            Material[] mats = { akMat, oldMat };
            string[] names = { "Level1_Modern", "Level2_Classic" };

            for (int i = 0; i < 2; i++)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabs[i]);
                if (prefab == null) throw new System.Exception("Extinguisher prefab missing: " + prefabs[i]);

                var holder = new GameObject(names[i]);
                holder.transform.SetParent(root.transform, false);
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, holder.transform);

                foreach (Collider c in inst.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
                foreach (Renderer r in inst.GetComponentsInChildren<Renderer>())
                {
                    var shared = new Material[r.sharedMaterials.Length];
                    for (int m = 0; m < shared.Length; m++) shared[m] = mats[i];
                    r.sharedMaterials = shared;
                }

                // Stand the model upright if its longest side is not pointing up.
                Bounds b = WorldBounds(inst);
                if (b.size.z > b.size.y * 1.2f && b.size.z >= b.size.x) inst.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                else if (b.size.x > b.size.y * 1.2f && b.size.x >= b.size.z) inst.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                b = WorldBounds(inst);
                Debug.Log("[Builder] " + names[i] + " bounds " + b.size + " centre " + b.center);

                const float targetHeight = 0.25f;
                float scale = targetHeight / Mathf.Max(0.0001f, b.size.y);
                inst.transform.localScale *= scale;
                b = WorldBounds(inst);
                inst.transform.position += holder.transform.position - b.center;
                b = WorldBounds(inst);

                holder.transform.localPosition = new Vector3(0.06f, -0.17f, 0.5f);
                holder.transform.localRotation = Quaternion.Euler(-12f, 215f, 0f);
                b = WorldBounds(inst);

                var nozzle = new GameObject("Nozzle");
                nozzle.transform.SetParent(holder.transform, true);
                nozzle.transform.position = new Vector3(b.center.x, b.max.y, b.center.z) + cameraTransform.forward * 0.05f;
                nozzles[i] = nozzle.transform;

                GameObject pin = Primitive(PrimitiveType.Cylinder, "SafetyPin", holder.transform,
                    Vector3.zero, new Vector3(0.03f, 0.003f, 0.03f), yellowMat);
                foreach (Collider c in pin.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
                pin.transform.position = new Vector3(b.center.x + b.extents.x * 0.55f, b.max.y - 0.015f, b.center.z);
                pin.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
                pins[i] = pin;

                models[i] = holder;
            }

            var emitter = new GameObject("SprayEmitter");
            emitter.transform.SetParent(root.transform, false);
            emitter.transform.localPosition = new Vector3(0.1f, -0.12f, 0.4f);

            var ps = emitter.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = 0.55f;
            main.startSpeed = 6f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.12f);
            main.startColor = new Color(0.92f, 0.96f, 1f, 0.55f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 200;

            var emission = ps.emission;
            emission.rateOverTime = 160f;
            emission.enabled = false;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 4f;
            shape.radius = 0.008f;

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.4f, 1f, 3.2f));

            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0.7f, 0f), new GradientAlphaKey(0.0f, 1f) });
            fade.color = grad;

            emitter.GetComponent<ParticleSystemRenderer>().sharedMaterial = sprayMat;

            var extinguisher = root.AddComponent<Extinguisher>();
            SetRef(extinguisher, "arCamera", cam);
            SetRef(extinguisher, "sprayParticles", ps);
            SetRef(extinguisher, "puffPrefab", puffPrefab);
            SetRef(extinguisher, "sprayEmitter", emitter.transform);
            SetArray(extinguisher, "models", models);
            SetArray(extinguisher, "pinVisuals", pins);
            SetArray(extinguisher, "nozzles", nozzles);
            return extinguisher;
        }

        private static Bounds WorldBounds(GameObject go)
        {
            Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            return b;
        }

        private static void SetArray(Object target, string field, Object[] values)
        {
            var so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(field);
            prop.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
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
        // The screen is designed at 1080 x 1920. On a typical phone this makes the text
        // and buttons below large enough to read and tap easily.

        private static Sprite roundSprite;
        private static Sprite fadeSprite;
        private static readonly Color Clear = new Color(0f, 0f, 0f, 0f);

        private static Sprite MakeSprite(string file, int w, int h, System.Func<int, int, Color> pixel, bool sliced)
        {
            EnsureFolder("Assets/PassAR/Art/UI");
            string path = "Assets/PassAR/Art/UI/" + file;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    tex.SetPixel(x, y, pixel(x, y));
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = TextureWrapMode.Clamp;
            if (sliced) importer.spriteBorder = new Vector4(w / 2f - 1, h / 2f - 1, w / 2f - 1, h / 2f - 1);
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void MakeUiSprites()
        {
            // A rounded square with fully round ends that can be stretched to any size.
            roundSprite = MakeSprite("rounded.png", 128, 128, (x, y) =>
            {
                float dx = x + 0.5f - 64f, dy = y + 0.5f - 64f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(64f - d + 0.5f);
                return new Color(1f, 1f, 1f, a);
            }, true);
            // A fade from solid at the top to clear at the bottom.
            fadeSprite = MakeSprite("fade.png", 4, 128, (x, y) =>
                new Color(1f, 1f, 1f, Mathf.Pow(y / 127f, 1.6f)), false);
        }

        private static UIManager BuildUI()
        {
            MakeUiSprites();

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

            // ---------------- Main menu ----------------
            RectTransform welcome = Panel(root, "WelcomePanel", Theme.Background, Vector2.zero, Vector2.one);

            RectTransform hero = Panel(welcome, "Hero", Color.white, new Vector2(0f, 0.60f), Vector2.one);
            hero.GetComponent<Image>().enabled = false;
            hero.gameObject.AddComponent<RectMask2D>();
            var photoGo = new GameObject("GladsPhoto", typeof(RectTransform), typeof(Image), typeof(AspectRatioFitter));
            photoGo.transform.SetParent(hero, false);
            var photoRect = (RectTransform)photoGo.transform;
            photoRect.anchorMin = photoRect.anchorMax = new Vector2(0.5f, 0.5f);
            var photo = photoGo.GetComponent<Image>();
            photo.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/PassAR/Art/Branding/glads_apartment.png");
            photo.color = new Color(0.62f, 0.62f, 0.66f, 1f);
            photo.raycastTarget = false;
            var fitter = photoGo.GetComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = photo.sprite != null ? photo.sprite.rect.width / photo.sprite.rect.height : 1.5f;
            Fade(hero, "HeroFade", Theme.Background, new Vector2(0f, 0f), new Vector2(1f, 0.75f), true);

            RectTransform menuSafe = Safe(welcome);
            Label(welcome, "Tag", "GLADS APARTMENT", 38, FontStyles.Bold, new Vector2(0.08f, 0.545f), new Vector2(0.92f, 0.59f),
                TextAlignmentOptions.BottomLeft, Theme.Accent, 8f);
            Label(welcome, "Title", "Fire Safety\nTrainer", 96, FontStyles.Bold, new Vector2(0.08f, 0.42f), new Vector2(0.92f, 0.545f),
                TextAlignmentOptions.TopLeft, Theme.Text, 0f).lineSpacing = -12f;
            TMP_Text menuBest = Label(menuSafe, "BestText", "", 36, FontStyles.Bold, new Vector2(0.08f, 0.375f), new Vector2(0.92f, 0.415f),
                TextAlignmentOptions.MidlineLeft, Theme.TextMuted, 4f);

            Label(menuSafe, "ExtLabel", "EXTINGUISHER", 32, FontStyles.Bold, new Vector2(0.08f, 0.335f), new Vector2(0.92f, 0.372f),
                TextAlignmentOptions.MidlineLeft, Theme.TextMuted, 6f);
            Button level1 = MakeButton(menuSafe, "Level1Button", "LEVEL 1 · MODERN", 30, Theme.Surface2,
                new Vector2(0.08f, 0.26f), new Vector2(0.485f, 0.325f), Theme.TextMuted);
            Button level2 = MakeButton(menuSafe, "Level2Button", "LEVEL 2 · CLASSIC", 30, Theme.Surface2,
                new Vector2(0.515f, 0.26f), new Vector2(0.92f, 0.325f), Theme.TextMuted);

            Button startButton = MakeButton(menuSafe, "StartButton", "START TRAINING", 52, Theme.Accent,
                new Vector2(0.08f, 0.165f), new Vector2(0.92f, 0.245f), Theme.OnAccent);
            Button howToButton = MakeButton(menuSafe, "HowToButton", "HOW TO PLAY", 38, Theme.Surface2,
                new Vector2(0.08f, 0.095f), new Vector2(0.485f, 0.155f), Theme.Text);
            Button settingsButton = MakeButton(menuSafe, "SettingsButton", "SETTINGS", 38, Theme.Surface2,
                new Vector2(0.515f, 0.095f), new Vector2(0.92f, 0.155f), Theme.Text);
            Button quitButton = MakeButton(menuSafe, "QuitButton", "QUIT", 34, Clear,
                new Vector2(0.3f, 0.03f), new Vector2(0.7f, 0.085f), Theme.TextMuted);
            TMP_Text busyText = Label(menuSafe, "BusyText", "", 40, FontStyles.Bold, new Vector2(0.08f, 0.17f), new Vector2(0.92f, 0.24f),
                TextAlignmentOptions.Midline, Theme.Accent2, 2f);

            // ---------------- How to play ----------------
            RectTransform howTo = Panel(root, "HowToPanel", Theme.Background, Vector2.zero, Vector2.one);
            RectTransform howSafe = Safe(howTo);
            Label(howSafe, "Title", "How to play", 88, FontStyles.Bold, new Vector2(0.08f, 0.88f), new Vector2(0.92f, 0.96f),
                TextAlignmentOptions.MidlineLeft, Theme.Text, 0f);
            Label(howSafe, "Sub", "Put out the virtual fire with the PASS method.", 40, FontStyles.Normal,
                new Vector2(0.08f, 0.835f), new Vector2(0.92f, 0.885f), TextAlignmentOptions.MidlineLeft, Theme.TextMuted, 0f);
            string[] letters = { "P", "A", "S", "S" };
            string[] heads = { "PULL", "AIM", "SQUEEZE", "SWEEP" };
            string[] bodies =
            {
                "Press PULL PIN to remove the safety pin.",
                "Point at the base of the fire, 1 to 3 metres away.",
                "Press and hold the SQUEEZE button.",
                "Move side to side until all three flames are out."
            };
            for (int i = 0; i < 4; i++)
            {
                float top = 0.815f - i * 0.135f;
                RectTransform card = Panel(howSafe, "Step" + (i + 1), Theme.Surface, new Vector2(0.08f, top - 0.12f), new Vector2(0.92f, top));
                Round(card.GetComponent<Image>(), 44f);
                RectTransform badge = Panel(card, "Badge", Theme.Accent, new Vector2(0.04f, 0.5f), new Vector2(0.04f, 0.5f));
                badge.pivot = new Vector2(0f, 0.5f);
                badge.sizeDelta = new Vector2(110f, 110f);
                badge.anchoredPosition = Vector2.zero;
                Round(badge.GetComponent<Image>(), 55f);
                Label(badge, "Letter", letters[i], 64, FontStyles.Bold, Vector2.zero, Vector2.one, TextAlignmentOptions.Center, Theme.OnAccent, 0f);
                Label(card, "Head", heads[i], 38, FontStyles.Bold, new Vector2(0.25f, 0.5f), new Vector2(0.96f, 0.92f),
                    TextAlignmentOptions.BottomLeft, Theme.Accent2, 4f);
                Label(card, "Body", bodies[i], 34, FontStyles.Normal, new Vector2(0.25f, 0.06f), new Vector2(0.96f, 0.5f),
                    TextAlignmentOptions.TopLeft, Theme.Text, 0f);
            }
            Label(howSafe, "Penalty", "Each mistake costs 10 points.", 36, FontStyles.Normal,
                new Vector2(0.08f, 0.185f), new Vector2(0.92f, 0.235f), TextAlignmentOptions.Midline, Theme.TextMuted, 0f);
            Button howToBack = MakeButton(howSafe, "BackButton", "BACK", 44, Theme.Surface2,
                new Vector2(0.08f, 0.06f), new Vector2(0.92f, 0.14f), Theme.Text);

            // ---------------- Settings ----------------
            RectTransform settings = Panel(root, "SettingsPanel", Theme.Background, Vector2.zero, Vector2.one);
            RectTransform setSafe = Safe(settings);
            Label(setSafe, "Title", "Settings", 88, FontStyles.Bold, new Vector2(0.08f, 0.88f), new Vector2(0.92f, 0.96f),
                TextAlignmentOptions.MidlineLeft, Theme.Text, 0f);
            Button soundToggle = MakeButton(setSafe, "SoundToggle", "Sound: ON", 44, Theme.Surface2,
                new Vector2(0.08f, 0.77f), new Vector2(0.92f, 0.85f), Theme.Text);
            TMP_Text soundToggleLabel = soundToggle.GetComponentInChildren<TMP_Text>();
            RectTransform rulesCard = Panel(setSafe, "RulesCard", Theme.Surface, new Vector2(0.08f, 0.50f), new Vector2(0.92f, 0.745f));
            Round(rulesCard.GetComponent<Image>(), 44f);
            Label(rulesCard, "Heading", "TRAINING RULES", 30, FontStyles.Bold, new Vector2(0.06f, 0.78f), new Vector2(0.94f, 0.96f),
                TextAlignmentOptions.MidlineLeft, Theme.TextMuted, 6f);
            TMP_Text rules = Label(rulesCard, "Rules", "", 34, FontStyles.Normal, new Vector2(0.06f, 0.05f), new Vector2(0.94f, 0.78f),
                TextAlignmentOptions.TopLeft, Theme.Text, 0f);
            TMP_Text savedInfo = Label(setSafe, "SavedInfo", "", 34, FontStyles.Normal, new Vector2(0.08f, 0.44f), new Vector2(0.92f, 0.49f),
                TextAlignmentOptions.MidlineLeft, Theme.TextMuted, 0f);
            Button clear = MakeButton(setSafe, "ClearButton", "CLEAR SAVED RESULTS", 38, Theme.Surface2,
                new Vector2(0.08f, 0.35f), new Vector2(0.92f, 0.425f), Theme.Danger);
            TMP_Text clearLabel = clear.GetComponentInChildren<TMP_Text>();
            Label(setSafe, "About",
                "Glads Fire Safety Trainer\nThe camera is only used to show AR. No pictures are saved or sent, and the app works offline.",
                32, FontStyles.Normal, new Vector2(0.08f, 0.17f), new Vector2(0.92f, 0.33f), TextAlignmentOptions.TopLeft, Theme.TextMuted, 0f);
            Button settingsBack = MakeButton(setSafe, "BackButton", "BACK", 44, Theme.Surface2,
                new Vector2(0.08f, 0.06f), new Vector2(0.92f, 0.14f), Theme.Text);

            // ---------------- Camera permission and AR messages ----------------
            RectTransform permission = Panel(root, "PermissionPanel", Theme.Background, Vector2.zero, Vector2.one);
            RectTransform permSafe = Safe(permission);
            TMP_Text permissionMessage = Label(permSafe, "Message", "", 46, FontStyles.Normal, new Vector2(0.1f, 0.45f), new Vector2(0.9f, 0.7f),
                TextAlignmentOptions.Midline, Theme.Text, 0f);
            Button retryButton = MakeButton(permSafe, "RetryButton", "TRY AGAIN", 48, Theme.Accent,
                new Vector2(0.15f, 0.32f), new Vector2(0.85f, 0.41f), Theme.OnAccent);

            // ---------------- HUD ----------------
            RectTransform hud = Panel(root, "HudPanel", Clear, Vector2.zero, Vector2.one);
            hud.GetComponent<Image>().raycastTarget = false;
            Fade(hud, "TopScrim", new Color(0f, 0f, 0f, 0.55f), new Vector2(0f, 0.72f), new Vector2(1f, 1f), false);
            Fade(hud, "BottomScrim", new Color(0f, 0f, 0f, 0.6f), new Vector2(0f, 0f), new Vector2(1f, 0.30f), true);
            RectTransform hudSafe = Safe(hud);

            RectTransform instructionBar = Panel(hudSafe, "InstructionBar", Theme.Surface, new Vector2(0f, 1f), new Vector2(1f, 1f));
            instructionBar.pivot = new Vector2(0.5f, 1f);
            instructionBar.sizeDelta = new Vector2(-64f, 230f);
            instructionBar.anchoredPosition = new Vector2(0f, -28f);
            instructionBar.GetComponent<Image>().raycastTarget = false;
            Round(instructionBar.GetComponent<Image>(), 48f);
            TMP_Text instruction = Label(instructionBar, "InstructionText", "", 42, FontStyles.Bold, Vector2.zero, Vector2.one,
                TextAlignmentOptions.MidlineLeft, Theme.Text, 0f);
            instruction.rectTransform.offsetMin = new Vector2(40f, 14f);
            instruction.rectTransform.offsetMax = new Vector2(-40f, -14f);
            instruction.enableAutoSizing = true;
            instruction.fontSizeMin = 34f;
            instruction.fontSizeMax = 44f;

            Button muteButton = MakeButton(hudSafe, "MuteButton", "Sound: ON", 34, Theme.Surface, Vector2.zero, Vector2.zero, Theme.Text);
            PlaceCorner(muteButton.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(32f, -280f), new Vector2(330f, 120f));
            TMP_Text muteLabel = muteButton.GetComponentInChildren<TMP_Text>();

            RectTransform timerChip = Panel(hudSafe, "TimerChip", Theme.Surface, Vector2.zero, Vector2.zero);
            PlaceCorner(timerChip, new Vector2(1f, 1f), new Vector2(-32f, -280f), new Vector2(230f, 120f));
            timerChip.GetComponent<Image>().raycastTarget = false;
            Round(timerChip.GetComponent<Image>(), 60f);
            TMP_Text timer = Label(timerChip, "TimerText", "", 46, FontStyles.Bold, Vector2.zero, Vector2.one,
                TextAlignmentOptions.Center, Theme.Accent2, 0f);

            GameObject reticle = new GameObject("Reticle", typeof(RectTransform), typeof(Image));
            reticle.transform.SetParent(hud, false);
            var reticleRect = reticle.GetComponent<RectTransform>();
            reticleRect.anchorMin = reticleRect.anchorMax = new Vector2(0.5f, 0.5f);
            reticleRect.sizeDelta = new Vector2(120f, 120f);
            var reticleImage = reticle.GetComponent<Image>();
            reticleImage.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/PassAR/Art/Textures/reticle.png");
            reticleImage.color = new Color(1f, 1f, 1f, 0.85f);
            reticleImage.raycastTarget = false;

            TMP_Text message = Label(hudSafe, "MessageText", "", 44, FontStyles.Bold, Vector2.zero, Vector2.zero,
                TextAlignmentOptions.Center, Theme.Text, 0f);
            PlaceCorner(message.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 330f), new Vector2(980f, 130f));

            RectTransform actions = Panel(hudSafe, "ActionButtons", Clear, Vector2.zero, Vector2.one);
            actions.GetComponent<Image>().raycastTarget = false;

            Button pullPin = MakeButton(actions, "PullPinButton", "PULL PIN", 46, Theme.Accent, Vector2.zero, Vector2.zero, Theme.OnAccent);
            PlaceCorner(pullPin.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(40f, 70f), new Vector2(460f, 190f));

            GameObject squeeze = new GameObject("SqueezeButton", typeof(RectTransform), typeof(Image), typeof(HoldButton), typeof(PressScale));
            squeeze.transform.SetParent(actions, false);
            PlaceCorner(squeeze.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(-40f, 70f), new Vector2(460f, 190f));
            var squeezeImage = squeeze.GetComponent<Image>();
            Round(squeezeImage, 95f);
            squeezeImage.color = Theme.Surface2;
            Label(squeeze.transform, "Label", "SQUEEZE", 46, FontStyles.Bold, Vector2.zero, Vector2.one,
                TextAlignmentOptions.Center, Theme.Text, 2f).raycastTarget = false;

            RectTransform lost = Panel(hudSafe, "TrackingLostBanner", Theme.Danger, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            lost.sizeDelta = new Vector2(900f, 190f);
            lost.GetComponent<Image>().raycastTarget = false;
            Round(lost.GetComponent<Image>(), 60f);
            Label(lost, "Text", "Tracking lost - move the phone slowly", 42, FontStyles.Bold, Vector2.zero, Vector2.one,
                TextAlignmentOptions.Center, Color.white, 0f).raycastTarget = false;
            lost.gameObject.SetActive(false);

            // ---------------- Summary ----------------
            RectTransform summary = Panel(root, "SummaryPanel", new Color(0f, 0f, 0f, 0.55f), Vector2.zero, Vector2.one);
            RectTransform card2 = Panel(summary, "Card", Theme.Surface, new Vector2(0.07f, 0.22f), new Vector2(0.93f, 0.78f));
            card2.GetComponent<Image>().color = new Color(0.086f, 0.098f, 0.125f, 0.97f);
            Round(card2.GetComponent<Image>(), 64f);
            Label(card2, "Tag", "TRAINING COMPLETE", 34, FontStyles.Bold, new Vector2(0.08f, 0.88f), new Vector2(0.92f, 0.96f),
                TextAlignmentOptions.Midline, Theme.Accent, 8f);
            TMP_Text sumScore = Label(card2, "Score", "", 200, FontStyles.Bold, new Vector2(0.05f, 0.58f), new Vector2(0.95f, 0.88f),
                TextAlignmentOptions.Midline, Theme.Text, 0f);
            TMP_Text sumTime = Label(card2, "Time", "", 38, FontStyles.Normal, new Vector2(0.08f, 0.47f), new Vector2(0.5f, 0.56f),
                TextAlignmentOptions.Midline, Theme.TextMuted, 0f);
            TMP_Text sumErrors = Label(card2, "Errors", "", 38, FontStyles.Normal, new Vector2(0.5f, 0.47f), new Vector2(0.92f, 0.56f),
                TextAlignmentOptions.Midline, Theme.TextMuted, 0f);
            TMP_Text sumBest = Label(card2, "Best", "", 36, FontStyles.Bold, new Vector2(0.08f, 0.39f), new Vector2(0.92f, 0.46f),
                TextAlignmentOptions.Midline, Theme.Accent2, 2f);
            Button restart = MakeButton(card2, "RestartButton", "TRY AGAIN", 48, Theme.Accent,
                new Vector2(0.08f, 0.20f), new Vector2(0.92f, 0.34f), Theme.OnAccent);
            Button menu = MakeButton(card2, "MenuButton", "MAIN MENU", 40, Theme.Surface2,
                new Vector2(0.08f, 0.04f), new Vector2(0.92f, 0.17f), Theme.Text);

            // ---------------- Connect everything ----------------
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
            SetArray(ui, "extinguisherButtons", new Object[] { level1, level2 });
            SetArray(ui, "extinguisherButtonImages", new Object[] { level1.GetComponent<Image>(), level2.GetComponent<Image>() });

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

        /// <summary>The area of the screen that stays clear of notches and rounded corners.</summary>
        private static RectTransform Safe(Transform parent)
        {
            var go = new GameObject("Safe", typeof(RectTransform), typeof(SafeArea));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static void Round(Image image, float radius)
        {
            image.sprite = roundSprite;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 64f / radius;
        }

        private static void Fade(Transform parent, string name, Color colour, Vector2 min, Vector2 max, bool opaqueAtBottom)
        {
            RectTransform rect = Panel(parent, name, colour, min, max);
            var image = rect.GetComponent<Image>();
            image.sprite = fadeSprite;
            image.raycastTarget = false;
            // The picture is solid at the top, so flip it for a fade at the bottom.
            if (opaqueAtBottom) rect.localScale = new Vector3(1f, -1f, 1f);
        }

        private static TMP_Text Label(Transform parent, string name, string text, int size, FontStyles style,
            Vector2 anchorMin, Vector2 anchorMax, TextAlignmentOptions alignment, Color colour, float spacing)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = rect.offsetMax = Vector2.zero;

            var label = go.GetComponent<TextMeshProUGUI>();
            label.fontSize = size;
            label.fontStyle = style;
            label.alignment = alignment;
            label.color = colour;
            label.text = text;
            label.characterSpacing = spacing;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Overflow;
            label.raycastTarget = false;
            return label;
        }

        private static Button MakeButton(Transform parent, string name, string label, int size, Color colour,
            Vector2 anchorMin, Vector2 anchorMax, Color textColour)
        {
            RectTransform rect = Panel(parent, name, colour, anchorMin, anchorMax);
            Round(rect.GetComponent<Image>(), 70f);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            button.transition = Selectable.Transition.None;
            rect.gameObject.AddComponent<PressScale>();
            Label(rect, "Label", label, size, FontStyles.Bold, Vector2.zero, Vector2.one, TextAlignmentOptions.Center, textColour, 3f);
            return button;
        }

        private static void PlaceCorner(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}
