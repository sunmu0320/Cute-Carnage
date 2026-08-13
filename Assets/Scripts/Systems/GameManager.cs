using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    public enum GamePhase
    {
        Unknown,
        Day,
        Night
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

    private void AfterEnterDayScene()
    {
        BasePersistentIdValidator.ValidateActiveScene(logContext: this);
        LogTransition(
            $"AfterEnterDayScene (Day {currentDay}). CurrentRunState exists={currentRunState != null}, CurrentBaseState exists={CurrentBaseState != null}, " +
            $"towerSlots={CurrentBaseState?.towerSlots?.Count ?? 0}");
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
            $"AfterEnterNightScene. CurrentRunState exists={currentRunState != null}, activeInstanceId={runtimeInstanceId}, " +
            $"fenceSlots={CurrentBaseState?.fenceSlots?.Count ?? 0}, towerSlots={CurrentBaseState?.towerSlots?.Count ?? 0}.");
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

        baseState.towerSlots = BuildDefaultTowerSlotStates();

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
        LogTransition($"Created default RunRuntimeState. TowerSlots: {baseState.towerSlots.Count}.");
    }

    private List<TowerSlotRuntimeState> BuildDefaultTowerSlotStates()
    {
        List<TowerSlotRuntimeState> slots = new List<TowerSlotRuntimeState>();
        HashSet<string> seen = new HashSet<string>();
        TowerSlot[] sceneSlots = FindObjectsByType<TowerSlot>(FindObjectsSortMode.None);

        for (int i = 0; i < sceneSlots.Length; i++)
        {
            TowerSlot slot = sceneSlots[i];
            if (slot == null || slot.gameObject.scene != SceneManager.GetActiveScene())
            {
                continue;
            }

            if (!TryGetPersistentId(slot.gameObject, out string id))
            {
                continue;
            }

            if (!seen.Add(id))
            {
                Debug.LogWarning($"[GameManager] Skipping duplicate tower slot PersistentId '{id}' on '{slot.name}'.", slot);
                continue;
            }

            slots.Add(new TowerSlotRuntimeState
            {
                id = id,
                hasTower = false,
                towerId = string.Empty,
                level = 1,
                currentHp = 0f,
                isDestroyed = false
            });
        }

        return slots;
    }

    private bool TryGetPersistentId(GameObject target, out string id)
    {
        id = string.Empty;
        if (target == null)
        {
            return false;
        }

        PersistentId persistentId = target.GetComponent<PersistentId>();
        if (persistentId == null)
        {
            Debug.LogWarning($"[GameManager] '{target.name}' is missing PersistentId and will be skipped from default runtime initialization.", target);
            return false;
        }

        if (string.IsNullOrWhiteSpace(persistentId.Id))
        {
            Debug.LogWarning($"[GameManager] '{target.name}' has an empty PersistentId and will be skipped from default runtime initialization.", target);
            return false;
        }

        id = persistentId.Id;
        return true;
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
