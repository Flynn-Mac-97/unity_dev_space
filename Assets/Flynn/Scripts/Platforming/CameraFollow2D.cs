using UnityEngine;

namespace Flynn.Platforming
{
    /// <summary>
    /// Smooth 2D follow camera for the layered platformer. Set the camera rig's tilt
    /// (~30°) and the framing in the Inspector; this only moves the camera to track the
    /// target. X and Y follow with damping; the offset's Z is held so the orthographic
    /// framing stays constant. Optional Y floor stops the view dipping below the ground.
    /// </summary>
    public class CameraFollow2D : MonoBehaviour
    {
        [SerializeField] private Transform _target;
        [Tooltip("Camera position relative to the target. Z sets the (fixed) ortho distance.")]
        [SerializeField] private Vector3 _offset = new Vector3(0f, 1.5f, -10f);
        [Tooltip("Smoothing time in seconds. 0 = snap.")]
        [SerializeField] private float _smoothTime = 0.15f;

        [Header("Optional Y floor")]
        [SerializeField] private bool _clampMinY = false;
        [SerializeField] private float _minY = 0f;

        private Vector3 _vel;

        private void LateUpdate()
        {
            if (_target == null) return;

            Vector3 goal = _target.position + _offset;
            if (_clampMinY) goal.y = Mathf.Max(goal.y, _minY);
            goal.z = _offset.z; // keep ortho distance fixed

            transform.position = Vector3.SmoothDamp(transform.position, goal, ref _vel, _smoothTime);
        }

        public void SetTarget(Transform target) => _target = target;
    }
}
