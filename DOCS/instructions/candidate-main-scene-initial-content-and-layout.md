# Candidate_MainScene 초기 콘텐츠 전환과 FlexibleLayout 영역 구성

## 1. 목적과 확정 사항

AppScene이 처음 로드하는 콘텐츠 Scene을 `Assets/Scenes/TMP_MainScene.unity`에서 `Assets/Scenes/Candidate_MainScene.unity`로 바꾼다. 그리고 `Candidate_MainScene`의 `Canvas/MainArea` 아래에 있는 `LeftArea`, `MiddleArea`, `RightArea`를 `FlexibleLayoutPanel`과 `FlexibleLayoutItem`으로 실제 배치되도록 구성한다. 함께, 사용자가 이미 삭제한 `MainScene_VisualProposal.unity`에 남아 있는 Build Settings 항목, 생성기, 테스트와 문서를 정리한다.

이 문서는 후속 구현 지침이며, 적용이나 실행 검증을 완료했다는 기록이 아니다.

사용자 답변으로 확정한 사항은 다음과 같다.

| 항목 | 확정 내용 |
| --- | --- |
| 초기 Scene 전환 범위 | 정식 기준으로 전환한다. AppRoot Prefab과 AppScene의 값뿐 아니라 `EditorPlayStartPolicy`의 기대값, `SceneTransitionPrefabBuilder`·`AppSceneRoot`의 기본값, `SceneTransitionTests`의 기대값도 `Candidate_MainScene`으로 바꾼다. `TMP_MainScene`은 Build Settings에 활성 상태로 남긴다. AppScene을 거쳐 `TMP_MainScene`을 기다리던 Play Mode 테스트는 `TMP_MainScene`으로 명시적으로 전환한 뒤 검사하도록 고친다. |
| 계층 구조 | `TMP_MainScene`과 같은 방식으로 `MainArea`를 `FlexibleLayoutPanel.prefab` 인스턴스로 교체하고, 세 Area도 각각 `FlexibleLayoutPanel.prefab` 인스턴스(`FlexibleLayoutItem` 추가)로 만들어 `MainArea/ContentLayer` 아래에 둔다. 오브젝트 이름은 유지한다. |
| 비율과 반응형 | Weighted 1:3:1이다. `MainArea`는 `Fixed Axis = Horizontal`, `Axis Policy = Vertical When Narrow`, `Breakpoint = 720`을 사용한다. Spacing과 Content Margins는 `TMP_MainScene`의 `Main_FlexibleLayoutPanel` 값(Spacing 20, 좌우 15·상하 10)을 따른다. |
| 삭제된 제안 Scene | Build Settings에서 존재하지 않는 Scene 항목을 제거하고, `MainSceneVisualProposalBuilder`와 관련 테스트·자산·문서도 함께 정리한다. |

## 2. 작성 시점에 확인한 현재 상태

### 2.1 초기 콘텐츠 경로가 기록된 위치

| 위치 | 현재 값 또는 동작 |
| --- | --- |
| `Assets/TxTRPG/SceneTransition/Prefabs/AppRoot.prefab` | `AppSceneRoot.initialContentScenePath: Assets/Scenes/TMP_MainScene.unity`, `loadInitialContentOnStart: 1` |
| `Assets/Scenes/AppScene.unity` | AppRoot Prefab 인스턴스가 `initialContentScenePath`를 같은 값(`TMP_MainScene`)으로 Override하고 있다. |
| `Assets/TxTRPG/SceneTransition/Runtime/AppSceneRoot.cs` | 필드 초기값이 `"Assets/Scenes/TMP_MainScene.unity"`이다. `[ScenePath(excludeAppScene: true)]`가 적용되어 Build Settings에 활성화된 Scene만 선택할 수 있다. |
| `Assets/TxTRPG/SceneTransition/Editor/SceneTransitionPrefabBuilder.cs` | `InitialContentScenePath` 상수가 `TMP_MainScene`이다. `CreateOrUpdateAssets`(`Tools > TxT RPG > Application > Rebuild App Scene`)는 기존 Prefab의 유효한 경로를 우선 보존하고, 유효하지 않을 때만 이 상수로 복구한다. 이어서 `ApplicationProjectBuilder.ConfigureInitialContentScene`을 호출한다. |
| `Assets/TxTRPG/Application/Editor/ApplicationProjectBuilder.cs` | `ConfigureInitialContentScene`은 초기 콘텐츠 Scene에서 `ActiveCharacterDisplayBinder` 또는 `CharacterDisplayPanelDemoLoader`를 찾지 못하면 `InvalidOperationException`을 발생시킨다. `Candidate_MainScene`에는 둘 다 없다. |
| `Assets/TxTRPG/SceneTransition/Editor/EditorPlayStartPolicy.cs` | `ExpectedInitialContentScenePath`가 `TMP_MainScene`이며, Play 직전 검증(`TryValidateConfiguration`)에서 값이 다르면 오류를 반환한다. Settings 검색 키워드에도 `TMP_MainScene`이 있다. |
| `Assets/TxTRPG/SceneTransition/Editor/AppSceneBuildValidator.cs` | 초기 콘텐츠 경로가 Build Settings에 정확히 한 번 활성화되어 있는지 검사한다. 경로 상수에는 의존하지 않는다. |
| `ProjectSettings/EditorBuildSettings.asset` | `AppScene`, `TMP_MainScene`, `MainScene_VisualProposal`(파일 삭제됨) 순서로 모두 활성화되어 있다. `Candidate_MainScene`(GUID `65b2ff3bfd3f2554ea05f386f198e49e`)은 등록되어 있지 않다. |

### 2.2 `TMP_MainScene`을 초기 콘텐츠로 가정하는 테스트와 도구

| 위치 | 의존 내용 |
| --- | --- |
| `Assets/TxTRPG/SceneTransition/Tests/Editor/SceneTransitionTests.cs` 45~46행 | `GeneratedAppScene_HasRequiredHierarchyReferencesAndBuildOrder`(`AssetWriting`)가 초기 경로를 `TMP_MainScene` 문자열로 단언한다. 73행 이후의 `BuildScenePathUtility` 테스트는 임의 목록을 사용하므로 영향이 없다. |
| `Assets/TxTRPG/Application/Tests/Editor/PlayerSessionTests.cs` | `InitialContentScene_UsesPlayerSessionInsteadOfDemoLoader`는 이름과 달리 `TMP_MainScene` 파일을 직접 읽어 운영 캐릭터 UI 연결을 검사한다. |
| `Assets/TxTRPG/UI/Tests/PlayMode/MainSceneMenuInputPlayModeTests.cs` | AppScene을 로드한 뒤 `TMP_MainScene`이 로드되기를 최대 600프레임 기다린다. 전환 후에는 `Candidate_MainScene`이 로드되므로 시간 초과로 실패한다. |
| `TMP_MainScene`이 Play Mode에서 로드되어 있어야 하는 Editor 검증 메뉴 | `ExplorationCardRuntimeVerification`, `TemporaryStoryDicePanelSwap`, `TemporaryExplorationEventPlayValidation`, `TemporarySlotMachinePlayValidation`, `TemporaryOwnedDiceIntegration`, `TemporaryExplorationSelectionVerification`, `CombatActionBoundaryValidation`, `TemporaryActionsGridVerification`의 Play Mode 메뉴. 전환 후에는 AppScene에서 Play를 시작해도 `TMP_MainScene`이 자동으로 로드되지 않는다. |
| `TemporaryExplorationEventPlayValidation.cs` 37행, `TemporarySlotMachinePlayValidation.cs` 38행 | Scene 재진입 검증에 사용하는 `OtherContentScene`이 삭제된 `MainScene_VisualProposal.unity`이다. |

### 2.3 `Candidate_MainScene` 계층

```text
Candidate_MainScene
├── EventSystem
├── Main Camera
├── Canvas                     Canvas, CanvasScaler(Constant Pixel Size), GraphicRaycaster
│   └── MainArea               FlexibleLayoutPanel(contentRoot 없음), Image(흰색 α0.392)
│       ├── LeftArea           FlexibleLayoutItem(Weighted 1), Image(주황 α0.392), Stretch
│       ├── MiddleArea         FlexibleLayoutItem(Weighted 1), Image(파랑 α0.392), Stretch
│       └── RightArea          FlexibleLayoutItem(Weighted 1), Image(초록 α0.392), Stretch
└── Directional Light
```

`MainArea`의 `FlexibleLayoutPanel`은 Prefab 없이 직접 추가된 컴포넌트이며 `contentRoot`, `contentLayout`이 비어 있다. `FlexibleLayoutPanel.SyncContentLayout`은 `ContentRoot`가 자기 자신이면 `contentLayout`을 비우고 반환하며, 실제 자식 배치는 `ContentLayer`의 `FlexibleContentLayoutGroup`만 수행한다. 따라서 현재 세 Area는 배분되지 않고 Stretch 앵커로 화면 전체에 겹쳐 있다.

`TMP_MainScene`은 `MainScreenViewport/Main_FlexibleLayoutPanel/ContentLayer` 아래에 `LeftPanel`, `MiddlePanel`, `RightPanel`을 모두 `FlexibleLayoutPanel.prefab` 인스턴스로 두고, 각 인스턴스에 `FlexibleLayoutItem`을 추가 컴포넌트로 붙였다. 영역 색상은 각 인스턴스의 `BackgroundLayer/BackgroundVisualRoot/BackgroundA` Image 색상 Override로 지정한다. `FlexibleLayoutPanel.prefab` 루트에는 `FlexibleLayoutItem`이 없다.

### 2.4 `MainScene_VisualProposal` 관련 잔여 항목

| 경로 | 상태 |
| --- | --- |
| `Assets/Scenes/MainScene_VisualProposal.unity`(.meta) | 사용자가 이미 삭제했다(작업 시작 전 Git 상태 `D`). |
| `Assets/TxTRPG/Application/Editor/MainSceneVisualProposalBuilder.cs` | `Tools/TxT RPG/UI/Visual Proposals/Rebuild Main Scene Visual Proposal`, `.../Play Main Scene Visual Proposal` 메뉴를 제공한다. |
| `Assets/TxTRPG/Application/Runtime/Presentation/MainSceneVisualProposalMarker.cs` | 생성기와 테스트에서만 사용한다. 다른 Scene·Prefab·자산에서 GUID 참조가 없다. |
| `Assets/TxTRPG/Application/Tests/Editor/MainSceneVisualProposalTests.cs` | `[Category("AssetWriting")]`, `[Explicit]` 테스트이다. |
| `Assets/TxTRPG/UI/VisualProposals/MainScene/` | 제안 전용 스타일 5개와 `README.md`가 있다. 폴더 밖의 자산에서 GUID 참조가 없다. |
| 문서 | `DOCS/development/main-scene-visual-proposal.md`, `DOCS/README.md`, `DOCS/development/workflows.md`, `DOCS/architecture/project-structure.md`, `DOCS/architecture/main-scene-default-presentation.md`, `DOCS/architecture/story-text-panel.md`, `DOCS/instructions/main-scene-visual-improvement-proposal.md`와 그 밖의 지침서 일부가 제안 Scene을 언급한다. |

`MainScenePresentationController`, `MainSceneDefaultContentBuilder`, `MainScenePreviewHarness.prefab`, `Assets/TxTRPG/Application/Configuration/MainScenePresentation/` 자산은 제안 Scene과 별개인 기본 표시·Preview 기능이므로 삭제 대상이 아니다.

## 3. 선행 조건

- 작업 시작 전에 `git status --short`를 기록한다. 작성 시점에는 `AddressableAssetSettings.asset`, `NotoSansKR-Fallback.asset` 수정, `MainScene_VisualProposal.unity` 삭제, `Candidate_MainScene.unity` 추가가 사용자 변경으로 존재했다. 이 변경들을 되돌리거나 함께 정리하지 않는다. 단, 이 작업 범위인 `MainScene_VisualProposal` 삭제는 유지한다.
- Unity Editor의 연결, 컴파일 상태, 열린 Scene의 미저장 변경을 확인한다. 미저장 변경이 있는 Scene은 저장하거나 덮어쓰지 말고 사용자에게 알린다.
- `Candidate_MainScene`은 아직 커밋되지 않은 사용자 Scene이다. 구조 변경 전에 현재 상태를 파악하고, 지정한 오브젝트 외에는 수정하지 않는다.

## 4. 작업 범위

### 4.1 포함

1. 초기 콘텐츠 Scene을 `Candidate_MainScene`으로 전환하고 관련 기본값·검증·테스트를 갱신한다.
2. Build Settings에 `Candidate_MainScene`을 활성 상태로 추가하고, 삭제된 `MainScene_VisualProposal` 항목을 제거한다.
3. `Rebuild App Scene`이 캐릭터 패널이 없는 초기 콘텐츠 Scene에서 실패하지 않게 한다.
4. Play Mode에서 `TMP_MainScene`으로 전환하는 Editor 메뉴를 추가하고, `TMP_MainScene`을 기다리던 Play Mode 테스트와 재진입 검증 메뉴를 갱신한다.
5. `Candidate_MainScene/Canvas/MainArea`와 세 Area를 `FlexibleLayoutPanel.prefab` 인스턴스 기반으로 재구성한다.
6. `MainSceneVisualProposalBuilder`, `MainSceneVisualProposalMarker`, `MainSceneVisualProposalTests`, 제안 전용 자산과 문서를 정리한다.
7. 위 변경을 설명하는 문서를 갱신한다.

### 4.2 제외

- `Candidate_MainScene`에 Story, Actions, Character, 적, 슬롯머신 등 게임 패널을 배치하는 작업은 하지 않는다. 전환 후 AppScene에서 Play하면 세 영역만 표시되는 것이 이번 작업의 의도된 결과이다.
- `TMP_MainScene`의 계층과 값은 바꾸지 않는다.
- `Candidate_MainScene`의 `CanvasScaler` 정책(현재 `TMP_MainScene`과 같은 Constant Pixel Size), Safe Area 처리(`TMP_MainScene`의 `MainScreenViewport`·`ResponsiveMainScreen`에 해당하는 구성)는 바꾸지 않는다. 이 항목은 후속 작업으로 최종 보고에 남긴다.
- 이미 삭제된 Scene 외의 Build Settings 항목은 정리하지 않는다.
- `MainScenePresentationController`와 기본 표시·Preview 기능은 삭제하지 않는다.
- 커밋과 푸시는 하지 않는다.

## 5. 구현 요구 사항

### 5.1 초기 콘텐츠 경로의 단일 기준

1. `SceneTransitionPrefabBuilder.InitialContentScenePath`를 `"Assets/Scenes/Candidate_MainScene.unity"`로 바꾼다.
2. `EditorPlayStartPolicy.ExpectedInitialContentScenePath`는 같은 어셈블리(`TxTRPG.SceneTransition.Editor`)에 있는 `SceneTransitionPrefabBuilder.InitialContentScenePath`를 참조하도록 바꿔 경로가 두 곳에 중복되지 않게 한다. 어셈블리 경계가 다르다고 확인되면 리터럴을 바꾸고 이유를 보고한다. Settings 검색 키워드의 `"TMP_MainScene"`은 `"Candidate_MainScene"`으로 바꾼다.
3. `AppSceneRoot.initialContentScenePath`의 필드 초기값을 `Candidate_MainScene` 경로로 바꾼다. `FormerlySerializedAs`와 `ScenePath` 특성은 유지한다. 필드 초기값은 이미 직렬화된 인스턴스 값을 바꾸지 않으므로, 실제 값은 아래 6.2절의 적용으로 바꾼다.
4. `AppSceneRoot.cs`의 런타임 어셈블리에서 Editor 어셈블리를 참조하지 않는다.

### 5.2 Build Settings

1. 최종 목록은 `AppScene`(0번, 활성), `Candidate_MainScene`(활성), `TMP_MainScene`(활성) 순서로 한다.
2. `Assets/Scenes/MainScene_VisualProposal.unity` 항목을 제거한다. 다른 항목의 활성 상태와 `m_configObjects`는 바꾸지 않는다.
3. 적용은 `EditorBuildSettings.scenes` API를 사용한다. `EditorBuildSettings.asset`을 텍스트로 직접 편집하지 않는다.

### 5.3 `Rebuild App Scene`과 캐릭터 패널이 없는 초기 콘텐츠

`ApplicationProjectBuilder.ConfigureInitialContentScene`은 초기 콘텐츠 Scene에 `ActiveCharacterDisplayBinder`와 `CharacterDisplayPanelDemoLoader`가 모두 없으면 예외 대신 `Debug.Log`로 "캐릭터 패널이 없어 운영 캐릭터 UI 연결을 건너뛰었다"는 사실과 Scene 경로를 남기고, Scene을 저장하지 않고 반환한다. 패널이 있는 경우의 기존 동작과 미저장 Scene에 대한 예외는 유지한다. 이 변경으로 `Rebuild App Scene`이 `Candidate_MainScene`을 수정하거나 저장하지 않아야 한다.

### 5.4 Play Mode에서 `TMP_MainScene`으로 전환하는 메뉴

1. `Assets/TxTRPG/SceneTransition/Editor/`에 Editor 전용 정적 클래스(예: `PlayModeContentSceneMenu`)를 추가하고 `TxTRPGEditorMenuPaths.Application + "Play Mode/Load TMP_MainScene"` 메뉴를 우선순위 `TxTRPGEditorMenuPriorities.Validate`로 만든다. 반복적으로 사용하는 검증 절차의 진입점이므로 영구 메뉴로 둔다.
2. 메뉴는 Play Mode가 아니거나, `AppSceneRoot.Instance` 또는 `SceneTransitions`가 없거나, 전환이 진행 중이면 이유를 담은 오류를 표시하고 아무것도 하지 않는다. `Assets/TxTRPG/Editor/Tests/Editor/EditorMenuTests.cs`는 `TxTRPG.SceneTransition.Editor` 어셈블리의 모든 `MenuItem`을 고유 경로로 검사하므로, `isValidateFunction` 메뉴 함수는 추가하지 않고 실행 시점 검사로 처리한다. 같은 테스트의 기대 목록에 새 메뉴와 우선순위를 추가한다.
3. 조건을 만족하면 `SceneTransitionService.LoadContentSceneAsync("Assets/Scenes/TMP_MainScene.unity")`를 호출하고, 완료 또는 실패를 Console에 남긴다. AppRoot, `PlayerSessionHost`와 세션 상태는 그대로 유지되어야 한다.
4. 이미 `TMP_MainScene`이 현재 콘텐츠이면 전환하지 않고 그 사실만 남긴다.
5. 2.2절의 Play Mode 검증 메뉴들은 전제 조건을 바꾸지 않는다. 대신 각 메뉴의 오류 문구 중 "Start from AppScene and wait for TMP_MainScene"처럼 자동 로드를 전제한 문구는 위 메뉴 경로를 안내하도록 고친다. 동작 논리는 바꾸지 않는다.

### 5.5 테스트와 재진입 검증

1. `SceneTransitionTests.GeneratedAppScene_HasRequiredHierarchyReferencesAndBuildOrder`의 기대값을 `SceneTransitionPrefabBuilder.InitialContentScenePath`로 바꾼다.
2. `PlayerSessionTests.InitialContentScene_UsesPlayerSessionInsteadOfDemoLoader`는 계속 `TMP_MainScene`을 검사하고, 메서드 이름을 `MainScene_UsesPlayerSessionInsteadOfDemoLoader`로 바꿔 의미를 맞춘다.
3. `MainSceneMenuInputPlayModeTests`는 AppScene을 로드하고 `Candidate_MainScene`(초기 콘텐츠)이 로드될 때까지 기다린 뒤, `AppSceneRoot.Instance.SceneTransitions.LoadContentSceneAsync(MainScenePath)`로 `TMP_MainScene`에 전환하고 완료를 기다린 다음 기존 검사를 수행한다. 초기 콘텐츠 경로는 리터럴 대신 `AppSceneRoot.Instance.InitialContentScenePath`로 얻는다.
4. `TemporaryExplorationEventPlayValidation`과 `TemporarySlotMachinePlayValidation`의 `OtherContentScene`을 `"Assets/Scenes/Candidate_MainScene.unity"`로 바꾸고, 로그·주석의 `MainScene_VisualProposal` 표기를 함께 바꾼다. 재진입 경로는 "TMP_MainScene → Candidate_MainScene → TMP_MainScene"이 된다.
5. `MainSceneDefaultPresentationTests.cs` 41행 주석의 제안 Scene 언급을 현재 상태(운영 Scene에 적용되지 않음)로 고친다.

### 5.6 `Candidate_MainScene` 영역 구성

목표 계층은 다음과 같다.

```text
Canvas
└── MainArea                    FlexibleLayoutPanel.prefab 인스턴스
    ├── BackgroundLayer
    │   └── BackgroundVisualRoot/BackgroundA   기존 MainArea Image 색상
    ├── BackgroundContentLayer
    ├── ContentLayer
    │   ├── LeftArea            FlexibleLayoutPanel.prefab 인스턴스 + FlexibleLayoutItem(Weighted 1)
    │   ├── MiddleArea          FlexibleLayoutPanel.prefab 인스턴스 + FlexibleLayoutItem(Weighted 3)
    │   └── RightArea           FlexibleLayoutPanel.prefab 인스턴스 + FlexibleLayoutItem(Weighted 1)
    └── ForegroundLayer
```

1. `MainArea`는 `Canvas`의 같은 형제 위치에 `Assets/TxTRPG/UI/Prefabs/FlexibleLayoutPanel.prefab`을 인스턴스화하여 만든다. 이름은 `MainArea`이고, RectTransform은 기존 값(Stretch, Offset 0)을 따른다.
2. `MainArea` 설정은 `Fixed Axis = Horizontal`, `Axis Policy = Vertical When Narrow`, `Breakpoint = 720`, `Spacing = 20`, `Override Content Margins = true`, `Content Margins = Left 15, Right 15, Top 10, Bottom 10`, `Overflow = ShrinkBelowMinimum`, `Include Inactive Children = false`, `Clip Content = false`이다. 새 `Node Id`가 비어 있으면 고유 값을 부여하고, 기존 `MainArea`의 `nodeId`(`8a1cefb1f6c94b8bbf238bcc6f2373a8`)를 옮길 수 있으면 그대로 옮긴다.
3. 세 Area는 각각 `FlexibleLayoutPanel.prefab` 인스턴스로 `MainArea/ContentLayer` 아래에 Left, Middle, Right 순서로 둔다. 각 인스턴스 루트에 `FlexibleLayoutItem`을 추가 컴포넌트로 붙이고 `Size Mode = Weighted`, Weight는 1, 3, 1, Minimum Size와 Maximum Size는 0으로 둔다. 세 Area 내부의 축은 Prefab 기본값을 유지한다.
4. 기존 Image 색상은 각 인스턴스의 `BackgroundLayer/BackgroundVisualRoot/BackgroundA` Image 색상 Override로 옮긴다(MainArea 흰색 α0.392, LeftArea 주황, MiddleArea 파랑, RightArea 초록, 각 α0.392). 영역을 구분하는 임시 표시이며, 배경 Image는 Prefab 기본값대로 `raycastTarget = false`를 유지한다.
5. 기존 `MainArea`, `LeftArea`, `MiddleArea`, `RightArea` 오브젝트는 새 구성으로 값을 옮긴 뒤 제거한다. 현재 세 Area에는 자식이 없으므로 옮길 자식은 없다. 작업 시점에 자식이나 추가 컴포넌트가 생겼다면 진행을 멈추고 사용자에게 보고한다.
6. `Canvas`, `EventSystem`, `Main Camera`, `Directional Light`와 `CanvasScaler` 설정은 바꾸지 않는다.
7. 레이아웃이 제어하는 Area 루트에는 효과를 적용하지 않는다. 이후 효과는 `DOCS/development/visual-effect-readiness.md`에 따라 각 Area의 `BackgroundVisualRoot` 또는 별도 `VisualRoot`에 적용한다.

### 5.7 적용 방식

이 작업은 한 번만 적용하면 되는 Scene·Prefab·설정 변경이므로 재사용 Builder를 만들지 않는다. Unity Editor 자동화로 직접 적용하는 방법을 우선한다. 자동화를 사용할 수 없으면 `TxTRPGEditorMenuPaths.Application + "Temporary/Apply Candidate Main Scene Initial Content"` 임시 메뉴를 만들어 사용자가 실행하게 한다. 임시 메뉴를 만드는 경우 다음을 지킨다.

- 클래스 주석에 목적, 임시 상태, 재실행 안전성을 기록한다.
- 실행 전 Play Mode·컴파일 중·대상 Scene 미저장 변경 여부를 검사하고, 조건이 맞지 않으면 아무것도 바꾸지 않는다.
- 이미 적용된 상태(`MainArea`가 `FlexibleLayoutPanel.prefab` 인스턴스이고 Build Settings·AppRoot 값이 목표와 같음)이면 변경 없이 그 사실을 로그로 남긴다. 따라서 재실행해도 안전해야 한다.
- AppRoot Prefab 값 변경, AppScene Override 되돌리기, Build Settings 변경, Candidate 영역 구성을 수행하고 바꾼 자산만 저장한다.
- 사용자가 적용과 검증을 끝낸 뒤 제거할 수 있다고 문서에 기록한다.

### 5.8 `MainScene_VisualProposal` 정리

1. 다음을 `.meta`와 함께 삭제한다.
   - `Assets/TxTRPG/Application/Editor/MainSceneVisualProposalBuilder.cs`
   - `Assets/TxTRPG/Application/Runtime/Presentation/MainSceneVisualProposalMarker.cs`
   - `Assets/TxTRPG/Application/Tests/Editor/MainSceneVisualProposalTests.cs`
   - `Assets/TxTRPG/UI/VisualProposals/MainScene/` 폴더 전체와 비게 되는 `Assets/TxTRPG/UI/VisualProposals/` 폴더
2. 삭제 전에 각 스크립트·자산의 GUID와 클래스 이름이 다른 코드, Scene, Prefab, 자산, `asmdef`에서 참조되지 않는지 다시 확인한다. 참조가 새로 발견되면 삭제를 멈추고 보고한다.
3. `MainSceneVisualProposalBuilder`가 `PrefabRebuild` 등록·제외 목록에 없다는 사실을 확인한다(작성 시점에는 없음).
4. `DOCS/development/main-scene-visual-proposal.md`를 삭제하고, `DOCS/README.md` 문서 목록의 해당 행과 `DOCS/development/workflows.md`의 링크·메뉴 설명을 제거한다.
5. `DOCS/instructions/main-scene-visual-improvement-proposal.md`는 기록으로 남긴다. 문서 맨 앞에 "이 지침의 결과물(제안 Scene, 생성기, 테스트, 자산)은 `candidate-main-scene-initial-content-and-layout.md`에 따라 제거되었다"는 안내를 추가하고, `DOCS/instructions/README.md`의 설명에도 같은 사실을 덧붙인다.
6. 그 밖의 아키텍처·개발 문서에서 제안 Scene을 현재 구성으로 설명하는 문장을 고친다. 지난 작업을 기록한 다른 지침서의 본문은 수정하지 않는다.

## 6. 적용 절차와 Editor 작업

### 6.1 코드 변경 후

1. 컴파일과 도메인 재로드가 끝날 때까지 기다리고 Console 컴파일 오류를 해결한다.
2. 삭제한 스크립트를 참조하던 Missing Script가 없는지 확인한다.

### 6.2 자산 적용

1. `AppRoot.prefab`의 `AppSceneRoot.initialContentScenePath`를 `Assets/Scenes/Candidate_MainScene.unity`로 바꾸고 저장한다. 이 작업은 Build Settings에 `Candidate_MainScene`을 먼저 추가한 뒤 수행한다.
2. `AppScene.unity`의 AppRoot 인스턴스에서 `initialContentScenePath` Override를 되돌려(Revert) Prefab 값을 따르게 하고 저장한다. 다른 Override는 유지한다.
3. 5.6절에 따라 `Candidate_MainScene`을 구성하고 저장한다.
4. `Tools > TxT RPG > Application > Rebuild App Scene`은 실행하지 않아도 된다. 실행하면 AppScene과 AppRoot Prefab이 재생성되므로, 실행한 경우 결과 값을 다시 확인한다.

### 6.3 직렬화 변경 검토

적용 후 `serialization-diff-reviewer`로 `.unity`, `.prefab`, `.asset`, `.meta`, `ProjectSettings` 변경을 검토한다. 의도한 변경은 `AppRoot.prefab`, `AppScene.unity`, `Candidate_MainScene.unity`, `EditorBuildSettings.asset`, 삭제한 자산과 `.meta`뿐이다. 작업 전부터 있던 사용자 변경은 되돌리지 않는다.

## 7. 호환성과 실패 동작

- AppRoot Prefab의 초기 경로가 Build Settings에 없거나 비활성화된 상태로 저장되지 않게 순서를 지킨다. 중간 실패 시에는 Inspector의 `<Invalid>` 표시와 `Validate App Scene Configuration`으로 원인을 확인할 수 있어야 한다.
- 저장 데이터, 입력 바인딩, 플레이어 설정 형식은 바뀌지 않는다. `PlayerSessionHost`는 AppRoot에 있으므로 콘텐츠 Scene 전환과 무관하게 유지된다.
- `Candidate_MainScene`에는 `ExplorationRunController` 등 게임 진행 구성 요소가 없으므로, Play 시 콘텐츠 초기화 계약(`SceneTransitionService.ValidateLoadedScene`, 준비 완료 대기)이 이를 요구하지 않는지 확인한다. 요구한다면 Scene에 임의 구성 요소를 추가하지 말고 실패 내용과 함께 보고한다.
- `TMP_MainScene`을 직접 열고 Play하는 경로(Play Mode Start 정책 해제)와, 기존 Edit Mode 적용 메뉴는 그대로 동작해야 한다.
- 좁은 화면(너비 720 미만, Canvas 단위)에서는 세 영역이 세로로 쌓이며, 비율은 같은 1:3:1 Weight를 따른다.

## 8. 검증 기준

### 8.1 Edit Mode 테스트

테스트 전후의 Git 상태를 기록하고, 다음 클래스만 필터로 실행한다.

| 필터 | 이유 |
| --- | --- |
| `SceneTransitionTests` | 초기 경로 기대값, Build Settings, `AppSceneBuildValidator`. `GeneratedAppScene_HasRequiredHierarchyReferencesAndBuildOrder`는 `AssetWriting`이며 `Rebuild App Scene`을 호출한다. 이번 변경이 이 Builder를 다루므로 포함한다. |
| `PlayerSessionTests` | 메서드 이름 변경과 `TMP_MainScene` 운영 캐릭터 UI 검사 |
| `EditorMenuTests` | 새 `Play Mode/Load TMP_MainScene` 메뉴의 경로·우선순위 등록 |
| `FlexibleLayoutPanelTests` 중 `AssetWriting`이 아닌 테스트 | `FlexibleLayoutPanel` 계산 회귀 확인. 이 작업은 Builder를 바꾸지 않으므로 `!AssetWriting`으로 제외한다. |

전체 Edit Mode 테스트는 실행하지 않는다. `SceneTransitionTests` 실행 후 AppScene·AppRoot Prefab·Build Settings가 목표 값과 다르게 바뀌었다면 원인을 보고한다.

### 8.2 Play Mode 테스트

`MainSceneMenuInputPlayModeTests`를 이름으로 실행하여 `Candidate_MainScene` 로드 → `TMP_MainScene` 전환 → 기존 메뉴 검사가 통과하는지 확인한다.

### 8.3 Editor 확인

1. `Edit > Project Settings > TxT RPG > Play Mode Start`에서 검증 오류가 없다.
2. `Tools > TxT RPG > Application > Validate App Scene Configuration`이 통과한다.
3. 아무 Scene에서 Play를 시작하면 AppScene을 거쳐 `Candidate_MainScene`이 로드되고, 세 영역이 왼쪽부터 1:3:1 너비로 겹치지 않고 표시된다. Spacing 20과 바깥 여백이 보인다.
4. Play 중 `Tools > TxT RPG > Application > Play Mode > Load TMP_MainScene`을 실행하면 `TMP_MainScene`으로 전환되고 기존 화면과 입력이 동작한다.
5. `TMP_MainScene`으로 전환한 뒤 `TemporarySlotMachinePlayValidation`의 Play Mode 검증 메뉴를 실행하면 재진입 단계가 `Candidate_MainScene`을 경유하여 통과한다.
6. Game View를 1920×1080, 2560×1080, 1280×720, 1080×1920, 750×1334 정도로 바꿔 `Candidate_MainScene`의 세 영역이 넓은 화면에서 가로, 너비 720 미만에서 세로로 배치되는지 확인한다. Game View 크기를 임시로 추가했다면 확인 후 제거한다.

### 8.4 정리 확인

- `rg "MainScene_VisualProposal|MainSceneVisualProposal|VisualProposals/MainScene" Assets DOCS ProjectSettings` 결과가 이 지침서, 기록으로 남긴 지침서와 그 안내 문구 외에는 없다.
- 전체 문서 링크가 깨지지 않았다.

## 9. 문서 갱신

| 문서 | 갱신 내용 |
| --- | --- |
| `DOCS/architecture/scene-transition.md` | 초기 콘텐츠가 `Candidate_MainScene`이라는 사실, Play Mode `Load TMP_MainScene` 메뉴, `ConfigureInitialContentScene`의 건너뛰기 동작 |
| `DOCS/architecture/flexible-layout-panel.md` | `Candidate_MainScene`의 `MainArea` 구성(1:3:1, Vertical When Narrow, Spacing·Margins) 적용 예 |
| `DOCS/architecture/project-structure.md` | 제안 Scene·폴더 설명 제거, `Candidate_MainScene`과 `TMP_MainScene`의 역할 |
| `DOCS/architecture/main-scene-default-presentation.md`, `DOCS/architecture/story-text-panel.md` | 제안 Scene을 현재 구성으로 설명하는 문장 수정 |
| `DOCS/architecture/player-session.md` | 초기 콘텐츠 Scene을 설명하는 부분이 있으면 갱신 |
| `DOCS/development/workflows.md` | `Rebuild App Scene` 설명, Play Mode 검증 절차의 `Load TMP_MainScene` 단계, `AssetWriting` 대상 목록과 `MainSceneVisualProposalTests` 설명 제거, 제안 Scene 메뉴 제거, 임시 적용 메뉴를 만든 경우 그 절차 |
| `DOCS/README.md`, `DOCS/instructions/README.md`, `DOCS/instructions/main-scene-visual-improvement-proposal.md` | 5.8절에 따른 링크 제거와 안내 추가 |

## 10. 최종 보고

다음을 구분하여 보고한다.

1. 구현 완료: 코드·테스트·문서 변경 목록
2. 적용 완료: Editor에서 적용하고 저장한 자산과 확인한 값
3. 적용 필요: 사용자가 실행해야 하는 임시 메뉴가 남았다면 `사용자가 수행할 적용 절차` 절과 복사 가능한 순서
4. 검증: 실행한 테스트 필터와 결과, Editor 확인 항목, 확인하지 못한 화면 크기·기기·빌드 대상
5. 직렬화 검토 결과와 되돌린 파일, 작업 전부터 있던 사용자 변경
6. 후속 작업: `Candidate_MainScene`의 Safe Area·CanvasScaler 정책, 게임 패널 배치
