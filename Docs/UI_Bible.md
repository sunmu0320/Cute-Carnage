# Survive! UI Bible

> 이 문서는 게임의 모든 UI에 적용할 시각 규칙, 사용성 규칙, 제작 표준을 정의한다.
>
> **중요:** 현재의 밝은 주간 UI 콘셉트는 가능성이 높은 참고 방향이며, 최종 승인된 디자인이 아니다. 명시적으로 승인되기 전까지 모든 결정은 `최종 결정 전`으로 취급한다.

| 문서 정보 | 내용 |
|---|---|
| 문서 버전 | `0.1 (초안)` |
| 마지막 업데이트 | `작성 필요: YYYY-MM-DD` |
| 전체 상태 | `미정 / 검토 중` |
| 현재 UI 디자인 단계 | `탐색 및 기준 수립` |
| Unity 버전 | `Unity 6.3 LTS` |
| 프로젝트 이름 | `Survive! / Cute Carnage` |

## 목차

1. [Overall Style](#1-overall-style)
2. [Color Palette](#2-color-palette)
3. [Borders](#3-borders)
4. [Panels and Windows](#4-panels-and-windows)
5. [Corners](#5-corners)
6. [Depth and Shading](#6-depth-and-shading)
7. [Buttons](#7-buttons)
8. [Icons](#8-icons)
9. [Progress Bars](#9-progress-bars)
10. [Slots and Inventory](#10-slots-and-inventory)
11. [HUD](#11-hud)
12. [Interaction UI](#12-interaction-ui)
13. [Large Windows](#13-large-windows)
14. [Tooltips](#14-tooltips)
15. [Typography](#15-typography)
16. [Animation](#16-animation)
17. [VFX](#17-vfx)
18. [Audio Feedback](#18-audio-feedback)
19. [Day and Night UI Variants](#19-day-and-night-ui-variants)
20. [Accessibility and Readability](#20-accessibility-and-readability)
21. [Unity Production Rules](#21-unity-production-rules)
22. [Reference Images](#22-reference-images)
23. [Decision Log](#23-decision-log)
24. [Asset Production Checklist](#24-asset-production-checklist)
25. [Open Questions](#25-open-questions)
26. [Next Actions](#26-next-actions)

## 진행 개요

상태 예시: `미정`, `작성 중`, `검토 중`, `승인`. 우선순위 예시: `높음`, `중간`, `낮음`.

| Category | Status | Priority | Notes |
|----------|--------|----------|-------|
| Overall Style | 미정 | 높음 | 작성 필요 |
| Color Palette | 검토 중 | 높음 | 주간/야간 모두 임시 방향 |
| Borders | 미정 | 높음 | 작성 필요 |
| Panels | 미정 | 높음 | 작성 필요 |
| Corners | 미정 | 중간 | 작성 필요 |
| Depth and Shading | 미정 | 중간 | 작성 필요 |
| Buttons | 미정 | 높음 | 작성 필요 |
| Icons | 미정 | 높음 | 작성 필요 |
| Progress Bars | 미정 | 높음 | 작성 필요 |
| Slots | 미정 | 중간 | 작성 필요 |
| HUD | 미정 | 높음 | 작성 필요 |
| Interaction UI | 미정 | 높음 | 현재 구현 방식 기록됨 |
| Large Windows | 미정 | 중간 | 작성 필요 |
| Tooltips | 미정 | 중간 | 작성 필요 |
| Typography | 미정 | 높음 | 한글 지원 확인 필요 |
| Animation | 미정 | 중간 | 작성 필요 |
| VFX | 미정 | 낮음 | 작성 필요 |
| Audio Feedback | 미정 | 중간 | 작성 필요 |
| Day and Night Variants | 검토 중 | 높음 | 색상 방향만 임시 제안 |
| Accessibility | 미정 | 높음 | 작성 필요 |
| Unity Production Rules | 초안 | 높음 | 구현 전 검증 필요 |

---

## 1. Overall Style

### HUD Design Direction

#### Current Design Goal

현재 최우선 과제는 전체 UI system보다 **주간 HUD**를 먼저 정의하는 것이다. HUD는 밝고 따뜻하며 한눈에 읽혀야 하고, stylized low-poly zombie survival game에 어울리는 cartoon-like 표현과 가벼운 입체감을 갖는다. 친근하되 지나치게 유아적으로 보이지 않는 균형을 유지한다.

밝은 mobile strategy game UI의 명료함과 활기찬 인상을 참고하되, 특정 게임의 형태나 장식을 직접 복제하지 않는다.

**사용 방향**

- Warm Ivory 또는 밝은 Beige panel background
- Dark Brown outline과 Light Gold 또는 Beige inner border
- Soft bevel, 얇은 top highlight, 아래쪽과 오른쪽의 부드러운 shadow
- 살짝 각지거나 잘린 corner
- 명확하고 chunky한 silhouette
- 한눈에 파악되는 강한 가독성

**피해야 할 방향**

- Blue 중심 UI
- 주간에 무거운 dark charcoal panel 사용
- 강한 military styling, 과도한 metal plating, 너무 많은 bolt 또는 rivet
- 심하게 파손된 post-apocalyptic frame
- 깊이감 없이 평평한 UI
- 지나치게 glossy하고 사실적인 표현
- 과도하게 귀엽거나 baby-like한 표현

> 이 방향은 주간 HUD 기준이다. 야간 버전은 같은 shape와 layout을 유지하면서 추후 더 어둡게 조정할 수 있다.

- 전체 주간 HUD 방향: `잠정 승인`

### 스타일 선택

- [ ] Cute
- [ ] Stylized
- [ ] Low Poly
- [ ] Toy-like
- [ ] Cartoon
- [ ] Post-apocalyptic
- [ ] Survival
- [ ] Clean
- [ ] Chunky
- [ ] Friendly
- [ ] Dangerous
- [ ] Handcrafted

| 편집 필드 | 내용 |
|---|---|
| 디자인 키워드 | `작성 필요` |
| 플레이어가 느껴야 할 감정 | `작성 필요` |
| 시각 참고 자료 | `참고 이미지 추가` |
| 피해야 할 요소 | `작성 필요` |
| 마음에 드는 점 | `작성 필요` |
| 마음에 들지 않는 점 | `작성 필요` |
| 요청 변경 사항 | `작성 필요` |
| 최종 결정 | `최종 결정 전` |

---

## 2. Color Palette

> 색상은 Hex와 실제 게임 화면 캡처를 함께 검토한다. 색상만으로 상태를 전달하지 않는다.

### Primary Color
- 색상/용도/상태: `작성 필요 / 작성 필요 / 미정`

### Secondary Color
- 색상/용도/상태: `작성 필요 / 작성 필요 / 미정`

### Accent Color
- 색상/용도/상태: `작성 필요 / 작성 필요 / 미정`

### Background Color
- 색상/용도/상태: `작성 필요 / 작성 필요 / 미정`

### Panel Color
- 색상/용도/상태: `작성 필요 / 작성 필요 / 미정`

### Text Color
- 색상/용도/상태: `작성 필요 / 작성 필요 / 미정`

### Success Color
- 색상/용도/상태: `작성 필요 / 작성 필요 / 미정`

### Warning Color
- 색상/용도/상태: `작성 필요 / 작성 필요 / 미정`

### Danger Color
- 색상/용도/상태: `작성 필요 / 작성 필요 / 미정`

### Disabled Color
- 색상/용도/상태: `작성 필요 / 작성 필요 / 미정`

### Day Theme

현재 주간 방향은 **밝은 아이보리, 베이지, 따뜻한 노랑, 주황, 초록 포인트, 선명한 어두운 외곽선**이다. 가능성이 높은 참고 방향이지만 **임시안이며 최종안이 아니다**.

### Night Theme

야간 방향은 동일한 전체 형태를 유지하면서 **짙은 차콜, 탁한 보라, 빨강, 주황**을 사용할 수 있다. 이 방향 역시 **임시안이며 최종안이 아니다**.

| Role | Color Name | Hex | Usage | Status |
|------|------------|-----|-------|--------|
| Primary | `작성 필요` | `#______` | `작성 필요` | 미정 |
| Secondary | `작성 필요` | `#______` | `작성 필요` | 미정 |
| Accent | `작성 필요` | `#______` | `작성 필요` | 미정 |
| Background | `작성 필요` | `#______` | `작성 필요` | 미정 |
| Panel | `작성 필요` | `#______` | `작성 필요` | 미정 |
| Text | `작성 필요` | `#______` | `작성 필요` | 미정 |
| Success | `작성 필요` | `#______` | `작성 필요` | 미정 |
| Warning | `작성 필요` | `#______` | `작성 필요` | 미정 |
| Danger | `작성 필요` | `#______` | `작성 필요` | 미정 |
| Disabled | `작성 필요` | `#______` | `작성 필요` | 미정 |

---

## 3. Borders

### 선택 옵션

- 외곽선 두께: `작성 필요: ___ px`
- 외곽선 색상: `작성 필요: #______`
- [ ] Single border
- [ ] Double border
- [ ] Bevel
- [ ] Inner line
- [ ] Corner brackets
- [ ] Bolts
- [ ] Rivets
- [ ] Cracks
- [ ] Wooden detail
- [ ] Metal detail
- [ ] Decorative damage

| 편집 필드 | 내용 |
|---|---|
| 마음에 드는 점 | `작성 필요` |
| 마음에 들지 않는 점 | `작성 필요` |
| 변경 사항 | `작성 필요` |
| 최종 결정 | `최종 결정 전` |

---

## 4. Panels and Windows

각 Panel 유형에서 아래 값을 채운다.

| Panel 유형 | 배경 재질 | Border 스타일 | Corner 스타일 | Shadow | Highlight | 투명도 | Padding | Header 스타일 | Close 버튼 스타일 | 최종 결정 |
|---|---|---|---|---|---|---|---|---|---|---|
| Small contextual panel | 작성 필요 | 미정 | 미정 | 미정 | 미정 | `___%` | `___ px` | 작성 필요 | 작성 필요 | 최종 결정 전 |
| Medium management panel | 작성 필요 | 미정 | 미정 | 미정 | 미정 | `___%` | `___ px` | 작성 필요 | 작성 필요 | 최종 결정 전 |
| Large menu window | 작성 필요 | 미정 | 미정 | 미정 | 미정 | `___%` | `___ px` | 작성 필요 | 작성 필요 | 최종 결정 전 |
| Modal popup | 작성 필요 | 미정 | 미정 | 미정 | 미정 | `___%` | `___ px` | 작성 필요 | 작성 필요 | 최종 결정 전 |
| Tooltip | 작성 필요 | 미정 | 미정 | 미정 | 미정 | `___%` | `___ px` | 작성 필요 | 해당 없음/미정 | 최종 결정 전 |
| Confirmation dialog | 작성 필요 | 미정 | 미정 | 미정 | 미정 | `___%` | `___ px` | 작성 필요 | 작성 필요 | 최종 결정 전 |

---

## 5. Corners

- [ ] Rounded
- [ ] Slightly rounded
- [ ] Angular
- [ ] Cut corners
- [ ] Bracketed corners
- [ ] Decorative corner plates
- [ ] Uneven or damaged corners

- Corner 반경/절단 크기: `작성 필요`
- 사용 범위: `작성 필요`
- 최종 결정: `최종 결정 전`

---

## 6. Depth and Shading

> UI는 약간의 3차원 깊이를 느끼게 하되, 지나치게 광택이 나거나 사실적으로 보이지 않아야 한다.

- [ ] Drop Shadow
- [ ] Inner Shadow
- [ ] Bevel
- [ ] Top Highlight
- [ ] Bottom Shadow
- [ ] Gradient
- [ ] Rim Light
- [ ] Outer Glow
- [ ] Pressed depth
- [ ] Raised button depth

| 설정 | 값 |
|---|---|
| 광원 방향 | `작성 필요` |
| 깊이 강도 | `작성 필요` |
| 그림자 거리/Blur | `작성 필요` |
| 최종 결정 | `최종 결정 전` |

---

## 7. Buttons

모든 버튼은 `Normal`, `Hover`, `Pressed`, `Selected`, `Disabled` 상태를 정의한다.

### Primary
| 상태 | Background | Border | Text color | Icon | Shadow | Animation | Sound |
|---|---|---|---|---|---|---|---|
| Normal | 미정 | 미정 | 미정 | 미정 | 미정 | 미정 | 미정 |
| Hover | 미정 | 미정 | 미정 | 미정 | 미정 | 미정 | 미정 |
| Pressed | 미정 | 미정 | 미정 | 미정 | 미정 | 미정 | 미정 |
| Selected | 미정 | 미정 | 미정 | 미정 | 미정 | 미정 | 미정 |
| Disabled | 미정 | 미정 | 미정 | 미정 | 미정 | 미정 | 미정 |
- 최종 결정: `최종 결정 전`

### Secondary
- 상태별 Background / Border / Text color / Icon / Shadow / Animation / Sound: `작성 필요`
- Normal: `미정` · Hover: `미정` · Pressed: `미정` · Selected: `미정` · Disabled: `미정`
- 최종 결정: `최종 결정 전`

### Confirm
- 상태별 Background / Border / Text color / Icon / Shadow / Animation / Sound: `작성 필요`
- Normal: `미정` · Hover: `미정` · Pressed: `미정` · Selected: `미정` · Disabled: `미정`
- 최종 결정: `최종 결정 전`

### Cancel
- 상태별 Background / Border / Text color / Icon / Shadow / Animation / Sound: `작성 필요`
- Normal: `미정` · Hover: `미정` · Pressed: `미정` · Selected: `미정` · Disabled: `미정`
- 최종 결정: `최종 결정 전`

### Upgrade
- 상태별 Background / Border / Text color / Icon / Shadow / Animation / Sound: `작성 필요`
- Normal: `미정` · Hover: `미정` · Pressed: `미정` · Selected: `미정` · Disabled: `미정`
- 최종 결정: `최종 결정 전`

### Build
- 상태별 Background / Border / Text color / Icon / Shadow / Animation / Sound: `작성 필요`
- Normal: `미정` · Hover: `미정` · Pressed: `미정` · Selected: `미정` · Disabled: `미정`
- 최종 결정: `최종 결정 전`

### Repair
- 상태별 Background / Border / Text color / Icon / Shadow / Animation / Sound: `작성 필요`
- Normal: `미정` · Hover: `미정` · Pressed: `미정` · Selected: `미정` · Disabled: `미정`
- 최종 결정: `최종 결정 전`

### Danger
- 상태별 Background / Border / Text color / Icon / Shadow / Animation / Sound: `작성 필요`
- Normal: `미정` · Hover: `미정` · Pressed: `미정` · Selected: `미정` · Disabled: `미정`
- 최종 결정: `최종 결정 전`

### Disabled
- 상태별 Background / Border / Text color / Icon / Shadow / Animation / Sound: `작성 필요`
- Normal: `미정` · Hover: `미정` · Pressed: `미정` · Selected: `미정` · Disabled: `미정`
- 최종 결정: `최종 결정 전`

### Icon-only button
- 상태별 Background / Border / Text color / Icon / Shadow / Animation / Sound: `작성 필요`
- Normal: `미정` · Hover: `미정` · Pressed: `미정` · Selected: `미정` · Disabled: `미정`
- 최종 결정: `최종 결정 전`

### Tab button
- 상태별 Background / Border / Text color / Icon / Shadow / Animation / Sound: `작성 필요`
- Normal: `미정` · Hover: `미정` · Pressed: `미정` · Selected: `미정` · Disabled: `미정`
- 최종 결정: `최종 결정 전`

---

## 8. Icons

### 아이콘 목록

- [ ] Wood
- [ ] Scrap
- [ ] Food
- [ ] Stone
- [ ] Health
- [ ] Hunger
- [ ] Day
- [ ] Night
- [ ] Wave
- [ ] Repair
- [ ] Upgrade
- [ ] Build
- [ ] Inventory
- [ ] Storage
- [ ] Crafting
- [ ] Tower
- [ ] Fence
- [ ] Base Core
- [ ] Settings
- [ ] Map
- [ ] Close
- [ ] Confirm
- [ ] Cancel

| 결정 항목 | 값 |
|---|---|
| 외곽선 두께 | `작성 필요` |
| Perspective | `작성 필요` |
| 광원 방향 | `작성 필요` |
| 색상 수 | `작성 필요` |
| Shadow | `작성 필요` |
| Highlight | `작성 필요` |
| Background tile | `작성 필요` |
| 크기 일관성 | `작성 필요` |
| 최종 결정 | `최종 결정 전` |

---

## 9. Progress Bars

각 Bar의 공통 편집 필드: Fill color, Empty color, Border, Icon, Text format, Animation, Low-value warning, Final decision.

### Player HP

- 형태: Horizontal bar
- Icon 위치: 왼쪽
- 값 표기: Current HP / Max HP
- Fill: Red 또는 Coral, 미세한 vertical gradient
- Empty gauge: 남은 용량이 분명히 보이는 dark color
- 외형: 강한 outline, soft inner shadow, 작은 top highlight
- HP Bar layout: `잠정 승인`
- HP icon: `시안 필요`

현재 heart icon은 너무 사랑스럽고 귀여운 인상이 강하다. Health를 즉시 전달하되 survival gameplay에 어울리는 방향으로 조정한다.

#### HP Icon 후보

1. **Survival Heart**
   - 조금 더 각진 heart silhouette
   - 작은 bandage detail
   - 두꺼운 outline
   - 덜 romantic하고 덜 부드러운 인상
2. **Cracked Heart**
   - 작고 절제된 crack
   - 지나치게 어둡거나 horror 중심으로 보이지 않게 한다.
3. **Medical Heart**
   - Heart 내부 또는 옆에 작은 medical cross
   - 강한 가독성
4. **Shield Heart**
   - Heart와 얕은 shield shape의 결합
   - 생존과 방어의 의미 전달

> 현재 권장안은 작은 bandage 또는 medical-cross detail을 넣은, 더 각진 Survival Heart이다.

#### Things to Avoid

- 반짝이는 romantic heart
- 지나치게 둥근 heart
- Baby-like face 또는 표정
- 과도한 pink
- 지나치게 glossy한 candy appearance

### Hunger

- Icon: chunky하고 읽기 쉬운 Meat Drumstick
- Fill: Yellow 또는 Orange, 부드러운 gradient
- Frame: HP Bar와 동일한 구조
- 값 표기: Current Hunger / Max Hunger
- 외형: 강한 outline과 subtle inner shadow

| 허기 상태 | Fill 방향 |
|---|---|
| 높음 | Warm Yellow |
| 중간 | Orange |
| 낮음 | Red Orange |
| 비어 있음 | Dark Red |

낮은 허기 상태에서는 subtle pulse, 가벼운 icon shake, 숫자 색상 변화를 사용할 수 있다. Full-screen flashing과 과도한 screen shake는 피한다.

> Meat Drumstick icon과 Yellow–Orange hunger gauge를 사용한다.

- Hunger icon: `잠정 승인`
- Hunger bar layout: `잠정 승인`

### Shared HP and Hunger Bar Rules

- HP와 Hunger는 같은 frame family를 사용한다.
- 높이, padding, text placement, outline treatment를 동일하게 유지한다.
- Icon과 fill color만 변경한다.
- 가능한 경우 공유 가능한 Unity prefab을 사용한다.
- Fill에는 미세한 vertical gradient를 사용한다.
- Empty gauge는 남은 용량이 분명히 보일 만큼 어둡게 한다.
- 숫자는 fill 위에서도 항상 읽혀야 한다.
- End cap은 살짝 둥글거나 부드럽게 각진 형태를 사용할 수 있다.

```text
Icon
→ Outer Frame
→ Empty Gauge Background
→ Gauge Fill
→ Top Highlight
→ Value Text
```

### Gather Progress
- Fill / Empty / Border / Icon: `작성 필요` / `작성 필요` / `미정` / `미정`
- Text / Animation / Low-value warning / 최종 결정: `작성 필요` / `미정` / `미정` / `최종 결정 전`

### Structure HP
- Fill / Empty / Border / Icon: `작성 필요` / `작성 필요` / `미정` / `미정`
- Text / Animation / Low-value warning / 최종 결정: `작성 필요` / `미정` / `미정` / `최종 결정 전`

### Base Core HP
- Fill / Empty / Border / Icon: `작성 필요` / `작성 필요` / `미정` / `미정`
- Text / Animation / Low-value warning / 최종 결정: `작성 필요` / `미정` / `미정` / `최종 결정 전`

### Day Timer
- Fill / Empty / Border / Icon: `작성 필요` / `작성 필요` / `미정` / `미정`
- Text / Animation / Low-value warning / 최종 결정: `작성 필요` / `미정` / `미정` / `최종 결정 전`

### Night Timer
- Fill / Empty / Border / Icon: `작성 필요` / `작성 필요` / `미정` / `미정`
- Text / Animation / Low-value warning / 최종 결정: `작성 필요` / `미정` / `미정` / `최종 결정 전`

### Wave Progress
- Fill / Empty / Border / Icon: `작성 필요` / `작성 필요` / `미정` / `미정`
- Text / Animation / Low-value warning / 최종 결정: `작성 필요` / `미정` / `미정` / `최종 결정 전`

### Upgrade Progress
- Fill / Empty / Border / Icon: `작성 필요` / `작성 필요` / `미정` / `미정`
- Text / Animation / Low-value warning / 최종 결정: `작성 필요` / `미정` / `미정` / `최종 결정 전`

### Experience
- Fill / Empty / Border / Icon: `작성 필요` / `작성 필요` / `미정` / `미정`
- Text / Animation / Low-value warning / 최종 결정: `작성 필요` / `미정` / `미정` / `최종 결정 전`

---

## 10. Slots and Inventory

| Slot 유형 | 기본 배경 | 선택 Border | 수량 Text | Rarity 표현 | Hover 표현 | Empty 상태 | 최종 결정 |
|---|---|---|---|---|---|---|---|
| Resource slot | 미정 | 미정 | 작성 필요 | 미정 | 미정 | 미정 | 최종 결정 전 |
| Inventory slot | 미정 | 미정 | 작성 필요 | 미정 | 미정 | 미정 | 최종 결정 전 |
| Crafting slot | 미정 | 미정 | 작성 필요 | 미정 | 미정 | 미정 | 최종 결정 전 |
| Equipment slot | 미정 | 미정 | 작성 필요 | 미정 | 미정 | 미정 | 최종 결정 전 |
| Locked slot | 미정 | 미정 | 작성 필요 | 미정 | 미정 | 미정 | 최종 결정 전 |
| Selected slot | 미정 | 미정 | 작성 필요 | 미정 | 미정 | 미정 | 최종 결정 전 |
| Disabled slot | 미정 | 미정 | 작성 필요 | 미정 | 미정 | 미정 | 최종 결정 전 |

---

## 11. HUD

### HUD Frame

| 필드 | 방향 |
|---|---|
| Main Background | Warm Ivory |
| Inner Background | Pale Cream |
| Outer Border | Dark Brown |
| Inner Border | Light Beige 또는 Light Gold |
| Corner Style | 살짝 잘리거나 각진 corner |
| Depth | Soft bevel |
| Highlight | 얇은 top highlight |
| Shadow | 아래쪽과 오른쪽의 soft shadow |
| Surface | 대부분 깨끗하며 wear는 최소화 |
| Damage Detail | 매우 미세하게만 사용 |
| Bolts and Rivets | 기능상 필요하지 않으면 사용하지 않음 |

> HUD frame은 handcrafted하고 game-like한 느낌을 주되, 주간 탐험에 맞게 충분히 밝고 깨끗해야 한다.

- HUD Frame: `잠정 승인`

### HUD Background

- Pure white 대신 Warm Ivory를 사용한다.
- 미세한 vertical gradient를 사용하며, 위쪽은 조금 더 밝고 아래쪽은 조금 더 어둡게 할 수 있다.
- 3D world와 명확하게 대비되어야 한다.
- 과도한 transparency와 시각적으로 복잡한 texture를 피한다.
- 주간에는 강한 scratch와 damage를 피한다.

| 역할 | 방향 | 상태 |
|---|---|---|
| Main Background | Warm Ivory | 잠정 승인 |
| Inner Background | Pale Cream | 잠정 승인 |
| Outer Border | Dark Brown | 잠정 승인 |
| Inner Highlight | Light Beige | 잠정 승인 |
| Shadow | Soft Brown Gray | 잠정 승인 |

### Player Portrait

현재 HUD reference에는 character portrait가 포함되어 있으나, 최종 HUD 적용 여부는 확정되지 않았다.

**장점**

- Player identity를 강화한다.
- Level, buff, debuff 또는 status를 표시할 수 있다.
- 향후 여러 character나 character customization이 추가될 경우 유용하다.
- Equipment 또는 injury state를 시각적으로 지원할 수 있다.

**단점**

- 중요한 화면 공간을 사용한다.
- Playable character가 하나뿐이면 충분한 gameplay information을 제공하지 못할 수 있다.
- HUD가 복잡해질 수 있다.
- 작은 화면에서 가독성을 낮출 수 있다.

> Portrait를 나중에 추가할 수 있도록 layout 공간은 확보하되, 첫 HUD 구현에는 필수로 요구하지 않는다.

- Player Portrait: `보류`

### 현재 HUD 요소

- Player HP
- Hunger
- Wood
- Scrap
- Food
- Day Timer

### 향후 HUD 요소

- Wave Timer
- Base Core HP
- Active Objective
- Weapon
- Ammo
- Buffs and Debuffs

| 요소 | 화면 위치 | 크기 | 표시 규칙 | 우선순위 | Day 외형 | Night 외형 | 최종 결정 |
|---|---|---|---|---|---|---|---|
| Player HP | 미정 | 미정 | 작성 필요 | 높음 | 미정 | 미정 | 최종 결정 전 |
| Hunger | 미정 | 미정 | 작성 필요 | 높음 | 미정 | 미정 | 최종 결정 전 |
| Wood | 미정 | 미정 | 작성 필요 | 높음 | 미정 | 미정 | 최종 결정 전 |
| Scrap | 미정 | 미정 | 작성 필요 | 높음 | 미정 | 미정 | 최종 결정 전 |
| Food | 미정 | 미정 | 작성 필요 | 높음 | 미정 | 미정 | 최종 결정 전 |
| Day Timer | 미정 | 미정 | 작성 필요 | 높음 | 미정 | 미정 | 최종 결정 전 |
| Wave Timer | 미정 | 미정 | 작성 필요 | 높음 | 미정 | 미정 | 최종 결정 전 |
| Base Core HP | 미정 | 미정 | 작성 필요 | 높음 | 미정 | 미정 | 최종 결정 전 |
| Active Objective | 미정 | 미정 | 작성 필요 | 중간 | 미정 | 미정 | 최종 결정 전 |
| Weapon | 미정 | 미정 | 작성 필요 | 중간 | 미정 | 미정 | 최종 결정 전 |
| Ammo | 미정 | 미정 | 작성 필요 | 중간 | 미정 | 미정 | 최종 결정 전 |
| Buffs and Debuffs | 미정 | 미정 | 작성 필요 | 중간 | 미정 | 미정 | 최종 결정 전 |

---

## 12. Interaction UI

### 대상 상호작용

- [ ] `[E] Prompt`
- [ ] Gather Prompt
- [ ] Repair
- [ ] Upgrade
- [ ] Craft
- [ ] Open
- [ ] Talk
- [ ] Enter
- [ ] Storage

### 현재 구현 기록

- Shared `InteractionCanvas`
- `Screen Space Overlay`
- Prompt가 world anchor를 따라감
- Gather progress bar가 world anchor를 따라감
- UI의 화면상 크기는 고정됨
- `InteractionCanvas`는 enabled 상태를 유지함
- 개별 UI root를 show/hide함

> 위 항목은 현재 구현 기록이다. 이 문서는 구현 코드를 변경하지 않는다.

| 편집 필드 | 내용 |
|---|---|
| Prompt 형태 | `작성 필요` |
| Key 표시 스타일 | `작성 필요` |
| Label text | `작성 필요` |
| Icon | `작성 필요` |
| 거리별 동작 | `작성 필요` |
| Animation | `작성 필요` |
| 최종 결정 | `최종 결정 전` |

---

## 13. Large Windows

| Window | 목적 | 주요 정보 | Primary action | Secondary action | Layout | Reference | 최종 결정 |
|---|---|---|---|---|---|---|---|
| Tower Panel | 작성 필요 | 작성 필요 | 미정 | 미정 | 미정 | 참고 이미지 추가 | 최종 결정 전 |
| Fence Panel | 작성 필요 | 작성 필요 | 미정 | 미정 | 미정 | 참고 이미지 추가 | 최종 결정 전 |
| Base Panel | 작성 필요 | 작성 필요 | 미정 | 미정 | 미정 | 참고 이미지 추가 | 최종 결정 전 |
| Storage Panel | 작성 필요 | 작성 필요 | 미정 | 미정 | 미정 | 참고 이미지 추가 | 최종 결정 전 |
| Crafting Panel | 작성 필요 | 작성 필요 | 미정 | 미정 | 미정 | 참고 이미지 추가 | 최종 결정 전 |
| Inventory | 작성 필요 | 작성 필요 | 미정 | 미정 | 미정 | 참고 이미지 추가 | 최종 결정 전 |
| Settings | 작성 필요 | 작성 필요 | 미정 | 미정 | 미정 | 참고 이미지 추가 | 최종 결정 전 |
| Pause | 작성 필요 | 작성 필요 | 미정 | 미정 | 미정 | 참고 이미지 추가 | 최종 결정 전 |
| Result Screen | 작성 필요 | 작성 필요 | 미정 | 미정 | 미정 | 참고 이미지 추가 | 최종 결정 전 |

---

## 14. Tooltips

| 편집 필드 | 내용 |
|---|---|
| Background | `작성 필요` |
| Border | `작성 필요` |
| Title | `작성 필요` |
| Description | `작성 필요` |
| Stat colors | `작성 필요` |
| Resource cost | `작성 필요` |
| Shortcut | `작성 필요` |
| Maximum width | `작성 필요: ___ px` |
| 표시 전 Delay | `작성 필요: ___ ms` |
| 최종 결정 | `최종 결정 전` |

---

## 15. Typography

| 편집 필드 | 내용 |
|---|---|
| Main font | `작성 필요` |
| Heading font | `작성 필요` |
| Body font | `작성 필요` |
| Number font | `작성 필요` |
| Button font | `작성 필요` |
| Font size hierarchy | `작성 필요` |
| Outline | `작성 필요` |
| Shadow | `작성 필요` |
| Letter spacing | `작성 필요` |
| Line spacing | `작성 필요` |
| Capitalization 규칙 | `작성 필요` |
| 한국어 지원 | `확인 필요` |
| English 지원 | `확인 필요` |
| 숫자 가독성 | `확인 필요` |
| 최종 결정 | `최종 결정 전` |

| Use | Size | Weight | Outline | Notes |
|-----|------|--------|---------|-------|
| 화면 제목 | `___ px` | 미정 | 미정 | 작성 필요 |
| Section 제목 | `___ px` | 미정 | 미정 | 작성 필요 |
| 본문 | `___ px` | 미정 | 미정 | 작성 필요 |
| Button | `___ px` | 미정 | 미정 | 작성 필요 |
| Tooltip | `___ px` | 미정 | 미정 | 작성 필요 |
| HUD 숫자 | `___ px` | 미정 | 미정 | 작성 필요 |

---

## 16. Animation

- [ ] Hover
- [ ] Press
- [ ] Pop
- [ ] Bounce
- [ ] Scale
- [ ] Slide
- [ ] Fade
- [ ] Pulse
- [ ] Flash
- [ ] Shake
- [ ] Fill animation
- [ ] Count-up animation
- [ ] Window open
- [ ] Window close

| 편집 필드 | 내용 |
|---|---|
| Duration | `작성 필요: ___ ms` |
| Easing | `작성 필요` |
| Intensity | `작성 필요` |
| 사용 사례 | `작성 필요` |
| 피해야 할 요소 | `작성 필요` |

---

## 17. VFX

| VFX | 사용 위치 | 색상/강도 | 상태 |
|---|---|---|---|
| Sparkle | 작성 필요 | 작성 필요 | 미정 |
| Dust | 작성 필요 | 작성 필요 | 미정 |
| Leaves | 작성 필요 | 작성 필요 | 미정 |
| Repair sparks | 작성 필요 | 작성 필요 | 미정 |
| Upgrade flash | 작성 필요 | 작성 필요 | 미정 |
| Damage flash | 작성 필요 | 작성 필요 | 미정 |
| Healing particles | 작성 필요 | 작성 필요 | 미정 |
| Resource pickup effect | 작성 필요 | 작성 필요 | 미정 |
| Wave warning | 작성 필요 | 작성 필요 | 미정 |
| Night transition | 작성 필요 | 작성 필요 | 미정 |

---

## 18. Audio Feedback

- [ ] Hover
- [ ] Click
- [ ] Confirm
- [ ] Cancel
- [ ] Open
- [ ] Close
- [ ] Upgrade
- [ ] Repair
- [ ] Build
- [ ] Resource pickup
- [ ] Error
- [ ] Warning

| 편집 필드 | 내용 |
|---|---|
| Sound 성격 | `작성 필요` |
| Pitch 범위 | `작성 필요` |
| Volume 우선순위 | `작성 필요` |
| 반복 규칙 | `작성 필요` |

---

## 19. Day and Night UI Variants

게임 흐름은 다음 세 단계로 구성된다.

1. **Day:** 탐험과 자원 수집
2. **Preparation:** 방어 준비
3. **Night:** 기지 방어

> 형태와 Layout은 일관되게 유지한다. 색상, 조명, Contrast, 경고 강도는 시간대에 따라 바뀔 수 있다.

| Element | Day Version | Night Version | Shared Rule |
|---------|-------------|---------------|-------------|
| Panel | 밝은 방향, 미정 | 어두운 방향, 미정 | 형태와 Padding 유지 |
| Border | 선명한 어두운 선, 미정 | 고대비 선, 미정 | 두께 유지 |
| Button | 따뜻한 색, 미정 | 경고 대비 강화, 미정 | 크기와 상태 유지 |
| HP Bar | 미정 | 미정 | 의미와 Icon 유지 |
| Hunger Bar | 미정 | 미정 | 의미와 Icon 유지 |
| Interaction Prompt | 미정 | 미정 | 위치와 입력 표기 유지 |
| Timer | 미정 | 미정 | Layout 유지 |
| Resource Counter | 미정 | 미정 | 순서와 Icon 유지 |
| Warning | 낮은 강도, 미정 | 높은 강도, 미정 | 문구와 형태 일관성 |
| Management Window | 미정 | 미정 | 정보 구조 유지 |

---

## 20. Accessibility and Readability

- [ ] 최소 Text 크기 정의: `___ px`
- [ ] 충분한 Contrast 확인
- [ ] 색각 이상 사용자를 위한 구분 확인
- [ ] 중요 정보에 Icon + Text 함께 사용
- [ ] 색상만으로 정보를 전달하지 않음
- [ ] 여러 화면 해상도에서 Scaling 확인
- [ ] Safe area 적용 규칙 정의
- [ ] Controller와 Keyboard Prompt 모두 지원
- [ ] 향후 Mobile 가독성 요구 가능성 기록

| 항목 | 기준/테스트 | 상태 |
|---|---|---|
| 최소 Text 크기 | `작성 필요` | 미정 |
| Contrast | `작성 필요` | 미정 |
| 해상도 범위 | `작성 필요` | 미정 |
| Safe area | `작성 필요` | 미정 |
| 입력 장치 전환 | `작성 필요` | 미정 |

---

## 21. Unity Production Rules

> 아래 내용은 제작 권장안이다. 실제 적용 전 프로젝트 요구 사항과 테스트 결과를 확인한다.

- [ ] Text에는 TextMeshPro 사용
- [ ] 크기 조절 Panel에는 9-sliced Sprite 사용
- [ ] Background, Border, Icon, Fill, Text Layer 분리
- [ ] 이미지에 Text를 구워 넣지 않음
- [ ] Asset이 안정된 뒤 Sprite Atlas 구성
- [ ] 반복 UI는 재사용 가능한 Prefab으로 제작
- [ ] Pixels Per Unit을 일관되게 유지
- [ ] Naming convention 적용
- [ ] 투명 배경 Asset은 Transparent PNG로 Export
- [ ] Fixed reference resolution 정의
- [ ] Canvas Scaler 규칙 정의
- [ ] Anchor와 Pivot 규칙 정의

### 제작 설정

| 설정 | 값 |
|---|---|
| Reference resolution | `작성 필요: ____ × ____` |
| Canvas Scaler mode | `작성 필요` |
| Match width/height | `작성 필요` |
| Pixels Per Unit | `작성 필요` |
| Naming convention | `작성 필요` |
| Anchor 규칙 | `작성 필요` |
| Pivot 규칙 | `작성 필요` |

### 제안 Prefab 목록

- `UI_Button_Primary`
- `UI_Button_Secondary`
- `UI_Button_Icon`
- `UI_Panel_Small`
- `UI_Panel_Medium`
- `UI_Panel_Large`
- `UI_ProgressBar`
- `UI_ResourceSlot`
- `UI_InventorySlot`
- `UI_Tooltip`
- `UI_InteractionPrompt`
- `UI_ContextPanel`

---

## 22. Reference Images

참고 이미지는 `Docs/Reference/UI/`에 정리한다. 파일명은 내용이 드러나도록 작성하고 공백 대신 `-` 또는 `_`를 사용한다.

```markdown
![Reference name](Reference/UI/example.png)
```

### MapleStory
- 참고 이미지: `참고 이미지 추가`
- 마음에 드는 점: `작성 필요`
- 마음에 들지 않는 점: `작성 필요`
- 적용할 요소: `작성 필요`
- 복사하지 않을 요소: `작성 필요`

### Clash of Clans
- 참고 이미지: `참고 이미지 추가`
- 마음에 드는 점: `작성 필요`
- 마음에 들지 않는 점: `작성 필요`
- 적용할 요소: `작성 필요`
- 복사하지 않을 요소: `작성 필요`

### Dave the Diver
- 참고 이미지: `참고 이미지 추가`
- 마음에 드는 점: `작성 필요`
- 마음에 들지 않는 점: `작성 필요`
- 적용할 요소: `작성 필요`
- 복사하지 않을 요소: `작성 필요`

### Generated Mockups
- 참고 이미지: `참고 이미지 추가`
- 마음에 드는 점: `작성 필요`
- 마음에 들지 않는 점: `작성 필요`
- 적용할 요소: `작성 필요`
- 복사하지 않을 요소: `작성 필요`

### Other References
- 참고 이미지: `참고 이미지 추가`
- 마음에 드는 점: `작성 필요`
- 마음에 들지 않는 점: `작성 필요`
- 적용할 요소: `작성 필요`
- 복사하지 않을 요소: `작성 필요`

---

## 23. Decision Log

결정이 승인될 때마다 번호를 증가시키고, 변경 시 기존 기록을 지우지 말고 새 결정을 추가한다.

### Decision UI-001

- Date: `작성 필요`
- Topic: `작성 필요`
- Status: `미정`
- Decision: `최종 결정 전`
- Reason: `작성 필요`
- Reference: `참고 이미지 추가`
- Affected UI: `작성 필요`
- Follow-up: `작성 필요`

### Decision UI-002

- Date: `작성 필요`
- Topic: `작성 필요`
- Status: `미정`
- Decision: `최종 결정 전`
- Reason: `작성 필요`
- Reference: `참고 이미지 추가`
- Affected UI: `작성 필요`
- Follow-up: `작성 필요`

### Decision UI-003

- Date: `작성 필요`
- Topic: `작성 필요`
- Status: `미정`
- Decision: `최종 결정 전`
- Reason: `작성 필요`
- Reference: `참고 이미지 추가`
- Affected UI: `작성 필요`
- Follow-up: `작성 필요`

---

## 24. Asset Production Checklist

- [ ] 최종 Palette 승인
- [ ] Border 승인
- [ ] Panel 승인
- [ ] Button family 승인
- [ ] Icon style 승인
- [ ] Progress bars 승인
- [ ] Resource slots 승인
- [ ] HUD mockup 승인
- [ ] Interaction prompt 승인
- [ ] Gather bar 승인
- [ ] Tower panel 승인
- [ ] Fence panel 승인
- [ ] Base panel 승인
- [ ] Storage panel 승인
- [ ] Typography 승인
- [ ] Day theme 승인
- [ ] Night theme 승인
- [ ] Unity slicing test 완료
- [ ] Sprite Atlas 생성
- [ ] Prefabs 생성
- [ ] 최종 게임 내 가독성 테스트 완료

---

## 25. Open Questions

- [ ] 전체 UI는 얼마나 귀엽고 얼마나 위험하게 보여야 하는가? 답: `작성 필요`
- [ ] 주간과 야간에 별도 Palette가 필요한가? 답: `미정`
- [ ] Panel의 주재료는 나무, 금속, 혼합 중 무엇인가? 답: `미정`
- [ ] Corner damage는 모든 Panel에 적용하는가? 답: `미정`
- [ ] 외곽선 두께는 해상도별로 어떻게 유지하는가? 답: `작성 필요`
- [ ] HUD에서 항상 보여야 하는 정보는 무엇인가? 답: `작성 필요`
- [ ] Controller Prompt는 어떤 Glyph 체계를 사용하는가? 답: `작성 필요`
- [ ] 추가 질문: `작성 필요`
- [ ] 추가 질문: `작성 필요`
- [ ] 추가 질문: `작성 필요`

---

## 26. Next Actions

- [ ] 생성된 주간 UI Mockup 검토
- [ ] 변경하지 않고 유지할 부분 결정
- [ ] 마음에 들지 않는 요소 목록 작성
- [ ] Main panel background 확정
- [ ] Border 확정
- [ ] Corner treatment 확정
- [ ] 주간 Color palette 확정
- [ ] 야간에 별도 Palette를 사용할지 결정
- [ ] 첫 번째 Production-ready interaction prompt 제작
- [ ] Unity에서 Interaction prompt 테스트

---

## 문서 편집 메모

- `작성 필요`, `미정`, `참고 이미지 추가`, `최종 결정 전`을 검색하면 미완료 항목을 빠르게 찾을 수 있다.
- 승인된 항목만 `승인` 또는 `최종`으로 변경한다.
- 중요한 변경은 [Decision Log](#23-decision-log)에 기록한다.
