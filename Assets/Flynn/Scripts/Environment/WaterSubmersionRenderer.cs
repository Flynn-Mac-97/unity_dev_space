using UnityEngine;



using Flynn.Player.Interaction;
namespace Flynn.Environment
{
    /// <summary>
    /// Drives a secondary camera that renders a submersion mask to a RenderTexture.
    /// Objects on the WaterInteraction layer are rendered as white silhouettes,
    /// which the StyledWater2D shader samples to draw intersection outlines.
    ///
    /// Attach to the same GameObject as the main camera (or a child).
    /// The submersion camera mirrors the main camera each frame.
    /// </summary>
    [ExecuteInEditMode]
    public class WaterSubmersionRenderer : MonoBehaviour
    {
        [SerializeField] private Camera _mainCamera;
        [SerializeField] private Camera _submersionCamera;
        [SerializeField] private RenderTexture _submersionMask;
        [SerializeField] private LayerMask _interactionLayerMask;

        [Header("Material Binding")]
        [SerializeField] private Material _waterMaterial;

        private static readonly int SubmersionMaskID = Shader.PropertyToID("_SubmersionMask");

        private void OnEnable()
        {
            if (_mainCamera == null)
                _mainCamera = Camera.main;

            if (_waterMaterial != null && _submersionMask != null)
                _waterMaterial.SetTexture(SubmersionMaskID, _submersionMask);
        }

        private void LateUpdate()
        {
            if (_mainCamera == null || _submersionCamera == null) return;

            // Sync transform
            _submersionCamera.transform.position = _mainCamera.transform.position;
            _submersionCamera.transform.rotation = _mainCamera.transform.rotation;

            // Sync projection
            _submersionCamera.orthographic = _mainCamera.orthographic;
            _submersionCamera.orthographicSize = _mainCamera.orthographicSize;
            _submersionCamera.fieldOfView = _mainCamera.fieldOfView;
            _submersionCamera.aspect = _mainCamera.aspect;

            // Render only the interaction layer
            _submersionCamera.cullingMask = _interactionLayerMask;
            _submersionCamera.targetTexture = _submersionMask;

            _submersionCamera.Render();
        }
    }

}
