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
   유일한 주체다.

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
- `BaseCore.TakeDamage()`가 HP 0 도달 시 `Debug.Log`만 남기고 실제
  게임오버 처리/이벤트가 없음. Day 기반 재시도 체크포인트를 만들기
  전에 반드시 먼저 해결해야 함.
- `TowerSlot.EnsureCurrentTowerReference()`의 반경 기반 타워 자동 채택
  로직이 단일 씬 모델에서는 죽은 코드일 가능성이 있으나 미확인 —
  건드리기 전에 pre-placed tower 존재 여부부터 확인할 것.
- Fence UI가 Damaged/Destroyed를 구분하지 못함 (`needsRepair`만 존재),
  Fence Upgrade 버튼은 `interactable = false`로 하드코딩됨.

## 참고 문서 (우선순위 순)

1. `Cute_Carnage_GDD_2.0V.md` — 게임 디자인 문서, 확정 기준 문서
2. `Cute_Carnage_Game_Structure_Guide2.0V.md` — 기술 아키텍처 기준 문서, 확정
3. `CLAUDE.md` (이 문서) — 작업 규칙, 확정
4. `PROJECT_HANDOFF.md`, `Single_Scene_Migration_Proposal.md` — **참조용
   과거 작업물.** 우선 문서 아님. 필요한 내용은 이미 위 세 문서에
   반영되어 있으므로, 위 세 문서와 내용이 충돌하면 위 세 문서를 따를 것.

## Backlog (정리 후보, 아직 미착수)

- `HUDController.ResolveMissingSources()` (Assets/Scripts/UI/HUDController.cs)
  — `Update()`에서 ~1초 간격(`nextSourceResolveTime`)으로 self-heal 폴링 중.
  규칙 6 위반. 단일 씬 마이그레이션과 무관하게 별도 작업으로 리팩터할 것.
