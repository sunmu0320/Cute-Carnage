using UnityEngine;
using UnityEngine.UI;

/// <summary>Shared settings controls (master volume, fullscreen), used by both the main menu and the
/// in-game pause menu. Values persist in PlayerPrefs; ApplySaved() pushes them to the engine on boot.</summary>
public class SettingsPanelUI : MonoBehaviour
{
    private const string VolumeKey = "settings.masterVolume";
    private const string FullscreenKey = "settings.fullscreen";

    [SerializeField] private Slider volumeSlider;
    [SerializeField] private Toggle fullscreenToggle;

    public static void ApplySaved()
    {
        AudioListener.volume = PlayerPrefs.GetFloat(VolumeKey, 1f);
        Screen.fullScreen = PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) == 1;
    }

    private void Awake()
    {
        volumeSlider.onValueChanged.AddListener(v => { AudioListener.volume = v; PlayerPrefs.SetFloat(VolumeKey, v); });
        fullscreenToggle.onValueChanged.AddListener(on => { Screen.fullScreen = on; PlayerPrefs.SetInt(FullscreenKey, on ? 1 : 0); });
    }

    private void OnEnable()
    {
        volumeSlider.SetValueWithoutNotify(AudioListener.volume);
        fullscreenToggle.SetIsOnWithoutNotify(Screen.fullScreen);
    }
}
