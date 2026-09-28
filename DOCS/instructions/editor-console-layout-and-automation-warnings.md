# Editor Console의 레이아웃·자동화 경고 수정 지침

## 목적과 확정된 방향

사용자가 제공한 Console 경고를 발생 원인별로 진단하고, UI의 안전하지 않은 레이아웃 변경과 준비 전 공간 부족 판정을 수정한다. 이 문서는 구현 계획이며, 문서 작성만으로 코드 수정이나 Editor 적용이 완료된 것은 아니다.

사용자가 확정한 운영 정책은 다음과 같다.

- 일반 Editor와 기존 자동화 연결을 유지한다. 사용 중인 도구를 확인한 뒤 필요한 자동화 세션에만 `-automated`를 적용한다.
- 일반 세션에서 Pipeline 실행 모드 안내 경고는 남을 수 있다. 모든 경고를 숨기는 것을 완료 기준으로 삼지 않는다.
- UnitySkills가 다른 포트에서 정상 시작했다면 실제 연결 대상과 포트 점유 원인을 확인한다. 경고를 없애기 위해 정상 프로세스를 종료하지 않는다.

## 선행 확인과 범위

`AGENTS.md`, `DOCS/README.md`, `DOCS/architecture/character-status-ui.md`, `DOCS/architecture/quick-items-and-game-windows.md`, `DOCS/architecture/panel-startup.md`, `DOCS/development/workflows.md`를 읽는다. 작업 시작 시 변경 중인 파일과 Editor의 미저장 Scene·Prefab, 컴파일 상태를 확인한다.

범위는 아래 경고의 직접 원인, 관련 콜백과 필요한 회귀 검증이다. UI 외형·정렬 정책 재설계, 패키지 제거·일괄 업데이트, 전체 Prefab 재생성, 플레이어 저장 데이터 변경은 제외한다. 기존에 진행 중인 다른 작업을 덮어쓰지 않는다.

`.codegraph/`가 있으면 영향 범위 탐색에 먼저 사용하되 실제 소스로 검증한다. 문서 작성 시 CodeGraph가 일부 파일의 색인 갱신 대기를 보고했으므로 색인 결과만으로 판단하지 않는다.

## 현재 확인한 사실과 미확인 원인

문서 작성 시 `ProjectSettings/ProjectVersion.txt`는 Unity `6000.3.22f1`이다. `Packages/manifest.json`과 `Packages/packages-lock.json`에서 Pipeline은 `0.5.0-exp.1`, UnitySkills는 Git 의존성이다. 재현 시 실제 로드된 패키지와 잠금 파일을 다시 대조한다.

| 경고 | 확인한 코드 경로 | 구분 |
| --- | --- | --- |
| Viewport의 SendMessage 경고 | `ConfigurableScrollbarController.OnValidate → Refresh → ApplyViewportInset`에서 offset을 변경한다. Refresh는 다른 RectTransform 변경과 활성 상태 변경도 수행한다. | 금지된 검증 시점에서 UI를 변경하는 직접 경로가 확인됐다. |
| 체력 바 자식들의 SendMessage 경고 | `HealthBarLayoutController.OnValidate → ApplyLayout → SetIfDifferent`에서 BarRoot의 크기를 변경한다. | 하위 UI로 크기 변경 알림이 전달되는 경로가 확인됐다. 자식별 오류라고 단정하지 않는다. |
| GameMenuPanel의 viewport 0x0 경고 | `LateUpdate → ApplyLayout`에서 콘텐츠를 재배치하고 공간 부족을 검사한다. Viewport의 유효 크기 확인은 없다. | 초기화 순서, 숨김 상태 또는 부모 배치 오류 중 어느 원인인지는 런타임 확인이 필요하다. |
| Pipeline 실행 모드 경고 | 설치된 `EditorPipelineServer.ServerStarted`는 `-automated`가 없고 batch mode가 아닐 때 경고한다. | UI 레이아웃 결함과 별개인 실행 환경 안내이다. |
| UnitySkills 8090 → 8091 경고 | 설치된 `SkillsHttpServer.Start`는 선호 포트 점유 후 대체 포트에서 시작하면 경고한다. | 제공된 로그는 대체 포트 시작 성공을 나타낸다. 점유 프로세스와 현재 연결 성공 여부는 미확인이다. |

패키지 코드의 진단 근거는 현재 캐시의 `EditorPipelineStartup.cs`, `EditorPipelineServer.cs`, `SkillsHttpServer.cs`이다. `Library/PackageCache`는 읽기 전용 진단 대상으로 취급하며 수정 대상으로 삼지 않는다. 잠금 파일과 캐시 식별자가 다르면 실제 로드 버전을 먼저 확인하고 임의로 캐시를 삭제하지 않는다.

## 영향 경로

| 경로 | 확인·수정 책임 |
| --- | --- |
| `Assets/TxTRPG/UI/Runtime/ConfigurableScrollbarController.cs` | 검증과 UI 적용 분리, 예약 공간·가시성·알림 유지 |
| `Assets/TxTRPG/UI/Runtime/Characters/HealthBarLayoutController.cs` | 편집 중 미리보기와 런타임 배치의 안전한 갱신 |
| `Assets/TxTRPG/UI/Runtime/Windows/GameMenuPanel.cs` | 준비 상태, 재갱신, 유효한 공간 부족 진단 |
| `Assets/TxTRPG/UI/Runtime/Windows/GameMenuLayoutGroup.cs` 및 `GameMenuLayout.cs` | 부모·Viewport 측정과 계산 결과의 적용 순서 확인 |
| `Assets/TxTRPG/UI/Runtime/ActionGridPanel.cs` 및 `ActionGridSurfaceLayout.cs` | Scrollbar 연계와 `ViewportLayoutChanged`의 재진입 영향 확인 |
| `Assets/TxTRPG/UI/Tests/Editor/ActionGridPanelTests.cs`, `HealthBarPanelTests.cs`, `GameMenuLayoutTests.cs` | 기존 동작 회귀 검증과 수명주기 테스트 확장 |
| `Assets/Scenes/AppScene.unity`, `Assets/Scenes/TMP_MainScene.unity` | 기존 초기화 경로와 실제 UI 재현 |
| `Packages/manifest.json`, `Packages/packages-lock.json` | 설치 상태 확인. 기본 수정 대상은 아니다. |

참조된 Prefab, Scene 인스턴스와 관련 Editor 미리보기 호출자를 실제 작업 시 추적한다. 경고와 무관한 컴포넌트까지 일괄 변경하지 않는다.

## 구현 요구사항

### 1. OnValidate와 실제 UI 변경을 분리한다

`OnValidate`는 직렬화된 값의 범위 보정과 갱신 필요 상태 기록으로 제한한다. RectTransform 변경, GameObject 활성화, 강제 레이아웃 재계산과 이벤트 전파는 안전한 메인 스레드 갱신 시점으로 이동한다. `GameMenuPanel.OnValidate → QueueRefresh → MarkLayoutForRebuild`도 함께 점검한다.

Unity 공식 문서는 OnValidate가 로딩 스레드 등 메인 스레드 외부에서도 호출될 수 있다고 설명한다. 따라서 단순히 일부 setter만 delayCall로 감싸고 나머지 Unity API 접근을 남기지 않는다. 예약 방식은 기존 Editor 구조를 활용하되, 작업 요청의 전달과 메인 스레드 처리 경계를 명확히 한다. 근거: [Unity OnValidate 문서](https://docs.unity3d.com/kr/current/ScriptReference/MonoBehaviour.OnValidate.html).

- 연속 Inspector 변경은 중복 예약을 합쳐 최신 값으로 적용한다. 지연 작업마다 새 클로저나 장기 이벤트 구독이 누적되지 않게 한다.
- 실행 직전에 대상의 생존 여부, Scene·Prefab 편집 맥락과 필요한 참조를 확인한다. 파괴, 비활성화, Scene 전환, 재컴파일 및 Play Mode 전환 후 오래된 작업이 실행되지 않게 한다.
- 비활성 동안 갱신을 미룬 경우 재활성화 시 최신 설정을 반영한다. 기존 편집 미리보기는 유지하며, 런타임 LateUpdate로 옮기는 것만으로 편집 모드 지원을 대체하지 않는다.
- 체력 바의 OnEnable·크기 변경·부모 변경·애니메이션 적용 경로에서도 재진입과 금지된 호출 시점이 없는지 확인한다. 모든 공개 메서드를 무조건 비동기로 바꾸지 말고 기존 호출자의 즉시 적용 계약을 확인한다.
- 실제 값이 달라질 때만 속성을 변경한다. Scrollbar의 기존 재진입 방어와 Auto·예약 공간 계산 의미를 유지하고 `ViewportLayoutChanged`가 변경 없이 반복 발생하지 않게 한다.
- Editor 전용 처리는 기존 어셈블리 경계 또는 조건부 컴파일로 격리한다. Player 빌드에 UnityEditor 의존성을 추가하지 않는다.
- 영구적인 전체 Scene 검색이나 매 프레임 무조건 강제 재배치를 도입하지 않는다. Prefab 원본이나 무관한 Scene을 자동 저장하지 않는다.

### 2. GameMenuPanel의 준비 상태와 공간 부족을 구분한다

먼저 0x0이 발생하는 객체와 부모 계층, 활성 상태, Canvas 및 부모 레이아웃 적용 순서를 기록한다. 콘텐츠만 강제 재계산해도 부모의 할당 크기가 확정된다고 가정하지 않는다.

유한한 양수 크기가 확보되기 전에는 정상적인 공간 부족 판정을 보류한다. 준비 상태가 바뀌거나 부모 크기가 확정될 때 반드시 다시 갱신되도록 한다. 단순 early return으로 최초 갱신 요청을 잃지 않게 한다.

일시적인 준비 대기는 경고하지 않되, 활성 UI가 계속 0 크기에 머무는 구성 오류는 원인을 진단할 수 있어야 한다. 숨김·접힘 상태와 영구적인 참조·부모 크기 오류를 구분하며, 일정 프레임을 무조건 기다리는 방식이나 무한 재시도로 대신하지 않는다.

유효한 크기에서 실제 공간이 부족하면 기존 진단과 `LayoutInsufficientSpace` 의미를 유지한다. 같은 부족 상태의 경고는 중복 출력하지 않고, 정상 복구 후 다시 부족해지면 새 상태를 진단할 수 있게 한다. 버튼 크기 축소나 배치 모드 변경을 자동으로 도입하지 않는다. 스크롤바 예약 영역 변경 후 최종 가용 크기를 기준으로 판정한다.

### 3. Pipeline 실행 모드 안내를 운영 정책에 따라 처리한다

현재 설치된 패키지에서 확인한 메뉴는 `Window/Pipeline/Settings...`, `Window/Pipeline/Start Server`, `Window/Pipeline/Stop Server`이다. 설정이 없으면 자동 시작 기본값이 사용된다. 이 사실은 자동 시작을 끄라는 요구가 아니다.

실제로 사용하는 자동화 클라이언트와 Pipeline 의존성을 조사한다. 일반 Editor의 시작 옵션과 서버 상태는 유지한다. 필요한 자동화 세션에 한해 설치된 버전에서 `-automated`의 동작과 대화상자 처리 영향을 확인하고 별도 실행 절차를 작성한다. 사용자의 미저장 작업을 보존하고 기존 Editor를 강제로 종료하거나 동일 프로젝트를 중복 실행하지 않는다.

일반 세션에서 해당 안내가 계속 나타나더라도 연결이 정상이고 확정 정책과 일치하면 잔여 안내로 보고한다. Console 필터, 로그 핸들러 또는 패키지 소스 수정으로 메시지를 숨기지 않는다. `-batchmode`를 단순한 경고 제거 수단으로 추가하지 않는다.

### 4. UnitySkills 포트 충돌과 연결 대상을 확인한다

8090과 실제 시작 포트의 수신 프로세스 및 프로젝트를 확인한다. 다른 정상 Editor가 사용 중이면 보존한다. 클라이언트가 8090만 고정 사용하고 있는지 확인하고, 지원되는 인스턴스 탐색으로 대상 프로젝트와 실제 포트를 검증한다. 8091에서 응답한다는 사실만으로 올바른 프로젝트라고 판단하지 않는다.

설치된 소스는 `PreferredPort`와 자동 탐색 범위 8090~8100, 마지막 포트 복원 및 대체 포트 시작을 지원한다. 실제 로드 버전의 설정 UI·저장 범위·복원 정책을 확인한 뒤 충돌하지 않는 설정을 적용한다. 포트 숫자를 게임 코드에 하드코딩하지 않는다. Auto 설정만 바꾸면 재발이 해결된다고 단정하지 않고 Domain Reload와 재실행 후 확인한다.

포트가 모두 사용 중이거나 연결 대상이 불명확하면 명확한 실패 상태로 보고한다. 다른 프로세스를 강제 종료하거나 방화벽을 넓게 개방하지 않는다. 정상적인 다중 Editor 사용으로 대체 포트 경고가 남으면 연결 성공 여부와 함께 구분해서 기록한다.

## 호환성과 적용

직렬화된 필드, 기존 배치 옵션, Scrollbar 정책, 체력 바 정렬·Padding 및 외형 효과를 보존한다. 세이브와 게임 규칙은 변경하지 않는다. 마우스·키보드·컨트롤러·터치의 메뉴 접근과 스크롤 동작에 회귀가 없어야 한다.

코드만 수정하면 되는지, 저장된 Scene·Prefab 참조 수정도 필요한지 확인한다. 필요한 자산만 직접 적용하고 전체 생성기를 기본 적용 수단으로 사용하지 않는다. Editor 자동화가 불가능하면 원인과 남은 적용 절차를 분리해 보고한다. 수동 메뉴가 필요하면 실제 검증한 정확한 메뉴 경로, 실행 순서, 저장 여부, 재실행 가능 여부와 검증 절차를 개발 문서 및 최종 보고에 기록한다.

## 검증 기준

1. 기존 관련 Edit Mode 테스트를 실행하고, Inspector 변경에 해당하는 직렬화 갱신 후 지연 적용까지 기다리는 테스트를 추가한다. OnValidate를 직접 호출하는 테스트만으로 실제 Editor 수명주기를 검증했다고 보고하지 않는다.
2. 연속 값 변경, 대상 파괴·비활성화·재활성화, Scene 전환, Domain Reload 및 Play Mode 전환에서 SendMessage 경고와 지연 작업 예외가 없어야 한다.
3. 체력 바의 Fixed·Stretch, 정렬·Padding과 효과 계층이 유지되고, Scrollbar의 Hidden·Auto·Always 및 공간 예약 정책이 정상이어야 한다.
4. 메뉴가 0x0에서 정상 크기로 전환하면 자동으로 배치되고 허위 공간 부족 경고가 없어야 한다. 양수지만 실제 부족한 크기, 충분한 크기로 복구, 재차 부족한 상태를 각각 검증한다. 지속적인 0 크기 구성 오류도 진단 가능해야 한다.
5. `Assets/Scenes/AppScene.unity`의 기존 실행 흐름을 통해 `Assets/Scenes/TMP_MainScene.unity`에 진입해 메뉴·Bag·Actions·체력 바를 확인한다. 좁은 화면과 넓은 화면, 입력 장치 전환과 스크롤을 확인한다.
6. Pipeline은 일반 세션과 필요한 자동화 세션을 구분해서 결과를 기록한다. UnitySkills는 포트 점유 상황과 재연결·재시작 후 실제 프로젝트 식별 및 연결 성공을 검증한다. 기존 로그 기록을 새 발생으로 오인하지 않도록 재현 시점을 구분한다.
7. 변경된 어셈블리와 사용 가능한 대상 빌드의 컴파일을 확인한다. 실행하지 못한 기기·빌드·수명주기 검사는 미검증으로 명시한다. 로그 전체 무시 설정으로 테스트를 통과시키지 않는다.
8. diff에서 무관한 Scene·Prefab 변경, 생성 폴더 수정 및 패키지 버전 변경이 없는지 확인한다.

## 문서 갱신과 최종 보고

구현 후 `DOCS/architecture/character-status-ui.md`, `DOCS/architecture/quick-items-and-game-windows.md`에 실제 변경된 갱신 수명주기를 반영한다. 운영 설정·실행·검증 절차는 `DOCS/development/workflows.md`에 기록한다. 주요 어셈블리나 책임 경계가 실제로 바뀐 경우에만 `DOCS/architecture/project-structure.md`도 갱신한다.

최종 보고에는 경고별 원인, 수정 파일, Editor 적용 여부, 수행한 테스트와 미검증 항목을 구분한다. 일반 모드 Pipeline 안내 및 정상적인 포트 대체 안내가 남았다면 이유와 연결 상태를 명시한다. 사용자 수동 적용이 남으면 `사용자가 수행할 적용 절차`에 완전한 순서를 text 코드 블록으로 제공한다. 남은 수동 단계가 없으면 없다고 명시한다.