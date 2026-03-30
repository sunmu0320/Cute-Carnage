using UnityEngine;

public class ExamplePromptInteractable : MonoBehaviour
{
    [Header("Prompt")]
    [SerializeField] private WorldPromptUI promptUI;
    [SerializeField] private string promptMessage = "E - Gather";
    [SerializeField] private string playerTag = "Player";

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag))
            return;

        if (promptUI == null)
            return;

        promptUI.Show(promptMessage);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(playerTag))
            return;

        if (promptUI == null)
            return;

        promptUI.Hide();
    }
}
