# 개발 및 검증 절차

## 이미지 메뉴와 가방 창 설정

1. `ItemDefinition`의 `Category Id`에 안정적인 ID를 입력합니다. 기본 소비품은 `consumable`이며 빈 값과 미등록 값은 `misc`로 표시합니다.
2. `InventoryWindowPage.prefab`의 `InventoryGameWindowPage`에서 `Display Mode`, `Items Per Page`, `Fill Page With Empty Slots`와 `Categories`의 ID·이름·순서를 설정합니다.
3. 런타임에서는 `SetDisplayMode`, `SelectCategory`, `GoToPage`를 사용합니다. 카테고리와 페이지를 바꾸면 열린 컨텍스트 메뉴가 먼저 닫힙니다.
4. 메뉴 이미지는 `GameMenuButtonView.SetIcon`으로 교체하고 `ConfigureDisplay`로 `ImageOnly` 또는 `ImageWithLabel`, 여백과 색상을 설정합니다. 이미지가 없으면 Label이 대체 표시됩니다.
5. Scene을 변경하지 않고 생성 자산만 갱신하려면 `Tools > TxT RPG > UI > Prefabs > Rebuild Game Menu and Modal Windows`를 실행합니다. 이 메뉴는 `GameMenuPanel.prefab`, `InventoryWindowPage.prefab`, `ModalWindowHost.prefab`과 `GameMenuScreen.prefab`을 갱신합니다.
6. 기존 `TMP_MainScene` 배치까지 이전할 때에만 Scene을 먼저 저장한 뒤 `Tools > TxT RPG > Application > Rebuild Quick Items and Game Windows`를 사용합니다. 미저장 Scene은 생성 전에 거부됩니다. 사용자 Override가 있다면 Prefab 전용 생성 후 Overrides 창에서 개별 적용합니다.

검증할 때에는 빈 카테고리, 1개, 페이지당 개수 전후, 여러 페이지, 마지막 페이지 항목 제거, 미등록 정의 ID를 확인합니다. VerticalScroll에서 수량 갱신 후 위치가 유지되는지, Paged에서 수직 스크롤이 꺼지는지, 사용이 퀵 슬롯 연결을 바꾸지 않는지도 확인합니다. 키보드·게임패드·터치와 좁은 화면의 탭 길이는 실제 목표 기기에서 별도로 확인합니다.

### TMP_MainScene 연결 및 실행

1. `TMP_MainScene`의 저장된 구성에서 기존 메뉴와 `GameWindowsOverlay`의 연결을 확인합니다. 별도의 일회성 적용 메뉴는 제공하지 않습니다.
2. Build Settings의 첫 Scene인 `Assets/Scenes/AppScene.unity`를 열고 Play Mode를 시작합니다. `AppScene`의 `PlayerSessionHost`가 저장 데이터를 준비한 뒤 `TMP_MainScene`을 로드합니다.
3. 기존 가방 버튼으로 창을 열고 Close 또는 Cancel로 닫은 뒤 포커스가 가방 버튼으로 복원되는지 확인합니다. `TMP_MainScene` 직접 Play에는 AppScene 세션이 없으므로 정식 실행 경로로 지원하지 않습니다.
4. System 버튼으로 제목과 닫기 버튼만 있는 빈 설정 모달이 열리는지 확인합니다.
5. 실제 저장 데이터가 비어 있으면 EmptyState가 정상입니다. 테스트 아이템이 필요하면 사용자 저장 파일이 아닌 격리된 테스트 세션을 사용합니다.

## ActionGridPanel 최초 스크롤 확인

1. 초기 데이터를 비동기로 적용하는 화면은 `PanelStartupController`의 `Layout Root` 안에 `ActionGridPanel`을 포함하거나, 로더에서 `BeginInitialContentSetup`과 `CompleteInitialContentSetup`을 호출합니다.
2. Play Mode에서 Capacity 또는 Entry를 여러 행이 생기도록 설정하고, 첫 프레임부터 첫 행이 상단 Padding 바로 아래에 표시되는지 확인합니다.
3. 중간으로 스크롤한 뒤 수량, 아이콘이나 동일 목록을 갱신하여 정규화 위치가 유지되는지 확인합니다.
4. Hidden, Auto, Always 스크롤바와 좁은 Viewport에서도 같은 동작인지 확인합니다. 기존 Scene이나 Prefab의 Content 좌표를 수동으로 0으로 저장하는 방법으로 문제를 우회하지 않습니다.

## 등록 기반 운영 Prefab 일괄 재생성

1. Scene과 Prefab Mode의 변경을 저장하거나 취소하고 Play Mode를 종료합니다.
2. `Tools > TxT RPG > Build > Rebuild Generated Prefabs...`를 엽니다.
3. 기본 선택된 운영 작업, 출력 경로와 제외 사유를 확인합니다. `Select All Eligible`은 등록된 적격 작업만 선택합니다.
4. `Validate Plan`으로 입력, Editor 상태와 등록 계약을 쓰기 전에 검사합니다.
5. 버전 관리 체크포인트를 만든 뒤 `Rebuild Selected`에서 출력 덮어쓰기를 확인합니다.
6. 실패하거나 취소되면 결과에 표시된 완료 작업과 실패 작업을 확인하고, 이미 저장된 출력은 버전 관리에서 개별 복구합니다.

등록 구조만 검사하려면 `Tools > TxT RPG > Build > Validate Prefab Rebuild Registry`를 사용합니다. 이 검증은 Prefab이나 Scene을 변경하지 않습니다.

새 생성기를 추가할 때에는 해당 기능의 Editor 어셈블리에 `IPrefabRebuildTaskProvider` 구현을 추가합니다. `PrefabRebuildTaskDescriptor`에 변경되지 않는 ID, 운영 또는 Demo 분류, 기본 선택 여부, 정확한 입력·출력, 선행 작업 ID, 설명, 공통 생성 메서드와 저장 후 검증을 선언합니다. Scene, 콘텐츠, Addressables나 Build Settings를 변경하거나 기존 Style 값을 초기화하는 생성기는 등록하지 않고 `PrefabRebuildExclusion`으로 이유를 남깁니다. 개별 MenuItem과 일괄 작업은 반드시 같은 생성 핵심 메서드를 호출해야 합니다.

## Game menu prefab composition

- Use `Tools > TxT RPG > UI > Prefabs > Rebuild Game Menu and Modal Windows` to rebuild `GameMenuPanel.prefab`, `ModalWindowHost.prefab`, and `GameMenuScreen.prefab` from the existing item catalog. The prefab-only command does not create or modify item content and does not modify a Scene.
- For an existing screen, retain `GameMenuPanel.prefab` and connect its shared `GameWindowService` with `BindExternalDependencies` or `GameMenuCompositionBinder`. The quick-item grid is optional.
- For a new standalone screen, place `GameMenuScreen.prefab` directly under a Canvas. Do not add it when the screen already owns a shared modal service; bind the base menu instead.
- When a Scene has unsaved changes, update the prefab source rather than rebuilding or saving the Scene. This preserves the instance parent, sibling order, RectTransform, and existing overrides.
- `QuickItemsUiProjectBuilder.ValidateSavedPrefabs` and `QuickItemsUiProjectBuilderTests` validate the base prefab's internal references and empty external references, plus the integrated binder references after save and reload.

## StoryTextPanel을 씬에서 사용하기

1. 씬에 Canvas를 준비합니다.
2. `Assets/TxTRPG/UI/Prefabs/StoryTextPanel.prefab`을 Canvas 아래에 배치합니다.
3. 앵커와 크기는 부모 화면 레이아웃 정책에 맞게 설정합니다.
4. 게임 또는 스토리 컴포넌트에서 `StoryTextPanel` 참조를 직렬화합니다.
5. 현지화가 완료된 문자열로 `StoryMessage`를 생성하여 `AddMessage`를 호출합니다.
6. 기본 배경은 `Assets/TxTRPG/UI/Styles/StoryTextPanelDefaultBackgroundStyle.asset`에서 수정합니다. 특정 화면만 다른 배경이 필요하면 별도의 `PanelBackgroundStyle`을 연결하거나 런타임 API를 사용합니다.

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

배경을 즉시 바꾸려면 `ApplyBackground`, 교차 페이드로 바꾸려면 `ChangeBackground`를 사용합니다. `ClearBackground`는 투명 상태를 유지하며, `ResetBackgroundToDefault`는 Prefab의 `initialStyle`로 돌아갑니다. 배경 전용 효과는 `SetBackgroundEffectsEnabled`, 테스트용 또는 플랫폼별 자산 공급자는 `SetBackgroundAssetProvider`로 제어합니다.

## Edit Mode에서 설정 확인하기

1. `Assets/TxTRPG/UI/DEMO/StoryTextPanelDemo.prefab`을 Prefab Mode로 엽니다.
2. 중첩된 `StoryTextPanel` 오브젝트를 선택합니다.
3. 스크롤바 위치·배경·손잡이 크기와 투명도 설정을 변경합니다.
4. Prefab 또는 Scene View에서 최신 메시지가 아래쪽에 배치된 모습을 확인합니다.

데모 데이터의 문구를 수정한 뒤 다음 메뉴를 실행하면 미리보기 항목이 다시 생성됩니다.

```text
Tools > TxT RPG > UI > Preview > Refresh Story Text Panel
```

## CharacterDisplayPanel을 씬에서 사용하기

1. `Assets/TxTRPG/UI/Prefabs/CharacterDisplayPanel.prefab`을 Canvas 아래에 배치합니다.
2. `Character2DView`의 `Appearance Definitions`에 캐릭터별 `CharacterAppearanceDefinition` 자산을 등록합니다.
3. 외형 정의의 `Framing`을 원본에 맞게 `Whole Artwork`, `Thigh Up` 또는 `Custom`으로 설정합니다.
4. View의 `Animate Visibility`, 등장·퇴장 시간과 프레임 시간 상한을 설정합니다.
5. 게임 또는 스토리 컴포넌트에서 `CharacterDisplayPanel`을 참조하고 ID 기반 `CharacterPresentation`을 전달합니다.
6. 위치 또는 크기 애니메이션은 `ArtworkRoot`가 아니라 `VisualRoot`에 적용합니다.

외형 정의 자산은 `Assets > Create > TxT RPG > UI > Character Appearance Definition`에서 생성할 수 있습니다.

캐릭터 표시 데모는 `Assets/TxTRPG/UI/DEMO/CharacterDisplayPanel/CharacterDisplayPanelDemo.prefab`을 Prefab Mode로 열어 확인합니다. Edit Mode와 Play Mode에서는 준비된 샘플 캐릭터가 즉시 완전히 표시됩니다. 캐릭터 교체 Fade를 확인하려면 `Animate Visibility`를 명시적으로 활성화합니다.

같은 Demo의 `CharacterDisplayBackgroundDemoStyle.asset`은 공통 `PanelBackgroundStyle` 제작 예시입니다. CharacterDisplayPanel의 배경은 `ApplyBackground`, `ChangeBackground`, `ClearBackground`로 캐릭터 표시와 독립적으로 제어합니다. 배경 전용 효과는 `BackgroundEffectOverlay`, 캐릭터 전용 효과는 Character2DView의 `EffectOverlay`, 전경 효과는 `ForegroundEffectLayer`, 전체 화면 전환은 `TransitionOverlay`에 적용합니다.

Story, Character, Enemy와 Action Grid Demo는 동일한 `PanelStartupController` 경로를 사용합니다. Demo 루트의 `CanvasGroup`, 구체적인 `PanelInitialDataLoader`, `FadePanelRevealTransition`과 `PanelStartupController` 참조를 함께 유지해야 합니다. `Animate Reveal` 기본값은 꺼져 있으며, Scene 시작 연출은 `AppScene`의 `SceneTransitionService`가 담당합니다. Loader를 교체할 때는 동기 `Start()`를 추가하지 않고 `LoadAndApplyAsync`가 모든 자산 준비를 기다리도록 구현합니다.

## 기본 플레이어와 운영 캐릭터 UI 구성하기

1. `Tools > TxT RPG > Content > Characters > Create Default Placeholder Content`를 실행하여 `CharacterContentCatalog.asset`과 `DefaultCharacterContent.asset`을 준비합니다.
2. 현재 열어 둔 `TMP_MainScene`에 저장하지 않은 변경이 있다면 먼저 저장합니다.
3. `Tools > TxT RPG > Application > Rebuild App Scene`을 실행합니다.
4. 생성된 `Assets/TxTRPG/Application/Configuration/DefaultNewGameProfile.asset`에서 Catalog와 Initial Character가 올바른지 확인합니다.
5. `Assets/TxTRPG/SceneTransition/Prefabs/AppRoot.prefab`의 `PlayerSessionHost`와 `Assets/Scenes/TMP_MainScene.unity`의 `ActiveCharacterDisplayBinder` 참조를 확인합니다.

재생성 과정은 `TMP_MainScene`의 캐릭터 Demo 인스턴스를 운영용 `CharacterDisplayPanel.prefab`으로 교체하고 `CharacterDisplayPresenter`와 외형 전용 `ActiveCharacterDisplayBinder`를 구성합니다. 상태 UI는 자동 생성하지 않습니다. 이미 로드된 Scene에 저장하지 않은 변경이 있으면 덮어쓰지 않고 실패합니다. 운영 Scene에는 `CharacterDisplayPanelDemoLoader`, `PanelStartupController`, `CharacterStatusPanel`, `ActiveCharacterStatusBinder` 또는 Demo 데이터가 남지 않아야 합니다.

정식 실행은 `Assets/Scenes/AppScene.unity`에서 시작합니다. 저장 파일이 없으면 `character.default`가 생성되고, 파일이 있으면 기존 `PlayerState`가 복원됩니다. 기본 저장 경로는 `Application.persistentDataPath/player-save.json`입니다. 손상된 저장 파일은 자동으로 새 게임으로 교체되지 않으므로 Console 오류와 원본 파일을 확인한 뒤 사용자에게 복구 또는 초기화 선택을 제공해야 합니다. 캐릭터 이름은 현지화 서비스가 `ICharacterNameLocalizer`로 연결되기 전까지 숨겨집니다.

## 선택적 CharacterStatusPanel 사용하기

1. `Tools > TxT RPG > UI > Prefabs > Rebuild Character Display Panel`을 실행하여 `HealthBarPanel.prefab`과 `CharacterStatusPanel.prefab`을 함께 생성합니다.
2. 상태 정보가 필요한 Scene에 `Assets/TxTRPG/UI/Prefabs/CharacterStatusPanel.prefab`을 배치합니다.
3. 같은 Scene의 적절한 오브젝트에 `ActiveCharacterStatusBinder`를 추가하고 Status Panel과 `CharacterContentCatalog.asset`을 연결합니다.
4. 이름이 필요하면 `CharacterNamePanel`을 상태 컨테이너 자식으로 추가하고 TMP Text를 연결합니다.
5. 현지화 조립 코드에서 `SetNameLocalizer`와 `SetHealthTextFormatter`를 호출합니다.

기본 CharacterStatusPanel Prefab에는 HealthBarPanel만 포함됩니다. Health Label과 Value Text는 선택적이며 참조를 제거해도 오류가 발생하지 않습니다. 공격력과 다른 일반 Stat은 이 패널에 추가하지 않고 추후 별도 CharacterStatWindow에서 표시합니다.

HealthBar의 Inspector 배치는 루트의 `HealthBarLayoutController`에서 설정합니다. Reference Area에는 일반적으로 `BackgroundLayer`, Bar Root에는 `BarRoot`를 연결합니다. 새 기본 Prefab은 Horizontal·Vertical Size Mode가 모두 `Stretch`이고 Left·Right·Top·Bottom Padding이 모두 12입니다. 이 설정은 Padding을 제외한 영역을 Slider가 모두 채우므로 Alignment를 변경해도 결과가 달라지지 않습니다.

Alignment가 필요한 제한 배치는 해당 축만 `Fixed`로 바꾸고 `Fixed Size`를 설정합니다. Fixed 값은 남은 영역보다 클 수 없으며, 호환성을 위해 Stretch로 돌아간 뒤에도 직렬화 값이 유지됩니다. Padding은 Canvas 로컬 UI 단위이고 Offset의 양수 X는 오른쪽, 양수 Y는 위쪽입니다. Offset은 Padding 계산 후 적용되므로 Padding 경계를 벗어날 수 있습니다. Slider 내부 `Fill Area`의 2단위 inset은 외부 Padding과 별개입니다.

런타임에는 다음 API를 사용합니다.

```csharp
layout.SetSizeModes(HealthBarAxisSizeMode.Stretch, HealthBarAxisSizeMode.Stretch);
layout.SetPadding(12, 12, 12, 12);

// 크기를 제한하여 Alignment가 의미를 갖게 하는 호환 모드입니다.
layout.SetSizeModes(HealthBarAxisSizeMode.Fixed, HealthBarAxisSizeMode.Fixed);
layout.SetFixedSize(280f, 24f);
layout.SetAlignment(HealthBarHorizontalAlignment.Right, HealthBarVerticalAlignment.Top);
layout.SetOffset(new Vector2(-8f, -6f));

healthBar.RegisterEffect(pulseEffect);
healthBar.SetEffectsEnabled(accessibilitySettings.EnableUiMotion);
```

효과 컴포넌트는 `HealthBarEffect`를 상속하고 `Apply`와 `Clear`를 구현합니다. 전체 바 효과는 `BarVisualRoot`, Fill 전용 효과는 `FillVisualRoot`에 적용하고 레이아웃 전용 `BarRoot`는 변경하지 않습니다. 파생 컴포넌트가 `OnEnable` 또는 `OnDisable`을 재정의한다면 기반 구현을 호출해야 자동 재동기화와 정리가 유지됩니다.

기존 HealthBar 자산은 먼저 Prefab 또는 Scene 인스턴스를 선택한 뒤 `Tools > TxT RPG > UI > Prefabs > Upgrade Selected Health Bars`를 실행합니다. 이 작업은 누락된 Layout Controller와 시각 루트 참조만 보완하며 기존 Slider 계층, Animator 경로, 이미 지정된 참조와 레이아웃 설정을 바꾸지 않습니다. Scene 변경은 자동 저장하지 않으며 Undo를 지원합니다.

기존 Fixed 크기를 Padding 기반 양축 자동 크기로 명시적으로 전환하려면 같은 대상을 선택하고 `Tools > TxT RPG > UI > Prefabs > Convert Selected Health Bars to Padding Sizing`을 실행합니다. 전환은 두 Size Mode만 Stretch로 바꾸며 기존 Padding, Alignment, Offset과 비활성화된 Fixed Size 값은 보존합니다. 따라서 기존 자산의 Top·Bottom Padding이 0이었다면 전환 전후에 개발자가 원하는 값, 새 기본값을 따르려면 각각 12로 설정해야 합니다. 두 메뉴는 반복 실행할 수 있고 Scene 인스턴스에서는 Undo와 Prefab Override를 유지합니다.

기존 계층에 전용 시각 루트가 없으면 Upgrade 메뉴는 호환 가능한 기존 Transform을 참조합니다. 별도 효과 계층이 필요할 때에는 새 운영 Prefab 구조를 기준으로 수동 이전한 뒤 Animator 경로를 함께 갱신합니다. 운영 기본 구조를 처음부터 다시 만들려면 `Rebuild Character Display Panel`을 사용할 수 있지만, 이 메뉴는 대상 Prefab을 재생성하므로 사용자 정의 Prefab에는 Upgrade와 Convert 메뉴를 우선 사용합니다.

`Tools > TxT RPG > UI > Demos > Rebuild Health Bar Panel Demo`는 `Assets/TxTRPG/UI/DEMO/CharacterStatusPanel/HealthBarPanelDemo.prefab`을 생성합니다. 이 데모는 9가지 Fixed 정렬 조합과 런타임 정렬·피해 pulse 예제를 포함합니다.

## Scene 전환 사용하기

### Editor에서 AppScene 경유로 Play하기

프로젝트 기본값에서는 `TMP_MainScene`이나 다른 콘텐츠 Scene을 열어 둔 상태에서 Play해도 `Assets/Scenes/AppScene.unity`가 먼저 실행됩니다. AppScene의 기존 초기화가 완료되면 `AppSceneRoot.Initial Content Scene Path`에 지정된 `Assets/Scenes/TMP_MainScene.unity`로 진입합니다. Play 종료 후에는 Play 전의 편집 Scene 구성과 활성 Scene으로 돌아옵니다.

설정은 `Edit > Project Settings > TxT RPG > Play Mode Start`에서 관리합니다. `Always start Play Mode through AppScene`을 끄면 현재 Scene을 직접 실행할 수 있습니다. 다시 켜거나 `Reapply AppScene Start Policy`를 누르면 AppScene을 시작 Scene으로 지정합니다. 기존에 다른 Play 시작 Scene이 있었다면 명시적 적용 시 백업하며, 정책을 끌 때 정책이 아직 AppScene을 소유하고 있는 경우에만 복원합니다.

미저장 Scene이 있으면 Play 진입 전에 Unity의 저장 확인 창이 표시됩니다. 저장을 취소하면 Play도 취소되며 편집 내용은 유지됩니다. Prefab Mode의 저장되지 않은 변경은 자동 저장하지 않고 Play를 중단하므로, Prefab을 저장한 뒤 다시 실행합니다. Play Mode 테스트는 Unity Test Runner가 지정한 테스트 Scene을 사용할 수 있도록 이 정책을 우회합니다.

정책이 적용되지 않거나 AppScene 구성에 오류가 있으면 현재 Scene을 대신 실행하지 않고 Play를 중단합니다. 다음 항목을 확인합니다.

- `Assets/Scenes/AppScene.unity`가 Build Settings의 첫 번째 활성 Scene인지 확인합니다.
- `Assets/TxTRPG/SceneTransition/Prefabs/AppRoot.prefab`의 `Load Initial Content On Start`가 활성화되어 있는지 확인합니다.
- 최초 콘텐츠 경로가 `Assets/Scenes/TMP_MainScene.unity`인지 확인합니다.
- Project Settings의 경고를 확인하고 필요한 경우 정책을 다시 적용합니다.

Enter Play Mode Options는 프로젝트의 기존 설정을 변경하지 않습니다. 현재 기본 구성처럼 Domain Reload와 Scene Reload가 모두 활성화된 조합을 기준으로 검증하며, 다른 조합을 사용한다면 정적 서비스와 이벤트 구독의 반복 Play 동작을 별도로 확인합니다.

1. 현재 작업 Scene을 저장한 뒤 `Tools > TxT RPG > Application > Rebuild App Scene`을 실행합니다. 이 메뉴는 기본 Scene Transition Profile, `DefaultNewGameProfile.asset`, PlayerSessionHost가 포함된 `AppRoot.prefab`, 운영 캐릭터 UI가 연결된 최초 콘텐츠 Scene과 `AppScene.unity`를 생성하고 AppScene을 Build Settings의 0번으로 등록합니다.
2. `AppRoot`의 `AppSceneRoot.Initial Content Scene Path` 목록에서 최초로 로드할 Scene을 선택합니다. 목록에는 활성화된 Build Settings Scene이 `Scene 이름 (Assets/.../*.unity)` 형식으로 표시되며 AppScene은 제외됩니다. 기본값은 `Assets/Scenes/TMP_MainScene.unity`입니다.
3. 이후 전환에는 `AppSceneRoot.Instance.SceneTransitions.LoadContentSceneAsync(scenePath, profile, cancellationToken)`을 호출합니다. `scenePath`에는 Build Settings에 등록된 전체 프로젝트 경로를 전달하고, 콘텐츠 Scene에서 Unity의 `LoadScene`을 직접 호출하지 않습니다.
4. 서비스가 시작해야 하는 초기화는 `ISceneInitializer`로 구현하고, Addressables·세이브·현지화처럼 의존성이 있는 작업은 `InitializationOrder`를 지정합니다.
5. 이미 다른 관리자가 시작한 비동기 작업은 `ISceneReadySource` 또는 `SceneReadySignal`로 노출하고, 필수 자산·카메라·초기 포커스가 모두 준비된 뒤 `MarkReady()`를 호출합니다.
6. 접근성 설정이 모션 감소를 요청하면 `SceneTransitionService.ReduceMotion`을 활성화합니다.
7. 호출자는 로드 예외를 처리하고 `TransitionFailed` 또는 `ErrorFallback`을 이용하여 재시도 경로를 제공합니다.

`SceneReadySignal.Mark Ready On Start`가 켜져 있으면 Start 시점에 자동으로 준비됩니다. 비동기 초기화가 있는 제품 Scene에서는 이 옵션을 끄고 외부 초기화 완료 시 명시적으로 호출합니다. 준비 참여자가 없는 Scene도 첫 프레임과 Canvas 레이아웃 갱신 후 준비된 것으로 처리합니다.

정식 실행과 Play Mode 검증은 `Assets/Scenes/AppScene.unity`에서 시작합니다. Content Scene만 직접 실행하면 AppScene과 전환 서비스가 존재하지 않으며, 이는 숨겨진 런타임 Bootstrap을 다시 만들지 않기 위한 의도된 제약입니다. 초기 콘텐츠를 바꿔 확인하려면 AppRoot Prefab의 `Initial Content Scene Path` 목록을 변경합니다. 목록에 Scene이 없다면 먼저 `File > Build Settings`에서 해당 Scene을 추가하고 활성화합니다. `<Invalid>` 경고가 표시되면 값을 자동 교체하지 않으므로 올바른 항목을 다시 선택해야 합니다.

Player 빌드 전에 `Tools > TxT RPG > Application > Validate App Scene Configuration`을 실행하면 빌드 전처리와 동일한 검증을 수동으로 수행할 수 있습니다. Scene 자산 이동이나 이름 변경, Build Settings 변경 뒤에는 이 검증을 실행합니다. `Tools > TxT RPG > Application > Rebuild App Scene`은 유효한 기존 전체 경로와 `Load Initial Content On Start` 값을 보존하며, 기존 Scene 이름은 유일하게 해석할 수 있을 때에만 전체 경로로 마이그레이션합니다.

기본 `Scene Swap Mode = Load Then Unload`는 실패 시 이전 Scene 복구를 우선합니다. 모바일 메모리 한계 때문에 `Unload Before Load`를 선택한 경우에는 이전 화면 복구가 불가능할 수 있으므로 `ErrorFallback`과 재시도 흐름을 반드시 확인합니다.

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

ActionGridPanel 전체를 기준으로 슬롯 중심을 유지하려면 `Scroll View`의 외부 Left와 Right offset도 같은 값으로 설정해야 합니다. Builder와 운영 Prefab의 기본값은 각각 18px입니다. Handle을 더 얇게 보이게 하는 `Sliding Area`의 수평 inset은 시각 설정이므로, Opposite 영역이나 외부 여백을 Handle 너비에 맞추지 않습니다. Builder를 수정한 뒤에는 `Tools > TxT RPG > UI > Prefabs > Rebuild Action Grid`를 실행하여 운영 Prefab과 이를 상속하는 Demo 구성을 갱신합니다.

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

빈 배분 공간이 필요하면 `Assets/TxTRPG/UI/Prefabs/FlexibleLayoutPlaceholder.prefab`을 `ContentLayer` 또는 `BackgroundContentLayer` 아래에 배치하고 `FlexibleLayoutItem`의 Weight, Fixed Size, Minimum Size와 Maximum Size를 설정합니다. Placeholder에는 그래픽이나 입력 차단 컴포넌트가 없습니다. `Include Inactive Children`가 꺼져 있을 때에도 공간을 유지하려면 Placeholder를 활성 상태로 둡니다.

배경과 기존 콘텐츠 사이에 별도 표현을 배치하려면 `BackgroundContentLayer`의 직계 자식으로 추가합니다. 기본 `Use Content Layout Settings`가 켜져 있으면 기존 Content 설정과 최종 Rect를 공유하지만, 자식 배분 계산은 독립적으로 실행됩니다. 다른 축, Breakpoint, Spacing, Padding 또는 Offset이 필요하면 공유 옵션을 끄고 Background Content 설정을 편집합니다. 레이아웃 항목의 애니메이션은 항목 아래에 `VisualRoot`를 만들고 그 자식에 적용합니다.

런타임에서는 기존 `Add(child, weight)`가 앞쪽 ContentLayer를 계속 사용합니다. 뒤쪽 레이어에는 `Add(child, FlexibleLayoutContentLayer.BackgroundContent, weight)`를 사용합니다. 입력을 허용하려면 `SetBackgroundInteraction(true, true)`를 호출한 뒤 해당 Selectable의 Navigation을 구성합니다. 앞쪽 Graphic이 Raycast를 받는 영역에서는 뒤쪽 Graphic까지 자동으로 입력이 전달되지 않습니다.

ContentLayer의 RectTransform Offset은 콘텐츠 전체의 외부 여백이고, `FlexibleContentLayoutGroup`의 Padding은 내부 여백입니다. 두 값은 합산됩니다. 실제 표시 영역을 자르려면 루트 `FlexibleLayoutPanel`의 `Clip Content`를 활성화합니다. `Overflow = Clip`만으로는 렌더링이 잘리지 않습니다.

Inspector에서 자식 사이 간격은 `Spacing`, ContentLayer 내부 상·하·좌·우 여백은 기본 `Padding`을 사용합니다. 패널 테두리와 ContentLayer 사이의 상·하·좌·우 외부 여백까지 설정하려면 `Override Content Margins`를 활성화하고 `Content Margins`를 입력합니다. 기존에 ContentLayer RectTransform Offset을 직접 편집한 Prefab은 이 옵션을 비활성화하여 기존 값을 유지할 수 있습니다.

배경 스타일은 Project 창의 `Assets > Create > TxT RPG > UI > Flexible Layout Background Style`에서 생성한 뒤 `BackgroundLayer`의 `FlexibleLayoutBackground.Initial Style`에 연결합니다. 즉시 변경에는 `ApplyStyle`, 교차 페이드에는 `Change`를 사용합니다. 배경과 셰이더 애니메이션은 `BackgroundVisualRoot`에만 적용하고, 입력 차단이 필요하면 별도의 InputBlocker를 Content 또는 Foreground 계층에 명시적으로 추가합니다.

`Assets/TxTRPG/UI/DEMO/FlexibleLayoutPanel/FlexibleLayoutPanelDemo.prefab`을 Prefab Mode로 열면 넓은 화면의 `MainContent : Character = 7 : 3` 구성과 `Story : Action = 65 : 35` 중첩 구성을 확인할 수 있습니다. Prefab 루트의 너비를 720 미만으로 줄이면 최상위 축이 세로로 전환됩니다.

`Assets/TxTRPG/UI/DEMO/FlexibleLayoutPanel/Sample_Main_FlexibleLayoutPanelDemo.prefab`은 `SampleScene`의 주 레이아웃을 참조한 1:3:1 열 구성입니다. 왼쪽부터 ActionGridPanel, StoryTextPanel, CharacterDisplayPanel Demo Prefab을 사용합니다. 루트 너비가 720 미만이면 세 열이 세로 방향으로 전환됩니다.

같은 오브젝트에서 주축을 제어하는 `ContentSizeFitter`는 사용하지 않습니다. 레이아웃 중첩은 필요한 수준으로만 유지하고, 런타임에서 매 프레임 `Rebuild`를 호출하지 않습니다.

## 자산 재생성 메뉴

| 메뉴 | 효과 |
| --- | --- |
| `Tools > TxT RPG > UI > Prefabs > Rebuild Story Text Panel` | 운영용 메시지와 패널 프리팹을 기본 구조로 다시 생성합니다. |
| `Tools > TxT RPG > UI > Demos > Rebuild Story Text Panel Demo` | 데모 데이터와 데모 프리팹을 기본 상태로 다시 생성합니다. |
| `Tools > TxT RPG > UI > Preview > Refresh Story Text Panel` | 현재 데모 데이터로 미리보기 항목을 다시 생성합니다. |
| `Tools > TxT RPG > UI > Prefabs > Rebuild Character Display Panel` | 운영용 2D 캐릭터 표시 패널과 HealthBar·CharacterStatus 프리팹을 기본 구조로 다시 생성합니다. |
| `Tools > TxT RPG > UI > Prefabs > Upgrade Selected Health Bars` | 선택한 기존 Prefab 또는 Scene 인스턴스의 누락된 HealthBar 레이아웃·시각 루트 참조를 비파괴적으로 보완합니다. |
| `Tools > TxT RPG > UI > Prefabs > Convert Selected Health Bars to Padding Sizing` | 기존 설정을 보존하면서 선택한 HealthBar의 양 축을 Padding 기반 Stretch 크기로 전환합니다. |
| `Tools > TxT RPG > UI > Demos > Rebuild Character Display Panel Demo` | 샘플 Sprite, 외형 정의, 데이터와 캐릭터 표시 데모 Prefab을 다시 생성합니다. |
| `Tools > TxT RPG > UI > Demos > Rebuild Health Bar Panel Demo` | 9가지 정렬과 런타임 변경·피해 pulse를 보여 주는 HealthBar 데모 Prefab을 다시 생성합니다. |
| `Tools > TxT RPG > UI > Prefabs > Rebuild Enemy Display Panel` | 운영용 적 표시 패널, 2D View Template과 풀 계층을 다시 생성합니다. |
| `Tools > TxT RPG > UI > Demos > Rebuild Enemy Display Panel Demo` | 샘플 적 Sprite, 외형 정의, 다중 적 데이터와 Demo Prefab을 다시 생성합니다. |
| `Tools > TxT RPG > UI > Prefabs > Rebuild Action Grid` | 셀, 컨텍스트 메뉴와 ActionGridPanel 운영용 Prefab을 다시 생성합니다. |
| `Tools > TxT RPG > UI > Demos > Rebuild Action Grid Demo` | 샘플 아이콘, 혼합 항목 데이터와 ActionGridPanel Demo를 다시 생성합니다. |
| `Tools > TxT RPG > UI > Prefabs > Rebuild Flexible Layout` | 자식 없는 운영용 FlexibleLayoutPanel Prefab을 다시 생성합니다. |
| `Tools > TxT RPG > UI > Prefabs > Upgrade Selected Flexible Layouts` | 선택한 기존 Prefab 또는 씬 인스턴스에 BackgroundContentLayer와 참조를 중복 없이 추가합니다. |
| `Tools > TxT RPG > UI > Demos > Rebuild Flexible Layout Demo` | 세 제품 UI Demo를 중첩한 반응형 Flexible Layout Demo를 다시 생성합니다. |
| `Tools > TxT RPG > UI > Demos > Rebuild Sample Main Layout Demo` | SampleScene의 1:3:1 주 레이아웃과 세 Demo Prefab을 조합한 샘플을 다시 생성합니다. |
| `Tools > TxT RPG > Addressables > Register UI Assets` | 기존 운영용 UI Prefab과 공통 스타일을 수명 기반 Addressables 그룹에 등록합니다. |
| `Tools > TxT RPG > Addressables > Validate Settings` | 빈 주소, 대소문자 중복, 누락 GUID와 그룹 스키마를 검사합니다. |
| `Tools > TxT RPG > Addressables > Build Player Content` | 현재 프로필과 그룹 설정으로 Addressables Player Content를 빌드합니다. |
| `Tools > TxT RPG > Application > Rebuild App Scene` | 기본 Scene Transition Profile, AppRoot Prefab, AppScene과 Build Settings 시작 순서를 다시 생성합니다. |
| `Tools > TxT RPG > Application > Validate App Scene Configuration` | AppScene 시작 순서, 전체 Scene 경로, 활성화 상태와 중복 등록을 Player 빌드 전에 검사합니다. |

## Addressables 콘텐츠 제작

1. 게임과 저장 데이터에서 사용할 안정적인 에셋 ID를 정합니다.
2. 동시에 로드하고 해제할 수명을 기준으로 그룹을 선택합니다.
3. 캐릭터, Action 아이콘 또는 배경 Style의 Addressables ID 필드에 주소를 기록합니다.
4. 화면 진입 전에 필요한 ID를 `AssetScope`로 미리 로드합니다.
5. 새 Scope가 준비된 뒤 화면을 교체하고 이전 Scope를 Dispose합니다.
6. Player 빌드 전에 `Build Player Content` 메뉴를 실행합니다.

Editor fallback Sprite는 미리보기 용도로만 사용합니다. 새 런타임 콘텐츠에 Addressables ID 없이 직접 Sprite만 설정하지 않습니다. 배경 Style에는 Addressables 로딩 전에도 표시할 수 있도록 투명하지 않은 Tint 또는 경량 fallback Sprite를 설정합니다. Panel마다 Shader 속성 값을 변경할 Effect Material은 `Effect Material Mode = Instance`를 사용합니다. Prefab 생성 또는 Demo 재생성 후에는 `Register UI Assets`를 다시 실행하여 주소와 그룹을 동기화합니다.

운영용 프리팹 재생성은 수동으로 적용한 프리팹 변경을 덮어쓸 수 있습니다. 생성기 코드가 권위 있는 구조인지 확인한 뒤 실행하십시오. 기존 자산에는 재생성 메뉴 대신 다음 업그레이드 절차를 사용합니다.

1. Project 창에서 기존 FlexibleLayout Prefab을 선택하거나 Hierarchy에서 업그레이드할 인스턴스 루트를 선택합니다.
2. `Tools > TxT RPG > UI > Prefabs > Upgrade Selected Flexible Layouts`를 실행합니다.
3. `BackgroundLayer`, `BackgroundContentLayer`, `ContentLayer`, `ForegroundLayer` 순서와 새 참조를 확인합니다.
4. 씬 인스턴스 변경은 자동 저장되지 않습니다. 결과를 확인한 뒤 직접 저장하거나 Undo로 되돌립니다.

명령은 기존 ContentLayer 자식, Offset, 참조와 Prefab 연결을 유지하며 Prefab을 Unpack하지 않습니다. 같은 대상에 반복 실행해도 레이어를 중복 생성하지 않습니다. Prefab 자산을 선택한 경우에는 해당 자산만 저장합니다.

데모 프리팹을 다시 생성하면 기존 미리보기 항목이 제거될 수 있습니다. 데모 재생성 후에는 Edit Mode 미리보기 새로고침 메뉴도 실행하십시오.

## TMP_MainScene 기본 표시와 Preview

1. `Tools > TxT RPG > Application > Configure Main Scene Default Content`를 실행합니다.
2. 생성기는 기존 레이아웃 계층과 RectTransform을 유지하고 운영 Profile, 로컬 대체 Style, 여섯 Preview Profile과 Preview Harness Prefab을 갱신합니다.
3. 운영 Scene의 `EnemyDisplayPanelDemoLoader`를 제거하고 같은 패널 인스턴스를 `MainScenePresentationController`에 연결합니다.
4. Preview가 필요하면 `MainScenePreviewHarness.prefab`을 임시로 배치하고 Target Controller와 원하는 `Preview/*.asset`을 연결한 뒤 `Run On Enable`을 켭니다. 검증 후 Harness 인스턴스를 제거합니다.
5. Preview는 일반 저장 파일과 `PlayerSessionHost`를 사용하지 않으며 아이템이나 적 상태를 운영 저장에 기록하지 않습니다.

오류 검증에는 `MissingConfiguration`, `ReadyEmpty`, `PartialAssetFailure`, `DelayedRecovery`, `InitializationFailure` Profile을 각각 사용합니다. `ReadyEmpty`에서는 안내 Story나 샘플 적이 나타나면 안 됩니다. `DelayedRecovery`에서는 Loading 중 가짜 상호작용을 허용하지 않고 지연 후 최신 요청만 반영되는지 확인합니다.

기존 자산을 이전하려면 Scene을 저장한 뒤 위 메뉴를 실행합니다. 생성기는 Panel Prefab을 재생성하거나 Unpack하지 않습니다. 적용 후 `Main_FlexibleLayoutPanel`의 Prefab Override와 자식 순서를 비교하고, `EnemyDisplayPanelContainer`에 Demo Loader가 남아 있지 않은지 확인합니다.
## 테스트

Edit Mode 테스트는 `Assets/TxTRPG/UI/Tests/Editor`, `Assets/TxTRPG/Application/Tests/Editor`, `Assets/TxTRPG/SceneTransition/Tests/Editor`와 `Assets/TxTRPG/Editor/Tests/Editor`에 있습니다. `EditorMenuTests`는 등록된 Editor 메뉴의 전체 경로, 우선순위와 중복 여부를 검증합니다.
캐릭터 Gameplay 도메인 테스트는 `Assets/TxTRPG/Gameplay/Tests/Editor`에 있습니다.

- `CharacterDomainTests`는 Stat, Health, 피해 계산과 단일 캐릭터 저장 호환성을 검증합니다.
- `PlayerStateTests`는 Definition ID와 Instance ID 분리, 활성 캐릭터 변경, 제거 규칙, 복수 캐릭터 저장과 기존 단일 저장 변환을 검증합니다.

## 캐릭터 콘텐츠 제작과 검사

1. 프로젝트에 저장된 `CharacterContentCatalog`와 기본 Sprite를 준비합니다.
2. `Tools > TxT RPG > Content > Characters > Open Authoring`을 엽니다.
3. 공백 없는 Definition ID, 유효한 파일명, `Assets/` 아래 출력 폴더, 공격력과 최대 체력을 입력합니다.
4. Default Sprite, Framing Preset, 추가 배율과 Pixel Offset을 설정합니다.
5. Visual State Policy에서 Critical과 Injured 체력 비율을 설정합니다.
6. `Add Standard Health States`로 `normal`, `injured`, `critical`, `defeated` 행을 추가하고 각 Sprite·프레이밍·주소를 설정합니다. 필요하면 Appearance·Pose·Expression 조건도 지정합니다.
7. 카탈로그를 선택하고 기본 Sprite Address, Addressables Group과 등록 여부를 설정합니다.
8. Health Preview와 Preview State로 fallback 및 상태별 Sprite를 확인합니다.
9. Validation 영역에 오류가 없고 생성될 네 경로가 올바른지 확인합니다.
10. `Create Character Content`를 누릅니다.
11. 생성된 Content Definition이 선택되면 `Tools > TxT RPG > Content > Characters > Validate All`을 실행합니다.

도구는 `<AssetName>Gameplay.asset`, `<AssetName>Appearance.asset`, `<AssetName>VisualStatePolicy.asset`, `<AssetName>Content.asset`을 생성하고 서로 연결합니다. 명시적으로 추가한 Variant에는 Sprite가 필요하지만, 추가하지 않은 상태는 Default Sprite로 대체됩니다. 기존 경로나 Definition ID, 모호한 Variant 또는 Addressables 주소가 충돌하면 쓰기 전에 중단합니다. 생성 도중 실패하면 이번 실행에서 생성한 에셋과 폴더, 카탈로그 항목과 모든 새 Addressables 엔트리만 역순으로 복구합니다.

`Register Addressables`를 끄면 Sprite Address에는 이미 사용할 수 있는 런타임 주소를 직접 입력해야 합니다. 켜면 입력 주소가 Sprite 에셋 주소가 되고 실제 하위 Sprite 로드 주소는 `<주소>[<Sprite 이름>]`으로 Appearance Definition에 저장됩니다.

Sprite Sheet의 여러 상태 Sprite를 선택했다면 모든 행에 같은 Texture base address를 입력합니다. Factory는 Texture GUID를 한 번만 등록하고 각 Variant에 `baseAddress[SpriteName]`을 저장합니다. 서로 다른 Texture에는 각각 다른 base address를 지정합니다.

런타임에서 체력 상태를 자동 반영하려면 `CharacterDisplayPresenter.Bind(runtimeState, contentDefinition)`을 호출합니다. 일시적인 피격·회복 상태는 `SetTemporaryVisualState`로 우선 적용하고 연출이 끝나면 `ClearTemporaryVisualState`를 호출합니다.

기본 캐릭터 원화가 준비되지 않은 초기 프로젝트에서는 `Tools > TxT RPG > Content > Characters > Create Default Placeholder Content`를 실행합니다. 이 메뉴는 Demo 에셋과 독립된 `DefaultCharacterPlaceholder.asset`, 네 캐릭터 정의 에셋과 `CharacterContentCatalog.asset`을 생성하고 `character.default`를 등록합니다. 네 체력 상태는 처음에는 같은 Placeholder Sprite를 사용합니다. 실제 원화를 준비한 뒤 `DefaultCharacterAppearance.asset`의 상태별 Variant만 교체합니다.

검사기는 누락된 Gameplay·Appearance 참조, 빈 Definition ID, Gameplay와 Appearance ID 불일치, 상태 임계값, Variant 모호성, 모든 Variant와 기본 외형의 Addressable artwork ID 누락 및 프로젝트 전체 Definition ID 중복을 Console에 보고합니다. 런타임 Content 테스트는 `Assets/TxTRPG/Content/Tests/Editor/CharacterContentTests.cs`에 있으며, 생성·재임포트·Sprite Sheet 등록·덮어쓰기 방지와 다중 Addressables 실패 복구 테스트는 `CharacterContentAssetFactoryTests.cs`에 있습니다. 빠른 상태 변경 중 비동기 Sprite 로드 취소는 `Assets/TxTRPG/UI/Tests/PlayMode/Character2DViewPlayModeTests.cs`에서 검증합니다.

| 테스트 클래스 | 검증 범위 |
| --- | --- |
| `StoryTextPanelTests` | 투명도 경계값, 운영용 프리팹 필수 참조, 기본 배경 Reset과 Effect Material 인스턴스를 검증합니다. |
| `StoryTextPanelDemoTests` | 데모 데이터의 양, 발화자 조합, 로더·패널·기본 배경 연결을 검증합니다. |
| `StoryTextPanelEditModePreviewTests` | 데모 데이터 개수와 직렬화된 미리보기 항목 개수가 일치하는지 검증합니다. |
| `CharacterDisplayPanelTests` | 표시 요청, 2D 패널 계층, 조합형 상태 UI, 비상호작용 Health Slider, 선택적 참조와 데모 연결을 검증합니다. |
| `HealthBarPanelTests` | Padding 크기 표, 초과 Padding 비례 보정, 9가지 제한 정렬, 효과 수명, 이전 멱등성, 생성 Prefab과 Demo 계약을 검증합니다. |
| `HealthBarPanelPlayModeTests` | 실제 프레임에서 피해 pulse 취소·원상 복구와 루트 크기 변경에 따른 양축 Stretch 재배치를 검증합니다. |
| `EnemyDisplayPanelTests` | 적 종류와 인스턴스 식별, 반응형 포메이션, View 풀 재사용과 운영·Demo Prefab 연결을 검증합니다. |
| `ActionGridPanelTests` | 표시 모델 정규화, 열 수와 필요 높이, 세로 중앙·상단 전환, 스크롤 위치 복구, 컨텍스트 메뉴 방향 전환·경계 제한, 운영용 Prefab 경계와 혼합 항목 Demo 상태를 검증합니다. |
| `FlexibleLayoutPanelTests` | 가중치·고정 크기, Placeholder, 독립 레이어 배분, 공유·독립 설정 복원, 구형 API 호환성, 업그레이드 반복 실행과 생성된 계층형 Prefab 구조를 검증합니다. |
| `AssetManagementTests` | AssetScope의 중복 없는 Lease 해제와 Addressables 주소·그룹 등록을 검증합니다. |
| `SceneTransitionTests` | AppScene 구조와 빌드 순서, Scene 경로 선택·마이그레이션·중복 이름 구분, Additive 로드, 초기 가림, Initializer 순서, 커밋 전 롤백, 커밋 후 대상 Scene 보존과 실패·취소 시 화면·입력 복구를 검증합니다. |
| `CharacterDomainTests` | 기본 스탯 생성과 검증, 피해·회복 제한, 전투 불능 이벤트, 최대 체력 보정, 저장 Round Trip, 구버전 마이그레이션과 누락 ID 처리를 검증합니다. |
| `PlayerSessionTests` | 기본 Profile, 새 게임 생성, 저장 복원, 손상 파일 보존, AppRoot Host와 운영 Scene Binder 계약을 검증합니다. |

관련 변경 후에는 다음 항목을 확인합니다.

1. `TxTRPG.UI`, `TxTRPG.UI.Editor`, `TxTRPG.UI.Tests.Editor`, `TxTRPG.SceneTransition`, `TxTRPG.SceneTransition.Editor`, `TxTRPG.SceneTransition.Tests.Editor` 어셈블리가 오류 없이 컴파일되는지 확인합니다.
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

TextMeshPro 기본 Liberation Sans는 한국어 글리프를 제공하지 않으므로 Noto Sans KR 전역 fallback을 사용합니다. 기본 영문 폰트와 기존 Prefab의 폰트 참조는 교체하지 않습니다.

1. 컴파일이 끝나고 Play Mode가 아닌지 확인합니다.
2. `Tools > TxT RPG > UI > Fonts > Apply Korean Font Fallback`을 실행합니다. 이 메뉴는 반복 실행해도 fallback을 중복 등록하지 않으며 자산을 자동 저장합니다.
3. `Assets/TextMesh Pro/Resources/TMP Settings.asset`의 기본 폰트가 `LiberationSans SDF`인지, fallback 목록에 `NotoSansKR Fallback`이 있는지 확인합니다.
4. Edit Mode에서 `KoreanFontFallbackTests`를 실행합니다.
5. `Assets/Scenes/TMP_MainScene.unity`을 열고 AppScene 실행 정책을 통해 Play Mode를 시작합니다. 주사위 굴림, Story 메시지, `가방`과 `설정` 모달에서 한국어와 영문이 함께 표시되는지 확인합니다.
6. 배포 대상 Player를 빌드하여 Missing glyph 경고, 글자 대체, 줄바꿈과 레이아웃을 확인합니다.

원본 폰트와 라이선스, atlas 정책 및 지원 범위는 `DOCS/architecture/korean-font-fallback.md`에 기록합니다. 동적 atlas의 메모리와 최초 생성 비용은 데스크톱과 모바일 실제 기기에서 각각 측정해야 합니다.

## 문서 갱신 확인

구조 변경을 완료하기 전에 다음 질문을 확인합니다.

- 새 클래스나 자산의 책임이 기존 문서에서 설명되는가?
- 클래스 또는 어셈블리 의존 방향이 바뀌었는가?
- 프리팹 계층이나 직렬화 참조가 바뀌었는가?
- 생성·검증 메뉴 또는 실행 절차가 바뀌었는가?
- 현재 구현과 향후 계획이 문서에서 명확하게 구분되는가?

하나라도 해당하면 같은 변경에서 `DOCS` 문서를 갱신합니다.

## 퀵 아이템과 게임 창 생성

운영 메뉴는 `Assets/Scenes/AppScene.unity`에서 시작하여 플레이어 세션이 준비된 경로로 검증합니다. `Assets/Scenes/TMP_MainScene.unity`에는 기존 `GameMenuPanel` 하나와 같은 Canvas 최상위의 `GameWindowsOverlay` 하나가 있어야 합니다. Overlay의 `GameWindowService`에는 `inventory`, `system`, `status` 페이지가 각각 한 번만 등록되어야 하며, 메뉴의 외부 서비스 참조는 이 서비스로 연결합니다.

가방 버튼이 비활성화되어 있으면 `GameMenuPanel.WindowService`와 서비스의 `inventory` 등록부터 확인합니다. 버튼을 강제로 활성화하면 연결 오류가 가려지므로 사용하지 않습니다. System 버튼은 같은 서비스의 빈 `system` 페이지를 열며, 제목과 닫기 버튼만 보이는 상태가 현재 정상 동작입니다. 닫기 버튼이나 Cancel 입력을 사용하면 창을 열었던 메뉴 버튼으로 포커스가 복원됩니다.

Scene 연결을 복구할 때에는 기존 메뉴의 Transform, 형제 순서, 이미지와 Prefab Override를 유지하고 Overlay 및 외부 참조만 저장합니다. 전체 Scene 생성은 기존 배치를 교체할 수 있으므로 연결 복구의 기본 절차로 사용하지 않습니다.

1. Scene 배치까지 새로 구성하려면 먼저 `TMP_MainScene`을 저장한 다음 `Tools > TxT RPG > Application > Rebuild Quick Items and Game Windows`를 실행합니다. Scene에 미저장 변경이 있으면 생성기는 어떤 자산도 변경하기 전에 중단합니다.
2. Scene을 변경하지 않고 운영 Prefab만 갱신하려면 `Tools > TxT RPG > UI > Prefabs > Rebuild Game Menu and Modal Windows`를 실행합니다.
3. 전체 생성기는 기존 `ActionGridPanel`을 유지하고 바로 아래에 `GameMenuPanel`을 배치하며, Canvas 최상위에 모달 호스트를 배치합니다.
4. 기존 Scene 인스턴스의 부모, 형제 순서, `FlexibleLayoutItem`, 창 서비스 연결과 Prefab Override를 보존해야 한다면 전체 생성기를 실행하지 말고 먼저 Prefab 전용 메뉴를 실행한 뒤 인스턴스의 Override를 검토합니다.

개발자는 `ItemCatalog`에 `ItemDefinition`을 추가하고 게임 획득 시점에 `PlayerState.Inventory.Add`를 호출하여 실제 수량을 반영합니다. `QuickItemService.TryRegister`, `TryUnregister`, `UseAsync`를 사용하면 마우스·터치·키보드·게임패드 UI가 같은 규칙을 사용합니다. 기본 페이지 ID는 `system`, `inventory`, `status`이며 표시 순서와 호출 여부는 `GameMenuPanel`의 바인딩 목록에서 변경할 수 있습니다.

### GameMenuPanel 설정

1. `GameMenuPanel/Viewport/Content`의 `GameMenuLayoutGroup`에서 `Layout Mode`를 선택합니다.
2. `Button Size`, `Spacing`, `Padding`, 수평·수직 정렬을 설정합니다. `Wrap`에서는 `AutoFit` 또는 `MaximumColumns`와 열 상한을 설정합니다.
3. 루트 `GameMenuPanel`에서 수평 스크롤바의 `Visibility`, `Space Mode`, 높이와 간격을 설정합니다. `Hidden`은 시각 요소만 숨기며 스크롤 입력은 유지합니다.
4. Inspector의 계산 열 수, 행 수, 필요 너비·높이와 `Insufficient Space`를 확인합니다. Wrap에 필요한 높이가 부모로부터 제공되지 않으면 `FlexibleLayoutItem`의 고정 크기 또는 최소 크기를 조정합니다.
5. 새 버튼은 `Content` 아래에 추가하고 `ButtonRoot/VisualRoot` 계층을 유지합니다. `GameMenuButtonView`의 Button, Background, Icon, Label, Border, EffectOverlay를 연결한 뒤 `GameMenuPanel.Buttons`에 안정적인 페이지 ID로 등록합니다.

독립 미리보기는 `Assets/TxTRPG/UI/DEMO/GameMenuPanel/GameMenuPanelDemo.prefab`을 Prefab Mode로 열어 확인합니다. 루트 너비를 바꾸고 `HorizontalScroll`과 `Wrap`을 전환하여 한 줄 스크롤, 열 개행, 마지막 행 정렬과 부족 공간 표시를 확인합니다. 이 Demo는 창 서비스가 없는 상태이므로 버튼을 눌러 실제 창을 여는 용도가 아니라 레이아웃과 외형을 검증하는 용도입니다.

런타임에서는 `ConfigureLayout(...)`, `ConfigureScrollbar(...)`, `SetItemVisible(pageId, visible)`를 호출한 뒤 별도 매 프레임 갱신 없이 한 번의 예약된 레이아웃 갱신으로 반영됩니다. 외형 애니메이션은 `VisualRoot` 또는 개별 Graphic에만 적용하고 `ButtonRoot`의 크기와 위치는 변경하지 않습니다.

기존 자산은 Prefab 전용 재생성 메뉴로 이전합니다. 이 메뉴는 `GameMenuPanel.prefab`의 GUID를 유지하면서 ScrollRect, Viewport, Content, 버튼 View와 선택적 스크롤바 계층을 다시 만듭니다. 사용자 정의 외형을 운영 Prefab 자체에 직접 수정했다면 먼저 Prefab Variant나 별도 버튼 Prefab으로 옮긴 뒤 재생성합니다. Scene 인스턴스의 Override는 자동 저장하지 않으며, 적용 후 Inspector의 Prefab Override 창에서 기존 위치와 서비스 참조를 확인합니다.
### 모달 콘텐츠와 가방 Grid 설정

1. `Assets/TxTRPG/UI/Prefabs/ModalWindowHost.prefab`의 `ContentContainer`에서 본문 Padding을 설정합니다. 페이지는 `ContentContainer/Pages` 아래에 두고, 본문 마스크 밖에 표시할 보조 UI는 `Window/OverlayRoot`를 사용합니다.
2. `Assets/TxTRPG/UI/Prefabs/InventoryWindowPage.prefab`의 `InventoryGameWindowPage > Grid Settings`에서 `Display Mode`를 선택합니다.
3. 번호 페이지에서는 `Slots Per Page`와 `Fill Page With Empty Slots`를 설정합니다. 세로 스크롤에서는 `Minimum Scroll Slots`를 설정하며 결과 전체가 계속 표시됩니다.
4. `Columns`와 `Column Policy`를 설정합니다. 기존 반응형 동작은 `Adaptive Up To Configured`, 고정 열은 `Exact`입니다.
5. 같은 설정에서 `Cell Size`, `Spacing`, `Padding`, 전체 Grid 정렬, 마지막 불완전 행 정렬과 세로 배치를 지정합니다.
6. Play Mode에서 카테고리를 바꾸면 첫 페이지와 상단으로 이동하는지, 수량 갱신에서는 선택과 스크롤 위치가 가능한 한 유지되는지 확인합니다. 번호 버튼은 현재 페이지 주변에서 최대 7개만 생성됩니다.

### 타입 기반 모달 요청 설정

1. `GameMenuPanel.Buttons`의 각 바인딩은 기존 `Page Id`와 함께 요청의 `Content Kind`와 선택적 제목 Override를 가집니다. Bag은 `inventory + ItemGrid + Bag`, System은 `system + CustomContent + System Settings`를 사용합니다.
2. 표시 설정과 데이터 공급자는 버튼이 영구 소유하지 않습니다. `InventoryGameWindowPage.CreateDefaultRequest`가 Inspector의 `Grid Settings` 사본과 기존 세션·카탈로그를 연결하는 `InventoryModalDataProvider`를 런타임 요청에 넣습니다.
3. 프로그램 코드에서는 가능하면 `GameWindowService.CreateRequest`로 기본 요청을 얻거나 `ModalOpenRequest`를 명시적으로 구성한 뒤 `OpenAsync(request)`를 호출합니다. 호환용 `OpenAsync(pageId, returnFocus)`도 내부에서 같은 요청을 생성합니다.
4. 새 본문 타입을 추가할 때에는 `ModalContentKind`, 해당 `IModalContentConfiguration`의 복사 구현, 명확한 `IModalDataProvider`와 이를 소비하는 `GameWindowPage.ApplyRequest`를 함께 구현합니다. 공통 Dictionary나 페이지 내 슬롯 인덱스를 도메인 ID로 사용하지 않습니다.
5. Paged에서는 이전·다음, 현재/전체 표시와 번호 버튼을 사용합니다. 번호 버튼은 현재 페이지 주변에서 최대 설정 개수만 생성되며, 생략된 페이지는 이전·다음으로 접근합니다. VerticalScroll에서는 번호 UI를 숨기고 해당 카테고리의 전체 결과를 표시합니다.
6. 재시도는 마지막 문자열 ID가 아니라 원래 `ModalOpenRequest` 전체를 다시 실행합니다. 닫기나 새 요청은 이전 준비 작업을 취소하며, 공급자의 기존 소유 데이터를 초기화하지 않습니다.

기존 자산을 이전할 때에는 Scene을 먼저 저장하고 `QuickItemsUiProjectBuilder.ApplyTypedModalRequestCoordinator`의 선별 적용 경로를 사용합니다. 이 경로는 `GameMenuPanel.prefab`, `GameMenuScreen.prefab`과 TMP_MainScene의 기존 메뉴 바인딩에 요청 종류와 제목만 기록하며 Transform, 이미지, 서비스 연결 및 사용자 Grid 설정을 유지합니다. 임시 내부 메뉴로 호출했다면 적용 직후 메뉴 선언을 제거하고 재컴파일합니다.

### 가방 오류 표시와 설정 대체 동작

1. `InventoryWindowPage.prefab`에서 `InventoryGameWindowPage`의 `Catalog`와 `Item Grid`가 연결되어 있는지 확인합니다. 런타임 세션은 `AppScene` 수명의 `PlayerSessionHost`에서 연결되며 Prefab에 Scene 객체를 저장하지 않습니다.
2. 표시 설정은 같은 컴포넌트의 `Grid Settings`에서 편집합니다. 유효하지 않은 값이나 설정 누락은 코드의 안전한 기본값으로 대체되며, 원본 설정과 플레이어 인벤토리는 변경되지 않습니다.
3. `ModalWindowHost.prefab`의 `ErrorStateRoot/Error`는 좌우 Stretch 상태를 유지합니다. 오류 UI를 `ContentContainer/Pages`나 Grid의 자식으로 옮기지 않습니다.
4. 오류 발생 시 Console의 `inventory.*` 진단 코드를 확인합니다. `session-host-missing`은 AppScene 실행 경로, `catalog-missing`은 운영 ItemCatalog 참조, `item-grid-missing`은 가방 전용 ActionGridPanel 참조를 각각 점검합니다.
5. 재시도 버튼은 누락된 필수 참조를 자동 생성하지 않습니다. 원인을 복구한 뒤 현재 페이지 준비를 다시 실행하며, 닫기와 Cancel 입력은 항상 사용할 수 있어야 합니다.

기존 생성 자산은 현재 `QuickItemsUiProjectBuilder`로 다시 생성하면 같은 계층과 안전 기본값 정책을 얻습니다. 사용자 정의 Prefab Override가 있거나 TMP_MainScene 배치를 유지해야 하면 전체 재생성 대신 `ApplyInventoryErrorLayoutAndFallbacks`의 선별 이전 절차를 사용하고, 적용 전 Scene을 저장합니다. 이 메서드를 호출하기 위한 임시 `Tools/TxT RPG/Internal/...` 메뉴는 작업 중에만 허용되며 적용 후 반드시 제거하고 재컴파일해야 합니다.

기존 자산의 `displayMode`, `itemsPerPage`, `fillPageWithEmptySlots`는 최초 역직렬화 때 `GridContentLayoutSettings`로 이전됩니다. 기존 `ActionGridLayoutMode` enum의 숫자는 바뀌지 않았습니다. 저장된 운영 Prefab은 전체 재생성하지 않고 대상 계층과 참조만 갱신해야 하며, `TMP_MainScene`의 `GameMenuPanel` Transform과 Prefab Override는 유지합니다.

## Main Scene 시각 개선 제안안

- `Tools > TxT RPG > UI > Visual Proposals > Rebuild Main Scene Visual Proposal`은 `TMP_MainScene`을 보존하면서 `MainScene_VisualProposal`과 전용 스타일을 생성하거나 갱신합니다.
- `Tools > TxT RPG > UI > Visual Proposals > Play Main Scene Visual Proposal`은 AppScene을 먼저 실행하고 기존 초기화가 끝난 뒤 같은 Scene 전환 서비스로 제안 Scene을 엽니다.
- 생성기가 소유하는 설정, 비교 화면과 수동 검증 항목은 [Main Scene 시각 개선 제안안](main-scene-visual-proposal.md)을 확인합니다.
## ActionGridCell 아이콘 크기 설정

`ActionGridCell` Inspector의 `Icon Layout`에서 `Relative To Content`와 `Fixed Padding`을 선택합니다. 비율 기반 기본값은 0.9이며, 고정 여백 값은 Canvas UI 단위입니다. 기존 Prefab의 다른 사용자 설정을 보존하면서 신규 기본값만 적용하려면 `Tools > TxT RPG > UI > Prefabs > Upgrade Action Grid Cell Icon Layout`을 실행합니다. 전체 `Rebuild Action Grid`도 같은 비율 기본값을 생성하지만 Cell·Panel·Context Menu를 함께 재생성하므로 개별 사용자 설정을 유지해야 할 때에는 사용하지 않습니다.

`TMP_MainScene`의 ActionGridPanel은 공용 `ActionGridCell.prefab` 참조를 사용하므로 선택적 업그레이드가 즉시 적용됩니다. 적용 후 Prefab의 `Icon Sizing Mode = RelativeToContent`, `Icon Area Ratio = 0.9`, Icon Anchor Min/Max가 각각 `(0.05, 0.05)`와 `(0.95, 0.95)`인지 확인합니다.
## ActionGridCell 빈 슬롯 크기 설정

`ActionGridCell` Inspector의 `Empty Slot Layout`에서 `Relative To Content` 또는 `Fixed Padding`을 선택합니다. 비율은 `ContentRoot`를 기준으로 하며 운영 기본값은 0.9입니다. 고정 방식의 기존 외형 보존값은 Left·Right·Top·Bottom 각각 14 Canvas UI 단위입니다. 이 설정은 `Icon Layout`과 독립적입니다.

기존 사용자 설정을 보존하면서 공용 Cell의 빈 슬롯 정책만 이전하려면 `Tools > TxT RPG > UI > Prefabs > Upgrade Action Grid Cell Empty Slot Layout`을 실행합니다. 이 작업은 `ActionGridCell.prefab`의 빈 슬롯 모드와 비율만 바꾸고 자동 저장하며 안전하게 재실행할 수 있습니다. 전체 `Rebuild Action Grid`는 관련 Prefab 전체를 다시 만들기 때문에 사용자 정의 설정이 있으면 사용하지 않습니다.

`TMP_MainScene`은 공용 `ActionGridCell.prefab`을 사용하므로 별도 Scene Override 없이 반영됩니다. 적용 후 Prefab과 Play Mode 인스턴스에서 `Empty Slot Sizing Mode = RelativeToContent`, `Empty Slot Area Ratio = 0.9`, `EmptySlot` Anchor Min/Max가 각각 `(0.05, 0.05)`와 `(0.95, 0.95)`인지 확인합니다.
## ActionGridCell 비율 상한 설정

`ActionGridCell` Inspector에서 Icon Layout과 Empty Slot Layout의 모드를 각각 `Relative With Max Size`로 선택한 뒤 `Area Ratio`와 `Maximum Size`를 설정합니다. 운영 기본값은 두 표시 모두 0.9와 64×64이며, 최종 크기는 ContentRoot 비율값과 최대 크기 중 축별로 작은 값입니다.

기존 공용 Cell을 사용자 설정을 보존하면서 이전하려면 `Tools > TxT RPG > UI > Prefabs > Upgrade Action Grid Cell Ratio Capped Layout`을 실행합니다. 이 메뉴는 두 표시의 모드·비율·상한과 생성기 기본 ContentRoot 여백만 저장하고 전체 ActionGridPanel을 재생성하지 않습니다. 자동 저장되며 같은 값으로 안전하게 재실행할 수 있습니다. `TMP_MainScene`은 공용 Cell 참조를 사용하므로 별도 Scene Override가 필요하지 않습니다.
## 주사위 도메인 검증

`Assets/TxTRPG/Gameplay/Tests/Editor/DiceTests.cs`는 4·6·8면 효과 구성, 중복 면, 외부 면 판정, 면·범위 교체의 원자성, 입력 복사, 결과 스냅샷, 미지원 효과와 난수 공급자 계약을 검증합니다. Unity Test Runner의 Edit Mode에서 `TxTRPG.Gameplay.Tests.DiceTests`를 실행합니다. 이 도메인은 순수 C#이므로 Scene이나 Prefab 적용 절차가 없습니다.

## 임시 플레이어 주사위 메뉴

- 설정 자산: `Assets/TxTRPG/Application/Configuration/TemporaryDiceConfiguration.asset`
- 적용 메뉴: `Tools > TxT RPG > Application > Temporary > Apply Player Dice Roll Menu`
- 실행 경로: `Assets/Scenes/AppScene.unity`에서 시작하여 `TMP_MainScene`이 로드된 후 GameMenuPanel의 `주사위 굴리기 (임시)`를 사용합니다.
- 면 설정: 각 Dice 항목의 `Faces`에서 `Effect Kind`와 `Amount`를 편집합니다. 현재 런타임은 Attack과 Heal만 허용합니다.
- 기능 해제: 설정 자산의 `Enabled For Session`을 끄면 세션 지급과 버튼 실행이 비활성화됩니다.
- 제거 지점: 정식 주사위 소유 시스템으로 전환할 때 `TemporaryDiceRollMenuController`, 임시 버튼 바인딩과 AppRoot의 임시 설정 참조를 제거합니다. 저장 스키마에는 이전할 데이터가 없습니다.

적용 메뉴는 기존 메뉴·Story 인스턴스를 찾아 필요한 버튼과 참조만 추가합니다. 알려진 숫자형 D4/D6/D8 기본 자산은 공격·회복 교대 면으로 한 번 업그레이드하며, 효과를 판단할 수 없는 사용자 정의 숫자형 자산은 변경하지 않고 오류로 중단합니다. 효과형 자산의 개발자 설정은 재실행해도 덮어쓰지 않습니다. 실행 전 TMP_MainScene의 미저장 변경이 있으면 중단하고, 성공하면 AppRoot.prefab과 TMP_MainScene을 저장합니다.


## StoryTextPanel 레이아웃 수명 주기 검증

`StoryTextPanel`의 필수 참조 누락과 레이아웃 재진입을 확인할 때에는 다음 순서로 검증합니다.

1. Edit Mode에서 `TxTRPG.UI.Tests.StoryTextPanelTests`를 실행하여 운영 Prefab 참조와 누락 참조 경고를 확인합니다.
2. Play Mode에서 `TxTRPG.UI.Tests.StoryTextPanelLifecyclePlayModeTests`를 실행합니다. 활성화 전 미구성, 활성화 후 참조 복구, 반복 크기 변경, Canvas 갱신, 비활성화·재활성화와 파괴 경로를 포함합니다.
3. `Assets/Scenes/AppScene.unity`에서 실행하여 `TMP_MainScene` 진입 후 StoryTextPanel에 메시지를 추가하고 창 크기를 변경합니다.
4. Scene 전환 또는 Play Mode 종료 후 `StoryTextPanel.RebuildAndRefresh`, `ScrollToBottomAfterLayout`과 MissingReference 관련 예외가 없는지 Console에서 확인합니다.

운영 Prefab과 Scene 참조가 정상이라면 별도 적용 메뉴를 실행하지 않습니다. 누락이 확인되었을 때에도 자동 Scene 검색으로 숨기지 말고 Prefab 또는 해당 Scene Override의 직렬화 참조를 복구합니다.

## TMP_MainScene 임시 적 전투

- 설정 자산: `Assets/TxTRPG/Application/Configuration/TemporaryCombatConfiguration.asset`에서 활성 여부, 적 이름·외형 ID·최대 체력·공격량·회복량을 설정합니다.
- 적용 메뉴: `Tools > TxT RPG > Application > Temporary > Apply Player Dice Roll Menu`는 기존 TMP_MainScene 배치를 보존하면서 임시 전투 참조, 적 체력 바, 행동 예고와 `행동` 버튼 제목을 선별 적용합니다. 설정 자산이 이미 있으면 값을 덮어쓰지 않으며 안전하게 다시 실행할 수 있습니다. Scene은 자동 저장되므로 적용 전에 관련 미저장 변경을 먼저 저장하거나 별도로 보존해야 합니다.
- 실행 경로: `Assets/Scenes/AppScene.unity`에서 Play Mode를 시작하고 TMP_MainScene이 로드되면 적 체력 40/40, `다음 적 행동: 공격 5`와 `행동` 버튼을 확인합니다. 버튼을 누르면 주사위별 기록, 실제 적용 결과와 적 행동이 Story에 순서대로 추가됩니다.
- 격리 검증: 행동 전후 `PlayerSessionHost.Instance.Session.CurrentPlayer.ActiveCharacter.Health` 값은 같아야 합니다. Scene을 다시 진입하면 임시 전투만 초기화됩니다.
- 비활성화: 설정 자산의 `Enabled For Scene`을 끄면 전투가 생성되지 않고 명령이 비활성화됩니다. 정식 전투로 이전할 때에는 Scene의 임시 컨트롤러·추가 체력 바·예고 표시와 임시 명령 바인딩을 제거합니다. 저장 마이그레이션은 필요하지 않습니다.
## 점진 생성 탐험 검증

- 설정 자산: `Assets/TxTRPG/Application/Configuration/ExplorationRunConfiguration.asset`에서 `Choice Count`, `Combat Weight`, `Recovery Upgrade Weight`를 설정합니다. 가중치 합계는 양수여야 합니다.
- Scene 적용: `Tools > TxT RPG > Application > Temporary > Apply Player Dice Roll Menu`는 탐험 설정이 없을 때만 기본 자산을 만들고, TMP_MainScene의 Canvas에 `ExplorationNodePanel`과 필수 참조를 선별 적용합니다. 기존 설정 자산 값은 덮어쓰지 않습니다.
- 실행: `Assets/Scenes/AppScene.unity`에서 시작합니다. 후보를 선택하면 전투 노드는 `행동` 버튼을 활성화하고, 회복·강화 노드는 미구현 안내와 `계속` 버튼을 표시합니다. 완료 뒤 새 후보가 나타나며 상태 문구의 체력이 이전 노드 결과를 유지해야 합니다.
- 진단: `ExplorationRunController.Run.Nodes`에서 부모 ID, 형제 순서, 깊이, 상태와 완료 이유를 읽을 수 있습니다. 미선택 형제는 `Unchosen`이며 자식을 갖지 않아야 합니다.
- 제거: 정식 탐험으로 교체할 때 Scene의 `ExplorationRunController`와 `ExplorationNodePanel`, 임시 설정 자산 및 명령 바인딩을 제거합니다. 저장 데이터 마이그레이션은 필요하지 않습니다.