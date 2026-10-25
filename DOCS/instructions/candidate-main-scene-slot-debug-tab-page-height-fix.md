# Candidate_MainScene DEBUG 패널 `슬롯` Tab 높이 수정

## 1. 목적과 확정 사항

`Candidate_MainScene`의 DEBUG 패널(`EncounterDebugPanel.prefab`)에서 `슬롯` Tab을 선택하면 Tab 버튼 아래에 아무 내용도 보이지 않습니다. 이 문서는 그 원인을 고치고, `슬롯` Tab이 선택된 동안 패널 높이가 `이벤트 판정` Tab을 선택했을 때의 패널 높이 이상이 되도록 하는 작업을 정의합니다.

### 1.1 사용자가 요청한 사항 (2026-10-25)

| 항목 | 내용 |
| --- | --- |
| 증상 | `슬롯` Tab을 선택하면 Tab 버튼 줄만 보이고, Tab 내용(Reel 수, Reel별 확률, 합계, 버튼, 결과 문구)이 보이지 않는다. |
| 높이 | `슬롯` Tab 선택 시 패널 높이(세로 길이)는 `이벤트 판정` Tab 선택 시의 패널 높이 이상이어야 한다. |
| 기존 동작 유지 | 세로 스크롤을 비롯한 기존 슬롯 Tab 동작은 그대로 유지한다. |

### 1.2 이 문서가 정한 기본값

| 항목 | 기본값 |
| --- | --- |
| 기준 높이 | 고정 숫자가 아니라 `Body/Pages/EncounterPage`의 실제 높이(1920×1080 캔버스 단위)를 기준으로 삼는다. `이벤트 판정` Tab의 구성이 나중에 바뀌어도 규칙이 그대로 성립한다. Tab 버튼 줄과 패널 여백은 두 Tab에서 같으므로, 페이지 높이가 기준 이상이면 패널 높이도 기준 이상이 된다. |
| 내용이 기준보다 짧을 때 | 페이지를 기준 높이까지 늘리고, 남는 공간은 내용 아래쪽의 빈 공간으로 둔다. 내용은 위쪽에 붙어 있고 스크롤되지 않는다. |
| 내용이 기준보다 길 때 | 기존 규칙을 그대로 따른다. 내용 높이까지 늘어나되, 패널 아래 가장자리가 부모(`OverlayLayer`) 아래 가장자리에서 `bottomMargin`(16) 위에 머물도록 제한하고, 넘치는 부분은 세로로 스크롤한다. |
| 기준 높이와 아래 여백 제한이 충돌할 때 | 기준 높이를 우선한다(`이벤트 판정` Tab도 같은 높이로 표시되므로). 화면 프레임이 항상 1920×1080 캔버스 단위이므로 현재 구성에서는 발생하지 않는다. |
| `이벤트 판정` Tab | 바꾸지 않는다. `슬롯` Tab 높이에 맞춰 늘리지 않는다. |

## 2. 작성 시점에 확인한 현재 상태

### 2.1 관련 구성

| 대상 | 경로 | 역할 |
| --- | --- | --- |
| `SlotDebugTabView` | `Assets/TxTRPG/UI/Runtime/SlotMachine/SlotDebugTabView.cs` | 슬롯 Tab 표시. `RefreshPageHeight()`가 `SlotPage`의 `LayoutElement.preferredHeight`·`minHeight`를 계산한다. |
| `DebugPanelTabGroup` | `Assets/TxTRPG/UI/Runtime/DebugPanels/DebugPanelTabGroup.cs` | 선택한 Tab의 페이지만 `SetActive(true)`로 켠다. 페이지 내용은 모른다. |
| `EncounterDebugPanel.prefab` | `Assets/TxTRPG/UI/Prefabs/Encounters/EncounterDebugPanel.prefab` | `Body/TabBar`, `Body/Pages/EncounterPage`, `Body/Pages/SlotPage`(`ScrollRect`, `LayoutElement`, `SlotDebugTabView`, `Viewport/Content`). 저장값: `boundsRoot` = 패널 루트, `bottomMargin` 16, `minimumPageHeight` 120, `LayoutElement` 높이 200. |
| `TemporarySlotDebugTabPlayCheck` | `Assets/TxTRPG/Application/Editor/TemporarySlotDebugTabPlayCheck.cs` | AppScene 경유 Play에서 슬롯 Tab을 확인하는 임시 메뉴. |
| `SlotDebugTabViewTests` | `Assets/TxTRPG/UI/Tests/Editor/SlotDebugTabViewTests.cs` | `TallParent_ShowsEveryControlWithoutScrolling`, `ShortParent_LimitsThePageToTheSpaceBelow_AndScrolls` 등. 페이지가 이미 켜진 상태에서 `RefreshPageHeight()`를 직접 호출하는 경우만 다룬다. |

### 2.2 추정 원인 (구현 전에 측정으로 확인할 것)

코드를 정적으로 읽어서 얻은 추정입니다. 구현자는 수정 전에 아래 값을 실제로 측정해서 원인을 확정해야 합니다.

1. Play 시작 시 `SlotReelDockDebugController`가 `SetReels()`를 호출하면 Reel ID가 바뀌므로 `RefreshPageHeight()`가 실행된다. 이때 `SlotPage`는 꺼져 있어서(`이벤트 판정`이 초기 Tab) `Content`의 Layout Group이 비활성 상태이고, `LayoutUtility.GetPreferredHeight(content)`가 0을 반환한다. 그 결과 `max(min(0, ∞), min(0, 120))` = 0이 `LayoutElement.preferredHeight`·`minHeight`에 기록된다.
2. `슬롯` Tab을 누르면 `SlotDebugTabView.OnEnable()`에서 다시 `RefreshPageHeight()`를 호출한다. 그러나 Unity는 부모의 `OnEnable`을 자식보다 먼저 호출하므로, 이 시점에도 `Content`와 그 자식의 Layout Group·`LayoutElement`가 아직 활성화되지 않았을 수 있다. 그러면 선호 높이가 다시 0으로 계산된다.
3. `LateUpdate()`는 부모 높이나 패널 위치가 바뀔 때만 다시 계산하므로, 높이 0이 그대로 남는다. 첫 번째 첨부 이미지처럼 Tab 버튼 줄만 보이는 현상과 일치한다.
4. `TemporarySlotDebugTabPlayCheck`의 2단계는 Tab 전환 직후 높이를 확인하지 않고, 3단계에서 `+`를 누르면 Reel 수가 바뀌어 높이가 다시 계산되므로 이 결함을 발견하지 못한다.

확인할 값: Tab 전환 직후 한 프레임 뒤의 `SlotPage` `LayoutElement.preferredHeight`, `SlotPage` `RectTransform.rect.height`, `LayoutUtility.GetPreferredHeight(Content)`, 패널 루트 높이. 추정과 다른 원인이 확인되면 그 원인을 고치고, 최종 보고에 실제 원인을 적습니다.

## 3. 선행 조건

- `DOCS/development/resolution-and-screen-frame.md`, `DOCS/development/visual-effect-readiness.md`, `DOCS/architecture/slot-machine-and-runes.md`의 슬롯 디버그 Tab 절, `DOCS/instructions/candidate-main-scene-debug-panel-tabs-and-slot-debug.md`를 읽습니다.
- 작업 시작 시 `git status`를 기록합니다. 작성 시점에 `Candidate_MainScene.unity`(RuneGridPanel 인스턴스의 anchor override), 폰트 Fallback 자산, `AddressableAssetSettings.asset`, `DefaultSceneTransitionProfile.asset`, `TemporaryCandidateLayoutProbe.cs`에 사용자의 미커밋 변경이 있었습니다. 이 변경은 커밋·stash·복원하지 않습니다.
- Editor 자동화를 사용하기 전에 연결 상태, 컴파일 상태, 열린 Scene의 미저장 변경을 확인합니다.

## 4. 작업 범위

### 4.1 포함

- 슬롯 Tab이 켜질 때 페이지 높이가 내용 기준으로 올바르게 계산되도록 수정합니다.
- 슬롯 페이지 높이의 하한에 `EncounterPage` 높이를 추가합니다(1.2 기본값).
- `EncounterDebugPanel.prefab`에 새 직렬화 참조가 필요하면 저장된 Prefab에서 필요한 부분만 수정합니다.
- Edit Mode 테스트와 `TemporarySlotDebugTabPlayCheck` 확인 단계를 보강합니다.
- 관련 문서를 갱신합니다.

### 4.2 제외

- `이벤트 판정` Tab의 구성·높이 변경, Tab 디자인 변경, 슬롯 Tab 기능(Reel 수, 확률, 회전, 독 이동) 변경.
- `TMP_MainScene`, `SlotReelDock.prefab`, 화면 프레임 정책 변경.
- Prefab 전체 재생성, 기존 임시 메뉴 삭제.

## 5. 구현 요구 사항

### 5.1 활성화 시점의 높이 계산 (`SlotDebugTabView`)

- 페이지가 비활성(`!gameObject.activeInHierarchy`)일 때 `RefreshPageHeight()`가 0과 같은 잘못된 높이를 기록하지 않도록 합니다. 비활성일 때는 다시 계산이 필요하다는 표시만 남기고, 기존 높이를 덮어쓰지 않습니다.
- `OnEnable()`에서 즉시 계산하지 말고, 자식이 모두 활성화된 뒤(같은 프레임의 `LateUpdate()` 등) 계산합니다. 화면에 높이 0인 프레임이 그려지지 않아야 합니다.
- `LateUpdate()`는 기존처럼 값 비교만 수행하고, 다시 계산이 필요하다는 표시가 있거나 부모 높이·패널 위치·기준 높이가 바뀌었을 때만 계산합니다. 매 프레임 할당이나 Layout 재구성을 추가하지 않습니다.
- 기존 공개 API(`RefreshPageHeight()`, `AvailablePageHeight()`, `PageLayout`, `BoundsRoot`, `BottomMargin`)의 의미는 유지합니다.

### 5.2 기준 높이 하한

- `SlotDebugTabView`에 기준 페이지를 가리키는 직렬화 참조(예: `heightReference`, `RectTransform`)를 추가하고 Prefab에서 `Body/Pages/EncounterPage`에 연결합니다. `DebugPanelTabGroup`은 페이지 내용을 모르는 일반 구성 요소로 유지하고, 이 규칙을 넣지 않습니다.
- 기준 높이는 `EncounterPage`가 마지막으로 배치된 높이(캔버스 단위)입니다. Tab 전환으로 `EncounterPage`가 꺼져도 `RectTransform`의 마지막 크기를 사용할 수 있는지 측정으로 확인합니다. `EncounterPage`가 한 번도 배치되지 않은 경우(예: 초기 Tab을 `슬롯`으로 바꾼 경우)에도 0이나 잘못된 값을 쓰지 않는 방법을 정하고, 선택한 방법을 아키텍처 문서에 적습니다.
- 높이 계산 규칙은 다음과 같습니다.
  - `limited = max(min(preferred, available), min(preferred, minimumPageHeight))` (기존 규칙)
  - `height = max(limited, referenceHeight)` (참조가 없거나 기준 높이를 알 수 없으면 `limited`)
- `height`가 내용보다 크면 `Content`는 위쪽에 붙어 있고 스크롤되지 않아야 합니다. `height`가 내용보다 작으면 기존처럼 세로로 스크롤되어야 합니다.
- 참조가 비어 있어도 예외 없이 기존 규칙으로 동작합니다. `IsValid()`의 필수 항목에 넣을지는 기존 Prefab 호환성을 고려해 정하고 보고합니다.

### 5.3 Prefab 적용

- 저장된 `EncounterDebugPanel.prefab`에서 `SlotPage`의 새 참조만 연결합니다. 다른 값, Scene override, 다른 Prefab은 바꾸지 않습니다.
- Editor 자동화로 직접 적용하는 것을 우선합니다. 임시 메뉴가 필요하면 범위가 좁은 별도 메뉴를 두거나 `TemporaryDebugPanelTabsApply`에 “이미 연결되어 있으면 `kept`” 경로를 추가하고, 재실행해도 안전한지 문서에 적습니다.
- 에디터 전용 설정 메서드(`SetPageBoundsForEditor` 등)를 확장하는 경우 기존 호출부와 테스트를 함께 갱신합니다.

### 5.4 기존 동작 유지

다음 동작은 바뀌지 않아야 합니다.

- Reel 1~10개에서 행 추가·제거, 페이지 세로 스크롤(짧은 부모에서), `minimumPageHeight` 120 하한, 패널 아래 여백 16 유지.
- Tab 전환, 컨트롤러 탐색(Tab 버튼 ↓ 첫 컨트롤, 첫 행 ↑ 선택된 Tab), 회전 중 비활성 규칙, 표시/숨김 버튼.
- `이벤트 판정` Tab의 모양과 높이.

## 6. 호환성과 실패 동작

- 새 직렬화 필드는 기본값(참조 없음)으로도 기존 규칙과 같게 동작해야 합니다. 기존 Prefab·Scene 데이터와 저장 데이터 형식은 바뀌지 않습니다.
- 기준 높이를 계산할 수 없으면 경고 없이 기존 규칙을 사용합니다. 반복 경고 로그를 남기지 않습니다.
- 높이 계산은 캔버스 단위로만 수행하고 `Screen.width`/`Screen.height`를 사용하지 않습니다.

## 7. 검증 기준

### 7.1 Edit Mode 테스트

`SlotDebugTabViewTests`(필요하면 `DebugPanelTabGroupTests`)에 다음을 추가합니다. 실행은 관련 클래스로 필터링하고 `AssetWriting` 카테고리는 제외합니다.

1. 페이지가 꺼진 상태에서 `SetReels()`를 호출한 뒤 페이지를 켜고 다시 계산하면, 페이지 높이가 0이 아니라 내용 높이(또는 제한된 높이)가 된다.
2. 기준 페이지가 내용보다 높으면 페이지 높이 = 기준 높이이고 스크롤되지 않는다.
3. 기준 페이지가 내용보다 낮으면 기존 규칙과 같은 높이가 된다. 짧은 부모에서는 기존처럼 스크롤된다.
4. 참조가 비어 있으면 기존 테스트 결과와 같다.
5. 기존 `SavedPrefab_IsComplete`에서 저장된 Prefab의 새 참조가 `EncounterPage`를 가리키는지 확인한다.

### 7.2 Editor Play Mode 확인

AppScene 경유로 Play를 시작하고 `Candidate_MainScene`이 표시된 상태에서 확인합니다.

1. `TemporarySlotDebugTabPlayCheck`에 Tab 전환 직후(다른 조작 전) 단계를 추가합니다: 한 프레임 뒤 `SlotPage` 높이 > 0, `SlotPage` 높이 ≥ 전환 전 `EncounterPage` 높이, 패널 루트 높이 ≥ `이벤트 판정` Tab 선택 시 패널 루트 높이, 패널 아래 여백 ≥ 16. 기존 단계는 모두 PASS를 유지합니다.
2. Reel 1개(내용이 짧은 경우)와 10개에서 위 조건과 기존 스크롤 조건을 확인합니다.
3. `슬롯` → `이벤트 판정` → `슬롯`으로 여러 번 전환해도 높이가 유지되는지 확인합니다.
4. `resolution-and-screen-frame.md`의 검증 화면 크기 중 16:9 데스크톱, 울트라와이드, 폰, 노치 기기 1종에서 패널이 프레임 안에 있는지 확인합니다.
5. Game View 캡처로 `슬롯` Tab 내용이 보이는지 확인하고 경로를 보고합니다.

### 7.3 직렬화 변경 검토

Prefab을 수정하거나 테스트를 실행한 뒤 `serialization-diff-reviewer`로 의도한 변경(`SlotPage`의 새 참조)과 부수 변경을 구분합니다. 작업 전에 수정되지 않았던 파일만 복원하고, 사용자의 기존 변경은 건드리지 않습니다.

## 8. 문서 갱신

- `DOCS/architecture/slot-machine-and-runes.md`의 슬롯 디버그 Tab 절: 페이지 높이 규칙(기준 높이 하한, 활성화 시점 계산)을 갱신합니다.
- `DOCS/development/workflows.md`: `Slot Debug Tab Play Check`의 확인 단계 설명과, 새 임시 메뉴가 있다면 그 메뉴를 추가합니다.
- `DOCS/instructions/candidate-main-scene-debug-panel-tabs-and-slot-debug.md`는 기록으로 두되, 높이 규칙이 이 문서로 바뀌었다는 한 줄을 추가합니다.
- `DOCS/instructions/README.md`의 이 문서 항목에 구현 결과를 반영합니다.

## 9. 최종 보고

- 측정으로 확정한 실제 원인과 수정 내용.
- 기준 높이를 얻는 방법과 초기 Tab이 `슬롯`일 때의 처리.
- 변경 파일 목록, Prefab에 적용 완료한 내용, 사용자가 실행해야 할 임시 메뉴(있다면 `사용자가 수행할 적용 절차` 절).
- 실행한 Edit Mode 테스트 필터와 결과, Play Mode 확인 결과와 캡처 경로, 확인한 화면 크기.
- 검증하지 않은 대상(Device Simulator 일부 기기, 실제 기기, Player 빌드 등).
