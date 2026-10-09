using System.IO;

namespace Cogworks.Sim
{
    /// <summary>Small helpers so sim types read and write the same way everywhere.</summary>
    public static class BinaryExt
    {
        public static void Write(this BinaryWriter w, Int2 v)
        {
            w.Write(v.X);
            w.Write(v.Y);
        }

        public static Int2 ReadInt2(this BinaryReader r) => new Int2(r.ReadInt32(), r.ReadInt32());
    }
}
