namespace KCAccess
{
    /// <summary>The few game facts that moved between game versions, in one place.</summary>
    internal static class GameCompat
    {
        /// <summary>Trees on a tile. Since the November 2026 game update the tree system keeps this count, not the cell.</summary>
        internal static int Trees(this Cell c)
        {
            var trees = TreeSystem.inst;
            return c != null && trees != null && trees.cellData != null ? trees.ItemAmount(c) : 0;
        }
    }
}
