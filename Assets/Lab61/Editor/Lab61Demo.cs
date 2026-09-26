using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Lab61
{
    // Escena liviana para mostrar el teletransporte en el Quest sin el modelo completo de Andahuasi
    // (~8 millones de triángulos, demasiado para el visor). Patio de subestación hecho con primitivas
    // y los modelos chicos del paquete (torre, postes, tanque, tachos), con los mismos pasos de la guía.
    public static partial class Lab61Setup
    {
        const string DemoScenePath = "Assets/Scenes/Escena_Teleport_Demo.unity";
        const string DemoMaterialsFolder = "Assets/Lab61/Demo";
        const string ModelsFolder = "Assets/Modelo 3D/Modelos/";

        [MenuItem("Lab 6.1/0. Crear escena DEMO liviana (para el Quest)", priority = 0)]
        public static void CreateDemoScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            if (FindType("OVRManager") == null || FindType("Oculus.Interaction.Locomotion.TeleportInteractable") == null)
            {
                EditorUtility.DisplayDialog("Lab 6.1",
                    "No se encontró el Meta XR SDK (OVRManager / TeleportInteractable).\n" +
                    "Espera a que el Package Manager termine de instalar com.meta.xr.sdk.all y vuelve a intentarlo.", "OK");
                return;
            }

            try
            {
                EditorUtility.DisplayProgressBar("Lab 6.1", "Creando escena demo...", 0.1f);
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                RenderSettings.skybox = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Skybox.mat");

                var light = new GameObject("Directional Light").AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.1f;
                light.shadows = LightShadows.None; // sombras en tiempo real = caras en el Quest
                light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

                var gravel   = DemoMaterial("Grava", new Color(0.46f, 0.43f, 0.38f));
                var concrete = DemoMaterial("Concreto", new Color(0.72f, 0.72f, 0.70f));
                var yellow   = DemoMaterial("Plataforma_Amarilla", new Color(0.95f, 0.78f, 0.25f));
                var wall     = DemoMaterial("Pared", new Color(0.90f, 0.89f, 0.85f));
                var roof     = DemoMaterial("Techo", new Color(0.35f, 0.38f, 0.42f));
                var fence    = DemoMaterial("Cerco", new Color(0.30f, 0.45f, 0.35f));
                var steel    = DemoMaterial("Transformador", new Color(0.42f, 0.50f, 0.58f));

                EditorUtility.DisplayProgressBar("Lab 6.1", "Armando el patio...", 0.3f);
                var env = new GameObject("Patio_Subestacion").transform;

                // Piso principal 60 x 60 m (Plane = 10 x 10 m y ya trae Mesh Collider).
                var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
                ground.name = "Piso_Patio";
                ground.transform.SetParent(env);
                ground.transform.localScale = new Vector3(6f, 1f, 6f);
                ground.GetComponent<Renderer>().sharedMaterial = gravel;

                // Superficies a las que uno se puede teletransportar (además del piso).
                var platforms = new List<GameObject>
                {
                    Block(env, "Losa_Transformador", new Vector3(0f, 0.15f, 10f), new Vector3(6f, 0.3f, 6f), concrete),
                    Block(env, "Plataforma_Media", new Vector3(-12f, 0.5f, 8f), new Vector3(5f, 1f, 5f), concrete),
                    Block(env, "Plataforma_Alta", new Vector3(12f, 1.25f, 12f), new Vector3(5f, 2.5f, 5f), yellow),
                    Block(env, "Techo_Caseta", new Vector3(-12f, 4.1f, 22f), new Vector3(8.4f, 0.2f, 6.4f), roof),
                    Block(env, "Pasarela", new Vector3(8f, 3f, 24f), new Vector3(12f, 0.3f, 2f), concrete),
                };

                // Obstáculos (no teletransportables): para mostrar que el rayo marca destino inválido.
                Block(env, "Transformador", new Vector3(0f, 1.55f, 11.5f), new Vector3(2f, 2.5f, 1.5f), steel);
                Block(env, "Caseta_Control", new Vector3(-12f, 2f, 22f), new Vector3(8f, 4f, 6f), wall);
                Block(env, "Pilar_Pasarela_1", new Vector3(2.5f, 1.4f, 24f), new Vector3(0.4f, 2.8f, 0.4f), steel);
                Block(env, "Pilar_Pasarela_2", new Vector3(13.5f, 1.4f, 24f), new Vector3(0.4f, 2.8f, 0.4f), steel);
                Block(env, "Cerco_Norte", new Vector3(0f, 1f, 30f), new Vector3(60f, 2f, 0.2f), fence);
                Block(env, "Cerco_Sur", new Vector3(0f, 1f, -30f), new Vector3(60f, 2f, 0.2f), fence);
                Block(env, "Cerco_Este", new Vector3(30f, 1f, 0f), new Vector3(0.2f, 2f, 60f), fence);
                Block(env, "Cerco_Oeste", new Vector3(-30f, 1f, 0f), new Vector3(0.2f, 2f, 60f), fence);

                EditorUtility.DisplayProgressBar("Lab 6.1", "Colocando modelos del paquete...", 0.45f);
                var props = new GameObject("Modelos_Andahuasi").transform;
                PlaceModel(props, "Torre_alta_tension.obj", new Vector3(20f, 0f, 20f), 90f, false);
                PlaceModel(props, "Tanque_agua.obj", new Vector3(-22f, 0f, 4f), 0f, false);
                foreach (var p in new[] { new Vector3(6f, 0f, 4f), new Vector3(-6f, 0f, 4f), new Vector3(6f, 0f, 17f), new Vector3(-6f, 0f, 17f) })
                    PlaceModel(props, "Poste_luz.FBX", p, 0f, true);
                for (int i = 0; i < 3; i++)
                    PlaceModel(props, "tacho_basura.FBX", new Vector3(3f + i * 0.9f, 0f, -3f), 180f, true);

                // --- Pasos de la guía ---
                EditorUtility.DisplayProgressBar("Lab 6.1", "Agregando OVRCameraRigInteraction...", 0.6f);
                var rig = InstantiatePrefab(RigInteractionGuid, "OVRCameraRigInteraction");
                if (rig == null) throw new Exception("No se encontró el prefab OVRCameraRigInteraction.");
                rig.transform.position = Vector3.zero;
                rig.transform.rotation = Quaternion.identity;
                SetFloorLevel(rig);

                EditorUtility.DisplayProgressBar("Lab 6.1", "Configurando superficies de teletransporte...", 0.75f);
                var surfaces = new List<MeshFilter> { ground.GetComponent<MeshFilter>() };
                foreach (var pl in platforms)
                {
                    Object.DestroyImmediate(pl.GetComponent<BoxCollider>()); // la guía usa Mesh Collider
                    surfaces.Add(pl.GetComponent<MeshFilter>());
                }
                foreach (var s in surfaces)
                {
                    AddMeshCollider(s);
                    AddTeleportSurface(s);
                }

                EditorUtility.DisplayProgressBar("Lab 6.1", "Colocando TeleportHotspot...", 0.9f);
                AddHotspots(platforms.Select(p => p.GetComponent<MeshFilter>()).ToList());

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene, DemoScenePath);
                UpdateBuildSettings();

                EditorUtility.ClearProgressBar();
                var msg = "Listo: Escena_Teleport_Demo creada y puesta como primera escena del build " +
                          "(Escena_Andahuasi quedó desactivada en Build Settings).\n\n" +
                          $"• {surfaces.Count} superficies con Teleport Interactable (piso, losas, plataformas, techo, pasarela).\n" +
                          "• TeleportHotspot sobre cada plataforma.\n" +
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

        static GameObject Block(Transform parent, string name, Vector3 center, Vector3 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent);
            go.transform.position = center;
            go.transform.localScale = size;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            return go;
        }

        // Instancia un modelo del paquete apoyado en el piso. Los FBX vienen acostados (eje Z arriba),
        // así que se prueba la orientación que los deja más altos.
        static void PlaceModel(Transform parent, string file, Vector3 position, float yaw, bool autoUpright)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelsFolder + file);
            if (asset == null)
            {
                Debug.LogWarning($"[Lab 6.1] No se encontró {ModelsFolder + file}; se omite.");
                return;
            }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            go.transform.SetParent(parent);
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) { Object.DestroyImmediate(go); return; }

            var baseRotation = Quaternion.identity;
            if (autoUpright)
            {
                float bestHeight = -1f;
                foreach (var candidate in new[] { Quaternion.identity, Quaternion.Euler(-90f, 0f, 0f), Quaternion.Euler(90f, 0f, 0f), Quaternion.Euler(0f, 0f, 90f) })
                {
                    go.transform.rotation = candidate;
                    float h = Encapsulate(renderers).size.y;
                    if (h > bestHeight + 0.01f) { bestHeight = h; baseRotation = candidate; }
                }
            }
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f) * baseRotation;

            var b = Encapsulate(renderers);
            go.transform.position += new Vector3(position.x - b.center.x, position.y - b.min.y, position.z - b.center.z);
            foreach (var r in renderers) r.shadowCastingMode = ShadowCastingMode.Off;
            Debug.Log($"[Lab 6.1] {file}: alto {b.size.y:0.0} m (si se ve mal de tamaño u orientación, ajústalo en el Inspector).");
        }

        static Material DemoMaterial(string name, Color color)
        {
            if (!AssetDatabase.IsValidFolder(DemoMaterialsFolder))
                AssetDatabase.CreateFolder("Assets/Lab61", "Demo");
            string path = $"{DemoMaterialsFolder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.color = color;
            mat.SetFloat("_Glossiness", 0.1f);
            EditorUtility.SetDirty(mat);
            return mat;
        }
    }
}
