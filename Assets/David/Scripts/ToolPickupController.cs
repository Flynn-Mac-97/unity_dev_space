using UnityEngine;

/// <summary>
/// Attach to the player. Press F near a Tool-tagged object to pick it up (floats at the right hand).
/// Press F again to drop it at the current position.
/// </summary>
public class ToolPickupController : MonoBehaviour
{
    [SerializeField] float pickupRange = 2.5f;
    [SerializeField] KeyCode interactKey = KeyCode.F;
    [SerializeField] Vector3 holdLocalOffset = new Vector3(0.55f, 1.15f, 0.25f);
    [SerializeField] Transform holdAnchor;

    GameObject _heldTool;
    Rigidbody _heldRigidbody;
    bool _hadRigidbody;
    bool _wasKinematic;
    bool _usedGravity;
    Collider _heldCollider;
    bool _colliderWasTrigger;

    void Awake()
    {
        EnsureHoldAnchor();
    }

    void Update()
    {
        if (!Input.GetKeyDown(interactKey))
            return;

        if (_heldTool != null)
            DropTool();
        else
            TryPickupNearestTool();
    }

    void EnsureHoldAnchor()
    {
        if (holdAnchor != null)
            return;

        var anchorGo = new GameObject("ToolHoldPoint");
        anchorGo.transform.SetParent(transform, false);
        anchorGo.transform.localPosition = holdLocalOffset;
        anchorGo.transform.localRotation = Quaternion.identity;
        holdAnchor = anchorGo.transform;
    }

    void TryPickupNearestTool()
    {
        GameObject nearest = FindNearestTool();
        if (nearest == null)
            return;

        PickUpTool(nearest);
    }

    GameObject FindNearestTool()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, pickupRange);
        GameObject nearest = null;
        float nearestSqr = pickupRange * pickupRange;

        foreach (Collider hit in hits)
        {
            if (hit == null)
                continue;

            GameObject tool = GetToolRoot(hit.transform);
            if (tool == null || tool == _heldTool)
                continue;

            float sqr = (tool.transform.position - transform.position).sqrMagnitude;
            if (sqr < nearestSqr)
            {
                nearestSqr = sqr;
                nearest = tool;
            }
        }

        return nearest;
    }

    static GameObject GetToolRoot(Transform t)
    {
        while (t != null)
        {
            if (t.CompareTag(ItemConstants.TOOL))
                return t.gameObject;
            t = t.parent;
        }

        return null;
    }

    void PickUpTool(GameObject tool)
    {
        _heldTool = tool;
        CacheAndDisablePhysics(tool);

        tool.transform.SetParent(holdAnchor, worldPositionStays: false);
        tool.transform.localPosition = Vector3.zero;
        tool.transform.localRotation = Quaternion.identity;
    }

    void CacheAndDisablePhysics(GameObject tool)
    {
        _heldRigidbody = tool.GetComponent<Rigidbody>();
        _hadRigidbody = _heldRigidbody != null;
        if (_hadRigidbody)
        {
            _wasKinematic = _heldRigidbody.isKinematic;
            _usedGravity = _heldRigidbody.useGravity;
            _heldRigidbody.isKinematic = true;
            _heldRigidbody.useGravity = false;
            _heldRigidbody.velocity = Vector3.zero;
            _heldRigidbody.angularVelocity = Vector3.zero;
        }

        _heldCollider = tool.GetComponent<Collider>();
        if (_heldCollider != null)
        {
            _colliderWasTrigger = _heldCollider.isTrigger;
            _heldCollider.isTrigger = true;
        }
    }

    void DropTool()
    {
        if (_heldTool == null)
            return;

        GameObject tool = _heldTool;
        _heldTool = null;

        tool.transform.SetParent(null, worldPositionStays: true);
        RestorePhysics(tool);
    }

    void RestorePhysics(GameObject tool)
    {
        if (_heldCollider != null)
            _heldCollider.isTrigger = _colliderWasTrigger;

        if (_hadRigidbody && _heldRigidbody != null)
        {
            _heldRigidbody.isKinematic = _wasKinematic;
            _heldRigidbody.useGravity = _usedGravity;
        }

        _heldRigidbody = null;
        _heldCollider = null;
        _hadRigidbody = false;
    }

    public bool IsHoldingTool => _heldTool != null;

    public GameObject HeldTool => _heldTool;

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, pickupRange);

        if (holdAnchor != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(holdAnchor.position, 0.08f);
        }
    }
#endif
}
