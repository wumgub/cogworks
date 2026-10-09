using Cogworks.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

namespace Cogworks.Editor
{
    /// <summary>Builds the main scene from code so its wiring is reproducible and reviewable.</summary>
    public static class SceneSetup
    {
        const string ScenePath = "Assets/_Game/Scenes/Main.unity";

        [MenuItem("Cogworks/Build Main Scene")]
        public static void BuildMainScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraGo = new GameObject("Main Camera") { tag = "MainCamera" };
            cameraGo.transform.position = new Vector3(0f, 0f, -10f);
            var camera = cameraGo.AddComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(20, 22, 28, 255);
            cameraGo.AddComponent<UniversalAdditionalCameraData>();
            cameraGo.AddComponent<AudioListener>();
            var pixelPerfect = cameraGo.AddComponent<PixelPerfectCamera>();
            pixelPerfect.assetsPPU = PlaceholderArt.PixelsPerUnit;
            pixelPerfect.refResolutionX = 640;
            pixelPerfect.refResolutionY = 360;
            var rig = cameraGo.AddComponent<CameraRig>();

            var light = new GameObject("Global Light 2D").AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;

            var gridGo = new GameObject("Grid");
            gridGo.AddComponent<Grid>();
            var terrainMap = CreateTilemap(gridGo.transform, "Terrain", 0);
            var oreMap = CreateTilemap(gridGo.transform, "Ore", 1);

            var gameGo = new GameObject("Game");
            var bootstrap = gameGo.AddComponent<GameBootstrap>();
            var worldView = gameGo.AddComponent<WorldView>();

            var playerGo = new GameObject("Player");
            playerGo.transform.position = new Vector3(0.5f, 0.5f, 0f);
            playerGo.AddComponent<SpriteRenderer>().sortingOrder = 10;
            var player = playerGo.AddComponent<PlayerController>();

            Wire(worldView, ("bootstrap", bootstrap), ("terrainMap", terrainMap), ("oreMap", oreMap), ("focus", cameraGo.transform));
            Wire(player, ("bootstrap", bootstrap));
            Wire(rig, ("target", playerGo.transform));

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Debug.Log($"[Cogworks] Built {ScenePath}");
        }

        static Tilemap CreateTilemap(Transform grid, string name, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(grid, false);
            var tilemap = go.AddComponent<Tilemap>();
            go.AddComponent<TilemapRenderer>().sortingOrder = sortingOrder;
            return tilemap;
        }

        static void Wire(Object target, params (string field, Object value)[] refs)
        {
            var so = new SerializedObject(target);
            foreach (var (field, value) in refs)
                so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
