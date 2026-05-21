using System.Collections.Generic;
using UnityEngine;
using UnityEngine.U2D;

public class IslandGeneratorTwo : MonoBehaviour
{
    // Template object — used only as a source to clone from, not rendered directly.
    [SerializeField] private SpriteShapeController grassSpriteShape;
    [SerializeField] private ShapeTangentMode       tangentMode = ShapeTangentMode.Continuous;
    [SerializeField] private David.IslandMapGeneratorUnity _mapGenerator;
    [SerializeField, Range(0.05f, 1f)]  private float bulgeScale     = 0.35f;
    [SerializeField, Range(0f,    0.5f)] private float bulgeNoise    = 0.2f;
    [SerializeField, Range(1, 6)]        private int   subsPerSegment = 3;

    [Header("Debug Visualization")]
    [SerializeField] private Sprite debugTileSprite;

    [Header("Sub-generators")]
    [SerializeField] private LakeGenerator     _lakeGenerator;
    [SerializeField] private RockBandGenerator _rockBandGenerator;
    [SerializeField] private GrassDecalPlacer  _decalPlacer;

    private static readonly Dictionary<David.Terrain, Color> TerrainColors = new Dictionary<David.Terrain, Color>
    {
        { David.Terrain.Sky,      new Color(0.2f, 0.5f, 1f)   },  // blue
        { David.Terrain.Land,     new Color(0.6f, 0.85f, 0.4f) }, // green
        { David.Terrain.Mountain, new Color(0.55f, 0.5f, 0.45f) },// grey
        { David.Terrain.Lake,     new Color(0.3f, 0.7f, 1f)   },  // light blue
        { David.Terrain.Forest,   new Color(0.1f, 0.45f, 0.15f) } // dark green
    };

    private David.Terrain[,] _terrainGrid;
    private readonly List<List<Vector3>>       _islandDetailPoints       = new List<List<Vector3>>();
    private readonly List<List<Vector3>>       _islandPerimeterVertices  = new List<List<Vector3>>();
    private readonly List<List<Vector3>>       _lakePerimeterVertices    = new List<List<Vector3>>();
    private readonly List<List<(int x, int y)>> _lakeRegions             = new List<List<(int x, int y)>>();
    private readonly List<GameObject>          _spawnedIslands           = new List<GameObject>();
    private readonly List<GameObject>          _debugTiles               = new List<GameObject>();

    // Public data exposed to sub-generators and other systems.
    public David.Terrain[,]                        Grid                    => _terrainGrid;
    public IReadOnlyList<List<Vector3>>            IslandPerimeters        => _islandDetailPoints;
    public IReadOnlyList<List<Vector3>>            IslandPerimeterVertices => _islandPerimeterVertices;
    public IReadOnlyList<List<Vector3>>            LakePerimeterVertices   => _lakePerimeterVertices;
    public IReadOnlyList<List<(int x, int y)>>     LakeRegions             => _lakeRegions;

    private void Awake()
    {
        if (_mapGenerator == null)
        {
            Debug.LogError("IslandGeneratorTwo: _mapGenerator is not assigned.");
            return;
        }
        _mapGenerator.Generate();
        _terrainGrid = _mapGenerator.Grid;

        // Hide the template so it doesn't render an empty spline.
        if (grassSpriteShape != null)
        {
            var r = grassSpriteShape.GetComponent<SpriteShapeRenderer>();
            if (r != null) r.enabled = false;
        }
    }

    private void Start()
    {
        BuildAllIslands();
    }

    [ContextMenu("Regenerate Rocks")]
    private void RegenerateRocks()
    {
        _rockBandGenerator?.Rebuild(_islandDetailPoints, _spawnedIslands, _mapGenerator.seed);
    }

    [ContextMenu("Regenerate All")]
    private void RegenerateAll()
    {
        if (_mapGenerator == null) return;
        _mapGenerator.Generate();
        _terrainGrid = _mapGenerator.Grid;
        ClearAll();
        BuildAllIslands();
    }

    private void OnValidate()
    {
        if (!Application.isPlaying) return;
        RegenerateAll();
    }

    // -------------------------------------------------------------------------
    // Build pipeline
    // -------------------------------------------------------------------------

    private void BuildAllIslands()
    {
        if (_terrainGrid == null) return;

        _islandDetailPoints.Clear();
        _islandPerimeterVertices.Clear();
        _lakePerimeterVertices.Clear();
        _lakeRegions.Clear();

        SpawnDebugTiles();

        // --- Land islands ---
        List<List<(int x, int y)>> regions = FindConnectedRegions();

        for (int islandIdx = 0; islandIdx < regions.Count; islandIdx++)
        {
            List<Vector3> rimPoints    = GetRegionPerimeterPoints(regions[islandIdx]);
            List<Vector3> detailPoints = SubdivideRimPoints(rimPoints);
            _islandDetailPoints.Add(detailPoints);

            List<Vector3> cornerPerimeter = GridPerimeterHelper.ComputeCornerPerimeter(
                regions[islandIdx],
                _terrainGrid.GetLength(1),
                _terrainGrid.GetLength(0),
                (GridPerimeterHelper.TileToWorldFn)TileToWorld);
            _islandPerimeterVertices.Add(cornerPerimeter);

            // Use exact corner vertices directly (bypassing noise/subdivide for testing).
            GameObject islandGO = SpawnIslandGrassShape(cornerPerimeter, islandIdx);
            _spawnedIslands.Add(islandGO);
        }

        // --- Lakes ---
        List<List<(int x, int y)>> lakeRegions = FindConnectedRegions(David.Terrain.Lake);
        foreach (var region in lakeRegions)
        {
            _lakeRegions.Add(region);

            List<Vector3> cornerPerimeter = GridPerimeterHelper.ComputeCornerPerimeter(
                region,
                _terrainGrid.GetLength(1),
                _terrainGrid.GetLength(0),
                (GridPerimeterHelper.TileToWorldFn)TileToWorld);
            _lakePerimeterVertices.Add(cornerPerimeter);
        }

        _lakeGenerator?.Build(_lakePerimeterVertices, transform);
        _rockBandGenerator?.Build(_islandDetailPoints, _spawnedIslands, _mapGenerator.seed);

        // Scatter decals across all island perimeters.
        if (_decalPlacer != null && _islandDetailPoints.Count > 0)
        {
            // Flatten all island polygons into one combined point list for the placer.
            List<Vector3> allPoints = new List<Vector3>();
            foreach (var pts in _islandDetailPoints) allPoints.AddRange(pts);
            _decalPlacer.Place(allPoints, _mapGenerator.seed);
        }
    }

    private void ClearAll()
    {
        _rockBandGenerator?.Clear();
        _lakeGenerator?.Clear();
        _decalPlacer?.Clear();
        foreach (var go in _spawnedIslands)
            if (go != null) Destroy(go);
        _spawnedIslands.Clear();
        _islandDetailPoints.Clear();
        _islandPerimeterVertices.Clear();
        _lakePerimeterVertices.Clear();
        _lakeRegions.Clear();
        foreach (var go in _debugTiles)
            if (go != null) Destroy(go);
        _debugTiles.Clear();
    }

    // -------------------------------------------------------------------------
    // Debug tile visualization
    // -------------------------------------------------------------------------

    private void SpawnDebugTiles()
    {
        if (debugTileSprite == null || _terrainGrid == null) return;

        int W = _terrainGrid.GetLength(1);
        int H = _terrainGrid.GetLength(0);

        GameObject container = new GameObject("DebugTiles");
        container.transform.SetParent(transform, false);
        _debugTiles.Add(container);

        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                David.Terrain type = _terrainGrid[y, x];
                Vector3 worldPos   = TileToWorld(x, y);

                GameObject tile = new GameObject($"Tile_{x}_{y}");
                tile.transform.SetParent(container.transform, false);
                tile.transform.localPosition = worldPos;
                tile.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                Vector2 spriteSize = debugTileSprite.bounds.size;
                tile.transform.localScale = new Vector3(1f / spriteSize.x, 1f / spriteSize.y, 1f);

                var sr = tile.AddComponent<SpriteRenderer>();
                Color tileColor = TerrainColors.TryGetValue(type, out Color c) ? c : Color.white;
                tileColor.a     = 0.5f;
                sr.sprite       = debugTileSprite;
                sr.color        = tileColor;
                sr.sortingOrder = 3;
            }
        }
    }

    // -------------------------------------------------------------------------
    // Region detection
    // -------------------------------------------------------------------------

    // BFS flood-fill — returns one cell list per disconnected land mass.
    private List<List<(int x, int y)>> FindConnectedRegions()
    {
        int W       = _terrainGrid.GetLength(1);
        int H       = _terrainGrid.GetLength(0);
        var visited = new bool[H, W];
        var regions = new List<List<(int, int)>>();

        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                if (visited[y, x] || _terrainGrid[y, x] == David.Terrain.Sky) continue;

                var region = new List<(int, int)>();
                var queue  = new Queue<(int, int)>();
                queue.Enqueue((x, y));
                visited[y, x] = true;

                while (queue.Count > 0)
                {
                    var (cx, cy) = queue.Dequeue();
                    region.Add((cx, cy));
                    TryEnqueue(cx + 1, cy, W, H, visited, queue);
                    TryEnqueue(cx - 1, cy, W, H, visited, queue);
                    TryEnqueue(cx, cy + 1, W, H, visited, queue);
                    TryEnqueue(cx, cy - 1, W, H, visited, queue);
                }

                regions.Add(region);
            }
        }

        return regions;
    }

    // BFS flood-fill restricted to a single terrain type.
    private List<List<(int x, int y)>> FindConnectedRegions(David.Terrain terrain)
    {
        int W       = _terrainGrid.GetLength(1);
        int H       = _terrainGrid.GetLength(0);
        var visited = new bool[H, W];
        var regions = new List<List<(int, int)>>();

        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                if (visited[y, x] || _terrainGrid[y, x] != terrain) continue;

                var region = new List<(int, int)>();
                var queue  = new Queue<(int, int)>();
                queue.Enqueue((x, y));
                visited[y, x] = true;

                while (queue.Count > 0)
                {
                    var (cx, cy) = queue.Dequeue();
                    region.Add((cx, cy));
                    TryEnqueueTerrain(cx + 1, cy, W, H, terrain, visited, queue);
                    TryEnqueueTerrain(cx - 1, cy, W, H, terrain, visited, queue);
                    TryEnqueueTerrain(cx, cy + 1, W, H, terrain, visited, queue);
                    TryEnqueueTerrain(cx, cy - 1, W, H, terrain, visited, queue);
                }

                regions.Add(region);
            }
        }

        return regions;
    }

    private void TryEnqueueTerrain(int x, int y, int W, int H, David.Terrain terrain, bool[,] visited, Queue<(int, int)> queue)
    {
        if (x < 0 || x >= W || y < 0 || y >= H) return;
        if (visited[y, x] || _terrainGrid[y, x] != terrain) return;
        visited[y, x] = true;
        queue.Enqueue((x, y));
    }

    private void TryEnqueue(int x, int y, int W, int H, bool[,] visited, Queue<(int, int)> queue)
    {
        if (x < 0 || x >= W || y < 0 || y >= H) return;
        if (visited[y, x] || _terrainGrid[y, x] == David.Terrain.Sky) return;
        visited[y, x] = true;
        queue.Enqueue((x, y));
    }

    // -------------------------------------------------------------------------
    // Perimeter extraction
    // -------------------------------------------------------------------------

    // Collects border cells for a single region and angle-sorts them around
    // that region's own centroid, so each island is handled independently.
    private List<Vector3> GetRegionPerimeterPoints(List<(int x, int y)> regionCells)
    {
        int W = _terrainGrid.GetLength(1);
        int H = _terrainGrid.GetLength(0);

        // Compute centroid of this region for angle-based sorting.
        float sumX = 0f, sumZ = 0f;
        foreach (var (cx, cy) in regionCells)
        {
            Vector3 w = TileToWorld(cx, cy);
            sumX += w.x; sumZ += w.z;
        }
        Vector3 centroid = new Vector3(sumX / regionCells.Count, 0f, sumZ / regionCells.Count);

        var rimPoints = new List<Vector3>();
        foreach (var (cx, cy) in regionCells)
        {
            if (IsPerimeterCell(cx, cy, W, H))
                rimPoints.Add(TileToWorld(cx, cy));
        }

        if (rimPoints.Count < 2)
        {
            Debug.LogWarning("IslandGeneratorTwo: a region has fewer than 2 perimeter tiles.");
            return rimPoints;
        }

        rimPoints.Sort((a, b) =>
        {
            float angleA = Mathf.Atan2(a.z - centroid.z, a.x - centroid.x);
            float angleB = Mathf.Atan2(b.z - centroid.z, b.x - centroid.x);
            return angleB.CompareTo(angleA);
        });

        float drawDuration = 10f;
        for (int i = 0; i < rimPoints.Count; i++)
        {
            Vector3 current = rimPoints[i] + Vector3.up * 0.1f;
            Vector3 next    = rimPoints[(i + 1) % rimPoints.Count] + Vector3.up * 0.1f;
            Debug.DrawLine(current, next, Color.red, drawDuration);
        }

        return rimPoints;
    }

    private List<Vector3> SubdivideRimPoints(List<Vector3> rimPoints)
    {
        var result = new List<Vector3>();
        int count  = rimPoints.Count;

        for (int i = 0; i < count; i++)
        {
            result.Add(rimPoints[i]);

            Vector3 from = rimPoints[i];
            Vector3 to   = rimPoints[(i + 1) % count];

            for (int s = 1; s <= subsPerSegment; s++)
            {
                float   t        = s / (float)(subsPerSegment + 1);
                Vector3 p        = Vector3.Lerp(from, to, t);
                Vector3 outward  = new Vector3(p.x, 0f, p.z).normalized;
                Vector3 lateral  = new Vector3(-outward.z, 0f, outward.x);
                float   push     = Random.Range(bulgeScale - bulgeNoise, bulgeScale + bulgeNoise);
                float   sideways = Random.Range(-bulgeNoise * 0.5f, bulgeNoise * 0.5f);
                float   segLen   = Vector3.Distance(from, to);
                result.Add(p + outward * (segLen * push) + lateral * (segLen * sideways));
            }
        }

        return result;
    }

    // -------------------------------------------------------------------------
    // Spawning
    // -------------------------------------------------------------------------

    private GameObject SpawnIslandGrassShape(List<Vector3> detailPoints, int islandIdx)
    {
        if (grassSpriteShape == null || detailPoints.Count < 2) return null;

        GameObject go = Instantiate(grassSpriteShape.gameObject, transform);
        go.name = $"Island_{islandIdx}_Grass";
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = grassSpriteShape.transform.localRotation;
        go.transform.localScale    = grassSpriteShape.transform.localScale;

        // Re-enable the renderer that was disabled on the template.
        var r = go.GetComponent<SpriteShapeRenderer>();
        if (r != null) r.enabled = true;

        var ssc = go.GetComponent<SpriteShapeController>();
        if (ssc != null) FillSpline(ssc, detailPoints);

        return go;
    }

    // -------------------------------------------------------------------------
    // Spline helper
    // -------------------------------------------------------------------------

    private void FillSpline(SpriteShapeController ssc, List<Vector3> points)
    {
        int       count  = points.Count;
        Transform t      = ssc.transform;
        Spline    spline = ssc.spline;
        spline.Clear();

        for (int i = 0; i < count; i++)
        {
            Vector3 local = t.InverseTransformPoint(points[i]);
            spline.InsertPointAt(i, local);
            spline.SetTangentMode(i, tangentMode);
        }

        if (tangentMode == ShapeTangentMode.Continuous || tangentMode == ShapeTangentMode.Broken)
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 prev    = points[(i - 1 + count) % count];
                Vector3 curr    = points[i];
                Vector3 next    = points[(i + 1) % count];
                Vector3 chord   = next - prev;
                float   mag     = (Vector3.Distance(prev, curr) + Vector3.Distance(curr, next)) * 0.25f;
                Vector3 tangent = t.InverseTransformVector(chord.normalized * mag);
                spline.SetRightTangent(i, tangent);
            }
        }

        spline.isOpenEnded = false;
    }

    // -------------------------------------------------------------------------
    // Gizmos
    // -------------------------------------------------------------------------

    private void OnDrawGizmos()
    {
        if (_islandPerimeterVertices == null) return;

        for (int i = 0; i < _islandPerimeterVertices.Count; i++)
        {
            var verts = _islandPerimeterVertices[i];
            if (verts == null || verts.Count < 2) continue;

            Gizmos.color = Color.cyan;
            for (int v = 0; v < verts.Count; v++)
            {
                Vector3 a = transform.TransformPoint(verts[v]);
                Vector3 b = transform.TransformPoint(verts[(v + 1) % verts.Count]);
                Gizmos.DrawLine(a, b);
            }

            Gizmos.color = new Color(1f, 1f, 0f, 0.9f);
            foreach (var v in verts)
                Gizmos.DrawSphere(transform.TransformPoint(v), 0.06f);
        }
    }

    // -------------------------------------------------------------------------
    // Utilities
    // -------------------------------------------------------------------------

    private bool IsPerimeterCell(int x, int y, int W, int H)
    {
        if (x == 0 || x == W - 1 || y == 0 || y == H - 1) return true;
        if (_terrainGrid[y - 1, x] == David.Terrain.Sky) return true;
        if (_terrainGrid[y + 1, x] == David.Terrain.Sky) return true;
        if (_terrainGrid[y, x - 1] == David.Terrain.Sky) return true;
        if (_terrainGrid[y, x + 1] == David.Terrain.Sky) return true;
        return false;
    }

    private Vector3 TileToWorld(int x, int y)
    {
        int W = _terrainGrid.GetLength(1);
        int H = _terrainGrid.GetLength(0);
        return new Vector3(x - (W - 1) * 0.5f, 0f, y - (H - 1) * 0.5f);
    }
}
