namespace Cogworks.Sim
{
    internal static class ItemPush
    {
        /// <summary>
        /// Hands one item to whatever is on <paramref name="tile"/>, travelling in direction <paramref name="travel"/>.
        /// Belts take it at their entry edge if they run the same way, or from the side if they run across;
        /// a belt running back toward the source refuses it.
        /// </summary>
        public static bool Into(World world, Int2 tile, Direction travel, ushort item, Int2 fromTile)
        {
            var target = world.Buildings.GetAt(tile)?.Logic;
            if (target == null) return false;
            if (target is ConveyorLogic belt)
            {
                var d = belt.Building.Direction;
                if (d == travel.Opposite()) return false;
                return d == travel ? belt.TryInsertAtEntry(item) : belt.TryInsertAt(ConveyorLogic.MidPos, item);
            }
            if (!target.CanAccept(item, fromTile)) return false;
            target.Insert(item, fromTile);
            return true;
        }
    }
}
