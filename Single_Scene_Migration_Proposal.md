# Cute Carnage — 단일 씬 마이그레이션 제안서

> **작성 근거:** VS Code Claude Code 조사 3회 + Unity AI 조사 1회 종합
> **상태:** 구현 착수 전 검토용 — 아직 코드 미변경
> **범위:** Day/Night 분리 씬 → 단일 씬(Day.unity 기준) 전환. 콘텐츠 확장(타워/좀비 종류, 마을 전역 스폰)은 별도 마일스톤으로 명시적 제외.

---

## 1. 지금 왜 이 작업을 하는가

기존 구조(별도 Day/Night 씬 + `RunRuntimeState`를 통한 상태 "번역")는 `PROJECT_HANDOFF.md`가 이미 4개의 리스크(#1~#4)로 지목한 근본 원인입니다. 이번 조사로 그중 2개가 실제로 문제였음이 코드로 확인됐습니다(3번 참고). 단일 씬 전환은 이 리스크들을 구조적으로 제거하는 작업이며, 부수적으로 밤 비주얼 시스템(조명/포그)이 애초에 구현된 적이 없다는 사실도 드러났습니다.

---

## 2. 현재 상태 (AS-IS)

### 2.1 씬 전환 메커니즘
- **Day → Night**: `DayTimeManager.OnDayEnded` 이벤트 → `GameManager.HandleDayEnded()` → `TransitionToNight()` → `SceneManager.LoadScene(nightSceneName, LoadSceneMode.Single)`
- **Night → Day**: `SimpleZombieSpawner.CompleteNight()`가 (최종 웨이브 완료 + `Zombie.AliveZombieCount == 0` 확인 후) `GameManager.Instance.TransitionToDay()` 직접 호출
- 씬이 로드/언로드될 때마다 Day 전용/Night 전용 GameObject 세트가 통째로 파괴·재생성됨

### 2.2 상태 유지 방식 — 비대칭 구조
| 단계 | 실행 시점 | 방식 |
|---|---|---|
| Capture | 씬 로드 **직전** | 명령형(imperative), `BeforeLeave*Scene()` → `CaptureRunStateFromScene()` |
| Apply | 씬 로드 **후** | 이벤트형(event-driven), `SceneManager.sceneLoaded` 콜백 → `AfterEnter*Scene()` |

`BaseManager.CaptureState/ApplyState`가 `FenceSlot`/`TowerSlot`을 `PersistentId` 기준으로 매핑해 "Day 씬의 오브젝트 → Night 씬의 다른 오브젝트"로 상태를 번역하는 구조.

### 2.3 확인된 버그 — BaseCore HP가 페이즈 전환 시 소실됨
- `BaseCore.Awake()`가 씬 로드마다 `currentHp = maxHp`로 무조건 리셋
- `BaseManager.CaptureState()`는 `FenceSlot`/`TowerSlot`만 다루고 **BaseCore는 전혀 캡처하지 않음**
- `RunRuntimeState.baseCore` 필드는 존재하나 실제 씬 값으로 채워지는 경로가 없음 (부트스트랩 1회, 200 HP 하드코딩값만 존재)
- **결과: 밤 동안 Base Core가 입은 피해가 다음 Day로 전환되는 순간 전부 사라짐.** 필러 1번("낮의 선택이 밤에 영향을 준다")을 무력화하는 실제 버그.

### 2.4 조명/포그/포스트프로세싱 — 애초에 미구현
Day.unity와 Night.unity의 Directional Light, Baked GI, Post-Processing Volume, Fog 설정이 **전부 byte-identical**이거나 동일하게 비활성 상태. AudioSource/ParticleSystem도 0건. GDD가 "승인된 비주얼 컨셉"으로 분류한 밤 분위기(dark, high-contrast, controlled fog)는 컨셉 아트로만 존재하며 엔진 안에는 구현된 적이 없음.

### 2.5 Night.unity 콘텐츠 실태
`Map_Blockout`(마을 전체, 2,001개 오브젝트) 전체가 Night.unity에는 없음. Night에 남은 건 베이스 구조물 + 좀비 스포너 + 정체불명 리소스 노드 잔존물 9개뿐. 좀비 스포너를 제외하면 Night 전용 콘텐츠는 사실상 없음.

### 2.6 NavMesh / 좀비 이동
Day/Night 둘 다 NavMesh 미구워짐. 더 중요하게, **좀비는 애초에 NavMesh를 쓰지 않고 `Vector3.MoveTowards` 직선 이동만 사용**. `NavMeshAgent` 컴포넌트 자체가 프로젝트에 없음.

### 2.7 Fence/Tower 슬롯 소유권
- **Tower**: 모호함 없음. 설치형 `ArrowTower.prefab` 하나만 존재, 가짜/장식용 타워 프리팹 없음.
- **Fence**: 이중 구조 확인.
  - **진짜 방어 슬롯**: `FenceSegment` + `PersistentId`는 `HomeBase.prefab` 내부에만 존재. `Fence1_Root`/`Fence2_Root`(설치형)는 씬에 정적 배치 0개 — 런타임에 슬롯 위로 동적 생성됨.
  - **장식용 가짜 펜스**: `Fence1`, `Fence2`, `FenceLong`, `FenceMid`, `FenceShort`, `FenceSTRONG`, `FenceGate` 등 수십 개가 Day.unity에 정적 배치돼 있으나 `FenceSlot`/`FenceSegment`/`PersistentId` 전혀 없음. `BaseManager`/`Validator` 둘 다 이 오브젝트들을 인식하지 못함. **지금은 무해**(좀비 공격 대상 아님)하지만 이름이 비슷해 향후 혼동 위험.

### 2.8 리소스 노드 day-only 게이팅
코드(`ResourceNode.CanInteract()`의 `GameManager.Instance.IsDay` 체크) + 배치(Day 씬에 다수, Night 씬엔 9개 잔존) 이중 보장. Day 씬 리소스 노드 총량은 조사마다 224/241로 불일치 — 리소스 시스템을 직접 건드릴 때 재확인 필요(지금은 급하지 않음).

---

## 3. 목표 상태 (TO-BE) — 통합 마이그레이션 (2026-08-13 갱신)

**변경된 원칙**: 애초에 "1차(트리거만 교체) / 2차(capture-apply 파이프라인 제거는 별도)"로 나누려 했으나, 조사 중 확인된 `DontDestroyOnLoad(transform.root)` 스코프 버그(6.1 참고)로 인해 전제가 바뀜. `DontDestroyOnLoad`와 `RunRuntimeState`의 capture/apply 파이프라인은 둘 다 "씬이 파괴·재생성된다"는 전제 위에서만 존재 이유가 있는 코드인데, SetActive 토글 방식(씬 재로드 없음)으로 확정되면서 그 전제 자체가 사라짐. 따라서 **트리거 교체와 함께 이 두 시스템도 같이 제거한다** — 별도 단계로 미루지 않음. 단, 삭제 전 전수조사(6.1) 선행.

### 3.1 씬 구조 — 확정 (2026-08-12 결정)
- `Day.unity`를 메인 씬으로 채택, `Night.unity`는 폐기(콘텐츠가 사실상 없으므로 병합 불필요)
- **BaseRoot 별도 신설 불필요.** `HomeBase.prefab` 계층 구조 확인 결과, `TowerSlots`(TowerSlot01×6)와 `FenceSlots`(FenceSlot×10)가 이미 `HomeBase` 루트 하나 아래 정리되어 있음. 이 프리팹 인스턴스를 그대로 상시 active 상태로 유지하면 됨 — 재구성 불필요.
- **dayRoot 불필요 — 확정.** 자원 노드/마을 콘텐츠(`Map_Blockout`)는 낮이든 밤이든 항상 시각적으로 표시된다(사용자 결정: "자원이나 건물들은 다 보여줘 항상, 밤에도. 자원 파밍을 못할 뿐이지 보이는 건 같아야 한다"). 상호작용 차단은 기존 `ResourceNode.CanInteract()`의 `IsDay` 코드 게이팅으로 이미 충분하므로 별도 SetActive 토글 불필요.
- **nightRoot = 좀비 스포너 하나뿐.** On/Off 토글이 실제로 필요한 유일한 요소는 `SimpleZombieSpawner`(프리팹으로 존재, Day 씬에 추가 배치).

**⚠️ 이 결정의 파생 영향**: 밤 분위기를 낼 수 있는 유일한 수단이 조명/포그/포스트프로세싱으로 좁혀짐(오브젝트를 숨겨서 무드를 만드는 방식을 쓰지 않기로 했으므로). 2.4에서 확인했듯 이 시스템은 현재 완전히 미구현 상태 — 따라서 **4번 "이번 단계 제외 항목"의 밤 조명/포그 작업은 선택이 아니라 이번 마이그레이션 직후 필수 후속 작업으로 격상됨.**

### 3.2 전환 로직
```
GameManager.TransitionToNight()
  BeforeLeaveDayScene()          // 기존 capture 로직 그대로 재사용
  dayRoot.SetActive(false)       // 필요시
  nightRoot.SetActive(true)
  AfterEnterNightScene()         // 기존 OnSceneLoaded 안에서 하던 일을 직접 호출
  OnPhaseChanged?.Invoke(Night)  // 신규 이벤트

GameManager.TransitionToDay()
  BeforeLeaveNightScene()
  nightRoot.SetActive(false)
  dayRoot.SetActive(true)
  AfterEnterDayScene()
  OnPhaseChanged?.Invoke(Day)
```
`SceneManager.LoadScene` 호출 및 `SceneManager.sceneLoaded` 구독 제거(또는 최초 1회 초기화용으로만 남김).

### 3.3 컴포넌트별 변경 사항
| 컴포넌트 | 조치 |
|---|---|
| `GameManager.TransitionToNight/Day` | `LoadScene` → `SetActive` 토글 |
| `GameManager.OnSceneLoaded` | 제거, `OnPhaseChanged` 이벤트로 대체 |
| `GameManager.cs:92-93` `DontDestroyOnLoad(transform.root)` | **삭제** — 씬 재로드 자체가 없어지므로 존재 이유 소멸. 단, 6.1 전수조사에서 다른 용도로 쓰이는 게 없는지 먼저 확인 |
| `BeforeLeave*`/`CaptureRunStateFromScene`, `ApplyRunRuntimeStateToScene` (Day↔Night 전환 목적 부분) | **삭제** — 오브젝트가 파괴되지 않으므로 "번역"할 대상이 없음. 단, 6.1 전수조사에서 부트스트랩/기타 목적으로 쓰이는 부분은 존치 |
| `RunRuntimeState` 부트스트랩 함수 (`BuildDefaultFenceStates`, `TryInitializeDefaultBaseRuntimeState` 등) | 존치 여부는 6.1 전수조사 결과에 따라 결정 — Day1 최초 초기화 목적이면 유지 |
| `PersistentId` / Validator | 변경 불필요 |
| `HUDController.OnSceneLoaded` | 삭제(GameManager의 기존 명령형 호출로 충분 — 현재도 중복 실행 중이었음) |
| `PlayerInteractor.OnSceneLoadedForResources` | 삭제(씬 파괴 없으니 재바인딩 불필요) |
| `SimpleZombieSpawner` | **신규**: `ResetForNewNight()` 추가 필요 — 없으면 두 번째 Night부터 `hasTriggeredDayTransition == true`라서 웨이브가 시작 안 되는 회귀 발생 |
| `DayTimeManager` | **신규**: `OnPhaseChanged`에 명시적 `Pause()`/`Resume()` 및 명시적 구독/해제 필요(기존엔 씬 파괴가 우연히 이 역할을 대신했음, DontDestroyOnLoad 버그 조사에서 확인) |
| HUD ↔ DayTimeManager 바인딩 | Day 씬 수동 와이어링 / Night 씬 auto-resolve라는 비대칭 구조를 없애고, 단일 안정 참조 + `OnPhaseChanged` 기반 갱신으로 통일 |
| `StructureActionPanelUI`(Day 전용) / `NightRepairPanelUI`(Night 전용) 가시성 | **신규**: 지금까지는 "Day 씬엔 전자만, Night 씬엔 후자만 존재"라는 게 씬 분리 자체로 보장됐음(둘 다 같은 씬에 존재한 적 없음). 단일 씬이 되면 두 패널이 동시에 존재하므로, `OnPhaseChanged` 핸들러에서 명시적으로 페이즈에 맞는 패널만 활성화/노출하도록 로직 추가 필요 — DayTimeManager Pause/Resume과 동일한 성격의 "씬 파괴가 우연히 대신 해주던 일" 케이스 |

### 3.4 BaseCore HP 버그 처리
단일 씬 전환으로 BaseCore GameObject가 더 이상 파괴되지 않으므로, **2.3의 버그는 구조적으로 저절로 해소됩니다.** 단, 이를 "우연한 해결"로 방치하지 않고 명시적 회귀 테스트 항목(4.3)으로 검증합니다.

---

## 4. 이번 단계에서 하지 않는 것 (명시적 제외)

1. ~~Capture/Apply 파이프라인 완전 제거 — 별도 세션~~ → **해소, 통합 진행으로 변경.** 3번/6.1 갱신에 따라 이번 마이그레이션에 함께 포함됨(별도 세션 아님). 이유: `DontDestroyOnLoad`와 capture/apply 파이프라인 둘 다 "씬 파괴·재생성" 전제 위에서만 의미가 있었는데 그 전제 자체가 이번 작업으로 사라지므로, 나중으로 미루면 오히려 죽은 코드를 한 번 더 만졌다가 다시 지우는 이중 작업이 됨.
2. **밤 조명/포그/포스트프로세싱 신규 구현** — 애초에 구현된 적 없는 별개 작업(2.4). **이번 단계에서는 제외하지만, dayRoot를 안 쓰기로 한 결정(3.1) 때문에 마이그레이션 직후 반드시 착수해야 하는 최우선 후속 작업으로 격상됨.** 이걸 미루면 낮/밤이 시각적으로 거의 구분 안 되는 상태로 플레이테스트하게 됨.
3. **타워 종류 축소(Ballista+SMG, T1-T3), 좀비 종류 확정(Basic/Runner/Tank)** — 콘텐츠 스코프 조정이며 이번 아키텍처 작업과 무관하게 병행/후행 가능.
4. **마을 전역 밤 스폰 시스템** — 별도 마일스톤. NavMeshAgent 전환(현재 좀비는 NavMesh 미사용, 직선 이동)을 이 마일스톤에 묶어서 진행 예정. **또한 "밤 자원 채집 허용 여부" 딜레마도 이 마일스톤에서 같이 재검토** — 현재는 좀비가 베이스 주변에만 스폰되므로 밤에 자원 채집을 열어도 실질적 위험이 없어 리스크 없는 이득(전략적 선택이 아닌 익스플로잇)이 됨. `PROJECT_HANDOFF.md` Later backlog에 기록 필요.
5. **Fence 장식 오브젝트(Fence1/FenceLong 등)의 슬롯 시스템 편입** — 지금은 무해하므로 손대지 않음. 향후 "장식 펜스도 파괴 가능하게" 요구가 생기면 별도 작업.
6. **리소스 노드 정확한 개수 재검증(224 vs 241 불일치)** — 리소스 시스템을 직접 다룰 때로 연기.

---

## 5. 회귀 검증 체크리스트 (마이그레이션 완료 후 필수)

`PROJECT_HANDOFF.md` 섹션 12 기준 + 이번 조사로 추가된 항목:

- [ ] Day1 → Night1 → Day2 → Night2 → Day3까지 최소 2주기 안정 확인 (기존엔 1주기만 확인됨)
- [ ] **Night1에서 Base Core를 의도적으로 손상 → Day2로 전환 → Night2 시작 시 손상이 유지되는지 확인** (2.3 버그가 실제로 해소됐는지 명시적 검증)
- [ ] **Day 타이머 게이지가 Night 진입 후 멈추고, Day 복귀 시 정상적으로 재시작되는지** (6.1 DontDestroyOnLoad 버그 해소 검증 — 디버그 오버레이로 확인)
- [ ] Fence/Tower 설치·수리·파괴 상태가 Day↔Night 전환 후에도 유지되는지
- [ ] 리소스(Wood/Scrap/Food), Hunger, HP가 전환 후 유지되는지
- [ ] Night2에서 웨이브가 정상적으로 시작되는지 (`SimpleZombieSpawner.ResetForNewNight()` 검증)
- [ ] HUD가 전환 후 stale 값 없이 정상 갱신되는지
- [ ] `[E]` 상호작용 프롬프트, `ESC` 패널 닫기가 전환 후에도 정상 동작하는지
- [ ] 리소스 노드가 Night 페이즈에 상호작용 불가능한 상태 유지되는지 (day-only 게이팅)
- [ ] 중복 오브젝트/중복 구조물 생성 여부 확인 (특히 `DontDestroyOnLoad` 제거 후 `GameManager`/`DayTimeManager`/`ResourceManager`/`BaseManager`가 씬당 정확히 1개씩만 존재하는지)

---

## 6. 결정이 필요한 항목

### 6.1 (신규, 2026-08-13) DontDestroyOnLoad 스코프 버그 — 마이그레이션에 흡수, 전수조사 선행
조사 중 확인: `GameManager.cs:92-93`가 `transform.root`("Managers", `DayTimeManager`/`ResourceManager`/`BaseManager`의 공통 부모)를 `DontDestroyOnLoad`시킴.

- **DayTimeManager**: 실제로 심각한 버그였음. Day의 원본이 안 죽고 계속 틱, Night의 로컬 사본은 `m_IsActive: 0`이라 HUD가 못 찾고 살아남은 원본에 우연히 바인딩됨 → Night 진입 후에도 낮 타이머가 실시간으로 계속 줄어들고, Day 복귀 후엔 새로 생긴 orphan 인스턴스가 `StartDay()`를 못 받아서 게이지가 100%에서 멈춤.
- **ResourceManager**: 똑같이 중복 생성되지만 `gameObject.scene == active` 명시적 필터 덕분에 무해 — 지금까지 리소스 유지가 잘 됐던 건 우연이 아니라 의도된 설계였음.
- **BaseManager**: 똑같이 중복 생성되고 조회 로직 결함도 동일하나, `CaptureState/ApplyState`가 인스턴스 필드를 안 쓰고 매번 전역 재탐색을 해서 지금은 무해. **잠재적 지뢰** — 나중에 인스턴스 필드가 추가되면 DayTimeManager와 같은 버그 재발 가능.

**결정: 별도 즉시 패치하지 않고 마이그레이션에 흡수한다.** 단일 씬(SetActive 토글, 씬 재로드 없음)이 확정되면서 `DontDestroyOnLoad`와 `RunRuntimeState` capture/apply 파이프라인 둘 다 "씬 파괴·재생성"이라는 전제 자체가 사라짐 — 트리거 교체와 함께 같이 제거. **단, 삭제 전 `RunRuntimeState`/`DontDestroyOnLoad`의 전체 사용처 전수조사 필요** (Day↔Night 전환 목적 외에 부트스트랩/디스크 세이브 등 다른 용도로 쓰이는 부분이 있을 수 있음 — 그 부분은 존치).

### 6.2 (해소됨, 2026-08-12) BaseRoot 신설 방식 / dayRoot 필요 여부
1. ~~BaseRoot 신설 방식~~ → **해소.** `HomeBase.prefab` 계층 구조(Hierarchy 확인, 첨부 스크린샷) 결과 `TowerSlots`/`FenceSlots`가 이미 `HomeBase` 아래 정리돼 있어 재구성 불필요. 상시 active로 유지.
2. ~~dayRoot 필요 여부~~ → **해소, 불필요로 확정.** 마을/자원 콘텐츠는 낮/밤 상관없이 항상 시각적으로 노출. 상호작용만 코드로 차단.

파생 영향(밤 조명 작업 우선순위 격상)은 4번 참고.
