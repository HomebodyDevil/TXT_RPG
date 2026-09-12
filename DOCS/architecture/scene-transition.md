# Scene Transition 설계

## 목적

`SceneTransitionService`는 항상 로드되어 있는 실제 `AppScene`에서 콘텐츠 Scene 교체를 조율합니다. 전역 화면 전환 UI는 `DontDestroyOnLoad`의 숨겨진 런타임 Scene이나 `Resources.Load` 자동 생성에 의존하지 않습니다. Hierarchy와 Build Settings에서 수명과 소유권을 직접 확인할 수 있습니다.

```text
AppScene                                      항상 유지
└── AppRoot                                   AppRoot.prefab 인스턴스
    ├── AppSceneRoot
    ├── SceneTransitionService
    ├── UnitySceneLoader
    └── TransitionCanvas                      Screen Space - Overlay, Order 32760
        ├── InputBlocker
        ├── TransitionImage                   FadeScreenTransitionEffect
        ├── EffectLayer
        ├── LoadingIndicator
        └── ErrorFallback

Content Scene                                 Additive로 하나씩 교체
└── ISceneInitializer / ISceneReadySource
```

제품 실행 경로는 Build Settings 0번인 `Assets/Scenes/AppScene.unity`에서 시작합니다. `AppSceneRoot`는 기본 콘텐츠인 `Assets/Scenes/TMP_MainScene.unity`를 로드합니다. 런타임 데이터는 Scene 이름이 아니라 전체 프로젝트 경로를 사용하므로, 같은 이름을 가진 Scene이 여러 폴더에 있어도 대상을 모호하지 않게 식별합니다.

## Editor Play 시작 정책

`EditorPlayStartPolicy`는 Editor에서 어떤 Scene을 열어 둔 상태인지와 관계없이 일반 Play를 `Assets/Scenes/AppScene.unity`에서 시작하게 합니다. 이 정책은 `EditorSceneManager.playModeStartScene`만 설정하며, AppRoot를 복제하거나 콘텐츠 Scene에 별도 Bootstrap을 추가하지 않습니다. 따라서 `TMP_MainScene`을 편집하다가 Play해도 제품과 동일하게 AppScene의 `PlayerSessionHost`, `SceneTransitionService`, 초기 콘텐츠 로드 순서를 거칩니다. Play를 종료하면 Unity가 원래 열려 있던 Scene 구성과 활성 Scene을 복원합니다.

프로젝트별 활성 상태와 정책이 소유한 이전 시작 Scene GUID는 `ProjectSettings/TxTRPGEditorPlaySettings.asset`에 저장됩니다. 자동 초기화는 이미 다른 시작 Scene이 지정되어 있으면 이를 덮어쓰지 않습니다. 개발자가 Project Settings에서 명시적으로 정책을 적용한 경우에만 이전 값을 백업하며, 정책을 끌 때 현재 값이 여전히 AppScene인 경우에만 이전 값으로 복원합니다. 정책 적용 후 개발자가 다시 바꾼 시작 Scene은 오래된 백업으로 덮어쓰지 않습니다.

Play 직전에는 AppScene 자산, Build Settings 첫 활성 Scene, AppRoot의 `Load Initial Content On Start`, 최초 콘텐츠 경로를 검증합니다. Scene에 미저장 변경이 있으면 Unity의 저장 확인을 사용하고, 취소하면 Play도 취소합니다. 저장되지 않은 Prefab Stage는 디스크에서 로드되는 결과와 다를 수 있으므로 Play를 중단하고 저장을 요청합니다. Unity Test Runner의 Play Mode 실행에는 이 정책을 적용하지 않습니다.

이 기능은 Editor 편의를 위한 정책입니다. Player 빌드의 시작 순서와 런타임 Scene 전환 계약은 계속 Build Settings와 `AppSceneRoot`가 결정합니다.

## Scene 경로 작성과 검증

`AppSceneRoot.initialContentScenePath`에는 `[ScenePath(excludeAppScene: true)]`가 적용되어 있습니다. Inspector에서는 직접 문자열을 입력하는 대신 Build Settings에 등록되어 있고 활성화된 Scene을 `Scene 이름 (전체 경로)` 형식의 목록에서 선택합니다. AppScene은 콘텐츠 대상으로 선택할 수 없습니다.

잘못된 기존 값은 자동으로 다른 Scene으로 바꾸지 않습니다. Inspector가 원래 문자열을 `<Invalid>`로 보존하고 원인을 표시하므로, 개발자가 누락되거나 비활성화된 Scene을 확인할 수 있습니다. 기존 `initialContentScene` 직렬화 필드는 `FormerlySerializedAs`로 마이그레이션하며, Prefab Builder는 과거의 Scene 이름이 활성화된 Build Settings Scene 하나와 유일하게 일치할 때에만 전체 경로로 변환합니다. 유일하게 결정할 수 없는 값은 기본 콘텐츠 경로로 복구합니다.

빌드 전 `AppSceneBuildValidator`는 다음 구성을 검사하며, 오류가 있으면 빌드를 중단합니다.

- AppScene이 Build Settings의 첫 번째 활성 Scene인지 확인합니다.
- Scene 경로가 실제 `.unity` 자산이고 Build Settings에 정확히 한 번 등록되어 활성화되었는지 확인합니다.
- 최초 콘텐츠 Scene이 AppScene과 다른지 확인합니다.
- Build Settings에 중복 Scene 경로가 없는지 확인합니다.

`UnitySceneLoader`도 로드 직전에 전체 `Assets/.../*.unity` 경로와 Player에서 로드 가능한지를 검사합니다. 이 검사는 Editor 작성 오류가 빌드를 통과하지 못하게 하는 검증을 대체하지 않으며, 손상되거나 외부에서 바뀐 런타임 구성에 대한 마지막 방어선입니다. Addressables Scene은 Build Settings 경로와 수명 규칙이 다르므로 현재 문자열 필드에 혼합하지 않습니다. 향후 별도의 참조 형식과 `ISceneLoader` 구현으로 추가합니다.

로드 완료 결과와 이미 로드된 Scene 검사는 `SceneManager.GetSceneByPath()` 및 `Scene.path`를 사용합니다. 파일명만 비교하지 않으므로 서로 다른 폴더의 같은 이름을 가진 Scene도 전체 경로로 구분합니다. 경로 구분자는 `/`로 정규화하며 비교 시 대소문자 차이는 허용합니다.

## 전환 흐름

최초 콘텐츠 Scene은 첫 화면이 렌더링되기 전에 완전히 가려진 상태로 준비합니다.

```text
AppScene Awake
→ TransitionImage 즉시 불투명
→ 입력 차단
→ 최초 Content Scene Additive 로드
→ Content Scene 활성화
→ ISceneInitializer 실행
→ ISceneReadySource 대기
→ Canvas 레이아웃 확정과 한 프레임 대기
→ Fade In
→ 입력 차단 해제
```

이후 전환은 다음 순서를 사용합니다.

```text
Fade Out 또는 즉시 Cover
→ 새 Content Scene Additive 로드
→ SceneManager.SetActiveScene
→ ISceneInitializer를 InitializationOrder 순서로 실행
→ ISceneReadySource 대기
→ 이전 Content Scene 언로드
→ Canvas.ForceUpdateCanvases
→ 한 프레임 대기 후 다시 레이아웃 확정
→ Fade In 또는 즉시 Reveal
```

대상 Scene의 Initializer와 Ready Source가 모두 완료되면 전환을 커밋합니다. `LoadThenUnload`에서는 이 커밋 직후 기존 Scene 언로드를 시작합니다. 커밋 전 실패나 취소는 기존 Scene을 다시 활성화하고 대상 Scene을 정리합니다. 커밋 후에는 기존 Scene이 이미 언로드되었을 수 있으므로 대상 Scene을 활성 콘텐츠로 유지하며, 레이아웃 대기나 Reveal 중 실패·취소가 발생해도 대상 Scene을 언로드하지 않습니다.

`ContentSceneSwapMode.LoadThenUnload`가 기본값입니다. 이전 Scene을 유지한 채 새 Scene을 준비하므로 실패 시 기존 화면으로 복구할 수 있지만, 전환 중 두 Scene의 메모리가 일시적으로 함께 필요합니다. 모바일처럼 메모리 상한이 더 중요한 화면은 `UnloadBeforeLoad`를 선택할 수 있습니다. 이 모드에서는 로드 실패 후 복구할 이전 Scene이 없을 수 있으므로 제품 수준의 오류 화면과 재시도 경로가 필요합니다.

## 초기화와 준비 완료 계약

Scene 파일의 로드 완료와 콘텐츠 준비 완료는 별개의 상태입니다.

| 형식 | 책임 |
| --- | --- |
| `ISceneInitializer` | Addressables, 저장 데이터, 현지화, 동적 UI 생성처럼 서비스가 시작해야 하는 초기화를 수행합니다. |
| `InitializationOrder` | 초기화 실행 순서를 결정합니다. 값이 작은 구현부터 순차 실행하므로 명시적인 의존 관계를 구성할 수 있습니다. |
| `SceneInitializationContext` | 새 Scene, 이전 Scene, 최초 로드 여부와 진행률 보고 대상을 제공합니다. |
| `ISceneReadySource` | 다른 시스템이 이미 시작한 필수 비동기 작업의 완료 Task를 제공합니다. |
| `SceneReadySignal` | 외부 관리자가 `MarkReady()`로 완료하는 기본 준비 신호입니다. |

서비스는 대상 Scene의 비활성 GameObject도 검색합니다. 초기화 구현은 전달된 `CancellationToken`을 지켜야 하며 `InitializeAsync`에서 `null` Task를 반환하면 구성 오류로 처리됩니다. 모든 Initializer와 Ready Source에는 Profile의 `Readiness Timeout`이 적용됩니다.

비동기 초기화가 없는 Scene도 로드 직후 한 프레임을 기다려 `Start`가 실행되게 하고, Canvas 레이아웃을 갱신한 뒤 표시합니다.

## 책임과 확장 지점

| 형식 | 책임 |
| --- | --- |
| `AppSceneRoot` | AppScene 인스턴스의 유일성을 확인하고 최초 콘텐츠 로드를 시작합니다. |
| `SceneTransitionService` | 전환 상태, 중복 요청, 활성 Scene, 초기화, 언로드, 취소와 실패 복구를 조율합니다. |
| `ISceneLoader` | Additive 로드, 언로드와 활성 Scene 지정을 추상화합니다. |
| `UnitySceneLoader` | Unity `LoadSceneAsync`, `UnloadSceneAsync`, `SetActiveScene`을 구현합니다. |
| `ScenePathAttribute` | 런타임에는 전체 Scene 경로 문자열을 유지하면서 Editor 선택 정책을 선언합니다. |
| `BuildScenePathUtility` | Build Settings Scene 목록, 기존 이름 마이그레이션과 경로 검증을 제공합니다. |
| `AppSceneBuildValidator` | Player 빌드 전에 AppScene과 최초 콘텐츠 Scene 구성을 검증합니다. |
| `IScreenTransitionEffect` | Cover, Reveal, 즉시 가림과 즉시 표시 계약을 정의합니다. |
| `FadeScreenTransitionEffect` | 전체 화면 Image의 알파를 프레임 시간 상한과 Unscaled Time 정책으로 변경합니다. |
| `SceneTransitionProfile` | Fade 사용 여부, 색상, 시간, 로드 중 가림, Scene 교체 순서, 입력 차단, Timeout과 Reduce Motion을 설정합니다. |

다른 화면 효과는 `ScreenTransitionEffect`를 상속하여 추가합니다. Scene 흐름은 Fade, Wipe, Material 또는 Sprite Animation 구현을 직접 참조하지 않습니다.

## Profile 옵션

- `Fade Out Enabled`를 끄고 `Cover During Load`를 켜면 로드 전에 설정 색상으로 즉시 가립니다.
- `Fade In Enabled`를 끄면 준비 완료 후 즉시 표시합니다.
- `Cover During Load`와 `Fade Out Enabled`를 모두 끄면 로딩 과정을 화면에 노출합니다.
- `Block Input`은 전환 동안 전체 화면 Raycast를 차단합니다.
- `Maximum Frame Delta`는 비정상적으로 큰 한 프레임이 Fade를 건너뛰지 않게 합니다.
- `Reduced Motion Mode`는 짧은 Fade 또는 즉시 전환으로 대체합니다.

Fade는 첫 애니메이션 프레임을 정확한 진행률 0으로 한 번 렌더링한 뒤 경과 시간을 누적합니다.

## 실패와 취소

- 전환 중 두 번째 요청은 `InvalidOperationException`으로 거부합니다. 현재 구현에는 요청 대기열이 없습니다.
- 새 Scene을 로드한 뒤 초기화가 실패하거나 취소되었고 이전 Scene이 아직 로드되어 있으면, 이전 Scene을 다시 활성화하고 실패한 새 Scene을 언로드합니다.
- 대상 Scene 준비가 완료되어 전환이 커밋된 뒤 실패하거나 취소되면, 기존 Scene을 복원하려 하지 않고 대상 Scene을 현재 콘텐츠로 유지합니다.
- `UnloadBeforeLoad`에서 이전 Scene을 이미 제거했거나 로드 자체가 실패하면 `ErrorFallback`을 표시하고 예외를 호출자에게 전달합니다.
- 모든 실패와 취소 경로는 Transition Image를 투명 상태로 복구하고 입력 차단을 해제합니다.
- Unity Scene 비동기 연산은 시작 후 취소할 수 없습니다. 로더는 연산이 끝날 때까지 기다리고, 서비스가 결과 Scene을 정리할 기회를 가진 뒤 취소를 관찰합니다.

## Panel Fade와의 관계

Scene 전체 Fade가 초기 표시를 담당하므로 `StoryTextPanel.revealInitialMessages`, `FadePanelRevealTransition.animateReveal`, `CharacterViewBase.animateVisibility`의 기본값은 꺼져 있습니다. 새 문장, 캐릭터 교체와 같은 Scene 내부의 국소 연출에는 필요한 옵션만 다시 켤 수 있습니다. `PanelBackgroundRenderer`의 배경 교차 Fade와 StoryTextPanel의 위치 기반 투명도는 Scene 전환과 목적이 다르므로 유지합니다.

## 직렬화 호환성과 레거시 자산

기존 `PersistentAppRoot` 클래스와 `Assets/TxTRPG/SceneTransition/Resources/TxTRPG/PersistentAppRoot.prefab`은 기존 GUID 참조를 깨뜨리지 않기 위한 레거시 호환 자산으로 남아 있습니다. 클래스에는 자동 Bootstrap과 `DontDestroyOnLoad` 동작이 없으므로 새 실행 경로에서는 사용되지 않습니다. 기존 Scene에 수동 배치된 인스턴스가 있다면 AppScene 전환 후 제거해야 합니다.

## 향후 작업

- Addressables 기반 Content Scene Loader
- EffectLayer를 사용하는 Wipe·Material·Sprite Sheet 효과
- LoadingIndicator와 ErrorFallback의 제품 UI
- 실제 두 제품 Scene 사이의 Play Mode 전환 테스트
- 플랫폼 설정과 연동된 Reduce Motion Provider

이 항목들은 현재 구현된 AppScene, Additive 교체, 초기화 Pipeline과 구분되는 후속 확장 범위입니다.
