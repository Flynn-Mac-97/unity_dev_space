using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Maps each resource type from the map painter palette (identified by either its
/// integer typeId or its human-readable stringKey) to the Unity prefab that should
/// be instantiated in the world.
///
/// The lookup tries stringKey first (more readable) and falls back to typeId.
/// Create via: Assets → Create → Flynn/Map/Resource Palette
/// </summary>
[CreateAssetMenu(menuName = "Flynn/Map/Resource Palette", fileName = "ResourcePalette")]
public class ResourcePaletteSO : ScriptableObject
{
    [SerializeField] private List<ResourcePaletteEntry> _entries = new();

    // Lazy-built lookup tables, cleared on OnValidate so Inspector edits take effect.
    private Dictionary<string, GameObject> _byKey;
    private Dictionary<int, GameObject> _byId;

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Tries to find a prefab for the given type, matching stringKey first then typeId.
    /// Returns false (and sets prefab to null) when no entry exists or the entry's prefab
    /// field was left empty.
    /// </summary>
    public bool TryGetPrefab(int typeId, string stringKey, out GameObject prefab)
    {
        BuildLookupsIfNeeded();

        if (!string.IsNullOrWhiteSpace(stringKey) && _byKey.TryGetValue(stringKey, out prefab) && prefab != null)
        {
            return true;
        }

        if (_byId.TryGetValue(typeId, out prefab) && prefab != null)
        {
            return true;
        }

        prefab = null;
        return false;
    }

    // -------------------------------------------------------------------------
    // Internal
    // -------------------------------------------------------------------------

    private void BuildLookupsIfNeeded()
    {
        if (_byKey != null)
        {
            return;
        }

        _byKey = new Dictionary<string, GameObject>(_entries.Count, StringComparer.Ordinal);
        _byId  = new Dictionary<int,    GameObject>(_entries.Count);

        for (int i = 0; i < _entries.Count; i++)
        {
            ResourcePaletteEntry entry = _entries[i];
            if (entry == null)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(entry.stringKey))
            {
                _byKey[entry.stringKey] = entry.prefab;
            }

            _byId[entry.typeId] = entry.prefab;
        }
    }

    // Invalidate the lazy dicts whenever the asset is edited in the Inspector.
    private void OnValidate()
    {
        _byKey = null;
        _byId  = null;
    }
}

/// <summary>
/// One entry in the <see cref="ResourcePaletteSO"/>: ties a palette resource type to
/// the Unity prefab that represents it in the world.
/// </summary>
[Serializable]
public class ResourcePaletteEntry
{
    [Tooltip("The integer id from the map painter palette (e.g. 2000 for res_wood).")]
    public int typeId;

    [Tooltip("The stringKey from the map painter palette (e.g. \"res_wood\"). Matched first.")]
    public string stringKey;

    [Tooltip("Prefab to instantiate at each tile that has this resource type. Leave empty to fall back to a colored quad.")]
    public GameObject prefab;
}
