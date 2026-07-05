using UnityEngine;


using Flynn.UI.Core;

namespace Flynn.Core
{
    /// <summary>
    /// Identifies the tool being used for a hit action. Maps to inventory tool items
    /// but is a separate enum so the hit system doesn't depend on inventory internals.
    /// </summary>
    public enum ToolType
    {
        None   = 0,
        Pick   = 1,
        Axe    = 2,
        Hammer = 3,
        Wrench = 4,
    }

    /// <summary>
    /// What kind of action produced this hit. Determines damage, feedback, and resource
    /// interaction rules (e.g. thrown wrench may deal different damage than melee).
    /// </summary>
    public enum ToolActionType
    {
        Swing = 0,
        Throw = 1,
    }

}
