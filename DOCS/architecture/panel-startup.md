# Panel Startup 설계

## 목적

`PanelStartupController`는 UI Prefab이 활성화된 뒤 일부 데이터나 이미지가 준비되지 않은 상태로 노출되는 것을 방지합니다. 패널별 데이터 형식은 유지하고, 숨김·비동기 준비·레이아웃 확정·등장·입력 활성화 순서만 공통화합니다.

```text
Awake에서 숨김 및 입력 차단
→ PanelInitialDataLoader.LoadAndApplyAsync
→ Addressables 자산 준비 완료
→ Canvas와 Layout 한 번 갱신
→ PanelRevealTransition 실행
→ 입력 활성화 및 Ready
```

## 구성 요소

| 형식 | 책임 |
| --- | --- |
| `PanelStartupController` | 상태, 중복 실행 방지, 취소, 실패 정책, 레이아웃 갱신과 입력 활성화를 관리합니다. |
| `PanelInitialDataLoader` | 패널별 타입 안전한 초기 데이터를 적용하고 자산 준비 완료 시점을 반환합니다. |
| `PanelRevealTransition` | 등장 연출 계약을 제공합니다. |
| `FadePanelRevealTransition` | 첫 프레임 진행률 0, 제한된 프레임 증가량과 Unscaled Time을 사용하는 기본 Fade입니다. |
| `CanvasGroup` | 준비 중 알파와 Pointer·Touch·Submit 입력 차단을 담당합니다. |

상태는 `Idle`, `Loading`, `Applying`, `WaitingForLayout`, `Revealing`, `Ready`, `Failed`, `Cancelled`로 구분합니다. 비활성화하면 진행 중 초기화와 Reveal이 취소됩니다. 실패 시 `StayHidden` 정책이 아니면 빈 상태라도 즉시 표시하여 영구적으로 투명한 화면이 남지 않게 합니다.

## Demo Loader

`StoryTextPanelDemoLoader`, `CharacterDisplayPanelDemoLoader`, `ActionGridPanelDemoController`는 모두 `PanelInitialDataLoader`를 구현합니다. 각 Loader는 기존 Demo 데이터 타입을 유지하며, Character Sprite·배경과 Action 아이콘은 패널이 소유한 `WhenAssetsReady`가 완료될 때까지 기다립니다.

Character Demo는 부모 패널 Reveal과 캐릭터 View Fade가 중복되지 않도록 `ShowCharacterImmediately`로 준비된 이미지를 완전히 표시한 뒤 부모 CanvasGroup만 Fade합니다.

## 데이터 없음과 실패

| 정책 | 동작 |
| --- | --- |
| `ShowEmpty` | 빈 패널을 레이아웃한 뒤 표시합니다. |
| `PreserveAuthoredState` | Prefab에 저장된 표현을 유지하고 표시합니다. |
| `StayHidden` | 외부 초기화 전까지 숨김 상태를 유지합니다. |

Addressables Lease는 Loader나 Startup Controller가 해제하지 않습니다. 실제 Sprite·아이콘·배경을 사용하는 패널이 계속 소유하며, 패널이 교체되거나 비활성화될 때 기존 수명 정책에 따라 해제합니다.
