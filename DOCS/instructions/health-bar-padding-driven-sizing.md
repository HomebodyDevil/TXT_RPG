# HealthBarPanel Padding 기반 크기와 Alignment 작업 지침

## 목적과 우선순위

Sol Light는 이 명세에 따라 Slider 크기를 배경 영역과 Padding에서 자동 계산하도록 구현하십시오. 이 문서는 구현 목표이며 현재 구현 상태를 의미하지 않습니다.

이 문서는 health-bar-alignment-and-effect-roots.md의 크기 정책을 대체합니다. 이전의 고정 높이 24 및 Fixed 크기 중심 기본값을 새 기본값으로 사용하지 마십시오. 기존 효과 계층 분리와 호환성 요구는 계속 적용합니다. 이미 구현된 기능은 재작성하지 말고 최신 코드에서 필요한 차이만 수정하십시오.

## 사전 확인

AGENTS.md, DOCS/architecture/character-status-ui.md, DOCS/development/workflows.md 및 기존 정렬·효과 지침서를 읽으십시오. CodeGraph가 있으면 관련 의존성을 탐색하고 실제 소스와 자산으로 확인하십시오.

주요 확인 대상은 Assets/TxTRPG/UI/Runtime/Characters/HealthBarPanel.cs, Assets/TxTRPG/UI/Editor/CharacterStatusPrefabBuilder.cs, Assets/TxTRPG/UI/Prefabs/HealthBarPanel.prefab과 CharacterStatusPanel.prefab입니다. 기존 사용자 변경 및 참조를 보존하십시오.

## 기본 크기 계산

- 기준은 BackgroundLayer의 효과 적용 전 RectTransform입니다. 없으면 패널 루트를 사용합니다.
- Left, Right, Top, Bottom Padding을 개발자가 설정합니다. 단위는 화면 픽셀이 아닌 Canvas 로컬 UI 단위입니다.
- 기본값은 좌우 Padding 12, 상하 Padding 12, Horizontal=Center, Vertical=Middle입니다. 양 축 모두 남은 영역을 채웁니다. Offset은 기본 0입니다.
- Width = max(0, BackgroundWidth - Left - Right)입니다.
- Height = max(0, BackgroundHeight - Top - Bottom)입니다.
- Slider와 정렬용 BarRoot의 효과 전 영역이 위 계산 결과와 일치해야 합니다. 과거 BarRoot의 0~0.58 앵커나 고정 높이, 텍스트 영역 예약이 크기를 다시 제한하면 안 됩니다.
- 부모 크기 또는 Padding이 바뀌면 가로·세로 크기를 모두 재계산합니다. 내부 Fill Area의 테두리용 Inset과 바깥 Padding은 별개의 값입니다.

## Alignment의 정확한 의미

Alignment는 남은 공간에서 위치를 결정하며 크기를 임의로 줄이는 옵션이 아닙니다. Horizontal은 Left/Center/Right, Vertical은 Top/Middle/Bottom입니다.

Padding 내부를 완전히 채우는 축에는 남는 공간이 없으므로 Alignment를 바꿔도 위치는 같습니다. 이것은 정상 동작입니다. Inspector에 설명하고 Alignment 변경으로 몰래 너비나 높이를 줄이지 마십시오.

기존 Fixed 또는 최대 크기 옵션이 이미 있다면 호환 목적으로 유지할 수 있습니다. 이번 변경의 기본값은 Padding 기반 자동 크기여야 합니다. Alignment 활용을 위해 제한이 필요하면 선택적 Maximum Width/Height만 제공할 수 있습니다. 0은 제한 없음으로 정의하고 별도 비율·크기 모드는 추가하지 마십시오. 제한된 축은 min(가용 크기, 최대 크기)이며 남은 공간에서 Alignment를 적용합니다.

기준 Rect의 로컬 xMin, yMin을 사용하고 Pivot이 중앙이라고 가정하지 마십시오. 정상 Padding에서 다음 식으로 배치합니다.

```text
availableWidth  = max(0, W - L - R)
availableHeight = max(0, H - T - B)
width  = optionalMaxWidth  > 0 ? min(availableWidth, optionalMaxWidth) : availableWidth
height = optionalMaxHeight > 0 ? min(availableHeight, optionalMaxHeight) : availableHeight
x = xMin + L + (availableWidth  - width)  * horizontalFactor
y = yMin + B + (availableHeight - height) * verticalFactor
horizontalFactor: Left=0, Center=0.5, Right=1
verticalFactor: Bottom=0, Middle=0.5, Top=1
```

이 식의 x,y는 배치 사각형의 좌하단이며 anchoredPosition 자체가 아닙니다. 대상 부모 좌표계와 Pivot으로 변환하십시오. 기본 정렬에서는 BackgroundLayer와 BarRoot가 같은 부모 좌표계를 사용하게 구성하고 애니메이션용 VisualRoot를 기준으로 삼지 마십시오.

Padding이 비대칭이면 중앙은 Padding을 제외한 영역의 중앙입니다. 대칭 Padding일 때만 배경 중심과 일치합니다. 기존 Offset은 있다면 위 계산 후 명시적으로 적용하고, Padding 경계를 벗어날 수 있음을 안내하십시오.

음수 Padding은 0으로 보정하십시오. 좌우 합이 W보다 크면 계산에 사용하는 좌우 값을 비례 축소하여 합을 W로 맞추고 너비는 0으로 처리하십시오. 상하도 동일합니다. Inspector에 입력한 원본 Padding은 유지하여 부모가 커지면 다시 적용하십시오. NaN과 무한대 입력도 거부하거나 안전한 값으로 보정하십시오.

## 효과 및 런타임 갱신

레이아웃은 BarRoot, 효과는 하위 BarVisualRoot 및 배경·테두리·문구 VisualRoot를 제어하도록 기존 책임 분리를 유지하십시오. Slider.fillRect가 제어하는 앵커를 효과가 덮어쓰지 않게 하십시오. 효과 때문에 시각적 크기가 바뀌어도 Padding 계산에 재입력하지 마십시오.

Inspector와 런타임에서 Padding과 Alignment를 변경할 수 있어야 합니다. 기존 API를 유지하고 필요한 API만 추가하십시오. 체력값, 채움 방향, 문구 및 효과 상태를 배치 변경 때문에 초기화하지 마십시오.

초기 활성화, 설정 변경, 부모 및 기준 영역 리사이즈 후 다음 정상 레이아웃 갱신에서 반영하십시오. 매 프레임 전체 계층 검색이나 ForceRebuild를 추가하지 말고 갱신 순환을 방지하십시오. Editor에서도 미리 볼 수 있어야 하며 OnValidate에서 계층을 생성하거나 자산을 저장하지 마십시오.

## 자산과 이전 절차

운영 HealthBarPanel 프리팹과 생성기를 함께 수정하고 중첩 CharacterStatusPanel도 검증하십시오. 새 기본 프리팹은 양 축 자동 크기를 사용합니다. 이전에 개발자가 명시한 고정 크기나 Offset을 일괄 삭제하지 말고 Padding 기반 모드로 바꾸는 명시적인 이전 절차를 제공하십시오. 중복 컴포넌트 생성 없이 반복 실행 가능해야 하며 Undo와 Prefab Override를 보존하십시오.

문구는 기본 Overlay를 유지하고 문구 유무로 Slider 크기가 변하지 않게 하십시오. 표시 전용 Slider, Navigation=None, Handle 없음과 장식 Graphic의 RaycastTarget=false를 유지하십시오. 운영 씬에 데모를 자동 추가하지 마십시오.

## 검증 예시와 완료 조건

| 배경 크기 | Padding L/R/T/B | 기대 Slider 크기 |
| --- | --- | --- |
| 520×64 | 12/12/12/12 | 496×40 |
| 520×64 | 12/12/20/20 | 496×24 |
| 320×100 | 10/30/20/10 | 280×70 |
| 20×10 | 12/12/12/12 | 0×0 |

- 최대 크기 제한이 없으면 Alignment를 바꿔도 같은 영역을 유지합니다.
- 제한을 지원한다면 남는 영역에서 아홉 방향 정렬을 검증합니다.
- 비대칭 Padding, 비중앙 Pivot, 런타임 부모 리사이즈와 Canvas Scaler를 확인합니다.
- 효과 재생 중 Padding 변경, 효과 종료와 비활성화 이후에도 새 배치를 유지합니다.
- 체력 0/중간/최대, 긴 문구와 문구 없음, 기존 API 및 프리팹 이전을 확인합니다.
- 관련 컴파일과 의미 있는 Edit Mode 테스트, 가능한 Unity 화면 검증을 수행하고 미검증 항목을 별도 보고합니다.

DOCS/architecture/character-status-ui.md와 DOCS/development/workflows.md를 실제 구현에 맞춰 갱신하십시오. 최종 보고에는 변경 파일, Inspector 설정법, 런타임 사용법, 이전 절차와 검증 결과를 포함하십시오. 이번 작업은 레이아웃 변경이며 새 효과 엔진이나 Stat Window를 구현하지 않습니다.
