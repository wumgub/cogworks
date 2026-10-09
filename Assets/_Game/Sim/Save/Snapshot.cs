using System.IO;
using System.IO.Compression;

namespace Cogworks.Sim
{
    /// <summary>
    /// Complete, compressed sim state. Used for late-join and desync recovery, and as the save file format.
    /// Bump <see cref="Version"/> whenever the layout changes.
    /// </summary>
    public static class Snapshot
    {
        const int Magic = 0x4B574743; // "CGWK"
        public const int Version = 1;

        public static byte[] Save(Simulation sim)
        {
            using var buffer = new MemoryStream();
            using (var gzip = new GZipStream(buffer, CompressionLevel.Fastest, leaveOpen: true))
            using (var w = new BinaryWriter(gzip))
            {
                w.Write(Magic);
                w.Write(Version);
                sim.Write(w);
            }
            return buffer.ToArray();
        }

        public static Simulation Load(byte[] data)
        {
            try
            {
                return LoadUnchecked(data);
            }
            catch (EndOfStreamException e)
            {
                throw new InvalidDataException("Snapshot is truncated", e);
            }
        }

        static Simulation LoadUnchecked(byte[] data)
        {
            using var gzip = new GZipStream(new MemoryStream(data), CompressionMode.Decompress);
            using var r = new BinaryReader(gzip);
            if (r.ReadInt32() != Magic) throw new InvalidDataException("Not a Cogworks snapshot");
            int version = r.ReadInt32();
            if (version != Version) throw new InvalidDataException($"Snapshot version {version}, expected {Version}");
            return Simulation.Read(r);
        }
    }
}
