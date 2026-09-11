# 퀵 아이템과 게임 창

## 책임과 데이터 소유권

| 영역 | 구성 요소 | 책임 |
| --- | --- | --- |
| Content | `ItemDefinition`, `ItemCatalog` | 아이템 ID, 이름 키, Addressables 아이콘 ID와 효과 정의를 보관합니다. 수량은 보관하지 않습니다. |
| Gameplay | `InventoryState` | 아이템 정의 ID별 보유 수량과 변경 이벤트를 소유합니다. |
| Gameplay | `QuickItemLoadout` | 0부터 시작하는 안정적인 슬롯 번호와 아이템 정의 ID의 연결을 소유합니다. |
| Application | `QuickItemService` | 등록, 해제, 사용 검증과 성공한 효과의 1회 차감을 조정합니다. |
| Application | `QuickItemGridPresenter` | 기존 `ActionGridPanel`에 `PreserveSlots` 정책으로 퀵 슬롯을 표시합니다. |
| UI | `GameMenuPanel`, `GameWindowService`, `ModalWindowHost` | 안정적인 페이지 ID 라우팅, 단일 모달 수명, 로딩·오류·포커스 복원을 담당합니다. |

`ActionGridEntry.Id`는 `quick-item:{slotIndex}` 형식의 UI 항목 ID입니다. 저장 데이터에는 이 ID를 기록하지 않고 슬롯 번호와 `ItemDefinition.DefinitionId`만 기록합니다. 수량이 0이 되어도 등록은 유지되며 해당 슬롯의 사용 명령만 비활성화됩니다.

## 아이템 사용

현재 지원하는 실제 효과는 `ItemEffectKind.Healing`입니다. `QuickItemService`는 활성 캐릭터 인스턴스 ID, 보유 수량과 최대 체력을 확인한 뒤 아이템을 차감하고 `HealthState.Heal`을 호출합니다. 회복이 적용되지 않으면 차감을 되돌립니다. 동시에 두 사용 요청이 들어오면 하나만 실행하고 나머지는 `Busy`로 거부합니다.

최대 체력, 빈 슬롯, 미등록 콘텐츠, 미지원 효과와 수량 부족은 아이템을 소비하지 않습니다. 알 수 없는 아이템 ID는 저장 및 슬롯 상태에 남겨 두며, 콘텐츠가 다시 제공될 때 복구할 수 있습니다.

## 저장 호환성

`PlayerSaveData` 버전 2는 `inventory`와 `quickItems`를 추가합니다. 버전 1 저장 파일은 캐릭터와 활성 캐릭터를 그대로 이전하고 두 새 목록을 빈 상태로 초기화합니다. 음수 수량, 중복 아이템 ID, 중복 슬롯 번호와 범위를 벗어난 슬롯은 손상 데이터로 진단하여 복원을 중단합니다.

## 창 수명과 입력

페이지 ID는 `system`, `inventory`, `status`입니다. 새 열기 요청은 이전 페이지 준비 작업을 취소하는 latest-request-wins 정책을 사용합니다. 모달은 Canvas 바로 아래에 생성되어 패널 마스크의 영향을 받지 않고, 딤 배경이 포인터 입력을 차단합니다. 닫을 때에는 창을 연 메뉴 버튼으로 포커스를 복원합니다.

기본 배치는 `ActionGridPanel` 바로 아래에 `GameMenuPanel`을 놓는 `BelowActionGrid` 정책입니다. 생성기는 기존 `ActionGridPanel` 인스턴스를 복제하지 않으며 동일한 부모의 다음 형제 위치에 메뉴를 추가합니다. 플랫폼별 단축키 표시는 후속 범위입니다.

## GameMenuPanel 반응형 레이아웃

`GameMenuPanel`은 명령 연결과 표시 상태를 담당하고, `GameMenuLayoutGroup`은 버튼의 크기와 위치 및 선호 크기만 계산합니다. `GameMenuButtonView`는 입력 영역인 `ButtonRoot`와 애니메이션 대상인 `VisualRoot`를 분리합니다.

```text
GameMenuPanel                 ScrollRect, FlexibleLayoutItem
├── Viewport                  RectMask2D
│   └── Content               GameMenuLayoutGroup
│       └── ButtonRoot        Button, GameMenuButtonView
│           └── VisualRoot
│               ├── Background
│               ├── Icon
│               ├── Label
│               ├── Border
│               └── EffectOverlay
└── HorizontalScrollbar       선택 표시
```

`HorizontalScroll`은 버튼 크기를 유지한 채 한 줄로 배치합니다. 콘텐츠가 Viewport보다 작으면 Left, Center 또는 Right 정렬을 적용하고, 넘치면 `ScrollRect`로 양 끝 패딩까지 이동할 수 있습니다. 스크롤바는 `Hidden`, `Auto`, `Always`를 지원하며 공간은 `Overlay`, `ReserveAlways`, `ReserveWhenVisible` 정책으로 제어합니다. 숨긴 경우에도 드래그와 키보드·게임패드 선택 이동은 유지됩니다.

`Wrap`은 Viewport의 실제 너비로 열 수를 계산하고 수평·수직 스크롤을 모두 끕니다. `MaximumColumns`는 고정 열 수가 아니라 상한입니다. 각 행은 실제 버튼 수를 기준으로 독립 정렬하므로 마지막 불완전 행도 Left, Center 또는 Right 정책을 따르며 버튼 순서는 변경되지 않습니다.

메뉴는 계산한 `Required Width`, `Required Height`, 열 수, 행 수와 `LayoutInsufficientSpace`를 Inspector에서 제공합니다. 부모가 필요한 높이를 배정하지 않으면 버튼을 축소하거나 모드를 암묵적으로 변경하지 않고 Viewport에서 클리핑하며, 동일한 부족 상태에 대한 경고는 한 번만 출력합니다. `FlexibleLayoutItem`의 크기 정책은 부모가 소유하므로 개발자가 계산된 필요 높이를 기준으로 해당 항목의 최소 또는 고정 크기를 조정해야 합니다.

버튼의 `pageId`, `Button`, `visible`, `windowService`, `quickItemGrid` 직렬화 필드와 기존 `Button.onClick` 리스너는 유지됩니다. 런타임 리스너만 개별적으로 등록하고 해제하며 `RemoveAllListeners`는 사용하지 않습니다. 중복 버튼 바인딩은 첫 번째 항목만 사용하고 진단 경고를 출력합니다.

## 운영 자산과 데모 데이터

## Game menu prefab composition

`GameMenuPanel.prefab` owns its Viewport, Content, `GameMenuLayoutGroup`, `ScrollRect`, button views, and horizontal scrollbar. It deliberately keeps `GameWindowService` and the optional `ActionGridPanel` reference empty. Until a valid service is bound, commands are disabled and the Inspector reports an external-dependency waiting state separately from internal configuration errors.

`GameMenuScreen.prefab` is the optional ready-to-place composition for new screens. Its `MenuArea` and `OverlayRoot` are siblings. A `GameMenuCompositionBinder` connects the nested menu to the owned `GameWindowService` and modal pages, while the overlay remains outside the menu viewport mask. The composition does not create a quick-item grid or demo player data.

The binder supports explicit `Bind` and `Unbind` after activation. By default it does not replace a different service already assigned in the Inspector, and it only removes a binding that it established.

## Game menu prefab composition

`GameMenuPanel.prefab` owns its Viewport, Content, `GameMenuLayoutGroup`, `ScrollRect`, button views, and horizontal scrollbar. It deliberately keeps `GameWindowService` and the optional `ActionGridPanel` reference empty. Until a valid service is bound, commands are disabled and the Inspector reports an external-dependency waiting state separately from internal configuration errors.

`GameMenuScreen.prefab` is the optional ready-to-place composition for new screens. Its `MenuArea` and `OverlayRoot` are siblings. A `GameMenuCompositionBinder` connects the nested menu to the owned `GameWindowService` and modal pages, while the overlay remains outside the menu viewport mask. The composition does not create a quick-item grid or demo player data.

The binder supports explicit `Bind` and `Unbind` after activation. By default it does not replace a different service already assigned in the Inspector, and it only removes a binding that it established.

`MinorHealingPotion.asset`과 `ItemCatalog.asset`은 콘텐츠 정의만 제공합니다. 새 게임 인벤토리에 아이템을 자동 지급하지 않습니다. 테스트와 데모에서 필요한 수량은 해당 테스트 또는 데모 초기화 코드가 명시적으로 추가해야 합니다.
