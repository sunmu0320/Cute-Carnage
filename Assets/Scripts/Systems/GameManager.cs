using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public enum GamePhase
    {
        Unknown,
        Day,
        Night,
        GameOver
    }

    public static GameManager Instance { get; private set; }

    [SerializeField]
    private KeyCode returnToDayKey = KeyCode.N;

    [Header("Night Content")]
    [Tooltip("Container holding night-only content (currently: the zombie spawner). Toggled active/inactive on phase transitions.")]
    [SerializeField]
    private GameObject nightRoot;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [Header("Regression Debug Tools")]
    [SerializeField]
    private KeyCode forceNightKey = KeyCode.M;

    [SerializeField]
    private KeyCode debugDamageBaseCoreKey = KeyCode.J;

    [SerializeField]
    private KeyCode debugDestroyBaseCoreKey = KeyCode.L;

    [SerializeField]
    private float debugBaseCoreDamageAmount = 25f;

    private BaseCore cachedBaseCore;
#endif

    private const int DefaultBaseLevel = 1;
    private const string DefaultBaseUpgradeId = "base_lv1";

    private DayTimeManager boundDayTimeManager;
    private ResourceManager cachedResourceManager;
    private SimpleZombieSpawner cachedNightSpawner;
    private BaseCore boundBaseCore;
    private GamePhase currentPhase = GamePhase.Unknown;
    private RunRuntimeState currentRunState;
    private bool hasInitializedRunState;
    private int runtimeInstanceId;
    private int currentDay = 1;

    public event Action<GamePhase> OnPhaseChanged;

    public RunRuntimeState CurrentRunState => currentRunState;
    public BaseRuntimeState CurrentBaseState => currentRunState?.baseState;
    public int CurrentDay => currentDay;
    public bool IsDay => currentPhase == GamePhase.Day;
    public bool IsGameOver => currentPhase == GamePhase.GameOver;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private void LogTransition(string message)
    {
        Debug.Log($"[GameManager] {message}", this);
    }
#else
    private void LogTransition(string message)
    {
    }
#endif

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.Log(
                $"[GameManager] Duplicate instance detected. Destroying '{name}' (id={GetInstanceID()}) and keeping '{Instance.name}' (id={Instance.GetInstanceID()}).",
                this);
            Destroy(gameObject);
            return;
        }

        Instance = this;
        runtimeInstanceId = GetInstanceID();
        Debug.Log($"[GameManager] Runtime instance created. id={runtimeInstanceId}, object='{name}'.", this);

        currentPhase = GamePhase.Day;
        if (nightRoot != null)
        {
            nightRoot.SetActive(false);
        }

        ResolveDayTimeManager();
        ResolveBaseCore();
    }

    private void Start()
    {
        AfterEnterDayScene();
        OnPhaseChanged?.Invoke(GamePhase.Day);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        if (boundDayTimeManager != null)
        {
            boundDayTimeManager.OnDayEnded -= HandleDayEnded;
            boundDayTimeManager = null;
        }

        if (boundBaseCore != null)
        {
            boundBaseCore.OnBaseDestroyed -= HandleBaseDestroyed;
            boundBaseCore = null;
        }
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private void Update()
    {
        HandleDebugPhaseInput();
        HandleDebugBaseCoreDamageInput();
    }

    private void HandleDebugPhaseInput()
    {
        if (Input.GetKeyDown(returnToDayKey))
        {
            LogTransition($"Debug Night->Day: '{returnToDayKey}' pressed. Calling TransitionToDay().");
            TransitionToDay();
        }

        if (Input.GetKeyDown(forceNightKey))
        {
            Debug.Log($"[TEMP-DEBUG][GameManager] Before TransitionToNight(): boundDayTimeManager id={(boundDayTimeManager != null ? boundDayTimeManager.GetInstanceID().ToString() : "null")}");
            LogTransition($"Debug Day->Night: '{forceNightKey}' pressed. Calling TransitionToNight().");
            TransitionToNight();
        }
    }

    private void HandleDebugBaseCoreDamageInput()
    {
        if (Input.GetKeyDown(debugDamageBaseCoreKey))
        {
            ApplyDebugBaseCoreDamage(debugBaseCoreDamageAmount);
        }

        if (Input.GetKeyDown(debugDestroyBaseCoreKey))
        {
            ApplyDebugBaseCoreDamage(float.MaxValue);
        }
    }

    private void ApplyDebugBaseCoreDamage(float amount)
    {
        BaseCore core = GetOrFindBaseCore();
        if (core == null)
        {
            Debug.LogWarning("[GameManager] Debug damage requested but no BaseCore found in active scene.", this);
            return;
        }

        float beforeHp = core.CurrentHp;
        core.TakeDamage(amount);
        LogTransition(
            $"Debug BaseCore damage applied: amount={amount:0.##} hp {beforeHp:0.##} -> {core.CurrentHp:0.##}/{core.MaxHp:0.##} destroyed={core.IsDestroyed}.");
    }

    private BaseCore GetOrFindBaseCore()
    {
        if (cachedBaseCore != null)
        {
            return cachedBaseCore;
        }

        cachedBaseCore = FindFirstObjectByType<BaseCore>();
        return cachedBaseCore;
    }

    private void OnGUI()
    {
        GUI.Box(new Rect(10, 10, 340, 130), GUIContent.none);
        GUILayout.BeginArea(new Rect(18, 18, 320, 114));

        GUILayout.Label($"Day: {currentDay}   Phase: {currentPhase}");

        BaseCore core = GetOrFindBaseCore();
        GUILayout.Label(core != null
            ? $"BaseCore HP: {core.CurrentHp:0.#} / {core.MaxHp:0.#} (destroyed={core.IsDestroyed})"
            : "BaseCore HP: (not found)");

        GUILayout.Space(6);
        GUILayout.Label($"[{returnToDayKey}] Night->Day   [{forceNightKey}] Day->Night");
        GUILayout.Label($"[{debugDamageBaseCoreKey}] Damage {debugBaseCoreDamageAmount:0.#}   [{debugDestroyBaseCoreKey}] Destroy");

        GUILayout.EndArea();
    }
#endif

    public void TransitionToNight()
    {
        if (currentPhase == GamePhase.Night)
        {
            return;
        }

        LogTransition("Transitioning Day -> Night.");
        currentPhase = GamePhase.Night;

        ResolveDayTimeManager();
        ResolveBaseCore();
        if (boundDayTimeManager != null)
        {
            boundDayTimeManager.Pause();
        }

        if (nightRoot != null)
        {
            nightRoot.SetActive(true);
            ResetNightSpawnerForNewNight();
        }
        else
        {
            Debug.LogWarning("[GameManager] nightRoot is not assigned; night content will not activate.", this);
        }

        AfterEnterNightScene();
        CloseDayOnlyPanels();
        OnPhaseChanged?.Invoke(GamePhase.Night);
    }

    public void TransitionToDay()
    {
        if (currentPhase == GamePhase.Day)
        {
            return;
        }

        currentDay++;
        if (currentDay > 7)
        {
            Debug.Log("<color=green>[GameManager] PROTOTYPE CLEAR: Day 7 Survived! All nights cleared.</color>");
            // For prototype, we stay in Night phase or stop logic.
            // In a real game, you might load a Win scene.
            return;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        LogTransition($"TransitionToDay(): Day {currentDay}.");
#else
        LogTransition($"Transitioning Night -> Day {currentDay}.");
#endif

        currentPhase = GamePhase.Day;

        if (nightRoot != null)
        {
            nightRoot.SetActive(false);
        }

        ResolveDayTimeManager();
        AfterEnterDayScene();
        CloseNightOnlyPanels();
        OnPhaseChanged?.Invoke(GamePhase.Day);
    }

    private void ResetNightSpawnerForNewNight()
    {
        if (cachedNightSpawner == null && nightRoot != null)
        {
            cachedNightSpawner = nightRoot.GetComponentInChildren<SimpleZombieSpawner>(true);
        }

        if (cachedNightSpawner != null)
        {
            cachedNightSpawner.ResetForNewNight();
        }
        else
        {
            Debug.LogWarning("[GameManager] No SimpleZombieSpawner found under nightRoot.", this);
        }
    }

    private void CloseDayOnlyPanels()
    {
        StructureActionPanelUI panel = FindFirstObjectByType<StructureActionPanelUI>();
        if (panel != null)
        {
            panel.Close();
        }
    }

    private void CloseNightOnlyPanels()
    {
        NightRepairPanelUI panel = FindFirstObjectByType<NightRepairPanelUI>();
        if (panel != null)
        {
            panel.Close();
        }
    }

    /// <summary>Resolves and subscribes to the single persistent DayTimeManager once. Single scene means the
    /// instance never changes across phase transitions, so this is a no-op after the first successful call;
    /// it's safe to call again from TransitionToNight/Day as a retry in case the initial Awake() resolve
    /// found nothing yet (e.g. init-order edge case).</summary>
    private void ResolveDayTimeManager()
    {
        if (boundDayTimeManager != null)
        {
            return;
        }

        DayTimeManager manager = FindFirstObjectByType<DayTimeManager>();
        if (manager == null)
        {
            Debug.Log("[GameManager] DayTimeManager not found; will retry on next phase transition.");
            return;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[TEMP-DEBUG][GameManager] ResolveDayTimeManager selected id={manager.GetInstanceID()}");
#endif

        boundDayTimeManager = manager;
        boundDayTimeManager.OnDayEnded += HandleDayEnded;
        Debug.Log("[GameManager] Bound DayTimeManager.");
    }

    private void HandleDayEnded()
    {
        TransitionToNight();
    }

    /// <summary>Resolves and subscribes to the single persistent BaseCore once. Same retry-safe pattern as
    /// ResolveDayTimeManager: single scene means the instance never changes, so this is a no-op after the
    /// first successful call.</summary>
    private void ResolveBaseCore()
    {
        if (boundBaseCore != null)
        {
            return;
        }

        BaseCore core = FindFirstObjectByType<BaseCore>();
        if (core == null)
        {
            Debug.Log("[GameManager] BaseCore not found; will retry on next phase transition.");
            return;
        }

        boundBaseCore = core;
        boundBaseCore.OnBaseDestroyed += HandleBaseDestroyed;
        Debug.Log("[GameManager] Bound BaseCore.");
    }

    private void HandleBaseDestroyed()
    {
        if (currentPhase == GamePhase.GameOver)
        {
            return;
        }

        LogTransition("BaseCore destroyed. Entering GameOver.");
        currentPhase = GamePhase.GameOver;
        Time.timeScale = 0f;
        OnPhaseChanged?.Invoke(GamePhase.GameOver);
    }

    /// <summary>Called from the GameOver screen's Continue action. Returns to the start of the same
    /// (not incremented) day: night content is torn down, the base is restored to full HP, and the day
    /// timer restarts.</summary>
    public void RetryCurrentDay()
    {
        if (currentPhase != GamePhase.GameOver)
        {
            return;
        }

        LogTransition($"RetryCurrentDay(): restarting Day {currentDay}.");
        Time.timeScale = 1f;

        if (nightRoot != null)
        {
            nightRoot.SetActive(false);
        }

        Zombie[] remainingZombies = FindObjectsByType<Zombie>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < remainingZombies.Length; i++)
        {
            Destroy(remainingZombies[i].gameObject);
        }

        if (boundBaseCore != null)
        {
            boundBaseCore.ResetToFull();
        }

        currentPhase = GamePhase.Day;
        ResolveDayTimeManager();
        AfterEnterDayScene();
        CloseNightOnlyPanels();
        OnPhaseChanged?.Invoke(GamePhase.Day);
    }

    private void AfterEnterDayScene()
    {
        BasePersistentIdValidator.ValidateActiveScene(logContext: this);
        LogTransition(
            $"AfterEnterDayScene (Day {currentDay}). CurrentRunState exists={currentRunState != null}, CurrentBaseState exists={CurrentBaseState != null}.");
        TryInitializeDefaultBaseRuntimeState();

        if (boundDayTimeManager != null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[TEMP-DEBUG][GameManager] Calling StartDay() on boundDayTimeManager id={boundDayTimeManager.GetInstanceID()}");
#endif
            LogTransition("Triggering DayTimeManager.StartDay() to ensure loop continues.");
            boundDayTimeManager.StartDay();
        }
    }

    private void AfterEnterNightScene()
    {
        BasePersistentIdValidator.ValidateActiveScene(logContext: this);
        LogTransition(
            $"AfterEnterNightScene. CurrentRunState exists={currentRunState != null}, activeInstanceId={runtimeInstanceId}.");
    }

    public void SetRunRuntimeState(RunRuntimeState run)
    {
        if (run == null)
        {
            Debug.LogWarning("[GameManager] SetRunRuntimeState called with null. Ignoring.");
            return;
        }

        if (run.baseState == null)
        {
            run.baseState = new BaseRuntimeState();
        }

        if (run.resourceState == null)
        {
            run.resourceState = new ResourceRuntimeState();
        }

        if (run.playerState == null)
        {
            run.playerState = new PlayerRuntimeState();
        }

        currentRunState = run;
        hasInitializedRunState = true;
        LogTransition("Active RunRuntimeState assigned.");
    }

    private void TryInitializeDefaultBaseRuntimeState()
    {
        if (currentPhase != GamePhase.Day)
        {
            LogTransition("Default runtime state creation skipped: reason=wrong_phase (only allowed during Day bootstrap).");
            return;
        }

        if (hasInitializedRunState && currentRunState != null)
        {
            LogTransition("Default runtime state creation skipped: reason=existing_state_already_present.");
            return;
        }

        LogTransition("Default runtime state creation: creating new state (no existing state detected).");

        BaseRuntimeState baseState = new BaseRuntimeState
        {
            baseLevel = DefaultBaseLevel,
            baseUpgradeId = DefaultBaseUpgradeId
        };

        ResourceManager resourceManager = GetOrFindResourceManager();
        ResourceRuntimeState resourceState = resourceManager != null
            ? resourceManager.CaptureRuntimeState()
            : new ResourceRuntimeState();

        PlayerHealth playerHealth = FindFirstObjectByType<PlayerHealth>();
        HungerSystem hungerSystem = FindFirstObjectByType<HungerSystem>();
        PlayerRuntimeState playerState = PlayerRuntimeState.FromScene(playerHealth, hungerSystem, null);
        if (playerState == null)
        {
            playerState = new PlayerRuntimeState();
        }

        SetRunRuntimeState(new RunRuntimeState(baseState, resourceState, playerState));
        LogTransition("Created default RunRuntimeState.");
    }

    private ResourceManager GetOrFindResourceManager()
    {
        if (cachedResourceManager != null)
        {
            return cachedResourceManager;
        }

        cachedResourceManager = ResourceManager.ResolveForRunStateTransfer();
        return cachedResourceManager;
    }
}
