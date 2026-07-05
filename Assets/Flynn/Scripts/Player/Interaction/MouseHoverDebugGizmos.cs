#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;


using Flynn.Core;
using Flynn.UI.Core;

namespace Flynn.Player.Interaction
{
    /// <summary>
    /// Editor-only debug overlay for the mouse-hover system.
    /// In Play mode it reads the cached result from <see cref="MousePointer"/> (the single
    /// source of truth) instead of running its own raycast, so the gizmo can never disagree
    /// with what gameplay/UI actually see. In edit mode it falls back to a local 2D cast.
    ///
    /// The marker + label are pulled toward the camera along its view direction and drawn with
    /// <c>Handles.zTest = Always</c>, so they render in front of scene sprites instead of being
    /// occluded by them (the scene is 2D on the XY plane with an orthographic camera pitched 30°,
    /// so "toward camera" is -cam.forward, NOT world -z).
    ///
    /// Enable Gizmos in the Game/Scene view to see it.
    /// </summary>
    public class MouseHoverDebugGizmos : MonoBehaviour
    {
        [Header("Label Style")]
        [SerializeField] private Color _labelColor = new Color(1f, 1f, 0.3f, 1f);
        [SerializeField] private int _fontSize = 14;

        [Header("Marker")]
        [SerializeField] private Color _hitColor = new Color(1f, 0.4f, 0f, 0.8f);
        [SerializeField] private Color _lineColor = new Color(1f, 1f, 0f, 0.4f);
        [SerializeField] private float _markerRadius = 0.12f;

        [Header("Behaviour")]
        [Tooltip("Distance to pull the marker/label toward the camera so it draws in front of sprites sitting on the same plane.")]
        [SerializeField] private float _cameraPull = 0.5f;
        [Tooltip("Also draw a marker when the cursor hits no collider (at the ground/aim point).")]
        [SerializeField] private bool _drawWhenNoHit = false;

        private void OnDrawGizmos()
        {
            if (!Resolve(out Camera cam, out Vector3 point, out GameObject obj, out bool hasHit))
                return;

            if (!hasHit && !_drawWhenNoHit) return;

            // Pull toward the camera along its view direction. Correct for a tilted orthographic
            // camera — world -z is NOT "toward camera" once the camera is pitched.
            Vector3 toCam     = -cam.transform.forward * _cameraPull;
            Vector3 drawPoint = point + toCam;

            CompareFunction prevZTest = Handles.zTest;
            Handles.zTest = CompareFunction.Always; // beat sprite depth so the label/marker stay visible

            Gizmos.color = hasHit ? _hitColor : _lineColor;
            Gizmos.DrawWireSphere(drawPoint, _markerRadius);
            Gizmos.DrawSphere(drawPoint, _markerRadius * 0.3f);

            Gizmos.color = _lineColor;
            Gizmos.DrawLine(transform.position + toCam, drawPoint);

            Handles.zTest = prevZTest;
            // NOTE: the label is drawn in OnGUI, NOT here. A GUI/Handles label issued from OnDrawGizmos
            // rides the gizmo render pass, which the editor occlusion/distance-culls relative to this
            // component's transform — so the text vanished as the player moved away. OnGUI is a clean
            // per-frame screen-space pass with no depth test and no gizmo culling.
        }

        // Screen-space label — its own pass so it never inherits gizmo depth/culling.
        private void OnGUI()
        {
            if (!Resolve(out Camera cam, out Vector3 point, out GameObject obj, out bool hasHit))
                return;
            if (!hasHit && !_drawWhenNoHit) return;

            Vector3 worldPos = point - cam.transform.forward * _cameraPull + cam.transform.up * 0.3f;
            string label = obj != null ? obj.name : "(no object)";
            DrawScreenLabel(cam, worldPos, label);
        }

        /// <summary>
        /// Resolves the hover point/object. Prefers the shared <see cref="MousePointer"/> cache in
        /// Play mode; falls back to a local 2D cast (Game-view cursor) in edit mode.
        /// </summary>
        private bool Resolve(out Camera cam, out Vector3 point, out GameObject obj, out bool hasHit)
        {
            point  = default;
            obj    = null;
            hasHit = false;

            MousePointer mp = MousePointer.Instance;
            if (Application.isPlaying && mp != null)
            {
                cam    = Camera.main;
                point  = mp.WorldPoint;
                obj    = mp.HoverObject;
                hasHit = mp.HasHit;
                return cam != null;
            }

            // Edit-mode fallback.
            cam = Camera.main;
            if (cam == null) return false;

            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            RaycastHit2D hit2D = Physics2D.GetRayIntersection(ray, Mathf.Infinity);
            if (hit2D.collider != null)
            {
                hasHit = true;
                point  = hit2D.point;
                obj    = hit2D.collider.gameObject;
            }
            else
            {
                // Project to the 2D plane (z = 0) for a usable no-hit aim marker.
                point = ZPlanePoint(ray, 0f);
            }
            return true;
        }

        private static Vector3 ZPlanePoint(Ray ray, float z)
        {
            float denom = ray.direction.z;
            if (Mathf.Abs(denom) > 1e-5f)
                return ray.origin + ray.direction * ((z - ray.origin.z) / denom);
            return ray.GetPoint(100f);
        }

        private void DrawScreenLabel(Camera cam, Vector3 worldPos, string text)
        {
            Vector3 sp = cam.WorldToScreenPoint(worldPos);
            if (sp.z <= 0f) return; // behind the camera

            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = _fontSize,
                fontStyle = FontStyle.Bold,
                normal = { textColor = _labelColor },
                alignment = TextAnchor.UpperCenter
            };

            // WorldToScreenPoint: origin bottom-left. GUI: origin top-left → flip Y.
            var content = new GUIContent(text);
            Vector2 size = style.CalcSize(content);
            var rect = new Rect(sp.x - size.x * 0.5f, cam.pixelHeight - sp.y - size.y, size.x, size.y);

            GUI.Label(rect, content, style);
        }
    }
    #endif

}
