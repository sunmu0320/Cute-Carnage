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

## 현재 진행 중인 작업
Day/Night 분리 씬 → 단일 씬 마이그레이션. 전체 배경, 확정된 설계,
결정 근거는 리포지토리 루트의 `Single_Scene_Migration_Proposal.md`를
반드시 먼저 읽을 것 — 이 문서가 지금까지의 모든 조사 결과와 스코프
결정을 담고 있음.

## 참고 문서
- `PROJECT_HANDOFF.md` — 프로젝트 전체 컨텍스트, 리스크, 우선순위
- `Cute_Carnage_GDD.md` — 게임 디자인 문서
- `Single_Scene_Migration_Proposal.md` — 현재 진행 중인 마이그레이션 스펙
