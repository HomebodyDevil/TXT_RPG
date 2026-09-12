# TMP_MainScene 직접 Play를 AppScene 시작 경로와 통일하기

## 목적과 범위

TMP_MainScene을 편집 중인 상태에서 Play를 눌러도 AppScene을 직접 열고 Play한 경우와 같은 초기화 및 TMP_MainScene 표시 결과를 얻도록 합니다.

Editor 전용 Play 시작 정책을 추가하며 런타임 Bootstrap을 중복 구현하지 않습니다. AppRoot 복제, TMP_MainScene 안의 별도 PlayerSession, DontDestroyOnLoad 자동 관리자 추가, 게임 저장 데이터 변경은 제외합니다.

## 사전 확인과 현재 근거

AGENTS.md, DOCS/architecture/scene-transition.md, DOCS/architecture/player-session.md, DOCS/development/workflows.md를 읽습니다.

- Assets/TxTRPG/SceneTransition/Runtime/AppSceneRoot.cs는 기존 AppScene의 SceneTransitionService를 사용하여 InitialContentScenePath를 로드합니다.
- 기본 InitialContentScenePath는 Assets/Scenes/TMP_MainScene.unity입니다.
- DOCS/architecture/scene-transition.md는 AppScene을 제품의 시작 Scene으로 정의합니다.
- 현재 조사한 Application/Editor 및 SceneTransition/Editor에서는 playModeStartScene 설정을 찾지 못했습니다. 구현 전에 다른 Editor 코드와 프로젝트별 설정에서도 중복 정책이 없는지 확인합니다.

영향 경로는 다음과 같습니다.

- Assets/Scenes/AppScene.unity
- Assets/Scenes/TMP_MainScene.unity
- Assets/TxTRPG/SceneTransition/Runtime/AppSceneRoot.cs
- Assets/TxTRPG/SceneTransition/Editor/
- Assets/TxTRPG/Application/Runtime/Players/PlayerSessionHost.cs
- Assets/TxTRPG/SceneTransition/Tests/Editor/

## 권장 방식

EditorSceneManager.playModeStartScene 등 현재 설치된 Unity 버전에서 지원하는 Editor 시작 Scene 기능을 확인하여 사용합니다.

기본 동작은 다음과 같습니다.

1. 편집 화면은 TMP_MainScene을 유지합니다.
2. Play 시작 Scene은 AppScene으로 지정합니다.
3. AppScene의 기존 초기화가 실행됩니다.
4. 기존 SceneTransitionService가 TMP_MainScene을 로드하고 준비·Fade·입력 허용을 처리합니다.
5. Play 종료 후 기존 편집 Scene 구성으로 돌아옵니다.

이 방식은 TMP_MainScene의 Awake가 먼저 실행된 후 뒤늦게 AppScene을 붙이는 방식과 다릅니다. 플레이어 상태가 필요한 UI가 잘못된 순서로 먼저 실행되지 않도록 처음부터 정식 시작 경로를 사용합니다.

Unity 아키텍처 지침에 따라 단일 초기화 경로를 유지하며, 필요한 Editor 정책만 추가합니다.

## 설정과 적용 정책

- 프로젝트의 기본 Editor Play 정책은 AppScene 시작으로 구성합니다.
- Inspector 또는 기존 Editor 설정 UI에서 개발자가 정책을 끄고 현재 Scene으로 직접 실행할 수 있게 합니다. 이는 반복 사용하는 개발 설정이므로 작은 설정 UI는 허용됩니다.
- 전역 Play 시작 Scene 지정은 다른 Scene을 편집할 때에도 영향을 줄 수 있음을 명시합니다. 다른 Scene의 단독 테스트는 명시적 비활성 옵션으로 지원합니다.
- 프로젝트별 설정을 사용하고 다른 Unity 프로젝트의 설정을 변경하지 않습니다.
- 자동 동기화가 필요하면 Editor 로드·설정 변경 시 가벼운 시작 Scene 설정만 적용합니다. Scene 열기·저장·Prefab 재생성을 컴파일 콜백에 연결하지 않습니다.
- 무한 재컴파일, 반복 이벤트 구독, Play 재시작 루프를 만들지 않습니다.
- 초기화 설정의 원본은 기존 AppSceneRoot입니다. 현재 편집 Scene을 근거로 InitialContentScenePath를 매번 바꾸지 않습니다.
- 현재 AppScene의 초기 콘텐츠가 TMP_MainScene인지 검증합니다. 다른 값이 의도적으로 설정돼 있다면 자동 덮어쓰기하지 말고 충돌을 설명합니다.
- loadInitialContentOnStart가 비활성이라면 의도와 현재 요구의 충돌을 확인합니다.
- 기존에 다른 playModeStartScene이 지정돼 있다면 사용자 설정을 조용히 덮어쓰지 않습니다. 변경 사실과 복원 정책을 명시합니다.
- 정책 해제 시 자신이 지정한 값만 해제 또는 이전 값으로 복원합니다. 이후 사용자가 지정한 값을 오래된 백업으로 덮어쓰지 않습니다.
- AppScene이 삭제·이동되면 검증 오류를 표시합니다. 잘못된 현재 Scene을 대신 실행하여 정상 동작처럼 보이게 하지 않습니다.
- 가능하면 SceneAsset 참조를 Editor 설정에 사용하고 경로 문자열이 필요한 경계에서만 해석합니다.

## 미저장 변경과 편집 상태 보호

- Play 전 미저장 Scene과 Prefab Stage 상태를 확인합니다.
- AppScene이 로드하는 TMP_MainScene은 디스크의 자산을 기준으로 할 수 있으므로, 미저장 편집 내용이 실행 결과에 포함된다고 가정하지 않습니다.
- 변경 내용을 포함해야 한다면 Unity의 명시적인 저장 확인을 사용합니다. 자동 저장·폐기하지 않습니다.
- 저장 확인 취소 시 Play 진입도 취소하고 편집 상태를 유지합니다.
- 열려 있던 Scene 목록·활성 Scene·Additive 구성·Prefab Stage를 무작정 닫거나 재생성하지 않습니다.
- Unity의 기본 편집 상태 복원을 우선 사용하고, 별도 복원이 필요할 때만 저장된 상태를 안전하게 관리합니다.
- Play 중 변경은 일반적인 Unity 규칙대로 처리하며, 종료 후 자동으로 운영 Scene이나 Prefab에 Apply하지 않습니다.
- 기존 UI 위치, Grid 설정, 메뉴·모달 연결 및 저장 파일을 변경하지 않습니다.

## 초기화와 실행 환경

- AppScene, AppRoot, PlayerSessionHost 및 콘텐츠 Scene이 각각 의도한 수만 생성되는지 확인합니다.
- 기존 세션 준비, 초기 콘텐츠 로드, Addressables, 모달 데이터 연결과 화면 전환을 그대로 사용합니다.
- 컴파일·준비 오류를 무시하거나 임의 지연 시간으로 초기화 완료를 추정하지 않습니다.
- AppScene과 TMP_MainScene을 동시에 편집 중이어도 플레이 시 콘텐츠가 중복 로드되지 않는지 확인합니다.
- Domain Reload와 Scene Reload의 Enter Play Mode Options 조합을 조사합니다. 지원하지 않는 조합은 명확히 진단하고 프로젝트 설정을 임의로 바꾸지 않습니다.
- Domain Reload 비활성 상태에서 정적 Singleton과 이벤트가 이전 실행 상태를 유지하면 관련 기존 초기화 정리만 보완합니다. 별도 Bootstrap으로 우회하지 않습니다.
- Unity Test Runner의 테스트 Scene 실행을 AppScene으로 강제 변경하지 않도록 테스트와 일반 Play 시작 정책을 분리합니다.
- Editor 전용 코드는 Editor 어셈블리에 두고 Player 빌드에 UnityEditor 의존성이 포함되지 않게 합니다.
- Build Settings의 제품 시작 순서는 기존 AppScene 정책을 유지합니다. Editor 편의를 위해 빌드 Scene 목록을 매 실행 변경하지 않습니다.

## 구현 순서

1. 기존 시작 Scene 설정과 Bootstrap·테스트 실행 경로를 조사합니다.
2. 최소한의 프로젝트별 Editor 시작 정책과 활성/비활성 설정을 구현합니다.
3. AppScene과 초기 콘텐츠의 유효성 및 미저장 변경 처리 규칙을 적용합니다.
4. 현재 프로젝트에 설정을 적용하고 실제 TMP_MainScene에서 Play를 시작합니다.
5. 종료·재실행과 편집 상태 복원을 검증합니다.

가능하면 AI가 Editor에서 직접 적용하고 검증합니다. 임시 적용 메뉴가 필요한 경우 최신 AGENTS.md에 따라 허용하되, 남은 수동 절차를 코드 블록으로 별도 안내합니다. 적용 메뉴를 만들었다는 이유만으로 실제 실행 결과가 확인됐다고 보고하지 않습니다.

## 검증 기준

동일한 저장 데이터와 설정을 사용하여 다음 두 실행을 비교합니다.

- AppScene을 열고 Play하여 TMP_MainScene에 도착합니다.
- TMP_MainScene을 열고 Play하여 같은 경로로 도착합니다.

비교 항목은 다음과 같습니다.

- AppScene과 TMP_MainScene의 로드 개수와 활성 콘텐츠 Scene입니다.
- AppRoot 및 PlayerSessionHost의 개수와 준비 완료 상태입니다.
- 실제 플레이어·인벤토리 상태와 UI 바인딩입니다.
- 초기 Fade·입력 허용 순서입니다.
- Bag 및 System 모달의 실제 열기·닫기입니다.
- 오류·Missing Script·중복 이벤트 여부입니다.

추가로 다음을 검사합니다.

- 두 번 이상 Play/Stop 반복과 Domain Reload 설정별 동작입니다.
- Play 종료 후 TMP_MainScene의 편집 상태·활성 Scene·배치 보존입니다.
- 미저장 변경 저장 승인·취소 및 안전한 재실행입니다.
- 다른 Scene 단독 실행 옵션과 Test Runner 회귀입니다.
- AppScene 누락, 잘못된 초기 콘텐츠, 컴파일 오류 상황에서 명확한 안내입니다.
- 설정 적용 후 Editor 재시작에도 의도한 정책이 유지되는지입니다.

테스트에 사용자 저장 데이터의 파괴적 수정이 필요하면 격리된 저장소를 사용합니다. 메뉴 기능 검증을 위해 사용자 아이템을 임의로 소비하지 않습니다.

## 문서와 최종 보고

DOCS/architecture/scene-transition.md에 Editor 시작 정책과 제품 실행 경로의 관계를 기록합니다. DOCS/development/workflows.md에는 설정 위치, 끄는 방법, 미저장 변경 처리, 테스트 실행과 복구 방법을 추가합니다.

최종 보고에는 실제 적용한 정책, 설정 위치, TMP_MainScene 직접 Play 결과, 편집 상태 복원 결과, 지원하는 Enter Play Mode Options 및 미검증 항목을 포함합니다. 수동 실행할 Tools 메뉴가 남았다면 정확한 경로·순서·확인 기준을 별도 코드 블록으로 안내합니다.

