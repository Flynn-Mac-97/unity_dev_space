

namespace Flynn.Resources
{
    /// <summary>
    /// Identifies the kind of resource a ResourceNode represents. Used by
    /// ToolEffectivenessTable to determine which tools are effective.
    /// </summary>
    public enum ResourceType
    {
        None     = 0,
        Stone    = 1,
        Wood     = 2,
        TechTrash = 3,
        Unknown  = 4,
    }

}
