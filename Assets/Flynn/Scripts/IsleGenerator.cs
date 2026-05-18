using UnityEngine;

public class IsleGenerator : MonoBehaviour
{
    [SerializeField] private GameObject spritePrefab; // Assign a prefab with a SpriteRenderer in the inspector.
    private GameObject[,] tileObjects; // Store references to instantiated tile GameObjects for future updates.
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
        Water,
        Sand,
        Grass,
        Forest,
        Mountain
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

    // Iterate over islands and place sprite on 3d plane facing up.
    private void GenerateIsland()
    {
        for (int x = 0; x < island2D.GetLength(0); x++)
        {
            for (int y = 0; y < island2D.GetLength(1); y++)
            {
                TileType tile = (TileType)island2D[x, y];
                // Instantiate a sprite prefab at the correct position based on tile type.
                GameObject tileGO = Instantiate(spritePrefab, new Vector3(x, 0, y), Quaternion.Euler(90, 0, 0), this.gameObject.transform);
                // Set the sprite based on tile type (you would assign these in the inspector).
                SpriteRenderer sr = tileGO.GetComponent<SpriteRenderer>();
                switch (tile)
                {
                    //for now set color based on tile type, you would replace this with actual sprites in a real implementation.
                    case TileType.Water:
                        sr.color = Color.blue;
                        break;
                    case TileType.Sand:
                        sr.color = Color.yellow;
                        break;
                    case TileType.Grass:
                        sr.color = Color.green;
                        break;
                    case TileType.Forest:
                        sr.color = new Color(0, 0.5f, 0); // Dark green
                        break;
                    case TileType.Mountain:
                        sr.color = Color.gray;
                        break;
                }
            }
        }
    }
    // Use a sunken base collider for ground.
    private void GenerateIslandColliders()
    {
        // Base collider: top surface sits at y=0 (ground level).
        BoxCollider islandCollider = gameObject.AddComponent<BoxCollider>();
        islandCollider.size = new Vector3(island2D.GetLength(0), 1, island2D.GetLength(1));

        float baseHeight = islandCollider.size.y;
        islandCollider.center = new Vector3(
            island2D.GetLength(0) / 2f - 0.5f,
            -baseHeight * 0.5f,
            island2D.GetLength(1) / 2f - 0.5f
        );
    }

    private void Start()
    {
        // Generate the island and its colliders when the game starts.
        GenerateIsland();
        GenerateIslandColliders();
    }
}