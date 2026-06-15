using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Maps (ToolType, ResourceType) → damage multiplier. Controls which tools are
/// effective against which resources. Unmatched combinations deal 0 damage
/// (wrong tool). Designers can tune this via the Inspector without code changes.
/// </summary>
[CreateAssetMenu(menuName = "Flynn/Resource/Tool Effectiveness Table", fileName = "ToolEffectivenessTable")]
public class ToolEffectivenessTable : ScriptableObject
{
    [System.Serializable]
    public struct Entry
    {
        public ToolType toolType;
        public ResourceType resourceType;
        [Range(0f, 3f)] public float multiplier;
    }

    [SerializeField] private Entry[] _entries = System.Array.Empty<Entry>();

    /// <summary>
    /// Default multiplier when no entry matches. 0 = wrong tool (no damage).
    /// The wrench as a multitool gets a small non-zero default so it can
    /// harvest anything, just inefficiently.
    /// </summary>
    [SerializeField] private float _defaultMultiplier = 0.3f;

    private Dictionary<(ToolType, ResourceType), float> _lookup;

    private void BuildLookup()
    {
        _lookup = new Dictionary<(ToolType, ResourceType), float>(_entries.Length);
        foreach (var e in _entries)
            _lookup[(e.toolType, e.resourceType)] = e.multiplier;
    }

    /// <summary>
    /// Get the damage multiplier for a tool/resource combination.
    /// Returns the matched entry, or _defaultMultiplier for the wrench,
    /// or 0 for other unmatched tools.
    /// </summary>
    public float GetMultiplier(ToolType tool, ResourceType resource)
    {
        if (_lookup == null) BuildLookup();

        if (_lookup.TryGetValue((tool, resource), out float m))
            return m;

        // Wrench as multitool: partial effectiveness against everything
        if (tool == ToolType.Wrench)
            return _defaultMultiplier;

        return 0f;
    }
}
