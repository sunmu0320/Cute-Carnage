using UnityEngine;
using UnityEngine.UI;

/// <summary>Shows/hides on GameManager.OnPhaseChanged (single-shot, no per-frame state polling).
/// While visible, clicking the full-screen background or pressing any key triggers the retry action.</summary>
public class GameOverPanelUI : MonoBehaviour
{
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Button backgroundButton;

    private bool IsVisible => panelRoot != null && panelRoot.activeSelf;

    private void Awake()
    {
        if (backgroundButton != null)
        {
            backgroundButton.onClick.AddListener(HandleContinueClicked);
        }

        SetVisible(false);
    }

    private void Start()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnPhaseChanged += HandlePhaseChanged;
        }
        else
        {
            Debug.LogWarning("[GameOverPanelUI] GameManager.Instance not found at Start; GameOver screen will not show.", this);
        }
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnPhaseChanged -= HandlePhaseChanged;
        }

        if (backgroundButton != null)
        {
            backgroundButton.onClick.RemoveListener(HandleContinueClicked);
        }
    }

    private void Update()
    {
        if (!IsVisible)
        {
            return;
        }

        if (Input.anyKeyDown)
        {
            HandleContinueClicked();
        }
    }

    private void HandlePhaseChanged(GameManager.GamePhase phase)
    {
        SetVisible(phase == GameManager.GamePhase.GameOver);
    }

    private void HandleContinueClicked()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.RetryCurrentDay();
        }
    }

    private void SetVisible(bool visible)
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(visible);
        }
    }
}
