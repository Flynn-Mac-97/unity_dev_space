using UnityEngine;

/// <summary>
/// Pulls an auto-collecting <see cref="WorldItem"/> to the player after a short settle delay, then
/// collects it: resources/items go to the inventory, currencies to their counter. If the inventory
/// has no room the item idles on the ground and re-checks periodically (VS-style gem behaviour).
///
/// Finds the player through a <see cref="PlayerAnchor"/> SO — no FindObjectOfType. Non-auto items
/// (the wrench) disable this component and are picked up with the pickup key instead.
/// </summary>
[RequireComponent(typeof(WorldItem))]
public class DroppedItemMagnet : MonoBehaviour
{
    [SerializeField] private PlayerAnchor _playerAnchor;

    [Header("Timing")]
    [Tooltip("Seconds the drop sits/settles before it may fly to the player.")]
    [SerializeField] private float _settleDelay = 0.6f;
    [Tooltip("How often to re-check for inventory room while waiting.")]
    [SerializeField] private float _roomRecheckInterval = 0.4f;

    [Header("Flight")]
    [SerializeField] private float _maxSpeed = 7f;
    [SerializeField] private float _acceleration = 16f;
    [SerializeField] private float _collectRadius = 0.55f;
    [SerializeField] private float _targetHeight = 0.6f;

    private WorldItem _item;
    private Rigidbody _rb;
    private PlayerInventory _inventory;

    private float _age;
    private float _extraDelay;
    private float _recheckTimer;
    private float _speed;
    private bool _flying;

    /// <summary>
    /// Delay auto-collection by <paramref name="seconds"/> beyond the normal settle (used when the
    /// player drops an item so it doesn't instantly fly back). Takes the longer of any pending delay.
    /// </summary>
    public void Suppress(float seconds)
    {
        _extraDelay = Mathf.Max(_extraDelay, seconds);
        _flying = false;
    }

    private void Awake()
    {
        _item = GetComponent<WorldItem>();
        _rb = GetComponent<Rigidbody>();
    }

    private void OnEnable()
    {
        _age = 0f;
        _extraDelay = 0f;
        _recheckTimer = 0f;
        _speed = 0f;
        _flying = false;
    }

    private void Update()
    {
        ItemDefinition def = _item != null ? _item.Item : null;
        if (def == null) return;

        // Non-auto items (wrench) wait for the pickup key — not our job.
        if (!def.AutoCollects) { enabled = false; return; }

        _age += Time.deltaTime;
        if (_age < _settleDelay + _extraDelay) return;

        Transform player = _playerAnchor != null ? _playerAnchor.Current : null;
        if (player == null) return;

        if (!_flying)
        {
            _recheckTimer -= Time.deltaTime;
            if (_recheckTimer > 0f) return;
            _recheckTimer = _roomRecheckInterval;
            if (!HasRoom(def, player)) return;
            BeginFlight();
        }

        _speed = Mathf.MoveTowards(_speed, _maxSpeed, _acceleration * Time.deltaTime);
        Vector3 target = player.position + Vector3.up * _targetHeight;
        transform.position = Vector3.MoveTowards(transform.position, target, _speed * Time.deltaTime);

        if (Vector3.Distance(transform.position, target) <= _collectRadius)
            Collect(def, player);
    }

    private bool HasRoom(ItemDefinition def, Transform player)
    {
        if (def.isCurrency) return true;
        EnsureInventory(player);
        return _inventory != null && _inventory.HasRoomFor(def);
    }

    private void BeginFlight()
    {
        _flying = true;
        if (_rb != null)
        {
            _rb.velocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _rb.isKinematic = true; // hand control to the magnet's MoveTowards
        }
    }

    private void Collect(ItemDefinition def, Transform player)
    {
        if (def.isCurrency)
        {
            if (def.currencyTarget != null) def.currencyTarget.Add(_item.Count);
            Destroy(gameObject);
            return;
        }

        EnsureInventory(player);
        int added = _inventory != null ? _inventory.TryAddItem(def, _item.Count) : 0;
        if (added >= _item.Count)
        {
            Destroy(gameObject);
            return;
        }

        // Inventory filled mid-flight: drop the remainder back on the ground and idle.
        if (added > 0) _item.Reduce(added);
        _flying = false;
        _speed = 0f;
        if (_rb != null) _rb.isKinematic = false;
    }

    private void EnsureInventory(Transform player)
    {
        if (_inventory == null && player != null)
            _inventory = player.GetComponentInChildren<PlayerInventory>();
    }
}
