using UnityEngine;

/// <summary>
/// 2D equivalent of <see cref="PlayerMouseAimer"/>. Uses <see cref="MousePointer"/>
/// for cursor raycasting and applies 2D-appropriate range checks.
/// Exposes a subset of hover results via <see cref="PlayerAimHoverResult"/>
/// that the 2D interaction system consumes.
/// </summary>
[RequireComponent(typeof(PlayerController2D))]
public class PlayerMouseAimer2D : MonoBehaviour
{
    [SerializeField] private float _interactionRange = 3f;

    private MousePointer _pointer;

    public float InteractionRange => _interactionRange;
    public Vector3 WorldAimPoint { get; private set; }
    public WorldItem HoveredKeyPickup { get; private set; }
    public IInteractionPromptProvider HoveredInteractable { get; private set; }
    public PlayerAimHoverResult HoverResult { get; private set; }

    private void Update()
    {
        if (_pointer == null) _pointer = MousePointer.Instance;
        if (_pointer == null) return;

        GameObject hovered = _pointer.HoverObject;
        bool hasHit = _pointer.HasHit;
        Vector3 point = _pointer.WorldPoint;

        UpdateAimPoint(hasHit, point);
        UpdateHoveredKeyPickup(hovered);
        UpdateHoveredInteractable(hovered, hasHit, point);
        BuildHoverResult(hasHit, point, hovered);
    }

    private void UpdateAimPoint(bool hasHit, Vector3 point)
    {
        if (hasHit)
        {
            WorldAimPoint = point;
            return;
        }
        WorldAimPoint = transform.position + (Vector3)(_pointer.WorldRay.direction.normalized * _interactionRange);
    }

    private void UpdateHoveredKeyPickup(GameObject hovered)
    {
        WorldItem item = hovered != null ? hovered.GetComponentInParent<WorldItem>() : null;
        bool needsKey = item != null && item.Item != null && !item.Item.AutoCollects;
        HoveredKeyPickup = needsKey && WithinRange(item.transform.position) ? item : null;
    }

    private void UpdateHoveredInteractable(GameObject hovered, bool hasHit, Vector3 point)
    {
        HoveredInteractable = null;
        if (!hasHit || hovered == null) return;
        if (!WithinRange(point)) return;

        var provider = hovered.GetComponentInParent<IInteractionPromptProvider>();
        if (provider != null) HoveredInteractable = provider;
    }

    private void BuildHoverResult(bool hasHit, Vector3 point, GameObject hovered)
    {
        HoverResult = new PlayerAimHoverResult
        {
            HasTarget = hasHit,
            WorldPoint = WorldAimPoint,
            WorldItem = HoveredKeyPickup,
            Interactable = HoveredInteractable,
            InInteractionRange = hasHit && hovered != null && WithinRange(point),
        };
    }

    private bool WithinRange(Vector3 worldPos)
        => Vector3.Distance(transform.position, worldPos) <= _interactionRange;
}
