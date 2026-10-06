using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>MainMenu scene flow: title ("press any key") -> menu (Continue / New Game / Settings / Quit).
/// Settings (master volume, fullscreen) persist in PlayerPrefs and are applied on boot.
/// Continue loads the single-slot autosave; New Game asks before overwriting an existing save.</summary>
public class MainMenuUI : MonoBehaviour
{
    private const string VolumeKey = "settings.masterVolume";
    private const string FullscreenKey = "settings.fullscreen";

    [SerializeField] private string gameSceneName = "Day";

    [Header("Panels")]
    [SerializeField] private GameObject titlePanel;
    [SerializeField] private GameObject menuPanel;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject overwriteConfirmPanel;

    [Header("Menu")]
    [SerializeField] private Button continueButton;
    [SerializeField] private Button newGameButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button quitButton;

    [Header("Settings")]
    [SerializeField] private Slider volumeSlider;
    [SerializeField] private Toggle fullscreenToggle;
    [SerializeField] private Button settingsBackButton;

    [Header("Overwrite Confirm")]
    [SerializeField] private Button overwriteYesButton;
    [SerializeField] private Button overwriteNoButton;

    private void Awake()
    {
        AudioListener.volume = PlayerPrefs.GetFloat(VolumeKey, 1f);
        Screen.fullScreen = PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) == 1;

        continueButton.interactable = SaveSystem.HasSave;
        continueButton.onClick.AddListener(() => StartGame(loadSave: true));
        newGameButton.onClick.AddListener(() =>
        {
            if (SaveSystem.HasSave) ShowOnly(overwriteConfirmPanel);
            else StartGame(loadSave: false);
        });
        overwriteYesButton.onClick.AddListener(() => StartGame(loadSave: false));
        overwriteNoButton.onClick.AddListener(() => ShowOnly(menuPanel));
        settingsButton.onClick.AddListener(() => ShowOnly(settingsPanel));
        quitButton.onClick.AddListener(Quit);
        settingsBackButton.onClick.AddListener(() => ShowOnly(menuPanel));

        volumeSlider.SetValueWithoutNotify(AudioListener.volume);
        volumeSlider.onValueChanged.AddListener(v => { AudioListener.volume = v; PlayerPrefs.SetFloat(VolumeKey, v); });
        fullscreenToggle.SetIsOnWithoutNotify(Screen.fullScreen);
        fullscreenToggle.onValueChanged.AddListener(on => { Screen.fullScreen = on; PlayerPrefs.SetInt(FullscreenKey, on ? 1 : 0); });

        ShowOnly(titlePanel);
    }

    private void Update()
    {
        // Input check only while the title is up; menu navigation is event-driven via Buttons.
        if (titlePanel.activeSelf && Input.anyKeyDown) ShowOnly(menuPanel);
    }

    private void StartGame(bool loadSave)
    {
        if (!loadSave) SaveSystem.Delete();
        SaveSystem.LoadOnNextStart = loadSave;
        SceneManager.LoadScene(gameSceneName);
    }

    private void ShowOnly(GameObject panel)
    {
        titlePanel.SetActive(panel == titlePanel);
        menuPanel.SetActive(panel == menuPanel);
        settingsPanel.SetActive(panel == settingsPanel);
        overwriteConfirmPanel.SetActive(panel == overwriteConfirmPanel);
    }

    private static void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
