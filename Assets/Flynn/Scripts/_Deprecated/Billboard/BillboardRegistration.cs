using UnityEngine;

/// <summary>
/// Attach to any GameObject that should be billboarded by the central
/// BillboardManager. Replaces the per-object Billboard.cs approach.
///
/// OnEnable finds the BillboardManager and registers; OnDisable unregisters.
/// Configure BillboardMode in the inspector to control rotation behaviour.
/// </summary>
[RequireComponent(typeof(Transform))]
public class BillboardRegistration : MonoBehaviour, IBillboardTarget
{
    [SerializeField] private BillboardMode _mode = BillboardMode.PitchOnly;

    public Transform BillboardTransform => transform;
    public BillboardMode Mode => _mode;

    private BillboardManager _manager;

    private void OnEnable()
    {
        if (_manager == null)
            _manager = FindObjectOfType<BillboardManager>();

        if (_manager != null)
            _manager.Register(this);
        else
            Debug.LogWarning("[BillboardRegistration] No BillboardManager found in scene.", this);
    }

    private void OnDisable()
    {
        if (_manager != null)
            _manager.Unregister(this);
    }
}
