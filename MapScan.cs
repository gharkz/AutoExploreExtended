using System;

namespace AutoExploreExtended
{
    // Map scans cover the playable bounds only: many maps sit in a much larger grid of void.
    internal static class MapScan
    {
        internal static void Playable(Action<Cell> each)
        {
            var map = EClass._map; var b = map.bounds; var cells = map.cells;
            for (int z = Math.Max(0, b.z); z <= Math.Min(map.Size - 1, b.maxZ); z++)
                for (int x = Math.Max(0, b.x); x <= Math.Min(map.Size - 1, b.maxX); x++)
                    each(cells[x, z]);
        }
    }
}
