# 슬롯 합계의 수치 단독 표시와 합계 카드 중심 정렬

## 1. 목표

슬롯 합계를 표시하는 두 화면에서 `합계` 라벨을 없애고 합계 수치만 표시한다. 또한 `Candidate_MainScene` 하단 슬롯 Reel 독의 합계 카드가 Reel 기계(슬롯 머신 영역) 위쪽 경계선에 중심을 두도록 배치한다.

| 화면 | 현재 | 변경 후 |
| --- | --- | --- |
| `Candidate_MainScene`의 `SlotReelDock` 합계 카드 | 카드 위 35%에 `합계`, 아래 65%에 수치 | 수치만 카드 글자 영역 전체의 가로·세로 가운데에 표시 |
| `TMP_MainScene`의 `SlotMachinePanel` 합계 상자(`TotalBox`) | 왼쪽 절반에 `합계`, 오른쪽 절반에 수치 | 수치만 합계 상자 글자 영역 전체의 가로·세로 가운데에 표시 |
| 합계 카드 위치 | 카드 높이의 70%가 Reel 기계 위쪽 가장자리 위로 나옴(`overhangRatio` 0.7) | 카드 높이의 50%가 위로 나와 카드 중심이 위쪽 가장자리에 놓임(`overhangRatio` 0.5) |

## 2. 사용자가 확정한 선택

| 항목 | 결정 |
| --- | --- |
| 라벨 제거 범위 | 두 화면 모두. `SlotReelDock` 합계 카드와 `SlotMachinePanel` 합계 상자에서 함께 없앤다. |
| 수치 배치 | 라벨 자리를 남겨 두지 않는다. 수치가 카드(또는 상자) 글자 영역 전체를 차지하고, 글자는 그 영역의 가로(Horizontal)·세로(Vertical) 가운데에 놓인다. 배수(`×N`)는 기존처럼 수치 옆이나 위에 놓고, 공간이 부족하면 배수 글자 크기를 줄인다. |
| 카드 위치 방식 | `overhangRatio` 설정을 유지하고 `SlotReelDock.prefab`의 값을 0.7에서 0.5로 바꾼다. 이후에도 Inspector에서 조절할 수 있다. |

## 3. 선행 조건

- [슬롯머신과 룬](../architecture/slot-machine-and-runes.md)의 `SlotReelDock`, 합계 카드 `SlotTotalCardView`, `SlotMachinePanel` 요약 영역 절을 읽는다.
- [해상도와 화면 프레임](../development/resolution-and-screen-frame.md)과 [시각 효과 준비](../development/visual-effect-readiness.md)를 읽는다. 카드는 화면 프레임 안의 UI이므로 이 문서들의 검증 크기와 규칙을 따른다.
- 관련 이전 지침: [candidate-main-scene-slot-reel-dock.md](candidate-main-scene-slot-reel-dock.md)(합계 카드 배치), [slot-progressive-total-display.md](slot-progressive-total-display.md)(회전 중 누적 합계와 배수 표시).
- 작업 시작 전 `git status`를 기록한다. 작성 시점(2026-10-25)에 `SlotReelDock.prefab`, `SlotMachinePanel.prefab`, `SlotReelDock.cs`, `SlotMachinePanel.cs`, 관련 테스트에 커밋되지 않은 사용자 변경이 있다. 이 변경을 되돌리거나 덮어쓰지 않고, 필요한 부분만 수정한다.

## 4. 작업 범위

### 포함

- 두 화면의 합계 표시에서 라벨을 없애고 수치를 영역 전체 가운데에 배치한다.
- `SlotReelDock.prefab`의 합계 카드 `overhangRatio`를 0.5로 바꾼다.
- 관련 Edit Mode 테스트, 임시 검증 메뉴의 기대값, 문서를 갱신한다.

### 제외

- Story 기록과 결과 문구에 쓰는 `합계` 문구는 바꾸지 않는다. 예: Fortune 요약 `[행운 치명] 2번 Reel ×3, 합계 ×2`(`SlotCombatStoryFormatter.FortuneSummary`), 디버그 결과 `3, 5×2, 2 → 합계 ×3 = 45`(`SlotReelDockDebugController.FormatResult`), 탐험 이벤트 대상 이름(`ExplorationEventEffects`), 디버그 패널의 `합계 Critical` 슬라이더 라벨, Fortune 설정 Editor의 `합계 규칙`.
- 합계 값 계산, 누적 표시 순서, 배수 판정은 바꾸지 않는다.
- 카드 모양·크기(`heightRatio`, `aspectRatio` 등)와 스킨, 룬 표는 바꾸지 않는다.
- 접근성 대체 텍스트(스크린 리더용 `합계` 의미 전달)는 현재 구조에 없으므로 이번 작업에서 추가하지 않는다. 필요하면 후속 작업으로 보고한다.

## 5. 관련 경로와 의존 관계

| 경로 | 역할 |
| --- | --- |
| `Assets/TxTRPG/Application/Runtime/SlotMachine/TemporarySlotCombatRecords.cs` | `SlotCombatStoryFormatter.TotalLabel`(`"합계"`) 상수. 표시 데이터에 라벨을 공급한다. |
| `Assets/TxTRPG/Application/Runtime/SlotMachine/TemporarySlotCombatController.cs` | `CreateSummary`가 `SlotSummaryDisplay`에 `TotalLabel`을 넣는다. 전투·탐험 이벤트·`SlotReelDockDebugController`가 이 경로를 쓴다. |
| `Assets/TxTRPG/UI/Runtime/SlotMachine/SlotMachineDisplays.cs` | `SlotSummaryDisplay.TotalLabel`. 표시 계약이다. |
| `Assets/TxTRPG/UI/Runtime/SlotMachine/SlotTotalCardView.cs` | 합계 카드. `SetTotal(label, value, multiplier)`, `overhangRatio`, `ContentRoot` 계산. |
| `Assets/TxTRPG/UI/Runtime/SlotMachine/SlotReelDock.cs` | `SetSummary` → `SlotTotalCardView.SetTotal`. `TotalLabelText` 노출. |
| `Assets/TxTRPG/UI/Runtime/SlotMachine/SlotMachinePanel.cs` | `SetSummary`가 `totalLabelText`, `totalValueText`를 설정한다. `totalLabelSkin` 스킨 대상. |
| `Assets/TxTRPG/UI/Runtime/SlotMachine/SlotTotalMultiplierLayout.cs` | 배수를 수치 글리프 옆이나 위에 배치한다. 수치 영역이 바뀌므로 동작을 확인한다. |
| `Assets/TxTRPG/UI/Runtime/SlotMachine/SlotHeightScaledText.cs` | 수치 글자 크기를 영역 높이로 정하고 `fitWidth`로 너비를 맞춘다. |
| `Assets/TxTRPG/UI/Prefabs/SlotMachine/SlotReelDock.prefab` | `PresentationRoot/TotalCard/ContentRoot/TotalLabel`(앵커 y 0.65~1), `TotalValue`(앵커 y 0~0.65), `SlotTotalCardView.overhangRatio` 0.7, `heightRatio` 0.4. |
| `Assets/TxTRPG/UI/Prefabs/SlotMachine/SlotMachinePanel.prefab` | `Content/SummaryArea/TotalBox/TotalLabel`(앵커 x 0.05~0.5), `TotalValue`(앵커 x 0.5~0.95). `TMP_MainScene`에서 쓴다. |
| `Assets/TxTRPG/Application/Editor/TemporarySlotReelDockApply.cs`, `TemporarySlotMachinePanelVisualApply.cs` | 이미 적용된 임시 생성·적용 메뉴. 다시 실행하면 이전 배치를 만들 수 있으므로 기본값을 새 배치와 맞추거나, 맞추지 않는다면 문서에 이유를 남긴다. |
| `Assets/TxTRPG/Application/Editor/TemporarySlotMachinePlayValidation.cs`, `TemporarySlotFortuneCriticalPlayValidation.cs`, `TemporarySlotProgressiveTotalPlayProbe.cs`, `SlotReelDockPreviewMenu.cs` | `TotalLabel` 텍스트·이름을 검사하거나 출력한다. 기대값을 갱신한다. |

`Candidate_MainScene.unity`와 `TMP_MainScene.unity`에는 작성 시점에 `overhangRatio`나 `TotalLabel`의 Scene 재정의가 없다. 작업 전에 다시 확인하고, 재정의가 있으면 보고한 뒤 Prefab 값을 따르도록 정리할지 사용자에게 묻는다.

## 6. 구현 요구 사항

### 6.1 라벨 데이터

1. Application이 공급하는 합계 라벨을 빈 문자열로 바꾼다. `SlotCombatStoryFormatter.TotalLabel`을 `string.Empty`로 바꾸거나, `CreateSummary`가 빈 라벨을 넘기도록 바꾼다. 다른 곳에서 이 상수를 Story 문구에 쓰지 않는지 호출처를 추적해 확인한다. 4장의 제외 문구는 각자 문자열 리터럴을 쓰므로 영향을 받지 않아야 한다.
2. `SlotSummaryDisplay.TotalLabel`과 `ISlotMachinePresenter` 계약은 유지한다. 이후 다른 화면에서 라벨이 필요해질 수 있으므로 필드를 제거하지 않는다.

### 6.2 라벨이 비었을 때의 표시

1. `SlotTotalCardView.SetTotal`과 `SlotMachinePanel.SetSummary`는 라벨이 빈 문자열이면 `TotalLabel` GameObject를 비활성화하고, 값이 있으면 활성화한다. 같은 상태면 아무것도 하지 않는다.
2. 라벨 비활성화가 레이아웃을 다시 계산하게 만들지 않도록, 수치 영역은 라벨 유무와 관계없이 Prefab에 저장된 전체 영역을 쓴다(6.3).
3. `SlotMachinePanel`의 `totalLabelSkin` 적용과 기본값 기록은 GameObject가 비활성이어도 오류 없이 동작해야 한다.

### 6.3 수치 영역과 정렬

1. `SlotReelDock.prefab`: `TotalCard/ContentRoot/TotalValue`의 앵커를 (0, 0)~(1, 1), 여백 0으로 바꿔 `ContentRoot` 전체를 채운다. `ContentRoot`는 카드 모양에 맞춘 안쪽 글자 영역이므로 모양별 안쪽 여백은 그대로 유지된다.
2. `SlotMachinePanel.prefab`: `TotalBox/TotalValue`의 앵커를 현재 라벨·수치 영역을 합친 범위(x 0.05~0.95, y 0~1)로 바꾼다. `TotalBox/Background`는 바꾸지 않는다.
3. 두 `TotalValue`의 TMP 정렬은 가로·세로 모두 가운데(`TextAlignmentOptions.Center`)로 둔다. 글자 크기 조정이나 배수 배치가 정렬을 바꾸지 않게 한다.
4. `SlotHeightScaledText`의 비율·최소·최대 값과 `fitWidth` 표본(`999`)은 우선 유지한다. 영역이 커져 글자가 최대값에 닿거나 너무 커 보이면 값을 바꾸기 전에 측정값과 함께 보고한다.
5. 배수(`TotalMultiplier`)는 기존 `SlotTotalMultiplierLayout` 규칙(수치 글리프 옆, 공간이 없으면 위, 그래도 없으면 글자 크기 축소)을 따른다. 수치가 영역 세로 가운데에 놓이면 위쪽 여유가 줄어들 수 있으므로, 세 자리 합계와 `×30`이 함께 있을 때 배수가 수치를 가리지 않고 영역 안에 머무는지 확인한다. 가려지거나 잘리면 원인과 측정값을 보고하고, 배치 규칙을 바꿔야 하면 사용자에게 방향을 묻는다.

### 6.4 합계 카드 위치

1. `SlotReelDock.prefab`의 `TotalCard`에 있는 `SlotTotalCardView.overhangRatio`를 0.5로 바꾼다. 코드 기본값(0.5)과 계산식은 바꾸지 않는다.
2. `SlotReelDock`은 카드 돌출 높이만큼 배치 루트 위쪽을 확보한다. 돌출 높이가 줄어 배치 루트 높이와 숨김 위치(`HiddenOffset`)가 달라지므로, 숨김 상태에서 카드가 화면 프레임 안에 남지 않고 `SlotReelDockClip`이 잘라 내는지 확인한다.
3. 기본값(`heightRatio` 0.4, `overhangRatio` 0.5)에서 카드 아래쪽 가장자리가 Payline 칸을 가리지 않는지 확인한다. 가린다면 측정값과 함께 보고하고, `heightRatio` 조정 여부는 사용자에게 묻는다.
4. `Frame`은 `ReelMachine`을 채우므로(앵커 0~1, 여백 0) Reel 기계 위쪽 가장자리가 보이는 틀의 위쪽 경계선과 같다. 스킨이 틀 바깥으로 그리는 테두리를 쓰면 보이는 경계선이 달라질 수 있으므로, 현재 선택된 스킨에서 카드 중심과 보이는 경계선이 일치하는지 화면으로 확인한다.

### 6.5 임시 메뉴

1. `TemporarySlotReelDockApply.CreateCard`와 `TemporarySlotMachinePanelVisualApply`의 라벨·수치 생성 값을 새 배치와 맞춘다. 두 메뉴는 기존 Prefab을 다시 만들지 않으므로 이번 Prefab 적용에는 쓰지 않는다.
2. Prefab 적용은 Unity Editor 자동화로 직접 수행하거나, 필요한 경우 다시 실행해도 안전한 임시 적용 메뉴를 둔다. 임시 메뉴를 두면 목적·임시 여부·재실행 안전성을 주석과 `DOCS/development/workflows.md`에 적는다. Prefab 전체 재생성은 하지 않고 필요한 필드만 바꾼다.

## 7. 호환성과 실패 동작

- 저장 데이터 형식은 바뀌지 않는다. 직렬화 필드를 추가·삭제하지 않는다.
- 라벨 참조(`totalLabelText`)가 비어 있어도 오류 없이 수치만 표시한다.
- Application이 나중에 비어 있지 않은 라벨을 넘기면 라벨 GameObject가 다시 활성화된다. 이때 수치 영역과 겹칠 수 있으므로, 그 경우의 배치는 이번 범위가 아니며 문서에 한계로 적는다.
- 사용자의 커밋되지 않은 Prefab·코드 변경을 유지한다.

## 8. 검증

### Edit Mode 테스트

필터를 지정해 관련 클래스만 실행한다. `AssetWriting` 카테고리는 제외한다(`!AssetWriting`).

| 테스트 클래스 | 확인 내용 |
| --- | --- |
| `SlotReelDockTests` | 라벨이 빈 문자열이면 `TotalLabel`이 비활성이고, 수치가 `ContentRoot` 전체 영역의 가운데에 있으며, 카드 중심 y가 Reel 기계 위쪽 가장자리와 같다(`overhangRatio` 0.5). 기존 `TotalLabelText` `"합계"` 기대값을 갱신한다. |
| `SlotMachinePanelTests` | 빈 라벨일 때 `TotalLabel` 비활성, `TotalValue`가 합계 상자 글자 영역 전체와 가운데 정렬. |
| `SlotMachineCombatTests` | `panel.TotalLabelText`, `summary.TotalLabel` 기대값을 빈 문자열로 갱신. |
| `SlotFortuneCriticalCombatTests`, `SlotReelDockDebugControllerTests` | Story·결과 문구의 `합계`가 그대로인지 확인(회귀 방지). |
| `SlotMachinePanelSkinTests` | 라벨 비활성 상태에서도 스킨 적용·기본값 기록이 동작한다. |

### Play Mode 확인

1. `Candidate_MainScene`(AppScene 경유)에서 디버그 패널 `슬롯` Tab의 `화면 안으로`, `회전`을 실행한다. 카드에 `합계` 없이 `0`이 보이고, 회전 중 누적 값과 결과(합계 Critical이면 `×N`)가 카드 가운데에 표시되며, 카드 중심이 Reel 기계 위쪽 경계선에 놓이는지 확인한다.
2. `TMP_MainScene`에서 `Tools/TxT RPG/Application/Temporary/Validate Slot Machine Combat (Play Mode)`, `Tools/TxT RPG/Application/Temporary/Validate Slot Fortune Critical (Play Mode)`를 실행하고, 합계 상자에 수치만 가운데 표시되는지 확인한다.
3. [해상도와 화면 프레임 문서](../development/resolution-and-screen-frame.md)의 검증 크기 중 최소 1920×1080, 21:9, 1024×768, 390×844에서 세 자리 합계와 `×30`이 카드·합계 상자 안에 들어가는지, 수치 글자 크기를 Canvas 단위로 기록한다. 휴대폰 크기에서 너무 작아지는 글자가 있으면 보고한다.
4. 숨김 상태에서 카드가 화면 프레임 아래로 잘려 보이지 않는지 확인한다.

### 직렬화 변경 검토

Prefab을 적용하거나 테스트를 실행한 뒤 `serialization-diff-reviewer`로 의도한 변경(두 Prefab의 `TotalValue` 앵커·정렬, `TotalLabel` 활성 상태, `overhangRatio`)과 부수 변경을 구분한다. 작업 전에 수정되지 않았던 파일이 테스트로 바뀌었다면 그 파일만 되돌린다.

## 9. 문서 갱신

- `DOCS/architecture/slot-machine-and-runes.md`: `SlotReelDock` 계층의 `TotalLabel`·`TotalValue` 설명, 합계 카드 크기·위치 표의 `overhangRatio`(Prefab 값 0.5), Play Mode 측정 표의 `카드 글자(라벨/값)` 열, 표시 데이터 절(빈 라벨이면 라벨 비활성), `SlotMachinePanel` 요약 영역 설명.
- `DOCS/development/workflows.md`: 합계 카드 확인 절차의 기대 화면, 임시 메뉴를 추가했다면 그 경로와 절차.
- `DOCS/instructions/README.md`: 이 문서 항목에 완료 상태와 검증 범위를 적는다.

## 10. 최종 보고

다음을 구분해 보고한다.

1. 구현 완료 항목(코드, 테스트, 문서)
2. 이미 적용한 Prefab·Scene 변경과 적용 방법
3. 사용자가 아직 수행해야 하는 적용 절차(있으면 `사용자가 수행할 적용 절차` 절에 정확한 메뉴 경로로)
4. 실행한 테스트 필터와 결과, Play Mode 확인 범위와 측정값
5. 미검증 항목(Device Simulator, 실제 기기, Player 빌드)과 보고할 문제(글자 크기, 배수 배치, Payline 가림)
