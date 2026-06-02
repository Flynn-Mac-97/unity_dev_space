using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Maps ground types from the map painter palette to their Unity sorting order.
/// Assign one asset to MapLoader so the designer controls which ground layers
/// visually sit above others without touching code.
///
/// Typical range: use values between -2000 and -100 so all ground stays below
/// the SortableSprite dynamic range (which starts at ~1500).
///
/// Example setup:
///   ground_sand       → -1200   (deepest, draws first)
///   ground_grass      → -1100
///   ground_water      → -1000   (above grass, below everything else)
///   ground_path       →  -900   (paths sit on top of grass)
///
/// Create via: Assets → Create → Flynn/Map/Ground Palette
/// </summary>
[CreateAssetMenu(menuName = "Flynn/Map/Ground Palette", fileName = "GroundPalette")]
public class GroundPaletteSO : ScriptableObject
{
    [Tooltip("Fallback sorting order used for any ground type not listed below.")]
    public int defaultSortingOrder = -1000;

    [SerializeField] private List<GroundPaletteEntry> _entries = new();

    // Lazy-built lookup, invalidated on Inspector changes.
    private Dictionary<string, int> _byKey;
    private Dictionary<int, int>    _byId;

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the sorting order for the given ground type.
    /// Tries stringKey first, then typeId, then falls back to defaultSortingOrder.
    /// </summary>
    public int GetSortingOrder(int typeId, string stringKey)
    {
        BuildLookupsIfNeeded();

        if (!string.IsNullOrWhiteSpace(stringKey) && _byKey.TryGetValue(stringKey, out int orderByKey))
            return orderByKey;

        if (_byId.TryGetValue(typeId, out int orderById))
            return orderById;

        return defaultSortingOrder;
    }

    // ── Internal ──────────────────────────────────────────────────────────────

    private void BuildLookupsIfNeeded()
    {
        if (_byKey != null) return;

        _byKey = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        _byId  = new Dictionary<int, int>();

        foreach (GroundPaletteEntry entry in _entries)
        {
            if (!string.IsNullOrWhiteSpace(entry.stringKey))
                _byKey[entry.stringKey] = entry.sortingOrder;

            if (entry.typeId > 0)
                _byId[entry.typeId] = entry.sortingOrder;
        }
    }

    private void OnValidate() { _byKey = null; _byId = null; }
}

/// <summary>One row in the ground palette: a type identifier and its sorting order.</summary>
[Serializable]
public class GroundPaletteEntry
{
    [Tooltip("String key from the map painter (e.g. 'ground_water'). Matched first.")]
    public string stringKey;

    [Tooltip("Numeric id from the map painter toolset. Used when stringKey is empty or not found.")]
    public int typeId;

    [Tooltip("Sorting order assigned to the SpriteShapeRenderer for this ground type.")]
    public int sortingOrder = -1000;
}
