# FlexibleLayoutPanel 설계

## 목적

`FlexibleLayoutPanel`은 여러 UI 영역을 가로 또는 세로로 분할하고, 같은 컴포넌트를 자식에 재귀적으로 배치하여 복합 화면을 구성합니다. 화면 크기에 따라 축을 전환할 수 있으며, 고정 크기와 가중치 기반 크기를 함께 사용할 수 있습니다.

운영용 `Assets/TxTRPG/UI/Prefabs/FlexibleLayoutPanel.prefab`은 배경·콘텐츠·전경 계층을 포함하지만 `ContentLayer`가 비어 있는 조합용 컨테이너입니다. 실제 패널 조합은 씬 또는 별도 화면 Prefab에서 구성합니다.

```text
FlexibleLayoutPanel
├── BackgroundLayer
│   └── BackgroundVisualRoot
│       ├── BackgroundA
│       ├── BackgroundB
│       └── BackgroundEffectOverlay
├── ContentLayer
└── ForegroundLayer
    └── Border
```

`FlexibleLayoutPanel`은 `ContentLayer`의 자식만 배치합니다. 배경과 전경에는 `LayoutElement.ignoreLayout`도 설정되어 있으므로 시각 계층이 크기 계산에 참여하지 않습니다. 이전 자산처럼 Content Root 참조가 비어 있으면 하위 호환을 위해 루트의 직계 자식을 배치합니다.

## 런타임 구성

| 형식 | 책임 |
| --- | --- |
| `FlexibleLayoutPanel` | 축, 반응형 정책, 간격, Padding, Overflow를 해석하고 직계 자식의 크기와 위치를 결정합니다. |
| `FlexibleLayoutItem` | 부모의 주축에서 사용할 `Weighted` 또는 `Fixed` 크기 정책과 최소·최대 크기를 정의합니다. |
| `FlexibleLayoutSizeRequest` | Unity 오브젝트에 의존하지 않는 크기 계산 입력을 표현합니다. |
| `FlexibleLayoutBackground` | 배경 슬롯, 효과 Overlay, 표시·숨김, 즉시 교체와 교차 페이드를 관리합니다. |
| `FlexibleLayoutBackgroundStyle` | Sprite, Tint, Opacity, Material, 표시 방식과 클리핑 정책을 재사용 가능한 자산으로 정의합니다. |

`FlexibleLayoutPanel`은 `LayoutGroup`을 상속하므로 중첩된 패널도 일반 자식처럼 계산됩니다. 각 패널은 자신의 `ContentLayer` 직계 자식만 관리하며, 자식 패널은 자신의 하위 계층을 별도로 계산합니다. `Add`, `Remove`와 `SetWeight`도 Content Root를 기준으로 동작합니다.

## 배경 표현과 전환

`FlexibleLayoutBackgroundStyle`은 `Assets > Create > TxT RPG > UI > Flexible Layout Background Style`에서 생성합니다.

| 설정 | 동작 |
| --- | --- |
| `Stretch` | 비율을 강제하지 않고 패널 전체에 맞춥니다. |
| `Fit` | Sprite 비율을 유지하며 전체 이미지를 표시합니다. |
| `Fill` | Sprite 비율을 유지하며 패널을 채우고 넘치는 영역을 자를 수 있습니다. |
| `Tile` | Image의 Tiled 방식으로 반복합니다. Sprite Import 설정도 반복 표시에 적합해야 합니다. |
| `Sliced` | 9-Slice Border를 유지합니다. Sprite에 Border가 설정되어 있어야 합니다. |

`ClipToPanel`은 BackgroundLayer의 `RectMask2D`를 활성화하고, `Visible`은 비활성화합니다. 배경 슬롯과 효과 Overlay는 항상 `raycastTarget = false`이므로 콘텐츠 입력을 가로막지 않습니다.

BackgroundLayer, ContentLayer와 ForegroundLayer에는 각각 CanvasGroup이 있으므로 계층별 투명도를 독립적으로 설정할 수 있습니다. 패널 전체를 숨길 때만 더 상위 CanvasGroup을 사용합니다.

`ApplyStyle`은 즉시 교체하고 `Change`는 BackgroundA와 BackgroundB를 번갈아 사용하여 교차 페이드합니다. 진행 중 새 변경 요청이 들어오거나 컴포넌트가 비활성화되면 기존 전환을 취소합니다. 비정상적으로 큰 프레임 시간 때문에 전환이 건너뛰지 않도록 프레임별 누적값을 0.05초로 제한합니다.

`Material Mode = Shared`는 원본 Material을 공유합니다. `Instance`는 패널 슬롯 전용 Material을 한 번 생성하고 교체 또는 제거 시 정리합니다. 매 프레임 Material을 만들지 않습니다. `SetEffectsEnabled(false)`를 사용하면 애니메이션 감소나 낮은 품질 설정에서 효과 Overlay를 끌 수 있습니다.

스타일 자산 자체를 저장 데이터에 기록하지 않습니다. 저장 데이터에는 안정적인 Background ID를 기록하고, 외부 UI 자산 공급자가 ID를 Style로 변환한 뒤 `ApplyStyle` 또는 `Change`에 전달해야 합니다. 비동기 로딩, 실패 복구와 Addressables 수명 관리는 이 컴포넌트의 책임이 아닙니다.

## 크기 계산

1. 부모의 주축 크기에서 Padding과 자식 사이의 Spacing을 뺍니다.
2. `Fixed` 항목의 크기와 `Weighted` 항목의 최소 크기를 예약합니다.
3. 남은 공간을 `Weighted` 항목의 Weight 비율로 배분합니다.
4. Maximum Size에 도달한 항목의 남은 몫은 다른 가중치 항목에 재분배합니다.

Weight는 전체 패널 크기의 고정 백분율이 아니라 Padding, Spacing, 고정 크기와 최소·최대 크기를 제외한 배분 가능 공간의 비율입니다.

최소 크기의 합이 가용 공간보다 클 때 `ShrinkBelowMinimum`은 모든 항목을 비례 축소하여 부모 안에 유지합니다. `Clip`은 최소 크기를 보존하고 부모 영역 밖으로 넘칠 수 있게 합니다. 일반 게임 UI에서는 `ShrinkBelowMinimum`을 기본값으로 사용하며, 최소 터치 영역이나 가독성을 반드시 보존해야 하는 화면에서는 ScrollRect와 `Clip`의 조합을 검토합니다.

## 반응형 축 정책

| 정책 | 동작 |
| --- | --- |
| `Fixed` | `Fixed Axis`를 항상 사용합니다. |
| `HorizontalWhenWide` | 너비가 Breakpoint 이상이면 가로축을 사용하고, 그보다 좁으면 `Fixed Axis`를 사용합니다. |
| `VerticalWhenNarrow` | 너비가 Breakpoint 미만이면 세로축을 사용하고, 그 이상이면 `Fixed Axis`를 사용합니다. |

Demo는 넓은 화면에서 가로, 너비 720 미만에서 세로가 되도록 `Fixed Axis = Horizontal`, `Axis Policy = Vertical When Narrow`로 구성되어 있습니다.

## Demo 계층

```text
FlexibleLayoutPanelDemo
└── ContentLayer
    ├── MainContent                 Weighted 7, Vertical
    │   └── ContentLayer
    │       ├── StoryTextPanelArea  Weighted 65
    │       └── ActionGridPanelArea Weighted 35
    └── CharacterDisplayPanelArea   Weighted 3
```

각 Area에는 기존 UI Demo Prefab이 중첩되어 있으므로, Prefab Mode에서 전체 화면 비율과 개별 패널의 실제 표현을 함께 확인할 수 있습니다.

## 수명 주기와 제약

- `[ExecuteAlways]`과 `LayoutGroup`의 갱신 흐름을 사용하므로 Edit Mode에서 크기와 정책 변경 결과를 바로 확인할 수 있습니다.
- `Add`, `Remove`, `SetWeight`, `SetAxis`, `SetSpacing`, `Rebuild`는 런타임 조합을 지원합니다.
- 같은 축을 제어하는 `ContentSizeFitter`를 같은 오브젝트에 함께 사용하지 않습니다. 두 컴포넌트가 크기를 반복해서 변경할 수 있습니다.
- 레이아웃 계층은 일반적으로 3~5단계 이내로 유지합니다. 게임 로직에서 매 프레임 `Rebuild`를 호출하지 않고, 구성이나 화면 크기가 바뀔 때만 갱신합니다.
- `Node Id`는 디버깅과 추후 저장·저작 도구 확장을 위한 안정적인 식별자입니다. Demo 생성기는 알려진 노드에 고정 ID를 부여합니다.
- BackgroundVisualRoot는 배경 이동·확대·회전 같은 시각 효과 전용입니다. FlexibleLayoutPanel 루트나 ContentLayer에는 배경 애니메이션을 적용하지 않습니다.
- UI Material과 Shader는 Canvas 알파, RectMask2D, 스텐실과 대상 모바일 그래픽 API에서 별도로 검증해야 합니다.
