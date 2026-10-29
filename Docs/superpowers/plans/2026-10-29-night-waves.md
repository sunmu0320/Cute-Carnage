# 밤 웨이브 개편 — 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 밤을 "끊기지 않는 스트림 + 시간 기반 웨이브 + 땅에서 나오는 좀비 + 접근 알람"으로 바꾸고, 동시 수백 마리에서도 끊기지 않게 한다.

**Architecture:** `SimpleZombieSpawner`가 밤 시계(경과 초) 하나로 스트림과 웨이브를 모두 굴린다. `NightSchedule`은 밤마다 스트림 설정 + 웨이브 포인트 목록(앞 웨이브부터 대기 초)만 가진다. 좀비는 스폰 직후 `Zombie.Initialize(...)`로 종류·체력 배율·소속 웨이브·등장 여부를 받는다. HUD와 알람은 전부 이벤트로 갱신한다.

**Tech Stack:** Unity 6 (URP), C# (Assembly-CSharp, asmdef 없음), unity CLI(`eval`/`eval_file`)로 Play Mode 자동 확인.

**Spec:** `Docs/superpowers/specs/2026-10-27-night-waves-design.md` (승인됨 2026-10-29). 실행자는 계획과 함께 반드시 읽는다.

## 진행 방식 [사용자 요청]

- **한 번에 한 단계(Phase)만** 진행한다. 단계가 끝나면 Claude가 Play Mode 자동 확인을 하고, **사용자가 직접 플레이해서 확인**한 뒤에 커밋하고 다음 단계로 간다.
- 단계마다 끝에 "사용자 확인 체크리스트"가 있다. 사용자가 OK하기 전에는 다음 단계 코드를 쓰지 않는다.
- 각 단계는 그 자체로 플레이 가능한 상태로 끝나야 한다 (중간에 밤이 깨진 채로 커밋 금지).

## Global Constraints

- 규칙 7: 밤은 **마지막 웨이브 포인트까지 전부 스폰 + 그날 밤 스폰된 좀비(보스 포함) 전부 처치**일 때만 끝난다. `SimpleZombieSpawner.CompleteNight()` → `GameManager.TransitionToDay()`가 유일한 경로. 시간으로 밤을 끝내는 코드 금지.
- 규칙 6: UI는 값이 바뀔 때만 갱신. 예외는 화면 가장자리 인디케이터의 **위치** 갱신뿐 (표시 전용, 코드 주석에 예외 명시).
- PersistentId / 슬롯 구조 / `ZombieType` 기존 값(Basic=0, Runner=1, Breaker=2, Tank=3) 변경 금지. 새 값은 **뒤에만** 추가.
- 밤 진행 상태는 저장하지 않는다. Retry는 기존 `GameManager.RetryCurrentDay()` 경로 그대로.
- Basic 이동속도 1.3m/s (이미 반영, `BasicZombieData.asset`).
- 수치 초안: 동시 생존 상한 300 (2026-10-29 사용자 결정, Phase 1 측정 기준), 숨 돌리기 = 웨이브 80% 처치 → 10초 동안 스트림 30%, 체력 +15%/일(복리), 알람 = 무리 5마리 진입, 점프 쿨다운 3초 / 웅크림 0.5초.
- 레이어: Default 0, Ignore Raycast 2, Interactable 6, Zombie 7, VFXVolume 8.
- Play Mode 테스트 전 `%USERPROFILE%/AppData/LocalLow/DefaultCompany/Cute Carnage/save.json` 백업, 끝나면 복원. `capture_game_view` 결과는 `Assets/` 밖으로 옮기거나 지운다.
- `Day.unity` 저장 시 UIRoot RectTransform driven 속성 0 diff는 무해 (CLAUDE.md 참고). 그 외 diff만 확인.

## 확인 방법 (이 프로젝트에는 테스트 어셈블리가 없음)

- **순수 계산 함수**(웨이브 시각 누적, 체력 배율, 스트림 간격)는 `NightSchedule`의 `static` 함수로 두고, `Assets/Editor/NightWaveChecks.cs`의 메뉴 `Tools/Cute Carnage/Run Night Wave Checks`에서 assert로 확인한다. 실패하면 `Debug.LogError("[NightWaveChecks] FAIL ...")`, 성공하면 `"[NightWaveChecks] PASS (n checks)"`. unity CLI로 메뉴를 실행해 로그를 본다.
- **동작**은 unity CLI `eval_file`로 Play Mode에서 확인한다 (예: 밤 강제 시작 → N초 진행 → 로그/카운트 확인).
- **느낌**(속도, 연출, 알람 타이밍)은 사용자 플레이로만 확인한다.

## Review Focus

1. **밤 중간 GameOver → Retry:** 남은 좀비 Destroy 후 `AliveZombieCount`, 웨이브별 처치 수, 숨 돌리기 상태, 알람 카운트, 화면 인디케이터가 전부 0/숨김으로 돌아가야 한다. 다음 밤이 정상 시작·종료해야 한다. → Task 2.3, 5.2 확인 단계에 포함.
2. **동시 생존 상한에 막힌 채 웨이브 시각이 지나감:** 밀린 스폰이 사라지지 않고 결국 전부 스폰되며, 그 전에는 밤이 끝나지 않아야 한다. → Task 2.3 확인 (상한을 5로 낮춰서 테스트).
3. **스폰 자리 확인이 계속 실패** (건물·물·맵 끝): 무한 루프 없이 그 프레임은 건너뛰고 다음 프레임에 다시 시도, 스폰 수는 줄지 않음, 경고는 한 번만. → Task 2.2 확인.
4. **등장 중에 죽은 좀비:** `Died`가 한 번만 발화, 일어서기가 이어서 재생되지 않음, 웨이브 처치 수에 정확히 반영. → Task 4.2 확인.
5. **일시정지(timeScale 0)·GameOver 정지 중:** 밤 시계, 스트림, 숨 돌리기 타이머, 등장 중인 좀비가 모두 멈춰야 한다 (`SetPaused`, `SetFrozen` 존중). → Task 2.3, 4.2 확인.

---

## Phase 1 — 성능 정리 (대규모 웨이브 전에 먼저)

### Task 1.1: 좀비 앞 감지 비용 줄이기 + 갱신 타이밍 흩뜨리기

**Files:**
- Modify: `Assets/Scripts/Enemies/Zombie.cs` (`Awake`, `Start`, `SelectFrontTarget`, `TakeDamage`, `Die`)
- Modify: `Assets/Prefabs/Enemy/BasicZModel.prefab` (Animator `m_CullingMode` 0 → 1)

**Interfaces:**
- Produces: 없음 (내부 변경)

- [ ] **Step 1:** `Awake`에서 `forwardDetectMask &= ~LayerMask.GetMask("Zombie");` — 다른 좀비는 타겟이 될 수 없는데 버퍼(64칸)를 채우고 있었다.
- [ ] **Step 2:** `SelectFrontTarget` 루프에서 `ClosestPoint` 전에 `if (col is TerrainCollider) continue;` 그리고 `zombie != null`이면 (자기 자신이든 아니든) `continue`.
- [ ] **Step 3:** `Start`에서 `SelectFrontTarget()` 뒤에 `targetRefreshTimer = Random.Range(0f, Mathf.Max(0.1f, targetRefreshInterval));` — 같은 프레임 스폰 좀비들이 동시에 갱신하지 않게.
- [ ] **Step 4:** `TakeDamage`/`Die`의 매 피격 `Debug.Log` 제거 (수백 마리에서 로그만으로 프레임 소모).
- [ ] **Step 5:** `BasicZModel.prefab` Animator Culling Mode = Cull Update Transforms (화면 밖 애니메이션 컬링).
- [ ] **Step 6: 확인** — Play Mode에서 펜스·타워·플레이어·베이스 공격이 전과 같이 되는지 (좀비 10마리 수동 스폰, 각 타겟 앞에서 공격 로그 확인).

### Task 1.2: 자동전투의 전체 검색 제거

**Files:**
- Modify: `Assets/Scripts/Enemies/Zombie.cs` (static 목록)
- Modify: `Assets/Scripts/Player/PlayerAutoCombat.cs:87-110` (`FindNearestZombieInRange`)

**Interfaces:**
- Produces: `public static IReadOnlyList<Zombie> Active` — `OnEnable`에서 추가, `OnDisable`에서 제거. 죽은 좀비(5초간 남음)도 들어 있으므로 사용하는 쪽에서 `IsDead` 거른다.

- [ ] **Step 1:** `Zombie`에 `private static readonly List<Zombie> active` + `Active` 프로퍼티.
- [ ] **Step 2:** `FindNearestZombieInRange`가 `FindObjectsByType` 대신 `Zombie.Active`를 순회.
- [ ] **Step 3: 확인** — 자동전투가 가장 가까운 좀비를 그대로 공격하는지.

### Task 1.3: 측정

- [ ] **Step 1:** 2026-10-27과 같은 방식(에디터 Play Mode, 300/500마리 걷기)으로 평균·상위 5% 프레임 측정. 추가로 **타워·플레이어가 싸우는 상태**에서 500마리 측정.
- [ ] **Step 2:** 결과를 스펙 §8 표에 추가. 목표: 전투 중 상위 5% ≤ 25ms.
- [ ] **Step 3: 풀링 판단** — 스펙 §8-4 풀링은 측정에서 스폰/사망 순간 프레임 튐(Instantiate/Destroy)이 보일 때만 진행할 것을 **사용자에게 제안**하고 결정을 받는다 (승인된 항목이므로 Claude 혼자 빼지 않는다). 하기로 하면 Phase 10 앞에 별도 Task로 추가.

**사용자 확인 체크리스트 (Phase 1):** 밤 1회 플레이 — 좀비 공격·타워 사격·자동전투가 전과 같은가, 체감 끊김이 줄었는가. OK → 커밋 `Perf: zombie detection, auto-combat registry, animator culling`.

---

## Phase 2 — 밤 흐름 (스트림 + 시간 웨이브 + 숨 돌리기, Basic만)

### Task 2.1: `NightSchedule` 새 구조 + 계산 함수 + 체크 메뉴

**Files:**
- Modify: `Assets/Data/Enemies/NightSchedule.cs` (전체 교체)
- Modify: `Assets/Data/Enemies/ZombieData.cs` (`prefab` 필드, `ZombieType.Boss` 뒤에 추가)
- Create: `Assets/Editor/NightWaveChecks.cs`
- Modify: `Assets/Data/Enemies/NightSchedule.asset` (새 구조로 7밤 임시값)

**Interfaces:**
- Produces (`NightSchedule`):
  - `Compass` enum, `CompassDirection(Compass)` — 기존 그대로.
  - `[Serializable] class SpawnEntry { ZombieData zombie; float weightStart = 1; float weightEnd = 1; }` — 밤 진행에 따라 가중치 보간 (한 밤 안에서 강한 종류 비율 상승).
  - `[Serializable] class Stream { SpawnEntry[] zombies; float intervalStart = 1.5f; float intervalEnd = 0.4f; }`
  - `[Serializable] class Group { ZombieData zombie; int count = 20; bool allAround; Compass from; float arcWidth = 40f; float startDelay; float spawnInterval = 0.2f; }`
  - `[Serializable] class WavePoint { float delayFromPrevious = 60f; Group[] groups; int TotalCount { get; } }`
  - `[Serializable] class Night { Stream stream; WavePoint[] waves; }`
  - `float hpGrowthPerDay = 0.15f` (+ public getter `HpGrowthPerDay`)
  - `Night GetNight(int day)` — 기존 그대로 (마지막 밤 재사용).
  - `static float WaveTime(WavePoint[] waves, int index)` — `delayFromPrevious` 누적 합 (index 0 = 첫 웨이브의 delay).
  - `static float HpMultiplier(float growthPerDay, int day)` — `Mathf.Pow(1f + growthPerDay, Mathf.Max(0, day - 1))`.
  - `static float StreamInterval(Stream s, float progress01)` — `Mathf.Lerp(intervalStart, intervalEnd, progress01)`.
  - `static ZombieData PickStreamZombie(Stream s, float progress01, float random01)` — 보간 가중치로 선택.
- Produces (`ZombieData`): `public GameObject prefab;` (null이면 스포너 기본 프리팹), `ZombieType.Boss` (값 4).

- [ ] **Step 1: 체크 먼저** — `NightWaveChecks` 메뉴에 다음 assert:
  - `WaveTime([20,40,60], 0) == 20`, `(…,1) == 60`, `(…,2) == 120`
  - `HpMultiplier(0.15f, 1) == 1`, `HpMultiplier(0.15f, 2) ≈ 1.15`, `HpMultiplier(0.15f, 7) ≈ 2.313` (±0.001)
  - `StreamInterval({1.5, 0.4}, 0) == 1.5`, `(…, 1) == 0.4`, `(…, 0.5) ≈ 0.95`
  - `PickStreamZombie`: 가중치 A(1→0), B(0→1)일 때 progress 0에서 항상 A, 1에서 항상 B
- [ ] **Step 2:** 메뉴 실행 → 컴파일 에러로 실패 확인.
- [ ] **Step 3:** `NightSchedule` 구현. 클래스 주석에 "웨이브 시각 = 앞 웨이브부터 대기 초의 누적" 명시.
- [ ] **Step 4:** 메뉴 실행 → `PASS`.
- [ ] **Step 5:** `NightSchedule.asset` 임시값 — 7밤 모두 Basic만, 각 밤 약 5분: 스트림 1.5→0.4초, 웨이브 3개 (예: 40/90/140초 → 40·130·270초), 그룹은 한 방향 무리 20~60마리. 수치는 Phase 10에서 다시 잡는다.

### Task 2.2: 스폰 자리 찾기 (바닥 확인 + 링 두 개)

**Files:**
- Modify: `Assets/Scripts/Systems/SimpleZombieSpawner.cs` (`GetSpawnPosition` 교체)

**Interfaces:**
- Produces: `bool TryFindSpawnPoint(float compassAngleDeg, float minR, float maxR, out Vector3 point)`
- Inspector: 기존 `minSpawnRadius`/`maxSpawnRadius` = **스트림 링** (가까움~화면 밖), 새 `waveMinSpawnRadius = 45f`/`waveMaxSpawnRadius = 60f` = **웨이브 링** (멀리). Gizmo에 두 링 다 표시 (스트림 노랑/빨강, 웨이브 청록).

- [ ] **Step 1:** 시도 `maxSpawnPositionAttempts`(기존 4)번: 반경 무작위 → 위 50m에서 아래로 Raycast (마스크: Zombie·VFXVolume·Ignore Raycast·UI 제외, 트리거 무시). **맞은 게 `TerrainCollider`일 때만** 후보. 후보 위치에 `OverlapCapsuleNonAlloc`(바닥 +0.5 ~ +1.8m, 반경 0.45) → `TerrainCollider` 외의 콜라이더가 하나라도 있으면 탈락.
- [ ] **Step 2:** 전부 실패하면 `false`, 경고는 처음 한 번만. 호출하는 쪽은 그 스폰을 **다음 프레임으로 미룬다** (수 감소 없음).
- [ ] **Step 3: 확인** — Play Mode에서 1000번 호출, 결과 지점이 전부 지형 위이고 바위·건물·펜스와 겹치지 않는지 (`Physics.CheckCapsule` 재확인). 링을 건물 한가운데로 일부러 좁혀 실패 경로에서 무한 루프·카운트 손실이 없는지.

### Task 2.3: 스포너를 밤 시계 기반으로 교체

**Files:**
- Modify: `Assets/Scripts/Systems/SimpleZombieSpawner.cs` (`WaveState` 상태기계 → 밤 시계)
- Modify: `Assets/Scripts/Enemies/Zombie.cs` (`Initialize`, `WaveIndex`, `Died` 시그니처)

**Interfaces:**
- Consumes: Task 2.1 `NightSchedule` API, Task 2.2 `TryFindSpawnPoint`.
- Produces (`Zombie`):
  - `public void Initialize(ZombieData data, float hpMultiplier, int waveIndex, bool emerge)` — `ApplyZombieData` 다시 적용 후 `maxHp *= hpMultiplier`, `currentHp = maxHp`. `emerge`는 Phase 4 전까지 무시.
  - `public int WaveIndex { get; }` — 스트림 = -1.
  - `public static event Action<Zombie> Died` (기존 `Action`에서 변경, 구독자는 스포너뿐).
- Produces (`SimpleZombieSpawner`):
  - `float NightElapsed`, `float NightDuration` (= 마지막 웨이브 `WaveTime`), `float NightProgress` (= `Clamp01(NightElapsed / NightDuration)`)
  - `int WaveCount`, `int WavesStarted`, `float WaveTimeAt(int i)`, `float SecondsToNextWave` (남은 웨이브 없으면 -1), `bool AllSpawned`
  - `event Action OnNightProgressChanged` — **경과 초의 정수 부분이 바뀔 때 1번** + 웨이브 시작 + 마지막 웨이브 이후 처치 때
  - `event Action<int> OnWaveStarted`
  - Inspector: `maxAlive = 300`, `restKillFraction = 0.8f`, `restDuration = 10f`, `restStreamFactor = 0.3f`. `initialWaveStartDelay`/`interWaveDelay`/`postClearDelay`는 삭제.

- [ ] **Step 1: 흐름 구현** (`Update`, `isPaused`면 아무것도 안 함):
  - `NightElapsed += Time.deltaTime`.
  - 웨이브 시작: `NightElapsed >= WaveTimeAt(WavesStarted)`이면 그 웨이브를 활성 목록에 넣고 `OnWaveStarted`. 앞 웨이브 상태와 무관 (겹침 허용).
  - 활성 웨이브의 그룹마다: `startDelay` 후 `spawnInterval`마다 1마리, **웨이브 링**에서, `allAround`면 360도, 아니면 `from` ± `arcWidth/2`.
  - 스트림: 마지막 웨이브의 스폰이 끝나기 전까지 `StreamInterval(stream, NightProgress)` 간격 (숨 돌리기 중엔 간격 ÷ `restStreamFactor`), **스트림 링**, 종류는 `PickStreamZombie`.
  - 상한: `Zombie.AliveZombieCount >= maxAlive`면 그 프레임 스폰 전부 미룸 (웨이브·스트림 타이머는 그대로 → 밀린 만큼 나중에 연속 스폰).
  - 모든 스폰은 `Instantiate(data.prefab ?? zombiePrefab)` 뒤 `Initialize(data, HpMultiplier(day), waveIndex, emerge: false)`.
- [ ] **Step 2: 숨 돌리기** — `Died(z)`에서 `z.WaveIndex >= 0`이면 그 웨이브 처치 수 +1. 처치 수 ≥ `TotalCount * restKillFraction`이 되는 순간 (웨이브당 1번) `restUntil = NightElapsed + restDuration`.
- [ ] **Step 3: 밤 종료** — `AllSpawned`(모든 웨이브 그룹 전부 스폰) 이고 `AliveZombieCount == 0`이면 기존 `CompleteNight()`. 다른 종료 경로 없음.
- [ ] **Step 4:** `ResetForNewNight()`가 시계·활성 웨이브·처치 수·`restUntil`·미룬 스폰을 전부 초기화.
- [ ] **Step 5: 걷기 애니메이션 속도 맞추기** — `BasicZombieAnimator.controller`에 float 파라미터 `WalkSpeedMul` 추가, Walk 상태의 Speed Multiplier로 연결. `Zombie.Initialize`에서 **한 번만** `animator.SetFloat("WalkSpeedMul", moveSpeed / walkAnimReferenceSpeed)` (`ZombieData.walkAnimReferenceSpeed` 기본 0.65 — 사용자와 보면서 조정).
- [ ] **Step 6: 확인 (Play Mode, 자동)**
  - 밤 강제 시작 → 10초 안에 첫 좀비가 펜스에 닿는지 (스트림 링 값은 사용자가 정한 값 기준).
  - 웨이브 시각에 정확히 `OnWaveStarted` (로그 시각 ±0.1초), 앞 웨이브 생존 중에도 다음 웨이브 시작.
  - 80% 처치 시 10초간 스트림 간격이 ÷0.3 되는지.
  - `maxAlive = 5`로 낮추고: 밀린 스폰이 결국 다 나오고, 그 전엔 밤이 안 끝나는지 (Review Focus 2).
  - 밤 중 GameOver → Retry → 다음 밤 정상 종료 (Review Focus 1). 일시정지 중 `NightElapsed` 정지 (Review Focus 5).
  - 2일차 Basic HP = 30 × 1.15.

**사용자 확인 체크리스트 (Phase 2):** 밤 1~2회 플레이 — 바로 전투가 시작되는가, 계속 몰려오는가, 웨이브 때 확 많아지는가, 숨 돌리기가 느껴지는가, 좀비 걸음이 미끄러지지 않는가, 약 5분에 끝나는가. (다이얼은 아직 예전 표시 — Phase 3에서 교체) OK → 커밋.

---

## Phase 3 — 다이얼 HUD (시간 기반)

### Task 3.1: `DayNightHUD` 밤 표시 교체

**Files:**
- Modify: `Assets/Scripts/UI/DayNightHUD.cs` (`RefreshNight`, `SetDividerCount` → 마커 배치)

**Interfaces:**
- Consumes: Task 2.3 `NightProgress`, `WaveCount`, `WaveTimeAt`, `NightDuration`, `WavesStarted`, `SecondsToNextWave`, `AllSpawned`, `OnNightProgressChanged`.

- [ ] **Step 1:** 밤 진행도 = `spawner.NightProgress` (달 위치·게이지). 갱신은 기존처럼 `OnNightProgressChanged`에서만 — 초 단위로 오므로 `Update` 추가 없음 (스펙 §3의 표시 예외가 필요 없어짐).
- [ ] **Step 2:** 마커: 웨이브마다 `WaveTimeAt(i) / NightDuration` 위치 (기존 균등 분할 대신). 마지막 웨이브는 게이지 끝(1.0)이므로 표시하지 않는다. 배치는 밤 시작 때 한 번만. 그 웨이브 그룹에 Tank/Boss가 있으면 `waveMarkerMajorColor`(Inspector, 기본 빨강)로 색만 바꾼다.
- [ ] **Step 3:** 상세 텍스트: 남은 웨이브가 있으면 `WAVE {WavesStarted + 1} · m:ss` (= `SecondsToNextWave` 올림), 다 시작했으면 `ZOMBIES LEFT {AliveZombieCount}`.
- [ ] **Step 4: 확인** — 웨이브 시각에 달이 마커를 지나는지, 텍스트가 1초마다 줄고 웨이브 시작 즉시 다음 웨이브 남은 시간으로 바뀌는지, 게이지가 리셋·정지 없이 흐르는지. 캡처로 확인.

**사용자 확인 체크리스트 (Phase 3):** 다이얼만 보고 "다음 웨이브까지 얼마 남았는지" 알 수 있는가. OK → 커밋.

---

## Phase 4 — 등장 연출 (화면 안 스트림 좀비만)

### Task 4.1: Getting Up 클립 + 등장 상태

**Files:**
- Modify: `Assets/Animations/ZombiePack/Getting Up.fbx.meta` (Rig Humanoid — 기존 좀비 FBX들과 같은 Avatar 설정, Loop off, 클립 0~170프레임)
- Modify: `Assets/Animations/ZombiePack/BasicZombieAnimator.controller` (`Emerge` 상태, Speed 1.8, 끝나면 Walk/Idle로)
- Modify: `Assets/Scripts/Enemies/Zombie.cs`

**Interfaces:**
- Produces: `public bool IsEmerging { get; }`. `Initialize(..., emerge: true)`이면: `animator.Play("Emerge", 0, 0f)`, 위치를 `emergeSinkDepth`(0.6m)만큼 내렸다가 `emergeDuration`(3.5초) 앞 40% 동안 원래 높이로 올림, 그동안 이동·공격·타겟 갱신 안 함. 맞을 수는 있음.

- [ ] **Step 1:** 등장 중 `Update`는 높이 보간과 타이머만, `isFrozen`이면 타이머도 정지.
- [ ] **Step 2:** 등장 중 죽으면 `Die()` 그대로 (사망 트리거가 Emerge를 덮음), 등장 타이머 중단.
- [ ] **Step 3:** 타워·자동전투가 등장 중인 좀비를 타겟으로 잡는지 확인 (땅에 묻힌 높이에서도 콜라이더가 맞는지).

### Task 4.2: 땅 솟음 + 먼지 이펙트, 화면 안 판정

**Files:**
- Create: `Assets/Scripts/Enemies/ZombieEmergeFX.cs`
- Create: `Assets/Prefabs/Systems/ZombieEmergeFX.prefab` (자식 `Debris`, `Dust` ParticleSystem, 둘 다 World space, Play On Awake off)
- Modify: `Assets/Scripts/Systems/SimpleZombieSpawner.cs` (스트림 스폰 때 판정·호출)

**Interfaces:**
- Produces: `ZombieEmergeFX.Play(Vector3 position, float scale)` — 이펙트 오브젝트를 만들지 않고 공용 ParticleSystem 두 개에 `Emit(EmitParams{position}, count)`만 한다 (풀링 불필요).
- Spawner Inspector: `ZombieEmergeFX emergeFX`, `float onScreenMargin = 0.05f`.

- [ ] **Step 1:** `Debris`: Mesh 렌더 모드, 흙·돌 조각 메시 2~3개(`Stone1`/`Stone2` 메시 축소 재사용 가능), `TL_Dirt` 톤, 위로 2~4m/s, 중력, 수명 ~1.2초, 끝에 작아짐, 1회 8~12개.
- [ ] **Step 2:** `Dust`: 기존 `NightMist.shader` 같은 soft particle, 수명 ~1.5초, 1회 6~10개.
- [ ] **Step 3:** 스포너: 스트림 스폰 위치가 `Camera.main` 뷰포트 안(여백 포함, z > 0)이면 `emerge: true` + `emergeFX.Play`, 아니면 `emerge: false`. 웨이브 그룹·Tank·Boss는 항상 `false`. 판정은 스폰 순간 1번.
- [ ] **Step 4: 확인** — 화면 안 스폰은 땅에서 솟고 3.5초 뒤 걷기, 화면 밖 스폰은 바로 걷기. 등장 중 처치 시 `Died` 1번, 웨이브 처치 수 정확 (Review Focus 4). 일시정지 중 등장도 정지 (Review Focus 5). 등장 100마리 동시 프레임 측정.

**사용자 확인 체크리스트 (Phase 4):** 땅이 솟는 느낌이 나는가, 너무 길거나 짧지 않은가 (`emergeDuration`, 클립 속도, 조각 수 조정), 화면이 지저분하지 않은가. OK → 커밋.

---

## Phase 5 — 웨이브 접근 알람 (`!`)

### Task 5.1: 화면 가장자리 인디케이터 (공용)

**Files:**
- Create: `Assets/Scripts/UI/EdgeIndicatorUI.cs`
- Modify: `Assets/Prefabs/UI/UIRoot.prefab` (HUDCanvas 아래 `EdgeIndicators` + 아이콘 템플릿, 비활성)

**Interfaces:**
- Produces: `int Show(Vector3 worldPoint, Sprite icon, Color color)` → 핸들, `void Hide(int handle)`, `void SetPoint(int handle, Vector3 worldPoint)`. 보이는 동안만 `LateUpdate`에서 아이콘을 화면 가장자리(여백 40px)에 고정 — **규칙 6 표시 예외, 주석 명시**. 표시 중인 게 없으면 `enabled = false`.
- 대상 지점이 화면 안이면 아이콘을 숨긴다 (핸들은 유지).

### Task 5.2: 베이스 둘레 트리거 알람

**Files:**
- Create: `Assets/Scripts/Systems/BaseApproachAlarm.cs`
- Modify: `Assets/Prefabs/Interactables/HomeBase.prefab` (자식 `ApproachAlarm`: SphereCollider 트리거, 반경 에디터 조정, kinematic Rigidbody, 레이어 Ignore Raycast — `HomeFogZone`과 같은 방식)

**Interfaces:**
- Consumes: `Zombie.WaveIndex`, `SimpleZombieSpawner.OnWaveStarted`, Task 5.1 `EdgeIndicatorUI`.
- Produces: `event Action<int, Vector3> HordeApproaching` (웨이브 인덱스, 진입 지점 평균).
- Inspector: `int alarmThreshold = 5`, `float maxShowSeconds = 10f`, `Sprite alarmIcon`.

- [ ] **Step 1:** `OnTriggerEnter`: `GetComponentInParent<Zombie>()`, `WaveIndex < 0`이면 무시. 웨이브별 진입 수·위치 합 누적, `alarmThreshold` 도달 순간 (웨이브당 1번) `HordeApproaching` + `EdgeIndicatorUI.Show(평균 지점, alarmIcon, 노랑)`.
- [ ] **Step 2:** 숨김: 그 웨이브 80% 처치(스포너 숨 돌리기 시작 시점 — 스포너에 `event Action<int> OnWaveMostlyCleared` 추가) 또는 `maxShowSeconds` 경과. 화면 안이면 Task 5.1이 자동 숨김.
- [ ] **Step 3:** 새 밤·Retry 때 진입 수와 표시 전부 초기화 (Review Focus 1).
- [ ] **Step 4: 확인** — 스트림 좀비로는 안 울림, 웨이브 무리로 1번만 울림, 방향이 맞음, Zombie 레이어 ↔ Ignore Raycast 충돌 매트릭스에서 트리거가 실제 발화하는지.

**사용자 확인 체크리스트 (Phase 5):** 알람이 "지금 저쪽으로 가야 한다"를 알려 주는 타이밍인가 (반경 조정). OK → 커밋.

---

## Phase 6 — 피격 방향 빨간 인디케이터

### Task 6.1

**Files:**
- Modify: `Assets/Scripts/Interaction/FenceSegment.cs`, `Assets/Scripts/Towers/ArrowTower.cs` (피해 받을 때 이벤트)
- Create: `Assets/Scripts/UI/StructureHitIndicator.cs`

**Interfaces:**
- Produces: `public static event Action<Component, Vector3> StructureDamaged` — `FenceSegment.TakeDamage`(:96), `ArrowTower.TakeDamage`(:99)에서 발화.
- Consumes: Task 5.1 `EdgeIndicatorUI` (빨강).

- [ ] **Step 1:** 구조물별 핸들 1개. 맞을 때마다 `hideAt = now + 2초`로 연장, 시간이 지나면 `Hide`. 화면 안이면 Task 5.1이 숨김.
- [ ] **Step 2: 확인** — 화면 밖 펜스가 맞으면 그 방향 빨강, 계속 맞으면 유지, 멈추면 2초 뒤 사라짐.

**사용자 확인 체크리스트 (Phase 6):** 화면 밖 위기를 알아챌 수 있는가. OK → 커밋.

---

## Phase 7 — 브레이커 · 탱커

### Task 7.1

**Files:**
- Modify: `Assets/Data/Enemies/ZombieData.cs` (`float fenceDamageMultiplier = 1f`)
- Modify: `Assets/Scripts/Enemies/Zombie.cs` (펜스 공격 시 배율 적용)
- Create: `Assets/Data/Enemies/BreakerZombieData.asset`, `TankZombieData.asset`, `Assets/Prefabs/Enemy/TankZombieRoot.prefab` (Basic 프리팹 Variant, 스케일 1.4)

- [ ] **Step 1:** 브레이커 = 보통 속도, `fenceDamageMultiplier` 3 (초안). 직진 중 만난 펜스 공격은 현재 동작 그대로라 행동 코드 추가 없음.
- [ ] **Step 2:** 탱커 = 느림(0.7), HP 높음(Basic × 10 초안), 웨이브 그룹에서만 사용 (스트림 엔트리에 넣지 않는다 — 스포너는 Tank/Boss를 항상 웨이브 링·등장 연출 없음으로 처리).
- [ ] **Step 3: 확인** — 브레이커가 펜스를 약 3배 빨리 부수는지, 탱커가 멀리서만 나오는지.

**사용자 확인 체크리스트 (Phase 7):** 4·5일차 느낌. OK → 커밋.

---

## Phase 8 — 점프형 (Runner)

**사전 조건:** 사용자가 Mixamo 점프(도약)·착지 애니메이션을 `Assets/Animations/ZombiePack/`에 넣어야 한다.

### Task 8.1

**Files:**
- Create: `Assets/Scripts/Enemies/ZombieLeaper.cs`
- Modify: `Assets/Scripts/Enemies/Zombie.cs` (이동 단계에서 `ZombieLeaper`가 있으면 먼저 맡김)
- Create: `Assets/Data/Enemies/RunnerZombieData.asset`, `Assets/Prefabs/Enemy/RunnerZombieRoot.prefab`

**Interfaces:**
- Consumes: `Zombie`의 이동/공격 함수 — `ZombieLeaper.TryTakeOver(Zombie z) : bool` (true면 이번 프레임 일반 이동·공격 건너뜀).
- Inspector: `playerLeapRange = 8f`, `towerLeapRange = 10f`, `hopDistance = 4f`, `crouchTime = 0.5f`, `leapCooldown = 3f`, `leapHeight = 1.8f`.

- [ ] **Step 1:** 판단 순서 (쿨다운이 끝났을 때만): ① 플레이어가 `playerLeapRange` 안 → 플레이어에게 점프 + 착지 순간 1회 공격 → 쿨다운 동안 일반 이동(걸어서 따라옴). ② 타워가 `towerLeapRange` 안 → 타워 앞(타워 반경 + 0.8m, 바닥 확인은 `TryFindSpawnPoint`와 같은 검사) 착지 → 이후 일반 공격(제자리 할퀴기)으로 넘김. ③ 아니면 베이스 방향으로 `hopDistance` 점프 반복.
- [ ] **Step 2:** 점프 = 웅크림 `crouchTime` → 포물선 이동(펜스 충돌 무시, 루트 콜라이더 잠깐 트리거로) → 착지. 점프 중 맞을 수 있음.
- [ ] **Step 3: 확인** — 세 경우 각각, 펜스를 넘어 타워를 때리는지, 착지가 건물 안에 박히지 않는지, 점프 중 사망 시 `Died` 1번.

**사용자 확인 체크리스트 (Phase 8):** 3일차에 "뛰어다녀야 한다"는 느낌. OK → 커밋.

---

## Phase 9 — 보스 (7일차)

### Task 9.1

**Files:**
- Create: `Assets/Scripts/Enemies/ZombieBoss.cs`, `Assets/Scripts/Enemies/VomitProjectile.cs`
- Create: `Assets/Data/Enemies/BossZombieData.asset` (`zombieType = Boss`), `Assets/Prefabs/Enemy/BossZombieRoot.prefab` (Basic Variant, 스케일 2.5 — 임시 외형)

**Interfaces:**
- Inspector: `vomitRange = 14f`, `vomitCooldown = 4f`, `vomitDamage`, 근접은 `ZombieData.attackInterval`을 길게(2.5초) + `attackDamage` 크게.

- [ ] **Step 1:** 사거리 안의 펜스·타워·플레이어 중 가장 가까운 것에 투사체 (바닥 웅덩이 없음). 근접은 기존 공격 경로.
- [ ] **Step 2:** 7일차 마지막 웨이브에 보스 그룹 1마리 (웨이브 링). 보스도 `AliveZombieCount`에 포함 → 기존 Victory 흐름.
- [ ] **Step 3: 확인** — 보스 처치 전엔 7일차 밤이 안 끝남, 처치 후 "YOU SURVIVED 7 NIGHTS".

**사용자 확인 체크리스트 (Phase 9):** 보스가 "다 같이 집중해야 잡히는" 수준인가. OK → 커밋.

---

## (Phase 9와 10 사이) 맵 축소 — 별도 설계 [사용자 결정, 2026-10-29]

전투 메카닉(Phase 3~9)을 끝낸 뒤, Phase 10 밸런스 전에 맵 축소를 별도 설계·작업으로 진행한다. 밸런스(이동 거리, 웨이브 링, 타워 커버리지)가 맵 크기에 묶여 있기 때문. 축소 후 스포너의 스트림/웨이브 링 반경과 접근 알람 반경을 다시 맞춘다. 자원 노드 PersistentId(CLAUDE.md 규칙 5)는 그 설계에서 다룬다.

---

## Phase 10 — 7일 밸런스 + 최종 측정

- [ ] **Step 1:** `NightSchedule.asset`을 스펙 §6 표대로 채움 (1일 Basic·사방 2웨이브 … 7일 3웨이브+보스, 스트림 엔트리 가중치로 밤 후반 강한 종류 비율 상승).
- [ ] **Step 2:** 7일차 마지막 웨이브에서 동시 생존 수백 마리 + 전투 상태 측정. 25ms 초과면 스펙 §8 "목표 미달 시"대로 `maxAlive`와 마릿수를 줄이고 결과를 스펙에 기록.
- [ ] **Step 3:** CLAUDE.md 현재 아키텍처 절에 새 밤 구조 요약 (Reported implemented / Verified 구분).

**사용자 확인 체크리스트 (Phase 10):** 1~7일 통 플레이. OK → 커밋.

---

## 확인 필요 (착수 전 사용자에게)

- ~~플레이어 주변 스폰 금지 반경~~ — 2026-10-29 사용자 결정: **넣지 않는다** (플레이어 바로 옆 스폰 허용). 플레이해 보고 억울하면 다시 논의.
- ~~풀링~~ — 2026-10-29 사용자 결정: **생략** (실제 스폰 속도에서 생성·삭제 튐 없음). 문제가 생기면 그때 추가.
