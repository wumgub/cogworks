using Cogworks.Sim;

namespace Cogworks.View
{
    /// <summary>Human-readable status lines shared by the hover tooltip and machine panels.</summary>
    public static class Describe
    {
        public static string Network(World world, Building b)
        {
            var net = world.Kinetics.NetworkOf(b);
            if (net == null) return "";
            if (net.Overstressed) return $"OVERSTRESSED  {net.Usage}/{net.Capacity} SU";
            if (net.Rpm == 0) return "No rotation";
            return $"{net.Rpm} rpm  ·  {net.Usage}/{net.Capacity} SU";
        }

        public static string Status(World world, Building b)
        {
            switch (b.Logic)
            {
                case DrillLogic d:
                    if (d.Output != ItemCatalog.None) return "Output blocked";
                    if (d.OutOfOre) return "Out of ore";
                    return d.LastRpm > 0 ? $"Mining at {d.LastRpm} rpm" : "Needs rotation";
                case FurnaceLogic f:
                    if (f.CurrentRecipe == null) return "Needs ore or stone";
                    if (f.Fuel.IsEmpty && !f.Burning) return "Needs fuel (coal)";
                    return f.Progress > 0 ? $"Smelting {ItemCatalog.NameOf(f.CurrentRecipe.Output.Item)}" : "Waiting";
                case AssemblerLogic a:
                    if (a.Recipe == null) return "No recipe (click to choose)";
                    if (a.LastRpm <= 0) return "Needs rotation";
                    return a.Crafting ? $"Crafting {a.Recipe.DisplayName}" : "Waiting for ingredients";
                case GeneratorLogic g:
                    if (b.Spec == BuildingCatalog.HandCrank) return g.Active ? "Turning" : "Press E to crank";
                    if (b.Spec == BuildingCatalog.SteamEngine) return g.Active ? "Running" : g.Fuel.IsEmpty ? "Needs coal" : "Idle (no load)";
                    return "Turning";
                case LabLogic l:
                    if (world.Research.Current == null) return "No research selected (T)";
                    return l.Working != ItemCatalog.None ? $"Researching {world.Research.Current.DisplayName}" : "Needs science packs";
                case InserterLogic i:
                    return i.Held != ItemCatalog.None ? $"Holding {ItemCatalog.NameOf(i.Held)}" : "Idle";
                case ConveyorLogic c:
                    return $"{c.Items.Count} item{(c.Items.Count == 1 ? "" : "s")}";
                case SplitterLogic _:
                    return "Shares items between front, left and right";
                case UndergroundEntranceLogic u:
                    var exit = u.FindExit(world);
                    return exit == null ? $"No exit facing the same way within {UndergroundEntranceLogic.MaxGap} tiles"
                        : $"Linked  ·  {u.InTransit} item{(u.InTransit == 1 ? "" : "s")} underground";
                case UndergroundExitLogic _:
                    return "Items come out here";
                case ChestLogic ch:
                    return ch.Inventory.IsEmpty ? "Empty" : "Storing items";
                default:
                    return "";
            }
        }

        /// <summary>One line for the hover tooltip.</summary>
        public static string Hover(World world, Building b)
        {
            string status = Status(world, b);
            string net = b.Spec.Kinetic != KineticRole.None ? Network(world, b) : "";
            string line = b.Spec.DisplayName;
            if (status.Length > 0) line += "  —  " + status;
            if (net.Length > 0) line += "  ·  " + net;
            return line;
        }

        public static string Tile(World world, Int2 tile)
        {
            var ore = world.GetOre(tile, out int amount);
            if (ore == Ore.None) return "";
            return $"{ItemCatalog.FromOre(ore).DisplayName} ({amount})  —  hold LMB to mine";
        }
    }
}
