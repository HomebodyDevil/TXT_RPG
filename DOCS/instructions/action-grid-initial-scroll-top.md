# ActionGridPanel 최초 스크롤 위치 상단 보장 지침

## 목적과 범위

Editor에서 Play를 시작했을 때 ActionGridPanel의 콘텐츠가 Viewport보다 길면 첫 번째 행부터 표시하도록 수정합니다. 초기 콘텐츠와 레이아웃이 준비된 시점에 위치를 확정하며, 이후 사용자의 스크롤을 반복 초기화하지 않습니다.

이 문서는 구현 계획입니다. 수정 대상은 초기 스크롤 수명, 필요한 로더 연결, 관련 생성 자산과 테스트입니다. 아이템 데이터, 슬롯 순서, 그리드 크기 정책, 스크롤바 외형 및 다른 패널의 스크롤 정책은 변경하지 않습니다.

## 사전 확인 및 현재 근거

AGENTS.md, DOCS/architecture/action-grid-panel.md, DOCS/architecture/panel-startup.md, DOCS/development/workflows.md를 읽습니다.

| 경로 | 조사 및 수정 대상 |
| --- | --- |
| Assets/TxTRPG/UI/Runtime/ActionGridPanel.cs | Awake, OnEnable, ApplyLayout, ResetScrollToTop, 초기 셀 및 선택 갱신을 추적합니다. |
| Assets/TxTRPG/UI/Runtime/ConfigurableScrollbarController.cs | Viewport 변경 및 ScrollRect와 핸들의 양방향 동기화를 확인합니다. |
| Assets/TxTRPG/UI/Runtime/ActionGridPanelDemoController.cs | 초기 Demo 데이터 적용 시점을 확인합니다. |
| Assets/TxTRPG/UI/Runtime/PanelStartupController.cs | 콘텐츠 준비, 레이아웃 확정, 표시와 입력 허용 순서를 확인합니다. |
| Assets/TxTRPG/Application/Runtime/Items/QuickItemGridPresenter.cs | 운영 초기 슬롯 및 아이콘 갱신과 재바인딩을 확인합니다. |
| Assets/TxTRPG/UI/Editor/ActionGridPrefabBuilder.cs | 기본 Content 좌표와 스크롤 설정 및 생성 자산을 확인합니다. |
| Assets/TxTRPG/UI/Prefabs/ActionGridPanel.prefab | 실제 저장된 초기 참조·좌표를 확인합니다. |
| Assets/TxTRPG/UI/Tests/Editor/ActionGridPanelTests.cs | 기존 회귀 테스트에 초기 위치 검증을 추가합니다. |
| Assets/Scenes/TMP_MainScene.unity | 사용자 배치와 Override를 유지하며 재현합니다. |

현재 소스에서 ResetScrollToTop은 관성을 정지하고 verticalNormalizedPosition을 1로 설정하며 Content의 anchoredPosition.y를 0으로 조정합니다. ApplyLayout에서 확인한 호출 조건은 wasOverflowing && contentFits입니다. 따라서 최초 overflow 상태의 상단 표시를 보장하는 조건과는 다릅니다.

중간 위치의 실제 원인은 아직 Play Mode로 확정하지 않았습니다. 초기화 누락, 저장된 Content 위치, 뒤늦은 부모 레이아웃, 스크롤바 역동기화, 선택 복원 등을 구분해야 합니다. 위 코드만으로 단일 원인을 확정하지 않습니다.

## 요구 동작

- 최초 준비 완료 후 overflow 상태이면 첫 행이 Viewport 상단 패딩 바로 아래에 표시되어야 합니다.
- 핸들이 Hidden이어도 같은 초기 위치를 사용합니다.
- 첫 화면에 중간 행을 보여준 뒤 뒤늦게 상단으로 점프하는 현상을 남기지 않습니다.
- 콘텐츠가 모두 들어오는 경우 기존 Top 또는 CenterWhenContentFits 정책을 그대로 유지합니다.
- 빈 슬롯도 표시 콘텐츠입니다. FillCapacityWithEmptySlots에서는 실제 Entry가 없어도 Capacity에 따른 첫 행을 기준으로 합니다.
- 준비 완료 이후 드래그·휠·터치·키보드·게임패드 탐색은 기존대로 동작합니다.
- 일반 항목 갱신, 수량·아이콘 변경, 화면 회전, 부모 크기 변경마다 상단으로 강제 이동하지 않습니다.
- overflow에서 fitting으로 바뀔 때의 기존 관성 정지와 중앙 배치 동작을 보존합니다.

## 구현 원칙

### 1. 원인을 재현하고 쓰기 순서를 확인합니다

초기화 각 단계에서 표시 셀 수, Viewport·Content 크기, Content 좌표, 정규화 위치, 관성, 선택 셀과 스크롤바 값을 확인합니다. 필요한 진단은 Editor/Development 용도로 제한하고 매 프레임 로그를 남기지 않습니다.

Scene에 저장된 오프셋을 변경하는 것만으로 해결하지 않습니다. 재생성된 Prefab, Demo, 런타임 생성, 지연된 데이터 적용에서도 재현되지 않아야 합니다.

### 2. 최초 준비에 한정된 위치 초기화를 추가합니다

- 패널 인스턴스의 최초 준비에 pending 상태 또는 동등한 명시적 상태를 둡니다.
- 셀 구성 → 실제 Viewport 및 Content 크기 계산 → 필요한 레이아웃 반영 → 상단 위치 확정 → 핸들 동기화 → 준비 완료 순서를 사용합니다.
- 실제 크기가 0이거나 부모 배치가 끝나지 않았다면 완료 처리하지 않습니다.
- Awake 또는 Start에서 위치를 한 번 지정하는 것만으로 모든 로드 경로가 해결된다고 가정하지 않습니다.
- 임의의 0.1초 지연, 여러 프레임 무조건 리셋, 매 프레임 위치 고정으로 해결하지 않습니다.
- 위치 변경 책임은 ActionGridPanel에 둡니다. 로더는 최초 준비의 시작·완료를 알릴 수 있지만 별도 스크롤 좌표 코드를 복제하지 않습니다.
- 기존 초기화 계약으로 충분하면 재사용합니다. 없는 경우에만 초기 콘텐츠 적용 완료를 전달하는 작은 API를 추가합니다.

### 3. 지연 로드와 빈 데이터의 완료를 구분합니다

빈 목록도 유효한 초기 결과입니다. 첫 비어 있지 않은 데이터가 올 때까지 무기한 기다리지 않습니다.

- 초기 로더가 없는 패널은 초기 Capacity와 셀 배치 확정 시 최초 준비를 완료합니다.
- 초기 로더가 있는 패널은 최초 로드의 완료를 명시적으로 기다립니다. 그 이후의 아이콘 완료나 목록 갱신은 최초 로드로 다시 취급하지 않습니다.
- 초기 로드 실패 시 기존 실패 정책을 유지하고, 무한 대기나 계속되는 리셋을 발생시키지 않습니다.
- 완료 전에 비활성화·파괴되면 예약 작업을 취소합니다. 다시 활성화되면 미완료 준비만 이어서 처리하고, 이미 준비된 패널의 사용자 위치는 유지합니다.
- 새 인스턴스는 다시 상단에서 시작합니다. 새로운 데이터 화면에 대한 명시적 초기화 API가 필요하면 일반 Refresh와 분리합니다.
- 늦게 끝난 이전 로드가 새 데이터의 스크롤 위치를 변경하지 못하도록 취소 또는 세대 식별을 적용합니다.

### 4. 기존 배치와 입력을 보존합니다

- 위치 확정 전에 StopMovement로 관성을 제거합니다.
- 현재 상단 Anchor/Pivot 계약을 확인하고 기존 ResetScrollToTop을 재사용 또는 보완합니다. 세로 위치 수정이 수평 좌표·패딩·Anchor를 바꾸지 않게 합니다.
- Content와 ScrollRect의 실제 위치를 기준으로 핸들을 동기화합니다. 핸들의 값만 바꾸어 화면 위치가 맞을 것이라고 가정하지 않습니다.
- 동기화가 이벤트를 재호출하여 중간 값으로 돌아가지 않게 합니다.
- 최초 자동 선택이 중간 셀을 다시 보이게 하는지 검사합니다. 최초 선택은 첫 번째 탐색 가능한 셀을 우선하되 게임 동작을 실행하지 않습니다.
- 준비 후 사용자가 이동한 선택과 명시적인 포커스 복원은 그대로 지원합니다.
- 공용 ConfigurableScrollbarController에 모든 패널의 OnEnable 위치를 초기화하는 코드를 넣지 않습니다. StoryTextPanel의 최신 텍스트 하단 표시를 훼손하면 안 됩니다.
- 기존 표시 준비 계약을 사용하는 경우 상단 확정도 준비 완료 조건에 포함합니다. 이 문제를 가리기 위한 새 Fade 효과는 추가하지 않습니다.

## 자산 및 호환성

운영 Prefab과 관련 Demo의 기본 좌표를 검증하되, TMP_MainScene의 배치·크기·슬롯·스크롤바 설정은 보존합니다. 전체 Scene Rebuild나 전체 Prefab Rebuild를 이 수정의 필수 단계로 만들지 않습니다.

생성기 변경이 필요하면 해당 출력만 수정하고, 현재 프로젝트의 생성기 등록·검증 규칙을 준수합니다. GUID와 사용자 Override를 보존합니다. 기존 직렬화 필드 이름과 enum 값을 불필요하게 변경하지 않습니다.

초기 위치 옵션은 이번 요구에 필수가 아닙니다. 이미 복원 옵션이 있다면 기본 상단 시작과 명시적 복원의 우선순위를 문서화하고, 없는 경우 범용 스크롤 저장 시스템을 새로 만들지 않습니다.

## 검증 기준

Edit Mode 계산 검증과 실제 Play Mode 검증을 함께 수행합니다.

1. 중간 또는 하단 Content 위치가 저장된 패널을 실행해 최초 overflow 화면이 상단인지 확인합니다.
2. 많은 Entry와 빈 슬롯 Capacity 각각으로 검사합니다. 첫 행의 실제 Rect가 상단 패딩 아래에 있는지 확인합니다.
3. 정규화 값뿐 아니라 첫 행의 화면 좌표, 핸들 위치와 관성을 검사합니다. 첫 행을 모두 표시할 수 없는 극소 Viewport는 상단 정렬 여부로 판정합니다.
4. 초기부터 overflow, fitting, 빈 목록, 지연 후 overflow, 지연 후 빈 결과를 검사합니다.
5. Hidden/Auto/Always 및 대칭 여백 설정에서 동일한 초기 위치를 검증합니다.
6. 부모 레이아웃이 뒤늦게 크기를 배정하는 경우와 런타임 Instantiate를 검사합니다.
7. 준비 직후 여러 프레임 동안 상단 위치가 다시 중간으로 바뀌지 않는지 확인합니다.
8. 사용자가 중간으로 스크롤한 후 수량·아이콘 갱신과 일반 Refresh가 위치를 초기화하지 않는지 확인합니다.
9. 비활성화 후 재활성화, 초기 로드 취소·실패·재시도, 데이터 요청 교체를 검사합니다.
10. 콘텐츠 축소로 fitting이 되면 기존 세로 중앙 배치가 유지되는지 확인합니다.
11. 키보드·게임패드 선택 이동과 컨텍스트 메뉴 종료 후 포커스 복원을 회귀 검증합니다.
12. StoryTextPanel 등 공용 스크롤바 사용처에 회귀가 없는지 확인합니다.
13. TMP_MainScene과 Demo에서 Play 직후 화면을 확인하고 실제 테스트한 해상도·입력 경로를 기록합니다.

## 문서 갱신 및 최종 보고

DOCS/architecture/action-grid-panel.md에 최초 준비와 이후 갱신의 구분, 위치 초기화 조건과 수명을 기록합니다. 필요한 경우 DOCS/architecture/panel-startup.md와 DOCS/development/workflows.md에 초기화 계약 및 검증 절차를 갱신합니다.

최종 보고에는 재현으로 확인한 원인, 수정한 초기화 경로, 사용자 스크롤 보존 방식, 변경 자산, 실제 수행한 테스트와 미검증 항목을 포함합니다. 컴파일 성공을 Play Mode 표시 검증으로 보고하지 않습니다.

