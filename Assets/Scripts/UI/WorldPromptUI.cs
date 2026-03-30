using TMPro;
using UnityEngine;

public class WorldPromptUI : MonoBehaviour
{
    [Header("World Prompt References")]
    [SerializeField] private Canvas worldCanvas;
    [SerializeField] private TextMeshProUGUI promptText;

    private void Awake()
    {
        // Prompt should start hidden every time.
        Hide();
    }

    public void Show(string message)
    {
        if (worldCanvas != null)
            worldCanvas.enabled = true;

        if (promptText != null)
            promptText.text = string.IsNullOrWhiteSpace(message) ? string.Empty : message;

        gameObject.SetActive(true);
    }

    public void Hide()
    {
        if (promptText != null)
            promptText.text = string.Empty;

        if (worldCanvas != null)
            worldCanvas.enabled = false;
    }
}
