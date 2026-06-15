using UnityEngine;

/// <summary>
/// Applies a momentum lean to a billboarded 2D sprite visual by adding X/Z euler
/// rotation after the Billboard component sets the camera-facing rotation.
/// Y rotation is never touched — Billboard owns that. Runs after Billboard via
/// DefaultExecutionOrder. Position is offset so the lean pivots from the feet.
/// </summary>
[DefaultExecutionOrder(100)]
public class VelocityLeanDriver : MonoBehaviour
{
    [SerializeField] private SolarpunkCharacterController _controller;
    [SerializeField] private float _maxLeanDeg = 12f;
    [SerializeField] private float _leanSpeed = 8f;
    [Tooltip("Vertical distance from transform origin down to feet for pivot offset.")]
    [SerializeField] private float _footOffsetY = 0.455f;

    private float _currentLeanX;
    private float _currentLeanZ;
    private float _targetLeanX;
    private float _targetLeanZ;
    private Vector3 _baseLocalPos;

    private void Awake()
    {
        _baseLocalPos = transform.localPosition;
    }

    private void LateUpdate()
    {
        if (_controller == null) return;

        // Use MoveInput direction + NormalizedSpeed so lean is responsive
        // even when Rigidbody velocity gets zeroed by the controller between frames
        var input = _controller.MoveInput;
        var speed = _controller.NormalizedSpeed; // 0-1

        if (speed > 0.05f && input.sqrMagnitude > 0.001f)
        {
            // Normalize the XZ input for direction
            var dir = new Vector3(input.x, 0f, input.z).normalized;
            // Moving +Z → lean forward (positive X rotation)
            // Moving +X → lean right (negative Z rotation)
            _targetLeanX = dir.z * speed * _maxLeanDeg;
            _targetLeanZ = -dir.x * speed * _maxLeanDeg;
        }
        else
        {
            _targetLeanX = 0f;
            _targetLeanZ = 0f;
        }

        _currentLeanX = Mathf.Lerp(_currentLeanX, _targetLeanX, _leanSpeed * Time.deltaTime);
        _currentLeanZ = Mathf.Lerp(_currentLeanZ, _targetLeanZ, _leanSpeed * Time.deltaTime);

        // Add lean to Billboard's rotation (X and Z only, Y preserved)
        var euler = transform.eulerAngles;
        euler.x += _currentLeanX;
        euler.z += _currentLeanZ;
        transform.eulerAngles = euler;

        // Offset position so lean pivots from feet instead of center
        var leanRot = Quaternion.Euler(_currentLeanX, 0f, _currentLeanZ);
        Vector3 feetOffset = new Vector3(0f, -_footOffsetY, 0f);
        transform.localPosition = _baseLocalPos + leanRot * feetOffset - feetOffset;
    }
}
