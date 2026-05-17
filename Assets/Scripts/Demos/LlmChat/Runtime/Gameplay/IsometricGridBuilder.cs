using UnityEngine;

public class IsometricGridBuilder : MonoBehaviour
{
    [SerializeField] private int width  = 10;
    [SerializeField] private int depth  = 10;
    [SerializeField] private Color colorA = new Color(0.45f, 0.72f, 0.45f);
    [SerializeField] private Color colorB = new Color(0.30f, 0.52f, 0.30f);

    private void Awake()
    {
        // One flat BoxCollider as the floor — CharacterController walks on this
        var floor = new GameObject("FloorCollider");
        floor.transform.SetParent(transform);
        floor.transform.position = new Vector3(width * 0.5f, -0.05f, depth * 0.5f);
        var box = floor.AddComponent<BoxCollider>();
        box.size = new Vector3(width, 0.1f, depth);

        // Visual tiles (quads rotated flat, checkerboard colours)
        var matA = new Material(Shader.Find("Sprites/Default")) { color = colorA };
        var matB = new Material(Shader.Find("Sprites/Default")) { color = colorB };

        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < depth; z++)
            {
                var tile = GameObject.CreatePrimitive(PrimitiveType.Quad);
                tile.name = $"Tile_{x}_{z}";
                tile.transform.SetParent(transform);
                tile.transform.position   = new Vector3(x + 0.5f, 0f, z + 0.5f);
                tile.transform.rotation   = Quaternion.Euler(90f, 0f, 0f);
                tile.transform.localScale = new Vector3(0.97f, 0.97f, 1f);
                tile.GetComponent<Renderer>().sharedMaterial = (x + z) % 2 == 0 ? matA : matB;
                Object.Destroy(tile.GetComponent<MeshCollider>());
            }
        }
    }
}
