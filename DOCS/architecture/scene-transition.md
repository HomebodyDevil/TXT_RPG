# Scene Transition 설계

## 목적

`SceneTransitionService`는 Scene 교체 과정 전체를 영속적인 전체 화면 Canvas로 가립니다. 개별 Panel은 데이터·Addressables·레이아웃 준비 책임을 계속 유지하지만, Scene 시작 시 Fade를 중복 실행하지 않습니다.

```text
전환 요청
→ 중복 요청 거부
→ 입력 차단
→ 화면 Cover
→ Scene 비동기 로드 및 활성화
→ 대상 Scene의 ISceneReadySource 대기
→ 최소 가림 시간 보장
→ 화면 Reveal
→ 입력 차단 해제
```

## 런타임 구조

```text
PersistentAppRoot                         DontDestroyOnLoad
├── PersistentAppRoot
├── SceneTransitionService
├── UnitySceneLoader
└── TransitionCanvas                     Screen Space - Overlay, Order 32760
    ├── InputBlocker
    ├── TransitionImage                  FadeScreenTransitionEffect
    ├── EffectLayer                      향후 Wipe·Material 효과용
    ├── LoadingIndicator                 향후 로딩 표시용
    └── ErrorFallback
```

`PersistentAppRoot`는 `RuntimeInitializeOnLoadMethod(BeforeSceneLoad)`에서 `Resources/TxTRPG/PersistentAppRoot`를 한 번만 생성합니다. 개발자가 임의의 Scene부터 직접 실행해도 같은 경로가 동작합니다. Resource Prefab이 누락되면 동일한 기본 계층을 런타임에 구성하므로 게임 시작 자체가 차단되지는 않습니다. 중복 인스턴스는 파괴하며 `DontDestroyOnLoad`는 루트 하나에만 적용합니다.

현재 프로젝트에는 별도의 Bootstrap Scene을 추가하지 않았습니다. 정식 시작 Scene을 도입할 때에도 Bootstrap에는 같은 Persistent Prefab만 배치하며, 자동 생성된 인스턴스가 이미 있으면 중복 인스턴스가 제거됩니다.

## 책임과 확장 지점

| 형식 | 책임 |
| --- | --- |
| `SceneTransitionService` | 전환 상태, 중복 요청, 취소, 입력 차단, 준비 대기와 실패 복구를 조율합니다. |
| `ISceneLoader` | Scene 로딩 방식을 분리합니다. 기본 구현은 `UnitySceneLoader`입니다. |
| `IScreenTransitionEffect` | Cover, Reveal, 즉시 복구 계약을 정의합니다. |
| `FadeScreenTransitionEffect` | 전체 화면 Image의 알파를 Unscaled Time 기준으로 변경합니다. |
| `SceneTransitionProfile` | 색상, 시간, 입력 차단, 준비 제한 시간과 Reduce Motion 대체 동작을 설정합니다. |
| `ISceneReadySource` | 대상 Scene의 필수 초기화가 완료되는 Task를 제공합니다. |
| `SceneReadySignal` | 외부 초기화 관리자가 명시적으로 완료할 수 있는 기본 준비 신호입니다. |

다른 효과는 `ScreenTransitionEffect`를 상속하여 추가합니다. Scene 로딩 코드는 Fade, Wipe, Material 또는 로딩 화면 구현을 직접 참조하지 않습니다.

## 실패와 취소

- 전환 중 두 번째 요청은 `InvalidOperationException`으로 거부합니다. 요청 대기열은 현재 구현 범위에 포함하지 않습니다.
- 모든 실패와 취소 경로는 `finally`에서 전환 효과를 투명 상태로 복구하고 `InputBlocker`를 해제합니다.
- 로드 실패 시 `ErrorFallback`을 표시하고 예외를 호출자에게 다시 전달합니다. 호출자는 재시도, 이전 화면 유지 또는 타이틀 복귀 정책을 결정합니다.
- 준비 신호가 `Readiness Timeout` 안에 완료되지 않으면 영구적인 검은 화면을 방지하기 위해 실패 처리합니다.
- Unity의 `LoadSceneAsync`는 시작 후 취소할 수 없습니다. `UnitySceneLoader`는 요청이 취소되어도 실제 로드가 끝날 때까지 관찰한 뒤 취소를 보고하므로, 나중에 Scene이 예고 없이 활성화되는 상태를 만들지 않습니다.

## 시간과 접근성

Fade는 첫 애니메이션 프레임을 정확한 진행률 0으로 렌더링한 뒤 시간을 누적합니다. 프레임별 증가량은 `Maximum Frame Delta`로 제한하며 기본적으로 Unscaled Time을 사용합니다. `SceneTransitionService.ReduceMotion`이 활성화되면 Profile의 `ShortFade` 또는 `Instant` 정책을 적용합니다.

## Panel Fade와의 관계

`PanelStartupController`는 준비 전 숨김, 데이터 로드, 레이아웃 갱신, 입력 제어와 실패 정책을 계속 담당합니다. `FadePanelRevealTransition.animateReveal`의 기본값만 `false`로 변경했기 때문에 준비가 끝나면 즉시 표시됩니다. 필요한 대화 연출에서는 Inspector에서 해당 옵션을 다시 활성화할 수 있습니다.

`CharacterViewBase.animateVisibility`도 기본값이 `false`입니다. 캐릭터 교체 연출이 필요한 화면에서는 개별적으로 활성화합니다. `PanelBackgroundRenderer`의 Cross Fade와 StoryTextPanel의 위치 기반 투명도는 Scene 전환과 목적이 다르므로 기존 동작을 유지합니다.

## 향후 작업

- Addressables 기반 Scene Loader 구현
- EffectLayer를 사용하는 Wipe·Material·Sprite Sheet 효과
- LoadingIndicator와 ErrorFallback의 제품 UI
- 정식 Bootstrap Scene과 실제 두 Scene 사이의 Play Mode 전환 테스트
- 플랫폼 설정과 연동된 Reduce Motion Provider

이 항목들은 현재 구현된 기본 Image Fade 및 복구 경로와 구분되는 후속 확장 범위입니다.
