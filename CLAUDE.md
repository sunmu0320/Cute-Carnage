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
   (참고: 7번째 Night 클리어 시 Spawner가 부른 `TransitionToDay()` 안에서
   Day 대신 `EnterVictory()`로 게임이 끝난다 — 새 Day 전환 경로가 아님.)
   (2026-10-29: 스트림은 마지막 웨이브 시작 + `streamTailSeconds`(15초)까지 나오고,
   그 꼬리가 끝나기 전엔 Night이 끝나지 않는다 — 종료를 늦추는 조건이지 조기종료가 아님.)

## 현재 아키텍처 (확정)

Day/Night 분리 씬 → 단일 씬 마이그레이션은 **완료됨**. 코드 감사로 확인:
`GameManager`에 `SceneManager.LoadScene` 호출 없음, `DontDestroyOnLoad`
전체 삭제됨, Day/Night은 단일 영구 씬 안에서 `dayRoot`/`nightRoot`
활성화 토글로 전환됨. 씬 리로드가 없으므로 오브젝트는 파괴되지 않고,
펜스/타워/Base Core 손상 상태는 씬 전환 사이에 자연히 유지된다.
`BaseManager`는 현재 빈 스텁이며 (캡처/적용할 대상이 없으므로),
`BaseRuntimeState`는 `baseLevel`/`baseUpgradeId`만 보유한다.

**게임 흐름 (2026-10-05 구현):** `MainMenu.unity`(빌드 인덱스 0, 타이틀
"Press any key" → 메뉴: Continue / New Game / Settings / Quit) →
`Day.unity`(게임 세계, 단일 씬). 씬 로드는 메뉴 진입/복귀 때만 UI
(`MainMenuUI`, `GameOverPanelUI`)에서 하고 `GameManager`는 여전히
`LoadScene`을 호출하지 않는다.
- **Victory:** `GamePhase.Victory` (`GameManager.FinalDay = 7`). 7번째 밤
  클리어 → GameOver와 같은 정지 → 2초 뒤 `GameOverPanelUI`가 "YOU SURVIVED
  7 NIGHTS" 표시 → 클릭 시 MainMenu. Victory 시 저장 삭제.
- **자동 저장 (슬롯 1개):** `SaveSystem` — 매 Day 시작의 `DayCheckpoint`를
  `persistentDataPath/save.json`에 기록(임시파일 후 교체, version 체크).
  Continue는 `SaveSystem.LoadOnNextStart`를 세우고 `GameManager.Start()`가
  재시도 복원 경로로 복원. Fence는 FenceData 대신 tier 번호로 저장하고
  `FenceSlot.FenceDataForTier()`로 복원. 저장이 있는 상태에서 New Game은
  덮어쓰기 확인 팝업을 띄움.
- **플레이어 시작 위치:** 새 게임(이어하기가 아닌 모든 시작, 에디터에서 Day
  직접 Play 포함) → `playerRespawnPoint`. 이어하기 → 저장된 Day 시작 위치/
  방향(`DayCheckpoint.playerPosition/playerYaw`, 포즈 없는 예전 저장은 씬
  위치). GameOver 재시도 → `playerRespawnPoint`. 이동은 `TeleportPlayer()`
  하나로 처리.
- **Esc 일시정지 메뉴:** `PauseMenuUI` (`UIRoot` 프리팹 안 `PauseMenuCanvas`,
  sortOrder 50). Resume / Settings / Main Menu / Quit Game, 뒤의 둘은 확인
  팝업("이번 Day 시작 이후 진행은 저장 안 됨"). `Time.timeScale = 0` +
  `PlayerInteractor`/`PlayerConsume` 비활성. `[DefaultExecutionOrder(-100)]`로
  Structure/NightRepair 패널이 열려 있으면 Esc를 그 패널에 양보,
  GameOver/Victory 중엔 무시. 설정 UI는 공용 `SettingsPanelUI`(메인·일시정지
  메뉴 공유, PlayerPrefs).
- ℹ️ `Day.unity` 저장 시 UIRoot 인스턴스의 RectTransform 오버라이드 약 170개
  (m_AnchorMin.y/m_AnchorMax.y/m_AnchoredPosition)가 0으로 바뀌는 diff가 생긴다. 확인 결과
  (2026-10-18) 전부 Horizontal/VerticalLayoutGroup 자식의 **driven 속성**이라 Unity가 저장 때
  0으로 쓰는 정상 동작이고, 런타임에 레이아웃이 다시 계산하므로 무해하다. 되돌리지 말 것
  (되돌려도 다음 저장 때 다시 0이 됨). 그 외 오버라이드가 바뀌었는지만 diff로 확인할 것.
  에디터에서 Day 씬을 직접 Play하면 새 게임 취급 → 기존 저장을 Day 1로
  덮어씀 (저장 유지 테스트는 MainMenu에서 시작).
- 위 항목들(메뉴/Victory/저장/시작 위치/일시정지) — **Reported implemented.** Claude가 Play
  Mode 자동 테스트로 확인(Day/자원/T2 펜스 HP/소진 노드 복원, 확인 팝업,
  Victory 시 삭제, 포즈 저장·복원, 새 게임/재시도 리스폰, 일시정지 버튼
  흐름). 사용자가 "잘 된 것 같다"고 했으나 항목별 실제 키/마우스 플레이테스트
  (특히 Esc 키 동작)는 명시적으로 확인 안 됨.
- **밤 조명/안개:** `PhaseLighting`이 `OnPhaseChanged`로 태양·Night Volume
  (3초)과 Fog·안개 파티클(3초 지연 후 10초)을 보간. 안개는 `PlayerRoot`
  child `NightMist` 파티클 + `NightMist.shader`(soft particle, 화면 중앙
  불규칙 구멍), 펜스 안은 `MistBlocker`(VFXVolume 레이어, 충돌 없음) +
  `MistFadeZone`으로 20%만 유지 — **Verified** (사용자 플레이 확인).

- **Day/Night HUD 다이얼 (2026-10-13):** `DayNightHUD`(HUDCanvas의 `TopCenterStatus`)가
  상단 중앙 다이얼/라벨/바/상세 텍스트를 단독 소유 (`HUDController`의 Day 타이머·DayText 코드는
  삭제됨). `Update()` 없음 — Day는 `DayTimeManager.OnSecondChanged`(초가 바뀔 때만), Night은
  `SimpleZombieSpawner.OnNightProgressChanged`(처치/웨이브 변경 때), 모드는 `OnPhaseChanged`.
  밤 진행도 = (현재 웨이브 인덱스 + 처치/예정 총수)/웨이브 수, 처치는 `Zombie.Died`
  (Die()에서 1회만 발화)로 집계. 아트 적용됨 (2026-10-14): `Art/UI/HUD/DayNightTiner/`
  원본은 보존, HUD는 여백 정리본 `Trimmed/`(해/달/구름/마을/프레임/게이지) + 원본 하늘 3장 사용.
  `Dial/Mask`(Knob 원형 Mask) 안에 하늘(낮/노을 알파/밤)→해·달→구름→마을, 마스크 밖 `Frame`.
  노을 = 낮 진행도 `sunsetStartProgress`(0.8)부터 알파. 구름 이동은 `DayNightClouds`의
  `Update()` — 규칙 6의 의도적 예외(상태 폴링이 아닌 장식 애니메이션, timeScale 0이면 정지).
  **밤 다이얼 시간 기반 (2026-10-29, Night waves Phase 3):** 게이지 = 밤 경과 / (마지막 웨이브 +
  스트림 꼬리), 웨이브마다 노란 마름모 마커(Tank/Boss 웨이브는 빨강), 텍스트 `WAVE n · m:ss` →
  마지막 웨이브 후 `ZOMBIES LEFT n`. 진행 아이콘 `DayNightRunner`(임시 그림 `RunnerPlaceholder.png`)는
  1초마다 목표를 받아 그 사이를 매 프레임 보간 — 구름과 같은 규칙 6 예외(장식, 상태 안 읽음).
  — **Reported implemented** (Play Mode 자동 테스트 + 사용자가 HUD 가시성 OK).
  **좀비 등장 연출 (2026-10-29, Night waves Phase 4):** 화면 안에서 스폰된 스트림 좀비만
  땅속 0.6m에서 1.2초간 솟으며 Getting Up(×1.8) → Scream(×1.4) → 걷기, 총 4.5초 이동·공격 불가
  (`Zombie.emergeDuration`). 흙 조각·먼지는 스포너 prefab 자식 `ZombieEmergeFX`가 공용 파티클에 Emit.
  비명 소리는 `ZombieData.emergeScream` 연결만 됨 (오디오 파일 없음, 사용자가 나중에 추가).
  같은 작업에서 `BasicZombieRoot`의 `Visual/BasicZombie` y를 -1.0 → -0.35로 올림 (모든 좀비 발이
  땅 밑 0.53m에 묻혀 있었음). — **Reported implemented** (Play Mode 자동 테스트 + 사용자 만족 확인).
  **밤 경고 UI (2026-10-29, Night waves Phase 5·6):** 웨이브 5마리가 HomeBase 자식 `ApproachAlarm`
  (반경 30m 트리거)에 들어오면 웨이브당 1번 화면 가장자리 노란 `!` (`EdgeIndicatorUI`). 펜스/타워/BaseCore가
  맞으면 플레이어 둘레 원 위 빨간 호 (`StructureHitIndicator`, `StructureDamageEvents.Report`, 맞을 때마다 2초
  유지). 둘 다 표시 중에만 LateUpdate — 규칙 6 표시 예외. — **Reported implemented** (Play Mode 자동 테스트 + 사용자 확인).
  **2026-11-01 변경:** 대상이 화면 안이어도 계속 표시 — `!`는 플레이어에서 대상 쪽 `onScreenRingRadius`(160)
  위치(더 가까우면 대상 위), 빨간 호는 화면 안 구조물도 표시. — **Verified** (Play Mode 짧은 확인 + 사용자 플레이 확인).
  ⚠️ 커밋된 `Day.unity`는 UIRoot가 비활성(`m_IsActive: 0`, 895fe8b부터)이었고 작업 중 1로 복구함.
  **좀비 군중 간격 (2026-11-01, SC2식):** `ZombieCrowd`(static, 첫 `Zombie.LateUpdate`가 프레임당 1회 실행)가
  공간 해시로 겹친 좀비를 밀어냄(GC 0). 공격 범위 안·등장 중·Frozen 좀비(`IsCrowdAnchored`)는 안 밀리고 걷는
  좀비만 비켜서서 앞줄만 공격, 뒷줄은 대기. 막힌 좀비는 공격 중/대기 중(`IsCrowdWaiting`) 좀비를 끼고 좀비별
  고정 방향(`CrowdSlideSign`)으로 옆으로 미끄러져 빈자리를 찾음. 간격 `Zombie.crowdRadius`(0.4 × bodyScale),
  미끄러짐 `ZombieCrowd.SlideFactor`(1). 걷기 애니는 실제 이동량 기준(막히면 idle). 슬롯 예약 방식은 안 씀.
  — **Verified** (Play Mode 자동 테스트: 겹침 0, BaseCore 공격 12마리·옆면까지 퍼짐 + 사용자 플레이 OK).
  펜스 동시 공격 수가 줄어 밤 밸런스가 바뀜 — `NightSchedule`은 사용자가 조정.
- **사운드 (2026-11-01):** 모든 1회성 효과음은 `SfxPlayer.Play(clip 또는 clip[], 위치, 볼륨)` 하나로 재생
  (첫 사용 시 자동 생성, AudioSource 24개 풀, 같은 소리 최소 간격 기본 0.08초, 피치 ±10%, timeScale 0이면 무음,
  마스터 볼륨 = `AudioListener.volume`). 좀비 소리 칸은 `ZombieData`(emergeScream/hit/death/attackImpact, 비면 무음).
  플레이어 발소리는 `CharacterVisual`의 `PlayerFootsteps` — Walking/Running FBX import 설정의 애니메이션 이벤트
  (`AnimationEvent_Footstep`, 발 닿는 프레임)로 재생, 블렌드 중엔 가중치 > 0.5 클립만. 소리 파일은
  `Assets/Audio/` (원본은 `Raw/`, 사용자 제공, Freesound CC0). — **Verified** (발소리: Play Mode 확인 + 사용자 청취 OK;
  좀비 소리는 파일 없음).
  좀비 소리 (2026-11-02, `Assets/Audio/Zombie/`): `ZombieData` — `emergeScreams`(Scream1~4, Tank는 TankScream2·3),
  `attackSounds`(claw_strike1~3, 공격 명중 순간), `groanSounds`(ZombiePain1~4, 맞을 때 전 좀비 공용 3~7초 무작위 쿨다운으로
  가끔만), `hitSound`(모든 타격음 — 파일 아직 없음). 웨이브 시작 = `SimpleZombieSpawner.waveStartScreams`(WaveStartScream1~6,
  웨이브가 50m 밖이라 리스너에서 웨이브 방향 10m 지점에서 재생). — **Reported implemented** (Play Mode: pain/claw 재생
  확인; 웨이브 비명은 실제 밤 미확인).
  새총 소리 (2026-11-01): 발사할 때만 `WeaponData.releaseSounds`(Slingshot_Release_02 하나,
  `PlayerAutoCombat.TryAttack`). 당김·피격 소리는 사용자 요청으로 삭제. — **Reported implemented**
  (Play Mode 짧은 확인; 사용자 청취 전).
- **타격 이펙트 (2026-11-02):** `Pebble` 자식 `Trail`(TrailRenderer 0.15초, 도착 시 떼어내 `autodestruct`로 페이드).
  좀비는 데미지마다 `hitFlashSeconds`(0.1) 동안 머티리얼 배열 끝에 `Zombie.hitFlashMaterial`(`Art/VFX/Hit/HitFlash.mat`,
  투명 흰 Unlit)을 덧그림 — 세기 = 그 머티리얼 Base Color 알파(0.2, Linear 색공간이라 숫자보다 진해 보임). 플래시 중엔
  틴트 블록을 끄고 `ApplyLook`으로 재적용, 모든 데미지 소스 공통. 피 튀김은
  `PlayerAutoCombat.hitVfxPrefab` = `BloodHit`(구 메시 파티클 버스트, 투사체 진행 방향, 끝나면 자동 삭제).
  — **Verified** (Play Mode 캡처 + 사용자 확인; 반투명 덧그리기는 2026-11-02 캡처 확인).
- **좀비 피격 반응 (2026-11-02):** `Zombie.TakeDamage(amount, knockDirection)` — 방향이 있으면(돌멩이) 0.15초 동안
  `knockbackDistance`(0.3m) × `ZombieData.knockbackMultiplier`(Tank 0.3) 밀림, 그동안 이동·공격 안 함. 상체 젖히기는
  애니메이션 클립/레이어 없이 LateUpdate에서 Spine/Chest/Neck/Head를 미는 방향으로 `hitLeanAngle`(20°) 기울였다
  `hitLeanSeconds`(0.5)에 걸쳐 복귀 (방향 없는 데미지는 앞에서 맞은 것처럼 뒤로). 휴머노이드 Override 레이어는 빈
  상태에서도 기본 포즈로 덮어써서 클립 방식은 폐기 — `Zombie Reaction Hit(_Simple).fbx`는 미사용. 등장 중/Frozen/
  죽는 타격은 넉백·젖히기 없음. — **Reported implemented** (Play Mode: 넉백 0.304m, 네 방향 젖힘 방향 확인; 사용자 직접 확인 전).
- **플레이어 사격 애니메이션 (2026-11-01):** 사거리 안에 대상이 있으면 몸 전체가 대상 쪽으로 부드럽게 돌고
  (`PlayerAutoCombat.TryGetFacingDirection` → `PlayerMovement.Rotate`), 이동은 입력 방향 그대로, 다리는 걷기/idle.
  `PlayerLocomotion.controller`의 `UpperBodyShoot` 레이어(`UpperBodyMask` 상체만, Override, 대상 있을 때 weight 1로
  블렌드)에서 발사마다 `Shoot` 트리거 → Recoil → Draw (Mixamo `Longbow Aiming Pack`, Humanoid로 Hansoo 아바타 복사).
  Mixamo 활 클립은 골반이 ~90° 돌아 있어 `CharacterVisual`의 `BowAimTwist`가 LateUpdate에서 척추를 비틀어
  왼팔(어깨→손)을 정면에 맞춤. 새총은 왼손: `WeaponHoldPoint`가 `mixamorig:LeftHandIndex1` 밑(오른손 값의
  bone-local X 미러), 모양은 `PlayerWeaponController` Weapon Visual Offsets (게임 시작 시 생성되므로 Play 중엔
  `BirdShot(Clone)` Transform으로 맞춰 보고 오프셋에 옮겨 적을 것). 뒷걸음/옆걸음 애니는 보류(Planned).
  — **Verified** (Play Mode 확인 + 사용자가 조준 방향·손 위치 확인).
- **홈베이스 밤 안개 감소 (2026-10-18):** HomeBase 프리팹 `MistBlocker`(VFXVolume, 파티클 트리거)를
  30×30 Box → Capsule 반지름 28로 교체 (안개 파티클 지름이 18m라 중심이 28m 안이면 흙바닥 ~20m 안쪽을
  덮음; 흙바닥 `TL_Dirt` 반지름 ~23m). 안쪽 파티클은 `MistFadeZone.keepFraction` 0.2.
  새 `HomeFogZone`(Ignore Raycast 레이어, Capsule 반지름 20, 트리거 진입/이탈로
  `PhaseLighting.SetPlayerInHomeZone`) → 거리 안개 밀도 ×`homeZoneFogFactor`(0.2). 거리 안개는
  카메라 거리상 영향이 ~3%라 체감 효과의 대부분은 파티클 쪽. 횃불/라이트는 Planned.
  — **Reported implemented** (Play Mode 자동 테스트: 구역 안/밖 밀도 전환, 시작 시 안쪽 감지,
  구역 안 파티클 알파 ~22%, 캡처 확인; 사용자 직접 플레이는 아직).
  같은 작업에서 Retry가 좀비를 `Destroy`만 해서 `AliveZombieCount`가 안 줄던 버그를
  `Zombie.OnDestroy`로 수정. — **Reported implemented** (Play Mode 자동 테스트로
  Day 0/50%, Night 처치·경계·중복 피격, GameOver→Retry→다음 밤 clear, Night→Day 2, 일시정지 확인;
  사용자 직접 플레이는 아직).

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
    복원 후 플레이어는 `GameManager.playerRespawnPoint`(HomeBase 프리팹
    child `RespawnPoint`)로 이동(Rigidbody 위치 직접 설정) — **Verified.**
  - `GameOverPanelUI`의 Panel Root/Background Button/Continue Hint/
    `Title Text` 모두 씬에 연결됨 (2026-10-05 확인).
- `TowerSlot.EnsureCurrentTowerReference()`의 반경 기반 타워 자동 채택
  로직이 단일 씬 모델에서는 죽은 코드일 가능성이 있으나 미확인 —
  건드리기 전에 pre-placed tower 존재 여부부터 확인할 것.
- Fence UI가 Damaged/Destroyed를 구분하지 못함 (`needsRepair`만 존재).
- `StructureActionPanelUI` (Day 패널) — Fence/Tower 둘 다 단일 Upgrade
  버튼(Empty면 INSTALL)으로 설치/업그레이드, Repair 버튼, 핫키 E=Upgrade,
  R=Repair (패널 연 프레임은 무시) — **Verified.** Tower 설치(Empty→T1)는
  즉시 실행. Tower T1→T2+는 `TowerUpgradeConfirmPanel` 확인 팝업을 거쳐야
  함 — **Approved design, 미구현** (T2 `TowerData`도 아직 없음, 지금은
  T1이 Max Tier라 Upgrade 비활성). `DayTimeTester.resetKey`는 R 충돌을
  피해 `0`(Alpha0)으로 변경됨. 테스트 키 `DayTimeTester.buildAllKey` = **B**: 빈(잠기지 않은) 펜스/타워 칸
  전부에 T1을 무료·풀 HP로 설치 (복원 경로 사용, 다음 Day 시작 전 재시도하면 되돌아감).

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
- **자원 노드 소진 + 재생성 — 구현 완료, Verified (2026-10-04 플레이테스트:
  채집→소진, 3~5일 뒤 재생성, 재생성 노드 재채집, GameOver 재시도 복원).**
  `ResourceNodeRegistry`(GameManager 소유) + `Tools/Cute Carnage/Assign Resource
  Node PersistentIds` 메뉴(Day.unity 336개 부여 완료). 새 자원 노드를 배치하면
  이 메뉴를 다시 실행할 것. 소진 목록은 DayCheckpoint와 함께 `SaveSystem`으로 디스크 저장됨.
  확정 설계 (2026-09-29):
  - 맵에 보이는 나무/스크랩/음식은 거의 전부 채집 가능. 숲 나무를 캐서 길이
    뚫리는 것도 의도된 것 (Day 시간 제한이 채집량을 제한).
  - **모든 자원은 1회 채집으로 소진** (부분 채집/남은 양 저장 없음).
  - 재생성까지 3~5일. **재생성 날짜(respawnDay)는 채집하는 순간 한 번만 굴려서
    저장**하고 이후 다시 굴리지 않음 (재시도 시 결과가 바뀌지 않도록).
  - 저장은 **delta save**: 캐진 노드만 `PersistentId -> respawnDay`로 기록, 손대지
    않은 노드는 저장 안 함. 중앙 **registry** 하나가 목록을 관리.
  - `GameManager.OnPhaseChanged`(Day 진입) 시 single-shot으로 캐진 목록만 훑어
    날짜 된 노드를 복구. 복구 자리에 플레이어/건물 등이 있으면 미룸.
  - `DayCheckpoint`에 캐진 목록 사본을 포함(재시도 시 복원).
  - 착수 시 첫 단계로 씬의 모든 ResourceNode 인스턴스에 PersistentId를 일괄
    부여(+중복 검사)하는 에디터 메뉴를 만들 것 — 프리팹 에셋에 붙이면 모든
    인스턴스가 같은 ID를 공유하므로 금지.

## graphify

This project has a knowledge graph at graphify-out/ with god nodes, community structure, and cross-file relationships.

Rules:
- For codebase questions, first run `graphify query "<question>"` when graphify-out/graph.json exists. Use `graphify path "<A>" "<B>"` for relationships and `graphify explain "<concept>"` for focused concepts. These return a scoped subgraph, usually much smaller than GRAPH_REPORT.md or raw grep output.
- If graphify-out/wiki/index.md exists, use it for broad navigation instead of raw source browsing.
- Read graphify-out/GRAPH_REPORT.md only for broad architecture review or when query/path/explain do not surface enough context.
- Graph refresh follows the Korean rule above (`graphify update .` only after large uncommitted changes; the post-commit hook handles the rest). Never run `cluster-only`/`label` without `--no-label` — they call an LLM.
