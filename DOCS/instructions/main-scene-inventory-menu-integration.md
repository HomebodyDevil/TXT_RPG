# TMP_MainScene의 기존 GameMenuPanel에 가방 창 연결하기

## 목적과 범위

Assets/Scenes/TMP_MainScene.unity에 배치된 기존 GameMenuPanel의 inventory 버튼으로 최근 구현한 카테고리형 가방 창을 실제 사용할 수 있게 연결합니다. 새 메뉴·인벤토리 시스템을 만들지 않습니다.

기존 화면의 배치, 부모, 형제 순서, 크기, FlexibleLayoutItem, 이미지 및 레이아웃 옵션을 보존합니다. 연결에 필요한 서비스·모달·페이지가 없다면 해당 요소만 추가합니다. Scene 전체 Rebuild, GameMenuScreen 중복 배치, 플레이어 데이터 초기화, 새로운 아이템 규칙 구현은 범위 밖입니다.

## 사전 확인과 현재 근거

AGENTS.md와 다음 문서를 읽습니다.

- DOCS/architecture/quick-items-and-game-windows.md
- DOCS/architecture/player-session.md
- DOCS/architecture/scene-transition.md
- DOCS/instructions/image-menu-and-inventory-window.md
- DOCS/instructions/game-menu-prefab-default-composition.md
- DOCS/development/workflows.md

| 확인한 경로 | 역할과 확인 내용 |
| --- | --- |
| Assets/Scenes/TMP_MainScene.unity | 저장된 Scene에 GameMenuPanel 이름이 있습니다. 실제 서비스·페이지 연결과 미저장 Editor 상태는 구현 시작 시 추가 확인합니다. |
| Assets/TxTRPG/UI/Prefabs/GameMenuPanel.prefab | 외부 창 서비스와 선택적 퀵 Grid를 연결할 기본 메뉴입니다. |
| Assets/TxTRPG/UI/Prefabs/InventoryWindowPage.prefab | 최근 구현한 가방 페이지 자산입니다. 내부 참조의 유효성은 실제 로드 후 검증합니다. |
| Assets/TxTRPG/UI/Runtime/Windows/GameMenuCompositionBinder.cs | 메뉴와 GameWindowService 및 선택적 Grid를 Bind합니다. 다른 Inspector 서비스를 기본적으로 덮어쓰지 않습니다. |
| Assets/TxTRPG/UI/Runtime/Windows/GameWindowSystem.cs | inventory 페이지 ID, 페이지 목록, 모달 서비스와 호스트를 제공합니다. |
| Assets/TxTRPG/Application/Runtime/Items/PlayerGameWindowPages.cs | InventoryGameWindowPage.PrepareAsync는 PlayerSessionHost, ItemCatalog, itemGrid를 요구합니다. sessionHost가 없으면 기존 Instance 경로를 사용합니다. |
| Assets/TxTRPG/Application/Editor/QuickItemsUiProjectBuilder.cs | 메뉴·모달 생성과 Scene 구성 경로입니다. 기존 Scene 재구성 메서드를 무조건 실행하지 않습니다. |

현재 문서에 따르면 가방은 VerticalScroll/Paged, 카테고리, 지원되는 사용 및 퀵 슬롯 등록을 제공합니다. 점유 슬롯 교체와 버리기 등 미구현 명령을 이번 연결 작업에서 완료된 것으로 취급하지 않습니다.

## 1. 수정 전에 실제 연결을 기록합니다

Editor에서 현재 열린 TMP_MainScene과 디스크의 차이 및 미저장 변경을 확인합니다. 미저장 작업을 자동 폐기하거나 덮어쓰지 않습니다.

다음 항목의 실제 Hierarchy 경로와 참조를 기록합니다.

- 사용자가 배치한 GameMenuPanel과 inventory 버튼입니다.
- 기존 GameWindowService, ModalWindowHost, 등록된 페이지 목록입니다.
- 기존 운영용 ActionGridPanel과 QuickItemGridPresenter입니다.
- Canvas, 모달 Overlay, EventSystem과 입력 모듈입니다.
- 플레이어 세션 초기화 경로와 ItemCatalog입니다.

이름이나 FindFirstObjectByType의 첫 결과만으로 연결 대상을 선택하지 않습니다. 후보가 여러 개이면 용도와 참조를 확인하고 모호한 경우 사용자에게 확인합니다.

## 2. 기존 메뉴에 연결합니다

- inventory 바인딩이 있으면 재사용하고 visible 및 실행 가능 상태를 확인합니다.
- 바인딩이 없으면 기존 버튼 구조를 따르는 가방 버튼 하나만 추가합니다. 추가로 인한 기존 메뉴 배치 변화는 보고합니다.
- 기존 이미지와 사용자 설정을 유지합니다. 가방 아이콘이 없을 때만 최근 구현한 기본 이미지 또는 명시적 대체 표시를 연결합니다.
- 기존 GameWindowService가 있으면 사용하고, 그 서비스에 inventory 페이지가 정확히 한 개 등록되도록 합니다.
- 구형 단순 inventory 페이지가 연결되어 있다면 새 InventoryWindowPage 인스턴스로 해당 등록만 교체합니다. System과 Status 등 다른 페이지는 유지합니다.
- 유효한 기존 바인딩을 자동 교체하지 않습니다. GameMenuCompositionBinder를 사용할 때 replaceInspectorService와 기존 참조의 충돌을 확인합니다.
- 창 서비스가 없다면 메뉴 외부에 필요한 서비스·모달 호스트만 추가합니다. GameMenuScreen을 그대로 추가하여 메뉴를 두 개 만들지 않습니다.
- 기존 운영 퀵 Grid를 선택적 quickItemGrid에 연결합니다. 가방 내부 itemGrid를 이 필드에 잘못 연결하지 않습니다.
- 메뉴 연결은 Scene 인스턴스 또는 명시적 Binder에 저장합니다. 공용 GameMenuPanel.prefab에 Scene 객체 참조를 Apply하지 않습니다.

## 3. 모달과 가방 내부 구성

- 모달은 메뉴·Grid의 Viewport와 FlexibleLayoutPanel 마스크 밖의 Overlay에 배치합니다.
- 가방 페이지는 기존 모달의 콘텐츠 영역 안에 배치하고 화면 크기와 Safe Area를 고려합니다.
- GameMenuCompositionBinder의 overlayRoot 필드에 참조를 넣었다는 이유만으로 실제 계층이 이동된다고 가정하지 않습니다. 현재 Bind 메서드는 메뉴 의존성 연결만 수행하므로 실제 모달의 부모와 RectTransform을 검증합니다.
- InventoryWindowPage의 CategoryTabs, itemGrid, 페이지 컨트롤, 빈 목록·결과 표시 등 필수 내부 참조를 검사합니다.
- ItemCatalog는 기존 운영 카탈로그를 사용합니다. 메뉴를 열기 위해 새 아이템 카탈로그를 생성하거나 기존 정의를 초기화하지 않습니다.
- 표시 모드와 itemsPerPage 등 이미 작성된 설정을 유지합니다. 유효한 설정이 없을 때만 기본 VerticalScroll을 사용하고 적용 값을 보고합니다.
- 페이지 모드에서 높이가 부족하면 임의로 Slot을 숨기지 않습니다. 필요한 최소 크기를 진단하고 가방 본문 범위에서 조정합니다.
- 모달과 Grid 컨텍스트 메뉴는 정상 닫기·취소 경로 및 포커스 복원을 지원해야 합니다.

## 4. 플레이어 세션과 실행 경로

운영 데이터는 AppScene의 기존 PlayerSessionHost가 소유하는 PlayerState와 InventoryState를 사용합니다. TMP_MainScene에 별도의 플레이어 세션이나 임시 인벤토리를 만들지 않습니다.

- 정식 검증은 Assets/Scenes/AppScene.unity에서 시작하여 TMP_MainScene이 로드되는 경로로 수행합니다.
- AppScene 객체에 대한 영구적인 cross-scene Inspector 참조를 강제로 저장하지 않습니다. 기존 세션 접근 경로나 명시적인 런타임 구성 경계를 사용합니다.
- 가방 페이지 준비가 완료되기 전에 아이템 명령을 허용하지 않습니다.
- TMP_MainScene 직접 Play가 현재 프로젝트에서 지원되는지 확인합니다. 기존 Editor 시작 Scene/Bootstrap 경로가 있으면 재사용합니다.
- 직접 Play에서 AppScene 서비스가 없다면 원인을 명확히 표시하고 정식 실행 방법을 문서화합니다. 이번 작업을 이유로 별도 자동 Bootstrap이나 숨겨진 Singleton을 추가하지 않습니다.
- 사용자가 실제로 TMP_MainScene 직접 Play를 요구하고 기존 지원 경로가 없다면, 씬 초기화 변경의 추가 범위를 설명하고 확인받습니다.
- 저장된 아이템이 없으면 정상적인 빈 가방을 표시합니다. 빈 목록과 연결 오류를 구분합니다.

## 5. 데이터와 상호작용 검증

- 지원되는 아이템 선택 시 기존 컨텍스트 메뉴가 표시되어야 합니다.
- 가방의 사용은 기존 UseItemAsync 경로로 실행하고 퀵 슬롯을 임시 변경하지 않습니다.
- 퀵 슬롯 등록 후 기존 화면의 ActionGridPanel에 같은 PlayerState의 변경이 반영되어야 합니다. UI 슬롯에만 아이콘을 수동 삽입하지 않습니다.
- 다른 아이템이 점유한 퀵 슬롯이나 버리기처럼 아직 지원하지 않는 명령은 기존 비활성 정책을 유지합니다.
- 모달이 열려 있는 동안 배경의 아이템 사용이나 메뉴 입력이 중복 실행되지 않는지 확인합니다.
- 닫기와 Cancel 후 포커스가 기존 가방 버튼으로 돌아와야 합니다.
- 반복 열기·닫기와 Scene 전환에서 구독, 아이콘 로드, 모달 인스턴스가 누적되지 않아야 합니다.

## 6. 안전한 변경 및 테스트

1. 수정 전 Scene 구성과 관련 Override를 기록합니다.
2. 기존 요소를 재사용하여 참조 연결과 필요한 모달·페이지 추가만 수행합니다.
3. Missing Script와 누락 참조를 검증하고 Scene을 저장한 뒤 재로드합니다.
4. AppScene 시작 경로에서 실제 가방 버튼으로 창을 엽니다.
5. 빈 가방, 아이템이 있는 가방, 카테고리 전환, 스크롤과 페이지 전환을 검사합니다.
6. 아이템 사용·등록, 재고 부족, 세션 준비 실패, 창 반복 열기·닫기를 검사합니다.
7. 마우스·키보드·게임패드·터치 경로와 좁은 화면의 모달·컨텍스트 메뉴 잘림을 확인합니다.
8. 기존 System/Status 버튼 및 다른 화면 요소가 유지되는지 회귀 확인합니다.
9. Scene diff에서 배치나 무관한 데이터가 변경되지 않았는지 검토합니다.

아이템이 있는 상태의 검증은 별도 테스트 세션 또는 격리된 검증 환경에서 수행합니다. 사용자의 저장 파일에 아이템을 추가하거나 삭제하지 않습니다. 테스트를 위해 신규 게임에 아이템 자동 지급 로직을 넣지 않습니다.

전체 Prefab Rebuild 또는 ConfigureScene을 검증의 기본 단계로 실행하지 않습니다. 연결에 필수인 자산 복구가 필요할 때만 해당 출력에 한정하고 이유를 보고합니다.

## 7. 문서와 완료 보고

DOCS/development/workflows.md에 TMP_MainScene에서 가방을 여는 실제 실행 절차와 서비스·페이지 연결 경로를 기록합니다. DOCS/architecture/quick-items-and-game-windows.md에는 Scene의 구성 방식과 데이터 소유권을 반영합니다.

최종 보고에는 다음을 포함합니다.

- 연결한 기존 메뉴, 서비스, 모달, 가방 페이지의 실제 Hierarchy 경로입니다.
- 새로 추가하거나 교체한 객체와 보존한 설정입니다.
- 실제 실행 시작 Scene과 직접 TMP_MainScene Play 지원 여부입니다.
- 가방 표시 모드, 사용 가능한 명령과 미구현 명령입니다.
- 실제 Play Mode 테스트와 스크린샷, 미검증 입력·환경입니다.
- 사용자 저장 데이터와 다른 Scene 요소를 변경하지 않았는지에 대한 확인입니다.

코드와 자산 참조 검사만으로 사용 가능하다고 판정하지 않습니다. 기존 TMP_MainScene의 버튼에서 실제 가방 창이 열리는 것을 완료 기준으로 합니다.

