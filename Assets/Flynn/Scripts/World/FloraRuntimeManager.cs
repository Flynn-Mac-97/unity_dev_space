using UnityEngine;
using UnityEngine.Tilemaps;
using Flynn.Resources;

namespace Flynn.World
{
    /// <summary>
    /// Scans a tilemap at runtime and instantiates a prefab at each occupied tile position.
    /// Used for flora (bushes, rocks, etc.) that need collider/hit behaviour but not a hover tag.
    /// </summary>
    public class FloraRuntimeManager : MonoBehaviour
    {
        [Header("Source")]
        [Tooltip("Tilemap whose tiles define spawn positions.")]
        [SerializeField] private Tilemap _tilemap;

        [Header("Prefab")]
        [Tooltip("Prefab to instantiate at each tile position.")]
        [SerializeField] private GameObject _floraPrefab;

        [Header("Options")]
        [Tooltip("Parent for instantiated objects. Null = this transform.")]
        [SerializeField] private Transform _parent;
        [Tooltip("Destroy the tilemap after spawning (visuals handled by prefabs).")]
        [SerializeField] private bool _hideTilemapAfterSpawn = true;

        private void Start()
        {
            if (_tilemap == null || _floraPrefab == null) return;

            _tilemap.CompressBounds();

            Vector3Int min = _tilemap.cellBounds.min;
            Vector3Int max = _tilemap.cellBounds.max;

            for (int x = min.x; x < max.x; x++)
            {
                for (int y = min.y; y < max.y; y++)
                {
                    Vector3Int cell = new Vector3Int(x, y, 0);
                    if (!_tilemap.HasTile(cell)) continue;

                    Vector3 worldPos = _tilemap.GetCellCenterWorld(cell);
                    GameObject flora = Instantiate(_floraPrefab, worldPos, Quaternion.identity, _parent != null ? _parent : transform);
                    flora.transform.localScale = _tilemap.transform.lossyScale;

                    // Set the tile sprite on VisualRoot and Outline SpriteRenderers
                    TileBase tileBase = _tilemap.GetTile(cell);
                    if (tileBase is Tile tile && tile.sprite != null)
                    {
                        var vr = flora.transform.Find("VisualRoot");
                        if (vr != null)
                        {
                            var vrSr = vr.GetComponent<SpriteRenderer>();
                            if (vrSr != null) vrSr.sprite = tile.sprite;

                            var outline = vr.Find("Outline");
                            if (outline != null)
                            {
                                var outlineSr = outline.GetComponent<SpriteRenderer>();
                                if (outlineSr != null) outlineSr.sprite = tile.sprite;
                            }
                        }
                    }

                    // Add contact handler for player interaction (shake + speed slow)
                    // Wind registration is handled by ResourceNode.Start() automatically.
                    flora.AddComponent<FloraContactHandler>();
                }
            }

            if (_hideTilemapAfterSpawn)
            {
                _tilemap.gameObject.SetActive(false);
            }
        }
    }
}
