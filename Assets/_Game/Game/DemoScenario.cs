using System.Collections;
using System.IO;
using Cogworks.Sim;
using Cogworks.View;
using UnityEngine;

namespace Cogworks.Game
{
    /// <summary>
    /// Builds a small working factory in a sandbox world and optionally captures screenshots of it and of
    /// each UI panel. Used for visual checks of builds without anyone at the keyboard.
    /// </summary>
    public static class DemoScenario
    {
        /// <summary>
        /// Steam engine → drill on iron → belt → inserter → furnace → inserter → chest, plus an assembler
        /// on the same power network via shafts, and a lab researching Automation.
        /// Returns a good camera focus point.
        /// </summary>
        public static Vector2 Build(Simulation sim)
        {
            var w = sim.World;
            var at = FindDrillSpot(w);
            var b = w.Buildings;

            var drill = b.Place(BuildingCatalog.Drill, at, Direction.East);
            for (int x = 2; x <= 7; x++) b.Place(BuildingCatalog.Conveyor, new Int2(at.X + x, at.Y), Direction.East);
            b.Place(BuildingCatalog.Inserter, new Int2(at.X + 8, at.Y), Direction.East);
            var furnace = b.Place(BuildingCatalog.Furnace, new Int2(at.X + 9, at.Y), Direction.North);
            b.Place(BuildingCatalog.Inserter, new Int2(at.X + 11, at.Y), Direction.East);
            b.Place(BuildingCatalog.Chest, new Int2(at.X + 12, at.Y), Direction.North);
            furnace?.Logic.InsertFromPlayer(ItemCatalog.Coal.Id, 30);

            var engine = b.Place(BuildingCatalog.SteamEngine, new Int2(at.X - 2, at.Y), Direction.North);
            engine?.Logic.InsertFromPlayer(ItemCatalog.Coal.Id, 50);
            b.Place(BuildingCatalog.Shaft, new Int2(at.X - 2, at.Y + 2), Direction.North);
            b.Place(BuildingCatalog.Shaft, new Int2(at.X - 2, at.Y + 3), Direction.North);
            var assembler = b.Place(BuildingCatalog.Assembler, new Int2(at.X - 3, at.Y + 4), Direction.North);
            if (assembler?.Logic is AssemblerLogic asm)
            {
                asm.SetRecipe(RecipeCatalog.IronGear.Id, new System.Collections.Generic.List<ItemStack>());
                asm.InsertFromPlayer(ItemCatalog.IronPlate.Id, 60);
            }

            var lab = b.Place(BuildingCatalog.Lab, new Int2(at.X + 4, at.Y + 3), Direction.North);
            lab?.Logic.InsertFromPlayer(ItemCatalog.RedScience.Id, 10);
            w.Research.SetCurrent(TechCatalog.Automation);

            // A crank on its own network, so its cog shows idle vs turning.
            b.Place(BuildingCatalog.HandCrank, new Int2(at.X + 4, at.Y - 2), Direction.North);

            if (drill == null) Debug.LogWarning("[Cogworks] Demo: couldn't place drill");
            return new Vector2(at.X + 5, at.Y + 2);
        }

        static Int2 FindDrillSpot(World w)
        {
            for (int r = 0; r < 60; r++)
            for (int y = -r; y <= r; y++)
            for (int x = -r; x <= r; x++)
            {
                var t = new Int2(x, y);
                if (w.GetOre(t, out _) != Ore.Iron) continue;
                if (!w.Buildings.CanPlace(BuildingCatalog.Drill, t, Direction.East)) continue;
                if (!Clear(w, new Int2(t.X - 3, t.Y - 2), 17, 9, t)) continue;
                return t;
            }
            return new Int2(0, 0);
        }

        // Dry and empty, apart from ore under the drill itself.
        static bool Clear(World w, Int2 origin, int width, int height, Int2 drill)
        {
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                var t = new Int2(origin.X + x, origin.Y + y);
                if (w.GetTerrain(t) == TerrainType.Water || w.Buildings.GetAt(t) != null) return false;
                bool underDrill = t.X >= drill.X && t.X < drill.X + 2 && t.Y >= drill.Y && t.Y < drill.Y + 2;
                if (!underDrill && w.GetOre(t, out _) != Ore.None && t.X > drill.X + 1) return false;
            }
            return true;
        }

        /// <summary>
        /// Captures the world and each panel, then quits. Renders offscreen (camera and UI into separate
        /// render textures, composited on the CPU) so it works headless or with the screen locked.
        /// </summary>
        public static IEnumerator CaptureShots(string dir, GameBootstrap bootstrap, PanelsController panels, CameraRig rig)
        {
            Directory.CreateDirectory(dir);
            const int width = 1280, height = 720;
            var cam = rig.GetComponent<Camera>();
            var worldRT = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var uiRT = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = worldRT;
            foreach (var doc in Object.FindObjectsByType<UnityEngine.UIElements.UIDocument>(FindObjectsInactive.Exclude))
            {
                var settings = doc.panelSettings;
                settings.targetTexture = uiRT;
                settings.clearColor = true;
                settings.colorClearValue = new Color(0, 0, 0, 0);
            }
            rig.SetZoom(1);

            yield return Wait(10f);
            yield return Shot(Path.Combine(dir, "1_world.png"), cam, worldRT, uiRT);

            panels.OpenInventory();
            yield return Wait(1f);
            yield return Shot(Path.Combine(dir, "2_inventory.png"), cam, worldRT, uiRT);

            Building assembler = null, furnace = null;
            foreach (var b in bootstrap.Sim.World.Buildings.All)
            {
                if (b.Spec == BuildingCatalog.Assembler) assembler = b;
                if (b.Spec == BuildingCatalog.Furnace) furnace = b;
            }
            if (assembler != null)
            {
                panels.OpenMachine(assembler);
                yield return Wait(1f);
                yield return Shot(Path.Combine(dir, "3_assembler.png"), cam, worldRT, uiRT);
            }
            if (furnace != null)
            {
                panels.OpenMachine(furnace);
                yield return Wait(1f);
                yield return Shot(Path.Combine(dir, "4_furnace.png"), cam, worldRT, uiRT);
            }

            panels.OpenResearch();
            yield return Wait(1f);
            yield return Shot(Path.Combine(dir, "5_research.png"), cam, worldRT, uiRT);

            panels.Close();
            rig.SetZoom(3);
            yield return Wait(1f);
            yield return Shot(Path.Combine(dir, "6_zoomed_out.png"), cam, worldRT, uiRT);
            Debug.Log("[Cogworks] Screenshots done");
            Application.Quit();
        }

        // Frame-counted rather than wall-clock so throttled or headless players still progress.
        static IEnumerator Wait(float seconds)
        {
            int frames = Mathf.CeilToInt(seconds * 60f);
            for (int i = 0; i < frames; i++) yield return null;
        }

        static IEnumerator Shot(string path, Camera cam, RenderTexture worldRT, RenderTexture uiRT)
        {
            yield return null;
            cam.Render();
            var world = Read(worldRT);
            var ui = Read(uiRT);
            var a = world.GetPixels32();
            var b = ui.GetPixels32();
            for (int i = 0; i < a.Length; i++)
            {
                int alpha = b[i].a;
                a[i] = new Color32(
                    (byte)((b[i].r * alpha + a[i].r * (255 - alpha)) / 255),
                    (byte)((b[i].g * alpha + a[i].g * (255 - alpha)) / 255),
                    (byte)((b[i].b * alpha + a[i].b * (255 - alpha)) / 255),
                    255);
            }
            world.SetPixels32(a);
            File.WriteAllBytes(path, world.EncodeToPNG());
            Object.Destroy(world);
            Object.Destroy(ui);
            Debug.Log($"[Cogworks] Saved {path}");
        }

        static Texture2D Read(RenderTexture rt)
        {
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            return tex;
        }
    }
}
