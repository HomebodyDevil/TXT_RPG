# Actions 영역의 Cell 기준 높이와 CharacterDisplayPanel 재배분

## 1. 목적과 확정 사항

`TMP_MainScene`의 `ActionGridPanel`(이하 Actions)은 소비 아이템과 스킬을 배치하여 사용하는 영역이다. 현재 Actions에서는 `ActionGridCell`과 `Scroll View/Viewport` 사이의 위아래 간격이 Cell 크기에 비해 지나치게 크다. 이 작업에서는 Actions의 높이가 Cell 높이와 개발자가 설정하는 `Gap`으로 결정되도록 하고, 그 결과 남거나 부족해지는 높이를 같은 부모 `RightPanel/ContentLayer` 안에서 `CharacterDisplayPanel`만 흡수하도록 한다.

이 문서는 후속 구현 지침이며, 적용이나 실행 검증을 완료했다는 기록이 아니다.

사용자 답변으로 확정한 사항은 다음과 같다.

| 항목 | 확정 내용 |
| --- | --- |
| Gap의 의미 | Cell의 위·아래 변과 `Viewport`의 위·아래 경계 사이의 거리이다. 따라서 `Viewport 높이 = Cell 높이 + 2 × Gap`이다. |
| 바깥 여백 | `Viewport`(정확히는 `Scroll View`)와 `ActionGridPanel` 외곽 사이의 기존 Body Margin(현재 상하 각 18)은 Gap과 별개로 유지한다. |
| 적용 축 | 상하에만 적용한다. 좌우 배치는 현재 SingleRow 정렬, Padding, 슬롯 간격 정책을 그대로 따른다. |
| 재배분 대상 | `CharacterDisplayPanel`만 크기가 변해야 한다. `HealthBarPanel`은 현재 높이로 고정하여 남는 높이를 나눠 받지 않게 한다. |
| 적용 범위 | 우선 `TMP_MainScene`의 Actions 인스턴스에만 적용한다. 다만 이후 실제 Main Scene을 구성할 때에도 같은 구성 요소와 절차로 다시 적용할 수 있어야 한다. 특정 Scene 경로나 오브젝트 이름에 의존하는 구현을 만들지 않는다. |
| Gap 초기값 | 12(Canvas UI 단위)이다. 개발자가 Inspector와 런타임 API에서 변경할 수 있어야 한다. |

### `CharacterDisplayPanel`의 Weight만 수정하는 방식이 적절하지 않은 이유

작성 시점에 Editor에서 확인한 `RightPanel/ContentLayer`의 `FlexibleLayoutItem` 설정은 다음과 같다.

| 순서 | 항목 | Size Mode | Weight | Fixed | Minimum | 1920×1080 실측 높이 |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | `HealthBarPanel` | Weighted | 0.15 | 100 | 0 | 약 49.95 |
| 2 | `CharacterDisplayPanel` | Weighted | 2 | 100 | 0 | 나머지 |
| 3 | `ActionGridPanel` | Fixed | 1 | 100 | 180 | 180 |
| 4 | `GameMenuPanel` | Fixed | 0.2 | 100 | 0 | 100 |

`ContentLayer`의 `FlexibleContentLayoutGroup`은 세로 축(`Fixed Axis = Vertical`)이며 Spacing은 12이다. Actions는 이미 `Fixed` 항목이고, 실제 높이 180은 `Minimum Size = 180`이 `Fixed Size = 100`보다 크기 때문에 결정된 값이다.

`FlexibleLayoutPanel.CalculateSizes`는 Fixed 항목을 먼저 예약한 뒤, 남은 공간을 Weighted 항목에 Weight 비율로 나눈다. 따라서 `CharacterDisplayPanel`의 Weight를 바꿔도 Actions의 높이는 줄어들지 않는다. 필요한 변경은 Actions의 Fixed 높이를 Cell과 Gap에서 계산하는 것이며, Actions가 줄어든 만큼의 공간은 Weighted 항목에 자동으로 돌아간다. 또한 `HealthBarPanel`도 Weighted 항목이므로 그대로 두면 남는 공간의 약 7%(0.15 / 2.15)를 함께 받게 된다. 이 때문에 `HealthBarPanel`을 고정하는 작업이 추가로 필요하다.

### 현재 간격이 큰 원인

| 값 | 현재 설정 |
| --- | --- |
| 표시 방식 | `Display Settings.mode = Balanced`, `flow = SingleRow` |
| Cell 크기 | Balanced `targetCellSize = 64×64`(최소 56, 최대 72 범위 안) |
| Preset Padding | 상하좌우 각 14 |
| 세로 정렬 | `singleRow.verticalAlignment = Center` |
| `ActionGridSurfaceLayout` | `manageHeaderLayout = true`, Header 비활성, `bodyMargins` 상하좌우 각 18 |
| 가로 Scrollbar | `singleRow.scrollbarVisibility = Hidden`(높이 12, 간격 4) |

패널 높이 180에서 Body Margin 18을 위아래로 빼면 `Viewport` 높이는 144이다. 64 높이의 Cell이 세로 중앙에 놓이므로 Cell과 `Viewport` 사이의 간격은 위아래 각각 40이고, Cell에서 패널 외곽까지는 58이다.

확정한 규칙에서 Gap이 12이면 `Viewport`는 88, 패널은 `18 + 88 + 18 = 124`가 된다. 1920×1080 기준으로 Actions가 56 줄어들고, `CharacterDisplayPanel`이 그만큼 커질 것으로 예상한다. 이 수치는 정적 조사에서 계산한 예상값이므로 적용 후 실측으로 확인해야 한다.

## 2. 선행 조사

1. `AGENTS.md`, `DOCS/README.md`, `DOCS/architecture/action-grid-panel.md`, `DOCS/architecture/flexible-layout-panel.md`, `DOCS/architecture/character-display-panel.md`, `DOCS/development/workflows.md`, `DOCS/development/visual-effect-readiness.md`를 읽는다.
2. 작업 전 `git status`를 기록한다. 작성 시점의 작업 트리에는 SlotMachine 관련 파일과 TMP 폰트 자산의 미커밋 변경이 있었다. 이 변경은 이번 작업과 무관하므로 보존한다.
3. `.codegraph/`가 있으면 `codegraph_explore`로 아래 영향 경로의 호출 관계를 먼저 확인하고, 중요한 결과는 실제 소스와 직렬화 자산에서 다시 확인한다. 색인을 임의로 갱신하지 않는다.
4. 공용 `FlexibleContentLayoutGroup`과 MonoBehaviour 구성을 변경하므로 편집 전에 `unity-impact-scout`를 호출한다. Scene 적용 후에는 `serialization-diff-reviewer`를 호출한다.
5. 열려 있는 `TMP_MainScene`이 저장되지 않은 변경을 가지고 있으면 적용 작업을 시작하지 않고 사용자에게 알린다.

### 영향 경로

| 경로 | 확인·변경할 내용 |
| --- | --- |
| `Assets/TxTRPG/UI/Runtime/FlexibleContentLayoutGroup.cs` | `CollectContentChildren`(101행 부근)과 `GetRequest`(172행 부근)에 크기 공급자 조회를 추가한다. |
| `Assets/TxTRPG/UI/Runtime/FlexibleLayoutItem.cs` 또는 새 파일 | 새 크기 공급자 인터페이스를 정의한다. 기존 enum과 필드는 변경하지 않는다. |
| `Assets/TxTRPG/UI/Runtime/FlexibleLayoutPanel.cs` | `CalculateSizes`는 변경하지 않는다. 공급자가 만든 요청도 같은 계산을 거친다. |
| `Assets/TxTRPG/UI/Runtime/ActionGridPanel.cs` | `ApplyLayoutCore`의 SingleRow 분기(704~718행 부근)에서 세로 Padding 대체와 설정 변경 알림을 처리한다. |
| `Assets/TxTRPG/UI/Runtime/ActionGridSurfaceLayout.cs` | `HeaderReservation`, `bodyMargins`, `ConfigureRowViewport`의 가로 Scrollbar 예약 규칙을 높이 계산에서 재사용한다. 공개 읽기 전용 속성이 필요하면 추가한다. |
| `Assets/TxTRPG/UI/Runtime/ActionGridSingleRowSettings.cs`, `ActionGridDisplaySettings.cs` | 활성 Preset의 Cell 크기 정규화 규칙을 높이 계산에서 같은 방식으로 사용한다. |
| `Assets/TxTRPG/UI/Runtime/ResponsiveMainScreen.cs` | 좁은 화면에서는 `RightPanel` 전체가 Fixed 640으로 바뀐다. 내부 배분이 이 정책과 충돌하지 않는지 확인한다. 이 파일은 원칙적으로 변경하지 않는다. |
| `Assets/TxTRPG/Application/Editor/TemporaryActionsGridVerification.cs` | `Apply Balanced To Main Scene`은 Actions의 Minimum을 184로 저장한다. 새 정책이 활성화된 동안에는 이 값이 무시되어야 하며, 이 메뉴를 다시 실행하지 않도록 문서에 기록한다. |
| `Assets/Scenes/TMP_MainScene.unity` | `Main_FlexibleLayoutPanel/ContentLayer/RightPanel/ContentLayer`의 `ActionGridPanel`과 `HealthBarPanel` 인스턴스 Override만 변경한다. |
| `Assets/TxTRPG/UI/Prefabs/ActionGridPanel.prefab` | 변경하지 않는다. Bag(`InventoryWindowPage/ItemGrid`)과 다른 인스턴스의 외형은 그대로 유지한다. |

## 3. 작업 범위

### 범위 안

- 부모 `FlexibleContentLayoutGroup`에 크기 요청을 직접 공급할 수 있는 확장 지점을 추가한다.
- Actions의 Cell 높이, Gap, Body Margin, Header 예약, 가로 Scrollbar 예약으로 필요한 높이를 계산하는 재사용 가능한 구성 요소를 추가한다.
- 구성 요소가 활성일 때 SingleRow에서 Cell이 `Viewport`의 위아래 경계로부터 정확히 Gap만큼 떨어지도록 세로 배치를 맞춘다.
- `TMP_MainScene`의 Actions 인스턴스에 구성 요소를 추가하고, `HealthBarPanel`을 현재 높이의 Fixed 항목으로 바꾼다.
- 이 적용 작업을 위한 임시 Tools 메뉴와 검증 메뉴를 제공한다.
- 관련 Edit Mode 테스트와 문서를 갱신한다.

### 범위 밖

- Grid flow(Bag 등)의 높이 자동 계산. 구성 요소는 SingleRow에서만 동작한다.
- 좌우 Gap, 좌우 Padding, 슬롯 간격과 정렬 정책의 변경.
- Cell 크기, Icon·EmptySlot 크기 정책, 셀 수와 Capacity의 변경.
- 공용 `ActionGridPanel.prefab`과 생성기(`ActionGridPrefabBuilder`) 기본값의 변경.
- `ResponsiveMainScreen`의 좁은 화면 높이(`narrowHeights`) 정책 변경.
- 실제 Main Scene의 신규 구성. 이번에는 재사용 가능한 구성 요소와 적용 절차만 준비한다.
- 시각 효과 추가.

## 4. 구현 요구 사항

### 4.1 공용 크기 공급자 확장 지점

`FlexibleLayoutItem`의 직렬화 값을 실행 중에 덮어쓰는 방식은 사용하지 않는다. 그렇게 하면 `[ExecuteAlways]` 레이아웃 갱신 중 Scene이 변경된 것으로 표시되고, 다른 도구(`ResponsiveMainScreen`, `TemporaryActionsGridVerification`)가 같은 필드를 저장할 때 충돌한다. 대신 부모가 계산 시점에 값을 조회하는 방식을 사용한다.

1. 다음 성격의 인터페이스를 `TxTRPG.UI`에 추가한다. 이름은 기존 규칙에 맞게 정하되, 의미는 유지한다.

   ```csharp
   public interface IFlexibleLayoutSizeSource
   {
       // 부모의 현재 주축을 받아, 이 항목이 직접 결정한 크기 요청을 반환한다.
       // false를 반환하면 부모는 기존 FlexibleLayoutItem(또는 기본 Weighted 1) 규칙을 사용한다.
       bool TryGetSizeRequest(FlexibleLayoutAxis axis, out FlexibleLayoutSizeRequest request);
   }
   ```

2. `FlexibleContentLayoutGroup.CollectContentChildren`에서 각 자식의 공급자를 `FlexibleLayoutItem`과 함께 수집한다. 활성화된(`Behaviour.isActiveAndEnabled`) 공급자만 사용한다.
3. `GetRequest`는 공급자가 `true`를 반환하면 그 요청을 사용하고, 그 밖의 경우에는 지금과 동일하게 동작해야 한다. 공급자가 없는 기존 화면의 계산 결과는 한 픽셀도 달라지지 않아야 한다.
4. `CalculateSizes`와 Overflow 정책은 변경하지 않는다. 공급자 요청도 같은 `ShrinkBelowMinimum`·`Clip` 규칙을 적용받는다.
5. 레이아웃 계산 중에 할당을 반복해서 만들지 않는다. 수집 목록은 기존 `items`처럼 재사용한다.

### 4.2 Actions 높이 구성 요소

`ActionGridPanel` 루트(현재 `FlexibleLayoutItem`이 있는 오브젝트)에 붙이는 새 MonoBehaviour를 추가한다. 이름 예시는 `ActionGridContentFitHeight`이다. 이 구성 요소는 크기 계산만 담당하며, Entry·선택·스크롤 책임은 기존 `ActionGridPanel`과 `ActionGridSurfaceLayout`에 그대로 둔다.

| 직렬화 필드 | 의미 | 기본값과 정규화 |
| --- | --- | --- |
| `panel` | 대상 `ActionGridPanel` | 같은 오브젝트의 구성 요소를 저장해 둔다. 실행 중 Scene 검색으로 보완하지 않는다. |
| `gap` (Inspector 표시 이름 `Gap`) | Cell과 `Viewport` 위·아래 경계 사이의 거리 | 12. NaN·Infinity는 12, 음수는 0, 상한은 기존 `ActionGridDisplayPreset.Finite` 관례에 맞춰 4096으로 제한한다. |

구성 요소는 다음 조건을 모두 만족할 때만 크기 요청을 공급한다.

- 구성 요소와 `panel`이 활성이고 참조가 완전하다.
- `panel.IsSingleRow`가 참이다.
- 부모의 현재 주축이 `Vertical`이다. 가로 축이면 `false`를 반환하여 기존 `FlexibleLayoutItem` 값을 사용한다.

공급하는 요청은 `Fixed` 모드이며, Fixed·Minimum·Maximum을 모두 아래 높이 H로 설정한다.

```text
CellHeight          = 활성 Preset(Manual이면 singleRow)의 targetCellSize.y를
                      ActionGridPanel이 실제로 사용하는 규칙과 같은 방법으로 정규화한 값
ViewportHeight      = CellHeight + 2 × Gap
ScrollbarReservation = 가로 Scrollbar가 표시될 때 scrollbarHeight + scrollbarGap, 아니면 0
                      (Always, 또는 Auto이면서 가로 overflow일 때 표시)
H = bodyMargins.top + HeaderReservation + ViewportHeight + ScrollbarReservation + bodyMargins.bottom
```

- `HeaderReservation`은 `ActionGridSurfaceLayout.HeaderReservation`을 그대로 사용한다. Header가 꺼져 있으면 0이다.
- `ActionGridSurfaceLayout.manageHeaderLayout`이 꺼져 있어 Body Margin을 소유하지 않는 인스턴스에서는, 작성된 `Scroll View` Offset을 Margin으로 사용하거나 공급을 중단하고 경고를 한 번 출력한다. 어느 쪽을 택했는지 문서에 기록한다.
- 표시 Cell 수가 0이어도 `CellHeight`를 예약한다. 슬롯이 없어진 순간 영역이 접히며 화면 전체가 흔들리는 일을 피하기 위해서이다.
- 계산은 순수 정적 함수로 분리하여 Unity 오브젝트 없이 Edit Mode 테스트할 수 있게 한다.
- 위 높이는 패널 자신의 높이에 의존하지 않는다. 가로 Scrollbar의 Auto 판정은 너비에만 의존하므로 높이 계산이 순환하지 않는다. 구현자는 이 성질을 깨뜨리지 않아야 한다.

### 4.3 Viewport 안의 세로 배치

구성 요소가 공급 중일 때 SingleRow의 Cell은 `Viewport` 위·아래 경계로부터 정확히 Gap만큼 떨어져야 한다.

- `ActionGridPanel.ApplyLayoutCore`의 SingleRow 분기에서, 구성 요소가 활성이면 `effectivePadding`의 사본을 만들어 `top`과 `bottom`만 Gap으로 바꾼다. Preset이나 수동 설정에 저장된 Padding 자체는 수정하지 않는다.
- `left`와 `right`는 기존 값을 그대로 유지한다. 따라서 overflow 판정, 양 끝 정렬, 중앙 묶음 정렬의 결과는 이번 변경으로 달라지지 않는다.
- 구성 요소가 비활성이거나 Grid flow이면 지금과 동일한 Padding을 사용한다.
- 세로 정렬 설정(`Top`·`Center`·`Bottom`)은 유지한다. Viewport 높이가 Cell + 2 × Gap이면 세 정렬의 결과가 같으므로 Center 기본값에서 의도한 배치가 나온다.

### 4.4 갱신 시점

- 높이에 영향을 주는 값(Gap, 표시 방식·Preset의 Cell 크기, Header 표시, Body Margin, 가로 Scrollbar 표시 여부)이 바뀌면 부모 `FlexibleContentLayoutGroup`을 `LayoutRebuilder.MarkLayoutForRebuild`로 다시 계산하게 한다.
- 레이아웃 재계산 도중에 다시 재계산을 요청하지 않는다. 이미 계산된 값과 비교하여 0.5 이상 달라졌을 때만 요청하고, 레이아웃 콜백 안에서 감지한 경우에는 기존 `EditorValidationRefreshPump`·`Canvas.willRenderCanvases` 같은 지연 경로를 사용한다.
- `OnValidate`에서는 값을 정규화하고 지연 갱신만 예약한다. `OnValidate`에서 Scene을 저장하거나 다른 오브젝트의 직렬화 값을 수정하지 않는다.
- 매 프레임 계산이나 Scene 검색을 추가하지 않는다.
- 런타임 API로 `SetGap(float)`와 현재 계산값을 읽는 속성(예: `ResolvedHeight`)을 제공한다. Inspector에는 계산된 H와 공급 여부, 공급하지 않는 이유(Grid flow, 가로 축, 참조 누락)를 읽기 전용으로 표시한다.

### 4.5 `HealthBarPanel` 고정

- `TMP_MainScene`의 `RightPanel/ContentLayer/HealthBarPanel`에 있는 `FlexibleLayoutItem`을 `Fixed`로 바꾼다. 값은 적용 직전 1920×1080 Game View에서 측정한 현재 높이(작성 시 약 49.95)를 정수로 반올림하여 Fixed·Minimum에 같이 저장한다. Weight 값은 되돌릴 수 있도록 그대로 둔다.
- `HealthBarPanel`이 자체 콘텐츠 높이를 계산하는 API를 이미 제공한다면, 측정값 대신 그 값을 사용할 수 있는지 먼저 확인하고 결과를 보고한다. 새 높이 계산 기능은 이번 범위에서 만들지 않는다.
- 이로써 `RightPanel/ContentLayer`의 Weighted 항목은 `CharacterDisplayPanel` 하나만 남는다. `CharacterDisplayPanel`의 Weight 2는 변경하지 않는다. 유일한 Weighted 항목이므로 값과 무관하게 남은 공간 전체를 받는다.

### 4.6 적용 메뉴

현재 Scene과 이후 실제 Main Scene 모두에 쓸 수 있도록, 메뉴는 Scene 경로나 오브젝트 이름이 아니라 **선택한 `ActionGridPanel`** 을 대상으로 동작한다.

- 메뉴 경로 예시: `Tools/TxT RPG/UI/Actions/Temporary/Apply Content-Fit Height To Selected Actions`
- 실행 조건: Edit Mode, 컴파일 완료, Prefab Stage가 열려 있지 않음, 활성 Scene에 저장되지 않은 변경이 없음, 정확히 하나의 `ActionGridPanel`이 선택됨, 그 부모에 `FlexibleContentLayoutGroup`이 있음, 대상이 SingleRow임. 조건이 맞지 않으면 아무것도 바꾸지 않고 이유를 오류로 출력한다.
- 동작: 대상에 구성 요소가 없으면 추가하고 `panel` 참조와 Gap 12를 저장한다. 이미 있으면 작성된 Gap을 보존한다. 같은 부모의 형제 중 `HealthBarPanel` 구성 요소를 가진 항목이 정확히 하나이고 Weighted이면 4.5의 규칙으로 고정한다. 이미 Fixed이면 건드리지 않는다.
- Undo를 기록하고 `PrefabUtility.RecordPrefabInstancePropertyModifications`로 인스턴스 Override만 남긴다. Scene은 저장하지 않고 Dirty 상태로 두어 사용자가 diff를 검토한 뒤 저장하게 한다.
- 다시 실행해도 안전해야 한다. 두 번째 실행은 변경 없이 "이미 적용됨"을 출력한다.
- 이 메뉴는 공용 Prefab Rebuild 일괄 작업에 등록하지 않는다. 메뉴 코드 주석에 임시 메뉴라는 사실, 재실행 안전성, 제거 시점(실제 Main Scene 구성 후 정식 작성 절차로 대체될 때)을 기록한다.
- 측정·검증 메뉴: 선택한 Actions의 현재 패널 높이, Viewport 높이, Cell Rect의 위·아래 간격, 공급 여부, 형제 항목의 높이를 `ACTIONS_FIT:` 접두어로 로그에 출력한다. Play Mode에서도 실행할 수 있게 한다.

## 5. 호환성과 실패 동작

- 기존 직렬화 자산에는 새 구성 요소가 없으므로 동작이 바뀌지 않는다. 공급자 인터페이스를 구현한 구성 요소가 없는 모든 `FlexibleContentLayoutGroup`은 현재와 동일하게 계산해야 한다.
- 구성 요소를 비활성화하거나 제거하면 Actions는 작성된 `FlexibleLayoutItem` 값(현재 Fixed 100, Minimum 180)으로 돌아간다. 이 값은 대체 경로이므로 지우지 않는다.
- Grid flow로 전환하면 공급을 중단하고 같은 대체 값을 사용한다. Padding도 원래 값으로 돌아간다.
- 부모가 가로 축이 되면 공급을 중단한다.
- 높이가 부족하면(예: 좁은 화면에서 `RightPanel`이 Fixed 640일 때 형제 항목의 합이 이를 넘으면) 기존 `ShrinkBelowMinimum` 규칙으로 모든 항목이 비례 축소된다. 이번 작업에서 새 클리핑이나 스크롤을 추가하지 않는다. 다만 Actions가 계산된 H보다 작게 배치된 상태를 Inspector와 측정 로그에서 확인할 수 있어야 한다.
- 참조가 누락되면 Editor와 Development Build에서 경고를 한 번 출력하고 공급을 중단한다. 예외로 레이아웃 전체를 중단시키지 않는다.
- 저장 데이터, 설정 데이터, 입력 바인딩은 변경하지 않는다.
- Cell 크기와 터치 영역은 변경하지 않는다. 키보드·게임패드 Navigation, 포커스 노출, 가로 스크롤 동작도 변경하지 않는다.
- 시각 효과 대비: 구성 요소는 레이아웃이 소유하는 RectTransform 크기만 결정한다. `ActionGridCell`의 `Border`·`ContentRoot` 같은 시각 효과용 계층에는 접근하지 않는다.
- `TemporaryActionsGridVerification`의 `Apply Balanced To Main Scene`을 다시 실행하면 Actions의 Minimum이 184로 저장되지만, 구성 요소가 활성인 동안에는 이 값이 무시된다. 이 메뉴는 다시 실행하지 않는다.

## 6. 검증

### 6.1 Edit Mode 테스트

새 테스트는 임시 오브젝트만 사용하고 끝에 정리하므로 `AssetWriting` 분류를 붙이지 않는다. 실행은 관련 클래스만 선택하고 `!AssetWriting` 분류 필터를 함께 사용한다. 전체 Edit Mode 테스트를 실행하지 않는다.

| 테스트 | 확인할 내용 |
| --- | --- |
| 높이 계산 함수(새 테스트 클래스) | Cell 64, Gap 12, Margin 18/18, Header 꺼짐, Scrollbar Hidden에서 124. Header 표시 시 `headerHeight + headerBodyGap` 추가. Scrollbar Always 또는 Auto+overflow에서 `scrollbarHeight + scrollbarGap` 추가. Gap 0, 음수, NaN, Infinity 정규화. Cell 수 0에서도 Cell 높이 예약. |
| `FlexibleContentLayoutGroup` 공급자 경로(새 테스트 또는 `FlexibleLayoutPanelTests`의 비 AssetWriting 메서드) | 공급자가 없을 때 기존 결과와 동일. 공급자가 Fixed를 반환하면 그 크기 사용. 공급자가 비활성이거나 `false`를 반환하면 `FlexibleLayoutItem` 사용. 남는 공간이 유일한 Weighted 항목에 모두 배분됨. |
| `ActionGridSingleRowTests` | 기존 테스트가 모두 통과함. 구성 요소 활성 시 Cell 위·아래 간격이 Gap과 같고, 좌우 overflow·정렬 결과는 변하지 않음. Grid 전환 시 원래 Padding으로 복원됨. |

테스트 실행 전후의 `git status`를 비교하고, 테스트가 이번 변경과 무관한 파일을 수정했다면 실행 전에 깨끗했던 파일만 복원한다.

### 6.2 Editor 적용 확인

1. 적용 메뉴 실행 후 `serialization-diff-reviewer`로 `TMP_MainScene.unity`의 diff를 검토한다. 의도한 변경은 Actions 인스턴스의 구성 요소 추가와 `HealthBarPanel`의 `FlexibleLayoutItem` Override뿐이어야 한다. 레이아웃 재계산으로 생긴 RectTransform 값 변화는 별도로 구분한다.
2. 1920×1080에서 측정 메뉴로 다음을 확인한다.
   - Actions 높이 124, Viewport 높이 88, Cell과 Viewport 위·아래 간격 각 12(±0.5).
   - `HealthBarPanel` 높이는 적용 전과 같음(±0.5).
   - `GameMenuPanel` 높이는 적용 전과 같음.
   - `CharacterDisplayPanel` 높이는 Actions가 줄어든 만큼(약 56) 늘어남.
3. Inspector에서 Gap을 0, 24로 바꿔 Actions와 `CharacterDisplayPanel`만 반대 방향으로 함께 변하는지 확인한 뒤 12로 되돌린다. 이 확인은 저장하지 않는다.
4. Header를 일시적으로 켜고 끄며 예약 높이가 반영되는지 확인한다. 확인 후 원래 상태로 되돌린다.

### 6.3 Play Mode와 해상도

`AppScene`을 통해 Play Mode를 시작하는 프로젝트 정책을 따른다. PlayMode Test Runner 대신 측정 메뉴와 `Editor.log`를 사용한다.

| 화면 | 확인할 내용 |
| --- | --- |
| 1920×1080 데스크톱 | 6.2의 수치가 Play Mode에서도 같음. |
| 2560×1080 울트라와이드 | Actions 높이는 같고 좌우 정렬만 기존 규칙대로 변함. |
| 1024×768 태블릿 | Actions 높이는 같고 `CharacterDisplayPanel`만 줄어듦. |
| 390×844 휴대폰 세로, 안전 영역 포함 | `ResponsiveMainScreen`의 좁은 화면 배치에서 `RightPanel` 640 안에 네 항목이 들어가고 Actions 높이가 124로 유지됨. |
| 844×390 휴대폰 가로 | 높이가 부족할 때 기존 비례 축소 규칙이 적용되는지, 그리고 그 상태가 측정 로그에 드러나는지 확인함. |

입력 경로로 마우스 클릭, 키보드·게임패드 방향 이동과 Submit, 터치 드래그(가로 스크롤)를 확인한다. 동작이 이전과 같아야 한다.

### 6.4 컴파일

Editor 컴파일 오류가 없어야 한다. 가능한 경우 Standalone과 Android(또는 iOS) 대상의 스크립트 컴파일을 확인하고, 수행하지 못한 대상은 보고서에 명시한다.

## 7. 문서 갱신

| 문서 | 갱신 내용 |
| --- | --- |
| `DOCS/architecture/flexible-layout-panel.md` | 크기 공급자 확장 지점, `FlexibleLayoutItem`과의 우선순위, 대체 경로를 "크기 계산" 절에 추가한다. |
| `DOCS/architecture/action-grid-panel.md` | "단일 행과 Header 영역" 절에 높이 구성 요소의 책임, 계산식, Padding 대체 규칙, 공급 조건을 추가한다. TMP_MainScene Actions의 현재 설정(Gap 12, 높이 124)을 기록하고, 기존 문장 "기존 부모 FlexibleLayout 배분은 변경하지 않습니다"를 실제 상태에 맞게 수정한다. |
| `DOCS/architecture/character-display-panel.md` 또는 `DOCS/architecture/main-scene-default-presentation.md` | `RightPanel/ContentLayer`의 배분(HealthBar Fixed, Character 유일 Weighted, Actions 계산 높이, Menu Fixed)을 기록한다. 어느 문서가 더 적합한지 확인하여 한 곳에 둔다. |
| `DOCS/development/workflows.md` | 적용 메뉴와 측정 메뉴의 경로, 실제 Main Scene에 같은 정책을 적용하는 절차, `Apply Balanced To Main Scene` 재실행 금지를 추가한다. |
| `DOCS/architecture/project-structure.md` | 새 파일이 기존 어셈블리 경계 안에 있으면 갱신하지 않아도 된다. 새 어셈블리나 디렉터리를 만들면 갱신한다. |

현재 구현된 동작과 계획된 동작을 구분하여 쓰고, 문서의 경로·클래스·메뉴 이름을 실제 작업 트리와 대조한다.

## 8. 최종 보고

다음 항목을 구분하여 보고한다.

1. 구현 완료: 추가·변경한 파일, 공급자 인터페이스와 구성 요소의 이름, 계산식.
2. 이미 적용한 내용: Editor에서 실행한 메뉴, 변경된 Scene Override, 저장 여부.
3. 사용자가 수행해야 할 남은 절차: 메뉴가 남아 있다면 `사용자가 수행할 적용 절차` 절에 복사 가능한 순서로 쓴다.
4. 검증 결과: 실행한 테스트 클래스와 필터, 통과·실패 수, 측정한 해상도별 수치, 확인한 입력 경로.
5. 확인하지 못한 항목: 실제 기기, 빌드 대상 컴파일 등.
6. `HealthBarPanel` 고정값을 측정값과 자체 높이 API 중 무엇으로 정했는지와 그 이유.
7. `git status` 비교 결과와 복원한 파일 목록.
