# ActionGridCell 빈 슬롯 표시 크기 설정

## 목적

아이템 Icon과 빈 슬롯 표시 EmptySlot의 크기를 개발자가 독립적으로 설정할 수 있게 한다. TMP_MainScene에서 빈 슬롯 표시에도 비율 기반 설정을 적용하여 결과를 확인할 수 있도록 한다. 코드뿐 아니라 Prefab·Scene 적용, 저장과 검증까지 수행한다.

## 사전 확인과 현재 동작

`AGENTS.md`, `DOCS/README.md`, `DOCS/architecture/action-grid-panel.md`, `DOCS/instructions/action-grid-cell-responsive-icon-sizing.md`를 읽는다. 이전 지침은 설계 의도를 참고하는 자료이며 현재 구현과 다르면 실제 소스와 자산을 확인한다.

작성 시점의 `ActionGridCell.BindEmpty()`는 Icon Image를 비활성화하고 `emptySlotVisual`을 활성화한다. `ApplyIconLayout()`은 Icon만 변경한다. 저장된 `ActionGridCell.prefab`의 EmptySlot은 ContentRoot를 기준으로 상하좌우 14의 고정 여백을 사용한다. 따라서 Icon Area Ratio를 바꿔도 빈 슬롯 사각형은 달라지지 않는다.

첨부 화면의 사각형이 EmptySlot이라는 판단은 코드와 외형을 바탕으로 한 추정이다. 구현 전에 실제 대상 Cell의 HasEntry, Icon.enabled, EmptySlot 활성 상태와 Rect를 확인하여 진단을 확정한다. 다른 원인이 확인되면 보고하고 이번 범위와 구분한다.

## 영향 범위

| 경로 | 작업 |
| --- | --- |
| `Assets/TxTRPG/UI/Runtime/ActionGridCell.cs` | 빈 슬롯 전용 설정·적용 API와 생명주기 연결을 추가한다. |
| `Assets/TxTRPG/UI/Prefabs/ActionGridCell.prefab` | 필수 참조와 빈 슬롯 기본 설정을 저장한다. |
| `Assets/TxTRPG/UI/Editor/ActionGridPrefabBuilder.cs` | 생성된 자산도 같은 설정을 사용하도록 한다. |
| `Assets/TxTRPG/UI/Prefabs/ActionGridPanel.prefab` | Cell 참조와 공유 영향 범위를 확인한다. |
| `Assets/Scenes/TMP_MainScene.unity` | 해당 ActionGridPanel의 빈 슬롯 표시를 실제 개선한다. |
| `Assets/TxTRPG/UI/Tests/Editor/ActionGridCellIconSizingTests.cs` | 기존 아이콘 검증을 유지하고 빈 슬롯 테스트를 확장하거나 별도 테스트 파일을 추가한다. |

실제 Inspector 확장과 Cell Variant가 있다면 함께 조사한다. Grid 배치·용량·스크롤, 아이템 지급·사용·저장, 전체 Scene 디자인, 신규 효과 구현은 범위에서 제외한다.

## 설정 계약

ActionGridCell에 `Empty Slot Layout` 설정 그룹을 추가한다. 다음 이름은 설계 제안이며 기존 API가 있다고 가정하지 않는다.

- `Empty Slot Sizing Mode`: `FixedPadding`과 `RelativeToContent`를 지원한다. 기존 크기 모드 타입을 안전하게 재사용할 수 있다면 불필요한 중복 enum을 만들지 않는다. 이름 변경이 필요하다면 직렬화·호출자 호환성을 검토한다.
- `Empty Slot Area Ratio`: ContentRoot 너비·높이에 각각 곱하는 0~1 비율이다. 비율 모드의 기본 제안값은 0.9다. Cell 전체나 이미지의 불투명 영역을 기준으로 한 값이 아님을 설명한다.
- `Empty Slot Padding`: 고정 모드에서 사용하는 상하좌우 Canvas UI 단위 여백이다. 기존 외형 보존값은 각 14다.

Icon 설정과 EmptySlot 설정은 독립적이어야 한다. 하나를 변경해도 다른 설정 값이나 Rect가 변하지 않아야 한다. Icon Area Ratio는 실제 아이템 Icon에만 적용된다는 Tooltip을 추가한다. 현재 상태가 빈 슬롯인지 아이템인지 확인할 수 있는 읽기 전용 정보가 기존 Inspector에 있다면 활용한다.

비율 모드에서는 EmptySlot Rect를 ContentRoot 안에 중앙 정렬하고 너비·높이를 각각 비율만큼 사용한다. 고정 여백을 추가로 중첩하지 않는다. 가능하면 Anchor로 구현하여 크기 변경을 자동으로 따르게 한다. 예를 들어 ContentRoot가 40×40이면 비율 0.9에서 36×36, 고정 여백 각 14에서는 12×12다.

설정은 직렬화된 private 필드로 두고 Header·Tooltip·Range·Min을 적절히 사용한다. 관련 모드의 값만 편집 가능하게 하는 것은 기존 Inspector 구조 안에서 최소한으로 구현한다. 범용 테마 프레임워크는 만들지 않는다.

## 적용과 안정성

1. 비율은 0~1로 정규화하고 NaN·Infinity에는 0.9를 사용한다. 비율 0은 영역 크기 0으로 정의한다. 여백은 음수를 허용하지 않고 가용 영역보다 클 때 Rect가 뒤집히지 않게 제한한다.
2. 런타임 설정 API와 Inspector 검증은 동일한 규칙을 사용한다. 최초 활성화, BindEmpty, 풀 재사용, 부모 크기 변경, 런타임 설정 변경과 Editor 미리보기에서 일관되게 적용한다.
3. 비활성 EmptySlot에도 설정을 저장·적용할 수 있어야 한다. 사이즈 계산이 활성 여부를 강제로 바꾸지 않아야 하며 아이템이 들어오면 기존대로 EmptySlot이 숨겨져야 한다.
4. Icon/EmptySlot 레이아웃 계산은 필요한 순수 계산만 공유한다. 각자의 Rect 참조와 설정은 분리한다. 매 프레임 검색·할당·강제 Layout rebuild와 재귀 갱신을 피한다.
5. EmptySlot 참조가 없거나 RectTransform이 없는 잘못된 구성에서는 NullReferenceException 없이 처리하고 명확한 검증 메시지를 제공한다. 매 프레임 반복 로그를 출력하지 않는다. 필수 참조는 Prefab에 저장한다.
6. Cell 루트, ContentRoot, 테두리, 수량, 쿨다운, 단축키, 선택 프레임, 클릭 영역은 변경하지 않는다. 향후 효과가 레이아웃 대상 Rect를 덮어쓰지 않도록 책임을 유지한다. 이번 작업에서 Animator·셰이더 효과를 추가하지 않는다.
7. OnValidate는 가벼운 값 검증과 표시 갱신만 수행한다. 컴파일·리로드와 연결한 Scene 저장 또는 자동 재생성을 추가하지 않는다.

## 기존 자산 호환 및 TMP_MainScene 적용

신규 필드가 없는 기존 자산은 FixedPadding 각 14로 기존 표시를 보존할 수 있어야 한다. 단순 필드 초기값만으로 기존 자산 이전이 완료되었다고 가정하지 않는다. 이미 사용자별 여백 Override가 있다면 이를 확인하여 보존한다. 고정 모드에서도 매번 14로 강제 덮어쓰지 않는다.

TMP_MainScene의 대상에는 RelativeToContent와 0.9를 명시적으로 적용한다. 공용 Cell Prefab의 기본값도 변경한다면 이를 쓰는 Inventory 등 모든 확인된 화면에 미치는 영향을 검증한다. 해당 Scene만 바꾸어야 한다면 기존 Cell Prefab 참조를 이용한 Variant로 격리한다. 불필요하게 모든 Prefab을 재생성하지 않는다.

EmptySlot의 색상·알파·Sprite는 기본적으로 유지한다. 영역이 커지면 배경 밝기가 다르게 느껴질 수 있으므로 실제 화면으로 확인하고, 별도 색상 변경이 필요하다면 크기 변경과 구분하여 보고한다. 개발자가 승인하지 않은 이미지 일괄 교체를 수행하지 않는다.

Editor 연결과 미저장 변경을 먼저 확인한다. Unity가 지원하는 자산 편집 경로로 필요한 변경만 적용·저장하고 사용자 변경을 임의로 저장하거나 폐기하지 않는다. 생성기의 재실행 결과도 같은 계약을 유지하도록 수정하되 검증을 위해 사용자 자산 전체를 덮어쓰지 않는다.

## 검증 기준

- ContentRoot 40×40에서 Ratio 0.9는 36×36, 고정 여백 각 14는 12×12가 되는지 실제 Rect 기준으로 검증한다.
- 0 크기, 작은 크기, 큰 크기, 비정사각형, 비율 0/1/잘못된 값, 과도한 여백을 검증한다.
- Icon 비율 변경이 EmptySlot에 영향을 주지 않고 그 반대도 성립하는지 확인한다.
- 빈 슬롯 → 아이템 → 빈 슬롯 전환과 풀 재사용에서 표시·설정이 유지되는지 확인한다. 검증용 아이템을 운영 저장 데이터에 지급하지 않는다.
- 비활성 상태의 설정 변경, 재활성화, 저장 후 Prefab·Scene 재열기에도 같은 결과가 나와야 한다.
- TMP_MainScene의 실제 운영 초기화 경로에서 EmptySlot 활성 상태를 확인하고 동일 해상도의 전후 화면을 캡처한다. Inspector 숫자만 바뀐 상태로 검증을 끝내지 않는다.
- 좁은 화면과 넓은 화면에서 Cell 크기 변화, 선택·클릭·컨텍스트 메뉴와 공유 Cell을 쓰는 가방 화면을 확인한다. 실행한 입력 방식과 검증하지 못한 실제 기기를 구분한다.
- 관련 컴파일·기존 아이콘 테스트·추가 빈 슬롯 테스트를 수행한다. 자동 계산 테스트 통과를 실제 화면 검증과 혼동하지 않는다.

## 문서 갱신과 최종 보고

`DOCS/architecture/action-grid-panel.md`에 Icon과 EmptySlot의 차이, 설정 위치, 기준 영역, 기본값과 이전 정책을 기록한다. 적용·검증 절차가 달라지면 `DOCS/development/workflows.md`도 갱신한다.

최종 보고에 확인된 실제 원인, 변경 경로, 적용한 비율·모드, 공유 Prefab 영향, 전후 화면, 수행한 테스트와 미검증 항목을 포함한다. 사용자가 조절해야 할 Inspector의 그룹·필드도 명시한다.

수동 Tools 실행이 남는다면 별도 `사용자가 수행할 적용 절차`에 정확한 전체 메뉴 경로, 전제 조건, 실행 순서, 저장 방법, 성공 확인과 재실행 주의점을 번호가 있는 코드 블록으로 제공한다. 코드·도구 작성과 실제 Scene 적용·검증 완료를 구분한다.
