using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Flynn.Map
{
    /// <summary>
    /// Single control point for a level's tilemap layers. Holds one Tilemap per
    /// <see cref="MapLayer"/> (assigned in the Inspector) and exposes lookup +
    /// visibility control, so the rest of the game never hunts for tilemaps by name.
    ///
    /// On Awake it pushes deterministic sorting order from the layer stack, so the
    /// back-to-front draw order can't drift. One per scene, on a MAP object.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class MapLayerManager : MonoBehaviour
    {
        public static MapLayerManager Instance { get; private set; }

        [System.Serializable]
        public struct LayerBinding
        {
            public MapLayer Layer;
            public Tilemap Tilemap;
        }

        [Tooltip("One Tilemap per layer. Sorting order is applied automatically from the layer enum.")]
        [SerializeField] private List<LayerBinding> _bindings = new();

        private readonly Dictionary<MapLayer, Tilemap> _byLayer = new();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"[MapLayerManager] Duplicate; destroying {name}.", this);
                Destroy(this);
                return;
            }
            Instance = this;

            _byLayer.Clear();
            foreach (var b in _bindings)
                if (b.Tilemap != null) _byLayer[b.Layer] = b.Tilemap;

            ApplySorting();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>The tilemap for a layer, or null if none is bound.</summary>
        public Tilemap Get(MapLayer layer) => _byLayer.TryGetValue(layer, out var t) ? t : null;

        /// <summary>Show/hide a whole layer (e.g. hide the foreground while indoors).</summary>
        public void SetLayerVisible(MapLayer layer, bool visible)
        {
            var t = Get(layer);
            if (t != null) t.gameObject.SetActive(visible);
        }

        /// <summary>Push sorting order from the layer stack so back-to-front is fixed.</summary>
        private void ApplySorting()
        {
            foreach (var kv in _byLayer)
            {
                var renderer = kv.Value.GetComponent<TilemapRenderer>();
                if (renderer != null) renderer.sortingOrder = (int)kv.Key * 100;
            }
        }
    }
}
