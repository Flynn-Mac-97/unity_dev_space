using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace David
{
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    //  JSON data model (matches floating-island-*.json)
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

        // Ground IDs routed to the "Island Tops" tilemap
        private static readonly HashSet<int> IslandTopsGrounds =
            new HashSet<int> { 3, 4, 5, 7, 8, 9, 10, 11, 12 };

        // Ground type ID → Z position (elevation) within the tilemap
        private static readonly Dictionary<int, int> IslandTopsZPositions =
            new Dictionary<int, int>
            {
                { 3,  1 },
                { 4,  2 },
                { 5,  3 },
                { 7,  0 },
                { 8,  1 },
                { 9,  1 },
                { 10, 2 },
                { 11, 2 },
                { 12, 3 },
            };

        // Ground type ID → palette tile name
        private static readonly Dictionary<int, string> GroundTileNames =
            new Dictionary<int, string>
            {
                { 0,  "Tileset_1" },
                { 1,  "Tileset_9" },
                { 2,  "Tileset_7" },
                { 3,  "plains-sliced_54" },
                { 4,  "plains-sliced_59" },
                { 5,  "plains-sliced_62" },
                { 6,  "Tileset_4" },
                { 7,  "020-floating-set-variations-result-8_0" },
                { 8,  "020-floating-set-variations-result-8_1" },
                { 9,  "020-floating-set-variations-result-7_0" },
                { 10, "020-floating-set-variations-result-7_1" },
                { 11, "molten_center" },
                { 12, "base08" },
            };

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

        [Header("Options")]
        public bool generateOnStart = true;
        public bool centerAtOrigin = true;

        private Dictionary<int, TileBase> _groundTiles;
        private Transform _mapRoot;

        // ── Public API ───────────────────────────────────

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

            ClearMap();

            if (!TryLoadTiles(mapData))
                return;

            CreateGridAndTilemaps(mapData);
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

        // ── Tile loading ─────────────────────────────────

        private bool TryLoadTiles(MapJsonData mapData)
        {
            var palettePrefab = ResolvePalettePrefab();
            if (palettePrefab == null)
            {
                Debug.LogError("[IsoMapLoader] No ground palette assigned or found!");
                return false;
            }

            var paletteTiles = ExtractPaletteTiles(palettePrefab);
            if (paletteTiles.Count == 0)
            {
                Debug.LogError("[IsoMapLoader] No tiles found in palette!");
                return false;
            }

            BuildGroundTileMapping(paletteTiles);
            return _groundTiles != null && _groundTiles.Count > 0;
        }

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

        private void BuildGroundTileMapping(List<TileBase> paletteTiles)
        {
            _groundTiles = new Dictionary<int, TileBase>();

            var byName = new Dictionary<string, TileBase>(StringComparer.Ordinal);
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
                TileBase tile = byName.TryGetValue(kvp.Value, out var paletteTile)
                    ? paletteTile
                    : LoadTileFromProject(kvp.Value);

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

        // ── Grid & tilemap creation ──────────────────────

        private void CreateGridAndTilemaps(MapJsonData mapData)
        {
            // Root
            var rootObj = new GameObject("IsoMap_" + mapData.mapName);
            rootObj.transform.SetParent(transform, false);
            _mapRoot = rootObj.transform;

            // Grid (isometric layout — matches palette settings)
            var grid = rootObj.AddComponent<Grid>();
            grid.cellSize = new Vector3(tileWidth, tileHeight, 1f);
            grid.cellLayout = GridLayout.CellLayout.Isometric;
            grid.cellSwizzle = GridLayout.CellSwizzle.XYZ;

            // Tilemap "Ground_0" (sortingOrder 0 — base layer)
            var groundTilemap = CreateTilemap(rootObj.transform, "Ground_0", 0);

            // Tilemap "Island Tops" (sortingOrder 1 — elevated layer, renders above Ground_0)
            var islandTopsTilemap = CreateTilemap(rootObj.transform, "Island Tops", 1);

            PlaceTiles(mapData, grid, groundTilemap, islandTopsTilemap);

            // Set active paint target to "Island Tops"
            SetActivePaintTarget(islandTopsTilemap.gameObject);
        }

        private static Tilemap CreateTilemap(Transform parent, string name, int sortingOrder)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var tilemap = obj.AddComponent<Tilemap>();
            var renderer = obj.AddComponent<TilemapRenderer>();
            renderer.sortOrder = TilemapRenderer.SortOrder.TopRight;
            renderer.sortingOrder = sortingOrder;
            return tilemap;
        }

        // ── Tile placement ──────────────────────────────

        private void PlaceTiles(
            MapJsonData mapData,
            Grid grid,
            Tilemap groundTilemap,
            Tilemap islandTopsTilemap)
        {
            // First pass: compute bounds for centering
            int minX = int.MaxValue, maxX = int.MinValue;
            int minY = int.MaxValue, maxY = int.MinValue;
            int totalTiles = 0;

            foreach (var island in mapData.islands)
            {
                if (island.tiles == null) continue;
                foreach (var t in island.tiles)
                {
                    // Map is mirrored across Y axis: (x, y) → (-x, y)
                    int px = t.x;
                    int py = t.y;
                    if (px < minX) minX = px;
                    if (px > maxX) maxX = px;
                    if (py < minY) minY = py;
                    if (py > maxY) maxY = py;
                    totalTiles++;
                }
            }

            if (totalTiles == 0)
            {
                Debug.LogWarning("[IsoMapLoader] No tiles found in map data!");
                return;
            }

            // Second pass: place tiles
            int placed = 0;
            foreach (var island in mapData.islands)
            {
                if (island.tiles == null) continue;
                foreach (var tile in island.tiles)
                {
                    if (_groundTiles == null || !_groundTiles.TryGetValue(tile.ground, out var tileBase))
                        continue;

                    int z = IslandTopsGrounds.Contains(tile.ground) &&
                            IslandTopsZPositions.TryGetValue(tile.ground, out var zPos)
                        ? zPos
                        : 0;
                    var cellPos = new Vector3Int(maxY - tile.y, maxX - tile.x, z);
                    var target = IslandTopsGrounds.Contains(tile.ground)
                        ? islandTopsTilemap
                        : groundTilemap;
                    target.SetTile(cellPos, tileBase);
                    placed++;
                }
            }

            // Center at origin using Grid's own world projection
            if (centerAtOrigin)
            {
                float cx = (minX + maxX) * 0.5f;
                float cy = (minY + maxY) * 0.5f;
                var centerWorld = grid.CellToWorld(new Vector3Int(
                    Mathf.RoundToInt(cx), Mathf.RoundToInt(cy), 0));
                _mapRoot.localPosition = new Vector3(-centerWorld.x, -centerWorld.y, 0f);
            }

            Debug.Log($"[IsoMapLoader] Generated {placed}/{totalTiles} tiles from " +
                      $"{mapData.islands.Count} islands. " +
                      $"Grid bounds: X[{minX}..{maxX}] Y[{minY}..{maxY}]");
        }

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
    }
}
