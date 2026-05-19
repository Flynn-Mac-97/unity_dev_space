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

    // Maps a tile grid coordinate to a world position centered at the origin.
    private Vector3 TileToWorld(int x, int y)
    {
        return new Vector3(x - (mapWidth - 1) * 0.5f, 0f, y - (mapHeight - 1) * 0.5f);
    }

    private void Start()
    {
        List<Vector3> rimPoints = GetOuterRimPoints();
        UpdateGrassShapeToOuterRim(rimPoints);
        SpawnCliffPrefabs(rimPoints);
    }

    // Updates the grass SpriteShape spline to match the clockwise-sorted sand perimeter.
    private void UpdateGrassShapeToOuterRim(List<Vector3> rimPoints)
    {
        if (grassSpriteShape == null)
        {
            Debug.LogWarning("UpdateGrassShapeToOuterRim: no SpriteShapeController assigned.");
            return;
        }

        if (rimPoints.Count < 2) return;

        Spline spline = grassSpriteShape.spline;
        spline.Clear();

        for (int i = 0; i < rimPoints.Count; i++)
        {
            AddOuterRimPoint(spline, i, rimPoints[i]);
        }

        spline.isOpenEnded = false;
    }

    private void AddOuterRimPoint(Spline spline, int index, Vector3 worldPoint)
    {
        Vector3 localPoint = grassSpriteShape.transform.InverseTransformPoint(worldPoint);
        spline.InsertPointAt(index, localPoint);
        spline.SetTangentMode(index, ShapeTangentMode.Linear);
    }

    // Instantiates cliffPrefab at each outer rim point, rotated to face outward.
    private void SpawnCliffPrefabs(List<Vector3> rimPoints)
    {
        if (cliffPrefab == null) return;

        int n = rimPoints.Count;
        for (int i = 0; i < n; i++)
        {
            Vector3 prev = rimPoints[(i - 1 + n) % n];
            Vector3 curr = rimPoints[i];
            Vector3 next = rimPoints[(i + 1) % n];

            // Outward direction = average of the two adjacent edge normals.
            Vector3 toPrev    = (prev - curr).normalized;
            Vector3 toNext    = (next - curr).normalized;
            Vector3 outwardXZ = -(toPrev + toNext).normalized;
            if (outwardXZ.sqrMagnitude < 0.001f)
                outwardXZ = -curr.normalized; // fallback: point away from island center

            Quaternion rotation = Quaternion.LookRotation(outwardXZ, Vector3.up);
            Instantiate(cliffPrefab, curr, rotation, transform);
        }
    }
}