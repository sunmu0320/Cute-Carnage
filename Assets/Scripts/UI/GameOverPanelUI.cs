using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Shows/hides on GameManager.OnPhaseChanged (single-shot, no per-frame state polling).
/// While visible, clicking the full-screen background or pressing any key triggers the retry action,
/// after a short delay so the key/click that caused GameOver can't also dismiss it. The "click to
/// continue" hint only appears once that delay has passed, so it never invites a click that won't work.
/// The title text reflects GameManager.LastGameOverCause (base destroyed vs. player died).</summary>
public class GameOverPanelUI : MonoBehaviour
{
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Button backgroundButton;
    [SerializeField] private GameObject continueHint;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private string baseDestroyedTitle = "BASE DESTROYED";
    [SerializeField] private string playerDiedTitle = "YOU DIED";
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

        if (continueHint != null && !continueHint.activeSelf && CanContinueNow)
        {
            continueHint.SetActive(true);
        }

        if (Input.anyKeyDown)
        {
            HandleContinueClicked();
        }
    }

    private void HandlePhaseChanged(GameManager.GamePhase phase)
    {
        bool visible = phase == GameManager.GamePhase.GameOver;
        if (visible)
        {
            ApplyTitleForCause();
        }

        SetVisible(visible);
    }

    private void ApplyTitleForCause()
    {
        if (titleText == null || GameManager.Instance == null)
        {
            return;
        }

        titleText.text = GameManager.Instance.LastGameOverCause == GameManager.GameOverCause.PlayerDied
            ? playerDiedTitle
            : baseDestroyedTitle;
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

        if (continueHint != null)
        {
            continueHint.SetActive(false);
        }

        if (visible)
        {
            shownAtUnscaledTime = Time.unscaledTime;
        }
    }
}
