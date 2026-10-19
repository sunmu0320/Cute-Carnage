using UnityEngine;

/// <summary>Trigger around the home base: tells PhaseLighting when the player enters/leaves so the night
/// fog thins there. Event-driven (trigger enter/exit), same pattern as HouseRoofVisibility. Keep it on the
/// Ignore Raycast layer so interaction/targeting queries never hit it.</summary>
[RequireComponent(typeof(Collider))]
public class HomeFogZone : MonoBehaviour
{
    private PhaseLighting phaseLighting;

    private void Awake()
    {
        phaseLighting = FindFirstObjectByType<PhaseLighting>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (phaseLighting != null && other.CompareTag("Player"))
            phaseLighting.SetPlayerInHomeZone(true);
    }

    private void OnTriggerExit(Collider other)
    {
        if (phaseLighting != null && other.CompareTag("Player"))
            phaseLighting.SetPlayerInHomeZone(false);
    }
}
