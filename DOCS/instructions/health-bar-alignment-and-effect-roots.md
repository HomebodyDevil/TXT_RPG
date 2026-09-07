# HealthBarPanel 정렬 및 효과 확장 작업 지침

## 목적과 현재 근거

이 문서는 Sol Light에게 전달할 구현 명세입니다. 구현 완료를 의미하지 않습니다. AGENTS.md, DOCS/architecture/character-status-ui.md, DOCS/development/workflows.md를 읽고 최신 소스와 사용자 변경을 확인하십시오.

확인한 기존 CharacterStatusPrefabBuilder는 BarRoot의 세로 앵커를 0~0.58로, TextLayer를 0.58~1로 설정합니다. Slider는 BarRoot를 채우므로 배경 전체의 중앙에 있지 않습니다. HealthBarPanel은 체력값과 문구를 적용하고 HealthBarEffect를 호출하지만 정렬 설정은 없습니다. 이번 작업은 Slider의 기본 수직 중앙 정렬, 개발자 정렬 설정, 런타임 변경과 효과용 계층 분리를 구현합니다.

주요 기존 파일은 다음과 같습니다.

- Assets/TxTRPG/UI/Runtime/Characters/HealthBarPanel.cs
- Assets/TxTRPG/UI/Runtime/Characters/CharacterStatusPanel.cs
- Assets/TxTRPG/UI/Editor/CharacterStatusPrefabBuilder.cs
- Assets/TxTRPG/UI/Prefabs/HealthBarPanel.prefab
- Assets/TxTRPG/UI/Prefabs/CharacterStatusPanel.prefab

CodeGraph가 있으면 호출자, 생성기와 영향받는 테스트를 먼저 탐색하고 실제 자산으로 검증하십시오.

## 정렬과 크기의 계약

> 후속 요청으로 아래 크기 기본값과 Fixed/Stretch 정책은 [Padding 기반 크기 작업 지침](health-bar-padding-driven-sizing.md)으로 대체됩니다. 충돌 시 후속 지침을 우선하고, 이 문서의 효과 계층과 수명 관리 요구는 유지하십시오.

- 기준은 BackgroundLayer의 효과 적용 전 RectTransform 영역입니다. Sprite의 투명 여백이나 애니메이션 중 시각적 외곽선은 기준으로 사용하지 않습니다.
- 기본값은 Vertical=Middle, Horizontal=Center, 가로 Stretch, 고정 높이 24, 좌우 Padding 12, 상하 Padding 0, Offset 0입니다. 단위는 Canvas 로컬 UI 단위입니다.
- Horizontal은 Left/Center/Right, Vertical은 Top/Middle/Bottom을 독립 선택할 수 있게 하십시오.
- 크기 정책은 각 축별 Fixed/Stretch로 명확히 구분하십시오. Stretch인 축은 정렬 선택이 결과에 영향을 주지 않음을 Inspector에서 표시하십시오. Left/Right 차이를 확인하려면 가로 Fixed를 사용할 수 있어야 합니다.
- Padding을 제외한 기준 영역 안에서 정렬하고, 추가 Offset은 X가 오른쪽, Y가 위쪽인 로컬 좌표로 정의하십시오. 비대칭 Padding을 지정하면 중앙은 남은 영역의 중앙입니다.
- 부모 영역이 고정 크기보다 작아지면 기본적으로 가용 영역까지 축소하고 음수 크기를 방지하십시오. 비정상 입력의 보정 정책을 문서화하십시오.
- Slider.Direction은 게이지 채움 방향입니다. 배치 Alignment 변경으로 Direction이나 현재 체력값을 바꾸지 마십시오.
- 배경 기준 참조가 없는 이전 자산은 루트 RectTransform으로 대체하여 정상 동작하게 하십시오. 같은 패널 내부의 기준만 지원하고 순환 참조를 검증하십시오.

## 권장 계층과 속성 소유권

아래는 목표 계층 예시입니다. 기존 참조와 애니메이션 경로를 보존할 수 있도록 실제 이름과 이전 방식은 확인 후 결정하십시오.

```text
HealthBarPanel
├── BackgroundLayer                 정렬 기준 영역
│   └── BackgroundVisualRoot        배경 Image 및 효과
├── BarRoot                         정렬·크기 계산 전용
│   └── BarVisualRoot               바 전체 흔들림·확대 등
│       ├── Slider
│       │   ├── Background
│       │   └── Fill Area
│       │       └── Fill            Slider.fillRect가 제어
│       │           └── FillVisualRoot
│       ├── BarEffectOverlay
│       └── BorderVisualRoot       선택적 테두리 Image 및 효과
├── TextLayer
│   └── TextVisualRoot             선택적 문구 및 효과
├── ForegroundEffectLayer
└── TransitionOverlay
```

- 정렬 담당 코드는 BarRoot만 제어하고 효과는 하위 VisualRoot를 제어하십시오. BackgroundLayer 자체를 애니메이션하지 마십시오.
- Slider가 갱신하는 fillRect 앵커와 효과가 쓰는 Transform을 분리하십시오. Image를 하위로 옮긴 경우 Slider.targetGraphic, 마스크, 재질과 직렬화 참조도 함께 갱신하십시오.
- BackgroundVisualRoot, BarVisualRoot, BorderVisualRoot와 TextVisualRoot를 명시적 참조로 제공하십시오. Border는 기본적으로 비활성 또는 시각적으로 중립이어야 합니다.
- 기본 Slider 배치는 문구의 존재 여부에 따라 중앙에서 밀려나지 않아야 합니다. 문구는 기본적으로 바 위의 Overlay로 두고 Label은 왼쪽, Value는 오른쪽에 표시하십시오. 긴 문구가 겹치지 않도록 별도 Rect 영역을 사용하고 축소·생략 정책을 지정하십시오. TMP 내용을 임의로 변경하지 마십시오.
- 문구 참조가 없어도 정상 동작해야 하며, 텍스트 효과가 가독성을 해치거나 바·테두리에 가려지지 않도록 렌더링 순서를 검증하십시오.

## 런타임 설정과 효과 수명

정렬 계산은 작은 전용 컴포넌트 또는 일관된 내부 모듈로 구현하고 Gameplay/Binder와 결합하지 마십시오. 정렬, 크기, Padding과 Offset을 Inspector와 런타임 API로 변경할 수 있게 하십시오. API 이름 예시는 SetAlignment, SetSize, SetPadding, SetOffset이며 실제 이름은 기존 관례에 맞추십시오.

초기 활성화, 설정 변경, 기준 영역 리사이즈와 Canvas 레이아웃 변경 시 갱신하십시오. 매 프레임 전체 계층 검색이나 ForceRebuild를 추가하지 마십시오. 효과 재생 중 정렬을 바꿔도 효과의 로컬 상태를 덮어쓰지 않아야 합니다. OnValidate는 계층을 생성하거나 사용자 자산을 저장하지 않아야 합니다.

기존 HealthBarEffect의 Apply(previous, current, isInitialValue), RegisterEffect, UnregisterEffect 계약을 재사용하십시오. 현재 UnregisterEffect는 목록에서 제거할 뿐 Clear를 호출하지 않으므로 해제·교체·비활성화·파괴 시 누가 Clear와 취소를 담당할지 명시하고 구현하십시오. 재등록 시 최신 값을 초기 적용하되 피격 효과로 오인하지 않아야 합니다. 비활성 효과를 호출할지와 재활성화 시 동기화 정책도 정하십시오.

Slider 값은 HealthBarPanel이 소유합니다. 향후 시각적 보간 효과가 필요하면 별도 표시용 값이나 지연 게이지를 사용하여 여러 컴포넌트가 같은 Slider.value를 덮어쓰지 않게 하십시오.

런타임 효과 켜기·끄기와 등록·해제를 지원하고, 효과가 변경한 색·알파·Transform을 해제 시 원래 상태로 복원할 수 있어야 합니다. 실제 글리치·모자이크 셰이더 전체를 이번에 구현할 필요는 없습니다. 검증용 작은 효과만으로 정렬과 효과의 공존을 확인하십시오.

공유 Material이나 Style 원본을 런타임에 직접 수정하지 마십시오. 개별 Material을 생성하면 교체 및 파괴 시 해제하고 매 프레임 복제하지 마십시오. 기존 공통 배경 렌더러를 활용할지는 실제 의존성과 필요성을 확인하여 결정하십시오. 이 작업 때문에 새 Addressables 로딩 계층을 만들지 마십시오.

## 호환성, 자산 및 데모

- Slider의 표시 전용 설정, Navigation=None, Handle 없음과 SetValueWithoutNotify를 유지하십시오. 장식 Graphic의 RaycastTarget은 false로 두십시오.
- 기존 HealthBarPanel.Configure, 상태 적용, 문구 포매터와 Binder API를 보존하십시오. 정렬 갱신은 체력이나 문구 데이터를 초기화하지 않아야 합니다.
- HealthBarPanel 운영 프리팹과 CharacterStatusPrefabBuilder를 함께 변경하고 중첩 CharacterStatusPanel 프리팹도 확인하십시오.
- 계층 변경으로 참조, Prefab Override와 Animator 경로가 끊기지 않게 하십시오. 명시적이고 반복 실행 가능한 이전 절차를 제공하고 Undo와 기존 사용자 설정을 보존하십시오. 생성기로 사용자 수정 자산을 무조건 덮어쓰지 마십시오.
- 별도 데모에서 중앙 기본값, 아홉 방향 정렬, 크기 정책 및 런타임 변경과 효과 공존을 확인할 수 있게 하십시오. 운영 씬에 데모나 상태 패널을 자동 추가하지 마십시오.

## 수용 기준과 보고

관련 컴파일 및 의미 있는 Edit Mode 테스트와 가능한 Play Mode 화면 검증을 수행하십시오.

1. 기본 설정에서 Slider의 효과 전 중심 Y가 BackgroundLayer 중심 Y와 일치합니다.
2. Fixed 크기로 아홉 방향 정렬을 확인하고 Stretch, 비대칭 Padding, Offset의 결과도 검증합니다.
3. 좁은 화면, 부모 리사이즈, Canvas Scaler 및 중첩 FlexibleLayoutPanel에서도 배치가 유지됩니다.
4. 런타임 정렬 변경 중 효과가 지속되며 효과 종료 시 새 정렬을 유지합니다.
5. 효과 해제·교체·비활성화 시 잔여 애니메이션과 임시 Material이 남지 않습니다.
6. 체력 0/중간/최대, 최대치 변경, 긴 현지화 문구, 문구 없음과 기존 자산을 확인합니다.
7. 체력 수치와 Binder 동작, Slider 채움 방향에 회귀가 없고 지속적인 Editor Dirty나 Rebuild 순환이 없습니다.

DOCS/architecture/character-status-ui.md와 DOCS/development/workflows.md를 갱신하십시오. 주요 책임 경계 변경 시 project-structure.md도 갱신하고 새 문서는 DOCS/README.md에 연결하십시오. 이 지침서의 목표를 검증 없이 구현 완료로 서술하지 마십시오.

최종 보고에는 변경 파일, Inspector 및 런타임 사용법, 기본 배치, 기존 자산 이전 방법, 실제 검증과 미검증 항목을 포함하십시오.
