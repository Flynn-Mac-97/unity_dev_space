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

    [Header("Rock Layers")]
    [SerializeField] private Sprite rockTestSprite;
    [SerializeField, Range(1, 8)]     private int   rockLayerCount     = 4;
    [SerializeField]                  private float rockDropY          = 0.4f;
    [SerializeField]                  private float rockExpandX = 0f;
    // X=0 is a side rock (|outward.x|≈1), X=1 is a center rock (|outward.x|≈0).
    // Y scales how much rockExpandX is applied at that position.
    [SerializeField] private AnimationCurve rockExpandXCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    [SerializeField]                  private float rockExpandZ = 0f;
    // X=0 is a side rock (|outward.z|≈0), X=1 is a top/bottom rock (|outward.z|≈1).
    // Y scales how much rockExpandZ is applied at that position.
    [SerializeField] private AnimationCurve rockExpandZCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    [SerializeField]                  private float rockFaceHeight     = 1.2f;
    [SerializeField]                  private Color rockBaseColor      = new Color(0.55f, 0.50f, 0.45f, 1f);
    [SerializeField, Range(0f, 0.3f)] private float rockColorDarken   = 0.06f;
    [SerializeField]                  private float rockSpriteSize     = 0.25f;
    [SerializeField]                  private float rockMinSpacing     = 0.5f;
    [SerializeField]                  private float frontArcZThreshold = 0f;

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
    private GameObject _rocksRoot;

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
    // Returns sand tile centers sorted clockwise by angle from the island center
    // and draws connected debug lines to visualise the perimeter.
    private List<Vector3> GetOuterRimPoints()
    {
        var rimPoints = new List<Vector3>();

        for (int x = 0; x < island2D.GetLength(0); x++)
        {
            for (int y = 0; y < island2D.GetLength(1); y++)
            {
                if (island2D[x, y] == SandIndex)
                {
                    rimPoints.Add(TileToWorld(x, y));
                }
            }
        }

        if (rimPoints.Count < 2)
        {
            Debug.LogWarning("GetOuterRimPoints: not enough sand tiles to draw a perimeter.");
            return rimPoints;
        }

        // Sort clockwise by angle around the island center (XZ plane).
        Vector3 center = Vector3.zero;
        rimPoints.Sort((a, b) =>
        {
            float angleA = Mathf.Atan2(a.z - center.z, a.x - center.x);
            float angleB = Mathf.Atan2(b.z - center.z, b.x - center.x);
            return angleB.CompareTo(angleA);
        });

        // Draw connected debug lines between consecutive perimeter points.
        float drawDuration = 10f;
        for (int i = 0; i < rimPoints.Count; i++)
        {
            Vector3 current = rimPoints[i] + Vector3.up * 0.1f;
            Vector3 next    = rimPoints[(i + 1) % rimPoints.Count] + Vector3.up * 0.1f;
            Debug.DrawLine(current, next, Color.red, drawDuration);
        }

        return rimPoints;
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

    private void Start()
    {
        List<Vector3> rimPoints = GetOuterRimPoints();
        _detailPoints = SubdivideRimPoints(rimPoints);
        UpdateGrassShapeToOuterRim(_detailPoints);
        BuildRockLayers(_detailPoints);
    }

    // Clears the existing rock layer visuals and rebuilds them from the current
    // serialized settings. Safe to call repeatedly — ideal for live Inspector tweaking.
    [ContextMenu("Regenerate Rocks")]
    private void RegenerateRocks()
    {
        if (_detailPoints == null || _detailPoints.Count < 3) return;

        // Destroy all existing layer children in-place so the root GO is reused.
        // This avoids the Find + DestroyImmediate timing issues in OnValidate.
        if (_rocksRoot != null)
        {
            for (int i = _rocksRoot.transform.childCount - 1; i >= 0; i--)
                Destroy(_rocksRoot.transform.GetChild(i).gameObject);
        }

        BuildRockLayers(_detailPoints);
    }

    private void OnValidate()
    {
        // Only regenerate while playing so OnValidate in edit mode doesn't
        // fire before the island has been generated.
        if (!Application.isPlaying) return;
        RegenerateRocks();
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

    // TEST: Spawns a small square sprite at each front-arc point per rock layer so
    // the positions and layer offsets can be validated before the SpriteShape is wired up.
    private void BuildRockLayers(List<Vector3> detailPoints)
    {
        // Collect every front-facing candidate first (no distance cull here — the
        // greedy sequential cull would drop the dense bottom-center cluster).
        var candidates = new List<Vector3>();
        foreach (var p in detailPoints)
        {
            if (p.z <= frontArcZThreshold)
                candidates.Add(p);
        }

        if (candidates.Count < 3) return;

        // Compute the cumulative arc-length along the candidate chain so we can
        // resample at perfectly even intervals regardless of local point density.
        var arcLengths = new float[candidates.Count];
        arcLengths[0] = 0f;
        for (int k = 1; k < candidates.Count; k++)
            arcLengths[k] = arcLengths[k - 1] + Vector3.Distance(candidates[k - 1], candidates[k]);

        float totalArc = arcLengths[arcLengths.Length - 1];
        int   sampleCount = Mathf.Max(3, Mathf.RoundToInt(totalArc / rockMinSpacing));

        var frontArc = new List<Vector3>(sampleCount);
        for (int s = 0; s < sampleCount; s++)
        {
            float target = (s / (float)(sampleCount - 1)) * totalArc;

            // Binary-search for the segment that contains this arc-length target.
            int lo = 0, hi = candidates.Count - 2;
            while (lo < hi)
            {
                int mid = (lo + hi) / 2;
                if (arcLengths[mid + 1] < target) lo = mid + 1;
                else hi = mid;
            }

            float segLen = arcLengths[lo + 1] - arcLengths[lo];
            float t      = segLen > 0f ? (target - arcLengths[lo]) / segLen : 0f;
            frontArc.Add(Vector3.Lerp(candidates[lo], candidates[lo + 1], t));
        }

        if (frontArc.Count < 3) return;

        var grassRenderer = grassSpriteShape.GetComponent<SpriteShapeRenderer>();
        int baseOrder     = grassRenderer != null ? grassRenderer.sortingOrder : 0;

        // Use assigned sprite, or create a 1x1 white fallback that works across Unity versions.
        Sprite dot;
        if (rockTestSprite != null)
        {
            dot = rockTestSprite;
        }
        else
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            dot = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }

        if (_rocksRoot == null)
        {
            _rocksRoot = new GameObject("RockLayers_TEST");
            _rocksRoot.transform.SetParent(transform);
            _rocksRoot.transform.localPosition = Vector3.zero;
            _rocksRoot.transform.localRotation = Quaternion.identity;
        }

        for (int i = 0; i < rockLayerCount; i++)
        {
            float dropY  = (i + 1) * rockDropY;
            float darken = rockColorDarken * i;
            Color color  = new Color(
                Mathf.Clamp01(rockBaseColor.r - darken),
                Mathf.Clamp01(rockBaseColor.g - darken),
                Mathf.Clamp01(rockBaseColor.b - darken),
                rockBaseColor.a);

            GameObject layerRoot = new GameObject($"RockLayer_{i}");
            layerRoot.transform.SetParent(_rocksRoot.transform);
            layerRoot.transform.localPosition = Vector3.zero;
            layerRoot.transform.localRotation = Quaternion.identity;

            // Derive the local scale that makes the sprite exactly rockSpriteSize world units wide.
            float spriteWorldWidth = dot.rect.width / dot.pixelsPerUnit;
            float spriteScale      = spriteWorldWidth > 0f ? rockSpriteSize / spriteWorldWidth : rockSpriteSize;

            float expandX = rockExpandX;
            float expandZ = rockExpandZ;

            foreach (var p in frontArc)
            {
                // Inset the sprite centre by half its width along the full outward normal
                // (both X and Z) so the visual edge aligns to the grass perimeter on all
                // sides of the arc, not just the left/right extremes.
                Vector3 outward       = new Vector3(p.x, 0f, p.z).normalized;
                float   halfSize      = rockSpriteSize * 0.5f;
                float   centeredness  = 1f - Mathf.Abs(outward.x);   // 0 at sides, 1 at center
                float   zedness       = Mathf.Abs(outward.z);         // 0 at sides, 1 at top/bottom
                float   xCurveScale   = rockExpandXCurve.Evaluate(centeredness);
                float   zCurveScale   = rockExpandZCurve.Evaluate(zedness);
                Vector3 worldPos = new Vector3(
                    p.x - outward.x * halfSize - outward.x * expandX * xCurveScale,
                    -dropY,
                    p.z - outward.z * halfSize - outward.z * expandZ * zCurveScale);

                GameObject marker = new GameObject($"L{i}_pt");
                marker.transform.SetParent(layerRoot.transform);
                marker.transform.position = worldPos;
                marker.transform.localScale = Vector3.one * spriteScale;

                var sr = marker.AddComponent<SpriteRenderer>();
                sr.sprite       = dot;
                sr.color        = color;
                sr.sortingOrder = baseOrder - (i + 1);
            }
        }
    }
}