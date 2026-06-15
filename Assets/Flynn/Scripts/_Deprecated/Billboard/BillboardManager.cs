using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central manager that drives billboard rotation for all registered
/// IBillboardTarget objects from a single LateUpdate loop.
///
/// Replace per-object Billboard.cs Update overhead with a single pass.
/// Existing Billboard.cs components continue working independently for
/// backward compatibility.
/// </summary>
[DefaultExecutionOrder(-100)]
public class BillboardManager : MonoBehaviour
{
    private readonly List<IBillboardTarget> _targets = new();
    private Camera _mainCamera;
    private bool _cameraCached;

    /// <summary>Register a target to receive automatic billboarding.</summary>
    public void Register(IBillboardTarget target)
    {
        if (target == null) return;
        if (!_targets.Contains(target))
            _targets.Add(target);
    }

    /// <summary>Unregister a target so it no longer receives billboarding.</summary>
    public void Unregister(IBillboardTarget target)
    {
        if (target == null) return;
        _targets.Remove(target);
    }

    private void CacheCamera()
    {
        if (!_cameraCached)
        {
            _mainCamera = Camera.main;
            _cameraCached = true;
        }
    }

    private void LateUpdate()
    {
        CacheCamera();
        if (_mainCamera == null) return;

        Vector3 cameraPos = _mainCamera.transform.position;

        for (int i = _targets.Count - 1; i >= 0; i--)
        {
            IBillboardTarget target = _targets[i];
            if (target?.BillboardTransform == null)
            {
                _targets.RemoveAt(i);
                continue;
            }

            Transform t = target.BillboardTransform;
            Vector3 dirToCamera = cameraPos - t.position;

            switch (target.Mode)
            {
                case BillboardMode.Full:
                    t.rotation = Quaternion.LookRotation(dirToCamera);
                    break;

                case BillboardMode.YOnly:
                    Vector3 flatDir = new Vector3(dirToCamera.x, 0f, dirToCamera.z);
                    if (flatDir.sqrMagnitude > 0.001f)
                        t.rotation = Quaternion.LookRotation(flatDir);
                    break;

                case BillboardMode.PitchOnly:
                    float horizontalDist = new Vector2(dirToCamera.x, dirToCamera.z).magnitude;
                    float pitchAngle = Mathf.Atan2(dirToCamera.y, horizontalDist) * Mathf.Rad2Deg;
                    t.rotation = Quaternion.Euler(pitchAngle, t.eulerAngles.y, 0f);
                    break;

                case BillboardMode.SpriteBillboard:
                    // Preserve the initial X tilt (e.g. 90° for upright sprites)
                    // and only rotate Y to face the camera on the horizontal plane.
                    Vector3 spriteFlatDir = new Vector3(dirToCamera.x, 0f, dirToCamera.z);
                    if (spriteFlatDir.sqrMagnitude > 0.001f)
                    {
                        float yAngle = Quaternion.LookRotation(spriteFlatDir).eulerAngles.y;
                        t.rotation = Quaternion.Euler(t.localEulerAngles.x, yAngle, 0f);
                    }
                    break;
            }
        }
    }
}
