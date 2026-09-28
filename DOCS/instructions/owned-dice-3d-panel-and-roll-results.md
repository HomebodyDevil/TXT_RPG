# 탐험 트리 부모 패널과 보유 주사위 3D 표시·굴림 결과

## 1. 목적과 작업 범위

TMP_MainScene의 기존 NodeTreePanel 위에 Flexible Layout 부모를 추가하고, 트리 아래에 현재 보유 주사위의 3D 표시 영역을 구성한다. 기존 확률 판정을 유지하면서 결과와 일치하는 굴림 애니메이션, 실행 중 보유 목록 갱신, 전투별 결과 기록과 모달 조회를 연결한다.

이 문서는 구현 지침이다. 아래 신규 타입·자산과 계층은 예정된 구조이며 아직 구현되었다고 간주하지 않는다. 문서 작성 작업은 Scene·Prefab·런타임 코드를 수정하지 않는다.

포함 범위는 메인 Scene 통합, D4·D6·D8 기본 자산, 보유 추가·제거 API, 결과 확정과 표시의 분리, 공통 결과 모달, 기록 보관 정책 및 검증이다. 물리 결과 판정, 획득·제거 전용 사용자 화면, 영구 저장, Text Area 탭, 인게임 옵션 화면 자체는 제외한다. 자동 모달 표시 설정은 이번에 분리하여 추후 인게임 옵션에서 변경할 수 있게 한다.

## 2. 사용자 확정 사항

| 항목 | 확정 요구 |
| --- | --- |
| 결과 판정 | 기존 확률로 먼저 한 번 판정하고 애니메이션을 해당 결과에 맞춰 정지시킨다. |
| 보유 변경 | 초기 보유 설정과 런타임 추가·제거 API 및 즉시 표시 갱신을 구현한다. 영구 저장은 제외한다. |
| 기본 형태 | D4·D6·D8을 모두 표시하고 추후 다면체를 추가할 수 있게 한다. |
| 세로 배분 | 넓은 화면에서 트리:주사위를 약 2:1로 배분한다. 좁은 화면은 최소 높이와 전체 화면 스크롤을 사용한다. |
| 많은 주사위 | 페이지로 나누지 않고 모두 한 화면에 배치한다. 개수가 늘면 크기를 줄인다. |
| 적용 시점 | 전체 굴림 연출 종료 후 전투와 Story에 결과를 한 번 적용한다. 연출 실패 시에도 이미 정한 결과로 완료한다. 진행 중 추가 행동은 차단한다. |
| 결과 모달 | 굴림 종료 후 기본적으로 자동 표시한다. 확인 버튼 또는 바깥 클릭·탭으로 닫으며 열린 동안 다음 행동을 차단한다. |
| 자동 표시 설정 | 기본값은 활성이다. 추후 인게임 옵션에 연결할 수 있도록 런타임 설정 경계를 제공한다. |
| 다시 보기 | OwnedDicePanel의 결과 보기 버튼에서 동일한 결과 모달을 연다. Text Area는 유지한다. |
| 기본 기록 범위 | 현재 전투 노드의 결과들을 보관한다. 비전투 노드에서는 가장 최근 전투 노드의 결과들을 유지한다. |
| 확장 기록 범위 | 개발자가 세션의 최근 전투 기록 보관 방식으로 전환할 수 있게 한다. |

외부 입력으로 강제로 주사위를 던지는 기능이나 수동 회전·드래그 조작은 이번 요구에 포함하지 않는다. 키보드·컨트롤러는 결과 보기와 확인 버튼에 Navigate·Submit으로 접근하고, 터치는 동일 버튼과 바깥 탭을 사용한다. 이번 결과 모달의 닫기 입력은 위 확정 요구로 한정하며 공통 모달 전체의 Cancel 정책은 변경하지 않는다.

## 3. 선행 문서와 확인한 현재 구조

작업 전 AGENTS.md, DOCS/README.md와 아래 문서를 읽는다.

- `DOCS/architecture/flexible-layout-panel.md`
- `DOCS/architecture/exploration-history-tree.md`
- `DOCS/architecture/dice-domain.md`
- `DOCS/architecture/temporary-combat.md`
- `DOCS/architecture/player-session.md`
- `DOCS/architecture/quick-items-and-game-windows.md`
- `DOCS/architecture/panel-startup.md`
- `DOCS/development/workflows.md`

현재 저장된 Scene과 소스에서 다음을 확인했다. 구현 직전 현재 작업 트리를 다시 확인한다.

- Scene은 `Assets/Scenes/TMP_MainScene.unity`이며 트리는 `Canvas/MainScreenViewport/Main_FlexibleLayoutPanel/ContentLayer/NodeTreePanel` 안에 있다. 오래된 단축 경로로 새 트리를 중복 생성하지 않는다.
- `ResponsiveMainScreen`은 상위 세 영역의 FlexibleLayoutItem을 직렬화 참조하며 좁은 화면에서 첫 영역 높이를 360으로 배분한다. 새 부모 추가 시 기존 트리 참조를 새 부모의 항목으로 교체하고 내부 두 영역의 필요 높이를 반영해야 한다.
- `TemporaryPlayerDiceState`는 초기 설정을 복사하고 Count와 RollAll을 제공한다. 현재 런타임 보유 목록 조회·추가·제거·변경 알림 API는 없다.
- `PlayerSessionHost.TemporaryDice`는 AppScene 수명으로 보유 상태를 유지한다. 기본 설정은 D4·D6·D8이며 각 면은 숫자만이 아닌 효과 종류와 수치를 보유한다.
- `TemporaryDiceRollMenuController.TryExecute`는 현재 RollAll → ExecuteTurn → RecordTurn → 표시 갱신을 동기식으로 수행한다. 연출 완료 대기는 새로 구현해야 한다.
- `Dice.ResolveFace`는 지정한 인덱스의 결과를 반환하지만 이번 방식에서 결과를 다시 판정하는 용도로 호출하지 않는다. 이미 얻은 DiceRollResult를 그대로 전달한다.
- 공통 모달은 `GameWindowService`, `ModalWindowHost`, `GameWindowPage`, `ModalOpenRequest`를 사용한다. 결과 전용 모달 시스템을 별도로 중복 구현할 필요가 없다.
- `TxTRPG.Application`은 `TxTRPG.UI`를 참조한다. UI에서 Application을 역참조하여 순환 의존성을 만들지 않는다.

CodeGraph로 관련 진입점·호출자·테스트를 탐색하고 실제 소스와 Scene 참조로 검증한다. 패키지 캐시나 생성 폴더를 수정하지 않는다.

## 4. 판정 방식 비교와 채택 이유

| 방식 | 장점 | 제약 | 이번 범위 |
| --- | --- | --- | --- |
| 확률 판정 + 애니메이션 | 기존 면별 확률과 결과 스냅샷을 유지하고 정확한 최종 자세를 지정할 수 있다. | 자연스러운 튕김은 연출로 구성한다. | 채택한다. |
| 확률 판정 + 물리 연출 + 최종 자세 보정 | 확률을 유지하면서 충돌 표현을 활용할 수 있다. | 최종 보정의 자연스러움, 충돌과 타임아웃 검증이 추가된다. | 구현하지 않는다. |
| 물리 정지 자세로 판정 | 실제 움직임이 결과를 결정한다. | 균등 확률을 보장할 수 없고 기울어짐·겹침·미정지 처리와 분포 검증이 필요하다. | 후속 확장 가능성만 고려한다. |

물리 방식의 일반적인 상단 면 후보는 면의 월드 법선과 기준 위쪽 벡터의 내적이 가장 큰 면이다. 외적 자체가 상단 면을 선택하는 연산은 아니다. D4와 특수 다면체에는 별도 읽기 규칙이 필요하다. 이번에는 물리 엔진의 자세나 바닥 접촉이 결과를 바꾸지 않는다.

참고: [Unity Vector3.Dot](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Vector3.Dot.html), [Unity Physics.Simulate](https://docs.unity3d.com/ja/2023.2/ScriptReference/Physics.Simulate.html). 공식 문서의 시간 간격 관련 설명을 모든 플랫폼에서 물리 결과가 동일하다는 보장으로 확대 해석하지 않는다.

## 5. 계층과 Flexible Layout 적용

새 부모 이름은 `ExplorationAndDicePanel`, 하단 패널 이름은 `OwnedDicePanel`로 한다. 기존 NodeTreePanel 객체와 내부 자산·참조는 유지하여 재부모화한다.

```text
Canvas/MainScreenViewport/Main_FlexibleLayoutPanel/ContentLayer
└── ExplorationAndDicePanel                 신규 FlexibleLayoutPanel + FlexibleLayoutItem
    ├── BackgroundLayer                     기존 FlexibleLayoutPanel 구성 준수
    ├── BackgroundContentLayer
    ├── ContentLayer                        세로 FlexibleContentLayoutGroup
    │   ├── NodeTreePanel                    기존 객체, 내부 트리 유지
    │   └── OwnedDicePanel                   신규 FlexibleLayoutItem
    │       ├── DiceViewport                 RawImage, 3D 렌더 결과
    │       ├── EmptyOrErrorState
    │       └── ResultsButton                결과 보기
    └── ForegroundLayer
```

3D 모델·카메라·조명·연출용 바닥은 별도 DiceStage 인스턴스가 소유한다. RectTransform 아래에 모델만 넣으면 화면에 올바르게 표시된다고 가정하지 않는다.

- 기존 NodeTreePanel의 상위 형제 순서와 외부 배분 정책은 새 부모가 이어받는다. NodeTreePanel은 새 부모 내부의 항목 설정을 사용한다.
- 새 부모 내부 축은 항상 세로로 두어 트리가 위, 주사위가 아래에 유지되게 한다. 가중치 2:1은 여백·최소 크기를 제외한 공간에 적용되므로 정확한 전체 높이 비율로 설명하지 않는다.
- 개발자가 Weight, 최소 높이, Padding·Spacing을 설정할 수 있게 한다. 기존 트리의 배경·내부 여백과 새 부모 배경을 중첩하여 불필요한 여백·불투명 배경이 생기지 않게 한다.
- 좁은 화면에서는 트리의 기존 가독 가능한 높이와 주사위 표시·결과 버튼의 최소 높이를 합산한다. ResponsiveMainScreen의 기존 360 높이를 새 부모에 그대로 적용하여 두 영역을 압축하지 않는다.
- 상위 페이지 스크롤, 트리 내부 스크롤, Safe Area, 넓은 화면 복원과 포커스 노출을 유지한다. 변경 후 부모 크기와 RenderTexture 크기 모두 재계산한다.
- 탐험 컨트롤러의 treePanel 참조와 트리 노드·선 연결 및 최신 위치 정책은 보존한다. 이름 검색에 의존하지 않고 저장된 참조를 갱신한다.

## 6. 상태와 표시의 책임 분리

아래 이름은 신규 설계를 설명하기 위한 제안이며 기존 동등 기능이 있으면 재사용한다. 구현 후 문서에는 실제 이름을 기록한다.

| 책임 | 배치 방향 |
| --- | --- |
| 보유 주사위 목록과 변경 알림 | 기존 Application/Dice의 TemporaryPlayerDiceState를 확장한다. |
| 행동별 결과 스냅샷·중복 실행 방어·기록 | Application 계층에서 세션·전투 수명과 연결한다. |
| 보유 상태 → 표시 데이터 바인딩 | Application 계층의 명시적 Binder가 담당한다. |
| 모델·카메라·애니메이션·결과 표시 | UI 계층에 순수 표시 데이터와 읽기 전용 결과를 전달한다. |
| 면의 확률과 효과 수치 | 기존 Gameplay/Dice가 계속 소유한다. |

### 보유 목록

초기 설정과 실행 중 추가·제거가 같은 상태 모델을 사용하게 한다. 각 보유 인스턴스에 안정적인 ID를 두고, 동일 형태·동일 정의의 여러 주사위를 구분한다. 콘텐츠 정의 ID와 보유 인스턴스 ID를 혼동하지 않는다.

추가 시 구성 검증과 복사를 완료한 후 목록을 원자적으로 갱신한다. 잘못된 면 구성·중복 인스턴스 ID는 목록을 훼손하지 않고 거부한다. 없는 인스턴스 제거는 명확한 실패 결과로 처리한다. 읽기 전용 스냅샷과 변경 알림을 제공하고 매 프레임 전체 보유 목록을 조회하지 않는다.

추가·제거는 다음 UI 갱신에서 반영한다. 모델 로드가 지연되면 대기·대체 표시를 사용하고 실제 보유 여부를 왜곡하지 않는다. 마지막 주사위를 제거하면 빈 상태를 표시하며, 기존의 보유 0개일 때 행동 불가 정책을 유지한다. 과거 기록은 제거와 무관하게 조회 가능하다.

행동 수락 시 참여 주사위와 면 구성을 스냅샷으로 고정한다. 진행 중 추가된 주사위는 다음 행동부터 참여한다. 진행 중 제거된 주사위는 보유 표시에서 제거하되 이미 수락한 행동의 결과에서 제외하지 않는다. 삭제된 View에 대한 애니메이션 콜백은 무효화하고 전체 결과 적용은 유지한다.

## 7. 3D 표시와 다면체 확장

전용 Camera가 RenderTexture에 주사위 Stage를 렌더링하고 OwnedDicePanel의 RawImage가 표시하는 구조를 기본으로 한다. 이 방식은 카메라 출력을 텍스처로 받는 공식 API에 기반한다: [Unity Camera.targetTexture](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Camera-targetTexture.html).

- 카메라·조명·모델의 표시 범위를 격리하여 메인 화면의 다른 객체나 조명과 섞이지 않게 한다. 새 AudioListener를 추가하지 않는다. 별도 레이어가 필요하면 기존 사용 상태를 확인하고 최소한으로 설정한다.
- RenderTexture 해상도는 표시 크기와 품질 설정에 맞추되 상한을 둔다. 리사이즈마다 매 프레임 재할당하지 않는다. 패널 비활성·Scene 해제 시 리소스를 반환한다.
- Stage와 모델의 수명은 표시 계층이 소유한다. 모델 로드 실패·그래픽 리소스 복구·낮은 품질에서도 결과 판정과 전투를 유지한다.
- 실제 메시로 D4·D6·D8을 제공하고, 형태 정의에서 모델 Prefab, 면 인덱스 매핑, 정지 자세와 시각 효과 루트를 교체할 수 있게 한다. 후속 다면체 추가에 중앙 switch 문 수정을 필수로 만들지 않는다.
- 기하학적 면·삼각형 순번이나 표시 숫자로 FaceIndex를 추정하지 않는다. 같은 효과·수치가 반복되어도 다른 면 인덱스로 매핑한다.
- D6·D8은 결과 면을 위쪽 및 카메라에서 식별 가능한 방향으로 놓는다. D4는 상단 평면이 존재한다고 가정하지 않고 결과별 정지 자세와 읽기 규칙을 정의한다. 예를 들어 아래쪽 면에 대응하는 결과를 위쪽 꼭짓점 주변의 반복 표기로 읽도록 구성하고 결과 라벨로 보강한다.
- 기본 면 표시는 기존 Attack/Heal과 Amount를 일치시킨다. 숫자 점만으로 효과를 구분하지 않는다. 형태별 면 매핑을 모두 검증하고 모델 변경 시 오류를 발견할 수 있게 한다.
- 미지원 형태·잘못된 매핑은 다른 주사위로 위장하지 않고 이름·효과·수치의 대체 표시와 진단을 제공한다. D4·D6·D8은 정상 자산을 반드시 제공한다.

모든 주사위는 한 화면 안에 그리드 또는 정돈된 배치로 놓고, 개수와 가용 크기를 기준으로 모델 크기·간격·카메라 구도를 조절한다. 페이지 분할이나 일부 주사위의 임의 생략은 하지 않는다. 너무 작아진 면의 가독성은 결과 모달로 보완한다. 실제 지원 가능한 보유 규모는 측정하고, 한계가 드러나면 임의 제한을 추가하지 말고 근거와 필요한 제품 결정을 보고한다.

모델 Root는 배치, VisualRoot는 굴림·효과를 담당한다. 굴림은 회전·작은 도약·착지 등으로 구현할 수 있으나 결과 면으로 정확히 정지해야 한다. 연출용 난수는 Gameplay 난수와 분리한다. 지속 시간과 강도는 개발자 설정으로 제공하고 모션 감소 시 짧은 전환 또는 즉시 결과 자세를 지원한다.

## 8. 행동 처리와 실패·수명주기

```text
행동 수락 및 잠금
→ 참여 주사위·전투·노드 식별자 고정
→ 기존 확률로 각 주사위 결과를 정확히 한 번 생성
→ 모든 참여 주사위의 연출 완료 또는 실패 대체 완료
→ 기존 ExecuteTurn을 한 번 실행
→ 결과 기록·Story·체력·전투 종료 상태 갱신
→ 설정에 따라 결과 모달 표시
→ 실행 및 모달 상태에 따라 다음 행동 가능 여부 갱신
```

- 기존 TryExecute의 동기 반환값은 행동 수락 여부로 유지할 수 있으나 비동기 완료와 혼동하지 않게 호출자·테스트를 갱신한다. 실행 작업의 예외를 관찰하고 async void로 처리 실패를 유실하지 않는다.
- 정상 연출에서는 모든 주사위가 정지한 후 결과를 적용한다. 타임아웃·모델 누락·표시 실패에서는 확정 결과를 대체 표시하고 동일 결과로 완료한다. 재추첨하거나 효과를 두 번 적용하지 않는다.
- 렌더러 콜백에만 전투 완료를 맡기지 않는다. 숨김, Scene 해제, 바인딩 해제, 재진입 시 수락된 행동은 해당 세션·전투에 최대 한 번 적용되도록 응용 계층이 소유한다. UI 소멸만으로 확정된 행동을 취소해 다시 추첨할 수 없어야 한다.
- 새 세션·새 전투로 바뀐 뒤 이전 비동기 완료가 새 상태에 적용되지 않게 세션·Run·Node·행동 ID 또는 동등한 세대 검증을 한다. 종료된 세션에는 이전 작업을 이식하지 않는다.
- 전투 적용 자체가 실패하면 무조건 재시도하지 않는다. 이미 적용된 변경 여부를 보존하고 오류를 보고한다. 연출 실패와 도메인 처리 실패를 구분한다.
- 기존 적 행동 순서, 적 사망 후 공격 건너뛰기와 회복 처리, 체력·Story 및 CombatFinished 알림의 단일 발생을 유지한다. 알림이 노드 진행을 유발하더라도 기록은 원래 전투 노드에 귀속한다.

## 9. 결과 모달과 기록 정책

기본 자동 표시 설정은 켜짐이다. 런타임에서 읽고 변경할 수 있는 설정 경계를 제공하여 추후 옵션 UI를 연결한다. 자동 표시를 꺼도 판정·전투 적용·Story·기록은 동일하고 결과 보기 버튼은 계속 사용할 수 있어야 한다. 옵션 영구 저장 화면은 이번에 구현하지 않는다.

공통 모달의 CustomContent 페이지로 결과 화면을 추가한다. 자동 표시와 수동 다시 보기는 동일한 읽기 전용 기록 모델을 사용하며, 모달의 열기·닫기·재시도는 결과 판정이나 ExecuteTurn을 호출하지 않는다.

- 완료 후 최신 행동을 우선 보여주며 보관 범위 내 이전 행동도 목록·스크롤 등으로 조회할 수 있게 한다.
- 각 행동의 순서, 전투 노드 식별 정보, 각 주사위의 당시 이름·형태·결과 효과·수치, 실제 적용량·건너뛰기 사유, 적 행동과 전투 종료 결과를 구분한다. 결과 기록은 현재 주사위 정의를 다시 조회해 복원하지 않는다.
- 확인 버튼, 닫기 버튼이 있다면 동일한 닫기 처리, 바깥 클릭·탭으로 닫는다. 창 내부 스크롤·클릭은 닫기로 처리하지 않는다. 확인 버튼은 키보드·컨트롤러 초기 포커스를 받는다.
- 모달이 열린 동안 직접 명령 호출도 다음 행동을 실행할 수 없게 한다. 버튼만 비활성화하는 것으로 끝내지 않는다. 닫으면 유효한 원래 버튼에 포커스를 복원한다.
- 다른 모달이 이미 열려 있으면 강제로 덮어쓰지 않는다. 현재 세션에서 유효한 자동 표시 요청을 직렬화하거나 기존 창 종료 후 표시한다. 오래된 세션의 요청은 폐기하며 재시도 큐가 무한 누적되지 않게 한다.
- 표시 실패는 기록을 삭제하지 않는다. Story의 결과 사실을 유지하고 결과 보기로 다시 열 수 있게 한다. 로딩 실패가 영구적인 입력 잠금을 만들지 않게 한다.
- 보유 0개여도 기록이 있으면 결과 보기 버튼은 사용 가능하다. 기록이 없으면 접근 가능한 빈 상태를 명확히 표시한다.

### 기본 모드: 현재 또는 가장 최근 전투 노드

새 전투 노드에 진입하면 조회 대상은 새 전투로 전환하고, 해당 노드에서 완료된 모든 행동을 순서대로 축적한다. 첫 행동 전에는 해당 전투의 결과 없음 상태를 표시한다. 전투 종료 및 비전투 노드 이동은 기록을 삭제하지 않는다. 다음 전투 진입 전까지 가장 최근 전투 기록을 유지한다. 같은 노드의 UI 재바인딩이나 Scene 재진입만으로 초기화하지 않는다.

### 개발자 선택 모드: 세션 최근 전투 기록

현재 전투와 이전 전투를 구분하여 조회할 수 있게 한다. 보관할 최근 전투 수는 개발자 설정으로 제공하며 가장 오래된 완료 전투부터 정리한다. 현재 전투 기록을 보관 수 제한 때문에 중간 삭제하지 않는다. 게임 재시작·세션 교체 시 기록은 초기화하며 영구 저장은 제외한다.

두 모드는 같은 기록 모델과 조회 API를 사용한다. 보관 정책이 삭제한 기록은 설정 전환으로 복원할 수 없으므로 설정 설명에 명시한다. 임의로 매 프레임 정책을 다시 적용하거나 UI의 OnEnable에서 기록을 초기화하지 않는다.

## 10. 주요 수정 경로와 신규 자산 위치

아래 기존 경로는 문서 작성 시 확인했다.

- `Assets/Scenes/TMP_MainScene.unity`
- `Assets/TxTRPG/UI/Runtime/ResponsiveMainScreen.cs`
- `Assets/TxTRPG/UI/Runtime/FlexibleLayoutPanel.cs`
- `Assets/TxTRPG/UI/Runtime/FlexibleLayoutItem.cs`
- `Assets/TxTRPG/Application/Runtime/Dice/TemporaryDiceConfiguration.cs`
- `Assets/TxTRPG/Application/Runtime/Dice/TemporaryPlayerDiceState.cs`
- `Assets/TxTRPG/Application/Runtime/Dice/TemporaryDiceRollMenuController.cs`
- `Assets/TxTRPG/Application/Runtime/Dice/DiceRollStoryFormatter.cs`
- `Assets/TxTRPG/Application/Runtime/Players/PlayerSessionHost.cs`
- `Assets/TxTRPG/UI/Runtime/Windows/GameWindowService.cs`
- `Assets/TxTRPG/UI/Runtime/Windows/ModalWindowHost.cs`
- `Assets/TxTRPG/UI/Runtime/Windows/GameWindowSystem.cs`

신규 표시 코드는 `Assets/TxTRPG/UI/Runtime/Dice/`, 응용 코드는 기존 `Assets/TxTRPG/Application/Runtime/Dice/`에 두는 방향을 사용한다. 신규 자산의 예정 경로는 `Assets/TxTRPG/UI/Prefabs/Dice/`와 `Assets/TxTRPG/UI/Styles/Dice/`이다. 이 경로와 타입은 생성 전에 현재 관례와 충돌 여부를 확인한다. 실제 생성한 경로와 담당 클래스를 완료 문서에 기록한다.

필수 Prefab 내부 참조는 자산에 완성하고 세션 서비스는 명시적 바인딩으로 연결한다. 런타임 이름 검색이나 중복 세션 생성으로 참조 누락을 숨기지 않는다. 보유 데이터와 결과 저장을 UI GameObject의 생존에 종속시키지 않는다.

## 11. 적용과 검증

Editor 연결·컴파일 상태·미저장 변경을 확인하고 기존 사용자 변경을 보존한다. 필요한 Scene·Prefab·설정만 적용하며 전체 Scene 생성이나 무관한 Prefab 재생성으로 대체하지 않는다. 임시 적용 메뉴가 필요하면 목적·범위·재실행 가능 여부·저장 여부를 문서화한다. 새로운 영구 생성기가 실제로 필요할 때만 프로젝트의 등록 규칙을 따른다.

기존 테스트 기준 경로:

- `Assets/TxTRPG/Gameplay/Tests/Editor/DiceTests.cs`
- `Assets/TxTRPG/Application/Tests/Editor/TemporaryPlayerDiceStateTests.cs`
- `Assets/TxTRPG/UI/Tests/Editor/FlexibleLayoutPanelTests.cs`
- `Assets/TxTRPG/UI/Tests/PlayMode/`

필수 검증은 다음과 같다.

1. 고정 난수 소스로 D4·D6·D8의 모든 FaceIndex를 강제하고 확정 결과, 정지 자세, 모달과 Story의 효과·수치가 모두 일치함을 확인한다. 동일 효과·수치를 가진 서로 다른 면도 검증한다.
2. 0개·1개·여러 개·같은 형태 여러 인스턴스, 실행 중 추가·제거, 잘못된 구성과 중복 ID, 마지막 주사위 제거 및 Scene 재바인딩을 확인한다.
3. 굴림 중 연타, 직접 명령 재호출, 모달이 열린 동안의 실행, 표시 실패·타임아웃·화면 숨김·Scene 전환·새 세션에서 결과가 누락되거나 중복 적용되지 않음을 검증한다.
4. 전투 A의 복수 행동 → 비전투 노드 → 전투 B 진입 → 첫 행동의 기록 범위를 확인한다. 세션 최근 기록 모드, 보관 수 정리, 수동 재열기, 보유 주사위 제거 후 과거 결과 조회를 검증한다.
5. 자동 표시 켜짐·꺼짐, 확인 및 바깥 클릭·탭, 모달 내부 클릭·스크롤, 실패 재열기와 포커스 복원을 마우스·키보드·컨트롤러·터치로 확인한다.
6. AppScene의 기존 시작 경로로 TMP_MainScene에 진입한다. 넓은 화면, 좁은 화면, 휴대폰·태블릿 종횡비, Safe Area와 회전에서 트리·전체 스크롤·주사위·결과 버튼이 접근 가능해야 한다.
7. 적은 수와 많은 수의 주사위로 겹침·클리핑·카메라 구도·면 표시를 검사한다. 모든 보유 주사위를 화면에 포함하고 작은 면의 결과는 모달에서 읽을 수 있어야 한다.
8. 프레임 시간·메모리·렌더 텍스처 수명과 반복 Scene 진입을 측정한다. 정적 코드 검토만으로 모바일 성능을 보장하지 않는다. 화면 밖·비활성 상태에서는 불필요한 지속 렌더링을 줄이되 수락된 행동의 완료는 유지한다.
9. 관련 Edit Mode·Play Mode 테스트와 사용 가능한 대상 빌드 컴파일을 수행한다. 확인하지 못한 실제 기기·빌드는 미검증으로 명시한다. 직렬화 diff에서 기존 트리·모달·사용자 설정의 소실이 없는지 확인한다.

## 12. 문서와 최종 보고

구현 후 실제 구조를 설명하는 `DOCS/architecture/owned-dice-presentation.md`를 신규 작성하고 DOCS/README.md에 연결한다. `DOCS/architecture/project-structure.md`, `dice-domain.md`, `temporary-combat.md`, `exploration-history-tree.md`, `quick-items-and-game-windows.md`의 해당 내용을 갱신한다. 신규 폴더·타입·결과 처리 순서·보관 정책을 현재 구현으로 명확하게 기록한다.

`DOCS/development/workflows.md`에는 주사위 형태·면 매핑 추가, 초기 보유 설정, 런타임 보유 변경 API, 높이·모션·자동 모달·기록 범위 설정, 자산 적용과 검증 절차를 기록한다.

최종 보고는 구현 완료, Editor 적용 완료, 남은 수동 적용과 실제 수행한 검증을 구분한다. 수동 Tools 단계가 남으면 `사용자가 수행할 적용 절차`에 정확한 메뉴 경로·순서·사전 조건·저장 여부·성공 기준을 text 코드 블록으로 제공한다. 남은 단계가 없으면 없다고 명시한다. 모델 교체·새 다면체 추가와 기록 정책 변경 위치를 개발자가 찾을 수 있도록 링크를 제공한다.