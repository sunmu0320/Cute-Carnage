using UnityEngine;
using UnityEngine.UI;

/// <summary>Shared settings controls (master volume, fullscreen, screen shake), used by both the main menu and the
/// in-game pause menu. Values persist in PlayerPrefs; ApplySaved() pushes them to the engine on boot.</summary>
public class SettingsPanelUI : MonoBehaviour
{
    private const string VolumeKey = "settings.masterVolume";
    private const string FullscreenKey = "settings.fullscreen";
    private const string ScreenShakeKey = "settings.screenShake";

    [SerializeField] private Slider volumeSlider;
    [SerializeField] private Toggle fullscreenToggle;
    [SerializeField] private Toggle screenShakeToggle;

    public static bool SavedScreenShake => PlayerPrefs.GetInt(ScreenShakeKey, 1) == 1;

    public static void ApplySaved()
    {
        AudioListener.volume = PlayerPrefs.GetFloat(VolumeKey, 1f);
        Screen.fullScreen = PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) == 1;
        CameraFollow.ShakeEnabled = SavedScreenShake;
    }

    private void Awake()
    {
        volumeSlider.onValueChanged.AddListener(v => { AudioListener.volume = v; PlayerPrefs.SetFloat(VolumeKey, v); });
        fullscreenToggle.onValueChanged.AddListener(on => { Screen.fullScreen = on; PlayerPrefs.SetInt(FullscreenKey, on ? 1 : 0); });
        screenShakeToggle.onValueChanged.AddListener(on => { CameraFollow.ShakeEnabled = on; PlayerPrefs.SetInt(ScreenShakeKey, on ? 1 : 0); });
    }

    private void OnEnable()
    {
        volumeSlider.SetValueWithoutNotify(AudioListener.volume);
        fullscreenToggle.SetIsOnWithoutNotify(Screen.fullScreen);
        screenShakeToggle.SetIsOnWithoutNotify(CameraFollow.ShakeEnabled);
    }
}
