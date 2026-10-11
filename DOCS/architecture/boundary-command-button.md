# 경계선 명령 버튼

두 패널의 경계선 위에 겹쳐 놓이는 명령 버튼입니다. 현재 TMP_MainScene의 전투 "행동" 버튼(`MiddlePanel/ContentLayer/CombatActionBoundary`)이 유일한 사용처이며, `EnemyDisplayPanelDemo`(위)와 `SlotMachinePanel`(아래)의 경계선 가운데에 놓입니다. 주사위 보류 전에는 아래 패널이 `OwnedDicePanel`이었습니다([슬롯머신과 룬](slot-machine-and-runes.md)). 버튼이 덮는 `SlotMachinePanel` 위쪽 가운데는 `Content`의 위쪽 여백 44px이며, 좌우·상하 배치 모두에서 Reel 창, 합계 값, 룬 표 행을 가리지 않음을 1920×1080, 21:9, 1024×768, 390×844에서 Play Mode 검증 메뉴로 확인했습니다. 전투 표시 조건과 포커스 규칙은 [임시 적 전투](temporary-combat.md#행동-버튼), 배치 계산은 [FlexibleLayoutPanel 설계](flexible-layout-panel.md#레이아웃에-참여하지-않는-경계선-오버레이)를 봅니다.

## 구성 요소

| 구성 요소 | 어셈블리·경로 | 책임 |
| --- | --- | --- |
| `PanelBoundaryCalculator` | `TxTRPG.UI`, `UI/Runtime/PanelBoundaryPlacement.cs` | 두 Rect 사이 경계의 중심과 방향, 컨테이너 안 보정을 계산하는 순수 계산입니다. |
| `PanelBoundaryPlacement` | 같은 파일 | 두 패널 `RectTransform`에서 경계를 계산해 대상 버튼을 구동하고 `BoundaryChanged`를 알립니다. |
| `BoundaryCommandButton` | `TxTRPG.UI.Windows`, `UI/Runtime/Windows/BoundaryCommandButton.cs` | 소유자가 정한 가용 여부와 경계 유무로 표시를 정하고, `IGameMenuCommandHandler.CanExecute`를 `interactable`에 반영하며, 클릭·Submit을 `TryExecute`로 전달합니다. 숨김 시 포커스를 정리합니다. |
| `ShapedButtonView` | `TxTRPG.UI.Shapes`, `UI/Runtime/Shapes/` | 배경·테두리 모양(절차적 도형 또는 이미지), 입력 영역 모양, 비활성 표시, 효과 Material 슬롯을 관리합니다. |
| `UiShapeGeometry`, `UiShape`, `UiShapeDrawMode` | 같은 폴더 | 공용 도형 경계·Mesh·점 포함 판정입니다. 탐험 카드 `ExplorationCardShapeGraphic`도 사용합니다. |
| `UiShapeGraphic` | 같은 폴더 | 텍스처 없는 도형 Graphic입니다(채우기 또는 내부 테두리). |
| `UiShapeHitArea` | 같은 폴더 | 그리지 않는 Raycast 대상입니다. 도형 경계 판정, 또는 선택적인 이미지 알파 판정을 합니다. |
| `GameMenuButtonView` | `TxTRPG.UI.Windows` (재사용) | 아이콘 표시와 Sprite 누락·아이콘 숨김 시 라벨 대체 표시를 메뉴 버튼과 같은 규칙으로 처리합니다. |
| `TemporarySlotCombatController` | `TxTRPG.Application` | 현재 명령 처리기입니다. `actionButton` 직렬화 참조로 버튼을 받고 가용 여부를 전달합니다. 보류된 `TemporaryDiceRollMenuController`도 같은 방식을 씁니다. UI 어셈블리는 Application을 참조하지 않습니다. |

## 계층

```text
CombatActionBoundary            Stretch, LayoutElement.ignoreLayout, PanelBoundaryPlacement, BoundaryCommandButton
└── ActionButton                72x72 기본, Button(Explicit 탐색), GameMenuButtonView, ShapedButtonView (기본 비활성)
    ├── HitArea                 UiShapeHitArea — 유일한 Raycast 대상, VisualRoot 밖
    └── VisualRoot              CanvasGroup(raycast 차단 없음) — 효과가 변형하는 루트
        ├── Background          UiShapeGraphic (절차적 모드)
        ├── BackgroundImage     Image (이미지 모드, 9-slice 자동)
        ├── Icon                Image — action 기본 아이콘
        ├── Label               TMP "행동" — 아이콘 대체 표시
        ├── Border              UiShapeGraphic 내부 테두리 (절차적 모드)
        ├── BorderImage         Image (이미지 모드)
        └── EffectOverlay       전경 효과용 Graphic을 넣는 자리
```

`CombatActionBoundary`는 `Assets/TxTRPG/UI/Prefabs/BoundaryCommandButton.prefab`의 인스턴스입니다. Scene 인스턴스는 두 패널 참조(`firstPanel` = `EnemyDisplayPanelDemo`, `secondPanel` = `SlotMachinePanel`), 명령 핸들러(`TemporarySlotCombatController`), 명령 ID `temporary-slot.spin`, 라벨 "행동", action 아이콘, 탐색 대상만 Override로 가집니다. `Apply Slot Machine Combat`이 `secondPanel`·핸들러·명령 ID를 주사위에서 슬롯머신으로 바꿨습니다. `TemporaryMenuIconUtility.ApplyToBoundaryButton`은 두 명령 ID 모두에 action 아이콘을 적용합니다. 재사용을 고려해 Prefab으로 두었고, Prefab은 없을 때만 `BoundaryCommandButtonPrefabUtility.EnsurePrefab`이 만들며 이후에는 개발자 소유 자산입니다(일괄 Rebuild 대상 아님, `ProjectPrefabRebuildExclusionProvider`에 `ui.boundary-command-button`으로 제외 사유 기록).

## 기본값과 선택 이유

| 항목 | 기본값 | 이유 |
| --- | --- | --- |
| 크기 | 72×72 | 원 모양은 짧은 변을 지름으로 쓰므로 메뉴 버튼(80×50)의 크기로는 지름 50이 됩니다. 터치 대상 48 이상과 아이콘·라벨 "행동"이 잘리지 않는 크기로 72를 정했습니다. Root의 `sizeDelta`에서 조정합니다. |
| 모양 | 원(절차적) | 지침의 기본값입니다. |
| 입력 영역 | 절차적 모드는 보이는 도형을 따름, 이미지 모드는 사각형 | 원 바깥 모서리를 눌러 실행되지 않게 했습니다. 이미지 모드는 알파 판정이 Read/Write 텍스처를 요구하므로 사각형을 기본으로 하고 `imageAlphaHitThreshold`로 선택합니다. 입력 영역은 `HitArea`가 맡으므로 효과가 `VisualRoot`를 움직여도 바뀌지 않습니다. |
| 비활성 표시 | Button Color Tint + `VisualRoot` 불투명도 0.45 | 색만으로 상태를 전달하지 않도록 투명도를 함께 낮춥니다. |
| 위치 계층 | `ContentLayer`의 마지막 형제 | `ExplorationNodePanel`과 같은 오버레이 방식이며 두 패널 위에 그려집니다. `ForegroundLayer`는 테두리 전용이라 사용하지 않았습니다. |
| 가리는 정보 | 조정 없음(겹침 허용) | 사용자 확정에 따라 버튼이 주사위 화면(`DiceViewport`) 위쪽 가운데와 `OwnedDicePanel/Title` 일부를 덮는 것을 허용합니다. 주사위 영역에 여백을 추가하지 않습니다. `Validate Combat Action Button (Play Mode)`의 측정에서 `ResultsButton`(오른쪽 위 120×44)과의 겹침은 1920×1080, 울트라와이드, 좁은 데스크톱, 휴대폰 세로·가로에서 없었고 태블릿(768×1024, 주사위 패널 너비 약 328)에서는 있었습니다. 알려진 제약: 주사위가 버튼 아래에 멈추면 결과 면 일부가 가려질 수 있습니다. 결과는 Story와 결과 창에서도 확인할 수 있으므로 주사위 연출과 구도는 바꾸지 않았습니다. 아래 문장은 Story 패널과 경계를 공유하던 이전 배치의 기록입니다. 1920×1080과 휴대폰 세로(390×844) 실행 화면에서 버튼은 적 패널 아래쪽 빈 영역과 Story 패널 위쪽 빈 영역만 덮었습니다. HP 바와 다음 적 행동은 적 패널 위쪽, 적 그림은 가운데, Story 최신 문단은 아래쪽에 있어 가려지지 않았습니다. 휴대폰 가로, 태블릿, 좁은 데스크톱, 울트라와이드는 버튼 위치와 잘림 여부만 수치로 검증했습니다. Story 기록이 패널 위쪽까지 차면 위쪽에 보이는 오래된 줄의 가운데 일부가 가려질 것으로 예상하지만(실행 화면에서는 미확인) 최신 문단은 아래쪽이라 영향이 없습니다. |

## 외형 설정

`ShapedButtonView`의 `Shape Appearance`에서 설정합니다.

- `Visual Mode`: `Procedural Shape`(텍스처 없음) 또는 `Image`(디자이너 Sprite. Sprite에 Border가 있으면 Sliced로 그림).
- `Shape`: Rectangle, RoundedRectangle, Circle, Ellipse, Triangle. `Corner Radius`는 RoundedRectangle과 Triangle에 적용됩니다.
- `Border Thickness`, `Background Color`, `Border Color`, `Background Sprite`, `Border Sprite`.
- `Shape Hit Test`, `Image Alpha Hit Threshold`.

아이콘은 `GameMenuButtonView`의 Icon Sprite·padding·tint를 사용합니다. 새 절차적 도형은 `UiShape`와 `UiShapeGeometry.BuildBoundary` 한 곳에 추가하면 버튼과 Graphic, 입력 판정에 모두 적용됩니다. 코드 변경 없는 새 모양은 이미지 모드로 추가합니다.

## 효과 확장 지점

`AGENTS.md`의 Visual Effect Readiness와 [시각 효과 적용을 전제로 한 표시 요소 설계](../development/visual-effect-readiness.md)를 적용한 구조입니다. 실제 노이즈·글리치·외곽선 셰이더는 아직 없습니다.

- 배치·입력: `CombatActionBoundary`(배치 컨테이너)와 `ActionButton` Root(크기, `PanelBoundaryPlacement`가 구동하는 위치)와 `HitArea`는 효과가 건드리지 않습니다. 흔들림·확대 같은 Transform 효과는 `VisualRoot` 또는 그 자식에 적용합니다.
- Material 슬롯: `ShapedButtonView`의 `Background Material`, `Border Material`, `Icon Material`, `Effect Overlay Material`(EffectOverlay 아래 모든 Graphic)에 셰이더 Material을 지정합니다. 비워 두면 기본 UI Material입니다. 라벨은 TMP 글꼴 Material을 사용하므로 슬롯이 없습니다.
- 런타임 속성 변경: `GetOrCreateEffectInstance(target)`가 뷰가 소유하는 인스턴스 Material을 만들어 적용합니다. 공유 Material 자산은 바꾸지 않습니다. `ResetEffects()`는 인스턴스를 파괴하고 슬롯 Material로 되돌리며, 버튼이 숨겨질 때와 `OnDisable`·`OnDestroy`에서도 호출되므로 다시 표시될 때 이전 효과 상태가 남지 않습니다. URP의 `MaterialPropertyBlock` 대신 uGUI Graphic의 Material 인스턴스 교체를 선택했습니다. 화면에 버튼이 하나뿐이라 SRP Batcher 영향이 작고 uGUI가 PropertyBlock을 지원하지 않기 때문입니다.
- 움직이는 효과 끄기: `SetEffectsAllowed(false)`이면 `Effects Are Animated`가 켜진 동안 효과 Material을 기본 Material로 바꾸고 EffectOverlay의 Graphic을 숨깁니다(뷰가 숨긴 것만 다시 켭니다). `BoundaryCommandButton`은 System 페이지의 `카드 효과 사용`·`움직임 줄이기` 설정(`ExplorationCardPlayerPreferences.AllowsMotion`, `Changed` 이벤트)에 연결되어 있습니다. 품질 단계에서도 같은 메서드를 호출하면 되며, 효과가 꺼져도 버튼 사용과 상태 표시는 그대로입니다.
- 테두리·외곽선·글로우가 차지할 여백: 기본 테두리는 내부 테두리라 크기를 늘리지 않습니다. 바깥으로 그리는 효과는 버튼이 소속 레이아웃 패널(`MiddlePanel`) 영역 안으로 보정되므로 패널 테두리에서 잘리지 않지만, 두께만큼 `Root` 크기에 여유를 두는 것을 권장합니다.

## 탐색 경로

| 출발 | 방향 | 도착 | 비고 |
| --- | --- | --- | --- |
| 행동 버튼 | 오른쪽 | `GameMenuPanel`의 System 버튼 | 이전과 같습니다. |
| 행동 버튼 | 아래 | `OwnedDicePanel/ResultsButton` | 주사위 보류로 대상이 비활성이므로 현재는 이동하지 않습니다. `SlotMachinePanel`에는 선택 가능한 요소가 없으며, 이번 작업에서 탐색 규칙은 바꾸지 않았습니다. |
| 행동 버튼 | 위·왼쪽 | 없음 | 위쪽 적 패널에는 선택 가능한 요소가 없습니다. |
| `ResultsButton` | 위 | 행동 버튼 | Automatic 탐색은 왼쪽 열의 더 가까운 버튼을 골라 Explicit으로 바꿨습니다. `ResultsButton`은 전투 중에만 도달할 수 있고(노드 선택 중에는 [탐색 차단](exploration-history-tree.md)), 전투 중에는 행동 버튼이 표시됩니다. |
| `ResultsButton` | 오른쪽 | System 버튼 | 행동 버튼과 같은 메뉴 왕복 경로입니다. |

System 등 메뉴 버튼은 Automatic 탐색을 유지합니다. `TemporaryDiceMenuBuilder.EnsureBoundaryActionButton`은 탐색이 비어 있거나 이전 기본값(아래 → System)일 때, `ResultsButton`은 Automatic일 때만 위 경로를 설정하고 그 밖의 사용자 Explicit 설정은 보존합니다.

## 실패와 비가용 처리

- 패널 참조 누락, 한쪽 패널 비활성, 크기 0, 겹침: 경계가 없으므로 버튼을 숨깁니다.
- 핸들러 미연결: 버튼을 숨기고 `UnavailableReason`에 기록합니다.
- 실행 불가 상태에서 입력: `TryExecute`를 호출하지 않고 경고 로그를 남긴 뒤 상태를 다시 계산합니다.
- `BoundaryCommandButton`은 런타임 전용이라 Edit Mode에서 `OnEnable`이 실행되지 않습니다. 테스트는 `BindForEditorTests()`를 사용합니다.
