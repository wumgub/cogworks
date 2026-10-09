using System.IO;

namespace Cogworks.Sim
{
    /// <summary>
    /// Per-player sim state (avatar position lives outside the sim). Players stay in the world after
    /// disconnecting so their slot, and later their inventory, survive a rejoin.
    /// </summary>
    public sealed class PlayerState
    {
        public readonly byte Id;

        internal PlayerState(byte id)
        {
            Id = id;
        }

        internal void Write(BinaryWriter w)
        {
            w.Write(Id);
        }

        internal static PlayerState Read(BinaryReader r) => new PlayerState(r.ReadByte());

        internal void AddToHash(ref Fnv64 h)
        {
            h.Add(Id);
        }
    }
}
