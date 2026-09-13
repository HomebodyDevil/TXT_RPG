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
| Application | `InventoryGameWindowPage`, `InventoryProjectionBuilder` | 가방의 카테고리 필터, 안정적인 정렬, 수직 스크롤·번호 페이지 표시와 ActionGrid 명령 연결을 담당합니다. |

`ActionGridEntry.Id`는 `quick-item:{slotIndex}` 형식의 UI 항목 ID입니다. 저장 데이터에는 이 ID를 기록하지 않고 슬롯 번호와 `ItemDefinition.DefinitionId`만 기록합니다. 수량이 0이 되어도 등록은 유지되며 해당 슬롯의 사용 명령만 비활성화됩니다.

## 이미지 메뉴와 가방 창

`GameMenuButtonView`는 `ImageOnly`와 `ImageWithLabel` 표시 모드를 지원합니다. 아이콘은 비율을 유지하며 `VisualRoot` 안에서만 배치되므로 메뉴 레이아웃 크기를 변경하지 않습니다. Sprite가 없으면 현지화 가능한 Label을 다시 표시하여 빈 버튼이 되지 않습니다. 생성기는 프로젝트가 소유하는 단순한 기본 가방 아이콘을 `DefaultBagIcon.asset`으로 만들며 개발자는 버튼 View의 Sprite로 교체할 수 있습니다.

가방은 기존 `inventory` 페이지 ID와 `GameWindowService`를 유지합니다. `InventoryWindowPage.prefab` 안에는 `CategoryTabs`, 재사용한 `ActionGridPanel`, `PaginationControls`, `EmptyState`와 결과 표시가 있습니다. 기본 카테고리는 `all`, `consumable`, `misc`이며, 비어 있거나 이전 버전에서 생성된 `ItemDefinition.categoryId`는 `misc`로 처리합니다. 필터와 페이지 이동은 `InventoryState`를 변경하지 않습니다.

`VerticalScroll`은 필터 결과 전체를 표시하며 카테고리를 바꿀 때만 첫 행으로 이동합니다. 일반 수량 갱신은 현재 스크롤과 선택 ID를 가능한 한 유지합니다. `Paged`는 `itemsPerPage`로 결과를 나누고 이전·다음 버튼, 1부터 시작하는 현재/전체 번호와 `GoToPage` API를 제공합니다. 0개 결과도 1페이지이며, `fillPageWithEmptySlots`를 켜면 마지막 페이지의 표시용 빈 칸만 채웁니다. 이 값은 실제 가방 용량이 아닙니다.

컨텍스트 메뉴는 기존 `IActionMenuProvider`와 `IActionCommandExecutor`를 사용합니다. 현재 실제 명령은 지원되는 회복 아이템의 `Use`와 비어 있거나 같은 아이템인 퀵 슬롯 대상의 `Register`입니다. 점유 슬롯 교체 확인 UI와 `Discard`의 데이터 파괴는 구현하지 않았으며 메뉴에서 비활성 사유를 표시합니다. 가방 사용은 퀵 슬롯을 임시 변경하지 않고 `QuickItemService.UseItemAsync`를 호출합니다. 같은 `PlayerState`를 사용하는 서비스 인스턴스는 실행 잠금을 공유하므로 퀵 슬롯과 가방의 빠른 중복 사용을 하나의 경계에서 차단합니다.

`TMP_MainScene`에서는 기존 `Main_FlexibleLayoutPanel/Action_FlexibleLayoutPanel/ContentLayer/GameMenuPanel` 인스턴스를 유지하고, 같은 Canvas 최상위의 `GameWindowsOverlay`에 `ModalWindowHost.prefab`을 한 번만 배치합니다. 메뉴의 `windowService`에는 Overlay의 서비스를, `quickItemGrid`에는 `QuickItemGridPresenter`가 연결된 운영 Grid를 저장합니다. 가방 내부 `ItemGrid`는 이 외부 필드에 연결하지 않습니다. 데이터는 Build Settings 첫 Scene인 `AppScene`의 `PlayerSessionHost`가 소유하며 Scene에는 별도 세션이나 인벤토리를 만들지 않습니다.

## 아이템 사용

현재 지원하는 실제 효과는 `ItemEffectKind.Healing`입니다. `QuickItemService`는 활성 캐릭터 인스턴스 ID, 보유 수량과 최대 체력을 확인한 뒤 아이템을 차감하고 `HealthState.Heal`을 호출합니다. 회복이 적용되지 않으면 차감을 되돌립니다. 동시에 두 사용 요청이 들어오면 하나만 실행하고 나머지는 `Busy`로 거부합니다.

최대 체력, 빈 슬롯, 미등록 콘텐츠, 미지원 효과와 수량 부족은 아이템을 소비하지 않습니다. 알 수 없는 아이템 ID는 저장 및 슬롯 상태에 남겨 두며, 콘텐츠가 다시 제공될 때 복구할 수 있습니다.

## 저장 호환성

`PlayerSaveData` 버전 2는 `inventory`와 `quickItems`를 추가합니다. 버전 1 저장 파일은 캐릭터와 활성 캐릭터를 그대로 이전하고 두 새 목록을 빈 상태로 초기화합니다. 음수 수량, 중복 아이템 ID, 중복 슬롯 번호와 범위를 벗어난 슬롯은 손상 데이터로 진단하여 복원을 중단합니다.

## 창 수명과 입력

페이지 ID는 `system`, `inventory`, `status`입니다. 새 열기 요청은 이전 페이지 준비 작업을 취소하는 latest-request-wins 정책을 사용합니다. 모달은 Canvas 바로 아래에 생성되어 패널 마스크의 영향을 받지 않고, 딤 배경이 포인터 입력을 차단합니다. 닫을 때에는 창을 연 메뉴 버튼으로 포커스를 복원합니다.

`TMP_MainScene`의 기존 `GameMenuPanel`은 Scene에 저장된 `GameWindowService`와 연결될 때만 실행 가능한 버튼을 활성화합니다. 서비스가 없거나 대상 페이지가 등록되지 않으면 해당 버튼을 비활성화하며, 빈 인벤토리는 정상적인 빈 상태로 표시합니다. `GamePageIds.System`은 현재 설정 데이터나 저장 기능이 없는 빈 페이지입니다. 공통 모달의 제목과 닫기 버튼만 표시하고 최초 포커스는 닫기 버튼으로 이동합니다. EventSystem의 Cancel 이벤트도 동일한 닫기 경로를 사용합니다.

각 `GameWindowPage`는 라우팅용 `PageId`와 사용자 표시용 `DisplayTitle`을 분리합니다. 기존 직렬화 자산에 표시 제목이 없으면 `PageId`를 대체값으로 사용합니다.

### 타입 기반 단일 개방 계약

운영 모달은 `GameWindowService.OpenAsync(ModalOpenRequest)`만 실제 개방 절차를 수행합니다. `GameMenuPanel`, 단축키, 재시도와 기존 `OpenAsync(string, GameObject)` 호출자는 모두 `CreateRequest`를 거쳐 같은 요청 경로를 사용합니다. `ModalWindowHost.SetVisible`과 `GameWindowPage.Show`는 서비스가 호출하는 표현 구현이며, 다른 런타임 호출자는 직접 모달을 활성화하지 않습니다.

`ModalOpenRequest`는 Window ID, `ModalContentKind`, 제목, 표시 설정 스냅샷, `IModalDataProvider`, 최초 카테고리·페이지와 포커스 복귀 대상을 보관합니다. `IModalContentConfiguration.CloneForRequest`가 요청 생성 시 설정을 복사하므로 Inspector 원본을 이후에 바꾸어도 진행 중인 요청이 변경되지 않습니다. 서비스는 latest-request-wins 취소 정책을 유지하고 마지막 논리 요청 전체를 보관하므로 재시도에서 타입·설정·공급자 연결이 유실되지 않습니다.

`InventoryGameWindowPage.CreateDefaultRequest`는 `ItemGrid`, 현재 Grid 설정 사본과 `InventoryModalDataProvider`를 구성합니다. 공급자는 기존 `PlayerSessionHost`와 `ItemCatalog`를 연결할 뿐 별도 `PlayerState`나 아이템 목록을 만들지 않습니다. `MessageGameWindowPage`를 사용하는 System 요청은 `CustomContent`이며 공급자와 Grid 설정을 요구하지 않습니다. 잘못된 타입과 호환되지 않는 공급자는 각각 진단 가능한 요청 오류가 되며 임의의 다른 창으로 대체되지 않습니다.

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
## 공통 모달 콘텐츠 영역

`ModalWindowHost`는 제목, 닫기, 로딩·오류 및 입력 차단을 유지하며 `ModalContentContainer`, `ErrorStateRoot`, 선택적 재시도 버튼을 소유합니다. `ErrorStateRoot`는 페이지 Grid 및 `ContentRoot`의 레이아웃 계산과 분리되어 본문 가용 폭을 사용하므로, 긴 오류 문구가 한 글자씩 세로로 붕괴하지 않습니다. 페이지 선택, 비동기 준비, 취소와 포커스 복원은 계속 `GameWindowService`가 담당합니다.

```text
ModalWindowHost
└── Window
    ├── Title / CloseButton
    ├── ContentContainer
    │   └── Pages
    │       ├── InventoryPage
    │       ├── statusPage
    │       └── systemPage
    ├── ErrorStateRoot
    │   ├── Error
    │   └── RetryButton
    └── OverlayRoot
```

`ContentContainer`에는 `ScrollRect`를 두지 않습니다. 가방의 세로 스크롤은 `InventoryPage/ItemGrid`가 담당하며, 일반 페이지는 Grid나 카테고리를 요구하지 않습니다. `OverlayRoot`는 컨텍스트 메뉴와 후속 보조 UI가 본문 마스크에 잘리지 않도록 분리한 영역입니다.

메뉴 페이지 ID, 가방 카테고리 ID와 번호 페이지는 서로 다른 식별 체계입니다. `GameWindowService`가 메뉴 페이지를 선택하고, `InventoryGameWindowPage`가 동일한 소유 목록을 카테고리로 필터링한 뒤 안정적으로 정렬하고 페이지를 나눕니다.

### 가방 Grid 설정 소유권

`InventoryGameWindowPage.gridSettings`의 `GridContentLayoutSettings`가 표시 모드와 Grid 표시 정책을 한 번만 소유합니다. 신규 설정의 기본값은 `Paged`, 페이지당 12칸과 최대 4열의 반응형 배치입니다. 기존 Prefab이나 Scene에 명시적으로 저장된 `VerticalScroll` 값은 이전 과정에서 덮어쓰지 않습니다. `VerticalScroll`은 필터 결과 전체와 `Minimum Scroll Slots` 중 큰 수만큼 칸을 표시합니다. `Paged`는 `Slots Per Page`만 바인딩하고, 선택적으로 마지막 페이지를 빈 칸으로 채웁니다. 두 정책은 실제 `InventoryState`의 수량이나 용량을 변경하지 않습니다.

`Column Policy = Adaptive Up To Configured`는 기존 `ActionGridLayoutMode.FixedColumns`의 최대 열 의미를 보존합니다. `Exact`는 새 `ActionGridLayoutMode.ExactColumns`를 사용하여 설정한 열 수와 Cell 크기를 유지합니다. 좁은 Viewport에서는 부족 공간이 발생할 수 있으며 항목을 누락하거나 열 수를 조용히 바꾸지 않습니다.

`InventoryGameWindowPage`는 `GridContentLayoutSettings.CreateSafeCopy`로 표시 설정의 복사본을 검증한 뒤 사용합니다. 따라서 런타임 보정이 Prefab이나 공유 설정 원본을 변경하지 않습니다. null, 알 수 없는 enum 값, NaN·Infinity, 음수 및 과도한 값은 안전 범위로 대체하지만, 플레이어의 `InventoryState`와 아이템 수량은 변경하지 않습니다. 현재 상한은 슬롯 512개, 열 32개, Cell 1024px, 간격 256px, Padding 512px입니다.

필수 의존성 오류는 `inventory.session-host-missing`, `inventory.catalog-missing`, `inventory.item-grid-missing`으로 구분하여 개발 로그에 원래 예외와 계층 경로를 남깁니다. 플레이어에게는 내부 경로나 예외 원문 대신 안전한 공통 문구를 표시합니다. 취소된 준비 요청은 오류로 처리하지 않으며, 재시도 버튼은 현재 페이지 준비 작업만 다시 요청합니다.

## 임시 명령 버튼

`GameMenuButtonBinding.actionKind`의 기본값 `OpenPage(0)`은 기존 직렬화 자산의 창 열기 동작을 보존합니다. `Command` 바인딩은 가짜 Page ID를 만들지 않고 `IGameMenuCommandHandler`로 전달됩니다. TMP_MainScene의 `TemporaryDiceRoll`만 이 경로를 사용하며, 세션과 StoryTextPanel이 모두 준비되고 보유 주사위가 있을 때만 활성화됩니다. 기존 Inventory, System, Status 버튼은 계속 `GameWindowService`를 사용합니다.
