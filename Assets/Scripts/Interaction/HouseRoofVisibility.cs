using UnityEngine;

public class HouseRoofVisibility : MonoBehaviour
{
    [Header("Roof Settings")]
    [Tooltip("The GameObject representing the roof of the house.")]
    [SerializeField] private GameObject roofObject;

    [Header("State")]
    [SerializeField] private int playersInsideCount = 0;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playersInsideCount++;
            UpdateRoofVisibility();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playersInsideCount--;
            if (playersInsideCount < 0)
            {
                playersInsideCount = 0;
            }
            UpdateRoofVisibility();
        }
    }

    private void UpdateRoofVisibility()
    {
        if (roofObject != null)
        {
            // Hide the roof if there are players inside, otherwise show it
            roofObject.SetActive(playersInsideCount == 0);
        }
    }

    // Helper to validate and auto-find roof object in parent hierarchy in Editor
    private void OnValidate()
    {
        if (roofObject == null)
        {
            Transform parent = transform.parent;
            if (parent != null)
            {
                Transform roofTransform = parent.Find("Roof1");
                if (roofTransform != null)
                {
                    roofObject = roofTransform.gameObject;
                }
            }
        }
    }
}
