# GameMenuPanel 기본 구성 완결성과 외부 서비스 연결 지침

## 목적

GameMenuPanel을 배치할 때 내부 UI를 수동으로 조립할 필요가 없도록 필수 구성 요소와 참조를 기본 프리팹에 포함합니다. 창 실행까지 연결된 통합 프리팹도 제공하되, 화면에서 공유하는 창 서비스와 기존 ActionGridPanel을 중복 생성하지 않습니다.

이 문서는 구현 계획입니다. 신규 형식과 자산명은 제안이며 현재 존재한다고 가정하지 않습니다. 장기 확장할 소규모 게임에 맞는 최소 구조를 사용합니다.

## 사전 확인

AGENTS.md와 다음 문서를 읽습니다.

- DOCS/architecture/quick-items-and-game-windows.md
- DOCS/architecture/flexible-layout-panel.md
- DOCS/development/workflows.md
- DOCS/instructions/game-menu-responsive-buttons.md

현재 확인한 소스와 자산은 다음과 같습니다.

| 경로 | 확인 결과 |
| --- | --- |
| Assets/TxTRPG/UI/Runtime/Windows/GameMenuPanel.cs | windowService, quickItemGrid, viewport, content, scrollRect, layout, horizontalScrollbar를 참조합니다. 내부 배치 참조가 없으면 ApplyLayout이 반환하며, 서비스가 없으면 Open에서 경고하고 종료합니다. |
| Assets/TxTRPG/UI/Prefabs/GameMenuPanel.prefab | 기존 HorizontalLayoutGroup 기반 구조가 남아 있고 viewport, content, scrollRect 및 외부 연결이 비어 있습니다. 최신 코드와 저장된 자산의 구성이 일치하지 않습니다. |
| Assets/TxTRPG/Application/Editor/QuickItemsUiProjectBuilder.cs | CreateOrUpdatePrefabs에서 메뉴의 형제 위치에 Grid와 모달을 만든 후 메뉴만 저장합니다. 별도 저장된 메뉴에는 이 외부 인스턴스들을 내부 참조로 보존할 수 없습니다. |
| Assets/TxTRPG/UI/Runtime/Windows/GameWindowSystem.cs | 창 서비스와 모달의 수명 및 페이지 연결을 확인할 대상입니다. |
| Assets/TxTRPG/UI/Editor/GameMenuPanelEditor.cs | 필수·선택·외부 의존성 표시와 검증을 추가할 대상입니다. |
| Assets/TxTRPG/UI/Tests/Editor/GameMenuLayoutTests.cs | 기존 레이아웃 회귀 검증 대상입니다. |

실제 Editor에서 로드된 컴포넌트, 미저장 Scene, Prefab override와 Missing Script 유무를 먼저 확인합니다. 현재 문서에 기술된 최신 계층이 자산에도 적용되었다고 가정하지 않습니다.

## 범위

- 내부 UI 참조 완성, 명시적 외부 연결, 통합 프리팹, 검증·마이그레이션·테스트를 구현합니다.
- 현재 수평 스크롤과 Wrap 설정, 버튼 크기·간격·정렬을 유지합니다.
- TMP_MainScene의 사용자 배치와 기존 창·Grid를 보존합니다.
- 게임 규칙, 저장 형식, 새 전역 Singleton, 새로운 인벤토리 및 실제 효과 셰이더 제작은 제외합니다.
- 모든 필드를 비어 있지 않게 만들기 위해 가짜 Grid나 사용하지 않는 서비스를 생성하지 않습니다.

## 1. 필수 내부 구성과 외부 의존성을 구분합니다

| 구분 | 기본 정책 |
| --- | --- |
| Viewport, Content, GameMenuLayoutGroup, ScrollRect | GameMenuPanel.prefab 내부에 포함하고 직렬화 참조를 저장합니다. |
| 버튼, GameMenuButtonView, VisualRoot와 기본 Graphic | 내부 또는 중첩 프리팹으로 포함하고 연결합니다. |
| HorizontalScrollbar, Handle, 배경 | 기본 프리팹에 포함하여 연결합니다. 기본 Visibility가 Hidden이어도 참조는 유지합니다. |
| GameWindowService와 ModalWindowHost | 화면에서 공유할 외부 의존성입니다. 통합 프리팹에서 기본 연결을 제공합니다. |
| Quick Item Grid | 메뉴를 열기 전에 기존 컨텍스트 메뉴를 닫는 선택적 협력 대상입니다. 메뉴 내부에 복제하지 않습니다. |
| Canvas와 EventSystem | 화면 환경이 제공합니다. 메뉴 인스턴스마다 생성하지 않습니다. |

Quick Item Grid가 없는 화면에서도 메뉴는 사용할 수 있어야 합니다. 반면 창을 여는 버튼은 유효한 서비스와 페이지가 준비되기 전에는 실행할 수 없어야 합니다.

## 2. 기본 GameMenuPanel 프리팹을 완성합니다

권장 계층은 다음과 같습니다.

```text
GameMenuPanel                  GameMenuPanel, ScrollRect, FlexibleLayoutItem
├── Viewport                   RectMask2D
│   └── Content                GameMenuLayoutGroup
│       └── ButtonRoot         Button, GameMenuButtonView
│           └── VisualRoot
│               ├── Background
│               ├── Icon
│               ├── Label
│               ├── Border
│               └── EffectOverlay
└── HorizontalScrollbar
    ├── Background
    └── SlidingArea
        └── Handle
```

- GameMenuPanel과 ScrollRect 양쪽의 Viewport·Content 참조를 동일한 내부 객체로 연결합니다.
- Scrollbar.handleRect, targetGraphic 및 모든 버튼의 View 참조를 검증합니다.
- 구형 HorizontalLayoutGroup이 새 레이아웃과 같은 RectTransform을 제어하지 않게 제거 또는 마이그레이션합니다.
- 기본 라벨의 Font와 Material을 포함한 표시 의존성을 확인합니다. 실제 표시 가능 여부를 테스트합니다.
- 내부 구조를 완성한 자산을 저장하고 다시 로드하여 검사합니다. 런타임 자동 검색으로만 누락을 숨기지 않습니다.
- 명시적인 내부 참조는 기본 자산에서 완성합니다. 구조 복구가 필요하면 Editor의 별도 복구 명령을 사용합니다.
- Runtime에 이름으로 자식을 찾거나 매 프레임 전체 계층을 검색하지 않습니다.
- 필수 내부 참조가 없으면 구성 오류를 한 번 알리고 관련 입력을 중단합니다. 정상 준비 완료로 취급하지 않습니다.

## 3. 바로 사용할 수 있는 통합 구성을 제공합니다

신규 제안 자산은 Assets/TxTRPG/UI/Prefabs/GameMenuScreen.prefab입니다. 이것은 GameMenuPanel 자체의 대체가 아니라 메뉴와 창을 묶는 선택적 화면 구성 프리팹입니다.

- GameMenuPanel 중첩 인스턴스, GameWindowService, ModalWindowHost와 기본 페이지 연결을 포함합니다.
- Canvas 아래에 배치하고 서비스 연결을 별도로 입력하지 않아도 기본 메뉴와 창을 열고 닫을 수 있어야 합니다.
- 기본 페이지는 현재 구현된 기능만 사용합니다. 플레이어 세션 등 외부 데이터가 필요한 페이지는 준비 전 비활성 상태 또는 명확한 이용 불가 상태를 표시합니다. Demo 인벤토리나 가짜 플레이어 상태를 생성하지 않습니다.
- 내부 메뉴는 화면 구성 루트와 별도로 크기를 배정할 수 있어야 합니다. 통합 프리팹을 작은 FlexibleLayoutItem 영역 안에 넣어 모달까지 그 영역에 제한하지 않습니다.
- 모달을 표시할 OverlayRoot를 명시적으로 지정합니다. 메뉴의 Viewport 및 상위 패널 마스크 밖에서 화면 전체를 덮도록 합니다.
- 기본 통합 구성에서는 메뉴 영역과 Overlay 영역을 형제 영역으로 분리하고, Canvas 아래에서의 실제 클리핑과 정렬을 검증합니다.
- 기존 화면에서는 통합 프리팹을 추가하지 않고, 기존 GameMenuPanel에 기존 공유 서비스를 연결하는 방식을 사용합니다.
- 여러 메뉴가 있는 화면은 같은 서비스를 명시적으로 공유합니다. 글로벌 검색 결과의 첫 서비스를 자동 선택하지 않습니다.

GameMenuPanel의 외부 필드가 Prefab Mode에서 비어 있는 것은 그 자체로 오류가 아닙니다. 내부 필수 누락과 외부 연결 대기 상태를 Inspector에서 구분합니다. 사용자는 기본 통합 프리팹을 통해 연결까지 완료된 구성을 선택할 수 있습니다.

## 4. 연결과 수명을 명시합니다

필요하면 Application 계층에 GameMenuCompositionBinder 같은 작은 구성 컴포넌트를 추가합니다. 이름은 제안입니다.

- Binder는 대상 메뉴, 창 서비스, 선택적 기존 Grid와 OverlayRoot를 직렬화 참조 또는 명시적 초기화 인수로 받습니다.
- UI가 PlayerSession이나 씬 전체를 스스로 찾아 조립하지 않도록 합니다.
- 준비 순서는 내부 참조 검증 → 서비스와 페이지 준비 → 메뉴 연결 → 실행 가능 상태 갱신입니다.
- Awake/OnEnable 호출 순서에만 의존하지 않습니다. 런타임 Bind/Unbind 또는 동등한 API를 제공하여 활성화 후 연결과 서비스 교체도 지원합니다.
- 준비 전 입력을 차단하고, 준비 실패 시 비활성 이유를 제공합니다. 외부 의존성의 누락을 NullReferenceException으로 처리하지 않습니다.
- Inspector에 직접 연결한 유효한 서비스를 Binder가 몰래 덮어쓰지 않습니다. 연결 방식과 우선순위를 하나의 명시적 정책으로 정합니다.
- 메뉴 비활성화·제거는 메뉴 소유 리스너만 해제합니다. 공유 서비스와 다른 화면의 자산을 제거하지 않습니다.
- 통합 구성은 자신이 만든 서비스·모달만 정리합니다. OverlayRoot로 이동시킨 객체가 있다면 별도로 소유권을 기록하고 함께 정리합니다.
- 같은 페이지 ID, 여러 모달 호스트, 중복 Binder와 중복 버튼 이벤트를 검증합니다.
- Quick Item Grid가 없으면 컨텍스트 메뉴 닫기만 생략합니다. 다른 필수 준비 조건까지 무시하지 않습니다.

## 5. 확장 경계를 유지합니다

- 명령 실행, 버튼 표시, 레이아웃, 창 호스팅, 데이터 연결을 분리합니다.
- 현재 GameWindowService와 pageId 라우팅을 재사용합니다. 대체 서비스가 실제로 필요한 경계만 인터페이스나 어댑터로 분리합니다.
- 향후 여러 컨텍스트 메뉴를 닫아야 한다면 UI 상호작용 조정자를 도입할 수 있습니다. 지금은 선택적 Grid 연결을 위해 거대한 이벤트 버스를 만들지 않습니다.
- 기본 버튼과 모달을 중첩 프리팹 또는 Variant로 교체할 수 있게 하되, 이름이 아니라 컴포넌트 참조와 안정적인 페이지 ID를 사용합니다.
- 배경·테두리·아이콘·텍스트 효과는 VisualRoot 아래에 적용합니다. 배치 및 클릭 영역의 RectTransform은 연출과 분리합니다.
- Style 원본과 공유 Material은 런타임에 직접 수정하지 않습니다. 생성한 Material, 구독, 비동기 작업의 정리 책임을 명시합니다.
- 나중에 Addressables를 사용하더라도 외부 서비스의 소유권과 자산 로드의 소유권을 혼합하지 않습니다. 이번 작업에서 모든 자산을 Addressables로 전환하지 않습니다.

## 6. 생성기와 기존 자산을 안전하게 수정합니다

- QuickItemsUiProjectBuilder의 메뉴 생성과 통합 구성 생성을 분리합니다.
- 기본 메뉴 저장 시 임시 루트의 형제나 Scene 객체 참조를 넣지 않습니다. 통합 프리팹은 공통 루트 전체를 저장합니다.
- Prefab 전용 생성 작업이 아이템 콘텐츠를 변경하지 않도록 분리합니다. 현재 RebuildPrefabsOnly도 CreateOrUpdateContent를 호출하므로 이름만 믿고 실행하지 않습니다.
- 현재 ConfigureScene은 기존 메뉴와 생성 루트를 삭제합니다. 이번 작업에는 이를 그대로 실행하지 말고 선택 인스턴스에 대한 비파괴적 마이그레이션을 제공합니다.
- 기존 GUID, Button 이벤트, pageId, Visible, 외형, 레이아웃 설정, Scene의 위치·크기·부모·형제 순서를 보존합니다.
- 마이그레이션은 Undo를 지원하고, 미저장 변경을 덮어쓰지 않으며, 반복 실행해도 객체와 서비스가 늘어나지 않아야 합니다.
- 새 GameMenuScreen.prefab은 신규 화면용입니다. TMP_MainScene에는 기존 메뉴와 창 서비스를 유지한 채 필요한 내부 복구와 연결만 적용합니다.
- 구현자가 실제 Editor 상태를 확인할 수 없다면 자산 수정과 테스트를 완료했다고 보고하지 않습니다.

## 7. 검증 기준

### Edit Mode

- 저장된 기본 프리팹을 다시 로드하여 필수 내부 객체와 모든 내부 참조가 유효한지 검사합니다.
- 기본 메뉴에는 외부 Scene 객체를 가리키는 참조가 없어야 합니다.
- 통합 프리팹의 메뉴와 서비스 및 페이지 연결이 저장 후에도 유효해야 합니다.
- 기존 GameMenuLayoutTests를 실행하고, 기본 자산 무결성·마이그레이션 반복 실행 테스트를 추가합니다.
- 테스트를 위한 메모리 객체 조립만 통과하고 실제 저장된 프리팹은 비어 있는 상태를 허용하지 않습니다.

### Play Mode

- Canvas와 적절한 Input System EventSystem이 있는 빈 화면에서 통합 프리팹을 배치하여 기본 창 열기·닫기를 검증합니다.
- 기본 메뉴만 배치하면 UI는 정상 표시되고, 외부 서비스가 없다는 상태가 명확히 드러나며 버튼 실행은 차단되어야 합니다.
- 기존 서비스 연결 후 메뉴가 재생성 없이 실행 가능해지는지 확인합니다.
- Grid가 없는 경우, 기존 Grid가 있는 경우, 두 메뉴가 한 서비스를 공유하는 경우를 검사합니다.
- 재활성화와 재연결 후 클릭 한 번에 창 요청이 한 번만 발생하는지 검사합니다.
- 선택 포커스 복원, 모달 입력 차단, 메뉴 마스크 밖 모달 표시를 확인합니다.
- 수평 스크롤·Wrap, 좁은 모바일 화면·넓은 데스크톱 화면, 키보드·마우스·터치·게임패드를 검증합니다.
- 씬 언로드와 서비스 교체 후 고아 모달, 이벤트 구독, 비동기 작업이 남지 않는지 확인합니다.
- TMP_MainScene의 기존 배치와 다른 UI가 변하지 않았는지 전후 비교합니다.

## 문서 갱신과 최종 보고

DOCS/architecture/quick-items-and-game-windows.md에 기본 메뉴와 통합 프리팹의 차이, 외부 연결, 준비 및 소유권을 기록합니다. DOCS/development/workflows.md에는 검증된 생성·복구 메뉴와 사용 절차를 기록합니다. 주요 경계나 디렉터리를 추가했다면 DOCS/architecture/project-structure.md도 갱신합니다.

최종 보고에는 수정 경로, 기본 포함 요소, 선택적 외부 요소, 통합 프리팹 사용법, 기존 Scene 마이그레이션 결과, 실제 수행한 테스트와 미검증 사항을 포함합니다.

