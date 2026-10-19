# 화면 프레임과 레터박스 해상도 정책 적용

## 1. 목적과 확정 사항

`Candidate_MainScene`에 1920×1080(16:9) 화면 프레임과 꾸밀 수 있는 레터박스를 적용한다. 같은 구성을 앞으로 추가할 Scene에서도 그대로 쓸 수 있도록 공용 구성 요소, 설정 자산, Canvas Prefab으로 만든다. Player 설정은 가로 고정과 크기를 조절할 수 있는 16:9 창으로 바꾼다.

이 문서는 후속 구현 지침이며, 적용이나 실행 검증을 완료했다는 기록이 아니다. 해상도 정책 전체와 UI 작업 규칙은 [해상도 대응과 화면 프레임 정책](../development/resolution-and-screen-frame.md)에 있다.

### 1.1 사용자가 확정한 사항 (2026-10-19)

| 항목 | 확정 내용 |
| --- | --- |
| 기준 해상도 | Scale With Screen Size 방식, 기준 1920×1080 |
| 비율 유지 | 화면의 가로나 세로가 기준 비율보다 길거나 짧으면, 위아래 또는 좌우에 레터박스를 넣어 16:9 비율을 유지한다. |
| 레터박스 꾸미기 | 개발자가 레터박스를 이미지, 텍스처, 애니메이션 등으로 바꿀 수 있어야 한다. |
| 적용 Scene | 이번 작업은 `Candidate_MainScene`에만 적용한다. `TMP_MainScene`은 바꾸지 않는다. 앞으로 추가할 Scene(로딩, 시작 등)도 같은 정책을 쓸 수 있어야 한다. |
| 화면 방향 | 가로 고정 |
| 데스크톱 창 | 창 크기 조절을 허용하고, 창 모드 기본 크기를 16:9로 한다. 전체 화면 기본값은 유지한다. |
| 모달과 전환 화면 | 모달 창 본문은 16:9 프레임 안에, 모달 뒤의 어두운 배경과 장면 전환 화면은 화면 전체에 그린다. |

### 1.2 확정 사항에서 도출한 구현 결정

| 항목 | 결정과 이유 |
| --- | --- |
| Match 값 | 사용하지 않는다. 레터박스로 16:9를 항상 유지하므로, 프레임은 화면(Safe Area) 안에 완전히 들어가야 한다. 이 조건은 Unity `CanvasScaler`의 Expand 방식과 같은 계산이며, Match 0.5를 쓰면 16:9가 아닌 화면에서 프레임 일부가 화면 밖으로 나간다. |
| Safe Area | 프레임은 `Screen.safeArea` 안에 들어가는 가장 큰 16:9 영역에 놓인다. 프로젝트 규칙(AGENTS.md의 Safe Area 지원)에 따른 결정이다. Unity 기본 `CanvasScaler`는 Safe Area를 계산에 넣지 않으므로 `CanvasScaler`를 상속한 구성 요소로 배율을 정한다. |
| 프레임 내부 단위 | 배율 = min(Safe Area 너비 / 1920, Safe Area 높이 / 1080). 따라서 프레임 내부는 항상 1920×1080 Canvas 단위이다. `Candidate_MainScene`의 기존 수치는 1920×1080, 배율 1에서 측정되었으므로 바꾸지 않는다. |
| 레터박스 그리기 순서 | 레터박스 레이어를 프레임보다 뒤에 두어 위에 그린다. 프레임 밖으로 넘친 그림은 레터박스가 덮으므로 프레임에 RectMask2D를 두지 않는다. |
| 레터박스 입력 | 레터박스 표시는 입력을 받지 않는다. |
| 기존 Breakpoint | `MainArea`의 너비 720 Breakpoint와 세로 배치 정책은 바꾸지 않는다. 프레임 너비가 항상 1920이므로 이 Scene에서는 가로 배치만 쓰인다. |

## 2. 작성 시점에 확인한 현재 상태

### 2.1 `Candidate_MainScene`

```text
Candidate_MainScene
├── EventSystem
├── Main Camera
├── Canvas                     Canvas(Screen Space - Overlay), CanvasScaler(Constant Pixel Size, Scale Factor 1), GraphicRaycaster
│   ├── MainArea               FlexibleLayoutPanel.prefab 인스턴스(1:3:1, 너비 720 미만이면 세로)
│   │   └── ContentLayer/LeftArea · MiddleArea · RightArea
│   ├── OverlayLayer           SlotReelDock, EncounterDebugPanel, EncounterResultModal 인스턴스
│   └── EncounterRollController
└── Directional Light
```

- `SlotReelDock`(`Assets/TxTRPG/UI/Runtime/SlotMachine/SlotReelDock.cs`)은 `respectSafeArea`가 켜져 있으면 화면 전체 기준의 Safe Area 여백(아래·왼쪽·오른쪽)을 바깥 여백에 더한다(`SafeInsets`). 부모가 이미 Safe Area 안에 있어도 여백을 다시 더한다. `SlotReelDock.prefab`은 `Candidate_MainScene`에서만 쓰인다.
- `EncounterResultModal.prefab`(`Assets/TxTRPG/UI/Prefabs/Encounters/`)은 `ModalWindowHost` 루트가 Stretch 배경(바깥 클릭 대상)이고, 그 아래 `Window`가 가운데 정렬된 구조이다. `ModalWindowHost`는 `windowRect` 바깥 클릭을 `RectTransformUtility.RectangleContainsScreenPoint`로 판정하고, 열릴 때 `SetAsLastSibling`으로 맨 뒤 형제가 된다.
- `EncounterDebugPanel`은 `OverlayLayer`의 오른쪽 위에 고정되어 있다.

### 2.2 그 밖의 관련 설정

| 대상 | 현재 상태 |
| --- | --- |
| `PersistentAppRoot.prefab`(`Assets/TxTRPG/SceneTransition/Resources/TxTRPG/`), `AppRoot.prefab` | 장면 전환 오버레이 Canvas. Scale With Screen Size 1920×1080, Match 0.5, 화면 전체를 덮는다. 1.1절의 "전환 화면은 화면 전체" 결정과 일치하므로 바꾸지 않는다. |
| `ProjectSettings/ProjectSettings.asset` | `defaultScreenOrientation: 4`(자동 회전), 세로 두 방향과 가로 두 방향 모두 허용, `defaultScreenWidth: 1024`, `defaultScreenHeight: 768`, `resizableWindow: 0`, `fullscreenMode: 1` |
| `TMP_MainScene` | Constant Pixel Size와 `ResponsiveMainScreen`을 유지한다. 가로 고정 후에는 휴대폰에서 세로 배치가 나타나지 않지만, Scene 자체는 바꾸지 않는다. |

## 3. 선행 조건

- 작업 시작 전에 `git status --short`를 기록한다. 작성 시점에는 `AGENTS.md`, `AddressableAssetSettings.asset`, `LiberationSans SDF - Fallback.asset`, `TemporaryCandidateLayoutProbe.cs`, `DefaultSceneTransitionProfile.asset`, `NotoSansKR-Fallback.asset`의 수정이 커밋되지 않은 상태로 있었다. 이 변경들을 되돌리거나 함께 정리하지 않는다.
- Unity Editor의 연결, 컴파일 상태, 열린 Scene의 미저장 변경을 확인한다. 미저장 변경이 있는 Scene은 저장하거나 덮어쓰지 말고 사용자에게 알린다.
- 변경이 Scene 계층 재구성, 공유 Prefab(`SlotReelDock.prefab`, `EncounterResultModal.prefab`), `ProjectSettings`에 걸치므로, 편집 전에 `unity-impact-scout`를 `opus` 모델로 실행해 다음의 영향(호출자, 직렬화 참조, 이름 기반 조회, 테스트, 문서)을 조사한다: `Candidate_MainScene`의 `Canvas`·`MainArea`·`OverlayLayer`, `SlotReelDock.SafeInsets`·`respectSafeArea`, `EncounterResultModal.prefab`, `ModalWindowHost`, `Canvas`를 이름이나 경로로 찾는 Editor 도구(`TemporaryCandidateLayoutProbe` 등 `Assets/TxTRPG/Application/Editor/`의 임시 도구), `ProjectSettings`의 화면 방향과 창 설정을 가정하는 테스트. 결과는 소스로 다시 확인한다.
- 다음 문서를 먼저 읽는다: [해상도 대응과 화면 프레임 정책](../development/resolution-and-screen-frame.md), [FlexibleLayoutPanel](../architecture/flexible-layout-panel.md), [Scene Transition](../architecture/scene-transition.md), [요소 토글과 이벤트 판정](../architecture/encounter-roll.md), [시각 효과 준비](../development/visual-effect-readiness.md).

## 4. 작업 범위

### 4.1 포함

1. UI 어셈블리(`TxTRPG.UI`)에 화면 프레임 계산, Canvas 배율, 프레임 배치, 레터박스 표시, 프레임 정렬 영역 구성 요소를 추가한다.
2. 기본 설정 자산 `DefaultScreenFrameProfile.asset`, 기본 레터박스 표시 Prefab, 새 Scene용 `ScreenFrameCanvas.prefab`을 만든다.
3. `Candidate_MainScene`의 `Canvas`를 화면 프레임 계층으로 바꾼다.
4. `SlotReelDock`의 Safe Area 여백 계산을 부모 영역 기준으로 고친다.
5. `EncounterResultModal.prefab`의 창 본문을 화면 프레임 영역에 맞춘다.
6. Player 설정의 화면 방향과 창 설정을 바꾼다.
7. Edit Mode 테스트와 문서를 추가·갱신한다.

### 4.2 제외

- `TMP_MainScene`, `ResponsiveMainScreen`, 공용 `ModalWindowHost.prefab`, `GameMenuScreen.prefab`은 바꾸지 않는다.
- `PersistentAppRoot.prefab`과 `AppRoot.prefab`의 전환 오버레이는 바꾸지 않는다. 이 Prefab에 앞으로 로딩 문구 같은 본문이 추가될 때 프레임에 맞추는 일은 후속 작업이다.
- 레터박스 이미지·애니메이션 콘텐츠는 만들지 않는다. 기본 표시(검은 막대)와 교체 경로만 만든다.
- 휴대폰용 글자 크기와 터치 영역 최솟값 결정, 이에 따른 기존 UI 수치 조정은 하지 않는다. 측정값만 보고한다.
- 3D 카메라 영역(`Camera.rect`)을 프레임에 맞추는 일은 하지 않는다. `Candidate_MainScene`에는 아직 월드 화면이 없다.
- 시작·로딩 Scene은 만들지 않는다.
- 커밋과 푸시는 하지 않는다.

## 5. 구현 요구 사항

### 5.1 구성 요소 (`TxTRPG.UI`, `Assets/TxTRPG/UI/Runtime/ScreenFrame/`, 네임스페이스 `TxTRPG.UI.ScreenFrame`)

| 타입 | 책임 |
| --- | --- |
| `ScreenFrameLayout` | 불변 값. `ScaleFactor`, `CanvasSize`(Canvas 단위 화면 크기), `FrameRect`, `SafeRect`, `TopBar`·`BottomBar`·`LeftBar`·`RightBar`(모두 Canvas 단위, 원점은 Canvas 왼쪽 아래), `IsValid`. 크기가 0인 막대도 그대로 담는다. |
| `ScreenFrameMath` | 정적 순수 함수 `Calculate(Vector2 screenPixels, Rect safeAreaPixels, Vector2 referenceResolution)`. 화면 크기가 0 이하이거나 NaN이면 `IsValid = false`. Safe Area가 비었거나 화면 밖으로 벗어나면 화면과 겹치는 부분만 쓰고, 겹치는 부분이 없으면 화면 전체를 쓴다. 배율 = min(Safe 너비 / 기준 너비, Safe 높이 / 기준 높이). 프레임은 Safe Area 가운데에 놓는다. 네 막대는 화면 전체에서 프레임을 뺀 영역이다(위·아래 막대는 화면 너비 전체, 왼쪽·오른쪽 막대는 프레임 높이). |
| `ScreenFrameProfile` (ScriptableObject) | `referenceResolution`(기본 1920×1080, 각 축 최소 1), `respectSafeArea`(기본 켜짐), `letterboxPresentation`(레터박스 표시 Prefab, 비워 둘 수 있음), `fallbackLetterboxColor`(기본 검정). `CreateAssetMenu`는 `TxT RPG/UI/Screen Frame Profile`. |
| `ScreenFrameCanvasScaler : CanvasScaler` | `profile`을 직렬화 참조로 받는다. `HandleScaleWithScreenSize`를 재정의해 `ScreenFrameMath`의 배율로 `SetScaleFactor`를 호출한다. `uiScaleMode`는 Scale With Screen Size로 고정하고, `referenceResolution`은 설정 자산 값을 따른다(Inspector 표시용으로 동기화). 설정 자산이 없으면 오류를 한 번 남기고 1920×1080으로 계산한다. |
| `ScreenFrameController` | Canvas 루트에 둔다. `scaler`, `frameRoot`(RectTransform), `letterboxView`를 직렬화 참조로 받는다. `LateUpdate`에서 `Screen.width`·`Screen.height`·`Screen.safeArea`를 이전 값과 비교하고, 바뀐 때만 다시 계산한다. 비교는 값 비교로 끝나야 하며 할당을 만들지 않는다. 다시 계산하면 `frameRoot`의 앵커·피벗을 왼쪽 아래(0, 0)로 두고 `anchoredPosition`과 `sizeDelta`를 `FrameRect`로 맞춘 뒤, `LetterboxView`에 배치를 전달하고 `event Action<ScreenFrameLayout> LayoutChanged`를 발생시킨다. `Current`로 마지막 배치를 제공한다. `OnEnable`에서 한 번 즉시 계산한다. |
| `ILetterboxPresentation` | 레터박스 표시 Prefab의 루트 구성 요소가 구현한다. `void ApplyLayout(ScreenFrameLayout layout)`. 같은 배치가 여러 번 전달되어도 결과가 같아야 한다. |
| `LetterboxView` | `LetterboxLayer`에 둔다. `Awake`에서 설정 자산의 표시 Prefab을 자식으로 하나 만들고, 그 인스턴스 루트의 `ILetterboxPresentation` 구현을 가져온다(자기 인스턴스 안에서만 찾는다). 표시 Prefab이 없거나 구현이 없으면 `fallbackLetterboxColor`의 기본 막대 네 개를 만든다. `SetPresentation(GameObject prefab)`으로 실행 중에 바꿀 수 있으며, 이전 인스턴스를 파괴하고 마지막 배치를 새 표시에 바로 전달한다. 레터박스 아래의 모든 Graphic은 `raycastTarget`을 끈다(기본 표시에서 보장하고, 사용자 표시 Prefab은 문서로 안내한다). |
| `LetterboxBarsPresentation` | 기본 표시 구현. `Top`, `Bottom`, `Left`, `Right` 네 개의 RectTransform(각각 `VisualRoot` 아래 Image)을 막대 영역에 맞춘다. 크기가 0인 막대는 Image를 끈다. 색은 직렬화 값(기본 검정)이다. 막대마다 Sprite를 지정할 수 있다. |
| `ScreenFrameAlignedRect` | 화면 전체 레이어 안에서 자기 RectTransform을 화면 프레임 영역과 같게 맞춘다. `controller`를 직렬화 참조로 받아 `LayoutChanged`를 구독한다. 그래픽을 갖지 않으며 입력을 막지 않는다. |

공통 요구:

1. Scene 검색(`FindObjectOfType` 등)과 이름 기반 조회를 하지 않는다. 연결은 직렬화 참조로 한다. `ScreenFrameAlignedRect`처럼 Prefab 인스턴스 안에서 Scene의 `ScreenFrameController`가 필요하면, Scene 적용 단계에서 참조를 저장한다.
2. 플랫폼 전처리 지시문을 쓰지 않는다. 모든 플랫폼에서 `Screen.safeArea`로 같은 경로를 쓴다.
3. 매 프레임 할당, 레이아웃 재구성, `GetComponent` 반복 호출을 만들지 않는다.
4. Editor에서 Play Mode가 아닐 때도 Game View 크기에 맞춰 프레임을 볼 수 있도록 `ScreenFrameController`와 `ScreenFrameCanvasScaler`는 `[ExecuteAlways]`로 동작해도 된다. 이 경우 Edit Mode에서 Scene을 더럽히지 않도록, 계산 결과로 직렬화 값을 바꾸는 일을 Play Mode와 구분한다(Edit Mode에서는 `RectTransform` 값 변경이 Scene 변경으로 기록되므로, `DrivenRectTransformTracker`로 구동 값임을 표시한다).

### 5.2 자산

| 자산 | 경로 | 내용 |
| --- | --- | --- |
| `DefaultScreenFrameProfile.asset` | `Assets/TxTRPG/UI/Styles/ScreenFrame/` | 1920×1080, Safe Area 반영, 표시 Prefab = `DefaultLetterboxPresentation.prefab`, 대체 색 검정 |
| `DefaultLetterboxPresentation.prefab` | `Assets/TxTRPG/UI/Prefabs/ScreenFrame/` | `LetterboxBarsPresentation` 루트(Stretch) 아래 `Top`·`Bottom`·`Left`·`Right`, 각 막대 아래 `VisualRoot/Image`(검정, `raycastTarget` 꺼짐) |
| `ScreenFrameCanvas.prefab` | `Assets/TxTRPG/UI/Prefabs/ScreenFrame/` | 새 Scene에서 시작할 Canvas. 계층은 5.3절과 같고, `ScreenFrame` 아래에는 빈 `OverlayLayer`만 둔다. `DefaultScreenFrameProfile.asset`을 연결한다. |

세 Prefab과 설정 자산은 개발자 소유 자산이며 Prefab Rebuild 배치에 등록하지 않는다([prefab-rebuild-registry.md](../architecture/prefab-rebuild-registry.md)의 제외 설명에 이유를 추가한다).

### 5.3 `Candidate_MainScene` 계층

```text
Canvas                          Canvas(Screen Space - Overlay), ScreenFrameCanvasScaler, GraphicRaycaster, ScreenFrameController
├── ScreenFrame                 RectTransform(왼쪽 아래 앵커·피벗, 1920×1080, ScreenFrameController가 구동)
│   ├── MainArea                기존 인스턴스를 옮긴다(Stretch 유지)
│   └── OverlayLayer            기존 오브젝트를 옮긴다(Stretch 유지). SlotReelDock, EncounterDebugPanel
├── LetterboxLayer              Stretch, LetterboxView
├── ScreenOverlayLayer          Stretch
│   └── EncounterResultModal    OverlayLayer에서 옮긴다
└── EncounterRollController     위치 유지
```

1. 기존 `Canvas` GameObject를 그대로 쓴다. `Canvas`를 새로 만들거나 Prefab으로 바꾸지 않는다. 기존 `CanvasScaler`는 `ScreenFrameCanvasScaler`로 교체한다(같은 GameObject에 Canvas Scaler 계열은 하나만 둔다).
2. `MainArea`와 `OverlayLayer`는 같은 GameObject를 옮겨 직렬화 참조(파일 ID)를 유지한다. 옮긴 뒤 각 RectTransform의 앵커·오프셋이 Stretch(0, 0, 0, 0)인지 확인한다.
3. `EncounterResultModal`을 `ScreenOverlayLayer`로 옮긴 뒤, `EncounterRollController`의 결과 모달 참조가 그대로인지 확인한다.
4. 계층 순서는 `ScreenFrame` → `LetterboxLayer` → `ScreenOverlayLayer`이다. 모달이 열릴 때 `SetAsLastSibling`은 `ScreenOverlayLayer` 안에서만 순서를 바꾼다.
5. 작업 시점의 계층이 2.1절과 다르면(이름이 같은 오브젝트가 이미 있거나 `Canvas` 아래 다른 자식이 있으면) 진행을 멈추고 사용자에게 보고한다.

### 5.4 `SlotReelDock`의 Safe Area 여백

1. `SafeInsets`를 부모 영역 기준으로 바꾼다. 부모 RectTransform의 화면 영역이 Safe Area 밖과 겹치는 만큼만 여백으로 계산한다. 화면 프레임 안에서는 부모가 이미 Safe Area 안에 있으므로 여백이 0이 된다.
2. 계산은 순수 정적 함수로 분리해 테스트한다(예: `SlotReelDock.CalculateSafeInsets(Rect parentScreenRect, Rect safeArea, Vector2 pixelsToCanvas)`).
3. `respectSafeArea` 필드와 기본값(켜짐), `SetRespectSafeAreaForEditor`는 유지한다.

### 5.5 `EncounterResultModal.prefab`

```text
EncounterResultModal              GameWindowService
└── ModalWindowHost               ModalWindowHost, CanvasGroup, 배경 Image(바깥 클릭 대상), Stretch — 화면 전체
    └── FrameArea                 ScreenFrameAlignedRect(그래픽 없음)
        └── Window                기존 구성 유지(가운데 정렬, windowRect)
```

1. `Window`를 새 `FrameArea` 아래로 옮긴다. `ModalWindowHost.windowRect` 참조는 `Window`를 유지한다.
2. `FrameArea`의 `controller` 참조는 Prefab에서 비워 두고, `Candidate_MainScene` 적용 단계에서 Scene의 `ScreenFrameController`를 인스턴스 Override로 저장한다. 참조가 없으면 `FrameArea`는 Stretch(화면 전체)로 동작하고 경고를 한 번 남긴다.
3. 배경 Image는 화면 전체를 덮는다. 레터박스 영역을 눌러도 바깥 클릭으로 닫힌다.

### 5.6 Player 설정

| 설정 | 값 |
| --- | --- |
| Default Orientation | Auto Rotation |
| Allowed Orientations | Landscape Left, Landscape Right만 허용(Portrait, Portrait Upside Down 끔) |
| Default Screen Width / Height | 1280 / 720 |
| Resizable Window | 켬 |
| Fullscreen Mode | 기존 값(Fullscreen Window) 유지 |

바꾼 항목 이외의 `ProjectSettings` 값은 건드리지 않는다. 저장 후 `ProjectSettings.asset` 차이에 위 항목만 있는지 확인한다.

### 5.7 적용 방식

구성 요소와 자산 생성, Scene 계층 변경은 한 번만 적용하면 되므로 재사용 Builder를 만들지 않는다. Unity Editor 자동화로 직접 적용하는 방법을 우선한다. 자동화를 사용할 수 없으면 `TxTRPGEditorMenuPaths.Application + "Temporary/Apply Candidate Screen Frame"` 임시 메뉴를 만들어 사용자가 실행하게 하고, 다음을 지킨다.

- 클래스 주석에 목적, 임시 상태, 재실행 안전성을 기록한다.
- 실행 전 Play Mode·컴파일 중·대상 Scene 미저장 변경 여부와 2.1절의 계층을 검사하고, 조건이 맞지 않으면 아무것도 바꾸지 않는다.
- 이미 있는 Prefab과 설정 자산은 덮어쓰지 않는다. `ScreenFrame`이 이미 있으면 Scene을 바꾸지 않고 그 사실을 로그로 남긴다. 따라서 재실행해도 안전해야 한다.
- 바꾼 자산, `Candidate_MainScene`, `ProjectSettings`만 저장한다.
- 사용자가 적용과 검증을 끝낸 뒤 제거할 수 있다고 문서에 기록한다.

### 5.8 Edit Mode 테스트

| 테스트 클래스 | 어셈블리 | 내용 |
| --- | --- | --- |
| `ScreenFrameMathTests`(새로 추가) | `TxTRPG.UI.Tests.Editor` | 1920×1080 → 배율 1, 막대 0. 3840×2160 → 배율 2. 2560×1080 → 좌우 막대 각 320. 1024×768 → 위아래 막대. 1080×1920 → 위아래 막대. Safe Area 노치(예: 왼쪽 100px) → 프레임이 Safe Area 안 가운데. 빈·화면 밖 Safe Area 처리, 화면 크기 0·NaN → `IsValid = false`. 막대와 프레임 넓이의 합이 화면 넓이와 같음. |
| `ScreenFrameControllerTests`(새로 추가) | `TxTRPG.UI.Tests.Editor` | 계산 결과대로 `frameRoot` 배치, 같은 입력에서 `LayoutChanged`가 다시 발생하지 않음, 설정 자산 누락 시 예외 없이 기본값. 화면 값은 테스트에서 주입할 수 있도록 입력 공급 지점을 분리한다. |
| `LetterboxViewTests`(새로 추가) | `TxTRPG.UI.Tests.Editor` | 표시 Prefab 생성과 배치 전달, `SetPresentation` 교체 시 이전 인스턴스 파괴와 마지막 배치 전달, 표시 Prefab이 없거나 구현이 없을 때 기본 막대, 모든 Graphic의 `raycastTarget` 꺼짐, 크기 0 막대 비활성 |
| `SlotReelDockTests`(기존) | `TxTRPG.UI.Tests.Editor` | 5.4절의 여백 함수: 부모가 Safe Area 안이면 0, 화면 전체 부모이면 기존과 같은 여백. 기존 테스트가 모두 통과하는지 확인한다. |

새 테스트는 Scene·Prefab을 저장하지 않으므로 `AssetWriting`이 아니다. 위 클래스와, 영향 조사에서 찾은 `ModalWindowHost`·`EncounterRollController` 관련 기존 테스트 클래스만 필터로 실행한다. 전체 Edit Mode 테스트는 실행하지 않는다. 테스트 어셈블리 이름은 실제 `.asmdef`로 확인한다.

## 6. 호환성과 실패 동작

- 세이브 데이터, 입력 바인딩, 플레이어 설정 형식은 바뀌지 않는다.
- 설정 자산이 없으면 1920×1080, Safe Area 반영, 검은 기본 막대로 동작하고 오류를 한 번 남긴다. 게임 진행은 막지 않는다.
- 사용자 레터박스 표시 Prefab이 예외를 던지면 그 표시를 끄고 기본 막대로 바꾼 뒤 오류를 한 번 남긴다.
- Safe Area 값이 비정상이면 화면 전체로 계산한다.
- 화면 크기가 0이 되는 순간(창 최소화 등)에는 마지막으로 유효했던 배치를 유지한다.
- `TMP_MainScene`은 이 변경과 무관하게 기존 동작을 유지해야 한다. 다만 가로 고정 후 휴대폰에서 세로 배치가 나타나지 않는다.

## 7. 검증 기준

### 7.1 Edit Mode 테스트

5.8절의 클래스만 필터로 실행하고 결과를 보고한다. 테스트 전후의 Git 상태를 기록하고, 테스트가 작업 범위 밖의 파일을 바꿨다면 작업 전에 변경이 없던 파일만 되돌린다.

### 7.2 Editor 확인

1. 아무 Scene에서 Play를 시작하면 AppScene을 거쳐 `Candidate_MainScene`이 로드된다.
2. Game View 1920×1080에서 레터박스가 없고, 배치가 적용 전과 같다(적용 전후 같은 크기로 캡처해 비교한다).
3. [해상도 대응과 화면 프레임 정책](../development/resolution-and-screen-frame.md) 3.6절의 화면 크기마다 레터박스 방향과 크기, 프레임 안 UI 배치, `SlotReelDock`과 결과 모달이 프레임 안에 있는지 확인하고 프레임 크기·배율을 기록한다. 임시로 추가한 Game View 크기는 확인 후 제거한다.
4. 디버그 패널에서 이벤트를 발생시켜 결과 모달을 연다. 어두운 배경이 레터박스까지 덮고, 창 본문은 프레임 가운데에 있다. 레터박스 영역을 클릭해도 모달이 닫힌다.
5. Play Mode 중 Game View 크기를 바꾸면 프레임과 레터박스가 다시 계산된다. 같은 크기를 유지하는 동안에는 다시 계산하지 않는다(로그나 Profiler로 확인).
6. 기본 레터박스 표시 대신 단색이 아닌 Sprite를 지정한 임시 표시 Prefab으로 `SetPresentation`을 호출해 교체가 동작하는지 확인하고, 임시 Prefab은 삭제한다.
7. Device Simulator에서 노치가 있는 기기 하나 이상을 가로로 놓고 프레임이 Safe Area 안에 있는지 확인한다. 확인하지 못했다면 그렇게 보고한다.
8. `TMP_MainScene`으로 Play했을 때 기존 화면이 그대로인지 1920×1080에서 확인한다.
9. Windows Standalone 빌드 대상 컴파일을 확인한다. 가능하면 Android 대상 컴파일도 확인한다. 실제 기기와 Player 빌드에서 확인하지 못한 항목은 미검증으로 보고한다.

### 7.3 직렬화 변경 검토

적용 후 `serialization-diff-reviewer`로 `.unity`, `.prefab`, `.asset`, `.meta`, `ProjectSettings` 변경을 검토한다. 의도한 변경은 `Candidate_MainScene.unity`, `SlotReelDock.prefab`(필요한 경우만), `EncounterResultModal.prefab`, 새 Prefab 2개와 설정 자산 1개, `ProjectSettings.asset`의 5.6절 항목, 새 스크립트·폴더의 `.meta`뿐이다. 작업 전부터 있던 사용자 변경과 TMP 동적 글꼴 Atlas 증가 같은 부수 변경은 구분하여 보고한다.

## 8. 문서 갱신

| 문서 | 갱신 내용 |
| --- | --- |
| `DOCS/development/resolution-and-screen-frame.md` | 2절을 실제 구현 상태로 바꾸고, 1.1절 계층의 구성 요소 이름을 실제 이름으로 확인한다. 레터박스 표시 Prefab을 만드는 방법(`ILetterboxPresentation` 구현, `raycastTarget` 끄기, 효과 감소 설정)을 추가한다. |
| `DOCS/architecture/screen-frame.md`(새 문서) | 구성 요소 책임표, 계산식, 갱신 순서(화면 변화 → 배율 → 프레임 → 레터박스·정렬 영역), 계층, 실패 동작, 확장 지점 |
| `DOCS/README.md` | 새 아키텍처 문서 링크 |
| `DOCS/architecture/project-structure.md` | `UI/Runtime/ScreenFrame/`, `UI/Prefabs/ScreenFrame/`, `UI/Styles/ScreenFrame/`와 `Candidate_MainScene`의 새 Canvas 계층 |
| `DOCS/architecture/flexible-layout-panel.md` | `Candidate_MainScene` 계층 예시를 `ScreenFrame` 아래로 고치고, 프레임 너비 1920 때문에 세로 배치가 쓰이지 않는다는 사실을 기록 |
| `DOCS/architecture/slot-machine-and-runes.md` | `SlotReelDock`의 Safe Area 여백이 부모 영역 기준이라는 사실 |
| `DOCS/architecture/encounter-roll.md` | 결과 모달의 `ScreenOverlayLayer` 위치와 `FrameArea` |
| `DOCS/architecture/prefab-rebuild-registry.md` | 새 Prefab 2개의 제외 이유 |
| `DOCS/development/workflows.md` | 새 테스트 위치와 필터, 임시 적용 메뉴를 만든 경우 그 절차와 제거 시점 |
| `DOCS/instructions/README.md` | 구현과 적용을 마치면 이 문서 설명에 완료 날짜와 적용 결과를 덧붙임 |

## 9. 최종 보고

다음을 구분하여 보고한다.

1. 구현 완료: 코드·Prefab·자산·테스트·문서 변경 목록
2. 적용 완료: Editor에서 적용하고 저장한 자산과 확인한 값(Canvas 계층, 배율 구성 요소 설정, 모달 `FrameArea` 참조, Player 설정)
3. 적용 필요: 사용자가 실행해야 하는 임시 메뉴가 남았다면 `사용자가 수행할 적용 절차` 절과 복사 가능한 순서
4. 검증: 실행한 테스트 필터와 결과, 확인한 화면 크기별 프레임 크기·배율·레터박스, 확인하지 못한 기기·입력 방식·빌드 대상
5. 직렬화 검토 결과와 되돌린 파일, 작업 전부터 있던 사용자 변경
6. 측정값: [해상도 대응과 화면 프레임 정책](../development/resolution-and-screen-frame.md) 3.5절 기준으로 계산한 휴대폰에서의 주요 글자 크기와 터치 영역 크기(특히 토글 버튼 최소 높이 48)
7. 후속 작업: 휴대폰 글자·터치 최솟값 결정과 UI 조정, 레터박스 콘텐츠 제작, `PersistentAppRoot` 본문 요소의 프레임 정렬, 3D 카메라 영역 정책, 시작·로딩 Scene
