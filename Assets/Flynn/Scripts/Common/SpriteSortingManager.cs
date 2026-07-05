using UnityEngine;
using UnityEngine.Rendering;


using Flynn.Core;
using Flynn.UI.Core;

namespace Flynn.Common
{
    /// <summary>
    /// Drop this on any scene GameObject (e.g. a "SpriteSortingManager" empty GO).
    ///
    /// What it does
    /// ────────────
    /// 1. Configures Camera.main's transparency sort mode to CustomAxis so Unity's
    ///    renderer natively sorts all transparent draw calls (sprites, particles) by
    ///    their projection onto the sort axis — zero per-frame C# cost.
    ///
    /// 2. Exposes the same SpriteSortingConfigSO used by SortableSprite so both
    ///    mechanisms always use the same axis and are never out of sync.
    ///
    /// When to add SortableSprite as well
    /// ────────────────────────────────────
    /// Camera sort mode handles objects on the same sorting layer+order perfectly.
    /// Add SortableSprite to any prefab (character, tree, NPC) where you need
    /// explicit integer sortingOrder control across heterogeneous objects.
    /// </summary>
    public class SpriteSortingManager : MonoBehaviour
    {
        [SerializeField] private SpriteSortingConfigSO _config;

        [Tooltip("When enabled the manager re-applies the camera sort axis every frame.\n" +
                 "Only needed if the camera rotates during play. Leave off for a fixed camera.")]
        [SerializeField] private bool _updateEachFrame = false;

        private Camera _cam;

        private void Awake()
        {
            _cam = Camera.main;
            Apply();
        }

        private void LateUpdate()
        {
            if (_updateEachFrame)
                Apply();
        }

        private void Apply()
        {
            if (_config == null || _cam == null) return;

            Vector3 axis = _config.SortAxisNormalized;

            // Unity-native: sorts all transparent renderers on the same layer by depth.
            _cam.transparencySortMode = TransparencySortMode.CustomAxis;
            _cam.transparencySortAxis = axis;
        }

    #if UNITY_EDITOR
        private void OnValidate()
        {
            // Refresh in Edit mode so the Gizmo reflects current settings immediately.
            _cam = Camera.main;
            if (_config != null && _cam != null)
                Apply();
        }

        private void OnDrawGizmosSelected()
        {
            if (_config == null) return;

            Vector3 axis = _config.SortAxisNormalized;
            Vector3 origin = transform.position + Vector3.up * 0.5f;

            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.9f);
            Gizmos.DrawRay(origin, axis * 3f);

            // Draw a small disc perpendicular to axis (shows the "ground plane" of depth)
            Vector3 perp = Vector3.Cross(axis, Vector3.up);
            if (perp.sqrMagnitude < 0.001f) perp = Vector3.right;
            perp.Normalize();
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.3f);
            for (int i = 0; i < 16; i++)
            {
                float a0 = i * Mathf.PI * 2f / 16f;
                float a1 = (i + 1) * Mathf.PI * 2f / 16f;
                Vector3 v0 = origin + Quaternion.AngleAxis(a0 * Mathf.Rad2Deg, axis) * perp;
                Vector3 v1 = origin + Quaternion.AngleAxis(a1 * Mathf.Rad2Deg, axis) * perp;
                Gizmos.DrawLine(v0, v1);
            }
        }
    #endif
    }

}
