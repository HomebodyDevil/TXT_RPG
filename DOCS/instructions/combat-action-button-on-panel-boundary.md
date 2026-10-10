# 전투 "행동" 버튼의 패널 경계선 배치

## 1. 목적과 작업 범위

현재 임시 전투의 "행동" 버튼(`temporary-dice.roll-all` 명령)은 System·Bag·Status 버튼과 함께 `GameMenuPanel`의 메뉴 영역에 있다. 이 작업은 "행동" 버튼을 `TMP_MainScene`의 `EnemyDisplayPanel`과 `StoryTextPanel` 사이 경계선 위로 옮긴다. 새 버튼은 `FlexibleLayoutPanel`의 공간 배분에 참여하는 항목(Area)이 아니라, 두 패널의 경계선 위에 겹쳐 놓이는 독립 버튼이다. 기존 메뉴 영역의 "행동" 버튼은 제거한다.

이 문서는 구현 지침이다. 아래의 신규 컴포넌트, 계층, 설정은 예정된 구조이며 아직 구현되었다고 간주하지 않는다. 이 문서를 작성하는 단계에서는 Scene, Prefab, 런타임 코드를 수정하지 않았다.

포함 범위는 다음과 같다.

- 경계선 위에 놓이는 새 "행동" 버튼과, 레이아웃이 바뀌어도 경계선을 따라가는 위치 계산
- 전투 중에만 표시하는 규칙과 실행 가능 상태 연결
- 원·사각형 등으로 바꿀 수 있고 이후 다른 모양을 추가할 수 있는 버튼 외형 구조
- 노이즈·글리치·테두리(outline) 같은 효과 셰이더를 나중에 적용할 수 있는 확장 지점
- 기존 메뉴 "행동" 버튼과 그 바인딩 제거, 생성기·아이콘 도구·테스트 갱신
- 키보드·마우스, 컨트롤러, 터치 입력과 포커스 처리
- 관련 테스트, Scene 적용, 문서 갱신

제외 범위는 다음과 같다.

- 실제 노이즈·글리치·외곽선 셰이더와 Material 제작. 이번 작업은 효과를 붙일 수 있는 구조까지만 만든다.
- 전투 규칙, 주사위 굴림 연출, 결과 적용 순서 변경
- System·Bag·Status 메뉴 버튼의 배치나 외형 변경
- `GameMenuScreen` 등 `TMP_MainScene` 이외 화면의 변경. 현재 "행동" 버튼은 `TMP_MainScene`에만 있다.

## 2. 사용자 확정 사항

| 항목 | 확정 요구 |
| --- | --- |
| 위치 | `EnemyDisplayPanel`과 `StoryTextPanel`의 경계선 위, 가로 가운데에 버튼 중심을 둔다. |
| 레이아웃 참여 | 새 버튼은 `FlexibleLayoutItem`과 무관하며 `FlexibleLayoutPanel`의 Area로 계산되지 않는다. 버튼 때문에 두 패널의 크기나 배분이 바뀌면 안 된다. |
| 표시 조건 | 진행 중인 전투가 있을 때만 표시한다. 노드 선택 화면(`ExplorationNodePanel`)과 전투 종료 후에는 숨긴다. 굴림 연출과 결과 처리 중에는 보이되 비활성 상태다. |
| 아이콘·이미지 | 아이콘은 기존 "행동" 아이콘(action, 주사위)을 그대로 쓴다. 기존 이미지 교체 규칙을 그대로 따른다. |
| 버튼 모양 | 버튼 자체의 모양을 원, 사각형 등으로 설정할 수 있어야 한다. 이후 다른 모양을 추가할 수 있어야 한다. |
| 효과 | 노이즈, 글리치, 테두리·외곽선 같은 효과(셰이더)를 적용할 수 있어야 한다. |
| 기존 버튼 | 메뉴 영역의 기존 "행동" 버튼은 제거한다. 숨기기만 하면 실행 경로와 포커스 대상이 두 개로 남고, 생성기가 버튼을 다시 만들 수 있기 때문이다. |

## 3. 선행 문서와 확인한 현재 구조

작업 전 `AGENTS.md`, `DOCS/README.md`와 아래 문서를 읽는다.

- `DOCS/architecture/flexible-layout-panel.md`
- `DOCS/architecture/temporary-combat.md`
- `DOCS/architecture/quick-items-and-game-windows.md`
- `DOCS/architecture/exploration-history-tree.md`
- `DOCS/architecture/exploration-node-tree.md`
- `DOCS/architecture/story-text-panel.md`
- `DOCS/architecture/enemy-display-panel.md`
- `DOCS/development/workflows.md`
- `DOCS/development/visual-effect-readiness.md`: `AGENTS.md`의 "Visual Effect Readiness" 절이 가리키는 문서다. 7절의 외형과 효과 확장 지점은 이 문서의 구조 규칙, Material·상태 관리, 가독성·품질 단계 규칙을 따른다.

문서 작성 시 현재 작업 트리에서 다음을 확인했다. 구현 직전 다시 확인하고, 다르면 실제 상태를 우선한다.

- `TMP_MainScene`의 계층은 `Canvas/MainScreenViewport/Main_FlexibleLayoutPanel/ContentLayer` 아래에 `Text_FlexibleLayoutPanel`, `ExplorationAndDicePanel`, `Character_FlexibleLayoutPanel`이 있는 구조다. `Main_FlexibleLayoutPanel`과 하위 패널들은 `Assets/TxTRPG/UI/Prefabs/FlexibleLayoutPanel.prefab` 등의 Prefab 인스턴스이며, 하위 구성 대부분은 Scene에서 추가한 Prefab 인스턴스와 Override다.
- `Text_FlexibleLayoutPanel/ContentLayer`에는 `ExplorationNodePanel`, `EnemyDisplayPanelDemo`(Prefab 인스턴스, 안에 `TemporaryEnemyNextAction`과 `TemporaryEnemyHealthBar`), `StoryTextPanel` Prefab 인스턴스가 있다. `Text_FlexibleLayoutPanel`의 `fixedAxis`는 1(`Vertical`)로 Override되어 있다. 따라서 넓은 화면에서 `EnemyDisplayPanelDemo`가 위, `StoryTextPanel`이 아래에 세로로 배치되며, 두 패널 사이에 가로 경계선이 생긴다. 구현 전 실제 형제 순서, 각 `FlexibleLayoutItem` 설정, Spacing을 다시 확인한다.
- `ExplorationNodePanel`은 이미 `Text_FlexibleLayoutPanel/ContentLayer` 안에서 Stretch와 `LayoutElement.ignoreLayout`으로 형제 패널의 공간 배분에 참여하지 않는다(`DOCS/architecture/exploration-history-tree.md`). 이것이 새 버튼과 같은 "레이아웃에 참여하지 않는 오버레이" 방식의 선례다. `ExplorationRunController`는 노드 선택 중에 이 패널을 켜고, 전투 노드 진행 중에는 끈다.
- `FlexibleLayoutPanel` 계층은 `BackgroundLayer`, `BackgroundContentLayer`, `ContentLayer`, `ForegroundLayer`(`Border`)이며, 형제 순서가 렌더링 순서다. 각 레이어 자체는 `LayoutElement.ignoreLayout`으로 루트 계산에서 빠진다. `ContentLayer`의 `FlexibleContentLayoutGroup`은 직계 자식만 계산한다.
- 좁은 화면에서는 `ResponsiveMainScreen`이 `Main_FlexibleLayoutPanel`을 세로 페이지로 바꾸고, 각 패널에 `narrowHeights`의 고정 높이를 적용해 `pageScroll` 안에서 스크롤한다. `Text_FlexibleLayoutPanel` 내부의 세로 배치는 그대로이므로 좁은 화면에서도 경계선이 있다.
- 기존 버튼은 `GameMenuPanel/Content/TemporaryDiceRoll`(150×48, `VisualRoot` 아래 `Background`, `Icon`, `Label`, `Border`, `EffectOverlay`, `Button`, `GameMenuButtonView`)이다. `GameMenuPanel.buttons`에 `GameMenuButtonActionKind.Command`, `commandId = temporary-dice.roll-all`인 바인딩으로 등록되어 있다.
- 실행은 `GameMenuPanel.ExecuteCommand` → `IGameMenuCommandHandler.CanExecute/TryExecute`이며, 핸들러는 `TemporaryDiceRollMenuController`(같은 GameObject)다. 컨트롤러는 실행 가능 상태가 바뀔 때 `menu.RefreshExecutionState()`를 호출한다.
- 버튼과 바인딩은 `TemporaryDiceMenuBuilder.EnsureActionButton`(메뉴 `Tools/TxT RPG/Application/Temporary/Apply Player Dice Roll Menu`, `ButtonName = "TemporaryDiceRoll"`)이 만들고, 이미 있으면 재사용한다. 아이콘은 `TemporaryMenuIconUtility.ApplyDefault(view, "action")`가 적용한다. 이 도구는 Sprite가 비어 있거나 확인된 옛 기본 가방 이미지일 때만 바꾸고, 사용자가 지정한 Sprite와 tint·padding은 보존한다(`DOCS/architecture/quick-items-and-game-windows.md`). `GameMenuButtonView`는 Sprite나 참조가 없으면 Label을 대신 표시한다.
- 기존 버튼을 참조하는 코드: `TemporaryMenuIconUtility.ApplyToMenu`(메뉴 바인딩의 명령 ID로 "action" 키를 고름), `TemporaryMenuIconTests`, `MainSceneMenuInputPlayModeTests`(메뉴 바인딩에서 행동 버튼을 찾음), `TemporaryOwnedDiceIntegration`(컨트롤러의 `CanExecute/TryExecute`를 직접 호출).
- 절차적 도형은 `Assets/TxTRPG/UI/Runtime/Exploration/ExplorationCardShapeGraphic.cs`(`ExplorationCardShape`: Rectangle, RoundedRectangle, Circle, Ellipse, Triangle / `Fill`, `InnerBorder`)와 `ExplorationCardShapePresentation.cs`(Image 모드와 Procedural Shape 모드)에 있다. 현재는 탐험 카드 전용 네임스페이스(`TxTRPG.UI.Exploration`)다.

CodeGraph로 `GameMenuPanel`, `GameMenuButtonView`, `TemporaryDiceRollMenuController`, `TemporaryDiceMenuBuilder`, `TemporaryMenuIconUtility`, `ExplorationCardShapeGraphic`, `FlexibleLayoutPanel`, `ResponsiveMainScreen`, `ExplorationRunController`의 호출자와 테스트를 먼저 탐색하고, 실제 소스와 Scene 직렬화로 확인한다.

## 4. 배치

### 4.1 계층과 렌더링 순서

- 새 버튼은 `Text_FlexibleLayoutPanel/ContentLayer`의 마지막 형제로 두고 `LayoutElement.ignoreLayout = true`를 적용한다. `ExplorationNodePanel`과 같은 방식이며, 두 패널 위에 그려지고 공간 배분에는 참여하지 않는다. `FlexibleLayoutItem`을 붙이지 않는다.
- 다른 위치(예: `Text_FlexibleLayoutPanel/ForegroundLayer`)가 더 적합하다고 판단되면 이유를 기록한다. 어느 쪽이든 버튼이 두 패널보다 앞에 그려지고, `ContentLayer`의 클리핑(`Clip Content`)이 켜져도 버튼이 잘리지 않아야 한다.
- 버튼 때문에 `Text_FlexibleLayoutPanel`, `EnemyDisplayPanelDemo`, `StoryTextPanel`의 크기·위치·Weight가 바뀌지 않는지 버튼이 있을 때와 없을 때를 비교해 확인한다.

### 4.2 경계선 위치 계산

- 버튼의 중심은 두 패널 사이 경계의 가운데에 둔다. 경계는 위 패널(`EnemyDisplayPanelDemo`)의 아래쪽 끝과 아래 패널(`StoryTextPanel`)의 위쪽 끝 사이의 중간선이다. Spacing이 0이면 두 끝이 같은 선이고, Spacing이 있으면 그 간격의 가운데다. 가로 위치는 두 패널이 겹치는 가로 범위의 가운데다.
- 위치는 고정 픽셀 값으로 정하지 않는다. 두 패널의 `RectTransform`을 직렬화 참조로 받는 작은 배치 컴포넌트를 두고, 두 패널의 실제 Rect에서 경계를 계산한다. 이름 검색이나 Scene 검색으로 패널을 찾지 않는다.
- 다음 상황에서 다시 계산한다: 화면 크기·비율 변경, 넓은 화면과 좁은 화면 전환(`ResponsiveMainScreen`), Weight·Spacing 변경, 안전 영역 변경, 패널 활성 상태 변경. 크기가 바뀌었을 때만 계산하고 매 프레임 할당을 만들지 않는다. `FlexibleLayoutPanel` 재배치보다 늦게 적용되어 한 프레임 어긋나지 않는지 확인한다.
- `Text_FlexibleLayoutPanel`의 축이 나중에 가로로 바뀌면 경계는 세로선이 된다. 이 경우에도 두 패널 사이 중간선의 가운데에 놓이도록 축에 따라 계산한다. 두 패널 중 하나라도 비활성이거나 크기가 0이면 경계가 없으므로 버튼을 숨긴다.
- 버튼의 크기는 기존 메뉴 버튼과 같은 기본값에서 시작하고 Inspector에서 조정할 수 있게 한다. 터치 대상으로 충분한 최소 크기를 보장한다. 좁은 화면과 휴대폰 비율에서도 버튼이 `Text_FlexibleLayoutPanel` 밖으로 잘리지 않아야 한다.

### 4.3 가리는 내용 확인

- 버튼은 경계선을 사이에 두고 위 패널 아래쪽과 아래 패널 위쪽을 일부 가린다. `TemporaryEnemyHealthBar`, `TemporaryEnemyNextAction`, 적 그림, `StoryTextPanel`의 최신 문단처럼 꼭 읽어야 하는 정보가 계속 가려지는지 넓은 화면, 좁은 화면, 휴대폰·태블릿 비율에서 확인한다.
- 계속 가려지는 정보가 있으면, 해당 패널의 기존 여백·Padding 설정으로 피하는 방법을 먼저 검토한다. 패널 Prefab의 구조를 바꿔야 하면 범위와 이유를 기록하고 보고한다.

## 5. 표시 조건과 실행 상태

- 표시: 진행 중인 전투(`TemporaryDiceRollMenuController.Combat`가 있고 `IsComplete`가 아님)가 있고 컨트롤러가 준비된 상태일 때만 표시한다. 노드 선택 중(`ExplorationNodePanel` 표시), 전투 없음, 전투 종료(승리·패배)에는 숨긴다.
- 실행 가능 여부: 기존 `CanExecute(RollAllCommandId, out reason)` 결과를 그대로 `interactable`에 반영한다. 굴림 연출, 결과 처리, 결과 창 열림, 다른 게임 창 열림 중에는 보이되 비활성이다.
- 갱신 시점: 지금 `menu.RefreshExecutionState()`를 호출하는 모든 지점과 전투 시작·종료·초기화 지점에서 새 버튼의 표시와 실행 가능 상태도 갱신한다. 폴링으로 상태를 맞추지 않는다.
- 실행: 버튼 입력은 기존과 같은 `IGameMenuCommandHandler.CanExecute/TryExecute(RollAllCommandId)` 경로를 쓴다. 명령 ID, 실행 잠금, 결과 1회 적용, 시간 제한 규칙은 바꾸지 않는다.
- 의존 방향: 새 버튼 컴포넌트는 UI 어셈블리(`TxTRPG.UI`)에 두고, 핸들러는 기존 UI 인터페이스 `IGameMenuCommandHandler`나 그와 같은 수준의 UI 인터페이스로 받는다. UI 어셈블리가 Application 어셈블리를 참조하지 않게 한다. 컨트롤러는 새 버튼을 직렬화 참조로 받는다.
- 굴림 중 Scene 전환, 세션 교체, 재진입 시에도 기존처럼 `PlayerSessionHost`의 상태에 다시 연결된 뒤 표시 상태가 올바르게 복원되어야 한다.

## 6. 입력과 포커스

- 마우스 클릭, 터치 탭, 컨트롤러·키보드의 `Submit`으로 실행한다. 키보드 문자 라벨을 하드코딩하지 않는다.
- 컨트롤러·키보드 탐색에서 새 버튼에 도달할 수 있어야 한다. 기존에는 `GameMenuPanel`의 메뉴 버튼 탐색 안에서 도달했으므로, 메뉴 버튼 ↔ 새 버튼 사이의 `Navigate` 경로를 명시적으로 정하고 기록한다. 자동 탐색에 맡겨 예측할 수 없는 경로가 생기지 않게 한다.
- 전투가 시작될 때 새 버튼을 자동 선택할지 여부는 기존 정책을 따른다. 노드 카드의 "초기 자동 선택 없음" 정책(`DOCS/instructions/exploration-card-no-initial-selection.md`)과 충돌하지 않게 한다. 정책을 바꿔야 하면 구현 전에 사용자에게 확인한다.
- 새 버튼이 선택된 상태에서 숨겨지면(전투 종료 등) 포커스를 숨겨진 오브젝트에 남기지 않는다. 다음 포커스 대상은 기존 탐험 화면의 포커스 규칙을 따른다.
- 결과 창을 닫을 때 포커스를 복원하는 기존 동작(`OwnedDiceSessionBinder`의 `pendingFocus`)이 새 버튼으로 돌아오는지 확인한다.
- 좁은 화면에서 새 버튼이 선택되면 `ResponsiveMainScreen.Reveal`이 페이지 스크롤로 버튼을 보이게 하는지 확인한다.

## 7. 외형

### 7.1 계층과 아이콘

- 기존 메뉴 버튼과 같은 역할 분리를 유지한다: 입력과 크기를 맡는 Root(`Button`), 시각 효과용 `VisualRoot`, 그 아래 배경(모양), 아이콘, 라벨(대체 표시), 테두리, 효과 오버레이. 시각 변화가 Root의 입력 영역이나 배치 크기를 바꾸지 않게 한다.
- 아이콘은 기존 "action" 기본 아이콘(`Assets/TxTRPG/UI/Icons/TemporaryMenu/`)을 쓴다. 기존 `GameMenuButtonView`를 재사용하거나 같은 규칙을 따르는 뷰를 만든다. Sprite·참조 누락이나 아이콘 표시 해제 시 "행동" 라벨을 표시하는 대체 규칙을 유지한다.
- 이미지 교체 규칙을 그대로 따른다: 임시 아이콘 도구는 빈 Sprite만 기본 아이콘으로 채우고, 사용자 Sprite와 tint·padding은 덮어쓰지 않는다. 런타임은 저장된 Sprite를 기본값으로 덮어쓰지 않는다. `TemporaryMenuIconUtility`가 새 버튼에도 같은 규칙으로 적용되도록 갱신한다.

### 7.2 버튼 모양

- 버튼 배경의 모양을 Inspector에서 고를 수 있게 한다. 최소한 원과 사각형(둥근 사각형 포함)을 지원한다.
- 모양 방식은 두 가지를 둔다.
  - 절차적 도형: 텍스처 없이 메시로 그린다. 기존 `ExplorationCardShapeGraphic`의 도형 생성(Rectangle, RoundedRectangle, Circle, Ellipse, Triangle, 채우기·내부 테두리)을 재사용한다.
  - 이미지: 디자이너가 만든 Sprite(9-slice 포함)를 배경과 테두리로 쓴다. 코드 변경 없이 새 모양을 추가하는 경로다.
- 도형 생성 코드를 버튼에서 재사용할 때 버튼이 `TxTRPG.UI.Exploration`의 카드 전용 타입에 직접 의존하지 않도록, 도형 생성 부분을 공용 UI 도형 타입으로 분리하는 것을 기본 방향으로 한다. 분리할 때 기존 탐험 카드의 직렬화 값과 Prefab 참조가 깨지지 않게 한다(타입·필드 이름 변경 시 `MovedFrom`·`FormerlySerializedAs` 등으로 호환을 유지하고 카드 Prefab을 검증한다). 분리하지 않고 다른 방식을 쓰면 이유를 기록한다.
- 이후 절차적 도형을 추가할 때 한 곳(도형 종류와 메시 생성)만 바꾸면 버튼과 카드에 모두 쓸 수 있게 한다. 모양별 분기를 버튼 컴포넌트 여러 곳에 흩어 두지 않는다.
- 입력 영역: 원처럼 사각형이 아닌 모양에서는 입력 영역이 보이는 모양을 따르는 것을 기본으로 한다(절차적 도형은 도형 경계 판정, 이미지 모드는 사각형을 기본으로 하고 선택적으로 알파 판정). 어떤 모양이든 4.2의 최소 터치 크기는 보장한다. 선택한 방식과 이유를 기록한다.
- 기본 모양은 원으로 하되, 기존 아이콘 크기와 라벨 대체 표시가 잘리지 않는 크기를 기본값으로 정한다. 기본값을 바꾸면 이유를 기록한다.

### 7.3 효과 확장 지점

이 절의 요구는 `AGENTS.md`의 "Visual Effect Readiness"와 `DOCS/development/visual-effect-readiness.md`를 이 버튼에 적용한 것이다. 두 문서와 이 절이 다르면 더 엄격한 쪽을 따르고 차이를 보고한다.

- 배치를 맡는 Root와 효과가 변형할 수 있는 `VisualRoot`를 분리하고, 배경·아이콘·라벨·테두리·전경 효과에 각각 독립된 루트를 둔다. 흔들림·확대·모자이크 같은 효과가 `VisualRoot`를 움직여도 경계선 배치(4.2)와 입력 영역이 바뀌지 않아야 한다.
- 정렬과 크기는 효과 적용 전의 레이아웃 영역을 기준으로 한다. 테두리·외곽선·글로우가 차지할 여백을 미리 확보하고, Sprite 여백이나 애니메이션 위치를 기준으로 정렬하지 않는다.
- 배경, 테두리, 아이콘, 효과 오버레이 각각에 Material을 지정할 수 있게 한다. 노이즈·글리치·외곽선 같은 셰이더 효과는 이 지점에 Material로 붙인다.
- 효과가 런타임 속성을 바꿔야 하면 공유 Material을 직접 수정하지 않고, 뷰가 소유하는 인스턴스 Material이나 `MaterialPropertyBlock`에 해당하는 방식을 쓴다. 효과를 제거하거나 버튼을 비활성화·파괴할 때 효과 상태를 원래대로 되돌리고, 만든 인스턴스는 해제한다. 전투가 끝나 숨겼다가 다시 표시할 때 이전 효과 상태가 남지 않아야 한다.
- 효과는 입력 영역, 배치 크기, 실행 가능 상태 표시를 바꾸지 않는다. 비활성 상태가 효과 때문에 구분되지 않으면 안 된다. 상태는 색만이 아니라 투명도·아이콘 대비 등으로도 구분한다.
- 움직이는 효과(노이즈, 글리치 등)는 끌 수 있어야 한다. 기존 "움직임 줄이기" 설정과 같은 경계에서 켜고 끌 수 있는 연결 지점을 두고, 품질 단계에 따라 생략해도 버튼 사용에 문제가 없게 한다.
- 실제 효과 셰이더는 이번 범위에서 만들지 않는다. 확장 지점이 동작하는지는 프로젝트의 기본 UI Material이나 검증용 임시 Material로 확인하고, 임시 자산은 남기지 않는다.

## 8. 기존 "행동" 버튼 제거

- `TMP_MainScene`의 `GameMenuPanel/Content/TemporaryDiceRoll` 오브젝트와 `GameMenuPanel.buttons`의 `temporary-dice.roll-all` 명령 바인딩을 제거한다. System·Bag·Status 버튼, 바인딩, 순서, 외형은 바꾸지 않는다.
- 제거 후 `GameMenuPanel`의 메뉴 레이아웃(열·행 계산, 가운데 정렬, 스크롤)과 `ResponsiveMainScreen`의 좁은 화면 메뉴 높이 계산이 버튼 세 개 기준으로 올바른지 확인한다.
- `GameMenuPanel.commandHandlerBehaviour` 연결은 다른 명령이 없으면 남길지 정리할지 확인하고 기록한다. 남기더라도 사용되지 않는 명령 버튼이 다시 생기지 않아야 한다.
- `TemporaryDiceMenuBuilder.EnsureActionButton`이 메뉴 버튼을 다시 만들지 않도록 바꾼다. 생성기가 새 경계선 버튼을 만들거나 찾도록 하고, 기존 메뉴 버튼과 바인딩이 있으면 제거하는 이행 처리를 둔다. 이미 적용된 Scene에서 다시 실행해도 결과가 같아야 하고(재실행 안전), 사용자가 바꾼 새 버튼의 모양·Sprite·효과 설정을 덮어쓰지 않아야 한다.
- 별도 임시 적용 메뉴를 추가하는 편이 더 안전하면 그렇게 해도 된다. 이때 `AGENTS.md` 규칙에 따라 목적, 임시 여부, 재실행 가능 여부, 저장 여부를 문서에 기록한다.
- `TemporaryMenuIconUtility.ApplyToMenu`의 "명령 바인딩 → action 키" 경로가 더 이상 메뉴에 없는 버튼을 전제로 하지 않게 정리하고, 새 버튼에 같은 아이콘 규칙을 적용하는 경로를 둔다.

## 9. 주요 수정 경로

문서 작성 시 아래 경로를 확인했다.

- `Assets/Scenes/TMP_MainScene.unity`: `Text_FlexibleLayoutPanel/ContentLayer`에 새 버튼 추가, `GameMenuPanel/Content/TemporaryDiceRoll`과 명령 바인딩 제거, 컨트롤러 참조 연결
- `Assets/TxTRPG/Application/Runtime/Dice/TemporaryDiceRollMenuController.cs`: 새 버튼 참조, 표시·실행 상태 갱신
- `Assets/TxTRPG/Application/Editor/TemporaryDiceMenuBuilder.cs`: `EnsureActionButton`, `CreateMenuButton`, `ButtonName`
- `Assets/TxTRPG/Application/Editor/TemporaryMenuIconUtility.cs`
- `Assets/TxTRPG/UI/Runtime/Windows/GameMenuPanel.cs`, `GameMenuButtonView.cs`: 재사용 또는 변경 시
- `Assets/TxTRPG/UI/Runtime/Exploration/ExplorationCardShapeGraphic.cs`, `ExplorationCardShapePresentation.cs`: 공용 도형으로 분리할 때
- `Assets/TxTRPG/UI/Runtime/ResponsiveMainScreen.cs`: 좁은 화면 전환과 위치 재계산 연결이 필요할 때
- `Assets/TxTRPG/Application/Tests/Editor/TemporaryMenuIconTests.cs`
- `Assets/TxTRPG/UI/Tests/PlayMode/MainSceneMenuInputPlayModeTests.cs`
- `Assets/TxTRPG/Application/Editor/TemporaryOwnedDiceIntegration.cs`: Scene 검증(`Validate Owned Dice Scene`, `Validate Play Flow`)이 메뉴 버튼을 전제로 하는지 확인

새 런타임 컴포넌트(경계선 배치, 버튼 뷰·모양, 공용 도형)는 `Assets/TxTRPG/UI/Runtime/` 아래 역할에 맞는 위치에 둔다. 위치 계산과 도형 판정처럼 Unity 객체 없이 계산할 수 있는 부분은 순수 계산으로 분리해 Edit Mode 테스트로 검증한다. 필수 내부 참조는 Scene·Prefab에 완성된 상태로 저장하고, 런타임 이름 검색이나 중복 생성으로 누락을 숨기지 않는다.

## 10. 적용

- 작업 시작 시 Editor 연결, 컴파일 상태, 열린 Scene·Prefab의 미저장 변경을 확인하고 사용자 변경을 보존한다. `TMP_MainScene`에 미저장 변경이 있으면 적용 전에 멈추고 보고한다.
- Scene은 필요한 부분만 수정한다. `Main_FlexibleLayoutPanel`과 하위 패널, 자식 순서, RectTransform, `FlexibleLayoutItem`, Prefab Override를 재생성하지 않는다. 새 버튼을 Prefab으로 만들지 Scene 오브젝트로 둘지 정하고 이유를 기록한다. 재사용 가능성을 고려하면 Prefab을 기본 방향으로 하고, 이때 Prefab 일괄 Rebuild 등록 규칙은 재사용 가능한 생성기를 만드는 경우에만 따른다.
- `FlexibleLayoutPanel.prefab`, `StoryTextPanel`, `EnemyDisplayPanel` Prefab 원본은 바꾸지 않는 것을 목표로 한다. 바꿔야 하면 다른 Scene(`GameMenuScreen`, `MainScene_VisualProposal` 등)에 미치는 영향을 확인하고 보고한다.
- 적용 후 저장된 Scene에서 새 버튼의 계층, 참조, `ignoreLayout`, 기존 버튼과 바인딩 제거를 확인한다.

## 11. 검증

1. 넓은 화면, 좁은 화면(`ResponsiveMainScreen` 전환), 휴대폰 세로·가로, 태블릿, 울트라와이드 비율에서 버튼 중심이 경계선의 가운데에 있는지, 버튼이 잘리지 않는지 확인한다. 화면 크기를 굴림 중과 굴림 밖에서 바꿔 본다.
2. 버튼이 있을 때와 없을 때 `EnemyDisplayPanelDemo`, `StoryTextPanel`, `Text_FlexibleLayoutPanel`의 Rect가 같은지 확인한다.
3. 경계선 위치 계산(Spacing 0과 양수, 세로·가로 축, 한쪽 패널 비활성)을 Edit Mode 테스트로 확인한다.
4. 전투 없음 → 전투 노드 선택 → 전투 중 → 굴림 중 → 결과 창 → 전투 종료(승리·패배) → 다음 노드 선택 흐름에서 표시·비활성·숨김이 5절대로 바뀌는지 확인한다. Scene 재진입과 세션 교체 후에도 확인한다.
5. 마우스 클릭, 터치 탭, 컨트롤러·키보드 `Submit`으로 굴림이 한 번만 실행되는지, 빠른 연속 입력이 차단되는지 확인한다. 컨트롤러 탐색 경로, 숨겨질 때의 포커스, 결과 창 닫힘 후 포커스 복원, 좁은 화면 스크롤 노출을 확인한다.
6. 메뉴 영역에 "행동" 버튼이 남아 있지 않고, System·Bag·Status 버튼의 배치·탐색·동작이 이전과 같은지 확인한다.
7. 모양을 원, 둥근 사각형, 사각형, 이미지 모드로 바꿔 표시와 입력 영역을 확인한다. 아이콘 누락 시 라벨 대체, 사용자 Sprite 보존(임시 아이콘 도구 재실행 포함)을 확인한다. 기존 탐험 카드의 도형 표시가 바뀌지 않았는지 확인한다.
8. 검증용 Material을 배경·테두리·오버레이에 지정해 효과 지점이 동작하는지, 공유 Material이 변경되지 않는지, 인스턴스 Material이 해제되는지 확인한다. 움직이는 효과 끄기 연결이 동작하는지 확인한다. `DOCS/development/visual-effect-readiness.md`의 "검증 시 확인 사항"을 이 버튼에 적용한다.
9. 생성기 또는 적용 메뉴를 적용된 Scene에서 다시 실행해 결과가 같고 사용자 설정이 보존되는지 확인한다.
10. `AGENTS.md`의 Edit Mode 테스트 범위 규칙에 따라 `TemporaryMenuIconTests`, 새 배치·도형 테스트, 도형 분리 시 탐험 카드 관련 테스트 등 관련 클래스만 필터로 실행하고 `AssetWriting` 범주는 변경과 관련된 경우에만 실행한다. `MainSceneMenuInputPlayModeTests`를 새 버튼 기준으로 갱신해 실행한다. Editor Play 시작 정책 때문에 Play Mode 테스트가 실행되지 않으면 그 사실과 대체 검증을 보고한다.
11. `Tools/TxT RPG/UI/Dice/Temporary/Validate Owned Dice Scene`, `Tools/TxT RPG/UI/Dice/Temporary/Validate Play Flow`를 실행한다.
12. AppScene 시작 경로로 `TMP_MainScene`에 진입해 위 흐름을 눈으로 확인한다. 사용 가능한 대상 빌드의 컴파일을 확인하고, 직렬화 diff에서 의도하지 않은 Scene·Prefab 변경이 없는지 확인한다. 실제 모바일 기기 확인이 불가능하면 미검증으로 명시한다.

## 12. 문서 갱신

- `DOCS/architecture/temporary-combat.md`: "행동" 버튼의 위치, 표시 조건, 실행 경로를 현재 구현 기준으로 갱신한다.
- `DOCS/architecture/quick-items-and-game-windows.md`: "행동 버튼은 TMP_MainScene에만 존재하고 `temporary-dice.roll-all` 명령을 유지합니다" 등 메뉴의 행동 버튼 설명을 갱신하고, 아이콘 교체 규칙이 새 버튼에도 적용됨을 기록한다.
- `DOCS/architecture/flexible-layout-panel.md` 또는 관련 문서: 레이아웃에 참여하지 않는 경계선 오버레이 버튼의 배치 방식과 위치 계산을 기록한다.
- 공용 도형으로 분리하면 `DOCS/architecture/exploration-node-tree.md`의 카드 도형 설명과 새 공용 도형의 위치를 갱신한다.
- `DOCS/architecture/project-structure.md`: 새 컴포넌트·폴더·Prefab을 추가하면 갱신한다.
- `DOCS/development/workflows.md`: `Apply Player Dice Roll Menu`의 변경된 동작, 새 적용 메뉴(있다면), 버튼 모양·효과 설정 방법, 검증 절차를 갱신한다.
- 이 지침서와 이전 지침서는 수정하지 않는다. 구현 결과는 아키텍처 문서에 기록한다.

## 13. 최종 보고

다음을 구분해서 보고한다.

- 구현 완료 항목과 실제 타입·경로
- Editor에 이미 적용하고 저장한 Scene·Prefab과 그 검증 결과
- 사용자가 실행해야 할 남은 적용 단계. 남은 수동 Tools 단계가 있으면 `AGENTS.md`에 따라 `사용자가 수행할 적용 절차` 절에 정확한 메뉴 경로, 순서, 전제 조건, 저장 여부, 성공 기준을 `text` 코드 블록으로 제공한다. 없으면 없다고 명시한다.
- 실제로 수행한 테스트와 확인, 확인하지 못한 기기·빌드
- 버튼 위치 계층(ContentLayer 또는 ForegroundLayer), Prefab 여부, 기본 크기·모양, 입력 영역 방식, 컨트롤러 탐색 경로의 선택과 이유
- 공용 도형 분리 여부와 탐험 카드 호환성 확인 결과
- 버튼이 가리는 정보의 확인 결과와 조정 내용
