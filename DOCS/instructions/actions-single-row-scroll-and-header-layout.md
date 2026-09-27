# Actions 한 줄 가로 탐색·조건부 정렬·Header 여백 수정

## 1. 목적과 확정 사항

Actions 슬롯의 정렬과 스크롤 시작 위치를 수정하고, Header를 숨겨도 본문 위아래 여백이 올바르게 계산되도록 한다. 기존 Grid 모드는 유지하면서 개발자가 한 줄 가로 배치와 정렬 정책을 선택할 수 있게 한다. 이 문서는 후속 구현 지침이며 적용·실행 검증 완료 기록이 아니다.

사용자 요청과 추가 답변으로 확정한 운영 Actions의 기본값은 다음과 같다.

- 한 줄(1 row)에 모든 표시 슬롯을 배치한다. 슬롯이 영역을 넘으면 줄바꿈 대신 가로 스크롤로 탐색한다.
- Scrollbar는 기본 숨김이다. 숨김 상태에서도 가로 스크롤 입력과 선택 슬롯 자동 노출은 동작한다. 숨긴 스크롤바의 공간은 남기지 않는다.
- 최초 준비가 완료된 overflow 상태에서는 콘텐츠의 가운데가 아니라 **가장 왼쪽**에서 시작한다.
- 한 줄이 영역 안에 들어가고 슬롯 수가 **4개 이하**이면 슬롯 묶음을 일정한 간격으로 중앙에 배치한다.
- 한 줄이 영역 안에 들어가고 슬롯 수가 **5개 이상**이면 첫 슬롯의 왼쪽 변과 마지막 슬롯의 오른쪽 변을 **좌우 Padding을 제외한 안쪽 경계**에 맞추고 사이 슬롯을 균등 간격으로 배치한다.
- 중앙에 모아 배치할 최대 슬롯 수는 개발자가 설정할 수 있어야 하며 기본값은 4이다. overflow 판정은 이 개수 규칙보다 우선한다.
- Actions의 Header는 기본 비활성이고, 남는 본문 영역에서 세로 중앙 정렬을 기본으로 한다. Header 표시와 가로·세로 정렬은 개발자가 설정할 수 있게 한다.
- 앞서 확정한 적용 범위대로 운영 기본값 변경은 메인 Actions에 한정한다. Bag과 다른 Grid의 기존 기본 표시·Header·스크롤 정책은 유지한다.

선행 `DOCS/instructions/actions-grid-spacing-and-display-presets.md`의 균형형·줄바꿈 기본값은 운영 Actions에 한해 이번 요구로 대체한다. 기존 프리셋과 수동 모드는 삭제하지 않는다. 기존 DistributedSpacing의 간격 상한 방식과 이번 양 끝 배치는 서로 다른 정책이다.

## 2. 선행 조사와 확인한 현재 구조

`AGENTS.md`, `DOCS/README.md`, `DOCS/architecture/action-grid-panel.md`, `DOCS/architecture/quick-items-and-game-windows.md`, `DOCS/development/workflows.md`를 읽는다. 현재 작업 트리에는 선행 기능의 수정·미추적 파일이 있으므로 Git 상태와 diff를 먼저 확인하여 보존한다. `.codegraph/`가 있으면 호출·영향 분석에 먼저 사용하고 중요 결과는 실제 소스·직렬화 자산과 대조한다. 임의로 재색인하지 않는다.

작성 시 확인한 소스 사실은 다음과 같다.

1. `ActionGridDisplaySettings.cs`에 Manual·Balanced·DistributedSpacing·LargeSlots와 `ActionGridLayoutCalculator`가 있다. 이전 지침서에 없는 기능이 실제로 추가되어 있으므로 새 구현을 중복 작성하지 않는다.
2. 프리셋에는 별도의 크기·간격·정렬 설정이 있고 `ActionGridPanel`이 활성 프리셋을 우선 적용한다. 수동 Layout Mode만 변경해도 프리셋 계산이 계속 적용되는지 조사해야 한다.
3. 현재 초기 스크롤 보정 `TryResolveInitialScroll`은 높이 overflow와 `ResetScrollToTop`을 기준으로 한다. 가로 시작 위치를 보장하는 계약은 아직 없다.
4. 기존 생성기는 Header 높이 58, Scroll View의 좌·우·아래 여백 18, 위쪽 여백 66을 별도로 설정한다. Header 비활성 시 위쪽 고정 여백이 자동 제거된다고 가정할 수 없다.
5. `ConfigurableScrollbarController`는 verticalNormalizedPosition과 좌우 Viewport 공간 예약을 사용하는 세로 스크롤바 구조이다. 가로 모드에서 이를 그대로 연결하거나 전역 수정하면 다른 패널에 영향을 줄 수 있다.
6. 실제 운영 화면에서 정렬 오류와 여백 문제의 최종 원인은 아직 재현·확정하지 않았다. Anchor만 원인이라고 단정하지 않는다. Content 폭, Pivot, Grid childAlignment, 실제 슬롯 수, Header 예약 높이와 스크롤바 공간을 함께 확인한다.

### 영향 경로

| 경로 | 책임·확인할 영향 |
| --- | --- |
| `Assets/TxTRPG/UI/Runtime/ActionGridPanel.cs` | 모드 선택, Content 크기, 초기 스크롤·선택 노출·Navigation과 수명이다. |
| `Assets/TxTRPG/UI/Runtime/ActionGridDisplaySettings.cs` | 기존 프리셋·정규화·공통 계산 결과를 확장한다. |
| `Assets/TxTRPG/UI/Runtime/ActionGridModels.cs` | 기존 Layout Mode와 정렬 enum의 호환성을 보존한다. |
| `Assets/TxTRPG/UI/Runtime/ActionGridLayoutGroup.cs` | 실제 셀 위치, 기존 마지막 행 정렬과 새 한 줄 정책의 관계이다. |
| `Assets/TxTRPG/UI/Runtime/ConfigurableScrollbarController.cs` | 세로 전용 처리와 숨김·공간 예약을 조사한다. 공용 동작을 무조건 바꾸지 않는다. |
| `Assets/TxTRPG/UI/Editor/ActionGridPanelEditor.cs` | 개발자 옵션, 적용 우선순위와 계산 결과 진단을 표시한다. |
| `Assets/TxTRPG/UI/Editor/ActionGridPrefabBuilder.cs` | 생성 시 Header·Scroll View·Content·Scrollbar의 유효한 연결을 유지한다. |
| `Assets/TxTRPG/UI/Prefabs/ActionGridPanel.prefab` | 공용 자산의 필수 참조와 기존 기본값 호환성을 유지한다. |
| `Assets/TxTRPG/UI/Prefabs/ActionGridCell.prefab` | 슬롯 그림·수량·클릭 영역을 보존한다. |
| `Assets/TxTRPG/Application/Runtime/Items/QuickItemGridPresenter.cs` | 표시 슬롯 수와 안정적인 퀵 슬롯 번호·실행 계약을 유지한다. |
| `Assets/TxTRPG/Application/Runtime/Items/PlayerGameWindowPages.cs` | Bag의 외부 설정 적용·세로 및 페이지 표시를 회귀 검증한다. |
| `Assets/TxTRPG/Application/Runtime/Items/InventoryWindowModels.cs` | Bag의 설정 원본과 기존 직렬화 기본값을 보존한다. |
| `Assets/Scenes/AppScene.unity`, `Assets/Scenes/TMP_MainScene.unity` | 정식 시작 경로와 운영 Actions에만 새 기본값을 적용한다. |
| `Assets/TxTRPG/UI/Tests/Editor/ActionGridDisplayTests.cs` | 신규 배치 계산과 기존 프리셋 회귀를 검증한다. |
| `Assets/TxTRPG/UI/Tests/Editor/ActionGridPanelTests.cs` | 위치·높이·포커스·모드 전환과 Header 여백을 검증한다. |
| `Assets/TxTRPG/UI/Tests/PlayMode/ActionGridInitialScrollPlayModeTests.cs` | 최초 가로 시작 위치와 기존 세로 초기 스크롤을 검증한다. |

## 3. 개발자 설정과 우선순위

기존 FixedColumns·AdaptiveCellSize·ExactColumns를 유지하고 한 줄 가로 배치 선택을 추가한다. 신규 enum 이름은 제안상 SingleRowHorizontal이며 실제 관례에 맞춰 정한다. 기존 enum 값의 순서·숫자와 ConfigureLayout의 의미는 바꾸지 않는다.

배치 축과 표시 프리셋의 책임을 명확히 한다. 한 줄 모드를 선택했는데 Balanced가 내부적으로 FixedColumns를 강제하여 다시 여러 행이 되는 상태는 금지한다. 한 줄에서는 열 수를 제한하거나 줄바꿈하지 않으며 프리셋은 셀 크기·기본 간격·Padding을 공급한다. 적용되지 않는 최대 열·마지막 행 설정은 Inspector에서 비활성 표시하고 이유를 설명한다. 기존 다중 행은 종전의 프리셋 계산을 유지한다.

개발자가 조절할 항목은 다음과 같다.

- 한 줄 가로 배치 또는 기존 Grid 모드이다.
- 한 줄의 목표 셀 크기, 기본 가로 간격, 상하좌우 Padding이다. 목표 크기는 기존 설정과 조합하여 한 번 결정하며 슬롯 수에 따라 모두 들어가도록 무조건 축소하지 않는다.
- fitting 상태의 배치 정책은 조건부 중앙/양 끝, 일정 간격 묶음 정렬, 항상 양 끝 배치를 제공한다. 일정 간격 묶음 정렬에는 Left·Center·Right를 사용한다.
- 조건부 정책의 중앙 배치 최대 개수는 기본 4, 0 이상의 정수이다. 0이면 두 슬롯 이상에서 항상 양 끝 정책을 사용한다. 슬롯 하나는 양 끝을 동시에 만족할 수 없으므로 중앙에 놓는다.
- 세로 정렬은 Top·Center·Bottom을 제공한다. 기존 Top 및 CenterWhenContentFits의 값·동작은 보존하고 필요 시 새 한 줄 전용 설정이나 호환 가능한 확장을 사용한다.
- Header 표시와 Header가 차지하는 높이·본문과의 간격을 제어한다. 기본 Actions에서는 Header가 꺼져야 한다.
- Scrollbar 기본은 Hidden이며 스크롤 활성 여부와 분리한다. 기존 개발자 표시 옵션을 보존하고 가로 표시를 선택할 경우 올바른 축으로 동작하게 한다. 세로 전용 제어기를 억지로 재사용하지 않는다.

계산 결과에는 실제 표시 슬롯 수, fitting/overflow, Content 폭, 가로 간격, 정렬 정책, Header 예약 높이와 현재 스크롤 위치를 진단 가능하게 표시한다. 기존 수동값·프리셋 편집값은 모드 전환으로 삭제하지 않는다. Bag에서 새 모드를 명시적으로 선택할 때에는 페이지 소유 설정 경로를 통해 전달하여 Refresh가 덮어쓰지 않게 하되 기존 Bag 자산은 변경하지 않는다.

## 4. 한 줄 계산 계약

N은 실제 표시 대상으로 바인딩된 슬롯 수이며 빈 슬롯을 포함한다. 비활성 풀 셀은 제외한다. 슬롯 순서와 ID를 바꾸지 않는다. W는 Viewport 폭에서 좌우 Padding을 제외한 내부 폭, S는 최종 셀 너비, G는 기본 가로 간격, K는 중앙 배치 최대 개수라고 정의한다.

```text
RequiredInnerWidth = N × S + max(0, N - 1) × G
fitting = RequiredInnerWidth <= W + 허용오차
```

크기·간격은 Canvas UI 단위이다. 폭과 높이가 유효하지 않은 초기 상태에서는 기존 초기 준비 절차가 끝날 때까지 최종 위치 확정을 보류한다. 간격을 늘린 후 overflow를 다시 판단하는 순환 계산을 만들지 않는다.

| 조건 | 배치·탐색 |
| --- | --- |
| N=0 | 셀을 배치하지 않고 잘못된 나눗셈·음수 폭 없이 기존 빈 상태 정책을 따른다. |
| overflow | 간격 G로 왼쪽부터 한 줄 배치한다. Content를 필요한 폭으로 확장하고 가로 탐색을 허용한다. |
| fitting, N=1 | 기본 조건부 정책에서는 중앙 배치한다. |
| fitting, 2≤N≤K | 간격 G를 유지한 슬롯 묶음을 중앙에 배치한다. |
| fitting, N>K, N≥2 | 아래 양 끝 배치 공식을 적용한다. |

조건부 정책의 임계값은 화면 해상도가 아니라 표시 슬롯 개수이다. 예를 들어 K=4인 상태에서 4개라도 폭을 넘으면 왼쪽 시작 가로 스크롤이며, 5개가 들어가면 양 끝 배치이다.

양 끝 배치는 다음 식을 사용한다.

```text
DistributedGap = (W - N × S) / (N - 1)
FirstLeft = PaddingLeft
SlotLeft(i) = FirstLeft + i × (S + DistributedGap)
```

첫 셀의 왼쪽 변과 마지막 셀의 오른쪽 변이 내부 경계와 일치해야 한다. 셀 중심을 경계에 놓아 절반이 잘리는 구현은 금지한다. fitting 판정을 기본 간격으로 했으므로 DistributedGap은 허용오차 범위에서 G 이상이다. 이전 DistributedSpacing의 최대 간격 상한을 재사용하여 마지막 슬롯이 오른쪽 끝에 도달하지 못하게 하지 않는다. 상한이 필요한 화면은 기존 다른 정책을 선택할 수 있다.

fitting이면 Content 폭은 Viewport 폭에 맞추고 불필요한 가로 이동과 관성을 해제한다. overflow이면 Content 폭은 Padding을 포함한 실제 필요 폭 이상으로 확보한다. 모든 슬롯을 한 행에 두며 세로 스크롤은 끈다. 높이가 부족한 경우에는 부족 공간을 진단하고 지원 해상도에서 셀·부모 크기를 조정한다. 몰래 두 행이나 세로 스크롤로 바꾸지 않는다.

세로 배치는 Header와 본문 외곽 여백을 반영한 유효 Viewport 안에서 Grid의 Top·Bottom Padding을 제외한 영역을 기준으로 계산한다. Center에서 대칭 Padding이면 슬롯 위아래 여유가 같아야 한다. 비대칭 Padding은 개발자가 지정한 값대로 반영한다.

## 5. 스크롤 시작·입력·Anchor 계약

Anchor와 Pivot은 스크롤 좌표계를 만드는 요소지만, 최초 위치는 Content 크기 확정과 ScrollRect 위치·관성 초기화까지 포함한다. 한 줄 모드에서 왼쪽 기준 Content 폭을 명시적으로 소유하고 ScrollRect의 가로 좌표계와 맞춘다. 가운데 Pivot 상태에서 크기만 늘려 첫 화면이 중간에서 시작하는 문제를 방지한다.

- 최초 데이터·레이아웃 준비 완료 후 overflow이면 관성을 정지하고 horizontalNormalizedPosition=0에 해당하는 왼쪽 끝을 보장한다. 첫 슬롯과 왼쪽 Padding이 보이는지 실제 화면 좌표로 검증한다.
- 비동기 아이콘 로드·수량 갱신·매 Canvas 렌더마다 왼쪽으로 강제 복귀시키지 않는다. 최초 초기화와 사용자가 요청한 명시적 초기화만 시작 위치를 적용한다.
- fitting에서 overflow로 바뀌면 기본 왼쪽에서 시작한다. overflow를 유지하는 단순 resize·Header 전환·수량 갱신에서는 현재 보이는 슬롯 또는 논리적 스크롤 위치를 가능한 한 유지하고 유효 범위로 제한한다. overflow에서 fitting으로 바뀌면 관성을 정지하고 새 정렬을 적용한다.
- 키보드·컨트롤러 이동으로 화면 밖 슬롯을 선택하면 가로 방향으로 해당 셀을 완전히 노출한다. 최초 자동 포커스가 마지막 셀을 선택하여 왼쪽 시작을 덮어쓰지 않게 한다. 사용자의 명시적인 탐색은 초기 위치보다 우선한다.
- 터치·마우스 드래그, 트랙패드의 가로 스크롤과 키보드·컨트롤러 Navigate를 지원한다. 일반 세로 휠을 한 줄 Actions 위에서 가로 탐색에 매핑하되 다른 패널의 스크롤을 전역으로 바꾸지 않는다.
- 드래그로 슬롯을 탐색할 때 아이템 사용이 실행되지 않게 기존 클릭 취소·임계값을 검증한다. 슬롯 인덱스와 퀵 슬롯 등록은 그대로 유지한다.
- Scrollbar 숨김은 GameObject만 잠깐 껐다가 제어기가 다시 켜는 방식으로 구현하지 않는다. 정책과 바인딩을 일치시키고 숨김일 때 scrollbar 폭·gap·OppositeScrollbarArea 예약을 제거한다. 스크롤을 막거나 클릭 영역을 좁히지 않는다.
- 기존 Grid 모드로 돌아갈 때 ScrollRect 축, Content Anchor·Pivot·폭·높이 소유권, 정렬·Scrollbar 연결을 복원한다. 모드 간 잔여 offset·velocity가 남지 않아야 한다.

## 6. Header 비활성 및 본문 여백

운영 Actions의 Header 전체를 기본 비활성으로 저장한다. 제목만 투명하게 만들거나 Title 문자열만 비우지 않는다. Header 안에 CategoryTabs가 있으므로 다른 Grid의 필요한 탭을 함께 숨기지 않도록 대상과 참조를 명시한다.

Header 영역 예약은 유효한 표시 상태에서만 발생한다. Header가 꺼졌으면 예약 높이와 Header-본문 간격은 0이어야 한다. 기본 본문 외곽 여백과 Grid Padding은 별개로 유지하며 같은 여백을 두 번 적용하지 않는다. 기존 Scroll View의 고정 위쪽 66을 그대로 두고 슬롯만 보정하는 방식은 금지한다.

개발자 설정 API와 Inspector 갱신을 제공하고, 사용자가 Header GameObject를 직접 비활성화한 경우에도 본문 영역이 갱신되어야 한다. Header 참조·표시 상태·높이 변경을 최소한의 변경 알림으로 처리한다. 매 프레임 전체 Layout 강제 재구축이나 장면 오브젝트 검색을 하지 않는다. 설정과 직접 활성 상태 중 무엇이 원본인지 명확히 하여 꺼 놓은 Header를 OnEnable이 임의로 다시 켜지 않게 한다.

Header 켜짐·꺼짐, 부모 크기·Canvas Scale·safe area 변화와 모드 변경 때 Scroll View, Viewport, Content를 일관된 순서로 갱신한다. 본문 여백·Header 높이·스크롤바 예약을 여러 컴포넌트가 같은 offset에 경쟁해서 쓰지 않도록 책임을 정한다. 상위 FlexibleLayout이 배정한 영역이나 이웃 메뉴 위치를 임의로 변경하지 않는다.

## 7. 적용·호환성·범위

- 기존 슬롯 데이터·개수·순서·PreserveSlots·명령·저장 데이터는 변경하지 않는다. 선택 프레임·수량·단축키·쿨다운·컨텍스트 메뉴와 초기 스크롤 준비 계약을 보존한다.
- 새 선택지를 기존 enum 끝에 추가하거나 호환 가능한 별도 설정으로 분리한다. 현재 enum 범위를 숫자로 검사하는 Normalize와 PropertyDrawer도 갱신한다. 신규 필드가 없는 기존 자산은 기존 Grid·Header·스크롤 정책을 유지한다.
- 운영 TMP_MainScene의 Actions에만 한 줄·조건부 정렬 K=4·세로 중앙·Header 숨김·Scrollbar 숨김을 명시적으로 저장한다. Bag과 공용 자산 전체를 새 기본값으로 일괄 덮어쓰지 않는다.
- Unity 연결·컴파일·Play Mode·미저장 변경을 확인하고 관련 Prefab·Scene만 선별 적용한다. 전체 재생성·무관한 Scene 저장·사용자 override 삭제를 하지 않는다. 생성기도 향후 필요한 참조를 잃지 않게 갱신한다.
- 저장 후 다시 로드하여 실제 Header 비활성, 스크롤 축, 필수 참조와 정책을 확인한다. 코드 작성만으로 운영 적용이 끝났다고 보고하지 않는다.
- 자동화가 불가능하면 원인·완료 작업·남은 적용과 검증을 구분한다. 필요하면 좁은 임시 적용 메뉴를 제공하되 정확한 메뉴 경로와 저장·재실행·덮어쓰기·오류 중단 조건을 문서화한다.

## 8. 검증 기준

1. K=4에서 N=0/1/2/4/5, 정확히 들어가는 수와 하나 초과한 수를 검증한다. K=0/1/6 및 잘못된 설정값도 검사한다. 4개 이하 overflow가 중앙에서 시작하지 않아야 한다.
2. fitting 중앙 묶음의 좌우 여유, 양 끝 배치의 첫 왼쪽·마지막 오른쪽 경계와 모든 중간 간격을 좌표로 검사한다. 빈 슬롯 포함 수를 사용하고 비활성 풀 셀은 제외한다.
3. Header 켜짐·꺼짐·직접 비활성화·재활성화와 Top/Center/Bottom, Left/Center/Right 옵션, 비대칭 Padding에서 실제 위치를 검증한다. 숨긴 Header와 Scrollbar의 예약 공간이 없어야 한다.
4. 최초 overflow의 왼쪽 시작을 실제 Play Mode에서 검사한다. 지연 데이터·아이콘 준비, 재개방·모드 전환, 사용자 스크롤 후 갱신과 resize에서 원치 않는 초기화를 검사한다.
5. fitting↔overflow와 가로↔기존 세로 Grid 모드를 반복 전환해 폭·높이·Anchor·Pivot·속도·Scrollbar·Navigation이 복원되는지 확인한다. 경계에서 레이아웃이 반복 왕복하지 않아야 한다.
6. 마우스·휠·트랙패드·터치·키보드·컨트롤러 탐색, 화면 밖 셀 자동 노출·컨텍스트 메뉴 위치와 포커스 복귀를 검증한다. 스크롤 드래그가 아이템 사용으로 이어지지 않아야 한다.
7. AppScene에서 TMP_MainScene으로 진입하여 데스크톱·울트라와이드·휴대폰 세로/가로·태블릿·safe area와 방향 변경을 확인한다. Before/After 이미지에 해상도·Viewport·슬롯 수·K·셀 크기·간격·Header 상태를 기록한다.
8. 기존 ActionGridDisplayTests, ActionGridPanelTests, ActionGridInitialScrollPlayModeTests를 수행하고 새 한 줄 계산·Header 회수·실제 시작 위치를 집중 검증한다. Bag의 페이지·세로 스크롤·빈 상태와 다른 스크롤바 사용 화면을 회귀 검증한다.
9. 실제 기기·빌드·입력 검증과 Editor 시뮬레이션을 구분한다. 가능한 대상의 컴파일과 직렬화 diff를 확인하고 실행하지 못한 검증은 이유와 함께 보고한다.

## 9. 문서 갱신과 최종 보고

실제 구현에 맞춰 `DOCS/architecture/action-grid-panel.md`와 `DOCS/development/workflows.md`에 새 모드·조건부 임계값·초기 왼쪽 시작·Header와 Scrollbar 공간 정책을 기록한다. Bag 설정 경로를 확장했다면 `DOCS/architecture/quick-items-and-game-windows.md`에 기존 기본값 보존을 명시한다. 주요 경계 변경이 있다면 `DOCS/architecture/project-structure.md`도 갱신한다.

최종 보고에는 실제 원인과 추정의 구분, 적용된 기본값·개발자 설정 위치, 운영 Scene 적용 결과, 화면 좌표·스크롤·입력·회귀 검증과 미검증 항목을 포함한다. 사용자가 실행할 Tools 메뉴가 남으면 별도 `사용자가 수행할 적용 절차`에 정확한 경로·순서·전제·저장·재실행·중단·최종 검증을 fenced text 블록으로 제공하고 개발 문서에도 남긴다. 수동 Tools 단계가 없으면 없다고 명시한다.
