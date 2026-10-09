using System.IO;

namespace Cogworks.Sim
{
    /// <summary>Binary encoding for commands, shared by networking and replay logs.</summary>
    public static class CommandCodec
    {
        public static void Write(BinaryWriter writer, Command command)
        {
            writer.Write((byte)command.Type);
            writer.Write(command.PlayerId);
            command.WriteFields(writer);
        }

        public static Command Read(BinaryReader reader)
        {
            var type = (CommandType)reader.ReadByte();
            byte playerId = reader.ReadByte();
            Command command = type switch
            {
                CommandType.PlaceBuilding => PlaceBuildingCommand.Read(reader),
                CommandType.RemoveBuilding => RemoveBuildingCommand.Read(reader),
                CommandType.SetBuildingDirection => SetBuildingDirectionCommand.Read(reader),
                CommandType.PlayerJoin => PlayerJoinCommand.Read(reader),
                CommandType.TransferItems => TransferItemsCommand.Read(reader),
                CommandType.SetRecipe => SetRecipeCommand.Read(reader),
                CommandType.HandCraft => HandCraftCommand.Read(reader),
                CommandType.MineTile => MineTileCommand.Read(reader),
                CommandType.Crank => CrankCommand.Read(reader),
                CommandType.SetResearch => SetResearchCommand.Read(reader),
                _ => throw new InvalidDataException($"Unknown command type {(byte)type}"),
            };
            command.PlayerId = playerId;
            return command;
        }

        public static byte[] ToBytes(Command command)
        {
            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream)) Write(writer, command);
            return stream.ToArray();
        }

        public static Command FromBytes(byte[] bytes)
        {
            using var reader = new BinaryReader(new MemoryStream(bytes));
            return Read(reader);
        }
    }
}
