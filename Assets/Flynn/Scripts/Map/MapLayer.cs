namespace Flynn.Map
{
    /// <summary>
    /// The fixed back-to-front layer stack for a level. Render/sort order is derived
    /// from the enum value (see MapLayerManager: order = (int)layer * 100) so layers
    /// never interleave by accident.
    ///
    /// In the layered platformer, gameplay collision lives on Ground + Platforms;
    /// Background and Foreground are visual only.
    /// </summary>
    public enum MapLayer
    {
        Background = 0, // parallax sky / distant scenery, no collision
        Ground     = 1, // the base walkable floor (collision)
        Platforms  = 2, // floating platforms jumped between (collision)
        Props      = 3, // resource nodes, crates, decals drawn above the ground
        Foreground = 4, // grass tufts / occluders drawn in front of the player
    }
}
