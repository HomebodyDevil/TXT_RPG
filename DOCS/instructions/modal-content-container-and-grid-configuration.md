# 모달 공통 콘텐츠 영역과 카테고리형 그리드 설정 지침

## 목적과 범위

TMP_MainScene의 GameMenuPanel에서 여는 모달에 공통 Content Container를 마련합니다. 가방처럼 Slot을 표시하는 본문과 시스템 설정처럼 일반 요소를 담는 본문을 같은 모달 안에서 교체할 수 있게 합니다.

슬롯 개수·열 수를 개발자가 설정하고, 카테고리별 결과를 번호 페이지 또는 하나의 세로 스크롤 목록으로 표시하도록 구성합니다. 기존 가방 구현을 확장하며 새 인벤토리나 창 서비스를 중복 구현하지 않습니다.

이번 문서는 후속 구현 명세입니다. 실제 설정 항목, 장비 기능, 아이템 버리기, 저장 형식 변경, 가상화 프레임워크는 범위 밖입니다. 시스템 메뉴의 본문은 계속 비워 두되 추후 요소를 추가할 수 있게 합니다.

## 사전 확인과 영향 경로

AGENTS.md, DOCS/architecture/quick-items-and-game-windows.md, DOCS/architecture/action-grid-panel.md, DOCS/development/workflows.md를 읽습니다.

| 현재 확인한 경로 | 책임과 영향 |
| --- | --- |
| Assets/TxTRPG/UI/Runtime/Windows/ModalWindowHost.cs | 제목, 닫기, 로딩·오류와 CanvasGroup을 관리합니다. 현재 명시적 콘텐츠 루트 필드는 없습니다. |
| Assets/TxTRPG/UI/Runtime/Windows/GameWindowService.cs | 페이지 교체와 준비·취소·포커스 수명의 기존 소유자입니다. |
| Assets/TxTRPG/UI/Runtime/Windows/GameWindowPage.cs | 메뉴 페이지의 기존 계약입니다. 유지합니다. |
| Assets/TxTRPG/Application/Runtime/Items/PlayerGameWindowPages.cs | InventoryGameWindowPage에 itemGrid, 카테고리, displayMode, itemsPerPage와 빈 칸 옵션이 있습니다. |
| Assets/TxTRPG/Application/Runtime/Items/InventoryWindowModels.cs | InventoryDisplayMode, InventoryCategoryDefinition, InventoryProjectionBuilder가 이미 있습니다. |
| Assets/TxTRPG/UI/Runtime/ActionGridPanel.cs | 기존 슬롯 배치와 입력·컨텍스트 메뉴를 재사용합니다. |
| Assets/TxTRPG/UI/Prefabs/InventoryWindowPage.prefab | 가방 본문의 기존 자산입니다. |
| Assets/TxTRPG/UI/Prefabs/ModalWindowHost.prefab | 공통 콘텐츠 영역을 적용할 자산입니다. |
| Assets/Scenes/TMP_MainScene.unity | 기존 메뉴 연결과 레이아웃을 보존하며 적용합니다. |

기존 VerticalScroll/Paged와 필터·페이지 계산은 이미 구현되어 있습니다. 전체를 다시 만들지 말고 필요한 설정과 소유권만 보완합니다. 실제 Editor 계층은 구현 시작 시 확인합니다.

## 1. 공통 Container와 메뉴 본문을 분리합니다

권장 신규 이름은 ModalContentContainer입니다. 기존 RectTransform과 작은 컴포넌트로 충분하며 별도 범용 UI 프레임워크를 만들지 않습니다.

권장 계층은 다음과 같습니다. 실제 이름이 다르면 기존 객체를 재사용합니다.

```text
ModalWindowHost
├── Header / CloseButton
├── ContentContainer
│   └── 현재 GameWindowPage
│       ├── 가방: CategoryTabs / ItemGrid / Pagination / 상태 표시
│       └── 시스템: 빈 OptionsRoot
└── OverlayRoot               컨텍스트 메뉴와 보조 선택 UI
```

- Container는 본문 영역, Padding, 자식 페이지의 크기 배정만 소유합니다.
- 어떤 페이지를 열고 닫는지는 GameWindowService가 계속 소유합니다. Container가 별도 페이지 목록·취소 토큰·히스토리를 중복 관리하지 않습니다.
- 등록된 기존 페이지를 연결하거나 필요한 경우 한 번 생성해 재사용합니다. 매번 창을 열 때 동일 페이지를 추가하지 않습니다.
- 일반 본문은 임의의 UI 요소를 담을 수 있어야 합니다. 시스템 페이지에 Grid·카테고리·페이지 버튼을 강제하지 않습니다.
- Container 자체에는 기본 ScrollRect를 넣지 않습니다. 가방 Grid의 ScrollRect와 이중 스크롤을 만들지 않습니다.
- 본문 영역은 제목과 닫기 영역을 침범하지 않으며 크기·Padding을 개발자가 조절할 수 있어야 합니다.
- 페이지는 Container 영역을 따릅니다. 부모 레이아웃과 ContentSizeFitter가 같은 RectTransform 크기를 경쟁 제어하지 않게 합니다.
- 컨텍스트 메뉴는 Grid 클리핑 밖의 OverlayRoot에서 표시하고 모달의 입력 범위와 화면 경계 안에 제한합니다.
- 프리팹 내부 필수 참조를 저장합니다. 이름 검색으로 매번 구조를 조립하지 않습니다.
- 옵션 본문의 재사용 가능성은 구조로 확보합니다. 실제 음량·언어 등 설정 요소나 설정 저장 모델은 추가하지 않습니다.

## 2. 설정은 Grid 본문에 둡니다

GridContentLayoutSettings 같은 직렬화 설정을 제안합니다. 명칭은 기존 코드 관례에 맞추고, 가방별 설정은 가방 페이지에 둡니다. 여러 화면이 실제로 공유해야 하면 ScriptableObject Profile을 사용합니다.

기존 itemsPerPage, displayMode, fillPageWithEmptySlots 값을 승계하고, 같은 값을 여러 컴포넌트가 독립적으로 소유하지 않게 합니다. Inspector에서는 유효한 설정과 계산 결과를 한곳에서 확인할 수 있게 합니다.

| 설정 | 의미 |
| --- | --- |
| DisplayMode | VerticalScroll 또는 Paged입니다. |
| SlotsPerPage | Paged의 한 페이지 최대 Slot 수입니다. 1 이상이며 기존 itemsPerPage를 승계합니다. |
| MinimumScrollSlots | VerticalScroll에서 표시할 최소 칸 수입니다. 0 이상이며 데이터가 더 많으면 모든 항목을 표시합니다. |
| FillPageWithEmptySlots | Paged 마지막 페이지의 남는 칸을 빈 Slot으로 채울지 선택합니다. |
| Columns | 개발자가 지정하는 열 수입니다. 1 이상입니다. |
| ColumnPolicy | Exact 또는 AdaptiveUpToConfigured를 선택합니다. |
| CellSize / Spacing / Padding | 셀 크기·간격·여백이며 기존 ActionGrid 설정과 통합합니다. |
| Alignment | 기존 수평·불완전 행·세로 배치 정책을 재사용합니다. |

슬롯 설정은 UI 표시 용량이지 실제 인벤토리 한도가 아닙니다. 화면 밖 항목을 도메인에서 제거하거나 기존 용량 축소 API로 버리지 않습니다.

기본 데이터만 한 번에 보이게 하는 것과 화면에 실제로 보이는 행 수는 다릅니다. 스크롤 모드의 MinimumScrollSlots는 전체 콘텐츠의 최소 칸 수이며 Viewport에 동시에 보이는 칸 수를 보장하지 않습니다. 동시 가시 행 수는 Viewport 높이와 셀 높이에서 계산하여 읽기 전용으로 표시합니다.

## 3. 열 수와 크기 정책을 명확히 합니다

- Exact는 Columns를 그대로 사용합니다. 부족한 폭에서 자동으로 개행하여 열 수를 바꾸지 않습니다.
- AdaptiveUpToConfigured는 현재 ActionGrid의 최대 열 수와 반응형 계산을 재사용하며 실제 열 수는 Columns 이하입니다.
- 기존 자산은 현재 반응형 의미를 유지하도록 AdaptiveUpToConfigured로 이전합니다. 개발자는 Exact로 명시적으로 전환할 수 있습니다.
- 기존 Fixed Columns가 실제로는 최대 열 수라는 점을 확인합니다. 새로운 Exact를 기존 enum 의미 변경으로 구현하지 않습니다.
- K개 Slot, C열의 행 수는 ceil(K/C)이며 마지막 행의 남는 칸과 정렬 정책은 서로 구분합니다.
- 기본적으로 데이터는 왼쪽에서 오른쪽, 위에서 아래 순서입니다. 정렬을 바꿔도 항목 순서가 바뀌지 않습니다.
- 필요한 너비는 좌우 Padding + C×셀 너비 + (C-1)×수평 간격입니다.
- 필요한 높이는 상하 Padding + 행 수×셀 높이 + max(0, 행 수-1)×수직 간격입니다.
- 계산에는 모달 전체가 아니라 탭·페이지 컨트롤·스크롤바 공간을 제외한 실제 Grid Viewport 크기를 사용합니다.
- Exact 너비 또는 Paged 높이가 부족하면 Inspector와 검증에서 오류를 표시합니다. 몰래 항목을 누락하거나 모드를 변경하지 않습니다.
- 지원할 모바일·데스크톱 크기에서 유효한 설정을 실제 적용합니다. 고정 크기·고정 칸 수·제한된 공간을 동시에 만족할 수 없는 구성은 개발자가 변경해야 함을 설명합니다.

## 4. 카테고리와 페이지의 관계

메뉴 페이지 ID(inventory/system), 카테고리 ID(consumable/equipment 등), 번호 페이지는 별개의 개념입니다.

- 카테고리는 하나의 소유 목록에 대한 필터이며 별도 InventoryState를 생성하지 않습니다.
- 데이터 조회 → 카테고리 필터 → 안정적인 정렬 → 페이지 분할 → UI 바인딩 순서를 유지합니다.
- 카테고리 목록은 기존 InventoryCategoryDefinition을 확장하여 ID, 표시 이름, 순서와 필요한 아이콘을 설정합니다.
- 소비품·장비 같은 분류를 추가할 수 있게 하되 장비 탭 추가가 장착 시스템 구현을 의미하지 않습니다.
- 전체와 기타의 기존 분류 정책, 미등록 정의의 대체 표시를 보존합니다.
- 각 카테고리는 Paged에서는 여러 번호 페이지의 묶음이고, VerticalScroll에서는 필터 결과 전체가 하나의 스크롤 목록입니다.
- 우선 메뉴 본문 단위로 DisplayMode와 Grid 설정을 적용합니다. 카테고리별 Override는 실제 필요가 없다면 구현하지 않습니다.
- 탭 전환 시 기본적으로 첫 페이지·상단으로 이동합니다. 같은 카테고리의 수량·아이콘 갱신은 선택 ID와 현재 위치를 가능한 한 유지합니다.
- 필터 결과가 줄면 현재 페이지를 마지막 유효 페이지로 보정합니다.
- 모드 전환 시 선택 항목이 속한 페이지/위치를 우선 유지하고 없으면 처음으로 이동합니다.
- 카테고리가 제거되면 전체 탭으로 복구합니다. 빈 탭은 기본 유지하고 명확한 EmptyState를 제공합니다.

## 5. 두 표시 모드의 계약

### Paged

- 필터 결과 N개, 페이지 용량 K에 대해 pageCount = max(1, ceil(N/K))입니다.
- 특정 페이지에 해당하는 항목만 Grid에 바인딩합니다.
- FillPageWithEmptySlots를 켜면 정확히 K개의 표시 칸을 구성합니다. 끄면 마지막 페이지는 실제 항목 수만 표시합니다.
- 빈 가방도 논리적으로 1페이지이며 페이지 이동은 비활성화합니다. 빈 칸 옵션과 EmptyState가 함께 보일 때 입력을 가리지 않게 합니다.
- 세로 스크롤은 끄고 이전·다음, 현재/전체 표시와 특정 페이지 선택을 제공합니다. 페이지가 많아도 모든 번호 버튼을 무제한 생성하지 않습니다.
- SlotsPerPage를 런타임에 바꾸면 선택 ID를 기준으로 새 페이지를 계산하고 결과가 없으면 유효 범위로 보정합니다.

### VerticalScroll

- 필터 결과 전체를 표시하고 표시 칸 수는 max(N, MinimumScrollSlots)입니다.
- SlotsPerPage는 적용하지 않습니다. 첫 K개만 표시하는 숨은 제한을 두지 않습니다.
- 페이지 이동 컨트롤은 숨기고 세로 스크롤을 허용합니다.
- 초기 콘텐츠와 레이아웃 준비 후 상단에서 시작합니다. 이후 사용자 스크롤을 일반 갱신마다 리셋하지 않습니다.
- 기존 스크롤바 Visibility·예약 공간·외형 설정을 재사용합니다.

## 6. 입력, 데이터와 수명

- 기존 ActionGridPanel, IActionMenuProvider, IActionCommandExecutor를 재사용합니다.
- 항목 없는 Slot은 사용·등록 컨텍스트 메뉴를 열지 않습니다.
- 가방의 기존 명령 정책을 유지합니다. 이번 작업에서 버리기나 점유 슬롯 교체 기능을 추가하지 않습니다.
- 탭·페이지·메뉴 교체 전에 기존 컨텍스트 메뉴를 닫고, 명령 실행 시 아이템 ID와 현재 상태를 재검증합니다.
- Content Container에 플레이어 데이터나 명령 실행 로직을 넣지 않습니다.
- 페이지 준비·교체·취소는 기존 서비스 경로를 사용합니다. 이전 요청 완료가 현재 본문을 덮어쓰지 않도록 합니다.
- 셀 풀 재사용과 비동기 아이콘 로드에 ID/요청 세대 및 Lease 해제를 적용합니다.
- 닫기·Cancel은 가장 안쪽 컨텍스트부터 처리하고 모달을 닫은 뒤 호출 메뉴 버튼으로 초점을 복원합니다.
- 탭과 페이지 이동 후 키보드·게임패드 초점이 숨긴 셀에 남지 않게 합니다. 터치에서 우클릭·hover를 요구하지 않습니다.
- 공통 Container 추가가 기존 모달 입력 차단, 설정 창 닫기, 가방 세션 연결을 깨뜨리지 않아야 합니다.

## 7. 적용 절차와 호환성

1. 실제 Scene 계층과 최신 프리팹·초기화 경로를 조사합니다.
2. 최소 Container와 참조를 추가하고 기존 inventory/system 페이지를 연결합니다.
3. 기존 설정을 손실 없이 통합하고 Exact 열 수 및 표시 슬롯 설정을 보완합니다.
4. 카테고리·페이지 계산과 입력 상태 갱신을 연결합니다.
5. TMP_MainScene의 기존 메뉴에서 실제 동작하도록 직접 적용·저장합니다.

Unity 아키텍처 지침에 따라 공통 호스팅과 메뉴별 본문을 분리합니다. 작은 재사용 경계를 추가하되 기존 가방을 범용 목록 프레임워크로 전면 재작성하지 않습니다.

새 일회성 Tools 메뉴를 추가하지 않습니다. 전체 Rebuild나 Scene 재생성을 기본 적용 방법으로 사용하지 않습니다. Editor 자동화로 필요한 부분만 수정하고 사용자 배치·이미지·Prefab Override와 저장 데이터를 보존합니다.

생성형 자산의 원본 코드가 있다면 해당 소유권에 맞는 최소 변경도 수행합니다. 기존 enum의 숫자와 직렬화 필드를 보존하거나 명시적으로 이전하며 개발자 설정을 기본값으로 덮어쓰지 않습니다.

## 8. 검증 기준

- inventory와 system이 같은 Container 영역을 사용하고 두 본문이 겹쳐 남지 않는지 확인합니다.
- 시스템 본문은 빈 상태이며 실제 설정 요소를 추가하지 않았는지 확인합니다.
- SlotsPerPage 1, 10, 12와 Columns 1, 3, 4 등을 조합하여 마지막 행과 빈 칸을 검사합니다.
- 10칸/4열이면 3행이며 마지막 행은 2칸이라는 계산을 검증합니다.
- 결과 수 0, K-1, K, K+1, 여러 페이지와 빈 카테고리를 검사합니다.
- 카테고리별 여러 페이지, 스크롤 전체 표시, Exact/Adaptive 열 계산을 검사합니다.
- 부족한 폭·높이가 조용한 항목 누락으로 처리되지 않는지 확인합니다.
- 수량 갱신·정렬·탭·모드 전환이 도메인 데이터를 변경하지 않는지 확인합니다.
- 느린 아이콘 로드, 반복 전환·열기·닫기, 취소와 오류 복구를 검사합니다.
- 컨텍스트 위치·입력 차단·포커스와 기존 사용·등록 동작을 회귀 검증합니다.
- 실제 저장된 Prefab을 다시 로드하여 필수 참조와 기존 Scene 연결을 확인합니다.
- AppScene 실행 경로의 TMP_MainScene에서 실제 메뉴 버튼으로 두 모드와 카테고리 전환을 검증합니다.
- 모바일·데스크톱 대표 화면과 입력 경로를 검사하고 실제 수행한 범위를 보고합니다.

테스트 데이터는 격리된 세션을 사용합니다. 사용자 저장 파일에 아이템을 지급하거나 소비하지 않습니다. 자동화 권한·연결이 막히면 구체적인 차단 원인을 보고하고 수동 절차 제공만으로 완료 처리하지 않습니다.

## 문서와 최종 보고

DOCS/architecture/quick-items-and-game-windows.md에 Container, 메뉴 페이지·카테고리·번호 페이지의 관계와 설정 소유권을 반영합니다. 공용 Grid API가 바뀌면 DOCS/architecture/action-grid-panel.md를 갱신합니다. DOCS/development/workflows.md에는 개발자의 실제 설정 위치와 예제를 추가합니다.

최종 보고에는 변경 경로, 기존 설정 이전 결과, 슬롯 수·열 수·표시 모드 설정 방법, 실제 적용된 Scene 연결, 수행한 테스트와 남은 제약을 포함합니다.

