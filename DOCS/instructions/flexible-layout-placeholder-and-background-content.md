# FlexibleLayout Placeholder 및 BackgroundContentLayer 구현 지침

## 목적과 사전 확인

이 문서는 Sol Light에게 전달하는 구현 명세이며 완료된 기능을 설명하지 않습니다. 최신 저장소와 사용자 변경을 확인하고 코드, 자산, 생성기, 데모, 테스트 및 문서를 함께 완성하십시오.

AGENTS.md, DOCS/README.md, DOCS/architecture/flexible-layout-panel.md, DOCS/development/workflows.md를 읽으십시오. CodeGraph가 있으면 변경 영향을 먼저 탐색하고 실제 코드와 자산으로 확인하십시오.

확인 대상은 다음과 같습니다.

- Assets/TxTRPG/UI/Runtime/FlexibleLayoutPanel.cs
- Assets/TxTRPG/UI/Runtime/FlexibleContentLayoutGroup.cs
- Assets/TxTRPG/UI/Runtime/FlexibleLayoutItem.cs
- Assets/TxTRPG/UI/Editor/FlexibleLayoutPrefabBuilder.cs
- Assets/TxTRPG/UI/Prefabs/FlexibleLayoutPanel.prefab
- Assets/TxTRPG/UI/Tests/Editor/FlexibleLayoutPanelTests.cs

## Placeholder

Assets/TxTRPG/UI/Prefabs/FlexibleLayoutPlaceholder.prefab을 다음 요구대로 생성하십시오.

- 기본 구성은 활성 GameObject의 RectTransform과 FlexibleLayoutItem입니다.
- 이미지나 자식이 없어도 기존 Weighted/Fixed, 최소·최대 크기 및 Spacing 계산에 참여해야 합니다. Weight를 고정 백분율로 변경하지 마십시오.
- 투명 Image나 입력 차단 컴포넌트는 필요하지 않습니다. 별도 런타임 클래스는 필요한 동작이 있을 때만 추가하십시오.
- 비활성 여부는 기존 Include Inactive Children 정책을 따릅니다. 공간을 유지하려면 Placeholder를 활성 상태로 두도록 안내하십시오.
- 양쪽 콘텐츠 레이어에서 사용할 수 있어야 합니다.

## 레이어와 설정

형제 순서는 BackgroundLayer, BackgroundContentLayer, ContentLayer, ForegroundLayer입니다. BackgroundContentLayer는 배경보다 앞, 기존 콘텐츠보다 뒤에 렌더링하십시오.

두 콘텐츠 레이어는 패널 위에 겹치는 독립 영역입니다. 각각 FlexibleContentLayoutGroup을 두고 자신의 직계 자식만 배치하십시오. 레이어 사이에 자식 수, 가중치, 크기와 Spacing을 합산하지 마십시오. 레이어 자체는 콘텐츠 항목 계산에서 제외하십시오. 기존 계산을 재사용하고 중첩 패널을 지원하십시오.

기존 ContentLayer 동작을 보존하고 BackgroundContentLayer에 Use Content Layout Settings 옵션을 제공하십시오. 기본값은 활성화합니다.

- 공유 모드: 축, 축 정책, Breakpoint, Spacing, Padding, Alignment, Overflow, 비활성 자식 포함 정책과 클리핑을 기존 ContentLayer에서 가져옵니다. 영역도 ContentLayer의 최종 RectTransform을 따르며 수동 Offset과 Margin Override 결과를 반영합니다.
- 독립 모드: 동등한 설정을 별도로 제공합니다. 수동 Offset은 보존하고 명시적인 Margin Override가 있을 때만 덮어씁니다. Breakpoint는 해당 레이어 실제 너비로 판단합니다.
- 공유 모드 전환으로 독립 설정과 영역을 소실하지 않고 복귀 시 이전 값을 복원하십시오.
- 공유 중 직접 편집 불가능한 항목을 Inspector에서 표시하십시오. 루트를 설정의 기준으로 유지하고 내부 LayoutGroup과 서로 값을 덮어쓰지 않게 하십시오.

동일 설정이어도 자식 구성과 가중치가 다르면 분할 경계는 달라집니다. 뒤쪽 1:1, 앞쪽 1:3은 각자 가용 공간에서 독립 계산합니다. 앞뒤 항목 자동 대응은 구현하지 마십시오.

## API와 입력

기존 Add, Remove, SetWeight 시그니처와 ContentLayer 대상 동작을 유지하십시오. enum으로 대상 레이어를 명시하는 오버로드 또는 동등한 API를 추가하십시오. 문자열 이름 검색을 피하고 BackgroundContentLayer의 Root와 Layout을 읽을 수 있게 하십시오.

없는 레이어를 명시적으로 요청하면 실패 결과나 명확한 예외를 제공하며 다른 레이어로 대체하지 마십시오. 새 참조가 없는 기존 자산은 기존 콘텐츠가 정상 동작해야 합니다. 필요한 레이아웃만 갱신하고 매 프레임 강제 Rebuild, 반복 검색과 무한 갱신을 추가하지 마십시오.

BackgroundContentLayer에 독립 CanvasGroup을 제공하고 기본 blocksRaycasts와 interactable을 false로 설정하십시오. 개발자가 상호작용을 허용할 수 있게 하며 컨트롤러 탐색도 확인하십시오. 입력 허용이 앞쪽 Graphic을 관통한다는 의미가 아님을 문서화하십시오.

레이어별 RectMask2D와 알파를 관리하십시오. 효과는 레이아웃 제어 Transform 아래의 VisualRoot에 적용하는 예시를 제공합니다. 새 셰이더나 효과 엔진, Addressables 구조는 구현하지 마십시오.

## 자산과 업그레이드

운영 프리팹과 생성기를 함께 변경하십시오. 기존 자산 업그레이드는 명시적인 Editor 명령으로 제공하며 반복 실행해도 중복되지 않아야 합니다. 기존 콘텐츠, 참조, Offset과 Prefab Override를 보존하고 불필요한 Unpack을 피하십시오. Undo를 지원하고 저장되지 않은 씬을 자동 덮어쓰지 마십시오.

OnValidate에서 레이어를 반복 생성하거나 사용자 콘텐츠를 자동 이동하지 마십시오. 설정 동기화와 자산 업그레이드를 구분하십시오. 데모는 Assets/TxTRPG/UI/DEMO/FlexibleLayoutPanel/ 아래에 구성하고 운영 씬을 데모로 변경하지 마십시오. 에셋과 .meta를 함께 관리하십시오. 실행 중인 Unity Editor를 임의 종료하지 말고 사용 가능한 Editor 작업 경로를 활용하십시오.

## 검증과 완료 조건

관련 컴파일, 의미 있는 Edit Mode 테스트와 가능한 Unity 화면 검증을 수행하십시오.

- 빈 Placeholder의 크기 정책, 최소·최대 크기와 Spacing을 확인합니다.
- 한 레이어의 자식 추가·제거가 다른 레이어 배분을 바꾸지 않아야 합니다.
- 공유 설정과 영역 반영, 독립 설정 복원, 수동 Offset 보존을 확인합니다.
- 가로·세로, 좁은 화면, Breakpoint와 중첩 패널을 확인합니다.
- 실제 렌더링 순서, 알파, 클리핑, 입력 통과와 선택적 상호작용을 확인합니다.
- 기존 API, 새 레이어 없는 자산, 업그레이드 반복 실행을 확인합니다.
- 지속적인 Editor Dirty, 반복 Rebuild와 누수가 없어야 합니다.

DOCS/architecture/flexible-layout-panel.md와 DOCS/development/workflows.md를 갱신하십시오. 주요 구조 변경 시 DOCS/architecture/project-structure.md도 갱신하고 새 문서는 DOCS/README.md에 연결하십시오.

최종 보고에는 변경 파일, 실제 Editor 메뉴와 사용 순서, 업그레이드 방법, 실제 검증 결과와 미확인 항목을 포함하십시오. CharacterStatusPanel, Stat Window와 드래그 앤 드롭 저작 도구는 이번 범위에서 제외합니다.
