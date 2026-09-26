using UnityEngine;
using UnityEngine.UI;

/// <summary>Shows/hides on GameManager.OnPhaseChanged (single-shot, no per-frame state polling).
/// While visible, clicking the full-screen background or pressing any key triggers the retry action,
/// after a short delay so the key/click that caused GameOver can't also dismiss it.</summary>
public class GameOverPanelUI : MonoBehaviour
{
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Button backgroundButton;
    [SerializeField, Min(0f)] private float continueDelaySeconds = 2f;

    private float shownAtUnscaledTime = -1f;

    private bool IsVisible => panelRoot != null && panelRoot.activeSelf;
    private bool CanContinueNow => IsVisible && Time.unscaledTime - shownAtUnscaledTime >= continueDelaySeconds;

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
        if (!CanContinueNow)
        {
            return;
        }

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

        if (visible)
        {
            shownAtUnscaledTime = Time.unscaledTime;
        }
    }
}
