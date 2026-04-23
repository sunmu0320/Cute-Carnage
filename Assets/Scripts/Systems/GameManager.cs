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
    private const bool DefaultTowerSlotUnlocked = true;
    private const bool DefaultTowerHasTower = false;
    private const string DefaultTowerTypeId = "";
    private const int DefaultTowerLevel = 1;
    private const string DefaultTowerUpgradeId = "tower_lv1";
    private const float DefaultTowerMaxHp = 100f;
    private const float DefaultTowerDamage = 0f;
    private const float DefaultTowerAttackRate = 0f;
    private const float DefaultTowerAttackRange = 0f;
    private static readonly string[] BaseCoreNameCandidates = { "BaseCore", "Base Core", "Core", "HomeBase", "Base" };

    private DayTimeManager boundDayTimeManager;
    private BaseManager baseManager;
    private float nightTimerRemaining;
    private GamePhase currentPhase = GamePhase.Unknown;
    public BaseRuntimeState CurrentBaseState { get; private set; }
    private bool hasInitializedBaseRuntimeState;
    private int runtimeInstanceId;

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
        bool shouldReturnToDay = nightTimerRemaining <= 0f || Input.GetKeyDown(returnToDayKey);
        if (shouldReturnToDay)
        {
            TransitionToDay();
        }
    }

    public void TransitionToNight()
    {
        if (currentPhase == GamePhase.Night)
        {
            return;
        }

        Debug.Log("[GameManager] Transitioning Day -> Night.");
        BeforeLeaveDayScene();
        LoadNightSceneInternal();
    }

    public void TransitionToDay()
    {
        if (currentPhase == GamePhase.Day)
        {
            return;
        }

        Debug.Log("[GameManager] Transitioning Night -> Day.");
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
        Debug.Log(
            $"[GameManager] sceneLoaded -> '{scene.name}'. activeInstanceId={runtimeInstanceId}, hasCurrentBaseState={CurrentBaseState != null}, fenceSlots={CurrentBaseState?.fenceSlots?.Count ?? 0}.",
            this);

        if (scene.name == daySceneName)
        {
            currentPhase = GamePhase.Day;
            Debug.Log("[GameManager] Entered Day scene.");
            BindDayManager();
            AfterEnterDayScene();
        }
        else if (scene.name == nightSceneName)
        {
            currentPhase = GamePhase.Night;
            Debug.Log("[GameManager] Entered Night scene.");
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
        // Capture runtime slot placement state before leaving Day scene.
        BaseManager manager = GetOrFindBaseManager();
        if (manager == null)
        {
            return;
        }

        BaseRuntimeState captured = manager.CaptureState();
        if (captured != null)
        {
            Debug.Log($"[GameManager] Captured Day runtime state. fenceSlots={captured.fenceSlots?.Count ?? 0}", this);
            SetBaseRuntimeState(captured);
        }
    }

    private void BeforeLeaveNightScene()
    {
        // Placeholder: export runtime/base state before leaving Night scene.
    }

    private void AfterEnterDayScene()
    {
        // Placeholder: import/reconstruct runtime/base state after entering Day scene.
        BasePersistentIdValidator.ValidateActiveScene(logContext: this);
        Debug.Log($"[GameManager] AfterEnterDayScene. CurrentBaseState exists={CurrentBaseState != null}", this);
        TryInitializeDefaultBaseRuntimeState();
    }

    private void AfterEnterNightScene()
    {
        // Placeholder: import/reconstruct runtime/base state after entering Night scene.
        BasePersistentIdValidator.ValidateActiveScene(logContext: this);
        Debug.Log(
            $"[GameManager] AfterEnterNightScene. CurrentBaseState exists={CurrentBaseState != null}, activeInstanceId={runtimeInstanceId}, fenceSlots={CurrentBaseState?.fenceSlots?.Count ?? 0}.",
            this);

        BaseManager manager = GetOrFindBaseManager();
        if (manager != null)
        {
            Debug.Log($"[GameManager] Applying runtime state on Night enter. fenceSlots={CurrentBaseState?.fenceSlots?.Count ?? 0}", this);
            manager.ApplyState(CurrentBaseState);
        }
    }

    public void SetBaseRuntimeState(BaseRuntimeState state)
    {
        if (state == null)
        {
            Debug.LogWarning("[GameManager] SetBaseRuntimeState called with null. Ignoring.");
            return;
        }

        CurrentBaseState = state;
        hasInitializedBaseRuntimeState = true;
        Debug.Log("[GameManager] Active BaseRuntimeState assigned.");
    }

    public BaseRuntimeState GetBaseRuntimeState()
    {
        return CurrentBaseState;
    }

    private void TryInitializeDefaultBaseRuntimeState()
    {
        if (currentPhase != GamePhase.Day)
        {
            Debug.Log("[GameManager] Default runtime state creation skipped: only allowed during Day bootstrap.", this);
            return;
        }

        if (hasInitializedBaseRuntimeState && CurrentBaseState != null)
        {
            Debug.Log("[GameManager] Default runtime state creation skipped: existing state already present.", this);
            return;
        }

        Debug.Log("[GameManager] Creating default BaseRuntimeState (no existing state detected).", this);

        BaseRuntimeState state = new BaseRuntimeState
        {
            baseLevel = DefaultBaseLevel,
            baseUpgradeId = DefaultBaseUpgradeId
        };

        state.baseCore = BuildDefaultBaseCoreState();
        state.fences = BuildDefaultFenceStates();
        state.towerSlots = BuildDefaultTowerSlotStates();

        SetBaseRuntimeState(state);
        Debug.Log(
            $"[GameManager] Created default BaseRuntimeState. Fences: {state.fences.Count}, TowerSlots: {state.towerSlots.Count}, BaseCoreId: '{state.baseCore.id}'.",
            this);
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
                isUnlocked = DefaultTowerSlotUnlocked,
                hasTower = DefaultTowerHasTower,
                towerTypeId = DefaultTowerTypeId,
                level = DefaultTowerLevel,
                upgradeId = DefaultTowerUpgradeId,
                currentHp = DefaultTowerMaxHp,
                maxHp = DefaultTowerMaxHp,
                damage = DefaultTowerDamage,
                attackRate = DefaultTowerAttackRate,
                attackRange = DefaultTowerAttackRange,
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

    private BaseManager GetOrFindBaseManager()
    {
        if (baseManager != null)
        {
            return baseManager;
        }

        baseManager = FindFirstObjectByType<BaseManager>();
        return baseManager;
    }
}
