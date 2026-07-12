using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace David
{
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    //  JSON data model (matches floating-island-third-map.json)
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    [Serializable]
    public class MapJsonData
    {
        public int version;
        public string mapName;
        public CanvasJsonData canvas;
        public ToolsetJsonData toolset;
        public List<IslandJsonData> islands;
        public LayersJsonData layers;
    }

    [Serializable]
    public class CanvasJsonData
    {
        public int size;
        public int cellSize;
        public int gridWidth;
        public int gridHeight;
    }

    [Serializable]
    public class ToolsetJsonData
    {
        public List<GroundTypeJsonData> groundTypes;
        public List<DecalTypeJsonData> decalTypes;
        public List<ResourceTypeJsonData> resourceTypes;
        public List<NpcTypeJsonData> npcTypes;
        public List<LargeSpriteTypeJsonData> largeSpriteTypes;
    }

    [Serializable]
    public class GroundTypeJsonData
    {
        public int id;
        public string name;
        public string stringKey;
        public string color;
        public bool enabled;
    }

    [Serializable]
    public class DecalTypeJsonData
    {
        public int id;
        public string name;
        public string stringKey;
        public string color;
        public bool enabled;
    }

    [Serializable]
    public class ResourceTypeJsonData
    {
        public int id;
        public string name;
        public string stringKey;
        public string color;
        public bool enabled;
    }

    [Serializable]
    public class NpcTypeJsonData
    {
        public int id;
        public string name;
        public string stringKey;
        public string color;
        public bool enabled;
    }

    [Serializable]
    public class LargeSpriteTypeJsonData
    {
        public int id;
        public string name;
        public string stringKey;
        public string color;
        public bool enabled;
        public int width;
        public int height;
    }

    [Serializable]
    public class IslandJsonData
    {
        public string id;
        public string name;
        public string outlineColor;
        public List<TileJsonData> tiles;
    }

    [Serializable]
    public class TileJsonData
    {
        public int x;
        public int y;
        public int ground;
    }

    [Serializable]
    public class LayersJsonData
    {
        public List<DecalJsonData> decals;
        public List<ResourceJsonData> resources;
        public List<NpcJsonData> npcs;
        public List<LargeSpriteJsonData> largeSprites;
    }

    [Serializable]
    public class DecalJsonData
    {
        public string id;
        public int typeId;
        public int x;
        public int y;
        public string islandId;
    }

    [Serializable]
    public class ResourceJsonData
    {
        public string id;
        public int typeId;
        public int x;
        public int y;
        public string islandId;
    }

    [Serializable]
    public class NpcJsonData
    {
        public string id;
        public int typeId;
        public int x;
        public int y;
        public string islandId;
    }

    [Serializable]
    public class LargeSpriteJsonData
    {
        public string id;
        public int typeId;
        public int x;
        public int y;
        public string islandId;
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    //  IsoMapLoader — reads JSON, spawns isometric tiles from palette
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    public class IsoMapLoader : MonoBehaviour
    {
        private const string DefaultPalettePath = "Assets/Flynn/TilePalettes/Ground.prefab";

        [Header("Map Data")]
        [Tooltip("JSON file containing the map data")]
        public TextAsset mapJsonFile;

        [Header("Tile Palette")]
        [Tooltip("Ground tile palette prefab. If unassigned, auto-loaded from " + DefaultPalettePath)]
        public GameObject groundPalettePrefab;

        [Header("Tile Settings")]
        [Tooltip("Width of each isometric tile in world units")]
        public float tileWidth = 1f;
        [Tooltip("Height of each isometric tile in world units (typically half the width for 2:1 iso)")]
        public float tileHeight = 0.5f;
        [Tooltip("Vertical offset per elevation level (0 = flat)")]
        public float elevationStep = 0f;

        [Header("Options")]
        public bool generateOnStart = true;
        public bool centerAtOrigin = true;

        private Dictionary<int, Color> _groundColors;
        private Dictionary<int, int> _groundElevations;
        private Dictionary<int, TileBase> _groundTiles;
        private Transform _mapRoot;

        void Start()
        {
            if (generateOnStart) GenerateMap();
        }

        [ContextMenu("Generate Map")]
        public void GenerateMap()
        {
            if (mapJsonFile == null)
            {
                Debug.LogError("[IsoMapLoader] No JSON file assigned!");
                return;
            }

            var mapData = JsonUtility.FromJson<MapJsonData>(mapJsonFile.text);
            if (mapData == null || mapData.islands == null)
            {
                Debug.LogError("[IsoMapLoader] Failed to parse JSON or no islands found!");
                return;
            }

            BuildGroundLookup(mapData);
            ClearMap();

            // Load palette and build tile-color mapping
            var palettePrefab = ResolvePalettePrefab();
            if (palettePrefab == null)
            {
                Debug.LogError("[IsoMapLoader] No ground palette assigned or found!");
                return;
            }

            var paletteTiles = ExtractPaletteTiles(palettePrefab);
            if (paletteTiles.Count == 0)
            {
                Debug.LogError("[IsoMapLoader] No tiles found in palette!");
                return;
            }

            BuildGroundTileMapping(mapData, paletteTiles);

            // Root
            var rootObj = new GameObject("IsoMap_" + mapData.mapName);
            rootObj.transform.SetParent(transform, false);
            _mapRoot = rootObj.transform;

            // Grid (isometric layout — matches palette settings)
            var grid = rootObj.AddComponent<Grid>();
            grid.cellSize = new Vector3(tileWidth, tileHeight, 1f);
            grid.cellLayout = GridLayout.CellLayout.Isometric;
            grid.cellSwizzle = GridLayout.CellSwizzle.XYZ;

            // Tilemap "Ground_0"
            var tilemapObj = new GameObject("Ground_0");
            tilemapObj.transform.SetParent(rootObj.transform, false);
            var tilemap = tilemapObj.AddComponent<Tilemap>();
            tilemapObj.AddComponent<TilemapRenderer>();

            // First pass: compute bounds for centering
            int minX = int.MaxValue, maxX = int.MinValue;
            int minY = int.MaxValue, maxY = int.MinValue;
            int totalTiles = 0;

            foreach (var island in mapData.islands)
            {
                if (island.tiles == null) continue;
                foreach (var t in island.tiles)
                {
                    if (t.x < minX) minX = t.x;
                    if (t.x > maxX) maxX = t.x;
                    if (t.y < minY) minY = t.y;
                    if (t.y > maxY) maxY = t.y;
                    totalTiles++;
                }
            }

            if (totalTiles == 0)
            {
                Debug.LogWarning("[IsoMapLoader] No tiles found in map data!");
                return;
            }

            // Place tiles
            int placed = 0;
            foreach (var island in mapData.islands)
            {
                if (island.tiles == null) continue;
                foreach (var tile in island.tiles)
                {
                    if (_groundTiles != null && _groundTiles.TryGetValue(tile.ground, out var tileBase))
                    {
                        tilemap.SetTile(new Vector3Int(tile.x, tile.y, 0), tileBase);
                        placed++;
                    }
                }
            }

            // Center at origin using Grid's own world projection
            if (centerAtOrigin)
            {
                float cx = (minX + maxX) * 0.5f;
                float cy = (minY + maxY) * 0.5f;
                var centerWorld = grid.CellToWorld(new Vector3Int(Mathf.RoundToInt(cx), Mathf.RoundToInt(cy), 0));
                rootObj.transform.localPosition = new Vector3(-centerWorld.x, -centerWorld.y, 0f);
            }

            // Set active paint target to "Ground_0"
            SetActivePaintTarget(tilemapObj);

            Debug.Log($"[IsoMapLoader] Generated {placed}/{totalTiles} tiles from {mapData.islands.Count} islands. " +
                      $"Grid bounds: X[{minX}..{maxX}] Y[{minY}..{maxY}]");
        }

        [ContextMenu("Clear Map")]
        public void ClearMap()
        {
            if (_mapRoot != null)
            {
                if (Application.isPlaying)
                    Destroy(_mapRoot.gameObject);
                else
                    DestroyImmediate(_mapRoot.gameObject);
                _mapRoot = null;
            }
        }

        // ── Private helpers ──────────────────────────────

        private void BuildGroundLookup(MapJsonData mapData)
        {
            _groundColors = new Dictionary<int, Color>();
            _groundElevations = new Dictionary<int, int>();

            if (mapData.toolset?.groundTypes == null) return;

            foreach (var gt in mapData.toolset.groundTypes)
            {
                _groundColors[gt.id] = HexToColor(gt.color);
                _groundElevations[gt.id] = InferElevation(gt.id, gt.name);
            }
        }

        private static int InferElevation(int id, string name)
        {
            // Elevation inference from ground type id/name
            return id switch
            {
                0  => 0,   // Grass, Ground 0 elevation
                3  => 1,   // Ground 1 elevation
                4  => 2,   // Ground 2 elevation
                5  => 3,   // Ground 3 elevation
                7  => 0,   // Ground floor
                8  => 1,   // 2nd floor
                9  => 0,   // Stairs
                11 => 2,   // 3rd floor
                12 => 3,   // Building roof
                _  => 0    // Mud, Ice, Water, Building outline, etc.
            };
        }

        // ── Palette loading & color matching ─────────────

        private GameObject ResolvePalettePrefab()
        {
            if (groundPalettePrefab != null) return groundPalettePrefab;

#if UNITY_EDITOR
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(DefaultPalettePath);
            if (prefab != null) return prefab;
            Debug.LogError($"[IsoMapLoader] Ground palette not found at {DefaultPalettePath}!");
#else
            Debug.LogError("[IsoMapLoader] No ground palette assigned! Assign one in the inspector.");
#endif
            return null;
        }

        private static List<TileBase> ExtractPaletteTiles(GameObject palettePrefab)
        {
            var result = new List<TileBase>();

            // Instantiate temporarily to read Tilemap data
            var tempInstance = Instantiate(palettePrefab);
            tempInstance.hideFlags = HideFlags.HideAndDontSave;

            var tilemap = tempInstance.GetComponentInChildren<Tilemap>();
            if (tilemap != null)
            {
                var bounds = tilemap.cellBounds;
                for (int x = bounds.xMin; x < bounds.xMax; x++)
                {
                    for (int y = bounds.yMin; y < bounds.yMax; y++)
                    {
                        var tile = tilemap.GetTile(new Vector3Int(x, y, 0));
                        if (tile != null && !result.Contains(tile))
                            result.Add(tile);
                    }
                }
            }

            if (Application.isPlaying)
                Destroy(tempInstance);
            else
                DestroyImmediate(tempInstance);

            return result;
        }

        private static readonly Dictionary<int, string> GroundTileNames = new Dictionary<int, string>
        {
            { 0,  "Tileset_1" },
            { 1,  "Tileset_9" },
            { 2,  "Tileset_7" },
            { 3,  "plains-sliced_54" },
            { 4,  "plains-sliced_61" },
            { 5,  "plains-sliced_57" },
            { 6,  "Tileset_4" },
            { 7,  "020-floating-set-variations-result-8_0" },
            { 8,  "020-floating-set-variations-result-8_1" },
            { 9,  "020-floating-set-variations-result-7_0" },
            { 10, "020-floating-set-variations-result-7_1" },
            { 11, "molten_center" },
            { 12, "base08" },
        };

        private void BuildGroundTileMapping(MapJsonData mapData, List<TileBase> paletteTiles)
        {
            _groundTiles = new Dictionary<int, TileBase>();

            // Build name → TileBase lookup from palette
            var byName = new Dictionary<string, TileBase>(System.StringComparer.Ordinal);
            if (paletteTiles != null)
            {
                foreach (var tile in paletteTiles)
                {
                    if (tile != null && !byName.ContainsKey(tile.name))
                        byName[tile.name] = tile;
                }
            }

            foreach (var kvp in GroundTileNames)
            {
                TileBase tile = null;

                if (byName.TryGetValue(kvp.Value, out var paletteTile))
                {
                    tile = paletteTile;
                }
                else
                {
                    tile = LoadTileFromProject(kvp.Value);
                }

                if (tile != null)
                {
                    _groundTiles[kvp.Key] = tile;
                    Debug.Log($"[IsoMapLoader] Ground {kvp.Key} → tile '{kvp.Value}'");
                }
                else
                {
                    Debug.LogWarning($"[IsoMapLoader] Tile '{kvp.Value}' not found for ground {kvp.Key}!");
                }
            }
        }

#if UNITY_EDITOR
        private static TileBase LoadTileFromProject(string tileName)
        {
            var guids = UnityEditor.AssetDatabase.FindAssets(tileName + " t:Tile");
            foreach (var guid in guids)
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var tile = UnityEditor.AssetDatabase.LoadAssetAtPath<TileBase>(path);
                if (tile != null)
                    return tile;
            }
            return null;
        }
#else
        private static TileBase LoadTileFromProject(string tileName) => null;
#endif

        // ── Active paint target ──────────────────────────

        private void SetActivePaintTarget(GameObject target)
        {
#if UNITY_EDITOR
            // GridPaintTargetsState lives in an editor assembly not directly
            // referenced by David.Runtime, so use reflection to set paintTargets.
            System.Type type = null;
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                type = asm.GetType("UnityEditor.GridPaintTargetsState");
                if (type != null) break;
            }

            if (type != null)
            {
                var prop = type.GetProperty("paintTargets");
                if (prop != null)
                    prop.SetValue(null, new[] { target });
            }

            Debug.Log($"[IsoMapLoader] Active paint target set to: {target.name}");
#endif
        }

        // ── Utilities ────────────────────────────────────

        private static Color HexToColor(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return Color.white;
            if (hex.StartsWith("#")) hex = hex.Substring(1);
            if (hex.Length < 6) return Color.white;

            byte r = byte.Parse(hex.Substring(0, 2), NumberStyles.HexNumber);
            byte g = byte.Parse(hex.Substring(2, 2), NumberStyles.HexNumber);
            byte b = byte.Parse(hex.Substring(4, 2), NumberStyles.HexNumber);
            return new Color32(r, g, b, 255);
        }
    }
}
