using UnityEngine;

/// <summary>
/// Shows the active inventory item as a held prop in the cube character's hand socket.
/// Listens to <see cref="PlayerInventory"/> slot changes and swaps the visual: the
/// item's <see cref="ItemDefinition.worldPrefab"/> is instantiated (stripped of its
/// world-pickup behaviour and physics so it's purely cosmetic), or a small placeholder
/// cube if the item has no prefab. Empty / no active item → empty hand.
/// </summary>
[RequireComponent(typeof(PlayerInventory))]
public class HeldItemSocket : MonoBehaviour
{
    [SerializeField] private PlayerInventory _inventory;
    [Tooltip("Hand transform the held prop is parented under (e.g. ProceduralCharacterDriver's HandSocketR).")]
    [SerializeField] private Transform _socket;
    [Tooltip("Uniform scale applied to the held prop in the hand.")]
    [SerializeField] private float _heldScale = 0.5f;
    [Tooltip("Placeholder cube colour used when an item has no worldPrefab.")]
    [SerializeField] private Color _placeholderColor = new Color(0.8f, 0.7f, 0.2f, 1f);

    private GameObject _current;
    private ItemDefinition _shownItem;

    private void Awake()
    {
        if (_inventory == null) _inventory = GetComponent<PlayerInventory>();
    }

    private void OnEnable()
    {
        if (_inventory != null)
        {
            _inventory.OnActiveSlotChanged += OnActiveSlotChanged;
            _inventory.OnSlotChanged += OnSlotChanged;
        }
        Refresh();
    }

    private void OnDisable()
    {
        if (_inventory != null)
        {
            _inventory.OnActiveSlotChanged -= OnActiveSlotChanged;
            _inventory.OnSlotChanged -= OnSlotChanged;
        }
    }

    private void OnActiveSlotChanged(int _) => Refresh();

    private void OnSlotChanged(int slot)
    {
        // Only the active slot affects what's in the hand.
        if (_inventory != null && slot == _inventory.ActiveSlotIndex) Refresh();
    }

    private void Refresh()
    {
        if (_socket == null) return;
        ItemDefinition item = _inventory != null ? _inventory.ActiveItem : null;
        if (item == _shownItem) return; // already showing the right thing

        Clear();
        _shownItem = item;
        if (item == null) return;

        _current = item.worldPrefab != null ? BuildFromPrefab(item) : BuildPlaceholder();
        _current.transform.SetParent(_socket, false);
        _current.transform.localPosition = Vector3.zero;
        _current.transform.localRotation = Quaternion.identity;
        _current.transform.localScale = Vector3.one * _heldScale;
    }

    private GameObject BuildFromPrefab(ItemDefinition item)
    {
        GameObject go = Instantiate(item.worldPrefab);
        go.name = $"Held_{item.itemId}";
        StripToVisual(go);
        return go;
    }

    private GameObject BuildPlaceholder()
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Held_Placeholder";
        var col = go.GetComponent<Collider>();
        if (col != null) Destroy(col);
        var r = go.GetComponent<Renderer>();
        if (r != null) r.material.color = _placeholderColor;
        return go;
    }

    /// <summary>Strip pickup/physics behaviour so the held copy is cosmetic only.
    /// Order matters: WorldItem has [RequireComponent(typeof(Collider))], so the WorldItem
    /// (and any other behaviour depending on the collider/body) must be destroyed BEFORE the
    /// collider, or Unity refuses the removal and logs a dependency error.</summary>
    private static void StripToVisual(GameObject go)
    {
        foreach (var wi in go.GetComponentsInChildren<WorldItem>()) Destroy(wi);
        foreach (var mb in go.GetComponentsInChildren<DroppedItemMagnet>()) Destroy(mb);
        foreach (var rb in go.GetComponentsInChildren<Rigidbody>()) Destroy(rb);
        foreach (var col in go.GetComponentsInChildren<Collider>()) Destroy(col);
    }

    private void Clear()
    {
        if (_current != null) Destroy(_current);
        _current = null;
        _shownItem = null;
    }
}
