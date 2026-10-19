# Candidate_MainScene MiddleArea 요소 토글 버튼과 디버그 이벤트 판정

## 1. 목적과 확정 사항

`Candidate_MainScene`의 `MiddleArea` 상단에 다중 선택이 가능한 토글 버튼 줄(이하 "요소 토글 바")을 배치하고, 우선 "룬" 버튼 하나만 추가한다. 선택된 버튼(요소)을 바탕으로 확률에 따라 이벤트(전투·이벤트·휴식)를 발생시키는 판정 규칙을 만들고, 개발 중에 확률을 조정하고 판정을 실행하는 디버그 utility와, 결과를 알려 주는 모달 창을 구현한다.

이 문서는 후속 구현 지침이며, 적용이나 실행 검증을 완료했다는 기록이 아니다.

### 1.1 사용자가 확정한 사항 (2026-10-19)

| 항목 | 확정 내용 |
| --- | --- |
| 버튼 배치 | `MiddleArea`의 상단에 버튼들을 배치한다. 지금은 "룬" 버튼 하나만 추가하고, 이후 다른 버튼을 코드 수정 없이(설정 자산에 항목을 추가하는 방식으로) 늘릴 수 있어야 한다. |
| 선택 방식 | 버튼은 토글로 동작하며, 여러 버튼을 동시에 선택할 수 있다. |
| 확률의 독립성 | "이벤트 자체가 발생하는가"를 정하는 확률과 "선택된 버튼 중 어떤 요소에 대한 이벤트인가"를 정하는 확률은 서로 독립적이다. |
| 요소 조합 결정 | 요소별 독립 포함 방식이다. 선택된 요소마다 포함 확률(기본 50%)로 독립 판정하고, 하나도 포함되지 않으면 선택된 요소 중 하나를 균등하게 추첨한다. 따라서 결과에는 선택된 요소가 항상 하나 이상 포함된다. |
| 이벤트 종류 결정 | 전투·이벤트·휴식 각각의 가중치(기본 1:1:1)를 설정 자산에 두고 가중치 비율로 고른다. 디버그 패널에서 슬라이더 세 개로 가중치를 바꿀 수 있다. |
| 디버그 utility | 확률(발생 확률, 요소 포함 확률, 종류 가중치)을 설정하는 슬라이더와 이벤트를 발생시키는 버튼을 둔다. |
| 미발생 처리 | 발생 확률 판정에 실패하거나 선택된 버튼이 없으면 모달을 열지 않고, 디버그 패널 안에 결과 문구를 표시한다. 선택된 요소가 없으면 판정 자체를 하지 않는다. |
| 디버그 노출 범위 | Editor와 Development Build에서만 표시·사용할 수 있다. 일반 Release 빌드에서는 디버그 오브젝트가 비활성화되어 보이지 않고 사용할 수 없다. 판정 규칙 자체는 공용 코드로 남긴다. |
| 결과 모달 | 이벤트가 발생하면 모달 창에 `[버튼 요소] : [전투/이벤트/휴식]` 형식으로 결과를 표시한다(예: `룬 : 전투`). 모달 창 바깥 영역을 클릭(탭)하면 닫힌다. |

### 1.2 이 문서가 정한 기본값

사용자 답변 이후 결과에 큰 영향이 없어 지침서가 정한 값이다. 구현자는 이 값을 그대로 사용하고, 바꿀 필요가 생기면 근거와 함께 보고한다. 길이는 Canvas 단위이다.

| 항목 | 기본값 |
| --- | --- |
| 기본 확률 | 이벤트 발생 확률 0.5, 요소 포함 확률 0.5, 종류 가중치 전투 1·이벤트 1·휴식 1 |
| 슬라이더 범위 | 발생 확률과 요소 포함 확률은 0~1(표시는 `0%`~`100%`, 1% 단위), 종류 가중치는 0~10(0.1 단위). 각 슬라이더 옆에 현재 값을 텍스트로 표시한다. |
| 가중치가 모두 0인 경우 | 종류를 고를 수 없으므로 판정하지 않고 디버그 패널에 `종류 가중치가 모두 0입니다`를 표시한다. |
| 판정 해상도 | 확률 판정은 `IRandomIndexProvider.NextIndex(10000) < round(p × 10000)`으로 한다. p = 0이면 항상 실패, p = 1이면 항상 성공한다. |
| 결과 문구 형식 | `{요소 표시 이름들} : {종류 표시 이름}`. 여러 요소는 설정 자산의 순서대로 `, `로 잇는다(예: `룬, 유물 : 휴식`). 종류 표시 이름은 `전투`, `이벤트`, `휴식`이다. |
| 모달 제목 | `이벤트 발생` |
| 모달 닫기 | 바깥 클릭·탭, `Cancel` 동작, 기존 닫기 버튼 모두로 닫힌다. 닫기 버튼은 키보드·게임패드 사용자를 위해 유지한다. 닫히면 포커스는 판정을 실행한 디버그 버튼으로 돌아간다. |
| 선택 상태 수명 | Scene 수명 동안만 유지하고 저장하지 않는다. Scene이 다시 로드되면 모두 미선택 상태로 시작한다. |
| 초기 선택 상태 | 모든 버튼이 미선택이다. |
| 선택 표시 | 색만으로 구분하지 않는다. 선택된 버튼은 체크 표시(Checkmark)와 굵은 테두리를 함께 보인다. |
| 토글 바 높이 | 고정 64. 버튼은 최소 너비 96, 최소 높이 48(터치 목표 크기), 버튼 간격 8, 좌우 안쪽 여백 8이다. 버튼 수가 영역 너비를 넘으면 가로 스크롤로 넘긴다(줄바꿈하지 않음). |
| 디버그 패널 위치 | `Canvas/OverlayLayer`의 오른쪽 위. 폭 320, 내용에 맞는 높이. 레이아웃 영역(`MainArea`)의 크기에는 영향을 주지 않는다. 머리글 `DEBUG · 이벤트 판정`과 접기·펼치기 버튼을 둔다. 기본은 펼친 상태이다. |
| 디버그 결과 문구 | 판정 실패: `이벤트 없음 (발생 확률 판정 실패)`, 선택 없음: `선택된 요소가 없습니다`, 발생 시: 모달에 표시한 문구와 같은 내용을 패널에도 남긴다. |

## 2. 작성 시점에 확인한 현재 상태

### 2.1 `Candidate_MainScene`

```text
Candidate_MainScene
├── EventSystem
├── Main Camera
├── Canvas                     Canvas, CanvasScaler(Constant Pixel Size), GraphicRaycaster
│   ├── MainArea               FlexibleLayoutPanel.prefab 인스턴스(1:3:1, 너비 720 미만이면 세로)
│   │   └── ContentLayer
│   │       ├── LeftArea       FlexibleLayoutPanel.prefab 인스턴스 + FlexibleLayoutItem(Weighted 1)
│   │       │   └── ContentLayer/RuneGrid   RuneGridPanel.prefab 인스턴스(커밋 전 작업 중)
│   │       ├── MiddleArea     FlexibleLayoutPanel.prefab 인스턴스 + FlexibleLayoutItem(Weighted 3), 콘텐츠 없음
│   │       └── RightArea      FlexibleLayoutPanel.prefab 인스턴스 + FlexibleLayoutItem(Weighted 1)
│   └── OverlayLayer           SlotReelDock 등 레이아웃 밖의 오버레이
└── Directional Light
```

- `Candidate_MainScene`에는 `ModalWindowHost`와 `GameWindowService`가 없다.
- `FlexibleLayoutPanel`의 `ContentLayer` 배치는 `FlexibleContentLayoutGroup`이 담당하며, `Fixed Axis`, `Axis Policy`, `Breakpoint`와 `FlexibleLayoutItem`(Weighted/Fixed)로 크기를 나눈다([FlexibleLayoutPanel](../architecture/flexible-layout-panel.md)).
- 작성 시점에 `LeftArea` 룬 Grid 작업([candidate-main-scene-rune-grid.md](candidate-main-scene-rune-grid.md))이 커밋되지 않은 상태로 작업 트리에 있다. 이 작업과 같은 Scene을 수정하므로, 그 변경을 되돌리거나 덮어쓰지 않는다.

### 2.2 재사용할 수 있는 요소

| 대상 | 활용 |
| --- | --- |
| `ModalWindowHost`(`Assets/TxTRPG/UI/Runtime/Windows/ModalWindowHost.cs`) | `windowRect` 바깥의 배경을 눌렀다 뗀 클릭만 닫기로 처리하고(드래그·취소된 터치 제외), `Cancel`로도 닫는다. `CanvasGroup`으로 표시와 입력 차단을 전환한다. |
| `GameWindowService` | 페이지 ID로 모달을 열고(`OpenAsync`), 닫을 때 `returnFocus`로 포커스를 돌려준다. `Opened`·`Closed` 이벤트와 `IsOpen`을 제공한다. |
| `MessageGameWindowPage` | 문구 하나를 표시하는 모달 페이지. `SetMessage`로 내용을 바꾼다. |
| `Assets/TxTRPG/UI/Prefabs/ModalWindowHost.prefab` | 기존 공용 모달 구성. 설정·상태 페이지 등 이 기능과 무관한 페이지를 포함하므로 그대로 쓰지 말고, 구조와 설정을 참고한다. |
| `IRandomIndexProvider`, `SystemRandomIndexProvider`(`Assets/TxTRPG/Gameplay/Runtime/Common/RandomIndexProvider.cs`) | Gameplay 공용 난수 계약. 테스트에서는 정해진 값을 돌려주는 구현으로 바꿔 끼운다. |
| `UiShapeGraphic`(`Assets/TxTRPG/UI/Runtime/Shapes/`) | 텍스처 없이 버튼 배경·테두리를 그릴 수 있다. |
| `TxTRPGEditorMenuPaths.Application`(`Assets/TxTRPG/Editor/Common/Menu/TxTRPGEditorMenuPaths.cs`) | 임시 메뉴 경로의 기준(`Tools/TxT RPG/Application/`) |

## 3. 선행 조건

- 작업 시작 전에 `git status --short`를 기록한다. 작성 시점에는 `AGENTS.md`, `AddressableAssetSettings.asset`, `LiberationSans SDF - Fallback.asset`, `DefaultSceneTransitionProfile.asset`, `NotoSansKR-Fallback.asset` 수정과 룬 Grid 작업의 변경이 커밋되지 않은 상태로 있었다. 이 변경들을 되돌리거나 함께 정리하지 않는다.
- Unity Editor의 연결, 컴파일 상태, 열린 Scene의 미저장 변경을 확인한다. 미저장 변경이 있는 Scene은 저장하거나 덮어쓰지 말고 사용자에게 알린다.
- 변경이 Gameplay·Application·UI 세 어셈블리와 Scene·새 Prefab·새 설정 자산에 걸치므로, 편집 전에 `unity-impact-scout`로 `Candidate_MainScene`의 `MiddleArea`·`OverlayLayer`, `ModalWindowHost`, `GameWindowService`, `MessageGameWindowPage`의 영향(호출자, 직렬화 참조, 테스트, 문서)을 조사한다. 결과는 소스로 다시 확인한다.
- 다음 문서를 먼저 읽는다: [FlexibleLayoutPanel](../architecture/flexible-layout-panel.md), [빠른 아이템과 게임 창](../architecture/quick-items-and-game-windows.md), [시각 효과 준비](../development/visual-effect-readiness.md), [Bag 빈 상태와 모달 바깥 클릭 닫기](inventory-empty-state-and-modal-outside-close.md).

## 4. 작업 범위

### 4.1 포함

1. Gameplay 어셈블리에 요소 선택 상태와 이벤트 판정 규칙(순수 C#)을 추가한다.
2. Application 어셈블리에 요소 목록 설정 자산, 판정 기본값 설정 자산, Scene 단위 판정 컨트롤러를 추가한다.
3. UI 어셈블리에 요소 토글 바, 디버그 패널, 개발 빌드 전용 표시 구성 요소를 추가한다.
4. `EncounterElementToggleBar.prefab`, `EncounterElementToggle.prefab`, `EncounterDebugPanel.prefab`, `EncounterResultModal.prefab`과 두 설정 자산(`EncounterElementCatalog.asset`, `EncounterRollConfiguration.asset`)을 만든다.
5. `Candidate_MainScene`의 `MiddleArea` 상단에 토글 바를, `OverlayLayer`에 디버그 패널과 결과 모달을 배치하고 연결한다.
6. Edit Mode 테스트, Play Mode 확인용 임시 메뉴, 문서를 추가·갱신한다.

### 4.2 제외

- 실제 전투·휴식·랜덤 이벤트의 실행(전투 시작, 체력 회복, 탐험 이벤트 연결)은 하지 않는다. 판정 결과는 모달 표시와 결과 이벤트(`RollCompleted`) 발행까지만 한다. 기존 `ExplorationRunState`·`ExplorationEventController`와 연결하지 않는다.
- 디버그 버튼 이외의 실제 판정 시점(턴 진행, 이동, 노드 선택 등)은 정하지 않는다. 컨트롤러의 공개 판정 메서드만 확장 지점으로 둔다.
- "룬" 이외의 요소 버튼은 추가하지 않는다. 확장 방식만 검증한다.
- 요소별 종류 가중치, 요소 조합별 전용 이벤트 내용은 만들지 않는다.
- 선택 상태와 디버그 확률 값의 저장, 세이브 데이터 연결은 하지 않는다.
- `MiddleArea`의 나머지 영역 콘텐츠, `LeftArea`·`RightArea`, `SlotReelDock`, `TMP_MainScene`, 기존 `ModalWindowHost.prefab`·`GameMenuScreen.prefab`은 바꾸지 않는다.
- `Candidate_MainScene`의 `CanvasScaler`와 Safe Area 정책은 바꾸지 않는다.
- 버튼 아이콘, 등장·선택 애니메이션 같은 시각 효과는 만들지 않는다.
- 커밋과 푸시는 하지 않는다.

## 5. 구현 요구 사항

### 5.1 Gameplay 판정 규칙 (`TxTRPG.Gameplay`, `Assets/TxTRPG/Gameplay/Runtime/Encounters/`, 네임스페이스 `TxTRPG.Gameplay.Encounters`)

Unity 타입에 의존하지 않는 순수 C#으로 작성한다.

| 타입 | 책임 |
| --- | --- |
| `EncounterKind` | `Combat`, `Event`, `Rest` 열거형. 직렬화되는 정수 값을 고정한다(0, 1, 2). |
| `EncounterRollSettings` | 불변 값. `OccurrenceChance`, `ElementInclusionChance`(0~1로 고정, NaN은 0), `CombatWeight`·`EventWeight`·`RestWeight`(음수·NaN은 0). `WithX` 형식의 복사 메서드로 일부 값만 바꾼다. |
| `EncounterElementSelection` | 선택된 요소 ID 집합. `IsSelected`, `SetSelected(id, bool)`, `Toggle(id)`, `Clear`, 순서 있는 `Snapshot`, `Changed` 이벤트. ID는 앞뒤 공백을 제거하고 대소문자를 구분한다. 빈 ID는 거부한다. 값이 실제로 바뀔 때만 `Changed`를 발생시킨다. |
| `EncounterRollOutcome` | `Occurred`, `NoElementsSelected`, `ChanceFailed`, `NoKindAvailable` 열거형 |
| `EncounterRollResult` | `Outcome`, `Kind`(발생 시에만 의미), `ElementIds`(발생 시 1개 이상, 입력 순서 유지) |
| `EncounterRoller` | `Roll(IReadOnlyList<string> selectedElementIds, EncounterRollSettings settings)`. 생성자에서 `IRandomIndexProvider`를 받는다. |

`EncounterRoller.Roll`의 순서는 다음과 같으며, 난수 호출 순서도 테스트로 고정한다.

1. 선택된 요소가 없으면 난수를 쓰지 않고 `NoElementsSelected`를 돌려준다.
2. 종류 가중치의 합이 0이면 난수를 쓰지 않고 `NoKindAvailable`을 돌려준다.
3. 발생 확률로 한 번 판정한다. 실패하면 `ChanceFailed`를 돌려준다.
4. 선택된 요소마다 입력 순서대로 요소 포함 확률로 판정한다. 하나도 포함되지 않으면 `NextIndex(선택 수)`로 하나를 고른다.
5. 종류 가중치로 하나를 고른다. 가중치를 1/1000 단위 정수로 바꿔(`round(w × 1000)`) 누적 구간을 만들고 `NextIndex(합계)`로 고른다. 0 가중치인 종류는 고르지 않는다.
6. `Occurred` 결과를 돌려준다.

3단계(발생 여부)와 4·5단계(요소·종류)는 서로 다른 난수 호출을 쓰며, 앞 단계의 결과가 뒤 단계의 확률을 바꾸지 않는다.

### 5.2 Application 설정 자산과 컨트롤러 (`TxTRPG.Application`, `Assets/TxTRPG/Application/Runtime/Encounters/`, 네임스페이스 `TxTRPG.Application.Encounters`)

| 타입 | 책임 |
| --- | --- |
| `EncounterElementCatalog` (ScriptableObject) | 요소 항목 목록: `Id`(예: `rune`), `DisplayName`(예: `룬`). 목록 순서가 버튼 순서와 결과 문구 순서이다. `Validate`가 빈 ID·중복 ID·빈 이름을 보고한다. 잘못된 항목은 버튼으로 만들지 않고 경고를 한 번 남긴다. |
| `EncounterRollConfiguration` (ScriptableObject) | `EncounterRollSettings`의 기본값(1.2절)을 저장한다. `CreateSettings()`로 값을 만든다. |
| `EncounterResultFormatter` | 정적 함수. `Format(EncounterRollResult, EncounterElementCatalog)` → `룬 : 전투`. 카탈로그에 없는 ID는 ID 문자열 그대로 표시한다. 종류 표시 이름(`전투`, `이벤트`, `휴식`)도 여기서 정한다. |
| `EncounterRollController` (MonoBehaviour) | Scene 단위 소유자. 카탈로그·설정 자산·토글 바·결과 모달(`GameWindowService`)·디버그 패널(선택)을 직렬화 참조로 받는다. `EncounterElementSelection`과 현재 `EncounterRollSettings`를 보유한다. |

`EncounterRollController`의 요구 사항:

1. `Awake`에서 카탈로그로 토글 바의 표시 데이터를 만들고, 설정 자산으로 현재 설정을 만든다. 필수 참조(카탈로그, 설정 자산, 토글 바)가 없으면 오류를 한 번 남기고 동작을 멈추되 Scene의 다른 기능은 막지 않는다. 결과 모달이 없으면 판정 결과를 `Debug.LogWarning`과 디버그 패널 문구로만 알린다.
2. 토글 바의 선택 변경 이벤트를 받아 `EncounterElementSelection`을 갱신한다. 선택 상태의 원본은 컨트롤러가 가진 `EncounterElementSelection`이며, 토글 바는 이를 표시만 한다.
3. 공개 API: `Selection`(읽기), `CurrentSettings`, `SetSettings(EncounterRollSettings)`, `ResetSettingsToConfiguration()`, `EncounterRollResult RollNow(GameObject returnFocus = null)`, `event Action<EncounterRollResult> RollCompleted`. 후속 실제 판정 시점은 `RollNow`를 호출해 연결한다.
4. `RollNow`는 결과가 `Occurred`이면 결과 모달을 열고(`returnFocus` 전달) `RollCompleted`를 발생시킨다. 그 밖의 결과는 모달을 열지 않고 `RollCompleted`만 발생시킨다. 모달이 이미 열려 있으면 판정하지 않고 경고만 남긴다.
5. 난수 공급자는 기본으로 `SystemRandomIndexProvider`를 쓰고, 테스트용으로 바꿔 끼우는 Editor 전용 설정 메서드(`ConfigureForEditor` 관례)를 둔다.
6. 디버그 패널은 컨트롤러의 공개 API만 사용한다. 컨트롤러는 디버그 패널이 없어도(Release 빌드에서 비활성화되어도) 정상 동작해야 한다.
7. Scene 검색(`FindObjectOfType` 등)과 이름 기반 조회를 하지 않는다.

### 5.3 UI 구성 요소 (`TxTRPG.UI`, `Assets/TxTRPG/UI/Runtime/Encounters/`, 네임스페이스 `TxTRPG.UI.Encounters`)

UI 어셈블리는 Gameplay·Application 타입을 쓰지 않고 표시 데이터와 이벤트만 주고받는다.

| 타입 | 책임 |
| --- | --- |
| `EncounterElementToggleEntry` | 표시 데이터(`Id`, `DisplayName`) |
| `EncounterElementToggleView` | 버튼 하나. Unity `Toggle`(ToggleGroup 없음)을 사용하고 `Bind(entry)`, `SetIsOnWithoutNotify(bool)`, `ValueChanged(id, isOn)` 이벤트를 제공한다. 선택 시 Checkmark와 굵은 테두리를 표시한다. |
| `EncounterElementToggleBar` | `SetEntries(IReadOnlyList<EncounterElementToggleEntry>)`, `SetSelected(id, bool)`(알림 없음), `event Action<string, bool> SelectionChanged`. View를 풀로 재사용하고 줄어든 항목은 비활성화한다. 버튼 사이의 좌우 Navigation을 명시적으로 연결한다(마지막에서 처음으로 순환하지 않음). |
| `EncounterDebugPanel` | 슬라이더 5개(발생 확률, 요소 포함 확률, 전투·이벤트·휴식 가중치)와 값 텍스트, `이벤트 발생` 버튼, `기본값으로` 버튼, 결과 문구 텍스트, 접기·펼치기 버튼. `SetValues(...)`(알림 없음), `event Action<EncounterDebugValues> ValuesChanged`, `event Action RollRequested`, `event Action ResetRequested`, `ShowResult(string)`를 제공한다. 데이터 타입 `EncounterDebugValues`는 다섯 값을 담는 UI 측 구조체이다. |
| `DevelopmentBuildOnly` | 범용 구성 요소. `Awake`에서 `Debug.isDebugBuild`가 false이면 자신의 GameObject를 비활성화한다. Editor와 Development Build에서는 아무것도 하지 않는다. 전처리기 지시문을 기능 코드에 흩뿌리지 않기 위해 이 구성 요소 하나로 판정을 모은다. |

연결은 `EncounterRollController`가 담당한다. 디버그 패널의 `ValuesChanged` → `SetSettings`, `RollRequested` → `RollNow(디버그 버튼)` → 결과 문구 `ShowResult`, `ResetRequested` → `ResetSettingsToConfiguration` 후 `SetValues`. 슬라이더 값은 실행 중 설정에만 반영하고 `EncounterRollConfiguration.asset`을 바꾸지 않는다.

### 5.4 Prefab 계층과 시각 효과 준비

`Assets/TxTRPG/UI/Prefabs/Encounters/EncounterElementToggleBar.prefab`

```text
EncounterElementToggleBar         EncounterElementToggleBar
└── Scroll View                   ScrollRect(가로만, Clamped, Scrollbar 없음), Stretch
    └── Viewport                  RectMask2D, 투명 Image(스크롤 입력 대상)
        └── Content               HorizontalLayoutGroup(간격 8, 좌우 여백 8, 세로 가운데), ContentSizeFitter(가로 Preferred)
```

`Assets/TxTRPG/UI/Prefabs/Encounters/EncounterElementToggle.prefab`

```text
EncounterElementToggle            LayoutElement(최소 너비 96, 최소 높이 48), Toggle, EncounterElementToggleView
├── HitArea                       투명 Image(Toggle의 targetGraphic, 입력 대상), Stretch
└── VisualRoot                    효과 적용 루트(Stretch, 입력 대상 아님)
    ├── Background                UiShapeGraphic 둥근 사각형
    ├── SelectedBorder            UiShapeGraphic 테두리(선택 시 표시)
    ├── Checkmark                 Image 또는 TMP 기호(선택 시 표시)
    └── Label                     TMP_Text(줄바꿈 없음, 자동 크기, 말줄임)
```

`Assets/TxTRPG/UI/Prefabs/Encounters/EncounterDebugPanel.prefab`

```text
EncounterDebugPanel               EncounterDebugPanel, DevelopmentBuildOnly, VerticalLayoutGroup, ContentSizeFitter(세로)
├── Header                        TMP_Text "DEBUG · 이벤트 판정" + CollapseButton
└── Body                          (접으면 비활성)
    ├── OccurrenceRow             Label · Slider · Value
    ├── InclusionRow              Label · Slider · Value
    ├── CombatWeightRow           Label · Slider · Value
    ├── EventWeightRow            Label · Slider · Value
    ├── RestWeightRow             Label · Slider · Value
    ├── Buttons                   RollButton("이벤트 발생"), ResetButton("기본값으로")
    └── ResultText                TMP_Text(줄바꿈)
```

`Assets/TxTRPG/UI/Prefabs/Encounters/EncounterResultModal.prefab`

```text
EncounterResultModal              GameWindowService(pages = [EncounterResultPage])
└── ModalWindowHost               ModalWindowHost, CanvasGroup, 배경 Image(반투명, 바깥 클릭 대상), Stretch
    └── Window                    windowRect, 패널 배경, 가운데 정렬, 너비 min(560, 화면 너비 − 32)
        ├── Title                 TMP_Text "이벤트 발생"
        ├── CloseButton
        ├── Loading / ErrorStateRoot / RetryButton   ModalWindowHost 필수 참조(기존 구성과 동일)
        └── ContentContainer      ModalContentContainer
            └── Pages/EncounterResultPage   MessageGameWindowPage(PageId 고정, 예: "encounter-result")
```

- 효과가 적용될 수 있는 시각 요소는 `VisualRoot` 아래에 두고, 레이아웃·입력 대상(`HitArea`, `Toggle`)과 분리한다([시각 효과 준비](../development/visual-effect-readiness.md)).
- 토글 바와 디버그 패널은 색만으로 상태를 전달하지 않는다(Checkmark, 테두리, 값 텍스트).
- 텍스트는 한국어 글꼴 대체(`NotoSansKR-Fallback`)가 적용된 기존 TMP 설정을 사용한다.
- 네 Prefab은 개발자 소유 자산이며 Prefab Rebuild 배치에 등록하지 않는다([prefab-rebuild-registry.md](../architecture/prefab-rebuild-registry.md)).
- 결과 모달의 `Window` 크기는 좁은 화면(640×1136, 700×400)에서도 문구와 닫기 버튼이 잘리지 않아야 한다.

### 5.5 설정 자산

| 자산 | 경로 | 내용 |
| --- | --- | --- |
| `EncounterElementCatalog.asset` | `Assets/TxTRPG/Application/Configuration/Encounters/` | 항목 1개: `Id = rune`, `DisplayName = 룬` |
| `EncounterRollConfiguration.asset` | 같은 폴더 | 발생 확률 0.5, 요소 포함 확률 0.5, 가중치 1·1·1 |

요소를 추가할 때는 카탈로그에 항목만 추가하면 버튼과 결과 문구에 반영되어야 한다. 이를 Edit Mode 테스트로 확인한다(5.9절).

### 5.6 `Candidate_MainScene` 배치

목표 계층은 다음과 같다.

```text
Canvas
├── MainArea/ContentLayer/MiddleArea                 (ContentLayer: Axis Policy = Fixed, Fixed Axis = Vertical — 인스턴스 Override)
│   └── ContentLayer
│       ├── EncounterElementToggleBar                EncounterElementToggleBar.prefab 인스턴스 + FlexibleLayoutItem(Fixed 64)
│       └── MiddleContentPlaceholder                 FlexibleLayoutPlaceholder.prefab 인스턴스(Weighted 1, 활성)
├── OverlayLayer
│   ├── (기존 SlotReelDock 등)
│   ├── EncounterDebugPanel                          EncounterDebugPanel.prefab 인스턴스(오른쪽 위 앵커, 여백 16)
│   └── EncounterResultModal                         EncounterResultModal.prefab 인스턴스(Stretch, 마지막 형제)
└── EncounterRollController                          빈 GameObject + EncounterRollController(카탈로그·설정·토글 바·모달·디버그 패널 참조)
```

1. `MiddleArea` 인스턴스의 `ContentLayer`를 세로축 고정으로 바꿔 토글 바가 항상 상단에 오게 한다. 현재 `ContentLayer`의 설정 값을 먼저 확인하고, 이미 세로축이면 바꾸지 않는다. `MainArea`·`LeftArea`·`RightArea`의 설정은 바꾸지 않는다.
2. 아래 남는 영역은 활성 상태의 Placeholder가 차지하게 하여, 후속 콘텐츠가 들어갈 자리를 남긴다.
3. 결과 모달은 `OverlayLayer`에서 가장 마지막 형제로 두어 디버그 패널과 `SlotReelDock` 위에 그려지게 한다. 열릴 때 `ModalWindowHost.SetVisible`이 마지막 형제로 옮기는 동작도 유지한다.
4. 작업 시점에 `MiddleArea/ContentLayer`에 다른 자식이 있거나, `OverlayLayer`에 같은 이름의 오브젝트가 있으면 진행을 멈추고 사용자에게 보고한다.
5. 룬 Grid 작업으로 생긴 `LeftArea` 변경과 사용자 변경은 그대로 둔다.

### 5.7 적용 방식

Prefab·자산·Scene 생성은 한 번만 적용하면 되므로 재사용 Builder를 만들지 않는다. Unity Editor 자동화로 직접 적용하는 방법을 우선한다. 자동화를 사용할 수 없으면 `TxTRPGEditorMenuPaths.Application + "Temporary/Apply Candidate Encounter Toggles"` 임시 메뉴를 만들어 사용자가 실행하게 하고, 다음을 지킨다.

- 클래스 주석에 목적, 임시 상태, 재실행 안전성을 기록한다.
- 실행 전 Play Mode·컴파일 중·대상 Scene 미저장 변경 여부를 검사하고, 조건이 맞지 않으면 아무것도 바꾸지 않는다.
- 이미 있는 Prefab과 설정 자산은 덮어쓰지 않는다. Scene에 대상 오브젝트가 이미 있으면 Scene을 바꾸지 않고 그 사실을 로그로 남긴다. 따라서 재실행해도 안전해야 한다.
- 바꾼 자산과 `Candidate_MainScene`만 저장한다.
- 사용자가 적용과 검증을 끝낸 뒤 제거할 수 있다고 문서에 기록한다.

### 5.8 Play Mode 확인용 임시 메뉴

`TxTRPGEditorMenuPaths.Application + "Temporary/Encounter Roll Play Check"` 임시 메뉴를 만든다.

1. Play Mode이고 `Candidate_MainScene`이 현재 콘텐츠이며 `EncounterRollController`를 직렬화 참조 경로(임시 메뉴는 Editor 도구이므로 현재 Scene의 루트 오브젝트에서 컴포넌트를 찾는 방식 허용)로 얻을 수 있을 때만 동작한다. 조건이 맞지 않으면 이유를 오류로 남기고 아무것도 하지 않는다.
2. 단계: (1) 선택 없음으로 `RollNow` → `NoElementsSelected`이고 모달이 닫혀 있는지 → (2) `룬` 선택, 발생 확률 0 → `ChanceFailed`, 모달 닫힘 → (3) 발생 확률 1, 가중치 전투만 1 → `Occurred`, 모달이 열리고 문구가 `룬 : 전투`인지 → (4) 모달을 닫고(`GameWindowService.Close`) 가중치 휴식만 1로 반복해 `룬 : 휴식`인지 → (5) 가중치 모두 0 → `NoKindAvailable` → (6) 설정을 기본값으로 되돌리고 선택을 해제한다.
3. 각 단계 결과를 `ENCOUNTER_ROLL_CHECK` 접두사로 Console에 남긴다.
4. 바깥 클릭으로 닫히는지는 사람이 직접 확인한다(7.2절).

### 5.9 Edit Mode 테스트

| 테스트 클래스(새로 추가) | 어셈블리 | 내용 |
| --- | --- | --- |
| `EncounterRollerTests` | `TxTRPG.Gameplay.Tests.Editor` | 선택 없음·가중치 0에서 난수 미사용, 발생 확률 0/1 경계, 요소 독립 포함(정해진 난수 순서로 부분 집합 결과), 아무것도 포함되지 않을 때 균등 추첨 1개, 결과 요소 순서 유지, 0 가중치 종류 미선택, 발생 판정과 요소·종류 판정의 난수 호출 분리, 설정 값 고정(음수·NaN·범위 밖) |
| `EncounterElementSelectionTests` | `TxTRPG.Gameplay.Tests.Editor` | Toggle·SetSelected·Clear, 값이 바뀔 때만 `Changed`, ID 공백 제거, 빈 ID 거부 |
| `EncounterResultFormatterTests` | `TxTRPG.Application.Tests.Editor` | `룬 : 전투`, 여러 요소의 카탈로그 순서와 `, ` 구분, 카탈로그에 없는 ID, 세 종류 표시 이름 |
| `EncounterElementCatalogTests` | `TxTRPG.Application.Tests.Editor` | 빈 ID·중복 ID·빈 이름 보고, 저장된 `EncounterElementCatalog.asset`이 `rune`/`룬` 하나를 갖고 검증을 통과, 항목을 추가하면 토글 표시 데이터가 늘어나는지 |
| `EncounterRollControllerTests` | `TxTRPG.Application.Tests.Editor` | 정해진 난수로 `RollNow`: 발생 시 모달 페이지 문구 설정과 `RollCompleted`, 미발생 시 모달 미개방, 모달이 열려 있을 때 판정 거부, 디버그 패널 없이 동작, 필수 참조 누락 시 예외 없이 중단 |
| `EncounterElementToggleBarTests` | `TxTRPG.UI.Tests.Editor` | 항목 수 증감 시 View 재사용·비활성화, 다중 선택, `SetSelected`가 이벤트를 보내지 않음, 선택 표시(Checkmark·테두리) 전환, 좌우 Navigation 연결 |
| `EncounterDebugPanelTests` | `TxTRPG.UI.Tests.Editor` | `SetValues` 무알림, 슬라이더 변경 시 `ValuesChanged` 값, 버튼 이벤트, 값 텍스트 형식(`50%`, `1.0`), 접기·펼치기 |
| `DevelopmentBuildOnlyTests` | `TxTRPG.UI.Tests.Editor` | 판정 함수를 분리하여(`ShouldDisable(bool isDebugBuild)`) Release 조건에서 비활성화, Debug 조건에서 유지 |

새 테스트는 Scene·Prefab을 저장하지 않으므로 `AssetWriting`이 아니다. 실행 시 위 클래스만 필터로 선택하고, 회귀 확인으로 `ModalWindowHost`·`GameWindowService` 관련 기존 테스트 클래스(영향 조사에서 찾은 것)를 함께 실행한다. 전체 Edit Mode 테스트는 실행하지 않는다. 테스트 어셈블리 이름은 실제 `.asmdef`로 확인한다.

## 6. 호환성과 실패 동작

- 세이브 데이터, 입력 바인딩, 플레이어 설정 형식은 바뀌지 않는다. 선택 상태와 디버그 값은 저장하지 않는다.
- 카탈로그가 비어 있거나 모든 항목이 잘못되었으면 토글 바는 비어 있고, 판정은 `NoElementsSelected`가 된다. 게임 진행은 막지 않는다.
- 결과 모달이 없거나 페이지를 찾지 못하면 판정 결과를 경고와 디버그 패널 문구로 알리고 예외를 던지지 않는다.
- Release 빌드에서는 `DevelopmentBuildOnly`가 디버그 패널을 비활성화한다. 토글 바와 판정 규칙은 그대로 동작하며, 디버그 이외의 판정 호출 경로는 후속 작업에서 추가한다.
- 모달이 열려 있는 동안 배경이 입력을 막으므로 토글과 디버그 버튼은 눌리지 않는다. 바깥 클릭은 누른 위치와 뗀 위치가 모두 창 밖이고 드래그가 아닐 때만 닫는다(기존 `ModalWindowHost` 동작).
- `MainArea`가 너비 720 미만에서 세로 배치로 바뀌어도 `MiddleArea` 안에서는 토글 바가 상단 고정 64를 유지한다.
- 디버그 패널은 오버레이이므로 작은 화면에서 다른 UI를 가릴 수 있다. 접기 버튼으로 가릴 면적을 줄일 수 있어야 한다.

## 7. 검증 기준

### 7.1 Edit Mode 테스트

5.9절의 클래스만 필터로 실행하고 결과를 보고한다. 테스트 전후의 Git 상태를 기록하고, 테스트가 작업 범위 밖의 파일을 바꿨다면 작업 전에 변경이 없던 파일만 되돌린다.

### 7.2 Editor 확인

1. 아무 Scene에서 Play를 시작하면 AppScene을 거쳐 `Candidate_MainScene`이 로드되고, `MiddleArea` 상단에 `룬` 버튼 하나가 미선택 상태로 보인다.
2. `룬` 버튼을 마우스 클릭·Submit 입력으로 켜고 끌 수 있고, 선택 상태가 Checkmark와 테두리로 보인다.
3. 디버그 패널이 오른쪽 위에 보이고, 슬라이더를 움직이면 값 텍스트가 바뀐다. `기본값으로`를 누르면 1.2절의 값으로 돌아온다.
4. 선택 없이 `이벤트 발생` → 패널에 `선택된 요소가 없습니다`. 발생 확률 0% → `이벤트 없음 (발생 확률 판정 실패)`. 두 경우 모두 모달이 열리지 않는다.
5. `룬` 선택, 발생 확률 100% → 모달이 열리고 `룬 : 전투`/`룬 : 이벤트`/`룬 : 휴식` 중 하나가 보인다. 가중치를 한 종류만 남기면 그 종류만 나온다.
6. 모달 창 바깥을 클릭하면 닫힌다. 창 안을 클릭하면 닫히지 않는다. 바깥에서 누른 채 끌었다가 놓으면 닫히지 않는다. `Cancel` 입력과 닫기 버튼으로도 닫히며, 닫힌 뒤 포커스가 `이벤트 발생` 버튼으로 돌아온다.
7. `Encounter Roll Play Check` 메뉴의 모든 단계가 통과한다.
8. Game View를 1920×1080, 2560×1080, 1280×720, 1080×1920, 750×1334, 640×1136, 700×400으로 바꿔 토글 바가 `MiddleArea` 상단에 있고 버튼이 잘리지 않는지, 모달 문구와 닫기 버튼이 보이는지 확인한다. Game View 크기를 임시로 추가했다면 확인 후 제거한다.
9. 가능하면 Device Simulator에서 터치로 토글과 모달 바깥 탭 닫기를 확인한다.
10. Release 빌드 동작은 `DevelopmentBuildOnlyTests`로 판정 함수를 검증한다. 실제 Release 빌드를 만들어 확인하지 못했다면 미검증으로 보고한다.

### 7.3 직렬화 변경 검토

적용 후 `serialization-diff-reviewer`로 `.unity`, `.prefab`, `.asset`, `.meta` 변경을 검토한다. 의도한 변경은 `Candidate_MainScene.unity`, 새 Prefab 4개, 새 설정 자산 2개와 각 `.meta`, 새 스크립트·폴더의 `.meta`뿐이다. 작업 전부터 있던 사용자 변경, 룬 Grid 작업의 변경, TMP 동적 글꼴 Atlas 증가 같은 부수 변경은 구분하여 보고한다.

## 8. 문서 갱신

| 문서 | 갱신 내용 |
| --- | --- |
| `DOCS/architecture/encounter-roll.md`(새 문서) | 구성 요소 책임표(Gameplay·Application·UI), 판정 순서와 확률 독립성, 데이터 흐름(토글 바 → 컨트롤러 → Roller → 모달·디버그 패널), Prefab 계층, 요소 추가 방법, `DevelopmentBuildOnly` 정책, 실패 동작, 효과 적용 위치 |
| `DOCS/README.md` | 새 아키텍처 문서 링크 |
| `DOCS/architecture/project-structure.md` | `Gameplay/Runtime/Encounters/`, `Application/Runtime/Encounters/`, `Application/Configuration/Encounters/`, `UI/Runtime/Encounters/`, `UI/Prefabs/Encounters/`와 `Candidate_MainScene`의 `MiddleArea`·`OverlayLayer` 구성 |
| `DOCS/architecture/flexible-layout-panel.md` | `Candidate_MainScene` 계층 예시에 `MiddleArea` 상단 토글 바와 Placeholder 추가 |
| `DOCS/architecture/quick-items-and-game-windows.md` | `EncounterResultModal`이 `ModalWindowHost`·`GameWindowService`·`MessageGameWindowPage`를 재사용한다는 사실 |
| `DOCS/architecture/prefab-rebuild-registry.md` | 제외 설명에 네 Prefab과 제외 이유 추가 |
| `DOCS/development/workflows.md` | 새 테스트 위치와 필터, `Encounter Roll Play Check` 메뉴, 임시 적용 메뉴를 만든 경우 그 절차와 제거 시점, 디버그 utility가 Release 빌드에서 숨겨지는 조건 |
| `DOCS/instructions/README.md` | 구현과 적용을 마치면 이 문서 설명에 완료 날짜와 적용 결과를 덧붙임 |

## 9. 최종 보고

다음을 구분하여 보고한다.

1. 구현 완료: 코드·Prefab·자산·테스트·문서 변경 목록
2. 적용 완료: Editor에서 적용하고 저장한 자산과 확인한 값(`MiddleArea/ContentLayer` 축 설정, 토글 바·Placeholder, 디버그 패널·결과 모달 위치, 컨트롤러 참조)
3. 적용 필요: 사용자가 실행해야 하는 임시 메뉴가 남았다면 `사용자가 수행할 적용 절차` 절과 복사 가능한 순서
4. 검증: 실행한 테스트 필터와 결과, Editor 확인 항목, 확인한 화면 크기, 확인하지 못한 입력 방식·기기·빌드 대상(특히 Release 빌드)
5. 직렬화 검토 결과와 되돌린 파일, 작업 전부터 있던 사용자 변경
6. 후속 작업: 실제 판정 시점 연결, 전투·휴식·랜덤 이벤트 실행 연결, 추가 요소 버튼, 요소별 종류 가중치, 버튼 아이콘과 효과, 게임패드 초기 포커스 정책, `Candidate_MainScene`의 Safe Area·CanvasScaler 정책
