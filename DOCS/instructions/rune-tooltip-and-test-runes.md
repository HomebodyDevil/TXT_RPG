# 룬 툴팁·키워드 툴팁과 테스트 룬

## 1. 목적과 확정 사항

`Candidate_MainScene`의 `LeftArea` 룬 Grid에서 룬 카드에 마우스를 올리면 툴팁이 나타나게 한다. 툴팁은 개발자가 설정 자산으로 작성하는 제목·텍스트·이미지 블록으로 구성되며, 본문 안의 키워드에 마우스를 올리면 그 키워드를 설명하는 툴팁이 다시 열린다. 키워드는 이후 룬·아이템·스킬과 연결될 수 있으므로, 키워드의 사용처와 연관 항목을 Editor에서 쉽게 확인할 수 있어야 한다.

함께 게임 결과에 영향을 주지 않는 테스트용 룬을 추가한다. 테스트 룬은 실제 룬과 같은 보유·줄·변경 이벤트 경로를 거치므로, 툴팁과 이후 추가되는 룬 관련 기능을 이 룬으로 확인할 수 있다. 테스트 룬은 개발용 DEBUG 패널에서 세션에 추가하고 제거한다.

이 문서는 후속 구현 지침이며, 적용이나 실행 검증을 완료했다는 기록이 아니다.

### 1.1 사용자가 확정한 사항 (2026-10-25)

| 항목 | 확정 내용 |
| --- | --- |
| 툴팁 위치 | 마우스 위치와 무관하게 **룬 카드(`CardArea`)의 오른쪽 위 모서리**를 툴팁 왼쪽 위 모서리의 고정 기준점으로 쓴다. 카드의 어느 지점에서 호버해도 툴팁 위치는 같다. 툴팁은 카드 오른쪽에 붙어 아래로 펼쳐지므로 카드와 툴팁이 한 변을 맞댄다. |
| 화면 경계 | 툴팁 상자는 16:9 화면 프레임(`ScreenFrame`) 안에만 놓인다. 오른쪽이나 아래쪽으로 넘치면 넘치는 축의 기준점을 반대쪽으로 뒤집는다(예: 오른쪽이 넘치면 카드 왼쪽 위 모서리가 툴팁의 오른쪽 위 모서리가 된다). 뒤집어도 넘치면 프레임 안으로 밀어 넣는다. 레터박스나 화면 밖으로 나가지 않는다. |
| 표시와 숨김 | 마우스가 룬 카드 위에 있으면 툴팁을 보인다. 카드를 벗어나면 숨긴다. 다만 툴팁이 열린 상태에서 마우스가 카드를 벗어나 툴팁 상자 위로 이동하면 툴팁을 유지한다. |
| 툴팁 내용 작성 | 키워드는 ID·이름·설명·아이콘·연관 항목을 가진 ScriptableObject로 만든다. 툴팁 본문은 제목·텍스트·이미지 블록 목록으로 구성한다. 텍스트 안에 `{kw:키워드ID}` 표기를 넣으면 키워드 강조 표시와 호버가 자동으로 연결된다. 키워드 Inspector에서 그 키워드를 쓰는 룬·아이템·스킬 목록을 볼 수 있게 한다. |
| 키워드 중첩 | 키워드 툴팁 안의 키워드에도 다시 툴팁이 열린다. 최대 깊이를 설정으로 제한하고(기본 3), 이미 열린 사슬에 같은 키워드가 있으면 다시 열지 않아 순환을 막는다. |
| 입력 범위 | 이번 작업은 **마우스만** 지원한다. 터치와 게임패드는 구현하지 않고, 포인터와 무관한 표시 API를 확장 지점으로 둔다. |
| 테스트 룬의 게임 영향 | 새 효과 종류를 추가한다. 이 종류의 룬 줄은 룬 Grid에 표시되지만, 슬롯 총합 분배와 전투 행동에서 제외한다. 공격·회복 룬의 분배 결과와 Story 기록은 테스트 룬이 없을 때와 같아야 한다. 분배 정책과 효과 종류 enum이 바뀐다. |
| 테스트 룬 관리 | 테스트 룬의 종류를 추가하고 삭제하기 쉬워야 한다. |
| 테스트 룬 표시 조건 | Editor·Development Build의 DEBUG 패널에서 켜고 끈다(추가·제거). 정식 빌드에서는 나타나지 않는다. |

### 1.2 이 문서가 정한 기본값

사용자 답변 이후 결과에 큰 영향이 없어 지침서가 정한 값이다. 구현자는 이 값을 그대로 사용하고, 바꿀 필요가 생기면 근거와 함께 보고한다. 모든 길이는 1920×1080 Canvas 단위이다.

| 항목 | 기본값 |
| --- | --- |
| 호버 판정 영역 | 카드의 `CardArea`와 `BadgeArea`. 두 영역 중 어디에 있어도 같은 룬의 호버로 본다. 셀의 빈 모서리(배지 돌출로 생긴 여백)는 포함하지 않는다. |
| 표시 지연 | 룬 툴팁 `showDelaySeconds` 0, 키워드 툴팁 0.1. 설정으로 바꿀 수 있다. |
| 숨김 유예 | `hideGraceSeconds` 0.08. 카드와 툴팁의 경계를 지날 때 1~2단위의 틈이나 Raycast 순서 때문에 툴팁이 깜빡이지 않게 하는 시간이다. 유예 중 마우스가 사슬 안의 영역으로 돌아오면 숨기지 않는다. |
| 기준점 간격 | 카드 모서리와 툴팁 사이 0. 프레임 가장자리 여백 `frameMargin` 8. |
| 툴팁 크기 | 최대 너비 420, 최소 너비 160. 안쪽 여백 16, 블록 간격 8. 최대 높이는 프레임 높이 − 2 × `frameMargin`이며, 넘치는 내용은 툴팁 안에서 잘리고 Editor·Development Build에서 경고를 한 번 남긴다. |
| 글자 | 제목 26(굵게), 본문 22. 기존 한국어 TMP fallback을 쓴다. |
| 키워드 강조 | 키워드 표시 텍스트를 굵게 하고 밑줄을 긋는다. 색은 보조 수단이다(기본 #FFD27A). 색만으로 키워드를 구분하지 않는다. |
| 키워드 툴팁 기준점 | 마우스 위치와 무관하게 키워드 표시 텍스트 전체 글자 영역(여러 줄이면 합친 사각형)의 오른쪽 위 모서리. 경계 처리는 룬 툴팁과 같다. |
| 최대 키워드 깊이 | `maximumKeywordDepth` 3. 룬 툴팁 1개 + 키워드 툴팁 최대 3개가 동시에 열릴 수 있다. |
| 툴팁 내용이 없는 룬 | 대체 내용(제목 = 룬 표시 이름, 본문 `설명이 없습니다.`)으로 툴팁을 보인다. |
| 새 효과 종류 | `RuneEffectKind.Test = 3`. Story·표시 이름은 `테스트`. |
| 테스트 룬 정의 위치 | 별도 설정 자산 `TemporaryTestRuneConfiguration`. 정의 ID는 `rune.test.`로 시작하고 효과는 `Test`만 허용한다. |
| 기본 테스트 룬 | `rune.test.tooltip`(테스트 룬, 아이콘·이미지 블록·키워드 사슬 포함), `rune.test.long`(긴 테스트 룬, 긴 본문으로 경계·최대 높이 확인), `rune.test.missing`(내용 없는 테스트 룬, 아이콘·툴팁 내용 없음 → 이름 카드와 대체 툴팁 확인) |
| 기본 테스트 키워드 | `keyword.test.alpha` → 설명에 `{kw:keyword.test.beta}`, `keyword.test.beta` → `{kw:keyword.test.gamma}`, `keyword.test.gamma` → `{kw:keyword.test.alpha}`(순환 확인). `alpha`에만 아이콘을 둔다. |
| 테스트 룬 추가 위치 | 같은 정의의 테스트 줄이 있으면 그 줄에 쌓고, 없으면 Loadout 맨 끝에 `line.test.<정의 ID 끝부분>` 줄을 새로 만든다. 인스턴스 ID는 `<정의 ID>.debug.<순번>`이다. |

## 2. 작성 시점에 확인한 현재 상태

### 2.1 룬 Grid (`Candidate_MainScene`)

- 계층은 `Canvas/ScreenFrame/MainArea/ContentLayer/LeftArea/ContentLayer/RuneGrid`이며 `RuneGridPanel.prefab` 인스턴스와 `TemporaryRuneGridBinder`가 붙어 있다([룬 Grid 패널](../architecture/rune-grid-panel.md)).
- 카드(`RuneGridCard.prefab`, `RuneGridCardView`) 하나는 룬 하나가 아니라 **룬 줄(`RuneLoadoutLine`) 하나**이다. 표시 데이터는 `RuneGridEntryDisplay`(`Key` = 줄 ID, `DefinitionId`, `DisplayName`, `StackCount` = Weight)이다.
- 카드와 배지의 모든 Graphic은 `raycastTarget = false`이고, 입력 대상은 `Viewport`의 투명 Image와 Scrollbar뿐이다. 따라서 지금은 카드 호버를 받을 수 없다.
- 카드는 `RuneGridPanel`이 실행 중 풀에서 만든다. Prefab에 카드 인스턴스는 저장되어 있지 않다.
- UI 어셈블리의 룬 Grid는 Gameplay·Application 타입을 쓰지 않는다. 아이콘은 UI 측 `RuneIconCatalog`(`TemporaryRuneIconCatalog.asset`, `rune.attack`·`rune.heal`)에서 찾는다.
- 툴팁, 호버, 키워드를 다루는 기존 구성 요소는 없다. 포인터 이벤트를 쓰는 예로 `ExplorationCardFeedbackController`(`IPointerEnterHandler`·`IPointerExitHandler`)가 있다.
- `Candidate_MainScene`의 `EventSystem`은 `InputSystemUIInputModule`을 사용한다.

### 2.2 룬 도메인과 분배

- `RuneEffectKind`는 `Unknown = 0`, `Attack = 1`, `Heal = 2`이며, `RuneEffectKinds.IsSupported`는 Attack·Heal만 허용한다(`Assets/TxTRPG/Gameplay/Runtime/Runes/Runes.cs`).
- `WeightedRuneAllocationPolicy.Allocate`는 **모든 줄**에 Weight 비율로 총합을 분배한다. 따라서 테스트 룬 줄을 그대로 추가하면 공격·회복 줄의 몫이 줄어든다. `RuneAllocationLine.Order`는 Loadout 안 위치(1부터)이며 Story의 `[룬] N번 줄`에 쓰인다.
- `TemporaryRuneState.CanAllocate`는 `Loadout.LineCount > 0 && Loadout.TotalWeight > 0`이다.
- `RuneCombatActions.ToActionKind`(`TemporarySlotCombatRecords.cs`)는 Attack·Heal 외의 효과에 예외를 던진다. `SlotCombatStoryFormatter.EffectName`은 그 외 효과를 `알 수 없는 효과`로 표시한다.
- `TemporarySlotCombatController.WeightLines(TemporaryRuneState)`가 슬롯 룬 표의 줄 목록을 만든다(`TMP_MainScene` 전투, 탐험 이벤트 슬롯 판정).
- 세션 룬은 `PlayerSessionHost`(`AppRoot.prefab`)의 `temporaryRuneConfiguration`에서 `TemporaryRuneState.TryCreate`로 한 번 만들며 저장하지 않는다. 정의 목록(`RuneDefinitionCatalog`)은 생성 후 바뀌지 않는다.
- 탐험 이벤트 효과(`ExplorationEventEffects.cs`)는 정의 ID로 룬을 지급하고 새 줄을 만들거나 같은 정의의 줄에 쌓는다.

### 2.3 DEBUG 패널

- `Canvas/ScreenFrame/OverlayLayer/EncounterDebugPanel`(`EncounterDebugPanel.prefab`, `DevelopmentBuildOnly`)은 `DebugPanelTabGroup`으로 `이벤트 판정`·`슬롯` Tab을 나눈다. 슬롯 Tab은 Scene의 `SlotReelDockDebugController`(`TxTRPG.Application.SlotMachine`)가 세션 상태를 직접 바꾸는 방식이다([요소 토글과 이벤트 판정](../architecture/encounter-roll.md), [슬롯머신과 룬](../architecture/slot-machine-and-runes.md#슬롯-디버그-tab과-slotreeldockdebugcontroller)).
- `OverlayLayer` 아래 순서는 `SlotReelDockClip`(RectMask2D) > `SlotReelDock`, 그다음 `EncounterDebugPanel`이다. 결과 모달은 `ScreenOverlayLayer`에 있다.

### 2.4 어셈블리 참조

`TxTRPG.UI`는 `TxTRPG.Gameplay`·TMP·uGUI·Input System을, `TxTRPG.Content`는 `TxTRPG.Gameplay`·`TxTRPG.UI`를, `TxTRPG.Application`은 Gameplay·Content·UI를 참조한다. 따라서 툴팁 타입을 `TxTRPG.UI`에 두면 이후 `ItemDefinition`(Content)과 Application의 스킬·룬 설정이 모두 툴팁 내용을 참조할 수 있다.

## 3. 선행 조건

1. `DOCS/development/resolution-and-screen-frame.md`(특히 3.4 모달·팝업·툴팁, 3.5 가독성), `DOCS/development/visual-effect-readiness.md`, `DOCS/architecture/rune-grid-panel.md`, `DOCS/architecture/slot-machine-and-runes.md`의 룬·분배 절을 읽는다.
2. `Candidate_MainScene`이 열려 있고 저장되지 않은 변경이 없는지, Editor가 컴파일 중이 아닌지 확인한다. 작업 전 Git 상태를 기록한다.
3. AGENTS.md 규칙에 따라 편집 전에 `unity-impact-scout`를 호출한다. 이 작업은 직렬화 enum(`RuneEffectKind`)과 분배 정책을 바꾸고 공유 Prefab(`RuneGridCard.prefab`, `RuneGridPanel.prefab`, `EncounterDebugPanel.prefab`, `AppRoot.prefab`)을 수정하므로 `opus` 모델로 호출한다. 조사 대상은 `RuneEffectKind`, `RuneEffectKinds`, `WeightedRuneAllocationPolicy`, `RuneAllocation`, `TemporaryRuneState`, `RuneCombatActions`, `SlotCombatStoryFormatter.EffectName`, `TemporarySlotCombatController.WeightLines`, `PlayerSessionHost`, `RuneGridPanel`, `RuneGridCardView`, `EncounterDebugPanel`이다.

## 4. 작업 범위

### 4.1 포함

1. Gameplay: `RuneEffectKind.Test`와 분배 참여 여부 규칙, 분배 정책·분배 결과의 테스트 줄 제외.
2. Application: 테스트 룬 설정 자산과 세션 카탈로그 병합, 분배 가능 판정·슬롯 룬 표·Story 문구의 테스트 줄 처리, DEBUG 패널 `룬` Tab 컨트롤러.
3. UI: 범용 툴팁 구성 요소(내용 자산, 키워드 자산·카탈로그, 표기 해석기, 툴팁 표시·배치·사슬 관리, 키워드 링크 감지), 룬 툴팁 연결(룬 정의 ID → 툴팁 내용 카탈로그, 카드 호버 판정), DEBUG `룬` Tab 표시.
4. Editor: 키워드 Inspector의 사용처·연관 항목 표시, 툴팁 내용 검증.
5. 자산: 툴팁 Prefab, 테스트 키워드 3개, 테스트 룬 3개의 툴팁 내용, 룬 툴팁 카탈로그, 테스트 룬 설정 자산, 테스트 룬 임시 아이콘 1개.
6. Prefab·Scene 적용: `RuneGridCard.prefab`, `RuneGridPanel.prefab`, `EncounterDebugPanel.prefab`, `AppRoot.prefab`, `Candidate_MainScene`.
7. Edit Mode 테스트, Play Mode 확인용 임시 메뉴, 문서 갱신.

### 4.2 제외

- 터치·게임패드로 툴팁 열기. 룬 Grid 게임패드 포커스. 이번 작업은 5.6의 확장 지점만 둔다.
- 아이템·스킬 등 룬 외 대상의 툴팁 연결. 키워드 자산의 연관 항목 기록과 사용처 표시까지만 한다.
- 툴팁 등장·퇴장 애니메이션과 시각 효과. 효과를 붙일 `VisualRoot`만 준비한다.
- `TMP_MainScene`의 화면 변경. 분배 규칙 변경은 공용이므로 이 Scene의 전투에도 적용되지만, 이 Scene에는 테스트 룬을 추가할 경로가 없다.
- 툴팁 내용의 동적 값(예: 줄 Weight를 본문에 넣는 토큰), 현지화 테이블 연결.
- 테스트 룬 상태의 저장. 룬은 현재처럼 세션 수명 동안만 유지한다.

## 5. 구현 요구 사항

### 5.1 Gameplay: 효과 종류와 분배 (`Assets/TxTRPG/Gameplay/Runtime/Runes/Runes.cs`)

1. `RuneEffectKind`에 `Test = 3`을 추가한다. 기존 값(0, 1, 2)은 바꾸지 않는다. 값 주석에 "분배와 전투 행동을 받지 않는 개발용 효과"임을 적는다.
2. `RuneEffectKinds.IsSupported`가 `Test`를 허용하게 한다. `RuneEffectKinds.ReceivesAllocation(RuneEffectKind)`를 추가하고 Attack·Heal만 `true`를 돌려준다. 이후 분배를 받지 않는 정식 효과(예: 지속 효과)가 생기면 이 함수가 확장 지점이다.
3. `WeightedRuneAllocationPolicy.Allocate`는 정의의 효과가 분배를 받는 줄만 대상으로 삼는다.
   - 분배 대상 줄의 Weight 합 `W`로 기존 최대 나머지 방식을 그대로 적용한다.
   - 결과 `RuneAllocation.Lines`에는 분배 대상 줄만 Loadout 순서대로 넣는다. `Order`는 분배 대상 줄 사이의 순번(1부터)으로 매긴다. 이렇게 하면 테스트 줄이 Loadout 앞이나 중간에 있어도 공격·회복 줄의 순번·분배 수치·Story 문구가 테스트 룬이 없을 때와 같다.
   - 분배 대상 줄이 없거나 `W = 0`이면 기존처럼 `InvalidOperationException`을 던진다.
   - `RuneAllocationLine.Order`의 주석("1-based position in the loadout")을 새 의미로 고친다.
4. `RuneLoadout.TotalWeight`의 의미는 바꾸지 않는다(모든 줄의 Weight 합). 분배 대상 Weight가 필요한 곳은 카탈로그를 받는 함수(예: `RuneLoadout.AllocatableWeight(RuneDefinitionCatalog)` 또는 정책의 정적 함수)를 하나 두고 그것만 쓴다.

### 5.2 Application: 테스트 룬 설정과 세션 상태

1. `TemporaryTestRuneConfiguration`(ScriptableObject, `Assets/TxTRPG/Application/Runtime/Runes/TemporaryTestRuneConfiguration.cs`, 만들기 메뉴 `Assets > Create > TxT RPG > Temporary Test Rune Configuration`)을 추가한다.
   - 필드: `definitions`(`TemporaryRuneDefinitionEntry` 목록). 시작 룬·시작 줄은 두지 않는다.
   - `Validate(out reason)`: 빈 ID, 중복 ID, `rune.test.`로 시작하지 않는 ID, `Test`가 아닌 효과를 거부한다.
   - 기본값과 자산 `Assets/TxTRPG/Application/Configuration/TemporaryTestRuneConfiguration.asset`에는 1.2의 테스트 룬 3개를 넣는다.
2. `PlayerSessionHost`에 `temporaryTestRuneConfiguration` 필드를 추가하고 `AppRoot.prefab`에서 위 자산을 참조한다. 세션 초기화 때 `TemporaryRuneState.TryCreate`에 함께 전달해 테스트 정의를 카탈로그에 병합한다(기존 오버로드는 테스트 설정 없이 동작).
   - 테스트 설정이 비어 있으면 테스트 정의 없이 시작한다. 경고를 남기지 않는다.
   - 테스트 설정이 검증에 실패하거나 ID가 일반 정의와 겹치면 `Debug.LogError`로 사유를 남기고 **테스트 정의만 빼고** 세션 룬을 만든다. 일반 룬과 게임 시작은 막지 않으며 `TemporarySlotCombatError`도 설정하지 않는다.
   - 테스트 정의는 Release Player에서도 카탈로그에 들어가지만, 시작 룬·줄에 없고 효과가 `Test`이므로 게임에 영향이 없다. 정식 빌드에서 테스트 룬을 추가하는 유일한 경로인 DEBUG 패널은 비활성이다. 플랫폼 전처리 지시문을 추가하지 않는다.
   - 테스트 룬 정의 목록과 상태를 확인할 수 있도록 `PlayerSessionHost.TemporaryTestRuneDefinitionIds`(읽기 전용) 같은 공개 속성을 둔다. DEBUG 컨트롤러는 이 목록만 사용한다.
3. `TemporaryRuneState.CanAllocate`는 분배 대상 Weight가 0보다 클 때만 참이다. 테스트 줄만 있으면 행동을 실행할 수 없다.
4. `TemporarySlotCombatController.WeightLines`는 분배를 받지 않는 줄을 슬롯 룬 표에서 뺀다. 룬 표와 분배 결과의 줄 순서·번호가 같아야 한다.
5. `RuneCombatActions.ToActionKind`는 `Test`에 대해서도 예외를 던진다(분배 결과에 나타나지 않으므로 정상 경로에서는 호출되지 않는다). `SlotCombatStoryFormatter.EffectName`은 `Test`를 `테스트`로 표시한다.
6. 탐험 이벤트의 룬 지급 효과는 바꾸지 않는다. 다만 7.1의 검증 테스트로 저장된 이벤트 자산이 `rune.test.` 정의를 지급하지 않음을 확인한다.

### 5.3 UI: 툴팁 내용과 키워드 (`TxTRPG.UI`, `Assets/TxTRPG/UI/Runtime/Tooltips/`, 네임스페이스 `TxTRPG.UI.Tooltips`)

| 타입 | 책임 |
| --- | --- |
| `TooltipBlockKind` | `Title = 1`, `Text = 2`, `Image = 3`, `Separator = 4`. 값을 고정하고 `0`(알 수 없음)은 표시하지 않고 경고한다. 새 블록은 새 값과 새 템플릿으로 추가한다. |
| `TooltipBlock` | `[Serializable]` 블록 하나. `kind`, `text`(Title·Text, `TextArea`), `sprite`·`imageHeight`(Image, 너비는 툴팁 너비에 맞추고 비율 유지). `[SerializeReference]`와 리플렉션 검색은 쓰지 않는다(IL2CPP 코드 제거 대비). |
| `TooltipContent` | ScriptableObject(`Assets > Create > TxT RPG > UI > Tooltip Content`). 블록 목록. 대상(룬·아이템·스킬)을 알지 않는다. 같은 내용을 여러 대상이 공유할 수 있다. |
| `TooltipKeywordDefinition` | ScriptableObject(`... > UI > Tooltip Keyword`). `id`(안정 문자열, 출시 후 변경 금지), `displayName`, `content`(키워드 툴팁에 보일 블록 목록. `TooltipContent` 참조 또는 내장 블록 중 하나로 통일), `icon`(선택), `relatedEntries`(연관 항목 목록). |
| `TooltipRelatedEntry` | `[Serializable]` 연관 항목. `category`(`TooltipSubjectCategory`: `Rune = 1`, `Item = 2`, `Skill = 3`, `Other = 4`)와 대상 ID 문자열. 대상 타입을 직접 참조하지 않아 UI 어셈블리가 Gameplay·Content 정의에 의존하지 않는다. |
| `TooltipKeywordCatalog` | ScriptableObject. 키워드 목록과 ID 조회(`TryGet`). `Validate(out reason)`는 빈 ID·중복 ID를 거부한다. ID는 앞뒤 공백 제거, 대소문자 구분(`Ordinal`). |
| `TooltipMarkup` | 순수 C# 해석기. `{kw:id}`와 `{kw:id\|표시 텍스트}`를 키워드 구간으로 바꾸고, `{{`·`}}`는 중괄호 문자로 표시한다. 표시 텍스트가 없으면 키워드의 `displayName`을 쓴다. 결과는 TMP 서식 문자열(키워드 구간은 `<link="kw:id">` + 1.2의 강조 서식)과 키워드 ID 목록이다. 카탈로그에 없는 ID는 일반 텍스트로 표시하고 Editor·Development Build에서 경고를 한 번 남긴다. 닫히지 않은 표기는 원문 그대로 보인다. 사용자 문자열 안의 TMP 태그는 그대로 허용한다(개발자 작성 내용만 받으므로). |

- 키워드 자산과 테스트 내용은 `Assets/TxTRPG/UI/Tooltips/` 아래에 둔다: `Keywords/`(키워드 자산), `TooltipKeywordCatalog.asset`, `Runes/TemporaryRuneTooltipCatalog.asset`, `Runes/Contents/`(룬별 `TooltipContent`).
- 테스트 키워드와 `rune.test.tooltip` 내용에는 Title·Text(키워드 포함)·Image·Separator 블록이 모두 한 번 이상 들어가야 한다. Image 블록은 기존 임시 룬 아이콘이나 5.8의 테스트 아이콘을 쓴다.
- `rune.attack`·`rune.heal`에도 짧은 툴팁 내용(제목과 한 줄 설명)을 만든다. 정식 문구가 아니라 임시값이라고 내용 자산 이름이나 문서에 표시한다.

### 5.4 UI: 툴팁 표시와 사슬 관리

| 타입 | 책임 |
| --- | --- |
| `TooltipView` | 툴팁 상자 하나(Prefab `Assets/TxTRPG/UI/Prefabs/Tooltips/TooltipView.prefab`). `Show(TooltipContent 또는 블록 목록, 키워드 카탈로그)`, `Clear()`. 블록 템플릿(Title·Text·Image·Separator)을 풀에서 재사용한다. 크기는 내용 기반(최소·최대 너비, 최대 높이)이다. 상자 전체가 Raycast 대상이어야 "툴팁 위에 있으면 유지"를 판정할 수 있다. |
| `TooltipKeywordLinkDetector` | Text 블록에 붙어 `IPointerMoveHandler`·`IPointerExitHandler`로 `TMP_TextUtilities.FindIntersectingLink`를 호출한다. 포인터가 움직일 때만 계산하고 매 프레임 계산하지 않는다. 링크가 바뀔 때만 상위에 알린다. 키워드 글자 영역(링크 글자 사각형의 합)을 Canvas 단위로 돌려준다. |
| `TooltipPresenter` | 툴팁 사슬의 유일한 관리자. `TooltipLayer`에 하나 둔다. 아래 규칙을 구현한다. 키워드 카탈로그·`TooltipView` Prefab·프레임 기준 RectTransform을 직렬화 필드로 받는다. Scene 검색을 하지 않는다. |
| `TooltipPlacement` | 순수 함수. 입력: 기준 사각형(카드나 키워드 글자 영역), 툴팁 크기, 프레임 사각형, 여백. 출력: 툴팁 위치와 뒤집힌 축. 1.1의 규칙(오른쪽 위 기준, 넘치는 축만 뒤집기, 그래도 넘치면 밀어 넣기)을 구현한다. |
| `ITooltipAnchor` | 툴팁을 연 대상의 계약. 기준 사각형(Canvas 단위), 대상이 아직 유효한지, 포인터가 대상 위에 있는지를 제공한다. 룬 카드와 키워드 링크가 구현한다. |

사슬 규칙:

1. 사슬은 깊이 0의 루트 툴팁(룬)과 깊이 1~`maximumKeywordDepth`의 키워드 툴팁으로 이루어진다. 새 루트 툴팁을 열면 기존 사슬을 모두 닫는다.
2. 키워드 툴팁은 바로 위 툴팁의 키워드 위에 포인터가 `showDelaySeconds` 동안 머물면 열린다. 같은 깊이에 이미 다른 키워드 툴팁이 열려 있으면 그것과 그 아래 사슬을 닫고 새로 연다. 최대 깊이에 도달했거나 사슬에 같은 키워드가 이미 열려 있으면 열지 않는다(강조 표시는 유지).
3. 각 툴팁은 "포인터가 자기를 연 대상 위, 자기 상자 위, 자기보다 깊은 툴팁 위 중 하나에 있다"는 조건이 거짓이 되면 `hideGraceSeconds` 뒤에 닫힌다. 유예 중 조건이 다시 참이 되면 닫지 않는다. 툴팁이 닫히면 그보다 깊은 툴팁도 함께 닫힌다.
4. 결과적으로 마우스가 카드에서 툴팁으로, 툴팁에서 키워드 툴팁으로 이동하는 동안 사슬이 유지되고, 사슬 밖의 아무 곳으로 나가면 모두 닫힌다.
5. 대상이 무효가 되면 즉시 닫는다: 카드가 비활성화·재사용되어 `Key`나 `DefinitionId`가 바뀜, 룬 Grid 스크롤로 카드가 움직임, 대상 GameObject 비활성·파괴, 모달이 열려 입력이 막힘(포인터 이탈 이벤트로 처리되는지 확인하고, 처리되지 않으면 Presenter에 `HideAll()`을 두어 호출).
6. 같은 줄의 표시만 갱신되고 정의가 같으면(예: Weight 변화) 툴팁을 닫지 않는다.
7. 포인터 판정은 EventSystem 포인터 이벤트로만 한다. 장치를 직접 읽지 않는다. `InputSystemUIInputModule`의 포인터 종류가 터치이면 툴팁을 열지 않는다(이번 범위가 마우스뿐이고, 터치의 진입·이탈 이벤트로 툴팁이 깜빡이는 것을 막기 위함). 펜은 마우스와 같이 취급한다.
8. 툴팁 배치는 툴팁을 열 때와 툴팁 크기가 바뀔 때만 계산한다. 마우스 이동으로 위치를 다시 계산하지 않는다.
9. 화면 프레임 배율이 바뀌거나(창 크기 변경) 프레임 크기가 바뀌면 열린 사슬을 닫는다. 다시 호버하면 새 배율로 열린다.

### 5.5 계층과 시각 효과 준비

`TooltipView.prefab`:

```text
TooltipView                 배치 루트(Presenter가 위치 지정, 피벗 (0,1)), CanvasGroup, TooltipView
└── VisualRoot              효과 적용 루트(Stretch)
    ├── Background          UiShapeGraphic 둥근 사각형(진한 남색 계열, 테두리 대비 확보), Raycast 대상
    └── Content             VerticalLayoutGroup(안쪽 여백 16, 간격 8), ContentSizeFitter
        ├── TitleTemplate   TMP_Text(비활성 템플릿)
        ├── TextTemplate    TMP_Text + TooltipKeywordLinkDetector(비활성 템플릿)
        ├── ImageTemplate   Image(preserveAspect, 비활성 템플릿)
        └── SeparatorTemplate Image(높이 2, 비활성 템플릿)
```

- 배치 루트와 `VisualRoot`를 분리한다. 크기와 기준점 계산은 효과 적용 전의 배치 루트 사각형으로 한다. 외곽선·발광이 프레임 밖으로 그려질 수 있으므로 `frameMargin`을 0으로 두지 않는다.
- Text 블록 외의 Graphic은 Raycast 대상이 아니다. 상자 영역 판정은 `Background` 하나로 한다.
- 공유 Material 자산을 실행 중 바꾸지 않는다. 키워드 강조는 TMP 서식으로 처리한다.
- `TooltipView.prefab`은 개발자 소유 Prefab이며 Prefab Rebuild 배치에 등록하지 않는다.

Scene 배치:

```text
Canvas/ScreenFrame/OverlayLayer
├── SlotReelDockClip
├── EncounterDebugPanel
└── TooltipLayer            새 오브젝트. Stretch(여백 0), Graphic 없음, TooltipPresenter. OverlayLayer의 마지막 자식
```

- `TooltipLayer`는 `ScreenFrame` 안에 있으므로 툴팁은 레터박스 위에 그려지지 않는다. 프레임 사각형은 `ScreenFrame` RectTransform을 직렬화 참조로 받아 `TooltipLayer` 로컬 좌표로 변환해 쓴다. `Screen.width`·`Screen.height`를 쓰지 않는다.
- 툴팁은 독과 DEBUG 패널 위에, 결과 모달(`ScreenOverlayLayer`) 아래에 그려진다.

### 5.6 룬 Grid 연결과 확장 지점

1. `RuneTooltipCatalog`(UI, `Assets/TxTRPG/UI/Runtime/Runes/RuneTooltipCatalog.cs`, ScriptableObject): 룬 정의 ID → `TooltipContent`. `RuneIconCatalog`와 같은 ID 규칙(앞뒤 공백 제거, `Ordinal`, 중복은 첫 항목, `Validate`)을 따른다. 게임 규칙 데이터(`RuneDefinition`, 설정 자산)에는 툴팁 필드를 추가하지 않는다.
2. `RuneGridCard.prefab`:
   - `CardArea`와 `BadgeArea` 아래에 각각 투명 `HitArea`(Image, alpha 0, Raycast 대상, Stretch)를 추가한다. 시각 효과 루트와 분리된 입력 대상이다.
   - 카드 루트에 `RuneGridCardTooltipTrigger`(`IPointerEnterHandler`·`IPointerExitHandler`, `ITooltipAnchor`)를 추가한다. 기준 사각형은 `CardArea`의 사각형이다. 두 `HitArea`의 진입·이탈을 하나의 호버로 합친다.
   - 트리거는 드래그 관련 인터페이스를 구현하지 않는다. 휠·드래그 이벤트가 부모 `ScrollRect`로 전달되어 세로 스크롤이 계속 동작해야 한다.
3. `RuneGridPanel`에 `tooltipPresenter`와 `tooltipCatalog` 직렬화 필드를 추가하고, 카드를 만들거나 바인딩할 때 트리거에 전달한다. `Candidate_MainScene`의 `RuneGrid` 인스턴스에서 Override로 지정한다(아이콘 카탈로그와 같은 방식). 두 참조가 없으면 경고를 한 번 남기고 툴팁 없이 기존처럼 동작한다.
4. 트리거가 보이는 내용은 `RuneTooltipCatalog`에서 찾고, 없으면 1.2의 대체 내용을 만든다. `DefinitionId`가 빈 카드(`알 수 없는 룬`)도 대체 내용을 보인다.
5. 확장 지점: `TooltipPresenter.ShowRoot(ITooltipAnchor anchor, TooltipContent content)`와 `HideAll()`은 포인터와 무관한 공개 API로 둔다. 이후 게임패드 포커스(`ISelectHandler`·`IDeselectHandler`)나 터치 탭이 같은 API를 호출하도록 만들 수 있다. 이번 작업에서는 이 경로를 연결하지 않으며, 문서에 "터치·게임패드에서는 툴팁을 볼 수 없음"을 현재 제약으로 기록한다.

### 5.7 DEBUG 패널 `룬` Tab

1. UI: `RuneDebugTabView`(`Assets/TxTRPG/UI/Runtime/Runes/`)는 테스트 룬 정의마다 행 하나(이름, 정의 ID, 현재 세션 개수, `+`·`−` 버튼)와 `모두 제거` 버튼, 결과 문구를 표시한다. 행은 템플릿에서 만들어 재사용한다. 표시 데이터 구조만 받으며 Application 타입을 쓰지 않는다. 버튼 이벤트를 `AddRequested(id)`, `RemoveRequested(id)`, `RemoveAllRequested`로 알린다.
2. `EncounterDebugPanel.prefab`에 세 번째 Tab `룬`과 페이지를 추가한다. 기존 `이벤트 판정`·`슬롯` Tab의 순서·초기 선택·동작은 바꾸지 않는다. Tab 버튼 Navigation은 기존 규칙(좌우 이웃 Tab, 아래 = 페이지 첫 컨트롤)을 따르고, 페이지 안 컨트롤의 Navigation 순서를 정한다. 페이지 높이는 기존 Tab과 같은 하한 규칙을 따른다.
3. Application: `TemporaryTestRuneDebugController`(`Assets/TxTRPG/Application/Runtime/Runes/`, `TxTRPG.Application.Runes`)를 `Candidate_MainScene`에 두고 `RuneDebugTabView`를 직렬화 참조로 받는다. `PlayerSessionHost.WhenReady` 후 `TemporaryRunes`와 테스트 정의 목록을 사용한다.
   - 추가: 1.2의 인스턴스 ID 규칙으로 `Inventory.TryAdd` 후, 같은 정의의 테스트 줄이 있으면 `TryAddRuneToLine`, 없으면 `TryAddLine`(맨 끝)을 호출한다. 줄 추가가 실패하면 방금 추가한 인스턴스를 제거해 상태를 되돌린다.
   - 제거: 그 정의의 가장 최근 디버그 인스턴스를 줄에서 빼고(`TryRemoveRuneFromLine`, 마지막이면 줄도 제거) 인벤토리에서 제거한다.
   - `모두 제거`: 테스트 정의의 모든 인스턴스를 같은 방식으로 제거한다. 일반 룬과 일반 줄은 절대 바꾸지 않는다.
   - 실패는 `Try...`의 사유를 결과 문구에 표시하고 상태를 바꾸지 않는다. 세션이 없으면 `세션이 없습니다. AppScene에서 Play를 시작하세요.`를 표시한다.
   - `TemporaryRuneState.Changed`를 구독해 개수를 갱신하고 `OnDisable`·`OnDestroy`에서 해제한다. 설정 자산은 바꾸지 않으며 아무것도 저장하지 않는다.
4. 테스트 룬은 실제 `Inventory`·`Loadout`·`Changed` 경로를 거치므로 룬 Grid, 툴팁, 슬롯 분배, 이후 추가되는 룬 기능이 일반 룬과 같은 방식으로 이 룬을 받는다.

### 5.8 테스트 룬 추가·삭제 절차와 임시 아이콘

- 임시 아이콘 `Assets/TxTRPG/UI/Icons/TemporaryRunes/test.png`(기존 임시 아이콘과 같은 크기·Import 설정, 플라스크나 물음표 같은 단순한 모양, 글자 없음)를 만들어 `TemporaryRuneIconCatalog.asset`에 `rune.test.tooltip`으로 등록한다. `rune.test.long`도 같은 아이콘을 쓰고, `rune.test.missing`은 등록하지 않는다.
- 테스트 룬 종류를 추가하는 절차는 세 자산만 고치는 것이어야 한다: `TemporaryTestRuneConfiguration.asset`에 정의 추가, (선택) `TemporaryRuneIconCatalog.asset`에 아이콘, (선택) `TemporaryRuneTooltipCatalog.asset`에 툴팁 내용. 코드 변경이 필요하지 않아야 한다.
- 삭제는 위 항목을 지우는 것이다. 7.1의 검증 테스트가 카탈로그에 남은 정의 없는 ID를 보고한다(오류가 아닌 경고 수준이면 테스트는 통과하되 목록을 출력).
- 이 절차를 `DOCS/development/workflows.md`에 기록한다.

### 5.9 Editor: 키워드 사용처 확인 (`TxTRPG.UI.Editor`, `Assets/TxTRPG/UI/Editor/Tooltips/`)

1. `TooltipKeywordDefinitionEditor`(Custom Inspector)는 기본 필드 아래에 다음을 읽기 전용으로 표시한다.
   - 연관 항목(`relatedEntries`) 목록: 분류별로 묶어 표시한다.
   - 사용처: 프로젝트의 모든 `TooltipContent`와 다른 키워드에서 `{kw:이 ID}`를 쓰는 곳, 그리고 그 `TooltipContent`를 참조하는 `RuneTooltipCatalog` 항목(룬 정의 ID). 각 항목은 클릭하면 해당 자산을 선택(Ping)한다.
   - 사용처는 `사용처 새로고침` 버튼을 누를 때만 `AssetDatabase.FindAssets`로 계산한다. Inspector를 그릴 때마다 전체 검색하지 않는다.
2. 연관 항목과 실제 사용처가 다르면(연관 항목에 있지만 사용하지 않음, 또는 그 반대) 정보 문구로 알린다. 자동으로 고치지 않는다.
3. 아이템·스킬 툴팁이 생기면 같은 검색에 그 카탈로그 종류를 추가하면 된다. 이 확장 지점을 Editor 코드 주석과 아키텍처 문서에 적는다.
4. 카탈로그·내용 자산을 저장하거나 고치는 동작은 넣지 않는다. 자동 재생성을 `OnValidate`나 컴파일 콜백에 연결하지 않는다.

### 5.10 적용 방식

- 새 Prefab·자산 생성과 기존 Prefab·Scene 수정은 Unity Editor 자동화로 직접 적용하거나, 목적을 밝힌 임시 메뉴 `Tools/TxT RPG/Application/Temporary/Apply Rune Tooltip And Test Runes`로 적용한다. 임시 메뉴는 필요한 부분만 고치고 다시 실행해도 결과가 같아야 하며(이미 있으면 건너뜀), 사용자의 Override·배치를 덮어쓰지 않는다. 전체 재생성은 하지 않는다.
- 적용 대상: `RuneGridCard.prefab`(`HitArea` 2개, 트리거), `RuneGridPanel.prefab`(새 필드는 비워 둠), `EncounterDebugPanel.prefab`(`룬` Tab), `AppRoot.prefab`(`temporaryTestRuneConfiguration`), `Candidate_MainScene`(`TooltipLayer`, `RuneGrid` Override 2개, `TemporaryTestRuneDebugController` 배치와 참조).
- 적용 후 저장된 자산과 Scene 참조를 다시 읽어 확인한다.

### 5.11 Play Mode 확인용 임시 메뉴

`Tools/TxT RPG/Application/Temporary/Probe Rune Tooltips`(Play 중 `Candidate_MainScene`, AppScene에서 시작)를 둔다. 이 메뉴는 다음을 자동으로 확인하고 결과를 Console에 요약한다. 확인이 끝나면 추가한 테스트 룬을 제거해 세션을 원래대로 돌린다.

1. 테스트 룬 3개를 DEBUG 컨트롤러 API로 추가하고 룬 Grid 카드 수가 늘었는지 확인한다.
2. 카드마다 포인터 진입을 주입해 툴팁이 열리고, 진입 지점(카드 왼쪽 위·가운데·오른쪽 아래)과 무관하게 툴팁 위치가 같은지, 툴팁 왼쪽 위가 `CardArea` 오른쪽 위와 일치하는지(뒤집기가 없을 때) 확인한다.
3. 툴팁 사각형이 프레임 사각형 안에 있는지, `rune.test.long`에서 아래쪽 뒤집기 또는 밀어 넣기가 일어나는지 확인한다.
4. 카드 → 툴팁 이동에서 유지, 사슬 밖 이동에서 닫힘을 확인한다.
5. 키워드 사슬을 깊이 3까지 열고, 순환 키워드가 다시 열리지 않는지 확인한다.
6. 같은 회전 결과로 테스트 룬이 있을 때와 없을 때 `TemporaryRuneState.Allocate` 결과(줄 ID·Order·수치)가 같은지 확인한다.

이 메뉴는 확인용 임시 메뉴이며, 지침서 완료와 사용자 확인 후 제거할 수 있다. 포인터 주입 방식이 실제 마우스 동작과 다를 수 있으므로 5.4의 사슬 동작은 실제 마우스로도 한 번 확인한다.

## 6. 호환성과 실패 동작

| 상황 | 동작 |
| --- | --- |
| 기존 `RuneEffectKind` 값 | 0·1·2를 유지한다. 룬은 저장되지 않으므로 저장 데이터 이주는 필요 없다. 이후 저장을 추가할 때 `3 = Test`를 그대로 사용한다. |
| 테스트 룬이 없는 세션 | 분배 결과, `Order`, Story, 슬롯 룬 표가 변경 전과 같다(기존 테스트가 그대로 통과해야 함). |
| 테스트 줄만 있음 | `CanAllocate`가 거짓이 되어 행동 버튼을 실행할 수 없다. 룬 Grid는 테스트 줄을 표시한다. |
| 테스트 설정 없음·잘못됨 | 5.2의 규칙. 일반 룬과 게임은 정상 동작한다. DEBUG `룬` Tab은 "테스트 룬 정의가 없습니다"를 표시한다. |
| 툴팁 참조 없음 | `RuneGridPanel`의 `tooltipPresenter`·`tooltipCatalog`가 비면 경고 한 번 후 툴팁 없이 동작한다. 키워드 카탈로그가 비면 키워드 표기를 일반 텍스트로 표시한다. |
| 내용·키워드 오류 | 알 수 없는 블록 종류, 알 수 없는 키워드 ID, 닫히지 않은 표기는 표시를 막지 않고 경고를 한 번 남긴다. |
| 프레임보다 큰 툴팁 | 최대 높이로 제한하고 넘치는 내용을 자르며 경고를 한 번 남긴다. |
| 모달 열림, Scene 전환, 대상 파괴 | 사슬을 닫는다. 남은 툴팁이 다른 Scene이나 모달 위에 남지 않는다. |
| Release Player | DEBUG 패널이 비활성이므로 테스트 룬을 추가할 수 없다. 툴팁은 일반 룬에서 동작한다. |
| 터치·게임패드 | 툴팁이 열리지 않는다(이번 범위). 룬 Grid 스크롤 등 기존 동작은 바뀌지 않는다. |

## 7. 검증 기준

### 7.1 Edit Mode 테스트

AGENTS.md의 규칙대로 관련 클래스만 필터로 실행하고 `AssetWriting` 범주는 제외한다. 실행 전후 Git 상태를 기록한다.

- 기존: `RuneTests`, `SlotMachineCombatTests`, `SlotFortuneCriticalCombatTests`, `TemporaryRuneGridBinderTests`, `RuneGridPanelTests`, `RuneIconCatalogTests`, `ExplorationEventEffectTests`, `ExplorationEventControllerTests`, `EncounterDebugPanelTests`, `DebugPanelTabGroupTests`, `SlotReelDockDebugControllerTests`.
- 새로 추가할 테스트(클래스 이름은 예시):
  - `RuneTests` 확장: `Test` 값 3 고정, `ReceivesAllocation`, 테스트 줄이 앞·중간·끝에 있을 때 분배 결과가 테스트 줄이 없을 때와 같음(줄 ID·Order·수치), 테스트 줄만 있으면 예외.
  - `TemporaryTestRuneConfigurationTests`: 검증 규칙, 카탈로그 병합, ID 충돌·잘못된 설정에서 일반 룬만으로 생성, `CanAllocate`.
  - `SlotMachineCombatTests` 확장: `WeightLines`가 테스트 줄을 빼고, Story 문구가 바뀌지 않음, `EffectName(Test)`.
  - `TemporaryTestRuneDebugControllerTests`: 추가(새 줄·기존 줄 쌓기), 제거(마지막 인스턴스면 줄 제거), 모두 제거가 일반 룬을 바꾸지 않음, 실패 시 되돌림.
  - `TooltipMarkupTests`: 키워드 표기, 표시 텍스트 지정, 중괄호 이스케이프, 알 수 없는 ID, 닫히지 않은 표기.
  - `TooltipPlacementTests`: 기본 배치, 오른쪽·아래·양쪽 뒤집기, 뒤집어도 넘칠 때 밀어 넣기, 프레임보다 큰 툴팁, 여백.
  - `TooltipPresenterTests`: 사슬 열기·닫기, 유예 시간, 최대 깊이, 순환 차단, 대상 무효화 시 닫기, 터치 포인터 무시.
  - `RuneTooltipCatalogTests`, `TooltipKeywordCatalogTests`: ID 규칙과 검증.
  - `TooltipContentAssetValidationTests`: 저장된 키워드·내용·카탈로그 자산이 검증을 통과하고, 모든 `{kw:}` ID가 카탈로그에 있으며, 탐험 이벤트 자산이 `rune.test.` 정의를 지급하지 않음.

### 7.2 Editor 확인

AppScene에서 Play를 시작하고 `Candidate_MainScene`에서 확인한다.

1. 5.11의 임시 메뉴가 모든 항목을 통과한다.
2. 실제 마우스로 공격·회복 룬과 테스트 룬 3개의 툴팁, 카드 → 툴팁 → 키워드 툴팁 이동, 사슬 밖 이동을 확인한다.
3. DEBUG `룬` Tab에서 `+`·`−`·`모두 제거`가 룬 Grid와 개수 표시에 바로 반영되고, 다른 Tab의 동작이 바뀌지 않는다.
4. 룬 Grid 스크롤이 카드 위에서도 휠로 동작하고, 스크롤하면 툴팁이 닫힌다.
5. `resolution-and-screen-frame.md` 3.6의 화면 크기(1920×1080, 1280×720, 2560×1080, 2400×1080, 1024×768, 2048×1536, Device Simulator 노치 기기 하나 이상)에서 툴팁이 프레임 밖이나 레터박스로 나가지 않는다. 임시로 추가한 Game View 크기는 확인 후 제거한다.
6. `TMP_MainScene`에서 `Tools/TxT RPG/Application/Temporary/Validate Slot Machine Combat`이 통과한다(분배 정책 변경의 회귀 확인).
7. 3.5의 표로 툴팁 글자(22·26)의 휴대폰 계산 크기를 보고에 적는다.

### 7.3 직렬화 변경 검토

적용·테스트 후 `serialization-diff-reviewer`를 작업 전 Git 상태와 함께 호출한다. 의도한 변경은 5.10의 자산과 새 자산뿐이어야 한다. TMP 동적 폰트 아틀라스 증가 등 부수 변경은 보고하고, 작업 전에 수정되지 않았던 파일만 되돌린다.

## 8. 문서 갱신

- 새 문서 `DOCS/architecture/tooltips.md`: 툴팁 구성 요소의 책임 표, 사슬 규칙, 배치 계산, Prefab·Scene 계층, 키워드 표기, 사용처 확인 방법, 확장 지점(아이템·스킬 연결, 터치·게임패드), 현재 제약. `DOCS/README.md`의 문서 목록에 링크한다.
- `DOCS/architecture/rune-grid-panel.md`: `HitArea`·트리거, 툴팁 참조 필드, 입력 대상 설명(기존 "Viewport의 투명 Image와 Scrollbar뿐" 문구), "아직 없는 기능"의 상세 정보 항목.
- `DOCS/architecture/slot-machine-and-runes.md`: `RuneEffectKind.Test`, 분배 대상 규칙과 `Order`의 새 의미, `CanAllocate`, `WeightLines`, 테스트 룬 설정 자산, DEBUG `룬` Tab과 컨트롤러.
- `DOCS/architecture/encounter-roll.md`: 디버그 패널 Tab 구성과 Scene 배치 트리(`TooltipLayer`, `TemporaryTestRuneDebugController`).
- `DOCS/architecture/screen-frame.md`: `OverlayLayer` 아래 `TooltipLayer`.
- `DOCS/development/resolution-and-screen-frame.md` 3.5: 툴팁 글자 크기 행 추가.
- `DOCS/architecture/project-structure.md`: `UI/Runtime/Tooltips/`, `UI/Editor/Tooltips/`, `UI/Tooltips/` 자산 폴더, 새 타입.
- `DOCS/development/workflows.md`: 테스트 룬 추가·삭제 절차(5.8), 툴팁 내용·키워드 작성 방법, 임시 메뉴와 확인 절차, 새 테스트 클래스.
- `DOCS/instructions/README.md`: 이 문서 항목의 완료 상태와 날짜.

## 9. 최종 보고

다음 항목을 구분해 보고한다.

1. 구현 완료 내용(타입, Prefab, 자산, Scene, 임시 메뉴, 테스트, 문서).
2. 이미 수행한 적용과 저장된 파일 목록.
3. 사용자가 수행해야 할 적용 절차가 남아 있으면 `사용자가 수행할 적용 절차` 절에 복사 가능한 `text` 블록으로 정리한다. 남은 절차가 없으면 그렇다고 명시한다.
4. 수행한 검증(테스트 클래스와 결과, Play Mode 확인, 확인한 화면 크기와 입력 방식)과 수행하지 못한 검증(터치·게임패드, 실제 기기, Release·Development Player 빌드 등).
5. 1.2의 기본값에서 벗어난 부분과 그 근거, 휴대폰 기준으로 작아 보이는 요소.
