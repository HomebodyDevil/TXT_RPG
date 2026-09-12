# ActionGridCell 반응형 아이콘 크기 개선

## 목적

TMP_MainScene의 작은 Cell에서 고정 여백 때문에 Icon이 지나치게 작아지는 문제를 해결한다. 개발자가 Inspector에서 비율 기반 표시와 기존 고정 여백 표시를 선택할 수 있게 하고, Editor와 런타임에서 동일하게 적용한다. 이번 작업은 실제 코드·Prefab·Scene 적용과 검증까지 포함한다.

## 사전 확인과 확인된 원인

`AGENTS.md`, `DOCS/README.md`, `DOCS/architecture/action-grid-panel.md`, `DOCS/development/workflows.md`를 먼저 읽는다. 구현 시점의 소스와 Prefab Override, Editor 미저장 변경을 재확인한다.

작성 시점에 확인한 저장 값은 다음과 같다. 실행 중의 실제 크기를 측정한 결과는 아니다.

- `Assets/Scenes/TMP_MainScene.unity`의 Grid Cell 크기는 약 45.037 × 45.037이다.
- `Assets/TxTRPG/UI/Prefabs/ActionGridCell.prefab`의 ContentRoot는 Cell에서 각 방향 3만큼 들어간다.
- 그 안의 Icon은 각 방향 10만큼 더 들어간다. 따라서 아이콘 영역은 약 19.037 × 19.037에 불과하다.
- Icon은 Preserve Aspect가 켜져 있다. 긴 이미지나 자체 투명 여백이 있는 이미지는 실제 그림이 더 작아질 수 있다. 현재 사용 이미지의 투명 여백을 원인으로 단정하지 않는다.
- `Assets/TxTRPG/UI/Editor/ActionGridPrefabBuilder.cs`에도 ContentRoot 3, Icon 10이라는 고정 여백 생성 코드가 있다. 저장된 Prefab만 바꾸면 재생성 시 되돌아갈 수 있다.

## 범위와 영향 경로

| 경로 | 확인·변경 책임 |
| --- | --- |
| `Assets/TxTRPG/UI/Runtime/ActionGridCell.cs` | 아이콘 바인딩과 설정 적용의 연결 지점을 확인한다. |
| `Assets/TxTRPG/UI/Runtime/ActionGridPanel.cs` | Cell 생성·재사용·크기 변경 시 설정이 유지되는지 확인한다. |
| `Assets/TxTRPG/UI/Prefabs/ActionGridCell.prefab` | 내부 참조와 신규 기본 설정을 저장한다. |
| `Assets/TxTRPG/UI/Prefabs/ActionGridPanel.prefab` | 사용하는 Cell Prefab 및 영향 범위를 확인한다. |
| `Assets/TxTRPG/UI/Editor/ActionGridPrefabBuilder.cs` | 재생성 결과도 같은 설정 계약을 따르도록 갱신한다. |
| `Assets/Scenes/TMP_MainScene.unity` | 해당 ActionGridPanel에 개선 설정을 실제 적용한다. |
| `Assets/TxTRPG/UI/Tests/Editor/ActionGridPanelTests.cs` | 기존 검증을 보존하고 관련 테스트를 추가하거나 별도 파일로 분리한다. |

새 설정 타입·컴포넌트의 이름은 구현 시 기존 관례에 맞춰 결정한다. 아래 설계 이름을 이미 존재하는 API로 취급하지 않는다. 가방 등 같은 Cell을 쓰는 다른 화면과 Variant도 조사한다. Grid 열·행 계산, 스크롤 정책, 게임 데이터, 아이템 사용 규칙, Scene 시작 대상은 이번 작업에서 변경하지 않는다.

## 설정과 계산 계약

### 두 가지 표시 방식

1. `RelativeToContent`는 ContentRoot의 실제 Rect 크기를 기준으로 아이콘 영역을 계산한다. `Icon Area Ratio`는 0~1 범위의 단일 비율로 제공하고 기본 제안값은 0.9로 한다. 이는 불투명한 그림의 점유율이 아니라 Image Rect의 점유율이다.
2. `FixedPadding`은 ContentRoot 안에서 Left/Right/Top/Bottom 여백을 적용한다. 기존 셀은 각 방향 10으로 이전 표시를 재현할 수 있어야 한다. 단위는 물리 화면 픽셀이 아닌 Canvas의 UI 단위라고 Tooltip에 명시한다.

비율 방식은 ContentRoot 너비·높이에 각각 동일한 비율을 곱한 중앙 정렬 Rect를 만든다. 고정 여백을 다시 중첩 적용하지 않는다. ContentRoot 자체의 여백은 유지하므로, Cell이 45이고 ContentRoot 여백이 각 3일 때 0.9를 적용하면 Icon 영역은 `(45 - 6) × 0.9 = 35.1`이다. Cell 전체의 90%라는 의미로 설명하지 않는다.

TMP_MainScene에는 비율 방식 0.9를 출발값으로 적용하고 실제 그림·수량·단축키의 가독성을 확인해 조정한다. 조정한 최종 값과 이유를 보고한다. Preserve Aspect는 기본적으로 유지한다. 강제 Stretch, SetNativeSize, Transform.localScale 확대를 크기 문제의 기본 해결책으로 사용하지 않는다.

### Inspector 및 유효성

- 표시 방식을 먼저 노출하고 모드별로 관련 설정만 편집 가능하게 한다. 커스텀 Inspector는 꼭 필요할 때만 추가한다. 기존 Inspector가 있으면 중복으로 만들지 않는다.
- 필드는 직렬화된 private 필드로 두고 Header, Tooltip, Range/Min 등을 필요한 만큼 사용한다. 기본값·기준 Rect·종횡비 보존 의미를 설명한다.
- 비율은 유한한 0~1 값으로 정규화하고 잘못된 NaN/Infinity에는 명시적인 기본값을 적용한다. 0은 아이콘 영역 없음이라는 의미로 정의한다.
- 고정 여백은 음수를 허용하지 않는다. 셀보다 여백 합이 큰 경우 영역이 음수가 되거나 뒤집히지 않도록 제한한다. 폭·높이가 0인 초기 레이아웃도 예외 없이 처리한다.
- 설정 변경 API는 Inspector와 동일한 검증을 거친다. OnValidate는 가벼운 검증·표시 갱신에만 사용하며 Scene 저장이나 Prefab 재생성을 실행하지 않는다.

## 책임 분리와 생명주기

- 크기 정책의 권위 있는 설정 위치는 Cell로 둔다. Panel과 Cell에 같은 정책을 중복 저장하지 않는다. 패널마다 다른 외형이 필요하면 기존 Cell Prefab 참조 또는 Variant를 활용한다. 이번 작업에서 별도 전역 Style 관리 체계를 만들지 않는다.
- 비율은 가능하면 RectTransform Anchor로 표현하여 부모 크기 변경을 자동으로 따르게 한다. 실제 크기 계산이 필요하면 변경 시점에만 수행하며 매 프레임 폴링·강제 Layout rebuild·재귀적인 Rect 갱신을 피한다.
- 아이콘 레이아웃은 Icon의 Rect만 소유한다. Grid가 관리하는 Cell 루트와 ContentRoot, 수량, 단축키, 쿨다운, 테두리, 선택 프레임을 함께 축소하거나 재배치하지 않는다.
- 필요하면 레이아웃 대상과 향후 효과 대상을 부모·자식으로 분리할 수 있으나 실제 충돌을 예방하는 최소 구조만 추가한다. Animator나 효과가 레이아웃 소유 Rect를 덮어쓰지 않도록 경계를 문서화한다. 이번에는 실제 효과를 구현하지 않는다.
- 최초 표시, 재활성화, 풀 재사용, Cell 크기 변경, 런타임 설정 변경, 비동기 아이콘 교체에서 같은 규칙이 적용되어야 한다. Sprite 교체가 개발자 크기 설정을 초기화하지 않아야 한다.
- 빈 슬롯은 기존 BindEmpty 동작을 유지하고 이전 아이콘이 남지 않아야 한다. 아이콘 변경 때문에 수량이나 클릭 영역을 바꾸지 않는다.

## 호환성과 자산 적용

기존 직렬화 데이터에 신규 설정이 없을 때 의도하지 않은 전체 화면 변경이 발생하지 않도록 한다. 예를 들어 enum의 기존 기본값을 FixedPadding으로 두고 실제 전환할 Prefab에 RelativeToContent를 명시적으로 저장하는 방식처럼 안전한 이전 경로를 선택한다. 필드 초기값만 바꾸면 모든 기존 자산이 갱신된다고 가정하지 않는다.

공용 ActionGridCell을 신규 기본 비율 방식으로 전환하려면 이를 공유하는 가방·다른 Grid도 회귀 검증한다. 기존 화면의 외형을 유지해야 한다면 FixedPadding 또는 Variant로 보존한다. TMP_MainScene만 수정하기 위해 불필요하게 모든 UI Prefab을 재생성하지 않는다.

생성기는 신규 기본 설정과 필수 참조를 저장해야 한다. 실제 자산은 필요한 부분만 수정하고 사용자 Override를 보존한다. 필드 이름을 바꾸면 직렬화 호환성을 처리한다. 자동 적용은 Unity Editor의 지원 경로를 우선 사용하며 미저장 변경을 임의로 저장하거나 폐기하지 않는다.

이미지 투명 여백은 별도 문제로 조사한다. Sprite의 원본과 Import/Sprite Rect를 확인하고, 여백이 크다면 해당 에셋을 별도 정리할 필요가 있음을 보고한다. 이 작업에서 모든 이미지 자동 Crop, 런타임 픽셀 분석, 원본 이미지 일괄 덮어쓰기를 추가하지 않는다.

## 검증 기준

### 자동 검증

- Cell 45, ContentRoot 각 3, Ratio 0.9에서 Icon Rect가 약 35.1인지 확인한다.
- FixedPadding 각 10에서 같은 조건의 Icon Rect가 약 19인지 확인한다.
- 크기 32/45/72/96/128/200 및 비정사각형 Cell에서 정책을 검증한다.
- 비율 0/1, 잘못된 값, 과도한 여백, 초기 0 크기에서 안정적인 결과를 확인한다.
- Sprite 교체·늦은 로드·재활성화·풀 재사용 후 설정과 빈 슬롯 표시가 유지되는지 확인한다.
- 레이아웃 변경이 Cell 루트, 수량, 선택 프레임과 클릭 영역을 변경하지 않는지 확인한다. 저장 후 다시 연 Prefab에도 설정과 참조가 남아 있어야 한다.

### 실제 화면 검증

- TMP_MainScene의 실제 운영 초기화 경로에서 작은 Cell의 아이콘을 전후 동일 해상도로 비교한다. 테스트를 위해 패널 폭이나 열 수를 바꿨다면 원래 값으로 복구한다.
- 정사각형·긴 이미지·투명 여백이 큰 이미지를 구분해 확인한다. 비율 보존과 여백 문제를 혼동하지 않는다.
- 수량 2자리 이상, 단축키, 쿨다운·비활성·선택 상태에서 겹침과 가독성을 확인한다.
- 마우스·터치·키보드/컨트롤러 선택 및 컨텍스트 메뉴 동작을 지원 범위에서 확인한다. 모바일 크기와 넓은 화면에서 레이아웃 변경을 확인한다.
- 공용 Cell을 쓰는 InventoryWindowPage 등 영향 화면을 확인한다. 실행·컴파일·테스트를 수행하지 못한 경우 해당 항목을 명시한다.

## 문서와 최종 보고

`DOCS/architecture/action-grid-panel.md`에 설정 위치, 기준 영역, 계산식, 기본값, 이전 방식과 효과 대상 경계를 추가한다. 제작·검증 절차가 달라지면 `DOCS/development/workflows.md`도 갱신한다. 신규 주요 구조가 생긴 경우에만 구조 문서를 갱신한다.

최종 보고에는 원인, 구현한 정책, TMP_MainScene 적용 값, 변경 자산과 공유 영향, 전후 비교, 수행한 테스트·미검증 사항을 포함한다. 실제 적용하지 않은 코드를 적용 완료로 보고하지 않는다.

수동 Tools 실행이 남는다면 AGENTS.md에 따라 별도 `사용자가 수행할 적용 절차`에 정확한 전체 메뉴 경로, 전제 조건, 순서, 저장 방법, 성공 확인과 재실행 주의점을 번호가 있는 코드 블록으로 제공한다. 임시 적용 메뉴만 추가하는 것을 개선 완료로 취급하지 않는다.
