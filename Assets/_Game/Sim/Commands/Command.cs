using System.Collections.Generic;
using System.IO;

namespace Cogworks.Sim
{
    public enum CommandType : byte
    {
        PlaceBuilding = 1,
        RemoveBuilding = 2,
        SetBuildingDirection = 3,
        PlayerJoin = 4,
        TransferItems = 5,
        SetRecipe = 6,
        HandCraft = 7,
        MineTile = 8,
        Crank = 9,
        SetResearch = 10,
    }

    /// <summary>
    /// The only way to change the world. Every peer applies the same commands on the same tick, so
    /// <see cref="Apply"/> must validate deterministically: an invalid command is a no-op everywhere.
    /// </summary>
    public abstract class Command
    {
        /// <summary>Who issued it. Stamped by the host in multiplayer; 0 in single-player.</summary>
        public byte PlayerId;

        public abstract CommandType Type { get; }

        /// <summary>Returns true if the world changed.</summary>
        public abstract bool Apply(World world);

        /// <summary>The issuing player, or null if they don't exist (commands from players need one).</summary>
        protected PlayerState Issuer(World world) => world.Players.Get(PlayerId);

        internal abstract void WriteFields(BinaryWriter writer);
    }

    public sealed class PlaceBuildingCommand : Command
    {
        public readonly ushort SpecId;
        public readonly Int2 Origin;
        public readonly Direction Direction;

        public PlaceBuildingCommand(ushort specId, Int2 origin, Direction direction)
        {
            SpecId = specId;
            Origin = origin;
            Direction = direction;
        }

        public override CommandType Type => CommandType.PlaceBuilding;

        public override bool Apply(World world)
        {
            var spec = BuildingCatalog.Get(SpecId);
            if (spec == null) return false;
            if (world.Sandbox) return world.Buildings.Place(spec, Origin, Direction) != null;

            // Building costs the matching item from the placer's inventory.
            var player = Issuer(world);
            if (player == null || player.Inventory.Count(spec.ItemId) < 1) return false;
            if (world.Buildings.Place(spec, Origin, Direction) == null) return false;
            player.Inventory.Remove(spec.ItemId, 1);
            return true;
        }

        internal override void WriteFields(BinaryWriter w)
        {
            w.Write(SpecId);
            w.Write(Origin.X);
            w.Write(Origin.Y);
            w.Write((byte)Direction);
        }

        internal static PlaceBuildingCommand Read(BinaryReader r) =>
            new PlaceBuildingCommand(r.ReadUInt16(), new Int2(r.ReadInt32(), r.ReadInt32()), (Direction)(r.ReadByte() & 3));
    }

    /// <summary>
    /// Removes whatever building covers <see cref="Tile"/>, refunding it and its contents to the remover.
    /// Refused if the remover's inventory can't hold everything, so nothing is ever destroyed.
    /// </summary>
    public sealed class RemoveBuildingCommand : Command
    {
        public readonly Int2 Tile;

        public RemoveBuildingCommand(Int2 tile)
        {
            Tile = tile;
        }

        public override CommandType Type => CommandType.RemoveBuilding;

        public override bool Apply(World world)
        {
            if (world.Sandbox) return world.Buildings.Remove(Tile);

            var player = Issuer(world);
            var building = world.Buildings.GetTopAt(Tile);
            if (player == null || building == null) return false;
            var refund = new List<ItemStack> { new ItemStack(building.Spec.ItemId, 1) };
            building.Logic.CollectContents(refund);
            if (!player.Inventory.CanInsertAll(refund)) return false;

            world.Buildings.Remove(Tile);
            foreach (var s in refund) player.Inventory.Insert(s.Item, s.Count);
            return true;
        }

        internal override void WriteFields(BinaryWriter w)
        {
            w.Write(Tile.X);
            w.Write(Tile.Y);
        }

        internal static RemoveBuildingCommand Read(BinaryReader r) =>
            new RemoveBuildingCommand(new Int2(r.ReadInt32(), r.ReadInt32()));
    }

    /// <summary>
    /// Sets an absolute facing rather than "rotate once", so a duplicated or reordered command
    /// can't leave peers facing different ways.
    /// </summary>
    public sealed class SetBuildingDirectionCommand : Command
    {
        public readonly Int2 Tile;
        public readonly Direction Direction;

        public SetBuildingDirectionCommand(Int2 tile, Direction direction)
        {
            Tile = tile;
            Direction = direction;
        }

        public override CommandType Type => CommandType.SetBuildingDirection;

        public override bool Apply(World world) => world.Buildings.SetDirection(Tile, Direction);

        internal override void WriteFields(BinaryWriter w)
        {
            w.Write(Tile.X);
            w.Write(Tile.Y);
            w.Write((byte)Direction);
        }

        internal static SetBuildingDirectionCommand Read(BinaryReader r) =>
            new SetBuildingDirectionCommand(new Int2(r.ReadInt32(), r.ReadInt32()), (Direction)(r.ReadByte() & 3));
    }

    /// <summary>Issued by the host (never by clients) when a player connects, so every peer adds them on the same tick.</summary>
    public sealed class PlayerJoinCommand : Command
    {
        public readonly byte JoiningPlayerId;

        public PlayerJoinCommand(byte joiningPlayerId)
        {
            JoiningPlayerId = joiningPlayerId;
        }

        public override CommandType Type => CommandType.PlayerJoin;

        public override bool Apply(World world) => world.Players.Join(JoiningPlayerId);

        internal override void WriteFields(BinaryWriter w) => w.Write(JoiningPlayerId);

        internal static PlayerJoinCommand Read(BinaryReader r) => new PlayerJoinCommand(r.ReadByte());
    }

    /// <summary>Moves items between the issuer's inventory and a building.</summary>
    public sealed class TransferItemsCommand : Command
    {
        public readonly Int2 Tile;
        public readonly ushort Item;
        public readonly int Count;
        /// <summary>True: player → building. False: building → player.</summary>
        public readonly bool IntoBuilding;

        public TransferItemsCommand(Int2 tile, ushort item, int count, bool intoBuilding)
        {
            Tile = tile;
            Item = item;
            Count = count;
            IntoBuilding = intoBuilding;
        }

        public override CommandType Type => CommandType.TransferItems;

        public override bool Apply(World world)
        {
            var player = Issuer(world);
            // Shafts hold nothing, so transfers always target the ground building.
            var building = world.Buildings.GetAt(Tile);
            if (player == null || building == null || Count <= 0) return false;
            if (IntoBuilding)
            {
                int have = System.Math.Min(Count, player.Inventory.Count(Item));
                int moved = have > 0 ? building.Logic.InsertFromPlayer(Item, have) : 0;
                player.Inventory.Remove(Item, moved);
                return moved > 0;
            }
            int room = System.Math.Min(Count, player.Inventory.SpaceFor(Item));
            int taken = room > 0 ? building.Logic.TakeToPlayer(Item, room) : 0;
            player.Inventory.Insert(Item, taken);
            return taken > 0;
        }

        internal override void WriteFields(BinaryWriter w)
        {
            w.Write(Tile);
            w.Write(Item);
            w.Write(Count);
            w.Write(IntoBuilding);
        }

        internal static TransferItemsCommand Read(BinaryReader r) =>
            new TransferItemsCommand(r.ReadInt2(), r.ReadUInt16(), r.ReadInt32(), r.ReadBoolean());
    }

    /// <summary>Chooses an assembler's recipe (0 = none). Items inside go back to the issuer.</summary>
    public sealed class SetRecipeCommand : Command
    {
        public readonly Int2 Tile;
        public readonly ushort RecipeId;

        public SetRecipeCommand(Int2 tile, ushort recipeId)
        {
            Tile = tile;
            RecipeId = recipeId;
        }

        public override CommandType Type => CommandType.SetRecipe;

        public override bool Apply(World world)
        {
            if (!(world.Buildings.GetAt(Tile)?.Logic is AssemblerLogic assembler)) return false;
            var recipe = RecipeCatalog.Get(RecipeId);
            if (RecipeId != 0 && (recipe == null || recipe.Station != CraftStation.Assembler || !world.Research.IsUnlocked(recipe)))
                return false;
            var refund = new List<ItemStack>();
            assembler.SetRecipe(RecipeId, refund);
            var player = Issuer(world);
            if (player != null)
                foreach (var s in refund) player.Inventory.Insert(s.Item, s.Count);
            return true;
        }

        internal override void WriteFields(BinaryWriter w)
        {
            w.Write(Tile);
            w.Write(RecipeId);
        }

        internal static SetRecipeCommand Read(BinaryReader r) => new SetRecipeCommand(r.ReadInt2(), r.ReadUInt16());
    }

    /// <summary>Queues hand crafts, taking ingredients now. Queues as many as the issuer can afford, up to Count.</summary>
    public sealed class HandCraftCommand : Command
    {
        public readonly ushort RecipeId;
        public readonly int Count;

        public HandCraftCommand(ushort recipeId, int count)
        {
            RecipeId = recipeId;
            Count = count;
        }

        public override CommandType Type => CommandType.HandCraft;

        public override bool Apply(World world)
        {
            var player = Issuer(world);
            var recipe = RecipeCatalog.Get(RecipeId);
            if (player == null || recipe == null || recipe.Station != CraftStation.Assembler) return false;
            if (!world.Research.IsUnlocked(recipe)) return false;

            int queued = 0;
            while (queued < Count && player.CraftQueue.Count < PlayerState.MaxCraftQueue && CanAfford(player, recipe))
            {
                foreach (var input in recipe.Inputs) player.Inventory.Remove(input.Item, input.Count);
                player.CraftQueue.Add(recipe.Id);
                queued++;
            }
            return queued > 0;
        }

        static bool CanAfford(PlayerState player, Recipe recipe)
        {
            foreach (var input in recipe.Inputs)
                if (player.Inventory.Count(input.Item) < input.Count) return false;
            return true;
        }

        internal override void WriteFields(BinaryWriter w)
        {
            w.Write(RecipeId);
            w.Write(Count);
        }

        internal static HandCraftCommand Read(BinaryReader r) => new HandCraftCommand(r.ReadUInt16(), r.ReadInt32());
    }

    /// <summary>Hand-mines one unit from an ore tile with nothing built on it.</summary>
    public sealed class MineTileCommand : Command
    {
        public readonly Int2 Tile;

        public MineTileCommand(Int2 tile)
        {
            Tile = tile;
        }

        public override CommandType Type => CommandType.MineTile;

        public override bool Apply(World world)
        {
            var player = Issuer(world);
            if (player == null || world.Buildings.GetAt(Tile) != null) return false;
            if (world.CurrentTick - player.LastMineTick < PlayerState.MineCooldownTicks) return false;
            var ore = world.GetOre(Tile, out int amount);
            var item = ItemCatalog.FromOre(ore);
            if (item == null || amount <= 0 || !player.Inventory.CanInsert(item.Id)) return false;
            world.MineOre(Tile);
            player.Inventory.Insert(item.Id, 1);
            player.LastMineTick = world.CurrentTick;
            return true;
        }

        internal override void WriteFields(BinaryWriter w) => w.Write(Tile);

        internal static MineTileCommand Read(BinaryReader r) => new MineTileCommand(r.ReadInt2());
    }

    /// <summary>Turns a hand crank, powering its network for a few seconds.</summary>
    public sealed class CrankCommand : Command
    {
        public readonly Int2 Tile;

        public CrankCommand(Int2 tile)
        {
            Tile = tile;
        }

        public override CommandType Type => CommandType.Crank;

        public override bool Apply(World world)
        {
            var b = world.Buildings.GetAt(Tile);
            if (b?.Spec != BuildingCatalog.HandCrank) return false;
            ((GeneratorLogic)b.Logic).Crank();
            return true;
        }

        internal override void WriteFields(BinaryWriter w) => w.Write(Tile);

        internal static CrankCommand Read(BinaryReader r) => new CrankCommand(r.ReadInt2());
    }

    /// <summary>Picks the team's current research (0 = none).</summary>
    public sealed class SetResearchCommand : Command
    {
        public readonly ushort TechId;

        public SetResearchCommand(ushort techId)
        {
            TechId = techId;
        }

        public override CommandType Type => CommandType.SetResearch;

        public override bool Apply(World world) => world.Research.SetCurrent(TechCatalog.Get(TechId));

        internal override void WriteFields(BinaryWriter w) => w.Write(TechId);

        internal static SetResearchCommand Read(BinaryReader r) => new SetResearchCommand(r.ReadUInt16());
    }
}
