using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    private enum GamePhase
    {
        Unknown,
        Day,
        Night
    }

    public static GameManager Instance { get; private set; }

    [SerializeField]
    private string daySceneName = "Day";

    [SerializeField]
    private string nightSceneName = "Night";

    [SerializeField]
    private float nightSurvivalSeconds = 60f;

    [SerializeField]
    private KeyCode returnToDayKey = KeyCode.N;

    private const int DefaultBaseLevel = 1;
    private const string DefaultBaseUpgradeId = "base_lv1";
    private const float DefaultBaseCoreMaxHp = 200f;
    private const float DefaultBaseCoreDefense = 10f;
    private const float DefaultFenceMaxHp = 100f;
    private const float DefaultFenceDefense = 5f;
    private const int DefaultFenceLevel = 1;
    private const string DefaultFenceUpgradeId = "fence_lv1";
    private const bool DefaultFenceUnlocked = true;
    private static readonly string[] BaseCoreNameCandidates = { "BaseCore", "Base Core", "Core", "HomeBase", "Base" };

    private DayTimeManager boundDayTimeManager;
    private BaseManager baseManager;
    private ResourceManager cachedResourceManager;
    private float nightTimerRemaining;
    private GamePhase currentPhase = GamePhase.Unknown;
    private RunRuntimeState currentRunState;
    private bool hasInitializedRunState;
    private int runtimeInstanceId;
    private int currentDay = 1;

    public RunRuntimeState CurrentRunState => currentRunState;
    public BaseRuntimeState CurrentBaseState => currentRunState?.baseState;
    public int CurrentDay => currentDay;

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

        GameObject persistentRoot = transform.root != null ? transform.root.gameObject : gameObject;
        DontDestroyOnLoad(persistentRoot);
        Debug.Log(
            $"[GameManager] Runtime instance created. id={runtimeInstanceId}, object='{name}', persistentRoot='{persistentRoot.name}'.",
            this);

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
        if (currentPhase != GamePhase.Night)
        {
            return;
        }

        UpdateNightStageTimer();
    }

    private void UpdateNightStageTimer()
    {
        nightTimerRemaining -= Time.deltaTime;
        bool timerExpired = nightTimerRemaining <= 0f;
        bool debugReturnToDay = Input.GetKeyDown(returnToDayKey);
        if (timerExpired || debugReturnToDay)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (debugReturnToDay)
            {
                LogTransition($"Debug Night->Day: '{returnToDayKey}' pressed. Calling TransitionToDay() (same path as timer end).");
            }
#endif
            TransitionToDay();
        }
    }

    public void TransitionToNight()
    {
        if (currentPhase == GamePhase.Night)
        {
            return;
        }

        LogTransition("Transitioning Day -> Night.");
        BeforeLeaveDayScene();
        LoadNightSceneInternal();
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
            // For prototype, we stay in Night scene or stop logic. 
            // In a real game, you might load a Win scene.
            return;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        LogTransition($"TransitionToDay(): Day {currentDay}. official Night -> Day path (BeforeLeaveNightScene + LoadDaySceneInternal).");
#else
        LogTransition($"Transitioning Night -> Day {currentDay}.");
#endif
        BeforeLeaveNightScene();
        LoadDaySceneInternal();
    }

    private void LoadDaySceneInternal()
    {
        SceneManager.LoadScene(daySceneName, LoadSceneMode.Single);
    }

    private void LoadNightSceneInternal()
    {
        SceneManager.LoadScene(nightSceneName, LoadSceneMode.Single);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        baseManager = null;
        cachedResourceManager = null;

        LogTransition(
            $"sceneLoaded -> '{scene.name}'. activeInstanceId={runtimeInstanceId}, hasRunState={currentRunState != null}, " +
            $"fenceSlots={CurrentBaseState?.fenceSlots?.Count ?? 0}, towerSlots={CurrentBaseState?.towerSlots?.Count ?? 0}.");

        if (scene.name == daySceneName)
        {
            currentPhase = GamePhase.Day;
            LogTransition("Entered Day scene.");
            BindDayManager();
            AfterEnterDayScene();
        }
        else if (scene.name == nightSceneName)
        {
            currentPhase = GamePhase.Night;
            LogTransition("Entered Night scene.");
            UnbindDayManager();
            nightTimerRemaining = nightSurvivalSeconds;
            AfterEnterNightScene();
        }
        else
        {
            currentPhase = GamePhase.Unknown;
            UnbindDayManager();
        }
    }

    private void BindDayManager()
    {
        DayTimeManager manager = FindFirstObjectByType<DayTimeManager>();
        if (manager == null)
        {
            Debug.Log("[GameManager] DayTimeManager not found in Day scene.");
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
        Debug.Log("[GameManager] Bound DayTimeManager.");
    }

    private void UnbindDayManager()
    {
        if (boundDayTimeManager == null)
        {
            return;
        }

        boundDayTimeManager.OnDayEnded -= HandleDayEnded;
        boundDayTimeManager = null;
        Debug.Log("[GameManager] Unbound DayTimeManager.");
    }

    private void HandleDayEnded()
    {
        if (boundDayTimeManager != null)
        {
            boundDayTimeManager.OnDayEnded -= HandleDayEnded;
            boundDayTimeManager = null;
        }

        TransitionToNight();
    }

    private void BeforeLeaveDayScene()
    {
        CaptureRunStateFromScene("Day");
    }

    private void BeforeLeaveNightScene()
    {
        CaptureRunStateFromScene("Night");
    }

    private void CaptureRunStateFromScene(string phaseLabel)
    {
        BaseRuntimeState previousBase = currentRunState?.baseState;
        BaseManager manager = GetOrFindBaseManager();
        BaseRuntimeState baseState;
        if (manager != null)
        {
            baseState = manager.CaptureState();
            if (previousBase != null)
            {
                MergeBaseCoreFromPreviousIfEmpty(previousBase, baseState);
                MergeTowerSlotsFromPreviousIfStronger(previousBase, baseState);
            }
        }
        else
        {
            baseState = previousBase ?? new BaseRuntimeState();
            LogTransition($"Capture {phaseLabel}: BaseManager not found; reusing previous base state or empty (no scene slot capture).");
        }

        cachedResourceManager = null;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        ResourceManager.TryLogDuplicateResourceManagersInActiveScene();
#endif

        ResourceManager resourceManager = GetOrFindResourceManager();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (resourceManager != null)
        {
            LogTransition($"Capturing resources from instance={resourceManager.GetInstanceID()} before snapshot {resourceManager.GetDebugSummary()}");
        }
        else
        {
            LogTransition("Capturing resources: no ResourceManager resolved.");
        }
#endif

        ResourceRuntimeState resourceState = resourceManager != null
            ? resourceManager.CaptureRuntimeState()
            : (currentRunState?.resourceState ?? new ResourceRuntimeState());

        PlayerHealth playerHealth = FindFirstObjectByType<PlayerHealth>();
        HungerSystem hungerSystem = FindFirstObjectByType<HungerSystem>();
        PlayerRuntimeState playerState = PlayerRuntimeState.FromScene(playerHealth, hungerSystem, currentRunState?.playerState);
        if (playerState == null)
        {
            playerState = currentRunState?.playerState ?? new PlayerRuntimeState();
        }

        Debug.Log($"[PlayerRuntimeState] Saved Player HP={playerState.currentHp} Hunger={playerState.currentHunger} before {phaseLabel} transition.");

        LogTransition(
            $"Captured {phaseLabel} runtime state. fenceSlots={baseState.fenceSlots?.Count ?? 0} towerSlots={baseState.towerSlots?.Count ?? 0} " +
            $"wood={resourceState.wood} scrap={resourceState.scrap} food={resourceState.food} hp={playerState.currentHp} hunger={playerState.currentHunger}");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        LogTowerSlotsLeavingScene(phaseLabel, baseState);
#endif
        SetRunRuntimeState(new RunRuntimeState(baseState, resourceState, playerState));
    }

    private void AfterEnterDayScene()
    {
        BasePersistentIdValidator.ValidateActiveScene(logContext: this);
        LogTransition(
            $"AfterEnterDayScene (Day {currentDay}). CurrentRunState exists={currentRunState != null}, CurrentBaseState exists={CurrentBaseState != null}, " +
            $"towerSlots={CurrentBaseState?.towerSlots?.Count ?? 0}");
        TryInitializeDefaultBaseRuntimeState();
        ApplyRunRuntimeStateToScene("Day");

        if (boundDayTimeManager != null)
        {
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
        ApplyRunRuntimeStateToScene("Night");
    }

    private void ApplyRunRuntimeStateToScene(string phaseLabel)
    {
        if (currentRunState == null)
        {
            return;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        LogTowerSlotsEnteringScene(phaseLabel, currentRunState.baseState);
#endif

        ResourceManager resourceManager = GetOrFindResourceManager();
        if (resourceManager != null && currentRunState.resourceState != null)
        {
            LogTransition(
                $"Applying resources on {phaseLabel} enter. targetInstanceId={resourceManager.GetInstanceID()} beforeApply {resourceManager.GetDebugSummary()} " +
                $"appliedWood={currentRunState.resourceState.wood} appliedScrap={currentRunState.resourceState.scrap} appliedFood={currentRunState.resourceState.food}");
            resourceManager.ApplyRuntimeState(currentRunState.resourceState);
        }

        BaseManager baseMgr = GetOrFindBaseManager();
        if (baseMgr != null && currentRunState.baseState != null)
        {
            LogTransition(
                $"Applying runtime state on {phaseLabel} enter. fenceSlots={currentRunState.baseState.fenceSlots?.Count ?? 0} " +
                $"towerSlots={currentRunState.baseState.towerSlots?.Count ?? 0}");
            baseMgr.ApplyState(currentRunState.baseState);
        }

        PlayerHealth playerHealth = FindFirstObjectByType<PlayerHealth>();
        if (playerHealth != null && currentRunState.playerState != null)
        {
            playerHealth.ApplyRuntimeState(currentRunState.playerState);
        }

        HungerSystem hungerSystem = FindFirstObjectByType<HungerSystem>();
        if (hungerSystem != null && currentRunState.playerState != null)
        {
            hungerSystem.ApplyRuntimeState(currentRunState.playerState);
        }

        HUDController hud = FindFirstObjectByType<HUDController>();
        if (hud != null)
        {
            hud.RefreshHudFromPlayerStats();
        }

        if (currentRunState.playerState != null)
{
            string context = phaseLabel == "Day" ? $"Day {currentDay}" : "Night scene";
            Debug.Log($"[PlayerRuntimeState] Restored Player HP={currentRunState.playerState.currentHp} Hunger={currentRunState.playerState.currentHunger} on {context} start.");
        }
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

    public void SetBaseRuntimeState(BaseRuntimeState state)
    {
        if (state == null)
        {
            Debug.LogWarning("[GameManager] SetBaseRuntimeState called with null. Ignoring.");
            return;
        }

        if (currentRunState == null)
        {
            currentRunState = new RunRuntimeState();
        }

        currentRunState.baseState = state;
        if (currentRunState.resourceState == null)
        {
            currentRunState.resourceState = new ResourceRuntimeState();
        }

        if (currentRunState.playerState == null)
        {
            currentRunState.playerState = new PlayerRuntimeState();
        }

        hasInitializedRunState = true;
        LogTransition("Active BaseRuntimeState assigned (base slice only).");
    }

    public BaseRuntimeState GetBaseRuntimeState()
    {
        return CurrentBaseState;
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

        baseState.baseCore = BuildDefaultBaseCoreState();
        baseState.fences = BuildDefaultFenceStates();
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
        LogTransition(
            $"Created default RunRuntimeState. Fences: {baseState.fences.Count}, TowerSlots: {baseState.towerSlots.Count}, BaseCoreId: '{baseState.baseCore.id}'.");
    }

    private BaseCoreRuntimeState BuildDefaultBaseCoreState()
    {
        BaseCoreRuntimeState core = new BaseCoreRuntimeState
        {
            currentHp = DefaultBaseCoreMaxHp,
            maxHp = DefaultBaseCoreMaxHp,
            defense = DefaultBaseCoreDefense,
            isDestroyed = false
        };

        GameObject baseCoreObject = FindBaseCoreObject(SceneManager.GetActiveScene());
        if (baseCoreObject == null)
        {
            Debug.LogWarning("[GameManager] Base core object not found while building default runtime state.", this);
            return core;
        }

        PersistentId id = baseCoreObject.GetComponent<PersistentId>();
        if (id == null || string.IsNullOrWhiteSpace(id.Id))
        {
            Debug.LogWarning($"[GameManager] Base core '{baseCoreObject.name}' is missing a valid PersistentId.", baseCoreObject);
            return core;
        }

        core.id = id.Id;
        Debug.Log($"[GameManager] Base core lookup succeeded: '{baseCoreObject.name}' (id='{core.id}').", this);
        return core;
    }

    private List<FenceRuntimeState> BuildDefaultFenceStates()
    {
        List<FenceRuntimeState> fences = new List<FenceRuntimeState>();
        HashSet<string> seen = new HashSet<string>();
        FenceSegment[] sceneFences = FindObjectsByType<FenceSegment>(FindObjectsSortMode.None);

        for (int i = 0; i < sceneFences.Length; i++)
        {
            FenceSegment fence = sceneFences[i];
            if (fence == null || fence.gameObject.scene != SceneManager.GetActiveScene())
            {
                continue;
            }

            if (!TryGetPersistentId(fence.gameObject, out string id))
            {
                continue;
            }

            if (!seen.Add(id))
            {
                Debug.LogWarning($"[GameManager] Skipping duplicate fence PersistentId '{id}' on '{fence.name}'.", fence);
                continue;
            }

            fences.Add(new FenceRuntimeState
            {
                id = id,
                level = DefaultFenceLevel,
                upgradeId = DefaultFenceUpgradeId,
                currentHp = DefaultFenceMaxHp,
                maxHp = DefaultFenceMaxHp,
                defense = DefaultFenceDefense,
                isDestroyed = false,
                isUnlocked = DefaultFenceUnlocked
            });
        }

        return fences;
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

    private static GameObject FindBaseCoreObject(Scene scene)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            GameObject root = roots[i];
            if (root == null || root.name != "HomeBase")
            {
                continue;
            }

            PersistentId rootPersistentId = root.GetComponent<PersistentId>();
            if (rootPersistentId != null)
            {
                return root;
            }

            PersistentId[] childPersistentIds = root.GetComponentsInChildren<PersistentId>(true);
            if (childPersistentIds.Length > 0 && childPersistentIds[0] != null)
            {
                return childPersistentIds[0].gameObject;
            }
        }

        for (int i = 0; i < roots.Length; i++)
        {
            GameObject found = FindBaseCoreByNameRecursive(roots[i].transform);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static GameObject FindBaseCoreByNameRecursive(Transform root)
    {
        if (root == null)
        {
            return null;
        }

        for (int i = 0; i < BaseCoreNameCandidates.Length; i++)
        {
            if (root.name == BaseCoreNameCandidates[i])
            {
                return root.gameObject;
            }
        }

        for (int i = 0; i < root.childCount; i++)
        {
            GameObject found = FindBaseCoreByNameRecursive(root.GetChild(i));
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static void MergeBaseCoreFromPreviousIfEmpty(BaseRuntimeState previous, BaseRuntimeState captured)
    {
        if (previous == null || captured == null || previous.baseCore == null || captured.baseCore == null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(captured.baseCore.id))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(previous.baseCore.id))
        {
            return;
        }

        captured.baseCore.id = previous.baseCore.id;
        captured.baseCore.currentHp = previous.baseCore.currentHp;
        captured.baseCore.maxHp = previous.baseCore.maxHp;
        captured.baseCore.defense = previous.baseCore.defense;
        captured.baseCore.isDestroyed = previous.baseCore.isDestroyed;
    }

    private static void MergeTowerSlotsFromPreviousIfStronger(BaseRuntimeState previous, BaseRuntimeState captured)
    {
        if (previous?.towerSlots == null || captured?.towerSlots == null)
        {
            return;
        }

        Dictionary<string, int> indexById = new Dictionary<string, int>();
        for (int i = 0; i < captured.towerSlots.Count; i++)
        {
            TowerSlotRuntimeState s = captured.towerSlots[i];
            if (s == null || string.IsNullOrWhiteSpace(s.id))
            {
                continue;
            }

            if (!indexById.ContainsKey(s.id))
            {
                indexById.Add(s.id, i);
            }
        }

        for (int p = 0; p < previous.towerSlots.Count; p++)
        {
            TowerSlotRuntimeState prev = previous.towerSlots[p];
            if (prev == null || !prev.hasTower || string.IsNullOrWhiteSpace(prev.id))
            {
                continue;
            }

            if (!indexById.TryGetValue(prev.id, out int idx))
            {
                captured.towerSlots.Add(
                    new TowerSlotRuntimeState
                    {
                        id = prev.id,
                        hasTower = true,
                        towerId = string.IsNullOrWhiteSpace(prev.towerId) ? TowerSlot.ArrowTowerTowerId : prev.towerId,
                        level = prev.level > 0 ? prev.level : 1,
                        currentHp = Mathf.Max(0f, prev.currentHp),
                        isDestroyed = prev.isDestroyed
                    });
                indexById[prev.id] = captured.towerSlots.Count - 1;
                continue;
            }

            TowerSlotRuntimeState cur = captured.towerSlots[idx];
            if (cur != null && !cur.hasTower)
            {
                cur.hasTower = true;
                cur.towerId = string.IsNullOrWhiteSpace(prev.towerId) ? TowerSlot.ArrowTowerTowerId : prev.towerId;
                cur.level = prev.level > 0 ? prev.level : 1;
                cur.currentHp = Mathf.Max(0f, prev.currentHp);
                cur.isDestroyed = prev.isDestroyed;
            }
        }
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private static void LogTowerSlotsLeavingScene(string phaseLabel, BaseRuntimeState baseState)
    {
        int fenceCount = baseState?.fenceSlots?.Count ?? 0;
        int towerCount = baseState?.towerSlots?.Count ?? 0;
        if (baseState?.towerSlots == null || baseState.towerSlots.Count == 0)
        {
            Debug.Log($"[GameManager] Leaving {phaseLabel}: captured fenceSlots={fenceCount} towerSlots={towerCount} (no tower slot entries).");
            return;
        }

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.Append($"[GameManager] Leaving {phaseLabel}: captured fenceSlots={fenceCount} towerSlots={towerCount}. Occupied towers: ");
        bool any = false;
        for (int i = 0; i < baseState.towerSlots.Count; i++)
        {
            TowerSlotRuntimeState t = baseState.towerSlots[i];
            if (t == null || !t.hasTower)
            {
                continue;
            }

            if (any)
            {
                sb.Append("; ");
            }

            any = true;
            sb.Append(
                $"id='{t.id}' towerId='{t.towerId}' level={t.level} hp={t.currentHp:0.##} destroyed={t.isDestroyed}");
        }

        if (!any)
        {
            sb.Append("(none with hasTower)");
        }

        Debug.Log(sb.ToString());
    }

    private static void LogTowerSlotsEnteringScene(string phaseLabel, BaseRuntimeState baseState)
    {
        bool hasSlice = baseState != null;
        int towerCount = baseState?.towerSlots?.Count ?? 0;
        if (!hasSlice || towerCount == 0)
        {
            Debug.Log(
                $"[GameManager] Enter {phaseLabel}: baseState slice exists={hasSlice} incomingTowerSlots={towerCount} incomingHasTowerIds=(none)");
            return;
        }

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.Append($"[GameManager] Enter {phaseLabel}: baseState slice exists=True incomingTowerSlots={towerCount}. Incoming hasTower slot IDs: ");
        bool any = false;
        for (int i = 0; i < baseState.towerSlots.Count; i++)
        {
            TowerSlotRuntimeState t = baseState.towerSlots[i];
            if (t == null || !t.hasTower)
            {
                continue;
            }

            if (any)
            {
                sb.Append(", ");
            }

            any = true;
            sb.Append(
                $"'{t.id}' (towerId='{t.towerId}' level={t.level} hp={t.currentHp:0.##} destroyed={t.isDestroyed})");
        }

        if (!any)
        {
            sb.Append("(none)");
        }

        Debug.Log(sb.ToString());
    }
#endif

    private BaseManager GetOrFindBaseManager()
    {
        if (baseManager != null)
        {
            return baseManager;
        }

        baseManager = FindFirstObjectByType<BaseManager>();
        return baseManager;
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
