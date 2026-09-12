# ActionGridCell 비율과 고정 상한의 최소 크기 적용

## 목적 및 해석

ActionGridCell에서 Icon과 EmptySlot에 각각 비율과 고정 크기를 설정하고, 두 값으로 계산한 크기 중 작은 크기를 적용한다. 고정값은 여백이 아니라 최대 너비·높이이며 Canvas UI 단위다. 물리 화면 픽셀 수를 의미하지 않는다. 두 표시의 설정은 독립적으로 유지한다.

이 문서는 구현 지침이다. 코드·Prefab·생성기·TMP_MainScene 적용과 검증까지 수행하되 Grid 배치·스크롤·아이템 데이터·효과 구현은 변경하지 않는다.

## 사전 확인 및 영향 경로

`AGENTS.md`, `DOCS/README.md`, `DOCS/architecture/action-grid-panel.md`와 기존 `DOCS/instructions/action-grid-cell-responsive-icon-sizing.md`, `DOCS/instructions/action-grid-empty-slot-sizing.md`를 읽는다. 이번 요구가 이전 비율 단독 설계를 확장한다. 실제 소스와 직렬화 자산을 다시 확인한다.

작성 시점의 `ActionGridCell`은 Icon/EmptySlot별 모드·비율·여백과 Configure API를 제공한다. `ActionGridIconSizingMode`는 FixedPadding과 RelativeToContent를 제공하며 고정 크기 상한은 없다. ApplySizingToRect를 공유하지만 두 표시의 활성 상태와 설정은 독립적이다.

| 기존 경로 | 작업 |
| --- | --- |
| `Assets/TxTRPG/UI/Runtime/ActionGridCell.cs` | 설정, 계산, 적용, 런타임 API를 확장한다. |
| `Assets/TxTRPG/UI/Editor/ActionGridPrefabBuilder.cs` | 새로 생성되는 자산도 명시적인 설정을 저장하도록 한다. |
| `Assets/TxTRPG/UI/Prefabs/ActionGridCell.prefab` | 실제 기본 설정과 참조를 저장한다. |
| `Assets/TxTRPG/UI/Prefabs/ActionGridPanel.prefab` | Cell 참조와 공유 영향을 확인한다. |
| `Assets/Scenes/TMP_MainScene.unity` | 대상 Grid에 신규 정책을 적용한다. |
| `Assets/TxTRPG/UI/Tests/Editor/ActionGridCellIconSizingTests.cs` | 아이콘 계산·회귀 테스트를 확장한다. |
| `Assets/TxTRPG/UI/Tests/Editor/ActionGridCellEmptySlotSizingTests.cs` | 빈 슬롯 계산·회귀 테스트를 확장한다. |

관련 호출자·Inspector·Variant와 공유하는 가방 화면을 추적한다. CodeGraph가 있으면 탐색에 먼저 사용하고 실제 소스와 자산으로 중요한 결과를 확인한다.

## 계산 계약

ContentRoot의 실제 너비·높이를 W, H, 설정 비율을 R, 고정 크기를 MaxWidth, MaxHeight라고 한다.

```text
FinalWidth  = min(max(0, W) × R, MaxWidth)
FinalHeight = min(max(0, H) × R, MaxHeight)
```

최소값은 축별로 선택한다. 계산 대상은 Image의 Rect이며 실제 그림의 불투명 영역이나 Sprite 원본 해상도가 아니다. Icon의 Preserve Aspect는 유지하여 그림이 최종 Rect 안에서 비율을 보존하게 한다. 비정사각형에서도 Rect와 그림의 종횡비를 혼동하지 않는다.

| ContentRoot | 비율 | 고정 상한 | 최종 Rect |
| --- | --- | --- | --- |
| 40×40 | 0.9 | 64×64 | 36×36 |
| 100×100 | 0.9 | 64×64 | 64×64 |
| 100×60 | 0.9 | 64×64 | 64×54 |

ContentRoot 자체의 여백은 유지하되 이 모드에서 Icon/EmptySlot의 FixedPadding을 다시 차감하지 않는다. 결과는 ContentRoot의 중앙에 배치한다. 큰 셀에서는 상한을 넘지 않고 작은 셀에서는 비율을 따라 줄어들어야 한다.

## Inspector와 호환성

- 기존 enum 값의 의미·순서를 보존하고 `RelativeWithMaxSize` 같은 신규 모드를 끝에 추가한다. 기존 두 모드는 호환성을 위해 유지한다. 이름은 제안이며 실제 명명 관례에 맞춰 결정한다.
- Icon Layout과 Empty Slot Layout 각각에 독립적인 Area Ratio와 Maximum Size(Vector2)를 제공한다. 새 모드에서는 이 두 값을 동시에 사용하고 Padding은 사용하지 않는다는 점을 설명한다.
- 기본 제안값은 두 표시 모두 비율 0.9, 최대 크기 64×64다. 이는 조정 가능한 초기값이며 TMP_MainScene의 실제 화면을 확인해 최종값을 결정하고 보고한다.
- 직렬화된 private 필드, Tooltip, Range 등을 사용한다. 고정값은 최대 크기이고 비율은 ContentRoot 기준임을 명시한다. 기존 Inspector가 있으면 확장하고 중복 Inspector를 만들지 않는다.
- 이전 자산은 기존 모드를 유지하여 외형이 저절로 변경되지 않게 한다. 신규 기본 Prefab과 TMP_MainScene에는 새 모드를 명시적으로 저장한다. 누락된 직렬화 필드의 초기값 처리와 Variant Override를 검증한다.
- 기존 Configure 메서드의 위치 인수 의미를 바꾸지 않는다. 별도 API나 명확한 overload로 최대 크기를 설정하고 두 표시의 변경이 서로에게 영향을 주지 않게 한다.

## 적용과 유효성

비율은 유한한 0~1, 최대 크기는 축별 유한한 0 이상의 값으로 정규화한다. 비율 0 또는 최대 크기 한 축 0은 그 축의 결과 0으로 정의한다. 0을 무제한이라는 숨은 규칙으로 사용하지 않는다. NaN·Infinity에는 비율 0.9, 해당 최대 크기 축 64를 사용한다. 음수 최대 크기는 0으로 제한한다. 설정과 런타임 API에 동일한 검증을 적용한다.

비율과 상한의 조합은 부모 크기에 따라 결과가 바뀐다. 기존 Anchor 비율 설정만으로 완료했다고 판단하지 않는다. 실제 최종 Rect를 계산·적용하고 부모 변경 때 재계산한다. 모드 전환 시 기존 Anchor·SizeDelta·Offset의 잔여 영향이 없도록 한다.

초기 활성화, Grid 크기 변경, ContentRoot만 변경되는 경우, Inspector 수정, 런타임 설정 변경, 비활성 상태의 설정 변경 후 재활성화, 풀 재사용을 검증한다. Cell 루트의 크기 변경 콜백만으로 모든 부모 변경이 감지된다고 가정하지 않는다. 필요한 경우 최소한의 변경 알림을 사용한다. 매 프레임 검색·불필요한 할당·강제 Layout rebuild와 재귀 갱신은 피한다.

레이아웃 계산은 Icon과 EmptySlot이 공유하되 설정·참조는 분리한다. Cell 루트, ContentRoot, 수량, 단축키, 테두리, 쿨다운과 클릭 영역은 변경하지 않는다. BindEmpty/Bind의 활성 상태 정책과 비동기 Sprite 교체 동작을 유지한다. 레이아웃 적용이 아이콘이나 빈 슬롯을 임의 활성화하지 않아야 한다. 누락 참조는 예외 없이 처리하고 필요한 진단은 반복 출력하지 않는다.

OnValidate는 가벼운 값 검증·표시 갱신에만 사용한다. Scene 저장이나 Prefab 재생성을 컴파일·리로드에 연결하지 않는다. Transform Scale이나 SetNativeSize를 크기 계산 대안으로 사용하지 않는다. 이미지 자체의 투명 여백은 별도 문제이며 자동 Crop을 추가하지 않는다.

## 자산 적용

Editor 연결·컴파일 상태·미저장 변경을 먼저 확인하고 사용자 변경을 보존한다. 실제 자산은 필요한 부분만 수정한다. 생성기를 갱신하되 전체 Prefab 재생성을 기본 적용 방법으로 삼지 않는다.

공용 Cell의 기본 모드를 바꾸면 Inventory 등 공유 화면도 회귀 검증한다. 기존 화면 유지가 필요하면 기존 모드나 Variant로 보존한다. 새 Scene, 운영 아이템 지급, 저장 데이터 변경은 필요하지 않다. TMP_MainScene에서 새 모드·비율·상한을 적용하고 저장 후 재진입하여 확인한다.

## 검증과 완료 기준

1. 위 계산 표와 상한 경계 직전·일치·직후를 Icon/EmptySlot 각각 검증한다. 비정사각형, 0 크기, 잘못된 비율·상한도 포함한다.
2. 순수 계산뿐 아니라 실제 RectTransform 결과를 검사한다. 모드 전환, ContentRoot 크기만 변경, 풀 재사용·재활성화 후 결과를 확인한다.
3. Icon 설정 변경이 EmptySlot을 바꾸지 않고 그 반대도 성립해야 한다. 빈 슬롯 → 아이템 → 빈 슬롯 전환을 확인한다.
4. 기존 FixedPadding과 RelativeToContent 결과 및 이전 Configure 호출의 호환성을 검증한다. 저장 후 Prefab 재열기에도 설정이 유지되어야 한다.
5. TMP_MainScene에서 작은 셀과 큰 셀을 동일 조건으로 비교 캡처한다. 최대 크기에 도달한 뒤 셀을 키워도 표시가 더 커지지 않는지 확인한다. 임시 레이아웃 수정은 원래 값으로 복구한다.
6. 수량·선택·컨텍스트 메뉴·스크롤과 공유 가방 화면을 확인한다. 지원 해상도와 입력 경로의 확인 결과를 기록한다. 관련 컴파일·테스트를 수행하고 미실행 항목은 명시한다.

## 문서와 최종 보고

`DOCS/architecture/action-grid-panel.md`에 계산식, 기준 영역, Inspector 필드, 기본값, 모드별 차이와 호환성을 기록한다. 제작·검증 절차가 바뀌면 `DOCS/development/workflows.md`도 갱신한다.

최종 보고에 적용한 모드·비율·상한, 변경 자산과 공유 영향, 실제 크기 예시, 전후 화면, 수행한 테스트·미검증 항목을 포함한다. 수동 적용이 남으면 별도의 `사용자가 수행할 적용 절차`에 전제 조건, 정확한 Tools 메뉴 전체 경로, 순서, 저장과 성공 확인 방법을 번호가 있는 코드 블록으로 제공한다. 코드 작성과 실제 적용·검증 완료를 구분한다.
