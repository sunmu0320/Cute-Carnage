using UnityEngine;
using System.Collections;

public class SimpleShake : MonoBehaviour
{
    Vector3 originalPos;
    Quaternion originalRot;

    void Awake()
    {
        originalPos = transform.localPosition;
        originalRot = transform.localRotation;
    }

    public void Shake(float duration, float strength)
    {
        StopAllCoroutines();
        StartCoroutine(ShakeRoutine(duration, strength));
    }

    IEnumerator ShakeRoutine(float duration, float strength)
    {
        float time = 0f;

        while (time < duration)
        {
            time += Time.deltaTime;

            // Small random offset
            Vector3 offset = Random.insideUnitSphere * strength;

            // Optional: reduce vertical movement for top-down
            offset.y *= 0.3f;

            transform.localPosition = originalPos + offset;

            yield return null;
        }

        // Reset
        transform.localPosition = originalPos;
        transform.localRotation = originalRot;
    }
}