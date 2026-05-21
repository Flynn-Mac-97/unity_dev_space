using UnityEngine;

public class Billboard : MonoBehaviour
{
    private Camera _mainCamera;

    private void Start()
    {
        _mainCamera = Camera.main;
    }

    private void LateUpdate()
    {
        if (_mainCamera == null) return;

        Vector3 directionToCamera = _mainCamera.transform.position - transform.position;

        float horizontalDist = new Vector2(directionToCamera.x, directionToCamera.z).magnitude;
        float pitchAngle = Mathf.Atan2(directionToCamera.y, horizontalDist) * Mathf.Rad2Deg;

        transform.rotation = Quaternion.Euler(pitchAngle, transform.eulerAngles.y, 0f);
    }
}
