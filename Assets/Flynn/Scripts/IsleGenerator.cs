using System.Collections.Generic;
using UnityEngine;
using UnityEngine.U2D;

public class IsleGenerator : MonoBehaviour
{
    [SerializeField] private SpriteShapeController grassSpriteShape;
    [SerializeField] private GameObject cliffPrefab;
    [SerializeField] private int mapWidth = 25;
    [SerializeField] private int mapHeight = 25;
    [SerializeField, Range(0f, 0.25f)] private float edgeNoise = 0.12f;
    [SerializeField] private int randomSeed;
    [SerializeField, Range(0.05f, 1f)]  private float bulgeScale    = 0.35f;
    [SerializeField, Range(0f,    0.5f)] private float bulgeNoise   = 0.2f;
    [SerializeField, Range(1, 6)]        private int   subsPerSegment = 3;

    [Header("Debug")]
    [SerializeField] private bool  showSplineDebugPoints = true;
    [SerializeField] private float debugPointSize        = 0.2f;

    private const int WaterIndex = 0;
    private const int SandIndex = 1;
    private const int GrassIndex = 2;
    private const int ForestIndex = 3;
    private const int MountainIndex = 4;
    // Tile types used by the demo island map.
    public enum TileType
    {
        Water = 0,
        Sand = 1,
        Grass = 2,
        Forest = 3,
        Mountain = 4
    }

    private int[,] island2D;
    private List<Vector3> _detailPoints;
    private GameObject _debugRoot;
    private bool _regenPending;

    private void Awake()
    {
        mapWidth = Mathf.Max(5, mapWidth);
        mapHeight = Mathf.Max(5, mapHeight);
        GenerateIslandMap();
    }

    // Creates an island with a noisy coastline so the edges are randomized each run.
    private void GenerateIslandMap()
    {
        int seed = randomSeed == 0 ? System.Environment.TickCount : randomSeed;
        Random.InitState(seed);

        island2D = new int[mapWidth, mapHeight];

        float centerX = (mapWidth - 1) * 0.5f;
        float centerY = (mapHeight - 1) * 0.5f;
        float radiusX = mapWidth * 0.45f;
        float radiusY = mapHeight * 0.45f;

        for (int x = 0; x < mapWidth; x++)
        {
            for (int y = 0; y < mapHeight; y++)
            {
                float dx = (x - centerX) / radiusX;
                float dy = (y - centerY) / radiusY;
                float distanceFromCenter = Mathf.Sqrt(dx * dx + dy * dy);
                float noise = Random.Range(-edgeNoise, edgeNoise);
                float edgeValue = distanceFromCenter + noise;

                if (edgeValue > 1f)
                {
                    island2D[x, y] = WaterIndex;
                }
                else if (edgeValue > 0.84f)
                {
                    island2D[x, y] = SandIndex;
                }
                else
                {
                    int roll = Random.Range(0, 100);
                    if (roll < 8)
                    {
                        island2D[x, y] = ForestIndex;
                    }
                    else if (roll < 11)
                    {
                        island2D[x, y] = MountainIndex;
                    }
                    else
                    {
                        island2D[x, y] = GrassIndex;
                    }
                }
            }
        }
    }
    // Traces the actual outer grid boundary of all non-water tiles and returns the
    // ordered corner points as world-space positions. Each point is a tile corner
    // where land meets water, giving a pixel-accurate island outline.
    private List<Vector3> GetGridPerimeter()
    {
        int w = island2D.GetLength(0);
        int h = island2D.GetLength(1);

        bool IsLand(int x, int y) =>
            x >= 0 && x < w && y >= 0 && y < h && island2D[x, y] != WaterIndex;

        // Directed edge map: startCorner -> endCorner.
        // Each edge is oriented so that land is to its left (counter-clockwise winding).
        var edgeMap = new Dictionary<Vector2Int, Vector2Int>();

        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                if (!IsLand(x, y)) continue;

                if (!IsLand(x,     y - 1)) edgeMap[new Vector2Int(x + 1, y    )] = new Vector2Int(x,     y    ); // bottom
                if (!IsLand(x + 1, y    )) edgeMap[new Vector2Int(x + 1, y + 1)] = new Vector2Int(x + 1, y    ); // right
                if (!IsLand(x,     y + 1)) edgeMap[new Vector2Int(x,     y + 1)] = new Vector2Int(x + 1, y + 1); // top
                if (!IsLand(x - 1, y    )) edgeMap[new Vector2Int(x,     y    )] = new Vector2Int(x,     y + 1); // left
            }
        }

        if (edgeMap.Count == 0)
        {
            Debug.LogWarning("GetGridPerimeter: no boundary edges found.");
            return new List<Vector3>();
        }

        // Trace the single outermost loop starting from the first available edge.
        Vector2Int startCorner = default;
        foreach (var key in edgeMap.Keys) { startCorner = key; break; }

        var perimeter = new List<Vector3>();
        Vector2Int current = startCorner;
        do
        {
            perimeter.Add(TileCornerToWorld(current.x, current.y));
            if (!edgeMap.TryGetValue(current, out current))
            {
                Debug.LogWarning("GetGridPerimeter: broken edge chain.");
                break;
            }
        }
        while (current != startCorner);

        // Draw connected debug lines to visualise the perimeter.
        float drawDuration = 10f;
        for (int i = 0; i < perimeter.Count; i++)
        {
            Vector3 a = perimeter[i] + Vector3.up * 0.1f;
            Vector3 b = perimeter[(i + 1) % perimeter.Count] + Vector3.up * 0.1f;
            Debug.DrawLine(a, b, Color.red, drawDuration);
        }

        return perimeter;
    }

    // Inserts sub-points between each rim pair so the spline has enough anchors
    // for small, varied scallops. Each sub-point is nudged outward (or inward) with
    // noise so no two lobes look identical.
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
                float t   = s / (float)(subsPerSegment + 1);
                Vector3 p = Vector3.Lerp(from, to, t);

                // Outward direction at this point.
                Vector3 outward = new Vector3(p.x, 0f, p.z).normalized;

                // Random lateral deflection perpendicular to the outward direction
                // gives each sub-point a unique twist, like a real rocky edge.
                Vector3 lateral  = new Vector3(-outward.z, 0f, outward.x);
                float   push     = Random.Range(bulgeScale - bulgeNoise, bulgeScale + bulgeNoise);
                float   sideways = Random.Range(-bulgeNoise * 0.5f, bulgeNoise * 0.5f);

                float segLen  = Vector3.Distance(from, to);
                result.Add(p + outward * (segLen * push) + lateral * (segLen * sideways));
            }
        }

        return result;
    }

    // Maps a tile grid coordinate to a world position centered at the origin.
    private Vector3 TileToWorld(int x, int y)
    {
        return new Vector3(x - (mapWidth - 1) * 0.5f, 0f, y - (mapHeight - 1) * 0.5f);
    }

    // Maps a tile corner index to world space. Corner (cx, cy) sits at the intersection
    // of tiles (cx-1,cy-1), (cx,cy-1), (cx-1,cy), and (cx,cy).
    private Vector3 TileCornerToWorld(int cx, int cy)
    {
        return new Vector3(cx - mapWidth * 0.5f, 0f, cy - mapHeight * 0.5f);
    }

    private void Start()
    {
        List<Vector3> rimPoints = GetGridPerimeter();
        _detailPoints = SubdivideRimPoints(rimPoints);
        UpdateGrassShapeToOuterRim(_detailPoints);
        DrawSplineDebugPoints();
    }

    // Regenerates the entire island — tile map, rim points, spline, and rock layers.
    // Call this when noise or shape parameters change.
    [ContextMenu("Regenerate All")]
    private void RegenerateAll()
    {
        GenerateIslandMap();

        List<Vector3> rimPoints = GetGridPerimeter();
        _detailPoints = SubdivideRimPoints(rimPoints);
        UpdateGrassShapeToOuterRim(_detailPoints);
        DrawSplineDebugPoints();
    }

    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            // Defer to Update — direct calls are forbidden inside OnValidate.
            _regenPending = true;
        }
        else
        {
#if UNITY_EDITOR
            // Edit mode: regenerate immediately via a delayed call so Unity has
            // finished applying the property change before we read it.
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null) return;   // guard against destroyed object
                RegenerateAll();
            };
#endif
        }
    }

    private void Update()
    {
        if (!_regenPending) return;
        _regenPending = false;
        RegenerateAll();
    }

    // Updates the grass SpriteShape spline using Continuous tangents derived from the
    // Catmull-Rom chord direction at each point. Direction is always smooth (no kinks),
    // so the border tile never gets cut. Shape variation comes from the point positions.
    private void UpdateGrassShapeToOuterRim(List<Vector3> rimPoints)
    {
        if (grassSpriteShape == null)
        {
            Debug.LogWarning("UpdateGrassShapeToOuterRim: no SpriteShapeController assigned.");
            return;
        }

        if (rimPoints.Count < 2) return;

        int count = rimPoints.Count;
        Transform t = grassSpriteShape.transform;
        Spline spline = grassSpriteShape.spline;
        spline.Clear();

        for (int i = 0; i < count; i++)
        {
            Vector3 local = t.InverseTransformPoint(rimPoints[i]);
            spline.InsertPointAt(i, local);
            spline.SetTangentMode(i, ShapeTangentMode.Continuous);
        }

        // Catmull-Rom tangent: direction is (next - prev), magnitude is a third of the
        // average neighbour distance. Continuous mode mirrors left from right automatically,
        // guaranteeing C1 continuity so the border UV never tears.
        for (int i = 0; i < count; i++)
        {
            Vector3 prev = rimPoints[(i - 1 + count) % count];
            Vector3 curr = rimPoints[i];
            Vector3 next = rimPoints[(i + 1) % count];

            Vector3 chord = next - prev;
            float   mag   = (Vector3.Distance(prev, curr) + Vector3.Distance(curr, next)) * 0.25f;

            Vector3 tangent = t.InverseTransformVector(chord.normalized * mag);
            spline.SetRightTangent(i, tangent);
        }

        spline.isOpenEnded = false;
    }

    // Spawns bright magenta dots at every actual spline control point so you can
    // verify that rock sprites align with the SpriteShape anchor positions.
    private void DrawSplineDebugPoints()
    {
        // Rebuild the debug root every time.
        if (_debugRoot != null)
        {
            for (int i = _debugRoot.transform.childCount - 1; i >= 0; i--)
                Destroy(_debugRoot.transform.GetChild(i).gameObject);
        }

        if (!showSplineDebugPoints || grassSpriteShape == null) return;

        if (_debugRoot == null)
        {
            _debugRoot = new GameObject("SplineDebugPoints");
            _debugRoot.transform.SetParent(transform);
            _debugRoot.transform.localPosition = Vector3.zero;
        }

        // Read actual spline control points so these markers are ground-truth.
        Spline spline  = grassSpriteShape.spline;
        int    count   = spline.GetPointCount();
        Transform t    = grassSpriteShape.transform;

        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        Sprite dot = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);

        float spriteWorldWidth = dot.rect.width / dot.pixelsPerUnit;
        float scale = spriteWorldWidth > 0f ? debugPointSize / spriteWorldWidth : debugPointSize;

        var grassRenderer = grassSpriteShape.GetComponent<SpriteShapeRenderer>();
        int topOrder = (grassRenderer != null ? grassRenderer.sortingOrder : 0) + 50;

        for (int i = 0; i < count; i++)
        {
            // GetPosition returns local space — transform back to world so the marker
            // sits exactly where the spline anchor lives in the scene.
            Vector3 worldPos = t.TransformPoint(spline.GetPosition(i));

            GameObject marker = new GameObject($"DbgPt_{i}");
            marker.transform.SetParent(_debugRoot.transform);
            marker.transform.position    = worldPos;
            marker.transform.localScale  = Vector3.one * scale;

            var sr         = marker.AddComponent<SpriteRenderer>();
            sr.sprite      = dot;
            sr.color       = Color.magenta;
            sr.sortingOrder = topOrder;
        }
    }

    // Rebuilds GPU-instanced rock layer data from the current perimeter points.
}