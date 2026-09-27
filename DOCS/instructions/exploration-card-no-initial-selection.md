# 노드 선택 카드의 초기 자동 선택 제거

## 1. 목적과 확정 동작

다음 노드 후보 카드를 표시할 때 첫 카드의 선택 강조가 자동으로 켜지는 동작을 제거한다. 모든 후보는 초기 UI 포커스가 없는 상태로 시작하며 실제 노드 진행 선택은 사용자의 명시적인 입력으로만 실행한다. 이 문서는 후속 구현 지침이며 코드·자산 적용이나 실행 검증이 끝났다는 의미가 아니다.

사용자가 확정한 키보드·컨트롤러 동작은 다음과 같다.

- 첫 방향 입력으로 목록 순서상 첫 번째 선택 가능한 카드에 포커스만 준다.
- 이후 확인 입력으로 해당 노드를 선택한다.
- 카드 포커스가 없는 초기 상태에서 확인 입력만으로 노드를 선택하거나 첫 카드에 자동 포커스를 주지 않는다.

최초 탐험과 노드 완료 후 새 후보 표시에서 동일하게 적용한다. 기존 카드 호버 강조·느린 박동, 눌림·확정 피드백과 선택 후 중복 실행 방지는 유지한다. 기본 외곽 테두리, UI 포커스 강조, 도메인의 노드 선택 확정은 서로 구분한다.

## 2. 선행 조사와 확인한 사실

`AGENTS.md`, `DOCS/README.md`, `DOCS/architecture/exploration-node-tree.md`, `DOCS/development/workflows.md`를 읽는다. 선행 `DOCS/instructions/exploration-card-readability-and-hover-pulse.md`의 호버·포커스 피드백을 유지하면서 초기 포커스 정책만 변경한다.

시작 시 Git 상태와 관련 diff를 확인하여 사용자 변경을 보존한다. `.codegraph/`가 있으면 호출·영향 분석에 먼저 사용하고 실제 소스·자산과 대조한다. 임의로 재색인하지 않는다.

작성 시 확인한 사항은 다음과 같다.

1. `ExplorationNodeChoiceCardList.Show`는 데이터 바인딩·레이아웃·스크롤 상단 적용 후, 후보가 있으면 `EventSystem.current.SetSelectedGameObject(cards[0].Button.gameObject)`를 호출한다. 초기 첫 카드 포커스 지정 경로가 확인되었다.
2. `ExplorationCardFeedbackController.OnSelect`는 focused를 켜며 `RefreshTarget`은 hovered 또는 focused를 강조 상태로 취급하여 focusVisual을 활성화한다. 이것이 자동 강조의 가능한 연결 경로이다. 사용자가 말한 Outline이 실제 FocusVisual인지 기본 Border인지 실행 화면에서 확인한다.
3. 현재 TMP_MainScene의 EventSystem에는 `m_FirstSelected: {fileID: 0}`이 저장되어 있다. Scene 설정만 바꾸는 수정으로 Show 내부 자동 선택은 없어지지 않는다.
4. `ExplorationRunController`에는 카드 목록이 없는 legacy 선택 버튼 경로에서도 첫 버튼을 선택하는 코드가 있다. 계속 진행 버튼의 포커스 지정은 별도의 동작이다. 운영 카드 경로를 우선 수정하고 관련 없는 계속 버튼·메뉴·모달의 기본 포커스는 제거하지 않는다.
5. 카드는 재사용되며 Bind·Unbind·ResetPresentation과 피드백 ResetState가 있다. 자동 지정 한 줄만 삭제하면 EventSystem이 재사용된 이전 카드 오브젝트를 계속 선택한 상태가 남을 수 있으므로 실제 수명을 검사한다.

### 영향 경로

| 경로 | 책임과 확인 사항 |
| --- | --- |
| `Assets/TxTRPG/UI/Runtime/Exploration/ExplorationNodeChoiceCardList.cs` | Show·Hide·카드 재사용·세대 번호·확정 잠금과 탐색 진입이다. |
| `Assets/TxTRPG/UI/Runtime/Exploration/ExplorationNodeChoiceCardView.cs` | Bind·Unbind·ResetPresentation과 선택 콜백이다. |
| `Assets/TxTRPG/UI/Runtime/Exploration/ExplorationCardFeedbackController.cs` | hovered·focused·navigationFocus·Outline과 박동 상태이다. |
| `Assets/TxTRPG/UI/Runtime/Exploration/ExplorationNodeChoiceLayoutGroup.cs` | 줄바꿈 배치와 방향 탐색의 공간적 관계를 확인한다. |
| `Assets/TxTRPG/Application/Runtime/Exploration/ExplorationRunController.cs` | 신규 후보 표시, legacy 선택 경로와 계속 버튼의 구분이다. |
| `Assets/TxTRPG/UI/Runtime/Windows/GameWindowService.cs` | 모달이 열린 동안 카드 입력을 받지 않도록 기존 소유권을 확인한다. |
| `Assets/TxTRPG/UI/Prefabs/ExplorationNodeChoiceCard.prefab` | 기본 Border와 선택 강조 참조·직렬화 상태를 확인한다. |
| `Assets/TxTRPG/UI/Editor/ExplorationNodeChoiceCardPrefabBuilder.cs` | 자산 수정이 필요할 때 생성 결과와 일치시킨다. |
| `Assets/TxTRPG/Application/Editor/TemporaryDiceMenuBuilder.cs` | 운영 카드 목록의 필수 연결이 추가될 때 조사한다. |
| `Assets/Scenes/AppScene.unity`, `Assets/Scenes/TMP_MainScene.unity` | 정상 시작 경로와 EventSystem·Input System 설정이다. |
| `Assets/TxTRPG/UI/Tests/Editor/ExplorationNodeChoiceCardTests.cs` | 초기화·재사용·선택 콜백의 집중 검증 지점이다. |
| `Assets/TxTRPG/UI/Tests/PlayMode/ExplorationCardFeedbackPlayModeTests.cs` | 실제 피드백과 입력 수명 회귀 검증 지점이다. |

## 3. 초기 무선택 상태

Show 완료 후 해당 카드 목록의 어느 Button도 EventSystem 선택 대상으로 자동 지정하지 않는다. 카드에 숨은 선택 상태를 남기고 Outline만 끄는 방식으로 처리하지 않는다. UI 포커스가 없으면 focused 기반 강조와 포커스 기반 박동도 없어야 한다.

목록 교체 전에 EventSystem의 현재 선택이 이 목록이 소유하는 이전 카드인지 확인하고, 해당하는 경우에만 선택을 해제한다. 다른 모달·메뉴의 포커스를 무조건 null로 만들지 않는다. 카드가 재사용되는 순서와 OnDeselect·ResetState의 적용 순서를 정리하여 이전 노드의 focused·pressed·confirming·탐색 출처가 새 후보에 남지 않게 한다.

Hide·Disable·Destroy 및 후보 교체 시 대기 중 입력과 구독을 정리한다. 기존 세대 번호와 선택 잠금을 유지하며 이전 콜백·확정 Coroutine이 새 후보를 선택하지 못하게 한다. 초기화 때문에 외부 onClick 리스너를 제거하거나 기본 테두리를 영구 비활성화하지 않는다.

마우스가 실제 카드 위에 있으면 기존 hovered 강조가 표시될 수 있다. 이는 자동 포커스와 다른 정상 동작이다. 모든 강조를 억제하기 위해 hover를 끄거나 포인터를 이동시켜야만 반응하도록 변경하지 않는다. 테스트에서는 포인터가 카드 밖에 있는 무입력 초기 상태와 카드 위의 호버 상태를 분리한다.

## 4. 무선택 상태에서 탐색 시작

EventSystem의 선택 대상이 null일 때에는 일반 선택 오브젝트의 이동 핸들러만으로 최초 탐색을 받지 못할 수 있다. 기존 `InputSystemUIInputModule`의 의미 기반 Navigate/move 액션과 UI 입력 소유권을 조사하여 목록 수준의 최소 진입 경로를 구현한다. 물리 키·게임패드 버튼을 직접 하드코딩하거나 각 카드마다 최초 탐색용 전역 구독을 추가하지 않는다.

첫 유효 방향 입력은 다음 조건에서 첫 활성·표시·상호작용 가능한 카드에 포커스를 준다.

- 해당 목록이 현재 탐험 선택 화면이며 입력을 허용한다.
- 선택 확정 처리 중이 아니고 더 높은 우선순위 모달·UI가 입력을 소유하지 않는다.
- 이 목록에 유효한 카드 포커스가 없다. 다른 활성 UI를 조작 중인 입력을 가로채지 않는다.

기존 탐험 화면이 표시될 때 입력 소유권이 어디로 이동하는지 명시적으로 정리한다. 이전 계속 버튼이 숨겨진 뒤 null 포커스로 진입하는 경우에도 방향 입력이 동작해야 한다. 선택이 없는 상태를 이유로 숨은 더미 Button에 포커스를 주어 초기 Submit이 전달되게 하지 않는다.

방향이 상·하·좌·우 중 무엇이든 최초 진입 대상은 목록 순서상 첫 번째 선택 가능한 카드이다. 같은 입력이 기본 UI 모듈에 이어 전달되어 두 번째 카드까지 이동하거나 즉시 노드를 확정하지 않도록 처리 순서와 소비를 보장한다. 목록이 열리기 전부터 눌려 있던 방향 입력이나 이전 화면의 Submit 잔여 입력이 자동 진입·선택을 유발하지 않게 한다. 필요하면 중립 상태를 거친 새 입력에서 탐색을 시작한다.

탐색 시작 후에는 기존 방향 Navigation과 Submit을 사용한다. 줄바꿈·세로 스크롤 상태에서 현재 포커스가 보이고 활성 카드 간 이동이 결정적이어야 한다. 선택 가능한 카드가 하나도 없으면 포커스·진행 없이 기다리며 예외·반복 경고를 발생시키지 않는다.

초기 Submit은 카드 선택과 자동 포커스를 모두 실행하지 않는다. 방향 입력과 Submit이 같은 처리 주기에 들어온 경우 최초 진입 동작은 포커스 지정까지만 수행하고, 이후 별도의 확인 입력으로 확정한다. 기존 UI 액션을 전역 Disable하거나 다른 화면의 Submit을 차단하는 구현은 금지한다.

## 5. 포인터·모달·수명 호환성

마우스 클릭과 터치 탭은 초기 무선택 상태에서도 해당 카드를 기존 경로로 선택할 수 있어야 한다. 터치에 사전 방향 탐색을 요구하지 않는다. 스크롤 드래그는 선택으로 실행되지 않아야 하며 터치 후 잔여 hover 박동을 만들지 않는다.

새 후보마다 초기 무선택 정책을 적용한다. 같은 후보를 실제로 재표시하는 호출과 단순 설정·색상 갱신은 구분하여, 사용자가 탐색 중인 포커스를 매 프레임 또는 설정 변경마다 지우지 않는다. 후보 세대·목록 수명을 기준으로 초기화 경계를 정한다.

모달이 열린 동안 방향·Submit 입력으로 배경 카드에 포커스를 주지 않는다. 모달의 정상 포커스 복원은 보존하며, 새 후보가 표시되었다는 이유만으로 모달 입력을 빼앗지 않는다. 장치 교체·분리, 목록 비활성화·Scene 종료 시 구독을 해제한다. 반복 표시마다 구독이나 콜백이 누적되지 않아야 한다.

기존 Run·ChoiceSet·Node ID 검증, 목록 전체 잠금, 선택 확정 연출과 정확히 한 번의 도메인 선택을 유지한다. Outline 표시 정책을 바꾸면서 게임의 노드 상태·저장 데이터·탐험 생성 규칙을 변경하지 않는다. legacy 버튼 경로는 실제 운영 여부를 확인하고, 이번 카드 수정만을 이유로 무관한 화면의 자동 포커스를 전역 제거하지 않는다.

## 6. 적용과 검증

1. AppScene에서 TMP_MainScene으로 진입하여 포인터를 카드 밖에 두고 첫 후보의 selectedGameObject와 실제 강조 오브젝트를 확인한다. 수정 전 자동 포커스와 기본 장식 테두리를 구분한다.
2. 자동 지정과 재사용 잔여 선택을 수정하고 목록 수준의 첫 방향 입력 진입을 구현한다. EventSystem이 없거나 선택 가능한 카드가 없는 경우도 안전하게 처리한다.
3. Unity 연결·컴파일·미저장 변경·Play Mode를 확인한다. 필요한 경우에만 카드 Prefab·운영 목록의 필수 참조를 선별 적용한다. Scene 전체 재생성, 무관한 자산 저장과 사용자 override 삭제는 하지 않는다.
4. 최초 후보와 다음 후보에서 무입력 상태의 카드 선택이 없고 포커스 강조·박동이 없는지 확인한다. 기본 테두리와 hover는 정상 유지되어야 한다.
5. 초기 Submit 무동작, 첫 방향 입력의 포커스만 지정, 이후 방향 탐색과 별도 Submit의 단일 선택을 실제 Input System 경로로 검증한다. 액션 콜백이나 OnSelect 직접 호출만으로 입력 처리가 검증되었다고 보고하지 않는다.
6. 이전 첫 카드·중간 카드·마지막 카드에 포커스가 있던 상태에서 새 후보로 전환하고, 동일 GameObject가 재사용되어도 무선택인지 확인한다. 길게 누른 입력·동시 입력·빠른 재개방·후보 0개·비활성 첫 카드를 포함한다.
7. 마우스 호버·클릭, 터치 탭·드래그, 키보드·컨트롤러 탐색, 모달 개방 중 입력·복원과 Scene 종료를 확인한다. 기존 효과 끄기·움직임 줄이기와 느린 박동도 회귀 검증한다.
8. 관련 Edit Mode·Play Mode 테스트를 실행하고 초기 무선택·첫 방향 진입·Submit 방지·재사용·모달 입력 차단의 집중 테스트를 추가한다. 실제 기기와 입력 이벤트 시뮬레이션을 구분한다. 가능한 대상의 컴파일을 확인하고 미검증 항목은 이유와 함께 보고한다.
9. diff와 Unity 직렬화를 검토하여 무관한 Outline·메뉴·계속 버튼·슬롯·저장 설정이 변경되지 않았는지 확인한다.

## 7. 문서 갱신과 최종 보고

`DOCS/architecture/exploration-node-tree.md`에 초기 무선택과 첫 방향 입력 진입, hover·포커스·도메인 선택의 차이를 기록한다. `DOCS/development/workflows.md`에 입력 검증과 실제 적용 절차를 반영한다. 주요 책임 경계를 바꿨다면 `DOCS/architecture/project-structure.md`도 갱신한다. 새 지침서는 두 문서 색인에 연결한다.

최종 보고는 확인한 원인, 변경한 자동 선택·입력 진입·재사용 처리, 실제 적용한 자산·Scene, 수행한 테스트와 미검증 범위를 포함한다. 구현·Editor 적용·실행 검증을 구분한다. 자동화가 막히면 원인과 남은 작업을 보고하고 도구 제공만으로 완료 처리하지 않는다. 사용자가 실행할 Tools 메뉴가 남으면 별도 `사용자가 수행할 적용 절차`에 정확한 메뉴 경로·순서·전제·저장·재실행·중단·최종 검증을 fenced text 블록으로 제공하며 개발 문서에도 기록한다. 수동 Tools 단계가 없으면 없다고 명시한다.
