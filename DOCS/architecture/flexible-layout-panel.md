# FlexibleLayoutPanel 설계

## 목적

`FlexibleLayoutPanel`은 여러 UI 영역을 가로 또는 세로로 분할하고, 같은 컴포넌트를 자식에 재귀적으로 배치하여 복합 화면을 구성합니다. 화면 크기에 따라 축을 전환할 수 있으며, 고정 크기와 가중치 기반 크기를 함께 사용할 수 있습니다.

운영용 `Assets/TxTRPG/UI/Prefabs/FlexibleLayoutPanel.prefab`은 자식이 없는 조합용 컨테이너입니다. 실제 패널 조합은 씬 또는 별도 화면 Prefab에서 구성합니다.

## 런타임 구성

| 형식 | 책임 |
| --- | --- |
| `FlexibleLayoutPanel` | 축, 반응형 정책, 간격, Padding, Overflow를 해석하고 직계 자식의 크기와 위치를 결정합니다. |
| `FlexibleLayoutItem` | 부모의 주축에서 사용할 `Weighted` 또는 `Fixed` 크기 정책과 최소·최대 크기를 정의합니다. |
| `FlexibleLayoutSizeRequest` | Unity 오브젝트에 의존하지 않는 크기 계산 입력을 표현합니다. |

`FlexibleLayoutPanel`은 `LayoutGroup`을 상속하므로 중첩된 패널도 일반 자식처럼 계산됩니다. 각 패널은 직계 자식만 관리하며, 자식 패널은 자신의 하위 계층을 별도로 계산합니다.

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
├── MainContent                 Weighted 7, Vertical
│   ├── StoryTextPanelArea      Weighted 65
│   └── ActionGridPanelArea     Weighted 35
└── CharacterDisplayPanelArea   Weighted 3
```

각 Area에는 기존 UI Demo Prefab이 중첩되어 있으므로, Prefab Mode에서 전체 화면 비율과 개별 패널의 실제 표현을 함께 확인할 수 있습니다.

## 수명 주기와 제약

- `[ExecuteAlways]`과 `LayoutGroup`의 갱신 흐름을 사용하므로 Edit Mode에서 크기와 정책 변경 결과를 바로 확인할 수 있습니다.
- `Add`, `Remove`, `SetWeight`, `SetAxis`, `SetSpacing`, `Rebuild`는 런타임 조합을 지원합니다.
- 같은 축을 제어하는 `ContentSizeFitter`를 같은 오브젝트에 함께 사용하지 않습니다. 두 컴포넌트가 크기를 반복해서 변경할 수 있습니다.
- 레이아웃 계층은 일반적으로 3~5단계 이내로 유지합니다. 게임 로직에서 매 프레임 `Rebuild`를 호출하지 않고, 구성이나 화면 크기가 바뀔 때만 갱신합니다.
- `Node Id`는 디버깅과 추후 저장·저작 도구 확장을 위한 안정적인 식별자입니다. Demo 생성기는 알려진 노드에 고정 ID를 부여합니다.
