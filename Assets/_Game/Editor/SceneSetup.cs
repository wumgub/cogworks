using System.IO;
using Cogworks.Game;
using Cogworks.View;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;
using UnityEngine.UIElements;

namespace Cogworks.Editor
{
    /// <summary>Builds the main scene (and the assets it needs) from code so its wiring is reproducible and reviewable.</summary>
    public static class SceneSetup
    {
        const string ScenePath = "Assets/_Game/Scenes/Main.unity";
        const string UiDir = "Assets/_Game/UI";
        const string PrefabDir = "Assets/_Game/Prefabs";
        const string AvatarPrefabPath = PrefabDir + "/PlayerAvatar.prefab";
        const string NetworkPrefabsListPath = "Assets/DefaultNetworkPrefabs.asset";

        [MenuItem("Cogworks/Build Main Scene")]
        public static void BuildMainScene()
        {
            // Clients must keep simulating while their window is in the background.
            PlayerSettings.runInBackground = true;
            DisablePostProcessing();
            var avatarPrefab = BuildAvatarPrefab();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraGo = new GameObject("Main Camera") { tag = "MainCamera" };
            cameraGo.transform.position = new Vector3(0f, 0f, -10f);
            var camera = cameraGo.AddComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(20, 22, 28, 255);
            cameraGo.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = false;
            cameraGo.AddComponent<AudioListener>();
            var pixelPerfect = cameraGo.AddComponent<PixelPerfectCamera>();
            pixelPerfect.assetsPPU = GameArt.PixelsPerUnit;
            pixelPerfect.refResolutionX = 640;
            pixelPerfect.refResolutionY = 360;
            var rig = cameraGo.AddComponent<CameraRig>();

            var light = new GameObject("Global Light 2D").AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;

            var gridGo = new GameObject("Grid");
            gridGo.AddComponent<Grid>();
            var terrainMap = CreateTilemap(gridGo.transform, "Terrain", 0);
            // Soft borders: one layer per terrain that can overlay others (TerrainBlend.Order[1..]).
            var blendMaps = new Tilemap[TerrainBlend.Order.Length];
            for (int i = 1; i < blendMaps.Length; i++)
                blendMaps[i] = CreateTilemap(gridGo.transform, "Blend_" + TerrainBlend.Order[i], i);
            var oreMap = CreateTilemap(gridGo.transform, "Ore", TerrainBlend.Order.Length);

            var gameGo = new GameObject("Game");
            var bootstrap = gameGo.AddComponent<GameBootstrap>();
            var worldView = gameGo.AddComponent<WorldView>();
            var buildingsView = gameGo.AddComponent<BuildingsView>();
            var build = gameGo.AddComponent<BuildController>();
            var beltItems = gameGo.AddComponent<BeltItemsView>();
            var sound = gameGo.AddComponent<SoundFx>();

            var panelSettings = GetOrCreatePanelSettings();
            var hudGo = new GameObject("HUD");
            var hudDoc = hudGo.AddComponent<UIDocument>();
            hudDoc.panelSettings = panelSettings;
            hudDoc.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UiDir + "/Hud.uxml");
            var hotbar = hudGo.AddComponent<HotbarView>();

            var panelsGo = new GameObject("Panels");
            var panelsDoc = panelsGo.AddComponent<UIDocument>();
            panelsDoc.panelSettings = panelSettings;
            panelsDoc.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UiDir + "/Panels.uxml");
            panelsDoc.sortingOrder = 5;
            var panels = panelsGo.AddComponent<PanelsController>();

            var menuGo = new GameObject("Menu");
            var menuDoc = menuGo.AddComponent<UIDocument>();
            menuDoc.panelSettings = panelSettings;
            menuDoc.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UiDir + "/Menu.uxml");
            menuDoc.sortingOrder = 10;
            var menu = menuGo.AddComponent<MenuController>();

            var netGo = new GameObject("Network");
            var transport = netGo.AddComponent<UnityTransport>();
            var network = netGo.AddComponent<NetworkManager>();
            network.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = transport,
                PlayerPrefab = avatarPrefab.gameObject,
                ConnectionApproval = true,
                TickRate = 60,
            };
            var prefabsList = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(NetworkPrefabsListPath);
            if (prefabsList != null)
            {
                if (!prefabsList.Contains(avatarPrefab.gameObject)) prefabsList.Add(new NetworkPrefab { Prefab = avatarPrefab.gameObject });
                EditorUtility.SetDirty(prefabsList);
                network.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(prefabsList);
            }
            var netGame = netGo.AddComponent<NetworkGame>();
            var launcher = netGo.AddComponent<CommandLineLauncher>();

            Wire(worldView, ("bootstrap", bootstrap), ("terrainMap", terrainMap), ("oreMap", oreMap), ("focus", cameraGo.transform));
            WireArray(worldView, "blendMaps", blendMaps);
            Wire(buildingsView, ("bootstrap", bootstrap), ("worldCamera", camera));
            Wire(build, ("bootstrap", bootstrap), ("worldCamera", camera), ("hotbar", hotbar));
            Wire(hotbar, ("build", build));
            Wire(beltItems, ("bootstrap", bootstrap), ("worldCamera", camera));
            Wire(sound, ("bootstrap", bootstrap), ("build", build));
            Wire(panels, ("bootstrap", bootstrap), ("build", build));
            Wire(netGame, ("bootstrap", bootstrap), ("network", network), ("transport", transport),
                ("avatarPrefab", avatarPrefab), ("cameraRig", rig), ("build", build));
            Wire(menu, ("game", netGame), ("bootstrap", bootstrap), ("build", build), ("panels", panels), ("hud", hotbar));
            Wire(launcher, ("game", netGame), ("bootstrap", bootstrap), ("panels", panels), ("cameraRig", rig), ("menu", menu));

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log($"[Cogworks] Built {ScenePath}");
        }

        /// <summary>
        /// Pixel art doesn't use post-processing; without this the 2D renderer sets up passes whose shaders
        /// are stripped from builds and logs warnings every frame.
        /// </summary>
        static void DisablePostProcessing()
        {
            var renderer = AssetDatabase.LoadAssetAtPath<Object>("Assets/Settings/Renderer2D.asset");
            if (renderer == null) return;
            var so = new SerializedObject(renderer);
            var prop = so.FindProperty("m_PostProcessData");
            if (prop != null && prop.objectReferenceValue != null)
            {
                prop.objectReferenceValue = null;
                so.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssets();
            }
        }

        static PlayerAvatar BuildAvatarPrefab()
        {
            Directory.CreateDirectory(PrefabDir);
            var go = new GameObject("PlayerAvatar");
            go.AddComponent<SpriteRenderer>().sortingOrder = 15;
            go.AddComponent<PlayerController>();
            go.AddComponent<PlayerSpriteAnimator>();
            go.AddComponent<NetworkObject>();
            var nt = go.AddComponent<NetworkTransform>();
            // The owner moves its own avatar; only 2D position needs to replicate.
            nt.AuthorityMode = NetworkTransform.AuthorityModes.Owner;
            nt.SyncPositionZ = false;
            nt.SyncRotAngleX = nt.SyncRotAngleY = nt.SyncRotAngleZ = false;
            nt.SyncScaleX = nt.SyncScaleY = nt.SyncScaleZ = false;
            nt.Interpolate = true;
            go.AddComponent<PlayerAvatar>();

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, AvatarPrefabPath);
            Object.DestroyImmediate(go);
            return prefab.GetComponent<PlayerAvatar>();
        }

        static PanelSettings GetOrCreatePanelSettings()
        {
            string path = UiDir + "/GamePanelSettings.asset";
            var settings = AssetDatabase.LoadAssetAtPath<PanelSettings>(path);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(settings, path);
            }
            settings.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(UiDir + "/GameTheme.tss");
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1280, 720);
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = 0.5f;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            return settings;
        }

        static Tilemap CreateTilemap(Transform grid, string name, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(grid, false);
            var tilemap = go.AddComponent<Tilemap>();
            go.AddComponent<TilemapRenderer>().sortingOrder = sortingOrder;
            return tilemap;
        }

        static void WireArray(Object target, string field, Object[] values)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field) ?? throw new System.ArgumentException($"{target.GetType().Name} has no field '{field}'");
            prop.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void Wire(Object target, params (string field, Object value)[] refs)
        {
            var so = new SerializedObject(target);
            foreach (var (field, value) in refs)
            {
                var prop = so.FindProperty(field);
                if (prop == null) throw new System.ArgumentException($"{target.GetType().Name} has no serialized field '{field}'");
                prop.objectReferenceValue = value;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
