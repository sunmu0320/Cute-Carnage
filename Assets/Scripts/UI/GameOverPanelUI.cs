using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Shows/hides on GameManager.OnPhaseChanged (single-shot, no per-frame state polling).
/// While visible, clicking the full-screen background or pressing any key triggers the retry action,
/// after a short delay so the key/click that caused GameOver can't also dismiss it. The "click to
/// continue" hint only appears once that delay has passed, so it never invites a click that won't work.
/// The title text reflects GameManager.LastGameOverCause (base destroyed vs. player died).
/// Also shown on Victory (final night cleared) with its own title; continuing then returns to the main menu.</summary>
public class GameOverPanelUI : MonoBehaviour
{
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Button backgroundButton;
    [SerializeField] private GameObject continueHint;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private string baseDestroyedTitle = "BASE DESTROYED";
    [SerializeField] private string playerDiedTitle = "YOU DIED";
    [SerializeField] private string victoryTitle = "YOU SURVIVED 7 NIGHTS";
    [SerializeField] private Color victoryTitleColor = new Color(1f, 0.85f, 0.35f);
    [SerializeField] private string mainMenuSceneName = "MainMenu";
    [SerializeField, Min(0f)] private float continueDelaySeconds = 2f;

    private float shownAtUnscaledTime = -1f;
    private bool isVictory;
    private Color defaultTitleColor;

    private bool IsVisible => panelRoot != null && panelRoot.activeSelf;
    private bool CanContinueNow => IsVisible && Time.unscaledTime - shownAtUnscaledTime >= continueDelaySeconds;

    private void Awake()
    {
        if (titleText != null)
        {
            defaultTitleColor = titleText.color;
        }

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
        isVictory = phase == GameManager.GamePhase.Victory;
        bool visible = isVictory || phase == GameManager.GamePhase.GameOver;
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

        titleText.color = isVictory ? victoryTitleColor : defaultTitleColor;
        if (isVictory)
        {
            titleText.text = victoryTitle;
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

        if (isVictory)
        {
            SceneManager.LoadScene(mainMenuSceneName);
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
