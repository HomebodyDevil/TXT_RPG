# FlexibleLayoutPanel 설계

## 목적

`FlexibleLayoutPanel`은 여러 UI 영역을 가로 또는 세로로 분할하고, 같은 컴포넌트를 자식에 재귀적으로 배치하여 복합 화면을 구성합니다. 화면 크기에 따라 축을 전환할 수 있으며, 고정 크기와 가중치 기반 크기를 함께 사용할 수 있습니다.

운영용 `Assets/TxTRPG/UI/Prefabs/FlexibleLayoutPanel.prefab`은 배경, 두 개의 독립 콘텐츠 영역과 전경 계층을 포함하지만 두 콘텐츠 레이어가 비어 있는 조합용 컨테이너입니다. 실제 패널 조합은 씬 또는 별도 화면 Prefab에서 구성합니다.

```text
FlexibleLayoutPanel
├── BackgroundLayer
│   └── BackgroundVisualRoot
│       ├── BackgroundA
│       ├── BackgroundB
│       └── BackgroundEffectOverlay
├── BackgroundContentLayer
│   └── FlexibleContentLayoutGroup
├── ContentLayer
│   └── FlexibleContentLayoutGroup
└── ForegroundLayer
    └── Border
```

형제 순서는 렌더링 순서이기도 합니다. `BackgroundContentLayer`는 정적 배경보다 앞에, 기존 `ContentLayer`보다 뒤에 표시됩니다. 두 `FlexibleContentLayoutGroup`은 자신의 직계 자식만 계산하므로 앞쪽 1:3 구성과 뒤쪽 1:1 구성은 서로의 자식 수, Weight, Spacing 또는 분할 경계에 영향을 주지 않습니다. 모든 레이어에는 `LayoutElement.ignoreLayout`이 설정되어 있으므로 레이어 자체는 루트의 항목 계산에 참여하지 않습니다.

## 런타임 구성

| 형식 | 책임 |
| --- | --- |
| `FlexibleLayoutPanel` | 외부 API, Node ID, ContentLayer와 BackgroundContentLayer 참조 및 설정을 관리합니다. |
| `FlexibleContentLayoutGroup` | 자신이 연결된 콘텐츠 레이어의 실제 크기를 기준으로 직계 자식의 크기와 위치를 결정합니다. |
| `FlexibleLayoutItem` | 부모의 주축에서 사용할 `Weighted` 또는 `Fixed` 크기 정책과 최소·최대 크기를 정의합니다. |
| `FlexibleLayoutPlaceholder.prefab` | 그래픽과 입력 컴포넌트 없이 `FlexibleLayoutItem`만으로 배분 공간을 예약합니다. |
| `FlexibleLayoutSizeRequest` | Unity 오브젝트에 의존하지 않는 크기 계산 입력을 표현합니다. |
| `FlexibleLayoutBackground` | 배경 슬롯, 효과 Overlay, 표시·숨김, 즉시 교체와 교차 페이드를 관리합니다. |
| `FlexibleLayoutBackgroundStyle` | Sprite, Tint, Opacity, Material, 표시 방식과 클리핑 정책을 재사용 가능한 자산으로 정의합니다. |

중첩된 패널은 부모의 `FlexibleContentLayoutGroup`에서 일반 자식처럼 계산됩니다. 기존 `Add`, `Remove`와 `SetWeight` 시그니처는 계속 `ContentLayer`를 대상으로 합니다. `FlexibleLayoutContentLayer`를 받는 오버로드로 `BackgroundContent`를 명시할 수 있습니다. 해당 레이어가 없는 구형 자산에서 BackgroundContent를 요청하면 다른 레이어로 대체하지 않고 `InvalidOperationException`을 발생시킵니다.

## BackgroundContent 설정과 입력

`Use Content Layout Settings`는 기본적으로 활성화됩니다. 공유 모드에서는 축, 축 정책, Breakpoint, Spacing, Padding, Alignment, Overflow, 비활성 자식 포함 정책과 클리핑을 `ContentLayer` 설정에서 가져옵니다. 영역도 Margin Override와 수동 Offset을 포함한 `ContentLayer`의 최종 RectTransform을 따릅니다. Inspector에는 독립 설정이 읽기 전용으로 표시되며 루트 `FlexibleLayoutPanel`이 설정의 기준입니다.

공유 옵션을 끄면 BackgroundContent 전용 설정을 사용합니다. Breakpoint는 `BackgroundContentLayer` 자체의 실제 너비로 판정합니다. `Override Background Content Margins`가 꺼져 있으면 직접 작성한 RectTransform Offset을 유지합니다. 공유 모드로 전환할 때 독립 Rect를 저장하고, 독립 모드로 돌아올 때 저장한 Anchor, Pivot, Position과 Size Delta를 복원하므로 공유 동기화가 작성 값을 소실하지 않습니다.

`BackgroundContentLayer`의 CanvasGroup은 기본적으로 `interactable = false`, `blocksRaycasts = false`입니다. 필요한 화면에서 개발자가 두 값을 활성화할 수 있지만, 앞쪽 `ContentLayer`의 `Graphic.raycastTarget`이 입력을 먼저 받는 영역에서는 뒤쪽 입력까지 자동으로 관통하지 않습니다. 키보드와 게임패드 탐색에 포함하려면 BackgroundContent의 Selectable을 명시적으로 Navigation 그래프에 연결해야 합니다.

각 콘텐츠 레이어에는 독립 `RectMask2D`와 CanvasGroup이 있습니다. 레이아웃이 제어하는 항목 자체를 흔들거나 확대하지 말고 `ItemRoot/VisualRoot` 구조를 만든 뒤 효과를 `VisualRoot`에 적용합니다.

```text
BackgroundContentLayer
└── ItemRoot                 FlexibleLayoutItem
    └── VisualRoot           이동·확대·회전·알파 효과
        └── Image
```

## 빈 공간 Placeholder

`Assets/TxTRPG/UI/Prefabs/FlexibleLayoutPlaceholder.prefab`은 활성 RectTransform과 `FlexibleLayoutItem`만 포함합니다. 양쪽 콘텐츠 레이어에서 일반 항목과 동일하게 Weighted/Fixed, Minimum Size와 Maximum Size 계산 및 Spacing에 참여합니다. Weight는 고정 백분율이 아니라 다른 예약 공간을 제외한 가용 공간의 배분 비율입니다. `Include Inactive Children`가 꺼진 레이어에서는 비활성 Placeholder가 계산에서 제외되므로 공간을 유지하려면 활성 상태로 둡니다.

ContentLayer의 RectTransform Offset은 배경과 테두리에 대한 콘텐츠 전체의 외부 여백입니다. `FlexibleContentLayoutGroup.Padding`은 그 영역 안의 내부 여백이며, Spacing은 자식 사이의 간격입니다. 외부 Offset과 내부 Padding은 의도적으로 합산됩니다. 반응형 Breakpoint는 Padding을 제외하기 전의 실제 ContentLayer 너비를 기준으로 판단합니다.

Inspector의 `Override Content Margins`를 활성화하면 `Content Margins`의 Left, Right, Top, Bottom 값이 ContentLayer Offset에 적용됩니다. 비활성화하면 Prefab이나 씬에서 직접 편집한 ContentLayer RectTransform Offset을 보존합니다. 런타임에서는 `SetPadding`과 `SetContentMargins`를 사용하며, 직접 편집한 Offset으로 돌아갈 때는 `UseAuthoredContentOffsets`를 사용합니다.

`Overflow = Clip`은 최소 크기를 보존하는 크기 계산 정책이며 실제 렌더링 클리핑을 의미하지 않습니다. `Clip Content`를 활성화해야 ContentLayer의 `RectMask2D`가 켜져 넘친 콘텐츠가 잘립니다.

`Sample_Main_FlexibleLayoutPanelDemo.prefab`은 `SampleScene`의 주 레이아웃을 독립적으로 확인하기 위한 샘플입니다. 1920×1080 기준으로 Action, Story Text, Character 열을 1:3:1 가중치로 배치하며, 각 열에는 해당 기능의 Demo Prefab 인스턴스가 들어갑니다. 원본 씬과의 결합을 피하기 위해 생성기는 `SampleScene.unity`를 변경하지 않습니다.

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
├── BackgroundContentLayer
│   ├── ReservedBackdropSpace      빈 Placeholder
│   └── BackdropRegion
│       └── VisualRoot/Tint        효과 계층 예시
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
- `SetBackgroundUsesContentLayoutSettings`, `ConfigureBackgroundLayout`, `SetBackgroundContentMargins`, `UseAuthoredBackgroundContentOffsets`, `SetBackgroundClipContent`, `SetBackgroundAlpha`와 `SetBackgroundInteraction`은 뒤쪽 콘텐츠 영역을 명시적으로 제어합니다.
- 같은 축을 제어하는 `ContentSizeFitter`를 같은 오브젝트에 함께 사용하지 않습니다. 두 컴포넌트가 크기를 반복해서 변경할 수 있습니다.
- 레이아웃 계층은 일반적으로 3~5단계 이내로 유지합니다. 게임 로직에서 매 프레임 `Rebuild`를 호출하지 않고, 구성이나 화면 크기가 바뀔 때만 갱신합니다.
- `Node Id`는 디버깅과 추후 저장·저작 도구 확장을 위한 안정적인 식별자입니다. Demo 생성기는 알려진 노드에 고정 ID를 부여합니다.
- BackgroundVisualRoot는 배경 이동·확대·회전 같은 시각 효과 전용입니다. FlexibleLayoutPanel 루트나 ContentLayer에는 배경 애니메이션을 적용하지 않습니다.
- UI Material과 Shader는 Canvas 알파, RectMask2D, 스텐실과 대상 모바일 그래픽 API에서 별도로 검증해야 합니다.
