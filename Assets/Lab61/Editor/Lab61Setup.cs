using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Lab61
{
    // Automatiza la guía "META XR SDK - Teleport" (Lab 6.1) sobre la subestación Andahuasi:
    //  1. Floor con Mesh Collider + Mesh Collider en los pisos transitables.
    //  2. Escena_Andahuasi_Joystick: OVRPlayerController (parte opcional de la guía).
    //  3. Escena_Andahuasi: OVRCameraRigInteraction + TeleportHotspot +
    //     Teleport Interactable / Collider Surface / Reticle Data Teleport en los pisos (Solid1:231, etc.).
    //  4. Tracking Origin Type = Floor Level y escenas agregadas al Build Settings.
    // Se ejecuta sola la primera vez que se abre el proyecto (pide confirmación) o desde el menú "Lab 6.1".
    [InitializeOnLoad]
    public static partial class Lab61Setup
    {
        const string ScenePath = "Assets/Scenes/Escena_Andahuasi.unity";
        const string JoystickScenePath = "Assets/Scenes/Escena_Andahuasi_Joystick.unity";
        const string ModelRootName = "Planta_Andahuasi_completa";
        const string MainFloorName = "Solid1:231";
        const string MarkerName = "Lab61_Configurado";
        const string HotspotsParentName = "Lab61_TeleportHotspots";
        const string RigInteractionGuid = "f54012181eacade4ea242c65f804720f"; // OVRCameraRigInteraction (Meta XR SDK 201, tomado de Hito1)
        const string AskedKey = "Lab61.AskedThisSession";

        // Criterios para considerar una pieza como "piso transitable" (metros).
        const float MaxFloorThickness = 0.6f;
        const float MinFloorArea = 4f;
        const float MinFloorSide = 1f;
        const float MaxHeightAboveGround = 2.5f;
        const int MaxHotspots = 12;

        static Lab61Setup()
        {
            EditorApplication.delayCall += AskOnFirstOpen;
        }

        static void AskOnFirstOpen()
        {
            if (SessionState.GetBool(AskedKey, false)) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall += AskOnFirstOpen;
                return;
            }
            SessionState.SetBool(AskedKey, true);
            if (!File.Exists(ScenePath) || File.ReadAllText(ScenePath).Contains("m_Name: " + MarkerName)) return;

            if (EditorUtility.DisplayDialog("Lab 6.1 - Teleport",
                    "La escena Escena_Andahuasi aún no está configurada.\n\n" +
                    "¿Aplicar ahora todos los pasos de la guía (colisionadores, OVRPlayerController, " +
                    "OVRCameraRigInteraction, TeleportHotspot, Teleport Interactable, Floor Level)?",
                    "Sí, configurar", "Ahora no"))
            {
                ConfigureAll();
            }
        }

        [MenuItem("Lab 6.1/1. Configurar escenas (todo el lab)", priority = 1)]
        public static void ConfigureAll()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            if (FindType("OVRManager") == null || FindType("Oculus.Interaction.Locomotion.TeleportInteractable") == null)
            {
                EditorUtility.DisplayDialog("Lab 6.1",
                    "No se encontró el Meta XR SDK (OVRManager / TeleportInteractable).\n" +
                    "Espera a que el Package Manager termine de instalar com.meta.xr.sdk.all y vuelve a ejecutar " +
                    "Lab 6.1 > 1. Configurar escenas.", "OK");
                return;
            }

            try
            {
                EditorUtility.DisplayProgressBar("Lab 6.1", "Abriendo Escena_Andahuasi...", 0.05f);
                var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

                CleanPrevious(scene);
                RemoveDefaultCamera(scene);

                EditorUtility.DisplayProgressBar("Lab 6.1", "Analizando el modelo...", 0.15f);
                var meshes = GetModelMeshes(scene);
                if (meshes.Count == 0) throw new Exception("No se encontraron mallas del modelo en la escena.");
                var modelBounds = Encapsulate(meshes.Select(m => m.GetComponent<Renderer>()));

                CreateFloor(modelBounds);

                EditorUtility.DisplayProgressBar("Lab 6.1", "Buscando pisos transitables...", 0.25f);
                var floors = FindWalkableFloors(meshes);
                Debug.Log($"[Lab 6.1] Pisos transitables ({floors.Count}): " + string.Join(", ", floors.Select(f => f.name)));

                EditorUtility.DisplayProgressBar("Lab 6.1", "Agregando Mesh Collider a los pisos...", 0.35f);
                foreach (var f in floors) AddMeshCollider(f);

                var spawn = GetSpawnPoint(floors);

                // --- Parte 1 (opcional en la guía): OVRPlayerController + joystick ---
                EditorUtility.DisplayProgressBar("Lab 6.1", "Creando Escena_Andahuasi_Joystick...", 0.5f);
                var player = InstantiatePrefabByName("OVRPlayerController");
                if (player != null)
                {
                    player.transform.position = spawn + Vector3.up * 1.1f;
                    SetFloorLevel(player);
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene, JoystickScenePath, true);
                    Object.DestroyImmediate(player);
                }
                else
                {
                    Debug.LogWarning("[Lab 6.1] No se encontró el prefab OVRPlayerController; se omite la escena de joystick.");
                }

                // --- Parte 2: OVRCameraRigInteraction + teletransporte ---
                EditorUtility.DisplayProgressBar("Lab 6.1", "Agregando OVRCameraRigInteraction...", 0.65f);
                var rig = InstantiatePrefab(RigInteractionGuid, "OVRCameraRigInteraction");
                if (rig == null) throw new Exception("No se encontró el prefab OVRCameraRigInteraction.");
                rig.transform.position = spawn;
                rig.transform.rotation = Quaternion.identity;
                SetFloorLevel(rig);

                EditorUtility.DisplayProgressBar("Lab 6.1", "Configurando superficies de teletransporte...", 0.75f);
                foreach (var f in floors) AddTeleportSurface(f);

                EditorUtility.DisplayProgressBar("Lab 6.1", "Colocando TeleportHotspot...", 0.85f);
                AddHotspots(floors);

                new GameObject(MarkerName);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene, ScenePath);

                UpdateBuildSettings();

                EditorUtility.ClearProgressBar();
                var msg = $"Listo.\n\n• Escena_Andahuasi (teletransporte): {floors.Count} pisos con Teleport Interactable.\n" +
                          (player != null ? "• Escena_Andahuasi_Joystick: OVRPlayerController.\n" : "") +
                          "• Tracking Origin Type = Floor Level.\n\n" +
                          (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android
                              ? "Falta cambiar la plataforma a Android: Lab 6.1 > 2. Cambiar plataforma a Android."
                              : "Conecta el Quest y usa File > Build And Run.");
                Debug.Log("[Lab 6.1] " + msg);
                EditorUtility.DisplayDialog("Lab 6.1", msg, "OK");
            }
            catch (Exception e)
            {
                EditorUtility.ClearProgressBar();
                Debug.LogException(e);
                EditorUtility.DisplayDialog("Lab 6.1", "Error: " + e.Message + "\n\nRevisa la Consola.", "OK");
            }
        }

        [MenuItem("Lab 6.1/2. Cambiar plataforma a Android", priority = 2)]
        public static void SwitchToAndroid()
        {
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
        }

        [MenuItem("Lab 6.1/3. Agregar Mesh Collider a TODO el modelo (pesado)", priority = 20)]
        public static void AddCollidersToEverything()
        {
            var meshes = GetModelMeshes(SceneManager.GetActiveScene());
            for (int i = 0; i < meshes.Count; i++)
            {
                if (i % 200 == 0 && EditorUtility.DisplayCancelableProgressBar("Lab 6.1", $"Mesh Collider {i}/{meshes.Count}", (float)i / meshes.Count))
                    break;
                AddMeshCollider(meshes[i]);
            }
            EditorUtility.ClearProgressBar();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        // ---------------------------------------------------------------- escena

        static void CleanPrevious(Scene scene)
        {
            var names = new HashSet<string> { MarkerName, HotspotsParentName, "OVRCameraRigInteraction", "OVRPlayerController", "Floor", "Plane" };
            foreach (var root in scene.GetRootGameObjects())
                if (names.Contains(root.name)) Object.DestroyImmediate(root);
        }

        static void RemoveDefaultCamera(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == "Main Camera" && root.GetComponent<Camera>() != null)
                    Object.DestroyImmediate(root);
        }

        static List<MeshFilter> GetModelMeshes(Scene scene)
        {
            var root = scene.GetRootGameObjects().FirstOrDefault(g => g.name == ModelRootName);
            IEnumerable<GameObject> roots = root != null ? new[] { root } : scene.GetRootGameObjects();
            return roots.SelectMany(r => r.GetComponentsInChildren<MeshFilter>(true))
                        .Where(m => m.sharedMesh != null && m.GetComponent<Renderer>() != null)
                        .ToList();
        }

        static Bounds Encapsulate(IEnumerable<Renderer> renderers)
        {
            bool first = true;
            var b = new Bounds();
            foreach (var r in renderers)
            {
                if (first) { b = r.bounds; first = false; }
                else b.Encapsulate(r.bounds);
            }
            return b;
        }

        // Plano "Floor" con Mesh Collider bajo todo el modelo, para no caer al vacío.
        static void CreateFloor(Bounds model)
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane); // trae Mesh Collider
            floor.name = "Floor";
            floor.transform.position = new Vector3(model.center.x, model.min.y - 0.05f, model.center.z);
            floor.transform.rotation = Quaternion.identity;
            // El Plane primitivo mide 10x10 m.
            floor.transform.localScale = new Vector3(model.size.x / 10f * 1.1f, 1f, model.size.z / 10f * 1.1f);
        }

        static List<MeshFilter> FindWalkableFloors(List<MeshFilter> meshes)
        {
            bool IsFlat(MeshFilter m)
            {
                var s = m.GetComponent<Renderer>().bounds.size;
                return s.y <= MaxFloorThickness && s.x >= MinFloorSide && s.z >= MinFloorSide && s.x * s.z >= MinFloorArea;
            }

            var flat = meshes.Where(IsFlat).ToList();
            if (flat.Count == 0) return meshes.Where(m => m.name == MainFloorName).ToList();

            // Nivel del suelo = cara superior de la losa más grande.
            var biggest = flat.OrderByDescending(m => { var s = m.GetComponent<Renderer>().bounds.size; return s.x * s.z; }).First();
            float ground = biggest.GetComponent<Renderer>().bounds.max.y;

            var floors = flat.Where(m =>
            {
                float top = m.GetComponent<Renderer>().bounds.max.y;
                return top >= ground - 1f && top <= ground + MaxHeightAboveGround;
            }).ToList();

            foreach (var m in meshes.Where(m => m.name == MainFloorName))
                if (!floors.Contains(m)) floors.Add(m);
            return floors;
        }

        static Vector3 GetSpawnPoint(List<MeshFilter> floors)
        {
            var main = floors.FirstOrDefault(f => f.name == MainFloorName)
                       ?? floors.OrderByDescending(f => { var s = f.GetComponent<Renderer>().bounds.size; return s.x * s.z; }).FirstOrDefault();
            if (main == null) return Vector3.zero;
            var b = main.GetComponent<Renderer>().bounds;
            return new Vector3(b.center.x, b.max.y, b.center.z);
        }

        static void AddMeshCollider(MeshFilter mf)
        {
            var mc = mf.GetComponent<MeshCollider>();
            if (mc == null) mc = mf.gameObject.AddComponent<MeshCollider>();
            mc.sharedMesh = mf.sharedMesh;
        }

        // Teleport Interactable + Collider Surface + Reticle Data Teleport (como Solid1:231 en la guía).
        static void AddTeleportSurface(MeshFilter mf)
        {
            var go = mf.gameObject;
            var collider = go.GetComponent<MeshCollider>();

            var surface = GetOrAdd(go, "Oculus.Interaction.Surfaces.ColliderSurface");
            SetRef(surface, "_collider", collider);

            var teleport = GetOrAdd(go, "Oculus.Interaction.Locomotion.TeleportInteractable");
            SetRef(teleport, "_surface", surface);

            var reticle = GetOrAdd(go, "Oculus.Interaction.DistanceReticles.ReticleDataTeleport");
            SetEnum(reticle, "_reticleMode", "ValidTarget");
        }

        static void AddHotspots(List<MeshFilter> floors)
        {
            var prefab = FindPrefab(null, "TeleportHotspot");
            if (prefab == null)
            {
                Debug.LogWarning("[Lab 6.1] No se encontró el prefab TeleportHotspot; el teletransporte funcionará solo sobre los pisos.");
                return;
            }
            var parent = new GameObject(HotspotsParentName).transform;
            var targets = floors.Select(f => f.GetComponent<Renderer>().bounds)
                                .OrderByDescending(b => b.size.x * b.size.z)
                                .Take(MaxHotspots);
            int i = 1;
            foreach (var b in targets)
            {
                var hs = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                hs.name = $"TeleportHotspot_{i++:00}";
                hs.transform.position = new Vector3(b.center.x, b.max.y, b.center.z);
            }
        }

        // Tracking Origin Type: Eye Level -> Floor Level (paso final de la guía).
        static void SetFloorLevel(GameObject rigRoot)
        {
            var managerType = FindType("OVRManager");
            if (managerType == null) return;

            var managers = Object.FindObjectsOfType(managerType, true).Cast<Component>().ToList();
            if (managers.Count == 0)
            {
                var cameraRigType = FindType("OVRCameraRig");
                var cameraRig = cameraRigType != null ? rigRoot.GetComponentInChildren(cameraRigType, true) : null;
                managers.Add((cameraRig != null ? cameraRig.gameObject : rigRoot).AddComponent(managerType));
            }
            foreach (var m in managers) SetEnum(m, "_trackingOriginType", "FloorLevel");
        }

        static void UpdateBuildSettings()
        {
            // Si existe la escena demo liviana, es la que se instala en el Quest; Andahuasi completa queda desactivada.
            bool hasDemo = File.Exists(DemoScenePath);
            var scenes = new List<EditorBuildSettingsScene>();
            if (hasDemo) scenes.Add(new EditorBuildSettingsScene(DemoScenePath, true));
            scenes.Add(new EditorBuildSettingsScene(ScenePath, !hasDemo));
            if (File.Exists(JoystickScenePath)) scenes.Add(new EditorBuildSettingsScene(JoystickScenePath, false));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // ---------------------------------------------------------------- utilidades

        static GameObject InstantiatePrefabByName(string name) => InstantiatePrefab(null, name);

        static GameObject InstantiatePrefab(string guid, string name)
        {
            var prefab = FindPrefab(guid, name);
            if (prefab == null) return null;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.name = name;
            return go;
        }

        static GameObject FindPrefab(string guid, string name)
        {
            if (!string.IsNullOrEmpty(guid))
            {
                var byGuid = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (byGuid != null) return byGuid;
            }
            var path = AssetDatabase.FindAssets(name + " t:Prefab")
                                    .Select(AssetDatabase.GUIDToAssetPath)
                                    .Where(p => Path.GetFileNameWithoutExtension(p) == name)
                                    .OrderBy(p => p.Contains("/Samples/") ? 1 : 0)
                                    .FirstOrDefault();
            return path != null ? AssetDatabase.LoadAssetAtPath<GameObject>(path) : null;
        }

        static Component GetOrAdd(GameObject go, string typeName)
        {
            var t = FindType(typeName);
            if (t == null)
            {
                Debug.LogWarning($"[Lab 6.1] No se encontró el componente {typeName}.");
                return null;
            }
            return go.GetComponent(t) ?? go.AddComponent(t);
        }

        static void SetRef(Component c, string field, Object value)
        {
            if (c == null || value == null) return;
            var so = new SerializedObject(c);
            var p = so.FindProperty(field);
            if (p == null) { Debug.LogWarning($"[Lab 6.1] {c.GetType().Name} no tiene el campo {field}."); return; }
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetEnum(Component c, string field, string enumName)
        {
            if (c == null) return;
            var so = new SerializedObject(c);
            var p = so.FindProperty(field);
            if (p == null || p.propertyType != SerializedPropertyType.Enum) { Debug.LogWarning($"[Lab 6.1] {c.GetType().Name} no tiene el enum {field}."); return; }
            int idx = Array.IndexOf(p.enumNames, enumName);
            if (idx < 0) { Debug.LogWarning($"[Lab 6.1] {field} no tiene el valor {enumName}."); return; }
            p.enumValueIndex = idx;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static readonly Dictionary<string, Type> TypeCache = new Dictionary<string, Type>();

        static Type FindType(string fullName)
        {
            if (TypeCache.TryGetValue(fullName, out var cached)) return cached;
            string shortName = fullName.Substring(fullName.LastIndexOf('.') + 1);
            Type found = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                found = asm.GetType(fullName, false);
                if (found != null) break;
            }
            if (found == null)
            {
                found = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a => { try { return a.GetTypes(); } catch (ReflectionTypeLoadException e) { return e.Types.Where(x => x != null); } })
                    .FirstOrDefault(x => x.Name == shortName && typeof(Component).IsAssignableFrom(x));
            }
            TypeCache[fullName] = found;
            return found;
        }
    }
}
