# Edit Mode 테스트 실패 5건 정리

## 1. 목적과 작업 범위

Edit Mode 테스트가 멈추던 문제를 고친 뒤, 멈춤과 관계없이 실패하는 테스트 5건이 드러났다. 이 작업은 5건의 원인을 확정하고, 확정된 방향에 따라 테스트·메뉴·자산을 정리해서 관련 Edit Mode 테스트 어셈블리가 모두 통과하게 만드는 것이 목표다.

이 문서는 구현 지침이다. 아래 원인 분석은 문서 작성 시점의 조사 결과이며, 구현 전에 현재 작업 트리로 다시 확인한다. 문서 작성 단계에서는 코드, Scene, Prefab을 수정하지 않았다.

포함 범위는 다음과 같다.

- 실패 테스트 5건의 원인 확정과 수정
- 3번 항목의 적용 여부 판단과 사용자 확인
- 5번 항목의 메뉴 경로 상수화, 메뉴 테스트 갱신, 임시 메뉴 퇴역 판단
- 관련 문서 갱신과 검증

제외 범위는 다음과 같다.

- `AssetWriting` 분류 테스트, PlayMode 테스트 전체 점검(4번을 PlayMode로 옮기는 경우의 해당 테스트는 예외)
- 테스트가 실행 후 남기는 `Assets/TxTRPG/Content/Tests/Temp` 빈 폴더 문제(아래 10절 참고)
- 주사위 던지기 등 다른 진행 중인 기능 작업

## 2. 사용자 확정 사항

| 항목 | 확정 방향 |
| --- | --- |
| 1번 `InitializerFailure_UnloadsFailedInitialContentScene` | 테스트 환경을 런타임과 같게 만들어서, "초기 로드 중 초기화가 실패하면 콘텐츠 Scene이 비어 있다"는 원래 기대를 검증한다. 운영 코드는 바꾸지 않는다. |
| 2번 `UnitySceneLoader_AcceptsEnabledFullPathAndRejectsLegacyName` | 먼저 원인을 확인한다. 운영 코드는 그대로 두고, 테스트를 Edit Mode에서 검증할 수 있는 범위로 바꾼다. 실제 Build Settings 포함 여부는 PlayMode나 빌드 검증에 맡긴다. |
| 3번 `ConfiguredScene_UsesProductionControllerAndNoEnemyDemoWriter` | 작업 세션이 먼저 조사하고, `TMP_MainScene`에 적용할지 테스트를 바꿀지 사용자에게 확인받은 뒤 진행한다. |
| 4번 `ActiveUnconfiguredPanel_ReportsMissingReferencesOnceWithoutException` | 테스트만 고친다. `StoryTextPanel`에 `[ExecuteAlways]`를 추가하지 않는다. |
| 5번 `TxTRPGMenus_UseExpectedHierarchyAndPriorities` | 영구 메뉴는 `TxTRPGEditorMenuPaths` 상수로 바꾸고 테스트 목록에 넣는다. `Temporary` 메뉴는 적용 완료 여부를 조사해서 퇴역할 수 있으면 삭제를 제안하고, 사용자 확인을 받은 뒤 삭제한다. |

## 3. 선행 문서와 확인한 현재 구조

작업 전 `AGENTS.md`, `DOCS/README.md`와 아래 문서를 읽는다. 특히 `AGENTS.md`의 `Edit Mode Test Scope` 규칙을 따른다.

- `DOCS/development/workflows.md`의 "테스트"와 "자산을 다시 쓰는 테스트와 실행 범위"
- `DOCS/architecture/scene-transition.md`
- `DOCS/architecture/story-text-panel.md`
- `DOCS/architecture/project-structure.md`의 `Assets/TxTRPG/Editor/Common/Menu` 설명
- `DOCS/instructions/main-scene-default-content-and-fallbacks.md` (3번 조사용)

문서 작성 시점의 테스트 결과는 다음과 같다. 모두 배치 모드, `!AssetWriting` 필터로 실행했다.

| 어셈블리 | 결과 |
| --- | --- |
| `TxTRPG.Application.Tests.Editor` | 81개 중 80개 통과 (3번 실패) |
| `TxTRPG.SceneTransition.Tests.Editor` | 15개 중 13개 통과 (1·2번 실패) |
| `TxTRPG.UI.Tests.Editor` | 188개 중 187개 통과 (4번 실패) |
| `TxTRPG.Editor.Tests.Editor` | 5개 중 4개 통과 (5번 실패) |
| `TxTRPG.Gameplay.Tests.Editor`, `TxTRPG.Content.Tests.Editor` | 모두 통과 |

CodeGraph로 각 테스트 대상의 호출자와 관련 테스트를 탐색하고, 실제 소스와 직렬화 자산으로 확인한다.

## 4. 항목별 요구 사항

### 4.1 1번: `SceneTransitionTests.InitializerFailure_UnloadsFailedInitialContentScene`

확인한 원인은 다음과 같다.

- `SceneTransitionService.Awake`가 `appScene = gameObject.scene`을 설정한다. 이 클래스에는 `[ExecuteAlways]`가 없으므로, Edit Mode 테스트에서 `AddComponent`로 만든 서비스는 `Awake`가 실행되지 않고 `appScene`이 기본값으로 남는다.
- 그 결과 `ResolveCurrentContentScene`이 Editor의 활성 Scene을 원래 콘텐츠 Scene(`sourceScene`)으로 판단한다. 초기화가 실패하면 `RecoverSceneStateAsync`가 그 Scene을 `currentContentScene`으로 되돌리므로 마지막 검증(`CurrentContentScene.IsValid()`가 `False`)이 실패한다.
- 런타임에서는 `Awake`가 실행되므로 이 조건이 생기지 않는다. 이 테스트는 이전에 `Assert.ThrowsAsync` 교착으로 멈춰서 마지막 검증까지 실행된 적이 없다.

요구 사항은 다음과 같다.

- 테스트 준비 단계(`CreateServiceFixture` 또는 이 테스트)에서 서비스가 런타임과 같은 App Scene 상태를 갖게 한다. 방법은 `Awake` 생명주기를 명시적으로 실행하는 것을 우선 검토한다.
- 운영 코드의 동작은 바꾸지 않는다. 테스트 전용 진입점이 꼭 필요하면 기존 관례(`#if UNITY_EDITOR`의 `ConfigureForEditor` 등)를 따르고, 런타임 동작에 영향이 없음을 확인한다.
- 같은 픽스처를 쓰는 다른 `SceneTransitionTests`가 계속 통과하는지 확인한다. 픽스처 변경이 다른 테스트의 전제를 바꾸면 그 영향을 보고한다.

### 4.2 2번: `SceneTransitionTests.UnitySceneLoader_AcceptsEnabledFullPathAndRejectsLegacyName`

확인한 사실은 다음과 같다.

- `UnitySceneLoader.ValidateScenePath`는 런타임 API `Application.CanStreamedLevelBeLoaded`로 Build Settings 포함 여부를 검사한다.
- 테스트는 `EditorBuildSettings.scenes`를 바꾼 뒤 이 검사를 호출한다. `ProjectSettings/EditorBuildSettings.asset`에도 `Assets/Scenes/TMP_MainScene.unity`가 활성화되어 있다. 그런데도 배치 모드 Edit Mode에서 "Build Settings에 포함·활성화되어 있지 않다"는 예외가 발생했다.
- 추정 원인은 이 런타임 API가 Edit Mode에서 Editor의 Build Settings 목록을 반영하지 않는다는 것이다. 아직 공식 문서와 Test Runner 창으로 확인하지 않았다.

요구 사항은 다음과 같다.

- 먼저 Unity `6000.3` 공식 문서와 Test Runner 창 실행으로 원인을 확정한다. 배치 모드에서만 실패하는지도 기록한다.
- 운영 코드(`UnitySceneLoader`)는 바꾸지 않는다.
- 테스트는 Edit Mode에서 의미 있게 검증할 수 있는 범위로 바꾼다. 예를 들어 전체 경로 형식 요구와 이전 Scene 이름(`TMP_MainScene`) 거부는 유지한다. Build Settings 포함 여부 검증은 이미 Edit Mode에서 검증하는 `BuildScenePathUtility` 테스트가 담당하는지 확인하고, 빠지는 범위를 보고한다.
- 원인 확인 결과가 추정과 다르면, 수정 전에 확인한 사실과 대안을 사용자에게 보고한다.

### 4.3 3번: `MainSceneDefaultPresentationTests.ConfiguredScene_UsesProductionControllerAndNoEnemyDemoWriter`

확인한 사실은 다음과 같다.

- `MainScenePresentationController`(`Assets/TxTRPG/Application/Runtime/Presentation/MainScenePresentationController.cs`, guid `9f45369dfbfc5114491194bad35dd4bc`)는 현재 `Assets/Scenes/TMP_MainScene.unity`에 없다.
- `git log -S`로 확인하면 이 컨트롤러가 `TMP_MainScene`에 커밋된 적이 한 번도 없다. 이 컨트롤러를 쓰는 저장 자산은 `Assets/Scenes/MainScene_VisualProposal.unity`뿐이다.
- 컨트롤러를 Scene에 추가하는 경로는 `Tools/TxT RPG/Application/Configure Main Scene Default Content`(`MainSceneDefaultContentBuilder.Configure` → `ConfigureScene`)이다. 이 메뉴는 Scene을 저장한다.
- 테스트는 2026-09-11 커밋 `e6881fc`에서 추가되었다. `TMP_MainScene`에는 현재 커밋되지 않은 변경이 있을 수 있다.

요구 사항은 다음과 같다.

- 컨트롤러의 책임, `MainSceneDefaultContentBuilder.ConfigureScene`이 바꾸는 범위, 현재 `TMP_MainScene` 구성, 관련 지침서와 아키텍처 문서를 조사한다.
- 조사 결과를 바탕으로 "운영 Scene에 적용" 또는 "테스트를 현재 구성에 맞게 수정" 중 권장안을 정하고, 근거와 각 선택의 영향을 사용자에게 한국어로 질문해서 확인받는다. 답을 받기 전에는 Scene이나 테스트를 바꾸지 않는다.
- 적용으로 결정되면 전체 Scene 재생성이 아니라 필요한 부분만 적용한다. 작업 전에 `TMP_MainScene`의 미커밋 변경과 Editor의 미저장 상태를 확인하고 보존한다. 적용 후 Scene diff를 검토하고, 관련 Play Mode 경로에서 표시를 확인한다.
- 테스트 수정으로 결정되면 테스트가 실제 운영 구성의 핵심 계약(데모 로더 없음, `StoryTextPanel`과 `ActionGridPanel` 존재 등)을 계속 검증하도록 한다.

### 4.4 4번: `StoryTextPanelTests.ActiveUnconfiguredPanel_ReportsMissingReferencesOnceWithoutException`

확인한 원인은 다음과 같다.

- 경고는 `StoryTextPanel.OnEnable` → `TryPrepareRequiredReferences` → `LogMissingRequiredReferences`에서 출력된다.
- `StoryTextPanel`에는 `[ExecuteAlways]`가 없으므로, Edit Mode 테스트에서 `SetActive(true)`를 호출해도 `OnEnable`이 실행되지 않는다. 테스트와 구현은 같은 커밋 `593e8cf`(2026-09-13)에서 추가되었으며, Edit Mode에서는 처음부터 통과할 수 없는 구조로 보인다.

요구 사항은 다음과 같다.

- 운영 코드에 `[ExecuteAlways]`나 Edit Mode 전용 동작을 추가하지 않는다.
- 테스트가 실제로 `OnEnable` 경로를 거치도록 바꾼다. `Assets/TxTRPG/UI/Tests/PlayMode/`의 PlayMode 테스트로 옮기는 방법과, Edit Mode에서 생명주기를 명시적으로 실행하는 방법 중 하나를 고르고 이유를 기록한다.
- "참조 누락 경고를 한 번만 출력하고 예외를 던지지 않는다"는 원래 검증 의도를 유지한다. PlayMode로 옮기면 `AGENTS.md`와 메모리에 기록된 PlayMode 실행 주의 사항(AppScene 시작 정책과 Test Runner 충돌)을 확인한다.

### 4.5 5번: `EditorMenuTests.TxTRPGMenus_UseExpectedHierarchyAndPriorities`

확인한 사실은 다음과 같다. 테스트는 Content Editor, Editor Common, UI Editor, SceneTransition Editor 어셈블리에서 `Tools/TxT RPG/`로 시작하는 `MenuItem`을 모아 기대 목록(26개)과 비교한다. 실제로는 35개이며, 다음 9개가 목록에 없다. 모두 이미 커밋된 코드다.

| 파일 | 메뉴 경로 | 경로 방식 | 성격 |
| --- | --- | --- | --- |
| `ActionGridPrefabBuilder.cs` | `UI/Prefabs/Upgrade Action Grid Cell Icon Layout` | 상수 사용 | 영구 |
| `ActionGridPrefabBuilder.cs` | `UI/Prefabs/Upgrade Action Grid Cell Empty Slot Layout` | 상수 사용 | 영구 |
| `ActionGridPrefabBuilder.cs` | `UI/Prefabs/Upgrade Action Grid Cell Ratio Capped Layout` | 상수 사용 | 영구 |
| `ExplorationCardRuntimeVerification.cs` | `UI/Exploration/Temporary/Verify Card Runtime Lifecycle` | 문자열 직접 | 임시 |
| `ExplorationCardRuntimeVerification.cs` | `UI/Exploration/Temporary/Capture Runtime Card States` | 문자열 직접 | 임시 |
| `ExplorationCardRuntimeVerification.cs` | `UI/Exploration/Temporary/Inspect Runtime Card Text` | 문자열 직접 | 임시 |
| `ExplorationNodeChoiceCardPrefabBuilder.cs` | `UI/Exploration/Temporary/Upgrade Card Readability And Pulse` | 문자열 직접 | 임시 |
| `ExplorationNodeChoiceCardPrefabBuilder.cs` | `UI/Exploration/Rebuild Node Choice Card Prefab` | 문자열 직접 | 영구 |
| `KoreanFontFallbackBuilder.cs` | `UI/Fonts/Apply Korean Font Fallback` | 문자열 직접 | 영구 |

`DOCS/architecture/project-structure.md`는 "각 기능의 Editor 어셈블리는 `TxTRPGEditorMenuPaths` 상수를 참조하므로 메뉴 계층과 정렬 정책을 개별 문자열로 중복하지 않는다"고 규정한다. 문자열을 직접 쓴 6개는 이 규칙에 어긋난다.

요구 사항은 다음과 같다.

- 영구 메뉴는 `TxTRPGEditorMenuPaths`의 기존 또는 새 상수를 사용하게 바꾼다. 사용자가 보는 메뉴 경로 문자열은 바꾸지 않는다. 우선순위는 `TxTRPGEditorMenuPriorities`의 의미에 맞춰 지정한다.
- 영구 메뉴를 `EditorMenuTests`의 기대 목록에 추가한다.
- `Temporary` 메뉴 4개는 관련 지침서(`exploration-card-readability-and-hover-pulse.md` 등)와 `DOCS/development/workflows.md`에서 적용 완료 여부와 퇴역 조건을 확인한다. 퇴역할 수 있으면 근거와 함께 삭제를 사용자에게 제안하고, 확인을 받은 뒤 삭제한다. 확인을 받기 전에는 삭제하지 않는다.
- 남기기로 한 임시 메뉴도 상수 경로를 쓰게 하고 테스트 목록에 넣는다. 테스트에서 임시 메뉴를 검증 대상에서 빼는 방식은 사용하지 않는다.
- 메뉴를 삭제하면 해당 메뉴를 설명하는 문서와 연결된 테스트를 함께 정리한다.

## 5. 주요 경로

문서 작성 시 아래 경로를 확인했다.

- `Assets/TxTRPG/SceneTransition/Tests/Editor/SceneTransitionTests.cs`
- `Assets/TxTRPG/SceneTransition/Runtime/SceneTransitionService.cs`
- `Assets/TxTRPG/SceneTransition/Runtime/UnitySceneLoader.cs`
- `Assets/TxTRPG/Application/Tests/Editor/MainSceneDefaultPresentationTests.cs`
- `Assets/TxTRPG/Application/Editor/MainSceneDefaultContentBuilder.cs`
- `Assets/TxTRPG/Application/Runtime/Presentation/MainScenePresentationController.cs`
- `Assets/Scenes/TMP_MainScene.unity`
- `Assets/TxTRPG/UI/Tests/Editor/StoryTextPanelTests.cs`
- `Assets/TxTRPG/UI/Runtime/StoryTextPanel.cs`
- `Assets/TxTRPG/UI/Tests/PlayMode/`
- `Assets/TxTRPG/Editor/Tests/Editor/EditorMenuTests.cs`
- `Assets/TxTRPG/Editor/Common/Menu/TxTRPGEditorMenuPaths.cs`, `TxTRPGEditorMenuPriorities.cs`
- `Assets/TxTRPG/UI/Editor/ActionGridPrefabBuilder.cs`
- `Assets/TxTRPG/UI/Editor/ExplorationCardRuntimeVerification.cs`
- `Assets/TxTRPG/UI/Editor/ExplorationNodeChoiceCardPrefabBuilder.cs`
- `Assets/TxTRPG/UI/Editor/KoreanFontFallbackBuilder.cs`

## 6. 호환성과 실패 시 동작

- 런타임 동작과 저장 데이터는 바꾸지 않는다. 3번에서 Scene을 적용하는 경우만 예외이며, 이때도 사용자 확인을 받은 범위만 바꾼다.
- 메뉴 경로 문자열은 유지한다. 퇴역하는 임시 메뉴를 제외하면 사용자가 쓰던 메뉴가 사라지거나 위치가 바뀌지 않아야 한다.
- 비동기 작업을 기다리는 테스트는 `Assert.ThrowsAsync`나 `Assert.DoesNotThrowAsync`를 쓰지 않는다. `DOCS/development/workflows.md`의 규칙과 `WaitForCompletion` 헬퍼 방식을 따른다.
- 테스트가 계속 실패하거나 원인이 이 문서의 분석과 다르면, 임의로 기대값을 완화하지 말고 확인한 사실을 보고한다.

## 7. 검증

1. 작업 전 `git status`를 기록한다. 실행할 Unity Editor가 프로젝트를 열고 있는지 확인한다. 배치 모드는 Editor가 같은 프로젝트를 열고 있으면 실행할 수 없다.
2. 수정한 테스트 클래스를 이름 필터로 하나씩 실행해서 통과를 확인한다.
3. 아래 어셈블리를 `-testCategory "!AssetWriting"`으로 각각 실행해서 실패 0건을 확인한다. 실행마다 제한 시간을 두고, 멈추면 `AGENTS.md` 규칙대로 보고한다.
   - `TxTRPG.Application.Tests.Editor`
   - `TxTRPG.SceneTransition.Tests.Editor`
   - `TxTRPG.UI.Tests.Editor`
   - `TxTRPG.Editor.Tests.Editor`
   - `TxTRPG.Gameplay.Tests.Editor`, `TxTRPG.Content.Tests.Editor` (회귀 확인)
4. 4번을 PlayMode로 옮겼다면 그 PlayMode 테스트를 실행한다. 실행할 수 없으면 원인과 대체 검증 방법을 기록한다.
5. 3번에서 Scene을 적용했다면 Scene diff를 검토하고, AppScene 시작 경로로 `TMP_MainScene`에 진입해서 표시를 확인한다.
6. 테스트 실행 뒤 작업과 관계없는 파일이 바뀌었으면, 실행 전에 깨끗했던 파일만 되돌린다. `Content` 테스트는 `AddressableAssetSettings.asset`의 해시와 `Assets/TxTRPG/Content/Tests/Temp` 빈 폴더를 남길 수 있다.
7. 2번의 원인 확인을 Test Runner 창에서 했는지, 배치 모드에서만 했는지 구분해서 기록한다.

## 8. 문서 갱신

- `DOCS/development/workflows.md`: 메뉴 상수 추가, 퇴역한 임시 메뉴, 테스트 위치 변경(4번을 PlayMode로 옮긴 경우), 2번의 Edit Mode 검증 한계를 반영한다.
- `DOCS/architecture/project-structure.md`: 메뉴 경로 상수가 늘어나면 메뉴 설명을 갱신한다.
- `DOCS/architecture/scene-transition.md`, `DOCS/architecture/story-text-panel.md`: 테스트가 검증하는 계약이 바뀌면 갱신한다.
- 3번에서 Scene을 적용했다면 관련 아키텍처 문서에 `TMP_MainScene`의 실제 구성을 기록한다.
- 이 지침서와 다른 지침서는 수정하지 않는다.

## 9. 최종 보고

다음을 구분해서 보고한다.

- 항목별 확정 원인, 수정 내용, 수정 경로
- 3번과 5번에서 사용자에게 확인받은 결정과 그 근거
- 2번의 원인 확인 방법과 결과(공식 문서, Test Runner 창, 배치 모드)
- 실행한 테스트 어셈블리별 결과와 실행 시간, 실행하지 못한 테스트와 이유
- 테스트 실행이 바꾼 파일과 복구 내용
- 남은 수동 적용 절차. 남은 Tools 단계가 있으면 `AGENTS.md`에 따라 `사용자가 수행할 적용 절차`를 `text` 코드 블록으로 제공하고, 없으면 없다고 명시한다.

## 10. 참고: 범위 밖으로 남겨 둔 문제

`CharacterContentAssetFactoryTests`는 `Assets/TxTRPG/Content/Tests/Temp/CharacterAuthoring_<GUID>` 폴더를 만들고 하위 폴더만 지운다. 그래서 실행할 때마다 빈 `Temp` 폴더와 `Temp.meta`가 남고, `AddressableAssetSettings.asset`의 해시도 바뀐다. 이 작업에서는 고치지 않는다. 필요하면 별도 작업으로 다룬다.
