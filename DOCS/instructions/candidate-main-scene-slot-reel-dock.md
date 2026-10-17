# Candidate_MainScene 하단 슬롯 Reel 독과 합계 카드

## 1. 목적과 확정 사항

`TMP_MainScene`에서 `FlexibleLayoutItem`으로 배치된 `SlotMachinePanel`의 Reel 표시를 `Candidate_MainScene`에서도 사용할 수 있게 한다. `Candidate_MainScene`에서는 슬롯 영역이 레이아웃 항목이 아니라 화면 하단에 떠 있는 독립된 영역(이하 "슬롯 Reel 독")으로 존재하며, 표시·숨김과 위치를 코드로 다룰 수 있어야 한다. 슬롯 Reel 독에는 Reel만 있고, 행동 결과의 합계는 Reel 영역 위쪽 가장자리에 걸쳐 놓이는 별도의 카드(이하 "합계 카드")로 표시한다. 합계 카드의 모양은 개발자가 직사각형, 둥근 직사각형, 원, 타원 중에서 고르고, 둥근 직사각형의 둥근 정도도 설정한다.

이 문서는 후속 구현 지침이며, 적용이나 실행 검증을 완료했다는 기록이 아니다.

### 1.1 사용자가 확정한 사항 (2026-10-17)

| 항목 | 확정 내용 |
| --- | --- |
| 이번 작업의 기능 범위 | 표시와 미리보기까지 구현한다. 슬롯 Reel 독과 합계 카드를 `Candidate_MainScene`에 배치하고, 표시 API를 공용 계약으로 정리한다. 실제 전투·탐험 이벤트 컨트롤러와의 연결은 후속 작업으로 남긴다. 회전과 합계 표시는 Editor 미리보기 메뉴로 확인한다. |
| Prefab 구성 | Reel·구분대·Payline을 다루는 코드를 공용 구성 요소로 분리하고, `Candidate_MainScene`용으로 새 Prefab(Reel 영역 + 합계 카드)을 만든다. `SlotMachinePanel.prefab`과 `TMP_MainScene`의 화면은 바꾸지 않는다. 두 Prefab은 같은 `SlotReel.prefab`과 회전 연출 설정을 사용한다. |
| 배치 기준 | `Canvas` 전체의 하단이다. `MainArea`(FlexibleLayout) 바깥의 오버레이 층에 두며, 세 Area의 배치와 독립적이다. |
| 입력 처리 | 뒤 화면의 입력을 차단하지 않는다. 슬롯 Reel 독은 입력 대상(`raycastTarget`)을 갖지 않는다. 표시·숨김과 위치 변경 API를 제공하고, 화면 밖에서 들어오는 전환 연출이나 드래그로 위치를 정하는 기능은 나중에 붙일 수 있는 확장 지점만 둔다. |

### 1.2 이 문서가 정한 기본값

사용자 답변 이후 결과에 큰 영향이 없어 지침서가 정한 값이다. 구현자는 이 값을 그대로 사용하고, 바꿀 필요가 생기면 근거와 함께 보고한다.

| 항목 | 기본값 |
| --- | --- |
| 합계 카드의 내용 | 합계 라벨(`합계`), 합계 값, 합계 Fortune Critical 배수(`×2`, 없으면 숨김). 룬 표는 슬롯 Reel 독에 두지 않는다. |
| 합계 카드의 기본 모양 | 첨부 이미지와 같은 `Rectangle`. 둥근 정도는 짧은 변 대비 비율 0.2를 미리 저장해 두어 `RoundedRectangle`로 바꾸면 바로 쓰인다. |
| 합계 카드의 위치 | Reel 기계의 가로 가운데, 카드 높이의 50%가 Reel 기계 위쪽 가장자리 위로 나온다. |
| 초기 표시 상태 | `Candidate_MainScene`에서 표시(독에 붙은 상태)로 시작한다. |
| 등장 전환 | 이번 작업에서는 즉시 전환만 구현한다. |

## 2. 작성 시점에 확인한 현재 상태

### 2.1 `Candidate_MainScene`

```text
Candidate_MainScene
├── EventSystem
├── Main Camera
├── Canvas                     Canvas, CanvasScaler(Constant Pixel Size), GraphicRaycaster
│   └── MainArea               FlexibleLayoutPanel.prefab 인스턴스
│       └── ContentLayer
│           ├── LeftArea       FlexibleLayoutPanel.prefab 인스턴스 + FlexibleLayoutItem(Weighted 1)
│           ├── MiddleArea     FlexibleLayoutPanel.prefab 인스턴스 + FlexibleLayoutItem(Weighted 3)
│           └── RightArea      FlexibleLayoutPanel.prefab 인스턴스 + FlexibleLayoutItem(Weighted 1)
└── Directional Light
```

- 게임 패널과 컨트롤러(`TemporarySlotCombatController`, `ExplorationEventController`, Story, 적, 행동 버튼)가 없다. `PlayerSessionHost`는 AppScene의 `AppRoot`에 있다.
- Safe Area 처리(`TMP_MainScene`의 `ResponsiveMainScreen`에 해당하는 구성)와 `CanvasScaler` 정책은 아직 정해지지 않았다([candidate-main-scene-initial-content-and-layout.md](candidate-main-scene-initial-content-and-layout.md) 4.2절).

### 2.2 `SlotMachinePanel`

현재 구조와 동작은 [슬롯머신과 룬 기반 전투 행동](../architecture/slot-machine-and-runes.md#slotmachinepanel)에 있다. 이번 작업과 관련된 사실은 다음과 같다.

| 대상 | 내용 |
| --- | --- |
| `Assets/TxTRPG/UI/Runtime/SlotMachine/SlotMachinePanel.cs` | Reel 처리(`EnsureReelCount`, `EnsureDividers`, `ArrangeSiblings`, `UpdatePaylineFrame`, `UpdatePaylineChildren`, `PlaySpinAsync`·`PlayReelAsync`, `StopAtResult`, `SetReels`, 회전 중 구성 변경 보류 `pendingReels`, 회전 세대 `generation`, 회전 설정 `Settings`)와 요약 영역 처리(`SetSummary`, 룬 표), 스킨 적용(`ApplySkinToAll`, `ApplyPaylineSkin`, `ApplyDividerSkin`)이 한 클래스에 있다. |
| `SlotMachinePanel.prefab` | `Content/ReelMachine`(Frame, ReelArea, PaylineFrame)와 `Content/SummaryArea`(TotalBox, RuneTableArea)를 가진다. |
| `SlotReel.prefab`, `SlotReelView`, `SlotReelViewBase` | Reel 하나의 표시와 회전 연출. 패널이나 설정 자산을 참조하지 않으므로 그대로 재사용할 수 있다. |
| `SlotReelAreaLayout` | Reel과 구분대를 가로로 배치하고 `LayoutApplied`를 발생시킨다. |
| `SlotMachinePanel`을 직접 참조하는 코드 | `TemporarySlotCombatController.slotPanel`, `ExplorationEventController.slotPanel`, Editor 검증 메뉴(`TemporarySlotMachinePlayValidation`, `TemporaryExplorationEventPlayValidation`, `TemporarySlotFortuneCriticalPlayValidation`, `TemporarySlotMachineIntegration`), 테스트(`SlotMachinePanelTests`, `SlotMachinePanelSkinTests`) |
| 표시 데이터 | `SlotReelDisplay`, `SlotReelStopDisplay`, `SlotSummaryDisplay`, `SlotSpinDisplay`(`SlotMachineDisplays.cs`). UI 어셈블리는 Gameplay·Application 타입을 모른다. |
| Prefab Rebuild | 두 Prefab은 개발자 소유 자산이며 등록 기반 Prefab Rebuild에서 제외된다([prefab-rebuild-registry.md](../architecture/prefab-rebuild-registry.md)). |

### 2.3 재사용할 수 있는 도형

`Assets/TxTRPG/UI/Runtime/Shapes/`의 `UiShapeGraphic`(`MaskableGraphic`)은 `UiShape`(`Rectangle`, `RoundedRectangle`, `Circle`, `Ellipse`, `Triangle`), 픽셀 단위 `cornerRadius`, `curveSegments`, `UiShapeDrawMode`(`Fill`, `InnerBorder`), `borderThickness`를 가지며 `Configure`로 실행 중에 바꿀 수 있다. 텍스처 없이 그리고 Material을 지정할 수 있으므로 효과를 나중에 붙일 수 있다. `Circle`은 사각형 안의 짧은 변을 지름으로 하는 원을 그린다.

## 3. 선행 조건

- 작업 시작 전에 `git status --short`를 기록한다. 작성 시점에는 `AddressableAssetSettings.asset`, `DefaultSceneTransitionProfile.asset`, `NotoSansKR-Fallback.asset` 수정이 사용자 변경으로 존재했다. 이 변경들을 되돌리거나 함께 정리하지 않는다.
- Unity Editor의 연결, 컴파일 상태, 열린 Scene의 미저장 변경을 확인한다. 미저장 변경이 있는 Scene은 저장하거나 덮어쓰지 말고 사용자에게 알린다.
- 편집 전에 `unity-impact-scout`로 `SlotMachinePanel`의 Reel 처리 코드를 분리할 때의 영향(직렬화 필드, 호출자, 테스트, Editor 메뉴, 문서)을 조사한다. 공용 Prefab의 직렬화에 영향을 주므로 `opus` 모델로 호출한다. 결과는 소스로 다시 확인한다.
- 다음 문서를 먼저 읽는다: [슬롯머신과 룬 기반 전투 행동](../architecture/slot-machine-and-runes.md), [시각 효과 준비](../development/visual-effect-readiness.md), [FlexibleLayoutPanel](../architecture/flexible-layout-panel.md), [경계선 명령 버튼](../architecture/boundary-command-button.md)(도형 Graphic 사용 예).

## 4. 작업 범위

### 4.1 포함

1. `SlotMachinePanel`의 Reel·구분대·Payline·회전 처리를 공용 구성 요소로 분리하고, `SlotMachinePanel`은 이 구성 요소에 위임한다. `SlotMachinePanel`의 공개 API, 직렬화 필드, Prefab, `TMP_MainScene`의 화면과 동작은 바뀌지 않는다.
2. 슬롯 표시 계약(인터페이스)을 UI 어셈블리에 추가하고 `SlotMachinePanel`과 새 슬롯 Reel 독이 모두 구현한다.
3. 슬롯 Reel 독 컴포넌트와 Prefab, 합계 카드 컴포넌트를 만든다.
4. `Candidate_MainScene`의 `Canvas` 아래 오버레이 층에 슬롯 Reel 독을 배치한다.
5. Play Mode에서 슬롯 Reel 독의 회전·합계·Reel 수 변경·표시/숨김을 확인하는 Editor 미리보기 메뉴를 만든다.
6. Edit Mode 테스트와 문서를 추가·갱신한다.

### 4.2 제외

- `TemporarySlotCombatController`, `ExplorationEventController`의 `slotPanel` 필드 형식 변경과 `Candidate_MainScene`의 실제 전투·이벤트 연결. 이 작업은 후속 작업으로 최종 보고에 남긴다.
- 화면 밖에서 들어오는 전환 연출과 드래그로 위치를 정하는 기능의 구현. 확장 지점만 둔다.
- 슬롯 Reel 독 뒤의 입력 차단, 모달 호스트(`GameWindow` 계열) 등록, Cancel 경로.
- `MiddleArea` 등 세 Area가 슬롯 Reel 독에 가려지는 높이만큼 공간을 비워 두는 처리. 현재 세 Area에는 콘텐츠가 없으므로 후속 작업으로 남긴다.
- `Candidate_MainScene`의 `CanvasScaler` 정책 변경과 화면 전체 Safe Area 구성. 슬롯 Reel 독은 자기 위치에만 Safe Area를 반영한다(5.4절).
- 합계 카드의 스킨 연동, 합계 카드의 이미지(Sprite) 모드.
- `SlotMachinePanel.prefab`, `SlotReel.prefab`, `TMP_MainScene`의 수정. 단, 5.1절의 리팩터링으로 기존 Prefab을 다시 저장해야 할 이유가 생기면 진행을 멈추고 보고한다.
- 커밋과 푸시.

## 5. 구현 요구 사항

### 5.1 Reel 처리 공용 구성 요소

1. `Assets/TxTRPG/UI/Runtime/SlotMachine/`에 MonoBehaviour가 아닌 일반 C# 클래스 `SlotReelMachineCore`(이름은 기존 관례에 맞게 조정할 수 있다)를 추가한다. 직렬화 필드를 옮기지 않기 위해 MonoBehaviour로 만들지 않는다. 기존 `SlotMachinePanel` 필드를 새 컴포넌트로 옮기면 `SlotMachinePanel.prefab`과 `TMP_MainScene`의 직렬화가 바뀌기 때문이다.
2. `SlotReelMachineCore`는 소유 컴포넌트가 넘겨주는 참조(Reel 영역, `SlotReelAreaLayout`, Reel Prefab, Reel 목록, 구분대 목록, Payline 루트·테두리·이미지, 글자 크기 범위, 회전 설정 공급자, 현재 스킨 공급자, 구분대 스킨 적용 콜백)로 동작한다. 다음 책임을 가진다.
   - Reel 수 맞추기(Reel·구분대 추가·제거·형제 순서), 새 Reel·구분대에 현재 스킨 적용
   - `SetReels`(회전 중이면 보류했다가 회전이 끝나면 적용)
   - `PlaySpinAsync`(Reel별 계획, 동시 시작, Reel별 Fortune 배수 표시, 세대 번호로 중단 판정)와 `StopAtResult`
   - Payline 위치·확장 갱신과 `SlotReelAreaLayout.LayoutApplied` 구독·해제
   - 비활성화 시 회전 정리
3. `SlotMachinePanel`은 기존 직렬화 필드와 공개 API(`SetReels`, `SetSummary`, `PlaySpinAsync`, `StopAtResult`, `ShowError`, `ClearError`, `GetMaximumPresentationSeconds`, `MaximumPresentationSeconds`, `IsSpinning`, `ReelCount`, `ReelViews`, `ActiveDividerCount`, Payline 속성, 스킨 API, `...ForEditor` 멤버)를 그대로 유지하고, Reel 처리만 `SlotReelMachineCore`에 위임한다. 요약 영역(합계 상자, 룬 표)의 처리, 스킨 대상 기록과 적용 순서는 지금처럼 `SlotMachinePanel`에 둔다.
4. 동작은 바뀌지 않아야 한다. 특히 다음을 유지한다.
   - 회전 중 `SetSummary(result.SpinningSummary)` → 모든 Reel 완료 → `SetSummary(result.ResultSummary)` 순서
   - `StopAtResult`가 세대 번호를 올려 진행 중인 연출을 무효화하는 동작
   - 회전 중 매 프레임 할당을 늘리지 않는 것. 공용 클래스는 모서리 배열 등 작업 버퍼를 생성 시 한 번 만든다.
5. 기존 테스트 `SlotMachinePanelTests`, `SlotMachinePanelSkinTests`는 수정 없이 통과해야 한다. 테스트를 고쳐야만 통과한다면 그 이유가 동작 변경인지 확인하고 보고한다.

### 5.2 슬롯 표시 계약

1. `TxTRPG.UI.SlotMachine` 네임스페이스에 `ISlotMachinePresenter`를 추가한다. 멤버는 컨트롤러가 현재 `SlotMachinePanel`에서 사용하는 것으로 한정한다: `IsSpinning`, `SetReels`, `SetSummary`, `PlaySpinAsync`, `StopAtResult`, `ShowError`, `ClearError`, `GetMaximumPresentationSeconds`. 실제 사용 멤버는 `TemporarySlotCombatController`와 `ExplorationEventController`의 호출부로 확인하고, 그 목록을 보고한다.
2. `SlotMachinePanel`과 슬롯 Reel 독이 이 계약을 구현한다. 슬롯 Reel 독은 `SlotSummaryDisplay` 중 `TotalLabel`, `TotalValue`, `TotalMultiplierText`만 사용하고 룬 항목은 무시한다. 무시한다는 사실을 클래스 주석에 적는다.
3. 이번 작업에서는 컨트롤러의 필드 형식을 바꾸지 않는다. 후속 연결 작업은 Unity가 인터페이스 필드를 직렬화하지 않으므로 `ExplorationRunController.combatControllerBehaviour`와 같은 방식(`MonoBehaviour` 필드 + 인터페이스 검사 + `FormerlySerializedAs`)을 쓰게 된다는 사실을 아키텍처 문서의 확장 지점에 적는다.

### 5.3 슬롯 Reel 독 Prefab과 계층

Prefab 경로는 `Assets/TxTRPG/UI/Prefabs/SlotMachine/SlotReelDock.prefab`, 컴포넌트는 `Assets/TxTRPG/UI/Runtime/SlotMachine/SlotReelDock.cs`(`SlotReelDock`)이다. 목표 계층은 다음과 같다.

```text
SlotReelDock                 (RectTransform 배치 루트, CanvasGroup, SlotReelDock)
├─ PresentationRoot          (RectTransform. 표시·숨김과 이후 전환 연출이 위치를 바꾸는 유일한 대상)
│  ├─ ReelMachine            (레이아웃 전용. 배치 루트를 채움, 위쪽은 합계 카드 돌출 높이만큼 비움)
│  │  ├─ Frame               (시각 루트, SlotMachinePanel의 ReelMachine/Frame과 같은 외형) > LeftEdge, RightEdge
│  │  ├─ ReelArea            (SlotReelAreaLayout)
│  │  │  ├─ SlotReel_{n}     (SlotReel.prefab 중첩 인스턴스, 기본 3개)
│  │  │  └─ ReelDivider_{n}
│  │  └─ PaylineFrame        (배치 전용 루트) > ProceduralBorder, SkinImage
│  ├─ TotalCard              (배치 루트, SlotTotalCardView. ReelMachine보다 뒤 형제 → 위에 그려짐)
│  │  ├─ VisualRoot          (효과 대상)
│  │  │  ├─ Fill             (UiShapeGraphic, Fill)
│  │  │  └─ Border           (UiShapeGraphic, InnerBorder, 기본 비활성)
│  │  └─ ContentRoot         (모양별 안쪽 글자 영역)
│  │     ├─ TotalLabel       (TMP)
│  │     ├─ TotalValue       (TMP)
│  │     └─ TotalMultiplier  (배치 루트, 기본 비활성) > VisualRoot > Text
│  ├─ StatusText             (구성 오류, 평소 비활성)
│  └─ EffectRoot             (독 단위 효과)
```

1. `ReelMachine` 하위의 외형(Frame 색, 그라데이션 Sprite, 구분대, Payline 테두리, Reel Window 비율)은 `SlotMachinePanel.prefab`의 `Content/ReelMachine`과 같게 만든다. 적용 시 원본 하위 구조를 복제하는 방법을 쓸 수 있으며, 그 경우 복제본은 원본 Prefab과 연결되지 않은 독립 객체여야 한다. `SlotReel_{n}`은 `SlotReel.prefab`의 중첩 인스턴스로 둔다.
2. 모든 저작 Graphic의 `raycastTarget`을 끈다. `CanvasGroup.blocksRaycasts`와 `interactable`은 꺼 둔다. 슬롯 Reel 독은 입력을 받지 않으며 뒤 화면의 클릭·탭을 막지 않는다.
3. `SlotReelDock`은 `SlotReelMachineCore`를 사용하고, `SlotMachinePanel`과 같은 회전 설정(`DefaultSlotReelSpinProfile.asset`)과 스킨 카탈로그(`SlotMachinePanelSkinCatalog.asset`)를 참조한다. 스킨은 Reel 관련 대상(Reel 기계 틀, 가장자리 띠, 구분대, Payline, 각 Reel)에만 적용한다. 스킨 대상 기본값 기록은 `SlotMachinePanel`과 같은 방식(Inspector 컴포넌트 메뉴 `기본 외형 다시 기록`)을 제공한다.
4. Reel 수는 `SetReels`·`PlaySpinAsync`·`StopAtResult`가 받은 표시 데이터의 Reel 수를 따른다. Reel 1개부터 동작하며, 가로 스크롤은 두지 않는다. Reel이 많아 각 Reel이 최소 너비(`SlotReel` `LayoutElement` 최소 40)보다 좁아지는 경우의 동작은 `SlotMachinePanel`과 같게 한다.
5. 숫자 크기 범위(`minimumFontSize`, `maximumFontSize`)는 `SlotMachinePanel`과 같은 기본값(16, 64)을 쓴다.

### 5.4 슬롯 Reel 독의 배치와 표시·숨김

`SlotReelDock`의 직렬화 설정은 다음과 같다. 크기 단위는 Canvas 단위이며, 비율은 독이 놓인 부모(오버레이 층) 크기 기준이다.

| 필드 | 기본값 | 의미 |
| --- | --- | --- |
| `heightRatio`, `minimumHeight`, `maximumHeight` | 0.28, 140, 320 | 독 높이 = 부모 높이 × 비율을 최소·최대로 제한. 합계 카드가 위로 돌출하는 높이는 포함하지 않는다. |
| `horizontalMargin` | 16 | 좌우 바깥 여백 |
| `bottomMargin` | 12 | 아래 바깥 여백 |
| `maximumWidth` | 1200 (0이면 제한 없음) | 초광폭 화면에서 Reel이 지나치게 넓어지지 않도록 가운데 정렬하며 너비를 제한한다. |
| `respectSafeArea` | 켜짐 | `Screen.safeArea`의 아래·좌·우 여백을 바깥 여백에 더한다. |
| `initiallyShown` | 켜짐 | Awake 시 표시 상태 |
| `transitionBehaviour` | 없음 | `ISlotReelDockTransition`을 구현한 `MonoBehaviour`. 비어 있으면 즉시 전환한다. |

1. 배치 루트(`SlotReelDock`의 RectTransform)는 부모의 아래쪽에 앵커를 두고, 위 설정으로 크기와 위치를 정한다. 합계 카드의 돌출 높이만큼 배치 루트 위쪽을 더 확보하여, 카드가 화면 위쪽이나 다른 요소에 잘리지 않게 한다.
2. 계산은 부모 크기, Safe Area, 설정이 바뀔 때만 수행한다. 매 프레임 검사는 값 비교로 끝나야 하며 할당이나 레이아웃 재구성을 일으키지 않는다.
3. 표시 상태 API는 다음과 같다.

   | API | 동작 |
   | --- | --- |
   | `IsShown` | 현재 목표 상태 |
   | `Show()`, `Hide()`, `SetShown(bool)` | 목표 상태를 바꾼다. 같은 상태면 아무것도 하지 않는다. |
   | `DockedOffset`, `SetDockedOffset(Vector2)` | 독에 붙은 상태의 추가 위치 오프셋(기본 0). 나중의 드래그 위치 설정이 이 값을 바꾼다. 결과 위치는 화면(Safe Area 포함) 안으로 제한한다. |
   | `HiddenOffset` | 숨김 상태 위치. 배치 루트 높이와 카드 돌출 높이, 아래 여백을 합한 만큼 아래로 내려 화면 밖에 둔다. |
   | `ShownChanged` | 상태가 실제로 바뀐 뒤 한 번 발생한다. |

4. 표시·숨김은 `PresentationRoot.anchoredPosition`만 바꾼다. 배치 루트의 위치·크기, Reel 레이아웃, 합계 카드의 배치는 바꾸지 않는다. 숨김 상태에서는 `CanvasGroup.alpha`를 0으로 둔다. GameObject는 끄지 않는다. 숨긴 상태에서 진행 중인 회전이 `OnDisable`로 취소되지 않게 하기 위해서다.
5. `ISlotReelDockTransition`은 `Play(RectTransform target, Vector2 from, Vector2 to, bool reduceMotion, CancellationToken token) → Task` 형태의 계약으로 정의하되, 이번 작업에서는 구현체를 만들지 않는다. 계약의 정확한 시그니처는 구현자가 정하고 보고한다. 전환이 비어 있거나 실패하면 즉시 목표 위치로 옮기고, 전환 중에 상태가 다시 바뀌면 이전 전환을 취소한다.

### 5.5 합계 카드

`Assets/TxTRPG/UI/Runtime/SlotMachine/SlotTotalCardView.cs`(`SlotTotalCardView`)는 `TotalCard` 배치 루트에 붙는다.

1. 모양 설정은 직렬화 가능한 값 형식 `SlotTotalCardAppearance`로 묶는다.

   | 필드 | 기본값 | 의미 |
   | --- | --- | --- |
   | `shape` | `Rectangle` | `Rectangle`, `RoundedRectangle`, `Circle`, `Ellipse` 중 하나. `UiShape`를 그대로 쓰되 `Triangle`은 허용하지 않는다. `Triangle`이나 알 수 없는 값이 들어오면 `Rectangle`로 그리고 한 번 경고한다. |
   | `cornerRadiusUnit` | `ShortSideRatio` | `ShortSideRatio`(카드 짧은 변 대비 비율, 0~0.5) 또는 `Pixels`(Canvas 단위) |
   | `cornerRadius` | 0.2 | 둥근 정도. `RoundedRectangle`에만 쓰인다. 단위가 `ShortSideRatio`면 [0, 0.5]로, `Pixels`면 [0, 짧은 변/2]로 제한한다. |
   | `curveSegments` | 8 | 곡선 분할 수. `UiShapeGeometry`의 범위로 제한한다. |
   | `fillColor` | 흰색 #FFFFFF | 첨부 이미지의 흰색 카드 |
   | `borderEnabled`, `borderColor`, `borderThickness` | 꺼짐, #0A0A0A, 3 | 안쪽 테두리. 카드 크기를 바꾸지 않는다. |
   | `textColor` | #0A0A0A | 라벨·값 글자 색. 흰 카드 위에서 대비를 확보한다. |

2. 크기와 위치 설정은 다음과 같다.

   | 필드 | 기본값 | 의미 |
   | --- | --- | --- |
   | `heightRatio`, `minimumHeight`, `maximumHeight` | 0.42, 56, 140 | 카드 높이 = Reel 기계 높이 × 비율, 최소·최대 제한 |
   | `aspectRatio` | 1.8 | 카드 너비 = 높이 × 비율. `Circle`이면 1로 취급한다. Reel 기계 너비의 90%를 넘지 않게 제한한다. |
   | `horizontalPosition` | 0.5 | Reel 기계 너비 기준 카드 중심의 가로 위치(0 왼쪽, 1 오른쪽). 카드가 Reel 기계 바깥으로 나가지 않게 제한한다. |
   | `overhangRatio` | 0.5 | 카드 높이 중 Reel 기계 위쪽 가장자리 위로 나오는 비율(0이면 Reel 기계 안쪽 위에 붙고, 1이면 완전히 위에 놓임) |

3. 카드 크기와 위치는 Reel 기계의 효과 적용 전 RectTransform 크기를 기준으로 계산한다. 계산은 Reel 기계 크기나 설정이 바뀔 때만 수행한다. `SlotReelDock`은 카드 돌출 높이(`카드 높이 × overhangRatio`)를 읽어 5.4절의 배치 루트 위쪽 확보에 쓴다.
4. `TotalCard`는 `ReelMachine`보다 뒤의 형제로 두어 Reel과 Payline 위에 그려지게 한다. 카드가 Payline 칸을 가리지 않도록, 기본값에서 카드의 아래쪽 가장자리가 가장 위의 보이는 칸(±2칸) 범위 안에 머무르는지 테스트로 확인한다. 설정 조합에 따라 Payline을 가릴 수 있다면 Inspector Tooltip과 문서에 적는다.
5. 글자는 모양 안쪽 영역(`ContentRoot`)에 둔다. `Rectangle`·`RoundedRectangle`은 카드 사각형에서 안쪽 여백(짧은 변 × 0.08, 둥근 직사각형은 반지름 × 0.3을 더함)을 뺀 영역, `Ellipse`·`Circle`은 내접 사각형(가로·세로 × 0.707, `Circle`은 짧은 변 기준)을 쓴다. 글자 크기는 TMP Auto Size 대신 기존 `SlotHeightScaledText`(`fitWidth` 포함)로 정한다. 라벨과 값은 위아래로 놓고(라벨 0.35 : 값 0.65), 공간이 부족하면 라벨을 숨기지 않고 글자를 줄인다. 세 자리 합계(`999`)가 들어가는 크기를 `widthSample`로 쓴다.
6. 합계 배수(`TotalMultiplier`)는 기존 `SlotTotalMultiplierLayout`을 재사용하여 값 옆이나 위에 둔다. 재사용할 수 없으면 그 이유를 보고하고 같은 규칙(값 글자 영역 기준, 크기 = 값 글자 × 0.5, [12, 36])으로 구현한다.
7. 실행 중 변경 API `Appearance`, `SetAppearance(SlotTotalCardAppearance)`를 제공한다. 같은 값이면 아무것도 하지 않는다. 값이 바뀌면 `UiShapeGraphic.Configure`와 글자 영역만 다시 계산한다.
8. `UiShapeGraphic`의 `cornerRadius`는 픽셀 단위이므로, `ShortSideRatio`일 때 `SlotTotalCardView`가 카드 크기로 픽셀 값을 계산해 넘긴다. `UiShapeGraphic`과 `UiShapeGeometry`의 공개 동작은 바꾸지 않는다. 바꿔야 한다면 `ShapedButtonView`와 탐험 카드에 미치는 영향을 조사하고 보고한다.
9. 모든 카드 Graphic의 `raycastTarget`을 끈다. 공유 Material을 바꾸지 않는다. 효과는 `VisualRoot`에만 붙인다.

### 5.6 `Candidate_MainScene` 배치

```text
Canvas
├── MainArea                 (변경 없음)
└── OverlayLayer             (RectTransform Stretch, 오프셋 0, Graphic 없음)
    └── SlotReelDock         (SlotReelDock.prefab 인스턴스)
```

1. `Canvas`의 마지막 자식으로 `OverlayLayer`를 추가하고, 그 아래에 `SlotReelDock.prefab` 인스턴스를 둔다. `OverlayLayer`에는 Graphic을 두지 않아 입력을 막지 않는다.
2. `MainArea`, 세 Area, `Canvas`, `CanvasScaler`, `EventSystem`, 카메라, 조명은 바꾸지 않는다.
3. 인스턴스에서 Prefab 값을 재정의하지 않는다. 필요한 값은 Prefab에 저장한다.

### 5.7 적용 방식

이 작업의 Prefab 생성과 Scene 배치는 한 번만 적용하면 되므로 재사용 Builder를 만들지 않는다. Unity Editor 자동화로 직접 적용하는 방법을 우선한다. 자동화를 사용할 수 없으면 `TxTRPGEditorMenuPaths.Application + "Temporary/Apply Slot Reel Dock To Candidate Main Scene"` 임시 메뉴를 만든다. 임시 메뉴를 만드는 경우 다음을 지킨다.

- 클래스 주석에 목적, 임시 상태, 재실행 안전성을 기록한다.
- 실행 전 Play Mode·컴파일 중·대상 Scene 미저장 변경 여부를 검사하고, 조건이 맞지 않으면 아무것도 바꾸지 않는다.
- `SlotReelDock.prefab`이 이미 있으면 다시 만들지 않고, 없는 객체·참조만 채운다. `Candidate_MainScene`에 인스턴스가 이미 있으면 추가하지 않는다. 이미 목표 상태이면 파일을 쓰지 않고 그 사실을 로그로 남긴다.
- `SlotMachinePanel.prefab`, `SlotReel.prefab`, `TMP_MainScene`을 저장하지 않는다.
- 사용자가 적용과 검증을 끝낸 뒤 제거할 수 있다고 문서에 기록한다.

`SlotReelDock.prefab`은 생성 후 개발자 소유 자산으로 취급하며 등록 기반 Prefab Rebuild 배치에 넣지 않는다. `DOCS/architecture/prefab-rebuild-registry.md`의 제외 설명에 이 Prefab과 이유(Scene 배치를 함께 하는 일회성 적용, 이후 개발자 소유)를 추가한다.

### 5.8 Editor 미리보기 메뉴

반복해서 쓰는 확인 도구이므로 영구 메뉴로 둔다. Gameplay·Application 설정을 읽어야 하므로 `TxTRPG.Application.Editor` 어셈블리에 두고, 경로는 `TxTRPGEditorMenuPaths.Application + "Preview/Slot Reel Dock/"` 아래로 한다.

| 메뉴 | 동작 |
| --- | --- |
| `Spin` | Play Mode에서 활성 콘텐츠 Scene의 `SlotReelDock`을 찾아, `PlayerSessionHost.TemporarySlotMachine`(`TemporarySlotMachineState`, 없으면 `TemporarySlotMachineConfiguration.asset`)의 Reel 구성으로 미리보기 결과를 만들고 `PlaySpinAsync`로 재생한다. 정지 위치는 UI 미리보기 전용 난수로 정하며 세션 상태, 룬, Fortune Critical 상태를 바꾸지 않는다. 합계는 Payline 값의 합이다. |
| `Spin With Total Multiplier` | 위와 같되 합계 배수 `×2`를 표시해 배수 배치를 확인한다. |
| `Add Reel`, `Remove Reel` | 독에 표시 중인 Reel 구성에 Reel을 하나 더하거나 끝에서 하나 빼고 `SetReels`를 호출한다(최소 1개). 세션 구성은 바꾸지 않는다. |
| `Toggle Shown` | `SetShown(!IsShown)` |
| `Cycle Card Shape` | 합계 카드 모양을 `Rectangle` → `RoundedRectangle` → `Circle` → `Ellipse` 순서로 바꾼다. Prefab에는 저장하지 않는다. |

- Play Mode가 아니거나, `SlotReelDock`이 없거나 둘 이상이면 이유를 Console에 남기고 아무것도 하지 않는다. 회전 중에 `Spin`을 다시 실행하면 이전 회전을 `StopAtResult`로 끝내고 새로 시작한다.
- Reduce Motion은 `PlayerSessionHost.TemporaryDicePreferences.ReduceMotion`을 따른다. 세션이 없으면 꺼짐으로 둔다.
- `Assets/TxTRPG/Editor/Tests/Editor/EditorMenuTests.cs`가 검사하는 어셈블리 목록에 `TxTRPG.Application.Editor`가 포함되는지 확인한다. 포함되면 기대 목록에 새 메뉴를 추가하고, 포함되지 않으면 그 사실을 보고한다. `isValidateFunction` 메뉴 함수는 추가하지 않고 실행 시점에 검사한다.

### 5.9 공통 품질 요구

- 회전 중 매 프레임 바뀌는 값은 기존 `SlotReelView`의 범위를 넘지 않는다. 독과 카드의 배치 계산은 크기·설정 변경 때만 실행한다.
- 색만으로 의미를 전달하지 않는다. 합계는 숫자와 라벨로, 배수는 `×N` 글자로 전달한다.
- 사용자에게 보이는 문구는 하드코딩하지 않고 표시 데이터(`SlotSummaryDisplay`)에서 받는다. Prefab 초기 문구(`합계`, `—`)는 표시 데이터가 오기 전의 기본값이다.
- 플랫폼별 전처리 지시문을 쓰지 않는다. Safe Area는 `Screen.safeArea`로 모든 플랫폼에서 같은 경로로 처리한다.

## 6. 호환성과 실패 동작

- `SlotMachinePanel`의 직렬화 필드와 Prefab은 바뀌지 않으므로 `TMP_MainScene`과 기존 저장 자산은 영향을 받지 않는다. 작업 후 `git diff`에 `SlotMachinePanel.prefab`, `SlotReel.prefab`, `TMP_MainScene.unity`가 없어야 한다.
- 저장 데이터, 입력 바인딩, 플레이어 설정 형식은 바뀌지 않는다.
- 슬롯 Reel 독에 Reel Prefab, Reel 영역, 회전 설정, 스킨 카탈로그 참조가 비어 있으면 `SlotMachinePanel`과 같은 규칙을 따른다: 회전 설정은 기본값으로 대체하고 한 번 경고하며, 스킨은 Prefab 기본 외형을 쓰고 한 번 경고한다. Reel을 만들 수 없으면 `PlaySpinAsync`는 `false`를 돌려준다. 게임 시작은 막지 않는다.
- `ShowError`는 `ReelMachine`과 합계 카드를 숨기고 `StatusText`에 사유를 표시한다. `ClearError`는 되돌린다.
- 합계 카드의 `UiShapeGraphic` 참조가 비어 있으면 카드 글자만 표시하고 한 번 경고한다.
- 숨김 상태에서 `PlaySpinAsync`를 호출해도 연출은 정상적으로 진행되고 결과를 돌려준다. 보이지 않을 뿐이다.
- 비활성화·파괴·취소 시 회전은 `false`를 돌려주고, 잔상과 `VisualRoot` 배율을 되돌린다(기존 Reel 동작).

## 7. 검증 기준

### 7.1 Edit Mode 테스트

새 테스트 `Assets/TxTRPG/UI/Tests/Editor/SlotReelDockTests.cs`(`TxTRPG.UI.Tests.Editor`)를 추가한다. 임시 Canvas에서 Prefab을 인스턴스화해 검사하고, 자산을 저장하지 않는다.

| 테스트 | 확인 내용 |
| --- | --- |
| Prefab 계층 | `ReelMachine`, `TotalCard`(뒤 형제), `PresentationRoot`가 있고 모든 저작 Graphic의 `raycastTarget`이 꺼져 있으며 `CanvasGroup.blocksRaycasts`가 꺼져 있다. 룬 표가 없다. |
| Reel 수 변경 | 1, 3, 5개로 바꿀 때 Reel·구분대 수와 형제 순서, Payline 범위가 맞다. |
| 회전 | `PlaySpinAsync`(Reduce Motion)와 `StopAtResult`가 같은 결과 상태로 끝나고, 합계 카드가 `TotalValue`·`TotalMultiplierText`를 표시하며 룬 항목은 무시된다. |
| 표시·숨김 | 숨김 시 `PresentationRoot`가 화면 밖에 있고 `alpha`가 0이며, 배치 루트와 Reel 레이아웃 크기는 같다. 같은 상태 요청은 `ShownChanged`를 발생시키지 않는다. |
| 배치 | 여러 부모 크기(1920×1080, 2560×1080, 1024×768, 390×844, 750×1334)에서 독 높이가 최소·최대 범위 안이고, 최대 너비 제한과 가운데 정렬이 적용되며, 카드가 부모 위쪽을 넘지 않는다. |
| 카드 모양 | 네 모양이 각각 `UiShapeGraphic`에 반영되고, `Triangle`은 `Rectangle`로 대체되며, `ShortSideRatio`·`Pixels` 반지름이 제한 범위 안의 픽셀 값으로 변환된다. `Circle`·`Ellipse`의 글자 영역이 내접 사각형이다. |
| 카드 위치 | 기본값에서 카드 중심이 Reel 기계 가운데이고 높이의 50%가 위쪽 가장자리 위에 있으며, Payline 칸을 가리지 않는다. |

리팩터링 회귀를 위해 다음 필터만 실행한다. 전체 Edit Mode 테스트는 실행하지 않는다.

| 필터 | 이유 |
| --- | --- |
| `SlotReelDockTests` | 새 기능 |
| `SlotMachinePanelTests`, `SlotMachinePanelSkinTests` | `SlotReelMachineCore` 분리 후 기존 패널 동작 유지 |
| `SlotMachineCombatTests`, `SlotFortuneCriticalCombatTests`, `ExplorationEventControllerTests` | `SlotMachinePanel`을 사용하는 컨트롤러 회귀. `AssetWriting` 카테고리는 `!AssetWriting`으로 제외한다. |
| `BoundaryCommandButtonTests` | `UiShapeGraphic`·`UiShapeGeometry`를 바꾼 경우에만 |
| `EditorMenuTests` | 새 메뉴가 검사 대상 어셈블리에 포함되는 경우 |

테스트 전후의 Git 상태를 기록하고, 테스트가 작업 범위 밖의 파일을 바꿨다면 작업 전에 수정되지 않았던 파일만 되돌린다.

### 7.2 Editor와 Play Mode 확인

1. 아무 Scene에서 Play를 시작하면 AppScene을 거쳐 `Candidate_MainScene`이 로드되고, 화면 하단에 Reel 3개가 있는 슬롯 Reel 독이 보이며, 흰색 직사각형 합계 카드가 Reel 기계 위쪽 가장자리 가운데에 걸쳐 놓인다.
2. 세 Area의 1:3:1 배치는 바뀌지 않고, 독 아래로 가려진 영역을 클릭해도 입력이 막히지 않는다(현재 콘텐츠가 없으므로 EventSystem의 레이캐스트 결과로 확인한다).
3. 5.8절의 미리보기 메뉴를 차례로 실행하여 회전·합계·배수·Reel 수 변경·표시/숨김·카드 모양 변경이 동작하는지 확인한다.
4. Inspector에서 합계 카드의 `shape`를 `RoundedRectangle`로 바꾸고 `cornerRadius`를 0, 0.2, 0.5로 바꿔 둥근 정도가 바뀌는지 확인한다. Play Mode에서 바꾼 값은 저장되지 않음을 확인한다.
5. Game View를 1920×1080, 2560×1080, 1280×720, 1080×1920, 750×1334로 바꿔 독과 카드가 잘리지 않고 숫자가 읽히는지 확인한다. 임시로 추가한 Game View 크기는 확인 후 제거한다. 실제 기기의 Safe Area는 Device Simulator로 노치가 있는 기기 하나 이상을 확인하고, 확인하지 못했다면 그렇게 보고한다.
6. `TMP_MainScene`으로 전환(`Tools > TxT RPG > Application > Play Mode > Load TMP_MainScene`)한 뒤 행동 버튼을 눌러 기존 `SlotMachinePanel`의 회전·합계·룬 표가 이전과 같게 동작하는지 확인한다.

### 7.3 직렬화 변경 검토

적용과 테스트 후 `serialization-diff-reviewer`로 `.unity`, `.prefab`, `.asset`, `.meta` 변경을 검토한다. 의도한 변경은 새 `SlotReelDock.prefab`(.meta), 새 스크립트의 `.meta`, `Candidate_MainScene.unity`뿐이다. 작업 전부터 있던 사용자 변경은 되돌리지 않는다.

## 8. 문서 갱신

| 문서 | 갱신 내용 |
| --- | --- |
| `DOCS/architecture/slot-machine-and-runes.md` | `SlotReelMachineCore` 분리와 `ISlotMachinePresenter`, 슬롯 Reel 독 절(계층, 배치 설정, 표시·숨김 API, 합계 카드 모양·크기·위치 설정, 실패 동작), 확장 지점 표에 전환 연출·드래그 위치·컨트롤러 연결 방법 추가 |
| `DOCS/architecture/project-structure.md` | `UI/Runtime/SlotMachine/`의 새 파일과 `UI/Prefabs/SlotMachine/SlotReelDock.prefab`, `Candidate_MainScene`의 `OverlayLayer` |
| `DOCS/architecture/prefab-rebuild-registry.md` | `SlotReelDock.prefab`의 제외 이유 |
| `DOCS/development/workflows.md` | 미리보기 메뉴 경로와 확인 절차, 임시 적용 메뉴를 만든 경우 그 절차와 은퇴 시점 |
| `DOCS/instructions/README.md` | 이 지침서의 구현 상태가 바뀌면 설명 갱신 |

## 9. 최종 보고

다음을 구분하여 보고한다.

1. 구현 완료: 코드·테스트·문서 변경 목록, `ISlotMachinePresenter`의 최종 멤버 목록과 `ISlotReelDockTransition` 시그니처
2. 적용 완료: Editor에서 만들고 저장한 Prefab과 Scene, 확인한 값
3. 적용 필요: 사용자가 실행해야 하는 임시 메뉴가 남았다면 `사용자가 수행할 적용 절차` 절과 복사 가능한 순서
4. 검증: 실행한 테스트 필터와 결과, Editor·Play Mode 확인 항목, 확인하지 못한 화면 크기·기기·빌드 대상
5. 직렬화 검토 결과와 되돌린 파일, 작업 전부터 있던 사용자 변경
6. 후속 작업: `Candidate_MainScene`의 실제 전투·이벤트 연결(컨트롤러 필드의 계약 전환), 등장 전환 연출과 드래그 위치 설정 구현, 세 Area가 독에 가려지는 공간 처리, 합계 카드 스킨 연동
