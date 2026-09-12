# TMP_MainScene Inventory 입력 수정과 빈 시스템 설정 모달

## 목적

TMP_MainScene의 기존 GameMenuPanel에서 Inventory 버튼이 동작하지 않는 원인을 재현하여 수정하고, 시스템 설정 버튼으로 빈 모달 창을 열 수 있게 합니다. 이번 요청은 지침서 작성이며, 아래 항목은 후속 구현자가 수행할 작업입니다.

## 사전 확인과 대상

AGENTS.md, DOCS/architecture/quick-items-and-game-windows.md, DOCS/architecture/player-session.md, DOCS/development/workflows.md 및 DOCS/instructions/main-scene-inventory-menu-integration.md를 읽습니다. 기존 지침의 수동 메뉴 실행 절차보다 최신 AGENTS.md의 직접 Editor 적용 정책을 우선합니다.

| 경로 | 역할 |
| --- | --- |
| Assets/Scenes/TMP_MainScene.unity | 사용자 메뉴 배치를 보존하며 수정할 Scene입니다. |
| Assets/TxTRPG/UI/Runtime/Windows/GameMenuPanel.cs | 내부 구성 검증, 서비스 바인딩과 버튼 interactable 조건을 확인합니다. |
| Assets/TxTRPG/UI/Runtime/Windows/GameMenuCompositionBinder.cs | 기존 서비스의 명시적인 연결·해제를 담당합니다. |
| Assets/TxTRPG/UI/Runtime/Windows/GameWindowSystem.cs | GamePageIds, GameWindowService, ModalWindowHost와 MessageGameWindowPage를 제공합니다. |
| Assets/TxTRPG/Application/Runtime/Items/PlayerGameWindowPages.cs | 실제 InventoryGameWindowPage의 준비와 실행을 확인합니다. |
| Assets/TxTRPG/Application/Editor/QuickItemsUiProjectBuilder.cs | 기존 메뉴·페이지 생성 의도와 생성 자산의 소유권을 확인합니다. |
| Assets/TxTRPG/UI/Prefabs/GameMenuPanel.prefab | 필요한 내부 구성만 수정하며 외부 Scene 참조를 저장하지 않습니다. |
| Assets/TxTRPG/UI/Prefabs/InventoryWindowPage.prefab | 실제 가방 페이지 자산의 참조를 검사합니다. |

현재 확인한 GameMenuPanel은 내부 구성이 유효하고 windowService가 있으며 HasPage(pageId)가 참일 때 버튼을 활성화합니다. 이 조건은 유력한 조사 대상이지 실제 문제의 확정 원인은 아닙니다.

기존 생성 코드에는 system ID의 버튼과 MessageGameWindowPage가 있습니다. 따라서 새 settings ID와 중복 버튼을 만들기 전에 현재 Scene의 system 연결을 확인합니다.

## 범위와 비범위

- Inventory의 실제 입력부터 가방 표시까지의 실패를 수정합니다.
- 시스템 설정 메뉴와 빈 모달을 기존 공통 창 구조에 연결합니다.
- 필요한 코드·Prefab·Scene 연결·저장·검증까지 수행합니다.
- 음량, 언어, 해상도, 키 설정, 저장 버튼, 설정 저장소 등 실제 설정 기능은 추가하지 않습니다.
- Inventory의 기존 사용·등록 규칙이나 저장 형식은 변경하지 않습니다.
- 일회성 적용 Tools 메뉴를 추가하지 않고 전체 Scene/Prefab Rebuild도 기본 적용 방법으로 사용하지 않습니다.

## 1. Inventory 버튼의 실패 단계를 구분합니다

Editor 연결, 프로젝트 경로, 컴파일 오류, 미저장 변경부터 확인합니다. 실제 TMP_MainScene의 사용자 메뉴를 대상으로 다음을 검사합니다.

1. 버튼이 활성 상태인지, Button.interactable과 상위 CanvasGroup.interactable이 유효한지 확인합니다.
2. EventSystem과 입력 모듈, Canvas의 GraphicRaycaster, 버튼의 Raycast 대상과 실제 화면 Rect를 확인합니다.
3. 투명한 Image, 닫힌 모달의 Dimmer, 전환 InputBlocker 등이 버튼 앞에서 입력을 가로채는지 확인합니다. CanvasGroup.alpha=0만으로 입력이 통과한다고 가정하지 않습니다.
4. 클릭 이벤트가 발생하는지, GameMenuPanel.Open에 도달하는지 구분합니다.
5. 내부 Viewport·Content·레이아웃 참조와 서비스 바인딩 실패 이유를 확인합니다.
6. 서비스에 inventory가 정확히 한 번 등록되어 있고 실제 최근 InventoryGameWindowPage를 가리키는지 확인합니다.
7. 클릭은 들어오지만 PrepareAsync에서 실패하는 경우 세션, ItemCatalog, itemGrid 및 표시 자산 준비 오류를 확인합니다.
8. Binder 연결 후 버튼 활성 상태가 갱신되는지, 뒤늦은 초기화·Unbind·페이지 목록 변경이 상태를 덮어쓰는지 확인합니다.
9. 창은 열리지만 마스크·Canvas 정렬·비활성 부모·크기 문제로 보이지 않는 경우를 구분합니다.

실제 관찰 결과와 원인을 보고할 수 있도록 필요한 상태를 기록합니다. 매 프레임 로그나 무조건적인 interactable=true로 문제를 가리지 않습니다.

## 2. 원인에 맞는 최소 수정

- 내부 필수 참조 누락은 저장된 자산 또는 해당 Scene 인스턴스에서 복구합니다.
- 기존 공유 GameWindowService와 Binder를 재사용하고 중복 서비스·플레이어 세션을 생성하지 않습니다.
- 준비 완료 후 실행 가능 상태를 명시적으로 갱신합니다. 매 프레임 검색이나 지연 시간 고정으로 해결하지 않습니다.
- 의도된 모달·Scene 전환 입력 차단은 유지하고, 닫힌 UI가 차단을 남기는 경우만 수정합니다.
- 장식용 Image의 불필요한 Raycast만 제거합니다. 실제 버튼과 모달 배경의 필요한 Raycast까지 모두 끄지 않습니다.
- 기존 사용자 onClick 리스너를 RemoveAllListeners로 지우지 않습니다. 재활성화 시 중복 실행되지 않게 소유 리스너만 관리합니다.
- 유효한 빈 인벤토리는 가방 창의 EmptyState로 표시합니다. 아이템이 없다는 이유로 가방 버튼을 비활성화하지 않습니다.
- 구성 오류나 준비 실패는 원인을 제공하고 복구 후 다시 시도할 수 있게 합니다. 데이터를 초기화하여 성공으로 위장하지 않습니다.

## 3. 시스템 설정 버튼과 빈 모달

기존 GamePageIds.System의 system ID를 유지합니다. System 버튼이 있으면 재사용하고, 없다면 기존 메뉴 버튼 구조를 따르는 버튼 하나만 추가합니다.

- 사용자 표시명은 현지화 가능한 시스템 설정 또는 설정으로 제공합니다.
- 기존 GameMenuButtonView의 이미지·라벨·포커스 상태 구조를 사용합니다. 전용 아이콘이 없으면 기존 자산 또는 라벨 대체 표시를 사용하며 임의 외부 이미지를 다운로드하지 않습니다.
- 버튼 크기·간격·정렬과 스크롤/Wrap 정책은 기존 GameMenuPanel 설정을 따릅니다.
- 같은 GameWindowService의 system 페이지로 연결합니다. 메뉴 전용 별도 창 서비스는 만들지 않습니다.
- 기존 MessageGameWindowPage를 빈 본문으로 사용할 수 있으면 재사용합니다. 가방이나 플레이어 데이터 준비에 불필요하게 의존하지 않는 페이지여야 합니다.
- 모달은 제목, 배경, 닫기 버튼과 비어 있는 ContentRoot만 표시합니다. 필요하지 않은 “미구현” 안내 문구와 설정 컨트롤은 넣지 않습니다.
- 향후 설정 요소는 ContentRoot 아래에 조합할 수 있게 합니다. 아직 필요하지 않은 설정 프레임워크나 저장 모델은 구현하지 않습니다.
- 필요하면 가벼운 SettingsWindowPage를 추가할 수 있지만, 기존 페이지로 충분하지 않은 이유를 설명합니다.
- 모달 영역은 메뉴 Viewport 및 상위 레이아웃 마스크 밖에서 정상 표시되어야 합니다.
- 닫기 버튼, Cancel/Escape와 게임패드 취소를 지원하고 닫은 뒤 시스템 설정 버튼으로 포커스를 복원합니다.
- 설정 컨트롤이 없으므로 최초 포커스는 닫기 버튼으로 지정합니다.
- 기존 단일 모달 정책을 유지하고 반복 열기·닫기에서 창을 누적 생성하지 않습니다.

## 4. 안전한 적용과 실행 경로

Unity 아키텍처 지침에 따라 메뉴는 요청, 서비스는 창 수명, 페이지는 콘텐츠를 담당하도록 유지합니다. 이번 작업은 소규모 수정이며 새 전역 관리자나 범용 도구 프레임워크를 추가하지 않습니다.

- Scene의 기존 메뉴 위치·크기·부모·형제 순서·이미지·Prefab Override를 보존합니다.
- 플레이어 세션은 기존 AppScene 실행 경로를 사용합니다. 다른 Scene 객체에 대한 영구 Inspector 참조를 강제로 저장하지 않습니다.
- TMP_MainScene 직접 Play도 재현하되 기존 Bootstrap 지원 여부를 확인합니다. 서비스가 없는 실행 경로의 문제와 버튼 입력 문제를 구분합니다.
- 필요한 변경은 Editor 자동화로 직접 적용·저장·재로드 검증합니다. 기존 Integrate 메뉴 실행을 사용자에게 남기고 완료 처리하지 않습니다.
- 기존 생성형 자산의 핵심 생성 코드도 변경이 필요하면 일관되게 갱신하되, 개발자 작성 자산을 무조건 생성 코드로 교체하지 않습니다.
- 새 영구 Tools 메뉴나 컴파일·OnValidate 시 자동 Rebuild를 추가하지 않습니다.
- 미저장 사용자 변경, 권한 또는 Editor 연결로 막히면 구체적인 차단 원인과 최소 필요한 조치를 보고합니다. 권한을 우회하거나 사용자 변경을 폐기하지 않습니다.
- 검증 중 사용자 저장 데이터에 아이템 지급·사용·삭제를 수행하지 않습니다. 데이터 변경 테스트는 격리된 세션에서 수행합니다.

## 5. 검증 및 완료 기준

- 수정 전 Inventory 실패를 재현하고, 수정 후 같은 실행 경로에서 클릭이 가방 표시까지 도달하는지 검증합니다.
- 메서드 직접 호출만으로 입력 검증을 대체하지 않습니다. 실제 버튼 클릭 또는 EventSystem 입력 경로를 검사합니다.
- Inventory가 정상 데이터와 빈 데이터 각각에서 열리고, 닫은 후 다시 열리는지 확인합니다.
- 시스템 설정 버튼을 누르면 빈 본문의 모달이 표시되고, 설정 항목이나 저장 기능이 없는지 확인합니다.
- Inventory와 System을 각각 열고 닫은 뒤 다른 메뉴를 열어 입력 차단이 남지 않는지 확인합니다.
- 로딩 중 중복 클릭과 반복 활성화에서 이벤트·모달·구독이 중복되지 않는지 확인합니다.
- 닫힌 모달과 전환 완료 후 Overlay가 버튼 Raycast를 가로채지 않는지 확인합니다.
- 메뉴의 마우스·터치·키보드·게임패드 Submit과 Cancel, 포커스 복원을 검사합니다.
- 좁은 화면과 넓은 화면에서 버튼 접근, 모달 잘림, 닫기 버튼의 가시성을 확인합니다.
- 기존 가방 탭·Grid·스크롤·페이지 전환과 Status 등 다른 메뉴에 회귀가 없는지 확인합니다.
- 저장한 Scene과 필요한 Prefab을 다시 로드하여 연결이 유지되는지 확인합니다.
- 실제 AppScene → TMP_MainScene 실행 결과와 TMP_MainScene 직접 실행 결과를 구분하여 보고합니다.
- 적용 전후 Scene diff와 스크린샷으로 의도하지 않은 레이아웃 변경이 없는지 확인합니다.

## 6. 문서 및 최종 보고

DOCS/architecture/quick-items-and-game-windows.md에 설정 모달의 현재 범위와 실제 수정한 입력·초기화 동작을 반영합니다. DOCS/development/workflows.md에는 실행 시작 경로와 검증 방법을 기록하되 수동 적용 메뉴 실행을 기본 필수 절차로 남기지 않습니다.

최종 보고에는 확인된 원인, 수정 경로, 재사용/추가한 버튼과 페이지, 실제 적용·저장 상태, 실행한 테스트, 미검증 환경과 차단 사항을 포함합니다. 코드 컴파일이나 새 메뉴 생성만으로 완료했다고 보고하지 않습니다.

