using UnityEngine;

/// Keeps the visual child facing the camera (Y-axis only — stable in isometric view).
public class PlayerBillboard : MonoBehaviour
{
    private Transform _cam;

    private void Start()
    {
        _cam = Camera.main.transform;
    }

    private void LateUpdate()
    {
        transform.rotation = Quaternion.Euler(0f, _cam.eulerAngles.y, 0f);
    }
}
