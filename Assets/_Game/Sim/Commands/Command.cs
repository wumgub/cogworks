using System.IO;

namespace Cogworks.Sim
{
    public enum CommandType : byte
    {
        PlaceBuilding = 1,
        RemoveBuilding = 2,
        SetBuildingDirection = 3,
        PlayerJoin = 4,
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

        public override bool Apply(World world) =>
            world.Buildings.Place(BuildingCatalog.Get(SpecId), Origin, Direction) != null;

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

    /// <summary>Removes whatever building covers <see cref="Tile"/>.</summary>
    public sealed class RemoveBuildingCommand : Command
    {
        public readonly Int2 Tile;

        public RemoveBuildingCommand(Int2 tile)
        {
            Tile = tile;
        }

        public override CommandType Type => CommandType.RemoveBuilding;

        public override bool Apply(World world) => world.Buildings.Remove(Tile);

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
}
