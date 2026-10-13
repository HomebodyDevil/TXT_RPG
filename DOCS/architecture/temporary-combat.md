# 임시 적 전투

## 범위와 상태 소유권

`TMP_MainScene`의 임시 전투는 정식 전투·저장 시스템과 분리된 탐험 노드 처리기입니다. `TemporaryCombatState`는 Unity 객체를 참조하지 않는 순수 C# 상태이며, 전투 노드를 시작할 때 탐험이 소유한 `HealthState`를 직접 사용합니다. 피해와 회복은 다음 노드까지 이어지지만 `PlayerSession`의 운영 체력과 저장 DTO는 변경되지 않습니다.

| 구성 요소 | 책임 |
| --- | --- |
| `TemporaryCombatConfiguration` | 활성 여부, 적 이름·외형 ID·체력 40·공격 5·회복 3을 설정합니다. |
| `TemporaryCombatState` | 양쪽 체력, 다음 적 행동, 종료 상태와 한 턴의 순차 적용 결과를 관리합니다. 입력은 `PlayerCombatAction`(종류·수치·출처 ID) 목록이며, 주사위 결과 오버로드는 이 입력으로 바꿔 위임합니다. |
| `TemporarySlotCombatController` | `TMP_MainScene`의 현재 전투 처리기입니다. 슬롯머신을 한 번 돌리고 룬 줄로 총합을 분배해 결과를 확정합니다. 연출 후 전투를 한 번 적용하고, 체력 바, 적 표현, 예고와 Story를 동기화합니다([슬롯머신과 룬 기반 전투 행동](slot-machine-and-runes.md)). |
| `TemporaryDiceRollMenuController` | 보류된 주사위 처리기입니다. 코드와 직렬화 설정은 남아 있지만 `TMP_MainScene`에서는 컴포넌트가 꺼져 있고 초기화에 참여하지 않습니다. |
| `ITemporaryCombatController` | 탐험(`ExplorationRunController`)이 전투를 시작·정리하는 계약입니다. 두 컨트롤러가 모두 구현합니다. |
| `TemporaryDiceMenuBuilder` | 주사위 경로의 설정 자산과 TMP_MainScene 참조를 적용하는 보류된 도구입니다. 슬롯머신이 적용된 Scene에서는 경고 후 중단합니다. |
| `TemporarySlotMachineIntegration` | 슬롯머신 Prefab·설정 자산, AppRoot 참조와 TMP_MainScene 연결을 적용·검증하는 임시 메뉴입니다. |
| `BoundaryCommandButton` (UI) | 경계선 "행동" 버튼의 표시·실행 가능 상태·클릭 실행·포커스 해제를 맡습니다. 실행은 `IGameMenuCommandHandler`로 컨트롤러에 전달합니다. |

## 한 턴의 처리

`행동` 명령(`temporary-slot.spin`)은 다음 순서로 처리됩니다.

1. 슬롯머신을 한 번 돌려 Payline 총합을 확정합니다.
2. 그 시점의 룬 줄 가중치로 총합을 분배합니다.
3. 수락 기록을 세션에 보관한 뒤 Reel 회전 연출을 보여 줍니다.
4. 연출이 끝나면 분배 결과를 줄 순서의 `PlayerCombatAction`으로 바꿔 전투에 한 번 적용합니다.

연출이 실패하거나 제한 시간을 넘겨도 다시 돌리지 않습니다. 제한 시간은 패널의 최대 연출 길이에 여유를 더한 값이며, 최솟값은 `spinTimeoutSeconds`입니다. 시간을 넘기면 Reel을 결과 칸에 먼저 멈춘 뒤 적용합니다. 수치 0인 줄은 행동하지 않습니다.

공격 줄은 임시 적을 공격하고 회복 줄은 임시 플레이어를 회복합니다. 적이 처리 도중 쓰러지면 이후 공격은 건너뛰고, 이후 회복은 계속 처리합니다. 적이 살아 있으면 예고된 공격 또는 회복을 한 번 수행하고 다음 행동으로 교대합니다. 최대 체력에서의 회복도 턴을 소비하며 실제 회복량 0을 기록합니다.

보류된 주사위 경로도 같은 전투 규칙(`ExecuteTurn` 위임)을 씁니다. 주사위 경로는 보유 주사위를 굴리고 물리 던지기를 연출한 뒤 적용했습니다([보유 주사위 3D 표시](owned-dice-presentation.md)). 두 경로는 `PlayerSessionHost`의 같은 행동 잠금(`TryBeginTemporaryCombatAction`)을 쓰므로 동시에 실행되지 않습니다. 탐험 이벤트의 슬롯 판정도 전투 없이 같은 잠금을 얻는 `TryBeginTemporarySlotPresentation`을 쓰므로 전투 행동과 겹치지 않습니다([탐험 이벤트](exploration-events.md#슬롯-판정)).

빠른 중복 실행은 컨트롤러의 실행 상태와 행동 버튼의 실행 가능 상태로 차단합니다. 이미 완료된 전투는 추가 행동을 받지 않습니다. UI 기록 오류가 발생하더라도 이미 적용한 전투 결과를 자동으로 재실행하지 않습니다.

## "행동" 버튼

"행동" 버튼은 게임 메뉴가 아니라 `MiddlePanel/ContentLayer/CombatActionBoundary`(Prefab `Assets/TxTRPG/UI/Prefabs/BoundaryCommandButton.prefab`의 인스턴스)에 있으며, `EnemyDisplayPanelDemo`와 `SlotMachinePanel` 사이 경계선 가운데에 겹쳐 놓입니다(주사위 보류 전에는 `OwnedDicePanel`). `StoryTextPanel`은 사용자가 왼쪽 열(`LeftPanel`, `NodeTreePanel` 아래)로 옮겼으므로 노드 선택 중에도 전투·탐험 기록이 보입니다. 배치 방식은 [FlexibleLayoutPanel 설계](flexible-layout-panel.md#레이아웃에-참여하지-않는-경계선-오버레이), 외형과 효과 확장 지점은 [경계선 명령 버튼](boundary-command-button.md)에 정리했습니다. 게임 메뉴에는 System·Bag·Status만 남고 행동 명령 바인딩은 없습니다. 버튼의 `commandId`는 `temporary-slot.spin`, `commandHandlerBehaviour`는 `TemporarySlotCombatController`입니다.

| 상태 | 버튼 |
| --- | --- |
| 전투 없음, 노드 선택 중(`ExplorationNodePanel` 표시) | 숨김 |
| 진행 중인 전투, 실행 가능 | 표시·활성 |
| 회전 연출·결과 처리 중, 게임 창 열림, Reel 없음, 장착된 룬 줄 없음(가중치 합 0) | 표시·비활성 (`CanExecute` 결과를 그대로 반영) |
| 전투 종료(승리 후 `ClearCombat`, 패배 후 `IsComplete`) | 숨김 |

컨트롤러는 `IsReady && !Combat.IsComplete`를 `BoundaryCommandButton.SetAvailable`로 전달합니다. 갱신은 폴링하지 않습니다. 컨트롤러의 모든 상태 변경 지점(초기화, 전투 시작·초기화·재연결, 회전 수락·완료·실패, 공용 행동 잠금 해제 `TemporaryCombatActionEnded`)은 `GameMenuPanel.RefreshExecutionState()`를 호출하고, 메뉴가 끝에 발생시키는 `ExecutionStateRefreshed`에서 버튼을 갱신합니다. `GameMenuPanel`은 `GameWindowService.Opened`·`Closed`도 구독하므로 게임 창이 열리고 닫힐 때도 같은 경로로 갱신됩니다. 실행은 버튼 클릭(마우스·터치)과 `Submit`이 같은 `Button.onClick` → `CanExecute/TryExecute(SpinCommandId)` 경로를 사용하며, 실행 잠금·결과 1회 적용·제한 시간 규칙은 주사위 경로와 같습니다.

포커스 규칙은 다음과 같습니다.

- 탐색: 행동 버튼은 `Explicit`이며 오른쪽·아래 → `System` 버튼입니다. 메뉴 버튼 세 개는 기존 `Automatic` 탐색을 유지하므로, 메뉴에서 행동 버튼으로 가는 경로는 Unity 자동 탐색이 위치로 고릅니다(사용자 결정).
- 전투 시작 시 자동 선택하지 않습니다.
- 숨겨질 때 행동 버튼이 선택되어 있으면 선택을 해제합니다(`focusFallback` 비어 있음). 탐험 화면의 "초기 자동 선택 없음" 규칙과 같게, 다음 방향 입력은 노드 카드로 진입합니다.
- 회전이 시작되어 버튼이 비활성이 되면 Unity가 선택을 해제합니다. 버튼은 이를 기억했다가 다시 활성화될 때 현재 선택이 없거나 숨겨진 오브젝트이면 자신을 다시 선택합니다. 그래서 회전이 끝나거나 게임 창을 닫으면 포커스가 행동 버튼으로 돌아옵니다. 플레이어가 그 사이 다른 보이는 UI를 선택했으면 그 선택을 유지합니다.
- `GameWindowService.Close`는 돌아갈 대상이 없을 때 숨겨진 창 컨트롤에 선택을 남기지 않고 해제합니다.
- 좁은 화면에서는 버튼이 선택되면 `ResponsiveMainScreen.Reveal`이 페이지 스크롤로 버튼을 보이게 합니다.

## Scene 수명과 표시

임시 컨트롤러는 전투 노드가 선택될 때까지 적을 생성하지 않습니다. TMP_MainScene에는 `MainScenePresentationController`가 없으며, `EnemyDisplayPanelDemo`의 `PanelStartupController.initialDataLoader` 연결을 해제해 `EnemyDisplayPanelDemoLoader`가 초기 데이터를 쓰지 않게 했으므로 임시 전투 컨트롤러만 적 표시를 갱신합니다. Scene이 파괴되면 체력 변경 구독을 해제하며, 재진입하면 `PlayerSessionHost`에 남은 탐험·전투 상태에 다시 연결합니다. 수락된 회전의 결과·Story·완료 통지도 세션에서 새 Scene으로 전달하며, 같은 결과를 다시 적용하거나 새로 돌리지 않습니다.

플레이어와 적 체력 바, 다음 적 행동, `StoryTextPanel`은 모두 같은 `TemporaryCombatState`를 읽습니다. 정식 전투, 보상, 경험치, 자동 부활, 룬·슬롯머신 저장과 전투 결과 저장 반영은 이 기능의 범위에 포함되지 않습니다.

## 실행 화면

![TMP_MainScene 임시 전투 실행 화면](../images/temporary-combat-runtime.png)

위 화면은 주사위 패널 기준이며, 현재는 같은 자리에 `SlotMachinePanel`이 표시됩니다.
