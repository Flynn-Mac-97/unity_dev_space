using System.Collections.Generic;
using UnityEngine;

// Scene-wide master list of NPC capabilities. Designer-editable. Each NPC's
// NpcPersonalityProfile.capabilities is a multi-select subset of this list,
// surfaced in the Identity tab as a flags-style dropdown.
[CreateAssetMenu(menuName = "Dialogue/NPC Capability Library", fileName = "NpcCapabilityLibrary")]
public class NpcCapabilityLibrary : ScriptableObject
{
    [Tooltip("Master list of concrete in-game actions any NPC may be configured to perform. Order is preserved for the dropdown.")]
    public List<string> entries = new List<string>();

    public bool Contains(string capability)
    {
        if (string.IsNullOrWhiteSpace(capability)) return false;
        string trimmed = capability.Trim();
        for (int i = 0; i < entries.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(entries[i])) continue;
            if (string.Equals(entries[i].Trim(), trimmed, System.StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    public bool TryAdd(string capability)
    {
        if (string.IsNullOrWhiteSpace(capability)) return false;
        string trimmed = capability.Trim();
        if (Contains(trimmed)) return false;
        entries.Add(trimmed);
        return true;
    }
}
