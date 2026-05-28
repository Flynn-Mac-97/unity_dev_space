using System.Collections.Generic;
using UnityEngine;
using UnityEngine.U2D;

/// <summary>
/// Scatters decal prefabs inside a polygon defined by world-space XZ perimeter points.
/// Call <see cref="Place"/> from your island/grass generator after building the spline,
/// and <see cref="Clear"/> before regenerating.
///
/// Uses rejection sampling with a point-in-polygon test (ray casting) and a
/// minimum-spacing check so decals don't stack on top of one another.
/// </summary>
public class GrassDecalPlacer : MonoBehaviour
{
    [SerializeField] private GrassDecalConfig _config;
    [SerializeField] private Transform        _decalParent;

    [Header("Test Shape (optional)")]
    [Tooltip("Assign a SpriteShapeController to auto-populate from its spline on Start. Remove when wired to IslandGeneratorTwo.")]
    [SerializeField] private SpriteShapeController _testSpriteShape;
    [SerializeField] private int                   _testSeed = 42;

    private readonly List<GameObject> _spawned   = new List<GameObject>();
    private readonly List<Vector2>    _placedXZ  = new List<Vector2>();

    // -------------------------------------------------------------------------
    // Test bootstrapper
    // -------------------------------------------------------------------------

    private void Start()
    {
        if (_testSpriteShape == null) return;

        var spline = _testSpriteShape.spline;
        int count  = spline.GetPointCount();
        if (count < 3) return;

        var points = new List<Vector3>(count);
        for (int i = 0; i < count; i++)
        {
            // Spline points are in local space — convert to world.
            Vector3 world = _testSpriteShape.transform.TransformPoint(spline.GetPosition(i));
            points.Add(world);
        }

        Place(points, _testSeed);
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Scatter decals inside <paramref name="perimeterXZ"/> (world-space XZ polygon).
    /// Perimeter winding order does not matter.
    /// </summary>
    public void Place(IReadOnlyList<Vector3> perimeterWorld, int seed = 0)
    {
        if (_config == null)
        {
            Debug.LogWarning($"{nameof(GrassDecalPlacer)}: no GrassDecalConfig assigned.", this);
            return;
        }

        if (perimeterWorld == null || perimeterWorld.Count < 3) return;

        Clear();

        // Flatten perimeter to XZ for 2D point-in-polygon tests.
        Vector2[] poly = new Vector2[perimeterWorld.Count];
        for (int i = 0; i < perimeterWorld.Count; i++)
            poly[i] = new Vector2(perimeterWorld[i].x, perimeterWorld[i].z);

        Bounds2D bounds = GetBounds(poly);
        float area = bounds.Width * bounds.Height;
        int target = Mathf.Min(
            Mathf.RoundToInt(area * _config.densityPerUnit),
            _config.maxDecals
        );

        if (target <= 0) return;

        Transform parent = _decalParent != null ? _decalParent : transform;
        System.Random rng = new System.Random(seed);
        float spacingSq   = _config.minSpacing * _config.minSpacing;

        // Rejection sampling — attempt up to 10x the target count before giving up.
        int  attempts = target * 10;
        int  placed   = 0;

        for (int a = 0; a < attempts && placed < target; a++)
        {
            float x = Lerp(bounds.MinX, bounds.MaxX, (float)rng.NextDouble());
            float z = Lerp(bounds.MinZ, bounds.MaxZ, (float)rng.NextDouble());
            Vector2 candidate = new Vector2(x, z);

            if (!PointInPolygon(candidate, poly))  continue;
            if (!CheckSpacing(candidate, spacingSq)) continue;

            SpawnDecal(rng, new Vector3(x, _config.yOffset, z), parent);
            _placedXZ.Add(candidate);
            placed++;
        }
    }

    /// <summary>Destroy all spawned decals and reset state.</summary>
    public void Clear()
    {
        foreach (var go in _spawned)
            if (go != null) Destroy(go);

        _spawned.Clear();
        _placedXZ.Clear();
    }

    // -------------------------------------------------------------------------
    // Spawn
    // -------------------------------------------------------------------------

    private void SpawnDecal(System.Random rng, Vector3 worldPos, Transform parent)
    {
        GameObject prefab = _config.PickRandom(rng);
        if (prefab == null) return;

        float scale = Mathf.Lerp(_config.scaleMin, _config.scaleMax, (float)rng.NextDouble());
        float yRot  = _config.randomYRotation ? (float)(rng.NextDouble() * 360.0) : 0f;

        GameObject go = Instantiate(prefab, worldPos, Quaternion.Euler(0f, yRot, 0f), parent);
        go.transform.localScale = Vector3.one * scale;
        _spawned.Add(go);
    }

    // -------------------------------------------------------------------------
    // Spatial helpers
    // -------------------------------------------------------------------------

    private bool CheckSpacing(Vector2 candidate, float minDistSq)
    {
        foreach (var p in _placedXZ)
        {
            float dx = p.x - candidate.x;
            float dy = p.y - candidate.y;
            if (dx * dx + dy * dy < minDistSq) return false;
        }
        return true;
    }

    /// <summary>
    /// Ray-casting point-in-polygon test (Jordan curve theorem).
    /// Handles concave polygons correctly.
    /// </summary>
    private static bool PointInPolygon(Vector2 p, Vector2[] poly)
    {
        int  n       = poly.Length;
        bool inside  = false;
        int  j       = n - 1;

        for (int i = 0; i < n; j = i++)
        {
            float xi = poly[i].x, yi = poly[i].y;
            float xj = poly[j].x, yj = poly[j].y;

            bool  intersect = ((yi > p.y) != (yj > p.y))
                           && (p.x < (xj - xi) * (p.y - yi) / (yj - yi) + xi);
            if (intersect) inside = !inside;
        }
        return inside;
    }

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;

    // -------------------------------------------------------------------------
    // Bounds
    // -------------------------------------------------------------------------

    private struct Bounds2D
    {
        public float MinX, MaxX, MinZ, MaxZ;
        public float Width  => MaxX - MinX;
        public float Height => MaxZ - MinZ;
    }

    private static Bounds2D GetBounds(Vector2[] poly)
    {
        Bounds2D b = new Bounds2D
        {
            MinX = float.MaxValue, MaxX = float.MinValue,
            MinZ = float.MaxValue, MaxZ = float.MinValue
        };

        foreach (var p in poly)
        {
            if (p.x < b.MinX) b.MinX = p.x;
            if (p.x > b.MaxX) b.MaxX = p.x;
            if (p.y < b.MinZ) b.MinZ = p.y;
            if (p.y > b.MaxZ) b.MaxZ = p.y;
        }
        return b;
    }
}
