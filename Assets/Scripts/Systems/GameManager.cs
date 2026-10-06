using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public enum GamePhase
    {
        Unknown,
        Day,
        Night,
        GameOver,
        Victory
    }

    public enum GameOverCause
    {
        None,
        BaseDestroyed,
        PlayerDied
    }

    public static GameManager Instance { get; private set; }

    [SerializeField]
    private KeyCode returnToDayKey = KeyCode.N;

    [Header("Night Content")]
    [Tooltip("Container holding night-only content (currently: the zombie spawner). Toggled active/inactive on phase transitions.")]
    [SerializeField]
    private GameObject nightRoot;

    [Header("GameOver Timing")]
    [Tooltip("Delay between BaseCore being destroyed and the GameOver panel appearing.")]
    [SerializeField]
    private float baseDestroyedPanelDelaySeconds = 2f;

    [Tooltip("Delay between the player dying and the GameOver panel appearing (leaves room for a death animation).")]
    [SerializeField]
    private float playerDiedPanelDelaySeconds = 2.5f;

    [Header("Respawn")]
    [Tooltip("Where the player is placed (position + facing) when retrying the day after GameOver. Leave empty to keep the player where they were.")]
    [SerializeField]
    private Transform playerRespawnPoint;

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
    private PlayerHealth boundPlayerHealth;
    private DayCheckpoint lastDayCheckpoint;
    private readonly ResourceNodeRegistry resourceNodeRegistry = new ResourceNodeRegistry();
    private Coroutine gameOverPanelDelayCoroutine;
    [SerializeField, Min(0f)] private float victoryPanelDelaySeconds = 2f;
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
    public bool IsVictory => currentPhase == GamePhase.Victory;
    public const int FinalDay = 7;
    public GameOverCause LastGameOverCause { get; private set; } = GameOverCause.None;

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
        ResolvePlayerHealth();
    }

    private void Start()
    {
        resourceNodeRegistry.BuildLookup();
        AfterEnterDayScene();
        LoadSaveIfRequested();
        CaptureDayCheckpoint();
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

        if (boundPlayerHealth != null)
        {
            boundPlayerHealth.onDeath.RemoveListener(HandlePlayerDied);
            boundPlayerHealth = null;
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
        // Top-right so it doesn't cover the top-left HP/Hunger HUD.
        float x = Screen.width - 350f;
        GUI.Box(new Rect(x, 10, 340, 130), GUIContent.none);
        GUILayout.BeginArea(new Rect(x + 8, 18, 320, 114));

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
        if (currentPhase == GamePhase.Night || currentPhase == GamePhase.GameOver || currentPhase == GamePhase.Victory)
        {
            return;
        }

        LogTransition("Transitioning Day -> Night.");
        currentPhase = GamePhase.Night;

        ResolveDayTimeManager();
        ResolveBaseCore();
        ResolvePlayerHealth();
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
        if (currentPhase == GamePhase.Day || currentPhase == GamePhase.GameOver || currentPhase == GamePhase.Victory)
        {
            return;
        }

        if (currentDay >= FinalDay)
        {
            EnterVictory();
            return;
        }

        currentDay++;

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
        resourceNodeRegistry.RegrowDue(currentDay);
        CaptureDayCheckpoint();
        OnPhaseChanged?.Invoke(GamePhase.Day);
    }

    public void DepleteResourceNode(ResourceNode node)
    {
        resourceNodeRegistry.MarkDepleted(node, currentDay);
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

    /// <summary>Resolves and subscribes to the single persistent PlayerHealth once. Same retry-safe pattern as
    /// ResolveBaseCore.</summary>
    private void ResolvePlayerHealth()
    {
        if (boundPlayerHealth != null)
        {
            return;
        }

        PlayerHealth health = FindFirstObjectByType<PlayerHealth>();
        if (health == null)
        {
            Debug.Log("[GameManager] PlayerHealth not found; will retry on next phase transition.");
            return;
        }

        boundPlayerHealth = health;
        boundPlayerHealth.onDeath.AddListener(HandlePlayerDied);
        Debug.Log("[GameManager] Bound PlayerHealth.");
    }

    private void HandleBaseDestroyed()
    {
        EnterGameOver(GameOverCause.BaseDestroyed, baseDestroyedPanelDelaySeconds);
    }

    private void HandlePlayerDied()
    {
        EnterGameOver(GameOverCause.PlayerDied, playerDiedPanelDelaySeconds);
    }

    /// <summary>Freezes the game immediately (player input, zombies, spawner, day timer, open panels), then
    /// shows the GameOver panel after panelDelaySeconds - long enough for a death/destruction beat (e.g. a
    /// future death animation) to read before the UI covers the screen.</summary>
    private void EnterGameOver(GameOverCause cause, float panelDelaySeconds)
    {
        if (currentPhase == GamePhase.GameOver || currentPhase == GamePhase.Victory)
        {
            return;
        }

        LogTransition($"Entering GameOver. cause={cause}.");
        currentPhase = GamePhase.GameOver;
        LastGameOverCause = cause;
        SetPlayerInputLocked(true);
        SetZombiesFrozen(true);
        if (cachedNightSpawner != null)
        {
            cachedNightSpawner.SetPaused(true);
        }

        if (boundDayTimeManager != null)
        {
            boundDayTimeManager.Pause();
        }

        CloseDayOnlyPanels();
        CloseNightOnlyPanels();

        if (gameOverPanelDelayCoroutine != null)
        {
            StopCoroutine(gameOverPanelDelayCoroutine);
        }

        gameOverPanelDelayCoroutine = StartCoroutine(AnnouncePhaseAfterDelay(GamePhase.GameOver, panelDelaySeconds));
    }

    /// <summary>Final night cleared (reached only via SimpleZombieSpawner -> TransitionToDay). Freezes play
    /// like GameOver, then announces Victory so the end panel shows and lighting fades to morning.</summary>
    private void EnterVictory()
    {
        LogTransition($"Victory: survived {FinalDay} nights.");
        SaveSystem.Delete();
        currentPhase = GamePhase.Victory;
        SetPlayerInputLocked(true);
        SetZombiesFrozen(true);
        if (cachedNightSpawner != null)
        {
            cachedNightSpawner.SetPaused(true);
        }

        if (boundDayTimeManager != null)
        {
            boundDayTimeManager.Pause();
        }

        CloseDayOnlyPanels();
        CloseNightOnlyPanels();

        if (gameOverPanelDelayCoroutine != null)
        {
            StopCoroutine(gameOverPanelDelayCoroutine);
        }

        gameOverPanelDelayCoroutine = StartCoroutine(AnnouncePhaseAfterDelay(GamePhase.Victory, victoryPanelDelaySeconds));
    }

    private IEnumerator AnnouncePhaseAfterDelay(GamePhase phase, float delaySeconds)
    {
        yield return new WaitForSecondsRealtime(delaySeconds);
        gameOverPanelDelayCoroutine = null;
        OnPhaseChanged?.Invoke(phase);
    }

    /// <summary>Locks/unlocks player movement, interaction, auto-attack, food consumption, and hunger drain
    /// for the GameOver freeze. Time.timeScale is left untouched so zombies stay alive (idling, not frozen
    /// mid-frame) and ambient environment content (wind sway, particles, etc.) keeps playing. UI buttons
    /// (Continue, future Settings) are unaffected since Unity's EventSystem handles clicks independently of
    /// these components.</summary>
    private void SetPlayerInputLocked(bool locked)
    {
        PlayerMovement playerMovement = FindFirstObjectByType<PlayerMovement>();
        if (playerMovement != null)
        {
            playerMovement.SetMovementLocked(locked);
        }

        PlayerInteractor playerInteractor = FindFirstObjectByType<PlayerInteractor>();
        if (playerInteractor != null)
        {
            playerInteractor.enabled = !locked;
        }

        PlayerConsume playerConsume = FindFirstObjectByType<PlayerConsume>();
        if (playerConsume != null)
        {
            playerConsume.enabled = !locked;
        }

        PlayerAutoCombat playerAutoCombat = FindFirstObjectByType<PlayerAutoCombat>();
        if (playerAutoCombat != null)
        {
            playerAutoCombat.enabled = !locked;
        }

        HungerSystem hungerSystem = FindFirstObjectByType<HungerSystem>();
        if (hungerSystem != null)
        {
            hungerSystem.SetActiveDrain(!locked);
        }
    }

    /// <summary>Stops zombies moving/attacking/re-targeting without disabling them, so they stay alive and
    /// visibly idle in place instead of freezing mid-animation.</summary>
    private void SetZombiesFrozen(bool frozen)
    {
        Zombie[] zombies = FindObjectsByType<Zombie>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < zombies.Length; i++)
        {
            zombies[i].SetFrozen(frozen);
        }
    }

    /// <summary>Called from the GameOver screen's Continue action. Fully reverts to the checkpoint captured
    /// at the start of the current Day: night content is torn down and BaseCore HP, resources, player
    /// HP/Hunger, and every Fence/Tower slot are restored to that snapshot before the day timer restarts.</summary>
    public void RetryCurrentDay()
    {
        if (currentPhase != GamePhase.GameOver)
        {
            return;
        }

        LogTransition($"RetryCurrentDay(): restoring Day {currentDay} checkpoint.");
        SetPlayerInputLocked(false);

        if (nightRoot != null)
        {
            nightRoot.SetActive(false);
        }

        Zombie[] remainingZombies = FindObjectsByType<Zombie>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < remainingZombies.Length; i++)
        {
            Destroy(remainingZombies[i].gameObject);
        }

        RestoreDayCheckpoint();
        RespawnPlayer();

        currentPhase = GamePhase.Day;
        ResolveDayTimeManager();
        AfterEnterDayScene();
        CloseNightOnlyPanels();
        OnPhaseChanged?.Invoke(GamePhase.Day);
    }

    /// <summary>Teleports the player to playerRespawnPoint. PlayerMovement drives a Rigidbody via MovePosition,
    /// so the Rigidbody's position is set directly (and velocity cleared) - moving only the Transform would
    /// be overwritten on the next physics step.</summary>
    private void RespawnPlayer()
    {
        if (playerRespawnPoint == null)
        {
            Debug.LogWarning("[GameManager] playerRespawnPoint is not assigned; player stays where they died.", this);
            return;
        }

        TeleportPlayer(playerRespawnPoint.position, Quaternion.Euler(0f, playerRespawnPoint.eulerAngles.y, 0f));
    }

    private void TeleportPlayer(Vector3 position, Quaternion rotation)
    {
        PlayerMovement player = FindFirstObjectByType<PlayerMovement>();
        if (player == null)
        {
            return;
        }

        player.transform.SetPositionAndRotation(position, rotation);

        Rigidbody rb = player.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.position = position;
            rb.rotation = rotation;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }

    /// <summary>Snapshots everything RetryCurrentDay() restores: BaseCore HP, resources, player HP/Hunger,
    /// and every Fence/Tower slot's occupied state + HP, keyed by PersistentId. Called at the start of each
    /// Day (Start()/TransitionToDay()) - not from RetryCurrentDay() itself, so repeated retries of the same
    /// day always return to the same original snapshot.</summary>
    private void CaptureDayCheckpoint()
    {
        DayCheckpoint checkpoint = new DayCheckpoint();

        if (boundBaseCore != null)
        {
            checkpoint.baseCoreHp = boundBaseCore.CurrentHp;
        }

        ResourceManager resourceManager = GetOrFindResourceManager();
        if (resourceManager != null)
        {
            checkpoint.resourceState = resourceManager.CaptureRuntimeState();
        }

        if (boundPlayerHealth != null)
        {
            checkpoint.playerHp = boundPlayerHealth.CurrentHealth;
        }

        HungerSystem hungerSystem = FindFirstObjectByType<HungerSystem>();
        if (hungerSystem != null)
        {
            checkpoint.playerHunger = hungerSystem.CurrentHunger;
        }

        PlayerMovement player = FindFirstObjectByType<PlayerMovement>();
        if (player != null)
        {
            checkpoint.hasPlayerPose = true;
            checkpoint.playerPosition = player.transform.position;
            checkpoint.playerYaw = player.transform.eulerAngles.y;
        }

        FenceSlot[] fenceSlots = FindObjectsByType<FenceSlot>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < fenceSlots.Length; i++)
        {
            FenceSlot slot = fenceSlots[i];
            string id = slot.PersistentSlotId;
            if (string.IsNullOrEmpty(id))
            {
                continue;
            }

            FenceSegment fence = slot.CurrentFence;
            checkpoint.fenceSlots[id] = new FenceCheckpoint
            {
                hasFence = slot.HasFence,
                fenceData = fence != null ? fence.Data : null,
                currentHp = fence != null ? fence.CurrentHp : 0f
            };
        }

        TowerSlot[] towerSlots = FindObjectsByType<TowerSlot>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < towerSlots.Length; i++)
        {
            TowerSlot slot = towerSlots[i];
            string id = slot.PersistentSlotId;
            if (string.IsNullOrEmpty(id))
            {
                continue;
            }

            ArrowTower tower = slot.CurrentTower;
            checkpoint.towerSlots[id] = new TowerCheckpoint
            {
                hasTower = slot.HasTower,
                currentHp = tower != null ? tower.CurrentHp : 0f
            };
        }

        checkpoint.depletedResourceNodes = resourceNodeRegistry.CaptureState();

        lastDayCheckpoint = checkpoint;
        SaveSystem.Write(ToSaveData(checkpoint));
        LogTransition(
            $"Captured day checkpoint for Day {currentDay}: baseHp={checkpoint.baseCoreHp:0.#}, " +
            $"wood={checkpoint.resourceState.wood} scrap={checkpoint.resourceState.scrap} food={checkpoint.resourceState.food}, " +
            $"fenceSlots={checkpoint.fenceSlots.Count}, towerSlots={checkpoint.towerSlots.Count}, " +
            $"depletedNodes={checkpoint.depletedResourceNodes.Count}.");
    }

    /// <summary>Continue from MainMenu: replaces the fresh scene state with the saved Day-start checkpoint
    /// (same restore path as RetryCurrentDay). Start() then re-captures it, which rewrites the same save.</summary>
    private void LoadSaveIfRequested()
    {
        if (!SaveSystem.LoadOnNextStart)
        {
            return;
        }

        SaveSystem.LoadOnNextStart = false;
        if (!SaveSystem.TryRead(out SaveData data))
        {
            Debug.LogWarning("[GameManager] Continue requested but no valid save found; starting a new game.", this);
            return;
        }

        currentDay = data.day;
        lastDayCheckpoint = FromSaveData(data);
        RestoreDayCheckpoint();
        if (data.hasPlayerPose)
        {
            TeleportPlayer(data.playerPosition, Quaternion.Euler(0f, data.playerYaw, 0f));
        }
        LogTransition($"Loaded save: Day {currentDay}.");
    }

    private SaveData ToSaveData(DayCheckpoint checkpoint)
    {
        SaveData data = new SaveData
        {
            day = currentDay,
            baseCoreHp = checkpoint.baseCoreHp,
            wood = checkpoint.resourceState.wood,
            scrap = checkpoint.resourceState.scrap,
            food = checkpoint.resourceState.food,
            playerHp = checkpoint.playerHp,
            playerHunger = checkpoint.playerHunger,
            hasPlayerPose = checkpoint.hasPlayerPose,
            playerPosition = checkpoint.playerPosition,
            playerYaw = checkpoint.playerYaw
        };

        foreach (KeyValuePair<string, FenceCheckpoint> kv in checkpoint.fenceSlots)
        {
            data.fences.Add(new FenceSave
            {
                id = kv.Key,
                hasFence = kv.Value.hasFence,
                tier = kv.Value.fenceData != null ? kv.Value.fenceData.TierNumber : 0,
                hp = kv.Value.currentHp
            });
        }

        foreach (KeyValuePair<string, TowerCheckpoint> kv in checkpoint.towerSlots)
        {
            data.towers.Add(new TowerSave { id = kv.Key, hasTower = kv.Value.hasTower, hp = kv.Value.currentHp });
        }

        foreach (KeyValuePair<string, int> kv in checkpoint.depletedResourceNodes)
        {
            data.depletedNodes.Add(new NodeSave { id = kv.Key, respawnDay = kv.Value });
        }

        return data;
    }

    private DayCheckpoint FromSaveData(SaveData data)
    {
        DayCheckpoint checkpoint = new DayCheckpoint
        {
            baseCoreHp = data.baseCoreHp,
            resourceState = new ResourceRuntimeState { wood = data.wood, scrap = data.scrap, food = data.food },
            playerHp = data.playerHp,
            playerHunger = data.playerHunger,
            hasPlayerPose = data.hasPlayerPose,
            playerPosition = data.playerPosition,
            playerYaw = data.playerYaw
        };

        Dictionary<string, FenceSlot> fenceSlotsById = new Dictionary<string, FenceSlot>();
        foreach (FenceSlot slot in FindObjectsByType<FenceSlot>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!string.IsNullOrEmpty(slot.PersistentSlotId))
            {
                fenceSlotsById[slot.PersistentSlotId] = slot;
            }
        }

        foreach (FenceSave fence in data.fences)
        {
            FenceData fenceData = fence.hasFence && fenceSlotsById.TryGetValue(fence.id, out FenceSlot slot)
                ? slot.FenceDataForTier(fence.tier)
                : null;
            checkpoint.fenceSlots[fence.id] = new FenceCheckpoint
            {
                hasFence = fenceData != null,
                fenceData = fenceData,
                currentHp = fence.hp
            };
        }

        foreach (TowerSave tower in data.towers)
        {
            checkpoint.towerSlots[tower.id] = new TowerCheckpoint { hasTower = tower.hasTower, currentHp = tower.hp };
        }

        foreach (NodeSave node in data.depletedNodes)
        {
            checkpoint.depletedResourceNodes[node.id] = node.respawnDay;
        }

        return checkpoint;
    }

    /// <summary>Restores the checkpoint captured by CaptureDayCheckpoint(). No-op if none was ever captured.</summary>
    private void RestoreDayCheckpoint()
    {
        if (lastDayCheckpoint == null)
        {
            return;
        }

        if (boundBaseCore != null)
        {
            boundBaseCore.SetCurrentHp(lastDayCheckpoint.baseCoreHp);
        }

        ResourceManager resourceManager = GetOrFindResourceManager();
        if (resourceManager != null)
        {
            resourceManager.SetAmount(ResourceType.Wood, lastDayCheckpoint.resourceState.wood);
            resourceManager.SetAmount(ResourceType.Scrap, lastDayCheckpoint.resourceState.scrap);
            resourceManager.SetAmount(ResourceType.Food, lastDayCheckpoint.resourceState.food);
        }

        if (boundPlayerHealth != null)
        {
            boundPlayerHealth.SetHealth(Mathf.RoundToInt(lastDayCheckpoint.playerHp));
        }

        HungerSystem hungerSystem = FindFirstObjectByType<HungerSystem>();
        if (hungerSystem != null)
        {
            hungerSystem.SetHunger(lastDayCheckpoint.playerHunger);
        }

        FenceSlot[] fenceSlots = FindObjectsByType<FenceSlot>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < fenceSlots.Length; i++)
        {
            FenceSlot slot = fenceSlots[i];
            string id = slot.PersistentSlotId;
            if (string.IsNullOrEmpty(id) || !lastDayCheckpoint.fenceSlots.TryGetValue(id, out FenceCheckpoint snapshot))
            {
                continue;
            }

            slot.RestoreFenceInternal(snapshot.hasFence ? snapshot.fenceData : null, snapshot.currentHp);
        }

        TowerSlot[] towerSlots = FindObjectsByType<TowerSlot>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < towerSlots.Length; i++)
        {
            TowerSlot slot = towerSlots[i];
            string id = slot.PersistentSlotId;
            if (string.IsNullOrEmpty(id) || !lastDayCheckpoint.towerSlots.TryGetValue(id, out TowerCheckpoint snapshot))
            {
                continue;
            }

            slot.RestoreTowerInternal(snapshot.hasTower, snapshot.currentHp);
        }

        resourceNodeRegistry.RestoreState(lastDayCheckpoint.depletedResourceNodes);

        LogTransition($"Restored day checkpoint: baseHp={lastDayCheckpoint.baseCoreHp:0.#}.");
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
