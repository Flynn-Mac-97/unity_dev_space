using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

public static class DrawStartingMap
{
    [MenuItem("Flynn/Draw Starting Map")]
    public static void Execute()
    {
        string jsonPath = "Assets/Flynn/raw maps/floating-island-starting-map.json";
        string jsonText = File.ReadAllText(jsonPath);

        // Parse with SimpleJSON-style manual parsing (no external lib)
        var data = JsonUtility.FromJson<MapData>(jsonText);

        var groundGrid = GameObject.Find("Ground Grid");
        var groundObj = groundGrid.transform.Find("Ground");
        var tm = groundObj.GetComponent<Tilemap>();
        var tile = AssetDatabase.LoadAssetAtPath<TileBase>("Assets/Flynn/Tiles/Ground/Tileset_0.asset");

        // Clear existing tiles
        tm.ClearAllTiles();
        Debug.Log("[DrawMap] Cleared existing tiles");

        // ── Phase 1: Paint all ground tiles ──
        int tileCount = 0;
        var elevatedSet = new HashSet<int> { 5, 6, 7 };

        foreach (var island in data.islands)
        {
            foreach (var t in island.tiles)
            {
                Vector3Int cell = new Vector3Int(t.x, t.y, 0);
                tm.SetTile(cell, tile);
                tileCount++;
            }
        }
        tm.CompressBounds();
        Debug.Log($"[DrawMap] Painted {tileCount} ground tiles");

        // ── Phase 2: Create placeholder sprites for non-ground assets ──

        // Build type name lookup
        var groundNames = new Dictionary<int, string>();
        if (data.toolset.groundTypes != null)
            foreach (var gt in data.toolset.groundTypes)
                groundNames[gt.id] = gt.name;

        var resourceNames = new Dictionary<int, string>();
        if (data.toolset.resourceTypes != null)
            foreach (var rt in data.toolset.resourceTypes)
                resourceNames[rt.id] = rt.name;

        var npcNames = new Dictionary<int, string>();
        if (data.toolset.npcTypes != null)
            foreach (var nt in data.toolset.npcTypes)
                npcNames[nt.id] = nt.name;

        var decalNames = new Dictionary<int, string>();
        if (data.toolset.decalTypes != null)
            foreach (var dt in data.toolset.decalTypes)
                decalNames[dt.id] = dt.name;

        var largeNames = new Dictionary<int, string>();
        if (data.toolset.largeSpriteTypes != null)
            foreach (var lt in data.toolset.largeSpriteTypes)
                largeNames[lt.id] = lt.name;

        // Create a parent for all markers
        var markerParent = new GameObject("MapMarkers");
        Undo.RegisterCreatedObjectUndo(markerParent, "Draw Starting Map");

        // Make a simple white square texture for placeholders
        var placeholderTex = MakeSquareTexture(32, Color.white);
        var placeholderSprite = Sprite.Create(placeholderTex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 32);

        int markerCount = 0;

        // Elevated ground markers
        foreach (var island in data.islands)
        {
            foreach (var t in island.tiles)
            {
                if (!elevatedSet.Contains(t.ground)) continue;
                string typeName = groundNames.ContainsKey(t.ground) ? groundNames[t.ground] : $"Ground_{t.ground}";
                CreateMarker(markerParent.transform, t.x, t.y, typeName, placeholderSprite, new Color(0.2f, 0.8f, 0.2f, 0.7f));
                markerCount++;
            }
        }
        Debug.Log($"[DrawMap] Created {markerCount} elevated ground markers");

        // Resource markers
        if (data.layers.resources != null)
        {
            foreach (var r in data.layers.resources)
            {
                string name = resourceNames.ContainsKey(r.typeId) ? resourceNames[r.typeId] : $"Resource_{r.typeId}";
                Color col = GetResourceColor(r.typeId);
                CreateMarker(markerParent.transform, r.x, r.y, name, placeholderSprite, col);
                markerCount++;
            }
        }

        // NPC markers
        if (data.layers.npcs != null)
        {
            foreach (var n in data.layers.npcs)
            {
                string name = npcNames.ContainsKey(n.typeId) ? npcNames[n.typeId] : $"NPC_{n.typeId}";
                CreateMarker(markerParent.transform, n.x, n.y, name, placeholderSprite, new Color(0.84f, 0.64f, 0.16f, 0.8f));
                markerCount++;
            }
        }

        // Decal markers
        if (data.layers.decals != null)
        {
            foreach (var d in data.layers.decals)
            {
                string name = decalNames.ContainsKey(d.typeId) ? decalNames[d.typeId] : $"Decal_{d.typeId}";
                CreateMarker(markerParent.transform, d.x, d.y, name, placeholderSprite, Color.white);
                markerCount++;
            }
        }

        // Large sprite markers
        if (data.layers.largeSprites != null)
        {
            foreach (var ls in data.layers.largeSprites)
            {
                string name = largeNames.ContainsKey(ls.typeId) ? largeNames[ls.typeId] : $"Large_{ls.typeId}";
                Color col = GetLargeSpriteColor(ls.typeId);
                float scaleX = ls.width;
                float scaleY = ls.height;
                CreateMarker(markerParent.transform, ls.x, ls.y, name, placeholderSprite, col, scaleX, scaleY);
                markerCount++;
            }
        }

        Debug.Log($"[DrawMap] Total markers: {markerCount}");

        // Save
        var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
        Debug.Log("[DrawMap] Done! Scene saved.");
    }

    static void CreateMarker(Transform parent, int gridX, int gridY, string name, Sprite sprite, Color color, float scaleX = 1f, float scaleY = 1f)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        // Convert grid coords to world position via the tilemap
        var tm = GameObject.Find("Ground Grid").transform.Find("Ground").GetComponent<Tilemap>();
        Vector3 worldPos = tm.CellToWorld(new Vector3Int(gridX, gridY, 0));
        worldPos.x += 0.5f; // center on cell
        worldPos.y += 0.25f; // center on iso cell (half of 0.5 cell height)
        go.transform.position = worldPos;
        go.transform.localScale = new Vector3(scaleX, scaleY, 1);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = 100; // above tiles

        Undo.RegisterCreatedObjectUndo(go, "Draw Starting Map");
    }

    static Color GetResourceColor(int typeId)
    {
        switch (typeId)
        {
            case 2000: return new Color(0f, 0.46f, 0.05f, 0.8f);  // Tree - green
            case 2001: return new Color(0.56f, 0.56f, 0.56f, 0.8f); // Stone - gray
            case 2002: return new Color(0.33f, 0.33f, 0.33f, 0.8f); // Rock - dark gray
            case 2003: return new Color(0.72f, 0.45f, 0f, 0.8f);  // Tree stub - brown
            case 2004: return new Color(0.22f, 0.54f, 0.24f, 0.8f); // Bush - dark green
            default: return new Color(1f, 0f, 1f, 0.8f); // magenta for unknown
        }
    }

    static Color GetLargeSpriteColor(int typeId)
    {
        switch (typeId)
        {
            case 4000: return new Color(0.22f, 0.22f, 0.22f, 0.8f); // Pod - dark
            case 4002: return new Color(0.73f, 0.51f, 1f, 0.8f);   // Gate - purple
            case 4003: return new Color(0.9f, 0.87f, 0f, 0.8f);    // Treasure - yellow
            default: return new Color(1f, 0f, 1f, 0.8f);
        }
    }

    static Texture2D MakeSquareTexture(int size, Color color)
    {
        var tex = new Texture2D(size, size);
        var pixels = new Color[size * size];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
        tex.SetPixels(pixels);
        tex.Apply();
        tex.filterMode = FilterMode.Point;
        return tex;
    }

    // ── JSON data classes ──
    [System.Serializable]
    public class MapData
    {
        public CanvasData canvas;
        public ToolsetData toolset;
        public IslandData[] islands;
        public LayersData layers;
    }

    [System.Serializable]
    public class CanvasData
    {
        public int size;
        public int cellSize;
        public int gridWidth;
        public int gridHeight;
    }

    [System.Serializable]
    public class ToolsetData
    {
        public GroundType[] groundTypes;
        public DecalType[] decalTypes;
        public ResourceType[] resourceTypes;
        public NpcType[] npcTypes;
        public LargeSpriteType[] largeSpriteTypes;
    }

    [System.Serializable]
    public class GroundType
    {
        public int id;
        public string name;
        public string stringKey;
        public string color;
        public bool enabled;
    }

    [System.Serializable]
    public class DecalType
    {
        public int id;
        public string name;
        public string stringKey;
        public string color;
        public bool enabled;
    }

    [System.Serializable]
    public class ResourceType
    {
        public int id;
        public string name;
        public string stringKey;
        public string color;
        public bool enabled;
    }

    [System.Serializable]
    public class NpcType
    {
        public int id;
        public string name;
        public string stringKey;
        public string color;
        public bool enabled;
    }

    [System.Serializable]
    public class LargeSpriteType
    {
        public int id;
        public string name;
        public string stringKey;
        public string color;
        public bool enabled;
        public int width;
        public int height;
    }

    [System.Serializable]
    public class IslandData
    {
        public string id;
        public string name;
        public string outlineColor;
        public TileData[] tiles;
    }

    [System.Serializable]
    public class TileData
    {
        public int x;
        public int y;
        public int ground;
    }

    [System.Serializable]
    public class LayersData
    {
        public DecalEntry[] decals;
        public ResourceEntry[] resources;
        public NpcEntry[] npcs;
        public LargeSpriteEntry[] largeSprites;
    }

    [System.Serializable]
    public class DecalEntry
    {
        public string id;
        public int typeId;
        public int x;
        public int y;
        public string islandId;
    }

    [System.Serializable]
    public class ResourceEntry
    {
        public string id;
        public int typeId;
        public int x;
        public int y;
        public string islandId;
    }

    [System.Serializable]
    public class NpcEntry
    {
        public string id;
        public int typeId;
        public int x;
        public int y;
        public string islandId;
    }

    [System.Serializable]
    public class LargeSpriteEntry
    {
        public string id;
        public int typeId;
        public int x;
        public int y;
        public string islandId;
        public int width;
        public int height;
        public string anchor;
    }
}
