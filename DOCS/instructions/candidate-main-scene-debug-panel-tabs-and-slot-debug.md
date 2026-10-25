# Candidate_MainScene 디버그 패널 Tab 구성과 Slot 디버그 Tab

## 1. 목적과 확정 사항

`Candidate_MainScene`의 디버그 패널(`EncounterDebugPanel`)을 Tab으로 나누어, 서로 분리된 페이지에서 설정과 테스트를 수행할 수 있게 한다. Tab은 "이벤트 판정"과 "슬롯" 두 개이다. 이벤트 판정 Tab은 현재 구성 요소와 동작을 그대로 유지한다. 슬롯 Tab에서는 Reel 수, Reel별 Critical(Fortune Critical) 확률, 합계 Critical 확률을 조절하고, 슬롯을 회전시키며, 하단 슬롯 Reel 독(`SlotReelDock`, 이하 "SlotMachine" 또는 "독")을 화면 안/밖으로 이동시킨다. 독의 이동은 easeInOutBack 함수를 따르고, 이동 시간은 디버그와 무관하게 개발자가 설정할 수 있다.

이 문서는 후속 구현 지침이며, 적용이나 실행 검증을 완료했다는 기록이 아니다.

### 1.1 사용자가 확정한 사항 (2026-10-21)

| 항목 | 확정 내용 |
| --- | --- |
| Tab 구성 | 디버그 패널을 Tab으로 구성하고, Tab마다 분리된 페이지에서 설정과 테스트를 수행한다. Tab은 이벤트 판정 Tab과 Slot Tab이다. |
| 이벤트 판정 Tab | 현재 디버그 패널의 구성 요소(슬라이더 5개와 값 텍스트, `이벤트 발생`, `기본값으로`, 결과 문구)를 그대로 유지한다. |
| Reel 수 | Slot Tab에서 Reel 수를 1~10개 범위에서 조절한다. 늘릴 때 추가되는 Reel은 마지막 Reel의 칸 구성을 복사한다. Reel마다 Critical 확률 조절 행이 자동으로 하나씩 생긴다. 내용이 길어지면 Tab 내용은 세로로 스크롤된다. |
| Reel별 Critical 확률 | Slot Tab에서 각 Reel의 Critical 확률을 조절한다. |
| 합계 Critical 확률 | Slot Tab에서 합계에 대한 Critical 확률을 조절한다. |
| 변경 범위 | Reel 수와 Critical 확률 변경은 세션 상태(`TemporarySlotMachineState`, `TemporaryFortuneCriticalState`)를 기존 `Try...` API로 직접 바꾼다. 같은 세션의 전투·탐험 이벤트 슬롯 판정도 같은 값을 쓴다. 설정 자산은 바꾸지 않으며, Play Mode가 끝나면 변경도 사라진다. |
| 회전 버튼 | Slot Tab에 Slot을 돌리는 버튼을 둔다. 세션 슬롯머신의 Reel로 회전하고 Fortune Critical 판정을 실제로 수행하여, 독의 합계 카드에 최종 합계와 배수를 표시한다. 룬 분배와 전투에는 적용하지 않는다. |
| 화면 밖/안 이동 | Slot Tab의 버튼으로 독을 화면 밖/안으로 움직인다. 움직임은 easeInOutBack 함수를 따른다. |
| 이동 시간 | 디버그와 무관하게 개발자가 설정할 수 있어야 한다. 들어오는 시간(In)과 나가는 시간(Out)을 따로 설정하며 둘 다 기본값은 0.3초이다. 동작 줄이기(Reduce Motion)가 켜져 있으면 즉시 이동한다. |
| 레터박스 | 이동 중 독이 화면 프레임의 아래쪽 가장자리를 넘어가는 부분은 잘라서 보이지 않게 한다. 레터박스 위에는 독을 그리지 않는다. |

### 1.2 이 문서가 정한 기본값

사용자 답변 이후 결과에 큰 영향이 없어 지침서가 정한 값이다. 구현자는 이 값을 그대로 사용하고, 바꿀 필요가 생기면 근거와 함께 보고한다. 길이는 1920×1080 Canvas 단위이다.

| 항목 | 기본값 |
| --- | --- |
| Tab 이름과 순서 | `이벤트 판정`, `슬롯`. Play 시작 시 `이벤트 판정` Tab이 선택되어 있다. 선택한 Tab은 Scene 수명 동안만 유지하고 저장하지 않는다. |
| 패널 제목 | Header의 제목을 `DEBUG · 이벤트 판정`에서 `DEBUG`로 바꾼다. 어느 Tab이 선택되었는지는 Tab 버튼이 표시한다. |
| Prefab·오브젝트 이름 | `EncounterDebugPanel.prefab`과 Scene의 `OverlayLayer/EncounterDebugPanel` 이름은 유지한다. 이름 변경은 이 작업에 포함하지 않는다. |
| 접기 | 기존 `접기`/`펼치기` 버튼은 Tab 줄과 모든 페이지를 함께 접는다. |
| Reel 수 조절 UI | `−` 버튼, 현재 Reel 수 텍스트, `+` 버튼. 1개에서는 `−`, 10개 이상에서는 `+`가 비활성화된다. 슬라이더를 쓰지 않는 이유는 한 번에 한 Reel씩 추가·제거되는 동작이 Gamepad와 터치에서 더 예측 가능하기 때문이다. |
| Reel 최대 수 | 컨트롤러의 직렬화 필드 `maximumReelCount`(기본 10, 최소 1). 세션 Reel 수가 이미 최대보다 많으면 `+`만 비활성화하고 기존 Reel은 그대로 둔다. |
| 추가 Reel의 ID | `reel.{n}` 형식에서 현재 세션에 없는 가장 작은 n(1부터). 칸 구성은 추가 시점에 마지막 Reel의 Strip 값을 복사한다. |
| Reel 제거 | 항상 마지막 Reel을 제거한다. 제거한 Reel의 Reel별 Fortune 규칙도 `TryRemoveReelRule`로 함께 제거하여, 같은 ID로 다시 추가된 Reel이 기본 규칙을 따르게 한다. |
| 확률 슬라이더 | Reel별·합계 모두 0~100(정수 = 0%~100%, 1% 단위). 값 텍스트는 `5%` 형식이다. 이벤트 판정 Tab의 확률 슬라이더와 같은 표시 규칙(`EncounterDebugPanel.FormatChance`)을 쓴다. |
| Reel 행 표시 | `Reel {순번}` 라벨(1부터, 현재 순서), 슬라이더, 값 텍스트. 값은 `FortuneCriticalRules.RuleForReel(reelId).Chance`이다. 개별 규칙이 없는 Reel은 기본 Reel 규칙의 확률을 보여 준다. |
| 확률 표시 반올림 | 설정 자산에 1% 단위가 아닌 값(예: 0.025)이 있으면 슬라이더는 반올림한 값을 보여 주지만, 사용자가 슬라이더를 움직이기 전에는 세션 값을 바꾸지 않는다. |
| 버튼 구성 | `회전`, `화면 안으로`, `화면 밖으로`, `기본값으로`. 독이 이미 표시 상태이면 `화면 안으로`, 숨김 상태이면 `화면 밖으로`가 비활성화된다. |
| `기본값으로` | 세션 Reel을 `TemporarySlotMachineConfiguration.asset`의 Reel로(`SlotMachine.TryReplaceAll`), Fortune 규칙을 `TemporaryFortuneCriticalConfiguration.asset`으로 만든 규칙으로(`TemporaryFortuneCriticalState.TryReplaceAll`) 되돌린다. 독의 표시 상태는 바꾸지 않는다. |
| 회전 중 제한 | 회전 중에는 `회전`, `−`, `+`, `기본값으로`가 비활성화된다. 확률 슬라이더와 `화면 안으로`/`화면 밖으로`는 사용할 수 있으며, 확률 변경은 다음 회전부터 반영된다. 숨긴 상태에서도 회전할 수 있고, 결과는 결과 문구로 확인한다. |
| 결과 문구 | 각 Reel의 Payline 값을 순서대로 `, `로 잇고, 배수가 1보다 크면 `5×2`처럼 붙인다. 합계 배수가 1보다 크면 ` → 합계 ×t`, 마지막에 ` = 최종 합계`를 붙인다. 상한이 적용된 Reel이 있으면 끝에 ` (상한 ×C 적용)`을 붙인다. 예: `3, 5×2, 2 → 합계 ×3 = 45`. |
| 난수 | 회전용과 Fortune 판정용 `SystemRandomIndexProvider`를 따로 두고, 각각 GUID로 시드를 정한다(`TemporarySlotCombatController`와 같은 이유). 테스트는 Editor 전용 주입 메서드로 바꾼다. |
| Slot 페이지 높이 | 페이지 내용은 `ScrollRect`(세로만) 안에 둔다. 페이지 높이는 `min(내용 선호 높이, 520)`이며, Reel 행 수가 바뀔 때만 다시 계산한다. |
| 이동 시간 기준 | `Time.unscaledDeltaTime`. 게임 시간 배율의 영향을 받지 않는다. |
| easing 필드 | 전환 컴포넌트에 `SlotEasingKind easing = InOutBack`을 직렬화 필드로 둔다. 기본값만 사용하고, 다른 값은 개발자가 Inspector에서 고를 수 있다. |

## 2. 작성 시점에 확인한 현재 상태

### 2.1 디버그 패널 (`EncounterDebugPanel`)

- 구조와 판정 규칙은 [요소 토글과 이벤트 판정](../architecture/encounter-roll.md)에 있다. Prefab은 `Assets/TxTRPG/UI/Prefabs/Encounters/EncounterDebugPanel.prefab`이고 컴포넌트는 `Assets/TxTRPG/UI/Runtime/Encounters/EncounterDebugPanel.cs`(네임스페이스 `TxTRPG.UI.Encounters`)이다.
- 현재 계층은 `EncounterDebugPanel`(Image, `EncounterDebugPanel`, `DevelopmentBuildOnly`, VerticalLayoutGroup, ContentSizeFitter 세로) > `Header`(Title `DEBUG · 이벤트 판정`, `CollapseButton`)·`Body`(슬라이더 행 5개, `Buttons`, `ResultText`)이다. `EncounterDebugPanel.body`는 `Body`를 참조하고, `SetExpanded`가 `Body`를 켜고 끈다.
- Scene 배치는 `Candidate_MainScene/Canvas/ScreenFrame/OverlayLayer/EncounterDebugPanel`(오른쪽 위 앵커, 여백 16, 폭 320)이며, `Canvas/EncounterRollController`가 이 패널을 참조한다.
- `EncounterDebugPanelTests`는 `CollapseButton`이 `Body`를 켜고 끄는지 확인한다.

### 2.2 슬롯 Reel 독 (`SlotReelDock`)

- 구조는 [슬롯머신과 룬](../architecture/slot-machine-and-runes.md#slotreeldock-candidate_mainscene-하단-슬롯-reel-독)에 있다. Prefab은 `Assets/TxTRPG/UI/Prefabs/SlotMachine/SlotReelDock.prefab`이고 Scene 인스턴스는 `Canvas/ScreenFrame/OverlayLayer/SlotReelDock`이다.
- `Show()`, `Hide()`, `SetShown(bool)`, `IsShown`, `ShownChanged`, `ReduceMotion`이 있다. 표시·숨김은 `PresentationRoot.anchoredPosition`만 바꾸고, 숨김 위치(`HiddenOffset`)는 부모(`OverlayLayer`)의 아래쪽 밖이다. 숨김 상태에서는 `CanvasGroup.alpha`가 0이다. 전환 중에는 alpha를 1로 두고, 전환이 끝나면 `SnapToState`가 숨김 상태의 alpha를 0으로 만든다.
- 전환 확장 지점 `ISlotReelDockTransition.PlayAsync(RectTransform target, Vector2 from, Vector2 to, bool reduceMotion, CancellationToken)`은 있지만 구현체가 없다. `transitionBehaviour`가 비어 있어 현재는 즉시 이동한다. 상태가 다시 바뀌면 독이 이전 토큰을 취소하고 현재 위치에서 다음 전환을 시작한다. 전환이 예외로 끝나면 독이 목표 위치로 옮긴다.
- `Candidate_MainScene`에는 독을 세션 슬롯머신에 연결하는 컨트롤러가 없다. 독은 Prefab의 기본 Reel 3개를 보여 주며, 회전은 영구 Editor 메뉴 `Tools > TxT RPG > Application > Preview > Slot Reel Dock > Spin`(`SlotReelDockPreviewMenu`, 미리보기 전용 난수, Fortune 판정 없음)으로만 확인할 수 있다.
- `OverlayLayer`에는 `RectMask2D`가 없다. 세로로 긴 화면에서는 프레임 아래에 레터박스가 있으므로, 전환 중 독이 프레임 아래쪽 밖에 그려질 수 있다.

### 2.3 재사용할 수 있는 요소

| 요소 | 위치 | 용도 |
| --- | --- | --- |
| `SlotEasing.Evaluate(SlotEasingKind.InOutBack, t)` | `UI/Runtime/SlotMachine/SlotEasing.cs` | easings.net 정의의 easeInOutBack. E(0) = 0, E(1) = 1이고 양 끝에서 범위를 벗어났다가 돌아온다. |
| `TemporarySlotMachineState`(`Machine.TryAddReel`, `TryRemoveReel`, `TryReplaceAll`, `Changed`) | `Application/Runtime/SlotMachine/TemporarySlotMachineConfiguration.cs` | 세션 Reel 변경 |
| `TemporaryFortuneCriticalState`(`TrySetReelChance`, `TrySetTotalChance`, `TryRemoveReelRule`, `TryReplaceAll`, `Rules`, `Changed`) | `Application/Runtime/SlotMachine/TemporaryFortuneCriticalConfiguration.cs` | 세션 Fortune 규칙 변경 |
| `TemporaryFortuneCriticalConfiguration.TryCreateRules(FortuneWeightFunctionRegistry.Default, ...)` | 같은 파일 | `기본값으로`의 규칙 생성 |
| `FortuneCriticalResolver.Resolve(SlotSpinResult, FortuneCriticalRules, IRandomIndexProvider)` | `Gameplay/Runtime/SlotMachine/FortuneCritical/` | 회전 결과의 Fortune 판정 |
| `TemporarySlotCombatController.CreateSpinDisplay(SlotSpinResult, FortuneCriticalResult, IReadOnlyList<SlotRuneLineDisplay>)`, `CreateSummary` | `Application/Runtime/SlotMachine/TemporarySlotCombatController.cs` | 독에 넘길 표시 데이터(룬 줄은 빈 배열) |
| `PlayerSessionHost.Instance`의 `TemporarySlotMachine`, `TemporaryFortuneCritical`, `TemporaryDicePreferences.ReduceMotion` | `Application/Runtime/Players/PlayerSessionHost.cs` | 세션 상태와 동작 줄이기 설정. `TemporaryRuneGridBinder`와 같은 방식으로 `Start`에서 연결한다. |
| `DevelopmentBuildOnly` | `UI/Runtime/` | Release Player에서 패널 비활성화(기존 그대로) |

## 3. 선행 조건

- [candidate-main-scene-encounter-toggles-and-debug-roll.md](candidate-main-scene-encounter-toggles-and-debug-roll.md), [candidate-main-scene-slot-reel-dock.md](candidate-main-scene-slot-reel-dock.md), [slot-fortune-critical.md](slot-fortune-critical.md), [screen-frame-letterbox-resolution-policy.md](screen-frame-letterbox-resolution-policy.md)의 구현이 적용되어 있어야 한다.
- 작업 전에 `DOCS/development/resolution-and-screen-frame.md`, `DOCS/development/visual-effect-readiness.md`, [요소 토글과 이벤트 판정](../architecture/encounter-roll.md), [슬롯머신과 룬](../architecture/slot-machine-and-runes.md), [화면 프레임과 레터박스](../architecture/screen-frame.md)를 읽는다.
- 작업 시작 시 `git status`를 기록한다. 작성 시점의 작업 트리에는 사용자의 미커밋 변경(`Candidate_MainScene.unity`, `TemporaryCandidateLayoutProbe.cs`, 폰트 Fallback 자산 등)이 있을 수 있다. 이 변경을 커밋·Stash·되돌리지 말고, Scene을 수정하기 전에 미저장·미커밋 변경과 겹치는지 확인한다. 겹치면 사용자에게 진행 방법을 묻는다.
- 변경이 여러 어셈블리와 Prefab·Scene 계층에 걸치므로 편집 전에 `unity-impact-scout`로 `EncounterDebugPanel`, `SlotReelDock`, `ISlotReelDockTransition`, `OverlayLayer/SlotReelDock` 경로 문자열을 쓰는 코드(임시 메뉴, 검증 도구, 테스트)를 조사한다.

## 4. 작업 범위

### 4.1 포함

1. 디버그 패널의 Tab 구성: 범용 Tab 컴포넌트, `EncounterDebugPanel.prefab` 계층 재구성(기존 구성 요소 보존), 제목 변경.
2. Slot Tab 표시 컴포넌트와 Prefab 내 페이지 구성.
3. `ISlotReelDockTransition` 계약 확장과 easing 기반 전환 컴포넌트, `SlotReelDock.prefab`에 전환 연결.
4. 독을 화면 프레임 경계에서 잘라내는 클리핑 컨테이너와 `Candidate_MainScene` 배치 변경.
5. Scene 소유 컨트롤러: 세션 Reel을 독에 표시, Slot Tab의 요청 처리(Reel 수, 확률, 회전, 화면 안/밖, 기본값).
6. Prefab·Scene 적용, Edit Mode 테스트, Play Mode 확인, 문서 갱신.

### 4.2 제외

- 이벤트 판정 Tab의 동작·값·문구 변경. 이벤트 판정과 슬롯 회전의 연결.
- 회전 결과를 룬 분배, 전투, Story, 저장에 적용하는 작업.
- Reel 칸 구성(Strip 값) 편집 UI, Fortune 배수 범위·분포 함수·상한 편집 UI.
- 설정 자산(`TemporarySlotMachineConfiguration.asset`, `TemporaryFortuneCriticalConfiguration.asset`, `EncounterRollConfiguration.asset`) 값 변경.
- `TMP_MainScene`, `SlotMachinePanel`, `SlotReelDockPreviewMenu`의 동작 변경. 독의 드래그 배치.
- 디버그 패널 Prefab·오브젝트 이름 변경, 패널 위치·폭 변경.

## 5. 구현 요구 사항

### 5.1 Tab 컴포넌트 (`TxTRPG.UI`, `Assets/TxTRPG/UI/Runtime/DebugPanels/`, 네임스페이스 `TxTRPG.UI.DebugPanels`)

`DebugPanelTabGroup`(MonoBehaviour)을 추가한다. 이벤트 판정·슬롯 규칙을 알지 못하는 표시 컴포넌트이다.

- 직렬화 필드: Tab 항목 목록(항목마다 `Button`, 라벨 `TMP_Text`, 선택 표시 오브젝트, 페이지 `GameObject`), 시작 Tab 순번(기본 0).
- 공개 API: `SelectedIndex`, `Select(int index)`(범위 밖이면 경고 후 무시, 같은 Tab이면 아무것도 하지 않음), `event Action<int> SelectionChanged`, `IsValid(out string missing)`.
- 선택된 Tab의 페이지만 활성화한다. 비활성 페이지의 컨트롤은 `SetActive(false)`로 Navigation과 Raycast 대상에서 빠진다.
- 선택 여부는 색만으로 나타내지 않는다. 선택 표시 오브젝트(예: 아래쪽 막대 `UiShapeGraphic`)와 라벨 글꼴 굵기 변경을 함께 쓴다.
- Tab 버튼을 눌러 Tab을 바꾸면 포커스는 누른 Tab 버튼에 그대로 둔다. Tab 버튼의 Navigation은 Explicit으로 좌우 Tab 버튼, 아래는 선택된 페이지의 첫 컨트롤로 연결한다. 페이지가 바뀌면 아래 방향 대상도 갱신한다. 패널은 Play 시작 시 포커스를 가져가지 않는다(게임 화면의 초기 선택을 빼앗지 않는다).
- Tab 버튼은 최소 높이 48, 최소 폭 96이다.

### 5.2 Slot Tab 표시 (`TxTRPG.UI`, `Assets/TxTRPG/UI/Runtime/SlotMachine/`, 네임스페이스 `TxTRPG.UI.SlotMachine`)

`SlotDebugTabView`(MonoBehaviour)와 표시 데이터 `SlotDebugReelEntry`(readonly struct: `ReelId`, `Label`, `Chance`)를 추가한다. 이 컴포넌트는 Gameplay·Application 타입을 쓰지 않고 표시 데이터와 이벤트만 주고받는다(`EncounterDebugPanel`과 같은 원칙).

- 이벤트: `ReelCountChangeRequested(int delta)`(−1 또는 +1), `ReelChanceChanged(string reelId, float chance)`, `TotalChanceChanged(float chance)`, `SpinRequested`, `ShowRequested`, `HideRequested`, `ResetRequested`.
- 알림 없이 값을 반영하는 메서드: `SetReels(IReadOnlyList<SlotDebugReelEntry> reels, bool canRemove, bool canAdd)`, `SetTotalChance(float chance)`, `SetShown(bool shown)`(두 이동 버튼의 interactable 갱신), `SetSpinning(bool spinning)`(1.2의 회전 중 제한 적용), `SetSessionAvailable(bool available)`, `ShowResult(string message)`.
- Reel 행은 Prefab 안의 비활성 행 템플릿을 복제해 만들고, 행 수가 줄면 남는 행을 비활성화해 다시 쓴다(풀링). 행을 다시 만들 때 매 프레임 생성하지 않는다. `SetReels`가 같은 ID 목록을 받으면 행을 새로 만들지 않고 값만 갱신한다.
- 슬라이더 값은 정수 0~100이며, 이벤트의 확률은 `값 / 100`이다. 슬라이더를 끌고 있는 동안 값이 바뀔 때마다 이벤트를 발생시킨다(이벤트 판정 Tab과 같은 동작).
- 행 수가 바뀌면 페이지 `LayoutElement.preferredHeight`를 `min(내용 선호 높이, 520)`으로 다시 계산한다. 매 프레임 계산하지 않는다.
- (기록) 페이지 높이 규칙은 2026-10-21 아래 여백 기준으로 바뀌었고, 2026-10-25 [candidate-main-scene-slot-debug-tab-page-height-fix.md](candidate-main-scene-slot-debug-tab-page-height-fix.md)에서 계산 시점과 `EncounterPage` 기준 높이 하한이 추가되었다. 현재 규칙은 `DOCS/architecture/slot-machine-and-runes.md`의 슬롯 디버그 Tab 절을 따른다.
- Reel 행의 Navigation은 위아래로 이어지고, 행이 추가·제거되면 갱신한다. 마지막 행 아래는 합계 확률 행, 그 아래는 버튼 줄이다. 포커스된 행이 제거되면 포커스를 `−` 버튼으로 옮긴다.
- 모든 버튼은 최소 높이 48이다. 버튼 4개는 2열 2행(`회전`·`기본값으로`, `화면 안으로`·`화면 밖으로`)으로 배치한다.
- 글자는 키보드·Gamepad 기호를 하드코딩하지 않는다.

### 5.3 디버그 패널 Prefab 재구성

`EncounterDebugPanel.prefab`을 저장된 자산 그대로 수정한다. 전체를 다시 생성하지 않는다. 목표 계층은 다음과 같다.

```text
EncounterDebugPanel            Image, EncounterDebugPanel, DevelopmentBuildOnly, DebugPanelTabGroup, VerticalLayoutGroup, ContentSizeFitter(세로)
├── Header                     Title "DEBUG", CollapseButton("접기"/"펼치기")   (기존 오브젝트 유지)
└── Body                       EncounterDebugPanel.body가 계속 참조 (접으면 Tab 줄과 페이지가 함께 사라짐)
    ├── TabBar                 HorizontalLayoutGroup
    │   ├── EncounterTab       Button, HitArea, VisualRoot > Background, SelectedIndicator, Label "이벤트 판정"
    │   └── SlotTab            같은 구조, Label "슬롯"
    └── Pages
        ├── EncounterPage      기존 Body의 자식(OccurrenceRow, InclusionRow, CombatWeightRow, EventWeightRow, RestWeightRow, Buttons, ResultText)을 그대로 옮김
        └── SlotPage           ScrollRect(세로만, Clamped), LayoutElement(높이는 5.2 규칙), SlotDebugTabView
            └── Viewport       RectMask2D, 투명 Image
                └── Content    VerticalLayoutGroup, ContentSizeFitter(세로)
                    ├── ReelCountRow     Label "Reel 수" · MinusButton "−" · CountText · PlusButton "+"
                    ├── ReelRows         ReelRowTemplate(비활성): Label · Slider(0~100) · Value
                    ├── TotalChanceRow   Label "합계 Critical" · Slider(0~100) · Value
                    ├── Buttons          SpinButton "회전", ResetButton "기본값으로", ShowButton "화면 안으로", HideButton "화면 밖으로"
                    └── ResultText
```

- 기존 오브젝트(슬라이더, 버튼, 텍스트)는 새로 만들지 말고 옮긴다. `EncounterDebugPanel`의 직렬화 참조와 `EncounterRollController`의 Scene 참조가 그대로 유지되어야 한다. 옮긴 뒤 이벤트 판정 Tab의 행 크기·값·문구가 이전과 같아야 한다.
- `EncounterDebugPanel`의 공개 API와 이벤트는 바꾸지 않는다. `IsValid`가 기존 참조를 계속 검증한다. 패널 루트에 `DebugPanelTabGroup`을 추가하더라도 `EncounterDebugPanel`은 Tab 그룹을 알 필요가 없다.
- Tab 버튼의 효과 적용 대상은 `VisualRoot` 아래이며, `HitArea`와 레이아웃 루트는 효과가 바꾸지 않는다([시각 효과 대비](../development/visual-effect-readiness.md)).
- 새 Graphic의 `raycastTarget`은 입력 대상(HitArea, Slider, Button 배경, Viewport)만 켠다.
- 이 Prefab은 개발자 소유 자산이며 Prefab Rebuild 배치에 등록하지 않는다.

### 5.4 독 전환 계약과 easing 전환 컴포넌트

1. `ISlotReelDockTransition.PlayAsync`에 방향 매개변수를 추가한다: `Task PlayAsync(RectTransform target, Vector2 from, Vector2 to, bool showing, bool reduceMotion, CancellationToken cancellationToken)`. 작성 시점에는 구현체도, 이 계약을 쓰는 테스트도 없으므로 호환성 영향은 `SlotReelDock`의 호출부뿐이다. `SlotReelDock.MoveToState`가 `IsShown`을 넘긴다. 주석의 계약(목표 위치에서 정확히 끝남, 취소·예외 처리)은 유지한다.
2. `SlotReelDockEasedTransition`(MonoBehaviour, `UI/Runtime/SlotMachine/`, `ISlotReelDockTransition` 구현)을 추가한다.

| 필드 | 기본값 | 의미 |
| --- | --- | --- |
| `showDuration` | 0.3 (Min 0) | 화면 안으로 들어오는 시간(초) |
| `hideDuration` | 0.3 (Min 0) | 화면 밖으로 나가는 시간(초) |
| `easing` | `SlotEasingKind.InOutBack` | 위치 보간 곡선 |

- 위치는 `from + (to − from) × SlotEasing.Evaluate(easing, elapsed / duration)`이다. InOutBack은 시작과 끝에서 반대 방향으로 조금 넘어가므로 0~1로 자르지 않는다. 마지막에는 정확히 `to`에 둔다.
- 경과 시간은 `Time.unscaledDeltaTime`으로 누적한다. 프레임 대기는 기존 `SlotReelMachineCore`의 비동기 프레임 대기 방식을 따르고, 매 프레임 메모리를 할당하지 않는다.
- `reduceMotion`이 참이거나 시간이 0이면 즉시 `to`에 두고 끝낸다.
- 취소되면 `OperationCanceledException`으로 끝내고 위치는 그대로 둔다(독이 다음 전환을 현재 위치에서 시작한다).
- 중간에 방향이 바뀌어도 새 전환은 지정된 시간 전체를 쓴다. 남은 거리에 비례해 줄이지 않는다.
- `target` 외의 Transform, 레이아웃, alpha는 바꾸지 않는다.
3. `SlotReelDock.prefab` 루트에 `SlotReelDockEasedTransition`을 추가하고 `transitionBehaviour`에 연결한다. 개발자는 이 Prefab(또는 Scene 인스턴스 Override)에서 시간을 설정한다. 이 설정은 디버그 패널과 무관하며 독의 모든 `Show`/`Hide` 호출에 적용된다.
4. 독의 `ReduceMotion`은 컨트롤러가 세션의 `TemporaryDicePreferences.ReduceMotion`으로 설정한다(5.6).

### 5.5 프레임 경계 클리핑과 Scene 배치

- `Canvas/ScreenFrame/OverlayLayer` 아래에 `SlotReelDockClip`(RectTransform Stretch, 여백 0, `RectMask2D`, Graphic 없음)을 추가하고, 기존 `SlotReelDock` 인스턴스를 그 자식으로 옮긴다. 인스턴스를 지우고 다시 만들지 않는다(Prefab 연결과 기존 Override 보존). `SlotReelDockClip`은 `EncounterDebugPanel`보다 앞 형제로 둔다(디버그 패널이 독 위에 그려진다).
- 독의 배치는 부모 크기를 기준으로 계산하므로, `SlotReelDockClip`의 크기는 `OverlayLayer`와 같아야 한다. 옮긴 뒤 독의 표시 위치·크기가 이전과 같은지 측정한다.
- 이 클리핑으로 프레임 아래쪽을 넘는 부분(숨김 위치로 가는 구간, InOutBack이 넘어가는 구간)은 보이지 않는다. 표시 위치 근처에서 위로 넘어가는 구간은 프레임 안이므로 보인다. 나중에 독에 glow·outline 같은 효과를 추가하면 프레임 가장자리에서 잘릴 수 있다는 점을 아키텍처 문서에 적는다.
- `OverlayLayer/SlotReelDock` 경로를 문자열로 찾는 코드(임시 메뉴, `TemporaryScreenFrameVerification`, `TemporaryCandidateLayoutProbe` 등)를 조사해 새 경로 `OverlayLayer/SlotReelDockClip/SlotReelDock`을 반영할지 판단한다. `TemporaryCandidateLayoutProbe.cs`에는 사용자의 미커밋 변경이 있으므로 수정이 필요하면 먼저 사용자에게 묻는다. 이미 적용을 마쳐 다시 실행하지 않는 임시 메뉴는 수정하지 않고 문서에 그 사실을 적는다.
- `Canvas` 자식으로 `SlotReelDockDebugController` 오브젝트를 추가한다(5.6).

### 5.6 Scene 소유 컨트롤러 (`TxTRPG.Application`, `Assets/TxTRPG/Application/Runtime/SlotMachine/`, 네임스페이스 `TxTRPG.Application.SlotMachine`)

`SlotReelDockDebugController`(MonoBehaviour)를 추가한다. 독을 세션 슬롯머신에 연결하고 Slot Tab의 요청을 처리한다. 디버그 패널이 없어도(Release) 독 연결은 동작한다.

| 직렬화 필드 | 의미 |
| --- | --- |
| `dock` | `SlotReelDock` |
| `slotTab` | `SlotDebugTabView`(Release에서 비활성 패널 아래에 있어도 참조는 유지) |
| `slotMachineConfiguration`, `fortuneCriticalConfiguration` | `기본값으로`가 읽는 설정 자산. 이 컨트롤러는 자산을 바꾸지 않는다. |
| `maximumReelCount` | 기본 10, 최소 1 |

동작 요구:

1. `Start`에서 `PlayerSessionHost.Instance`로 연결한다. 세션이 없으면 경고를 한 번 남기고 Slot Tab에 `세션이 없습니다. AppScene에서 Play를 시작하세요.`를 표시하며, `화면 안으로`/`화면 밖으로`만 사용할 수 있게 한다.
2. 연결되면 세션 Reel로 `dock.SetReels`, 대기 상태 요약(`SlotCombatStoryFormatter.IdleTotalValue`)으로 `dock.SetSummary`를 호출하고, `dock.ReduceMotion`을 세션 설정으로 맞춘다. `TemporarySlotMachineState.Changed`와 `TemporaryFortuneCriticalState.Changed`를 구독하여 독과 Slot Tab을 갱신하고, `OnDestroy`에서 해제한다. 다른 기능이 세션을 바꿔도 Slot Tab 표시가 따라간다.
3. Reel 수 `+1`: 1.2의 ID·칸 구성 규칙으로 `SlotReel`을 만들어 `Machine.TryAddReel`. `−1`: 마지막 Reel을 `TryRemoveReel`한 뒤 그 ID의 `TryRemoveReelRule`. 범위 밖 요청은 무시한다. `Try...`가 실패하면 세션을 바꾸지 않고 사유를 결과 문구에 표시한다.
4. Reel 확률: `TrySetReelChance(reelId, chance)`. 합계 확률: `TrySetTotalChance(chance)`. 실패하면 사유를 표시하고 Slot Tab 값을 세션 값으로 되돌린다.
5. 회전: `TemporarySlotMachineState.Spin(spinRandom)` → `FortuneCriticalResolver.Resolve(spin, fortuneState.Rules, fortuneRandom)` → `TemporarySlotCombatController.CreateSpinDisplay(spin, fortune, 빈 룬 줄)` → `dock.PlaySpinAsync(display, reduceMotion, token)`. 회전 중에는 `SetSpinning(true)`로 1.2의 제한을 적용하고, 끝나면(중단 포함) 해제한 뒤 1.2 형식의 결과 문구를 표시한다. `OverflowException`은 잡아서 결과 문구에 표시하고 재생하지 않는다. 결과는 룬·전투·Story·기록에 전달하지 않는다.
6. 화면 안/밖: `dock.Show()`/`dock.Hide()`. `dock.ShownChanged`를 구독해 `slotTab.SetShown`을 갱신한다.
7. `기본값으로`: 1.2의 규칙대로 세션 Reel과 Fortune 규칙을 설정 자산 값으로 되돌린다. 설정 자산이 없거나 규칙 생성에 실패하면 해당 부분은 바꾸지 않고 사유를 표시한다.
8. Editor 전용 `ConfigureForEditor(...)`와 난수 주입 메서드를 두어 Edit Mode 테스트에서 세션·독·Tab·난수를 바꿔 끼울 수 있게 한다. 기존 컨트롤러의 `ConfigureForEditor` 관례를 따른다.

컨트롤러는 Scene 검색(`FindObjectOfType` 등)으로 참조를 찾지 않는다. 모든 Scene 참조는 직렬화 필드로 저장한다.

### 5.7 적용 방식

- 적용 대상: `EncounterDebugPanel.prefab`(재구성), `SlotReelDock.prefab`(전환 컴포넌트 추가·연결), `Candidate_MainScene.unity`(`SlotReelDockClip` 추가와 독 이동, 컨트롤러 추가·참조 연결).
- Unity Editor 자동화를 쓸 수 있으면 직접 적용한다. 쓸 수 없거나 반복 적용이 필요하면 임시 메뉴 `Tools > TxT RPG > Application > Temporary > Apply Candidate Debug Panel Tabs`(`Assets/TxTRPG/Application/Editor/TemporaryDebugPanelTabsApply.cs`)를 만든다. 임시 메뉴는 Play Mode·컴파일 중·Prefab Stage이거나 열린 Scene에 미저장 변경이 있으면 아무것도 바꾸지 않고, 이미 목표 상태이면 파일을 쓰지 않고 `kept` 로그만 남겨 재실행해도 안전해야 한다. 로그 접두사는 `DEBUG_PANEL_TABS_APPLY`이다.
- 적용 전 Editor 연결, 컴파일 상태, 미저장 변경을 확인하고, 사용자 편집과 Prefab Override를 보존한다.
- 이전 작업의 임시 메뉴(`Apply Candidate Encounter Toggles`, `Apply Slot Reel Dock To Candidate Main Scene` 등)는 다시 실행하지 않는다.

### 5.8 Play Mode 확인용 임시 메뉴

`Tools > TxT RPG > Application > Temporary > Slot Debug Tab Play Check`(`Assets/TxTRPG/Application/Editor/TemporarySlotDebugTabPlayCheck.cs`)를 만든다. AppScene 경유 Play에서 `Candidate_MainScene`이 표시된 상태로 실행하며, 자산을 저장하지 않는다. 결과는 `SLOT_DEBUG_TAB_CHECK` 로그로 남긴다. 확인 항목:

1. 시작 시 `이벤트 판정` Tab 선택, 이벤트 판정 페이지만 활성, 기존 슬라이더 값과 문구 유지.
2. `슬롯` Tab 전환 후 Reel 행 수 = 세션 Reel 수, 각 행 값 = 세션 규칙의 확률.
3. `+`를 최대까지 눌렀을 때 세션 Reel 수 10, 독 Reel 수 10, `+` 비활성. `−`를 1까지 눌렀을 때 `−` 비활성. 추가된 Reel의 Strip = 추가 시점 마지막 Reel의 Strip.
4. Reel 1의 확률 100%, 합계 100%로 회전하면 Reel 1과 합계의 배수가 1보다 크고 독 합계 카드에 배수와 최종 합계가 표시된다. 다른 Reel을 0%로 두면 배수가 1이다.
5. `화면 밖으로`: 약 0.3초(허용 오차 ±1프레임) 뒤 `PresentationRoot`가 `HiddenOffset`에 있고 alpha 0. 전환 도중 위치가 시작 위치보다 위로 넘어가는 프레임(InOutBack)이 있다. `화면 안으로`: 약 0.3초 뒤 표시 위치, alpha 1. 전환 도중 방향을 바꾸면 현재 위치에서 새 전환이 시작된다.
6. Reduce Motion을 켜면 즉시 이동한다. 확인이 끝나면 원래 값으로 되돌린다.
7. `기본값으로`가 세션 Reel과 규칙을 설정 자산 값으로 되돌린다. 확인이 끝나면 Play 시작 시 상태(Reel, 규칙, 독 표시 상태, 선택 Tab)로 되돌린다.

### 5.9 Edit Mode 테스트

| 테스트 클래스 | 어셈블리 | 확인 내용 |
| --- | --- | --- |
| `TxTRPG.UI.Tests.DebugPanelTabGroupTests` | `TxTRPG.UI.Tests.Editor` | 시작 Tab, 선택 시 페이지 전환과 `SelectionChanged` 1회, 같은 Tab·범위 밖 선택 무시, 선택 표시 |
| `TxTRPG.UI.Tests.SlotDebugTabViewTests` | `TxTRPG.UI.Tests.Editor` | 무알림 `SetReels`·행 재사용, 슬라이더 이벤트와 확률 변환, `−`/`+` 활성 규칙, 회전 중 제한, 이동 버튼 활성 규칙, 결과 문구 표시 |
| `TxTRPG.UI.Tests.SlotReelDockEasedTransitionTests` | `TxTRPG.UI.Tests.Editor` | 방향별 시간 선택, 마지막 위치가 정확히 `to`, InOutBack 중간값이 범위를 벗어남, `reduceMotion`·시간 0의 즉시 이동, 취소 시 위치 유지. 시간은 테스트용 시간 공급으로 주입한다. |
| `TxTRPG.UI.Tests.SlotReelDockTests` (테스트 추가) | `TxTRPG.UI.Tests.Editor` | 가짜 전환 구현으로 `showing` 값 전달과, 전환 중 다시 상태를 바꿀 때의 취소 확인 |
| `TxTRPG.Application.Tests.SlotReelDockDebugControllerTests` | `TxTRPG.Application.Tests.Editor` | 세션 연결과 독 표시, Reel 추가 ID·Strip 복사·제거 시 규칙 제거, 범위 제한, 확률 변경이 세션에 반영, 정해진 난수로 회전 결과와 결과 문구, 오버플로 처리, `기본값으로`, 세션 없음 처리 |

- 회귀 확인: `TxTRPG.UI.Tests.EncounterDebugPanelTests`, `TxTRPG.Application.Tests.EncounterRollControllerTests`, `TxTRPG.Application.Tests.SlotFortuneCriticalCombatTests`, `TxTRPG.UI.Tests.SlotMachinePanelTests`.
- 새 테스트는 저장된 Prefab·설정 자산을 읽기만 하고 `AssetWriting`이 아니어야 한다. 실행은 위 클래스로 필터링하고 전체 Edit Mode Suite는 실행하지 않는다.

## 6. 호환성과 실패 동작

| 상황 | 동작 |
| --- | --- |
| Release Player | `DevelopmentBuildOnly`가 패널 전체를 비활성화한다. 컨트롤러는 독을 세션 Reel에 연결하는 일만 하고 Slot Tab 요청은 발생하지 않는다. |
| 세션 없음(AppScene을 거치지 않은 Play) | 5.6의 1번. 예외 없음 |
| `dock` 참조 누락 | 오류 1회, 컨트롤러만 동작 중지. 이벤트 판정 Tab과 Scene의 다른 기능은 영향 없음 |
| `slotTab` 참조 누락 | 경고 1회. 독 연결은 계속 동작 |
| Fortune 상태가 `Disabled`(설정 누락·오류) | 확률 변경은 `Try...` 결과대로 처리하고 실패 사유를 표시한다. 회전은 배수 1로 표시된다. |
| 전환 컴포넌트 누락 | 기존 동작대로 즉시 이동한다(독이 한 번 경고). |
| 저장 데이터 | 저장 형식을 바꾸지 않는다. 디버그 변경은 저장하지 않는다. |
| 직렬화 호환 | 기존 `EncounterDebugPanel` 필드와 Scene 참조를 유지한다. `SlotEasingKind` 값 번호를 바꾸지 않는다. |

## 7. 검증 기준

### 7.1 Edit Mode 테스트

5.9의 새 테스트와 회귀 테스트가 모두 통과한다. 실행 전후 `git status`를 비교하여 테스트가 변경 범위 밖의 파일을 바꾸지 않았는지 확인한다.

### 7.2 Editor 확인

1. 아무 Scene에서 Play를 시작해 AppScene을 거쳐 `Candidate_MainScene`이 로드되면, 오른쪽 위에 `DEBUG` 패널이 보이고 `이벤트 판정` Tab이 선택되어 있다. 이벤트 판정 Tab의 슬라이더 5개, `이벤트 발생`, `기본값으로`, 결과 문구가 이전과 같이 동작한다([workflows.md](../development/workflows.md)의 기존 확인 절차 3~5).
2. `슬롯` Tab에서 5.8의 항목을 사람이 직접 확인하고, `Slot Debug Tab Play Check`를 실행해 `SLOT_DEBUG_TAB_CHECK: PASS all steps`를 확인한다.
3. 마우스, 키보드(Navigate·Submit), Gamepad, 터치(Device Simulator)로 Tab 전환, 슬라이더 조작, 버튼 실행이 가능하다. 페이지가 바뀌어도 숨은 페이지의 컨트롤로 포커스가 가지 않는다.
4. `resolution-and-screen-frame.md`의 검증 화면 크기(16:9 데스크톱, 울트라와이드, 태블릿, 폰 세로, 노치 기기 1종 포함)에서 패널이 프레임 안에 있고, 10개 Reel에서 Slot 페이지가 세로 스크롤된다. 폰 크기에서 버튼과 슬라이더의 화면상 크기를 문서의 변환식으로 계산해 보고한다.
5. 세로로 긴 화면에서 `화면 밖으로`/`화면 안으로` 중 독이 프레임 아래 레터박스 위에 그려지지 않는다. 16:9 화면에서 독의 표시 위치·크기가 작업 전과 같다.
6. Inspector에서 `SlotReelDock.prefab`(또는 Scene 인스턴스)의 `showDuration`/`hideDuration`을 바꾸면 이동 시간이 바뀐다.

### 7.3 직렬화 변경 검토

Editor 적용과 테스트 실행 뒤 `serialization-diff-reviewer`로 `.prefab`, `.unity`, `.asset`, `.meta` 변경을 검토한다. 의도한 변경(5.3, 5.4의 3번, 5.5)만 남고, TMP Fallback 폰트 Atlas 증가 같은 부수 변경은 작업 전부터 수정되어 있던 파일이 아닌 경우에만 되돌린다.

## 8. 문서 갱신

- `DOCS/architecture/encounter-roll.md`: 디버그 패널의 Tab 구조, 새 Prefab 계층, 제목 변경, Scene 배치 트리(`SlotReelDockClip`, `SlotReelDockDebugController`).
- `DOCS/architecture/slot-machine-and-runes.md`: `ISlotReelDockTransition` 계약 변경, `SlotReelDockEasedTransition` 필드와 동작, 클리핑 컨테이너, `SlotReelDockDebugController`의 책임과 실패 동작, Slot Tab 표시 데이터. "실제 전투·탐험 이벤트 연결은 아직 없습니다" 문구를 현재 상태에 맞게 고친다.
- `DOCS/architecture/project-structure.md`: `UI/Runtime/DebugPanels/`, 새 타입, Scene 구성 설명의 `OverlayLayer` 내용.
- `DOCS/development/workflows.md`: 새 임시 메뉴, 확인 절차, 테스트 클래스, 기존 확인 절차의 패널 제목(`DEBUG · 이벤트 판정` → `DEBUG`)과 독 경로 변경.
- `DOCS/instructions/README.md`: 이 문서 항목의 완료 상태와 날짜.

## 9. 최종 보고

다음 항목을 구분해 보고한다.

1. 구현 완료 내용(타입, Prefab, Scene, 임시 메뉴, 테스트, 문서).
2. 이미 수행한 적용과 저장된 파일 목록.
3. 사용자가 수행해야 할 적용 절차가 남아 있으면 `사용자가 수행할 적용 절차` 절에 복사 가능한 `text` 블록으로 정리한다. 남은 절차가 없으면 그렇다고 명시한다.
4. 수행한 검증(테스트 클래스와 결과, Play Mode 확인, 확인한 화면 크기와 입력 방식)과 수행하지 못한 검증(실제 기기, Release·Development Player 빌드 등).
5. 1.2의 기본값에서 벗어난 부분과 그 근거, 수정하지 않고 남긴 경로 문자열 사용처.
