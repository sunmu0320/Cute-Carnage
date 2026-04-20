using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [SerializeField]
    private string daySceneName = "Day";

    [SerializeField]
    private string nightSceneName = "Night";

    [SerializeField]
    private float nightSurvivalSeconds = 60f;

    [SerializeField]
    private KeyCode returnToDayKey = KeyCode.N;

    private DayTimeManager boundDayTimeManager;
    private float nightTimerRemaining;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        SceneManager.sceneLoaded -= OnSceneLoaded;
        UnbindDayManager();
    }

    private void Update()
    {
        Scene active = SceneManager.GetActiveScene();
        if (active.name != nightSceneName)
        {
            return;
        }

        nightTimerRemaining -= Time.deltaTime;
        if (nightTimerRemaining <= 0f || Input.GetKeyDown(returnToDayKey))
        {
            LoadDayScene();
        }
    }

    public void LoadDayScene()
    {
        SceneManager.LoadScene(daySceneName, LoadSceneMode.Single);
    }

    public void LoadNightScene()
    {
        SceneManager.LoadScene(nightSceneName, LoadSceneMode.Single);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == daySceneName)
        {
            BindDayManager();
        }
        else if (scene.name == nightSceneName)
        {
            UnbindDayManager();
            nightTimerRemaining = nightSurvivalSeconds;
        }
    }

    private void BindDayManager()
    {
        DayTimeManager manager = FindFirstObjectByType<DayTimeManager>();
        if (manager == null)
        {
            return;
        }

        if (boundDayTimeManager == manager)
        {
            return;
        }

        UnbindDayManager();
        boundDayTimeManager = manager;
        boundDayTimeManager.OnDayEnded -= HandleDayEnded;
        boundDayTimeManager.OnDayEnded += HandleDayEnded;
    }

    private void UnbindDayManager()
    {
        if (boundDayTimeManager == null)
        {
            return;
        }

        boundDayTimeManager.OnDayEnded -= HandleDayEnded;
        boundDayTimeManager = null;
    }

    private void HandleDayEnded()
    {
        if (boundDayTimeManager != null)
        {
            boundDayTimeManager.OnDayEnded -= HandleDayEnded;
            boundDayTimeManager = null;
        }

        LoadNightScene();
    }
}
