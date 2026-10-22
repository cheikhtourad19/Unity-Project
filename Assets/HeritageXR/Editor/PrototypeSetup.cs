using System;
using System.IO;
using System.Linq;
using HeritageXR;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEditor.XR.ARKit;
using UnityEditor.XR.ARSubsystems;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.UIElements;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Management;
using Unity.XR.CoreUtils;

namespace HeritageXR.Editor
{
    public static class PrototypeSetup
    {
        const string Root = "Assets/HeritageXR";
        public const string ScenePath = Root + "/Scenes/Museum.unity";

        // Legacy CLI entry point now imports content without regenerating the scene.
        public static string Run() => CollectionImport.Sync();

        static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) { asset = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(asset, path); }
            return asset;
        }
        static void Copy(string source, string destination)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            File.Copy(source, destination, true);
            AssetDatabase.ImportAsset(destination, ImportAssetOptions.ForceSynchronousImport);
        }
        static void ImportTexture(string path, int maxSize)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.maxTextureSize = maxSize; importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.SaveAndReimport();
        }

        static GameObject ImportModel(string id, JToken metadata)
        {
            string folder = Root + "/Models/" + id;
            Directory.CreateDirectory(folder);
            string objPath = folder + "/" + Path.GetFileName((string)metadata["sourcePath"]);
            foreach (var source in metadata["materialPaths"].Values<string>().Concat(metadata["texturePaths"].Values<string>()))
                Copy(source, folder + "/" + Path.GetFileName(source));
            Copy((string)metadata["sourcePath"], objPath);
            var importer = (ModelImporter)AssetImporter.GetAtPath(objPath);
            importer.materialImportMode = ModelImporterMaterialImportMode.None; importer.importAnimation = false;
            importer.isReadable = false; importer.SaveAndReimport();
            string texturePath = folder + "/" + Path.GetFileName((string)metadata["texturePaths"][0]);
            ImportTexture(texturePath, id == "bm_ea24" ? 4096 : 1024);
            var materialPath = folder + "/Museum.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null) { material = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(material, materialPath); }
            material.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            material.SetFloat("_Glossiness", .12f); EditorUtility.SetDirty(material);
            var root = new GameObject(id);
            var mesh = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(objPath));
            mesh.transform.SetParent(root.transform, false);
            var renderers = mesh.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) throw new Exception("No mesh in " + objPath);
            Bounds bounds = renderers[0].bounds;
            foreach (var renderer in renderers) { bounds.Encapsulate(renderer.bounds); renderer.sharedMaterials = Enumerable.Repeat(material, renderer.sharedMaterials.Length).ToArray(); }
            float scale = .20f / Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            mesh.transform.localScale *= scale;
            mesh.transform.localPosition = new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z) * scale;
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, folder + "/Artwork.prefab");
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        static void ConfigureXR()
        {
            var settings = LoadOrCreate<XRGeneralSettingsPerBuildTarget>(Root + "/Settings/XRGeneralSettings.asset");
            EditorBuildSettings.AddConfigObject(XRGeneralSettings.settingsKey, settings, true);
            if (!settings.HasSettingsForBuildTarget(BuildTargetGroup.iOS)) settings.CreateDefaultSettingsForBuildTarget(BuildTargetGroup.iOS);
            var ios = settings.SettingsForBuildTarget(BuildTargetGroup.iOS);
            if (ios.Manager == null) settings.CreateDefaultManagerSettingsForBuildTarget(BuildTargetGroup.iOS);
            ios = settings.SettingsForBuildTarget(BuildTargetGroup.iOS);
            if (!XRPackageMetadataStore.AssignLoader(ios.Manager, "UnityEngine.XR.ARKit.ARKitLoader", BuildTargetGroup.iOS))
                throw new Exception("ARKit loader assignment failed");
            ios.InitManagerOnStart = true;
            EditorUtility.SetDirty(settings); EditorUtility.SetDirty(ios); EditorUtility.SetDirty(ios.Manager);
            var arkit = ARKitSettings.GetOrCreateSettings();
            if (!AssetDatabase.Contains(arkit)) AssetDatabase.CreateAsset(arkit, Root + "/Settings/ARKitSettings.asset");
            ARKitSettings.currentSettings = arkit;
        }

        static void CreateScene(MuseumCollection collection, XRReferenceImageLibrary library)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var fallback = new GameObject("Interface Camera").AddComponent<Camera>();
            fallback.tag = "MainCamera"; fallback.clearFlags = CameraClearFlags.SolidColor;
            fallback.backgroundColor = new Color(.961f, .953f, .929f); fallback.cullingMask = 0;
            fallback.gameObject.AddComponent<AudioListener>();
            var light = new GameObject("Museum Light").AddComponent<Light>();
            light.type = LightType.Directional; light.intensity = 1.15f; light.transform.rotation = Quaternion.Euler(35, -30, 0);
            RenderSettings.ambientLight = new Color(.68f, .70f, .68f);
            var sessionGO = new GameObject("AR Session");
            var session = sessionGO.AddComponent<ARSession>(); session.enabled = false;
            sessionGO.AddComponent<ARInputManager>().enabled = false;
            var rig = new GameObject("AR Origin"); rig.SetActive(false);
            var origin = rig.AddComponent<XROrigin>(); origin.CameraYOffset = 0;
            var offset = new GameObject("Camera Offset"); offset.transform.SetParent(rig.transform, false); origin.CameraFloorOffsetObject = offset;
            var cameraGO = new GameObject("AR Camera"); cameraGO.transform.SetParent(offset.transform, false);
            var camera = cameraGO.AddComponent<Camera>(); camera.nearClipPlane = .03f; camera.farClipPlane = 30f; camera.depth = 1;
            camera.cullingMask = ~(1 << 30); cameraGO.AddComponent<ARCameraManager>(); cameraGO.AddComponent<ARCameraBackground>();
            var pose = cameraGO.AddComponent<TrackedPoseDriver>();
            var position = new InputAction("AR Position", InputActionType.Value, "<HandheldARInputDevice>/devicePosition", expectedControlType: "Vector3");
            position.AddBinding("<XRHMD>/centerEyePosition"); pose.positionInput = new InputActionProperty(position);
            var rotation = new InputAction("AR Rotation", InputActionType.Value, "<HandheldARInputDevice>/deviceRotation", expectedControlType: "Quaternion");
            rotation.AddBinding("<XRHMD>/centerEyeRotation"); pose.rotationInput = new InputActionProperty(rotation);
            pose.trackingStateInput = new InputActionProperty(new InputAction("AR Tracking", InputActionType.Value, "<HandheldARInputDevice>/trackingState"));
            origin.Camera = camera;
            var images = rig.AddComponent<ARTrackedImageManager>(); images.referenceLibrary = library;
            images.requestedMaxNumberOfMovingImages = 1; images.enabled = false;
            var appGO = new GameObject("HeritageXR");
            var ar = appGO.AddComponent<MuseumAR>(); ar.collection = collection; ar.session = session;
            ar.cameraRig = rig; ar.imageManager = images; ar.fallbackCamera = fallback;
            var viewerGO = new GameObject("3D Viewer"); viewerGO.transform.position = new Vector3(100, 0, 0);
            var viewer = viewerGO.AddComponent<ArtworkViewer>();
            string rtPath = Root + "/Settings/ModelPreview.renderTexture";
            var rt = AssetDatabase.LoadAssetAtPath<RenderTexture>(rtPath);
            if (rt == null) { rt = new RenderTexture(1536, 1536, 24); AssetDatabase.CreateAsset(rt, rtPath); }
            var previewGO = new GameObject("Preview Camera"); previewGO.transform.SetParent(viewerGO.transform, false);
            previewGO.transform.localPosition = new Vector3(0, .13f, .42f);
            previewGO.transform.rotation = Quaternion.LookRotation(viewerGO.transform.position + new Vector3(0, .09f, 0) - previewGO.transform.position);
            var preview = previewGO.AddComponent<Camera>(); preview.targetTexture = rt; preview.cullingMask = 1 << 30;
            preview.fieldOfView = 34; preview.nearClipPlane = .01f; preview.clearFlags = CameraClearFlags.SolidColor; preview.backgroundColor = fallback.backgroundColor;
            previewGO.SetActive(false); viewer.previewCamera = preview; viewer.texture = rt;
            var panel = LoadOrCreate<PanelSettings>(Root + "/Settings/MobilePanel.asset");
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize; panel.referenceResolution = new Vector2Int(430, 932);
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight; panel.match = 0;
            panel.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(Root + "/UI/RuntimeTheme.tss");
            EditorUtility.SetDirty(panel);
            var uiGO = new GameObject("Museum Interface");
            uiGO.AddComponent<UIDocument>().panelSettings = panel;
            var app = uiGO.AddComponent<MuseumApp>(); app.collection = collection; app.ar = ar; app.viewer = viewer;
            app.theme = AssetDatabase.LoadAssetAtPath<StyleSheet>(Root + "/UI/Museum.uss");
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        public static string Verify() => CollectionImport.Verify();
    }
}
