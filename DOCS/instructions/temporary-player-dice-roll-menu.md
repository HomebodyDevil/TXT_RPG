# 임시 플레이어 주사위 보유·메뉴 굴림·Story 결과 표시

## 목적과 확정 사항

TMP_MainScene의 기존 GameMenuPanel에 임시 주사위 굴림 버튼을 추가하고, 플레이어가 보유한 주사위 각각의 결과를 기존 StoryTextPanel에 누적 표시한다. 사용자 표기의 StroyTextPanel은 프로젝트의 StoryTextPanel을 의미한다.

사용자가 확인한 방향은 다음과 같다.

- D4, D6, D8을 각 1개 보유한다. 면 값은 각각 1~4, 1~6, 1~8이다.
- 소유자는 활성 캐릭터가 아니라 플레이어 세션이다. 캐릭터가 바뀌어도 유지한다.
- 실행을 종료하고 다시 시작하면 초기 구성으로 돌아간다. 운영 저장 파일은 변경하지 않는다.
- 임시 버튼 한 번으로 현재 임시 보유 목록 전체를 각 1회 굴린다. 정식 장착 시스템이나 결과 합산·턴 진행은 이번 범위에 포함하지 않는다.

이 문서는 구현 예정 지침이다. 코드·설정·필요 자산·Scene 연결·검증을 포함하며 지침서 작성만으로 실제 구현되었다고 취급하지 않는다.

## 사전 조사와 확인된 구조

AGENTS.md, DOCS/README.md와 다음 문서를 읽는다.

- `DOCS/architecture/dice-domain.md`
- `DOCS/architecture/player-session.md`
- `DOCS/architecture/scene-transition.md`
- `DOCS/architecture/quick-items-and-game-windows.md`
- `DOCS/architecture/story-text-panel.md`
- `DOCS/architecture/main-scene-default-presentation.md`

CodeGraph로 호출자·의존성을 먼저 확인하고 실제 소스와 자산으로 검증한다. 기존 미저장 Scene 및 Prefab Override를 보존한다.

현재 Dice는 순수 C# 객체이며 IRandomIndexSource를 받아 Roll하고 DiceRollResult로 면 인덱스와 값을 반환한다. PlayerSessionHost는 AppScene 수명의 세션을 제공한다. GameMenuButtonBinding은 PageId 등 창 요청 설정을 가지고 GameMenuPanel.Open은 Window 서비스를 호출한다. StoryTextPanel.AddMessage는 기존 기록 뒤에 메시지를 추가한다. 이 구조를 재확인한 뒤 확장한다.

## 영향 경로

| 기존 경로 | 확인·변경 범위 |
| --- | --- |
| `Assets/TxTRPG/Gameplay/Runtime/Dice/` | 기존 Dice, DiceRollResult, 난수 공급자를 재사용한다. |
| `Assets/TxTRPG/Application/Runtime/Players/PlayerSessionHost.cs` | 세션 초기화·종료에 임시 상태 수명을 연결한다. |
| `Assets/TxTRPG/UI/Runtime/Windows/GameMenuPanel.cs` | 창 열기와 임시 명령 버튼을 구분하고 기존 레이아웃·입력을 유지한다. |
| `Assets/TxTRPG/UI/Runtime/StoryTextPanel.cs` | 기존 AddMessage API를 사용한다. 주사위 게임 규칙을 넣지 않는다. |
| `Assets/TxTRPG/UI/Prefabs/GameMenuPanel.prefab` | 공용 변경 필요성을 검토하며 다른 화면에 임시 기능을 무조건 활성화하지 않는다. |
| `Assets/Scenes/AppScene.unity` | 세션 수명의 임시 기능 설정이 필요한 경우 최소 연결만 추가한다. |
| `Assets/Scenes/TMP_MainScene.unity` | 기존 메뉴 안의 버튼과 기존 StoryTextPanel을 연결한다. |
| `Assets/TxTRPG/Application/Tests/Editor/`, `Assets/TxTRPG/UI/Tests/` | 관련 테스트와 화면 검증을 추가한다. |

신규 Application 코드의 제안 위치는 `Assets/TxTRPG/Application/Runtime/Dice/`다. 명명과 파일 분리는 기존 관례에 맞춘다. 새 전역 매니저, 별도 플레이어 세션, 별도 이야기 패널을 만들지 않는다.

## 책임과 데이터 소유권

| 역할 | 책임 |
| --- | --- |
| 임시 주사위 설정 | 기능 활성 여부, 순서가 있는 면 구성과 허용 범위를 개발자가 수정할 수 있게 한다. |
| 세션 임시 상태 | 설정을 복사해 생성한 개별 Dice와 세션 내 식별자를 보유한다. UI와 저장 DTO를 참조하지 않는다. |
| 굴림 처리 | 현재 목록을 안정적인 순서로 각 1회 굴려 식별자·표시 순서·당시 면 수·결과를 묶어 반환한다. |
| 메뉴 명령 연결 | 사용자의 버튼 활성화를 처리에 전달하고 준비 상태를 표시한다. |
| 결과 표현 연결 | 결과 값을 문자열로 변환하여 기존 StoryTextPanel에 추가한다. |

각 역할을 반드시 별도 클래스·인터페이스로 만들 필요는 없다. 기존 서비스를 활용하고 미래를 위한 범용 명령 프레임워크는 만들지 않는다.

## 임시 보유와 수명

세션이 준비되면 유효한 임시 설정으로 D4/D6/D8 각 1개를 한 번만 생성한다. 각 Dice는 독립적인 상태다. 메뉴 생성, OnEnable, Scene 재진입, 캐릭터 선택 변경마다 새로 지급하지 않는다.

세션이 유지되는 콘텐츠 Scene 전환 동안 목록과 런타임 면 변경을 유지한다. 세션이 종료되거나 다른 플레이어 세션으로 교체되면 임시 상태를 폐기한다. Editor의 Domain Reload 비활성 설정에서도 이전 실행의 정적 상태가 남지 않도록 한다.

임시 설정의 기본 활성 여부를 명시적으로 관리하고 TMP_MainScene 검증 경로에서는 활성화한다. 다른 운영 경로에는 암묵적으로 지급하지 않는다. 설정을 끄면 임시 버튼도 숨기거나 사용할 수 없도록 일관되게 처리한다. 비활성 상태에서 새 난수 호출이나 데이터 지급이 없어야 한다.

인벤토리, QuickItemLoadout, PlayerSaveData 또는 저장 버전에는 주사위를 추가하지 않는다. 임시 상태를 저장 모델에 넣고 우연히 직렬화되게 하지 않는다. 기존 저장을 삭제하거나 초기화하지 않는다. 향후 정식 소유·장착 공급자로 바꿀 수 있게 UI는 임시 설정의 구체적인 생성 로직을 알지 않도록 한다.

잘못된 면 설정은 기존 Dice 검증으로 거부하고 명확하게 진단한다. 일부만 조용히 지급하거나 임의 숫자로 보정하지 않는다. 명시적인 빈 목록은 정상적인 0개 보유 상태로 처리한다.

## GameMenuPanel 버튼

기존 메뉴 콘텐츠 안에 `주사위 굴리기 (임시)` 기능을 추가한다. 기존 버튼 View와 레이아웃·아이콘/라벨 규칙을 재사용하고 현재 레이아웃 정책에 따라 탐색할 수 있게 한다. 표시 이름은 설정 또는 기존 현지화 경계에서 관리한다.

이 버튼은 Modal Window를 열지 않는 명령이다. 존재하지 않는 PageId를 지정하거나 Window 서비스에 가짜 페이지를 등록하지 않는다. 창 열기/명령 실행을 최소한의 명시적 구분으로 확장하거나 기존 적합한 명령 경로를 활용한다. 기존 직렬화 바인딩은 계속 창 열기로 작동해야 한다.

Window 서비스의 페이지 유무 검사 때문에 주사위 버튼이 잘못 비활성화되지 않도록 준비 조건을 구분한다. 주사위 세션, 결과 출력 대상과 필요한 UI 참조가 준비되었을 때만 활성화한다. 기존 Bag·System·Status 버튼의 동작을 바꾸지 않는다.

클릭·터치·Submit은 동일한 명령을 호출한다. 한 번의 활성화에 이벤트가 이중 등록되어 두 번 굴러가지 않도록 한다. OnDisable·파괴·재바인딩 시 구독을 해제한다. 사용자가 별도로 다시 누르면 새로운 한 묶음의 결과를 생성하며 임의 쿨다운이나 턴 제한은 추가하지 않는다.

## 굴림과 StoryTextPanel 표시

주사위 목록과 표시 정보의 스냅샷을 잡고 순서대로 각 1회 굴린다. 난수 공급자는 세션 동안 재사용하고 표시 계층에서 다시 추첨하지 않는다. 결과에 주사위 식별 정보를 함께 전달해 같은 D6가 여러 개여도 구분할 수 있게 한다.

기존 StoryTextPanel.AddMessage로 주사위당 한 메시지를 추가한다. 예시의 숫자는 결과 예시이지 고정 출력이 아니다.

```text
[주사위] 1번 주사위 (D4): 3
[주사위] 2번 주사위 (D6): 5
[주사위] 3번 주사위 (D8): 2
```

기존 기록을 Clear하거나 Replace하지 않는다. 새 결과는 아래에 추가하고 기존 Follow Latest Message·스크롤·오래된 메시지 투명도 정책을 유지한다. 사용자 스크롤 정책을 무조건 덮어쓰지 않는다. 표시 문구는 도메인에 넣지 않고 포맷 경계에 둔다. 면 인덱스가 아니라 DiceRollResult의 숫자 값을 보여준다.

MainScenePresentationController 등 초기 데이터 공급자가 후속 초기화로 결과를 지우지 않도록 준비 순서를 확인한다. 부트스트랩·Story 기본 데이터 적용 완료 전에 버튼을 활성화하지 않는다. Scene이 언로드된 후 파괴된 Panel에 결과를 보내지 않는다.

0개 보유 상태는 예외가 아니며 굴림 불가 사유를 표시한다. 출력 대상이 준비되지 않았다면 난수를 소비하기 전에 요청을 거부한다. 중간 굴림 실패 시 부분 결과를 정상적인 전체 성공처럼 표시하지 않고 오류를 보고한다. 실패를 숨기기 위한 자동 재추첨은 하지 않는다. 이번 임시 동기 작업에서 RNG 되감기나 영구 결과 저장까지 구현하지 않는다.

## 실제 적용과 검증

1. 기존 Scene·Prefab 연결과 dirty 상태를 확인하고 필요한 변경만 적용한다. 사용자 배치와 Override, 저장 파일을 보존한다.
2. 실제 AppScene 초기화 경로에서 TMP_MainScene을 실행하여 D4/D6/D8이 정확히 한 번씩 생성되는지 확인한다. Editor의 Play 시작 정책도 확인하여 실행 방법을 문서화한다.
3. 버튼을 한 번 누르면 주사위별 정확히 3개의 결과가 기존 이야기 뒤에 추가되는지 확인한다. 두 번 누르면 두 묶음이 순서대로 남아야 한다.
4. 가짜 난수로 지정한 면의 실제 숫자가 출력되는지 검증한다. 중복 면 값·런타임 숫자 변경도 결과에 반영되어야 한다.
5. 캐릭터 변경, Scene 재진입, 메뉴 재활성화 후 지급·이벤트 중복이 없는지 확인한다. 재실행하면 초기 구성으로 돌아와야 한다.
6. 0개 보유, 잘못된 설정, 세션 미준비, Story 참조 누락, 기능 비활성에서 안정적인 처리를 확인한다.
7. Bag·System 창, 메뉴 배치, 키보드/컨트롤러 포커스·Submit, 마우스·터치 경로와 좁은 화면을 지원 범위에서 확인한다. 실제 기기 검증과 Editor 모의 확인을 구분한다.
8. 저장 코드 diff와 회귀 테스트로 임시 주사위가 저장 스키마나 기존 사용자 데이터에 포함되지 않는지 확인한다. 기존의 정상적인 자동 저장 자체를 막지는 않는다.
9. 신규 도메인 연결·Application·UI 테스트와 영향 어셈블리 컴파일을 수행한다. 실제 메뉴 클릭과 결과 표시 화면을 캡처한다. 미수행 검증은 명시한다.

## 문서와 최종 보고

`DOCS/architecture/dice-domain.md`와 `DOCS/architecture/quick-items-and-game-windows.md`에 실제 연결 책임과 임시 범위를 기록한다. 주요 구조가 추가되면 `DOCS/architecture/project-structure.md`를 갱신한다. `DOCS/development/workflows.md`에는 실행 Scene, 버튼 위치, 설정 위치, 기능 해제 및 추후 제거 지점과 검증 방법을 기록한다.

최종 보고에는 변경 경로, 세션 소유·초기화 정책, 실제 버튼과 결과 화면, 테스트, 미검증 사항을 포함한다. 수동 Tools 실행이 없다면 없다고 명시한다. 남는다면 별도 `사용자가 수행할 적용 절차`에 정확한 메뉴 경로·전제 조건·순서·저장·성공 확인을 번호가 있는 코드 블록으로 제공하고, 코드 작성과 실제 적용·검증 상태를 구분한다.
