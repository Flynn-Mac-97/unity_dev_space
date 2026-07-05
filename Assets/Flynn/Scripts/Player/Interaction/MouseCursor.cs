using UnityEngine;


using Flynn.UI.Core;

namespace Flynn.Player.Interaction
{
    /// <summary>
    /// Projects the cursor sprite onto the gameplay plane and draws a tapered,
    /// gradient aim line from the player to the cursor. The line pulses gently
    /// for a living, solarpunk feel.
    /// </summary>
    public class MouseCursor : MonoBehaviour
    {
        public GameObject cursor;
        public LineRenderer lineRenderer;
        public Transform playerTransform;
        public bool active = true;

        [Header("Pulse")]
        [Tooltip("How fast the aim line pulses.")]
        [SerializeField] private float _pulseSpeed = 2.5f;
        [Tooltip("Alpha oscillation amplitude (0–0.5).")]
        [SerializeField] private float _pulseAmplitude = 0.15f;

        private static readonly Plane _playPlane = new Plane(Vector3.forward, Vector3.zero);

        void Awake()
        {
            if (lineRenderer != null)
            {
                lineRenderer.useWorldSpace = true;
                lineRenderer.positionCount = 2;
            }
        }

        void Update()
        {
            var mp = MousePointer.Instance;
            if (mp == null || cursor == null) return;

            Ray ray = mp.WorldRay;
            if (!_playPlane.Raycast(ray, out float dist)) return;
            Vector3 mousePosition = ray.GetPoint(dist);
            cursor.transform.position = mousePosition;

            if (active && lineRenderer != null && playerTransform != null)
            {
                lineRenderer.enabled = true;
                lineRenderer.SetPosition(0, playerTransform.position);
                lineRenderer.SetPosition(1, mousePosition);
                ApplyPulse();
            }
            else if (lineRenderer != null)
            {
                lineRenderer.enabled = false;
            }
        }

        private void ApplyPulse()
        {
            float t = (Mathf.Sin(Time.time * _pulseSpeed) + 1f) * 0.5f; // 0–1
            float alpha = 0.55f + t * _pulseAmplitude;

            var g = lineRenderer.colorGradient;
            var keys = g.alphaKeys;
            keys[0].alpha = alpha;
            keys[1].alpha = alpha * 0.4f;
            keys[2].alpha = alpha * 0.12f;
            g.alphaKeys = keys;
            lineRenderer.colorGradient = g;
        }
    }

}
