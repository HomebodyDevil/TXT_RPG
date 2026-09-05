# 개발 및 검증 절차

## StoryTextPanel을 씬에서 사용하기

1. 씬에 Canvas를 준비합니다.
2. `Assets/TxTRPG/UI/Prefabs/StoryTextPanel.prefab`을 Canvas 아래에 배치합니다.
3. 앵커와 크기는 부모 화면 레이아웃 정책에 맞게 설정합니다.
4. 게임 또는 스토리 컴포넌트에서 `StoryTextPanel` 참조를 직렬화합니다.
5. 현지화가 완료된 문자열로 `StoryMessage`를 생성하여 `AddMessage`를 호출합니다.

```csharp
[SerializeField] private StoryTextPanel storyTextPanel;

public void ShowNarration(string localizedBody)
{
    storyTextPanel.AddMessage(new StoryMessage(localizedBody));
}

public void ShowDialogue(string localizedSpeaker, string localizedBody)
{
    storyTextPanel.AddMessage(new StoryMessage(localizedBody, localizedSpeaker));
}
```

## Edit Mode에서 설정 확인하기

1. `Assets/TxTRPG/UI/DEMO/StoryTextPanelDemo.prefab`을 Prefab Mode로 엽니다.
2. 중첩된 `StoryTextPanel` 오브젝트를 선택합니다.
3. 스크롤바 위치·배경·손잡이 크기와 투명도 설정을 변경합니다.
4. Prefab 또는 Scene View에서 최신 메시지가 아래쪽에 배치된 모습을 확인합니다.

데모 데이터의 문구를 수정한 뒤 다음 메뉴를 실행하면 미리보기 항목이 다시 생성됩니다.

```text
Tools > TxT RPG > Refresh Story Text Panel Edit Mode Preview
```

## CharacterDisplayPanel을 씬에서 사용하기

1. `Assets/TxTRPG/UI/Prefabs/CharacterDisplayPanel.prefab`을 Canvas 아래에 배치합니다.
2. `Character2DView`의 `Appearance Definitions`에 캐릭터별 `CharacterAppearanceDefinition` 자산을 등록합니다.
3. 외형 정의의 `Framing`을 원본에 맞게 `Whole Artwork`, `Thigh Up` 또는 `Custom`으로 설정합니다.
4. View의 `Animate Visibility`, 등장·퇴장 시간과 프레임 시간 상한을 설정합니다.
5. 게임 또는 스토리 컴포넌트에서 `CharacterDisplayPanel`을 참조하고 ID 기반 `CharacterPresentation`을 전달합니다.
6. 위치 또는 크기 애니메이션은 `ArtworkRoot`가 아니라 `VisualRoot`에 적용합니다.

외형 정의 자산은 `Assets > Create > TxT RPG > UI > Character Appearance Definition`에서 생성할 수 있습니다.

캐릭터 표시 데모는 `Assets/TxTRPG/UI/DEMO/CharacterDisplayPanel/CharacterDisplayPanelDemo.prefab`을 Prefab Mode로 열어 확인합니다. Edit Mode에서는 생성된 샘플 캐릭터가 완전히 표시되며, Play Mode에서는 실제 `ShowCharacter` 호출과 등장 페이드가 실행됩니다.

같은 Demo의 `CharacterDisplayBackgroundDemoStyle.asset`은 공통 `PanelBackgroundStyle` 제작 예시입니다. CharacterDisplayPanel의 배경은 `ApplyBackground`, `ChangeBackground`, `ClearBackground`로 캐릭터 표시와 독립적으로 제어합니다. 배경 전용 효과는 `BackgroundEffectOverlay`, 캐릭터 전용 효과는 Character2DView의 `EffectOverlay`, 전경 효과는 `ForegroundEffectLayer`, 전체 화면 전환은 `TransitionOverlay`에 적용합니다.

Story, Character와 Action Grid Demo는 동일한 `PanelStartupController` 경로를 사용합니다. Demo 루트의 `CanvasGroup`, 구체적인 `PanelInitialDataLoader`, `FadePanelRevealTransition`과 `PanelStartupController` 참조를 함께 유지해야 합니다. Fade Duration은 기본 0.35초이며, 모션 감소가 필요하면 `Reduce Motion`을 활성화합니다. Loader를 교체할 때는 동기 `Start()`를 추가하지 않고 `LoadAndApplyAsync`가 모든 자산 준비를 기다리도록 구현합니다.

## EnemyDisplayPanel을 씬에서 사용하기

1. `Assets/TxTRPG/UI/Prefabs/EnemyDisplayPanel.prefab`을 Canvas 또는 `FlexibleLayoutPanel.ContentLayer` 아래에 배치합니다.
2. `Enemy2DDisplayBackend.Appearance Definitions`에 전투에서 사용할 `EnemyAppearanceDefinition`을 등록합니다.
3. `InstanceId`는 전투 내에서 고유하게, `EnemyId`는 같은 적 종류끼리 동일하게 구성하여 `EnemyPresentation` 목록을 만듭니다.
4. 전체 목록 교체에는 `SetEnemies`, 부분 변경에는 `AddEnemy`, `UpdateEnemy`, `RemoveEnemy`를 사용합니다.
5. 타깃, 애니메이션과 효과 API에는 `EnemyId`가 아니라 `InstanceId`를 전달합니다.
6. 화면별 최대 표시 수와 `ResponsiveHorizontalEnemyLayoutStrategy`의 최대 열 수, 기준 View 크기, 간격과 최소·최대 배율을 설정합니다.

외형 정의는 `Assets > Create > TxT RPG > UI > Enemy Appearance Definition`에서 생성합니다. Editor fallback Sprite는 미리보기용이며, Player에서 사용할 Sprite는 `Fallback Sprite Asset Id` 또는 Variant의 `Sprite Asset Id`에 Addressables 주소를 설정해야 합니다.

`Assets/TxTRPG/UI/DEMO/EnemyDisplayPanel/EnemyDisplayPanelDemo.prefab`을 Prefab Mode로 열면 같은 `EnemyId`를 가진 세 개의 고유 인스턴스, 행 중앙 정렬과 타깃 표시를 확인할 수 있습니다. Play Mode에서는 `PanelStartupController`가 초기 자산과 배경을 기다린 뒤 패널을 표시합니다. 좁은 화면에서는 최소 배율을 침범하지 않는지와 여러 행의 각 행이 중앙에 배치되는지를 확인합니다.

## ActionGridPanel을 씬에서 사용하기

1. `Assets/TxTRPG/UI/Prefabs/ActionGridPanel.prefab`을 Canvas 아래에 배치합니다.
2. 아이템과 스킬 도메인 데이터를 `ActionGridEntry` 목록으로 변환하여 `SetEntries`에 전달합니다.
3. `IActionMenuProvider`와 `IActionCommandExecutor` 구현을 `SetServices`로 연결합니다.
4. `Grid Alignment`, `Incomplete Row Alignment`, `Vertical Placement`, `Layout Mode`, 최대 열 수, 셀 최소·최대 크기, `Spacing (X, Y)`와 Padding을 화면 정책에 맞게 설정합니다. Grid Alignment 기본값은 Center이며 전체 열 묶음을 정렬합니다. Incomplete Row Alignment 기본값은 Left이며 덜 찬 마지막 행만 정렬합니다. 운영 Prefab의 Vertical Placement는 Center When Content Fits이므로 슬롯이 모두 들어오면 세로 중앙, 넘치면 상단부터 배치됩니다. `Fixed Columns`의 열 수는 최대값이며 좁은 화면에서는 자동으로 감소합니다.
5. 제한된 슬롯을 표시할 때만 `Fill Capacity With Empty Slots`와 `Capacity`를 사용합니다.

운영용 ActionGridPanel Prefab은 기본적으로 `Fill Capacity With Empty Slots`를 사용합니다. Play Mode가 시작되면 초기 데이터가 없어도 Capacity만큼 빈 슬롯이 활성화됩니다. 이후 도메인 시스템은 `TryAddEntry`, `TryInsertEntry` 또는 `SetEntries`로 아이템과 스킬을 등록합니다. 데이터가 있는 셀만 표시하려는 목록 UI에서는 `Population Mode = Entries Only`로 명시적으로 변경합니다.

기본 스크롤바 정책은 `Visibility = Hidden`, `Space Mode = Reserve When Visible`입니다. 런타임 변경에는 `ConfigurableScrollbarController.Visibility`, `SpaceMode`, `Side`, `Width`, `Gap` 속성을 사용합니다. 속성 변경은 Viewport offset과 ActionGridPanel 슬롯 레이아웃을 즉시 갱신합니다.

스크롤바 Side와 관계없이 슬롯 영역의 시각적 중심을 고정하려면 Inspector에서 `Space Mode = Reserve Symmetrically Always`를 선택합니다. 이 모드는 Visibility가 Hidden이어도 Viewport 좌우에 각각 `Width + Gap`을 예약합니다. SampleScene의 `Main_FlexibleLayoutPanel/ActionGridPanel`에는 이 설정과 `Grid Alignment = Center`, `Incomplete Row Alignment = Left`, `Vertical Placement = Center When Content Fits`가 적용되어 있습니다. 화면 너비에 따른 자동 모드 전환은 현재 지원하지 않으므로 필요한 경우 런타임 구성 코드에서 명시적으로 `SpaceMode`를 변경합니다.

대칭 모드의 `OppositeScrollbarArea`는 Builder가 자동 생성하고 컨트롤러에 연결합니다. Width는 Scrollbar 루트와 Opposite 영역에 동일하게 적용되며 Gap은 두 영역의 너비가 아니라 Viewport 간격입니다. 수동 Prefab을 구성할 때에는 Graphic이나 입력 컴포넌트가 없는 RectTransform을 Scroll View 아래에 추가하고 `Opposite Scrollbar Area` 참조에 연결합니다.

ActionGridPanel 전체를 기준으로 슬롯 중심을 유지하려면 `Scroll View`의 외부 Left와 Right offset도 같은 값으로 설정해야 합니다. Builder와 운영 Prefab의 기본값은 각각 18px입니다. Handle을 더 얇게 보이게 하는 `Sliding Area`의 수평 inset은 시각 설정이므로, Opposite 영역이나 외부 여백을 Handle 너비에 맞추지 않습니다. Builder를 수정한 뒤에는 `Tools > TxT RPG > Rebuild Action Grid Prefabs`를 실행하여 운영 Prefab과 이를 상속하는 Demo 구성을 갱신합니다.

런타임 정렬 변경에는 `SetGridAlignment`, `SetIncompleteRowAlignment`와 `SetVerticalPlacement`를 사용합니다. 이 옵션은 데이터 채움 방향과 무관하므로 Right를 선택해도 Entry 순서는 역전되지 않습니다. Center When Content Fits의 판정은 화면에 활성화되는 Cell 수를 기준으로 하므로, Fill Capacity With Empty Slots에서는 Entry 수가 적어도 Capacity 전체가 높이 계산에 포함됩니다. ActionGridPanel Demo는 Capacity 12와 최대 5열을 사용하므로 넓은 화면에서 불완전한 마지막 행과 세로 중앙 배치를 확인할 수 있습니다. Inspector에서 Capacity나 패널 높이를 변경하면 상단 스크롤 배치로 전환되는 결과도 확인할 수 있습니다.

ActionGridPanel의 Content 높이는 패널이 직접 계산하므로 Content에 `ContentSizeFitter`를 추가하지 않습니다. 이전에 직접 제작한 Prefab에 ContentSizeFitter가 남아 있어도 런타임에는 비활성화되지만, Editor 계층에서도 제거하여 레이아웃 책임을 명확하게 유지하는 것이 좋습니다.

`Initial Capacity`와 `Population Mode`로 초기 셀 풀과 빈 슬롯 표시를 설정합니다. 런타임 변경은 `SetCapacity`, `TryAddEntry`, `TryInsertEntry`, `RemoveEntry`, `UpdateEntry` API를 사용합니다. 공통 스크롤바 외형은 `Assets > Create > TxT RPG > UI > Scrollbar Style`에서 생성하고 Scroll View의 `ConfigurableScrollbarController`에 연결합니다.

셀에 애니메이션을 추가할 때 테두리 효과는 `Border`, 아이콘과 상태의 흔들림·확대·회전 효과는 `ContentRoot`에 적용합니다. `ActionGridCell` 루트 Transform에는 위치 또는 크기 애니메이션을 적용하지 않습니다.

컨텍스트 메뉴는 선택 셀의 오른쪽을 우선하여 표시하고 공간이 부족하면 왼쪽, 아래쪽, 위쪽 순으로 위치를 전환합니다. 모든 후보가 완전히 들어가지 않으면 `ContextMenuAnchor` 경계 안으로 좌표를 제한합니다. 메뉴의 셀 간격은 `ActionContextMenu.Anchor Gap`, 패널 가장자리 여백은 `Edge Padding`에서 설정합니다.

`Assets/TxTRPG/UI/DEMO/ActionGridPanel/ActionGridPanelDemo.prefab`을 Prefab Mode로 열면 아이템과 스킬, 수량, 비활성, 쿨다운, 선택과 컨텍스트 메뉴를 동시에 확인할 수 있습니다. Play Mode에서는 셀 선택과 Demo 명령 실행 흐름을 확인할 수 있습니다.

## FlexibleLayoutPanel로 화면 구성하기

1. `Assets/TxTRPG/UI/Prefabs/FlexibleLayoutPanel.prefab`을 Canvas 또는 상위 UI 컨테이너 아래에 배치합니다.
2. 배치할 UI를 `ContentLayer` 아래에 추가합니다.
3. ContentLayer의 직계 자식에 `FlexibleLayoutItem`을 추가하고 `Weighted` 또는 `Fixed`를 선택합니다.
4. 가중치, 고정 크기와 필요한 최소·최대 크기를 설정합니다.
5. 부모 패널에서 Axis, Spacing, Padding과 Overflow 정책을 설정합니다.
6. 좁은 화면에서 배치 방향을 바꾸려면 Axis Policy와 Breakpoint를 설정합니다.
7. 복합 화면은 ContentLayer의 자식에 `FlexibleLayoutPanel`을 추가하여 같은 방식으로 중첩합니다.

ContentLayer의 RectTransform Offset은 콘텐츠 전체의 외부 여백이고, `FlexibleContentLayoutGroup`의 Padding은 내부 여백입니다. 두 값은 합산됩니다. 실제 표시 영역을 자르려면 루트 `FlexibleLayoutPanel`의 `Clip Content`를 활성화합니다. `Overflow = Clip`만으로는 렌더링이 잘리지 않습니다.

Inspector에서 자식 사이 간격은 `Spacing`, ContentLayer 내부 상·하·좌·우 여백은 기본 `Padding`을 사용합니다. 패널 테두리와 ContentLayer 사이의 상·하·좌·우 외부 여백까지 설정하려면 `Override Content Margins`를 활성화하고 `Content Margins`를 입력합니다. 기존에 ContentLayer RectTransform Offset을 직접 편집한 Prefab은 이 옵션을 비활성화하여 기존 값을 유지할 수 있습니다.

배경 스타일은 Project 창의 `Assets > Create > TxT RPG > UI > Flexible Layout Background Style`에서 생성한 뒤 `BackgroundLayer`의 `FlexibleLayoutBackground.Initial Style`에 연결합니다. 즉시 변경에는 `ApplyStyle`, 교차 페이드에는 `Change`를 사용합니다. 배경과 셰이더 애니메이션은 `BackgroundVisualRoot`에만 적용하고, 입력 차단이 필요하면 별도의 InputBlocker를 Content 또는 Foreground 계층에 명시적으로 추가합니다.

`Assets/TxTRPG/UI/DEMO/FlexibleLayoutPanel/FlexibleLayoutPanelDemo.prefab`을 Prefab Mode로 열면 넓은 화면의 `MainContent : Character = 7 : 3` 구성과 `Story : Action = 65 : 35` 중첩 구성을 확인할 수 있습니다. Prefab 루트의 너비를 720 미만으로 줄이면 최상위 축이 세로로 전환됩니다.

`Assets/TxTRPG/UI/DEMO/FlexibleLayoutPanel/Sample_Main_FlexibleLayoutPanelDemo.prefab`은 `SampleScene`의 주 레이아웃을 참조한 1:3:1 열 구성입니다. 왼쪽부터 ActionGridPanel, StoryTextPanel, CharacterDisplayPanel Demo Prefab을 사용합니다. 루트 너비가 720 미만이면 세 열이 세로 방향으로 전환됩니다.

같은 오브젝트에서 주축을 제어하는 `ContentSizeFitter`는 사용하지 않습니다. 레이아웃 중첩은 필요한 수준으로만 유지하고, 런타임에서 매 프레임 `Rebuild`를 호출하지 않습니다.

## 자산 재생성 메뉴

| 메뉴 | 효과 |
| --- | --- |
| `Tools > TxT RPG > Rebuild Story Text Panel Prefabs` | 운영용 메시지와 패널 프리팹을 기본 구조로 다시 생성합니다. |
| `Tools > TxT RPG > Rebuild Story Text Panel Demo` | 데모 데이터와 데모 프리팹을 기본 상태로 다시 생성합니다. |
| `Tools > TxT RPG > Refresh Story Text Panel Edit Mode Preview` | 현재 데모 데이터로 미리보기 항목을 다시 생성합니다. |
| `Tools > TxT RPG > Rebuild Character Display Panel Prefab` | 운영용 2D 캐릭터 표시 패널 프리팹을 기본 구조로 다시 생성합니다. |
| `Tools > TxT RPG > Rebuild Character Display Panel Demo` | 샘플 Sprite, 외형 정의, 데이터와 캐릭터 표시 데모 Prefab을 다시 생성합니다. |
| `Tools > TxT RPG > Rebuild Enemy Display Panel Prefab` | 운영용 적 표시 패널, 2D View Template과 풀 계층을 다시 생성합니다. |
| `Tools > TxT RPG > Rebuild Enemy Display Panel Demo` | 샘플 적 Sprite, 외형 정의, 다중 적 데이터와 Demo Prefab을 다시 생성합니다. |
| `Tools > TxT RPG > Rebuild Action Grid Prefabs` | 셀, 컨텍스트 메뉴와 ActionGridPanel 운영용 Prefab을 다시 생성합니다. |
| `Tools > TxT RPG > Rebuild Action Grid Demo` | 샘플 아이콘, 혼합 항목 데이터와 ActionGridPanel Demo를 다시 생성합니다. |
| `Tools > TxT RPG > Rebuild Flexible Layout Prefab` | 자식 없는 운영용 FlexibleLayoutPanel Prefab을 다시 생성합니다. |
| `Tools > TxT RPG > Rebuild Flexible Layout Demo` | 세 제품 UI Demo를 중첩한 반응형 Flexible Layout Demo를 다시 생성합니다. |
| `Tools > TxT RPG > Rebuild Sample Main Flexible Layout Demo` | SampleScene의 1:3:1 주 레이아웃과 세 Demo Prefab을 조합한 샘플을 다시 생성합니다. |
| `Tools > TxT RPG > Addressables > Register UI Assets` | 기존 운영용 UI Prefab과 공통 스타일을 수명 기반 Addressables 그룹에 등록합니다. |
| `Tools > TxT RPG > Addressables > Validate Settings` | 빈 주소, 대소문자 중복, 누락 GUID와 그룹 스키마를 검사합니다. |
| `Tools > TxT RPG > Addressables > Build Player Content` | 현재 프로필과 그룹 설정으로 Addressables Player Content를 빌드합니다. |

## Addressables 콘텐츠 제작

1. 게임과 저장 데이터에서 사용할 안정적인 에셋 ID를 정합니다.
2. 동시에 로드하고 해제할 수명을 기준으로 그룹을 선택합니다.
3. 캐릭터, Action 아이콘 또는 배경 Style의 Addressables ID 필드에 주소를 기록합니다.
4. 화면 진입 전에 필요한 ID를 `AssetScope`로 미리 로드합니다.
5. 새 Scope가 준비된 뒤 화면을 교체하고 이전 Scope를 Dispose합니다.
6. Player 빌드 전에 `Build Player Content` 메뉴를 실행합니다.

Editor fallback Sprite는 미리보기 용도로만 사용합니다. 새 런타임 콘텐츠에 Addressables ID 없이 직접 Sprite만 설정하지 않습니다. Prefab 생성 또는 Demo 재생성 후에는 `Register UI Assets`를 다시 실행하여 주소와 그룹을 동기화합니다.

운영용 프리팹 재생성은 수동으로 적용한 프리팹 변경을 덮어쓸 수 있습니다. 생성기 코드가 권위 있는 구조인지 확인한 뒤 실행하십시오.

데모 프리팹을 다시 생성하면 기존 미리보기 항목이 제거될 수 있습니다. 데모 재생성 후에는 Edit Mode 미리보기 새로고침 메뉴도 실행하십시오.

## 테스트

Edit Mode 테스트는 `Assets/TxTRPG/UI/Tests/Editor`에 있습니다.

| 테스트 클래스 | 검증 범위 |
| --- | --- |
| `StoryTextPanelTests` | 투명도 경계값과 운영용 프리팹 필수 참조를 검증합니다. |
| `StoryTextPanelDemoTests` | 데모 데이터의 양, 발화자 조합, 로더와 패널 연결을 검증합니다. |
| `StoryTextPanelEditModePreviewTests` | 데모 데이터 개수와 직렬화된 미리보기 항목 개수가 일치하는지 검증합니다. |
| `CharacterDisplayPanelTests` | 표시 요청의 null 정규화, 2D 패널 계층과 데모 미리보기·로더 연결을 검증합니다. |
| `EnemyDisplayPanelTests` | 적 종류와 인스턴스 식별, 반응형 포메이션, View 풀 재사용과 운영·Demo Prefab 연결을 검증합니다. |
| `ActionGridPanelTests` | 표시 모델 정규화, 열 수와 필요 높이, 세로 중앙·상단 전환, 스크롤 위치 복구, 컨텍스트 메뉴 방향 전환·경계 제한, 운영용 Prefab 경계와 혼합 항목 Demo 상태를 검증합니다. |
| `FlexibleLayoutPanelTests` | 가중치·고정 크기, 최소·최대 크기, Overflow 계산, 배경 스타일 정책과 생성된 계층형 Prefab 구조를 검증합니다. |
| `AssetManagementTests` | AssetScope의 중복 없는 Lease 해제와 Addressables 주소·그룹 등록을 검증합니다. |

관련 변경 후에는 다음 항목을 확인합니다.

1. `TxTRPG.UI`, `TxTRPG.UI.Editor`, `TxTRPG.UI.Tests.Editor` 어셈블리가 오류 없이 컴파일되는지 확인합니다.
2. 관련 Edit Mode 테스트를 실행합니다.
3. 데모 프리팹을 Prefab Mode에서 열어 최신 메시지 하단 정렬과 상단 투명도 감쇠를 확인합니다.
4. Play Mode에서 미리보기 항목이 제거되고 런타임 메시지만 생성되는지 확인합니다.
5. 스크롤바 위쪽 끝과 아래쪽 끝의 정렬을 확인합니다.
6. `Allow User Scrolling`을 끈 상태에서 스크롤바와 사용자 스크롤이 비활성화되는지 확인합니다.
7. 왼쪽·오른쪽 스크롤바와 배경 표시·숨김 조합을 확인합니다.
8. 긴 문장, 빈 발화자, 여러 줄 본문을 확인합니다.

## 변경 영향 확인표

| 변경 대상 | 함께 확인할 대상 |
| --- | --- |
| `StoryMessage` | `StoryMessageItem.Bind`, 데모 데이터, 호출자, 저장·현지화 경계 |
| `StoryMessageItem` | 메시지 프리팹 생성기, Content 레이아웃, 투명도 적용 |
| `StoryTextPanel` | 운영 프리팹, 데모 중첩 프리팹, 스크롤 동기화, 테스트 |
| 스크롤바 또는 Viewport 구조 | 생성기, 직렬화 참조, 좌우 배치, 입력 방식 |
| 데모 데이터 | 런타임 로더, Edit Mode 미리보기 재생성, 미리보기 테스트 |
| Editor 생성기 경로·메뉴 | 이 문서와 `DOCS/architecture/story-text-panel.md` |
| 어셈블리 정의 | 프로젝트 구조 문서, 플레이어 빌드 포함 여부, 테스트 참조 |
| `CharacterDisplayPanel` 또는 View | 2D Prefab, 외형 정의, 페이드 수명 주기, 향후 3D 교체 경계 |
| `EnemyDisplayPanel` 또는 Backend | InstanceId 안정성, View 풀, 포메이션, Addressables Lease와 타깃 상태 |
| `ActionGridPanel` 또는 Cell | 셀 풀, Navigation, 반응형 열 수, 컨텍스트 메뉴와 외부 명령 경계 |
| `FlexibleLayoutPanel` 또는 Item | 크기 계산, Overflow, 반응형 축, 중첩 Demo, 동일 축의 다른 Layout 컴포넌트 충돌 |

## 다국어 글꼴 확인

현재 TextMeshPro 필수 리소스의 기본 Liberation Sans는 한국어 전체 글리프를 제공하지 않습니다. 다국어 기능을 구현할 때 다음 작업이 필요합니다.

- 지원 언어의 글리프를 포함하는 TMP Font Asset을 준비합니다.
- 언어별 또는 공통 fallback 목록을 설정합니다.
- 동적 폰트 사용 시 빌드 크기, 메모리와 플랫폼별 폰트 가용성을 확인합니다.
- 한국어, 영어, 일본어와 긴 문자열 언어에서 줄바꿈과 레이아웃을 검증합니다.

## 문서 갱신 확인

구조 변경을 완료하기 전에 다음 질문을 확인합니다.

- 새 클래스나 자산의 책임이 기존 문서에서 설명되는가?
- 클래스 또는 어셈블리 의존 방향이 바뀌었는가?
- 프리팹 계층이나 직렬화 참조가 바뀌었는가?
- 생성·검증 메뉴 또는 실행 절차가 바뀌었는가?
- 현재 구현과 향후 계획이 문서에서 명확하게 구분되는가?

하나라도 해당하면 같은 변경에서 `DOCS` 문서를 갱신합니다.
