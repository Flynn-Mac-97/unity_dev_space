using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Makes one hotbar slot draggable: dragging the slot's icon off the hotbar bar and releasing
/// drops that slot's whole stack into the world via <see cref="PlayerDropController.DropSlot"/>.
/// Added to each slot by <see cref="PlayerHudUGUI"/>; the slot border is the raycast target.
/// </summary>
public class HotbarSlotDragHandler : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private int _slotIndex;
    private PlayerDropController _dropController;
    private RectTransform _barRect;
    private RectTransform _icon;
    private Vector2 _iconHome;
    private bool _dragging;

    public void Init(int slotIndex, PlayerDropController dropController, RectTransform barRect, RectTransform icon)
    {
        _slotIndex = slotIndex;
        _dropController = dropController;
        _barRect = barRect;
        _icon = icon;
    }

    public void OnBeginDrag(PointerEventData e)
    {
        if (_icon == null) return;
        _dragging = true;
        _iconHome = _icon.anchoredPosition;
    }

    public void OnDrag(PointerEventData e)
    {
        if (_dragging && _icon != null) _icon.position = e.position;
    }

    public void OnEndDrag(PointerEventData e)
    {
        if (!_dragging) return;
        _dragging = false;

        // ScreenSpaceOverlay → null camera for the containment test.
        bool releasedOffBar = _barRect == null ||
            !RectTransformUtility.RectangleContainsScreenPoint(_barRect, e.position, null);

        if (_icon != null) _icon.anchoredPosition = _iconHome; // snap icon home regardless
        if (releasedOffBar && _dropController != null) _dropController.DropSlot(_slotIndex);
    }
}
