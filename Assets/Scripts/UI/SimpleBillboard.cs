using UnityEngine;

public class SimpleBillboard : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;

    private void LateUpdate()
    {
        Camera cam = targetCamera != null ? targetCamera : Camera.main;
        if (cam == null)
            return;

        Vector3 directionToCamera = transform.position - cam.transform.position;
        if (directionToCamera.sqrMagnitude <= 0.0001f)
            return;

        transform.rotation = Quaternion.LookRotation(directionToCamera);
    }
}
