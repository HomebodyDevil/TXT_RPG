# TMP_MainScene의 Story·주사위 패널 위치 교체 후속 정리

## 1. 목적과 작업 범위

사용자가 `TMP_MainScene`의 계층을 직접 바꿨다. `StoryTextPanel`과 `OwnedDicePanel`의 위치를 서로 바꾸고, 상위 패널 이름과 일부 `FlexibleLayoutItem` Weight를 고쳤다. 이 작업은 그 변경으로 생긴 부수 효과를 정리한다.

1. 전투 "행동" 버튼(`CombatActionBoundary`)이 `EnemyDisplayPanelDemo`와 `OwnedDicePanel`의 경계선 위에 놓이게 한다.
2. 이전보다 작아진 `StoryTextPanel`에서 텍스트가 적절히 보이도록, 패널 너비에 따라 글자 크기와 고정 너비 요소를 조절한다.
3. 사용자가 언급하지 않은 나머지 부수 효과(이름 기반 Editor 도구, 검증 메뉴, 좁은 화면 높이, 탐색, 문서)를 3.3의 분석에 따라 고친다.

사용자가 바꾼 계층, 이름, Weight는 의도한 변경이다. 이 작업은 그 변경을 되돌리지 않고, 나머지 코드와 설정을 새 구조에 맞춘다.

이 문서는 구현 지침이다. 아래의 신규 설정과 동작은 예정된 구조이며 아직 구현되었다고 간주하지 않는다. 이 문서를 작성하는 단계에서는 Scene, Prefab, 코드를 수정하지 않았다.

포함 범위는 다음과 같다.

- `CombatActionBoundary`의 패널 참조와 이를 만드는 생성기·적용 메뉴·검증 메뉴·테스트 갱신
- `StoryTextPanel`의 너비 기반 글자 크기 조절과 고정 너비 요소 맞춤
- 옛 패널 이름(`Text_FlexibleLayoutPanel`, `ExplorationAndDicePanel`, `Character_FlexibleLayoutPanel`)에 의존하는 Editor 도구와 검증 정리
- 좁은 화면(`ResponsiveMainScreen`)의 패널 높이 재검토
- 노드 선택 중 가려진 주사위 패널 컨트롤의 탐색 처리
- 관련 테스트, Scene 적용, 문서 갱신

제외 범위는 다음과 같다.

- 사용자가 바꾼 계층, 패널 이름, Weight(`StoryTextPanel` 3, `OwnedDicePanel` 0.7 등) 되돌리기
- 행동 버튼이 주사위 화면 위쪽과 `Title`을 덮는 문제의 해결. 사용자가 겹침을 허용했다.
- `ExplorationNodePanel`의 덮는 범위 변경. 사용자가 `MiddlePanel` 전체를 덮는 현재 방식을 유지하기로 했다.
- `MainScene_VisualProposal.unity`와 `FlexibleLayoutPanel` 샘플 Prefab 재생성
- `.gitignore`, `NotoSansKR-Fallback.asset`, `DOCS/development/workflows.md`의 기존 미커밋 변경. 이 파일들은 이번 작업과 관계없는 사용자 변경이므로 되돌리지 않는다.

## 2. 사용자 확정 사항

| 항목 | 확정 요구 |
| --- | --- |
| 계층 변경 | 사용자가 바꾼 `TMP_MainScene` 계층, 이름, Weight를 유지한다. |
| 행동 버튼 위치 | `EnemyDisplayPanelDemo`와 `OwnedDicePanel`의 경계선 위, 가로 가운데에 둔다. 표시 조건, 외형, 입력 규칙은 기존 `CombatActionBoundary`와 같다. |
| 버튼과 주사위 영역의 겹침 | 겹침을 허용한다. 버튼이 주사위 화면 위쪽 가운데와 `OwnedDicePanel/Title` 일부를 덮어도 된다. 주사위 영역에 여백을 추가하지 않는다. |
| Story 텍스트 | 패널 너비에 따라 본문·발화자 글자 크기를 최소~최대 범위 안에서 자동으로 조절한다. 구분선처럼 고정 너비인 요소는 영역에 맞춘다. 넓은 영역에서는 현재 크기를 유지한다. |
| 노드 선택 화면 | `ExplorationNodePanel`은 지금처럼 `MiddlePanel` 전체를 덮는다. 노드 선택 중에는 적 패널과 주사위 패널이 모두 가려진다. |

## 3. 선행 문서와 확인한 현재 구조

작업 전 `AGENTS.md`, `DOCS/README.md`와 아래 문서를 읽는다.

- `DOCS/architecture/boundary-command-button.md`
- `DOCS/architecture/flexible-layout-panel.md`
- `DOCS/architecture/story-text-panel.md`
- `DOCS/architecture/owned-dice-presentation.md`
- `DOCS/architecture/temporary-combat.md`
- `DOCS/architecture/exploration-history-tree.md`
- `DOCS/development/visual-effect-readiness.md`
- `DOCS/development/workflows.md`

### 3.1 사용자가 바꾼 내용

문서 작성 시 `git diff Assets/Scenes/TMP_MainScene.unity`(커밋 `3cfd96e` 기준)로 확인했다. 구현 직전 다시 확인하고, 다르면 실제 상태를 우선한다.

| 항목 | 이전 | 현재 |
| --- | --- | --- |
| `Main_FlexibleLayoutPanel/ContentLayer`의 첫째 열 | `ExplorationAndDicePanel`(`NodeTreePanel` + `OwnedDicePanel`) | `LeftPanel`(`NodeTreePanel` + `StoryTextPanel`), 같은 오브젝트의 이름 변경 |
| 둘째 열 | `Text_FlexibleLayoutPanel`(`ExplorationNodePanel`, `EnemyDisplayPanelDemo`, `StoryTextPanel`, `CombatActionBoundary`) | `MiddlePanel`(`EnemyDisplayPanelDemo`, `OwnedDicePanel`, `ExplorationNodePanel`, `CombatActionBoundary`), 같은 오브젝트의 이름 변경 |
| 셋째 열 | `Character_FlexibleLayoutPanel` | `RightPanel`, 이름만 변경 |
| `StoryTextPanel`의 `FlexibleLayoutItem.weight` | 1 | 3 |
| `OwnedDicePanel`의 `FlexibleLayoutItem.weight` | 1 | 0.7 (`minimumSize` 200 유지) |

현재 열 Weight는 `LeftPanel` 1(최소 30), `MiddlePanel` 2.5, `RightPanel` 1(최소 30)이다. 따라서 `StoryTextPanel`은 Weight 2.5인 가운데 열에서 Weight 1인 왼쪽 열의 아래쪽(위쪽은 `NodeTreePanel`, Weight 2, 최소 360)으로 옮겨져 영역이 크게 줄었다. `OwnedDicePanel`은 가운데 열의 `EnemyDisplayPanelDemo`(Weight 2) 아래로 옮겨져 영역이 넓어졌다. 각 `ContentLayer`의 실제 형제 순서는 Editor에서 다시 확인한다.

### 3.2 변경 전부터 있던 관련 구조

- `CombatActionBoundary`는 `Assets/TxTRPG/UI/Prefabs/BoundaryCommandButton.prefab`의 인스턴스이며 `MiddlePanel/ContentLayer`의 마지막 형제다. `PanelBoundaryPlacement`가 `firstPanel`·`secondPanel` 두 `RectTransform`의 경계를 계산하고 컨테이너 안으로 보정한다. 경계가 없거나 패널이 겹치면 버튼을 숨긴다.
- `StoryTextPanel`의 글자 크기는 `speakerFontSize`(17), `bodyFontSize`(24) 고정값이고, 구분선은 `separatorWidth`(360) 고정값이다. 스크롤바 폭(18)과 간격(8)이 본문 너비에서 빠진다. 크기 변경 콜백은 갱신 요청만 기록하고 `LateUpdate`에서 한 번 처리한다(`DOCS/architecture/story-text-panel.md`).
- `ResponsiveMainScreen`(`Canvas/MainScreenViewport`)의 `panels`는 `[LeftPanel, MiddlePanel, RightPanel]`, `narrowHeights`는 `[620, 520, 640]`이다. 이 값은 패널 오브젝트 기준이라 이번 변경에서 그대로 남았다. 620은 원래 `NodeTreePanel` + `OwnedDicePanel`을 위한 높이였다.
- `ExplorationNodePanel`은 `MiddlePanel/ContentLayer`에서 Stretch와 `LayoutElement.ignoreLayout`으로 노드 선택 중 가운데 열 전체를 덮는다.
- `OwnedDicePanel` 안에서 `Title`은 위쪽 가로 Stretch(높이 40), `ResultsButton`은 오른쪽 위(120×44), `DiceViewport`는 위쪽 96을 뺀 나머지다.

### 3.3 부수 효과 분석

문서 작성 시 코드와 Scene에서 확인한 부수 효과다. 구현자는 같은 방법(CodeGraph, 이름 문자열 검색, Scene 직렬화 참조 확인)으로 빠진 것이 없는지 다시 확인하고, 추가로 찾은 항목을 보고한다.

| 번호 | 부수 효과 | 근거 | 처리 |
| --- | --- | --- | --- |
| S1 | 행동 버튼이 잘못된 경계를 계산하거나 숨겨진다. | `CombatActionBoundary`의 `secondPanel`이 다른 열로 옮겨진 `StoryTextPanel`을 가리킨다. | 4절 |
| S2 | `Apply Player Dice Roll Menu`와 `Apply Combat Action Boundary Button`이 예외로 실패한다. | `TemporaryDiceMenuBuilder.EnsureBoundaryActionButton`과 `TemporaryCombatActionBoundaryApply`가 `StoryTextPanel`의 부모 `ContentLayer`에 `EnemyDisplayPanel`이 함께 있다고 가정한다. | 4절 |
| S3 | `Validate Combat Action Button (Play Mode)`의 판정과 문구가 옛 구조를 전제로 한다. | `CombatActionBoundaryValidation`이 `Text_FlexibleLayoutPanel` 범위를 검사한다. | 4절 |
| S4 | `Validate Owned Dice Scene`과 이를 호출하는 `Apply Owned Dice To TMP Main Scene`, `Validate Play Flow`가 실패하거나, Apply가 패널을 새로 만들 위험이 있다. | `TemporaryOwnedDiceIntegration`이 `Main_FlexibleLayoutPanel/ContentLayer/ExplorationAndDicePanel` 이름으로 찾고, 그 안에 `NodeTreePanel`과 `OwnedDicePanel`이 함께 있다고 검사한다. Apply는 이름을 찾지 못하면 새 패널을 만드는 경로로 들어간다. | 6.1 |
| S5 | `Constrain Choices To Center`, `Apply Narrow Screen Scroll` 임시 메뉴가 실패한다. | `TemporaryExplorationTreeIntegration`이 `Text_FlexibleLayoutPanel`, `Character_FlexibleLayoutPanel` 이름으로 찾는다. | 6.1 |
| S6 | `Rebuild Main Scene Visual Proposal`이 실패한다. | `MainSceneVisualProposalBuilder`가 `TMP_MainScene`에서 `Text_FlexibleLayoutPanel`, `Character_FlexibleLayoutPanel` 이름으로 찾는다. | 6.1 |
| S7 | 작아진 Story 영역에서 큰 글자와 360 너비 구분선이 넘치거나 줄바꿈이 과도해진다. | 3.2의 고정값 | 5절 |
| S8 | 좁은 화면에서 Story 영역이 매우 작아질 수 있다. | `LeftPanel` 높이 620에서 `NodeTreePanel`이 최소 360을 차지하면 Story에는 Spacing·여백을 빼고 약 260 이하가 남는다. `MiddlePanel` 520에는 적 패널과 최소 200인 주사위 패널이 함께 들어간다. | 6.2 |
| S9 | 노드 선택 중 가려진 주사위 패널의 `ResultsButton`에 컨트롤러·키보드 탐색이 도달할 수 있다. | `ExplorationNodePanel`은 Raycast만 막고 아래 Selectable의 탐색은 막지 않는다. | 6.3 |
| S10 | 행동 버튼과 `OwnedDicePanel`의 탐색 경로가 정의되지 않았다. | `CombatActionBoundary`는 아래·오른쪽 탐색을 `GameMenuPanel`의 System 버튼으로 둔다. 이제 버튼 바로 아래에 주사위 패널이 있다. | 6.3 |
| S11 | 주사위가 행동 버튼 아래에 멈추면 결과 면이 가려질 수 있다. | 사용자가 겹침을 허용했다. | 위험으로 기록만 한다(7절). |
| S12 | 노드 선택 중에도 Story 기록이 보인다. | 이전에는 `ExplorationNodePanel`이 Story를 덮었다. | 개선된 동작으로 보고 문서에 기록한다. |
| S13 | 주사위 영역의 비율과 크기가 바뀌어 주사위가 더 크게 보이고 RenderTexture 크기가 바뀐다. | `DiceArenaLayout`이 현재 비율로 다시 계산하고, RenderTexture는 `maximumTextureDimension`(1024) 상한을 따른다. | 동작 확인만 한다(8절). |
| S14 | 문서가 옛 이름과 구조를 설명한다. | `boundary-command-button.md`, `owned-dice-presentation.md`, `temporary-combat.md`, `flexible-layout-panel.md`, `exploration-history-tree.md`, `workflows.md` 등 | 9절 |

`FlexibleLayoutPrefabBuilder`와 `FlexibleLayoutPanelTests`의 `Text_FlexibleLayoutPanel`·`Character_FlexibleLayoutPanel`은 샘플 Prefab 안의 이름이며 `TMP_MainScene`과 무관하므로 바꾸지 않는다. `TemporaryExplorationTreeIntegration`의 `Apply Tree Panel` 경로(`ContentLayer/NodeTreePanel`)는 이번 변경 이전부터 현재 구조와 맞지 않았으므로 이번 범위에서 고치지 않고 보고만 한다.

## 4. 행동 버튼 경계 변경 (S1~S3)

- `TMP_MainScene`의 `CombatActionBoundary`에서 `PanelBoundaryPlacement.secondPanel`을 `OwnedDicePanel`의 `RectTransform`으로 바꾼다. `firstPanel`(`EnemyDisplayPanelDemo`)과 다른 Override(명령 핸들러, 명령 ID, 라벨, 아이콘, 모양, 효과 설정)는 유지한다.
- 버튼은 `MiddlePanel/ContentLayer`의 마지막 형제를 유지한다. `ExplorationNodePanel`보다 뒤에 그려지는지 확인한다. 노드 선택 중에는 버튼이 표시 조건에 따라 숨겨지므로 충돌하지 않아야 한다.
- `TemporaryDiceMenuBuilder.EnsureBoundaryActionButton`과 `TemporaryCombatActionBoundaryApply`가 `StoryTextPanel` 대신 `OwnedDicePanel`을 두 번째 패널로 사용하도록 바꾼다. 두 패널이 같은 `ContentLayer`의 직계 자식이라는 검사는 유지하고, 오류 문구를 새 구조에 맞춘다. 이미 적용된 Scene에서 다시 실행하면 참조만 맞추고 사용자가 바꾼 외형 설정은 보존한다. 패널 참조가 다른 오브젝트를 가리키는 기존 Scene(이번 경우처럼 `StoryTextPanel`)도 새 참조로 고친다.
- `Apply Player Dice Roll Menu`가 `StoryTextPanel`을 `TemporaryDiceRollMenuController`에 연결하는 부분(전투 기록 출력)은 그대로 둔다. 바뀌는 것은 경계 버튼의 두 번째 패널뿐이다.
- `CombatActionBoundaryValidation`의 범위 검사와 문구를 버튼이 속한 컨테이너(`MiddlePanel`) 기준으로 바꾼다. 패널 이름을 문자열로 하드코딩하지 않고, 버튼의 부모 `FlexibleLayoutPanel`이나 직렬화 참조로 판단한다.
- `MainSceneMenuInputPlayModeTests`, `BoundaryCommandButtonTests`, `TemporaryMenuIconTests` 중 `StoryTextPanel`을 경계 패널로 가정한 부분을 갱신한다.
- 버튼이 주사위 화면 위쪽과 `Title`을 덮는 것은 허용 범위다(2절). 다만 `ResultsButton`(오른쪽 위)과 겹치지 않는지는 확인하고, 좁은 화면에서 겹치면 보고한다.

## 5. StoryTextPanel 너비 기반 글자 크기 (S7)

### 5.1 동작

- `StoryTextPanel`에 너비 기반 글자 크기 조절 옵션을 추가한다. 기준은 스크롤바와 여백을 뺀 실제 본문 영역 너비다.
- 본문과 발화자 글자 크기는 기준 너비 이상에서는 현재 설정값(`bodyFontSize` 24, `speakerFontSize` 17)을 그대로 쓰고, 기준 너비보다 좁아지면 비율에 맞춰 줄이되 최소값 아래로는 내려가지 않게 한다. 최대값은 현재 설정값이다. 넓은 화면에서 글자를 키우지 않는다.
- 기준 너비, 본문 최소 크기, 발화자 최소 크기를 Inspector에서 설정한다. 기본 기준 너비는 변경 전 Story가 있던 가운데 열의 1920×1080 본문 너비를 측정해 정한다. 최소 크기는 가독성을 기준으로 본문 18, 발화자 14를 출발점으로 하고, 1920×1080과 휴대폰 세로·가로에서 확인해 정한 뒤 근거를 보고한다.
- 옵션을 끄면 지금처럼 고정 크기를 쓴다. 기존 Scene과 Prefab의 직렬화 값은 의미를 바꾸지 않는다. 새 옵션의 기본값은 `TMP_MainScene`의 Story 인스턴스에서 켜고, 다른 Scene에 있는 Story 인스턴스에는 영향이 없도록 Prefab 기본값을 정한다. Prefab 기본값을 켤지 여부와 그 영향(다른 Scene)을 확인하고 보고한다.
- 구분선은 `separatorWidth`와 본문 영역 너비 중 작은 값으로 그린다. 그 밖에 고정 너비로 넘치는 요소가 있으면 같은 방식으로 맞춘다.
- 너비가 바뀌면 이미 표시된 메시지에도 새 크기를 적용한다. 기존의 지연 갱신(`LateUpdate`에서 병합) 규칙을 지키고, Canvas를 즉시 강제 갱신하거나 레이아웃 재진입을 만들지 않는다. 크기가 실제로 바뀐 경우에만 다시 계산하고 매 프레임 할당을 만들지 않는다.
- 최신 메시지 따라가기(`followLatestMessage`), 사용자 스크롤 위치, 위쪽 투명도 계산이 글자 크기 변경 후에도 기존과 같이 동작해야 한다. 사용자가 위로 스크롤한 상태에서 크기가 바뀌어도 위치가 크게 튀지 않게 한다.
- 이후 접근성 글자 배율 설정을 연결할 수 있도록, 최종 크기에 곱할 배율을 받을 수 있는 지점을 둔다. 배율이 커지면 최대값보다 커질 수 있게 한다. 배율 설정 UI는 이번 범위가 아니다.

### 5.2 효과와의 관계

`AGENTS.md`의 Visual Effect Readiness를 지킨다. 글자 크기 조절은 레이아웃 크기만 바꾸고, `StoryTextPanel`의 `BackgroundEffectOverlay`, `ForegroundEffectLayer`, `TransitionOverlay`와 메시지별 TMP 효과 구조를 바꾸지 않는다.

## 6. 그 밖의 부수 효과 정리

### 6.1 이름 기반 Editor 도구 (S4~S6)

- `TemporaryOwnedDiceIntegration`
  - `ValidateScene`은 `ExplorationAndDicePanel` 이름 대신 `OwnedDicePanel` 컴포넌트와 그 부모 `FlexibleLayoutPanel`로 구조를 확인한다. 검사 조건을 새 구조에 맞춘다: `OwnedDicePanel`의 부모 패널이 세로 축이고, 같은 `ContentLayer`에 `EnemyDisplayPanelDemo`가 있으며, 결과 페이지와 바인더가 연결되어 있다. `NodeTreePanel`과 같은 부모여야 한다는 조건은 없앤다.
  - `ApplyScene`은 `OwnedDicePanel`이 이미 있으면 어디에 있든 새로 만들지 않고 검증만 한다. 이름을 찾지 못했다는 이유로 패널을 새로 만들어 Scene을 바꾸는 경로가 실행되지 않게 한다.
  - `Validate Play Flow`가 `ValidateScene`을 거쳐 새 구조에서 통과하는지 확인한다.
- `TemporaryExplorationTreeIntegration`의 `Constrain Choices To Center`와 `Apply Narrow Screen Scroll`은 이미 적용이 끝난 일회성 임시 메뉴다. 옛 이름 대신 컴포넌트와 구조(예: `ExplorationNodePanel`이 들어 있는 패널, `ResponsiveMainScreen`의 `panels`)로 찾도록 고치거나, 이미 적용된 상태에서는 변경 없이 종료하게 한다. 메뉴를 삭제하지 않는다. 선택한 방식을 기록한다.
- `MainSceneVisualProposalBuilder`는 `TMP_MainScene`에서 옛 이름으로 패널을 찾는다. 이름 대신 구조로 찾도록 고치되, 개선안 Scene을 다시 생성하지는 않는다. 이 생성기를 바꾸면 `AGENTS.md`에 따라 `MainSceneVisualProposalTests`(`AssetWriting`)를 실행할지 판단하고, 실행했다면 관련 없는 자산 diff를 복원한다.
- 위 도구 외에 `TMP_MainScene`의 옛 패널 이름을 문자열로 쓰는 코드가 남아 있지 않은지 다시 검색한다.

### 6.2 좁은 화면 높이 (S8)

- `ResponsiveMainScreen`의 `narrowHeights`(`LeftPanel` 620, `MiddlePanel` 520, `RightPanel` 640)를 새 내용에 맞게 다시 정한다. `LeftPanel`에서 `NodeTreePanel`(최소 360)을 뺀 뒤에도 Story가 5절의 최소 글자 크기로 몇 줄 이상 보이는지, `MiddlePanel`에서 적 패널과 주사위 패널(최소 200)이 함께 들어가는지 휴대폰 세로·가로에서 측정한다.
- 높이를 바꿔야 하면 Scene의 `narrowHeights` 값만 바꾼다. 사용자가 정한 넓은 화면의 Weight와 최소 크기는 바꾸지 않는다. 바꾼 값과 근거를 보고한다.

### 6.3 탐색과 포커스 (S9, S10)

- 노드 선택 중 `ExplorationNodePanel`에 가려진 `OwnedDicePanel/ResultsButton`과 적 패널의 Selectable에 컨트롤러·키보드 탐색이 도달하지 않게 한다. 가장 단순한 방법을 고르고(예: 가려지는 동안 해당 패널의 `CanvasGroup.interactable`을 끄기), 노드 선택이 끝나면 원래 상태로 돌린다. 마우스·터치 입력은 이미 막혀 있다. 이 처리 때문에 굴림 결과 창, 결과 보기 버튼의 전투 중 동작이 바뀌면 안 된다.
- 행동 버튼의 명시적 탐색 경로를 새 배치에 맞게 다시 정한다. 버튼 바로 아래에 주사위 패널(`ResultsButton`)이 있으므로 위·아래·좌·우 이동이 화면 배치와 맞게 예측 가능해야 한다. `GameMenuPanel`의 System 버튼과의 왕복 경로도 유지한다. 결정한 경로를 문서에 기록한다.
- Story가 왼쪽 열로 옮겨졌으므로 Story 스크롤 영역에 대한 탐색·스크롤 입력(`NestedScrollRectBridge` 포함)이 좁은 화면과 넓은 화면에서 이전과 같이 동작하는지 확인한다.

## 7. 허용한 위험과 기록 (S11, S12)

- 주사위가 행동 버튼 아래에 멈추면 결과 면이 일부 가려질 수 있다. 사용자가 겹침을 허용했으므로 주사위 연출과 구도는 바꾸지 않는다. 결과는 Story와 결과 창에서도 확인할 수 있다는 점과 함께 아키텍처 문서에 알려진 제약으로 기록한다.
- 노드 선택 중 Story 기록이 보이게 된 것은 의도하지 않았지만 바람직한 변화로 기록한다.

## 8. 주요 수정 경로

문서 작성 시 아래 경로를 확인했다.

- `Assets/Scenes/TMP_MainScene.unity`: `CombatActionBoundary.secondPanel`, 행동 버튼 탐색, `StoryTextPanel` 글자 크기 옵션, 필요 시 `ResponsiveMainScreen.narrowHeights`
- `Assets/TxTRPG/UI/Runtime/StoryTextPanel.cs`와 메시지 표시(`StoryMessageItem`): 너비 기반 글자 크기, 구분선 너비 맞춤
- `Assets/TxTRPG/Application/Editor/TemporaryDiceMenuBuilder.cs`: `EnsureBoundaryActionButton`
- `Assets/TxTRPG/Application/Editor/TemporaryCombatActionBoundaryApply.cs`
- `Assets/TxTRPG/Application/Editor/CombatActionBoundaryValidation.cs`
- `Assets/TxTRPG/Application/Editor/TemporaryOwnedDiceIntegration.cs`: `ApplyScene`, `ValidateScene`
- `Assets/TxTRPG/Application/Editor/TemporaryExplorationTreeIntegration.cs`
- `Assets/TxTRPG/Application/Editor/MainSceneVisualProposalBuilder.cs`
- 노드 선택 중 탐색 차단을 둘 위치: `ExplorationRunController` 또는 UI 측 컴포넌트. UI 어셈블리가 Application을 참조하지 않게 한다.
- 테스트: `Assets/TxTRPG/UI/Tests/Editor/StoryTextPanelTests.cs`, `BoundaryCommandButtonTests.cs`, `Assets/TxTRPG/UI/Tests/PlayMode/MainSceneMenuInputPlayModeTests.cs`, `StoryTextPanelLifecyclePlayModeTests.cs`, `Assets/TxTRPG/Application/Tests/Editor/TemporaryMenuIconTests.cs`, `MainSceneDefaultPresentationTests.cs`(구조 가정 여부 확인)

## 9. 적용

- 작업 시작 시 Editor 연결, 컴파일 상태, 열린 Scene·Prefab의 미저장 변경을 확인한다. `TMP_MainScene`의 미커밋 변경은 사용자 변경이므로 보존하고, 적용 전에 Git 상태를 기록한다.
- Scene은 필요한 값만 바꾼다. 사용자가 바꾼 계층, 이름, Weight, RectTransform을 재생성하거나 되돌리지 않는다. 생성기나 적용 메뉴로 Scene을 바꾸는 경우에도 결과 diff가 4절, 5절, 6절의 값으로만 한정되는지 확인한다.
- `StoryTextPanel` Prefab 원본을 바꾸면 이 Prefab을 쓰는 다른 자산(문서 작성 시 확인: `Assets/Scenes/MainScene_VisualProposal.unity`, `Assets/TxTRPG/UI/DEMO/StoryTextPanelDemo.prefab`)에서 결과가 달라지지 않는지 확인한다.
- 적용 후 저장된 Scene에서 `CombatActionBoundary`의 두 패널 참조, 탐색 경로, Story 옵션, `narrowHeights`를 다시 확인한다.

## 10. 검증

1. 1920×1080, 울트라와이드, 좁은 데스크톱, 태블릿, 휴대폰 세로·가로에서 행동 버튼의 중심이 `EnemyDisplayPanelDemo`와 `OwnedDicePanel`의 경계선 가운데에 있고 잘리지 않는지 확인한다. `ResultsButton`과의 겹침 여부를 기록한다.
2. 전투 없음 → 노드 선택 → 전투 중 → 굴림 중 → 결과 창 → 전투 종료 흐름에서 행동 버튼의 표시·비활성·숨김이 이전과 같은지 확인한다.
3. `Apply Player Dice Roll Menu`, `Apply Combat Action Boundary Button`을 적용된 Scene에서 다시 실행해 참조만 맞춰지고 사용자 설정과 계층이 보존되는지 확인한다.
4. `Validate Owned Dice Scene`, `Validate Play Flow`, `Validate Combat Action Button (Play Mode)`, `Validate Tree Panel`이 새 구조에서 통과하는지 확인한다. `Apply Owned Dice To TMP Main Scene`이 새 패널을 만들지 않는지 확인한다.
5. Story 패널 너비를 바꿔 가며 글자 크기가 기준 너비 이상에서는 그대로이고, 좁아지면 줄어들되 최소값 아래로 내려가지 않는지 확인한다. 구분선이 넘치지 않는지, 이미 표시된 메시지에도 적용되는지, 최신 메시지 따라가기와 사용자 스크롤 위치가 유지되는지 확인한다. 이를 Edit Mode 테스트(크기 계산)와 Play Mode 확인으로 나눠 검증한다.
6. 휴대폰 세로·가로에서 좁은 화면 페이지의 각 패널 높이와 Story 표시 줄 수, 주사위 패널 최소 크기를 확인한다.
7. 노드 선택 중 컨트롤러·키보드로 가려진 `ResultsButton`이나 적 패널에 도달하지 않는지, 노드 선택이 끝나면 다시 도달할 수 있는지 확인한다. 행동 버튼의 탐색 경로를 확인한다.
8. 주사위 굴림을 여러 번 실행해 넓어진 주사위 영역에서 주사위가 화면 안에 멈추고 결과 면이 확정 결과와 같은지 확인한다(`Validate Throw Presentation`). 버튼 아래에 멈춘 사례가 있으면 기록한다.
9. `AGENTS.md`의 Edit Mode 테스트 범위 규칙에 따라 `StoryTextPanelTests`, `BoundaryCommandButtonTests`, `TemporaryMenuIconTests`, `OwnedDiceIntegrationTests` 등 관련 클래스만 필터로 실행하고, `AssetWriting` 범주는 6.1의 생성기 변경과 관련된 경우에만 실행한다. `MainSceneMenuInputPlayModeTests`, `StoryTextPanelLifecyclePlayModeTests`를 실행한다. Editor Play 시작 정책 때문에 Play Mode 테스트가 실행되지 않으면 그 사실과 대체 검증을 보고한다.
10. AppScene 시작 경로로 `TMP_MainScene`에 진입해 키보드·마우스, 컨트롤러, 터치로 위 흐름을 확인한다. 사용 가능한 대상 빌드의 컴파일을 확인하고, 직렬화 diff에서 사용자 변경 외의 의도하지 않은 변경이 없는지 확인한다. 실제 모바일 기기 확인이 불가능하면 미검증으로 명시한다.

## 11. 문서 갱신

- `DOCS/architecture/boundary-command-button.md`: 사용처 경로(`MiddlePanel/ContentLayer/CombatActionBoundary`), 두 패널(`EnemyDisplayPanelDemo`, `OwnedDicePanel`), 탐색 경로, "가리는 정보" 항목(주사위 화면·`Title` 겹침 허용, 결과 면이 가려질 수 있는 제약)을 갱신한다.
- `DOCS/architecture/owned-dice-presentation.md`: `ExplorationAndDicePanel`의 2:1 배치 설명을 새 위치(`MiddlePanel`, 적 패널 아래, Weight 0.7·최소 200)와 노드 선택 중 가려지는 동작으로 바꾼다.
- `DOCS/architecture/story-text-panel.md`: 너비 기반 글자 크기 옵션, 구분선 너비 맞춤, 접근성 배율 연결 지점을 기록한다.
- `DOCS/architecture/temporary-combat.md`, `DOCS/architecture/flexible-layout-panel.md`, `DOCS/architecture/exploration-history-tree.md`: 옛 패널 이름과 배치 설명을 갱신한다. 노드 선택 중 Story가 보인다는 점과 가려진 컨트롤의 탐색 차단을 기록한다.
- `DOCS/architecture/project-structure.md`: 새 컴포넌트를 추가하면 갱신한다.
- `DOCS/development/workflows.md`: 바뀐 Editor 도구의 동작, 검증 절차, Story 글자 크기 설정 방법을 갱신한다. 이 파일에 있는 사용자의 기존 미커밋 변경(프로젝트 서브에이전트 절)은 보존한다.
- 옛 지침서(`DOCS/instructions/`)는 수정하지 않는다.

## 12. 최종 보고

다음을 구분해서 보고한다.

- 3.3의 부수 효과별 처리 결과와, 구현 중 추가로 찾은 부수 효과
- 구현 완료 항목과 실제 타입·경로
- Editor에 이미 적용하고 저장한 Scene·Prefab과 그 검증 결과
- 사용자가 실행해야 할 남은 적용 단계. 남은 수동 Tools 단계가 있으면 `AGENTS.md`에 따라 `사용자가 수행할 적용 절차` 절에 정확한 메뉴 경로, 순서, 전제 조건, 저장 여부, 성공 기준을 `text` 코드 블록으로 제공한다. 없으면 없다고 명시한다.
- 실제로 수행한 테스트와 확인, 확인하지 못한 기기·빌드
- Story 글자 크기의 기준 너비·최소값과 근거, Prefab 기본값 변경 여부
- `narrowHeights` 변경 여부와 근거, 행동 버튼과 노드 선택 중 탐색 처리 방식
