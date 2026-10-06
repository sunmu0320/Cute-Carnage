using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>In-game pause menu on Esc: Resume / Settings / Main Menu / Quit Game (the last two confirm first,
/// since progress since the Day-start autosave is lost). Pausing sets Time.timeScale = 0 and disables
/// interaction/eating. Runs before the structure/repair panels so that, when one of them is open, Esc only
/// closes that panel (their own Update) instead of also opening this menu. Ignored during GameOver/Victory.</summary>
[DefaultExecutionOrder(-100)]
public class PauseMenuUI : MonoBehaviour
{
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Header("Panels")]
    [SerializeField] private GameObject rootPanel;      // dim background + everything below
    [SerializeField] private GameObject menuPanel;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject confirmPanel;
    [SerializeField] private TMPro.TextMeshProUGUI confirmHeader;

    [Header("Buttons")]
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button mainMenuButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private Button settingsBackButton;
    [SerializeField] private Button confirmYesButton;
    [SerializeField] private Button confirmNoButton;

    private bool confirmQuitsApp;

    public bool IsPaused => rootPanel.activeSelf;

    private void Awake()
    {
        resumeButton.onClick.AddListener(Resume);
        settingsButton.onClick.AddListener(() => ShowOnly(settingsPanel));
        settingsBackButton.onClick.AddListener(() => ShowOnly(menuPanel));
        mainMenuButton.onClick.AddListener(() => AskConfirm(quitApp: false));
        quitButton.onClick.AddListener(() => AskConfirm(quitApp: true));
        confirmNoButton.onClick.AddListener(() => ShowOnly(menuPanel));
        confirmYesButton.onClick.AddListener(Confirm);
        rootPanel.SetActive(false);
    }

    private void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Escape)) return;

        if (IsPaused)
        {
            if (menuPanel.activeSelf) Resume();
            else ShowOnly(menuPanel);
            return;
        }

        GameManager gm = GameManager.Instance;
        if (gm != null && (gm.IsGameOver || gm.IsVictory)) return;
        if (AnyGameplayPanelOpen()) return; // that panel closes itself on this Esc

        Pause();
    }

    private void OnDestroy()
    {
        Time.timeScale = 1f;
    }

    private static bool AnyGameplayPanelOpen()
    {
        foreach (StructureActionPanelUI p in FindObjectsByType<StructureActionPanelUI>(FindObjectsSortMode.None))
            if (p.IsOpen) return true;
        foreach (NightRepairPanelUI p in FindObjectsByType<NightRepairPanelUI>(FindObjectsSortMode.None))
            if (p.IsOpen) return true;
        return false;
    }

    private void Pause()
    {
        Time.timeScale = 0f;
        SetPlayerActionsEnabled(false);
        rootPanel.SetActive(true);
        ShowOnly(menuPanel);
    }

    private void Resume()
    {
        rootPanel.SetActive(false);
        SetPlayerActionsEnabled(true);
        Time.timeScale = 1f;
    }

    private void AskConfirm(bool quitApp)
    {
        confirmQuitsApp = quitApp;
        confirmHeader.text = quitApp ? "QUIT GAME?" : "RETURN TO MAIN MENU?";
        ShowOnly(confirmPanel);
    }

    private void Confirm()
    {
        Time.timeScale = 1f;
        if (!confirmQuitsApp)
        {
            SceneManager.LoadScene(mainMenuSceneName);
            return;
        }

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void ShowOnly(GameObject panel)
    {
        menuPanel.SetActive(panel == menuPanel);
        settingsPanel.SetActive(panel == settingsPanel);
        confirmPanel.SetActive(panel == confirmPanel);
    }

    // Movement/combat/hunger are time-driven and stop with timeScale 0; key-press actions are not.
    private static void SetPlayerActionsEnabled(bool enabled)
    {
        PlayerInteractor interactor = FindFirstObjectByType<PlayerInteractor>();
        if (interactor != null) interactor.enabled = enabled;
        PlayerConsume consume = FindFirstObjectByType<PlayerConsume>();
        if (consume != null) consume.enabled = enabled;
    }
}
