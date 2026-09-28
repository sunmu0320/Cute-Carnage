# Cute Carnage — Claude Code Instructions

이 프로젝트 작업 시 다음을 항상 지켜라:

1. 코드 수정 전 반드시 현재 리포지토리 상태를 먼저 확인하고 보고할 것.
   추측하지 말고 실제 파일을 읽어라.
2. 다음 네 가지를 구분해서 보고할 것:
   - Verified (실제 플레이테스트/명시적 확인됨)
   - Reported implemented (이전에 구현됐다고 보고됐으나 재확인 필요)
   - Approved design (설계는 확정, 미구현일 수 있음)
   - Planned (미래 작업, 현재 기능 아님)
3. 승인된 설계나 아키텍처 결정을 조용히 뒤집지 말 것. 충돌이 있으면
   설명하고 먼저 물어볼 것.
4. 큰 변경 전에는 계획을 먼저 설명하고 승인을 받은 뒤 실제 코드를 작성할 것.
5. Persistent ID, 슬롯 계층 구조, 데이터 ID는 마이그레이션에 민감하니
   함부로 재생성/변경하지 말 것.
6. UI/데이터 상태는 매 프레임 폴링하지 말 것 (Update()에서 매 프레임
   체크/갱신 금지). 대신 값이 실제로 바뀔 때만 한 번 발화하는
   이벤트 기반, single-shot 갱신을 우선할 것
   (예: OnPhaseChanged, OnDayEnded, 리소스 변경 이벤트 등).
7. Night은 오직 마지막으로 설정된 웨이브가 전부 스폰되고, 스폰된 좀비가
   전부 죽었을 때만 끝난다. 이를 조기 종료시키는 별도 타이머를
   추가하지 말 것 — `SimpleZombieSpawner`가 Day 전환을 요청하는
   유일한 주체다. **단, 예외 한 가지(승인됨):** `BaseCore`가 파괴되어
   `GameManager`가 `GamePhase.GameOver`로 전환된 뒤 플레이어가
   Continue를 눌러 `RetryCurrentDay()`가 호출되는 경우는 Night 클리어
   조기종료가 아니라 패배 후 같은 Day 재시도이므로 Day로 직접 전환한다.
   이 경로 외에 Day 전환을 추가로 만들지 말 것.

## 현재 아키텍처 (확정)

Day/Night 분리 씬 → 단일 씬 마이그레이션은 **완료됨**. 코드 감사로 확인:
`GameManager`에 `SceneManager.LoadScene` 호출 없음, `DontDestroyOnLoad`
전체 삭제됨, Day/Night은 단일 영구 씬 안에서 `dayRoot`/`nightRoot`
활성화 토글로 전환됨. 씬 리로드가 없으므로 오브젝트는 파괴되지 않고,
펜스/타워/Base Core 손상 상태는 씬 전환 사이에 자연히 유지된다.
`BaseManager`는 현재 빈 스텁이며 (캡처/적용할 대상이 없으므로),
`BaseRuntimeState`는 `baseLevel`/`baseUpgradeId`만 보유한다.

`Single_Scene_Migration_Proposal.md`는 이 마이그레이션이 완료되기 전에
작성된 계획 문서로, 지금은 **참조용 과거 작업물**이다 — 더 이상
"진행 중인 작업"이 아니므로 최우선으로 읽을 필요는 없다.

**알려진 확정 gap (구현 필요, 방향은 이미 설계 문서에 있음):**
- ~~`BaseCore.TakeDamage()`가 HP 0 도달 시 `Debug.Log`만 남기고 실제
  게임오버 처리/이벤트가 없음~~, ~~플레이어 사망 시 아무 처리 없음~~ —
  베이스 파괴(`BaseCore.OnBaseDestroyed`)와 플레이어 사망
  (`PlayerHealth.onDeath`, 예전엔 구독자 0명이었음) 둘 다
  `GameManager.EnterGameOver()`로 합류 → `GamePhase.GameOver`,
  원인별로 파괴/사망 직후 즉시 정지(플레이어 이동·상호작용·자동전투·
  음식섭취·Hunger 드레인 잠금, 좀비 `SetFrozen`으로 idle 정지,
  스포너/Day타이머 정지, Day/Night 패널 닫기) 후 2~2.5초 뒤에
  `GameOverPanelUI`가 원인별 타이틀("BASE DESTROYED"/"YOU DIED")로 뜸 —
  **Verified.** 베이스 파괴/플레이어 사망 둘 다 실제 플레이테스트로
  타이틀 정확히 뜨는 것 확인됨.
  - 재시도(`RetryCurrentDay()`)는 "Day 시작 시점으로 완전 복원" —
    **Verified.** 자원/Hunger/Fence/Tower 전부 전날 Day 시작 상태로
    복원되는 것 플레이테스트로 확인됨.
    `GameManager`가 매 Day 시작마다(`Start()`/`TransitionToDay()`)
    BaseCore HP, 자원(Wood/Scrap/Food), 플레이어 HP/Hunger, 모든
    Fence/Tower 슬롯의 설치 여부+HP를 PersistentId 기준으로 스냅샷
    (`DayCheckpoint`)해두고, Continue 시 전부 복원. 이 스냅샷/복원
    경로는 여러 시스템(`FenceSlot.RestoreFenceInternal`,
    `TowerSlot.RestoreTowerInternal` 등 새 메서드)에 걸쳐 있음.
  - `GameOverPanelUI`는 씬에 배치·연결은 됐으나(Panel Root/Background
    Button/Continue Hint), 새로 추가된 `Title Text` 필드는 아직 씬에
    연결 안 됐을 수 있음 — 연결 안 해도 에러는 안 나고 그냥 타이틀
    텍스트만 안 바뀜.
- `TowerSlot.EnsureCurrentTowerReference()`의 반경 기반 타워 자동 채택
  로직이 단일 씬 모델에서는 죽은 코드일 가능성이 있으나 미확인 —
  건드리기 전에 pre-placed tower 존재 여부부터 확인할 것.
- Fence UI가 Damaged/Destroyed를 구분하지 못함 (`needsRepair`만 존재).
- `StructureActionPanelUI` (Day 패널) — Fence/Tower 둘 다 단일 Upgrade
  버튼(Empty면 INSTALL)으로 설치/업그레이드, Repair 버튼, 핫키 E=Upgrade,
  R=Repair (패널 연 프레임은 무시) — **Verified.** Tower 설치(Empty→T1)는
  즉시 실행. Tower T1→T2+는 `TowerUpgradeConfirmPanel` 확인 팝업을 거쳐야
  함 — **Approved design, 미구현** (T2 `TowerData`도 아직 없음, 지금은
  T1이 Max Tier라 Upgrade 비활성). `DayTimeTester.resetKey`(R)가 Repair
  핫키와 충돌 — Inspector에서 키 변경 필요.

## 참고 문서 (우선순위 순)

1. `Cute_Carnage_GDD_2.0V.md` — 게임 디자인 문서, 확정 기준 문서
2. `Cute_Carnage_Game_Structure_Guide2.0V.md` — 기술 아키텍처 기준 문서, 확정
3. `CLAUDE.md` (이 문서) — 작업 규칙, 확정
4. `PROJECT_HANDOFF.md`, `Single_Scene_Migration_Proposal.md` — **참조용
   과거 작업물.** 우선 문서 아님. 필요한 내용은 이미 위 세 문서에
   반영되어 있으므로, 위 세 문서와 내용이 충돌하면 위 세 문서를 따를 것.

## graphify (코드 지식 그래프) 사용 규칙

- graphify 그래프(`graphify-out/graph.json`)는 **탐색용**이다. 코드 구조 질문은
  `graphify query "..."`, `graphify explain`, `graphify affected`로 먼저 조회하되,
  코드를 수정하기 전에는 실제 파일을 반드시 읽는다.
- Inspector 연결, UnityEvent, SendMessage/Invoke 문자열 호출, 애니메이션 이벤트,
  Resources/Addressables 로드는 그래프에 나오지 않는다. 영향 범위를 분석할 때는
  해당 클래스명과 메서드명으로 `.unity`, `.prefab`, `.asset` 파일을 grep해서 보완한다.
- 커밋하지 않은 큰 변경 뒤에는 `graphify update .`로 그래프를 갱신한다. 대량 삭제
  후라서 갱신이 거부되면 `--force`를 쓴다.
- `graphify extract`는 항상 `--code-only`로 실행한다. 문서/이미지 분석(LLM 모드)이
  필요해 보이면 실행하지 말고 먼저 사용자에게 묻는다.
- 그래프 출력은 반드시 프로젝트 루트의 `graphify-out/`에 둔다 (`Assets/` 안에 생기면
  Unity가 에셋으로 import함). 추출은 항상 루트에서 `graphify extract . --code-only --out .`
  로 한다 — `Assets`를 스캔 루트로 쓰면 git 훅이 `Assets/graphify-out/`에 결과를 쓴다.
  제외 목록은 루트의 `.graphifyignore` (스캔 루트 폴더의 파일만 읽힘).

## Backlog (정리 후보, 아직 미착수)

- `HUDController.ResolveMissingSources()` (Assets/Scripts/UI/HUDController.cs)
  — `Update()`에서 ~1초 간격(`nextSourceResolveTime`)으로 self-heal 폴링 중.
  규칙 6 위반. 단일 씬 마이그레이션과 무관하게 별도 작업으로 리팩터할 것.

## graphify

This project has a knowledge graph at graphify-out/ with god nodes, community structure, and cross-file relationships.

Rules:
- For codebase questions, first run `graphify query "<question>"` when graphify-out/graph.json exists. Use `graphify path "<A>" "<B>"` for relationships and `graphify explain "<concept>"` for focused concepts. These return a scoped subgraph, usually much smaller than GRAPH_REPORT.md or raw grep output.
- If graphify-out/wiki/index.md exists, use it for broad navigation instead of raw source browsing.
- Read graphify-out/GRAPH_REPORT.md only for broad architecture review or when query/path/explain do not surface enough context.
- Graph refresh follows the Korean rule above (`graphify update .` only after large uncommitted changes; the post-commit hook handles the rest). Never run `cluster-only`/`label` without `--no-label` — they call an LLM.
