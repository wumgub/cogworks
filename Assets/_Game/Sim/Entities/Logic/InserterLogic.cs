using System.Collections.Generic;
using System.IO;

namespace Cogworks.Sim
{
    /// <summary>
    /// Picks up from the tile behind it and drops onto the tile in front (it faces where it drops).
    /// Only grabs items the target can take right now, so it never gets stuck holding junk.
    /// </summary>
    public sealed class InserterLogic : BuildingLogic
    {
        public const int SwingTicks = 20;

        public enum State : byte
        {
            Idle,
            ToTarget,
            Dropping,
            Returning,
        }

        public State Phase { get; private set; }
        public ushort Held { get; private set; }
        public int Timer { get; private set; }

        /// <summary>0..1 arm position from source (0) to target (1), for animation.</summary>
        public float ArmPosition => Phase switch
        {
            State.ToTarget => 1f - Timer / (float)SwingTicks,
            State.Dropping => 1f,
            State.Returning => Timer / (float)SwingTicks,
            _ => 0f,
        };

        public override void Tick(World world)
        {
            switch (Phase)
            {
                case State.Idle:
                    var source = world.Buildings.GetAt(Building.BackTile())?.Logic;
                    var target = world.Buildings.GetAt(Building.FrontTile())?.Logic;
                    if (source == null || target == null || source == target) return;
                    Int2 me = Building.Origin;
                    if (source.TryTake(i => target.CanAccept(i, me), out ushort item))
                    {
                        Held = item;
                        Phase = State.ToTarget;
                        Timer = SwingTicks;
                    }
                    break;
                case State.ToTarget:
                    if (--Timer <= 0) Phase = State.Dropping;
                    break;
                case State.Dropping:
                    // If the target was removed or filled meanwhile, keep holding until it frees up.
                    var dest = world.Buildings.GetAt(Building.FrontTile())?.Logic;
                    if (dest != null && dest.CanAccept(Held, Building.Origin))
                    {
                        dest.Insert(Held, Building.Origin);
                        Held = ItemCatalog.None;
                        Phase = State.Returning;
                        Timer = SwingTicks;
                    }
                    break;
                case State.Returning:
                    if (--Timer <= 0) Phase = State.Idle;
                    break;
            }
        }

        public override void CollectContents(List<ItemStack> into) => AddContents(into, Held, 1);

        internal override void Write(BinaryWriter w)
        {
            w.Write((byte)Phase);
            w.Write(Held);
            w.Write((byte)Timer);
        }

        internal override void Read(BinaryReader r)
        {
            Phase = (State)r.ReadByte();
            Held = r.ReadUInt16();
            Timer = r.ReadByte();
        }

        internal override void AddToHash(ref Fnv64 h)
        {
            h.Add((byte)Phase);
            h.Add(Held);
            h.Add(Timer);
        }
    }
}
